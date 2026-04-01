using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DebugSliderUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text valueText;

    private DebugFloat _debugFloat;

    public void Setup(DebugFloat debugFloat)
    {
        _debugFloat = debugFloat;

        label.text = debugFloat.Name;

        slider.minValue = debugFloat.Min;
        slider.maxValue = debugFloat.Max;
        slider.value = debugFloat.GetValue();

        slider.onValueChanged.AddListener(OnValueChanged);
        UpdateText(slider.value);
    }

    private void OnValueChanged(float value)
    {
        _debugFloat.SetValue(value);
        UpdateText(value);
    }

    private void UpdateText(float value)
    {
        valueText.text = value.ToString("0.00");
    }
}