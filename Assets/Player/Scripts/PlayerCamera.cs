using StarterAssets;
using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera m_camera;
    [SerializeField] private Transform m_weaponHoldPoint;
    [SerializeField] private Animator m_playerAnim;
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
    private float m_noiseSeedX;
    private float m_noiseSeedY;
    private float m_noiseTime;

    public void Shake(float strength, float duration)
    {
        m_shakeStrength = strength;
        m_shakeDuration = duration;
        m_shakeTimer = duration;

        // random seeds so each shake feels different
        m_noiseSeedX = Random.Range(0f, 1000f);
        m_noiseSeedY = Random.Range(0f, 1000f);

        m_noiseTime = 0f;
    }

    private void CameraShakeUpdate()
    {
        if (m_shakeTimer <= 0f)
        {
            m_cameraRoot.localPosition = m_initialLocalPos;
            return;
        }

        m_shakeTimer -= Time.deltaTime;
        m_noiseTime += Time.deltaTime;

        float t = 1f - (m_shakeTimer / m_shakeDuration);

        // optional fade out (keeps shake from ending abruptly)
        float falloff = m_shakeCurve != null ? m_shakeCurve.Evaluate(t) : 1f;

        float time = m_noiseTime * 10f; // frequency control (tweak this)

        float x = (Mathf.PerlinNoise(m_noiseSeedX, time) - 0.5f) * 2f;
        float y = (Mathf.PerlinNoise(m_noiseSeedY, time) - 0.5f) * 2f;

        Vector3 noiseOffset = new Vector3(x, y, 0f) * m_shakeStrength * falloff;

        m_cameraRoot.localPosition = m_initialLocalPos + noiseOffset;
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

    public Animator GetPlayerAnimator()
    {
        if (m_playerAnim == null)
        {
            Debug.LogError("Missing Player Animation reference.");
            return null;
        }
        return m_playerAnim;
    }
}