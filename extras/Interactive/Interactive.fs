(* This is a very basic interactive console for working with an MCU running the Brief firmware.
   This is extreemely useful for debugging and experimenting with new hardware; allowing interactive
   compilation and execution Brief code as well as adding of definitions to the dictionary.

   The full Brief language syntax is available. Additionally, there are several "compile-time" words
   such as connect/disconnect/reset, define/variable, and debug.

   We keep a compile-time stack of lexed/parsed nodes. This can include literals and quotations that
   end up being consumed by following compile-time words. Anything not consumed is assumed to be
   meant for runtime and is compiled. *)

open System
open System.IO
open Bytecode
open Brief

let compiler = new Compiler()
let traceMode = ref false // whether to spew trace info (bytecode, disassembly, etc.)
let comm =
    new Communication(
        (fun e -> printfn "Event: %s" e),
        (fun execute bytecode ->
            if !traceMode then
                printfn "%s:%s\nBytecode (%i): %s\n"
                    (if execute then "Execute" else "Define")
                    (compiler.Disassemble(bytecode))
                    bytecode.Length
                    (new String(bytecode |> Array.map (sprintf "%02x ") |> Seq.concat |> Array.ofSeq))))

(* Here we use the lexer/parser to process lines of Brief code. Most everything is handled by the
   compiler, but several words are intercepted here as "compile-time" words for the interactive:

   Connecting to, disconnecting from and resetting the MCU can be done as follows. You must connect
   to an MCU before executing anything or issuing definitions. The 'connect' word expects a
   quotation (on the compiler-stack) containing a single word specifying the COM port. This sounds
   strange to use undefined words such as "com16", but remember that this is consumed by the
   interactive at compile-time. There need not be any such words in the dictionary. Examples:

       'com16 connect
       'com8 conn

       disconnect

       reset

    Definitions are added by the following form:

        [foo bar] 'baz define

    That is, a compile-time quotation containing the definition, followed by a quotation containing
    a single (not necessarily defined word) usually abreviated with a tick, but [baz] is equally
    valid. These two compile-time arguments are followed by define or def. Examples:

        [dup *] 'square def
        [dup 0 < 'neg if] 'abs define

    Variables are really just defined words that push the address of a two-byte slot of memory taken
    from dictionary space. They are intended to then be used with the fetch (@) and store (!) words.
    Here we use a little trick of a definition containing simply [0]. This will compile to a
    quotation (which pushed the address of the contained code) containing a simple literal; the code
    for which happens to be two bytes. This two-byte value is used as writable memory.

    Variables take a compile-time single-word quotation giving the name. Examples:

        'foo variable
        'bar var

    Remember that these words now push the address of the two-byte slot. The can be used in
    combination with fetch (@) to retrieve the value of the variable:

        foo @
        bar @

    They can be used along with literals (or any calculated value already on the stack) and the
    store (!) word to set the value of the variable:

        123 foo !
        0 analogRead bar !

    Code may be loaded from a file with the load word. This accepts a single quotation containing
    the file path. It may be an absolute path or otherwise is relative to the working directory:

        'foo.txt load
        'c:\temp\test.txt load

    Commented lines may begin with backslash (\).

    Debugging mode may be toggled in which disassembly and raw bytecode are displayed. For example,
    the following interactive session defining and using words to turn on/off the built-in LED on
    the Teensy:

        > 'com16 conn
        > trace
        Debug mode: true

        > 11 output pinMode
        Execute: 11 1 pinMode
        Bytecode (5): 01 0b 01 01 3a

    You can see that "output" disassembles to a literal 1, and that a total of five bytes is sent
    down to the MCU.

        > [high 11 digitalWrite] 'ledOn def
        > [low 11 digitalWrite] 'ledOff def

    Then we define a pair of words to turn the LED on/off. Notice though that nothing at all is sent
    down to the MCU! The bytecode is lazily defined upon first use:

        > ledOn
        Define: -1 11 digitalWrite (return)
        Bytecode (6): 01 ff 01 0b 3c 00

        Execute: ledOn
        Bytecode: (2): 80 00

        >ledOff
        Define: 0 11 digitalWrite (return)
        Bytecode (6): 01 00 01 0b 3c 00

        Execute: ledOff
        Bytecode (2): 80 06

    The disassembly of the definitions shows the trailing "(return)" instruction and the fact that
    "high"/"low" are translated to literals -1/0. In the bytecode, 01 is apparently the lit8
    instruction followed by the values. The 3c bytecode is apparently digitalWrite and 00 is return.
    
    Upon executing "ledOn", we can see that the 6-byte definition is sent down followed by a 2-byte
    call to it. The same thing happens for "ledOff". Now the second time we execute these words:

        > ledOn
        Execute: ledOn
        Bytecode (2): 80 00

        > ledOff
        Execute: ledOff
        Bytecode (2): 80 06

    We can clearly see that only the 2-byte calls need to be send as the definitions are already in
    the dictionary at the MCU. All of these interesting mechanics and the raw and disassembled
    bytecode can be seen with tracing on. *)

let rec rep line =
    let reset () = comm.SendBytes(true, compiler.EagerCompile("(reset)") |> fst)
    let p = line |> parse
    let rec rep' stack = function
        | Token tok :: t ->
            match tok with
            | "connect" | "conn" ->
                match stack with
                | [Quotation [Token com]] :: stack' ->
                    printfn "Connecting to %s" com
                    comm.Connect(com)
                    reset ()
                    rep' stack' t
                | _ -> failwith "Malformed connect syntax - usage: '7 connect"
            | "disconnect" ->
                comm.Disconnect()
                rep' stack t
            | "reset" ->
                reset ()
                compiler.Reset()
                rep' stack t
            | "define" | "def" ->
                match stack with
                | [Quotation [Token name]] :: [Quotation def] :: stack' ->
                    compiler.Define(name, compiler.LazyAssemble(def))
                    rep' stack' t
                | _ -> failwith "Malformed definition syntax - usage: [foo bar] 'baz define"
            | "instruction" ->
                match stack with
                | [Quotation [Token name]] :: [Number code] :: stack' ->
                    compiler.Instruction(name, byte code)
                    rep' stack' t
                | _ -> failwith "Malformed instruction definition - usage: 123 'foo instruction"
            | "variable" | "var" ->
                match stack with
                | [Quotation [Token name]] :: stack' ->
                    compiler.Define(name, compiler.LazyCompile("[(return)]"))
                    rep' stack' t
                | _ -> failwith "Malformed variable syntax - usage: 'foo variable"
            | "load" ->
                match stack with
                | [Quotation [Token path]] :: stack' ->
                    use file = File.OpenText path
                    file.ReadToEnd().Split '\n'
                    |> Array.iter (fun line ->
                        printfn "  %s" line
                        rep line)
                    rep' stack' t
                | _ -> failwith "Malformed load syntax - usage: 'foo.txt load"
            | "\\" -> rep' stack []
            | "." -> rep' stack (Number (int16 0xF0uy) :: Token "event" :: t)
            | "prompt" ->
                Console.ReadLine() |> ignore
                rep' stack t
            | "trace" ->
                traceMode := not !traceMode
                printfn "Trace mode: %b" !traceMode
                rep' stack t
            | "memory" | "mem" ->
                printfn "Memory used: %i bytes" compiler.Address
                rep' stack t
            | "go" ->
                traceMode := true
                printfn "Trace mode: %b" !traceMode
                rep' stack [Quotation [Token "com4"]; Token "conn"; Quotation [Token "test.b"]; Token "load"]
            | "exit" ->
                comm.Disconnect()
                failwith "exit"
            | _ -> rep' ([Token tok] :: stack) t
        | node :: t -> rep' ([node] :: stack) t
        | [] -> List.rev stack
    let exe, def = compiler.EagerAssemble(rep' [] p |> List.concat)
    if def.Length > 0 then comm.SendBytes(false, def)
    if exe.Length > 0 then comm.SendBytes(true, (Array.concat [exe; [|0uy|]]))

let rec repl () =
    printf "\n> "
    try
        Console.ReadLine() |> rep
        repl ()
    with ex ->
        if ex.Message <> "exit" then
            printfn "Error: %s" ex.Message
            repl ()

printfn "Welcome to Brief"
repl ()
