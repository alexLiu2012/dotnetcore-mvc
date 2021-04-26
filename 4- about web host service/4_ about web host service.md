## about web host as service



### 1. about

#### 1.1 summary

web host 是托管 web 服务的 host。在 dotnetcore 3.0 以前，使用 web host 托管 web 服务。与 host 类似，它包含解析、释放服务的 service provider、lifetime 等属性，同时包含很多 web 应用特有的服务；在 dotnetcore 3.0 以后，很多基础功能转移到了 host 中，web host 不再作为单独的 host 使用，而是封装成了 hosted service，由 host 执行。

对应的，generic web host builder 用于构建 web host service，它不能直接构建 web host，而是注入 web host service 所需的服务，然后由 di 创建 generic web host service。

web host service 创建并管理着 web app，

* IServer 监听 http 请求
* IHttpApplication 封装 http 请求并创建 http context
* 由 IMiddlware 构建的 request delegate  链式处理 http 请并返回 response

所需服务由 di 解析，它们在 web host builder 中配置并注入 di

#### 1.2 how designed

对 web host service 的配置体现在“注册服务”和“配置请求管道”上，分别对应着`ConfigureServices`和`Configure[ApplicaitonBuilder]`方法，还有配置 configuration、environment 等

可以通过封装的 startup 类配置，它可以是强约束的（实现IStartup），也可以不实现 IStartup，但必须包含`ConfigureServices`和`Configure`方法

第三方配置 startup 通过 hosting startup 特性标记

##### 1.2.1 generic web host service

封装的 web host 服务，管理 web 服务

###### 1.2.1.1 start

解析 url => 创建并 application builder => 创建 hosting application => 启动 server => 开启日志

###### 1.2.1.2 stop

停止 server => 停止日志

##### 1.2.2 generic web host builder

* 注册 generic web host 所需的服务
* 不能直接构建 web host，它的功能由`generic web host service` 代替
* 可以使用 web host builder 的扩展方法配置

##### 1.2.3 hosting startup web host builder

包含 hosting startup filter

##### 1.2.4 使用 web host service

利用 host builder 的扩展方法可以配置 web host builder，进而创建 web host 服务

### 2. web host

#### 2.1 startup

```c#
public interface IStartup
{    
    // configure services（注册服务）
    IServiceProvider ConfigureServices(IServiceCollection services);         
    // configure（配置管道）
    void Configure(IApplicationBuilder app);
}

```

##### 2.1.1 startup base

```c#
public abstract class StartupBase : IStartup
{            
    // -> services provider
    IServiceProvider IStartup.ConfigureServices(IServiceCollection services)
    {
        // 配置 service collection（注册服务）
        ConfigureServices(services);
        return CreateServiceProvider(services);
    }
    
    // 在派生类重写 create service provider 方法
    public virtual IServiceProvider CreateServiceProvider(IServiceCollection services)
    {
        return services.BuildServiceProvider();
    }
    
    // configure services（注册服务）
    public virtual void ConfigureServices(IServiceCollection services)
    {
    }
    
    // configure（配置管道）
    public abstract void Configure(IApplicationBuilder app);                  
}

```

##### 2.1.2 startup base of t

```c#
public abstract class StartupBase<TBuilder> : StartupBase where TBuilder : notnull
{
   
    private readonly IServiceProviderFactory<TBuilder> _factory;            
    public StartupBase(IServiceProviderFactory<TBuilder> factory)
    {
        // 注入 service provider factory of t
        _factory = factory;
    }
        
    // 重写 create service provider
    public override IServiceProvider CreateServiceProvider(IServiceCollection services)
    {
        var builder = _factory.CreateBuilder(services);
        ConfigureContainer(builder);
        return _factory.CreateServiceProvider(builder);
    }
        
    // 在派生类实现（配置 container）
    public virtual void ConfigureContainer(TBuilder builder)
    {
    }
}

```

##### 2.1.3 具体实现

###### 2.1.3.1 delegate startup

* configure service 的返回类型是 void

```c#
// 使用 service collection 作为 container，不需要重写 configure container
// by "application builder action"
public class DelegateStartup : StartupBase<IServiceCollection>
{        
    private Action<IApplicationBuilder> _configureApp;   
    
    public DelegateStartup(
        IServiceProviderFactory<IServiceCollection> factory, 
        Action<IApplicationBuilder> configureApp) : base(factory)
    {
        // 注入 application builder action
        _configureApp = configureApp;
    }
            
    // 重写 configure application builder 方法，即注入的 application builder action
    public override void Configure(IApplicationBuilder app) => _configureApp(app);
}

```

###### 2.1.3.2 convention based startup

* configure service 的返回类型是 service provider

```c#
internal class ConventionBasedStartup : IStartup
{    
    private readonly StartupMethods _methods;    
    public ConventionBasedStartup(StartupMethods methods)
    {
        // 注入 startup method
        _methods = methods;
    }    
    
    // 使用 startup method 的 configure services
    public IServiceProvider ConfigureServices(IServiceCollection services)
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
    
    // 使用 startup method 的 configure
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

##### 2.1.4 startup method

```c#
internal class StartupMethods
{
    public object? StartupInstance { get; }
    
    // configure service
    public Func<IServiceCollection, IServiceProvider> ConfigureServicesDelegate { get; }       
    // configure
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

#### 2.2 web host builder

```c#
public interface IWebHostBuilder
{   
    // 配置 application configuration
    IWebHostBuilder ConfigureAppConfiguration(Action<WebHostBuilderContext, IConfigurationBuilder> configureDelegate);
    // 配置 services（返回 void）
    IWebHostBuilder ConfigureServices(Action<IServiceCollection> configureServices);        
    IWebHostBuilder ConfigureServices(Action<WebHostBuilderContext, IServiceCollection> configureServices);
    // settings    
    string? GetSetting(string key);      
    IWebHostBuilder UseSetting(string key, string? value);
   
    IWebHost Build();    
}

```

##### 2.2.1 web host builder context

```c#
public class WebHostBuilderContext
{
    public IWebHostEnvironment HostingEnvironment { get; set; } = default!;        
    public IConfiguration Configuration { get; set; } = default!;
}

```

##### 2.2.2 扩展方法 - 注入 settings

```c#
public static class HostingAbstractionsWebHostBuilderExtensions
{
    // startup assembly name
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
        
        return hostBuilder.UseSetting(WebHostDefaults.ApplicationKey, startupAssemblyName)
            			 .UseSetting(WebHostDefaults.StartupAssemblyKey, startupAssemblyName);
    }
    
    // capture startup errors (flag)
    public static IWebHostBuilder CaptureStartupErrors(
        this IWebHostBuilder hostBuilder, 
        bool captureStartupErrors)
    {
        return hostBuilder.UseSetting(
            WebHostDefaults.CaptureStartupErrorsKey, 
            captureStartupErrors ? "true" : "false");
    }
    
    // environment (dev/stage/prod)
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
        
    // (web host) content root
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
        
    // web root
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
        
    // url
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
        
    // prefer url
    public static IWebHostBuilder PreferHostingUrls(
        this IWebHostBuilder hostBuilder, 
        bool preferHostingUrls)
    {
        return hostBuilder.UseSetting(
            WebHostDefaults.PreferHostingUrlsKey, 
            preferHostingUrls ? "true" : "false");
    }
        
    // suppress status message (flag)
    public static IWebHostBuilder SuppressStatusMessages(
        this IWebHostBuilder hostBuilder, 
        bool suppressStatusMessages)
    {
        return hostBuilder.UseSetting(
            WebHostDefaults.SuppressStatusMessagesKey, 
            suppressStatusMessages ? "true" : "false");
    }
        
    // shutdown timeout
    public static IWebHostBuilder UseShutdownTimeout(
        this IWebHostBuilder hostBuilder, 
        TimeSpan timeout)
    {
        return hostBuilder.UseSetting(
            WebHostDefaults.ShutdownTimeoutKey, 
            ((int)timeout.TotalSeconds).ToString(CultureInfo.InvariantCulture));
    }
    
    // configuration
    public static IWebHostBuilder UseConfiguration(
        this IWebHostBuilder hostBuilder, 
        IConfiguration configuration)
    {
        // 遍历 configuration（kv pair），
        foreach (var setting in configuration.AsEnumerable(makePathsRelative: true))
        {
            // 注入 web host builder 的 setting
            hostBuilder.UseSetting(setting.Key, setting.Value);
        }
        
        return hostBuilder;
    }    
}

```

##### 2.2.3 扩展方法 - 注入服务

```c#
public static class WebHostBuilderExtensions
{ 
    // 注入 logging，with action<logging builder>
    public static IWebHostBuilder ConfigureLogging(
        this IWebHostBuilder hostBuilder, 
        Action<ILoggingBuilder> configureLogging)
    {
        return hostBuilder.ConfigureServices(collection => collection.AddLogging(configureLogging));
    }
      
    // 注入 logging，with action<builder context, logging builder>
    public static IWebHostBuilder ConfigureLogging(
        this IWebHostBuilder hostBuilder, 
        Action<WebHostBuilderContext, ILoggingBuilder> configureLogging)
    {
        return hostBuilder.ConfigureServices((context, collection) => 
        	collection.AddLogging(builder => configureLogging(context, builder)));
    }
}
    
public static class HostingAbstractionsWebHostBuilderExtensions
{           
    // 注入 server, with server instance
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
                // It would be nicer if this was transient but we need to pass in the factory instance directly
                services.AddSingleton(server);
            });
    }
}

// 其他 server

```

##### 2.2.4 扩展方法 - 配置 application configuration

```c#
public static class WebHostBuilderExtensions
{                  
    public static IWebHostBuilder ConfigureAppConfiguration(
        this IWebHostBuilder hostBuilder, 
        Action<IConfigurationBuilder> configureDelegate)
    {
        // 注入 action<host builder context, configuration builder>
        return hostBuilder.ConfigureAppConfiguration((context, builder) => configureDelegate(builder));
    }
}

```

##### 2.2.5 扩展方法 - 配置 application builder

```c#
public static class WebHostBuilderExtensions
{    
    // with <application builder> action
    public static IWebHostBuilder Configure(
        this IWebHostBuilder hostBuilder, 
        Action<IApplicationBuilder> configureApp)
    {
        return hostBuilder.Configure(
            (_, app) => configureApp(app), 
            configureApp.GetMethodInfo().DeclaringType!.Assembly.GetName().Name!);
    }
        
    // with <web host builder context, application builder> action
    public static IWebHostBuilder Configure(
        this IWebHostBuilder hostBuilder, 
        Action<WebHostBuilderContext, IApplicationBuilder> configureApp)
    {
        return hostBuilder.Configure(
            configureApp, 
            configureApp.GetMethodInfo().DeclaringType!.Assembly.GetName().Name!);
    }
    
    // wtih action & startup assembly name
    private static IWebHostBuilder Configure(
        this IWebHostBuilder hostBuilder, 
        Action<WebHostBuilderContext, IApplicationBuilder> configureApp, 
        string startupAssemblyName)
    {
        if (configureApp == null)
        {
            throw new ArgumentNullException(nameof(configureApp));
        }
        
        // 注入 setting [applicationKey, startup assembly name]
        hostBuilder.UseSetting(
            WebHostDefaults.ApplicationKey, 
            startupAssemblyName);
        
        // 如果 builder 实现了 support startup 接口，使用 support startup 的 configure 方法         
        if (hostBuilder is ISupportsStartup supportsStartup)
        {
            return supportsStartup.Configure(configureApp);
        }
        
        // （由上，否则），注入 service <IStarup, delegate startup> 
        return hostBuilder.ConfigureServices((context, services) =>            
            {
                services.AddSingleton<IStartup>(sp =>
                {
                    return new DelegateStartup(
                        sp.GetRequiredService<IServiceProviderFactory<IServiceCollection>>(), 
                        (app => configureApp(context, app)));
                });
            });
    }
}

```

##### 2.2.6 扩展方法 - use default service provider

```c#
public static class WebHostBuilderExtensions
{          
    // with action<service provider options>
    public static IWebHostBuilder UseDefaultServiceProvider(
        this IWebHostBuilder hostBuilder, 
        Action<ServiceProviderOptions> configure)
    {
        return hostBuilder.UseDefaultServiceProvider((context, options) => configure(options));
    }
    
    // with action<builder context, servie proivder options>
    public static IWebHostBuilder UseDefaultServiceProvider(
        this IWebHostBuilder hostBuilder, 
        Action<WebHostBuilderContext, ServiceProviderOptions> configure)
    {
        // 如果 host builder 实现了 support use default service provider 接口，
        // 使用 support use default service provider 的 use default service provider 方法
        if (hostBuilder is ISupportsUseDefaultServiceProvider supportsDefaultServiceProvider)
        {
            return supportsDefaultServiceProvider.UseDefaultServiceProvider(configure);
        }
        // （由上，否则），替换 service <IServiceProviderFactory, default service provider factory>
        return hostBuilder.ConfigureServices((context, services) =>                                     	
            {
                var options = new ServiceProviderOptions();
                configure(context, options);                
                services.Replace(ServiceDescriptor.Singleton<IServiceProviderFactory<IServiceCollection>>(
                    new DefaultServiceProviderFactory(options)));
        	});
    }
}

```

###### 2.2.6.1 support use default service provider

```c#
internal interface ISupportsUseDefaultServiceProvider
{
    IWebHostBuilder UseDefaultServiceProvider(
        Action<WebHostBuilderContext, 
        ServiceProviderOptions> configure);
}

```

##### 2.2.7 扩展方法 - use startup

```c#
public static class WebHostBuilderExtensions
{        
    // with func<builder context, tstartup>
    public static IWebHostBuilder UseStartup<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]TStartup>(
        this IWebHostBuilder hostBuilder, 
        Func<WebHostBuilderContext, TStartup> startupFactory) 
        	where TStartup : class
    {
        if (startupFactory == null)
        {
            throw new ArgumentNullException(nameof(startupFactory));
        }
        
        // 解析 startup assembly name
        var startupAssemblyName = 
            startupFactory.GetMethodInfo().DeclaringType!.Assembly.GetName().Name;                
        // 注入 setting [application key, startup assembly name]
        hostBuilder.UseSetting(
            WebHostDefaults.ApplicationKey, 
            startupAssemblyName);
        
        // 如果 builder 实现了 support startup 接口，使用 support startup 的 use startup 方法      
        if (hostBuilder is ISupportsStartup supportsStartup)
        {
            return supportsStartup.UseStartup(startupFactory);
        }
        
        // （由上，否则），注入 service <IStartup, convention based startup>
        return hostBuilder.ConfigureServices((context, services) =>
        	{
                services.AddSingleton(
                    typeof(IStartup), 
                    sp =>
                    	{
                            // 由传入的 startup factory 创建 startup instance
                            var instance = startupFactory(context) ?? throw new InvalidOperationException(
                                "The specified factory returned null startup instance.");
                            
                            // 解析 hosting environment
                            var hostingEnvironment = sp.GetRequiredService<IHostEnvironment>();
                            
                            // Check if the instance implements IStartup before wrapping
                            if (instance is IStartup startup)
                            {
                                return startup;
                            }
                            
                            // 创建 convention based startup
                            return new ConventionBasedStartup(StartupLoader.LoadMethods(
                                sp, 
                                instance.GetType(),
                                hostingEnvironment.EnvironmentName, 
                                instance));
                        });
            });
    }
    
    // with tstart
    public static IWebHostBuilder UseStartup<[DynamicallyAccessedMembers(StartupLinkerOptions.Accessibility)]TStartup>(
        this IWebHostBuilder hostBuilder) 
        	where TStartup : class
    {
        return hostBuilder.UseStartup(typeof(TStartup));
    }        
    
    // type t
    public static IWebHostBuilder UseStartup(
        this IWebHostBuilder hostBuilder, 
        [DynamicallyAccessedMembers(StartupLinkerOptions.Accessibility)] Type startupType)
    {
        if (startupType == null)
        {
            throw new ArgumentNullException(nameof(startupType));
        }
        
        // 解析 startup assembly name
        var startupAssemblyName = startupType.Assembly.GetName().Name;        
        // 注入 setting [application key, startup assembly name]
        hostBuilder.UseSetting(
            WebHostDefaults.ApplicationKey, 
            startupAssemblyName);
        
        // 如果 builder 实现了 support startup 接口，使用 support startup 的 use startup 方法      
        if (hostBuilder is ISupportsStartup supportsStartup)
        {
            return supportsStartup.UseStartup(startupType);
        }
        
        // （由上，否则），注册 service <IStartup, convention based startup>
        return hostBuilder.ConfigureServices(services =>
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
                                // 解析 hosting environment
                                var hostingEnvironment = sp.GetRequiredService<IHostEnvironment>();                                
                                // 创建 convention based startup
                                return new ConventionBasedStartup(StartupLoader.LoadMethods(
                                    sp, 
                                    startupType,
                                    hostingEnvironment.EnvironmentName));
                            });
                }
            });
    }        
}

```

###### 2.2.7.1 support startup

```c#
internal interface ISupportsStartup
{    
    IWebHostBuilder Configure(Action<WebHostBuilderContext, IApplicationBuilder> configure);
       
    IWebHostBuilder UseStartup([DynamicallyAccessedMembers(StartupLinkerOptions.Accessibility)] Type startupType);   
    IWebHostBuilder UseStartup<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]TStartup>(
        Func<WebHostBuilderContext, TStartup> startupFactory);
}

```

#### 2.3 startup loader

```c#
internal class StartupLoader
{
    // a- load method
    public static StartupMethods LoadMethods(
        IServiceProvider hostingServiceProvider, 
        [DynamicallyAccessedMembers(StartupLinkerOptions.Accessibility)] Type startupType, 
        string environmentName, 
        object? instance = null)
    {
        // configure method builder
        var configureMethod = FindConfigureDelegate(startupType, environmentName);
        // configure service builder
        var servicesMethod = FindConfigureServicesDelegate(startupType, environmentName);
        // configure container 
        var configureContainerMethod = FindConfigureContainerDelegate(startupType, environmentName);
        
        // 确保 startup type instance 
        if (instance == null && 
            (!configureMethod.MethodInfo.IsStatic || 
             (servicesMethod?.MethodInfo != null && !servicesMethod.MethodInfo.IsStatic)))
        {
            instance = ActivatorUtilities.GetServiceOrCreateInstance(hostingServiceProvider, startupType);
        }
        
        // The type of the TContainerBuilder. 
        // If there is no ConfigureContainer method we can just use object as it's not going to be used for anything.
        var type = configureContainerMethod.MethodInfo != null 
            ? configureContainerMethod.GetContainerType() 
            : typeof(object);
        
        // 创建 configure service delegate builder（service collection => servcie provider）
        var builder = (ConfigureServicesDelegateBuilder)Activator.CreateInstance(
            typeof(ConfigureServicesDelegateBuilder<>).MakeGenericType(type),
            hostingServiceProvider,
            // configure service builder
            servicesMethod,
            // configure container builder
            configureContainerMethod,
            instance)!;
        
        return new StartupMethods(
            // startup type instance
            instance, 
            // application builder action
            configureMethod.Build(instance), 
            // func<service collection, service provider>
            builder.Build());
    }
    
    // b- find startup type    
    [UnconditionalSuppressMessage(
        "ReflectionAnalysis", 
        "IL2026:RequiresUnreferencedCode", 
        Justification = "We're warning at the entry point. This is an implementation detail.")]
    public static Type FindStartupType(string startupAssemblyName, string environmentName)
    {
        // 如果 startup assembly name 为空，-> 抛出异常
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
        
        // 加载 startup assembly，如果为 null，-> 抛出异常
        var assembly = Assembly.Load(new AssemblyName(startupAssemblyName));
        if (assembly == null)
        {
            throw new InvalidOperationException($"The assembly '{startupAssemblyName}' failed to load.");
        }
        
        var startupNameWithEnv = "Startup" + environmentName;
        var startupNameWithoutEnv = "Startup";
        
        // 先加载 “startup” 名字相关的 type
        var type = assembly.GetType(startupNameWithEnv) ??
            assembly.GetType(startupAssemblyName + "." + startupNameWithEnv) ??
            assembly.GetType(startupNameWithoutEnv) ??
            assembly.GetType(startupAssemblyName + "." + startupNameWithoutEnv);
        
        // （由上），如果没有找到，-> full scan
        if (type == null)
        {            
            var definedTypes = assembly.DefinedTypes.ToList();            
            var startupType1 = definedTypes.Where(info => info.Name.Equals(startupNameWithEnv, StringComparison.OrdinalIgnoreCase));
            var startupType2 = definedTypes.Where(info => info.Name.Equals(startupNameWithoutEnv, StringComparison.OrdinalIgnoreCase));
            
            var typeInfo = startupType1.Concat(startupType2).FirstOrDefault();
            if (typeInfo != null)
            {
                type = typeInfo.AsType();
            }
        }
        
        // 如果 type 为 null（没找到），-> 抛出异常
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

    
    // c- has configure service (return service proivder)
    internal static bool HasConfigureServicesIServiceProviderDelegate(
        [DynamicallyAccessedMembers(StartupLinkerOptions.Accessibility)] Type startupType, 
        string environmentName)
    {
        return null != FindMethod(
            startupType, 
            "Configure{0}Services", 
            environmentName, 
            typeof(IServiceProvider), 
            required: false);
    }
          
    /* find method */
    private static MethodInfo? FindMethod(
        [DynamicallyAccessedMembers(StartupLinkerOptions.Accessibility)] Type startupType, 
        string methodName, 
        string environmentName, 
        Type? returnType = null, 
        bool required = true)
    {
        var methodNameWithEnv = string.Format(
            CultureInfo.InvariantCulture, 
            methodName, 
            environmentName);
        
        var methodNameWithNoEnv = string.Format(
            CultureInfo.InvariantCulture, 
            methodName, 
            "");
        
        // 解析 method with env
        var methods = startupType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        var selectedMethods = methods.Where(method => method.Name.Equals(methodNameWithEnv, StringComparison.OrdinalIgnoreCase))
            					   .ToList();
        if (selectedMethods.Count > 1)
        {
            throw new InvalidOperationException(
                $"Having multiple overloads of method '{methodNameWithEnv}' is not supported.");
        }
        
        // 如果不成功（没找到），-> 解析 method without env
        if (selectedMethods.Count == 0)
        {
            selectedMethods = methods.Where(method => method.Name.Equals(methodNameWithNoEnv, StringComparison.OrdinalIgnoreCase))
		           				   .ToList();
            if (selectedMethods.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Having multiple overloads of method '{methodNameWithNoEnv}' is not supported.");
            }
        }
        
        // 如果不成功（没找到），-> 抛出异常
        var methodInfo = selectedMethods.FirstOrDefault();
        if (methodInfo == null)
        {
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
        
        // 如果 method 返回类型不是 return type，-> 抛出异常
        if (returnType != null && methodInfo.ReturnType != returnType)
        {
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
}

```

##### 2.3.1 configure builder (delegate)

###### 2.3.1.1 configure builder

```c#
internal class ConfigureBuilder
{
    // 注入 method info
    public MethodInfo MethodInfo { get; }        
    public ConfigureBuilder(MethodInfo configure)
    {
        MethodInfo = configure;
    }
    
    // build => application builder action !!!
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

###### 2.3.1.2 find configure delegate

```c#
internal class StartupLoader
{
    internal static ConfigureBuilder FindConfigureDelegate(
        [DynamicallyAccessedMembers(StartupLinkerOptions.Accessibility)] Type startupType, 
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
}

```

##### 2.3.2 configure service builder (delegate)

###### 2.3.2.1 configure service builder

```c#
internal class ConfigureServicesBuilder
{            
    public MethodInfo? MethodInfo { get; }
    // filter
    public Func<Func<IServiceCollection, IServiceProvider?>, Func<IServiceCollection, IServiceProvider?>> 
        StartupServiceFilters { get; set; } = f => f;
    
    public ConfigureServicesBuilder(MethodInfo? configureServices)
    {
        // 注入 method info
        MethodInfo = configureServices;
    }
     
    // build => func<service collection, service provider> !!!
    // （instance 是 container ）
    public Func<IServiceCollection, IServiceProvider?> Build(object instance) => services => Invoke(instance, services);
    
    private IServiceProvider? Invoke(object instance, IServiceCollection services)
    {
        // 执行 filter
        return StartupServiceFilters(Startup)(services);
        // 创建 service provider
        IServiceProvider? Startup(IServiceCollection serviceCollection) => InvokeCore(instance, serviceCollection);
    }
            
    // 执行 method
    private IServiceProvider? InvokeCore(object instance, IServiceCollection services)
    {
        if (MethodInfo == null)
        {
            return null;
        }
        
        // Only support IServiceCollection parameters
        var parameters = MethodInfo.GetParameters();
        if (parameters.Length > 1 ||
            parameters.Any(p => p.ParameterType != typeof(IServiceCollection)))
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
        
        return MethodInfo.InvokeWithoutWrappingExceptions(instance, arguments) as IServiceProvider;
    }
}

```

###### 2.3.2.2 find configure service delegate

```c#
internal class StartupLoader
{
    internal static ConfigureServicesBuilder FindConfigureServicesDelegate(
        [DynamicallyAccessedMembers(StartupLinkerOptions.Accessibility)] Type startupType, 
        string environmentName)
    {
        var servicesMethod = FindMethod(
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
}

```

##### 2.3.3 configure container builder (delegate)

###### 2.3.3.1 configure container builder

```c#
internal class ConfigureContainerBuilder
{    
    public MethodInfo? MethodInfo { get; }
    // filter
    public Func<Action<object>, Action<object>> ConfigureContainerFilters { get; set; } = f => f;
    
    public ConfigureContainerBuilder(MethodInfo? configureContainerMethod)
    {
        // 注入 method info
        MethodInfo = configureContainerMethod;
    }
            
    // build => action of object (service container)
    public Action<object> Build(object instance) => container => Invoke(instance, container);
            
    private void Invoke(object instance, object container)
    {
        // 执行 filter
        ConfigureContainerFilters(StartupConfigureContainer)(container);
        
        void StartupConfigureContainer(object containerBuilder) => InvokeCore(instance, containerBuilder);
    }
    
    // 执行 method
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
            throw new InvalidOperationException($"The {MethodInfo.Name} method must take only one parameter.");
        }
        return parameters[0].ParameterType;
    }
}

```

###### 2.3.3.3 find configure container delegate

```c#
internal class StartupLoader
{
    internal static ConfigureContainerBuilder FindConfigureContainerDelegate(
        [DynamicallyAccessedMembers(StartupLinkerOptions.Accessibility)] Type startupType, 
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
}

```

##### 2.3.4 configure service delegate builder

```c#
internal class StartupLoader
{
    // 抽象基类
    private abstract class ConfigureServicesDelegateBuilder
    {
        public abstract Func<IServiceCollection, IServiceProvider> Build();
    }
    
    // 派生类    
    private class ConfigureServicesDelegateBuilder<TContainerBuilder> : 
    	ConfigureServicesDelegateBuilder where TContainerBuilder : notnull
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
            // 注入 configure service builder (configure*service method)
            ConfigureServicesBuilder = configureServicesBuilder;
            // 注入 configure container builder (configure*container method)
            ConfigureContainerBuilder = configureContainerBuilder;
            Instance = instance;
        }
                       
        // build => func<service collection, service provider>
        public override Func<IServiceCollection, IServiceProvider> Build()
        {
            // 配置 configure service builder，注入 filter a
            ConfigureServicesBuilder.StartupServiceFilters = BuildStartupServicesFilterPipeline;            
            var configureServicesCallback = ConfigureServicesBuilder.Build(Instance);
            
            // 配置 configure container builder，注入 filter b
            ConfigureContainerBuilder.ConfigureContainerFilters = ConfigureContainerPipeline;
            var configureContainerCallback = ConfigureContainerBuilder.Build(Instance);
            
            // 
            return ConfigureServices(configureServicesCallback, configureContainerCallback);
            
            // b- filter of "configure container"
            Action<object> ConfigureContainerPipeline(Action<object> action)
            {
                return Target;
                
                // The ConfigureContainer pipeline needs an Action<TContainerBuilder> as source, so we just adapt the
                // signature with this function.
                void Source(TContainerBuilder containerBuilder) => action(containerBuilder);
                
                // The ConfigureContainerBuilder.ConfigureContainerFilters expects an Action<object> as value, but our pipeline
                // produces an Action<TContainerBuilder> given a source, so we wrap it on an Action<object> that internally casts
                // the object containerBuilder to TContainerBuilder to match the expected signature of our ConfigureContainer pipeline.
                void Target(object containerBuilder) =>
                    // b2
                    BuildStartupConfigureContainerFiltersPipeline(Source)((TContainerBuilder)containerBuilder);
            }
        }
                
        // a- filter of "configure service" -> IStartupConfigureServicesFilter
        // "IStartupConfigureServicesFilter" 过时
        private Func<IServiceCollection, IServiceProvider?> BuildStartupServicesFilterPipeline(
            Func<IServiceCollection, IServiceProvider?> startup)
        {
            return RunPipeline;
            
            IServiceProvider? RunPipeline(IServiceCollection services)
            {          
                // 解析 startup configure service fitler
                var filters = 
                    HostingServiceProvider.GetRequiredService<IEnumerable<IStartupConfigureServicesFilter>>()
                    					.ToArray();

                // 如果没有 filter，-> bypass              
                if (filters.Length == 0)
                {
                    return startup(services);
                }
                
                // 遍历 filter 配置 startup
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
                    // 执行 startup
                    var result = startup(serviceCollection);
                    // 如果有 fitler，不能返回 service provider（scope 不同）
                    if (filters.Length > 0 && result != null)
                    {                        
                        var message = $"A ConfigureServices method that returns an {nameof(IServiceProvider)} is " +
                            $"not compatible with the use of one or more {nameof(IStartupConfigureServicesFilter)}. " +
                            $"Use a void returning ConfigureServices method instead or a ConfigureContainer method.";
                        
                        throw new InvalidOperationException(message);
                    };
                }
            }
        }
        
        // b2- ftiler of "configure container" -> IStartupConfigureContainerFilter    
        // "IStartupConfigureContainerFilter" 过时
        private Action<TContainerBuilder> BuildStartupConfigureContainerFiltersPipeline(Action<TContainerBuilder> configureContainer)
        {
            return RunPipeline;
            
            void RunPipeline(TContainerBuilder containerBuilder)
            {
                // 解析 startup configure container filter
                var filters = 
                    HostingServiceProvider.GetRequiredService<IEnumerable<IStartupConfigureContainerFilter<TContainerBuilder>>>();

                // 反向遍历 fitler 并配置 configure container
                Action<TContainerBuilder> pipeline = InvokeConfigureContainer;
                foreach (var filter in filters.Reverse())
                {
                    pipeline = filter.ConfigureContainer(pipeline);
                }
                
                pipeline(containerBuilder);
                
                void InvokeConfigureContainer(TContainerBuilder builder) => configureContainer(builder);
            }
        }
    }    
              
    // 构建 func<service collection, service provider>
    Func<IServiceCollection, IServiceProvider> ConfigureServices(
        Func<IServiceCollection, IServiceProvider?> configureServicesCallback,
        Action<object> configureContainerCallback)
    {
        return ConfigureServicesWithContainerConfiguration;
        
        IServiceProvider ConfigureServicesWithContainerConfiguration(IServiceCollection services)
        {
            // Call ConfigureServices, if that returned an IServiceProvider, we're done
            var applicationServiceProvider = configureServicesCallback.Invoke(services);
            
            if (applicationServiceProvider != null)
            {
                return applicationServiceProvider;
            }
            
            // （由上，不能由 configure service builder 构建 service provider func）
            
            // 如果有 configure*container 方法，-> 使用 configure container builder 配置
            if (ConfigureContainerBuilder.MethodInfo != null)
            {
                // 从 host services 中解析 service provider factory <t>
                var serviceProviderFactory = HostingServiceProvider.GetRequiredService<IServiceProviderFactory<TContainerBuilder>>();  
                var builder = serviceProviderFactory.CreateBuilder(services);                
                configureContainerCallback(builder);                
                applicationServiceProvider = serviceProviderFactory.CreateServiceProvider(builder);
            }
            // （由上），没有 configure*container 方法，默认构建 service provider
            else
            {
                // 从 host services 中解析 service provider factory <service collection>
                var serviceProviderFactory = HostingServiceProvider.GetRequiredService<IServiceProviderFactory<IServiceCollection>>();
                var builder = serviceProviderFactory.CreateBuilder(services);
                applicationServiceProvider = serviceProviderFactory.CreateServiceProvider(builder);
            }
            
            return applicationServiceProvider ?? services.BuildServiceProvider();
        }
    }                   
}

```

###### 2.3.4.1 startup configure service filter

**过时**

###### 2.3.4.2 startup configure container filter

**过时**

### 3. web host service

#### 3.1 generic web host service 

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
        
        /* 注入 web service，在 generic web host builder 中创建 */
        DiagnosticListener = diagnosticListener;
        HttpContextFactory = httpContextFactory;
        ApplicationBuilderFactory = applicationBuilderFactory;
        StartupFilters = startupFilters;
        Configuration = configuration;
        HostingEnvironment = hostingEnvironment;
    }                        
}

```

##### 3.1.1 generic web host service options

```c#
internal class GenericWebHostServiceOptions
{
    public Action<IApplicationBuilder>? ConfigureApplication { get; set; }
    
    // Always set when options resolved by DI
    public WebHostOptions WebHostOptions { get; set; } = default!;     
    public AggregateException? HostingStartupExceptions { get; set; }
}

```

###### 3.1.1.1 web host options

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

        // 从 configuration 解析，
        ApplicationName = configuration[WebHostDefaults.ApplicationKey] ?? applicationNameFallback;        
        Environment = configuration[WebHostDefaults.EnvironmentKey];        
        ContentRootPath = configuration[WebHostDefaults.ContentRootKey];        
        WebRoot = configuration[WebHostDefaults.WebRootKey];
        
        // 从 configuration 解析 startup assembly，
        // 按 “；” 拆分 startup assembly 和 startup exclue assembly
        StartupAssembly = configuration[WebHostDefaults.StartupAssemblyKey];        
        HostingStartupAssemblies = Split($"{ApplicationName};{configuation[WebHostDefaults.HostingStartupAssembliesKey]}");        
        HostingStartupExcludeAssemblies = Split(configuration[WebHostDefaults.HostingStartupExcludeAssembliesKey]);
        
        // 从 configuration 解析，
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
        
    // 解析 final host startup assembly 集合（出去 excluded assembly）
    public IEnumerable<string> GetFinalHostingStartupAssemblies()
    {
        return HostingStartupAssemblies.Except(
            HostingStartupExcludeAssemblies, 
            StringComparer.OrdinalIgnoreCase);
    }
    
    // 按照 “；” 分隔字符串
    private IReadOnlyList<string> Split(string value)
    {
        return value?.Split(
            ';', 
            StringSplitOptions.TrimEntries | 
            StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
    }
}

```

###### 3.1.1.2 web host default

```c#
public static class WebHostDefaults
{    
    public static readonly string ApplicationKey = "applicationName";        
    public static readonly string StartupAssemblyKey = "startupAssembly";        
    public static readonly string HostingStartupAssembliesKey = "hostingStartupAssemblies";        
    public static readonly string HostingStartupExcludeAssembliesKey = "hostingStartupExcludeAssemblies";        
    public static readonly string DetailedErrorsKey = "detailedErrors";        
    public static readonly string EnvironmentKey = "environment";        
    public static readonly string WebRootKey = "webroot";        
    public static readonly string CaptureStartupErrorsKey = "captureStartupErrors";        
    public static readonly string ServerUrlsKey = "urls";        
    public static readonly string ContentRootKey = "contentRoot";        
    public static readonly string PreferHostingUrlsKey = "preferHostingUrls";        
    public static readonly string PreventHostingStartupKey = "preventHostingStartup";        
    public static readonly string SuppressStatusMessagesKey = "suppressStatusMessages";        
    public static readonly string ShutdownTimeoutKey = "shutdownTimeoutSeconds";        
    public static readonly string StaticWebAssetsKey = "staticWebAssets";
}

```

###### 3.1.1.3 web host utilities

```c#
internal class WebHostUtilities
{
    public static bool ParseBool(IConfiguration configuration, string key)
    {
        return string.Equals("true", configuration[key], StringComparison.OrdinalIgnoreCase) || 
               string.Equals("1", configuration[key], StringComparison.OrdinalIgnoreCase);
    }
}

```

##### 3.1.2 接口方法 

###### 3.1.2.1 start

```c#
internal class GenericWebHostService : IHostedService    
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        HostingEventSource.Log.HostStart();
        
        /* 解析 url address */        
        var serverAddressesFeature = Server.Features.Get<IServerAddressesFeature>();
        var addresses = serverAddressesFeature?.Addresses;
        if (addresses != null && 
            !addresses.IsReadOnly && 
            addresses.Count == 0)
        {
            var urls = Configuration[WebHostDefaults.ServerUrlsKey];
            if (!string.IsNullOrEmpty(urls))
            {
                serverAddressesFeature!.PreferHostingUrls = WebHostUtilities.ParseBool(
                    Configuration, 
                    WebHostDefaults.PreferHostingUrlsKey);
                
                foreach (var value in urls.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    addresses.Add(value);
                }
            }
        }
        
        /* 创建请求管道（request delegate） */
        RequestDelegate? application = null;
        
        try
        {
            // 从 generic web host options 解析 application builder action           
            var configure = Options.ConfigureApplication;       
            
            if (configure == null)
            {
                throw new InvalidOperationException(
                    $"No application configured. 
                     "Please specify an application via IWebHostBuilder.UseStartup, IWebHostBuilder.Configure, 
                     "or specifying the startup assembly via {nameof(WebHostDefaults.StartupAssemblyKey)} 
                     "in the web host configuration.");
            }
            
            // 创建 application builder
            var builder = ApplicationBuilderFactory.CreateBuilder(Server.Features);
            
            // 反向遍历 startup filter，用 filter 配置 configure（application builder action）
            foreach (var filter in StartupFilters.Reverse())
            {
                configure = filter.Configure(configure);
            }
            
            // 用 configure（application builder action）配置 application builder
            configure(builder);
            
            // 由 application builder 构建 application（request delegate）            
            application = builder.Build();
        }
        catch (Exception ex)
        {
            Logger.ApplicationError(ex);
            
            if (!Options.WebHostOptions.CaptureStartupErrors)
            {
                throw;
            }
            
            var showDetailedErrors = HostingEnvironment.IsDevelopment() || 
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

###### 2.1.2.2 stop

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

##### 3.1.3 startup filter

```c#
public interface IStartupFilter
{        
    Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next);
}

```

#### 3.2 generic web host builder

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
        // 注入 host builder
        _builder = builder;
        
        // 配置 host builder 的 host configuration    
         
        // 配置 host builder 的 app configuration                         
         
        // 配置（注入） host builder 的 configure services
    }    
}

```

##### 3.3.1 配置 host configuration

```c#
internal class GenericWebHostBuilder 
{
    public GenericWebHostBuilder(
        IHostBuilder builder, 
        WebHostBuilderOptions options)
    {
        // 创建 configuration builder
        var configBuilder = new ConfigurationBuilder().AddInMemoryCollection();       
        
        // 如果 web host builder options 没有标记 suppress environment configuration，
        // -> 加载环境变量 “ASPNETCORE_” 开头
        if (!options.SuppressEnvironmentConfiguration)
        {
            configBuilder.AddEnvironmentVariables(prefix: "ASPNETCORE_");
        }
        
        // 构建 configuration
        _config = configBuilder.Build();        
        
        // 向 host builder 注入 host configuration action
        _builder.ConfigureHostConfiguration(config =>
       	{
            // 注入构建的 configuration
            config.AddConfiguration(_config);            
            
            // We do this super early but still late enough that we can process the configuration
            // wired up by calls to UseSetting
            ExecuteHostingStartups();
        });
    }        
}

```

##### 3.3.2 执行 hosting startup

```c#
internal class GenericWebHostBuilder 
{
    private void ExecuteHostingStartups()
    {
        // 创建 web host options（注入构建的 config）
        var webHostOptions = new WebHostOptions(
            _config, 
            Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty);

        // 如果 web host options 设置 suppress hosting startup，-> 返回
        if (webHostOptions.PreventHostingStartup)
        {
            return;
        }
        
        // （由上），web host options 没有标记 suppress hosting startup
                        
        var exceptions = new List<Exception>();     
        
        // 创建 hosting startup web host builder
        _hostingStartupWebHostBuilder = new HostingStartupWebHostBuilder(this);
                
        // 遍历 web host options 中的 hosting startup assembly 集合，        
        foreach (var assemblyName in webHostOptions.GetFinalHostingStartupAssemblies()
                 								.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                // 加载 hosting startup assembly
                var assembly = Assembly.Load(new AssemblyName(assemblyName));
                
                // 遍历 assembly 的 hosting startup attribute
                foreach (var attribute in assembly.GetCustomAttributes<HostingStartupAttribute>())
                {
                    // 创建 attribute 的 hosting startup type 实例（IHostingStartup 的实现）
                    var hostingStartup = (IHostingStartup)Activator.CreateInstance(attribute.HostingStartupType)!;
                    
                    // 使用 hosting startup 的 configure 方法配置 hosting startup web host builder
                    hostingStartup.Configure(_hostingStartupWebHostBuilder);
                }
            }
            catch (Exception ex)
            {
                // Capture any errors that happen during startup
                exceptions.Add(new InvalidOperationException(
                    $"Startup assembly {assemblyName} failed to execute. See the inner exception for more details.", 
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

###### 3.3.2.1 hosting startup attribute

```c#
[AttributeUsage(
    AttributeTargets.Assembly, 
    Inherited = false, 
    AllowMultiple = true)]
public sealed class HostingStartupAttribute : Attribute
{    
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type HostingStartupType { get; }
    
    public HostingStartupAttribute(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type hostingStartupType)
    {
        if (hostingStartupType == null)
        {
            throw new ArgumentNullException(nameof(hostingStartupType));
        }
        
        if (!typeof(IHostingStartup).IsAssignableFrom(hostingStartupType))
        {
            throw new ArgumentException(
                $@"""{hostingStartupType}"" does not implement {typeof(IHostingStartup)}.", 
                nameof(hostingStartupType));
        }
        
        HostingStartupType = hostingStartupType;        
    }        
}

```

###### 3.3.2.2 hosting startup 接口

```c#
public interface IHostingStartup
{    
    void Configure(IWebHostBuilder builder);
}

```

##### 3.3.3 配置 application configuration

```c#
internal class GenericWebHostBuilder
{
    public GenericWebHostBuilder(
        IHostBuilder builder, 
        WebHostBuilderOptions options)
    {
        _builder.ConfigureAppConfiguration((context, configurationBuilder) =>
        	{
                // 如果不是 hosting startup web host builder
                if (_hostingStartupWebHostBuilder != null)
                {
                    // 解析 web host builder context
                    var webhostContext = GetWebHostBuilderContext(context);
                    // 将（host builer）的 configuration builder 注入 hosting startup web host builder
                    _hostingStartupWebHostBuilder.ConfigureAppConfiguration(
                        webhostContext, 
                        configurationBuilder);
                }
            });                
    }
    
    private WebHostBuilderContext GetWebHostBuilderContext(HostBuilderContext context)
    {
        // 解析或创建 host builder context 中解析，        
        if (!context.Properties.TryGetValue(
            	typeof(WebHostBuilderContext), 
            	out var contextVal))
        {
            // 创建 web host options
            var options = new WebHostOptions(
                context.Configuration, 
                Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty);
            
            // 创建 web host builder context
            var webHostBuilderContext = new WebHostBuilderContext
            {
                Configuration = context.Configuration,
                HostingEnvironment = new HostingEnvironment(),
            };
            
            // web host builer context 初始化
            webHostBuilderContext.HostingEnvironment.Initialize(
                context.HostingEnvironment.ContentRootPath, 
                options);
            
            // 将 web host builder context 注入 host builder context
            context.Properties[typeof(WebHostBuilderContext)] = webHostBuilderContext;
            context.Properties[typeof(WebHostOptions)] = options;
            
            return webHostBuilderContext;
        }
               
        // 创建 web host builder context 并注入 host builder context
        var webHostContext = (WebHostBuilderContext)contextVal;
        webHostContext.Configuration = context.Configuration;
        
        return webHostContext;
    }
}

```

##### 3.3.4 注入 host service

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
            var webHostOptions = (WebHostOptions)context.Properties[typeof(WebHostOptions)];
            
            // 注入 hosting environment
            services.AddSingleton(webhostContext.HostingEnvironment);
            
            // 注入 generic web host application lifetime
            services.AddSingleton<IApplicationLifetime, GenericWebHostApplicationLifetime>();

            // 注入 generic web host service options
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
            var listener = new DiagnosticListener("Microsoft.AspNetCore");
            services.TryAddSingleton<DiagnosticListener>(listener);
            services.TryAddSingleton<DiagnosticSource>(listener);            
            services.TryAddSingleton<IHttpContextFactory, DefaultHttpContextFactory>();
            services.TryAddScoped<IMiddlewareFactory, MiddlewareFactory>();
            services.TryAddSingleton<IApplicationBuilderFactory, ApplicationBuilderFactory>();
                                    
            /* 加载 startup type 的配置 */            
            if (!string.IsNullOrEmpty(webHostOptions.StartupAssembly))
            {
                try
                {
                    // 解析 startup type
                    var startupType = StartupLoader.FindStartupType(
                        webHostOptions.StartupAssembly,
                        webhostContext.HostingEnvironment.EnvironmentName);
                    
                    // support startup 接口的方法
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

##### 3.3.5 接口方法 - web host builder 接口

```c#
internal class GenericWebHostBuilder
{
    // 不能直接构建 web host，由 hosted service 执行 web host 功能
    public IWebHost Build()
    {
        throw new NotSupportedException(
            $"Building this implementation of {nameof(IWebHostBuilder)} is not supported.");
    }
    
    // configure app configuration
    public IWebHostBuilder ConfigureAppConfiguration(Action<WebHostBuilderContext, IConfigurationBuilder> configureDelegate)
    {
        _builder.ConfigureAppConfiguration((context, builder) =>
    	{
            var webhostBuilderContext = GetWebHostBuilderContext(context);
            configureDelegate(webhostBuilderContext, builder);
        });
        
        return this;
    }
    
    // configure service
    public IWebHostBuilder ConfigureServices(
        Action<IServiceCollection> configureServices)
    {
        return ConfigureServices((context, services) => configureServices(services));
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
       
    // get setting
    public string GetSetting(string key)
    {
        return _config[key];
    }
    // use setting
    public IWebHostBuilder UseSetting(string key, string? value)
    {
        _config[key] = value;
        return this;
    }
}

```

##### 3.3.6 接口方法 - support startup 接口

```c#
internal class GenericWebHostBuilder
{
    // configure application builder 
    public IWebHostBuilder Configure(Action<WebHostBuilderContext, IApplicationBuilder> configure)
    {
        // Clear the startup type
        _startupObject = configure;
        
        _builder.ConfigureServices((context, services) =>
            {
                if (object.ReferenceEquals(_startupObject, configure))
                {
                    services.Configure<GenericWebHostServiceOptions> options =>
                    	{
                         	var webhostBuilderContext = GetWebHostBuilderContext(context);
                        	options.ConfigureApplication = app => configure(webhostBuilderContext, app);
                    	});
                }
            });
        
        return this;
    }
    
    // use startup t
    public IWebHostBuilder UseStartup([DynamicallyAccessedMembers(StartupLinkerOptions.Accessibility)] Type startupType)
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
    
    // use startup T with start factory func
    public IWebHostBuilder UseStartup<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]TStartup>(
        Func<WebHostBuilderContext, TStartup> startupFactory)
    {
        // Clear the startup type
        _startupObject = startupFactory;
        
        _builder.ConfigureServices((context, services) =>
            {
                // UseStartup can be called multiple times. Only run the last one.
                if (object.ReferenceEquals(_startupObject, startupFactory))
                {
                    // 解析 startup type instance
                    var webHostBuilderContext = GetWebHostBuilderContext(context);
                    var instance = startupFactory(webHostBuilderContext) ?? throw new InvalidOperationException(
                        "The specified factory returned null startup instance.");
                    
                    UseStartup(instance.GetType(), context, services, instance);
                }
            });
        
        return this;
    }
    
    // the real method to use startup 
    [UnconditionalSuppressMessage(
        "ReflectionAnalysis", 
        "IL2006:UnrecognizedReflectionPattern", 
        Justification = "We need to call a generic method on IHostBuilder.")]
    private void UseStartup(
        [DynamicallyAccessedMembers(StartupLinkerOptions.Accessibility)] Type startupType, 
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
            // 如果 startup type 实现了 IStartup 接口，-> 抛出异常            
            if (typeof(IStartup).IsAssignableFrom(startupType))
            {
                throw new NotSupportedException($"{typeof(IStartup)} isn't supported");
            }
            
            // 如果 startup type 中的 configure services 方法返回 service provider，-> 抛出异常               
            if (StartupLoader.HasConfigureServicesIServiceProviderDelegate(
                	startupType, 
	                context.HostingEnvironment.EnvironmentName))
            {
                throw new NotSupportedException($"ConfigureServices returning an {typeof(IServiceProvider)} isn't supported.");
            }
            
            /* configure services */
            
            // 创建 host service provider，注入 host builder context 中
            instance ??= ActivatorUtilities.CreateInstance(
                new HostServiceProvider(webHostBuilderContext), 
                startupType);
            
            context.Properties[_startupKey] = instance;
            
            // 解析 startup type 中的 configure service builder，            
            var configureServicesBuilder = StartupLoader.FindConfigureServicesDelegate(
                startupType, 
                context.HostingEnvironment.EnvironmentName);
            // 并由 func 创建 configure services            
            var configureServices = configureServicesBuilder.Build(instance);
            // 调用 configure services -> services provider
            configureServices(services);
            
            /* configure container */
            
            // 解析 startup type 中的 configure container builder                   
            var configureContainerBuilder = StartupLoader.FindConfigureContainerDelegate(
                startupType, 
                context.HostingEnvironment.EnvironmentName);
            
            if (configureContainerBuilder.MethodInfo != null)
            {
                /* 缓存 container type */
                var containerType = configureContainerBuilder.GetContainerType();
                // Store the builder in the property bag
                _builder.Properties[typeof(ConfigureContainerBuilder)] = configureContainerBuilder;
                
                var actionType = typeof(Action<,>).MakeGenericType(
                    typeof(HostBuilderContext), 
                    containerType);
                
                // 解析 generic web host builder 的 private "configureContainerImpl" 作为 callback
                // Get the private ConfigureContainer method on this type then close over the container type
                var configureCallback = typeof(GenericWebHostBuilder).GetMethod(
											                    	nameof(ConfigureContainerImpl), 
											                    	BindingFlags.NonPublic | BindingFlags.Instance)!                 
                    											 .MakeGenericMethod(containerType)                                   
											                     .CreateDelegate(actionType, this);

                // 调用 configure 方法（如果没有，使用 callback）
                typeof(IHostBuilder).GetMethod(nameof(IHostBuilder.ConfigureContainer))!
                    			   .MakeGenericMethod(containerType)
                    			   .InvokeWithoutWrappingExceptions(
                    					_builder, 
                    					new object[] { configureCallback });
            }
                        
            // 解析 startup type 中的 configure builder
            // Resolve Configure after calling ConfigureServices and ConfigureContainer
            configureBuilder = StartupLoader.FindConfigureDelegate(
                startupType, 
                context.HostingEnvironment.EnvironmentName);
        }
        catch (Exception ex) when (webHostOptions.CaptureStartupErrors)
        {
            startupError = ExceptionDispatchInfo.Capture(ex);
        }
               
        // 注入 generic web host service options action        
        services.Configure<GenericWebHostServiceOptions>(options =>
        {
            options.ConfigureApplication = app =>
            {
                // Throw if there was any errors initializing startup
                startupError?.Throw();
                
                // Execute Startup.Configure
                if (instance != null && configureBuilder != null)
                {
                    // 由 configure builder 构建 configure 并执行
                    configureBuilder.Build(instance)(app);
                }
            };
        });
    }                
}

```

###### 3.3.6.1  configure container impl

```c#
internal class GenericWebHostBuilder
{
    private void ConfigureContainerImpl<TContainer>(
            HostBuilderContext context, 
            TContainer container) 
            	where TContainer : notnull
    {
        // 从 host builder context 解析 configure container func（fallback）
        var instance = context.Properties[_startupKey];
        var builder = (ConfigureContainerBuilder)context.Properties[typeof(ConfigureContainerBuilder)];
        builder.Build(instance)(container);
    }
}

```

###### 3.3.6.2 host service provider

```c#
internal class GenericWebHostBuilder
{
    // This exists just so that we can use ActivatorUtilities.CreateInstance on the Startup class
    private class HostServiceProvider : IServiceProvider
    {
        private readonly WebHostBuilderContext _context;
        
        public HostServiceProvider(WebHostBuilderContext context)
        {
            _context = context;
        }
        
        public object? GetService(Type serviceType)
        {
            
            if(serviceType == typeof(Microsoft.Extensions.Hosting.IHostingEnvironment) || 
               serviceType == typeof(Microsoft.AspNetCore.Hosting.IHostingEnvironment) ||               
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

##### 3.3.7 接口方法 - support default service provider 接口

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

#### 3.4 hosting startup web host builder

```c#
internal class HostingStartupWebHostBuilder : 
	IWebHostBuilder, 
	ISupportsStartup, 
	ISupportsUseDefaultServiceProvider
{                            
    private readonly GenericWebHostBuilder _builder;
    public HostingStartupWebHostBuilder(GenericWebHostBuilder builder)
    {
        _builder = builder;
    }
            
    // configure service 
    private Action<WebHostBuilderContext, IServiceCollection>? _configureServices;    
        
    public void ConfigureServices(
        WebHostBuilderContext context, 
        IServiceCollection services)
    {
        _configureServices?.Invoke(context, services);
    }
    
    // configure configuration
    private Action<WebHostBuilderContext, IConfigurationBuilder>? _configureConfiguration;
        
    public void ConfigureAppConfiguration(
        WebHostBuilderContext context, 
        IConfigurationBuilder builder)
    {
        _configureConfiguration?.Invoke(context, builder);
    }                
}

```

##### 3.4.1 接口方法 - web host builder 接口

```c#
internal class HostingStartupWebHostBuilder
{
    // 不能直接创建 web host，由 hosted service 实现功能
    public IWebHost Build()
    {
        throw new NotSupportedException($"Building this implementation of {nameof(IWebHostBuilder)} is not supported.");
    }
    
    // configure applicatino configuration
    public IWebHostBuilder ConfigureAppConfiguration(
        Action<WebHostBuilderContext, IConfigurationBuilder> configureDelegate)
    {
        _configureConfiguration += configureDelegate;
        return this;
    }
    
    // configure service
    public IWebHostBuilder ConfigureServices(
        Action<IServiceCollection> configureServices)
    {
        return ConfigureServices((context, services) => configureServices(services));
    }
    
    public IWebHostBuilder ConfigureServices(
        Action<WebHostBuilderContext, IServiceCollection> configureServices)
    {
        _configureServices += configureServices;
        return this;
    }
    
    // get setting    
    public string GetSetting(string key) => _builder.GetSetting(key);
    // use setting
    public IWebHostBuilder UseSetting(string key, string? value)
    {
        _builder.UseSetting(key, value);
        return this;
    }
}

```

##### 3.4.2 接口方法 - support startup 接口

```c#
internal class HostingStartupWebHostBuilder
{
    public IWebHostBuilder Configure(Action<WebHostBuilderContext, IApplicationBuilder> configure)
    {
        return _builder.Configure(configure);
    }
    
    public IWebHostBuilder UseStartup([DynamicallyAccessedMembers(StartupLinkerOptions.Accessibility)] Type startupType)
    {
        return _builder.UseStartup(startupType);
    }
    
    public IWebHostBuilder UseStartup<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]TStartup>(
        Func<WebHostBuilderContext, TStartup> startupFactory)
    {
        return _builder.UseStartup(startupFactory);
    }
}

```

##### 3.4.3 接口方法 - support default service provider 接口

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

#### 3.5 注册 generic web host service

##### 3.5.1 configure web host in "generic host"

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
        builder.ConfigureServices((context, services) => services.AddHostedService<GenericWebHostService>());
        
        return builder;
    }
}

```

##### 3.5.2 configure web host default in "generic host"

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
                
        return builder.ConfigureWebHost(webHostBuilder =>
        	{
                // 使用 WebHost 的静态方法
                WebHost.ConfigureWebDefaults(webHostBuilder);                
                configure(webHostBuilder);
            });
    }
}

```

##### 3.5.3 configure web host default in "web host"

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
            /* 注入 hosting filter */ 
            .ConfigureServices((hostingContext, services) =>
            {
                // 后配置 host filtering options
                services.PostConfigure<HostFilteringOptions>(options =>
                {
                    if (options.AllowedHosts == null || 
                        options.AllowedHosts.Count == 0)
                    {
                        // "AllowedHosts": "localhost;127.0.0.1;[::1]"
                        var hosts = hostingContext.Configuration["AllowedHosts"]?.Split(
                            new[] { ';' }, 
                            StringSplitOptions.RemoveEmptyEntries);
                        
                        // Fall back to "*" to disable.
                        options.AllowedHosts = (hosts?.Length > 0 ? hosts : new[] { "*" });
                    }
                });
                
                // 注入 host filtering options change token
                services.AddSingleton<IOptionsChangeTokenSource<HostFilteringOptions>>(    
                    new ConfigurationChangeTokenSource<HostFilteringOptions>(hostingContext.Configuration));
                
                // 注入 host filtering startup filter
                services.AddTransient<IStartupFilter, HostFilteringStartupFilter>();
				
                // 注入 forward service
                if (string.Equals(
                    	"true", 
                    	hostingContext.Configuration["ForwardedHeaders_Enabled"], 
                    	StringComparison.OrdinalIgnoreCase))
                {
                    // 注入 forwarded header options
                    services.Configure<ForwardedHeadersOptions>(options =>
                        {
                            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | 
                                					 ForwardedHeaders.XForwardedProto;
                            // Only loopback proxies are allowed by default. 
                            // Clear that restriction because forwarders are being enabled by explicit configuration.
                            options.KnownNetworks.Clear();
                            options.KnownProxies.Clear();
                        });
                    // 注入 forwarded header startup filter
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

###### 3.5.3.1 host filtering options?

```c#

```

###### 3.5.3.2 host filtering startup filter

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

###### 3.5.3.3 forwarded header options?

```c#

```

###### 3.5.3.4 forwarded header startup filter

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









