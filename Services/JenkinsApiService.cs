using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DQEHelper.Services
{
    public class JenkinsApiService
    {
        // Отключаем авто-редирект, так как успешный запуск вернет 201 Created
        // с заголовком Location, указывающим на queue item
        private static readonly HttpClient _httpClient = new(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        });

        private readonly string _baseUrl;
        private readonly string _cookieString;
        private readonly string _crumbValue;

        public JenkinsApiService(string baseUrl, string cookieString, string crumbValue)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _cookieString = cookieString;
            _crumbValue = crumbValue;
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> TriggerJobAsync(string jobName, Dictionary<string, string> parameters, CancellationToken ct = default)
        {
            try
            {
                // Официальный API-эндпоинт для запуска параметризованной джобы.
                // Не нужно имитировать внутреннюю Stapler-форму (json/name/value/statusCode/redirectTo) -
                // именно эта имитация чаще всего и приводит к 500 на стороне Jenkins,
                // так как формат этой формы завязан на конкретную версию Jenkins/плагинов.
                var queryBuilder = new StringBuilder();
                foreach (var p in parameters)
                {
                    if (queryBuilder.Length > 0) queryBuilder.Append('&');
                    queryBuilder.Append(Uri.EscapeDataString(p.Key));
                    queryBuilder.Append('=');
                    queryBuilder.Append(Uri.EscapeDataString(p.Value));
                }

                string requestUri = $"{_baseUrl}/job/{Uri.EscapeDataString(jobName)}/buildWithParameters?{queryBuilder}";

                using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);

                request.Headers.Add("Cookie", _cookieString);
                if (!string.IsNullOrWhiteSpace(_crumbValue))
                {
                    request.Headers.Add("Jenkins-Crumb", _crumbValue);
                }

                using var response = await _httpClient.SendAsync(request, ct);

                // Успешный запуск через buildWithParameters обычно отдает 201 Created
                // (с Location на /queue/item/...). 302/303 тоже иногда встречается в зависимости
                // от версии Jenkins.
                if (response.StatusCode == HttpStatusCode.Created ||
                    response.StatusCode == HttpStatusCode.SeeOther ||
                    response.StatusCode == HttpStatusCode.Found ||
                    response.IsSuccessStatusCode)
                {
                    return (true, string.Empty);
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync(ct);

                    // временно: сохраняем полный ответ на диск для диагностики
                    string dumpPath = string.Empty;
                    try
                    {
                        dumpPath = System.IO.Path.Combine(
                            System.IO.Path.GetTempPath(), $"jenkins_error_{jobName}_{DateTime.Now:HHmmss}.html");
                        await System.IO.File.WriteAllTextAsync(dumpPath, errorContent, ct);
                    }
                    catch { /* игнор, это только для дебага */ }

                    string details = ExtractJenkinsErrorDetails(errorContent);
                    return (false, $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase}) | {details} | dump: {dumpPath}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Network exception: {ex.Message}");
            }
        }

        // Jenkins обычно кладет суть ошибки в <title> или в первый <h1>/<pre> на странице,
        // а не в начало HTML (там просто <head> с ресурсами). Достаём именно эту часть,
        // чтобы реально было видно, что сломалось.
        private static string ExtractJenkinsErrorDetails(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return "(пустой ответ)";

            string title = ExtractBetween(html, "<title>", "</title>");
            string h1 = ExtractBetween(html, "<h1>", "</h1>");
            string pre = ExtractBetween(html, "<pre>", "</pre>");

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(title)) parts.Add($"Title: {title}");
            if (!string.IsNullOrWhiteSpace(h1)) parts.Add($"H1: {h1}");
            if (!string.IsNullOrWhiteSpace(pre))
            {
                if (pre.Length > 500) pre = pre.Substring(0, 500) + "...";
                parts.Add($"Details: {pre}");
            }

            if (parts.Count == 0)
            {
                // fallback - просто обрежем как раньше, но чуть больше
                return html.Length > 400 ? html.Substring(0, 400) + "..." : html;
            }

            return string.Join(" | ", parts);
        }

        private static string ExtractBetween(string source, string start, string end)
        {
            int startIdx = source.IndexOf(start, StringComparison.OrdinalIgnoreCase);
            if (startIdx < 0) return string.Empty;
            startIdx += start.Length;
            int endIdx = source.IndexOf(end, startIdx, StringComparison.OrdinalIgnoreCase);
            if (endIdx < 0) return string.Empty;
            return source.Substring(startIdx, endIdx - startIdx).Trim();
        }
    }
}