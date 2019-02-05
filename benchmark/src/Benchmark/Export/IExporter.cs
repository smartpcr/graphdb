using Common.DocDB;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Benchmark.Export
{
    public interface IExporter
    {
        void Export(string query, Action<IEnumerable<IDocument>> writeAction, CancellationToken token);
    }
}