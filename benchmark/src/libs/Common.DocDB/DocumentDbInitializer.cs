using System;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace Common.DocDB
{
    public class DocumentDbInitializer : IDocumentDbInitializer
    {
        public IDocumentClient GetClient(string endpointUrl, string authorizationKey, ConnectionPolicy connectionPolicy = null)
        {
            if (string.IsNullOrWhiteSpace(endpointUrl))
                throw new ArgumentNullException(nameof(endpointUrl));

            if (string.IsNullOrWhiteSpace(authorizationKey))
                throw new ArgumentNullException(nameof(authorizationKey));

            var documentClient = new DocumentClient(new Uri(endpointUrl), authorizationKey, connectionPolicy ?? new ConnectionPolicy());
            return documentClient;
        }
    }
}
