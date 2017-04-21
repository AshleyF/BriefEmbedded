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
    eight levels deep! Note that infinite tail recursion is possible none-the-less. */

    // Memory (dictionary)

    void error(uint8_t code); // forward decl

    uint8_t memory[MEM_SIZE]; // dictionary (and local/arg space for IL semantics)

    uint8_t memget(int16_t address) // fetch with bounds checking
    {
        if (address < 0 || address >= MEM_SIZE)
        {
            error(VM_ERROR_OUT_OF_MEMORY);
            return 0;
        }

	return memory[address];
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

    The lower seven bits become essentially an index into a function table. Each may consume
    and/or produce values on the data stack as well as having other side effects. Only three
    instructions manipulate the return stack. Two are `push` and `pop` which move values between the
    data and return stack. The third is (return); popping an address at which execution continues.

    It is extremely common to factor out redundant sequences of code into subroutines. The `call`
    instruction is not used for general subroutine calls. Instead, if the high bit is set then the
    following byte is taken and together (in little endian), with the high bit reset, they become
    an address to be called.

      1xxxxxxxxxxxxxxx

    This allows 15-bit addressing to definitions in the dictionary.

    Upon calling, the VM pushes the current program counter to the return stack. There is a `return`
    instruction, used to terminate definitions, which pops the return stack to continue execution
    after the call. */

    void (*instructions[MAX_PRIMITIVES])(); // instruction function table

    void bind(uint8_t i, void (*f)()) // add function to instruction table
    {
        instructions[i] = f;
    }

    int16_t p; // program counter (VM instruction pointer)

    int16_t pget()
    {
        return p;
    }

    void pset(int16_t pp)
    {
        p = pp;
    }

    void ret() // return instruction
    {
        p = rpop();
    }

    void run() // run code at p
    {
        int16_t i;
        do
        {
            i = memget(p++);
            if ((i & 0x80) == 0) // instruction?
            {
                instructions[i](); // execute instruction
            }
            else // address to call
            {
                if (memget(p + 1) != 0) // not followed by return (TCO)
                    rpush(p + 1); // return address
                p = ((i << 8) & 0x7F00) | memget(p); // jump
            }
        } while (p >= 0); // -1 pushed to return stack
    }

    void exec(int16_t address) // execute code at given address
    {
        r = rstack - 1; // reset return stack
        rpush(-1); // causing `run()` to fall through upon completion
        p = address;
        run();
    }

/*  As the dictionary is filled `here` points to the next available byte, while `last` points to the
    byte following the previously commited definition. This way the dictionary also acts as a scratch
    buffer; filled with event data or with "immediate mode" instructions, then rolled back to `last`. */

    int16_t here; // dictionary 'here' pointer
    int16_t last; // last definition address

/*  Events are used to send unsolicited data up to the PC. Requests may cause events, but it is
    not a request/response model. That is, the event is always async and is not correlated with a
    particular request (at the protocol level).

    The payload is a zero- or single-byte identifier followed by an arbitrary number of data bytes.
    This is prefixed by a length header byte, indicating the length of the data (excluding ID).

      Length: 1 byte
      ID:     1 byte
      Data:   n bytes (0, 1 or 2)

    Events may be considered simple signed scalar values generated by the event instruction. In this
    case the data bytes consist of 0-, 1- or 2-bytes depending on the value taken from the stack.
    The value 0 is transmitted as zero-length data and may be used when the ID alone is enough
    information to signal an event. Other values have various lengths:

      x = 0                   0 bytes
      -128 >= x <= 127        1 byte
      otherwise               2 bytes

    Events may instead be hand packed records of data, such as a "heartbeat" of sensor data. This is
    produced using the `eventHeader` and `eventFooter` instructions. Event data may be included using
    `eventBody8`/`eventBody16`. */

    int16_t eventBuffer = MEM_SIZE; // index into event buffer (reusing dictionary)

    void eventHeader() // pack event payload (ID from stack)
    {
	eventBuffer = here; // note: initially `MEM_SIZE` to cause OOM if body/footer without header
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

    void eventFooter() // send packed event
    {
	byte len = eventBuffer - here;
	Serial.write(len - 1);
	for (int16_t i = 0; i < len; i++)
	{
	    Serial.write(memget(here + i));
	}
	Serial.flush();
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

/*  Several event IDs are used to notify the PC of VM activity and errors. Defined in Brief.h:

      ID                 Value    Meaning
      0xFF   Reset       None     MCU reset
      0xFE - VM          0        Return stack underflow
                         1        Return stack overflow
                         2        Data stack underflow
                         3        Data stack overflow
                         4        Indexed out of memory */

    void error(uint8_t code) // error events
    {
	event(code, VM_EVENT_ID);
    }

/*  Below are the primitive Brief instructions; later bound in setup. All of these functions take no
    parameters and return nothing. Arguments and return values flow through the stack. */

    void eventOp() // send event up to PC containing top stack value
    {
        int8_t id = pop();
        int16_t val = pop();
        event(id, val);
    }

/*  Memory `fetch`/`store` instructions. Fetches take an address from the stack and push back the
    contents of that address (within the dictionary). Stores take a value and an address from the
    stack and store the value to the address. */

    inline int16_t mem16(int16_t address) // helper (not Brief instruction)
    {
        int16_t x = ((int16_t)memget(address)) << 8;
        return x | memget(address + 1);
    }

    void fetch8()
    {
        *s = memget(*s);
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

/*  Literal values are pushed to the stack by the `lit8`/`lit16` instructions. The values is a
    parameter to the instruction. Literals (as well as branches below) are one of the few
    instructions to actually have operands. This is done by consuming the bytes at the current
    program counter and advancing the counter to skip them for execution. */

    void lit8()
    {
        push(memget(p++));
    }

    void lit16()
    {
        push(mem16(p++)); p++;
    }

/*  Binary and unary ALU operations pop one or two values and push back one. These include basic
    arithmetic, bitwise operations, comparison, etc.
    
    The truth value used in Brief is all bits reset (-1) and so the bitwise `and`/`or`/`not` words
    serve equally well as logical operators. */

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
	int16_t x = pop(); // negative values shift left, positive right
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
    is for. Think of arguments vs. locals. The normal way of handling locals in Brief that need to
    survive a call and return from another word is to store them on the return stack. */

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

    The `forget` function is a Forthism for reverting to the address of a previously defined word;
    essentially forgetting it and any (potentially dependent words) defined thereafter. */

    void forget() // revert dictionary pointer to TOS
    {
        int16_t i = pop();
        if (i < here) // don't "remember" random memory!
            here = i;
    }

    /*  A `call` instruction pops an address and calls it; pushing the current `p` as to return. */

    void call()
    {
        rpush(p);
        p = pop();
    }

/*  Quotations and `choice` need some explanation. The idea behind quotations is something like
    an anonymous lambda and is used with some nice syntax in the Brief language. The `quote`
    instruction precedes a sequence that is to be treated as an embedded definition
    essentially. It takes a length as an operand, pushes the address of the sequence of code
    following and then jumps over that code.

    The net result is that the sequence is not executed, but its address is left on the stack
    for future words to call as they see fit.

    One primitive that makes use of this is `choice` which is the idiomatic Brief conditional.
    It pops two addresses (likely from two quotations) along with a predicate value (likely the
    result of some comparison or logical operations). It then executes one or the other
    quotation depending on the predicate.

    Another primitive making use of quotations is `chooseIf` (called simply `if` in Brief) which
    pops a predicate and a single address; calling the address if non-zero.

    Many secondary words in Brief also use quotation such as `bi`, `tri`, `map`, `fold`, etc.
    which act as higher-order functions. */

    void quote()
    {
        uint8_t len = memget(p++);
        push(p); // address of quotation
        p += len; // jump over
    }

    void choice()
    {
	int16_t f = pop(), t = pop();
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

    void next()
    {
        int16_t count = rpop() - 1;
        int16_t rel = memget(p++);
        if (count > 0)
        {
            rpush(count);
            p -= (rel + 2);
        }
    }

    void nop()
    {
    }

/*  A Brief word (address) may be set to run in the main loop. Also, a loop counter is
    maintained for use by conditional logic (throttling for example). */

    int16_t loopword = -1; // address of loop word

    int16_t loopIterations = 0; // number of iterations since 'setup' (wraps)

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
        loopword = -1;
        loopIterations = 0;
    }

/*  Here begins all of the Arduino-specific instructions.

    Starting with basic setup and reading/write to GPIO pins. Note we treat `HIGH`/`LOW` values as
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

/*  Brief word addresses (or quotations) may be set to run upon interrupts. For more info on
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

    We keep up to MAX_SERVO_COUNT (48) servo instances attached. */

    Servo servos[MAX_SERVO_COUNT];

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

/*  A couple of stragglers... */

    void milliseconds()
    {
        push(millis());
    }

    void pulseIn()
    {
        push(::pulseIn(pop(), pop()));
    }

/*  The Brief VM needs to be hooked into the main setup and loop on the hosting project.
    A minimal *.ino would contain something like:
    
	#include <Brief.h>

	void setup()
	{
	    brief::setup();
	}

	void loop()
	{
	    brief::loop();
	}

    Brief setup binds all of the instruction functions from above. After setup, the hosting
    project is free to bind its own custom functions as well!
    
    An example of this could be to add a `delayMillis` instruction. Such an instruction is not
    included in the VM to discourage blocking code, but you're free to add whatever you like:
    
	void delayMillis()
	{
	    delay((int)brief::pop());
	}

	void setup()
	{
	    brief::setup(19200);
	    brief::bind(100, delayMillis);
	}
	
    This adds the new instruction as opcode 100. You can then give it a name and tell the compiler
    about it with `compiler.Instruction("delay", 100)` in PC-side code or can tell the Brief
    interactive about it with `100 'delay instruction`. This is the extensibility story for Brief.
    
    Notice that custom instruction function may retrieve and return values via the
    `brief::pop()` and `brief::push()` functions, as well as raise errors with
    `brief::error(uint8_t code)`.
*/

    void setup()
    {
	Serial.begin(19200); // assumed by interactive
        resetBoard();

        bind(0,  ret);
        bind(1,  lit8);
        bind(2,  lit16);
        bind(3,  quote);
        bind(4,  eventHeader);
        bind(5,  eventBody8);
        bind(6,  eventBody16);
        bind(7,  eventFooter);
        bind(8,  eventOp);
        bind(9,  fetch8);
        bind(10, store8);
        bind(11, fetch16);
        bind(12, store16);
        bind(13, add);
        bind(14, sub);
        bind(15, mul);
        bind(16, div);
        bind(17, mod);
        bind(18, andb);
        bind(19, orb);
        bind(20, xorb);
        bind(21, shift);
        bind(22, eq);
        bind(23, neq);
        bind(24, gt);
        bind(25, geq);
        bind(26, lt);
        bind(27, leq);
        bind(28, notb);
        bind(29, neg);
        bind(30, inc);
        bind(31, dec);
        bind(32, drop);
        bind(33, dup);
        bind(34, swap);
        bind(35, pick);
        bind(36, roll);
        bind(37, clr);
        bind(38, pushr);
        bind(39, popr);
        bind(40, peekr);
        bind(41, forget);
        bind(42, call);
        bind(43, choice);
        bind(44, chooseIf);
        bind(45, loopTicks);
        bind(46, setLoop);
        bind(47, stopLoop);
        bind(48, resetBoard);
        bind(49, pinMode);
        bind(50, digitalRead);
        bind(51, digitalWrite);
        bind(52, analogRead);
        bind(53, analogWrite);
        bind(54, attachISR);
        bind(55, detachISR);
        bind(56, milliseconds);
        bind(57, pulseIn);
	bind(58, next);
	bind(59, nop);

        for (int16_t i = 0; i < MAX_INTERRUPTS; i++)
        {
            isrs[i] = -1;
        }

        event(BOOT_EVENT_ID, 0); // boot event
    }

/*  The payload from the PC to the MCU is in the form of Brief code. A header byte indicates
    the length and whether the code is to be executed immediately (0x00) or appended to the
    dictionary as a new definition (0x01).

    A dictionary pointer is maintained at the MCU. This pointer always references the first
    available free byte of dictionary space (beginning at address 0x0000). Each definition sent to
    the MCU is appended to the end of the dictionary and advances the pointer. The bottom end of the
    dictionary space is used for arguments (mainly for IL, not idiomatic Brief).

    If code is a definition then it is expected to already be terminated by a `return` instruction (if
    appropriate) and so we do nothing at all; just leave it in place and leave the `here` pointer
    alone.

    If code is to be executed immediately then a return instruction is appended and exec(...) is
    called on it. The dictionary pointer (`here`) is restored; reclaiming this memory. */

    void loop()
    {
	if (Serial.available())
	{
	    int8_t b = Serial.read();
	    bool isExec = (b & 0x80) == 0x80;
	    int8_t len = b & 0x7f;
	    for (; len > 0; len--)
	    {
		while(!Serial.available());
		memset(here++, Serial.read());
	    }

	    if (isExec)
	    {
		memset(here++, 0); // ensure return
		here = last;
		exec(here);
	    }
	    else
	    {
		last = here;
	    }
	}
	
        if (loopword >= 0)
        {
            exec(loopword);
            loopIterations++;
        }
    }
}
