using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DQEHelper.Services;

namespace DQEHelper.Views.Csv
{
    public partial class CsvProcessorView : UserControl
    {
        private string? _selectedFilePath;
        private string? _lastOutputCsvPath; // Сохраняем путь для предпросмотра

        public CsvProcessorView()
        {
            InitializeComponent();
        }

        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    _selectedFilePath = files[0];
                    DropZoneText.Text = $"Загружен архив:\n{Path.GetFileName(_selectedFilePath)}";
                    DropZoneIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")); // Зеленая иконка
                }
            }
        }

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath) || !File.Exists(_selectedFilePath))
            {
                MessageBox.Show("Сначала перетащите файл.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string[] patterns = ProviderInputTextBox.Text.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToArray();
            if (patterns.Length == 0) return;

            string directory = Path.GetDirectoryName(_selectedFilePath) ?? string.Empty;
            _lastOutputCsvPath = Path.Combine(directory, "output_filtered.csv");

            var processor = new CsvProcessingService();
            GenerateButton.IsEnabled = false;

            // Прячем старые результаты на время генерации
            ReportItemsControl.ItemsSource = null;
            ProcessingStatusText.Text = "Обработка файла... Пожалуйста, подождите.";

            try
            {
                // 🚀 ИСПРАВЛЕНИЕ: Отводим тяжелую работу в фоновый пул потоков.
                // UI-поток остается свободным, интерфейс не зависает!
                var results = await Task.Run(() => processor.ProcessCsvAsync(_selectedFilePath, _lastOutputCsvPath, patterns));

                // Сюда мы возвращаемся уже в UI-потоке с готовыми результатами
                ProcessingStatusText.Text = "Генерация завершена! Результаты выборки:";
                ReportItemsControl.ItemsSource = results; // WPF мгновенно отрисует карточки

                PreviewButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ProcessingStatusText.Text = $"Ошибка: {ex.Message}";
            }
            finally
            {
                GenerateButton.IsEnabled = true;
            }
        }

        // --- НОВЫЙ ФУНКЦИОНАЛ ---

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedFilePath = null;
            _lastOutputCsvPath = null;
            ProviderInputTextBox.Text = string.Empty;
            DataTemplateSelector selector = ReportItemsControl.ItemTemplateSelector;
            ReportItemsControl.ItemsSource = null; // Очищаем старые результаты
            PreviewButton.IsEnabled = false;
            ProcessingStatusText.Text = string.Empty;
            DropZoneText.Text = "Перетащи сюда архив поставщиков (.csv)";
            DropZoneIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10288C"));
        }

        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_lastOutputCsvPath) || !File.Exists(_lastOutputCsvPath)) return;

            // Открываем новое окно предпросмотра
            var previewWindow = new PreviewWindow(_lastOutputCsvPath);
            previewWindow.Show();
        }
    }
}