## about web hosting

相关程序集：

* microsoft.aspnetcore.hosting

----

### 1.about

#### 1.1 summary



#### 1.2 how designed

##### 1.2.1 web host

* 运行 web service 的 host
* 由`IHostApplicationLifetime`控制生命周期

###### 1.2.1.1 构造时注入

* 注入必要的服务，
* 这些服务从`hosted service provider`中解析，
* 服务在`web host builder`中注入服务
  * service collection
  * hosting service provider
  * web host options
  * configuration

###### 1.2.1.2 构造时解析

* web host 会解析必要的服务，
* 这些服务在`web host builder`构建前注入
  * IStartup
  * application lifetime
  * hosted service executor
  * application service provider
  * IServer
  * ILogger

###### 1.2.1.3 初始化 web host

* web host 构造后需要初始化，
* 初始化在`web host builder`构建时调用
  * 从 hosting service provider 中解析 IStartup
  * 从 IStartup 中解析 app service provider 

###### 1.2.1.3 启动 web host

* 解析、启动日志
* build application（构建 request delegate ）
  * 解析 server
  * 解析 application build factory 并创建 application builder
  * 解析 startup filter 并用其配置 application builder
* 解析 hosted service excutor 并执行
* 配置server 并启动
  * 解析 diagnostic listener，
  * 解析 http context factory
  * 创建 hosting application
  * 由上述组件配置并启动 server
* 启动 application lifetime notify

###### 1.2.1.4 停止 web host

* 停止日志
* 停止 server
* 停止 hosted service executor
* 停止 application lifetime notify

##### 1.2.2 web host builder

* 创建 web host 的构造器

###### 1.2.2.1 构造函数

* 创建 hosting environment
* 加载 (hosting) configuration
* 加载环境变量并使用，即将环境变量注入 configuration
* 创建并注入 web host builder context，包含 web host options & web host envrionment

###### 1.2.2.2 构建 web host

* 构建 hosting service provider
  * 创建 service collection并注入服务
    * 执行 hosting startup 配置
      * 加载 hosting startup type 并用其配置 builder
    * app configuration 
      * 创建 configuration，
      * 合并 hosting configuration，
      * 用 builder 中的 action 配置，
      * 注入到 service collection
    * 注册服务
      * diagnostic listener
      * application builder factory
      * http context factory
      * middleware factory
      * options
      * logging
      * service provider factory
    * 注册 IStartup
      * 解析 startup type
      * 注册 startup type
    * 配置 service collection
      * 用 builder 中的 action 配置 service collection（注入服务）
  * 获取 service provider
    * default service provider （ms 实现）
    * service provider factory 创建（第三方实现）
* 注入 application service
  * 注入并替换 diagnostic listener & source
* 通过扩展方法注入 server
* 创建 web host
* 初始化 web host

###### 1.2.2.3 配置 web host builder

* 配置（添加）configuration
* 配置（注册）服务
* 添加具体项到 configuration、envrionment

##### 1.2.3 generic web host builder

* 适配 generic host 的 web host builder
* generic host 出现后 web host builder 的重构实现
* 用于进行配置（注册服务）
* 不能实际生成 web host（在 generic host 中，web host 由 generic web host service 代替）

###### 1.2.3.1 构造函数

* 解析或创建`web host builder context`
  * 从`host builder context`中解析`web host builder context`
  * 用`host builder context`中的内容创建`web host builder context`
* 执行 hosting startup 配置
* 注册服务
  * diagnostic listener、source
  * application builder factory
  * http context factory
  * middleware factory
  * options
  * logging
  * service provider factory
* 注册 IStartup
* 通过扩展方法
  * 注册 server
  * 创建 service provider

##### 1.2.4 hosting startup web host builder

* hosting startup 配置项
* 不用于直接生成 web host

##### 1.2.5 generic web host service

* 代替 web host 执行 start、stop

###### 1.2.5.1 构造函数

* 直接从 host service provider 中解析并注入相关服务
* 与 web host 相似

###### 1.2.5.2 启动

* 与 web host 相似

###### 1.2.5.3 停止

* 与 web host 相似

### 2a. details - web host

#### 2.1 web host

* 构造函数中注入必要的服务，这些服务由 web host builder 构建并注入
  * service collection
  * hosting service provider
  * configuration

* 初始化，创建、启动必要的服务
  * 从 hosting service provider 中解析 application lifetime，并注入 service collection
  * 从 hosting service provider 中解析 IStartup
  * 从 IStartup 中解析 app service provider
* 启动 web host
  * 解析 logger 并启动
  * 构建 applicaiton builder，进而构建 request delegate
  * 解析 applicaiton lifetime 并启动
  * 解析 hosted service executor 并启动
  * 解析 server、配置并启动

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

* 从`web host builder`中解析并注入服务

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
            
            // 并记录异常
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
            // 从 IStartup 构建 service provider
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

#### 2.2 web host 组件

##### 2.2.2 web host default

* web host builder 在 configuration 中的 key_name

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

##### 2.2.3 application lifetime

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

##### 2.2.4  hosted service executor

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





##### 2.2.6 server addresses feature

###### 2.2.6.1 接口

```c#
public interface IServerAddressesFeature
{    
    ICollection<string> Addresses { get; }        
    bool PreferHostingUrls { get; set; }
}

```

###### 2.2.6.2 实现

```c#
 public class ServerAddressesFeature : IServerAddressesFeature
 {
     /// <inheritdoc />
     public ICollection<string> Addresses { get; } = new List<string>();     
     /// <inheritdoc />
     public bool PreferHostingUrls { get; set; }
 }

```

##### 





### 2.b details - web host builder

#### 2.1 web host builder



##### 2.1.2 实现

```c#
public class WebHostBuilder : IWebHostBuilder
{
    private readonly HostingEnvironment _hostingEnvironment;
    private readonly IConfiguration _config;
    private readonly WebHostBuilderContext _context;    
    private WebHostOptions? _options;
    
    // action -> configure service collection
    private Action<WebHostBuilderContext, IServiceCollection>? 
        _configureServices;
    // action -> configure configuration builder
    private Action<WebHostBuilderContext, IConfigurationBuilder>? 
        _configureAppConfigurationBuilder;

    private bool _webHostBuilt;
   
    public WebHostBuilder()
    {
        // 创建 hosting environment
        _hostingEnvironment = new HostingEnvironment();
        
        // 创建 configuration，
        // 读取环境变量 ASPENTCORE_
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
    
    // ...
}

```

##### 2.1.3 configure services

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

##### 2.1.4 configure app configuration

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

#### 2.2 构建 web host

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

##### 2.2.1  输出 obsolete 信息

```c#
public class WebHostBuilder : IWebHostBuilder
{
    public IWebHost Build()
    {
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
    }
}

```

##### 2.2.2 build common service

```c#
public class WebHostBuilder : IWebHostBuilder
{
    [MemberNotNull(nameof(_options))]
    private IServiceCollection BuildCommonServices(
        out AggregateException? hostingStartupErrors)
    {
        hostingStartupErrors = null;
        
        // 创建 web host options,
        // 注入 envrionment variable & assembly name
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
           web host environment => builder context */                
        
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

###### 2.2.2.1  执行 hosting startup 中的配置

* load startup type from assembly

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

###### 2.2.2.2 加载 web host envrionment 配置并注入

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

###### 2.2.2.3 执行 application configuration 中的配置并注入

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

###### 2.2.2.4 注入 web common service

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

###### 2.2.2.5 注入 IStartup

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

##### 2.2.3 clone service collection

```c#
internal static class ServiceCollectionExtensions
{
    public static IServiceCollection Clone(
        this IServiceCollection serviceCollection)
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

##### 2.2.4 get service provider

* 返回 default service provider
* 或者第三方 service provider，
  * （由注入的 service provider factory 创建）

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

##### 2.2.5 注入 applicaiton service

* 注入 diagnostic listener
* 注入 diagnostic source

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

#### 2.3 配置 builder 的扩展方法

##### 2.3.1 web host builder extension

```c#

    
    
   
    
    
    public static IWebHostBuilder UseStaticWebAssets(this IWebHostBuilder builder)
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







#### 2.4 web host builder 组件



##### 2.6.2 static web asset

###### 2.6.2.1 static web asset loader

```c#
public class StaticWebAssetsLoader
{
    internal const string StaticWebAssetsManifestName = 
        "Microsoft.AspNetCore.StaticWebAssets.xml";
        
    public static void UseStaticWebAssets(
        IWebHostEnvironment environment, 
        IConfiguration configuration)
    {
        // get maifest
        using var manifest = ResolveManifest(environment, configuration);
        if (manifest != null)
        {
            // manifast 为 null，使用 web access core
            UseStaticWebAssetsCore(environment, manifest);
        }
    }
    
    /* 解析 manifest（stream） */        
    
    internal static Stream? ResolveManifest(
        IWebHostEnvironment environment, 
        IConfiguration configuration)
    {
        try
        {
            var manifestPath = configuration.GetValue<string>(
                WebHostDefaults.StaticWebAssetsKey);
            
            var filePath = manifestPath ?? ResolveRelativeToAssembly(environment);            
            if (filePath != null && File.Exists(filePath))
            {
                return File.OpenRead(filePath);
            }
            else
            {
                // A missing manifest might simply mean that the feature is not enabled, 
                // so we simply return early. 
                // Misconfigurations will be uncommon given that the entire process 
                // is automated at build time.
                return null;
            }
        }
        catch
        {
            return null;
        }
    }
    
    private static string? ResolveRelativeToAssembly(
        IWebHostEnvironment environment)
    {
        var assembly = Assembly.Load(environment.ApplicationName);
        if (string.IsNullOrEmpty(assembly.Location))
        {
            return null;
        }
        
        return Path.Combine(
            Path.GetDirectoryName(assembly.Location)!, 
            $"{environment.ApplicationName}.StaticWebAssets.xml");
    }
    
    /* use static web asset core */
    
    internal static void UseStaticWebAssetsCore(
        IWebHostEnvironment environment, 
        Stream manifest)
    {
        var webRootFileProvider = environment.WebRootFileProvider;
        
        var additionalFiles = StaticWebAssetsReader
            .Parse(manifest)
            .Select(cr => 
            	new StaticWebAssetsFileProvider(cr.BasePath, cr.Path))
            // Upcast so we can insert on the resulting list.
            .OfType<IFileProvider>() 
            .ToList();
        
        if (additionalFiles.Count == 0)
        {
            return;
        }
        else
        {
            additionalFiles.Insert(0, webRootFileProvider);
            environment.WebRootFileProvider = new CompositeFileProvider(additionalFiles);
        }
    }
}

```

###### 2.6.2.2 static web asset file provider

```c#
internal class StaticWebAssetsFileProvider : IFileProvider
{
    private static readonly StringComparison FilePathComparison = 
        OperatingSystem.IsWindows() 
        	? StringComparison.OrdinalIgnoreCase 
        	: StringComparison.Ordinal;
    
    public PathString BasePath { get; }
    public PhysicalFileProvider InnerProvider { get; }
    
    public StaticWebAssetsFileProvider(
        string pathPrefix, 
        string contentRoot)
    {
        BasePath = NormalizePath(pathPrefix);
        InnerProvider = new PhysicalFileProvider(contentRoot);
    }                    
    
    /// <inheritdoc />
    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        var modifiedSub = NormalizePath(subpath);
        
        if (BasePath == "/")
        {
            return InnerProvider.GetDirectoryContents(modifiedSub);
        }
        
        if (StartsWithBasePath(
            	modifiedSub, 
            	out var physicalPath))
        {
            return InnerProvider
                .GetDirectoryContents(physicalPath.Value);
        }
        else if (string.Equals(subpath, string.Empty) || 
                 string.Equals(modifiedSub, "/"))
        {
            return new StaticWebAssetsDirectoryRoot(BasePath);
        }            
        else if (BasePath.StartsWithSegments(
            		modifiedSub, 
            		FilePathComparison, 
            		out var remaining))
        {
            return new StaticWebAssetsDirectoryRoot(remaining);
        }
        
        return NotFoundDirectoryContents.Singleton;
    }
    
    /// <inheritdoc />
    public IFileInfo GetFileInfo(string subpath)
    {
        var modifiedSub = NormalizePath(subpath);
        
        if (BasePath == "/")
        {
            return InnerProvider.GetFileInfo(subpath);
        }
        
        if (!StartsWithBasePath(
            	modifiedSub, 
            	out var physicalPath))
        {
            return new NotFoundFileInfo(subpath);
        }
        else
        {
            return InnerProvider.GetFileInfo(physicalPath.Value);
        }
    }
    
    /// <inheritdoc />
    public IChangeToken Watch(string filter)
    {
        return InnerProvider.Watch(filter);
    }
    
    private static string NormalizePath(string path)
    {
        path = path.Replace('\\', '/');
        return path.StartsWith('/') ? path : "/" + path;
    }
    
    private bool StartsWithBasePath(
        string subpath, 
        out PathString rest)
    {
        return new PathString(subpath)
            .StartsWithSegments(
            	BasePath, 
            	FilePathComparison, 
            	out rest);
    }
    
    private class StaticWebAssetsDirectoryRoot : IDirectoryContents
    {
        private readonly string _nextSegment;
                                                
        public StaticWebAssetsDirectoryRoot(PathString remainingPath)
        {
            // We MUST use the Value property here because it is unescaped.
            _nextSegment = remainingPath.Value
                ?.Split(
                	"/", 
                	StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() 
                ?? string.Empty;
        }
        
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GenerateEnum();
        }
        
        public IEnumerator<IFileInfo> GetEnumerator()
        {
            return GenerateEnum();
        }
                       
        private IEnumerator<IFileInfo> GenerateEnum()
        {
            return new[] { new StaticWebAssetsFileInfo(_nextSegment) }
            .Cast<IFileInfo>().GetEnumerator();
        }
        
        private class StaticWebAssetsFileInfo : IFileInfo
        {            
            public string Name { get; }
            public bool Exists => true;
            public bool IsDirectory => true;
            
            public long Length => throw new NotImplementedException();            
            public string PhysicalPath => throw new NotImplementedException();            
            public DateTimeOffset LastModified => throw new NotImplementedException();
            
            public StaticWebAssetsFileInfo(string name)
            {
                Name = name;
            }
                                                                                   
            public Stream CreateReadStream()
            {
                throw new NotImplementedException();
            }
        }
    }
}

```

###### 2.6.2.3 static web asset reader

```c#
internal static class StaticWebAssetsReader
{
    private const string ManifestRootElementName = "StaticWebAssets";
    private const string VersionAttributeName = "Version";
    private const string ContentRootElementName = "ContentRoot";
        
    internal static IEnumerable<ContentRootMapping> Parse(Stream manifest)
    {
        var document = XDocument.Load(manifest);
        if (!string.Equals(
            	document.Root!.Name.LocalName, 
            	ManifestRootElementName, 
            	StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Invalid manifest format. 
                "Manifest root must be '{ManifestRootElementName}'");
        }
        
        var version = document.Root.Attribute(VersionAttributeName);
        if (version == null)
        {
            throw new InvalidOperationException(
                $"Invalid manifest format. 
                "Manifest root element must contain a version 
                '{VersionAttributeName}' attribute");
        }
        
        if (version.Value != "1.0")
        {
            throw new InvalidOperationException(
                $"Unknown manifest version. Manifest version must be '1.0'");
        }
        
        foreach (var element in document.Root.Elements())
        {
            if (!string.Equals(
                element.Name.LocalName, 
                ContentRootElementName, 
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Invalid manifest format. 
                    "Invalid element '{element.Name.LocalName}'. 
                    "All {StaticWebAssetsLoader.StaticWebAssetsManifestName} 
                    "child elements must be '{ContentRootElementName}' elements.");
            }
            if (!element.IsEmpty)
            {
                throw new InvalidOperationException(
                    $"Invalid manifest format. 
                    "{ContentRootElementName} can't have content.");
            }
            
            var basePath = ParseRequiredAttribute(element, "BasePath");
            var path = ParseRequiredAttribute(element, "Path");
            yield return new ContentRootMapping(basePath, path);
        }
    }
    
    private static string ParseRequiredAttribute(
        XElement element, 
        string attributeName)
    {
        var attribute = element.Attribute(attributeName);
        if (attribute == null)
        {
            throw new InvalidOperationException(
                $"Invalid manifest format. 
                "Missing {attributeName} attribute in 
                '{ContentRootElementName}' element.");
        }
        return attribute.Value;
    }
    
    internal readonly struct ContentRootMapping
    {
        public ContentRootMapping(string basePath, string path)
        {
            BasePath = basePath;
            Path = path;
        }
        
        public string BasePath { get; }
        public string Path { get; }
    }
}

```











