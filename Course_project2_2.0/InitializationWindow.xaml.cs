using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ElevatorSim
{
    public partial class InitializationWindow : Window
    {
        public class Person
        {
            public int Id { get; set; }
            public double Weight { get; set; }
            public int CurrentFloor { get; set; }
            public int TargetFloor { get; set; }
            public string Direction => TargetFloor > CurrentFloor ? "Вверх" : TargetFloor < CurrentFloor ? "Вниз" : "На месте";
            public DateTime CreationTime { get; set; } // Добавляем время создания
        }

        private ObservableCollection<Person> _people = new ObservableCollection<Person>();
        private int _nextPersonId = 1;
        private MainWindow _mainWindow;

        public InitializationWindow()
        {
            InitializeComponent();
            PeopleDataGrid.ItemsSource = _people;
        }

        private void AddPersonButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(FloorsTextBox.Text, out int totalFloors) || totalFloors < 2)
                {
                    MessageBox.Show("Сначала укажите корректное количество этажей (минимум 2)", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var createWindow = new CreatePersonWindow(this, totalFloors)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                createWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии окна создания человека: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void AddPersonFromDialog(double weight, int currentFloor, int targetFloor)
        {
            try
            {
                var person = new Person
                {
                    Id = _nextPersonId++,
                    Weight = weight,
                    CurrentFloor = currentFloor,
                    TargetFloor = targetFloor,
                    CreationTime = DateTime.Now // Устанавливаем время создания
                };

                _people.Add(person);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении человека: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemovePersonButton_Click(object sender, RoutedEventArgs e)
        {
            if (PeopleDataGrid.SelectedItem is Person selectedPerson)
            {
                _people.Remove(selectedPerson);
            }
            else
            {
                MessageBox.Show("Выберите человека для удаления", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void InitializeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(FloorsTextBox.Text, out int totalFloors) || totalFloors < 2)
                {
                    MessageBox.Show("Введите корректное количество этажей (минимум 2)", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!int.TryParse(StartFloorTextBox.Text, out int startFloor) ||
                    startFloor < 1 || startFloor > totalFloors)
                {
                    MessageBox.Show($"Стартовый этаж должен быть от 1 до {totalFloors}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                foreach (var person in _people)
                {
                    if (person.Weight <= 0 || person.Weight > 200)
                    {
                        MessageBox.Show($"Вес человека ID {person.Id} должен быть от 1 до 200 кг", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (person.CurrentFloor < 1 || person.CurrentFloor > totalFloors)
                    {
                        MessageBox.Show($"Текущий этаж человека ID {person.Id} должен быть от 1 до {totalFloors}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (person.TargetFloor < 1 || person.TargetFloor > totalFloors)
                    {
                        MessageBox.Show($"Целевой этаж человека ID {person.Id} должен быть от 1 до {totalFloors}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (person.CurrentFloor == person.TargetFloor)
                    {
                        MessageBox.Show($"Человек ID {person.Id} уже находится на целевом этаже", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                var mainData = new MainWindow.InitializationData
                {
                    TotalFloors = totalFloors,
                    StartFloor = startFloor,
                    People = new ObservableCollection<Person>(_people)
                };
                var mainWindow = new MainWindow(mainData);
                mainWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Отменить инициализацию и выйти?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }
    }
}