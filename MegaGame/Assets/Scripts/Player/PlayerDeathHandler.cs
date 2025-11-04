using UnityEngine;
using Events;

public class PlayerDeathHandler : MonoBehaviour
{
    [SerializeField] float restartDelay = 1.2f;

    void OnEnable() => EventBus.Subscribe<PlayerDied>(OnPlayerDied);
    void OnDisable() => EventBus.Unsubscribe<PlayerDied>(OnPlayerDied);

    void OnPlayerDied(PlayerDied _)
    {
        Invoke(nameof(Restart), restartDelay);
    }

    void Restart()
    {
        if (GameRunManager.Instance != null)
            GameRunManager.Instance.RestartSameMap(); // по умолчанию
        else
            Debug.LogWarning("GameRunManager missing Ч cannot soft-restart.");
    }
}
