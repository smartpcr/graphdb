using System;
using System.IO;
using Common.DocDB;
using Microsoft.Azure.Documents;
using Microsoft.Extensions.Configuration;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
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

            Console.Read();
        }
    }
}
