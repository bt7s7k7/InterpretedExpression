using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using InterEx.CompilerInternals;

namespace InterEx
{
    public partial class IEDocument(string path, Statement.Group root)
    {
        public readonly string Path = path;
        public readonly Statement.Group Root = root;

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
                    Statement.TemplateLiteral literal => makeObject(new() {
                        {"kind", Statement.TemplateLiteral.Kind},
                        {"fragments", makeArray(literal.Fragments.Select(((v) => new JsonObject() {
                            {"statement", visit(v.Statement)},
                            {"format", v.Format},
                        })))}
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

        public static IEDocument ParseCode(string path, string input)
        {
            int index = 0;

            bool matches(string value)
            {
                if (index + value.Length > input.Length) return false;
                return input.AsSpan(index, value.Length).Equals(value, StringComparison.Ordinal);
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

            ReadOnlySpan<char> readWhile(Func<bool> predicate)
            {
                var start = index;
                skipWhile(predicate);
                var end = index;
                return input.AsSpan(start, end - start);
            }

            ReadOnlySpan<char> consumeWord()
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

            List<Statement> parseBlock(params ReadOnlySpan<string> terms)
            {
                var result = new List<Statement>();

                while (!isDone())
                {
                    skipWhitespace();
                    if (isDone()) break;

                    if (!terms.IsEmpty)
                    {
                        foreach (var term in terms)
                        {
                            if (consume(term)) goto terminate_block;
                        }
                    }
                    if (consume(",")) continue;
                    result.Add(parseExpression());
                }
            terminate_block:

                return result;
            }

            string formatException(string message, int index) => new IEPosition(path, input, index).Format(message);

            char parseEscapeSequence()
            {
                if (isDone()) throw new IEParsingException(formatException("Unexpected EOF", index));

                var e = input[index];
                index++;
                if (e == 'x')
                {
                    var charStart = index;
                    index++;
                    if (isDone()) throw new IEParsingException(formatException("Unexpected EOF", index));
                    index++;
                    if (!Byte.TryParse(input.AsSpan(charStart, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var charValue))
                    {
                        throw new IEParsingException(formatException("Invalid number", index));
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
                    _ => throw new IEParsingException(formatException("Invalid escape character", index))
                };
            }

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
                        value.Append(parseEscapeSequence());
                        continue;
                    }

                    value.Append(c);
                }

                return new Statement.StringLiteral(new IEPosition(path, input, start), value.ToString());
            }

            Statement parseTemplate(char term)
            {
                var template = new Statement.TemplateLiteral(new IEPosition(path, input, index - 1), []);
                var fragmentStart = index;
                StringBuilder fragment = null;

                while (!isDone())
                {
                    var c = input[index];
                    index++;

                    if (c == term) break;
                    if (c == '\\')
                    {
                        parseEscapeSequence();
                        continue;
                    }

                    if (c == '$' && !isDone() && input[index] == '{')
                    {
                        index++;
                        var statements = parseBlock("}", ":");
                        var statementStart = index;

                        string formatString = null;
                        if (input[index - 1] == ':')
                        {
                            formatString = new String(readWhile(() => input[index] != '}'));
                            index++;
                        }

                        if (statements.Count == 0) continue;

                        if (fragment != null)
                        {
                            template.Fragments.Add((new Statement.StringLiteral(new IEPosition(path, input, fragmentStart), fragment.ToString()), null));
                            fragment = null;
                        }

                        if (statements.Count == 1)
                        {
                            template.Fragments.Add((statements[0], formatString));
                        }
                        else
                        {
                            template.Fragments.Add((new Statement.Group(new IEPosition(path, input, statementStart), statements), formatString));
                        }

                        continue;
                    }

                    if (fragment == null)
                    {
                        fragment = new();
                        fragmentStart = index - 1;
                    }

                    fragment.Append(c);
                }

                if (fragment != null)
                {
                    template.Fragments.Add((new Statement.StringLiteral(new IEPosition(path, input, fragmentStart), fragment.ToString()), null));
                    fragment = null;
                }

                return template;
            }

            Statement parseTarget()
            {
                int start = index;

                if (input[index] is >= '0' and <= '9' or '-')
                {
                    var numberText = new StringBuilder();
                    if (consume("-")) numberText.Append('-');

                    numberText.Append(readWhile(() => input[index] is >= '0' and <= '9'));

                    if (consume("."))
                    {
                        numberText.Append('.');
                        numberText.Append(readWhile(() => input[index] is >= '0' and <= '9'));
                    }

                    if (!Double.TryParse(numberText.ToString(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
                    {
                        throw new IEParsingException(formatException("Invalid number", index));
                    }

                    return new Statement.NumberLiteral(new IEPosition(path, input, start), number);
                }

                if (consume("$\"")) return parseTemplate('"');
                if (consume("$'")) return parseTemplate('\'');
                if (consume("$`")) return parseTemplate('`');

                if (consume("\"")) return parseString('"');
                if (consume("'")) return parseString('\'');
                if (consume("'")) return parseString('`');

                if (consume("$"))
                {
                    var name = consumeWord();
                    if (name == "") throw new IEParsingException(formatException("Missing variable name", index));

                    return new Statement.VariableDeclaration(new IEPosition(path, input, start), name.ToString());
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

                        var name = consumeWord().ToString();
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
                            if (parameter.IsEmpty) throw new IEParsingException(formatException("Expected token", index));
                            parameters.Add(parameter.ToString());
                        }
                    }

                    skipWhitespace();

                    List<Statement> body;
                    if (consume("{"))
                    {
                        body = parseBlock("}");
                    }
                    else
                    {
                        body = [parseExpression()];
                    }

                    return new Statement.FunctionDeclaration(new IEPosition(path, input, start), parameters, body);
                }

                var variable = consumeWord();
                if (variable.IsEmpty) throw new IEParsingException(formatException("Expected token", index));
                return new Statement.VariableAccess(new IEPosition(path, input, start), variable.ToString());
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
                        string memberName;
                        if (consume("$"))
                        {
                            var member = consumeWord();
                            memberName = "$" + member.ToString();
                        }
                        else
                        {
                            var member = consumeWord();
                            if (member.IsEmpty) throw new IEParsingException(formatException("Expected member name", index));
                            memberName = member.ToString();
                        }

                        target = new Statement.MemberAccess(new IEPosition(path, input, start), target, memberName);
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
