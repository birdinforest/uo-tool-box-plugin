using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using CUO_API;

namespace Assistant
{
    
    public class Engine
    {
        private static unsafe PluginHeader* header;
        public static App _app;
        public static Process _process = new Process();
        
        public static unsafe void Install(PluginHeader* plugin)
        {
            header = plugin;

            if (_app == null)
            {
                _app = new App();
            }
            _app.Install(header);
            Console.WriteLine($"[Plugin] Install to CUO.");

            var thread = new Thread(KeepingAlive);
            thread.Start();
            Console.WriteLine($"[Plugin] start thread {thread.Name}");

            NamePipeServer.Start();
        }
        
        private static unsafe void KeepingAlive()
        {
            _process.StartInfo.FileName = "/Users/forrrest/projects/UOToolBox/bin/Debug/net6.0/UOToolBox";  
            // _process.StartInfo.CreateNoWindow = true;  
            _process.Start();
            
            Console.WriteLine($"[Plugin] Engine._process: {Engine._process}");
            Process[] processes = Process.GetProcessesByName("UOToolBox");
            if (processes != null && processes.Length > 0)
            {
                Console.WriteLine($"[Plugin] processes[0]: {processes[0]}");
            }
        }
    }

    public class App
    {
        // CUO
        private static OnPacketSendRecv _sendToClient, _sendToServer, _recv, _send;
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate string dOnGetUOFilePath();
        
        public unsafe void Install(PluginHeader* header)
        {
            var _uoFilePath =
                (OnGetUOFilePath) Marshal.GetDelegateForFunctionPointer(header->GetUOFilePath, typeof(OnGetUOFilePath));
        
            Console.WriteLine("[Plugin] clientPath: " + _uoFilePath());
        
            Console.WriteLine("[Plugin] Hello from dynamic dll.");
            
            _recv = OnRecv;            
            header->OnRecv = Marshal.GetFunctionPointerForDelegate(_recv);
        }
        
        private unsafe bool OnRecv(ref byte[] data, ref int length)
        {
            // Console.WriteLine($"[Plugin] OnRecv. Length:{length}, data:{data}");
            // Console.WriteLine($"[Plugin] app: {Engine._app}");
            // Process[] processes = Process.GetProcessesByName("UOToolBox");
            // Console.WriteLine($"[Plugin] Engine._process: {Engine._process}");
            // if (processes != null && processes.Length > 0)
            // {
            //     Console.WriteLine($"[Plugin] processes[0]: {processes[0]}");
            // }
            //
            // Engine._process.Modules.GetType();
            
            NamePipeServer.SendPck(data);
            return true;
        } 
    }
}