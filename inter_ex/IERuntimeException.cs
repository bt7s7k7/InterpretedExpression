using System;

namespace InterEx
{
    public class IERuntimeException : Exception
    {
        public IERuntimeException() { }
        public IERuntimeException(string message) : base(message) { }
        public IERuntimeException(string message, string inner) : base(message + "\n  " + inner.Replace("\n", "\n  ")) { }
    }
}
