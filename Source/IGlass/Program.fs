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
  let form = Views.MainWindow()
  let model = form.DataContext.cast<ViewModels.MainWindowViewModel>()

  Option.do' model.SelectGallery fileOpt
  
  Application().Run(form)

[<STAThread>]
[<EntryPoint>]
let main argv =
  let appGuid = "{09AA7BFC-BE4D-4621-B72E-A2E6259563F6}"

  setupEnvironment()

  let fileOpt = getPathParameter argv |> Option.bind iGlass.Core.FileDesc.Verify

  let settings = Config.GlobalSetting.getSettings()
  use ownership = InstanceManagement.acquireSingleInstance appGuid

  if ownership.IsSingle || settings.AllowMultipleInstances then
    runApplication fileOpt
  else
    // TODO it's possible that the first instance has been already exited.
    InstanceManagement.passArgumentsToFirstInstance appGuid argv
    |> Option.cata (constant 1)
                   (fun ex -> printfn "Cannot talk to first instance: %A" ex
                              runApplication fileOpt)
