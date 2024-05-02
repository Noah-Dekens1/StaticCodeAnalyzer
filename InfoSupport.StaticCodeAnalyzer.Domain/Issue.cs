using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.Domain;

public enum AnalyzerSeverity
{
    Invalid,
    Suggestion,
    Warning,
    Important,
}

public class Issue(string code, CodeLocation location, AnalyzerSeverity severity)
{
    public Issue() : this(null!, null!, AnalyzerSeverity.Suggestion) { } // only for EF Core

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = code;
    public CodeLocation Location { get; set; } = location;
    public AnalyzerSeverity AnalyzerSeverity { get; set; } = severity;
}
