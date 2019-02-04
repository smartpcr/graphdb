using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace Benchmark
{
    public interface IDocumentDbInitializer
    {
        IDocumentClient GetClient(string endpointUrl, string authorizationKey, ConnectionPolicy connectionPolicy = null);
    }
}
