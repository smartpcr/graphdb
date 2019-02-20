using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Benchmark.Models;
using Common.DocDB;
using Common.KeyVault;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace benchmark
{
    static class Program
    {
        static void Main(string[] args)
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var isProd = !string.IsNullOrEmpty(env) && env.Equals("Production", StringComparison.OrdinalIgnoreCase);
            var hostBuilder = new HostBuilder()
                .UseEnvironment(isProd?"Production": env)
                .ConfigureAppConfiguration(configBuilder =>
                    {
                        configBuilder.AddJsonFile("appsettings.json", false, false);
                        if (!isProd)
                        {
                            var overrides = env?.Split(".", StringSplitOptions.RemoveEmptyEntries);
                            if (overrides != null)
                            {
                                foreach (var envOverride in overrides)
                                {
                                    configBuilder.AddJsonFile($"appsettings.{envOverride}.json", false, false);
                                }
                            }
                        }

                        configBuilder.AddEnvironmentVariables();
                    })
                .ConfigureServices((hostBuilderContext, services) =>
                {
                    ConfigKeyVault(services, hostBuilderContext.Configuration);
                    
                    ConfigureServices(services, hostBuilderContext.HostingEnvironment, hostBuilderContext.Configuration);
                    services.TryAddSingleton<App>();
                });

            using (var host = hostBuilder.Build())
            {
                var app = host.Services.GetRequiredService<App>();
                app.Run().GetAwaiter().GetResult();
            }
            
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
                int totalexported = 0;
                switch(modelType)
                {
                    case nameof(ApplicabilityScope):
                        totalexported = ExportCollection<ApplicabilityScope>(accountName, dbName, collectionName, authKey, query).Result;
                        break;
                    case nameof(Control):
                        totalexported = ExportCollection<Control>(accountName, dbName, collectionName, authKey, query).Result;
                        break;
                    case nameof(NodeMapping):
                        totalexported = ExportCollection<NodeMapping>(accountName, dbName, collectionName, authKey, query).Result;
                        break;
                }
                Console.WriteLine($"Total of {totalexported} documents are exported from collection {collectionName}");

                int totalImported = 0;
                switch (modelType)
                {
                    case nameof(ApplicabilityScope):
                        totalImported = ImportDocuments<ApplicabilityScope>(dbSetting).Result;
                        break;
                    case nameof(Control):
                        totalImported = ImportDocuments<Control>(dbSetting).Result;
                        break;
                    case nameof(NodeMapping):
                        totalImported = ImportDocuments<NodeMapping>(dbSetting).Result;
                        break;
                }
                Console.WriteLine($"Total of {totalImported} {modelType} documents are imported to collection {dbSetting.CollectionName}");
            }

            Console.WriteLine("\nDone!\n");
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
            var outputFolder = GetOutputFolder<T>();
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

        private static async Task<int> ImportDocuments<T>(CosmosDbSetting setting) where T : class, IDocument, new()
        {
            var outputFolder = GetOutputFolder<T>();
            var jsonFiles = Directory.GetFiles(outputFolder, "*.json");
            var items = new List<T>();
            foreach (var jsonFile in jsonFiles)
            {
                items.Add(JsonConvert.DeserializeObject<T>(File.ReadAllText(jsonFile)));
            }

            var factory = new DocumentClientFactory();
            var docClient = factory.GetClient(setting) as DocumentClient;
            var bulkExe = await factory.GetBulkExecutor(setting);
            var repo = new DocDbRepository<T>(docClient, bulkExe, setting.DbName, setting.CollectionName, _loggerFactory.CreateLogger("DocDbRepo"));
            int total = await repo.BulkImport(items);
            return total;
        }

        private static string GetOutputFolder<T>() where T : class, IDocument, new()
        {
            var outputFolder = Path.Combine("output", typeof(T).Name);
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }
d
            return outputFolder;
        }

        private static void ConfigureServices(IServiceCollection services, IHostingEnvironment env, IConfiguration config)
        {
            services.TryAddSingleton(config);
            
            ConfigureApp(services);
        }

        private static void ConfigKeyVault(IServiceCollection services, IConfiguration configuration)
        {
            var kvSetting = new KeyVaultSetting
            {
                AuthCertThumbprint = configuration["ServicePrincipal:CertificateThumbprint"],
                AuthClientId = configuration["ServicePrincipal:ApplicationId"],
                VaultName = configuration["Vault:Name"],
            };
            services.TryAddSingleton(kvSetting);
        }
        
        

        private static void ConfigureApp(IServiceCollection services)
        {
            
        }
    }
}
