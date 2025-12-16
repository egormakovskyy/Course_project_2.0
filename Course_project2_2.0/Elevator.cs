using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ElevatorSim.Models
{
    public class Elevator : INotifyPropertyChanged
    {
        private ElevatorState _state;
        private bool _isOverloaded;
        private List<int> _targetFloors = new List<int>();
        private List<Person> _peopleInside = new List<Person>();

        public int CurrentFloor { get; set; }

        public ElevatorState State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    OnPropertyChanged();
                }
            }
        }

        public Direction CurrentDirection { get; set; }

        public List<int> TargetFloors
        {
            get => _targetFloors;
            set
            {
                _targetFloors = value;
                OnPropertyChanged();
            }
        }

        public List<Person> PeopleInside
        {
            get => _peopleInside;
            set
            {
                _peopleInside = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentWeight));
                CheckOverload();
            }
        }

        public double CurrentWeight => PeopleInside.Sum(p => p.Weight);

        public bool IsOverloaded
        {
            get => _isOverloaded;
            private set
            {
                if (_isOverloaded != value)
                {
                    _isOverloaded = value;
                    OnPropertyChanged();

                    // Если появилась перегрузка, меняем состояние
                    if (_isOverloaded)
                    {
                        State = ElevatorState.Overloaded;
                    }
                    else if (State == ElevatorState.Overloaded)
                    {
                        State = ElevatorState.Idle;
                    }
                }
            }
        }

        public bool HasPeople => PeopleInside.Count > 0;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Метод для проверки перегрузки
        public void CheckOverload()
        {
            IsOverloaded = CurrentWeight > 400;
        }

        // Метод для добавления целевого этажа
        public void AddTargetFloor(int floor)
        {
            if (!TargetFloors.Contains(floor))
            {
                TargetFloors.Add(floor);
                OnPropertyChanged(nameof(TargetFloors));
            }
        }

        // Метод для удаления целевого этажа
        public void RemoveTargetFloor(int floor)
        {
            TargetFloors.Remove(floor);
            OnPropertyChanged(nameof(TargetFloors));
        }

        // Метод для очистки всех целей
        public void ClearTargetFloors()
        {
            TargetFloors.Clear();
            OnPropertyChanged(nameof(TargetFloors));
        }

        // Метод для добавления человека в лифт
        public bool AddPerson(Person person)
        {
            PeopleInside.Add(person);
            OnPropertyChanged(nameof(PeopleInside));
            OnPropertyChanged(nameof(CurrentWeight));

            // Проверяем перегрузку после добавления
            CheckOverload();

            return IsOverloaded;
        }

        // Метод для удаления человека из лифта
        public void RemovePerson(Person person)
        {
            PeopleInside.Remove(person);
            OnPropertyChanged(nameof(PeopleInside));
            OnPropertyChanged(nameof(CurrentWeight));

            // Проверяем перегрузку после удаления
            CheckOverload();
        }
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
}