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

    private const string DemoName = "DemoPassThroughDays";

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

    [Fact]
    public void The_declaration_becomes_an_input_field_without_any_code_change()
    {
        var declarations = CsvGenericFieldCatalog.DeclarationsOf(CatalogWithGenericField());

        var declaration = declarations.Should().ContainSingle().Subject;
        declaration.Name.Should().Be(DemoName);
        declaration.FieldId.Should().Be(DemoFieldId);
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
    /// 埋め込み catalog の 1 項目の mapping を <c>storage: "generic"</c> へ差し替えた catalog。
    /// 項目定義（桁数・属性・requiredWhen）は変えない。
    /// </summary>
    private static CsvSpecificationCatalog CatalogWithGenericField(bool withPassThroughClaim = true)
    {
        var source = CsvSpecificationLoader.LoadEmbedded();
        var mappings = source.MappingByFieldId.ToDictionary(
            pair => pair.Key,
            pair => pair.Key == DemoFieldId
                ? pair.Value with
                {
                    Status = "missing",
                    Storage = "generic",
                    TargetModel = "ClaimGenericInput",
                    TargetProperty = DemoName,
                    UiSurface = "ClaimInputView",
                    MigrationRequired = false,
                    CrossFieldGroup = null,
                    GeneratorRule = null,
                    ModelPath = null,
                    // 元は generated で依存宣言を持つ。generic では依存を持てないので落とす。
                    SourceContracts = null,
                    SourceFieldIds = null,
                    GenericInput = new CsvGenericInputDeclaration(
                        "実証用の転記項目", "宣言だけで増やせることの実証用。", "numeric", 2),
                }
                : pair.Value,
            StringComparer.Ordinal);

        // この項目には既に別の claim（日数定義・ADR 0034）があるため、差し替える。
        var others = source.EvidenceClaims
            .Where(claim => !string.Equals(claim.ClaimId, DemoFieldId, StringComparison.Ordinal));
        var claims = withPassThroughClaim
            ? others.Append(new CsvSpecEvidenceClaim(
                DemoFieldId,
                "field",
                "実証用。転記のみで報酬算定には渡さない。",
                [
                    new CsvSpecSourceRef(
                        "common-r7-10",
                        CommonSha256,
                        "p.6;section=実証用",
                        "authoritative",
                        ["pass-through"]),
                ])).ToArray()
            : [.. others];

        return new CsvSpecificationCatalog(
            source.Version,
            source.CommonRecords,
            source.ProviderRecords,
            mappings,
            source.SourcesById,
            claims,
            source.EvidenceGaps);
    }
}
