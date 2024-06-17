using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace InterEx
{
    public partial class IEEngine
    {
        protected class GlobalScopeType : Scope { }
        public readonly Scope GlobalScope = new GlobalScopeType();

        public void AddGlobal(string name, object value)
        {
            var content = this.ImportValue(value);
            var variable = this.GlobalScope.Declare(name);
            variable.Content = content;
        }

        public readonly ReflectionCache InstanceCache = new(ReflectionCache.BindingType.Instance);
        public readonly ReflectionCache StaticCache = new(ReflectionCache.BindingType.Static);
        protected readonly ReflectionCache _operatorCache = new(ReflectionCache.BindingType.Static);

        public Value GetProperty(Value receiver, string name)
        {
            if (receiver.Content == null) throw new IERuntimeException($"Cannot get property '{name}' of null");
            if (receiver.Content is ICustomValue customValue)
            {
                if (customValue.Get(this, name, out var result)) return result;
                throw new IERuntimeException($"Object does not contain property '{name}'");
            }

            var type = receiver.Content.GetType();
            var info = this.InstanceCache.GetClassInfo(type);

            if (!info.Properties.TryGetValue(name, out var member)) throw new IERuntimeException($"Object '{type}' does not contain property '{name}'");

            if (member is PropertyInfo property)
            {
                return this.ImportValue(property.GetValue(receiver.Content));
            }
            else if (member is FieldInfo field)
            {
                return this.ImportValue(field.GetValue(receiver.Content));
            }
            else throw new();
        }

        public void SetProperty(Value receiver, string name, Value value)
        {
            if (receiver.Content == null) throw new IERuntimeException($"Cannot get property '{name}' of null");
            if (receiver.Content is ICustomValue customValue)
            {
                if (customValue.Set(this, name, value)) return;
            }

            var type = receiver.Content.GetType();
            var info = this.InstanceCache.GetClassInfo(type);

            if (!info.Properties.TryGetValue(name, out var member)) throw new IERuntimeException($"Object '{type}' does not contain property '{name}'");

            if (member is PropertyInfo property)
            {
                property.SetValue(receiver.Content, this.ExportValue(value, property.PropertyType));
            }
            else if (member is FieldInfo field)
            {
                field.SetValue(receiver.Content, this.ExportValue(value, field.FieldType));
            }
            else throw new();
        }

        public Value Invoke(Value receiver, Statement.Invocation invocation, string method, Value[] arguments)
        {
            if (receiver.Content is IEFunction function)
            {
                return function.InvokeRaw(arguments);
            }

            if (receiver.Content is ICustomValue customValue)
            {
                if (customValue.Invoke(this, invocation, method, out var result, arguments)) return result;
            }

            var type = receiver.Content?.GetType();
            var info = type == null ? null : this.InstanceCache.GetClassInfo(type);

            if (method == "") method = "Invoke";

            if (info != null && info.Functions.TryGetValue(method, out var overloads))
            {
                return this.BridgeMethodCall(overloads, invocation, receiver, arguments);
            }

            var operators = this._operatorCache.GetClassInfo(typeof(IEOperators));
            if (operators.Functions.TryGetValue(method, out var operatorMethod))
            {
                var operatorArguments = new[] { receiver }.Concat(arguments).ToArray();
                return this.BridgeMethodCall(operatorMethod, invocation, new(null), operatorArguments);
            }

            throw new IERuntimeException($"Object '{type?.ToString() ?? "null"}' does not contain method '{method}'");
        }

        public Variable GetVariable(string name, IEPosition position, Scope scope)
        {
            if (scope.Get(name, out var variable)) return variable;

            foreach (var provider in this._providers)
            {
                if (provider.Find(this, name, out var value))
                {
                    var newVariable = this.GlobalScope.Declare(name);
                    newVariable.Content = value;
                    return newVariable;
                }
            }

            foreach (var provider in this._providersFallback)
            {
                if (provider.Find(this, name, out var value))
                {
                    var newVariable = this.GlobalScope.Declare(name);
                    newVariable.Content = value;
                    return newVariable;
                }
            }

            throw new IERuntimeException(position.Format("Cannot find variable " + name));
        }

        public Value Evaluate(Statement statement, Scope scope)
        {
            try
            {
                if (statement is Statement.VariableAccess variableAccess)
                {
                    return this.GetVariable(variableAccess.Name, variableAccess.Position, scope).Content;
                }

                if (statement is Statement.Group group)
                {
                    if (group.Statements.Count == 0) return new Value(null);

                    foreach (var member in group.Statements.Take(group.Statements.Count - 1))
                    {
                        this.Evaluate(member, scope);
                    }

                    return this.Evaluate(group.Statements[^1], scope);
                }

                if (statement is Statement.VariableDeclaration declaration)
                {
                    scope.Declare(declaration.Name);
                    return new Value(null);
                }

                if (statement is Statement.MemberAccess access)
                {
                    var receiver = this.Evaluate(access.Receiver, scope);

                    try
                    {
                        return this.GetProperty(receiver, access.Member);
                    }
                    catch (IERuntimeException error)
                    {
                        throw new IERuntimeException(access.Position.Format("Cannot get property"), error);
                    }
                }

                if (statement is Statement.Assignment assignment)
                {
                    var target = assignment.Receiver;
                    if (target is Statement.VariableAccess inner)
                    {
                        var variable = this.GetVariable(inner.Name, inner.Position, scope);
                        var value = this.Evaluate(assignment.Value, scope);
                        variable.Content = value;
                        return value;
                    }

                    if (target is Statement.VariableDeclaration decl)
                    {
                        var variable = scope.Declare(decl.Name);
                        var value = this.Evaluate(assignment.Value, scope);
                        variable.Content = value;
                        return value;
                    }

                    if (target is Statement.MemberAccess member)
                    {
                        var receiver = this.Evaluate(member.Receiver, scope);
                        var value = this.Evaluate(assignment.Value, scope);

                        try
                        {
                            this.SetProperty(receiver, member.Member, value);
                            return value;
                        }
                        catch (IERuntimeException error)
                        {
                            throw new IERuntimeException(member.Position.Format("Cannot set property"), error);
                        }
                    }

                    throw new IERuntimeException(assignment.Position.Format("Target is not assignable"));
                }

                if (statement is Statement.Invocation invocation)
                {
                    var function = invocation.Function;
                    var isKeyword = function.StartsWith("k_");

                    var receiver = (Value)default;

                    if (invocation.Receiver == null)
                    {
                        receiver = this.GetVariable(function, invocation.Position, scope).Content;
                        function = "";
                    }
                    else
                    {
                        receiver = isKeyword ? new Value(invocation.Receiver) : this.Evaluate(invocation.Receiver, scope);
                    }

                    var arguments = (Value[])null;
                    if (isKeyword)
                    {
                        arguments = new[] { new Value(this) }
                            .Concat(invocation.Arguments.Select(v => new Value(v)))
                            .Concat(new[] { new Value(scope) })
                            .ToArray();
                    }
                    else
                    {
                        arguments = invocation.Arguments.Select(v => this.Evaluate(v, scope)).ToArray();
                    }

                    try
                    {
                        return this.Invoke(receiver, invocation, function, arguments);
                    }
                    catch (IERuntimeException error)
                    {
                        throw new IERuntimeException(invocation.Position.Format("Cannot invoke"), error);
                    }
                }

                if (statement is Statement.FunctionDeclaration functionDeclaration)
                {
                    return new Value(new IEFunction(
                        Engine: this,
                        Scope: scope,
                        Root: functionDeclaration.Body.Count == 1 ? functionDeclaration.Body[0] : new Statement.Group(functionDeclaration.Position, functionDeclaration.Body),
                        Parameters: functionDeclaration.Parameters
                    ));
                }

                if (statement is Statement.ObjectLiteral objectLiteral)
                {
                    return new Value(
                        new Dictionary<string, Value>(
                            objectLiteral.Properties.Select(
                                v => new KeyValuePair<string, Value>(v.Key, this.Evaluate(v.Value, scope))
                            )
                        )
                    );
                }

                if (statement is Statement.StringLiteral stringLiteral) return new Value(stringLiteral.Value);
                if (statement is Statement.NumberLiteral numberLiteral) return new Value(numberLiteral.Value);

                throw new IERuntimeException(statement.Position.Format("Invalid statement"));
            }
            catch (Exception error)
            {
                if (error is IERuntimeException) throw;
                if (error.InnerException is IERuntimeException inner) throw new IERuntimeException(statement.Position.Format("Invalid operation"), inner);
                throw new IERuntimeException(statement.Position.Format("Caught system exception"), error);
            }
        }
    }
}
