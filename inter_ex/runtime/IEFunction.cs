using System;
using System.Collections.Generic;
using System.Linq;

namespace InterEx
{
    public partial record class IEFunction(IEEngine Engine, IList<string> Parameters, Statement Root, IEEngine.Scope Scope)
    {
        public IEEngine.Value InvokeRaw(IEEngine.Value[] arguments)
        {
            if (this.Parameters.Count > arguments.Length)
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

        public T Invoke<T>(params object[] arguments)
        {
            return this.Engine.ExportValue<T>(this.InvokeRaw(arguments.Select(this.Engine.ImportValue).ToArray()));
        }

        public void Invoke(params object[] arguments)
        {
            this.InvokeRaw(arguments.Select(this.Engine.ImportValue).ToArray());
        }

        public object InvokeAndExport(Type resultType, object[] arguments)
        {
            return this.Engine.ExportValue(this.InvokeRaw(arguments.Select(this.Engine.ImportValue).ToArray()), resultType);
        }
    }
}
