public class DebugFloat
{
    public string Name;
    public float Min;
    public float Max;

    private System.Func<float> _getter;
    private System.Action<float> _setter;

    public DebugFloat(string name, float min, float max, System.Func<float> getter, System.Action<float> setter)
    {
        Name = name;
        Min = min;
        Max = max;
        _getter = getter;
        _setter = setter;
    }

    public float GetValue() => _getter();
    public void SetValue(float value) => _setter(value);
}