﻿namespace iGlass.ViewModels

open System
open System.Windows
open System.Windows.Input
open System.Windows.Data

open FSharp.Core.Fluent
open FSharp.ViewModule
open iGlass.Core
open System.Windows.Media
open System.Windows.Controls
open System.Diagnostics
open FSharp.Core.Printf
open System.Windows.Media.Imaging
open RZ.Foundation
open RZ.Extensions
open RZ.Wpf.CodeBehind

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
  | FirstPage
  | NextPage
  | PreviousPage
  | LastPage

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


type MainWindowViewModel() as me =
  inherit ViewModelBase()

  [<Literal>]
  static let AppTitle = "iGlassy"

  let modelEvent = Event<MainWindowEvent>()

  let EmptySource = DependencyProperty.UnsetValue

  let mutable imageSource: ImageSource option = None
  let image: INotifyingValue<ImageIndex option> = me.Factory.Backing(<@ me.Image @>, None)
  let imageCount = me.Factory.Backing(<@ me.ImageCount @>, 0)
  let scaleMode = me.Factory.Backing(<@ me.ScaleMode @>, ScaleUpToWindow)
  let scale = me.Factory.Backing(<@ me.Scale @>, 1.0)
  let viewSize = me.Factory.Backing(<@ me.ViewSize @>, Size(1.,1.))

  static let isVertOverflow (viewSize: Size) (imageSize: Size) = (viewSize.Width / imageSize.Width) > (viewSize.Height / imageSize.Height)
  static let showScroll = function
    | true -> ScrollBarVisibility.Visible
    | false -> ScrollBarVisibility.Hidden

  static let getNormalScrollVisibility scaleMode =
    match scaleMode with
    | Manual _ -> Some ScrollBarVisibility.Auto
    | FillWindow -> None
    | _ -> Some ScrollBarVisibility.Disabled

  let getImageSize() = imageSource.map(fun bmp -> Size(bmp.Width, bmp.Height))

  let rec recalcScale scaleMode (viewSize: Size) (imageSize: Size) =
    match scaleMode with
    | Manual -> scale.Value
    | ScaleUpToWindow ->
      if viewSize |> Size.canContain imageSize
        then 1.
        else recalcScale FitToWindow viewSize imageSize

    | FitToWindow -> min (viewSize.Width / imageSize.Width) (viewSize.Height / imageSize.Height)
    | FillWindow -> max (viewSize.Width / imageSize.Width) (viewSize.Height / imageSize.Height)

  let commandCenter =
    [ NavigationCommands.FirstPage |> CommandMap.to' (constant FirstPage)
      NavigationCommands.NextPage |> CommandMap.to' (constant NextPage)
      NavigationCommands.PreviousPage |> CommandMap.to' (constant PreviousPage)
      NavigationCommands.LastPage |> CommandMap.to' (constant LastPage) 
      AppCommands.ZoomChanged |> CommandMap.to' (Zoom << (cast<string> >> ScaleModel.toMode))
      AppCommands.CheckDrop |> CommandMap.to' cast<MainWindowEvent>
      AppCommands.DropItem |> CommandMap.to' cast<MainWindowEvent> ]
    |> CommandControlCenter modelEvent.Trigger

  interface ICommandHandler with
    member __.ControlCenter = commandCenter

  member private __.RecalcScale(bmp: ImageSource) = 
    scale.Value <- recalcScale scaleMode.Value viewSize.Value (Size(bmp.Width, bmp.Height))
    me.RaisePropertyChanged "ScaleApply"
    me.RaisePropertyChanged "HScrollbarVisibility"
    me.RaisePropertyChanged "VScrollbarVisibility"
    me.RaisePropertyChanged "Title"

  member __.Image with get() = image.Value 
                  and set v = image.Value <- v
                              imageSource <- v.bind(fst >> ImageLoader.extractImage)
                              imageSource.do' me.RecalcScale
                              me.RaisePropertyChanged "ImageSource"
  member __.ImageCount with get() = imageCount.Value 
                       and set v = imageCount.Value <- v
                                   me.RaisePropertyChanged "Title"
  member __.Scale with get() = scale.Value
                  and set v = scale.Value <- v
                              scaleMode.Value <- Manual
  member __.ScaleMode with get() = scaleMode.Value 
                      and set v = scaleMode.Value <- v
                                  imageSource.do' me.RecalcScale
                                  me.RaisePropertyChanged "ScaleApply"
  member __.ViewSize with get() = viewSize.Value and set v = viewSize.Value <- v; imageSource.do' me.RecalcScale

  (********* Derived Properties *********)
  member __.ImageSource = imageSource.map(box).getOrElse(constant EmptySource)

  member __.CheckDrop = AppCommands.CheckDrop
  member __.DropItem = AppCommands.DropItem
  member __.DragEnterConverter(_:string, e: DragEventArgs) = DragEnter e
  member __.DropConverter(_:string, e: DragEventArgs) = e |> DragEventHandlers.getDropTarget |> Drop


  /// <summary>
  /// Transformation scale that should be applied to image.
  /// </summary>
  member __.ScaleApply =
    match scaleMode.Value with
    | FillWindow
    | Manual -> scale.Value
    | _ -> 1.

  member __.HScrollbarVisibility = 
    scaleMode.Value
      |> getNormalScrollVisibility
      |> Option.orTry (getImageSize >> Option.map ((not << isVertOverflow viewSize.Value) >> showScroll))
      |> Option.getOrElse (constant ScrollBarVisibility.Disabled)
  member __.VScrollbarVisibility = 
    scaleMode.Value
      |> getNormalScrollVisibility
      |> Option.orTry (getImageSize >> Option.map (isVertOverflow viewSize.Value >> showScroll))
      |> Option.getOrElse (constant ScrollBarVisibility.Disabled)

  member __.Title =
    match image.Value with
    | None -> AppTitle
    | Some (img, pos) -> sprintf "%s - [%d/%d] (%.3f) %s" AppTitle (pos+1) me.ImageCount scale.Value img

  member __.ViewEvents = modelEvent.Publish :> IObservable<MainWindowEvent>

type ScaleModelConverter() =
  static let scaleModelToStretch _ =
    function
    | FillWindow
    | Manual _ -> Stretch.None
    | ScaleUpToWindow
    | FitToWindow -> Stretch.Uniform
    >> box

  static let scaleModelToDirection _ =
    function
    | ScaleUpToWindow -> StretchDirection.DownOnly
    | FillWindow
    | Manual _ -> StretchDirection.Both  // ignored anyway
    | FitToWindow -> StretchDirection.Both
    >> box

  static let scaleModelToMenuItemCheckBox = ScaleModel.toMode >> (=) >> ((<<) box)
  
  static let converters =
    [ typeof<Stretch>, scaleModelToStretch
      typeof<StretchDirection>, scaleModelToDirection
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

