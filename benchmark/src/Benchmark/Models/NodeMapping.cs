using System;
using Newtonsoft.Json;

namespace Benchmark.Models
{
    public class NodeMapping : BaseDocument
    {
        [JsonProperty("artifactType")]
        public string ArtifactType { get; set; }

        [JsonProperty("artifactId")]
        public string ArtifactId { get; set; }

        [JsonProperty("nodeId")]
        public string NodeId { get; set; }

        [JsonProperty("createdWhen")]
        public long CreatedWhen { get; set; }

        [JsonProperty("createdBy")]
        public Guid CreatedBy { get; set; }

        [JsonProperty("modifiedWhen")]
        public long ModifiedWhen { get; set; }

        [JsonProperty("modifiedBy")]
        public Guid ModifiedBy { get; set; }
    }
}
