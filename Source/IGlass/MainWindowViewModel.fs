namespace iGlass.ViewModels

open System
open System.Windows
open System.Windows.Data
open System.Windows.Media.Imaging

open FSharp.Core.Fluent
open FSharp.ViewModule
open RZ.Foundation
open iGlass.Core

[<NoComparison>]
type MainWindowEvent =
  | Invalid of string
  | DragEnter of DragEventArgs
  | Drop      of FileDesc list
  | MouseMove of Point

[<AutoOpen>]
module private Internal =
  let dbg = Diagnostics.Debug.WriteLine

module private DragEventHandlers =
  let isDragEvents = function
  | Drop _ | DragEnter _ -> true
  | _ -> false

  let validateDrag (arg: DragEventArgs) =
    arg.Effects <- if arg.Data.GetDataPresent(DataFormats.FileDrop)
                      then DragDropEffects.Copy
                      else Printf.kprintf dbg "Dropping object: %s" <| String.Join(",", arg.Data.GetFormats())
                           DragDropEffects.None

  let getDropTarget (arg: DragEventArgs) =
    arg.Handled <- true
    let data = arg.Data.GetData(DataFormats.FileDrop) :?> string[]
    data |> Seq.choose FileDesc.Verify
         |> Seq.toList

type MainWindowViewModel() as me =
  inherit EventViewModelBase<MainWindowEvent>()

  [<Literal>]
  let AppTitle = "iGlassy"

  let image: INotifyingValue<ImageIndex option> = me.Factory.Backing(<@ me.Image @>, None)
  let imageCount = me.Factory.Backing(<@ me.ImageCount @>, 0)

  let mainWindowCommand = me.Factory.EventValueCommand()

  member __.Image
    with get() = image.Value 
    and set v = image.Value <- v
                me.RaisePropertyChanged "Title"
  member __.ImageCount
    with get() = imageCount.Value 
    and set v = imageCount.Value <- v
                me.RaisePropertyChanged "Title"
  member __.Title =
    match image.Value with
    | None -> AppTitle
    | Some (img, pos) -> sprintf "%s - [%d/%d] () %s" AppTitle (pos+1) me.ImageCount img

  member __.MainWindowCommand = mainWindowCommand

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

type ImageFromImageIndex() =
  let extractImage (path, _) =
    try
      let bi = BitmapImage()
      bi.BeginInit()
      bi.UriSource <- Uri(path)
      bi.EndInit()
      Some bi
    with
    | :? NotSupportedException -> None  // possibly invalid file

  interface IValueConverter with
    member __.Convert(value, targetType, parameter, culture) =
      value
        .tryCast<ImageIndex option>()
        .join()
        .bind(extractImage)
        .map(box)
        .getOrElse(constant DependencyProperty.UnsetValue)

    member __.ConvertBack(value, targetType, parameter, culture) = DependencyProperty.UnsetValue

type private DragEventConverter = FsXaml.EventArgsConverter<DragEventArgs, MainWindowEvent>
type DropConverter() = inherit DragEventConverter(DragEventHandlers.getDropTarget >> Drop, Invalid "DropConverter")
type DragEnterConverter() = inherit DragEventConverter(DragEnter, Invalid "DragEnterConverter")
