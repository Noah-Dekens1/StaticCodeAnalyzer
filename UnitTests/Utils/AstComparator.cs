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
    private readonly Dictionary<Type, List<string>> _ignoredProperties = [];

    [DebuggerHidden]
    public static AstComparator Create()
    {
        return new AstComparator();
    }

    //[DebuggerHidden]
    public AstComparator IgnorePropertyOfType<T>(Expression<Func<T, object>> selector)
    {
        var type = typeof(T);

        // @note cast to UnaryExpression in case of value types since boxing occurs
        var memberExpression = selector.Body as MemberExpression ??
                           (selector.Body as UnaryExpression)?.Operand as MemberExpression;

        if (memberExpression == null)
        {
            throw new ArgumentException("The provided selector does not refer to a property.");
        }

        var value = memberExpression.Member.Name;

        if (_ignoredProperties.TryGetValue(type, out List<string>? list))
        {
            list.Add(value);
        }
        else
        {
            _ignoredProperties.Add(type, [value]);
        }

        return this;
    }

    [DebuggerHidden]
    public void Compare(AST expected, AST actual)
    {
        CompareRecursive(expected, actual, "");
    }

    private void CompareRecursive(object? expected, object? actual, string fieldPath)
    {
        if (expected is null ^ actual is null)
        {
            throw new AssertFailedException($"Nullability of {fieldPath} didn't match");
        }

        // If both are null then they match (using || because the compiler doesn't understand the combination)
        if (expected is null || actual is null)
        {
            return;
        }

        var expectedType = expected.GetType();
        var actualType = actual.GetType();

        //Console.WriteLine($"Checking {expectedType}");

        if (expectedType != actualType)
        {
            throw new AssertFailedException($"Types of objects at {fieldPath} didn't match expected: {expectedType}, actual: {actualType}");
        }

        var type = expectedType;

        // get public properties/fields, don't care about private ones
        PropertyInfo[] properties = type.GetProperties();
        FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);


        foreach (var property in properties)
        {
            //Console.WriteLine($"Checking property {property.Name}");
            var expectedValue = property.GetValue(expected);
            var actualValue = property.GetValue(actual);

            var exclude = false;

            foreach (var (classType, ignored) in _ignoredProperties)
            {
                if (classType.IsAssignableFrom(type) && ignored.Contains(property.Name))
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
                    throw new AssertFailedException($"Nullability of string properties {fieldPath}.{property.Name} didn't match" +
                        $", expected {expectedValue} but got {actualValue}");

                if (expectedValue is null || actualValue is null)
                    continue;

                if (!expectedValue.Equals(actualValue))
                {
                    throw new AssertFailedException($"Value-type properties {fieldPath}.{property.Name} didn't match" +
                        $", expected {expectedValue} but got {actualValue}");
                }

                continue;
            }

            var runtimeType = expectedValue?.GetType();
            bool isRuntimeValueType = runtimeType?.IsValueType ?? false;

            if (expectedValue is IEnumerable enumerable && actualValue is IEnumerable actualEnumerable)
            {
                List<object> expectedList = [.. enumerable];
                List<object> actualList = [.. actualEnumerable];

                var expectedCount = expectedList.Count;
                var actualCount = actualList.Count;

                if (expectedCount != actualCount)
                {
                    throw new AssertFailedException($"[Prop {fieldPath}.{property.Name}]: Expected {expectedCount} items but got {actualCount}");
                }

                for (int i = 0; i < expectedCount; i++)
                {
                    CompareRecursive(expectedList[i], actualList[i], $"{fieldPath}.{property.Name}[{i}]");
                }

                continue;
            }

            if (isRuntimeValueType)
            {
                var isStruct = runtimeType is not null
                    && !runtimeType!.IsPrimitive && !runtimeType!.IsEnum
                    && runtimeType != typeof(decimal);

                if (isStruct)
                {
                    CompareRecursive(expectedValue, actualValue, $"{fieldPath}.{property.Name}");

                    continue;
                }

                if (!expectedValue!.Equals(actualValue))
                {
                    throw new AssertFailedException($"Value-type properties {fieldPath}.{property.Name} didn't match" +
                        $", expected {expectedValue} but got {actualValue}");
                }
            }
            else
            {
                CompareRecursive(expectedValue, actualValue, $"{fieldPath}.{property.Name}");
            }
        }

        foreach (var field in fields)
        {
            var expectedValue = field.GetValue(expected);
            var actualValue = field.GetValue(actual);

            var exclude = false;

            foreach (var (classType, ignored) in _ignoredProperties)
            {
                if (classType.IsAssignableFrom(type) && ignored.Contains(field.Name))
                {
                    exclude = true;
                    break;
                }
            }

            if (exclude)
                continue;

            // String is a special case
            if (typeof(string) == field.FieldType)
            {
                if (expectedValue is null ^ actualValue is null)
                    throw new AssertFailedException($"Nullability of string fields {fieldPath}.{field.Name} didn't match" +
                        $", expected {expectedValue} but got {actualValue}");

                if (expectedValue is null || actualValue is null)
                    continue;

                if (!expectedValue.Equals(actualValue))
                {
                    throw new AssertFailedException($"Value-type fields {fieldPath}.{field.Name} didn't match" +
                        $", expected {expectedValue} but got {actualValue}");
                }

                continue;
            }

            var runtimeType = expectedValue?.GetType();
            bool isRuntimeValueType = runtimeType?.IsValueType ?? false;

            if (expectedValue is IEnumerable enumerable && actualValue is IEnumerable actualEnumerable)
            {
                List<object> expectedList = [.. enumerable];
                List<object> actualList = [.. actualEnumerable];

                var expectedCount = expectedList.Count;
                var actualCount = actualList.Count;

                if (expectedCount != actualCount)
                {
                    throw new AssertFailedException($"[Field {fieldPath}.{field.Name}]: Expected {expectedCount} items but got {actualCount}");
                }

                for (int i = 0; i < expectedCount; i++)
                {
                    CompareRecursive(expectedList[i], actualList[i], $"{fieldPath}.{field.Name}[{i}]");
                }

                continue;
            }

            if (isRuntimeValueType)
            {
                var isStruct = runtimeType is not null
                    && runtimeType!.IsPrimitive && runtimeType!.IsEnum
                    && runtimeType != typeof(decimal);

                if (isStruct)
                {
                    CompareRecursive(expectedValue, actualValue, $"{fieldPath}.{field.Name}");
                }

                if (!expectedValue!.Equals(actualValue))
                {
                    throw new AssertFailedException($"Value-type fields {fieldPath}.{field.Name} didn't match" +
                        $", expected {expectedValue} but got {actualValue}");
                }
            }
            else
            {
                CompareRecursive(expectedValue, actualValue, $"{fieldPath}.{field.Name}");
            }
        }
    }
}