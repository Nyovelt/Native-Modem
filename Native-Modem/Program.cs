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
            var recorder = new Recorder();
            recorder.setupRecordArgs(1, 48000);
            recorder.startRecord();
            Console.ReadLine();
            recorder.stopRecord();
        }
    }
}
