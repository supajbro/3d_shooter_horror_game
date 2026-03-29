using UnityEngine;

public class AutoRifle : BaseGunController
{
    protected override bool IsFiring()
    {
        return Input.GetMouseButton(0); // hold to fire
    }
}