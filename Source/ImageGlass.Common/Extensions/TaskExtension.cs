using System;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace ImageGlass.Common.Extensions{
    public static class TaskExtension{
        public static Task<TB> Map<TA,TB>(this Task<TA> task, Func<TA, TB> mapper){
            Contract.Requires(task != null);
            Contract.Requires(mapper != null);
            Contract.Ensures(Contract.Result<Task<TB>>() != null);

            return task.ContinueWith(t => mapper(t.Result));
        } 
        public static Task<Either<Exception, T>> MapEither<T>(this Task<T> task){
            Contract.Requires(task != null);
            Contract.Ensures(Contract.Result<Task<Either<Exception,T>>>() != null);
            return MapEither(task, CancellationToken.None);
        }
        public static Task<Either<Exception, T>> MapEither<T>(this Task<T> task, CancellationToken token){
            Contract.Requires(task != null);
            Contract.Ensures(Contract.Result<Task<Either<Exception,T>>>() != null);
            return task.ContinueWith(t =>{
                if (!token.IsCancellationRequested && t.IsCompleted)
                    return (Either<Exception,T>) Either<Exception, T>.Right(t.Result);
                else
                    return Either<Exception, T>.Left(t.Exception);
            }, token);
        }
        public static void Success<T>(this Task<T> task, Action<T> handler){
            Contract.Requires(task != null);
            Contract.Requires(handler != null);
            task.ContinueWith(t =>{
                if (t.IsCompleted)
                    handler(t.Result);
            });
        }
    }
}