using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TeklaMcpServer
{
    public class Program
    {
        public static async System.Threading.Tasks.Task Main(string[] args)
        {
            // 1. Register assembly resolver BEFORE any Tekla types are touched
            AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
                TeklaInit.ResolveFromCurrentRuntime(resolveArgs);

            // 2. Log fatal errors to disk
            var logDir = Path.Combine(Path.GetTempPath(), "TeklaMcpServer");
            Directory.CreateDirectory(logDir);

            try
            {
                // 3. Run the server (NoInlining prevents JIT from loading Tekla types in Main)
                await Run(args);
            }
            catch (Exception ex)
            {
                var fatalLog = Path.Combine(logDir, "fatal.log");
                File.AppendAllText(fatalLog, $"[{DateTime.UtcNow:o}] FATAL: {ex}\n\n");
                Console.Error.WriteLine($"FATAL: {ex.Message}");
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async System.Threading.Tasks.Task Run(string[] args)
        {
            Console.Error.WriteLine($"[TeklaMcpServer] Runtime: {TeklaInit.DescribeRuntimeSelection()}");

            var builder = Host.CreateApplicationBuilder(args);
            builder.Logging.ClearProviders();
            builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.None);

            builder.Services
                .AddMcpServer(options =>
                {
                    options.ServerInfo = new() { Name = "TeklaMcpServer", Version = "3.0.0" };
                })
                .WithStdioServerTransport()
                .WithToolsFromAssembly();

            var host = builder.Build();
            await host.RunAsync();
        }
    }
}