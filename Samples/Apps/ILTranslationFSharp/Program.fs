open System
open Microsoft.Robotics.Microcontroller

let awaitTask = Async.AwaitIAsyncResult >> Async.Ignore

let PORT = "com16"
let LED_PIN = 9

let delay (ms : int) = ()

let blink pin ms =
    Microcontroller.DigitalWrite(Microcontroller.High, pin)
    delay ms
    Microcontroller.DigitalWrite(Microcontroller.Low, pin)
    delay ms

let dit = 100

let s pin =
    for _ in 0 .. 2 do
        blink pin dit
    delay dit

let da = 300

let o pin =
    for _ in 0 .. 2 do
        blink pin da
    delay dit

let sos pin =
    s pin
    o pin
    s pin

async {
    printfn "Connecting to MCU (%s)..." PORT
    let mcu = new Microcontroller(new SerialTransport(PORT))

    do! awaitTask (mcu.Connect())

    printfn "Init..."
    mcu.Instruction("delay", 100uy)
    mcu.BindAction("delay", delay)
    mcu.DefineAction(blink)
    mcu.DefineAction(s)
    mcu.DefineAction(o)

    printfn "Blinking SOS..."
    do! awaitTask (mcu.ExecuteAction(sos, LED_PIN))


    printfn "Disconnecting from MCU..."
    do! awaitTask (mcu.Disconnect())
}
|> Async.StartImmediate