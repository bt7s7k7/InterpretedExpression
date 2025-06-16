# Interpreted Expression

```cs
System.Console.WriteLine("Hello World")
```

InterEx is a **minimalist, dynamic and embeddable** programming language designed to configure C# applications. Through its (almost) zero-config **interop with any C# libraries** it makes allows for easy runtime configuration of applications, creation of plugins and mods or definition of high-level workflows or data-pipelines.

InterEx is made with the philosophy of only including the most basic features and leaving all other features to be **customized by the user** using modules. Most programming is calling functions and passing the results to other functions and that is what InterEx does well. All other language features are implemented through this simple framework, with only a small amount of syntax sugar.

But this also allows for a large amount of customization. InterEx includes a **full AST  metaprogramming** functionality, allowing the user to create all desired constructs without modifying the language internals.

# Example

```cs
k_Using(System)
k_Using(System.Text)
k_Using(System.Collections.Generic)

$list = List(String)().init("a", "b", "c")

Console.WriteLine(String.Join(", ", list))

$dictionary = Dictionary(String, Int32)().init({ q: 5, a: 10, z: 21 })

dictionary.forEach(^(kv) {
    Console.WriteLine(StringBuilder()
        .Append(kv.Key)
        .Append(": ")
        .Append(kv.Value)
    )
})

$test = TestClass()

test.Action2 = ^(stringValue, doubleValue) {
    Console.WriteLine(stringValue)
    Console.WriteLine(doubleValue)
}

test.Action2.Invoke("a", 5)

test.Func1 = ^(a) { a + 1 }

test.Func1.Invoke(1)
```

Check out tests contained in `InterExTests.cs` to view additional examples.

# Usage

Currently the best way to install this library is to copy its source files in the `inter_ex` folder.

```cs
var engine = new IEEngine();
engine.Integration.EntityProvider.LoadAllAssemblies();

try
{
    var document = new IEParser("path/to/file.ie", fileContent).Parse();
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
```

Check `Program.cs` for a simple script-runner and REPL.

# Guide

## Syntax

Define a variable using the `$` operator.

```cs
$val = 5
```

Later references to variables do not use `$`, only their name. You can call a function using `()`.

```cs
func(val)
```

You can access a method or a property.

```cs
instance.Value = 10
instance.Method("String")
```

You can define delegates using the `^` operator. The last expression contained within is used as the return value.

```cs
^(param1, param2) { param1.Use(param2) }
^{ /* Zero parameter delegate */ }
```

The are also literals for numbers, strings, arrays and dictionaries.

```cs
"string literal"
$"interpolated literal ${52:F2}"
350420
[elem1, elem2, elem3]
{ key: value, key2: 58 }
```

## Operators

Operators are mapped to methods defined in the `IEOperators` class. This class is partial and it is expected that a user will expand it with overloads and additional operators related to their use-case. All methods on `IEOperators` are available and all objects, where the receiver is instead used as the first argument.

There is also special syntax defined for calling a selection of operators. The following operators have special syntax:

| Operator | Function |
| ---------| -------- |
| (unary) `-` | `neg` |
| (unary) `!` | `not` |
| `=` | (assignment)  |
| `&&` | `k_Then` |
| `\|\|` | `k_Else` |
| `<` | `lt` |
| `<=` | `lte` |
| `>` | `gt` |
| `>=` | `gte` |
| `==` | `eq` |
| `!=` | `neq` |
| `+` | `add` |
| `-` | `sub` |
| `*` | `mul` |
| `/` | `div` |
| `%` | `mod` |
| `.` | (member access) |
| `()` | (invocation) |
| `[]` | (index) |

Several operators have a special implementation that does not map to a function but creates a custom syntax construct. Details can be found in the `IEParser.ParseExpression()` method. It is also worth mentioning that, because `k_Then` and `k_Else` are keyword functions, they can not be overloaded.

## Metaprogramming

More complex language features are created by using metaprogramming. This is done through so called keyword functions, which are functions prefixed by `k_`. These functions are called with AST trees instead of evaluated values which allows for definition of any other language features by the user.

```cs
k_Using(System)

$i = 0
$len = 10

i.lt(len).k_While((
    Console.WriteLine(i)
    i = i.add(1)
))
```

## C# Interop

InterEx can perform all operations on any provided C# object. Executing the `EntityProvider.LoadAllAssemblies()` method allows you to import C# namespaces and use their classes directly.

InterEx is a dynamic language. That means the types of variables and function calls is only checked at runtime. To make this more simple for a user, InterEx uses simpler internal values which are then converted into C# types on use. For example instead of all C#'s number types, InterEx just has a single number type representing a double.

Interop with C# happens through the import/export process where internal values are converted based on the desired C# type. InterEx defines importers and exporters for simple values but allows the user to easily create their own, check `IntrinsicSource.cs` for how to do it. For all unsupported types, the values are simply preserved.

Function calls to C# methods are performed using reflection. All reflection objects are cached for performance. This process can also be customized by the user, for example allowing you to add a custom method to a specific class or interface through a `ClassPatcher` delegate.

# Editor Integration

Syntax highlighting extension is available for VSCode, inside the `extension` folder. You can install it using the `workbench.extensions.action.installExtensionFromLocation` command.
