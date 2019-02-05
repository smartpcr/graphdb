using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Benchmark.Models
{
    public class Control : BaseDocument
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("configuration")]
        public JObject Configuration { get; set; }
    }
}
