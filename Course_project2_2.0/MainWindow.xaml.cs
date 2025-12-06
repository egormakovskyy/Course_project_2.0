using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;

namespace ElevatorSim
{
    public partial class MainWindow : Window
    {
        // Объявляем класс данных
        public class InitializationData
        {
            public int TotalFloors { get; set; }
            public int StartFloor { get; set; }
            public ObservableCollection<InitializationWindow.Person> People { get; set; }
        }

        private InitializationData _initData;

        // Конструктор по умолчанию (без параметров) - для дизайнера
        public MainWindow()
        {
            InitializeComponent();
            Title = "Система контроля лифта - Не инициализировано";

            // Показываем сообщение, если окно открыто без данных
            var textBlock = new TextBlock
            {
                Text = "Окно открыто без инициализации. Пожалуйста, закройте и запустите программу снова.",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap
            };

            Content = textBlock;
        }

        // Конструктор с данными из окна инициализации
        public MainWindow(InitializationData initData)
        {
            InitializeComponent();
            _initData = initData;

            // Инициализация системы лифта
            InitializeElevatorSystem();
        }

        private void InitializeElevatorSystem()
        {
            // Здесь будет ваша основная логика лифта
            if (_initData != null)
            {
                Title = $"Система контроля лифта - {_initData.TotalFloors} этажей, {_initData.People.Count} человек";

                // Для отладки выведем в консоль
                System.Diagnostics.Debug.WriteLine("=== Инициализация системы лифта ===");
                System.Diagnostics.Debug.WriteLine($"Этажей: {_initData.TotalFloors}");
                System.Diagnostics.Debug.WriteLine($"Стартовый этаж лифта: {_initData.StartFloor}");
                System.Diagnostics.Debug.WriteLine($"Количество людей: {_initData.People.Count}");

                foreach (var person in _initData.People)
                {
                    System.Diagnostics.Debug.WriteLine($"Человек {person.Id}: {person.Weight}кг, {person.CurrentFloor}→{person.TargetFloor} ({person.Direction})");
                }

                // Создаем основной интерфейс
                CreateMainInterface();
            }
        }

        private void CreateMainInterface()
        {
            // Создаем основную сетку
            var mainGrid = new Grid();

            // Добавляем строки
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Создаем интерфейс лифта
            var elevatorPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20, 20, 20, 20) // Исправлено: указаны все 4 параметра
            };

            // Отображаем этажи
            var floorsPanel = new StackPanel
            {
                Margin = new Thickness(10, 10, 10, 10) // Исправлено: указаны все 4 параметра
            };

            for (int i = _initData.TotalFloors; i >= 1; i--)
            {
                var floorText = new TextBlock
                {
                    Text = $"Этаж {i}",
                    FontSize = 14,
                    Margin = new Thickness(0, 5, 0, 5) // Исправлено: указаны все 4 параметра
                };

                if (i == _initData.StartFloor)
                {
                    floorText.Text += " [ЛИФТ]";
                    floorText.FontWeight = FontWeights.Bold;
                    floorText.Foreground = System.Windows.Media.Brushes.Red;
                }

                floorsPanel.Children.Add(floorText);
            }

            elevatorPanel.Children.Add(floorsPanel);

            // Панель информации
            var infoPanel = new StackPanel
            {
                Margin = new Thickness(20, 20, 20, 20), // Исправлено: указаны все 4 параметра
                VerticalAlignment = VerticalAlignment.Top
            };

            infoPanel.Children.Add(new TextBlock
            {
                Text = "Информация о системе:",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            infoPanel.Children.Add(new TextBlock
            {
                Text = $"Всего этажей: {_initData.TotalFloors}",
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 5)
            });

            infoPanel.Children.Add(new TextBlock
            {
                Text = $"Людей в системе: {_initData.People.Count}",
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 5)
            });

            infoPanel.Children.Add(new TextBlock
            {
                Text = $"Лифт на этаже: {_initData.StartFloor}",
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 5)
            });

            elevatorPanel.Children.Add(infoPanel);

            // Добавляем на главную сетку
            Grid.SetRow(elevatorPanel, 0);
            mainGrid.Children.Add(elevatorPanel);

            // Добавляем кнопки управления
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10) // Исправлено: указаны все 4 параметра
            };

            var startButton = new Button
            {
                Content = "Старт симуляции",
                Width = 120,
                Height = 30,
                Margin = new Thickness(5, 5, 5, 5) // Исправлено: указаны все 4 параметра
            };
            startButton.Click += StartSimulation_Click;

            var stopButton = new Button
            {
                Content = "Стоп",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5, 5, 5, 5), // Исправлено: указаны все 4 параметра
                IsEnabled = false
            };
            stopButton.Click += StopSimulation_Click;

            buttonPanel.Children.Add(startButton);
            buttonPanel.Children.Add(stopButton);

            Grid.SetRow(buttonPanel, 1);
            mainGrid.Children.Add(buttonPanel);

            // Устанавливаем содержимое окна
            Content = mainGrid;
        }

        private void StartSimulation_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Симуляция начата! (Эта функциональность будет реализована позже)",
                "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void StopSimulation_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Симуляция остановлена! (Эта функциональность будет реализована позже)",
                "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}