using InfoSupport.StaticCodeAnalyzer.Domain;

namespace InfoSupport.StaticCodeAnalyzer.WebApp.Services;

public record IssueData(string Name, string Description)
{
    public string Severity { get; set; } = string.Empty;
}

public class IssueDescriptionService
{
    private static readonly IssueData ClassParentsIssueData = new(
        "Too many class parents",
        "This issue arises when a class inherits from too many parent classes, potentially leading to a complex and brittle inheritance hierarchy."
    );

    private static readonly IssueData TooManyElsesIssueData = new(
        "Too many 'else' statements",
        "This issue occurs when a method contains multiple 'else' statements, which can complicate logic flow and reduce readability."
    );

    private static readonly IssueData MethodTooLargeIssueData = new(
        "Method too large",
        "This issue indicates that a method's size exceeds the recommended number of statements, making it hard to maintain and understand."
    );

    private static readonly IssueData ClassTooLargeIssueData = new(
        "Class too large",
        "This issue is flagged when a class has too many class members, suggesting high complexity and low cohesion."
    );

    private static readonly IssueData MagicNumberIssueData = new(
        "Magic number",
        "This issue highlights the use of hard-coded numeric constants, which can make the code less understandable and maintainable."
    );

    private static readonly IssueData TooManyMethodParametersIssueData = new(
        "Too many method parameters",
        "This issue occurs when a method has too many parameters, which can make the method difficult to use and modify."
    );

    private static readonly IssueData NestedTernaryIssueData = new(
        "Nested ternary operators",
        "This issue occurs when ternary operators are nested within one another, significantly reducing code readability."
    );

    private static readonly IssueData PartialVariableAssignmentIssueData = new(
        "Partial variable assignment",
        "This issue flags situations where only some variables in a declared group are initialized, leading to potential misunderstandings."
    );

    private static readonly IssueData SwitchTooManyCasesIssueData = new(
        "Switch with too many cases",
        "This issue occurs when a 'switch' statement has too many 'case' branches, suggesting that a different control structure might be more appropriate."
    );

    private static readonly IssueData TestMethodWithoutAssertionIssueData = new(
        "Test method without assertion",
        "This issue is identified when a test method does not contain any assertions, which may indicate that the test is not verifying outcomes effectively."
    );

    private static readonly IssueData UnusedParameterIssueData = new(
        "Unused parameter",
        "This issue is reported when a method parameter is never used within the method body, suggesting that it could be removed to clarify the method's intent."
    );

    public static IssueData GetIssueData(Issue issue)
    {
        var meta = issue.Code switch
        {
            "too-many-class-parents" => ClassParentsIssueData,
            "too-many-elses" => TooManyElsesIssueData,
            "method-too-large" => MethodTooLargeIssueData,
            "class-too-large" => ClassTooLargeIssueData,
            "magic-number" => MagicNumberIssueData,
            "too-many-method-parameters" => TooManyMethodParametersIssueData,
            "nested-ternary" => NestedTernaryIssueData,
            "partial-variable-assignment" => PartialVariableAssignmentIssueData,
            "switch-too-many-cases" => SwitchTooManyCasesIssueData,
            "test-method-without-assertion" => TestMethodWithoutAssertionIssueData,
            "unused-parameter" => UnusedParameterIssueData,
            _ => throw new ArgumentException("Unknown issue code", nameof(issue))
        };

        meta.Severity = issue.AnalyzerSeverity switch
        {
            AnalyzerSeverity.Suggestion => "info",
            AnalyzerSeverity.Warning => "warning",
            AnalyzerSeverity.Important => "error",
            _ => "hint"
        };

        return meta;
    }
}
