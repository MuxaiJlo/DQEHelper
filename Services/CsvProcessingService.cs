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

            public ProviderQuota(string name)
            {
                Name = name;
            }
        }

        public async Task<string> ProcessCsvAsync(string inputFilePath, string outputFilePath, string[] searchPatterns)
        {
            var quotas = searchPatterns
                .Select(p => new ProviderQuota(p))
                .ToList();

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null, 
                BadDataFound = null       
            };

            using var reader = new StreamReader(new FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous));
            using var csvReader = new CsvReader(reader, config);
            csvReader.Context.RegisterClassMap<DqeCsvRecordMap>();

            using var writer = new StreamWriter(new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous));
            using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csvWriter.Context.RegisterClassMap<DqeCsvRecordMap>();

            csvWriter.WriteHeader<DqeCsvRecord>();
            await csvWriter.NextRecordAsync();

            while (await csvReader.ReadAsync())
            {
                if (quotas.All(q => q.IsComplete))
                {
                    break;
                }

                var record = csvReader.GetRecord<DqeCsvRecord>();
                
                // Вытаскиваем нужные поля
                string providerField = record.DealsProvider ?? string.Empty;
                string urlField = record.Url ?? string.Empty;
                string screengrabField = record.Screengrab ?? string.Empty;
                
                string hotelKey = !string.IsNullOrWhiteSpace(record.HotelName) ? record.HotelName : urlField;
                
                // 1. ПРОВЕРКА ДОСТУПНОСТИ: Используем только колонку deals.provider
                bool isAvailable = !string.IsNullOrWhiteSpace(providerField);

                foreach (var quota in quotas)
                {
                    if (quota.IsComplete) continue;

                    // 2. ИДЕНТИФИКАЦИЯ ПРОВАЙДЕРА: Ищем совпадения только в URL и Screengrab
                    if (urlField.Contains(quota.Name, StringComparison.OrdinalIgnoreCase) ||
                        screengrabField.Contains(quota.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        if (quota.SeenHotels.Contains(hotelKey))
                        {
                            continue;
                        }

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
                            csvWriter.WriteRecord(record);
                            await csvWriter.NextRecordAsync();
                        }

                        break;
                    }
                }
            }

            var summary = new System.Text.StringBuilder("Отчет по выборке:\n\n");
            foreach (var quota in quotas)
            {
                summary.AppendLine($"Провайдер: {quota.Name}");
                summary.AppendLine($"- Доступных: {quota.AvailableCount}/7");
                summary.AppendLine($"- Недоступных: {quota.UnavailableCount}/4");
                if (!quota.IsComplete)
                {
                    summary.AppendLine("  (В архиве не хватило уникальных данных)");
                }
                summary.AppendLine();
            }

            return summary.ToString();
        }
    }
}