using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ImageGlass.Common{
    sealed class TrackInfo{
        public string Name = string.Empty;
        public Stopwatch Watch = new Stopwatch();
        public int BeginThreadId;
    }
    public static class DebugHelper{
        static readonly TrackInfo[] tracker;
        static int trackIndex;
        static DebugHelper(){
            tracker = Enumerable.Range(1, 10).Select(_ => new TrackInfo()).ToArray();
        }

        sealed class Logger : IDisposable {
            readonly Stopwatch watch;
            readonly Action<long> logger;
            public Logger(Action<long> logger){
                watch = Stopwatch.StartNew();
                this.logger = logger;
            }
            public void Dispose() => logger(watch.ElapsedMilliseconds);
        }
        public static IDisposable Time(Action<long> logger) => new Logger(logger);
        public static void Track(string title){
            if (trackIndex > 0)
                EndTrack(trackIndex -1);
            BeginTrack(trackIndex++, title);
        }
        public static void BeginTrack(int n, string title){
            var track = tracker[n];
            if (!track.Watch.IsRunning){
                track.BeginThreadId = Thread.CurrentThread.ManagedThreadId;
                track.Name = title;
                track.Watch.Reset();
                track.Watch.Start();
            }
        }
        public static void EndTrack(int n){
            var track = tracker[n];
            if (track.Watch.IsRunning){
                track.Watch.Stop();
                var id = Thread.CurrentThread.ManagedThreadId;
                Task.Run(() => Debug.Print($"{n}: [{track.BeginThreadId}/{id}] `{track.Name}` elasped {track.Watch.ElapsedMilliseconds} ms."));
            }
        }
        public static Action<long> LogIf(long threshold, string message) => elapsed =>{
            if (elapsed >= threshold){
                var id = Thread.CurrentThread.ManagedThreadId;
                LogTime(id, message)(elapsed);
            }
        };
        public static Action<long> LogTime(string message) => LogTime(Thread.CurrentThread.ManagedThreadId, message);
        public static Action<long> LogTime(int threadId, string message) => elapsed => Task.Run(() => Debug.Print($"[{threadId}] ({elapsed} ms) {message}"));
    }
}