/*
  ReflectaArduinoCore.cpp - Library for exposing the core Arduino library functions over Reflecta
*/

#include <Arduino.h>
#include <ReflectaFramesSerial.h>
#include <ReflectaFunctions.h>
#include "ReflectaArduinoCore.h"

using namespace reflectaFunctions;

namespace reflectaArduinoCore
{
  void pinMode()
  {
    ::pinMode(pop(), pop());
  }
  
  void digitalRead()
  {
    push(::digitalRead(pop()));
  }
  
  void digitalWrite()
  {
    ::digitalWrite(pop(), pop());
  }
  
  void analogRead()
  {
    push(::analogRead(pop()));
  }
  
  void analogWrite()
  {
    ::analogWrite(pop(), pop());
  }
  
  // Bind the Arduino core methods to the ARDU1 interface
  void setup()
  {
    reflectaFunctions::bind("ARDU1", pinMode);
    reflectaFunctions::bind("ARDU1", digitalRead);
    reflectaFunctions::bind("ARDU1", digitalWrite);
    reflectaFunctions::bind("ARDU1", analogRead);
    reflectaFunctions::bind("ARDU1", analogWrite);
  }
};
