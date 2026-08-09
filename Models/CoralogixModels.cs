using System.Text.Json.Serialization;
using CsvHelper.Configuration;

namespace DQEHelper.Models
{
    // Модель для отображения в результатах поиска
    public record CoralogixLogEntry(string ProviderScanId, string TaskId);

    // Модель для итогового отчета (Корзины)
    public record ScanReportItem
    {
        public string ProviderScanId { get; init; } = string.Empty;
        public string TaskId { get; init; } = string.Empty;

        // Кастомные поля для отчета
        public string Flag1_0 { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string CustomProvider { get; set; } = string.Empty;

        // Поля из внутреннего JSON API
        public string SnapshotUrl { get; set; } = string.Empty;
        public string DeepLink { get; set; } = string.Empty;
        public string Pos { get; set; } = string.Empty;

        // Полный слепок исходного JSON
        public string ExtData { get; set; } = string.Empty;
        public string HdsUrl { get; set; } = string.Empty;
    }

    // Правила маппинга для итогового CSV отчета
    public sealed class ScanReportItemMap : ClassMap<ScanReportItem>
    {
        public ScanReportItemMap()
        {
            Map(m => m.Flag1_0).Name("1/0").Index(0);
            Map(m => m.Comment).Name("comment").Index(1);
            Map(m => m.CustomProvider).Name("_provider").Index(2);
            Map(m => m.HdsUrl).Name("HDS").Index(3);
            Map(m => m.ExtData).Name("ext_data").Index(4);
            Map(m => m.SnapshotUrl).Name("snapshot_url").Index(5);
            Map(m => m.DeepLink).Name("deep_link").Index(6);
            Map(m => m.Pos).Name("pos").Index(7);
        }
    }
}