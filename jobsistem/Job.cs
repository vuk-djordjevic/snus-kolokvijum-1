using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jobsistem
{
    public enum JobType
    {
        Prime,
        IO
    }

    public class Job
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public JobType Type { get; set; }
        public string Payload { get; set; }
        public int Priority { get; set; }
    }
}
