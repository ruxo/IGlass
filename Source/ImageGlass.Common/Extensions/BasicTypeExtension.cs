namespace ImageGlass.Common.Extensions{
    public static class BasicTypeExtension{
        public static Option<int> ParseInt32(this string s){
            int result;
            return int.TryParse(s, out result) ? (Option<int>) Option<int>.Some(result) : Option<int>.None();
        }
    }
}