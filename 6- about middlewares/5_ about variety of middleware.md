## variety of middleware



### 1. about



### 2. http

#### 2.1 websocket

##### 2.1.1 add websocket

* 注入 websocket 需要的服务

```c#
public static class WebSocketsDependencyInjectionExtensions
{    
    public static IServiceCollection AddWebSockets(
        this IServiceCollection services, 
        Action<WebSocketOptions> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }
        
        return services.Configure(configure);
    }
}

```

###### 2.1.2.1 websocket options

```c#
public class WebSocketOptions
{
    // alive interval
    public TimeSpan KeepAliveInterval { get; set; }
    // allowed orgins
    public IList<string> AllowedOrigins { get; }
    
    public WebSocketOptions()
    {
        KeepAliveInterval = TimeSpan.FromMinutes(2);
        AllowedOrigins = new List<string>();
    }        
}

```

##### 2.1.2 use websocket

* 配置 websocket 中间件

```c#
public static class WebSocketMiddlewareExtensions
{    
    // without options (default options)
    public static IApplicationBuilder UseWebSockets(this IApplicationBuilder app)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        
        return app.UseMiddleware<WebSocketMiddleware>();
    }
    
    // with websocket options
    public static IApplicationBuilder UseWebSockets(
        this IApplicationBuilder app, 
        WebSocketOptions options)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        return app.UseMiddleware<WebSocketMiddleware>(Options.Create(options));
    }
}

```

###### 2.1.2.1 websocket middleware

```c#
public class WebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly WebSocketOptions _options;
    private readonly ILogger _logger;
    
    private readonly bool _anyOriginAllowed;
    private readonly List<string> _allowedOrigins;
        
    public WebSocketMiddleware(
        RequestDelegate next, 
        IOptions<WebSocketOptions> options, 
        ILoggerFactory loggerFactory)
    {
        if (next == null)
        {
            throw new ArgumentNullException(nameof(next));
        }
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        _next = next;
        _options = options.Value;
                
        // 解析 allowed origins
        _allowedOrigins = _options.AllowedOrigins
            					  .Select(o => o.ToLowerInvariant())
            					  .ToList();
        // 解析 allow any origin (flag)
        _anyOriginAllowed = _options.AllowedOrigins.Count == 0 || 
            				_options.AllowedOrigins.Contains("*", StringComparer.Ordinal);

         _logger = loggerFactory.CreateLogger<WebSocketMiddleware>();                   
    }
        
    public Task Invoke(HttpContext context)
    {
        // Detect if an opaque upgrade is available. If so, add a websocket upgrade.
        
        // 解析 upgrade feature
        var upgradeFeature = context.Features.Get<IHttpUpgradeFeature>();
        
        // 如果能解析到 upgrade featue，且 websocket feature 为 null
        if (upgradeFeature != null && 
            context.Features.Get<IHttpWebSocketFeature>() == null)
        {
            // 创建 websocket feature (upgrade handshake)，
            // 并注入 http context features
            var webSocketFeature = new UpgradeHandshake(context, upgradeFeature, _options);
            context.Features.Set<IHttpWebSocketFeature>(webSocketFeature);
            
            // 如果 allow any orgin (flag)
            if (!_anyOriginAllowed)
            {                
                // 解析 http request header - origin
                var originHeader = context.Request
                    					  .Headers[HeaderNames.Origin];
                
                // 如果 origin header 为空，且（request）是 websocket request，
                if (!StringValues.IsNullOrEmpty(originHeader) && 
                    webSocketFeature.IsWebSocketRequest)
                {
                    // 但是如果 origin header 不在 allowed origins 集合中，-> 抛出异常
                    if (!_allowedOrigins.Contains(
	                        originHeader.ToString(), 
    	                    StringComparer.Ordinal))
                    {
                        _logger.LogDebug(
                            "Request origin {Origin} is not in the list of allowed origins.", 
                            originHeader);
                        
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }
                }
            }
        }
        
        return _next(context);
    }        
}

```

###### 2.1.2.2 http upgrade feature?

```c#
// 接口
public interface IHttpUpgradeFeature
{    
    bool IsUpgradableRequest { get; }        
    Task<Stream> UpgradeAsync();
}

```

###### 2.1.2.3 http websocket feature

```c#
// 接口
public interface IHttpWebSocketFeature
{    
    bool IsWebSocketRequest { get; }        
    Task<WebSocket> AcceptAsync(WebSocketAcceptContext context);
}

// 实现
public class WebSocketMiddleware
{
    private class UpgradeHandshake : IHttpWebSocketFeature
    {
        private readonly HttpContext _context;
        private readonly IHttpUpgradeFeature _upgradeFeature;
        private readonly WebSocketOptions _options;
        
        private bool? _isWebSocketRequest;
        public bool IsWebSocketRequest
        {
            // 返回 _isWebSocketRequest 的 value           
            get
            {
                // 如果 _isWebSocketRequest 为 null，
                if (_isWebSocketRequest == null)
                {
                    // http upgrade feature 不是 upgradable request，-> false
                    if (!_upgradeFeature.IsUpgradableRequest)
                    {
                        _isWebSocketRequest = false;
                    }
                    else
                    {
                        // 解析 http request header
                        var headers = new List<KeyValuePair<string, string>>();
                        foreach (string headerName in HandshakeHelpers.NeededHeaders)
                        {
                            foreach (var value in 
                                     _context.Request
                                     		 .Headers
                                     		 .GetCommaSeparatedValues(headerName))
                            {
                                headers.Add(
                                    new KeyValuePair<string, string>(headerName, value));
                            }
                        }
                        
                        // 使用 hand shake helper 判断 is websocket request
                        _isWebSocketRequest = 
                            HandshakeHelpers.CheckSupportedWebSocketRequest(
                            	_context.Request.Method, 
                            	headers);
                    }
                }
                
                return _isWebSocketRequest.Value;
            }
        }
        
        public UpgradeHandshake(
            HttpContext context, 
            IHttpUpgradeFeature upgradeFeature, 
            WebSocketOptions options)
        {
            _context = context;
            _upgradeFeature = upgradeFeature;
            _options = options;
        }
                        
        public async Task<WebSocket> AcceptAsync(WebSocketAcceptContext acceptContext)
        {
            // 如果 http request 不是 websocket request，抛出异常
            if (!IsWebSocketRequest)
            {
                throw new InvalidOperationException("Not a WebSocket request.");
            }
            
            // 解析 sup protocol 
            string? subProtocol = null;            
            if (acceptContext != null)
            {
                subProtocol = acceptContext.SubProtocol;
            }
            
            // 解析 keep alive interval
            TimeSpan keepAliveInterval = _options.KeepAliveInterval;            
            var advancedAcceptContext = acceptContext as ExtendedWebSocketAcceptContext;
            if (advancedAcceptContext != null)
            {
                if (advancedAcceptContext.KeepAliveInterval.HasValue)
                {
                    keepAliveInterval = advancedAcceptContext.KeepAliveInterval.Value;
                }
            }
            
            // 解析 http request header - sec websocket key
            string key = _context.Request
                				 .Headers[HeaderNames.SecWebSocketKey];
            
            // 创建 http response header
            HandshakeHelpers.GenerateResponseHeaders(
                key, 
                subProtocol, 
                _context.Response.Headers);
                        
            // Sets status code to 101
            // 通过 upgrade feature 创建 stream
            Stream opaqueTransport = await _upgradeFeature.UpgradeAsync(); 
            
            return WebSocket.CreateFromStream(
                opaqueTransport, 
                isServer: true, 
                subProtocol: subProtocol, 
                keepAliveInterval: keepAliveInterval);
        }
    }
}
    
```

###### 2.1.2.4 (extended) websocket accept context

```c#
//
public class WebSocketAcceptContext
{    
    public virtual string? SubProtocol { get; set; }
}

//
public class ExtendedWebSocketAcceptContext : WebSocketAcceptContext
{    
    public override string? SubProtocol { get; set; }                
    public TimeSpan? KeepAliveInterval { get; set; }
}

```

###### 2.1.2.5 handshake helpers

```c#
internal static class HandshakeHelpers
{    
    internal static class Constants
    {
        public static class Headers
        {                  
            public const string UpgradeWebSocket = "websocket"; 
            public const string ConnectionUpgrade = "Upgrade";
            public const string SupportedVersion = "13";
        }
    }
    
    public static readonly IEnumerable<string> NeededHeaders = new[]
    {
        HeaderNames.Upgrade,
        HeaderNames.Connection,
        HeaderNames.SecWebSocketKey,
        HeaderNames.SecWebSocketVersion
    };
    
    // "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
    // This uses C# compiler's ability to refer to static data directly. 
    private static ReadOnlySpan<byte> EncodedWebSocketKey => new byte[]
    {
        (byte)'2', (byte)'5', (byte)'8', (byte)'E', (byte)'A', (byte)'F', (byte)'A', (byte)'5',
        (byte)'-',
        (byte)'E', (byte)'9', (byte)'1', (byte)'4', 
        (byte)'-', 
        (byte)'4', (byte)'7', (byte)'D', (byte)'A',
        (byte)'-', 
        (byte)'9', (byte)'5', (byte)'C', (byte)'A', 
        (byte)'-', 
        (byte)'C', (byte)'5', (byte)'A', (byte)'B', (byte)'0', (byte)'D', (byte)'C', (byte)'8', 
        (byte)'5', (byte)'B', (byte)'1', (byte)'1'
    };
    
    // Verify Method, Upgrade, Connection, version,  key, etc..
    public static bool CheckSupportedWebSocketRequest(
        string method, 
        IEnumerable<KeyValuePair<string, string>> headers)
    {
        bool validUpgrade = false, 
        	 validConnection = false, 
        	 validKey = false, 
        	 validVersion = false;
        
        // 如果不是 get 方法，-> false
        if (!string.Equals(
            	"GET", 
            	method, 
            	StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        
        // 遍历 request header，
        foreach (var pair in headers)
        {
            // 如果 header 有 [connection, "upgrade"]，-> true
            if (string.Equals(
                	HeaderNames.Connection, 
                	pair.Key, 
                	StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(
                    	Constants.Headers.ConnectionUpgrade, 
                    	pair.Value, 
                    	StringComparison.OrdinalIgnoreCase))
                {
                    validConnection = true;
                }
            }
            // 如果 header 有 [upgrade, "websocket"]，-> true
            else if (string.Equals(
                		HeaderNames.Upgrade, 
                		pair.Key, 
                		StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(
                    	Constants.Headers.UpgradeWebSocket, 
                    	pair.Value, 
                    	StringComparison.OrdinalIgnoreCase))
                {
                    validUpgrade = true;
                }
            }
            // 如果 header 有 [sec websocket version, "13"]
            else if (string.Equals(
                		HeaderNames.SecWebSocketVersion, 
                		pair.Key, 
                		StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(
                    	Constants.Headers.SupportedVersion, 
                    	pair.Value, 
                    	StringComparison.OrdinalIgnoreCase))
                {
                    validVersion = true;
                }
            }
            // 如果 header 有 [sec websocket key, ...]，
            else if (string.Equals(
                		HeaderNames.SecWebSocketKey, 
                		pair.Key, 
                		StringComparison.OrdinalIgnoreCase))
            {
                // 验证 value 是否 request valid key
                validKey = IsRequestKeyValid(pair.Value);
            }
        }
        
        // 全部满足 -> true
        return validConnection && 
               validUpgrade && 
               validVersion && 
               validKey;
    }
    
    public static bool IsRequestKeyValid(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }
        
        Span<byte> temp = stackalloc byte[16];
        var success = Convert.TryFromBase64String(value, temp, out var written);
        return success && written == 16;
    }
    
    // 创建 response header
    public static void GenerateResponseHeaders(
        string key, 
        string? subProtocol, 
        IHeaderDictionary headers)
    {
        headers[HeaderNames.Connection] = Constants.Headers.ConnectionUpgrade;
        headers[HeaderNames.Upgrade] = Constants.Headers.UpgradeWebSocket;
        headers[HeaderNames.SecWebSocketAccept] = CreateResponseKey(key);
        
        if (!string.IsNullOrWhiteSpace(subProtocol))
        {
            headers[HeaderNames.SecWebSocketProtocol] = subProtocol;
        }
    }
                    
    public static string CreateResponseKey(string requestKey)
    {
        // "The value of this header field is constructed by concatenating /key/, 
        // defined above in step 4 in Section 4.2.2, with the string 
        // "258EAFA5-E914-47DA-95CA-C5AB0DC85B11", taking the SHA-1 hash of
        // this concatenated value to obtain a 20-byte value and base64-encoding"
        // https://tools.ietf.org/html/rfc6455#section-4.2.2 requestKey is already 
        // verified to be small (24 bytes) by 'IsRequestKeyValid()' and everything 
        // is 1:1 mapping to UTF8 bytes so this can be hardcoded to 60 bytes for the 
        // requestKey + static websocket string
        Span<byte> mergedBytes = stackalloc byte[60];
        Encoding.UTF8.GetBytes(requestKey, mergedBytes);
        EncodedWebSocketKey.CopyTo(mergedBytes.Slice(24));
        
        Span<byte> hashedBytes = stackalloc byte[20];
        var written = SHA1.HashData(mergedBytes, hashedBytes);
        if (written != 20)
        {
            throw new InvalidOperationException(
                "Could not compute the hash for the 'Sec-WebSocket-Accept' header.");
        }
        
        return Convert.ToBase64String(hashedBytes);
    }
}

```

#### 2.2 https redirection

* http -> https 请求

##### 2.2.1 add https redirection

```c#
public static class HttpsRedirectionServicesExtensions
{    
    public static IServiceCollection AddHttpsRedirection(
        this IServiceCollection services, 
        Action<HttpsRedirectionOptions> configureOptions)
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
        return services;
    }
}

```

###### 2.2.1.1 http redirection options

```c#
public class HttpsRedirectionOptions
{    
    public int RedirectStatusCode { get; set; } = StatusCodes.Status307TemporaryRedirect;
        
    // If the HttpsPort is not set, we will try to get the HttpsPort from the following:
    // 1. HTTPS_PORT environment variable
    // 2. IServerAddressesFeature
    // If that fails then the middleware will log a warning and turn off.    
    public int? HttpsPort { get; set; }
}

```

##### 2.2.2 use https redirection

```c#
public static class HttpsPolicyBuilderExtensions
{    
    public static IApplicationBuilder UseHttpsRedirection(this IApplicationBuilder app)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        
        // 解析 server address feature
        var serverAddressFeature = app.ServerFeatures
            						  .Get<IServerAddressesFeature>();
        
        // 注册 https redirection middleware
        if (serverAddressFeature != null)
        {
            app.UseMiddleware<HttpsRedirectionMiddleware>(serverAddressFeature);
        }
        else
        {
            app.UseMiddleware<HttpsRedirectionMiddleware>();
        }
        
        return app;
    }
}

```

###### 2.2.2.1 https redirection middleware

```c#
public class HttpsRedirectionMiddleware
{
    private const int PortNotFound = -1;
    
    private readonly RequestDelegate _next;
    private readonly Lazy<int> _httpsPort;
    private readonly int _statusCode;
    
    private readonly IServerAddressesFeature? _serverAddressesFeature;
    private readonly IConfiguration _config;
    private readonly ILogger _logger;
        
    public HttpsRedirectionMiddleware(
        RequestDelegate next, 
        IOptions<HttpsRedirectionOptions> options, 
        IConfiguration config, 
        ILoggerFactory loggerFactory)        
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        // 解析 https redirection options 中的 value，
        var httpsRedirectionOptions = options.Value;
        
        // 获取 value 中的 https port（没有则创建）
        if (httpsRedirectionOptions.HttpsPort.HasValue)
        {
            _httpsPort = new Lazy<int>(httpsRedirectionOptions.HttpsPort.Value);
        }
        else
        {
            _httpsPort = new Lazy<int>(TryGetHttpsPort);
        }
        
        // status code = redirect status code
        _statusCode = httpsRedirectionOptions.RedirectStatusCode;
        _logger = loggerFactory.CreateLogger<HttpsRedirectionMiddleware>();
    }
        
    public HttpsRedirectionMiddleware(
        RequestDelegate next, 
        IOptions<HttpsRedirectionOptions> options, 
        IConfiguration config, 
        ILoggerFactory loggerFactory,
        IServerAddressesFeature serverAddressesFeature)            
        	: this(
                  next, 
                  options, 
                  config, 
                  loggerFactory)
    {
        _serverAddressesFeature = serverAddressesFeature 
            ?? throw new ArgumentNullException(nameof(serverAddressesFeature));
    }
        
    public Task Invoke(HttpContext context)
    {
        // 如果 request 已经是 https，-> 下一个 middleware
        if (context.Request.IsHttps)
        {
            return _next(context);
        }
        
        // 如果不能解析 port，-> 下一个 middleware
        var port = _httpsPort.Value;
        if (port == PortNotFound)
        {
            return _next(context);
        }
        
        // 创建（新的）host
        var host = context.Request.Host;
        if (port != 443)
        {
            host = new HostString(host.Host, port);
        }
        else
        {
            host = new HostString(host.Host);
        }
        
        // 创建 redirect url
        var request = context.Request;
        var redirectUrl = UriHelper.BuildAbsolute(
            "https", 
            host,
            request.PathBase,
            request.Path,
            request.QueryString);
        
        /* 写回 response */
        
        context.Response
               .StatusCode = _statusCode;
        
        context.Response
               .Headers[HeaderNames.Location] = redirectUrl;
        
        _logger.RedirectingToHttps(redirectUrl);        
        return Task.CompletedTask;
    }
    
    //  Returns PortNotFound (-1) if we were unable to determine the port.
    private int TryGetHttpsPort()
    {
        // The IServerAddressesFeature will not be ready until the middleware is Invoked,
        // Order for finding the HTTPS port:
        // 1. Set in the HttpsRedirectionOptions
        // 2. HTTPS_PORT environment variable
        // 3. IServerAddressesFeature
        // 4. Fail if not sets
        
        var nullablePort = _config.GetValue<int?>("HTTPS_PORT") 
            					  ?? _config
            					  .GetValue<int?>("ANCM_HTTPS_PORT");
        
        if (nullablePort.HasValue)
        {
            var port = nullablePort.Value;
            _logger.PortLoadedFromConfig(port);
            return port;
        }
        
        if (_serverAddressesFeature == null)
        {
            _logger.FailedToDeterminePort();
            return PortNotFound;
        }
        
        foreach (var address in _serverAddressesFeature.Addresses)
        {
            var bindingAddress = BindingAddress.Parse(address);
            if (bindingAddress.Scheme
                			  .Equals(
                                   "https", 
                                   StringComparison.OrdinalIgnoreCase))
            {
                // If we find multiple different https ports specified, throw
                if (nullablePort.HasValue && 
                    nullablePort != bindingAddress.Port)
                {
                    throw new InvalidOperationException(
                        "Cannot determine the https port from IServerAddressesFeature, 
                        "multiple values were found. " +
                        "Set the desired port explicitly on 
                        "HttpsRedirectionOptions.HttpsPort.");
                }
                else
                {
                    nullablePort = bindingAddress.Port;
                }
            }
        }
        
        if (nullablePort.HasValue)
        {
            var port = nullablePort.Value;
            _logger.PortFromServer(port);
            return port;
        }
        
        _logger.FailedToDeterminePort();
        return PortNotFound;
    }
}

```

#### 2.3 hsts 

##### 2.3.1 add hsts

```c#
public static class HstsServicesExtensions
{    
    public static IServiceCollection AddHsts(
        this IServiceCollection services, 
        Action<HstsOptions> configureOptions)
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
        return services;
    }
}

```

###### 2.3.1.1 hsts options

```c#
public class HstsOptions
{    
    // Max-age is required; defaults to 30 days.
    // See: https://tools.ietf.org/html/rfc6797#section-6.1.1    
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(30);        
    // See: https://tools.ietf.org/html/rfc6797#section-6.1.2    
    public bool IncludeSubDomains { get; set; }        
    // Preload is not part of the RFC specification, but is supported by web browsers
    // to preload HSTS sites on fresh install. See https://hstspreload.org/.    
    public bool Preload { get; set; }
        
    public IList<string> ExcludedHosts { get; } = new List<string>
    {
        "localhost",
        "127.0.0.1", 	// ipv4
        "[::1]" 		// ipv6
    };
}

```

##### 2.3.2 use hsts

```c#
public static class HstsBuilderExtensions
{    
    public static IApplicationBuilder UseHsts(this IApplicationBuilder app)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        
        return app.UseMiddleware<HstsMiddleware>();
    }
}

```

###### 2.3.2.1 hsts middleware

```c#
public class HstsMiddleware
{
    private const string IncludeSubDomains = "; includeSubDomains";
    private const string Preload = "; preload";
    
    private readonly RequestDelegate _next;
    private readonly StringValues _strictTransportSecurityValue;
    private readonly IList<string> _excludedHosts;
    private readonly ILogger _logger;
        
    public HstsMiddleware(
        RequestDelegate next, 
        IOptions<HstsOptions> options, 
        ILoggerFactory loggerFactory)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        _next = next ?? throw new ArgumentNullException(nameof(next));
        
        var hstsOptions = options.Value;
        
        var maxAge = 
            Convert.ToInt64(Math.Floor(hstsOptions.MaxAge
                                       			  .TotalSeconds))
            	   .ToString(CultureInfo.InvariantCulture);
        
        var includeSubdomains = 
            hstsOptions.IncludeSubDomains 
            	? IncludeSubDomains 
            	: StringSegment.Empty;
        
        var preload = 
            hstsOptions.Preload 
            	? Preload 
            	: StringSegment.Empty;
        
        _strictTransportSecurityValue = 
            new StringValues($"max-age={maxAge}{includeSubdomains}{preload}");
        
        _excludedHosts = hstsOptions.ExcludedHosts;
        
        _logger = loggerFactory.CreateLogger<HstsMiddleware>();
    }
        
    public HstsMiddleware(RequestDelegate next, IOptions<HstsOptions> options)
            : this(next, options, NullLoggerFactory.Instance) { }
    
    public Task Invoke(HttpContext context)
    {
        // 如果不是 https request，-> 下一个 middleware
        if (!context.Request.IsHttps)
        {
            _logger.SkippingInsecure();
            return _next(context);
        }
        
        // 如果是 excluded host，-> 下一个 middleware
        if (IsHostExcluded(context.Request.Host.Host))
        {
            _logger.SkippingExcludedHost(context.Request.Host.Host);
            return _next(context);
        }
 
        // 写入 response，header[strict transport security, max-age...]
        context.Response
               .Headers[HeaderNames.StrictTransportSecurity] = _strictTransportSecurityValue;
        
        _logger.AddingHstsHeader();
        
        return _next(context);
    }
    
    private bool IsHostExcluded(string host)
    {
        if (_excludedHosts == null)
        {
            return false;
        }
        
        for (var i = 0; i < _excludedHosts.Count; i++)
        {
            if (string.Equals(
                	host, 
                	_excludedHosts[i], 
                	StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        return false;
    }
}

```

#### 2.4 certificate forwarding

##### 2.4.1 add certificate forwarding

```c#
public static IServiceCollection AddCertificateForwarding(
    this IServiceCollection services,
    Action<CertificateForwardingOptions> configure)
{
    if (services == null)
    {
        throw new ArgumentNullException(nameof(services));
    }
    
    if (configure == null)
    {
        throw new ArgumentNullException(nameof(configure));
    }
    
    services.AddOptions<CertificateForwardingOptions>()
        	.Validate(
        		o => !string.IsNullOrEmpty(o.CertificateHeader), 
        		"CertificateForwarderOptions.CertificateHeader cannot be null or empty.");
    
    return services.Configure(configure);
}

```

###### 2.4.1.1 certificate forwarding

```c#
public class CertificateForwardingOptions
{   
    public string CertificateHeader { get; set; } = "X-Client-Cert";            
 
    // This defaults to a conversion from a base64 encoded string.    
    public Func<string, X509Certificate2> HeaderConverter = 
        (headerValue) => new X509Certificate2(Convert.FromBase64String(headerValue));
}

```

##### 2.4.2 use certificate forwarding

```c#
public static class CertificateForwardingBuilderExtensions
{    
    public static IApplicationBuilder UseCertificateForwarding(this IApplicationBuilder app)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        
        return app.UseMiddleware<CertificateForwardingMiddleware>();
    }
}

```

###### 2.4.2.1 certificate forwarding middleware

```c#
public class CertificateForwardingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly CertificateForwardingOptions _options;
    private readonly ILogger _logger;
            
    public CertificateForwardingMiddleware(
        RequestDelegate next,
        ILoggerFactory loggerFactory,
        IOptions<CertificateForwardingOptions> options)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }        
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        _options = options.Value;
        _logger = loggerFactory.CreateLogger<CertificateForwardingMiddleware>();
    }
            
    public Task Invoke(HttpContext httpContext)
    {
        // 解析 header [X-Client-Cert, ...]
        var header = httpContext.Request.Headers[_options.CertificateHeader];
        
        // 如果 header 不为空，
        // 注入 certificate forwarding feature（tls connection feature）
        if (!StringValues.IsNullOrEmpty(header))
        {
            httpContext.Features
                	   .Set<ITlsConnectionFeature>(
                			new CertificateForwardingFeature(_logger, header, _options));
        }
        
        return _next(httpContext);
    }
}

```

#### 2.5 forward header

##### 2.5.1 use forwarded headers

```c#
public static class ForwardedHeadersExtensions
{
    private const string ForwardedHeadersAdded = "ForwardedHeadersAdded";
            
    public static IApplicationBuilder UseForwardedHeaders(this IApplicationBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        // Don't add more than one instance of this middleware to the pipeline 
        // using the options from the DI container.
        // Doing so could cause a request to be processed multiple times and the 
        // ForwardLimit to be exceeded.
        if (!builder.Properties.ContainsKey(ForwardedHeadersAdded))
        {
            builder.Properties[ForwardedHeadersAdded] = true;
            return builder.UseMiddleware<ForwardedHeadersMiddleware>();
        }
        
        return builder;
    }
    
        
    public static IApplicationBuilder UseForwardedHeaders(
        this IApplicationBuilder builder, 
        ForwardedHeadersOptions options)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        return builder.UseMiddleware<ForwardedHeadersMiddleware>(Options.Create(options));
    }
}

```

###### 2.5.1.1 forwarded header options

```c#
public class ForwardedHeadersOptions
{    
    public string ForwardedForHeaderName { get; set; } = 
        ForwardedHeadersDefaults.XForwardedForHeaderName;
        
    public string ForwardedHostHeaderName { get; set; } = 
        ForwardedHeadersDefaults.XForwardedHostHeaderName;
       
    public string ForwardedProtoHeaderName { get; set; } = 
        ForwardedHeadersDefaults.XForwardedProtoHeaderName;
        
    public string OriginalForHeaderName { get; set; } = 
        ForwardedHeadersDefaults.XOriginalForHeaderName;
       
    public string OriginalHostHeaderName { get; set; } = 
        ForwardedHeadersDefaults.XOriginalHostHeaderName;
        
    public string OriginalProtoHeaderName { get; set; } = 
        ForwardedHeadersDefaults.XOriginalProtoHeaderName;
       
    public ForwardedHeaders ForwardedHeaders { get; set; }
        
    public int? ForwardLimit { get; set; } = 1;
       
    public IList<IPAddress> KnownProxies { get; } = 
        new List<IPAddress>() { IPAddress.IPv6Loopback };
        
    public IList<IPNetwork> KnownNetworks { get; } = 
        new List<IPNetwork>() { new IPNetwork(IPAddress.Loopback, 8) };
                
    public IList<string> AllowedHosts { get; set;  } = new List<string>();        
    public bool RequireHeaderSymmetry { get; set; } = false;
}

```

###### 2.5.1.2 forwarded header middleware

```c#
public class ForwardedHeadersMiddleware
{
    private static readonly bool[] HostCharValidity = new bool[127];
    private static readonly bool[] SchemeCharValidity = new bool[123];
    
    private readonly ForwardedHeadersOptions _options;
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;
    private bool _allowAllHosts;
    private IList<StringSegment>? _allowedHosts;
    
    static ForwardedHeadersMiddleware()
    {
        // RFC 3986 scheme = ALPHA * (ALPHA / DIGIT / "+" / "-" / ".")
        SchemeCharValidity['+'] = true;
        SchemeCharValidity['-'] = true;
        SchemeCharValidity['.'] = true;
        
        // Host Matches Http.Sys and Kestrel
        // Host Matches RFC 3986 except "*" / "+" / "," / ";" / "=" and "%" HEXDIG HEXDIG 
        // which are not allowed by Http.Sys
        HostCharValidity['!'] = true;
        HostCharValidity['$'] = true;
        HostCharValidity['&'] = true;
        HostCharValidity['\''] = true;
        HostCharValidity['('] = true;
        HostCharValidity[')'] = true;
        HostCharValidity['-'] = true;
        HostCharValidity['.'] = true;
        HostCharValidity['_'] = true;
        HostCharValidity['~'] = true;
        
        for (var ch = '0'; ch <= '9'; ch++)
        {
            SchemeCharValidity[ch] = true;
            HostCharValidity[ch] = true;
        }
        for (var ch = 'A'; ch <= 'Z'; ch++)
        {
            SchemeCharValidity[ch] = true;
            HostCharValidity[ch] = true;
        }
        for (var ch = 'a'; ch <= 'z'; ch++)
        {
            SchemeCharValidity[ch] = true;
            HostCharValidity[ch] = true;
        }
    }
            
    public ForwardedHeadersMiddleware(
        RequestDelegate next, 
        ILoggerFactory loggerFactory, 
        IOptions<ForwardedHeadersOptions> options)
    {
        if (next == null)
        {
            throw new ArgumentNullException(nameof(next));
        }
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        // Make sure required options is not null or whitespace
        EnsureOptionNotNullorWhitespace(
            options.Value.ForwardedForHeaderName, 
            nameof(options.Value.ForwardedForHeaderName));
        
        EnsureOptionNotNullorWhitespace(
            options.Value.ForwardedHostHeaderName, 
            nameof(options.Value.ForwardedHostHeaderName));
        
        EnsureOptionNotNullorWhitespace(
            options.Value.ForwardedProtoHeaderName, 
            nameof(options.Value.ForwardedProtoHeaderName));
        
        EnsureOptionNotNullorWhitespace(
            options.Value.OriginalForHeaderName, 
            nameof(options.Value.OriginalForHeaderName));
        
        EnsureOptionNotNullorWhitespace(
            options.Value.OriginalHostHeaderName, 
            nameof(options.Value.OriginalHostHeaderName));
        
        EnsureOptionNotNullorWhitespace(
            options.Value.OriginalProtoHeaderName, 
            nameof(options.Value.OriginalProtoHeaderName));
        
        _options = options.Value;
        _logger = loggerFactory.CreateLogger<ForwardedHeadersMiddleware>();
        _next = next;
        
        PreProcessHosts();
    }
    
    private static void EnsureOptionNotNullorWhitespace(
        string value, 
        string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"options.{propertyName} is required", 
                "options");
        }
    }
    
    private void PreProcessHosts()
    {
        if (_options.AllowedHosts == null || 
            _options.AllowedHosts.Count == 0)
        {
            _allowAllHosts = true;
            return;
        }
        
        var allowedHosts = new List<StringSegment>();
        foreach (var entry in _options.AllowedHosts)
        {
            // Punycode. Http.Sys requires you to register Unicode hosts, 
            // but the headers contain punycode.
            var host = new HostString(entry).ToUriComponent();
            
            if (IsTopLevelWildcard(host))
            {
                // Disable filtering
                _allowAllHosts = true;
                return;
            }
            
            if (!allowedHosts.Contains(
                	host, 
                	StringSegmentComparer.OrdinalIgnoreCase))
            {
                allowedHosts.Add(host);
            }
        }
        
        _allowedHosts = allowedHosts;
    }
    
    private bool IsTopLevelWildcard(string host)
    {
        return (
            // HttpSys wildcard
            string.Equals("*", host, StringComparison.Ordinal) || 
            // Kestrel wildcard, IPv6 Any
            string.Equals("[::]", host, StringComparison.Ordinal) || 
            // IPv4 Any
            string.Equals("0.0.0.0", host, StringComparison.Ordinal)); 
    }
            
    public Task Invoke(HttpContext context)
    {
        ApplyForwarders(context);
        return _next(context);
    }
    
        
    public void ApplyForwarders(HttpContext context)
    {
        // Gather expected headers.
        string[]? forwardedFor = null, 
        		  forwardedProto = null, 
        		  forwardedHost = null;
        
        bool checkFor = false, checkProto = false, checkHost = false;
        
        int entryCount = 0;
        
        var request = context.Request;
        var requestHeaders = context.Request.Headers;
        
        if (_options.ForwardedHeaders
            		.HasFlag(ForwardedHeaders.XForwardedFor))
        {
            checkFor = true;
            forwardedFor = 
                requestHeaders.GetCommaSeparatedValues(_options.ForwardedForHeaderName);
            entryCount = Math.Max(forwardedFor.Length, entryCount);
        }
        
        if (_options.ForwardedHeaders
            		.HasFlag(ForwardedHeaders.XForwardedProto))
        {
            checkProto = true;
            forwardedProto = 
                requestHeaders.GetCommaSeparatedValues(_options.ForwardedProtoHeaderName);
            
            if (_options.RequireHeaderSymmetry && 
                checkFor && 
                forwardedFor!.Length != forwardedProto.Length)
            {
                _logger.LogWarning(
                    1, 
                    "Parameter count mismatch between X-Forwarded-For and X-Forwarded-Proto.");
                return;
            }
            entryCount = Math.Max(forwardedProto.Length, entryCount);
        }
        
        if (_options.ForwardedHeaders
            		.HasFlag(ForwardedHeaders.XForwardedHost))
        {
            checkHost = true;
            forwardedHost = 
                requestHeaders.GetCommaSeparatedValues(_options.ForwardedHostHeaderName);
            
            if (_options.RequireHeaderSymmetry && 
                ((checkFor && forwardedFor!.Length != forwardedHost.Length)|| 
                 (checkProto && forwardedProto!.Length != forwardedHost.Length)))
            {
                _logger.LogWarning(
                    1, 
                    "Parameter count mismatch between X-Forwarded-Host and 
                    "X-Forwarded-For or X-Forwarded-Proto.");
                return;
            }
            entryCount = Math.Max(forwardedHost.Length, entryCount);
        }
        
        // Apply ForwardLimit, if any
        if (_options.ForwardLimit.HasValue && 
            entryCount > _options.ForwardLimit)
        {
            entryCount = _options.ForwardLimit.Value;
        }
        
        // Group the data together.
        var sets = new SetOfForwarders[entryCount];
        for (int i = 0; i < sets.Length; i++)
        {
            // They get processed in reverse order, right to left.
            var set = new SetOfForwarders();
            if (checkFor && i < forwardedFor!.Length)
            {
                set.IpAndPortText = forwardedFor[forwardedFor.Length - i - 1];
            }
            if (checkProto && i < forwardedProto!.Length)
            {
                set.Scheme = forwardedProto[forwardedProto.Length - i - 1];
            }
            if (checkHost && i < forwardedHost!.Length)
            {
                set.Host = forwardedHost[forwardedHost.Length - i - 1];
            }
            sets[i] = set;
        }
        
        // Gather initial values
        var connection = context.Connection;
        var currentValues = new SetOfForwarders()
        {
            RemoteIpAndPort = 
                connection.RemoteIpAddress != null 
                	? new IPEndPoint(connection.RemoteIpAddress, connection.RemotePort) 
                	: null,
            // Host and Scheme initial values are never inspected, no need to set them here.
        };
        
        var checkKnownIps = _options.KnownNetworks.Count > 0 || 
            				_options.KnownProxies.Count > 0;
        
        bool applyChanges = false;
        int entriesConsumed = 0;
        
        for (; entriesConsumed < sets.Length; entriesConsumed++)
        {
            var set = sets[entriesConsumed];
            if (checkFor)
            {
                // For the first instance, allow remoteIp to be null for servers that 
                // don't support it natively.
                if (currentValues.RemoteIpAndPort != null && 
                    checkKnownIps && 
                    !CheckKnownAddress(currentValues.RemoteIpAndPort.Address))
                {
                    // Stop at the first unknown remote IP, but still apply changes 
                    // processed so far.
                    _logger.LogDebug(
                        1, 
                        "Unknown proxy: {RemoteIpAndPort}", 
                        currentValues.RemoteIpAndPort);
                    break;
                }
                
                if (IPEndPoint.TryParse(
                    	set.IpAndPortText, 
                    	out var parsedEndPoint))
                {
                    applyChanges = true;
                    set.RemoteIpAndPort = parsedEndPoint;
                    currentValues.IpAndPortText = set.IpAndPortText;
                    currentValues.RemoteIpAndPort = set.RemoteIpAndPort;
                }
                else if (!string.IsNullOrEmpty(set.IpAndPortText))
                {
                    // Stop at the first unparsable IP, but still apply changes 
                    // processed so far.
                    _logger.LogDebug(
                        1, 
                        "Unparsable IP: {IpAndPortText}", 
                        set.IpAndPortText);
                    break;
                }
                else if (_options.RequireHeaderSymmetry)
                {
                    _logger.LogWarning(
                        2, 
                        "Missing forwarded IPAddress.");
                    return;
                }
            }
            
            if (checkProto)
            {
                if (!string.IsNullOrEmpty(set.Scheme) && 
                    TryValidateScheme(set.Scheme))
                {
                    applyChanges = true;
                    currentValues.Scheme = set.Scheme;
                }
                else if (_options.RequireHeaderSymmetry)
                {
                    _logger.LogWarning(
                        3, 
                        $"Forwarded scheme is not present, this is required by 
                        "{nameof(_options.RequireHeaderSymmetry)}");
                    return;
                }
            }
            
            if (checkHost)
            {
                if (!string.IsNullOrEmpty(set.Host) && 
                    TryValidateHost(set.Host) && 
                    (_allowAllHosts || HostString.MatchesAny(set.Host, _allowedHosts!)))
                {
                    applyChanges = true;
                    currentValues.Host = set.Host;
                }
                else if (_options.RequireHeaderSymmetry)
                {
                    _logger.LogWarning(
                        4, 
                        $"Incorrect number of x-forwarded-host header values, 
                        "see {nameof(_options.RequireHeaderSymmetry)}.");
                    return;
                }
            }
        }
        
        if (applyChanges)
        {
            if (checkFor && 
                currentValues.RemoteIpAndPort != null)
            {
                if (connection.RemoteIpAddress != null)
                {
                    // Save the original
                    requestHeaders[_options.OriginalForHeaderName] = 
                        new IPEndPoint(
                        	connection.RemoteIpAddress, 
                        	connection.RemotePort).ToString();
                }
                if (forwardedFor!.Length > entriesConsumed)
                {
                    // Truncate the consumed header values
                    requestHeaders[_options.ForwardedForHeaderName] = 
                        forwardedFor.Take(forwardedFor.Length - entriesConsumed)
                        			.ToArray();
                }
                else
                {
                    // All values were consumed
                    requestHeaders.Remove(_options.ForwardedForHeaderName);
                }
                
                connection.RemoteIpAddress = currentValues.RemoteIpAndPort
                    									  .Address;
                
                connection.RemotePort = currentValues.RemoteIpAndPort
                    								 .Port;
            }
            
            if (checkProto && 
                currentValues.Scheme != null)
            {
                // Save the original
                requestHeaders[_options.OriginalProtoHeaderName] = request.Scheme;
                
                if (forwardedProto!.Length > entriesConsumed)
                {
                    // Truncate the consumed header values
                    requestHeaders[_options.ForwardedProtoHeaderName] = 
                        forwardedProto.Take(forwardedProto.Length - entriesConsumed)
                        			  .ToArray();
                }
                else
                {
                    // All values were consumed
                    requestHeaders.Remove(_options.ForwardedProtoHeaderName);
                }
                
                request.Scheme = currentValues.Scheme;
            }
            
            if (checkHost && 
                currentValues.Host != null)
            {
                // Save the original
                requestHeaders[_options.OriginalHostHeaderName] = request.Host.ToString();
                if (forwardedHost!.Length > entriesConsumed)
                {
                    // Truncate the consumed header values
                    requestHeaders[_options.ForwardedHostHeaderName] = 
                        forwardedHost.Take(forwardedHost.Length - entriesConsumed)
                        			 .ToArray();
                }
                else
                {
                    // All values were consumed
                    requestHeaders.Remove(_options.ForwardedHostHeaderName);
                }
                request.Host = HostString.FromUriComponent(currentValues.Host);
            }
        }
    }
    
    private bool CheckKnownAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            var ipv4Address = address.MapToIPv4();
            if (CheckKnownAddress(ipv4Address))
            {
                return true;
            }
        }
        if (_options.KnownProxies.Contains(address))
        {
            return true;
        }
        foreach (var network in _options.KnownNetworks)
        {
            if (network.Contains(address))
            {
                return true;
            }
        }
        return false;
    }
    
    private struct SetOfForwarders
    {
        public string IpAndPortText;
        public IPEndPoint? RemoteIpAndPort;
        public string Host;
        public string Scheme;
    }
    
    // Empty was checked for by the caller
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryValidateScheme(string scheme)
    {
        for (var i = 0; i < scheme.Length; i++)
        {
            if (!IsValidSchemeChar(scheme[i]))
            {
                return false;
            }
        }
        return true;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidSchemeChar(char ch)
    {
        return ch < SchemeCharValidity.Length && SchemeCharValidity[ch];
    }
    
    // Empty was checked for by the caller
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryValidateHost(string host)
    {
        if (host[0] == '[')
        {
            return TryValidateIPv6Host(host);
        }
        
        if (host[0] == ':')
        {
            // Only a port
            return false;
        }
        
        var i = 0;
        for (; i < host.Length; i++)
        {
            if (!IsValidHostChar(host[i]))
            {
                break;
            }
        }
        return TryValidateHostPort(host, i);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidHostChar(char ch)
    {
        return ch < HostCharValidity.Length && HostCharValidity[ch];
    }
    
    // The lead '[' was already checked
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryValidateIPv6Host(string hostText)
    {
        for (var i = 1; i < hostText.Length; i++)
        {
            var ch = hostText[i];
            if (ch == ']')
            {
                // [::1] is the shortest valid IPv6 host
                if (i < 4)
                {
                    return false;
                }
                return TryValidateHostPort(hostText, i + 1);
            }
            
            if (!IsHex(ch) && ch != ':' && ch != '.')
            {
                return false;
            }
        }
        
        // Must contain a ']'
        return false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryValidateHostPort(string hostText, int offset)
    {
        if (offset == hostText.Length)
        {
            // No port
            return true;
        }
        
        if (hostText[offset] != ':' || hostText.Length == offset + 1)
        {
            // Must have at least one number after the colon if present.
            return false;
        }
        
        for (var i = offset + 1; i < hostText.Length; i++)
        {
            if (!IsNumeric(hostText[i]))
            {
                return false;
            }
        }
        
        return true;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsNumeric(char ch)
    {
        return '0' <= ch && ch <= '9';
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsHex(char ch)
    {
        return IsNumeric(ch) || 
               ('a' <= ch && ch <= 'f') || 
               ('A' <= ch && ch <= 'F');
    }
}

```

#### 2.6 http method override

##### 2.6.1 use http method override

```c#
public static class HttpMethodOverrideExtensions
{    
    // Allows incoming POST request to override method type with type specified in header. 
    // This middleware is used when a client is limited to sending GET or POST methods but 
    // wants to invoke other HTTP methods.
    /// By default, the X-HTTP-Method-Override request header is used to specify the HTTP 
    // method being tunneled.
    
    public static IApplicationBuilder UseHttpMethodOverride(this IApplicationBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        return builder.UseMiddleware<HttpMethodOverrideMiddleware>();
    }

        
    public static IApplicationBuilder UseHttpMethodOverride(
        this IApplicationBuilder builder, 
        HttpMethodOverrideOptions options)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        return builder.UseMiddleware<HttpMethodOverrideMiddleware>(Options.Create(options));
    }
}

```

###### 2.6.1.1 http method override options

```c#
public class HttpMethodOverrideOptions
{    
    public string? FormFieldName { get; set; }
}

```

###### 2.6.1.2 http method override middleware

```c#
public class HttpMethodOverrideMiddleware
{
    private const string xHttpMethodOverride = "X-Http-Method-Override";
    private readonly RequestDelegate _next;
    private readonly HttpMethodOverrideOptions _options;
        
    public HttpMethodOverrideMiddleware(
        RequestDelegate next, 
        IOptions<HttpMethodOverrideOptions> options)
    {
        if (next == null)
        {
            throw new ArgumentNullException(nameof(next));
        }
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        _next = next;
        _options = options.Value;
    }
       
    public async Task Invoke(HttpContext context)
    {
        if (HttpMethods.IsPost(context.Request.Method))
        {
            if (_options.FormFieldName != null)
            {
                if (context.Request.HasFormContentType)
                {
                    var form = await context.Request
                        					.ReadFormAsync();
                    
                    var methodType = form[_options.FormFieldName];
                    if (!string.IsNullOrEmpty(methodType))
                    {
                        context.Request.Method = methodType;
                    }
                }
            }
            else
            {
                var xHttpMethodOverrideValue = context.Request
                    								  .Headers[xHttpMethodOverride];
                if (!string.IsNullOrEmpty(xHttpMethodOverrideValue))
                {
                    context.Request.Method = xHttpMethodOverrideValue;
                }
            }
        }
        await _next(context);
    }
}

```

#### 2.7 web encoder

##### 2.7.1 add web encoder

```c#
public static class EncoderServiceCollectionExtensions
{    
    public static IServiceCollection AddWebEncoders(this IServiceCollection services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        services.AddOptions();
        
        // Register the default encoders
        // We want to call the 'Default' property getters lazily since they perform 
        // static caching
        services.TryAddSingleton(
                CreateFactory(
                    () => HtmlEncoder.Default, 
                    settings => HtmlEncoder.Create(settings)));
        
        services.TryAddSingleton(
                CreateFactory(
                    () => JavaScriptEncoder.Default, 
                    settings => JavaScriptEncoder.Create(settings)));
        
        services.TryAddSingleton(
                CreateFactory(
                    () => UrlEncoder.Default, 
                    settings => UrlEncoder.Create(settings)));
        
        return services;
    }
        
    public static IServiceCollection AddWebEncoders(
        this IServiceCollection services, 
        Action<WebEncoderOptions> setupAction)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }        
        if (setupAction == null)
        {
            throw new ArgumentNullException(nameof(setupAction));
        }
        
        services.AddWebEncoders();
        services.Configure(setupAction);
        
        return services;
    }
    
    private static Func<IServiceProvider, TService> CreateFactory<TService>(
        Func<TService> defaultFactory,
        Func<TextEncoderSettings, TService> customSettingsFactory)
    {
        return serviceProvider =>
        {
            var settings = serviceProvider?.GetService<IOptions<WebEncoderOptions>>()
						                  ?.Value
						                  ?.TextEncoderSettings;
            
            return (settings != null) 
                ? customSettingsFactory(settings) 
                : defaultFactory();
        };
    }
}

```

###### 2.7.1.1 web encoder options

```c#
public sealed class WebEncoderOptions
{    
    public TextEncoderSettings? TextEncoderSettings { get; set; }
}

```

#### 2.8 session

##### 2.8.1 session

###### 2.8.1.1 接口

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

###### 2.8.1.2 distributed session

```c#
public class DistributedSession : ISession
{
    private const int IdByteCount = 16;    
    private const byte SerializationRevision = 2;
    private const int KeyLengthLimit = ushort.MaxValue;
    
    private readonly IDistributedCache _cache;
    
    private readonly string _sessionKey;
    private readonly TimeSpan _idleTimeout;
    private readonly TimeSpan _ioTimeout;
    private readonly Func<bool> _tryEstablishSession;
    private readonly ILogger _logger;
    
    private IDistributedSessionStore _store;
    public IEnumerable<string> Keys
    {
        get
        {
            Load();
            return _store.Keys.Select(key => key.KeyString);
        }
    }
    
    private bool _isModified;
    private bool _loaded;
    
    private bool _isAvailable;
    public bool IsAvailable
    {
        get
        {
            Load();
            return _isAvailable;
        }
    }
    
    private bool _isNewSessionKey;
    
    private string? _sessionId;
    public string Id
    {
        get
        {
            Load();
            if (_sessionId == null)
            {
                _sessionId = new Guid(IdBytes).ToString();
            }
            return _sessionId;
        }
    }
    
    private byte[]? _sessionIdBytes;
    private byte[] IdBytes
    {
        get
        {
            Load();
            if (_sessionIdBytes == null)
            {
                _sessionIdBytes = new byte[IdByteCount];
                RandomNumberGenerator.Fill(_sessionIdBytes);
            }
            return _sessionIdBytes;
        }
    }
        
    public DistributedSession(
        IDistributedCache cache,
        string sessionKey,
        TimeSpan idleTimeout,
        TimeSpan ioTimeout,
        Func<bool> tryEstablishSession,
        ILoggerFactory loggerFactory,
        bool isNewSessionKey)
    {
        if (cache == null)
        {
            throw new ArgumentNullException(nameof(cache));
        }        
        if (string.IsNullOrEmpty(sessionKey))
        {
            throw new ArgumentException(
                Resources.ArgumentCannotBeNullOrEmpty, 
                nameof(sessionKey));
        }        
        if (tryEstablishSession == null)
        {
            throw new ArgumentNullException(nameof(tryEstablishSession));
        }
        
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }
        
        _cache = cache;
        _sessionKey = sessionKey;
        _idleTimeout = idleTimeout;
        _ioTimeout = ioTimeout;
        _tryEstablishSession = tryEstablishSession;
        
        // When using a NoOpSessionStore, using a dictionary as a backing store results 
        // in problematic API choices particularly with nullability.
        // We instead use a more limited contract - `IDistributedSessionStore` as the 
        // backing store that plays better.
        _store = new DefaultDistributedSessionStore();
        _logger = loggerFactory.CreateLogger<DistributedSession>();
        _isNewSessionKey = isNewSessionKey;
    }
        
    public bool TryGetValue(
        string key, 
        [NotNullWhen(true)] out byte[]? value)
    {
        Load();
        return _store.TryGetValue(new EncodedKey(key), out value);
    }
            
    /// <inheritdoc />
    public void Set(string key, byte[] value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        
        if (IsAvailable)
        {
            // 创建 encoded key
            var encodedKey = new EncodedKey(key);
            
            if (encodedKey.KeyBytes.Length > KeyLengthLimit)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(key),
                    Resources.FormatException_KeyLengthIsExceeded(KeyLengthLimit));
            }
            
            if (!_tryEstablishSession())
            {
                throw new InvalidOperationException(
                    Resources.Exception_InvalidSessionEstablishment);
            }
            
            _isModified = true;
            
            byte[] copy = new byte[value.Length];
            Buffer.BlockCopy(
                src: value, 
                srcOffset: 0, 
                dst: copy, 
                dstOffset: 0, 
                count: value.Length);
            
            _store.SetValue(encodedKey, copy);
        }
    }
    
    /// <inheritdoc />
    public void Remove(string key)
    {
        Load();
        _isModified |= _store.Remove(new EncodedKey(key));
    }
    
    /// <inheritdoc />
    public void Clear()
    {
        Load();
        _isModified |= _store.Count > 0;
        _store.Clear();
    }
    
    private void Load()
    {
        if (!_loaded)
        {
            try
            {
                var data = _cache.Get(_sessionKey);
                if (data != null)
                {
                    Deserialize(new MemoryStream(data));
                }
                else if (!_isNewSessionKey)
                {
                    _logger.AccessingExpiredSession(_sessionKey);
                }
                _isAvailable = true;
            }
            catch (Exception exception)
            {
                _logger.SessionCacheReadException(_sessionKey, exception);
                _isAvailable = false;
                _sessionId = string.Empty;
                _sessionIdBytes = null;
                _store = new NoOpSessionStore();
            }
            finally
            {
                _loaded = true;
            }
        }
    }
    
    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        // This will throw if called directly and a failure occurs. 
        // The user is expected to handle the failures.
        if (!_loaded)
        {
            using (var timeout = new CancellationTokenSource(_ioTimeout))
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(
                    timeout.Token, 
                    cancellationToken);
                try
                {
                    cts.Token
                       .ThrowIfCancellationRequested();
                    
                    var data = await _cache.GetAsync(_sessionKey, cts.Token);
                    if (data != null)
                    {
                        Deserialize(new MemoryStream(data));
                    }
                    else if (!_isNewSessionKey)
                    {
                        _logger.AccessingExpiredSession(_sessionKey);
                    }
                }
                catch (OperationCanceledException oex)
                {
                    if (timeout.Token.IsCancellationRequested)
                    {
                        _logger.SessionLoadingTimeout();
                        throw new OperationCanceledException(
                            "Timed out loading the session.", 
                            oex, 
                            timeout.Token);
                    }
                    throw;
                }
            }
            _isAvailable = true;
            _loaded = true;
        }
    }
    
    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            _logger.SessionNotAvailable();
            return;
        }
        
        using (var timeout = new CancellationTokenSource(_ioTimeout))
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(
                timeout.Token, 
                cancellationToken);
            
            if (_isModified)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    // This operation is only so we can log if the session already existed.
                    // Log and ignore failures.
                    try
                    {
                        cts.Token
                           .ThrowIfCancellationRequested();
                                                
                        var data = await _cache.GetAsync(_sessionKey, cts.Token);
                        if (data == null)
                        {
                            _logger.SessionStarted(_sessionKey, Id);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception exception)
                    {
                        _logger.SessionCacheReadException(_sessionKey, exception);
                    }
                }
                
                var stream = new MemoryStream();
                Serialize(stream);
                
                try
                {
                    cts.Token
                       .ThrowIfCancellationRequested();
                    
                    await _cache.SetAsync(
                        _sessionKey,
                        stream.ToArray(),
                        new DistributedCacheEntryOptions().SetSlidingExpiration(_idleTimeout),
                        cts.Token);
                    
                    _isModified = false;
                    _logger.SessionStored(_sessionKey, Id, _store.Count);
                }
                catch (OperationCanceledException oex)
                {
                    if (timeout.Token.IsCancellationRequested)
                    {
                        _logger.SessionCommitTimeout();
                        
                        throw new OperationCanceledException(
                            "Timed out committing the session.", 
                            oex, 
                            timeout.Token);
                    }
                    
                    throw;
                }
            }
            else
            {
                try
                {
                    await _cache.RefreshAsync(_sessionKey, cts.Token);
                }
                catch (OperationCanceledException oex)
                {
                    if (timeout.Token.IsCancellationRequested)
                    {
                        _logger.SessionRefreshTimeout();
                        
                        throw new OperationCanceledException(
                            "Timed out refreshing the session.", 
                            oex, 
                            timeout.Token);
                    }
                    
                    throw;
                }
            }
        }
    }
    
    // Format:
    // Serialization revision: 1 byte, range 0-255
    // Entry count: 3 bytes, range 0-16,777,215
    // SessionId: IdByteCount bytes (16)
    // foreach entry:
    //   key name byte length: 2 bytes, range 0-65,535
    //   UTF-8 encoded key name byte[]
    //   data byte length: 4 bytes, range 0-2,147,483,647
    //   data byte[]
    private void Serialize(Stream output)
    {
        output.WriteByte(SerializationRevision);
        SerializeNumAs3Bytes(output, _store.Count);
        output.Write(IdBytes, 0, IdByteCount);
        
        foreach (var entry in _store)
        {
            var keyBytes = entry.Key.KeyBytes;
            SerializeNumAs2Bytes(output, keyBytes.Length);
            output.Write(keyBytes, 0, keyBytes.Length);
            SerializeNumAs4Bytes(output, entry.Value.Length);
            output.Write(entry.Value, 0, entry.Value.Length);
        }
    }
    
    private void Deserialize(Stream content)
    {
        if (content == null || 
            content.ReadByte() != SerializationRevision)
        {
            // Replace the un-readable format.
            _isModified = true;
            return;
        }
        
        int expectedEntries = DeserializeNumFrom3Bytes(content);
        _sessionIdBytes = ReadBytes(content, IdByteCount);
        
        for (int i = 0; i < expectedEntries; i++)
        {
            int keyLength = DeserializeNumFrom2Bytes(content);
            var key = new EncodedKey(ReadBytes(content, keyLength));
            int dataLength = DeserializeNumFrom4Bytes(content);
            _store.SetValue(key, ReadBytes(content, dataLength));
        }
        
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _sessionId = new Guid(_sessionIdBytes).ToString();
            _logger.SessionLoaded(_sessionKey, _sessionId, expectedEntries);
        }
    }
    
    private void SerializeNumAs2Bytes(Stream output, int num)
    {
        if (num < 0 || ushort.MaxValue < num)
        {
            throw new ArgumentOutOfRangeException(
                nameof(num), 
                Resources.Exception_InvalidToSerializeIn2Bytes);
        }
        output.WriteByte((byte)(num >> 8));
        output.WriteByte((byte)(0xFF & num));
    }
    
    private int DeserializeNumFrom2Bytes(Stream content)
    {
        return content.ReadByte() << 8 | content.ReadByte();
    }
    
    private void SerializeNumAs3Bytes(Stream output, int num)
    {
        if (num < 0 || 0xFFFFFF < num)
        {
            throw new ArgumentOutOfRangeException(
                nameof(num), 
                Resources.Exception_InvalidToSerializeIn3Bytes);
        }
        output.WriteByte((byte)(num >> 16));
        output.WriteByte((byte)(0xFF & (num >> 8)));
        output.WriteByte((byte)(0xFF & num));
    }
    
    private int DeserializeNumFrom3Bytes(Stream content)
    {
        return content.ReadByte() << 16 | 
               content.ReadByte() << 8 | 
               content.ReadByte();
    }
    
    private void SerializeNumAs4Bytes(Stream output, int num)
    {
        if (num < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(num), 
                Resources.Exception_NumberShouldNotBeNegative);
        }
        output.WriteByte((byte)(num >> 24));
        output.WriteByte((byte)(0xFF & (num >> 16)));
        output.WriteByte((byte)(0xFF & (num >> 8)));
        output.WriteByte((byte)(0xFF & num));
    }
    
    private int DeserializeNumFrom4Bytes(Stream content)
    {
        return content.ReadByte() << 24 | 
               content.ReadByte() << 16 | 
               content.ReadByte() << 8 | 
               content.ReadByte();
    }
    
    private byte[] ReadBytes(Stream stream, int count)
    {
        var output = new byte[count];
        int total = 0;
        while (total < count)
        {
            var read = stream.Read(output, total, count - total);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }
            total += read;
        }
        return output;
    }
}

```

###### 2.8.1.3 encoded key

```c#
internal class EncodedKey
{
    private int? _hashCode;
    
    private string? _keyString;
    internal string KeyString
    {
        get
        {
            if (_keyString == null)
            {
                _keyString = Encoding.UTF8.GetString(KeyBytes, 0, KeyBytes.Length);
            }
            return _keyString;
        }
    }
    
    internal byte[] KeyBytes { get; private set; }
        
    internal EncodedKey(string key)
    {
        _keyString = key;
        KeyBytes = Encoding.UTF8.GetBytes(key);
    }
    
    public EncodedKey(byte[] key)
    {
        KeyBytes = key;
    }
                    
    public override bool Equals(object? obj)
    {
        var otherKey = obj as EncodedKey;
        if (otherKey == null)
        {
            return false;
        }
        if (KeyBytes.Length != otherKey.KeyBytes.Length)
        {
            return false;
        }
        if (_hashCode.HasValue && 
            otherKey._hashCode.HasValue
            && _hashCode.Value != otherKey._hashCode.Value)
        {
            return false;
        }
        for (int i = 0; i < KeyBytes.Length; i++)
        {
            if (KeyBytes[i] != otherKey.KeyBytes[i])
            {
                return false;
            }
        }
        return true;
    }
    
    public override int GetHashCode()
    {
        if (!_hashCode.HasValue)
        {
            _hashCode = SipHash.GetHashCode(KeyBytes);
        }
        return _hashCode.Value;
    }
    
    public override string ToString()
    {
        return KeyString;
    }
}

```

##### 2.8.2 session store

###### 2.8.2.1 session store 接口

```c#
public interface ISessionStore
{    
    ISession Create(
        string sessionKey, 
        TimeSpan idleTimeout, 
        TimeSpan ioTimeout, 
        Func<bool> tryEstablishSession, 
        bool isNewSessionKey);
}

```

###### 2.8.2.2 distributed session store

```c#
public class DistributedSessionStore : ISessionStore
{
    private readonly IDistributedCache _cache;
    private readonly ILoggerFactory _loggerFactory;
    
    
    public DistributedSessionStore(
        IDistributedCache cache, 
        ILoggerFactory loggerFactory)
    {
        if (cache == null)
        {
            throw new ArgumentNullException(nameof(cache));
        }        
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }
        
        _cache = cache;
        _loggerFactory = loggerFactory;
    }
    
    /// <inheritdoc />
    public ISession Create(
        string sessionKey, 
        TimeSpan idleTimeout, 
        TimeSpan ioTimeout, 
        Func<bool> tryEstablishSession, 
        bool isNewSessionKey)
    {
        if (string.IsNullOrEmpty(sessionKey))
        {
            throw new ArgumentException(
                Resources.ArgumentCannotBeNullOrEmpty, 
                nameof(sessionKey));
        }
        
        if (tryEstablishSession == null)
        {
            throw new ArgumentNullException(nameof(tryEstablishSession));
        }
        
        return new DistributedSession(
            _cache, 
            sessionKey, 
            idleTimeout, 
            ioTimeout, 
            tryEstablishSession, 
            _loggerFactory, 
            isNewSessionKey);
    }
}

```

##### 2.8.3 distributed session store

###### 2.8.3.1 接口

```c#
internal interface IDistributedSessionStore : IEnumerable<KeyValuePair<EncodedKey, byte[]>>
{
    int Count { get; }    
    ICollection<EncodedKey> Keys { get; }
    
    bool TryGetValue(EncodedKey key, [MaybeNullWhen(false)] out byte[] value);    
    void SetValue(EncodedKey key, byte[] value);    
    bool Remove(EncodedKey encodedKey);    
    void Clear();
}

```

###### 2.8.3.2 default distributed session store

```c#
internal sealed class DefaultDistributedSessionStore : IDistributedSessionStore
{
    private readonly Dictionary<EncodedKey, byte[]> _store = 
        new Dictionary<EncodedKey, byte[]>();
    
    public int Count => _store.Count;    
    public ICollection<EncodedKey> Keys => _store.Keys;
    
    public bool TryGetValue(EncodedKey key, [MaybeNullWhen(false)] out byte[] value)
            => _store.TryGetValue(key, out value);

    public void SetValue(EncodedKey key, byte[] value) => _store[key] = value;
    
    public bool Remove(EncodedKey encodedKey)
        => _store.Remove(encodedKey);
    
    public void Clear()
        => _store.Clear();
    
    /* enumerator 方法 */
    public IEnumerator<KeyValuePair<EncodedKey, byte[]>> GetEnumerator()
            => _store.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

```

###### 2.8.3.3 no op session store

```c#
internal class NoOpSessionStore : IDistributedSessionStore
{
    public void SetValue(EncodedKey key, byte[] value)
    {
    }
    
    public int Count => 0;    
    public bool IsReadOnly { get; }    
    public ICollection<EncodedKey> Keys { get; } = Array.Empty<EncodedKey>();    
    public ICollection<byte[]> Values { get; } = new byte[0][];
    
    public void Clear() { }
            
    public bool Remove(EncodedKey key) => false;
    
    public bool TryGetValue(EncodedKey key, [MaybeNullWhen(false)] out byte[] value)
    {
        value = null;
        return false;
    }
    
    /* enumerator 方法 */
    public IEnumerator<KeyValuePair<EncodedKey, byte[]>> GetEnumerator() => 
        Enumerable.Empty<KeyValuePair<EncodedKey, byte[]>>()
        		  .GetEnumerator();
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

```

##### 2.8.4 add session

* 注入 session 所需的服务

```c#
public static class SessionServiceCollectionExtensions
{    
    public static IServiceCollection AddSession(this IServiceCollection services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        services.TryAddTransient<ISessionStore, DistributedSessionStore>();
        services.AddDataProtection();
        
        return services;
    }
        
    public static IServiceCollection AddSession(
        this IServiceCollection services, 
        Action<SessionOptions> configure)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }        
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }
        
        services.Configure(configure);
        services.AddSession();
        
        return services;
    }
}

```

###### 2.8.4.1 session options

```c#
public class SessionOptions
{
    private CookieBuilder _cookieBuilder = new SessionCookieBuilder();            
    public CookieBuilder Cookie
    {
        get => _cookieBuilder;
        set => _cookieBuilder = value ?? throw new ArgumentNullException(nameof(value));
    }
            
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(20);           
    public TimeSpan IOTimeout { get; set; } = TimeSpan.FromMinutes(1);
    
    private class SessionCookieBuilder : CookieBuilder
    {
        public SessionCookieBuilder()
        {
            Name = SessionDefaults.CookieName;
            Path = SessionDefaults.CookiePath;
            SecurePolicy = CookieSecurePolicy.None;
            SameSite = SameSiteMode.Lax;
            HttpOnly = true;
            // Session is considered non-essential as it's designed for ephemeral data.
            IsEssential = false;
        }
        
        public override TimeSpan? Expiration
        {
            get => null;
            set => throw new InvalidOperationException(
                nameof(Expiration) + 
                " cannot be set for the cookie defined by " + 
                nameof(SessionOptions));
        }
    }
}

```

###### 2.8.4.2 session default

```c#
public static class SessionDefaults
{    
    public static readonly string CookieName = ".AspNetCore.Session";        
    public static readonly string CookiePath = "/";
}

```

##### 2.8.5 use session

```c#
public static class SessionMiddlewareExtensions
{    
    public static IApplicationBuilder UseSession(this IApplicationBuilder app)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        
        return app.UseMiddleware<SessionMiddleware>();
    }
        
    public static IApplicationBuilder UseSession(
        this IApplicationBuilder app, 
        SessionOptions options)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        return app.UseMiddleware<SessionMiddleware>(Options.Create(options));
    }
}

```

###### 2.8.5.1 session middleware

```c#
public class SessionMiddleware
{
    private const int SessionKeyLength = 36; // "382c74c3-721d-4f34-80e5-57657b6cbc27"
    private static readonly Func<bool> ReturnTrue = () => true;
    
    private readonly RequestDelegate _next;
    private readonly SessionOptions _options;
    private readonly ILogger _logger;
    private readonly ISessionStore _sessionStore;
    private readonly IDataProtector _dataProtector;
        
    public SessionMiddleware(
        RequestDelegate next,
        ILoggerFactory loggerFactory,
        IDataProtectionProvider dataProtectionProvider,
        ISessionStore sessionStore,
        IOptions<SessionOptions> options)
    {
        if (next == null)
        {
            throw new ArgumentNullException(nameof(next));
        }        
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }        
        if (dataProtectionProvider == null)
        {
            throw new ArgumentNullException(nameof(dataProtectionProvider));
        }        
        if (sessionStore == null)
        {
            throw new ArgumentNullException(nameof(sessionStore));
        }        
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        _next = next;
        logger = loggerFactory.CreateLogger<SessionMiddleware>();
        
        // 创建 data protector
        _dataProtector = dataProtectionProvider.CreateProtector(nameof(SessionMiddleware));
        // 注入 session options
        _options = options.Value;
        // 注入 session store
        _sessionStore = sessionStore;
    }
        
    public async Task Invoke(HttpContext context)
    {
        var isNewSessionKey = false;
        Func<bool> tryEstablishSession = ReturnTrue;
        
        var cookieValue = 
            context.Request
            	   .Cookies[_options.Cookie.Name!];
        var sessionKey = 
            CookieProtection.Unprotect(
	            _dataProtector, 
    	        cookieValue, 
        	    _logger);
        
        if (string.IsNullOrWhiteSpace(sessionKey) || 
            sessionKey.Length != SessionKeyLength)
        {
            // No valid cookie, new session.
            var guidBytes = new byte[16];
            RandomNumberGenerator.Fill(guidBytes);
            sessionKey = new Guid(guidBytes).ToString();
            cookieValue = CookieProtection.Protect(_dataProtector, sessionKey);
            
            // 判断 session 已经创建（flag）
            var establisher = new SessionEstablisher(context, cookieValue, _options);
            tryEstablishSession = establisher.TryEstablishSession;
            
            // 标记 new session 
            isNewSessionKey = true;
        }
        
        // 创建 session，注入 session feature，
        var feature = new SessionFeature();
        feature.Session = _sessionStore.Create(
            sessionKey, 
            _options.IdleTimeout, 
            _options.IOTimeout, 
            tryEstablishSession, 
            isNewSessionKey);
        
        // 注入 httpcontext
        context.Features.Set<ISessionFeature>(feature);
        
        try
        {
            await _next(context);
        }
        finally
        {
            context.Features.Set<ISessionFeature?>(null);
            
            if (feature.Session != null)
            {
                try
                {
                    await feature.Session.CommitAsync();
                }
                catch (OperationCanceledException)
                {
                    _logger.SessionCommitCanceled();
                }
                catch (Exception ex)
                {
                    _logger.ErrorClosingTheSession(ex);
                }
            }
        }
    }
    
    private class SessionEstablisher
    {
        private readonly HttpContext _context;
        private readonly string _cookieValue;
        private readonly SessionOptions _options;
        private bool _shouldEstablishSession;
        
        public SessionEstablisher(
            ttpContext context, 
            string cookieValue, 
            SessionOptions options)
        {
            _context = context;
            _cookieValue = cookieValue;
            _options = options;
            
            context.Response.OnStarting(OnStartingCallback, state: this);
        }
        
        private static Task OnStartingCallback(object state)
        {
            var establisher = (SessionEstablisher)state;
            if (establisher._shouldEstablishSession)
            {
                establisher.SetCookie();
            }
            return Task.CompletedTask;
        }
        
        private void SetCookie()
        {
            var cookieOptions = _options.Cookie.Build(_context);
            
            var response = _context.Response;
            response.Cookies.Append(_options.Cookie.Name!, _cookieValue, cookieOptions);
            
            var responseHeaders = response.Headers;
            responseHeaders[HeaderNames.CacheControl] = "no-cache,no-store";
            responseHeaders[HeaderNames.Pragma] = "no-cache";
            responseHeaders[HeaderNames.Expires] = "-1";
        }
        
        // Returns true if the session has already been established, or if it still 
        // can be because the response has not been sent.
        internal bool TryEstablishSession()
        {
            return (_shouldEstablishSession |= !_context.Response.HasStarted);
        }
    }
}

```

###### 2.8.5.2 session feature?

```c#
public class SessionFeature : ISessionFeature
{
    /// <inheritdoc />
    public ISession Session { get; set; } = default!;
}

```

###### 2.8.5.3 cookie protection

```c#
internal static class CookieProtection
{
    internal static string Protect(
        IDataProtector protector, 
        string data)
    {
        if (protector == null)
        {
            throw new ArgumentNullException(nameof(protector));
        }
        
        if (string.IsNullOrEmpty(data))
        {
            return data;
        }
        
        // encode to utf8
        var userData = Encoding.UTF8.GetBytes(data);        
        // encode by data protector
        var protectedData = protector.Protect(userData);
        // convert to base 64
        return Convert.ToBase64String(protectedData).TrimEnd('=');
    }
    
    internal static string Unprotect(
        IDataProtector protector, 
        string? protectedText, 
        ILogger logger)
    {
        try
        {
            if (string.IsNullOrEmpty(protectedText))
            {
                return string.Empty;
            }
            
            var protectedData = Convert.FromBase64String(Pad(protectedText));
            if (protectedData == null)
            {
                return string.Empty;
            }
            
            var userData = protector.Unprotect(protectedData);
            if (userData == null)
            {
                return string.Empty;
            }
            
            return Encoding.UTF8.GetString(userData);
        }
        catch (Exception ex)
        {
            // Log the exception, but do not leak other information
            logger.ErrorUnprotectingSessionCookie(ex);
            return string.Empty;
        }
    }
    
    private static string Pad(string text)
    {
        var padding = 3 - ((text.Length + 3) % 4);
        if (padding == 0)
        {
            return text;
        }
        return text + new string('=', padding);
    }
}

```

#### 2.9 host filtering

##### 2.9.1 add host filtering

```c#
public static class HostFilteringServicesExtensions
{    
    public static IServiceCollection AddHostFiltering(
        this IServiceCollection services, 
        Action<HostFilteringOptions> configureOptions)
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
        return services;
    }
}

```

###### 2.9.1.1 host filtering options

```c#
public class HostFilteringOptions
{
    // Port numbers must be excluded.</description></item>
    // A top level wildcard "*" allows all non-empty hosts.
    // Subdomain wildcards are permitted. E.g. "*.example.com" matches subdomains like 
    // foo.example.com, but not the parent domain example.com.
    // Unicode host names are allowed but will be converted to punycode for matching.
    /// IPv6 addresses must include their bounding brackets and be in their normalized form.
    public IList<string> AllowedHosts { get; set; } = new List<string>();
        
    // HTTP/1.0 does not require a host header.
    // Http/1.1 requires a host header, but says the value may be empty.    
    public bool AllowEmptyHosts { get; set; } = true;
    
    // 标记是否 inclue failure message，
    // 即 host 不是 allowed 时返回 default html
    public bool IncludeFailureMessage { get; set; } = true;
}

```

##### 2.9.2 use host fitering

```c#
public static class HostFilteringBuilderExtensions
{    
    public static IApplicationBuilder UseHostFiltering(this IApplicationBuilder app)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        
        app.UseMiddleware<HostFilteringMiddleware>();
        
        return app;
    }
}

```

###### 2.9.2.1 host filtering middleware

```c#
public class HostFilteringMiddleware
{
    // Matches Http.Sys.
    private static readonly byte[] DefaultResponse = 
        Encoding.ASCII.GetBytes(
 	       "<!DOCTYPE HTML PUBLIC \"-//W3C//DTD HTML 4.01//EN\"\"http://www.w3.org/TR/html4/strict.dtd\">\r\n" + 
        	"<HTML><HEAD><TITLE>Bad Request</TITLE>\r\n"  + 
        "<META HTTP-EQUIV=\"Content-Type\" Content=\"text/html; charset=us-ascii\"></ HEAD >\r\n" + 
        "<BODY><h2>Bad Request - Invalid Hostname</h2>\r\n" + 
        "<hr><p>HTTP Error 400. The request hostname is invalid.</p>\r\n" + 
        "</BODY></HTML>");
    
    private readonly RequestDelegate _next;
    private readonly ILogger<HostFilteringMiddleware> _logger;
    private readonly IOptionsMonitor<HostFilteringOptions> _optionsMonitor;
    
    private HostFilteringOptions _options;    
    private IList<StringSegment>? _allowedHosts;
    private bool? _allowAnyNonEmptyHost;
        
    public HostFilteringMiddleware(
        RequestDelegate next, 
        ILogger<HostFilteringMiddleware> logger, 
        IOptionsMonitor<HostFilteringOptions> optionsMonitor)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // 注入 hosting filteringoptions 的 monitor，
        // 跟踪其变化
        _optionsMonitor = optionsMonitor 
            ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _options = _optionsMonitor.CurrentValue;
        _optionsMonitor.OnChange(options =>
                                 {
                                     // Clear the cached settings so the 
                                     // next EnsureConfigured will re-evaluate.
                                     _options = options;
                                     _allowedHosts = new List<StringSegment>();
                                     _allowAnyNonEmptyHost = null;
                                 });
    }
        
    public Task Invoke(HttpContext context)
    {
        // 解析 allowed host
        var allowedHosts = EnsureConfigured();
        
        // check
        if (!CheckHost(context, allowedHosts))
        {
            return HostValidationFailed(context);
        }
        
        return _next(context);
    }
    
    /* ensure configure */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IList<StringSegment> EnsureConfigured()
    {
        if (_allowAnyNonEmptyHost == true || 
            _allowedHosts?.Count > 0)
        {
            Debug.Assert(_allowedHosts != null);
            
            return _allowedHosts;
        }
        
        return Configure();
    }
    
    // 解析（配置） allowed host
    private IList<StringSegment> Configure()
    {
        // 预结果
        var allowedHosts = new List<StringSegment>();
        
        // 如果 options 中 allowed host 不为空，且可以解析到通配符
        if (_options.AllowedHosts?.Count > 0 && 
            !TryProcessHosts(_options.AllowedHosts, allowedHosts))
        {
            _logger.WildcardDetected();
            _allowedHosts = allowedHosts;
            _allowAnyNonEmptyHost = true;
            return _allowedHosts;
        }
        
        if (allowedHosts.Count == 0)
        {
            throw new InvalidOperationException("No allowed hosts were configured.");
        }
        
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.AllowedHosts(string.Join("; ", allowedHosts));
        }
        
        _allowedHosts = allowedHosts;
        return _allowedHosts;
    }
    
    // returns false if any wildcards were found
    private bool TryProcessHosts(
        IEnumerable<string> incoming, 
        IList<StringSegment> results)
    {
        foreach (var entry in incoming)
        {
            // Punycode. Http.Sys requires you to register Unicode hosts, 
            // but the headers contain punycode.
            var host = new HostString(entry).ToUriComponent();
            
            if (IsTopLevelWildcard(host))
            {
                // Disable filtering
                return false;
            }
            
            if (!results.Contains(
                	host, 
                	StringSegmentComparer.OrdinalIgnoreCase))
            {
                results.Add(host);
            }
        }
        
        return true;
    }
    
    private bool IsTopLevelWildcard(string host)
    {
        return (
            // HttpSys wildcard
            string.Equals("*", host, StringComparison.Ordinal) || 
            // Kestrel wildcard, IPv6 Any
            string.Equals("[::]", host, StringComparison.Ordinal) || 
            // IPv4 Any
            string.Equals("0.0.0.0", host, StringComparison.Ordinal)); 
    }        
        
    /* check host */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CheckHost(
        HttpContext context, 
        IList<StringSegment> allowedHosts)
    {
        // 从 http request 解析 host
        var host = context.Request
            			  .Headers[HeaderNames.Host]
            			  .ToString();
        
        if (host.Length == 0)
        {
            return IsEmptyHostAllowed(context);
        }
        
        if (_allowAnyNonEmptyHost == true)
        {
            _logger.AllHostsAllowed();
            return true;
        }
        
        return CheckHostInAllowList(allowedHosts, host);
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool IsEmptyHostAllowed(HttpContext context)
    {
        // Http/1.0 does not require the host header.
        // Http/1.1 requires the header but the value may be empty.
        if (!_options.AllowEmptyHosts)
        {
            _logger.RequestRejectedMissingHost(context.Request.Protocol);
            return false;
        }
        _logger.RequestAllowedMissingHost(context.Request.Protocol);
        return true;
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool CheckHostInAllowList(
        IList<StringSegment> allowedHosts, 
        string host)
    {
        if (HostString.MatchesAny(new StringSegment(host), allowedHosts))
        {
            _logger.AllowedHostMatched(host);
            return true;
        }
        
        _logger.NoAllowedHostMatched(host);
        return false;
    }
    
    /* host validation failed */                                
    [MethodImpl(MethodImplOptions.NoInlining)]
    private Task HostValidationFailed(HttpContext context)
    {
        context.Response
               .StatusCode = 400;
        
        // 如果标记了 inclue failure message，
        // 返回默认的 html
        if (_options.IncludeFailureMessage)
        {
            context.Response
                   .ContentLength = DefaultResponse.Length;
            context.Response
                   .ContentType = "text/html";
            
            return context.Response
                		  .Body
                		  .WriteAsync(
                			   DefaultResponse, 
                			   0, 
                			   DefaultResponse.Length);
        }
        
        return Task.CompletedTask;
    }                        
}

```

#### 2.10 response cache

##### 2.10.1 add response cache

```c#
public static class ResponseCachingServicesExtensions
{    
    public static IServiceCollection AddResponseCaching(this IServiceCollection services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        services.TryAddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        
        return services;
    }
    
    
    public static IServiceCollection AddResponseCaching(
        this IServiceCollection services, 
        Action<ResponseCachingOptions> configureOptions)
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
        services.AddResponseCaching();
        
        return services;
    }
}

```

###### 2.10.1.1 response caching options

```c#
public class ResponseCachingOptions
{    
    public long SizeLimit { get; set; } = 100 * 1024 * 1024;        
    public long MaximumBodySize { get; set; } = 64 * 1024 * 1024;        
    public bool UseCaseSensitivePaths { get; set; } = false;        
 
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal ISystemClock SystemClock { get; set; } = new SystemClock();
}

```

##### 2.10.2 use response cache

```c#
public static IApplicationBuilder UseResponseCaching(this IApplicationBuilder app)
{
    if (app == null)
    {
        throw new ArgumentNullException(nameof(app));
    }
    
    return app.UseMiddleware<ResponseCachingMiddleware>();
}

```

###### 2.10.2.1 response caching middleware

```c#
public class ResponseCachingMiddleware
{
    private static readonly TimeSpan DefaultExpirationTimeSpan = TimeSpan.FromSeconds(10);
    
    // see https://tools.ietf.org/html/rfc7232#section-4.1
    private static readonly string[] HeadersToIncludeIn304 = new[] 
    { 
        "Cache-Control", 
        "Content-Location", 
        "Date", 
        "ETag", 
        "Expires", 
        "Vary" 
    };
    
    private readonly RequestDelegate _next;
    private readonly ResponseCachingOptions _options;
    private readonly ILogger _logger;
    private readonly IResponseCachingPolicyProvider _policyProvider;
    private readonly IResponseCache _cache;
    private readonly IResponseCachingKeyProvider _keyProvider;
            
    public ResponseCachingMiddleware(
        RequestDelegate next,
        IOptions<ResponseCachingOptions> options,
        ILoggerFactory loggerFactory,
        ObjectPoolProvider poolProvider)
        	: this(
                next,
                options,
                loggerFactory,
                new ResponseCachingPolicyProvider(),
                new MemoryResponseCache(
                    new MemoryCache(
                        new MemoryCacheOptions
                        {
                            SizeLimit = options.Value.SizeLimit
                        })),
                new ResponseCachingKeyProvider(poolProvider, options))
    { 
    }
    
    // for testing
    internal ResponseCachingMiddleware(
        RequestDelegate next,
        IOptions<ResponseCachingOptions> options,
        ILoggerFactory loggerFactory,
        IResponseCachingPolicyProvider policyProvider,
        IResponseCache cache,
        IResponseCachingKeyProvider keyProvider)
    {
        if (next == null)
        {
            throw new ArgumentNullException(nameof(next));
        }
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }
        if (policyProvider == null)
        {
            throw new ArgumentNullException(nameof(policyProvider));
        }
        if (cache == null)
        {
            throw new ArgumentNullException(nameof(cache));
        }
        if (keyProvider == null)
        {
            throw new ArgumentNullException(nameof(keyProvider));
        }
        
        _next = next;
        _options = options.Value;
        _logger = loggerFactory.CreateLogger<ResponseCachingMiddleware>();
        _policyProvider = policyProvider;
        _cache = cache;
        _keyProvider = keyProvider;
    }
           
    public async Task Invoke(HttpContext httpContext)
    {
        var context = new ResponseCachingContext(httpContext, _logger);
        
        // 如果 policy 可以 attempt response cache
        if (_policyProvider.AttemptResponseCaching(context))
        {                        
            // 如果 policy 允许 lookup，
            // 并且可以 serve cache，-> return
            if (_policyProvider.AllowCacheLookup(context) && 
                // 1- try serve cache
                await TryServeFromCacheAsync(context))
            {
                return;
            }
            
            // 如果 policy 允许 storage，
            if (_policyProvider.AllowCacheStorage(context))
            {
                // 2- 注入 response stream
                ShimResponseStream(context);
                
                try
                {
                    await _next(httpContext);
                    
                    // If there was no response body, check the response headers now. 
                    // We can cache things like redirects.
                    StartResponse(context);
                    
                    // Finalize the cache entry
                    FinalizeCacheBody(context);
                }
                finally
                {
                    UnshimResponseStream(context);
                }
                
                return;
            }
        }
        
        // Response should not be captured but add IResponseCachingFeature which 
        // may be required when the response is generated
        
        // （否则，即不可以 attempt response cache），
        // 注入 response caching feature
        AddResponseCachingFeature(httpContext);
        
        try
        {
            await _next(httpContext);
        }
        finally
        {
            RemoveResponseCachingFeature(httpContext);
        }
    }
    
    // 1- serve from cache
    internal async Task<bool> TryServeFromCacheAsync(ResponseCachingContext context)
    {
        // 创建 cache key
        context.BaseKey = _keyProvider.CreateBaseKey(context);
        // 按照 cache key 解析 cache entry
        var cacheEntry = _cache.Get(context.BaseKey);
        
        // 如果 cache entry 是 cached vary by rules
        if (cacheEntry is CachedVaryByRules cachedVaryByRules)
        {
            // Request contains vary rules, recompute key(s) and try again
            context.CachedVaryByRules = cachedVaryByRules;
            
            foreach (var varyKey in _keyProvider.CreateLookupVaryByKeys(context))
            {
                if (await TryServeCachedResponseAsync(context, _cache.Get(varyKey)))
                {
                    return true;
                }
            }
        }
        // 否则，即 cache entry 不是 cached vary by rules（或者 null）
        else
        {
            if (await TryServeCachedResponseAsync(context, cacheEntry))
            {
                return true;
            }
        }
        
        // 如果 http request header 中包含 [cache control, only if canched string]，
        // -> 返回 504 gateway timeout
        if (HeaderUtilities.ContainsCacheDirective(
            	context.HttpContext
            		   .Request
            		   .Headers[HeaderNames.CacheControl], 
	            CacheControlHeaderValue.OnlyIfCachedString))
        {
            _logger.GatewayTimeoutServed();
            context.HttpContext
                   .Response
                   .StatusCode = StatusCodes.Status504GatewayTimeout;
            return true;
        }
        
        _logger.NoResponseServed();
        return false;
    }
    
    // 1.5- serve cached response
    internal async Task<bool> TryServeCachedResponseAsync(
        ResponseCachingContext context, 
        IResponseCacheEntry? cacheEntry)
    {
        // 如果 cache entry 不是 cached response，-> false
        if (!(cacheEntry is CachedResponse cachedResponse))
        {
            return false;
        }
        
        /* cache entry 是 cached response */
        
        // 注入 cached response
        context.CachedResponse = cachedResponse;
        // 注入 cached response header
        context.CachedResponseHeaders = cachedResponse.Headers;
        // 注入 response time (current time)
        context.ResponseTime = _options.SystemClock.UtcNow;
        // 计算 cached entry age        
        var cachedEntryAge = context.ResponseTime.Value - context.CachedResponse.Created;
        context.CachedEntryAge = cachedEntryAge > TimeSpan.Zero 
            ? cachedEntryAge 
            : TimeSpan.Zero;
        
        // 如果 cache policy provider 判断可以 fresh cached entry
        if (_policyProvider.IsCachedEntryFresh(context))
        {            
            // 如果 content not modified，-> 304 & headers
            if (ContentIsNotModified(context))
            {
                _logger.NotModifiedServed();
                context.HttpContext
                       .Response
                       .StatusCode = StatusCodes.Status304NotModified;
                
                if (context.CachedResponseHeaders != null)
                {
                    foreach (var key in HeadersToIncludeIn304)
                    {
                        if (context.CachedResponseHeaders.
                            	   TryGetValue(key, out var values))
                        {
                            context.HttpContext
                                   .Response
                                   .Headers[key] = values;
                        }
                    }
                }
            }
            // 否则，即 content modified
            else
            {
                // 解析 http context response
                var response = context.HttpContext.Response;
                // 复制 status code 和 headers
                response.StatusCode = context.CachedResponse.StatusCode;
                foreach (var header in context.CachedResponse.Headers)
                {
                    response.Headers[header.Key] = header.Value;
                }
                
                // Note: int64 division truncates result and errors may be up to 1 second. 
                // This reduction in accuracy of age calculation is considered appropriate 
                // since it is small compared to clock skews and the "Age" header is an 
                // estimate of the real age of cached content.
                
                // 设置 age
                response.Headers[HeaderNames.Age] = 
                    HeaderUtilities.FormatNonNegativeInt64(
                    	context.CachedEntryAge
                    		   .Value
                    		   .Ticks / TimeSpan.TicksPerSecond);
                
                // Copy the cached response body
                var body = context.CachedResponse.Body;
                if (body.Length > 0)
                {
                    try
                    {
                        await body.CopyToAsync(
                            response.BodyWriter, 
                            context.HttpContext.RequestAborted);
                    }
                    catch (OperationCanceledException)
                    {
                        context.HttpContext.Abort();
                    }
                }
                _logger.CachedResponseServed();
            }
            return true;
        }
        
        return false;
    }
    
    
        
    private bool OnFinalizeCacheHeaders(ResponseCachingContext context)
    {
        if (_policyProvider.IsResponseCacheable(context))
        {
            var storeVaryByEntry = false;
            context.ShouldCacheResponse = true;
            
            // Create the cache entry now
            var response = context.HttpContext.Response;
            var varyHeaders = new StringValues(
                response.Headers
                		.GetCommaSeparatedValues(HeaderNames.Vary));
            var varyQueryKeys = new StringValues(
                context.HttpContext
                	   .Features
                	   .Get<IResponseCachingFeature>()
                	   ?.VaryByQueryKeys);
            context.CachedResponseValidFor = context.ResponseSharedMaxAge 
                ?? context.ResponseMaxAge 
                ?? (context.ResponseExpires - context.ResponseTime!.Value) 
                ?? DefaultExpirationTimeSpan;
            
            // Generate a base key if none exist
            if (string.IsNullOrEmpty(context.BaseKey))
            {
                context.BaseKey = _keyProvider.CreateBaseKey(context);
            }
            
            // Check if any vary rules exist
            if (!StringValues.IsNullOrEmpty(varyHeaders) || 
                !StringValues.IsNullOrEmpty(varyQueryKeys))
            {
                // Normalize order and casing of vary by rules
                var normalizedVaryHeaders = 
                    GetOrderCasingNormalizedStringValues(varyHeaders);
                var normalizedVaryQueryKeys = 
                    GetOrderCasingNormalizedStringValues(varyQueryKeys);
                
                // Update vary rules if they are different
                if (context.CachedVaryByRules == null ||
                    !StringValues.Equals(
                        context.CachedVaryByRules.QueryKeys, 
                        normalizedVaryQueryKeys) ||
                    !StringValues.Equals(
                        context.CachedVaryByRules.Headers, 
                        normalizedVaryHeaders))
                {
                    context.CachedVaryByRules = new CachedVaryByRules
                    {
                        VaryByKeyPrefix = FastGuid.NewGuid().IdString,
                        Headers = normalizedVaryHeaders,
                        QueryKeys = normalizedVaryQueryKeys
                    };
                }
                
                // Always overwrite the CachedVaryByRules to update the expiry information
                _logger.VaryByRulesUpdated(
                    normalizedVaryHeaders, 
                    normalizedVaryQueryKeys);
                
                storeVaryByEntry = true;
                
                context.StorageVaryKey = _keyProvider.CreateStorageVaryByKey(context);
            }
            
            // Ensure date header is set
            if (!context.ResponseDate.HasValue)
            {
                context.ResponseDate = context.ResponseTime!.Value;
                // Setting the date on the raw response headers.
                context.HttpContext
                       .Response
                       .Headers[HeaderNames.Date] = HeaderUtilities.FormatDate(
                    									context.ResponseDate.Value);
            }
            
            // Store the response on the state
            context.CachedResponse = new CachedResponse
            {
                Created = context.ResponseDate.Value,
                StatusCode = context.HttpContext.Response.StatusCode,
                Headers = new HeaderDictionary()
            };
            
            foreach (var header in context.HttpContext
                     					  .Response
                     					  .Headers)
            {
                if (!string.Equals(
                    	header.Key, 
                    	HeaderNames.Age, 
                    	StringComparison.OrdinalIgnoreCase))
                {
                    context.CachedResponse
                           .Headers[header.Key] = header.Value;
                }
            }
            
            return storeVaryByEntry;
        }
        
        context.ResponseCachingStream.DisableBuffering();
        return false;
    }
    
    internal void FinalizeCacheHeaders(ResponseCachingContext context)
    {
        if (OnFinalizeCacheHeaders(context))
        {
            _cache.Set(
                context.BaseKey, 
                context.CachedVaryByRules, 
                context.CachedResponseValidFor);
        }
    }
    
    internal void FinalizeCacheBody(ResponseCachingContext context)
    {
        if (context.ShouldCacheResponse && 
            context.ResponseCachingStream
            	   .BufferingEnabled)
        {
            var contentLength = context.HttpContext
                					   .Response
                					   .ContentLength;
            var cachedResponseBody = context.ResponseCachingStream
                							.GetCachedResponseBody();
            if (!contentLength.HasValue || 
                contentLength == cachedResponseBody.Length || 
                (cachedResponseBody.Length == 0 && 
                 HttpMethods.IsHead(context.HttpContext
                                    	   .Request
                                    	   .Method)))
            {
                var response = context.HttpContext.Response;
                // Add a content-length if required
                if (!response.ContentLength.HasValue && 
                    StringValues.IsNullOrEmpty(
                        response.Headers[HeaderNames.TransferEncoding]))
                {
                    context.CachedResponse
                           .Headers[HeaderNames.ContentLength] = 
                        HeaderUtilities.FormatNonNegativeInt64(cachedResponseBody.Length);
                }
                
                context.CachedResponse.Body = cachedResponseBody;
                _logger.ResponseCached();
                _cache.Set(
                    context.StorageVaryKey ?? context.BaseKey, 
                    context.CachedResponse, 
                    context.CachedResponseValidFor);
            }
            else
            {
                _logger.ResponseContentLengthMismatchNotCached();
            }
        }
        else
        {
            _logger.LogResponseNotCached();
        }
    }
    

    private bool OnStartResponse(ResponseCachingContext context)
    {
        if (!context.ResponseStarted)
        {
            context.ResponseStarted = true;
            context.ResponseTime = _options.SystemClock.UtcNow;
            
            return true;
        }
        return false;
    }
    
    internal void StartResponse(ResponseCachingContext context)
    {
        if (OnStartResponse(context))
        {
            FinalizeCacheHeaders(context);
        }
    }
    
    internal static void AddResponseCachingFeature(HttpContext context)
    {
        if (context.Features
            	   .Get<IResponseCachingFeature>() != null)
        {
            throw new InvalidOperationException(
                $"Another instance of {nameof(ResponseCachingFeature)} already exists. Only one instance of {nameof(ResponseCachingMiddleware)} can be configured for an application.");
        }
        context.Features.Set<IResponseCachingFeature>(new ResponseCachingFeature());
    }
    
    internal void ShimResponseStream(ResponseCachingContext context)
    {
        // Shim response stream
        context.OriginalResponseStream = context.HttpContext
            									.Response
            									.Body;
        
        context.ResponseCachingStream = new ResponseCachingStream(
            context.OriginalResponseStream,
            _options.MaximumBodySize,
            StreamUtilities.BodySegmentSize,
            () => StartResponse(context));
        
        context.HttpContext
               .Response
               .Body = context.ResponseCachingStream;
        
        // Add IResponseCachingFeature
        AddResponseCachingFeature(context.HttpContext);
    }
    
    internal static void RemoveResponseCachingFeature(HttpContext context) =>
        context.Features.Set<IResponseCachingFeature?>(null);
    
    internal static void UnshimResponseStream(ResponseCachingContext context)
    {
        // Unshim response stream
        context.HttpContext
               .Response
               .Body = context.OriginalResponseStream;
        
        // Remove IResponseCachingFeature
        RemoveResponseCachingFeature(context.HttpContext);
    }
    
    internal static bool ContentIsNotModified(ResponseCachingContext context)
    {
        var cachedResponseHeaders = context.CachedResponseHeaders;
        var ifNoneMatchHeader = context.HttpContext
						               .Request
						               .Headers[HeaderNames.IfNoneMatch];
        
        if (!StringValues.IsNullOrEmpty(ifNoneMatchHeader))
        {
            if (ifNoneMatchHeader.Count == 1 && 
                StringSegment.Equals(
                    ifNoneMatchHeader[0], 
                    EntityTagHeaderValue.Any.Tag, 
                    StringComparison.OrdinalIgnoreCase))
            {
                context.Logger.NotModifiedIfNoneMatchStar();
                return true;
            }
            
            EntityTagHeaderValue eTag;
            if (!StringValues.IsNullOrEmpty(cachedResponseHeaders[HeaderNames.ETag]) &&
                EntityTagHeaderValue.TryParse(
                    cachedResponseHeaders[HeaderNames.ETag].ToString(), 
                    out eTag) && 
                EntityTagHeaderValue.TryParseList(
                    ifNoneMatchHeader, 
                    out var ifNoneMatchEtags))
            {
                for (var i = 0; i < ifNoneMatchEtags.Count; i++)
                {
                    var requestETag = ifNoneMatchEtags[i];
                    if (eTag.Compare(
                        	requestETag, 
                        	useStrongComparison: false))
                    {
                        context.Logger
                               .NotModifiedIfNoneMatchMatched(requestETag);
                        return true;
                    }
                }
            }
        }
        else
        {
            var ifModifiedSince = context.HttpContext
                						 .Request
                						 .Headers[HeaderNames.IfModifiedSince];
            if (!StringValues.IsNullOrEmpty(ifModifiedSince))
            {
                DateTimeOffset modified;
                if (!HeaderUtilities.TryParseDate(
                    	cachedResponseHeaders[HeaderNames.LastModified].ToString(), 
                    	out modified) &&
                    !HeaderUtilities.TryParseDate(
                        cachedResponseHeaders[HeaderNames.Date].ToString(), 
                        out modified))
                {
                    return false;
                }
                
                DateTimeOffset modifiedSince;
                if (HeaderUtilities.TryParseDate(
                    	ifModifiedSince.ToString(), 
                    	out modifiedSince) &&
                    modified <= modifiedSince)
                {
                    context.Logger
                           .NotModifiedIfModifiedSinceSatisfied(modified, modifiedSince);
                    return true;
                }
            }
        }
        
        return false;
    }
    
    // Normalize order and casing
    internal static StringValues GetOrderCasingNormalizedStringValues(
        StringValues stringValues)
    {
        if (stringValues.Count == 1)
        {
            return new StringValues(stringValues.ToString()
                                    			.ToUpperInvariant());
        }
        else
        {
            var originalArray = stringValues.ToArray();
            var newArray = new string[originalArray.Length];
            
            for (var i = 0; i < originalArray.Length; i++)
            {
                newArray[i] = originalArray[i].ToUpperInvariant();
            }
            
            // Since the casing has already been normalized, use Ordinal comparison
            Array.Sort(newArray, StringComparer.Ordinal);
            
            return new StringValues(newArray);
        }
    }
}

```



#### 2.11 response compression

##### 2.11.1 compression provider

###### 2.11.1.1 接口

```c#
public interface ICompressionProvider
{        
    string EncodingName { get; }        
    bool SupportsFlush { get; }    
    
    Stream CreateStream(Stream outputStream);
}

```

###### 2.11.1.2 gzip compression provider

```c#
public class GzipCompressionProvider : ICompressionProvider
{    
    private GzipCompressionProviderOptions Options { get; }
    public string EncodingName { get; } = "gzip";     
    public bool SupportsFlush => true;            
        
    public GzipCompressionProvider(
        IOptions<GzipCompressionProviderOptions> options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        Options = options.Value;
    }    
    
    /// <inheritdoc />
    public Stream CreateStream(Stream outputStream)
        => new GZipStream(outputStream, Options.Level, leaveOpen: true);
}

public class GzipCompressionProviderOptions : IOptions<GzipCompressionProviderOptions>
{    
    public CompressionLevel Level { get; set; } = CompressionLevel.Fastest;    
    
    /// <inheritdoc />
    GzipCompressionProviderOptions 
        IOptions<GzipCompressionProviderOptions>.Value => this;
}

```

###### 2.11.1.3 brotli compression provider

```c#
public class BrotliCompressionProvider : ICompressionProvider
{    
    private BrotliCompressionProviderOptions Options { get; }    
    public string EncodingName { get; } = "br";   
    public bool SupportsFlush { get; } = true;
                    
    public BrotliCompressionProvider(
        IOptions<BrotliCompressionProviderOptions> options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        Options = options.Value;
    }
    
    /// <inheritdoc />
    public Stream CreateStream(Stream outputStream)
    {
        return new BrotliStream(outputStream, Options.Level, leaveOpen: true);
    }       
}

public class BrotliCompressionProviderOptions : 
	IOptions<BrotliCompressionProviderOptions>
{   
    public CompressionLevel Level { get; set; } = CompressionLevel.Fastest;    
        
    /// <inheritdoc />
    BrotliCompressionProviderOptions 
        IOptions<BrotliCompressionProviderOptions>.Value => this;
}

```

##### 2.11.2 compression collection

```c#
public class CompressionProviderCollection : Collection<ICompressionProvider>
{    
    // Provider instances will be created using an "IServiceProvider"    
    public void Add<TCompressionProvider>() 
        where TCompressionProvider : ICompressionProvider
    {
        Add(typeof(TCompressionProvider));
    }
        
    // Provider instances will be created using an "IServiceProvider"     
    public void Add(Type providerType)
    {
        if (providerType == null)
        {
            throw new ArgumentNullException(nameof(providerType));
        }
        
        if (!typeof(ICompressionProvider).IsAssignableFrom(providerType))
        {
            throw new ArgumentException(
                $"The provider must implement {nameof(ICompressionProvider)}.", 
                nameof(providerType));
        }
        
        var factory = new CompressionProviderFactory(providerType);
        Add(factory);
    }
}

```

##### 2.11.3 compression provider factory

```c#
internal class CompressionProviderFactory : ICompressionProvider
{
    private Type ProviderType { get; }
    
    string ICompressionProvider.EncodingName
    {
        get { throw new NotSupportedException(); }
    }
    
    bool ICompressionProvider.SupportsFlush
    {
        get { throw new NotSupportedException(); }
    }
            
    public CompressionProviderFactory(Type providerType)
    {
        ProviderType = providerType;
    }
            
    public ICompressionProvider CreateInstance(IServiceProvider serviceProvider)
    {
        if (serviceProvider == null)
        {
            throw new ArgumentNullException(nameof(serviceProvider));
        }
        
        return (ICompressionProvider)ActivatorUtilities.CreateInstance(
            serviceProvider, 
            ProviderType, 
            Type.EmptyTypes);
    }
    
    Stream ICompressionProvider.CreateStream(Stream outputStream)
    {
        throw new NotSupportedException();
    }
}

```

##### 2.11.4 response compression provider

###### 2.11.4.1 接口

```c#
public interface IResponseCompressionProvider
{    
    ICompressionProvider? GetCompressionProvider(HttpContext context);        
    bool ShouldCompressResponse(HttpContext context);       
    bool CheckRequestAcceptsCompression(HttpContext context);
}

```

###### 2.11.4.2 response compression provider

```c#
public class ResponseCompressionProvider : IResponseCompressionProvider
{
    private readonly ICompressionProvider[] _providers;
    private readonly HashSet<string> _mimeTypes;
    private readonly HashSet<string> _excludedMimeTypes;
    private readonly bool _enableForHttps;
    private readonly ILogger _logger;
        
    public ResponseCompressionProvider(
        IServiceProvider services, 
        IOptions<ResponseCompressionOptions> options)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        // 从 response compression options 中解析 providers 集合
        var responseCompressionOptions = options.Value;        
        _providers = responseCompressionOptions.Providers
            								   .ToArray();
        
        // 如果 providers 为空，创建集合并注入 brotli factory、gzip factory
        if (_providers.Length == 0)
        {
            // Use the factory so it can resolve 
            // IOptions<GzipCompressionProviderOptions> from DI.
            _providers = new ICompressionProvider[]
            {
                new CompressionProviderFactory(typeof(BrotliCompressionProvider)),
                new CompressionProviderFactory(typeof(GzipCompressionProvider)),
            };
        }
        
        // 遍历 providers 集合，将 factory 创建 provider 注入
        for (var i = 0; i < _providers.Length; i++)
        {
            var factory = _providers[i] as CompressionProviderFactory;
            if (factory != null)
            {
                _providers[i] = factory.CreateInstance(services);
            }
        }
        
        // 从 response comression options 解析 mime type
        // （如果为空，创建 default），        
        var mimeTypes = responseCompressionOptions.MimeTypes;
        if (mimeTypes == null || !mimeTypes.Any())
        {
            mimeTypes = ResponseCompressionDefaults.MimeTypes;
        }
        // 注入 mime types 集合
        _mimeTypes = new HashSet<string>(
            mimeTypes, 
            StringComparer.OrdinalIgnoreCase);
        
        // 从 response compression opitons 解析 exclude mime type，
        // 注入 exclude mime types 集合
        _excludedMimeTypes = new HashSet<string>(
            responseCompressionOptions.ExcludedMimeTypes ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
        
        // 从 response compression options 解析 enable for https
        _enableForHttps = responseCompressionOptions.EnableForHttps;
        
        _logger = services.GetRequiredService<ILogger<ResponseCompressionProvider>>();
    }
    
    /// <inheritdoc />
    public virtual ICompressionProvider? GetCompressionProvider(HttpContext context)
    {
        // 解析 http request 的 accept encoding
        var accept = context.Request
            				.Headers[HeaderNames.AcceptEncoding];
        
        // Note this is already checked in CheckRequestAcceptsCompression 
        // which _should_ prevent any of these other methods from being called.
        if (StringValues.IsNullOrEmpty(accept))
        {
            Debug.Assert(false, "Duplicate check failed.");
            _logger.NoAcceptEncoding();
            return null;
        }
        
        // 如果不能从 accept encoding 中解析 encodings，
        // 或者 encodings 为空， -> null
        if (!StringWithQualityHeaderValue.TryParseList(
            	accept, 
            	out var encodings) || 
            encodings.Count == 0)
        {
            _logger.NoAcceptEncoding();
            return null;
        }
        
        // 预结果
        var candidates = new HashSet<ProviderCandidate>();
        
        // 遍历 encoding，        
        foreach (var encoding in encodings)
        {
            var encodingName = encoding.Value;
            var quality = encoding.Quality.GetValueOrDefault(1);
            
            if (quality < double.Epsilon)
            {
                continue;
            }
            
            // 封装 quality > 0 的 encoding、compression provider，
            // 注入到 provider candidate 集合
            for (int i = 0; i < _providers.Length; i++)
            {
                var provider = _providers[i];
                
                if (StringSegment.Equals(
                    	provider.EncodingName, 
                    	encodingName, 
                    	StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(
                        new ProviderCandidate(
                            	provider.EncodingName, 
                            	quality, 
                            	i, 
                            	provider));
                }
            }
            
            // Uncommon but valid options
            
            // 封装 ”*“ 的 encoding、compression provider，
            // 注入到 provider candidate 集合            
            if (StringSegment.Equals(
                	"*", 
                	encodingName, 
                	StringComparison.Ordinal))
            {
                for (int i = 0; i < _providers.Length; i++)
                {
                    var provider = _providers[i];
                    
                    // Any provider is a candidate.
                    candidates.Add(
                        new ProviderCandidate(
                            	provider.EncodingName, 
                            	quality, 
                            	i, 
                            	provider));
                }
                
                break;
            }
            // 封装 ”identity“ 的 encoding、compression provider，
            // 注入到 provider candidate 集合            
            if (StringSegment.Equals(
                	"identity", 
                	encodingName, 
                	StringComparison.OrdinalIgnoreCase))
            {
                // We add 'identity' to the list of "candidates" with a very low priority 
                // and no provider.
                // This will allow it to be ordered based on its quality (and priority) 
                // later in the method.
                candidates.Add(
                    new ProviderCandidate(
                        	encodingName.Value, 
                        	quality, 
                        	priority: int.MaxValue, 
                        	provider: null));
            }
        }
        
        // 按 quality 排序 candidate、获取 1st candidate 的 provider
        ICompressionProvider? selectedProvider = null;
        if (candidates.Count <= 1)
        {
            selectedProvider = candidates.FirstOrDefault()
                						 .Provider;
        }
        else
        {
            selectedProvider = candidates.OrderByDescending(x => x.Quality)
						                 .ThenBy(x => x.Priority)
						                 .First()
                						 .Provider;
        }
        
        if (selectedProvider == null)
        {
            // "identity" would match as a candidate but not have a provider implementation
            _logger.NoCompressionProvider();
            return null;
        }
        
        _logger.CompressingWith(selectedProvider.EncodingName);
        
        // 返回 provider
        return selectedProvider;
    }
    
    /// <inheritdoc />
    public virtual bool ShouldCompressResponse(HttpContext context)
    {
        // 从 http context 解析 https compression mode
        var httpsMode = context.Features
            				   .Get<IHttpsCompressionFeature>()
            				   ?.Mode 
            				?? HttpsCompressionMode.Default;
        
        // Check if the app has opted into or out of compression over HTTPS
        
        // 如果是 https，
        // 但是 compression mode 是 do not compress，
        // 或者 enable for https 标记为 false，且 compression mode 是 compress，
        // 说明不对 https 采用 cmpress -> false
        if (context.Request.IsHttps && 
            (httpsMode == HttpsCompressionMode.DoNotCompress|| 
             !(_enableForHttps || httpsMode == HttpsCompressionMode.Compress)))
        {
            _logger.NoCompressionForHttps();
            return false;
        }
        
        // 如果 http request header 包含 content range，-> false
        if (context.Response
            	   .Headers
            	   .ContainsKey(HeaderNames.ContentRange))
        {
            _logger.NoCompressionDueToHeader(HeaderNames.ContentRange);
            return false;
        }
        // 如果 http response header 包含 content encoding，-> false
        if (context.Response
            	   .Headers
            	   .ContainsKey(HeaderNames.ContentEncoding))
        {
            _logger.NoCompressionDueToHeader(HeaderNames.ContentEncoding);
            return false;
        }
        
        // 解析 mime type，如果不成功，-> false
        var mimeType = context.Response.ContentType;        
        if (string.IsNullOrEmpty(mimeType))
        {
            _logger.NoCompressionForContentType(mimeType);
            return false;
        }
        
        // 去掉 mime type 的 optional parameters
        var separator = mimeType.IndexOf(';');
        if (separator >= 0)
        {
            // Remove the content-type optional parameters
            mimeType = mimeType.Substring(0, separator);
            mimeType = mimeType.Trim();
        }
        
        // 判断，
        //   1- mime type exact
        //   2- mime type partial
        //   3- mime type wildcard
        var shouldCompress = 
            //check exact match type/subtype
            ShouldCompressExact(mimeType) 
            	//check partial match type/*
            	?? ShouldCompressPartial(mimeType) 
	            //check wildcard */*
            	?? _mimeTypes.Contains("*/*"); 
        
        if (shouldCompress)
        {
            _logger.ShouldCompressResponse();  // Trace, there will be more logs
            return true;
        }
        
        _logger.NoCompressionForContentType(mimeType);
        return false;
    }
    
    // 1- should compress exact
    private bool? ShouldCompressExact(string mimeType)
    {
        //Check excluded MIME types first, then included
        if (_excludedMimeTypes.Contains(mimeType))
        {
            return false;
        }
        
        if (_mimeTypes.Contains(mimeType))
        {
            return true;
        }
        
        return null;
    }
    
    // 2- shoudl compress partial
    private bool? ShouldCompressPartial(string? mimeType)
    {
        var slashPos = mimeType?.IndexOf('/');
        
        if (slashPos >= 0)
        {
            var partialMimeType = mimeType!.Substring(0, slashPos.Value) + "/*";
            return ShouldCompressExact(partialMimeType);
        }
        
        return null;
    }
                
    /// <inheritdoc />
    public bool CheckRequestAcceptsCompression(HttpContext context)
    {
        if (string.IsNullOrEmpty(
            	context.Request
                       .Headers[HeaderNames.AcceptEncoding]))
        {
            _logger.NoAcceptEncoding();
            return false;
        }
        
        _logger.RequestAcceptsCompression(); // Trace, there will be more logs
        return true;
    }
        
    // provider candidate 结构体
    private readonly struct ProviderCandidate : IEquatable<ProviderCandidate>
    {
        public string EncodingName { get; }        
        public double Quality { get; }        
        public int Priority { get; }        
        public ICompressionProvider? Provider { get; }
        
        public ProviderCandidate(
            string encodingName, 
            double quality, 
            int priority, 
            ICompressionProvider? provider)
        {
            EncodingName = encodingName;
            Quality = quality;
            Priority = priority;
            Provider = provider;
        }
                        
        public bool Equals(ProviderCandidate other)
        {
            return string.Equals(
                EncodingName, 
                other.EncodingName, 
                StringComparison.OrdinalIgnoreCase);
        }
        
        public override bool Equals(object? obj)
        {
            return obj is ProviderCandidate candidate && 
                   Equals(candidate);
        }
        
        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase
                				 .GetHashCode(EncodingName);
        }
    }
}

```

###### 2.11.4.3 response compression default

```c#
public class ResponseCompressionDefaults
{   
    public static readonly IEnumerable<string> MimeTypes = new[]
    {
        // General
        "text/plain",
        // Static files
        "text/css",
        "application/javascript",
        // MVC
        "text/html",
        "application/xml",
        "text/xml",
        "application/json",
        "text/json",
        // WebAssembly
        "application/wasm",
    };
}

```

##### 2.11.5 add response compression

```c#
public static class ResponseCompressionServicesExtensions
{    
    public static IServiceCollection AddResponseCompression(this IServiceCollection services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        services.TryAddSingleton<
            IResponseCompressionProvider, 
        	ResponseCompressionProvider>();
        
        return services;
    }
        
    public static IServiceCollection AddResponseCompression(
        this IServiceCollection services, 
        Action<ResponseCompressionOptions> configureOptions)
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
        services.TryAddSingleton<
            IResponseCompressionProvider, 
        	ResponseCompressionProvider>();
        
        return services;
    }
}

```

###### 2.11.5.1 response compression options

```c#
public class ResponseCompressionOptions
{    
    public IEnumerable<string> MimeTypes { get; set; } = 
        Enumerable.Empty<string>();    
    
    public IEnumerable<string> ExcludedMimeTypes { get; set; } = 
        Enumerable.Empty<string>();  
    
    public bool EnableForHttps { get; set; }
        
    public CompressionProviderCollection Providers { get; } = 
        new CompressionProviderCollection();
}

```

##### 2.11.6 use response compression

```c#
public static class ResponseCompressionBuilderExtensions
{    
    public static IApplicationBuilder UseResponseCompression(this IApplicationBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        return builder.UseMiddleware<ResponseCompressionMiddleware>();
    }
}
```

###### 2.11.6.1 response compression middleware

```c#

public class ResponseCompressionMiddleware
{
    private readonly RequestDelegate _next;    
    private readonly IResponseCompressionProvider _provider;
            
    public ResponseCompressionMiddleware(
        RequestDelegate next, 
        IResponseCompressionProvider provider)
    {
        if (next == null)
        {
            throw new ArgumentNullException(nameof(next));
        }
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }
        
        _next = next;
        _provider = provider;
    }
            
    public async Task Invoke(HttpContext context)
    {
        // 如果不是 request accept compression
        if (!_provider.CheckRequestAcceptsCompression(context))
        {
            await _next(context);
            return;
        }
        
        // 解析 original response body、compression feature
        var originalBodyFeature = context.Features.Get<IHttpResponseBodyFeature>();
        var originalCompressionFeature = context.Features.Get<IHttpsCompressionFeature>();
        
        Debug.Assert(originalBodyFeature != null);
        
        // 构建 response compression body
        var compressionBody = 
            new ResponseCompressionBody(
	            context, 
    	        _provider, 
        	    originalBodyFeature);
        
        // 注入 response compression body 作为新的 response body、compression feature
        context.Features.Set<IHttpResponseBodyFeature>(compressionBody);
        context.Features.Set<IHttpsCompressionFeature>(compressionBody);

        try
        {
            await _next(context);
            await compressionBody.FinishCompressionAsync();
        }
        finally
        {
            // 恢复 response body feature、compression feature
            context.Features.Set(originalBodyFeature);
            context.Features.Set(originalCompressionFeature);
        }
    }
}

```

###### 2.11.6.2 response compression body

```c#
internal class ResponseCompressionBody : 
	Stream, 
	IHttpResponseBodyFeature, 
	IHttpsCompressionFeature
{
    private readonly HttpContext _context;
    private readonly IHttpResponseBodyFeature _innerBodyFeature;                
    private readonly IResponseCompressionProvider _provider;
    
    private readonly Stream _innerStream;
        
    private ICompressionProvider? _compressionProvider;
    private bool _compressionChecked;
    private Stream? _compressionStream;
    private PipeWriter? _pipeAdapter;
    private bool _providerCreated;
    private bool _autoFlush;
    private bool _complete;
    
    internal ResponseCompressionBody(
        HttpContext context, 
        IResponseCompressionProvider provider,        
        IHttpResponseBodyFeature innerBodyFeature)
    {
        _context = context;
        _provider = provider;
        _innerBodyFeature = innerBodyFeature;
        _innerStream = innerBodyFeature.Stream;
    }
    
    internal async Task FinishCompressionAsync()
    {
        if (_complete)
        {
            return;
        }
        
        _complete = true;
        
        if (_pipeAdapter != null)
        {
            await _pipeAdapter.CompleteAsync();
        }
        
        if (_compressionStream != null)
        {
            await _compressionStream.DisposeAsync();
        }
        
        // Adds the compression headers for HEAD requests even if the body was not used.
        if (!_compressionChecked && 
            HttpMethods.IsHead(_context.Request.Method))
        {
            InitializeCompressionHeaders();
        }
    }
    
    HttpsCompressionMode IHttpsCompressionFeature.Mode { get; set; } = 
        HttpsCompressionMode.Default;
    
    public override bool CanRead => false;    
    public override bool CanSeek => false;    
    public override bool CanWrite => _innerStream.CanWrite;
        
    public override long Length
    {
        get { throw new NotSupportedException(); }
    }
    
    public override long Position
    {
        get { throw new NotSupportedException(); }
        set { throw new NotSupportedException(); }
    }
    
    public Stream Stream => this;
    
    public PipeWriter Writer
    {
        get
        {
            if (_pipeAdapter == null)
            {
                _pipeAdapter = PipeWriter.Create(
                    Stream, 
                    new StreamPipeWriterOptions(leaveOpen: true));
            }
            
            return _pipeAdapter;
        }
    }
    
    public override void Flush()
    {
        if (!_compressionChecked)
        {
            OnWrite();
            // Flush the original stream to send the headers. Flushing the compression 
            // stream won't flush the original stream if no data has been written yet.
            _innerStream.Flush();
            return;
        }
        
        if (_compressionStream != null)
        {
            _compressionStream.Flush();
        }
        else
        {
            _innerStream.Flush();
        }
    }
    
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        if (!_compressionChecked)
        {
            OnWrite();
            // Flush the original stream to send the headers. Flushing the compression 
            // stream won't flush the original stream if no data has been written yet.
            return _innerStream.FlushAsync(cancellationToken);
        }
        
        if (_compressionStream != null)
        {
            return _compressionStream.FlushAsync(cancellationToken);
        }
        
        return _innerStream.FlushAsync(cancellationToken);
    }
    
    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
    
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }
    
    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }
    
    public override void Write(byte[] buffer, int offset, int count)
    {
        OnWrite();
        
        if (_compressionStream != null)
        {
            _compressionStream.Write(buffer, offset, count);
            if (_autoFlush)
            {
                _compressionStream.Flush();
            }
        }
        else
        {
            _innerStream.Write(buffer, offset, count);
        }
    }
    
    public override IAsyncResult BeginWrite(
        byte[] buffer, 
        int offset, 
        int count, 
        AsyncCallback? callback, 
        object? state)
    {
        var tcs = new TaskCompletionSource(
            state: state, 
            TaskCreationOptions.RunContinuationsAsynchronously);
        
        InternalWriteAsync(buffer, offset, count, callback, tcs);
        return tcs.Task;
    }
    
    private async void InternalWriteAsync(
        byte[] buffer, 
        int offset, 
        int count, 
        AsyncCallback? callback, 
        TaskCompletionSource tcs)
    {
        try
        {
            await WriteAsync(buffer, offset, count);
            tcs.TrySetResult();
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
        
        if (callback != null)
        {
            // Offload callbacks to avoid stack dives on sync completions.
            var ignored = Task.Run(() =>
                	{
                        try
                        {
                            callback(tcs.Task);
                        }
                        catch (Exception)
                        {
                            // Suppress exceptions on background threads.
                        }
                    });
        }
    }
    
    public override void EndWrite(IAsyncResult asyncResult)
    {
        if (asyncResult == null)
        {
            throw new ArgumentNullException(nameof(asyncResult));
        }
        
        var task = (Task)asyncResult;
        task.GetAwaiter().GetResult();
    }
    
    public override async Task WriteAsync(
        byte[] buffer, 
        int offset, 
        int count, 
        CancellationToken cancellationToken)
    {
        OnWrite();
        
        if (_compressionStream != null)
        {
            await _compressionStream.WriteAsync(
                buffer, 
                offset, 
                count, 
                cancellationToken);
            
            if (_autoFlush)
            {
                await _compressionStream.FlushAsync(cancellationToken);
            }
        }
        else
        {
            await _innerStream.WriteAsync(
                buffer, 
                offset, 
                count, 
                cancellationToken);
        }
    }
    
    private void InitializeCompressionHeaders()
    {
        if (_provider.ShouldCompressResponse(_context))
        {
            // If the MIME type indicates that the response could be compressed, 
            // caches will need to vary by the Accept-Encoding header
            var varyValues = _context.Response
                .Headers
                .GetCommaSeparatedValues(HeaderNames.Vary);
            
            var varyByAcceptEncoding = false;
            
            for (var i = 0; i < varyValues.Length; i++)
            {
                if (string.Equals(
                    varyValues[i], 
                    HeaderNames.AcceptEncoding, 
                    StringComparison.OrdinalIgnoreCase))
                {
                    varyByAcceptEncoding = true;
                    break;
                }
            }
            
            if (!varyByAcceptEncoding)
            {
                _context.Response
                    .Headers
                    .Append(
                    HeaderNames.Vary, 
                    HeaderNames.AcceptEncoding);
            }
            
            var compressionProvider = ResolveCompressionProvider();
            if (compressionProvider != null)
            {
                _context.Response
                    .Headers
                    .Append(
                    HeaderNames.ContentEncoding, 
                    compressionProvider.EncodingName);
                // Reset the MD5 because the content changed.
                _context.Response
                    .Headers
                    .Remove(HeaderNames.ContentMD5); 
                
                _context.Response
                    .Headers
                    .Remove(HeaderNames.ContentLength);
            }
        }
    }
    
    private void OnWrite()
    {
        if (!_compressionChecked)
        {
            _compressionChecked = true;
            
            InitializeCompressionHeaders();
            
            if (_compressionProvider != null)
            {
                _compressionStream = _compressionProvider.CreateStream(_innerStream);
            }
        }
    }
    
    private ICompressionProvider? ResolveCompressionProvider()
    {
        if (!_providerCreated)
        {
            _providerCreated = true;
            _compressionProvider = _provider.GetCompressionProvider(_context);
        }
        
        return _compressionProvider;
    }
    
    // For this to be effective it needs to be called before the first write.
    public void DisableBuffering()
    {
        if (ResolveCompressionProvider()?.SupportsFlush == false)
        {
            // Don't compress, some of the providers don't implement Flush 
            // (e.g. .NET 4.5.1 GZip/Deflate stream) which would block real-time 
            // responses like SignalR.
            _compressionChecked = true;
        }
        else
        {
            _autoFlush = true;
        }
        
        _innerBodyFeature.DisableBuffering();
    }
    
    public Task SendFileAsync(
        string path, 
        long offset, 
        long? count, 
        CancellationToken cancellation)
    {
        OnWrite();
        
        if (_compressionStream != null)
        {
            return SendFileFallback.SendFileAsync(
                Stream, 
                path, 
                offset, 
                count, 
                cancellation);
        }
        
        return _innerBodyFeature.SendFileAsync(
            path, 
            offset, 
            count, 
            cancellation);
    }
    
    public Task StartAsync(CancellationToken token = default)
    {
        OnWrite();
        return _innerBodyFeature.StartAsync(token);
    }
    
    public async Task CompleteAsync()
    {
        if (_complete)
        {
            return;
        }
        
        await FinishCompressionAsync(); // Sets _complete
        await _innerBodyFeature.CompleteAsync();
    }
}

```

### 3. resource

#### 3.1 

#### 3.2 static file

##### 3.2.1 use static file

```c#
public static class StaticFileExtensions
{     
    public static IApplicationBuilder UseStaticFiles(this IApplicationBuilder app)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        
        return app.UseMiddleware<StaticFileMiddleware>();
    }
    
    public static IApplicationBuilder UseStaticFiles(
        this IApplicationBuilder app, 
        string requestPath)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        
        return app.UseStaticFiles(new StaticFileOptions
                                  {
                                      RequestPath = new PathString(requestPath)
                                  });
    }
    
    public static IApplicationBuilder UseStaticFiles(
        this IApplicationBuilder app, 
        StaticFileOptions options)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        return app.UseMiddleware<StaticFileMiddleware>(Options.Create(options));
    }
}

```

###### 3.2.1.1 static file options

```c#
public class StaticFileOptions : SharedOptionsBase
{
    public IContentTypeProvider ContentTypeProvider { get; set; } = default!;       
    public string? DefaultContentType { get; set; }       
    public bool ServeUnknownFileTypes { get; set; }      
    public HttpsCompressionMode HttpsCompression { get; set; } = HttpsCompressionMode.Compress;
       
    public Action<StaticFileResponseContext> OnPrepareResponse { get; set; }
    
    public StaticFileOptions() : this(new SharedOptions())
    {
    }
        
    public StaticFileOptions(SharedOptions sharedOptions) : base(sharedOptions)
    {
        OnPrepareResponse = _ => { };
    }           
}

```

###### 3.2.1.2 static file middleware

```c#
public class StaticFileMiddleware
{
    private readonly StaticFileOptions _options;
    private readonly PathString _matchUrl;
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;
    private readonly IFileProvider _fileProvider;
    private readonly IContentTypeProvider _contentTypeProvider;
        
    public StaticFileMiddleware(
        RequestDelegate next, 
        IWebHostEnvironment hostingEnv, 
        IOptions<StaticFileOptions> options, 
        ILoggerFactory loggerFactory)
    {
        if (next == null)
        {
            throw new ArgumentNullException(nameof(next));
        }        
        if (hostingEnv == null)
        {
            throw new ArgumentNullException(nameof(hostingEnv));
        }        
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }        
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }
        
        _next = next;
        _options = options.Value;
        
        // content type provider
        _contentTypeProvider = _options.ContentTypeProvider 
            ?? new FileExtensionContentTypeProvider();
        // file provider
        _fileProvider = _options.FileProvider 
            ?? Helpers.ResolveFileProvider(hostingEnv);
        
        _matchUrl = _options.RequestPath;
        
        _logger = loggerFactory.CreateLogger<StaticFileMiddleware>();
    }
    
        
    public Task Invoke(HttpContext context)
    {
        // 1- 如果有 endpoint，
        if (!ValidateNoEndpoint(context))
        {
            _logger.EndpointMatched();
        }
        // 2- 如果 http request method 不是 valid，
        else if (!ValidateMethod(context))
        {
            _logger.RequestMethodNotSupported(context.Request.Method);
        }
        // 3- 如果 http request path 不是 valid
        else if (!ValidatePath(context, _matchUrl, out var subPath))
        {
            _logger.PathMismatch(subPath);
        }
        // 4- 如果不能解析 content type
        else if (!LookupContentType(
            		_contentTypeProvider, 
            		_options, 
            		subPath, 
            		out var contentType))
        {
            _logger.FileTypeNotSupported(subPath);
        }
        // 否则，即全满足上述条件，-> 5- try serve file
        else
        {
            // If we get here, we can try to serve the file
            return TryServeStaticFile(context, contentType, subPath);
        }
        
        return _next(context);
    }
    
    // 1- 是否有 endpoint
    private static bool ValidateNoEndpoint(HttpContext context) => 
        context.GetEndpoint() == null;
    
    // 2- 是否是 get/head method    
    private static bool ValidateMethod(HttpContext context)
    {
        return Helpers.IsGetOrHeadMethod(context.Request.Method);
    }
    
    // 3- 是否 validpath
    internal static bool ValidatePath(
        HttpContext context, 
        PathString matchUrl, 
        out PathString subPath) => 
        	Helpers.TryMatchPath(
        		context, 
        		matchUrl, 
        		forDirectory: false, 
        		out subPath);
    
    // 4- 能够解析 content type
    internal static bool LookupContentType(
        IContentTypeProvider contentTypeProvider, 
        StaticFileOptions options, 
        PathString subPath, 
        out string? contentType)
    {
        if (contentTypeProvider.TryGetContentType(
            	subPath.Value!, 
            	out contentType))
        {
            return true;
        }
        
        if (options.ServeUnknownFileTypes)
        {
            contentType = options.DefaultContentType;
            return true;
        }
        
        return false;
    }
    
    // 5- try serve file
    private Task TryServeStaticFile(
        HttpContext context, 
        string? contentType, 
        PathString subPath)
    {
        var fileContext = new StaticFileContext(
            context, 
            _options, 
            _logger, 
            _fileProvider, 
            contentType, 
            subPath);
        
        if (!fileContext.LookupFileInfo())
        {
            _logger.FileNotFound(fileContext.SubPath);
        }
        else
        {
            // If we get here, we can try to serve the file
            return fileContext.ServeStaticFile(context, _next);
        }
        
        return _next(context);
    }
}

```

###### 3.2.1.3 static file context

```c#
internal struct StaticFileContext
{
    private readonly HttpContext _context;
    private readonly StaticFileOptions _options;
    private readonly HttpRequest _request;
    private readonly HttpResponse _response;
    private readonly ILogger _logger;
    private readonly IFileProvider _fileProvider;
    private readonly string _method;
    private readonly string? _contentType;
    
    private IFileInfo _fileInfo;
    private EntityTagHeaderValue? _etag;
    private RequestHeaders? _requestHeaders;
    private ResponseHeaders? _responseHeaders;
    private RangeItemHeaderValue? _range;
    
    private long _length;
    private readonly PathString _subPath;
    private DateTimeOffset _lastModified;
    
    private PreconditionState _ifMatchState;
    private PreconditionState _ifNoneMatchState;
    private PreconditionState _ifModifiedSinceState;
    private PreconditionState _ifUnmodifiedSinceState;
    
    private RequestType _requestType;
    
    public StaticFileContext(
        HttpContext context, 
        StaticFileOptions options, 
        ILogger logger, 
        IFileProvider fileProvider, 
        string? contentType, 
        PathString subPath)
    {
        _context = context;
        _options = options;
        _request = context.Request;
        _response = context.Response;
        _logger = logger;
        _fileProvider = fileProvider;
        _method = _request.Method;
        _contentType = contentType;
        _fileInfo = default!;
        _etag = null;
        _requestHeaders = null;
        _responseHeaders = null;
        _range = null;
        
        _length = 0;
        _subPath = subPath;
        _lastModified = new DateTimeOffset();
        _ifMatchState = PreconditionState.Unspecified;
        _ifNoneMatchState = PreconditionState.Unspecified;
        _ifModifiedSinceState = PreconditionState.Unspecified;
        _ifUnmodifiedSinceState = PreconditionState.Unspecified;
        
        if (HttpMethods.IsGet(_method))
        {
            _requestType = RequestType.IsGet;
        }
        else if (HttpMethods.IsHead(_method))
        {
            _requestType = RequestType.IsHead;
        }
        else
        {
            _requestType = RequestType.Unspecified;
        }
    }
    
    // request header
    private RequestHeaders RequestHeaders => 
        _requestHeaders ??= _request.GetTypedHeaders();    
    // response header
    private ResponseHeaders ResponseHeaders => 
        _responseHeaders ??= _response.GetTypedHeaders();
    
    public bool IsHeadMethod => _requestType.HasFlag(RequestType.IsHead);
    
    public bool IsGetMethod => _requestType.HasFlag(RequestType.IsGet);
    
    public bool IsRangeRequest
    {
        get => _requestType.HasFlag(RequestType.IsRange);
        private set
        {
            if (value)
            {
                _requestType |= RequestType.IsRange;
            }
            else
            {
                _requestType &= ~RequestType.IsRange;
            }
        }
    }
    
    public string SubPath => _subPath.Value!;
    
    public string PhysicalPath => _fileInfo.PhysicalPath;
    
    public bool LookupFileInfo()
    {
        _fileInfo = _fileProvider.GetFileInfo(_subPath.Value);
        if (_fileInfo.Exists)
        {
            _length = _fileInfo.Length;
            
            DateTimeOffset last = _fileInfo.LastModified;
            // Truncate to the second.
            _lastModified = new DateTimeOffset(last.Year, last.Month, last.Day, last.Hour, last.Minute, last.Second, last.Offset).ToUniversalTime();
            
            long etagHash = _lastModified.ToFileTime() ^ _length;
            _etag = new EntityTagHeaderValue('\"' + Convert.ToString(etagHash, 16) + '\"');
        }
        return _fileInfo.Exists;
    }
    
    public void ComprehendRequestHeaders()
    {
        ComputeIfMatch();
        
        ComputeIfModifiedSince();
        
        ComputeRange();
        
        ComputeIfRange();
    }
    
    private void ComputeIfMatch()
    {
        var requestHeaders = RequestHeaders;
        
        // 14.24 If-Match
        var ifMatch = requestHeaders.IfMatch;
        if (ifMatch?.Count > 0)
        {
            _ifMatchState = PreconditionState.PreconditionFailed;
            foreach (var etag in ifMatch)
            {
                if (etag.Equals(EntityTagHeaderValue.Any) || 
                    etag.Compare(_etag, useStrongComparison: true))
                {
                    _ifMatchState = PreconditionState.ShouldProcess;
                    break;
                }
            }
        }
        
        // 14.26 If-None-Match
        var ifNoneMatch = requestHeaders.IfNoneMatch;
        if (ifNoneMatch?.Count > 0)
        {
            _ifNoneMatchState = PreconditionState.ShouldProcess;
            foreach (var etag in ifNoneMatch)
            {
                if (etag.Equals(EntityTagHeaderValue.Any) || 
                    etag.Compare(_etag, useStrongComparison: true))
                {
                    _ifNoneMatchState = PreconditionState.NotModified;
                    break;
                }
            }
        }
    }
    
    private void ComputeIfModifiedSince()
    {
        var requestHeaders = RequestHeaders;
        var now = DateTimeOffset.UtcNow;
        
        // 14.25 If-Modified-Since
        var ifModifiedSince = requestHeaders.IfModifiedSince;
        if (ifModifiedSince.HasValue && ifModifiedSince <= now)
        {
            bool modified = ifModifiedSince < _lastModified;
            _ifModifiedSinceState = modified 
                ? PreconditionState.ShouldProcess 
                : PreconditionState.NotModified;
        }
        
        // 14.28 If-Unmodified-Since
        var ifUnmodifiedSince = requestHeaders.IfUnmodifiedSince;
        if (ifUnmodifiedSince.HasValue && ifUnmodifiedSince <= now)
        {
            bool unmodified = ifUnmodifiedSince >= _lastModified;
            _ifUnmodifiedSinceState = unmodified 
                ? PreconditionState.ShouldProcess 
                : PreconditionState.PreconditionFailed;
        }
    }
    
    private void ComputeIfRange()
    {
        // 14.27 If-Range
        var ifRangeHeader = RequestHeaders.IfRange;
        if (ifRangeHeader != null)
        {
            // If the validator given in the If-Range header field matches the
            // current validator for the selected representation of the target
            // resource, then the server SHOULD process the Range header field as
            // requested.  If the validator does not match, the server MUST ignore
            // the Range header field.
            if (ifRangeHeader.LastModified.HasValue)
            {
                if (_lastModified > ifRangeHeader.LastModified)
                {
                    IsRangeRequest = false;
                }
            }
            else if (_etag != null && 
                     ifRangeHeader.EntityTag != null && 
                     !ifRangeHeader.EntityTag.Compare(_etag, useStrongComparison: true))
            {
                IsRangeRequest = false;
            }
        }
    }
    
    private void ComputeRange()
    {
        // 14.35 Range
        // http://tools.ietf.org/html/draft-ietf-httpbis-p5-range-24
        
        // A server MUST ignore a Range header field received with a request method other
        // than GET.
        if (!IsGetMethod)
        {
            return;
        }
        
        (var isRangeRequest, var range) = 
            RangeHelper.ParseRange(
            	_context, 
            	RequestHeaders, 
            	_length, 
            	_logger);
        
        _range = range;
        IsRangeRequest = isRangeRequest;
    }
    
    public void ApplyResponseHeaders(int statusCode)
    {
        _response.StatusCode = statusCode;
        if (statusCode < 400)
        {
            // these headers are returned for 200, 206, and 304
            // they are not returned for 412 and 416
            if (!string.IsNullOrEmpty(_contentType))
            {
                _response.ContentType = _contentType;
            }
            
            var responseHeaders = ResponseHeaders;
            responseHeaders.LastModified = _lastModified;
            responseHeaders.ETag = _etag;
            responseHeaders.Headers[HeaderNames.AcceptRanges] = "bytes";
        }
        if (statusCode == StatusCodes.Status200OK)
        {
            // this header is only returned here for 200
            // it already set to the returned range for 206
            // it is not returned for 304, 412, and 416
            _response.ContentLength = _length;
        }
        
        _options.OnPrepareResponse(new StaticFileResponseContext(_context, _fileInfo!));
    }
    
    public PreconditionState GetPreconditionState() => 
        GetMaxPreconditionState(
	        _ifMatchState, 
    	    _ifNoneMatchState, 
	        _ifModifiedSinceState, 
    	    _ifUnmodifiedSinceState);
    
    private static PreconditionState GetMaxPreconditionState(params PreconditionState[] states)
    {
        PreconditionState max = PreconditionState.Unspecified;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i] > max)
            {
                max = states[i];
            }
        }
        return max;
    }
    
    public Task SendStatusAsync(int statusCode)
    {
        ApplyResponseHeaders(statusCode);
        
        _logger.Handled(statusCode, SubPath);
        return Task.CompletedTask;
    }
    
    public async Task ServeStaticFile(
        HttpContext context, 
        RequestDelegate next)
    {
        ComprehendRequestHeaders();
        switch (GetPreconditionState())
        {
            case PreconditionState.Unspecified:
                
            case PreconditionState.ShouldProcess:
                if (IsHeadMethod)
                {
                    await SendStatusAsync(StatusCodes.Status200OK);
                    return;
                }                
                try
                {
                    if (IsRangeRequest)
                    {
                        await SendRangeAsync();
                        return;
                    }
                    
                    await SendAsync();
                    _logger.FileServed(SubPath, PhysicalPath);
                    return;
                }
                catch (FileNotFoundException)
                {
                    context.Response.Clear();
                }
                await next(context);
                return;
                
            case PreconditionState.NotModified:
                _logger.FileNotModified(SubPath);
                await SendStatusAsync(StatusCodes.Status304NotModified);
                return;
                
            case PreconditionState.PreconditionFailed:
                _logger.PreconditionFailed(SubPath);
                await SendStatusAsync(StatusCodes.Status412PreconditionFailed);
                return;
                
            default:
                var exception = new NotImplementedException(GetPreconditionState().ToString());
                Debug.Fail(exception.ToString());
                throw exception;
        }
    }
    
    public async Task SendAsync()
    {
        SetCompressionMode();
        ApplyResponseHeaders(StatusCodes.Status200OK);
        try
        {
            await _context.Response
                		  .SendFileAsync(
                			   _fileInfo, 
                			   0,
                			   _length, 
                			   _context.RequestAborted);
        }
        catch (OperationCanceledException ex)
        {
            // Don't throw this exception, 
            // it's most likely caused by the client disconnecting.
            _logger.WriteCancelled(ex);
        }
    }
    
    // When there is only a single range the bytes are sent directly in the body.
    internal async Task SendRangeAsync()
    {
        if (_range == null)
        {
            // 14.16 Content-Range - A server sending a response with status code 
            // 416 (Requested range not satisfiable)
            // SHOULD include a Content-Range field with a byte-range-resp-spec of "*". 
            // The instance-length specifies the current length of the selected resource.  
            // e.g. */length
            ResponseHeaders.ContentRange = new ContentRangeHeaderValue(_length);
            ApplyResponseHeaders(StatusCodes.Status416RangeNotSatisfiable);
            
            _logger.RangeNotSatisfiable(SubPath);
            return;
        }
        
        ResponseHeaders.ContentRange = ComputeContentRange(
            _range, 
            out var start, 
            out var length);
        
        _response.ContentLength = length;
        SetCompressionMode();
        ApplyResponseHeaders(StatusCodes.Status206PartialContent);
        
        try
        {
            var logPath = !string.IsNullOrEmpty(_fileInfo.PhysicalPath) 
                ? _fileInfo.PhysicalPath 
                : SubPath;
            
            _logger.SendingFileRange(
                _response.Headers[HeaderNames.ContentRange], 
                logPath);
            
            await _context.Response
                		  .SendFileAsync(
                			   _fileInfo, 
                			   start, 
                			   length, 
                			   _context.RequestAborted);
        }
        catch (OperationCanceledException ex)
        {
            // Don't throw this exception, 
            // it's most likely caused by the client disconnecting.
            _logger.WriteCancelled(ex);
        }
    }
    
    // Note: This assumes ranges have been normalized to absolute byte offsets.
    private ContentRangeHeaderValue ComputeContentRange(
        RangeItemHeaderValue range, 
        out long start, 
        out long length)
    {
        start = range.From!.Value;
        var end = range.To!.Value;
        length = end - start + 1;
        return new ContentRangeHeaderValue(start, end, _length);
    }
    
    // Only called when we expect to serve the body.
    private void SetCompressionMode()
    {
        var responseCompressionFeature = _context.Features
            									 .Get<IHttpsCompressionFeature>();
        if (responseCompressionFeature != null)
        {
            responseCompressionFeature.Mode = _options.HttpsCompression;
        }
    }
    
    internal enum PreconditionState : byte
    {
        Unspecified,
        NotModified,
        ShouldProcess,
        PreconditionFailed
    }
    
    [Flags]
    private enum RequestType : byte
    {
        Unspecified = 0b_000,
        IsHead = 0b_001,
        IsGet = 0b_010,
        IsRange = 0b_100,
    }
}

```

###### 3.2.1.4 static file response context

```c#
public class StaticFileResponseContext
{
    public HttpContext Context { get; }        
    public IFileInfo File { get; }
    
    public StaticFileResponseContext(
        HttpContext context, 
        IFileInfo file)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        File = file ?? throw new ArgumentNullException(nameof(file));
    }            
}

```

##### 3.2.2 for endpoint

```c#
public static class StaticFilesEndpointRouteBuilderExtensions
{        
    // "MapFallbackToFile(IEndpointRouteBuilder, string)" is intended to handle cases 
    // where URL path of the request does not contain a filename, and no other endpoint 
    // has matched. 
    // This is convenient for routing requests for dynamic content to a SPA framework, 
    // while also allowing requests for non-existent files to result in an HTTP 404.
        
    public static IEndpointConventionBuilder MapFallbackToFile(
        this IEndpointRouteBuilder endpoints,
        string filePath)
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }        
        if (filePath == null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }
        
        return endpoints.MapFallback(
            CreateRequestDelegate(endpoints, filePath));
    }
                
    public static IEndpointConventionBuilder MapFallbackToFile(
        this IEndpointRouteBuilder endpoints,
        string filePath,
        StaticFileOptions options)
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }        
        if (filePath == null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }
        
        return endpoints.MapFallback(
            CreateRequestDelegate(endpoints, filePath, options));
    }

        
    public static IEndpointConventionBuilder MapFallbackToFile(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        string filePath)
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }        
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }        
        if (filePath == null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }
        
        return endpoints.MapFallback(
            pattern, 
            CreateRequestDelegate(endpoints, filePath));
    }
    
        
    public static IEndpointConventionBuilder MapFallbackToFile(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        string filePath,
        StaticFileOptions options)
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }        
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }        
        if (filePath == null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }
        
        return endpoints.MapFallback(
            pattern, 
            CreateRequestDelegate(endpoints, filePath, options));
    }
    
    private static RequestDelegate CreateRequestDelegate(
        IEndpointRouteBuilder endpoints,
        string filePath,
        StaticFileOptions? options = null)
    {
        var app = endpoints.CreateApplicationBuilder();
        
        app.Use(next => 
        	context =>
                {
                    context.Request.Path = "/" + filePath;
                    
                    // Set endpoint to null so the static files middleware will 
                    // handle the request.
                    context.SetEndpoint(null);
                    
                    return next(context);
                });
        
        /* 转向 use static file 中间件 */
        
        if (options == null)
        {
            app.UseStaticFiles();
        }
        else
        {
            app.UseStaticFiles(options);
        }
        
        return app.Build();
    }
}

```

#### 3.3 default file

##### 3.3.1 use default file

```c#
public static class DefaultFilesExtensions
{
   
    public static IApplicationBuilder UseDefaultFiles(this IApplicationBuilder app)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        
        return app.UseMiddleware<DefaultFilesMiddleware>();
    }
    
    
    public static IApplicationBuilder UseDefaultFiles(
        this IApplicationBuilder app, 
        string requestPath)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        
        return app.UseDefaultFiles(
            new DefaultFilesOptions
            {
                RequestPath = new PathString(requestPath)
            });
    }
    
    
    public static IApplicationBuilder UseDefaultFiles(
        this IApplicationBuilder app, 
        DefaultFilesOptions options)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        return app.UseMiddleware<DefaultFilesMiddleware>(Options.Create(options));
    }
}

```

###### 3.3.1.1 default file options?

```c#

```

###### 3.3.1.2 default file middleware

```c#
public class DefaultFilesMiddleware
{
    private readonly DefaultFilesOptions _options;
    private readonly PathString _matchUrl;
    private readonly RequestDelegate _next;
    private readonly IFileProvider _fileProvider;
            
    public DefaultFilesMiddleware(
        RequestDelegate next, 
        IWebHostEnvironment hostingEnv, 
        IOptions<DefaultFilesOptions> options)
    {
        if (next == null)
        {
            throw new ArgumentNullException(nameof(next));
        }        
        if (hostingEnv == null)
        {
            throw new ArgumentNullException(nameof(hostingEnv));
        }        
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        _next = next;
        _options = options.Value;
        _fileProvider = _options.FileProvider ?? Helpers.ResolveFileProvider(hostingEnv);
        _matchUrl = _options.RequestPath;
    }
    
        
    public Task Invoke(HttpContext context)
    {
        if (context.GetEndpoint() == null && 
            Helpers.IsGetOrHeadMethod(context.Request.Method) && 
            Helpers.TryMatchPath(
                context, 
                _matchUrl, 
                forDirectory: true, 
                subpath: out var subpath))
        {
            var dirContents = _fileProvider.GetDirectoryContents(subpath.Value);
            if (dirContents.Exists)
            {
                // Check if any of our default files exist.
                for (int matchIndex = 0; 
                     matchIndex < _options.DefaultFileNames.Count; 
                     matchIndex++)
                {
                    string defaultFile = _options.DefaultFileNames[matchIndex];
                    var file = _fileProvider.GetFileInfo(subpath.Value + defaultFile);
                    // TryMatchPath will make sure subpath always ends with a "/" by 
                    // adding it if needed.
                    if (file.Exists)
                    {
                        // If the path matches a directory but does not end in a slash, 
                        // redirect to add the slash.
                        // This prevents relative links from breaking.
                        if (_options.RedirectToAppendTrailingSlash && 
                            !Helpers.PathEndsInSlash(context.Request.Path))
                        {
                            Helpers.RedirectToPathWithSlash(context);
                            return Task.CompletedTask;
                        }
                        // Match found, re-write the url. 
                        // A later middleware will actually serve the file.
                        context.Request.Path = new PathString(
                            Helpers.GetPathValueWithSlash(context.Request.Path) + defaultFile);
                        break;
                    }
                }
            }
        }
        
        return _next(context);
    }
}

```

#### 3.4 directory browser

##### 3.4.1 add directory browser

```c#
public static class DirectoryBrowserServiceExtensions
{    
    public static IServiceCollection AddDirectoryBrowser(this IServiceCollection services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        services.AddWebEncoders();        
        return services;
    }
}

```

##### 3.4.2 use directory browser

```c#
public static class DirectoryBrowserExtensions
{    
    public static IApplicationBuilder UseDirectoryBrowser(this IApplicationBuilder app)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        
        return app.UseMiddleware<DirectoryBrowserMiddleware>();
    }
    
    
    public static IApplicationBuilder UseDirectoryBrowser(
        this IApplicationBuilder app, 
        string requestPath)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        
        return app.UseDirectoryBrowser(
            new DirectoryBrowserOptions
            {
                RequestPath = new PathString(requestPath)
            });
    }
        
    public static IApplicationBuilder UseDirectoryBrowser(
        this IApplicationBuilder app, 
        DirectoryBrowserOptions options)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        return app.UseMiddleware<DirectoryBrowserMiddleware>(Options.Create(options));
    }
}

```

###### 3.4.2.1 directory browser options

```c#
public class DirectoryBrowserOptions : SharedOptionsBase
{
    public IDirectoryFormatter? Formatter { get; set; }
    
    public DirectoryBrowserOptions() : this(new SharedOptions())
    {
    }
        
    public DirectoryBrowserOptions(SharedOptions sharedOptions) : base(sharedOptions)
    {
    }    
}

```

###### 3.4.2.2 directory browser middleware

```c#
public class DirectoryBrowserMiddleware
{
    private readonly DirectoryBrowserOptions _options;
    private readonly PathString _matchUrl;
    private readonly RequestDelegate _next;
    private readonly IDirectoryFormatter _formatter;
    private readonly IFileProvider _fileProvider;
            
    public DirectoryBrowserMiddleware(
        RequestDelegate next, 
        IWebHostEnvironment hostingEnv, 
        IOptions<DirectoryBrowserOptions> options) : 
    		this(
                next, 
                hostingEnv, 
                HtmlEncoder.Default, 
                options)
    {
    }
            
    public DirectoryBrowserMiddleware(
        RequestDelegate next, 
        IWebHostEnvironment hostingEnv, 
        HtmlEncoder encoder, 
        IOptions<DirectoryBrowserOptions> options)
    {
        if (next == null)
        {
            throw new ArgumentNullException(nameof(next));
        }        
        if (hostingEnv == null)
        {
            throw new ArgumentNullException(nameof(hostingEnv));
        }        
        if (encoder == null)
        {
            throw new ArgumentNullException(nameof(encoder));
        }        
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        _next = next;
        _options = options.Value;
        _fileProvider = _options.FileProvider ?? Helpers.ResolveFileProvider(hostingEnv);
        _formatter = _options.Formatter ?? new HtmlDirectoryFormatter(encoder);
        _matchUrl = _options.RequestPath;
    }
            
    public Task Invoke(HttpContext context)
    {
        // Check if the URL matches any expected paths, skip if an endpoint was selected
        
        //如果没有 endpoint，是 get/head 方法，匹配 path，能够解析到 directory info
        if (context.GetEndpoint() == null && 
            Helpers.IsGetOrHeadMethod(context.Request.Method) && 
            Helpers.TryMatchPath(
                context, 
                _matchUrl, 
                forDirectory: true, 
                subpath: out var subpath) && 
            TryGetDirectoryInfo(subpath, out var contents))
        {
            // If the path matches a directory but does not end in a slash, 
            // redirect to add the slash.
            // This prevents relative links from breaking.
            if (_options.RedirectToAppendTrailingSlash && 
                !Helpers.PathEndsInSlash(context.Request.Path))
            {
                Helpers.RedirectToPathWithSlash(context);
                return Task.CompletedTask;
            }
            
            return _formatter.GenerateContentAsync(context, contents);
        }
        
        return _next(context);
    }
    
    private bool TryGetDirectoryInfo(
        PathString subpath, 
        out IDirectoryContents contents)
    {
        contents = _fileProvider.GetDirectoryContents(subpath.Value);
        return contents.Exists;
    }
}

```

#### 3.5 file server

##### 3.5.1 use file server

```c#
public static class FileServerExtensions
{    
    public static IApplicationBuilder UseFileServer(this IApplicationBuilder app)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        
        return app.UseFileServer(new FileServerOptions());
    }
        
    public static IApplicationBuilder UseFileServer(
        this IApplicationBuilder app, 
        bool enableDirectoryBrowsing)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        
        return app.UseFileServer(
            new FileServerOptions
            {
                EnableDirectoryBrowsing = enableDirectoryBrowsing
            });
    }
            
    public static IApplicationBuilder UseFileServer(
        this IApplicationBuilder app, 
        string requestPath)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        
        if (requestPath == null)
        {
            throw new ArgumentNullException(nameof(requestPath));
        }
        
        return app.UseFileServer(
            new FileServerOptions
            {
                RequestPath = new PathString(requestPath)
            });
    }
            
    public static IApplicationBuilder UseFileServer(
        this IApplicationBuilder app, 
        FileServerOptions options)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        if (options.EnableDefaultFiles)
        {
            app.UseDefaultFiles(options.DefaultFilesOptions);
        }
        
        if (options.EnableDirectoryBrowsing)
        {
            app.UseDirectoryBrowser(options.DirectoryBrowserOptions);
        }
        
        return app.UseStaticFiles(options.StaticFileOptions);
    }
}

```

###### 3.5.1.1 file server options

```c#
public class FileServerOptions : SharedOptionsBase
{
    public StaticFileOptions StaticFileOptions { get; private set; }        
    public DirectoryBrowserOptions DirectoryBrowserOptions { get; private set; }        
    public DefaultFilesOptions DefaultFilesOptions { get; private set; }     
    
    public bool EnableDirectoryBrowsing { get; set; }        
    public bool EnableDefaultFiles { get; set; }
    
    public FileServerOptions() : base(new SharedOptions())
    {
        StaticFileOptions = new StaticFileOptions(SharedOptions);
        DirectoryBrowserOptions = new DirectoryBrowserOptions(SharedOptions);
        DefaultFilesOptions = new DefaultFilesOptions(SharedOptions);
        EnableDefaultFiles = true;
    }            
}

```

#### 3.6 spa static file

##### 3.6.1 add static file

```c#
public static class SpaStaticFilesExtensions
{    
    public static void AddSpaStaticFiles(
        this IServiceCollection services,
        Action<SpaStaticFilesOptions>? configuration = null)
    {
        services.AddSingleton<ISpaStaticFileProvider>(serviceProvider =>
        	{
                // 解析 spa static file options
                var optionsProvider = 
                    serviceProvider.GetService<IOptions<SpaStaticFilesOptions>>()!;
                var options = optionsProvider.Value;
                
                // 配置 spa static file options
                configuration?.Invoke(options);

                // 如果 spa static file options 中 root path 为空，-> 抛出异常
                if (string.IsNullOrEmpty(options.RootPath))
                {
                    throw new InvalidOperationException(
                        $"No {nameof(SpaStaticFilesOptions.RootPath)} " +
                        $"was set on the {nameof(SpaStaticFilesOptions)}.");
                }

                return new DefaultSpaStaticFileProvider(serviceProvider, options);
            });
    }                   
}

```

###### 3.6.1.1 spa static file options

```c#
public class SpaStaticFilesOptions
{    
    public string RootPath { get; set; } = default!;
}

```

###### 3.6.1.2 default spa static file provider

```c#
// 接口
public interface ISpaStaticFileProvider
{    
    IFileProvider? FileProvider { get; }
}

// default provider
internal class DefaultSpaStaticFileProvider : ISpaStaticFileProvider
{
    private IFileProvider? _fileProvider;
    public IFileProvider? FileProvider => _fileProvider;
    
    public DefaultSpaStaticFileProvider(
        IServiceProvider serviceProvider,
        SpaStaticFilesOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        if (string.IsNullOrEmpty(options.RootPath))
        {
            throw new ArgumentException(
                $"The {nameof(options.RootPath)} property " +
                $"of {nameof(options)} cannot be null or empty.");
        }
        
        // 解析 web host environment
        var env = serviceProvider.GetRequiredService<IWebHostEnvironment>();
        
        // 合并 web host environment 的 content root path，
        // 和 spa static file options 的 root path
        var absoluteRootPath = Path.Combine(
            env.ContentRootPath,
            options.RootPath);
        
        // PhysicalFileProvider will throw if you pass a non-existent path,
        // but we don't want that scenario to be an error because for SPA
        // scenarios, it's better if non-existing directory just means we
        // don't serve any static files.
        if (Directory.Exists(absoluteRootPath))
        {
            _fileProvider = new PhysicalFileProvider(absoluteRootPath);
        }
    }        
}

```

##### 3.6.2 use spa static file

```c#
public static class SpaStaticFilesExtensions
{        
    public static void UseSpaStaticFiles(this IApplicationBuilder applicationBuilder)
    {
        UseSpaStaticFiles(applicationBuilder, new StaticFileOptions());
    }
           
    public static void UseSpaStaticFiles(
        this IApplicationBuilder applicationBuilder, 
        StaticFileOptions options)
    {
        if (applicationBuilder == null)
        {
            throw new ArgumentNullException(nameof(applicationBuilder));
        }        
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        UseSpaStaticFilesInternal(
            applicationBuilder,            
            staticFileOptions: options,
            allowFallbackOnServingWebRootFiles: false);
    }
    
    internal static void UseSpaStaticFilesInternal(
        this IApplicationBuilder app,
        StaticFileOptions staticFileOptions,
        bool allowFallbackOnServingWebRootFiles)
    {
        if (staticFileOptions == null)
        {
            throw new ArgumentNullException(nameof(staticFileOptions));
        }
        
        // If the file provider was explicitly supplied, that takes precedence over 
        // any other configured file provider. This is most useful if the application 
        // hosts multiple SPAs (via multiple calls to UseSpa()), so each needs to serve 
        // its own separate static files instead of using 
        // AddSpaStaticFiles/UseSpaStaticFiles.
        // But if no file provider was specified, try to get one from the DI config.
        
        // 如果 static file options 中 file provider 为 null，       
        if (staticFileOptions.FileProvider == null)
        {
            // 判断是否可以 serve static file（创建 file provider）
            var shouldServeStaticFiles = ShouldServeStaticFiles(
                app,
                allowFallbackOnServingWebRootFiles,
                out var fileProviderOrDefault);
            
            // 将 file provider 注入 static file options
            if (shouldServeStaticFiles)
            {
                staticFileOptions.FileProvider = fileProviderOrDefault;
            }
            else
            {
                // The registered ISpaStaticFileProvider says we shouldn't
                // serve static files
                return;
            }
        }
        
        app.UseStaticFiles(staticFileOptions);
    }
    
    private static bool ShouldServeStaticFiles(
        IApplicationBuilder app,
        bool allowFallbackOnServingWebRootFiles,
        out IFileProvider? fileProviderOrDefault)
    {
        var spaStaticFilesService = app.ApplicationServices
            						   .GetService<ISpaStaticFileProvider>();
        if (spaStaticFilesService != null)
        {
            // If an ISpaStaticFileProvider was configured but it says no IFileProvider 
            // is available (i.e., it supplies 'null'), this implies we should not serve 
            // any static files. This is typically the case in development when SPA static 
            // files are being served from a SPA development server (e.g., Angular CLI or 
            // create-react-app), in which case no directory of prebuilt files will exist 
            // on disk.
            fileProviderOrDefault = spaStaticFilesService.FileProvider;
            return fileProviderOrDefault != null;
        }
        else if (!allowFallbackOnServingWebRootFiles)
        {
            throw new InvalidOperationException(
                $"To use {nameof(UseSpaStaticFiles)}, you must " +
                $"first register an {nameof(ISpaStaticFileProvider)} in the service 
                "provider, typically " +
                $"by calling services.{nameof(AddSpaStaticFiles)}.");
        }
        else
        {
            // Fall back on serving wwwroot
            fileProviderOrDefault = null;
            return true;
        }
    }
}

```

#### 3.7 spa default

##### 3.7.1 use spa default page

```c#

```



### 4. spa

#### 4.1 spa builder

##### 4.1.1 抽象

###### 4.1.1.1 接口

```c#
public interface ISpaBuilder
{    
    IApplicationBuilder ApplicationBuilder { get; }        
    SpaOptions Options { get; }
}

```

###### 4.1.1.2 spa options

```c#
public class SpaOptions
{
    private PathString _defaultPage = "/index.html";
    public PathString DefaultPage
    {
        get => _defaultPage;
        set
        {
            if (string.IsNullOrEmpty(value.Value))
            {
                throw new ArgumentException(
                    $"The value for {nameof(DefaultPage)} cannot be null or empty.");
            }
            
            _defaultPage = value;
        }
    }  
    
    private string _packageManagerCommand = "npm";    
    public string PackageManagerCommand
    {
        get => _packageManagerCommand;
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(
                    $"The value for {nameof(PackageManagerCommand)} cannot be null or empty.");
            }
            
            _packageManagerCommand = value;
        }
    }
    
    public StaticFileOptions? DefaultPageStaticFileOptions { get; set; }        
    public string? SourcePath { get; set; }        
    public int DevServerPort { get; set; } = default(int);
          
    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(120);
                
    public SpaOptions()
    {
    }
           
    internal SpaOptions(SpaOptions copyFromOptions)
    {
        _defaultPage = copyFromOptions.DefaultPage;
        _packageManagerCommand = copyFromOptions.PackageManagerCommand;
        DefaultPageStaticFileOptions = copyFromOptions.DefaultPageStaticFileOptions;
        SourcePath = copyFromOptions.SourcePath;
        DevServerPort = copyFromOptions.DevServerPort;
    }                              
}

```

##### 4.1.2  default spa builder

```c#
internal class DefaultSpaBuilder : ISpaBuilder
{
    public IApplicationBuilder ApplicationBuilder { get; }    
    public SpaOptions Options { get; }
    
    public DefaultSpaBuilder(
        IApplicationBuilder applicationBuilder, 
        SpaOptions options)
    {
        ApplicationBuilder = applicationBuilder 
            ?? throw new ArgumentNullException(nameof(applicationBuilder));
        
        Options = options
            ?? throw new ArgumentNullException(nameof(options));
    }
}

```

#### 4.2 default page

##### 4.2.1 use spa (default page)

```c#
public static class SpaApplicationBuilderExtensions
{    
    // Handles all requests from this point in the middleware chain by returning
    // the default page for the Single Page Application (SPA).
    // 
    // This middleware should be placed late in the chain, so that other middleware
    // for serving static files, MVC actions, etc., takes precedence.   
    
    public static void UseSpa(
        this IApplicationBuilder app, 
        Action<ISpaBuilder> configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }
        
        // Use the options configured in DI (or blank if none was configured). 
        // We have to clone it otherwise if you have multiple UseSpa calls, their 
        // configurations would interfere with one another.
        
        // 解析、克隆 spa options
        var optionsProvider = app.ApplicationServices
            					 .GetService<IOptions<SpaOptions>>()!;
        var options = new SpaOptions(optionsProvider.Value);
        
        // 创建、配置 spa builder
        var spaBuilder = new DefaultSpaBuilder(app, options);
        configuration.Invoke(spaBuilder);
        
        // 将 spa default page middleware 附加到 spa builder
        SpaDefaultPageMiddleware.Attach(spaBuilder);
    }
}

```

###### 4.2.1.1 spa default page middleware

```c#
internal class SpaDefaultPageMiddleware
{
    public static void Attach(ISpaBuilder spaBuilder)
    {
        if (spaBuilder == null)
        {
            throw new ArgumentNullException(nameof(spaBuilder));
        }
        
        var app = spaBuilder.ApplicationBuilder;
        var options = spaBuilder.Options;
        
        // Rewrite all requests to the default page
        app.Use(
            (context, next) =>
                {
                    // If we have an Endpoint, then this is a deferred match - just noop.
                    if (context.GetEndpoint() != null)
                    {
                        return next();
                    }
                    
                    context.Request.Path = options.DefaultPage;
                    return next();
                });
        
        // Serve it as a static file
        // Developers who need to host more than one SPA with distinct default pages can
        // override the file provider
        
        // 使用 spa static file 
        app.UseSpaStaticFilesInternal(
            options.DefaultPageStaticFileOptions ?? new StaticFileOptions(),
            allowFallbackOnServingWebRootFiles: true);
        
        // If the default file didn't get served as a static file (usually because it was not
        // present on disk), the SPA is definitely not going to work.
        
        // 如果没有找 static file，-> 抛出异常
        app.Use(
            (context, next) =>
                {
                    // If we have an Endpoint, then this is a deferred match - just noop.
                    if (context.GetEndpoint() != null)
                    {
                        return next();
                    }
                    
                    var message = 
                        "The SPA default page middleware could not return the default page " +
                        $"'{options.DefaultPage}' because it was not found, and no other 
                        "middleware " +
                        "handled the request.\n";
                    
                    // Try to clarify the common scenario where someone runs an application in
                    // Production environment without first publishing the whole application
                    // or at least building the SPA.
                    var hostEnvironment = 
                        (IWebHostEnvironment?)context.RequestServices
                        							 .GetService(typeof(IWebHostEnvironment));
                    
                    if (hostEnvironment != null && hostEnvironment.IsProduction())
                    {
                        message += 
                            "Your application is running in Production mode, so make sure 
                            "it has " +
	                        "been published, or that you have built your SPA manually. 
                            "Alternatively you " +
	                        "may wish to switch to the Development environment.\n";
                    }
                    
                    throw new InvalidOperationException(message);
                });
    }
}

```

#### 4.3 spa proxy

##### 4.3.1 use proxy

```c#
public static class SpaProxyingExtensions
{
    
    // Configures the application to forward incoming requests to a local Single Page
    // Application (SPA) development server. This is only intended to be used during
    // development. 
    // 
    // Do not enable this middleware in production applications.
        
    public static void UseProxyToSpaDevelopmentServer(
        this ISpaBuilder spaBuilder,
        string baseUri)
    {
        UseProxyToSpaDevelopmentServer(
            spaBuilder,
            new Uri(baseUri));
    }
    
    
    public static void UseProxyToSpaDevelopmentServer(
        this ISpaBuilder spaBuilder,
        Uri baseUri)
    {
        UseProxyToSpaDevelopmentServer(
            spaBuilder,
            () => Task.FromResult(baseUri));
    }
    

    public static void UseProxyToSpaDevelopmentServer(
        this ISpaBuilder spaBuilder,
        Func<Task<Uri>> baseUriTaskFactory)
    {
        var applicationBuilder = spaBuilder.ApplicationBuilder;
        var applicationStoppingToken = GetStoppingToken(applicationBuilder);
        
        // Since we might want to proxy WebSockets requests (e.g., by default, 
        // AngularCliMiddleware requires it), enable it for the app
        applicationBuilder.UseWebSockets();
        
        // It's important not to time out the requests, as some of them might be to
        // server-sent event endpoints or similar, where it's expected that the response
        // takes an unlimited time and never actually completes
        var neverTimeOutHttpClient =
            SpaProxy.CreateHttpClientForProxy(Timeout.InfiniteTimeSpan);
        
        // Proxy all requests to the SPA development server
        applicationBuilder.Use(
            async (context, next) =>
            {
                var didProxyRequest = await SpaProxy.PerformProxyRequest(
                    context, 
                    neverTimeOutHttpClient, 
                    baseUriTaskFactory(), 
                    applicationStoppingToken,
                    proxy404s: true);
            });
    }
    
    private static CancellationToken GetStoppingToken(IApplicationBuilder appBuilder)
    {
        var applicationLifetime = appBuilder.ApplicationServices
							    		    .GetRequiredService<IHostApplicationLifetime>();
        
        return applicationLifetime.ApplicationStopping;
    }
}

```

##### 4.3.2 spa proxy

```c#
internal static class SpaProxy
{
    private const int DefaultWebSocketBufferSize = 4096;
    private const int StreamCopyBufferSize = 81920;
    
    // https://github.com/dotnet/aspnetcore/issues/16797
    private static readonly string[] NotForwardedHttpHeaders = new[] 
    { 
        "Connection" 
    };
    
    // Don't forward User-Agent/Accept because of 
    // https://github.com/aspnet/JavaScriptServices/issues/1469
    // Others just aren't applicable in proxy scenarios
    private static readonly string[] NotForwardedWebSocketHeaders = new[] 
    { 
        "Accept", 
        "Connection", 
        "Host", 
        "User-Agent", 
        "Upgrade", 
        "Sec-WebSocket-Key", 
        "Sec-WebSocket-Protocol", 
        "Sec-WebSocket-Version" 
    };
    
    public static HttpClient CreateHttpClientForProxy(TimeSpan requestTimeout)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
        };
        
        return new HttpClient(handler)
        {
            Timeout = requestTimeout
        };
    }
    
    public static async Task<bool> PerformProxyRequest(
        HttpContext context,
        HttpClient httpClient,
        Task<Uri> baseUriTask,
        CancellationToken applicationStoppingToken,
        bool proxy404s)
    {
        // Stop proxying if either the server or client wants to disconnect
        var proxyCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(
            context.RequestAborted,
            applicationStoppingToken).Token;
        
        // We allow for the case where the target isn't known ahead of time, and want to
        // delay proxied requests until the target becomes known. This is useful, for example,
        // when proxying to Angular CLI middleware: we won't know what port it's listening
        // on until it finishes starting up.
        var baseUri = await baseUriTask;
        var targetUri = new Uri(
            baseUri,
            context.Request.Path + context.Request.QueryString);
        
        try
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                await AcceptProxyWebSocketRequest(
                    context, 
                    ToWebSocketScheme(targetUri), 
                    proxyCancellationToken);
                
                return true;
            }
            else
            {
                using (var requestMessage = CreateProxyHttpRequest(context, targetUri))
                    using (var responseMessage = await httpClient.SendAsync(
                        requestMessage,
                        HttpCompletionOption.ResponseHeadersRead,
                        proxyCancellationToken))
                {
                    if (!proxy404s)
                    {
                        if (responseMessage.StatusCode == HttpStatusCode.NotFound)
                        {
                            // We're not proxying 404s, i.e., we want to resume the 
                            // middleware pipeline and let some other middleware handle this.
                            return false;
                        }
                    }
                    
                    await CopyProxyHttpResponse(
                        context, 
                        responseMessage, 
                        proxyCancellationToken);
        
                    return true;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // If we're aborting because either the client disconnected, or the server
            // is shutting down, don't treat this as an error.
            return true;
        }
        catch (IOException)
        {
            // This kind of exception can also occur if a proxy read/write gets interrupted
            // due to the process shutting down.
            return true;
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException(
                $"Failed to proxy the request to {targetUri.ToString()}, because the 
                "request to " +                
                $"the proxy target failed. Check that the proxy target server is running 
                "and " +
                $"accepting requests to {baseUri.ToString()}.\n\n" +
                $"The underlying exception message was '{ex.Message}'." +
                $"Check the InnerException for more details.", 
                ex);
        }
    }
    
    private static HttpRequestMessage CreateProxyHttpRequest(HttpContext context, Uri uri)
    {
        var request = context.Request;
        
        var requestMessage = new HttpRequestMessage();
        var requestMethod = request.Method;
        if (!HttpMethods.IsGet(requestMethod) &&
            !HttpMethods.IsHead(requestMethod) &&
            !HttpMethods.IsDelete(requestMethod) &&
            !HttpMethods.IsTrace(requestMethod))
        {
            var streamContent = new StreamContent(request.Body);
            requestMessage.Content = streamContent;
        }
        
        // Copy the request headers
        foreach (var header in request.Headers)
        {
            if (NotForwardedHttpHeaders.Contains(
                	header.Key, 
                	StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }
            
            if (!requestMessage.Headers
                			   .TryAddWithoutValidation(
                                   header.Key, 
                                   header.Value.ToArray()) && 
                requestMessage.Content != null)
            {
                requestMessage.Content
                    		  ?.Headers
                    		  .TryAddWithoutValidation(
                    			   header.Key, 
                    			   header.Value.ToArray());
            }
        }
        
        requestMessage.Headers.Host = uri.Authority;
        requestMessage.RequestUri = uri;
        requestMessage.Method = new HttpMethod(request.Method);
        
        return requestMessage;
    }
    
    private static async Task CopyProxyHttpResponse(
        HttpContext context, 
        HttpResponseMessage responseMessage, 
        CancellationToken cancellationToken)
    {
        context.Response.StatusCode = (int)responseMessage.StatusCode;
        foreach (var header in responseMessage.Headers)
        {
            context.Response
                   .Headers[header.Key] = header.Value.ToArray();
        }
        
        foreach (var header in responseMessage.Content.Headers)
        {
            context.Response
                   .Headers[header.Key] = header.Value.ToArray();
        }
        
        // SendAsync removes chunking from the response. 
        // This removes the header so it doesn't expect a chunked response.
        context.Response
               .Headers
               .Remove("transfer-encoding");
        
        using (var responseStream = await responseMessage.Content
               											 .ReadAsStreamAsync())
        {
            await responseStream.CopyToAsync(
                context.Response.Body, 
                StreamCopyBufferSize, 
                cancellationToken);
        }
    }
    
    private static Uri ToWebSocketScheme(Uri uri)
    {
        if (uri == null)
        {
            throw new ArgumentNullException(nameof(uri));
        }
        
        var uriBuilder = new UriBuilder(uri);
        if (string.Equals(
            	uriBuilder.Scheme, 
            	"https", 
            	StringComparison.OrdinalIgnoreCase))
        {
            uriBuilder.Scheme = "wss";
        }
        else if (string.Equals(
            		uriBuilder.Scheme, 
            		"http", 
            		StringComparison.OrdinalIgnoreCase))
        {
            uriBuilder.Scheme = "ws";
        }
        
        return uriBuilder.Uri;
    }
    
    private static async Task<bool> AcceptProxyWebSocketRequest(
        HttpContext context, 
        Uri destinationUri, 
        CancellationToken cancellationToken)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (destinationUri == null)
        {
            throw new ArgumentNullException(nameof(destinationUri));
        }
        
        using (var client = new ClientWebSocket())
        {
            foreach (var protocol in context.WebSockets
                     						.WebSocketRequestedProtocols)
            {
                client.Options
                      .AddSubProtocol(protocol);
            }
            
            foreach (var headerEntry in context.Request.Headers)
            {
                if (!NotForwardedWebSocketHeaders.Contains(
                    	headerEntry.Key, 
                    	StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        client.Options
                              .SetRequestHeader(headerEntry.Key, headerEntry.Value);
                    }
                    catch (ArgumentException)
                    {
                        // On net461, certain header names are reserved and can't be set.
                        // We filter out the known ones via the test above, but there could
                        // be others arbitrarily set by the client. It's not helpful to
                        // consider it an error, so just skip non-forwardable headers.
                        // The perf implications of handling this via a catch aren't an
                        // issue since this is a dev-time only feature.
                    }
                }
            }
            
            try
            {
                // Note that this is not really good enough to make Websockets work with
                // Angular CLI middleware. For some reason, ConnectAsync takes over 1 second,
                // on Windows, by which time the logic in SockJS has already timed out and made
                // it fall back on some other transport (xhr_streaming, usually). It's fine
                // on Linux though, completing almost instantly.
                //
                // The slowness on Windows does not cause a problem though, because the 
                // transport fallback logic works correctly and doesn't surface any errors, 
                // but it would be better if ConnectAsync was fast enough and the initial 
                // Websocket transport could actually be used.
                await client.ConnectAsync(destinationUri, cancellationToken);
            }
            catch (WebSocketException)
            {
                context.Response.StatusCode = 400;
                return false;
            }
            
            using (var server = await context.WebSockets
                   							 .AcceptWebSocketAsync(client.SubProtocol))
            {
                var bufferSize = DefaultWebSocketBufferSize;
                
                await Task.WhenAll(
                    PumpWebSocket(client, server, bufferSize, cancellationToken),
                    PumpWebSocket(server, client, bufferSize, cancellationToken));
            }
            
            return true;
        }
    }
    
    private static async Task PumpWebSocket(
        WebSocket source, 
        WebSocket destination, 
        int bufferSize, 
        CancellationToken cancellationToken)
    {
        if (bufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize));
        }
        
        var buffer = new byte[bufferSize];
        
        while (true)
        {
            // Because WebSocket.ReceiveAsync doesn't work well with CancellationToken 
            // (it doesn't actually exit when the token notifies, at least not in the 
            // 'server' case), use polling. The perf might not be ideal, but this is a 
            // dev-time feature only.
            var resultTask = source.ReceiveAsync(
                new ArraySegment<byte>(buffer), 
                cancellationToken);
            
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                
                if (resultTask.IsCompleted)
                {
                    break;
                }
                
                await Task.Delay(100);
            }
            
            var result = resultTask.Result; // We know it's completed already
            if (result.MessageType == WebSocketMessageType.Close)
            {
                if (destination.State == WebSocketState.Open || 
                    destination.State == WebSocketState.CloseReceived)
                {
                    await destination.CloseOutputAsync(
                        source.CloseStatus!.Value, 
                        source.CloseStatusDescription, 
                        cancellationToken);
                }
                
                return;
            }
            
            await destination.SendAsync(
                new ArraySegment<byte>(buffer, 0, result.Count), 
                result.MessageType, 
                result.EndOfMessage, 
                cancellationToken);
        }
    }
}

```

#### 4.4 node script

##### 4.4.1 node script runner

```c#
internal class NodeScriptRunner : IDisposable
{
    private Process? _npmProcess;
    public EventedStreamReader StdOut { get; }
    public EventedStreamReader StdErr { get; }
    
    private static Regex AnsiColorRegex = 
        new Regex(
        		"\x001b\\[[0-9;]*m", 
        		RegexOptions.None, 
        		TimeSpan.FromSeconds(1));
    
    public NodeScriptRunner(
        string workingDirectory, 
        string scriptName, 
        string? arguments, 
        IDictionary<string, string>? envVars, 
        string pkgManagerCommand, 
        DiagnosticSource diagnosticSource, 
        CancellationToken applicationStoppingToken)
    {
        if (string.IsNullOrEmpty(workingDirectory))
        {
            throw new ArgumentException("Cannot be null or empty.", nameof(workingDirectory));
        }        
        if (string.IsNullOrEmpty(scriptName))
        {
            throw new ArgumentException("Cannot be null or empty.", nameof(scriptName));
        }        
        if (string.IsNullOrEmpty(pkgManagerCommand))
        {
            throw new ArgumentException("Cannot be null or empty.", nameof(pkgManagerCommand));
        }
        
        var exeToRun = pkgManagerCommand;
        var completeArguments = $"run {scriptName} -- {arguments ?? string.Empty}";
        if (OperatingSystem.IsWindows())
        {
            // On Windows, the node executable is a .cmd file, so it can't be executed
            // directly (except with UseShellExecute=true, but that's no good, because
            // it prevents capturing stdio). So we need to invoke it via "cmd /c".
            exeToRun = "cmd";
            completeArguments = $"/c {pkgManagerCommand} {completeArguments}";
        }
        
        var processStartInfo = new ProcessStartInfo(exeToRun)
        {
            Arguments = completeArguments,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory
        };
        
        if (envVars != null)
        {
            foreach (var keyValuePair in envVars)
            {
                processStartInfo.Environment[keyValuePair.Key] = keyValuePair.Value;
            }
        }
        
        _npmProcess = LaunchNodeProcess(processStartInfo, pkgManagerCommand);
        StdOut = new EventedStreamReader(_npmProcess.StandardOutput);
        StdErr = new EventedStreamReader(_npmProcess.StandardError);
        
        applicationStoppingToken.Register(((IDisposable)this).Dispose);
        
        if (diagnosticSource.IsEnabled("Microsoft.AspNetCore.NodeServices.Npm.NpmStarted"))
        {
            diagnosticSource.Write(
                "Microsoft.AspNetCore.NodeServices.Npm.NpmStarted",
                new
                {
                    processStartInfo = processStartInfo,
                    process = _npmProcess
                });
        }
    }
    
    public void AttachToLogger(ILogger logger)
    {
        // When the node task emits complete lines, pass them through to the real logger
        StdOut.OnReceivedLine += line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                // Node tasks commonly emit ANSI colors, but it wouldn't make sense to forward
                // those to loggers (because a logger isn't necessarily any kind of terminal)
                logger.LogInformation(StripAnsiColors(line));
            }
        };
        
        StdErr.OnReceivedLine += line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                logger.LogError(StripAnsiColors(line));
            }
        };
        
        // But when it emits incomplete lines, assume this is progress information and
        // hence just pass it through to StdOut regardless of logger config.
        StdErr.OnReceivedChunk += chunk =>
        {
            Debug.Assert(chunk.Array != null);
            
            var containsNewline = 
                Array.IndexOf(
	                chunk.Array, 
    	            '\n', 
        	        chunk.Offset, 
            	    chunk.Count) >= 0;
            
            if (!containsNewline)
            {
                Console.Write(chunk.Array, chunk.Offset, chunk.Count);
            }
        };
    }
    
    private static string StripAnsiColors(string line) => 
        AnsiColorRegex.Replace(line, string.Empty);
    
    private static Process LaunchNodeProcess(
        ProcessStartInfo startInfo, 
        string commandName)
    {
        try
        {
            var process = Process.Start(startInfo)!;
            
            // See equivalent comment in OutOfProcessNodeInstance.cs for why
            process.EnableRaisingEvents = true;
            
            return process;
        }
        catch (Exception ex)
        {
            var message = 
                $"Failed to start '{commandName}'. To resolve this:.\n\n" + 
                $"[1] Ensure that '{commandName}' is installed and can be found in one 
                "of the PATH directories.\n" + 
                $"    Current PATH enviroment variable is: { 
                "Environment.GetEnvironmentVariable("PATH") }\n" + 
                "    Make sure the executable is in one of those directories, or update 
                "your PATH.\n\n" + 
                "[2] See the InnerException for further details of the cause.";
            throw new InvalidOperationException(message, ex);
        }
    }
    
    void IDisposable.Dispose()
    {
        if (_npmProcess != null && !_npmProcess.HasExited)
        {
            _npmProcess.Kill(entireProcessTree: true);
            _npmProcess = null;
        }
    }
}

```

##### 4.4.2 anugular service

###### 4.4.2.1 use angular cli server

```c#
public static class AngularCliMiddlewareExtensions
{    
    // Handles requests by passing them through to an instance of the Angular CLI server.
    // This means you can always serve up-to-date CLI-built resources without having
    // to run the Angular CLI server manually.
    //
    // This feature should only be used in development. For production deployments, be
    // sure not to enable the Angular CLI server.
    
    public static void UseAngularCliServer(
        this ISpaBuilder spaBuilder,
        string npmScript)
    {
        if (spaBuilder == null)
        {
            throw new ArgumentNullException(nameof(spaBuilder));
        }
        
        var spaOptions = spaBuilder.Options;
        
        if (string.IsNullOrEmpty(spaOptions.SourcePath))
        {
            throw new InvalidOperationException(
                $"To use {nameof(UseAngularCliServer)}, you must supply a non-empty 
                "value for the {nameof(SpaOptions.SourcePath)} property of 
                "{nameof(SpaOptions)} when calling 
                "{nameof(SpaApplicationBuilderExtensions.UseSpa)}.");
        }
        
        AngularCliMiddleware.Attach(spaBuilder, npmScript);
    }
}

```

###### 4.4.2.2 angular cli middleware

```c#
internal static class AngularCliMiddleware
{
    private const string LogCategoryName = "Microsoft.AspNetCore.SpaServices";
    
    // This is a development-time only feature, so a very long timeout is fine
    private static TimeSpan RegexMatchTimeout = TimeSpan.FromSeconds(5); 

    public static void Attach(ISpaBuilder spaBuilder, string scriptName)
    {
        var pkgManagerCommand = spaBuilder.Options.PackageManagerCommand;
        var sourcePath = spaBuilder.Options.SourcePath;
        var devServerPort = spaBuilder.Options.DevServerPort;
        
        if (string.IsNullOrEmpty(sourcePath))
        {
            throw new ArgumentException(
                "Cannot be null or empty", 
                nameof(sourcePath));
        }
        
        if (string.IsNullOrEmpty(scriptName))
        {
            throw new ArgumentException(
                "Cannot be null or empty", 
                nameof(scriptName));
        }
        
        // Start Angular CLI and attach to middleware pipeline
        var appBuilder = spaBuilder.ApplicationBuilder;
        var applicationStoppingToken = 
            appBuilder.ApplicationServices
            		  .GetRequiredService<IHostApplicationLifetime>()
            		  .ApplicationStopping;
        var logger = LoggerFinder.GetOrCreateLogger(appBuilder, LogCategoryName);
        var diagnosticSource = 
            appBuilder.ApplicationServices
            		  .GetRequiredService<DiagnosticSource>();
        var angularCliServerInfoTask = 
            StartAngularCliServerAsync(
            	sourcePath, 
            	scriptName, 
            	pkgManagerCommand, 
            	devServerPort, 
            	logger, 
            	diagnosticSource, 
            	applicationStoppingToken);
        
        SpaProxyingExtensions.UseProxyToSpaDevelopmentServer(
            spaBuilder, 
            () =>
            	{
                    // On each request, we create a separate startup task with its 
                    // own timeout. That way, even if the first request times out, 
                    // subsequent requests could still work.
                    var timeout = spaBuilder.Options.StartupTimeout;
                    return angularCliServerInfoTask.WithTimeout(timeout,                    						$"The Angular CLI process did not start listening for requests " +
	                    $"within the timeout period of {timeout.TotalSeconds} seconds. " +
    	                $"Check the log output for error information.");
                });
    }
    
    private static async Task<Uri> StartAngularCliServerAsync(
        string sourcePath, 
        string scriptName, 
        string pkgManagerCommand, 
        int portNumber, 
        ILogger logger, 
        DiagnosticSource diagnosticSource, 
        CancellationToken applicationStoppingToken)
    {
        if (portNumber == default(int))
        {
            portNumber = TcpPortFinder.FindAvailablePort();
        }
        logger.LogInformation($"Starting @angular/cli on port {portNumber}...");
        
        var scriptRunner = new NodeScriptRunner(
            sourcePath, 
            scriptName, 
            $"--port {portNumber}", 
            null, 
            pkgManagerCommand, 
            diagnosticSource, 
            applicationStoppingToken);
        
        scriptRunner.AttachToLogger(logger);
        
        Match openBrowserLine;
        
        using (var stdErrReader = new EventedStreamStringReader(scriptRunner.StdErr))
        {
            try
            {
                openBrowserLine = await scriptRunner.StdOut
                    								.WaitForMatch(
                    new Regex(
                        	"open your browser on (http\\S+)", 
                        	RegexOptions.None, 
                        	RegexMatchTimeout));
            }
            catch (EndOfStreamException ex)
            {
                throw new InvalidOperationException(
                    $"The {pkgManagerCommand} script '{scriptName}' exited without 
                    "indicating that the " +                    
                    $"Angular CLI was listening for requests. The error output was: " +
                    $"{stdErrReader.ReadAsString()}", 
                    ex);
            }
        }
        
        var uri = new Uri(openBrowserLine.Groups[1].Value);
        
        // Even after the Angular CLI claims to be listening for requests, there's a short
        // period where it will give an error if you make a request too quickly
        await WaitForAngularCliServerToAcceptRequests(uri);
        
        return uri;
    }
    
    private static async Task WaitForAngularCliServerToAcceptRequests(Uri cliServerUri)
    {
        // To determine when it's actually ready, try making HEAD requests to '/'. If it
        // produces any HTTP response (even if it's 404) then it's ready. If it rejects the
        // connection then it's not ready. We keep trying forever because this is dev-mode
        // only, and only a single startup attempt will be made, and there's a further level
        // of timeouts enforced on a per-request basis.
        var timeoutMilliseconds = 1000;
        using (var client = new HttpClient())
        {
            while (true)
            {
                try
                {
                    // If we get any HTTP response, the CLI server is ready
                    await client.SendAsync(
                        new HttpRequestMessage(HttpMethod.Head, cliServerUri),
                        new CancellationTokenSource(timeoutMilliseconds).Token);
                    return;
                }
                catch (Exception)
                {
                    await Task.Delay(500);
                    
                    // Depending on the host's networking configuration, the requests 
                    // can take a while to go through, most likely due to the time spent 
                    // resolving 'localhost'.
                    // Each time we have a failure, allow a bit longer next time (up to a 
                    // maximum).
                    // This only influences the time until we regard the dev server as 
                    // 'ready', so it doesn't affect the runtime perf (even in dev mode) 
                    // once the first connection is made.
                    // Resolves https://github.com/aspnet/JavaScriptServices/issues/1611
                    if (timeoutMilliseconds < 10000)
                    {
                        timeoutMilliseconds += 3000;
                    }
                }
            }
        }
    }
}

```

##### 4.4.3 react service

###### 4.4.3.1 use react server

```c#
public static class ReactDevelopmentServerMiddlewareExtensions
{   
    // Handles requests by passing them through to an instance of the create-react-app 
    // server.
    // This means you can always serve up-to-date CLI-built resources without having
    // to run the create-react-app server manually.
    //
    // This feature should only be used in development. For production deployments, be
    // sure not to enable the create-react-app server.
    
    public static void UseReactDevelopmentServer(
        this ISpaBuilder spaBuilder,
        string npmScript)
    {
        if (spaBuilder == null)
        {
            throw new ArgumentNullException(nameof(spaBuilder));
        }
        
        var spaOptions = spaBuilder.Options;
        
        if (string.IsNullOrEmpty(spaOptions.SourcePath))
        {
            throw new InvalidOperationException(
                $"To use {nameof(UseReactDevelopmentServer)}, you must supply a non-empty 
                "value for the {nameof(SpaOptions.SourcePath)} property of 
                "{nameof(SpaOptions)} when calling 
                "{nameof(SpaApplicationBuilderExtensions.UseSpa)}.");
        }
        
        ReactDevelopmentServerMiddleware.Attach(spaBuilder, npmScript);
    }
}

```

###### 4.4.3.2 react middleware

```c#
internal static class ReactDevelopmentServerMiddleware
{
    private const string LogCategoryName = "Microsoft.AspNetCore.SpaServices";
    
    // This is a development-time only feature, so a very long timeout is fine
    private static TimeSpan RegexMatchTimeout = TimeSpan.FromSeconds(5); 

    public static void Attach(
        ISpaBuilder spaBuilder,
        string scriptName)
    {
        var pkgManagerCommand = spaBuilder.Options.PackageManagerCommand;
        var sourcePath = spaBuilder.Options.SourcePath;
        var devServerPort = spaBuilder.Options.DevServerPort;
        if (string.IsNullOrEmpty(sourcePath))
        {
            throw new ArgumentException("Cannot be null or empty", nameof(sourcePath));
        }
        
        if (string.IsNullOrEmpty(scriptName))
        {
            throw new ArgumentException("Cannot be null or empty", nameof(scriptName));
        }
        
        // Start create-react-app and attach to middleware pipeline
        var appBuilder = spaBuilder.ApplicationBuilder;
        var applicationStoppingToken = 
            appBuilder.ApplicationServices
            		  .GetRequiredService<IHostApplicationLifetime>()
            		  .ApplicationStopping;
        var logger = LoggerFinder.GetOrCreateLogger(appBuilder, LogCategoryName);
        var diagnosticSource = 
            appBuilder.ApplicationServices
            		  .GetRequiredService<DiagnosticSource>();
        var portTask = StartCreateReactAppServerAsync(
            sourcePath, 
            scriptName, 
            pkgManagerCommand, 
            devServerPort, 
            logger, 
            diagnosticSource, 
            applicationStoppingToken);

        // Everything we proxy is hardcoded to target http://localhost because:
        // - the requests are always from the local machine (we're not accepting remote
        //   requests that go directly to the create-react-app server)
        // - given that, there's no reason to use https, and we couldn't even if we
        //   wanted to, because in general the create-react-app server has no certificate
        var targetUriTask = portTask.ContinueWith(task => 
                            	new UriBuilder("http", "localhost", task.Result).Uri);
        
        SpaProxyingExtensions.UseProxyToSpaDevelopmentServer(
            spaBuilder, 
            () =>
            	{
                	// On each request, we create a separate startup task with its 
                    // own timeout. 
                    // That way, even if the first request times out, subsequent requests 
                    // could still work.
                    var timeout = spaBuilder.Options
                        					.StartupTimeout;
                    return targetUriTask.WithTimeout(timeout,
                    	$"The create-react-app server did not start listening for requests " +
                    	$"within the timeout period of {timeout.Seconds} seconds. " +
	                    $"Check the log output for error information.");
                });
    }
    
    private static async Task<int> StartCreateReactAppServerAsync(
        string sourcePath, 
        string scriptName, 
        string pkgManagerCommand, 
        int portNumber, 
        ILogger logger, 
        DiagnosticSource diagnosticSource, 
        CancellationToken applicationStoppingToken)
    {
        if (portNumber == default(int))
        {
            portNumber = TcpPortFinder.FindAvailablePort();
        }
        logger.LogInformation($"Starting create-react-app server on port {portNumber}...");
        
        var envVars = new Dictionary<string, string>
        {
            { "PORT", portNumber.ToString(CultureInfo.InvariantCulture) },
            // We don't want create-react-app to open its own extra browser window 
            // pointing to the internal dev server port
            { "BROWSER", "none" }, 
        };
        var scriptRunner = new NodeScriptRunner(
            sourcePath, 
            scriptName, 
            null, 
            envVars, 
            pkgManagerCommand, 
            diagnosticSource, 
            applicationStoppingToken);
        
        scriptRunner.AttachToLogger(logger);
        
        using (var stdErrReader = new EventedStreamStringReader(scriptRunner.StdErr))
        {
            try
            {
                // Although the React dev server may eventually tell us the URL it's 
                // listening on, it doesn't do so until it's finished compiling, and even 
                // then only if there were no compiler warnings. So instead of waiting for
                // that, consider it ready as soon as it starts listening for requests.
                await scriptRunner.StdOut
                    			  .WaitForMatch(
                    new Regex(
                        	"Starting the development server", 
                        	RegexOptions.None, 
                        	RegexMatchTimeout));
            }
            catch (EndOfStreamException ex)
            {
                throw new InvalidOperationException(
                    $"The {pkgManagerCommand} script '{scriptName}' exited without 
                    "indicating that the " +
                    $"create-react-app server was listening for requests. The error 
                    "output was: " +
                    $"{stdErrReader.ReadAsString()}", ex);
            }
        }
        
        return portNumber;
    }
}

```



##### 



##### 4.2.2 conditional middleware?

```c#
internal class ConditionalProxyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly Task<Uri> _baseUriTask;
        private readonly string _pathPrefix;
        private readonly bool _pathPrefixIsRoot;
        private readonly HttpClient _httpClient;
        private readonly CancellationToken _applicationStoppingToken;

        public ConditionalProxyMiddleware(
            RequestDelegate next,
            string pathPrefix,
            TimeSpan requestTimeout,
            Task<Uri> baseUriTask,
            IHostApplicationLifetime applicationLifetime)
        {
            if (!pathPrefix.StartsWith('/'))
            {
                pathPrefix = "/" + pathPrefix;
            }

            _next = next;
            _pathPrefix = pathPrefix;
            _pathPrefixIsRoot = string.Equals(_pathPrefix, "/", StringComparison.Ordinal);
            _baseUriTask = baseUriTask;
            _httpClient = SpaProxy.CreateHttpClientForProxy(requestTimeout);
            _applicationStoppingToken = applicationLifetime.ApplicationStopping;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments(_pathPrefix) || _pathPrefixIsRoot)
            {
                var didProxyRequest = await SpaProxy.PerformProxyRequest(
                    context, _httpClient, _baseUriTask, _applicationStoppingToken, proxy404s: false);
                if (didProxyRequest)
                {
                    return;
                }
            }

            // Not a request we can proxy
            await _next.Invoke(context);
        }
    }
```













