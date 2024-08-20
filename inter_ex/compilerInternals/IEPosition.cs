using System;
using System.Linq;

namespace InterEx.CompilerInternals
{
    public record struct IEPosition(string Path, string Content, int Index)
    {
        public readonly string Format(string message)
        {
            var lineStart = this.Content.LastIndexOf('\n', Math.Min(this.Index, this.Content.Length - 1));
            if (lineStart == -1) lineStart = 0;
            else lineStart++;
            var lineNum = this.Content.Take(lineStart).Count(v => v == '\n') + 1;
            var charNum = Math.Max(1, this.Index - lineStart);

            return $"{message} at {this.Path}:{lineNum}:{charNum}";
        }
    }
}
