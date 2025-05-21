namespace InterEx.Modules;

[Serializable]
public class ModuleLoadException : IERuntimeException
{
    public ModuleLoadException() { }
    public ModuleLoadException(string message) : base(message) { }
    public ModuleLoadException(string message, Exception inner) : base(message, inner) { }
}
