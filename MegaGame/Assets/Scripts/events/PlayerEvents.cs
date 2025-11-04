using UnityEngine;

namespace Events
{
    public readonly struct PlayerMoved
    {
        public readonly Vector2 Pos;
        public PlayerMoved(Vector2 pos) { Pos = pos; }
    }

    public readonly struct PlayerDamaged
    {
        public readonly int Amount;
        public readonly int HpNow;
        public PlayerDamaged(int amount, int hpNow) { Amount = amount; HpNow = hpNow; }
    }

    public readonly struct PlayerDied { }

    public readonly struct RunStarted
    {
        public readonly bool ReuseMap;
        public RunStarted(bool reuseMap) { ReuseMap = reuseMap; }
    }
}
