namespace iGlass.ViewModels

open System
open System.Windows
open System.Windows.Data
open System.Windows.Media.Imaging

open FSharp.Core.Fluent
open FSharp.ViewModule
open RZ.Foundation
open iGlass.Core
open System.Windows.Media
open System.Windows.Controls

[<NoComparison>]
type MainWindowEvent =
  | Invalid of string
  | DragEnter of DragEventArgs
  | Drop      of FileDesc list
  | MouseMove of Point

type ScaleModel =
  | Manual
  | ScaleUpToWindow
  | FitToWindow
  | FillWindow
  with
    static member name = function
      | Manual -> "Manual Scale"
      | ScaleUpToWindow -> "Scale Up to Window"
      | FitToWindow -> "Fit Content to Window"
      | FillWindow -> "Scale Content to Fill Window"

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
  let scaleMode = me.Factory.Backing(<@ me.ScaleMode @>, ScaleUpToWindow)

  let imageStretch = me.Factory.Backing(<@ me.ImageStretch @>, Stretch.Uniform)
  let imageStretchDir = me.Factory.Backing(<@ me.StretchDirection @>, StretchDirection.DownOnly)

  let mainWindowCommand = me.Factory.EventValueCommand()

  member __.Image
    with get() = image.Value 
    and set v = image.Value <- v
                me.RaisePropertyChanged "Title"
  member __.ImageCount
    with get() = imageCount.Value 
    and set v = imageCount.Value <- v
                me.RaisePropertyChanged "Title"
  member __.ScaleMode
    with get() = scaleMode.Value
    and set v = scaleMode.Value <- v

  member __.ImageStretch = imageStretch.Value
  member __.StretchDirection = imageStretchDir.Value
                
  member __.Title =
    match image.Value with
    | None -> AppTitle
    | Some (img, pos) -> sprintf "%s - [%d/%d] () %s" AppTitle (pos+1) me.ImageCount img

  member __.MainWindowCommand = mainWindowCommand


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
