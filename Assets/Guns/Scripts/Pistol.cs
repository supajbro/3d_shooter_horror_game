using UnityEngine;

public class Pistol : BaseGunController
{
    protected override bool IsFiring()
    {
        return Input.GetMouseButton(0); // hold to fire
    }
}