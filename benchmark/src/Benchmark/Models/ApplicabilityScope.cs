using Newtonsoft.Json.Linq;

namespace Benchmark.Models
{
    public class ApplicabilityScope : BaseDocument
    {
        public string Scope { get; set; }
        public string ControlId { get; set; }
        public JObject Filter { get; set; }
    }
}
