using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace ImageGlass.Common.Extensions{
    public static class CollectionExtension{
        public static void Clear<T>(this ConcurrentQueue<T> queue){
            Contract.Requires(queue != null);

            T item;
            while(queue.TryDequeue(out item)) { }
        }
        public static void ForEach<T>(this IEnumerable<T> seq, Action<T> handler){
            Contract.Requires(seq != null);
            Contract.Requires(handler != null);
            foreach (var item in seq)
                handler(item);
        }
        public static void ForEachIndex<T>(this IEnumerable<T> seq, Action<T,int> handler){
            Contract.Requires(seq != null);
            Contract.Requires(handler != null);
            var index = 0;
            foreach (var item in seq)
                handler(item, index++);
        }
        public static T[] RemoveAt<T>(this T[] array, int n){
            Contract.Requires(array != null);
            Contract.Requires(n >= 0);
            Contract.Ensures(Contract.Result<T[]>() != null);
            return array.Take(n).Skip(n + 1).ToArray();
        }
        public static Option<T> Get<TKey, T>(this Dictionary<TKey, T> dict, TKey key){
            Contract.Requires(dict != null);
            Contract.Requires(!ReferenceEquals(null, key));
            Contract.Ensures(Contract.Result<Option<T>>() != null);
            T result;
            return dict.TryGetValue(key, out result) ? (Option<T>) Option<T>.Some(result) : Option<T>.None();
        }
        public static Option<T> TryFirst<T>(this IEnumerable<T> seq, Func<T, bool> predicate){
            Contract.Requires(seq != null);
            Contract.Requires(predicate != null);
            Contract.Ensures(Contract.Result<Option<T>>() != null);

            foreach (var item in seq.Where(predicate))
                return Option<T>.Some(item);
            return Option<T>.None();
        }
    }
}