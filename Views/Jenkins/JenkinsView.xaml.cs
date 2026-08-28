using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DQEHelper.Services;

namespace DQEHelper.Views.Jenkins
{
    // Модель для чекбоксов
    public class ProviderItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Name { get; init; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public partial class JenkinsView : UserControl
    {
        private readonly ObservableCollection<ProviderItem> _providers = new();

        // URL твоего Jenkins сервера
        private const string JenkinsBaseUrl = "https://ci-gcp.fornova.net";
        private const string JobName = "Temerix--Data-Checking-Snapshot-for-Agoda-test";

        public JenkinsView()
        {
            InitializeComponent();
            LoadProviders();
            ProvidersListBox.ItemsSource = _providers;

            // Подписываемся на изменения, чтобы обновлять счетчик на кнопке
            foreach (var provider in _providers)
            {
                provider.PropertyChanged += (s, e) => UpdateRunButtonText();
            }
        }

        private void LoadProviders()
        {
            // Список собран из твоих логов
            string[] providerNames = {
                "jalan", "booking", "tripadvisor", "hyatt", "hilton", "google", "traveloka",
                "traveloka_mob", "klook", "kayak", "ascott", "rakuten_us", "choice", "makemytrip",
                "makemytrip_mob", "marriott", "hotelscombined", "sonesta", "eztravel", "ikyu",
                "radisson", "rakuten_jp", "wotif_app", "accor", "wotif", "makemytrip_mob_сug"
            };

            foreach (var name in providerNames.OrderBy(n => n))
            {
                _providers.Add(new ProviderItem { Name = name, IsSelected = false });
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var p in _providers) p.IsSelected = true;
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var p in _providers) p.IsSelected = false;
        }

        private void UpdateRunButtonText()
        {
            int selectedCount = _providers.Count(p => p.IsSelected);
            RunJobsButton.Content = $"ЗАПУСТИТЬ ВЫБРАННЫЕ ({selectedCount})";
            RunJobsButton.IsEnabled = selectedCount > 0;
        }

        private void Log(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            ConsoleLogTextBox.AppendText($"[{time}] {message}\n");
            ConsoleLogTextBox.ScrollToEnd();
        }

        private async void RunJobsButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedProviders = _providers.Where(p => p.IsSelected).Select(p => p.Name).ToList();
            if (!selectedProviders.Any()) return;

            string destination = ((ComboBoxItem)DestinationCombo.SelectedItem).Content.ToString() ?? "prod-il";
            string typeReport = ((ComboBoxItem)TypeReportCombo.SelectedItem).Content.ToString() ?? "Main";

            // 🚀 ИСПОЛЬЗУЕМ ДАННЫЕ ИЗ APPCONFIG
            var apiService = new JenkinsApiService(JenkinsBaseUrl, AppConfig.JenkinsCookie, AppConfig.JenkinsCrumb);

            RunJobsButton.IsEnabled = false;
            Log("=== НАЧАЛО МАССОВОГО ЗАПУСКА ===");

            // Отправляем запросы последовательно
            foreach (var provider in selectedProviders)
            {
                var parameters = new Dictionary<string, string>
                {
                    { "PROVIDER", provider },
                    { "destination", destination },
                    { "TYPE_REPORT", typeReport }
                };

                Log($"⏳ Отправка: {provider}...");

                // Деконструируем кортеж (Pattern Matching C#)
                var (isSuccess, errorMessage) = await apiService.TriggerJobAsync(JobName, parameters);

                if (isSuccess)
                {
                    Log($"✅ Успех: {provider} добавлен в очередь сборки.");
                }
                else
                {
                    Log($"❌ Ошибка: Не удалось запустить {provider}.");
                    // Выводим сырую ошибку от Jenkins прямо в нашу UI консоль
                    Log($"   👉 Детали: {errorMessage}");
                }

                await Task.Delay(500);
            }

            Log("=== ЗАПУСК ЗАВЕРШЕН ===");
            UpdateRunButtonText(); // Восстанавливает состояние кнопки
        }
    }
}