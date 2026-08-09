using System.Text.Json.Serialization;

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
        public string ScreengrabUrl { get; set; } = string.Empty;
        public string DeepLink { get; set; } = string.Empty;
        public string Pos { get; set; } = string.Empty;

        // Полный слепок исходного JSON
        public string ExtData { get; set; } = string.Empty;
    }
}