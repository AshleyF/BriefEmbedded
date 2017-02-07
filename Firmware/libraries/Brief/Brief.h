/* Brief.h

   Scriptable firmware for interfacing hardware with the .NET libraries and for
   running real time control loops. */

#define __STDC_LIMIT_MACROS
#include <Arduino.h>
#include <Servo.h>
#include <Wire.h>

#ifndef BRIEF_H
#define BRIEF_H

#define MEM_SIZE          512   // dictionary space
#define DATA_STACK_SIZE   4     // evaluation stack elements (int32s)
#define RETURN_STACK_SIZE 4     // return and locals stack elements (int32s)

#define MAX_PRIMITIVES    128   // max number of primitive (7-bit) instructions
#define MAX_INTERRUPTS    6     // max number of ISR words
#define MAX_SERVOS        48    // max number of servos

#define BOOT_EVENT_ID     0xFF  // event sent upon 'setup' (not reset)
#define VM_EVENT_ID       0xFE  // event sent upon VM error

#define VM_ERROR_RETURN_STACK_UNDERFLOW 0
#define VM_ERROR_RETURN_STACK_OVERFLOW  1
#define VM_ERROR_DATA_STACK_UNDERFLOW   2
#define VM_ERROR_DATA_STACK_OVERFLOW    3
#define VM_ERROR_OUT_OF_MEMORY          4

namespace brief
{
    /* The following setup() and loop() are expected to be added to the main *.ino (before Reflecta)
       as you will find in Brief.ino. */

    void setup(); // initialize everything, bind primitives
    void loop(); // execute loop word (execute/define driven by Reflecta)

    /* The following are meant for those wanting to bind their own functions into the Brief system.
       New functions can be bound to the instruction table, they can push/pop data to interact with
       other Brief instructions, and they may emit errors up to the PC. */

    void bind(uint8_t i, void (*f)()); // add function to instruction table
    void push(int16_t x); // push data to evaluation stack
    int16_t pop(); // pop data from evaluation stack
    void error(uint8_t code); // error events

    /* If, for some reason, you want to manually execute Brief bytecode in memory */

    void exec(int16_t address); // execute code at given address
}

#endif // BRIEF_H
