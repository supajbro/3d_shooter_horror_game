public class DebugFloat
{
    public string m_name;
    public float m_min;
    public float m_max;

    public int intMin;
    public int intMax;

    private System.Func<float> m_getter;
    private System.Action<float> m_setter;

    private System.Func<int> m_intGetter;
    private System.Action<int> m_intSetter;

    public DebugFloat(string name, float min, float max, System.Func<float> getter, System.Action<float> setter)
    {
        m_name = name;
        m_min = min;
        m_max = max;
        m_getter = getter;
        m_setter = setter;
    }

    public float GetValue() => m_getter();
    public void SetValue(float value) => m_setter(value);
}