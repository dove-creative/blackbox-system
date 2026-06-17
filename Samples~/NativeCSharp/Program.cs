using System;

namespace BlackThunder.BlackboxSystem.Samples.NativeCSharp
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            PrintUsage();

            if (args.Length > 0)
                RunCommand(args[0]);

            while (true)
            {
                Console.Write("sample> ");
                var input = Console.ReadLine();
                if (input == null)
                    return 0;

                var command = input.Trim();
                if (command.Length == 0)
                    continue;

                if (string.Equals(command, "exit", StringComparison.OrdinalIgnoreCase))
                    return 0;

                RunCommand(command);
            }
        }

        private static void RunCommand(string input)
        {
            var command = input.Trim().ToLowerInvariant();
            if (command.Length == 0)
                return;

            try
            {
                switch (command)
                {
                    case "1":
                    case "write":
                        Sample_1_Write.Run();
                        break;

                    case "2":
                    case "exert":
                        Sample_2_Exert.Run();
                        break;

                    case "3":
                    case "tag":
                        Sample_3_Tag.Run();
                        break;

                    case "4":
                    case "exception":
                        RunExceptionSample();
                        break;

                    case "help":
                        PrintUsage();
                        return;

                    default:
                        Console.WriteLine($"[NativeCSharp Sample] Unknown command: {input}");
                        PrintUsage();
                        return;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[NativeCSharp Sample] Unexpected error: {exception}");
            }

            Console.WriteLine($"[NativeCSharp Sample] Logs: {SampleEnvironment.BaseLogDirectory}");
        }

        private static void RunExceptionSample()
        {
            try
            {
                Sample_4_Exception.Run();
            }
            catch (NullReferenceException exception)
            {
                Console.WriteLine($"[NativeCSharp Sample] Expected exception sample: {exception.Message}");
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Commands:");
            Console.WriteLine("  write");
            Console.WriteLine("  exert");
            Console.WriteLine("  tag");
            Console.WriteLine("  exception");
            Console.WriteLine("  help");
            Console.WriteLine("  exit");
        }
    }
}
