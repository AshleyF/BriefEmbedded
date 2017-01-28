#include <Arduino.h>
#include <Servo.h>
#include <Wire.h>
#include <Reflecta.h>
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
    brief::setup();
    reflectaFrames::setup(19200);

    // Bind extended function to a custom instruction (100)
    // In the interactive: 100 'delay instruction
    // In C#: mcu.Instruction("delay", 100)
    brief::bind(100, delayMillis);
}

void loop()
{
    brief::loop();
    reflectaFrames::loop();
    
 pinMode(13, OUTPUT);
 digitalWrite(13, HIGH);   // turn the LED on (HIGH is the voltage level)
 delay(500);               // wait for a second
 digitalWrite(13, LOW);    // turn the LED off by making the voltage LOW
 delay(500);
}
