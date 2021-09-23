using NAudio.Wave;
using System;
namespace Native_Modem
{
    public class Recorder
    {
        public Recorder(int AsioDriverIndex = 0)
        {
            printASIODriver();
            var drivernames = AsioOut.GetDriverNames();
            var asioOut = new AsioOut(drivernames[AsioDriverIndex]);

        }

        public void printASIODriver()
        {
            var drivernames = AsioOut.GetDriverNames();
            foreach (var drivername in drivernames)
            {
                Console.WriteLine(drivername);
            }
        }
    }
}
