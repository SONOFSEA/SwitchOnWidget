using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using SwitchOnWidget.Models;
using SwitchOnWidget.Services;
using MediaColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace SwitchOnWidget.Views;

public partial class WeightLogWindow : Window
{
    private readonly DietPlanService _dietPlan;
    private readonly WeightProjectionService _projection;
    private readonly StorageService _storage;
    private readonly CsvExportService _csvExport;
    private readonly UserProfile _profile;
    private readonly List<DailyRecord> _records;
    private readonly IReadOnlyList<DietDay> _days;

    public ObservableCollection<WeightRow> Rows { get; } = [];

    public WeightLogWindow(DietPlanService dietPlan, WeightProjectionService projection,
        StorageService storage, CsvExportService csvExport, UserProfile profile,
        List<DailyRecord> records)
    {
        InitializeComponent();
        DataContext = this;
        _dietPlan = dietPlan;
        _projection = projection;
        _storage = storage;
        _csvExport = csvExport;
        _profile = profile;
        _records = records;
        _days = dietPlan.GetAllDays();

        BuildRows();
        UpdateSummary();
        Loaded += (_, _) => DrawGraph();
    }

    private void BuildRows()
    {
        Rows.Clear();
        foreach (DietDay day in _days)
        {
            WeightProjection expected = _projection.GetProjection(day.Day, _profile);
            DailyRecord? record = _records.FirstOrDefault(item => item.Date == day.Date);
            Rows.Add(new WeightRow
            {
                Date = day.Date,
                DateLabel = day.Date.ToString("yyyy-MM-dd (ddd)", CultureInfo.GetCultureInfo("ko-KR")),
                Day = day.Day,
                Conservative = expected.Conservative,
                Standard = expected.Standard,
                Challenge = expected.Challenge,
                ActualWeight = record?.Weight,
                Memo = record?.Memo ?? string.Empty
            });
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!CommitGridEdits() || !ValidateWeights())
            return;

        ApplyRowsToRecords();
        try
        {
            await _storage.SaveRecordsAsync(_records);
            UpdateSummary();
            DrawGraph();
            StatusText.Text = $"저장했습니다. {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"체중 기록을 저장하지 못했습니다.\n\n{ex.Message}",
                "저장 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool CommitGridEdits()
    {
        WeightDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        return WeightDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    private bool ValidateWeights()
    {
        WeightRow? invalid = Rows.FirstOrDefault(row => row.ActualWeight is < 30 or > 200);
        if (invalid is null)
            return true;

        StatusText.Text = $"{invalid.DateLabel}: 체중은 30~200kg만 입력할 수 있습니다.";
        WeightDataGrid.SelectedItem = invalid;
        WeightDataGrid.ScrollIntoView(invalid);
        return false;
    }

    private void ApplyRowsToRecords()
    {
        foreach (WeightRow row in Rows)
        {
            DailyRecord? record = _records.FirstOrDefault(item => item.Date == row.Date);
            if (record is null && row.ActualWeight is null && string.IsNullOrWhiteSpace(row.Memo))
                continue;

            if (record is null)
            {
                record = new DailyRecord { Date = row.Date, Day = row.Day };
                _records.Add(record);
            }

            record.Weight = row.ActualWeight is double weight
                ? Math.Round(weight, 1, MidpointRounding.AwayFromZero)
                : null;
            record.WeightRecorded = record.Weight.HasValue;
            record.Memo = row.Memo?.Trim() ?? string.Empty;
        }
    }

    private void UpdateSummary()
    {
        List<double> actuals = Rows.Where(row => row.ActualWeight.HasValue)
            .OrderBy(row => row.Date)
            .Select(row => row.ActualWeight!.Value)
            .ToList();

        if (actuals.Count == 0)
        {
            ChangeText.Text = "기록 없음";
            AverageText.Text = "—";
            RecordedDaysText.Text = "0 / 42일";
            return;
        }

        double latest = actuals[^1];
        double change = latest - _profile.StartWeight;
        ChangeText.Text = $"{change:+0.0;-0.0;0.0} kg";
        AverageText.Text = $"{actuals.TakeLast(7).Average():0.0} kg";
        RecordedDaysText.Text = $"{actuals.Count} / 42일";
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        CommitGridEdits();
        if (!ValidateWeights())
            return;

        ApplyRowsToRecords();
        await _storage.SaveRecordsAsync(_records);

        Microsoft.Win32.SaveFileDialog dialog = new()
        {
            Title = "체중 기록 CSV 내보내기",
            Filter = "CSV 파일 (*.csv)|*.csv",
            FileName = $"switch-on-weight-{DateTime.Now:yyyyMMdd}.csv",
            AddExtension = true,
            DefaultExt = ".csv"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            Dictionary<DateOnly, DailyRecord> recordMap = _records
                .GroupBy(record => record.Date)
                .ToDictionary(group => group.Key, group => group.Last());
            await _csvExport.ExportAsync(dialog.FileName, _days, recordMap, _profile, _projection);
            StatusText.Text = $"CSV를 저장했습니다: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"CSV를 내보내지 못했습니다.\n\n{ex.Message}",
                "CSV 내보내기", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void WeightCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawGraph();

    private void DrawGraph()
    {
        double width = WeightCanvas.ActualWidth;
        double height = WeightCanvas.ActualHeight;
        if (width < 80 || height < 60 || Rows.Count == 0)
            return;

        WeightCanvas.Children.Clear();
        const double left = 34;
        const double right = 12;
        const double top = 10;
        const double bottom = 24;

        List<double> values = Rows.SelectMany(row => new[] { row.Conservative, row.Standard, row.Challenge })
            .Concat(Rows.Where(row => row.ActualWeight.HasValue).Select(row => row.ActualWeight!.Value))
            .ToList();
        double minWeight = Math.Floor(values.Min() - 1);
        double maxWeight = Math.Ceiling(values.Max() + 1);
        double plotWidth = width - left - right;
        double plotHeight = height - top - bottom;

        double X(int index) => left + index / 41d * plotWidth;
        double Y(double weight) => top + (maxWeight - weight) / (maxWeight - minWeight) * plotHeight;

        for (int i = 0; i <= 4; i++)
        {
            double weight = minWeight + (maxWeight - minWeight) * i / 4d;
            double y = Y(weight);
            WeightCanvas.Children.Add(new Line
            {
                X1 = left, X2 = width - right, Y1 = y, Y2 = y,
                Stroke = new SolidColorBrush(MediaColor.FromRgb(47, 57, 71)), StrokeThickness = 1
            });
            TextBlock label = new()
            {
                Text = weight.ToString("0", CultureInfo.InvariantCulture),
                Foreground = new SolidColorBrush(MediaColor.FromRgb(145, 156, 174)),
                FontSize = 10
            };
            Canvas.SetLeft(label, 3);
            Canvas.SetTop(label, y - 7);
            WeightCanvas.Children.Add(label);
        }

        AddPolyline(Rows.Select(row => row.Conservative), MediaColor.FromRgb(122, 168, 232), X, Y);
        AddPolyline(Rows.Select(row => row.Standard), MediaColor.FromRgb(98, 214, 167), X, Y);
        AddPolyline(Rows.Select(row => row.Challenge), MediaColor.FromRgb(233, 183, 90), X, Y);

        List<(int Index, double Weight)> actualPoints = Rows.Select((row, index) => (index, row.ActualWeight))
            .Where(item => item.ActualWeight.HasValue)
            .Select(item => (item.index, item.ActualWeight!.Value))
            .ToList();
        if (actualPoints.Count > 0)
        {
            Polyline actual = new()
            {
                Stroke = new SolidColorBrush(MediaColor.FromRgb(240, 124, 145)),
                StrokeThickness = 2.8
            };
            foreach ((int index, double weight) in actualPoints)
                actual.Points.Add(new WpfPoint(X(index), Y(weight)));
            WeightCanvas.Children.Add(actual);

            foreach ((int index, double weight) in actualPoints)
            {
                Ellipse dot = new() { Width = 6, Height = 6, Fill = actual.Stroke };
                Canvas.SetLeft(dot, X(index) - 3);
                Canvas.SetTop(dot, Y(weight) - 3);
                WeightCanvas.Children.Add(dot);
            }
        }

        AddXAxisLabel("6/22", left, height - bottom + 5);
        AddXAxisLabel("7/13", X(21) - 12, height - bottom + 5);
        AddXAxisLabel("8/2", width - right - 22, height - bottom + 5);
    }

    private void AddPolyline(IEnumerable<double> values, MediaColor color,
        Func<int, double> x, Func<double, double> y)
    {
        Polyline line = new()
        {
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 1.7,
            Opacity = 0.95
        };
        int index = 0;
        foreach (double value in values)
            line.Points.Add(new WpfPoint(x(index++), y(value)));
        WeightCanvas.Children.Add(line);
    }

    private void AddXAxisLabel(string text, double x, double y)
    {
        TextBlock label = new()
        {
            Text = text,
            Foreground = new SolidColorBrush(MediaColor.FromRgb(145, 156, 174)),
            FontSize = 10
        };
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, y);
        WeightCanvas.Children.Add(label);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    public sealed class WeightRow
    {
        public DateOnly Date { get; init; }
        public required string DateLabel { get; init; }
        public int Day { get; init; }
        public double Conservative { get; init; }
        public double Standard { get; init; }
        public double Challenge { get; init; }
        public double? ActualWeight { get; set; }
        public string Memo { get; set; } = string.Empty;
    }
}
