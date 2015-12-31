namespace iGlass.ViewModels

open iGlass.Core

type MainWindowController(model: MainWindowViewModel) =
  let mutable imageManager = ImageManager(Seq.empty)

  let galleryFrom showFile (fdList: FileDesc seq) =
    imageManager <- ImageManager(fdList)
    model.Image <- showFile |> Option.bind imageManager.FindFileName
    model.ImageCount <- imageManager.ImageCount

  let galleryFromSingleFile(fd: FileDesc) = galleryFrom (fd.file()) [fd.getDirectory()]

  let handleDrag = function
  | DragEnter arg -> DragEventHandlers.validateDrag arg; arg.Handled <- true
  | Drop fileList ->
    match fileList with
    | [] -> ()
    | [single] -> galleryFromSingleFile single
    | xs -> galleryFrom None xs
  | case -> Printf.kprintf dbg "Unexpected case: %A" case

  member __.Initialize() =
    model.EventStream
    |> Observable.filter DragEventHandlers.isDragEvents
    |> Observable.subscribe handleDrag
    |> ignore
    
  member __.SelectFileName filename = model.Image <- imageManager.FindFileName(filename)
  member __.InitGallery = galleryFromSingleFile
