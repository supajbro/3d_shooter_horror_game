using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    /*
      CameraPunch(
        new Vector3(0f, 0.015f, -0.025f),
        new Vector3(-1f, 0f, 0f)
    );

    CameraPunch(
    new Vector3(0f, 0.03f, -0.08f),
    new Vector3(-3f, 0.25f, 0f)
    );

    CameraPunch(
    new Vector3(0f, 0.04f, -0.13f),
    new Vector3(-5f, 0.4f, 0f)
    );
    */

    [Header("References")]
    [SerializeField] private Camera m_camera;
    [SerializeField] private Transform m_weaponHoldPoint;
    [SerializeField] private Animator m_playerAnim;
    private Transform m_cameraRoot;
    private CinemachineCamera m_cinemachineCamera;

    [Header("Head Bob Settings")]
    [SerializeField] private bool m_enableHeadBob = true;
    [SerializeField] private float m_bobFrequency = 6f;
    [SerializeField] private float m_bobAmplitude = 0.05f;
    [SerializeField] private float m_bobHorizontalAmplitude = 0.02f;
    [SerializeField] private float m_speedMultiplier = 1f;
    [SerializeField] private float m_idleReturnSpeed = 5f;

    // runtime bob state
    private float m_bobBlend = 0f;

    private float m_pitch;
    public void SetPitch(float pitch){m_pitch = pitch;}

    [Header("Impulse Settings")]
    private Vector3 m_targetPositionImpulse;
    private Vector3 m_targetRotationImpulse;
    private Vector3 m_cameraImpulsePosition;
    private Vector3 m_cameraImpulseVelocity;
    private Vector3 m_cameraImpulseRotation;
    private Vector3 m_cameraImpulseRotationVelocity;

    [Header("FOV Settings")]
    private float m_initialFOV      = -1;
    private float m_fovKickValue    = 2.0f;

    private Vector3 m_initialLocalPos;
    private float m_timer;
    private FirstPersonController m_player;

    [SerializeField] private float m_slideCameraOffset = -0.5f;
    [SerializeField] private float m_slideCameraSmoothSpeed = 10f;
    private float m_currentSlideOffset;

    [Header("Spring Settings")]
    [SerializeField] private float m_cameraSpringSmoothTime = 0.06f; // lower = snappier
    private Vector3 m_cameraPositionVelocity;

    public void Init(FirstPersonController player, Transform cameraRoot, CinemachineCamera cinemachine)
    {
        if (m_camera != null)
        {
            m_initialLocalPos = m_camera.transform.localPosition;
        }

        m_player = player;
        m_cameraRoot = cameraRoot;
        m_cinemachineCamera = cinemachine;
        m_initialFOV = m_cinemachineCamera.Lens.FieldOfView;

        // ensure bob blend initial state
        m_bobBlend = 0f;
    }

    public void UpdateCamera()
    {
        if(m_camera == null)
        {
            Debug.LogError("Missing something pretty crucial here.");
            return;
        }

        UpdateCameraImpulse();
        UpdateFOV();

        float speed = GetMovementSpeed();

        // Slide offset handled and smoothed here (no external writer)
        float targetSlideOffset = m_player != null && m_player.IsSliding() ? m_slideCameraOffset : 0f;
        m_currentSlideOffset = Mathf.Lerp(
            m_currentSlideOffset,
            targetSlideOffset,
            Time.deltaTime * m_slideCameraSmoothSpeed);

        // Maintain bob phase continuity but fade amplitude smoothly.
        // Keep timer advancing slightly even when stopped to avoid phase reset.
        float minPhaseAdvance = 0.05f; // small advance when idle
        float effectiveSpeed = Mathf.Max(speed, minPhaseAdvance);
        m_timer += Time.deltaTime * m_bobFrequency * effectiveSpeed * m_speedMultiplier;

        // Blend amplitude to 1 when moving, to 0 when stopped (smooth)
        const float speedThreshold = 0.1f;
        float targetBobBlend = speed > speedThreshold ? 1f : 0f;

        Vector3 basePosition = m_initialLocalPos + Vector3.up * m_currentSlideOffset;
        Vector3 bobOffset = Vector3.zero;

        if (m_enableHeadBob)
        {
            m_bobBlend = Mathf.Lerp(m_bobBlend, targetBobBlend, Time.deltaTime * m_idleReturnSpeed);

            if (m_bobBlend > 0.001f)
            {
                bobOffset.x = Mathf.Cos(m_timer * 0.5f) * m_bobHorizontalAmplitude * m_bobBlend;
                bobOffset.y = Mathf.Sin(m_timer) * m_bobAmplitude * m_bobBlend;
            }
        }
        else
        {
            // When head bob is disabled ensure there is no residual motion
            m_bobBlend = 0f;
            m_timer = 0f;
            bobOffset = Vector3.zero;
        }

        Vector3 targetPosition = basePosition + bobOffset + m_cameraImpulsePosition;

        // Use SmoothDamp as a simple critically-feeling spring to follow targetPosition
        if (m_cameraRoot != null)
        {
            m_cameraRoot.localPosition = Vector3.SmoothDamp(
                m_cameraRoot.localPosition,
                targetPosition,
                ref m_cameraPositionVelocity,
                m_cameraSpringSmoothTime);

            m_cameraRoot.localRotation = Quaternion.Euler(
                m_pitch + m_cameraImpulseRotation.x,
                m_cameraImpulseRotation.y,
                m_cameraImpulseRotation.z);
        }
    }

    #region - IMPULSE SETTINGS -
    public void CameraPunch(Vector3 positionKick, Vector3 rotationKick)
    {
        m_targetPositionImpulse += positionKick;
        m_targetRotationImpulse += rotationKick;
    }

    private void UpdateCameraImpulse()
    {
        const float MOVE_SPEED = 15f;

        m_cameraImpulsePosition = Vector3.Lerp(
            m_cameraImpulsePosition,
            m_targetPositionImpulse,
            Time.deltaTime * MOVE_SPEED);

        m_cameraImpulseRotation = Vector3.Lerp(
            m_cameraImpulseRotation,
            m_targetRotationImpulse,
            Time.deltaTime * MOVE_SPEED);

        m_targetPositionImpulse = Vector3.SmoothDamp(
            m_cameraImpulsePosition,
            Vector3.zero,
            ref m_cameraImpulseVelocity,
            0.08f);

        m_targetRotationImpulse = Vector3.SmoothDamp(
            m_cameraImpulseRotation,
            Vector3.zero,
            ref m_cameraImpulseRotationVelocity,
            0.1f);
    }
    #endregion

    #region - FOV KICK -
    public void AddFOVKick(float power)
    {
        m_fovKickValue += power;
    }

    private void UpdateFOV()
    {
        const float KICK_RETURN_SPEED = 10f;
        const float FOV_SPEED = 20f;

        m_fovKickValue = Mathf.Lerp(
            m_fovKickValue,
            0f,
            Time.deltaTime * KICK_RETURN_SPEED);

        m_cinemachineCamera.Lens.FieldOfView = Mathf.Lerp(
             m_cinemachineCamera.Lens.FieldOfView,
            m_initialFOV + m_fovKickValue,
            Time.deltaTime * FOV_SPEED);
    }
    #endregion

    #region - GETTERS -
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
    #endregion

    // Allow external code to toggle head bobbing (useful for wall-grab, ladders, cutscenes)
    public void SetHeadBobEnabled(bool enabled)
    {
        m_enableHeadBob = enabled;

        if (!m_enableHeadBob)
        {
            // Reset runtime bob state so there is no residual movement
            m_bobBlend = 0f;
            m_timer = 0f;

            // Snap camera root to its base local position and clear velocity
            if (m_cameraRoot != null)
            {
                m_cameraRoot.localPosition = m_initialLocalPos;
                m_cameraPositionVelocity = Vector3.zero;
            }

            // Also clear any small impulses so camera doesn't drift
            m_cameraImpulsePosition = Vector3.zero;
            m_cameraImpulseRotation = Vector3.zero;
            m_targetPositionImpulse = Vector3.zero;
            m_targetRotationImpulse = Vector3.zero;
        }
    }
}