using System;
using System.Drawing;

namespace Assistant.Utility
{
    public class Logger
    {
        // Implement dynamic change by config.
        public static int ModuleFilter = 0; //(int)Module.PipeServer;
        
        [Flags]
        public enum Module
        {
            PipeServer = 1 << 1,
        }
        
        public static void Log(Module module, string message)
        {
            var value = ModuleFilter & (int) module;
            if (value == (int)module)
            {
                Console.WriteLine($"[UTB-Plug\t| {Enum.GetName(typeof(Module), module)}] {message}");
            }
        } 
    }
}