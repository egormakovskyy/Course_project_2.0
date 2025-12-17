using System;
using System.Windows;

namespace ElevatorSim
{
    public partial class CreatePersonWindow : Window
    {
        private readonly int _totalFloors;
        private readonly object _parentWindow;
        private static int _nextPersonId = 1001;
        private bool _isFromInitialization = false;

        public CreatePersonWindow(object parentWindow, int totalFloors)
        {
            InitializeComponent();
            _parentWindow = parentWindow;
            _totalFloors = totalFloors;

            _isFromInitialization = parentWindow is InitializationWindow;

            InitializeFloorComboBoxes();
        }

        private void InitializeFloorComboBoxes()
        {
            for (int i = 1; i <= _totalFloors; i++)
            {
                CurrentFloorComboBox.Items.Add($"Этаж {i}");
                TargetFloorComboBox.Items.Add($"Этаж {i}");
            }

            CurrentFloorComboBox.SelectedIndex = 0;
            TargetFloorComboBox.SelectedIndex = 1;
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int currentFloor = CurrentFloorComboBox.SelectedIndex + 1;
                int targetFloor = TargetFloorComboBox.SelectedIndex + 1;

                if (!double.TryParse(WeightTextBox.Text, out double weight) || weight <= 0 || weight > 200)
                {
                    MessageBox.Show("Введите корректный вес (от 1 до 200 кг)", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (currentFloor == targetFloor)
                {
                    MessageBox.Show("Текущий и целевой этаж не могут совпадать", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (currentFloor < 1 || currentFloor > _totalFloors ||
                    targetFloor < 1 || targetFloor > _totalFloors)
                {
                    MessageBox.Show($"Этажи должны быть в диапазоне от 1 до {_totalFloors}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (_isFromInitialization)
                {
                    if (_parentWindow is InitializationWindow initWindow)
                    {
                        initWindow.AddPersonFromDialog(weight, currentFloor, targetFloor);
                        // УБИРАЕМ всплывающее окно сообщения
                        // MessageBox.Show($"Человек создан на этаже {currentFloor}", "Успех",
                        //     MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    if (_parentWindow is MainWindow mainWindow)
                    {
                        var newPerson = new Models.Person
                        {
                            Id = _nextPersonId++,
                            Weight = weight,
                            CurrentFloor = currentFloor,
                            StartFloor = currentFloor,
                            TargetFloor = targetFloor,
                            State = Models.PersonState.Waiting,
                            CreationTime = DateTime.Now
                        };

                        mainWindow.AddNewPerson(newPerson);
                    }
                }

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании человека: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}