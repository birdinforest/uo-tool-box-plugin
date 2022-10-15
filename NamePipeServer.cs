using System;
using System.IO;
using System.IO.Pipes;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Assistant.Utility;

namespace Assistant
{
    public class NamePipeServer
    {
        private static int numThreads = 1;

        private static NamePipeServer _pipeServer;

        private static StreamBuffer _streamBuffer;
        
        // ID of transmitted data
        private static int _id = -1;

        public static Thread[] Start()
        {
            int i;
            Thread[] servers = new Thread[numThreads];

            // Console.WriteLine("\n*** Named pipe server stream with impersonation example ***\n");
            Console.WriteLine("\n[Plugin][PipeServer]Waiting for client connect...\n");

            for (i = 0; i < numThreads; i++)
            {
                servers[i] = new Thread(ServerThread);
                servers[i].Start();
                Console.WriteLine($"\n[Plugin][PipeServer]Thread[{servers[i].ManagedThreadId}] start.");
            }

            --i;

            Thread.Sleep(250);
            // while (i >= 0)
            // {
            //     for (int j = 0; j < numThreads; j++)
            //     {
            //         if (servers[j] != null)
            //         {
            //             if (servers[j].Join(250))
            //             {
            //                 Console.WriteLine($"[Plugin][PipeServer]Server thread[{servers[i].ManagedThreadId}] finished.");
            //                 servers[i] = null;
            //                 i--; // decrement the thread watch count
            //             }
            //         }
            //     }
            // }

            Console.WriteLine("\n[Plugin][PipeServer]Server threads established.");

            return servers;
        }

        private static void ServerThread(object data)
        {
            var pipeServer = new NamedPipeServerStream("pipe", PipeDirection.InOut, numThreads);

            int threadId = Thread.CurrentThread.ManagedThreadId;

            // Wait for a client to connect
            pipeServer.WaitForConnection();

            Console.WriteLine($"\n[Plugin][PipeServer]Client connected on thread[{threadId}].");

            try
            {
                _streamBuffer = new StreamBuffer(pipeServer);

                // Verify our identity to the connected client using a
                // string that the client anticipates.

                Log($"Send validation string.");
                _streamBuffer.WriteString("Hello!");

                string filename = _streamBuffer.ReadString();

                // Read in the ontents of the file while impersonating the client.
                ReadFileToStream fileReader = new ReadFileToStream(_streamBuffer, filename);

                // Display the name of the user we are imperonating.
                Log($"Reading file: {filename} on thread {threadId} as user: {pipeServer.GetImpersonationUserName()}.");
                pipeServer.RunAsClient(fileReader.Start);

                var message = _streamBuffer.ReadString();
                Log($"Receive message: {message}");
            }
            // Catch the IOException that is raised if the pipe is broken
            // or disconnected.
            catch (Exception e)
            {
                Log($"ERROR: {e.Message}. thread: {threadId}");
            }

            // Console.WriteLine($"\n[Plugin][PipeServer]Closing pipeServer holding thread {threadId}.");
            // pipeServer.Close();
        }

        public static int SendPck(byte[] data)
        {
            if (_streamBuffer == null)
            {
                NamePipeServer.Log(
                    "Write buffer failed. Stream buffer handler has not been created.");
                return -1;
            }

            Logger.Log(Logger.Module.PipeServer, PrintByteArray(data));

            return _streamBuffer.Write(data);
        }

        private static string DebugPrintBuffer(byte[] data)
        {
            return Encoding.Default.GetString(data);
        }

        public static string PrintByteArray(byte[] bytes)
        {
            var sb = new StringBuilder("byte[] { ");
            foreach (var b in bytes)
            {
                sb.Append(b + ", ");
            }

            sb.Append("}");
            return sb.ToString();
        }

        public static void Log(string message)
        {
            Logger.Log(Logger.Module.PipeServer, message);
        }

        public static int GenerateDataId()
        {
            ++_id;
            if (_id >= UInt16.MaxValue)
                _id = 0;

            return _id;
        }
    }

    public class StreamBuffer
    {
        private Stream ioStream;
        private UnicodeEncoding streamEncoding;

        public StreamBuffer(Stream ioStream)
        {
            this.ioStream = ioStream;
            streamEncoding = new UnicodeEncoding();
        }

        public byte[] Read()
        {
            
            int id = -1;
            
            var result = ioStream.ReadByte();
            if (result == -1) return new byte[] { };
            id = result * 256;
            
            result = ioStream.ReadByte();
            if (result == -1) return new byte[] { };
            id += result;

            int len;
            result = ioStream.ReadByte();
            if (result == -1) return new byte[] { };
            len = result * 256;

            result = ioStream.ReadByte();
            if (result == -1) return new byte[] { };
            len += result;
            
            byte[] inBuffer = new byte[len];
            ioStream.Read(inBuffer, 0, len);

            NamePipeServer.Log($"|Read\t| id: {id}. len:{len}");
                
            return inBuffer;
        }
        
        public string ReadString()
        {
            var inBuffer = Read();
            return inBuffer.Length == 0 
                ? "" 
                : streamEncoding.GetString(inBuffer);
        }

        public int Write(byte[] outBuffer)
        {
            var id = NamePipeServer.GenerateDataId();
            
            int len = outBuffer.Length;
            
            // Set size limit to 65535
            if (len > UInt16.MaxValue)
            {
                len = (int) UInt16.MaxValue;
            }

            NamePipeServer.Log($"|Write\t| id: {id}, len: {len}");
            
            ioStream.WriteByte((byte) (id / 256));
            ioStream.WriteByte((byte) (id & 255));
            
            ioStream.WriteByte((byte) (len / 256));
            ioStream.WriteByte((byte) (len & 255));
            
            ioStream.Write(outBuffer, 0, len);
            
            ioStream.Flush();

            return outBuffer.Length + 2;
        }
        
        public int WriteString(string outString)
        {
            byte[] outBuffer = streamEncoding.GetBytes(outString);

            return Write(outBuffer);
        }
    }

    // Contains the method executed in the context of the impersonated user
    public class ReadFileToStream
    {
        private string fn;
        private StreamBuffer streamBuffer;

        public ReadFileToStream(StreamBuffer str, string filename)
        {
            fn = filename;
            streamBuffer = str;
        }

        public void Start()
        {
            string contents = File.ReadAllText(fn);
            streamBuffer.WriteString(contents);
            NamePipeServer.Log("Send content of given filename.");
        }
    }
}