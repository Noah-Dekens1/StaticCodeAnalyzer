﻿// <auto-generated />
using System;
using InfoSupport.StaticCodeAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace InfoSupport.StaticCodeAnalyzer.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20240502125642_ReportSeverity")]
    partial class ReportSeverity
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.3");

            modelBuilder.Entity("InfoSupport.StaticCodeAnalyzer.Domain.Issue", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("TEXT");

                    b.Property<int>("AnalyzerSeverity")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Code")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<Guid?>("ProjectFileId")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("ProjectFileId");

                    b.ToTable("Issues");
                });

            modelBuilder.Entity("InfoSupport.StaticCodeAnalyzer.Domain.Project", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Path")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Projects");
                });

            modelBuilder.Entity("InfoSupport.StaticCodeAnalyzer.Domain.ProjectFile", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("Content")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Path")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<Guid?>("ReportId")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("ReportId");

                    b.ToTable("ProjectFiles");
                });

            modelBuilder.Entity("InfoSupport.StaticCodeAnalyzer.Domain.Report", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsSuccess")
                        .HasColumnType("INTEGER");

                    b.Property<Guid?>("ProjectId")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("RunAt")
                        .HasColumnType("datetime");

                    b.Property<long>("SeverityScore")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("ProjectId");

                    b.ToTable("Reports");
                });

            modelBuilder.Entity("InfoSupport.StaticCodeAnalyzer.Domain.Issue", b =>
                {
                    b.HasOne("InfoSupport.StaticCodeAnalyzer.Domain.ProjectFile", null)
                        .WithMany("Issues")
                        .HasForeignKey("ProjectFileId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.OwnsOne("InfoSupport.StaticCodeAnalyzer.Domain.CodeLocation", "Location", b1 =>
                        {
                            b1.Property<Guid>("IssueId")
                                .HasColumnType("TEXT");

                            b1.HasKey("IssueId");

                            b1.ToTable("Issues");

                            b1.WithOwner()
                                .HasForeignKey("IssueId");

                            b1.OwnsOne("InfoSupport.StaticCodeAnalyzer.Domain.Position", "End", b2 =>
                                {
                                    b2.Property<Guid>("CodeLocationIssueId")
                                        .HasColumnType("TEXT");

                                    b2.Property<ulong>("Column")
                                        .HasColumnType("INTEGER")
                                        .HasColumnName("EndColumn");

                                    b2.Property<ulong>("Line")
                                        .HasColumnType("INTEGER")
                                        .HasColumnName("EndLine");

                                    b2.HasKey("CodeLocationIssueId");

                                    b2.ToTable("Issues");

                                    b2.WithOwner()
                                        .HasForeignKey("CodeLocationIssueId");
                                });

                            b1.OwnsOne("InfoSupport.StaticCodeAnalyzer.Domain.Position", "Start", b2 =>
                                {
                                    b2.Property<Guid>("CodeLocationIssueId")
                                        .HasColumnType("TEXT");

                                    b2.Property<ulong>("Column")
                                        .HasColumnType("INTEGER")
                                        .HasColumnName("StartColumn");

                                    b2.Property<ulong>("Line")
                                        .HasColumnType("INTEGER")
                                        .HasColumnName("StartLine");

                                    b2.HasKey("CodeLocationIssueId");

                                    b2.ToTable("Issues");

                                    b2.WithOwner()
                                        .HasForeignKey("CodeLocationIssueId");
                                });

                            b1.Navigation("End")
                                .IsRequired();

                            b1.Navigation("Start")
                                .IsRequired();
                        });

                    b.Navigation("Location")
                        .IsRequired();
                });

            modelBuilder.Entity("InfoSupport.StaticCodeAnalyzer.Domain.ProjectFile", b =>
                {
                    b.HasOne("InfoSupport.StaticCodeAnalyzer.Domain.Report", null)
                        .WithMany("ProjectFiles")
                        .HasForeignKey("ReportId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("InfoSupport.StaticCodeAnalyzer.Domain.Report", b =>
                {
                    b.HasOne("InfoSupport.StaticCodeAnalyzer.Domain.Project", null)
                        .WithMany("Reports")
                        .HasForeignKey("ProjectId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("InfoSupport.StaticCodeAnalyzer.Domain.Project", b =>
                {
                    b.Navigation("Reports");
                });

            modelBuilder.Entity("InfoSupport.StaticCodeAnalyzer.Domain.ProjectFile", b =>
                {
                    b.Navigation("Issues");
                });

            modelBuilder.Entity("InfoSupport.StaticCodeAnalyzer.Domain.Report", b =>
                {
                    b.Navigation("ProjectFiles");
                });
#pragma warning restore 612, 618
        }
    }
}
