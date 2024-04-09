using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoSupport.StaticCodeAnalyzer.Infrastructure.Data.Configurations;
public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.HasMany(r => r.ProjectFiles)
            .WithOne()
            .HasForeignKey("ReportId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.RunAt)
            .HasColumnType("datetime");
    }
}
