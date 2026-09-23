using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Key : MonoBehaviour
{
    private void Reset()
    {
        // ensure collider exists and is a trigger
        var col = GetComponent<Collider>();
        if (col == null)
            col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        var gsm = GameStateManager.Instance;
        if (gsm == null)
        {
            Debug.LogWarning("Key: No GameStateManager instance found.");
            return;
        }

        // Find current gameplay state and increment key count
        var current = gsm.GetCurrentState();
        if (current is GameplayState gameplay)
        {
            gameplay.keyCollected += 1;
            Debug.Log($"Key collected. Total keys: {gameplay.keyCollected}");
        }

        // Disable the key so it can't be collected again
        gameObject.SetActive(false);
    }
}
