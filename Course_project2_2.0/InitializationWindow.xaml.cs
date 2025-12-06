using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Linq;

namespace ElevatorSim
{
    public partial class InitializationWindow : Window
    {
        // Класс для хранения данных о человеке
        public class Person
        {
            public int Id { get; set; }
            public double Weight { get; set; }
            public int CurrentFloor { get; set; }
            public int TargetFloor { get; set; }
            // Направление вычисляем, но не показываем в таблице
            public string Direction => TargetFloor > CurrentFloor ? "Вверх" : TargetFloor < CurrentFloor ? "Вниз" : "На месте";
        }

        private ObservableCollection<Person> _people = new ObservableCollection<Person>();
        private int _nextPersonId = 1;

        public InitializationWindow()
        {
            InitializeComponent();
            PeopleDataGrid.ItemsSource = _people;
            UpdateSummary();
            AddLog("Окно инициализации загружено. Добавьте людей в систему.");
        }

        // Класс для передачи данных в основное окно
        public class InitializationData
        {
            public int TotalFloors { get; set; }
            public int StartFloor { get; set; }
            public ObservableCollection<Person> People { get; set; }
        }

        public InitializationData Data { get; private set; }

        // Кнопка "Добавить человека"
        private void AddPersonButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Получаем количество этажей для проверки
                if (!int.TryParse(FloorsTextBox.Text, out int totalFloors) || totalFloors < 2)
                {
                    MessageBox.Show("Сначала укажите корректное количество этажей (минимум 2)", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Создаем нового человека со значениями по умолчанию
                var person = new Person
                {
                    Id = _nextPersonId++,
                    Weight = 70,
                    CurrentFloor = 1,
                    TargetFloor = Math.Min(totalFloors, 2) // Чтобы не выходить за пределы этажей
                };
                
                _people.Add(person);
                AddLog($"Добавлен человек ID {person.Id}: вес {person.Weight}кг, с этажа {person.CurrentFloor} на этаж {person.TargetFloor}");
                UpdateSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении человека: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Кнопка "Удалить выбранного"
        private void RemovePersonButton_Click(object sender, RoutedEventArgs e)
        {
            if (PeopleDataGrid.SelectedItem is Person selectedPerson)
            {
                _people.Remove(selectedPerson);
                AddLog($"Удален человек ID {selectedPerson.Id}");
                UpdateSummary();
            }
            else
            {
                MessageBox.Show("Выберите человека для удаления", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Обновление сводной информации
        private void UpdateSummary()
        {
            var totalWeight = _people.Sum(p => p.Weight);
            var upCount = _people.Count(p => p.Direction == "Вверх");
            var downCount = _people.Count(p => p.Direction == "Вниз");
            
            SummaryTextBlock.Text = $"Всего людей: {_people.Count}\n" +
                                   $"Общий вес: {totalWeight} кг\n" +
                                   $"Хотят подняться: {upCount}\n" +
                                   $"Хотят спуститься: {downCount}";
        }

        // Кнопка "Инициализировать систему"
        private void InitializeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Проверка количества этажей
                if (!int.TryParse(FloorsTextBox.Text, out int totalFloors) || totalFloors < 2)
                {
                    MessageBox.Show("Введите корректное количество этажей (минимум 2)", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Проверка стартового этажа
                if (!int.TryParse(StartFloorTextBox.Text, out int startFloor) ||
                    startFloor < 1 || startFloor > totalFloors)
                {
                    MessageBox.Show($"Стартовый этаж должен быть от 1 до {totalFloors}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Проверка наличия людей
                if (_people.Count == 0)
                {
                    MessageBox.Show("Добавьте хотя бы одного человека", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Проверка всех людей
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

                AddLog("Инициализация завершена успешно!");

                // Открываем основное окно
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

        // Кнопка "Отмена"
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Отменить инициализацию и выйти?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }

        // Добавление записи в лог
        private void AddLog(string message)
        {
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            LogTextBox.ScrollToEnd();
        }
    }
}