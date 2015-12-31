namespace iGlass.Core

open System
open System.IO
open FSharp.Core.Fluent
open RZ
open RZ.Foundation

type FilePath = string
type ImageIndex = string * int

type ImageManagerContext =
  { OriginSource: FileDesc list 
    FileList: string[]
    Current: int option }

type ImageManager(source: FileDesc seq) =
  static let supportImages =
    [ ".JPG"; ".JPEG"
      ".PNG"
      ".BMP"
      ".GIF"
      ".ICO"; ".ICON"
      ".WDP"
      ".TIFF" ]

  let isSupported f =
    let ext = Path.GetExtension f
    in supportImages.exists(fun supported -> supported.Equals(ext, StringComparison.OrdinalIgnoreCase))

  let fileList = source
                   .collect(FileDesc.getFiles >> Seq.filter isSupported)
                   .distinct()
                   .toArray()
  let icmp st (ss:string) = ss.Equals(st, StringComparison.OrdinalIgnoreCase)

  member __.ImageCount = fileList.Length

  member __.FindFileName filename :ImageIndex option =
    fileList
      .tryFindIndex(Path.GetFileName >> icmp filename)
      .map(fun i -> fileList.[i], i)

  member __.GetFileName idx :ImageIndex option = Array.tryItem idx fileList |> Option.map (Pair.with' idx)