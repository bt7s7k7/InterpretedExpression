using System.Runtime.CompilerServices;
using InterEx;
using InterEx.CompilerInternals;
using InterEx.Integration;
using InterEx.InterfaceTypes;
using InterEx.Modules;

namespace Test;

public class ScriptedTest
{
    public IEEngine Engine;
    public ImportLib ImportLib;

    public Value Run(string code, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0, Scope scope = null)
    {
        var document = IEDocument.ParseCode(file, new String('\n', line) + code);
        return this.Engine.Evaluate(document.Root, scope ?? this.Engine.PrepareCall());
    }

    public Module RunModule(string code, [CallerFilePath] string file = null)
    {
        var document = IEDocument.ParseCode(file, code);
        var module = new Module("file://" + file);
        module.Load(this.ImportLib, document);
        return module;
    }

    public ScriptedTest()
    {

        if (_integrationManager == null)
        {
            _integrationManager = new IEIntegrationManager();
            _provider = ReflectionValueProvider.CreateAndRegister(_integrationManager);

            _provider.AddAllAssemblies();
            _integrationManager.AddExporter(new NUnitExporter());
        }

        this.Engine = new IEEngine(_integrationManager);
        this.Engine.AddGlobal("AssertEqual", (object a, object b) => Assert.That(a, Is.EqualTo(b)));

        ReflectionValueProvider.ImportFromNamespace(_provider.Global.Members["NUnit"].Members["Framework"], this.Engine.GlobalScope);

        var importLib = IEModulesInitializer.Initialize(this.Engine);
        importLib.Loaders.Add(new FileSystemModuleLoader());
        this.ImportLib = importLib;
    }

    private static IEIntegrationManager _integrationManager;
    private static ReflectionValueProvider _provider;

    private class NUnitExporter : IValueExporter
    {
        public bool Export(IEIntegrationManager integration, Value value, Type type, out object data)
        {
            if (value.Content is string text && type == typeof(NUnitString))
            {
                data = new NUnitString(text);
                return true;
            }

            data = default;
            return false;
        }
    }
}
