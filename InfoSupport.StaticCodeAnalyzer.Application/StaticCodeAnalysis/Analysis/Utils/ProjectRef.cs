using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Utils;
public class ProjectRef
{
    public Dictionary<string, AST> ProjectFiles { get; set; } = [];
    public TypeLookup TypeLookup { get; set; } = new();
}
