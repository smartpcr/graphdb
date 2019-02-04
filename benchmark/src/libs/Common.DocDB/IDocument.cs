using Newtonsoft.Json;

namespace Common.DocDB
{
    public interface IDocument
    {
        [JsonProperty("id")]
        string Id { get; }

        [JsonIgnore]
        string[] PartitionKeys { get; }

        [JsonProperty("_etag")]
        string ETag { get; set; }

        [JsonProperty("_self")]
        string Self { get; }

        [JsonProperty("_rid")]
        string ResourceId { get; }
    }
}
