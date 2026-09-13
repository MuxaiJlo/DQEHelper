using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DQEHelper.Services; // Namespace твоего SecureStorage

namespace DQEHelper.Views
{
    public partial class ProfileView : UserControl
    {
        public ProfileView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateAuthStatus();
            
            // Если хочешь, чтобы сохраненная кука отображалась в поле (обычно скрывают, но для себя можно):
            CookieTextBox.Text = SecureStorage.GetCoralogixCookie();
        }

        private void SaveCookieButton_Click(object sender, RoutedEventArgs e)
        {
            string newCookie = CookieTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(newCookie))
            {
                MessageBox.Show("Пожалуйста, вставь значение куки.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Сохраняем зашифрованно!
            SecureStorage.SaveCoralogixCookie(newCookie);
            
            UpdateAuthStatus();
            MessageBox.Show("Авторизационные данные успешно обновлены и зашифрованы!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateAuthStatus()
        {
            if (SecureStorage.HasValidCookie())
            {
                AuthStatusTextBlock.Text = "Authenticated ✓";
                AuthStatusTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")); // Зеленый
            }
            else
            {
                AuthStatusTextBlock.Text = "Not authenticated";
                AuthStatusTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D32F2F")); // Красный
            }
        }
    }
}