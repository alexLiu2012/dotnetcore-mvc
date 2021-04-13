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
        return new ApplicationBuilder(_serviceProvider, serverFeatures);
    }
}

```





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















