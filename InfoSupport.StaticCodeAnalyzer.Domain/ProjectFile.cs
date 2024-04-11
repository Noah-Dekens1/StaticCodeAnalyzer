using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.Domain;
public class ProjectFile(string name, string path, string? content)
{
    private ProjectFile() : this(null!, null!, null!) { }  // only for EF Core

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = name;
    public string Path { get; set; } = path;
    public string? Content { get; set; } = content;
    public List<Issue> Issues { get; set; } = [];
}
