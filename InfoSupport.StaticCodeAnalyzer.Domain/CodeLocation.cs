using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.Domain;

[DebuggerDisplay("Ln: {Line} Col: {Column}")]
public class Position(ulong line, ulong column)
{
    public ulong Line { get; set; } = line;
    public ulong Column { get; set; } = column;

    public Position() : this(1, 1)
    {
    }

    public void Add(Position other)
    {
        Line += other.Line;
        Column += other.Column;
    }
}

[DebuggerDisplay("({Start};{End})")]
public class CodeLocation(Position start, Position end)
{
    public CodeLocation() : this(new Position(), new Position()) { }

    public Position Start { get; set; } = start;
    public Position End { get; set; } = end;

    public static CodeLocation From(CodeLocation original)
    {
        return new CodeLocation
        {
            Start = new Position(original.Start.Line, original.Start.Column),
            End = new Position(original.End.Line, original.End.Column)
        };
    }
}

public class CodeLocationComparator : IComparer<CodeLocation>
{
    private static int ComparePosition(Position x, Position y)
    {
        if (x.Line < y.Line) return -1;
        if (x.Line > y.Line) return 1;
        if (x.Column < y.Column) return -1;
        if (x.Column > y.Column) return 1;
        return 0;
    }

    public int Compare(CodeLocation? x, CodeLocation? y)
    {
        if (x is null || y is null)
        {
            if (x is null && y is null) return 0;
            return x is null ? -1 : 1;
        }

        int startComparison = ComparePosition(x.Start, y.Start);

        return startComparison != 0 
            ? startComparison 
            : ComparePosition(x.End, y.End);
    }
}
