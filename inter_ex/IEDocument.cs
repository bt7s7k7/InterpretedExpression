using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace InterEx
{
    public partial class IEDocument
    {
        public readonly string Path;
        public readonly Statement.Group Root;

        public string ToJson()
        {
            JsonObject makeObject(Dictionary<string, JsonNode> props)
            {
                return new JsonObject(props);
            }

            JsonArray makeArray(IEnumerable<JsonNode> elements)
            {
                var array = new JsonArray();
                foreach (var element in elements) array.Add(element);
                return array;
            }

            JsonNode visit(Statement statement_1)
            {
                return statement_1 switch
                {
                    Statement.StringLiteral literal => makeObject(new() {
                        {"kind", Statement.StringLiteral.Kind},
                        {"value", literal.Value}
                    }),
                    Statement.NumberLiteral literal => makeObject(new() {
                        {"kind", Statement.NumberLiteral.Kind},
                        {"value", literal.Value}
                    }),
                    Statement.ObjectLiteral literal => makeObject(new() {
                        {"kind", Statement.ObjectLiteral.Kind},
                        {"properties", new JsonObject(literal.Properties.Select(v => new KeyValuePair<string, JsonNode>(v.Key, visit(v.Value))))}
                    }),
                    Statement.VariableAccess statement => makeObject(new() {
                        {"kind", Statement.VariableAccess.Kind},
                        {"name", statement.Name}
                    }),
                    Statement.VariableDeclaration statement => makeObject(new() {
                        {"kind", Statement.VariableDeclaration.Kind},
                        {"name", statement.Name}
                    }),
                    Statement.Assignment statement => makeObject(new() {
                        {"kind", Statement.Assignment.Kind},
                        {"receiver", visit(statement.Receiver)},
                        {"value", visit(statement.Value)},
                    }),
                    Statement.Invocation statement => makeObject(new() {
                        {"kind", Statement.Invocation.Kind},
                        {"receiver", visit(statement.Receiver)},
                        {"function", statement.Function},
                        {"arguments", makeArray(statement.Arguments.Select(visit))}
                    }),
                    Statement.MemberAccess statement => makeObject(new() {
                        {"kind", Statement.MemberAccess.Kind},
                        {"receiver", visit(statement.Receiver)},
                        {"member", statement.Member},
                    }),
                    Statement.Group statement => makeObject(new() {
                        {"kind", Statement.Group.Kind},
                        {"statements", makeArray(statement.Statements.Select(visit))},
                    }),
                    Statement.FunctionDeclaration statement => makeObject(new() {
                        {"kind", Statement.FunctionDeclaration.Kind},
                        {"parameters", makeArray(statement.Parameters.Select(v => JsonValue.Create(v)))},
                        {"body", makeArray(statement.Body.Select(visit))},
                    }),
                    _ => null
                };
            }

            return visit(this.Root).ToJsonString(new()
            {
                WriteIndented = true
            });
        }

        public IEDocument(string path, Statement.Group root)
        {
            this.Path = path;
            this.Root = root;
        }

        public static IEDocument ParseCode(string path, string input)
        {
            int index = 0;

            bool matches(string value)
            {
                if (index + value.Length > input.Length) return false;
                return input[index..(index + value.Length)] == value;
            }

            bool consume(string value)
            {
                if (matches(value))
                {
                    index += value.Length;
                    return true;
                }

                return false;
            }

            string readWhile(Func<bool> predicate)
            {
                var start = index;
                skipWhile(predicate);
                var end = index;
                return input[start..end];
            }

            string consumeWord()
            {
                return readWhile(() =>
                {
                    var curr = input[index];
                    return (
                        (curr >= 'a' && curr <= 'z') ||
                        (curr >= 'A' && curr <= 'Z') ||
                        (curr >= '0' && curr <= '9') ||
                        curr == '_'
                    );
                });
            }

            bool isDone()
            {
                return index >= input.Length;
            }

            void skipWhile(Func<bool> predicate)
            {
                while (!isDone())
                {
                    if (!predicate())
                    {
                        return;
                    }

                    index++;
                }
            }

            var skippedNewline = false;
            var lastSkippedIndex = -1;
            void skipWhitespace()
            {
                if (index == lastSkippedIndex) return;

                var currSkippedNewLine = false;
                while (!isDone())
                {
                    skipWhile(() => input[index] is ' ' or '\t');
                    if (isDone()) break;
                    if (input[index] is '\n' or '\r')
                    {
                        currSkippedNewLine = true;
                        index++;
                        continue;
                    }

                    if (consume("//"))
                    {
                        skipWhile(() => input[index] is not '\n');
                        continue;
                    }

                    if (consume("/*"))
                    {
                        var depth = 1;

                        while (!isDone() && depth > 0)
                        {
                            if (consume("/*"))
                            {
                                depth++;
                                continue;
                            }

                            if (consume("*/"))
                            {
                                depth--;
                                continue;
                            }

                            index++;
                        }

                        continue;
                    }

                    break;
                }

                lastSkippedIndex = index;
                skippedNewline = currSkippedNewLine;
            }

            List<Statement> parseBlock(string term)
            {
                var result = new List<Statement>();

                while (!isDone())
                {
                    skipWhitespace();
                    if (isDone()) break;

                    if (term != null && consume(term)) break;
                    if (consume(",")) continue;
                    result.Add(parseExpression());
                }

                return result;
            }

            string formatException(string message, int index) => new IEPosition(path, input, index).Format(message);

            Statement parseString(char term)
            {
                var value = new StringBuilder();
                var start = index - 1;

                while (!isDone())
                {
                    var c = input[index];
                    index++;

                    if (c == term) break;
                    if (c == '\\')
                    {
                        if (isDone()) throw new IEParsingException(formatException("Unexpected EOF", index));

                        var e = input[index];
                        index++;
                        if (e == 'x')
                        {
                            var a = input[index];
                            index++;
                            if (isDone()) throw new IEParsingException(formatException("Unexpected EOF", index));
                            var b = input[index];
                            index++;
                            if (!UInt16.TryParse(new char[] { a, b }, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var charValue))
                            {
                                throw new IEParsingException(formatException("Invalid number", index));
                            }
                            value.Append((char)charValue);
                            continue;
                        }

                        value.Append(e switch
                        {
                            'n' => '\n',
                            'r' => '\r',
                            't' => '\t',
                            '\'' => '\'',
                            '"' => '"',
                            '`' => '`',
                            '\\' => '\\',
                            _ => throw new IEParsingException(formatException("Invalid escape character", index))
                        });

                        continue;
                    }

                    value.Append(c);
                }

                return new Statement.StringLiteral(new IEPosition(path, input, start), value.ToString());
            }

            Statement parseTarget()
            {
                int start = index;

                if (input[index] is >= '0' and <= '9' or '-')
                {
                    var numberText = "";
                    if (consume("-")) numberText = "-";

                    numberText += readWhile(() => input[index] is >= '0' and <= '9');

                    if (consume("."))
                    {
                        numberText += "." + readWhile(() => input[index] is >= '0' and <= '9');
                    }

                    if (!Double.TryParse(numberText, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
                    {
                        throw new IEParsingException(formatException("Invalid number", index));
                    }

                    return new Statement.NumberLiteral(new IEPosition(path, input, start), number);
                }

                if (consume("\"")) return parseString('"');
                if (consume("'")) return parseString('\'');
                if (consume("`")) return parseString('`');

                if (consume("$"))
                {
                    var name = consumeWord();
                    if (name == "") throw new IEParsingException(formatException("Missing variable name", index));

                    return new Statement.VariableDeclaration(new IEPosition(path, input, start), name);
                }

                if (consume("("))
                {
                    var statements = parseBlock(")");
                    return new Statement.Group(new IEPosition(path, input, start), statements);
                }

                if (consume("{"))
                {
                    var properties = new Dictionary<string, Statement>();

                    while (!isDone())
                    {
                        skipWhitespace();
                        if (consume("}")) break;
                        if (consume(",")) continue;

                        var propStart = index;

                        var name = consumeWord();
                        if (name == "") throw new IEParsingException(formatException("Missing property name", index));

                        skipWhitespace();

                        var value = (Statement)null;
                        if (consume(":"))
                        {
                            skipWhitespace();
                            value = parseExpression();
                        }
                        else
                        {
                            value = new Statement.VariableAccess(new IEPosition(path, input, propStart), name);
                        }

                        if (properties.ContainsKey(name)) throw new IEParsingException(formatException("Duplicate property", propStart));
                        properties.Add(name, value);
                    }

                    return new Statement.ObjectLiteral(new IEPosition(path, input, start), properties);
                }

                if (consume("^"))
                {
                    var parameters = new List<string>();

                    if (consume("("))
                    {
                        while (!isDone())
                        {
                            skipWhitespace();
                            if (consume(")")) break;
                            if (consume(",")) continue;
                            var parameter = consumeWord();
                            if (parameter == "") throw new IEParsingException(formatException("Expected token", index));
                            parameters.Add(parameter);
                        }
                    }

                    skipWhitespace();

                    if (!consume("{")) throw new IEParsingException(formatException("Expected function body", index));
                    var body = parseBlock("}");

                    return new Statement.FunctionDeclaration(new IEPosition(path, input, start), parameters, body);
                }

                var variable = consumeWord();
                if (variable == "") throw new IEParsingException(formatException("Expected token", index));
                return new Statement.VariableAccess(new IEPosition(path, input, start), variable);
            }

            Statement parseExpression()
            {
                var target = parseTarget();

                while (!isDone())
                {
                    skipWhitespace();

                    if (consume("."))
                    {
                        var start = index;
                        var member = consumeWord();
                        if (member == "") throw new IEParsingException(formatException("Expected member name", index));

                        target = new Statement.MemberAccess(new IEPosition(path, input, start), target, member);
                        continue;
                    }

                    if (skippedNewline) break;

                    if (consume("("))
                    {
                        var (receiver, method) = (target switch
                        {
                            Statement.VariableAccess variable => (null, variable.Name),
                            Statement.MemberAccess access => (access.Receiver, access.Member),
                            _ => (target, "")
                        });

                        var arguments = parseBlock(")");
                        var position = target.Position;

                        target = new Statement.Invocation(position, receiver, method, arguments);
                        continue;
                    }

                    if (consume("="))
                    {
                        skipWhitespace();
                        var start = index;
                        var value = parseExpression();

                        if (target is Statement.Invocation invocation)
                        {
                            invocation.Arguments.Add(value);
                            continue;
                        }

                        target = new Statement.Assignment(new IEPosition(path, input, start), target, value);
                        continue;
                    }

                    break;
                }

                return target;
            }

            return new IEDocument(path, new Statement.Group(new IEPosition(path, input, 0), parseBlock(null)));
        }
    }
}
