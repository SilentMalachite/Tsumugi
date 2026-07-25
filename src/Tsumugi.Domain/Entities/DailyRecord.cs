using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Entities;

/// <summary>
/// 日次記録（取引記録・厳密追記）。決して更新・削除しない。訂正・取消は新レコードで表現する。
/// 更新トークンは持たず、基底の <see cref="Entity.ConcurrencyToken"/> は無視する。
/// </summary>
public sealed record DailyRecord : Entity
{
    public required Guid RecipientId { get; init; }
    public required DateOnly ServiceDate { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? OriginId { get; init; }
    public Attendance Attendance { get; init; }
    public TransportKind Transport { get; init; }
    public bool MealProvided { get; init; }
    public string? Note { get; init; }
    public TimeOnly? ServiceStartTime { get; init; }
    public TimeOnly? ServiceEndTime { get; init; }

    /// <summary>
    /// 訪問支援特別加算で<b>実際にサービス提供した時間・単位は「分」</b>。
    /// 公式項目 <c>provider:J611:02:027</c>「訪問支援特別加算（サービス提供時間数）」に対応する実績値であり、
    /// <see cref="SpecialVisitSupportBilledHours"/>（算定時間数・単位は「時間」）とは別項目。
    /// </summary>
    public int? SpecialVisitSupportMinutes { get; init; }

    /// <summary>
    /// 訪問支援特別加算の<b>算定時間数・単位は「時間」（整数）</b>。
    /// 公式項目 <c>provider:J611:02:028</c>「訪問支援特別加算（算定時間数）」＝
    /// 「算定する時間数（時間）を設定（整数）」。
    /// 実際にサービス提供した時間を分で保持する <see cref="SpecialVisitSupportMinutes"/>
    /// （<c>provider:J611:02:027</c>「サービス提供時間数」）とは<b>別項目</b>で、そこからは導出できない。
    /// 根拠は留意事項通知 2(6)⑨「所要時間については、実際に要した時間により算定されるのではなく、
    /// 計画に基づいて行われるべき指定サービス等に要する時間に基づき算定される」。
    /// </summary>
    public int? SpecialVisitSupportBilledHours { get; init; }

    public bool? OffsiteSupportApplied { get; init; }
    public MedicalCoordinationType MedicalCoordinationType { get; init; }
    public TrialUseSupportType TrialUseSupportType { get; init; }
    public bool? RegionalCollaborationApplied { get; init; }
    public bool? IntensiveSupportApplied { get; init; }
    public bool? EmergencyAdmissionApplied { get; init; }
    public RecipientConfirmationStatus RecipientConfirmation { get; init; }

    public static DailyRecord NewRecord(
        Guid id, Guid recipientId, DateOnly serviceDate,
        Attendance attendance, TransportKind transport, bool mealProvided,
        string? note, string createdBy, DateTimeOffset createdAt) =>
        NewRecord(
            id, recipientId, serviceDate, attendance, transport, mealProvided,
            note, createdBy, createdAt,
            serviceStartTime: null,
            serviceEndTime: null,
            specialVisitSupportMinutes: null,
            offsiteSupportApplied: null,
            medicalCoordinationType: MedicalCoordinationType.Unspecified,
            trialUseSupportType: TrialUseSupportType.Unspecified,
            regionalCollaborationApplied: null,
            intensiveSupportApplied: null,
            emergencyAdmissionApplied: null,
            recipientConfirmation: RecipientConfirmationStatus.Unspecified,
            specialVisitSupportBilledHours: null);

    public static DailyRecord NewRecord(
        Guid id, Guid recipientId, DateOnly serviceDate,
        Attendance attendance, TransportKind transport, bool mealProvided,
        string? note, string createdBy, DateTimeOffset createdAt,
        TimeOnly? serviceStartTime = null,
        TimeOnly? serviceEndTime = null,
        int? specialVisitSupportMinutes = null,
        bool? offsiteSupportApplied = null,
        MedicalCoordinationType medicalCoordinationType = MedicalCoordinationType.Unspecified,
        TrialUseSupportType trialUseSupportType = TrialUseSupportType.Unspecified,
        bool? regionalCollaborationApplied = null,
        bool? intensiveSupportApplied = null,
        bool? emergencyAdmissionApplied = null,
        RecipientConfirmationStatus recipientConfirmation = RecipientConfirmationStatus.Unspecified,
        int? specialVisitSupportBilledHours = null)
    {
        ValidateClaimInputs(
            serviceStartTime, serviceEndTime, specialVisitSupportMinutes,
            medicalCoordinationType, trialUseSupportType, recipientConfirmation,
            specialVisitSupportBilledHours);

        return new()
        {
            Id = id,
            RecipientId = recipientId,
            ServiceDate = serviceDate,
            Kind = RecordKind.New,
            OriginId = null,
            Attendance = attendance,
            Transport = transport,
            MealProvided = mealProvided,
            Note = note,
            ServiceStartTime = serviceStartTime,
            ServiceEndTime = serviceEndTime,
            SpecialVisitSupportMinutes = specialVisitSupportMinutes,
            SpecialVisitSupportBilledHours = specialVisitSupportBilledHours,
            OffsiteSupportApplied = offsiteSupportApplied,
            MedicalCoordinationType = medicalCoordinationType,
            TrialUseSupportType = trialUseSupportType,
            RegionalCollaborationApplied = regionalCollaborationApplied,
            IntensiveSupportApplied = intensiveSupportApplied,
            EmergencyAdmissionApplied = emergencyAdmissionApplied,
            RecipientConfirmation = recipientConfirmation,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,  // 取引記録は更新しないため未使用
        };
    }

    public static DailyRecord Correction(
        Guid id, Guid recipientId, DateOnly serviceDate, Guid originId,
        Attendance attendance, TransportKind transport, bool mealProvided,
        string? note, string createdBy, DateTimeOffset createdAt) =>
        Correction(
            id, recipientId, serviceDate, originId,
            attendance, transport, mealProvided,
            note, createdBy, createdAt,
            serviceStartTime: null,
            serviceEndTime: null,
            specialVisitSupportMinutes: null,
            offsiteSupportApplied: null,
            medicalCoordinationType: MedicalCoordinationType.Unspecified,
            trialUseSupportType: TrialUseSupportType.Unspecified,
            regionalCollaborationApplied: null,
            intensiveSupportApplied: null,
            emergencyAdmissionApplied: null,
            recipientConfirmation: RecipientConfirmationStatus.Unspecified,
            specialVisitSupportBilledHours: null);

    public static DailyRecord Correction(
        Guid id, Guid recipientId, DateOnly serviceDate, Guid originId,
        Attendance attendance, TransportKind transport, bool mealProvided,
        string? note, string createdBy, DateTimeOffset createdAt,
        TimeOnly? serviceStartTime = null,
        TimeOnly? serviceEndTime = null,
        int? specialVisitSupportMinutes = null,
        bool? offsiteSupportApplied = null,
        MedicalCoordinationType medicalCoordinationType = MedicalCoordinationType.Unspecified,
        TrialUseSupportType trialUseSupportType = TrialUseSupportType.Unspecified,
        bool? regionalCollaborationApplied = null,
        bool? intensiveSupportApplied = null,
        bool? emergencyAdmissionApplied = null,
        RecipientConfirmationStatus recipientConfirmation = RecipientConfirmationStatus.Unspecified,
        int? specialVisitSupportBilledHours = null)
    {
        ValidateClaimInputs(
            serviceStartTime, serviceEndTime, specialVisitSupportMinutes,
            medicalCoordinationType, trialUseSupportType, recipientConfirmation,
            specialVisitSupportBilledHours);

        return new()
        {
            Id = id,
            RecipientId = recipientId,
            ServiceDate = serviceDate,
            Kind = RecordKind.Correct,
            OriginId = originId,
            Attendance = attendance,
            Transport = transport,
            MealProvided = mealProvided,
            Note = note,
            ServiceStartTime = serviceStartTime,
            ServiceEndTime = serviceEndTime,
            SpecialVisitSupportMinutes = specialVisitSupportMinutes,
            SpecialVisitSupportBilledHours = specialVisitSupportBilledHours,
            OffsiteSupportApplied = offsiteSupportApplied,
            MedicalCoordinationType = medicalCoordinationType,
            TrialUseSupportType = trialUseSupportType,
            RegionalCollaborationApplied = regionalCollaborationApplied,
            IntensiveSupportApplied = intensiveSupportApplied,
            EmergencyAdmissionApplied = emergencyAdmissionApplied,
            RecipientConfirmation = recipientConfirmation,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };
    }

    public static DailyRecord Cancellation(
        Guid id, Guid recipientId, DateOnly serviceDate, Guid originId,
        string createdBy, DateTimeOffset createdAt) => new()
        {
            Id = id,
            RecipientId = recipientId,
            ServiceDate = serviceDate,
            Kind = RecordKind.Cancel,
            OriginId = originId,
            Attendance = Attendance.Discontinued,
            Transport = TransportKind.None,
            MealProvided = false,
            Note = null,
            ServiceStartTime = null,
            ServiceEndTime = null,
            SpecialVisitSupportMinutes = null,
            // 取消レコードは請求入力値を持たない（訪問支援特別加算の算定時間数も含めて null に落とす）。
            SpecialVisitSupportBilledHours = null,
            OffsiteSupportApplied = null,
            MedicalCoordinationType = MedicalCoordinationType.Unspecified,
            TrialUseSupportType = TrialUseSupportType.Unspecified,
            RegionalCollaborationApplied = null,
            IntensiveSupportApplied = null,
            EmergencyAdmissionApplied = null,
            RecipientConfirmation = RecipientConfirmationStatus.Unspecified,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };

    private static void ValidateClaimInputs(
        TimeOnly? serviceStartTime,
        TimeOnly? serviceEndTime,
        int? specialVisitSupportMinutes,
        MedicalCoordinationType medicalCoordinationType,
        TrialUseSupportType trialUseSupportType,
        RecipientConfirmationStatus recipientConfirmation,
        int? specialVisitSupportBilledHours)
    {
        if (serviceStartTime is not null &&
            serviceEndTime is not null &&
            serviceStartTime > serviceEndTime)
        {
            throw new ArgumentException("サービス開始時刻は終了時刻以前である必要があります。");
        }

        if (specialVisitSupportMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(specialVisitSupportMinutes),
                "訪問支援特別加算の時間数は0以上である必要があります。");
        }

        // 上限（1月あたりの算定回数・時間数の制度上の限度）は公式の実値をコードに持ち込まないため検証しない（CLAUDE.md §ハード制約3）。
        if (specialVisitSupportBilledHours < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(specialVisitSupportBilledHours),
                "訪問支援特別加算の算定時間数は0以上である必要があります。");
        }

        EnsureDefined(medicalCoordinationType, nameof(medicalCoordinationType));
        EnsureDefined(trialUseSupportType, nameof(trialUseSupportType));
        EnsureDefined(recipientConfirmation, nameof(recipientConfirmation));
    }

    private static void EnsureDefined<TEnum>(TEnum value, string paramName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(paramName, "未定義の区分は指定できません。");
    }
}
