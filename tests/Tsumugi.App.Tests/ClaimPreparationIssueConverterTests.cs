using System.Globalization;
using FluentAssertions;
using Tsumugi.App.Converters;
using Tsumugi.Application.Claim;

namespace Tsumugi.App.Tests;

public sealed class ClaimPreparationIssueConverterTests
{
    [Fact]
    public void Converts_issue_to_japanese_message_without_recipient_identity()
    {
        var recipientId = Guid.NewGuid();
        var issue = new ClaimPreparationIssue(
            ClaimPreparationIssueCode.MissingRequiredEvidence,
            recipientId,
            "CertificateClaimEvidence.Effective",
            ClaimInputDestination.ClaimInput);

        var message = (string)ClaimPreparationIssueConverter.Instance.Convert(
            issue, typeof(string), null, CultureInfo.InvariantCulture);

        message.Should().Contain("必須の請求根拠が未登録です");
        message.Should().Contain("請求入力画面で確認");
        message.Should().Contain("CertificateClaimEvidence.Effective");
        message.Should().NotContain(recipientId.ToString());
    }

    [Fact]
    public void Office_scoped_issue_reports_office_wide_scope()
    {
        var issue = new ClaimPreparationIssue(
            ClaimPreparationIssueCode.MasterVersionUnavailable,
            null,
            "ClaimMaster.Version",
            ClaimInputDestination.ClaimPreparation);

        var message = (string)ClaimPreparationIssueConverter.Instance.Convert(
            issue, typeof(string), null, CultureInfo.InvariantCulture);

        message.Should().Contain("対象月の請求制度マスターが未整備です");
        message.Should().Contain("事業所全体");
    }

    [Fact]
    public void Non_issue_value_converts_to_empty_string()
    {
        var message = ClaimPreparationIssueConverter.Instance.Convert(
            null, typeof(string), null, CultureInfo.InvariantCulture);

        message.Should().Be(string.Empty);
    }
}
