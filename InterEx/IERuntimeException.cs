using System;
using System.Text;

namespace InterEx
{
    public class IERuntimeException : Exception
    {
        public IERuntimeException() { }
        public IERuntimeException(string message) : base(message) { }
        public IERuntimeException(string message, IERuntimeException inner) : base(message + "\n  " + inner.Message.Replace("\n", "\n  "), inner.InnerException) { }
        public IERuntimeException(string message, Exception inner) : base(message, inner) { }

        public bool ContainsNativeError()
        {
            var inner = this.InnerException;

            while (inner != null)
            {
                if (inner is not IERuntimeException or System.Reflection.TargetInvocationException) return true;
            }

            return false;
        }

        public string FlattenMessage()
        {
            var result = new StringBuilder();
            result.AppendLine(this.Message);

            var inner = this.InnerException;
            while (inner != null)
            {
                result.AppendLine(inner.Message);
                inner = inner.InnerException;
            }

            return result.ToString();
        }
    }
}
