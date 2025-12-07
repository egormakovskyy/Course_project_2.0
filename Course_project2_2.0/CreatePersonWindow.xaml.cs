using System;
using System.Windows;
using System.Windows.Controls;
using ElevatorSim.Models;

namespace ElevatorSim
{
    public partial class CreatePersonWindow : Window
    {
        private readonly int _totalFloors;
        private readonly MainWindow _mainWindow;
        private static int _nextPersonId = 1001;

        public CreatePersonWindow(MainWindow mainWindow, int totalFloors)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _totalFloors = totalFloors;
            InitializeFloorComboBoxes();
        }

        private void InitializeFloorComboBoxes()
        {
            // Заполняем комбобоксы этажами
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
                // Получаем значения
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

                // Создаем нового человека
                var newPerson = new Person
                {
                    Id = _nextPersonId++,
                    Weight = weight,
                    CurrentFloor = currentFloor,
                    StartFloor = currentFloor,
                    TargetFloor = targetFloor,
                    State = PersonState.Waiting
                };

                // Добавляем в основную систему через главное окно
                _mainWindow?.AddNewPerson(newPerson);

                // УБРАНО: MessageBox.Show($"Человек ID {newPerson.Id} создан на этаже {currentFloor}", "Успех", ...)

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