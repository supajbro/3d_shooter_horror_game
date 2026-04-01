using TMPro;
using UnityEngine;

public class EnemyUtils : MonoBehaviour
{
    [SerializeField] private Canvas m_debugCanvas;
    [SerializeField] private TextMeshProUGUI m_debugHealthText;

    public void InitDebug(bool debug, EnemyHealth health)
    {
        if(!debug)
        {
            m_debugCanvas.gameObject.SetActive(false);
            return;
        }
        m_debugCanvas.gameObject.SetActive(true);

        if (health == null)
        {
            Debug.LogError("Missing reference to enemy health.");
            return;
        }

        if (m_debugCanvas == null)
        {
            Debug.LogError("Missing reference to debug canvas.");
            return;
        }

        if (m_debugHealthText == null)
        {
            Debug.LogError("Missing reference to enemy health text.");
            return;
        }

        m_debugCanvas.worldCamera = Camera.main;

        SetHealthText(health.GetHealth());
        health.OnHealthChanged += SetHealthText;
    }

    private void SetHealthText(float value)
    {
        m_debugHealthText.text = value.ToString();
    }
}
