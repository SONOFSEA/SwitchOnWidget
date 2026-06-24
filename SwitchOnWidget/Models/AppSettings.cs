namespace SwitchOnWidget.Models;

public sealed class AppSettings
{
    public bool RunAtStartup { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool Topmost { get; set; } = true;
    public DateOnly? LastRunDate { get; set; }
}
