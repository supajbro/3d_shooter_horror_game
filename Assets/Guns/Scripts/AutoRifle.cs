using UnityEngine;

public class AutoRifle : BaseGunController
{
    public override void Init()
    {
        base.Init();

        DebugManager.Instance.RegisterFloat(
            new DebugFloat(
                "AutoRifle Fire Rate",
                20f,
                100f,
                () => m_fireRate,
                (v) => m_fireRate = v
            ),
            "AutoRifle"
        );

        DebugManager.Instance.RegisterFloat(
            new DebugFloat(
                "AutoRifle Recoil Kickback",
                0.06f,
                0.5f,
                () => recoilKickback,
                (v) => recoilKickback = v
            ),
            "AutoRifle"
        );

        DebugManager.Instance.RegisterFloat(
            new DebugFloat(
                "AutoRifle Recoil Speed",
                2.5f,
                10f,
                () => recoilSpeed,
                (v) => recoilSpeed = v
            ),
            "AutoRifle"
        );

        DebugManager.Instance.RegisterFloat(
            new DebugFloat(
                "AutoRifle Return Speed",
                0f,
                10f,
                () => returnSpeed,
                (v) => returnSpeed = v
            ),
            "AutoRifle"
        );
    }

    protected override bool IsFiring()
    {
        return Input.GetMouseButton(0); // hold to fire
    }
}