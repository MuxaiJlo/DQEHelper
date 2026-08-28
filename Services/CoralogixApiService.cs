using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DQEHelper.Models;

namespace DQEHelper.Services
{
    public class CoralogixApiService
    {
        private readonly HttpClient _httpClient;

        // Используем внутренний URL, который ты нашел
        private readonly string _coralogixSearchUrl = "https://fornova_507.coralogix.com/opendashboards/internal/search/opensearch";

        public CoralogixApiService(string cookieString)
        {
            _httpClient = new HttpClient();

            // Маскируемся под браузер
            _httpClient.DefaultRequestHeaders.Add("Cookie", cookieString);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");

            // Этот заголовок часто обязателен для внутренних API OpenSearch/Kibana
            _httpClient.DefaultRequestHeaders.Add("osd-xsrf", "true");
            _httpClient.DefaultRequestHeaders.Add("kbn-xsrf", "true");
        }

        public async Task<List<CoralogixLogEntry>> SearchLogsAsync(
            string customer, string provider, string scanMethod,
            string accessLevel, string stage, string availability, int limit, string timeRange)
        {
            var mustConditions = new List<object>();

            // Базовый фильтр подсистемы (Используем Dictionary для полей с точками)
            mustConditions.Add(new
            {
                @bool = new
                {
                    should = new[] {
                        new { match_phrase = new Dictionary<string, string> { { "coralogix.metadata.subsystemName", "etl-hotel-prod" } } },
                        new { match_phrase = new Dictionary<string, string> { { "coralogix.metadata.subsystemName", "etl-allrooms-prod" } } }
                    },
                    minimum_should_match = 1
                }
            });

            // Динамические фильтры из UI
            if (!string.IsNullOrWhiteSpace(customer))
                mustConditions.Add(new { match_phrase = new Dictionary<string, string> { { "log.customerName", customer } } });

            if (!string.IsNullOrWhiteSpace(provider))
                mustConditions.Add(new { match_phrase = new Dictionary<string, string> { { "log.provider", provider } } });

            if (scanMethod != "Any")
                mustConditions.Add(new { match_phrase = new Dictionary<string, string> { { "log.scanMethod", scanMethod } } });

            if (accessLevel != "Any")
                mustConditions.Add(new { match_phrase = new Dictionary<string, string> { { "log.accessLevel.keyword", accessLevel } } });

            if (stage != "Any")
                mustConditions.Add(new { match_phrase = new Dictionary<string, string> { { "log.stage", stage } } });

            if (availability != "Any")
                mustConditions.Add(new { match_phrase = new Dictionary<string, string> { { "log.availability", availability } } });

            // Добавляем фильтр по времени (например: "1h", "6h", "12h", "24h")
            if (!string.IsNullOrWhiteSpace(timeRange) && timeRange != "Any")
            {
                int hours = 0;
                if (timeRange.EndsWith("h") && int.TryParse(timeRange.Substring(0, timeRange.Length - 1), out var h)) hours = h;
                else if (int.TryParse(timeRange, out var h2)) hours = h2;

                if (hours > 0)
                {
                    mustConditions.Add(new
                    {
                        range = new Dictionary<string, object>
                        {
                            { "coralogix.timestamp", new Dictionary<string, string> { { "gte", $"now-{hours}h" }, { "lte", "now" } } }
                        }
                    });
                }
            }

            var opensearchPayload = new
            {
                @params = new
                {
                    index = "*:507_newlogs*",
                    body = new
                    {
                        size = limit,
                        query = new { @bool = new { must = mustConditions } },
                        _source = new { includes = new[] { "log.providerScanId", "log.taskId" } },

                        // Сортировку тоже исправляем через словарь
                        sort = new[] { new Dictionary<string, string> { { "coralogix.timestamp", "desc" } } }
                    }
                }
            };

            string jsonBody = JsonSerializer.Serialize(opensearchPayload);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            // --- ЛОГИРОВАНИЕ ДЛЯ ОТЛАДКИ ---
            Debug.WriteLine("=== CORALOGIX API REQUEST ===");
            Debug.WriteLine(jsonBody);

            var response = await _httpClient.PostAsync(_coralogixSearchUrl, content);
            string responseJson = await response.Content.ReadAsStringAsync();
            // Быстрая проверка: если сервер вернул HTML (например, страница логина или ошибка),
            // избежать JsonException и дать понятную подсказку пользователю.
            var trimmed = responseJson?.TrimStart();
            if (!string.IsNullOrEmpty(trimmed) && trimmed.StartsWith("<"))
            {
                string preview = trimmed.Length > 800 ? trimmed.Substring(0, 800) + "..." : trimmed;
                throw new Exception($"Coralogix вернул HTML вместо JSON (возможная страница логина/ошибка). Проверьте cookie/доступ/VPN. Response start: {preview}");
            }

            Debug.WriteLine("=== CORALOGIX API RESPONSE ===");
            Debug.WriteLine($"STATUS: {response.StatusCode}");
            Debug.WriteLine(responseJson);
            // -------------------------------

            if (!response.IsSuccessStatusCode)
            {
                string snippet = responseJson?.Length > 1000 ? responseJson.Substring(0, 1000) + "..." : responseJson;
                throw new Exception($"Ошибка Coralogix ({response.StatusCode}). Response: {snippet}");
            }

            // Попытка распарсить JSON; если сервер вернул HTML (начинается с '<') или другой мусор,
            // покажем понятную ошибку вместо необработанного JsonException.
            try
            {
                return ParseElasticsearchResponse(responseJson);
            }
            catch (System.Text.Json.JsonException jex)
            {
                string preview = responseJson?.TrimStart();
                if (preview != null && preview.Length > 200) preview = preview.Substring(0, 200) + "...";
                throw new Exception($"Невалидный JSON от Coralogix. Начало ответа: {preview}. ParseError: {jex.Message}");
            }
        }

        // ЭТАП 2: Запрос сырого JSON из GCP API
        public async Task<string> GetRawScanDataAsync(string providerScanId, string taskId)
        {
            // 1. Очищаем от случайных пробелов
            string cleanScanId = providerScanId.Trim();
            string cleanTaskId = taskId.Trim();

            // 2. Кодируем параметры для безопасности URL (превратит ":" в "%3A")
            string safeScanId = Uri.EscapeDataString(cleanScanId);
            string safeTaskId = Uri.EscapeDataString(cleanTaskId);

            // 3. Формируем URL БЕЗ хардкода "::0", так как он уже есть в taskId
            string url = $"http://historical-data-api-prod.prod.gcphosts.net:5000/data/hot/{safeScanId}?id={safeTaskId}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string jsonResult = await response.Content.ReadAsStringAsync();

            // Форматируем JSON для красивого отображения в UI
            using var jsonDoc = JsonDocument.Parse(jsonResult);
            return JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
        }

        private List<CoralogixLogEntry> ParseElasticsearchResponse(string jsonResponse)
        {
            var results = new List<CoralogixLogEntry>();
            using var jsonDoc = JsonDocument.Parse(jsonResponse);

            // OpenSearch может возвращать данные прямо в hits или внутри rawResponse.hits
            var root = jsonDoc.RootElement;
            if (root.TryGetProperty("rawResponse", out var rawResp))
            {
                root = rawResp;
            }

            if (root.TryGetProperty("hits", out var rootHits) && rootHits.TryGetProperty("hits", out var hitsArray))
            {
                foreach (var hit in hitsArray.EnumerateArray())
                {
                    if (hit.TryGetProperty("_source", out var source) && source.TryGetProperty("log", out var log))
                    {
                        string scanId = log.TryGetProperty("providerScanId", out var sId) ? sId.GetString() ?? "" : "";
                        string taskId = log.TryGetProperty("taskId", out var tId) ? tId.GetString() ?? "" : "";

                        if (!string.IsNullOrEmpty(scanId) && !string.IsNullOrEmpty(taskId))
                        {
                            results.Add(new CoralogixLogEntry(scanId, taskId));
                        }
                    }
                }
            }
            return results;
        }
    }
}