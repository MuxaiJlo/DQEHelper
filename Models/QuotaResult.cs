namespace DQEHelper.Models
{
    // Record - идеальный тип для хранения неизменяемых данных отчета
    public record QuotaResult(string ProviderName, int Available, int Unavailable, bool IsComplete);
}