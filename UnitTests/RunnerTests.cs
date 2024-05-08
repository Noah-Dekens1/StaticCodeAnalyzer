using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.UnitTests;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Text.Json;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis;
using InfoSupport.StaticCodeAnalyzer.Domain;

[TestClass]
public class RunnerTests
{
    private static Configuration GetDefaultConfig()
    {
        return new Configuration()
        {
            Analyzers = [new()],
            Severities = new SeverityConfig(),
            CodeGuard = new CodeGuardConfig(),
        };
    }

    private static JsonSerializerOptions GetSerializationOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    [TestMethod]
    public void Test_RunAnalysis_NoFiles_ProjectDirectoryExists()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { "/test/", new MockDirectoryData() }  // Project directory exists but no files
        });
        var runner = new Runner(mockFileSystem);

        var project = new Project { Path = "/test" };
        var cancellationToken = CancellationToken.None;

        // Act
        var report = runner.RunAnalysis(project, cancellationToken);

        // Assert
        Assert.IsNotNull(report);
        Assert.AreEqual(0, report.ProjectFiles.Count);
    }

    [TestMethod]
    public void Test_RunAnalysis_NoFiles_ProjectDirectoryDoesNotExist()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>());
        var runner = new Runner(mockFileSystem);

        var project = new Project { Path = "/nonexistent" };
        var cancellationToken = CancellationToken.None;

        // Act & Assert
        Assert.ThrowsException<DirectoryNotFoundException>(() =>
        {
            runner.RunAnalysis(project, cancellationToken);
        });
    }

    [TestMethod]
    public void Test_RunAnalysis_WithAnalyzerConfig()
    {
        // Arrange
        var configContent = JsonSerializer.Serialize(GetDefaultConfig(), GetSerializationOptions());
        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { "/test/analyzer-config.json", new MockFileData(configContent) },
            { "/test/test1.cs", new MockFileData("class Test1 {}") }
        });
        var runner = new Runner(mockFileSystem);

        var project = new Project { Path = "/test" };
        var cancellationToken = CancellationToken.None;

        // Act
        var report = runner.RunAnalysis(project, cancellationToken);

        // Assert
        Assert.IsNotNull(report);
        Assert.AreEqual(1, report.ProjectFiles.Count);
    }

    [TestMethod]
    public void Test_RunAnalysis_WithByteOrderMarkInFile()
    {
        // Arrange
        var configContent = JsonSerializer.Serialize(GetDefaultConfig(), GetSerializationOptions());
        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { "/test/analyzer-config.json", new MockFileData(configContent) },
            { "/test/test1.cs", new MockFileData($"{(char)0xfeff}class Test1 {{}}") }
        });
        var runner = new Runner(mockFileSystem);

        var project = new Project { Path = "/test" };
        var cancellationToken = CancellationToken.None;

        // Act
        var report = runner.RunAnalysis(project, cancellationToken);

        // Assert
        Assert.IsNotNull(report);
        Assert.AreEqual(1, report.ProjectFiles.Count);
    }

    [TestMethod]
    public void Test_RunAnalysis_InvalidAnalyzerConfig()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { "/test/analyzer-config.json", new MockFileData("invalid json content") },
            { "/test/test1.cs", new MockFileData("class Test1 {}") }
        });
        var runner = new Runner(mockFileSystem);

        var project = new Project { Path = "/test" };
        var cancellationToken = CancellationToken.None;

        // Act
        var report = runner.RunAnalysis(project, cancellationToken);

        // Assert
        Assert.IsNull(report);
    }
}
