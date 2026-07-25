using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Tsumugi.App.Tests;

/// <summary>
/// Phase 1 の View が、対応する ViewModel/DTO のプロパティを
/// 必ず画面入力として晒していることを XAML テキストスキャンで担保する。
/// VM や DTO に値があるのに画面で触れない、という Round 2/3 で頻発した抜けを再発防止するトリップワイヤ。
/// </summary>
public sealed class ViewInputWiringTests
{
    [Fact]
    public void OfficeView_exposes_ServiceCategory_and_RegionGrade_inputs()
    {
        var xml = ReadView("OfficeView.axaml");
        // OfficeViewModel の Category / Region に対する Binding が必須。
        xml.Should().Contain("{Binding Category}",
            because: "サービス種別の入力フィールドが画面に無いと AC1-1 を満たさない");
        xml.Should().Contain("{Binding Region}",
            because: "地域区分の入力フィールドが画面に無いと AC1-1 を満たさず、報酬算定の前提が画面で確定できない");
    }

    [Fact]
    public void OfficeView_binds_F5_to_LoadCommand()
    {
        var xml = ReadView("OfficeView.axaml");

        xml.Should().Contain("Gesture=\"F5\"");
        xml.Should().Contain("{Binding LoadCommand}");
    }

    [Fact]
    public void Phase31_owned_certificate_and_office_fields_are_exposed_without_evidence_fields()
    {
        var certificate = ReadView("CertificateView.axaml");
        certificate.Should().Contain("{Binding MunicipalityNumber}");
        certificate.Should().Contain("{Binding SubsidyMunicipalityNumber}");
        certificate.Should().Contain("{Binding UpperLimitManagementProviderNumber}");
        certificate.Should().Contain("{Binding ProviderCertificateEntryNumber}");
        // Phase 3-3: 請求CSVの開始年月日（provider:J121:02:008）は契約ごとの個別入力（ADR 0032）。
        certificate.Should().Contain("{Binding ProviderFirstServiceDate,");
        certificate.Should().Contain("{Binding SelectedProvider}");
        certificate.Should().NotContain("CertificateClaimEvidence");
        certificate.Should().NotContain("Article31");
        certificate.Should().NotContain("OriginalDocumentReference");

        var office = ReadView("OfficeView.axaml");
        office.Should().Contain("{Binding PostalCode}");
        office.Should().Contain("{Binding Address}");
        office.Should().Contain("{Binding PhoneNumber}");
        office.Should().Contain("{Binding RepresentativeTitleAndName}");
    }

    [Fact]
    public void DailyRecordView_exposes_eleven_claim_fields_episode_context_and_keyboard_commands()
    {
        var xml = ReadView("DailyRecordView.axaml");
        foreach (var binding in new[]
        {
            "EditorServiceStartTime", "EditorServiceEndTime", "EditorSpecialVisitSupportMinutes",
            // 訪問支援特別加算の算定時間数（時間）。サービス提供時間数（分）とは別項目で
            // 日次実績から導出できないため、画面に入力欄が無いと永久に null のまま保存される。
            "EditorSpecialVisitSupportBilledHours",
            "EditorOffsiteSupportApplied", "EditorMedicalCoordinationType", "EditorTrialUseSupportType",
            "EditorRegionalCollaborationApplied", "EditorIntensiveSupportApplied",
            "EditorEmergencyAdmissionApplied", "EditorRecipientConfirmation",
            "SelectedOffice", "EpisodeRevisions",
            "EpisodeCurrentHeadId", "EpisodeEffectiveHeadId",
        })
            xml.Should().Contain($"{{Binding {binding}}}");

        xml.Should().Contain("{Binding EpisodeStartDate,");
        // 単位の取り違えは請求誤りに直結する。両方のラベルが単位を示していることを固定する。
        xml.Should().Contain("サービス提供時間数（分）").And.Contain("算定時間数（時間");

        xml.Should().Contain("Gesture=\"Ctrl+S\"");
        xml.Should().Contain("{Binding SaveSelectedDailyRecordCommand}");
        xml.Should().Contain("Gesture=\"F5\"");
        xml.Should().NotContain("ClaimInputView");
        xml.Should().NotContain("Phase3-2");
        xml.Should().NotContain("Phase3-3");
    }

    [Fact]
    public void ClaimInputView_exposes_only_owned_fields_histories_and_keyboard_commands()
    {
        var xml = ReadView("ClaimInputView.axaml");
        foreach (var binding in new[]
        {
            "SelectedOffice", "SelectedRecipient", "SelectedCertificate", "Year", "Month",
            "SourceFiscalYear", "SelectedAggregate", "ClaimInputCurrentHeadId",
            "ClaimInputEffectiveHeadId", "ClaimInputRevisions", "UpperLimitManagementResult",
            "UpperLimitManagedAmountYen", "MunicipalSubsidyAmountYen",
            "AverageWageCurrentHeadId", "AverageWageEffectiveHeadId",
            "AnnualWagePaidYen", "AnnualExtendedUsers", "AnnualOpeningDays", "Completeness",
            "AverageWageEvidenceDocumentId", "DailyEvidenceReference", "MonthlyEvidenceReference",
            "OfficeProfileChains", "SelectedOfficeProfileChain", "OfficeProfileCurrentHeadId",
            "OfficeProfileEffectiveHeadId", "MasterVersion", "ReformStatus",
            "AverageWageBandOption", "DesignationDate", "SupportStartDate",
            "EarlierRegisteredBandOption", "EarlierRegistrationMonth",
            "LaterRegisteredBandOption", "LaterRegistrationMonth",
            "ReformComparisonEvidenceDocumentId", "FiledTransitionPeriod",
            "FiledTransitionEvidenceDocumentId", "OfficeProfileEvidenceDocumentId",
            "CertificateEvidenceChains", "SelectedCertificateEvidenceChain",
            "CertificateEvidenceCurrentHeadId", "CertificateEvidenceEffectiveHeadId",
            "MonthlyCostCapYen", "UpperLimitManagementApplicability",
            "UpperLimitManagementOfficeNumber", "Article31Status", "Article31AmountYen",
            "Article31EffectivePeriod", "CertificateOriginalDocumentReference",
            "StatementCurrentHeadId", "StatementEffectiveHeadId", "MunicipalityNumber",
            "StatementCertificateNumber", "StatementLines",
        })
            xml.Should().Contain($"{{Binding {binding}");

        xml.Should().Contain("Gesture=\"F5\"").And.Contain("{Binding LoadCommand}");
        xml.Should().Contain("Gesture=\"Ctrl+S\"")
            .And.Contain("{Binding SaveCurrentAggregateCommand}");
        xml.Should().Contain("Gesture=\"Escape\"")
            .And.Contain("{Binding DiscardCurrentEditCommand}");
        xml.Should().Contain("<ScrollViewer");
        xml.Should().Contain("{Binding AddStatementLineCommand}")
            .And.Contain("{Binding RemoveStatementLineCommand}")
            .And.Contain("{Binding SelectedStatementLine}");
        // Phase 3-3: 例外利用日の 4 項目（provider:J121:04:030-033）は本画面で入力できる
        // （field-mapping の crossFieldGroup "exceptional-usage" / uiSurface=ClaimInputView）。
        // Phase 3-2 までは「孤立 4 フィールド」として未提供であり、ここは NotContain だった。
        foreach (var binding in new[]
        {
            "ExceptionalUsageStartYear", "ExceptionalUsageStartMonth",
            "ExceptionalUsageEndYear", "ExceptionalUsageEndMonth",
            "ExceptionalUsageDays", "StandardUsageDayTotal",
            // 訪問支援特別加算の算定回数（回）と施設外支援の累計日数（日）。どちらも日次実績から
            // 導出できない月次の個別入力なので、画面に入力欄が無いと永久に null のまま確定される。
            "SpecialVisitSupportBilledCount", "OffsiteSupportCumulativeDays",
        })
            xml.Should().Contain($"{{Binding {binding}");

        // 施設外支援の累計は「当月分を含むか」を公式資料から確定できない。UI が断定しないよう、
        // 明細書の『累計』欄の値をそのまま入れる旨の補助文を必須にする。
        xml.Should().Contain("明細書の『累計』欄");

        xml.Should().NotContain("IntensiveSupportEpisode")
            .And.NotContain("Phase3-2")
            .And.NotContain("Phase3-3");
    }

    [Fact]
    public void ClaimInputView_uses_safe_two_way_converters_for_value_object_text_inputs()
    {
        var xml = ReadView("ClaimInputView.axaml");
        foreach (var (binding, converter) in new[]
        {
            ("AverageWagePeriodStart", "DateOnlyConverter.Instance"),
            ("AverageWagePeriodEnd", "DateOnlyConverter.Instance"),
            ("AverageWageConfirmedAt", "DateTimeOffsetConverter.Instance"),
            ("ProfileEffectiveFrom", "DateOnlyConverter.Instance"),
            ("ProfileEffectiveTo", "DateOnlyConverter.Instance"),
            ("MasterVersion", "ClaimMasterVersionConverter.Instance"),
            ("AverageWageBandOption", "AverageWageBandOptionConverter.Instance"),
            ("DesignationDate", "DateOnlyConverter.Instance"),
            ("SupportStartDate", "DateOnlyConverter.Instance"),
            ("EarlierRegisteredBandOption", "VersionedAverageWageBandOptionConverter.Instance"),
            ("EarlierRegistrationMonth", "ServiceMonthConverter.Instance"),
            ("LaterRegisteredBandOption", "VersionedAverageWageBandOptionConverter.Instance"),
            ("LaterRegistrationMonth", "ServiceMonthConverter.Instance"),
            ("FiledTransitionPeriod", "DateRangeConverter.Instance"),
            ("OfficeProfileConfirmedAt", "DateTimeOffsetConverter.Instance"),
            ("CertificateValidityStart", "DateOnlyConverter.Instance"),
            ("CertificateValidityEnd", "DateOnlyConverter.Instance"),
            ("Article31EffectivePeriod", "DateRangeConverter.Instance"),
            ("CertificateEvidenceConfirmedAt", "DateTimeOffsetConverter.Instance"),
            ("StatementReceivedAt", "DateTimeOffsetConverter.Instance"),
            ("StatementConfirmedAt", "DateTimeOffsetConverter.Instance"),
        })
        {
            xml.Should().Contain(
                $"{{Binding {binding}, Mode=TwoWay, Converter={{x:Static conv:{converter}}}}}");
        }
    }

    [Fact]
    public void ContractView_exposes_PeriodEnd_input()
    {
        var xml = ReadView("ContractView.axaml");
        // PeriodEnd は VM/UseCase で保存に使うが、画面に入力欄が無いと永久に null のまま保存される。
        xml.Should().Contain("{Binding PeriodEnd",
            because: "契約終了日の入力フィールドが画面に無いと AC1-1 を満たさない");
    }

    [Fact]
    public void ContractView_wires_LoadCommand_for_read_path()
    {
        var xml = ReadView("ContractView.axaml");
        // 利用者選択後に契約一覧を読む導線が必要。F5 か更新ボタン経由で LoadCommand を発火させる。
        xml.Should().Contain("{Binding LoadCommand}",
            because: "契約一覧 Read が画面から到達不能だと AC1-1 CRUD を満たさない");
    }

    private static string ReadView(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.EnumerateFiles("Tsumugi.sln").Any())
            {
                return File.ReadAllText(
                    Path.Combine(dir.FullName, "src", "Tsumugi.App", "Views", fileName));
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Tsumugi.sln が祖先方向に見つからない");
    }
}
