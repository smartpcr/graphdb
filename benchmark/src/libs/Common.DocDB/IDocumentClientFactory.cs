using Microsoft.Azure.CosmosDB.BulkExecutor;
using Microsoft.Azure.Documents;
using System.Threading.Tasks;

namespace Common.DocDB
{
    public interface IDocumentClientFactory
    {
        IDocumentClient GetClient(CosmosDbSetting setting);
        Task<IBulkExecutor> GetBulkExecutor(CosmosDbSetting setting);
    }
}
