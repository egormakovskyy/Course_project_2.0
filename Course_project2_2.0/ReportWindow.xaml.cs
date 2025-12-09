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
            _totalTrips = totalTrips;
            _emptyTrips = emptyTrips;
            _totalWeightMoved = totalWeightMoved;
            _totalPeopleCreated = totalPeopleCreated;

            DisplayStatistics();
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

            // Дополнительная информация
            AdditionalInfoText.Text = "Примечание:\n";
            AdditionalInfoText.Text += "• Под поездкой понимается изменение направления движения\n";
            AdditionalInfoText.Text += "• Холостая поездка - лифт двигался без пассажиров\n";
            AdditionalInfoText.Text += "• Вес считается только для доставленных людей";

            if (_emptyTrips == 0 && _totalTrips > 0)
            {
                AdditionalInfoText.Text += "\n\n✅ Отличная эффективность! Все поездки были с пассажирами.";
            }
            else if (_emptyTrips > _totalTrips / 2 && _totalTrips > 0)
            {
                AdditionalInfoText.Text += "\n\n⚠️ Много холостых поездок. Рассмотрите оптимизацию маршрутов.";
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void NewSimulationButton_Click(object sender, RoutedEventArgs e)
        {
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