## about hosting

相关程序集：

* microsoft.extensions.hosting.abstract
* micorsoft.extension.hosting

----

### 1. about

#### 1.1 summary

* ms 提供的托管应用的宿主服务，分为 generic host 和 web host
  * recommend to use “generic host”
  * web app 中会使用“web host”

#### 1.2 how designed

##### 1.2.1 host

* 通用主机
  * 定义了 startAsync、stopAsync 方法
  * 扩展了同步方法，runAsyn 和 run 方法

##### 1.2.2 host builder

* host 建造者

###### 1.2.2.1 构建（build）host

* a - host configuration
* b - host environment
* c - 合并 a 和 b 到 host build context
* d - app configuration
* e - service provider

###### 1.2.2.2 配置 host builder

* builder 中的方法
* 扩展方法

##### 1.2.3 Host 静态

* 创建 IHost 的静态方法类
* 调用 IHostBuilder 并使用了默认配置

### 2. details


#### 2.1 host

##### 2.1.1 IHost 接口

```c#
public interface IHost : IDisposable
{    
    IServiceProvider Services { get; }        
    
    Task StartAsync(CancellationToken cancellationToken = default);  
    Task StopAsync(CancellationToken cancellationToken = default);
}

```

##### 2.1.2 Host 实现

```c#
internal class Host : IHost, IAsyncDisposable
{
    public IServiceProvider Services { get; }
    
    private readonly ILogger<Host> _logger;     
    private readonly IHostLifetime _hostLifetime;      
    private readonly ApplicationLifetime _applicationLifetime;  
    private readonly HostOptions _options;      
    private IEnumerable<IHostedService> _hostedServices;
        
    /* 初始化（构造），从 di 注入服务 */
    public Host(
        IServiceProvider services, 
        IHostLifetime hostLifetime, 
        IOptions<HostOptions> options,
        IHostApplicationLifetime applicationLifetime,
        ILogger<Host> logger)
    {
        // 注入 service provider，
        // 如果为null，抛出异常
        Services = services ?? 
            throw new ArgumentNullException(nameof(services));
        
        // 注入 host application lifetime，
        // 如果为null，抛出异常
        _applicationLifetime = (applicationLifetime ?? 
        	throw new ArgumentNullException(nameof(applicationLifetime))) 
            	as ApplicationLifetime;      
        
        if (_applicationLifetime is null)
        {
            throw new ArgumentException(
                "Replacing IHostApplicationLifetime is not supported.", 
                nameof(applicationLifetime));
        }
        
        // 注入 logger，
        // 如果为null，抛出异常
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // 注入 host lifetime，
        // 如果为null，抛出异常
        _hostLifetime = hostLifetime ?? throw new ArgumentNullException(nameof(hostLifetime));
        // 注入 host options，
        // 如果为null，抛出异常
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }                               
}

```

###### 2.1.2.1 host start

```c#
internal class Host : IHost, IAsyncDisposable
{
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.Starting();
        
        /* 创建 cancellation token source */
        using var combinedCancellationTokenSource = 
            CancellationTokenSource.CreateLinkedTokenSource(
            	cancellationToken, 
            	_applicationLifetime.ApplicationStopping);
        
        CancellationToken combinedCancellationToken = 
            combinedCancellationTokenSource.Token;
        
        /* 启动 host lifetime */
        await _hostLifetime.WaitForStartAsync(
            combinedCancellationToken).ConfigureAwait(false);
        
        combinedCancellationToken.ThrowIfCancellationRequested();
        
        /* 启动 hosted services */
        _hostedServices = 
            Services.GetService<IEnumerable<IHostedService>>();
        
        foreach (IHostedService hostedService in _hostedServices)
        {
            // Fire IHostedService.Start
            await hostedService
                .StartAsync(combinedCancellationToken)
                	.ConfigureAwait(false);
            
            if (hostedService is BackgroundService backgroundService)
            {
                // 激活 background service exception handler
                _ = HandleBackgroundException(backgroundService);
            }
        }
        
        /* 启动 host application */
        // Fire IHostApplicationLifetime.Started
        _applicationLifetime.NotifyStarted();
        
        _logger.Started();
    }
    
    // background service exception handler
    private async Task HandleBackgroundException(BackgroundService backgroundService)
    {
        try
        {
            await backgroundService.ExecuteTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.BackgroundServiceFaulted(ex);
        }
    }
}

```

###### 2.1.2.2 host stop

```c#
internal class Host : IHost, IAsyncDisposable
{
    public async Task StopAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.Stopping();
        
        using (var cts = new CancellationTokenSource(_options.ShutdownTimeout))       
        using (var linkedCts = CancellationTokenSource
               .CreateLinkedTokenSource(cts.Token, cancellationToken))
        {
            // 创建 cancellatino token 
            CancellationToken token = linkedCts.Token;
            
            /* 停止 host application */
            // Trigger IHostApplicationLifetime.ApplicationStopping
            _applicationLifetime.StopApplication();
            
            /* 停止 hosted services，记录 exceptions */
            IList<Exception> exceptions = new List<Exception>();
            if (_hostedServices != null) // Started?
            {
                foreach (IHostedService hostedService in _hostedServices.Reverse())
                {
                    try
                    {
                        await hostedService.StopAsync(token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }
            }
            
            /* 停止 notify */
            // Fire IHostApplicationLifetime.Stopped
            _applicationLifetime.NotifyStopped();
            
            /* 停止 host lifetime，记录 exceptions，抛出 */
            try
            {
                await _hostLifetime.StopAsync(token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
            
            if (exceptions.Count > 0)
            {
                var ex = new AggregateException(
                    "One or more hosted services failed to stop.", exceptions);
                _logger.StoppedWithException(ex);
                throw ex;
            }
        }
        
        _logger.Stopped();
    }
}

```

###### 2.1.2.3 host logging 扩展

```c#
internal static class HostingLoggerExtensions
{
    public static void ApplicationError(
        this ILogger logger, 
        EventId eventId, 
        string message, 
        Exception exception)
    {
        var reflectionTypeLoadException = 
            exception as ReflectionTypeLoadException;
        if (reflectionTypeLoadException != null)
        {
            foreach (Exception ex in reflectionTypeLoadException.LoaderExceptions)
            {
                message = message + Environment.NewLine + ex.Message;
            }
        }
        
        logger.LogCritical(
            eventId: eventId,
            message: message,
            exception: exception);
    }
    
    public static void Starting(this ILogger logger)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                eventId: LoggerEventIds.Starting,
                message: "Hosting starting");
        }
    }
    
    public static void Started(this ILogger logger)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                eventId: LoggerEventIds.Started,
                message: "Hosting started");
        }
    }
    
    public static void Stopping(this ILogger logger)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                eventId: LoggerEventIds.Stopping,
                message: "Hosting stopping");
        }
    }
    
    public static void Stopped(this ILogger logger)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                eventId: LoggerEventIds.Stopped,
                message: "Hosting stopped");
        }
    }
    
    public static void StoppedWithException(this ILogger logger, Exception ex)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                eventId: LoggerEventIds.StoppedWithException,
                exception: ex,
                message: "Hosting shutdown exception");
        }
    }
    
    public static void BackgroundServiceFaulted(this ILogger logger, Exception ex)
    {
        if (logger.IsEnabled(LogLevel.Error))
        {
            logger.LogError(
                eventId: LoggerEventIds.BackgroundServiceFaulted,
                exception: ex,
                message: "BackgroundService failed");
        }
    }
}

```



###### 2.1.2.3 host dispose

```c#
internal class Host : IHost, IAsyncDisposable
{
    public void Dispose() => 
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    
    public async ValueTask DisposeAsync()
    {
        switch (Services)
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

##### 2.1.3 扩展的 host 方法

###### 2.1.3.1 start

* 扩展了 start 同步方法

```c#
public static class HostingAbstractionsHostExtensions
{
    public static void Start(this IHost host)
    {
        host.StartAsync().GetAwaiter().GetResult();
    }
}

```

###### 2.1.3.2 wait for stop

```c#
public static class HostingAbstractionsHostExtensions
{
    public static async Task WaitForShutdownAsync(
        this IHost host, 
        CancellationToken token = default)
    {
        IHostApplicationLifetime applicationLifetime = 
            host.Services.GetService<IHostApplicationLifetime>();
        
        token.Register(state =>
        	{
                ((IHostApplicationLifetime)state).StopApplication();
            },
            applicationLifetime);
        
        var waitForStop = new TaskCompletionSource<object>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        
        applicationLifetime
            .ApplicationStopping
            .Register(obj =>
            	{
                    var tcs = (TaskCompletionSource<object>)obj;
                    tcs.TrySetResult(null);
                }, 
                waitForStop);
        
        await waitForStop.Task.ConfigureAwait(false);
        
        // Host will use its default ShutdownTimeout if none is specified.
        // The cancellation token may have been triggered to unblock waitForStop. Don't pass it here because that would trigger an abortive shutdown.
        await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
    }
    
    public static void WaitForShutdown(this IHost host)
    {
        host.WaitForShutdownAsync().GetAwaiter().GetResult();
    }
}

```

###### 2.1.3.3 run

* 同步和异步 run 方法

```c#
public static class HostingAbstractionsHostExtensions
{        
    public static async Task RunAsync(
        this IHost host, 
        CancellationToken token = default)
    {
        try
        {
            // start host
            await host.StartAsync(token).ConfigureAwait(false);            
            // keep await
            await host.WaitForShutdownAsync(token).ConfigureAwait(false);
        }
        finally
        {
            // dispose host
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
    
    public static void Run(this IHost host)
    {
        host.RunAsync().GetAwaiter().GetResult();
    }
}

```

###### 2.1.3.4 stop

* 定时关闭

```c#
public static class HostingAbstractionsHostExtensions
{            
    public static async Task StopAsync(this IHost host, TimeSpan timeout)
    {
        using CancellationTokenSource cts = new CancellationTokenSource(timeout);
        await host.StopAsync(cts.Token).ConfigureAwait(false);
    }
}

```

##### 2.1.3 host options

```c#
public class HostOptions
{    
    public TimeSpan ShutdownTimeout { get; set; } = 
        TimeSpan.FromSeconds(5);
    
    internal void Initialize(IConfiguration configuration)
    {
        var timeoutSeconds = configuration["shutdownTimeoutSeconds"];
        
        if (!string.IsNullOrEmpty(timeoutSeconds) && 
            int.TryParse(
                timeoutSeconds, 
                NumberStyles.None, 
                CultureInfo.InvariantCulture, 
                out var seconds))
        {
            ShutdownTimeout = TimeSpan.FromSeconds(seconds);
        }
    }
}

```

##### 2.1.4 host lifetime

* 控制 host 生命周期

```c#
public interface IHostLifetime
{    
    Task WaitForStartAsync(CancellationToken cancellationToken);     
    Task StopAsync(CancellationToken cancellationToken);
}

```

##### 2.1.5 host application lifetime

* 控制 host 中的 application 的生命周期

```c#
public interface IHostApplicationLifetime
{    
    CancellationToken ApplicationStarted { get; }        
    CancellationToken ApplicationStopping { get; }        
    CancellationToken ApplicationStopped { get; }
        
    void StopApplication();
}

```

##### 2.1.6 host environment

* 封装的 host env 参数，强类型

```c#
public interface IHostEnvironment
{    
    string EnvironmentName { get; set; }        
    string ApplicationName { get; set; }        
    string ContentRootPath { get; set; }        
    IFileProvider ContentRootFileProvider { get; set; }
}

```

#### 2.2 host builder

##### 2.2.1 接口

```c#
public interface IHostBuilder
{    
    /* 配置 host builder 的 action 集合*/         
    IHostBuilder ConfigureHostConfiguration(
        Action<IConfigurationBuilder> configureDelegate);        
    IHostBuilder ConfigureAppConfiguration(
        Action<HostBuilderContext, IConfigurationBuilder> configureDelegate);        
    IHostBuilder ConfigureServices(
        Action<HostBuilderContext, IServiceCollection> configureDelegate);        
    IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
        IServiceProviderFactory<TContainerBuilder> factory);        
    IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
        Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> factory);        
    IHostBuilder ConfigureContainer<TContainerBuilder>(
        Action<HostBuilderContext, TContainerBuilder> configureDelegate);
    
    /* 传递参数*/
    IDictionary<object, object> Properties { get; }  
        
    IHost Build();
}

```

##### 2.2.2 实现

```c#
public class HostBuilder : IHostBuilder
{
    /* 用于配置 host builder 的 action 集合 */
    private List<Action<IConfigurationBuilder>> 
        _configureHostConfigActions = 
        	new List<Action<IConfigurationBuilder>>();
    private List<Action<HostBuilderContext, IConfigurationBuilder>> 
        _configureAppConfigActions = 
        	new List<Action<HostBuilderContext, IConfigurationBuilder>>();
    private List<Action<HostBuilderContext, IServiceCollection>> 
        _configureServicesActions = 
        	new List<Action<HostBuilderContext, IServiceCollection>>();
    private List<IConfigureContainerAdapter> 
        _configureContainerActions = 
        	new List<IConfigureContainerAdapter>();    
    
    // service provider factory -> default
    private IServiceFactoryAdapter _serviceProviderFactory = 
        new ServiceFactoryAdapter<IServiceCollection>(
        	new DefaultServiceProviderFactory());    
    // built 标记
    private bool _hostBuilt;
    // 传递数据
    public IDictionary<object, object> Properties { get; } = 
        new Dictionary<object, object>();
    
    // a - host configuration
    private IConfiguration _hostConfiguration;
    // b - host application configuration
    private IConfiguration _appConfiguration;
    // c - host build context
    private HostBuilderContext _hostBuilderContext;
    // d - host environment
    private HostingEnvironment _hostingEnvironment;
    // e - service provider
    private IServiceProvider _appServices;
        
    // 构建 host                    
    public IHost Build()
    {
        // 如果已经构建过（built = true），抛出异常
        if (_hostBuilt)
        {
            throw new InvalidOperationException(SR.BuildCalled);
        }
        
        // 设置 built = true，
        // 只能 build 一次
        _hostBuilt = true;
        
        // a
        BuildHostConfiguration();
        // b
        CreateHostingEnvironment();
        // c
        CreateHostBuilderContext();
        // d
        BuildAppConfiguration();
        // e
        CreateServiceProvider();
        
        // 从 di 解析 host，
        // 由 di 控制 host 生命周期（使用后 dispose）
        return _appServices.GetRequiredService<IHost>();
    }
    
    
    
    
    
    private string ResolveContentRootPath(string contentRootPath, string basePath)
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

##### 2.2.3 配置 host builder

* host builder 提供了配置 builder 的方法

###### 2.2.3.1  配置 configuration

* host configuration 和 host application configuration 最终合并到 host builder context

```c#
public class HostBuilder : IHostBuilder
{
    // 配置 host configuration
    public IHostBuilder ConfigureHostConfiguration(
        Action<IConfigurationBuilder> configureDelegate)
    {
        _configureHostConfigActions.Add(
            configureDelegate ?? 
            	throw new ArgumentNullException(nameof(configureDelegate)));
        
        return this;
    }
    
    // 配置 host application configuration
    public IHostBuilder ConfigureAppConfiguration(
        Action<HostBuilderContext, IConfigurationBuilder> configureDelegate)
    {
        _configureAppConfigActions.Add(
            configureDelegate ?? 
            	throw new ArgumentNullException(nameof(configureDelegate)));
        
        return this;
    }
}

```

###### 2.2.3.2 注册服务

```c#
public class HostBuilder : IHostBuilder
{
    public IHostBuilder ConfigureServices(
        Action<HostBuilderContext, 
        IServiceCollection> configureDelegate)
    {
        _configureServicesActions.Add(
            configureDelegate ?? 
            	throw new ArgumentNullException(nameof(configureDelegate)));
        
        return this;
    }
}
```

###### 2.2.3.3 配置 container builder

```c#
public class HostBuilder : IHostBuilder
{
    public IHostBuilder ConfigureContainer<TContainerBuilder>(
        Action<HostBuilderContext, TContainerBuilder> configureDelegate)
    {
        _configureContainerActions.Add(
            new ConfigureContainerAdapter<TContainerBuilder>(
                configureDelegate ?? 
                	throw new ArgumentNullException(nameof(configureDelegate))));
        
        return this;
    }
}
```

###### 2.2.3.4 配置 service provider factory

```c#
public class HostBuilder : IHostBuilder
{                
    public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
        IServiceProviderFactory<TContainerBuilder> factory)
    {
        _serviceProviderFactory = 
            new ServiceFactoryAdapter<TContainerBuilder>(
            	factory ?? throw new ArgumentNullException(nameof(factory)));
        
        return this;
    }
    
    public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
        Func<HostBuilderContext, 
        IServiceProviderFactory<TContainerBuilder>> factory)
    {
        _serviceProviderFactory = 
            new ServiceFactoryAdapter<TContainerBuilder>(
            	() => _hostBuilderContext, 
            	factory ?? 
            		throw new ArgumentNullException(nameof(factory)));
        
        return this;
    }        
}

```

##### 2.2.4 扩展的配置方法

###### 2.2.4.1 configure environment

```c#
public static class HostingHostBuilderExtensions
{
    public static IHostBuilder UseEnvironment(
        this IHostBuilder hostBuilder, 
        string environment)
    {
        return hostBuilder.ConfigureHostConfiguration(configBuilder =>
        {
            configBuilder.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>(
                    HostDefaults.EnvironmentKey,
                    environment ?? 
                    	throw new ArgumentNullException(nameof(environment)))
            });
        });
    }
    
    public static IHostBuilder UseContentRoot(
        this IHostBuilder hostBuilder, 
        string contentRoot)
    {
        return hostBuilder.ConfigureHostConfiguration(configBuilder =>
        {
            configBuilder.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>(
                    HostDefaults.ContentRootKey,
                    contentRoot ?? 
                    	throw new ArgumentNullException(nameof(contentRoot)))
            });
        });
    }        
}

```

###### 2.2.4.2 configure app configuration

```c#
public static class HostingHostBuilderExtensions
{
    public static IHostBuilder ConfigureAppConfiguration(
        this IHostBuilder hostBuilder, 
        Action<IConfigurationBuilder> configureDelegate)
    {
        return hostBuilder.ConfigureAppConfiguration(
            (context, builder) => configureDelegate(builder));
    }
}

```

###### 2.2.4.3 configure service provider

```c#
public static class HostingHostBuilderExtensions
{
    public static IHostBuilder UseDefaultServiceProvider(
        this IHostBuilder hostBuilder, 
        Action<ServiceProviderOptions> configure)
            => hostBuilder.UseDefaultServiceProvider(
        		(context, options) => configure(options));
        
    public static IHostBuilder UseDefaultServiceProvider(
        this IHostBuilder hostBuilder, 
        Action<HostBuilderContext, 
        ServiceProviderOptions> configure)
    {
        return hostBuilder.UseServiceProviderFactory(context =>
        {
            var options = new ServiceProviderOptions();
            configure(context, options);
            return new DefaultServiceProviderFactory(options);
        });
    }
    
    public static IHostBuilder ConfigureServices(
        this IHostBuilder hostBuilder, 
        Action<IServiceCollection> configureDelegate)
    {
        return hostBuilder.ConfigureServices(
            (context, collection) => configureDelegate(collection));
    }
        
    public static IHostBuilder ConfigureContainer<TContainerBuilder>(
        this IHostBuilder hostBuilder, 
        Action<TContainerBuilder> configureDelegate)
    {
        return hostBuilder.ConfigureContainer<TContainerBuilder>(
            (context, builder) => configureDelegate(builder));
    }
}

```

###### 2.2.4.4 configure logging

```c#
public static class HostingHostBuilderExtensions
{                                        
    public static IHostBuilder ConfigureLogging(
        this IHostBuilder hostBuilder, 
        Action<HostBuilderContext, ILoggingBuilder> configureLogging)
    {
        return hostBuilder.ConfigureServices((context, collection) => 
        	collection.AddLogging(builder => configureLogging(context, builder)));
    }
        
    public static IHostBuilder ConfigureLogging(
        this IHostBuilder hostBuilder, 
        Action<ILoggingBuilder> configureLogging)
    {
        return hostBuilder.ConfigureServices(
            (context, collection) => 
            	collection.AddLogging(builder => configureLogging(builder)));
    }                       
}
    
```

###### 2.2.4.5 add hosted service

```c#
public static class ServiceCollectionHostedServiceExtensions
{
    
    public static IServiceCollection 
        AddHostedService
        	<[DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes
                	.PublicConstructors)] THostedService>(
        	this IServiceCollection services)            
        where THostedService : class, IHostedService
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, THostedService>());
        
        return services;
    }
    
    
    public static IServiceCollection 
        AddHostedService<THostedService>(
        	this IServiceCollection services, 
        	Func<IServiceProvider, THostedService> implementationFactory)            
        where THostedService : class, IHostedService
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService>(implementationFactory));
        
        return services;
    }
}

```

###### 2.2.4.6 配置 lifetime

```c#

```

##### 2.2.5 构建 host builder

###### 2.2.5.1 a - build host configuration

```c#
public class HostBuilder : IHostBuilder
{
    private void BuildHostConfiguration()
    {
        // Make sure there's some default storage since there are no default providers
        IConfigurationBuilder configBuilder = 
            new ConfigurationBuilder().AddInMemoryCollection(); 
        
        foreach (Action<IConfigurationBuilder> buildAction in 
                 _configureHostConfigActions)
        {
            buildAction(configBuilder);
        }
        
        _hostConfiguration = configBuilder.Build();
    }
}

```

###### 2.2.5.2 b - create hosting environment

```c#
public class HostBuilder : IHostBuilder
{
    private void CreateHostingEnvironment()
    {
        // 从 host configuration 中读取相关信息，
        // 并用其创建 host environment
        _hostingEnvironment = new HostingEnvironment()
        {
            ApplicationName = 
                _hostConfiguration[HostDefaults.ApplicationKey],
            EnvironmentName = 
                _hostConfiguration[HostDefaults.EnvironmentKey] ?? 
                	Environments.Production,
            ContentRootPath = 
                ResolveContentRootPath(
                	_hostConfiguration[HostDefaults.ContentRootKey], 
                	AppContext.BaseDirectory),
        };
        
        if (string.IsNullOrEmpty(_hostingEnvironment.ApplicationName))
        {
            // Note GetEntryAssembly returns null for the net4x console test runner.
            _hostingEnvironment.ApplicationName = 
                Assembly.GetEntryAssembly()?.GetName().Name;
        }
        
        _hostingEnvironment.ContentRootFileProvider = 
            new PhysicalFileProvider(_hostingEnvironment.ContentRootPath);
    }
}

```

###### 2.2.5.3 c - create host build context

```c#
public class HostBuilder : IHostBuilder
{
    private void CreateHostBuilderContext()
    {
        _hostBuilderContext = new HostBuilderContext(Properties)
        {
            HostingEnvironment = _hostingEnvironment,
            Configuration = _hostConfiguration
        };
    }
}

```

###### 2.2.5.4 d - build app configuration

```c#
public class HostBuilder : IHostBuilder
{
    private void BuildAppConfiguration()
    {
        // 加载 content root 下的配置，
        // 合并 host configuration
        IConfigurationBuilder configBuilder = new ConfigurationBuilder()
            .SetBasePath(_hostingEnvironment.ContentRootPath)
            .AddConfiguration(_hostConfiguration, shouldDisposeConfiguration: true);
        
        foreach (Action<HostBuilderContext, IConfigurationBuilder> 
                 buildAction in _configureAppConfigActions)
        {
            buildAction(_hostBuilderContext, configBuilder);
        }
        
        _appConfiguration = configBuilder.Build();
        _hostBuilderContext.Configuration = _appConfiguration;
    }
}

```

###### 2.2.5.5 e - create service provider

```c#
public class HostBuilder : IHostBuilder
{
    private void CreateServiceProvider()
    {
        var services = new ServiceCollection();
        
        /* 注册 host environment */
		//#pragma warning disable CS0618 // Type or member is obsolete
        //    services.AddSingleton<IHostingEnvironment>(_hostingEnvironment);
        //#pragma warning restore CS0618 // Type or member is obsolete            
        services.AddSingleton<IHostEnvironment>(_hostingEnvironment);
        
        /* 注册 host builder context */
        services.AddSingleton(_hostBuilderContext);
        
        /* 注册 host application configuration */
        // register configuration as factory to make it dispose with the service provider
        services.AddSingleton(_ => _appConfiguration);
        
        /* 注册 host application lifetime */
        //#pragma warning disable CS0618 // Type or member is obsolete
        //    services.AddSingleton<IApplicationLifetime>(s => 
      	//        (IApplicationLifetime)s.GetService<IHostApplicationLifetime>());
        //#pragma warning restore CS0618 // Type or member is obsolete            
        services.AddSingleton<IHostApplicationLifetime, ApplicationLifetime>();
        
        /* 注册 host lifetime */
        services.AddSingleton<IHostLifetime, ConsoleLifetime>();
        
        /* 注册 host */
        services.AddSingleton<IHost>(_ =>
            {
                return new Internal.Host(
                    _appServices,
                    _appServices.GetRequiredService<IHostApplicationLifetime>(),
                    _appServices.GetRequiredService<ILogger<Internal.Host>>(),
                    _appServices.GetRequiredService<IHostLifetime>(),
                    _appServices.GetRequiredService<IOptions<HostOptions>>());
            });
        
        // 注册 host options
        services.AddOptions().Configure<HostOptions>(options => 
        	{
                options.Initialize(_hostConfiguration); 
            });
        
        // 注册 logging
        services.AddLogging();
        
        // 注册 host builder 中配置的 services
        foreach (Action<HostBuilderContext, IServiceCollection> 
                 configureServicesAction in _configureServicesActions)
        {
            configureServicesAction(_hostBuilderContext, services);
        }
        
        // 创建 container builder
        object containerBuilder = _serviceProviderFactory.CreateBuilder(services);
        // 用 host builder 中的 container confiure action，
        // 配置 container builder
        foreach (IConfigureContainerAdapter 
                 containerAction in _configureContainerActions)
        {
            containerAction.ConfigureContainer(
                _hostBuilderContext, containerBuilder);
        }
        
        // 用 container builder 创建 service provider
        _appServices = _serviceProviderFactory
            .CreateServiceProvider(containerBuilder);
        
        if (_appServices == null)
        {
            throw new InvalidOperationException(SR.NullIServiceProvider);
        }
        
        // resolve configuration explicitly once to mark it as resolved within the
        // service provider, ensuring it will be properly disposed with the provider
        _ = _appServices.GetService<IConfiguration>();
    }
}

```

##### 2.2.6 host environment

###### 2.2.6.1 接口

```c#
public interface IHostEnvironment
{    
    string EnvironmentName { get; set; }        
    string ApplicationName { get; set; }        
    string ContentRootPath { get; set; }        
    IFileProvider ContentRootFileProvider { get; set; }
}

```

###### 2.2.6.2 实现

```c#
#pragma warning disable CS0618 // Type or member is obsolete    
public class HostingEnvironment : IHostingEnvironment, IHostEnvironment
#pragma warning restore CS0618 // Type or member is obsolete
{
    public string EnvironmentName { get; set; }    
    public string ApplicationName { get; set; }    
    public string ContentRootPath { get; set; }    
    public IFileProvider ContentRootFileProvider { get; set; }
}

```

###### 2.2.6.3 default environment

* enum

  ```c#
  public static class Environments
  {
      public static readonly string Development = "Development";
      public static readonly string Staging = "Staging";
      public static readonly string Production = "Production";
  }
  
  ```

* host key

  ```c#
  public static class HostDefaults
  {
      public static readonly string EnvironmentKey = "environment";
      public static readonly string ApplicationKey = "applicationName";                    
      public static readonly string ContentRootKey = "contentRoot";
  }
  
  ```

###### 2.2.6.3 扩展方法

```c#
public static class HostEnvironmentEnvExtensions
{
    // for general
    public static bool IsEnvironment(
        this IHostEnvironment hostEnvironment,
        string environmentName)
    {
        if (hostEnvironment == null)
        {
            throw new ArgumentNullException(nameof(hostEnvironment));
        }
        
        return string.Equals(
            hostEnvironment.EnvironmentName,
            environmentName,
            StringComparison.OrdinalIgnoreCase);
    }
    // is development
    public static bool IsDevelopment(this IHostEnvironment hostEnvironment)
    {
        if (hostEnvironment == null)
        {
            throw new ArgumentNullException(nameof(hostEnvironment));
        }
        
        return hostEnvironment.IsEnvironment(Environments.Development);
    }
    // is staging    
    public static bool IsStaging(this IHostEnvironment hostEnvironment)
    {
        if (hostEnvironment == null)
        {
            throw new ArgumentNullException(nameof(hostEnvironment));
        }
        
        return hostEnvironment.IsEnvironment(Environments.Staging);
    }
    // is production    
    public static bool IsProduction(this IHostEnvironment hostEnvironment)
    {
        if (hostEnvironment == null)
        {
            throw new ArgumentNullException(nameof(hostEnvironment));
        }
        
        return hostEnvironment.IsEnvironment(Environments.Production);
    }            
}

```

#### 2.3 host lifetime

```c#
public interface IHostLifetime
{    
    Task WaitForStartAsync(CancellationToken cancellationToken);      
    Task StopAsync(CancellationToken cancellationToken);
}

```

##### 2.2.1 console lifetime

```c#
public class ConsoleLifetime : IHostLifetime, IDisposable
{
    /* 启动、停止 cancellatin token， event */
    
    private readonly ManualResetEvent _shutdownBlock = new ManualResetEvent(false);
    private CancellationTokenRegistration _applicationStartedRegistration;
    private CancellationTokenRegistration _applicationStoppingRegistration;
        
    /* 初始化（构造），注入服务 */

    private ConsoleLifetimeOptions Options { get; }    
    private IHostEnvironment Environment { get; }    
    private IHostApplicationLifetime ApplicationLifetime { get; }    
    private HostOptions HostOptions { get; }    
    private ILogger Logger { get; }
    
	public ConsoleLifetime(
        IOptions<ConsoleLifetimeOptions> options, 
        IHostEnvironment environment, 
        HostApplicationLifetime applicationLifetime, 
        IOptions<HostOptions> hostOptions)            
        	: this(
                options, 
                environment, 
                applicationLifetime, 
                hostOptions, NullLoggerFactory.Instance) 
    {
    }
                
    public ConsoleLifetime(
        IOptions<ConsoleLifetimeOptions> options, 
        IHostEnvironment environment, 
        IHostApplicationLifetime applicationLifetime, 
        IOptions<HostOptions> hostOptions, 
        ILoggerFactory loggerFactory)
    {
        Options = options?.Value ?? 
            throw new ArgumentNullException(nameof(options));
        Environment = environment ?? 
            throw new ArgumentNullException(nameof(environment));
        ApplicationLifetime = applicationLifetime ?? 
            throw new ArgumentNullException(nameof(applicationLifetime));
        HostOptions = hostOptions?.Value ?? 
            throw new ArgumentNullException(nameof(hostOptions));
        Logger = loggerFactory.CreateLogger("Microsoft.Hosting.Lifetime");
    }                                
}

```

###### 2.2.1.1 wait for start

```c#
public class ConsoleLifetime 
{
    public Task WaitForStartAsync(CancellationToken cancellationToken)
    {
        if (!Options.SuppressStatusMessages)
        {
            // 注册 on started cancellatin token
            _applicationStartedRegistration = 
                ApplicationLifetime
                	.ApplicationStarted
                	.Register(state =>                
                    	{
                            ((ConsoleLifetime)state).OnApplicationStarted();
                        },
                        this);
            // 注册 on stopping cancellatino token
            _applicationStoppingRegistration = 
                ApplicationLifetime
                	.ApplicationStopping
                	.Register(state =>
                    	{
                            ((ConsoleLifetime)state).OnApplicationStopping();
                        },
                        this);
        }
        
        // 订阅 process exit 事件
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        // 订阅 cancel key press 事件
        Console.CancelKeyPress += OnCancelKeyPress;
        
        // Console applications start immediately.
        return Task.CompletedTask;
    }
    
    // on application started，
    // 记录日志
    private void OnApplicationStarted()
    {
        Logger.LogInformation(
            "Application started. Press Ctrl+C to shut down.");
        Logger.LogInformation(
            "Hosting environment: {envName}", 
            Environment.EnvironmentName);
        Logger.LogInformation(
            "Content root path: {contentRoot}", 
            Environment.ContentRootPath);
    }
    // on application stopping，
    // 记录日志
    private void OnApplicationStopping()
    {
        Logger.LogInformation("Application is shutting down...");
    }
    
    /* process exit 事件 */
    private void OnProcessExit(object sender, EventArgs e)
    {
        ApplicationLifetime.StopApplication();
        if (!_shutdownBlock.WaitOne(HostOptions.ShutdownTimeout))
        {
            Logger.LogInformation(
                "Waiting for the host to be disposed. Ensure all 'IHost' instances are wrapped in 'using' blocks.");
        }
        _shutdownBlock.WaitOne();
        // On Linux if the shutdown is triggered by SIGTERM then that's signaled with the 143 xit code.
        // Suppress that since we shut down gracefully. https://github.com/dotnet/aspnetcore/issues/6526
        System.Environment.ExitCode = 0;
    }
    
    /* cancel key press 事件 */
    private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        ApplicationLifetime.StopApplication();
    }
}

```

###### 2.2.1.2 stop and dispose

```c#
public class ConsoleLifetime 
{
    public Task StopAsync(CancellationToken cancellationToken)
    {
        // There's nothing to do here
        return Task.CompletedTask;
    }
    
    public void Dispose()
    {
        _shutdownBlock.Set();
        
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        Console.CancelKeyPress -= OnCancelKeyPress;
        
        _applicationStartedRegistration.Dispose();
        _applicationStoppingRegistration.Dispose();
    }
}

```

###### 2.2.1.3 use console lifetime

```c#
public static class HostingHostBuilderExtensions
{
    public static IHostBuilder UseConsoleLifetime(
        this IHostBuilder hostBuilder)
    {
        return hostBuilder.ConfigureServices(
            (context, collection) => 
            	collection.AddSingleton<IHostLifetime, ConsoleLifetime>());
    }
        
    public static IHostBuilder UseConsoleLifetime(
        this IHostBuilder hostBuilder, 
        Action<ConsoleLifetimeOptions> configureOptions)
    {
        return hostBuilder.ConfigureServices(
            (context, collection) =>
            {
                collection.AddSingleton<IHostLifetime, ConsoleLifetime>();
                collection.Configure(configureOptions);
            });
    }
        
    public static Task RunConsoleAsync(
        this IHostBuilder hostBuilder, 
        CancellationToken cancellationToken = default)
    {
        return hostBuilder
            .UseConsoleLifetime()
            .Build()
            .RunAsync(cancellationToken);
    }
        
    public static Task RunConsoleAsync(
        this IHostBuilder hostBuilder, 
        Action<ConsoleLifetimeOptions> configureOptions, 
        CancellationToken cancellationToken = default)
    {
        return hostBuilder
            .UseConsoleLifetime(configureOptions)
            .Build()
            .RunAsync(cancellationToken);
        }
    }
}

```

##### 2.2.2 systemd lifetime

```c#
public class SystemdLifetime : IHostLifetime, IDisposable
{
    /* 启动、停止 cancellatin token， event */
    
    private readonly ManualResetEvent _shutdownBlock = new ManualResetEvent(false);
    private CancellationTokenRegistration _applicationStartedRegistration;
    private CancellationTokenRegistration _applicationStoppingRegistration;
    
    /* 初始化（构造），注入服务 */
    
    private IHostEnvironment Environment { get; }
    private IHostApplicationLifetime ApplicationLifetime { get; }
    private ISystemdNotifier SystemdNotifier { get; }
    private ILogger Logger { get; }
        
    public SystemdLifetime(
        IHostEnvironment environment, 
        IHostApplicationLifetime applicationLifetime, 
        ISystemdNotifier systemdNotifier, 
        ILoggerFactory loggerFactory)
    {
        Environment = environment ?? 
            throw new ArgumentNullException(nameof(environment));
        ApplicationLifetime = applicationLifetime ?? 
            throw new ArgumentNullException(nameof(applicationLifetime));
        SystemdNotifier = systemdNotifier ?? 
            throw new ArgumentNullException(nameof(systemdNotifier));
        Logger = loggerFactory.CreateLogger("Microsoft.Hosting.Lifetime");
    }                               
}

```

###### 2.2.2.1 wait for start

```c#
public class SystemdLifetime
{
    public Task WaitForStartAsync(CancellationToken cancellationToken)
    {
        // 注册 started cancellation token
        _applicationStartedRegistration = 
            ApplicationLifetime
            	.ApplicationStarted
            	.Register(state =>            
                	{
                        ((SystemdLifetime)state).OnApplicationStarted();
                    },
                    this);
        
        // 注册 stopping cancellatin token
        _applicationStoppingRegistration = 
            ApplicationLifetime
            	.ApplicationStopping
            	.Register(state =>
                	{
                        ((SystemdLifetime)state).OnApplicationStopping();
                    },
                    this);
        
        // 订阅 on process exit 事件
        // systemd sends SIGTERM to stop the service.
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        
        return Task.CompletedTask;
    }
    
    // on application started，
    // 记录日志、send notify
    private void OnApplicationStarted()
    {
        Logger.LogInformation(
            "Application started. Hosting environment: {EnvironmentName}; Content root path: {ContentRoot}",
            Environment.EnvironmentName, Environment.ContentRootPath);
        
        SystemdNotifier.Notify(ServiceState.Ready);
    }
    
    // on application stopping，
    // 记录日志、send notify
    private void OnApplicationStopping()
    {
        Logger.LogInformation("Application is shutting down...");
        
        SystemdNotifier.Notify(ServiceState.Stopping);
    }
    
    // process exit 事件
    private void OnProcessExit(object sender, EventArgs e)
    {
        ApplicationLifetime.StopApplication();
        
        _shutdownBlock.WaitOne();
        
        // On Linux if the shutdown is triggered by SIGTERM then that's signaled with the 143 exit code.
        // Suppress that since we shut down gracefully. https://github.com/dotnet/aspnetcore/issues/6526
        System.Environment.ExitCode = 0;
    }
}

```

###### 2.2.2.2 stop and dispose

```c#
public class SystemdLifetime
{
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    
    public void Dispose()
    {
        _shutdownBlock.Set();
        
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        
        _applicationStartedRegistration.Dispose();
        _applicationStoppingRegistration.Dispose();
    }
}

```

###### 2.2.2.3 use systemd lifetime

```c#
public static class SystemdHostBuilderExtensions
{    
    public static IHostBuilder UseSystemd(this IHostBuilder hostBuilder)
    {
        if (SystemdHelpers.IsSystemdService())
        {
            hostBuilder.ConfigureServices(
                (hostContext, services) =>
                {
                    services.Configure<ConsoleLoggerOptions>(options =>
                    	{
                            options.FormatterName = ConsoleFormatterNames.Systemd;
                        });
                    
                    services.AddSingleton<ISystemdNotifier, SystemdNotifier>();                 
                    services.AddSingleton<IHostLifetime, SystemdLifetime>();
                });
        }
        
        return hostBuilder;
    }
}

```

##### 2.2.3 windows service lifetime

```c#
public class WindowsServiceLifetime : IHostLifetime, ServiceBase
{
    private readonly TaskCompletionSource<object> _delayStart = 
        new TaskCompletionSource<object>(
        	TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ManualResetEventSlim _delayStop = 
        new ManualResetEventSlim();
    private readonly HostOptions _hostOptions;
    
    /* 初始化（构造），注册服务 */
    
    private IHostEnvironment Environment { get; }
    private IHostApplicationLifetime ApplicationLifetime { get; }    
    private ILogger Logger { get; }
    
    public WindowsServiceLifetime(
        IHostEnvironment environment, 
        IHostApplicationLifetime applicationLifetime, 
        ILoggerFactory loggerFactory, 
        IOptions<HostOptions> optionsAccessor)            
        	: this(
                environment, 
                applicationLifetime, 
                loggerFactory, 
                optionsAccessor, 
                Options.Options.Create(new WindowsServiceLifetimeOptions()))
    {
    }
    
    public WindowsServiceLifetime(
        IHostEnvironment environment, 
        IHostApplicationLifetime applicationLifetime, 
        ILoggerFactory loggerFactory, 
        IOptions<HostOptions> optionsAccessor, 
        IOptions<WindowsServiceLifetimeOptions> windowsServiceOptionsAccessor)
    {
        Environment = environment ?? 
            throw new ArgumentNullException(nameof(environment));
        ApplicationLifetime = applicationLifetime ?? 
            throw new ArgumentNullException(nameof(applicationLifetime));
        Logger = loggerFactory.CreateLogger("Microsoft.Hosting.Lifetime");
        if (optionsAccessor == null)
        {
            throw new ArgumentNullException(nameof(optionsAccessor));
        }
        if (windowsServiceOptionsAccessor == null)
        {
            throw new ArgumentNullException(nameof(windowsServiceOptionsAccessor));
        }
        _hostOptions = optionsAccessor.Value;
        ServiceName = windowsServiceOptionsAccessor.Value.ServiceName;
        CanShutdown = true;
    }                                
}

```

###### 2.2.3.1 wait for start

```c#
public class WindowsServiceLifetime 
{
    public Task WaitForStartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.Register(() => _delayStart.TrySetCanceled());
        
        // 注册 started cancellation token，
        // 记录日志
        ApplicationLifetime
            .ApplicationStarted
            .Register(() =>
            	{
                    Logger.LogInformation(
                        "Application started.\ 
                        "Hosting environment: {envName}; Content root path: {contentRoot}",
                        Environment.EnvironmentName, Environment.ContentRootPath);
                });
        // 注册 stopping cancellation token，
        // 记录日志
        ApplicationLifetime
            .ApplicationStopping
            .Register(() =>
            	{
                    Logger.LogInformation("Application is shutting down...");
                });
        // 注册 stopped cancellation token，
        // 记录日志
        ApplicationLifetime
            .ApplicationStopped
            .Register(() =>
            	{
                    _delayStop.Set();
                });
        
        Thread thread = new Thread(Run);
        thread.IsBackground = true;
        // Otherwise this would block and prevent IHost.StartAsync from finishing.
        thread.Start(); 
        
        return _delayStart.Task;
    }
    
    private void Run()
    {
        try
        {
            // This blocks until the service is stopped.
            Run(this); 
            _delayStart.TrySetException(
                new InvalidOperationException("Stopped without starting"));
        }
        catch (Exception ex)
        {
            _delayStart.TrySetException(ex);
        }
    }
}
```

###### 2.2.3.2 stop

```c#
public class WindowsServiceLifetime 
{
    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Avoid deadlock where host waits for StopAsync before firing ApplicationStopped,
        // and Stop waits for ApplicationStopped.
        Task.Run(Stop);
        return Task.CompletedTask;
    }
}
```

###### 2.2.3.3 override base service

```c#
public class WindowsServiceLifetime 
{
    // Called by base.Run when the service is ready to start.
    protected override void OnStart(string[] args)
    {
        _delayStart.TrySetResult(null);
        base.OnStart(args);
    }
    
    // Called by base.Stop. This may be called multiple times by service Stop, ApplicationStopping, and StopAsync.
    // That's OK because StopApplication uses a CancellationTokenSource and prevents any recursion.
    protected override void OnStop()
    {
        ApplicationLifetime.StopApplication();
        // Wait for the host to shutdown before marking service as stopped.
        _delayStop.Wait(_hostOptions.ShutdownTimeout);
        base.OnStop();
    }
    
    protected override void OnShutdown()
    {
        ApplicationLifetime.StopApplication();
        // Wait for the host to shutdown before marking service as stopped.
        _delayStop.Wait(_hostOptions.ShutdownTimeout);
        base.OnShutdown();
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _delayStop.Set();
        }
        
        base.Dispose(disposing);
    }
}

```

###### 2.2.3.4 use windows service lifetime

```c#
public static class WindowsServiceLifetimeHostBuilderExtensions
{    
    public static IHostBuilder UseWindowsService(this IHostBuilder hostBuilder)
    {
        return UseWindowsService(hostBuilder, _ => { });
    }
    
    
    public static IHostBuilder UseWindowsService(
        this IHostBuilder hostBuilder, 
        Action<WindowsServiceLifetimeOptions> configure)
    {
        if (WindowsServiceHelpers.IsWindowsService())
        {
            // Host.CreateDefaultBuilder uses CurrentDirectory for VS scenarios, but CurrentDirectory for services is c:\Windows\System32.
            hostBuilder.UseContentRoot(AppContext.BaseDirectory);
            hostBuilder.ConfigureLogging(
                (hostingContext, logging) =>
                	{
                        logging.AddEventLog();
                    })
                	.ConfigureServices((hostContext, services) =>
                    	{
                            services.AddSingleton<IHostLifetime, WindowsServiceLifetime>();
                            services.Configure<EventLogSettings>(settings =>
                            	{
                                    if (string.IsNullOrEmpty(settings.SourceName))
                                    {
                                        settings.SourceName = 
                                            hostContext.HostingEnvironment.ApplicationName;
                                    }
                                });
                            services.Configure(configure);
                        });
        }
        
        return hostBuilder;
    }
}

```

#### 2.4 host application lifetime

##### 2.4.1 接口

```c#
public interface IHostApplicationLifetime
{    
    CancellationToken ApplicationStarted { get; }        
    CancellationToken ApplicationStopping { get; }        
    CancellationToken ApplicationStopped { get; }
        
    void StopApplication();
}

```

##### 2.4.2 实现

```c#
#pragma warning disable CS0618 // Type or member is obsolete
public class ApplicationLifetime : IApplicationLifetime, IHostApplicationLifetime
#pragma warning restore CS0618 // Type or member is obsolete
{
    private readonly CancellationTokenSource _startedSource = new CancellationTokenSource();
    private readonly CancellationTokenSource _stoppingSource = new CancellationTokenSource();
    private readonly CancellationTokenSource _stoppedSource = new CancellationTokenSource();
    private readonly ILogger<ApplicationLifetime> _logger;
    
    public ApplicationLifetime(ILogger<ApplicationLifetime> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Triggered when the application host has fully started and is about to wait
    /// for a graceful shutdown.
    /// </summary>
    public CancellationToken ApplicationStarted => _startedSource.Token;
    
    /// <summary>
    /// Triggered when the application host is performing a graceful shutdown.
    /// Request may still be in flight. Shutdown will block until this event completes.
    /// </summary>
    public CancellationToken ApplicationStopping => _stoppingSource.Token;
    
    /// <summary>
    /// Triggered when the application host is performing a graceful shutdown.
    /// All requests should be complete at this point. Shutdown will block
    /// until this event completes.
    /// </summary>
    public CancellationToken ApplicationStopped => _stoppedSource.Token;
    
    /// <summary>
    /// Signals the ApplicationStopping event and blocks until it completes.
    /// </summary>
    public void StopApplication()
    {
        // Lock on CTS to synchronize multiple calls to StopApplication. This guarantees that the first call
        // to StopApplication and its callbacks run to completion before subsequent calls to StopApplication,
        // which will no-op since the first call already requested cancellation, get a chance to execute.
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
    
    /// <summary>
    /// Signals the ApplicationStarted event and blocks until it completes.
    /// </summary>
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
    
    /// <summary>
    /// Signals the ApplicationStopped event and blocks until it completes.
    /// </summary>
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

#### 2.5 host 静态方法

```c#
public static class Host
{    
    public static IHostBuilder CreateDefaultBuilder() =>
        CreateDefaultBuilder(args: null);
        
    public static IHostBuilder CreateDefaultBuilder(string[] args)
    {
        var builder = new HostBuilder();
        
        // 配置默认的 environment
                
        builder
            .ConfigureAppConfiguration((hostingContext, config) =>
            	{
                    // ...
                })            	
            .ConfigureLogging((hostingContext, logging) =>
            	{
                    // ...
                })            
            .UseDefaultServiceProvider((context, options) => 
            	{
                    // ...
                });
        
        return builder;
    }
}

```

##### 2.5.1 配置默认 environment

```c#
public static class Host
{ 
    public static IHostBuilder CreateDefaultBuilder(string[] args)
    {
        // var builder = new HostBuilder();
        
        // 设置 content root 为当前文件夹（bin文件夹）
        builder.UseContentRoot(Directory.GetCurrentDirectory());

        // 配置默认的 host configuration
        builder.ConfigureHostConfiguration(config =>
        	{
                // 添加 DOTNET 开头的系统环境变量
                config.AddEnvironmentVariables(prefix: "DOTNET_");
                
                // 添加命令行参数 args
                if (args != null)
                {
                    config.AddCommandLine(args);
                }
            });
    }
}

```

##### 2.5.2 配置 app configuration

```c#
public static class Host
{ 
    public static IHostBuilder CreateDefaultBuilder(string[] args)
    {
        // var builder = new HostBuilder();
        
        builder
            .ConfigureAppConfiguration(
            	(hostingContext, config) =>
            	{
                    IHostEnvironment env = hostingContext.HostingEnvironment;
                    
                    // 从 DOTNET_ 和 args 中加载，
                    // reloadConfigurationChange，
                    // 默认 true，即监视变化
                    bool reloadOnChange = 
                        hostingContext
                        	.Configuration
                        	.GetValue(
                        		"hostBuilder:reloadConfigOnChange", 
                        		defaultValue: true);
                    
                    config
                        // 加载 appsettings 文件作为 configuration 源，
                        // 可选的
                        .AddJsonFile(
                        	"appsettings.json", 
                        	optional: true, 
                        	reloadOnChange: reloadOnChange)       
                        // 加载 appsettings.env 文件作为 configuration 源，
                        // 可选的
                        .AddJsonFile(
                        	$"appsettings.{env.EnvironmentName}.json", 
                        	optional: true, 
                        	reloadOnChange: reloadOnChange);
                    
                    // 如果是 development 环境，
                    // 且 applicationName 不为 null
                    if (env.IsDevelopment() && 
                        !string.IsNullOrEmpty(env.ApplicationName))
                    {
                        // 加载 application assembly 的 user secrets
                        var appAssembly = Assembly.Load(new AssemblyName(env.ApplicationName));
                        if (appAssembly != null)
                        {
                            config.AddUserSecrets(
                                appAssembly, 
                                optional: true, 
                                reloadOnChange: reloadOnChange);
                        }
                    }
                    
                    // 加载环境变量
                    config.AddEnvironmentVariables();
                    
                    // 再次加载 args，                    
                    if (args != null)
                    {
                        config.AddCommandLine(args);
                    }
                });            
    }
}
        
```

##### 2.5.3 logging

```c#
public static class Host
{ 
    public static IHostBuilder CreateDefaultBuilder(string[] args)
    {
        // var builder = new HostBuilder();
        
        builder            
            .ConfigureLogging(
            	(hostingContext, logging) =>
            	{
                    // windows 系统标记
                    bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                    
                    // IMPORTANT: This needs to be added *before* configuration is loaded, 
                    // this lets the defaults be overridden by the configuration.
                    if (isWindows)
                    {
                        // Default the EventLogLoggerProvider to warning or above
                        logging.AddFilter<EventLogLoggerProvider>(
                            level => level >= LogLevel.Warning);
                    }
                    
                    // 使用 configuration 中 “Logging” 节的配置
                    logging.AddConfiguration(
                        hostingContext.Configuration.GetSection("Logging"));
                    
                    // 输出到控制台
                    logging.AddConsole();
                    // 输出到debug
                    logging.AddDebug();
                    // 输出到 event source logger
                    logging.AddEventSourceLogger();
                    
                    // 如果是 windows 系统，输出到 event log
                    if (isWindows)
                    {
                        // Add the EventLogLoggerProvider on windows machines
                        logging.AddEventLog();
                    }
                    
                    // 追踪
                    logging.Configure(options =>
                    	{
                            options.ActivityTrackingOptions = 
                                ActivityTrackingOptions.SpanId | 
                                ActivityTrackingOptions.TraceId | 
                                ActivityTrackingOptions.ParentId;
                        });                    
                });                        
    }
}
        
```

##### 2.5.4 配置 service provider

```c#
public static class Host
{ 
    public static IHostBuilder CreateDefaultBuilder(string[] args)
    {
        // var builder = new HostBuilder();
        
        builder            
            .UseDefaultServiceProvider(
            	(context, options) =>
            		{
                        bool isDevelopment = context.HostingEnvironment.IsDevelopment();
                        options.ValidateScopes = isDevelopment;
                        options.ValidateOnBuild = isDevelopment;
                    });
    }
}
        
```

### 3. practice

