namespace iGlass.Core

open System
open System.IO
open FSharp.Core.Fluent

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

  let fileList = source.collect(FileDesc.getFiles >> Seq.filter isSupported).toArray()
  let currentIndex = if fileList.Length > 0 then Some 0 else None

  member x.Current: ImageIndex option = currentIndex.map(fun pos -> Array.get fileList pos, pos)