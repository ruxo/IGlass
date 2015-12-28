using System;
using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading.Tasks;
using ImageGlass.Common;
using ImageGlass.Common.Extensions;

namespace ImageGlass.Core{
    sealed class DiskWorkItem{
        public DateTime Timestamp { get; }
        public Action Work { get; }
        public DiskWorkItem(Action work){
            Work = work;
            Timestamp = DateTime.Now;
        }
    }
    [ContractClassFor(typeof(IDiskManager))]
    class IDiskManagerContract : IDiskManager {
        public Task<Either<Exception, byte[]>> LoadFile(string filename, IoPriority priority){
            Contract.Requires(!string.IsNullOrWhiteSpace(filename));
            Contract.Ensures(Contract.Result<Either<Exception,byte[]>>() != null);

            return null;
        }
        public Task<T> ScheduleIO<T>(string path, IoPriority priority, Func<T> work){
            Contract.Requires(work != null);
            Contract.Requires(!string.IsNullOrWhiteSpace(path));
            Contract.Ensures(Contract.Result<Task<T>>() != null);

            return null;
        }
        public Task ScheduleIO(string path, IoPriority priority, Action work){
            Contract.Requires(work != null);
            Contract.Requires(!string.IsNullOrWhiteSpace(path));
            Contract.Ensures(Contract.Result<Task>() != null);
            return null;
        }
    }
    /// <summary>
    /// Serialize disk I/O access to get optimum performance.
    /// </summary>
    [ContractClass(typeof(IDiskManagerContract))]
    public interface IDiskManager{
        Task<Either<Exception, byte[]>> LoadFile(string filename, IoPriority priority);
        /// <summary>
        /// Schedule the work on the presumed <paramref name="path"/>
        /// </summary>
        /// <typeparam name="T">Result of I/O work</typeparam>
        /// <param name="path">Path that assumes the work will be working on.</param>
        /// <param name="priority"></param>
        /// <param name="work">I/O work that should perform on the target path.</param>
        /// <returns></returns>
        Task<T> ScheduleIO<T>(string path, IoPriority priority, Func<T> work);
        /// <summary>
        /// Schedule the work on the presumed <paramref name="path"/>
        /// </summary>
        /// <param name="path">Path that assumes the work will be working on.</param>
        /// <param name="priority"></param>
        /// <param name="work">I/O work that should perform on the target path.</param>
        /// <returns></returns>
        Task ScheduleIO(string path, IoPriority priority, Action work);
    }
    /// <summary>
    /// Center disk I/O manager
    /// </summary>
    public sealed class DiskManager : ISTAgentQueue, IDiskManager {
        readonly ConcurrentQueue<DiskWorkItem> backgroundLoad = new ConcurrentQueue<DiskWorkItem>();
        readonly ConcurrentQueue<DiskWorkItem> workLoad = new ConcurrentQueue<DiskWorkItem>();
        readonly ConcurrentQueue<DiskWorkItem> priorityLoad = new ConcurrentQueue<DiskWorkItem>();
        readonly SingleThreadAgent agent;
        public DiskManager(){
            agent = new SingleThreadAgent(this);
        }
        public Task<Either<Exception, byte[]>> LoadFile(string filename, IoPriority priority){
            return EnqueueIoTask<Either<Exception, byte[]>>(priority, result =>{
                try{
                    result.SetResult(Either<Exception, byte[]>.Right(File.ReadAllBytes(filename)));
                }
                catch (Exception ex){
                    result.SetResult(Either<Exception, byte[]>.Left(ex));
                }
            });
        }
        public Task<T> ScheduleIO<T>(string path, IoPriority priority, Func<T> work){
            // TODO use path to schedule agent on different disk volumes.

            return EnqueueIoTask<T>(priority, result => {
                try{
                    result.SetResult(work());
                }
                catch (Exception ex){
                    result.SetException(ex);
                }
            });
        }
        public Task ScheduleIO(string path, IoPriority priority, Action work) => ScheduleIO(path, priority, () => {
            work();
            return true;
        });
        Task<T> EnqueueIoTask<T>(IoPriority priority, Action<TaskCompletionSource<T>> action){
            var result = new TaskCompletionSource<T>();
            var workItem = new DiskWorkItem(() => action(result));
            var queue = priority == IoPriority.Background
                ? backgroundLoad
                : priority == IoPriority.Asap
                    ? priorityLoad
                    : workLoad;
            queue.Enqueue(workItem);
            agent.Schedule();
            return result.Task;
        }

        #region ISTAgentQueue

        bool ISTAgentQueue.HasWork => workLoad.Count > 0;
        Option<Action> ISTAgentQueue.GetWorkItem(){
            DiskWorkItem item;
            return priorityLoad.TryDequeue(out item)
                || workLoad.TryDequeue(out item)
                || backgroundLoad.TryDequeue(out item)
                ? (Option<Action>) Option<Action>.Some(item.Work)
                : Option<Action>.None();
        }

        #endregion
    }
}