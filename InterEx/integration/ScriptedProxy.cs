using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using InterEx.InterfaceTypes;

namespace InterEx.Integration;

public class ScriptedProxy : DispatchProxy
{
    public Type InterfaceType;
    public IEIntegrationManager Integration;
    public Table Implementer;

    protected Dictionary<MethodInfo, (object DelegateInstance, MethodInfo Invoke)> _defaultImplementationCache = null;

    private static Dictionary<(bool ReturnType, int ParameterCount), Type> _delegateTypes = null;
    private static Dictionary<MethodInfo, Type> _defaultImplementations = null;

    protected override object Invoke(MethodInfo targetMethod, object[] args)
    {
        var name = targetMethod.Name;
        if (this.Implementer.TryGetProperty(name, out var implementation))
        {
            return this.Integration.ExportValue<IEFunction>(implementation).InvokeAndExport(targetMethod.ReturnType, args);
        }
        else if (this.Implementer.TryGetProperty("_invoke", out var fallback))
        {
            return this.Integration.ExportValue<IEFunction>(fallback).InvokeAndExport(targetMethod.ReturnType, [name, args]);
        }
        else
        {
            if (targetMethod.ReturnType == typeof(void))
            {
                return null;
            }

            if (targetMethod.IsAbstract) throw new NotImplementedException($"Cannot find implementation for method '{name}'");

            if (this._defaultImplementationCache == null || !this._defaultImplementationCache.TryGetValue(targetMethod, out var cached))
            {
                if (_defaultImplementations == null || !_defaultImplementations.TryGetValue(targetMethod, out var specializedDelegateType))
                {
                    _delegateTypes ??= new()
                    {
                        [(false, 0)] = typeof(Action),
                        [(false, 1)] = typeof(Action<>),
                        [(false, 2)] = typeof(Action<,>),
                        [(false, 3)] = typeof(Action<,,>),
                        [(false, 4)] = typeof(Action<,,,>),
                        [(true, 0)] = typeof(Func<>),
                        [(true, 1)] = typeof(Func<,>),
                        [(true, 2)] = typeof(Func<,,>),
                        [(true, 3)] = typeof(Func<,,,>),
                        [(true, 4)] = typeof(Func<,,,,>),
                    };

                    var returnsValues = targetMethod.ReturnType != typeof(void);
                    var parameters = targetMethod.GetParameters().Select(v => v.ParameterType).ToArray();

                    if (!_delegateTypes.TryGetValue((returnsValues, parameters.Length), out var delegateType))
                    {
                        throw new NotImplementedException($"Tried to call default implementation of '{targetMethod}', but it has an incompatible signature");
                    }

                    specializedDelegateType = delegateType.MakeGenericType(returnsValues ? [.. parameters, targetMethod.ReturnType] : parameters);
                    _defaultImplementations ??= [];
                    _defaultImplementations.Add(targetMethod, specializedDelegateType);
                }

                var delegateInstance = Activator.CreateInstance(specializedDelegateType, this, targetMethod.MethodHandle.GetFunctionPointer());
                cached = (delegateInstance, delegateInstance.GetType().GetMethod("Invoke"));
                this._defaultImplementationCache ??= [];
                this._defaultImplementationCache.Add(targetMethod, cached);
            }

            return cached.Invoke.Invoke(cached.DelegateInstance, args);
        }
    }

    public static object Create(IEIntegrationManager integration, Table implementer, Type interfaceType)
    {
        var proxy = (ScriptedProxy)Create(interfaceType, typeof(ScriptedProxy));

        proxy.InterfaceType = interfaceType;
        proxy.Integration = integration;
        proxy.Implementer = implementer;

        return proxy;
    }
}
