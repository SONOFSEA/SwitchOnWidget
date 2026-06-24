using System.Globalization;
using System.IO;
using System.Text;
using SwitchOnWidget.Models;

namespace SwitchOnWidget.Services;

public sealed class CsvExportService
{
    public async Task ExportAsync(string path, IEnumerable<DietDay> days,
        IReadOnlyDictionary<DateOnly, DailyRecord> records, UserProfile profile,
        WeightProjectionService projectionService)
    {
        StringBuilder csv = new();
        csv.AppendLine("date,day,expected_conservative,expected_standard,expected_challenge,actual_weight,memo");

        foreach (DietDay day in days.OrderBy(day => day.Date))
        {
            WeightProjection expected = projectionService.GetProjection(day.Day, profile);
            records.TryGetValue(day.Date, out DailyRecord? record);
            string actual = record?.Weight?.ToString("0.0", CultureInfo.InvariantCulture) ?? string.Empty;
            string memo = Escape(record?.Memo ?? string.Empty);

            csv.Append(day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                .Append(day.Day).Append(',')
                .Append(expected.Conservative.ToString("0.0", CultureInfo.InvariantCulture)).Append(',')
                .Append(expected.Standard.ToString("0.0", CultureInfo.InvariantCulture)).Append(',')
                .Append(expected.Challenge.ToString("0.0", CultureInfo.InvariantCulture)).Append(',')
                .Append(actual).Append(',').AppendLine(memo);
        }

        await File.WriteAllTextAsync(path, csv.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
