using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SwitchOnWidget.Models;
using SwitchOnWidget.Services;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;

namespace SwitchOnWidget.Views;

public partial class CalendarWindow : Window
{
    private readonly DietPlanService _dietPlan;
    private readonly WeightProjectionService _projection;
    private readonly StorageService _storage;
    private readonly UserProfile _profile;
    private readonly List<DailyRecord> _records;
    private readonly IReadOnlyList<DietDay> _days;
    private DietDay? _selectedDay;

    public CalendarWindow(DietPlanService dietPlan, WeightProjectionService projection,
        StorageService storage, UserProfile profile, List<DailyRecord> records)
    {
        InitializeComponent();
        _dietPlan = dietPlan;
        _projection = projection;
        _storage = storage;
        _profile = profile;
        _records = records;
        _days = dietPlan.GetAllDays();

        RefreshCalendar();
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        SelectDay(dietPlan.IsInProgram(today) ? today : DietPlanService.StartDate);
    }

    private void RefreshCalendar()
    {
        CalendarItemsControl.ItemsSource = _days.Select(day =>
        {
            DailyRecord? record = _records.FirstOrDefault(item => item.Date == day.Date);
            WeightProjection expected = _projection.GetProjection(day.Day, _profile);
            int percent = record?.CompletionPercent ?? 0;
            MediaBrush brush = percent switch
            {
                100 => new SolidColorBrush(MediaColor.FromRgb(35, 82, 68)),
                >= 50 => new SolidColorBrush(MediaColor.FromRgb(55, 67, 73)),
                > 0 => new SolidColorBrush(MediaColor.FromRgb(66, 57, 57)),
                _ => new SolidColorBrush(MediaColor.FromRgb(31, 38, 49))
            };

            return new CalendarDayItem
            {
                Date = day.Date,
                DayLabel = $"DAY {day.Day}",
                DateLabel = day.Date.ToString("M/d", CultureInfo.InvariantCulture),
                Weekday = day.Date.ToString("ddd", CultureInfo.GetCultureInfo("ko-KR")),
                ExpectedLabel = $"표준 {expected.Standard:0.0}kg",
                ActualLabel = record?.Weight is double weight ? $"실제 {weight:0.0}kg" : "실제 —",
                CompletionLabel = $"완료 {percent}%",
                SpecialLabel = day.SpecialNote,
                StatusBrush = brush
            };
        }).ToList();
    }

    private void DayCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { CommandParameter: DateOnly date })
            SelectDay(date);
    }

    private void SelectDay(DateOnly date)
    {
        _selectedDay = _dietPlan.GetDay(date);
        DailyRecord record = FindOrCreateRecord(_selectedDay);
        WeightProjection expected = _projection.GetProjection(_selectedDay.Day, _profile);

        SelectedDateText.Text = $"{date:yyyy년 M월 d일} {date.ToString("dddd", CultureInfo.GetCultureInfo("ko-KR"))} · DAY {_selectedDay.Day}";
        SelectedExpectedText.Text = $"표준 예상 {expected.Standard:0.0}kg";
        SelectedSpecialText.Text = _selectedDay.SpecialNote;
        SelectedDietText.Text =
            $"아침  {_selectedDay.Breakfast}\n\n점심  {_selectedDay.Lunch}\n\n간식  {_selectedDay.Snack}\n\n저녁  {_selectedDay.Dinner}\n\n{_selectedDay.Extra}";

        SelectedShakeCheckBox.IsChecked = record.ShakeDone;
        SelectedLunchCheckBox.IsChecked = record.LunchDone;
        SelectedDinnerCheckBox.IsChecked = record.DinnerDone;
        SelectedWaterCheckBox.IsChecked = record.WaterDone;
        SelectedExerciseCheckBox.IsChecked = record.ExerciseDone;
        SelectedWeightRecordedCheckBox.IsChecked = record.WeightRecorded;
        SelectedWeightTextBox.Text = record.Weight?.ToString("0.0", CultureInfo.InvariantCulture) ?? string.Empty;
        SelectedMemoTextBox.Text = record.Memo;
        EditorStatusText.Text = string.Empty;
    }

    private DailyRecord FindOrCreateRecord(DietDay day)
    {
        DailyRecord? record = _records.FirstOrDefault(item => item.Date == day.Date);
        if (record is not null)
            return record;

        record = new DailyRecord { Date = day.Date, Day = day.Day };
        _records.Add(record);
        return record;
    }

    private async void SaveSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedDay is null)
            return;

        DailyRecord record = FindOrCreateRecord(_selectedDay);
        string rawWeight = SelectedWeightTextBox.Text.Trim();

        if (!string.IsNullOrEmpty(rawWeight))
        {
            if ((!double.TryParse(rawWeight, NumberStyles.Float, CultureInfo.InvariantCulture, out double weight) &&
                 !double.TryParse(rawWeight, NumberStyles.Float, CultureInfo.CurrentCulture, out weight)) ||
                weight is < 30 or > 200)
            {
                EditorStatusText.Text = "체중은 30~200kg 숫자로 입력해 주세요.";
                SelectedWeightTextBox.Focus();
                return;
            }

            record.Weight = Math.Round(weight, 1, MidpointRounding.AwayFromZero);
            record.WeightRecorded = true;
            SelectedWeightRecordedCheckBox.IsChecked = true;
        }
        else
        {
            record.Weight = null;
        }

        record.ShakeDone = SelectedShakeCheckBox.IsChecked == true;
        record.LunchDone = SelectedLunchCheckBox.IsChecked == true;
        record.DinnerDone = SelectedDinnerCheckBox.IsChecked == true;
        record.WaterDone = SelectedWaterCheckBox.IsChecked == true;
        record.ExerciseDone = SelectedExerciseCheckBox.IsChecked == true;
        record.WeightRecorded = SelectedWeightRecordedCheckBox.IsChecked == true;
        record.Memo = SelectedMemoTextBox.Text.Trim();

        try
        {
            await _storage.SaveRecordsAsync(_records);
            EditorStatusText.Text = $"저장했습니다. {DateTime.Now:HH:mm:ss}";
            RefreshCalendar();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"기록을 저장하지 못했습니다.\n\n{ex.Message}",
                "저장 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private sealed class CalendarDayItem
    {
        public DateOnly Date { get; init; }
        public required string DayLabel { get; init; }
        public required string DateLabel { get; init; }
        public required string Weekday { get; init; }
        public required string ExpectedLabel { get; init; }
        public required string ActualLabel { get; init; }
        public required string CompletionLabel { get; init; }
        public required string SpecialLabel { get; init; }
        public required MediaBrush StatusBrush { get; init; }
    }
}
