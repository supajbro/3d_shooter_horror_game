using UnityEngine;

public abstract class PickupItem : MonoBehaviour
{
    public abstract void OnPickup(PlayerPickup player);
}