using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Claim;

/// <summary>
/// プレビュー計算の共有結果。<see cref="Result"/>が<c>null</c>のとき算定はスキップされており、
/// <see cref="Issues"/>が必ず理由を運ぶ。<see cref="PreviewHash"/>と<see cref="DetailDrafts"/>は
/// 算定成立時のみ非空。
/// </summary>
internal sealed record ClaimPreviewComputation(
    string ClaimMasterVersion,
    IReadOnlyList<ClaimPreparationIssue> Issues,
    ClaimCalculationResult? Result,
    IReadOnlyList<ClaimFinalizationDetailDraft> DetailDrafts,
    string PreviewHash,
    // 事前登録済みの将来版で必要になる項目（確定は止めない警告）。
    IReadOnlyList<ClaimUpcomingSpecificationIssue> UpcomingSpecificationIssues = null!,
    // 体制届で宣言されたが当月に有効なマスタ行が無いキー（確定は止めない警告。ADR 0049）。
    IReadOnlyList<string> CapabilityCoverageWarnings = null!,
    // 体制届で宣言されたキーは当月に有効だが、それを要求する行がすべて他のcapabilityキーも
    // 要求していて、宣言集合では1行も成立しないキー（確定は止めない警告。ADR 0049の一般化）。
    IReadOnlyList<string> IncompleteCapabilityDeclarationWarnings = null!)
{
    public IReadOnlyList<ClaimUpcomingSpecificationIssue> UpcomingSpecificationIssues { get; init; } =
        UpcomingSpecificationIssues ?? [];

    public IReadOnlyList<string> CapabilityCoverageWarnings { get; init; } =
        CapabilityCoverageWarnings ?? [];

    public IReadOnlyList<string> IncompleteCapabilityDeclarationWarnings { get; init; } =
        IncompleteCapabilityDeclarationWarnings ?? [];
}

/// <summary>
/// snapshot読取→readiness評価→算定→snapshot envelope→PreviewHash を
/// Calculate/Close両use caseで共有する内部パイプライン（Closeはこの結果を照合・確定する）。
/// </summary>
internal sealed class ClaimPreviewPipeline(
    IClaimCalculationSnapshotReader snapshotReader,
    IClaimMasterProvider masterProvider,
    IOfficeRepository officeRepository,
    IClaimBillingTokenProvider tokenProvider,
    ClaimPreparationReadiness readiness,
    IClaimCsvSpecificationVersions specificationVersions,
    IClaimGenericFieldCatalog genericFieldCatalog)
{
    private static readonly ClaimSnapshotValidationCodecV2 SnapshotCodec = new();

    public async Task<ClaimPreviewComputation> ComputeAsync(
        Guid officeId, ServiceMonth serviceMonth, CancellationToken ct)
    {
        var snapshot = await snapshotReader.ReadAsync(officeId, serviceMonth, ct);
        var office = await officeRepository.FindByIdAsync(officeId, ct);

        Domain.Logic.Claim.Models.ClaimMasterRelease? release = null;
        try
        {
            release = masterProvider.ResolveVersion(serviceMonth);
        }
        catch (ClaimMasterPolicyUnavailableException)
        {
            // 版が未定義の月はreadiness issue（MasterVersionUnavailable）として可視化する。
        }

        var tokens = office is null ? null : tokenProvider.Resolve(office, snapshot.Profile, serviceMonth);
        var currentVersionForGeneric = specificationVersions.Current;
        var contextResult = ClaimPreparationContextBuilder.Build(
            snapshot,
            office,
            masterVersionAvailable: release is not null,
            declaredGenericNames: [.. genericFieldCatalog
                .GetDeclarations(currentVersionForGeneric)
                .Select(declaration => declaration.Name)]);
        // readiness は「現行版の要件」で評価する（確定時に記録する版と同じ出所）。
        var currentVersion = currentVersionForGeneric;
        var readinessResult = readiness.Evaluate(contextResult.Context, currentVersion);

        // 事前登録済みの将来版でも評価し、現行版との差だけを情報として集める。
        // IsReady には影響させない（将来版の要求で今月の確定を止めない／緩めない）。
        // 比較は (受給者, 項目) 単位。同じ項目で issue code だけが変わる場合は「変化なし」として扱う
        // （両方向に出して二重に見せない）。
        var upcomingIssues = specificationVersions.UpcomingVersions
            .SelectMany(version => DiffAgainstUpcoming(
                version, readinessResult.Issues, readiness.Evaluate(contextResult.Context, version).Issues))
            .ToArray();
        var requestResult = ClaimCalculationRequestBuilder.Build(snapshot, serviceMonth, tokens);

        var issues = Normalize(
            contextResult.Issues
                .Concat(readinessResult.Issues)
                .Concat(requestResult.Issues));
        var claimMasterVersion = release?.Version.Value ?? "";

        // ADR 0049: 体制届で宣言されたキーは、readiness不成立やrequest構築の失敗に
        // 関わらず snapshot から直接解決できる（request構築の成否に依存しない。
        // ClaimCalculationRequestBuilder.ResolveDeclaredOfficeCapabilityKeys参照）。
        // 宣言キーが1件も無ければ比較の必要が無いため、masters解決自体を試みない
        // （Execute_returns_issues_and_skips_calculation_when_not_readyが固定する
        // 「無関係な理由でのnot-readyでは算定マスタに触れない」という既存の遅延評価を保つ）。
        // 宣言キーがある場合だけ、readiness不成立に関わらずここでmastersを先読みして
        // 警告を計算する（ADR 0041のUpcomingSpecificationIssuesと同じ理由:
        // ここで落とすと、無関係な理由でnot-readyな月は警告が消え、運用者が気付けない）。
        var declaredCapabilityKeys = ClaimCalculationRequestBuilder.ResolveDeclaredOfficeCapabilityKeys(
            snapshot, serviceMonth);
        ClaimCalculationMasterBundle? masters = null;
        IReadOnlyList<string> capabilityCoverageWarnings = [];
        IReadOnlyList<string> incompleteCapabilityDeclarationWarnings = [];
        if (declaredCapabilityKeys.Count > 0 && release is not null)
        {
            masters = masterProvider.ResolveCalculationMasters(serviceMonth);
            capabilityCoverageWarnings = OfficeCapabilityCoveragePolicy.FindUncoveredKeys(
                declaredCapabilityKeys,
                OfficeCapabilityCoveragePolicy.ExtractCapabilityValues(masters.ConditionDefinitions),
                masterProvider.AllOfficeCapabilityConditionValues());
            // 隣にある別の穴（本タスク）: キーは当月に生きているが、それを要求する行がすべて
            // 他のcapabilityキーも要求していて宣言集合では1行も成立しない場合。FindUncoveredKeys
            // とは排反（前者は「当月に無い」が前提）なので、同じ入口で並行に計算する。
            incompleteCapabilityDeclarationWarnings = OfficeCapabilityCoveragePolicy.FindUnsatisfiableDeclaredKeys(
                declaredCapabilityKeys, BuildMonthCapabilityRows(masters));
        }

        if (issues.Length > 0 || requestResult.Request is not { } request || release is null)
        {
            // 算定不成立でも「次の施行分での変更」「体制届カバレッジ」は運ぶ。緩む方向・無音の
            // 加算欠落はまさに not-ready のときにも起き得るため、ここで落とすと気付けない。
            return new ClaimPreviewComputation(
                claimMasterVersion, issues, null, [], "", upcomingIssues, capabilityCoverageWarnings,
                incompleteCapabilityDeclarationWarnings);
        }

        // 宣言キーが無く上でmastersを解決していなければ、ここで初めて解決する
        // （従来どおり、ready経路でのみ必要になる遅延評価）。
        masters ??= masterProvider.ResolveCalculationMasters(serviceMonth);

        // Task 13 (ADR 0023): 経過措置（対象月のtransition-rules行 × profileの版・R8状態・
        // 版付きoption）の不一致は算定前にフェイルクローズする。R8-06境界で旧版profileや
        // R6区分の残留による無検証の単価請求を生成しない（推測しない）。
        var transitionIssues = OfficeClaimProfileTransitionGuard.Validate(
            masters.TransitionRules, snapshot.Profile);
        if (transitionIssues.Count > 0)
        {
            return new ClaimPreviewComputation(
                claimMasterVersion, Normalize(transitionIssues), null, [], "", upcomingIssues,
                capabilityCoverageWarnings, incompleteCapabilityDeclarationWarnings);
        }

        var result = ClaimCalculator.Calculate(masters, request);
        // 版文字列は PreviewHash に入る。プレビューと確定で同じ出所（現行版）を使わないと
        // hash が一致せず確定できない。
        var csvSpecificationVersion = currentVersion;
        var detailDrafts = BuildDetailDrafts(
            snapshot, serviceMonth, claimMasterVersion, request, result, csvSpecificationVersion);
        var previewHash = ClaimPreviewHashing.Compute(
            officeId, serviceMonth, claimMasterVersion, result, detailDrafts, csvSpecificationVersion);
        return new ClaimPreviewComputation(
            claimMasterVersion, issues, result, detailDrafts, previewHash, upcomingIssues,
            capabilityCoverageWarnings, incompleteCapabilityDeclarationWarnings);
    }

    /// <summary>
    /// 当月に有効な各service-code行のConditionSelectorsをConditionDefinitionsへ引き当て、
    /// <see cref="OfficeCapabilityCoveragePolicy.ExtractCapabilityValueSets"/>で
    /// office-capability種別だけを1行分ずつ取り出す（<see cref="OfficeCapabilityCoveragePolicy.FindUnsatisfiableDeclaredKeys"/>
    /// の入力を組み立てる）。未知のselector（本来は起こらない。ロード時のvalidatorが保証する）は
    /// 警告計算という非ブロッキング経路の性質上、例外にせず単に無視する。
    /// </summary>
    private static IReadOnlyList<IReadOnlySet<string>>[] BuildMonthCapabilityRows(
        ClaimCalculationMasterBundle masters)
    {
        var conditionsByKey = masters.ConditionDefinitions
            .ToDictionary(condition => condition.Key, condition => condition, StringComparer.Ordinal);

        return masters.ServiceCodes
            .Select(row =>
            {
                var rowConditions = new List<ClaimConditionDefinition>();
                foreach (var selector in row.ConditionSelectors)
                {
                    if (conditionsByKey.TryGetValue(selector, out var condition))
                        rowConditions.Add(condition);
                }

                return OfficeCapabilityCoveragePolicy.ExtractCapabilityValueSets(rowConditions);
            })
            .ToArray();
    }

    /// <summary>
    /// 現行版と将来版の readiness 結果を突き合わせ、<b>両方向</b>の変化を返す。
    /// 「次の施行分で必要になる項目」（締まる方向）だけでなく、
    /// 「次の施行分では不要になる項目」（緩む方向）も示す。緩む方向を伏せると、運用者は
    /// 次の施行分まで待てば入力不要な項目のために入力させられていることに気付けない。
    /// </summary>
    private static IEnumerable<ClaimUpcomingSpecificationIssue> DiffAgainstUpcoming(
        string upcomingVersion,
        IReadOnlyList<ClaimPreparationIssue> currentIssues,
        IReadOnlyList<ClaimPreparationIssue> upcomingVersionIssues)
    {
        var currentTargets = currentIssues.Select(Target).ToHashSet();
        var upcomingTargets = upcomingVersionIssues.Select(Target).ToHashSet();

        var becomesRequired = upcomingVersionIssues
            .Where(issue => !currentTargets.Contains(Target(issue)))
            .Select(issue => new ClaimUpcomingSpecificationIssue(
                upcomingVersion, ClaimUpcomingSpecificationChange.BecomesRequired, issue));
        var becomesOptional = currentIssues
            .Where(issue => !upcomingTargets.Contains(Target(issue)))
            .Select(issue => new ClaimUpcomingSpecificationIssue(
                upcomingVersion, ClaimUpcomingSpecificationChange.BecomesOptional, issue));

        return becomesRequired.Concat(becomesOptional);
    }

    private static (Guid? RecipientId, string FieldCode) Target(ClaimPreparationIssue issue) =>
        (issue.RecipientId, issue.FieldCode);

    private static ClaimFinalizationDetailDraft[] BuildDetailDrafts(
        ClaimCalculationSnapshot snapshot,
        ServiceMonth serviceMonth,
        string claimMasterVersion,
        ClaimCalculationRequest request,
        ClaimCalculationResult result,
        string csvSpecificationVersion)
    {
        var sourceByRecipient = request.Recipients.ToDictionary(source => source.RecipientId);
        return result.Details
            .Select(detail =>
            {
                var source = sourceByRecipient[detail.RecipientId];
                var claimInput = snapshot.EffectiveClaimInputs
                    .Single(input => input.RecipientId == detail.RecipientId);
                var inputEnvelope = SnapshotCodec.CreateEnvelope(
                    ClaimRecipientSnapshotWriter.WriteInputSnapshot(
                        serviceMonth, request, source, claimInput));
                var calculationEnvelope = SnapshotCodec.CreateEnvelope(
                    ClaimRecipientSnapshotWriter.WriteCalculationSnapshot(
                        serviceMonth, claimMasterVersion, detail));
                return new ClaimFinalizationDetailDraft(
                    detail.RecipientId,
                    ClaimSnapshotValidationCodecV2.SchemaVersionValue,
                    claimMasterVersion,
                    csvSpecificationVersion,
                    ClaimFinalizationVersions.ReportSpecificationVersion,
                    ClaimFinalizationVersions.SnapshotApplicationVersion,
                    inputEnvelope,
                    calculationEnvelope,
                    detail.TotalUnits,
                    detail.TotalCostYen,
                    detail.BenefitYen,
                    detail.BurdenYen);
            })
            .ToArray();
    }

    private static ClaimPreparationIssue[] Normalize(
        IEnumerable<ClaimPreparationIssue> issues)
        => issues
            .Distinct()
            .OrderBy(issue => issue.RecipientId.HasValue)
            .ThenBy(issue => issue.RecipientId)
            .ThenBy(issue => issue.Code)
            .ThenBy(issue => issue.FieldCode, StringComparer.Ordinal)
            .ToArray();
}
