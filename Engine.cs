using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
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
            Process[] processes = Process.GetProcessesByName("UOToolBox");
            if (processes != null && processes.Length > 0)
            {
                processes[0].Kill();
                Console.WriteLine($"[Plugin] Kill processes[0]: {processes[0]}");
            }

            _process.StartInfo.FileName = "/Users/forrrest/projects/UOToolBox/bin/Debug/net6.0/UOToolBox";  
            // _process.StartInfo.CreateNoWindow = true;  
            _process.Start();
            
            Console.WriteLine($"[Plugin] Engine._process: {Engine._process}");
            processes = Process.GetProcessesByName("UOToolBox");
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
            // TODO: Send UO Path to UOToolBox, so that it could load local files.
            var _uoFilePath =
                (OnGetUOFilePath) Marshal.GetDelegateForFunctionPointer(header->GetUOFilePath, typeof(OnGetUOFilePath));
        
            Console.WriteLine("[Plugin] clientPath: " + _uoFilePath());
        
            Console.WriteLine("[Plugin] Hello from dynamic dll.");

            //_recv = OnRecv;
            _recv = OnRecvModify;
            header->OnRecv = Marshal.GetFunctionPointerForDelegate(_recv);
        }
        
        private unsafe bool OnRecv(ref byte[] data, ref int length)
        {
            //Console.WriteLine($"[Plugin] OnRecv. Length:{length}, data:{data}");
            // Console.WriteLine($"[Plugin] app: {Engine._app}");
            // Process[] processes = Process.GetProcessesByName("UOToolBox");
            // Console.WriteLine($"[Plugin] Engine._process: {Engine._process}");
            // if (processes != null && processes.Length > 0)
            // {
            //     Console.WriteLine($"[Plugin] processes[0]: {processes[0]}");
            // }
            //
            // Engine._process.Modules.GetType();

            if(IsJourney(ref data)) {
                Console.WriteLine($"[Plugin][server] receive a journy packet:");
                Console.WriteLine($"[Plugin][server] text: {ParseJourney(ref data)}");
            }

            NamePipeServer.SendPck(data);
            return true;
        }

        private unsafe bool OnRecvModify(ref byte[] data, ref int length)
        {
            //Console.WriteLine($"[Plugin] OnRecv. Length:{length}, data:{data}");
            // Console.WriteLine($"[Plugin] app: {Engine._app}");
            // Process[] processes = Process.GetProcessesByName("UOToolBox");
            // Console.WriteLine($"[Plugin] Engine._process: {Engine._process}");
            // if (processes != null && processes.Length > 0)
            // {
            //     Console.WriteLine($"[Plugin] processes[0]: {processes[0]}");
            // }
            //
            // Engine._process.Modules.GetType();

            var textMap = new Dictionary<string, string>()
	        {
                {
                    "You can type '[helpadmin' to learn the commands for this server.\0", "你可以通过命令 '[helpadmin' 学习服务器支持的更多命令。"
                },
                {
                    "You have 0 of max 0 in your mailbox.\0", "在你的邮箱有0封邮件，邮箱容量为0。"
                }
		    };

            if(IsJourney(ref data)) {
                var content = ParseJourneyContent(ref data);
                Console.WriteLine($"[Plugin][server] receive a journy packet:");
                Console.WriteLine($"[Plugin][server] text: {content} \n length: {data.Length}");

                //Console.WriteLine($"[Plugin][server] modified text: {ParseJourney(ref data)} - Modified by plugin.");

                if(textMap.ContainsKey(content))
                //if(content == "You can type '[helpadmin' to learn the commands for this server.")
                {
                    var header = SliceMe(data, 48);
                    var contentBytes = Encoding.BigEndianUnicode.GetBytes(textMap[content]);
                    data = Combine(header, contentBytes);
                    length = 48 + contentBytes.Length;
                    Console.WriteLine($"[Plugin][server] localization: {textMap[content]} \n length: {data.Length}");
		        }
            }

            NamePipeServer.SendPck(data);
            return true;
        }

        public static byte[] Combine(byte[] first, byte[] second)
        {
            byte[] bytes = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, bytes, 0, first.Length);
            Buffer.BlockCopy(second, 0, bytes, first.Length, second.Length);
            return bytes;
        }

        static byte[] SliceMe(byte[] source, int length)
        {
            byte[] destfoo = new byte[length];
            Array.Copy(source, 0, destfoo, 0, length);
            return destfoo;
        }

        /**
         * Check if this pacakge is journey. Journy pacakge Id: 174
         */
        public static bool IsJourney(ref byte[] buffer)
        {
            // BinaryPrimitives.TryReadUInt32BigEndian(buffer, out uint v);
            var id = buffer[0];
            //Console.WriteLine($"[Plugin][server][Parse] id: {id}");
            return id == 174;
        }

        /**
         * Parse journey name and content
         */
        public static string ParseJourney(ref byte[] buffer)
        {
            var name = Encoding.Default.GetString(buffer, 18, 30);
            var content = Encoding.BigEndianUnicode.GetString(buffer, 48, buffer.Length - 48);

            return $"{name}:{content}";
        }

        /**
         * Parse journey content
         */
        public static string ParseJourneyContent(ref byte[] buffer)
        {
            var content = Encoding.BigEndianUnicode.GetString(buffer, 48, buffer.Length - 48);

            return content;
        }

        //private static int GetIndexOfZero(ReadOnlySpan<byte> span, int sizeT)
        //{
        //    switch (sizeT)
        //    {
        //        case 2: return MemoryMarshal.Cast<byte, char>(span).IndexOf('\0') * 2;
        //        case 4: return MemoryMarshal.Cast<byte, uint>(span).IndexOf((uint)0) * 4;
        //        default: return span.IndexOf((byte)0);
        //    }
        //}
    }
}