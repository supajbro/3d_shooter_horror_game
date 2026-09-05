using TMPro;
using UnityEngine;

/// <summary>World-space text that animates itself and then returns to its pool.</summary>
public class FloatingCombatText : MonoBehaviour, IPoolable
{
    [SerializeField] private TMP_Text m_text;
    [SerializeField] private float m_lifetime = 0.8f;
    [SerializeField] private float m_moveSpeed = 1.2f;
    [SerializeField] private Vector3 m_spawnOffset;
    [SerializeField] private float m_fontSize = 24f;
    [SerializeField] private Color m_textColor = Color.white;

    private string m_poolKey;
    private Vector3 m_moveDirection;
    private float m_remainingLifetime;
    private Color m_startColor;
    private Camera m_camera;

    public void SetPoolKey(string key) => m_poolKey = key;

    public void SetVisualStyle(float fontSize, Color color)
    {
        m_fontSize = fontSize;
        m_textColor = color;
        EnsureText();
        m_text.fontSize = m_fontSize;
        m_text.color = m_textColor;
        m_startColor = m_text.color;
    }

    private void Awake()
    {
        EnsureText();
        m_text.fontSize = m_fontSize;
        m_text.color = m_textColor;
        m_text.alignment = TextAlignmentOptions.Center;
        m_text.enableWordWrapping = false;
        m_startColor = m_text.color;
    }

    public void Configure(string message, Vector3 hitPoint, Vector3 moveDirection, Camera camera)
    {
        EnsureText();
        m_camera = camera;
        m_text.text = message;
        m_startColor = m_text.color;
        m_remainingLifetime = m_lifetime;
        m_moveDirection = moveDirection.normalized;
        transform.position = hitPoint + m_spawnOffset;
        FaceCamera();
    }

    private void OnEnable()
    {
        if (m_text != null)
            m_text.color = m_startColor;
    }

    private void Update()
    {
        if (m_camera == null)
            m_camera = Camera.main;

        FaceCamera();
        transform.position += m_moveDirection * (m_moveSpeed * Time.deltaTime);
        m_remainingLifetime -= Time.deltaTime;

        if (m_text != null)
        {
            Color color = m_startColor;
            color.a *= Mathf.Clamp01(m_remainingLifetime / m_lifetime);
            m_text.color = color;
        }

        if (m_remainingLifetime <= 0f)
            ObjectPooler.Instance?.ReturnToPool(m_poolKey, gameObject);
    }

    private void FaceCamera()
    {
        if (m_camera != null)
            transform.rotation = Quaternion.LookRotation(m_camera.transform.forward, m_camera.transform.up);
    }

    private void EnsureText()
    {
        if (m_text != null)
            return;

        m_text = GetComponent<TMP_Text>();
        if (m_text == null)
            m_text = gameObject.AddComponent<TextMeshPro>();
    }
}
