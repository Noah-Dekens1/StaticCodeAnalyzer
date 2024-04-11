using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.Domain;

public class Issue(string code, string description, CodeLocation location)
{
    private Issue() : this(null!, null!, null!) { } // only for EF Core

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = code;
    public string Description { get; set; } = description;
    public CodeLocation Location { get; set; } = location;
}
