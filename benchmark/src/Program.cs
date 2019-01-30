using System;
using System.IO;
using Benchmark;
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
            var dbSetting = new CosmosDbSetting();
            configuration.GetSection("CosmosDb").Bind(dbSetting);

            Console.WriteLine(JsonConvert.SerializeObject(dbSetting));
        }
    }
}
