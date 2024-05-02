using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.Domain;

public class Project(string name, string path)
{
    public Project() : this(null!, null!) { } // only for EF Core

    public Guid Id { get; set; } = Guid.NewGuid();
    public List<Report> Reports { get; set; } = [];
    public string Name { get; set; } = name;
    public string Path { get; set; } = path;
}
