using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jobsistem
{
    public class JobResult
    {
        public Job Job { get; init; }
        public bool Success { get; init; }
        public long Duration { get; init; }

        public JobResult(Job job, bool success, long duration)
        {
            Job = job;
            Success = success;
            Duration = duration;
        }
    }
}
