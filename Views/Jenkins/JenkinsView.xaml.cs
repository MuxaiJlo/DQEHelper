using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using DQEHelper.Models;

namespace DQEHelper.Views.Jenkins
{
    public partial class JenkinsView : UserControl
    {
        private const string StartUrl = "https://ci-gcp.fornova.net/view/Temerix--Data-Checking/job/Temerix--Data-Checking-Snapshot-for-Agoda-test/";
        private readonly ObservableCollection<ProviderNode> _parsedReports = new();

        private int _parsedTotalSnaps = 10;

        // 🚀 НОВЫЕ ПЕРЕМЕННЫЕ: Храним статус и текст пропущенного теста
        private string _lastTestStatus = "Unknown";
        private string _lastSkippedMessage = "";

        public JenkinsView()
        {
            InitializeComponent();
            ReportTreeView.ItemsSource = _parsedReports;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await JenkinsWebView.EnsureCoreWebView2Async(null);
            JenkinsWebView.CoreWebView2.Navigate(StartUrl);
            Log("Браузер готов. Открой отчет Allure.");
        }

        private void Log(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            ConsoleLogTextBox.AppendText($"[{time}] {message}\n");
            ConsoleLogTextBox.ScrollToEnd();
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            JenkinsWebView.CoreWebView2.Navigate(StartUrl);
            Log("Возврат на стартовую страницу Jenkins...");
        }

        private async void ParseAllureButton_Click(object sender, RoutedEventArgs e)
        {
            Log("⏳ Чтение данных со страницы...");
            _parsedReports.Clear();
            _lastTestStatus = "Unknown";
            _lastSkippedMessage = "";

            string jsCode = @"
                (function() {
                    try {
                        let providerName = 'UnknownProvider';
                        let titles = document.querySelectorAll('.long-line');
                        for (let t of titles) {
                            if (t.innerText.includes('check') && t.innerText.includes('Results')) {
                                providerName = t.innerText;
                                break;
                            }
                        }

                        let statusElement = document.querySelector('.test-result__status .label');
                        let testStatus = statusElement ? statusElement.innerText.trim() : 'Unknown';

                        let errorBlock = document.querySelector('.status-details__message code');
                        let errorText = errorBlock ? errorBlock.innerText : '';

                        let maxSnap = 0;
                        let stepNames = document.querySelectorAll('.step__name');
                        for (let step of stepNames) {
                            let match = step.innerText.match(/Compare snap index:\s*'(\d+)'/i);
                            if (match) {
                                let snapIdx = parseInt(match[1], 10);
                                if (snapIdx > maxSnap) {
                                    maxSnap = snapIdx;
                                }
                            }
                        }

                        return JSON.stringify({ 
                            Provider: providerName, 
                            Errors: errorText,
                            TotalSnaps: maxSnap,
                            Status: testStatus
                        });
                    } catch(e) {
                        return JSON.stringify({ Error: e.message });
                    }
                })();
            ";

            try
            {
                string rawResult = await JenkinsWebView.CoreWebView2.ExecuteScriptAsync(jsCode);
                string unescapedJson = Regex.Unescape(rawResult?.Trim('"') ?? "");

                if (string.IsNullOrWhiteSpace(unescapedJson) || unescapedJson.Contains("\"Error\""))
                {
                    Log("❌ Ошибка: убедись, что открыт отчет Allure.");
                    return;
                }

                using var doc = JsonDocument.Parse(unescapedJson);
                string rawProvider = doc.RootElement.GetProperty("Provider").GetString() ?? "Unknown";
                string rawErrorText = doc.RootElement.GetProperty("Errors").GetString() ?? "";

                if (doc.RootElement.TryGetProperty("Status", out var statusElement))
                {
                    _lastTestStatus = statusElement.GetString() ?? "Unknown";
                }

                if (doc.RootElement.TryGetProperty("TotalSnaps", out var totalSnapsElement))
                {
                    int parsedTotal = totalSnapsElement.GetInt32();
                    if (parsedTotal > 0)
                    {
                        _parsedTotalSnaps = parsedTotal;
                        Log($"🔢 Авто-определение: найдено {_parsedTotalSnaps} снэпов.");
                    }
                }

                string cleanProvider = Regex.Replace(rawProvider, @"^.*check(.*)Results.*$", "$1", RegexOptions.IgnoreCase).Trim();
                if (string.IsNullOrEmpty(cleanProvider)) cleanProvider = rawProvider;

                if (_lastTestStatus.Equals("Passed", StringComparison.OrdinalIgnoreCase))
                {
                    _parsedReports.Add(new ProviderNode { Title = $"Провайдер: {cleanProvider} (PASSED)" });
                    Log($"✅ Тест успешен (Passed) для {cleanProvider}");
                    return;
                }

                if (_lastTestStatus.Equals("Skipped", StringComparison.OrdinalIgnoreCase) ||
                    _lastTestStatus.Equals("Broken", StringComparison.OrdinalIgnoreCase))
                {
                    _lastSkippedMessage = rawErrorText.Trim();
                    _parsedReports.Add(new ProviderNode { Title = $"Провайдер: {cleanProvider} (SKIPPED)" });
                    Log($"⚠️ Тест пропущен/сломан (Skipped) для {cleanProvider}");
                    return;
                }

                if (string.IsNullOrWhiteSpace(rawErrorText))
                {
                    Log("⚠️ Статус Failed, но текст ошибки не найден.");
                    return;
                }

                var providerNode = new ProviderNode { Title = $"Провайдер: {cleanProvider}" };
                string pattern = @"(?<error>.*?),\s*Additional info:\s*Snap index :\s*(?<snap>\d+)";
                var matches = Regex.Matches(rawErrorText, pattern, RegexOptions.Singleline);

                foreach (Match match in matches)
                {
                    string rawError = match.Groups["error"].Value.Replace("The following asserts failed:", "").Trim();

                    int roomIndex = 0;
                    var roomMatch = Regex.Match(rawError, @"Room index\s*[:\-]?\s*(?<room>\d+)", RegexOptions.IgnoreCase);
                    if (roomMatch.Success)
                    {
                        roomIndex = int.Parse(roomMatch.Groups["room"].Value);
                        rawError = rawError.Remove(roomMatch.Index, roomMatch.Length);
                    }

                    string cleanError = rawError
                        .Replace("\n", " ")
                        .Replace("\r", "")
                        .Replace("\t", "")
                        .TrimStart('-', ',', ':', ' ');

                    int cutIdx = cleanError.IndexOfAny(new[] { '.', ',' });
                    if (cutIdx > 0)
                    {
                        cleanError = cleanError.Substring(0, cutIdx).Trim();
                    }
                    if (!cleanError.EndsWith(".")) cleanError += ".";

                    int snapId = int.Parse(match.Groups["snap"].Value);

                    // 🚀 НОВАЯ ИЕРАРХИЯ: Provider -> Error -> Snap -> Room
                    var errorGroup = providerNode.Errors.FirstOrDefault(e => e.Title == cleanError);
                    if (errorGroup == null)
                    {
                        errorGroup = new ErrorGroupNode { Title = cleanError };
                        providerNode.Errors.Add(errorGroup);
                    }

                    var snapNode = errorGroup.Snaps.FirstOrDefault(s => s.SnapIndex == snapId);
                    if (snapNode == null)
                    {
                        snapNode = new SnapNode { SnapIndex = snapId, Title = $"Snap Index: {snapId}" };
                        errorGroup.Snaps.Add(snapNode);
                    }

                    var roomNode = snapNode.Rooms.FirstOrDefault(r => r.RoomIndex == roomIndex);
                    if (roomNode == null)
                    {
                        string roomTitle = roomIndex == 0 ? "General Errors" : $"Room Index: {roomIndex}";
                        roomNode = new RoomNode { RoomIndex = roomIndex, Title = roomTitle };
                        snapNode.Rooms.Add(roomNode);
                    }
                }

                if (providerNode.Errors.Any())
                {
                    _parsedReports.Add(providerNode);

                    foreach (var item in ReportTreeView.Items)
                    {
                        if (ReportTreeView.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem tvi)
                            tvi.IsExpanded = true;
                    }

                    Log($"✅ Найдено {matches.Count} ошибок. Сгруппировано: {providerNode.Errors.Count}. ({cleanProvider})");
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Системная ошибка: {ex.Message}");
            }
        }

        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_parsedReports.Any()) return;

            try
            {
                var currentReport = _parsedReports.First();

                string providerName = currentReport.Title
                    .Replace("Провайдер: ", "")
                    .Replace(" (PASSED)", "")
                    .Replace(" (SKIPPED)", "");

                string allureUrl = JenkinsWebView.Source?.ToString() ?? "URL_NOT_FOUND";
                int totalSnaps = _parsedTotalSnaps;

                if (_lastTestStatus.Equals("Passed", StringComparison.OrdinalIgnoreCase))
                {
                    string cell1 = $"\"Autotest\n0/{totalSnaps} (0%)\"";
                    string exportString = $"{cell1}\t\t{providerName}\t{allureUrl}";
                    Clipboard.SetText(exportString);
                    Log("✅ УСПЕХ: Отчет (Passed) скопирован!");
                    return;
                }

                if (_lastTestStatus.Equals("Skipped", StringComparison.OrdinalIgnoreCase) ||
                    _lastTestStatus.Equals("Broken", StringComparison.OrdinalIgnoreCase))
                {
                    string cell1 = "\"Autotest\n-\"";
                    string safeComment = _lastSkippedMessage.Replace("\"", "\"\"");
                    string cell2 = $"\"{safeComment}\"";

                    string exportString = $"{cell1}\t{cell2}\t{providerName}\t{allureUrl}";
                    Clipboard.SetText(exportString);
                    Log("✅ УСПЕХ: Отчет (Skipped) скопирован!");
                    return;
                }

                // 🚀 СБОР АКТИВНЫХ ОШИБОК ИЗ НОВОЙ ИЕРАРХИИ
                var activeErrors = currentReport.Errors
                    .SelectMany(errGroup => errGroup.Snaps
                        .SelectMany(snap => snap.Rooms
                            .Where(room => room.IsChecked)
                            .Select(room => new { ErrorText = errGroup.Title, SnapId = snap.SnapIndex })
                        )
                    ).ToList();

                // Уникальные упавшие снэпы
                int invalidSnapsCount = activeErrors.Select(x => x.SnapId).Distinct().Count();

                if (invalidSnapsCount == 0)
                {
                    string c1 = $"\"Autotest\n0/{totalSnaps} (0%)\"";
                    string exportStr = $"{c1}\t\t{providerName}\t{allureUrl}";
                    Clipboard.SetText(exportStr);
                    Log("✅ УСПЕХ: Галочки сняты, отчет скопирован как Passed (0 ошибок).");
                    return;
                }

                double failPercentage = ((double)invalidSnapsCount / totalSnaps) * 100;

                // Группировка уникальных ошибок по снэпам
                var errorCounts = activeErrors
                    .Distinct() // Ошибка в пределах одного снэпа считается один раз
                    .GroupBy(x => x.ErrorText)
                    .Select(g => $"{g.Count()} - {g.Key}")
                    .ToList();

                string errorsJoined = string.Join("\n", errorCounts);

                string cell1Failed = $"\"Autotest\n{invalidSnapsCount}/{totalSnaps} ({Math.Round(failPercentage)}%)\"";
                string cell2Failed = $"\"{errorsJoined}\"";

                string exportStringFailed = $"{cell1Failed}\t{cell2Failed}\t{providerName}\t{allureUrl}";

                Clipboard.SetText(exportStringFailed);
                Log("✅ УСПЕХ: Отчет скопирован! (Вставь в таблицу)");
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка копирования: {ex.Message}");
            }
        }
    }
}