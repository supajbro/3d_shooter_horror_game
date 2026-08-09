using UnityEngine;

/// <summary>
/// Simple exit trigger. When the player enters the trigger the game transitions
/// to the GameOver state via GameStateManager (wraps existing end-state logic).
/// Attach to a GameObject with a trigger collider; LevelManager creates one automatically.
/// </summary>
public class ExitTrigger : MonoBehaviour
{
    private void Reset()
    {
        // ensure there's a trigger collider for convenience when the component is added in editor
        Collider c = GetComponent<Collider>();
        if (c == null)
        {
            SphereCollider sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 1.25f;
        }
        else
        {
            c.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Detect player by tag (consistent with other code)
        if (!other.CompareTag("Player"))
            return;

        if (GameStateManager.Instance == null)
        {
            Debug.LogWarning("ExitTrigger: No GameStateManager instance found. Cannot transition to GameOverState.");
            return;
        }

        // Transition to GameOver state using existing state class
        GameStateManager.Instance.SetState(new GameOverState(GameStateManager.Instance));
    }
}