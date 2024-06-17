using System;

public class TestClass
{
    public Action<string> Action1 = null;
    public Action<string, double> Action2 = null;
    public Func<double, double> Func1 = null;
}
