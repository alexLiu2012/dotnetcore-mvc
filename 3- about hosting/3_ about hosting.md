## about generic hosting

相关程序集：

* microsoft.extensions.hosting.abstract
* micorsoft.extension.hosting

----

### 1. about

#### 1.1 summary

程序运行后变成进程，并由操作系统控制其运行、销毁。

面向框架的程序设计，首先要构建一个可以运行的程序容器。它本身可以受操作系统控制，针对不同的操作系统设计不同的 lifetime ；同时容器可以托管管需要运行的子程序（服务），并控制这些子程序（服务）的 lifetime；该容器还应该是自身及托管的服务读取外界配置的代理。

generic host 就是这样的容器，它是 microsoft 提供的 web application 宿主，亦可用于其他应用，如 backgroun service，故而名为“generic host”

#### 1.2 how designed

##### 1.2.1 host

通用主机模型，.net core 框架运行的容器宿主，封装了如下组件：

* logging

* host lifetime，控制 host 的生命周期

* configuration，从外界读取的配置信息

* hosted service & host applicationlifetime，由 host 托管的应用及其生命周期

* service provider，

  di 容器，提供、管理 service；

  上述组件在 host builder 构建 host 时注入 di；

  host 本身也是由 di 解析、管理的

##### 1.2.2 host builder

host 构建器，由如下步骤构建

* a - host configuration，
  * 读取配置信息
* b - host environment，
  * 读取环境信息
* c - 合并 a 和 b 到 host build context，
  * 创建 build context，即对 host configuration 和 host environment 的封装
* d - app configuration
  * 创建 host application configuration
  * 合并了 host configuration
* e - service provider
  * 使用 service factory adapter 创建
  * 注入不同的 adapter，可以使用 第三方 service provider

##### 1.2.3 Host 静态类

Host 静态类通过 host builder 创建 IHost

#### 1.3 how to use

* 通过 host builder 配置 host 组件，进而构建 host
* 通过 Host 静态方法创建 default host builder，再由 default host builder 构建 host

### 2. details

#### 2.1 host lifetime

```c#
public interface IHostLifetime
{    
    Task WaitForStartAsync(CancellationToken cancellationToken);      
    Task StopAsync(CancellationToken cancellationToken);
}

```

##### 2.1.1 console lifetime

```c#
public class ConsoleLifetime : IHostLifetime, IDisposable
{   
    private readonly ManualResetEvent _shutdownBlock = new ManualResetEvent(false);
    
    // started token registration
    private CancellationTokenRegistration _applicationStartedRegistration;
    // stopping token registration
    private CancellationTokenRegistration _applicationStoppingRegistration;
           
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
        Options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        ApplicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
        HostOptions = hostOptions?.Value ?? throw new ArgumentNullException(nameof(hostOptions));
        // 静态方法创建的全局 logger
        Logger = loggerFactory.CreateLogger("Microsoft.Hosting.Lifetime");
    }                                
}

```

###### 2.1.1.1 console lifetime options

```c#
public class ConsoleLifetimeOptions
{    
    public bool SuppressStatusMessages { get; set; }
}

```

###### 2.1.1.2 接口方法 - wait for start

```c#
public class ConsoleLifetime 
{
    public Task WaitForStartAsync(CancellationToken cancellationToken)
    {
        // 如果 console lifetime options 没有标记 suppress status message
        if (!Options.SuppressStatusMessages)
        {
            // 注册 on started log
            _applicationStartedRegistration = ApplicationLifetime.ApplicationStarted.Register(
                state =>                
                	{
                        ((ConsoleLifetime)state).OnApplicationStarted();
                    },
                this);
            
            // 注册 on stopping log
            _applicationStoppingRegistration = ApplicationLifetime.ApplicationStopping.Register(
                state =>
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
    private void OnApplicationStarted()
    {
        Logger.LogInformation("Application started. Press Ctrl+C to shut down.");
        Logger.LogInformation("Hosting environment: {envName}", Environment.EnvironmentName);
        Logger.LogInformation("Content root path: {contentRoot}", Environment.ContentRootPath);
    }
    
    // on application stopping，    
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
        // On Linux if the shutdown is triggered by SIGTERM then that's signaled with the 143 exit code.
        // Suppress that since we shut down gracefully. 
        // https://github.com/dotnet/aspnetcore/issues/6526
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

###### 2.1.1.3 接口方法 -  stop and dispose

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

##### 2.1.2 systemd lifetime

```c#
public class SystemdLifetime : IHostLifetime, IDisposable
{    
    private readonly ManualResetEvent _shutdownBlock = new ManualResetEvent(false);
    
    // started token registration
    private CancellationTokenRegistration _applicationStartedRegistration;
    // stopping token registration
    private CancellationTokenRegistration _applicationStoppingRegistration;
        
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
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        ApplicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
        SystemdNotifier = systemdNotifier ?? throw new ArgumentNullException(nameof(systemdNotifier));
        // logger 静态类创建全部 logger
        Logger = loggerFactory.CreateLogger("Microsoft.Hosting.Lifetime");
    }                               
}

```

###### 2.1.2.1 接口方法 - wait for start

```c#
public class SystemdLifetime
{
    public Task WaitForStartAsync(CancellationToken cancellationToken)
    {
        // 注册 on started log
        _applicationStartedRegistration = ApplicationLifetime.ApplicationStarted.Register(
            state =>            
            	{
                    ((SystemdLifetime)state).OnApplicationStarted();
                },
            this);
        
        // 注册 on stopping log
        _applicationStoppingRegistration = ApplicationLifetime.ApplicationStopping.Register(
            state =>
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
    private void OnApplicationStarted()
    {
        Logger.LogInformation(
            "Application started. Hosting environment: {EnvironmentName}; Content root path: {ContentRoot}",
            Environment.EnvironmentName, 
            Environment.ContentRootPath);        
        SystemdNotifier.Notify(ServiceState.Ready);
    }
    
    // on application stopping，   
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
        
        // On Linux if the shutdown is triggered by SIGTERM
        // then that's signaled with the 143 exit code.
        // Suppress that since we shut down gracefully. 
        // https://github.com/dotnet/aspnetcore/issues/6526
        System.Environment.ExitCode = 0;
    }
}

```

###### 2.1.2.2 接口方法 - stop and dispose

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

###### 2.1.2.3 systemd notifier

```c#
public interface ISystemdNotifier
{   
    bool IsEnabled { get; }
    void Notify(ServiceState state);        
}

public class SystemdNotifier : ISystemdNotifier
{
    private const string NOTIFY_SOCKET = "NOTIFY_SOCKET";    
    private readonly string _socketPath;
    
    public bool IsEnabled => _socketPath != null;
    
    public SystemdNotifier() : this(GetNotifySocketPath())
    {
    }
    
    private static string GetNotifySocketPath()
    {
        string socketPath = Environment.GetEnvironmentVariable(NOTIFY_SOCKET);
        
        if (string.IsNullOrEmpty(socketPath))
        {
            return null;
        }
        
        // Support abstract socket paths.
        if (socketPath[0] == '@')
        {
            socketPath = "\0" + socketPath.Substring(1);
        }
        
        return socketPath;
    }
    
    
    internal SystemdNotifier(string socketPath)
    {
        _socketPath = socketPath;
    }
                   
    public void Notify(ServiceState state)
    {
        if (!IsEnabled)
        {
            return;
        }
        
        using (var socket = new Socket(
            AddressFamily.Unix, 
            SocketType.Dgram, 
            ProtocolType.Unspecified))
        {
            var endPoint = new UnixDomainSocketEndPoint(_socketPath);
            socket.Connect(endPoint);
            
            // It's safe to do a non-blocking call here: messages sent here are much
            // smaller than kernel buffers so we won't get blocked.
            socket.Send(state.GetData());
        }
    }        
}

```

##### 2.1.3 windows service lifetime

```c#
public class WindowsServiceLifetime : IHostLifetime, ServiceBase
{
    private readonly TaskCompletionSource<object> _delayStart = 
        new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ManualResetEventSlim _delayStop = new ManualResetEventSlim();
    private readonly HostOptions _hostOptions;
        
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
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        ApplicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
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

###### 2.1.3.1 host options

```c#
public class HostOptions
{    
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);
    
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

###### 2.1.3.2 windows service lifetime options 

```c#
public class WindowsServiceLifetimeOptions
{    
    public string ServiceName { get; set; } = string.Empty;
}

```

###### 2.1.3.3 扩展方法 - wait for start

```c#
public class WindowsServiceLifetime 
{
    public Task WaitForStartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.Register(() => _delayStart.TrySetCanceled());
        
        // 注册 started log       
        ApplicationLifetime.ApplicationStarted.Register(
            () =>
            	{
                    Logger.LogInformation(
                        "Application started. Hosting environment: {envName}; Content root path: {contentRoot}",
                        Environment.EnvironmentName, 
                        Environment.ContentRootPath);
                });
        
        // 注册 stopping log   
        ApplicationLifetime.ApplicationStopping.Register(
            () =>
            	{
                    Logger.LogInformation("Application is shutting down...");
                });
        
        // 注册 stopped log
        ApplicationLifetime.ApplicationStopped.Register(
            () =>
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
            _delayStart.TrySetException(new InvalidOperationException("Stopped without starting"));
        }
        catch (Exception ex)
        {
            _delayStart.TrySetException(ex);
        }
    }
}

```

###### 2.1.3.3 接口方法 - stop

```c#
public class WindowsServiceLifetime 
{
    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Avoid deadlock where host waits for StopAsync before firing ApplicationStopped, and Stop waits for ApplicationStopped.
        Task.Run(Stop);
        return Task.CompletedTask;
    }
}

```

###### 2.1.3.4 方法 - start / shutdown

```c#
public class WindowsServiceLifetime 
{
    // Called by base.Run when the service is ready to start.
    protected override void OnStart(string[] args)
    {
        _delayStart.TrySetResult(null);
        base.OnStart(args);
    }
    
    // Called by base.Stop. 
    // This may be called multiple times by service Stop, ApplicationStopping, and StopAsync.
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

#### 2.2 host application lifetime

##### 2.4.1 接口

```c#
public interface IHostApplicationLifetime
{    
    // start token
    CancellationToken ApplicationStarted { get; }        
    // stopping token
    CancellationToken ApplicationStopping { get; }        
    // stopped token
    CancellationToken ApplicationStopped { get; }
        
    void StopApplication();
}

```

##### 2.4.2 application lifetime

```c#
public class ApplicationLifetime : IHostApplicationLifetime
{
    // start token = new cts.token
    private readonly CancellationTokenSource _startedSource = new CancellationTokenSource();
    public CancellationToken ApplicationStarted => _startedSource.Token;
    
    // stopping token = new cts.token
    private readonly CancellationTokenSource _stoppingSource = new CancellationTokenSource();
    public CancellationToken ApplicationStopping => _stoppingSource.Token;
    
    // stopped token = new cts.token
    private readonly CancellationTokenSource _stoppedSource = new CancellationTokenSource();
    public CancellationToken ApplicationStopped => _stoppedSource.Token;
    
    private readonly ILogger<ApplicationLifetime> _logger;
    
    public ApplicationLifetime(ILogger<ApplicationLifetime> logger)
    {
        _logger = logger;
    }
    
    public void StopApplication()
    {
        // Lock on CTS to synchronize multiple calls to StopApplication. 
        // This guarantees that the first call to StopApplication and its callbacks run to completion before subsequent calls 
        // to StopApplication, which will no-op since the first call already requested cancellation, get a chance to execute.
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

#### 2.3 host

```c#
public interface IHost : IDisposable
{        
    IServiceProvider Services { get; }          
    Task StartAsync(CancellationToken cancellationToken = default);  
    Task StopAsync(CancellationToken cancellationToken = default);
}

```

##### 2.3.1 Host

```c#
internal class Host : IHost, IAsyncDisposable
{        
    private readonly ILogger<Host> _logger;     
    private readonly IHostLifetime _hostLifetime;      
    private readonly ApplicationLifetime _applicationLifetime;  
    private readonly HostOptions _options;      
    
    public IServiceProvider Services { get; }
    private IEnumerable<IHostedService> _hostedServices;
        
    /* 初始化（构造），由 builder 注入服务 */
    public Host(
        IServiceProvider services, 
        IHostLifetime hostLifetime, 
        IOptions<HostOptions> options,
        IHostApplicationLifetime applicationLifetime,
        ILogger<Host> logger)
    {
        // 注入 service provider（在 host builder 中构建，如果为null，抛出异常
        Services = services ?? throw new ArgumentNullException(nameof(services));
        
        // 注入 host application lifetime，如果为null，抛出异常
        _applicationLifetime = 
            (applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime))) as ApplicationLifetime;      
        
        if (_applicationLifetime is null)
        {
            throw new ArgumentException(
                "Replacing IHostApplicationLifetime is not supported.", 
                nameof(applicationLifetime));
        }
        
        // 注入 logger，如果为null，抛出异常
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // 注入 host lifetime，如果为null，抛出异常
        _hostLifetime = hostLifetime ?? throw new ArgumentNullException(nameof(hostLifetime));
        // 注入 host options，如果为null，抛出异常
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }                               
}

```

###### 2.3.1.1 hosted service

```c#
public interface IHostedService
{    
    Task StartAsync(CancellationToken cancellationToken);        
    Task StopAsync(CancellationToken cancellationToken);
}

```

###### 2.3.1.2 background service 

```c#
public abstract class BackgroundService : IHostedService, IDisposable
{
    // 执行的任务的引用
    private Task _executeTask;
    public virtual Task ExecuteTask => _executeTask;
    
    private CancellationTokenSource _stoppingCts;
                    
    // 具体任务，在派生类中实现
    protected abstract Task ExecuteAsync(CancellationToken stoppingToken);
        
    /* 启动任务 */
    public virtual Task StartAsync(CancellationToken cancellationToken)
    {
        // Create linked token to allow cancelling executing task from provided token
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        // Store the task we're executing
        _executeTask = ExecuteAsync(_stoppingCts.Token);
        
        // If the task is completed then return it, 
        // this will bubble cancellation and failure to the caller
        if (_executeTask.IsCompleted)
        {
            return _executeTask;
        }
        
        // Otherwise it's running
        return Task.CompletedTask;
    }
    
    /* 结束任务 */
    public virtual async Task StopAsync(CancellationToken cancellationToken)
    {
        // Stop called without start
        if (_executeTask == null)
        {
            return;
        }
        
        try
        {
            // Signal cancellation to the executing method
            _stoppingCts.Cancel();
        }
        finally
        {
            // Wait until the task completes or the stop token triggers
            await Task.WhenAny(_executeTask, Task.Delay(Timeout.Infinite, cancellationToken))
                	  .ConfigureAwait(false);
        }        
    }
    
    public virtual void Dispose()
    {
        _stoppingCts?.Cancel();
    }
}

```

##### 2.3.2 方法

###### 2.3.2.1 接口方法 - host start

```c#
internal class Host : IHost, IAsyncDisposable
{
    public async Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.Starting();
        
        // 创建 combined cts，封装传入的 cancellation token & application lifetime.application stopping        
        using var combinedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, 
            _applicationLifetime.ApplicationStopping);
        
        // 创建 combined ct，引用 combined cts 的 token
        CancellationToken combinedCancellationToken = combinedCancellationTokenSource.Token;
        
        // 启动 host lifetime        
        await _hostLifetime.WaitForStartAsync(combinedCancellationToken)
            			  .ConfigureAwait(false);
        // token 开始监听
        combinedCancellationToken.ThrowIfCancellationRequested();
        
        /* 启动 hosted services */                    
        _hostedServices = Services.GetService<IEnumerable<IHostedService>>();
        // 遍历 hosted service
        foreach (IHostedService hostedService in _hostedServices)
        {
            // 启动 hosted service，与 host 相同 lifetime，因为使用同一个 cancellation token
            await hostedService.StartAsync(combinedCancellationToken).ConfigureAwait(false);
            
            // 如果是 background service，注册 background exception handler
            if (hostedService is BackgroundService backgroundService)
            {
                // 激活 background service exception handler
                _ = HandleBackgroundException(backgroundService);
            }
        }
        
        /* 开启 application lifetime 的 notify，即 exception handler & logger */       
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

###### 2.3.2.2 扩展方法 - host stop

```c#
internal class Host : IHost, IAsyncDisposable
{
    public async Task StopAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.Stopping();
        
        using (var cts = new CancellationTokenSource(_options.ShutdownTimeout))       
        using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken))
        {
            // 创建 cancellatino token（结束 host 的 task 的 token） 
            CancellationToken token = linkedCts.Token;
            
            /* 停止 application lifetime */            
            _applicationLifetime.StopApplication();
            
            /* 停止 hosted services */
            IList<Exception> exceptions = new List<Exception>();
            if (_hostedServices != null) 
            {
                // 反向遍历 hosted service
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
            
            /* 停止 application lifetime notify，即 exception handler & logger*/            
            _applicationLifetime.NotifyStopped();
            
            /* 停止 host lifetime */
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
                    "One or more hosted services failed to stop.", 
                    exceptions);
                
                _logger.StoppedWithException(ex);
                throw ex;
            }
        }
        
        _logger.Stopped();
    }
}

```

###### 2.3.2.3 host dispose

```c#
internal class Host : IHost, IAsyncDisposable
{
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    
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

##### 2.3.3 扩展方法

* recommended method to start、stop the host

###### 2.3.3.1 start

```c#
public static class HostingAbstractionsHostExtensions
{
    public static void Start(this IHost host)
    {
        host.StartAsync().GetAwaiter().GetResult();
    }
}

```

###### 2.3.3.2 wait for stop

```c#
public static class HostingAbstractionsHostExtensions
{
    public static void WaitForShutdown(this IHost host)
    {
        host.WaitForShutdownAsync().GetAwaiter().GetResult();
    }
    
    public static async Task WaitForShutdownAsync(
        this IHost host, 
        CancellationToken token = default)
    {
        // 解析 application lifetime
        IHostApplicationLifetime applicationLifetime = 
            host.Services.GetService<IHostApplicationLifetime>();        
        // 注册 token consume handler，即 application lifetime 的 stop application()
        token.Register(
            state =>
            	{
                    ((IHostApplicationLifetime)state).StopApplication();
                 },
            applicationLifetime);
        
        // 创建 tcs
        var waitForStop = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);        
        // 注册 application lifetime stopping handler，即 tcs 的 set result        
        applicationLifetime.ApplicationStopping.Register(
            obj =>
            	{
                    var tcs = (TaskCompletionSource<object>)obj;
                    tcs.TrySetResult(null);
                 }, 
            waitForStop);
        // 启动 tcs
        await waitForStop.Task.ConfigureAwait(false);
        
        // Host will use its default ShutdownTimeout if none is specified.
        // The cancellation token may have been triggered to unblock waitForStop. 
        // Don't pass it here because that would trigger an abortive shutdown.
        await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
    }        
}

```

###### 2.3.3.3 run

```c#
public static class HostingAbstractionsHostExtensions
{       
    public static void Run(this IHost host)
    {
        host.RunAsync().GetAwaiter().GetResult();
    }
    
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
}

```

###### 2.3.3.4 stop

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

#### 2.4 host builder

##### 2.4.1 接口

```c#
public interface IHostBuilder
{        
    // 配置 host configuration
    IHostBuilder ConfigureHostConfiguration(Action<IConfigurationBuilder> configureDelegate);          
    // 配置 application configuration
    IHostBuilder ConfigureAppConfiguration(Action<HostBuilderContext, IConfigurationBuilder> configureDelegate);       
    
    // 注入 service
    IHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate);   
    
    // 配置 service provider factory
    IHostBuilder UseServiceProviderFactory<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory);     
    IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
        Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> factory);       
    
    // 配置 (service) container builder
    IHostBuilder ConfigureContainer<TContainerBuilder>(Action<HostBuilderContext, TContainerBuilder> configureDelegate);
        
    IDictionary<object, object> Properties { get; }  
        
    IHost Build();
}

```

##### 2.4.2 host builder

```c#
public class HostBuilder : IHostBuilder
{    
    // host configuration
    private IConfiguration _hostConfiguration;    
    // host configuration builder action 集合
    private List<Action<IConfigurationBuilder>> _configureHostConfigActions = new List<Action<IConfigurationBuilder>>();    
    
    // application configuration
    private IConfiguration _appConfiguration;    
    // application configuration builder action 集合
    private List<Action<HostBuilderContext, IConfigurationBuilder>> _configureAppConfigActions = 
        	new List<Action<HostBuilderContext, IConfigurationBuilder>>();
    
    // configure container adapter 集合
    private List<IConfigureContainerAdapter> _configureContainerActions = 
        	new List<IConfigureContainerAdapter>();    
    
    // service collection action 集合
    private List<Action<HostBuilderContext, IServiceCollection>> _configureServicesActions = 
        	new List<Action<HostBuilderContext, IServiceCollection>>();
    
    // service provider factory adapter -> default service provider factory
    private IServiceFactoryAdapter _serviceProviderFactory = 
        new ServiceFactoryAdapter<IServiceCollection>(new DefaultServiceProviderFactory());    
    
    public IDictionary<object, object> Properties { get; } = new Dictionary<object, object>();    
    
    private bool _hostBuilt;                           
    private HostBuilderContext _hostBuilderContext;    
    private HostingEnvironment _hostingEnvironment;    
    private IServiceProvider _appServices;    
}

```

###### 2.4.2.1 host builder context

```c#
public class HostBuilderContext
{
    public IHostEnvironment HostingEnvironment { get; set; }        
    public IConfiguration Configuration { get; set; }        
    public IDictionary<object, object> Properties { get; }
    
    public HostBuilderContext(IDictionary<object, object> properties)
    {
        Properties = properties ?? throw new System.ArgumentNullException(nameof(properties));
    }                
}

```

###### 2.4.2.2 host environment

```c#
// 接口
public interface IHostEnvironment
{    
    string EnvironmentName { get; set; }        
    string ApplicationName { get; set; }        
    string ContentRootPath { get; set; }        
    IFileProvider ContentRootFileProvider { get; set; }
}

// 实现
public class HostingEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; }    
    public string ApplicationName { get; set; }    
    public string ContentRootPath { get; set; }    
    public IFileProvider ContentRootFileProvider { get; set; }
}

// environment name
public static class Environment
{
    public static readonly string Development = "Development";
    public static readonly string Staging = "Staging";
    public static readonly string Production = "Production";
}

// 扩展方法
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

###### 2.4.2.3 configure container adapter

```c#
// 接口
internal interface IConfigureContainerAdapter
{
    void ConfigureContainer(HostBuilderContext hostContext, object containerBuilder);
}

// 实现
internal sealed class ConfigureContainerAdapter<TContainerBuilder> : IConfigureContainerAdapter
{
    private Action<HostBuilderContext, TContainerBuilder> _action;
    
    public ConfigureContainerAdapter(Action<HostBuilderContext, TContainerBuilder> action)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }
    
    public void ConfigureContainer(HostBuilderContext hostContext, object containerBuilder)
    {
        _action(hostContext, (TContainerBuilder)containerBuilder);
    }
}

```

###### 2.4.2.4 service factoryadapter

```c#
// 接口
internal interface IServiceFactoryAdapter
{
    object CreateBuilder(IServiceCollection services);    
    IServiceProvider CreateServiceProvider(object containerBuilder);
}

// 实现
internal class ServiceFactoryAdapter<TContainerBuilder> : IServiceFactoryAdapter
{
    private IServiceProviderFactory<TContainerBuilder> _serviceProviderFactory;
    // func - 创建 host builder context 
    private readonly Func<HostBuilderContext> _contextResolver;
    // func- 由 host builder context 创建 service provider factory
    private Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> _factoryResolver;
    
    public ServiceFactoryAdapter(IServiceProviderFactory<TContainerBuilder> serviceProviderFactory)
    {
        _serviceProviderFactory = 
            serviceProviderFactory ?? throw new ArgumentNullException(nameof(serviceProviderFactory));
    }
    
    public ServiceFactoryAdapter(
        Func<HostBuilderContext> contextResolver, 
        Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> factoryResolver)
    {
        _contextResolver = contextResolver ?? throw new ArgumentNullException(nameof(contextResolver));
        _factoryResolver = factoryResolver ?? throw new ArgumentNullException(nameof(factoryResolver));
    }
    
    public object CreateBuilder(IServiceCollection services)
    {
        if (_serviceProviderFactory == null)
        {
            _serviceProviderFactory = _factoryResolver(_contextResolver());
            
            if (_serviceProviderFactory == null)
            {
                throw new InvalidOperationException(SR.ResolverReturnedNull);
            }
        }
        
        return _serviceProviderFactory.CreateBuilder(services);
    }
    
    public IServiceProvider CreateServiceProvider(object containerBuilder)
    {
        if (_serviceProviderFactory == null)
        {
            throw new InvalidOperationException(SR.CreateBuilderCallBeforeCreateServiceProvider);
        }
        
        return _serviceProviderFactory.CreateServiceProvider((TContainerBuilder)containerBuilder);
    }
}

```



##### 2.4.3 builde host

```c#
public class HostBuilder : IHostBuilder
{
    public IHost Build()
    {
        // 保证只构建一次        
        if (_hostBuilt)
        {
            throw new InvalidOperationException(SR.BuildCalled);
        }        
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
        
        // 从 di 解析 host
        return _appServices.GetRequiredService<IHost>();
    }                                                
}
```

###### 2.4.3.1 a - build host configuration

```c#
public class HostBuilder : IHostBuilder
{
    // 构建 host configuration，用 host configuration actions 配置
    private void BuildHostConfiguration()
    {
        // 创建 configuration builder，
        // 并注入 in memory collection source，(default)，防止抛出异常
        // Make sure there's some default storage since there are no default providers
        IConfigurationBuilder configBuilder = new ConfigurationBuilder().AddInMemoryCollection(); 
        
        // 遍历 host configuration builder actions，配置 host configuration builder
        foreach (Action<IConfigurationBuilder> buildAction in _configureHostConfigActions)
        {
            buildAction(configBuilder);
        }
        
        // 构建 host configuration
        _hostConfiguration = configBuilder.Build();
    }
}

```

###### 2.4.3.2 b - create hosting environment

```c#
public class HostBuilder : IHostBuilder
{
    // 构建 host environment，
    //  - application name = from configuration / or assembly name
    //  - environment name = from configuration / or production
    //  - content root = from configuration / or app context base directory
    //  - content root file provider = provider of content root
    private void CreateHostingEnvironment()
    {        
        // 创建 hosting environment，
        // 从 host configuration（a 创建的）读取信息，并注入 hosting environment
        _hostingEnvironment = new HostingEnvironment()
        {
            // application name
            ApplicationName = _hostConfiguration[HostDefaults.ApplicationKey],
            // environment name
            EnvironmentName = _hostConfiguration[HostDefaults.EnvironmentKey] ?? Environments.Production,
            // content root path
            ContentRootPath = ResolveContentRootPath(
                _hostConfiguration[HostDefaults.ContentRootKey], 
                AppContext.BaseDirectory),
        };
        
        if (string.IsNullOrEmpty(_hostingEnvironment.ApplicationName))
        {
            // Note GetEntryAssembly returns null for the net4x console test runner.
            _hostingEnvironment.ApplicationName = Assembly.GetEntryAssembly()?.GetName().Name;
        }
        
        // 创建 content root file provider（physical file provider）
        _hostingEnvironment.ContentRootFileProvider = new PhysicalFileProvider(_hostingEnvironment.ContentRootPath);
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

###### 2.4.3.3 c - create host build context

```c#
public class HostBuilder : IHostBuilder
{
    // 构建 host builder context，
    //  - configuration = （构建的）host configuration，（会更新为 application configuration）
    //  - hosting environment = （构建的）host environment  
    private void CreateHostBuilderContext()
    {
        // 创建 host builder context，
        // 注入 1- host configuration（a 创建的）；2- hosting environment（b 创建的）
        _hostBuilderContext = new HostBuilderContext(Properties)
        {
            HostingEnvironment = _hostingEnvironment,
            Configuration = _hostConfiguration
        };
    }
}

```

###### 2.4.3.4 d - build app configuration

```c#
public class HostBuilder : IHostBuilder
{
    // 构建 application configuration，
    private void BuildAppConfiguration()
    {                
        // 创建 (app) configuration builder，
        // 设置 base path = content root path；注入 host configuration（a 创建的）
        IConfigurationBuilder configBuilder = new ConfigurationBuilder()
            .SetBasePath(_hostingEnvironment.ContentRootPath)
            .AddConfiguration(_hostConfiguration, shouldDisposeConfiguration: true);
        
        // 配置 (app) configuration builder
        foreach (Action<HostBuilderContext, IConfigurationBuilder> buildAction in _configureAppConfigActions)
        {
            buildAction(_hostBuilderContext, configBuilder);
        }
        
        // 构建 application configuration
        _appConfiguration = configBuilder.Build();
        // 注入 host builder context
        _hostBuilderContext.Configuration = _appConfiguration;
    }
}

```

###### 2.4.3.5 e - create service provider

```c#
public class HostBuilder : IHostBuilder
{
    private void CreateServiceProvider()
    {
        // 创建 service collection（default container builder）
        var services = new ServiceCollection();
            
        // 注册 logging 服务
        services.AddLogging();
                
        // 注入 host environment
        services.AddSingleton<IHostEnvironment>(_hostingEnvironment);
        // 注入 application configuration
        services.AddSingleton(_ => _appConfiguration);
        // 注入 host options
        services.AddOptions().Configure<HostOptions>(options => 
        	{
                options.Initialize(_hostConfiguration); 
            });        
        // 注入 host builder context
        services.AddSingleton(_hostBuilderContext);    
        
        // 注入 host application lifetime -> application life time
        services.AddSingleton<IHostApplicationLifetime, ApplicationLifetime>();
                
        // 注入 host lifetime -> console lifetime
        // 没有注入 console lifetime options，解析到 default
        services.AddSingleton<IHostLifetime, ConsoleLifetime>();
        
        // 注入 host，
        // 构造时注入从 service provider 解析的组件（由 di 控制生命周期）
        services.AddSingleton<IHost>(_ =>
            {
                return new Internal.Host(
                    _appServices,
                    _appServices.GetRequiredService<IHostApplicationLifetime>(),
                    _appServices.GetRequiredService<ILogger<Internal.Host>>(),
                    _appServices.GetRequiredService<IHostLifetime>(),
                    _appServices.GetRequiredService<IOptions<HostOptions>>());
            });
        
                                                        
        // 注入（添加的）service（可以使用 host builder context 作为参数，如 context.properties）
        foreach (Action<HostBuilderContext, IServiceCollection> configureServicesAction in _configureServicesActions)
        {
            configureServicesAction(_hostBuilderContext, services);
        }
        
        /* 构建 service provider */
        // 创建 container builder，使用 configure container actions 配置
        object containerBuilder = _serviceProviderFactory.CreateBuilder(services);      
        foreach (IConfigureContainerAdapter containerAction in _configureContainerActions)
        {
            containerAction.ConfigureContainer(_hostBuilderContext, containerBuilder);
        }
        
        // 由 container builder 创建 service provider
        _appServices = _serviceProviderFactory.CreateServiceProvider(containerBuilder);
        
        if (_appServices == null)
        {
            throw new InvalidOperationException(SR.NullIServiceProvider);
        }
        
        // resolve configuration explicitly once to mark it as resolved within the service provider, 
        // ensuring it will be properly disposed with the provider
        _ = _appServices.GetService<IConfiguration>();
    }
}

```

###### 2.4.3.6 host default

```c#
public static class HostDefaults
{
    public static readonly string EnvironmentKey = "environment";
    public static readonly string ApplicationKey = "applicationName";                    
    public static readonly string ContentRootKey = "contentRoot";
}

```

##### 2.4.4 方法 

```c#
public class HostBuilder : IHostBuilder
{
    // 配置 host configuration（注入 host configuration builder action）
    public IHostBuilder ConfigureHostConfiguration(
        Action<IConfigurationBuilder> configureDelegate)
    {
        _configureHostConfigActions.Add(
            configureDelegate ?? throw new ArgumentNullException(nameof(configureDelegate)));
        
        return this;
    }
    
    // 配置 host application configuration（注入 app configuration builder action）
    public IHostBuilder ConfigureAppConfiguration(
        Action<HostBuilderContext, IConfigurationBuilder> configureDelegate)
    {
        _configureAppConfigActions.Add(
            configureDelegate ?? throw new ArgumentNullException(nameof(configureDelegate)));
        
        return this;
    }
    
    // 注入 service
    public IHostBuilder ConfigureServices(
        Action<HostBuilderContext, IServiceCollection> configureDelegate)
    {
        _configureServicesActions.Add(
            configureDelegate ?? throw new ArgumentNullException(nameof(configureDelegate)));
        
        return this;
    }
    
    // 配置 container builder（注入 container builder action）
    public IHostBuilder ConfigureContainer<TContainerBuilder>(
        Action<HostBuilderContext, TContainerBuilder> configureDelegate)
    {
        _configureContainerActions.Add(new ConfigureContainerAdapter<TContainerBuilder>(
            configureDelegate ?? throw new ArgumentNullException(nameof(configureDelegate))));
        
        return this;
    }
    
    // 设置 service provider factory -> by service provider factory instance
    public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
        IServiceProviderFactory<TContainerBuilder> factory)
    {
        _serviceProviderFactory = new ServiceFactoryAdapter<TContainerBuilder>(
            factory ?? throw new ArgumentNullException(nameof(factory)));
        
        return this;
    }
    
    // 设置 service provider factory -> by service provider factory func
    public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
        Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> factory)
    {
        _serviceProviderFactory = new ServiceFactoryAdapter<TContainerBuilder>(            	
            () => _hostBuilderContext, 
            factory ?? throw new ArgumentNullException(nameof(factory)));
        
        return this;
    }        
}

```

##### 2.4.5 扩展方法

```c#
public static class HostingHostBuilderExtensions
{
    // 注入 environment（替换原有配置）
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
                    environment ?? throw new ArgumentNullException(nameof(environment)))
            });
        });
    }
    
    // 注入 content root（替换原有配置）
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
                    contentRoot ?? throw new ArgumentNullException(nameof(contentRoot)))
            });
        });
    }   
    
    // 配置 app configuration（注入 app configuration builder action）
    public static IHostBuilder ConfigureAppConfiguration(
        this IHostBuilder hostBuilder, 
        Action<IConfigurationBuilder> configureDelegate)
    {
        return hostBuilder.ConfigureAppConfiguration(
            (context, builder) => configureDelegate(builder));
    }
    
    // 注入 host options（合并 configure options）
    public static IHostBuilder ConfigureHostOptions(
        this IHostBuilder hostBuilder, 
        Action<HostBuilderContext, HostOptions> configureOptions)
    {
        return hostBuilder.ConfigureServices(
            (context, collection) => collection.Configure<HostOptions>(options => configureOptions(context, options)));
    }
           
    public static IHostBuilder ConfigureHostOptions(
        this IHostBuilder hostBuilder, 
        Action<HostOptions> configureOptions)
    {
        return hostBuilder.ConfigureServices(collection => collection.Configure(configureOptions));
    }
    
    
    // 注入 service
    public static IHostBuilder ConfigureServices(
        this IHostBuilder hostBuilder, 
        Action<IServiceCollection> configureDelegate)
    {
        return hostBuilder.ConfigureServices(
            (context, collection) => configureDelegate(collection));
    }
    
    // 注入、配置 logging 
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
            (context, collection) => collection.AddLogging(builder => configureLogging(builder)));
    }                 
}

```

##### 2.4.6 扩展方法 - 配置 service provider

```c#
public static class HostingHostBuilderExtensions
{
    // 配置 container builder,
    //（使用 host builder 的 configure container 方法注入 configure container builder action
    public static IHostBuilder ConfigureContainer<TContainerBuilder>(
        this IHostBuilder hostBuilder, 
        Action<TContainerBuilder> configureDelegate)
    {
        return hostBuilder.ConfigureContainer<TContainerBuilder>(
            (context, builder) => configureDelegate(builder));
    }
    
    // 设置 default service provider (options)
    public static IHostBuilder UseDefaultServiceProvider(
        this IHostBuilder hostBuilder, 
        Action<ServiceProviderOptions> configure) => 
        	hostBuilder.UseDefaultServiceProvider((context, options) => configure(options));
        
    // 设置 default service provider (options)，
    // （使用 host builder 的 use service provider factory 方法设置 service provider factory
    public static IHostBuilder UseDefaultServiceProvider(
        this IHostBuilder hostBuilder, 
        Action<HostBuilderContext, ServiceProviderOptions> configure)
    {
        return hostBuilder.UseServiceProviderFactory(context =>
        {
            var options = new ServiceProviderOptions();
            configure(context, options);
            return new DefaultServiceProviderFactory(options);
        });
    }    
}

```

##### 2.4.7 扩展方法 - 注入 hosted service

```c#
/* 注入 hosted service */
    public static IServiceCollection AddHostedService<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors)] THostedService>(this IServiceCollection services) 
        	where THostedService : class, IHostedService
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, THostedService>());        
        return services;
    }
        
    public static IServiceCollection AddHostedService<THostedService>(
        this IServiceCollection services, 
        Func<IServiceProvider, THostedService> implementationFactory)            
        	where THostedService : class, IHostedService
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService>(implementationFactory));        
        return services;
    }
```

##### 2.4.8 扩展方法 - host lifetime

###### 2.4.8.1 console

```c#
public static class HostingHostBuilderExtensions
{
    // use console lifetime
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
        
    // run console 
    public static Task RunConsoleAsync(
        this IHostBuilder hostBuilder, 
        CancellationToken cancellationToken = default)
    {
        return hostBuilder.UseConsoleLifetime()
            			 .Build()
            			 .RunAsync(cancellationToken);
    }
        
    public static Task RunConsoleAsync(
        this IHostBuilder hostBuilder, 
        Action<ConsoleLifetimeOptions> configureOptions, 
        CancellationToken cancellationToken = default)
    {
        return hostBuilder.UseConsoleLifetime(configureOptions)
            			 .Build()
            			 .RunAsync(cancellationToken);        
    }
}

```

###### 2.4.8.2 systemd

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

###### 2.4.8.3 windows service

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
            // Host.CreateDefaultBuilder uses CurrentDirectory for VS scenarios, 
            // but CurrentDirectory for services is c:\Windows\System32.
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

##### 2.4.9 扩展方法 - create default

```c#
public static IHostBuilder ConfigureDefaults(this IHostBuilder builder, string[] args)
{
    // 设置 content root 为 current directory
    builder.UseContentRoot(Directory.GetCurrentDirectory());
    
    // 注入 host configuration
    builder.ConfigureHostConfiguration(
        config =>
        {
            // 环境变量 - “DOTNET_”开头
            config.AddEnvironmentVariables(prefix: "DOTNET_");
            // 命令行参数
            if (args is { Length: > 0 })
            {
                config.AddCommandLine(args);
            }
        });
    
    builder
        /* 配置 application configuration */
        .ConfigureAppConfiguration((hostingContext, config) =>
        	{
                // 从 host builder context 解析 hosting environment（env name，dev/staging/prod）                
                IHostEnvironment env = hostingContext.HostingEnvironment;    
                
                // 从 host builder context 解析 configuration，进而解析 reload configuration onchange（默认 true）
                bool reloadOnChange = hostingContext.Configuration
                								 .GetValue("hostBuilder:reloadConfigOnChange", defaultValue: true);
                
                // 注入 appsettings.json & appsettings.{env}.json 配置文件
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: reloadOnChange)
                      .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: reloadOnChange);
                
                // 如果 env 是 development，且 application name 不为空，
                // 加载 user secret 配置
                if (env.IsDevelopment() && 
                    env.ApplicationName is { Length: > 0 })
                {
                    var appAssembly = Assembly.Load(new AssemblyName(env.ApplicationName));
                    if (appAssembly is not null)
                    {
                        config.AddUserSecrets(appAssembly, optional: true, reloadOnChange: reloadOnChange);
                    }
                }
                
                // 注入环境变量配置源
                config.AddEnvironmentVariables();
                
                // 注入命令行参数
                if (args is { Length: > 0 })
                {
                    config.AddCommandLine(args);
                }
            })
        /* 配置 logging */
        .ConfigureLogging((hostingContext, logging) =>
            {
                bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                
                // IMPORTANT: This needs to be added *before* configuration is loaded, this lets
                // the defaults be overridden by the configuration.
                if (isWindows)
                {
                    // Default the EventLogLoggerProvider to warning or above
                    logging.AddFilter<EventLogLoggerProvider>(level => level >= LogLevel.Warning);
                }
                
                logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                logging.AddConsole();
                logging.AddDebug();
                logging.AddEventSourceLogger();
                
                if (isWindows)
                {
                    // Add the EventLogLoggerProvider on windows machines
                    logging.AddEventLog();
                }
                
                logging.Configure(options =>
                                  {
                                      options.ActivityTrackingOptions = ActivityTrackingOptions.SpanId |
	                                       							 ActivityTrackingOptions.TraceId |
						                                              ActivityTrackingOptions.ParentId;
                                  });
                
            })
        /* 配置 service provider */
        .UseDefaultServiceProvider((context, options) =>
            {
                // 如果 env 是 development，-> validate scope
                bool isDevelopment = context.HostingEnvironment.IsDevelopment();
                options.ValidateScopes = isDevelopment;
                options.ValidateOnBuild = isDevelopment;
            });
    
    return builder;
}

```

#### 2.5 host logging

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

#### 2.6 host 静态类

```c#
public static class Host
{    
    public static IHostBuilder CreateDefaultBuilder() => CreateDefaultBuilder(args: null);
    
    public static IHostBuilder CreateDefaultBuilder(string[] args)
    {
        HostBuilder builder = new();
        return builder.ConfigureDefaults(args);
    }
}

```

