using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using jobsistem;

var config = ConfigReader.Read("SystemConfig.xml");
var system = new ProcessingSystem(config.WorkerCount, config.MaxQueueSize);

var logLock = new object();
void LogToFile(string line)
{
    lock (logLock)
        File.AppendAllText("log.txt", line + "\n");
}
system.JobCompleted += (job, result) =>
    Task.Run(() => LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [COMPLETED] {job.Id}, {result}"));
system.JobFailed += job =>
    Task.Run(() => LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [FAILED] {job.Id}, -"));
system.JobAborted += job =>
    Task.Run(() => LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ABORT] {job.Id}, -"));

// Initial Jobs
foreach (var job in config.InitialJobs)
{
    try
    {
        var handle = system.Submit(job);
        Console.WriteLine($"Submitted initial job {job.Id} ({job.Type}, priority {job.Priority})");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[INIT] Submit failed: {ex.Message}");
    }
}

// Producers
for (int i = 0; i < config.ProducerCount; i++)
{
    int producerId = i;
    _ = Task.Run(async () =>
    {
        var localRng = new Random(producerId * 1000 + Environment.TickCount);
        while (true)
        {
            try
            {
                var job = CreateRandomJob(localRng);
                var handle = system.Submit(job);
                Console.WriteLine($"[Producer {producerId}] Submitted {job.Id} ({job.Type}, priority {job.Priority})");

                // Clinet simulation
                _ = handle.Result.ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully)
                        Console.WriteLine($"[Producer {producerId}] Job {handle.Id} result: {t.Result}");
                    else
                        Console.WriteLine($"[Producer {producerId}] Job {handle.Id} failed/aborted.");
                });
            }
            catch (InvalidOperationException ex)
            {
                // Queue full or idempotency check
                Console.WriteLine($"[Producer {producerId}] {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Producer {producerId}] Unexpected error: {ex.Message}");
            }
            await Task.Delay(localRng.Next(200, 800));
        }
    });
}


Console.WriteLine("System running. Press Enter to exit.");
Console.ReadLine();

// Demo for GetTopJobs and GetJob
var topJobs = system.GetTopJobs(3);
Console.WriteLine("\n--- Top 3 jobs in queue ---");
foreach (var j in topJobs)
    Console.WriteLine($"  {j.Id} ({j.Type}, priority {j.Priority})");

var firstInitial = config.InitialJobs.FirstOrDefault();
if (firstInitial != null)
{
    var found = system.GetJob(firstInitial.Id);
    Console.WriteLine($"\n--- GetJob({firstInitial.Id}) ---");
    Console.WriteLine(found != null ? $"  First initial job found: {found.Type}, priority {found.Priority}" : "  First initial job not found");
}

Console.WriteLine("Create job and try to GetJob function");
var job2 = CreateRandomJob(new Random());
var handle2 = system.Submit(job2);
var found2 = system.GetJob(job2.Id);
Console.WriteLine(found2 != null ? $"Job found: {found2.Type}" : "Job not found");

// Final report
system.GenerateReport();
Console.WriteLine("Final report generated.");


static Job CreateRandomJob(Random r)
{
    var type = r.Next(2) == 0 ? JobType.Prime : JobType.IO;
    string payload = type == JobType.Prime
        ? $"numbers:{r.Next(1000, 50000)},threads:{r.Next(1, 9)}"
        : $"delay:{r.Next(100, 3000)}";

    return new Job
    {
        Id = Guid.NewGuid(),
        Type = type,
        Payload = payload,
        Priority = r.Next(1, 11)
    };
}
