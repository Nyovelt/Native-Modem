using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Wave.Asio;
using System;
using System.Runtime.InteropServices;

namespace Native_Modem
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var Recorder = new Recorder();
            Recorder.setupRecordArgs();
        }
    }
}
