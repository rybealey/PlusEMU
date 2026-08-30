using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using NLog;
using NLog.Extensions.Logging;
using Plus.Core;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Plus.Communication.Flash;
using Plus.Communication.Nitro;
using Plus.Communication.RCON;
using Plus.Database;
using Plus.Plugins;
using Plus.Utilities.DependencyInjection;
using Scrutor;
using System.Runtime.InteropServices;

namespace Plus;

public static class Program
{
    private static Dictionary<ServiceLifetime, IEnumerable<Type>> _defaultTypes = new();

    // PosixSignalRegistration is disposed when collected — the references
    // must outlive Main's loop for the handlers to keep firing.
    private static readonly List<PosixSignalRegistration> _signalRegistrations = new();

    public static async Task Main(string[] args)
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        // pixelrp: raise the ThreadPool floor. Almost every packet handler and
        // the per-room ProcessTask do SYNCHRONOUS MySQL, so a pooled worker
        // blocks on I/O rather than finishing quickly. The pool's default
        // minimum is Environment.ProcessorCount (2 on the VPS) and past that it
        // injects new workers at only ~1-2/second — so a couple of concurrent
        // queries left `room.ProcessTask.Start()` queued for hundreds of ms and
        // the 500ms room beat was skipped outright ("[stall] Room N tick
        // skipped ... after 503ms" with the CPU idle). These threads are only
        // created on demand; the floor just removes the injection delay.
        // Override with PLUS_MIN_THREADS if a bigger box wants a different one.
        var minThreads = int.TryParse(Environment.GetEnvironmentVariable("PLUS_MIN_THREADS"), out var configuredMinThreads) && configuredMinThreads > 0
            ? configuredMinThreads
            : Math.Max(64, Environment.ProcessorCount * 16);
        ThreadPool.GetMinThreads(out var currentWorker, out var currentIo);
        ThreadPool.SetMinThreads(Math.Max(minThreads, currentWorker), Math.Max(minThreads, currentIo));

        var services = new ServiceCollection();
        _defaultTypes[ServiceLifetime.Singleton] = typeof(Program).Assembly.GetTypes().Where(t => t.IsInterface && t.GetCustomAttributes<SingletonAttribute>().Any());
        _defaultTypes[ServiceLifetime.Scoped] = typeof(Program).Assembly.GetTypes().Where(t => t.IsInterface && t.GetCustomAttributes<ScopedAttribute>().Any());

        // Plugins
        Directory.CreateDirectory("plugins");
        var pluginAssemblies = new DirectoryInfo("plugins").GetDirectories().Select(d => PluginLoadContext.LoadPlugin(Path.Join("plugins", d.Name), d.Name)).ToList();
        pluginAssemblies.AddRange(new DirectoryInfo("plugins").GetFiles().Where(f => Path.GetExtension(f.Name).Equals(".dll")).Select(f => PluginLoadContext.LoadPlugin(Path.Join("plugins"), Path.GetFileNameWithoutExtension(f.Name))));
        var pluginDefinitions = pluginAssemblies.SelectMany(pluginAssembly => AddPlugin(services, pluginAssembly)).ToList();

        // Configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Join(Directory.GetCurrentDirectory(), "Config"))
            .AddJsonFile("config.json")
            .Build();

        services.AddConfiguration<FlashServerConfiguration>(configuration.GetSection("Flash"));
        services.AddConfiguration<NitroServerConfiguration>(configuration.GetSection("Nitro"));
        services.AddConfiguration<DatabaseConfiguration>(configuration.GetSection("Database"));
        services.AddConfiguration<RconConfiguration>(configuration.GetSection("Rcon"));

        // Dependency Injection
        services.AddDefaultRules(typeof(Program).Assembly);

        foreach (var plugin in pluginDefinitions)
            plugin.OnServicesConfigured();


        // Configuration
        LogManager.LoadConfiguration(Path.Join(Directory.GetCurrentDirectory(), "Config", "nlog.config"));

        // pixelrp: PLUS_LOG_LEVEL overrides nlog.config's rules at startup, so
        // turning debug logging back on is an env var and a restart rather than
        // a rebuild. Applied here (not in the XML) because layout renderers are
        // not reliably supported in a rule's minLevel attribute, and a parse
        // failure there would take the whole emulator down at boot.
        var nlogLevelName = Environment.GetEnvironmentVariable("PLUS_LOG_LEVEL");
        if (!string.IsNullOrWhiteSpace(nlogLevelName))
        {
            try
            {
                var nlogLevel = NLog.LogLevel.FromString(nlogLevelName);
                foreach (var rule in LogManager.Configuration.LoggingRules)
                    rule.SetLoggingLevels(nlogLevel, NLog.LogLevel.Fatal);
                LogManager.ReconfigExistingLoggers();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Ignoring PLUS_LOG_LEVEL='{nlogLevelName}': {e.Message}");
            }
        }
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            // pixelrp: Information, not Trace. At Trace every LogDebug call site
            // is evaluated (arguments boxed, messages formatted) before NLog can
            // drop it — GameClient logs one per OUTGOING PACKET. nlog.config
            // filters again on the NLog side; this gate is the cheaper one.
            // PLUS_LOG_LEVEL=Debug|Trace re-enables it without a rebuild.
            loggingBuilder.SetMinimumLevel(
                Enum.TryParse<Microsoft.Extensions.Logging.LogLevel>(Environment.GetEnvironmentVariable("PLUS_LOG_LEVEL"), true, out var minLogLevel)
                    ? minLogLevel
                    : Microsoft.Extensions.Logging.LogLevel.Information);
            loggingBuilder.AddNLog();
        });

        var serviceProvider = services.BuildServiceProvider();
        foreach (var plugin in pluginDefinitions)
            plugin.OnServiceProviderBuild(serviceProvider);

        Console.ForegroundColor = ConsoleColor.White;
        Console.CursorVisible = false;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        // `docker compose stop` delivers SIGTERM; without these hooks the
        // process dies before it can broadcast, save inventories or reset
        // server_status.
        foreach (var signal in new[] { PosixSignal.SIGTERM, PosixSignal.SIGINT })
        {
            _signalRegistrations.Add(PosixSignalRegistration.Create(signal, context =>
            {
                context.Cancel = true;
                PlusEnvironment.PerformShutDown();
            }));
        }

        // Start
        var environment = serviceProvider.GetRequiredService<IPlusEnvironment>();
        var started = await environment.Start();
        if (!started)
        {
            Environment.Exit(1);
            return;
        }
        while (true)
        {
            if (Console.ReadKey(true).Key == ConsoleKey.Enter)
            {
                Console.Write("plus> ");
                var input = Console.ReadLine();
                if (input.Length > 0)
                {
                    var s = input.Split(' ')[0];
                    ConsoleCommands.InvokeCommand(s);
                }
            }
        }
    }

    public static IServiceCollection AddAssignableTo<T>(this IServiceCollection services, Assembly assembly, ServiceLifetime lifetime = ServiceLifetime.Singleton) => services.AddAssignableTo(new[] { assembly }, typeof(T), lifetime);

    public static IServiceCollection AddAssignableTo<T>(this IServiceCollection services, IEnumerable<Assembly> assemblies, ServiceLifetime lifetime = ServiceLifetime.Singleton) => services.AddAssignableTo(assemblies, typeof(T), lifetime);

    public static IServiceCollection AddAssignableTo(this IServiceCollection services, Assembly assembly, Type type, ServiceLifetime lifetime = ServiceLifetime.Singleton) => services.AddAssignableTo(new[] { assembly }, type, lifetime);

    public static IServiceCollection AddAssignableTo(this IServiceCollection services, IEnumerable<Assembly> assemblies, Type type, ServiceLifetime lifetime = ServiceLifetime.Singleton) =>
        services.Scan(scan => scan.FromAssemblies(assemblies)
            .AddClasses(classes => classes.Where(t => t.IsAssignableTo(type) && !t.IsAbstract && !t.IsInterface))
            .UsingRegistrationStrategy(RegistrationStrategy.Append)
            .AsSelfWithInterfaces()
            .WithSingletonLifetime());

    private static IServiceCollection AddDefaultRules(this IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().Where(t => t.IsInterface && t.GetCustomAttributes<SingletonAttribute>().Any()).Concat(_defaultTypes[ServiceLifetime.Singleton]).Distinct())
            services.AddAssignableTo(assembly, type, ServiceLifetime.Singleton);
        foreach (var type in assembly.GetTypes().Where(t => t.IsInterface && t.GetCustomAttributes<ScopedAttribute>().Any()).Concat(_defaultTypes[ServiceLifetime.Scoped]).Distinct())
            services.AddAssignableTo(assembly, type, ServiceLifetime.Scoped);

        services.Scan(scan => scan.FromAssemblies(assembly)
            .AddClasses(classes => classes.Where(c => c.GetInterface($"I{c.Name}") != null))
            .UsingRegistrationStrategy(RegistrationStrategy.Skip)
            .AsSelfWithInterfaces()
            .WithSingletonLifetime());
        return services;
    }

    private static IEnumerable<IPluginDefinition> AddPlugin(IServiceCollection services, Assembly pluginAssembly)
    {
        var pluginDefinitions = new List<IPluginDefinition>();
        try
        {
            services.AddDefaultRules(pluginAssembly);

            foreach (var pluginDefinition in pluginAssembly.DefinedTypes.Where(t =>
                         t.ImplementedInterfaces.Contains(typeof(IPluginDefinition))))
            {
                var plugin = (IPluginDefinition?)Activator.CreateInstance(pluginDefinition);
                if (plugin != null)
                {
                    plugin.ConfigureServices(services);
                    services.AddSingleton(plugin);
                    pluginDefinitions.Add(plugin);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load plugin assembly { pluginAssembly.FullName}. Possibly outdated. {e.Message}");
        }
        return pluginDefinitions;
    }
    public static IServiceCollection AddConfiguration<T>(this IServiceCollection services, IConfigurationSection section)
        where T : class
    {
        services.Configure<T>(section);
        return services;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        var e = (Exception)args.ExceptionObject;
        //Logger.LogCriticalException("SYSTEM CRITICAL EXCEPTION: " + e);
        PlusEnvironment.PerformShutDown();
    }
}