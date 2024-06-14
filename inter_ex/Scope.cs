using System.Collections.Generic;

namespace InterEx
{
    public partial class IEEngine
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

        public FunctionScope PrepareCall()
        {
            return new FunctionScope(this.GlobalScope);
        }

        public class FunctionScope : Scope
        {
            protected readonly Scope _parent;

            public override bool Get(string name, out Variable variable)
            {
                if (base.Get(name, out variable)) return true;
                return this._parent.Get(name, out variable);
            }

            public FunctionScope(Scope parent)
            {
                this._parent = parent;
            }
        }
    }
}
;
