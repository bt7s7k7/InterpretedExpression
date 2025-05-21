namespace InterEx.Modules;

public readonly record struct ModuleLoadInfo(string Name, IEDocument Document);

public interface IModuleLoader
{
    public bool TryLoadModule(ImportLib context, Module importer, string name, out ModuleLoadInfo loadInfo);

    public static bool ResolveRelativeImportUsingProtocol(string name, Module importer, string protocol, out string targetPath)
    {
        var prefix = protocol + "://";

        if (name.StartsWith(prefix))
        {
            targetPath = Path.Combine(name[(prefix.Length - 1)..]);
        }
        else if (importer != null && importer.Name.StartsWith(prefix) && name.StartsWith("./"))
        {
            targetPath = Path.Combine(Path.GetDirectoryName(importer.Name[(prefix.Length - 1)..]), name);
        }
        else
        {
            targetPath = default;
            return false;
        }

        return true;
    }
}

