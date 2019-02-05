using Common.DocDB;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Benchmark.Export.DocDb
{
    public class DocumentExporter : IExporter 
    {
        public DocumentExporter()
        {
            
        }

        public void Export(string query, Action<IEnumerable<IDocument>> writeAction, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}