using System.Globalization;
using System.IO;
using CsvHelper;
using CsvHelper.Configuration;
using DQEHelper.Models;

namespace DQEHelper.Services
{
    public class CsvProcessingService
    {
        public async Task ProcessCsvAsync(string inputFilePath, string outputFilePath, string[] searchPatterns)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null, // Игнорируем отсутствие колонок
                BadDataFound = null       // Пропускаем битые строки
            };

            // Используем FileOptions.Asynchronous для неблокирующего I/O
            using var reader = new StreamReader(new FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous));
            using var csvReader = new CsvReader(reader, config);
            csvReader.Context.RegisterClassMap<DqeCsvRecordMap>();

            using var writer = new StreamWriter(new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous));
            using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csvWriter.Context.RegisterClassMap<DqeCsvRecordMap>();

            // Пишем заголовки в новый файл сразу
            csvWriter.WriteHeader<DqeCsvRecord>();
            await csvWriter.NextRecordAsync();

            // Читаем исходный файл построчно (Streaming)
            while (await csvReader.ReadAsync())
            {
                var record = csvReader.GetRecord<DqeCsvRecord>();

                // Проверка на совпадения (OrdinalIgnoreCase заменяет (?i) из PowerShell)
                bool isMatch = false;
                
                // Защита от NullReferenceException
                string provider = record.DealsProvider ?? string.Empty;
                string url = record.Url ?? string.Empty;
                string screengrab = record.Screengrab ?? string.Empty;

                foreach (var pattern in searchPatterns)
                {
                    if (provider.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                        url.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                        screengrab.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = true;
                        break;
                    }
                }

                if (isMatch)
                {
                    // Пишем только те строки, которые подошли
                    csvWriter.WriteRecord(record);
                    await csvWriter.NextRecordAsync();
                }
            }
        }
    }
}