using System.Collections.Generic;
using System.Linq;

namespace ElevatorSim.Models
{
    public class Elevator
    {
        public int CurrentFloor { get; set; }
        public ElevatorState State { get; set; }
        public Direction CurrentDirection { get; set; }
        public List<int> TargetFloors { get; set; } = new List<int>();
        public List<Person> PeopleInside { get; set; } = new List<Person>();

        public double CurrentWeight => PeopleInside.Sum(p => p.Weight);
        public bool IsOverloaded => CurrentWeight > 400;

        public bool HasPeople => PeopleInside.Count > 0;

        // Метод для добавления целевого этажа
        public void AddTargetFloor(int floor)
        {
            if (!TargetFloors.Contains(floor))
            {
                TargetFloors.Add(floor);
            }
        }

        // Метод для удаления целевого этажа
        public void RemoveTargetFloor(int floor)
        {
            TargetFloors.Remove(floor);
        }

        // Метод для очистки всех целей
        public void ClearTargetFloors()
        {
            TargetFloors.Clear();
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