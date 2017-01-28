namespace Microsoft.Robotics.Brief

open IL
open Bytecode
open System

(* Below is a class meant to be owned by an IMicrocontroller instance in C#-land. As such we give a
   little object wrapper with overloaded methods hiding the option types and such. It holds onto an
   instance the dictionary, the current address and the sequence of pending bytecode to be sent down
   to the MCU. This makes many of the methods simply curried versions of existing functions with
   these captured. In fact, isn't it interesting to think of objects as really a set of partially-
   applied functions? :)

   Each of the Eager* methods return a pair of byte arrays (byte[] * byte[] tuple) representing
   the dependent definitions needing to be sent down and the bytecode for the compiled/translated
   code.

   Each of the Lazy* methods return a Lazy<byte[]> which can be reified (as if eager) later with
   the Reify method.

   The Define methods have the side effect of adding lazy definitions to the dictionary. These may
   later be reified implicitly and returned as definitions to send down when depending code is
   reified.

   Again, Compiler instances are expected to be owned ("has a") by an IMicrocontroller, exposing
   "easier" interfaces to these "simple" internals. *)

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
    member x.EagerTranslate(meth) = eagerTranslate dict meth,   getPending ()
    member x.EagerAssemble(ast)   = eagerAssemble  dict ast,    getPending ()

    member x.LazyCompile(source) = lazyCompile   dict source address pending
    member x.LazyTranslate(meth) = lazyTranslate dict meth   address pending
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