## about service components of web application

### 1. about

`(general) host`建立后，http application 被封装为`host`的一个`background service`。对应整个 http application 而言，可以分为通用的 request/response 通道，和高层次的 mvc 模式

#### 1.1 http 通道

##### 1.1.1 server

server 用于监听物理主机上的 http 请求（可以支持 web socket）；

对于监听到的 http 请求，使用 http application 将请求中包含的信息注入到`feature collection`集合。

##### 1.1.2 feature collection

因为跨平台支持，server 可以是不同的的实现，为了实现不同 server 的适配，增加一个中间层，即 feature 接口，feature 应对 http qurest、http response 的信息，不同 server 具体实现 feature 接口。

feature collection 是 feature 的集合

##### 1.1.3 http application

处理 http 请求的 pipeline，server 监听到 http request 后， 将http request 携带的信息封装到 feature collection，然后将由 http application。http application 主要进行3个处理：

* 创建 http context

  http application 中封装了`http context factory`，由其将 feature collection 创建为`http context`

* 执行处理请求的委托（request delegate）

  http context 中封装了处理请求的委托，即在`configure()`方法中配置的请求管道；

  delegate 将创建的 http context 作为参数传入，并执行具体的 委托

* 销毁 http context

###### 1.1.3.1 http context

封装了 http 信息，request、response，安全信息如 claim user 等

###### 1.1.3.2 application builder

http application 构造器，用于注入和配置 request delegate；扩展的方法中可以添加 middleware，即一种表示 request delegate 的委托方法，可以是强类型的实现`IMiddleware`的；也可以是弱类型的（必须包含 invoke 方法），这样可以匹配多个不确定参数（从 di 获取）

#### 1.2 routing

不同的 http request 可能需要不同的处理，这就是 routing（路由），即判断 http request 匹配的 request delegate 并执行。它本身也是一个 middleware，使用时需要注入到请求管道（request delegate）

##### 1.2.1 route template & constraint

表示

##### 1.2.2 router routing

asp.net core 2.1 时用的路由方式

`useRouter(IRouter)`将 router middleware 注入请求管道，用于处理 router routing；`useRouter(action<routerBuilder> routerBuilder)`将首先根据 action 配置 router builder，然后创建 router collection (irouter)，作为参数调用`useRouter(IRouter)`

* irouter

  表示路由的功能组件，包括 route 方法（正向路由）和 get virtual path 方法（反向路由）

  route（正向路由）时，解析 request 并将匹配的 handler 注入 route context 的 route data（如果能解析到）；

* irouterBuilder

  配置并创建 router collection (irouter)

##### 1.2.3 endpoint routing





### 2. http request pipeline

#### 2.1 server

##### 2.1.1 接口

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

##### 2.1.2 实现

* kestel
* IIS
* httpsys

#### 2.2 feature collection

##### 2.2.1 feature collection

###### 2.2.1.1 接口

```c#
public interface IFeatureCollection : 
	IEnumerable<KeyValuePair<Type, object>>
{    
    bool IsReadOnly { get; }        
    int Revision { get; }        
    object? this[Type key] { get; set; }
        
    TFeature? Get<TFeature>();        
    void Set<TFeature>(TFeature? instance);
}

```

###### 2.2.1.2 实现

```c#
public class FeatureCollection : IFeatureCollection
{
    private static readonly KeyComparer 
        FeatureKeyComparer = new KeyComparer();
    
    private readonly IFeatureCollection? _defaults;
    private IDictionary<Type, object>? _features;
    private volatile int _containerRevision;
    
    /* 实现接口属性 */
    
    public bool IsReadOnly 
    { 
        get 
        { 
            return false; 
        } 
    }
    
    public virtual int Revision
    {
        get 
        { 
            return _containerRevision + (_defaults?.Revision ?? 0); 
        }
    }
    
    public object? this[Type key]
    {
        get
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            
            return _features != null && 
                   _features.TryGetValue(key, out var result) 
                       ? result 
                	   : _defaults?[key];
        }
        set
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            
            if (value == null)
            {
                if (_features != null && _features.Remove(key))
                {
                    _containerRevision++;
                }
                return;
            }
            
            if (_features == null)
            {
                _features = new Dictionary<Type, object>();
            }
            
            _features[key] = value;
            _containerRevision++;
        }
    }
    
    /* 构造函数 */    
    public FeatureCollection()
    {
    }
    
    public FeatureCollection(IFeatureCollection defaults)
    {
        _defaults = defaults;
    }
                                    
    /* 迭代器 */    
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
        
    public IEnumerator<KeyValuePair<Type, object>> GetEnumerator()
    {
        if (_features != null)
        {
            foreach (var pair in _features)
            {
                yield return pair;
            }
        }
        
        if (_defaults != null)
        {
            // Don't return features masked by the wrapper.
            foreach (var pair in _features == null 
                     	? _defaults 
                     	: _defaults.Except(
                            _features, 
                            FeatureKeyComparer))
            {
                yield return pair;
            }
        }
    }
    
    /* 方法 */
    public TFeature? Get<TFeature>()
    {
        return (TFeature?)this[typeof(TFeature)];
    }
        
    public void Set<TFeature>(TFeature? instance)
    {
        this[typeof(TFeature)] = instance;
    }
    
    // 比较器
    private class KeyComparer : IEqualityComparer<KeyValuePair<Type, object>>
    {
        public bool Equals(
            KeyValuePair<Type, object> x, 
            KeyValuePair<Type, object> y)
        {
            return x.Key.Equals(y.Key);
        }
        
        public int GetHashCode(
            KeyValuePair<Type, object> obj)
        {
            return obj.Key.GetHashCode();
        }
    }
}

```

##### 2.2.2 feature reference

###### 2.2.2.1 feature reference

```c#
public struct FeatureReference<T>
{
    public static readonly FeatureReference<T> Default = 
        new FeatureReference<T>(default(T), -1);
    
    private T? _feature;
    private int _revision;
    
    private FeatureReference(T? feature, int revision)
    {
        _feature = feature;
        _revision = revision;
    }                
    
    public T? Fetch(IFeatureCollection features)
    {
        if (_revision == features.Revision)
        {
            return _feature;
        }
        _feature = (T?)features[typeof(T)];
        _revision = features.Revision;
        return _feature;
    }
        
    public T Update(
        IFeatureCollection features, 
        T feature)
    {
        features[typeof(T)] = feature;
        _feature = feature;
        _revision = features.Revision;
        return feature;
    }
}

```

###### 2.2.2.2 feature references

```c#
public struct FeatureReferences<TCache>
{
    public IFeatureCollection Collection { get; private set; }
    
    public TCache? Cache;
    public int Revision { get; private set; }
    
    // 构造函数
    public FeatureReferences(IFeatureCollection collection)
    {
        Collection = collection;
        Cache = default;
        Revision = collection.Revision;
    }
    
    /* 初始化 */
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Initalize(IFeatureCollection collection)
    {
        Revision = collection.Revision;
        Collection = collection;
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Initalize(IFeatureCollection collection, int revision)
    {
        Revision = revision;
        Collection = collection;
    }
    
    /* fetch */
    
    /// <summary>
    /// This API is part of ASP.NET Core's infrastructure and 
    //  should not be referenced by application code.
    /// </summary>
    public TFeature? Fetch<TFeature>(
        ref TFeature? cached, 
        Func<IFeatureCollection, 
        TFeature?> factory)
        	where TFeature : class? => Fetch(ref cached, Collection, factory);
        
    // Careful with modifications to the Fetch method; 
    // it is carefully constructed for inlining
    // See: https://github.com/aspnet/HttpAbstractions/pull/704
    // This method is 59 IL bytes and at inline call depth 3 from accessing a property.
    // This combination is enough for the jit to consider it an "unprofitable inline"
    // Aggressively inlining it causes the entire call chain to dissolve:
    //
    // This means this call graph:
    //
    // HttpResponse.Headers 
    // 	-> Response.HttpResponseFeature 
    // 	-> Fetch -> Fetch      
    //	-> Revision
    //  -> Collection -> Collection
    //  -> Collection.Revision
    //
    // Has 6 calls eliminated and becomes just:                                    
    // 	-> UpdateCached
    //
    // HttpResponse.Headers 
    //	-> Collection.Revision
    //  -> UpdateCached (not called on fast path)
    //
    // As this is inlined at the callsite we want to keep the method small, 
    // so it only detects if a reset or update is required and 
    // all the reset and update logic is pushed to UpdateCached.
    //
    // Generally Fetch is called at a ratio > x4 of UpdateCached so this is a large gain
    
    /// <summary>
    /// This API is part of ASP.NET Core's infrastructure and 
    /// should not be referenced by application code.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TFeature? Fetch<TFeature, TState>(        
        ref TFeature? cached,
        TState state,
        Func<TState, TFeature?> factory) where TFeature : class?
    {
        var flush = false;
        var revision = Collection?.Revision ?? ContextDisposed();
        if (Revision != revision)
        {
            // Clear cached value to force call to UpdateCached
            cached = null!;
            // Collection changed, clear whole feature cache
            flush = true;
        }
        
        return cached ?? UpdateCached(
            ref cached!, 
            state, 
            factory, 
            revision, 
            flush);
    }
    
    // Update and cache clearing logic, when the fast-path in Fetch isn't applicable
    private TFeature? UpdateCached<TFeature, TState>(
        ref TFeature? cached, 
        TState state, 
        Func<TState, TFeature?> factory, 
        int revision, bool flush) 
        	where TFeature : class?
    {
        if (flush)
        {
            // Collection detected as changed, clear cache
            Cache = default;
        }
        
        cached = Collection.Get<TFeature>();
        if (cached == null)
        {
            // Item not in collection, create it with factory
            cached = factory(state);
            // Add item to IFeatureCollection
            Collection.Set(cached);
            // Revision changed by .Set, update revision to new value
            Revision = Collection.Revision;
        }
        else if (flush)
        {
            // Cache was cleared, but item retrieved from current Collection for version
            // so use passed in revision rather than making another virtual call
            Revision = revision;
        }
        
        return cached;
    }        
    
    private static int ContextDisposed()
    {
        ThrowContextDisposed();
        return 0;
    }
    
    private static void ThrowContextDisposed()
    {
        throw new ObjectDisposedException(
            nameof(Collection), 
            nameof(IFeatureCollection) + " has been disposed.");
    }
}

```

#### 2.3 http application

##### 2.3.1 接口 

```c#
public interface IHttpApplication<TContext> where TContext : notnull
{        
    TContext CreateContext(IFeatureCollection contextFeatures);
        
    Task ProcessRequestAsync(TContext context);
        
    void DisposeContext(TContext context, Exception? exception);
}

```

##### 2.3.2 hosting application

```c#
internal class HostingApplication : IHttpApplication<HostingApplication.Context>
{
    private readonly RequestDelegate _application;
    private readonly IHttpContextFactory? _httpContextFactory;
    private readonly DefaultHttpContextFactory? _defaultHttpContextFactory;    
    private HostingApplicationDiagnostics _diagnostics;
    
    // 注入服务，
    //   - http context factory，用于创建 http context
    //   - request delegate，处理请求的最终委托
    public HostingApplication(
        RequestDelegate application,
        ILogger logger,
        DiagnosticListener diagnosticSource,
        IHttpContextFactory httpContextFactory)
    {
        _application = application;
        _diagnostics = new HostingApplicationDiagnostics(logger, diagnosticSource);
        if (httpContextFactory is DefaultHttpContextFactory factory)
        {
            _defaultHttpContextFactory = factory;
        }
        else
        {
            _httpContextFactory = httpContextFactory;
        }
    }

    /* 创建 context，即 http context 的封装 */
    // Set up the request
    public Context CreateContext(IFeatureCollection contextFeatures)
    {
        // 获取或创建 context
        Context? hostContext;
        
        if (contextFeatures is IHostContextContainer<Context> container)
        {
            hostContext = container.HostContext;
            if (hostContext is null)
            {
                hostContext = new Context();
                container.HostContext = hostContext;
            }
        }
        else
        {
            // Server doesn't support pooling, so create a new Context
            hostContext = new Context();
        }
        
        // 使用 (default) http context factory，
        //   - 创建 http context
        //   - 注入到 host context
        HttpContext httpContext;
        
        if (_defaultHttpContextFactory != null)
        {
            var defaultHttpContext = (DefaultHttpContext?)hostContext.HttpContext;
            if (defaultHttpContext is null)
            {
                httpContext = _defaultHttpContextFactory.Create(contextFeatures);
                hostContext.HttpContext = httpContext;
            }
            else
            {
                _defaultHttpContextFactory.Initialize(
                    						  defaultHttpContext, 
                    						  contextFeatures);
                httpContext = defaultHttpContext;
            }
        }
        else
        {
            httpContext = _httpContextFactory!.Create(contextFeatures);
            hostContext.HttpContext = httpContext;
        }
        
        _diagnostics.BeginRequest(httpContext, hostContext);
        return hostContext;
    }
    
    /* 处理请求，
       使用了传入的 request delegate */
    // Execute the request
    public Task ProcessRequestAsync(Context context)
    {
        return _application(context.HttpContext!);
    }
    
    /* 销毁 context */
    // Clean up the request
    public void DisposeContext(Context context, Exception? exception)
    {
        var httpContext = context.HttpContext!;
        _diagnostics.RequestEnd(httpContext, exception, context);
        
        if (_defaultHttpContextFactory != null)
        {
            _defaultHttpContextFactory.Dispose((DefaultHttpContext)httpContext);
            
            if (_defaultHttpContextFactory.HttpContextAccessor != null)
            {
                // Clear the HttpContext if the accessor was used. 
                // It's likely that the lifetime extends past the end of the http request 
                // and we want to avoid changing the reference from under consumers.
                context.HttpContext = null;
            }
        }
        else
        {
            _httpContextFactory!.Dispose(httpContext);
        }
        
        _diagnostics.ContextDisposed(context);
        
        // Reset the context as it may be pooled
        context.Reset();
    }            
}

```

###### 2.3.2.1 (host) context

```c#
internal class HostingApplication : IHttpApplication<HostingApplication.Context>
{
    internal class Context
    {
        public HttpContext? HttpContext { get; set; }
        public IDisposable? Scope { get; set; }
        public Activity? Activity { get; set; }
        internal HostingRequestStartingLog? StartLog { get; set; }
        
        public long StartTimestamp { get; set; }
        internal bool HasDiagnosticListener { get; set; }
        public bool EventLogEnabled { get; set; }
        
        public void Reset()
        {
            // Not resetting HttpContext here as we pool it on the Context
            
            Scope = null;
            Activity = null;
            StartLog = null;
            
            StartTimestamp = 0;
            HasDiagnosticListener = false;
            EventLogEnabled = false;
        }
    }
}

```

###### 2.3.2.2 (host) context container

```c#
public interface IHostContextContainer<TContext> where TContext : notnull
{    
    TContext? HostContext { get; set; }
}

```

#### 2.4 http context

##### 2.4.1 抽象基类

```c#
public abstract class HttpContext
{    
    public abstract IFeatureCollection Features { get; }   
    
    public abstract HttpRequest Request { get; }        
    public abstract HttpResponse Response { get; }        
    public abstract ConnectionInfo Connection { get; }            
    public abstract WebSocketManager WebSockets { get; }      
    
    public abstract ClaimsPrincipal User { get; set; }        
    public abstract IDictionary<object, object?> Items { get; set; }        
    public abstract IServiceProvider RequestServices { get; set; }        
    public abstract CancellationToken RequestAborted { get; set; }        
    public abstract string TraceIdentifier { get; set; }        
    public abstract ISession Session { get; set; }
        
    // 在派生类实现
    public abstract void Abort();
}

```

##### 2.4.2 default http context

```c#
public sealed class DefaultHttpContext : HttpContext
{    
    [EditorBrowsable(EditorBrowsableState.Never)]
    public HttpContext HttpContext => this;    
    
    /* only for this default implement */    
    public FormOptions FormOptions { get; set; } = default!;        
    public IServiceScopeFactory ServiceScopeFactory { get; set; } = default!;     
    
    /* 构造函数 */    
    // 构建 request、response，初始化 feature collection
    public DefaultHttpContext(IFeatureCollection features)
    {
        _features.Initalize(features);
        _request = new DefaultHttpRequest(this);
        _response = new DefaultHttpResponse(this);
    }
    // 然后，在 feature collection 中注册
    //   - request feature,
    //   - response feature,
    //   - response body feautre
    public DefaultHttpContext() : this(new FeatureCollection())
    {
        Features.Set<IHttpRequestFeature>(new HttpRequestFeature());        
        Features.Set<IHttpResponseFeature>(new HttpResponseFeature());
        Features.Set<IHttpResponseBodyFeature>(
            new StreamResponseBodyFeature(Stream.Null));
    }                
    
    // initialize
    // 初始化 basic props
    public void Initialize(IFeatureCollection features)
    {
        var revision = features.Revision;
        _features.Initalize(features, revision);
        _request.Initialize(revision);
        _response.Initialize(revision);
        _connection?.Initialize(features, revision);
        _websockets?.Initialize(features, revision);
    }
    
    // uninitialize    
    // 初始化 advanced props
    public void Uninitialize()
    {
        _features = default;
        _request.Uninitialize();
        _response.Uninitialize();
        _connection?.Uninitialize();
        _websockets?.Uninitialize();
    }                                                  
       
    public override void Abort()
    {
        LifetimeFeature.Abort();
    }                        
}

```

###### 2.4.2.1 basic props

* 在构造时配置的基本属性

```c#
public sealed class DefaultHttpContext : HttpContext
{        
    /* feature collection */
    struct FeatureInterfaces
    {
        public IItemsFeature? Items;
        public IServiceProvidersFeature? ServiceProviders;
        public IHttpAuthenticationFeature? Authentication;
        public IHttpRequestLifetimeFeature? Lifetime;
        public ISessionFeature? Session;
        public IHttpRequestIdentifierFeature? RequestIdentifier;
    }     
    
    private FeatureReferences<FeatureInterfaces> _features;        
            
    public override IFeatureCollection Features => 
        _features.Collection ?? ContextDisposed();     
    
    private static IFeatureCollection ContextDisposed()
    {
        ThrowContextDisposed();
        return null;
    }
    
    [DoesNotReturn]
    private static void ThrowContextDisposed()
    {
        throw new ObjectDisposedException(
            nameof(HttpContext), 
            $"Request has finished and {nameof(HttpContext)} disposed.");
    }
    
    /* http request */
    
    private readonly DefaultHttpRequest _request;
    public override HttpRequest Request => _request;     
    
    /* http response */
    
    private readonly DefaultHttpResponse _response;    
    public override HttpResponse Response => _response;   
    
    /* connection info */
    
    private DefaultConnectionInfo? _connection;
    public override ConnectionInfo Connection => 
        _connection ?? (_connection = new DefaultConnectionInfo(Features));     
    
    /* web socket manager */
    
    private DefaultWebSocketManager? _websockets;
    public override WebSocketManager WebSockets => 
        _websockets ?? (_websockets = new DefaultWebSocketManager(Features));  
}

```

###### 2.4.2.2 advanced props

```c#
public sealed class DefaultHttpContext : HttpContext
{
    /* claims principal user */         
    public override ClaimsPrincipal User
    {
        get
        {
            var user = HttpAuthenticationFeature.User;
            if (user == null)
            {
                user = new ClaimsPrincipal(new ClaimsIdentity());
                HttpAuthenticationFeature.User = user;
            }
            return user;
        }
        set 
        { 
            HttpAuthenticationFeature.User = value; 
        }
    }
    // 从 feature collection 解析 authentication feature
    private IHttpAuthenticationFeature HttpAuthenticationFeature =>
        _features.Fetch(
        	ref _features.Cache.Authentication, 
        	_newHttpAuthenticationFeature)!;
    // 如果没有，创建
    private readonly static Func<IFeatureCollection, IHttpAuthenticationFeature> 
        _newHttpAuthenticationFeature = f => 
        	new HttpAuthenticationFeature();
        
    /* dictionary items */      
    public override IDictionary<object, object?> Items
    {
        get 
        { 
            return ItemsFeature.Items; 
        }
        set 
        { 
            ItemsFeature.Items = value; 
        }
    }
    // 从 feature collection 解析 items feature
    private IItemsFeature ItemsFeature =>
        _features.Fetch(
        			  ref _features.Cache.Items, 
        			  _newItemsFeature)!;
    // 如果没有，创建
    private readonly static Func<IFeatureCollection, IItemsFeature> 
        _newItemsFeature = f => new ItemsFeature();
        
    /* service provider */     
    public override IServiceProvider RequestServices
    {
        get 
        { 
            return ServiceProvidersFeature.RequestServices; 
        }
        set 
        { 
            ServiceProvidersFeature.RequestServices = value; 
        }
    }    
    // 从 feature collection 解析 service provider feature
    private IServiceProvidersFeature ServiceProvidersFeature =>
        _features.Fetch(
        			  ref _features.Cache.ServiceProviders, 
        			  this, 
        			  _newServiceProvidersFeature)!;
    // 如果没有，创建
    private readonly static Func<DefaultHttpContext, IServiceProvidersFeature> 
        _newServiceProvidersFeature = context => 
        	new RequestServicesFeature(
        			context, 
	        		context.ServiceScopeFactory);
            
    /* cancellation token */
    public override CancellationToken RequestAborted
    {
        get 
        { 
            return LifetimeFeature.RequestAborted; 
        }
        set 
        { 
            LifetimeFeature.RequestAborted = value; 
        }
    }
    // 从 feature collection 解析 request lifetime feature
    private IHttpRequestLifetimeFeature LifetimeFeature =>
        _features.Fetch(
        	ref _features.Cache.Lifetime, 
        	_newHttpRequestLifetimeFeature)!;
    // 如果没有，创建
    private readonly static Func<IFeatureCollection, IHttpRequestLifetimeFeature> 
        _newHttpRequestLifetimeFeature = f => 
        	new HttpRequestLifetimeFeature();
        
    /* trace id */ 
    public override string TraceIdentifier
    {
        get 
        { 
            return RequestIdentifierFeature.TraceIdentifier; 
        }
        set 
        { 
            RequestIdentifierFeature.TraceIdentifier = value; 
        }
    }
    // 从 feature collection 解析 request identifier feature
    private IHttpRequestIdentifierFeature RequestIdentifierFeature =>
        _features.Fetch(
        	ref _features.Cache.RequestIdentifier, 
        	_newHttpRequestIdentifierFeature)!;
    // 如果没有，创建
    private readonly static Func<IFeatureCollection, IHttpRequestIdentifierFeature> 
        _newHttpRequestIdentifierFeature = f => 
        	new HttpRequestIdentifierFeature();
    
    /* session */
    public override ISession Session
    {
        get
        {
            // session or null
            var feature = SessionFeatureOrNull;
            if (feature == null)
            {
                throw new InvalidOperationException(
                    "Session has not been configured for this application " +  
                    "or request.");
            }
            return feature.Session;
        }
        set
        {
            // session
            SessionFeature.Session = value;
        }
    }           
    // 从 feature collection 解析 session feature null
    private ISessionFeature? SessionFeatureOrNull =>
        _features.Fetch(
        	ref _features.Cache.Session, 
        	_nullSessionFeature);
    // 如果没有，创建(null)
    private readonly static Func<IFeatureCollection, ISessionFeature?> 
        _nullSessionFeature = f => null;    
    // 从 feature collection 解析 session feature
    private ISessionFeature SessionFeature =>        
        _features.Fetch(
        	ref _features.Cache.Session, 
        	_newSessionFeature)!;    	                        
    // 如果没有，创建
    private readonly static Func<IFeatureCollection, ISessionFeature> 
        _newSessionFeature = f => 
        	new DefaultSessionFeature();                
}

```

###### 2.4.2.3 扩展方法 - get server variable

```c#
public static class HttpContextServerVariableExtensions
{    
    public static string? GetServerVariable(
        this HttpContext context, 
        string variableName)
    {
        var feature = context.Features
            				 .Get<IServerVariablesFeature>();
        
        if (feature == null)
        {
            return null;
        }
        
        return feature[variableName];
    }
}

```

##### 2.4.3 http context accessor

* http context 异步访问器

###### 2.4.3.1 接口

```c#
public interface IHttpContextAccessor
{        
    HttpContext? HttpContext { get; set; }
}

```

###### 2.4.3.2 http context accessor

```c#
public class HttpContextAccessor : IHttpContextAccessor
{
    private static AsyncLocal<HttpContextHolder> 
        _httpContextCurrent = new AsyncLocal<HttpContextHolder>();
    
    /// <inheritdoc/>
    public HttpContext? HttpContext
    {
        get
        {
            return  _httpContextCurrent.Value?.Context;
        }
        set
        {
            var holder = _httpContextCurrent.Value;
            if (holder != null)
            {
                // Clear current HttpContext trapped in the AsyncLocals, as its done.
                holder.Context = null;
            }
            
            if (value != null)
            {
                // Use an object indirection to hold the HttpContext in the AsyncLocal,
                // so it can be cleared in all ExecutionContexts when its cleared.
                _httpContextCurrent.Value = new HttpContextHolder 
                { 
                    Context = value 
                };
            }
        }
    }
    
    private class HttpContextHolder
    {
        public HttpContext? Context;
    }
}

```

###### 2.4.3.3 注册 http context accessor

```c#
public static class HttpServiceCollectionExtensions
{    
    public static IServiceCollection AddHttpContextAccessor(
        this IServiceCollection services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        return services;
    }
}

```

##### 2.4.4 http context factory

###### 2.4.4.1 接口

```c#
public interface IHttpContextFactory
{    
    HttpContext Create(IFeatureCollection featureCollection);        
    void Dispose(HttpContext httpContext);
}

```

###### 2.4.4.2 default http context factory

```c#
public class DefaultHttpContextFactory : IHttpContextFactory
{
    private readonly IHttpContextAccessor? _httpContextAccessor;
    internal IHttpContextAccessor? HttpContextAccessor => _httpContextAccessor;
    
    private readonly FormOptions _formOptions;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    
    
    
    // This takes the IServiceProvider because it needs to support an ever expanding
    // set of services that flow down into HttpContext features    
    public DefaultHttpContextFactory(IServiceProvider serviceProvider)
    {
        // May be null
        _httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
        _formOptions = serviceProvider.GetRequiredService<IOptions<FormOptions>>().Value;
        _serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
    }
           
    // 创建 http context
    public HttpContext Create(IFeatureCollection featureCollection)
    {
        if (featureCollection is null)
        {
            throw new ArgumentNullException(nameof(featureCollection));
        }
        
        // 构建 default http context
        var httpContext = new DefaultHttpContext(featureCollection);
        // 初始化 default http context，
        // 注入 form options 和 service scope factory
        Initialize(httpContext);
        
        return httpContext;
    }
    
    /* initial */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Initialize(
        DefaultHttpContext httpContext, 
        IFeatureCollection featureCollection)
    {
        Debug.Assert(featureCollection != null);
        Debug.Assert(httpContext != null);
        
        httpContext.Initialize(featureCollection);        
        Initialize(httpContext);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private DefaultHttpContext Initialize(
        DefaultHttpContext httpContext)
    {
        if (_httpContextAccessor != null)
        {
            _httpContextAccessor.HttpContext = httpContext;
        }
        
        httpContext.FormOptions = _formOptions;
        httpContext.ServiceScopeFactory = _serviceScopeFactory;
        
        return httpContext;
    }
    
    /* dispose */
    
    public void Dispose(HttpContext httpContext)
    {
        if (_httpContextAccessor != null)
        {
            _httpContextAccessor.HttpContext = null;
        }
    }
    
    internal void Dispose(DefaultHttpContext httpContext)
    {
        if (_httpContextAccessor != null)
        {
            _httpContextAccessor.HttpContext = null;
        }
        
        httpContext.Uninitialize();
    }
}

```

#### 2.4 application builder

* 构建请求管道

##### 2.4.1 application builder 抽象

###### 2.4.1.1 application builder 接口

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

###### 2.4.1.2 request delegate

```c#
public delegate Task RequestDelegate(HttpContext context);

```

###### 2.4.1.3 middleware 接口

```c#
public interface IMiddleware
{    
    Task InvokeAsync(HttpContext context, RequestDelegate next);
}

```

###### 2.4.1.4 middleware factory  接口

```c#
public interface IMiddlewareFactory
{    
    IMiddleware? Create(Type middlewareType);    
    void Release(IMiddleware middleware);    
}

```

###### 2.4.1.5 middleware factory

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





##### 2.4.2 application builder

```c#
public class ApplicationBuilder : IApplicationBuilder
{
    private const string ServerFeaturesKey = "server.Features";
    private const string ApplicationServicesKey = "application.Services";   
    
    // middleware 容器
    private readonly List<Func<RequestDelegate, RequestDelegate>> _components = new();
    
    /* 实现接口属性 */
    
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
    
    // properties dictionary   
    public IDictionary<string, object?> Properties { get; }
                            
    /* 构造函数 */
    
    // 注入 services provider
    public ApplicationBuilder(
        IServiceProvider serviceProvider)
    {
        Properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        ApplicationServices = serviceProvider;
    }
    // 注入 server
    public ApplicationBuilder(
        IServiceProvider serviceProvider, 
        object server)
        	: this(serviceProvider)
    {            
        // ["server.Features", server]
        SetProperty(ServerFeaturesKey, server);
    }   
    
    
    // get property
    private T? GetProperty<T>(string key)
    {
        return Properties.TryGetValue(key, out var value) 
            			 	? (T?)value 
            			 	: default(T);
    }
    
    // set property
    private void SetProperty<T>(string key, T value)
    {
        Properties[key] = value;
    }
        
    /* 实现接口的 use 方法 */
            
    /* 实现接口的 new 方法 */       
            
    /* 实现了接口的 build 方法 */        
}

```

###### 2.4.2.1 接口方法 - use

* 注入处理请求的委托

```c#
public class ApplicationBuilder : IApplicationBuilder
{    
    public IApplicationBuilder Use(
        Func<RequestDelegate, 
        RequestDelegate> middleware)
    {
        _components.Add(middleware);
        return this;
    }        
}

```



###### 2.4.2.2 接口方法 - new

* 克隆自身

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

###### 2.4.2.3 接口方法 - build

* 构建 request delegate

```c#
public class ApplicationBuilder : IApplicationBuilder
{        
    public RequestDelegate Build()
    {
        // http context -> task
        RequestDelegate app = context =>
        {
            // If we reach the end of the pipeline, but we have an endpoint, 
            // then something unexpected has happened.
            // This could happen if user code sets an endpoint, 
            // but they forgot to add the UseEndpoint middleware.
            var endpoint = context.GetEndpoint();
            var endpointRequestDelegate = endpoint?.RequestDelegate;
            if (endpointRequestDelegate != null)
            {
                var message =                        
                    $"The request reached the end of the pipeline 
                    "without executing the endpoint: '{endpoint!.DisplayName}'. " +
                    $"Please register the EndpointMiddleware using 
                    "'{nameof(IApplicationBuilder)}.UseEndpoints(...)' if using " +
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

##### 2.4.3 扩展方法 - use

###### 2.4.3.1 use(by func)

* 注册 func 形式的 middleware

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

###### 2.4.3.2 use middleware (of T)

* 通过实现了`IMiddleware`接口的`middleware`类型注册 middleware
* 实质上创了`TMiddleware`实例，然后通过`use middleware type`实现

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

###### 2.4.3.3 use middleware(of type)

* 通过`middleware type`注册 middleware

```c#
public static class UseMiddlewareExtensions
{
    internal const string InvokeMethodName = "Invoke";
    internal const string InvokeAsyncMethodName = "InvokeAsync";
    
    private static readonly MethodInfo GetServiceInfo = 
        typeof(UseMiddlewareExtensions).GetMethod(
							        		nameof(GetService), 
							        		BindingFlags.NonPublic | BindingFlags.Static)!;
    
    private static object GetService(
        					  IServiceProvider sp, 
        					  Type type, 
        					  Type middleware)
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
    
    // We're going to keep all public constructors and public methods on middleware
    private const DynamicallyAccessedMemberTypes 
        MiddlewareAccessibility = 
        	DynamicallyAccessedMemberTypes.PublicConstructors | 
        	DynamicallyAccessedMemberTypes.PublicMethods;
        
    public static IApplicationBuilder UseMiddleware(
        this IApplicationBuilder app, 
        [DynamicallyAccessedMembers(
            MiddlewareAccessibility)] Type middleware, 
        params object?[] args)
    {
        // ...
        /* 如果 middleware 实现了 IMiddleware 接口，
           使用 a- use middleware interface */           
        
        
        /* 否则，即 middleware 没有实现 IMiddleware 接口，
           通过反射创建 func（request delegate，request delegate）并 use */
        
    }
    
    /* 由 IMiddleware 接口添加 middleware 到 app builder */
            
    /* 使用反射创建 func(middleware, httpcontext, servcieprovider) */                
}

```

###### 2.4.3.4 强类型 middleware type 

* 即 middleware type 实现了`IMiddleware`接口

```c#
public static class UseMiddlewareExtensions
{
    public static IApplicationBuilder UseMiddleware(
							  	          this IApplicationBuilder app, 
								          Type middleware, 
								          params object?[] args)
    {
        if (typeof(IMiddleware).IsAssignableFrom(middleware))
        {
            /* IMiddleware 不支持参数，如果带有参数，抛出异常 */
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
    }
    
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
            // 此处返回的是 request delegate，即 func<http context, task>
            // http context -> task
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
                
                // 由 middleware factory 创建 middleware type 的实例，
                // 如果创建失败，抛出异常
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
                    // 调用 IMiddleware.Invoke，返回 task，
                    // 此处 next 是上一个 request delegate，
                    // 所有 request action 沿着 next 返回
                    await middleware.InvokeAsync(context, next);
                }
                finally
                {
                    middlewareFactory.Release(middleware);
                }
            };
        });
    }
}

```

###### 2.4.3.5 弱类型 middleware type 

* 即 middleware type 没有实现`IMiddleware`接口，通过反射创建

```c#
public static class UseMiddlewareExtensions
{
    public static IApplicationBuilder UseMiddleware(
        this IApplicationBuilder app, 
        Type middleware, 
        params object?[] args)
    {
        // 解析 service provider
        var applicationServices = app.ApplicationServices;
        
        /* 创建 func(request delegate, request delegate) 委托，
           使用 app builder 原始 use 方法注入 */
        return app.Use(next =>
        {
            /* 通过反射获取 middleware 中的 invoke 方法，
                 - 名字是 invoke 或者 invokeAsync
                 - 有且仅有1个方法
                 - 返回值必须是 task */
            
            // 反射公共方法
            var methods = middleware.GetMethods(BindingFlags.Instance | 
                                                BindingFlags.Public);
            // 过滤 invoke 或 invokeAsync 方法
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
            
            // 如果 method 不唯一，抛出异常
            if (invokeMethods.Length > 1)
            {
                throw new InvalidOperationException(
                    Resources.FormatException_UseMiddleMutlipleInvokes(
                        InvokeMethodName, 
                        InvokeAsyncMethodName));
            }
            // 如果 method 不存在，抛出异常
            if (invokeMethods.Length == 0)
            {
                throw new InvalidOperationException(
                    Resources.FormatException_UseMiddlewareNoInvokeMethod(
                        InvokeMethodName, 
                        InvokeAsyncMethodName, 
                        middleware));
            }            
            // 如果 method 的返回类型不是 task，抛出异常
            var methodInfo = invokeMethods[0];
            if (!typeof(Task).IsAssignableFrom(methodInfo.ReturnType))
            {
                    throw new InvalidOperationException(
                        Resources.FormatException_UseMiddlewareNonTaskReturnType(
                            InvokeMethodName, 
                            InvokeAsyncMethodName, 
                            nameof(Task)));
            }
            
            /* 获取创建 middleware type 实例的参数 */
            
            // 获取 method 参数
            var parameters = methodInfo.GetParameters();
            
            // 如果没有参数，
            // 或者第一个参数不是 httpContext，抛出异常
            if (parameters.Length == 0 || 
                parameters[0].ParameterType != typeof(HttpContext))
            {
                throw new InvalidOperationException(
                    Resources.FormatException_UseMiddlewareNoParameters(
                        InvokeMethodName, 
                        InvokeAsyncMethodName, 
                        nameof(HttpContext)));
            }            
            // 合并 next 和 传入的参数
            var ctorArgs = new object[args.Length + 1];
            ctorArgs[0] = next;
            Array.Copy(args, 0, ctorArgs, 1, args.Length);
            
            /* 创建 func<http context,task> 即 request delegate 并返回 */
            
            // 创建 middleware type 实例
            var instance = ActivatorUtilities.CreateInstance(
                			   app.ApplicationServices, 
                			   middleware, 
                			   ctorArgs);
            
            // 如果 method 只有一个参数，
            // 它是 terminal middleware，只有一个 http context 参数，
            // 直接执行 method 创建 request delegate */
            if (parameters.Length == 1)
            {
                return (RequestDelegate)methodInfo.CreateDelegate(
                    	   typeof(RequestDelegate), 
		                   instance);
            }
            
            // 否则，将方法格式化为 func<T,httpContext,IServiceProvider,Task> 委托            
            var factory = Compile<object>(methodInfo, parameters);
            
			// 通过 (compiled) func 创建 request delegate            
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
                                
                return factory(instance, context, serviceProvider);
            };
        });
    }        
}

```

###### 2.4.5.6 compile (middleware instance)

```c#
public static class UseMiddlewareExtensions
{
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
        //    }
        // }
        //
        
        // We'll end up with something like this:
        //   Generic version:
        //
        //   Task Invoke(
        //		Middleware instance, 
        //		HttpContext httpContext, 
        //		IServiceProvider provider)
        //   {
        //      return instance.Invoke(
        //			httpContext, 
        //			(ILoggerFactory)UseMiddlewareExtensions
        //				.GetService(provider, typeof(ILoggerFactory));
        //   }
        
        //   Non generic version:
        //
        //   Task Invoke(
        //		 object instance, 
        //		 HttpContext httpContext, 
        //		 IServiceProvider provider)
        //   {
        //      return ((Middleware)instance)
        //			.Invoke(
        //				httpContext, 
        //				(ILoggerFactory)UseMiddlewareExtensions
        //					.GetService(provider, typeof(ILoggerFactory));
        //   }
        
        var middleware = typeof(T);
        
        var httpContextArg = Expression.Parameter(
            								typeof(HttpContext), 
            								"httpContext");
        var providerArg = Expression.Parameter(
            							typeof(IServiceProvider), 
            							"serviceProvider");
        var instanceArg = Expression.Parameter(
            							middleware, 
            							"middleware");
        
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
}

```

###### 2.4.5.7 use when

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

###### 2.4.5.8 use base path

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

###### 2.4.5.9 base path middleware

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
        
        // 如果能够解析到 matched path，        
        if (context.Request
            	   .Path
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
        // 否则，即不能解析到 matched path
        else
        {
            await _next(context);
        }
    }
}

```

##### 2.4.4 扩展方法 - map

###### 2.4.4.1 map

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

###### 2.4.4.2 map options

```c#
public class MapOptions
{    
    public PathString PathMatch { get; set; }        
    public RequestDelegate? Branch { get; set; }       
    public bool PreserveMatchedPathSegment { get; set; }
}

```

###### 2.4.4.3 map middleware

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
            throw new ArgumentException(
                "Branch not set on options.", 
                nameof(options));
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
        
        // 如果能够解析到 matched path，
        if (context.Request
            	   .Path
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
        // 否则，即不能解析到 matched path
        else
        {
            await _next(context);
        }
    }
}

```

###### 2.4.4.4 map when

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
        return app.Use(next => 
                       	   new MapWhenMiddleware(next, options).Invoke);
    }
}

```

###### 2.4.4.5 map when options

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

###### 2.4.4.6 map when middleware

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
            throw new ArgumentException(
                "Predicate not set on options.", 
                nameof(options));
        }        
        if (options.Branch == null)
        {
            throw new ArgumentException(
                "Branch not set on options.", 
                nameof(options));
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

##### 2.4.5 扩展 - run

```c#
public static class RunExtensions
{    
    public static void Run(
        this IApplicationBuilder app, 
        RequestDelegate handler)
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

##### 2.4.6 application builder factory

###### 2.4.6.1 接口

```c#
public interface IApplicationBuilderFactory
{        
    IApplicationBuilder CreateBuilder(IFeatureCollection serverFeatures);
}

```

###### 2.4.6.2 application builder factory

```c#
public class ApplicationBuilderFactory : IApplicationBuilderFactory
{
    private readonly IServiceProvider _serviceProvider;        
    public ApplicationBuilderFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public IApplicationBuilder CreateBuilder(IFeatureCollection serverFeatures)
    {
        return new ApplicationBuilder(_serviceProvider, serverFeatures;
    }
}

```

### 3. routing

#### 3.1 routing context

##### 3.1.1 for route

###### 3.1.1.1 route context

```c#
public class RouteContext
{
    // http context
    public HttpContext HttpContext { get; }
    // route data
    private RouteData _routeData;
    public RouteData RouteData
    {
        get
        {
            return _routeData;
        }
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(RouteData));
            }
            
            _routeData = value;
        }
    }
    // route handler
    public RequestDelegate? Handler { get; set; }
                            
    public RouteContext(HttpContext httpContext)
    {
        HttpContext = httpContext 
            ?? throw new ArgumentNullException(nameof(httpContext));     
        RouteData = new RouteData();
    }            
}

```

###### 3.1.1.2 route data

```c#
public class RouteData
{
    // routers
    private List<IRouter>? _routers;
    public IList<IRouter> Routers
    {
        get
        {
            if (_routers == null)
            {
                _routers = new List<IRouter>();
            }
            
            return _routers;
        }
    }
    
    // data tokens
    private RouteValueDictionary? _dataTokens;
    public RouteValueDictionary DataTokens
    {
        get
        {
            if (_dataTokens == null)
            {
                _dataTokens = new RouteValueDictionary();
            }
            
            return _dataTokens;
        }
    }
    
    // values
    private RouteValueDictionary? _values;
    public RouteValueDictionary Values
    {
        get
        {
            if (_values == null)
            {
                _values = new RouteValueDictionary();
            }
            
            return _values;
        }
    }
                    
    public RouteData()
    {
        // Perf: Avoid allocating collections unless needed.
    }
            
    public RouteData(RouteData other)
    {
        if (other == null)
        {
            throw new ArgumentNullException(nameof(other));
        }
        
        // Perf: Avoid allocating collections unless we need to make a copy.
        
        if (other._routers != null)
        {
            _routers = new List<IRouter>(other.Routers);
        }        
        if (other._dataTokens != null)
        {
            _dataTokens = new RouteValueDictionary(other._dataTokens);
        }        
        if (other._values != null)
        {
            _values = new RouteValueDictionary(other._values);
        }
    }

    public RouteData(RouteValueDictionary values)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }
        
        _values = values;
    }   
    
    // push snaphsot state
    public RouteDataSnapshot PushState(
        IRouter? router, 
        RouteValueDictionary? values, 
        RouteValueDictionary? dataTokens)
    {
        /* 克隆 routers 集合 */
        // Perf: this is optimized for small list sizes, in particular to avoid overhead 
        // of a native call in Array.CopyTo inside the List(IEnumerable<T>) constructor.       
        List<IRouter>? routers = null;
        var count = _routers?.Count;
        if (count > 0)
        {
            Debug.Assert(_routers != null);
            
            routers = new List<IRouter>(count.Value);
            for (var i = 0; i < count.Value; i++)
            {
                routers.Add(_routers[i]);
            }
        }
        
        /* 将原有 route data 创建 snapshot */
        var snapshot = new RouteDataSnapshot(
            this,
            _dataTokens?.Count > 0 ? new RouteValueDictionary(_dataTokens) : null, 
            routers,
            _values?.Count > 0 ? new RouteValueDictionary(_values) : null);
        
        // 注入新的 router
        if (router != null)
        {
            Routers.Add(router);
        }
        // 注入新的 values
        if (values != null)
        {
            foreach (var kvp in values)
            {
                if (kvp.Value != null)
                {
                    Values[kvp.Key] = kvp.Value;
                }
            }
        }
        // 注入新的 data token
        if (dataTokens != null)
        {
            foreach (var kvp in dataTokens)
            {
                DataTokens[kvp.Key] = kvp.Value;
            }
        }
        
        return snapshot;
    }        
}

```

###### 3.1.1.3 route data snapshot

```c#
public class RouteData
{    
    public readonly struct RouteDataSnapshot
    {
        private readonly RouteData _routeData;
        private readonly RouteValueDictionary? _dataTokens;
        private readonly IList<IRouter>? _routers;
        private readonly RouteValueDictionary? _values;
                
        public RouteDataSnapshot(
            RouteData routeData,
            RouteValueDictionary? dataTokens,
            IList<IRouter>? routers,
            RouteValueDictionary? values)
        {
            if (routeData == null)
            {
                throw new ArgumentNullException(nameof(routeData));
            }
            
            _routeData = routeData;
            _dataTokens = dataTokens;
            _routers = routers;
            _values = values;
        }
        
        // 恢复，
        // _datatoken、_routes、_values 注入 _routedata
        public void Restore()
        {
            /* data tokens */
            
            if (_routeData._dataTokens == null && 
                _dataTokens == null)
            {
                // Do nothing
            }
            else if (_dataTokens == null)
            {
                _routeData._dataTokens!.Clear();
            }
            else
            {
                _routeData._dataTokens!.Clear();
                
                foreach (var kvp in _dataTokens)
                {
                    _routeData._dataTokens
                        	  .Add(kvp.Key, kvp.Value);
                }
            }
            
            /* routers */
            
            if (_routeData._routers == null 
                && _routers == null)
            {
                // Do nothing
            }
            else if (_routers == null)
            {
                // Perf: this is optimized for small list sizes, in particular to avoid 
                // overhead of a native call in Array.Clear inside the List.Clear() method.
                var routers = _routeData._routers!;
                for (var i = routers.Count - 1; i >= 0 ; i--)
                {
                    routers.RemoveAt(i);
                }
            }
            else
            {
                // Perf: this is optimized for small list sizes, in particular to avoid 
                // overhead of a native call in Array.Clear inside the List.Clear() method.  
                //
                // We want to basically copy the contents of _routers in
                // _routeData._routers - this change does that with the minimal number of 
                // reads/writes and without calling Clear().
                var routers = _routeData._routers!;
                var snapshotRouters = _routers;
                
                // This is made more complicated by the fact that List[int] throws if 
                // i == Count, so we have to do two loops and call Add for those cases.
                var i = 0;
                for (; i < snapshotRouters.Count && 
                     i < routers.Count; i++)
                {
                    routers[i] = snapshotRouters[i];
                }
                
                for (; i < snapshotRouters.Count; i++)
                {
                    routers.Add(snapshotRouters[i]);
                }
                
                // Trim excess - again avoiding RemoveRange because it uses native methods.
                for (i = routers.Count - 1; i >= snapshotRouters.Count; i--)
                {
                    routers.RemoveAt(i);
                }
            }
            
            /* values */
            
            if (_routeData._values == null && 
                _values == null)
            {
                // Do nothing
            }
            else if (_values == null)
            {
                _routeData._values!.Clear();
            }
            else
            {
                _routeData._values!.Clear();
                
                foreach (var kvp in _values)
                {
                    _routeData._values
                        	  .Add(kvp.Key, kvp.Value);
                }
            }
        }
    }
}

```

##### 3.1.2 for url generation

###### 3.1.2.1 virtual path context

```c#
public class VirtualPathContext
{         
    public HttpContext HttpContext { get; }        
    public string? RouteName { get; }        
    public RouteValueDictionary Values { get; set; }
    public RouteValueDictionary AmbientValues { get; }   
    
    public VirtualPathContext(
        HttpContext httpContext,
        RouteValueDictionary ambientValues,
        RouteValueDictionary values)
            : this(httpContext, ambientValues, values, null)
    {
    }
        
    public VirtualPathContext(
        HttpContext httpContext,
        RouteValueDictionary ambientValues,
        RouteValueDictionary values,
        string? routeName)
    {
        HttpContext = httpContext;
        AmbientValues = ambientValues;
        Values = values;
        RouteName = routeName;
    }               
}

```

###### 3.1.2.2 virtual path data

```c#
public class VirtualPathData
{        
    // router
    public IRouter Router { get; set; }
    
    // data tokens
    private RouteValueDictionary _dataTokens;
    public RouteValueDictionary DataTokens
    {
        get
        {
            if (_dataTokens == null)
            {
                _dataTokens = new RouteValueDictionary();
            }
            
            return _dataTokens;
        }
    }
    
    // virtual paths
    private string _virtualPath;
    public string VirtualPath
    {
        get
        {
            return _virtualPath;
        }
        set
        {
            _virtualPath = NormalizePath(value);
        }
    }    
                                    
    public VirtualPathData(
        IRouter router, 
        string virtualPath)
        	: this(router, virtualPath, dataTokens: null)
    {
    }
        
    public VirtualPathData(
        IRouter router,
        string virtualPath,
        RouteValueDictionary dataTokens)
    {
        if (router == null)
        {
            throw new ArgumentNullException(nameof(router));
        }
        
        Router = router;
        VirtualPath = virtualPath;
        _dataTokens = dataTokens == null 
            			  ? null 
            			  : new RouteValueDictionary(dataTokens);
    }     
    
    // 加上“/”
    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }
        
        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            return "/" + path;
        }
        
        return path;
    }
}

```

##### 3.1.3 in http context

###### 3.1.3.1 routing feature

```c#
public interface IRoutingFeature
{        
    RouteData? RouteData { get; set; }
}

public class RoutingFeature : IRoutingFeature
{
    /// <inheritdoc />
    public RouteData? RouteData { get; set; }
}

```

###### 3.1.3.2 route value feature?

```c#

```



###### 3.1.3.3 get routing feature from http context

```c#
public static class RoutingHttpContextExtensions
{    
    // get route data
    public static RouteData GetRouteData(this HttpContext httpContext)
    {
        if (httpContext == null)
        {
            throw new ArgumentNullException(nameof(httpContext));
        }
        
        var routingFeature = httpContext.Features.Get<IRoutingFeature>();
        return routingFeature?.RouteData ?? new RouteData(httpContext.Request.RouteValues);
    }
          
    // get route value
    public static object? GetRouteValue(this HttpContext httpContext, string key)
    {
        if (httpContext == null)
        {
            throw new ArgumentNullException(nameof(httpContext));
        }
        
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }
        
        return httpContext.Features.Get<IRouteValuesFeature>()?.RouteValues[key];
    }
}

```

#### 3.2 router routing

##### 3.2.1 router 接口

###### 3.2.1.1 router 接口

```c#
public interface IRouter
{
    // （正向）路由，从 url 路由到 (controller)
    Task RouteAsync(RouteContext context);    
    // （逆向）路由，从 (controller) 返回 url    
    VirtualPathData? GetVirtualPath(VirtualPathContext context);
}

```

###### 3.2.1.2 named router 接口

```c#
public interface INamedRouter : IRouter
{    
    string? Name { get; }
}

```

###### 3.2.1.3 route handler 接口

```c#
public interface IRouteHandler
{
    RequestDelegate GetRequestHandler(
        HttpContext httpContext, 
        RouteData routeData);
}

```

###### 3.2.1.4 route collection 接口

```c#
public interface IRouteCollection : IRouter
{    
    void Add(IRouter router);
}

```

##### 3.2.2 null router

```c#
internal class NullRouter : IRouter
{
    public static readonly NullRouter Instance = new NullRouter();
    
    private NullRouter()
    {
    }
    
    public Task RouteAsync(RouteContext context)
    {
        return Task.CompletedTask;
    }
    
    public VirtualPathData? GetVirtualPath(VirtualPathContext context)
    {
        return null;
    }        
}

```

##### 3.2.3 route

```c#
public class Route : RouteBase
{
    private readonly IRouter _target;
    
    public string? RouteTemplate => ParsedTemplate.TemplateText;
        
    public Route(
        IRouter target,
        string routeTemplate,
        IInlineConstraintResolver inlineConstraintResolver)
            : this(
                target,
                routeTemplate,
                defaults: null,
                constraints: null,
                dataTokens: null,
                inlineConstraintResolver: inlineConstraintResolver)
    {
    }
        
    public Route(
        IRouter target,
        string routeTemplate,
        RouteValueDictionary? defaults,
        IDictionary<string, object>? constraints,
        RouteValueDictionary? dataTokens,
        IInlineConstraintResolver inlineConstraintResolver)
            : this(
                target, 
                null, 
                routeTemplate, 
                defaults, 
                constraints, 
                dataTokens, 
                inlineConstraintResolver)
    {
    }
        
    public Route(
        IRouter target,
        string? routeName,
        string? routeTemplate,
        RouteValueDictionary? defaults,
        IDictionary<string, object>? constraints,
        RouteValueDictionary? dataTokens,
        IInlineConstraintResolver inlineConstraintResolver)
            : base(
                  routeTemplate,
                  routeName,
                  inlineConstraintResolver,
                  defaults,
                  constraints,
                  dataTokens)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        _target = target;
    }
        
    /// <inheritdoc />
    protected override Task OnRouteMatched(RouteContext context)
    {
        context.RouteData
               .Routers
               .Add(_target);
        
        return _target.RouteAsync(context);
    }
    
    /// <inheritdoc />
    protected override VirtualPathData? OnVirtualPathGenerated(VirtualPathContext context)
    {
        return _target.GetVirtualPath(context);
    }
}

```

###### 3.2.3.1 route base 抽象基类

```c#
public abstract class RouteBase : IRouter, INamedRouter
{
    private readonly object _loggersLock = new object();
    
    // 匹配 request -> template
    private TemplateMatcher? _matcher;
    // 匹配 template -> virtual path
    private TemplateBinder? _binder;
    
    private ILogger? _logger;
    private ILogger? _constraintLogger;
           
    // name
    public virtual string? Name { get; protected set; }
    
    public virtual IDictionary<string, IRouteConstraint> Constraints { get; protected set; } 
    protected virtual IInlineConstraintResolver ConstraintResolver { get; set; }
    
    public virtual RouteValueDictionary DataTokens { get; protected set; }        
    public virtual RouteValueDictionary Defaults { get; protected set; }
           
    public virtual RouteTemplate ParsedTemplate { get; protected set; }
    
    /* 构造函数 */    
    public RouteBase(
        string? template,
        string? name,
        IInlineConstraintResolver constraintResolver,
        RouteValueDictionary? defaults,
        IDictionary<string, object>? constraints,
        RouteValueDictionary? dataTokens)
    {       
        if (constraintResolver == null)
        {
            throw new ArgumentNullException(nameof(constraintResolver));
        }
        
        // 注入 template string，如果是 null，转为 string.empty
        template = template ?? string.Empty;
        
        Name = name;                
        ConstraintResolver = constraintResolver;                
        DataTokens = dataTokens ?? new RouteValueDictionary();
        
        try
        {
            /* 1- 解析 route template，
               	  使用 template parser 从 template string 解析 route template */
            // Data we parse from the template will be used 
            // to fill in the rest of the constraints or defaults. 
            // The parser will throw for invalid routes.
            ParsedTemplate = TemplateParser.Parse(template);
            
            /* 2- 解析 route constraint，
                  使用 inline constraint resolver 从 route template 解析 route constraint */   
            Constraints = GetConstraints(
                			  constraintResolver, 
			                  ParsedTemplate, 
              				  constraints);
            
            /* 3- 获取 parameter 的 default value， 
            	  从 route template 解析 default value */            	  
            Defaults = GetDefaults(ParsedTemplate, defaults);
            
        }
        catch (Exception exception)
        {
            throw new RouteCreationException(
                Resources.FormatTemplateRoute_Exception(name, template), 
                exception);
        }
    }
                 
    /// <inheritdoc />
    public override string ToString()
    {
        return ParsedTemplate.TemplateText!;
    }
    
    [MemberNotNull(nameof(_logger), nameof(_constraintLogger))]
    private void EnsureLoggers(HttpContext context)
    {
        // We check first using the _logger to see 
        // if the loggers have been initialized to avoid taking
        // the lock on the most common case.
        if (_logger == null)
        {
            // We need to lock here to ensure that _constraintLogger 
            // and _logger get initialized atomically.
            lock (_loggersLock)
            {
                if (_logger != null)
                {
                    // Multiple threads might have tried to acquire 
                    // the lock at the same time. 
                    // Technically there is nothing wrong if things 
                    // get reinitialized by a second thread, 
                    // but its easy to prevent by just rechecking and returning here.
                    Debug.Assert(_constraintLogger != null);
                    
                    return;
                }
                
                // 解析 logger factory
                var factory = context.RequestServices
                    				 .GetRequiredService<ILoggerFactory>();
                
                // 创建 constraint logger
                _constraintLogger = 
                    factory.CreateLogger(typeof(RouteConstraintMatcher).FullName!);
                
                // 创建 logger
                _logger = factory.CreateLogger(typeof(RouteBase).FullName!);
            }            
        }
        
        Debug.Assert(_constraintLogger != null);
    }
}

```

###### 3.2.3.2 get constraint

```c#
public abstract class RouteBase : IRouter, INamedRouter
{
    protected static IDictionary<string, IRouteConstraint> GetConstraints(
        IInlineConstraintResolver inlineConstraintResolver,
        RouteTemplate parsedTemplate,
        IDictionary<string, object>? constraints)
    {
        // 创建 route constraint builder
        var constraintBuilder = new RouteConstraintBuilder(
            inlineConstraintResolver, 
            parsedTemplate.TemplateText!);
        
        // 将（传入的） constraints 注入constraint builder
        if (constraints != null)
        {
            foreach (var kvp in constraints)                
            {
                constraintBuilder.AddConstraint(kvp.Key, kvp.Value);
            }
        }
                
        // 遍历（传入的）route template 的 parameter part，
        foreach (var parameter in parsedTemplate.Parameters)
        {
            // 如果 parameter part 是 optional，标记
            if (parameter.IsOptional)
            {
                constraintBuilder.SetOptional(parameter.Name!);
            }
            // 遍历 parameter part 所有 inline constraint， 注入 constraint builder
            foreach (var inlineConstraint in parameter.InlineConstraints)
            {
                constraintBuilder.AddResolvedConstraint(
                    parameter.Name!, 
                    inlineConstraint.Constraint);
            }
        }
        
        // 构建 constraints dictionary
        return constraintBuilder.Build();
    }
}
```

###### 3.2.3.3 get defaults

```c#
public abstract class RouteBase : IRouter, INamedRouter
{
     protected static RouteValueDictionary GetDefaults(
        RouteTemplate parsedTemplate,
        RouteValueDictionary? defaults)
    {
        // 预结果，创建或者克隆（传入的）defaults
        var result = defaults == null 
            		 	? new RouteValueDictionary() 
			            : new RouteValueDictionary(defaults);
        
        // 遍历（传入的）route template 的 parameter part
        foreach (var parameter in parsedTemplate.Parameters)
        {
            // 如果 parameter part 的 default value 不为 null，注入 result
            if (parameter.DefaultValue != null)
            {
#if RVD_TryAdd
    			if (!result.TryAdd(
                    	parameter.Name, 
                    	parameter.DefaultValue))
                {
                    throw new InvalidOperationException(
                        Resources.FormatTemplateRoute
		                          _CannotHaveDefaultValueSpecifiedInlineAndExplicitly(
                                      parameter.Name));
                }
#else
                if (result.ContainsKey(parameter.Name!))
                {
                    throw new InvalidOperationException(
                        Resources.FormatTemplateRoute
                        		 _CannotHaveDefaultValueSpecifiedInlineAndExplicitly(
                                     parameter.Name));
                }
                else
                {
                    result.Add(
                        	   parameter.Name!, 
		                       parameter.DefaultValue);
                }
#endif
            }
        }
                
        return result;
    }                                
}
```

###### 3.2.3.4 接口方法 - route async 

```c#
public abstract class RouteBase : IRouter, INamedRouter
{
    public virtual Task RouteAsync(RouteContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        // a- 确认 template matcher 不为 null（创建）
        EnsureMatcher();
        
        // 确认 logger 不为 null       
        EnsureLoggers(context.HttpContext);
                        
        // 从 http context 中解析 request path，
        var requestPath = context.HttpContext.Request.Path;     
        
        // 使用 template matche 验证 request path，不匹配 -> 结束
        if (!_matcher.TryMatch(
            	requestPath, 
            	context.RouteData.Values))
        {
            // If we got back a null value set, that means the URI did not match
            return Task.CompletedTask;
        }
        
        // b- 合并（额外的）data token
        // Perf: Avoid accessing dictionaries if you don't need to write to them, 
        // these dictionaries are all created lazily.
        if (DataTokens.Count > 0)
        {
            MergeValues(
                context.RouteData.DataTokens, 
                DataTokens);
        }
                
        // 使用 constraint matcher 验证，如果不匹配 -> 结束
        if (!RouteConstraintMatcher.Match(
	            Constraints,        
    	        context.RouteData.Values,
        	    context.HttpContext,
            	this,
            	RouteDirection.IncomingRequest,
            	_constraintLogger))
        {
            return Task.CompletedTask;
        }
        
        /* request path（http context 解析得到）匹配 route template 和  route constrain，*/
        
        // 记录日志
        _logger.RequestMatchedRoute(Name!, ParsedTemplate.TemplateText!);        
        // 触发 on route matched 钩子
        return OnRouteMatched(context);
    }
    
    // a- 由 route template（解析得到）创建 template matcher
    [MemberNotNull(nameof(_matcher))]
    private void EnsureMatcher()
    {
        if (_matcher == null)
        {
            _matcher = new TemplateMatcher(ParsedTemplate, Defaults);
        }
    }
    
    // b- 合并传入的 data token
    private static void MergeValues(
        RouteValueDictionary destination,
        RouteValueDictionary values)
    {
        foreach (var kvp in values)
        {
            // This will replace the original value for the specified key.
            // Values from the matched route will take preference over previous
            // data in the route context.
            destination[kvp.Key] = kvp.Value;
        }
    }
    
    // on route matched 钩子
    protected abstract Task OnRouteMatched(RouteContext context);        
}

```

###### 3.2.3.5 接口方法 - get virtual path

```c#
public abstract class RouteBase : IRouter, INamedRouter
{
    public virtual VirtualPathData? GetVirtualPath(VirtualPathContext context)
    {
        // a- 确认 template binder 不为 null（创建）
        EnsureBinder(context.HttpContext);
        
        // 确认 logger 不为 null
        EnsureLoggers(context.HttpContext);
        
        // 使用 template binder 解析 template value result，
        var values = _binder.GetValues(
            					context.AmbientValues, 
					            context.Values);
        // 不能解析，返回 null
        if (values == null)
        {
            // We're missing one of the required values for this route.
            return null;
        }
                        
        // 使用 constraint matcher 验证 template value result 的 combined values，
        // 如果不匹配，返回 null
        if (!RouteConstraintMatcher.Match(
            	Constraints,
	            values.CombinedValues,
    	        context.HttpContext,
        	    this,
            	RouteDirection.UrlGeneration,
            	_constraintLogger))
        {
            return null;
        }
                
        // （通过 constraint 验证），
        // 将 template value result 的 combined values 注入到 virtual path context
        context.Values = values.CombinedValues;   
        
        // b- 使用 on virtual path generated 钩子创建 virtual path data
        var pathData = OnVirtualPathGenerated(context);
        // 如果创建成功，返回结果
        if (pathData != null)
        {
            // If the target generates a value then that can short circuit.
            return pathData;
        }
        
        /* 不能由 on virtual path generated 钩子创建 virtual path data，*/
        
        // 使用 template binder 将 template value result 绑定到 virtual path string
        var virtualPath = _binder.BindValues(values.AcceptedValues);
        // 如果不能绑定，返回 null
        if (virtualPath == null)
        {
            return null;
        }
        // 由 virtual path string 创建 virtual path data        
        pathData = new VirtualPathData(this, virtualPath);
        
        // 注入传入的 data token
        if (DataTokens != null)
        {
            foreach (var dataToken in DataTokens)
            {
                pathData.DataTokens
                    	.Add(dataToken.Key, dataToken.Value);
            }
        }
        
        return pathData;
    }
    
    // a- 使用 template binder factory（从 service provider 中解析），
    //	  由 route template（解析得到）创建 template binder    
    [MemberNotNull(nameof(_binder))]
    private void EnsureBinder(HttpContext context)
    {
        if (_binder == null)
        {
            var binderFactory = context
                .RequestServices
                .GetRequiredService<TemplateBinderFactory>();
            
            _binder = binderFactory.Create(ParsedTemplate, Defaults);
        }
    }
    
    // b- virtual path generated 钩子，由派生类实现
    protected abstract VirtualPathData? 
        OnVirtualPathGenerated(VirtualPathContext context);
}

```

##### 3.2.4 route handler

###### 3.2.4.1 接口

```c#

```

###### 3.2.4.2 route handler

```c#
public class RouteHandler : IRouteHandler, IRouter
{
    private readonly RequestDelegate _requestDelegate;        
    public RouteHandler(RequestDelegate requestDelegate)
    {
        _requestDelegate = requestDelegate;
    }
    
    /// <inheritdoc />
    public RequestDelegate GetRequestHandler(
        HttpContext httpContext, 
        RouteData routeData)
    {
        return _requestDelegate;
    }
    
    /// <inheritdoc />
    public VirtualPathData? GetVirtualPath(VirtualPathContext context)
    {
        // Nothing to do.
        return null;
    }
    
    /// <inheritdoc />
    public Task RouteAsync(RouteContext context)
    {
        context.Handler = _requestDelegate;
        return Task.CompletedTask;
    }
}

```

##### 3.2.5 router collection

###### 3.2.5.1 接口

```c#

```

###### 3.2.5.2 router collection

```c#
public class RouteCollection : IRouteCollection
{
    private readonly static char[] UrlQueryDelimiters = new char[] { '?', '#' };
    
    private readonly List<IRouter> _routes = new List<IRouter>();    
    private readonly List<IRouter> _unnamedRoutes = new List<IRouter>();
    private readonly Dictionary<string, INamedRouter> _namedRoutes =
        new Dictionary<string, INamedRouter>(StringComparer.OrdinalIgnoreCase);
    
    private RouteOptions? _options;
                           
    public int Count
    {
        get { return _routes.Count; }
    }
    
    /* add router */    
    public void Add(IRouter router)
    {
        if (router == null)
        {
            throw new ArgumentNullException(nameof(router));
        }
        
        // 如果 route 实现了 named router 接口，
        var namedRouter = router as INamedRouter;
        if (namedRouter != null)
        {
            if (!string.IsNullOrEmpty(namedRouter.Name))
            {
                // 注入 named route 集合
                _namedRoutes.Add(namedRouter.Name, namedRouter);
            }
        }
        // 否则，即没有实现 named router 接口，
        else
        {
            // 注入 unamed route 集合
            _unnamedRoutes.Add(router);
        }
        
        // 同时注入 routes 集合（无论是否实现 named router 接口）        
        _routes.Add(router);
    }
    
    public IRouter this[int index]
    {
        get { return _routes[index]; }
    }                
    
    // 解析 route options
    [MemberNotNull(nameof(_options))]
    private void EnsureOptions(HttpContext context)
    {
        if (_options == null)
        {
            _options = context.RequestServices
                			  .GetRequiredService<IOptions<RouteOptions>>()
                			  .Value;
        }
    }                
}

```

###### 3.2.5.1 接口方法 - route async

```c#
public class RouteCollection : IRouteCollection
{    
    public async virtual Task RouteAsync(RouteContext context)
    {
        // Perf: We want to avoid allocating a new RouteData for each route we 
        // need to process.
        // We can do this by snapshotting the state at the beginning and then restoring 
        // it for each router we execute.
        var snapshot = context.RouteData.PushState(null, values: null, dataTokens: null);
        
        // 遍历 route 集合，
        for (var i = 0; i < Count; i++)
        {
            // 将 route 注入 route context
            var route = this[i];
            context.RouteData
                   .Routers
                   .Add(route);
            
            try
            {
                // 执行 route 的 route async 方法
                await route.RouteAsync(context);                
                // 如果执行结果不为 null，结束
                if (context.Handler != null)
                {
                    break;
                }
            }
            finally
            {
                if (context.Handler == null)
                {
                    snapshot.Restore();
                }
            }
        }
    }
}

```

###### 3.2.5.2 接口方法 - get virtual path

```c#
public class RouteCollection : IRouteCollection
{
    /// <inheritdoc />
    public virtual VirtualPathData? GetVirtualPath(VirtualPathContext context)
    {
        // 解析 route options 
        EnsureOptions(context.HttpContext);
        
        /* 如果 virtual path context 中 route name 不为空  */
        if (!string.IsNullOrEmpty(context.RouteName))
        {
            // 预结果
            VirtualPathData? namedRoutePathData = null;
            
            // 从 named route 集合中找到匹配的 router，
            if (_namedRoutes.TryGetValue(
                				context.RouteName, 
                				out var matchedNamedRoute))
            {
                // 如果能找到，使用 route 解析 virtual path data
                namedRoutePathData = matchedNamedRoute.GetVirtualPath(context);
            }
            
            // a- 从 unamed route 集合中解析 virtual path data
            var pathData = GetVirtualPath(context, _unnamedRoutes);
                        
            // If the named route and one of the unnamed routes also matches, 
            // then we have an ambiguity.
            
            // 如果都能解析到 virtual path data，抛出异常
            if (namedRoutePathData != null && 
                pathData != null)
            {
                var message = Resources.FormatNamedRoutes
                    					_AmbiguousRoutesFound(context.RouteName);
                throw new InvalidOperationException(message);
            }
            
            /* b- 由 named route data 或者 unamed route data 创建 virtual path data */
            return NormalizeVirtualPath(namedRoutePathData ?? pathData);
        }
        /* 否则，即 route name 为空 */
        else
        {
            // a- & b-
            return NormalizeVirtualPath(GetVirtualPath(context, _routes));
        }
    }
        
    // a- 从 route 集合中 get virtual path
    private VirtualPathData? GetVirtualPath(
        VirtualPathContext context, 
        List<IRouter> routes)
    {
        // 遍历 route 集合解析 virtual path data，        
        for (var i = 0; i < routes.Count; i++)
        {
            var route = routes[i];
            
            var pathData = route.GetVirtualPath(context);
            if (pathData != null)
            {
                // 只要找到（第一个），返回结果
                return pathData;
            }
        }
        
        // 找不到，返回 null
        return null;
    }
    
    // b- 标准化 virtual path data
    private VirtualPathData? NormalizeVirtualPath(VirtualPathData? pathData)
    {
        if (pathData == null)
        {
            return pathData;
        }
        
        Debug.Assert(_options != null);
        
        var url = pathData.VirtualPath;
        
        if (!string.IsNullOrEmpty(url) && 
            (_options.LowercaseUrls || _options.AppendTrailingSlash))
        {
            var indexOfSeparator = url.IndexOfAny(UrlQueryDelimiters);
            var urlWithoutQueryString = url;
            var queryString = string.Empty;
            
            if (indexOfSeparator != -1)
            {
                urlWithoutQueryString = url.Substring(0, indexOfSeparator);
                queryString = url.Substring(indexOfSeparator);
            }
            
            if (_options.LowercaseUrls)
            {
                urlWithoutQueryString = urlWithoutQueryString.ToLowerInvariant();
            }
            
            if (_options.LowercaseUrls && 
                _options.LowercaseQueryStrings)
            {
                queryString = queryString.ToLowerInvariant();
            }
            
            if (_options.AppendTrailingSlash && 
                !urlWithoutQueryString.EndsWith("/", StringComparison.Ordinal))
            {
                urlWithoutQueryString += "/";
            }
            
            // queryString will contain the delimiter ? or # as the first character, 
            // so it's safe to append.
            url = urlWithoutQueryString + queryString;
            
            return new VirtualPathData(pathData.Router, url, pathData.DataTokens);
        }
        
        return pathData;
    }        
}

```

##### 3.2.6 route builder

###### 3.2.6.1 接口

```c#
public interface IRouteBuilder
{    
    IApplicationBuilder ApplicationBuilder { get; }        
    IRouter? DefaultHandler { get; set; }    
    IServiceProvider ServiceProvider { get; }            
    IList<IRouter> Routes { get; }
        
    IRouter Build();
}

```

###### 3.2.6.2 route builder

```c#
public class RouteBuilder : IRouteBuilder
{
    public IApplicationBuilder ApplicationBuilder { get; }      
    public IRouter? DefaultHandler { get; set; }       
    public IServiceProvider ServiceProvider { get; }       
    public IList<IRouter> Routes { get; }
            
    public RouteBuilder(IApplicationBuilder applicationBuilder)
        : this(
            applicationBuilder, 
            defaultHandler: null)
    {
    }
        
    public RouteBuilder(
        IApplicationBuilder applicationBuilder, 
        IRouter? defaultHandler)
    {
        if (applicationBuilder == null)
        {
            throw new ArgumentNullException(nameof(applicationBuilder));
        }
        
        // 如果 app builder 中没有注入 routing marker service，抛出异常        
        if (applicationBuilder.ApplicationServices
            				  .GetService(typeof(RoutingMarkerService)) == null)
        {
            throw new InvalidOperationException(
                Resources.FormatUnableToFindServices(
                    nameof(IServiceCollection),
                    nameof(RoutingServiceCollectionExtensions.AddRouting),
                    "ConfigureServices(...)"));
        }
        
        ApplicationBuilder = applicationBuilder;
        DefaultHandler = defaultHandler;
        ServiceProvider = applicationBuilder.ApplicationServices;
        
        // 创建 route 集合（默认）
        Routes = new List<IRouter>();
    }
        
    // 返回新的 route collection 实例
    public IRouter Build()
    {
        var routeCollection = new RouteCollection();
        
        foreach (var route in Routes)
        {
            routeCollection.Add(route);
        }
        
        return routeCollection;
    }
}

```

###### 3.2.6.3 扩展方法 - map route

```c#
public static class MapRouteRouteBuilderExtensions
{    
    public static IRouteBuilder MapRoute(
        this IRouteBuilder routeBuilder,
        string? name,
        string? template)
    {
        return MapRoute(
            routeBuilder, 
            name, 
            template, 
            defaults: null);                
    }
        
    public static IRouteBuilder MapRoute(
        this IRouteBuilder routeBuilder,
        string? name,
        string? template,
        object? defaults)
    {
        return MapRoute(
            routeBuilder, 
            name, 
            template, 
            defaults, 
            constraints: null);
    }
            
    public static IRouteBuilder MapRoute(
        this IRouteBuilder routeBuilder,
        string? name,
        string? template,
        object? defaults,
        object? constraints)
    {
        return MapRoute(
            routeBuilder, 
            name, 
            template, 
            defaults, 
            constraints, 
            dataTokens: null);
    }
    
    // 向 route builder 中注入 route
    public static IRouteBuilder MapRoute(
        this IRouteBuilder routeBuilder,
        string? name,
        string? template,
        object? defaults,
        object? constraints,
        object? dataTokens)
    {
        if (routeBuilder.DefaultHandler == null)
        {
            throw new RouteCreationException(
                Resources.FormatDefaultHandler_MustBeSet(nameof(IRouteBuilder)));
        }
        
        routeBuilder.Routes
            		.Add(
            			new Route(
                            routeBuilder.DefaultHandler,
                            name,
                            template,
                            new RouteValueDictionary(defaults),
                            new RouteValueDictionary(constraints)!,
                            new RouteValueDictionary(dataTokens),
                            CreateInlineConstraintResolver(routeBuilder.ServiceProvider)));
        
        return routeBuilder;
    }      
    
    /* create inline constraint resolver */
    
    private static IInlineConstraintResolver CreateInlineConstraintResolver(
        IServiceProvider serviceProvider)
    {
        var inlineConstraintResolver = 
            serviceProvider.GetRequiredService<IInlineConstraintResolver>();
        
        var parameterPolicyFactory = 
            serviceProvider.GetRequiredService<ParameterPolicyFactory>();
        
        // This inline constraint resolver will return a null constraint for 
        // non-IRouteConstraint parameter policies so Route does not error
        return new BackCompatInlineConstraintResolver(
            inlineConstraintResolver, 
            parameterPolicyFactory);
    }
    
    private class BackCompatInlineConstraintResolver : IInlineConstraintResolver
    {
        private readonly IInlineConstraintResolver _inner;
        private readonly ParameterPolicyFactory _parameterPolicyFactory;
        
        public BackCompatInlineConstraintResolver(
            IInlineConstraintResolver inner, 
            ParameterPolicyFactory parameterPolicyFactory)
        {
            _inner = inner;
            _parameterPolicyFactory = parameterPolicyFactory;
        }
        
        public IRouteConstraint? ResolveConstraint(string inlineConstraint)
        {
            // 使用 inner constraint resolver 解析 constraint，            
            var routeConstraint = _inner.ResolveConstraint(inlineConstraint);
            // 如果能解析到，返回 constraint
            if (routeConstraint != null)
            {
                return routeConstraint;
            }
            
            // 否则，即 inner constraint resolver 不能解析，
            // 由 parameter policy factory 创建 constraint，
            var parameterPolicy = _parameterPolicyFactory.Create(null!, inlineConstraint);
            // 如果能创建，返回 constraint
            if (parameterPolicy != null)
            {
                // Logic inside Route will skip adding NullRouteConstraint
                return NullRouteConstraint.Instance;
            }
            
            // 都不能，返回 null
            return null;
        }
    }
}

```

###### 3.2.6.4 扩展方法 - map route with handler

```c#
public static class RequestDelegateRouteBuilderExtensions
{    
    public static IRouteBuilder MapRoute(
        this IRouteBuilder builder, 
        string template, 
        RequestDelegate handler)
    {
        var route = new Route(
            new RouteHandler(handler),
            template,
            defaults: null,
            constraints: null,
            dataTokens: null,
            inlineConstraintResolver: GetConstraintResolver(builder));
        
        builder.Routes
	           .Add(route);
        
        return builder;
    }
    
    public static IRouteBuilder MapMiddlewareRoute(
        this IRouteBuilder builder, 
        string template, 
        Action<IApplicationBuilder> action)
    {
        var nested = builder.ApplicationBuilder
            				.New();
        action(nested);
        
        return builder.MapRoute(template, nested.Build());
    }                           
}

```

###### 3.2.6.5 扩展方法 - map verb with handler (request delegate)

* 封装 request delegate 为 route handler，作为参数创建 route，然后注入 builder

```c#
public static class RequestDelegateRouteBuilderExtensions
{
    private static IInlineConstraintResolver GetConstraintResolver(IRouteBuilder builder)
    {
        return builder.ServiceProvider
            		  .GetRequiredService<IInlineConstraintResolver>();
    }
    
    /* map verb */        
    public static IRouteBuilder MapVerb(
        this IRouteBuilder builder,
        string verb,
        string template,
        RequestDelegate handler)
    {
        var route = new Route(
            new RouteHandler(handler),
            template,
            defaults: null,
            constraints: 
            	new RouteValueDictionary(
                    new 
                    { 
                        httpMethod = new HttpMethodRouteConstraint(verb) 
                    })!,
            dataTokens: null,
            inlineConstraintResolver: GetConstraintResolver(builder));
        
        builder.Routes
               .Add(route);
         
        return builder;
    }
                        
    /* map get */
    public static IRouteBuilder MapGet(
        this IRouteBuilder builder, 
        string template, 
        RequestDelegate handler)
    {
        return builder.MapVerb("GET", template, handler);
    }
                            
    /* map post */
    public static IRouteBuilder MapPost(
        this IRouteBuilder builder, 
        string template, 
        RequestDelegate handler)
    {
        return builder.MapVerb("POST", template, handler);
    }
                        
    /* map put */
    public static IRouteBuilder MapPut(
        this IRouteBuilder builder, 
        string template, 
        RequestDelegate handler)
    {
        return builder.MapVerb("PUT", template, handler);
    }
                   
    /* map delete */
    public static IRouteBuilder MapDelete(
        this IRouteBuilder builder, 
        string template, 
        RequestDelegate handler)
    {
        return builder.MapVerb("DELETE", template, handler);
    }                   
}

```

###### 3.2.6.6 扩展方法 - map verb with handler (func)

```c#
public static class RequestDelegateRouteBuilderExtensions
{
    private static IInlineConstraintResolver GetConstraintResolver(IRouteBuilder builder)
    {
        return builder.ServiceProvider
            		  .GetRequiredService<IInlineConstraintResolver>();
    }
    
    /* map verb */
    public static IRouteBuilder MapVerb(
        this IRouteBuilder builder,
        string verb,
        string template,
        Func<HttpRequest, HttpResponse, RouteData, Task> handler)
    {
        RequestDelegate requestDelegate = 
            (httpContext) =>
        		{
            		return handler(
                    		   httpContext.Request, 
                    		   httpContext.Response, 
                    	   	   httpContext.GetRouteData());
        		};
        
        return builder.MapVerb(verb, template, requestDelegate);
    }   
                
    /* map get */   
    public static IRouteBuilder MapGet(
        this IRouteBuilder builder,
        string template,
        Func<HttpRequest, HttpResponse, RouteData, Task> handler)
    {
        return builder.MapVerb("GET", template, handler);
    }
    
    /* map post */    
    public static IRouteBuilder MapPost(
        this IRouteBuilder builder,
        string template,
        Func<HttpRequest, HttpResponse, RouteData, Task> handler)
    {
        return builder.MapVerb("POST", template, handler);
    }
    
    /* map put */    
    public static IRouteBuilder MapPut(
        this IRouteBuilder builder,
        string template,
        Func<HttpRequest, HttpResponse, RouteData, Task> handler)
    {
        return builder.MapVerb("PUT", template, handler);
    }
    
    /* map delete */    
    public static IRouteBuilder MapDelete(
        this IRouteBuilder builder,
        string template,
        Func<HttpRequest, HttpResponse, RouteData, Task> handler)
    {
        return builder.MapVerb("DELETE", template, handler);
    }                                              
}

```

###### 3.2.6.7 map verb with middleware (application builder)

* 以 application builder 的方式提供 request delegate

```c#
public static class RequestDelegateRouteBuilderExtensions
{
    public static IRouteBuilder MapMiddlewareVerb(
        this IRouteBuilder builder,
        string verb,
        string template,
        Action<IApplicationBuilder> action)
    {
        var nested = builder.ApplicationBuilder
            				.New();
        action(nested);
        return builder.MapVerb(verb, template, nested.Build());
    }                                           
       
    // get
    public static IRouteBuilder MapMiddlewareGet(
        this IRouteBuilder builder, 
        string template, 
        Action<IApplicationBuilder> action)
    {
        return builder.MapMiddlewareVerb("GET", template, action);
    }
        
    // post
    public static IRouteBuilder MapMiddlewarePost(
        this IRouteBuilder builder, 
        string template, 
        Action<IApplicationBuilder> action)
    {
        return builder.MapMiddlewareVerb("POST", template, action);
    }
       
    // put
    public static IRouteBuilder MapMiddlewarePut(
        this IRouteBuilder builder, 
        string template, 
        Action<IApplicationBuilder> action)
    {
        return builder.MapMiddlewareVerb("PUT", template, action);
    }

    // delete
    public static IRouteBuilder MapMiddlewareDelete(
        this IRouteBuilder builder, 
        string template, 
        Action<IApplicationBuilder> action)
    {
        return builder.MapMiddlewareVerb("DELETE", template, action);
    }                                          
}

```

#### 3.3 tree routing

##### 3.3.a tree router

###### 3.3.a.1 tree router

```c#
public class TreeRouter : IRouter
{
    /// <summary>
    /// Key used by routing and action selection to match an attribute
    /// route entry to a group of action descriptors.
    /// </summary>
    public static readonly string RouteGroupKey = "!__route_group";
    
    private readonly UrlMatchingTree[] _trees;
     internal IEnumerable<UrlMatchingTree> MatchingTrees => _trees;
    
    private readonly LinkGenerationDecisionTree _linkGenerationTree;    
    private readonly IDictionary<string, OutboundMatch> _namedEntries;
            
    private readonly ILogger _logger;
    private readonly ILogger _constraintLogger;
    
    public int Version { get; }    
   
        
    internal TreeRouter(
        UrlMatchingTree[] trees,
        IEnumerable<OutboundRouteEntry> linkGenerationEntries,
        UrlEncoder urlEncoder,
        ObjectPool<UriBuildingContext> objectPool,
        ILogger routeLogger,
        ILogger constraintLogger,
        int version)
    {
        if (trees == null)
        {
            throw new ArgumentNullException(nameof(trees));
        }        
        if (linkGenerationEntries == null)
        {
            throw new ArgumentNullException(nameof(linkGenerationEntries));
        }        
        if (urlEncoder == null)
        {
            throw new ArgumentNullException(nameof(urlEncoder));
        }        
        if (objectPool == null)
        {
            throw new ArgumentNullException(nameof(objectPool));
        }        
        if (routeLogger == null)
        {
            throw new ArgumentNullException(nameof(routeLogger));
        }        
        if (constraintLogger == null)
        {
            throw new ArgumentNullException(nameof(constraintLogger));
        }
        
        // 注入服务
        _trees = trees;
        _logger = routeLogger;
        _constraintLogger = constraintLogger;
        
        // 创建 dict<strig,outbound match>（默认值，empty）
        _namedEntries = 
            new Dictionary<string, OutboundMatch>(StringComparer.OrdinalIgnoreCase);

        var outboundMatches = new List<OutboundMatch>();
        
        // 遍历 传入 outbound route entry 集合
        foreach (var entry in linkGenerationEntries)
        {
            /* 封装 outbound route entry 为 outbound match，注入 outbound matches  */
            var binder = new TemplateBinder(
                urlEncoder, 
                objectPool, 
                entry.RouteTemplate, 
                entry.Defaults);
            
            var outboundMatch = 
                new OutboundMatch() 
            	{
                	Entry = entry, 
                	TemplateBinder = binder 
            	};
            
            outboundMatches.Add(outboundMatch);
            
            /* 如果 outbound route entry 重名，抛出异常，
               否则 注入 named entries */
            // Skip unnamed entries
            if (entry.RouteName == null)
            {
                continue;
            }
            
            // We only need to keep one OutboundMatch per route template
            // so in case two entries have the same name and the same template we only keep
            // the first entry.
            if (_namedEntries.TryGetValue(
                	entry.RouteName, 
                	out var namedMatch) &&
                !string.Equals(
                    namedMatch.Entry.RouteTemplate.TemplateText,
                    entry.RouteTemplate.TemplateText,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    Resources.FormatAttributeRoute
                    		 _DifferentLinkGenerationEntries
                    		 _SameName(entry.RouteName),
                    nameof(linkGenerationEntries));
            }
            else if (namedMatch == null)
            {
                _namedEntries.Add(entry.RouteName, outboundMatch);
            }
        }
        
        // 由 outbound matches 创建 link generation decision tree
        // The decision tree will take care of ordering for these entries.
        _linkGenerationTree = new LinkGenerationDecisionTree(outboundMatches.ToArray());
        
        Version = version;
    }                                                                                           
}

```

###### 3.3.a.1 扩展方法 - route async

```c#
public class TreeRouter : IRouter
{    
    public async Task RouteAsync(RouteContext context)
    {
        // 遍历 url matching tree 集合，
        foreach (var tree in _trees)
        {
            /* 为 url matching tree 创建 tree enumerator */
            var tokenizer = new PathTokenizer(context.HttpContext.Request.Path);
            var root = tree.Root;            
            var treeEnumerator = new TreeEnumerator(root, tokenizer);
            
            // Create a snapshot before processing the route. 
            // We'll restore this snapshot before running each to restore the state. 
            // This is likely an "empty" snapshot, which doesn't allocate.
            
            // 创建 route data 的 snapshot
            var snapshot = context.RouteData
                				  .PushState(
                					   router: null, 
                					   values: null, 
                					   dataTokens: null);
            
            // 遍历 url matching node，
            while (treeEnumerator.MoveNext())
            {
                var node = treeEnumerator.Current;
                
                // 遍历 node 中的 matches
                foreach (var item in node.Matches)
                {                    
                    var entry = item.Entry;
                    var matcher = item.TemplateMatcher;
                    
                    try
                    {
                        // 如果 request 不能通过 matcher 匹配，下一个 inbound match
                        if (!matcher.TryMatch(
                            	context.HttpContext.Request.Path, 
                            	context.RouteData.Values))
                        {
                            continue;
                        }
                        
                        // 如果 (request) 不能通过 route constraint matcher 匹配，
                        // 下一个 inbound match
                        if (!RouteConstraintMatcher.Match(
	                            entry.Constraints,
    	                        context.RouteData.Values,
        	                    context.HttpContext,
            	                this,
                	            RouteDirection.IncomingRequest,
                    	        _constraintLogger))
                        {
                            continue;
                        }
                        
                        /* inbound match 匹配 request */
                        
                        // 日志
                        _logger.RequestMatchedRoute(
                            entry.RouteName, 
                            entry.RouteTemplate.TemplateText);
                        
                        // 向 route data 注入该 inbound route entry 的 handler(irouter)
                        context.RouteData.Routers.Add(entry.Handler);
                        // 执行 irouter
                        await entry.Handler.RouteAsync(context);
                        if (context.Handler != null)
                        {
                            return;
                        }
                    }
                    finally
                    {                        
                        if (context.Handler == null)
                        {
                            // Restore the original values to prevent polluting the route data.
                            snapshot.Restore();
                        }
                    }
                }
            }
        }
    }
}

```

###### 3.3.a.2 扩展方法 - get virtual path

```c#
public class TreeRouter : IRouter
{       
    public VirtualPathData GetVirtualPath(VirtualPathContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        // If it's a named route we will try to generate a link directly and
        // if we can't, we will not try to generate it using an unnamed route.
        
        // 如果 route name 不为 null，调用 get virtual path for named route
        if (context.RouteName != null)
        {
            return GetVirtualPathForNamedRoute(context);
        }
        
        // The decision tree will give us back all entries that match the provided 
        // route data in the correct order. 
        // We just need to iterate them and use the first one that can generate a link.
        
        /* route name 不为 null，使用 outbound match 创建 virtual path */
        
        // 解析 outbound match
        var matches = _linkGenerationTree.GetMatches(
            context.Values, 
            context.AmbientValues);
        
        if (matches == null)
        {
            return null;
        }
        
        // 遍历 outbound match，调用 generate virtual path 方法
        for (var i = 0; i < matches.Count; i++)
        {
            var path = GenerateVirtualPath(
                context, 
                matches[i].Match.Entry, 
                matches[i].Match.TemplateBinder);
            
            if (path != null)
            {
                return path;
            }
        }
        
        return null;
    }
    
    // get virtual path for named route
    private VirtualPathData GetVirtualPathForNamedRoute(VirtualPathContext context)
    {
        if (_namedEntries.TryGetValue(
	            context.RouteName, 
    	        out var match))
        {
            // 调用 generate virtual path 方法
            var path = GenerateVirtualPath(
                context, 
                match.Entry, 
                match.TemplateBinder);
            if (path != null)
            {
                return path;
            }
        }
        return null;
    }
    
    // 创建 virtual path
    private VirtualPathData GenerateVirtualPath(
        VirtualPathContext context,
        OutboundRouteEntry entry,
        TemplateBinder binder)
    {
        // In attribute the context includes the values that are used to select this 
        // entry - typically these will be the standard 'action', 'controller' and 
        // maybe 'area' tokens. 
        // However, we don't want to pass these to the link generation code, or else 
        // they will end up as query parameters.
        //
        // So, we need to exclude from here any values that are 'required link values', 
        // but aren't parameters in the template.
        //
        // Ex:
        //      template: api/Products/{action}
        //      required values: { id = "5", action = "Buy", Controller = "CoolProducts" }
        //
        //      result: { id = "5", action = "Buy" }
        
        /* 将 outbound route entry 的 required link value（用于属性注入）
           注入 input values */
        var inputValues = new RouteValueDictionary();
        foreach (var kvp in context.Values)
        {
            if (entry.RequiredLinkValues.ContainsKey(kvp.Key))
            {
                var parameter = entry.RouteTemplate.GetParameter(kvp.Key);
                
                if (parameter == null)
                {
                    continue;
                }
            }
            
            inputValues.Add(kvp.Key, kvp.Value);
        }
        
        // 使用 template binder 合并 values
        var bindingResult = binder.GetValues(context.AmbientValues, inputValues);
        if (bindingResult == null)
        {
            // A required parameter in the template didn't get a value.
            return null;
        }
        
        // constraint matcher 验证 constraint
        var matched = RouteConstraintMatcher.Match(
            entry.Constraints,
            bindingResult.CombinedValues,
            context.HttpContext,
            this,
            RouteDirection.UrlGeneration,
            _constraintLogger);
        
        if (!matched)
        {
            // A constraint rejected this link.
            return null;
        }
        
        // 获取 outbound route entry 中 iroute 的virtual path，
        // 如果不为 null，直接返回结果
        var pathData = entry.Handler.GetVirtualPath(context);
        if (pathData != null)
        {
            // If path is non-null then the target router short-circuited, we don't expect this
            // in typical MVC scenarios.
            return pathData;
        }
        
        // 否则，即 irouter 无法创建 path data，
        // 由 template binder 创建 virtual path data 并返回
        var path = binder.BindValues(bindingResult.AcceptedValues);
        if (path == null)
        {
            return null;
        }        
        return new VirtualPathData(this, path);
    }
}

```

##### 3.3.b tree router builder

```c#
public class TreeRouteBuilder
{
    private readonly ILogger _logger;
    private readonly ILogger _constraintLogger;
    private readonly UrlEncoder _urlEncoder;
    private readonly ObjectPool<UriBuildingContext> _objectPool;
    private readonly IInlineConstraintResolver _constraintResolver;
    
    public IList<InboundRouteEntry> InboundEntries { get; } = new List<InboundRouteEntry>();  
    public IList<OutboundRouteEntry> OutboundEntries { get; } = new List<OutboundRouteEntry>();
    
    internal TreeRouteBuilder(
        ILoggerFactory loggerFactory,
        ObjectPool<UriBuildingContext> objectPool,
        IInlineConstraintResolver constraintResolver)
    {
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }        
        if (objectPool == null)
        {
            throw new ArgumentNullException(nameof(objectPool));
        }        
        if (constraintResolver == null)
        {
            throw new ArgumentNullException(nameof(constraintResolver));
        }
        
        _urlEncoder = UrlEncoder.Default;
        _objectPool = objectPool;
        _constraintResolver = constraintResolver;
        
        _logger = loggerFactory.CreateLogger<TreeRouter>();
        _constraintLogger = 
            loggerFactory.CreateLogger(typeof(RouteConstraintMatcher).FullName);
    }
                                                     
    public void Clear()
    {
        InboundEntries.Clear();
        OutboundEntries.Clear();
    }
}

```

###### 3.3.b.1 map inbound

```c#
public class TreeRouteBuilder
{
    public InboundRouteEntry MapInbound(
        IRouter handler,
        RouteTemplate routeTemplate,
        string routeName,
        int order)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }        
        if (routeTemplate == null)
        {
            throw new ArgumentNullException(nameof(routeTemplate));
        }
        
        var entry = new InboundRouteEntry()
        {
            Handler = handler,
            Order = order,
            Precedence = RoutePrecedence.ComputeInbound(routeTemplate),
            RouteName = routeName,
            RouteTemplate = routeTemplate,
        };
        
        var constraintBuilder = new RouteConstraintBuilder(
            _constraintResolver, 
            routeTemplate.TemplateText);
        
        foreach (var parameter in routeTemplate.Parameters)
        {
            if (parameter.InlineConstraints != null)
            {
                if (parameter.IsOptional)
                {
                    constraintBuilder.SetOptional(parameter.Name);
                }
                
                foreach (var constraint in parameter.InlineConstraints)
                {
                    constraintBuilder.AddResolvedConstraint(
                        parameter.Name, 
                        constraint.Constraint);
                }
            }
        }
        entry.Constraints = constraintBuilder.Build();
        
        entry.Defaults = new RouteValueDictionary();
        foreach (var parameter in entry.RouteTemplate.Parameters)
        {
            if (parameter.DefaultValue != null)
            {
                entry.Defaults.Add(parameter.Name, parameter.DefaultValue);
            }           
        }
        InboundEntries.Add(entry);
        return entry;
    }
}

```

###### 3.3.b.2 map outbound

```c#
public class TreeRouteBuilder
{
    public OutboundRouteEntry MapOutbound(
        IRouter handler,
        RouteTemplate routeTemplate,
        RouteValueDictionary requiredLinkValues,
        string routeName,
        int order)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }        
        if (routeTemplate == null)
        {
            throw new ArgumentNullException(nameof(routeTemplate));
        }        
        if (requiredLinkValues == null)
        {                
            throw new ArgumentNullException(nameof(requiredLinkValues));
            
        }
        
        var entry = new OutboundRouteEntry()            
        {
            Handler = handler,
            Order = order,
            Precedence = RoutePrecedence.ComputeOutbound(routeTemplate),
            RequiredLinkValues = requiredLinkValues,
            RouteName = routeName,
            RouteTemplate = routeTemplate,
        };
        
        var constraintBuilder = new RouteConstraintBuilder(
            _constraintResolver, 
            routeTemplate.TemplateText);
        
        foreach (var parameter in routeTemplate.Parameters)
        {
            if (parameter.InlineConstraints != null)
            {
                if (parameter.IsOptional)
                {
                    constraintBuilder.SetOptional(parameter.Name);
                }
                
                foreach (var constraint in parameter.InlineConstraints)
                {
                    constraintBuilder.AddResolvedConstraint(
                        parameter.Name, 
                        constraint.Constraint);
                }
            }
        }
        
        entry.Constraints = constraintBuilder.Build();

        entry.Defaults = new RouteValueDictionary();
        foreach (var parameter in entry.RouteTemplate.Parameters)
        {
            if (parameter.DefaultValue != null)
            {
                entry.Defaults
                     .Add(
                    	  parameter.Name, 
                    	  parameter.DefaultValue);
            }
        }
        
        OutboundEntries.Add(entry);
        return entry;
    }
}

```

###### 3.3.b.3 build

```c#
public class TreeRouteBuilder
{
    public TreeRouter Build()
    {
        return Build(version: 0);
    }
           
    public TreeRouter Build(int version)
    {
        // Tree route builder builds a tree for each of the different route orders defined 
        // by the user. When a route needs to be matched, the matching algorithm in tree 
        // router just iterates over the trees in ascending order when it tries to match 
        // the route.
        var trees = new Dictionary<int, UrlMatchingTree>();
        
        foreach (var entry in InboundEntries)
        {
            if (!trees.TryGetValue(entry.Order, out var tree))
            {                    
                tree = new UrlMatchingTree(entry.Order);
                trees.Add(entry.Order, tree);
            }
            
            tree.AddEntry(entry);
        }
                
        return new TreeRouter(
            trees.Values.OrderBy(tree => tree.Order).ToArray(),
            OutboundEntries,
            _urlEncoder,
            _objectPool,
            _logger,
            _constraintLogger,
            version);
    }
}

```



#### 3.3 endpoint routing



#### 2.5 routing

##### 2.5.1 注入服务

###### 2.5.1.1 add routing

```c#
public static class RoutingServiceCollectionExtensions
{
    
    public static IServiceCollection AddRouting(this IServiceCollection services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        services.TryAddTransient<
            IInlineConstraintResolver, 
        	DefaultInlineConstraintResolver>();
        services.TryAddTransient<
            ObjectPoolProvider, 
        	DefaultObjectPoolProvider>();
        services.TryAddSingleton<ObjectPool<UriBuildingContext>>(s =>
        	{
                var provider = s.GetRequiredService<ObjectPoolProvider>();
                return provider.Create<UriBuildingContext>(
                    new UriBuilderContextPooledObjectPolicy());
            });

        // The TreeRouteBuilder is a builder for creating routes, 
        // it should stay transient because it's stateful.
        services.TryAdd(ServiceDescriptor.Transient<TreeRouteBuilder>(s =>
        	{
                var loggerFactory = s.GetRequiredService<ILoggerFactory>();
                var objectPool = s.GetRequiredService<ObjectPool<UriBuildingContext>>();
                var constraintResolver = s.GetRequiredService<IInlineConstraintResolver>();
                return new TreeRouteBuilder(loggerFactory, objectPool, constraintResolver);
            }));

        services.TryAddSingleton(typeof(RoutingMarkerService));
        
        // Setup global collection of endpoint data sources
        var dataSources = new ObservableCollection<EndpointDataSource>();
        services.TryAddEnumerable(
            ServiceDescriptor
            	.Transient<IConfigureOptions<RouteOptions>, ConfigureRouteOptions>(
                    serviceProvider => new ConfigureRouteOptions(dataSources)));
        
        // Allow global access to the list of endpoints.
        services.TryAddSingleton<EndpointDataSource>(s =>
        	{
                // Call internal ctor and pass global collection
                return new CompositeEndpointDataSource(dataSources);
            });
        
        //
        // Default matcher implementation
        //
        services.TryAddSingleton<ParameterPolicyFactory, DefaultParameterPolicyFactory>();
        services.TryAddSingleton<MatcherFactory, DfaMatcherFactory>();
        services.TryAddTransient<DfaMatcherBuilder>();
        services.TryAddSingleton<DfaGraphWriter>();
        services.TryAddTransient<DataSourceDependentMatcher.Lifetime>();
        services.TryAddSingleton<EndpointMetadataComparer>(services =>
        	{
                // This has no public constructor. 
                return new EndpointMetadataComparer(services);
            });
        
        // Link generation related services
        services.TryAddSingleton<LinkGenerator, DefaultLinkGenerator>();
        services.TryAddSingleton<
            IEndpointAddressScheme<string>, EndpointNameAddressScheme>();
        services.TryAddSingleton<
            IEndpointAddressScheme<RouteValuesAddress>, RouteValuesAddressScheme>();
        services.TryAddSingleton<LinkParser, DefaultLinkParser>();
        
        //
        // Endpoint Selection
        //
        services.TryAddSingleton<EndpointSelector, DefaultEndpointSelector>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<MatcherPolicy, HttpMethodMatcherPolicy>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<MatcherPolicy, HostMatcherPolicy>());
        
        //
        // Misc infrastructure
        //
        services.TryAddSingleton<TemplateBinderFactory, DefaultTemplateBinderFactory>();
        services.TryAddSingleton<RoutePatternTransformer, DefaultRoutePatternTransformer>();
        return services;
    }
    
        
    public static IServiceCollection AddRouting(
        this IServiceCollection services,
        Action<RouteOptions> configureOptions)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }
        
        services.Configure(configureOptions);
        services.AddRouting();
        
        return services;
    }
}

```

###### 2.5.1.2 route options

```c#
public class RouteOptions
{    
    /* endpoint data source */
    
    private ICollection<EndpointDataSource> _endpointDataSources = default!;            
    internal ICollection<EndpointDataSource> EndpointDataSources
    {
        get
        {
            // IOptions<RouteOptions> 在 routing service 中注册
            Debug.Assert(
                _endpointDataSources != null, 
                "Endpoint data sources should have been set in DI.");
            return _endpointDataSources;
        }
        set => _endpointDataSources = value;
    }
    
    /* constraint */
    
    private IDictionary<string, Type> _constraintTypeMap = GetDefaultConstraintMap();  
    private static IDictionary<string, Type> GetDefaultConstraintMap()
    {
        var defaults = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        
        // Type-specific constraints
        AddConstraint<IntRouteConstraint>(defaults, "int");
        AddConstraint<BoolRouteConstraint>(defaults, "bool");
        AddConstraint<DateTimeRouteConstraint>(defaults, "datetime");
        AddConstraint<DecimalRouteConstraint>(defaults, "decimal");
        AddConstraint<DoubleRouteConstraint>(defaults, "double");
        AddConstraint<FloatRouteConstraint>(defaults, "float");
        AddConstraint<GuidRouteConstraint>(defaults, "guid");
        AddConstraint<LongRouteConstraint>(defaults, "long");
        
        // Length constraints
        AddConstraint<MinLengthRouteConstraint>(defaults, "minlength");
        AddConstraint<MaxLengthRouteConstraint>(defaults, "maxlength");
        AddConstraint<LengthRouteConstraint>(defaults, "length");
        
        // Min/Max value constraints
        AddConstraint<MinRouteConstraint>(defaults, "min");
        AddConstraint<MaxRouteConstraint>(defaults, "max");
        AddConstraint<RangeRouteConstraint>(defaults, "range");
        
        // Regex-based constraints
        AddConstraint<AlphaRouteConstraint>(defaults, "alpha");
        AddConstraint<RegexInlineRouteConstraint>(defaults, "regex");
        
        AddConstraint<RequiredRouteConstraint>(defaults, "required");
        
        // Files
        AddConstraint<FileNameRouteConstraint>(defaults, "file");
        AddConstraint<NonFileNameRouteConstraint>(defaults, "nonfile");
        
        return defaults;
    }        
    // This API could be exposed on RouteOptions
    private static void AddConstraint<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors)]TConstraint>(
        Dictionary<string, Type> constraintMap, 
        string text) 
        	where TConstraint : IRouteConstraint
    {
        constraintMap[text] = typeof(TConstraint);
    }
    // constraint map
    public IDictionary<string, Type> ConstraintMap
    {
        get
        {
            return _constraintTypeMap;
        }
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(ConstraintMap));
            }
            
            _constraintTypeMap = value;
        }
    }
    
    /* for link generator */
    
    public bool LowercaseUrls { get; set; }            
    public bool LowercaseQueryStrings { get; set; }           
    public bool AppendTrailingSlash { get; set; }       
    public bool SuppressCheckForUnhandledSecurityMetadata { get; set; }        
}

```

##### 2.5.2 router routing

###### 2.5.2.1 use router

```c#
public static class RoutingBuilderExtensions
{
    public static IApplicationBuilder UseRouter(
        this IApplicationBuilder builder, 
        IRouter router)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }    
        if (router == null)
        {
            throw new ArgumentNullException(nameof(router));
        }
        
        // 如果没有注册 routing marker service，抛出异常
        if (builder.ApplicationServices
            	   .GetService(typeof(RoutingMarkerService)) == null)
        {
            throw new InvalidOperationException(
                Resources.FormatUnableToFindServices(
                    nameof(IServiceCollection),
                    nameof(RoutingServiceCollectionExtensions.AddRouting),
                    "ConfigureServices(...)"));
        }
        
        // 注入 router middleware
        return builder.UseMiddleware<RouterMiddleware>(router);
    }
        
    public static IApplicationBuilder UseRouter(
        this IApplicationBuilder builder, 
        Action<IRouteBuilder> action)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }
        
        // 如果没有注册 routing marker service，抛出异常
        if (builder.ApplicationServices
            	   .GetService(typeof(RoutingMarkerService)) == null)
        {
            throw new InvalidOperationException(
                Resources.FormatUnableToFindServices(
                    nameof(IServiceCollection),
                    nameof(RoutingServiceCollectionExtensions.AddRouting),
                    "ConfigureServices(...)"));
        }

        // 创建 route builder 并配置
        var routeBuilder = new RouteBuilder(builder);
        action(routeBuilder);
        
        // 由 route builder 创建 irouter（router collection），
        // 调用 use router（irouter）方法
        return builder.UseRouter(routeBuilder.Build());
    }
}

```

###### 2.5.2.2 router middleware

```c#
public class RouterMiddleware
{
    private readonly ILogger _logger;
    private readonly RequestDelegate _next;
    private readonly IRouter _router;
            
    public RouterMiddleware(
        RequestDelegate next,
        ILoggerFactory loggerFactory,
        IRouter router)
    {
        _next = next;
        _router = router;        
        _logger = loggerFactory.CreateLogger<RouterMiddleware>();
    }
        
    public async Task Invoke(HttpContext httpContext)
    {
        // 创建 route context，封装当前 http context
        var context = new RouteContext(httpContext);
        // 向 route context 注入 _router
        context.RouteData
               .Routers
               .Add(_router);
        
        // 执行 _router 的 route async 方法
        await _router.RouteAsync(context);
        
        // 如果没有 handler（ route 不匹配），转到 next
        if (context.Handler == null)
        {
            _logger.RequestNotMatched();
            await _next.Invoke(httpContext);
        }
        // 否则，即 route 匹配，
        else
        {
            // 封装 route data -> routing feature，           
            var routingFeature = new RoutingFeature()
            {
                RouteData = context.RouteData
            };
                        
            // Set the RouteValues on the current request, this is to keep the 
            // IRouteValuesFeature inline with the IRoutingFeature
            
            // 将 routing feature（route data）注入 http context，
            httpContext.Request.RouteValues = context.RouteData.Values;
            httpContext.Features.Set<IRoutingFeature>(routingFeature);
            
            // 执行 handler
            await context.Handler(context.HttpContext);
        }
    }
}

```

##### 2.5.3 endpoint routing

###### 2.5.3.1 use routing

```c#
public static class EndpointRoutingApplicationBuilderExtensions
{
    private const string EndpointRouteBuilder = "__EndpointRouteBuilder";
    
    public static IApplicationBuilder UseRouting(
        this IApplicationBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        // 确认 routing service 已经注册
        VerifyRoutingServicesAreRegistered(builder);
        
        // 创建 default endpoint route builder
        var endpointRouteBuilder = new DefaultEndpointRouteBuilder(builder);
        // 将 default endpoint route builder 注入 IApplicationBuilder
        builder.Properties[EndpointRouteBuilder] = endpointRouteBuilder;
        
        // 注入 endpoint routing middleware
        return builder.UseMiddleware<EndpointRoutingMiddleware>(endpointRouteBuilder);
    }
}

```

###### 2.5.3.2 endpoint routing middleware

* 选择匹配的 endpoint 并注入 http context

```c#
internal sealed class EndpointRoutingMiddleware
{
    private const string 
        DiagnosticsEndpointMatchedKey = "Microsoft.AspNetCore.Routing.EndpointMatched";
    
    private readonly MatcherFactory _matcherFactory;
    private readonly ILogger _logger;
    private readonly EndpointDataSource _endpointDataSource;
    private readonly DiagnosticListener _diagnosticListener;
    private readonly RequestDelegate _next;
    
    private Task<Matcher>? _initializationTask;
    
    public EndpointRoutingMiddleware(
        MatcherFactory matcherFactory,
        ILogger<EndpointRoutingMiddleware> logger,
        IEndpointRouteBuilder endpointRouteBuilder,
        DiagnosticListener diagnosticListener,
        RequestDelegate next)
    {
        if (endpointRouteBuilder == null)
        {
            throw new ArgumentNullException(nameof(endpointRouteBuilder));
        }
        
        // 注入服务
        _matcherFactory = matcherFactory 
            ?? throw new ArgumentNullException(nameof(matcherFactory));
        _logger = logger 
            ?? throw new ArgumentNullException(nameof(logger));
        _diagnosticListener = diagnosticListener 
            ?? throw new ArgumentNullException(nameof(diagnosticListener));
        _next = next 
            ?? throw new ArgumentNullException(nameof(next));    
        
        // 创建 endpoint data source（默认值，empty）
        _endpointDataSource = new CompositeEndpointDataSource(
            endpointRouteBuilder.DataSources);
    }
    
    public Task Invoke(HttpContext httpContext)
    {
        /* 如果 http context 中已经注入了 endpoint，返回 */
        // There's already an endpoint, skip matching completely
        var endpoint = httpContext.GetEndpoint();
        if (endpoint != null)
        {
            Log.MatchSkipped(_logger, endpoint);
            return _next(httpContext);
        }
        
        /* 找到适合的 endpoint */           
        
        // There's an inherent race condition between
        // waiting for init and accessing the matcher
        // this is OK because once `_matcher` is initialized, it will not be set to null again.
        var matcherTask = InitializeAsync();
        if (!matcherTask.IsCompletedSuccessfully)
        {
            // matcher task 赢得异步执行
            return AwaitMatcher(this, httpContext, matcherTask);
        }
        
        var matchTask = matcherTask.Result.MatchAsync(httpContext);
        if (!matchTask.IsCompletedSuccessfully)
        {
            // match task 赢得异步执行
            return AwaitMatch(this, httpContext, matchTask);
        }
        
        return SetRoutingAndContinue(httpContext);
        
        // Awaited fallbacks for when the Tasks do not synchronously complete
        static async Task AwaitMatcher(
            EndpointRoutingMiddleware middleware, 
            HttpContext httpContext, 
            Task<Matcher> matcherTask)
        {
            var matcher = await matcherTask;
            await matcher.MatchAsync(httpContext);
            await middleware.SetRoutingAndContinue(httpContext);
        }
        
        static async Task AwaitMatch(
            EndpointRoutingMiddleware middleware, 
            HttpContext httpContext, 
            Task matchTask)
        {
            await matchTask;
            await middleware.SetRoutingAndContinue(httpContext);
        }        
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task SetRoutingAndContinue(HttpContext httpContext)
    {
        // If there was no mutation of the endpoint then log failure
        var endpoint = httpContext.GetEndpoint();
        if (endpoint == null)
        {
            Log.MatchFailure(_logger);
        }
        else
        {
            // Raise an event if the route matched
            if (_diagnosticListener.IsEnabled() && 
                _diagnosticListener.IsEnabled(DiagnosticsEndpointMatchedKey))
            {
                // We're just going to send the HttpContext 
                // since it has all of the relevant information
                _diagnosticListener.Write(DiagnosticsEndpointMatchedKey, httpContext);
            }
            
            Log.MatchSuccess(_logger, endpoint);
        }
        
        return _next(httpContext);
    }
    
    /* 从 matcher factory 解析 matcher */
    
    // Initialization is async to avoid blocking threads while reflection and things
    // of that nature take place.
    //
    // We've seen cases where startup is very slow if we  allow multiple threads to race
    // while initializing the set of endpoints/routes. Doing CPU intensive work is a
    // blocking operation if you have a low core count and enough work to do.
    private Task<Matcher> InitializeAsync()
    {
        var initializationTask = _initializationTask;
        if (initializationTask != null)
        {
            return initializationTask;
        }
        
        return InitializeCoreAsync();
    }
    
    private Task<Matcher> InitializeCoreAsync()
    {
        var initialization = 
            new TaskCompletionSource<Matcher>(
	            TaskCreationOptions.RunContinuationsAsynchronously);
        var initializationTask = 
            Interlocked.CompareExchange(
            	ref _initializationTask, 
            initialization.Task, null);
        
        if (initializationTask != null)
        {
            // This thread lost the race, join the existing task.
            return initializationTask;
        }
        
        // This thread won the race, do the initialization.
        try
        {
            var matcher = _matcherFactory.CreateMatcher(_endpointDataSource);
            
            // Now replace the initialization task with one created 
            // with the default execution context.
            // This is important because capturing the execution context 
            // will leak memory in ASP.NET Core.
            using (ExecutionContext.SuppressFlow())
            {
                _initializationTask = Task.FromResult(matcher);
            }
            
            // Complete the task, 
            // this will unblock any requests that came in while initializing.
            initialization.SetResult(matcher);
            return initialization.Task;
        }
        catch (Exception ex)
        {
            // Allow initialization to occur again. Since DataSources can change, it's possible
            // for the developer to correct the data causing the failure.
            _initializationTask = null;
            
            // Complete the task, 
            // this will throw for any requests that came in while initializing.
            initialization.SetException(ex);
            return initialization.Task;
        }
    }
    
#nullable disable
    private static class Log
    {
        private static readonly Action<ILogger, string, Exception> 
            _matchSuccess = LoggerMessage.Define<string>(
            	LogLevel.Debug,
	            new EventId(1, "MatchSuccess"),
    	        "Request matched endpoint '{EndpointName}'");
        
        private static readonly Action<ILogger, Exception> 
            _matchFailure = LoggerMessage.Define(
	            LogLevel.Debug,
    	        new EventId(2, "MatchFailure"),
        	    "Request did not match any endpoints");
        
        private static readonly Action<ILogger, string, Exception> 
            _matchingSkipped = LoggerMessage.Define<string>(
            	LogLevel.Debug,
	            new EventId(3, "MatchingSkipped"),
    	        "Endpoint '{EndpointName}' already set, skipping route matching.");
        
        public static void MatchSuccess(ILogger logger, Endpoint endpoint)
        {
            _matchSuccess(logger, endpoint.DisplayName, null);
        }
        
        public static void MatchFailure(ILogger logger)
        {
            _matchFailure(logger, null);
        }
        
        public static void MatchSkipped(ILogger logger, Endpoint endpoint)
        {
            _matchingSkipped(logger, endpoint.DisplayName, null);
        }
    }
}

```

###### 2.5.3.3 use endpoints

```c#
public static class EndpointRoutingApplicationBuilderExtensions
{
    private const string EndpointRouteBuilder = "__EndpointRouteBuilder";
    
    public static IApplicationBuilder UseEndpoints(
        this IApplicationBuilder builder, 
        Action<IEndpointRouteBuilder> configure)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        
        }
        
        // 确认 routing service 已经注册
        VerifyRoutingServicesAreRegistered(builder);
        
        /* 确认已经调用了 use routing，
           此时可以使用注入的 use routing 方法创建的 endpoint route builder，
           从而保证是 endpoint route builder 是 global，
           此处（use endpoint 方法）注入的 endpoint 可以在 use routing 发现并匹配 request */
        VerifyEndpointRoutingMiddlewareIsRegistered(builder, out var endpointRouteBuilder);
        
        // 配置 endpoint route builder，即注入 endpoint
        configure(endpointRouteBuilder);
                
        // Yes, this mutates an IOptions. 
        // We're registering data sources in a global collection which
        // can be used for discovery of endpoints or URL generation.
        //
        // Each middleware gets its own collection of data sources, 
        // and all of those data sources also get added to a global collection.
        
        /* 将 endpoint route builder 的 endpoint data source 注入 route options */
        
        // 解析 route options
        var routeOptions = builder.ApplicationServices
            					  .GetRequiredService<IOptions<RouteOptions>>();
        // 遍历 endpoint route builder 的 data source，
        // 注入 route options 的 value
        foreach (var dataSource in endpointRouteBuilder.DataSources)
        {
            routeOptions.Value.EndpointDataSources.Add(dataSource);
        }
        
        // 注入 endpoint middleware 
        return builder.UseMiddleware<EndpointMiddleware>();
    }
}

```

###### 2.5.3.4 endpoint middleware

```c#
internal sealed class EndpointMiddleware
{
    internal const string 
        AuthorizationMiddlewareInvokedKey = 
        	"__AuthorizationMiddlewareWithEndpointInvoked";
    internal const string 
        CorsMiddlewareInvokedKey = 
        	"__CorsMiddlewareWithEndpointInvoked";
    
    private readonly ILogger _logger;
    private readonly RequestDelegate _next;
    private readonly RouteOptions _routeOptions;
    
    public EndpointMiddleware(
        ILogger<EndpointMiddleware> logger,
        RequestDelegate next,
        IOptions<RouteOptions> routeOptions)
    {
        _logger = logger 
            ?? throw new ArgumentNullException(nameof(logger));
        _next = next 
            ?? throw new ArgumentNullException(nameof(next));
        _routeOptions = routeOptions?.Value 
            ?? throw new ArgumentNullException(nameof(routeOptions));
    }
    
    public Task Invoke(HttpContext httpContext)
    {
        var endpoint = httpContext.GetEndpoint();
        if (endpoint?.RequestDelegate != null)
        {
            // 如果 route options 标记了 suppres check for unhandle security
            if (!_routeOptions.SuppressCheckForUnhandledSecurityMetadata)
            {
                // 如果 endpoint 包含 IAuthordata，
                // 但是 httpContext 的 items 没有注入对应的 key --
                //   “__AuthorizationMiddlewareWithEndpointInvoked”，抛出异常                
                if (endpoint.Metadata
	                    	.GetMetadata<IAuthorizeData>() != null &&
                    !httpContext.Items
		                    	.ContainsKey(AuthorizationMiddlewareInvokedKey))
                {
                    ThrowMissingAuthMiddlewareException(endpoint);
                }
                
                // 如果 endpoint 包含 ICorsMetadata，
                // 但是 http context 的 items 没有注入对应的 key --
                //   “__CorsMiddlewareWithEndpointInvoked”，抛出异常                
                if (endpoint.Metadata
                    		.GetMetadata<ICorsMetadata>() != null &&
                    !httpContext.Items
	                    	.ContainsKey(CorsMiddlewareInvokedKey))
                {
                    ThrowMissingCorsMiddlewareException(endpoint);
                }
            }
            
            Log.ExecutingEndpoint(_logger, endpoint);
            
            try
            {
                // 获取 endpoint 中的的 request delegate 并执行
                var requestTask = endpoint.RequestDelegate(httpContext);
                if (!requestTask.IsCompletedSuccessfully)
                {
                    return AwaitRequestTask(endpoint, requestTask, _logger);
                }
            }
            catch (Exception exception)
            {
                Log.ExecutedEndpoint(_logger, endpoint);
                return Task.FromException(exception);
            }
            
            Log.ExecutedEndpoint(_logger, endpoint);
            return Task.CompletedTask;
        }
        
        return _next(httpContext);
        
        // 异步执行等待
        static async Task AwaitRequestTask(
            Endpoint endpoint, 
            Task requestTask, 
            ILogger logger)
        {
            try
            {
                await requestTask;
            }
            finally
            {
                Log.ExecutedEndpoint(logger, endpoint);
            }
        }
    }
    
    private static void ThrowMissingAuthMiddlewareException(Endpoint endpoint)
    {
        throw new InvalidOperationException(
            $"Endpoint {endpoint.DisplayName} contains authorization metadata, " +                         "but a middleware was not found that supports authorization." +                                 Environment.NewLine +
            "Configure your application startup by adding app.UseAuthorization() inside the all to Configure(..) in the application startup code. The call to app.UseAuthorization() must appear between app.UseRouting() and app.UseEndpoints(...).");
    }
    
    private static void ThrowMissingCorsMiddlewareException(Endpoint endpoint)
    {
        throw new InvalidOperationException(
            $"Endpoint {endpoint.DisplayName} contains CORS metadata, " +                                   "but a middleware was not found that supports CORS." +                                         Environment.NewLine +
            "Configure your application startup by adding app.UseCors() inside the call to Configure(..) in the application startup code. The call to app.UseCors() must appear between app.UseRouting() and app.UseEndpoints(...).");
    }
    
#nullable disable
    private static class Log
    {
        private static readonly Action<ILogger, string, Exception> 
            _executingEndpoint = LoggerMessage.Define<string>(
            	LogLevel.Information,
            	new EventId(0, "ExecutingEndpoint"),
            	"Executing endpoint '{EndpointName}'");
        
        private static readonly Action<ILogger, string, Exception> 
            _executedEndpoint = LoggerMessage.Define<string>(
            	LogLevel.Information,
            	new EventId(1, "ExecutedEndpoint"),
            	"Executed endpoint '{EndpointName}'");
        
        public static void ExecutingEndpoint(ILogger logger, Endpoint endpoint)
        {
            _executingEndpoint(logger, endpoint.DisplayName, null);
        }
        
        public static void ExecutedEndpoint(ILogger logger, Endpoint endpoint)
        {
            _executedEndpoint(logger, endpoint.DisplayName, null);
        }
    }
}

```

##### 2.5.4 tree route





#### 2.5 middleware variety ？







#### 2.7 mvc

##### 2.7.1 controller action endpoint builder

```c#
public sealed class ControllerActionEndpointConventionBuilder : IEndpointConventionBuilder
{
    // The lock is shared with the data source.
    private readonly object _lock;
    private readonly List<Action<EndpointBuilder>> _conventions;
    
    internal ControllerActionEndpointConventionBuilder(
        object @lock, 
        List<Action<EndpointBuilder>> conventions)
    {
        _lock = @lock;
        _conventions = conventions;
    }
        
    public void Add(Action<EndpointBuilder> convention)
    {
        if (convention == null)
        {
            throw new ArgumentNullException(nameof(convention));
        }
        
        // The lock is shared with the data source. We want to lock here
        // to avoid mutating this list while its read in the data source.
        lock (_lock)
        {
            _conventions.Add(convention);
        }
    }
}

```

##### 2.7.2 controller action endpoint data source

###### 2.7.1.1 action endpoint data source base

```c#
internal abstract class ActionEndpointDataSourceBase : 
	EndpointDataSource, 
	IDisposable
{
    private readonly IActionDescriptorCollectionProvider _actions;
    
    // The following are protected by this lock for WRITES only. 
    // This pattern is similar to DefaultActionDescriptorChangeProvider 
    // - see comments there for details on all of the threading behaviors.
    protected readonly object Lock = new object();
    
    // Protected for READS and WRITES.
    protected readonly List<Action<EndpointBuilder>> Conventions;
        
    private List<Endpoint>? _endpoints;
    private CancellationTokenSource? _cancellationTokenSource;
    private IChangeToken? _changeToken;
    private IDisposable? _disposable;
    
    public override IReadOnlyList<Endpoint> Endpoints
    {            
        get
        {
            Initialize();
            Debug.Assert(_changeToken != null);
            Debug.Assert(_endpoints != null);
            return _endpoints;
        }
    }
    
    public ActionEndpointDataSourceBase(
        IActionDescriptorCollectionProvider actions)
    {
        // 注入 action descriptor collection provider
        _actions = actions;        
        // 创建 action<endpoint builder> 集合
        Conventions = new List<Action<EndpointBuilder>>();
    }
        
    protected void Subscribe()
    {
        // IMPORTANT: 
        // this needs to be called by the derived class 
        // to avoid the fragile base class problem. 
        // We can't call this in the base-class constuctor because it's too early.
        //
        // It's possible for someone to override the collection provider without providing
        // change notifications. If that's the case we won't process changes.
        if (_actions is ActionDescriptorCollectionProvider collectionProviderWithChangeToken)
        {
            _disposable = ChangeToken.OnChange(
                () => collectionProviderWithChangeToken.GetChangeToken(),
                UpdateEndpoints);
        }
    }
        
    private void UpdateEndpoints()
    {
        lock (Lock)
        {
            var endpoints = CreateEndpoints(_actions.ActionDescriptors.Items, Conventions);
            
            // See comments in DefaultActionDescriptorCollectionProvider. These steps are done
            // in a specific order to ensure callers always see a consistent state.
            
            // Step 1 - capture old token
            var oldCancellationTokenSource = _cancellationTokenSource;
            
            // Step 2 - update endpoints
            _endpoints = endpoints;
            
            // Step 3 - create new change token
            _cancellationTokenSource = new CancellationTokenSource();
            _changeToken = new CancellationChangeToken(_cancellationTokenSource.Token);
            
            // Step 4 - trigger old token
            oldCancellationTokenSource?.Cancel();
        }
    }
        
    // Will be called with the lock.
    protected abstract List<Endpoint> CreateEndpoints(
        IReadOnlyList<ActionDescriptor> actions, 
        IReadOnlyList<Action<EndpointBuilder>> conventions);
                                            
    public override IChangeToken GetChangeToken()
    {
        Initialize();
        Debug.Assert(_changeToken != null);
        Debug.Assert(_endpoints != null);
        return _changeToken;
    }
    
    private void Initialize()
    {
        if (_endpoints == null)
        {
            lock (Lock)
            {
                if (_endpoints == null)
                {
                    UpdateEndpoints();
                }
            }
        }
    }
        
    public void Dispose()
    {
        // Once disposed we won't process updates anymore, 
        // but we still allow access to the endpoints.
        _disposable?.Dispose();
        _disposable = null;
    }                
}

```

###### 2.7.1.2 controller action endpoint data source

```c#
internal class ControllerActionEndpointDataSource : ActionEndpointDataSourceBase
{
    private readonly ActionEndpointFactory _endpointFactory;
    private readonly OrderedEndpointsSequenceProvider _orderSequence;
    private readonly List<ConventionalRouteEntry> _routes;
    
    public ControllerActionEndpointDataSource(
        ControllerActionEndpointDataSourceIdProvider dataSourceIdProvider,
        IActionDescriptorCollectionProvider actions,
        ActionEndpointFactory endpointFactory,
        OrderedEndpointsSequenceProvider orderSequence)            : base(actions)
    {
        _endpointFactory = endpointFactory;
        
        DataSourceId = dataSourceIdProvider.CreateId();
        _orderSequence = orderSequence;
        
        _routes = new List<ConventionalRouteEntry>();
        
        DefaultBuilder = new ControllerActionEndpointConventionBuilder(Lock, Conventions);
        
        // IMPORTANT: this needs to be the last thing we do in the constructor.
        // Change notifications can happen immediately!
        Subscribe();
    }
    
    public int DataSourceId { get; }
    
    public ControllerActionEndpointConventionBuilder DefaultBuilder { get; }
    
    // Used to control whether we create 'inert' (non-routable) endpoints for use in dynamic
    // selection. Set to true by builder methods that do dynamic/fallback selection.
    public bool CreateInertEndpoints { get; set; }
    
    public ControllerActionEndpointConventionBuilder AddRoute(
        string routeName,
        string pattern,
        RouteValueDictionary defaults,
        IDictionary<string, object> constraints,
        RouteValueDictionary dataTokens)
    {
        lock (Lock)
        {
            var conventions = new List<Action<EndpointBuilder>>();
            _routes.Add(
                new ConventionalRouteEntry(
                    routeName, 
                    pattern, 
                    defaults, 
                    constraints, 
                    dataTokens, 
                    _orderSequence.GetNext(), 
                    conventions));
            return new ControllerActionEndpointConventionBuilder(Lock, conventions);
        }
    }
    
    protected override List<Endpoint> CreateEndpoints(
        IReadOnlyList<ActionDescriptor> actions, 
        IReadOnlyList<Action<EndpointBuilder>> conventions)
    {
        var endpoints = new List<Endpoint>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // MVC guarantees that when two of it's endpoints 
        // have the same route name they are equivalent.
        //
        // However, Endpoint Routing requires Endpoint Names to be unique.
        var routeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // For each controller action - add the relevant endpoints.
        //
        // 1. If the action is attribute routed, we use that information verbatim
        // 2. If the action is conventional routed
        //      a. Create a *matching only* endpoint for each action X route (if possible)
        //      b. Ignore link generation for now
        for (var i = 0; i < actions.Count; i++)
        {
            if (actions[i] is ControllerActionDescriptor action)
            {
                _endpointFactory.AddEndpoints(
                    endpoints, 
                    routeNames, 
                    action, 
                    routes, 
                    conventions, 
                    CreateInertEndpoints);
                
                if (_routes.Count > 0)
                {
                    // If we have conventional routes, keep track of the keys so we can create
                    // the link generation routes later.
                    foreach (var kvp in action.RouteValues)
                    {
                        keys.Add(kvp.Key);
                    }
                }
            }
        }
        
        // Now create a *link generation only* endpoint for each route. This gives us a very
        // compatible experience to previous versions.
        for (var i = 0; i < _routes.Count; i++)
        {
            var route = _routes[i];
            _endpointFactory.AddConventionalLinkGenerationRoute(
                endpoints, 
                routeNames, 
                keys, 
                route, 
                conventions);
        }
        
        return endpoints;
    }
    
    internal void AddDynamicControllerEndpoint(
        IEndpointRouteBuilder endpoints, 
        string pattern, 
        Type transformerType, 
        object state, 
        int? order = null)
    {
        CreateInertEndpoints = true;
        lock (Lock)
        {
            order ??= _orderSequence.GetNext();
            
            endpoints
                .Map(
                	pattern,
	                context =>
    	            {
        	            throw new InvalidOperationException(
            	            "This endpoint is not expected to be executed directly.");
                	})
                .Add(b =>
                     {
                         ((RouteEndpointBuilder)b).Order = order.Value;
                         b.Metadata.Add(
                             new DynamicControllerRouteValueTransformerMetadata(
                                 transformerType, 
                                 state));
                         b.Metadata.Add(new ControllerEndpointDataSourceIdMetadata(
                             DataSourceId));
                    });
        }
    }
}

```

###### 2.7.1.3 controller action endpoint data source factory

```c#
internal class ControllerActionEndpointDataSourceFactory
{
    private readonly ControllerActionEndpointDataSourceIdProvider _dataSourceIdProvider;
    private readonly IActionDescriptorCollectionProvider _actions;
    private readonly ActionEndpointFactory _factory;
    
    public ControllerActionEndpointDataSourceFactory(
        ControllerActionEndpointDataSourceIdProvider dataSourceIdProvider,
        IActionDescriptorCollectionProvider actions,
        ActionEndpointFactory factory)
    {
        _dataSourceIdProvider = dataSourceIdProvider;
        _actions = actions;
        _factory = factory;
    }
    
    public ControllerActionEndpointDataSource Create(
        OrderedEndpointsSequenceProvider orderProvider)
    {
        return new ControllerActionEndpointDataSource(
            _dataSourceIdProvider, 
            _actions, 
            _factory, 
            orderProvider);
    }
}

```

##### 2.7.3 注入 controller action endpoint

###### 2.7.3.1 ensure controller service

```c#
public static class ControllerEndpointRouteBuilderExtensions
{
    private static void EnsureControllerServices(IEndpointRouteBuilder endpoints)
    {
        var marker = endpoints.ServiceProvider.GetService<MvcMarkerService>();
        
        if (marker == null)
        {
            throw new InvalidOperationException(Resources.FormatUnableToFindServices(
                nameof(IServiceCollection),
                "AddControllers",
                "ConfigureServices(...)"));
        }
    }
}

```

###### 2.7.3.1 get or create data source

```c#
public static class ControllerEndpointRouteBuilderExtensions
{
     private static ControllerActionEndpointDataSource GetOrCreateDataSource(
        IEndpointRouteBuilder endpoints)
    {
        var dataSource = endpoints    	
            .DataSources
            .OfType<ControllerActionEndpointDataSource>()
            .FirstOrDefault();
        
        if (dataSource == null)
        {
            var orderProvider = endpoints
                .ServiceProvider
                .GetRequiredService<OrderedEndpointsSequenceProviderCache>();
            var factory = endpoints
                .ServiceProvider
                .GetRequiredService<ControllerActionEndpointDataSourceFactory>();
            dataSource = factory
                .Create(
                	orderProvider
                		.GetOrCreateOrderedEndpointsSequenceProvider(endpoints));
            endpoints.DataSources.Add(dataSource);
        }
        
        return dataSource;
    }
}
    
```

###### 2.7.3.3 register in cache

```c#
public static class ControllerEndpointRouteBuilderExtensions
{
    private static void RegisterInCache(
        IServiceProvider serviceProvider, 
        ControllerActionEndpointDataSource dataSource)
    {
        var cache = serviceProvider
            .GetRequiredService<DynamicControllerEndpointSelectorCache>();
        cache.AddDataSource(dataSource);
    }
}

```

###### 2.7.3.4 map controller

```c#
public static class ControllerEndpointRouteBuilderExtensions
{ 
     public static ControllerActionEndpointConventionBuilder MapControllers(
        this IEndpointRouteBuilder endpoints)
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }
        
        EnsureControllerServices(endpoints);     
        
        return GetOrCreateDataSource(endpoints).DefaultBuilder;
    }
    
    public static ControllerActionEndpointConventionBuilder MapControllerRoute(
        this IEndpointRouteBuilder endpoints,
        string name,
        string pattern,
        object defaults = null,
        object constraints = null,
        object dataTokens = null)
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }
        
        EnsureControllerServices(endpoints);
        
        var dataSource = GetOrCreateDataSource(endpoints);
        
        return dataSource.AddRoute(
            name,
            pattern,
            new RouteValueDictionary(defaults),
            new RouteValueDictionary(constraints),
            new RouteValueDictionary(dataTokens));
    }
    
    public static ControllerActionEndpointConventionBuilder MapDefaultControllerRoute(
        this IEndpointRouteBuilder endpoints)
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }
        
        EnsureControllerServices(endpoints);
        
        var dataSource = GetOrCreateDataSource(endpoints);
        
        return dataSource.AddRoute(
            "default",
            "{controller=Home}/{action=Index}/{id?}",
            defaults: null,
            constraints: null,
            dataTokens: null);
    }
}
    
```

###### 2.7.3.5 map area controller

```c#
public static class ControllerEndpointRouteBuilderExtensions
{ 
    public static ControllerActionEndpointConventionBuilder MapAreaControllerRoute(
        this IEndpointRouteBuilder endpoints,
        string name,
        string areaName,
        string pattern,
        object defaults = null,
        object constraints = null,
        object dataTokens = null)
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }
        
        if (string.IsNullOrEmpty(areaName))
        {
            throw new ArgumentException(
                Resources.ArgumentCannotBeNullOrEmpty, 
                nameof(areaName));
        }
        
        var defaultsDictionary = new RouteValueDictionary(defaults);
        defaultsDictionary["area"] = defaultsDictionary["area"] ?? areaName;
        
        var constraintsDictionary = new RouteValueDictionary(constraints);
        constraintsDictionary["area"] = constraintsDictionary["area"] 
            ?? new StringRouteConstraint(areaName);
        
        return endpoints
            .MapControllerRoute(
	            name, 
    	        pattern, 
        	    defaultsDictionary, 
            	constraintsDictionary, 
	            dataTokens);
    }
}

```

###### 2.7.3.6 map fallback to controller

```c#
public static class ControllerEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapFallbackToController(
        this IEndpointRouteBuilder endpoints,
        string action,
        string controller)
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }        
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }        
        if (controller == null)
        {
            throw new ArgumentNullException(nameof(controller));
        }
        
        EnsureControllerServices(endpoints);
        
        // Called for side-effect to make sure that the data source is registered.
        var dataSource = GetOrCreateDataSource(endpoints);
        dataSource.CreateInertEndpoints = true;
        RegisterInCache(
            endpoints.ServiceProvider, 
            dataSource);
        
        // Maps a fallback endpoint with an empty delegate. This is OK because
        // we don't expect the delegate to run.
        var builder = endpoints.MapFallback(context => Task.CompletedTask);
        builder.Add(b =>
        	{
                // MVC registers a policy that looks for this metadata.
                b.Metadata.Add(
                    CreateDynamicControllerMetadata(
                        action, 
                        controller, 
                        area: null));
                b.Metadata.Add(
                    new ControllerEndpointDataSourceIdMetadata(dataSource.DataSourceId));
            });
        return builder;
    }

        
    public static IEndpointConventionBuilder MapFallbackToController(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        string action,
        string controller)
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }        
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }        
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }        
        if (controller == null)
        {
            throw new ArgumentNullException(nameof(controller));
        }
        
        EnsureControllerServices(endpoints);
        
        // Called for side-effect to make sure that the data source is registered.
        var dataSource = GetOrCreateDataSource(endpoints);
        dataSource.CreateInertEndpoints = true;
        RegisterInCache(endpoints.ServiceProvider, dataSource);
        
        // Maps a fallback endpoint with an empty delegate. This is OK because
        // we don't expect the delegate to run.
        var builder = endpoints.MapFallback(
            pattern, 
            context => Task.CompletedTask);
        
        builder.Add(b =>
            {
                // MVC registers a policy that looks for this metadata.
                b.Metadata.Add(
                    CreateDynamicControllerMetadata(
                        action, 
                        controller, 
                        area: null));
                b.Metadata.Add(
                    new ControllerEndpointDataSourceIdMetadata(dataSource.DataSourceId));
            });
        return builder;
    }
}

```

###### 2.7.3.7 map fallback to area controller

```c#
public static class ControllerEndpointRouteBuilderExtensions
{ 
    public static IEndpointConventionBuilder MapFallbackToAreaController(
        this IEndpointRouteBuilder endpoints,
        string action,
        string controller,
        string area)
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }        
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }        
        if (controller == null)
        {
            throw new ArgumentNullException(nameof(controller));
        }
        
        EnsureControllerServices(endpoints);
        
        // Called for side-effect to make sure that the data source is registered.
        var dataSource = GetOrCreateDataSource(endpoints);
        dataSource.CreateInertEndpoints = true;
        RegisterInCache(endpoints.ServiceProvider, dataSource);
        
        // Maps a fallback endpoint with an empty delegate. This is OK because
        // we don't expect the delegate to run.
        var builder = endpoints.MapFallback(context => Task.CompletedTask);
        builder.Add(b =>
            {
                // MVC registers a policy that looks for this metadata.
                b.Metadata.Add(
                    CreateDynamicControllerMetadata(
                        action, 
                        controller, 
                        area));
                b.Metadata.Add(
                    new ControllerEndpointDataSourceIdMetadata(dataSource.DataSourceId));
            });
            return builder;
        }

    
    public static IEndpointConventionBuilder MapFallbackToAreaController(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        string action,
        string controller,
        string area)
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }
        if (controller == null)
        {
            throw new ArgumentNullException(nameof(controller));
        }
        
        EnsureControllerServices(endpoints);
        
        // Called for side-effect to make sure that the data source is registered.
        var dataSource = GetOrCreateDataSource(endpoints);
        dataSource.CreateInertEndpoints = true;
        RegisterInCache(endpoints.ServiceProvider, dataSource);
        
        // Maps a fallback endpoint with an empty delegate. This is OK because
        // we don't expect the delegate to run.
        var builder = endpoints.MapFallback(pattern, context => Task.CompletedTask);
        builder.Add(b =>
            {
                // MVC registers a policy that looks for this metadata.
                b.Metadata.Add(
                    CreateDynamicControllerMetadata(
                        action, 
                        controller, 
                        area));
                b.Metadata.Add(
                    new ControllerEndpointDataSourceIdMetadata(dataSource.DataSourceId));
            });
        return builder;
    }
}

```

###### 2.7.3.8 map dynamic controller

```c#
public static class ControllerEndpointRouteBuilderExtensions
{                                                                          
    public static void MapDynamicControllerRoute<TTransformer>(
        this IEndpointRouteBuilder endpoints, 
        string pattern)        
        	where TTransformer : DynamicRouteValueTransformer
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }
        
        MapDynamicControllerRoute<TTransformer>(endpoints, pattern, state: null);
    }
    
    
    public static void MapDynamicControllerRoute<TTransformer>(
        this IEndpointRouteBuilder endpoints, 
        string pattern, 
        object state)            
        	where TTransformer : DynamicRouteValueTransformer
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }
        
        EnsureControllerServices(endpoints);
        
        // Called for side-effect to make sure that the data source is registered.
        var controllerDataSource = GetOrCreateDataSource(endpoints);
                
        RegisterInCache(endpoints.ServiceProvider, controllerDataSource);
        
        // The data source is just used to share the common order 
        // with conventionally routed actions.
        controllerDataSource
            .AddDynamicControllerEndpoint(
            	endpoints, 
            	pattern, 
            	typeof(TTransformer), 
            	state);
    }
        
    public static void MapDynamicControllerRoute<TTransformer>(
        this IEndpointRouteBuilder endpoints, 
        string pattern, 
        object state, 
        int order)            
        	where TTransformer : DynamicRouteValueTransformer
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }
        
        EnsureControllerServices(endpoints);
        
        // Called for side-effect to make sure that the data source is registered.
        var controllerDataSource = GetOrCreateDataSource(endpoints);
                
        RegisterInCache(endpoints.ServiceProvider, controllerDataSource);
        
        // The data source is just used to share the common order 
        // with conventionally routed actions.
        controllerDataSource
            .AddDynamicControllerEndpoint(
            	endpoints, 
            	pattern, 
            	typeof(TTransformer), 
            	state, 
            	order);
    }
    
    private static DynamicControllerMetadata CreateDynamicControllerMetadata(
        string action, 
        string controller, 
        string area)
    {
        return new DynamicControllerMetadata(
            new RouteValueDictionary()
            {               
                { "action", action },
                { "controller", controller },
                { "area", area }
            });
    }               
}

```















