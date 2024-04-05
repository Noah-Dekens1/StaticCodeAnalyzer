using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.Domain;

[DebuggerDisplay("Ln: {Line} Col: {Column}")]
public struct Position(ulong line, ulong column)
{
    public ulong Line { get; set; } = line;
    public ulong Column { get; set; } = column;
}

public readonly struct CodeLocation(Position start, Position end)
{
    public Position Start { get; } = start;
    public Position End { get; } = end;
}
