using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CsvHelper;
using System.Globalization;
using DQEHelper.Services;
using DQEHelper.Models;

namespace DQEHelper.Views.Csv
{
    public partial class CsvProcessorView : UserControl
    {
        private string? _selectedFilePath;

        public CsvProcessorView()
        {
            InitializeComponent();
        }

        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    _selectedFilePath = files[0];
                    // Меняем текст, чтобы пользователь видел, какой файл загружен
                    DropZoneText.Text = $"Выбран архив:\n{Path.GetFileName(_selectedFilePath)}";
                }
            }
        }

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath) || !File.Exists(_selectedFilePath))
            {
                MessageBox.Show("Пожалуйста, сначала перетащите файл архива в зону загрузки.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string rawInput = ProviderInputTextBox.Text;
            string[] patterns = rawInput
                .Split(',')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            if (patterns.Length == 0)
            {
                MessageBox.Show("Введите хотя бы одного провайдера для фильтрации.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Создаем выходной файл в той же папке, где лежит исходник
            string directory = Path.GetDirectoryName(_selectedFilePath) ?? string.Empty;
            string outputCsv = Path.Combine(directory, "output_filtered.csv");

            var processor = new CsvProcessingService();
            GenerateButton.IsEnabled = false;

            try
            {
                // Запускаем асинхронную обработку и получаем отчет
                string resultSummary = await processor.ProcessCsvAsync(_selectedFilePath, outputCsv, patterns);

                // Показываем пользователю результаты выборки
                MessageBox.Show(resultSummary, "Генерация завершена", MessageBoxButton.OK, MessageBoxImage.Information);

                // Загружаем предпросмотр результата в DataGrid
                LoadPreview(outputCsv);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обработки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadPreview(string filePath)
        {
            // Читаем только первые 100 записей, чтобы не перегружать UI
            using var reader = new StreamReader(filePath);
            using var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);
            csvReader.Context.RegisterClassMap<DqeCsvRecordMap>();

            var records = csvReader.GetRecords<DqeCsvRecord>().Take(100).ToList();
            ResultsGrid.ItemsSource = records;
        }

    }
}