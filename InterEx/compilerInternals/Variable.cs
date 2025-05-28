using InterEx.InterfaceTypes;

namespace InterEx.CompilerInternals
{
    public sealed class Variable(Value value)
    {
        public Value Content = value;
    }
}
