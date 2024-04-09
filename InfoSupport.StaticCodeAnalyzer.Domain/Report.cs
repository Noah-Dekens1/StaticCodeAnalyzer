using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.Domain;

public class Report(Project project, List<ProjectFile> projectFiles)
{
    private Report() : this(null!, null!) { }  // only for EF Core

    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonIgnore]
    public Project Project { get; } = project;
    public DateTime RunAt { get; set; } = DateTime.Now;
    public List<ProjectFile> ProjectFiles { get; set; } = projectFiles;
}
