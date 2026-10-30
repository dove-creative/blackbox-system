using System;
using System.IO;
using BlackThunder.BlackboxSystem.Samples.Scenarios;

namespace BlackThunder.BlackboxSystem.Samples
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length > 0)
                return RunCommand(args, false);

            PrintUsage(Console.Out);
            while (true)
            {
                Console.Write("sample> ");
                var input = Console.ReadLine();
                if (input == null)
                    return 0;

                var commandArgs = input.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (commandArgs.Length == 0)
                    continue;

                if (commandArgs.Length == 1 && string.Equals(commandArgs[0], "exit", StringComparison.OrdinalIgnoreCase))
                    return 0;

                RunCommand(commandArgs, true);
            }
        }

        private static int RunCommand(string[] args, bool interactive)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("[Blackbox Samples] Commands do not accept additional arguments.");
                PrintUsage(Console.Error);
                return 2;
            }

            var command = args[0].Trim().ToLowerInvariant();
            command = command switch
            {
                "1" => "write",
                "2" => "exert",
                "3" => "tag",
                "4" => "exception",
                _ => command
            };

            if (command == "help")
            {
                PrintUsage(Console.Out);
                return 0;
            }
            if (command == "exit") return 0;

            string scenarioName;
            switch (command)
            {
                case "write": scenarioName = "Write"; break;
                case "exert": scenarioName = "Exert"; break;
                case "tag": scenarioName = "Tag"; break;
                case "exception": scenarioName = "Exception"; break;
                default:
                    Console.Error.WriteLine($"[Blackbox Samples] Unknown command: {args[0]}");
                    PrintUsage(Console.Error);
                    return 2;
            }

            try
            {
                switch (command)
                {
                    case "write": WriteSample.Run(interactive); break;
                    case "exert": ExertSample.Run(interactive); break;
                    case "tag": TagSample.Run(interactive); break;
                    case "exception": ExceptionSample.Run(); break;
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"[Blackbox Samples] Failed to run '{command}': {exception}");
                return 1;
            }

            Console.WriteLine($"[Blackbox Samples] Completed '{command}'.");
            Console.WriteLine($"[Blackbox Samples] Output: {SampleEnvironment.GetOutputDirectory(scenarioName)}");
            return 0;
        }

        private static void PrintUsage(TextWriter output)
        {
            output.WriteLine("Commands:");
            output.WriteLine("  write     (1) Record object activity and scopes.");
            output.WriteLine("  exert     (2) Record interactions between objects.");
            output.WriteLine("  tag       (3) Attach related objects to recorded activity.");
            output.WriteLine("  exception (4) Record an expected failure and export crash history.");
            output.WriteLine("  help          Show these commands.");
            output.WriteLine("  exit          Exit the sample app.");
            output.WriteLine("Commands do not accept additional arguments.");
        }
    }
}
