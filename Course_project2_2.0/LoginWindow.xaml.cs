using System.Windows;

namespace ElevatorSim
{
    public partial class LoginWindow : Window
    {
        private const string CorrectPassword = "admin"; // Пароль по умолчанию

        public LoginWindow()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string enteredPassword = PasswordBox.Password;

            if (enteredPassword == CorrectPassword)
            {
                // Открываем окно инициализации
                InitializationWindow initWindow = new InitializationWindow();
                initWindow.Show();
                this.Close();
            }
            else
            {
                ErrorMessage.Text = "Неверный пароль!";
                ErrorMessage.Visibility = Visibility.Visible;
            }
        }
    }
}