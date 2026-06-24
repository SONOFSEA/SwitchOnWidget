using SwitchOnWidget.Models;

namespace SwitchOnWidget.Services;

public sealed class WeightProjectionService
{
    public WeightProjection GetProjection(int day, UserProfile profile)
    {
        int safeDay = Math.Clamp(day, 1, 42);
        double ratio = (safeDay - 1) / 41d;

        return new WeightProjection(
            Interpolate(profile.StartWeight, profile.ConservativeGoalWeight, ratio),
            Interpolate(profile.StartWeight, profile.StandardGoalWeight, ratio),
            Interpolate(profile.StartWeight, profile.ChallengeGoalWeight, ratio));
    }

    private static double Interpolate(double start, double end, double ratio) =>
        Math.Round(start + ((end - start) * ratio), 1, MidpointRounding.AwayFromZero);
}

public readonly record struct WeightProjection(
    double Conservative,
    double Standard,
    double Challenge);
