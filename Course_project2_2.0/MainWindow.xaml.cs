using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Linq;
using System.Collections.Generic;

namespace ElevatorSim
{
    public partial class MainWindow : Window
    {
        // Класс данных
        public class InitializationData
        {
            public int TotalFloors { get; set; }
            public int StartFloor { get; set; }
            public ObservableCollection<InitializationWindow.Person> People { get; set; }
        }

        // Класс человека в системе
        public class PersonInSystem
        {
            public int Id { get; set; }
            public double Weight { get; set; }
            public int CurrentFloor { get; set; }
            public int TargetFloor { get; set; }
            public PersonState State { get; set; }
            public string Status => GetStatus();

            private string GetStatus()
            {
                switch (State)
                {
                    case PersonState.Waiting:
                        return $"Ожидает на {CurrentFloor} этаже";
                    case PersonState.InElevator:
                        return $"В лифте → этаж {TargetFloor}";
                    case PersonState.Delivered:
                        return $"Доставлен на {TargetFloor} этаж";
                    default:
                        return "Неизвестно";
                }
            }
        }

        public enum PersonState
        {
            Waiting,      // Ожидает лифт
            InElevator,   // В лифте
            Delivered     // Доставлен
        }

        // Класс лифта
        public class Elevator
        {
            public int CurrentFloor { get; set; }
            public ElevatorState State { get; set; }
            public Direction CurrentDirection { get; set; }
            public List<int> TargetFloors { get; set; } = new List<int>();
            public List<PersonInSystem> PeopleInside { get; set; } = new List<PersonInSystem>();
            public double CurrentWeight
            {
                get
                {
                    double total = 0;
                    foreach (var person in PeopleInside)
                    {
                        total += person.Weight;
                    }
                    return total;
                }
            }
            public bool IsOverloaded => CurrentWeight > 400;
        }

        public enum ElevatorState
        {
            Idle,         // Ожидание
            MovingUp,     // Движение вверх
            MovingDown,   // Движение вниз
            DoorsOpen,    // Двери открыты
            Overloaded    // Перегрузка
        }

        public enum Direction
        {
            Up,
            Down,
            None
        }

        private InitializationData _initData;
        private ObservableCollection<PersonInSystem> _allPeople = new ObservableCollection<PersonInSystem>();
        private Elevator _elevator = new Elevator();
        private DispatcherTimer _simulationTimer;
        private int _currentTime = 0;
        private int _transportedCount = 0;
        private bool _isPaused = false;

        // Визуальные элементы
        private List<Border> _floorVisuals = new List<Border>();
        private List<TextBlock> _floorTexts = new List<TextBlock>();
        private List<TextBlock> _peopleTexts = new List<TextBlock>();
        private Border _elevatorVisual;
        private TextBlock _elevatorPeopleText;

        // Конструктор с данными
        public MainWindow(InitializationData initData)
        {
            InitializeComponent();
            _initData = initData;
            InitializeSystem();
            CreateVisualInterface();
            InitializeTimer();
        }

        // Инициализация системы
        private void InitializeSystem()
        {
            _elevator.CurrentFloor = _initData.StartFloor;
            _elevator.State = ElevatorState.Idle;
            _elevator.CurrentDirection = Direction.None;

            // Конвертируем людей из InitializationWindow в систему
            foreach (var person in _initData.People)
            {
                _allPeople.Add(new PersonInSystem
                {
                    Id = person.Id,
                    Weight = person.Weight,
                    CurrentFloor = person.CurrentFloor,
                    TargetFloor = person.TargetFloor,
                    State = PersonState.Waiting
                });
            }

            UpdateStatus("Система инициализирована. Готов к запуску.");
            UpdateInfoPanel();
        }

        // Создание визуального интерфейса
        private void CreateVisualInterface()
        {
            MainContentGrid.Children.Clear();
            _floorVisuals.Clear();
            _floorTexts.Clear();
            _peopleTexts.Clear();

            // Создаем сетку для этажей
            var floorsGrid = new Grid();

            // 2 колонки: этажи + информация
            floorsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            floorsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

            // Панель для этажей (левая колонка)
            var floorsStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(20)
            };

            // Создаем визуализацию для каждого этажа (сверху вниз)
            for (int floorNum = _initData.TotalFloors; floorNum >= 1; floorNum--)
            {
                // Контейнер для этажа
                var floorContainer = new Border
                {
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 5, 0, 5),
                    Padding = new Thickness(10),
                    CornerRadius = new CornerRadius(5),
                    Width = 300
                };

                // Сетка внутри контейнера
                var floorGrid = new Grid();
                floorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                floorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Текст этажа
                var floorText = new TextBlock
                {
                    Text = $"Этаж {floorNum}",
                    FontSize = 16,
                    FontWeight = FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Текст людей на этаже
                var peopleText = new TextBlock
                {
                    Text = "",
                    FontSize = 14,
                    FontWeight = FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0),
                    Foreground = Brushes.DarkBlue
                };

                Grid.SetColumn(floorText, 0);
                Grid.SetColumn(peopleText, 1);
                floorGrid.Children.Add(floorText);
                floorGrid.Children.Add(peopleText);

                floorContainer.Child = floorGrid;
                floorsStack.Children.Add(floorContainer);

                // Сохраняем ссылки
                _floorVisuals.Add(floorContainer);
                _floorTexts.Add(floorText);
                _peopleTexts.Add(peopleText);
            }

            Grid.SetColumn(floorsStack, 0);
            floorsGrid.Children.Add(floorsStack);

            // Панель информации (правая колонка)
            var infoStack = new StackPanel
            {
                Margin = new Thickness(20),
                Orientation = Orientation.Vertical
            };

            // Визуализация лифта
            var elevatorContainer = new Border
            {
                BorderBrush = Brushes.DarkBlue,
                BorderThickness = new Thickness(2),
                Background = Brushes.LightBlue,
                Padding = new Thickness(15),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 20)
            };

            var elevatorGrid = new Grid();
            elevatorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            elevatorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var elevatorTitle = new TextBlock
            {
                Text = "ЛИФТ",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.DarkBlue,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _elevatorPeopleText = new TextBlock
            {
                Text = "Пустой",
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };

            Grid.SetRow(elevatorTitle, 0);
            Grid.SetRow(_elevatorPeopleText, 1);
            elevatorGrid.Children.Add(elevatorTitle);
            elevatorGrid.Children.Add(_elevatorPeopleText);

            elevatorContainer.Child = elevatorGrid;
            _elevatorVisual = elevatorContainer;

            infoStack.Children.Add(elevatorContainer);

            // Список людей
            var peopleTitle = new TextBlock
            {
                Text = "Люди в системе:",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            infoStack.Children.Add(peopleTitle);

            var peopleList = new ListView
            {
                Height = 300,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var gridView = new GridView();
            gridView.Columns.Add(new GridViewColumn
            {
                Header = "ID",
                DisplayMemberBinding = new System.Windows.Data.Binding("Id"),
                Width = 50
            });
            gridView.Columns.Add(new GridViewColumn
            {
                Header = "Вес",
                DisplayMemberBinding = new System.Windows.Data.Binding("Weight"),
                Width = 60
            });
            gridView.Columns.Add(new GridViewColumn
            {
                Header = "Статус",
                DisplayMemberBinding = new System.Windows.Data.Binding("Status"),
                Width = 200
            });

            peopleList.View = gridView;
            peopleList.ItemsSource = _allPeople;
            infoStack.Children.Add(peopleList);

            // Лог событий
            var logTitle = new TextBlock
            {
                Text = "Лог событий:",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            infoStack.Children.Add(logTitle);

            var logTextBox = new TextBox
            {
                Height = 150,
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12
            };
            infoStack.Children.Add(logTextBox);

            Grid.SetColumn(infoStack, 1);
            floorsGrid.Children.Add(infoStack);

            MainContentGrid.Children.Add(floorsGrid);

            UpdateVisuals();
        }

        // Инициализация таймера
        private void InitializeTimer()
        {
            _simulationTimer = new DispatcherTimer();
            _simulationTimer.Interval = TimeSpan.FromSeconds(1);
            _simulationTimer.Tick += SimulationTimer_Tick;
        }

        // Обновление визуализации
        private void UpdateVisuals()
        {
            // Обновляем этажи
            for (int i = 0; i < _initData.TotalFloors; i++)
            {
                int floorNumber = _initData.TotalFloors - i; // Инвертируем порядок
                var floorContainer = _floorVisuals[i];
                var floorText = _floorTexts[i];
                var peopleText = _peopleTexts[i];

                // Подсвечиваем текущий этаж лифта
                if (floorNumber == _elevator.CurrentFloor)
                {
                    floorContainer.Background = Brushes.LightCoral;
                    floorText.FontWeight = FontWeights.Bold;
                    floorText.Text = $"Этаж {floorNumber} [ЛИФТ]";
                }
                else
                {
                    floorContainer.Background = Brushes.White;
                    floorText.FontWeight = FontWeights.Normal;
                    floorText.Text = $"Этаж {floorNumber}";
                }

                // Показываем людей на этаже
                var peopleOnFloor = new List<PersonInSystem>();
                foreach (var person in _allPeople)
                {
                    if (person.State == PersonState.Waiting && person.CurrentFloor == floorNumber)
                    {
                        peopleOnFloor.Add(person);
                    }
                }

                if (peopleOnFloor.Count > 0)
                {
                    var ids = new List<string>();
                    foreach (var person in peopleOnFloor)
                    {
                        ids.Add(person.Id.ToString());
                    }
                    peopleText.Text = $"Люди: {string.Join(", ", ids)}";
                }
                else
                {
                    peopleText.Text = "";
                }
            }

            // Обновляем лифт
            string stateText;
            switch (_elevator.State)
            {
                case ElevatorState.Idle:
                    stateText = "Ожидание";
                    break;
                case ElevatorState.MovingUp:
                    stateText = $"Движение вверх → {_elevator.CurrentFloor}";
                    break;
                case ElevatorState.MovingDown:
                    stateText = $"Движение вниз → {_elevator.CurrentFloor}";
                    break;
                case ElevatorState.DoorsOpen:
                    stateText = "Двери открыты";
                    break;
                case ElevatorState.Overloaded:
                    stateText = "ПЕРЕГРУЗКА!";
                    break;
                default:
                    stateText = "Неизвестно";
                    break;
            }

            _elevatorVisual.Background = _elevator.IsOverloaded ? Brushes.OrangeRed : Brushes.LightBlue;

            // Люди в лифте
            if (_elevator.PeopleInside.Count > 0)
            {
                var ids = new List<string>();
                foreach (var person in _elevator.PeopleInside)
                {
                    ids.Add(person.Id.ToString());
                }
                _elevatorPeopleText.Text = $"Люди: {string.Join(", ", ids)}\n" +
                                          $"Вес: {_elevator.CurrentWeight} кг";
            }
            else
            {
                _elevatorPeopleText.Text = "Пустой";
            }

            UpdateInfoPanel();
        }

        // Обновление информационной панели
        private void UpdateInfoPanel()
        {
            CurrentFloorText.Text = _elevator.CurrentFloor.ToString();
            ElevatorStateText.Text = _elevator.State.ToString();
            WeightText.Text = $"{_elevator.CurrentWeight} кг";
            TransportedText.Text = _transportedCount.ToString();
        }

        // Обновление статуса
        private void UpdateStatus(string message)
        {
            StatusText.Text = $"Статус: {message}";
            AddLog(message);
        }

        // Добавление в лог
        private void AddLog(string message)
        {
            try
            {
                // Ищем TextBox для лога более безопасным способом
                if (MainContentGrid.Children.Count > 0 &&
                    MainContentGrid.Children[0] is Grid mainGrid &&
                    mainGrid.Children.Count > 1 &&
                    mainGrid.Children[1] is StackPanel infoStack &&
                    infoStack.Children.Count > 4 &&
                    infoStack.Children[4] is TextBox logTextBox)
                {
                    logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                    logTextBox.ScrollToEnd();
                }
                else
                {
                    // Если не нашли лог, выводим в консоль
                    System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при записи в лог: {ex.Message}");
            }
        }

        // Основная логика симуляции
        private void SimulationTimer_Tick(object sender, EventArgs e)
        {
            if (_isPaused) return;

            _currentTime++;

            switch (_elevator.State)
            {
                case ElevatorState.Idle:
                    ProcessIdleState();
                    break;

                case ElevatorState.MovingUp:
                case ElevatorState.MovingDown:
                    ProcessMovingState();
                    break;

                case ElevatorState.DoorsOpen:
                    ProcessDoorsOpenState();
                    break;

                case ElevatorState.Overloaded:
                    ProcessOverloadedState();
                    break;
            }

            UpdateVisuals();

            // Проверка завершения
            bool allDelivered = true;
            foreach (var person in _allPeople)
            {
                if (person.State != PersonState.Delivered)
                {
                    allDelivered = false;
                    break;
                }
            }

            if (allDelivered)
            {
                _simulationTimer.Stop();
                UpdateStatus("Все люди доставлены! Симуляция завершена.");
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = false;
                PauseButton.IsEnabled = false;
                StepButton.IsEnabled = false;
            }
        }

        // Обработка состояния "Ожидание"
        private void ProcessIdleState()
        {
            // Ищем цели для лифта
            FindNextTarget();

            if (_elevator.TargetFloors.Count > 0)
            {
                // Определяем направление
                int nextTarget = _elevator.TargetFloors[0];
                _elevator.CurrentDirection = nextTarget > _elevator.CurrentFloor ? Direction.Up : Direction.Down;
                _elevator.State = _elevator.CurrentDirection == Direction.Up ? ElevatorState.MovingUp : ElevatorState.MovingDown;
                UpdateStatus($"Лифт начал движение {_elevator.CurrentDirection} к этажу {nextTarget}");
            }
        }

        // Обработка состояния "Движение"
        private void ProcessMovingState()
        {
            // Двигаем лифт
            if (_elevator.CurrentDirection == Direction.Up)
            {
                _elevator.CurrentFloor++;
            }
            else if (_elevator.CurrentDirection == Direction.Down)
            {
                _elevator.CurrentFloor--;
            }

            UpdateStatus($"Лифт на этаже {_elevator.CurrentFloor}");

            // Проверяем, достигли ли цели
            if (_elevator.TargetFloors.Contains(_elevator.CurrentFloor))
            {
                _elevator.State = ElevatorState.DoorsOpen;
                _elevator.TargetFloors.Remove(_elevator.CurrentFloor);
                UpdateStatus($"Лифт остановился на этаже {_elevator.CurrentFloor}. Двери открываются.");
            }
        }

        // Обработка состояния "Двери открыты"
        private void ProcessDoorsOpenState()
        {
            // Высадка людей
            var peopleToExit = new List<PersonInSystem>();
            foreach (var person in _elevator.PeopleInside)
            {
                if (person.TargetFloor == _elevator.CurrentFloor)
                {
                    peopleToExit.Add(person);
                }
            }

            foreach (var person in peopleToExit)
            {
                person.State = PersonState.Delivered;
                _elevator.PeopleInside.Remove(person);
                _transportedCount++;
                UpdateStatus($"Человек {person.Id} вышел на этаже {_elevator.CurrentFloor}");
            }

            // Посадка людей
            var peopleToEnter = new List<PersonInSystem>();
            foreach (var person in _allPeople)
            {
                if (person.State == PersonState.Waiting &&
                    person.CurrentFloor == _elevator.CurrentFloor &&
                    !_elevator.IsOverloaded)
                {
                    peopleToEnter.Add(person);
                }
            }

            foreach (var person in peopleToEnter)
            {
                // Проверяем вес
                if (_elevator.CurrentWeight + person.Weight <= 400)
                {
                    person.State = PersonState.InElevator;
                    _elevator.PeopleInside.Add(person);
                    if (!_elevator.TargetFloors.Contains(person.TargetFloor))
                    {
                        _elevator.TargetFloors.Add(person.TargetFloor);
                    }
                    UpdateStatus($"Человек {person.Id} вошел в лифт (вес: {person.Weight} кг)");
                }
            }

            // Убираем дубликаты целей и сортируем
            var uniqueFloors = new List<int>();
            foreach (var floor in _elevator.TargetFloors)
            {
                if (!uniqueFloors.Contains(floor))
                {
                    uniqueFloors.Add(floor);
                }
            }
            _elevator.TargetFloors = uniqueFloors;

            if (_elevator.CurrentDirection == Direction.Up)
            {
                _elevator.TargetFloors.Sort();
            }
            else
            {
                _elevator.TargetFloors.Sort((a, b) => b.CompareTo(a));
            }

            // Проверяем перегрузку
            if (_elevator.IsOverloaded)
            {
                _elevator.State = ElevatorState.Overloaded;
                UpdateStatus("ПЕРЕГРУЗКА! Лифт не может двигаться.");
            }
            else
            {
                _elevator.State = ElevatorState.Idle;
                UpdateStatus("Двери закрываются");
            }
        }

        // Обработка состояния "Перегрузка"
        private void ProcessOverloadedState()
        {
            // В реальной системе нужно высадить кого-то, но в симуляции просто ждем
            UpdateStatus("Лифт перегружен. Требуется освободить место.");
            _elevator.State = ElevatorState.Idle;
        }

        // Поиск следующей цели
        private void FindNextTarget()
        {
            if (_elevator.TargetFloors.Count == 0)
            {
                // Ищем людей, ожидающих лифт
                var waitingPeople = new List<PersonInSystem>();
                foreach (var person in _allPeople)
                {
                    if (person.State == PersonState.Waiting)
                    {
                        waitingPeople.Add(person);
                    }
                }

                if (waitingPeople.Count > 0)
                {
                    // Берем ближайшего человека
                    PersonInSystem nearestPerson = null;
                    int minDistance = int.MaxValue;

                    foreach (var person in waitingPeople)
                    {
                        int distance = Math.Abs(person.CurrentFloor - _elevator.CurrentFloor);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            nearestPerson = person;
                        }
                    }

                    if (nearestPerson != null)
                    {
                        _elevator.TargetFloors.Add(nearestPerson.CurrentFloor);
                    }
                }
            }
        }

        #region Обработчики кнопок

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            _simulationTimer.Start();
            UpdateStatus("Симуляция запущена");
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            PauseButton.IsEnabled = true;
            StepButton.IsEnabled = true;
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _simulationTimer.Stop();
            UpdateStatus("Симуляция остановлена");
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            PauseButton.IsEnabled = false;
            StepButton.IsEnabled = false;
            _isPaused = false;
            PauseButton.Content = "Пауза";
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = !_isPaused;
            if (_isPaused)
            {
                UpdateStatus("Симуляция на паузе");
                PauseButton.Content = "Продолжить";
            }
            else
            {
                UpdateStatus("Симуляция продолжена");
                PauseButton.Content = "Пауза";
            }
        }

        private void StepButton_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = true;
            PauseButton.Content = "Продолжить";
            SimulationTimer_Tick(null, EventArgs.Empty);
            UpdateStatus("Выполнен шаг симуляции (1 секунда)");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _simulationTimer?.Stop();
        }

        #endregion
    }
}