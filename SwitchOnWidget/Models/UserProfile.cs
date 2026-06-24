namespace SwitchOnWidget.Models;

public sealed class UserProfile
{
    public string Gender { get; set; } = "남자";
    public int Age { get; set; } = 27;
    public double StartWeight { get; set; } = 79.0;
    public double BodyFatPercent { get; set; } = 18.0;
    public double SkeletalMuscleMass { get; set; } = 37.0;
    public int GoalWeeks { get; set; } = 6;
    public double ChallengeGoalWeight { get; set; } = 68.0;
    public double StandardGoalWeight { get; set; } = 71.5;
    public double ConservativeGoalWeight { get; set; } = 74.0;
    public DateOnly StartDate { get; set; } = new(2026, 6, 22);
}
