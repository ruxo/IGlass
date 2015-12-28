using System;

namespace ImageGlass.Common
{
    public static class Prelude
    {
        public static Func<T> Constant<T>(T x) => () => x;
        public static T Identity<T>(T x) => x;
        public static void Noop() { }
    }
}
