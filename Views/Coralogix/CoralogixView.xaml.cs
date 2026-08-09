using System.Windows;
using System.Windows.Controls;

namespace DQEHelper.Views.Coralogix
{
    public partial class CoralogixView : UserControl
    {
        public CoralogixView()
        {
            InitializeComponent();
        }

        // Обработчик кнопки поиска
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchButton.IsEnabled = false;
            // TODO: Сбор данных из фильтров
            // TODO: Вызов CoralogixApiService

            MessageBox.Show("Здесь будет HTTP-запрос к API Coralogix.", "Поиск", MessageBoxButton.OK, MessageBoxImage.Information);

            SearchButton.IsEnabled = true;
        }

        // Обработчик клика по результату поиска (для предпросмотра)
        private void SearchResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchResultsList.SelectedItem == null)
            {
                AddToCartButton.IsEnabled = false;
                AddToCartButton.Content = "Добавить выбранные (0)";
                return;
            }

            // Активируем кнопку добавления
            AddToCartButton.IsEnabled = true;
            AddToCartButton.Content = $"Добавить выбранные ({SearchResultsList.SelectedItems.Count})";

            // TODO: Сделать запрос к historical-data-api-prod для выбранного элемента
            PreviewStatusText.Text = "Загрузка JSON...";
            JsonPreviewTextBox.Text = "{\n  \"status\": \"Ожидание интеграции API...\"\n}";
        }

        // Обработчик добавления в корзину
        private void AddToCartButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Перенести данные из предпросмотра (или загрузить параллельно) в память отчета
            // TODO: Обновить CartListBox.ItemsSource
            MessageBox.Show("Скан добавлен в корзину для будущего отчета.", "Корзина", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Обработчик генерации итогового CSV
        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Запись собранных данных в CSV с помощью CsvHelper
            MessageBox.Show("CSV отчет успешно выгружен!", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}