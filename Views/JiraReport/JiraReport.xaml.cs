using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace DQEHelper.Views
{
    public partial class JiraReport : UserControl
    {
        private enum ReportSection
        {
            Coralogix,
            Daily,
            Autotest
        }

        private record ShopRecord(ReportSection Section, string Provider, string Flag, string Comment);

        public JiraReport()
        {
            InitializeComponent();

            // Автозаполнение даты за сегодняшний день и номера партии (можно редактировать в UI)
            StreamDateTextBox.Text = DateTime.Now.ToString("yyyy_MM_dd");
            PartNumberTextBox.Text = "1";
        }

        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV/TSV Файлы (*.csv;*.txt)|*.csv;*.txt|Все файлы (*.*)|*.*",
                Title = "Выберите выгрузку (Example - Sheet1.csv)"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    GenerateReport(openFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при обработке файла:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void GenerateReport(string filePath)
        {
            List<ShopRecord> records = ParseCsvData(filePath);

            // Идеальный подсчет: Ручные проверки = все строки в секциях Coralogix и Daily
            int calculatedManualCount = records.Count(r => r.Section == ReportSection.Coralogix || r.Section == ReportSection.Daily);

            // Подсчет автотестов: Извлекаем знаменатель (число после знака /) из каждой записи
            int calculatedAutoCount = records
                .Where(r => r.Section == ReportSection.Autotest)
                .Select(r => Regex.Match(r.Flag, @"\d+/(\d+)"))
                .Where(m => m.Success)
                .Sum(m => int.Parse(m.Groups[1].Value));

            // Записываем посчитанные значения в UI, чтобы их можно было увидеть/отредактировать
            ManualCountTextBox.Text = calculatedManualCount.ToString();
            AutotestCountTextBox.Text = calculatedAutoCount.ToString();

            var sb = new StringBuilder();

            string streamDate = StreamDateTextBox.Text.Trim();
            string partNum = PartNumberTextBox.Text.Trim();
            string manualCount = ManualCountTextBox.Text.Trim();
            string autoCount = AutotestCountTextBox.Text.Trim();

            sb.AppendLine($"During checking **«Agoda DTI | Agoda_streamed_{streamDate}_part_{partNum}»** we checked {manualCount} records manually and {autoCount} records with autotest and found the following issues:");
            sb.AppendLine();

            AppendSection(sb, "Coralogix part", records.Where(r => r.Section == ReportSection.Coralogix), isAutotest: false);
            AppendSection(sb, "Daily report part", records.Where(r => r.Section == ReportSection.Daily), isAutotest: false);
            AppendSection(sb, "Autotest part", records.Where(r => r.Section == ReportSection.Autotest), isAutotest: true);

            sb.AppendLine($"**Link to the document:** {SpreadsheetLinkTextBox.Text.Trim()}");
            sb.AppendLine();
            sb.AppendLine("If you have any questions, please let me know <Me> \nFYI <Sergiy Gulko>");

            ReportOutputTextBox.Text = sb.ToString();
        }

        // Честный CSV парсер для поддержки многострочных комментариев внутри ячеек
        private List<string[]> ReadCsvRobust(string filePath)
        {
            var results = new List<string[]>();
            string fileContent = File.ReadAllText(filePath);
            var currentRecord = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < fileContent.Length; i++)
            {
                char c = fileContent[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < fileContent.Length && fileContent[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if ((c == ',' || c == ';' || c == '\t') && !inQuotes)
                {
                    currentRecord.Add(currentField.ToString());
                    currentField.Clear();
                }
                else if (c == '\n' && !inQuotes)
                {
                    if (currentField.Length > 0 && currentField[currentField.Length - 1] == '\r')
                        currentField.Length--;

                    currentRecord.Add(currentField.ToString());
                    results.Add(currentRecord.ToArray());
                    currentRecord = new List<string>();
                    currentField.Clear();
                }
                else if (c != '\r' || inQuotes)
                {
                    currentField.Append(c);
                }
            }

            if (currentField.Length > 0 || currentRecord.Count > 0)
            {
                if (currentField.Length > 0 && currentField[currentField.Length - 1] == '\r')
                    currentField.Length--;
                currentRecord.Add(currentField.ToString());
                results.Add(currentRecord.ToArray());
            }

            return results;
        }

        private List<ShopRecord> ParseCsvData(string filePath)
        {
            var results = new List<ShopRecord>();
            var rows = ReadCsvRobust(filePath);

            // По умолчанию начинаем с Coralogix
            ReportSection currentSection = ReportSection.Coralogix;

            foreach (var columns in rows)
            {
                if (columns.Length == 0 || (columns.Length == 1 && string.IsNullOrWhiteSpace(columns[0])))
                    continue;

                string rawFirstCol = columns[0].Trim();
                string rawThirdCol = columns.Length >= 3 ? columns[2].Trim() : "";

                // Разделитель: Переход в Daily
                if (rawFirstCol.Contains("1/0comments_provider", StringComparison.OrdinalIgnoreCase) ||
                    rawThirdCol.Contains("1/0comments_provider", StringComparison.OrdinalIgnoreCase) ||
                    rawFirstCol.Contains("deals.provider", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = ReportSection.Daily;
                    continue;
                }

                // Разделитель: Переход в Автотесты
                if (rawFirstCol.Equals("Автотест", StringComparison.OrdinalIgnoreCase) ||
                    rawFirstCol.Equals("Autotest", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = ReportSection.Autotest;
                    continue;
                }

                if (columns.Length >= 3)
                {
                    string flag = rawFirstCol;
                    string comment = columns[1].Trim();
                    string provider = columns[2].Trim();

                    // Пропускаем шапки таблиц
                    if (flag == "1/0" || string.IsNullOrEmpty(provider))
                        continue;

                    // Обрабатываем ячейки Автотеста, если слово Autotest "прилипло" к значению
                    if (flag.StartsWith("Autotest\n", StringComparison.OrdinalIgnoreCase))
                    {
                        currentSection = ReportSection.Autotest;
                        flag = flag.Substring("Autotest\n".Length).Trim();
                    }

                    results.Add(new ShopRecord(currentSection, provider, flag, comment));
                }
            }

            return results;
        }

        private void AppendSection(StringBuilder sb, string sectionName, IEnumerable<ShopRecord> records, bool isAutotest)
        {
            var recordList = records.ToList();
            if (!recordList.Any()) return;

            sb.AppendLine($"**{sectionName}**");

            var providerGroups = recordList.GroupBy(r => r.Provider).ToList();
            var spotlessProviders = new List<string>();

            foreach (var group in providerGroups)
            {
                string provider = group.Key;
                var recordsInGroup = group.ToList();

                if (isAutotest)
                {
                    var errors = recordsInGroup.Where(r => !string.IsNullOrWhiteSpace(r.Comment)).ToList();

                    if (!errors.Any())
                    {
                        spotlessProviders.Add(provider);
                    }
                    else
                    {
                        foreach (var error in errors)
                        {
                            string flag = error.Flag;
                            sb.AppendLine($"* **Agoda DTI | {provider}**:");

                            if (flag == "-" || string.IsNullOrWhiteSpace(flag))
                            {
                                AppendMultilineComment(sb, error.Comment);
                            }
                            else
                            {
                                sb.AppendLine($"  Scale: {flag}");
                                AppendMultilineComment(sb, error.Comment);
                            }
                        }
                    }
                }
                else
                {
                    int totalChecks = recordsInGroup.Count();
                    var errors = recordsInGroup.Where(r => r.Flag == "0" || !string.IsNullOrWhiteSpace(r.Comment)).ToList();
                    int errorCount = errors.Count;

                    if (errorCount == 0)
                    {
                        spotlessProviders.Add(provider);
                    }
                    else
                    {
                        int percent = (int)Math.Round((errorCount * 100.0) / totalChecks);

                        sb.AppendLine($"* **Agoda DTI | {provider}**:");
                        sb.AppendLine($"  Scale: {errorCount}/{totalChecks} ({percent}%)");

                        var groupedComments = errors
                            .Where(e => !string.IsNullOrEmpty(e.Comment))
                            .GroupBy(e => e.Comment)
                            .Select(g => $"{g.Count()} - {g.Key}");

                        foreach (var comment in groupedComments)
                        {
                            sb.AppendLine($"  {comment}");
                        }
                    }
                }
            }

            if (spotlessProviders.Any())
            {
                string combinedSpotless = string.Join(", ", spotlessProviders);
                sb.AppendLine($"* **Agoda DTI | {combinedSpotless}**: No issues were found");
            }

            sb.AppendLine();
        }

        private void AppendMultilineComment(StringBuilder sb, string multilineComment)
        {
            var lines = multilineComment.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                sb.AppendLine($"  {line.Trim()}");
            }
        }

        private void SaveMarkdownButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ReportOutputTextBox.Text))
            {
                MessageBox.Show("Сначала сгенерируйте отчет, выбрав файл.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string date = StreamDateTextBox.Text.Trim();
            string part = PartNumberTextBox.Text.Trim();

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Markdown Файл (*.md)|*.md",
                Title = "Сохранить отчет",
                FileName = $"Jira_Report_Agoda_{date}_part_{part}.md"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(saveFileDialog.FileName, ReportOutputTextBox.Text, Encoding.UTF8);
                    MessageBox.Show("Отчет успешно сохранен в формате Markdown!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}