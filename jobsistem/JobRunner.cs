using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jobsistem
{
    public static class JobRunner
    {
        private static readonly Random _random = new();

        public static Task<int> Run(Job job) => job.Type switch
        {
            JobType.Prime => RunPrime(job.Payload),
            JobType.IO => RunIO(job.Payload),
            _ => Task.FromResult(0)
        };

        // Prime
        private static async Task<int> RunPrime(string payload)
        {
            var (limit, threadCount) = ParsePrimePayload(payload);
            threadCount = Math.Clamp(threadCount, 1, 8);
            int range = (limit - 2 + 1) / threadCount;
            var tasks = new List<Task<int>>(threadCount);

            for (int t = 0; t < threadCount; t++)
            {
                int from = 2 + t * range;
                int to = (t == threadCount - 1) ? limit : from + range - 1;

                tasks.Add(Task.Run(() => CountPrimesInRange(from, to)));
            }

            var results = await Task.WhenAll(tasks);
            return results.Sum();
        }

        private static int CountPrimesInRange(int from, int to)
        {
            int count = 0;
            for (int n = from; n <= to; n++)
                if (IsPrime(n)) count++;
            return count;
        }

        private static bool IsPrime(int n)
        {
            if (n < 2) return false;
            if (n == 2) return true;
            if (n % 2 == 0) return false;
            for (int i = 3; i * i <= n; i += 2)
                if (n % i == 0) return false;
            return true;
        }

        private static (int limit, int threads) ParsePrimePayload(string payload)
        {
            // example: numbers:10_000,threads:3
            int limit = 10_000, threads = 1;
            foreach (var part in payload.Split(','))
            {
                var kv = part.Trim().Split(':');
                if (kv.Length != 2) continue;
                int value = int.Parse(kv[1].Replace("_", ""));
                switch (kv[0].Trim().ToLower())
                {
                    case "numbers": limit = value; break;
                    case "threads": threads = value; break;
                }
            }
            return (limit, threads);
        }

        // IO
        private static async Task<int> RunIO(string payload)
        {
            int delay = ParseIOPayload(payload);
            await Task.Delay(delay);
            return _random.Next(0, 101);
        }

        private static int ParseIOPayload(string payload)
        {
            // example: delay:1_000
            foreach (var part in payload.Split(','))
            {
                var kv = part.Trim().Split(':');
                if (kv.Length == 2 && kv[0].Trim().ToLower() == "delay")
                    return int.Parse(kv[1].Replace("_", ""));
            }
            return 0;
        }
    }
}
