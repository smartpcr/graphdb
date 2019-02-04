using Microsoft.Azure.Documents;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Common.DocDB
{
    public interface IDocDbRepository<T> where T : class, IDocument, new()
    {
        Task<IList<T>> GetAllAsync();
        Task<IList<T>> GetAllAsync(int skip, int take);
        Task<IList<T>> GetAsync(Expression<Func<T, bool>> predicate);
        Task<IList<T>> GetAsync(string queryText, SqlParameterCollection sqlParameters = null);
        Task<T> GetById(string id, params object[] partitionKeyValues);
        Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
        Task<T> FirstOrDefaultAsync(SqlQuerySpec query);
        Task<string> ExecuteStoredProcedureAsync(string storedProcedureName, params object[] parameters);


        Task<T> UpsertAsync(T entity);
        Task<bool> RemoveAsync(T entity);
        Task<int> RemoveAsync(Expression<Func<T, bool>> predicate);

        #region count 
        Task<int> CountAsync();
        Task<int> CountAsync(Expression<Func<T, bool>> predicate);
        #endregion 

        #region bulk
        Task<int> BulkImport(IEnumerable<T> entities);

        Task<int> BulkExport(string queryText, SqlParameterCollection sqlParameters = null,
            Action<IList<T>> exportRecordsAction = null);
        #endregion
    }
}
