using UnityEngine;
using Events;

public static class PlayerState
{
    public static Vector2 Pos;
    public static float Speed = 400f; 
}

public class PlayerController2D : MonoBehaviour
{
    [SerializeField] private float speed = 400f;

    void Start()
    {
        PlayerState.Speed = speed;

        var ws = WorldState.Instance;
        if (ws && ws.PlayerSpawn != default) PlayerState.Pos = ws.PlayerSpawn;
        else PlayerState.Pos = Vector2.zero;

        Events.EventBus.Publish(new PlayerMoved(PlayerState.Pos));
    }

    void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        if (h == 0f && v == 0f) return;

        var ws = WorldState.Instance;
        if (!ws) return;

        float half = ws.MapHalfSize;
        Vector2 delta = new Vector2(h, v).normalized * (PlayerState.Speed * Time.deltaTime);
        PlayerState.Pos = new Vector2(
            Mathf.Clamp(PlayerState.Pos.x + delta.x, -half, half),
            Mathf.Clamp(PlayerState.Pos.y + delta.y, -half, half)
        );

        Events.EventBus.Publish(new PlayerMoved(PlayerState.Pos));
    }
}
