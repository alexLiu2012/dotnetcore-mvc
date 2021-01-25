## about http context



### 1. about

#### 1.1 overview

* asp.net core 对 http 协议的封装
* 对 http 协议中的具体细节建模

#### 1.2 how designed

##### 1.2.1 http context

* 封装的 http 协议上下文，即对传入、传出 server 的 http 数据的封装

* 包括如下组件

  * feature collection

    用于修改 http context 内容的功能接口

  * http request

    请求

  * http response

    响应

  * connection info

    连接信息

  * web socket manager

    web socket 管理

  * claims principal

    凭证等

  * session

##### 1.2.2 feature

* 针对 http 具体内容修改的功能接口

###### 1.2.2.1 feature collection

* 存储 feature 的容器

###### 1.2.2.2 feature reference

* 封装 feature 的结构体

###### 1.2.2.3 feature references

* 管理、缓存 feature

##### 1.2.3 创建 http context

###### 1.2.3.1 http context factory

* 创建 http context 的工厂方法

###### 1.2.3.2 http context accessor

* http context 异步线程存储、解析

### 2. details

#### 2.1 http context

* 对 http 协议的建模

##### 2.1.1 抽象基类

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

##### 2.1.2 默认实现

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
    private readonly DefaultHttpRequest _request;
    private readonly DefaultHttpResponse _response;    
    private DefaultConnectionInfo? _connection;
    private DefaultWebSocketManager? _websockets;
                       
    /* 构造函数 */
    
    // 构建 request、response，初始化 feature collection
    public DefaultHttpContext(IFeatureCollection features)
    {
        _features.Initalize(features);
        _request = new DefaultHttpRequest(this);
        _response = new DefaultHttpResponse(this);
    }
    // 然后，在 feature collection 中注册
    //   request feature,
    //   response feature,
    //   response body feautre
    public DefaultHttpContext() : this(new FeatureCollection())
    {
        Features.Set<IHttpRequestFeature>(
            new HttpRequestFeature());
        Features.Set<IHttpResponseFeature>(
            new HttpResponseFeature());
        Features.Set<IHttpResponseBodyFeature>(
            new StreamResponseBodyFeature(Stream.Null));
    }
            
    /* 方法 */    
    
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
       
    public override void Abort()
    {
        LifetimeFeature.Abort();
    }                        
}

```

###### 2.1.2.1 公共属性

```c#
public sealed class DefaultHttpContext : HttpContext
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public HttpContext HttpContext => this;
    
    // basic 
    
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
    
    public override HttpRequest Request => _request;        
    
    public override HttpResponse Response => _response;        
    
    public override ConnectionInfo Connection => 
        _connection ?? (_connection = new DefaultConnectionInfo(Features));        
    
    public override WebSocketManager WebSockets => 
        _websockets ?? (_websockets = new DefaultWebSocketManager(Features));
    
    // advanced
    
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
        
    public override ISession Session
    {
        get
        {
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
            SessionFeature.Session = value;
        }
    }    
    
    // more
    
    public FormOptions FormOptions { get; set; } = default!;        
    public IServiceScopeFactory ServiceScopeFactory { get; set; } = default!;
}

```

###### 2.1.2.2 features

* 从 feature collection 中获取，
* 如果没有，创建 default（_newXxxFeature）

```c#
public sealed class DefaultHttpContext : HttpContext
{
    // private property
    
    private IItemsFeature ItemsFeature =>
        _features.Fetch(
        	ref _features.Cache.Items, 
        	_newItemsFeature)!;
    
    private IServiceProvidersFeature ServiceProvidersFeature =>
        _features.Fetch(
        	ref _features.Cache.ServiceProviders, 
        	this, 
        	_newServiceProvidersFeature)!;
    
    private IHttpAuthenticationFeature HttpAuthenticationFeature =>
        _features.Fetch(
        	ref _features.Cache.Authentication, 
        	_newHttpAuthenticationFeature)!;
    
    private IHttpRequestLifetimeFeature LifetimeFeature =>
        _features.Fetch(
        	ref _features.Cache.Lifetime, 
        	_newHttpRequestLifetimeFeature)!;

    private ISessionFeature SessionFeature =>        
        _features.Fetch(
        	ref _features.Cache.Session, 
        	_newSessionFeature)!;

    private ISessionFeature? SessionFeatureOrNull =>
        _features.Fetch(
        	ref _features.Cache.Session, 
        	_nullSessionFeature);

    private IHttpRequestIdentifierFeature RequestIdentifierFeature =>
        _features.Fetch(
        	ref _features.Cache.RequestIdentifier, 
        	_newHttpRequestIdentifierFeature)!;
    
    // default
    
    private readonly static Func<IFeatureCollection, IItemsFeature> 
        _newItemsFeature = f => 
        	new ItemsFeature();
    
    private readonly static Func<DefaultHttpContext, IServiceProvidersFeature> 
        _newServiceProvidersFeature = context => 
        	new RequestServicesFeature(
        		context, 
        		context.ServiceScopeFactory);
    
    private readonly static Func<IFeatureCollection, IHttpAuthenticationFeature> 
        _newHttpAuthenticationFeature = f => 
        	new HttpAuthenticationFeature();
    
    private readonly static Func<IFeatureCollection, IHttpRequestLifetimeFeature> 
        _newHttpRequestLifetimeFeature = f => 
        	new HttpRequestLifetimeFeature();
    
    private readonly static Func<IFeatureCollection, ISessionFeature> 
        _newSessionFeature = f => 
        	new DefaultSessionFeature();
    
    private readonly static Func<IFeatureCollection, ISessionFeature?> 
        _nullSessionFeature = f => null;
    
    private readonly static Func<IFeatureCollection, IHttpRequestIdentifierFeature> 
        _newHttpRequestIdentifierFeature = f => 
        	new HttpRequestIdentifierFeature();
}

```

##### 2.1.3 扩展方法

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

#### 2.2 headers

##### 2.2.1 header dictionary

###### 2.2.1.1 接口

```c#
public interface IHeaderDictionary : IDictionary<string, StringValues>
{    
    new StringValues this[string key] { get; set; }        
    long? ContentLength { get; set; }
}

```

###### 2.2.1.2 实现

```c#
public class HeaderDictionary : IHeaderDictionary
{
    // empty keys & values
    private static readonly string[] EmptyKeys = Array.Empty<string>();
    private static readonly StringValues[] EmptyValues = Array.Empty<StringValues>();
    
    // empty enumerator
    private static readonly Enumerator EmptyEnumerator = new Enumerator();    
    private static readonly IEnumerator<KeyValuePair<string, StringValues>> 
        EmptyIEnumeratorType = EmptyEnumerator;
    private static readonly IEnumerator EmptyIEnumerator = EmptyEnumerator;
    
    // container
    private Dictionary<string, StringValues>? Store { get; set; }
    
	/* 属性 */            
    
    /* 构造函数 */
    
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
    
    /* 方法 crud */        
    
    /* 迭代器 */        
}

```

###### 2.2.1.3 属性

```c#
public class HeaderDictionary : IHeaderDictionary
{
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
                HeaderUtilities.TryParseNonNegativeInt64(
                    new StringSegment(rawValue[0]).Trim(), 
                    out value))
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
                this[HeaderNames.ContentLength] = 
                    HeaderUtilities.FormatNonNegativeInt64(
                    	value.GetValueOrDefault());
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
}

```

###### 2.2.1.4 方法

```c#
public class HeaderDictionary : IHeaderDictionary
{
    [MemberNotNull(nameof(Store))]
    private void EnsureStore(int capacity)
    {
        if (Store == null)
        {
            Store = new Dictionary<string, StringValues>(
                capacity, 
                StringComparer.OrdinalIgnoreCase);
        }
    }       
    
    private void ThrowIfReadOnly()
    {
        if (IsReadOnly)
        {
            throw new InvalidOperationException(
                "The response headers cannot be modified 
                "because the response has already started.");
        }
    }
    
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

###### 2.2.1.5 迭代器

```c#
public class HeaderDictionary : IHeaderDictionary
{
    public Enumerator GetEnumerator()
    {
        if (Store == null || Store.Count == 0)
        {
            // Non-boxed Enumerator
            return EmptyEnumerator;
        }
        return new Enumerator(Store.GetEnumerator());
    }
        
    IEnumerator<KeyValuePair<string, StringValues>> 
        IEnumerable<KeyValuePair<string, StringValues>>.GetEnumerator()
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
}

```

##### 2.2.2 headers 扩展方法

* known parser
* known list parser

```c#
public static class HeaderDictionaryTypeExtensions
{
    private static IDictionary<Type, object> KnownParsers = new Dictionary<Type, object>()
    {
        { 
            typeof(CacheControlHeaderValue), 
            new Func<string, CacheControlHeaderValue?>(value => 
            	{ 
                    return CacheControlHeaderValue
                        .TryParse(value, out var result) ? result : null; 
                }) 
        },
        { 
            typeof(ContentDispositionHeaderValue), 
           	new Func<string, ContentDispositionHeaderValue?>(value => 
            	{ 
                    return ContentDispositionHeaderValue
                        .TryParse(value, out var result) ? result : null; 
                }) 
        },
        {
            typeof(ContentRangeHeaderValue), 
            new Func<string, ContentRangeHeaderValue?>(value => 
            	{ 
                    return ContentRangeHeaderValue
                        .TryParse(value, out var result) ? result : null; 
                }) 
        },
        { 
            typeof(MediaTypeHeaderValue), 
            new Func<string, MediaTypeHeaderValue?>(value => 
            	{ 
                    return MediaTypeHeaderValue
                        .TryParse(value, out var result) ? result : null; 
                }) 
        },
        { 
            typeof(RangeConditionHeaderValue), 
            new Func<string, RangeConditionHeaderValue?>(value => 
            	{ 
                    return RangeConditionHeaderValue
                        .TryParse(value, out var result) ? result : null; 
                }) 
        },
        { 
            typeof(RangeHeaderValue), 
            new Func<string, RangeHeaderValue?>(value => 
            	{ 
                    return RangeHeaderValue
                        .TryParse(value, out var result) ? result : null; 
                }) 
        },
        { 
            typeof(EntityTagHeaderValue), 
            new Func<string, EntityTagHeaderValue?>(value => 
            	{ 
                    return EntityTagHeaderValue
                        .TryParse(value, out var result) ? result : null; 
                }) 
        },
        { 
            typeof(DateTimeOffset?), 
            new Func<string, DateTimeOffset?>(value => 
            	{ 
                    return HeaderUtilities
                        .TryParseDate(value, out var result) ? result : null; 
                }) 
        },
        { 
            typeof(long?), 
            new Func<string, long?>(value => 
            	{ 
                    return HeaderUtilities
                        .TryParseNonNegativeInt64(value, out var result) ? result : null; 
                }) 
        },
    };
    
    private static IDictionary<Type, object> KnownListParsers = new Dictionary<Type, object>()
    {
        { 
            typeof(MediaTypeHeaderValue), 
            new Func<IList<string>, IList<MediaTypeHeaderValue>>(value => 
            	{ 
                    return MediaTypeHeaderValue
                        .TryParseList(value, out var result) 
                        	? result 
                        	: Array.Empty<MediaTypeHeaderValue>(); 
                })  
        },
        { 
            typeof(StringWithQualityHeaderValue), 
            new Func<IList<string>, IList<StringWithQualityHeaderValue>>(value => 
            	{ 
                    return StringWithQualityHeaderValue
                        .TryParseList(value, out var result) 
                        	? result 
                        	: Array.Empty<StringWithQualityHeaderValue>(); 
                })  
        },
        { 
            typeof(CookieHeaderValue), 
            new Func<IList<string>, IList<CookieHeaderValue>>(value => 
                { 
                    return CookieHeaderValue
                        .TryParseList(value, out var result) 
                        	? result 
                        : Array.Empty<CookieHeaderValue>(); 
                })  
        },
        { 
            typeof(EntityTagHeaderValue), 
            new Func<IList<string>, IList<EntityTagHeaderValue>>(value => 
                { 
                    return EntityTagHeaderValue
                        .TryParseList(value, out var result) 
                        	? result 
                        	: Array.Empty<EntityTagHeaderValue>(); 
                })  
        },
        { 
            typeof(SetCookieHeaderValue), 
            new Func<IList<string>, IList<SetCookieHeaderValue>>(value => 
                { 
                    return SetCookieHeaderValue
                        .TryParseList(value, out var result) 
                        	? result 
                        	: Array.Empty<SetCookieHeaderValue>(); 
                })  
        },
    };        
}

```

###### 2.2.2.1 get via reflection

```c#
public static class HeaderDictionaryTypeExtensions
{
    // get T via reflection
    private static T? GetViaReflection<T>(string value)
    {
        // TODO: Cache the reflected type for later? Only if success?
        var type = typeof(T);
        var method = type
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(methodInfo =>
            	{
                    if (string.Equals(
                        	"TryParse", 
                        	methodInfo.Name, 
                        	StringComparison.Ordinal) && 
                        methodInfo
                        	.ReturnParameter
                        	.ParameterType
                        	.Equals(typeof(bool)))
                    {
                        var methodParams = methodInfo.GetParameters();
                        return methodParams.Length == 2 && 
                               methodParams[0]
                            	   .ParameterType
                            	   .Equals(typeof(string)) && 
                               methodParams[1].IsOut && 
                               methodParams[1]
                            	   .ParameterType
                            	   .Equals(type.MakeByRefType());
                    }
                    return false;
                });
        
        if (method == null)
        {
            throw new NotSupportedException(string.Format(
                CultureInfo.CurrentCulture,
                "The given type '{0}' does not have a TryParse method with the required signature 'public static bool TryParse(string, out {0}).",
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
    
    // get list<T> via reflection
    private static IList<T> GetListViaReflection<T>(StringValues values)
    {
        // TODO: Cache the reflected type for later? Only if success?
        var type = typeof(T);
        var method = type
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(methodInfo =>
                {
                    if (string.Equals(
                        	"TryParseList", 
                        	methodInfo.Name, 
                        	StringComparison.Ordinal) && 
                        methodInfo
                        	.ReturnParameter
                        	.ParameterType
                        	.Equals(typeof(Boolean)))
                    {
                        var methodParams = methodInfo.GetParameters();
                        return methodParams.Length == 2 && 
                               methodParams[0]
                            	   .ParameterType
                            	   .Equals(typeof(IList<string>)) && 
                               methodParams[1].IsOut && 
	                       	   methodParams[1]
                            	   .ParameterType
                            	   .Equals(typeof(IList<T>)
                                   .MakeByRefType());
                    }
                    return false;
                });
        
        if (method == null)
        {
            throw new NotSupportedException(string.Format(
                CultureInfo.CurrentCulture,
                "The given type '{0}' does not have a TryParseList method with the required signature 'public static bool TryParseList(IList<string>, out IList<{0}>).",
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

###### 2.2.2.2 get

```c#
public static class HeaderDictionaryTypeExtensions
{
    // get T
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
        
        return GetViaReflection<T>(value.ToString());
    }
    
    // get list<T>
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
        
        return GetListViaReflection<T>(values);
    }
}

```

###### 2.2.2.3 set and append

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

###### 2.2.2.4 get and set date

```c#
public static class HeaderDictionaryTypeExtensions
{
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

###### 2.2.2.5 more

```c#

    
    
   
   
```

```c#
public static class HeaderDictionaryExtensions
{    
    public static void Append(
        this IHeaderDictionary headers, 
        string key, 
        StringValues value)
    {
        ParsingHelpers
            .AppendHeaderUnmodified(headers, key, value);
    }
        
    public static void AppendCommaSeparatedValues(
        this IHeaderDictionary headers, 
        string key, 
        params string[] values)
    {
        ParsingHelpers
            .AppendHeaderJoined(headers, key, values);
    }
        
    public static string[] GetCommaSeparatedValues(
        this IHeaderDictionary headers, 
        string key)
    {
        return ParsingHelpers
            .GetHeaderSplit(headers, key).ToArray();
    }
        
    public static void SetCommaSeparatedValues(
        this IHeaderDictionary headers, 
        string key, 
        params string[] values)
    {
        ParsingHelpers
            .SetHeaderJoined(headers, key, values);
    }
}

```

##### 2.2.3 request headers

```c#
public class RequestHeaders
{
    // 构造，注入 headers dictionary
    
    public IHeaderDictionary Headers { get; }
    public RequestHeaders(IHeaderDictionary headers)
    {
        if (headers == null)
        {
            throw new ArgumentNullException(nameof(headers));
        }
        
        Headers = headers;
    }
                
    // 公共属性
    
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
            return Headers
                .GetList<StringWithQualityHeaderValue>(HeaderNames.AcceptCharset);
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
            return Headers
                .GetList<StringWithQualityHeaderValue>(HeaderNames.AcceptEncoding);
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
            return Headers
                .GetList<StringWithQualityHeaderValue>(HeaderNames.AcceptLanguage);
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
            return Headers
                .Get<CacheControlHeaderValue>(HeaderNames.CacheControl);
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
            return Headers
                .Get<ContentDispositionHeaderValue>(HeaderNames.ContentDisposition);
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
            return Headers
                .Get<ContentRangeHeaderValue>(HeaderNames.ContentRange);
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
            if (Uri.TryCreate(
                Headers[HeaderNames.Referer], 
                UriKind.RelativeOrAbsolute, 
                out var uri))
            {
                return uri;
            }
            return null;
        }
        set
        {
            Headers.Set(
                HeaderNames.Referer, 
                value == null ? null : UriHelper.Encode(value));
        }
    }           
}

```

###### 2.2.3.1 方法

```c#
public class RequestHeaders
{
    // get
    
    public T? Get<T>(string name)
    {
        return Headers.Get<T>(name);
    }
        
    public IList<T> GetList<T>(string name)
    {
        return Headers.GetList<T>(name);
    }
    
    // set 
    
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
        
    // append
    
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

###### 2.2.3.2 扩展方法

```c#
public static class HeaderDictionaryTypeExtensions
{    
    public static RequestHeaders GetTypedHeaders(this HttpRequest request)
    {
        return new RequestHeaders(request.Headers);
    }
}

```

##### 2.2.4 response headers

```c#
public class ResponseHeaders
{
    // 构造函数，注入 headers dictionary
    public IHeaderDictionary Headers { get; }
    public ResponseHeaders(IHeaderDictionary headers)
    {
        if (headers == null)
        {
            throw new ArgumentNullException(nameof(headers));
        }
        
        Headers = headers;
    }
    
    // 公共属性
    
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
            if (Uri.TryCreate(
                Headers[HeaderNames.Location], 
                UriKind.RelativeOrAbsolute, 
                out var uri))
            {
                return uri;
            }
            return null;
        }
        set
        {
            Headers.Set(
                HeaderNames.Location, 
                value == null ? null : UriHelper.Encode(value));
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

###### 2.2.4.1 方法

```c#
public class ResponseHeaders
{
    // get   
    public T? Get<T>(string name)
    {
        return Headers.Get<T>(name);
    }
        
    public IList<T> GetList<T>(string name)
    {
        return Headers.GetList<T>(name);
    }
      
    // set
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
      
    // append
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



###### 2.2.4.2 扩展方法

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

##### 2.2.1 抽象基类

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

##### 2.2.2 默认实现

```c#
internal sealed class DefaultHttpRequest : HttpRequest
{
    private const string Http = "http";
    private const string Https = "https";
        
    private readonly DefaultHttpContext _context;
    
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
    
    /* 构造函数 */
    
    public DefaultHttpRequest(DefaultHttpContext context)
    {
        _context = context;
        _features.Initalize(context.Features);
    }
    
    /* 方法 */
    
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
    
    public override Task<IFormCollection> ReadFormAsync(
        CancellationToken cancellationToken)
    {
        return FormFeature.ReadFormAsync(cancellationToken);
    }
}

```

###### 2.2.2.1 公共属性

```c#
internal sealed class DefaultHttpRequest : HttpRequest
{
    public override HttpContext HttpContext => _context;        
    
    public override string Method
    {
        get { return HttpRequestFeature.Method; }
        set { HttpRequestFeature.Method = value; }
    }
    public override string Scheme
    {
        get { return HttpRequestFeature.Scheme; }
        set { HttpRequestFeature.Scheme = value; }
    }    
    public override bool IsHttps
    {
        get { return string.Equals(
            	Https, 
            	Scheme, 
            	StringComparison.OrdinalIgnoreCase); }
        set { Scheme = value ? Https : Http; }
    }
    
    public override HostString Host
    {
        get { return HostString.FromUriComponent(Headers[HeaderNames.Host]); }
        set { Headers[HeaderNames.Host] = value.ToUriComponent(); }
    }
    public override PathString PathBase
    {
        get { return new PathString(HttpRequestFeature.PathBase); }
        set { HttpRequestFeature.PathBase = value.Value ?? string.Empty; }
    }    
    public override PathString Path
    {
        get { return new PathString(HttpRequestFeature.Path); }
        set { HttpRequestFeature.Path = value.Value ?? string.Empty; }
    }
    
    public override QueryString QueryString
    {
        get { return new QueryString(HttpRequestFeature.QueryString); }
        set { HttpRequestFeature.QueryString = value.Value ?? string.Empty; }
    }
    public override IQueryCollection Query        
    {
        get { return QueryFeature.Query; }
        set { QueryFeature.Query = value; }
    }
    
    public override string Protocol
    {
        get { return HttpRequestFeature.Protocol; }
        set { HttpRequestFeature.Protocol = value; }
    }
    
    public override IHeaderDictionary Headers
    {
        get { return HttpRequestFeature.Headers; }
    }
    
    public override IRequestCookieCollection Cookies
    {
        get { return RequestCookiesFeature.Cookies; }
        set { RequestCookiesFeature.Cookies = value; }
    }
    
    public override long? ContentLength
    {
        get { return Headers.ContentLength; }
        set { Headers.ContentLength = value; }
    }
    public override string ContentType
    {
        get { return Headers[HeaderNames.ContentType]; }
        set { Headers[HeaderNames.ContentType] = value; }
    }
    public override Stream Body
    {
        get { return HttpRequestFeature.Body; }
        set { HttpRequestFeature.Body = value; }
    }
    public override PipeReader BodyReader
    {
        get { return RequestBodyPipeFeature.Reader; }
    }
    
    public override bool HasFormContentType
    {
        get { return FormFeature.HasFormContentType; }
    }    
    public override IFormCollection Form
    {
        get { return FormFeature.ReadForm(); }
        set { FormFeature.Form = value; }
    }
    
    public override RouteValueDictionary RouteValues
    {
        get { return RouteValuesFeature.RouteValues; }
        set { RouteValuesFeature.RouteValues = value; }
    }                                                                    
}
```

###### 2.2.2.2 features

```c#
internal sealed class DefaultHttpRequest : HttpRequest
{
    // private property
    
    private IHttpRequestFeature HttpRequestFeature =>
        _features.Fetch(
        	ref _features.Cache.Request, 
        	_nullRequestFeature)!;
    
    private IQueryFeature QueryFeature =>
        _features.Fetch(
        	ref _features.Cache.Query, 
        	_newQueryFeature)!;
    
    private IFormFeature FormFeature =>
        _features.Fetch(
        	ref _features.Cache.Form, 
        	this, 
        	_newFormFeature)!;
    
    private IRequestCookiesFeature RequestCookiesFeature =>
        _features.Fetch(
        	ref _features.Cache.Cookies, 
        	_newRequestCookiesFeature)!;
    
    private IRouteValuesFeature RouteValuesFeature =>
        _features.Fetch(
        	ref _features.Cache.RouteValues, 
        	_newRouteValuesFeature)!;
    
    private IRequestBodyPipeFeature RequestBodyPipeFeature =>
        _features.Fetch(
        	ref _features.Cache.BodyPipe, 
        	this.HttpContext, 
        	_newRequestBodyPipeFeature)!;
    
    // default 
    
    private readonly static Func<IFeatureCollection, IHttpRequestFeature?> 
        _nullRequestFeature = f => null;
    
    private readonly static Func<IFeatureCollection, IQueryFeature?> 
        _newQueryFeature = f => new QueryFeature(f);
    
    private readonly static Func<DefaultHttpRequest, IFormFeature> 
        _newFormFeature = r => 
        	new FormFeature(
        		r, 
        		r._context.FormOptions ?? FormOptions.Default);
    
    private readonly static Func<IFeatureCollection, IRequestCookiesFeature> 
        _newRequestCookiesFeature = f => new RequestCookiesFeature(f);
    
    private readonly static Func<IFeatureCollection, IRouteValuesFeature> 
        _newRouteValuesFeature = f => new RouteValuesFeature();
    
    private readonly static Func<HttpContext, IRequestBodyPipeFeature> 
        _newRequestBodyPipeFeature = context => new RequestBodyPipeFeature(context);
}

```

###### 2.2.2.3 json 扩展

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
        var (inputStream, usesTranscodingStream) = 
            GetInputStream(
	            request.HttpContext, 
    	        encoding);
        
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
        return request.ReadFromJsonAsync(
            type, 
            options: null, cancellationToken);
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
        var (inputStream, usesTranscodingStream) = 
            GetInputStream(
            request.HttpContext, 
            encoding);
        
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
        
        if (!MediaTypeHeaderValue.TryParse(
            	request.ContentType, 
            	out var mt))
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
        return httpContext
            .RequestServices
            ?.GetService<IOptions<JsonOptions>>()
            ?.Value
            ?.SerializerOptions 
            	?? JsonOptions.DefaultSerializerOptions;
    }
    
    private static InvalidOperationException CreateContentTypeError(HttpRequest request)
    {
        return new InvalidOperationException(
            $"Unable to read the request as JSON because 
            "the request content type '{request.ContentType}' 
            "is not a known JSON content type.");
    }
    
    private static (Stream inputStream, bool usesTranscodingStream) GetInputStream(
        HttpContext httpContext, 
        Encoding? encoding)
    {
        if (encoding == null || 
            encoding.CodePage == Encoding.UTF8.CodePage)
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
                $"Unable to read the request as JSON because 
                "the request content type charset '{charset}' 
                "is not a known encoding.", 
                ex);
        }
    }
}

```

###### 2.2.2.4  multipart 扩展

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
        
        return HeaderUtilities
            .RemoveQuotes(mediaType.Boundary)
            .ToString();
    }
}

```

###### 2.2.2.5 rewind 扩展

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
        BufferingHelper.EnableRewind(
            request, 
            bufferThreshold);
    }
        
    public static void EnableBuffering(
        this HttpRequest request, 
        long bufferLimit)
    {
        BufferingHelper.EnableRewind(
            request, 
            bufferLimit: bufferLimit);
    }
        
    public static void EnableBuffering(
        this HttpRequest request, 
        int bufferThreshold, 
        long bufferLimit)
    {
        BufferingHelper.EnableRewind(
            request, 
            bufferThreshold, 
            bufferLimit);
    }
}

```

###### 2.2.2.6 form reader 扩展

```c#
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
            throw new InvalidOperationException(
                "Incorrect Content-Type: " + request.ContentType);
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

###### 2.2.2.7 trailer 扩展

```c#
public static class RequestTrailerExtensions
{    
    public static StringValues GetDeclaredTrailers(this HttpRequest request)
    {
        return request
            .Headers
            .GetCommaSeparatedValues(HeaderNames.Trailer);
    }
        
    public static bool SupportsTrailers(this HttpRequest request)
    {
        return request
            .HttpContext
            .Features
            .Get<IHttpRequestTrailersFeature>() != null;
    }
        
    public static bool CheckTrailersAvailable(this HttpRequest request)
    {
        return request
            .HttpContext
            .Features
            .Get<IHttpRequestTrailersFeature>()?.Available == true;
    }
        
    public static StringValues GetTrailer(this HttpRequest request, string trailerName)
    {
        var feature = request
            .HttpContext
            .Features
            .Get<IHttpRequestTrailersFeature>();
        
        if (feature == null)
        {
            throw new NotSupportedException(
                "This request does not support trailers.");
        }
        
        return feature.Trailers[trailerName];
    }
}

```



##### 2.2.3 query collection

###### 2.2.3.1 接口

```c#
public interface IQueryCollection : 	
	IEnumerable<KeyValuePair<string, StringValues>>
{    
    int Count { get; }        
    ICollection<string> Keys { get; }        
    bool ContainsKey(string key);        
    bool TryGetValue(string key, out StringValues value);        
    StringValues this[string key] { get; }
}

```

###### 2.2.3.2 实现

```c#
public class QueryCollection : IQueryCollection
{    
    // empty collection
    public static readonly QueryCollection Empty = new QueryCollection();    
    
    // empty keys & values
    private static readonly string[] EmptyKeys = Array.Empty<string>();
    private static readonly StringValues[] EmptyValues = Array.Empty<StringValues>();
    
    // empty enumerator
    private static readonly Enumerator EmptyEnumerator = new Enumerator();    
    private static readonly IEnumerator<KeyValuePair<string, StringValues>> 
        EmptyIEnumeratorType = EmptyEnumerator;
    private static readonly IEnumerator EmptyIEnumerator = EmptyEnumerator;
    
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
    
    /* 迭代器 */        
    
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
}

```

###### 2.2.3.3 迭代器

```c#
public class QueryCollection : IQueryCollection
{  
    public Enumerator GetEnumerator()
    {
        if (Store == null || Store.Count == 0)
        {
            // Non-boxed Enumerator
            return EmptyEnumerator;
        }
        return new Enumerator(Store.GetEnumerator());
    }
        
    IEnumerator<KeyValuePair<string, StringValues>> 
        IEnumerable<KeyValuePair<string, StringValues>>.GetEnumerator()
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
        
        /// <inheritdoc />
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





##### 2.2.4 request cookie collection

###### 2.2.4.1 接口 

```c#
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

```

###### 2.2.4.2 实现

```c#
internal class RequestCookieCollection : IRequestCookieCollection
{
    // empty collection
    public static readonly RequestCookieCollection Empty = new RequestCookieCollection();
    
    // emtpy kes
    private static readonly string[] EmptyKeys = Array.Empty<string>();    
    
    // empty enumerator    
    private static readonly Enumerator EmptyEnumerator = new Enumerator();    
    private static readonly IEnumerator<KeyValuePair<string, string>> 
        EmptyIEnumeratorType = EmptyEnumerator;
    private static readonly IEnumerator EmptyIEnumerator = EmptyEnumerator;
    
    private Dictionary<string, string>? Store { get; set; }
    
    /* 公共属性 */
    
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
    
    /* 静态方法 */
    
    public static RequestCookieCollection Parse(IList<string> values) => 
        ParseInternal(
        	values, 
        	AppContext.TryGetSwitch(
                ResponseCookies.EnableCookieNameEncoding, 
                out var enabled) && 
        	enabled);
    
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
}

```

###### 2.2.4.3 迭代器

```c#
internal class RequestCookieCollection : IRequestCookieCollection
{
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
    
    
    IEnumerator<KeyValuePair<string, string>> 
        IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
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

##### 2.2.5 form collection

###### 2.2.5.1 接口

```c#
public interface IFormCollection : IEnumerable<KeyValuePair<string, StringValues>>
{    
    int Count { get; }        
    ICollection<string> Keys { get; }
    StringValues this[string key] { get; }        
    IFormFileCollection Files { get; }
    
    bool ContainsKey(string key);        
    bool TryGetValue(string key, out StringValues value);            
}

```

###### 2.2.5.2 实现

```c#
public class FormCollection : IFormCollection
{ 
    // emtpy collection
    public static readonly FormCollection Empty = new FormCollection();
    
    // emtpy keys
    private static readonly string[] EmptyKeys = Array.Empty<string>();
    
    // empty enumerator
    private static readonly Enumerator EmptyEnumerator = new Enumerator();        
    private static readonly IEnumerator<KeyValuePair<string, StringValues>> 
        EmptyIEnumeratorType = EmptyEnumerator;
    private static readonly IEnumerator EmptyIEnumerator = EmptyEnumerator;
        
    // form file collection
    private static IFormFileCollection EmptyFiles = new FormFileCollection();
    private IFormFileCollection? _files;
    
    private Dictionary<string, StringValues>? Store { get; set; }
    
    /* 公共属性 */
    
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
        
    public FormCollection(
        Dictionary<string, StringValues>? fields, 
        IFormFileCollection? files = null)
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
    
   
}

```

###### 2.2.5.3 迭代器

```c#
public class FormCollection : IFormCollection
{ 
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
        
    IEnumerator<KeyValuePair<string, StringValues>> 
        IEnumerable<KeyValuePair<string, StringValues>>.GetEnumerator()
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

##### 2.2.6 form file collection

###### 2.2.6.1 接口

```c#
public interface IFormFileCollection : IReadOnlyList<IFormFile>
{    
    IFormFile? this[string name] { get; }     
    
    IFormFile? GetFile(string name);        
    IReadOnlyList<IFormFile> GetFiles(string name);
}

```

###### 2.2.6.2 实现

```c#
public class FormFileCollection : List<IFormFile>, IFormFileCollection
{
    /// <inheritdoc />
    public IFormFile? this[string name] => GetFile(name);
    
    /// <inheritdoc />
    public IFormFile? GetFile(string name)
    {
        foreach (var file in this)
        {
            if (string.Equals(
                	name, 
                	file.Name, 
                	StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }
        
        return null;
    }
    
    /// <inheritdoc />
    public IReadOnlyList<IFormFile> GetFiles(string name)
    {
        var files = new List<IFormFile>();
        
        foreach (var file in this)
        {
            if (string.Equals(
                	name, 
                	file.Name, 
                	StringComparison.OrdinalIgnoreCase))
            {
                files.Add(file);
            }
        }
        
        return files;
    }
}

```

###### 2.2.6.3 form

* 接口

```c#
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
    Task CopyToAsync(
        Stream target, 
        CancellationToken cancellationToken = default(CancellationToken));
}

```

* 实现

```c#
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

##### 2.2.7 route value dictionary

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
    private int _count;
    
    
    public static RouteValueDictionary FromArray(KeyValuePair<string, object?>[] items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }
        
        // We need to compress the array by removing non-contiguous items. We
        // typically have a very small number of items to process. We don't need
        // to preserve order.
        var start = 0;
        var end = items.Length - 1;
        
        // We walk forwards from the beginning of the array and fill in 'null' slots.
        // We walk backwards from the end of the array end move items in non-null' slots
        // into whatever start is pointing to. O(n)
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
                // Both null, we need to hold on 'start' since we
                // still need to fill it with something.
                end--;
            }
        }
        
        return new RouteValueDictionary()
        {
            _arrayStorage = items!,
            _count = start,
        };
    }
    
    
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
    
    /// <inheritdoc />
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
            
            // We're calling this here for the side-effect 
            // of converting from properties to array. 
            // We need to create the array even if we just set 
            // an existing value since property storage is immutable.
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

        
        public IEqualityComparer<string> Comparer => StringComparer.OrdinalIgnoreCase;

        /// <inheritdoc />
        public int Count => _count;

        /// <inheritdoc />
        bool ICollection<KeyValuePair<string, object?>>.IsReadOnly => false;

        /// <inheritdoc />
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

        IEnumerable<string> IReadOnlyDictionary<string, object?>.Keys => Keys;

        /// <inheritdoc />
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

        IEnumerable<object?> IReadOnlyDictionary<string, object?>.Values => Values;

        /// <inheritdoc />
        void ICollection<KeyValuePair<string, object?>>.Add(KeyValuePair<string, object?> item)
        {
            Add(item.Key, item.Value);
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        bool ICollection<KeyValuePair<string, object?>>.Contains(KeyValuePair<string, object?> item)
        {
            return TryGetValue(item.Key, out var value) && EqualityComparer<object>.Default.Equals(value, item.Value);
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        void ICollection<KeyValuePair<string, object?>>.CopyTo(
            KeyValuePair<string, object?>[] array,
            int arrayIndex)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if (arrayIndex < 0 || arrayIndex > array.Length || array.Length - arrayIndex < this.Count)
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

        /// <inheritdoc />
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        /// <inheritdoc />
        IEnumerator<KeyValuePair<string, object?>> IEnumerable<KeyValuePair<string, object?>>.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
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

            // Ensure property storage is converted to array storage as we'll be
            // applying the lookup and removal on the array
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

        /// <summary>
        /// Attempts to remove and return the value that has the specified key from the <see cref="RouteValueDictionary"/>.
        /// </summary>
        /// <param name="key">The key of the element to remove and return.</param>
        /// <param name="value">When this method returns, contains the object removed from the <see cref="RouteValueDictionary"/>, or <c>null</c> if key does not exist.</param>
        /// <returns>
        /// <c>true</c> if the object was removed successfully; otherwise, <c>false</c>.
        /// </returns>
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

            // Ensure property storage is converted to array storage as we'll be
            // applying the lookup and removal on the array
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

        /// <summary>
        /// Attempts to the add the provided <paramref name="key"/> and <paramref name="value"/> to the dictionary.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>Returns <c>true</c> if the value was added. Returns <c>false</c> if the key was already present.</returns>
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

        /// <inheritdoc />
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



#### 2.3 http response

##### 2.3.1 抽象基类

```c#
public abstract class HttpResponse
{
    private static readonly Func<object, Task> 
        _callbackDelegate = callback => ((Func<Task>)callback)();
    
    private static readonly Func<object, Task> 
        _disposeDelegate = disposable =>
    		{
        		((IDisposable)disposable).Dispose();
        		return Task.CompletedTask;
    		};
    
    private static readonly Func<object, Task> 
        _disposeAsyncDelegate = disposable => 
        	((IAsyncDisposable)disposable)
	        .DisposeAsync()
        	.AsTask();
        
    public abstract HttpContext HttpContext { get; }
    
    public abstract int StatusCode { get; set; }                        
    public abstract bool HasStarted { get; }
            
    public abstract IHeaderDictionary Headers { get; }
    public abstract IResponseCookies Cookies { get; }
    
    public abstract long? ContentLength { get; set; }        
    public abstract string ContentType { get; set; }
    public abstract Stream Body { get; set; }        
    public virtual PipeWriter BodyWriter { get => throw new NotImplementedException(); }
    
    
    public virtual void OnStarting(Func<Task> callback) => 
        OnStarting(_callbackDelegate, callback);
    
    // 在派生类实现
    public abstract void OnStarting(
        Func<object, Task> callback, object state);
    
    public virtual void OnCompleted(Func<Task> callback) => 
        OnCompleted(_callbackDelegate, callback);
            
    // 在派生类实现
    public abstract void OnCompleted(
        Func<object, Task> callback, object state);
        
    public virtual void RegisterForDispose(IDisposable disposable) => 
        OnCompleted(_disposeDelegate, disposable);

    public virtual void RegisterForDisposeAsync(IAsyncDisposable disposable) =>         
        OnCompleted(_disposeAsyncDelegate, disposable);
                   
    public virtual void Redirect(string location) => 
        Redirect(location, permanent: false);
        
    // 在派生类实现
    public abstract void Redirect(string location, bool permanent);
        
    public virtual Task StartAsync(
        CancellationToken cancellationToken = default) 
    { 
        throw new NotImplementedException(); 
    }
        
    public virtual Task CompleteAsync() 
    { 
        throw new NotImplementedException(); 
    }
}

```

##### 2.3.2 默认实现

```c#
internal sealed class DefaultHttpResponse : HttpResponse
{            
    private readonly DefaultHttpContext _context;
    
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
    
    /* 方法 */
    
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
    
    /* 实现（重写）基类方法  */
    
    public override void OnStarting(
        Func<object, Task> callback, object state)
    {
        if (callback == null)
        {
            throw new ArgumentNullException(nameof(callback));
        }
        
        HttpResponseFeature.OnStarting(callback, state);
    }
    
    public override void OnCompleted(
        Func<object, Task> callback, object state)
    {
        if (callback == null)
        {
            throw new ArgumentNullException(nameof(callback));
        }
        
        HttpResponseFeature.OnCompleted(callback, state);
    }
    
    public override void Redirect(
        string location, bool permanent)
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
    
    public override Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        if (HasStarted)
        {
            return Task.CompletedTask;
        }
        
        return HttpResponseBodyFeature.StartAsync(cancellationToken);
    }
    
    public override Task CompleteAsync() => 
        HttpResponseBodyFeature.CompleteAsync();        
}

```

###### 2.3.2.1 公共属性

```c#
internal sealed class DefaultHttpResponse : HttpResponse
{
    public override HttpContext HttpContext { get { return _context; } }
    
    public override int StatusCode
    {
        get { return HttpResponseFeature.StatusCode; }
        set { HttpResponseFeature.StatusCode = value; }
    }
    
    public override bool HasStarted
    {
        get { return HttpResponseFeature.HasStarted; }
    }
    
    public override IHeaderDictionary Headers
    {
        get { return HttpResponseFeature.Headers; }
    }
    
    public override IResponseCookies Cookies
    {
        get { return ResponseCookiesFeature.Cookies; }
    }
    
    public override long? ContentLength
    {
        get { return Headers.ContentLength; }
        set { Headers.ContentLength = value; }
    }
    
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
                HttpResponseFeature
                    .Headers
                    .Remove(HeaderNames.ContentType);
            }
            else
            {
                HttpResponseFeature
                    .Headers[HeaderNames.ContentType] = value;
            }
        }
    }
    
    public override Stream Body
    {
        get { return HttpResponseBodyFeature.Stream; }
        set
        {
            var otherFeature = _features
                .Collection
                .Get<IHttpResponseBodyFeature>()!;
            
            if (otherFeature is StreamResponseBodyFeature streamFeature
                && streamFeature.PriorFeature != null
                && object.ReferenceEquals(
                    value, 
                    streamFeature.PriorFeature.Stream))
            {
                // They're reverting the stream back to the prior one. 
                // Revert the whole feature.
                _features.Collection.Set(streamFeature.PriorFeature);
                return;
            }
            
            _features.Collection.Set<IHttpResponseBodyFeature>(
                new StreamResponseBodyFeature(value, otherFeature));
        }
    }
                        
    public override PipeWriter BodyWriter
    {
        get { return HttpResponseBodyFeature.Writer; }
    }
}

```

###### 2.3.2.2 features

```c#
internal sealed class DefaultHttpResponse : HttpResponse
{
    // feature
    private IHttpResponseFeature HttpResponseFeature =>
        _features.Fetch(ref _features.Cache.Response, _nullResponseFeature)!;

    private IHttpResponseBodyFeature HttpResponseBodyFeature =>
        _features.Fetch(ref _features.Cache.ResponseBody, _nullResponseBodyFeature)!;
    
    private IResponseCookiesFeature ResponseCookiesFeature =>
        _features.Fetch(ref _features.Cache.Cookies, _newResponseCookiesFeature)!;
    
    // default
    private readonly static Func<IFeatureCollection, IHttpResponseFeature?> 
        _nullResponseFeature = f => null;
    
    private readonly static Func<IFeatureCollection, IHttpResponseBodyFeature?> 
        _nullResponseBodyFeature = f => null;
    
    private readonly static Func<IFeatureCollection, IResponseCookiesFeature?> 
        _newResponseCookiesFeature = f => new ResponseCookiesFeature(f);
}

```

###### 2.3.2.3 扩展

```c#
public static class ResponseExtensions
{
    
    public static void Clear(this HttpResponse response)
    {
        if (response.HasStarted)
        {
            throw new InvalidOperationException(
                "The response cannot be cleared, 
                "it has already started sending.");
        }
        
        response.StatusCode = 200;
        
        response
            .HttpContext
            .Features
            .Get<IHttpResponseFeature>()
            !.ReasonPhrase = null;
        
        response.Headers.Clear();
        if (response.Body.CanSeek)
        {
            response.Body.SetLength(0);
        }
    }
        
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
}

```

###### 2.3.2.4 send file 扩展

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
        
        return SendFileAsyncCore(response, file, 0, null, cancellationToken);
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
            count, cancellationToken);
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
                await StreamCopyOperation
                    .CopyToAsync(
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
            await response
                .SendFileAsync(
                	file.PhysicalPath, 
                	offset, 
                	count, 
                	cancellationToken);
        }
    }
    
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
        var sendFile = response
            .HttpContext
            .Features
            .Get<IHttpResponseBodyFeature>()!;
        
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
            (count.GetValueOrDefault() < 0 || 
             count.GetValueOrDefault() > fileLength - offset))
        {
            throw new ArgumentOutOfRangeException(
                nameof(count), 
                count, 
                string.Empty);
        }
    }
}

```

###### 2.3.2.5 writing 扩展

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
        
        var flushAsyncTask = response
            .BodyWriter
            .FlushAsync(cancellationToken);
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

###### 2.3.2.6 trailer 扩展

```c#
public static class ResponseTrailerExtensions
{
    
    public static void DeclareTrailer(
        this HttpResponse response, 
        string trailerName)
    {
        response
            .Headers
            .AppendCommaSeparatedValues(
            	HeaderNames.Trailer, 
            	trailerName);
    }
        
    public static bool SupportsTrailers(this HttpResponse response)
    {
        var feature = response
            .HttpContext
            .Features
            .Get<IHttpResponseTrailersFeature>();
        
        return feature?.Trailers != null && 
               !feature.Trailers.IsReadOnly;
    }
    
    
    public static void AppendTrailer(
        this HttpResponse response, 
        string trailerName, 
        StringValues trailerValues)
    {
        var feature = response
            .HttpContext
            .Features
            .Get<IHttpResponseTrailersFeature>();
        
        if (feature?.Trailers == null || 
            feature.Trailers.IsReadOnly)
        {
            throw new InvalidOperationException(
                "Trailers are not supported for this response.");
        }
        
        feature.Trailers.Append(trailerName, trailerValues);
    }
}

```



##### 2.3.3 response cookie collection

###### 2.3.3.1 接口

```c#
public interface IResponseCookies
{    
    void Append(string key, string value);        
    void Append(string key, string value, CookieOptions options);  
    
    void Delete(string key);        
    void Delete(string key, CookieOptions options);
}

```

###### 2.3.3.2 实现

```c#
internal class ResponseCookies : IResponseCookies
{
    internal const string EnableCookieNameEncoding = 
        "Microsoft.AspNetCore.Http.EnableCookieNameEncoding";
    internal bool _enableCookieNameEncoding = 
        AppContext.TryGetSwitch(
        	EnableCookieNameEncoding, 
        	out var enabled) && 
        enabled;

    private readonly IFeatureCollection _features;
    private ILogger? _logger;
    private IHeaderDictionary Headers { get; set; }
    
        
    internal ResponseCookies(IFeatureCollection features)
    {
        _features = features;
        Headers = _features.Get<IHttpResponseFeature>()!.Headers;
    }
    
            
    public void Append(
        string key, 
        string value)
    {
        var setCookieHeaderValue = new SetCookieHeaderValue(
            _enableCookieNameEncoding 
            	? Uri.EscapeDataString(key) 
            	: key,
            Uri.EscapeDataString(value))
        {
            Path = "/"
        };
        
        var cookieValue = setCookieHeaderValue.ToString();
        
        Headers[HeaderNames.SetCookie] = 
            StringValues.Concat(
            	Headers[HeaderNames.SetCookie], 
            	cookieValue);
    }
        
    public void Append(
        string key, 
        string value, 
        CookieOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        // SameSite=None cookies must be marked as Secure.
        if (!options.Secure && options.SameSite == SameSiteMode.None)
        {
            if (_logger == null)
            {
                var services = _features
                    .Get<Features.IServiceProvidersFeature>()
                    ?.RequestServices;
                _logger = services?.GetService<ILogger<ResponseCookies>>();
            }
            
            if (_logger != null)
            {
                Log.SameSiteCookieNotSecure(_logger, key);
            }
        }
        
        var setCookieHeaderValue = new SetCookieHeaderValue(
            _enableCookieNameEncoding 
            	? Uri.EscapeDataString(key) 
            	: key,
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
        
        Headers[HeaderNames.SetCookie] = 
            StringValues.Concat(
            	Headers[HeaderNames.SetCookie], 
            	cookieValue);
    }
    
    /// <inheritdoc />
    public void Delete(string key)
    {
        Delete(key, new CookieOptions() { Path = "/" });
    }
    
    /// <inheritdoc />
    public void Delete(string key, CookieOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        var encodedKeyPlusEquals = 
            (_enableCookieNameEncoding 
             	? Uri.EscapeDataString(key) 
             	: key) + 
            "=";
        
        bool domainHasValue = !string.IsNullOrEmpty(options.Domain);
        bool pathHasValue = !string.IsNullOrEmpty(options.Path);
        
        Func<string, string, CookieOptions, bool> rejectPredicate;
        if (domainHasValue)
        {
            rejectPredicate = (value, encKeyPlusEquals, opts) =>
                value.StartsWith(
                	encKeyPlusEquals, 
                	StringComparison.OrdinalIgnoreCase) &&
                value.IndexOf(
                	$"domain={opts.Domain}", 
                	StringComparison.OrdinalIgnoreCase) != -1;
        }
        else if (pathHasValue)
        {
            rejectPredicate = (value, encKeyPlusEquals, opts) =>
                value.StartsWith(
                	encKeyPlusEquals, 
                	StringComparison.OrdinalIgnoreCase) &&
                value.IndexOf(
                	$"path={opts.Path}", 
                	StringComparison.OrdinalIgnoreCase) != -1;
        }
        else
        {
            rejectPredicate = (value, encKeyPlusEquals, opts) => 
                value.StartsWith(
                	encKeyPlusEquals, 
                	StringComparison.OrdinalIgnoreCase);
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
        private static readonly Action<ILogger, string, Exception?> 
            _samesiteNotSecure = LoggerMessage.Define<string>(
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

```

###### 2.3.3.3 cookie options

```c#
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

#### 2.4 connection info

##### 2.4.1 抽象基类

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

##### 2.4.2 默认实现

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
    
    // 实现（重写）基类方法
    public override Task<X509Certificate2?> GetClientCertificateAsync(
        CancellationToken cancellationToken = default)
    {
        return TlsConnectionFeature
            .GetClientCertificateAsync(cancellationToken);
    }        
}

```

###### 2.4.2.1 公共属性

```c#
internal sealed class DefaultConnectionInfo : ConnectionInfo
{
    public override string Id
    {
        get { return HttpConnectionFeature.ConnectionId; }
        set { HttpConnectionFeature.ConnectionId = value; }
    }
    
    public override IPAddress? RemoteIpAddress
    {
        get { return HttpConnectionFeature.RemoteIpAddress; }
        set { HttpConnectionFeature.RemoteIpAddress = value; }
    }
    
    public override int RemotePort
    {
        get { return HttpConnectionFeature.RemotePort; }
        set { HttpConnectionFeature.RemotePort = value; }
    }
    
    public override IPAddress? LocalIpAddress
    {
        get { return HttpConnectionFeature.LocalIpAddress; }
        set { HttpConnectionFeature.LocalIpAddress = value; }
    }
    
    public override int LocalPort
    {
        get { return HttpConnectionFeature.LocalPort; }
        set { HttpConnectionFeature.LocalPort = value; }
    }
    
    public override X509Certificate2? ClientCertificate
    {
        get { return TlsConnectionFeature.ClientCertificate; }
        set { TlsConnectionFeature.ClientCertificate = value; }
    }
}

```

###### 2.4.2.2 features

```c#
internal sealed class DefaultConnectionInfo : ConnectionInfo
{
    // features
    private IHttpConnectionFeature HttpConnectionFeature =>
        _features.Fetch(ref _features.Cache.Connection, _newHttpConnectionFeature)!;
    
    private ITlsConnectionFeature TlsConnectionFeature=>
        _features.Fetch(ref _features.Cache.TlsConnection, _newTlsConnectionFeature)!;
    
    // default
    private readonly static Func<IFeatureCollection, IHttpConnectionFeature> 
        _newHttpConnectionFeature = f => new HttpConnectionFeature();
    
    private readonly static Func<IFeatureCollection, ITlsConnectionFeature> 
        _newTlsConnectionFeature = f => new TlsConnectionFeature();
}

```

#### 2.5 web socket manager

##### 2.5.1 抽象基类

```c#
public abstract class WebSocketManager
{    
    public abstract bool IsWebSocketRequest { get; }        
    public abstract IList<string> WebSocketRequestedProtocols { get; }
        
    public virtual Task<WebSocket> AcceptWebSocketAsync()
    {
        return AcceptWebSocketAsync(subProtocol: null);
    }
    
    // 在派生类中实现
    public abstract Task<WebSocket> AcceptWebSocketAsync(string? subProtocol);
}

```

##### 2.5.2 默认实现

```c#
internal sealed class DefaultWebSocketManager : WebSocketManager
{            
    private FeatureReferences<FeatureInterfaces> _features;
    struct FeatureInterfaces
    {
        public IHttpRequestFeature? Request;
        public IHttpWebSocketFeature? WebSockets;
    }
    
    /* 构造函数 */
    public DefaultWebSocketManager(IFeatureCollection features)
    {
        Initialize(features);
    }
    
    /* 方法 */
    
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
                    
    public override Task<WebSocket> AcceptWebSocketAsync(string? subProtocol)
    {
        if (WebSocketFeature == null)
        {
            throw new NotSupportedException("WebSockets are not supported");
        }
        
        return WebSocketFeature
            .AcceptAsync(new WebSocketAcceptContext() 
            	{ 
                    SubProtocol = subProtocol 
                });
    }        
}

```

###### 2.5.2.1 公共属性

```c#
internal sealed class DefaultWebSocketManager : WebSocketManager
{
    public override bool IsWebSocketRequest
    {
        get
        {
            return WebSocketFeature != null && 
                   WebSocketFeature.IsWebSocketRequest;
        }
    }
    
    public override IList<string> WebSocketRequestedProtocols
    {
        get
        {
            return HttpRequestFeature
                .Headers
                .GetCommaSeparatedValues(
                	HeaderNames.WebSocketSubProtocols);
        }
    }
}

```

###### 2.5.2.2 features

```c#
internal sealed class DefaultWebSocketManager : WebSocketManager
{
    // features
    private IHttpRequestFeature HttpRequestFeature =>
        _features.Fetch(ref _features.Cache.Request, _nullRequestFeature)!;
    
    private IHttpWebSocketFeature WebSocketFeature =>
        _features.Fetch(ref _features.Cache.WebSockets, _nullWebSocketFeature)!;
    
    // default
    private readonly static Func<IFeatureCollection, IHttpRequestFeature?> 
        _nullRequestFeature = f => null;
    
    private readonly static Func<IFeatureCollection, IHttpWebSocketFeature?> 
        _nullWebSocketFeature = f => null;
}

```

##### 2.5.3 web socket accept context

```c#
public class WebSocketAcceptContext
{    
    public virtual string? SubProtocol { get; set; }
}

```

#### 2.6 session

##### 2.6.1 接口

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

##### 2.6.2 扩展

```c#
public static class SessionExtensions
{    
    public static void SetInt32(
        this ISession session, 
        string key, 
        int value)
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
        
    public static int? GetInt32(
        this ISession session, 
        string key)
    {
        var data = session.Get(key);
        if (data == null || data.Length < 4)
        {
            return null;
        }
        return data[0] << 24 | data[1] << 16 | data[2] << 8 | data[3];
    }
        
    public static void SetString(
        this ISession session, 
        string key, 
        string value)
    {
        session.Set(key, Encoding.UTF8.GetBytes(value));
    }
        
    public static string? GetString(
        this ISession session, 
        string key)
    {
        var data = session.Get(key);
        if (data == null)
        {
            return null;
        }
        return Encoding.UTF8.GetString(data);
    }
       
    public static byte[]? Get(
        this ISession session, 
        string key)
    {
        session.TryGetValue(key, out var value);
        return value;
    }
}

```

#### 2.7 about feature

* 实现某种特定功能的接口

##### 2.7.1 feature collection

###### 2.7.1.1 接口

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

###### 2.7.1.2 实现

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

##### 2.7.2 feature reference

###### 2.7.2.1 reference

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

###### 2.7.2.2 references

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

#### 2.8 创建 http context

##### 2.8.1 http context factory

```c#
public interface IHttpContextFactory
{    
    HttpContext Create(IFeatureCollection featureCollection);        
    void Dispose(HttpContext httpContext);
}

```

##### 2.8.2 http context accessor

###### 2.8.2.1 接口

```c#
public interface IHttpContextAccessor
{        
    HttpContext? HttpContext { get; set; }
}

```

###### 2.8.2.2 实现

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

###### 2.8.2.3 service collection 扩展方法

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

#### 2.9 various features



##### 2.9.2 request

###### 2.9.2.1 request feature

* 接口

```c#
public interface IHttpRequestFeature
{    
    string Protocol { get; set; }                
    string Scheme { get; set; }        
    string Method { get; set; }        
    string PathBase { get; set; }        
    string Path { get; set; }                
    string QueryString { get; set; }        
    string RawTarget { get; set; }        
    IHeaderDictionary Headers { get; set; }        
    Stream Body { get; set; }
}

```

* 实现

```c#
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

###### 2.9.2.2 request identifier feature

* 接口

```c#
public interface IHttpRequestIdentifierFeature
{    
    string TraceIdentifier { get; set; }
}

```

* 实现

```c#
public class HttpRequestIdentifierFeature : IHttpRequestIdentifierFeature
{
    // Base32 encoding - in ascii sort order for easy text based sorting
    private static readonly char[] s_encode32Chars = 
        "0123456789ABCDEFGHIJKLMNOPQRSTUV".ToCharArray();
    
    // Seed the _requestId for this application instance with
    // the number of 100-nanosecond intervals that 
    // have elapsed since 12:00:00 midnight, January 1, 0001
    // for a roughly increasing _requestId over restarts
    private static long _requestId = DateTime.UtcNow.Ticks;
    
    private string? _id = null;
        
    public string TraceIdentifier
    {
        get
        {
            // Don't incur the cost of generating the request ID until it's asked for
            if (_id == null)
            {
                _id = GenerateRequestId(
                    Interlocked.Increment(ref _requestId));
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

###### 2.9.2.3 request trailer feature

```c#
public interface IHttpRequestTrailersFeature
{    
    bool Available { get; }        
    IHeaderDictionary Trailers { get; }
}

```

###### 2.9.2.4 query feature

* 接口

```c#
public interface IQueryFeature
{    
    IQueryCollection Query { get; set; }
}

```

* 实现

```c#
public class QueryFeature : IQueryFeature
{
    
    private readonly static Func<IFeatureCollection, IHttpRequestFeature?> 
        _nullRequestFeature = f => null;
    
    private FeatureReferences<IHttpRequestFeature> _features;
     private IHttpRequestFeature HttpRequestFeature =>
        _features.Fetch(ref _features.Cache, _nullRequestFeature)!;    
    private string? _original;
    private IQueryCollection? _parsedValues;
    
    /* 构造函数*/
    
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
    
   	/* 方法 */
       
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
                !string.Equals(
                    _original, 
                    current, 
                    StringComparison.Ordinal))
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
}

```



###### 2.9.2.5 request cookie feature

* 接口

```c#
public interface IRequestCookiesFeature
{    
    IRequestCookieCollection Cookies { get; set; }
}

```

* 实现

```c#
public class RequestCookiesFeature : IRequestCookiesFeature
{    
    private readonly static Func<IFeatureCollection, IHttpRequestFeature?> 
        _nullRequestFeature = f => null;
    
    private FeatureReferences<IHttpRequestFeature> _features;
    private IHttpRequestFeature HttpRequestFeature =>
        _features.Fetch(ref _features.Cache, _nullRequestFeature)!;
    
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
                    HttpRequestFeature
                        .Headers
                        .Remove(HeaderNames.Cookie);
                }
                else
                {
                    var headers = new List<string>(_parsedValues.Count);
                    foreach (var pair in _parsedValues)
                    {
                        headers.Add(
                            new CookieHeaderValue(pair.Key, pair.Value).ToString());
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



###### 2.9.2.6 request lifetime feature

* 接口

```c#
public interface IHttpRequestLifetimeFeature
{    
    CancellationToken RequestAborted { get; set; }
        
    void Abort();
}

```

* 实现

```c#
public class HttpRequestLifetimeFeature : IHttpRequestLifetimeFeature
{    
    public CancellationToken RequestAborted { get; set; }
    
    public void Abort()
    {
    }
}

```

###### 2.9.2.7 request body detect feature

```c#
public interface IHttpRequestBodyDetectionFeature
{    
    bool CanHaveBody { get; }
}

```

###### 2.9.2.8 max request body size feature



###### 2.9.2.9 request body pipe feature

* 接口

```c#
public interface IRequestBodyPipeFeature
{    
    PipeReader Reader { get; }
}

```

* 实现

```c#
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
                !ReferenceEquals(
                    _streamInstanceWhenWrapped, 
                    _context.Request.Body))
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

###### 2.9.2.10 form feature

* 接口

```c#
public interface IFormFeature
{    
    bool HasFormContentType { get; }        
    IFormCollection? Form { get; set; }
        
    IFormCollection ReadForm();        
    Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken);
}

```

* 实现

```c#
public class FormFeature : IFormFeature
{
    private readonly HttpRequest _request;
    private readonly FormOptions _options;
    private Task<IFormCollection>? _parsedFormTask;        
    private IFormCollection? _form;    
    
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
            return HasApplicationFormContentType(contentType) || 
                   HasMultipartFormContentType(contentType);
        }
    }
            
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
    
    public FormFeature(
        HttpRequest request, 
        FormOptions options)        
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
            throw new InvalidOperationException(
                "Incorrect Content-Type: " + _request.ContentType);
        }
        
        // TODO: Issue #456 Avoid Sync-over-Async 
        // http://blogs.msdn.com/b/pfxteam/archive/2012/04/13/10293638.aspx
        // TODO: How do we prevent thread exhaustion?
        return ReadFormAsync().GetAwaiter().GetResult();
    }
    
    /// <inheritdoc />
    public Task<IFormCollection> ReadFormAsync() => ReadFormAsync(CancellationToken.None);
    
    /// <inheritdoc />
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
    
    private async Task<IFormCollection> InnerReadFormAsync(
        CancellationToken cancellationToken)
    {
        if (!HasFormContentType)
        {
            throw new InvalidOperationException(
                "Incorrect Content-Type: " + _request.ContentType);
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
        
        // Some of these code paths use StreamReader 
        // which does not support cancellation tokens.
        using (cancellationToken.Register(
                  state => ((HttpContext)state!).Abort(), 
            	  _request.HttpContext))
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
                formFields = new FormCollection(
                    await formReader.ReadFormAsync(cancellationToken));
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
                var section = await multipartReader
                    .ReadNextSectionAsync(cancellationToken);
                while (section != null)
                {
                    // Parse the content disposition here 
                    // and pass it further to avoid eparsings
                    if (!ContentDispositionHeaderValue.TryParse(
                        	section.ContentDisposition, 
                        	out var contentDisposition))
                    {
                        throw new InvalidDataException(
                            "Form section has invalid Content-Disposition value: " + 
                            section.ContentDisposition);
                    }
                    
                    if (contentDisposition.IsFileDisposition())
                    {
                        var fileSection = new FileMultipartSection(
                            section, 
                            contentDisposition);
                        
                        // Enable buffering for the file if not already done for the full body
                        section.EnableRewind(
                            _request
                            	.HttpContext
                            	.Response
                            	.RegisterForDispose,
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
                            throw new InvalidDataException(
                                $"Form value count limit 
                                "{_options.ValueCountLimit} exceeded.");
                        }
                        files.Add(file);
                    }
                    else if (contentDisposition.IsFormDisposition())
                    {
                        var formDataSection = new FormMultipartSection(
                            section, 
                            contentDisposition);
                        
                        // Content-Disposition: form-data; name="key" value                   
                        // Do not limit the key name length here 
                        // because the multipart headers length limit is already in effect.
                        var key = formDataSection.Name;
                        var value = await formDataSection.GetValueAsync();
                        
                        formAccumulator.Append(key, value);
                        if (formAccumulator.ValueCount > _options.ValueCountLimit)
                        {
                            throw new InvalidDataException(
                                $"Form value count limit 
                                "{_options.ValueCountLimit} exceeded.");
                        }
                    }
                    else
                    {
                        System
                            .Diagnostics
                            .Debug
                            .Assert(
                            	false, 
                            	"Unrecognized content-disposition for this section: " +
                            	section.ContentDisposition);
                    }
                    
                    section = await multipartReader
                        .ReadNextSectionAsync(cancellationToken);
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
               contentType
               	   .MediaType
            	   .Equals(
            		   "application/x-www-form-urlencoded", 
            		   StringComparison.OrdinalIgnoreCase);
    }
    
    private bool HasMultipartFormContentType(
        [NotNullWhen(true)] MediaTypeHeaderValue? contentType)
    {
        // Content-Type: multipart/form-data; 
        // boundary=----WebKitFormBoundarymx2fSWqWSd0OxQqq
        return contentType != null && 
               contentType
                   .MediaType
            	   .Equals(
            		   "multipart/form-data", 
            		   StringComparison.OrdinalIgnoreCase);
    }
    
    private bool HasFormDataContentDisposition(
        ContentDispositionHeaderValue contentDisposition)
    {
        // Content-Disposition: form-data; name="key";
        return contentDisposition != null && 
               contentDisposition
            	   .DispositionType
             	   .Equals("form-data") && 
               StringSegment
            	   .IsNullOrEmpty(contentDisposition.FileName) && 
               StringSegment
            	   .IsNullOrEmpty(contentDisposition.FileNameStar);
    }
    
    private bool HasFileContentDisposition(
        ContentDispositionHeaderValue contentDisposition)
    {
        // Content-Disposition: form-data; name="myfile1"; filename="Misc 002.jpg"
        return contentDisposition != null && 
               contentDisposition
             	   .DispositionType
            	   .Equals("form-data") && 
               (!StringSegment.IsNullOrEmpty(contentDisposition.FileName) || 
                !StringSegment.IsNullOrEmpty(contentDisposition.FileNameStar));
    }
    
    // Content-Type: multipart/form-data; boundary="----WebKitFormBoundarymx2fSWqWSd0OxQqq"
    // The spec says 70 characters is a reasonable limit.
    private static string GetBoundary(
        MediaTypeHeaderValue contentType, 
        int lengthLimit)
    {
        var boundary = HeaderUtilities
            .RemoveQuotes(contentType.Boundary);
        if (StringSegment.IsNullOrEmpty(boundary))
        {
            throw new InvalidDataException("Missing content-type boundary.");
        }
        if (boundary.Length > lengthLimit)
        {
            throw new InvalidDataException(
                $"Multipart boundary length limit {lengthLimit} exceeded.");
        }
        return boundary.ToString();
    }
}

```

* form options

```c#
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
    
    public int MultipartBoundaryLengthLimit { get; set; } = 
        DefaultMultipartBoundaryLengthLimit;        
    public int MultipartHeadersCountLimit { get; set; } = 
        MultipartReader.DefaultHeadersCountLimit;        
    public int MultipartHeadersLengthLimit { get; set; } = 
        MultipartReader.DefaultHeadersLengthLimit;        
    public long MultipartBodyLengthLimit { get; set; } = 
        DefaultMultipartBodyLengthLimit;
}

```







##### 2.9.2 response

###### 2.9.2.1 response feature

* 接口

```c#
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

```

* 实现

```c#
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
                   
    public virtual void OnStarting(
        Func<object, Task> callback, 
        object state)
    {
    }
        
    public virtual void OnCompleted(
        Func<object, Task> callback, 
        object state)
    {
    }
}

```

###### 2.9.2.2 response trailer feature

```c#
public interface IHttpResponseTrailersFeature
{    
    IHeaderDictionary Trailers { get; set; }
}

```



###### 2.9.2.3 response cookies feature

* 接口

```c#
public interface IResponseCookiesFeature
{    
    IResponseCookies Cookies { get; }
}

```

* 实现

```c#
public class ResponseCookiesFeature : IResponseCookiesFeature
{    
    private readonly static Func<IFeatureCollection, IHttpResponseFeature?> 
        nullResponseFeature = f => null;
    
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
        _features = features 
            ?? throw new ArgumentNullException(nameof(feaures));
    }        
}

```

###### 2.9.2.4 response body feature

```c#
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

```



##### 2.9.3 http connection feature

* 接口

```c#
public interface IHttpConnectionFeature
{    
    string ConnectionId { get; set; }
        
    IPAddress? RemoteIpAddress { get; set; }
    int RemotePort { get; set; }
    
    IPAddress? LocalIpAddress { get; set; }                   
    int LocalPort { get; set; }
}

```

* 实现

```c#
public class HttpConnectionFeature : IHttpConnectionFeature
{    
    public string ConnectionId { get; set; } = default!;        

    public IPAddress? LocalIpAddress { get; set; }        
    public int LocalPort { get; set; }
        
    public IPAddress? RemoteIpAddress { get; set; }    
    public int RemotePort { get; set; }
}

```

##### 2.9.4 http web socket feature

```c#
public interface IHttpWebSocketFeature
{    
    bool IsWebSocketRequest { get; }        
    Task<WebSocket> AcceptAsync(WebSocketAcceptContext context);
}

```

##### 2.9.5 http upgrade feature

```c#
public interface IHttpUpgradeFeature
{    
    bool IsUpgradableRequest { get; }
        
    Task<Stream> UpgradeAsync();
}

```

##### 2.9.6 https compression feature

```c#
public interface IHttpsCompressionFeature
{    
    HttpsCompressionMode Mode { get; set; }
}

```

##### 2.9.7 http body control feature

```c#
public interface IHttpBodyControlFeature
{    
    bool AllowSynchronousIO { get; set; }
}

```

##### 2.9.8 http reset feature

```c#
public interface IHttpResetFeature
{    
    void Reset(int errorCode);
}

```

##### 2.9.9 session

```c#
public interface ISessionFeature
{    
    ISession Session { get; set; }
}

```

##### 2.9.10 authentication (claims)

```c#
public interface IHttpAuthenticationFeature
{    
    ClaimsPrincipal? User { get; set; }
}

```

##### 2.9.11 tls

###### 2.9.11.1 tls connection feature

```c#
public interface ITlsConnectionFeature
{    
    X509Certificate2? ClientCertificate { get; set; }        
    Task<X509Certificate2?> GetClientCertificateAsync(CancellationToken cancellationToken);
}

```

###### 2.9.11.2 tls token binding feature

```c#
public interface ITlsTokenBindingFeature
{    
    byte[] GetProvidedTokenBindingId();            
    byte[] GetReferredTokenBindingId();
}

```

##### 2.9.12 service provider feature

* 接口

```c#
public interface IServiceProvidersFeature
{    
    IServiceProvider RequestServices { get; set; }
}

```

* 实现

```c#
public class ServiceProvidersFeature : IServiceProvidersFeature
{
    /// <inheritdoc />
    public IServiceProvider RequestServices { get; set; } = default!
}

```



















