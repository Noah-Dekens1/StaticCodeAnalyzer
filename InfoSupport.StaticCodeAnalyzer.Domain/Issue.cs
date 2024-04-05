using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.Domain;

public class Issue(string code, string description, CodeLocation location)
{
    public string Code { get; set; } = code;
    public string Description { get; set; } = description;
    public CodeLocation Location { get; set; } = location;
}
