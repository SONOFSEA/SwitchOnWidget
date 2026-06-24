using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace SwitchOnWidget.Services;

public sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SwitchOnWidget";

    public bool IsRegistered()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public void Register()
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(ValueName, BuildLaunchCommand(), RegistryValueKind.String);
    }

    public void Unregister()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string BuildLaunchCommand()
    {
        string processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("현재 실행 파일 경로를 확인할 수 없습니다.");

        if (!string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet",
                StringComparison.OrdinalIgnoreCase))
        {
            return $"\"{processPath}\"";
        }

        // dotnet run 환경에서도 등록 테스트가 가능하도록 DLL 경로를 함께 기록한다.
        string assemblyPath = Assembly.GetEntryAssembly()?.Location
            ?? throw new InvalidOperationException("앱 어셈블리 경로를 확인할 수 없습니다.");
        return $"\"{processPath}\" \"{assemblyPath}\"";
    }
}
