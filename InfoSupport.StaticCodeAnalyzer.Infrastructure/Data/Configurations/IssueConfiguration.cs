using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoSupport.StaticCodeAnalyzer.Infrastructure.Data.Configurations;

// Review: Good choice of you to use your Domain models as models for your database
// It's nice you are using the IEntityTypeConfiguration interface to configure your entities instead of using data annotations on your models
public class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.OwnsOne(i => i.Location, loc =>
        {
            loc.WithOwner();

            loc.OwnsOne(l => l.Start, start =>
            {
                start.Property(s => s.Line).HasColumnName("StartLine");
                start.Property(s => s.Column).HasColumnName("StartColumn");
            });

            loc.OwnsOne(l => l.End, end =>
            {
                end.Property(e => e.Line).HasColumnName("EndLine");
                end.Property(e => e.Column).HasColumnName("EndColumn");
            });
        });
    }
}
