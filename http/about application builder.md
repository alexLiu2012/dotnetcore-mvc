## about application builder

相关程序集

* microsoft.aspnetcore.http.abstract
* microsoft.aspnetcore.http

----

### 1. about

#### 1.1 overview

* application builder 是 asp.net core 框架定义的管道请求委托（request delegate）的构造器
* 它创建`RequestDelegate`，即封装的`Func<HttpContext,Task>`
* request delegate 对 http context 进行处理，最终得到 http response，
* http response 同样包裹在 http context 中

#### 1.2 how designed



### 2. details

#### 2.1 application builder

##### 2.1.1 接口

```c#
public interface IApplicationBuilder
{    
    ServiceProvider ApplicationServices { get; set; }            
    IFeatureCollection ServerFeatures { get; }        
    IDictionary<string, object?> Properties { get; }       
    
    IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware);        
    IApplicationBuilder New();        
    RequestDelegate Build();
}

```

##### 2.1.2 实现

```c#
public class ApplicationBuilder : IApplicationBuilder
{
    private const string ServerFeaturesKey = "server.Features";
    private const string ApplicationServicesKey = "application.Services";
    
    private readonly List<Func<RequestDelegate, RequestDelegate>> _components = new();
    
    // service provider
    public IServiceProvider ApplicationServices
    {
        get
        {
            return GetProperty<IServiceProvider>(ApplicationServicesKey)!;
        }
        set
        {
            SetProperty<IServiceProvider>(ApplicationServicesKey, value);
        }
    }
    // feature collection    
    public IFeatureCollection ServerFeatures
    {
        get
        {
            return GetProperty<IFeatureCollection>(ServerFeaturesKey)!;
        }
    }
    // properties    
    public IDictionary<string, object?> Properties { get; }
                    
    /* 注入、解析 property */
    
    private T? GetProperty<T>(string key)
    {
        return Properties
            .TryGetValue(
            	key, 
            	out var value) 
            ? (T?)value : default(T);
    }
    
    private void SetProperty<T>(string key, T value)
    {
        Properties[key] = value;
    }
    
    /* 实现接口的 use 方法 */
            
    /* 实现接口的 new 方法 */       
            
    /* 实现了接口的 build 方法 */        
}

```

###### 2.1.2.1 构造函数

```c#
public class ApplicationBuilder : IApplicationBuilder
{
    public ApplicationBuilder(
        IServiceProvider serviceProvider)
    {
        Properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        ApplicationServices = serviceProvider;
    }
        
    public ApplicationBuilder(
        IServiceProvider serviceProvider, 
        object server)
        : this(serviceProvider)
    {            
        SetProperty(ServerFeaturesKey, server);
    }   
}

```

###### 2.1.2.2 use

* 添加 middleware，用 func(request delegate, request delegate) 形式

```c#
public class ApplicationBuilder : IApplicationBuilder
{
    public IApplicationBuilder Use(
        Func<RequestDelegate, RequestDelegate> middleware)
    {
        _components.Add(middleware);
        return this;
    }
}

```

###### 2.1.2.3 new

* 克隆自身（properties）

```c#
public class ApplicationBuilder : IApplicationBuilder
{
    public IApplicationBuilder New()
    {
        return new ApplicationBuilder(this);
    }
    
    private ApplicationBuilder(ApplicationBuilder builder)
    {
        Properties = new CopyOnWriteDictionary<string, object?>(
            builder.Properties, 
            StringComparer.Ordinal);
    }
}

```

###### 2.1.2.4 build

* 构架 request delegate

```c#
public class ApplicationBuilder : IApplicationBuilder
{
    public RequestDelegate Build()
    {
        RequestDelegate app = context =>
        {
            // If we reach the end of the pipeline, 
            // but we have an endpoint, 
            // then something unexpected has happened.
            // This could happen if user code sets an endpoint, 
            // but they forgot to add the UseEndpoint middleware.
            var endpoint = context.GetEndpoint();
            var endpointRequestDelegate = endpoint?.RequestDelegate;
            if (endpointRequestDelegate != null)
            {
                var message =                        
                    $"The request reached the end of the pipeline 
                    "without executing the endpoint: 
                    "'{endpoint!.DisplayName}'. " +
                    $"Please register the EndpointMiddleware using 
                    "'{nameof(IApplicationBuilder)}.UseEndpoints(...)' 
                    "if using " +
                    $"routing.";
                
                throw new InvalidOperationException(message);
            }
            
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        };
        
        for (var c = _components.Count - 1; c >= 0; c--)
        {
            app = _components[c](app);
        }
        
        return app;
    }
}

```

#### 2.3 扩展方法

##### 2.2.1 use middleware

###### 2.2.1.1 by func

```c#
public static class UseExtensions
{    
    public static IApplicationBuilder Use(
        this IApplicationBuilder app, 
        Func<HttpContext, Func<Task>, Task> middleware)
    {
        return app.Use(next =>
        {
            // 创建了 func(context, task)，即 request delegate
            return context =>
            {
                Func<Task> simpleNext = () => next(context);
                return middleware(context, simpleNext);
            };
        });
    }
}

```

###### 2.2.1.2 by TMiddleware

```c#
public static class UseMiddlewareExtensions
{
    public static IApplicationBuilder UseMiddleware<
        [DynamicallyAccessedMembers(
            MiddlewareAccessibility)]TMiddleware>(
        this IApplicationBuilder app, 
        params object?[] args)
    {
        return app.UseMiddleware(typeof(TMiddleware), args);
    }
}
```

###### 2.2.1.3 by middleware type

```c#
public static class UseMiddlewareExtensions
{
    internal const string InvokeMethodName = "Invoke";
    internal const string InvokeAsyncMethodName = "InvokeAsync";
    
    private static readonly MethodInfo GetServiceInfo = 
        typeof(UseMiddlewareExtensions)
        	.GetMethod(
        		nameof(GetService), 
        		BindingFlags.NonPublic | 
        		BindingFlags.Static)!;
    
    // We're going to keep all public constructors and public methods on middleware
    private const DynamicallyAccessedMemberTypes MiddlewareAccessibility = 
        DynamicallyAccessedMemberTypes.PublicConstructors | 
        DynamicallyAccessedMemberTypes.PublicMethods;
        
    public static IApplicationBuilder UseMiddleware(
        this IApplicationBuilder app, 
        [DynamicallyAccessedMembers(
            MiddlewareAccessibility)] Type middleware, 
        params object?[] args)
    {
        /* 如果 middleware 实现了 IMiddleware 接口，
           且 middleware 没有泛型参数，
           调用 use middleware interface 方法 */
        if (typeof(IMiddleware).IsAssignableFrom(middleware))
        {
            // IMiddleware doesn't support passing args directly since it's
            // activated from the container
            if (args.Length > 0)
            {
                throw new NotSupportedException(
                    Resources
                    	.FormatException_UseMiddlewareExplicitArgumentsNotSupported(
                            typeof(IMiddleware)));
            }
            
            return UseMiddlewareInterface(app, middleware);
        }
        
        /* 否则，即 middleware 没有实现 IMiddleware 接口，
           通过反射创建 func（request delegate，request delegate）并 use */
        var applicationServices = app.ApplicationServices;
        return app.Use(next =>
        {
            /* 反射获取 middleware 中的方法，
               过滤方法名为 invoke 或 invokeAsync */
            var methods = middleware
                .GetMethods(BindingFlags.Instance | BindingFlags.Public);
            
            var invokeMethods = methods
                .Where(m =>
                    string.Equals(
                        m.Name, 
                        InvokeMethodName, 
                        StringComparison.Ordinal) || 
                    string.Equals(
                        m.Name, 
                        InvokeAsyncMethodName, 
                        StringComparison.Ordinal))
                .ToArray();
            
            /* 如果 method 不存在或者不唯一，抛出异常 */
            if (invokeMethods.Length > 1)
            {
                throw new InvalidOperationException(
                    Resources.FormatException_UseMiddleMutlipleInvokes(
                        InvokeMethodName, 
                        InvokeAsyncMethodName));
            }
            
            if (invokeMethods.Length == 0)
            {
                throw new InvalidOperationException(
                    Resources.FormatException_UseMiddlewareNoInvokeMethod(
                        InvokeMethodName, 
                        InvokeAsyncMethodName, 
                        middleware));
            }
            
            /* 如果 method 的返回类型不是 task，抛出异常 */
            var methodInfo = invokeMethods[0];
            if (!typeof(Task).IsAssignableFrom(methodInfo.ReturnType))
            {
                    throw new InvalidOperationException(
                        Resources.FormatException_UseMiddlewareNonTaskReturnType(
                            InvokeMethodName, 
                            InvokeAsyncMethodName, 
                            nameof(Task)));
            }
            
            // 获取 method 参数
            var parameters = methodInfo.GetParameters();
            
            /* 如果没有参数，
               或者第一个参数不是 httpContext，抛出异常 */
            if (parameters.Length == 0 || 
                parameters[0].ParameterType != typeof(HttpContext))
            {
                throw new InvalidOperationException(
                    Resources.FormatException_UseMiddlewareNoParameters(
                        InvokeMethodName, 
                        InvokeAsyncMethodName, 
                        nameof(HttpContext)));
            }
            
            // 获取构造函数参数
            var ctorArgs = new object[args.Length + 1];
            ctorArgs[0] = next;
            Array.Copy(args, 0, ctorArgs, 1, args.Length);
            
            // 创建 middleware 实例
            var instance = ActivatorUtilities.CreateInstance(
                app.ApplicationServices, 
                middleware, 
                ctorArgs);
            
            /* 如果只有一个参数，
               它是 terminal middleware，只有一个 request delegate，
               直接由 middleware 创建 request delegate */
            if (parameters.Length == 1)
            {
                return (RequestDelegate)methodInfo.CreateDelegate(
                    typeof(RequestDelegate), 
                    instance);
            }
            
            // 由 methodInfo 创建构建 request delegate 的 func 委托
            var factory = Compile<object>(methodInfo, parameters);
            
            /* 由 return 方法创建 func<httpcontext, task>，
               即 request delegate */
            return context =>
            {
                // 从 http context 解析 service provider，
                // 如果没有，抛出异常
                var serviceProvider = context.RequestServices ?? applicationServices;
                if (serviceProvider == null)
                {
                    throw new InvalidOperationException(
                        Resources.FormatException_UseMiddlewareIServiceProviderNotAvailable(
                            nameof(IServiceProvider)));
                }
                
                // 调用 func 创建 task
                return factory(instance, context, serviceProvider);
            };
        });
    }
    
    /* 由 IMiddleware 接口添加 middleware 到 app builder */
    
    private static IApplicationBuilder UseMiddlewareInterface(
        IApplicationBuilder app, 
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] Type middlewareType)
    {
        /* 创建 func(request delegate, request delegate) 委托，
           使用 app builder 原始 use 方法注入 */
        return app.Use(next =>
        {
            return async context =>
            {
                // 从 http context 中解析 middleware factory，
                // 如果没有，抛出异常
                var middlewareFactory = (IMiddlewareFactory?)context
                    .RequestServices
                    .GetService(typeof(IMiddlewareFactory));
                if (middlewareFactory == null)
                {
                    // No middleware factory
                    throw new InvalidOperationException(
                        Resources.FormatException_UseMiddlewareNoMiddlewareFactory(
                            typeof(IMiddlewareFactory)));
                }
                
                // 由 middleware factory 创建 middleware
                var middleware = middlewareFactory.Create(middlewareType);
                if (middleware == null)
                {
                    // The factory returned null, it's a broken implementation
                    throw new InvalidOperationException(
                        Resources.FormatException_UseMiddlewareUnableToCreateMiddleware(
                            middlewareFactory.GetType(), 
                            middlewareType));
                }
                
                try
                {
                    await middleware.InvokeAsync(context, next);
                }
                finally
                {
                    middlewareFactory.Release(middleware);
                }
            };
        });
    }
    
    /* 使用反射创建 func(middleware, httpcontext, servcieprovider) */
    
    private static Func<T, HttpContext, IServiceProvider, Task> Compile<T>(
        MethodInfo methodInfo, 
        ParameterInfo[] parameters)
    {
        // If we call something like
        //
        // public class Middleware
        // {
        //    public Task Invoke(HttpContext context, ILoggerFactory loggerFactory)
        //    {
        //
        //    }
        // }
        //
        
        // We'll end up with something like this:
        //   Generic version:
        //
        //   Task Invoke(Middleware instance, HttpContext httpContext, IServiceProvider provider)
        //   {
        //      return instance.Invoke(httpContext, (ILoggerFactory)UseMiddlewareExtensions.GetService(provider, typeof(ILoggerFactory));
        //   }
        
        //   Non generic version:
        //
        //   Task Invoke(object instance, HttpContext httpContext, IServiceProvider provider)
        //   {
        //      return ((Middleware)instance).Invoke(httpContext, (ILoggerFactory)UseMiddlewareExtensions.GetService(provider, typeof(ILoggerFactory));
        //   }
        
        var middleware = typeof(T);
        
        var httpContextArg = Expression.Parameter(typeof(HttpContext), "httpContext");
        var providerArg = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
        var instanceArg = Expression.Parameter(middleware, "middleware");
        
        var methodArguments = new Expression[parameters.Length];
        methodArguments[0] = httpContextArg;
        for (int i = 1; i < parameters.Length; i++)
        {
            var parameterType = parameters[i].ParameterType;
            if (parameterType.IsByRef)
            {
                throw new NotSupportedException(
                    Resources.FormatException_InvokeDoesNotSupportRefOrOutParams(
                        InvokeMethodName));
            }
            
            var parameterTypeExpression = new Expression[]
            {
                providerArg,
                Expression.Constant(
                    parameterType, 
                    typeof(Type)),
                Expression.Constant(
                    methodInfo.DeclaringType, 
                    typeof(Type))
            };
            
            var getServiceCall = Expression.Call(
                GetServiceInfo, 
                parameterTypeExpression);
            methodArguments[i] = Expression.Convert(
                getServiceCall, 
                parameterType);
        }
        
        Expression middlewareInstanceArg = instanceArg;
        if (methodInfo.DeclaringType != null && 
            methodInfo.DeclaringType != typeof(T))
        {
            middlewareInstanceArg = Expression.Convert(
                middlewareInstanceArg, 
                methodInfo.DeclaringType);
        }
        
        var body = Expression.Call(
            middlewareInstanceArg, 
            methodInfo, 
            methodArguments);
        
        var lambda = Expression
            .Lambda<Func<T, HttpContext, IServiceProvider, Task>>(
            	body, 
            	instanceArg, 
            	httpContextArg, 
            	providerArg);
        
        return lambda.Compile();
    }
    
     private static object GetService(IServiceProvider sp, Type type, Type middleware)
    {
        var service = sp.GetService(type);
        if (service == null)
        {
            throw new InvalidOperationException(
                Resources.FormatException_InvokeMiddlewareNoService(
                    type, 
                    middleware));
        }
        
        return service;
    }
}

```

##### 2.2.2 use base path

###### 2.2.2.1 方法

```c#
public static class UsePathBaseExtensions
{    
    public static IApplicationBuilder UsePathBase(
        this IApplicationBuilder app, 
        PathString pathBase)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        
        // Strip trailing slashes
        pathBase = pathBase.Value?.TrimEnd('/');
        if (!pathBase.HasValue)
        {
            return app;
        }
        
        // 使用 app.useMiddleware<TMiddleware>(args) 方法
        return app.UseMiddleware<UsePathBaseMiddleware>(pathBase);
    }            
}

```

###### 2.2.2.2 base path middleware

* 实现 path base 的具体的 middleware

* TMiddleware 的具名类型

```c#
public class UsePathBaseMiddleware
{
    private readonly RequestDelegate _next;
    private readonly PathString _pathBase;
           
    public UsePathBaseMiddleware(
        RequestDelegate next, 
        PathString pathBase)
    {
        if (next == null)
        {
            throw new ArgumentNullException(nameof(next));
        }        
        if (!pathBase.HasValue)
        {
            throw new ArgumentException(
                $"{nameof(pathBase)} cannot be null or empty.");
        }
        
        _next = next;
        _pathBase = pathBase;
    }
    
        
    public async Task Invoke(HttpContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        PathString matchedPath;
        PathString remainingPath;
        
        /* 解析 matched path，如果有 */
        if (context.Request.Path
            	.StartsWithSegments(
                    _pathBase, 
                    out matchedPath, 
                    out remainingPath))
        {            
            var originalPath = context.Request.Path;
            var originalPathBase = context.Request.PathBase;
            
            /* 修改 http context 中的 request.path*/
            context.Request.Path = remainingPath;
            context.Request.PathBase = originalPathBase.Add(matchedPath);
            
            try
            {
                // 执行 next（delegate）
                await _next(context);
            }
            finally
            {
                /* 执行后恢复 http context 中的 request.path */
                context.Request.Path = originalPath;
                context.Request.PathBase = originalPathBase;
            }
        }
        else
        {
            await _next(context);
        }
    }
}

```



##### 2.2.3 use when

```c#
using Predicate = Func<HttpContext, bool>;

public static class UseWhenExtensions
{    
    public static IApplicationBuilder UseWhen(
        this IApplicationBuilder app, 
        Predicate predicate, 
        Action<IApplicationBuilder> configuration)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        
        if (predicate == null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }
        
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }
        
        // Create and configure the branch builder right away; otherwise,
        // we would end up running our branch after all the components
        // that were subsequently added to the main builder.
        var branchBuilder = app.New();
        configuration(branchBuilder);
        
        return app.Use(main =>
        {
            // This is called only when the main application builder 
            // is built, not per request.
            branchBuilder.Run(main);
            var branch = branchBuilder.Build();
            
            return context =>
            {
                if (predicate(context))
                {
                    return branch(context);
                }
                else
                {
                    return main(context);
                }
            };            
        });
    }
}

```

##### 2.2.4 map

###### 2.2.4.1 方法

```c#
public static class MapExtensions
{
    
    public static IApplicationBuilder Map(
        this IApplicationBuilder app, 
        PathString pathMatch, 
        Action<IApplicationBuilder> configuration)
    {
        return Map(
            app, 
            pathMatch, 
            preserveMatchedPathSegment: false, 
            configuration);
    }
    
    
    public static IApplicationBuilder Map(
        this IApplicationBuilder app, 
        PathString pathMatch, 
        bool preserveMatchedPathSegment, 
        Action<IApplicationBuilder> configuration)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }
        
        if (pathMatch.HasValue && 
            pathMatch.Value!.EndsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The path must not end with a '/'", 
                nameof(pathMatch));
            
        }
        
        // create branch
        var branchBuilder = app.New();
        configuration(branchBuilder);
        var branch = branchBuilder.Build();
        
        var options = new MapOptions
        {
            Branch = branch,
            PathMatch = pathMatch,
            PreserveMatchedPathSegment = preserveMatchedPathSegment
        };
        
        return app.Use(next => 
        	new MapMiddleware(next, options).Invoke);
    }
}

```

###### 2.2.4.2 map options

```c#
public class MapOptions
{    
    public PathString PathMatch { get; set; }        
    public RequestDelegate? Branch { get; set; }       
    public bool PreserveMatchedPathSegment { get; set; }
}

```

###### 2.2.4.3 map middleware

```c#
public class MapMiddleware
{
    private readonly RequestDelegate _next;
    private readonly MapOptions _options;
        
    public MapMiddleware(
        RequestDelegate next, 
        MapOptions options)
    {
        if (next == null)
        {
            throw new ArgumentNullException(nameof(next));
        }
        
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        if (options.Branch == null)
        {
            throw new ArgumentException("Branch not set on options.", nameof(options));
        }
        
        _next = next;
        _options = options;
    }
            
    public async Task Invoke(HttpContext context)
    {
        if (context == null)            
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        // 解析 matched path，如果有
        if (context.Request.Path
            	.StartsWithSegments(
                    _options.PathMatch, 
                    out var matchedPath, 
                    out var remainingPath))
        {
            var path = context.Request.Path;
            var pathBase = context.Request.PathBase;
            
            // 修改 http context 中的 request.path 为 matched path
            if (!_options.PreserveMatchedPathSegment)
            {
                // Update the path
                context.Request.PathBase = pathBase.Add(matchedPath);
                context.Request.Path = remainingPath;
            }
            
            try
            {
                // 执行 next（request delegate），
                // 包裹在 map options 中
                await _options.Branch!(context);
            }
            finally
            {
                if (!_options.PreserveMatchedPathSegment)
                {
                    context.Request.PathBase = pathBase;
                    context.Request.Path = path;
                }
            }
        }
        else
        {
            await _next(context);
        }
    }
}

```

##### 2.2.5 map when

###### 2.2.5.1 方法

```c#
using Predicate = Func<HttpContext, bool>;

public static class MapWhenExtensions
{    
    public static IApplicationBuilder MapWhen(
        this IApplicationBuilder app, 
        Predicate predicate, 
        Action<IApplicationBuilder> configuration)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        
        if (predicate == null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }
        
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }
        
        // create branch
        var branchBuilder = app.New();
        configuration(branchBuilder);
        var branch = branchBuilder.Build();
        
        // put middleware in pipeline
        var options = new MapWhenOptions
        {
            Predicate = predicate,
            Branch = branch,
        };
        return app.Use(next => new MapWhenMiddleware(next, options).Invoke);
    }
}

```

###### 2.2.5.2 map when options

```c#
public class MapWhenOptions
{
    private Func<HttpContext, bool>? _predicate;
        
    public Func<HttpContext, bool>? Predicate
    {
        get
        {
            return _predicate;
        }
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            
            _predicate = value;
        }
    }
        
    public RequestDelegate? Branch { get; set; }
}

```

###### 2.2.5.3 map when middleware

```c#
public class MapWhenMiddleware
{
    private readonly RequestDelegate _next;
    private readonly MapWhenOptions _options;
    
    
    public MapWhenMiddleware(RequestDelegate next, MapWhenOptions options)
    {
        if (next == null)
        {
            throw new ArgumentNullException(nameof(next));
        }        
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }        
        if (options.Predicate == null)
        {
            throw new ArgumentException("Predicate not set on options.", nameof(options));
        }        
        if (options.Branch == null)
        {
            throw new ArgumentException("Branch not set on options.", nameof(options));
        }
        
        _next = next;
        _options = options;
    }
            
    public async Task Invoke(HttpContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        if (_options.Predicate!(context))
        {
            await _options.Branch!(context);
        }
        else
        {
            await _next(context);
        }
    }
}

```

##### 2.2.6 run

```c#
public static class RunExtensions
{    
    public static void Run(this IApplicationBuilder app, RequestDelegate handler)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }
        
        app.Use(_ => handler);
    }
}

```

#### 2.4 middleware

##### 2.4.1 IMiddleware 接口

```c#
public interface IMiddleware
{    
    Task InvokeAsync(HttpContext context, RequestDelegate next);
}

```

##### 2.4.2 IMiddlewareFactory 接口

```c#
public interface IMiddlewareFactory
{    
    IMiddleware? Create(Type middlewareType);    
    void Release(IMiddleware middleware);    
}

```

##### 2.4.3 IMiddlewareFactory 实现

```c#
public class MiddlewareFactory : IMiddlewareFactory
{    
    private readonly IServiceProvider _serviceProvider;        
    public MiddlewareFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
        
    public IMiddleware? Create(Type middlewareType)
    {
        return _serviceProvider.GetRequiredService(middlewareType) as IMiddleware;
    }
        
    public void Release(IMiddleware middleware)
    {
        // The container owns the lifetime of the service
    }
}

```

