using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CsvHelper;
using Microsoft.Win32;
using DQEHelper.Models;
using DQEHelper.Services;

namespace DQEHelper.Views.Coralogix
{
    public partial class CoralogixView : UserControl
    {
        private readonly CoralogixApiService _apiService;

        // Коллекция для хранения собранных сканов (автоматически обновляет UI)
        private readonly ObservableCollection<ScanReportItem> _reportCart = new();

        public CoralogixView()
        {
            InitializeComponent();
            _apiService = new CoralogixApiService(AppConfig.CoralogixCookie);

            // Привязываем корзину к ListBox в правой панели
            CartListBox.ItemsSource = _reportCart;
        }

        // ==========================================
        // ЭТАП 1: ПОИСК (Левая панель)
        // ==========================================
        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchButton.IsEnabled = false;
            SearchButton.Content = "Поиск...";
            SearchResultsList.ItemsSource = null;

            try
            {
                string customer = CustomerTextBox.Text.Trim();
                string provider = ProviderTextBox.Text.Trim();
                string scanMethod = ((ComboBoxItem)ScanMethodCombo.SelectedItem).Content.ToString() ?? "Any";
                string accessLevel = ((ComboBoxItem)AccessLevelCombo.SelectedItem).Content.ToString() ?? "Any";
                string stage = ((ComboBoxItem)StageCombo.SelectedItem).Content.ToString() ?? "Any";
                string availability = ((ComboBoxItem)AvailabilityCombo.SelectedItem).Content.ToString() ?? "Any";

                if (!int.TryParse(LimitTextBox.Text, out int limit)) limit = 10;

                var results = await Task.Run(() => _apiService.SearchLogsAsync(
                    customer, provider, scanMethod, accessLevel, stage, availability, limit));

                SearchResultsList.ItemsSource = results;

                if (results.Count == 0)
                {
                    MessageBox.Show("По вашему запросу ничего не найдено.", "Результат", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка поиска:\n{ex.Message}", "Ошибка API", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SearchButton.IsEnabled = true;
                SearchButton.Content = "Найти в Coralogix";
            }
        }

        // ==========================================
        // ЭТАП 2: ПРЕДПРОСМОТР (Центральная панель)
        // ==========================================
        private async void SearchResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchResultsList.SelectedItem is not CoralogixLogEntry selectedEntry)
            {
                AddToCartButton.IsEnabled = false;
                AddToCartButton.Content = "Добавить выбранные (0)";
                JsonPreviewTextBox.Text = string.Empty;
                PreviewStatusText.Text = "Выберите скан из списка слева...";
                return;
            }

            AddToCartButton.IsEnabled = true;
            AddToCartButton.Content = $"Добавить выбранные ({SearchResultsList.SelectedItems.Count})";
            PreviewStatusText.Text = "Загрузка JSON из GCP API...";
            JsonPreviewTextBox.Text = "Загрузка...";

            try
            {
                // Подтягиваем сырой JSON через VPN
                string rawJson = await _apiService.GetRawScanDataAsync(selectedEntry.ProviderScanId, selectedEntry.TaskId);
                JsonPreviewTextBox.Text = rawJson;
                PreviewStatusText.Text = $"Успешно загружен скан: {selectedEntry.ProviderScanId}";
            }
            catch (Exception ex)
            {
                JsonPreviewTextBox.Text = $"Ошибка загрузки данных из GCP:\n{ex.Message}";
                PreviewStatusText.Text = "Ошибка загрузки";
            }
        }

        // ==========================================
        // ЭТАП 3: КОРЗИНА И ОТЧЕТ (Правая панель)
        // ==========================================
        private async void AddToCartButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = SearchResultsList.SelectedItems.Cast<CoralogixLogEntry>().ToList();
            if (!selectedItems.Any()) return;

            AddToCartButton.IsEnabled = false;
            AddToCartButton.Content = "Добавление...";

            try
            {
                // 1. Читаем текущие фильтры из UI
                string provider = ProviderTextBox.Text.Trim();
                string rawScanMethod = ((ComboBoxItem)ScanMethodCombo.SelectedItem).Content.ToString() ?? "";
                string accessLevel = ((ComboBoxItem)AccessLevelCombo.SelectedItem).Content.ToString()?.ToLower() ?? "any";

                // 2. Сокращаем названия для красивого отчета (Pattern Matching C# 8+)
                string scanMethodShort = rawScanMethod switch
                {
                    "mobile_app" => "mob",
                    "mobile_web" => "mweb",
                    "web" => "web",
                    _ => "any"
                };

                // 3. Собираем итоговое имя (например: traveloka_mob_cug)
                var providerParts = new List<string> { provider };
                if (scanMethodShort != "any") providerParts.Add(scanMethodShort);
                if (accessLevel != "any") providerParts.Add(accessLevel);
                
                string customProviderName = string.Join("_", providerParts);

                // 4. Запускаем параллельное скачивание, передавая собранное имя
                var downloadTasks = selectedItems.Select(entry => ProcessAndCreateReportItemAsync(entry, customProviderName));
                var reportItems = await Task.WhenAll(downloadTasks);

                foreach (var item in reportItems)
                {
                    if (item != null)
                    {
                        _reportCart.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении в корзину: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                AddToCartButton.IsEnabled = true;
                AddToCartButton.Content = $"Добавить выбранные ({SearchResultsList.SelectedItems.Count})";
            }
        }

        // Асинхронный процессор одного скана (добавлен параметр customProviderName)
        private async Task<ScanReportItem?> ProcessAndCreateReportItemAsync(CoralogixLogEntry entry, string customProviderName)
        {
            try
            {
                string rawJson = await _apiService.GetRawScanDataAsync(entry.ProviderScanId, entry.TaskId);
                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                string hdsUrl = $"http://historical-data-api-prod.prod.gcphosts.net:5000/data/hot/{entry.ProviderScanId}?id={entry.TaskId}";

                // Сжимаем JSON в одну строку для идеального вида в Excel
                string minifiedJson = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = false });

                return new ScanReportItem
                {
                    ProviderScanId = entry.ProviderScanId,
                    TaskId = entry.TaskId,
                    HdsUrl = hdsUrl,
                    ExtData = minifiedJson, 
                    
                    SnapshotUrl = FindJsonValue(root, "snapshot_url") ?? "",
                    DeepLink = FindJsonValue(root, "deep_link") ?? "",
                    Pos = FindJsonValue(root, "pos") ?? "",
                    
                    // Используем наше сгенерированное составное имя!
                    CustomProvider = customProviderName 
                };
            }
            catch
            {
                return null; 
            }
        }

        // Улучшенный рекурсивный поиск ключа (с поддержкой массивов)
        private string? FindJsonValue(JsonElement element, string targetKey)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                // Если нашли нужное поле
                if (element.TryGetProperty(targetKey, out var match))
                {
                    // 🚀 ПРОВЕРКА НА МАССИВ: Если это массив, берем первый элемент
                    if (match.ValueKind == JsonValueKind.Array && match.GetArrayLength() > 0)
                    {
                        return match[0].ToString(); 
                    }
                    
                    // Иначе возвращаем как есть (строку, число и т.д.)
                    return match.ToString();
                }

                // Идем глубже по объекту
                foreach (var prop in element.EnumerateObject())
                {
                    var found = FindJsonValue(prop.Value, targetKey);
                    if (found != null) return found;
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                // Идем по элементам массива
                foreach (var item in element.EnumerateArray())
                {
                    var found = FindJsonValue(item, targetKey);
                    if (found != null) return found;
                }
            }
            return null;
        }

        // ==========================================
        // ЭТАП 4: ЭКСПОРТ В CSV
        // ==========================================
        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_reportCart.Any())
            {
                MessageBox.Show("Корзина пуста. Сначала добавьте сканы.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV Файл (*.csv)|*.csv",
                Title = "Сохранить отчет",
                FileName = $"QA_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using var writer = new StreamWriter(saveFileDialog.FileName);
                    using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

                    // Регистрируем наш маппинг колонок
                    csv.Context.RegisterClassMap<ScanReportItemMap>();

                    // Записываем всю коллекцию в файл
                    csv.WriteRecords(_reportCart);

                    MessageBox.Show("Отчет успешно сохранен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Опционально: очистить корзину после выгрузки
                    // _reportCart.Clear(); 
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}