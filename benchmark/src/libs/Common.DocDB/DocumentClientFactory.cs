using System;
using System.Threading.Tasks;
using Microsoft.Azure.CosmosDB.BulkExecutor;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Common.DocDB
{
    public class DocumentClientFactory : IDocumentClientFactory
    {
        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public IDocumentClient GetClient(CosmosDbSetting setting)
        {
            var dbConn = GetDbConnection(setting);
            var client = new DocumentClient(dbConn.VaultUrl, dbConn.AuthorizationKey, JsonSerializerSettings, dbConn.ConnectionPolicy);
            return client;
        }

        public IDocumentClient GetClient(string acct, string authKey)
        {
            var client = new DocumentClient(new Uri($"https://{acct}.documents.azure.com:443/"), authKey, JsonSerializerSettings, new ConnectionPolicy());
            return client;
        }

        public async Task<IBulkExecutor> GetBulkExecutor(CosmosDbSetting setting)
        {
            var dbConn = GetDbConnection(setting);
            dbConn.ConnectionPolicy.RetryOptions.MaxRetryWaitTimeInSeconds = 30;
            dbConn.ConnectionPolicy.RetryOptions.MaxRetryAttemptsOnThrottledRequests = 9;

            var client = new DocumentClient(dbConn.VaultUrl, dbConn.AuthorizationKey, JsonSerializerSettings, dbConn.ConnectionPolicy);
            var collection = await client.EnsureDatabaseAndCollection(setting.DbName, setting.CollectionName);

            return await CreateBulkExecutor(client, collection);
        }

        public async Task<IBulkExecutor> GetBulkExecutor(string acct, string authKey, string dbName, string collName)
        {
            var client = new DocumentClient(new Uri($"https://{acct}.documents.azure.com:443/"), authKey, JsonSerializerSettings, new ConnectionPolicy());
            var collection = await client.EnsureDatabaseAndCollection(dbName, collName);

            return await CreateBulkExecutor(client, collection);
        }

        private (Uri VaultUrl, string AuthorizationKey, ConnectionPolicy ConnectionPolicy) GetDbConnection(CosmosDbSetting setting)
        {
            var endpointUrl = $"https://{setting.AccountName}.documents.azure.com:443/";
            var connectionPolicy = new ConnectionPolicy()
            {
                ConnectionMode = ConnectionMode.Direct,
                ConnectionProtocol = Protocol.Tcp,
                RequestTimeout = TimeSpan.FromSeconds(setting.TimeoutInSeconds)
            };
            return (new Uri(endpointUrl), setting.AuthKey, connectionPolicy);
        }

        private async Task<IBulkExecutor> CreateBulkExecutor(DocumentClient client, DocumentCollection collection)
        {
            try
            {
                var bulkExecutor = new BulkExecutor(client, collection);
                await bulkExecutor.InitializeAsync();

                client.ConnectionPolicy.RetryOptions.MaxRetryWaitTimeInSeconds = 0;
                client.ConnectionPolicy.RetryOptions.MaxRetryAttemptsOnThrottledRequests = 0;

                return bulkExecutor;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }
    }
}
