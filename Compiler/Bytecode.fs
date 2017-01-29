module Bytecode

open System

(* Below is everything having to do with Brief bytecode, AST, assembly, disassembly and lexing,
   parsing and compiling from source. Also the host-side dictionary.

   Below are all of the base Brief instructions. Any user-defined functions will be (User byte).
   Notice that most of them have no operands; instead taking parameters from the stack. The
   exceptions are literals, branches, and Quote. A Word is a 16-bit subroutine address. The User
   type is for user-defined instructions.

   To understand the instruction set, refer to the VM implementation:

       RDK\Firmware\Teensy\libraries\Brief\Brief.cpp/h *)

type Instruction =
    | Literal    of int16 // becomes lit8/16
    | Branch     of sbyte
    | ZeroBranch of sbyte
    | Quote      of byte
    | Return
    | EventHeader | EventBody8 | EventBody16 | EventFooter | Event
    | Fetch8 | Store8
    | Fetch16 | Store16
    | Add | Subtract | Multiply | Divide | Modulus
    | And | Or | ExclusiveOr
    | Shift
    | Equal | NotEqual | Greater | GreaterOrEqual | Less | LessOrEqual
    | Not
    | Negate
    | Increment | Decrement
    | Drop | Duplicate | Swap | Pick | Roll | Clear
    | Push | Pop | Peek
    | Forget
    | Alloc | Free | Tail
    | Local | LocalFetch16 | LocalStore16
    | Call
    | Choice | If
    | LoopTicks
    | SetLoop | StopLoop
    | Reset
    | PinMode
    | DigitalWrite | DigitalRead
    | AnalogWrite | AnalogRead
    | AttachISR | DetachISR
    | Milliseconds
    | PulseIn
    | Word of int16 * string
    | User of byte // user defined instruction
    | NoOperation

(* We maintain mappings from Word names, and optionally Brief instructions to bytecode. Definitions
   exist host-side (PC) initially. This is why Code is a Lazy<byte array>. Only upon first use are
   they reified. We want to allow libraries of host-side definitions with a "pay as you go" model.
   At that point, long definitions are sent down to the MCU and the reified form becomes a two-byte
   call. The address of the call is specific to the MCU. Another reason for MCU-specificity is that
   bytecode values may change depending on the order in which they're bound.

   A "dictionary" is a simple Definition list. Various helper functions are provided to search the
   dictionary and to add new definitions. Defintions may shadow existing ones (last one
   defined becomes the first one found).

   Upon lookup, these definitions may be simply returned as is, which is what happens when they are
   very short. A call is two bytes, so there is no reason to add definitions at the MCU for bytecode
   sequences <= 2 bytes. This is the case for definitions mapped directly to primitives (e.g.
   PinMode, DigitalWrite, etc.) and is true for aliases to 'call's already defined or other very
   short definitions (e.g. [dup *] 'sq define) which are always inlined. See 'shrink' and
   'lazyGenerate' below to understand how lazy thunks are created.

   If a definition is a longer sequence then it is sent down to the MCU as a definition and lookup
   returns a 2-byte call instead of the sequence. This call address is specific to the MCU on which
   it's defined, so lookup takes a dictionary instance owned by a particular MCU. The Brief compiler
   uses this mechanism to resolve word definitions. *)

type Definition = {
    Brief : Instruction option    // Brief instruction (optional)
    Word  : string                // Brief word name
    Code  : Lazy<byte array> }    // on-demand code generator

let find pred dict = List.tryFind pred (!dict)

let findBrief brief = find (fun d -> d.Brief = Some brief)
let findWord  word  = find (fun d -> d.Word  = word)
let findCode  code  = find (fun d -> d.Code.IsValueCreated && d.Code.Value = code)

let codeToWord dict call =
    match findCode call dict with
    | Some def -> def.Word
    | None -> failwith "Unrecognized bytecode sequence."

let define dict brief word token code =
    dict :=
        { Brief = brief
          Word  = word
          Code  = code } :: !dict

(* Below is the Brief assembler. Here we convert Brief instruction sequences to bytecode. It's a
   pretty straightforward process. Notice that Literals become either two or three bytes depending
   on the value. Future optimizations may include specific single-byte instructions for certain
   values. Words become two-byte calls with the high bit set. There is a Call instruction but this
   is for taking an address from the stack. Instead, this high-bit-scheme makes for very efficiently
   packed subroutine threaded code.

   In idiomatic Brief code, there are no branches. Instead we make use of quotations (the Quote
   instruction) and Choice and If for conditionals. This mechanism, along with subroutine calls,
   is all that is needed for a fully expressive language. *)

let assembleBriefInstruction dict = function
    | Literal x ->
        if x >= -128s && x <= 127s then [1uy; byte x] // Lit8 x
        else [2uy; x >>> 8 |> byte; byte x] // Lit16 x
    | Branch x     -> [3uy; byte x]
    | ZeroBranch x -> [4uy; byte x]
    | Quote x      -> [5uy; byte x]
    | Word (x, _)  -> [byte (x >>> 8) ||| 0x80uy; byte x]
    | NoOperation  -> []
    | User x       -> [x]
    | brief        ->
        match findBrief brief dict with
        | Some def -> def.Code.Force() |> List.ofArray
        | None -> failwith "Unrecognized Brief bytecode"

let assembleBrief dict = List.map (assembleBriefInstruction dict) >> List.concat

(* For debugging and diagnostics, it is often useful to convert raw bytecode back to a list of
   Brief instructions. For subroutine calls, we even look up the name in the dictionary. We also
   provide a simple pretty-printer. *)

let disassembleBrief dict bytecode =
    let rec disassemble dis b =
        let recurse t d = disassemble (d :: dis) t
        let unpackInt16 a b = (int16 a <<< 8 ||| int16 b)
        match b with
        |  1uy :: x      :: t -> Literal (x |> sbyte |> int16) |> recurse t
        |  2uy :: a :: b :: t -> Literal (unpackInt16 a b)     |> recurse t
        |  3uy :: x      :: t -> Branch (sbyte x |> int8)      |> recurse t
        |  4uy :: x      :: t -> ZeroBranch (sbyte x)          |> recurse t
        |  5uy :: x      :: t -> Quote (byte x)                |> recurse t
        | a :: b :: t when a &&& 0x80uy <> 0uy -> // call
            let addr = unpackInt16 (a &&& 0x7Fuy) b
            let word = codeToWord dict [|a; b|]
            Word (addr, word) |> recurse t
        | bytecode :: t ->
            (match findCode [|bytecode|] dict with
            | Some def ->
                match def.Brief with
                | Some brief -> brief
                | None -> User bytecode
            | None -> User bytecode) |> recurse t
        | [] -> List.rev dis
    bytecode |> List.ofArray |> disassemble []

let printBrief dict b = // Brief to 'words'
    let rec print = function
        | Literal x      -> sprintf "%i" x
        | Branch x       -> sprintf "(branch%i)" x
        | ZeroBranch x   -> sprintf "(0branch%i)" x
        | Quote x        -> sprintf "(quote%i)" x
        | Word (_, name) -> name
        | NoOperation    -> failwith "NoOperation should not exist in assembled code"
        | User x         ->
            match findCode [|x|] dict with
            | Some def -> def.Word
            | None -> sprintf "(user%i)" x
        | brief          ->
            match findBrief brief dict with
            | Some def -> def.Word
            | None -> sprintf "(unknown-%A)" brief
    b |> List.map print

(* Below is everything needed to lex/parse/compile Brief source.

   The lexer is quite simple! For the most part, tokens are plainly whitespace separated. The
   exception to this is square brackets for quotations and a small bit of syntactic sugar allowing
   a tick mark to quote single tokens. Square brackets become separate tokens (even if not space
   separated) and ' followed by a token expands as if the token were surrounded by square brackets.

   For example:

       foo bar 123
       Becomes three tokens "foo", "bar", "123".

       foo [bar] 123
       Becomes five tokens "foo", "[", "bar", "]", "123".

       foo 'bar 123
       Becomes the same thing with 'bar expanding to "[", "bar", "]". *)

let lex source =
    let rec lex' quote token source = seq {
        let emitToken token = seq { // emit word or [word] if quote
            let tokenToString = List.fold (fun s c -> c.ToString() + s) ""
            if List.length token > 0 then
                if quote then yield "["
                yield tokenToString token
                if quote then yield "]"
            elif quote then failwith "Syntax error: Dangling tick" }
        match source with
        | c :: t when Char.IsWhiteSpace c -> // whitespace delimeted tokens
            yield! emitToken token
            yield! lex' false [] t
        | ('[' as c) :: t | (']' as c) :: t -> // brackets separate token
            if quote then failwith "Syntax error: '[ or ']"
            yield! emitToken token
            yield c.ToString()
            yield! lex' false [] t
        | '\'' :: t -> // quote next token: 'foo becomes [foo] for example
            if quote then failwith "Syntax error: ''"
            yield! emitToken token
            yield! lex' true [] t
        | c :: t -> yield! lex' quote (c :: token) t
        | [] -> yield! emitToken token }
    source |> List.ofSeq |> lex' false []

(* The parser takes a sequence of tokens (from the lexer) and gives them some semantic meaning.
   Square bracket surrounded tokens become a single Quotation node with the surrounded tokens
   parsed as a child Node list. Tokens which can be parsed as an Int16 become Numbers. Special
   syntax is allowed for literal subroutine call addresses in the form "(123)".

   Note that there is no guarantee that output from the pretty-printer can be "round tripped"
   through the lexer/parser. *)

type Node =
    | Token of string
    | Address of int16
    | Number of int16
    | Quotation of Node list

let parse source =
    let rec parse' nodes source =
        match source with
        | "[" :: t ->
            let q, t' = parse' [] t
            parse' (Quotation q :: nodes) t'
        | "]" :: t -> List.rev nodes, t
        | [] -> List.rev nodes, []
        | token :: t ->
            let isNum, num = Int16.TryParse token
            let len = token.Length
            if isNum then parse' (Number num :: nodes) t // 123 becomes Number
            elif len >= 3 && token.[0] = '(' && token.[len - 1] = ')' then
                match token.Substring(1, token.Length - 2) |> Int16.TryParse with
                | true, addr -> parse' (Address addr :: nodes) t // (123) becomes Call
                | false, _ -> parse' (Token token :: nodes) t // not a call
            else parse' (Token token :: nodes) t // otherwise remains a Token
    source |> lex |> List.ofSeq |> parse' [] |> fst // TODO: unmatched brackets

(* Below is the assembler, taking parsed syntax trees (Node) and producing the final bytecode.
   Tokens are looked up in the dictionary and are *eagerly* reified. Addresses and Numbers are
   assembled straightforwardly. Quotations have a special case when they contain a single Word.
   In this case, we emit the Word address directly rather than a Quote 1 Word Return; saving a few
   bytes and also making expressions like 'foo setLoop valid for immediate execution (otherwise
   you'd be setting a temporarily allocated anonymous quotation address as the loop word. *)

let eagerAssemble dict parsed =
    let rec assemble' bytecode = function
        | Token tok :: t ->
            match findWord tok dict with
            | Some word ->
                let code = word.Code.Force() |> List.ofSeq
                assemble' (code :: bytecode) t
            | None -> sprintf "Unrecognized token: %s" tok |> failwith
        | Address addr :: t ->
            let call = [Word (int16 addr, "")] |> assembleBrief dict
            assemble' (call :: bytecode) t // TODO: address to name
        | Number n :: t -> assemble' (assembleBrief dict [Literal n] :: bytecode) t
        | Quotation quote :: t ->
            let q = assemble' [] quote
            match disassembleBrief dict q with
            | [Word (addr, _)] -> // special case for single secondary
                assemble' (assembleBrief dict [Literal addr] :: bytecode) t // emit address directly
            | _ ->
                let q' = assembleBrief dict [Quote (1 + Array.length q |> byte)]
                let ret = assembleBrief dict [Return]
                assemble' (ret :: (q' @ List.ofArray q) :: bytecode) t
        | [] -> bytecode |> List.rev |> List.concat |> Array.ofList
    assemble' [] parsed

let eagerCompile dict = parse >> eagerAssemble dict

(* The essence of subroutine threaded code is that long sequences of code are aggressively factored
   out into definitions and replaced with two-byte calls. Lazily generated definition are "shrunken"
   in this way upon first use. They are assumed to be sent down to the MCU as a definition at the
   given address (addr argument).

   There is no need to shrink code that is already only two bytes (not including the Return
   instruction). In this case we return it as is to be inlined. This allows small definitions such
   as aliases for individual Brief instructions, for 8-bit numbers or or tiny definitions such as
   [dup *] 'sq define to be made with no cost. Asside from this, the stack machine mechanics of the
   VM make subroutine calls extreemely light-weight so relentless factoring is highly encouraged. *)

let shrink dict addr (code : byte array) =
    let ret = assembleBriefInstruction dict Return |> Array.ofList
    let len = code.Length
    if len = 0 then [||], addr, [||] // empty
    elif len <= 2 then code.[0..len-1], addr, [||] // inline
    else [|addr >>> 8 |> byte ||| 0x80uy; byte addr|], addr + len + 1, Array.append code ret

(* We can't eagerly reify definitions because they may depend on other definitions that have yet to
   be shrunken (which implies sending them down to the MCU). We could easily cause a cascading effect
   in which many definitions suddenly need to be reified in order to know their addresses to embed as
   calls.

   To avoid this, as we've talked about in the dictionary mechanics above, we store bytecode in the
   dictionary as a Lazy<byte array>. Forcing these lazy values causes compilation, assembly at that
   moment. We call the compiler/assembler/translator function a 'generator', a unit -> byte array
   function. *)

let lazyGenerate dict generator address pending = lazy (
    let code, addr, def = generator () |> shrink dict !address
    address := addr
    pending := Seq.append !pending def
    code)

let lazyCompile dict source = lazyGenerate dict (fun () -> eagerCompile dict source)

let lazyAssemble dict ast = lazyGenerate dict (fun () -> eagerAssemble dict ast)

(* Below is a function to initialize a dictionary with mappings for all of the Brief primitives
   as well as a library of useful words which can be thought of as being part of the language. *)

let initDictionary dict address pending =
    let defineBytecode (b, w, c) = define dict (Some b) w None (lazy [|byte c|])
    List.iter defineBytecode
        [Return,                "(return)",              0   //             -  (from return)
         EventHeader,           "event{",                6   // id          -
         EventBody8,            "cdata",                 7   // val         -
         EventBody16,           "data",                  8   // val         -
         EventFooter,           "}event",                9   //             -
         Event,                 "event",                 10  // val id      -
         Fetch8,                "c@",                    11  // addr        - val
         Store8,                "c!",                    12  // val addr    -
         Fetch16,               "@",                     13  // addr        - val
         Store16,               "!",                     14  // val addr    -
         Add,                   "+",                     15  // y x         - sum
         Subtract,              "-",                     16  // y x         - diff
         Multiply,              "*",                     17  // y x         - prod
         Divide,                "/",                     18  // y x         - quot
         Modulus,               "mod",                   19  // y x         - rem
         And,                   "and",                   20  // y x         - result
         Or,                    "or",                    21  // y x         - result
         ExclusiveOr,           "xor",                   22  // y x         - result
         Shift,                 "shift",                 23  // x bits      - result
         Equal,                 "=",                     24  // y x         - pred
         NotEqual,              "<>",                    25  // y x         - pred
         Greater,               ">",                     26  // y x         - pred
         GreaterOrEqual,        ">=",                    27  // y x         - pred
         Less,                  "<",                     28  // y x         - pred
         LessOrEqual,           "<=",                    29  // y x         - pred
         Not,                   "not",                   30  // x           - result
         Negate,                "neg",                   31  // x           - -x
         Increment,             "1+",                    32  // x           - x+1
         Decrement,             "1-",                    33  // x           - x-1
         Drop,                  "drop",                  34  // x           -
         Duplicate,             "dup",                   35  // x           - x x
         Swap,                  "swap",                  36  // y x         - x y
         Pick,                  "pick",                  37  // n           - val
         Roll,                  "roll",                  38  // n           -
         Clear,                 "clear",                 39  //             -
         Push,                  "push",                  40  // x           -   (to return)
         Pop,                   "pop",                   41  //             - x (from return)
         Peek,                  "peek",                  42  //             - x (from return)
         Forget,                "forget",                43  // addr        -
         Alloc,                 "(alloc)",               44  // len         -  (to return)
         Free,                  "(free)",                45  //             -  (from return)
         Tail,                  "(tail)",                46  //             -  (from/to return)
         Local,                 "(local)",               47  // index       - addr
         LocalFetch16,          "(local@)",              48  // index       - val
         LocalStore16,          "(local!)",              49  // val index   -
         Call,                  "call",                  50  // addr        -
         Choice,                "choice",                51  // q p         -
         If,                    "if",                    52  // q           -
         LoopTicks,             "loopTicks",             53  //             -
         SetLoop,               "setLoop",               54  // addr        -
         StopLoop,              "stopLoop",              55  //             -
         Reset,                 "(reset)",               56  //             -
         PinMode,               "pinMode",               57  // mode pin    -
         DigitalRead,           "digitalRead",           58  // pin         - val
         DigitalWrite,          "digitalWrite",          59  // val pin     -
         AnalogRead,            "analogRead",            60  // pin         - val
         AnalogWrite,           "analogWrite",           61  // val pin     -
         AttachISR,             "attachISR",             62  // addr i mode -
         DetachISR,             "detachISR",             63  // i           -
         Milliseconds,          "milliseconds",          64  //             - millis
         PulseIn,               "pulseIn",               65] // val pin     - duration

    let library (w, d) = lazyCompile dict d address pending |> define dict None w None
    List.iter library
        ["square"       , "dup *"
         "cube"         , "dup dup * *"
         "over"         , "1 pick"
         "rot"          , "2 roll"
         "-rot"         , "rot rot"
         "nip"          , "swap drop"
         "tuck"         , "swap over"
         "abs"          , "dup 0 < 'neg if"
         "2dup"         , "over over"
         "min"          , "2dup > 'swap if drop"
         "max"          , "2dup < 'swap if drop"
         "nor"          , "or not"
         "xnor"         , "xor not"
         "+!"           , "dup push @ + pop !"
         "-!"           , "dup push @ swap - pop !"
         "clamp"        , "dup neg rot max min"
         "sign"         , "-1 max 1 min" // 1 clamp
         "true"         , "-1"
         "high"         , "-1"
         "on"           , "-1"
         "false"        , "0"
         "low"          , "0"
         "off"          , "0"
         "input"        , "0"
         "output"       , "1"
         "change"       , "1"
         "falling"      , "2"
         "rising"       , "3"
         "lastms"       , "'(return)" // variable
         "ms"           , "milliseconds 32767 and" // wrapping at 32767 instead of going negative
         "ellapsed"     , "ms lastms @ - abs"
         "resetEllapsed", "ms lastms !"
         "ontick"       , "ellapsed <= [resetEllapsed call] 'drop choice" // e.g. [foo bar] 10 ontick
         "dip"          , "swap push call pop" // abq-xb
         "when"         , "[[] choice]"
         "unless"       , "[[] swap choice]"
         "apply"        , "[true swap when]"
         "sum"          , "[0 [+] fold]"
         "2drop"        , "[drop drop]"
         "3drop"        , "[drop drop drop]"
         "neg"          , "[0 swap -]"
         "abs"          , "[dup 0 < [neg] when]"
         "nip"          , "[swap drop]"
         "2nip"         , "[[2drop] dip]"
         "over"         , "[[dup] dip swap]"
         "2dup"         , "[over over]"
         "pick"         , "[[over] dip swap]"
         "3dup"         , "[pick pick pick]"
         "dupd"         , "[[dup] dip]"
         "swapd"        , "[[swap] dip]"
         "rot"          , "[swapd swap]"
         "-rot"         , "[rot rot]"
         "2dip"         , "[swap [dip] dip]"
         "3dip"         , "[swap [2dip] dip]"
         "4dip"         , "[swap [3dip] dip]"
         "keep"         , "[dupd dip]"
         "2keep"        , "[[2dup] dip 2dip ]"
         "3keep"        , "[[3dup] dip 3dip ]"
         "bi"           , "[[keep] dip apply ]"
         "2bi"          , "[[2keep] dip apply]"
         "3bi"          , "[[3keep] dip apply]"
         "tri"          , "[[keep] 2dip [keep] dip apply]"
         "2tri"         , "[[2keep] 2dip [2keep] dip apply]"
         "3tri"         , "[[3keep] 2dip [3keep] dip apply]"
         "bi*"          , "[[dip] dip apply]"
         "2bi*"         , "[[2dip] dip apply]"
         "tri*"         , "[[2dip] 2dip [dip] dip apply]"
         "2tri*"        , "[[4dip] 2dip [2dip] dip apply]"
         "bi@"          , "[dup 2dip apply]"
         "2bi@"         , "[dup 3dip apply]"
         "tri@"         , "[dup 3dip dup 2dip apply]"
         "2tri@"        , "[dup 4dip apply]"
         "both?"        , "[bi@ and]"
         "either?"      , "[bi@ or]" ]
