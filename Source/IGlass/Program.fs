module iGlass.Program

open System
open System.IO
open System.Windows
open RZ.Foundation

let private dirExists path = if Directory.Exists path then Some path else None

let private getNativeDllPath() =
  let currentDir = System.Reflection.Assembly.GetExecutingAssembly().Location
                   |> Path.GetDirectoryName
  let cpuArch = if Environment.Is64BitProcess then "x64" else "x86"
  Path.Combine(currentDir, cpuArch) |> dirExists

let private addEnvPath path =
  let current = Environment.GetEnvironmentVariable "PATH" in
  let newPath = sprintf "%s;%s" current path in
  Environment.SetEnvironmentVariable("PATH", newPath)

let private setupEnvironment = getNativeDllPath >> Option.cata id addEnvPath

let getPathParameter = function
| [||] -> None
| x -> Some x.[0]

let runApplication fileOpt =
  let model = ViewModels.MainWindowViewModel()
  let form = Views.MainWindow().Root
  form.DataContext <- model

  fileOpt |> Option.do' model.SelectGallery
  
  Application().Run(form) |> ignore

[<STAThread>]
[<EntryPoint>]
let main argv =
  let appGuid = "{f2a83de1-b9ac-4461-81d0-cc4547b0b27b}"

  setupEnvironment()

  let fileOpt = getPathParameter argv |> Option.bind iGlass.Core.FileDesc.Verify

  let settings = Config.GlobalSetting.getSettings()
  use ownership = InstanceManagement.acquireSingleInstance appGuid

  if ownership.IsSingle || settings.AllowMultipleInstances then
    runApplication fileOpt
  else
    // TODO it's possible that the first instance has been already exited.
    InstanceManagement.passArgumentsToFirstInstance appGuid argv
    |> Option.cata id (fun ex -> printfn "Cannot talk to first instance: %A" ex
                                 runApplication fileOpt)

  0