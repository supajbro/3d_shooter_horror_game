using StarterAssets;
using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera m_camera;
    [SerializeField] private Transform m_weaponHoldPoint;
    private Transform m_cameraRoot;

    [Header("Head Bob Settings")]
    [SerializeField] private bool m_enableHeadBob = true;
    [SerializeField] private float m_bobFrequency = 6f;
    [SerializeField] private float m_bobAmplitude = 0.05f;
    [SerializeField] private float m_bobHorizontalAmplitude = 0.02f;
    [SerializeField] private float m_speedMultiplier = 1f;
    [SerializeField] private float m_idleReturnSpeed = 5f;

    [Header("Camera Shake Settings")]
    [SerializeField] private AnimationCurve m_shakeCurve;
    private float m_shakeTimer;
    private float m_shakeDuration;
    private float m_shakeStrength;

    private Vector3 m_initialLocalPos;
    private float m_timer;
    private FirstPersonController m_player;

    public void Init(FirstPersonController player, Transform cameraRoot)
    {
        if (m_camera != null)
        {
            m_initialLocalPos = m_camera.transform.localPosition;
        }

        m_player = player;
        m_cameraRoot = cameraRoot;
    }

    private void Update()
    {
        HeadboppingUpdate();
    }

    private void LateUpdate()
    {
        CameraShakeUpdate();
    }

    private void HeadboppingUpdate()
    {
        if (!m_enableHeadBob || m_camera == null)
            return;

        float speed = GetMovementSpeed();

        if (speed > 0.1f)
        {
            m_timer += Time.deltaTime * m_bobFrequency * speed * m_speedMultiplier;

            float verticalOffset = Mathf.Sin(m_timer) * m_bobAmplitude;
            float horizontalOffset = Mathf.Cos(m_timer * 0.5f) * m_bobHorizontalAmplitude;

            Vector3 bobOffset = new Vector3(horizontalOffset, verticalOffset, 0f);
            m_cameraRoot.transform.localPosition = m_initialLocalPos + bobOffset;
        }
        else
        {
            // Smoothly return to original position when idle
            m_timer = 0f;
            m_cameraRoot.transform.localPosition = Vector3.Lerp(
                m_cameraRoot.transform.localPosition,
                m_initialLocalPos,
                Time.deltaTime * m_idleReturnSpeed
            );
        }
    }

    #region - SHAKE - 
    public void Shake(float strength, float duration)
    {
        m_shakeStrength = strength;
        m_shakeDuration = duration;
        m_shakeTimer = duration;
    }

    private void CameraShakeUpdate()
    {
        if (m_shakeTimer <= 0f)
            return;

        m_shakeTimer -= Time.deltaTime;

        float t = 1f - (m_shakeTimer / m_shakeDuration);
        float curveValue = m_shakeCurve.Evaluate(t);

        // single directional kick, not random spam
        Vector3 offset = new Vector3(0f, curveValue * m_shakeStrength, 0f);

        m_cameraRoot.localPosition = m_initialLocalPos + offset;
    }
    #endregion

    private float GetMovementSpeed()
    {
        if(m_player == null)
        {
            Debug.LogError("Missing reference to the player");
            return -1f;
        }
        return m_player.GetSpeed();
    }

    public Camera GetCamera()
    {
        if (m_camera == null)
        {
            Debug.LogError("Missing camera reference.");
            return null;
        }
        return m_camera;
    }

    public Transform GetWeaponHoldPoint()
    {
        if (m_weaponHoldPoint == null)
        {
            Debug.LogError("Missing weapon hold point reference.");
            return null;
        }
        return m_weaponHoldPoint;
    }
}