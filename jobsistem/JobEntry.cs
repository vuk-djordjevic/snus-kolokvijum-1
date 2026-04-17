using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jobsistem
{
    public class JobEntry
    {
        public Job Job { get; set; }
        public TaskCompletionSource<int> Tcs { get; set; }
        public JobEntry(Job job, TaskCompletionSource<int> tcs)
        {
            Job = job;
            Tcs = tcs;
        }
    }
}
