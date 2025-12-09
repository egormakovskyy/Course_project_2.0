using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Linq;
using System.Collections.Generic;
using ElevatorSim.Models;

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

        private InitializationData _initData;
        private ObservableCollection<Person> _allPeople = new ObservableCollection<Person>();
        private Elevator _elevator = new Elevator();
        private DispatcherTimer _simulationTimer;
        private DispatcherTimer _cleanupTimer;
        private DispatcherTimer _statusTimer;
        private int _currentTime = 0;
        private int _transportedCount = 0;
        private int _nextPersonId = 1;
        private bool _systemStopped = false;
        private DateTime _systemStartTime;

        // Статистика
        private int _totalTrips = 0;
        private int _emptyTrips = 0;
        private double _totalWeightMoved = 0;
        private int _totalPeopleCreated = 0;
        private Direction _lastDirection = Direction.None;

        // Визуальные элементы
        private List<Border> _floorVisuals = new List<Border>();
        private List<TextBlock> _floorTexts = new List<TextBlock>();
        private Border _elevatorVisual;
        private TextBlock _elevatorPeopleText;
        private TextBox _logTextBox;
        private ListView _peopleListView;

        // Конструктор с данными
        public MainWindow(InitializationData initData)
        {
            InitializeComponent();
            _initData = initData;
            _totalPeopleCreated = initData.People.Count; // Учитываем людей из инициализации
            InitializeSystem();
            CreateVisualInterface();
            InitializeTimers();
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
                _allPeople.Add(new Person
                {
                    Id = _nextPersonId++,
                    Weight = person.Weight,
                    CurrentFloor = person.CurrentFloor,
                    StartFloor = person.CurrentFloor,
                    TargetFloor = person.TargetFloor,
                    State = PersonState.Waiting
                });
            }

            UpdateStatus("Система инициализирована. Готов к запуску.");
            UpdateInfoPanel();
        }

        // Инициализация таймеров
        private void InitializeTimers()
        {
            _simulationTimer = new DispatcherTimer();
            _simulationTimer.Interval = TimeSpan.FromSeconds(1);
            _simulationTimer.Tick += SimulationTimer_Tick;

            _cleanupTimer = new DispatcherTimer();
            _cleanupTimer.Interval = TimeSpan.FromSeconds(1);
            _cleanupTimer.Tick += CleanupTimer_Tick;

            // Таймер для обновления времени работы системы
            _statusTimer = new DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(1);
            _statusTimer.Tick += StatusTimer_Tick;
        }

        // Таймер обновления статуса (время работы)
        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            if (!_systemStopped)
            {
                UpdateElapsedTime();
            }
        }

        // Обновление прошедшего времени
        private void UpdateElapsedTime()
        {
            if (_simulationTimer.IsEnabled)
            {
                TimeSpan elapsedTime = DateTime.Now - _systemStartTime;
                ElapsedTimeText.Text = $"{elapsedTime:mm\\:ss}";
            }
            else
            {
                ElapsedTimeText.Text = "00:00";
            }
        }

        // Публичный метод для добавления нового человека из других окон
        public void AddNewPerson(Person person)
        {
            Dispatcher.Invoke(() =>
            {
                _allPeople.Add(person);
                _totalPeopleCreated++;
                UpdateStatus($"Создан новый человек ID {person.Id} на этаже {person.CurrentFloor}");
                UpdateInfoPanel();
            });
        }

        // Таймер очистки доставленных людей
        private void CleanupTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (_systemStopped) return;

                var peopleToRemoveNow = new List<Person>();

                // Проверяем, прошло ли 5 секунд с момента доставки
                foreach (var person in _allPeople.Where(p => p.State == PersonState.Delivered).ToList())
                {
                    if ((DateTime.Now - person.DeliveryTime).TotalSeconds >= 5)
                    {
                        peopleToRemoveNow.Add(person);
                    }
                }

                foreach (var person in peopleToRemoveNow)
                {
                    _allPeople.Remove(person);
                    AddLog($"Человек ID {person.Id} удален из системы");
                }

                UpdateInfoPanel();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в CleanupTimer: {ex.Message}");
            }
        }

        // Создание визуального интерфейса
        private void CreateVisualInterface()
        {
            MainContentGrid.Children.Clear();
            _floorVisuals.Clear();
            _floorTexts.Clear();

            var floorsGrid = new Grid();
            floorsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            floorsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

            // Панель этажей
            var floorsStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(20)
            };

            for (int floorNum = _initData.TotalFloors; floorNum >= 1; floorNum--)
            {
                var floorContainer = new Border
                {
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 5, 0, 5),
                    Padding = new Thickness(10),
                    CornerRadius = new CornerRadius(5),
                    Width = 250,
                    Height = 40
                };

                var floorText = new TextBlock
                {
                    Text = $"Этаж {floorNum}",
                    FontSize = 16,
                    FontWeight = FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                floorContainer.Child = floorText;
                floorsStack.Children.Add(floorContainer);

                _floorVisuals.Add(floorContainer);
                _floorTexts.Add(floorText);
            }

            Grid.SetColumn(floorsStack, 0);
            floorsGrid.Children.Add(floorsStack);

            // Панель информации
            var infoStack = new StackPanel
            {
                Margin = new Thickness(20),
                Orientation = Orientation.Vertical
            };

            // Лифт
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

            _peopleListView = new ListView
            {
                Height = 250,
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
                Width = 250
            });

            _peopleListView.View = gridView;
            _peopleListView.ItemsSource = _allPeople;
            infoStack.Children.Add(_peopleListView);

            // Лог событий
            var logTitle = new TextBlock
            {
                Text = "Лог событий:",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            infoStack.Children.Add(logTitle);

            _logTextBox = new TextBox
            {
                Height = 120,
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12
            };
            infoStack.Children.Add(_logTextBox);

            Grid.SetColumn(infoStack, 1);
            floorsGrid.Children.Add(infoStack);
            MainContentGrid.Children.Add(floorsGrid);

            UpdateVisuals();
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

                // Подсвечиваем текущий этаж лифта
                if (floorNumber == _elevator.CurrentFloor)
                {
                    floorContainer.Background = Brushes.LightCoral;
                    floorText.FontWeight = FontWeights.Bold;
                    floorText.Text = $"Этаж {floorNumber} [ЛИФТ]";
                    floorText.Foreground = Brushes.Red;
                }
                else
                {
                    floorContainer.Background = Brushes.White;
                    floorText.FontWeight = FontWeights.Normal;
                    floorText.Text = $"Этаж {floorNumber}";
                    floorText.Foreground = Brushes.Black;
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
                    stateText = $"Движение вверх";
                    break;
                case ElevatorState.MovingDown:
                    stateText = $"Движение вниз";
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
                _elevatorPeopleText.Text = $"Люди внутри: {string.Join(", ", ids)}\n" +
                                          $"Вес: {_elevator.CurrentWeight:F1} кг\n" +
                                          $"Состояние: {stateText}";
            }
            else
            {
                _elevatorPeopleText.Text = $"Пустой\nСостояние: {stateText}";
            }

            UpdateInfoPanel();
        }

        // Обновление информационной панели
        private void UpdateInfoPanel()
        {
            CurrentFloorText.Text = _elevator.CurrentFloor.ToString();
            ElevatorStateText.Text = _elevator.State.ToString();
            WeightText.Text = $"{_elevator.CurrentWeight:F1} кг";
            TransportedCountText.Text = _transportedCount.ToString();

            // Обновляем состояние кнопки Стоп
            StopButton.IsEnabled = (_elevator.PeopleInside.Count == 0);
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
                Dispatcher.Invoke(() =>
                {
                    if (_logTextBox != null)
                    {
                        _logTextBox.AppendText($"[{_currentTime} сек] {message}\n");
                        _logTextBox.ScrollToEnd();
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при записи в лог: {ex.Message}");
            }
        }

        // Основная логика симуляции
        private void SimulationTimer_Tick(object sender, EventArgs e)
        {
            if (_systemStopped) return;

            _currentTime++;

            // Обновляем текущий этаж для людей в лифте
            foreach (var person in _elevator.PeopleInside)
            {
                person.CurrentFloor = _elevator.CurrentFloor;
            }

            switch (_elevator.State)
            {
                case ElevatorState.Idle:
                    ProcessIdleState();
                    break;

                case ElevatorState.MovingUp:
                    ProcessMovingUpState();
                    break;

                case ElevatorState.MovingDown:
                    ProcessMovingDownState();
                    break;

                case ElevatorState.DoorsOpen:
                    ProcessDoorsOpenState();
                    break;

                case ElevatorState.Overloaded:
                    ProcessOverloadedState();
                    break;
            }

            UpdateVisuals();
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

                // Проверяем границы этажей
                if (nextTarget < 1)
                {
                    _elevator.TargetFloors.Remove(nextTarget);
                    FindNextTarget();
                    return;
                }

                if (nextTarget > _elevator.CurrentFloor)
                {
                    // Учет смены направления
                    if (_elevator.CurrentDirection != Direction.Up)
                    {
                        _totalTrips++;
                        if (_elevator.PeopleInside.Count == 0) _emptyTrips++;
                        _lastDirection = Direction.Up;
                    }

                    _elevator.CurrentDirection = Direction.Up;
                    _elevator.State = ElevatorState.MovingUp;
                    UpdateStatus($"Лифт начал движение ВВЕРХ к этажу {nextTarget}");
                }
                else if (nextTarget < _elevator.CurrentFloor)
                {
                    // Учет смены направления
                    if (_elevator.CurrentDirection != Direction.Down)
                    {
                        _totalTrips++;
                        if (_elevator.PeopleInside.Count == 0) _emptyTrips++;
                        _lastDirection = Direction.Down;
                    }

                    _elevator.CurrentDirection = Direction.Down;
                    _elevator.State = ElevatorState.MovingDown;
                    UpdateStatus($"Лифт начал движение ВНИЗ к этажу {nextTarget}");
                }
                else
                {
                    _elevator.State = ElevatorState.DoorsOpen;
                    _elevator.TargetFloors.Remove(nextTarget);
                    UpdateStatus($"Лифт на целевом этаже {nextTarget}. Двери открываются.");
                }
            }
        }

        // Обработка состояния "Движение вверх"
        private void ProcessMovingUpState()
        {
            // Двигаем лифт вверх
            _elevator.CurrentFloor++;

            // Проверяем верхнюю границу
            if (_elevator.CurrentFloor > _initData.TotalFloors)
            {
                _elevator.CurrentFloor = _initData.TotalFloors;
                _elevator.State = ElevatorState.Idle;
                UpdateStatus($"Достигнут верхний этаж {_initData.TotalFloors}");
                return;
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

        // Обработка состояния "Движение вниз"
        private void ProcessMovingDownState()
        {
            // Двигаем лифт вниз
            _elevator.CurrentFloor--;

            // Проверяем нижнюю границу
            if (_elevator.CurrentFloor < 1)
            {
                _elevator.CurrentFloor = 1;
                _elevator.State = ElevatorState.Idle;
                UpdateStatus($"Достигнут нижний этаж 1");
                return;
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
            var peopleToExit = new List<Person>();
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
                person.CurrentFloor = _elevator.CurrentFloor;
                _elevator.PeopleInside.Remove(person);
                _transportedCount++;

                // Учитываем перемещенный вес
                _totalWeightMoved += person.Weight;

                UpdateStatus($"Человек {person.Id} вышел на этаже {_elevator.CurrentFloor} (будет удален через 5 сек)");
            }

            // Посадка людей
            var peopleToEnter = new List<Person>();
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
                    UpdateStatus($"Человек {person.Id} вошел в лифт (вес: {person.Weight} кг) → этаж {person.TargetFloor}");
                }
                else
                {
                    UpdateStatus($"Человек {person.Id} не может войти: перегрузка!");
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

            // Сортируем в зависимости от направления
            if (_elevator.CurrentDirection == Direction.Up ||
                (_elevator.CurrentDirection == Direction.None && _elevator.TargetFloors.Any(f => f > _elevator.CurrentFloor)))
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
                var waitingPeople = new List<Person>();
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
                    Person nearestPerson = null;
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

                // Если в лифте есть люди, добавляем их цели
                foreach (var person in _elevator.PeopleInside)
                {
                    if (!_elevator.TargetFloors.Contains(person.TargetFloor))
                    {
                        _elevator.TargetFloors.Add(person.TargetFloor);
                    }
                }
            }
        }

        // Обработчик кнопки "Создать человека"
        private void CreatePersonButton_Click(object sender, RoutedEventArgs e)
        {
            var createWindow = new CreatePersonWindow(this, _initData.TotalFloors);
            createWindow.Owner = this;
            createWindow.ShowDialog();
        }

        #region Обработчики кнопок
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            _systemStopped = false;
            _systemStartTime = DateTime.Now;
            _simulationTimer.Start();
            _cleanupTimer.Start();
            _statusTimer.Start();
            UpdateStatus("Симуляция запущена");
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = (_elevator.PeopleInside.Count == 0);
            CreatePersonButton.IsEnabled = true;
            UpdateElapsedTime();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем, что лифт пустой
            if (_elevator.PeopleInside.Count > 0)
            {
                MessageBox.Show("Невозможно остановить систему: в лифте находятся люди!",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _systemStopped = true;
            _simulationTimer.Stop();
            _cleanupTimer.Stop();
            _statusTimer.Stop();

            // Создаем окно отчета
            var reportWindow = new ReportWindow(
                _totalTrips,
                _emptyTrips,
                _totalWeightMoved,
                _totalPeopleCreated);

            reportWindow.Owner = this;
            reportWindow.Show();

            // Скрываем текущее окно
            this.Hide();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _systemStopped = true;
            _simulationTimer?.Stop();
            _cleanupTimer?.Stop();
            _statusTimer?.Stop();
        }
        #endregion

        // Метод для начала новой симуляции (вызывается из ReportWindow)
        public void StartNewSimulation()
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
    }
}