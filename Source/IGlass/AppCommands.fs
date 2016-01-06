namespace iGlass.ViewModels

module AppCommands =
  open System.Windows
  open System.Windows.Input

  let CheckDrop = RoutedUICommand("Check drop item is compatible.", "CheckDrop", typeof<Window>)
  let DropItem = RoutedUICommand("Drop item", "DropItem", typeof<Window>)
  let ZoomChanged = RoutedUICommand("Change zoom mode", "ZoomChanged", typeof<Window>)

