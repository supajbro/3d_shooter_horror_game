public class DebugInt
{
    public string m_name;
    public float m_min;
    public float m_max;

    private System.Func<int> m_getter;
    private System.Action<int> m_setter;

    public DebugInt(string name, int min, int max, System.Func<int> getter, System.Action<int> setter)
    {
        m_name = name;
        m_min = min;
        m_max = max;
        m_getter = getter;
        m_setter = setter;
    }

    public int GetValue() => m_getter();
    public void SetValue(int value) => m_setter(value);
}