using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

namespace InfoSupport.StaticCodeAnalyzer.UnitTests.Utils;

public class AstComparator
{
    private readonly Dictionary<Type, string> _ignoredProperties = [];

    [DebuggerHidden]
    public static AstComparator Create()
    { 
        return new AstComparator();
    }

    [DebuggerHidden]
    public AstComparator IgnorePropertyOfType<T>(Expression<Func<T, object>> selector)
    {
        _ignoredProperties.Add(typeof(T), ((MemberExpression)selector.Body).Member.Name);
        return this;
    }

    [DebuggerHidden]
    public void Compare(AST expected, AST actual)
    {
        CompareRecursive(expected, actual, "");
    }

    private void CompareRecursive(object? expected, object? actual, string propertyPath)
    {
        if (expected is null ^ actual is null)
        {
            throw new AssertFailedException($"Nullability didn't match");
        }

        // If both are null then they match (using || because the compiler doesn't understand the combination)
        if (expected is null || actual is null)
        {
            return;
        }

        var expectedType = expected.GetType();
        var actualType = actual.GetType();

        Console.WriteLine($"Checking {expectedType}");

        if (expectedType != actualType)
        {
            throw new AssertFailedException($"Types of objects at {propertyPath} didn't match expected: {expectedType}, actual: {actualType}");
        }

        var type = expectedType;

        // get public properties/fields, don't care about private ones
        PropertyInfo[] properties = type.GetProperties();
        FieldInfo[] fields = type.GetFields();


        foreach (var property in properties)
        {
            Console.WriteLine($"Checking property {property.Name}");
            var expectedValue = property.GetValue(expected);
            var actualValue = property.GetValue(actual);

            var exclude = false;

            foreach (var (classType, name) in _ignoredProperties)
            {
                if (classType.IsAssignableFrom(type) && property.Name == name) 
                {
                    exclude = true;
                    break;
                }
            }

            if (exclude)
                continue;

            // String is a special case
            if (typeof(string) == property.PropertyType)
            {
                if (expectedValue is null ^ actualValue is null)
                    throw new AssertFailedException($"Nullability of string properties {propertyPath}.{property.Name} didn't match" +
                        $", expected {expectedValue} but got {actualValue}");

                if (expectedValue is null || actualValue is null)
                    continue;

                if (!expectedValue.Equals(actualValue))
                {
                    throw new AssertFailedException($"Value-type properties didn't match" +
                        $", expected {expectedValue} but got {actualValue}");
                }

                continue;
            }

            bool isRuntimeValueType = expectedValue?.GetType().IsValueType ?? false;

            if (expectedValue is IEnumerable enumerable && actualValue is IEnumerable actualEnumerable)
            {
                List<object> expectedList = [..enumerable];
                List<object> actualList = [..actualEnumerable];

                var expectedCount = expectedList.Count;
                var actualCount = actualList.Count;

                if (expectedCount != actualCount)
                {
                    throw new AssertFailedException($"[Prop {property.Name}]: Expected {expectedCount} items but got {actualCount}");
                }

                for (int i = 0; i < expectedCount; i++)
                {
                    CompareRecursive(expectedList[i], actualList[i], $"{propertyPath}.{property.Name}[{i}]");
                }

                continue;
            }

            if (isRuntimeValueType)
            {
                if (!expectedValue!.Equals(actualValue))
                {
                    throw new AssertFailedException($"Value-type properties {propertyPath}.{property.Name} didn't match" +
                        $", expected {expectedValue} but got {actualValue}");
                }
            }
            else
            {
                CompareRecursive(expectedValue, actualValue, $"{propertyPath}.{property.Name}");
            }
        }
    }
}
