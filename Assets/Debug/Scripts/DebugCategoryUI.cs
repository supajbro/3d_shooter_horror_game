using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class DebugCategoryUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI m_title;
    [SerializeField] private Button m_button;

    public Button Button => m_button;

    public void Setup(string title, Action onClick)
    {
        if(m_title == null || m_button == null)
        {
            Debug.LogError("Unable to setupo debug category UI.");
            return;
        }

        m_title.text = title;
        m_button.onClick.AddListener(() => onClick.Invoke());
    }
}