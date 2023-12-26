using System.Device.Gpio;
var pin = 184; //gpio_6a0
using var controller = new GpioController();
var controlPin = controller.OpenPin(pin, PinMode.InputPullUp);
Console.WriteLine($" Pin {pin} is {controlPin.GetPinMode()} value {controlPin.Read()} ");
WaitForEventResult result;
while (true)
{
    do
    {
        result = controller.WaitForEvent(pin, PinEventTypes.Rising, TimeSpan.FromMinutes(5));
        Console.Write(".");
    } while (result.TimedOut);
    Console.WriteLine("Rise");
    do
    {
        result = controller.WaitForEvent(pin, PinEventTypes.Falling, TimeSpan.FromMinutes(5));
        Console.Write(".");
    } while (result.TimedOut);
    Console.WriteLine("Fall");
}