using System;
using System.Collections.Generic;

namespace AquaExpansion.Core
{
    public class BioLatentScheduler
    {
        private class LatentEntry
        {
            public string Key;
            public Action Action;
            public int Counter;
            public int Interval;
            public bool Repeat;
            public LatentEntry(
                Action action,
                int ticks,
                bool repeat,
                int interval)
            {
                Key = null;
                Action = action;
                Counter = ticks;
                Repeat = repeat;
                Interval = interval;
            }

            public LatentEntry(
                string key,
                Action action,
                int ticks,
                bool repeat,
                int interval)
            {
                Key = key;
                Action = action;
                Counter = ticks;
                Repeat = repeat;
                Interval = interval;
            }
        }
        private readonly List<LatentEntry> latentActions = new List<LatentEntry>();
        /// <summary>
        /// Shedule a one-off/repeat action after N ticks direct signature
        /// </summary>
        /// <param name="action"></param>
        /// <param name="ticks"></param>
        /// <param name="repeat"></param>
        /// <param name="interval"></param>
        public void Schedule(Action action,int ticks,bool repeat,int interval)
        {
            if (action == null || ticks < 0)
                return;
            if (repeat && interval <= 0)
                return;
            for (int i = 0; i < latentActions.Count; i++)
            {
                LatentEntry entry = latentActions[i];
                if (entry.Key == null &&
                    entry.Action == action)
                    return;
            }
            latentActions.Add(new LatentEntry(action,ticks,repeat,interval));
        }
        /// <summary>
        /// Shedule a one-off/repeat action after N ticks lambdakeyed signature
        /// </summary>
        /// <param name="key"></param>
        /// <param name="action"></param>
        /// <param name="ticks"></param>
        /// <param name="repeat"></param>
        /// <param name="interval"></param>
        public void Schedule(string key,Action action,int ticks,bool repeat,int interval)
        {
            if (action == null || ticks < 0)
                return;
            if (repeat && interval <= 0)
                return;
            if (string.IsNullOrEmpty(key))
            {
                Schedule(action,ticks,repeat,interval);
                return;
            }
            for (int i = 0; i < latentActions.Count; i++)
            {
                LatentEntry entry = latentActions[i];
                if (entry.Key == key)
                    return;
            }
            latentActions.Add(new LatentEntry(key,action,ticks,repeat,interval));
        }
        /// <summary>
        /// Update
        /// </summary>
        public void Update()
        {
            for (int i = latentActions.Count - 1; i >= 0; i--)
            {
                LatentEntry entry = latentActions[i];
                entry.Counter--;
                if (entry.Counter > 0)
                    continue;
                if (!entry.Repeat)
                {
                    latentActions.RemoveAt(i);
                    entry.Action.Invoke();
                    continue;
                }
                entry.Counter += entry.Interval;
                entry.Action.Invoke();
            }
        }
        /// <summary>
        /// Cancel sheduled action direct signature
        /// </summary>
        /// <param name="action"></param>
        public void Cancel(Action action)
        {
            if (action == null)
                return;
            latentActions.RemoveAll(
                delegate (LatentEntry entry)
                {
                    return entry.Action == action;
                });
        }
        /// <summary>
        /// Cancel sheduled action lambdakeyed
        /// </summary>
        /// <param name="key"></param>
        public void Cancel(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;
            latentActions.RemoveAll(
                delegate (LatentEntry entry)
                {
                    return entry.Key == key;
                });
        }
        /// <summary>
        /// Clear All
        /// </summary>
        public void Clear()
        {
            latentActions.Clear();
        }
    }
}
