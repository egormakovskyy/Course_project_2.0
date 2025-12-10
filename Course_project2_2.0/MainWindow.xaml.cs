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

        // Кнопки вызова на этажах
        private Dictionary<int, Button> _callButtons = new Dictionary<int, Button>();

        // Кнопки этажей в лифте
        private Dictionary<int, Button> _elevatorFloorButtons = new Dictionary<int, Button>();
        private Button _startMovementButton;
        private Border _elevatorPanelContainer;
        private Border _callPanelContainer;
        private Border _overloadIndicator;
        private TextBlock _overloadText;
        private DispatcherTimer _overloadTimer;

        // Визуальные элементы
        private List<Border> _floorVisuals = new List<Border>();
        private List<TextBlock> _floorTexts = new List<TextBlock>();
        private Border _elevatorVisual;
        private TextBlock _elevatorPeopleText;
        private TextBox _logTextBox;
        private ListView _peopleListView;

        // Состояния кнопок
        private bool _waitingForStartButton = false;
        private List<int> _pressedFloorButtons = new List<int>();

        // Конструктор с данными
        public MainWindow(InitializationData initData)
        {
            InitializeComponent();
            _initData = initData;
            _totalPeopleCreated = initData.People?.Count ?? 0;
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
            _elevator.TargetFloors.Clear();

            // Конвертируем людей из InitializationWindow в систему
            if (_initData.People != null)
            {
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

            // Таймер для индикатора перегрузки
            _overloadTimer = new DispatcherTimer();
            _overloadTimer.Interval = TimeSpan.FromSeconds(1);
            _overloadTimer.Tick += OverloadTimer_Tick;
        }

        // Таймер для индикатора перегрузки
        private void OverloadTimer_Tick(object sender, EventArgs e)
        {
            if (_elevator.IsOverloaded)
            {
                ActivateOverloadIndicator();
            }
            else
            {
                DeactivateOverloadIndicator();
            }
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

                // Автоматически нажимаем кнопку вызова на этаже, где появился человек
                if (_callButtons.ContainsKey(person.CurrentFloor))
                {
                    PressCallButton(person.CurrentFloor);

                    // Добавляем этаж в цели лифта
                    if (!_elevator.TargetFloors.Contains(person.CurrentFloor))
                    {
                        _elevator.TargetFloors.Add(person.CurrentFloor);
                        SortTargetFloors();
                    }
                }

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
            _callButtons.Clear();
            _elevatorFloorButtons.Clear();

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) });

            // Левая панель: этажи здания с кнопками вызова
            _callPanelContainer = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1, 1, 1, 1),
                Background = Brushes.WhiteSmoke,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(10, 10, 10, 10),
                Padding = new Thickness(5, 5, 5, 5)
            };

            var callPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var callPanelTitle = new TextBlock
            {
                Text = "Вызов лифта",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };
            callPanel.Children.Add(callPanelTitle);

            for (int floorNum = _initData.TotalFloors; floorNum >= 1; floorNum--)
            {
                var floorContainer = new Border
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    Margin = new Thickness(5, 5, 5, 5),
                    Padding = new Thickness(8, 8, 8, 8),
                    CornerRadius = new CornerRadius(5),
                    Background = Brushes.White,
                    Width = 220 // УДЛИНЕНО для надписи "Этаж 2 [ЛИФТ]"
                };

                var floorGrid = new Grid();
                floorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
                floorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

                var floorText = new TextBlock
                {
                    Text = $"Этаж {floorNum}",
                    FontSize = 14,
                    FontWeight = FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                };

                // Кнопка вызова на этаже
                var callButton = new Button
                {
                    Content = "Вызов",
                    Width = 70, // УДЛИНЕНО
                    Height = 28,
                    Background = new SolidColorBrush(Color.FromRgb(100, 149, 237)), // Приятный голубой - CornflowerBlue
                    Foreground = Brushes.White,
                    BorderBrush = Brushes.RoyalBlue,
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    Tag = floorNum,
                    FontSize = 11,
                    FontWeight = FontWeights.Medium,
                    IsEnabled = false
                };

                Grid.SetColumn(floorText, 0);
                Grid.SetColumn(callButton, 1);
                floorGrid.Children.Add(floorText);
                floorGrid.Children.Add(callButton);

                floorContainer.Child = floorGrid;
                callPanel.Children.Add(floorContainer);

                _floorVisuals.Add(floorContainer);
                _floorTexts.Add(floorText);
                _callButtons[floorNum] = callButton;
            }

            _callPanelContainer.Child = callPanel;
            Grid.SetColumn(_callPanelContainer, 0);
            mainGrid.Children.Add(_callPanelContainer);

            // Центральная панель: кнопки лифта
            _elevatorPanelContainer = new Border
            {
                BorderBrush = Brushes.DarkGray,
                BorderThickness = new Thickness(1, 1, 1, 1),
                Background = Brushes.WhiteSmoke,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(10, 10, 10, 10),
                Padding = new Thickness(10, 10, 10, 10)
            };

            var centerPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var elevatorButtonsTitle = new TextBlock
            {
                Text = "Панель лифта",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };
            centerPanel.Children.Add(elevatorButtonsTitle);

            // Сетка для кнопок этажей (по 3 в ряд)
            int buttonsPerRow = 3;
            int totalRows = (int)Math.Ceiling(_initData.TotalFloors / (double)buttonsPerRow);

            for (int row = 0; row < totalRows; row++)
            {
                var rowPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 2)
                };

                for (int col = 0; col < buttonsPerRow; col++)
                {
                    int floor = _initData.TotalFloors - (row * buttonsPerRow + col);
                    if (floor < 1) break;

                    var button = new Button
                    {
                        Content = $"{floor}",
                        Width = 45, // УДЛИНЕНО
                        Height = 38,
                        Margin = new Thickness(3, 3, 3, 3),
                        Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)), // Серый по умолчанию
                        Foreground = Brushes.Black,
                        BorderBrush = Brushes.Gray,
                        BorderThickness = new Thickness(1, 1, 1, 1),
                        Tag = floor,
                        FontWeight = FontWeights.Bold,
                        FontSize = 13,
                        IsEnabled = false
                    };

                    _elevatorFloorButtons[floor] = button;
                    rowPanel.Children.Add(button);
                }

                if (rowPanel.Children.Count > 0)
                {
                    centerPanel.Children.Add(rowPanel);
                }
            }

            // Панель с кнопкой ХОД и индикатором перегрузки
            var controlPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 15, 0, 5)
            };

            // Индикатор перегрузки
            _overloadIndicator = new Border
            {
                Width = 85,
                Height = 40,
                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)), // Серый (неактивный)
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1, 1, 1, 1),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(0, 0, 10, 0),
                ToolTip = "Индикатор перегрузки"
            };

            var overloadStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _overloadText = new TextBlock
            {
                Text = "ПЕРЕГРУЗКА",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brushes.Gray
            };

            overloadStack.Children.Add(_overloadText);
            _overloadIndicator.Child = overloadStack;
            controlPanel.Children.Add(_overloadIndicator);

            // Кнопка "ХОД"
            _startMovementButton = new Button
            {
                Content = "ХОД",
                Width = 70,
                Height = 40,
                Margin = new Thickness(10, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                Foreground = Brushes.Black,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(2, 2, 2, 2),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                IsEnabled = false
            };
            controlPanel.Children.Add(_startMovementButton);

            centerPanel.Children.Add(controlPanel);
            _elevatorPanelContainer.Child = centerPanel;
            Grid.SetColumn(_elevatorPanelContainer, 1);
            mainGrid.Children.Add(_elevatorPanelContainer);

            // Правая панель: информация
            var rightPanel = new StackPanel
            {
                Margin = new Thickness(10, 10, 10, 10),
                Orientation = Orientation.Vertical
            };

            // Лифт
            var elevatorContainer = new Border
            {
                BorderBrush = Brushes.DarkBlue,
                BorderThickness = new Thickness(2, 2, 2, 2),
                Background = Brushes.LightBlue,
                Padding = new Thickness(15, 15, 15, 15),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 10)
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
                Margin = new Thickness(0, 10, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };

            Grid.SetRow(elevatorTitle, 0);
            Grid.SetRow(_elevatorPeopleText, 1);
            elevatorGrid.Children.Add(elevatorTitle);
            elevatorGrid.Children.Add(_elevatorPeopleText);

            elevatorContainer.Child = elevatorGrid;
            _elevatorVisual = elevatorContainer;
            rightPanel.Children.Add(elevatorContainer);

            // Список людей
            var peopleTitle = new TextBlock
            {
                Text = "Люди в системе:",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 10, 0, 5)
            };
            rightPanel.Children.Add(peopleTitle);

            _peopleListView = new ListView
            {
                Height = 200,
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
            rightPanel.Children.Add(_peopleListView);

            // Лог событий
            var logTitle = new TextBlock
            {
                Text = "Лог событий:",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            rightPanel.Children.Add(logTitle);

            _logTextBox = new TextBox
            {
                Height = 150,
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromRgb(248, 248, 248)),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1, 1, 1, 1)
            };
            rightPanel.Children.Add(_logTextBox);

            Grid.SetColumn(rightPanel, 2);
            mainGrid.Children.Add(rightPanel);
            MainContentGrid.Children.Add(mainGrid);

            UpdateVisuals();
        }

        // Нажать кнопку вызова на этаже
        private void PressCallButton(int floor)
        {
            Dispatcher.Invoke(() =>
            {
                if (_callButtons.ContainsKey(floor))
                {
                    var button = _callButtons[floor];
                    // Приятный голубой цвет при активном вызове - CornflowerBlue
                    button.Background = new SolidColorBrush(Color.FromRgb(100, 149, 237));
                    button.Foreground = Brushes.White;
                    button.BorderBrush = Brushes.RoyalBlue;
                    button.Content = "Вызов ✓";
                }
            });
        }

        // Отжать кнопку вызова на этаже
        private void ReleaseCallButton(int floor)
        {
            Dispatcher.Invoke(() =>
            {
                if (_callButtons.ContainsKey(floor))
                {
                    var button = _callButtons[floor];
                    // Возвращаем исходный цвет
                    button.Background = new SolidColorBrush(Color.FromRgb(100, 149, 237)); // Остается голубым
                    button.Foreground = Brushes.White;
                    button.BorderBrush = Brushes.RoyalBlue;
                    button.Content = "Вызов";
                }
            });
        }

        // Нажать кнопку этажа в лифте
        private void PressElevatorFloorButton(int floor)
        {
            Dispatcher.Invoke(() =>
            {
                if (_elevatorFloorButtons.ContainsKey(floor))
                {
                    var button = _elevatorFloorButtons[floor];
                    // Приятный красный цвет при нажатии
                    button.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Красный
                    button.Foreground = Brushes.White;
                    button.BorderBrush = Brushes.DarkRed;
                    button.BorderThickness = new Thickness(2, 2, 2, 2);

                    if (!_pressedFloorButtons.Contains(floor))
                    {
                        _pressedFloorButtons.Add(floor);
                    }
                }
            });
        }

        // Отжать кнопку этажа в лифте
        private void ReleaseElevatorFloorButton(int floor)
        {
            Dispatcher.Invoke(() =>
            {
                if (_elevatorFloorButtons.ContainsKey(floor))
                {
                    var button = _elevatorFloorButtons[floor];
                    button.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)); // Светло-серый
                    button.Foreground = Brushes.Black;
                    button.BorderBrush = Brushes.Gray;
                    button.BorderThickness = new Thickness(1, 1, 1, 1);

                    _pressedFloorButtons.Remove(floor);
                }
            });
        }

        // Нажать кнопку ХОД
        private void PressStartMovementButton()
        {
            Dispatcher.Invoke(() =>
            {
                _startMovementButton.Background = new SolidColorBrush(Color.FromRgb(40, 167, 69)); // Зеленый
                _startMovementButton.Foreground = Brushes.White;
                _startMovementButton.BorderBrush = Brushes.DarkGreen;
                _waitingForStartButton = false;
            });
        }

        // Отжать кнопку ХОД
        private void ReleaseStartMovementButton()
        {
            Dispatcher.Invoke(() =>
            {
                _startMovementButton.Background = new SolidColorBrush(Color.FromRgb(220, 220, 220));
                _startMovementButton.Foreground = Brushes.Black;
                _startMovementButton.BorderBrush = Brushes.Gray;
            });
        }

        // Активировать индикатор перегрузки
        private void ActivateOverloadIndicator()
        {
            Dispatcher.Invoke(() =>
            {
                _overloadIndicator.Background = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Оранжевый
                _overloadIndicator.BorderBrush = Brushes.Orange;
                _overloadText.Foreground = Brushes.Black;
                _overloadText.FontWeight = FontWeights.Bold;
            });
        }

        // Деактивировать индикатор перегрузки
        private void DeactivateOverloadIndicator()
        {
            Dispatcher.Invoke(() =>
            {
                _overloadIndicator.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)); // Серый
                _overloadIndicator.BorderBrush = Brushes.Gray;
                _overloadText.Foreground = Brushes.Gray;
            });
        }

        // Сортировка целей в зависимости от направления
        private void SortTargetFloors()
        {
            if (_elevator.TargetFloors.Count == 0) return;

            // Сортируем в зависимости от направления
            if (_elevator.CurrentDirection == Direction.Up ||
                (_elevator.CurrentDirection == Direction.None &&
                 _elevator.TargetFloors.Any(f => f > _elevator.CurrentFloor)))
            {
                _elevator.TargetFloors.Sort();
            }
            else if (_elevator.CurrentDirection == Direction.Down ||
                     (_elevator.CurrentDirection == Direction.None &&
                      _elevator.TargetFloors.Any(f => f < _elevator.CurrentFloor)))
            {
                _elevator.TargetFloors.Sort((a, b) => b.CompareTo(a));
            }
        }

        // Обновление визуализации
        private void UpdateVisuals()
        {
            Dispatcher.Invoke(() =>
            {
                // Обновляем этажи
                for (int i = 0; i < _initData.TotalFloors; i++)
                {
                    int floorNumber = _initData.TotalFloors - i;
                    var floorContainer = _floorVisuals[i];
                    var floorText = _floorTexts[i];

                    // Подсвечиваем текущий этаж лифта
                    if (floorNumber == _elevator.CurrentFloor)
                    {
                        floorContainer.Background = new SolidColorBrush(Color.FromRgb(220, 255, 220)); // Светло-зеленый
                        floorText.FontWeight = FontWeights.Bold;
                        floorText.Text = $"Этаж {floorNumber} [ЛИФТ]";
                        floorText.Foreground = Brushes.DarkGreen;
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
                        stateText = $"Движение вверх → этаж {_elevator.TargetFloors.FirstOrDefault()}";
                        break;
                    case ElevatorState.MovingDown:
                        stateText = $"Движение вниз → этаж {_elevator.TargetFloors.FirstOrDefault()}";
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

                _elevatorVisual.Background = _elevator.IsOverloaded ?
                    new SolidColorBrush(Color.FromRgb(255, 220, 220)) : // Светло-красный при перегрузке
                    new SolidColorBrush(Color.FromRgb(173, 216, 230)); // Светло-голубой

                // Обновляем индикатор перегрузки в реальном времени
                if (_elevator.IsOverloaded)
                {
                    ActivateOverloadIndicator();
                }
                else
                {
                    DeactivateOverloadIndicator();
                }

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
            });
        }

        // Обновление информационной панели
        private void UpdateInfoPanel()
        {
            Dispatcher.Invoke(() =>
            {
                CurrentFloorText.Text = _elevator.CurrentFloor.ToString();
                ElevatorStateText.Text = _elevator.State.ToString();
                WeightText.Text = $"{_elevator.CurrentWeight:F1} кг";
                TransportedCountText.Text = _transportedCount.ToString();

                // Кнопка Стоп доступна только когда лифт пустой
                StopButton.IsEnabled = (_elevator.PeopleInside.Count == 0);

                // Визуальное оформление кнопки Стоп
                if (StopButton.IsEnabled)
                {
                    StopButton.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Красный
                    StopButton.Foreground = Brushes.White;
                    StopButton.ToolTip = "Остановить симуляцию";
                }
                else
                {
                    StopButton.Background = new SolidColorBrush(Color.FromRgb(200, 200, 200)); // Серый
                    StopButton.Foreground = Brushes.Gray;
                    StopButton.ToolTip = "Невозможно остановить: в лифте есть люди";
                }
            });
        }

        // Обновление статуса
        private void UpdateStatus(string message)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"Статус: {message}";
                AddLog(message);
            });
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
                        string timestamp = DateTime.Now.ToString("HH:mm:ss");
                        _logTextBox.AppendText($"[{timestamp}] {message}\n");
                        _logTextBox.ScrollToEnd();

                        // Ограничиваем количество строк в логе (последние 100)
                        var lines = _logTextBox.Text.Split('\n');
                        if (lines.Length > 100)
                        {
                            _logTextBox.Text = string.Join("\n", lines.Skip(lines.Length - 100));
                        }
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

            try
            {
                // 1. Автоматически нажимаем кнопки вызова для ожидающих людей
                foreach (var person in _allPeople.Where(p => p.State == PersonState.Waiting))
                {
                    if (!_elevator.TargetFloors.Contains(person.CurrentFloor))
                    {
                        PressCallButton(person.CurrentFloor);
                        _elevator.TargetFloors.Add(person.CurrentFloor);
                        SortTargetFloors();
                    }
                }

                // 2. Обновляем текущий этаж для людей в лифте
                foreach (var person in _elevator.PeopleInside)
                {
                    person.CurrentFloor = _elevator.CurrentFloor;
                }

                // 3. Обрабатываем текущее состояние лифта
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
            catch (Exception ex)
            {
                AddLog($"Ошибка в симуляции: {ex.Message}");
            }
        }

        // Обработка состояния "Ожидание"
        private void ProcessIdleState()
        {
            // Если есть цели - начинаем движение
            if (_elevator.TargetFloors.Count > 0)
            {
                int nextTarget = _elevator.TargetFloors[0];

                // Проверяем, не находимся ли уже на целевом этаже
                if (nextTarget == _elevator.CurrentFloor)
                {
                    // Если уже на целевом этаже, открываем двери
                    _elevator.State = ElevatorState.DoorsOpen;
                    UpdateStatus($"Лифт прибыл на этаж {nextTarget}. Двери открываются.");
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
            }
            // УБРАНО: постоянное сообщение об ожидании команд
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

            // Проверяем, достигли ли цели
            if (_elevator.TargetFloors.Contains(_elevator.CurrentFloor))
            {
                _elevator.State = ElevatorState.DoorsOpen;
                UpdateStatus($"Лифт прибыл на этаж {_elevator.CurrentFloor}. Двери открываются.");
            }
            else
            {
                // Не засоряем лог сообщениями о каждом этаже
                if (_currentTime % 2 == 0) // Каждые 2 секунды
                    UpdateStatus($"Лифт на этаже {_elevator.CurrentFloor}");
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

            // Проверяем, достигли ли цели
            if (_elevator.TargetFloors.Contains(_elevator.CurrentFloor))
            {
                _elevator.State = ElevatorState.DoorsOpen;
                UpdateStatus($"Лифт прибыл на этаж {_elevator.CurrentFloor}. Двери открываются.");
            }
            else
            {
                // Не засоряем лог сообщениями о каждом этаже
                if (_currentTime % 2 == 0) // Каждые 2 секунды
                    UpdateStatus($"Лифт на этаже {_elevator.CurrentFloor}");
            }
        }

        // Обработка состояния "Двери открыты"
        private void ProcessDoorsOpenState()
        {
            // Шаг 1: Высадка людей
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

                UpdateStatus($"Человек {person.Id} вышел на этаже {_elevator.CurrentFloor}");

                // Отжимаем кнопку этажа в лифте
                ReleaseElevatorFloorButton(_elevator.CurrentFloor);
            }

            // Шаг 2: Снимаем вызов на этом этаже
            ReleaseCallButton(_elevator.CurrentFloor);
            _elevator.TargetFloors.Remove(_elevator.CurrentFloor);

            // Шаг 3: Посадка людей
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

            bool peopleEntered = false;
            foreach (var person in peopleToEnter)
            {
                // Проверяем вес
                if (_elevator.CurrentWeight + person.Weight <= 400)
                {
                    person.State = PersonState.InElevator;
                    _elevator.PeopleInside.Add(person);
                    peopleEntered = true;

                    // 1. Автоматически нажимаем кнопку целевого этажа в лифте
                    PressElevatorFloorButton(person.TargetFloor);

                    // 2. Добавляем этаж в цели, если его там нет
                    if (!_elevator.TargetFloors.Contains(person.TargetFloor))
                    {
                        _elevator.TargetFloors.Add(person.TargetFloor);
                        SortTargetFloors();
                    }

                    UpdateStatus($"Человек {person.Id} вошел в лифт (вес: {person.Weight} кг) → нажата кнопка этажа {person.TargetFloor}");
                }
                else
                {
                    // При попытке войти при перегрузке
                    UpdateStatus($"Человек {person.Id} не может войти: перегрузка!");

                    // Активируем индикатор перегрузки на 1 секунду
                    ActivateOverloadIndicator();
                    DispatcherTimer overloadTimer = new DispatcherTimer();
                    overloadTimer.Interval = TimeSpan.FromSeconds(1);
                    overloadTimer.Tick += (s, args) =>
                    {
                        overloadTimer.Stop();
                        if (!_elevator.IsOverloaded)
                        {
                            DeactivateOverloadIndicator();
                        }
                    };
                    overloadTimer.Start();
                }
            }

            // Шаг 4: Если люди вошли в лифт, запускаем таймер для кнопки ХОД
            if (peopleEntered && _elevator.PeopleInside.Count > 0 && _pressedFloorButtons.Count > 0)
            {
                // Ждем 1 секунду перед "нажатием" кнопки ХОД
                DispatcherTimer startTimer = new DispatcherTimer();
                startTimer.Interval = TimeSpan.FromSeconds(1);
                startTimer.Tick += (s, args) =>
                {
                    startTimer.Stop();

                    // Нажимаем кнопку ХОД
                    PressStartMovementButton();
                    UpdateStatus("Кнопка 'ХОД' нажата - начинаем движение");

                    // Еще через 1 секунду отжимаем и начинаем движение
                    DispatcherTimer moveTimer = new DispatcherTimer();
                    moveTimer.Interval = TimeSpan.FromSeconds(1);
                    moveTimer.Tick += (s2, args2) =>
                    {
                        moveTimer.Stop();

                        // Отжимаем кнопку ХОД
                        ReleaseStartMovementButton();

                        // Закрываем двери и начинаем движение
                        _elevator.State = ElevatorState.Idle;
                        UpdateStatus("Двери закрываются. Начинаем движение.");
                    };
                    moveTimer.Start();
                };
                startTimer.Start();
            }
            else
            {
                // Если никто не вошел в лифт, просто закрываем двери
                _elevator.State = ElevatorState.Idle;
                UpdateStatus("Двери закрываются. Ожидание новых вызовов.");
            }

            // Проверяем перегрузку
            if (_elevator.IsOverloaded)
            {
                _elevator.State = ElevatorState.Overloaded;
                UpdateStatus("ПЕРЕГРУЗКА! Лифт не может двигаться.");
            }
        }

        // Обработка состояния "Перегрузка"
        private void ProcessOverloadedState()
        {
            UpdateStatus("Лифт перегружен. Требуется освободить место.");

            // Активируем индикатор перегрузки
            ActivateOverloadIndicator();

            // Ждем 1 секунду (как в реальной жизни)
            DispatcherTimer overloadTimer = new DispatcherTimer();
            overloadTimer.Interval = TimeSpan.FromSeconds(1);
            overloadTimer.Tick += (s, args) =>
            {
                overloadTimer.Stop();
                if (!_elevator.IsOverloaded)
                {
                    _elevator.State = ElevatorState.Idle;
                    DeactivateOverloadIndicator();
                    UpdateStatus("Перегрузка устранена. Готов к работе.");
                }
                else
                {
                    // Если все еще перегрузка, продолжаем ожидание
                    ProcessOverloadedState();
                }
            };
            overloadTimer.Start();
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
            _overloadTimer.Start();
            UpdateStatus("Симуляция запущена");
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = (_elevator.PeopleInside.Count == 0);
            CreatePersonButton.IsEnabled = true;
            UpdateElapsedTime();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем, что лифт пустой (единственное ограничение)
            if (_elevator.PeopleInside.Count > 0)
            {
                MessageBox.Show("Невозможно остановить систему: в лифте находятся люди!",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Подтверждение остановки
            var result = MessageBox.Show("Вы уверены, что хотите остановить симуляцию?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            _systemStopped = true;
            _simulationTimer.Stop();
            _cleanupTimer.Stop();
            _statusTimer.Stop();
            _overloadTimer.Stop();

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
            _overloadTimer?.Stop();
        }
        #endregion

        // Метод для начала новой симуляции (вызывается из ReportWindow)
        public void StartNewSimulation()
        {
            var initWindow = new InitializationWindow();
            initWindow.Show();
            this.Close();
        }
    }
}