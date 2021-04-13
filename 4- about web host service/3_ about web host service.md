## about web host as service

### 1. about

#### 1.1 overview

##### 1.1.1 what's web host service

web host 是托管 web 服务的 host。

在 dotnetcore 3.0 以前，使用 web host 托管 web 服务。与 host 类似，它包含解析、释放服务的 service provider、lifetime 等属性，同时包含很多 web 应用特有的服务。

在 dotnetcore 3.0 以后，很多基础功能转移到了 host 中，web host 不再作为单独的 host 使用，而是封装成了 hosted service，由 host 执行。对应的，generic web host builder 用于构建 web host service，它不能直接构建 web host，而是注入 web host service 所需的服务，然后由 di 创建 generic web host service。

##### 1.1.2 what did the service do

web host service 创建并管理着 web app，

* IServer 监听 http 请求
* IHttpApplication 封装 http 请求并创建 http context
* 由 IMiddlware 构建的 request delegate  链式处理 http 请并返回 response

所需服务由 di 解析，它们在 web host builder 中配置并注入 di

##### 1.1.3 how to configure the service

对 web host service 的配置体现在“注册服务”和“配置请求管道”上，分别对应着`ConfigureServices`和`Configure[ApplicaitonBuilder]`方法，还有配置 configuration、environment 等

###### 1.1.3.1 startup

可以通过封装的 startup 类配置，它可以是强约束的（实现IStartup），也可以不实现 IStartup，但必须包含`ConfigureServices`和`Configure`方法

###### 1.1.3.2 hosting startup

第三方配置 startup

###### 1.1.3.3 startup filter

？？？

#### 1.2 how designed

##### 1.2.1 generic web host service

封装的 web host，实现 start 和 stop 

###### 1.2.1.1 start

* 解析 url
* 创建 request delegate
  * from startup 
  * from startup filter（后执行）
* 创建 hosting application
  * 创建 (host) context
  * 执行 request delegate
  * 解构 (host) context
* 启动 server
  * 开启监听
  * 使用 hosting application
* 开启日志

###### 1.2.1.2 stop

* 停止 server
* 停止日志

##### 1.2.2 generic web host builder

* 注册 generic web host 所需的服务
* 不能直接构建 web host，它的功能由`generic web host service` 代替
* 可以使用 web host builder 的扩展方法配置

##### 1.2.3 hosting startup web host builder

封装第三方 startup 配置

##### 1.2.4 use startup

* startup 是没有确定签名的、用于配置 web host 的类，包括2个方法
  * configure services
  * configure (application builder)
* 动态加载 startup，并使用 configure services、configure 方法配置 web host builder

###### 1.2.4.1 实现 ISupportStartup 接口的 web host builder

* 使用接口的 configure services、configure 方法
* 实质是调用 startup loader 的方法创建 startup method

###### 1.2.4.2 没有实现 ISupportStartup 接口的 web host builder

* 创建 startup 实体类
  * delegate startup，用 func 委托配置 services、application builder
  * convention startup，使用注入的 startup method 包含的 configure services、configure 委托

##### 1.2.4 使用 web host service

利用 host builder 的扩展方法可以配置 web host builder，进而创建 web host 服务

### 2. details

#### 2.1 generic web host service 

```c#
internal class GenericWebHostService : IHostedService    
{
    public GenericWebHostServiceOptions Options { get; }
    public IServer Server { get; }
    public ILogger Logger { get; }
    // Only for high level lifetime events
    public ILogger LifetimeLogger { get; }
    
    /* web services */
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
        
        /* 注入 web service，
           在 generic web host builder 中创建 */
        DiagnosticListener = diagnosticListener;
        HttpContextFactory = httpContextFactory;
        ApplicationBuilderFactory = applicationBuilderFactory;
        StartupFilters = startupFilters;
        Configuration = configuration;
        HostingEnvironment = hostingEnvironment;
    }                        
}

```

##### 2.1.1 start

```c#
internal class GenericWebHostService : IHostedService    
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        HostingEventSource.Log.HostStart();
        
        /* 解析 url address */
        // 从 configuration 中解析 url，并注入 server address feature
        var serverAddressesFeature = Server.Features
            							   .Get<IServerAddressesFeature>();
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
        
        /* 创建 request delegate */
        RequestDelegate? application = null;
        
        try
        {
            // 解析 generic web host options 的 action<applicationBuilder>            
            var configure = Options.ConfigureApplication;   
            // 如果没有，抛出异常
            if (configure == null)
            {
                throw new InvalidOperationException(
                    $"No application configured. Please specify an application via IWebHostBuilder.UseStartup, IWebHostBuilder.Configure, or specifying the startup assembly via {nameof(WebHostDefaults.StartupAssemblyKey)} in the web host configuration.");
            }
            // 构建 application builder
            var builder = ApplicationBuilderFactory
                .CreateBuilder(Server.Features);
            // 加载 startup filter 的 action<applicationBuilder>，
            // 合并。。。
            foreach (var filter in StartupFilters.Reverse())
            {
                configure = filter.Configure(configure);
            }
            // 用 action<applicationBuilder> 配置 application builder
            configure(builder);
            
            // 用 application builder 构建 request delegate            
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
        
        /* 创建 hosting application */
        var httpApplication = new HostingApplication(
            application, 
            Logger, 
            DiagnosticListener, 
            HttpContextFactory);
        
        /* 启动 server */
        await Server.StartAsync(httpApplication, cancellationToken);
        
        /* 日志 */
        
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
}

```

##### 2.1.2 stop 

```c#
internal class GenericWebHostService : IHostedService    
{
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

##### 2.1.3 web host service options

###### 2.1.3.1 generic web host service options

```c#
internal class GenericWebHostServiceOptions
{
    public Action<IApplicationBuilder>? ConfigureApplication { get; set; }
    
    // Always set when options resolved by DI
    public WebHostOptions WebHostOptions { get; set; } = default!;     
    public AggregateException? HostingStartupExceptions { get; set; }
}

```

###### 2.1.3.2 web host options

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
            configuration[WebHostDefaults.ApplicationKey] ?? applicationNameFallback;
        
        Environment = 
            configuration[WebHostDefaults.EnvironmentKey];
        
        ContentRootPath = 
            configuration[WebHostDefaults.ContentRootKey];
        
        WebRoot = 
            configuration[WebHostDefaults.WebRootKey];
        
        StartupAssembly = 
            configuration[WebHostDefaults.StartupAssemblyKey];
        
        // Search the primary assembly and configured assemblies.
        HostingStartupAssemblies = Split(
            $"{ApplicationName};{configuation[WebHostDefaults.HostingStartupAssembliesKey]}");
        
        HostingStartupExcludeAssemblies = Split(
            configuration[WebHostDefaults.HostingStartupExcludeAssembliesKey]);
        
        PreventHostingStartup = WebHostUtilities.ParseBool(
            configuration, 
            WebHostDefaults.PreventHostingStartupKey);         
        
        SuppressStatusMessages = WebHostUtilities.ParseBool(
            configuration, 
            WebHostDefaults.SuppressStatusMessagesKey);        
        
        DetailedErrors = WebHostUtilities.ParseBool(
            configuration, 
            WebHostDefaults.DetailedErrorsKey);        
        
        CaptureStartupErrors = WebHostUtilities.ParseBool(
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
            StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
    }
}

```

##### 2.1.4 startup filter

###### 2.1.4.1 接口

```c#
public interface IStartupFilter
{        
    Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next);
}

```

###### 2.1.4.2 host filtering startup filter

```c#
internal class HostFilteringStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseHostFiltering();
            next(app);
        };
    }
}

```

###### 2.1.4.3 middleware filter builder startup filter

```c#

```

#### 2.2 web host builder

##### 2.2.1 web host builder 接口

```c#
public interface IWebHostBuilder
{   
    /* build web host */
    IWebHost Build();
    
    /* configure service，注册服务 */
    IWebHostBuilder ConfigureServices(
         Action<IServiceCollection> configureServices);
        
    IWebHostBuilder ConfigureServices(
        Action<WebHostBuilderContext, IServiceCollection> 
        	configureServices);
    
    /* configure (configuration builder) */
    IWebHostBuilder ConfigureAppConfiguration(
        Action<WebHostBuilderContext, IConfigurationBuilder> 
        	configureDelegate);
    
    /* get & set setting */
    string? GetSetting(string key);  
    
    IWebHostBuilder UseSetting(string key, string? value);
}

```

##### 2.2.2 web host builder 扩展方法

###### 2.2.2.1 configure application builder

```c#
public static class WebHostBuilderExtensions
{    
    public static IWebHostBuilder Configure(
        this IWebHostBuilder hostBuilder, 
        Action<IApplicationBuilder> configureApp)
    {
        return hostBuilder.Configure(
            (_, app) => configureApp(app), 
            configureApp.GetMethodInfo()
            	.DeclaringType!.Assembly.GetName().Name!);
    }
        
    public static IWebHostBuilder Configure(
        this IWebHostBuilder hostBuilder, 
        Action<WebHostBuilderContext, IApplicationBuilder> configureApp)
    {
        return hostBuilder.Configure(
            configureApp, 
            configureApp.GetMethodInfo()
            	.DeclaringType!.Assembly.GetName().Name!);
    }
    
    // 真正实现配置 application builder 的方法
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
        
        /* 如果 builder 实现了 support startup 接口，
           使用 support startup 的 configure 方法 */
        // Light up the ISupportsStartup implementation
        if (hostBuilder is ISupportsStartup supportsStartup)
        {
            return supportsStartup.Configure(configureApp);
        }
        
        /* 否则，注册 delegate startup，暴露为 IStartup */
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

###### 2.2.2.2 use startup

```c#
public static class WebHostBuilderExtensions
{        
    public static IWebHostBuilder UseStartup
        <[DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods)]TStartup>(
        this IWebHostBuilder hostBuilder, 
        Func<WebHostBuilderContext, TStartup> startupFactory) 
        	where TStartup : class
    {
        if (startupFactory == null)
        {
            throw new ArgumentNullException(nameof(startupFactory));
        }
        
        var startupAssemblyName = startupFactory.GetMethodInfo()
            .DeclaringType!.Assembly.GetName().Name;
        
        hostBuilder.UseSetting(
            WebHostDefaults.ApplicationKey, 
            startupAssemblyName);
        
        /* 如果 builder 实现了 support startup 接口，
           使用 support startup 的 use startup 方法 */
        // Light up the GenericWebHostBuilder implementation
        if (hostBuilder is ISupportsStartup supportsStartup)
        {
            return supportsStartup.UseStartup(startupFactory);
        }
        
        /* 否则，注入 convention based startup，暴露为 IStartup */
        return hostBuilder.ConfigureServices(
            (context, services) =>
            {
                services.AddSingleton(
                    typeof(IStartup), 
                    sp =>
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
        
    public static IWebHostBuilder UseStartup<
        [DynamicallyAccessedMembers(
            StartupLinkerOptions.Accessibility)]TStartup>(
        this IWebHostBuilder hostBuilder) 
        	where TStartup : class
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
        
        hostBuilder.UseSetting(
            WebHostDefaults.ApplicationKey, 
            startupAssemblyName);
        
        /* 如果 builder 实现了 support startup 接口，
           使用 support startup 的 use startup 方法 */
        // Light up the GenericWebHostBuilder implementation
        if (hostBuilder is ISupportsStartup supportsStartup)
        {
            return supportsStartup.UseStartup(startupType);
        }
        /* 否则，注册服务 convention based startup，暴露为 IStartup */
        return hostBuilder.ConfigureServices(
            services =>
            {
                if (typeof(IStartup).IsAssignableFrom(startupType))
                {
                    services.AddSingleton(typeof(IStartup), startupType);
                }
                else
                {
                    services.AddSingleton(
                        typeof(IStartup), 
                        sp =>
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

###### 2.2.2.3 use default service provider

```c#
public static class WebHostBuilderExtensions
{          
    public static IWebHostBuilder UseDefaultServiceProvider(
        this IWebHostBuilder hostBuilder, 
        Action<ServiceProviderOptions> configure)
    {
        return hostBuilder
            .UseDefaultServiceProvider((context, options) => configure(options));
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
        
        return hostBuilder.ConfigureServices((context, services) =>                                     	{
            	var options = new ServiceProviderOptions();
            	configure(context, options);                
            	services.Replace(
                    ServiceDescriptor.Singleton<
                    	IServiceProviderFactory<IServiceCollection>>(
                            new DefaultServiceProviderFactory(options)));
        	});
    }
}

```

###### 2.2.2.4 use server

```c#
public static class HostingAbstractionsWebHostBuilderExtensions
{                                            
    public static IWebHostBuilder UseServer(
        this IWebHostBuilder hostBuilder, 
        IServer server)
    {
        if (server == null)
        {
            throw new ArgumentNullException(nameof(server));
        }
        
        return hostBuilder.ConfigureServices(services =>            
        	{
                // It would be nicer if this was transient but we need to pass in the
                // factory instance directly
                services.AddSingleton(server);
            });
    }
}

```

###### 2.2.2.5 configure app configuration

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
 
public static class HostingAbstractionsWebHostBuilderExtensions
{
    
    public static IWebHostBuilder UseConfiguration(
        this IWebHostBuilder hostBuilder, 
        IConfiguration configuration)
    {
        foreach (var setting in 
                 configuration.AsEnumerable(makePathsRelative: true))
        {
            hostBuilder.UseSetting(setting.Key, setting.Value);
        }
        
        return hostBuilder;
    }    
}

```

###### 2.2.2.6 configure logging

```c#
public static class WebHostBuilderExtensions
{ 
     public static IWebHostBuilder ConfigureLogging(
        this IWebHostBuilder hostBuilder, 
        Action<ILoggingBuilder> configureLogging)
    {
        return hostBuilder.ConfigureServices(
            collection => collection.AddLogging(configureLogging));
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

###### 2.2.2.7 use settings

```c#
public static class HostingAbstractionsWebHostBuilderExtensions
{
    [RequiresUnreferencedCode(
        "Types and members the loaded assembly depends on might be removed.")]
    public static IWebHostBuilder UseStartup(
        this IWebHostBuilder hostBuilder, 
        string startupAssemblyName)
    {
        if (startupAssemblyName == null)
        {
            throw new ArgumentNullException(nameof(startupAssemblyName));
        }
        
        return hostBuilder
            .UseSetting(
            	WebHostDefaults.ApplicationKey, 
            	startupAssemblyName)
            .UseSetting(
            	WebHostDefaults.StartupAssemblyKey, 
            	startupAssemblyName);
    }
    
    public static IWebHostBuilder CaptureStartupErrors(
        this IWebHostBuilder hostBuilder, 
        bool captureStartupErrors)
    {
        return hostBuilder.UseSetting(
            WebHostDefaults.CaptureStartupErrorsKey, 
            captureStartupErrors ? "true" : "false");
    }
    
    public static IWebHostBuilder UseEnvironment(
        this IWebHostBuilder hostBuilder, 
        string environment)
    {
        if (environment == null)
        {
            throw new ArgumentNullException(nameof(environment));
        }
        
        return hostBuilder.UseSetting(
            WebHostDefaults.EnvironmentKey, 
            environment);
    }
        
    public static IWebHostBuilder UseContentRoot(
        this IWebHostBuilder hostBuilder, 
        string contentRoot)
    {
        if (contentRoot == null)
        {
            throw new ArgumentNullException(nameof(contentRoot));
        }
        
        return hostBuilder.UseSetting(
            WebHostDefaults.ContentRootKey, 
            contentRoot);
    }
        
    public static IWebHostBuilder UseWebRoot(
        this IWebHostBuilder hostBuilder, 
        string webRoot)
    {
        if (webRoot == null)
        {
            throw new ArgumentNullException(nameof(webRoot));
        }
        
        return hostBuilder.UseSetting(
            WebHostDefaults.WebRootKey, 
            webRoot);
    }
        
    public static IWebHostBuilder UseUrls(
        this IWebHostBuilder hostBuilder, 
        params string[] urls)
    {
        if (urls == null)
        {
            throw new ArgumentNullException(nameof(urls));
        }
        
        return hostBuilder.UseSetting(
            WebHostDefaults.ServerUrlsKey, 
            string.Join(';', urls));
    }
        
    public static IWebHostBuilder PreferHostingUrls(
        this IWebHostBuilder hostBuilder, 
        bool preferHostingUrls)
    {
        return hostBuilder.UseSetting(
            WebHostDefaults.PreferHostingUrlsKey, 
            preferHostingUrls ? "true" : "false");
    }
        
    public static IWebHostBuilder SuppressStatusMessages(
        this IWebHostBuilder hostBuilder, 
        bool suppressStatusMessages)
    {
        return hostBuilder.UseSetting(
            WebHostDefaults.SuppressStatusMessagesKey, 
            suppressStatusMessages ? "true" : "false");
    }
        
    public static IWebHostBuilder UseShutdownTimeout(
        this IWebHostBuilder hostBuilder, 
        TimeSpan timeout)
    {
        return hostBuilder.UseSetting(
            WebHostDefaults.ShutdownTimeoutKey, 
            ((int)timeout.TotalSeconds).ToString(CultureInfo.InvariantCulture));
    }
}
```

##### 2.2.3 support startup 接口

```c#
internal interface ISupportsStartup
{
    // configure application builder
    IWebHostBuilder Configure(
        Action<WebHostBuilderContext, IApplicationBuilder> configure);
    // use startup
    IWebHostBuilder UseStartup
        ([DynamicallyAccessedMembers(
            StartupLinkerOptions.Accessibility)] 
         Type startupType);
    // use startup T
    IWebHostBuilder UseStartup
        <[DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicMethods)]TStartup>(
        Func<WebHostBuilderContext, TStartup> startupFactory);
}

```

##### 2.2.4 support default service provider

```c#
internal interface ISupportsUseDefaultServiceProvider
{
    IWebHostBuilder UseDefaultServiceProvider(
        Action<WebHostBuilderContext, 
        ServiceProviderOptions> configure);
}

```

##### 2.2.5 startup

###### 2.2.5.1 IStartup 接口

```c#
public interface IStartup
{    
    IServiceProvider ConfigureServices(IServiceCollection services);         
    void Configure(IApplicationBuilder app);
}

```

###### 2.2.5.2 startup base

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

###### 2.2.5.3 delegate startup

```c#
public class DelegateStartup : StartupBase<IServiceCollection>
{    
    // 注入配置 application builder 的 action
    private Action<IApplicationBuilder> _configureApp;            
    public DelegateStartup(
        IServiceProviderFactory<IServiceCollection> factory, 
        Action<IApplicationBuilder> configureApp) : 
    		base(factory)
    {
        _configureApp = configureApp;
    }
            
    // 使用注入的 application builder action 配置 application builder
    public override void Configure(IApplicationBuilder app) => 
        _configureApp(app);
}

```

###### 2.2.5.4 convention startup

```c#
internal class ConventionBasedStartup : IStartup
{
    // 注入 startup method
    private readonly StartupMethods _methods;    
    public ConventionBasedStartup(StartupMethods methods)
    {
        _methods = methods;
    }    
    // 使用 startup method 的委托配置
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
    // 使用 startup method 的委托配置
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

##### 2.2.6 startup filter

* startup filter 在 configure(applicationBuilder) 方法前配置 IApplicationBuilder

###### 2.2.6.1 接口

```c#
public interface IStartupFilter
{       
    Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next);
}

```

###### 2.2.6.2 host filtering startup filter

```c#
internal class HostFilteringStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseHostFiltering();
            next(app);
        };
    }
}

```

###### 2.2.6.3 forwarded header startup filter

```c#
internal class ForwardedHeadersStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseForwardedHeaders();
            next(app);
        };
    }
}

```





#### 2.3 startup

##### 2.3.1 startup method

* 封装 configure services、configure (application builder)

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

##### 2.3.2 startup loader

```c#
internal class StartupLoader
{
    public static StartupMethods LoadMethods(
        IServiceProvider hostingServiceProvider, 
        [DynamicallyAccessedMembers(
            StartupLinkerOptions.Accessibility)] Type startupType, 
        string environmentName, 
        object? instance = null)
    {
        /* 加载 startup 中的对应方法，
           并创建 builder 委托 */
        
        // action<IApplicationBuilder>
        var configureMethod = 
            FindConfigureDelegate(
            startupType,
            environmentName);    
        // func<IServiceCollection,IServiceProvider>
        var servicesMethod = 
            FindConfigureServicesDelegate(
            startupType, 
            environmentName);    
        // action<object>
        var configureContainerMethod = 
            FindConfigureContainerDelegate(
            startupType, 
            environmentName);
        
        /* 创建 instance */
        if (instance == null && 
            (!configureMethod.MethodInfo.IsStatic || 
             (servicesMethod?.MethodInfo != null && 
              !servicesMethod.MethodInfo.IsStatic)))
        {
            instance = ActivatorUtilities
                .GetServiceOrCreateInstance(hostingServiceProvider, startupType);
        }
        
        /* 获取 container builder 类型 */
        // The type of the TContainerBuilder. 
        // If there is no ConfigureContainer method we can just use object as it's not
        // going to be used for anything.
        var type = configureContainerMethod.MethodInfo != null 
            ? configureContainerMethod.GetContainerType() 
            : typeof(object);
        
        /* 创建 configure service delegate builder <TContainerBuilder> */
        var builder = (ConfigureServicesDelegateBuilder)Activator.CreateInstance(
            typeof(ConfigureServicesDelegateBuilder<>).MakeGenericType(type),
            hostingServiceProvider,
            servicesMethod,
            configureContainerMethod,
            instance)!;
        
        /* 创建 startup methods */
        return new StartupMethods(
            instance, 
            configureMethod.Build(instance), 	// func<IServcieCollection,IServiceProvider>
            builder.Build());					// action<IApplicaitonBuilder>
    }
}

```

##### 2.3.3 find method

```c#
private static MethodInfo? FindMethod(
    [DynamicallyAccessedMembers(
        StartupLinkerOptions.Accessibility)] Type startupType, 
    string methodName, 
    string environmentName, 
    Type? returnType = null, 
    bool required = true)
{
    /* 拼接 method name，带有 env 和 不带有 env */
    var methodNameWithEnv = string.Format(
        CultureInfo.InvariantCulture, 
        methodName, 
        environmentName);
    var methodNameWithNoEnv = string.Format(
        CultureInfo.InvariantCulture, 
        methodName, 
        "");
    
    /* 从 startup 中获取 method（with env name） */
    var methods = startupType.GetMethods(
        BindingFlags.Public | 
        BindingFlags.Instance | 
        BindingFlags.Static);
    var selectedMethods = methods
        .Where(method => 
               method.Name.Equals(
                   methodNameWithEnv, 
                   tringComparison.OrdinalIgnoreCase))
        .ToList();
    
    if (selectedMethods.Count > 1)
    {
        // 有多个 method，抛出异常
        throw new InvalidOperationException(
            $"Having multiple overloads of method '{methodNameWithEnv}' 
            "is not supported.");
    }
    // 如果没找匹配的 method，获取 method（without env name）
    if (selectedMethods.Count == 0)
    {
        selectedMethods = methods
            .Where(method => 
                   method.Name.Equals(
                       methodNameWithNoEnv, 
                       StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (selectedMethods.Count > 1)
        {
            throw new InvalidOperationException(
                $"Having multiple overloads of method '{methodNameWithNoEnv}' 
                "is not supported.");
        }
    }
    
    /* 验证，
       required=false 抑制抛出异常 */
    var methodInfo = selectedMethods.FirstOrDefault();
    if (methodInfo == null)
    {
        // 没有找到 method
        if (required)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.CurrentCulture,
                "A public method named '{0}' or '{1}' could not be found in the '{2}' type.",
                methodNameWithEnv,
                methodNameWithNoEnv,
                startupType.FullName));            
        }
        return null;
    }
    if (returnType != null && 
        methodInfo.ReturnType != returnType)
    {
        // method 的 return type 为匹配       
        if (required)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.CurrentCulture,
                "The '{0}' method in the type '{1}' must have a return type of '{2}'.",
                methodInfo.Name,
                startupType.FullName,
                returnType.Name));
        }
        return null;
    }
    return methodInfo;
}

```

###### 2.3.2.1 find configure delegate

* configure builder 定义

```c#
internal class ConfigureBuilder
{
    // 注入 method info
    public MethodInfo MethodInfo { get; }        
    public ConfigureBuilder(MethodInfo configure)
    {
        MethodInfo = configure;
    }
    
    public Action<IApplicationBuilder> Build(object? instance) => 
        builder => Invoke(instance, builder);
    
    private void Invoke(object? instance, IApplicationBuilder builder)
    {
        // Create a scope for Configure, this allows creating scoped dependencies
        // without the hassle of manually creating a scope.
        using (var scope = builder.ApplicationServices.CreateScope())
        {
            var serviceProvider = scope.ServiceProvider;
            var parameterInfos = MethodInfo.GetParameters();
            var parameters = new object[parameterInfos.Length];
            for (var index = 0; index < parameterInfos.Length; index++)
            {
                var parameterInfo = parameterInfos[index];
                if (parameterInfo.ParameterType == typeof(IApplicationBuilder))
                {
                    parameters[index] = builder;
                }
                else
                {
                    try
                    {
                        parameters[index] = serviceProvider
                            .GetRequiredService(parameterInfo.ParameterType);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(string.Format(
                            CultureInfo.InvariantCulture,
                            "Could not resolve a service of type '{0}' 
                            "for the parameter '{1}' of method '{2}' on type '{3}'.",
                            parameterInfo.ParameterType.FullName,
                            parameterInfo.Name,
                            MethodInfo.Name,
                            MethodInfo.DeclaringType?.FullName), ex);
                    }
                }
            }
            
            MethodInfo.InvokeWithoutWrappingExceptions(instance, parameters);
        }
    }
}
    
```

* 实现

```c#
internal static ConfigureBuilder FindConfigureDelegate(
    [DynamicallyAccessedMembers(
        StartupLinkerOptions.Accessibility)] Type startupType, 
    string environmentName)
{
    var configureMethod = FindMethod(
        startupType, 
        "Configure{0}", 
        environmentName, 
        typeof(void), 
        required: true)!;
    
    return new ConfigureBuilder(configureMethod);
}
        
```

###### 2.3.2.2 find configure service delegate

* configure service builder 定义

```c#
internal class ConfigureServicesBuilder
{        
    // 注入 method info
    public MethodInfo? MethodInfo { get; }
    public ConfigureServicesBuilder(MethodInfo? configureServices)
    {
        MethodInfo = configureServices;
    }
                   
    public Func<IServiceCollection, IServiceProvider?> Build(object instance) => 
        services => Invoke(instance, services);
    
    private IServiceProvider? Invoke(object instance, IServiceCollection services)
    {
        return StartupServiceFilters(Startup)(services);
        
        IServiceProvider? Startup(IServiceCollection serviceCollection) => 
            InvokeCore(instance, serviceCollection);
    }
    
    public Func<
        	   Func<IServiceCollection, IServiceProvider?>, 
    		   Func<IServiceCollection, IServiceProvider?>> 
           StartupServiceFilters { get; set; } = f => f;
    
    private IServiceProvider? InvokeCore(object instance, IServiceCollection services)
    {
        if (MethodInfo == null)
        {
            return null;
        }
        
        // Only support IServiceCollection parameters
        var parameters = MethodInfo.GetParameters();
        if (parameters.Length > 1 ||
            parameters.Any(p => 
                           p.ParameterType != typeof(IServiceCollection)))
        {
            throw new InvalidOperationException(
                "The ConfigureServices method must either be parameterless 
                "or take only one parameter of type IServiceCollection.");
        }
        
        var arguments = new object[MethodInfo.GetParameters().Length];
        
        if (parameters.Length > 0)
        {
            arguments[0] = services;
        }
        
        return MethodInfo.InvokeWithoutWrappingExceptions(instance, arguments) 
            as IServiceProvider;
    }
}

```

* 实现

```c#
internal static ConfigureServicesBuilder FindConfigureServicesDelegate(
    [DynamicallyAccessedMembers(
        StartupLinkerOptions.Accessibility)] Type startupType, 
    string environmentName)
{
    var servicesMethod = 
        FindMethod(
        	startupType, 
	        "Configure{0}Services", 
    	    environmentName, 
        	typeof(IServiceProvider), 
	        required: false)
       	?? FindMethod(
        	startupType, 
        	"Configure{0}Services", 
        	environmentName, 
        	typeof(void), 
        	required: false);
    
    return new ConfigureServicesBuilder(servicesMethod);
}
```

###### 2.3.2.3 find configure container delegate

* configure container builder 定义

```c#
internal class ConfigureContainerBuilder
{
    // 注入 method info
    public MethodInfo? MethodInfo { get; }
    public ConfigureContainerBuilder(MethodInfo? configureContainerMethod)
    {
        MethodInfo = configureContainerMethod;
    }
            
    public Func<Action<object>, Action<object>> 
        ConfigureContainerFilters { get; set; } = f => f;
    
    public Action<object> Build(object instance) => 
        container => Invoke(instance, container);
            
    private void Invoke(object instance, object container)
    {
        ConfigureContainerFilters(StartupConfigureContainer)(container);
        
        void StartupConfigureContainer(object containerBuilder) =>
            InvokeCore(instance, containerBuilder);
    }
    
    private void InvokeCore(object instance, object container)
    {
        if (MethodInfo == null)
        {
            return;
        }
        
        var arguments = new object[1] { container };
        
        MethodInfo.InvokeWithoutWrappingExceptions(instance, arguments);
    }
    
    public Type GetContainerType()
    {
        Debug.Assert(
            MethodInfo != null, 
            "Shouldn't be called when there is no Configure method.");
        
        var parameters = MethodInfo.GetParameters();
        if (parameters.Length != 1)
        {
            // REVIEW: This might be a breaking change
            throw new InvalidOperationException(
                $"The {MethodInfo.Name} method must take only one parameter.");
        }
        return parameters[0].ParameterType;
    }
}

```

* 实现

```c#
internal static ConfigureContainerBuilder FindConfigureContainerDelegate(
    [DynamicallyAccessedMembers(
        StartupLinkerOptions.Accessibility)] Type startupType, 
    string environmentName)
{
    var configureMethod = FindMethod(
        startupType, 
        "Configure{0}Container", 
        environmentName, 
        typeof(void), 
        required: false);
    
    return new ConfigureContainerBuilder(configureMethod);
}

```

##### 2.3.4 configure service delegate builder

###### 2.3.4.1 抽象基类

```c#
private abstract class ConfigureServicesDelegateBuilder
{
    public abstract Func<IServiceCollection, IServiceProvider> Build();
}

```

###### 2.3.4.2 实现

```c#
private class ConfigureServicesDelegateBuilder<TContainerBuilder> 
    : ConfigureServicesDelegateBuilder where TContainerBuilder : notnull
{
    public IServiceProvider HostingServiceProvider { get; }
    public ConfigureServicesBuilder ConfigureServicesBuilder { get; }
    public ConfigureContainerBuilder ConfigureContainerBuilder { get; }
    public object Instance { get; }
        
    public ConfigureServicesDelegateBuilder(
        IServiceProvider hostingServiceProvider,
        ConfigureServicesBuilder configureServicesBuilder,
        ConfigureContainerBuilder configureContainerBuilder,
        object instance)
    {
        HostingServiceProvider = hostingServiceProvider;
        ConfigureServicesBuilder = configureServicesBuilder;
        ConfigureContainerBuilder = configureContainerBuilder;
        Instance = instance;
    }
            
    // build
    public override Func<IServiceCollection, IServiceProvider> Build()
    {
        // a - 创建 func<IServiceCollection,IServiceProvider>
        ConfigureServicesBuilder
            .StartupServiceFilters = BuildStartupServicesFilterPipeline;
        var configureServicesCallback = ConfigureServicesBuilder.Build(Instance);
        
        // b - 创建 action<object>
        ConfigureContainerBuilder
            .ConfigureContainerFilters = ConfigureContainerPipeline;
        var configureContainerCallback = ConfigureContainerBuilder.Build(Instance);
        
        // c - 创建并返回 configure services 委托
        return ConfigureServices(configureServicesCallback, configureContainerCallback);
        
        
    }
}

```

###### 2.3.4.3 创建 configure service callback

```c#
private class ConfigureServicesDelegateBuilder<TContainerBuilder> 
{
    private Func<IServiceCollection, IServiceProvider?> 
        BuildStartupServicesFilterPipeline(
        	Func<IServiceCollection, IServiceProvider?> startup)
    {
        return RunPipeline;
        
        IServiceProvider? RunPipeline(IServiceCollection services)
        {
            
            var filters = HostingServiceProvider
                .GetRequiredService<IEnumerable<IStartupConfigureServicesFilter>>()     	   
                .ToArray();
            
            // If there are no filters just run startup 
            // (makes IServiceProvider ConfigureServices(IServiceCollection services) work.
            if (filters.Length == 0)
            {
                return startup(services);
            }
            
            Action<IServiceCollection> pipeline = InvokeStartup;
            for (int i = filters.Length - 1; i >= 0; i--)
            {
                pipeline = filters[i].ConfigureServices(pipeline);
            }
            
            pipeline(services);
            
            // We return null so that the host here builds the container 
            // (same result as void ConfigureServices(IServiceCollection services);
            return null;
            
            void InvokeStartup(IServiceCollection serviceCollection)
            {
                var result = startup(serviceCollection);
                if (filters.Length > 0 && result != null)
                {
                    // public IServiceProvider ConfigureServices                        
                    // (IServiceCollection serviceCollection) 
                    // is not compatible with IStartupServicesFilter;
                    
                    var message = 
                        $"A ConfigureServices method that 
                        "returns an {nameof(IServiceProvider)} is " +
                        $"not compatible with the use of one or more 
                        "{nameof(IStartupConfigureServicesFilter)}. " +
                        $"Use a void returning ConfigureServices method 
                        "instead or a ConfigureContainer method.";
                    throw new InvalidOperationException(message);
                };
            }
        }
    }        
}

```

###### 2.3.4.4 创建 configure container callback

```c#
private class ConfigureServicesDelegateBuilder<TContainerBuilder> 
{
    public override Func<IServiceCollection, IServiceProvider> Build()
    {
        Action<object> ConfigureContainerPipeline(Action<object> action)
        {
            return Target;
            
            // The ConfigureContainer pipeline needs an Action<TContainerBuilder> as source, 
            // so we just adapt the signature with this function.
            void Source(TContainerBuilder containerBuilder) =>
                action(containerBuilder);
            
            // The ConfigureContainerBuilder.
            // ConfigureContainerFilters expects an Action<object> as value, 
            // but our pipeline produces an Action<TContainerBuilder> given a source, 
            // so we wrap it on an Action<object> that internally casts
            // the object containerBuilder to TContainerBuilder to match the 
            // expected signature of our ConfigureContainer pipeline.
            void Target(object containerBuilder) =>
                BuildStartupConfigureContainerFiltersPipeline
                	(Source)((TContainerBuilder)containerBuilder);
        }
    }
    
    private Action<TContainerBuilder> BuildStartupConfigureContainerFiltersPipeline(
        Action<TContainerBuilder> configureContainer)
    {
        return RunPipeline;
        
        void RunPipeline(TContainerBuilder containerBuilder)
        {
            var filters = HostingServiceProvider.GetRequiredService<
                IEnumerable<IStartupConfigureContainerFilter<TContainerBuilder>>>();
            
            Action<TContainerBuilder> pipeline = InvokeConfigureContainer;
            foreach (var filter in filters.Reverse())
            {
                pipeline = filter.ConfigureContainer(pipeline);
            }
            
            pipeline(containerBuilder);
            
            void InvokeConfigureContainer(TContainerBuilder builder) => 
                configureContainer(builder);
        }
    }    
}

```

###### 2.3.4.5 configure service

```c#
private class ConfigureServicesDelegateBuilder<TContainerBuilder> 
{
    Func<IServiceCollection, IServiceProvider> ConfigureServices(
        Func<IServiceCollection, IServiceProvider?> configureServicesCallback,
        Action<object> configureContainerCallback)
    {
        return ConfigureServicesWithContainerConfiguration;
        
        IServiceProvider 
            ConfigureServicesWithContainerConfiguration(IServiceCollection services)
        {
            // Call ConfigureServices, if that returned an IServiceProvider, we're done
            var applicationServiceProvider = configureServicesCallback.Invoke(services);
            
            if (applicationServiceProvider != null)
            {
                return applicationServiceProvider;
            }
            
            // If there's a ConfigureContainer method
            if (ConfigureContainerBuilder.MethodInfo != null)
            {
                var serviceProviderFactory = 
                    HostingServiceProvider
                    	.GetRequiredService<IServiceProviderFactory<TContainerBuilder>>();
                var builder = 
                    serviceProviderFactory.CreateBuilder(services);
                configureContainerCallback(builder);
                applicationServiceProvider = 
                    serviceProviderFactory.CreateServiceProvider(builder);
            }
            else
            {
                // Get the default factory
                var serviceProviderFactory = 
                    HostingServiceProvider
                    	.GetRequiredService<IServiceProviderFactory<IServiceCollection>>();
                var builder = 
                    serviceProviderFactory.CreateBuilder(services);
                applicationServiceProvider = 
                    serviceProviderFactory.CreateServiceProvider(builder);
            }
            
            return applicationServiceProvider ?? services.BuildServiceProvider();
        }
    }
}

```

###### 2.3.4.6 find startup type

* 按照约定，查找 startup 类型（名称为 startup）

```c#
private class ConfigureServicesDelegateBuilder<TContainerBuilder> 
{
    [UnconditionalSuppressMessage(
        "ReflectionAnalysis", 
        "IL2026:RequiresUnreferencedCode", 
        Justification = "We're warning at the entry point. This is an implementation detail.")]
    public static Type FindStartupType(
        string startupAssemblyName, 
        string environmentName)
    {
        // startup assembly 为空，抛出异常
        if (string.IsNullOrEmpty(startupAssemblyName))
        {
            throw new ArgumentException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    "A startup method, startup type or startup assembly is required. 
                    "If specifying an assembly, '{0}' cannot be null or empty.",
                    nameof(startupAssemblyName)),
                nameof(startupAssemblyName));
        }
        
        // 加载 assembly，如果为 null，抛出异常
        var assembly = Assembly.Load(new AssemblyName(startupAssemblyName));
        if (assembly == null)
        {
            throw new InvalidOperationException(
                $"The assembly '{startupAssemblyName}' failed to load.");
        }
        
        var startupNameWithEnv = "Startup" + environmentName;
        var startupNameWithoutEnv = "Startup";
        
        /* 查找 startup，
           按约定，以 startup 命名 */
        // Check the most likely places first
        var type =
            assembly.GetType(startupNameWithEnv) ??
            assembly.GetType(startupAssemblyName + "." + startupNameWithEnv) ??
            assembly.GetType(startupNameWithoutEnv) ??
            assembly.GetType(startupAssemblyName + "." + startupNameWithoutEnv);
        
        if (type == null)
        {
            // Full scan
            var definedTypes = assembly.DefinedTypes.ToList();
            
            var startupType1 = definedTypes
                .Where(info => 
                       info.Name.Equals(
                           startupNameWithEnv, 
                           StringComparison.OrdinalIgnoreCase));
            
            var startupType2 = definedTypes
                .Where(info => 
                       info.Name.Equals(
                           startupNameWithoutEnv, 
                           StringComparison.OrdinalIgnoreCase));
            
            var typeInfo = startupType1.Concat(startupType2).FirstOrDefault();
            if (typeInfo != null)
            {
                type = typeInfo.AsType();
            }
        }
        
        // 如果没找到 startup type，抛出异常
        if (type == null)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.CurrentCulture,
                "A type named '{0}' or '{1}' could not be found in assembly '{2}'.",
                startupNameWithEnv,
                startupNameWithoutEnv,
                startupAssemblyName));
        }
        
        return type;
    }
}

```

###### 2.3.4.7 has configure service method

```c#
private class ConfigureServicesDelegateBuilder<TContainerBuilder> 
{
    internal static bool HasConfigureServicesIServiceProviderDelegate(
        [DynamicallyAccessedMembers(
            StartupLinkerOptions.Accessibility)] Type startupType, 
        string environmentName)
    {
        return null != FindMethod(
            startupType, 
            "Configure{0}Services", 
            environmentName, 
            typeof(IServiceProvider), 
            required: false);
    }
}

```

#### 2.4 generic web host builder

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
        
     public GenericWebHostBuilder(
        IHostBuilder builder, 
        WebHostBuilderOptions options)
    {
        /* 注入 host builder */
        _builder = builder;
        
        /* 配置 host builder 的 host configuration */
        
        /* 配置 host builder 的 app configuration */
                        
        /* 配置（注入） host builder 的 configure services */  
    }
        
    // ...
}

```

##### 2.4.1 配置 host configuration

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
        // 如果没有标记抑制 environment configuration，
        // 加载环境变量 ASPNETCORE_
        if (!options.SuppressEnvironmentConfiguration)
        {
            configBuilder.AddEnvironmentVariables(prefix: "ASPNETCORE_");
        }
        
        // 构建 configuration
        _config = configBuilder.Build();        
        
        // 加载 host configuration
        _builder.ConfigureHostConfiguration(config =>
       	{
            // 注入加载的环境变量配置（如果没有标记抑制）
            config.AddConfiguration(_config);            
            // We do this super early but still late enough 
            // that we can process the configuration
            // wired up by calls to UseSetting
            ExecuteHostingStartups();
        });
    }
}

```

###### 2.4.1.1 execute hosting startups

```c#
internal class GenericWebHostBuilder
{
    private void ExecuteHostingStartups()
    {
        // 用加载的 configuration 创建 web host options
        var webHostOptions = new WebHostOptions(
            _config, 
            Assembly.GetEntryAssembly()
            	?.GetName().Name ?? string.Empty);

        // 如果 web host options 设置禁止 hosting startup，返回
        if (webHostOptions.PreventHostingStartup)
        {
            return;
        }
        
        /* 加载 hosting startup 类型，
           注入到 hosting startup web host builder */
        
        var exceptions = new List<Exception>();        
        // 创建 hosting startup web host builder
        _hostingStartupWebHostBuilder = new HostingStartupWebHostBuilder(this);
                
        // 解析 web host options 中的 hosting startup assembly 名字，
        // 遍历。。。
        foreach (var assemblyName in 
                 webHostOptions
                 	.GetFinalHostingStartupAssemblies()
                 	.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                // 加载 hosting startup assembly
                var assembly = Assembly.Load(new AssemblyName(assemblyName));
                
                // 获取 assembly 中 hosting startup attribute，
                // 因为 hosting startup attribute 实现了 IHostingStartup 接口，
                // 使用 IHostingStartup 配置创建的 hosting startup web host builder
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

###### 2.4.1.2 hosting startup attribute

```c#
[AttributeUsage(
    AttributeTargets.Assembly, 
    Inherited = false, 
    AllowMultiple = true)]
public sealed class HostingStartupAttribute : Attribute
{    
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes
        	.PublicParameterlessConstructor)]
    public Type HostingStartupType { get; }
    
    public HostingStartupAttribute(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicParameterlessConstructor)] Type hostingStartupType)
    {
        if (hostingStartupType == null)
        {
            throw new ArgumentNullException(nameof(hostingStartupType));
        }
        
        if (!typeof(IHostingStartup).IsAssignableFrom(hostingStartupType))
        {
            throw new ArgumentException(
                $@"""{hostingStartupType}"" does not implement 
                ""{typeof(IHostingStartup)}.", 
                nameof(hostingStartupType));
        }
        
        HostingStartupType = hostingStartupType;        
    }        
}

```

###### 2.4.1.3 IHostingStartup

```c#
public interface IHostingStartup
{    
    void Configure(IWebHostBuilder builder);
}

```

##### 2.4.2 configure app configuration

```c#
internal class GenericWebHostBuilder
{
    public GenericWebHostBuilder(
        IHostBuilder builder, 
        WebHostBuilderOptions options)
    {
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

###### 2.4.2.1 get web host builder context

```c#
internal class GenericWebHostBuilder
{       
    private WebHostBuilderContext GetWebHostBuilderContext(
        HostBuilderContext context)
    {
        // 如果不能从 host builder context 中解析，
        // 创建 web host builder context 并注入 host builder context（缓存）
        if (!context.Properties.TryGetValue(
            	typeof(WebHostBuilderContext), 
            	out var contextVal))
        {
            // 创建 web host options
            var options = new WebHostOptions(
                context.Configuration, 
                Assembly.GetEntryAssembly()
                	?.GetName().Name ?? string.Empty);
            // 创建 web host builder context
            var webHostBuilderContext = new WebHostBuilderContext
            {
                Configuration = context.Configuration,
                HostingEnvironment = new HostingEnvironment(),
            };
            // web host builer context 初始化
            webHostBuilderContext
                .HostingEnvironment
                .Initialize(
                	context.HostingEnvironment.ContentRootPath, 
                	options);
            
            // 将 web host builder context 注入 host builder context
            context.Properties[typeof(WebHostBuilderContext)] = webHostBuilderContext;
            context.Properties[typeof(WebHostOptions)] = options;
            
            return webHostBuilderContext;
        }
        
        /* 从 host builder context 解析*/
        // Refresh config, it's periodically updated/replaced
        var webHostContext = (WebHostBuilderContext)contextVal;
        webHostContext.Configuration = context.Configuration;
        
        return webHostContext;
    }
}

```

###### 2.4.2.2 web host builder context

```c#
public class WebHostBuilderContext
{
    public IWebHostEnvironment HostingEnvironment { get; set; } = default!;        
    public IConfiguration Configuration { get; set; } = default!;
}

```

##### 2.4.3 configure services

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
            
            /* 注册服务 
            	diagnostic listener
            	diagnostic source
            	http context factory
            	middleware factory
            	application builder factory */
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

##### 2.4.4  实现 web host builder 接口

```c#
internal class GenericWebHostBuilder
{
    /* build，
       不能创建 web host，由 hosted service 代替 */    
    public IWebHost Build()
    {
        throw new NotSupportedException(
            $"Building this implementation of {nameof(IWebHostBuilder)} is not supported.");
    }
    
    /* configure app configuration */    
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
    
    /* configure service */
    
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
    
    /* get & set settings */
    
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

##### 2.4.5 实现 support startup 接口

```c#
internal class GenericWebHostBuilder
{
    /* configure application builder */    
    public IWebHostBuilder Configure(
            Action<WebHostBuilderContext, IApplicationBuilder> configure)
    {
        // Clear the startup type
        _startupObject = configure;
        
        _builder.ConfigureServices((context, services) =>
        {
            if (object.ReferenceEquals(_startupObject, configure))
            {
                services.Configure<GenericWebHostServiceOptions>
                    options =>
                	{
                    	var webhostBuilderContext = 
                            GetWebHostBuilderContext(context);
                        options.ConfigureApplication = 
                            app => configure(webhostBuilderContext, app);
                    });
                }
            });

            return this;
    }
        
    // use startup
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
    
    // use startup T
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
    
    /* the real method to use startup */
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
            /* startup type 中的 configure services 方法不能返回 IServicesProvider */
            
            /* 如果 startup type 实现了 IStartup，
               即 configure services 的返回类型是 IServicesProvider，抛出异常 */
            // We cannot support methods that return IServiceProvider as that is terminal 
            // and we need ConfigureServices to compose
            if (typeof(IStartup).IsAssignableFrom(startupType))
            {
                throw new NotSupportedException($"{typeof(IStartup)} isn't supported");
            }
            /* 如果 startup type 中的 configure services 方法，
               返回类型是 IServicesProvider，抛出异常*/
            if (StartupLoader
                	.HasConfigureServicesIServiceProviderDelegate(
                    	startupType, 
                    	context.HostingEnvironment.EnvironmentName))
            {
                throw new NotSupportedException(
                    $"ConfigureServices returning an 
                    "{typeof(IServiceProvider)} isn't supported.");
            }
            
            // 创建 host service provider，注入 host builder context 中
            instance ??= ActivatorUtilities.CreateInstance(
                new HostServiceProvider(webHostBuilderContext), 
                startupType);
            
            context.Properties[_startupKey] = instance;
            
            /* 创建 configure services builder,
               由其创建 configure services 的委托，
               即 func<IServiceCollection,IServiceProvider>，并调用 */
            // Startup.ConfigureServices
            var configureServicesBuilder = 
                StartupLoader.FindConfigureServicesDelegate(
                	startupType, 
                	context.HostingEnvironment.EnvironmentName);
            var configureServices = configureServicesBuilder.Build(instance);
            // configure services
            configureServices(services);
            
            /* 创建 configure container builder，
               从 configure container builder 中获取 container type 并创建，
               即创建了 service collection（默认情况）*/
               
            // REVIEW: We're doing this in the callback 
            // so that we have access to the hosting environment Startup.ConfigureContainer
            var configureContainerBuilder = 
                StartupLoader.FindConfigureContainerDelegate(
	                startupType, 
                	context.HostingEnvironment.EnvironmentName);
            if (configureContainerBuilder.MethodInfo != null)
            {
                /* 缓存 container type */
                var containerType = configureContainerBuilder.GetContainerType();
                // Store the builder in the property bag
                _builder.Properties[
                    typeof(ConfigureContainerBuilder)] = configureContainerBuilder;
                
                var actionType = 
                    typeof(Action<,>)
                    	.MakeGenericType(
                    		typeof(HostBuilderContext), 
                    		containerType);
                
                /* 使用 configure container builder 的 build 方法，
                   构建 configure container 委托，即 action<object> */
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
            
            /* 解析配置 application builder 的委托*/
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
        
        /* 注册 generic web host builder options，
           注入了上述 configure application builder 的方法 */
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

###### 2.4.5.1 configure container impl

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
}

```

###### 2.4.5.2 host service provider

```c#
internal class GenericWebHostBuilder
{
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

##### 2.4.6 实现 support default service provider 接口

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

#### 2.5 hosting startup web host builder

```c#
internal class HostingStartupWebHostBuilder : 
	IWebHostBuilder, 
	ISupportsStartup, 
	ISupportsUseDefaultServiceProvider
{                        
    // 注入 generic web host builder
    private readonly GenericWebHostBuilder _builder;
    public HostingStartupWebHostBuilder(GenericWebHostBuilder builder)
    {
        _builder = builder;
    }
            
    /* configure service */
    private Action<WebHostBuilderContext, IServiceCollection>? 
        _configureServices;    
        
    public void ConfigureServices(
        WebHostBuilderContext context, 
        IServiceCollection services)
    {
        _configureServices?.Invoke(context, services);
    }
    
    /* configure configuration */
    private Action<WebHostBuilderContext, IConfigurationBuilder>? 
        _configureConfiguration;
        
    public void ConfigureAppConfiguration(
        WebHostBuilderContext context, 
        IConfigurationBuilder builder)
    {
        _configureConfiguration?.Invoke(context, builder);
    }                
}

```

##### 2.5.1 实现 web host builder 接口

```c#
internal class HostingStartupWebHostBuilder
{
    public IWebHost Build()
    {
        throw new NotSupportedException(
            $"Building this implementation of {nameof(IWebHostBuilder)} is not supported.");
    }
    
    /* configure configuration，注册传入的 action 委托 */
    public IWebHostBuilder ConfigureAppConfiguration(
        Action<WebHostBuilderContext, IConfigurationBuilder> configureDelegate)
    {
        _configureConfiguration += configureDelegate;
        return this;
    }
    
    /* configure service，注册传入的 action 委托*/
    
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
    
    /* get & set settings */
    
    public string GetSetting(string key) => _builder.GetSetting(key);
    
    public IWebHostBuilder UseSetting(string key, string? value)
    {
        _builder.UseSetting(key, value);
        return this;
    }
}

```

##### 2.5.2 实现 support startup 接口

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

##### 2.5.3 实现 support default service provider 接口

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

#### 2.6 创建 generic web host service

##### 2.6.1 host builder -> configure web host

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
        
        // 配置 web host builder options
        var webHostBuilderOptions = new WebHostBuilderOptions();
        configureWebHostBuilder(webHostBuilderOptions);
        
        // 配置 web host builder
        var webhostBuilder = new GenericWebHostBuilder(builder, webHostBuilderOptions);
        configure(webhostBuilder);
        
        // 注入 generic web host service
        builder.ConfigureServices((context, services) => 
        	services.AddHostedService<GenericWebHostService>());
        
        return builder;
    }
}

```

##### 2.6.2 web host builder -> configure web defaults

```c#
public static class WebHost
{
    internal static void ConfigureWebDefaults(IWebHostBuilder builder)
    {
        // 配置 app configuration
        builder.ConfigureAppConfiguration((ctx, cb) =>
        	{
                if (ctx.HostingEnvironment.IsDevelopment())
                {
                    StaticWebAssetsLoader.UseStaticWebAssets(
                        ctx.HostingEnvironment, 
                        ctx.Configuration);
                }
            });
        
        builder
            // 使用 kestrel
            .UseKestrel((builderContext, options) =>
        	{
                options.Configure(
                    builderContext.Configuration.GetSection("Kestrel"), 
                    reloadOnChange: true);
            })
            // 注入 host filter 配置
            .ConfigureServices((hostingContext, services) =>
            {
                /* 配置 host filtering options */
                // Fallback
                services.PostConfigure<HostFilteringOptions>(options =>
                {
                    if (options.AllowedHosts == null || 
                        options.AllowedHosts.Count == 0)
                    {
                        // "AllowedHosts": "localhost;127.0.0.1;[::1]"
                        var hosts = hostingContext
                            .Configuration["AllowedHosts"]
                            ?.Split(
                            	new[] { ';' }, 
                            	StringSplitOptions.RemoveEmptyEntries);
                        // Fall back to "*" to disable.
                        options.AllowedHosts = (hosts?.Length > 0 ? hosts : new[] { "*" });
                    }
                });
                /* 注入 change token */
                // Change notification
                services.AddSingleton<IOptionsChangeTokenSource<HostFilteringOptions>>(                             new ConfigurationChangeTokenSource<HostFilteringOptions>(
                   	    hostingContext.Configuration));
                
				/* 注入 host filtering startup filter*/
                services.AddTransient<IStartupFilter, HostFilteringStartupFilter>();
				
                // 注入 forward service
                if (string.Equals(
                    "true", 
                    hostingContext.Configuration["ForwardedHeaders_Enabled"], 
                    StringComparison.OrdinalIgnoreCase))
                {
                    services.Configure<ForwardedHeadersOptions>(options =>
                    {
                        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | 
                            					   ForwardedHeaders.XForwardedProto;
                        // Only loopback proxies are allowed by default. 
                        // Clear that restriction because forwarders are
                        // being enabled by explicit configuration.
                        options.KnownNetworks.Clear();
                        options.KnownProxies.Clear();
                    });

                    services.AddTransient<IStartupFilter, ForwardedHeadersStartupFilter>();
                }
				// 注入 routing
                services.AddRouting();
            })
            .UseIIS()
            .UseIISIntegration();
    }
}

```

##### 2.6.3 default web host

```c#
public static class GenericHostBuilderExtensions
{        
    public static IHostBuilder ConfigureWebHostDefaults(
        this IHostBuilder builder, 
        Action<IWebHostBuilder> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }
        
        // ihostbuilder 扩展方法
        return builder.ConfigureWebHost(webHostBuilder =>
        	{
                // 使用 WebHost 的静态方法
                WebHost.ConfigureWebDefaults(webHostBuilder);                
                configure(webHostBuilder);
            });
    }
}

```

#### 3. practice

* 在 host 中注册 web host service，并配置









