using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Benchmark.Models
{
    public class Control : BaseDocument
    {
        public string Type { get; set; }
        public JObject Configuration { get; set; }
    }
}
