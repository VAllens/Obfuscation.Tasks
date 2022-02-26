using System;
using Serilog;
using Serilog.Events;

namespace ObfuscationSamples
{
    class Program
    {
        static Program()
        {
            CreateBootstrapLogger();
        }

        static void Main(params string[] args)
        {
            Log.Debug("Hello, World!");
            Log.Information("This is ObfuscationSamples");
            Console.ReadKey();
        }

        private static void CreateBootstrapLogger()
        {
            Log.Logger = new LoggerConfiguration()
#if DEBUG
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Debug)
                .MinimumLevel.Override("System", LogEventLevel.Debug)
#else
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
#endif
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.WithEnvironmentUserName()
                .Enrich.WithAssemblyName()
                .Enrich.WithAssemblyVersion()
                .Enrich.WithProcessId()
                .Enrich.WithProcessName()
                .Enrich.WithThreadId()
                .Enrich.WithThreadName()
                .WriteTo.Async(c => c.Console())
                .WriteTo.Async(c => c.File(
                    path: $"Logs/Bootstrapper_{DateTime.Today:yyyyMMdd}.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} ({AssemblyName}/{AssemblyVersion}/{EnvironmentName}/{MachineName}/{ProcessName}/{ProcessId}/{ThreadId}) {Application} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
                .WriteTo.Async(c => c.Trace(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} ({AssemblyName}/{AssemblyVersion}/{EnvironmentName}/{MachineName}/{ProcessName}/{ProcessId}/{ThreadId}) {Application} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
                .CreateBootstrapLogger();
        }
    }
}