using System;
using System.Collections.Generic;
using System.Text;

namespace Benchmark
{
    public class CosmosDbSetting
    {
        public string AccountName { get; set; }
        public string DbName { get; set; }
        public string CollectionName { get; set; }
        public string AuthClientId { get; set; }
        public string AuthCertThumbprint { get; set; }
    }
}
