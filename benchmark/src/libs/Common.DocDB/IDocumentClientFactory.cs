using Microsoft.Azure.CosmosDB.BulkExecutor;
using Microsoft.Azure.Documents;
using System.Threading.Tasks;

namespace Common.DocDB
{
    public interface IDocumentClientFactory
    {
        IDocumentClient GetClient(CosmosDbSetting setting);
        IDocumentClient GetClient(string acct, string authKey);
        Task<IBulkExecutor> GetBulkExecutor(CosmosDbSetting setting);
        Task<IBulkExecutor> GetBulkExecutor(string acct, string authKey, string dbName, string collName);
    }
}
