using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Benchmark.Models;
using Common.DocDB;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace benchmark
{
    class Program
    {
        private static ILoggerFactory _loggerFactory;

        static void Main(string[] args)
        {
            var services = new ServiceCollection();
            _loggerFactory = new LoggerFactory();
            // ((ILoggerFactory)_loggerFactory).AddConsole();
            services.AddSingleton(_loggerFactory);
            services.AddLogging();

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true);
            var configuration = builder.Build();
            var dbSetting = new CosmosDbSetting()
            {
                AccountName = configuration["DocDb:Name"],
                DbName = configuration["DocDb:Db"],
                CollectionName = configuration["DocDb:Collection"],
                AuthCertThumbprint = configuration["ServicePrincipal:CertificateThumbprint"],
                AuthClientId = configuration["ServicePrincipal:ApplicationId"],
                VaultName = configuration["Vault:Name"],
                DbKeySecret = configuration["Vault:Secrets:DocDbKey"]
            };
            //configuration.GetSection("CosmosDb").Bind(dbSetting);

            Console.WriteLine(JsonConvert.SerializeObject(dbSetting));

            var factory = new DocumentClientFactory();
            IDocumentClient client = factory.GetClient(dbSetting);

            JObject json = JObject.Parse(File.ReadAllText("secrets.json"));
            var accountName = json.Value<string>("account");
            var dbName = json.Value<string>("dbName");
            var authKey = json.Value<string>("authKey");
            foreach(var coll in json.Value<JArray>("collections"))
            {
                var collectionName = coll.Value<string>("name");
                var query = coll.Value<string>("query");
                var modelType = coll.Value<string>("model");
                int total = 0;
                switch(modelType)
                {
                    case nameof(ApplicabilityScope):
                        total = ExportCollection<ApplicabilityScope>(accountName, dbName, collectionName, authKey, query).Result;
                        break;
                    case nameof(Control):
                        total = ExportCollection<Control>(accountName, dbName, collectionName, authKey, query).Result;
                        break;
                    case nameof(NodeMapping):
                        total = ExportCollection<NodeMapping>(accountName, dbName, collectionName, authKey, query).Result;
                        break;
                }
                Console.WriteLine($"Total of {total} documents are exported from collection {collectionName}");
            }

            Console.Read();
        }

        private static async Task<int> ExportCollection<T>(
            string accountName, string dbName, string collectionName, 
            string authKey, string query) where T: class, IDocument , new()
        {
            var factory = new DocumentClientFactory();
            var client = factory.GetClient(accountName, authKey) as DocumentClient;
            var bulkExe = await factory.GetBulkExecutor(accountName, authKey, dbName, collectionName);
            var docDbRepo = new DocDbRepository<T>(client, bulkExe, dbName, collectionName, _loggerFactory.CreateLogger("DocDbRepo"));
            var total = await docDbRepo.BulkExport(query, null, WriteToJsonFile);
            
            return total;
        }

        private static void WriteToJsonFile<T>(IList<T> list) where T: class, IDocument,new()
        {
            var outputFolder = Path.Combine("output", typeof(T).Name);
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }
            var idProp = typeof(T).GetProperty("Id");
            foreach(var item in list)
            {
                var id = idProp.GetValue(item)?.ToString();
                if (!string.IsNullOrEmpty(id))
                {
                    var outputFile = Path.Combine(outputFolder, $"{id}.json");
                    var json = JsonConvert.SerializeObject(item);
                    File.WriteAllText(outputFile, json);
                }
            }
        }
    }
}
