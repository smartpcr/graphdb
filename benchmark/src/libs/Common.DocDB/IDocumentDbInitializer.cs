using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace Common.DocDB
{
    public interface IDocumentDbInitializer
    {
        IDocumentClient GetClient(string endpointUrl, string authorizationKey, ConnectionPolicy connectionPolicy = null);
    }
}
