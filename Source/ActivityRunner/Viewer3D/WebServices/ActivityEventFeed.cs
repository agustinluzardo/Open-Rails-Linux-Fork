using System;
using System.Collections.Generic;
using System.Linq;

namespace Orts.ActivityRunner.Viewer3D.WebServices
{
    internal sealed class ActivityEventFeed
    {
        internal sealed record Entry(long Sequence, DateTime TimestampUtc, string Header, string Text);

        private readonly object sync = new object();
        private readonly List<Entry> entries = new List<Entry>();
        private long sequence;

        public void Add(string header, string text)
        {
            if (string.IsNullOrWhiteSpace(header) && string.IsNullOrWhiteSpace(text))
                return;

            lock (sync)
            {
                entries.Add(new Entry(++sequence, DateTime.UtcNow, header ?? string.Empty, text ?? string.Empty));
                if (entries.Count > 200)
                    entries.RemoveRange(0, entries.Count - 200);
            }
        }

        public Entry[] Snapshot()
        {
            lock (sync)
                return entries.ToArray();
        }
    }
}
