using System.Threading;
using System.Windows;
using System.Windows.Threading;
using SwitchOnWidget.Services;

namespace SwitchOnWidget;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, "SwitchOnWidget.SingleInstance", out bool createdNew);
        _ownsSingleInstanceMutex = createdNew;
        if (!createdNew)
        {
            System.Windows.MessageBox.Show("스위치온 위젯이 이미 실행 중입니다. 시스템 트레이를 확인해 주세요.",
                "스위치온 위젯", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        try
        {
            StorageService storage = new();
            DietPlanService dietPlan = new();
            WeightProjectionService projection = new();
            StartupService startup = new();
            CsvExportService csvExport = new();

            MainWindow window = new(storage, dietPlan, projection, startup, csvExport);
            MainWindow = window;
            window.Show();
            await window.InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"앱을 시작하지 못했습니다.\n\n{ex.Message}",
                "시작 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        System.Windows.MessageBox.Show($"처리 중 오류가 발생했습니다. 저장된 데이터는 유지됩니다.\n\n{e.Exception.Message}",
            "스위치온 위젯", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsSingleInstanceMutex)
            _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
