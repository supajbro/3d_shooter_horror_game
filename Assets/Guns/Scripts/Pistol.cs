using UnityEngine;

public class Pistol : BaseGunController
{
    [SerializeField] protected Animator m_craneAnim;

    public override void Init()
    {
        base.Init();

        DebugManager.Instance.RegisterFloat(
            new DebugFloat(
                "Pistol Fire Rate",
                0f,
                10f,
                () => m_fireRate,
                (v) => m_fireRate = v
            ),
            "Pistol"
        );

        DebugManager.Instance.RegisterInt(
            new DebugInt(
                "Pistol Max Ammo",
                5,
                100,
                () => m_maxAmmo,
                (v) => m_maxAmmo = v
            ),
            "Pistol"
        );

        DebugManager.Instance.RegisterFloat(
            new DebugFloat(
                "Pistol Recoil Kickback",
                0f,
                1f,
                () => recoilKickback,
                (v) => recoilKickback = v
            ),
            "Pistol"
        );

        DebugManager.Instance.RegisterFloat(
            new DebugFloat(
                "Pistol Recoil Speed",
                0f,
                20f,
                () => recoilSpeed,
                (v) => recoilSpeed = v
            ),
            "Pistol"
        );

        DebugManager.Instance.RegisterFloat(
            new DebugFloat(
                "Pistol Return Speed",
                0f,
                10f,
                () => returnSpeed,
                (v) => returnSpeed = v
            ),
            "Pistol"
        );
    }

    protected override void IdleState()
    {
        base.IdleState();
        m_craneAnim.SetTrigger("Idle");
    }

    protected override void ShootState()
    {
        base.ShootState();
    }

    protected override void ReloadState()
    {
        base.ReloadState();
        m_craneAnim.SetTrigger("Reload");
    }

    protected override bool IsFiring()
    {
        return Input.GetMouseButton(0); // hold to fire
    }
}