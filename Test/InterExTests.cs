using InterEx;

namespace Test;

public class InterExTests
{
    [SetUp]
    public void SetUp()
    {
        _ = new TestClass();
        _ = new ScriptedTest();
    }

    [Test]
    public void Delegates()
    {
        new ScriptedTest().Run("""
            k_Using(System)

            $test = TestClass()

            $executed = false

            test.Action2 = ^(string, double) {
                AssertEqual(string, "a")
                AssertEqual(double, 5)
                executed = true
            }

            test.Action2("a", 5)
            AssertEqual(executed, true)

            test.Func1 = ^(a) a + 1

            AssertEqual(test.Func1(1), 2)
        """);
    }

    [Test]
    public void Collections()
    {
        new ScriptedTest().Run("""
            k_Using(System)
            k_Using(System.Text)
            k_Using(System.Collections.Generic)

            $list = List(String)().init("a", "b", "c")

            AssertEqual(String.Join(", ", list), "a, b, c")

            $arrayList = System.Collections.ArrayList().init("a", "b", "c")
            AssertEqual(String.Join(", ", arrayList), "a, b, c")

            $dictionary = Dictionary(String, Int32)().init({ q: 5, a: 10, z: 21 })

            $builder = StringBuilder()
            dictionary.forEach(^(kv) {
                builder
                    .Append(kv.Key)
                    .Append(": ")
                    .Append(kv.Value)
                    .Append(", ")
            })

            AssertEqual(builder.ToString(), "q: 5, a: 10, z: 21, ")
        """);
    }

    [Test]
    public void Enums()
    {
        var tester = new ScriptedTest();
        var state = tester.Run("""
            k_Using(System)

            $test = TestClass()

            test.State = "Wrong"
            test.State
        """);

        Assert.That(tester.Engine.Integration.ExportValue<TestClass.StateType>(state), Is.EqualTo(TestClass.StateType.Wrong));
    }

    [Test]
    public void Declaration()
    {
        new ScriptedTest().Run("""
            $foo = new()

            Assert.Throws(InterEx.IERuntimeException, ^{
                foo.name = "Hello"
            })

            foo.$name = "Hello"

            AssertEqual(foo.name, "Hello")

            $executed = false

            foo._decl("value", ^{ executed = true, 52 }, null)

            AssertEqual(foo.value, 52)
            AssertEqual(executed, true)

            Assert.Throws(InterEx.IERuntimeException, ^{
                foo.value = 0
            })

            $variable = 1
            foo._bind("variable", k_Ref(variable))

            AssertEqual(foo.variable, variable)
            variable = 5
            AssertEqual(foo.variable, variable)
            foo.variable = 10
            AssertEqual(foo.variable, variable)
        """);
    }

    [Test]
    public void Using()
    {
        var tester = new ScriptedTest();
        tester.Run("""
            k_Using(System.Text)

            AssertEqual(StringBuilder().Append(128).ToString(), "128")
        """);

        Assert.Throws<IERuntimeException>(() =>
        {
            tester.Run("""
                StringBuilder()
            """);
        });

        tester.Run("""
            k_Using(System.Text)
            k_Using(System)

            $closure = ^{
                AssertEqual(StringBuilder().Append(128).ToString(), "128")
            }

            closure()
        """);
    }

    [Test]
    public void Modules()
    {
        new ScriptedTest().RunModule("""
            $testModule = import("./testModule.ie")
            AssertEqual(testModule.value, 58)

            $testModuleAgain = import("./testModule.ie")
            AssertEqual(testModule.unique, testModuleAgain.unique)
        """);
    }

    [Test]
    public void TemplateString()
    {
        new ScriptedTest().Run("""
            AssertEqual($"hello", "hello")
            AssertEqual($"${52}", "52")
            AssertEqual($"${52:F2}", "52.00")
            AssertEqual($"the number ${0:F2} has two", "the number 0.00 has two")
        """);
    }

    [Test]
    public void ScopeManipulation()
    {
        var tester = new ScriptedTest();

        tester.Run("""
            GLOBAL["value"] = 10
        """);

        Assert.That(tester.Engine.Integration.ExportValue<double>(tester.Run("""
            value
        """)), Is.EqualTo(10.0));
    }

    [Test]
    public void If()
    {
        var tester = new ScriptedTest();

        tester.Run("""
            $value = 5

            AssertEqual(k_If(
                (value == 1) "one"
                (value == 5) "five"
            ), "five")

            AssertEqual(k_If(
                (value == 1) "one"
                (value == 6) "six"
                "missing"
            ), "missing")

            AssertEqual(k_If(
                "default"
            ), "default")

            AssertEqual(k_If(
                (value == 1) "one"
                (value == 6) "six"
            ), null)
        """);
    }

    [Test]
    public void Switch()
    {
        var tester = new ScriptedTest();

        tester.Run("""
            AssertEqual(k_Switch(5,
                (1) "one"
                (5) "five"
            ), "five")

            AssertEqual(k_Switch(6,
                (1) "one"
                (5) "five"
                "missing"
            ), "missing")

            AssertEqual(k_Switch(6,
                "default"
            ), "default")

            AssertEqual(k_Switch(6,
                (1) "one"
                (5) "five"
            ), null)
        """);
    }

    [Test]
    public void ScriptedInterface()
    {
        var runner = new ScriptedTest();
        var scope = runner.Engine.PrepareCall();
        var resultValue = runner.Run("""
            $value = 0
            $impl = new({
                get_Number: ^value
                set_Number: ^(v) value = v
                Get1: ^1
                Invoke2: ^(callback) callback()
            })

            impl
        """, scope: scope);

        var result = runner.Engine.Integration.ExportValue<ITargetInterface>(resultValue);

        Assert.That(result.Get1(), Is.EqualTo(1));

        Assert.Throws<NotImplementedException>(() =>
        {
            result.Get2();
        });

        result.Invoke1();

        var executed = false;
        result.Invoke2(() => { executed = true; });
        Assert.That(executed, Is.True);

        Assert.That(result.Number, Is.EqualTo(0));
        result.Number = 1;
        Assert.That(result.Number, Is.EqualTo(1));

        Assert.That(result.DefaultValue, Is.EqualTo(10));

        runner.Run("""
            AssertEqual(ITargetInterface(impl).Number, 1)
        """, scope: scope);
    }

    [Test]
    public void MethodGroup()
    {
        var tester = new ScriptedTest();

        tester.Run("""
            $method = TestClass.GetString

            AssertEqual(method("foo"), "foo")
            AssertEqual(method(), "default")
        """);
    }

    [Test]
    public void Operators()
    {
        var tester = new ScriptedTest();

        tester.Run("""
            AssertEqual(1 * 2 + 3, 5)
            AssertEqual(1 + 2 * 3, 7)
            AssertEqual(1 == 2, false)
            AssertEqual(1 != 2, true)

            $a = 1
            $b = 2

            a = b = 3
            AssertEqual(a, 3)
            AssertEqual(b, 3)

            false && (a = 4)
            AssertEqual(a, 3)
            true || (a = 5)
            AssertEqual(a, 3)
            false || (a = 5)
            AssertEqual(a, 5)

            $list = [1, 2, 3]
            AssertEqual(list[1], 2)
            list[1] = 5
            AssertEqual(list[1], 5)
        """);
    }

    [Test]
    public void GenericMethods()
    {
        new ScriptedTest().RunModule("""
            AssertEqual(TestClass.Typed(System.Double, 1), "1")
        """);

        Assert.Throws(
            Is.TypeOf<IERuntimeException>()
            .And.Message.Contain("Cannot convert value System.Double into System.Type"),
            () =>
            {
                new ScriptedTest().RunModule("""
                    AssertEqual(TestClass.Typed(1), "1")
                """);
            }
        );

        Assert.Throws(
            Is.TypeOf<IERuntimeException>()
            .And.Message.Contain("Argument count mismatch, got 0, but expected 1 (System.Type)"),
            () =>
            {
                new ScriptedTest().RunModule("""
                    AssertEqual(TestClass.Typed(), "1")
                """);
            }
        );

        Assert.Throws(
            Is.TypeOf<IERuntimeException>()
            .And.Message.Contain("Argument count mismatch, got 0, but expected 1 (System.Double)"),
            () =>
            {
                new ScriptedTest().RunModule("""
                    AssertEqual(TestClass.Typed(System.Double), "1")
                """);
            }
        );

        Assert.Throws(
            Is.TypeOf<IERuntimeException>()
            .And.Message.Contain("Argument count mismatch, got 2, but expected 1 (System.Double)"),
            () =>
            {
                new ScriptedTest().RunModule("""
                    AssertEqual(TestClass.Typed(System.Double, 1, 0), "1")
                """);
            }
        );
    }

    [Test]
    public void ScopeAccess()
    {
        new ScriptedTest().RunModule("""
            SCOPE.Declare("a").Content = 5
            AssertEqual(a, 5)

            $SCOPE = 5
            AssertEqual(SCOPE, 5)
        """);
    }
}
