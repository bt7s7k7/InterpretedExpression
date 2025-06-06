using System;
using System.Collections.Generic;
using System.Linq;
using InterEx.CompilerInternals;

namespace InterEx.InterfaceTypes
{
    public partial record class IEFunction(IEEngine Engine, IList<string> Parameters, Statement Root, Scope Scope)
    {
        public Value InvokeRaw(IReadOnlyList<Value> arguments)
        {
            if (this.Parameters.Count > arguments.Count)
            {
                throw new IERuntimeException($"Argument type mismatch, expected {this.Parameters.Count}, but got {arguments.Count}");
            }

            var innerScope = this.Scope.MakeChild();
            for (var i = 0; i < this.Parameters.Count; i++)
            {
                innerScope.Declare(this.Parameters[i]).Content = arguments[i];
            }

            return this.Engine.Evaluate(this.Root, innerScope);
        }

        public T Invoke<T>(params IEnumerable<object> arguments)
        {
            return this.Engine.Integration.ExportValue<T>(this.InvokeRaw(arguments.Select(this.Engine.Integration.ImportValue).ToArray()));
        }

        public void Invoke(params IEnumerable<object> arguments)
        {
            this.InvokeRaw(arguments.Select(this.Engine.Integration.ImportValue).ToArray());
        }

        public object InvokeAndExport(Type resultType, IEnumerable<object> arguments)
        {
            return this.Engine.Integration.ExportValue(this.InvokeRaw(arguments.Select(this.Engine.Integration.ImportValue).ToArray()), resultType);
        }
    }
}
