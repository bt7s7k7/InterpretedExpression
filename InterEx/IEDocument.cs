using System.Collections.Generic;
using System.Linq;
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
                    Statement.StringLiteral literal => makeObject(new()
                    {
                        ["kind"] = Statement.StringLiteral.Kind,
                        ["value"] = literal.Value
                    }),
                    Statement.TemplateLiteral literal => makeObject(new()
                    {
                        ["kind"] = Statement.TemplateLiteral.Kind,
                        ["fragments"] = makeArray(literal.Fragments.Select(((v) => new JsonObject()
                        {
                            ["statement"] = visit(v.Statement),
                            ["format"] = v.Format,
                        })))
                    }),
                    Statement.NumberLiteral literal => makeObject(new()
                    {
                        ["kind"] = Statement.NumberLiteral.Kind,
                        ["value"] = literal.Value
                    }),
                    Statement.ObjectLiteral literal => makeObject(new()
                    {
                        ["kind"] = Statement.ObjectLiteral.Kind,
                        ["properties"] = new JsonObject(literal.Properties.Select(v => new KeyValuePair<string, JsonNode>(v.Key, visit(v.Value))))
                    }),
                    Statement.VariableAccess statement => statement.Name,
                    Statement.VariableDeclaration statement => makeObject(new()
                    {
                        ["kind"] = Statement.VariableDeclaration.Kind,
                        ["name"] = statement.Name
                    }),
                    Statement.Assignment statement => makeObject(new()
                    {
                        ["kind"] = Statement.Assignment.Kind,
                        ["receiver"] = visit(statement.Receiver),
                        ["value"] = visit(statement.Value),
                    }),
                    Statement.Invocation statement => makeArray([
                        visit(statement.Receiver),
                        statement.Function,
                        .. statement.Arguments.Select(visit)
                    ]),
                    Statement.MemberAccess statement => makeObject(new()
                    {
                        ["kind"] = Statement.MemberAccess.Kind,
                        ["receiver"] = visit(statement.Receiver),
                        ["member"] = statement.Member,
                    }),
                    Statement.Group statement => makeObject(new()
                    {
                        ["kind"] = Statement.Group.Kind,
                        ["statements"] = makeArray(statement.Statements.Select(visit)),
                    }),
                    Statement.Indexer statement => makeObject(new()
                    {
                        ["kind"] = Statement.Indexer.Kind,
                        ["statements"] = makeArray(statement.Statements.Select(visit)),
                    }),
                    Statement.FunctionDeclaration statement => makeObject(new()
                    {
                        ["kind"] = Statement.FunctionDeclaration.Kind,
                        ["parameters"] = makeArray(statement.Parameters.Select(v => JsonValue.Create(v))),
                        ["body"] = makeArray(statement.Body.Select(visit)),
                    }),
                    Statement.Operator statement => makeObject(new()
                    {
                        ["kind"] = Statement.Operator.Kind,
                        ["token"] = statement.Token,
                    }),
                    _ => null
                };
            }

            return visit(this.Root).ToJsonString(new()
            {
                WriteIndented = true
            });
        }
    }
}
