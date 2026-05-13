using System;
using System.Threading;
using System.Threading.Tasks;

namespace AccSaber.API
{
    internal class Throttler(int callsPerCycle, int cycleLength) //Straight from here: https://github.com/IMightBeeAPerson/BLPPCounter/blob/master/BLPPCounter/Utils/API%20Handlers/Throttler.cs
    {
        public int CallsPerCycle { get; private set; } = callsPerCycle;
        public int CycleLength { get; private set; } = cycleLength;

        private DateTime CycleStartTime = DateTime.UtcNow;
        private int CallsThisCycle = 0;
        private readonly object locker = new();

        public async Task Call()
        {
            int restTime = 0;
            lock (locker)
            {
                TimeSpan diff = DateTime.UtcNow - CycleStartTime;

                if (diff.TotalSeconds >= CycleLength)
                {
                    CallsThisCycle = 0;
                    CycleStartTime = DateTime.UtcNow;
                }

                CallsThisCycle++;

                if (CallsThisCycle > CallsPerCycle)
                {
                    restTime = (int)(CycleLength * 1000 - diff.TotalMilliseconds);
                    Thread.Sleep(restTime);
                    CallsThisCycle = 1;
                    CycleStartTime = DateTime.UtcNow.AddMilliseconds(restTime);
                }
            }
            if (restTime > 0)
            {
                Plugin.Log.Info("Throttling calls for " + restTime + "ms.");
                await Task.Delay(restTime);
            }
        }
    }
}
