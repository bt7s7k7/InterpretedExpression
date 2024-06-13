using System;
using System.IO;
using InterEx;

var engine = new IEEngine();

var path = "./example/sample.ie";

engine.AddProviderFallback(new ReflectionValueProvider().AddAllAssemblies());

if (Environment.GetCommandLineArgs() is ["repl"])
{
    while (true)
    {
        var input = Console.ReadLine();
        try
        {
            var document = IEDocument.ParseCode("anon", input);
            Console.WriteLine(document.ToJson());

            var result = engine.Evaluate(document.Root, engine.PrepareCall());
            Console.WriteLine(result.Content ?? "null");
        }
        catch (IEParsingException error)
        {
            Console.WriteLine("[SYN] " + error.Message);
        }
        catch (IERuntimeException error)
        {
            Console.WriteLine("[ERR] " + error.Message);
            var inner = error.InnerException;
            while (inner != null)
            {
                Console.WriteLine("    " + inner.Message);
                inner = inner.InnerException;
            }
        }
    }
}
else if (Environment.GetCommandLineArgs() is ["loop"])
{
    while (true)
    {
        var input = File.ReadAllText(path);
        try
        {
            var document = IEDocument.ParseCode(path, input);
            Console.WriteLine(document.ToJson());

            var result = engine.Evaluate(document.Root, engine.PrepareCall());
            Console.WriteLine(result.Content ?? "null");
        }
        catch (IEParsingException error)
        {
            Console.WriteLine("[SYN] " + error.Message);
        }
        catch (IERuntimeException error)
        {
            Console.WriteLine("[ERR] " + error.Message);
            var inner = error.InnerException;
            while (inner != null)
            {
                Console.WriteLine("    " + inner.Message);
                inner = inner.InnerException;
            }
        }

        Console.ReadKey();
    }
}
else
{
    var input = File.ReadAllText(path);

    try
    {
        var document = IEDocument.ParseCode(path, input);
        Console.WriteLine(document.ToJson());

        var result = engine.Evaluate(document.Root, engine.PrepareCall());
    }
    catch (IEParsingException error)
    {
        Console.WriteLine("[SYN] " + error.Message);
    }
    catch (IERuntimeException error)
    {
        Console.WriteLine("[ERR] " + error.Message);
    }
}
