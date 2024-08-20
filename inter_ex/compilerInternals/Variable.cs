using InterEx.InterfaceTypes;

namespace InterEx.CompilerInternals
{
    public sealed class Variable
    {
        public Value Content;

        public Variable(Value value)
        {
            this.Content = value;
        }
    }
}
