using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing.Misc;

[ExcludeFromCodeCoverage]
public class ScopedTimer(string name) : IDisposable
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private bool _disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                Console.WriteLine($"[Timer] Timer finished for {name} in {_stopwatch.Elapsed}");
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}