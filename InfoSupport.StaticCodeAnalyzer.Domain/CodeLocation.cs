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

    public Position Start { get; } = start;
    public Position End { get; } = end;
}
