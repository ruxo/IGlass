using System;
using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;
using ImageGlass.Common;

namespace ImageGlass.Core{
    [ContractClassFor(typeof(ISTAgentQueue))]
    class ISTAgentQueueContract : ISTAgentQueue{
        public bool HasWork => false;
        public Option<Action> GetWorkItem(){
            Contract.Ensures(Contract.Result<Option<Action>>() != null);
            return null;
        }
    }
    [ContractClass(typeof(ISTAgentQueueContract))]
    public interface ISTAgentQueue{
        bool HasWork { get; }
        Option<Action> GetWorkItem();
    }
    public sealed class SingleThreadAgent{
        readonly ISTAgentQueue workLoader;
        bool pause;
        // stupid pause control... I'm lazy of making command classes...
        readonly ConcurrentQueue<TaskCompletionSource<bool>> pauseQueue = new ConcurrentQueue<TaskCompletionSource<bool>>();
        public SingleThreadAgent(ISTAgentQueue workLoader){
            Contract.Requires(workLoader != null);

            this.workLoader = workLoader;
        }
        public void Schedule(){
            if (!pause)
                scheduleAgent();
        }
        public Task Pause(){
            var result = new TaskCompletionSource<bool>();
            pauseQueue.Enqueue(result);
            scheduleAgent();
            return result.Task;
        }
        public void Resume(){
            DrainPauseQueue();
            pause = false;
            scheduleAgent();
        }
        public Task Clear(Action clearAction) => Pause().ContinueWith(t => {
            clearAction();
            // clear pause and clear works that have been enqueued AFTER ClearQueue(), if any.
            Resume();
        });

        #region Control Block

        int singleThreadControl;
        void scheduleAgent() {
            if (Interlocked.Increment(ref singleThreadControl) == 1)
                Task.Run(() => {
                    try{
                        agent();
                    } finally{
                        releaseControl();
                    }
                });
            else
                releaseControl();
        }
        void agent(){
            Option<Action> work;
            while (!CheckPause() && (work = workLoader.GetWorkItem()).IsSome)
                work.Do(f => f());
        }
        bool CheckPause(){
            if (pauseQueue.Count <= 0) return pause;
            pause = true;
            DrainPauseQueue();
            return pause;
        }
        void DrainPauseQueue(){
            TaskCompletionSource<bool> pauseTask;
            while(pauseQueue.TryDequeue(out pauseTask))
                pauseTask.SetResult(true);
        }
        void releaseControl() {
            if (Interlocked.Decrement(ref singleThreadControl) == 0 && !pause && workLoader.HasWork)
                scheduleAgent();
        }

        #endregion
    }
}