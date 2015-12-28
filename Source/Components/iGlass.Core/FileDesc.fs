namespace iGlass.Core

open System
open System.Runtime.CompilerServices
open RZ.Foundation

type FileDesc =
  | DirectoryLocation of string
  | FileLocation of string * string

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FileDesc =
  open System.IO

  let location = function
    | FileLocation (dir, _)
    | DirectoryLocation dir -> dir

  let file = function
    | DirectoryLocation _ -> None
    | FileLocation (_,f) -> Some f

  let Verify path =
    if File.Exists path then
      Some <|
      FileLocation ( Path.GetDirectoryName path |> Path.GetFullPath
                   , Path.GetFileName path )
    elif Directory.Exists path then
      Some (DirectoryLocation <| Path.GetFullPath path)
    else
      None

  let getFullPath (fileDesc: FileDesc) =
    Path.Combine(location fileDesc, file fileDesc |> Option.getOrElse (constant String.Empty))

  let getFiles (fileDesc: FileDesc) =
    if (file fileDesc).IsSome then
      [| getFullPath fileDesc |]
    else
      location fileDesc |> Directory.GetFiles

[<Extension>]
[<AutoOpen>]
type FileDescEx =
  [<Extension>] static member location fd = FileDesc.location fd
  [<Extension>] static member file fd = FileDesc.file fd
  [<Extension>] static member getFiles fd = FileDesc.getFiles fd
  [<Extension>] static member getFullPath fd = FileDesc.getFullPath fd
    
