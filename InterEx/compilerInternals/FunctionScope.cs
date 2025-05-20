namespace InterEx.CompilerInternals
{
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
;
