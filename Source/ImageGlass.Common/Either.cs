using System;

namespace ImageGlass.Common {
    public abstract class Either<TLeft,TRight> {
        public sealed class LeftType : Either<TLeft,TRight> {
            readonly TLeft value;
            public LeftType(TLeft value) {
                this.value = value;
            }

            public override bool IsLeft { get; } = true;
            public override bool IsRight { get; } = false;
            public override void Do(Action<TRight> handler){ /* do nothing */ }
            public override void Do(Action<TLeft> left, Action<TRight> someHandler) => left(value);
            public override T Get<T>(Func<TLeft, T> left, Func<TRight, T> right) => left(value);
            public override Option<TRight> ToOption() => Option<TRight>.None();
        }
        public sealed class RightType : Either<TLeft,TRight> {
            readonly TRight value;
            public RightType(TRight value) {
                this.value = value;
            }

            public override bool IsLeft { get; } = false;
            public override bool IsRight { get; } = true;
            public override void Do(Action<TRight> handler) => handler(value);
            public override void Do(Action<TLeft> left, Action<TRight> right) => right(value);
            public override T Get<T>(Func<TLeft, T> left, Func<TRight, T> right) => right(value);
            public override Option<TRight> ToOption() => Option<TRight>.Some(value);
        }
        public static LeftType Left(TLeft value) {
            return new LeftType(value);
        }
        public static RightType Right(TRight value) {
            return new RightType(value);
        }
        public static Either<Exception, T> SafeDo<T>(Func<T> f){
            try{
                return Either<Exception, T>.Right(f());
            } catch (Exception ex){
                return Either<Exception, T>.Left(ex);
            }
        }
        public abstract bool IsLeft { get; }
        public abstract bool IsRight { get; }
        public abstract void Do(Action<TRight> handler);
        public abstract void Do(Action<TLeft> left, Action<TRight> right);
        public abstract T Get<T>(Func<TLeft, T> left, Func<TRight, T> right);
        public abstract Option<TRight> ToOption();
    }
}
