using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoSupport.StaticCodeAnalyzer.Infrastructure.Data.Configurations;
public class ProjectFileConfiguration : IEntityTypeConfiguration<ProjectFile>
{
    public void Configure(EntityTypeBuilder<ProjectFile> builder)
    {
        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.HasMany(pf => pf.Issues)
            .WithOne()
            .HasForeignKey("ProjectFileId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
