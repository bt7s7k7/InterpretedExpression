using System.Collections.Generic;

namespace InterEx
{
    public partial record class IEFunction(IEEngine Engine, List<string> Parameters, Statement Root, IEEngine.Scope Scope)
    {
        public IEEngine.Value InvokeRaw(IEEngine.Value[] arguments)
        {
            if (this.Parameters.Count != arguments.Length)
            {
                throw new IERuntimeException($"Argument type mismatch, expected {this.Parameters.Count}, but got {arguments.Length}");
            }

            var innerScope = this.Scope.MakeChild();
            for (var i = 0; i < this.Parameters.Count; i++)
            {
                innerScope.Declare(this.Parameters[i]).Content = arguments[i];
            }

            return this.Engine.Evaluate(this.Root, innerScope);
        }
    }
}
