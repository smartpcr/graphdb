using Common.DocDB;
using Newtonsoft.Json;

namespace Benchmark.Models
{
    public class BaseDocument : IDocument
    {
        public string Id { get; set; }

        [JsonIgnore]
        public string[] PartitionKeys
        {
            get
            {
                return new string[] { };
            }
        }

        [JsonProperty("_etag")]
        public string ETag { get; set; }

        [JsonProperty("_self")]
        public string Self { get; set; }

        [JsonProperty("_rid")]
        public string ResourceId { get; set; }

        public string DocumentType { get; set; }
    }
}
