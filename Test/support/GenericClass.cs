class GenericClass { }

class GenericClass<T> : GenericClass where T : new()
{
    public readonly T Value = new T();
}
