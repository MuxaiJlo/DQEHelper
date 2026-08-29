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

        // 🚀 НОВОЕ: Скрытая переменная для хранения общего количества снэпов
        private int _parsedTotalSnaps = 10; // 10 как fallback по умолчанию

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
                            TotalSnaps: maxSnap 
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

                // 🚀 Сохраняем спарсенное значение в память C# (никакого UI TextBox)
                if (doc.RootElement.TryGetProperty("TotalSnaps", out var totalSnapsElement))
                {
                    int parsedTotal = totalSnapsElement.GetInt32();
                    if (parsedTotal > 0)
                    {
                        _parsedTotalSnaps = parsedTotal;
                        Log($"🔢 Авто-определение: найдено {_parsedTotalSnaps} снэпов.");
                    }
                }

                if (string.IsNullOrWhiteSpace(rawErrorText))
                {
                    Log("✅ Ошибок в отчете не найдено!");
                    return;
                }

                string cleanProvider = Regex.Replace(rawProvider, @"^.*check(.*)Results.*$", "$1", RegexOptions.IgnoreCase).Trim();
                if (string.IsNullOrEmpty(cleanProvider)) cleanProvider = rawProvider;

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

                    var snapNode = providerNode.Snaps.FirstOrDefault(s => s.SnapIndex == snapId);
                    if (snapNode == null)
                    {
                        snapNode = new SnapNode { SnapIndex = snapId, Title = $"Snap Index: {snapId}" };
                        providerNode.Snaps.Add(snapNode);
                    }

                    var roomNode = snapNode.Rooms.FirstOrDefault(r => r.RoomIndex == roomIndex);
                    if (roomNode == null)
                    {
                        string roomTitle = roomIndex == 0 ? "General Errors" : $"Room Index: {roomIndex}";
                        roomNode = new RoomNode { RoomIndex = roomIndex, Title = roomTitle };
                        snapNode.Rooms.Add(roomNode);
                    }

                    roomNode.Errors.Add(new ErrorNode { Title = cleanError });
                }

                if (providerNode.Snaps.Any())
                {
                    _parsedReports.Add(providerNode);

                    foreach (var item in ReportTreeView.Items)
                    {
                        if (ReportTreeView.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem tvi)
                            tvi.IsExpanded = true;
                    }

                    Log($"✅ Найдено {matches.Count} ошибок. ({cleanProvider})");
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
                string providerName = currentReport.Title.Replace("Провайдер: ", "");
                string allureUrl = JenkinsWebView.Source?.ToString() ?? "URL_NOT_FOUND";

                // 🚀 Берем количество прямо из памяти C#
                int totalSnaps = _parsedTotalSnaps;

                var invalidSnaps = currentReport.Snaps
                    .Where(s => s.Rooms.Any(r => r.Errors.Any(err => err.IsChecked)))
                    .ToList();

                int invalidSnapsCount = invalidSnaps.Count;
                if (invalidSnapsCount == 0)
                {
                    Log("⚠️ Нет выбранных галочками ошибок.");
                    return;
                }

                double failPercentage = ((double)invalidSnapsCount / totalSnaps) * 100;

                var errorCounts = invalidSnaps
                    .SelectMany(s => s.Rooms
                        .SelectMany(r => r.Errors)
                        .Where(err => err.IsChecked)
                        .Select(err => new { SnapId = s.SnapIndex, ErrorText = err.Title })
                    )
                    .Distinct()
                    .GroupBy(x => x.ErrorText)
                    .Select(g => $"{g.Count()} - {g.Key}")
                    .ToList();

                string errorsJoined = string.Join("\n", errorCounts);

                string cell1 = $"\"Autotest\n{invalidSnapsCount}/{totalSnaps} ({Math.Round(failPercentage)}%)\"";
                string cell2 = $"\"{errorsJoined}\"";
                string cell3 = providerName;
                string cell4 = allureUrl;

                string exportString = $"{cell1}\t{cell2}\t{cell3}\t{cell4}";

                Clipboard.SetText(exportString);
                Log("✅ УСПЕХ: Отчет скопирован! (Вставь в таблицу)");
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка копирования: {ex.Message}");
            }
        }
    }
}