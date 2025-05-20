using System.Runtime.CompilerServices;
using InterEx;
using InterEx.Integration;
using InterEx.InterfaceTypes;

namespace Test;

public class ScriptedTest
{
    public IEEngine engine;

    public Value Run(string code, [CallerFilePath] string file = null)
    {
        var document = IEDocument.ParseCode(file, code);
        return this.engine.Evaluate(document.Root, this.engine.PrepareCall());
    }

    public ScriptedTest()
    {

        if (_integrationManager == null)
        {
            _integrationManager = new IEIntegrationManager();
            var provider = ReflectionValueProvider.CreateAndRegister(_integrationManager);

            provider.AddAllAssemblies();
            provider.Using(provider.Global.Members["NUnit"].Members["Framework"]);
            _integrationManager.AddExporter(new NUnitExporter());
        }

        this.engine = new IEEngine(_integrationManager);
        this.engine.AddGlobal("AssertEqual", (object a, object b) => Assert.That(a, Is.EqualTo(b)));
    }

    private static IEIntegrationManager _integrationManager;

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
