using UnityEngine;

public class Pistol : BaseGunController
{
    [SerializeField] protected Animator m_craneAnim;

    [Header("Cylinder")]
    [SerializeField] private Transform m_cylinder;
    [SerializeField] private int m_chambers = 6;
    [SerializeField] private float m_rotationSpeed = 800f;
    [SerializeField] private Vector3 m_rotationAxis = Vector3.forward;

    private Quaternion m_initialRotation;
    private int m_currentChamber;
    private float m_targetAngle;
    private float m_currentAngle;

    public override void Init()
    {
        base.Init();

        m_initialRotation = m_cylinder.localRotation;

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

    protected override void Update()
    {
        base.Update();
        RotateCylinder();
    }

    private void RotateCylinder()
    {
        m_currentAngle = Mathf.MoveTowardsAngle(
            m_currentAngle,
            m_targetAngle,
            m_rotationSpeed * Time.deltaTime);

        m_cylinder.localRotation =
            m_initialRotation *
            Quaternion.AngleAxis(m_currentAngle, m_rotationAxis);
    }

    private void AdvanceCylinder()
    {
        m_currentChamber = (m_currentChamber + 1) % m_chambers;

        float step = 360f / m_chambers;
        m_targetAngle = m_currentChamber * step;
    }

    protected override void OnShoot()
    {
        base.OnShoot();
        AdvanceCylinder();
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