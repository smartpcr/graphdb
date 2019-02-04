﻿using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Common.DocDB
{
    public class DocDbRepository<T> : IDocDbRepository<T> where T : class, IDocument, new()
    {
        private static readonly QueryBuilder<T> _queryBuilder = new QueryBuilder<T>();
        private readonly IDocumentClient _client;
        private DocumentCollection _collection;

        public DocDbRepository(IDocumentClient client, string dbId, string collectionId)
        {
            _client = client;
            EnsureDatabaseAndCollection(dbId, collectionId).GetAwaiter().GetResult();
        }

        public async Task<IList<T>> GetAllAsync()
        {
            var list = new List<T>();
            var docQuery = CreateDocumentQuery();
            while (docQuery.HasMoreResults)
            {
                var batch = await docQuery.ExecuteNextAsync<T>();
                list.AddRange(batch);
            }

            return list;
        }

        public async Task<IList<T>> GetAllAsync(int skip, int take)
        {
            var list = new List<T>();
            int totalProcessed = 0;
            var docQuery = CreateDocumentQuery();
            while (docQuery.HasMoreResults)
            {
                var batch = (await docQuery.ExecuteNextAsync<T>()).ToList();
                if (batch.Count + totalProcessed > skip)
                {
                    var start = Math.Max(0, skip - totalProcessed);
                    var length = Math.Min(batch.Count, start + take);
                    list.AddRange(batch.Skip(start).Take(length));
                }

                totalProcessed += batch.Count;
                list.AddRange(batch);
                if (list.Count >= take)
                {
                    break;
                }
            }

            return list;
        }

        public async Task<IList<T>> GetAsync(Expression<Func<T, bool>> predicate)
        {
            var list = new List<T>();
            var docQuery = CreateDocumentQuery(predicate);
            while (docQuery.HasMoreResults)
            {
                var batch = await docQuery.ExecuteNextAsync<T>();
                list.AddRange(batch);
            }

            return list;
        }

        public async Task<IList<T>> GetAsync(string queryText, SqlParameterCollection sqlParameters = null)
        {
            var list = new List<T>();
            var docQuery = CreateDocumentQuery(new SqlQuerySpec(queryText, sqlParameters ?? new SqlParameterCollection()));
            while (docQuery.HasMoreResults)
            {
                var batch = await docQuery.ExecuteNextAsync<T>();
                list.AddRange(batch);
            }

            return list;
        }

        public async Task<T> GetById(string id, params object[] partitionKeyValues)
        {
            var sqlQuery = _queryBuilder.BuildSqlQuery(id, partitionKeyValues);
            var docQuery = CreateDocumentQuery(sqlQuery);
            T found = default(T);
            if (docQuery.HasMoreResults)
            {
                var batch = await docQuery.ExecuteNextAsync<T>();
                found = batch.FirstOrDefault();
            }
            return found;
        }

        public async Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            var docQuery = CreateDocumentQuery(predicate);
            T found = default(T);
            if (docQuery.HasMoreResults)
            {
                var batch = await docQuery.ExecuteNextAsync<T>();
                found = batch.FirstOrDefault();
            }
            return found;
        }

        public async Task<T> FirstOrDefaultAsync(SqlQuerySpec query)
        {
            var docQuery = CreateDocumentQuery(query);
            T found = default(T);
            if (docQuery.HasMoreResults)
            {
                var batch = await docQuery.ExecuteNextAsync<T>();
                found = batch.FirstOrDefault();
            }
            return found;
        }

        public async Task<string> ExecuteStoredProcedureAsync(string storedProcedureName, params dynamic[] parameters)
        {
            var sproc = _client.CreateStoredProcedureQuery(_collection.SelfLink).Where(sp => sp.Id == storedProcedureName).AsEnumerable().First();
            return await _client.ExecuteStoredProcedureAsync<string>(sproc.SelfLink, parameters);
        }

        public async Task<T> UpsertAsync(T entity)
        {
            var query = _queryBuilder.BuildSqlQuery(entity);
            var found = await FirstOrDefaultAsync(query);
            var doc = await _client.UpsertDocumentAsync(_collection.SelfLink, entity);
            return JsonConvert.DeserializeObject<T>(doc.Resource.ToString());
        }

        public async Task<bool> RemoveAsync(T entity)
        {
            if (!string.IsNullOrEmpty(entity.Self))
            {
                var resp = await _client.DeleteDocumentAsync(entity.Self);
                return resp.StatusCode == System.Net.HttpStatusCode.NoContent;
            }
            else
            {
                return false;
            }
        }

        public async Task<int> RemoveAsync(Expression<Func<T, bool>> predicate)
        {
            int removed = 0;
            var itemsFound = await GetAsync(predicate);
            foreach (var selflink in itemsFound.Select(i => i.Self))
            {
                var resp = await _client.DeleteDocumentAsync(selflink);
                if (resp.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    removed++;
                }
                else
                {
                    throw new Exception(resp.StatusCode.ToString());
                }
            }
            return removed;
        }

        public async Task<int> CountAsync()
        {
            var sqlQuery = new SqlQuerySpec
            {
                QueryText = "select VALUE COUNT(1) from c",
                Parameters = new SqlParameterCollection()
            };
            var query = CreateDocumentQuery(sqlQuery);
            int count = 0;
            while (query.HasMoreResults)
            {
                count += (await query.ExecuteNextAsync<int>()).First();
            }
            return count;
        }

        public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
        {
            var query = CreateDocumentQuery(predicate);
            int count = 0;
            while (query.HasMoreResults)
            {
                var batch = await query.ExecuteNextAsync<T>();
                count += batch.Count;
            }
            return count;
        }

        public Task<int> BulkImport(IEnumerable<T> entities)
        {
            // use variant of writeGraph procedure
            throw new NotImplementedException();
        }

        public async Task<int> BulkExport(string queryText, SqlParameterCollection sqlParameters = null, Action<IList<T>> exportRecordsAction = null)
        {
            var sqlQuery = new SqlQuerySpec(queryText, sqlParameters ?? new SqlParameterCollection());
            var result = CreateDocumentQuery(sqlQuery);
            int total = 0;
            while (result.HasMoreResults)
            {
                var batch = await result.ExecuteNextAsync<T>();
                total += batch.Count;
                exportRecordsAction?.Invoke(batch.ToList());
            }
            return total;
        }

        private async Task EnsureDatabaseAndCollection(string dbId, string collectionId)
        {
            var db = _client.CreateDatabaseQuery().Where(d => d.Id == dbId).AsEnumerable().FirstOrDefault()
                ?? await _client.CreateDatabaseAsync(new Database() { Id = dbId });

            _collection = _client.CreateDocumentCollectionQuery(db.SelfLink)
                .Where(c => c.Id == collectionId).AsEnumerable().FirstOrDefault();
            if (_collection == null)
            {
                var collectionDefinition = new DocumentCollection() { Id = collectionId };
                if (_queryBuilder.PartitionKeys != null && _queryBuilder.PartitionKeys.Length > 0)
                {
                    foreach (var partitionKey in _queryBuilder.PartitionKeys)
                    {
                        collectionDefinition.PartitionKey.Paths.Add(partitionKey);
                    }
                }

                _collection = await _client.CreateDocumentCollectionAsync(db.SelfLink, collectionDefinition);
            }
        }

        private IDocumentQuery<T> CreateDocumentQuery()
        {
            return _client.CreateDocumentQuery<T>(_collection.SelfLink, _queryBuilder.GetFeedOptions()).AsDocumentQuery<T>();
        }

        private IDocumentQuery<T> CreateDocumentQuery(Expression<Func<T, bool>> predicate)
        {
            var filteredQuery = _client.CreateDocumentQuery<T>(_collection.SelfLink, _queryBuilder.GetFeedOptions()).Where(predicate);
            return filteredQuery.AsDocumentQuery<T>();
        }

        private IDocumentQuery<T> CreateDocumentQuery(SqlQuerySpec querySpec)
        {
            var filteredQuery = _client.CreateDocumentQuery<T>(
                _collection.SelfLink,
                querySpec,
                _queryBuilder.GetFeedOptions());
            return filteredQuery.AsDocumentQuery<T>();
        }

    }
}
