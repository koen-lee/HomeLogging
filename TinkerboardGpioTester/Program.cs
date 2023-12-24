
using System.Device.Gpio;

Console.WriteLine("Hello, World!");

var pin = 184; //gpio_6a0
using var controller = new GpioController();
controller.SetPinMode(pin, PinMode.InputPullUp);
controller.OpenPin(pin, PinMode.Output);

WaitForEventResult result;
while (true)
{
    do
    {
        Console.Write(".");
        result = controller.WaitForEvent(pin, PinEventTypes.Rising, TimeSpan.FromMinutes(5));
    } while (result.TimedOut);
    Console.WriteLine("Rise");
    do
    {
        Console.Write(".");
        result = controller.WaitForEvent(pin, PinEventTypes.Falling, TimeSpan.FromMinutes(5));
    } while (result.TimedOut); 
    Console.WriteLine("Fall");
}