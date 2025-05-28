namespace InterEx.InterfaceTypes
{
    public readonly struct Value(object content)
    {
        public readonly object Content = content;

        public override string ToString()
        {
            return this.Content == null ? "null" : this.Content.ToString();
        }
    }
}
