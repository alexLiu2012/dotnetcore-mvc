## about web hosting

相关程序集：

* microsoft.aspnetcore.hosting

----

### 1.about

#### 1.1 summary



#### 1.2 how designed



### 2a. details - web host

#### 2.1 web host

##### 2.1.1 接口

```c#
public interface IWebHost : IDisposable
{    
    IFeatureCollection ServerFeatures { get; }        
    IServiceProvider Services { get; }
    
    void Start();        
    Task StartAsync(CancellationToken cancellationToken = default);        
    Task StopAsync(CancellationToken cancellationToken = default);
}

```

##### 2.1.2 实现

```c#
internal class WebHost : IWebHost, IAsyncDisposable
{
    private static readonly string DeprecatedServerUrlsKey = "server.urls";
    
    /* 构造时注入的服务 */
    private readonly IServiceCollection _applicationServiceCollection;
    private readonly IServiceProvider _hostingServiceProvider;
    private readonly WebHostOptions _options;
    private readonly IConfiguration _config;
    private readonly AggregateException? _hostingStartupErrors;
    
    /* 从 host service provider 中解析的服务 */    
    private IStartup? _startup;
    private ApplicationLifetime? _applicationLifetime;
    private HostedServiceExecutor? _hostedServiceExecutor;            
    private IServiceProvider? _applicationServices;
    private ExceptionDispatchInfo? _applicationServicesException;    
    private IServer? Server { get; set; }
    private ILogger _logger =  NullLogger.Instance;
    
    /* 标记 */
    private bool _stopped;
    private bool _startedServer;
    
    // Used for testing only
    internal WebHostOptions Options => _options;
            
    public IServiceProvider Services
    {
        get
        {
            Debug.Assert(
                _applicationServices != null, 
                "Initialize must be called before accessing services.");
            
            return _applicationServices;
        }
    }
    
    public IFeatureCollection ServerFeatures
    {
        get
        {
            EnsureServer();
            return Server.Features;
        }
    }                                                   
}

```

###### 2.1.2.1 构造函数

```c#
internal class WebHost : IWebHost, IAsyncDisposable
{
    public WebHost(
        IServiceCollection appServices,
        IServiceProvider hostingServiceProvider,
        WebHostOptions options,
        IConfiguration config,
        AggregateException? hostingStartupErrors)
    {
        // 注入 service collection 为 null，抛出异常
        if (appServices == null)
        {
            throw new ArgumentNullException(nameof(appServices));
        }
        // 注入 host service provider 为 null，抛出异常
        if (hostingServiceProvider == null)
        {
            throw new ArgumentNullException(nameof(hostingServiceProvider));
        }
        // 注入 configuration 为 null，抛出异常
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }
        
        _config = config;
        _hostingStartupErrors = hostingStartupErrors;
        _options = options;
        _applicationServiceCollection = appServices;
        _hostingServiceProvider = hostingServiceProvider;
        
        // 注入 application lifetime 实例
        _applicationServiceCollection
            .AddSingleton<ApplicationLifetime>();
        // 将 application lifetime 暴露为 IHostApplicationLifetime
        _applicationServiceCollection
            .AddSingleton(services => 
            	services.GetService<ApplicationLifetime>() as 
            		IHostApplicationLifetime);
        
        // 注入 hosted service executor
        _applicationServiceCollection
            .AddSingleton<HostedServiceExecutor>();
    }                                 
}

```

###### 2.1.2.2 dispose

```c#
internal class WebHost : IWebHost, IAsyncDisposable
{
    // 同步释放
    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }
    
    // 异步释放
    public async ValueTask DisposeAsync()
    {
        if (!_stopped)
        {
            try
            {
                await StopAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.ServerShutdownException(ex);
            }
        }
        
        await DisposeServiceProviderAsync(_applicationServices)
            .ConfigureAwait(false);
        
        await DisposeServiceProviderAsync(_hostingServiceProvider)
            .ConfigureAwait(false);
    }
    
    private async ValueTask DisposeServiceProviderAsync(
        IServiceProvider? serviceProvider)
    {
        switch (serviceProvider)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }
}

```

##### 2.1.3 web host initial

* 从 IStartup 构建 service provider
* web app 天然是 scoped

###### 2.1.3.1 initial

```c#
internal class WebHost : IWebHost, IAsyncDisposable
{    
    // Called immediately after the constructor so the properties can rely on it.
    public void Initialize()
    {
        try
        {
            // 从 IStartup 构建 service provider
            EnsureApplicationServices();
        }
        catch (Exception ex)
        {
            /* 如果没有 IStartup，创建 service provider */
            // EnsureApplicationServices may have failed 
            // due to a missing or throwing Startup class.
            if (_applicationServices == null)
            {
                _applicationServices = 
                    _applicationServiceCollection.BuildServiceProvider();
            }
            
            // 如果没有标记捕捉 startup error，抛出异常
            if (!_options.CaptureStartupErrors)
            {
                throw;
            }
            
            // 记录异常
            _applicationServicesException = ExceptionDispatchInfo.Capture(ex);
        }
    }
}

```

###### 2.1.3.2 ensure app services

* 从 IStartup 构建 service provider

```c#
internal class WebHost : IWebHost, IAsyncDisposable
{
    private void EnsureApplicationServices()
    {
        if (_applicationServices == null)
        {
            // 解析 IStartup
            EnsureStartup();
            // 构建 service provider
            _applicationServices = 
                _startup.ConfigureServices(_applicationServiceCollection);
        }
    }
}

```

###### 2.1.3.3 ensure startup

* 解析 IStartup

```c#
internal class WebHost : IWebHost, IAsyncDisposable
{
    [MemberNotNull(nameof(_startup))]
    private void EnsureStartup()
    {
        if (_startup != null)
        {
            return;
        }
        
        // 从 host service provider 解析 IStartup，
        // 即构造函数中注入的 service provider
        var startup = _hostingServiceProvider.GetService<IStartup>();
        
        if (startup == null)
        {
            throw new InvalidOperationException($"No application configured. Please specify startup via IWebHostBuilder.UseStartup, IWebHostBuilder.Configure, injecting {nameof(IStartup)} or specifying the startup assembly via {nameof(WebHostDefaults.StartupAssemblyKey)} in the web host configuration.");
        }
        
        _startup = startup;
    }
}

```

##### 2.1.4 web host start

###### 2.1.4.1 start

```c#
internal class WebHost : IWebHost, IAsyncDisposable
{
    // 同步方法
    public void Start()
    {
        StartAsync().GetAwaiter().GetResult();
    }
}

```

###### 2.1.4.2 start async

```c#
internal class WebHost : IWebHost, IAsyncDisposable
{
    // 异步方法
    public virtual async Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        // 判断 service provider 不为 null
        Debug.Assert(
            _applicationServices != null, 
            "Initialize must be called first.");
        
        /* a - 启动 event source log */
        HostingEventSource.Log.HostStart();
        
        /* b - 解析 logger，记录 logger starting */
        _logger = _applicationServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics");        
        _logger.Starting();
        
        /* c - 构建 IApplicationBuilder */
        var application = BuildApplication();
        
        /* d - 解析 (host) application lifetime */
        _applicationLifetime = _applicationServices
            GetRequiredService<ApplicationLifetime>();
        
        /* e - 解析并启动 hosted service executor */
        
        // 解析 hosted service executor
        _hostedServiceExecutor = _applicationServices
            .GetRequiredService<HostedServiceExecutor>();                
        // Fire IHostedService.Start
        await _hostedServiceExecutor
            .StartAsync(cancellationToken)
            .ConfigureAwait(false);
        
        /* f - 创建并启动 server */
        
        // 解析 diagnostic listener
        var diagnosticSource = _applicationServices
            .GetRequiredService<DiagnosticListener>();
        // 解析 http context factory
        var httpContextFactory = _applicationServices
            .GetRequiredService<IHttpContextFactory>();
        // 创建 hosting application
        var hostingApp = new HostingApplication(
            application, 
            _logger, 
            diagnosticSource, 
            httpContextFactory);
        // 启动（build application）解析的 server        
        await Server.StartAsync(
            hostingApp, 
            cancellationToken)
            .ConfigureAwait(false);        
        _startedServer = true;
        
        /* g - 启动 lifetime notify */
        // Fire IApplicationLifetime.Started
        _applicationLifetime?.NotifyStarted();
                
        /* h - 记录 logger started */
        _logger.Started();
        
        // 记录加载的程序集
        // Log the fact that we did load hosting startup assemblies.
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            foreach (var assembly in 
                     _options.GetFinalHostingStartupAssemblies())
            {
                _logger.LogDebug(
                    "Loaded hosting startup assembly {assemblyName}", 
                    assembly);
            }
        }
        // 记录 hosting startup 错误
        if (_hostingStartupErrors != null)
        {
            foreach (var exception in 
                     _hostingStartupErrors.InnerExceptions)
            {
                _logger.HostingStartupAssemblyError(exception);
            }
        }
    }
}

```

###### 2.1.4.3 build application

* 构建 request delegate
  * 构建了 server

```c#
internal class WebHost : IWebHost, IAsyncDisposable
{
    [MemberNotNull(nameof(Server))]
    private RequestDelegate BuildApplication()
    {
        Debug.Assert(
            _applicationServices != null, 
            "Initialize must be called first.");
        
        try
        {
            // 解析 service provider 时有错误，抛出异常
            // 此时使用创建的 service provider
            _applicationServicesException?.Throw();
            
            // 解析 IServer
            EnsureServer();
            
            /* 构建 application builder */
            
            // 解析 app builder factory
            var builderFactory = _applicationServices
                .GetRequiredService<IApplicationBuilderFactory>();
            // 用 factory 构建 app builder
            var builder = builderFactory
                .CreateBuilder(Server.Features);
            // 指定 app builder 的 service provider，
            // 为当前解析的 service provider
            builder.ApplicationServices = _applicationServices;
            
            /* 解析 startup filter 并配置 applicaiton builder */
            
            // 解析 startup filter
            var startupFilters = _applicationServices
                .GetService<IEnumerable<IStartupFilter>>();
            // 解析 IStartup 的 applicaiton builder 配置 action
            Action<IApplicationBuilder> configure = _startup!.Configure;
            // 用 startup filter 配置 application builder action
            if (startupFilters != null)
            {
                foreach (var filter in startupFilters.Reverse())
                {
                    configure = filter.Configure(configure);
                }
            }
            
            // 用 application builder action 配置 builder
            configure(builder);
            
            // 构建 request delegate
            return builder.Build();
        }
        catch (Exception ex)
        {
            // 如果不抑制 status msg
            if (!_options.SuppressStatusMessages)
            {
                // Write errors to standard out 
                // so they can be retrieved when not in development mode.
                Console.WriteLine(
                    "Application startup exception: " + ex.ToString());
            }
            
            // 从 app service provider 中解析 logger，
            // 记录（build application）异常            
            var logger = _applicationServices
                .GetRequiredService<ILogger<WebHost>>();
            logger.ApplicationError(ex);
            
            // 如果没有标记捕获错误，抛出异常
            if (!_options.CaptureStartupErrors)
            {
                throw;
            }
            
            // 解析 server
            EnsureServer();
            
            /* 创建 error page request delegate */
            // Generate an HTML error page.
            var hostingEnv = _applicationServices
                .GetRequiredService<IHostEnvironment>();
            
            var showDetailedErrors = 
                hostingEnv.IsDevelopment() || _options.DetailedErrors;
            
            return ErrorPageBuilder
                .BuildErrorPageApplication(
                	hostingEnv.ContentRootFileProvider, 
                	logger, 
                	showDetailedErrors, 
                	ex);
        }
    }
}

```

###### 2.1.4.4 ensure server

```c#
internal class WebHost : IWebHost, IAsyncDisposable
{
    [MemberNotNull(nameof(Server))]
    private void EnsureServer()
    {
        Debug.Assert(
            _applicationServices != null, 
            "Initialize must be called first.");
        
        if (Server == null)
        {
            // 从构建后的 service provider 中解析 IServer，
            // 即 IStartup 的 service provider 中解析
            Server = _applicationServices
                .GetRequiredService<IServer>();
            
            // 获取 server 中的 addresses 集合
            var serverAddressesFeature = Server.Features?.Get<IServerAddressesFeature>();
            var addresses = serverAddressesFeature?.Addresses;
            
            // 读取配置中的 url 并写入 addresses 集合 
            if (addresses != null && 
                !addresses.IsReadOnly && 
                addresses.Count == 0)
            {
                /* 指定的 configuration key */
                var urls = _config[WebHostDefaults.ServerUrlsKey] 
                    ?? _config[DeprecatedServerUrlsKey];
                
                if (!string.IsNullOrEmpty(urls))
                {
                    serverAddressesFeature!.PreferHostingUrls = 
                        WebHostUtilities.ParseBool(
                        	_config, 
                        	WebHostDefaults.PreferHostingUrlsKey);
                    
                    foreach (var value in 
                             urls.Split(
                                 ';', 
                                 StringSplitOptions.RemoveEmptyEntries))
                    {
                        addresses.Add(value);
                    }
                }
            }
        }
    }        
}

```

##### 2.1.5 web host stop

```c#
internal class WebHost : IWebHost, IAsyncDisposable
{
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        // 判断和设置 stopped 标记
        // 保证不会重复 stop
        if (_stopped)
        {
            return;
        }
        _stopped = true;
        
        _logger.Shutdown();
        
        using var timeoutCTS = new CancellationTokenSource(Options.ShutdownTimeout);
        var timeoutToken = timeoutCTS.Token;
        if (!cancellationToken.CanBeCanceled)
        {
            cancellationToken = timeoutToken;
        }
        else
        {
            cancellationToken = 
                CancellationTokenSource
                	.CreateLinkedTokenSource(
                		cancellationToken, 
                		timeoutToken)
                	.Token;
        }
        
        /* 停止 host application lifetime */
        // Fire IApplicationLifetime.Stopping
        _applicationLifetime?.StopApplication();
        
        /* 停止 server */
        if (Server != null && _startedServer)
        {
            await Server
                .StopAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        
        /* 停止 host service executor */
        // Fire the IHostedService.Stop
        if (_hostedServiceExecutor != null)
        {
            await _hostedServiceExecutor
                .StopAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        
        /* 停止 notify */
        // Fire IApplicationLifetime.Stopped
        _applicationLifetime?.NotifyStopped();
        
        /* 停止 event source log */
        HostingEventSource.Log.HostStop();
    }
}

```

##### 2.1.6 扩展方法

###### 2.1.6.1 wait for shutdown

```c#
public static class WebHostExtensions
{
    public static void WaitForShutdown(this IWebHost host)
    {
        host.WaitForShutdownAsync().GetAwaiter().GetResult();
    }
    
    public static async Task WaitForShutdownAsync(
        this IWebHost host, 
        CancellationToken token = default)
    {
        var done = new ManualResetEventSlim(false);
        using (var cts = CancellationTokenSource
               	.CreateLinkedTokenSource(token))
        {
            using (var lifetime = 
                   	new WebHostLifetime(
                        cts, 
                        done, 
                        shutdownMessage: tring.Empty))
            {
                try
                {
                    await host.WaitForTokenShutdownAsync(cts.Token);
                    lifetime.SetExitedGracefully();
                }
                finally
                {
                    done.Set();
                }
            }            
        }
    }    
    
    private static async Task WaitForTokenShutdownAsync(
        this IWebHost host, 
        CancellationToken token)
    {
        var applicationLifetime = 
            host.Services.GetRequiredService<IHostApplicationLifetime>();
        
        token.Register(
            state =>
            	{
                	((IHostApplicationLifetime)state!).StopApplication();
            	},
            applicationLifetime);
        
        var waitForStop = 
            new TaskCompletionSource(
            	TaskCreationOptions.RunContinuationsAsynchronously);
        
        applicationLifetime
            .ApplicationStopping
            .Register(
            	obj =>
            		{
                        var tcs = (TaskCompletionSource)obj!;
                        tcs.TrySetResult();
                    }, 
            	waitForStop);
        
        await waitForStop.Task;
        
        // WebHost will use its default ShutdownTimeout if none is specified.
        await host.StopAsync();
    }
}

```

###### 2.1.6.2 run

```c#
public static class WebHostExtensions
{
    public static void Run(this IWebHost host)
    {
        host.RunAsync().GetAwaiter().GetResult();
    }
    
    
    public static async Task RunAsync(
        this IWebHost host, 
        CancellationToken token = default)
    {
        // Wait for token shutdown if it can be canceled
        if (token.CanBeCanceled)
        {
            await host.RunAsync(token, startupMessage: null);
            return;
        }
        
        // If token cannot be canceled, attach Ctrl+C and SIGTERM shutdown
        var done = new ManualResetEventSlim(false);
        using (var cts = new CancellationTokenSource())
        {
            var shutdownMessage = 
                host.Services.GetRequiredService<WebHostOptions>()
                	.SuppressStatusMessages 
                		? string.Empty 
                		: "Application is shutting down...";
            
            using (var lifetime = 
                   	new WebHostLifetime(
                        cts, 
                        done, 
                        shutdownMessage: shutdownMessage))
            {
                try
                {
                    await host.RunAsync(
                        cts.Token, 
                        "Application started. Press Ctrl+C to shut down.");
                    
                    lifetime.SetExitedGracefully();
                }
                finally
                {
                    done.Set();
                }
            }
        }
    }
    
    private static async Task RunAsync(
        this IWebHost host, 
        CancellationToken token, 
        string? startupMessage)
    {
        try
        {
            await host.StartAsync(token);
            
            var hostingEnvironment = host.Services.GetService<IHostEnvironment>();
            var options = host.Services.GetRequiredService<WebHostOptions>();
            
            if (!options.SuppressStatusMessages)
            {
                Console.WriteLine(
                    $"Hosting environment: {hostingEnvironment?.EnvironmentName}");
                Console.WriteLine(
                    $"Content root path: {hostingEnvironment?.ContentRootPath}");
                                
                var serverAddresses = 
                    host.ServerFeatures
                    	.Get<IServerAddressesFeature>()?.Addresses;
                if (serverAddresses != null)
                {
                    foreach (var address in serverAddresses)
                    {
                        Console.WriteLine($"Now listening on: {address}");
                    }
                }
                
                if (!string.IsNullOrEmpty(startupMessage))
                {                    
                    Console.WriteLine(startupMessage);
                }
            }
            
            await host.WaitForTokenShutdownAsync(token);
        }
        finally
        {
            if (host is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                host.Dispose();
            }
        }
    }
}

```

###### 2.1.6.3 stop

```c#
public static class WebHostExtensions
{    
    public static async Task StopAsync(this IWebHost host, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        await host.StopAsync(cts.Token);
    }                                                
}

```

#### 2.2 web host options

##### 2.2.1 web host options

```c#
internal class WebHostOptions
{
    public string ApplicationName { get; set; }
    public string Environment { get; set; }
   
    public string ContentRootPath { get; set; }
    public string WebRoot { get; set; }
    
    public string StartupAssembly { get; set; }
    public IReadOnlyList<string> HostingStartupAssemblies { get; set; }    
    public IReadOnlyList<string> HostingStartupExcludeAssemblies { get; set; }
        
    public bool PreventHostingStartup { get; set; }    
    public bool SuppressStatusMessages { get; set; }            
    public bool DetailedErrors { get; set; }        
    public bool CaptureStartupErrors { get; set; }
                                    
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);
                        
    public WebHostOptions(
        IConfiguration configuration, 
        string applicationNameFallback)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        ApplicationName = 
            configuration[WebHostDefaults.ApplicationKey] 
            	?? applicationNameFallback;
        Environment = 
            configuration[WebHostDefaults.EnvironmentKey];
        ContentRootPath = 
            configuration[WebHostDefaults.ContentRootKey];
        WebRoot = 
            configuration[WebHostDefaults.WebRootKey];
        
        StartupAssembly = 
            configuration[WebHostDefaults.StartupAssemblyKey];
        // Search the primary assembly and configured assemblies.
        HostingStartupAssemblies = 
            Split(
            	$"{ApplicationName};
            	 "{configuation[WebHostDefaults.HostingStartupAssembliesKey]}");
        HostingStartupExcludeAssemblies = 
            Split(
            	configuration[WebHostDefaults.HostingStartupExcludeAssembliesKey]);
        
        PreventHostingStartup = 
            WebHostUtilities.ParseBool(
            	configuration, 
            	WebHostDefaults.PreventHostingStartupKey);         
        SuppressStatusMessages = 
            WebHostUtilities.ParseBool(
            	configuration, 
            	WebHostDefaults.SuppressStatusMessagesKey);        
        DetailedErrors = 
            WebHostUtilities.ParseBool(
            	configuration, 
            	WebHostDefaults.DetailedErrorsKey);        
        CaptureStartupErrors = 
            WebHostUtilities.ParseBool(
            	configuration, 
            	WebHostDefaults.CaptureStartupErrorsKey);
                                                        
        var timeout = configuration[WebHostDefaults.ShutdownTimeoutKey];
        if (!string.IsNullOrEmpty(timeout) && 
            int.TryParse(
                timeout, 
                NumberStyles.None, 
                CultureInfo.InvariantCulture, 
                out var seconds))
        {
            ShutdownTimeout = TimeSpan.FromSeconds(seconds);
        }
    }
        
    public IEnumerable<string> GetFinalHostingStartupAssemblies()
    {
        return HostingStartupAssemblies.Except(
            HostingStartupExcludeAssemblies, 
            StringComparer.OrdinalIgnoreCase);
    }
    
    private IReadOnlyList<string> Split(string value)
    {
        return value?.Split(
            ';', 
            StringSplitOptions.TrimEntries | 
            StringSplitOptions.RemoveEmptyEntries)
                ?? Array.Empty<string>();
    }
}

```

##### 2.2.2 web host default

```c#
public static class WebHostDefaults
{    
    public static readonly string ApplicationKey = "applicationName";
    public static readonly string EnvironmentKey = "environment";
        
    public static readonly string ContentRootKey = "contentRoot";
    public static readonly string WebRootKey = "webroot";
    
    public static readonly string StartupAssemblyKey = "startupAssembly";        
    public static readonly string HostingStartupAssembliesKey = "hostingStartupAssemblies"; 
    public static readonly string HostingStartupExcludeAssembliesKey = "hostingStartupExcludeAssemblies";

    public static readonly string PreventHostingStartupKey = "preventHostingStartup";    
    public static readonly string SuppressStatusMessagesKey = "suppressStatusMessages";
    public static readonly string DetailedErrorsKey = "detailedErrors";
    public static readonly string CaptureStartupErrorsKey = "captureStartupErrors";
    
    public static readonly string ShutdownTimeoutKey = "shutdownTimeoutSeconds";
    
    public static readonly string ServerUrlsKey = "urls";   
    public static readonly string PreferHostingUrlsKey = "preferHostingUrls";
    public static readonly string StaticWebAssetsKey = "staticWebAssets";            
}

```

#### 2.3 application lifetime

```c#
internal class ApplicationLifetime : IHostApplicationLifetime
{
    private readonly CancellationTokenSource _startedSource = new CancellationTokenSource();
    private readonly CancellationTokenSource _stoppingSource = new CancellationTokenSource();
    private readonly CancellationTokenSource _stoppedSource = new CancellationTokenSource();
    private readonly ILogger<ApplicationLifetime> _logger;
    
    public CancellationToken ApplicationStarted => _startedSource.Token;        
    public CancellationToken ApplicationStopping => _stoppingSource.Token;        
    public CancellationToken ApplicationStopped => _stoppedSource.Token;
    
    public ApplicationLifetime(ILogger<ApplicationLifetime> logger)
    {
        _logger = logger;
    }
                
    public void StopApplication()
    {
        // Lock on CTS to synchronize multiple calls to StopApplication. 
        // This guarantees that the first call to StopApplication and 
        // its callbacks run to completion before subsequent calls to StopApplication, 
        // which will no-op since the first call already requested cancellation, 
        // get a chance to execute.
        lock (_stoppingSource)
        {
            try
            {
                ExecuteHandlers(_stoppingSource);
            }
            catch (Exception ex)
            {
                _logger.ApplicationError(
                    LoggerEventIds.ApplicationStoppingException,
                    "An error occurred stopping the application",
                    ex);
            }
        }
    }
            
    public void NotifyStarted()
    {
        try
        {
            ExecuteHandlers(_startedSource);
        }
        catch (Exception ex)
        {
            _logger.ApplicationError(
                LoggerEventIds.ApplicationStartupException,
                "An error occurred starting the application",
                ex);
        }
    }
            
    public void NotifyStopped()
    {
        try
        {
            ExecuteHandlers(_stoppedSource);
        }
        catch (Exception ex)
        {
            _logger.ApplicationError(
                LoggerEventIds.ApplicationStoppedException,
                "An error occurred stopping the application",
                ex);
        }
    }
    
    private void ExecuteHandlers(CancellationTokenSource cancel)
    {
        // Noop if this is already cancelled
        if (cancel.IsCancellationRequested)
        {
            return;
        }
        
        // Run the cancellation token callbacks
        cancel.Cancel(throwOnFirstException: false);
    }
}

```

#### 2.4  hosted service executor

```c#
internal class HostedServiceExecutor
{
    private readonly IEnumerable<IHostedService> _services;
    private readonly ILogger<HostedServiceExecutor> _logger;
    
    public HostedServiceExecutor(
        ILogger<HostedServiceExecutor> logger, 
        IEnumerable<IHostedService> services)
    {
        _logger = logger;
        _services = services;
    }

    public Task StartAsync(CancellationToken token)
    {
        return ExecuteAsync(
            service => service.StartAsync(token));
    }
    
    public Task StopAsync(CancellationToken token)
    {
        return ExecuteAsync(
            service => service.StopAsync(token), 
            throwOnFirstFailure: false);
    }
    
    private async Task ExecuteAsync(
        Func<IHostedService, Task> callback, 
        bool throwOnFirstFailure = true)
    {
        List<Exception>? exceptions = null;
        
        foreach (var service in _services)
        {
            try
            {
                await callback(service);
            }
            catch (Exception ex)
            {
                if (throwOnFirstFailure)
                {
                    throw;
                }
                
                if (exceptions == null)
                {
                    exceptions = new List<Exception>();
                }
                
                exceptions.Add(ex);
            }
        }
        
        // Throw an aggregate exception if there were any exceptions
        if (exceptions != null)
        {
            throw new AggregateException(exceptions);
        }
    }
}

```



#### 2.a IStartup

##### 2.a.1 接口

```c#
public interface IStartup
{    
    IServiceProvider ConfigureServices(IServiceCollection services);         
    void Configure(IApplicationBuilder app);
}

```

##### 2.a.2 实现

###### 2.a.2.1 startup base

```c#
public abstract class StartupBase : IStartup
{            
    IServiceProvider IStartup.ConfigureServices(IServiceCollection services)
    {
        ConfigureServices(services);
        return CreateServiceProvider(services);
    }
    
    public abstract void Configure(IApplicationBuilder app);
    
    public virtual void ConfigureServices(IServiceCollection services)
    {
    }
        
    public virtual IServiceProvider CreateServiceProvider(
        IServiceCollection services)
    {
        return services.BuildServiceProvider();
    }
}


public abstract class StartupBase<TBuilder> : 	
	StartupBase where TBuilder : notnull
{
    private readonly IServiceProviderFactory<TBuilder> _factory;            
    public StartupBase(IServiceProviderFactory<TBuilder> factory)
    {
        _factory = factory;
    }
    
    
    public override IServiceProvider CreateServiceProvider(
        IServiceCollection services)
    {
        var builder = _factory.CreateBuilder(services);
        ConfigureContainer(builder);
        return _factory.CreateServiceProvider(builder);
    }
    
    
    public virtual void ConfigureContainer(TBuilder builder)
    {
    }
}

```

###### 2.a.2.2 delegate startup

```c#
public class DelegateStartup : StartupBase<IServiceCollection>
{
    private Action<IApplicationBuilder> _configureApp;            
    public DelegateStartup(
        IServiceProviderFactory<IServiceCollection> factory, 
        Action<IApplicationBuilder> configureApp) : 
    		base(factory)
    {
        _configureApp = configureApp;
    }
    
        
    public override void Configure(IApplicationBuilder app) => 
        _configureApp(app);
}

```

###### 2.a.2.3 convention statup

```c#
internal class ConventionBasedStartup : IStartup
{
    private readonly StartupMethods _methods;    
    public ConventionBasedStartup(StartupMethods methods)
    {
        _methods = methods;
    }
    
    public IServiceProvider ConfigureServices(
        IServiceCollection services)
    {
        try
        {
            return _methods.ConfigureServicesDelegate(services);
        }
        catch (Exception ex)
        {
            if (ex is TargetInvocationException)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
            }
            
            throw;
        }
    }
    
    public void Configure(IApplicationBuilder app)
    {
        try
        {
            _methods.ConfigureDelegate(app);
        }
        catch (Exception ex)
        {
            if (ex is TargetInvocationException)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
            }
            
            throw;
        }
    }        
}

```

##### 2.2.3 startup method

```c#
internal class StartupMethods
{
    public object? StartupInstance { get; }
    public Func<IServiceCollection, IServiceProvider> ConfigureServicesDelegate { get; }        
    public Action<IApplicationBuilder> ConfigureDelegate { get; }
    
    public StartupMethods(
        object? instance, 
        Action<IApplicationBuilder> configure, 
        Func<IServiceCollection, IServiceProvider> configureServices)
    {
        Debug.Assert(configure != null);
        Debug.Assert(configureServices != null);
        
        StartupInstance = instance;
        ConfigureDelegate = configure;
        ConfigureServicesDelegate = configureServices;
    }            
}

```

#### 2.3 iserver

##### 2.3.1 接口

```c#
public interface IServer : IDisposable
{    
    IFeatureCollection Features { get; }
        
    Task StartAsync<TContext>(
        IHttpApplication<TContext> application, 
        CancellationToken cancellationToken) 
        	where TContext : notnull;
        
    Task StopAsync(CancellationToken cancellationToken);
}

```



```c#
public interface IHttpApplication<TContext> where TContext : notnull
{    
    TContext CreateContext(IFeatureCollection contextFeatures);        
    Task ProcessRequestAsync(TContext context);        
    void DisposeContext(TContext context, Exception? exception);
}

```



```c#
public interface IServerAddressesFeature
{    
    ICollection<string> Addresses { get; }        
    bool PreferHostingUrls { get; set; }
}

public class ServerAddressesFeature : IServerAddressesFeature
    {
        /// <inheritdoc />
        public ICollection<string> Addresses { get; } = new List<string>();

        /// <inheritdoc />
        public bool PreferHostingUrls { get; set; }
    }
```

#### 2.4 application builder

```c#
public interface IApplicationBuilderFactory
    {
        /// <summary>
        /// Create an <see cref="IApplicationBuilder" /> builder given a <paramref name="serverFeatures" />
        /// </summary>
        /// <param name="serverFeatures">An <see cref="IFeatureCollection"/> of HTTP features.</param>
        /// <returns>An <see cref="IApplicationBuilder"/> configured with <paramref name="serverFeatures"/>.</returns>
        IApplicationBuilder CreateBuilder(IFeatureCollection serverFeatures);
    }

public class ApplicationBuilderFactory : IApplicationBuilderFactory
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Initialize a new factory instance with an <see cref="IServiceProvider" />.
        /// </summary>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to resolve dependencies and initialize components.</param>
        public ApplicationBuilderFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Create an <see cref="IApplicationBuilder" /> builder given a <paramref name="serverFeatures" />.
        /// </summary>
        /// <param name="serverFeatures">An <see cref="IFeatureCollection"/> of HTTP features.</param>
        /// <returns>An <see cref="IApplicationBuilder"/> configured with <paramref name="serverFeatures"/>.</returns>
        public IApplicationBuilder CreateBuilder(IFeatureCollection serverFeatures)
        {
            return new ApplicationBuilder(_serviceProvider, serverFeatures);
        }
    }

```





#### 2.5 web host builder

##### 2.5.1 接口

```c#

```

##### 2.5.2 实现

```c#

```

###### 2.5.2.1 configure service



###### 2.5.2.2 configure



##### 2.5.3 build

```c#

```

###### 2.5.3.1 build common service

```c#

```



###### 2.5.3.2 clone

```c#
internal static class ServiceCollectionExtensions
    {
        public static IServiceCollection Clone(this IServiceCollection serviceCollection)
        {
            IServiceCollection clone = new ServiceCollection();
            foreach (var service in serviceCollection)
            {
                clone.Add(service);
            }
            return clone;
        }
    }
```



###### 2.5.3.3 get provider from factory

```c#

```

###### 2.5.3.4 resovle content path

```c#
public class WebHostBuilder : IWebHostBuilder
{
    
}

```

###### 2.5.3.5 add application service

```c#

```

##### 2.5.4 builder 扩展方法

```c#
public static IWebHostBuilder Configure(this IWebHostBuilder hostBuilder, Action<IApplicationBuilder> configureApp)
        {
            return hostBuilder.Configure((_, app) => configureApp(app), configureApp.GetMethodInfo().DeclaringType!.Assembly.GetName().Name!);
        }

        /// <summary>
        /// Specify the startup method to be used to configure the web application.
        /// </summary>
        /// <param name="hostBuilder">The <see cref="IWebHostBuilder"/> to configure.</param>
        /// <param name="configureApp">The delegate that configures the <see cref="IApplicationBuilder"/>.</param>
        /// <returns>The <see cref="IWebHostBuilder"/>.</returns>
        public static IWebHostBuilder Configure(this IWebHostBuilder hostBuilder, Action<WebHostBuilderContext, IApplicationBuilder> configureApp)
        {
            return hostBuilder.Configure(configureApp, configureApp.GetMethodInfo().DeclaringType!.Assembly.GetName().Name!);
        }

        private static IWebHostBuilder Configure(this IWebHostBuilder hostBuilder, Action<WebHostBuilderContext, IApplicationBuilder> configureApp, string startupAssemblyName)
        {
            if (configureApp == null)
            {
                throw new ArgumentNullException(nameof(configureApp));
            }

            hostBuilder.UseSetting(WebHostDefaults.ApplicationKey, startupAssemblyName);

            // Light up the ISupportsStartup implementation
            if (hostBuilder is ISupportsStartup supportsStartup)
            {
                return supportsStartup.Configure(configureApp);
            }

            return hostBuilder.ConfigureServices((context, services) =>
            {
                services.AddSingleton<IStartup>(sp =>
                {
                    return new DelegateStartup(sp.GetRequiredService<IServiceProviderFactory<IServiceCollection>>(), (app => configureApp(context, app)));
                });
            });
        }

        /// <summary>
        /// Specify a factory that creates the startup instance to be used by the web host.
        /// </summary>
        /// <param name="hostBuilder">The <see cref="IWebHostBuilder"/> to configure.</param>
        /// <param name="startupFactory">A delegate that specifies a factory for the startup class.</param>
        /// <returns>The <see cref="IWebHostBuilder"/>.</returns>
        /// <remarks>When using the il linker, all public methods of <typeparamref name="TStartup"/> are preserved. This should match the Startup type directly (and not a base type).</remarks>
        public static IWebHostBuilder UseStartup<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]TStartup>(this IWebHostBuilder hostBuilder, Func<WebHostBuilderContext, TStartup> startupFactory) where TStartup : class
        {
            if (startupFactory == null)
            {
                throw new ArgumentNullException(nameof(startupFactory));
            }

            var startupAssemblyName = startupFactory.GetMethodInfo().DeclaringType!.Assembly.GetName().Name;

            hostBuilder.UseSetting(WebHostDefaults.ApplicationKey, startupAssemblyName);

            // Light up the GenericWebHostBuilder implementation
            if (hostBuilder is ISupportsStartup supportsStartup)
            {
                return supportsStartup.UseStartup(startupFactory);
            }

            return hostBuilder
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton(typeof(IStartup), sp =>
                    {
                        var instance = startupFactory(context) ?? throw new InvalidOperationException("The specified factory returned null startup instance.");

                        var hostingEnvironment = sp.GetRequiredService<IHostEnvironment>();

                        // Check if the instance implements IStartup before wrapping
                        if (instance is IStartup startup)
                        {
                            return startup;
                        }

                        return new ConventionBasedStartup(StartupLoader.LoadMethods(sp, instance.GetType(), hostingEnvironment.EnvironmentName, instance));
                    });
                });
        }

        /// <summary>
        /// Specify the startup type to be used by the web host.
        /// </summary>
        /// <param name="hostBuilder">The <see cref="IWebHostBuilder"/> to configure.</param>
        /// <param name="startupType">The <see cref="Type"/> to be used.</param>
        /// <returns>The <see cref="IWebHostBuilder"/>.</returns>
        public static IWebHostBuilder UseStartup(this IWebHostBuilder hostBuilder, [DynamicallyAccessedMembers(StartupLinkerOptions.Accessibility)] Type startupType)
        {
            if (startupType == null)
            {
                throw new ArgumentNullException(nameof(startupType));
            }

            var startupAssemblyName = startupType.Assembly.GetName().Name;

            hostBuilder.UseSetting(WebHostDefaults.ApplicationKey, startupAssemblyName);

            // Light up the GenericWebHostBuilder implementation
            if (hostBuilder is ISupportsStartup supportsStartup)
            {
                return supportsStartup.UseStartup(startupType);
            }

            return hostBuilder
                .ConfigureServices(services =>
                {
                    if (typeof(IStartup).IsAssignableFrom(startupType))
                    {
                        services.AddSingleton(typeof(IStartup), startupType);
                    }
                    else
                    {
                        services.AddSingleton(typeof(IStartup), sp =>
                        {
                            var hostingEnvironment = sp.GetRequiredService<IHostEnvironment>();
                            return new ConventionBasedStartup(StartupLoader.LoadMethods(sp, startupType, hostingEnvironment.EnvironmentName));
                        });
                    }
                });
        }

        /// <summary>
        /// Specify the startup type to be used by the web host.
        /// </summary>
        /// <param name="hostBuilder">The <see cref="IWebHostBuilder"/> to configure.</param>
        /// <typeparam name ="TStartup">The type containing the startup methods for the application.</typeparam>
        /// <returns>The <see cref="IWebHostBuilder"/>.</returns>
        public static IWebHostBuilder UseStartup<[DynamicallyAccessedMembers(StartupLinkerOptions.Accessibility)]TStartup>(this IWebHostBuilder hostBuilder) where TStartup : class
        {
            return hostBuilder.UseStartup(typeof(TStartup));
        }

        /// <summary>
        /// Configures the default service provider
        /// </summary>
        /// <param name="hostBuilder">The <see cref="IWebHostBuilder"/> to configure.</param>
        /// <param name="configure">A callback used to configure the <see cref="ServiceProviderOptions"/> for the default <see cref="IServiceProvider"/>.</param>
        /// <returns>The <see cref="IWebHostBuilder"/>.</returns>
        public static IWebHostBuilder UseDefaultServiceProvider(this IWebHostBuilder hostBuilder, Action<ServiceProviderOptions> configure)
        {
            return hostBuilder.UseDefaultServiceProvider((context, options) => configure(options));
        }

        /// <summary>
        /// Configures the default service provider
        /// </summary>
        /// <param name="hostBuilder">The <see cref="IWebHostBuilder"/> to configure.</param>
        /// <param name="configure">A callback used to configure the <see cref="ServiceProviderOptions"/> for the default <see cref="IServiceProvider"/>.</param>
        /// <returns>The <see cref="IWebHostBuilder"/>.</returns>
        public static IWebHostBuilder UseDefaultServiceProvider(this IWebHostBuilder hostBuilder, Action<WebHostBuilderContext, ServiceProviderOptions> configure)
        {
            // Light up the GenericWebHostBuilder implementation
            if (hostBuilder is ISupportsUseDefaultServiceProvider supportsDefaultServiceProvider)
            {
                return supportsDefaultServiceProvider.UseDefaultServiceProvider(configure);
            }

            return hostBuilder.ConfigureServices((context, services) =>
            {
                var options = new ServiceProviderOptions();
                configure(context, options);
                services.Replace(ServiceDescriptor.Singleton<IServiceProviderFactory<IServiceCollection>>(new DefaultServiceProviderFactory(options)));
            });
        }

        /// <summary>
        /// Adds a delegate for configuring the <see cref="IConfigurationBuilder"/> that will construct an <see cref="IConfiguration"/>.
        /// </summary>
        /// <param name="hostBuilder">The <see cref="IWebHostBuilder"/> to configure.</param>
        /// <param name="configureDelegate">The delegate for configuring the <see cref="IConfigurationBuilder" /> that will be used to construct an <see cref="IConfiguration" />.</param>
        /// <returns>The <see cref="IWebHostBuilder"/>.</returns>
        /// <remarks>
        /// The <see cref="IConfiguration"/> and <see cref="ILoggerFactory"/> on the <see cref="WebHostBuilderContext"/> are uninitialized at this stage.
        /// The <see cref="IConfigurationBuilder"/> is pre-populated with the settings of the <see cref="IWebHostBuilder"/>.
        /// </remarks>
        public static IWebHostBuilder ConfigureAppConfiguration(this IWebHostBuilder hostBuilder, Action<IConfigurationBuilder> configureDelegate)
        {
            return hostBuilder.ConfigureAppConfiguration((context, builder) => configureDelegate(builder));
        }

        /// <summary>
        /// Adds a delegate for configuring the provided <see cref="ILoggingBuilder"/>. This may be called multiple times.
        /// </summary>
        /// <param name="hostBuilder">The <see cref="IWebHostBuilder" /> to configure.</param>
        /// <param name="configureLogging">The delegate that configures the <see cref="ILoggingBuilder"/>.</param>
        /// <returns>The <see cref="IWebHostBuilder"/>.</returns>
        public static IWebHostBuilder ConfigureLogging(this IWebHostBuilder hostBuilder, Action<ILoggingBuilder> configureLogging)
        {
            return hostBuilder.ConfigureServices(collection => collection.AddLogging(configureLogging));
        }

        /// <summary>
        /// Adds a delegate for configuring the provided <see cref="LoggerFactory"/>. This may be called multiple times.
        /// </summary>
        /// <param name="hostBuilder">The <see cref="IWebHostBuilder" /> to configure.</param>
        /// <param name="configureLogging">The delegate that configures the <see cref="LoggerFactory"/>.</param>
        /// <returns>The <see cref="IWebHostBuilder"/>.</returns>
        public static IWebHostBuilder ConfigureLogging(this IWebHostBuilder hostBuilder, Action<WebHostBuilderContext, ILoggingBuilder> configureLogging)
        {
            return hostBuilder.ConfigureServices((context, collection) => collection.AddLogging(builder => configureLogging(context, builder)));
        }

        /// <summary>
        /// Configures the <see cref="IWebHostEnvironment.WebRootFileProvider"/> to use static web assets
        /// defined by referenced projects and packages.
        /// </summary>
        /// <param name="builder">The <see cref="IWebHostBuilder"/>.</param>
        /// <returns>The <see cref="IWebHostBuilder"/>.</returns>
        public static IWebHostBuilder UseStaticWebAssets(this IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, configBuilder) =>
            {
                StaticWebAssetsLoader.UseStaticWebAssets(context.HostingEnvironment, context.Configuration);
            });

            return builder;
        }
    }
```

### 2b. web host builder

#### 2.1 web host builder

##### 2.1.1 接口

```c#
public interface IWebHostBuilder
{   
    /* build web host */
    IWebHost Build();
    
    /* configure service */
    IWebHostBuilder ConfigureServices(
         Action<IServiceCollection> configureServices);
        
    IWebHostBuilder ConfigureServices(
        Action<WebHostBuilderContext, IServiceCollection> 
        	configureServices);
    
    /* configure (configuration builder) */
    IWebHostBuilder ConfigureAppConfiguration(
        Action<WebHostBuilderContext, IConfigurationBuilder> 
        	configureDelegate);
                       
    string? GetSetting(string key);        
    IWebHostBuilder UseSetting(string key, string? value);
}

```

##### 2.1.2 builder context

```c#
public class WebHostBuilderContext
{
    public IWebHostEnvironment HostingEnvironment { get; set; } = default!;        
    public IConfiguration Configuration { get; set; } = default!;
}

```

##### 2.1.3 实现

```c#
public class WebHostBuilder : IWebHostBuilder
{
    private readonly HostingEnvironment _hostingEnvironment;
    private readonly IConfiguration _config;
    private readonly WebHostBuilderContext _context;    
    private WebHostOptions? _options;
    
    // action -> configure service
    private Action<WebHostBuilderContext, IServiceCollection>? 
        _configureServices;
    // action -> configure (configuration builder)
    private Action<WebHostBuilderContext, IConfigurationBuilder>? 
        _configureAppConfigurationBuilder;

    private bool _webHostBuilt;
   
    public WebHostBuilder()
    {
        // 创建 hosting environment
        _hostingEnvironment = new HostingEnvironment();
        
        // 创建 configuration，
        // 读取环境变量
        _config = new ConfigurationBuilder()
            .AddEnvironmentVariables(prefix: "ASPNETCORE_")
            .Build();
        
        if (string.IsNullOrEmpty(GetSetting(WebHostDefaults.EnvironmentKey)))
        {
            // Try adding legacy environment keys, never remove these.
            UseSetting(
                WebHostDefaults
                	.EnvironmentKey, 
                Environment
                	.GetEnvironmentVariable("Hosting:Environment")
                		?? Environment.GetEnvironmentVariable("ASPNET_ENV"));
        }
        
        if (string.IsNullOrEmpty(GetSetting(WebHostDefaults.ServerUrlsKey)))
        {
            // Try adding legacy url key, never remove this.
            UseSetting(
                WebHostDefaults
                	.ServerUrlsKey, 
                Environment
                	.GetEnvironmentVariable("ASPNETCORE_SERVER.URLS"));
        }
        
        // 创建 web host builder context，
        // 指定 context.configuration 为当前 configuration
        _context = new WebHostBuilderContext
        {
            Configuration = _config
        };
    }
            
    public string GetSetting(string key)
    {
        return _config[key];
    }
            
    public IWebHostBuilder UseSetting(string key, string? value)
    {
        _config[key] = value;
        return this;
    }    
}

```

#### 2.2 配置builder

##### 2.2.1 configure service

```c#
public class WebHostBuilder : IWebHostBuilder
{
    public IWebHostBuilder ConfigureServices(
        Action<IServiceCollection> configureServices)
    {
        if (configureServices == null)
        {
            throw new ArgumentNullException(nameof(configureServices));
        }
        
        return ConfigureServices(
            (_, services) => configureServices(services));
    }
    
    public IWebHostBuilder ConfigureServices(
        Action<WebHostBuilderContext, IServiceCollection> 
        	configureServices)
    {
        _configureServices += configureServices;
        return this;
    }
}

```

##### 2.2.2 configure configuration builder

```c#
public class WebHostBuilder : IWebHostBuilder
{
    public IWebHostBuilder ConfigureAppConfiguration(
        Action<WebHostBuilderContext, IConfigurationBuilder> 
        	configureDelegate)
    {
        _configureAppConfigurationBuilder += configureDelegate;
        return this;
    }
}

```

#### 2.3 创建 web host

##### 2.3.1 build

```c#
public class WebHostBuilder : IWebHostBuilder
{
    public IWebHost Build()
    {
        if (_webHostBuilt)
        {
            throw new InvalidOperationException(
                Resources.WebHostBuilder_SingleInstance);
        }
        _webHostBuilt = true;
        
        /* a - 创建 hosting service provider */
        var hostingServices = 
            BuildCommonServices(out var hostingStartupErrors);
        var applicationServices = 
            hostingServices.Clone();
        var hostingServiceProvider = 
            GetProviderFromFactory(hostingServices);
        
        /* b - 如果没有禁用 status msg，输出过时信息*/
        /*
        if (!_options.SuppressStatusMessages)
        {
            // Warn about deprecated environment variables
            if (Environment
                	.GetEnvironmentVariable("Hosting:Environment") != null)
            {
                Console.WriteLine(
                    "The environment variable 'Hosting:Environment' 
                    "is obsolete and has been replaced 
                    "with 'ASPNETCORE_ENVIRONMENT'");
            }
            
            if (Environment
                	.GetEnvironmentVariable("ASPNET_ENV") != null)
            {
                Console.WriteLine(
                    "The environment variable 'ASPNET_ENV' 
                    "is obsolete and has been replaced 
                    "with 'ASPNETCORE_ENVIRONMENT'");
            }
            
            if (Environment
                	.GetEnvironmentVariable("ASPNETCORE_SERVER.URLS") != null)
            {
                Console.WriteLine(
                    "The environment variable 'ASPNETCORE_SERVER.URLS' 
                    "is obsolete and has been replaced 
                    "with 'ASPNETCORE_URLS'");
            }
        }
        */
        
        /* c - 添加 application service */
        AddApplicationServices(applicationServices, hostingServiceProvider);
        
        /* d - 创建 web host */
        var host = new WebHost(
            applicationServices,
            hostingServiceProvider,
            _options,
            _config,
            hostingStartupErrors);
        
        /* e - 初始化 web host */
        try
        {
            host.Initialize();
            
            // resolve configuration explicitly once to mark it as resolved within the
            // service provider, ensuring it will be properly disposed with the provider
            _ = host.Services.GetService<IConfiguration>();
            
            /* 解析 logger，记录重复加载 assembly 异常 */            
            var logger = host.Services.GetRequiredService<ILogger<WebHost>>();            
            // Warn about duplicate HostingStartupAssemblies
            var assemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var assemblyName in _options.GetFinalHostingStartupAssemblies())
            {
                if (!assemblyNames.Add(assemblyName))
                {
                    logger.LogWarning(
                        $"The assembly {assemblyName} was specified multiple times. 
                        "Hosting startup assemblies should only be specified once.");
                }
            }
            
            return host;
        }
        catch
        {
            // Dispose the host if there's a failure to initialize, this should dispose
            // services that were constructed until the exception was thrown
            host.Dispose();
            throw;
        }                
    }
}

```

##### 2.3.2 build common service

```c#
public class WebHostBuilder : IWebHostBuilder
{
    [MemberNotNull(nameof(_options))]
    private IServiceCollection BuildCommonServices(
        out AggregateException? hostingStartupErrors)
    {
        hostingStartupErrors = null;
        
        // 创建 web host options
        _options = new WebHostOptions(
            _config, 
            Assembly
            	.GetEntryAssembly()
            	?.GetName().Name 
            		?? string.Empty);
        
        /* 如果没有标记 prevent hosting startup，
           加载 hosting startup assembly，
           并配置 builder */        
        
        /* 配置 web hosting environment，
           hosting environment 在 builder 构造函数中创建；
           web host environment => builder context*/                
        
        /* 创建 service collection,
           注入 web host (builder) 组件服务 */
        var services = new ServiceCollection();
        // 注入 web host options
        services.AddSingleton(_options);
        // 注入 web hsot environment
        services.AddSingleton
            <IWebHostEnvironment>(_hostingEnvironment);
        // 注入 host environment
        services.AddSingleton
            <IHostEnvironment>(_hostingEnvironment);
        // 注入 builder context，
        // builder context 在 builder 构造函数中创建
    	services.AddSingleton(_context);
                                
        /* 创建并配置 configuration builder，
           构建 configuration，
           configuration => builder context */
        
        /* 注册（web）服务 */
        
        
        
        /* 注册 IStartup */
        
        
        _configureServices?.Invoke(_context, services);
        
        return services;
    }
}

```

###### 2.3.2.1 hosting startup assembly

* load hosting startup assembly

```c#
public class WebHostBuilder : IWebHostBuilder
{
    [MemberNotNull(nameof(_options))]
    private IServiceCollection BuildCommonServices(
        out AggregateException? hostingStartupErrors)
    {
        if (!_options.PreventHostingStartup)
        {
            var exceptions = new List<Exception>();
            
            // Execute the hosting startup assemblies
            foreach (var assemblyName in 
                     _options
                     	.GetFinalHostingStartupAssemblies()
                     	.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    // 加载程序集
                    var assembly = Assembly.Load(new AssemblyName(assemblyName));
                    
                    // 遍历程序集中的、
                    // 标记 hosting startup 特性的属性
                    foreach (var attribute in 
                             assembly.GetCustomAttributes<HostingStartupAttribute>())
                    {
                        // 创建 hosting startup 特性中的 startup type 类
                        var hostingStartup = 
                            (IHostingStartup)Activator
                            	.CreateInstance(attribute.HostingStartupType)!;
                        
                        hostingStartup.Configure(this);
                    }
                }
                catch (Exception ex)
                {
                    // Capture any errors that happen during startup
                    exceptions.Add(
                        new InvalidOperationException(
                            $"Startup assembly {assemblyName} failed to execute. 
                            "See the inner exception for more details.", 
                            ex));
                }
            }
            
            if (exceptions.Count > 0)
            {
                hostingStartupErrors = new AggregateException(exceptions);
            }
        }
    }
}

```

* hosting startup attribute

```c#
[AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = true)]
public sealed class HostingStartupAttribute : Attribute
{    
    public HostingStartupAttribute(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicParameterlessConstructor)] 
        Type hostingStartupType)
    {
        // 如果 hosting startup type 为null，抛出异常
        if (hostingStartupType == null)
        {
            throw new ArgumentNullException(nameof(hostingStartupType));
        }
        // 如果 hosting startup type 没有实现 IHostingStartup 接口，
        // 抛出异常
        if (!typeof(IHostingStartup).IsAssignableFrom(hostingStartupType))
        {
            throw new ArgumentException(
                $@"""{hostingStartupType}"" 
                does not implement {typeof(IHostingStartup)}.", 
                nameof(hostingStartupType));
        }
        
        HostingStartupType = hostingStartupType;
    }
    
    
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes
        	.PublicParameterlessConstructor)]
    public Type HostingStartupType { get; }
}

```

* IHostingStartup

```c#
public interface IHostingStartup
{    
    void Configure(IWebHostBuilder builder);
}

```

###### 2.3.2.2 配置 host environm

```c#
public class WebHostBuilder : IWebHostBuilder
{
    [MemberNotNull(nameof(_options))]
    private IServiceCollection BuildCommonServices(
        out AggregateException? hostingStartupErrors)
    {
        var contentRootPath = 
            ResolveContentRootPath(
            	_options.ContentRootPath, 
            	AppContext.BaseDirectory);
        
        // Initialize the hosting environment
        ((IWebHostEnvironment)_hostingEnvironment)
        	.Initialize(
                contentRootPath, 
                _options);
        
        // hosting envrionment -> builder context
        _context.HostingEnvironment = _hostingEnvironment;
    }
    
    private string ResolveContentRootPath(
        string contentRootPath, 
        string basePath)
    {
        if (string.IsNullOrEmpty(contentRootPath))
        {
            return basePath;
        }
        if (Path.IsPathRooted(contentRootPath))
        {
            return contentRootPath;
        }
        return Path.Combine(Path.GetFullPath(basePath), contentRootPath);
    }
}

```

###### 2.3.2.3 配置 configuration

```c#
public class WebHostBuilder : IWebHostBuilder
{
    [MemberNotNull(nameof(_options))]
    private IServiceCollection BuildCommonServices(
        out AggregateException? hostingStartupErrors)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(_hostingEnvironment.ContentRootPath)
            .AddConfiguration(_config, shouldDisposeConfiguration: true);
        
        _configureAppConfigurationBuilder?.Invoke(_context, builder);
        
        var configuration = builder.Build();
        // register configuration as factory to make it dispose with the service provider
        services.AddSingleton
            <IConfiguration>(_ => configuration);
        
        // configuratino -> builder context
        _context.Configuration = configuration;
    }
}

```

###### 2.3.2.4 注入（web）服务

```c#
public class WebHostBuilder : IWebHostBuilder
{
    [MemberNotNull(nameof(_options))]
    private IServiceCollection BuildCommonServices(
        out AggregateException? hostingStartupErrors)
    {
        // 创建并注册 diagnostic listener, diagnostic source               
        var listener = new DiagnosticListener("Microsoft.AspNetCore");
        services.AddSingleton<DiagnosticListener>(listener);
        services.AddSingleton<DiagnosticSource>(listener);    
        
        // application builer factory
        services.AddTransient
            <IApplicationBuilderFactory, ApplicationBuilderFactory>();
        
        // http context factory
        services.AddTransient
            <IHttpContextFactory, DefaultHttpContextFactory>();
        
        // middleware factory
        services.AddScoped
            <IMiddlewareFactory, MiddlewareFactory>();
        
        // options
        services.AddOptions();
        
        // logging
        services.AddLogging();        
        
        // service provider factory
        services.AddTransient
            <IServiceProviderFactory<IServiceCollection>, 
        	 DefaultServiceProviderFactory>();        
    }
}

```

###### 2.3.2.5 注入 IStartup

```c#
public class WebHostBuilder : IWebHostBuilder
{
    [MemberNotNull(nameof(_options))]
    private IServiceCollection BuildCommonServices(
        out AggregateException? hostingStartupErrors)
    {
        if (!string.IsNullOrEmpty(_options.StartupAssembly))
        {
            try
            {
                // 从 web host options 解析 startup type
                var startupType = 
                    StartupLoader.FindStartupType(
                    	_options.StartupAssembly, 
                    	_hostingEnvironment.EnvironmentName);
                
                // startup type 实现了 IStartup 接口，直接注入
                if (typeof(IStartup).IsAssignableFrom(startupType))
                {
                    services.AddSingleton(typeof(IStartup), startupType);
                }
                // 否则，创建 convention based startup
                else
                {
                    services.AddSingleton(
                        typeof(IStartup), 
                        sp =>
                        {
                            var hostingEnvironment = 
                                sp.GetRequiredService<IHostEnvironment>();
                            var methods = 
                                StartupLoader.LoadMethods(
                                	sp, 
                                	startupType, 
                                	hostingEnvironment.EnvironmentName);
                            return new ConventionBasedStartup(methods);
                        });
                }
            }
            catch (Exception ex)
            {
                var capture = ExceptionDispatchInfo.Capture(ex);
                services.AddSingleton<IStartup>(_ =>
                	{
                        capture.Throw();
                        return null;
                    });
            }
        }
    }
}

```

##### 2.3.3 get service provider from factory

```c#
public class WebHostBuilder : IWebHostBuilder
{
    IServiceProvider GetProviderFromFactory(IServiceCollection collection)
    {
        var provider = collection.BuildServiceProvider();
        var factory = provider.GetService<IServiceProviderFactory<IServiceCollection>>();
        
        if (factory != null && 
            !(factory is DefaultServiceProviderFactory))
        {
            using (provider)
            {
                return factory.CreateServiceProvider(factory.CreateBuilder(collection));
            }
        }
        
        return provider;
    }
}

```

##### 2.3.4 add application service

```c#
public class WebHostBuilder : IWebHostBuilder
{
    private void AddApplicationServices(
        IServiceCollection services, 
        IServiceProvider hostingServiceProvider)
    {
        /* 从 host service provider 解析 diagnostic listener */
        // We are forwarding services from hosting container 
        // so hosting container can still manage their 
        // lifetime (disposal) shared instances with application services.
        // NOTE: This code overrides original services lifetime. 
        // Instances would always be singleton in application container.
        var listener = hostingServiceProvider.GetService<DiagnosticListener>();
        
        // 注册 listener 为 diagnostic listener
        services.Replace(
            ServiceDescriptor.Singleton(
                typeof(DiagnosticListener), 
                listener!));
        
        // 注册 listener 为 diagnostic source
        services.Replace(
            ServiceDescriptor.Singleton(
                typeof(DiagnosticSource), 
                listener!));
    }
}

```

#### 2.4 扩展方法

##### 2.4.1 configure application builder

```c#
public static class WebHostBuilderExtensions
{
    public static IWebHostBuilder Configure(
        this IWebHostBuilder hostBuilder, 
        Action<IApplicationBuilder> configureApp)
    {
        return hostBuilder.Configure(
            (_, app) => configureApp(app), 
            configureApp
            	.GetMethodInfo()
            	.DeclaringType
            	!.Assembly.GetName().Name!);
    }
    
    public static IWebHostBuilder Configure(
        this IWebHostBuilder hostBuilder, 
        Action<WebHostBuilderContext, IApplicationBuilder> configureApp)
    {
        return hostBuilder.Configure(
            configureApp, 
            configureApp
            	.GetMethodInfo()
            	.DeclaringType
            	!.Assembly.GetName().Name!);
    }
    
    private static IWebHostBuilder Configure(
        this IWebHostBuilder hostBuilder, 
        Action<WebHostBuilderContext, IApplicationBuilder> configureApp, 
        string startupAssemblyName)
    {
        if (configureApp == null)
        {
            throw new ArgumentNullException(nameof(configureApp));
        }
        
        hostBuilder.UseSetting(
            WebHostDefaults.ApplicationKey, 
            startupAssemblyName);
        
        // Light up the ISupportsStartup implementation
        if (hostBuilder is ISupportsStartup supportsStartup)
        {
            return supportsStartup.Configure(configureApp);
        }
        
        return hostBuilder.ConfigureServices((context, services) =>
        	{
                services.AddSingleton<IStartup>(sp =>
                	{
                        return new DelegateStartup(
                            sp.GetRequiredService
                            	<IServiceProviderFactory<IServiceCollection>>(), 
                            (app => configureApp(context, app)));
                    });
            });
    }
}

```

##### 2.4.2 use startup

###### 2.4.2.1 by startup

```c#
public static class WebHostBuilderExtensions
{
    public static IWebHostBuilder UseStartup
        <[DynamicallyAccessedMembers(
            StartupLinkerOptions.Accessibility)]TStartup>(
        this IWebHostBuilder hostBuilder) where TStartup : class
    {
        return hostBuilder.UseStartup(typeof(TStartup));
    }
    
    public static IWebHostBuilder UseStartup(
        this IWebHostBuilder hostBuilder, 
        [DynamicallyAccessedMembers(
            StartupLinkerOptions.Accessibility)] Type startupType)
    {
        if (startupType == null)
        {
            throw new ArgumentNullException(nameof(startupType));
        }
        
        var startupAssemblyName = startupType.Assembly.GetName().Name;
        
        hostBuilder.UseSetting(WebHostDefaults.ApplicationKey, startupAssemblyName);
        
        // Light up the GenericWebHostBuilder implementation
        if (hostBuilder is ISupportsStartup supportsStartup)
        {
            return supportsStartup.UseStartup(startupType);
        }
        
        return hostBuilder
            .ConfigureServices(services =>
                {
                    if (typeof(IStartup).IsAssignableFrom(startupType))
                    {
                        services.AddSingleton(typeof(IStartup), startupType);
                    }
                    else
                    {
                        services.AddSingleton(typeof(IStartup), sp =>
                        {
                            var hostingEnvironment = 
                                sp.GetRequiredService<IHostEnvironment>();
                            return new ConventionBasedStartup(
                                StartupLoader.LoadMethods(
                                    sp, 
                                    startupType, 
                                    hostingEnvironment.EnvironmentName));
                        });
                    }
                });
    }
}
```

###### 2.4.2.2 with startup factory

```c#
public static class WebHostBuilderExtensions
{ 
    public static IWebHostBuilder 
        UseStartup<[DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods)]TStartup>(
        		this IWebHostBuilder hostBuilder, 
        		Func<WebHostBuilderContext, TStartup> startupFactory) 
        	where TStartup : class
    {
        if (startupFactory == null)
        {
            throw new ArgumentNullException(nameof(startupFactory));
        }
        
        var startupAssemblyName = startupFactory
            .GetMethodInfo()
            .DeclaringType
            !.Assembly.GetName().Name;
        
        hostBuilder.UseSetting(
            WebHostDefaults.ApplicationKey, 
            startupAssemblyName);
        
        // Light up the GenericWebHostBuilder implementation
        if (hostBuilder is ISupportsStartup supportsStartup)
        {
            return supportsStartup.UseStartup(startupFactory);
        }
        
        return hostBuilder
            .ConfigureServices((context, services) =>
                {
                    services.AddSingleton(typeof(IStartup), sp =>
                    {
                        var instance = startupFactory(context) 
                            ?? throw new InvalidOperationException(
                            	"The specified factory returned null startup instance.");

                        var hostingEnvironment = sp.GetRequiredService<IHostEnvironment>();

                        // Check if the instance implements IStartup before wrapping
                        if (instance is IStartup startup)
                        {
                            return startup;
                        }

                        return new ConventionBasedStartup(
                            StartupLoader.LoadMethods(
                                sp, 
                                instance.GetType(), 
                                hostingEnvironment.EnvironmentName, 
                                instance));
                    });
                }); 
	}
}
    
```

##### 2.4.3 use default service provider

```c#
public static class WebHostBuilderExtensions
{
    public static IWebHostBuilder UseDefaultServiceProvider(
        this IWebHostBuilder hostBuilder, 
        Action<ServiceProviderOptions> configure)
    {
        return hostBuilder.UseDefaultServiceProvider(
            (context, options) => configure(options));
    }
    
        
    public static IWebHostBuilder UseDefaultServiceProvider(
        this IWebHostBuilder hostBuilder, 
        Action<WebHostBuilderContext, ServiceProviderOptions> configure)
    {
        // Light up the GenericWebHostBuilder implementation
        if (hostBuilder is ISupportsUseDefaultServiceProvider 
            supportsDefaultServiceProvider)
        {
            return supportsDefaultServiceProvider
                .UseDefaultServiceProvider(configure);
        }
        
        return hostBuilder.ConfigureServices((context, services) =>
        	{
                var options = new ServiceProviderOptions();
                configure(context, options);
                
                services.Replace(
                    ServiceDescriptor.Singleton
                    	<IServiceProviderFactory<IServiceCollection>>(
                            new DefaultServiceProviderFactory(options)));
            });
    }
}

```

##### 2.4.4 configure app configuration

```c#
public static class WebHostBuilderExtensions
{
    public static IWebHostBuilder ConfigureAppConfiguration(
        this IWebHostBuilder hostBuilder, 
        Action<IConfigurationBuilder> configureDelegate)
    {
        return hostBuilder.ConfigureAppConfiguration(
            (context, builder) => configureDelegate(builder));
    }
}

```

##### 2.4.5  configure logging

```c#
public static class WebHostBuilderExtensions
{
    public static IWebHostBuilder ConfigureLogging(
        this IWebHostBuilder hostBuilder, 
        Action<ILoggingBuilder> configureLogging)
    {
        return hostBuilder.ConfigureServices(collection => 
        	collection.AddLogging(configureLogging));
    }
    
    
    public static IWebHostBuilder ConfigureLogging(
        this IWebHostBuilder hostBuilder, 
        Action<WebHostBuilderContext, ILoggingBuilder> configureLogging)
    {
        return hostBuilder.ConfigureServices((context, collection) =>              
           	collection.AddLogging(builder => 
            	configureLogging(context, builder)));
    }
}

```

##### 2.4.6 use static web assets

```c#
public static class WebHostBuilderExtensions
{                                                                 
    public static IWebHostBuilder UseStaticWebAssets(
        this IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, configBuilder) =>
        	{
                StaticWebAssetsLoader.UseStaticWebAssets(
                    context.HostingEnvironment, 
                    context.Configuration);
            });
        
        return builder;
    }
}

```

### 2c. with  generic host

#### 2.1 接口

##### 2.1.1 support startup

```c#
internal interface ISupportsStartup
{
    IWebHostBuilder Configure(
        Action<WebHostBuilderContext, IApplicationBuilder> configure);
    
    IWebHostBuilder UseStartup
        ([DynamicallyAccessedMembers(
            StartupLinkerOptions.Accessibility)] 
         Type startupType);
    
    IWebHostBuilder UseStartup
        <[DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicMethods)]TStartup>(
        Func<WebHostBuilderContext, TStartup> startupFactory);
}

```

##### 2.1.2 support sue default service provider

```c#
internal interface ISupportsUseDefaultServiceProvider
{
    IWebHostBuilder UseDefaultServiceProvider(
        Action<WebHostBuilderContext, ServiceProviderOptions> configure);
}

```

#### 2.2 generic web host builder

```c#
internal class GenericWebHostBuilder : 
	IWebHostBuilder, 
	ISupportsStartup, 
	ISupportsUseDefaultServiceProvider
{
    private readonly IHostBuilder _builder;
    private readonly IConfiguration _config;
    private object? _startupObject;
    private readonly object _startupKey = new object();
    
    private AggregateException? _hostingStartupErrors;
    private HostingStartupWebHostBuilder? _hostingStartupWebHostBuilder;
        
    // ...
}
```

##### 2.2.1 构造函数

```c#
internal class GenericWebHostBuilder
{
    public GenericWebHostBuilder(
        IHostBuilder builder, 
        WebHostBuilderOptions options)
    {
        /* 注入 host builder */
        _builder = builder;
        
        /* 加载 configuration */
                        
        /* 注册服务*/    
        
        /* 加载 startup */
    }
}

```

###### 2.2.1.1 加载 configuration

```c#
internal class GenericWebHostBuilder
{
    public GenericWebHostBuilder(
        IHostBuilder builder, 
        WebHostBuilderOptions options)
    {
        // 避免异常
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection();
        
        // 如果标记抑制 environment configuration，
        // 加载环境变量 ASPNETCORE_
        if (!options.SuppressEnvironmentConfiguration)
        {
            configBuilder.AddEnvironmentVariables(prefix: "ASPNETCORE_");
        }
        
        _config = configBuilder.Build();
        
        // 配置 host configuration，
        // 注入创建/加载（如果没有标记抑制环境变量）configuration
        _builder.ConfigureHostConfiguration(config =>
       	{
            config.AddConfiguration(_config);
            
            // We do this super early but still late enough 
            // that we can process the configuration
            // wired up by calls to UseSetting
            ExecuteHostingStartups();
        });
                
        // 如果 hosting startup web host builder 存在，
        // （在 execute hosting startup 中获取），
        // 配置host app configuration，
        // IHostingStartup needs to be executed before 
        // any direct methods on the builder so register these callbacks first
        _builder.ConfigureAppConfiguration((context, configurationBuilder) =>
        {
            if (_hostingStartupWebHostBuilder != null)
            {
                var webhostContext = GetWebHostBuilderContext(context);
                _hostingStartupWebHostBuilder
                    .ConfigureAppConfiguration(
                    	webhostContext, 
                    	configurationBuilder);
            }
        });
    }
}

```

###### 2.2.1.2 注册服务

```c#
internal class GenericWebHostBuilder
{
    public GenericWebHostBuilder(
        IHostBuilder builder, 
        WebHostBuilderOptions options)
    {
        _builder.ConfigureServices((context, services) =>
        {
            // 解析 web host builder context
            var webhostContext = GetWebHostBuilderContext(context);
            // 解析 web host options
            var webHostOptions = (WebHostOptions)context
                .Properties[typeof(WebHostOptions)];
            
            /* 注册 hosting environment */
            // Add the IHostingEnvironment and IApplicationLifetime 
            // from Microsoft.AspNetCore.Hosting
            services.AddSingleton(
                webhostContext.HostingEnvironment);
            
            /* 注册 application lifetime */
            services.AddSingleton
                <IApplicationLifetime, 
            	 GenericWebHostApplicationLifetime>();

            /* 注入 generic web host service options，
               包含解析到的 web host options */
            services.Configure<GenericWebHostServiceOptions>(options =>
            {
                // Set the options
                options.WebHostOptions = webHostOptions;
                // Store and forward any startup errors
                options.HostingStartupExceptions = _hostingStartupErrors;
            });
            
            /* 注册服务 */
            // REVIEW: This is bad since we don't own this type. 
            // Anybody could add one of these and it would mess things up
            // We need to flow this differently
            var listener = new DiagnosticListener("Microsoft.AspNetCore");
            services.TryAddSingleton<DiagnosticListener>(listener);
            services.TryAddSingleton<DiagnosticSource>(listener);
            
            services.TryAddSingleton
                <IHttpContextFactory, DefaultHttpContextFactory>();
            services.TryAddScoped
                <IMiddlewareFactory, MiddlewareFactory>();
            services.TryAddSingleton
                <IApplicationBuilderFactory, ApplicationBuilderFactory>();
            
            /* 注册 hosting startup web host builder 中的服务，
               如果有（即 execute hosting startup 后获取）*/
            // IMPORTANT: This needs to run *before* 
            // direct calls on the builder (like UseStartup)
            _hostingStartupWebHostBuilder
                ?.ConfigureServices(webhostContext, services);
            
            /* 加载 startup type */
            // Support UseStartup(assemblyName)
            if (!string.IsNullOrEmpty(webHostOptions.StartupAssembly))
            {
                try
                {
                    var startupType = StartupLoader.FindStartupType(
                        webHostOptions.StartupAssembly,
                        webhostContext.HostingEnvironment.EnvironmentName);
                    
                    UseStartup(startupType, context, services);
                }
                catch (Exception ex) when (webHostOptions.CaptureStartupErrors)
                {
                    var capture = ExceptionDispatchInfo.Capture(ex);
                    
                    services.Configure<GenericWebHostServiceOptions>(options =>
                    {
                        options.ConfigureApplication = app =>
                        {
                            // Throw if there was any errors initializing startup
                            capture.Throw();
                        };
                    });
                }
            }
        });
    }
}

```

##### 2.2.2 构造函数组件

###### 2.2.2.1 get web host builder context

```c#
internal class GenericWebHostBuilder
{
    private WebHostBuilderContext GetWebHostBuilderContext(
        HostBuilderContext context)
    {
        if (!context.Properties.TryGetValue(
            	typeof(WebHostBuilderContext), 
            	out var contextVal))
        {
            var options = new WebHostOptions(
                context.Configuration, 
                Assembly.GetEntryAssembly()
                	?.GetName().Name ?? string.Empty);
            
            var webHostBuilderContext = new WebHostBuilderContext
            {
                Configuration = context.Configuration,
                HostingEnvironment = new HostingEnvironment(),
            };
            webHostBuilderContext
                .HostingEnvironment
                .Initialize(
                	context.HostingEnvironment.ContentRootPath, 
                	options);
            
            context.Properties[typeof(WebHostBuilderContext)] = webHostBuilderContext;
            context.Properties[typeof(WebHostOptions)] = options;
            
            return webHostBuilderContext;
        }
        
        // Refresh config, it's periodically updated/replaced
        var webHostContext = (WebHostBuilderContext)contextVal;
        webHostContext.Configuration = context.Configuration;
        
        return webHostContext;
    }
}

```

###### 2.2.2.2 exectue hosting startup

```c#
internal class GenericWebHostBuilder
{
    private void ExecuteHostingStartups()
    {
        var webHostOptions = new WebHostOptions(
            _config, 
            Assembly.GetEntryAssembly()
            	?.GetName().Name ?? string.Empty);
        
        if (webHostOptions.PreventHostingStartup)
        {
            return;
        }
        
        var exceptions = new List<Exception>();
        
        // 创建 hosting startup web host builder
        _hostingStartupWebHostBuilder = new HostingStartupWebHostBuilder(this);
        
        // Execute the hosting startup assemblies
        foreach (var assemblyName in 
                 webHostOptions
                 	.GetFinalHostingStartupAssemblies()
                 	.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var assembly = Assembly.Load(new AssemblyName(assemblyName));
                
                foreach (var attribute in 
                         assembly.GetCustomAttributes<HostingStartupAttribute>())
                {
                    var hostingStartup = (IHostingStartup)Activator
                        .CreateInstance(attribute.HostingStartupType)!;
                    hostingStartup.Configure(_hostingStartupWebHostBuilder);
                }
            }
            catch (Exception ex)
            {
                // Capture any errors that happen during startup
                exceptions.Add(
                    new InvalidOperationException(
                        $"Startup assembly {assemblyName} failed to execute. 
                        "See the inner exception for more details.", 
                        ex));
            }
        }
        
        if (exceptions.Count > 0)
        {
            _hostingStartupErrors = new AggregateException(exceptions);
        }
    }
}

```

##### 2.2.3 web host builder 接口实现

```c#
internal class GenericWebHostBuilder
{
    public IWebHost Build()
    {
        throw new NotSupportedException(
            $"Building this implementation of {nameof(IWebHostBuilder)} is not supported.");
    }
    
    public IWebHostBuilder ConfigureAppConfiguration(
        Action<WebHostBuilderContext, IConfigurationBuilder> configureDelegate)
    {
        _builder.ConfigureAppConfiguration((context, builder) =>
    	{
            var webhostBuilderContext = GetWebHostBuilderContext(context);
            configureDelegate(webhostBuilderContext, builder);
        });
        
        return this;
    }
    
    public IWebHostBuilder ConfigureServices(
        Action<IServiceCollection> configureServices)
    {
        return ConfigureServices((context, services) => 
        	configureServices(services));
    }
    
    public IWebHostBuilder ConfigureServices(
        Action<WebHostBuilderContext, IServiceCollection> configureServices)
    {
        _builder.ConfigureServices((context, builder) =>
        {
            var webhostBuilderContext = GetWebHostBuilderContext(context);
            configureServices(webhostBuilderContext, builder);
        });
        
        return this;
    }
    
    public string GetSetting(string key)
    {
        return _config[key];
    }
    
    public IWebHostBuilder UseSetting(string key, string? value)
    {
        _config[key] = value;
        return this;
    }
}

```



##### 2.2.4 support startup 接口实现

###### 2.2.4.1 configure

```c#
internal class GenericWebHostBuilder
{
    public IWebHostBuilder Configure(
            Action<WebHostBuilderContext, IApplicationBuilder> configure)
        {
            // Clear the startup type
            _startupObject = configure;

            _builder.ConfigureServices((context, services) =>
            {
                if (object.ReferenceEquals(_startupObject, configure))
                {
                    services.Configure<GenericWebHostServiceOptions>(options =>
                    {
                        var webhostBuilderContext = GetWebHostBuilderContext(context);
                        options.ConfigureApplication = 
                            app => configure(webhostBuilderContext, app);
                    });
                }
            });

            return this;
        }

}

```

###### 2.2.4.2 use startup

```c#
internal class GenericWebHostBuilder
{
    public IWebHostBuilder UseStartup(
        [DynamicallyAccessedMembers(
            StartupLinkerOptions.Accessibility)] Type startupType)
    {
        // UseStartup can be called multiple times. Only run the last one.
        _startupObject = startupType;
        
        _builder.ConfigureServices((context, services) =>
        {
            // Run this delegate if the startup type matches
            if (object.ReferenceEquals(_startupObject, startupType))
            {
                UseStartup(startupType, context, services);
            }
        });
        
        return this;
    }
    
    public IWebHostBuilder UseStartup
        <[DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods)]TStartup>(
        Func<WebHostBuilderContext, TStartup> startupFactory)
    {
        // Clear the startup type
        _startupObject = startupFactory;
        
        _builder.ConfigureServices((context, services) =>
        {
            // UseStartup can be called multiple times. Only run the last one.
            if (object.ReferenceEquals(_startupObject, startupFactory))
            {
                var webHostBuilderContext = GetWebHostBuilderContext(context);
                var instance = startupFactory(webHostBuilderContext) 
                    ?? throw new InvalidOperationException(
                    	"The specified factory returned null startup instance.");
                
                UseStartup(instance.GetType(), context, services, instance);
            }
        });
        
        return this;
    }
}

```

###### 2.2.4.3 use startup real did

```c#
internal class GenericWebHostBuilder
{
    [UnconditionalSuppressMessage(
        "ReflectionAnalysis", 
        "IL2006:UnrecognizedReflectionPattern", 
        Justification = "We need to call a generic method on IHostBuilder.")]
    private void UseStartup(
        [DynamicallyAccessedMembers(
            StartupLinkerOptions.Accessibility)] Type startupType, 
        HostBuilderContext context, 
        IServiceCollection services, 
        object? instance = null)
    {
        var webHostBuilderContext = GetWebHostBuilderContext(context);
        var webHostOptions = (WebHostOptions)context.Properties[typeof(WebHostOptions)];
        
        ExceptionDispatchInfo? startupError = null;
        ConfigureBuilder? configureBuilder = null;
        
        try
        {
            // We cannot support methods that 
            // return IServiceProvider as that is terminal 
            // and we need ConfigureServices to compose
            if (typeof(IStartup).IsAssignableFrom(startupType))
            {
                throw new NotSupportedException($"{typeof(IStartup)} isn't supported");
            }
            if (StartupLoader
                .HasConfigureServicesIServiceProviderDelegate(
                    startupType, 
                    context.HostingEnvironment.EnvironmentName))
            {
                throw new NotSupportedException(
                    $"ConfigureServices returning an 
                    "{typeof(IServiceProvider)} isn't supported.");
            }
            
            instance ??= ActivatorUtilities.CreateInstance(
                new HostServiceProvider(webHostBuilderContext), 
                startupType);
            
            context.Properties[_startupKey] = instance;
            
            // Startup.ConfigureServices
            var configureServicesBuilder = 
                StartupLoader.FindConfigureServicesDelegate(
                	startupType, 
                	context.HostingEnvironment.EnvironmentName);
            var configureServices = configureServicesBuilder.Build(instance);
            
            configureServices(services);
            
            // REVIEW: We're doing this in the callback 
            // so that we have access to the hosting environment
            // Startup.ConfigureContainer
            var configureContainerBuilder = 
                StartupLoader.FindConfigureContainerDelegate(
	                startupType, 
                	context.HostingEnvironment.EnvironmentName);
            if (configureContainerBuilder.MethodInfo != null)
            {
                var containerType = configureContainerBuilder.GetContainerType();
                // Store the builder in the property bag
                _builder.Properties[
                    typeof(ConfigureContainerBuilder)] = configureContainerBuilder;
                
                var actionType = 
                    typeof(Action<,>)
                    	.MakeGenericType(
                    		typeof(HostBuilderContext), 
                    		containerType);
                
                // Get the private ConfigureContainer method 
                // on this type then close over the container type
                var configureCallback = 
                    typeof(GenericWebHostBuilder)
                    	.GetMethod(
                    		nameof(ConfigureContainerImpl), 
                    		BindingFlags.NonPublic | BindingFlags.Instance)
                    	!.MakeGenericMethod(containerType)                                     			               .CreateDelegate(actionType, this);

                // _builder.ConfigureContainer<T>(ConfigureContainer);
                typeof(IHostBuilder)
                    .GetMethod(nameof(IHostBuilder.ConfigureContainer))
                    !.MakeGenericMethod(containerType)
                    .InvokeWithoutWrappingExceptions(
                    	_builder, 
                    	new object[] { configureCallback });
                }

                // Resolve Configure after calling ConfigureServices and ConfigureContainer
                configureBuilder = 
                    StartupLoader.FindConfigureDelegate(
                    	startupType, 
                    	context.HostingEnvironment.EnvironmentName);
        }
        catch (Exception ex) when (webHostOptions.CaptureStartupErrors)
        {
            startupError = ExceptionDispatchInfo.Capture(ex);
        }
        
        // Startup.Configure
        services.Configure<GenericWebHostServiceOptions>(options =>
        {
            options.ConfigureApplication = app =>
            {
                // Throw if there was any errors initializing startup
                startupError?.Throw();
                
                // Execute Startup.Configure
                if (instance != null && configureBuilder != null)
                {
                    configureBuilder.Build(instance)(app);
                }
            };
        });
    }
}

```

###### 2.2.4.4 component in use startup

```c#
internal class GenericWebHostBuilder
{
    private void ConfigureContainerImpl<TContainer>(
            HostBuilderContext context, 
            TContainer container) 
            	where TContainer : notnull
    {
        var instance = context.Properties[_startupKey];
        var builder = (ConfigureContainerBuilder)context
            .Properties[typeof(ConfigureContainerBuilder)];
        builder.Build(instance)(container);
    }
    
    // This exists just so that we can use 
    // ActivatorUtilities.CreateInstance on the Startup class
    private class HostServiceProvider : IServiceProvider
    {
        private readonly WebHostBuilderContext _context;
        
        public HostServiceProvider(WebHostBuilderContext context)
        {
            _context = context;
        }
        
        public object? GetService(Type serviceType)
        {
            // The implementation of the HostingEnvironment supports both interfaces
#pragma warning disable CS0618 // Type or member is obsolete
    		if (serviceType == 
                	typeof(Microsoft.Extensions.Hosting.IHostingEnvironment) || 
                serviceType == 
                	typeof(Microsoft.AspNetCore.Hosting.IHostingEnvironment) ||
#pragma warning restore CS0618 // Type or member is obsolete
                serviceType == typeof(IWebHostEnvironment) || 
                serviceType == typeof(IHostEnvironment))
            {
                return _context.HostingEnvironment;
            }
            
            if (serviceType == typeof(IConfiguration))
            {
                return _context.Configuration;
            }
            
            return null;
        }
    }
}

```



##### 2.2.5 support default service provider 接口实现

```c#
internal class GenericWebHostBuilder
{
    public IWebHostBuilder UseDefaultServiceProvider(
        Action<WebHostBuilderContext, ServiceProviderOptions> configure)
    {
        _builder.UseServiceProviderFactory(context =>
        {
            var webHostBuilderContext = GetWebHostBuilderContext(context);
            var options = new ServiceProviderOptions();
            configure(webHostBuilderContext, options);
            return new DefaultServiceProviderFactory(options);
        });
        
        return this;
    }            
}

```











##### 2.5.1 host 扩展 configure web host

```c#
public static class GenericHostWebHostBuilderExtensions
{
    
    public static IHostBuilder ConfigureWebHost(
        this IHostBuilder builder, 
        Action<IWebHostBuilder> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }
        
        return builder.ConfigureWebHost(configure, _ => { });
    }
    
    
    public static IHostBuilder ConfigureWebHost(
        this IHostBuilder builder, 
        Action<IWebHostBuilder> configure, 
        Action<WebHostBuilderOptions> configureWebHostBuilder)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }
        
        if (configureWebHostBuilder is null)
        {
            throw new ArgumentNullException(nameof(configureWebHostBuilder));
        }
        
        var webHostBuilderOptions = new WebHostBuilderOptions();
        configureWebHostBuilder(webHostBuilderOptions);
        
        var webhostBuilder = new GenericWebHostBuilder(
            builder, 
            webHostBuilderOptions);
        
        configure(webhostBuilder);
        
        builder.ConfigureServices((context, services) => 
        	services.AddHostedService<GenericWebHostService>());
        
        return builder;
    }
}

```

#### 2.3 hosting startup web host builder

```c#
internal class HostingStartupWebHostBuilder : 
	IWebHostBuilder, 
	ISupportsStartup, 
	ISupportsUseDefaultServiceProvider
{
    private readonly GenericWebHostBuilder _builder;
    private Action<WebHostBuilderContext, IConfigurationBuilder>? 
        _configureConfiguration;
    private Action<WebHostBuilderContext, IServiceCollection>? 
        _configureServices;
    
    public HostingStartupWebHostBuilder(GenericWebHostBuilder builder)
    {
        _builder = builder;
    }
            
    public void ConfigureServices(
        WebHostBuilderContext context, 
        IServiceCollection services)
    {
        _configureServices?.Invoke(context, services);
    }
    
    public void ConfigureAppConfiguration(
        WebHostBuilderContext context, 
        IConfigurationBuilder builder)
    {
        _configureConfiguration?.Invoke(context, builder);
    }                
}

```

##### 2.3.1 web host builder 接口实现

```c#
internal class HostingStartupWebHostBuilder
{
    public IWebHost Build()
    {
        throw new NotSupportedException(
            $"Building this implementation of {nameof(IWebHostBuilder)} is not supported.");
    }
    
    public IWebHostBuilder ConfigureAppConfiguration(
        Action<WebHostBuilderContext, IConfigurationBuilder> configureDelegate)
    {
        _configureConfiguration += configureDelegate;
        return this;
    }
    
    public IWebHostBuilder ConfigureServices(
        Action<IServiceCollection> configureServices)
    {
        return ConfigureServices((context, services) => 
                                 configureServices(services));
    }
    
    public IWebHostBuilder ConfigureServices(
        Action<WebHostBuilderContext, IServiceCollection> configureServices)
    {
        _configureServices += configureServices;
        return this;
    }
    
    public string GetSetting(string key) => _builder.GetSetting(key);
    
    public IWebHostBuilder UseSetting(string key, string? value)
    {
        _builder.UseSetting(key, value);
        return this;
    }
}

```

##### 2.3.2  support startup 接口实现

```c#
internal class HostingStartupWebHostBuilder
{
    public IWebHostBuilder Configure(
        Action<WebHostBuilderContext, 
        IApplicationBuilder> configure)
    {
        return _builder.Configure(configure);
    }
    
    public IWebHostBuilder UseStartup(
        [DynamicallyAccessedMembers(
            StartupLinkerOptions.Accessibility)] Type startupType)
    {
        return _builder.UseStartup(startupType);
    }
    
    public IWebHostBuilder UseStartup
        <[DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicMethods)]TStartup>(
        Func<WebHostBuilderContext, TStartup> startupFactory)
    {
        return _builder.UseStartup(startupFactory);
    }
}

```

##### 2.3.3 support default service provider 接口实现

```c#
internal class HostingStartupWebHostBuilder
{
    public IWebHostBuilder UseDefaultServiceProvider(
        Action<WebHostBuilderContext, 
        ServiceProviderOptions> configure)
    {
        return _builder.UseDefaultServiceProvider(configure);
    }
}

```

#### 2.4 generic web hosted service

##### 2.4.1 service

```c#
internal class GenericWebHostService : IHostedService    
{
    public GenericWebHostServiceOptions Options { get; }
    public IServer Server { get; }
    public ILogger Logger { get; }
    // Only for high level lifetime events
    public ILogger LifetimeLogger { get; }
    
    public DiagnosticListener DiagnosticListener { get; }
    public IHttpContextFactory HttpContextFactory { get; }
    public IApplicationBuilderFactory ApplicationBuilderFactory { get; }
    public IEnumerable<IStartupFilter> StartupFilters { get; }
    public IConfiguration Configuration { get; }
    public IWebHostEnvironment HostingEnvironment { get; }
    
    public GenericWebHostService(
        IOptions<GenericWebHostServiceOptions> options,
        IServer server,                                     
        ILoggerFactory loggerFactory,                                     
        DiagnosticListener diagnosticListener,  
        IHttpContextFactory httpContextFactory,    
        IApplicationBuilderFactory applicationBuilderFactory,    
        IEnumerable<IStartupFilter> startupFilters,      
        IConfiguration configuration,                                     
        IWebHostEnvironment hostingEnvironment)
    {
        Options = options.Value;
        Server = server;
        Logger = loggerFactory.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics");
        LifetimeLogger = loggerFactory.CreateLogger("Microsoft.Hosting.Lifetime");
        
        DiagnosticListener = diagnosticListener;
        HttpContextFactory = httpContextFactory;
        ApplicationBuilderFactory = applicationBuilderFactory;
        StartupFilters = startupFilters;
        Configuration = configuration;
        HostingEnvironment = hostingEnvironment;
    }
    
        
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        HostingEventSource.Log.HostStart();
        
        var serverAddressesFeature = Server.Features.Get<IServerAddressesFeature>();
        var addresses = serverAddressesFeature?.Addresses;
        if (addresses != null && 
            !addresses.IsReadOnly && 
            addresses.Count == 0)
        {
            var urls = Configuration[WebHostDefaults.ServerUrlsKey];
            if (!string.IsNullOrEmpty(urls))
            {
                serverAddressesFeature!.PreferHostingUrls = 
                    WebHostUtilities.ParseBool(
                    	Configuration, 
                    	WebHostDefaults.PreferHostingUrlsKey);
                
                foreach (var value in 
                         urls.Split(
                             ';', 
                             StringSplitOptions.RemoveEmptyEntries))
                {
                    addresses.Add(value);
                }
            }
        }
        
        RequestDelegate? application = null;
        
        try
        {
            var configure = Options.ConfigureApplication;
            
            if (configure == null)
            {
                throw new InvalidOperationException($"No application configured. Please specify an application via IWebHostBuilder.UseStartup, IWebHostBuilder.Configure, or specifying the startup assembly via {nameof(WebHostDefaults.StartupAssemblyKey)} in the web host configuration.");
            }
            
            var builder = ApplicationBuilderFactory
                .CreateBuilder(Server.Features);
            
            foreach (var filter in StartupFilters.Reverse())
            {
                configure = filter.Configure(configure);
            }
            
            configure(builder);
            
            // Build the request pipeline
            application = builder.Build();
        }
        catch (Exception ex)
        {
            Logger.ApplicationError(ex);
            
            if (!Options.WebHostOptions.CaptureStartupErrors)
            {
                throw;
            }
            
            var showDetailedErrors = 
                HostingEnvironment.IsDevelopment() || 
                Options.WebHostOptions.DetailedErrors;
            
            application = ErrorPageBuilder.BuildErrorPageApplication(
                HostingEnvironment.ContentRootFileProvider, 
                Logger, 
                showDetailedErrors, 
                ex);
        }
        
        var httpApplication = new HostingApplication(
            application, 
            Logger, 
            DiagnosticListener, 
            HttpContextFactory);
        
        await Server.StartAsync(httpApplication, cancellationToken);
        
        if (addresses != null)
        {
            foreach (var address in addresses)
            {
                LifetimeLogger.LogInformation("Now listening on: {address}", address);
            }
        }
        
        if (Logger.IsEnabled(LogLevel.Debug))
        {
            foreach (var assembly in Options.WebHostOptions.GetFinalHostingStartupAssemblies())
            {
                Logger.LogDebug("Loaded hosting startup assembly {assemblyName}", assembly);
            }
        }
        
        if (Options.HostingStartupExceptions != null)
        {
            foreach (var exception in Options.HostingStartupExceptions.InnerExceptions)
            {
                Logger.HostingStartupAssemblyError(exception);
            }
        }
    }
    
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Server.StopAsync(cancellationToken);
        }
        finally
        {
            HostingEventSource.Log.HostStop();
        }
    }
}

```

##### 2.4.2 service options

```c#
internal class GenericWebHostServiceOptions
{
    public Action<IApplicationBuilder>? ConfigureApplication { get; set; }
    
    // Always set when options resolved by DI
    public WebHostOptions WebHostOptions { get; set; } = default!;     
    public AggregateException? HostingStartupExceptions { get; set; }
}

```

















### 3. practice



