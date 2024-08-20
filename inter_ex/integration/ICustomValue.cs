using InterEx.CompilerInternals;
using InterEx.InterfaceTypes;

namespace InterEx.Integration
{
    public interface ICustomValue
    {
        public bool Set(IEEngine engine, string name, Value value);
        public bool Get(IEEngine engine, string name, out Value value);
        public bool Invoke(IEEngine engine, Statement.Invocation invocation, string name, out Value result, Value[] arguments);
    }
}
