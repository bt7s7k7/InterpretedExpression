namespace InterEx
{
    public abstract class IEReference
    {
        public abstract T Get<T>();
        public abstract IEEngine.Value Get();

        public abstract void Set(IEEngine.Value value);
        public abstract void Set(object value);

        public class ObjectProperty : IEReference
        {
            protected readonly IEEngine _engine;
            protected readonly IEEngine.Value _receiver;
            protected readonly string _property;

            public ObjectProperty(IEEngine engine, IEEngine.Value receiver, string property)
            {
                this._engine = engine;
                this._receiver = receiver;
                this._property = property;
            }

            public override T Get<T>()
            {
                return this._engine.ExportValue<T>(this.Get());
            }

            public override IEEngine.Value Get()
            {
                return this._engine.GetProperty(this._receiver, this._property);
            }

            public override void Set(object value)
            {
                this.Set(this._engine.ImportValue(value));
            }

            public override void Set(IEEngine.Value value)
            {
                this._engine.SetProperty(this._receiver, this._property, value);
            }
        }

        public class VariableReference : IEReference
        {
            protected readonly IEEngine _engine;
            protected readonly IEEngine.Variable _variable;

            public VariableReference(IEEngine engine, IEEngine.Variable variable)
            {
                this._engine = engine;
                this._variable = variable;
            }

            public override T Get<T>()
            {
                return this._engine.ExportValue<T>(this.Get());
            }

            public override IEEngine.Value Get()
            {
                return this._variable.Content;
            }

            public override void Set(IEEngine.Value value)
            {
                this._variable.Content = value;
            }

            public override void Set(object value)
            {
                this.Set(this._engine.ImportValue(value));
            }
        }
    }
}
