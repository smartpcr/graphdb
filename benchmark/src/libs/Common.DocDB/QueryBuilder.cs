using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Common.DocDB
{
    internal class QueryBuilder<T> where T: class, IDocument, new() 
    {
        private static string[] _partitionKeys;
        private static PropertyInfo _idProp;

        public string[] PartitionKeys => _partitionKeys;

        static QueryBuilder()
        {
            var defaultInstance = Activator.CreateInstance<T>();
            _partitionKeys = defaultInstance.PartitionKeys;
            _idProp = typeof(T).GetProperty("Id");
        }

        public SqlQuerySpec BuildSqlQuery(T entity)
        {
            var sqlParameters = new SqlParameterCollection();
            StringBuilder sbQuery = new StringBuilder();
            sbQuery.Append("SELECT c FROM c WHERE ");
            var id = _idProp.GetValue(entity);
            sqlParameters.Add(new SqlParameter
            {
                Name = "@id",
                Value = id
            });
            sbQuery.Append($"c.id = @id");

            if (_partitionKeys != null && _partitionKeys.Length > 0)
            {
                var allProps = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.CanRead);
                foreach (var partitionKey in _partitionKeys)
                {
                    var prop = allProps.FirstOrDefault(p => p.Name == partitionKey);
                    if (prop == null)
                    {
                        throw new InvalidOperationException($"Unable to find property for partition key {partitionKey}");
                    }
                    var propValue = prop.GetValue(entity);
                    if (propValue == null)
                    {
                        throw new InvalidOperationException($"Property value for partition key {partitionKey} cannot be null");
                    }

                    if (sbQuery.Length > 0)
                    {
                        sbQuery.Append(" and ");
                    }
                    sbQuery.Append($"c.{partitionKey} = @{partitionKey}");
                    sqlParameters.Add(new SqlParameter
                    {
                        Name = "@" + partitionKey,
                        Value = propValue
                    });
                }
            }

            var queryText = sbQuery.ToString();
            return new SqlQuerySpec
            {
                QueryText = queryText,
                Parameters = sqlParameters
            };
        }

        public SqlQuerySpec BuildSqlQuery(string id, params object[] partitionKeyValues)
        {
            StringBuilder sb = new StringBuilder();
            var sqlParameters = new SqlParameterCollection();
            var queryText = $"id = @id";
            sqlParameters.Add(new SqlParameter
            {
                Name = "@id",
                Value = id
            });

            if (_partitionKeys != null && partitionKeyValues != null && _partitionKeys.Length == partitionKeyValues.Length)
            {
                for (int i = 0; i < _partitionKeys.Length; i++)
                {
                    sb.Append($"{_partitionKeys[i]} = @{_partitionKeys[i]} and ");
                    sqlParameters.Add(new SqlParameter
                    {
                        Name = "@" + _partitionKeys[i],
                        Value = partitionKeyValues[i]
                    });
                }
                sb.Append(queryText);
            }

            return new SqlQuerySpec
            {
                QueryText = sb.ToString(),
                Parameters = sqlParameters
            };
        }

        public FeedOptions GetFeedOptions(int batchSize = 100)
        {
            return new FeedOptions() { EnableCrossPartitionQuery = _partitionKeys?.Any() == true, MaxItemCount = batchSize };
        }
    }
}
