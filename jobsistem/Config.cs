using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace jobsistem
{
    public class SystemConfig
    {
        public int WorkerCount { get; set; } = 5;
        public int MaxQueueSize { get; set; } = 100;
        public int ProducerCount { get; set; } = 3;
        public List<Job> InitialJobs { get; set; } = new();
    }

    public static class ConfigReader
    {
        public static SystemConfig Read(string path)
        {
            var doc = XDocument.Load(path);
            var root = doc.Root!;

            var config = new SystemConfig
            {
                WorkerCount = int.Parse(root.Element("WorkerCount")!.Value),
                MaxQueueSize = int.Parse(root.Element("MaxQueueSize")!.Value),
            };

            // Optional ProducerCount
            var pc = root.Element("ProducerCount");
            if (pc != null) config.ProducerCount = int.Parse(pc.Value);

            var jobs = root.Element("Jobs")?.Elements("Job");
            if (jobs != null)
            {
                foreach (var j in jobs)
                {
                    config.InitialJobs.Add(new Job
                    {
                        Id = j.Attribute("Id") != null
                                       ? Guid.Parse(j.Attribute("Id")!.Value)
                                       : Guid.NewGuid(),
                        Type = Enum.Parse<JobType>(j.Attribute("Type")!.Value),
                        Payload = j.Attribute("Payload")!.Value,
                        Priority = int.Parse(j.Attribute("Priority")!.Value)
                    });
                }
            }

            return config;
        }
    }
}
