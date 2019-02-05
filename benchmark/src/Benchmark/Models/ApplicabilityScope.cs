using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Benchmark.Models
{
    public class ApplicabilityScope : BaseDocument
    {
        [JsonProperty("scope")]
        public string Scope { get; set; }

        [JsonProperty("controlId")]
        public string ControlId { get; set; }

        [JsonProperty("filter")]
        public JObject Filter { get; set; }
    }
}
