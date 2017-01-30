#include <Arduino.h>
#include <Brief.h>

// This is an example of how to extend the VM.
// Add a void function taking no arguments.
// Pop parameters from the stack and push return values.
void delayMillis()
{
    delay((int)brief::pop());
}

void setup()
{
    brief::setup(19200);

    // Bind extended function to a custom instruction (100)
    // In the interactive: 100 'delay instruction
    // In .NET: compiler.Instruction("delay", 100)
    brief::bind(100, delayMillis);
    pinMode(13, OUTPUT);
    digitalWrite(13, LOW);
}

void loop()
{
    brief::loop();
}
