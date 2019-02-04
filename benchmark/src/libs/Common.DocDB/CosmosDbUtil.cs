using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace Common.DocDB
{
    internal class CosmosDbUtil
    {
        private readonly DocumentClient _client;

        public CosmosDbUtil(DocumentClient client)
        {
            _client = client;
        }

        public async Task<Database> EnsureDatabase(string dbName)
        {
            var db = _client.CreateDatabaseQuery().Where(d => d.Id == dbName)
                         .AsEnumerable().FirstOrDefault() 
                     ?? await _client.CreateDatabaseAsync(new Database() {Id = dbName});

            return db;
        }

        public async Task<DocumentCollection> EnsureCollection(string dbName, string collectionName, int throughput = 1000)
        {
            var db = await EnsureDatabase(dbName);
            var collection = _client.CreateDocumentCollectionQuery(db.SelfLink)
                                        .Where(c => c.Id == collectionName)
                                        .AsEnumerable().FirstOrDefault()
                                    ?? await _client.CreateDocumentCollectionAsync(db.SelfLink, new DocumentCollection()
                                    {
                                        Id = collectionName,
                                        PartitionKey = new PartitionKeyDefinition(),

                                    });

            return collection;
        }
    }
}
