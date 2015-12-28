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
  let AppTitle = "iGLassy"

  let image: NotifyingValue<ImageIndex option> = me.Factory.Backing(<@ me.Image @>, None)

  let mainWindowCommand = me.Factory.EventValueCommand()

  member __.Image with get() = image.Value 
                  and set v = image.Value <- v
                              me.RaisePropertyChanged "Title"
  member __.Title =
    match image.Value with
    | None -> AppTitle
    | Some (img, pos) -> sprintf "%s - [%d] %s" AppTitle pos img

  member __.MainWindowCommand = mainWindowCommand

type MainWindowController(model: MainWindowViewModel) =
  let mutable imageManager = ImageManager(Seq.empty)

  let galleryFrom (fdList: FileDesc seq) =
    imageManager <- ImageManager(fdList)
    model.Image <- imageManager.Current

  let handleDrag = function
  | DragEnter arg -> DragEventHandlers.validateDrag arg; arg.Handled <- true
  | Drop fileList -> galleryFrom(fileList)
  | case -> Printf.kprintf dbg "Unexpected case: %A" case

  member __.Initialize() =
    model.EventStream
    |> Observable.filter DragEventHandlers.isDragEvents
    |> Observable.subscribe handleDrag
    |> ignore
    
  member __.SelectGallery (fileDesc: FileDesc) = galleryFrom(Seq.singleton fileDesc)


type ImageFromImageIndex() =
  let extractImage (path, _) =
    let bi = BitmapImage()
    bi.BeginInit()
    bi.UriSource <- Uri(path)
    bi.EndInit()
    bi

  interface IValueConverter with
    member __.Convert(value, targetType, parameter, culture) =
      value
        .tryCast<ImageIndex option>()
        .join()
        .map(extractImage >> box)
        .getOrElse(constant DependencyProperty.UnsetValue)

    member __.ConvertBack(value, targetType, parameter, culture) = DependencyProperty.UnsetValue

type private DragEventConverter = FsXaml.EventArgsConverter<DragEventArgs, MainWindowEvent>
type DropConverter() = inherit DragEventConverter(DragEventHandlers.getDropTarget >> Drop, Invalid "DropConverter")
type DragEnterConverter() = inherit DragEventConverter(DragEnter, Invalid "DragEnterConverter")
