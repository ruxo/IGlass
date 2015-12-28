namespace iGlass.ViewModels

open System
open System.Windows.Data
open System.Windows.Media.Imaging

open FSharp.ViewModule
open System.Windows
open RZ.Foundation
open iGlass.Core

[<NoComparison>]
type MainWindowEvent =
  | Invalid of string
  | DragEnter of DragEventArgs
  | Drop      of FileDesc list
  | MouseMove of Point

[<AutoOpen>]
module private CommonUse =
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

  let mutable imageManager = ImageManager(Seq.empty)
  let image: NotifyingValue<string option> = me.Factory.Backing(<@ me.Image @>, None)

  let mainWindowCommand = me.Factory.EventValueCommand()

  member x.Image with get() = image.Value
  member x.MainWindowCommand = mainWindowCommand
  member x.Title = image.Value |> Option.cata (constant AppTitle) (sprintf "%s - %s" AppTitle)

  member x.GalleryFrom (fdList: FileDesc seq) =
    imageManager <- ImageManager(fdList)
    image.Value <- imageManager.Current
    
  member x.SelectGallery (fileDesc: FileDesc) = x.GalleryFrom(Seq.singleton fileDesc)


type MainWindowController(model: MainWindowViewModel) =
  let handleDrag = function
  | DragEnter arg -> DragEventHandlers.validateDrag arg; arg.Handled <- true
  | Drop fileList -> model.GalleryFrom(fileList)
  | case -> Printf.kprintf dbg "Unexpected case: %A" case

  member __.Initialize() =
    model.EventStream
    |> Observable.filter DragEventHandlers.isDragEvents
    |> Observable.subscribe handleDrag
    |> ignore


type StringOptionToImageSource() =
  interface IValueConverter with
    member x.Convert(value, targetType, parameter, culture) =
      let stringOpt = value :?> string option
      match stringOpt with
      | None -> DependencyProperty.UnsetValue
      | Some path ->
        let bi = BitmapImage()
        bi.BeginInit()
        bi.UriSource <- Uri(path)
        bi.EndInit()
        bi :> obj

    member x.ConvertBack(value, targetType, parameter, culture) = DependencyProperty.UnsetValue

type private DragEventConverter = FsXaml.EventArgsConverter<DragEventArgs, MainWindowEvent>
type DropConverter() = inherit DragEventConverter(DragEventHandlers.getDropTarget >> Drop, Invalid "DropConverter")
type DragEnterConverter() = inherit DragEventConverter(DragEnter, Invalid "DragEnterConverter")
