using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

class Readline
{
    public delegate void LineHandler(string line);

    public event LineHandler OnLine;

    public const string ANSI_REPLACE_LINE = "\r\x1b[2K";
    public static string AnsiMoveCursor(int index) => $"\x1b[{index}G";

    public string Prompt = "> ";

    protected List<char> _lineBuffer = new(capacity: 256);
    protected int _cursor = 0;
    protected string _lineContent => new String(CollectionsMarshal.AsSpan(this._lineBuffer));
    protected List<string> _history = [];
    protected (string SavedContent, int SavedCursor, int Offset)? _historyExploration = null;

    public void Run()
    {
        this.PrintPrompt();
        while (true)
        {
            this.ProcessKey(Console.ReadKey(true));
            this.PrintPrompt();
        }
    }

    public void PrintPrompt()
    {
        Console.Write($"{ANSI_REPLACE_LINE}{this.Prompt}{this._lineContent}{AnsiMoveCursor(1 + this.Prompt.Length + this._cursor)}");
    }

    public void SubmitLine()
    {
        var line = this._lineContent;

        if (this._history.Count == 0 || this._history[^1] != line)
        {
            this._history.Add(line);
        }

        Console.Write("\n");
        this.OnLine?.Invoke(line);
        this._lineBuffer.Clear();
        this._cursor = 0;
    }

    protected void _ReplaceLineBuffer(string content, int cursor)
    {
        this._lineBuffer.Clear();
        this._lineBuffer.AddRange(content);
        this._cursor = cursor;
    }

    protected void _RecallHistoryEntry(int offset)
    {
        var content = this._history[^(offset + 1)];
        this._ReplaceLineBuffer(content, content.Length);
    }

    public void ProcessKey(ConsoleKeyInfo keyInfo)
    {
        var key = keyInfo.Key;

        if (key == ConsoleKey.LeftArrow)
        {
            if (this._cursor > 0) this._cursor--;
            return;
        }

        if (key == ConsoleKey.RightArrow)
        {
            if (this._cursor < this._lineBuffer.Count) this._cursor++;
            return;
        }

        if (key == ConsoleKey.Home)
        {
            this._cursor = 0;
            return;
        }

        if (key == ConsoleKey.End)
        {
            this._cursor = this._lineBuffer.Count;
            return;
        }

        if (key == ConsoleKey.Delete)
        {
            if (this._cursor == this._lineBuffer.Count) return;
            this._lineBuffer.RemoveAt(this._cursor);
            return;
        }

        if (key == ConsoleKey.UpArrow)
        {
            if (this._historyExploration is var (SavedContent, SavedCursor, Offset))
            {
                if (Offset == this._history.Count - 1)
                {
                    return;
                }
                this._historyExploration = (SavedContent, SavedCursor, Offset + 1);
            }
            else
            {
                this._historyExploration = (this._lineContent, this._cursor, 0);
            }
            this._RecallHistoryEntry(this._historyExploration.Value.Offset);
            return;
        }

        if (key == ConsoleKey.DownArrow)
        {
            if (this._historyExploration is var (SavedContent, SavedCursor, Offset))
            {
                if (Offset == 0)
                {
                    this._historyExploration = null;
                    this._ReplaceLineBuffer(SavedContent, SavedCursor);
                }
                else
                {
                    this._historyExploration = (SavedContent, SavedCursor, Offset - 1);
                    this._RecallHistoryEntry(this._historyExploration.Value.Offset);
                }
            }
            return;
        }

        if (key == ConsoleKey.Backspace)
        {
            if (this._cursor == 0) return;
            this._lineBuffer.RemoveAt(this._cursor - 1);
            this._cursor--;
            return;
        }

        if (key == ConsoleKey.Enter)
        {
            this.SubmitLine();
            return;
        }

        var c = keyInfo.KeyChar;
        if (c < 0x20)
        {
            // Invalid character
            return;
        }

        if (this._cursor == this._lineBuffer.Count)
        {
            this._lineBuffer.Add(c);
        }
        else
        {
            this._lineBuffer.Insert(this._cursor, c);
        }

        this._cursor++;
    }
}
