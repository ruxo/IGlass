using ImageGlass.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Globalization;
using System.Linq;
using System.Diagnostics.Contracts;
using ImageGlass.Common.Extensions;
using BitmapCacheOption = ImageGlass.Common.Option<ImageGlass.Common.Either<System.Exception, System.Drawing.Bitmap>>;
using BitmapCacheItem = ImageGlass.Common.Either<System.Exception, System.Drawing.Bitmap>;

namespace ImageGlass.Core {

    class ImgCacheItem {
        public string File { get; set; }
        public DateTime LastAccess { get; set; }
        public BitmapCacheItem Bitmap { get; set; }
    }
    class WorkingQueueItem
    {
        public string File;
        public TaskCompletionSource<BitmapCacheItem> Promise;
        public IoPriority Priority;
    }

    /// <summary>
    /// Thread-safe image cache.
    /// </summary>
    public class ImgCache : ISTAgentQueue, IDisposable {
        readonly IDiskManager diskManager;
        const int MaximumCacheItems = 30;   // assume 1 image size ~= 16MB.. 30 entries ~= 480MB of RAM
        /// <summary>
        /// Number of items left after purged.
        /// </summary>
        const int PurgeSize = 20;

        ImgCacheItem[] cache = new ImgCacheItem[0];

        readonly SingleThreadAgent agent;

        public ImgCache(IDiskManager diskManager){
            Contract.Requires(diskManager != null);

            this.diskManager = diskManager;
            agent = new SingleThreadAgent(this);
        }

        public Task<BitmapCacheItem> GetImage(string filename) {
            Contract.Requires(!string.IsNullOrWhiteSpace(filename));
            Contract.Ensures(Contract.Result<Task<BitmapCacheItem>>() != null);

            var normalized = normalizeFilename(filename);

            return imageFromCache(normalized)
                .Get(() => scheduleGet(normalized, IoPriority.Asap), Task.FromResult);
        }
        public void Preload(params string[] files){
            Contract.Requires(files != null);

            Preload((IEnumerable<string>) files);
        }
        public void Preload(IEnumerable<string> files) {
            Contract.Requires(files != null);
            Contract.Requires(Contract.ForAll(files, s => !string.IsNullOrWhiteSpace(s)));

            files
                .Select(normalizeFilename)
                .Where(f => cache.All(fc => fc.File != f))
                .ForEach(f => scheduleGet(f, IoPriority.General));
        }
        BitmapCacheOption imageFromCache(string filename) {
            return BitmapCacheOption.From(() => cache.FirstOrDefault(item => item.File == filename)?.Bitmap);
        }
        /// <summary>
        /// Normalize file name for Windows sytem. On Linux, it can just return the same filename.
        /// </summary>
        string normalizeFilename(string filename) => filename.ToUpper(CultureInfo.InvariantCulture);
        Task<BitmapCacheItem> scheduleGet(string normalized, IoPriority priority) {
            var result = new TaskCompletionSource<BitmapCacheItem>();

            loadingQueue.Enqueue(new WorkingQueueItem{
                File = normalized, Promise = result, Priority = priority
            });
            agent.Schedule();

            return result.Task;
        }

        #region Fetching agent

        readonly ConcurrentQueue<WorkingQueueItem> loadingQueue = new ConcurrentQueue<WorkingQueueItem>();
        async Task<BitmapCacheItem> cacheFile(IoPriority priority, string path) {
            var result = await Interpreter.Load(diskManager, priority, path, forPreview: false);
            var cacheItem = new ImgCacheItem {
                File = path,
                LastAccess = DateTime.Now,
                Bitmap = result
            };
            cache = purgeCache().Concat(new [] { cacheItem }).ToArray();
            return result;
        }
        IEnumerable<ImgCacheItem> purgeCache() {
            if (cache.Length >= MaximumCacheItems)
                return cache.OrderByDescending(i => i.LastAccess).Take(PurgeSize);
            else
                return cache;
        }
        Option<WorkingQueueItem> fetchWork() {
            WorkingQueueItem result;

            return loadingQueue.TryDequeue(out result)
                 ? (Option<WorkingQueueItem>) Option<WorkingQueueItem>.Some(result)
                 : Option<WorkingQueueItem>.None();
        }
        public bool HasWork => loadingQueue.Count > 0;
        public Option<Action> GetWorkItem(){
            return fetchWork()
                .Map(w => new Action(() =>{
                    var img = imageFromCache(w.File);
                    if (img.IsSome)
                        w.Promise.SetResult(img.Get());
                    else
                        cacheFile(w.Priority, w.File).Success(w.Promise.SetResult);
                }));
        }
        #endregion

        #region IDisposable Support

        bool disposedValue;

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    flushQueue(loadingQueue);
                    // TODO sync with fetch agent

                    cache.ForEach(i => i.Bitmap.Do(bmp => bmp.Dispose()));
                    cache = null;
                }
                disposedValue = true;
            }
        }
        void flushQueue<T>(ConcurrentQueue<T> queue) {
            T temp;
            while (queue.TryDequeue(out temp)){}
        }
        public void Dispose() {
            Dispose(true);
        }
        #endregion

    }
}
