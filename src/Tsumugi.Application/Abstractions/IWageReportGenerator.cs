using Tsumugi.Application.Dtos;

namespace Tsumugi.Application.Abstractions;

/// <summary>工賃帳票（PDF）生成の抽象。実装は <c>Tsumugi.Infrastructure.Reporting</c> に置く。</summary>
public interface IWageReportGenerator
{
    /// <summary>利用者ごとの工賃明細 PDF。</summary>
    byte[] GenerateStatement(WageStatementDto statement, RecipientDto recipient, OfficeDto office);

    /// <summary>事業所・月次の工賃支払一覧 PDF。</summary>
    byte[] GeneratePaymentList(
        IReadOnlyList<WageStatementDto> statements,
        IReadOnlyDictionary<Guid, RecipientDto> recipients,
        OfficeDto office,
        int year,
        int month);
}
