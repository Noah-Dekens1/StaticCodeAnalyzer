using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis;

public class Configuration
{
    public required List<AnalyzersListConfig> Analyzers { get; set; }
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
    public AnalyzerConfig UnusedParameterAnalyzer { get; set; } = new();
    
    // Unused
    public AnalyzerConfig DuplicateCode { get; set; } = new();
}


public class AnalyzerConfig
{
    public bool Enabled { get; set; } = true;
}

public class MaxParentsConfig : AnalyzerConfig
{
    public int MaxParents { get; set; }
}

public class MaxStatementsConfig : AnalyzerConfig
{
    public int MaxStatements { get; set; }
}

public class MaxElsesConfig : AnalyzerConfig
{
    public int MaxElses { get; set; }
}

public class MaxMembersConfig : AnalyzerConfig
{
    public int MaxMembers { get; set; }
}

public class MaxParametersConfig : AnalyzerConfig
{
    public int MaxParameters { get; set; }
}

public class MaxCasesConfig : AnalyzerConfig
{
    public int MaxCases { get; set; }
}

public class TestAssertionsConfig : AnalyzerConfig
{
    public bool AnyNameIncludingAssert { get; set; }
    public bool CheckCalledMethods { get; set; }
    public bool UseCustomAssertionMethods { get; set; }
    public List<string> AssertionMethods { get; set; } = [];
}


