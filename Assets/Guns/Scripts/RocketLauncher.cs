using System.Net.Sockets;
using UnityEngine;

public class RocketLauncher : BaseGunController
{
    [Header("Rocket Settings")]
    [SerializeField] private float m_launchForce = 1f;

    public override void Init()
    {
        base.Init();

        DebugManager.Instance.RegisterFloat(
            new DebugFloat(
                "Rocket Launcher Fire Rate",
                0f,
                10f,
                () => m_fireRate,
                (v) => m_fireRate = v
            )
        );

        DebugManager.Instance.RegisterFloat(
            new DebugFloat(
                "Rocket Launcher Recoil Kickback",
                0f,
                1f,
                () => recoilKickback,
                (v) => recoilKickback = v
            )
        );

        DebugManager.Instance.RegisterFloat(
            new DebugFloat(
                "Rocket Launcher Recoil Speed",
                0f,
                20f,
                () => recoilSpeed,
                (v) => recoilSpeed = v
            )
        );

        DebugManager.Instance.RegisterFloat(
            new DebugFloat(
                "Rocket Launcher Return Speed",
                0f,
                10f,
                () => returnSpeed,
                (v) => returnSpeed = v
            )
        );
    }

    protected override void OnShoot(Bullet bullet, Vector3 direction)
    {
        base.OnShoot(bullet, direction);
    }

    protected override bool IsFiring()
    {
        // Single shot instead of hold
        return Input.GetMouseButtonDown(0);
    }
}