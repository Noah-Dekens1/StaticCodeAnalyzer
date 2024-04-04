using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.Domain;
public class Report(Project project, List<ProjectFile> projectFiles)
{
    public Project Project { get; } = project;
    public DateTime RunAt { get; } = DateTime.Now;
    public List<ProjectFile> ProjectFiles { get; } = projectFiles;
}
