using System;
using System.Collections.Generic;

namespace InterEx
{
    public abstract record class Statement(IEPosition Position)
    {
        public sealed record class StringLiteral(IEPosition Position, string Value) : Statement(Position) { public const string Kind = "string"; };
        public sealed record class NumberLiteral(IEPosition Position, double Value) : Statement(Position) { public const string Kind = "number"; }
        public sealed record class ObjectLiteral(IEPosition Position, Dictionary<string, Statement> Properties) : Statement(Position) { public const string Kind = "object"; }
        public sealed record class VariableAccess(IEPosition Position, string Name) : Statement(Position) { public const string Kind = "var"; }
        public sealed record class VariableDeclaration(IEPosition Position, string Name) : Statement(Position) { public const string Kind = "define"; }
        public sealed record class Assignment(IEPosition Position, Statement Receiver, Statement Value) : Statement(Position) { public const string Kind = "set"; }
        public sealed record class MemberAccess(IEPosition Position, Statement Receiver, string Member) : Statement(Position) { public const string Kind = "member"; }
        public sealed record class Group(IEPosition Position, List<Statement> Statements) : Statement(Position) { public const string Kind = "group"; }
        public sealed record class FunctionDeclaration(IEPosition Position, List<string> Parameters, List<Statement> Body) : Statement(Position) { public const string Kind = "function"; }

        public sealed record class Invocation(IEPosition Position, Statement Receiver, string Function, List<Statement> Arguments) : Statement(Position)
        {
            public const string Kind = "call";

            public record class DispatchCache(
                Type ReceiverType,
                Type[] Parameters,
                ReflectionCache.FunctionInfo Target
            );

            public int DeoptimizeCounter = 0;
            public DispatchCache CachedCall = null;
        }
    }
}
