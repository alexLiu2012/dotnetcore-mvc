## about mvc middleware



### 1. about



### 2. details

#### 2.1 mvc builder

##### 2.1.1 接口

```c#
public interface IMvcBuilder
{
    IServiceCollection Services { get; }     
    ApplicationPartManager PartManager { get; }
}

```

##### 2.1.2 mvc builder

```c#
internal class MvcBuilder : IMvcBuilder
{
    public IServiceCollection Services { get; }        
    public ApplicationPartManager PartManager { get; }
    
    public MvcBuilder(
        IServiceCollection services, 
        ApplicationPartManager manager)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }        
        if (manager == null)
        {
            throw new ArgumentNullException(nameof(manager));
        }
        
        Services = services;
        PartManager = manager;
    }    
}

```

##### 2.1.3 扩展方法

###### 2.1.3.1 add mvc options

```c#
public static class MvcCoreMvcBuilderExtensions
{    
    public static IMvcBuilder AddMvcOptions(
        this IMvcBuilder builder,
        Action<MvcOptions> setupAction)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (setupAction == null)
        {
            throw new ArgumentNullException(nameof(setupAction));
        }
        
        builder.Services.Configure(setupAction);        
        return builder;
    }
}

```

###### 2.1.3.2 add json options

```c#
public static class MvcCoreMvcBuilderExtensions
{
    public static IMvcBuilder AddJsonOptions(
        this IMvcBuilder builder,
        Action<JsonOptions> configure)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }
        
        builder.Services.Configure(configure);        
        return builder;
    }
}

```

###### 2.1.3.3 add formatter mapping

```c#
public static class MvcCoreMvcBuilderExtensions
{
     public static IMvcBuilder AddFormatterMappings(
        this IMvcBuilder builder,
        Action<FormatterMappings> setupAction)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (setupAction == null)
        {
            throw new ArgumentNullException(nameof(setupAction));
        }
        
        builder.Services.Configure<MvcOptions>((options) => 
                                               		setupAction(options.FormatterMappings));
        
        return builder;
    }
}

```

###### 2.1.3.4 add & configure application part

```c#
public static class MvcCoreMvcBuilderExtensions
{
    // add applicaiton part
    public static IMvcBuilder AddApplicationPart(
        this IMvcBuilder builder, 
        Assembly assembly)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (assembly == null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }
        
        builder.ConfigureApplicationPartManager(manager =>
                                                {
                                                    var partFactory = ApplicationPartFactory.GetApplicationPartFactory(assembly);
                                                    foreach (var applicationPart in partFactory.GetApplicationParts(assembly))
                                                    {
                                                        manager.ApplicationParts.Add(applicationPart);
                                                    }
                                                });
        
        return builder;
    }
    
    // configure application part manager
    public static IMvcBuilder ConfigureApplicationPartManager(
        this IMvcBuilder builder,
        Action<ApplicationPartManager> setupAction)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (setupAction == null)
        {
            throw new ArgumentNullException(nameof(setupAction));
        }
        
        setupAction(builder.PartManager);        
        return builder;
    }
}

```

###### 2.1.3.5 add controller as service

```c#
public static class MvcCoreMvcBuilderExtensions
{
    public static IMvcBuilder AddControllersAsServices(this IMvcBuilder builder)        
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        var feature = new ControllerFeature();
        builder.PartManager.PopulateFeature(feature);
        
        foreach (var controller in feature.Controllers
                 						.Select(c => c.AsType()))
        {
            builder.Services.TryAddTransient(controller, controller);
        }
        
        builder.Services.Replace(
            ServiceDescriptor.Transient<IControllerActivator, ServiceBasedControllerActivator>());
        
        return builder;
    }
}

```

###### 2.1.3.6 configure api behavior options

```c#
public static class MvcCoreMvcBuilderExtensions
{                                                                               
    public static IMvcBuilder ConfigureApiBehaviorOptions(
        this IMvcBuilder builder,
        Action<ApiBehaviorOptions> setupAction)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (setupAction == null)
        {
            throw new ArgumentNullException(nameof(setupAction));
        }
        
        builder.Services.Configure(setupAction);        
        return builder;
    }
}

```

#### 2.2 mvc core builder

##### 2.2.1 接口

```c#
public interface IMvcCoreBuilder
{
    IServiceCollection Services { get; }     
    ApplicationPartManager PartManager { get; }
}

```

##### 2.2.2 mvc core builder

```c#
internal class MvcCoreBuilder : IMvcCoreBuilder
{
    public ApplicationPartManager PartManager { get; }        
    public IServiceCollection Services { get; }
    
    public MvcCoreBuilder(
        IServiceCollection services,
        ApplicationPartManager manager)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }        
        if (manager == null)
        {
            throw new ArgumentNullException(nameof(manager));
        }
        
        Services = services;
        PartManager = manager;
    }    
}

```

##### 2.2.3 扩展方法

###### 2.2.3.1 add mvc options

```c#
public static class MvcCoreMvcCoreBuilderExtensions
{    
    public static IMvcCoreBuilder AddMvcOptions(
        this IMvcCoreBuilder builder,
        Action<MvcOptions> setupAction)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (setupAction == null)
        {
            throw new ArgumentNullException(nameof(setupAction));
        }
        
        builder.Services.Configure(setupAction);        
        return builder;
    }
}

```

###### 2.2.3.2 add json options

```c#
public static class MvcCoreMvcCoreBuilderExtensions
{    
    public static IMvcCoreBuilder AddJsonOptions(
        this IMvcCoreBuilder builder,
        Action<JsonOptions> configure)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }
        
        builder.Services.Configure(configure);        
        return builder;
    }
}

```

###### 2.2.3.3 add formatter mappings

```c#
public static class MvcCoreMvcCoreBuilderExtensions
{    
    // add formatter mapping
    public static IMvcCoreBuilder AddFormatterMappings(
        this IMvcCoreBuilder builder,
        Action<FormatterMappings> setupAction)
    {
        AddFormatterMappingsServices(builder.Services);
        
        if (setupAction != null)
        {
            builder.Services.Configure<MvcOptions>((options) => 
                                                   setupAction(options.FormatterMappings));
        }
        
        return builder;
    }
    
    // add format filter service
    public static IMvcCoreBuilder AddFormatterMappings(this IMvcCoreBuilder builder)
    {
        AddFormatterMappingsServices(builder.Services);
        return builder;
    }               
    
    internal static void AddFormatterMappingsServices(IServiceCollection services)
    {
        services.TryAddSingleton<FormatFilter, FormatFilter>();
    }
}

```

###### 2.2.3.4 add & configure application part

```c#
public static class MvcCoreMvcCoreBuilderExtensions
{  
    // add application part
    public static IMvcCoreBuilder AddApplicationPart(
        this IMvcCoreBuilder builder, 
        Assembly assembly)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (assembly == null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }
        
        builder.ConfigureApplicationPartManager(manager =>
                                                {
                                                    var partFactory = ApplicationPartFactory.GetApplicationPartFactory(assembly);
                                                    foreach (var applicationPart in partFactory.GetApplicationParts(assembly))
                                                    {
                                                        manager.ApplicationParts.Add(applicationPart);
                                                    }
                                                });
        
        return builder;
    }
    
    // configure application part manager
    public static IMvcCoreBuilder ConfigureApplicationPartManager(
        this IMvcCoreBuilder builder,
        Action<ApplicationPartManager> setupAction)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (setupAction == null)
        {
            throw new ArgumentNullException(nameof(setupAction));
        }
        
        setupAction(builder.PartManager);        
        return builder;
    }
}

```

###### 2.2.3.5 add controller as service

```c#
public static class MvcCoreMvcCoreBuilderExtensions
{  
    public static IMvcCoreBuilder AddControllersAsServices(this IMvcCoreBuilder builder)
    {
        var feature = new ControllerFeature();
        builder.PartManager.PopulateFeature(feature);
        
        foreach (var controller in feature.Controllers.Select(c => c.AsType()))
        {
            builder.Services.TryAddTransient(controller, controller);
        }
        
        builder.Services.Replace(
            ServiceDescriptor.Transient<IControllerActivator, ServiceBasedControllerActivator>());
        
        return builder;
    }
}

```

###### 2.2.3.6 configure api behavior options

```c#
public static class MvcCoreMvcCoreBuilderExtensions
{  
    public static IMvcCoreBuilder ConfigureApiBehaviorOptions(
        this IMvcCoreBuilder builder,
        Action<ApiBehaviorOptions> setupAction)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (setupAction == null)
        {
            throw new ArgumentNullException(nameof(setupAction));
        }
        
        builder.Services.Configure(setupAction);        
        return builder;
    }    
}

```

###### 2.2.3.7 authorization

```c#
public static class MvcCoreMvcCoreBuilderExtensions
{              
    // add authorization (options)
    public static IMvcCoreBuilder AddAuthorization(
        this IMvcCoreBuilder builder,
        Action<AuthorizationOptions> setupAction)
    {
        AddAuthorizationServices(builder.Services);
        
        if (setupAction != null)
        {
            builder.Services.Configure(setupAction);
        }
        
        return builder;
    }
    
    // add authorization service
    public static IMvcCoreBuilder AddAuthorization(this IMvcCoreBuilder builder)
    {
        AddAuthorizationServices(builder.Services);
        return builder;
    }                
       
    internal static void AddAuthorizationServices(IServiceCollection services)
    {
        services.AddAuthenticationCore();
        services.AddAuthorization();
        
        services.TryAddEnumerable(
            ServiceDescriptor.Transient<IApplicationModelProvider, 
            AuthorizationApplicationModelProvider>());
    }                                   
}

```

#### 2.3 add mvc core

```c#
public static class MvcCoreServiceCollectionExtensions
{    
    // The "MvcCoreServiceCollectionExtensions.AddMvcCore(IServiceCollection)" approach for configuring MVC is provided 
    // for experienced MVC developers who wish to have full control over the set of default services registered. 
    //
    // "MvcCoreServiceCollectionExtensions.AddMvcCore(IServiceCollection)" will register the minimum set of services necessary 
    // to route requests and invoke controllers. It is not expected that any application will satisfy its requirements 
    // with just a call to "MvcCoreServiceCollectionExtensions.AddMvcCore(IServiceCollection)". 
    // Additional configuration using the "IMvcCoreBuilder" will be required.
    
    // 注册 mvc core service 并配置 mvc options
    public static IMvcCoreBuilder AddMvcCore(
        this IServiceCollection services,
        Action<MvcOptions> setupAction)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }        
        if (setupAction == null)
        {
            throw new ArgumentNullException(nameof(setupAction));
        }
        
        // 注入 mvc core 服务
        var builder = services.AddMvcCore();
        // 配置 mvc options
        services.Configure(setupAction);
        
        return builder;
    }
    
    // 注册 mvc core service
    public static IMvcCoreBuilder AddMvcCore(this IServiceCollection services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        // 解析 application part manager、注入 di
        var partManager = GetApplicationPartManager(services);
        services.TryAddSingleton(partManager);
        
        // configure controller feature provider
        ConfigureDefaultFeatureProviders(partManager);
        // configure service provider？
        ConfigureDefaultServices(services);
        // 注册 mvc core service
        AddMvcCoreServices(services);
        
        // 由 service provider、application part manager 创建 mvc core builder 并返回
        var builder = new MvcCoreBuilder(services, partManager);        
        return builder;
    }
        
    /* get application part manager */
    private static ApplicationPartManager GetApplicationPartManager(IServiceCollection services)
    {
        // 从 di 解析 application part manager
        var manager = GetServiceFromCollection<ApplicationPartManager>(services);
        // 如果 application part manager，
        if (manager == null)
        {
            // 创建 application part manager
            manager = new ApplicationPartManager();
            
            // 从 di 解析 web host environment
            var environment = GetServiceFromCollection<IWebHostEnvironment>(services);
            // 解析 assembly name
            var entryAssemblyName = environment?.ApplicationName;
            // 如果 assembly name 为空，返回 application part manager，
            if (string.IsNullOrEmpty(entryAssemblyName))
            {
                return manager;
            }
            // 否则，将 assembly name 注入 application part manager
            manager.PopulateDefaultParts(entryAssemblyName);
        }
        
        return manager;
    }
    
    private static T? GetServiceFromCollection<T>(IServiceCollection services)
    {
        return (T?)services.LastOrDefault(d => d.ServiceType == typeof(T))?
            			  .ImplementationInstance;
    }
    
    /* 向 application part manager 注入 controller feature provider */
    private static void ConfigureDefaultFeatureProviders(ApplicationPartManager manager)
    {
        if (!manager.FeatureProviders
            	    .OfType<ControllerFeatureProvider>()
            	    .Any())
        {
            manager.FeatureProviders.Add(new ControllerFeatureProvider());
        }
    }
    
    // add mvc core services                
    internal static void AddMvcCoreServices(IServiceCollection services)
    {
        //
        // Options
        //
        services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<MvcOptions>, MvcCoreMvcOptionsSetup>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IPostConfigureOptions<MvcOptions>, MvcCoreMvcOptionsSetup>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<ApiBehaviorOptions>, ApiBehaviorOptionsSetup>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<RouteOptions>, MvcCoreRouteOptionsSetup>());
        
        //
        // Action Discovery
        //
        // These are consumed only when creating action descriptors, then they can be deallocated
        services.TryAddSingleton<ApplicationModelFactory>();
        services.TryAddEnumerable(ServiceDescriptor.Transient<IApplicationModelProvider, DefaultApplicationModelProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IApplicationModelProvider, ApiBehaviorApplicationModelProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IActionDescriptorProvider, ControllerActionDescriptorProvider>());
        
        services.TryAddSingleton<IActionDescriptorCollectionProvider, DefaultActionDescriptorCollectionProvider>();
        
        //
        // Action Selection
        //
        services.TryAddSingleton<IActionSelector, ActionSelector>();
        services.TryAddSingleton<ActionConstraintCache>();
        
        // Will be cached by the DefaultActionSelector
        services.TryAddEnumerable(ServiceDescriptor.Transient<IActionConstraintProvider, DefaultActionConstraintProvider>());
        
        // Policies for Endpoints
        services.TryAddEnumerable(ServiceDescriptor.Singleton<MatcherPolicy, ConsumesMatcherPolicy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<MatcherPolicy, ActionConstraintMatcherPolicy>());
        
        //
        // Controller Factory
        //
        // This has a cache, so it needs to be a singleton
        services.TryAddSingleton<IControllerFactory, DefaultControllerFactory>();
        
        // Will be cached by the DefaultControllerFactory
        services.TryAddTransient<IControllerActivator, DefaultControllerActivator>();
        
        services.TryAddSingleton<IControllerFactoryProvider, ControllerFactoryProvider>();
        services.TryAddSingleton<IControllerActivatorProvider, ControllerActivatorProvider>();
        services.TryAddEnumerable(
            ServiceDescriptor.Transient<IControllerPropertyActivator, DefaultControllerPropertyActivator>());
        
        //
        // Action Invoker
        //
        // The IActionInvokerFactory is cachable
        services.TryAddSingleton<IActionInvokerFactory, ActionInvokerFactory>();
        services.TryAddEnumerable(
            ServiceDescriptor.Transient<IActionInvokerProvider, ControllerActionInvokerProvider>());
        
        // These are stateless
        services.TryAddSingleton<ControllerActionInvokerCache>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IFilterProvider, DefaultFilterProvider>());
        services.TryAddSingleton<IActionResultTypeMapper, ActionResultTypeMapper>();
        
        //
        // Request body limit filters
        //
        services.TryAddTransient<RequestSizeLimitFilter>();
        services.TryAddTransient<DisableRequestSizeLimitFilter>();
        services.TryAddTransient<RequestFormLimitsFilter>();
        
        //
        // ModelBinding, Validation
        //
        // The DefaultModelMetadataProvider does significant caching and should be a singleton.
        services.TryAddSingleton<IModelMetadataProvider, DefaultModelMetadataProvider>();
        services.TryAdd(ServiceDescriptor.Transient<ICompositeMetadataDetailsProvider>(s =>
                        {
                            var options = s.GetRequiredService<IOptions<MvcOptions>>().Value;
                            return new DefaultCompositeMetadataDetailsProvider(options.ModelMetadataDetailsProviders);
                        }));
        services.TryAddSingleton<IModelBinderFactory, ModelBinderFactory>();
        services.TryAddSingleton<IObjectModelValidator>(s =>
                 	{
                        var options = s.GetRequiredService<IOptions<MvcOptions>>().Value;
                        var metadataProvider = s.GetRequiredService<IModelMetadataProvider>();
                        return new DefaultObjectValidator(metadataProvider, options.ModelValidatorProviders, options);
                    });
        services.TryAddSingleton<ClientValidatorCache>();
        services.TryAddSingleton<ParameterBinder>();
        
        //
        // Random Infrastructure
        //
        services.TryAddSingleton<MvcMarkerService, MvcMarkerService>();
        services.TryAddSingleton<ITypeActivatorCache, TypeActivatorCache>();
        services.TryAddSingleton<IUrlHelperFactory, UrlHelperFactory>();
        services.TryAddSingleton<IHttpRequestStreamReaderFactory, MemoryPoolHttpRequestStreamReaderFactory>();
        services.TryAddSingleton<IHttpResponseStreamWriterFactory, MemoryPoolHttpResponseStreamWriterFactory>();
        services.TryAddSingleton(ArrayPool<byte>.Shared);
        services.TryAddSingleton(ArrayPool<char>.Shared);
        services.TryAddSingleton<OutputFormatterSelector, DefaultOutputFormatterSelector>();
        services.TryAddSingleton<IActionResultExecutor<ObjectResult>, ObjectResultExecutor>();
        services.TryAddSingleton<IActionResultExecutor<PhysicalFileResult>, PhysicalFileResultExecutor>();
        services.TryAddSingleton<IActionResultExecutor<VirtualFileResult>, VirtualFileResultExecutor>();
        services.TryAddSingleton<IActionResultExecutor<FileStreamResult>, FileStreamResultExecutor>();
        services.TryAddSingleton<IActionResultExecutor<FileContentResult>, FileContentResultExecutor>();
        services.TryAddSingleton<IActionResultExecutor<RedirectResult>, RedirectResultExecutor>();
        services.TryAddSingleton<IActionResultExecutor<LocalRedirectResult>, LocalRedirectResultExecutor>();
        services.TryAddSingleton<IActionResultExecutor<RedirectToActionResult>, RedirectToActionResultExecutor>();
        services.TryAddSingleton<IActionResultExecutor<RedirectToRouteResult>, RedirectToRouteResultExecutor>();
        services.TryAddSingleton<IActionResultExecutor<RedirectToPageResult>, RedirectToPageResultExecutor>();
        services.TryAddSingleton<IActionResultExecutor<ContentResult>, ContentResultExecutor>();
        services.TryAddSingleton<IActionResultExecutor<JsonResult>, SystemTextJsonResultExecutor>();
        services.TryAddSingleton<IClientErrorFactory, ProblemDetailsClientErrorFactory>();
        services.TryAddSingleton<ProblemDetailsFactory, DefaultProblemDetailsFactory>();
        
        //
        // Route Handlers
        //
        services.TryAddSingleton<MvcRouteHandler>(); // Only one per app
        services.TryAddTransient<MvcAttributeRouteHandler>(); // Many per app
        
        //
        // Endpoint Routing / Endpoints
        //
        services.TryAddSingleton<ControllerActionEndpointDataSourceFactory>();
        services.TryAddSingleton<OrderedEndpointsSequenceProviderCache>();
        services.TryAddSingleton<ControllerActionEndpointDataSourceIdProvider>();
        services.TryAddSingleton<ActionEndpointFactory>();
        services.TryAddSingleton<DynamicControllerEndpointSelectorCache>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<MatcherPolicy, DynamicControllerEndpointMatcherPolicy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRequestDelegateFactory, ControllerRequestDelegateFactory>());
        
        //
        // Middleware pipeline filter related
        //
        services.TryAddSingleton<MiddlewareFilterConfigurationProvider>();
        // This maintains a cache of middleware pipelines, so it needs to be a singleton
        services.TryAddSingleton<MiddlewareFilterBuilder>();
        // Sets ApplicationBuilder on MiddlewareFilterBuilder
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, MiddlewareFilterBuilderStartupFilter>());
    }
    
    private static void ConfigureDefaultServices(IServiceCollection services)
    {
        services.AddRouting();
    }
}

```



#### 2.4 add mvc

##### 2.4.1 add controllers

```c#
public static class MvcServiceCollectionExtensions
{
    public static IMvcBuilder AddControllers(this IServiceCollection services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        var builder = AddControllersCore(services);
        return new MvcBuilder(builder.Services, builder.PartManager);
    }
        
    public static IMvcBuilder AddControllers(this IServiceCollection services, Action<MvcOptions> configure)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        // This method excludes all of the view-related services by default.
        var builder = AddControllersCore(services);
        if (configure != null)
        {
            builder.AddMvcOptions(configure);
        }
        
        return new MvcBuilder(builder.Services, builder.PartManager);
    }
    
    private static IMvcCoreBuilder AddControllersCore(IServiceCollection services)
    {
        // This method excludes all of the view-related services by default.
        return services.AddMvcCore()
            		  .AddApiExplorer()
            		  .AddAuthorization()
            		  .AddCors()
            		  .AddDataAnnotations()
            		  .AddFormatterMappings();
    }
}

```

##### 2.4.2 add tag help framework

```c#
public static class MvcServiceCollectionExtensions
{
    internal static void AddTagHelpersFrameworkParts(ApplicationPartManager partManager)
    {
        var mvcTagHelpersAssembly = typeof(InputTagHelper).Assembly;
        if (!partManager.ApplicationParts
            		   .OfType<AssemblyPart>()
            		   .Any(p => p.Assembly == mvcTagHelpersAssembly))
        {
            partManager.ApplicationParts.Add(new FrameworkAssemblyPart(mvcTagHelpersAssembly));
        }
        
        var mvcRazorAssembly = typeof(UrlResolutionTagHelper).Assembly;
        if (!partManager.ApplicationParts
            		   .OfType<AssemblyPart>()
            		   .Any(p => p.Assembly == mvcRazorAssembly))
        {
            partManager.ApplicationParts.Add(new FrameworkAssemblyPart(mvcRazorAssembly));
        }
    }
    
    [DebuggerDisplay("{Name}")]
    private class FrameworkAssemblyPart : 
    	AssemblyPart, 
    	ICompilationReferencesProvider
    {
        IEnumerable<string> ICompilationReferencesProvider.GetReferencePaths() => Enumerable.Empty<string>();
            
        public FrameworkAssemblyPart(Assembly assembly) : base(assembly)
        {
        }                
    }
}

```

##### 2.4.3 add controllers with view

```c#
public static class MvcServiceCollectionExtensions
{
    public static IMvcBuilder AddControllersWithViews(this IServiceCollection services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        var builder = AddControllersWithViewsCore(services);
        return new MvcBuilder(builder.Services, builder.PartManager);
    }
    
    
    public static IMvcBuilder AddControllersWithViews(
        this IServiceCollection services, 
        Action<MvcOptions> configure)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        // This method excludes all of the view-related services by default.
        var builder = AddControllersWithViewsCore(services);
        if (configure != null)
        {
            builder.AddMvcOptions(configure);
        }
        
        return new MvcBuilder(builder.Services, builder.PartManager);
    }
    
    private static IMvcCoreBuilder AddControllersWithViewsCore(IServiceCollection services)
    {
        var builder = AddControllersCore(services).AddViews()
							                   .AddRazorViewEngine()
							                   .AddCacheTagHelper();

        AddTagHelpersFrameworkParts(builder.PartManager);
        return builder;
    }
}
```

##### 2.4.4 add razor page

```c#
public static class MvcServiceCollectionExtensions
{
    public static IMvcBuilder AddRazorPages(this IServiceCollection services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        var builder = AddRazorPagesCore(services);
        return new MvcBuilder(builder.Services, builder.PartManager);
    }
    
    public static IMvcBuilder AddRazorPages(
        this IServiceCollection services, 
        Action<RazorPagesOptions> configure)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        var builder = AddRazorPagesCore(services);
        if (configure != null)
        {
            builder.AddRazorPages(configure);
        }
        
        return new MvcBuilder(builder.Services, builder.PartManager);        
    }

    private static IMvcCoreBuilder AddRazorPagesCore(IServiceCollection services)
    {
        // This method includes the minimal things controllers need. 
        // It's not really feasible to exclude the services for controllers.
        var builder = services.AddMvcCore()
			                 .AddAuthorization()
			                 .AddDataAnnotations()
			                 .AddRazorPages()
			                 .AddCacheTagHelper();

        AddTagHelpersFrameworkParts(builder.PartManager);        
        return builder;
    }    
}

```

##### 2.4.5 add mvc

```c#
public static class MvcServiceCollectionExtensions
{    
    public static IMvcBuilder AddMvc(this IServiceCollection services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        services.AddControllersWithViews();
        return services.AddRazorPages();
    }
            
    public static IMvcBuilder AddMvc(
        this IServiceCollection services, 
        Action<MvcOptions> setupAction)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }        
        if (setupAction == null)
        {
            throw new ArgumentNullException(nameof(setupAction));
        }
        
        var builder = services.AddMvc();
        builder.Services.Configure(setupAction);
        
        return builder;
    }        
}

```

