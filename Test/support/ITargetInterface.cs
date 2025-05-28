public interface ITargetInterface
{
    public double Number { get; set; }
    public double DefaultValue => 10;

    public void Invoke1();
    public void Invoke2(Action value);
    public double Get1();
    public double Get2();
}
