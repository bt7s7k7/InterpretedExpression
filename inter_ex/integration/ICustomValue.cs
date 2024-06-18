namespace InterEx
{
    public interface ICustomValue
    {
        public bool Set(IEEngine engine, string name, IEEngine.Value value);
        public bool Get(IEEngine engine, string name, out IEEngine.Value value);
        public bool Invoke(IEEngine engine, Statement.Invocation invocation, string name, out IEEngine.Value result, IEEngine.Value[] arguments);
    }
}
