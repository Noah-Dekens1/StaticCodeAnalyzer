﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Domain;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis;

public class Configuration
{
    public required List<AnalyzersListConfig> Analyzers { get; set; }

    public required SeverityConfig Severities { get; set; }
    public required CodeGuardConfig CodeGuard { get; set; }
    public DirectoriesConfig Directories { get; set; } = new();
}

public class DirectoriesConfig
{
    public List<string> Excluded { get; set; } = [];
}

public class AnalyzersListConfig
{
    public MaxParentsConfig ClassParents { get; set; } = new();
    public MaxStatementsConfig LargeMethods { get; set; } = new();
    public MaxElsesConfig IfElse { get; set; } = new();
    public MaxMembersConfig LargeTypes { get; set; } = new();
    public AnalyzerConfig MagicNumbers { get; set; } = new();
    public MaxParametersConfig MethodParameterCount { get; set; } = new();
    public AnalyzerConfig NestedTernary { get; set; } = new();
    public AnalyzerConfig PartialVariableAssignment { get; set; } = new();
    public MaxCasesConfig SwitchCases { get; set; } = new();
    public TestAssertionsConfig TestAssertions { get; set; } = new();
    public UnusedParametersConfig UnusedParameters { get; set; } = new();
    
    // Unused
    public AnalyzerConfig DuplicateCode { get; set; } = new();
}

public class SeverityConfig
{
    public uint Suggestion { get; set; } = 0;
    public uint Warning { get; set; } = 1;
    public uint Important { get; set; } = 5;
}

public class CodeGuardConfig
{
    public bool FailOnReachSeverityScore { get; set; } = true;
    public uint MaxAllowedSeverityScore { get; set; } = 50;
}

public class AnalyzerConfig
{
    public bool Enabled { get; set; } = true;
    public string Severity { get; set; } = "warning";

    public AnalyzerSeverity AnalyzerSeverity
        => Severity switch
        {
            "suggestion" => AnalyzerSeverity.Suggestion,
            "warning" => AnalyzerSeverity.Warning,
            "important" => AnalyzerSeverity.Important,
            _ => AnalyzerSeverity.Invalid,
        };
}

public class MaxParentsConfig : AnalyzerConfig
{
    public int MaxParents { get; set; } = 5;
}

public class MaxStatementsConfig : AnalyzerConfig
{
    public int MaxStatements { get; set; } = 30;
}

public class MaxElsesConfig : AnalyzerConfig
{
    public int MaxElses { get; set; } = 3;
}

public class MaxMembersConfig : AnalyzerConfig
{
    public int MaxMembers { get; set; } = 30;
}

public class MaxParametersConfig : AnalyzerConfig
{
    public int MaxParameters { get; set; } = 5;
}

public class MaxCasesConfig : AnalyzerConfig
{
    public int MaxCases { get; set; } = 10;
}

public class TestAssertionsConfig : AnalyzerConfig
{
    public bool AnyNameIncludingAssert { get; set; } = true;
    public bool CheckCalledMethods { get; set; } = true;
    public bool UseCustomAssertionMethods { get; set; } = false;
    public List<string> AssertionMethods { get; set; } = [];
}

public class UnusedParametersConfig : AnalyzerConfig
{
    public List<string> IgnoreWhenImplementingTypes { get; set; } = [];
}
