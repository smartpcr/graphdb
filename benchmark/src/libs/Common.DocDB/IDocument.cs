using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using Newtonsoft.Json;

namespace Benchmark
{
    public interface IDocument
    {
        [JsonProperty("_id")]
        string Id { get; }

        [JsonIgnore, NotMapped]
        string[] PartitionKeys { get; }
    }
}
