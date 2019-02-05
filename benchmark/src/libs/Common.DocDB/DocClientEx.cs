using Microsoft.Azure.Documents;
using System.Linq;
using System.Threading.Tasks;

namespace Common.DocDB
{
    internal static class DocClientEx
    {
        public static async Task<DocumentCollection> EnsureDatabaseAndCollection(this IDocumentClient client, string dbId, string collectionId, string[] partitionKeys = null)
        {
            var db = client.CreateDatabaseQuery().Where(d => d.Id == dbId).AsEnumerable().FirstOrDefault()
                ?? await client.CreateDatabaseAsync(new Database() { Id = dbId });

            var collection = client.CreateDocumentCollectionQuery(db.SelfLink)
                .Where(c => c.Id == collectionId).AsEnumerable().FirstOrDefault();
            if (collection == null)
            {
                var collectionDefinition = new DocumentCollection() { Id = collectionId };
                if (partitionKeys != null && partitionKeys.Length > 0)
                {
                    foreach (var partitionKey in partitionKeys)
                    {
                        collectionDefinition.PartitionKey.Paths.Add(partitionKey);
                    }
                }

                collection = await client.CreateDocumentCollectionAsync(db.SelfLink, collectionDefinition);
            }

            return collection;
        }
    }
}
