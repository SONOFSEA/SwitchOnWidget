using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SwitchOnWidget.Models;

namespace SwitchOnWidget.Services;

public sealed class StorageService
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SwitchOnWidget");

    private string ProfilePath => Path.Combine(DataDirectory, "profile.json");
    private string RecordsPath => Path.Combine(DataDirectory, "records.json");
    private string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    public async Task<(UserProfile Profile, List<DailyRecord> Records, AppSettings Settings)> InitializeAsync()
    {
        Directory.CreateDirectory(DataDirectory);

        UserProfile profile = await LoadOrDefaultAsync(ProfilePath, () => new UserProfile());
        List<DailyRecord> records = await LoadOrDefaultAsync(RecordsPath, () => new List<DailyRecord>());
        AppSettings settings = await LoadOrDefaultAsync(SettingsPath, () => new AppSettings());

        await SaveProfileAsync(profile);
        await SaveRecordsAsync(records);
        await SaveSettingsAsync(settings);
        return (profile, records, settings);
    }

    public Task SaveProfileAsync(UserProfile profile) => WriteJsonAsync(ProfilePath, profile);
    public Task SaveRecordsAsync(IEnumerable<DailyRecord> records) =>
        WriteJsonAsync(RecordsPath, records.OrderBy(record => record.Date).ToList());
    public Task SaveSettingsAsync(AppSettings settings) => WriteJsonAsync(SettingsPath, settings);

    private async Task<T> LoadOrDefaultAsync<T>(string path, Func<T> createDefault)
    {
        if (!File.Exists(path))
            return createDefault();

        try
        {
            await using FileStream stream = File.OpenRead(path);
            T? value = await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions);
            return value ?? createDefault();
        }
        catch (Exception) when (File.Exists(path))
        {
            // JSON이 깨졌더라도 앱은 계속 실행하고 손상 파일은 별도 보관한다.
            try
            {
                string backup = $"{path}.corrupt-{DateTime.Now:yyyyMMddHHmmss}";
                File.Copy(path, backup, overwrite: true);
            }
            catch
            {
                // 백업 실패는 기본값 복구를 막지 않는다.
            }

            return createDefault();
        }
    }

    private async Task WriteJsonAsync<T>(string path, T value)
    {
        await _writeLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(DataDirectory);
            string tempPath = path + ".tmp";
            await using (FileStream stream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, value, _jsonOptions);
                await stream.FlushAsync();
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
