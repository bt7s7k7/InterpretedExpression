// Operators use a different naming scheme to prevent collisions
#pragma warning disable IDE1006

using System;
using System.Collections;
using System.Globalization;

namespace InterEx
{
    public sealed partial class IEOperators
    {
        public static double add(double a, double b) => a + b;
        public static double sub(double a, double b) => a - b;
        public static double mul(double a, double b) => a * b;
        public static double div(double a, double b) => a / b;
        public static double neg(double a) => -a;

        public static bool lt(double a, double b) => a < b;
        public static bool gt(double a, double b) => a > b;
        public static bool lte(double a, double b) => a <= b;
        public static bool gte(double a, double b) => a >= b;

        public static bool eq(object a, object b) => a.Equals(b);
        public static bool neq(object a, object b) => !a.Equals(b);

        public static double length(ICollection list) => list.Count;

        public static string @string(string a) => a;
        public static string @string(object a) => a == null ? "null" : a.ToString();

        public static bool @bool(bool a) => a;
        public static bool not(bool a) => !a;
        public static bool @bool(string a) => a != null && a.Length > 0;
        public static bool not(string a) => a == null || a.Length == 0;
        public static bool @bool(double a) => a != 0;
        public static bool not(double a) => a == 0;
        public static bool @bool(object a) => a != null;
        public static bool not(object a) => a == null;

        public static double number(double a) => a;
        public static double number(string a) => Double.Parse(a, CultureInfo.InvariantCulture);
        public static double number(bool a) => a ? 1 : 0;
        public static double number(object _) => Double.NaN;

        public static IEEngine.Value k_Then(Statement predicateStmt, IEEngine engine, Statement exprStmt, IEEngine.Scope scope)
        {
            var predicate = engine.Evaluate(predicateStmt, scope);
            var predicateBool = engine.Invoke(predicate, null, "bool", Array.Empty<IEEngine.Value>());
            var predicateValue = engine.ExportValue<bool>(predicateBool);

            if (predicateValue)
            {
                var innerScope = scope.MakeChild();
                return engine.Evaluate(exprStmt, innerScope);
            }
            else
            {
                return predicate;
            }
        }

        public static IEEngine.Value k_Else(Statement predicateStmt, IEEngine engine, Statement exprStmt, IEEngine.Scope scope)
        {
            var predicate = engine.Evaluate(predicateStmt, scope);
            var predicateBool = engine.Invoke(predicate, null, "bool", Array.Empty<IEEngine.Value>());
            var predicateValue = engine.ExportValue<bool>(predicateBool);

            if (!predicateValue)
            {
                var innerScope = scope.MakeChild();
                return engine.Evaluate(exprStmt, innerScope);
            }
            else
            {
                return predicate;
            }
        }

        public static void k_While(Statement predicateStmt, IEEngine engine, Statement exprStmt, IEEngine.Scope scope)
        {
            var innerScope = scope.MakeChild();

            while (true)
            {
                var predicate = engine.Evaluate(predicateStmt, scope);
                var predicateBool = engine.Invoke(predicate, null, "bool", Array.Empty<IEEngine.Value>());
                var predicateValue = engine.ExportValue<bool>(predicateBool);
                if (!predicateValue) break;

                engine.Evaluate(exprStmt, innerScope);
            }
        }

        public static void forEach(IEnumerable enumerable, IEFunction callback)
        {
            var i = 0;
            foreach (var element in enumerable)
            {
                callback.Invoke(element, i);
                i++;
            }
        }
    }
}
