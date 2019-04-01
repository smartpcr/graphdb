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
                    ConfigureCosmosDB(services, hostBuilderContext.Configuration);
                    
                    ConfigureServices(services, hostBuilderContext.HostingEnvironment, hostBuilderContext.Configuration);
                    services.TryAddSingleton<App>();
                });

            using (var host = hostBuilder.Build())
            {
                var app = host.Services.GetRequiredService<App>();
                app.Run().GetAwaiter().GetResult();
            }
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
                DocDbAuthKeySecretName = configuration["Vault:Secrets:DocDbAuthKey"],
                GraphDbAuthKeySecretName = configuration["Vault:Secrets:GraphDbAuthKey"],
            };
            services.TryAddSingleton(kvSetting);
        }

        private static void ConfigureCosmosDB(IServiceCollection services, IConfiguration configuration)
        {
            services.AddTransient<Func<string, CosmosDbSetting>>(serviceProvider => name =>
            {
                var kvSetting = serviceProvider.GetRequiredService<KeyVaultSetting>();
                var kvCert = CertUtil.FindCertificateByThumbprint(kvSetting.AuthCertThumbprint);
                var kvClient = new KeyVaultUtil(kvSetting.VaultName, kvSetting.AuthClientId, kvCert);

                switch (name)
                {
                    case "graph":
                        var graphDbKey = kvClient.GetSecret(kvSetting.GraphDbAuthKeySecretName).Result;
                        var graphDbSetting = new CosmosDbSetting()
                        {
                            AccountName = configuration["graphDb:accountName"],
                            DbName = configuration["graphDb:dbName"],
                            CollectionName = configuration["graphDb:collectionName"],
                            AuthKey = graphDbKey
                        };
                        return graphDbSetting;
                    default:
                        var docDbKey = kvClient.GetSecret(kvSetting.DocDbAuthKeySecretName).Result;
                        var docDbSetting = new CosmosDbSetting()
                        {
                            AccountName = configuration["docDb:accountName"],
                            DbName = configuration["docDb:dbName"],
                            CollectionName = configuration["docDb:collectionName"],
                            AuthKey = docDbKey
                        };
                        return docDbSetting;
                }
            });
        }

        private static void ConfigureApp(IServiceCollection services)
        {
            
        }

    }
}
