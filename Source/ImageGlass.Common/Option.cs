using System;
using System.Diagnostics.Contracts;

namespace ImageGlass.Common
{
    [ContractClassFor(typeof(Option<>))]
    abstract class OptionContract<T> : Option<T>{
        public override void Do(Action<T> handler){
            Contract.Requires(handler != null);
        }

        public override void Do(Action noneHandler, Action<T> someHandler){
            Contract.Requires(noneHandler != null);
            Contract.Requires(someHandler != null);
        }

        public override TResult Get<TResult>(Func<TResult> noneHandler, Func<T, TResult> someHandler){
            Contract.Requires(noneHandler != null);
            Contract.Requires(someHandler != null);
            return default(TResult);
        }

        public override Option<TB> Map<TB>(Func<T, TB> mapper){
            Contract.Requires(mapper != null);
            return null;
        }

        public override Option<TB> Chain<TB>(Func<T, Option<TB>> mapper){
            Contract.Requires(mapper != null);
            return null;
        }
    }
    [ContractClass(typeof(OptionContract<>))]
    public abstract class Option<T>
    {
        static readonly NoneType NoneSingleton = new NoneType();
        public sealed class NoneType : Option<T>
        {
            public override Option<TB> Chain<TB>(Func<T, Option<TB>> mapper) => Option<TB>.None();
            public override Option<T> FailedTry(Func<Option<T>> other) => other();

            public override bool IsSome { get; } = false;
            public override bool IsNone { get; } = true;

            public override void Do(Action<T> handler) {
                // intentionally do nothing
            }

            public override void Do(Action noneHandler, Action<T> someHandler) => noneHandler();
            public override T Get(){
                throw new InvalidOperationException();
            }
            public override TResult Get<TResult>(Func<TResult> noneHandler, Func<T, TResult> someHandler) => noneHandler();
            public override T GetOrElse(Func<T> noneHandler) => noneHandler();
            public override Option<TB> Map<TB>(Func<T, TB> mapper) => Option<TB>.None();
        }
        public sealed class SomeType : Option<T> {
            readonly T value;
            public SomeType(T value) {
                this.value = value;
            }

            public override void Do(Action<T> handler) => handler(value);
            public override void Do(Action noneHandler, Action<T> someHandler) => someHandler(value);
            public override T Get(){
                return value;
            }
            public override TResult Get<TResult>(Func<TResult> noneHandler, Func<T, TResult> someHandler) => someHandler(value);
            public override T GetOrElse(Func<T> noneHandler) => value;
            public override Option<TB> Map<TB>(Func<T, TB> mapper) => Option<TB>.Some(mapper(value));

            #region Equality
            public override bool Equals(object obj) {
                var other = obj as SomeType;
                return other != null && Equals(value, other.value);
            }
            public override int GetHashCode() => value.GetHashCode();
            #endregion

            public override Option<TB> Chain<TB>(Func<T, Option<TB>> mapper) => mapper(value);
            public override Option<T> FailedTry(Func<Option<T>> other) => this;
            public override bool IsSome { get; } = true;
            public override bool IsNone { get; } = false;
        }
        public static Option<T> From(Func<T> initializer) {
            Contract.Requires(initializer != null);
            try {
                var result = initializer();
                return Equals(result, null)? (Option<T>) None() : Some(result);
            } catch (Exception) {
                return None();
            }
        }
        public static NoneType None() => NoneSingleton;
        public static SomeType Some(T value) => new SomeType(value);
        public abstract void Do(Action<T> handler);
        public abstract void Do(Action noneHandler, Action<T> someHandler);
        public abstract T Get();
        public abstract TResult Get<TResult>(Func<TResult> noneHandler, Func<T,TResult> someHandler);
        public abstract T GetOrElse(Func<T> noneHandler);
        public abstract Option<TB> Map<TB>(Func<T, TB> mapper);
        public abstract Option<TB> Chain<TB>(Func<T, Option<TB>> mapper);
        public abstract Option<T> FailedTry(Func<Option<T>> other);
        public abstract bool IsSome { get; }
        public abstract bool IsNone { get; }
    }
}
