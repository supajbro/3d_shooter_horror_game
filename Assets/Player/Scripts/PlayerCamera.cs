using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    [SerializeField] private Camera m_camera;
    [SerializeField] private Transform m_weaponHoldPoint;

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
        if(m_weaponHoldPoint == null)
        {
            Debug.LogError("Missing weapon hold point reference.");
            return null;
        }
        return m_weaponHoldPoint;
    }
}
