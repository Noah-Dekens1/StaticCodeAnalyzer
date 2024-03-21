using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.CLI;
internal class InterpolationTest
{
    public static void A()
    {
#pragma warning disable CS0219 // Variable is assigned but its value is never used
#pragma warning disable IDE0059 // Unnecessary assignment of a value
        var t = "\n";
        var a = @$"{{ {"hello"} {{}} {$"{"hello" + $"{"a}"}"}"} ""Hello 1""  ";
#pragma warning restore CS0219 // Variable is assigned but its value is never used
#pragma warning restore IDE0059 // Unnecessary assignment of a value
    }
}