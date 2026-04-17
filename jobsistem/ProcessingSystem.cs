using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace jobsistem
{
    public class ProcessingSystem
    {
        private readonly PriorityQueue<Job, int> _queue = new();
        private readonly int _maxQueueSize;
        private readonly SemaphoreSlim _signal = new(0);
        private readonly ConcurrentDictionary<Guid, bool> _processed = new();
        private readonly ConcurrentDictionary<Guid, JobEntry> _jobs = new();
        private readonly ConcurrentBag<JobResult> _history = new();
        private readonly object _lock = new();

        public event Action<Job, int>? JobCompleted;
        public event Action<Job>? JobFailed;
        public event Action<Job>? JobAborted;

        public ProcessingSystem(int workerCount, int maxQueueSize)
        {
            _maxQueueSize = maxQueueSize;
            for (int i = 0; i < workerCount; i++)
                Task.Run(() => WorkerLoop());

            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(60_000);
                    GenerateReport();
                }
            });
        }

        public JobHandle Submit(Job job)
        {
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_lock)
            {
                if (_queue.Count >= _maxQueueSize)
                    throw new InvalidOperationException("Queue is full");

                if (_processed.ContainsKey(job.Id))
                    throw new InvalidOperationException($"Duplicate job: {job.Id}");

                _queue.Enqueue(job, job.Priority);
            }
            _jobs[job.Id] = new JobEntry(job, tcs);
            _signal.Release();

            return new JobHandle { Id = job.Id, Result = tcs.Task };
        }

        private async Task WorkerLoop()
        {
            while (true)
            {
                await _signal.WaitAsync();

                Job job;
                lock (_lock)
                {
                    if (_queue.Count == 0) continue;
                    job = _queue.Dequeue();
                }
                await ProcessJob(job);
            }
        }

        private async Task ProcessJob(Job job)
        {
            if (!_processed.TryAdd(job.Id, true))
            {
                if (_jobs.TryRemove(job.Id, out var dup))
                    dup.Tcs.TrySetCanceled();
                return;
            }

            var sw = Stopwatch.StartNew();
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    var result = await RunWithTimeout(job);

                    sw.Stop();
                    _history.Add(new JobResult(job, true, sw.ElapsedMilliseconds));
                    if (_jobs.TryRemove(job.Id, out var entry))
                        entry.Tcs.TrySetResult(result);

                    JobCompleted?.Invoke(job, result);
                    return;
                }
                catch when (attempt < 3)
                {
                    JobFailed?.Invoke(job);
                }
                catch
                {
                    sw.Stop();
                    _history.Add(new JobResult(job, false, sw.ElapsedMilliseconds));

                    if (_jobs.TryRemove(job.Id, out var entry))
                        entry.Tcs.TrySetException(new Exception("Job aborted after 3 failures"));

                    JobAborted?.Invoke(job);
                }
            }
        }

        private static async Task<int> RunWithTimeout(Job job)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var jobTask = JobRunner.Run(job);
            var delay = Task.Delay(Timeout.Infinite, cts.Token);

            var completed = await Task.WhenAny(jobTask, delay);
            if (completed != jobTask)
                throw new TimeoutException($"Job {job.Id} timed out");

            return await jobTask;
        }

        public IEnumerable<Job> GetTopJobs(int n)
        {
            lock (_lock)
            {
                return _queue.UnorderedItems
                    .OrderBy(x => x.Priority)
                    .Take(n)
                    .Select(x => x.Element)
                    .ToList();
            }
        }

        public Job? GetJob(Guid id) =>
            _jobs.TryGetValue(id, out var entry) ? entry.Job : null;

        // Reports
        public void GenerateReport()
        {
            var report = _history
                .GroupBy(x => x.Job.Type)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Type = g.Key,
                    Count = g.Count(x => x.Success),
                    AvgTime = g.Where(x => x.Success)
                               .Select(x => (double)x.Duration)
                               .DefaultIfEmpty(0)
                               .Average(),
                    Failed = g.Count(x => !x.Success)
                });

            var doc = new XDocument(
                new XElement("Report",
                    new XAttribute("GeneratedAt", DateTime.Now.ToString("o")),
                    report.Select(r =>
                        new XElement("JobType",
                            new XAttribute("Type", r.Type),
                            new XElement("Count", r.Count),
                            new XElement("AvgTime", Math.Round(r.AvgTime, 2)),
                            new XElement("Failed", r.Failed)))));

            doc.Save($"report_{DateTime.Now:HH_mm_ss}.xml");
            ManageReports();
        }

        private static void ManageReports()
        {
            var files = Directory.GetFiles(".", "report_*.xml")
                                 .OrderBy(f => f)
                                 .ToList();

            while (files.Count > 10)
            {
                File.Delete(files[0]);
                files.RemoveAt(0);
            }
        }
    }
}
