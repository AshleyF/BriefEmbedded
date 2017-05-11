namespace Brief

open System
open System.IO.Ports
open System.Threading
open Bytecode

(* Below is a class meant to be used in C#-land. As such we give a little object wrapper with
   overloaded methods hiding the option types and such. It holds onto an instance the dictionary,
   the current address and the sequence of pending bytecode to be sent down to the MCU. This
   makes many of the methods simply curried versions of existing functions with these captured.
   In fact, it's interesting to think of objects as really a set of partially-applied functions? :)

   Each of the Eager* methods return a pair of byte arrays (byte[] * byte[] tuple) representing
   the dependent definitions needing to be sent down and the bytecode for the compiled/translated
   code.

   Each of the Lazy* methods return a Lazy<byte[]> which can be reified (as if eager) later with
   the Reify method.

   The Define methods have the side effect of adding lazy definitions to the dictionary. These may
   later be reified implicitly and returned as definitions to send down when depending code is
   reified. *)

open System.Reflection

type Compiler() =
    let dict = ref []
    let address = ref 0
    let pending = ref Seq.empty

    let getPending () =
        let p = !pending |> Array.ofSeq
        pending := Seq.empty; p

    let token (memb : MemberInfo) = Some (memb.Module.FullyQualifiedName, memb.MetadataToken)

    do initDictionary dict address pending

    member x.Reset() = dict := []; address := 0; pending := Seq.empty; initDictionary dict address pending

    member x.EagerCompile(source) = eagerCompile   dict source, getPending ()
    member x.EagerAssemble(ast)   = eagerAssemble  dict ast,    getPending ()

    member x.LazyCompile(source) = lazyCompile   dict source address pending
    member x.LazyAssemble(ast)   = lazyAssemble  dict ast    address pending

    member x.Reify(lazycode : Lazy<byte array>) = lazycode.Force(), getPending ()

    member x.Define(word, code)               = define dict  None        word  None         code
    member x.Define(word, brief, code)        = define dict (Some brief) word  None         code
    member x.Define(word, memb, code)         = define dict  None        word (token memb ) code
    member x.Define(word, brief, meth, code)  = define dict (Some brief) word (token meth ) code

    member x.Instruction(word, code) = define dict None word None (lazy ([|code|]))

    member x.Address = !address

    member x.Disassemble(bytecode) =
        bytecode
        |> disassembleBrief dict
        |> printBrief dict
        |> List.map ((+) " ") |> List.reduce (+)

type Communication(eventFn : Action<string>, traceFn: Action<bool, byte[]>) =
    let (serial : SerialPort option ref) = ref None
    let rec readEvents () =
        let event message = if eventFn <> null then eventFn.Invoke(message)
        match !serial with
        | Some port ->
            if port.IsOpen && port.BytesToRead > 0 then
                let len = port.ReadByte()
                let id = port.ReadByte() |> byte
                let data = Array.create len 0uy
                port.Read(data, 0, len) |> ignore
                let toInt d =
                    match Array.length d with
                    | 0 -> 0s
                    | 1 -> d.[0] |> sbyte |> int16
                    | 2 -> (int16 d.[0] <<< 8) ||| int16 d.[1]
                    | _ -> failwith "Invalid event data."
                match id with
                | id when id = 0xF0uy -> data |> toInt |> sprintf "%i" |> event
                | 0xFFuy -> event "Boot event"
                | 0xFEuy ->
                    sprintf "VM Error: %s"
                        (match data with
                        | [|0uy|] -> "Return stack underflow"
                        | [|1uy|] -> "Return stack overflow"
                        | [|2uy|] -> "Data stack underflow"
                        | [|3uy|] -> "Data stack overflow"
                        | [|4uy|] -> "Out of memory"
                        | _ -> "Unknown") |> event
                | _ -> sprintf "Event (%i): %A" id data |> event
        | None -> ()
        Thread.Sleep(100)
        readEvents ()
    let mutable (readThread: Thread) = null
    member x.Connect(com) =
        let port = new SerialPort(com, 19200)
        serial := Some port
        port.Open()
        port.DiscardInBuffer()
        port.DiscardOutBuffer()
        readThread <- new Thread(readEvents, IsBackground = true)
        readThread.Start()
    member x.Disconnect() =
        match !serial with
        | Some port ->
            port.Close()
            serial := None
            readThread.Abort()
            readThread <- null
        | None -> failwith "Not connected"
    member x.SendBytes(execute, bytecode) =
        let trace () = if traceFn <> null then traceFn.Invoke(execute, bytecode)
        match !serial with
        | Some port ->
            if bytecode.Length > 127 then failwith "Too much bytecode in single packet"
            trace ()
            let header = (byte bytecode.Length ||| if execute then 0x80uy else 0uy)
            port.Write(Array.create 1 header, 0, 1)
            port.Write(bytecode, 0, bytecode.Length)
            port.BaseStream.Flush()
        | None -> failwith "Not connected to MCU."