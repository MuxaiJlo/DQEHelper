using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using DQEHelper.Models;

namespace DQEHelper.Services
{
    public class CsvProcessingService
    {
        private class ProviderQuota
        {
            public string Name { get; }
            public int AvailableCount { get; set; } = 0;
            public int UnavailableCount { get; set; } = 0;
            public HashSet<string> SeenHotels { get; } = new(StringComparer.OrdinalIgnoreCase);
            public bool IsComplete => AvailableCount >= 7 && UnavailableCount >= 4;

            // У каждого провайдера теперь свое персональное правило поиска!
            public Func<DqeCsvRecord, bool> IsMatch { get; }

            public ProviderQuota(string name, Func<DqeCsvRecord, bool> matchRule)
            {
                Name = name;
                IsMatch = matchRule;
            }
        }

        // 🧠 ФАБРИКА ПРАВИЛ ФИЛЬТРАЦИИ
        private Func<DqeCsvRecord, bool> GetMatchRule(string providerName)
        {
            string normalized = providerName.Trim().ToLowerInvariant();

            return normalized switch
            {
                // Профиль Google: Ищем google.com/google.ru строго в URL. Игнорируем screengrab.
                "google" => (record) =>
                    (record.Url ?? "").Contains("google.com", StringComparison.OrdinalIgnoreCase) ||
                    (record.Url ?? "").Contains("google.co", StringComparison.OrdinalIgnoreCase),

                // Профиль Traveloka Mobile: URL пустой, ищем метку в скрингрэбе 
                "traveloka mobile" or "travelokamobile" => (record) =>
                    // Проходит проверку если URL пустой/нулл, либо есть метка в screengrab
                    string.IsNullOrWhiteSpace(record.Url) &&
                    (record.Screengrab ?? "").Contains("traveloka", StringComparison.OrdinalIgnoreCase),

                // Профиль MakeMyTrip Mobile: URL пустой, ищем метку в скрингрэбе 
                "makemytrip mobile" or "makemytripmobile" => (record) =>
                    // Проходит проверку если URL пустой/нулл, либо есть метка в screengrab
                    string.IsNullOrWhiteSpace(record.Url) &&
                    (record.Screengrab ?? "").Contains("makemytrip", StringComparison.OrdinalIgnoreCase),

                "wotif mobile" or "wotifmobile" => (record) =>
                    // Проходит проверку если URL пустой/нулл, либо есть метка в screengrab
                    string.IsNullOrWhiteSpace(record.Url) &&
                    (record.Screengrab ?? "").Contains("wotif", StringComparison.OrdinalIgnoreCase),

                // Профиль по умолчанию
                _ => (record) =>
                    (record.Url ?? "").Contains(providerName, StringComparison.OrdinalIgnoreCase) ||
                    (record.Screengrab ?? "").Contains(providerName, StringComparison.OrdinalIgnoreCase)
            };
        }

        public async Task<List<QuotaResult>> ProcessCsvAsync(string inputFilePath, string outputFilePath, string[] searchPatterns)
        {
            // Назначаем каждому провайдеру его правило при создании
            var quotas = searchPatterns
                .Select(p => new ProviderQuota(p, GetMatchRule(p)))
                .ToList();

            var collectedRecords = new List<DqeCsvRecord>();

            var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true, MissingFieldFound = null, BadDataFound = null };

            using (var reader = new StreamReader(new FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous)))
            using (var csvReader = new CsvReader(reader, config))
            {
                csvReader.Context.RegisterClassMap<DqeCsvRecordMap>();
                await csvReader.ReadAsync();
                csvReader.ReadHeader();

                while (await csvReader.ReadAsync())
                {
                    if (quotas.All(q => q.IsComplete)) break;

                    var record = csvReader.GetRecord<DqeCsvRecord>();
                    string providerField = record.DealsProvider ?? string.Empty;
                    string urlField = record.Url ?? string.Empty;

                    string hotelKey = !string.IsNullOrWhiteSpace(record.HotelName) ? record.HotelName : urlField;
                    bool isAvailable = !string.IsNullOrWhiteSpace(providerField);

                    foreach (var quota in quotas)
                    {
                        if (quota.IsComplete) continue;

                        // Вызываем персональное правило профиля!
                        if (quota.IsMatch(record))
                        {
                            if (quota.SeenHotels.Contains(hotelKey)) continue;

                            bool writeRecord = false;
                            if (isAvailable && quota.AvailableCount < 7)
                            {
                                quota.AvailableCount++;
                                writeRecord = true;
                            }
                            else if (!isAvailable && quota.UnavailableCount < 4)
                            {
                                quota.UnavailableCount++;
                                writeRecord = true;
                            }

                            if (writeRecord)
                            {
                                quota.SeenHotels.Add(hotelKey);
                                collectedRecords.Add(record);
                            }
                            break;
                        }
                    }
                }
            }

            var sortedRecords = collectedRecords.OrderBy(r => r.Screengrab ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToList();

            using (var writer = new StreamWriter(new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous)))
            using (var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csvWriter.Context.RegisterClassMap<DqeCsvRecordMap>();
                csvWriter.WriteHeader<DqeCsvRecord>();
                await csvWriter.NextRecordAsync();
                await csvWriter.WriteRecordsAsync(sortedRecords);
            }

            // Возвращаем структурированный список вместо текста
            return quotas.Select(q => new QuotaResult(q.Name, q.AvailableCount, q.UnavailableCount, q.IsComplete)).ToList();
        }
    }
}