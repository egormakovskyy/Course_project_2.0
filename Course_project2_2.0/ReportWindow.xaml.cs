using System;
using System.Windows;

namespace ElevatorSim
{
    public partial class ReportWindow : Window
    {
        private readonly int _totalTrips;
        private readonly int _emptyTrips;
        private readonly double _totalWeightMoved;
        private readonly int _totalPeopleCreated;

        public ReportWindow(int totalTrips, int emptyTrips, double totalWeightMoved, int totalPeopleCreated)
        {
            InitializeComponent();

            // Добавляем обработчик закрытия окна
            this.Closing += ReportWindow_Closing;

            _totalTrips = totalTrips;
            _emptyTrips = emptyTrips;
            _totalWeightMoved = totalWeightMoved;
            _totalPeopleCreated = totalPeopleCreated;

            DisplayStatistics();
        }

        private void ReportWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Просто завершаем приложение при закрытии окна
            Application.Current.Shutdown();
        }

        private void DisplayStatistics()
        {
            // Отображаем статистику
            TotalTripsText.Text = _totalTrips.ToString();
            EmptyTripsText.Text = _emptyTrips.ToString();
            TotalWeightText.Text = $"{_totalWeightMoved:F1} кг";
            TotalPeopleText.Text = _totalPeopleCreated.ToString();

            // Рассчитываем и отображаем эффективность
            if (_totalTrips > 0)
            {
                double efficiency = ((double)(_totalTrips - _emptyTrips) / _totalTrips) * 100;
                EfficiencyText.Text = $"{efficiency:F1}%";

                if (efficiency >= 70)
                    EfficiencyText.Foreground = System.Windows.Media.Brushes.Green;
                else if (efficiency >= 40)
                    EfficiencyText.Foreground = System.Windows.Media.Brushes.Orange;
                else
                    EfficiencyText.Foreground = System.Windows.Media.Brushes.Red;
            }
            else
            {
                EfficiencyText.Text = "Н/Д";
                EfficiencyText.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void NewSimulationButton_Click(object sender, RoutedEventArgs e)
        {
            // Убираем обработчик, чтобы не вызывать Shutdown при программном закрытии
            this.Closing -= ReportWindow_Closing;

            // Открываем окно инициализации
            var initWindow = new InitializationWindow();
            initWindow.Show();

            // Закрываем текущее окно отчета
            this.Close();

            // Закрываем главное окно (MainWindow), если оно открыто
            if (this.Owner != null)
            {
                this.Owner.Close();
            }
        }
    }
}