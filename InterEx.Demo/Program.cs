﻿using System;
using System.IO;
using InterEx;

var engine = new IEEngine();

var path = "../example/sample.ie";

engine.Integration.EntityProvider.LoadAllAssemblies();

var argv = Environment.GetCommandLineArgs();
Console.WriteLine(String.Join(", ", argv));

if (argv is [_, "repl"])
{
    var readline = new Readline();
    readline.OnLine += (input) =>
    {
        try
        {
            var document = IEDocument.ParseCode("anon", input);
            Console.WriteLine(document.ToJson());

            var result = engine.Evaluate(document.Root, engine.PrepareCall());
            Console.WriteLine(result.ToString());
        }
        catch (IEParsingException error)
        {
            Console.WriteLine("[SYN] " + error.Message);
        }
        catch (IERuntimeException error)
        {
            Console.WriteLine("[ERR] " + error.FlattenMessage());
        }
    };

    readline.Run();
}
else if (argv is [_, "loop"])
{
    while (true)
    {
        var input = File.ReadAllText(path);
        try
        {
            var document = IEDocument.ParseCode(path, input);
            Console.WriteLine(document.ToJson());

            var result = engine.Evaluate(document.Root, engine.PrepareCall());
            Console.WriteLine(result.ToString());
        }
        catch (IEParsingException error)
        {
            Console.WriteLine("[SYN] " + error.Message);
        }
        catch (IERuntimeException error)
        {
            Console.WriteLine("[ERR] " + error.FlattenMessage());
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
        Console.WriteLine(result.ToString());
    }
    catch (IEParsingException error)
    {
        Console.WriteLine("[SYN] " + error.Message);
    }
    catch (IERuntimeException error)
    {
        Console.WriteLine("[ERR] " + error.FlattenMessage());
    }
}
