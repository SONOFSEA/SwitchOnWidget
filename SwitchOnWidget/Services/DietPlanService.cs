using SwitchOnWidget.Models;

namespace SwitchOnWidget.Services;

public sealed class DietPlanService
{
    public static readonly DateOnly StartDate = new(2026, 6, 22);
    public static readonly DateOnly EndDate = StartDate.AddDays(41);

    private static readonly DateOnly DinnerAppointment = new(2026, 6, 26);
    private static readonly HashSet<DateOnly> DrinkingAppointments =
    [
        new DateOnly(2026, 6, 27),
        new DateOnly(2026, 7, 10)
    ];

    private static readonly HashSet<DateOnly> RecoveryDays =
    [
        new DateOnly(2026, 6, 28),
        new DateOnly(2026, 7, 11)
    ];

    public IReadOnlyList<DietDay> GetAllDays() =>
        Enumerable.Range(0, 42).Select(offset => CreateDay(StartDate.AddDays(offset))).ToList();

    public bool IsInProgram(DateOnly date) => date >= StartDate && date <= EndDate;

    public int GetDayNumber(DateOnly date) => date.DayNumber - StartDate.DayNumber + 1;

    public DietDay GetDay(DateOnly date)
    {
        if (!IsInProgram(date))
            throw new ArgumentOutOfRangeException(nameof(date), "6주 프로그램 기간 밖의 날짜입니다.");

        return CreateDay(date);
    }

    private DietDay CreateDay(DateOnly date)
    {
        int day = GetDayNumber(date);

        if (date == DinnerAppointment)
        {
            return NewDay(date, day,
                "단백질 쉐이크 1회",
                "닭가슴살 150g + 샐러드 200g",
                "필요 시 오이 또는 무가당 차",
                "외식 허용 · 밥 반 공기 이하 · 단백질 메뉴 우선",
                "튀김·면·디저트는 제한하세요.",
                "저녁 밥 약속");
        }

        if (DrinkingAppointments.Contains(date))
        {
            return NewDay(date, day,
                "단백질 쉐이크 1회",
                "닭가슴살 150g + 샐러드 200g",
                "술 전 계란 2개 또는 두부 150g",
                "회·구이·두부·계란·고기류 우선",
                "과음과 튀김·라면·볶음밥을 제한하세요. 다음 날 회복식이 표시됩니다.",
                "저녁 술약속");
        }

        if (RecoveryDays.Contains(date))
        {
            return NewDay(date, day,
                "물 + 단백질 쉐이크",
                "닭가슴살 150g + 샐러드 200g + 현미밥 80g",
                "물·아메리카노·무가당 차",
                "두부 200g + 샐러드 200g",
                "라면·해장국 과식·단 음료를 제한하세요.",
                "술 다음날 회복식");
        }

        if (day <= 3)
        {
            return NewDay(date, day,
                "단백질 쉐이크 1회",
                "단백질 쉐이크 1회",
                "오후 단백질 쉐이크 1회",
                "단백질 쉐이크 1회",
                "물을 충분히 마시고 강도 높은 운동 대신 가볍게 걸으세요.",
                "초기 4일 쉐이크 중심");
        }

        return NewDay(date, day,
            "단백질 쉐이크 1회",
            "닭가슴살 150g + 현미/잡곡밥 100g + 샐러드 200g + 김치 50g",
            "삶은 계란 2개 또는 두부 150g",
            "닭가슴살 150g 또는 두부 200g + 샐러드 200g + 방울토마토 100g",
            "운동 후 필요 시 단백질 쉐이크 1회",
            string.Empty);
    }

    private static DietDay NewDay(DateOnly date, int day, string breakfast, string lunch,
        string snack, string dinner, string extra, string special) => new()
    {
        Date = date,
        Day = day,
        Breakfast = breakfast,
        Lunch = lunch,
        Snack = snack,
        Dinner = dinner,
        Extra = extra,
        SpecialNote = special
    };
}
