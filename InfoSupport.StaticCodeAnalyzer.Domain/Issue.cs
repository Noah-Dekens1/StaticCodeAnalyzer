using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.Domain;

public readonly struct CodeLocation(int line, int column)
{
    public int Line { get; } = line;
    public int Column { get; } = column;
}

public readonly struct IssueLocation(CodeLocation start, CodeLocation end)
{
    public CodeLocation Start { get; } = start;
    public CodeLocation End { get; } = end;
}

public class Issue(string code, string description, IssueLocation location)
{
    public string Code { get; set; } = code;
    public string Description { get; set; } = description;
    public IssueLocation Location { get; set; } = location;
}
