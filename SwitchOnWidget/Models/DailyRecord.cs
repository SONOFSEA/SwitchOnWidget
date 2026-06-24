namespace SwitchOnWidget.Models;

public sealed class DailyRecord
{
    public DateOnly Date { get; set; }
    public int Day { get; set; }
    public double? Weight { get; set; }
    public bool ShakeDone { get; set; }
    public bool LunchDone { get; set; }
    public bool DinnerDone { get; set; }
    public bool WaterDone { get; set; }
    public bool ExerciseDone { get; set; }
    public bool WeightRecorded { get; set; }
    public string Memo { get; set; } = string.Empty;

    public int CompletedCount => new[]
    {
        ShakeDone, LunchDone, DinnerDone, WaterDone, ExerciseDone, WeightRecorded
    }.Count(done => done);

    public int CompletionPercent => (int)Math.Round(CompletedCount / 6d * 100d);
}
