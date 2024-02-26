using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.CLI;
internal class InterpolationTest
{
    void a()
    {
        var a = @$"{{ {"hello"} {{}} {$"{"hello" + $"{"a}"}"}"} ""Hello 1""  ";
    }
}
