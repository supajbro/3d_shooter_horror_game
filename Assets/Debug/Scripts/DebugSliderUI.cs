using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DebugSliderUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text valueText;

    private DebugFloat  m_debugFloat;
    private DebugInt    m_debugInt;

    public void SetupFloat(DebugFloat debugFloat)
    {
        m_debugFloat = debugFloat;

        label.text = debugFloat.m_name;

        slider.wholeNumbers     = false;
        slider.minValue         = debugFloat.m_min;
        slider.maxValue         = debugFloat.m_max;
        slider.value            = debugFloat.GetValue();

        slider.onValueChanged.AddListener(OnValueChangedFloat);
        UpdateText(slider.value);
    }

    public void SetupInt(DebugInt debugInt)
    {
        m_debugInt = debugInt;

        label.text = debugInt.m_name;

        slider.wholeNumbers     = true;
        slider.minValue         = debugInt.m_min;
        slider.maxValue         = debugInt.m_max;
        slider.value            = debugInt.GetValue();

        slider.onValueChanged.AddListener(OnValueChangedInt);
        UpdateText(slider.value);
    }

    private void OnValueChangedFloat(float value)
    {
        m_debugFloat.SetValue(value);
        UpdateText(value);
    }

    private void OnValueChangedInt(float value)
    {
        m_debugInt.SetValue((int)value);
        UpdateText(value);
    }

    private void UpdateText(float value)
    {
        valueText.text = value.ToString("0.00");
    }
}