namespace iGlass.ViewModels

open System.Diagnostics
open System.Windows
open RZ.Foundation
open iGlass.Core

module private DragEventHandlers =
  let validateDrag (arg: DragEventArgs) =
    arg.Effects <- if arg.Data.GetDataPresent(DataFormats.FileDrop)
                      then DragDropEffects.Copy
                      else DragDropEffects.None

  let getDropTarget (arg: DragEventArgs) =
    arg.Handled <- true
    let data = arg.Data.GetData(DataFormats.FileDrop) :?> string[]
    data |> Seq.choose FileDesc.Verify
         |> Seq.toList

type private DragEventConverter = FsXaml.EventArgsConverter<DragEventArgs, MainWindowEvent>
type DropConverter() = inherit DragEventConverter(DragEventHandlers.getDropTarget >> Drop, Invalid "DropConverter")
type DragEnterConverter() = inherit DragEventConverter(DragEnter, Invalid "DragEnterConverter")


type MainWindowController(model: MainWindowViewModel) =
  let mutable imageManager = ImageManager(Seq.empty)

  let galleryFrom showFile (fdList: FileDesc seq) =
    imageManager <- ImageManager(fdList)
    model.Image <- showFile |> Option.bind imageManager.FindFileName |> Option.orTry imageManager.FirstFileName
    model.ImageCount <- imageManager.ImageCount

  let galleryFromSingleFile(fd: FileDesc) = galleryFrom (fd.file()) [fd.getDirectory()]

  let changeZoom zoom = model.ScaleMode <- zoom

  let handleEvents = function
  | DragEnter arg -> DragEventHandlers.validateDrag arg; arg.Handled <- true
  | Drop fileList ->
    match fileList with
    | [] -> ()
    | [single] -> galleryFromSingleFile single
    | xs -> galleryFrom None xs
  | Zoom zoom -> changeZoom zoom
  | LastPage
  | NextPage -> Debug.WriteLine("Next/Last page")
  | Invalid case -> Printf.kprintf Debug.WriteLine "Invalid: %s" case

  member __.Initialize() =
    model.ViewEvents |> Observable.subscribe handleEvents |> ignore
    
  member __.SelectFileName filename = model.Image <- imageManager.FindFileName(filename)
  member __.InitGallery = galleryFromSingleFile
