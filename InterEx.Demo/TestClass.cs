using System;

public class TestClass
{
    public enum StateType
    {
        Normal, Wrong, Right
    }

    public Action<string> Action1 = null;
    public Action<string, double> Action2 = null;
    public Func<double, double> Func1 = null;

    public string String = "hello";

    public StateType State = StateType.Normal;
}
