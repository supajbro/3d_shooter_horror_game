using UnityEngine;

public class AmmoDrop : MonoBehaviour, IDropable
{
    [SerializeField] private GameObject m_ammoPickupPrefab;

    [SerializeField, Range(0f, 1f)] private float m_dropChance = 0.25f;
    public float DropChance => m_dropChance;

    public void Drop()
    {
        if (Random.value > DropChance)
            return;
        Instantiate(m_ammoPickupPrefab, transform.position, Quaternion.identity);
    }
}