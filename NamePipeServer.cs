using System;
using System.IO;
using System.IO.Pipes;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace Assistant
{
    public class NamePipeServer
    {
        private static int numThreads = 1;

        private static NamePipeServer _pipeServer;

        private static StreamBuffer _streamBuffer;

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
                // Read the request from the client. Once the client has
                // written to the pipe its security token will be available.
                var ss = new StreamString(pipeServer);

                // Verify our identity to the connected client using a
                // string that the client anticipates.

                ss.WriteString("I am the one true server!");
                Console.WriteLine($"\n[Plugin][PipeServer]Send validation string.");

                string filename = ss.ReadString();

                // Read in the ontents of the file while impersonating the client.
                ReadFileToStream fileReader = new ReadFileToStream(ss, filename);

                _streamBuffer = new StreamBuffer(pipeServer);

                // Display the name of the user we are imperonating.
                Console.WriteLine("\n[Plugin][PipeServer]Reading file: {0} on thread{1} as user: {2}.",
                    filename, threadId, pipeServer.GetImpersonationUserName());
                pipeServer.RunAsClient(fileReader.Start);

                var message = ss.ReadString();
                Console.WriteLine("\n[Plugin][PipeServer]Receive message: {0}", message);
            }
            // Catch the IOException that is raised if the pipe is broken
            // or disconnected.
            catch (Exception e)
            {
                Console.WriteLine("\n[Plugin][PipeServer]ERROR: {0}. thread: {1}", e.Message, threadId);
            }

            // Console.WriteLine($"\n[Plugin][PipeServer]Closing pipeServer holding thread {threadId}.");
            // pipeServer.Close();
        }

        public static int SendPck(byte[] data)
        {
            if (_streamBuffer == null)
            {
                Console.WriteLine(
                    $"\n[Plugin][PipeServer]Write buffer failed. Stream buffer handler has not been created.");
                return -1;
            }

            Console.WriteLine($"\n[Plugin][PipeServer]Write buffer. Length: {data.Length}. ");
            // $"\nData string: {DebugPrintBuffer(data)}" + 
            // $"\nRaw data: {PrintByteArray(data)}");
            return _streamBuffer.Write(data);
        }

        private static string DebugPrintBuffer(byte[] data)
        {
            return Encoding.Default.GetString(data);
        }

        private static string PrintByteArray(byte[] bytes)
        {
            var sb = new StringBuilder("byte[] { ");
            foreach (var b in bytes)
            {
                sb.Append(b + ", ");
            }

            sb.Append("}");
            return sb.ToString();
        }
    }

    // Defines the data protocol for reading and writing strings on our stream
    public class StreamString
    {
        private Stream ioStream;
        private UnicodeEncoding streamEncoding;

        public StreamString(Stream ioStream)
        {
            this.ioStream = ioStream;
            streamEncoding = new UnicodeEncoding();
        }

        public string ReadString()
        {
            int len = 0;

            len = ioStream.ReadByte() * 256;
            len += ioStream.ReadByte();
            byte[] inBuffer = new byte[len];
            ioStream.Read(inBuffer, 0, len);

            Console.WriteLine("\n[Plugin][PipeServer]Read length: " + len);

            return streamEncoding.GetString(inBuffer);
        }

        public int WriteString(string outString)
        {
            byte[] outBuffer = streamEncoding.GetBytes(outString);
            int len = outBuffer.Length;
            if (len > UInt16.MaxValue)
            {
                len = (int) UInt16.MaxValue;
            }

            Console.WriteLine("\n[Plugin][PipeServer]Write length: " + len);
            ioStream.WriteByte((byte) (len / 256));
            ioStream.WriteByte((byte) (len & 255));
            ioStream.Write(outBuffer, 0, len);
            ioStream.Flush();

            return outBuffer.Length + 2;
        }
    }

    public class StreamBuffer
    {
        private Stream ioStream;

        public StreamBuffer(Stream ioStream)
        {
            this.ioStream = ioStream;
        }

        public byte[] Read()
        {
            int len = 0;

            len = ioStream.ReadByte() * 256;
            len += ioStream.ReadByte();
            byte[] inBuffer = new byte[len];
            ioStream.Read(inBuffer, 0, len);

            return inBuffer;
        }

        public int Write(byte[] outBuffer)
        {
            int len = outBuffer.Length;
            if (len > UInt16.MaxValue)
            {
                len = (int) UInt16.MaxValue;
            }

            try
            {
                ioStream.WriteByte((byte) (len / 256));
                ioStream.WriteByte((byte) (len & 255));
                ioStream.Write(outBuffer, 0, len);
                ioStream.Flush();
            }
            catch (Exception e)
            {
                Console.WriteLine($"\n[Plugin][PipeServer]StreamBuffer.Write. Error: {e.Message}.");
            }

            return outBuffer.Length + 2;
        }
    }

    // Contains the method executed in the context of the impersonated user
    public class ReadFileToStream
    {
        private string fn;
        private StreamString ss;

        public ReadFileToStream(StreamString str, string filename)
        {
            fn = filename;
            ss = str;
        }

        public void Start()
        {
            string contents = File.ReadAllText(fn);
            ss.WriteString(contents);
            Console.WriteLine("[Plugin][PipeServer]Send content of given filename.");
        }
    }
}