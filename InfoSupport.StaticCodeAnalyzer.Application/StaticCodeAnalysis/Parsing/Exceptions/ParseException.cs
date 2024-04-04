using System.Diagnostics.CodeAnalysis;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing.Exceptions;

[ExcludeFromCodeCoverage]
public class ParseException(string message) : Exception(message)
{
}
