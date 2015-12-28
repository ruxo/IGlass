module iGlass.Views

open System.Windows
open RZ.Wpf.CodeBehind

type MainWindow() as me =
  inherit Window()
  
  do me.InitializeCodeBehind("MainWindow.xaml")
