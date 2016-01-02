namespace iGlass.ViewModels

open System
open System.Windows
open System.Windows.Data

open FSharp.Core.Fluent
open FSharp.ViewModule
open RZ.Foundation
open iGlass.Core
open System.Windows.Media
open System.Windows.Controls
open System.Diagnostics
open FSharp.Core.Printf
open System.Windows.Media.Imaging

type ScaleModel =
  | Manual
  | ScaleUpToWindow
  | FitToWindow
  | FillWindow
  with
    static member toMode = function
      | "Manual" -> Manual
      | "ScaleUpToWindow" -> ScaleUpToWindow
      | "FitToWindow" -> FitToWindow
      | "FillWindow" -> FillWindow
      | _ -> ScaleUpToWindow

[<NoComparison>]
type MainWindowEvent =
  | Invalid             of string
  | DragEnter           of DragEventArgs
  | Drop                of FileDesc list
  | Zoom                of ScaleModel

module ImageLoader =
  let extractImage path =
    try
      let bi = BitmapImage()
      bi.BeginInit()
      bi.UriSource <- Uri(path)
      bi.EndInit()
      bi.Freeze()
      Some (bi :> ImageSource)
    with
    | :? NotSupportedException -> None  // possibly invalid file

type MainWindowViewModel() as me =
  inherit EventViewModelBase<MainWindowEvent>()

  [<Literal>]
  let AppTitle = "iGlassy"

  let EmptySource = DependencyProperty.UnsetValue

  let mutable imageSource: obj = EmptySource
  let image: INotifyingValue<ImageIndex option> = me.Factory.Backing(<@ me.Image @>, None)
  let imageCount = me.Factory.Backing(<@ me.ImageCount @>, 0)
  let scaleMode = me.Factory.Backing(<@ me.ScaleMode @>, ScaleUpToWindow)
  let scale = me.Factory.Backing(<@ me.Scale @>, 1.0)
  let viewSize = me.Factory.Backing(<@ me.ViewSize @>, Size(0.,0.))

  let mainWindowCommand = me.Factory.EventValueCommand()
  let zoomCommand = me.Factory.EventValueCommand(ScaleModel.toMode >> Zoom)

  let reloadImageSource (v: ImageIndex option) =
    v.map(fst >> ImageLoader.extractImage >> Option.map box >> Option.getOrElse (constant EmptySource))
     .do'(fun bmp -> imageSource <- bmp; me.RaisePropertyChanged "ImageSource")

  member __.Image
    with get() = image.Value 
    and set v = image.Value <- v
                reloadImageSource v
                me.RaisePropertyChanged "Title"
  member __.ImageCount
    with get() = imageCount.Value 
    and set v = imageCount.Value <- v
                me.RaisePropertyChanged "Title"
  member __.ImageSource = imageSource
  member __.Scale
    with get() = scale.Value
    and set v = scale.Value <- v
                scaleMode.Value <- Manual
  member __.ScaleMode with get() = scaleMode.Value and set v = scaleMode.Value <- v

  member __.ViewSize with get() = viewSize.Value and set v = viewSize.Value <- v

  member __.Title =
    match image.Value with
    | None -> AppTitle
    | Some (img, pos) -> sprintf "%s - [%d/%d] (%.3f) %s" AppTitle (pos+1) me.ImageCount scale.Value img

  member __.MainWindowCommand = mainWindowCommand
  member __.ZoomCommand = zoomCommand

type ScaleModelConverter() =
  static let scaleModelToStretch _ =
    function
    | Manual _ -> Stretch.None
    | ScaleUpToWindow
    | FitToWindow -> Stretch.Uniform
    | FillWindow -> Stretch.UniformToFill
    >> box

  static let scaleModelToDirection _ =
    function
    | ScaleUpToWindow -> StretchDirection.DownOnly
    | Manual _ -> StretchDirection.Both  // ignored anyway
    | FitToWindow
    | FillWindow -> StretchDirection.Both
    >> box

  static let scaleModelToScrollBarVisibility _ =
    function
    | Manual _ -> ScrollBarVisibility.Auto
    | _ -> ScrollBarVisibility.Disabled
    >> box

  static let scaleModelToMenuItemCheckBox = ScaleModel.toMode >> (=) >> ((<<) box)
  
  static let converters =
    [ typeof<Stretch>, scaleModelToStretch
      typeof<StretchDirection>, scaleModelToDirection
      typeof<ScrollBarVisibility>, scaleModelToScrollBarVisibility
      typeof<bool>, scaleModelToMenuItemCheckBox
    ]

  interface IValueConverter with
    member __.Convert(value, targetType, parameter, culture) =
      converters
        .tryFind(fst >> (=)targetType)
        .map(snd)
        .ap(Some <| parameter.cast<string>())
        .ap(value.tryCast<ScaleModel>())
        .getOrElse(constant DependencyProperty.UnsetValue)

    member __.ConvertBack(value, targetType, parameter, culture) = DependencyProperty.UnsetValue

