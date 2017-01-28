module IL

open System
open System.Reflection

(* Here is the complete IL instruction set. Names are those used by ILDasm with periods replaced by
   underscore. See: Partition III of the CLI spec:
   
       http://www.ecma-international.org/publications/standards/Ecma-335.htm *)

type IL =
    | Nop | Break | Ret
    | LdArg_0 | LdArg_1 | LdArg_2 | LdArg_3
    | LdLoc_0 | LdLoc_1 | LdLoc_2 | LdLoc_3
    | StLoc_0 | StLoc_1 | StLoc_2 | StLoc_3
    | LdNull
    | LdC_i4_m1 | LdC_i4_0 | LdC_i4_1 | LdC_i4_2 | LdC_i4_3
    | LdC_i4_4 | LdC_i4_5 | LdC_i4_6 | LdC_i4_7 | LdC_i4_8
    | Dup | Pop
    | LdInd_i1 | LdInd_u1
    | LdInd_i2 | LdInd_u2
    | LdInd_i4 | LdInd_u4
    | LdInd_i8 | LdInd_i
    | LdInd_r4 | LdInd_r8
    | LdInd_ref
    | StInd_ref
    | StInd_i1 | StInd_i2 | StInd_i4 | StInd_i8 | StInd_r4 | StInd_r8
    | Add | Sub | Mul | Div | Div_un | Rem | Rem_un
    | Add_ovf | Add_ovf_un | Mul_ovf | Mul_ovf_un | Sub_ovf | Sub_ovf_un
    | And | Or | XOr | Neg | Not
    | ShL | ShR | ShR_un
    | Conv_i1 | Conv_i2 | Conv_i4 | Conv_i8 | Conv_r4 | Conv_r8 | Conv_u4 | Conv_u8 | Conv_r_un
    | Throw | Rethrow | EndFinally
    | Conv_u
    | Conv_ovf_i1_un | Conv_ovf_i2_un | Conv_ovf_i4_un | Conv_ovf_i8_un
    | Conv_ovf_u1_un | Conv_ovf_u2_un | Conv_ovf_u4_un | Conv_ovf_u8_un
    | Conv_ovf_i_un | Conv_ovf_u_un
    | Conv_ovf_i1 | Conv_ovf_u1
    | Conv_ovf_i2 | Conv_ovf_u2
    | Conv_ovf_i4 | Conv_ovf_u4
    | Conv_ovf_i8 | Conv_ovf_u8
    | Conv_u2 | Conv_u1 | Conv_i
    | Conv_ovf_i | Conv_ovf_u
    | LdLen
    | LdElem_i1 | LdElem_u1
    | LdElem_i2 | LdElem_u2
    | LdElem_i4 | LdElem_u4
    | LdElem_i8 | LdElem_i
    | LdElem_r4 | LdElem_r8
    | LdElem_ref
    | StElem_i | StElem_i1 | StElem_i2 | StElem_i4 | StElem_i8 | StElem_r4 | StElem_r8 | StElem_ref
    | CkFinite
    | StInd_i
    | ArgList
    | Ceq | Cgt | Cgt_un | Clt | Clt_un
    | LocalLoc
    | EndFilter
    | CpBlk | InitBlk
    | RefAnyType
    | ReadOnly_ | Volatile_ | Tail_
    | LdC_i4_s     of int8
    | Br_s         of int8
    | BrFalse_s    of int8
    | BrNull_s     of int8
    | BrZero_s     of int8
    | BrTrue_s     of int8
    | BrInst_s     of int8
    | Beq_s        of int8
    | Bge_s        of int8
    | Bgt_s        of int8
    | Ble_s        of int8
    | Blt_s        of int8
    | Bne_un_s     of int8
    | Bge_un_s     of int8
    | Bgt_un_s     of int8
    | Ble_un_s     of int8
    | Blt_un_s     of int8
    | Leave_s      of int8
    | LdArg_s      of uint8
    | LdArga_s     of uint8
    | StArg_s      of uint8
    | LdLoc_s      of uint8
    | LdLoca_s     of uint8
    | StLoc_s      of uint8
    | Unaligned_   of uint8
    | Jmp          of int32 // token
    | Call         of int32 // token
    | Calli        of int32 // token
    | LdC_i4       of int32
    | Br           of int32
    | BrFalse      of int32
    | BrTrue       of int32
    | Beq          of int32
    | Bge          of int32
    | Bgt          of int32
    | Ble          of int32
    | Blt          of int32
    | Bne_un       of int32
    | Bge_un       of int32
    | Bgt_un       of int32
    | Ble_un       of int32
    | Blt_un       of int32
    | CallVirt     of int32 // method
    | CpObj        of int32 // type
    | LdObj        of int32 // type
    | IsInst       of int32 // type
    | Unbox        of int32 // type
    | LdFld        of int32 // field
    | LdFlda       of int32 // field
    | StFld        of int32 // field
    | LdsFld       of int32 // field
    | LdsFlda      of int32 // field
    | StsFld       of int32 // field
    | StObj        of int32 // field
    | Box          of int32 // type
    | NewArr       of int32 // type
    | LdElema      of int32 // type
    | LdElem_any   of int32 // type
    | StElem_any   of int32 // type
    | Unbox_any    of int32 // type
    | RefAnyVal    of int32 // type
    | MkRefAny     of int32 // type
    | LdToken      of int32 // type/field/method
    | Leave        of int32
    | NewObj       of int32 // method
    | CastClass    of int32 // type
    | LdFtn        of int32 // method
    | LdVirtFtn    of int32 // method
    | InitObj      of int32 // type
    | Constrained_ of int32 // type
    | LdStr        of int32 // string token
    | SizeOf       of int32 // type
    | LdArg        of uint32
    | LdArga       of uint32
    | StArg        of uint32
    | LdLoc        of uint32
    | LdLoca       of uint32
    | StLoc        of uint32
    | LdC_i8       of int64
    | LdC_r4       of float32
    | LdC_r8       of float
    | Switch       of int32 array

(* Dissassembly of an IL byte list is rather straightforward. Unlike Brief bytecode, many IL
   instructions are followed by operands. Here we essentially bundle the operands into the
   descriminated union type from above. Note that 0xFE prefixes extended instructions.

   We emit a list of address/IL pairs (int * IL tuples). The address is used for branch
   patching during translation to Brief bytecode. *)

let disassembleIL il =
    let rec disassemble dis il =
        let zeroParam addr inst = disassemble ((addr, inst) :: dis)
        let int8Param addr inst = function
            | (_, a) :: t -> disassemble ((addr, inst (int8 a)) :: dis) t
            | _ -> failwith "Expected int8 parameter"
        let uint8Param addr inst = function
            | (_, a) :: t -> disassemble ((addr, inst (uint8 a)) :: dis) t
            | _ -> failwith "Expected int8 parameter"
        let int32Param addr inst = function
            | (_, a) :: (_, b) :: (_, c) :: (_, d) :: t ->
                let i32 = BitConverter.ToInt32([|a; b; c; d|], 0)
                disassemble ((addr, inst i32) :: dis) t
            | _ -> failwith "Expected int32 parameter"
        let uint32Param addr inst = function
            | (_, a) :: (_, b) :: (_, c) :: (_, d) :: t ->
                let i32 = BitConverter.ToUInt32([|a; b; c; d|], 0)
                disassemble ((addr, inst i32) :: dis) t
            | _ -> failwith "Expected int32 parameter"
        let int64Param addr inst = function
            | (_, a) :: (_, b) :: (_, c) :: (_, d) :: (_, e) :: (_, f) :: (_, g) :: (_, h) :: t ->
                let i64 = BitConverter.ToInt64([|a; b; c; d; e; f; g; h|], 0)
                disassemble ((addr, inst i64) :: dis) t
            | _ -> failwith "Expected int64 parameter"
        let float32Param addr inst =  function
            | (_, a) :: (_, b) :: (_, c) :: (_, d) :: t ->
                let f32 = BitConverter.ToSingle([|a; b; c; d|], 0)
                disassemble ((addr, inst f32) :: dis) t
            | _ -> failwith "Expected float32 parameter"
        let float64Param addr inst =  function
            | (_, a) :: (_, b) :: (_, c) :: (_, d) :: (_, e) :: (_, f) :: (_, g) :: (_, h) :: t ->
                let f64 = BitConverter.ToDouble([|a; b; c; d; e; f; g; h|], 0)
                disassemble ((addr, inst f64) :: dis) t
            | _ -> failwith "Expected float64 parameter"
        let switchParam addr inst il =
            failwith "Switches unsupported." // TODO: (uint32=N) + N(int32)
        let invalidCode c = sprintf "Invalid IL byte code: %A" c |> failwith
        match il with
        | (a, 0x00uy) ::                t -> zeroParam    a Nop            t
        | (a, 0x01uy) ::                t -> zeroParam    a Break          t
        | (a, 0x02uy) ::                t -> zeroParam    a LdArg_0        t
        | (a, 0x03uy) ::                t -> zeroParam    a LdArg_1        t
        | (a, 0x04uy) ::                t -> zeroParam    a LdArg_2        t
        | (a, 0x05uy) ::                t -> zeroParam    a LdArg_3        t
        | (a, 0x06uy) ::                t -> zeroParam    a LdLoc_0        t
        | (a, 0x07uy) ::                t -> zeroParam    a LdLoc_1        t
        | (a, 0x08uy) ::                t -> zeroParam    a LdLoc_2        t
        | (a, 0x09uy) ::                t -> zeroParam    a LdLoc_3        t
        | (a, 0x0Auy) ::                t -> zeroParam    a StLoc_0        t
        | (a, 0x0Buy) ::                t -> zeroParam    a StLoc_1        t
        | (a, 0x0Cuy) ::                t -> zeroParam    a StLoc_2        t
        | (a, 0x0Duy) ::                t -> zeroParam    a StLoc_3        t
        | (a, 0x0Euy) ::                t -> uint8Param   a LdArg_s        t
        | (a, 0x0Fuy) ::                t -> uint8Param   a LdArga_s       t
        | (a, 0x10uy) ::                t -> uint8Param   a StArg_s        t
        | (a, 0x11uy) ::                t -> uint8Param   a LdLoc_s        t
        | (a, 0x12uy) ::                t -> uint8Param   a LdLoca_s       t
        | (a, 0x13uy) ::                t -> uint8Param   a StLoc_s        t
        | (a, 0x14uy) ::                t -> zeroParam    a LdNull         t
        | (a, 0x15uy) ::                t -> zeroParam    a LdC_i4_m1      t
        | (a, 0x16uy) ::                t -> zeroParam    a LdC_i4_0       t
        | (a, 0x17uy) ::                t -> zeroParam    a LdC_i4_1       t
        | (a, 0x18uy) ::                t -> zeroParam    a LdC_i4_2       t
        | (a, 0x19uy) ::                t -> zeroParam    a LdC_i4_3       t
        | (a, 0x1Auy) ::                t -> zeroParam    a LdC_i4_4       t
        | (a, 0x1Buy) ::                t -> zeroParam    a LdC_i4_5       t
        | (a, 0x1Cuy) ::                t -> zeroParam    a LdC_i4_6       t
        | (a, 0x1Duy) ::                t -> zeroParam    a LdC_i4_7       t
        | (a, 0x1Euy) ::                t -> zeroParam    a LdC_i4_8       t
        | (a, 0x1Fuy) ::                t -> int8Param    a LdC_i4_s       t
        | (a, 0x20uy) ::                t -> int32Param   a LdC_i4         t
        | (a, 0x21uy) ::                t -> int64Param   a LdC_i8         t
        | (a, 0x22uy) ::                t -> float32Param a LdC_r4         t
        | (a, 0x23uy) ::                t -> float64Param a LdC_r8         t
        | (a, 0x25uy) ::                t -> zeroParam    a Dup            t
        | (a, 0x26uy) ::                t -> zeroParam    a Pop            t
        | (a, 0x27uy) ::                t -> int32Param   a Jmp            t
        | (a, 0x28uy) ::                t -> int32Param   a Call           t
        | (a, 0x29uy) ::                t -> int32Param   a Calli          t
        | (a, 0x2Auy) ::                t -> zeroParam    a Ret            t
        | (a, 0x2Buy) ::                t -> int8Param    a Br_s           t
        | (a, 0x2Cuy) ::                t -> int8Param    a BrFalse_s      t
        | (a, 0x2Duy) ::                t -> int8Param    a BrTrue_s       t
        | (a, 0x2Euy) ::                t -> int8Param    a Beq_s          t
        | (a, 0x2Fuy) ::                t -> int8Param    a Bge_s          t
        | (a, 0x30uy) ::                t -> int8Param    a Bgt_s          t
        | (a, 0x31uy) ::                t -> int8Param    a Ble_s          t
        | (a, 0x32uy) ::                t -> int8Param    a Blt_s          t
        | (a, 0x33uy) ::                t -> int8Param    a Bne_un_s       t
        | (a, 0x34uy) ::                t -> int8Param    a Bge_un_s       t
        | (a, 0x35uy) ::                t -> int8Param    a Bgt_un_s       t
        | (a, 0x36uy) ::                t -> int8Param    a Ble_un_s       t
        | (a, 0x37uy) ::                t -> int8Param    a Blt_un_s       t
        | (a, 0x38uy) ::                t -> int32Param   a Br             t
        | (a, 0x39uy) ::                t -> int32Param   a BrFalse        t
        | (a, 0x3Auy) ::                t -> int32Param   a BrTrue         t
        | (a, 0x3Buy) ::                t -> int32Param   a Beq            t
        | (a, 0x3Cuy) ::                t -> int32Param   a Bge            t
        | (a, 0x3Duy) ::                t -> int32Param   a Bgt            t
        | (a, 0x3Euy) ::                t -> int32Param   a Ble            t
        | (a, 0x3Fuy) ::                t -> int32Param   a Blt            t
        | (a, 0x40uy) ::                t -> int32Param   a Bne_un         t
        | (a, 0x41uy) ::                t -> int32Param   a Bge_un         t
        | (a, 0x42uy) ::                t -> int32Param   a Bgt_un         t
        | (a, 0x43uy) ::                t -> int32Param   a Ble_un         t
        | (a, 0x44uy) ::                t -> int32Param   a Blt_un         t
        | (a, 0x45uy) ::                t -> switchParam  a Switch         t
        | (a, 0x46uy) ::                t -> zeroParam    a LdInd_i1       t
        | (a, 0x47uy) ::                t -> zeroParam    a LdInd_u1       t
        | (a, 0x48uy) ::                t -> zeroParam    a LdInd_i2       t
        | (a, 0x49uy) ::                t -> zeroParam    a LdInd_u2       t
        | (a, 0x4Auy) ::                t -> zeroParam    a LdInd_i4       t
        | (a, 0x4Buy) ::                t -> zeroParam    a LdInd_u4       t
        | (a, 0x4Cuy) ::                t -> zeroParam    a LdInd_i8       t // LdInd_u8 same
        | (a, 0x4Duy) ::                t -> zeroParam    a LdInd_i        t
        | (a, 0x4Euy) ::                t -> zeroParam    a LdInd_r4       t
        | (a, 0x4Fuy) ::                t -> zeroParam    a LdInd_r8       t
        | (a, 0x50uy) ::                t -> zeroParam    a LdInd_ref      t
        | (a, 0x51uy) ::                t -> zeroParam    a StInd_ref      t
        | (a, 0x52uy) ::                t -> zeroParam    a StInd_i1       t
        | (a, 0x53uy) ::                t -> zeroParam    a StInd_i2       t
        | (a, 0x54uy) ::                t -> zeroParam    a StInd_i4       t
        | (a, 0x55uy) ::                t -> zeroParam    a StInd_i8       t
        | (a, 0x56uy) ::                t -> zeroParam    a StInd_r4       t
        | (a, 0x57uy) ::                t -> zeroParam    a StInd_r8       t
        | (a, 0x58uy) ::                t -> zeroParam    a Add            t
        | (a, 0x59uy) ::                t -> zeroParam    a Sub            t
        | (a, 0x5Auy) ::                t -> zeroParam    a Mul            t
        | (a, 0x5Buy) ::                t -> zeroParam    a Div            t
        | (a, 0x5Cuy) ::                t -> zeroParam    a Div_un         t
        | (a, 0x5Duy) ::                t -> zeroParam    a Rem            t
        | (a, 0x5Euy) ::                t -> zeroParam    a Rem_un         t
        | (a, 0x5Fuy) ::                t -> zeroParam    a And            t
        | (a, 0x60uy) ::                t -> zeroParam    a Or             t
        | (a, 0x61uy) ::                t -> zeroParam    a XOr            t
        | (a, 0x62uy) ::                t -> zeroParam    a ShL            t
        | (a, 0x63uy) ::                t -> zeroParam    a ShR            t
        | (a, 0x64uy) ::                t -> zeroParam    a ShR_un         t
        | (a, 0x65uy) ::                t -> zeroParam    a Neg            t
        | (a, 0x66uy) ::                t -> zeroParam    a Not            t
        | (a, 0x67uy) ::                t -> zeroParam    a Conv_i1        t
        | (a, 0x68uy) ::                t -> zeroParam    a Conv_i2        t
        | (a, 0x69uy) ::                t -> zeroParam    a Conv_i4        t
        | (a, 0x6Auy) ::                t -> zeroParam    a Conv_i8        t
        | (a, 0x6Buy) ::                t -> zeroParam    a Conv_r4        t
        | (a, 0x6Cuy) ::                t -> zeroParam    a Conv_r8        t
        | (a, 0x6Duy) ::                t -> zeroParam    a Conv_u4        t
        | (a, 0x6Euy) ::                t -> zeroParam    a Conv_u8        t
        | (a, 0x6Fuy) ::                t -> int32Param   a CallVirt       t
        | (a, 0x70uy) ::                t -> int32Param   a CpObj          t
        | (a, 0x71uy) ::                t -> int32Param   a LdObj          t
        | (a, 0x72uy) ::                t -> int32Param   a LdStr          t
        | (a, 0x73uy) ::                t -> int32Param   a NewObj         t
        | (a, 0x74uy) ::                t -> int32Param   a CastClass      t
        | (a, 0x75uy) ::                t -> int32Param   a IsInst         t
        | (a, 0x76uy) ::                t -> zeroParam    a Conv_r_un      t
        | (a, 0x79uy) ::                t -> int32Param   a Unbox          t
        | (a, 0x7Auy) ::                t -> zeroParam    a Throw          t
        | (a, 0x7Buy) ::                t -> int32Param   a LdFld          t
        | (a, 0x7Cuy) ::                t -> int32Param   a LdFlda         t
        | (a, 0x7Duy) ::                t -> int32Param   a StFld          t
        | (a, 0x7Euy) ::                t -> int32Param   a LdsFld         t
        | (a, 0x7Fuy) ::                t -> int32Param   a LdsFlda        t
        | (a, 0x80uy) ::                t -> int32Param   a StsFld         t
        | (a, 0x81uy) ::                t -> int32Param   a StObj          t
        | (a, 0x82uy) ::                t -> zeroParam    a Conv_ovf_i1_un t
        | (a, 0x83uy) ::                t -> zeroParam    a Conv_ovf_i2_un t
        | (a, 0x84uy) ::                t -> zeroParam    a Conv_ovf_i4_un t
        | (a, 0x85uy) ::                t -> zeroParam    a Conv_ovf_i8_un t
        | (a, 0x86uy) ::                t -> zeroParam    a Conv_ovf_u1_un t
        | (a, 0x87uy) ::                t -> zeroParam    a Conv_ovf_u2_un t
        | (a, 0x88uy) ::                t -> zeroParam    a Conv_ovf_u4_un t
        | (a, 0x89uy) ::                t -> zeroParam    a Conv_ovf_u8_un t
        | (a, 0x8Auy) ::                t -> zeroParam    a Conv_ovf_i_un  t
        | (a, 0x8Buy) ::                t -> zeroParam    a Conv_ovf_u_un  t
        | (a, 0x8Cuy) ::                t -> int32Param   a Box            t
        | (a, 0x8Duy) ::                t -> int32Param   a NewArr         t
        | (a, 0x8Euy) ::                t -> zeroParam    a LdLen          t
        | (a, 0x8Fuy) ::                t -> int32Param   a LdElema        t
        | (a, 0x90uy) ::                t -> zeroParam    a LdElem_i1      t
        | (a, 0x91uy) ::                t -> zeroParam    a LdElem_u1      t
        | (a, 0x92uy) ::                t -> zeroParam    a LdElem_i2      t
        | (a, 0x93uy) ::                t -> zeroParam    a LdElem_u2      t
        | (a, 0x94uy) ::                t -> zeroParam    a LdElem_i4      t
        | (a, 0x95uy) ::                t -> zeroParam    a LdElem_u4      t
        | (a, 0x96uy) ::                t -> zeroParam    a LdElem_i8      t // LdElem_u8 same
        | (a, 0x97uy) ::                t -> zeroParam    a LdElem_i       t
        | (a, 0x98uy) ::                t -> zeroParam    a LdElem_r4      t
        | (a, 0x99uy) ::                t -> zeroParam    a LdElem_r8      t
        | (a, 0x9Auy) ::                t -> zeroParam    a LdElem_ref     t
        | (a, 0x9Buy) ::                t -> zeroParam    a StElem_i       t
        | (a, 0x9Cuy) ::                t -> zeroParam    a StElem_i1      t
        | (a, 0x9Duy) ::                t -> zeroParam    a StElem_i2      t
        | (a, 0x9Euy) ::                t -> zeroParam    a StElem_i4      t
        | (a, 0x9Fuy) ::                t -> zeroParam    a StElem_i8      t
        | (a, 0xA0uy) ::                t -> zeroParam    a StElem_r4      t
        | (a, 0xA1uy) ::                t -> zeroParam    a StElem_r8      t
        | (a, 0xA2uy) ::                t -> zeroParam    a StElem_ref     t
        | (a, 0xA3uy) ::                t -> int32Param   a LdElem_any     t // LdElem same
        | (a, 0xA4uy) ::                t -> int32Param   a StElem_any     t // StElem same
        | (a, 0xA5uy) ::                t -> int32Param   a Unbox_any      t
        | (a, 0xB3uy) ::                t -> zeroParam    a Conv_ovf_i1    t
        | (a, 0xB4uy) ::                t -> zeroParam    a Conv_ovf_u1    t
        | (a, 0xB5uy) ::                t -> zeroParam    a Conv_ovf_i2    t
        | (a, 0xB6uy) ::                t -> zeroParam    a Conv_ovf_u2    t
        | (a, 0xB7uy) ::                t -> zeroParam    a Conv_ovf_i4    t
        | (a, 0xB8uy) ::                t -> zeroParam    a Conv_ovf_u4    t
        | (a, 0xB9uy) ::                t -> zeroParam    a Conv_ovf_i8    t
        | (a, 0xBAuy) ::                t -> zeroParam    a Conv_ovf_u8    t
        | (a, 0xC2uy) ::                t -> int32Param   a RefAnyVal      t
        | (a, 0xC3uy) ::                t -> zeroParam    a CkFinite       t
        | (a, 0xC6uy) ::                t -> int32Param   a MkRefAny       t
        | (a, 0xD0uy) ::                t -> int32Param   a LdToken        t
        | (a, 0xD1uy) ::                t -> zeroParam    a Conv_u2        t
        | (a, 0xD2uy) ::                t -> zeroParam    a Conv_u1        t
        | (a, 0xD3uy) ::                t -> zeroParam    a Conv_i         t
        | (a, 0xD4uy) ::                t -> zeroParam    a Conv_ovf_i     t
        | (a, 0xD5uy) ::                t -> zeroParam    a Conv_ovf_u     t
        | (a, 0xD6uy) ::                t -> zeroParam    a Add_ovf        t
        | (a, 0xD7uy) ::                t -> zeroParam    a Add_ovf_un     t
        | (a, 0xD8uy) ::                t -> zeroParam    a Mul_ovf        t
        | (a, 0xD9uy) ::                t -> zeroParam    a Mul_ovf_un     t
        | (a, 0xDAuy) ::                t -> zeroParam    a Sub_ovf        t
        | (a, 0xDBuy) ::                t -> zeroParam    a Sub_ovf_un     t
        | (a, 0xDCuy) ::                t -> zeroParam    a EndFinally     t // EndFault same
        | (a, 0xDDuy) ::                t -> int32Param   a Leave          t
        | (a, 0xDEuy) ::                t -> int8Param    a Leave_s        t
        | (a, 0xDFuy) ::                t -> zeroParam    a StInd_i        t
        | (a, 0xE0uy) ::                t -> zeroParam    a Conv_u         t
        | (a, 0xFEuy) :: (_, 0x00uy) :: t -> zeroParam    a ArgList        t
        | (a, 0xFEuy) :: (_, 0x01uy) :: t -> zeroParam    a Ceq            t
        | (a, 0xFEuy) :: (_, 0x02uy) :: t -> zeroParam    a Cgt            t
        | (a, 0xFEuy) :: (_, 0x03uy) :: t -> zeroParam    a Cgt_un         t
        | (a, 0xFEuy) :: (_, 0x04uy) :: t -> zeroParam    a Clt            t
        | (a, 0xFEuy) :: (_, 0x05uy) :: t -> zeroParam    a Clt_un         t
        | (a, 0xFEuy) :: (_, 0x06uy) :: t -> int32Param   a LdFtn          t
        | (a, 0xFEuy) :: (_, 0x07uy) :: t -> int32Param   a LdVirtFtn      t
        | (a, 0xFEuy) :: (_, 0x09uy) :: t -> uint32Param  a LdArg          t
        | (a, 0xFEuy) :: (_, 0x0Auy) :: t -> uint32Param  a LdArga         t
        | (a, 0xFEuy) :: (_, 0x0Buy) :: t -> uint32Param  a StArg          t
        | (a, 0xFEuy) :: (_, 0x0Cuy) :: t -> uint32Param  a LdLoc          t
        | (a, 0xFEuy) :: (_, 0x0Duy) :: t -> uint32Param  a LdLoca         t
        | (a, 0xFEuy) :: (_, 0x0Euy) :: t -> uint32Param  a StLoc          t
        | (a, 0xFEuy) :: (_, 0x0Fuy) :: t -> zeroParam    a LocalLoc       t
        | (a, 0xFEuy) :: (_, 0x11uy) :: t -> zeroParam    a EndFilter      t
        | (a, 0xFEuy) :: (_, 0x12uy) :: t -> uint8Param   a Unaligned_     t
        | (a, 0xFEuy) :: (_, 0x13uy) :: t -> zeroParam    a Volatile_      t
        | (a, 0xFEuy) :: (_, 0x14uy) :: t -> zeroParam    a Tail_          t
        | (a, 0xFEuy) :: (_, 0x15uy) :: t -> int32Param   a InitObj        t
        | (a, 0xFEuy) :: (_, 0x16uy) :: t -> int32Param   a Constrained_   t
        | (a, 0xFEuy) :: (_, 0x17uy) :: t -> zeroParam    a CpBlk          t
        | (a, 0xFEuy) :: (_, 0x18uy) :: t -> zeroParam    a InitBlk        t
        | (a, 0xFEuy) :: (_, 0x1Auy) :: t -> zeroParam    a Rethrow        t
        | (a, 0xFEuy) :: (_, 0x1Cuy) :: t -> int32Param   a SizeOf         t
        | (a, 0xFEuy) :: (_, 0x1Duy) :: t -> zeroParam    a RefAnyType     t
        | (a, 0xFEuy) :: (_, 0x1Euy) :: t -> zeroParam    a ReadOnly_      t
        | (a, 0xFEuy) :: (a', c) :: _ -> invalidCode [|a, 0xFEuy; a', c|]
        | (a, c) :: _ -> invalidCode [|a, c|]
        | [] -> List.rev dis
    il |> List.mapi (fun a i -> a, i) |> disassemble []

(* Here's the meat of it. This is where we translate IL instructions to Brief bytecode.

   If you're used to Brief then you know that all arguments are passed through the stack. This is
   the beauty of a pure stack machine. There is no calling convention. There are no stack frames.
   Arguments can flow through the call stack untouched if desired without passing them along
   explicitly. The overhead for a subroutine call is only a simple push of the program counter to
   the return stack and a jump. Returning takes no cleanup; only a pop from the return stack. There
   is also no concept of a "local" in Brief. Again the stack is used exclusively. There are plenty
   of stack manipulation primitives (swap, over, pick, roll, dup, drop, ...) to allow rearrangement
   of the stack. It is possible (though less common) to use the return stack (via pop/push) to tuck
   away values to be used multiple times or to persist after a call/return. You can think of these
   as "locals" in a sense.

   The CLR is different. There are very few stack manipulation primitives in IL (only dup and pop).
   Instead, code generated by the C# and F# compilers makes heavy use of locals (via StLoc* and
   LdLoc). Also, there is a C-like calling convention in the CLR in which arguments are taken from
   the stack and moved to an "arguments" block. The actual format of the call stack is abstracted.
   The IL instructions LdArg* and StArg* give access.

   To support CLR-style arguments and locals in the Brief VM, there are the 'alloc' and 'free'
   instructions (managing blocks of memory taken from the end of dictionary space), along with
   a 'local' instruction indexing into the block (pushing an address to be used by fetch/store).
   There is also a 'tail' Brief instruction to handle tail call optimization - freeing allocated
   space early and causing the free at return-time to do nothing further. Also there are compact
   forms 'localFetch16'/'localStore16' which are single-byte equivalents to 'local' followed by
   'fetch'/'store'. All of this is there only to serve translated IL code. It is not at all
   ideomatic Brief to make use of these.

   As a side note, hand written Brief can be *much* more efficient than translated IL. For example:

       var x = 42;
       var y = 7;
       return x * y;

    Becomes the following IL:

        LdC_i4 42
        StLoc 0
        LdC_i4_7
        StLoc 1
        LdLoc 1
        LdLoc 0
        Mul
        Return

    So silly that the constants are stored in locals only to be loaded back on the stack (in reverse
    order). In Brief it is done without locals (still assuming out-of-order literals):

        Literal 42
        Literal 7
        Swap
        Mul
        Return

    The argCount and locCount is used to allocate space for arguments and locals up front. This
    space is then freed upon return (or earlier if a .tail marker is encountered).

    Calls in IL include a metadata token. These are looked up in the dictionary and reified. A call
    to a method which hasn't been added to the dictionary is a fatal error.

    IL has a small zoo of branch instructions while Brief has only two (Branch, ZeroBranch). Many
    of these become reversed conditionals followed by a ZeroBranch. For example Bge will become
    Less ZeroBranch, and Beq becomes NotEqual ZeroBranch, etc.

    Some IL instructions have no meaning in translation (e.g. break points, actual No-ops, ...).
    Many other instructions are unsupported in the Brief VM. For example, anything dealing with
    64-bit ints, object references, floats/doubles, unsigned values, arrays, strings, ... Also,
    long branches, params style args, indirect calls, overflow checks, switches, ... *)

open Bytecode

let translate dict argCount locCount (meth : MethodInfo) il =
    let heap = (int16 argCount + int16 locCount) * 2s
    let allocStackFrame = seq { // alloc locals/arguments
        if heap > 0s then
            yield -1, Literal (int16 heap)
            yield -1, Alloc
            for i in 0s .. int16 argCount - 1s do
                yield -1, Literal ((int16 locCount + int16 argCount - i - 1s) * 2s)
                yield -1, LocalStore16 } |> List.ofSeq
    let reify name address =
        match findWord name dict with
        | Some def -> def.Code.Force () |> disassembleBrief dict |> List.map (fun b -> address, b)
        | None -> failwith "Unrecognized method name"
    let translate' il =
        match il with
        | a, LdC_i4_0    -> [a, Literal  0s]
        | a, LdC_i4_1    -> [a, Literal  1s]
        | a, LdC_i4_2    -> [a, Literal  2s]
        | a, LdC_i4_3    -> [a, Literal  3s]
        | a, LdC_i4_4    -> [a, Literal  4s]
        | a, LdC_i4_5    -> [a, Literal  5s]
        | a, LdC_i4_6    -> [a, Literal  6s]
        | a, LdC_i4_7    -> [a, Literal  7s]
        | a, LdC_i4_8    -> [a, Literal  8s]
        | a, LdC_i4_m1   -> [a, Literal -1s]
        | a, LdC_i4 x    -> [a, Literal (int16 x)]
        | a, LdC_i4_s  x -> [a, Literal (int16 x)]
        | a, LdLoc_0     -> [a, Literal 0s; a, LocalFetch16]
        | a, StLoc_0     -> [a, Literal 0s; a, LocalStore16]
        | a, LdLoc_1     -> [a, Literal 4s; a, LocalFetch16]
        | a, StLoc_1     -> [a, Literal 4s; a, LocalStore16]
        | a, LdLoc_2     -> [a, Literal 8s; a, LocalFetch16]
        | a, StLoc_2     -> [a, Literal 8s; a, LocalStore16]
        | a, LdLoc_3     -> [a, Literal 12s; a, LocalFetch16]
        | a, StLoc_3     -> [a, Literal 12s; a, LocalStore16]
        | a, StLoc_s   x -> [a, Literal (int16 x * 2s); a, LocalStore16]
        | a, LdLoc_s   x -> [a, Literal (int16 x * 2s); a, LocalFetch16]
        | a, StLoc     x -> [a, Literal (int16 x * 2s); a, LocalStore16]
        | a, LdLoc     x -> [a, Literal (int16 x * 2s); a, LocalFetch16]
        | a, LdArg_0     -> [a, Literal (int16 locCount * 2s); a, LocalFetch16]
        | a, LdArg_1     -> [a, Literal (int16 locCount * 2s + 2s); a, LocalFetch16]
        | a, LdArg_2     -> [a, Literal (int16 locCount * 2s + 4s); a, LocalFetch16]
        | a, LdArg_3     -> [a, Literal (int16 locCount * 2s + 6s); a, LocalFetch16]
        | a, LdArg_s   x -> [a, Literal (int16 locCount * 2s + int16 x * 2s); a, LocalFetch16]
        | a, StArg_s   x -> [a, Literal (int16 locCount * 2s + int16 x * 2s); a, LocalStore16]
        | a, LdArg     x -> [a, Literal (int16 locCount * 2s + int16 x * 2s); a, LocalFetch16]
        | a, StArg     x -> [a, Literal (int16 locCount * 2s + int16 x * 2s); a, LocalStore16]
        | a, Beq_s     x -> [a, NotEqual; a, ZeroBranch x]
        | a, Bge_s     x -> [a, Less; a, ZeroBranch x]
        | a, Bgt_s     x -> [a, LessOrEqual; a, ZeroBranch x]
        | a, Ble_s     x -> [a, Greater; a, ZeroBranch x]
        | a, Blt_s     x -> [a, GreaterOrEqual; a, ZeroBranch x]
        | a, Bne_un_s  x -> [a, Equal; a, ZeroBranch x]
        | a, Br_s      x -> [a, Branch x]
        | a, BrFalse_s x -> [a, ZeroBranch x]
        | a, BrTrue_s  x -> [a, Not; a, ZeroBranch x]
        | a, BrZero_s  x -> [a, ZeroBranch x]
        | a, Ceq         -> [a, Equal]
        | a, Cgt         -> [a, Greater]
        | a, Clt         -> [a, Less]
        | a, IL.Add      -> [a, Add]
        | a, IL.Sub      -> [a, Subtract]
        | a, IL.Mul      -> [a, Multiply]
        | a, IL.Div      -> [a, Divide]
        | a, Rem         -> [a, Modulus]
        | a, IL.And      -> [a, And]
        | a, IL.Or       -> [a, Or]
        | a, IL.XOr      -> [a, ExclusiveOr]
        | a, Neg         -> [a, Negate]
        | a, IL.Not      -> [a, Not]
        | a, ShL         -> [a, Negate; a, Shift]
        | a, ShR         -> [a, Shift]
        | a, Tail_       -> [a, Tail; -1, NoOperation]
        | a, Ret         -> (if heap > 0s then [a, Free] else []) @ [a, Return]
        | a, Dup         -> [a, Duplicate]
        | a, IL.Pop      -> [a, Drop]
        | a, IL.Call t | a, CallVirt t | a, Jmp t -> reify (meth.Module.ResolveMethod(t).Name) a
        | a, LdsFld t -> (reify (meth.Module.ResolveField(t).Name) a) @ [a, Fetch16]
        | a, StsFld t -> (reify (meth.Module.ResolveField(t).Name) a) @ [a, Store16]
        // meaningless:
        | a, Nop | a, Break | a, ReadOnly_ | a, Volatile_
          -> [-1, NoOperation] // filtered out below
        // unsupported:
        | _, LdFld _ | _, StFld _ // no instance fields
        | _, LdC_i8 _ | _, LdC_r4 _ | _, LdC_r8 _ // no 64-bit ints, no floats/doubles
        | _, Beq _ | _, Bge _ | _, Bgt _ | _, Ble _ | _, Blt _ | _, Bne_un _ // no long branches
        | _, Br _ | _, BrFalse _ | _, BrTrue _ // no long branches
        | _, LdNull | _, LdFlda _ | _, LdsFlda _ // no objects
        | _, BrNull_s _ | _, BrInst_s _ // no objects
        | _, Bne_un_s _ | _, Bge_un_s _ | _, Bgt_un_s _
        | _, Ble_un_s _ | _, Blt_un_s _ // no unsigned ints
        | _, Bne_un _ | _, Bge_un _ | _, Bgt_un _ | _, Ble_un _ | _, Blt_un _ // no long branches
        | _, LdInd_i1 | _, LdInd_i2 | _, LdInd_i4 // no indirect load
        | _, LdInd_u1 | _, LdInd_u2 | _, LdInd_u4 // no indirect load
        | _, StInd_i1 | _, StInd_i2 | _, StInd_i4 // no indirect store
        | _, LdInd_i8 | _, LdInd_i | _, LdInd_i2 // no 64-bit ints (or indirect load)
        | _, LdInd_r4 | _, LdInd_r8 // no floats/doubles (or indirect load)
        | _, LdInd_ref | _, StInd_ref // no objects
        | _, StInd_i8 | _, StInd_i // no native or 64-bit ints (or indirect store)
        | _, StInd_r4 | _, StInd_r8 // no floats/doubles (or indirect store)
        | _, Add_ovf | _, Add_ovf_un | _, Mul_ovf | _, Sub_ovf | _, Sub_ovf_un // no overflow checks
        | _, Mul_ovf_un | _, Div_un | _, Rem_un | _, ShR_un // no unsigned ints (or overflow checks)
        | _, Conv_i1 | _, Conv_i2 | _, Conv_i4 | _, Conv_i8 | _, Conv_r4 // no types (only int32)
        | _, Conv_r8| _, Conv_u4 | _, Conv_u8 | _, Conv_r_un | _, Conv_u // no types (only int32)
        | _, Throw | _, Rethrow | _, EndFinally | _, Leave _ | _, Leave_s _ // no exception handling
        | _, Conv_ovf_i1_un | _, Conv_ovf_i2_un | _, Conv_ovf_i4_un
        | _, Conv_ovf_i8_un | _, Conv_ovf_u1_un | _, Conv_ovf_u2_un
        | _, Conv_ovf_u4_un | _, Conv_ovf_u8_un // no types (only int32)
        | _, Conv_ovf_i_un | _, Conv_ovf_u_un // no types (only int32)
        | _, Conv_ovf_i1 | _, Conv_ovf_u1 | _, Conv_ovf_i2 | _, Conv_ovf_u2 // no types (only int32)
        | _, Conv_ovf_i4 | _, Conv_ovf_u4 | _, Conv_ovf_i8 | _, Conv_ovf_u8 // no types (only int32)
        | _, Conv_u2 | _, Conv_u1 | _, Conv_i | _, Conv_ovf_i | _, Conv_ovf_u // no types
        | _, LdElem_i1 | _, LdElem_u1 | _, LdElem_i2 | _, LdElem_u2 // no arrays
        | _, LdElem_i4 | _, LdElem_u4 | _, LdElem_i8 | _, LdElem_i // no arrays
        | _, LdElem_r4 | _, LdElem_r8 | _, LdElem_ref | _, LdLen // no arrays
        | _, StElem_i | _, StElem_i1 | _, StElem_i2 | _, StElem_i4 // no arrays
        | _, StElem_i8 | _, StElem_r4 | _, StElem_r8 | _, StElem_ref // no arrays
        | _, NewArr _ | _, LdElema _ | _, LdElem_any _ | _, StElem_any _ // no arrays
        | _, ArgList // no params style args
        | _, Cgt_un | _, Clt_un // no unsigned ints
        | _, LdArga_s _ | _, LdLoca_s _ // no objects
        | _, CkFinite | _, LocalLoc | _, EndFilter | _, CpBlk | _, InitBlk | _, RefAnyType
        | _, Unaligned_ _ | _, IsInst _ | _, Constrained_ _ | _, SizeOf _ | _, LdToken _
        | _, Calli _ // no indirect calls
        | _, InitObj _ | _, StObj _ | _, CastClass _ | _, NewObj _ | _, Unbox _
        | _, LdArga _ | _, LdLoca _ | _, CpObj _ | _, LdObj _ // no objects
        | _, Box _ | _, Unbox_any _ | _, RefAnyVal _ | _, MkRefAny _ // no objects
        | _, LdStr _ // no strings
        | _, LdFtn _ | _, LdVirtFtn _ // no functions (not this kind anyway)
        | _, Switch _ // no switches
          -> il |> snd |> sprintf "Unsupported instruction (%A)" |> failwith
    let translated = il |> List.map translate' |> List.concat
    allocStackFrame @ translated |> List.filter ((<>) (-1, NoOperation))

let translateMethod dict argCount locCount meth il =
    il |> disassembleIL
    // uncomment the following for disassembly tracing
    |> (fun d -> List.map (snd >> sprintf "%A ") d |> List.fold (+) "" |> printfn "IL: %s"; d)
    |> translate dict argCount locCount meth |> assembleBriefWithBranchPatching dict

(* Ultimately all we need is a MethodInfo and we can extract the IL and everything we need to make
   a proper definition in the Brief dictionary. Once defined, the method can be called from other
   methods being translated and/or from other Brief code (by name). *)

let getMethodIL (meth : MethodInfo) =
    let name = meth.Name
    let token = meth.MetadataToken
    let body = meth.GetMethodBody()
    let locCount = body.LocalVariables.Count
    let argCount = meth.GetParameters().Length
    let il = body.GetILAsByteArray() |> List.ofArray
    name, token, argCount, locCount, il

let eagerTranslate dict (meth : MethodInfo) =
    let _, _, argCount, locCount, il = getMethodIL meth
    try
        translateMethod dict argCount locCount meth il
    with ex -> sprintf "Translation failure (%s) - %s" meth.Name ex.Message |> failwith

let lazyTranslate dict (meth : MethodInfo) = lazyGenerate dict (fun () -> eagerTranslate dict meth)