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

        if (!m_enableHeadBob)
            return;

        float speed = GetMovementSpeed();

        float targetSlideOffset = m_player.IsSliding() ? m_slideCameraOffset : 0f;
        m_currentSlideOffset = Mathf.Lerp(
            m_currentSlideOffset,
            targetSlideOffset,
            Time.deltaTime * m_slideCameraSmoothSpeed);

        Vector3 basePosition = m_initialLocalPos + Vector3.up * m_currentSlideOffset;
        Vector3 bobOffset = Vector3.zero;

        if (speed > 0.1f)
        {
            m_timer += Time.deltaTime * m_bobFrequency * speed * m_speedMultiplier;

            bobOffset.x = Mathf.Cos(m_timer * 0.5f) * m_bobHorizontalAmplitude;
            bobOffset.y = Mathf.Sin(m_timer) * m_bobAmplitude;
        }
        else
        {
            m_timer = 0f;
        }

        Vector3 targetPosition = basePosition + bobOffset + m_cameraImpulsePosition;

        m_cameraRoot.localPosition = Vector3.Lerp(
            m_cameraRoot.localPosition,
            targetPosition,
            Time.deltaTime * m_idleReturnSpeed);

        m_cameraRoot.localRotation = Quaternion.Euler(
            m_pitch + m_cameraImpulseRotation.x,
            m_cameraImpulseRotation.y,
            m_cameraImpulseRotation.z);
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
        Debug.Log("KICK: " + m_fovKickValue);
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
}