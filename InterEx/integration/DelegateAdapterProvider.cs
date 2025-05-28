using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using InterEx.InterfaceTypes;

namespace InterEx.Integration
{
    public class DelegateAdapterProvider(ReflectionCache typeSource)
    {
        public delegate Delegate Adapter(IEFunction function);

        public record class DelegateAdapter(Adapter Adapt) { }

        public readonly ReflectionCache ClassInfoProvider = typeSource;
        protected readonly Dictionary<Type, DelegateAdapter> _cache = [];

        public bool IsDelegate(Type type) => type.IsAssignableTo(typeof(Delegate));

        public DelegateAdapter GetAdapter(Type delegateType)
        {
            if (this._cache.TryGetValue(delegateType, out var existing)) return existing;

            var classInfo = this.ClassInfoProvider.GetClassInfo(delegateType);
            var invoke = classInfo.Functions["Invoke"][0];
            var parameters = invoke.Parameters;
            var returnType = ((MethodInfo)invoke.Target).ReturnType;

            var ieFunctionInfo = this.ClassInfoProvider.GetClassInfo(typeof(IEFunction));
            var ieFunctionInvoke = (MethodInfo)ieFunctionInfo.Functions["Invoke"][0].Target;
            var ieFunctionInvokeAndExport = (MethodInfo)ieFunctionInfo.Functions["InvokeAndExport"][0].Target;

            var ieFunctionParam = Expression.Parameter(typeof(IEFunction), "function");
            var delegateParams = parameters.Select(Expression.Parameter).ToArray();

            /*
                 ieFunctionParam  delegateParams             argumentsInit
                         v              v              /-----------^----------\
                    (function) => (...args) => function(new object[] {...args})
                    \___  ___/    \___ ___/    \_______________ ______________/
                        \/            v                        v
                      adapter       caller                   call
            */

            var argumentsInit = Expression.NewArrayInit(
                typeof(object),
                delegateParams.Select(v => (Expression)(v.Type.IsValueType ? Expression.TypeAs(v, typeof(object)) : v)).ToArray()
            );

            var call = returnType switch
            {
                _ when returnType == typeof(void) => Expression.Call(
                    ieFunctionParam,
                    ieFunctionInvoke,
                    argumentsInit
                ),
                _ => (Expression)Expression.Convert(
                    Expression.Call(
                        ieFunctionParam,
                        ieFunctionInvokeAndExport,
                        Expression.Constant(returnType),
                        argumentsInit
                    ),
                    returnType
                )
            };

            var caller = Expression.Lambda(delegateType, call, delegateParams);

            var adapter = Expression.Lambda<Adapter>(caller, [ieFunctionParam]).Compile();

            var info = new DelegateAdapter(adapter);
            this._cache.Add(delegateType, info);
            return info;
        }
    }
}
