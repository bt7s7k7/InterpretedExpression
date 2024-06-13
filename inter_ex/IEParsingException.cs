using System;

namespace InterEx
{
    [Serializable]
    public class IEParsingException : Exception
    {
        public IEParsingException() { }
        public IEParsingException(string message) : base(message) { }
        public IEParsingException(string message, Exception inner) : base(message, inner) { }
    }
}
