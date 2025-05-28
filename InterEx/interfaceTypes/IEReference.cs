using InterEx.CompilerInternals;

namespace InterEx.InterfaceTypes
{
    public abstract class IEReference
    {
        public abstract T Get<T>();
        public abstract Value Get();

        public abstract void Set(Value value);
        public abstract void Set(object value);

        public class ObjectProperty(IEEngine engine, Value receiver, string property) : IEReference
        {
            protected readonly IEEngine _engine = engine;
            protected readonly Value _receiver = receiver;
            protected readonly string _property = property;

            public override T Get<T>()
            {
                return this._engine.Integration.ExportValue<T>(this.Get());
            }

            public override Value Get()
            {
                return this._engine.GetProperty(this._receiver, this._property);
            }

            public override void Set(object value)
            {
                this.Set(this._engine.Integration.ImportValue(value));
            }

            public override void Set(Value value)
            {
                this._engine.SetProperty(this._receiver, this._property, value);
            }
        }

        public class VariableReference(IEEngine engine, Variable variable) : IEReference
        {
            protected readonly IEEngine _engine = engine;
            protected readonly Variable _variable = variable;

            public override T Get<T>()
            {
                return this._engine.Integration.ExportValue<T>(this.Get());
            }

            public override Value Get()
            {
                return this._variable.Content;
            }

            public override void Set(Value value)
            {
                this._variable.Content = value;
            }

            public override void Set(object value)
            {
                this.Set(this._engine.Integration.ImportValue(value));
            }
        }
    }
}
