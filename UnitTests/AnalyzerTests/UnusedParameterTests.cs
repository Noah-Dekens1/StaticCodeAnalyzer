using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Analyzers;
using InfoSupport.StaticCodeAnalyzer.UnitTests.Utils;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnalyzerTests;

[TestClass]
public class UnusedParameterTests
{
    [TestMethod]
    public void Analyze_UsedParameter_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Test(int a)
            {
                Console.WriteLine(a);
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_SimpleUnusedParameter_ReturnsIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Test(int a)
            {
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(1, issues.Count);
        Assert.AreEqual(issues[0].Code, "unused-parameter");
    }

    [TestMethod]
    public void Analyze_SimpleMixedParameters_ReturnsIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Test(int a, int b)
            {
                Console.WriteLine(b);
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(1, issues.Count);
        Assert.AreEqual(issues[0].Code, "unused-parameter");
    }

    [TestMethod]
    public void Analyze_MultipleUnusedParameters_ReturnsMultipleIssues()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Test(int a, int b)
            {
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(2, issues.Count);
        Assert.AreEqual(issues[0].Code, "unused-parameter");
        Assert.AreEqual(issues[1].Code, "unused-parameter");
    }

    [TestMethod]
    public void Analyze_UnusedParametersButUsedIdentifier_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Test(int a)
            {
                Utils.a.Method();
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(1, issues.Count);
    }

    [TestMethod]
    public void Analyze_UsedParameterByMemberAccess_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Test(Person person)
            {
                Console.WriteLine(person.Name);
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_DiscardedParameterBeforeUse_ReturnsIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Test(Person person)
            {
                person = new Person();
                Console.WriteLine(person.Name);
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(1, issues.Count);
    }

    [TestMethod]
    public void Analyze_DiscardedParameterBeforeUseConditionally_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Test(Person person)
            {
                if (3 == 4)
                {
                    person = new Person();
                }

                Console.WriteLine(person.Name);
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_DiscardedParameterBeforeUseConditionallyNoScope_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Test(Person person)
            {
                if (3 == 4)
                    person = new Person();

                Console.WriteLine(person.Name);
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_UsedParameterInMemberAccess_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            async Task<Report?> GetReportById(Guid id)
            {
                return await _context.Reports
                    .Where(r => r.Id == id)
                    .Include(r => r.ProjectFiles)
                    .ThenInclude(f => f.Issues)
                    .SingleOrDefaultAsync();
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_UsedParameterInLeftmostMemberAccess_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            T EmitStatic<T>(T node, CodeLocation location) where T : AstNode
                {
                    node.Location = location;
            #if DEBUG
                    node.ConstructedInEmit = true;
            #endif
                    return node;
                }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_ForcedParametersFromInterface_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            public interface IExample
            {
                public void Method(bool unusedValue);
            }
            
            public class Example : IExample
            {
                public void Method(bool unusedValue)
                {
                }
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_AssignWithSelfOnLhsAndRhs_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Example(int a, int b)
            {
                a = a;              // unused param
                b = SomeMethod(b);  // used param
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(1, issues.Count);
    }

    [TestMethod]
    public void Analyze_UseParameterInUnreachableCode_ReturnsIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Example(int a, int b)
            {
                return;
                Console.WriteLine(a);
                Console.WriteLine(b);
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(2, issues.Count);
    }

    [TestMethod]
    public void Analyze_ParameterUsedInForEach_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void CreateNamespace(NamespaceRepNode ns, NamespaceNode source)
            {
                foreach (var childNs in source.Namespaces)
                {
                    HandleNamespace(ns, childNs, recurse: true);
                }
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_ComplexCode_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            string ReadStringLiteral(StringData topString, int stringStart)
            {
                var literalBuilder = new StringBuilder();
                //var stringStart = _index;

                Stack<StackString> stack = [];
                stack.Push(new StackString(false, topString));

                bool MatchString(out StringData str, bool includeConsumed)
                {
                    str = new();
                    int dollarSigns, rawQuotes;

                    // Backtracking because I'm lazy
                    var pos = Tell();
                    int peekIndex = 0;
                    if ((dollarSigns = PeekMatchGreedy('$', ref peekIndex, 1)) != -1)
                    {
                        if ((rawQuotes = PeekMatchGreedy('"', ref peekIndex, 3)) != -1)
                        {
                            str.IsRaw = true;
                            str.DollarSignCount = dollarSigns;
                            str.DQouteCount = rawQuotes;
                            str.IsInterpolated = true;
                            Seek(_index + peekIndex);
                            return true;
                        }

                        // We didn't find a match so backtrack so that $"" or $@"" or @$"" still can match
                        //Seek(pos);
                        // no need to backtrack anymore as we didn't consume anything
                    }
                    // Check for regular raw string
                    else if ((rawQuotes = ConsumeIfMatchGreedy('"', 3)) != -1)
                    {
                        str.IsRaw = true;
                        str.IsInterpolated = false;
                        str.DQouteCount = rawQuotes;
                        return true;
                    }

                    if (
                        ConsumeIfMatchSequence(['@', '$', '"'], includeConsumed) ||
                        ConsumeIfMatchSequence(['$', '@', '"'], includeConsumed)
                        )
                    {
                        str.IsInterpolated = true;
                        str.IsVerbatim = true;
                        return true;
                    }
                    else if (ConsumeIfMatchSequence(['@', '"'], includeConsumed))
                    {
                        str.IsVerbatim = true;
                        return true;
                    }
                    else if (ConsumeIfMatchSequence(['$', '"'], includeConsumed))
                    {
                        str.IsInterpolated = true;
                        return true;
                    }
                    else if (ConsumeIfMatch('"', includeConsumed))
                    {
                        return true;
                    }

                    return false;
                }

                while (!IsAtEnd() && stack.Count != 0)
                {
                    char c = PeekCurrent();

                    //literalBuilder.Append(c);

                    var strData = stack.Peek();
                    var str = strData.StringData;

                    if (strData.IsCode)
                    {
                        var possibleMatchStart = _index;

                        StringData? lastStr = null;

                        foreach (var item in stack)
                        {
                            if (item.IsCode)
                                continue;

                            lastStr = item.StringData;
                            break;
                        }

                        if (MatchString(out var stringData, false))
                        {
                            stack.Push(new StackString(false, stringData));
                        }
                        else if (((lastStr.HasValue && !lastStr.Value.IsRaw) || !lastStr.HasValue) && ConsumeIfMatch('}'))
                        {
                            Debug.Assert(stack.Peek().IsCode);
                            stack.Pop();
                        }
                        // Wait a sec, do we have to look up the stack to see the dollarsigncount?
                        else if (lastStr.HasValue && lastStr.Value.IsRaw && ConsumeIfMatchGreedy('}', lastStr.Value.DollarSignCount, lastStr.Value.DollarSignCount) != -1)
                        {
                            Debug.Assert(stack.Peek().IsCode);
                            stack.Pop();
                            continue;
                        }
                        else
                        {
                            Consume();
                        }

                        continue;
                    }

                    if (!str!.Value.IsVerbatim && !str!.Value.IsRaw)
                    {
                        if (ConsumeIfMatch('\\'))
                        {
                            if (ConsumeIfMatch('"'))
                            {
                                continue;
                            }
                            else if (ConsumeIfMatch('\\'))
                            {
                                continue;
                            }
                        }

                        if (ConsumeIfMatch('"'))
                        {
                            stack.Pop();
                            continue;
                        }
                        else if (!str.Value.IsInterpolated)
                        {
                            Consume();
                            continue;
                        }
                    }
                    else if (str!.Value.IsRaw)
                    {
                        var dollarSignCount = str.Value.DollarSignCount;
                        var dquoteCount = str.Value.DQouteCount;
                        var possibleStart = Tell();

                        if (str.Value.IsInterpolated && ConsumeIfMatchGreedy('{', dollarSignCount, dollarSignCount) != -1)
                        {
                            stack.Push(new StackString(true, null));
                            continue;
                        }

                        if (ConsumeIfMatchGreedy('"', dquoteCount, dquoteCount) != -1)
                        {
                            stack.Pop();
                            continue;
                        }

                        Consume(); // We don't let standard interpolation handle consuming characters so we have to
                    }
                    else
                    {

                        if (ConsumeIfMatch('"'))
                        {
                            if (!ConsumeIfMatch('"'))
                            {
                                bool shouldEnterString = stack.Peek().IsCode;

                                if (!shouldEnterString)
                                {
                                    stack.Pop();
                                    continue;
                                }
                            }

                            continue;
                        }
                        else if (!str.Value.IsInterpolated) // We've handled it so consume any character
                        {
                            Consume();
                            continue;
                        }
                    }

                    if (str.Value.IsInterpolated && !str.Value.IsRaw)
                    {
                        if (ConsumeIfMatch('{'))
                        {
                            if (!ConsumeIfMatch('{'))
                            {
                                stack.Push(new StackString(true, null));
                            }
                        }
                        else
                        {
                            Consume();
                        }
                    }
                }
                // This doesn't catch all issues of course (stack could be empty before string completes)
                // But this way we do prevent *some* overruns
                Debug.Assert(stack.Count == 0);
                var stringEnd = _index;

                for (int i = stringStart; i < stringEnd; i++)
                {
                    literalBuilder.Append(_input[i]);
                }

                //Console.WriteLine(literalBuilder.ToString());

                return literalBuilder.ToString();
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_IgnoredBaseType_ReturnsNoIssue()
    {
        var config = new AnalyzersListConfig();
        config.UnusedParameters.IgnoreWhenImplementingTypes.Add("SomeBase");

        var issues = AnalyzerUtils.Analyze("""
            class Example : SomeBase
            {
                public void Test(int a)
                {

                }
            }
            """, new UnusedParameterAnalyzer(), config);

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_Try_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            async Task<IActionResult> UpdateFolders([FromBody] FolderInDto folderInDto)
            {
                try
                {
                    var email = HttpContext.User.Identity!.Name;
                    await FolderService.UpdateFoldersAsync(email, folderInDto);
                    return Ok();
                }
                catch (CommonErrorException e)
                {
                    Logger.LogWarning("{ErrorMessage}", e.Message);
                    return StatusCode(e.Error.Status, e.Error);
                }
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_Index_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            bool IsLastMessage(dynamic jsonObj)
            {
                var firstChoice = jsonObj["choices"][0];

                var finishReason = firstChoice["finish_reason"];
                return finishReason != null;
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }
}
