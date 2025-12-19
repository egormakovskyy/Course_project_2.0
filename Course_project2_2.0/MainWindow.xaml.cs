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
        // Класс данных инициализации
        public class InitializationData
        {
            public int TotalFloors { get; set; }
            public int StartFloor { get; set; }
            public ObservableCollection<InitializationWindow.Person> People { get; set; }
        }

        // Приватные поля
        private InitializationData _initData;
        private ObservableCollection<Person> _allPeople = new ObservableCollection<Person>();
        private Elevator _elevator = new Elevator();
        private DispatcherTimer _simulationTimer;
        private DispatcherTimer _cleanupTimer;
        private DispatcherTimer _statusTimer;
        private DispatcherTimer _overloadTimer;
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

        // Элементы управления
        private Dictionary<int, Button> _callButtons = new Dictionary<int, Button>();
        private Dictionary<int, Button> _elevatorFloorButtons = new Dictionary<int, Button>();
        private Button _startMovementButton;
        private Border _elevatorPanelContainer;
        private Border _callPanelContainer;
        private TextBox _logTextBox;
        private ListView _peopleListView;

        // Визуальные элементы
        private List<Border> _floorVisuals = new List<Border>();
        private List<TextBlock> _floorTexts = new List<TextBlock>();
        private Border _elevatorVisual;
        private TextBlock _elevatorPeopleText;

        // Состояния
        private bool _waitingForStartButton = false;
        private List<int> _pressedFloorButtons = new List<int>();
        private bool _isProcessingOverload = false;
        private Person _lastEnteredPerson = null;

        // Флаги для индикатора перегрузки
        private bool _showOverloadIndicator = false;
        private DateTime _overloadStartTime;
        private DispatcherTimer _overloadIndicatorTimer;

        // Защита от зацикливания и задержка вызова
        private int _lastOverloadFloor = -1;
        private DateTime _lastOverloadTime;
        private bool _isOverloadCooldown = false;
        private DispatcherTimer _overloadCooldownTimer;

        // Задержка для новых людей (5 секунд)
        private Dictionary<int, DateTime> _personCreationTimes = new Dictionary<int, DateTime>();

        // Флаг для предотвращения повторного сообщения о стоящем лифте
        private bool _idleMessageShown = false;

        // Таймер для кнопки ХОД
        private DispatcherTimer _startButtonTimer;

        // Конструктор
        public MainWindow(InitializationData initData)
        {
            InitializeComponent();
            _initData = initData;
            _totalPeopleCreated = initData.People?.Count ?? 0;
            InitializeSystem();
            CreateVisualInterface();
            InitializeTimers();
            SetupEventHandlers();
        }

        // Инициализация системы
        private void InitializeSystem()
        {
            _elevator.CurrentFloor = _initData.StartFloor;
            _elevator.State = ElevatorState.Idle;
            _elevator.CurrentDirection = Direction.None;
            _elevator.TargetFloors.Clear();

            // Конвертируем людей из InitializationWindow
            if (_initData.People != null)
            {
                foreach (var person in _initData.People)
                {
                    var newPerson = new Person
                    {
                        Id = _nextPersonId++,
                        Weight = person.Weight,
                        CurrentFloor = person.CurrentFloor,
                        StartFloor = person.CurrentFloor,
                        TargetFloor = person.TargetFloor,
                        State = PersonState.Waiting
                    };

                    _allPeople.Add(newPerson);
                    // Запоминаем время создания
                    _personCreationTimes[newPerson.Id] = DateTime.Now;
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

            _statusTimer = new DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(1);
            _statusTimer.Tick += StatusTimer_Tick;

            _overloadTimer = new DispatcherTimer();
            _overloadTimer.Interval = TimeSpan.FromSeconds(1);
            _overloadTimer.Tick += OverloadTimer_Tick;

            // Таймер для индикатора перегрузки
            _overloadIndicatorTimer = new DispatcherTimer();
            _overloadIndicatorTimer.Interval = TimeSpan.FromMilliseconds(100);
            _overloadIndicatorTimer.Tick += OverloadIndicatorTimer_Tick;

            // Таймер для защиты от зацикливания
            _overloadCooldownTimer = new DispatcherTimer();
            _overloadCooldownTimer.Interval = TimeSpan.FromSeconds(3);
            _overloadCooldownTimer.Tick += OverloadCooldownTimer_Tick;

            // Таймер для кнопки ХОД
            _startButtonTimer = new DispatcherTimer();
            _startButtonTimer.Interval = TimeSpan.FromSeconds(1);
            _startButtonTimer.Tick += StartButtonTimer_Tick;
        }

        // Настройка обработчиков событий
        private void SetupEventHandlers()
        {
            _elevator.PropertyChanged += Elevator_PropertyChanged;
        }

        // Обработчик изменения свойств лифта
        private void Elevator_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_elevator.IsOverloaded))
            {
                if (_elevator.IsOverloaded && !_showOverloadIndicator)
                {
                    // Запускаем индикатор перегрузки
                    _showOverloadIndicator = true;
                    _overloadStartTime = DateTime.Now;
                    _overloadIndicatorTimer.Start();
                    UpdateOverloadIndicator();
                    UpdateVisuals();
                }
                else if (!_elevator.IsOverloaded && _showOverloadIndicator)
                {
                    // Перегрузка устранена
                    // Индикатор будет гореть еще 1 секунду
                }
            }
            else if (e.PropertyName == nameof(_elevator.CurrentWeight))
            {
                UpdateInfoPanel();
            }
        }

        // Таймер для кнопки ХОД
        private void StartButtonTimer_Tick(object sender, EventArgs e)
        {
            _startButtonTimer.Stop();

            // Отжимаем кнопку ХОД
            ReleaseStartMovementButton();

            // Начинаем движение лифта
            _elevator.State = ElevatorState.Idle;
            UpdateStatus("Двери закрываются. Начинаем движение.");
            _idleMessageShown = false; // Сбрасываем флаг
        }

        // Таймер для защиты от зацикливания
        private void OverloadCooldownTimer_Tick(object sender, EventArgs e)
        {
            _overloadCooldownTimer.Stop();
            _isOverloadCooldown = false;
        }

        // Таймер для управления индикатора перегрузки
        private void OverloadIndicatorTimer_Tick(object sender, EventArgs e)
        {
            if (_showOverloadIndicator)
            {
                // Проверяем, прошла ли 1 секунда с момента начала перегрузки
                TimeSpan elapsed = DateTime.Now - _overloadStartTime;
                if (elapsed.TotalSeconds >= 1.0 && !_elevator.IsOverloaded)
                {
                    // Прошла 1 секунда и перегрузки больше нет - выключаем индикатор
                    _showOverloadIndicator = false;
                    _overloadIndicatorTimer.Stop();
                }

                UpdateOverloadIndicator();
                UpdateVisuals();
            }
        }

        // Таймер обработки перегрузки
        private void OverloadTimer_Tick(object sender, EventArgs e)
        {
            if (!_isProcessingOverload) return;

            _overloadTimer.Stop();

            // Проверяем, устранена ли перегрузка
            if (!_elevator.IsOverloaded)
            {
                _isProcessingOverload = false;
                UpdateStatus("Перегрузка устранена.");

                // Включаем защиту от зацикливания на этом этаже
                ActivateOverloadProtection();

                _simulationTimer.Start();
            }
            else
            {
                // Если перегрузка все еще есть, ждем еще секунду
                UpdateStatus($"Перегрузка! Вес: {_elevator.CurrentWeight:F1} кг. Ожидание выхода людей...");
                _overloadTimer.Start();
            }
        }

        // Включение защиты от зацикливания
        private void ActivateOverloadProtection()
        {
            _lastOverloadFloor = _elevator.CurrentFloor;
            _lastOverloadTime = DateTime.Now;
            _isOverloadCooldown = true;
            _overloadCooldownTimer.Start();
        }

        // Проверка, разрешен ли вызов с этажа (с учетом задержки 5 секунд для новых людей)
        private bool IsCallAllowedFromFloor(int floor, int personId = -1)
        {
            // Проверка защиты от зацикливания
            if (_isOverloadCooldown && floor == _lastOverloadFloor)
            {
                TimeSpan elapsed = DateTime.Now - _lastOverloadTime;
                if (elapsed.TotalSeconds < 3)
                {
                    return false;
                }
                else
                {
                    _isOverloadCooldown = false;
                    _overloadCooldownTimer.Stop();
                }
            }

            // Проверка задержки 5 секунд для новых людей
            if (personId > 0 && _personCreationTimes.ContainsKey(personId))
            {
                TimeSpan elapsed = DateTime.Now - _personCreationTimes[personId];
                if (elapsed.TotalSeconds < 5)
                {
                    return false;
                }
            }

            return true;
        }

        // Обновление индикатора перегрузки
        private void UpdateOverloadIndicator()
        {
            Dispatcher.Invoke(() =>
            {
                if (_showOverloadIndicator || _elevator.State == ElevatorState.Overloaded)
                {
                    OverloadIndicator.Background = Brushes.Red;
                    OverloadIndicator.BorderBrush = Brushes.DarkRed;
                    // УБИРАЕМ надпись "ДА" - оставляем только индикатор
                    OverloadText.Text = "";
                    OverloadText.Foreground = Brushes.Red;
                    OverloadText.FontWeight = FontWeights.Bold;
                }
                else
                {
                    OverloadIndicator.Background = Brushes.LightGray;
                    OverloadIndicator.BorderBrush = Brushes.DarkGray;
                    // УБИРАЕМ надпись "Нет"
                    OverloadText.Text = "";
                    OverloadText.Foreground = Brushes.Black;
                    OverloadText.FontWeight = FontWeights.Normal;
                }
            });
        }

        // Создание визуального интерфейса
        private void CreateVisualInterface()
        {
            MainContentGrid.Children.Clear();
            _floorVisuals.Clear();
            _floorTexts.Clear();
            _callButtons.Clear();
            _elevatorFloorButtons.Clear();

            // Создаем основную сетку с 3 колонками
            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) });

            // Левая панель: вызов лифта - ЗАНИМАЕТ ВСЮ ВЫСОТУ
            _callPanelContainer = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1, 1, 1, 1),
                Background = Brushes.WhiteSmoke,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(10, 10, 5, 10),
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // Grid для организации содержимого панели вызова
            var callGrid = new Grid();
            callGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            callGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var callPanelTitle = new TextBlock
            {
                Text = "Вызов лифта",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10)
            };
            Grid.SetRow(callPanelTitle, 0);
            callGrid.Children.Add(callPanelTitle);

            // ScrollViewer для прокрутки этажей
            var callScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(5, 0, 5, 10)
            };
            Grid.SetRow(callScrollViewer, 1);

            var callPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            for (int floorNum = _initData.TotalFloors; floorNum >= 1; floorNum--)
            {
                var floorContainer = new Border
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    Margin = new Thickness(5, 3, 5, 3),
                    Padding = new Thickness(8, 6, 8, 6),
                    CornerRadius = new CornerRadius(5),
                    Background = Brushes.White,
                    Width = 220,
                    Height = 40
                };

                var floorGrid = new Grid();
                floorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
                floorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

                var floorText = new TextBlock
                {
                    Text = $"Этаж {floorNum}",
                    FontSize = 13,
                    FontWeight = FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var callButton = new Button
                {
                    Content = "Вызов",
                    Width = 70,
                    Height = 26,
                    Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                    Foreground = Brushes.Black,
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    Tag = floorNum,
                    FontSize = 11,
                    FontWeight = FontWeights.Medium,
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

            callScrollViewer.Content = callPanel;
            callGrid.Children.Add(callScrollViewer);
            _callPanelContainer.Child = callGrid;
            Grid.SetColumn(_callPanelContainer, 0);
            mainGrid.Children.Add(_callPanelContainer);

            // Центральная панель: управление лифтом - ЗАНИМАЕТ ВСЮ ВЫСОТУ
            _elevatorPanelContainer = new Border
            {
                BorderBrush = Brushes.DarkGray,
                BorderThickness = new Thickness(1, 1, 1, 1),
                Background = Brushes.WhiteSmoke,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(5, 10, 5, 10),
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // Grid для организации содержимого панели лифта
            var elevatorGridContainer = new Grid();
            elevatorGridContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            elevatorGridContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            elevatorGridContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var elevatorButtonsTitle = new TextBlock
            {
                Text = "Панель лифта",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 15)
            };
            Grid.SetRow(elevatorButtonsTitle, 0);
            elevatorGridContainer.Children.Add(elevatorButtonsTitle);

            // ScrollViewer для кнопок этажей лифта
            var elevatorScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(5)
            };
            Grid.SetRow(elevatorScrollViewer, 1);

            var buttonsContainer = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Кнопки этажей (по 4 в ряд для лучшего заполнения)
            int buttonsPerRow = 4;
            int totalRows = (int)Math.Ceiling(_initData.TotalFloors / (double)buttonsPerRow);

            for (int row = 0; row < totalRows; row++)
            {
                var rowPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 3, 0, 3)
                };

                for (int col = 0; col < buttonsPerRow; col++)
                {
                    int floor = _initData.TotalFloors - (row * buttonsPerRow + col);
                    if (floor < 1) break;

                    var button = new Button
                    {
                        Content = $"{floor}",
                        Width = 40,
                        Height = 35,
                        Margin = new Thickness(3, 3, 3, 3),
                        Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                        Foreground = Brushes.Black,
                        BorderBrush = Brushes.Gray,
                        BorderThickness = new Thickness(1, 1, 1, 1),
                        Tag = floor,
                        FontWeight = FontWeights.Bold,
                        FontSize = 13,
                    };

                    _elevatorFloorButtons[floor] = button;
                    rowPanel.Children.Add(button);
                }

                if (rowPanel.Children.Count > 0)
                {
                    buttonsContainer.Children.Add(rowPanel);
                }
            }

            elevatorScrollViewer.Content = buttonsContainer;
            elevatorGridContainer.Children.Add(elevatorScrollViewer);

            // Кнопка "ХОД" - ВНИЗУ ПАНЕЛИ
            var controlPanel = new Border
            {
                Margin = new Thickness(0, 15, 0, 15),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(controlPanel, 2);

            _startMovementButton = new Button
            {
                Content = "ХОД",
                Width = 70,
                Height = 40,
                Margin = new Thickness(0),
                Background = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                Foreground = Brushes.Black,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(2, 2, 2, 2),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            controlPanel.Child = _startMovementButton;
            elevatorGridContainer.Children.Add(controlPanel);

            _elevatorPanelContainer.Child = elevatorGridContainer;
            Grid.SetColumn(_elevatorPanelContainer, 1);
            mainGrid.Children.Add(_elevatorPanelContainer);

            // Правая панель: информация и логи - ЗАНИМАЕТ ВСЮ ВЫСОТУ
            var rightPanel = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1, 1, 1, 1),
                Background = Brushes.WhiteSmoke,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(5, 10, 10, 10),
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // Grid для правой панели - Теперь 4 равные строки
            var rightPanelGrid = new Grid();
            rightPanelGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Лифт
            rightPanelGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Заголовок "Люди"
            rightPanelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Список людей (50%)
            rightPanelGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Заголовок "Лог"
            rightPanelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Лог событий (50%)

            // Лифт
            _elevatorVisual = new Border
            {
                BorderBrush = Brushes.DarkBlue,
                BorderThickness = new Thickness(2, 2, 2, 2),
                Background = Brushes.LightBlue,
                Padding = new Thickness(15, 15, 15, 15),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(10, 10, 10, 10)
            };
            Grid.SetRow(_elevatorVisual, 0);

            var elevatorInfoGrid = new Grid();
            elevatorInfoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            elevatorInfoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

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
            elevatorInfoGrid.Children.Add(elevatorTitle);
            elevatorInfoGrid.Children.Add(_elevatorPeopleText);

            _elevatorVisual.Child = elevatorInfoGrid;
            rightPanelGrid.Children.Add(_elevatorVisual);

            // Заголовок "Люди в системе"
            var peopleTitle = new TextBlock
            {
                Text = "Люди в системе:",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(10, 5, 10, 5)
            };
            Grid.SetRow(peopleTitle, 1);
            rightPanelGrid.Children.Add(peopleTitle);

            // ScrollViewer для списка людей
            var peopleScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(10, 0, 10, 10)
            };
            Grid.SetRow(peopleScrollViewer, 2);

            _peopleListView = new ListView
            {
                VerticalAlignment = VerticalAlignment.Stretch,
                MinHeight = 150 // Минимальная высота
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
            peopleScrollViewer.Content = _peopleListView;
            rightPanelGrid.Children.Add(peopleScrollViewer);

            // Заголовок "Лог событий"
            var logTitle = new TextBlock
            {
                Text = "Лог событий:",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(10, 5, 10, 5)
            };
            Grid.SetRow(logTitle, 3);
            rightPanelGrid.Children.Add(logTitle);

            // Лог событий - ScrollViewer для прокрутки
            var logScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(10, 0, 10, 10)
            };
            Grid.SetRow(logScrollViewer, 4);

            _logTextBox = new TextBox
            {
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromRgb(248, 248, 248)),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1, 1, 1, 1),
                VerticalAlignment = VerticalAlignment.Stretch,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 150 // Минимальная высота
            };

            logScrollViewer.Content = _logTextBox;
            rightPanelGrid.Children.Add(logScrollViewer);

            rightPanel.Child = rightPanelGrid;
            Grid.SetColumn(rightPanel, 2);
            mainGrid.Children.Add(rightPanel);

            // Добавляем основную сетку в MainContentGrid
            MainContentGrid.Children.Add(mainGrid);

            UpdateVisuals();
        }

        // Вспомогательные методы для кнопок
        private void PressCallButton(int floor)
        {
            Dispatcher.Invoke(() =>
            {
                if (_callButtons.ContainsKey(floor))
                {
                    var button = _callButtons[floor];
                    button.Background = new SolidColorBrush(Color.FromRgb(100, 149, 237));
                    button.Foreground = Brushes.White;
                    button.BorderBrush = Brushes.RoyalBlue;
                }
            });
        }

        private void ReleaseCallButton(int floor)
        {
            Dispatcher.Invoke(() =>
            {
                if (_callButtons.ContainsKey(floor))
                {
                    var button = _callButtons[floor];
                    button.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                    button.Foreground = Brushes.Black;
                    button.BorderBrush = Brushes.Gray;
                }
            });
        }

        private void PressElevatorFloorButton(int floor)
        {
            Dispatcher.Invoke(() =>
            {
                if (_elevatorFloorButtons.ContainsKey(floor))
                {
                    var button = _elevatorFloorButtons[floor];
                    button.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69));
                    button.Foreground = Brushes.White;
                    button.BorderBrush = Brushes.Red;
                    button.BorderThickness = new Thickness(2, 2, 2, 2);

                    if (!_pressedFloorButtons.Contains(floor))
                        _pressedFloorButtons.Add(floor);
                }
            });
        }

        private void ReleaseElevatorFloorButton(int floor)
        {
            Dispatcher.Invoke(() =>
            {
                if (_elevatorFloorButtons.ContainsKey(floor))
                {
                    var button = _elevatorFloorButtons[floor];
                    button.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                    button.Foreground = Brushes.Black;
                    button.BorderBrush = Brushes.Gray;
                    button.BorderThickness = new Thickness(1, 1, 1, 1);
                    _pressedFloorButtons.Remove(floor);
                }
            });
        }

        // НАЖАТИЕ кнопки ХОД (красный цвет)
        private void PressStartMovementButton()
        {
            Dispatcher.Invoke(() =>
            {
                _startMovementButton.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Красный
                _startMovementButton.Foreground = Brushes.White;
                _startMovementButton.BorderBrush = Brushes.DarkRed;
                _waitingForStartButton = false;
            });
        }

        // ОТЖАТИЕ кнопки ХОД (возврат к исходному состоянию)
        private void ReleaseStartMovementButton()
        {
            Dispatcher.Invoke(() =>
            {
                _startMovementButton.Background = new SolidColorBrush(Color.FromRgb(220, 220, 220)); // Серый
                _startMovementButton.Foreground = Brushes.Black;
                _startMovementButton.BorderBrush = Brushes.Gray;
            });
        }

        // Сортировка целей
        private void SortTargetFloors()
        {
            if (_elevator.TargetFloors.Count == 0) return;

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

                    if (floorNumber == _elevator.CurrentFloor)
                    {
                        floorContainer.Background = new SolidColorBrush(Color.FromRgb(220, 255, 220));
                        floorText.FontWeight = FontWeights.Bold;

                        // Отображаем перегрузку - теперь просто "!" вместо "ПЕРЕГРУЗКА!"
                        if (_showOverloadIndicator || _elevator.State == ElevatorState.Overloaded)
                        {
                            floorText.Text = $"Этаж {floorNumber} [!]";
                            floorText.Foreground = Brushes.Red;
                        }
                        else
                        {
                            floorText.Text = $"Этаж {floorNumber} [ЛИФТ]";
                            floorText.Foreground = Brushes.DarkGreen;
                        }
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
                Color backgroundColor;

                if (_showOverloadIndicator || _elevator.State == ElevatorState.Overloaded)
                {
                    stateText = "ПЕРЕГРУЗКА!";
                    backgroundColor = Color.FromRgb(255, 182, 193);
                }
                else
                {
                    switch (_elevator.State)
                    {
                        case ElevatorState.Idle:
                            stateText = "Стоит с закрытыми дверьми";
                            backgroundColor = Color.FromRgb(173, 216, 230);
                            break;
                        case ElevatorState.MovingUp:
                            stateText = $"Движение вверх → этаж {_elevator.TargetFloors.FirstOrDefault()}";
                            backgroundColor = Color.FromRgb(144, 238, 144);
                            break;
                        case ElevatorState.MovingDown:
                            stateText = $"Движение вниз → этаж {_elevator.TargetFloors.FirstOrDefault()}";
                            backgroundColor = Color.FromRgb(144, 238, 144);
                            break;
                        case ElevatorState.DoorsOpen:
                            stateText = "Стоит с открытыми дверьми";
                            backgroundColor = Color.FromRgb(255, 228, 181);
                            break;
                        default:
                            stateText = "Неизвестно";
                            backgroundColor = Color.FromRgb(173, 216, 230);
                            break;
                    }
                }

                _elevatorVisual.Background = new SolidColorBrush(backgroundColor);

                if (_elevator.PeopleInside.Count > 0)
                {
                    var ids = _elevator.PeopleInside.Select(p => p.Id.ToString());
                    string overloadStatus = (_showOverloadIndicator || _elevator.State == ElevatorState.Overloaded) ?
                        $"ПЕРЕГРУЗКА! Вес: {_elevator.CurrentWeight:F1} кг\n" :
                        $"Вес: {_elevator.CurrentWeight:F1} кг\n";

                    _elevatorPeopleText.Text = $"Люди внутри: {string.Join(", ", ids)}\n" +
                                              overloadStatus +
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
                WeightText.Text = $"{_elevator.CurrentWeight:F1} кг";
                TransportedCountText.Text = _transportedCount.ToString();
                StopButton.IsEnabled = (_elevator.PeopleInside.Count == 0);

                if (StopButton.IsEnabled)
                {
                    StopButton.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69));
                    StopButton.Foreground = Brushes.White;
                }
                else
                {
                    StopButton.Background = new SolidColorBrush(Color.FromRgb(200, 200, 200));
                    StopButton.Foreground = Brushes.Gray;
                }
            });
        }

        // Таймер обновления статуса
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

        // Добавление нового человека
        public void AddNewPerson(Person person)
        {
            Dispatcher.Invoke(() =>
            {
                _allPeople.Add(person);
                _totalPeopleCreated++;

                // Запоминаем время создания человека
                _personCreationTimes[person.Id] = DateTime.Now;

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

                var peopleToRemove = _allPeople.Where(p => p.State == PersonState.Delivered &&
                                                          (DateTime.Now - p.DeliveryTime).TotalSeconds >= 5).ToList();

                foreach (var person in peopleToRemove)
                {
                    _allPeople.Remove(person);
                    // Удаляем из словаря времени создания
                    _personCreationTimes.Remove(person.Id);
                    AddLog($"Человек ID {person.Id} удален из системы");
                }

                UpdateInfoPanel();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в CleanupTimer: {ex.Message}");
            }
        }

        // Основной таймер симуляции
        private void SimulationTimer_Tick(object sender, EventArgs e)
        {
            if (_systemStopped || _isProcessingOverload) return;

            _currentTime++;

            try
            {
                // Автоматические вызовы для ожидающих людей (с проверкой защиты и задержки)
                foreach (var person in _allPeople.Where(p => p.State == PersonState.Waiting))
                {
                    // Проверяем задержку 5 секунд для новых людей
                    if (!IsCallAllowedFromFloor(person.CurrentFloor, person.Id))
                    {
                        TimeSpan elapsed = DateTime.Now - _personCreationTimes[person.Id];
                        if (elapsed.TotalSeconds < 5)
                        {
                            // Пропускаем вызов для новых людей
                            continue;
                        }
                    }

                    if (!_elevator.TargetFloors.Contains(person.CurrentFloor) &&
                        IsCallAllowedFromFloor(person.CurrentFloor))
                    {
                        PressCallButton(person.CurrentFloor);
                        _elevator.AddTargetFloor(person.CurrentFloor);
                        SortTargetFloors();
                    }
                }

                // Обновление текущего этажа для людей в лифте
                foreach (var person in _elevator.PeopleInside)
                {
                    person.CurrentFloor = _elevator.CurrentFloor;
                }

                // Обработка состояния лифта
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

        // Состояние "Ожидание" (теперь "Стоит с закрытыми дверьми")
        private void ProcessIdleState()
        {
            if (_elevator.IsOverloaded)
            {
                _elevator.State = ElevatorState.Overloaded;
                return;
            }

            if (_elevator.TargetFloors.Count > 0)
            {
                int nextTarget = _elevator.TargetFloors[0];

                if (nextTarget == _elevator.CurrentFloor)
                {
                    _elevator.State = ElevatorState.DoorsOpen;
                    UpdateStatus($"Лифт прибыл на этаж {nextTarget}. Двери открываются.");
                    return;
                }

                if (nextTarget > _elevator.CurrentFloor)
                {
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
                else
                {
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
            else
            {
                // Нет целей - просто стоим с закрытыми дверьми
                // Не пишем постоянно в лог
                if (!_idleMessageShown)
                {
                    UpdateStatus("Лифт стоит с закрытыми дверьми.");
                    _idleMessageShown = true;
                }
            }
        }

        // Состояние "Движение вверх"
        private void ProcessMovingUpState()
        {
            if (_elevator.IsOverloaded)
            {
                _elevator.State = ElevatorState.Overloaded;
                return;
            }

            _elevator.CurrentFloor++;

            if (_elevator.CurrentFloor > _initData.TotalFloors)
            {
                _elevator.CurrentFloor = _initData.TotalFloors;
                _elevator.State = ElevatorState.Idle;
                UpdateStatus($"Достигнут верхний этаж {_initData.TotalFloors}.");
                _idleMessageShown = false; // Сбрасываем флаг для нового сообщения
                return;
            }

            if (_elevator.TargetFloors.Contains(_elevator.CurrentFloor))
            {
                _elevator.State = ElevatorState.DoorsOpen;
                UpdateStatus($"Лифт прибыл на этаж {_elevator.CurrentFloor}. Двери открываются.");
            }
            else
            {
                UpdateStatus($"Лифт на этаже {_elevator.CurrentFloor}");
            }
        }

        // Состояние "Движение вниз"
        private void ProcessMovingDownState()
        {
            if (_elevator.IsOverloaded)
            {
                _elevator.State = ElevatorState.Overloaded;
                return;
            }

            _elevator.CurrentFloor--;

            if (_elevator.CurrentFloor < 1)
            {
                _elevator.CurrentFloor = 1;
                _elevator.State = ElevatorState.Idle;
                UpdateStatus($"Достигнут нижний этаж 1.");
                _idleMessageShown = false; // Сбрасываем флаг для нового сообщения
                return;
            }

            if (_elevator.TargetFloors.Contains(_elevator.CurrentFloor))
            {
                _elevator.State = ElevatorState.DoorsOpen;
                UpdateStatus($"Лифт прибыл на этаж {_elevator.CurrentFloor}. Двери открываются.");
            }
            else
            {
                UpdateStatus($"Лифт на этаже {_elevator.CurrentFloor}");
            }
        }

        // Состояние "Перегрузка"
        private void ProcessOverloadedState()
        {
            UpdateStatus($"ПЕРЕГРУЗКА! Вес: {_elevator.CurrentWeight:F1} кг. Ожидание выхода людей...");

            // Если перегрузка устранена, меняем состояние
            if (!_elevator.IsOverloaded)
            {
                _elevator.State = ElevatorState.Idle;
                UpdateStatus("Перегрузка устранена.");
                _idleMessageShown = false; // Сбрасываем флаг
            }
        }

        // Состояние "Двери открыты" - ИСПРАВЛЕННАЯ ВЕРСИЯ
        private void ProcessDoorsOpenState()
        {
            // Высадка людей по целевому этажу
            var peopleToExit = _elevator.PeopleInside.Where(p => p.TargetFloor == _elevator.CurrentFloor).ToList();
            foreach (var person in peopleToExit)
            {
                _elevator.RemovePerson(person);
                _transportedCount++;
                _totalWeightMoved += person.Weight;
                person.State = PersonState.Delivered;
                person.CurrentFloor = _elevator.CurrentFloor;

                UpdateStatus($"Человек {person.Id} вышел на этаже {_elevator.CurrentFloor}");
                ReleaseElevatorFloorButton(_elevator.CurrentFloor);
            }

            // Снимаем вызов на этом этаже
            ReleaseCallButton(_elevator.CurrentFloor);
            _elevator.RemoveTargetFloor(_elevator.CurrentFloor);

            // ПОСАДКА ЛЮДЕЙ - с защитой от зацикливания и проверкой задержки
            // Проверяем, не действует ли защита от зацикливания на этом этаже
            if (_isOverloadCooldown && _lastOverloadFloor == _elevator.CurrentFloor)
            {
                UpdateStatus($"Защита от зацикливания активна на этаже {_elevator.CurrentFloor}. Посадка временно запрещена.");

                // Пропускаем посадку и сразу закрываем двери
                _elevator.State = ElevatorState.Idle;
                _idleMessageShown = false; // Сбрасываем флаг
                return;
            }

            var peopleToEnter = _allPeople.Where(p => p.State == PersonState.Waiting &&
                                                     p.CurrentFloor == _elevator.CurrentFloor).ToList();

            bool peopleEntered = false;
            bool overloadOccurred = false;

            foreach (var person in peopleToEnter)
            {
                // Разрешаем вход без проверки веса
                _lastEnteredPerson = person;
                bool causedOverload = _elevator.AddPerson(person);
                person.State = PersonState.InElevator;
                peopleEntered = true;

                UpdateStatus($"Человек {person.Id} вошел в лифт (вес: {person.Weight} кг)");

                // Если возникла перегрузка
                if (_elevator.IsOverloaded)
                {
                    overloadOccurred = true;
                    UpdateStatus($"ПЕРЕГРУЗКА! Человек {person.Id} вызвал перегрузку. Вес: {_elevator.CurrentWeight:F1} кг");
                    _elevator.State = ElevatorState.Overloaded;

                    // Принудительно высаживаем последнего вошедшего
                    _elevator.RemovePerson(person);
                    person.State = PersonState.Waiting;
                    person.CurrentFloor = _elevator.CurrentFloor;

                    UpdateStatus($"Человек {person.Id} не смог войти из-за перегрузки");

                    // Включаем защиту от зацикливания
                    ActivateOverloadProtection();

                    break; // Прерываем посадку, если произошла перегрузка
                }
                else
                {
                    // Если перегрузки нет, добавляем цель этажа
                    PressElevatorFloorButton(person.TargetFloor);
                    if (!_elevator.TargetFloors.Contains(person.TargetFloor))
                    {
                        _elevator.AddTargetFloor(person.TargetFloor);
                        SortTargetFloors();
                    }
                    UpdateStatus($"Человек {person.Id} направляется на этаж {person.TargetFloor}");
                }
            }

            // Обработка результатов посадки
            if (overloadOccurred)
            {
                // Если была перегрузка, останавливаем систему на 1 секунду
                _isProcessingOverload = true;
                _simulationTimer.Stop();
                _overloadTimer.Start();
                UpdateStatus($"Перегрузка! Система приостановлена.");
            }
            else if (peopleEntered && _elevator.PeopleInside.Count > 0)
            {
                // Если люди успешно вошли, закрываем двери
                _elevator.State = ElevatorState.Idle;

                // НАЖИМАЕМ кнопку ХОД когда лифт переходит в состояние "Стоит с закрытыми дверьми"
                PressStartMovementButton();
                UpdateStatus("Кнопка 'ХОД' нажата");

                // Запускаем таймер для отжатия кнопки через 2 секунды
                _startButtonTimer.Start();
            }
            else if (_elevator.PeopleInside.Count > 0)
            {
                // Если в лифте уже были люди (высадка, но не посадка)
                _elevator.State = ElevatorState.Idle;

                // НАЖИМАЕМ кнопку ХОД когда лифт переходит в состояние "Стоит с закрытыми дверьми"
                PressStartMovementButton();
                UpdateStatus("Кнопка 'ХОД' нажата");

                // Запускаем таймер для отжатия кнопки через 2 секунды
                _startButtonTimer.Start();
            }
            else
            {
                // Если никто не вошел и в лифте нет людей, просто закрываем двери
                _elevator.State = ElevatorState.Idle;
                _idleMessageShown = false; // Сбрасываем флаг
            }
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

                        var lines = _logTextBox.Text.Split('\n');
                        if (lines.Length > 100)
                            _logTextBox.Text = string.Join("\n", lines.Skip(lines.Length - 100));
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при записи в лог: {ex.Message}");
            }
        }

        // Обработчики кнопок
        private void CreatePersonButton_Click(object sender, RoutedEventArgs e)
        {
            var createWindow = new CreatePersonWindow(this, _initData.TotalFloors);
            createWindow.Owner = this;
            createWindow.ShowDialog();
        }

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
            if (_elevator.PeopleInside.Count > 0)
            {
                MessageBox.Show("Невозможно остановить систему: в лифте находятся люди!",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show("Вы уверены, что хотите остановить симуляцию?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            _systemStopped = true;
            _simulationTimer.Stop();
            _cleanupTimer.Stop();
            _statusTimer.Stop();
            _overloadTimer.Stop();
            _overloadIndicatorTimer.Stop();
            _overloadCooldownTimer.Stop();
            _startButtonTimer.Stop();

            var reportWindow = new ReportWindow(
                _totalTrips,
                _emptyTrips,
                _totalWeightMoved,
                _totalPeopleCreated);

            reportWindow.Owner = this;
            reportWindow.Show();
            this.Hide();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _systemStopped = true;
            _simulationTimer?.Stop();
            _cleanupTimer?.Stop();
            _statusTimer?.Stop();
            _overloadTimer?.Stop();
            _overloadIndicatorTimer?.Stop();
            _overloadCooldownTimer?.Stop();
            _startButtonTimer?.Stop();
        }

        // Метод для начала новой симуляции
        public void StartNewSimulation()
        {
            var initWindow = new InitializationWindow();
            initWindow.Show();
            this.Close();
        }
    }
}