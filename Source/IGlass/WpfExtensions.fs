namespace RZ.Extensions

module Size =
  open System.Windows

  let canContain (t: Size) (s: Size) = s.Width >= t.Width && s.Height >= t.Height
