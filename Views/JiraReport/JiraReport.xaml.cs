using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        // 🚀 ИСПРАВЛЕНИЕ 1: Теперь мы храним первый столбец как чистую строку (Flag), а не как true/false
        private record ShopRecord(ReportSection Section, string Provider, string Flag, string Comment);

        public JiraReport()
        {
            InitializeComponent();
        }

        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV Файлы (*.csv)|*.csv|Все файлы (*.*)|*.*",
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
            sb.AppendLine("If you have any questions, please let me know <Me> FYI <Sergiy Gulko>");

            ReportOutputTextBox.Text = sb.ToString();
        }

        private List<ShopRecord> ParseCsvData(string filePath)
        {
            var results = new List<ShopRecord>();
            var lines = File.ReadAllLines(filePath);

            ReportSection currentSection = ReportSection.Coralogix;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.Contains("1/0comments_provider", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("deals.provider", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = ReportSection.Daily;
                    continue;
                }

                if (line.Contains("Автотест", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("Autotest", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = ReportSection.Autotest;
                    continue;
                }

                var columns = line.Split(new[] { ',', ';' });

                if (columns.Length >= 3)
                {
                    // 🚀 ГЛАВНЫЙ ФИКС: Очищаем данные от случайных пробелов и скрытых кавычек CSV
                    string flag = columns[0].Trim().Trim('"');
                    string comment = columns[1].Trim().Trim('"');
                    string provider = columns[2].Trim().Trim('"');

                    // Пропускаем шапки таблиц или одинокое слово "Autotest" (первая половина ячейки)
                    if (flag == "1/0" || string.IsNullOrEmpty(provider) || flag.StartsWith("Autotest", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Сохраняем "как есть" - тут будет "0", "1", "33/38 (92%)" или "-"
                    bool isError = flag == "0" || !string.IsNullOrEmpty(comment);

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
                    // 🚀 ЛОГИКА АВТОТЕСТОВ: Просто копируем из файла, никакой математики!
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

                            // Если флаг - это прочерк (как у hyatt), выводим просто текст ошибки
                            if (flag == "-" || string.IsNullOrWhiteSpace(flag))
                            {
                                sb.AppendLine($"* **Agoda DTI | {provider}**: {error.Comment}");
                            }
                            else
                            {
                                // Подставляем готовую метрику из файла (например, 33/38 (92%))
                                sb.AppendLine($"* **Agoda DTI | {provider}**: Scale: {flag}: {error.Comment};");
                            }
                        }
                    }
                }
                else
                {
                    // 🚀 ЛОГИКА РУЧНЫХ ПРОВЕРОК: Агрегируем строки и считаем %
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

                        var groupedComments = errors
                            .Where(e => !string.IsNullOrEmpty(e.Comment))
                            .GroupBy(e => e.Comment)
                            .Select(g => $"{g.Count()} - {g.Key}");

                        string commentsString = string.Join("; ", groupedComments);

                        sb.AppendLine($"* **Agoda DTI | {provider}**: Scale: {errorCount}/{totalChecks} ({percent}%): {commentsString};");
                    }
                }
            }

            if (spotlessProviders.Any())
            {
                string combinedSpotless = string.Join(", ", spotlessProviders);
                sb.AppendLine($"* **Agoda DTI | {combinedSpotless}**: No issues were found;");
            }

            sb.AppendLine();
        }

        private void SaveMarkdownButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ReportOutputTextBox.Text))
            {
                MessageBox.Show("Сначала сгенерируйте отчет, выбрав CSV файл.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
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