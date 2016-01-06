namespace iGlass.ViewModels

open System.Diagnostics
open System.Windows
open FSharp.Core.Fluent
open RZ.Foundation
open iGlass.Core

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
  | FirstPage -> model.Image <- imageManager.FirstFileName()
  | LastPage -> model.Image <- imageManager.LastFileName()
  | NextPage -> model.Image <- model.Image.bind(snd >> imageManager.NextIndex)
  | PreviousPage -> model.Image <- model.Image.bind(snd >> imageManager.PreviousIndex)
  | Invalid case -> Printf.kprintf Debug.WriteLine "Invalid: %s" case

  member __.Initialize() =
    model.ViewEvents |> Observable.subscribe handleEvents |> ignore
    
  member __.SelectFileName filename = model.Image <- imageManager.FindFileName(filename)
  member __.InitGallery = galleryFromSingleFile
