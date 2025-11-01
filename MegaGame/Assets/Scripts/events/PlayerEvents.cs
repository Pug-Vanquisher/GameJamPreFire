using UnityEngine;

namespace Events
{
    public readonly struct PlayerMoved
    {
        public readonly Vector2 Pos;
        public PlayerMoved(Vector2 pos) { Pos = pos; }
    }
}
