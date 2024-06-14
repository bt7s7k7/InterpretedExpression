using System;

namespace InterEx
{
    public class IERuntimeException : Exception
    {
        public IERuntimeException() { }
        public IERuntimeException(string message) : base(message) { }
        public IERuntimeException(string message, IERuntimeException inner) : base(message + "\n  " + inner.Message.Replace("\n", "\n  "), inner.InnerException) { }
        public IERuntimeException(string message, Exception inner) : base(message, inner) { }
    }
}
