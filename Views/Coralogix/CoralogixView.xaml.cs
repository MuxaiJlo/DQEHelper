using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DQEHelper.Services;

namespace DQEHelper.Views.Coralogix
{
    public partial class CoralogixView : UserControl
    {
        // Инициализируем сервис, забирая ключ из нашего безопасного конфига
        private readonly CoralogixApiService _apiService;

        public CoralogixView()
        {
            InitializeComponent();
            // Подкидываем куки из конфига
            _apiService = new CoralogixApiService(AppConfig.CoralogixCookie);
        }

        // ==========================================
        // ЭТАП 1: ПОИСК (Левая панель)
        // ==========================================
        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchButton.IsEnabled = false;
            SearchButton.Content = "Поиск...";
            SearchResultsList.ItemsSource = null; // Очищаем старые результаты

            try
            {
                // Собираем значения из UI
                string customer = CustomerTextBox.Text.Trim();
                string provider = ProviderTextBox.Text.Trim();

                // Для ComboBox берем текст выбранного элемента
                string scanMethod = ((ComboBoxItem)ScanMethodCombo.SelectedItem).Content.ToString() ?? "Any";
                string accessLevel = ((ComboBoxItem)AccessLevelCombo.SelectedItem).Content.ToString() ?? "Any";
                string stage = ((ComboBoxItem)StageCombo.SelectedItem).Content.ToString() ?? "Any";
                string availability = ((ComboBoxItem)AvailabilityCombo.SelectedItem).Content.ToString() ?? "Any";

                // Парсим лимит (с защитой от ввода букв)
                if (!int.TryParse(LimitTextBox.Text, out int limit))
                {
                    limit = 10; // Значение по умолчанию
                }

                // 🚀 Запускаем поиск (уводим в фоновый поток, чтобы не фризить UI)
                var results = await Task.Run(() => _apiService.SearchLogsAsync(
                    customer, provider, scanMethod, accessLevel, stage, availability, limit));

                // Привязываем полученный список к ListView
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
        private void SearchResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchResultsList.SelectedItem == null)
            {
                AddToCartButton.IsEnabled = false;
                AddToCartButton.Content = "Добавить выбранные (0)";
                JsonPreviewTextBox.Text = string.Empty;
                PreviewStatusText.Text = "Выберите скан из списка слева...";
                return;
            }

            // Активируем кнопку добавления для мульти-выбора (если включим его в будущем)
            AddToCartButton.IsEnabled = true;
            AddToCartButton.Content = $"Добавить выбранные ({SearchResultsList.SelectedItems.Count})";

            // Пока просто выводим заглушку в превью (настоящий запрос к GCP API сделаем следующим шагом)
            PreviewStatusText.Text = "Загрузка JSON...";
            JsonPreviewTextBox.Text = "{\n  \"status\": \"API запрос к historical-data-api-prod будет здесь...\"\n}";
        }

        // ==========================================
        // ЭТАП 3: КОРЗИНА И ОТЧЕТ (Правая панель)
        // ==========================================
        private void AddToCartButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Логика скачивания и добавления в корзину будет добавлена на следующем этапе.", "В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Логика экспорта в CSV будет добавлена на следующем этапе.", "В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}