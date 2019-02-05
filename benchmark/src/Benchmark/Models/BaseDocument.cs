using Common.DocDB;
using Newtonsoft.Json;

namespace Benchmark.Models
{
    public class BaseDocument : IDocument
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonIgnore]
        public string[] PartitionKeys => new string[] { };

        [JsonProperty("_etag")]
        public string ETag { get; set; }

        [JsonProperty("_self")]
        public string Self { get; set; }

        [JsonProperty("_rid")]
        public string ResourceId { get; set; }

        [JsonProperty("documentType")]
        public string DocumentType { get; set; }
    }
}
