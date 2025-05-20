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
    public void IntegrationWorks()
    {
        new ScriptedTest().Run("""
            Assert.Pass()
        """);
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

        Assert.That(tester.engine.Integration.ExportValue<TestClass.StateType>(state), Is.EqualTo(TestClass.StateType.Wrong));
    }
}
