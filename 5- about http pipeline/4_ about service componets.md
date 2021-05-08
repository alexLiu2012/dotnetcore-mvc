## about web host service components





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



### 2. server

#### 2.1 server

```c#
public interface IServer : IDisposable
{    
    // feature 容器
    IFeatureCollection Features { get; }
        
    // start 方法
    Task StartAsync<TContext>(
        IHttpApplication<TContext> application, 
        CancellationToken cancellationToken) 
        	where TContext : notnull;     
    
    // stop 方法
    Task StopAsync(CancellationToken cancellationToken);
}

```

##### 2.1.1 feature collection

###### 2.1.1.1 接口

```c#
public interface IFeatureCollection : IEnumerable<KeyValuePair<Type, object>>
{    
    bool IsReadOnly { get; }        
    int Revision { get; }        
    object? this[Type key] { get; set; }
        
    TFeature? Get<TFeature>();        
    void Set<TFeature>(TFeature? instance);
}

```

###### 2.1.1.2 实现

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

##### 2.1.2 feature reference

###### 2.1.2.1 feature reference

```c#
public struct FeatureReference<T>
{
    public static readonly FeatureReference<T> Default = new FeatureReference<T>(default(T), -1);
    
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

###### 2.1.2.2 feature references

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

#### 2.2 variety of server

##### 2.2.1 kestel

##### 2.2.2 iis

##### 2.2.3 httpsys

#### 2.3 test server

```c#
public class TestServer : IServer
{        
    private bool _disposed = false;
    
    public IServiceProvider Services { get; }        
    public IFeatureCollection Features { get; }   
    
    public Uri BaseAddress { get; set; } = new Uri("http://localhost/");
    public bool AllowSynchronousIO { get; set; }        
    public bool PreserveExecutionContext { get; set; }
        
    private IWebHost? _hostInstance;    
    public IWebHost Host
    {
        get
        {
            return _hostInstance ?? throw new InvalidOperationException(
                "The TestServer constructor was not called with a IWebHostBuilder so IWebHost is not available.");
        }
    }
    
    private ApplicationWrapper? _application;
    private ApplicationWrapper Application
    {
        get => _application ?? throw new InvalidOperationException(
            "The server has not been started or no web application was configured.");
    }
    
    /* 由 service provider 构造 */
    public TestServer(IServiceProvider services) : this(services, new FeatureCollection())
    {
    }
        
    public TestServer(
        IServiceProvider services, 
        IFeatureCollection featureCollection) : 
    	this(
            services, 
            featureCollection, 
            Options.Create(new TestServerOptions()))
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Features = featureCollection ?? throw new ArgumentNullException(nameof(featureCollection));
    }
    
    public TestServer(
        IServiceProvider services, 
        IOptions<TestServerOptions> optionsAccessor) : 
    		this(
                services, 
                new FeatureCollection(), 
                optionsAccessor)
    {
    }
    
    // the real ctor did
    public TestServer(
        IServiceProvider services, 
        IFeatureCollection featureCollection, 
        IOptions<TestServerOptions> optionsAccessor)
    {
        // 注入 service provider
        Services = services ?? throw new ArgumentNullException(nameof(services));
        // 注入 feature collection
        Features = featureCollection ?? throw new ArgumentNullException(nameof(featureCollection));        
        // 注入 test server options
        var options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
        
        // 由 test server options 解析
        AllowSynchronousIO = options.AllowSynchronousIO;
        PreserveExecutionContext = options.PreserveExecutionContext;
        BaseAddress = options.BaseAddress;
    }

    /* 由 web host builder 构建，过时？？？*/                
    public TestServer(IWebHostBuilder builder) : this(builder, new FeatureCollection())
    {
    }
       
    public TestServer(
        IWebHostBuilder builder, 
        IFeatureCollection featureCollection)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        Features = featureCollection ?? throw new ArgumentNullException(nameof(featureCollection));
        
        // use this server, 构建 host
        var host = builder.UseServer(this).Build();
        // 启动 host
        host.StartAsync().GetAwaiter().GetResult();
        
        _hostInstance = host;        
        Services = host.Services;
    }
                                                                                                 
    public async Task<HttpContext> SendAsync(
        Action<HttpContext> configureContext, 
        CancellationToken cancellationToken = default)
    {
        if (configureContext == null)
        {
            throw new ArgumentNullException(nameof(configureContext));
        }
        
        var builder = new HttpContextBuilder(
            Application, 
            AllowSynchronousIO, 
            PreserveExecutionContext);
        
        builder.Configure((context, reader) =>
            {
                var request = context.Request;
                request.Scheme = BaseAddress.Scheme;
                request.Host = HostString.FromUriComponent(BaseAddress);
                if (BaseAddress.IsDefaultPort)
                {
                    request.Host = new HostString(request.Host.Host);
                }
                var pathBase = PathString.FromUriComponent(BaseAddress);
                if (pathBase.HasValue && pathBase.Value.EndsWith('/'))
                {
                    // All but the last character.
                    pathBase = new PathString(pathBase.Value[..^1]); 
                }
                request.PathBase = pathBase;
            });
        
        builder.Configure((context, reader) => configureContext(context));
        // TODO: Wrap the request body if any?
        return await builder.SendAsync(cancellationToken).ConfigureAwait(false);
    }
           
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _hostInstance?.Dispose();
        }
    }                
}

```

##### 2.3.1 test server options

```c#
public class TestServerOptions
{    
    public bool AllowSynchronousIO { get; set; }        
    public bool PreserveExecutionContext { get; set; }        
    public Uri BaseAddress { get; set; } = new Uri("http://localhost/");
}

```

##### 2.3.2 application wrapper

```c#
// 接口
internal abstract class ApplicationWrapper
{
    internal abstract object CreateContext(IFeatureCollection features);    
    internal abstract Task ProcessRequestAsync(object context);    
    internal abstract void DisposeContext(object context, Exception? exception);
}

// 实现
internal class ApplicationWrapper<TContext> : 
	ApplicationWrapper, 
	IHttpApplication<TContext> where TContext : notnull
{
    // 封装 http application 实例
    private readonly IHttpApplication<TContext> _application;
    // pre process request 委托
    private readonly Action _preProcessRequestAsync;
    
    public ApplicationWrapper(
        IHttpApplication<TContext> application, 
        Action preProcessRequestAsync)
    {
        // 注入 http application
        _application = application;
        // 注入 pre process request 委托
        _preProcessRequestAsync = preProcessRequestAsync;
    }
    
    // create context，调用封装的 http application 实例的方法
    internal override object CreateContext(IFeatureCollection features)
    {
        return ((IHttpApplication<TContext>)this).CreateContext(features);
    }
    
    TContext IHttpApplication<TContext>.CreateContext(IFeatureCollection features)
    {
        return _application.CreateContext(features);
    }
    
    // process request，调用封装的 http application 实例的方法
    internal override Task ProcessRequestAsync(object context)
    {
        return ((IHttpApplication<TContext>)this).ProcessRequestAsync((TContext)context);
    }
    
    Task IHttpApplication<TContext>.ProcessRequestAsync(TContext context)
    {
        _preProcessRequestAsync();
        return _application.ProcessRequestAsync(context);
    }
    
    // dispose context，调用封装的 http application 实例的方法
    internal override void DisposeContext(object context, Exception? exception)
    {
        ((IHttpApplication<TContext>)this).DisposeContext((TContext)context, exception);
    }
    
    void IHttpApplication<TContext>.DisposeContext(TContext context, Exception? exception)
    {
        _application.DisposeContext(context, exception);
    }        
}

```

##### 2.3.3 接口方法 

###### 2.3.3.1 start

```c#
public class TestServer : IServer
{
    Task IServer.StartAsync<TContext>(
        IHttpApplication<TContext> application, 
        CancellationToken cancellationToken)
    {
        _application = new ApplicationWrapper<TContext>(
            application, 
            () =>
            	{
                    if (_disposed)
                    {
                        throw new ObjectDisposedException(GetType().FullName);
                    }
                });
        
        return Task.CompletedTask;
    }
}

```

###### 2.3.3.2 stop

```c#
public class TestServer : IServer
{
    Task IServer.StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

```

##### 2.3.4 方法-  http message handler / http client

###### 2.3.4.1 client handler

```c#
public class ClientHandler : HttpMessageHandler
{
    private readonly ApplicationWrapper _application;
    private readonly PathString _pathBase;
        
    internal bool AllowSynchronousIO { get; set; }    
    internal bool PreserveExecutionContext { get; set; }
    
    internal ClientHandler(PathString pathBase, ApplicationWrapper application)
    {
        // 注入 http application wrapper
        _application = application ?? throw new ArgumentNullException(nameof(application));
        
        // 注入 path base（如果以“/”结尾，去掉“/”）       
        if (pathBase.HasValue && pathBase.Value.EndsWith('/'))
        {
            pathBase = new PathString(pathBase.Value[..^1]); 
        }
        _pathBase = pathBase;
    }
        
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        
        // 创建 http context builder
        var contextBuilder = new HttpContextBuilder(_application, AllowSynchronousIO, PreserveExecutionContext);     
        
        /* a- 设置 http context request */
        
        // 从 http request message 解析 content
        var requestContent = request.Content;    
        
        // a1- 如果 content 不为 null，向 http context builder 注册 send stream
        if (requestContent != null)
        {
            // Read content from the request HttpContent into a pipe in a background task. 
            // This will allow the request delegate to start before the request HttpContent is complete. 
            // A background task allows duplex streaming scenarios.
            contextBuilder.SendRequestStream(async writer =>
                {
                    if (requestContent is StreamContent)
                    {
                        // This is odd but required for backwards compat. If StreamContent is passed in then seek to beginning.
                        // This is safe because StreamContent.ReadAsStreamAsync doesn't block. It will return the inner stream.
                        var body = await requestContent.ReadAsStreamAsync();
                        if (body.CanSeek)
                        {
                            // This body may have been consumed before, rewind it.
                            body.Seek(0, SeekOrigin.Begin);
                        }
                        
                        await body.CopyToAsync(writer);
                    }
                    else
                    {
                        await requestContent.CopyToAsync(writer.AsStream());
                    }
                    
                    await writer.CompleteAsync();
                });
        }
        
        // a2- 注册 http context 委托
        contextBuilder.Configure((context, reader) =>
            {
                // 解析 http context 中的 request
                var req = context.Request;
                
                // 设置（http context request）的 http protocol
                if (request.Version == HttpVersion.Version20)
                {                   
                    req.Protocol = HttpProtocol.Http2;
                }
                else
                {
                    req.Protocol = "HTTP/" + request.Version.ToString(fieldCount: 2);
                }
                
                // 设置（http context request）的 http method
                req.Method = request.Method.ToString();
                
                // 设置（http context request）的 http scheme
                req.Scheme = request.RequestUri!.Scheme;
                
                // 设置（http context request）的 body 和 header
                var canHaveBody = false;
                if (requestContent != null)
                {
                    canHaveBody = true;
                    // Chunked takes precedence over Content-Length, don't create a request with both Content-Length and chunked.
                    if (request.Headers.TransferEncodingChunked != true)
                    {
                        // Reading the ContentLength will add it to the Headers‼                       
                        var contentLength = requestContent.Headers.ContentLength;
                        if (!contentLength.HasValue && 
                            request.Version == HttpVersion.Version11)
                        {
                            // HTTP/1.1 requests with a body require either Content-Length or Transfer-Encoding: chunked.
                            request.Headers.TransferEncodingChunked = true;
                        }
                        else if (contentLength == 0)
                        {
                            canHaveBody = false;
                        }
                    }
                    
                    // 遍历 http request message content 中的 header，追加到 http context 的 request 中
                    foreach (var header in requestContent.Headers)
                    {
                        req.Headers.Append(header.Key, header.Value.ToArray());
                    }

                    // 如果（http context request）have body，注入 stream wrapper 
                    if (canHaveBody)
                    {
                        req.Body = new AsyncStreamWrapper(
                            reader.AsStream(), 
                            () => contextBuilder.AllowSynchronousIO);
                    }
                }
                
                // 设置（http context request）的 request body detection feature
                context.Features.Set<IHttpRequestBodyDetectionFeature>(new RequestBodyDetectionFeature(canHaveBody));

                // 遍历 http request message 中的 header，-> 追加到 http context 的 request
                foreach (var header in request.Headers)
                {
                    // User-Agent is a space delineated single line header but HttpRequestHeaders parses it as multiple elements.
                    if (string.Equals(
                        	header.Key, 
                        	HeaderNames.UserAgent, 
                        	StringComparison.OrdinalIgnoreCase))
                    {
                        req.Headers.Append(header.Key, string.Join(" ", header.Value));
                    }
                    else
                    {
                        req.Headers.Append(header.Key, header.Value.ToArray());
                    }
                }

                // 设置（http context request）的 host，
                // 如果 http request message 中没有显示指定，从 request uri 中解析
                if (!req.Host.HasValue)
                {                    
                    req.Host = HostString.FromUriComponent(request.RequestUri);
                    if (request.RequestUri.IsDefaultPort)
                    {
                        req.Host = new HostString(req.Host.Host);
                    }
                }

                // 设置（http context request）的 path、base path、query string
                req.Path = PathString.FromUriComponent(request.RequestUri);                
                req.PathBase = PathString.Empty;
                if (req.Path.StartsWithSegments(_pathBase, out var remainder))
                {
                    req.Path = remainder;
                    req.PathBase = _pathBase;
                }
                
                // 设置（http context request）的 query string
                req.QueryString = QueryString.FromUriComponent(request.RequestUri);
            });
        
        /* b- 发送 request、创建 http context */
        
        // 创建 http response message（预结果）
        var response = new HttpResponseMessage();
        
        // 注册 response read complete 回调
        contextBuilder.RegisterResponseReadCompleteCallback(context =>
            {
                var responseTrailersFeature = context.Features.Get<IHttpResponseTrailersFeature>()!;
                                                                
                foreach (var trailer in responseTrailersFeature.Trailers)
                {
                    bool success = response.TrailingHeaders.TryAddWithoutValidation(
                        trailer.Key, 
                        (IEnumerable<string>)trailer.Value);
                    
                    Contract.Assert(success, "Bad trailer");
                }
            });
        
        // 发送、创建 http context
        var httpContext = await contextBuilder.SendAsync(cancellationToken);
        
        /* c- 配置 http context response */
        
        // 解析 http context 的 statue code，注入 http response message（预结果）
        response.StatusCode = (HttpStatusCode)httpContext.Response.StatusCode;
        
        // 解析 http context 的 reason phrase，注入 http response message（预结果）
        response.ReasonPhrase = httpContext.Features.Get<IHttpResponseFeature>()!.ReasonPhrase;
        
        // 将传入的 http request message 注入 http response message（预结果）
        response.RequestMessage = request;
        
        // 将传入的 http request message 的 version 注入 http response message（预结果）
        response.Version = request.Version;
        
        // 创建 stream content，注入 http response message（预结果）
        response.Content = new StreamContent(httpContext.Response.Body);
        
        // 遍历 http context response 的 header，追加到 http response message（预结果）
        foreach (var header in httpContext.Response.Headers)
        {
            if (!response.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>)header.Value))
            {
                bool success = response.Content.Headers.TryAddWithoutValidation(
                    header.Key, 
                    (IEnumerable<string>)header.Value);
                
                Contract.Assert(success, "Bad header");
            }
        }
        
        return response;
    }
}

```

###### 2.3.4.2 http context builder

```c#
internal class HttpContextBuilder : IHttpBodyControlFeature, IHttpResetFeature
{
    private readonly ApplicationWrapper _application;
    private readonly bool _preserveExecutionContext;
    public bool AllowSynchronousIO { get; set; }
    
    private readonly HttpContext _httpContext;
    
    private readonly Pipe _requestPipe;
    private readonly RequestLifetimeFeature _requestLifetimeFeature;
    
    private readonly TaskCompletionSource<HttpContext> _responseTcs = 
        new TaskCompletionSource<HttpContext>(TaskCreationOptions.RunContinuationsAsynchronously);   
    
    private readonly ResponseBodyReaderStream _responseReaderStream;
    private readonly ResponseBodyPipeWriter _responsePipeWriter;
    private readonly ResponseFeature _responseFeature;    
    private readonly ResponseTrailersFeature _responseTrailersFeature = new ResponseTrailersFeature();
    
    private bool _pipelineFinished;
    private bool _returningResponse;
    private object? _testContext;
    
    
    private Action<HttpContext>? _responseReadCompleteCallback;
    private Task? _sendRequestStreamTask;
    
    internal HttpContextBuilder(
        ApplicationWrapper application, 
        bool allowSynchronousIO, 
        bool preserveExecutionContext)
    {
        // 注入 http application wrapper
        _application = application ?? throw new ArgumentNullException(nameof(application));
        // 注入 allow synchronous io 标志
        AllowSynchronousIO = allowSynchronousIO;
        // 注入 preserve execution context 标志
        _preserveExecutionContext = preserveExecutionContext;
        
        // 创建 http context
        _httpContext = new DefaultHttpContext();
        // 创建 response feature
        _responseFeature = new ResponseFeature(Abort);
        // 创建 response lifetime feature 
        _requestLifetimeFeature = new RequestLifetimeFeature(Abort);
        
        // 设置 http context 的 request 的 protocol = http1，method = get（默认值）
        var request = _httpContext.Request;
        request.Protocol = HttpProtocol.Http11;
        request.Method = HttpMethods.Get;
        
        // 创建 request pipe
        _requestPipe = new Pipe();
        
        // 创建 response pipe
        var responsePipe = new Pipe();
        
        // - 由 response pipe 创建 response body reader stream
        _responseReaderStream = new ResponseBodyReaderStream(
            responsePipe, 
            ClientInitiatedAbort, 
            () => _responseReadCompleteCallback?.Invoke(_httpContext));
        // - 由 response pipe 创建 response body pipe writer
        _responsePipeWriter = new ResponseBodyPipeWriter(
            responsePipe, 
            ReturnResponseMessageAsync);
        
        // 设置 response feature 的 body，-> response body writer stream
        _responseFeature.Body = new ResponseBodyWriterStream(
            _responsePipeWriter, 
            () => AllowSynchronousIO);
        // 设置 response feature 的 body writer，-> response body pipe writer
        _responseFeature.BodyWriter = _responsePipeWriter;
        
        // 注入 feature
        _httpContext.Features.Set<IHttpBodyControlFeature>(this);
        _httpContext.Features.Set<IHttpResponseFeature>(_responseFeature);
        _httpContext.Features.Set<IHttpResponseBodyFeature>(_responseFeature);
        _httpContext.Features.Set<IHttpRequestLifetimeFeature>(_requestLifetimeFeature);
        _httpContext.Features.Set<IHttpResponseTrailersFeature>(_responseTrailersFeature);
    }
    
    // Triggered by request CancellationToken canceling or response stream Disposal.
    internal void ClientInitiatedAbort()
    {
        if (!_pipelineFinished)
        {
            // We don't want to trigger the token for already completed responses.
            _requestLifetimeFeature.Cancel();
        }
        
        // Writes will still succeed, the app will only get an error if they check the CT.
        _responseReaderStream.Abort(new IOException("The client aborted the request."));
        
        // Cancel any pending request async activity when the client aborts a duplex
        // streaming scenario by disposing the HttpResponseMessage.
        CancelRequestBody();
    }
    
    private void CancelRequestBody()
    {
        _requestPipe.Writer.CancelPendingFlush();
        _requestPipe.Reader.CancelPendingRead();
    }
    
    /* 方法 */
    
    // 配置 http context、request pipe reader
    internal void Configure(Action<HttpContext, PipeReader> configureContext)
    {
        if (configureContext == null)
        {
            throw new ArgumentNullException(nameof(configureContext));
        }
        
        configureContext(_httpContext, _requestPipe.Reader);
    }
    
    // 注入 send request stream 委托
    internal void SendRequestStream(Func<PipeWriter, Task> sendRequestStream)
    {
        if (sendRequestStream == null)
        {
            throw new ArgumentNullException(nameof(sendRequestStream));
        }
        
        _sendRequestStreamTask = sendRequestStream(_requestPipe.Writer);
    }
    
    // 注入 response read complete 回调
    internal void RegisterResponseReadCompleteCallback(Action<HttpContext> responseReadCompleteCallback)
    {
        _responseReadCompleteCallback = responseReadCompleteCallback;
    }
    
    // send
    internal Task<HttpContext> SendAsync(CancellationToken cancellationToken)
    {
        // cancel token
        var registration = cancellationToken.Register(ClientInitiatedAbort);
                
        async Task RunRequestAsync()
        {
            // 如果是 http2，-> 设置 http reset feature
            if (HttpProtocol.IsHttp2(_httpContext.Request.Protocol))
            {                
                _httpContext.Features.Set<IHttpResetFeature>(this);
            }
            
            // 由 http application wrapper 创建 http context
            _testContext = _application.CreateContext(_httpContext.Features);
            
            try
            {
                // 执行 http application wrapper 的 process request 方法
                await _application.ProcessRequestAsync(_testContext);
                
                // s1- 
                // 判断 request in process
                var requestBodyInProgress = RequestBodyReadInProgress();
                
                // 如果 request in process = true（还在 request 中），-> cancel request body
                if (requestBodyInProgress)
                {                    
                    CancelRequestBody();
                }
                
                // s2- 
                // Matches Kestrel server: response is completed before request is drained
                await CompleteResponseAsync();
                
                // 如果 request in process = false（request 完成），-> request pipe reader complete
                if (!requestBodyInProgress)
                {
                    // Writer was already completed in send request callback.
                    await _requestPipe.Reader.CompleteAsync();                                        
                }
                
                // 执行 httpapplication wrapper 的 dispose context 方法
                _application.DisposeContext(_testContext, exception: null);
            }
            catch (Exception ex)
            {
                // s3- 
                Abort(ex);
                _application.DisposeContext(_testContext, ex);
            }
            finally
            {
                registration.Dispose();
            }
        }
        
        // Async offload, don't let the test code block the caller.
        if (_preserveExecutionContext)
        {
            _ = Task.Factory.StartNew(RunRequestAsync);
        }
        else
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
                {
                    _ = RunRequestAsync();
                }, null);
        }
        
        return _responseTcs.Task;
    }
    
    // s1- 
    private bool RequestBodyReadInProgress()
    {
        try
        {
            return !_requestPipe.Reader.TryRead(out var result) || !result.IsCompleted;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "An error occurred when completing the request. 
                "Request delegate may have finished while there is a pending read of the request body.", 
                ex);
        }
    }
    
    // s2- 
    internal async Task CompleteResponseAsync()
    {
        _pipelineFinished = true;
        await ReturnResponseMessageAsync();
        _responsePipeWriter.Complete();
        await _responseFeature.FireOnResponseCompletedAsync();
    }
        
    internal async Task ReturnResponseMessageAsync()
    {
        // Check if the response is already returning because the TrySetResult below could happen a bit late
        // (as it happens on a different thread) by which point the CompleteResponseAsync could run and calls this
        // method again.
        if (!_returningResponse)
        {
            _returningResponse = true;
            
            try
            {
                await _responseFeature.FireOnSendingHeadersAsync();
            }
            catch (Exception ex)
            {
                Abort(ex);
                return;
            }
            
            /* 设置 http context 的 feature collection */
            
            // Copy the feature collection so we're not multi-threading on the same collection.
            var newFeatures = new FeatureCollection();
            foreach (var pair in _httpContext.Features)
            {
                newFeatures[pair.Key] = pair.Value;
            }
            
            // 从 http context 中解析 http response feature
            var serverResponseFeature = _httpContext.Features.Get<IHttpResponseFeature>()!;
            // 创建 client response feature
            var clientResponseFeature = new HttpResponseFeature()
            {
                StatusCode = serverResponseFeature.StatusCode,
                ReasonPhrase = serverResponseFeature.ReasonPhrase,
                Headers = serverResponseFeature.Headers,
                Body = _responseReaderStream
            };
            
            // 注入 http response feature
            newFeatures.Set<IHttpResponseFeature>(clientResponseFeature);
            // 注入 http response body feature
            newFeatures.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(_responseReaderStream));
           
            _responseTcs.TrySetResult(new DefaultHttpContext(newFeatures));
        }
    }
        
    // s3- 
    internal void Abort(Exception exception)
    {
        _responsePipeWriter.Abort(exception);
        _responseReaderStream.Abort(exception);
        _requestLifetimeFeature.Cancel();
        _responseTcs.TrySetException(exception);
        CancelRequestBody();
    }   
    
    void IHttpResetFeature.Reset(int errorCode)
    {
        Abort(new HttpResetTestException(errorCode));
    }                        
}

```

###### 2.3.4.3 create http message handler

```c#
public class TestServer : IServer
{     
    public HttpMessageHandler CreateHandler()
    {
        var pathBase = BaseAddress == null 
            ? PathString.Empty 
            : PathString.FromUriComponent(BaseAddress);
        
        return new ClientHandler(pathBase, Application) 
    	    { 
            	AllowSynchronousIO = AllowSynchronousIO, 
            	PreserveExecutionContext = PreserveExecutionContext 
        	};
    }
}

```

###### 2.3.4.4 create http client

```c#
public class TestServer : IServer
{
    public HttpClient CreateClient()
    {
        return new HttpClient(CreateHandler()) 
        	{ 
            	BaseAddress = BaseAddress 
        	};
    }
}

```

##### 2.3.5 方法- about web socket

###### 2.3.5.1 web socket client

```c#
public class WebSocketClient
{
    private readonly ApplicationWrapper _application;
    private readonly PathString _pathBase;
    
    public IList<string> SubProtocols { get; }           
    public Action<HttpRequest>? ConfigureRequest { get; set; }
    
    internal bool AllowSynchronousIO { get; set; }
    internal bool PreserveExecutionContext { get; set; }
    
    internal WebSocketClient(PathString pathBase, ApplicationWrapper application)
    {
        // 注入 http application wrapper
        _application = application ?? throw new ArgumentNullException(nameof(application));
        
        // 注入 path base（如果以“/”结尾，去掉“/”）  
        if (pathBase.HasValue && pathBase.Value.EndsWith('/'))
        {
            pathBase = new PathString(pathBase.Value[..^1]); 
        }
        _pathBase = pathBase;
        
        // 创建 sub protocols 集合（string 集合）
        SubProtocols = new List<string>();
    }
    
    /* 方法- connect */
    public async Task<WebSocket> ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        WebSocketFeature? webSocketFeature = null;
        
        // 创建 http context builder
        var contextBuilder = new HttpContextBuilder(
            _application, 
            AllowSynchronousIO, 
            PreserveExecutionContext);
        
        /* 设置 request */
        
        // 向 http context builder 注入 http context 委托
        contextBuilder.Configure((context, reader) =>
            {
                // 设置 http context 的 request 的 scheme
                var request = context.Request;
                var scheme = uri.Scheme;
                scheme = (scheme == "ws") ? "http" : scheme;
                scheme = (scheme == "wss") ? "https" : scheme;
                request.Scheme = scheme;
                
                // 设置 http context 的 request 的 host
                if (!request.Host.HasValue)
                {
                    request.Host = uri.IsDefaultPort
                        ? new HostString(HostString.FromUriComponent(uri).Host)
                        : HostString.FromUriComponent(uri);
                }
                
                // 设置 http context 的 request 的 path、base path
                request.Path = PathString.FromUriComponent(uri);
                request.PathBase = PathString.Empty;
                if (request.Path.StartsWithSegments(_pathBase, out var remainder))
                {
                    request.Path = remainder;
                    request.PathBase = _pathBase;
                }
                
                // 设置 http context 的 request 的 query string
                request.QueryString = QueryString.FromUriComponent(uri);
                
                // 设置 http context 的 request 的 header
                request.Headers.Add(HeaderNames.Connection, new string[] { "Upgrade" });
                request.Headers.Add(HeaderNames.Upgrade, new string[] { "websocket" });
                request.Headers.Add(HeaderNames.SecWebSocketVersion, new string[] { "13" });
                request.Headers.Add(HeaderNames.SecWebSocketKey, new string[] { CreateRequestKey() });
                
                // 向 http context 的 request 注入 sub protocols
                if (SubProtocols.Any())
                {
                    request.Headers.Add(
                        HeaderNames.SecWebSocketProtocol, 
                        SubProtocols.ToArray());
                }
                
                request.Body = Stream.Null;
                
                // 向 http context 注入 http web socket feature
                webSocketFeature = new WebSocketFeature(context);
                context.Features.Set<IHttpWebSocketFeature>(webSocketFeature);
                
                ConfigureRequest?.Invoke(context.Request);
            });
        
        /* 发送、创建 http context */
        var httpContext = await contextBuilder.SendAsync(cancellationToken);
        
        /* 设置 response */
        if (httpContext.Response.StatusCode != StatusCodes.Status101SwitchingProtocols)
        {
            throw new InvalidOperationException(
                "Incomplete handshake, status code: " + httpContext.Response.StatusCode);
        }
        
        Debug.Assert(webSocketFeature != null);
        if (webSocketFeature.ClientWebSocket == null)
        {
            throw new InvalidOperationException("Incomplete handshake");
        }
        
        // 返回 web socket feature 的 client web socket
        // （web socket feature 被注入到 http context 中，在 http pipeline 被处理过）
        return webSocketFeature.ClientWebSocket;
    }
    
    private string CreateRequestKey()
    {
        byte[] data = new byte[16];
        RandomNumberGenerator.Fill(data);
        return Convert.ToBase64String(data);
    }
    
    private class WebSocketFeature : IHttpWebSocketFeature
    {
        private readonly HttpContext _httpContext;
        
        public WebSocketFeature(HttpContext context)
        {
            _httpContext = context;
        }
        
        bool IHttpWebSocketFeature.IsWebSocketRequest => true;
        
        public WebSocket? ClientWebSocket { get; private set; }        
        public WebSocket? ServerWebSocket { get; private set; }
        
        async Task<WebSocket> IHttpWebSocketFeature.AcceptAsync(WebSocketAcceptContext context)
        {
            var websockets = TestWebSocket.CreatePair(context.SubProtocol);
            
            if (_httpContext.Response.HasStarted)
            {
                throw new InvalidOperationException("The response has already started");
            }
            
            _httpContext.Response.StatusCode = StatusCodes.Status101SwitchingProtocols;
            ClientWebSocket = websockets.Item1;
            ServerWebSocket = websockets.Item2;
            
            await _httpContext.Response.Body.FlushAsync(_httpContext.RequestAborted); 
            // Send headers to the client
            return ServerWebSocket;
        }
    }
}

```

###### 2.3.5.2 test web socket

```c#
internal class TestWebSocket : WebSocket
{    
    // buffer
    private readonly ReceiverSenderBuffer _receiveBuffer;
    private readonly ReceiverSenderBuffer _sendBuffer;
        
    // message
    private Message? _receiveMessage;
    
    // sub protocol
    private readonly string? _subProtocol;
    public override string? SubProtocol
    {
        get { return _subProtocol; }
    }
    
    // web socket state
    private WebSocketState _state;
    public override WebSocketState State
    {
        get { return _state; }
    }
    
    // web socket close status
    private WebSocketCloseStatus? _closeStatus;
    public override WebSocketCloseStatus? CloseStatus
    {
        get { return _closeStatus; }
    }
    
    // (web socket) close status description
    private string? _closeStatusDescription;
    public override string? CloseStatusDescription
    {
        get { return _closeStatusDescription; }
    }
    
    /* 方法- 创建 client - server pair（sender buffer - receivebuffer）*/            
    public static Tuple<TestWebSocket, TestWebSocket> CreatePair(string? subProtocol)
    {
        var buffers = new[] { new ReceiverSenderBuffer(), new ReceiverSenderBuffer() };
        return Tuple.Create(
            new TestWebSocket(subProtocol, buffers[0], buffers[1]),
            new TestWebSocket(subProtocol, buffers[1], buffers[0]));
    }
    // 私有 ctor
    private TestWebSocket(
        string? subProtocol, 
        ReceiverSenderBuffer readBuffer, 
        ReceiverSenderBuffer writeBuffer)
    {
        // 设置 web socket state 为 open
        _state = WebSocketState.Open;
        
        // 注入 sup protocols
        _subProtocol = subProtocol;
        // 注入 receive buffer（read）
        _receiveBuffer = readBuffer;
        // 注入 send buffer（write）
        _sendBuffer = writeBuffer;
    }
    
                
    public async override Task CloseAsync(
        WebSocketCloseStatus closeStatus, 
        string? statusDescription, 
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        
        if (State == WebSocketState.Open || State == WebSocketState.CloseReceived)
        {
            // Send a close message.
            await CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
        }
        
        if (State == WebSocketState.CloseSent)
        {
            // Do a receiving drain
            var data = new byte[1024];
            WebSocketReceiveResult result;
            do
            {
                result = await ReceiveAsync(new ArraySegment<byte>(data), cancellationToken);
            }
            while (result.MessageType != WebSocketMessageType.Close);
        }
    }
    
    public async override Task CloseOutputAsync(
        WebSocketCloseStatus closeStatus, 
        string? statusDescription, 
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ThrowIfOutputClosed();
        
        var message = new Message(closeStatus, statusDescription);
        await _sendBuffer.SendAsync(message, cancellationToken);
        
        if (State == WebSocketState.Open)
        {
            _state = WebSocketState.CloseSent;
        }
        else if (State == WebSocketState.CloseReceived)
        {
            _state = WebSocketState.Closed;
            Close();
        }
    }
    
    public override void Abort()
    {
        if (_state >= WebSocketState.Closed) // or Aborted
        {
            return;
        }
        
        _state = WebSocketState.Aborted;
        Close();
    }
    
    public override void Dispose()
    {
        if (_state >= WebSocketState.Closed) // or Aborted
        {
            return;
        }
        
        _state = WebSocketState.Closed;
        Close();
    }
    
    public override async Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer, 
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ThrowIfInputClosed();
        ValidateSegment(buffer);
        // TODO: InvalidOperationException if any receives are currently in progress.
        
        Message? receiveMessage = _receiveMessage;
        _receiveMessage = null;
        if (receiveMessage == null)
        {
            receiveMessage = await _receiveBuffer.ReceiveAsync(cancellationToken);
        }
        if (receiveMessage.MessageType == WebSocketMessageType.Close)
        {
            _closeStatus = receiveMessage.CloseStatus;
            _closeStatusDescription = receiveMessage.CloseStatusDescription ?? string.Empty;
            var result = new WebSocketReceiveResult(
                0, 
                WebSocketMessageType.Close, 
                true, 
                _closeStatus, 
                _closeStatusDescription);
            
            if (_state == WebSocketState.Open)
            {
                _state = WebSocketState.CloseReceived;
            }
            else if (_state == WebSocketState.CloseSent)
            {
                _state = WebSocketState.Closed;
                Close();
            }
            return result;
        }
        else
        {
            int count = Math.Min(buffer.Count, receiveMessage.Buffer.Count);
            bool endOfMessage = count == receiveMessage.Buffer.Count;
            Array.Copy(
                receiveMessage.Buffer.Array!, 
                receiveMessage.Buffer.Offset, 
                buffer.Array!, 
                buffer.Offset, 
                count);
            if (!endOfMessage)
            {
                receiveMessage.Buffer = new ArraySegment<byte>(
                    receiveMessage.Buffer.Array!, 
                    receiveMessage.Buffer.Offset + count, 
                    receiveMessage.Buffer.Count - count);
                
                _receiveMessage = receiveMessage;
            }
            endOfMessage = endOfMessage && receiveMessage.EndOfMessage;
            return new WebSocketReceiveResult(count, receiveMessage.MessageType, endOfMessage);
        }
    }
    
    public override Task SendAsync(
        ArraySegment<byte> buffer, 
        WebSocketMessageType messageType, 
        bool endOfMessage, 
        CancellationToken cancellationToken)
    {
        ValidateSegment(buffer);
        if (messageType != WebSocketMessageType.Binary && messageType != WebSocketMessageType.Text)
        {
            // Block control frames
            throw new ArgumentOutOfRangeException(nameof(messageType), messageType, string.Empty);
        }
        
        var message = new Message(buffer, messageType, endOfMessage);
        return _sendBuffer.SendAsync(message, cancellationToken);
    }
    
    private void Close()
    {
        _receiveBuffer.SetReceiverClosed();
        _sendBuffer.SetSenderClosed();
    }
    
    private void ThrowIfDisposed()
    {
        if (_state >= WebSocketState.Closed) // or Aborted
        {
            throw new ObjectDisposedException(typeof(TestWebSocket).FullName);
        }
    }
    
    private void ThrowIfOutputClosed()
    {
        if (State == WebSocketState.CloseSent)
        {
            throw new InvalidOperationException("Close already sent.");
        }
    }
        
    private void ThrowIfInputClosed()
    {
        if (State == WebSocketState.CloseReceived)
        {
            throw new InvalidOperationException("Close already received.");
        }
    }
    
    private void ValidateSegment(ArraySegment<byte> buffer)
    {
        if (buffer.Array == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }
        if (buffer.Offset < 0 || buffer.Offset > buffer.Array.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(buffer.Offset), buffer.Offset, string.Empty);
        }
        if (buffer.Count < 0 || buffer.Count > buffer.Array.Length - buffer.Offset)
        {
            throw new ArgumentOutOfRangeException(nameof(buffer.Count), buffer.Count, string.Empty);
        }
    }
    
    
    // message class
    private class Message
    {
        public WebSocketCloseStatus? CloseStatus { get; set; }
        public string? CloseStatusDescription { get; set; }
        public ArraySegment<byte> Buffer { get; set; }
        public bool EndOfMessage { get; set; }
        public WebSocketMessageType MessageType { get; set; }
        
        public Message(
            ArraySegment<byte> buffer, 
            WebSocketMessageType messageType, 
            bool endOfMessage)
        {
            Buffer = buffer;
            CloseStatus = null;
            CloseStatusDescription = null;
            EndOfMessage = endOfMessage;
            MessageType = messageType;
        }
        
        public Message(
            WebSocketCloseStatus? closeStatus, 
            string? closeStatusDescription)
        {
            Buffer = new ArraySegment<byte>(Array.Empty<byte>());
            CloseStatus = closeStatus;
            CloseStatusDescription = closeStatusDescription;
            MessageType = WebSocketMessageType.Close;
            EndOfMessage = true;
        }                
    }
    
    // receiver sender buffer class
    private class ReceiverSenderBuffer
    {
        private bool _receiverClosed;
        private bool _senderClosed;
        private bool _disposed;
        private readonly SemaphoreSlim _sem;
        private readonly Queue<Message> _messageQueue;
        
        public ReceiverSenderBuffer()
        {
            _sem = new SemaphoreSlim(0);
            _messageQueue = new Queue<Message>();
        }
        
        public async virtual Task<Message> ReceiveAsync(CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                ThrowNoReceive();
            }
            
            await _sem.WaitAsync(cancellationToken);
            
            lock (_messageQueue)
            {
                if (_messageQueue.Count == 0)
                {
                    _disposed = true;
                    _sem.Dispose();
                    ThrowNoReceive();
                }
                
                return _messageQueue.Dequeue();
            }
        }
        
        public virtual Task SendAsync(Message message, CancellationToken cancellationToken)
        {
            lock (_messageQueue)
            {
                if (_senderClosed)
                {
                    throw new ObjectDisposedException(typeof(TestWebSocket).FullName);
                }
                if (_receiverClosed)
                {
                    throw new IOException(
                        "The remote end closed the connection.", 
                        new ObjectDisposedException(typeof(TestWebSocket).FullName));
                }
                
                // we return immediately so we need to copy the buffer since the sender can re-use it
                var array = new byte[message.Buffer.Count];
                Array.Copy(
                    message.Buffer.Array!, 
                    message.Buffer.Offset, 
                    array, 
                    0, 
                    message.Buffer.Count);
                
                message.Buffer = new ArraySegment<byte>(array);
                
                _messageQueue.Enqueue(message);
                _sem.Release();
                
                return Task.FromResult(true);
            }
        }
        
        public void SetReceiverClosed()
        {
            lock (_messageQueue)
            {
                if (!_receiverClosed)
                {
                    _receiverClosed = true;
                    if (!_disposed)
                    {
                        _sem.Release();
                    }
                }
            }
        }
        
        public void SetSenderClosed()
        {
            lock (_messageQueue)
            {
                if (!_senderClosed)
                {
                    _senderClosed = true;
                    if (!_disposed)
                    {
                        _sem.Release();
                    }
                }
            }
        }
        
        private void ThrowNoReceive()
        {
            if (_receiverClosed)
            {
                throw new ObjectDisposedException(typeof(TestWebSocket).FullName);
            }
            else // _senderClosed
            {
                throw new IOException(
                    "The remote end closed the connection.", 
                    new ObjectDisposedException(typeof(TestWebSocket).FullName));
            }
        }
    }
}

```

###### 2.3.5.3 create web socket client

```c#
public class TestServer : IServer
{
    public WebSocketClient CreateWebSocketClient()
    {
        var pathBase = BaseAddress == null 
            ? PathString.Empty 
            : PathString.FromUriComponent(BaseAddress);
        
        return new WebSocketClient(pathBase, Application) 
        	{ 
            	AllowSynchronousIO = AllowSynchronousIO,
            	PreserveExecutionContext = PreserveExecutionContext 
        	};
    }
}

```

##### 2.3.6 方法- about request builder

###### 2.3.6.1 request builder

* 强类型封装的 request

```c#
public class RequestBuilder
{
    private readonly HttpRequestMessage _req;
    public TestServer TestServer { get; }
        
    public RequestBuilder(TestServer server, string path)
    {
        // 注入 test server
        TestServer = server ?? throw new ArgumentNullException(nameof(server));
        // 创建 http request message
        _req = new HttpRequestMessage(HttpMethod.Get, path);
    }
          
    // 方法- 配置 http request message
    public RequestBuilder And(Action<HttpRequestMessage> configure)
    {
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }
        
        configure(_req);
        return this;
    }
        
    // 方法- add header
    public RequestBuilder AddHeader(string name, string value)
    {
        if (!_req.Headers.TryAddWithoutValidation(name, value))
        {
            if (_req.Content == null)
            {
                _req.Content = new StreamContent(Stream.Null);
            }
            if (!_req.Content.Headers.TryAddWithoutValidation(name, value))
            {
                // TODO: 
                // throw new ArgumentException(
                // 	   string.Format(CultureInfo.CurrentCulture, Resources.InvalidHeaderName, name), 
                // 	   "name");
                throw new ArgumentException("Invalid header name: " + name, nameof(name));
            }
        }
        return this;
    }
    
    // send
    public Task<HttpResponseMessage> SendAsync(string method)
    {
        _req.Method = new HttpMethod(method);
        return TestServer.CreateClient().SendAsync(_req);
    }
    // get    
    public Task<HttpResponseMessage> GetAsync()
    {
        _req.Method = HttpMethod.Get;
        return TestServer.CreateClient().SendAsync(_req);
    }
    // post
    public Task<HttpResponseMessage> PostAsync()
    {
        _req.Method = HttpMethod.Post;
        return TestServer.CreateClient().SendAsync(_req);
    }
}

```

###### 2.3.6.2 create request builder

```c#
public class TestServer : IServer
{
    public RequestBuilder CreateRequest(string path)
    {
        return new RequestBuilder(this, path);
    }
}

```

##### 2.3.7 扩展方法 in "generic host"

###### 2.3.7.1 use test server

```c#
public static class WebHostBuilderExtensions
{     
    public static IWebHostBuilder UseTestServer(this IWebHostBuilder builder)
    {
        return builder.ConfigureServices(services =>
            {
                services.AddSingleton<IHostLifetime, NoopHostLifetime>();
                services.AddSingleton<IServer, TestServer>();
            });
    }
        
    public static IWebHostBuilder UseTestServer(
        this IWebHostBuilder builder, 
        Action<TestServerOptions> configureOptions)
    {
        return builder.ConfigureServices(services =>
            {
                services.Configure(configureOptions);
                services.AddSingleton<IHostLifetime, NoopHostLifetime>();
                services.AddSingleton<IServer, TestServer>();
            });
    }
}

```

###### 2.3.7.2 get test server

```c#
/* obsolete */
public static class WebHostBuilderExtensions
{
    /* not for generic web host service */
    public static TestServer GetTestServer(this IWebHost host)
    {
        return (TestServer)host.Services.GetRequiredService<IServer>();
    }
}

// for generic host
public static class HostBuilderTestServerExtensions
{    
    public static TestServer GetTestServer(this IHost host)
    {
        return (TestServer)host.Services.GetRequiredService<IServer>();
    }        
}

```

###### 2.3.7.3 get test client 

```c#
/* obsolete */
public static class WebHostBuilderExtensions
{
    /* not for generic web host service */   
    public static HttpClient GetTestClient(this IWebHost host)
    {
        return host.GetTestServer().CreateClient();
    }            
}

// for generic host
public static class HostBuilderTestServerExtensions
{        
    public static HttpClient GetTestClient(this IHost host)
    {
        return host.GetTestServer().CreateClient();
    }
}

```

###### 2.3.7.4 configure service

```c#
public static class WebHostBuilderExtensions
{
    public static IWebHostBuilder ConfigureTestServices(
        this IWebHostBuilder webHostBuilder, 
        Action<IServiceCollection> servicesConfiguration)
    {
        if (webHostBuilder == null)
        {
            throw new ArgumentNullException(nameof(webHostBuilder));
        }        
        if (servicesConfiguration == null)
        {
            throw new ArgumentNullException(nameof(servicesConfiguration));
        }
        
        // 如果是 generic web host builder，
        if (webHostBuilder.GetType().Name.Equals("GenericWebHostBuilder"))
        {            
            webHostBuilder.ConfigureServices(servicesConfiguration);
        }
        // * (obsolete) 否则，注入 startup configure service filter *
        else
        {            
            webHostBuilder.ConfigureServices(
                s => s.AddSingleton<IStartupConfigureServicesFilter>(
                    new ConfigureTestServicesStartupConfigureServicesFilter(servicesConfiguration)));            
        }
        
        return webHostBuilder;
    }
    
    // (obsolete) configure servcie filter
    private class ConfigureTestServicesStartupConfigureServicesFilter : IStartupConfigureServicesFilter        
    {
        private readonly Action<IServiceCollection> _servicesConfiguration;
        
        public ConfigureTestServicesStartupConfigureServicesFilter(Action<IServiceCollection> servicesConfiguration)
        {
            if (servicesConfiguration == null)
            {
                throw new ArgumentNullException(nameof(servicesConfiguration));
            }
            
            _servicesConfiguration = servicesConfiguration;
        }
        
        public Action<IServiceCollection> ConfigureServices(Action<IServiceCollection> next) =>
            serviceCollection =>
                {
                    next(serviceCollection);
                    _servicesConfiguration(serviceCollection);
                };
        }
}

```

###### 2.3.7.5 configure service container

* obsolete

```c#
public static class WebHostBuilderExtensions
{
    public static IWebHostBuilder ConfigureTestContainer<TContainer>(
        this IWebHostBuilder webHostBuilder, 
        Action<TContainer> servicesConfiguration)
    {
        if (webHostBuilder == null)
        {
            throw new ArgumentNullException(nameof(webHostBuilder));
        }        
        if (servicesConfiguration == null)
        {
            throw new ArgumentNullException(nameof(servicesConfiguration));
        }
       
        // * （obsolete) 注入 startup configure container filter *
        webHostBuilder.ConfigureServices(
            s => s.AddSingleton<IStartupConfigureContainerFilter<TContainer>>(
                new ConfigureTestServicesStartupConfigureContainerFilter<TContainer>(servicesConfiguration)));
        
        return webHostBuilder;
    }
    
    // configure container filter
    private class ConfigureTestServicesStartupConfigureContainerFilter<TContainer> : 
    	IStartupConfigureContainerFilter<TContainer>        
    {
        private readonly Action<TContainer> _servicesConfiguration;
        
        public ConfigureTestServicesStartupConfigureContainerFilter(Action<TContainer> containerConfiguration)
        {
            if (containerConfiguration == null)
            {
                throw new ArgumentNullException(nameof(containerConfiguration));
            }
            
            _servicesConfiguration = containerConfiguration;
        }
        
        public Action<TContainer> ConfigureContainer(Action<TContainer> next) =>
            containerBuilder =>
                {
            		next(containerBuilder);
            		_servicesConfiguration(containerBuilder);
                };
    }
}

```

###### 2.3.7.6 content root

```c#
public static class WebHostBuilderExtensions
{                                              
    [SuppressMessage(
        "ApiDesign", 
        "RS0026:Do not add multiple public overloads with optional parameters", 
        Justification = "Required to maintain compatibility")]
    public static IWebHostBuilder UseSolutionRelativeContentRoot(
        this IWebHostBuilder builder,
        string solutionRelativePath,
        string solutionName = "*.sln")
    {
        return builder.UseSolutionRelativeContentRoot(
            solutionRelativePath, 
            AppContext.BaseDirectory, 
            solutionName);
    }
        
    [SuppressMessage(
        "ApiDesign", 
        "RS0026:Do not add multiple public overloads with optional parameters", 
        Justification = "Required to maintain compatibility")]
    public static IWebHostBuilder UseSolutionRelativeContentRoot(
        this IWebHostBuilder builder,
        string solutionRelativePath,
        string applicationBasePath,
        string solutionName = "*.sln")
    {
        if (solutionRelativePath == null)
        {
            throw new ArgumentNullException(nameof(solutionRelativePath));
        }        
        if (applicationBasePath == null)
        {
            throw new ArgumentNullException(nameof(applicationBasePath));
        }
        
        var directoryInfo = new DirectoryInfo(applicationBasePath);
        
        do
        {
            var solutionPath = Directory.EnumerateFiles(directoryInfo.FullName, solutionName).FirstOrDefault();
            if (solutionPath != null)
            {
                builder.UseContentRoot(
                    Path.GetFullPath(Path.Combine(directoryInfo.FullName, solutionRelativePath)));
                return builder;
            }
            
            directoryInfo = directoryInfo.Parent;
        }
        while (directoryInfo!.Parent != null);
        
        throw new InvalidOperationException(
            $"Solution root could not be located using application root {applicationBasePath}.");
    }    	        
}

```

### 2. http

#### 2.1 header dictionary

```c#
public interface IHeaderDictionary : IDictionary<string, StringValues>
{    
    new StringValues this[string key] { get; set; }        
    long? ContentLength { get; set; }
}

```

##### 2.1.1 header dictionary

```c#
public class HeaderDictionary : IHeaderDictionary
{
    // empty keys & values
    private static readonly string[] EmptyKeys = Array.Empty<string>();
    private static readonly StringValues[] EmptyValues = Array.Empty<StringValues>();
    
    public int Count => Store?.Count ?? 0;
    public bool IsReadOnly { get; set; }
    
    public long? ContentLength
    {
        get
        {
            long value;
            var rawValue = this[HeaderNames.ContentLength];
            if (rawValue.Count == 1 &&
                !string.IsNullOrEmpty(rawValue[0]) &&
                HeaderUtilities.TryParseNonNegativeInt64(new StringSegment(rawValue[0]).Trim(), out value))
            {
                return value;
            }
            
            return null;
        }
        set
        {
            ThrowIfReadOnly();
            if (value.HasValue)
            {
                this[HeaderNames.ContentLength] = HeaderUtilities.FormatNonNegativeInt64(value.GetValueOrDefault());
            }
            else
            {
                this.Remove(HeaderNames.ContentLength);
            }
        }
    }
    
    public ICollection<string> Keys
    {
        get
        {
            if (Store == null)
            {
                return EmptyKeys;
            }
            return Store.Keys;
        }
    }
        
    public ICollection<StringValues> Values
    {
        get
        {
            if (Store == null)
            {
                return EmptyValues;
            }
            return Store.Values;
        }
    }
    
    public StringValues this[string key]
    {
        get
        {
            if (Store == null)
            {
                return StringValues.Empty;
            }
            
            if (TryGetValue(key, out StringValues value))
            {
                return value;
            }
            return StringValues.Empty;
        }
        set
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            ThrowIfReadOnly();
            
            if (value.Count == 0)
            {
                Store?.Remove(key);
            }
            else
            {
                EnsureStore(1);
                Store[key] = value;
            }
        }
    }
    
    StringValues IDictionary<string, StringValues>.this[string key]
    {
        get { return this[key]; }
        set
        {
            ThrowIfReadOnly();
            this[key] = value;
        }
    }
                                    
    //  真正存储数据的 container
    private Dictionary<string, StringValues>? Store { get; set; }
    	              
    // 构造
    public HeaderDictionary()
    {
    }
                
    public HeaderDictionary(Dictionary<string, StringValues>? store)
    {
        Store = store;        
    }
                    
    public HeaderDictionary(int capacity)
    {
        EnsureStore(capacity);
    }   
    
    [MemberNotNull(nameof(Store))]
    private void EnsureStore(int capacity)
    {
        if (Store == null)
        {
            Store = new Dictionary<string, StringValues>(capacity, StringComparer.OrdinalIgnoreCase);
        }
    }       
        
    // empty enumerator 
    private static readonly Enumerator EmptyEnumerator = new Enumerator();    
    private static readonly IEnumerator<KeyValuePair<string, StringValues>> EmptyIEnumeratorType = EmptyEnumerator;
    private static readonly IEnumerator EmptyIEnumerator = EmptyEnumerator;
    
    public Enumerator GetEnumerator()
    {
        if (Store == null || Store.Count == 0)
        {
            // Non-boxed Enumerator
            return EmptyEnumerator;
        }
        return new Enumerator(Store.GetEnumerator());
    }
        
    IEnumerator<KeyValuePair<string, StringValues>> IEnumerable<KeyValuePair<string, StringValues>>.GetEnumerator()
    {
        if (Store == null || Store.Count == 0)
        {
            // Non-boxed Enumerator
            return EmptyIEnumeratorType;
        }
        return Store.GetEnumerator();
    }
        
    IEnumerator IEnumerable.GetEnumerator()
    {
        if (Store == null || Store.Count == 0)
        {
            // Non-boxed Enumerator
            return EmptyIEnumerator;
        }
        return Store.GetEnumerator();
    }
    
    // enumerator 结构体
    public struct Enumerator : IEnumerator<KeyValuePair<string, StringValues>>
    {
        // Do NOT make this readonly, or MoveNext will not work
        private Dictionary<string, StringValues>.Enumerator _dictionaryEnumerator;
        private bool _notEmpty;
        
        object IEnumerator.Current
        {
            get
            {
                return Current;
            }
        }
        
        internal Enumerator(
            Dictionary<string, StringValues>.Enumerator dictionaryEnumerator)
        {
            _dictionaryEnumerator = dictionaryEnumerator;
            _notEmpty = true;
        }
                
        public bool MoveNext()
        {
            if (_notEmpty)
            {
                return _dictionaryEnumerator.MoveNext();
            }
            return false;
        }
                
        public KeyValuePair<string, StringValues> Current
        {
            get
            {
                if (_notEmpty)
                {
                    return _dictionaryEnumerator.Current;
                }
                return default(KeyValuePair<string, StringValues>);
            }
        }
                
        public void Dispose()
        {
        }
                        
        void IEnumerator.Reset()
        {
            if (_notEmpty)
            {
                ((IEnumerator)_dictionaryEnumerator).Reset();
            }
        }
    }
    
    
    private void ThrowIfReadOnly()
    {
        if (IsReadOnly)
        {
            throw new InvalidOperationException(
                "The response headers cannot be modified because the response has already started.");
        }
    }    
}

```

###### 2.1.1.1 方法 - crud

```c#
public class HeaderDictionary : IHeaderDictionary
{        
    /* add */    
    public void Add(KeyValuePair<string, StringValues> item)
    {
        if (item.Key == null)
        {
            throw new ArgumentNullException("The key is null");
        }
        ThrowIfReadOnly();
        EnsureStore(1);
        Store.Add(item.Key, item.Value);
    }
        
    public void Add(string key, StringValues value)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }
        ThrowIfReadOnly();
        EnsureStore(1);
        Store.Add(key, value);
    }
        
    /* clear */
    public void Clear()
    {
        ThrowIfReadOnly();
        Store?.Clear();
    }
       
    /* contain */    
    public bool Contains(KeyValuePair<string, StringValues> item)
    {
        if (Store == null ||
            !Store.TryGetValue(item.Key, out StringValues value) ||
            !StringValues.Equals(value, item.Value))
        {
            return false;
        }
        return true;
    }
                
    public bool ContainsKey(string key)
    {
        if (Store == null)
        {
            return false;
        }
        return Store.ContainsKey(key);
    }
        
    /* remove */        
    public bool Remove(KeyValuePair<string, StringValues> item)
    {
        ThrowIfReadOnly();
        if (Store == null)
        {
            return false;
        }
        
        if (Store.TryGetValue(item.Key, out var value) && 
            StringValues.Equals(item.Value, value))
        {
            return Store.Remove(item.Key);
        }
        return false;
    }
        
    public bool Remove(string key)
    {
        ThrowIfReadOnly();
        if (Store == null)
        {
            return false;
        }
        return Store.Remove(key);
    }
        
    /* get */
    public bool TryGetValue(
        string key, 
        out StringValues value)
    {
        if (Store == null)
        {
            value = default(StringValues);
            return false;
        }
        return Store.TryGetValue(key, out value);
    }
    
    /* copy (clone) */
    public void CopyTo(
        KeyValuePair<string, StringValues>[] array, 
        int arrayIndex)
    {
        if (Store == null)
        {
            return;
        }
        
        foreach (var item in Store)
        {
            array[arrayIndex] = item;
            arrayIndex++;
        }
    }
}

```

###### 2.1.1.2 know parser

```c#
public static class HeaderDictionaryTypeExtensions
{
    private static IDictionary<Type, object> KnownParsers = new Dictionary<Type, object>()
    {
        { 
            typeof(CacheControlHeaderValue), 
            new Func<string, CacheControlHeaderValue?>(value => 
            	{ 
                    return CacheControlHeaderValue.TryParse(value, out var result) ? result : null; 
                }) 
        },
        { 
            typeof(ContentDispositionHeaderValue), 
           	new Func<string, ContentDispositionHeaderValue?>(value => 
            	{ 
                    return ContentDispositionHeaderValue.TryParse(value, out var result) ? result : null; 
                }) 
        },
        {
            typeof(ContentRangeHeaderValue), 
            new Func<string, ContentRangeHeaderValue?>(value => 
            	{ 
                    return ContentRangeHeaderValue.TryParse(value, out var result) ? result : null; 
                }) 
        },
        { 
            typeof(MediaTypeHeaderValue), 
            new Func<string, MediaTypeHeaderValue?>(value => 
            	{ 
                    return MediaTypeHeaderValue.TryParse(value, out var result) ? result : null; 
                }) 
        },
        { 
            typeof(RangeConditionHeaderValue), 
            new Func<string, RangeConditionHeaderValue?>(value => 
            	{ 
                    return RangeConditionHeaderValue.TryParse(value, out var result) ? result : null; 
                }) 
        },
        { 
            typeof(RangeHeaderValue), 
            new Func<string, RangeHeaderValue?>(value => 
            	{ 
                    return RangeHeaderValue.TryParse(value, out var result) ? result : null; 
                }) 
        },
        { 
            typeof(EntityTagHeaderValue), 
            new Func<string, EntityTagHeaderValue?>(value => 
            	{ 
                    return EntityTagHeaderValue.TryParse(value, out var result) ? result : null; 
                }) 
        },
        { 
            typeof(DateTimeOffset?), 
            new Func<string, DateTimeOffset?>(value => 
            	{ 
                    return HeaderUtilities.TryParseDate(value, out var result) ? result : null; 
                }) 
        },
        { 
            typeof(long?), 
            new Func<string, long?>(value => 
            	{ 
                    return HeaderUtilities.TryParseNonNegativeInt64(value, out var result) ? result : null; 
                }) 
        },
    };        
}

```

###### 2.1.1.3 know list parser

```c#
public static class HeaderDictionaryTypeExtensions
{        
    private static IDictionary<Type, object> KnownListParsers = new Dictionary<Type, object>()
    {
        { 
            typeof(MediaTypeHeaderValue), 
            new Func<IList<string>, IList<MediaTypeHeaderValue>>(value => 
            	{ 
                    return MediaTypeHeaderValue.TryParseList(value, out var result) 
                        ? result 
                        : Array.Empty<MediaTypeHeaderValue>(); 
                })  
        },
        { 
            typeof(StringWithQualityHeaderValue), 
            new Func<IList<string>, IList<StringWithQualityHeaderValue>>(value => 
            	{ 
                    return StringWithQualityHeaderValue.TryParseList(value, out var result) 
                        ? result 
                        : Array.Empty<StringWithQualityHeaderValue>(); 
                })  
        },
        { 
            typeof(CookieHeaderValue), 
            new Func<IList<string>, IList<CookieHeaderValue>>(value => 
                { 
                    return CookieHeaderValue.TryParseList(value, out var result) 
                        ? result 
                        : Array.Empty<CookieHeaderValue>(); 
                })  
        },
        { 
            typeof(EntityTagHeaderValue), 
            new Func<IList<string>, IList<EntityTagHeaderValue>>(value => 
                { 
                    return EntityTagHeaderValue.TryParseList(value, out var result) 
                        ? result 
                        : Array.Empty<EntityTagHeaderValue>(); 
                })  
        },
        { 
            typeof(SetCookieHeaderValue), 
            new Func<IList<string>, IList<SetCookieHeaderValue>>(value => 
                { 
                    return SetCookieHeaderValue.TryParseList(value, out var result) 
                        ? result 
                        : Array.Empty<SetCookieHeaderValue>(); 
                })  
        },
    };        
}

```

###### 2.1.1.4 扩展方法 - get

```c#
public static class HeaderDictionaryTypeExtensions
{
    /* get T */
    internal static T? Get<T>(
        this IHeaderDictionary headers, 
        string name)
    {
        if (headers == null)
        {
            throw new ArgumentNullException(nameof(headers));
        }
        
        var value = headers[name];        
        if (StringValues.IsNullOrEmpty(value))
        {
            return default(T);
        }
        
        if (KnownParsers.TryGetValue(typeof(T), out var temp))
        {
            var func = (Func<string, T>)temp;
            return func(value);
        }
        
        // get t via reflection
        return GetViaReflection<T>(value.ToString());
    }
    
    // get via reflection
    private static T? GetViaReflection<T>(string value)
    {
        // TODO: Cache the reflected type for later? Only if success?
        var type = typeof(T);
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            			.FirstOrDefault(methodInfo =>
            {
                if (string.Equals("TryParse", methodInfo.Name, StringComparison.Ordinal) && 
                    methodInfo.ReturnParameter.ParameterType.Equals(typeof(bool)))
                {
                    var methodParams = methodInfo.GetParameters();
                    return methodParams.Length == 2 && 
                           methodParams[0].ParameterType.Equals(typeof(string)) && 
                           methodParams[1].IsOut && 
                           methodParams[1].ParameterType.Equals(type.MakeByRefType());
                    }
                    return false;
                });
        
        if (method == null)
        {
            throw new NotSupportedException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    "The given type '{0}' does not have a TryParse method with the required signature 
                    "public static bool TryParse(string, out {0}).",
                nameof(T)));
        }
        
        var parameters = new object?[] { value, null };
        var success = (bool)method.Invoke(null, parameters)!;
        if (success)
        {
            return (T?)parameters[1];
        }
        return default(T);
    }
    
    /* get list T */
    internal static IList<T> GetList<T>(
        this IHeaderDictionary headers, 
        string name)
    {
        if (headers == null)
        {
            throw new ArgumentNullException(nameof(headers));
        }
        
        var values = headers[name];        
        if (StringValues.IsNullOrEmpty(values))
        {
            return Array.Empty<T>();
        }
        
        if (KnownListParsers.TryGetValue(typeof(T), out var temp))
        {
            var func = (Func<IList<string>, IList<T>>)temp;
            return func(values);
        }
        
        // get list via reflection
        return GetListViaReflection<T>(values);
    }
    
    // get list via reflection
    private static IList<T> GetListViaReflection<T>(StringValues values)
    {
        // TODO: Cache the reflected type for later? Only if success?
        var type = typeof(T);
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            			.FirstOrDefault(methodInfo =>
            {
                if (string.Equals("TryParseList", methodInfo.Name, StringComparison.Ordinal) && 
                    methodInfo.ReturnParameter.ParameterType.Equals(typeof(Boolean)))
                {
                    var methodParams = methodInfo.GetParameters();
                    return methodParams.Length == 2 && 
                           methodParams[0].ParameterType.Equals(typeof(IList<string>)) && 
                           methodParams[1].IsOut && 
                           methodParams[1].ParameterType.Equals(typeof(IList<T>).MakeByRefType());
                }
                return false;
            });
        
        if (method == null)
        {
            throw new NotSupportedException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    "The given type '{0}' does not have a TryParseList method with the required signature 
                    'public static bool TryParseList(IList<string>, out IList<{0}>).",
                nameof(T)));
        }
        
        var parameters = new object?[] 
        { 
            values, 
            null 
        };
        
        var success = (bool)method.Invoke(null, parameters)!;
        if (success)
        {
            return (IList<T>)parameters[1]!;
        }
        return Array.Empty<T>();
    }
}

```

###### 2.1.1.5 扩展方法 - set

```c#
public static class HeaderDictionaryTypeExtensions
{
    internal static void Set(
        this IHeaderDictionary headers, 
        string name, 
        object? value)
    {
        if (headers == null)
        {
            throw new ArgumentNullException(nameof(headers));
        }        
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }       
        if (value == null)
        {
            headers.Remove(name);
        }
        else
        {
            headers[name] = value.ToString();
        }
    }
    
    internal static void SetList<T>(
        this IHeaderDictionary headers, 
        string name, 
        IList<T>? values)
    {
        if (headers == null)
        {
            throw new ArgumentNullException(nameof(headers));
        }        
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }        
        if (values == null || values.Count == 0)
        {
            headers.Remove(name);
        }
        else if (values.Count == 1)
        {
            headers[name] = new StringValues(values[0]!.ToString());
        }
        else
        {
            var newValues = new string[values.Count];
            for (var i = 0; i < values.Count; i++)
            {
                newValues[i] = values[i]!.ToString()!;
            }
            headers[name] = new StringValues(newValues);
        }
    }
}

```

###### 2.1.1.6 扩展方法 - append list

```c#
public static class HeaderDictionaryTypeExtensions
{         
    public static void AppendList<T>(
        this IHeaderDictionary Headers, 
        string name, 
        IList<T> values)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }        
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }
        
        switch (values.Count)
        {
            case 0:
                Headers.Append(name, StringValues.Empty);
                break;
            case 1:
                Headers.Append(name, new StringValues(values[0]!.ToString()));
                break;
            default:
                var newValues = new string[values.Count];
                for (var i = 0; i < values.Count; i++)
                {
                    newValues[i] = values[i]!.ToString()!;
                }
                Headers.Append(name, new StringValues(newValues));
                break;
        }
    }
}

```

###### 2.1.1.7 扩展方法 - about date

```c#
public static class HeaderDictionaryTypeExtensions
{
    // get date
    internal static DateTimeOffset? GetDate(
        this IHeaderDictionary headers, 
        string name)
    {
        if (headers == null)
        {
            throw new ArgumentNullException(nameof(headers));
        }        
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        return headers.Get<DateTimeOffset?>(name);
    }      
    
    // set date
    internal static void SetDate(
        this IHeaderDictionary headers, 
        string name, 
        DateTimeOffset? value)
    {
        if (headers == null)
        {
            throw new ArgumentNullException(nameof(headers));
        }        
        if (name == null)            
        {
            throw new ArgumentNullException(nameof(name));
        }        
        if (value.HasValue)
        {
            headers[name] = HeaderUtilities.FormatDate(value.GetValueOrDefault());
        }
        else
        {
            headers.Remove(name);
        }
    }
    
}

```

###### 2.1.1.8 扩展方法 - append & comma separator

```c#
public static class HeaderDictionaryExtensions
{               
    public static void Append(
        this IHeaderDictionary headers, 
        string key, 
        StringValues value)
    {
        ParsingHelpers.AppendHeaderUnmodified(headers, key, value);
    }
    
    public static string[] GetCommaSeparatedValues(
        this IHeaderDictionary headers, 
        string key)
    {
        return ParsingHelpers.GetHeaderSplit(headers, key).ToArray();
    }
        
    public static void SetCommaSeparatedValues(
        this IHeaderDictionary headers, 
        string key, 
        params string[] values)
    {
        ParsingHelpers.SetHeaderJoined(headers, key, values);
    }
    
    public static void AppendCommaSeparatedValues(
        this IHeaderDictionary headers, 
        string key, 
        params string[] values)
    {
        ParsingHelpers.AppendHeaderJoined(headers, key, values);
    }
}

internal static class ParsingHelpers
{
    public static StringValues GetHeader(IHeaderDictionary headers, string key)
    {
        StringValues value;
        return headers.TryGetValue(key, out value) ? value : StringValues.Empty;
    }
    
    public static StringValues GetHeaderSplit(IHeaderDictionary headers, string key)
    {
        var values = GetHeaderUnmodified(headers, key);
        
        StringValues result = default;
        
        foreach (var segment in new HeaderSegmentCollection(values))
        {
            if (!StringSegment.IsNullOrEmpty(segment.Data))
            {
                var value = DeQuote(segment.Data.Value);
                if (!string.IsNullOrEmpty(value))
                {
                    result = StringValues.Concat(in result, value);
                }
            }
        }
        
        return result;
    }
    
    public static StringValues GetHeaderUnmodified(IHeaderDictionary headers, string key)
    {
        if (headers == null)
        {
            throw new ArgumentNullException(nameof(headers));
        }
        
        StringValues values;
        return headers.TryGetValue(key, out values) ? values : StringValues.Empty;
    }
    
    public static void SetHeaderJoined(IHeaderDictionary headers, string key, StringValues value)
    {
        if (headers == null)
        {
            throw new ArgumentNullException(nameof(headers));
        }
        
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }
        if (StringValues.IsNullOrEmpty(value))
        {
            headers.Remove(key);
        }
        else
        {
            headers[key] = string.Join(",", value.Select((s) => QuoteIfNeeded(s)));
        }
    }
    
    // Quote items that contain commas and are not already quoted.
    private static string QuoteIfNeeded(string value)
    {
        if (!string.IsNullOrEmpty(value) &&
            value.Contains(',') &&
            (value[0] != '"' || value[value.Length - 1] != '"'))
        {
            return $"\"{value}\"";
        }
        return value;
    }
    
    private static string DeQuote(string value)
    {
        if (!string.IsNullOrEmpty(value) &&
            (value.Length > 1 && value[0] == '"' && value[value.Length - 1] == '"'))
        {
            value = value.Substring(1, value.Length - 2);
        }
        
        return value;
    }
    
    public static void SetHeaderUnmodified(IHeaderDictionary headers, string key, StringValues? values)
    {
        if (headers == null)
        {
            throw new ArgumentNullException(nameof(headers));
        }
        
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }
        if (!values.HasValue || StringValues.IsNullOrEmpty(values.GetValueOrDefault()))
        {
            headers.Remove(key);
        }
        else
        {
            headers[key] = values.GetValueOrDefault();
        }
    }
    
    public static void AppendHeaderJoined(IHeaderDictionary headers, string key, params string[] values)
    {
        if (headers == null)
        {
            throw new ArgumentNullException(nameof(headers));
        }
        
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }
        
        if (values == null || values.Length == 0)
        {
            return;
        }
        
        string existing = GetHeader(headers, key);
        if (existing == null)
        {
            SetHeaderJoined(headers, key, values);
        }
        else
        {
            
            headers[key] = existing + "," + string.Join(",", values.Select(value => QuoteIfNeeded(value)));
        }
    }
    
    public static void AppendHeaderUnmodified(IHeaderDictionary headers, string key, StringValues values)
    {
        if (headers == null)
        {
            throw new ArgumentNullException(nameof(headers));
        }
        
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }
        
        if (values.Count == 0)
        {
            return;
        }
        
        var existing = GetHeaderUnmodified(headers, key);
        SetHeaderUnmodified(headers, key, StringValues.Concat(existing, values));
    }
}

```

##### 2.1.2 request headers

```c#
public class RequestHeaders
{    
    public IHeaderDictionary Headers { get; }                    
    public RequestHeaders(IHeaderDictionary headers)
    {
        if (headers == null)
        {
            throw new ArgumentNullException(nameof(headers));
        }
        
        Headers = headers;
    }
                
    // rfc     
    public IList<MediaTypeHeaderValue> Accept
    {
        get
        {
            return Headers.GetList<MediaTypeHeaderValue>(HeaderNames.Accept);
        }
        set
        {
            Headers.SetList(HeaderNames.Accept, value);
        }
    }
        
    public IList<StringWithQualityHeaderValue> AcceptCharset
    {
        get
        {
            return Headers.GetList<StringWithQualityHeaderValue>(HeaderNames.AcceptCharset);
        }
        set
        {
            Headers.SetList(HeaderNames.AcceptCharset, value);
        }
    }
        
    public IList<StringWithQualityHeaderValue> AcceptEncoding
    {
        get
        {
            return Headers.GetList<StringWithQualityHeaderValue>(HeaderNames.AcceptEncoding);
        }
        set
        {
            Headers.SetList(HeaderNames.AcceptEncoding, value);
        }
    }
        
    public IList<StringWithQualityHeaderValue> AcceptLanguage
    {
        get
        {
            return Headers.GetList<StringWithQualityHeaderValue>(HeaderNames.AcceptLanguage);
        }
        set
        {
            Headers.SetList(HeaderNames.AcceptLanguage, value);
        }
    }
        
    public CacheControlHeaderValue? CacheControl
    {
        get
        {
            return Headers.Get<CacheControlHeaderValue>(HeaderNames.CacheControl);
        }
        set
        {
            Headers.Set(HeaderNames.CacheControl, value);
        }
    }
        
    public ContentDispositionHeaderValue? ContentDisposition
    {
        get
        {
            return Headers.Get<ContentDispositionHeaderValue>(HeaderNames.ContentDisposition);
        }
        set
        {
            Headers.Set(HeaderNames.ContentDisposition, value);
        }
    }
    
    public long? ContentLength
    {
        get
        {
            return Headers.ContentLength;
        }
        set
        {      
            Headers.ContentLength = value;
        }
    }
        
    public ContentRangeHeaderValue? ContentRange
    {
        get
        {
            return Headers.Get<ContentRangeHeaderValue>(HeaderNames.ContentRange);
        }
        set
        {
            Headers.Set(HeaderNames.ContentRange, value);
        }
    }
    
    public MediaTypeHeaderValue? ContentType
    {
        get
        {
            return Headers.Get<MediaTypeHeaderValue>(HeaderNames.ContentType);
        }
        set
        {
            Headers.Set(HeaderNames.ContentType, value);
        }
    }
        
    public IList<CookieHeaderValue> Cookie
    {
        get
        {
            return Headers.GetList<CookieHeaderValue>(HeaderNames.Cookie);
        }
        set
        {
            Headers.SetList(HeaderNames.Cookie, value);
        }
    }
        
    public DateTimeOffset? Date
    {
        get
        {
            return Headers.GetDate(HeaderNames.Date);
        }
        set
        {
            Headers.SetDate(HeaderNames.Date, value);
        }
    }
        
    public DateTimeOffset? Expires
    {
        get
        {
            return Headers.GetDate(HeaderNames.Expires);
        }
        set
        {
            Headers.SetDate(HeaderNames.Expires, value);
        }
    }
        
    public HostString Host
    {
        get
        {
            return HostString.FromUriComponent(Headers[HeaderNames.Host]);
        }
        set
        {
            Headers[HeaderNames.Host] = value.ToUriComponent();
        }
    }
        
    public IList<EntityTagHeaderValue> IfMatch
    {
        get
        {
            return Headers.GetList<EntityTagHeaderValue>(HeaderNames.IfMatch);
        }
        set
        {
            Headers.SetList(HeaderNames.IfMatch, value);
        }
    }
        
    public DateTimeOffset? IfModifiedSince
    {
        get
        {
            return Headers.GetDate(HeaderNames.IfModifiedSince);
        }
        set
        {
            Headers.SetDate(HeaderNames.IfModifiedSince, value);
        }
    }
        
    public IList<EntityTagHeaderValue> IfNoneMatch
    {
        get
        {
            return Headers.GetList<EntityTagHeaderValue>(HeaderNames.IfNoneMatch);
        }
        set
        {
            Headers.SetList(HeaderNames.IfNoneMatch, value);
        }
    }
        
    public RangeConditionHeaderValue? IfRange
    {
        get
        {
            return Headers.Get<RangeConditionHeaderValue>(HeaderNames.IfRange);
        }
        set
        {
            Headers.Set(HeaderNames.IfRange, value);
        }
    }
        
    public DateTimeOffset? IfUnmodifiedSince
    {
        get
        {
            return Headers.GetDate(HeaderNames.IfUnmodifiedSince);
        }
        set
        {
            Headers.SetDate(HeaderNames.IfUnmodifiedSince, value);
        }
    }
        
    public DateTimeOffset? LastModified
    {
        get
        {
            return Headers.GetDate(HeaderNames.LastModified);
        }
        set
        {
            Headers.SetDate(HeaderNames.LastModified, value);
        }
    }
        
    public RangeHeaderValue? Range
    {
        get
        {
            return Headers.Get<RangeHeaderValue>(HeaderNames.Range);
        }
        set
        {
            Headers.Set(HeaderNames.Range, value);
        }
    }
        
    public Uri? Referer
    {
        get
        {
            if (Uri.TryCreate(Headers[HeaderNames.Referer], UriKind.RelativeOrAbsolute, out var uri))
            {
                return uri;
            }
            return null;
        }
        set
        {
            Headers.Set(HeaderNames.Referer, value == null ? null : UriHelper.Encode(value));
        }
    }           
}

```

###### 2.1.2.1 方法 - crud

```c#
public class RequestHeaders
{
    /* get */    
    public T? Get<T>(string name)
    {
        return Headers.Get<T>(name);
    }
        
    public IList<T> GetList<T>(string name)
    {
        return Headers.GetList<T>(name);
    }
    
    /* set */    
    public void Set(string name, object? value)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        Headers.Set(name, value);
    }
        
    public void SetList<T>(string name, IList<T>? values)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        Headers.SetList<T>(name, values);
    }
        
    /* append */    
    public void Append(string name, object value)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        
        Headers.Append(name, value.ToString());
    }
        
    public void AppendList<T>(string name, IList<T> values)
    {
        Headers.AppendList<T>(name, values);
    }
}

```

###### 2.1.2.2 扩展方法 - get typed headers

```c#
public static class HeaderDictionaryTypeExtensions
{    
    public static RequestHeaders GetTypedHeaders(this HttpRequest request)
    {
        return new RequestHeaders(request.Headers);
    }
}

```

##### 2.1.3 response headers

```c#
public class ResponseHeaders
{    
    public IHeaderDictionary Headers { get; }    
    public ResponseHeaders(IHeaderDictionary headers)
    {
        if (headers == null)
        {
            throw new ArgumentNullException(nameof(headers));
        }
        
        Headers = headers;
    }        
    
    // rfc
    public CacheControlHeaderValue? CacheControl
    {
        get
        {
            return Headers.Get<CacheControlHeaderValue>(HeaderNames.CacheControl);
        }
        set
        {
            Headers.Set(HeaderNames.CacheControl, value);
        }
    }
    
    public ContentDispositionHeaderValue? ContentDisposition
    {
        get
        {
            return Headers.Get<ContentDispositionHeaderValue>(HeaderNames.ContentDisposition);
        }
        set
        {
            Headers.Set(HeaderNames.ContentDisposition, value);
        }
    }
        
    public long? ContentLength
    {
        get
        {
            return Headers.ContentLength;
        }
        set
        {
            Headers.ContentLength = value;
        }
    }
        
    public ContentRangeHeaderValue? ContentRange
    {
        get
        {
            return Headers.Get<ContentRangeHeaderValue>(HeaderNames.ContentRange);
        }
        set
        {
            Headers.Set(HeaderNames.ContentRange, value);
        }
    }
        
    public MediaTypeHeaderValue? ContentType
    {
        get
        {
            return Headers.Get<MediaTypeHeaderValue>(HeaderNames.ContentType);
        }
        set
        {
            Headers.Set(HeaderNames.ContentType, value);
        }
    }
        
    public DateTimeOffset? Date
    {
        get
        {
            return Headers.GetDate(HeaderNames.Date);
        }
        set
        {
            Headers.SetDate(HeaderNames.Date, value);
        }
    }
        
    public EntityTagHeaderValue? ETag
    {
        get
        {
            return Headers.Get<EntityTagHeaderValue>(HeaderNames.ETag);
        }
        set
        {
            Headers.Set(HeaderNames.ETag, value);
        }
    }
        
    public DateTimeOffset? Expires
    {
        get
        {
            return Headers.GetDate(HeaderNames.Expires);
        }
        set
        {
            Headers.SetDate(HeaderNames.Expires, value);
        }
    }
        
    public DateTimeOffset? LastModified
    {
        get
        {
            return Headers.GetDate(HeaderNames.LastModified);
        }
        set
        {
            Headers.SetDate(HeaderNames.LastModified, value);
        }
    }
        
    public Uri? Location
    {
        get
        {
            if (Uri.TryCreate(Headers[HeaderNames.Location], UriKind.RelativeOrAbsolute, out var uri))
            {
                return uri;
            }
            return null;
        }
        set
        {
            Headers.Set(HeaderNames.Location, value == null ? null : UriHelper.Encode(value));
        }
    }
        
    public IList<SetCookieHeaderValue> SetCookie
    {
        get
        {
            return Headers.GetList<SetCookieHeaderValue>(HeaderNames.SetCookie);
        }
        set
        {
            Headers.SetList(HeaderNames.SetCookie, value);
        }
    }            
}

```

###### 2.1.3.1 方法 - crud

```c#
public class ResponseHeaders
{
    /* get */
    public T? Get<T>(string name)
    {
        return Headers.Get<T>(name);
    }
        
    public IList<T> GetList<T>(string name)
    {
        return Headers.GetList<T>(name);
    }
      
    /* set */
    public void Set(string name, object? value)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        Headers.Set(name, value);
    }
        
    public void SetList<T>(string name, IList<T>? values)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        Headers.SetList<T>(name, values);
    }
      
    /* append */
    public void Append(string name, object value)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }        
        if (value == null)
        {
            
            throw new ArgumentNullException(nameof(value));
        }
        
        Headers.Append(name, value.ToString());
    }
        
    public void AppendList<T>(string name, IList<T> values)
    {
        Headers.AppendList<T>(name, values);
    }
}

```

###### 2.1.3.2 扩展方法 - get typed headers

```c#
public static class HeaderDictionaryTypeExtensions
{        
    public static ResponseHeaders GetTypedHeaders(this HttpResponse response)
    {
        return new ResponseHeaders(response.Headers);
    }
}

```

#### 2.2 http request

```c#
public abstract class HttpRequest
{    
    public abstract HttpContext HttpContext { get; }        
 
    public abstract string Method { get; set; }        
    public abstract string Scheme { get; set; }        
    public abstract bool IsHttps { get; set; }       
    
    public abstract HostString Host { get; set; }        
    public abstract PathString PathBase { get; set; }        
    public abstract PathString Path { get; set; }        
    
    public abstract QueryString QueryString { get; set; }        
    public abstract IQueryCollection Query { get; set; }        
    
    public abstract string Protocol { get; set; }        
    public abstract IHeaderDictionary Headers { get; }        
    
    public abstract IRequestCookieCollection Cookies { get; set; }        
    public abstract long? ContentLength { get; set; }        
    public abstract string ContentType { get; set; }        
    public abstract Stream Body { get; set; }        
    public virtual PipeReader BodyReader { get => throw new NotImplementedException();  }       
    
    public abstract bool HasFormContentType { get; }        
    public abstract IFormCollection Form { get; set; }      
    
    public virtual RouteValueDictionary RouteValues { get; set; } = null!;
    
    // 在派生类实现
    public abstract Task<IFormCollection> ReadFormAsync(
        CancellationToken cancellationToken = new CancellationToken());                
}

```

##### 2.2.1 default http request

```c#
internal sealed class DefaultHttpRequest : HttpRequest
{
    private const string Http = "http";
    private const string Https = "https";
         
    private FeatureReferences<FeatureInterfaces> _features;
    struct FeatureInterfaces
    {
        public IHttpRequestFeature? Request;
        public IQueryFeature? Query;
        public IFormFeature? Form;
        public IRequestCookiesFeature? Cookies;
        public IRouteValuesFeature? RouteValues;
        public IRequestBodyPipeFeature? BodyPipe;
    }
            
    private readonly DefaultHttpContext _context;
    public override HttpContext HttpContext => _context;   
        
    public DefaultHttpRequest(DefaultHttpContext context)
    {
        // 注入 http context
        _context = context;
        // 初始化 features（注入 http context 的 feature collection）
        _features.Initalize(context.Features);
    }
       
    // initialize
    public void Initialize()
    {
        _features.Initalize(_context.Features);
    }
    
    public void Initialize(int revision)
    {
        _features.Initalize(_context.Features, revision);
    }
    
    public void Uninitialize()
    {
        _features = default;
    }            
}

```

###### 2.2.1.1 feature

```c#
internal sealed class DefaultHttpRequest : HttpRequest
{
    /* http request feature with default */
    private IHttpRequestFeature HttpRequestFeature => _features.Fetch(
        ref _features.Cache.Request, 
        _nullRequestFeature)!;
    
    private readonly static Func<IFeatureCollection, IHttpRequestFeature?> _nullRequestFeature = f => null;
    
    /* query feature with default */
    private IQueryFeature QueryFeature => _features.Fetch(
        ref _features.Cache.Query, 
        _newQueryFeature)!;
    
    private readonly static Func<IFeatureCollection, IQueryFeature?> _newQueryFeature = f => new QueryFeature(f);    
    
    /* request cookie feature with default */    
    private IRequestCookiesFeature RequestCookiesFeature => _features.Fetch(
        ref _features.Cache.Cookies, 
        _newRequestCookiesFeature)!;
    
    private readonly static Func<IFeatureCollection, IRequestCookiesFeature> _newRequestCookiesFeature = f => 
        new RequestCookiesFeature(f);
    
    /* request body pipe feauture with default */    
    private IRequestBodyPipeFeature RequestBodyPipeFeature => _features.Fetch(
        ref _features.Cache.BodyPipe, 
        this.HttpContext, 
        _newRequestBodyPipeFeature)!;
    
    private readonly static Func<HttpContext, IRequestBodyPipeFeature> _newRequestBodyPipeFeature = context => 
        new RequestBodyPipeFeature(context);
        
    /* form feature with default */    
    private IFormFeature FormFeature => _features.Fetch(
        ref _features.Cache.Form, 
        this, 
        _newFormFeature)!;
    
    private readonly static Func<DefaultHttpRequest, IFormFeature> _newFormFeature = r => 
        new FormFeature(r, r._context.FormOptions ?? FormOptions.Default);
    
     /* route value feature with default */
    private IRouteValuesFeature RouteValuesFeature => _features.Fetch(
        ref _features.Cache.RouteValues, 
        _newRouteValuesFeature)!;                
    
    private readonly static Func<IFeatureCollection, IRouteValuesFeature> _newRouteValuesFeature = f => 
        new RouteValuesFeature();
}

```

###### 2.2.1.2 props

```c#
internal sealed class DefaultHttpRequest : HttpRequest
{                                                  
    // 从 http request feature 解析 method    
    public override string Method
    {
        get { return HttpRequestFeature.Method; }
        set { HttpRequestFeature.Method = value; }
    }
    
    // 从 http request feature 解析 scheme
    public override string Scheme
    {
        get { return HttpRequestFeature.Scheme; }
        set { HttpRequestFeature.Scheme = value; }
    }    
    
    // https (flag)
    public override bool IsHttps
    {
        get { return string.Equals(Https, Scheme, StringComparison.OrdinalIgnoreCase); }
        set { Scheme = value ? Https : Http; }
    }
    
    // 从 header 属性解析 host
    public override HostString Host
    {
        get { return HostString.FromUriComponent(Headers[HeaderNames.Host]); }
        set { Headers[HeaderNames.Host] = value.ToUriComponent(); }
    }
    
    // 从 http request feature 解析 path-base
    public override PathString PathBase
    {
        get { return new PathString(HttpRequestFeature.PathBase); }
        set { HttpRequestFeature.PathBase = value.Value ?? string.Empty; }
    }
    
    //从 http request feature 解析 path
    public override PathString Path
    {
        get { return new PathString(HttpRequestFeature.Path); }
        set { HttpRequestFeature.Path = value.Value ?? string.Empty; }
    }
    
    // 从 http request feature 解析 query string
    public override QueryString QueryString
    {
        get { return new QueryString(HttpRequestFeature.QueryString); }
        set { HttpRequestFeature.QueryString = value.Value ?? string.Empty; }
    }
            
    // 从 query feature 解析 query collection
    public override IQueryCollection Query        
    {
        get { return QueryFeature.Query; }
        set { QueryFeature.Query = value; }
    }
    
    // 从 http request feature 解析 protocol
    public override string Protocol
    {
        get { return HttpRequestFeature.Protocol; }
        set { HttpRequestFeature.Protocol = value; }
    }
    
    // 从 http request feature 解析 (request) headers
    public override IHeaderDictionary Headers
    {
        get { return HttpRequestFeature.Headers; }
    }
            
    // 从 request cookies feature 解析 cookies
    public override IRequestCookieCollection Cookies
    {
        get { return RequestCookiesFeature.Cookies; }
        set { RequestCookiesFeature.Cookies = value; }
    }
        
    // 从 headers 属性解析 content length
    public override long? ContentLength
    {
        get { return Headers.ContentLength; }
        set { Headers.ContentLength = value; }
    }
    
    // 从 headers 属性解析 content type
    public override string ContentType
    {
        get { return Headers[HeaderNames.ContentType]; }
        set { Headers[HeaderNames.ContentType] = value; }
    }
    
    // 从 http request feature 解析 body
    public override Stream Body
    {
        get { return HttpRequestFeature.Body; }
        set { HttpRequestFeature.Body = value; }
    }
            
    // 从 request body pipe feature 解析 body ready
    public override PipeReader BodyReader
    {
        get { return RequestBodyPipeFeature.Reader; }
    }
            
    // 从 form feature 判断 has form 
    public override bool HasFormContentType
    {
        get { return FormFeature.HasFormContentType; }
    }    
    
    // 从 form feature 解析 form
    public override IFormCollection Form
    {
        get { return FormFeature.ReadForm(); }
        set { FormFeature.Form = value; }
    }
               
    // 从 route value feature 解析 route value dictionary
    public override RouteValueDictionary RouteValues
    {
        get { return RouteValuesFeature.RouteValues; }
        set { RouteValuesFeature.RouteValues = value; }
    }                                                                    
}

```

###### 2.2.1.3 方法 - read from json

```c#
public static class HttpRequestJsonExtensions
{    
    [SuppressMessage(
        "ApiDesign", 
        "RS0026:Do not add multiple public overloads with optional parameters", 
        Justification = "Required to maintain compatibility")]
    public static ValueTask<TValue?> ReadFromJsonAsync<TValue>(
        this HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        return request.ReadFromJsonAsync<TValue>(
            options: null, 
            cancellationToken);
    }
        
    [SuppressMessage(
        "ApiDesign", 
        "RS0026:Do not add multiple public overloads with optional parameters", 
        Justification = "Required to maintain compatibility")]
    public static async ValueTask<TValue?> ReadFromJsonAsync<TValue>(
        this HttpRequest request,
        JsonSerializerOptions? options,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }        
        if (!request.HasJsonContentType(out var charset))
        {
            throw CreateContentTypeError(request);
        }
        
        options ??= ResolveSerializerOptions(request.HttpContext);
        
        var encoding = GetEncodingFromCharset(charset);
        var (inputStream, usesTranscodingStream) = GetInputStream(request.HttpContext, encoding);
        
        try
        {
            return await JsonSerializer.DeserializeAsync<TValue>(
                inputStream, 
                options, 
                cancellationToken);
        }
        finally
        {
            if (usesTranscodingStream)
            {
                await inputStream.DisposeAsync();
            }
        }
    }
        
    [SuppressMessage(
        "ApiDesign", 
        "RS0026:Do not add multiple public overloads with optional parameters", 
        Justification = "Required to maintain compatibility")]
    public static ValueTask<object?> ReadFromJsonAsync(
        this HttpRequest request,
        Type type,
        CancellationToken cancellationToken = default)
    {
        return request.ReadFromJsonAsync(type, options: null, cancellationToken);
    }
            
    [SuppressMessage(
        "ApiDesign", 
        "RS0026:Do not add multiple public overloads with optional parameters",
        Justification = "Required to maintain compatibility")]
    public static async ValueTask<object?> ReadFromJsonAsync(
        this HttpRequest request,
        Type type,
        JsonSerializerOptions? options,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }        
        if (!request.HasJsonContentType(out var charset))
        {
            throw CreateContentTypeError(request);
        }
        
        options ??= ResolveSerializerOptions(request.HttpContext);
        
        var encoding = GetEncodingFromCharset(charset);
        var (inputStream, usesTranscodingStream) = GetInputStream(request.HttpContext, encoding);
        
        try
        {
            return await JsonSerializer.DeserializeAsync(
                inputStream, 
                type, 
                options, 
                cancellationToken);
        }
        finally
        {
            if (usesTranscodingStream)
            {
                await inputStream.DisposeAsync();
            }
        }
    }
        
    public static bool HasJsonContentType(
        this HttpRequest request)
    {
        return request.HasJsonContentType(out _);
    }
    
    private static bool HasJsonContentType(
        this HttpRequest request, 
        out StringSegment charset)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var mt))
        {
            charset = StringSegment.Empty;
            return false;
        }
        
        // Matches application/json
        if (mt.MediaType.Equals(
	            JsonConstants.JsonContentType, 
    	        StringComparison.OrdinalIgnoreCase))
        {
            charset = mt.Charset;
            return true;
        }
        
        // Matches +json, e.g. application/ld+json
        if (mt.Suffix.Equals(
	            "json", 
    	        StringComparison.OrdinalIgnoreCase))
        {
            charset = mt.Charset;
            return true;
        }
        
        charset = StringSegment.Empty;
        return false;
    }
        
    private static JsonSerializerOptions ResolveSerializerOptions(HttpContext httpContext)
    {
        // Attempt to resolve options from DI then fallback to default options
        return httpContext.RequestServices?
            			 .GetService<IOptions<JsonOptions>>()?
            			 .Value?
            			 .SerializerOptions 
            				?? JsonOptions.DefaultSerializerOptions;
    }
    
    private static InvalidOperationException CreateContentTypeError(HttpRequest request)
    {
        return new InvalidOperationException(
            $"Unable to read the request as JSON because the request content type '{request.ContentType}'
            "is not a known JSON content type.");
    }
    
    private static (Stream inputStream, bool usesTranscodingStream) GetInputStream(
        HttpContext httpContext, 
        Encoding? encoding)
    {
        if (encoding == null || encoding.CodePage == Encoding.UTF8.CodePage)
        {
            return (httpContext.Request.Body, false);
        }
        
        var inputStream = Encoding.CreateTranscodingStream(
            httpContext.Request.Body, 
            encoding, 
            Encoding.UTF8, 
            leaveOpen: true);
        
        return (inputStream, true);
    }
    
    private static Encoding? GetEncodingFromCharset(StringSegment charset)
    {
        if (charset.Equals("utf-8", StringComparison.OrdinalIgnoreCase))
        {
            // This is an optimization for utf-8 that prevents the Substring caused by
            // charset.Value
            return Encoding.UTF8;
        }
        
        try
        {
            // charset.Value might be an invalid encoding name as in charset=invalid.
            return charset.HasValue 
                ? Encoding.GetEncoding(charset.Value) 
                : null;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Unable to read the request as JSON because the request content type charset '{charset}' 
                "is not a known encoding.", 
                ex);
        }
    }
}

```

###### 2.2.1.4 方法 - read form

```c#
internal sealed class DefaultHttpRequest : HttpRequest
{
    public override Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken)
    {
        return FormFeature.ReadFormAsync(cancellationToken);
    }
}

public static class RequestFormReaderExtensions
{    
    public static Task<IFormCollection> ReadFormAsync(
        this HttpRequest request, 
        FormOptions options,
        CancellationToken cancellationToken = new CancellationToken())
    {           
        if (request == null)            
        {
            throw new ArgumentNullException(nameof(request));
        }
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }        
        if (!request.HasFormContentType)
        {
            throw new InvalidOperationException("Incorrect Content-Type: " + request.ContentType);
        }
        
        var features = request.HttpContext.Features;
        var formFeature = features.Get<IFormFeature>();
        if (formFeature == null || formFeature.Form == null)
        {
            // We haven't read the form yet, replace the reader with one using our own options.
            features.Set<IFormFeature>(new FormFeature(request, options));
        }
        
        return request.ReadFormAsync(cancellationToken);
    }
}

```

###### 2.2.1.5 方法 - request multipart

```c#
public static class HttpRequestMultipartExtensions
{    
    public static string GetMultipartBoundary(this HttpRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        
        if (!MediaTypeHeaderValue.TryParse(
            	request.ContentType, 
            	out var mediaType))
        {
            return string.Empty;
        }
        
        return HeaderUtilities.RemoveQuotes(mediaType.Boundary).ToString();
    }
}

```

###### 2.2.1.6 方法 - request rewind

```c#
public static class HttpRequestRewindExtensions
{    
    public static void EnableBuffering(this HttpRequest request)
    {
        BufferingHelper.EnableRewind(request);
    }
        
    public static void EnableBuffering(
        this HttpRequest request, 
        int bufferThreshold)
    {
        BufferingHelper.EnableRewind(request, bufferThreshold);
    }
        
    public static void EnableBuffering(
        this HttpRequest request, 
        long bufferLimit)
    {
        BufferingHelper.EnableRewind(request, bufferLimit: bufferLimit);
    }
        
    public static void EnableBuffering(
        this HttpRequest request, 
        int bufferThreshold, 
        long bufferLimit)
    {
        BufferingHelper.EnableRewind(request, bufferThreshold, bufferLimit);
    }
}

```

###### 2.2.1.7 方法 - trailer

```c#
public static class RequestTrailerExtensions
{    
    public static StringValues GetDeclaredTrailers(this HttpRequest request)
    {
        return request.Headers.GetCommaSeparatedValues(HeaderNames.Trailer);
    }
        
    public static bool SupportsTrailers(this HttpRequest request)
    {
        return request.HttpContext.Features.Get<IHttpRequestTrailersFeature>() != null;
    }
        
    public static bool CheckTrailersAvailable(this HttpRequest request)
    {
        return request.HttpContext.Features.Get<IHttpRequestTrailersFeature>()?.Available == true;
    }
        
    public static StringValues GetTrailer(this HttpRequest request, string trailerName)
    {
        var feature = request.HttpContext.Features.Get<IHttpRequestTrailersFeature>();
        
        if (feature == null)
        {
            throw new NotSupportedException("This request does not support trailers.");
        }
        
        return feature.Trailers[trailerName];
    }
}

```

###### a- http request trailer

```c#
public interface IHttpRequestTrailersFeature
{    
    bool Available { get; }        
    IHeaderDictionary Trailers { get; }
}

```



##### 2.2.2 request features

###### 2.2.2.1 http request feature?

```c#
// 接口？

// 实现
public class HttpRequestFeature : IHttpRequestFeature
{    
    public string Protocol { get; set; }        
    public string Scheme { get; set; }        
    public string Method { get; set; }        
    public string PathBase { get; set; }        
    public string Path { get; set; }        
    public string QueryString { get; set; }        
    public string RawTarget { get; set; }        
    public IHeaderDictionary Headers { get; set; }        
    public Stream Body { get; set; }
    
    public HttpRequestFeature()                
    {
        Headers = new HeaderDictionary();
        Body = Stream.Null;
        Protocol = string.Empty;
        Scheme = string.Empty;
        Method = string.Empty;
        PathBase = string.Empty;
        Path = string.Empty;
        QueryString = string.Empty;
        RawTarget = string.Empty;
    }            
}

```

###### 2.2.2.2 query feature

```c#
// 接口
public interface IQueryFeature
{    
    IQueryCollection Query { get; set; }
}

// 实现
public class QueryFeature : IQueryFeature
{            
    // features
    private FeatureReferences<IHttpRequestFeature> _features;
            
    /* 从 features 解析 http request feature */
    private IHttpRequestFeature HttpRequestFeature => _features.Fetch(
        ref _features.Cache, 
        _nullRequestFeature)!;    
    
    private readonly static Func<IFeatureCollection, IHttpRequestFeature?> _nullRequestFeature = f => null;
    
    // query collection
    private string? _original;    
    private IQueryCollection? _parsedValues;    
    public IQueryCollection Query
    {
        get
        {
            if (_features.Collection == null)
            {
                if (_parsedValues == null)
                {
                    _parsedValues = QueryCollection.Empty;
                }
                return _parsedValues;
            }
            
            var current = HttpRequestFeature.QueryString;
            
            if (_parsedValues == null || 
                !string.Equals(_original, current, StringComparison.Ordinal))
            {
                _original = current;
                
                var result = QueryHelpers.ParseNullableQuery(current);
                
                if (result == null)
                {
                    _parsedValues = QueryCollection.Empty;
                }
                else
                {
                    _parsedValues = new QueryCollection(result);
                }
            }
            return _parsedValues;
        }
        set
        {
            _parsedValues = value;
            if (_features.Collection != null)
            {
                if (value == null)
                {
                    _original = string.Empty;
                    HttpRequestFeature.QueryString = string.Empty;
                }
                else
                {
                    _original = QueryString.Create(_parsedValues).ToString();
                    HttpRequestFeature.QueryString = _original;
                }
            }
        }
    }
    
    /* 构造 */    
    public QueryFeature(IQueryCollection query)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }
        
        _parsedValues = query;
    }
        
    public QueryFeature(IFeatureCollection features)
    {
        if (features == null)
        {
            throw new ArgumentNullException(nameof(features));
        }
        
        _features.Initalize(features);
    }       	           
}

```

###### a- query collection

```c#
// 接口
public interface IQueryCollection : IEnumerable<KeyValuePair<string, StringValues>>
{    
    int Count { get; }        
    ICollection<string> Keys { get; }     
    
    bool ContainsKey(string key);        
    bool TryGetValue(string key, out StringValues value);        
    StringValues this[string key] { get; }
}

// 实现
public class QueryCollection : IQueryCollection
{        
    // empty
    public static readonly QueryCollection Empty = new QueryCollection();         
    private static readonly string[] EmptyKeys = Array.Empty<string>();
    private static readonly StringValues[] EmptyValues = Array.Empty<StringValues>();
            
    // container
    private Dictionary<string, StringValues>? Store { get; set; }
    
    /* 属性 */    
    public int Count
    {
        get
        {
            if (Store == null)
            {
                return 0;
            }
            return Store.Count;
        }
    }
        
    public ICollection<string> Keys
    {
        get
        {
            if (Store == null)
            {
                return EmptyKeys;
            }
            return Store.Keys;
        }
    }
    
    public StringValues this[string key]
    {
        get
        {
            if (Store == null)
            {
                return StringValues.Empty;
            }
            
            if (TryGetValue(key, out var value))
            {
                return value;
            }
            return StringValues.Empty;
        }
    }

    /* 构造函数 */    
    public QueryCollection()
    {
    }
        
    public QueryCollection(Dictionary<string, StringValues> store)
    {
        Store = store;
    }
        
    public QueryCollection(QueryCollection store)
    {
        Store = store.Store;
    }
    
    public QueryCollection(int capacity)
    {
        Store = new Dictionary<string, StringValues>(
            capacity, 
            StringComparer.OrdinalIgnoreCase);
    }            
    
    /* 方法 */    
    public bool ContainsKey(string key)
    {
        if (Store == null)
        {
            return false;
        }
        return Store.ContainsKey(key);
    }
        
    public bool TryGetValue(string key, out StringValues value)
    {
        if (Store == null)
        {
            value = default(StringValues);
            return false;
        }
        return Store.TryGetValue(key, out value);
    }
    
    /* 迭代器 */   
    
    // empty enumerator
    private static readonly Enumerator EmptyEnumerator = new Enumerator();    
    private static readonly IEnumerator<KeyValuePair<string, StringValues>> EmptyIEnumeratorType = EmptyEnumerator;
    private static readonly IEnumerator EmptyIEnumerator = EmptyEnumerator;
    
     public Enumerator GetEnumerator()
    {
        if (Store == null || Store.Count == 0)
        {
            // Non-boxed Enumerator
            return EmptyEnumerator;
        }
        return new Enumerator(Store.GetEnumerator());
    }
        
    IEnumerator<KeyValuePair<string, StringValues>> IEnumerable<KeyValuePair<string, StringValues>>.GetEnumerator()
    {
        if (Store == null || Store.Count == 0)
        {
            // Non-boxed Enumerator
            return EmptyIEnumeratorType;
        }
        return Store.GetEnumerator();
    }
        
    IEnumerator IEnumerable.GetEnumerator()
    {
        if (Store == null || Store.Count == 0)
        {
            // Non-boxed Enumerator
            return EmptyIEnumerator;
        }
        return Store.GetEnumerator();
    }    
     
    // enumerator 结构体
    public struct Enumerator : IEnumerator<KeyValuePair<string, StringValues>>
    {
        // Do NOT make this readonly, or MoveNext will not work
        private Dictionary<string, StringValues>.Enumerator _dictionaryEnumerator;
        private bool _notEmpty;
        
        internal Enumerator(Dictionary<string, StringValues>.Enumerator dictionaryEnumerator)
        {
            _dictionaryEnumerator = dictionaryEnumerator;
            _notEmpty = true;
        }
                
        public bool MoveNext()
        {
            if (_notEmpty)
            {
                return _dictionaryEnumerator.MoveNext();
            }
            return false;
        }
                
        public KeyValuePair<string, StringValues> Current
        {
            get
            {
                if (_notEmpty)
                {
                    return _dictionaryEnumerator.Current;
                }
                return default(KeyValuePair<string, StringValues>);
            }
        }
               
        public void Dispose()
        {
        }
        
        object IEnumerator.Current
        {
            get
            {
                return Current;
            }
        }
        
        void IEnumerator.Reset()
        {
            if (_notEmpty)
            {
                ((IEnumerator)_dictionaryEnumerator).Reset();
            }
        }
    }
    
}

```

###### 2.2.2.3 form feature

```c#
// 接口
public interface IFormFeature
{    
    bool HasFormContentType { get; }        
    IFormCollection? Form { get; set; }
        
    IFormCollection ReadForm();        
    Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken);
}

// 实现
public class FormFeature : IFormFeature
{
    private readonly HttpRequest _request;
    private readonly FormOptions _options;    
    
    private MediaTypeHeaderValue? ContentType
    {
        get
        {
            MediaTypeHeaderValue.TryParse(_request.ContentType, out var mt);
            return mt;
        }
    }
    
    public bool HasFormContentType
    {
        get
        {
            // Set directly
            if (Form != null)
            {
                return true;
            }
            
            var contentType = ContentType;
            return HasApplicationFormContentType(contentType) || HasMultipartFormContentType(contentType);
        }
    }
            
    private Task<IFormCollection>? _parsedFormTask;        
    private IFormCollection? _form;    
    public IFormCollection? Form
    {
        get { return _form; }
        set
        {
            _parsedFormTask = null;
            _form = value;
        }
    }
    
    /* 构造函数 */                    
    public FormFeature(IFormCollection form)
    {
        if (form == null)
        {
            throw new ArgumentNullException(nameof(form));
        }
        
        Form = form;
        _request = default!;
        _options = FormOptions.Default;
    }
        
    public FormFeature(HttpRequest request) : this(request, FormOptions.Default)
    {
    }
    
    public FormFeature(HttpRequest request, FormOptions options)        
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        _request = request;
        _options = options;
    }
    
    /* 方法 */        
    public IFormCollection ReadForm()
    {
        if (Form != null)
        {
            return Form;
        }
        
        if (!HasFormContentType)
        {
            throw new InvalidOperationException("Incorrect Content-Type: " + _request.ContentType);
        }
        
        // TODO: Issue #456 Avoid Sync-over-Async 
        // http://blogs.msdn.com/b/pfxteam/archive/2012/04/13/10293638.aspx
        // TODO: How do we prevent thread exhaustion?
        return ReadFormAsync().GetAwaiter().GetResult();
    }
       
    public Task<IFormCollection> ReadFormAsync() => ReadFormAsync(CancellationToken.None);
        
    public Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken)
    {
        // Avoid state machine and task allocation for repeated reads
        if (_parsedFormTask == null)
        {
            if (Form != null)
            {
                _parsedFormTask = Task.FromResult(Form);
            }
            else
            {
                _parsedFormTask = InnerReadFormAsync(cancellationToken);
            }
        }
        
        return _parsedFormTask;
    }
    
    private async Task<IFormCollection> InnerReadFormAsync(CancellationToken cancellationToken)
    {
        if (!HasFormContentType)
        {
            throw new InvalidOperationException("Incorrect Content-Type: " + _request.ContentType);
        }
        
        cancellationToken.ThrowIfCancellationRequested();
        
        if (_request.ContentLength == 0)
        {
            return FormCollection.Empty;
        }
        
        if (_options.BufferBody)
        {
            _request.EnableRewind(
                _options.MemoryBufferThreshold, 
                _options.BufferBodyLengthLimit);
        }
        
        FormCollection? formFields = null;
        FormFileCollection? files = null;
        
        // Some of these code paths use StreamReader which does not support cancellation tokens.
        using (cancellationToken.Register(state => ((HttpContext)state!).Abort(), _request.HttpContext))
        {
            var contentType = ContentType;
            // Check the content-type
            if (HasApplicationFormContentType(contentType))
            {
                var encoding = FilterEncoding(contentType.Encoding);
                var formReader = new FormPipeReader(_request.BodyReader, encoding)
                {
                    ValueCountLimit = _options.ValueCountLimit,
                    KeyLengthLimit = _options.KeyLengthLimit,
                    ValueLengthLimit = _options.ValueLengthLimit,
                };
                
                formFields = new FormCollection(await formReader.ReadFormAsync(cancellationToken));
            }
            else if (HasMultipartFormContentType(contentType))
            {
                var formAccumulator = new KeyValueAccumulator();
                
                var boundary = GetBoundary(
                    contentType, 
                    _options.MultipartBoundaryLengthLimit);
                
                var multipartReader = new MultipartReader(boundary, _request.Body)
                {
                    HeadersCountLimit = _options.MultipartHeadersCountLimit,
                    HeadersLengthLimit = _options.MultipartHeadersLengthLimit,
                    BodyLengthLimit = _options.MultipartBodyLengthLimit,
                };
                
                var section = await multipartReader.ReadNextSectionAsync(cancellationToken);
                
                while (section != null)
                {
                    // Parse the content disposition here and pass it further to avoid eparsings
                    if (!ContentDispositionHeaderValue.TryParse(
                        	section.ContentDisposition, 
                        	out var contentDisposition))
                    {
                        throw new InvalidDataException(
                            "Form section has invalid Content-Disposition value: " + section.ContentDisposition);
                    }
                    
                    if (contentDisposition.IsFileDisposition())
                    {
                        var fileSection = new FileMultipartSection(
                            section, 
                            contentDisposition);
                        
                        // Enable buffering for the file if not already done for the full body
                        section.EnableRewind(
                            _request.HttpContext.Response.RegisterForDispose,
                            _options.MemoryBufferThreshold, 
                            _options.MultipartBodyLengthLimit);
                        
                        // Find the end
                        await section.Body.DrainAsync(cancellationToken);
                        
                        var name = fileSection.Name;
                        var fileName = fileSection.FileName;
                        
                        FormFile file;
                        
                        if (section.BaseStreamOffset.HasValue)
                        {
                            // Relative reference to buffered request body
                            file = new FormFile(
                                _request.Body, 
                                section.BaseStreamOffset.GetValueOrDefault(), 
                                section.Body.Length, 
                                name, 
                                fileName);
                        }
                        else
                        {
                            // Individually buffered file body
                            file = new FormFile(
                                section.Body, 
                                0, 
                                section.Body.Length, 
                                name, 
                                fileName);
                        }
                        
                        file.Headers = new HeaderDictionary(section.Headers);
                        
                        if (files == null)
                        {
                            files = new FormFileCollection();
                        }
                        
                        if (files.Count >= _options.ValueCountLimit)
                        {
                            throw new InvalidDataException($"Form value count limit {_options.ValueCountLimit} exceeded.");
                        }
                        
                        files.Add(file);
                    }
                    else if (contentDisposition.IsFormDisposition())
                    {
                        var formDataSection = new FormMultipartSection(
                            section, 
                            contentDisposition);
                        
                        // Content-Disposition: form-data; name="key" value            
                        // Do not limit the key name length here because the multipart headers length limit is already in effect.
                        var key = formDataSection.Name;
                        var value = await formDataSection.GetValueAsync();
                        
                        formAccumulator.Append(key, value);
                        if (formAccumulator.ValueCount > _options.ValueCountLimit)
                        {
                            throw new InvalidDataException($"Form value count limit {_options.ValueCountLimit} exceeded.");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.Assert(
                            false, 
                            "Unrecognized content-disposition for this section: " +	section.ContentDisposition);
                    }
                    
                    section = await multipartReader.ReadNextSectionAsync(cancellationToken);
                }
                
                if (formAccumulator.HasValues)
                {
                    formFields = new FormCollection(
                        formAccumulator.GetResults(), 
                        files);
                }
            }
        }
        
        // Rewind so later readers don't have to.
        if (_request.Body.CanSeek)
        {
            _request.Body.Seek(0, SeekOrigin.Begin);
        }
        
        if (formFields != null)
        {
            Form = formFields;
        }
        else if (files != null)
        {
            Form = new FormCollection(null, files);
        }
        else
        {
            Form = FormCollection.Empty;
        }
        
        return Form;
    }
    
    private static Encoding FilterEncoding(Encoding? encoding)
    {
        // UTF-7 is insecure and should not be honored. 
        // UTF-8 will succeed for most cases.
        // https://docs.microsoft.com/en-us/dotnet/core/compatibility/syslib-warnings/syslib0001
        if (encoding == null || encoding.CodePage == 65000)
        {
            return Encoding.UTF8;
        }
        return encoding;
    }
    
    private bool HasApplicationFormContentType(
        [NotNullWhen(true)] MediaTypeHeaderValue? contentType)
    {
        // Content-Type: application/x-www-form-urlencoded; charset=utf-8
        return contentType != null && 
               contentType.MediaType.Equals(
            		"application/x-www-form-urlencoded", 
            		StringComparison.OrdinalIgnoreCase);
    }
    
    private bool HasMultipartFormContentType([NotNullWhen(true)] MediaTypeHeaderValue? contentType)
    {
        // Content-Type: multipart/form-data; 
        // boundary=----WebKitFormBoundarymx2fSWqWSd0OxQqq
        return contentType != null && 
               contentType.MediaType.Equals(
            		"multipart/form-data", 
		            StringComparison.OrdinalIgnoreCase);
    }
    
    private bool HasFormDataContentDisposition(ContentDispositionHeaderValue contentDisposition)
    {
        // Content-Disposition: form-data; name="key";
        return contentDisposition != null && 
               contentDisposition.DispositionType.Equals("form-data") && 
               StringSegment.IsNullOrEmpty(contentDisposition.FileName) && 
               StringSegment.IsNullOrEmpty(contentDisposition.FileNameStar);
    }
    
    private bool HasFileContentDisposition(ContentDispositionHeaderValue contentDisposition)
    {
        // Content-Disposition: form-data; name="myfile1"; filename="Misc 002.jpg"
        return contentDisposition != null && 
               contentDisposition.DispositionType.Equals("form-data") && 
               (!StringSegment.IsNullOrEmpty(contentDisposition.FileName) || 
                !StringSegment.IsNullOrEmpty(contentDisposition.FileNameStar));
    }
    
    // Content-Type: multipart/form-data; boundary="----WebKitFormBoundarymx2fSWqWSd0OxQqq"
    // The spec says 70 characters is a reasonable limit.
    private static string GetBoundary(
        MediaTypeHeaderValue contentType, 
        int lengthLimit)
    {
        var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary);
        if (StringSegment.IsNullOrEmpty(boundary))
        {
            throw new InvalidDataException("Missing content-type boundary.");
        }
        if (boundary.Length > lengthLimit)
        {
            throw new InvalidDataException($"Multipart boundary length limit {lengthLimit} exceeded.");
        }
        return boundary.ToString();
    }
}

// form options
public class FormOptions
{
    internal static readonly FormOptions Default = new FormOptions();
        
    public const int DefaultMemoryBufferThreshold = 1024 * 64;        
    public const int DefaultBufferBodyLengthLimit = 1024 * 1024 * 128;        
    public const int DefaultMultipartBoundaryLengthLimit = 128;        
    public const long DefaultMultipartBodyLengthLimit = 1024 * 1024 * 128;
        
    public bool BufferBody { get; set; } = false;        
    public int MemoryBufferThreshold { get; set; } = DefaultMemoryBufferThreshold;        
    public long BufferBodyLengthLimit { get; set; } = DefaultBufferBodyLengthLimit;        
    public int ValueCountLimit { get; set; } = FormReader.DefaultValueCountLimit;        
    public int KeyLengthLimit { get; set; } = FormReader.DefaultKeyLengthLimit;        
    public int ValueLengthLimit { get; set; } = FormReader.DefaultValueLengthLimit;      
    
    public int MultipartBoundaryLengthLimit { get; set; } = DefaultMultipartBoundaryLengthLimit;        
    public int MultipartHeadersCountLimit { get; set; } = MultipartReader.DefaultHeadersCountLimit;        
    public int MultipartHeadersLengthLimit { get; set; } = MultipartReader.DefaultHeadersLengthLimit;        
    public long MultipartBodyLengthLimit { get; set; } = DefaultMultipartBodyLengthLimit;
}

```

###### a- form collection

```c#
// 接口
public interface IFormCollection : IEnumerable<KeyValuePair<string, StringValues>>
{    
    int Count { get; }        
    ICollection<string> Keys { get; }
    StringValues this[string key] { get; }        
    IFormFileCollection Files { get; }
    
    bool ContainsKey(string key);        
    bool TryGetValue(string key, out StringValues value);            
}

// 实现
public class FormCollection : IFormCollection
{     
    // empty
    public static readonly FormCollection Empty = new FormCollection();     
    private static readonly string[] EmptyKeys = Array.Empty<string>(); 
    private static IFormFileCollection EmptyFiles = new FormFileCollection();
    
    // container
    private Dictionary<string, StringValues>? Store { get; set; }
       
    public int Count
    {
        get
        {
            return Store?.Count ?? 0;
        }
    }
          
    public ICollection<string> Keys
    {
        get
        {
            if (Store == null)
            {
                return EmptyKeys;
            }
            return Store.Keys;
        }
    }
    
    public StringValues this[string key]
    {
        get            
        {            
            if (Store == null)
            {
                return StringValues.Empty;
            }
            
            if (TryGetValue(key, out StringValues value))
            {
                return value;
            }
            
            return StringValues.Empty;
        }
    }
    
    private IFormFileCollection? _files;    
    public IFormFileCollection Files
    {
        get => _files ?? EmptyFiles;
        private set => _files = value;
    }
    
    /* 构造函数 */    
    private FormCollection()
    {
        // For static Empty
    }
        
    public FormCollection(Dictionary<string, StringValues>? fields, IFormFileCollection? files = null)
    {
        // can be null
        Store = fields;
        _files = files;
    }    
    
    /* 方法 */                                       
    public bool ContainsKey(string key)
    {
        if (Store == null)
        {
            return false;
        }
        return Store.ContainsKey(key);
    }
        
    public bool TryGetValue(string key, out StringValues value)
    {
        if (Store == null)
        {
            value = default(StringValues);
            return false;
        }
        return Store.TryGetValue(key, out value);
    }
    
    /* 迭代器 */
    
    // empty enumerator
    private static readonly Enumerator EmptyEnumerator = new Enumerator();        
    private static readonly IEnumerator<KeyValuePair<string, StringValues>> EmptyIEnumeratorType = EmptyEnumerator;
    private static readonly IEnumerator EmptyIEnumerator = EmptyEnumerator;
    
    public Enumerator GetEnumerator()        
    {
        if (Store == null || Store.Count == 0)
        {
            // Non-boxed Enumerator
            return EmptyEnumerator;
        }
        // Non-boxed Enumerator
        return new Enumerator(Store.GetEnumerator());
    }
        
    IEnumerator<KeyValuePair<string, StringValues>> IEnumerable<KeyValuePair<string, StringValues>>.GetEnumerator()
    {
        if (Store == null || Store.Count == 0)
        {
            // Non-boxed Enumerator
            return EmptyIEnumeratorType;
        }
        // Boxed Enumerator
        return Store.GetEnumerator();
    }
            
    IEnumerator IEnumerable.GetEnumerator()
    {
        if (Store == null || Store.Count == 0)
        {
            // Non-boxed Enumerator
            return EmptyIEnumerator;
        }
        // Boxed Enumerator
        return Store.GetEnumerator();
    }
    
    // enumeratory 结构体
    public struct Enumerator : IEnumerator<KeyValuePair<string, StringValues>>
    {
        // Do NOT make this readonly, or MoveNext will not work
        private Dictionary<string, StringValues>.Enumerator _dictionaryEnumerator;
        private bool _notEmpty;
        
        object IEnumerator.Current
        {
            get
            {
                return Current;
            }
        }
        
        internal Enumerator(Dictionary<string, StringValues>.Enumerator dictionaryEnumerator)
        {
            _dictionaryEnumerator = dictionaryEnumerator;
            _notEmpty = true;
        }
        
        public bool MoveNext()
            
        {
            if (_notEmpty)
            {
                return _dictionaryEnumerator.MoveNext();
            }
            return false;
        }
               
        public KeyValuePair<string, StringValues> Current
        {
            get
            {
                if (_notEmpty)
                {
                    return _dictionaryEnumerator.Current;
                }
                return default;
            }
        }
                
        public void Dispose()
        {
        }
                        
        void IEnumerator.Reset()
        {
            if (_notEmpty)
            {
                ((IEnumerator)_dictionaryEnumerator).Reset();
            }
        }
    }
}

```

###### b- form file

```c#
// 接口
public interface IFormFile
{    
    string ContentType { get; }        
    string ContentDisposition { get; }        
    IHeaderDictionary Headers { get; }        
    long Length { get; }        
    string Name { get; }        
    string FileName { get; }
        
    Stream OpenReadStream();        
    void CopyTo(Stream target);        
    Task CopyToAsync(Stream target, CancellationToken cancellationToken = default(CancellationToken));
}

// 实现
public class FormFile : IFormFile
{
    // Stream.CopyTo method uses 80KB as the default buffer size.
    private const int DefaultBufferSize = 80 * 1024;
    
    private readonly Stream _baseStream;
    private readonly long _baseStreamOffset;
    
    public string ContentType        
    {
        get 
        { 
            return Headers[HeaderNames.ContentType]; 
        }
        set 
        { 
            Headers[HeaderNames.ContentType] = value; 
        }
    }
    
    public string ContentDisposition
    {
        get 
        { 
            return Headers[HeaderNames.ContentDisposition]; 
        }
        set 
        {
            Headers[HeaderNames.ContentDisposition] = value; 
        }
    }
                    
    public IHeaderDictionary Headers { get; set; } = default!;
        
    public long Length { get; }        
    public string Name { get; }        
    public string FileName { get; }
        
    // 构造函数
    public FormFile(
        Stream baseStream, 
        long baseStreamOffset, 
        long length, 
        string name, 
        string fileName)
    {
        _baseStream = baseStream;
        _baseStreamOffset = baseStreamOffset;
        Length = length;
        Name = name;
        FileName = fileName;
    }
    
    public Stream OpenReadStream()
    {
        return new ReferenceReadStream(
            _baseStream, 
            _baseStreamOffset, 
            Length);
    }    
            
    public void CopyTo(Stream target)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }
        
        using (var readStream = OpenReadStream())
        {
            readStream.CopyTo(target, DefaultBufferSize);
        }
    }
        
    public async Task CopyToAsync(
        Stream target, 
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }
        
        using (var readStream = OpenReadStream())
        {
            await readStream.CopyToAsync(
                target, 
                DefaultBufferSize, 
                cancellationToken);
        }
    }
}

```

###### c- form file collection

```c#
// 接口
public interface IFormFileCollection : IReadOnlyList<IFormFile>
{    
    IFormFile? this[string name] { get; }     
    
    IFormFile? GetFile(string name);        
    IReadOnlyList<IFormFile> GetFiles(string name);
}

// 实现
public class FormFileCollection : List<IFormFile>, IFormFileCollection
{   
    public IFormFile? this[string name] => GetFile(name);
        
    public IFormFile? GetFile(string name)
    {
        foreach (var file in this)
        {
            if (string.Equals(name, file.Name, StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }
        
        return null;
    }
       
    public IReadOnlyList<IFormFile> GetFiles(string name)
    {
        var files = new List<IFormFile>();
        
        foreach (var file in this)
        {
            if (string.Equals(name, file.Name, StringComparison.OrdinalIgnoreCase))
            {
                files.Add(file);
            }
        }
        
        return files;
    }
}

```

###### 2.2.2.4 request cookies feature

```c#
// 接口
public interface IRequestCookiesFeature
{    
    IRequestCookieCollection Cookies { get; set; }
}

// 实现
public class RequestCookiesFeature : IRequestCookiesFeature
{    
    // http request feature with default (null)    
    private FeatureReferences<IHttpRequestFeature> _features;
    
    private IHttpRequestFeature HttpRequestFeature => _features.Fetch(
        ref _features.Cache, 
        _nullRequestFeature)!;
    
    private readonly static Func<IFeatureCollection, IHttpRequestFeature?> _nullRequestFeature = f => null;
        
    private StringValues _original;
    private IRequestCookieCollection? _parsedValues;
    
    public IRequestCookieCollection Cookies
    {
        get
        {
            if (_features.Collection == null)
            {
                if (_parsedValues == null)
                {
                    _parsedValues = RequestCookieCollection.Empty;
                }
                return _parsedValues;
            }
            
            var headers = HttpRequestFeature.Headers;
            StringValues current;
            if (!headers.TryGetValue(HeaderNames.Cookie, out current))
            {
                current = string.Empty;
            }
            
            if (_parsedValues == null || _original != current)
            {
                _original = current;
                _parsedValues = RequestCookieCollection.Parse(current.ToArray());
            }
            
            return _parsedValues;
        }
        set
        {
            _parsedValues = value;
            _original = StringValues.Empty;
            if (_features.Collection != null)
            {
                if (_parsedValues == null || _parsedValues.Count == 0)
                {
                    HttpRequestFeature.Headers.Remove(HeaderNames.Cookie);
                }
                else
                {
                    var headers = new List<string>(_parsedValues.Count);
                    foreach (var pair in _parsedValues)
                    {
                        headers.Add(new CookieHeaderValue(pair.Key, pair.Value).ToString());
                    }
                    _original = headers.ToArray();
                    HttpRequestFeature.Headers[HeaderNames.Cookie] = _original;
                }
            }
        }
    }
    
    /* 构造函数 */    
    public RequestCookiesFeature(IRequestCookieCollection cookies)
    {
        if (cookies == null)
        {
            throw new ArgumentNullException(nameof(cookies));
        }
        
        _parsedValues = cookies;
    }
            
    public RequestCookiesFeature(IFeatureCollection features)
    {
        if (features == null)
        {
            throw new ArgumentNullException(nameof(features));
        }
        
        features.Initalize(features);
        
    }                        
}

```

###### a- request cookie collection

```c#
// 接口
public interface IRequestCookieCollection : IEnumerable<KeyValuePair<string, string>>
{    
    int Count { get; }        
    ICollection<string> Keys { get; }
    string? this[string key] { get; }
    
    bool ContainsKey(string key);        
    bool TryGetValue(
        	 string key, 
	         [MaybeNullWhen(false)] out string? value);            
}

// 实例
internal class RequestCookieCollection : IRequestCookieCollection
{
    // 静态 empty 实例
    public static readonly RequestCookieCollection Empty = new RequestCookieCollection();
    private static readonly string[] EmptyKeys = Array.Empty<string>();   
                        
    private Dictionary<string, string>? Store { get; set; }
           
    public int Count
    {
        get
        {
            if (Store == null)
            {
                return 0;
            }
            return Store.Count;
        }
    }
    
    
    public ICollection<string> Keys
    {
        get
        {
            if (Store == null)
            {
                return EmptyKeys;
            }
            return Store.Keys;
        }
    }
    
    public string? this[string key]
    {
        get
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            
            if (Store == null)
            {
                return null;
            }
            
            if (TryGetValue(key, out var value))
            {
                return value;
            }
            return null;
        }
    }
    
    /* 构造函数 */    
    public RequestCookieCollection()
    {
    }    
    
    public RequestCookieCollection(Dictionary<string, string> store)
    {
        Store = store;
    }
    
    public RequestCookieCollection(int capacity)
    {
        Store = new Dictionary<string, string>(
            capacity, 
            StringComparer.OrdinalIgnoreCase);
    }
    
    /* 静态方法 - parse */    
    public static RequestCookieCollection Parse(IList<string> values) => 
        ParseInternal(
        	values, 
        	AppContext.TryGetSwitch(ResponseCookies.EnableCookieNameEncoding, out var enabled) && enabled);
    
    internal static RequestCookieCollection ParseInternal(
        IList<string> values, 
        bool enableCookieNameEncoding)
    {
        if (values.Count == 0)
        {
            return Empty;
        }
        
        if (CookieHeaderValue.TryParseList(values, out var cookies))
        {
            if (cookies.Count == 0)
            {
                return Empty;
            }
            
            var collection = new RequestCookieCollection(cookies.Count);
            var store = collection.Store!;
            for (var i = 0; i < cookies.Count; i++)
            {
                var cookie = cookies[i];    
                
                var name = enableCookieNameEncoding 
                    		   ? Uri.UnescapeDataString(cookie.Name.Value) 
		                       : cookie.Name.Value;                
                var value = Uri.UnescapeDataString(cookie.Value.Value);
                
                store[name] = value;
            }
            
            return collection;
        }
        return Empty;
    }
            
    /* 方法 */    
    public bool ContainsKey(string key)
    {
        if (Store == null)
        {
            return false;
        }
        return Store.ContainsKey(key);
    }
    
    public bool TryGetValue(
        string key, 
        [MaybeNullWhen(false)] out string? value)
    {
        if (Store == null)
        {
            value = null;
            return false;
        }
        return Store.TryGetValue(key, out value);
    }     
    
    /* 迭代器 */
    
    // empty enumerator    
    private static readonly Enumerator EmptyEnumerator = new Enumerator();    
    private static readonly IEnumerator<KeyValuePair<string, string>> 
        EmptyIEnumeratorType = EmptyEnumerator;
    private static readonly IEnumerator EmptyIEnumerator = EmptyEnumerator;
    
    public Enumerator GetEnumerator()
    {
        if (Store == null || Store.Count == 0)
        {
            // Non-boxed Enumerator
            return EmptyEnumerator;
        }
        // Non-boxed Enumerator
        return new Enumerator(Store.GetEnumerator());
    }
        
    IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
    {
        if (Store == null || Store.Count == 0)
        {
            // Non-boxed Enumerator
            return EmptyIEnumeratorType;
        }
        // Boxed Enumerator
        return GetEnumerator();
    }
        
    IEnumerator IEnumerable.GetEnumerator()
    {
        if (Store == null || Store.Count == 0)
        {
            // Non-boxed Enumerator
            return EmptyIEnumerator;
        }
        // Boxed Enumerator
        return GetEnumerator();
    }
    
    // enumerator 结构体
    public struct Enumerator : IEnumerator<KeyValuePair<string, string>>
    {
        // Do NOT make this readonly, or MoveNext will not work
        private Dictionary<string, string>.Enumerator _dictionaryEnumerator;
        private bool _notEmpty;
        
        public KeyValuePair<string, string> Current
        {
            get
            {
                if (_notEmpty)
                {
                    var current = _dictionaryEnumerator.Current;
                    return new KeyValuePair<string, string>(current.Key, current.Value);
                }
                return default(KeyValuePair<string, string>);
            }
        }
        
        object IEnumerator.Current
        {
            get
            {
                return Current;
            }
        }
                
        internal Enumerator(Dictionary<string, string>.Enumerator dictionaryEnumerator)
        {
            _dictionaryEnumerator = dictionaryEnumerator;
            _notEmpty = true;
        }
        
        public bool MoveNext()
        {
            if (_notEmpty)
            {
                return _dictionaryEnumerator.MoveNext();
            }
            return false;
        }                
        
        public void Dispose()
        {
        }
        
        public void Reset()
        {
            if (_notEmpty)
            {
                ((IEnumerator)_dictionaryEnumerator).Reset();
            }
        }
    }        
}

```

###### 2.2.2.5 route value feature

```c#
// 接口
public interface IRouteValuesFeature
{    
    RouteValueDictionary RouteValues { get; set; }
}

// 实现
public class RouteValuesFeature : IRouteValuesFeature
{
    private RouteValueDictionary? _routeValues;           
    public RouteValueDictionary RouteValues
    {
        get
        {
            if (_routeValues == null)
            {
                _routeValues = new RouteValueDictionary();
            }
            
            return _routeValues;
        }
        set => _routeValues = value;
    }
}


```

###### a- route value dictionary

```c#
public class RouteValueDictionary : 		
	IDictionary<string, object?>, 	
	IReadOnlyDictionary<string, object?>
{
    // 4 is a good default capacity here because 
    // that leaves enough space for area/controller/action/id
    private const int DefaultCapacity = 4;
        
    internal KeyValuePair<string, object?>[] _arrayStorage;
    internal PropertyStorage? _propertyStorage;
        
    public IEqualityComparer<string> Comparer => StringComparer.OrdinalIgnoreCase;

    bool ICollection<KeyValuePair<string, object?>>.IsReadOnly => false;
     
    // count    
    private int _count;    
    public int Count => _count;                

    // keys 
    IEnumerable<string> IReadOnlyDictionary<string, object?>.Keys => Keys;
    public ICollection<string> Keys
    {
        get
        {
            EnsureCapacity(_count);
            
            var array = _arrayStorage;
            var keys = new string[_count];
            for (var i = 0; i < keys.Length; i++)
            {
                keys[i] = array[i].Key;
            }
            
            return keys;
        }
    }
            
    // values
    IEnumerable<object?> IReadOnlyDictionary<string, object?>.Values => Values;    
    public ICollection<object?> Values
    {
        get
        {
            EnsureCapacity(_count);
            
            var array = _arrayStorage;
            var values = new object?[_count];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = array[i].Value;
            }
            
            return values;
        }
    }
    
    public object? this[string key]
    {
        get
        {
            if (key == null)
            {
                ThrowArgumentNullExceptionForKey();
            }
            
            TryGetValue(key, out var value);
            return value;
        }
        
        set
        {
            if (key == null)
            {
                ThrowArgumentNullExceptionForKey();
            }
            
            // We're calling this here for the side-effect of converting from properties to array. 
            // We need to create the array even if we just set an existing value since property storage is immutable.
            EnsureCapacity(_count);
            
            var index = FindIndex(key);
            if (index < 0)
            {
                EnsureCapacity(_count + 1);
                _arrayStorage[_count++] = new KeyValuePair<string, object?>(key, value);
            }
            else
            {
                _arrayStorage[index] = new KeyValuePair<string, object?>(key, value);
            }
        }
    }
        
    /* 构造 */
    public RouteValueDictionary()
    {
        _arrayStorage = Array.Empty<KeyValuePair<string, object?>>();
    }
            
    public RouteValueDictionary(object? values)
    {
        if (values is RouteValueDictionary dictionary)
        {
            if (dictionary._propertyStorage != null)
            {
                // PropertyStorage is immutable so we can just copy it.
                _propertyStorage = dictionary._propertyStorage;
                _count = dictionary._count;
                _arrayStorage = Array.Empty<KeyValuePair<string, object?>>();
                return;
            }
            
            var count = dictionary._count;
            if (count > 0)
            {
                var other = dictionary._arrayStorage;
                var storage = new KeyValuePair<string, object?>[count];
                Array.Copy(other, 0, storage, 0, count);
                _arrayStorage = storage;
                _count = count;
            }
            else
            {
                _arrayStorage = Array.Empty<KeyValuePair<string, object?>>();
            }
            
            return;
        }
        
        if (values is IEnumerable<KeyValuePair<string, object>> keyValueEnumerable)
        {
            _arrayStorage = Array.Empty<KeyValuePair<string, object?>>();
                        
            foreach (var kvp in keyValueEnumerable)
            {
                Add(kvp.Key, kvp.Value);
            }
            
            return;
        }
        if (values is IEnumerable<KeyValuePair<string, string>> stringValueEnumerable)
        {
            _arrayStorage = Array.Empty<KeyValuePair<string, object?>>();
            
            foreach (var kvp in stringValueEnumerable)
            {
                Add(kvp.Key, kvp.Value);
            }
            
            return;
        }
        
        if (values != null)
        {
            var storage = new PropertyStorage(values);
            _propertyStorage = storage;
            _count = storage.Properties.Length;
            _arrayStorage = Array.Empty<KeyValuePair<string, object?>>();
        }
        else
        {
            _arrayStorage = Array.Empty<KeyValuePair<string, object?>>();
        }
    }    
        
    // (parse) from array
    public static RouteValueDictionary FromArray(KeyValuePair<string, object?>[] items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }
        
        // We need to compress the array by removing non-contiguous items. 
        // We typically have a very small number of items to process. We don't need to preserve order.
        var start = 0;
        var end = items.Length - 1;
        
        // We walk forwards from the beginning of the array and fill in 'null' slots.
        // We walk backwards from the end of the array end move items in non-null' slots into whatever start is pointing to. O(n)
        while (start <= end)
        {
            if (items[start].Key != null)
            {
                start++;
            }
            else if (items[end].Key != null)
            {
                // Swap this item into start and advance
                items[start] = items[end];
                items[end] = default;
                start++;
                end--;
            }
            else
            {
                // Both null, we need to hold on 'start' since we still need to fill it with something.
                end--;
            }
        }
        
        return new RouteValueDictionary()
        {
            _arrayStorage = items!,
            _count = start,
        };
    }
                                        
    /* 方法 */
    void ICollection<KeyValuePair<string, object?>>.Add(KeyValuePair<string, object?> item)
    {
        Add(item.Key, item.Value);
    }
            
    public void Add(string key, object? value)
    {
        if (key == null)
        {
            ThrowArgumentNullExceptionForKey();
        }
        
        EnsureCapacity(_count + 1);
        
        if (ContainsKeyArray(key))
        {
            var message = Resources.FormatRouteValueDictionary_DuplicateKey(key, nameof(RouteValueDictionary));
            throw new ArgumentException(message, nameof(key));
        }
        
        _arrayStorage[_count] = new KeyValuePair<string, object?>(key, value);
        _count++;
    }
                
    public void Clear()
    {
        if (_count == 0)
        {
            return;
        }
        
        if (_propertyStorage != null)
        {
            _arrayStorage = Array.Empty<KeyValuePair<string, object?>>();
            _propertyStorage = null;
            _count = 0;
            return;
        }
        
        Array.Clear(_arrayStorage, 0, _count);
        _count = 0;
    }
                
    bool ICollection<KeyValuePair<string, object?>>.Contains(KeyValuePair<string, object?> item)
    {
        return TryGetValue(item.Key, out var value) && EqualityComparer<object>.Default.Equals(value, item.Value);
    }
        
    public bool ContainsKey(string key)
    {
        if (key == null)
        {
            ThrowArgumentNullExceptionForKey();
        }
        
        return ContainsKeyCore(key);
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ContainsKeyCore(string key)
    {
        if (_propertyStorage == null)
        {
            return ContainsKeyArray(key);
        }
        
        return ContainsKeyProperties(key);
    }
                
    void ICollection<KeyValuePair<string, object?>>.CopyTo(
        KeyValuePair<string, object?>[] array,
        int arrayIndex)
    {
        if (array == null)
        {
            throw new ArgumentNullException(nameof(array));
        }
        
        if (arrayIndex < 0 || 
            arrayIndex > array.Length || 
            array.Length - arrayIndex < this.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }
        
        if (Count == 0)
        {
            return;
        }
        
        EnsureCapacity(Count);
        
        var storage = _arrayStorage;
        Array.Copy(storage, 0, array, arrayIndex, _count);
    }
    
    /* 迭代器 */
        
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

       
        IEnumerator<KeyValuePair<string, object?>> IEnumerable<KeyValuePair<string, object?>>.GetEnumerator()
        {
            return GetEnumerator();
        }

       
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

       
        bool ICollection<KeyValuePair<string, object?>>.Remove(KeyValuePair<string, object?> item)
        {
            if (Count == 0)
            {
                return false;
            }

            Debug.Assert(_arrayStorage != null);

            EnsureCapacity(Count);

            var index = FindIndex(item.Key);
            var array = _arrayStorage;
            if (index >= 0 && EqualityComparer<object>.Default.Equals(array[index].Value, item.Value))
            {
                Array.Copy(array, index + 1, array, index, _count - index);
                _count--;
                array[_count] = default;
                return true;
            }

            return false;
        }

       
        public bool Remove(string key)
        {
            if (key == null)
            {
                ThrowArgumentNullExceptionForKey();
            }

            if (Count == 0)
            {
                return false;
            }

            // Ensure property storage is converted to array storage as we'll be applying the lookup and removal on the array
            EnsureCapacity(_count);

            var index = FindIndex(key);
            if (index >= 0)
            {
                _count--;
                var array = _arrayStorage;
                Array.Copy(array, index + 1, array, index, _count - index);
                array[_count] = default;

                return true;
            }

            return false;
        }

       
        public bool Remove(string key, out object? value)
        {
            if (key == null)
            {
                ThrowArgumentNullExceptionForKey();
            }

            if (_count == 0)
            {
                value = default;
                return false;
            }

            // Ensure property storage is converted to array storage as we'll be applying the lookup and removal on the array
            EnsureCapacity(_count);

            var index = FindIndex(key);
            if (index >= 0)
            {
                _count--;
                var array = _arrayStorage;
                value = array[index].Value;
                Array.Copy(array, index + 1, array, index, _count - index);
                array[_count] = default;

                return true;
            }

            value = default;
            return false;
        }

       
        public bool TryAdd(string key, object? value)
        {
            if (key == null)
            {
                ThrowArgumentNullExceptionForKey();
            }

            if (ContainsKeyCore(key))
            {
                return false;
            }

            EnsureCapacity(Count + 1);
            _arrayStorage[Count] = new KeyValuePair<string, object?>(key, value);
            _count++;
            return true;
        }

        
        public bool TryGetValue(string key, out object? value)
        {
            if (key == null)
            {
                ThrowArgumentNullExceptionForKey();
            }

            if (_propertyStorage == null)
            {
                return TryFindItem(key, out value);
            }

            return TryGetValueSlow(key, out value);
        }

        private bool TryGetValueSlow(string key, out object? value)
        {
            if (_propertyStorage != null)
            {
                var storage = _propertyStorage;
                for (var i = 0; i < storage.Properties.Length; i++)
                {
                    if (string.Equals(storage.Properties[i].Name, key, StringComparison.OrdinalIgnoreCase))
                    {
                        value = storage.Properties[i].GetValue(storage.Value);
                        return true;
                    }
                }
            }

            value = default;
            return false;
        }

        [DoesNotReturn]
        private static void ThrowArgumentNullExceptionForKey()
        {
            throw new ArgumentNullException("key");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int capacity)
        {
            if (_propertyStorage != null || _arrayStorage.Length < capacity)
            {
                EnsureCapacitySlow(capacity);
            }
        }

        private void EnsureCapacitySlow(int capacity)
        {
            if (_propertyStorage != null)
            {
                var storage = _propertyStorage;

                // If we're converting from properties, it's likely due to an 'add' to make sure we have at least
                // the default amount of space.
                capacity = Math.Max(DefaultCapacity, Math.Max(storage.Properties.Length, capacity));
                var array = new KeyValuePair<string, object?>[capacity];

                for (var i = 0; i < storage.Properties.Length; i++)
                {
                    var property = storage.Properties[i];
                    array[i] = new KeyValuePair<string, object?>(property.Name, property.GetValue(storage.Value));
                }

                _arrayStorage = array;
                _propertyStorage = null;
                return;
            }

            if (_arrayStorage.Length < capacity)
            {
                capacity = _arrayStorage.Length == 0 ? DefaultCapacity : _arrayStorage.Length * 2;
                var array = new KeyValuePair<string, object?>[capacity];
                if (_count > 0)
                {
                    Array.Copy(_arrayStorage, 0, array, 0, _count);
                }

                _arrayStorage = array;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindIndex(string key)
        {
            // Generally the bounds checking here will be elided by the JIT because this will be called
            // on the same code path as EnsureCapacity.
            var array = _arrayStorage;
            var count = _count;

            for (var i = 0; i < count; i++)
            {
                if (string.Equals(array[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryFindItem(string key, out object? value)
        {
            var array = _arrayStorage;
            var count = _count;

            // Elide bounds check for indexing.
            if ((uint)count <= (uint)array.Length)
            {
                for (var i = 0; i < count; i++)
                {
                    if (string.Equals(array[i].Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        value = array[i].Value;
                        return true;
                    }
                }
            }

            value = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ContainsKeyArray(string key)
        {
            var array = _arrayStorage;
            var count = _count;

            // Elide bounds check for indexing.
            if ((uint)count <= (uint)array.Length)
            {
                for (var i = 0; i < count; i++)
                {
                    if (string.Equals(array[i].Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ContainsKeyProperties(string key)
        {
            Debug.Assert(_propertyStorage != null);

            var properties = _propertyStorage.Properties;
            for (var i = 0; i < properties.Length; i++)
            {
                if (string.Equals(properties[i].Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public struct Enumerator : IEnumerator<KeyValuePair<string, object?>>
        {
            private readonly RouteValueDictionary _dictionary;
            private int _index;

            /// <summary>
            /// Instantiates a new enumerator with the values provided in <paramref name="dictionary"/>.
            /// </summary>
            /// <param name="dictionary">A <see cref="RouteValueDictionary"/>.</param>
            public Enumerator(RouteValueDictionary dictionary)
            {
                if (dictionary == null)
                {
                    throw new ArgumentNullException();
                }

                _dictionary = dictionary;

                Current = default;
                _index = 0;
            }

            /// <inheritdoc />
            public KeyValuePair<string, object?> Current { get; private set; }

            object IEnumerator.Current => Current;

            /// <summary>
            /// Releases resources used by the <see cref="Enumerator"/>.
            /// </summary>
            public void Dispose()
            {
            }

            // Similar to the design of List<T>.Enumerator - Split into fast path and slow path for inlining friendliness
            /// <inheritdoc />
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                var dictionary = _dictionary;

                // The uncommon case is that the propertyStorage is in use
                if (dictionary._propertyStorage == null && ((uint)_index < (uint)dictionary._count))
                {
                    Current = dictionary._arrayStorage[_index];
                    _index++;
                    return true;
                }

                return MoveNextRare();
            }

            private bool MoveNextRare()
            {
                var dictionary = _dictionary;
                if (dictionary._propertyStorage != null && ((uint)_index < (uint)dictionary._count))
                {
                    var storage = dictionary._propertyStorage;
                    var property = storage.Properties[_index];
                    Current = new KeyValuePair<string, object?>(property.Name, property.GetValue(storage.Value));
                    _index++;
                    return true;
                }

                _index = dictionary._count;
                Current = default;
                return false;
            }

            /// <inheritdoc />
            public void Reset()
            {
                Current = default;
                _index = 0;
            }
        }

        internal class PropertyStorage
        {
            private static readonly ConcurrentDictionary<Type, PropertyHelper[]> _propertyCache = new ConcurrentDictionary<Type, PropertyHelper[]>();

            public readonly object Value;
            public readonly PropertyHelper[] Properties;

            public PropertyStorage(object value)
            {
                Debug.Assert(value != null);
                Value = value;

                // Cache the properties so we can know if we've already validated them for duplicates.
                var type = Value.GetType();
                if (!_propertyCache.TryGetValue(type, out Properties!))
                {
                    Properties = PropertyHelper.GetVisibleProperties(type);
                    ValidatePropertyNames(type, Properties);
                    _propertyCache.TryAdd(type, Properties);
                }
            }

            private static void ValidatePropertyNames(Type type, PropertyHelper[] properties)
            {
                var names = new Dictionary<string, PropertyHelper>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < properties.Length; i++)
                {
                    var property = properties[i];

                    if (names.TryGetValue(property.Name, out var duplicate))
                    {
                        var message = Resources.FormatRouteValueDictionary_DuplicatePropertyName(
                            type.FullName,
                            property.Name,
                            duplicate.Name,
                            nameof(RouteValueDictionary));
                        throw new InvalidOperationException(message);
                    }

                    names.Add(property.Name, property);
                }
            }
        }
    }

```

###### 2.2.2.6 request body pipe feature

```c#
// 接口
public interface IRequestBodyPipeFeature
{    
    PipeReader Reader { get; }
}

// 实现
public class RequestBodyPipeFeature : IRequestBodyPipeFeature
{
    private PipeReader? _internalPipeReader;
    private Stream? _streamInstanceWhenWrapped;
    private HttpContext _context;
    
    public PipeReader Reader
    {
        get
        {
            if (_internalPipeReader == null ||
                !ReferenceEquals(_streamInstanceWhenWrapped, _context.Request.Body))
            {
                _streamInstanceWhenWrapped = _context.Request.Body;
                _internalPipeReader = PipeReader.Create(_context.Request.Body);
                
                _context.Response.OnCompleted(
                    self =>
                    {
                        ((PipeReader)self).Complete();
                        return Task.CompletedTask;
                    },
                    _internalPipeReader);
            }
            
            return _internalPipeReader;
        }
    }
    
    public RequestBodyPipeFeature(HttpContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
       _context = context;
    }    
}

```



additional doc

#### 2.3 http response

```c#
public abstract class HttpResponse
{        
    public abstract HttpContext HttpContext { get; }
    
    public abstract int StatusCode { get; set; }                        
    public abstract bool HasStarted { get; }
            
    public abstract IHeaderDictionary Headers { get; }
    public abstract IResponseCookies Cookies { get; }
    
    public abstract long? ContentLength { get; set; }        
    public abstract string ContentType { get; set; }
    public abstract Stream Body { get; set; }        
    public virtual PipeWriter BodyWriter { get => throw new NotImplementedException(); }
    
    
    public virtual void OnStarting(Func<Task> callback) => OnStarting(_callbackDelegate, callback);        
    public abstract void OnStarting(Func<object, Task> callback, object state);
    
    public virtual void OnCompleted(Func<Task> callback) => OnCompleted(_callbackDelegate, callback);               
    public abstract void OnCompleted(Func<object, Task> callback, object state);
                               
    public virtual void Redirect(string location) => Redirect(location, permanent: false);            
    public abstract void Redirect(string location, bool permanent);
        
    public virtual Task StartAsync(CancellationToken cancellationToken = default) 
    { 
        throw new NotImplementedException(); 
    }
        
    public virtual Task CompleteAsync() 
    { 
        throw new NotImplementedException(); 
    }
    
    /* register for dispose */
    public virtual void RegisterForDispose(IDisposable disposable) => OnCompleted(_disposeDelegate, disposable);
    public virtual void RegisterForDisposeAsync(IAsyncDisposable disposable) =>        
        OnCompleted(_disposeAsyncDelegate, disposable);
    
    private static readonly Func<object, Task> _callbackDelegate = callback => ((Func<Task>)callback)();
    
    private static readonly Func<object, Task> _disposeDelegate = disposable =>
    	{
        	((IDisposable)disposable).Dispose();
        	return Task.CompletedTask;
    	};
    
    private static readonly Func<object, Task> _disposeAsyncDelegate = disposable => 
         ((IAsyncDisposable)disposable).DisposeAsync().AsTask();
}

```

##### 2.3.1 default http response

```c#
internal sealed class DefaultHttpResponse : HttpResponse
{            
    private readonly DefaultHttpContext _context;
    public override HttpContext HttpContext { get { return _context; } }
    
    private FeatureReferences<FeatureInterfaces> _features;
    struct FeatureInterfaces
    {
        public IHttpResponseFeature? Response;
        public IHttpResponseBodyFeature? ResponseBody;
        public IResponseCookiesFeature? Cookies;
    }
            
    public DefaultHttpResponse(DefaultHttpContext context)
    {
        _context = context;
        _features.Initalize(context.Features);
    }
    
    // initialize
    public void Initialize()
    {
        _features.Initalize(_context.Features);
    }
    
    public void Initialize(int revision)
    {
        _features.Initalize(_context.Features, revision);
    }
    
    public void Uninitialize()
    {
        _features = default;
    }        
}

```

###### 2.3.1.1 features

```c#
internal sealed class DefaultHttpResponse : HttpResponse
{       
    /* http response feature with default (null) */
    private IHttpResponseFeature HttpResponseFeature => _features.Fetch(
        ref _features.Cache.Response, 
        _nullResponseFeature)!;    
    
    private readonly static Func<IFeatureCollection, IHttpResponseFeature?> _nullResponseFeature = f => null;
    
    /* response cookies feature with default */ 
    private IResponseCookiesFeature ResponseCookiesFeature => _features.Fetch(
        ref _features.Cache.Cookies, 
        _newResponseCookiesFeature)!;
    
    private readonly static Func<IFeatureCollection, IResponseCookiesFeature?> _newResponseCookiesFeature = f => 
        new ResponseCookiesFeature(f);
        
    /* http response body feature with default */
    private IHttpResponseBodyFeature HttpResponseBodyFeature => _features.Fetch(
        ref _features.Cache.ResponseBody, 
        _nullResponseBodyFeature)!;
    
    private readonly static Func<IFeatureCollection, IHttpResponseBodyFeature?> _nullResponseBodyFeature = f => null;    
}

```

###### 2.3.1.2 props

```c#
internal sealed class DefaultHttpResponse : HttpResponse
{        
    // 从 http response feature 解析 status code
    public override int StatusCode
    {
        get { return HttpResponseFeature.StatusCode; }
        set { HttpResponseFeature.StatusCode = value; }
    }
    
    // 从 http response feature 解析 has started
    public override bool HasStarted
    {
        get { return HttpResponseFeature.HasStarted; }
    }
    
    // 从 http response feature 解析 (response headers)
    public override IHeaderDictionary Headers
    {
        get { return HttpResponseFeature.Headers; }
    }
    
    // 从 response cookies feature 解析 response cookies
    public override IResponseCookies Cookies
    {
        get { return ResponseCookiesFeature.Cookies; }
    }
    
    // 从 (response) headers 解析 content length
    public override long? ContentLength
    {
        get { return Headers.ContentLength; }
        set { Headers.ContentLength = value; }
    }
    
    // 从 (response) headers 解析 content type
    public override string ContentType
    {
        get
        {
            return Headers[HeaderNames.ContentType];
        }
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                HttpResponseFeature.Headers.Remove(HeaderNames.ContentType);
            }
            else
            {
                HttpResponseFeature.Headers[HeaderNames.ContentType] = value;
            }
        }
    }
    
    // 从 response body feature 解析 stream
    public override Stream Body
    {
        get { return HttpResponseBodyFeature.Stream; }
        set
        {
            var otherFeature = _features.Collection.Get<IHttpResponseBodyFeature>()!;
            
            if (otherFeature is StreamResponseBodyFeature streamFeature && 
                streamFeature.PriorFeature != null && 
                object.ReferenceEquals(
                    value, 
                    streamFeature.PriorFeature.Stream))
            {
                // They're reverting the stream back to the prior one. 
                // Revert the whole feature.
                _features.Collection.Set(streamFeature.PriorFeature);
                return;
            }
            
            _features.Collection.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(value, otherFeature));
        }
    }
                      
    // 从 response body feature 解析 body writer
    public override PipeWriter BodyWriter
    {
        get { return HttpResponseBodyFeature.Writer; }
    }
}

```

###### 2.3.1.3 方法 - start / complete / redirect

```c#
internal sealed class DefaultHttpResponse : HttpResponse
{  
    /* start */
    public override void OnStarting(Func<object, Task> callback, object state)
    {
        if (callback == null)
        {
            throw new ArgumentNullException(nameof(callback));
        }
        
        HttpResponseFeature.OnStarting(callback, state);
    }
    
    public override Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (HasStarted)
        {
            return Task.CompletedTask;
        }
        
        return HttpResponseBodyFeature.StartAsync(cancellationToken);
    }
    
    /* complete */
    public override void OnCompleted(Func<object, Task> callback, object state)
    {
        if (callback == null)
        {
            throw new ArgumentNullException(nameof(callback));
        }
        
        HttpResponseFeature.OnCompleted(callback, state);
    }
    
    public override Task CompleteAsync() => HttpResponseBodyFeature.CompleteAsync();   
    
    /* redirect */
    public override void Redirect(string location, bool permanent)
    {
        if (permanent)
        {
            HttpResponseFeature.StatusCode = 301;
        }
        else
        {
            HttpResponseFeature.StatusCode = 302;
        }
        
        Headers[HeaderNames.Location] = location;
    }
}


public static class ResponseExtensions
{
    // redirect
    public static void Redirect(
        this HttpResponse response, 
        string location, 
        bool permanent, 
        bool preserveMethod)
    {
        if (preserveMethod)
        {
            response.StatusCode = permanent 
                ? StatusCodes.Status308PermanentRedirect 
                : StatusCodes.Status307TemporaryRedirect;
        }
        else
        {
            
            response.StatusCode = permanent 
                ? StatusCodes.Status301MovedPermanently 
                : StatusCodes.Status302Found;
        }
        
        response.Headers[HeaderNames.Location] = location;
    }
    
    // clear
    public static void Clear(this HttpResponse response)
    {
        if (response.HasStarted)
        {
            throw new InvalidOperationException("The response cannot be cleared, it has already started sending.");
        }
        
        response.StatusCode = 200;        
        response.HttpContext.Features.Get<IHttpResponseFeature>()!.ReasonPhrase = null;
        
        response.Headers.Clear();
        if (response.Body.CanSeek)
        {
            response.Body.SetLength(0);
        }
    }
}

```

###### 2.3.1.4 方法 - send file

```c#
public static class SendFileResponseExtensions
{
    private const int StreamCopyBufferSize = 64 * 1024;
        
    [SuppressMessage(
        "ApiDesign", 
        "RS0026:Do not add multiple public overloads with optional parameters", 
        Justification = "Required to maintain compatibility")]
    public static Task SendFileAsync(
        this HttpResponse response, 
        IFileInfo file, 
        CancellationToken cancellationToken = default)
    {
        if (response == null)
        {
            throw new ArgumentNullException(nameof(response));
        }
        if (file == null)
        {
            throw new ArgumentNullException(nameof(file));
        }
        
        return SendFileAsyncCore(
            response, 
            file, 
            0, 
            null, 
            cancellationToken);
    }
            
    [SuppressMessage(
        "ApiDesign", 
        "RS0026:Do not add multiple public overloads with optional parameters", 
        Justification = "Required to maintain compatibility")]
    public static Task SendFileAsync(
        this HttpResponse response, 
        IFileInfo file, 
        long offset, 
        long? count, 
        CancellationToken cancellationToken = default)
    {
        if (response == null)
        {
            throw new ArgumentNullException(nameof(response));
        }
        if (file == null)
        {
            throw new ArgumentNullException(nameof(file));
        }
        
        return SendFileAsyncCore(
            response, 
            file, 
            offset, 
            count, 
            cancellationToken);
    }
        
    [SuppressMessage(
        "ApiDesign", 
        "RS0026:Do not add multiple public overloads with optional parameters", 
        Justification = "Required to maintain compatibility")]
    public static Task SendFileAsync(
        this HttpResponse response, 
        string fileName, 
        CancellationToken cancellationToken = default)
    {
        if (response == null)
        {
            throw new ArgumentNullException(nameof(response));
        }        
        if (fileName == null)
        {
            throw new ArgumentNullException(nameof(fileName));
        }
        
        return SendFileAsyncCore(
            response, 
            fileName, 
            0, 
            null, 
            cancellationToken);
    }
            
    [SuppressMessage(
        "ApiDesign", 
        "RS0026:Do not add multiple public overloads with optional parameters", 
        Justification = "Required to maintain compatibility")]
    public static Task SendFileAsync(
        this HttpResponse response, 
        string fileName, 
        long offset, 
        long? count, 
        CancellationToken cancellationToken = default)
    {
        if (response == null)
        {
            throw new ArgumentNullException(nameof(response));
        }        
        if (fileName == null)
        {
            throw new ArgumentNullException(nameof(fileName));
        }
        
        return SendFileAsyncCore(
            response, 
            fileName, 
            offset, 
            count, 
            cancellationToken);
    }

    private static async Task SendFileAsyncCore(
        HttpResponse response, 
        IFileInfo file, 
        long offset, 
        long? count, 
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(file.PhysicalPath))
        {
            CheckRange(offset, count, file.Length);
            using var fileContent = file.CreateReadStream();
            
            var useRequestAborted = !cancellationToken.CanBeCanceled;
            var localCancel = useRequestAborted 
                ? response.HttpContext.RequestAborted 
                : cancellationToken;
            
            try
            {
                localCancel.ThrowIfCancellationRequested();
                if (offset > 0)
                {
                    fileContent.Seek(offset, SeekOrigin.Begin);
                }
                await StreamCopyOperation.CopyToAsync(
                    fileContent, 
                    response.Body, 
                    count, 
                    StreamCopyBufferSize, 
                    localCancel);
            }
            catch (OperationCanceledException) when (useRequestAborted) 
            {
            }
        }
        else
        {
            await response.SendFileAsync(
                file.PhysicalPath, 
                offset, 
                count, 
                cancellationToken);
        }
    }
    
    // send file core
    private static async Task SendFileAsyncCore(
        HttpResponse response, 
        string fileName, 
        long offset, 
        long? count, 
        CancellationToken cancellationToken = default)
    {
        var useRequestAborted = !cancellationToken.CanBeCanceled;
        var localCancel = useRequestAborted 
            ? response.HttpContext.RequestAborted 
            : cancellationToken;
        var sendFile = response.HttpContext.Features.Get<IHttpResponseBodyFeature>()!;
        
        try
        {
            await sendFile.SendFileAsync(
                fileName, 
                offset, 
                count, 
                localCancel);
        }
        catch (OperationCanceledException) when (useRequestAborted) { }
    }
    
    private static void CheckRange(
        long offset, 
        long? count, 
        long fileLength)
    {
        if (offset < 0 || offset > fileLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset), 
                offset, 
                string.Empty);
        }
        if (count.HasValue &&
            (count.GetValueOrDefault() < 0 || count.GetValueOrDefault() > fileLength - offset))
        {
            throw new ArgumentOutOfRangeException(
                nameof(count), 
                count, 
                string.Empty);
        }
    }
}

```

###### 2.3.1.5 方法 - write

```c#
public static class HttpResponseWritingExtensions
{
    private const int UTF8MaxByteLength = 6;
        
    [SuppressMessage(
        "ApiDesign", 
        "RS0026:Do not add multiple public overloads with optional parameters", 
        Justification = "Required to maintain compatibility")]
    public static Task WriteAsync(
        this HttpResponse response, 
        string text, 
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (response == null)
        {
            throw new ArgumentNullException(nameof(response));
        }        
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }
        
        return response.WriteAsync(
            text, 
            Encoding.UTF8, 
            cancellationToken);
    }
        
    [SuppressMessage(
        "ApiDesign", 
        "RS0026:Do not add multiple public overloads with optional parameters", 
        Justification = "Required to maintain compatibility")]
    public static Task WriteAsync(
        this HttpResponse response, 
        string text, 
        Encoding encoding, 
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (response == null)
        {
            throw new ArgumentNullException(nameof(response));
        }        
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }        
        if (encoding == null)
        {
            throw new ArgumentNullException(nameof(encoding));
        }
        
        // Need to call StartAsync before GetMemory/GetSpan
        if (!response.HasStarted)
        {
            var startAsyncTask = response.StartAsync(cancellationToken);
            if (!startAsyncTask.IsCompletedSuccessfully)
            {
                return StartAndWriteAsyncAwaited(
                    response, 
                    text, 
                    encoding, 
                    cancellationToken, 
                    startAsyncTask);
            }
        }
        
        Write(response, text, encoding);
        
        var flushAsyncTask = response.BodyWriter.FlushAsync(cancellationToken);
        if (flushAsyncTask.IsCompletedSuccessfully)
        {
            // Most implementations of ValueTask reset state in GetResult, 
            // so call it before returning a completed task.
            flushAsyncTask.GetAwaiter().GetResult();
            return Task.CompletedTask;
        }
        
        return flushAsyncTask.AsTask();
    }
    
    private static async Task StartAndWriteAsyncAwaited(
        this HttpResponse response, 
        string text, Encoding encoding, 
        CancellationToken cancellationToken, 
        Task startAsyncTask)
    {
        await startAsyncTask;
        Write(response, text, encoding);
        await response.BodyWriter.FlushAsync(cancellationToken);
    }
    
    private static void Write(
        this HttpResponse response, 
        string text, 
        Encoding encoding)
    {
        var minimumByteSize = GetEncodingMaxByteSize(encoding);
        var pipeWriter = response.BodyWriter;
        var encodedLength = encoding.GetByteCount(text);
        var destination = pipeWriter.GetSpan(minimumByteSize);
        
        if (encodedLength <= destination.Length)
        {
            // Just call Encoding.GetBytes if everything will fit into a single segment.
            var bytesWritten = encoding.GetBytes(text, destination);
            pipeWriter.Advance(bytesWritten);
        }
        else
        {
            WriteMultiSegmentEncoded(
                pipeWriter, 
                text, 
                encoding, 
                destination, 
                encodedLength, 
                minimumByteSize);
        }
    }
    
    private static int GetEncodingMaxByteSize(Encoding encoding)
    {
        if (encoding == Encoding.UTF8)
        {
            return UTF8MaxByteLength;
        }
        
        return encoding.GetMaxByteCount(1);
    }
    
    private static void WriteMultiSegmentEncoded(
        PipeWriter writer, 
        string text, 
        Encoding encoding, 
        Span<byte> destination, 
        int encodedLength, 
        int minimumByteSize)
    {
        var encoder = encoding.GetEncoder();
        var source = text.AsSpan();
        var completed = false;
        var totalBytesUsed = 0;
        
        // This may be a bug, but encoder.Convert returns completed = true for UTF7 too early.
        // Therefore, we check encodedLength - totalBytesUsed too.
        while (!completed || 
               encodedLength - totalBytesUsed != 0)
        {
            // 'text' is a complete string, the converter should always flush its buffer.
            encoder.Convert(
                source, 
                destination, 
                flush: true, 
                out var charsUsed, 
                out var bytesUsed, 
                out completed);
            
            totalBytesUsed += bytesUsed;
            
            writer.Advance(bytesUsed);
            source = source.Slice(charsUsed);
            
            destination = writer.GetSpan(minimumByteSize);
        }
    }
}

```

###### 2.3.1.6 方法 - write json

```c#
public static partial class HttpResponseJsonExtensions
{        
    [SuppressMessage(
        "ApiDesign", 
        "RS0026:Do not add multiple public overloads with optional parameters", 
        Justification = "Required to maintain compatibility")]
    public static Task WriteAsJsonAsync<TValue>(
        this HttpResponse response,
        TValue value,
        CancellationToken cancellationToken = default)
    {
        return response.WriteAsJsonAsync<TValue>(
            value, 
            options: null, 
            contentType: null, 
            cancellationToken);
    }
        
    [SuppressMessage(
        "ApiDesign", 
        "RS0026:Do not add multiple public overloads with optional parameters", 
        Justification = "Required to maintain compatibility")]
    public static Task WriteAsJsonAsync<TValue>(
        this HttpResponse response,
        TValue value,
        JsonSerializerOptions? options,
        CancellationToken cancellationToken = default)
    {
        return response.WriteAsJsonAsync<TValue>(
            value, 
            options, 
            contentType: null, 
            cancellationToken);
    }
       
    [SuppressMessage(
        "ApiDesign", 
        "RS0026:Do not add multiple public overloads with optional parameters", 
        Justification = "Required to maintain compatibility")]
    public static Task WriteAsJsonAsync<TValue>(
        this HttpResponse response,
        TValue value,
        JsonSerializerOptions? options,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        if (response == null)
        {
            throw new ArgumentNullException(nameof(response));
        }
        
        options ??= ResolveSerializerOptions(response.HttpContext);        
        response.ContentType = contentType ?? JsonConstants.JsonContentTypeWithCharset;
        
        return JsonSerializer.SerializeAsync<TValue>(
            response.Body, 
            value, 
            options, 
            cancellationToken);
    }
        
    [SuppressMessage(
        "ApiDesign", 
        "RS0026:Do not add multiple public overloads with optional parameters", 
        Justification = "Required to maintain compatibility")]
    public static Task WriteAsJsonAsync(
        this HttpResponse response,
        object? value,
        Type type,
        CancellationToken cancellationToken = default)
    {
        return response.WriteAsJsonAsync(
            value, 
            type, 
            options: null, 
            contentType: null, 
            cancellationToken);
    }
        
    [SuppressMessage(
        "ApiDesign", 
        "RS0026:Do not add multiple public overloads with optional parameters", 
        Justification = "Required to maintain compatibility")]
    public static Task WriteAsJsonAsync(
        this HttpResponse response,
        object? value,
        Type type,
        JsonSerializerOptions? options,
        CancellationToken cancellationToken = default)
    {
        return response.WriteAsJsonAsync(
            value, 
            type, 
            options, 
            contentType: null, 
            cancellationToken);
    }
        
    [SuppressMessage(
        "ApiDesign", 
        "RS0026:Do not add multiple public overloads with optional parameters", 
        Justification = "Required to maintain compatibility")]
    public static Task WriteAsJsonAsync(
        this HttpResponse response,
        object? value,
        Type type,
        JsonSerializerOptions? options,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        if (response == null)
        {
            throw new ArgumentNullException(nameof(response));
        }
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }
        
        options ??= ResolveSerializerOptions(response.HttpContext);        
        response.ContentType = contentType ?? JsonConstants.JsonContentTypeWithCharset;
        
        return JsonSerializer.SerializeAsync(
            response.Body, 
            value, 
            type, 
            options, 
            cancellationToken);
    }
    
    private static JsonSerializerOptions ResolveSerializerOptions(HttpContext httpContext)
    {
        // Attempt to resolve options from DI then fallback to default options
        return httpContext.RequestServices?
			             .GetService<IOptions<JsonOptions>>()?
			             .Value?
			             .SerializerOptions 
            			 	?? JsonOptions.DefaultSerializerOptions;
    }
}

```

###### 2.3.1.7 方法 - trailer

```c#
public static class ResponseTrailerExtensions
{    
    public static void DeclareTrailer(
        this HttpResponse response, 
        string trailerName)
    {
        response.Headers.AppendCommaSeparatedValues(HeaderNames.Trailer, trailerName);
    }
        
    public static bool SupportsTrailers(this HttpResponse response)
    {
        var feature = response.HttpContext.Features.Get<IHttpResponseTrailersFeature>();
        
        return feature?.Trailers != null && 
               !feature.Trailers.IsReadOnly;
    }
        
    public static void AppendTrailer(
        this HttpResponse response, 
        string trailerName, 
        StringValues trailerValues)
    {
        var feature = response.HttpContext.Features.Get<IHttpResponseTrailersFeature>();
        
        if (feature?.Trailers == null || 
            feature.Trailers.IsReadOnly)
        {
            throw new InvalidOperationException("Trailers are not supported for this response.");
        }
        
        feature.Trailers.Append(trailerName, trailerValues);
    }
}

```

###### a- http response trailer feature

```c#
public interface IHttpResponseTrailersFeature
{    
    IHeaderDictionary Trailers { get; set; }
}

```



##### 2.3.2 response features

###### 2.3.2.1 http response feature

```c#
// 接口
public interface IHttpResponseFeature
{    
    int StatusCode { get; set; }        
    string? ReasonPhrase { get; set; }        
    IHeaderDictionary Headers { get; set; }        
    [Obsolete("Use IHttpResponseBodyFeature.Stream instead.", error: false)]
    Stream Body { get; set; }        
    bool HasStarted { get; }
        
    void OnStarting(Func<object, Task> callback, object state);        
    void OnCompleted(Func<object, Task> callback, object state);
}

// 实现
public class HttpResponseFeature : IHttpResponseFeature
{
    public int StatusCode { get; set; }        
    public string? ReasonPhrase { get; set; }        
    public IHeaderDictionary Headers { get; set; }        
    public Stream Body { get; set; }        
    public virtual bool HasStarted => false;
    
    public HttpResponseFeature()
    {
        StatusCode = 200;
        Headers = new HeaderDictionary();
        Body = Stream.Null;
    }
                   
    public virtual void OnStarting(Func<object, Task> callback, object state)
    {
    }
        
    public virtual void OnCompleted(Func<object, Task> callback, object state)
    {
    }
}

```

###### 2.3.2.2 response body feature

```c#
// 接口
public interface IHttpResponseBodyFeature
{    
    Stream Stream { get; }    
    PipeWriter Writer { get; }
            
    void DisableBuffering();      
    
    Task StartAsync(CancellationToken cancellationToken = default);    
    
    Task SendFileAsync(
        string path, 
        long offset, 
        long? count, 
        CancellationToken cancellationToken = default);     
    
    Task CompleteAsync();
}

// 实现
public class StreamResponseBodyFeature : IHttpResponseBodyFeature
{    
    private bool _started;
    private bool _completed;
    private bool _disposed;
    
    public Stream Stream { get; }       
    public IHttpResponseBodyFeature? PriorFeature { get; }

    private PipeWriter? _pipeWriter;    
    public PipeWriter Writer
    {
        get
        {
            if (_pipeWriter == null)
            {
                _pipeWriter = PipeWriter.Create(
                    Stream, 
                    new StreamPipeWriterOptions(leaveOpen: true));
                
                if (_completed)
                {
                    _pipeWriter.Complete();
                }
            }
            
            return _pipeWriter;
        }
    }
        
    public StreamResponseBodyFeature(Stream stream)
    {
        Stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }
            
    public StreamResponseBodyFeature(
        Stream stream, 
        IHttpResponseBodyFeature priorFeature)
    {
        Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        PriorFeature = priorFeature;
    }
                            
    public virtual void DisableBuffering()
    {
        PriorFeature?.DisableBuffering();
    }
           
    public virtual async Task SendFileAsync(
        string path, 
        long offset, 
        long? count, 
        CancellationToken cancellationToken)
    {
        if (!_started)
        {
            await StartAsync(cancellationToken);
        }
        
        await SendFileFallback.SendFileAsync(
            Stream, 
            path, 
            offset, 
            count, 
            cancellationToken);
    }
    
    public virtual async Task CompleteAsync()
    {
        // CompleteAsync is registered with HttpResponse.OnCompleted and there's no way to unregister it.
        // Prevent it from running by marking as disposed.
        if (_disposed)
        {
            return;
        }
        
        if (_completed)
        {
            return;
        }
        
        if (!_started)
        {
            await StartAsync();
        }
        
        _completed = true;
        
        if (_pipeWriter != null)
        {
            await _pipeWriter.CompleteAsync();
        }
    }
    
    public virtual Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_started)
        {
            _started = true;
            return Stream.FlushAsync(cancellationToken);
        }
        
        return Task.CompletedTask;
    }
            
    public void Dispose()
    {
        _disposed = true;
    }
}

// send file fallback
public static class SendFileFallback
{    
    public static async Task SendFileAsync(
        Stream destination, 
        string filePath, 
        long offset, 
        long? count, 
        CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(filePath);
        if (offset < 0 || offset > fileInfo.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset), 
                offset, 
                string.Empty);
        }
        if (count.HasValue &&
            (count.Value < 0 || count.Value > fileInfo.Length - offset))
        {
            throw new ArgumentOutOfRangeException(
                nameof(count), 
                count, 
                string.Empty);
        }
        
        cancellationToken.ThrowIfCancellationRequested();
        
        const int bufferSize = 1024 * 16;
        
        var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: bufferSize,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        
        using (fileStream)
        {
            fileStream.Seek(offset, SeekOrigin.Begin);
            await StreamCopyOperationInternal.CopyToAsync(
                fileStream, 
                destination, 
                count, 
                bufferSize, 
                cancellationToken);
        }
    }
}

```

###### 2.3.2.3 response cookie feature

```c#
// 接口
public interface IResponseCookiesFeature
{    
    IResponseCookies Cookies { get; }
}

// 实现
public class ResponseCookiesFeature : IResponseCookiesFeature
{    
    private readonly static Func<IFeatureCollection, IHttpResponseFeature?> nullResponseFeature = f => null;
    
    private readonly IFeatureCollection _features;
    private IResponseCookies? _cookiesCollection;
    
    public IResponseCookies Cookies
    {
        get
        {
            if (_cookiesCollection == null)
            {
                _cookiesCollection = new ResponseCookies(_features);
            }
            
            return _cookiesCollection;
        }
    }
    
    public ResponseCookiesFeature(IFeatureCollection features)
    {
        _features = features ?? throw new ArgumentNullException(nameof(feaures));
    }        
}

```

###### a- reponse cookies (collection)

```c#
// 接口
public interface IResponseCookies
{    
    void Append(string key, string value);        
    void Append(string key, string value, CookieOptions options);  
    
    void Delete(string key);        
    void Delete(string key, CookieOptions options);
}

// 实现
internal class ResponseCookies : IResponseCookies
{
    internal const string EnableCookieNameEncoding = "Microsoft.AspNetCore.Http.EnableCookieNameEncoding";    
    internal bool _enableCookieNameEncoding = 
        AppContext.TryGetSwitch(EnableCookieNameEncoding, out var enabled) && enabled;

    private ILogger? _logger;
    private readonly IFeatureCollection _features;    
    private IHeaderDictionary Headers { get; set; }
            
    internal ResponseCookies(IFeatureCollection features)
    {
        _features = features;
        Headers = _features.Get<IHttpResponseFeature>()!.Headers;
    }
                
    public void Append(string key, string value)
    {
        var setCookieHeaderValue = new SetCookieHeaderValue(
            _enableCookieNameEncoding ? Uri.EscapeDataString(key) : key,
            Uri.EscapeDataString(value))
        {
            Path = "/"
        };
        
        var cookieValue = setCookieHeaderValue.ToString();
        
        Headers[HeaderNames.SetCookie] = StringValues.Concat(Headers[HeaderNames.SetCookie], cookieValue);
    }
        
    public void Append(string key, string value, CookieOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        // SameSite=None cookies must be marked as Secure.
        if (!options.Secure && 
            options.SameSite == SameSiteMode.None)
        {
            if (_logger == null)
            {
                var services = _features.Get<Features.IServiceProvidersFeature>()?.RequestServices;
                _logger = services?.GetService<ILogger<ResponseCookies>>();
            }
            
            if (_logger != null)
            {
                Log.SameSiteCookieNotSecure(_logger, key);
            }
        }
        
        var setCookieHeaderValue = new SetCookieHeaderValue(
            _enableCookieNameEncoding? Uri.EscapeDataString(key) : key,
            Uri.EscapeDataString(value))
        {
            Domain = options.Domain,
            Path = options.Path,
            Expires = options.Expires,
            MaxAge = options.MaxAge,
            Secure = options.Secure,
            SameSite = (Net.Http.Headers.SameSiteMode)options.SameSite,
            HttpOnly = options.HttpOnly
        };
        
        var cookieValue = setCookieHeaderValue.ToString();
        
        Headers[HeaderNames.SetCookie] = StringValues.Concat(Headers[HeaderNames.SetCookie], cookieValue);
    }
        
    public void Delete(string key)
    {
        Delete(key, new CookieOptions() { Path = "/" });
    }
        
    public void Delete(string key, CookieOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        var encodedKeyPlusEquals = (_enableCookieNameEncoding ? Uri.EscapeDataString(key) : key) + "=";
        
        bool domainHasValue = !string.IsNullOrEmpty(options.Domain);
        bool pathHasValue = !string.IsNullOrEmpty(options.Path);
        
        Func<string, string, CookieOptions, bool> rejectPredicate;
        if (domainHasValue)
        {
            rejectPredicate = (value, encKeyPlusEquals, opts) =>
                value.StartsWith(encKeyPlusEquals, StringComparison.OrdinalIgnoreCase) &&
                value.IndexOf($"domain={opts.Domain}", StringComparison.OrdinalIgnoreCase) != -1;
        }
        else if (pathHasValue)
        {
            rejectPredicate = (value, encKeyPlusEquals, opts) =>
                value.StartsWith(encKeyPlusEquals, StringComparison.OrdinalIgnoreCase) &&
                value.IndexOf($"path={opts.Path}", StringComparison.OrdinalIgnoreCase) != -1;
        }
        else
        {
            rejectPredicate = (value, encKeyPlusEquals, opts) => 
                value.StartsWith(encKeyPlusEquals, StringComparison.OrdinalIgnoreCase);
        }
        
        var existingValues = Headers[HeaderNames.SetCookie];
        if (!StringValues.IsNullOrEmpty(existingValues))
        {
            var values = existingValues.ToArray();
            var newValues = new List<string>();
            
            for (var i = 0; i < values.Length; i++)
            {
                if (!rejectPredicate(
                    	values[i], 
                    	encodedKeyPlusEquals, 
                    	options))
                {
                    newValues.Add(values[i]);
                }
            }
            
            Headers[HeaderNames.SetCookie] = new StringValues(newValues.ToArray());
        }
        
        Append(
            key, 
            string.Empty, 
            new CookieOptions()
            {
                Path = options.Path,
                Domain = options.Domain,
                Expires = DateTimeOffset.UnixEpoch,
                Secure = options.Secure,
                HttpOnly = options.HttpOnly,
                SameSite = options.SameSite
            });
    }
    
    private static class Log
    {
        private static readonly Action<ILogger, string, Exception?> _samesiteNotSecure = 
            LoggerMessage.Define<string>(
            	LogLevel.Warning,
            	EventIds.SameSiteNotSecure,
            	"The cookie '{name}' has set 'SameSite=None' and must also set 'Secure'.");
        
        public static void SameSiteCookieNotSecure(
            ILogger logger, 
            string name)
        {
            _samesiteNotSecure(logger, name, null);
        }
    }
}

// cookie options
public class CookieOptions
{    
    public CookieOptions()
    {
        Path = "/";
    }
            
    public string? Domain { get; set; }        
    public string? Path { get; set; }        
    public DateTimeOffset? Expires { get; set; }        
    public bool Secure { get; set; }        
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Unspecified;        
    public bool HttpOnly { get; set; }        
    public TimeSpan? MaxAge { get; set; }        
    public bool IsEssential { get; set; }
}

```



additional doc

#### 2.4 http context features

##### 2.4.1 service provider

```c#
// 接口
public interface IServiceProvidersFeature
{    
    IServiceProvider RequestServices { get; set; }
}

// 实现
public class ServiceProvidersFeature : IServiceProvidersFeature
{    
    public IServiceProvider RequestServices { get; set; } = default!
}

```

##### 2.4.2 items

```c#
// 接口
public interface IItemsFeature
{    
    IDictionary<object, object?> Items { get; set; }
}

// 实现
public class ItemsFeature : IItemsFeature
{    
    public IDictionary<object, object?> Items { get; set; }    
    public ItemsFeature()
    {
        Items = new ItemsDictionary();
    }        
}

```

###### 2.4.2.1 item dictionary

```c#
internal class ItemsDictionary : IDictionary<object, object?>
{
    // 静态 empty 实例
    private static class EmptyDictionary
    {
        // In own class so only initalized if CopyTo is called on an empty ItemsDictionary
        public readonly static IDictionary<object, object?> Dictionary = new Dictionary<object, object?>();
        public static ICollection<KeyValuePair<object, object?>> Collection => Dictionary;
    }
    
    private IDictionary<object, object?>? _items;
    public IDictionary<object, object?> Items => this;
    
    int ICollection<KeyValuePair<object, object?>>.Count => _items?.Count ?? 0;    
    bool ICollection<KeyValuePair<object, object?>>.IsReadOnly => _items?.IsReadOnly ?? false;
    
    ICollection<object> IDictionary<object, object?>.Keys
    {
        get
        {
            if (_items == null)
            {
                return EmptyDictionary.Dictionary.Keys;
            }
            
            return _items.Keys;
        }
    }
    
    ICollection<object?> IDictionary<object, object?>.Values
    {
        get
        {
            if (_items == null)
            {
                return EmptyDictionary.Dictionary.Values;
            }
            
            return _items.Values;
        }
    }
    
    // Replace the indexer with one that returns null for missing values
    object? IDictionary<object, object?>.this[object key]
    {
        get
        {
            if (_items != null && _items.TryGetValue(key, out var value))
            {
                return value;
            }
            return null;
        }
        set
        {
            EnsureDictionary();
            _items[key] = value;
        }
    }
    
    [MemberNotNull(nameof(_items))]
    private void EnsureDictionary()
    {
        if (_items == null)
        {
            _items = new Dictionary<object, object?>();
        }
    }
            
    // 构造
    public ItemsDictionary()
    {        
    }
    
    public ItemsDictionary(IDictionary<object, object?> items)
    {
        _items = items;
    }
    
    // add             
    void IDictionary<object, object?>.Add(object key, object? value)
    {
        EnsureDictionary();
        _items.Add(key, value);
    }
    
    void ICollection<KeyValuePair<object, object?>>.Add(KeyValuePair<object, object?> item)
    {
        EnsureDictionary();
        _items.Add(item);
    }
    
    // clear
    void ICollection<KeyValuePair<object, object?>>.Clear() => _items?.Clear();
    
    // remove
    bool IDictionary<object, object?>.Remove(object key) => 
        _items != null && _items.Remove(key);
            
    bool ICollection<KeyValuePair<object, object?>>.Remove(KeyValuePair<object, object?> item)
    {
        if (_items == null)
        {
            return false;
        }
        
        if (_items.TryGetValue(item.Key, out var value) && 
            Equals(item.Value, value))
        {
            return _items.Remove(item.Key);
        }
        return false;
    }
    
    // contains
    bool ICollection<KeyValuePair<object, object?>>.Contains(
        KeyValuePair<object, object?> item) => _items != null && _items.Contains(item);
    
    bool IDictionary<object, object?>.ContainsKey(object key) => 
        _items != null && _items.ContainsKey(key);
    
    // get
    bool IDictionary<object, object?>.TryGetValue(object key, out object? value)
    {
        value = null;
        return _items != null && _items.TryGetValue(key, out value);
    }
                            
    // copy to 
    void ICollection<KeyValuePair<object, object?>>.CopyTo(
        KeyValuePair<object, object?>[] array, int arrayIndex)
    {
        if (_items == null)
        {
            //Delegate to Empty Dictionary to do the argument checking.
            EmptyDictionary.Collection.CopyTo(array, arrayIndex);
        }
        
        _items?.CopyTo(array, arrayIndex);
    }
    
    /* 迭代器 */    
    IEnumerator<KeyValuePair<object, object?>> IEnumerable<KeyValuePair<object, object?>>.GetEnumerator() => 
        _items?.GetEnumerator() ?? EmptyEnumerator.Instance;

    IEnumerator IEnumerable.GetEnumerator() => _items?.GetEnumerator() ?? EmptyEnumerator.Instance;
    
    private class EmptyEnumerator : IEnumerator<KeyValuePair<object, object?>>
    {
        // In own class so only initalized if GetEnumerator is called on an empty
        // ItemsDictionary
        public readonly static IEnumerator<KeyValuePair<object, object?>> Instance = 
            new EmptyEnumerator();
        
        public KeyValuePair<object, object?> Current => default;        
        object? IEnumerator.Current => null;
        
        public void Dispose()
        { 
        }
        
        public bool MoveNext() => false;
        
        public void Reset()
        { 
        }
    }        
}

```

##### 2.4.3 http request identifier feature

```c#
// 接口
public interface IHttpRequestIdentifierFeature
{    
    string TraceIdentifier { get; set; }
}

// 实现
public class HttpRequestIdentifierFeature : IHttpRequestIdentifierFeature
{
    // Base32 encoding - in ascii sort order for easy text based sorting
    private static readonly char[] s_encode32Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUV".ToCharArray();
    
    // Seed the _requestId for this application instance with the number of 100-nanosecond intervals that 
    // have elapsed since 12:00:00 midnight, January 1, 0001 for a roughly increasing _requestId over restarts
    private static long _requestId = DateTime.UtcNow.Ticks;
    
    private string? _id = null;
        
    public string TraceIdentifier
    {
        get
        {
            // Don't incur the cost of generating the request ID until it's asked for
            if (_id == null)
            {
                _id = GenerateRequestId(Interlocked.Increment(ref _requestId));
            }
            return _id;
        }
        set
        {
            _id = value;
        }
    }
    
    private static string GenerateRequestId(long id)
    {
        return string.Create(
            13, 
            id, 
            (buffer, value) =>
            {
                char[] encode32Chars = s_encode32Chars;
                
                buffer[12] = encode32Chars[value & 31];
                buffer[11] = encode32Chars[(value >> 5) & 31];
                buffer[10] = encode32Chars[(value >> 10) & 31];
                buffer[9] = encode32Chars[(value >> 15) & 31];
                buffer[8] = encode32Chars[(value >> 20) & 31];
                buffer[7] = encode32Chars[(value >> 25) & 31];
                buffer[6] = encode32Chars[(value >> 30) & 31];
                buffer[5] = encode32Chars[(value >> 35) & 31];
                buffer[4] = encode32Chars[(value >> 40) & 31];
                buffer[3] = encode32Chars[(value >> 45) & 31];
                buffer[2] = encode32Chars[(value >> 50) & 31];
                buffer[1] = encode32Chars[(value >> 55) & 31];
                buffer[0] = encode32Chars[(value >> 60) & 31];
            });
    }
}

```

##### 2.4.4 http request lifetime feature

```c#
// 接口
public interface IHttpRequestLifetimeFeature
{    
    CancellationToken RequestAborted { get; set; }
        
    void Abort();
}

// 实现
public class HttpRequestLifetimeFeature : IHttpRequestLifetimeFeature
{    
    public CancellationToken RequestAborted { get; set; }
    
    public void Abort()
    {
    }
}

```

##### 2.4.5 session featrue

```c#
// 接口
public interface ISessionFeature
{    
    ISession Session { get; set; }
}

// 实现
public class DefaultSessionFeature : ISessionFeature
{   
    public ISession Session { get; set; } = default!;
}

```

###### 2.4.5.1 session

```c#
public interface ISession
{    
    bool IsAvailable { get; }        
    string Id { get; }        
    IEnumerable<string> Keys { get; }
        
    Task LoadAsync(CancellationToken cancellationToken = default(CancellationToken));        
    Task CommitAsync(CancellationToken cancellationToken = default(CancellationToken));        
    bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value);        
    void Set(string key, byte[] value);        
    void Remove(string key);        
    void Clear();
}

```

###### 2.4.5.2 session 扩展方法

```c#
public static class SessionExtensions
{   
    // get
    public static byte[]? Get(this ISession session, string key)
    {
        session.TryGetValue(key, out var value);
        return value;
    }
    
    /* get & set int32 */
    public static int? GetInt32(this ISession session, string key)
    {
        var data = session.Get(key);
        if (data == null || data.Length < 4)
        {
            return null;
        }
        return data[0] << 24 | data[1] << 16 | data[2] << 8 | data[3];
    }
    
    public static void SetInt32(this ISession session, string key, int value)
    {
        var bytes = new byte[]
        {
            (byte)(value >> 24),
            (byte)(0xFF & (value >> 16)),
            (byte)(0xFF & (value >> 8)),
            (byte)(0xFF & value)
        };
        session.Set(key, bytes);
    }
        
    /* get & set string */
    public static string? GetString(this ISession session, string key)
    {
        var data = session.Get(key);
        if (data == null)
        {
            return null;
        }
        return Encoding.UTF8.GetString(data);
    }    
    
    public static void SetString(this ISession session, string key, string value)
    {
        session.Set(key, Encoding.UTF8.GetBytes(value));
    }                      
}

```

###### 2.4.5.3 session 实现？

##### 2.4.6 http authentication feature

```c#
// 接口
public interface IHttpAuthenticationFeature
{    
    ClaimsPrincipal? User { get; set; }
}

// 实现
public class HttpAuthenticationFeature : IHttpAuthenticationFeature
{    
    public ClaimsPrincipal? User { get; set; }
}

```

###### claimsPrincipal ???



#### 2.5 connection info

```c#
public abstract class ConnectionInfo
{
    // tracking id
    public abstract string Id { get; set; }
    
	// remote        
    public abstract IPAddress? RemoteIpAddress { get; set; }        
    public abstract int RemotePort { get; set; }
    
    // local    
    public abstract IPAddress? LocalIpAddress { get; set; }        
    public abstract int LocalPort { get; set; }
    
    // ssl(x509)    
    public abstract X509Certificate2? ClientCertificate { get; set; }
        
    public abstract Task<X509Certificate2?> GetClientCertificateAsync(
        CancellationToken cancellationToken = new CancellationToken());
}

```

##### 2.5.1 default connection info

```c#
internal sealed class DefaultConnectionInfo : ConnectionInfo
{                
    private FeatureReferences<FeatureInterfaces> _features;
    struct FeatureInterfaces
    {
        public IHttpConnectionFeature? Connection;
        public ITlsConnectionFeature? TlsConnection;
    }
        
    public DefaultConnectionInfo(IFeatureCollection features)
    {
        Initialize(features);
    }
    
    // initialize
    public void Initialize( IFeatureCollection features)
    {
        _features.Initalize(features);
    }
    
    public void Initialize(IFeatureCollection features, int revision)
    {
        _features.Initalize(features, revision);
    }
    
    public void Uninitialize()
    {
        _features = default;
    }        
}

```

###### 2.5.1.1 features

```c#
internal sealed class DefaultConnectionInfo : ConnectionInfo
{
     /* http connection feature with default */
    private IHttpConnectionFeature HttpConnectionFeature => _features.Fetch(
        ref _features.Cache.Connection, 
        _newHttpConnectionFeature)!;
    
    private readonly static Func<IFeatureCollection, IHttpConnectionFeature> _newHttpConnectionFeature = f => 
        new HttpConnectionFeature();
    
     /* tls connection feature with default */    
    private ITlsConnectionFeature TlsConnectionFeature=> _features.Fetch(
        ref _features.Cache.TlsConnection, 
        _newTlsConnectionFeature)!;       
    
    private readonly static Func<IFeatureCollection, ITlsConnectionFeature> _newTlsConnectionFeature = f => 
        new TlsConnectionFeature();
}

```

###### 2.5.1.2 props

```c#
internal sealed class DefaultConnectionInfo : ConnectionInfo
{       
    // 从 http connection feature 解析 connection id
    public override string Id
    {
        get { return HttpConnectionFeature.ConnectionId; }
        set { HttpConnectionFeature.ConnectionId = value; }
    }
    
    // 从 http connection feature 解析 remote ip address
    public override IPAddress? RemoteIpAddress
    {
        get { return HttpConnectionFeature.RemoteIpAddress; }
        set { HttpConnectionFeature.RemoteIpAddress = value; }
    }
    
    // 从 http connection feature 解析 remote port
    public override int RemotePort
    {
        get { return HttpConnectionFeature.RemotePort; }
        set { HttpConnectionFeature.RemotePort = value; }
    }
    
    // 从 http connection feature 解析 local ip address
    public override IPAddress? LocalIpAddress
    {
        get { return HttpConnectionFeature.LocalIpAddress; }
        set { HttpConnectionFeature.LocalIpAddress = value; }
    }
    
    // 从 http connection feature 解析 local port
    public override int LocalPort
    {
        get { return HttpConnectionFeature.LocalPort; }
        set { HttpConnectionFeature.LocalPort = value; }
    }
                       
    // 从 tls connection feature 解析 client certificate
    public override X509Certificate2? ClientCertificate
    {
        get { return TlsConnectionFeature.ClientCertificate; }
        set { TlsConnectionFeature.ClientCertificate = value; }
    }
}

```

###### 2.5.1.3 方法

```c#
internal sealed class DefaultConnectionInfo : ConnectionInfo
{   
    public override Task<X509Certificate2?> GetClientCertificateAsync(CancellationToken cancellationToken = default)
    {
        return TlsConnectionFeature.GetClientCertificateAsync(cancellationToken);
    }        
}

```

##### 2.5.2 features in connection info

###### 2.5.2.1 http connection feature

```c#
// 接口
public interface IHttpConnectionFeature
{    
    string ConnectionId { get; set; }
        
    IPAddress? RemoteIpAddress { get; set; }
    int RemotePort { get; set; }
    
    IPAddress? LocalIpAddress { get; set; }                   
    int LocalPort { get; set; }
}

// 实现
public class HttpConnectionFeature : IHttpConnectionFeature
{    
    public string ConnectionId { get; set; } = default!;        

    public IPAddress? LocalIpAddress { get; set; }        
    public int LocalPort { get; set; }
        
    public IPAddress? RemoteIpAddress { get; set; }    
    public int RemotePort { get; set; }
}

```

###### 2.5.2.2 tls connection feature

```c#
// 接口
public interface ITlsConnectionFeature
{    
    X509Certificate2? ClientCertificate { get; set; }        
    Task<X509Certificate2?> GetClientCertificateAsync(CancellationToken cancellationToken);
}

// 实现
public class TlsConnectionFeature : ITlsConnectionFeature
{    
    public X509Certificate2? ClientCertificate { get; set; }
       
    public Task<X509Certificate2?> GetClientCertificateAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(ClientCertificate);
    }
}

```

###### 2.5.2.3 tls token binding feature

```c#
public interface ITlsTokenBindingFeature
{    
    byte[] GetProvidedTokenBindingId();            
    byte[] GetReferredTokenBindingId();
}

```



#### 2.6 web socket manager

```c#
public abstract class WebSocketManager
{    
    public abstract bool IsWebSocketRequest { get; }        
    public abstract IList<string> WebSocketRequestedProtocols { get; }
        
    public virtual Task<WebSocket> AcceptWebSocketAsync()
    {
        return AcceptWebSocketAsync(subProtocol: null);
    }
        
    public abstract Task<WebSocket> AcceptWebSocketAsync(string? subProtocol);
}

```

##### 2.6.1 default web socket manager

```c#
internal sealed class DefaultWebSocketManager : WebSocketManager
{            
    private FeatureReferences<FeatureInterfaces> _features;
    struct FeatureInterfaces
    {
        public IHttpRequestFeature? Request;
        public IHttpWebSocketFeature? WebSockets;
    }
                
    public DefaultWebSocketManager(IFeatureCollection features)
    {
        Initialize(features);
    }
    
    // initialize
    public void Initialize(IFeatureCollection features)
    {
        _features.Initalize(features);
    }
    
    public void Initialize(IFeatureCollection features, int revision)
    {
        _features.Initalize(features, revision);
    }
    
    public void Uninitialize()
    {
        _features = default;
    }                        
}

```

###### 2.6.1.1 features

```c#
internal sealed class DefaultWebSocketManager : WebSocketManager
{
    /* http web socket feature with default (null) */
    private IHttpWebSocketFeature WebSocketFeature => _features.Fetch(
        ref _features.Cache.WebSockets, 
        _nullWebSocketFeature)!;
                
    private readonly static Func<IFeatureCollection, IHttpWebSocketFeature?> _nullWebSocketFeature = f => null;
    
    /* http request feature with default (null) */
    private IHttpRequestFeature HttpRequestFeature => _features.Fetch(
        ref _features.Cache.Request, 
        _nullRequestFeature)!;
    
    private readonly static Func<IFeatureCollection, IHttpRequestFeature?> _nullRequestFeature = f => null;
}

```

###### 2.6.1.2 props

```c#
internal sealed class DefaultWebSocketManager : WebSocketManager
{        
    // 从 web socket feature 解析 is web socket request
    public override bool IsWebSocketRequest
    {
        get
        {
            return WebSocketFeature != null && WebSocketFeature.IsWebSocketRequest;
        }
    }
                
    // 从 http request feature 解析 web socket request protocols
    public override IList<string> WebSocketRequestedProtocols
    {
        get
        {
            return HttpRequestFeature.Headers.GetCommaSeparatedValues(Names.WebSocketSubProtocols);
        }
    }
}

```

###### 2.6.1.3 方法

```c#
internal sealed class DefaultWebSocketManager : WebSocketManager
{
    public override Task<WebSocket> AcceptWebSocketAsync(string? subProtocol)
    {
        if (WebSocketFeature == null)
        {
            throw new NotSupportedException("WebSockets are not supported");
        }
        
        return WebSocketFeature.AcceptAsync(
            new WebSocketAcceptContext() 
            {                     
                SubProtocol = subProtocol 
            });
    }        
}

public class WebSocketAcceptContext
{    
    public virtual string? SubProtocol { get; set; }
}

```

##### 2.6.2 feature in web socket manager

###### 2.6.2.1 http web socket feature？

```c#
// 接口
public interface IHttpWebSocketFeature
{    
    bool IsWebSocketRequest { get; }        
    Task<WebSocket> AcceptAsync(WebSocketAcceptContext context);
}

// 实现？？
```

#### 2.7 other features

##### 2.7.1 http x

```c#
public interface IHttpUpgradeFeature
{    
    bool IsUpgradableRequest { get; }
        
    Task<Stream> UpgradeAsync();
}

public interface IHttpsCompressionFeature
{    
    HttpsCompressionMode Mode { get; set; }
}

public interface IHttpBodyControlFeature
{    
    bool AllowSynchronousIO { get; set; }
}

public interface IHttpResetFeature
{    
    void Reset(int errorCode);
}

public interface IHttpRequestBodyDetectionFeature
{    
    bool CanHaveBody { get; }
}

```

### 3. http pipeline

#### 3.1 http application

```c#
public interface IHttpApplication<TContext> where TContext : notnull
{        
    TContext CreateContext(IFeatureCollection contextFeatures);        
    Task ProcessRequestAsync(TContext context);        
    void DisposeContext(TContext context, Exception? exception);
}

```

##### 3.1.1 hosting application

```c#
internal class HostingApplication : IHttpApplication<HostingApplication.Context>
{
    private readonly RequestDelegate _application;
    private readonly IHttpContextFactory? _httpContextFactory;
    private readonly DefaultHttpContextFactory? _defaultHttpContextFactory;    
    private HostingApplicationDiagnostics _diagnostics;
    
    // 注入服务，
    // - http context factory，用于创建 http context
    // - request delegate，处理请求的最终委托
    public HostingApplication(
        RequestDelegate application,
        ILogger logger,
        DiagnosticListener diagnosticSource,
        IHttpContextFactory httpContextFactory)
    {
        // 注入 request delegate
        _application = application;
        
        _diagnostics = new HostingApplicationDiagnostics(logger, diagnosticSource);
        
        // 注入 http context factory
        if (httpContextFactory is DefaultHttpContextFactory factory)
        {
            // 是 default factory
            _defaultHttpContextFactory = factory;
        }
        else
        {
            // 不是 default factory
            _httpContextFactory = httpContextFactory;
        }
    }

    // 创建 host context（http context 的封装）
    public Context CreateContext(IFeatureCollection contextFeatures)
    {        
        /* 解析 context */
        Context? hostContext;
        
        // 如果 context feature 是 host context container
        // （host context container 类似 cache）
        if (contextFeatures is IHostContextContainer<Context> container)
        {
            // 解析 context（没有则创建并 cache）
            hostContext = container.HostContext;
            if (hostContext is null)
            {
                hostContext = new Context();
                container.HostContext = hostContext;
            }
        }
        // 否则，即 context feature 不是 host context container
        else
        {
            // 创建 context
            hostContext = new Context();
        }
               
        /* 解析 http context */
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
    
    // 使用 application（request delegate，请求管道）处理 context    
    public Task ProcessRequestAsync(Context context)
    {
        return _application(context.HttpContext!);
    }
    
    // 销毁 host context
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
    
    // (host) context
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

###### 3.1.1.2 http context container?

```c#
public interface IHostContextContainer<TContext> where TContext : notnull
{    
    TContext? HostContext { get; set; }
}

```

#### 3.2 http context

```c#
public abstract class HttpContext
{    
    public abstract IServiceProvider RequestServices { get; set; }    
    public abstract IFeatureCollection Features { get; }   
    public abstract IDictionary<object, object?> Items { get; set; }        
    
    public abstract ConnectionInfo Connection { get; }  
    public abstract ClaimsPrincipal User { get; set; }       
    public abstract WebSocketManager WebSockets { get; }    
    public abstract ISession Session { get; set; }
    
    public abstract string TraceIdentifier { get; set; } 
    public abstract CancellationToken RequestAborted { get; set; }           
    public abstract HttpRequest Request { get; }        
    public abstract HttpResponse Response { get; }       
    
                                       
    // 在派生类实现
    public abstract void Abort();
}

```

##### 3.2.1 default http context

```c#
public sealed class DefaultHttpContext : HttpContext
{    
    [EditorBrowsable(EditorBrowsableState.Never)]
    public HttpContext HttpContext => this;    
    
    /* only for this default implement */    
    public FormOptions FormOptions { get; set; } = default!;        
    public IServiceScopeFactory ServiceScopeFactory { get; set; } = default!;     
        
    public DefaultHttpContext(IFeatureCollection features)
    {        
        _features.Initalize(features);
        
        // 创建 default request、default response
        _request = new DefaultHttpRequest(this);
        _response = new DefaultHttpResponse(this);
    }
    
    public DefaultHttpContext() : this(new FeatureCollection())
    {
        // 注入 request feature
        Features.Set<IHttpRequestFeature>(new HttpRequestFeature());   
        // 注入 response feature
        Features.Set<IHttpResponseFeature>(new HttpResponseFeature());
        // 注入 resonse body feature
        Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(Stream.Null));
    }                
    
    // initialize   
    public void Initialize(IFeatureCollection features)
    {
        var revision = features.Revision;
        _features.Initalize(features, revision);
        _request.Initialize(revision);
        _response.Initialize(revision);
        _connection?.Initialize(features, revision);
        _websockets?.Initialize(features, revision);
    }
    
    public void Uninitialize()
    {
        _features = default;
        _request.Uninitialize();
        _response.Uninitialize();
        _connection?.Uninitialize();
        _websockets?.Uninitialize();
    }                                                  
       
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
    
    public override void Abort()
    {
        LifetimeFeature.Abort();
    }                        
}

```

###### 3.2.1.1 features

```c#
public sealed class DefaultHttpContext : HttpContext
{   
    private FeatureReferences<FeatureInterfaces> _features;     
    struct FeatureInterfaces
    {
        public IItemsFeature? Items;
        public IServiceProvidersFeature? ServiceProviders;
        public IHttpAuthenticationFeature? Authentication;
        public IHttpRequestLifetimeFeature? Lifetime;
        public ISessionFeature? Session;
        public IHttpRequestIdentifierFeature? RequestIdentifier;
    }     
                        
    /* 解析 http authentication feature */
    private IHttpAuthenticationFeature HttpAuthenticationFeature => _features.Fetch(
        ref _features.Cache.Authentication, 
        _newHttpAuthenticationFeature)!;
   
    private readonly static Func<IFeatureCollection, IHttpAuthenticationFeature> _newHttpAuthenticationFeature = f => 
        new HttpAuthenticationFeature();
    
    /* 解析 items feature */
    private IItemsFeature ItemsFeature => _features.Fetch(
        ref _features.Cache.Items, 
        _newItemsFeature)!;
    
    private readonly static Func<IFeatureCollection, IItemsFeature> _newItemsFeature = f => new ItemsFeature();
    
     /* 解析 service provider feature */
    private IServiceProvidersFeature ServiceProvidersFeature =>_features.Fetch(
        ref _features.Cache.ServiceProviders,        
        _newServiceProvidersFeature)!;
   
    private readonly static Func<DefaultHttpContext, IServiceProvidersFeature> _newServiceProvidersFeature = context => 
        new RequestServicesFeature(context, context.ServiceScopeFactory);
    
     /* 解析 request lifetime feature */
    private IHttpRequestLifetimeFeature LifetimeFeature => _features.Fetch(
        ref _features.Cache.Lifetime, 
        _newHttpRequestLifetimeFeature)!;
    
    private readonly static Func<IFeatureCollection, IHttpRequestLifetimeFeature> _newHttpRequestLifetimeFeature = f => 
        new HttpRequestLifetimeFeature();
    
    /* 解析 request identifier feature */
    private IHttpRequestIdentifierFeature RequestIdentifierFeature => _features.Fetch(
        ref _features.Cache.RequestIdentifier, 
        _newHttpRequestIdentifierFeature)!;
    
    private readonly static Func<IFeatureCollection, IHttpRequestIdentifierFeature> _newHttpRequestIdentifierFeature = f => 
        new HttpRequestIdentifierFeature();
    
     /* 解析 session feature null */
    private ISessionFeature? SessionFeatureOrNull => _features.Fetch(
        ref _features.Cache.Session, 
        _nullSessionFeature);
    
    private readonly static Func<IFeatureCollection, ISessionFeature?> _nullSessionFeature = f => null;    
    
    /*  解析 session feature */
    private ISessionFeature SessionFeature => _features.Fetch(
        ref _features.Cache.Session, 
        _newSessionFeature)!;    	                        
    
    private readonly static Func<IFeatureCollection, ISessionFeature> _newSessionFeature = f => 
        new DefaultSessionFeature();                
}

```

###### 3.2.1.2 props

```c#
public sealed class DefaultHttpContext : HttpContext
{            
    // features                   
    public override IFeatureCollection Features => _features.Collection ?? ContextDisposed();     
    
    // http request   
    private readonly DefaultHttpRequest _request;
    public override HttpRequest Request => _request;     
    
    // http response  
    private readonly DefaultHttpResponse _response;    
    public override HttpResponse Response => _response;   
    
    // connection info 
    private DefaultConnectionInfo? _connection;
    public override ConnectionInfo Connection =>  _connection ?? (_connection = new DefaultConnectionInfo(Features));     
    
    // web socket manager  
    private DefaultWebSocketManager? _websockets;
    public override WebSocketManager WebSockets => _websockets ?? (_websockets = new DefaultWebSocketManager(Features)); 
        
    // claims principal user       
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
    
    // dictionary items
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
    
     // service provider
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
    
	// cancellation token
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
           
    // trace id 
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
    
    // session
    public override ISession Session
    {
        get
        {            
            var feature = SessionFeatureOrNull;
            
            if (feature == null)
            {
                throw new InvalidOperationException("Session has not been configured for this application or request.");
            }
            
            return feature.Session;
        }
        set
        {           
            SessionFeature.Session = value;
        }
    }      
}

```

###### 3.2.1.3 扩展方法 - get server variable

```c#
public static class HttpContextServerVariableExtensions
{    
    public static string? GetServerVariable(
        this HttpContext context, 
        string variableName)
    {
        var feature = context.Features.Get<IServerVariablesFeature>();
        
        if (feature == null)
        {
            return null;
        }
        
        return feature[variableName];
    }
}

```

##### 3.2.2 http context accessor

```c#
public interface IHttpContextAccessor
{        
    HttpContext? HttpContext { get; set; }
}

```

###### 3.2.2.1 http context accessor（实现）

```c#
public class HttpContextAccessor : IHttpContextAccessor
{
    private static AsyncLocal<HttpContextHolder> _httpContextCurrent = new AsyncLocal<HttpContextHolder>();
       
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

###### 3.2.2.2 扩展方法 - add http context accessor

```c#
public static class HttpServiceCollectionExtensions
{    
    public static IServiceCollection AddHttpContextAccessor(this IServiceCollection services)
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

##### 3.2.3 http context factory

```c#
public interface IHttpContextFactory
{    
    HttpContext Create(IFeatureCollection featureCollection);        
    void Dispose(HttpContext httpContext);
}

```

###### 3.2.3.1 default http context factory

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
    
    // initial
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
    
    // dispose
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
    
    // 创建 http context
    public HttpContext Create(IFeatureCollection featureCollection)
    {
        if (featureCollection is null)
        {
            throw new ArgumentNullException(nameof(featureCollection));
        }
        
        // 构建 default http context
        var httpContext = new DefaultHttpContext(featureCollection);
        // 初始化 default http context，注入 form options 和 service scope factory
        Initialize(httpContext);
        
        return httpContext;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private DefaultHttpContext Initialize(DefaultHttpContext httpContext)
    {
        if (_httpContextAccessor != null)
        {
            _httpContextAccessor.HttpContext = httpContext;
        }
        
        // 注入 form options
        httpContext.FormOptions = _formOptions;
        // 注入 service scope factory（http context 中的 service provider 是 scoped）
        httpContext.ServiceScopeFactory = _serviceScopeFactory;
        
        return httpContext;
    }
}

```

#### 3.3 application builder

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

##### 3.3.1 request delegate

```c#
public delegate Task RequestDelegate(HttpContext context);

```

##### 3.3.2 application builder（实现）

```c#
public class ApplicationBuilder : IApplicationBuilder
{
    private const string ServerFeaturesKey = "server.Features";
    private const string ApplicationServicesKey = "application.Services";   
    
    // middleware 容器
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
            // 注入 service provider -> ["application.services", value]
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
                            
    
    /* 构造 */
    public ApplicationBuilder(IServiceProvider serviceProvider, object server) : this(serviceProvider)
    {            
        // 注入 server features，-> ["server.Features", server]
        SetProperty(ServerFeaturesKey, server);
    }   
    
    public ApplicationBuilder(IServiceProvider serviceProvider)
    {
        Properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        ApplicationServices = serviceProvider;
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
            
    /* 实现接口的 build 方法 */        
}

```

###### 3.3.2.1 接口方法 - use

```c#
public class ApplicationBuilder : IApplicationBuilder
{    
    public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware)
    {
        _components.Add(middleware);
        return this;
    }        
}

```

###### 3.3.2.2 接口方法 - new

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

###### 3.3.2.3 接口方法 - build

```c#
public class ApplicationBuilder : IApplicationBuilder
{        
    public RequestDelegate Build()
    {
        // 起始的 request delegate（default），从 app 开始反向应用 middlewares
        // http context -> task
        RequestDelegate app = context =>
        {
            // If we reach the end of the pipeline, but we have an endpoint, then something unexpected has happened.
            // This could happen if user code sets an endpoint, but they forgot to add the UseEndpoint middleware.
            var endpoint = context.GetEndpoint();
            var endpointRequestDelegate = endpoint?.RequestDelegate;
            if (endpointRequestDelegate != null)
            {
                var message = 
                    $"The request reached the end of the pipeline without executing the endpoint: '{endpoint!.DisplayName}'." +
                    $"Please register the EndpointMiddleware using '{nameof(IApplicationBuilder)}.UseEndpoints(...)' 
                    $"if using routing.";
                
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

##### 3.3.3 middleware

###### 3.3.3.1 middleware

```c#
// 接口
public interface IMiddleware
{    
    Task InvokeAsync(HttpContext context, RequestDelegate next);
}

```

###### 3.3.3.2 middleware factory

```c#
// 接口
public interface IMiddlewareFactory
{    
    IMiddleware? Create(Type middlewareType);    
    void Release(IMiddleware middleware);    
}

// 实现
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

##### 3.3.4 扩展方法 - use

###### 3.3.4.1 by func

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

###### 3.3.4.2 by middleware t 

```c#
public static class UseMiddlewareExtensions
{    
    public static IApplicationBuilder UseMiddleware<[DynamicallyAccessedMembers(MiddlewareAccessibility)]TMiddleware>(
        this IApplicationBuilder app, 
        params object?[] args)
    {
        return app.UseMiddleware(typeof(TMiddleware), args);
    }
    
    public static IApplicationBuilder UseMiddleware(
        this IApplicationBuilder app, 
        [DynamicallyAccessedMembers(MiddlewareAccessibility)] Type middleware, 
        params object?[] args)
    {
    
}

```

###### 3.3.4.3 by middleware type

```c#
public static class UseMiddlewareExtensions
{        
    public static IApplicationBuilder UseMiddleware(
        this IApplicationBuilder app, 
        [DynamicallyAccessedMembers(MiddlewareAccessibility)] Type middleware, 
        params object?[] args)
    {
        // a- 强类型，使用 IMiddleware 的方法;
        if (typeof(IMiddleware).IsAssignableFrom(middleware))
        {
            // IMiddleware 不支持参数，如果带有参数，抛出异常
            // IMiddleware doesn't support passing args directly since it's activated from the container
            if (args.Length > 0)
            {
                throw new NotSupportedException(
                    Resources.FormatException_UseMiddlewareExplicitArgumentsNotSupported(typeof(IMiddleware)));
            }
            
            return UseMiddlewareInterface(app, middleware);
        }
        
        // b- 弱类型；使用 compile
    }    
}

```

###### a- 强类型

```c#
public static class UseMiddlewareExtensions
{        
    private static IApplicationBuilder UseMiddlewareInterface(
        IApplicationBuilder app, 
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type middlewareType)
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
                var middlewareFactory = (IMiddlewareFactory?)context.RequestServices
                    											.GetService(typeof(IMiddlewareFactory));
                // 如果没有，-> 抛出异常
                if (middlewareFactory == null)
                {                    
                    throw new InvalidOperationException(
                        Resources.FormatException_UseMiddlewareNoMiddlewareFactory(typeof(IMiddlewareFactory)));
                }
                
                // 由 middleware factory 创建 middleware type 的实例，                
                var middleware = middlewareFactory.Create(middlewareType);
                // // 如果创建失败，-> 抛出异常
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

###### b- 弱类型

```c#
public static class UseMiddlewareExtensions
{
    internal const string InvokeMethodName = "Invoke";
    internal const string InvokeAsyncMethodName = "InvokeAsync";
    
    private static readonly MethodInfo GetServiceInfo = typeof(UseMiddlewareExtensions).GetMethod(
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
                Resources.FormatException_InvokeMiddlewareNoService(type, middleware));
        }
        
        return service;
    }
    
    // We're going to keep all public constructors and public methods on middleware
    private const DynamicallyAccessedMemberTypes MiddlewareAccessibility = 
        DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods;
        
    public static IApplicationBuilder UseMiddleware(
        this IApplicationBuilder app, 
        [DynamicallyAccessedMembers(MiddlewareAccessibility)] Type middleware, 
        params object?[] args)
    {
        // a- 强类型
        
        // （没有实现 IMiddleware 接口，即弱类型）
        // 解析 service provider
        var applicationServices = app.ApplicationServices;
        
        /* 创建 func(request delegate, request delegate) 委托，使用 app builder 原始 use 方法注入 */
        return app.Use(next =>
        {
            // 通过反射获取 middleware 中的 invoke 方法，
            //  - 名字是 invoke 或者 invokeAsync
            //  - 有且仅有1个方法
            //  - 返回值必须是 task 
            
            // 反射公共方法
            var methods = middleware.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            
            // 过滤 invoke 或 invokeAsync 方法
            var invokeMethods = methods.Where(m =>
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
                return (RequestDelegate)methodInfo.CreateDelegate(typeof(RequestDelegate), instance);
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
                        Resources.FormatException_UseMiddlewareIServiceProviderNotAvailable(nameof(IServiceProvider)));
                }
                                
                return factory(instance, context, serviceProvider);
            };
        });
        
    }
    
    // compile
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
                    Resources.FormatException_InvokeDoesNotSupportRefOrOutParams(InvokeMethodName));
            }
            
            var parameterTypeExpression = new Expression[]
            {
                providerArg,
                Expression.Constant(parameterType, typeof(Type)),
                Expression.Constant(methodInfo.DeclaringType, typeof(Type))
            };
            
            var getServiceCall = Expression.Call(GetServiceInfo, parameterTypeExpression);
            methodArguments[i] = Expression.Convert(getServiceCall, parameterType);
        }
        
        Expression middlewareInstanceArg = instanceArg;
        if (methodInfo.DeclaringType != null && 
            methodInfo.DeclaringType != typeof(T))
        {
            middlewareInstanceArg = Expression.Convert(middlewareInstanceArg, methodInfo.DeclaringType);
        }
        
        var body = Expression.Call(
            middlewareInstanceArg, 
            methodInfo, 
            methodArguments);
        
        var lambda = Expression.Lambda<Func<T, HttpContext, IServiceProvider, Task>>(
            body, 
            instanceArg, 
            httpContextArg, 
            providerArg);
        
        return lambda.Compile();
    }
}

```

##### 3.3.5 扩展方法 - use when

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
        // we would end up running our branch after all the components that were subsequently added to the main builder.
        var branchBuilder = app.New();
        configuration(branchBuilder);
        
        return app.Use(main =>
        {
            // This is called only when the main application builder is built, not per request.
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

##### 3.3.6 扩展方法 - use base path

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

###### 3.3.6.1 base path middleware

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
        if (context.Request.Path.StartsWithSegments(
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

##### 3.3.7 扩展方法 - map

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
        
        return app.Use(next => new MapMiddleware(next, options).Invoke);
    }
}

```

###### 3.3.7.1 map options

```c#
public class MapOptions
{    
    public PathString PathMatch { get; set; }        
    public RequestDelegate? Branch { get; set; }       
    public bool PreserveMatchedPathSegment { get; set; }
}

```

###### 3.3.7.2 map middleware

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
        if (context.Request.Path.StartsWithSegments(
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

##### 3.3.8 扩展方法 - map when

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

###### 3.3.8.1 map when options

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

###### 3.3.8.2 map when middleware

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

##### 3.3.9 扩展方法 - run

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

##### 3.3.10 application builder factory

```c#
public interface IApplicationBuilderFactory
{        
    IApplicationBuilder CreateBuilder(IFeatureCollection serverFeatures);
}

```

###### 3.3.10.1 application builder factory

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















