using System;
using System.ComponentModel;

namespace ElevatorSim.Models
{
    public class Person : INotifyPropertyChanged
    {
        private PersonState _state;
        private int _currentFloor;
        private DateTime _deliveryTime;
        private bool _scheduledForRemoval;
        private DateTime _creationTime;

        public int Id { get; set; }
        public double Weight { get; set; }
        public int StartFloor { get; set; }
        public int TargetFloor { get; set; }

        public int CurrentFloor
        {
            get => _currentFloor;
            set
            {
                if (_currentFloor != value)
                {
                    _currentFloor = value;
                    OnPropertyChanged(nameof(CurrentFloor));
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        public PersonState State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;

                    // При доставке запоминаем время
                    if (_state == PersonState.Delivered)
                    {
                        _deliveryTime = DateTime.Now;
                    }

                    OnPropertyChanged(nameof(State));
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        public DateTime DeliveryTime => _deliveryTime;

        // Время создания человека
        public DateTime CreationTime
        {
            get => _creationTime;
            set => _creationTime = value;
        }

        public bool ShouldBeRemoved
        {
            get
            {
                if (State == PersonState.Delivered && !_scheduledForRemoval)
                {
                    return (DateTime.Now - _deliveryTime).TotalSeconds >= 5;
                }
                return false;
            }
        }

        public void MarkForRemoval()
        {
            _scheduledForRemoval = true;
        }

        public bool IsScheduledForRemoval => _scheduledForRemoval;

        public string Status
        {
            get
            {
                switch (State)
                {
                    case PersonState.Waiting:
                        return $"Ожидает на этаже {CurrentFloor} → этаж {TargetFloor}";
                    case PersonState.InElevator:
                        return $"В лифте → этаж {TargetFloor}";
                    case PersonState.Delivered:
                        if (_scheduledForRemoval)
                        {
                            double secondsLeft = 5 - (DateTime.Now - _deliveryTime).TotalSeconds;
                            if (secondsLeft > 0)
                            {
                                return $"Доставлен на этаж {TargetFloor} (удаление через {secondsLeft:F1} сек)";
                            }
                            else
                            {
                                return $"Доставлен на этаж {TargetFloor} (удаление...)";
                            }
                        }
                        return $"Доставлен на этаж {TargetFloor}";
                    default:
                        return "Неизвестно";
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Метод для обновления статуса
        public void UpdateStatus()
        {
            OnPropertyChanged(nameof(Status));
        }

        // Конструктор по умолчанию
        public Person()
        {
            _creationTime = DateTime.Now;
        }
    }

    public enum PersonState
    {
        Waiting,      // Ожидает лифт
        InElevator,   // В лифте
        Delivered     // Доставлен
    }
}