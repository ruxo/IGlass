module iGlass.InstanceManagement

open System
open System.Threading

type Ownership =
  inherit IDisposable
  abstract member IsSingle: bool

let acquireSingleInstance identifier =
  let owned = ref false
  let mutex = new Mutex(initiallyOwned=true, name=identifier, createdNew=owned)

  { new Ownership with
      member x.IsSingle = !owned
      member x.Dispose() = mutex.Dispose() }

let passArgumentsToFirstInstance identifier (arguments: string seq) =
  try
    use client = new System.IO.Pipes.NamedPipeClientStream(identifier)
    use writer = new System.IO.StreamWriter(client)

    client.Connect(200)

    arguments |> Seq.iter writer.WriteLine
    None
  with
  | _ as ex -> Some ex