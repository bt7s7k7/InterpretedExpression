using System.Collections.Generic;
using InterEx.InterfaceTypes;

namespace InterEx.CompilerInternals
{
    public class Scope()
    {
        public Scope(Scope parent) : this()
        {
            this.Parent = parent;
        }

        protected Dictionary<string, Variable> _variables = [];

        public readonly Scope Parent = null;
        public bool IsGlobal => this.Parent == null;

        public Variable Declare(string name)
        {
            var variable = new Variable(new Value(null));
            this._variables[name] = variable;
            return variable;
        }

        public Scope MakeChild()
        {
            return new Scope(this);
        }

        public bool TryGetOwn(string name, out Variable variable)
        {
            return this._variables.TryGetValue(name, out variable);
        }

        public Value this[string name]
        {
            get => this._variables[name].Content;
            set
            {
                if (this._variables.TryGetValue(name, out var variable))
                {
                    variable.Content = value;
                }
                else
                {
                    this.Declare(name).Content = value;
                }
            }
        }
    }
}
