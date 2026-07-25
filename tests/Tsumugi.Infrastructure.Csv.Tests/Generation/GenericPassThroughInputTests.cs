using System.Text;
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Infrastructure.Csv.Generation;
using Tsumugi.Infrastructure.Csv.Mapping;
using Tsumugi.Infrastructure.Csv.Specifications;
using Tsumugi.Infrastructure.Csv.Writer;

namespace Tsumugi.Infrastructure.Csv.Tests.Generation;

/// <summary>
/// 汎用 pass-through 入力（ADR 0042）の受け入れ基準:
/// <b>C# を 1 行も書かず、spec の宣言だけで</b>「readiness で要求され・CSV へ出る」状態になること。
/// </summary>
/// <remarks>
/// 実証のため、埋め込み catalog の 1 項目（例外利用日の対象期間・開始）の mapping だけを
/// <c>storage: "generic"</c> へ差し替えた catalog を組む。項目定義（桁数・属性・requiredWhen）は
/// 一切変えないので、宣言の差し替えだけで動くことを示せる。
/// </remarks>
public sealed class GenericPassThroughInputTests
{
    /// <summary>
    /// 実証に使う項目。<c>requiredWhen: "always"</c> の項目を選ぶ（条件付き項目だと未入力時に
    /// 条件が偽で評価に入らず、「宣言だけで出る／未入力なら止まる」の両方を示せない）。
    /// </summary>
    private const string DemoFieldId = "provider:J121:04:009";

    /// <summary>同じ値を運ぶ2項目目（同じレコード・同じ属性・同じ桁数）。</summary>
    private const string SecondDemoFieldId = "provider:J121:04:027";

    private const string DemoName = "DemoPassThroughDays";

    private const string DemoLabel = "実証用の転記項目";

    /// <summary>共通編の登録済み SHA-256（証跡台帳が要求する pass-through claim に使う）。</summary>
    private const string CommonSha256 =
        "f6932c52a4fd1044cbdd5aa752baf52e1c2e1eda106d9fa54991423fbd04a9a9";

    [Fact]
    public void A_generic_declaration_alone_is_accepted_by_the_specification()
    {
        var catalog = CatalogWithGenericField();

        catalog.MappingByFieldId[DemoFieldId].Storage.Should().Be("generic");
        catalog.MappingByFieldId[DemoFieldId].TargetModel.Should().Be("ClaimGenericInput");
        catalog.MappingByFieldId[DemoFieldId].MigrationRequired.Should().BeFalse(
            "汎用入力は Domain の型付き列を増やさないので migration は要らない");
    }

    // NOTE(teeth): 証跡台帳に pass-through claim が無いまま generic を宣言したら読み込みで落ちる。
    // これが「算定に効く項目が汎用側へ流れる」ことへの歯。
    [Fact]
    public void A_generic_declaration_without_a_pass_through_claim_fails_closed()
    {
        var act = () => CatalogWithGenericField(withPassThroughClaim: false);

        act.Should().Throw<InvalidDataException>().WithMessage("*pass-through*");
    }

    // NOTE(teeth): 汎用入力は「受給者×サービス提供年月で1個」しか保存しない。日ごと明細に宣言すると
    // 同じ値が全日へ複製され、ファイル単位のレコードでは受給者行が無く生成が落ちる。宣言時に拒否する。
    [Theory]
    [InlineData("provider:J611:02:027")] // 日ごと明細（日ごとに1行）
    [InlineData("provider:J121:03:007")] // サービス明細（サービスコードごとに1行）
    [InlineData("provider:J111:01:005")] // 請求書（ファイル全体で1行）
    [InlineData("common:outer:control:006")] // 共通編の外側レコード
    public void A_generic_declaration_outside_the_monthly_scope_fails_closed(string fieldId)
    {
        var act = () => CatalogWithGenericField(fieldId: fieldId);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*once per recipient and service month*");
    }

    // NOTE(teeth): 実装しているのは請求入力画面だけ。他の画面を宣言しても入力欄が無く、
    // readiness と生成だけが不足を訴え続ける状態になる。
    [Fact]
    public void A_generic_declaration_for_an_unimplemented_surface_fails_closed()
    {
        var act = () => CatalogWithGenericField(uiSurface: "ServicePerformanceRecordView");

        act.Should().Throw<InvalidDataException>().WithMessage("*unsupported uiSurface*");
    }

    // NOTE(teeth): 同じ値を2項目が運ぶ宣言は1欄にまとめる。まとめないと同名の欄が並び、
    // 保存時に名前をキーにした辞書が重複で落ちる。
    [Fact]
    public void Two_fields_carrying_the_same_value_become_one_input_field()
    {
        var declarations = CsvGenericFieldCatalog.DeclarationsOf(
            CatalogWithGenericField(alsoDeclare: SecondDemoFieldId));

        var declaration = declarations.Should().ContainSingle().Subject;
        declaration.FieldIds.Should().Equal(DemoFieldId, SecondDemoFieldId);
    }

    [Fact]
    public void The_same_name_declared_with_a_different_label_fails_closed()
    {
        var act = () => CatalogWithGenericField(
            alsoDeclare: SecondDemoFieldId, secondLabel: "別の見せ方");

        act.Should().Throw<InvalidDataException>().WithMessage("*conflicting labels*");
    }

    // NOTE(teeth): 日付・年月は<b>CSV へ出る形</b>（8桁・6桁）で受ける。区切り記号つきで受けると
    // 項目定義の桁数を必ず超え、超えなかったとしても encoder の「数字のみ」検査で落ちる
    // （＝宣言できても永久に出力できない項目になる）。
    [Theory]
    [InlineData("provider:J121:05:009", "20260401", true)]
    [InlineData("provider:J121:05:009", "2026-04-01", false)]
    [InlineData("provider:J121:05:009", "20261301", false)]
    [InlineData("provider:J121:04:031", "202604", true)]
    [InlineData("provider:J121:04:031", "2026-04", false)]
    public void A_declared_date_value_is_validated_in_the_shape_the_csv_requires(
        string fieldId, string value, bool accepted)
    {
        var catalog = CatalogWithGenericField(fieldId: fieldId);
        var generic = CsvGenericFieldCatalog.ForCatalog(catalog);

        var act = () => generic.ValidateValue(catalog.Version, DemoName, value);

        if (accepted) act.Should().NotThrow();
        else act.Should().Throw<ClaimGenericValueInvalidException>();
    }

    [Fact]
    public void The_declaration_becomes_an_input_field_without_any_code_change()
    {
        var declarations = CsvGenericFieldCatalog.DeclarationsOf(CatalogWithGenericField());

        var declaration = declarations.Should().ContainSingle().Subject;
        declaration.Name.Should().Be(DemoName);
        declaration.FieldIds.Should().Equal(DemoFieldId);
        declaration.Label.Should().Be("実証用の転記項目");
        declaration.DataType.Should().Be("numeric", "項目定義と一致する");
        declaration.MaxBytes.Should().Be(2);
        declaration.UiSurface.Should().Be("ClaimInputView");
    }

    [Fact]
    public void The_declaration_becomes_a_readiness_requirement_without_any_code_change()
    {
        var requirements = ClaimInputRequirementProvider
            .ForCatalog(CatalogWithGenericField()).GetRequirements();

        requirements.Should().Contain(requirement =>
            requirement.TargetPath == "ClaimGenericInput." + DemoName
            && requirement.FieldIds.Contains(DemoFieldId));
    }

    // NOTE(teeth): 宣言だけで CSV へ出ること。ここが RED になると「新項目ごとに C# を触る」状態に戻る。
    [Fact]
    public void The_declared_value_reaches_the_csv_without_any_code_change()
    {
        var dto = ClaimCsvFixtures.Normal();
        var withGenericValue = dto with
        {
            Recipients =
            [
                dto.Recipients[0] with
                {
                    GenericInputs = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [DemoName] = "12",
                    },
                },
            ],
        };

        var bytes = new ClaimCsvGenerator(CatalogWithGenericField()).Generate(withGenericValue).Bytes;

        FieldAt(bytes, position: 9).Should().Be("12");
    }

    // NOTE(teeth): 宣言された必須項目が未入力なら、出力側でも項目 ID 付きで fail-close する
    // （型付き列と同じ扱い。汎用側だから緩くなる、ということが無い）。
    [Fact]
    public void An_unentered_declared_value_fails_closed_at_generation()
    {
        var act = () => new ClaimCsvGenerator(CatalogWithGenericField())
            .Generate(ClaimCsvFixtures.Normal());

        act.Should().Throw<Application.Claim.ClaimCsvExportFailedException>()
            .Where(exception => exception.FieldId == DemoFieldId
                && exception.Reason == "MissingRequired");
    }

    /// <summary>
    /// 集計情報レコードの項番 <paramref name="position"/> の値。行頭には外側データレコードの
    /// レコード種別と連番が付くため、項番 N は 0 起点で N+1 の位置に来る。
    /// </summary>
    private static string FieldAt(byte[] bytes, int position) => SummaryRecordFields(bytes)[position + 1];

    private static string[] SummaryRecordFields(byte[] bytes) => CsvCellEncoder.Cp932
        .GetString(bytes)
        .Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
        .Single(line => line.Contains(",J121,04,", StringComparison.Ordinal))
        .Split(',');

    /// <summary>
    /// 埋め込み catalog の指定項目の mapping を <c>storage: "generic"</c> へ差し替えた catalog。
    /// 項目定義（桁数・属性・requiredWhen）は変えない（宣言の差し替えだけで動くことを示すため）。
    /// </summary>
    private static CsvSpecificationCatalog CatalogWithGenericField(
        bool withPassThroughClaim = true,
        string fieldId = DemoFieldId,
        string uiSurface = "ClaimInputView",
        string? alsoDeclare = null,
        string? secondLabel = null)
    {
        var source = CsvSpecificationLoader.LoadEmbedded();
        var fieldsById = source.CommonRecords.Concat(source.ProviderRecords)
            .SelectMany(record => record.Fields)
            .ToDictionary(field => field.FieldId, StringComparer.Ordinal);
        var declared = new Dictionary<string, string>(StringComparer.Ordinal) { [fieldId] = DemoLabel };
        if (alsoDeclare is not null) declared[alsoDeclare] = secondLabel ?? DemoLabel;

        var mappings = source.MappingByFieldId.ToDictionary(
            pair => pair.Key,
            pair => declared.TryGetValue(pair.Key, out var label)
                ? pair.Value with
                {
                    Status = "missing",
                    Storage = "generic",
                    TargetModel = "ClaimGenericInput",
                    TargetProperty = DemoName,
                    UiSurface = uiSurface,
                    MigrationRequired = false,
                    CrossFieldGroup = null,
                    GeneratorRule = null,
                    ModelPath = null,
                    // 元は generated で依存宣言を持つ。generic では依存を持てないので落とす。
                    SourceContracts = null,
                    SourceFieldIds = null,
                    // 属性・桁数は項目定義と一致させる（宣言側の値を勝手に決めない）。
                    GenericInput = new CsvGenericInputDeclaration(
                        label,
                        "宣言だけで増やせることの実証用。",
                        fieldsById[pair.Key].DataType,
                        fieldsById[pair.Key].MaxBytes),
                }
                : pair.Value,
            StringComparer.Ordinal);

        // 宣言した項目には既に別の claim（日数定義・ADR 0034 等）があることがあるため、差し替える。
        var others = source.EvidenceClaims
            .Where(claim => !declared.ContainsKey(claim.ClaimId));
        var claims = withPassThroughClaim
            ? [.. others, .. declared.Keys.Select(PassThroughClaim)]
            : others.ToArray();

        return new CsvSpecificationCatalog(
            source.Version,
            source.CommonRecords,
            source.ProviderRecords,
            mappings,
            source.SourcesById,
            claims,
            source.EvidenceGaps);
    }

    private static CsvSpecEvidenceClaim PassThroughClaim(string fieldId) => new(
        fieldId,
        "field",
        "実証用。転記のみで報酬算定には渡さない。",
        [
            new CsvSpecSourceRef(
                "common-r7-10",
                CommonSha256,
                "p.6;section=実証用",
                "authoritative",
                ["pass-through"]),
        ]);
}
