using InterEx.CompilerInternals;
using InterEx.Declaration;
using InterEx.InterfaceTypes;

namespace InterEx.Modules;

public class Module(string name)
{
    public Table Exports { get; protected set; }
    public Scope Scope { get; protected set; }

    public readonly string Name = name;
    public bool Loading { get; protected set; } = false;
    public bool Loaded { get; protected set; } = false;

    public void Load(ImportLib context, IEDocument document)
    {
        if (this.Loaded) throw new IERuntimeException($"Duplicate loading of module '{this.Name}'");
        if (this.Loading) throw new IERuntimeException($"Circular reference on '{this.Name}'");

        this.Loading = true;

        this.Scope = context.Engine.PrepareCall();
        this.Exports = new Table();
        this.Exports.DeclareProperty("module", new Value(this));

        this.Scope.Declare("module").Content = new Value(this);
        this.Scope.Declare("import").Content = new Value((string name) =>
        {
            return context.LoadModule(name, this).Exports;
        });

        context.Engine.Evaluate(document.Root, this.Scope);

        this.Loading = false;
        this.Loaded = true;
    }
}
