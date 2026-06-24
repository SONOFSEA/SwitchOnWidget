using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using SwitchOnWidget.Models;
using SwitchOnWidget.Services;
using SwitchOnWidget.Views;
using Forms = System.Windows.Forms;

namespace SwitchOnWidget;

public partial class MainWindow : Window
{
    private readonly StorageService _storage;
    private readonly DietPlanService _dietPlan;
    private readonly WeightProjectionService _projection;
    private readonly StartupService _startup;
    private readonly CsvExportService _csvExport;
    private readonly DispatcherTimer _positionSaveTimer;
    private readonly Forms.NotifyIcon _trayIcon;

    private UserProfile _profile = new();
    private AppSettings _settings = new();
    private List<DailyRecord> _records = [];
    private DailyRecord? _currentRecord;
    private DietDay? _currentDay;
    private bool _isLoading = true;
    private bool _isExiting;

    public MainWindow(StorageService storage, DietPlanService dietPlan,
        WeightProjectionService projection, StartupService startup, CsvExportService csvExport)
    {
        InitializeComponent();
        _storage = storage;
        _dietPlan = dietPlan;
        _projection = projection;
        _startup = startup;
        _csvExport = csvExport;

        _positionSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _positionSaveTimer.Tick += PositionSaveTimer_Tick;

        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        System.Drawing.Icon trayDrawingIcon = File.Exists(iconPath)
            ? new System.Drawing.Icon(iconPath)
            : (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = trayDrawingIcon,
            Text = "스위치온 6주 체크 위젯",
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(RestoreFromTray);

        Forms.ContextMenuStrip trayMenu = new();
        trayMenu.Items.Add("위젯 열기", null, (_, _) => Dispatcher.Invoke(RestoreFromTray));
        trayMenu.Items.Add("완전히 종료", null, (_, _) => Dispatcher.Invoke(async () => await ExitApplicationAsync()));
        _trayIcon.ContextMenuStrip = trayMenu;
    }

    public async Task InitializeAsync()
    {
        (UserProfile profile, List<DailyRecord> records, AppSettings settings) = await _storage.InitializeAsync();
        _profile = profile;
        _records = records;
        _settings = settings;
        _settings.RunAtStartup = _startup.IsRegistered();
        _settings.LastRunDate = DateOnly.FromDateTime(DateTime.Now);

        ApplyWindowSettings();
        LoadToday();
        UpdateStartupStatus();
        _isLoading = false;
        await _storage.SaveSettingsAsync(_settings);
    }

    private void ApplyWindowSettings()
    {
        Topmost = _settings.Topmost;
        TopmostCheckBox.IsChecked = _settings.Topmost;

        if (_settings.WindowLeft is double left && _settings.WindowTop is double top &&
            left >= SystemParameters.VirtualScreenLeft - Width + 40 &&
            left <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 40 &&
            top >= SystemParameters.VirtualScreenTop - 20 &&
            top <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 40)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
    }

    private void LoadToday()
    {
        _isLoading = true;
        DateOnly localToday = DateOnly.FromDateTime(DateTime.Now);
        DateOnly displayDate = localToday < DietPlanService.StartDate
            ? DietPlanService.StartDate
            : localToday > DietPlanService.EndDate ? DietPlanService.EndDate : localToday;

        _currentDay = _dietPlan.GetDay(displayDate);
        _currentRecord = FindOrCreateRecord(_currentDay);

        TodayText.Text = localToday.ToString("yyyy년 M월 d일 dddd", CultureInfo.GetCultureInfo("ko-KR"));
        DayText.Text = $"DAY {_currentDay.Day}";

        if (localToday < DietPlanService.StartDate)
            PeriodStatusText.Text = $"시작일까지 {DietPlanService.StartDate.DayNumber - localToday.DayNumber}일 · 첫날 계획 미리보기";
        else if (localToday > DietPlanService.EndDate)
            PeriodStatusText.Text = "6주 프로그램 종료 · 마지막 날 기록 보기";
        else
            PeriodStatusText.Text = "Asia/Seoul 로컬 날짜 기준";

        double progress = Math.Round(_currentDay.Day / 42d * 100d);
        ProgramProgressBar.Value = progress;
        ProgressText.Text = $"{progress:0}%";

        BreakfastText.Text = $"아침  {_currentDay.Breakfast}";
        LunchText.Text = $"점심  {_currentDay.Lunch}";
        SnackText.Text = $"간식  {_currentDay.Snack}";
        DinnerText.Text = $"저녁  {_currentDay.Dinner}";
        ExtraText.Text = _currentDay.Extra;
        SpecialBadge.Visibility = _currentDay.IsSpecial ? Visibility.Visible : Visibility.Collapsed;
        SpecialText.Text = _currentDay.SpecialNote;

        ShakeCheckBox.IsChecked = _currentRecord.ShakeDone;
        LunchCheckBox.IsChecked = _currentRecord.LunchDone;
        DinnerCheckBox.IsChecked = _currentRecord.DinnerDone;
        WaterCheckBox.IsChecked = _currentRecord.WaterDone;
        ExerciseCheckBox.IsChecked = _currentRecord.ExerciseDone;
        WeightRecordedCheckBox.IsChecked = _currentRecord.WeightRecorded;
        WeightTextBox.Text = _currentRecord.Weight?.ToString("0.0", CultureInfo.InvariantCulture) ?? string.Empty;
        MemoTextBox.Text = _currentRecord.Memo;

        WeightProjection expected = _projection.GetProjection(_currentDay.Day, _profile);
        ExpectedWeightText.Text =
            $"예상  보수 {expected.Conservative:0.0}kg  ·  표준 {expected.Standard:0.0}kg  ·  도전 {expected.Challenge:0.0}kg";
        UpdateChecklistProgress();
        _isLoading = false;
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

    private async void RecordCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _currentRecord is null)
            return;

        CopyChecksToRecord();
        UpdateChecklistProgress();
        await SaveRecordsSafelyAsync("체크 상태 자동 저장됨");
    }

    private void CopyChecksToRecord()
    {
        if (_currentRecord is null)
            return;

        _currentRecord.ShakeDone = ShakeCheckBox.IsChecked == true;
        _currentRecord.LunchDone = LunchCheckBox.IsChecked == true;
        _currentRecord.DinnerDone = DinnerCheckBox.IsChecked == true;
        _currentRecord.WaterDone = WaterCheckBox.IsChecked == true;
        _currentRecord.ExerciseDone = ExerciseCheckBox.IsChecked == true;
        _currentRecord.WeightRecorded = WeightRecordedCheckBox.IsChecked == true;
    }

    private void UpdateChecklistProgress()
    {
        CopyChecksToRecord();
        ChecklistProgressText.Text = _currentRecord is null ? "0%" : $"{_currentRecord.CompletionPercent}%";
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e) => await SaveCurrentRecordAsync(showMessage: true);

    private async Task<bool> SaveCurrentRecordAsync(bool showMessage)
    {
        if (_currentRecord is null)
            return false;

        string rawWeight = WeightTextBox.Text.Trim();
        if (!string.IsNullOrEmpty(rawWeight))
        {
            if (!double.TryParse(rawWeight, NumberStyles.Float, CultureInfo.InvariantCulture, out double weight) &&
                !double.TryParse(rawWeight, NumberStyles.Float, CultureInfo.CurrentCulture, out weight))
            {
                ShowValidation("체중을 숫자로 입력해 주세요.");
                return false;
            }

            if (weight is < 30 or > 200)
            {
                ShowValidation("체중은 30kg 이상 200kg 이하로 입력해 주세요.");
                return false;
            }

            _currentRecord.Weight = Math.Round(weight, 1, MidpointRounding.AwayFromZero);
            _currentRecord.WeightRecorded = true;
            WeightRecordedCheckBox.IsChecked = true;
            WeightTextBox.Text = _currentRecord.Weight.Value.ToString("0.0", CultureInfo.InvariantCulture);
        }
        else
        {
            _currentRecord.Weight = null;
        }

        CopyChecksToRecord();
        _currentRecord.Memo = MemoTextBox.Text.Trim();
        UpdateChecklistProgress();
        await SaveRecordsSafelyAsync(showMessage ? "오늘 기록을 저장했습니다." : "자동 저장됨");
        return true;
    }

    private void ShowValidation(string message)
    {
        SaveStatusText.Text = message;
        WeightTextBox.Focus();
        WeightTextBox.SelectAll();
    }

    private async Task SaveRecordsSafelyAsync(string status)
    {
        try
        {
            await _storage.SaveRecordsAsync(_records);
            SaveStatusText.Text = $"{status}  {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            SaveStatusText.Text = "저장 실패";
            System.Windows.MessageBox.Show($"기록을 저장하지 못했습니다.\n\n{ex.Message}",
                "저장 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void CalendarButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveCurrentRecordAsync(showMessage: false);
        CalendarWindow window = new(_dietPlan, _projection, _storage, _profile, _records)
        {
            Owner = this
        };
        window.ShowDialog();
        LoadToday();
    }

    private async void WeightLogButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveCurrentRecordAsync(showMessage: false);
        WeightLogWindow window = new(_dietPlan, _projection, _storage, _csvExport, _profile, _records)
        {
            Owner = this
        };
        window.ShowDialog();
        LoadToday();
    }

    private async void RegisterStartupButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _startup.Register();
            _settings.RunAtStartup = _startup.IsRegistered();
            await _storage.SaveSettingsAsync(_settings);
            UpdateStartupStatus();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"자동실행을 등록하지 못했습니다.\n\n{ex.Message}",
                "자동실행", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void UnregisterStartupButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _startup.Unregister();
            _settings.RunAtStartup = false;
            await _storage.SaveSettingsAsync(_settings);
            UpdateStartupStatus();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"자동실행을 해제하지 못했습니다.\n\n{ex.Message}",
                "자동실행", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UpdateStartupStatus()
    {
        bool registered = _startup.IsRegistered();
        StartupStatusText.Text = registered ? "등록됨" : "해제됨";
        StartupStatusText.Foreground = registered
            ? (System.Windows.Media.Brush)FindResource("AccentBrush")
            : (System.Windows.Media.Brush)FindResource("MutedTextBrush");
    }

    private async void TopmostCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
            return;

        Topmost = TopmostCheckBox.IsChecked == true;
        _settings.Topmost = Topmost;
        await _storage.SaveSettingsAsync(_settings);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1 && e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => HideToTray();

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
            HideToTray();
    }

    private void HideToTray()
    {
        Hide();
        _trayIcon.ShowBalloonTip(1500, "스위치온 위젯",
            "시스템 트레이에서 계속 실행 중입니다. 아이콘을 두 번 눌러 다시 여세요.",
            Forms.ToolTipIcon.Info);
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = _settings.Topmost;
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (_isLoading || WindowState != WindowState.Normal)
            return;

        _positionSaveTimer.Stop();
        _positionSaveTimer.Start();
    }

    private async void PositionSaveTimer_Tick(object? sender, EventArgs e)
    {
        _positionSaveTimer.Stop();
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        await _storage.SaveSettingsAsync(_settings);
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e) => await ExitApplicationAsync();

    private async Task ExitApplicationAsync()
    {
        if (_isExiting)
            return;

        _isExiting = true;
        try
        {
            await SaveCurrentRecordAsync(showMessage: false);
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
            _settings.Topmost = TopmostCheckBox.IsChecked == true;
            _settings.LastRunDate = DateOnly.FromDateTime(DateTime.Now);
            await _storage.SaveSettingsAsync(_settings);
        }
        finally
        {
            _positionSaveTimer.Stop();
            _trayIcon.Visible = false;
            _trayIcon.Icon?.Dispose();
            _trayIcon.Dispose();
            System.Windows.Application.Current.Shutdown();
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isExiting)
            return;

        e.Cancel = true;
        HideToTray();
    }
}
