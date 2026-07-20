using Tsumugi.Application.Dtos.Claim.Reports;

namespace Tsumugi.Application.Abstractions;

/// <summary>請求関連 PDF 帳票の生成抽象。決定論（同 payload + 同 TimeProvider → 同バイト）。</summary>
public interface IClaimReportGenerator
{
    /// <summary>サービス提供実績記録票（A4、利用者×月次）。</summary>
    byte[] GenerateServiceProvisionRecord(ServiceProvisionRecordDto dto);

    /// <summary>介護給付費・訓練等給付費等 請求書（事業所×月次の集計）。</summary>
    byte[] GenerateClaimInvoice(ClaimInvoiceDto dto);

    /// <summary>介護給付費・訓練等給付費等 請求明細書（事業所×月次の受給者別明細）。</summary>
    byte[] GenerateClaimStatement(ClaimStatementDto dto);
}
