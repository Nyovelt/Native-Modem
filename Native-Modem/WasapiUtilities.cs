using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Native_Modem
{
    public static class WasapiUtilities
    {
        public static MMDevice SelectInputDevice()
        {
            MMDeviceEnumerator devices = new MMDeviceEnumerator();
            List<string> deviceIds = new List<string>();
            foreach (MMDevice device in devices.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                deviceIds.Add(device.ID);
                Console.WriteLine($"{deviceIds.Count}: {device.FriendlyName}");
            }
            Console.Write("Select input device:");
            int input = int.Parse(Console.ReadLine());
            MMDevice inputDevice = devices.GetDevice(deviceIds[input - 1]);
            devices.Dispose();
            return inputDevice;
        }

        public static MMDevice SelectOutputDevice()
        {
            MMDeviceEnumerator devices = new MMDeviceEnumerator();
            List<string> deviceIds = new List<string>();
            foreach (MMDevice device in devices.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                deviceIds.Add(device.ID);
                Console.WriteLine($"{deviceIds.Count}: {device.FriendlyName}");
            }
            Console.Write("Select output device:");
            int input = int.Parse(Console.ReadLine());
            MMDevice outputDevice = devices.GetDevice(deviceIds[input - 1]);
            devices.Dispose();
            return outputDevice;
        }
    }
}
