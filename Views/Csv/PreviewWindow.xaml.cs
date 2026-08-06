using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CsvHelper;
using DQEHelper.Models;

namespace DQEHelper.Views.Csv
{
    public partial class PreviewWindow : Window
    {
        public PreviewWindow(string csvPath)
        {
            InitializeComponent();
            LoadData(csvPath);
        }

        private void LoadData(string path)
        {
            using var reader = new StreamReader(path);
            using var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);
            csvReader.Context.RegisterClassMap<DqeCsvRecordMap>();
            
            // Читаем всё (файл уже маленький)
            var records = csvReader.GetRecords<DqeCsvRecord>().ToList();
            PreviewGrid.ItemsSource = records;
        }

        // Прагматичный подход: укорачиваем длинные строки на лету
        private void PreviewGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.PropertyName == "Url" || e.PropertyName == "Screengrab")
            {
                var style = new Style(typeof(TextBlock));
                // Обрезаем текст многоточием
                style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
                // Показываем полный URL при наведении
                style.Setters.Add(new Setter(TextBlock.ToolTipProperty, new Binding(e.PropertyName)));

                if (e.Column is DataGridTextColumn textColumn)
                {
                    textColumn.ElementStyle = style;
                    textColumn.MaxWidth = 250; // Визуально ограничиваем ширину колонки
                }
            }
        }
    }
}