using InterEx.Integration;

namespace InterEx.InterfaceTypes
{
    public class IEIntegrationManager
    {
        public readonly ReflectionCache InstanceCache;
        public readonly ReflectionCache StaticCache;
        public readonly DelegateAdapterProvider Delegates;

        public IEIntegrationManager()
        {
            this.InstanceCache = new ReflectionCache(ReflectionCache.BindingType.Instance);
            this.StaticCache = new ReflectionCache(ReflectionCache.BindingType.Static);
            this.Delegates = new DelegateAdapterProvider(this.InstanceCache);
        }
    }
}
