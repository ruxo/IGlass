module iGlass.Config.GlobalSetting

open System
open Microsoft.Win32
open RZ.Foundation

[<AutoOpen>]
module private Private =
  let hkey = RegistryHive.CurrentUser, @"Software\PhapSoftware\ImageGlass\"

  type RegistrySimpleType =
  | Binary of byte[]
  | DWord of uint32
  | QWord of uint64
  | String of string
  | MultiString of string[]

  let getConfig0(hive, path) key =
    try
      let reg = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(path)
      let value = reg.GetValue(key)
      match reg.GetValueKind(key) with
      | RegistryValueKind.Binary -> Some (Binary <| cast<byte[]> value)
      | RegistryValueKind.DWord -> Some (DWord <| cast<uint32> value)
      | RegistryValueKind.QWord -> Some (QWord <| cast<uint64> value)
      | RegistryValueKind.String
      | RegistryValueKind.ExpandString -> Some (String <| cast<string> value)
      | RegistryValueKind.MultiString -> Some (MultiString <| cast<string[]> value)
      | _ -> None
    with
    | _ -> None

  let getConfigNumber hpath key =
    let toNumber = function
    | DWord x -> Some (x |> Convert.ToInt64)
    | QWord x -> Some (x |> Convert.ToInt64)
    | _ -> None
    in getConfig0 hpath key |> Option.bind toNumber

  let getConfigString hpath key =
    let toString = function
    | String x -> Some [|x|]
    | MultiString x -> Some x
    | _ -> None
    in getConfig0 hpath key |> Option.bind toString

module private String =
  let join del (sArray: string seq) = String.Join(del, sArray)

  let asBoolean: string[] option -> bool = Option.map (join "" >> Boolean.Parse) >> Option.getOrElse (constant false)

type Settings =
  { AllowMultipleInstances: bool }

let getSettings() =
  { AllowMultipleInstances = getConfigString hkey "IsAllowMultiInstances" |> String.asBoolean }