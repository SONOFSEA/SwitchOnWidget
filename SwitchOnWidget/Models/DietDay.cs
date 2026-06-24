namespace SwitchOnWidget.Models;

public sealed class DietDay
{
    public required DateOnly Date { get; init; }
    public required int Day { get; init; }
    public required string Breakfast { get; init; }
    public required string Lunch { get; init; }
    public required string Snack { get; init; }
    public required string Dinner { get; init; }
    public required string Extra { get; init; }
    public string SpecialNote { get; init; } = string.Empty;

    public bool IsSpecial => !string.IsNullOrWhiteSpace(SpecialNote);
}
