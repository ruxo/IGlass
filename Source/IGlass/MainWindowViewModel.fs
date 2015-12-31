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
  | Invalid of string
  | DragEnter of DragEventArgs
  | Drop      of FileDesc list
  | Zoom of ScaleModel

[<AutoOpen>]
module private Internal =
  let dbg = Diagnostics.Debug.WriteLine

type MainWindowViewModel() as me =
  inherit EventViewModelBase<MainWindowEvent>()

  [<Literal>]
  let AppTitle = "iGlassy"

  let image: INotifyingValue<ImageIndex option> = me.Factory.Backing(<@ me.Image @>, None)
  let imageCount = me.Factory.Backing(<@ me.ImageCount @>, 0)
  let scaleMode = me.Factory.Backing(<@ me.ScaleMode @>, ScaleUpToWindow)

  let mainWindowCommand = me.Factory.EventValueCommand()
  let zoomCommand = me.Factory.EventValueCommand(ScaleModel.toMode >> Zoom)

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

  member __.Title =
    match image.Value with
    | None -> AppTitle
    | Some (img, pos) -> sprintf "%s - [%d/%d] () %s" AppTitle (pos+1) me.ImageCount img

  member __.MainWindowCommand = mainWindowCommand
  member __.ZoomCommand = zoomCommand

type ScaleModelConverter() =
  static let scaleModelToStretch = function
    | Manual _ -> Stretch.None
    | ScaleUpToWindow
    | FitToWindow -> Stretch.Uniform
    | FillWindow -> Stretch.UniformToFill

  static let scaleModelToDirection = function
    | ScaleUpToWindow -> StretchDirection.DownOnly
    | Manual _ -> StretchDirection.Both  // ignored anyway
    | FitToWindow
    | FillWindow -> StretchDirection.Both

  static let scaleModelToScrollBarVisibility = function
    | Manual _ -> ScrollBarVisibility.Auto
    | _ -> ScrollBarVisibility.Disabled

  static let converters =
    [ typeof<Stretch>, scaleModelToStretch >> box
      typeof<StretchDirection>, scaleModelToDirection >> box
      typeof<ScrollBarVisibility>, scaleModelToScrollBarVisibility >> box
    ]

  interface IValueConverter with
    member __.Convert(value, targetType, parameter, culture) =
      converters
        .tryFind(fst >> (=)targetType)
        .map(snd)
        .ap(value.tryCast<ScaleModel>())
        .getOrElse(constant DependencyProperty.UnsetValue)

    member __.ConvertBack(value, targetType, parameter, culture) = DependencyProperty.UnsetValue

