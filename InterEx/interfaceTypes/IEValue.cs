namespace InterEx.InterfaceTypes
{
    public readonly struct Value
    {
        public readonly object Content;

        public override string ToString()
        {
            return this.Content == null ? "null" : this.Content.ToString();
        }

        public Value(object content)
        {
            this.Content = content;
        }
    }
}
