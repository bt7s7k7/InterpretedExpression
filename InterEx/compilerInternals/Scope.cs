using System.Collections.Generic;
using InterEx.Integration;
using InterEx.InterfaceTypes;

namespace InterEx.CompilerInternals
{
    public abstract class Scope : ICustomValue
    {
        protected Dictionary<string, Variable> _variables = new();

        public virtual bool Get(string name, out Variable variable)
        {
            return this._variables.TryGetValue(name, out variable);
        }

        public Variable Declare(string name)
        {
            var variable = new Variable(new Value(null));
            this._variables[name] = variable;
            return variable;
        }

        public FunctionScope MakeChild()
        {
            return new FunctionScope(this);
        }

        public bool TryGetOwn(string name, out Variable variable)
        {
            return this._variables.TryGetValue(name, out variable);
        }

        bool ICustomValue.Set(IEEngine engine, string name, Value value)
        {
            if (this._variables.TryGetValue(name, out var existing))
            {
                existing.Content = value;
                return true;
            }

            this.Declare(name).Content = value;
            return true;
        }

        bool ICustomValue.Get(IEEngine engine, string name, out Value value)
        {
            if (this.Get(name, out var variable))
            {
                value = variable.Content;
                return true;
            }

            value = default;
            return false;
        }

        bool ICustomValue.Invoke(IEEngine engine, Statement.Invocation invocation, string name, out Value result, Value[] arguments)
        {
            result = default;
            return false;
        }
    }
}
