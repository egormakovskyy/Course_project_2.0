using System;
using System.Windows;
using System.Windows.Input;

namespace ElevatorSim
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();

            // Устанавливаем фокус на окно для обработки клавиши Enter
            this.Loaded += (s, e) => this.Focus();

            // Обработка нажатия клавиш
            this.KeyDown += LoginWindow_KeyDown;
        }

        private void LoginWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // Если нажата клавиша Enter
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                NavigateToInitialization();
            }

            // Опционально: Escape для выхода
            if (e.Key == Key.Escape)
            {
                Application.Current.Shutdown();
            }
        }

        private void EnterButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToInitialization();
        }

        private void NavigateToInitialization()
        {
            try
            {
                // Показываем окно инициализации
                var initializationWindow = new InitializationWindow();
                initializationWindow.Show();

                // Закрываем текущее окно
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при запуске системы: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}