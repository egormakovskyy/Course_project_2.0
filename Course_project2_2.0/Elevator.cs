using System.Collections.Generic;
using System.Linq;
using ElevatorSim.Models;

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