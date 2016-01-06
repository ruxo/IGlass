module iGlass.Views

open FSharp.Core.Fluent
open System.Windows
open System.Windows.Controls
open RZ.Foundation
open RZ.Wpf.CodeBehind
open System.Windows.Input

type MainWindow() as me =
  inherit Window()
    
  do me.InitializeCodeBehind("MainWindow.xaml")
     me.InstallCommandForwarder()

  let imageView = me.FindName("imageView") :?> ScrollViewer

  let getDataContext() = Option.ofObj me.DataContext
  static let getProperty prop (o:obj) = Option.ofObj <| o.GetType().GetProperty(prop)
  static let getSizePropertySetter (o: obj) =
    o
    |> getProperty "ViewSize"
    |> Option.bind (fun p -> Option.ofObj <| p.SetMethod)
    |> Option.map (fun m -> (fun (size: Size) -> m.Invoke(o, [|size|]) |> ignore))

  static let getViewportSize: obj -> Size option = function
    | :? ScrollViewer as view -> Some <| Size(view.ViewportWidth, view.ViewportHeight)
    | _ -> None

  let setViewSizeToModel sender = 
    getDataContext().bind(getSizePropertySetter).ap(getViewportSize sender)
    |> ignore

  member __.ForwardToImageView(_:obj, e:KeyboardFocusChangedEventArgs) =
    if e.Source.Equals(me) then
      e.Handled <- true
      imageView.Focus() |> ignore

  member __.NotifyViewportChanged(sender: obj, _: SizeChangedEventArgs) = setViewSizeToModel sender
    
  override __.OnActivated _ = setViewSizeToModel imageView