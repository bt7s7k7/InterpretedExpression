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

            test.Func1 = ^(a) { a.add(1) }

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
            GLOBAL.at("value") = 10
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
                (value.eq(1)) "one"
                (value.eq(5)) "five"
            ), "five")

            AssertEqual(k_If(
                (value.eq(1)) "one"
                (value.eq(6)) "six"
                "missing"
            ), "missing")

            AssertEqual(k_If(
                "default"
            ), "default")

            AssertEqual(k_If(
                (value.eq(1)) "one"
                (value.eq(6)) "six"
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
}
