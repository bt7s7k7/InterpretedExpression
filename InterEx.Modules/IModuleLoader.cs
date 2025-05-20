namespace InterEx.Modules;

public interface IModuleLoader
{
    public bool TryLoadModule(ImportLib context, Module importer, string name, out Module module);
}

