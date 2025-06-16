using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using InterEx.CompilerInternals;

namespace InterEx;

public class IEParser(string path, string input)
{
    public enum OperatorType
    {
        Invocation, Assignment, MemberAccess
    }
    public record class Operator(int Precedence, OperatorType Type, string Name)
    {
        public Operator(int precedence, string name) : this(precedence, OperatorType.Invocation, name) { }
        public Operator(int precedence, OperatorType type) : this(precedence, type, null) { }

        public int ResultPrecedence = Precedence + 1;
    };

    public const int PREFIX = Int32.MaxValue;
    public static readonly Dictionary<string, Operator> PrefixOperators = new()
    {
        ["-"] = new Operator(10, "neg"),
        ["!"] = new Operator(10, "not"),
        ["+"] = new Operator(10, "number"),
    };
    public static readonly Dictionary<string, Operator> InfixOperators = new()
    {
        ["="] = new Operator(0, OperatorType.Assignment) { ResultPrecedence = 0 },

        ["&&"] = new Operator(1, "k_Then"),
        ["||"] = new Operator(1, "k_Else"),

        ["<"] = new Operator(2, "lt"),
        ["<="] = new Operator(2, "lte"),
        [">"] = new Operator(2, "gt"),
        [">="] = new Operator(2, "gte"),
        ["=="] = new Operator(2, "eq"),
        ["!="] = new Operator(2, "neq"),

        ["+"] = new Operator(3, "add"),
        ["-"] = new Operator(3, "sub"),

        ["*"] = new Operator(4, "mul"),
        ["/"] = new Operator(4, "div"),
        ["%"] = new Operator(4, "mod"),

        ["**"] = new Operator(5, "pow") { ResultPrecedence = 5 },

        ["."] = new Operator(100, OperatorType.MemberAccess),
    };
    public static readonly List<string> OperatorTokens;
    static IEParser()
    {
        OperatorTokens = PrefixOperators.Keys.Concat(InfixOperators.Keys).Distinct().OrderBy(v => -v.Length).ToList();
    }

    public string Path = path;
    public string Input = input;
    public int Index = 0;

    public char Current => this.Input[this.Index];
    public bool IsDone => this.Index >= this.Input.Length;

    protected bool _skippedNewline = false;
    protected int _lastSkippedIndex = -1;

    public IEPosition GetPosition() => new IEPosition(this.Path, this.Input, this.Index);
    public IEPosition GetPosition(int index) => new IEPosition(this.Path, this.Input, index);

    public IEParsingException Abort(string message, IEPosition position) => new IEParsingException(position.Format(message));
    public IEParsingException Abort(string message, int index) => new IEParsingException(this.GetPosition(index).Format(message));
    public IEParsingException Abort(string message) => new IEParsingException(this.GetPosition().Format(message));

    public bool Matches(string value)
    {
        if (this.Index + value.Length > this.Input.Length) return false;
        return this.Input.AsSpan(this.Index, value.Length).Equals(value, StringComparison.Ordinal);
    }

    public bool Consume(string value)
    {
        if (this.Matches(value))
        {
            this.Index += value.Length;
            return true;
        }

        return false;
    }

    public ReadOnlySpan<char> ConsumeWord()
    {
        return this.ReadWhile(() => this.Current is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_');
    }

    public void SkipWhile(Func<bool> predicate)
    {
        while (!this.IsDone)
        {
            if (!predicate())
            {
                return;
            }

            this.Index++;
        }
    }

    public ReadOnlySpan<char> ReadWhile(Func<bool> predicate)
    {
        var start = this.Index;
        this.SkipWhile(predicate);
        var end = this.Index;
        return this.Input.AsSpan(start, end - start);
    }

    protected Statement _token = null;
    public Statement PeekToken(bool operatorOnly = false)
    {
        if (this._token == null)
        {
            return this.NextToken(operatorOnly);
        }

        return this._token;
    }

    public void SkipWhitespace()
    {
        if (this.Index == this._lastSkippedIndex) return;

        var currSkippedNewLine = false;
        while (!this.IsDone)
        {
            this.SkipWhile(() => this.Current is ' ' or '\t');
            if (this.IsDone) break;
            if (this.Current is '\n' or '\r')
            {
                currSkippedNewLine = true;
                this.Index++;
                continue;
            }

            if (this.Consume("//"))
            {
                this.SkipWhile(() => this.Current is not '\n');
                continue;
            }

            if (this.Consume("/*"))
            {
                var depth = 1;

                while (!this.IsDone && depth > 0)
                {
                    if (this.Consume("/*"))
                    {
                        depth++;
                        continue;
                    }

                    if (this.Consume("*/"))
                    {
                        depth--;
                        continue;
                    }

                    this.Index++;
                }

                continue;
            }

            break;
        }

        this._lastSkippedIndex = this.Index;
        this._skippedNewline = currSkippedNewLine;
    }

    public char ParseEscapeSequence()
    {
        if (this.IsDone) throw this.Abort("Unexpected EOF");

        var e = this.Current;
        this.Index++;
        if (e == 'x')
        {
            var charStart = this.Index;
            this.Index++;
            if (this.IsDone) throw this.Abort("Unexpected EOF");
            this.Index++;
            if (!Byte.TryParse(this.Input.AsSpan(charStart, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var charValue))
            {
                throw this.Abort("Invalid number");
            }
            return (char)charValue;
        }

        return e switch
        {
            'n' => '\n',
            'r' => '\r',
            't' => '\t',
            '\'' => '\'',
            '"' => '"',
            '`' => '`',
            '\\' => '\\',
            '$' => '$',
            _ => throw this.Abort("Invalid escape character")
        };
    }

    public Statement ParseString(char term)
    {
        var value = new StringBuilder();
        var start = this.Index - 1;

        while (!this.IsDone)
        {
            var c = this.Current;
            this.Index++;

            if (c == term) break;
            if (c == '\\')
            {
                value.Append(this.ParseEscapeSequence());
                continue;
            }

            value.Append(c);
        }

        return new Statement.StringLiteral(this.GetPosition(start), value.ToString());
    }

    public Statement ParseTemplate(char term)
    {
        var template = new Statement.TemplateLiteral(this.GetPosition(this.Index - 1), []);
        var fragmentStart = this.Index;
        StringBuilder fragment = null;

        while (!this.IsDone)
        {
            var c = this.Current;
            this.Index++;

            if (c == term) break;
            if (c == '\\')
            {
                if (fragment == null)
                {
                    fragment = new();
                    fragmentStart = this.Index - 1;
                }

                fragment.Append(this.ParseEscapeSequence());
                continue;
            }

            if (c == '$' && !this.IsDone && this.Current == '{')
            {
                this.Index++;
                var statements = this.ParseBlock("}", ":");
                var statementStart = this.Index;

                string formatString = null;
                if (this.Input[this.Index - 1] == ':')
                {
                    formatString = new String(this.ReadWhile(() => this.Current != '}'));
                    this.Index++;
                }

                if (statements.Count == 0) continue;

                if (fragment != null)
                {
                    template.Fragments.Add((new Statement.StringLiteral(this.GetPosition(fragmentStart), fragment.ToString()), null));
                    fragment = null;
                }

                if (statements.Count == 1)
                {
                    template.Fragments.Add((statements[0], formatString));
                }
                else
                {
                    template.Fragments.Add((new Statement.Group(this.GetPosition(statementStart), statements), formatString));
                }

                continue;
            }

            if (fragment == null)
            {
                fragment = new();
                fragmentStart = this.Index - 1;
            }

            fragment.Append(c);
        }

        if (fragment != null)
        {
            template.Fragments.Add((new Statement.StringLiteral(this.GetPosition(fragmentStart), fragment.ToString()), null));
            fragment = null;
        }

        return template;
    }

    public Statement NextToken(bool operatorOnly = false)
    {
        this.SkipWhitespace();

        var skippedNewline = this._skippedNewline;

        if (this.IsDone)
        {
            return this._token = null;
        }

        var start = this.Index;
        foreach (var token in OperatorTokens)
        {
            if (this.Consume(token)) return this._token = new Statement.Operator(this.GetPosition(start), token);
        }

        if (this.Consume("("))
        {
            this._token = new Statement.Group(this.GetPosition(start), this.ParseBlock(")"));
            this._skippedNewline = skippedNewline;
            return this._token;
        }

        if (this.Consume("["))
        {
            this._token = new Statement.Indexer(this.GetPosition(start), this.ParseBlock("]"));
            this._skippedNewline = skippedNewline;
            return this._token;
        }

        if (operatorOnly)
        {
            return this._token = null;
        }

        if (this.Current is >= '0' and <= '9' or '-')
        {
            var numberText = new StringBuilder();
            if (this.Consume("-")) numberText.Append('-');

            numberText.Append(this.ReadWhile(() => this.Current is >= '0' and <= '9'));

            if (this.Consume("."))
            {
                numberText.Append('.');
                numberText.Append(this.ReadWhile(() => this.Current is >= '0' and <= '9'));
            }

            if (!Double.TryParse(numberText.ToString(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
            {
                throw this.Abort("Invalid number");
            }

            return this._token = new Statement.NumberLiteral(this.GetPosition(start), number);
        }

        if (this.Consume("$\"")) return this._token = this.ParseTemplate('"');
        if (this.Consume("$'")) return this._token = this.ParseTemplate('\'');
        if (this.Consume("$`")) return this._token = this.ParseTemplate('`');

        if (this.Consume("\"")) return this._token = this.ParseString('"');
        if (this.Consume("'")) return this._token = this.ParseString('\'');
        if (this.Consume("'")) return this._token = this.ParseString('`');

        if (this.Consume("$"))
        {
            var name = this.ConsumeWord();
            if (name == "") throw this.Abort("Missing variable name");

            return this._token = new Statement.VariableDeclaration(this.GetPosition(start), name.ToString());
        }

        if (this.Consume("{"))
        {
            var properties = new Dictionary<string, Statement>();

            while (!this.IsDone)
            {
                this.SkipWhitespace();
                if (this.Consume("}")) break;
                if (this.Consume(",")) continue;

                var propStart = this.Index;

                var name = this.ConsumeWord().ToString();
                if (name == "") throw this.Abort("Missing property name");

                this.SkipWhitespace();

                var value = (Statement)null;
                if (this.Consume(":"))
                {
                    this.SkipWhitespace();
                    this._token = null;
                    value = this.ParseExpression();
                }
                else
                {
                    value = new Statement.VariableAccess(this.GetPosition(propStart), name);
                }

                if (properties.ContainsKey(name)) throw this.Abort("Duplicate property", propStart);
                properties.Add(name, value);
            }

            return this._token = new Statement.ObjectLiteral(this.GetPosition(start), properties);
        }

        if (this.Consume("^"))
        {
            var parameters = new List<string>();

            if (this.Consume("("))
            {
                while (!this.IsDone)
                {
                    this.SkipWhitespace();
                    if (this.Consume(")")) break;
                    if (this.Consume(",")) continue;
                    var parameter = this.ConsumeWord();
                    if (parameter.IsEmpty) throw this.Abort("Expected token");
                    parameters.Add(parameter.ToString());
                }
            }

            this.SkipWhitespace();

            List<Statement> body;
            if (this.Consume("{"))
            {
                body = this.ParseBlock("}");
            }
            else
            {
                this._token = null;
                body = [this.ParseExpression()];
            }

            return this._token = new Statement.FunctionDeclaration(this.GetPosition(start), parameters, body);
        }

        var variable = this.ConsumeWord();
        if (variable.IsEmpty) return this._token = null;
        return this._token = new Statement.VariableAccess(this.GetPosition(start), variable.ToString());
    }

    public List<Statement> ParseBlock(params ReadOnlySpan<string> terms)
    {
        this._token = null;
        var result = new List<Statement>();

        while (true)
        {
            if (this.PeekToken() is null)
            {
                this.SkipWhitespace();
                if (this.IsDone) break;

                if (this.Consume(",")) continue;

                if (!terms.IsEmpty)
                {
                    foreach (var term in terms)
                    {
                        if (this.Consume(term)) goto terminate_block;
                    }
                }

                throw this.Abort("Invalid token");
            }

            result.Add(this.ParseExpression());
        }
    terminate_block:

        return result;
    }

    public Statement ParseExpression(int precedence = 0)
    {
        var target = this.PeekToken() ?? throw this.Abort(this.IsDone ? "Unexpected end of input" : "Invalid token");

        if (target is Statement.Operator { Token: var prefixOperatorName })
        {
            if (!PrefixOperators.TryGetValue(prefixOperatorName, out var prefixOperator)) throw this.Abort("Unexpected operator", target.Position);
            this.NextToken();
            var operand = this.ParseExpression(prefixOperator.ResultPrecedence);
            target = new Statement.Invocation(target.Position, operand, prefixOperator.Name, []);
        }
        else
        {
            this.NextToken(operatorOnly: true);
        }

        while (true)
        {
            var next = this.PeekToken(operatorOnly: true);
            if (next is Statement.Operator { Token: var operatorName })
            {
                if (!InfixOperators.TryGetValue(operatorName, out var infixOperator)) throw this.Abort("Unexpected operator", next.Position);
                if (infixOperator.Precedence >= precedence)
                {
                    this.NextToken();
                    var operand = this.ParseExpression(infixOperator.ResultPrecedence);
                    if (infixOperator.Type == OperatorType.Invocation)
                    {
                        target = new Statement.Invocation(next.Position, target, infixOperator.Name, [operand]);
                    }
                    else if (infixOperator.Type == OperatorType.MemberAccess)
                    {
                        if (operand is Statement.VariableAccess member)
                        {
                            target = new Statement.MemberAccess(member.Position, target, member.Name);
                        }
                        else if (operand is Statement.VariableDeclaration memberDecl)
                        {
                            target = new Statement.MemberAccess(memberDecl.Position, target, "$" + memberDecl.Name);
                        }
                        else if (operand is Statement.Group indexer)
                        {
                            target = new Statement.Invocation(next.Position, target, "at", indexer.Statements);
                        }
                        else
                        {
                            throw this.Abort("Expected member name or indexer");
                        }
                    }
                    else if (infixOperator.Type == OperatorType.Assignment)
                    {
                        if (target is Statement.Invocation invocation)
                        {
                            invocation.Arguments.Add(operand);
                            continue;
                        }

                        target = new Statement.Assignment(next.Position, target, operand);
                    }
                }
                else
                {
                    return target;
                }
            }
            else if (!this._skippedNewline && next is Statement.Group group)
            {
                if (precedence > 100) return target;

                var (receiver, method) = (target switch
                {
                    Statement.VariableAccess variable => (null, variable.Name),
                    Statement.MemberAccess access => (access.Receiver, access.Member),
                    _ => (target, "")
                });

                var arguments = group.Statements;
                var position = target.Position;

                target = new Statement.Invocation(position, receiver, method, arguments);
                this.NextToken(operatorOnly: true);
            }
            else if (!this._skippedNewline && next is Statement.Indexer indexer)
            {
                if (precedence > 100) return target;

                var arguments = indexer.Statements;
                var position = next.Position;

                target = new Statement.Invocation(position, target, "at", arguments);
                this.NextToken(operatorOnly: true);
            }
            else
            {
                return target;
            }
        }
    }

    public IEDocument Parse()
    {
        return new IEDocument(this.Path, new Statement.Group(this.GetPosition(), this.ParseBlock()));
    }
}
