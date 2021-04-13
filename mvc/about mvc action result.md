## about mvc action result



### 1. about



### 2. action result

#### 2.1 action result 抽象

##### 2.1.3 action result type mapper

* 提供给上层服务使用的接口，
* 可以将 value 转换成指定的`action result`

#### 2.4 redirect result

##### 2.4.1 url helper

###### 2.4.1.1 url helper 接口

```c#
public interface IUrlHelper
{    
    ActionContext ActionContext { get; }
    
    // 判断是否为 local url，
    //   1 - absolute path，但是不包含 host / authority    
    //   2 - virtual path（~/开头）         		
    //
    // local url
    //  /Views/Default/Index.html
    // 	~/Index.html
    // 非 local url
    //  ../Index.html
    //  http://www.contoso.com/
    // 	http://localhost/Index.html           
    bool IsLocalUrl([NotNullWhen(true)] string? url);
    
    // 由 url action context 生成 absolute path        
    string? Action(UrlActionContext actionContext);

    // 由 content path 生成 absolute path，
    // 将 virtual path（~/开头）转换为 absolute path    
    [return: NotNullIfNotNull("contentPath")]
    string? Content(string? contentPath);
            
    // 由 url route context 生成 absolute path   
    string? RouteUrl(UrlRouteContext routeContext);

    // 由 route name 生成 absolute path    
    string? Link(string? routeName, object? values);
}

```

###### 2.4.1.2 url helper 扩展

```c#
public static class UrlHelperExtensions
{        
    public static string Action(this IUrlHelper helper)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.Action(
            action: null,
            controller: null,
            values: null,
            protocol: null,
            host: null,
            fragment: null);
    }
            
    public static string Action(
        this IUrlHelper helper, 
        string action)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.Action(
            action, 
            controller: null, 
            values: null, 
            protocol: null, 
            host: null, 
            fragment: null);
    }
        
    public static string Action(
        this IUrlHelper helper, 
        string action, 
        object values)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.Action(
            action, 
            controller: null, 
            values: values, 
            protocol: null, 
            host: null, 
            fragment: null);
    }
        
    public static string Action(
        this IUrlHelper helper, 
        string action, 
        string controller)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.Action(
            action, 
            controller, 
            values: null, 
            protocol: null, 
            host: null, 
            fragment: null);
    }
       
    public static string Action(
        this IUrlHelper helper, 
        string action, 
        string controller, 
        object values)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.Action(
            action, 
            controller, 
            values, 
            protocol: null, 
            host: null, 
            fragment: null);
    }

            
    public static string Action(
        this IUrlHelper helper,
        string action,
        string controller,
        object values,
        string protocol)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.Action(
            action, 
            controller, 
            values, 
            protocol, 
            host: null, 
            fragment: null);
    }
        
    public static string Action(
        this IUrlHelper helper,
        string action,
        string controller,
        object values,
        string protocol,
        string host)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.Action(
            action, 
            controller, 
            values, 
            protocol, 
            host, 
            fragment: null);
    }
                
    public static string Action(
        this IUrlHelper helper,
        string action,
        string controller,
        object values,
        string protocol,
        string host,
        string fragment)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.Action(
            new UrlActionContext()
            {
                Action = action,
                Controller = controller,
                Host = host,                
                Values = values,
                Protocol = protocol,
                Fragment = fragment
            });
    }
    
    /* 扩展 route url */
    
    public static string RouteUrl(
        this IUrlHelper helper, 
        object values)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.RouteUrl(
            routeName: null, 
            values: values, 
            protocol: null, 
            host: null, 
            fragment: null);
    }
            
    public static string RouteUrl(
        this IUrlHelper helper, 
        string routeName)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.RouteUrl(
            routeName, 
            values: null, 
            protocol: null, 
            host: null, 
            fragment: null);
    }
                
    public static string RouteUrl(
        this IUrlHelper helper, 
        string routeName, 
        object values)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.RouteUrl(
            routeName, 
            values, 
            protocol: null, 
            host: null, 
            fragment: null);
    }
                            
    public static string RouteUrl(
        this IUrlHelper helper,
        string routeName,
        object values,
        string protocol)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.RouteUrl(
            routeName, 
            values, 
            protocol, 
            host: null, 
            fragment: null);
    }
            
    public static string RouteUrl(
        this IUrlHelper helper,
        string routeName,
        object values,
        string protocol,
        string host)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.RouteUrl(
            routeName, 
            values, 
            protocol, 
            host, 
            fragment: null);
    }
        
    public static string RouteUrl(
        this IUrlHelper helper,
        string routeName,
        object values,
        string protocol,
        string host,
        string fragment)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.RouteUrl(
            new UrlRouteContext()
            {
                RouteName = routeName,
                Values = values,
                Protocol = protocol,
                Host = host,
                Fragment = fragment
            });
    }
        
    /* 扩展 page */
    
    public static string Page(
        this IUrlHelper urlHelper, 
        string pageName) => 
        	Page(
        		urlHelper, 
        		pageName, 
        		values: null);
        
    public static string Page(
        this IUrlHelper urlHelper, 
        string pageName, 
        string pageHandler) => 
        	Page(
        		urlHelper, 
        		pageName, 
        		pageHandler, 
        		values: null);
       
    public static string Page(
        this IUrlHelper urlHelper, 
        string pageName, 
        object values) => 
        	Page(
        		urlHelper, 
        		pageName, 
        		pageHandler: null, 
        		values: values);
        
    public static string Page(
        this IUrlHelper urlHelper,
        string pageName,
        string pageHandler,
        object values) => 
        	Page(
        		urlHelper, 
        		pageName, 
        		pageHandler, 
        		values, 
        		protocol: null);
        
    public static string Page(
        this IUrlHelper urlHelper,            
        string pageName,
        string pageHandler,
        object values,
        string protocol) => 
        	Page(
        		urlHelper, 
        		pageName, 
        		pageHandler, 
        		values, 
        		protocol, 
        		host: null, 
        		fragment: null);
        
    public static string Page(
        this IUrlHelper urlHelper,
        string pageName,
        string pageHandler,
        object values,
        string protocol,
        string host) => 
        	Page(
        		urlHelper, 
        		pageName, 
        		pageHandler, 
        		values, 
        		protocol, 
        		host, 
        		fragment: null);
        
    public static string Page(
        this IUrlHelper urlHelper,
        string pageName,
        string pageHandler,
        object values,
        string protocol,
        string host,
        string fragment)
    {
        if (urlHelper == null)
        {
            throw new ArgumentNullException(nameof(urlHelper));
        }
        
        var routeValues = new RouteValueDictionary(values);
        var ambientValues = urlHelper.ActionContext.RouteData.Values;
        
        UrlHelperBase.NormalizeRouteValuesForPage(
            			urlHelper.ActionContext, 
            			pageName, 
            			pageHandler, 
            			routeValues, 
            			ambientValues);

        return urlHelper.RouteUrl(
            				routeName: null,
            				values: routeValues,
            				protocol: protocol,
            				host: host,
            				fragment: fragment);
    }
        
    /* 扩展 action link */
    
    public static string ActionLink(
        this IUrlHelper helper,
        string action = null,
        string controller = null,
        object values = null,
        string protocol = null,
        string host = null,
        string fragment = null)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        var httpContext = helper.ActionContext.HttpContext;
        
        if (protocol == null)
        {
            protocol = httpContext.Request.Scheme;
        }
        
        if (host == null)
        {
            host = httpContext.Request.Host.ToUriComponent();
        }
        
        return Action(
            helper, action, controller, values, protocol, host, fragment);
    }
        
    /* 扩展 page link */
    
    public static string PageLink(
        this IUrlHelper urlHelper,
        string pageName = null,
        string pageHandler = null,
        object values = null,
        string protocol = null,
        string host = null,
        string fragment = null)
    {
        if (urlHelper == null)
        {
            throw new ArgumentNullException(nameof(urlHelper));
        }
        
        var httpContext = urlHelper.ActionContext.HttpContext;
        
        if (protocol == null)
        {
            protocol = httpContext.Request.Scheme;
        }
        
        if (host == null)
        {
            host = httpContext.Request.Host.ToUriComponent();
        }
        
        return Page(
            urlHelper, pageName, pageHandler, values, protocol, host, fragment);
    }
}

```

###### 2.4.1.3 url helper base

```c#
public abstract class UrlHelperBase : IUrlHelper
{
    // Perf: Share the StringBuilder object across multiple calls of GenerateURL 
    // for this UrlHelper
    private StringBuilder? _stringBuilder;
    
    // Perf: Reuse the RouteValueDictionary across multiple calls of Action 
    // for this UrlHelper
    private readonly RouteValueDictionary _routeValueDictionary;
        
    protected RouteValueDictionary AmbientValues { get; }       
    public ActionContext ActionContext { get; }
            
    protected UrlHelperBase(ActionContext actionContext)
    {
        if (actionContext == null)
        {
            throw new ArgumentNullException(nameof(actionContext));
        }
        
        ActionContext = actionContext;
        AmbientValues = actionContext.RouteData.Values;
        _routeValueDictionary = new RouteValueDictionary();
    }
            
    /// <inheritdoc />
    public virtual bool IsLocalUrl([NotNullWhen(true)] string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }        
        // Allows "/" or "/foo" but not "//" or "/\".
        if (url[0] == '/')
        {
            // url is exactly "/"
            if (url.Length == 1)
            {
                return true;
            }            
            // url doesn't start with "//" or "/\"
            if (url[1] != '/' && url[1] != '\\')
            {
                return !HasControlCharacter(url.AsSpan(1));
            }
            
            return false;
        }        
        // Allows "~/" or "~/foo" but not "~//" or "~/\".
        if (url[0] == '~' && url.Length > 1 && url[1] == '/')
        {
            // url is exactly "~/"
            if (url.Length == 2)
            {
                return true;
            }            
            // url doesn't start with "~//" or "~/\"
            if (url[2] != '/' && url[2] != '\\')
            {
                return !HasControlCharacter(url.AsSpan(2));
            }
            
            return false;
        }
        
        return false;
        
        static bool HasControlCharacter(ReadOnlySpan<char> readOnlySpan)
        {
            // URLs may not contain ASCII control characters.
            for (var i = 0; i < readOnlySpan.Length; i++)
            {
                if (char.IsControl(readOnlySpan[i]))
                {
                    return true;
                }
            }
            
            return false;
        }
    }
    
    /// <inheritdoc />
    [return: NotNullIfNotNull("contentPath")]
    public virtual string? Content(string? contentPath)
    {
        if (string.IsNullOrEmpty(contentPath))
        {
            return null;
        }
        else if (contentPath[0] == '~')
        {
            var segment = new PathString(contentPath.Substring(1));
            var applicationPath = ActionContext.HttpContext.Request.PathBase;
            
            var path = applicationPath.Add(segment);
            Debug.Assert(path.HasValue);
            return path.Value;
        }
        
        return contentPath;
    }
    
    /// <inheritdoc />
    public virtual string? Link(string? routeName, object? values)
    {
        return RouteUrl(
            new UrlRouteContext()
            {
                RouteName = routeName,
                Values = values,
                Protocol = ActionContext.HttpContext.Request.Scheme,
                Host = ActionContext.HttpContext.Request.Host.ToUriComponent()
            });
    }

    /// <inheritdoc />
    public abstract string? Action(UrlActionContext actionContext);
    
    /// <inheritdoc />
    public abstract string? RouteUrl(UrlRouteContext routeContext);

        /// <summary>
        /// Gets a <see cref="RouteValueDictionary"/> using the specified values.
        /// </summary>
        /// <param name="values">The values to use.</param>
        /// <returns>A <see cref="RouteValueDictionary"/> with the specified values.</returns>
    protected RouteValueDictionary GetValuesDictionary(object? values)
    {
        // Perf: RouteValueDictionary can be cast to IDictionary<string, object>, 
        // but it is special cased to avoid allocating boxed Enumerator.
        if (values is RouteValueDictionary routeValuesDictionary)
        {
            _routeValueDictionary.Clear();
            foreach (var kvp in routeValuesDictionary)
            {
                _routeValueDictionary.Add(kvp.Key, kvp.Value);
            }
            
            return _routeValueDictionary;
        }
        
        if (values is IDictionary<string, object> dictionaryValues)
        {
            _routeValueDictionary.Clear();
            foreach (var kvp in dictionaryValues)
            {
                _routeValueDictionary.Add(kvp.Key, kvp.Value);
            }
            
            return _routeValueDictionary;
        }
        
        return new RouteValueDictionary(values);
    }
            
    protected string? GenerateUrl(
        string? protocol, 
        string? host, 
        string? virtualPath, 
        string? fragment)
    {
        if (virtualPath == null)
        {
            return null;
        }
        
        // Perf: In most of the common cases, GenerateUrl is called with a null protocol, 
        // host and fragment.
        // In such cases, we might not need to build any URL as the url generated 
        // is mostly same as the virtual path available in pathData.
        // For such common cases, this FastGenerateUrl method saves a string allocation 
        // per GenerateUrl call.
        if (TryFastGenerateUrl(protocol, host, virtualPath, fragment, out var url))
        {
            return url;
        }
        
        var builder = GetStringBuilder();
        try
        {
            var pathBase = ActionContext.HttpContext.Request.PathBase;
            
            if (string.IsNullOrEmpty(protocol) && string.IsNullOrEmpty(host))
            {
                AppendPathAndFragment(builder, pathBase, virtualPath, fragment);
                // We're returning a partial URL (just path + query + fragment), 
                // but we still want it to be rooted.
                if (builder.Length == 0 || builder[0] != '/')
                {
                    builder.Insert(0, '/');
                }
            }
            else
            {
                protocol = string.IsNullOrEmpty(protocol) ? "http" : protocol;
                builder.Append(protocol);
                
                builder.Append(Uri.SchemeDelimiter);
                
                host = string.IsNullOrEmpty(host) 
                    ? ctionContext.HttpContext.Request.Host.Value 
                    : host;
                builder.Append(host);
                AppendPathAndFragment(builder, pathBase, virtualPath, fragment);
            }
            
            var path = builder.ToString();
            return path;
        }
        finally
        {
            // Clear the StringBuilder so that it can reused for the next call.
            builder.Clear();
        }
    }
        
    protected string? GenerateUrl(string? protocol, string? host, string? path)
    {
        // This method is similar to GenerateUrl, but it's used for EndpointRouting. 
        // It ignores pathbase and fragment because those have already been incorporated.
        if (path == null)
        {
            return null;
        }
        
        // Perf: In most of the common cases, GenerateUrl is called with a null protocol, 
        // host and fragment.
        // In such cases, we might not need to build any URL as the url generated 
        // is mostly same as the virtual path available in pathData.
        // For such common cases, this FastGenerateUrl method saves a string allocation 
        // per GenerateUrl call.
        if (TryFastGenerateUrl(protocol, host, path, fragment: null, out var url))
        {
            return url;
        }
        
        var builder = GetStringBuilder();
        try
        {
            if (string.IsNullOrEmpty(protocol) && string.IsNullOrEmpty(host))
            {
                AppendPathAndFragment(builder, pathBase: null, path, fragment: null);
                
                // We're returning a partial URL (just path + query + fragment), 
                // but we still want it to be rooted.
                if (builder.Length == 0 || builder[0] != '/')
                {
                    builder.Insert(0, '/');
                }
            }
            else
            {
                protocol = string.IsNullOrEmpty(protocol) ? "http" : protocol;
                builder.Append(protocol);
                
                builder.Append(Uri.SchemeDelimiter);
                
                host = string.IsNullOrEmpty(host) 
                    ? ActionContext.HttpContext.Request.Host.Value 
                    : host;
                builder.Append(host);
                AppendPathAndFragment(builder, pathBase: null, path, fragment: null);
            }
            
            return builder.ToString();
        }
        finally
        {
            // Clear the StringBuilder so that it can reused for the next call.
            builder.Clear();
        }
    }
    
    internal static void NormalizeRouteValuesForAction(
        string? action,
        string? controller,
        RouteValueDictionary values,
        RouteValueDictionary? ambientValues)
    {
        object? obj = null;
        if (action == null)
        {
            if (!values.ContainsKey("action") &&
                (ambientValues?.TryGetValue("action", out obj) ?? false))
            {
                values["action"] = obj;
            }
        }
        else
        {
            values["action"] = action;
        }
        
        if (controller == null)
        {
            if (!values.ContainsKey("controller") &&
                (ambientValues?.TryGetValue("controller", out obj) ?? false))
            {
                values["controller"] = obj;
            }
        }
        else
        {
            values["controller"] = controller;
        }
    }
    
    internal static void NormalizeRouteValuesForPage(
        ActionContext? context,
        string? page,
        string? handler,
        RouteValueDictionary values,
        RouteValueDictionary? ambientValues)
    {
        object? value = null;
        if (string.IsNullOrEmpty(page))
        {
            if (!values.ContainsKey("page") &&
                (ambientValues?.TryGetValue("page", out value) ?? false))
            {
                values["page"] = value;
            }
        }
        else
        {
            values["page"] = CalculatePageName(context, ambientValues, page);
        }
        
        if (string.IsNullOrEmpty(handler))
        {
            if (!values.ContainsKey("handler") &&
                (ambientValues?.ContainsKey("handler") ?? false))
            {
                // Clear out form action unless it's explicitly specified in the routeValues.
                values["handler"] = null;
            }
        }
        else
        {
            values["handler"] = handler;
        }
    }
    
    private static object CalculatePageName(
        ActionContext? context, 
        RouteValueDictionary? ambientValues, 
        string pageName)
    {
        Debug.Assert(pageName.Length > 0);
        // Paths not qualified with a leading slash are treated as relative 
        // to the current page.
        if (pageName[0] != '/')
        {
            // OK now we should get the best 'normalized' version of the page route value 
            // that we can.
            string? currentPagePath;
            if (context != null)
            {
                currentPagePath = 
                    NormalizedRouteValue.GetNormalizedRouteValue(context, "page");
            }
            else if (ambientValues != null)
            {
                currentPagePath = Convert.ToString(
                    				  ambientValues["page"], 
                    				  CultureInfo.InvariantCulture);
            }
            else
            {
                currentPagePath = null;
            }
            
            if (string.IsNullOrEmpty(currentPagePath))
            {
                // Disallow the use sibling page routing, a Razor page specific feature, 
                // from a non-page action.
                // OR - this is a call from LinkGenerator 
                // where the HttpContext was not specified.
                //
                // We can't use a relative path in either case, 
                // because we don't know the base path.
                throw new InvalidOperationException(
                    Resources.FormatUrlHelper_RelativePagePathIsNotSupported(
                        		pageName,
                        		nameof(LinkGenerator),
                        		nameof(HttpContext)));
            }
            
            return ViewEnginePath.CombinePath(currentPagePath, pageName);
        }
        
        return pageName;
    }
    
    // for unit testing
    internal static void AppendPathAndFragment(
        StringBuilder builder, 
        PathString pathBase, 
        string virtualPath, 
        string? fragment)
    {
        if (!pathBase.HasValue)
        {
            if (virtualPath.Length == 0)
            {
                builder.Append('/');
            }
            else
            {
                if (!virtualPath.StartsWith('/'))
                {
                    builder.Append('/');
                }
                
                builder.Append(virtualPath);
            }
        }
        else
        {
            if (virtualPath.Length == 0)
            {
                builder.Append(pathBase.Value);
            }
            else
            {
                builder.Append(pathBase.Value);
                
                if (pathBase.Value.EndsWith("/", StringComparison.Ordinal))
                {
                    builder.Length--;
                }
                
                if (!virtualPath.StartsWith("/", StringComparison.Ordinal))
                {
                    builder.Append('/');
                }
                
                builder.Append(virtualPath);
            }
        }
        
        if (!string.IsNullOrEmpty(fragment))
        {
            builder.Append('#').Append(fragment);
        }
    }
    
    private bool TryFastGenerateUrl(
        string? protocol,
        string? host,
        string virtualPath,
        string? fragment,
        [NotNullWhen(true)] out string? url)
    {
        var pathBase = ActionContext.HttpContext.Request.PathBase;
        url = null;
        
        if (string.IsNullOrEmpty(protocol)
            && string.IsNullOrEmpty(host)
            && string.IsNullOrEmpty(fragment)
            && !pathBase.HasValue)
        {
            if (virtualPath.Length == 0)
            {
                url = "/";
                return true;
            }
            else if (virtualPath.StartsWith("/", StringComparison.Ordinal))
            {
                url = virtualPath;
                return true;
            }
        }
        
        return false;
    }
    
    private StringBuilder GetStringBuilder()
    {
        if (_stringBuilder == null)
        {
            _stringBuilder = new StringBuilder();
        }
        
        return _stringBuilder;
    }
}

```

###### 2.4.1.4 url helper

```c#
public class UrlHelper : UrlHelperBase
{       
    protected HttpContext HttpContext => ActionContext.HttpContext;
            
    protected IRouter Router
    {
        get
        {
            var routers = ActionContext.RouteData.Routers;
            if (routers.Count == 0)
            {
                throw new InvalidOperationException(
                    "Could not find an IRouter associated with the ActionContext. " + 
                    "If your application is using endpoint routing then you can 
                    "get a IUrlHelperFactory with " + 
                    "dependency injection and use it to create a UrlHelper, 
                    "or use Microsoft.AspNetCore.Routing.LinkGenerator.");
            }
            
            return routers[0];
        }
    }
    
    public UrlHelper(ActionContext actionContext) : base(actionContext)
    {
    }
                   
    /// <inheritdoc />
    public override string? Action(UrlActionContext actionContext)
    {
        if (actionContext == null)
        {
            throw new ArgumentNullException(nameof(actionContext));
        }
        
        var valuesDictionary = GetValuesDictionary(actionContext.Values);
        
        NormalizeRouteValuesForAction(
            actionContext.Action, 
            actionContext.Controller, 
            valuesDictionary, 
            AmbientValues);

        var virtualPathData = 
            GetVirtualPathData(routeName: null, values: valuesDictionary);
        
        return GenerateUrl(
            actionContext.Protocol, 
            actionContext.Host, 
            virtualPathData, 
            actionContext.Fragment);
    }
    
    /// <inheritdoc />
    public override string? RouteUrl(UrlRouteContext routeContext)
    {
        if (routeContext == null)
        {
            throw new ArgumentNullException(nameof(routeContext));
        }
        
        var valuesDictionary = 
            routeContext.Values as RouteValueDictionary 
            	?? GetValuesDictionary(routeContext.Values);
        var virtualPathData = 
            GetVirtualPathData(routeContext.RouteName, valuesDictionary);
        
        return GenerateUrl(
            routeContext.Protocol, 
            routeContext.Host, 
            virtualPathData, 
            routeContext.Fragment);
    }

        
    protected virtual VirtualPathData? GetVirtualPathData(
        string? routeName, 
        RouteValueDictionary values)
    {
        var context = 
            new VirtualPathContext(
            		HttpContext, 
            		AmbientValues, 
            		values, 
            		routeName);
        
        return Router.GetVirtualPath(context);
    }
           
    protected virtual string? GenerateUrl(
        string? protocol, 
        string? host, 
        VirtualPathData? pathData, 
        string? fragment)
    {
        return GenerateUrl(
            protocol, 
            host, 
            pathData?.VirtualPath, 
            fragment);
    }
}

```

###### 2.4.1.5 url helper factory 接口

```c#
public interface IUrlHelperFactory
{    
    IUrlHelper GetUrlHelper(ActionContext context);
}

```

###### 2.4.1.5 url helper factory

```c#
public class UrlHelperFactory : IUrlHelperFactory
{
    /// <inheritdoc />
    public IUrlHelper GetUrlHelper(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        var httpContext = context.HttpContext;
        
        if (httpContext == null)
        {
            throw new ArgumentException(
                Resources.FormatPropertyOfTypeCannotBeNull(
                	nameof(ActionContext.HttpContext),
	                nameof(ActionContext)));
        }

        if (httpContext.Items == null)
        {
            throw new ArgumentException(
                Resources.FormatPropertyOfTypeCannotBeNull(
                    nameof(HttpContext.Items),
                    nameof(HttpContext)));
        }
        
        // Perf: Create only one UrlHelper per context
        if (httpContext.Items
            		   .TryGetValue(
                           typeof(IUrlHelper), 
                           out var value) && 
            value is IUrlHelper urlHelper)
        {
            return urlHelper;
        }
        
        var endpointFeature = httpContext.Features.Get<IEndpointFeature>();
        if (endpointFeature?.Endpoint != null)
        {
            var services = httpContext.RequestServices;
            var linkGenerator = services.GetRequiredService<LinkGenerator>();
            var logger = services.GetRequiredService<ILogger<EndpointRoutingUrlHelper>>();
            
            urlHelper = 
                new EndpointRoutingUrlHelper(
                	context,
                	linkGenerator,
                	logger);
        }
        else
        {
            urlHelper = new UrlHelper(context);
        }
        
        httpContext.Items[typeof(IUrlHelper)] = urlHelper;
        
        return urlHelper;
    }
}

```

###### 

```c#

```

###### 

```c#

```



```c#

```

##### 

```c#

```



