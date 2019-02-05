using System;
using System.Collections.Generic;
using System.Text;

namespace Benchmark.Models
{
    public class NodeMapping : BaseDocument
    {
        public string ArtifactType { get; set; }
        public string ArtifactId { get; set; }
        public string NodeId { get; set; }
        public long CreatedWhen { get; set; }
        public Guid CreatedBy { get; set; }
        public long ModifiedWhen { get; set; }
        public Guid ModifiedBy { get; set; }
    }
}
