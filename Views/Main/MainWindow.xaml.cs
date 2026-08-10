using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DQEHelper.Views.Coralogix;
using DQEHelper.Views.Csv;
using DQEHelper.Views.Jenkins;
using DQEHelper.Views;

namespace DQEHelper.Views.Main
{
    public partial class MainWindow : Window
    {
        // Создаем экземпляры страниц заранее, чтобы они сохраняли свое состояние
        private readonly CsvProcessorView _csvView;
        private readonly CoralogixView _coralogixView;
        private readonly JenkinsView _jenkinsView;
        private readonly JiraReport _jiraReport;
        private readonly Border _welcomeContent;

        public MainWindow()
        {
            InitializeComponent();

            // Инициализируем наши представления (UserControl)
            _csvView = new CsvProcessorView();
            _coralogixView = new CoralogixView();
            _jenkinsView = new JenkinsView();
            _jiraReport = new JiraReport();
            _welcomeContent = CreateWelcomeContent();

            // При запуске приложения показываем приветственное сообщение и просим выбрать задачу
            MainContentArea.Content = _welcomeContent;
        }

        private Border CreateWelcomeContent()
        {
            return new Border
            {
                Background = System.Windows.Media.Brushes.Transparent,
                Padding = new Thickness(30),
                Child = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Добро пожаловать!",
                            FontSize = 28,
                            FontWeight = FontWeights.Bold,
                            Foreground = System.Windows.Media.Brushes.Black,
                            Margin = new Thickness(0,0,0,12),
                            TextAlignment = TextAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = "Пожалуйста, выберите задачу из меню слева, чтобы продолжить.",
                            FontSize = 16,
                            Foreground = System.Windows.Media.Brushes.Black,
                            TextAlignment = TextAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 520
                        }
                    }
                }
            };
        }

        // --- Обработчики меню ---

        private void NavCsvProcessor_Checked(object sender, RoutedEventArgs e)
        {
            if (MainContentArea != null && _csvView != null)
            {
                MainContentArea.Content = _csvView;
                Width = 1000;
            }
        }

        private void NavCoralogixApiFetcher_Checked(object sender, RoutedEventArgs e)
        {
            if (MainContentArea != null && _coralogixView != null)
            {
                MainContentArea.Content = _coralogixView;
                Width = 1400;
            }
        }
        private void NavJenkinsApiFetcher_Checked(object sender, RoutedEventArgs e)
        {
            if (MainContentArea != null && _jenkinsView != null)
            {
                MainContentArea.Content = _jenkinsView;
                Width = 1400;
            }
        }

        private void NavJiraReportFetcher_Checked(object sender, RoutedEventArgs e)
        {
            if (MainContentArea != null && _jiraReport != null)
            {
                MainContentArea.Content = _jiraReport;
                Width = 1400;
            }
        }

        // Позволяет перетаскивать окно за верхнюю панель
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }


        // Кнопка сворачивания
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        // Кнопка закрытия
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
