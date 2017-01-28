#include "Brief.h"

namespace brief
{
/*  The Brief VM revolves around a pair of stacks and a block of memory serving as a dictionary of
    subroutines.

    The dictionary is typically 1Kb. This is where Brief byte code is stored and executed. While it
    can technically be used as general purpose memory, the intent is to treat it as a structured
    space for definitions; subroutines, variables, and the like, all contiguously packed.

    The two stacks are each eight elements of 16-bit signed integers. They are used to store data
    and addresses. They are connected in that elements can be popped from the top of one and pushed
    to the top of the other.

    One stack is used as a data stack; persisting values across instructions and subroutine calls.
    With very few exceptions, instructions get their operands only from the data stack. All
    parameter passing between subroutines is done via this stack.

    The other stack is used by the VM as a return stack. The program counter is pushed here before
    jumping into a subroutine and is popped to return. Be careful not to nest subroutines more than
    eight levels deep! Note that infinite tail recursion is possible none the less. */

    // Memory (dictionary)

    void error(uint8_t code); // forward decl

    uint8_t memory[MEM_SIZE]; // dictionary (and local/arg space for IL semantics)

    uint8_t mem(int16_t address) // fetch with bounds checking
    {
        if (address < 0 || address >= MEM_SIZE)
        {
            error(VM_ERROR_OUT_OF_MEMORY);
            return 0;
        }
        else
        {
            return memory[address];
        }
    }

    void memset(int16_t address, uint8_t value) // store with bounds checking
    {
        if (address >= MEM_SIZE)
        {
            error(VM_ERROR_OUT_OF_MEMORY);
        }
        else
        {
            memory[address] = value;
        }
    }

    // Data stack

    int16_t dstack[DATA_STACK_SIZE]; // eval stack (and args in Brief semantics)

    int16_t* s; // data stack pointer

    void push(int16_t x)
    {
        if (s >= dstack + DATA_STACK_SIZE)
        {
            s = dstack - 1;
            error(VM_ERROR_DATA_STACK_OVERFLOW);
        }
        else
        {
            *(++s) = x;
        }
    }

    int16_t pop()
    {
        if (s < dstack)
        {
            s = dstack - 1;
            error(VM_ERROR_DATA_STACK_UNDERFLOW);
        }
        else
        {
            return *s--;
        }
    }

    // Return stack

    int16_t rstack[RETURN_STACK_SIZE]; // return stack (and locals in Brief)

    int16_t* r; // return stack pointer

    void rpush(int16_t x)
    {
        if (r >= rstack + RETURN_STACK_SIZE)
        {
            error(VM_ERROR_RETURN_STACK_OVERFLOW);
        }
        else
        {
            *(++r) = x;
        }
    }

    int16_t rpop()
    {
        if (r < rstack)
        {
            error(VM_ERROR_RETURN_STACK_UNDERFLOW);
        }
        else
        {
            return *r--;
        }
    }

/*  Brief instructions are single bytes with the high bit reset:

      0xxxxxxx

    The lower seven bits become essentially an index into a function table.  Each may consume
    and/or produce values on the data stack as well as having other side effects. Only three
    instructions manipulate the return stack.  Two are push and pop which move values between the
    data and return stack.  The third is (return); popping an address at which execution continues.

    It is extremely common to factor out redundant sequences of code into subroutines. There is no
    "call" instruction. Instead, if the high bit is set then the following byte is taken and
    together (in little endian), with the high bit reset, they become an address to be called.

      1xxxxxxxxxxxxxxx

    This allows 15-bit addressing to definitions in the dictionary.

    Upon calling, the VM pushes the current program counter to the return stack. There is a return
    instruction, used to terminate definitions, which pops the return stack to continue execution
    after the call. */

    void (*instructions[MAX_PRIMITIVES])(); // instruction function table

    void bind(uint8_t i, void (*f)()) // add function to instruction table
    {
        instructions[i] = f;
    }

    int16_t p; // program counter (VM instruction pointer)

    void ret() // return instruction
    {
        p = rpop();
    }

    void run() // run code at p
    {
        int16_t i;
        do
        {
            i = mem(p++);
            if ((i & 0x80) == 0) // instruction?
            {
                instructions[i](); // execute instruction
            }
            else // address to call
            {
                if (mem(p + 1) != 0) // not followed by return (TCO)
                    rpush(p + 1); // return address
                p = ((i << 8) & 0x7F00) | mem(p); // jump
            }
        } while (p >= 0); // -1 pushed to return stack
    }

    void exec(int16_t address) // execute code at given address
    {
        r = rstack - 1; // reset return stack
        p = address;
        rpush(-1); // causing run() to fall through upon completion
        run();
    }

/*  Reflecta handles the framing protocol. This includes the sequence numbers, CRC checks, etc.

    We hook our own frameAllocation function which simply allocates directly from the next available
    space in the dictionary. This means Reflecta will write definitions exactly where they belong
    and we avoid a copy.

    We also hook our own frameReceived function to process bytecode sent down. The last byte of the 
    frame indicates whether it is to be executed immediately (0) or is a definition (1) to remain in
    the dictionary.

    The payload from the PC to the MCU is in the form of Brief code. A trailing byte indicates
    whether the code is to be executed immediately (0x00) or appended to the dictionary as a new
    definition (0x01).

    A dictionary pointer is maintained at the MCU. This pointer always references the first
    available free byte of dictionary space (beginning at address 0x0000). Each definition sent to
    the MCU is appended to the end of the dictionary and advances the pointer. The bottom end of the
    dictionary space is used for locals/arguments (mainly for IL, not idiomatic Brief).

    If code is a definition then it is expected to already be terminated by a return instruction (if
    appropriate) and so we do nothing at all; just leave it in place and leave the 'here' pointer
    alone.

    If code is to be executed immediately then a return instruction is appended and exec(...) is
    called on it. The dictionary pointer ('here') is restored; reclaiming this memory. */

    int16_t here; // dictionary 'here' pointer
    int16_t last; // last definition address
    int16_t locals; // local allocation pointer

    uint8_t frameAllocation(uint8_t** frameBuffer)
    {
        // allocate Reflecta frame buffer from dictionary space
        *frameBuffer = memory + here;
        return min(255, MEM_SIZE - here);
    }

    void frameReceived(uint8_t sequence, uint8_t frameLength, uint8_t* frame)
    {
        // process Reflecta frame containing Brief bytecode
        last = here;
        here += frameLength - 1; // -1 not including exec/def flag
        bool isExec = memory[here] == 0;
        if (isExec)
        {
            memset(here++, 0); // return instruction
        }
        if (here > locals)
        {
            error(VM_ERROR_OUT_OF_MEMORY);
        }
        else if (isExec)
        {
            here = last;
            exec(here);
        }
    }

/*  Events may be sent as unsolicited data up to the PC. Requests may cause events, but it is not a
    request/response model. That is, the event is always async and is not correlated with a
    particular request (at the protocol level).

    Events follow the same framing protocol from above. The payload is a single- byte identifier
    followed by an arbitrary number of data bytes.

      ID:   1 byte
      Data: n bytes

    Events may be considered simple signed scalar values generated by the event instruction. In this
    case the data bytes consist of 0-, 1- or 2-bytes depending on the value taken from the stack.
    The value 0 is transmitted as zero-length data and may be used when the ID alone is enough
    information to signal an event. Other values have various lengths:

      x = 0                   0 bytes
      -128 >= x <= 127        1 byte
      otherwise               2 bytes

    Events may instead be hand packed records of data, such as a heartbeat of sensor data. This is
    produced using the eventHeader and eventFooter instructions. Event data may be included using
    eventBody8/16. */

    int16_t eventBuffer = MEM_SIZE; // index into event buffer (reusing dictionary)

    void eventHeader() // pack event payload (ID from stack)
    {
        eventBuffer = here;
        memset(eventBuffer++, pop());
    }

    void eventBody8() // append byte to packed event payload
    {
        memset(eventBuffer++, pop());
    }

    void eventBody16() // append int16 to packed event payload
    {
        int16_t val = pop();
        memset(eventBuffer++, val >> 8);
        memset(eventBuffer++, val);
    }

    void eventFooter() // send packed event as a Reflecta frame
    {
        reflectaFrames::sendFrame(memory + here, eventBuffer - here);
    }

    void event(uint8_t id, int16_t val) // helper to send simple scaler events
    {
        push(id);
        eventHeader();
        if (val != 0)
        {
            if (val >= INT8_MIN && val <= INT8_MAX)
            {
                push(val);
                eventBody8();
            }
            else
            {
                push(val);
                eventBody16();
            }
        }
        eventFooter();
    }

/*  Several event IDs are used to notify the PC of protocol and VM errors.  Defined in Brief.h and
    ReflectaFramesSerial.h, but for reference:

      ID                 Value    Meaning
      0xFF   Reset       None     MCU reset
      0xFE   Protocol    0        Out of sequence frame
                         1        Unexpected escape byte
                         2        CRC failure
      0xFD - VM          0        Return stack underflow
                         1        Return stack overflow
                         2        Data stack underflow
                         3        Data stack overflow
                         4        Indexed out of memory */

    void error(uint8_t code) // error events
    {
        push(code);
        push(VM_EVENT_ID);
        eventHeader();
        eventBody8();
        eventFooter();
    }

/*  Below are the primitive Brief instructions; later bound in setup. All of these functions take no
    parameters and return nothing. Arguments and return values flow through the stack. */

    void eventOp() // send event up to PC containing top stack value
    {
        int8_t id = pop();
        int16_t val = pop();
        event(id, val);
    }

/*  Memory fetch/store instructions. Fetches take an address from the stack and push back the
    contents of that address (within the dictionary). Stores take a value and an address from the
    stack and store the value to the address. */

    inline int16_t mem16(int16_t address) // helper (not Brief instruction)
    {
        int16_t x = ((int16_t)mem(address)) << 8;
        return x | mem(address + 1);
    }

    void fetch8()
    {
        *s = mem(*s);
    }

    void store8()
    {
        memset(pop(), (uint8_t)pop());
    }

    void fetch16()
    {
        int16_t a = *s;
        *s = mem16(a);
    }

    void store16()
    {
        int16_t a = pop(), v = pop();
        memset(a, v >> 8);
        memset(a + 1, v);
    }

/*  Literal (constant) values are pushed to the stack by the lit8/16 instructions. The values is a
    parameter to the instruction. Literals (as well as branches below) are one of the few
    instructions to actually have operands. This is done by consuming the bytes at the current
    program counter and advancing the counter to skip them for execution. */

    void lit8()
    {
        push((int8_t)mem(p++));
    }

    void lit16()
    {
        push(mem16(p++)); p++;
    }

/*  Binary and unary ALU operations pop one or two values and push back one. These include basic
    arithmetic, bitwise operations, comparison, etc. */

    void add()
    {
        int16_t x = pop();
        *s = *s + x;
    }

    void sub()
    {
        int16_t x = pop();
        *s = *s - x;
    }

    void mul()
    {
        int16_t x = pop();
        *s = *s * x;
    }

    void div()
    {
        int16_t x = pop();
        *s = *s / x;
    }

    void mod()
    {
        int16_t x = pop();
        *s = *s % x;
    }

    void andb()
    {
        int16_t x = pop();
        *s = *s & x;
    }

    void orb()
    {
        int16_t x = pop();
        *s = *s | x;
    }

    void xorb()
    {
        int16_t x = pop();
        *s = *s ^ x;
    }

    void shift()
    {
        int16_t x = pop();
        if (x < 0) *s = *s << -x;
        else *s = *s >> x;
    }

    inline int16_t boolval(int16_t b) // helper (not Brief instruction)
    {
        // true is all bits on (works for bitwise and logical operations alike)
        return b ? 0xFFFF : 0;
    }

    void eq()
    {
        int16_t x = pop();
        *s = boolval(*s == x);
    }

    void neq()
    {
        int16_t x = pop();
        *s = boolval(*s != x);
    }

    void gt()
    {
        int16_t x = pop();
        *s = boolval(*s > x);
    }

    void geq()
    {
        int16_t x = pop();
        *s = boolval(*s >= x);
    }

    void lt()
    {
        int16_t x = pop();
        *s = boolval(*s < x);
    }

    void leq()
    {
        int16_t x = pop();
        *s = boolval(*s <= x);
    }

    void notb()
    {
        *s = ~(*s);
    }

    void neg()
    {
        *s = -(*s);
    }

    void inc()
    {
        *s = ++(*s);
    }

    void dec()
    {
        *s = --(*s);
    }

/*  Stack manipulation instructions */

    void drop()
    {
        s--;
    }

    void dup()
    {
        push(*s);
    }

    void swap()
    {
        int16_t t = *s;
        int16_t* n = s - 1;
        *s = *n; *n = t;
    }

    void pick() // nth item to top of stack
    {
        int16_t n = pop();
        push(*(s - n));
    }

    void roll() // top item slipped into nth position
    {
        int16_t n = pop(), t = *(s - n);
        int16_t* i;
        for (i = s - n; i < s; i++)
        {
            *i = *(i + 1);
        }
        *s = t;
    }

    void clr() // clear stack
    {
        s = dstack - 1;
    }

/*  Moving items between data and return stack. The return stack is commonly also used to store data
    that is local to a subroutine. It is safe to push data here to be recovered after a subroutine
    call. It is not safe to use it for passing data between subroutines. That is what the data stack
    is for. Think of arguments vs. locals. */

    void pushr()
    {
        rpush(pop());
    }

    void popr()
    {
        push(rpop());
    }

    void peekr()
    {
        push(*r);
    }

/*  Dictionary manipulation instructions:

    The 'forget' function is a Forthism for reverting to the address of a previously defined word;
    essentially forgetting it and any (potentially dependent words) defined thereafter.

    The alloc, free, tail, and local* instructions are all to support IL translation. The CLR
    doesn't use the evaluation stack for parameter passing and local storage. For example, there
    are no stack manipulation instructions in IL except drop. Instead, IL code generally makes use
    of a per-method locals and arguments fetched and stored via instructions such as
    StLoc/LdLoc/StArg/LdArg.

    This is not a feature used in idiomatic Brief code but is here to make IL translation more
    straight forward. Each method allocated enough space for locals and args. Before returning (or
    earlier for TCO), this is freed.

    The normal way of handling locals in Brief that need to survive a call and return from another
    word is to store them on the return stack. The alloc instruction does this to persist the size
    of allocation to be used by free/tail later. Tail call optimization (that is, .tail in the IL)
    is handled by the tail instruction. This frees and pushes back a zero so that freeing later upon
    return has no further effect.

    Local and arg space is allocated from the bottom of dictionary space. The 'local' instruction is
    used for args as well despite the name. It simply pushes the address of the nth slot. This
    address can then be used by the regular fetch and store instructions. Because 16-bit values are
    commonly used in translated IL, there are single-byte instructions for this. */

    void forget() // revert dictionary pointer to TOS
    {
        int16_t i = pop();
        if (i < here) // don't "remember" random memory!
            here = i;
    }

    void alloc()
    {
        int16_t len = pop();
        locals -= len;
        rpush(len); // remember for free later
        for (int16_t i = locals; i < locals + len; i++)
        {
            memset(i, 0);
        }
        if (locals < here)
        {
            error(VM_ERROR_OUT_OF_MEMORY);
        }
    }
 
    void free()
    {
        locals += rpop();
    }

    void tail()
    {
        free(); // free early
        rpush(0); // leave this for free later
    }

    void local()
    {
        push(locals + pop());
    }

    void localFetch16()
    {
        local();
        fetch16();
    }

    void localStore16()
    {
        local();
        store16();
    }

    /*  Control flow is done by instructions that manipulate the program counter (p).

        A 'call' instruction pops an address and calls it; pushing the current p as to return.

        Conditional and unconditional branching is done by relative offsets as a parameter to the
        instruction (following byte). These (like literals) are among the few instructions with
        operands; in this case to save code size by not requiring a preceeding literal.

        Notice that there is only the single conditional branch instruction.  There are no 'branch
        if greater', 'branch if equal', etc. Instead the separate comparison instructions above are
        used as the preceding predicate. */

    void call()
    {
        rpush(p);
        p = pop();
    }

    void branch()
    {
        p += (int8_t)mem(p);
    }

    void zbranch()
    {
        if (pop() == 0)
        {
            branch();
        }
        else
        {
            p++;
        }
    }

    /*  Quotations and 'choice' need some explanation. The idea behind quotations is something like
        an anonymous lambda and is used with some nice syntax in the Brief language. The 'quote'
        instruction precedes a sequence that is to be treated as an embedded definition
        essentially. It takes a length as an operand, pushes the address of the sequence of code
        following and then jumps over that code.

        The net result is that the sequence is not executed, but its address is left on the stack
        for future words to call as they see fit.

        One primitive that makes use of this is 'choice' which is the idiomatic Brief conditional.
        It pops two addresses (likely from two quotations) along with a predicate value (likely the
        result of some comparison or logical operations). It then executes one or the other
        quotation depending on the predicate.

        Another primitive making use of quotations is 'chooseIf' (called simply 'if' in Brief) which
        pops a predicate and a single address; calling the address if non-zero.

        Many secondary words in Brief also use quotation such as 'bi', 'tri', 'map', 'fold', etc.
        which act as higher-order functions applying. */

    void quote()
    {
        uint8_t len = mem(p++);
        push(p); // address of quotation
        p += len; // jump over
    }

    void choice()
    {
        int16_t f = pop();
        int16_t t = pop();
        rpush(p);
        p = pop() == 0 ? f : t;
    }

    void chooseIf()
    {
        int16_t t = pop();
        if (pop() != 0)
        {
            rpush(p);
            p = t;
        }
    }

    /*  A Brief word (address) may be set to run in the main loop. Also, a loop counter is
        maintained for use by conditional logic (throttling for example). */

    int16_t loopword = -1; // address of loop word

    int16_t loopIterations = 0; // number of iterations since 'setup'

    void loopTicks()
    {
        push(loopIterations & 0x7FFF);
    }

    void setLoop()
    {
        loopIterations = 0;
        loopword = pop();
    }

    void stopLoop()
    {
        loopword = -1;
    }

    /*  Upon first connecting to a board, the PC will execute a reset so that assumptions about
        dictionary contents and such hold true. */ 

    void resetBoard() // likely called initialy upon connecting from PC
    {
        clr();
        here = last = 0;
        locals = MEM_SIZE;
        loopword = -1;
        loopIterations = 0;
        reflectaFrames::reset();
    }

    /*  Here begins all of the Arduino-specific instructions.

        Starting with basic setup and reading/write to GPIO pins. Note we treat HIGH/LOW values as
        Brief-style booleans (-1 or 0) to play well with the logical and conditional operations. */

    void pinMode()
    {
        ::pinMode(pop(), pop());
    }

    void digitalRead()
    {
        push(::digitalRead(pop()) ? -1 : 0);
    }

    void digitalWrite()
    {
        ::digitalWrite(pop(), pop() == 0 ? LOW : HIGH);
    }

    void analogRead()
    {
        push(::analogRead(pop()));
    }

    void analogWrite()
    {
        ::analogWrite(pop(), pop());
    }

    /*  I2C support comes from several instructions, essentially mapping composable, zero-operand
        instructions to functions in the Arduino library:

          http://arduino.cc/en/Reference/Wire

        Brief words (addresses/quotations) may be hooked to respond to Wire events. */

    /*
    void wireBegin()
    {
        Wire.begin(); // join bus as master (slave not supported)
    }

    void wireRequestFrom()
    {
        Wire.requestFrom(pop(), pop());
    }

    void wireAvailable()
    {
        push(Wire.available());
    }

    void wireRead()
    {
        while (Wire.available() < 1); // TODO: blocking?
        push(Wire.read());
    }

    void wireBeginTransmission()
    {
        Wire.beginTransmission((uint8_t)pop());
    }

    void wireWrite()
    {
        Wire.write((uint8_t)pop());
    }

    void wireEndTransmission()
    {
        Wire.endTransmission();
    }

    int16_t onReceiveWord = -1;

    void wireOnReceive(int16_t count)
    {
        if (onReceiveWord != -1)
        {
            push(count);
            exec(onReceiveWord);
        }
    }

    void wireSetOnReceive()
    {
        onReceiveWord = pop();
        Wire.onReceive(wireOnReceive);
    }

    int16_t onRequestWord = -1;

    void wireOnRequest()
    {
        if (onRequestWord != -1)
        {
            exec(onRequestWord);
        }
    }

    void wireSetOnRequest()
    {
        onRequestWord = pop();
        Wire.onRequest(wireOnRequest);
    }
    */

    /*  Brief word addresses (or quotations) may be set to run upon interrupts.  For more info on
        the argument values and behavior, see:

          http://arduino.cc/en/Reference/AttachInterrupt

        We keep a mapping of up to MAX_INTERRUPTS (6) words. */

    int16_t isrs[MAX_INTERRUPTS];

    void interrupt(int16_t n) // helper (not Brief instruction)
    {
        int16_t w = isrs[n];
        if (w != -1) exec(w);
    }

    void interrupt0() // helper (not Brief instruction)
    {
        interrupt(0);
    }

    void interrupt1() // helper (not Brief instruction)
    {
        interrupt(1);
    }

    void interrupt2() // helper (not Brief instruction)
    {
        interrupt(2);
    }

    void interrupt3() // helper (not Brief instruction)
    {
        interrupt(3);
    }

    void interrupt4() // helper (not Brief instruction)
    {
        interrupt(4);
    }

    void interrupt5() // helper (not Brief instruction)
    {
        interrupt(5);
    }

    void interrupt6() // helper (not Brief instruction)
    {
        interrupt(6);
    }

    void attachISR()
    {
        uint8_t mode = pop();
        uint8_t interrupt = pop();
        isrs[interrupt] = pop();
        switch (interrupt)
        {
            case 0 : attachInterrupt(0, interrupt0, mode);
            case 1 : attachInterrupt(1, interrupt1, mode);
            case 2 : attachInterrupt(2, interrupt2, mode);
            case 3 : attachInterrupt(3, interrupt3, mode);
            case 4 : attachInterrupt(4, interrupt4, mode);
            case 5 : attachInterrupt(5, interrupt5, mode);
            case 6 : attachInterrupt(6, interrupt6, mode);
        }
    }

    void detachISR()
    {
        int interrupt = pop();
        isrs[interrupt] = -1;
        detachInterrupt(interrupt);
    }

    /*  Servo support also comes by simple mapping of composable, zero-operand instructions to
        Arduino library calls:

          http://arduino.cc/en/Reference/Servo

        We keep up to MAX_SERVOS (48) servo instances attached. */

    /*
    Servo servos[MAX_SERVOS];

    void servoAttach()
    {
        int16_t pin = pop();
        servos[pin].attach(pin);
    }

    void servoDetach()
    {
        servos[pop()].detach();
    }

    void servoWriteMicros()
    {
        servos[pop()].writeMicroseconds(pop());
    }
    */

    // A couple of stragglers...

    void milliseconds()
    {
        push(millis());
    }

    void pulseIn()
    {
        push(::pulseIn(pop(), pop()));
    }

    /*  The Brief VM needs to be hooked into the main setup and loop on the hosting project. It's
        also expected that Reflecta framing is hooked.  A minimal *.ino would contain something like
        what you find in the main Brief.ino.

        Brief setup hooks into Reflecta framing, and binds all of the instruction functions from
        above. After setup, the hosting project is free to bind its own custom functions as well! */

    void setup()
    {
        // we want to allocate frames from the dictionary
        reflectaFrames::setBufferAllocationCallback(frameAllocation);

        // we want to process frames by executing bytecode
        reflectaFrames::setFrameReceivedCallback(frameReceived);

        resetBoard();

        bind(0,  ret); // assumed in frameReceived
        bind(1,  lit8);
        bind(2,  lit16);
        bind(3,  branch); // used only by IL translation
        bind(4,  zbranch); // used only by IL translation
        bind(5,  quote);
        bind(6,  eventHeader);
        bind(7,  eventBody8);
        bind(8,  eventBody16);
        bind(9,  eventFooter);
        bind(10, eventOp);
        bind(11, fetch8);
        bind(12, store8);
        bind(13, fetch16);
        bind(14, store16);
        bind(15, add);
        bind(16, sub);
        bind(17, mul);
        bind(18, div);
        bind(19, mod);
        bind(20, andb);
        bind(21, orb);
        bind(22, xorb);
        bind(23, shift);
        bind(24, eq);
        bind(25, neq);
        bind(26, gt);
        bind(27, geq);
        bind(28, lt);
        bind(29, leq);
        bind(30, notb);
        bind(31, neg);
        bind(32, inc);
        bind(33, dec);
        bind(34, drop);
        bind(35, dup);
        bind(36, swap);
        bind(37, pick);
        bind(38, roll);
        bind(39, clr);
        bind(40, pushr);
        bind(41, popr);
        bind(42, peekr);
        bind(43, forget);
        bind(44, alloc);
        bind(45, free);
        bind(46, tail);
        bind(47, local);
        bind(48, localFetch16);
        bind(49, localStore16);
        bind(50, call);
        bind(51, choice);
        bind(52, chooseIf);
        bind(53, loopTicks);
        bind(54, setLoop);
        bind(55, stopLoop);
        bind(56, resetBoard);
        bind(57, pinMode);
        bind(58, digitalRead);
        bind(59, digitalWrite);
        bind(60, analogRead);
        bind(61, analogWrite);
        bind(62, attachISR);
        bind(63, detachISR);
        bind(64, milliseconds);
        bind(65, pulseIn);

        for (int16_t i = 0; i < MAX_INTERRUPTS; i++)
        {
            isrs[i] = -1;
        }

        event(BOOT_EVENT_ID, 0); // boot event
    }

    void loop()
    {
        if (loopword >= 0)
        {
            exec(loopword);
            loopIterations++;
        }
    }
}
