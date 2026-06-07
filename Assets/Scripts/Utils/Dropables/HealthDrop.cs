using UnityEngine;

public class HealthDrop : MonoBehaviour, IDropable
{
    [SerializeField] private HealthPickup m_healthPrefab;
    [SerializeField, Range(0f, 1f)] private float m_dropChance = 0.25f;

    public float DropChance => m_dropChance;

    public void Drop()
    {
        if(m_healthPrefab == null)
        {
            Debug.LogError("Missing health pickup prefab.");
            return;
        }
        if (Random.value > DropChance) 
            return;
        var clone = Instantiate(m_healthPrefab, transform.position, Quaternion.identity);
        var newPos = clone.transform.position;
        newPos.y += 0.75f;
        clone.transform.position = newPos;
        clone?.Activate();
    }
}