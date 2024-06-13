using System.Collections.Generic;

namespace InterEx
{
    public partial class IEEngine
    {
        public abstract class Scope
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
