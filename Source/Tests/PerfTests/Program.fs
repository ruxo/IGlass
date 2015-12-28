open System.Diagnostics
open ImageGlass.Core
open System.IO

let TESTPATH = @"Z:\p\Unclassified"

let time f =
  let watch = Stopwatch.StartNew()
  f()
  let elasped = watch.Elapsed
  printfn "Work @ %f ms." elasped.TotalMilliseconds

let jitLoad() =
  use cache = new ImgCache()
  ignore <| cache.GetImage("xxxx")

let test() =
  use cache = new ImgCache()
  ignore <| cache.GetImage(Path.Combine(TESTPATH, "20150702_122652688_iOS.jpg"))

[<EntryPoint>]
let main argv = 
  jitLoad()

  time test
  0
