## about mvc filter



### 1. about



### 2. details

#### 2.1 filter 抽象

##### 2.1.1 filter metadata 接口

```c#
public interface IFilterMetadata
{
}

```

###### 2.1.1.1 filter factory 接口

```c#
public interface IFilterFactory : IFilterMetadata
{    
    bool IsReusable { get; }        
    IFilterMetadata CreateInstance(IServiceProvider serviceProvider);    
}

```

###### 2.1.1.2 filter container 接口

```c#
public interface IFilterContainer
{    
    IFilterMetadata FilterDefinition { get; set; }
}

```

###### 2.1.1.3 filter context

```c#
public abstract class FilterContext : ActionContext
    {
        /// <summary>
        /// Instantiates a new <see cref="FilterContext"/> instance.
        /// </summary>
        /// <param name="actionContext">The <see cref="ActionContext"/>.</param>
        /// <param name="filters">All applicable <see cref="IFilterMetadata"/> implementations.</param>
        public FilterContext(
            ActionContext actionContext,
            IList<IFilterMetadata> filters)
            : base(actionContext)
        {
            if (filters == null)
            {
                throw new ArgumentNullException(nameof(filters));
            }

            Filters = filters;
        }

        /// <summary>
        /// Gets all applicable <see cref="IFilterMetadata"/> implementations.
        /// </summary>
        public virtual IList<IFilterMetadata> Filters { get; }

        /// <summary>
        /// Returns a value indicating whether the provided <see cref="IFilterMetadata"/> is the most effective
        /// policy (most specific) applied to the action associated with the <see cref="FilterContext"/>.
        /// </summary>
        /// <typeparam name="TMetadata">The type of the filter policy.</typeparam>
        /// <param name="policy">The filter policy instance.</param>
        /// <returns>
        /// <c>true</c> if the provided <see cref="IFilterMetadata"/> is the most effective policy, otherwise <c>false</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <see cref="IsEffectivePolicy{TMetadata}(TMetadata)"/> method is used to implement a common convention
        /// for filters that define an overriding behavior. When multiple filters may apply to the same 
        /// cross-cutting concern, define a common interface for the filters (<typeparamref name="TMetadata"/>) and 
        /// implement the filters such that all of the implementations call this method to determine if they should
        /// take action.
        /// </para>
        /// <para>
        /// For instance, a global filter might be overridden by placing a filter attribute on an action method.
        /// The policy applied directly to the action method could be considered more specific.
        /// </para>
        /// <para>
        /// This mechanism for overriding relies on the rules of order and scope that the filter system
        /// provides to control ordering of filters. It is up to the implementor of filters to implement this 
        /// protocol cooperatively. The filter system has no innate notion of overrides, this is a recommended
        /// convention.
        /// </para>
        /// </remarks>
        public bool IsEffectivePolicy<TMetadata>(TMetadata policy) where TMetadata : IFilterMetadata
        {
            if (policy == null)
            {
                throw new ArgumentNullException(nameof(policy));
            }

            var effective = FindEffectivePolicy<TMetadata>();
            return ReferenceEquals(policy, effective);
        }

        /// <summary>
        /// Returns the most effective (most specific) policy of type <typeparamref name="TMetadata"/> applied to 
        /// the action associated with the <see cref="FilterContext"/>.
        /// </summary>
        /// <typeparam name="TMetadata">The type of the filter policy.</typeparam>
        /// <returns>The implementation of <typeparamref name="TMetadata"/> applied to the action associated with
        /// the <see cref="FilterContext"/>
        /// </returns>
        [return: MaybeNull]
        public TMetadata FindEffectivePolicy<TMetadata>() where TMetadata : IFilterMetadata
        {
            // The most specific policy is the one closest to the action (nearest the end of the list).
            for (var i = Filters.Count - 1; i >= 0; i--)
            {
                var filter = Filters[i];
                if (filter is TMetadata match)
                {
                    return match;
                }
            }

            return default;
        }
    }
```



##### 2.1.2 filter provider 接口

```c#
public interface IFilterProvider
{    
    int Order { get; }
    
    void OnProvidersExecuting(FilterProviderContext context);    
    void OnProvidersExecuted(FilterProviderContext context);
}

```

###### 2.1.2.1 filter provider context

```c#
public class FilterProviderContext
{
    public ActionContext ActionContext { get; set; }        
    public IList<FilterItem> Results { get; set; }
    
    public FilterProviderContext(
        ActionContext actionContext, 
        IList<FilterItem> items)
    {
        if (actionContext == null)
        {
            throw new ArgumentNullException(nameof(actionContext));
        }        
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }
        
        ActionContext = actionContext;
        Results = items;
    }        
}

```

###### 2.1.2.2 filter item

```c#
[DebuggerDisplay("FilterItem: {Filter}")]
public class FilterItem
{
    public FilterDescriptor Descriptor { get; } = default!;        
    public IFilterMetadata Filter { get; set; } = default!;        
    public bool IsReusable { get; set; }
    
    public FilterItem(FilterDescriptor descriptor)
    {
        if (descriptor == null)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }
        
        Descriptor = descriptor;
    }
            
    public FilterItem(
        FilterDescriptor descriptor, 
        IFilterMetadata filter) : this(descriptor)
    {
        if (filter == null)
        {
            throw new ArgumentNullException(nameof(filter));
        }
        
        Filter = filter;
    }                 
}

```

###### 2.1.2.3 filter descriptor

```c#
[DebuggerDisplay("Filter = {Filter.ToString(),nq}, Order = {Order}")]
public class FilterDescriptor
{
    public IFilterMetadata Filter { get; }        
    public int Order { get; set; }
    public int Scope { get; }
           
    public FilterDescriptor(
        IFilterMetadata filter, 
        int filterScope)
    {
        if (filter == null)
        {
            throw new ArgumentNullException(nameof(filter));
        }
        
        Filter = filter;
        Scope = filterScope;
        
        // 如果 filter 实现了 ordered filter 接口，
        // 将 ordered filter 接口 order 赋值到 this.Order
        if (Filter is IOrderedFilter orderedFilter)
        {
            Order = orderedFilter.Order;
        }
    }            
}

```

##### 2.1.3 default filter provider

```c#
internal class DefaultFilterProvider : IFilterProvider
{
    public int Order => -1000;
    
    /* provider executing */
    /// <inheritdoc />
    public void OnProvidersExecuting(FilterProviderContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        /* 如果 filter provider context 中，
           action descriptor 的 filter descriptors 集合不为 null */
        
        /* 即 filter provider 为 action 注入相关的 filter */
        
        if (context.ActionContext
            	   .ActionDescriptor
            	   .FilterDescriptors != null)
        {
            var results = context.Results;
            // Perf: Avoid allocating enumerator and read interface .Count once 
            // rather than per iteration
            var resultsCount = results.Count;
            
            /* 遍历 filter provider context 中的 results（filter item 集合），*/
            for (var i = 0; i < resultsCount; i++)
            {
                ProvideFilter(context, results[i]);
            }
        }
    }
            
    public void ProvideFilter(
        FilterProviderContext context,
        FilterItem filterItem)
    {
        // 如果 filter item 的 filter 不为 null，
        // 即 filter item 完成配置，直接返回
        if (filterItem.Filter != null)
        {
            return;
        }
        
        // 解析 filter item 的 descriptor 中的 filter metadata
        var filter = filterItem.Descriptor.Filter;
        
        /* 如果 filter metadata 不是 filter factory */
        if (filter is not IFilterFactory filterFactory)
        {
            // 直接将 filter metadata 赋值到 filter item 的 filter
            filterItem.Filter = filter;
            filterItem.IsReusable = true;
        }
        /* 否则，即 filter metadata 是 filter factory */
        else
        {
            // 调用 filter factory 创建 filter metadata，并赋值给 filter item 的 filter
            var services = context.ActionContext.HttpContext.RequestServices;
            filterItem.Filter = filterFactory.CreateInstance(services);
            filterItem.IsReusable = filterFactory.IsReusable;
            
            // 如果创建的 filter 为 null，抛出异常
            if (filterItem.Filter == null)
            {
                throw new InvalidOperationException(
                    Resources.FormatTypeMethodMustReturnNotNullValue(
                        "CreateInstance",
                        typeof(IFilterFactory).Name));
            }
            
            // 应用 filter container
            ApplyFilterToContainer(filterItem.Filter, filterFactory);
        }
    }
    
    private void ApplyFilterToContainer(
        object actualFilter, 
        IFilterMetadata filterMetadata)
    {
        Debug.Assert(actualFilter != null, "actualFilter should not be null");
        Debug.Assert(filterMetadata != null, "filterMetadata should not be null");

        // 如果 filter metadata 是 filter container，
        if (actualFilter is IFilterContainer container)
        {
            // 封装 filter metadata 到 filter container 的 filter defination
            container.FilterDefinition = filterMetadata;
        }
    }
    
    /// <inheritdoc />
    public void OnProvidersExecuted(FilterProviderContext context)
    {
    }
}

```

#### 2.2 filter 派生接口

##### 2.2.1 authorization filter

###### 2.2.1.1 authorization filter 接口

```c#
public interface IAuthorizationFilter : IFilterMetadata
{    
    void OnAuthorization(AuthorizationFilterContext context);
}

```

###### 2.2.1.2 async authorization filter 接口

```c#
public interface IAsyncAuthorizationFilter : IFilterMetadata
{
    Task OnAuthorizationAsync(AuthorizationFilterContext context);    
}

```

###### 2.2.1.3 authorization filter context

```c#
public class AuthorizationFilterContext : FilterContext
{    
    public AuthorizationFilterContext(
        ActionContext actionContext,
        IList<IFilterMetadata> filters) : base(actionContext, filters)
    {
    }
        
    public virtual IActionResult? Result { get; set; }
}

```

###### 2.2.1.4 allow anonymous filter 接口

```c#
public interface IAllowAnonymousFilter : IFilterMetadata
{
}

```

##### 2.2.2 resource filter

###### 2.2.2.1 resource filter 接口

```c#
public interface IResourceFilter : IFilterMetadata
{    
    void OnResourceExecuting(ResourceExecutingContext context);        
    void OnResourceExecuted(ResourceExecutedContext context);
}

```

###### 2.2.2.2 async resource filter 接口

```c#
public interface IAsyncResourceFilter : IFilterMetadata
{
    
    Task OnResourceExecutionAsync(
        ResourceExecutingContext context,
        ResourceExecutionDelegate next);
}

```

###### 2.2.2.3 resource executing context

```c#
public class ResourceExecutingContext : FilterContext
{
    public virtual IActionResult? Result { get; set; }          
    public IList<IValueProviderFactory> ValueProviderFactories { get; }
    
    public ResourceExecutingContext(
        ActionContext actionContext,
        IList<IFilterMetadata> filters,
        IList<IValueProviderFactory> valueProviderFactories)                    	
        	: base(actionContext, filters)
    {
        if (valueProviderFactories == null)
        {
            throw new ArgumentNullException(nameof(valueProviderFactories));
        }
        
        ValueProviderFactories = valueProviderFactories;
    }            
}

```

###### 2.2.2.4 resource executed context

```c#
public class ResourceExecutedContext : FilterContext
{
    private Exception? _exception;
    private ExceptionDispatchInfo? _exceptionDispatchInfo;
    
    public virtual Exception? Exception
    {
        get
        {
            if (_exception == null && 
                _exceptionDispatchInfo != null)
            {
                return _exceptionDispatchInfo.SourceException;
            }
            else
            {
                return _exception;
            }
        }
        
        set
        {
            _exceptionDispatchInfo = null;
            _exception = value;
        }
    }
    
    public virtual ExceptionDispatchInfo? ExceptionDispatchInfo
    {
        get
        {
            return _exceptionDispatchInfo;
        }
        
        set
        {
            _exception = null;
            _exceptionDispatchInfo = value;
        }
    }
    
    public virtual bool Canceled { get; set; }                                                
    public virtual bool ExceptionHandled { get; set; }    
    
    public virtual IActionResult? Result { get; set; }
            
    public ResourceExecutedContext(
        ActionContext actionContext, 
        IList<IFilterMetadata> filters) : base(actionContext, filters)
    {
    }           
}

```

###### 2.2.2.5 resource execution delegate

```c#
public delegate Task<ResourceExecutedContext> ResourceExecutionDelegate();
```

##### 2.2.3 action filter

###### 2.2.3.1 action filter 接口

```c#
public interface IActionFilter : IFilterMetadata
{    
    void OnActionExecuting(ActionExecutingContext context);        
    void OnActionExecuted(ActionExecutedContext context);
}

```

###### 2.2.3.2 async action filter 接口

```c#
public interface IAsyncActionFilter : IFilterMetadata
{    
    Task OnActionExecutionAsync(
        ActionExecutingContext context, 
        ActionExecutionDelegate next);
}

```

###### 2.2.3.3 action executing context

```c#
public class ActionExecutingContext : FilterContext
{
    public virtual IDictionary<string, object?> ActionArguments { get; }        
    public virtual object Controller { get; }
    
    public virtual IActionResult? Result { get; set; }
    
    public ActionExecutingContext(
        ActionContext actionContext,
        IList<IFilterMetadata> filters,
        IDictionary<string, object?> actionArguments,
        object controller)        : base(actionContext, filters)
    {
        if (actionArguments == null)
        {
            throw new ArgumentNullException(nameof(actionArguments));
        }
        
        ActionArguments = actionArguments;
        Controller = controller;
    }            
}

```

###### 2.2.3.4 action executed context

```c#
public class ActionExecutedContext : FilterContext
{
    private Exception? _exception;
    private ExceptionDispatchInfo? _exceptionDispatchInfo;
    
    public virtual Exception? Exception
    {
        get
        {
            if (_exception == null && _exceptionDispatchInfo != null)
            {
                return _exceptionDispatchInfo.SourceException;
            }
            else
            {
                return _exception;
            }
        }
        
        set
        {
            _exceptionDispatchInfo = null;
            _exception = value;
        }
    }
        
    public virtual ExceptionDispatchInfo? ExceptionDispatchInfo
    {
        get
        {
            return _exceptionDispatchInfo;
        }
        
        set
        {
            _exception = null;
            _exceptionDispatchInfo = value;
        }
    }
    
    public virtual bool Canceled { get; set; }        
    public virtual object Controller { get; }        
    public virtual bool ExceptionHandled { get; set; }
    
    public virtual IActionResult? Result { get; set; }
        
    public ActionExecutedContext(
        ActionContext actionContext,
        IList<IFilterMetadata> filters,
        object controller)        
        	: base(actionContext, filters)
    {
        Controller = controller;
    }                    
}

```

###### 2.2.3.5 action execution delegate

```c#
public delegate Task<ActionExecutedContext> ActionExecutionDelegate();
```

##### 2.2.4 exception filter

###### 2.2.4.1 exception filter 接口

```c#
public interface IExceptionFilter : IFilterMetadata
{    
    void OnException(ExceptionContext context);
}

```

###### 2.2.4.2 async exception filter 接口

```c#
public interface IAsyncExceptionFilter : IFilterMetadata
{    
    Task OnExceptionAsync(ExceptionContext context);
}

```

###### 2.2.4.3 exception context

```c#
public class ExceptionContext : FilterContext
{
    private Exception? _exception;
    private ExceptionDispatchInfo? _exceptionDispatchInfo;
    
    public virtual Exception Exception
    {
        get
        {
            if (_exception == null && _exceptionDispatchInfo != null)
            {
                return _exceptionDispatchInfo.SourceException;
            }
            else
            {
                return _exception!;
            }
        }
        
        set
        {
            _exceptionDispatchInfo = null;
            _exception = value;
        }
    }
    
    public virtual ExceptionDispatchInfo? ExceptionDispatchInfo
    {
        get
        {
            return _exceptionDispatchInfo;
        }
        
        set
        {
            _exception = null;
            _exceptionDispatchInfo = value;
        }
    }
    
    public virtual bool ExceptionHandled { get; set; }
    
    public virtual IActionResult? Result { get; set; }
        
    public ExceptionContext(
        ActionContext actionContext, 
        IList<IFilterMetadata> filters)        
        	: base(actionContext, filters)
    {
    }       
}

```

##### 2.2.5 result filter

###### 2.2.5.1 result filter 接口

```c#
public interface IResultFilter : IFilterMetadata
{    
    void OnResultExecuting(ResultExecutingContext context);        
    void OnResultExecuted(ResultExecutedContext context);
}

```

###### 2.2.5.2 async result filter 接口

```c#
public interface IAsyncResultFilter : IFilterMetadata
{    
    Task OnResultExecutionAsync(
        ResultExecutingContext context, 
        ResultExecutionDelegate next);
}

```

###### 2.2.5.3 result executing context

```c#
public class ResultExecutingContext : FilterContext
{
    public virtual bool Cancel { get; set; }
    public virtual object Controller { get; }
    
    public virtual IActionResult Result { get; set; }
    
    public ResultExecutingContext(
        ActionContext actionContext,
        IList<IFilterMetadata> filters,
        IActionResult result,
        object controller) : base(actionContext, filters)
    {
        Result = result;
        Controller = controller;
    }           
}

```

###### 2.2.5.4 result executed context

```c#
public class ResultExecutedContext : FilterContext
{
    private Exception? _exception;
    private ExceptionDispatchInfo? _exceptionDispatchInfo;    
    
    public virtual Exception? Exception
    {
        get
        {
            if (_exception == null && _exceptionDispatchInfo != null)
            {
                return _exceptionDispatchInfo.SourceException;
            }
            else
            {
                return _exception;
            }
        }
        
        set
        {
            _exceptionDispatchInfo = null;
            _exception = value;
        }
    }
        
    public virtual ExceptionDispatchInfo? ExceptionDispatchInfo
    {
        get
        {
            return _exceptionDispatchInfo;
        }
        
        set
        {
            _exception = null;
            _exceptionDispatchInfo = value;
        }
    }
        
    public virtual bool Canceled { get; set; }        
    public virtual object Controller { get; }        
    public virtual bool ExceptionHandled { get; set; }
        
    public virtual IActionResult Result { get; }
    
    public ResultExecutedContext(
        ActionContext actionContext,
        IList<IFilterMetadata> filters,
        IActionResult result,
        object controller) : base(actionContext, filters)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }
        
        Result = result;
        Controller = controller;
    }                
}

```

###### 2.2.5.5 result execution delegate

```c#
public delegate Task<ResultExecutedContext> ResultExecutionDelegate();
```

###### 2.2.5.6 always run result filter

```c#
public interface IAlwaysRunResultFilter : IResultFilter
{
}

```

###### 2.2.5.7 async always run result filter

```c#
public interface IAsyncAlwaysRunResultFilter : IAsyncResultFilter
{
}

```

#### 2.3 authorize filter 实现

##### 2.3.1 authorize filter

```c#
public class AuthorizeFilter : IAsyncAuthorizationFilter, IFilterFactory
{
    public IEnumerable<IAuthorizeData> AuthorizeData { get; }        
    public AuthorizationPolicy Policy { get; }
    public IAuthorizationPolicyProvider PolicyProvider { get; }
    
    bool IFilterFactory.IsReusable => true;
       
    public AuthorizeFilter(IEnumerable<IAuthorizeData> authorizeData)
    {
        if (authorizeData == null)
        {
            throw new ArgumentNullException(nameof(authorizeData));
        }
        
        AuthorizeData = authorizeData;
    }
    
    public AuthorizeFilter()        
        : this(authorizeData: new[] { new AuthorizeAttribute() })
    {
    }
                        
    public AuthorizeFilter(
        IAuthorizationPolicyProvider policyProvider, 
        IEnumerable<IAuthorizeData> authorizeData) : this(authorizeData)
    {
        if (policyProvider == null)
        {
            throw new ArgumentNullException(nameof(policyProvider));
        }
        
        PolicyProvider = policyProvider;
    }

    public AuthorizeFilter(AuthorizationPolicy policy)
    {
        if (policy == null)
        {
            throw new ArgumentNullException(nameof(policy));
        }
        
        Policy = policy;
    }    
            
    public AuthorizeFilter(string policy) 
        : this(new[] { new AuthorizeAttribute(policy) })
    {
    }
             
    /* 实现 on authorization 方法 */
    /// <inheritdoc />
    public virtual async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        /* 如果 context 验证 authorization policy 无效，直接返回 */
        if (!context.IsEffectivePolicy(this))
        {
            return;
        }
        
        /* 从 context 中解析（effective）authorization policy，
           如果解析为 null，直接返回 */
        // IMPORTANT: Changes to authorization logic should be mirrored in 
        // security's AuthorizationMiddleware
        var effectivePolicy = await GetEffectivePolicyAsync(context);
        if (effectivePolicy == null)
        {
            return;
        }
        
        /* 解析 authorization policy evaluator */
        var policyEvaluator = 
            context.HttpContext
              	   .RequestServices
            	   .GetRequiredService<IPolicyEvaluator>();
        
        /* 使用 authorization policy evaluator 验证 */
        var authenticateResult = 
            await policyEvaluator.AuthenticateAsync(
            						  effectivePolicy, 
            						  context.HttpContext);
        
        /* 如果标记了 allow anonymous，直接返回 */
        // Allow Anonymous skips all authorization
        if (HasAllowAnonymous(context))
        {
            return;
        }
        
        /* 生成 authorize result（action result） */
        var authorizeResult = 
            await policyEvaluator.AuthorizeAsync(
            						  effectivePolicy, 
						              authenticateResult, 
						              context.HttpContext, 
						              context);        
        if (authorizeResult.Challenged)
        {
            context.Result = 
                new ChallengeResult(
                	effectivePolicy.AuthenticationSchemes.ToArray());
        }
        else if (authorizeResult.Forbidden)
        {
            context.Result = 
                new ForbidResult(
                	effectivePolicy.AuthenticationSchemes.ToArray());
        }
    }
    
    internal async Task<AuthorizationPolicy> 
        GetEffectivePolicyAsync(AuthorizationFilterContext context)
    {
        // Combine all authorize filters into single effective policy 
        // that's only run on the closest filter
        var builder = new AuthorizationPolicyBuilder(await ComputePolicyAsync());
        for (var i = 0; i < context.Filters.Count; i++)
        {
            if (ReferenceEquals(this, context.Filters[i]))
            {
                continue;
            }
            
            if (context.Filters[i] is AuthorizeFilter authorizeFilter)
            {
                // Combine using the explicit policy, or the dynamic policy provider
                builder.Combine(await authorizeFilter.ComputePolicyAsync());
            }
        }
        
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint != null)
        {
            // When doing endpoint routing, MVC does not create filters for any 
            // authorization specific metadata i.e [Authorize] does not get translated 
            // into AuthorizeFilter. 
            // Consequently, there are some rough edges when an application uses a mix of
            // AuthorizeFilter explicilty configured by the user (e.g. global auth filter), 
            // and uses endpoint metadata.
            // To keep the behavior of AuthFilter identical to pre-endpoint routing, we will 
            // gather auth data from endpoint metadata and produce a policy using this.
            // This would mean we would have effectively run some auth twice, 
            // but it maintains compat.
            var policyProvider =
                PolicyProvider 
                	?? context.HttpContext
                			  .RequestServices
                			  .GetRequiredService<IAuthorizationPolicyProvider>();
            
            var endpointAuthorizeData = 
                endpoint.Metadata
                		.GetOrderedMetadata<IAuthorizeData>() 
                	?? Array.Empty<IAuthorizeData>();
            
            var endpointPolicy = 
                await AuthorizationPolicy.CombineAsync(
                							  policyProvider, 
                							  endpointAuthorizeData);
            
            if (endpointPolicy != null)
            {
                builder.Combine(endpointPolicy);
            }
        }
        
        return builder.Build();
    }
    
    
    // Computes the actual policy for this filter using either Policy or 
    // PolicyProvider + AuthorizeData
    private Task<AuthorizationPolicy> ComputePolicyAsync()
    {
        if (Policy != null)
        {
            return Task.FromResult(Policy);
        }
        
        if (PolicyProvider == null)
        {
            throw new InvalidOperationException(
                Resources.FormatAuthorizeFilter_AuthorizationPolicyCannotBeCreated(
                    		  nameof(AuthorizationPolicy),
			                  nameof(IAuthorizationPolicyProvider)));
        }
        
        return AuthorizationPolicy.CombineAsync(
            						   PolicyProvider, 
            						   AuthorizeData);
    }
    
    private static bool HasAllowAnonymous(AuthorizationFilterContext context)
    {
        var filters = context.Filters;
        for (var i = 0; i < filters.Count; i++)
        {
            if (filters[i] is IAllowAnonymousFilter)
            {
                return true;
            }
        }
        
        // When doing endpoint routing, MVC does not add AllowAnonymousFilters for 
        // AllowAnonymousAttributes that were discovered on controllers and actions. 
        // To maintain compat with 2.x, we'll check for the presence of IAllowAnonymous 
        // in endpoint metadata.
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
        {
            return true;
        }
        
        return false;
    }
    
    
    
    IFilterMetadata IFilterFactory.CreateInstance(IServiceProvider serviceProvider)
    {
        if (Policy != null || 
            PolicyProvider != null)
        {
            // The filter is fully constructed. Use the current instance to authorize.
            return this;
        }
        
        Debug.Assert(AuthorizeData != null);
        var policyProvider = 
            serviceProvider.GetRequiredService<IAuthorizationPolicyProvider>();
        
        return AuthorizationApplicationModelProvider.GetFilter(policyProvider, AuthorizeData);
    }        
}

```

#### 2.4 resource filter 实现

##### 2.4.1 middleware filter

```c#
internal class MiddlewareFilter : IAsyncResourceFilter
{
    private readonly RequestDelegate _middlewarePipeline;
    
    public MiddlewareFilter(RequestDelegate middlewarePipeline)
    {
        if (middlewarePipeline == null)
        {
            throw new ArgumentNullException(nameof(middlewarePipeline));
        }
        // 注入 middleware 管道（添加了关于 filter 的处理方法）
        _middlewarePipeline = middlewarePipeline;
    }
    
    /* 实现 execution 方法 */
    public Task OnResourceExecutionAsync(
        ResourceExecutingContext context, 
        ResourceExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        
        /* 记录 middleware 执行点，封装为 middleware filter feature */
        // Capture the current context into the feature. 
        // This will later be used in the end middleware to continue the execution flow 
        // to later MVC layers.
        // Example:
        // this filter -> user-middleware1 -> user-middleware2 -> the-end-middleware -> 
        // resource filters or model binding
        var feature = new MiddlewareFilterFeature()
        {
            ResourceExecutionDelegate = next,
            ResourceExecutingContext = context
        };
        /* 注入 http context features */
        httpContext.Features.Set<IMiddlewareFilterFeature>(feature);
        
        /* 执行 包含 filter 处理方法的 middleware 管道 */
        return _middlewarePipeline(httpContext);
    }
}

```

###### 2.4.1.1 middleware filter feature 接口

```c#
internal interface IMiddlewareFilterFeature
{
    ResourceExecutingContext ResourceExecutingContext { get; }    
    ResourceExecutionDelegate ResourceExecutionDelegate { get; }
}

```

###### 2.4.1.2 middleware filter feature

```c#
internal class MiddlewareFilterFeature : IMiddlewareFilterFeature
{
    public ResourceExecutingContext ResourceExecutingContext { get; set; }    
    public ResourceExecutionDelegate ResourceExecutionDelegate { get; set; }
}

```

##### 2.4.2 middleware filter attribute

```c#
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method, 
    AllowMultiple = true, Inherited = true)]
public class MiddlewareFilterAttribute : 
	Attribute, 
	IFilterFactory, 
	IOrderedFilter
{
    public Type ConfigurationType { get; }        
    public int Order { get; set; }        
    public bool IsReusable => true;
    
    public MiddlewareFilterAttribute(Type configurationType)
    {
        if (configurationType == null)
        {
            throw new ArgumentNullException(nameof(configurationType));
        }
        
        // 注入 configuration type，配置 filter 处理方法
        ConfigurationType = configurationType;
    }
        
    /// <inheritdoc />
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        if (serviceProvider == null)
        {
            throw new ArgumentNullException(nameof(serviceProvider));
        }
        // 解析 middleware filter builder
        var middlewarePipelineService = 
            serviceProvider.GetRequiredService<MiddlewareFilterBuilder>();
        // 由 configuration type 构建新的 middleware 管道
        var pipeline = middlewarePipelineService.GetPipeline(ConfigurationType);

        // 由 新的 middleware 管道创建 middleware filter
        return new MiddlewareFilter(pipeline);
    }
}

```

###### 2.4.2.1 middleware filter builder

```c#
internal class MiddlewareFilterBuilder
{
    /* 以 configuration type 为 可以，缓存对应的请求管道（request delegate） */
    // 'GetOrAdd' call on the dictionary is not thread safe and we might end up creating 
    // the pipeline more than once. 
    // To prevent this Lazy<> is used. 
    // In the worst case multiple Lazy<> objects are created for multiple threads 
    // but only one of the objects succeeds in creating a pipeline.
    private readonly ConcurrentDictionary<Type, Lazy<RequestDelegate>> 
        _pipelinesCache = new ConcurrentDictionary<Type, Lazy<RequestDelegate>>();
    
    private readonly MiddlewareFilterConfigurationProvider _configurationProvider;    
    
    public IApplicationBuilder ApplicationBuilder { get; set; }
    
    public MiddlewareFilterBuilder(
        MiddlewareFilterConfigurationProvider configurationProvider)
    {
        _configurationProvider = configurationProvider;
    }
    
    public RequestDelegate GetPipeline(Type configurationType)
    {
        // Build the pipeline only once. 
        // This is similar to how middleware registered in Startup are constructed.        
        var requestDelegate = _pipelinesCache.GetOrAdd(
            configurationType,
            key => new Lazy<RequestDelegate>(() => BuildPipeline(key)));
        
        return requestDelegate.Value;
    }
    
    private RequestDelegate BuildPipeline(Type middlewarePipelineProviderType)
    {
        if (ApplicationBuilder == null)
        {
            throw new InvalidOperationException(                
                Resources.FormatMiddlewareFilterBuilder_NullApplicationBuilder(
                    nameof(ApplicationBuilder)));
        }
        
        /* 创建新的 application builder*/
        var nestedAppBuilder = ApplicationBuilder.New();
        
        /* 解析 configuration type 中配置 application builder 的方法，
           即 configure(IApplicationBuilder app) 方法 */
        // Get the 'Configure' method from the user provided type.
        var configureDelegate = 
            _configurationProvider.CreateConfigureDelegate(
        	    middlewarePipelineProviderType);
        /* 配置 application builder */
        configureDelegate(nestedAppBuilder);

        /* 注册执行 middleware 中断点的委托 */
        // The middleware resource filter, after receiving the request executes the user 
        // configured middleware pipeline. 
        // Since we want execution of the request to continue to later MVC layers 
        // (resource filters or model binding), add a middleware at the end of the user
        // provided pipeline which make sure to continue this flow.        
        // Example:
        // middleware filter -> user-middleware1 -> user-middleware2 -> end-middleware 
        // -> resource filters or model binding
        nestedAppBuilder.Run(
            async (httpContext) =>
            {
                var feature = httpContext.Features
                    					 .Get<IMiddlewareFilterFeature>();
                if (feature == null)
                {
                    throw new InvalidOperationException(
                        Resources.FormatMiddlewareFilterBuilder_NoMiddlewareFeature(
                            nameof(IMiddlewareFilterFeature)));
                }
                
                var resourceExecutionDelegate = feature.ResourceExecutionDelegate;

                var resourceExecutedContext = await resourceExecutionDelegate();
                if (resourceExecutedContext.ExceptionHandled)
                {
                    return;
                }

                // Ideally we want the experience of a middleware pipeline to behave 
                // the same as if it was registered in Startup. 
                // In this scenario, an Exception thrown in a middleware later in the 
                // pipeline gets propagated back to earlier middleware. 
                // So, check if a later resource filter threw an Exception and propagate that
                // back to the middleware pipeline.
                resourceExecutedContext.ExceptionDispatchInfo?.Throw();
                if (resourceExecutedContext.Exception != null)
                {
                    // This line is rarely reachable because ResourceInvoker captures 
                    // thrown Exceptions using ExceptionDispatchInfo. 
                    // That said, filters could set only resourceExecutedContext.Exception.
                    throw resourceExecutedContext.Exception;
                }
            });
        
        // 构建 request delegate
        return nestedAppBuilder.Build();
    }
}

```

###### 2.4.2.2 middleware filter configuration provider

```c#
internal class MiddlewareFilterConfigurationProvider
{
    public Action<IApplicationBuilder> CreateConfigureDelegate(Type configurationType)
    {
        if (configurationType == null)
        {
            throw new ArgumentNullException(nameof(configurationType));
        }
        
        if (!HasParameterlessConstructor(configurationType))
        {
            throw new InvalidOperationException(
                Resources.FormatMiddlewareFilterConfigurationProvider
                		  _CreateConfigureDelegate_CannotCreateType(
                              configurationType, 
                              nameof(configurationType)));
        }
        
        var instance = Activator.CreateInstance(configurationType);
        var configureDelegateBuilder = GetConfigureDelegateBuilder(configurationType);
        return configureDelegateBuilder.Build(instance);
    }
    
    private static ConfigureBuilder GetConfigureDelegateBuilder(Type startupType)
    {
        var configureMethod = FindMethod(startupType, typeof(void));
        return new ConfigureBuilder(configureMethod);
    }
    
    private static MethodInfo FindMethod(Type startupType, Type returnType = null)
    {
        var methodName = "Configure";
        
        var methods = startupType.GetMethods(BindingFlags.Public | 
                                             BindingFlags.Instance | 
                                             BindingFlags.Static);
        
        var selectedMethods = methods.Where(method => 
                                            	method.Name.Equals(methodName))
            						 .ToList();
        if (selectedMethods.Count > 1)
        {
            throw new InvalidOperationException(
                Resources.FormatMiddewareFilter_ConfigureMethodOverload(methodName));
        }
        
        var methodInfo = selectedMethods.FirstOrDefault();
        if (methodInfo == null)
        {
            throw new InvalidOperationException(
                Resources.FormatMiddewareFilter_NoConfigureMethod(
                    methodName,
                    startupType.FullName));
        }
        
        if (returnType != null && 
            methodInfo.ReturnType != returnType)
        {
            throw new InvalidOperationException(
                Resources.FormatMiddlewareFilter_InvalidConfigureReturnType(
                    methodInfo.Name,
                    startupType.FullName,
                    returnType.Name));
        }
        return methodInfo;
    }
    
    private static bool HasParameterlessConstructor(Type modelType)
    {
        return !modelType.IsAbstract && 
               modelType.GetConstructor(Type.EmptyTypes) != null;
    }
    
    private class ConfigureBuilder
    {
        public MethodInfo MethodInfo { get; }
        
        public ConfigureBuilder(MethodInfo configure)
        {
            MethodInfo = configure;
        }
        
        public Action<IApplicationBuilder> Build(object instance)
        {
            return (applicationBuilder) => Invoke(instance, applicationBuilder);
        }
        
        private void Invoke(object instance, IApplicationBuilder builder)
        {
            var serviceProvider = builder.ApplicationServices;
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
                        parameters[index] = 
                            serviceProvider.GetRequiredService(parameterInfo.ParameterType);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            Resources.FormatMiddlewareFilter_ServiceResolutionFail(
                                parameterInfo.ParameterType.FullName,
                                parameterInfo.Name,
                                MethodInfo.Name,
                                MethodInfo.DeclaringType.FullName),
                            ex);
                    }
                }
            }
            MethodInfo.Invoke(instance, parameters);
        }
    }
}

```

#### 2.5 action filter 实现

##### 2.5.1 controller action filter

```c#
internal class ControllerActionFilter : IAsyncActionFilter, IOrderedFilter
{    
    public int Order { get; set; } = int.MinValue;
    
    /// <inheritdoc />
    public Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (next == null)
        {
            throw new ArgumentNullException(nameof(next));
        }
        
        /* 从 action executing context 解析 controller */
        var controller = context.Controller;
        if (controller == null)
        {
            throw new InvalidOperationException(
                Resources.FormatPropertyOfTypeCannotBeNull(
                    nameof(context.Controller),
                    nameof(ActionExecutingContext)));
        }
        
        // 如果 controller 实现了 async action filter
        if (controller is IAsyncActionFilter asyncActionFilter)
        {
            return asyncActionFilter.OnActionExecutionAsync(context, next);
        }
        // 如果 controller 实现了 action filter
        else if (controller is IActionFilter actionFilter)
        {
            return ExecuteActionFilter(context, next, actionFilter);
        }
        // 否则，返回 next 委托
        else
        {
            return next();
        }
    }
    
    private static async Task ExecuteActionFilter(
        ActionExecutingContext context,
        ActionExecutionDelegate next,
        IActionFilter actionFilter)
    {
        // 执行 executing
        actionFilter.OnActionExecuting(context);
        // 如果 result 为 null，执行 executed
        if (context.Result == null)
        {
            actionFilter.OnActionExecuted(await next());
        }
    }
}

```

##### 2.5.2 action filter attribute

```c#
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method, 
    AllowMultiple = true, 
    Inherited = true)]
public abstract class ActionFilterAttribute :
	Attribute, 
	IActionFilter, 
	IAsyncActionFilter, 
	IResultFilter, 
	IAsyncResultFilter, 
	IOrderedFilter
{
    /// <inheritdoc />
    public int Order { get; set; }
    
    /// <inheritdoc />
    public virtual void OnActionExecuting(ActionExecutingContext context)
    {
    }
    
    /// <inheritdoc />
    public virtual void OnActionExecuted(ActionExecutedContext context)
    {
    }
    
    /// <inheritdoc />
    public virtual async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (next == null)
        {
            throw new ArgumentNullException(nameof(next));
        }
        
        OnActionExecuting(context);
        if (context.Result == null)
        {
            OnActionExecuted(await next());
        }
    }
    
    /// <inheritdoc />
    public virtual void OnResultExecuting(ResultExecutingContext context)
    {
    }
    
    /// <inheritdoc />
    public virtual void OnResultExecuted(ResultExecutedContext context)
    {
    }
    
    /// <inheritdoc />
    public virtual async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (next == null)
        {
            throw new ArgumentNullException(nameof(next));
        }
        
        OnResultExecuting(context);
        if (!context.Cancel)
        {
            OnResultExecuted(await next());
        }
    }
}

```

##### 2.5.3 response cache filter

###### 2.5.3.1 response cache filter 接口

```c#
internal interface IResponseCacheFilter : IFilterMetadata
{
}

```

###### 2.5.3.2 response cache filter

```c#
internal class ResponseCacheFilter : IActionFilter, IResponseCacheFilter
{
    private readonly ResponseCacheFilterExecutor _executor;
    private readonly ILogger _logger;
    
    public int Duration
    {
        get => _executor.Duration;
        set => _executor.Duration = value;
    }    
    public ResponseCacheLocation Location
    {
        get => _executor.Location;
        set => _executor.Location = value;
    }        
    public bool NoStore
    {
        get => _executor.NoStore;
        set => _executor.NoStore = value;
    }        
    public string VaryByHeader
    {
        get => _executor.VaryByHeader;
        set => _executor.VaryByHeader = value;
    }        
    public string[] VaryByQueryKeys
    {
        get => _executor.VaryByQueryKeys;
        set => _executor.VaryByQueryKeys = value;
    }
        
    public ResponseCacheFilter(CacheProfile cacheProfile, ILoggerFactory loggerFactory)
    {
        _executor = new ResponseCacheFilterExecutor(cacheProfile);
        _logger = loggerFactory.CreateLogger(GetType());
    }
            
    /// <inheritdoc />
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));            
        }

        // If there are more filters which can override the values written by this filter,
        // then skip execution of this filter.
        var effectivePolicy = context.FindEffectivePolicy<IResponseCacheFilter>();
        if (effectivePolicy != null && 
            effectivePolicy != this)
        {
            _logger.NotMostEffectiveFilter(
                GetType(), 
                effectivePolicy.GetType(), 
                typeof(IResponseCacheFilter));
            
            return;
        }
        
        _executor.Execute(context);
    }
    
    /// <inheritdoc />
    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}

```

###### 2.5.3.3 response cache filter executor

```c#
internal class ResponseCacheFilterExecutor
{
    private readonly CacheProfile _cacheProfile;
    
    private int? _cacheDuration;
    public int Duration
    {
        get => _cacheDuration ?? _cacheProfile.Duration ?? 0;
        set => _cacheDuration = value;
    }
    
    private ResponseCacheLocation? _cacheLocation;
     public ResponseCacheLocation Location
    {
        get => _cacheLocation ?? _cacheProfile.Location ?? ResponseCacheLocation.Any;
        set => _cacheLocation = value;
    }
    
    private bool? _cacheNoStore;
    public bool NoStore
    {
        get => _cacheNoStore ?? _cacheProfile.NoStore ?? false;
        set => _cacheNoStore = value;
    }
    
    private string _cacheVaryByHeader;
    public string VaryByHeader
    {
        get => _cacheVaryByHeader ?? _cacheProfile.VaryByHeader;
        set => _cacheVaryByHeader = value;
    }
    
    private string[] _cacheVaryByQueryKeys;
    public string[] VaryByQueryKeys
    {
        get => _cacheVaryByQueryKeys ?? _cacheProfile.VaryByQueryKeys;
        set => _cacheVaryByQueryKeys = value;
    }
    
    public ResponseCacheFilterExecutor(CacheProfile cacheProfile)
    {
        _cacheProfile = cacheProfile 
            				?? throw new ArgumentNullException(nameof(cacheProfile));
    }                                       
    
    public void Execute(FilterContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        if (!NoStore)
        {
            // Duration MUST be set (either in the cache profile or in this filter) 
            // unless NoStore is true.
            if (_cacheProfile.Duration == null && _cacheDuration == null)
            {
                throw new InvalidOperationException(
                    Resources.FormatResponseCache_SpecifyDuration(
                        nameof(NoStore), 
                        nameof(Duration)));
            }
        }
        
        var headers = context.HttpContext.Response.Headers;
        
        // Clear all headers
        headers.Remove(HeaderNames.Vary);
        headers.Remove(HeaderNames.CacheControl);
        headers.Remove(HeaderNames.Pragma);
        
        if (!string.IsNullOrEmpty(VaryByHeader))
        {
            headers[HeaderNames.Vary] = VaryByHeader;
        }
        
        if (VaryByQueryKeys != null)
        {
            var responseCachingFeature = context.HttpContext
                								.Features
                								.Get<IResponseCachingFeature>();
            if (responseCachingFeature == null)
            {
                throw new InvalidOperationException(
                    Resources.FormatVaryByQueryKeys
                    		  _Requires_ResponseCachingMiddleware(
                                  nameof(VaryByQueryKeys)));
            }
            
            responseCachingFeature.VaryByQueryKeys = VaryByQueryKeys;
        }
        
        if (NoStore)
        {
            headers[HeaderNames.CacheControl] = "no-store";
            
            // Cache-control: no-store, no-cache is valid.
            if (Location == ResponseCacheLocation.None)
            {
                headers.AppendCommaSeparatedValues(HeaderNames.CacheControl, "no-cache");
                headers[HeaderNames.Pragma] = "no-cache";
            }
        }
        else
        {
            string cacheControlValue;
            switch (Location)
            {
                    case ResponseCacheLocation.Any:
                        cacheControlValue = "public,";
                        break;
                    case ResponseCacheLocation.Client:
                        cacheControlValue = "private,";
                        break;
                    case ResponseCacheLocation.None:
                        cacheControlValue = "no-cache,";
                        headers[HeaderNames.Pragma] = "no-cache";
                        break;
                    default:
                        cacheControlValue = null;
                        break;
                }

                cacheControlValue = $"{cacheControlValue}max-age={Duration}";
                headers[HeaderNames.CacheControl] = cacheControlValue;
            }
    }
}

```

###### 2.5.3.4 response cache location

```c#
public enum ResponseCacheLocation
{
    /// <summary>
    /// Cached in both proxies and client.
    /// Sets "Cache-control" header to "public".
    /// </summary>
    Any = 0,
    /// <summary>
    /// Cached only in the client.
    /// Sets "Cache-control" header to "private".
    /// </summary>
    Client = 1,
    /// <summary>
    /// "Cache-control" and "Pragma" headers are set to "no-cache".
    /// </summary>
    None = 2
}

```

##### 2.5.4 response cache fiter attribute

```c#
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class ResponseCacheAttribute : Attribute, IFilterFactory, IOrderedFilter
{
    // A nullable-int cannot be used as an Attribute parameter.
        // Hence this nullable-int is present to back the Duration property.
        // The same goes for nullable-ResponseCacheLocation and nullable-bool.
    private int? _duration;
    public int Duration
    {
        get => _duration ?? 0;
        set => _duration = value;
    }
    
    private ResponseCacheLocation? _location;
    public ResponseCacheLocation Location
    {
        get => _location ?? ResponseCacheLocation.Any;
        set => _location = value;
    }
    
    private bool? _noStore;
    public bool NoStore
    {
        get => _noStore ?? false;
        set => _noStore = value;
    }
            
    public string VaryByHeader { get; set; }        
    public string[] VaryByQueryKeys { get; set; }       
    public string CacheProfileName { get; set; }        
    public int Order { get; set; }       
    public bool IsReusable => true;
        
    public CacheProfile GetCacheProfile(MvcOptions options)
    {
        CacheProfile selectedProfile = null;
        if (CacheProfileName != null)
        {
            options.CacheProfiles.TryGetValue(CacheProfileName, out selectedProfile);
            if (selectedProfile == null)
            {
                throw new InvalidOperationException(
                    Resources.FormatCacheProfileNotFound(CacheProfileName));
            }
        }
        
        // If the ResponseCacheAttribute parameters are set,
        // then it must override the values from the Cache Profile.
        // The below expression first checks if the duration is set by the 
        // attribute's parameter.
        // If absent, it checks the selected cache profile (Note: There can be 
        // no cache profile as well)
        // The same is the case for other properties.
        _duration = _duration ?? selectedProfile?.Duration;
        _noStore = _noStore ?? selectedProfile?.NoStore;
        _location = _location ?? selectedProfile?.Location;
        VaryByHeader = VaryByHeader ?? selectedProfile?.VaryByHeader;
        VaryByQueryKeys = VaryByQueryKeys ?? selectedProfile?.VaryByQueryKeys;
        
        return new CacheProfile
        {
            Duration = _duration,
            Location = _location,
            NoStore = _noStore,
            VaryByHeader = VaryByHeader,
            VaryByQueryKeys = VaryByQueryKeys,
        };
    }
    
    /// <inheritdoc />
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        if (serviceProvider == null)
        {
            throw new ArgumentNullException(nameof(serviceProvider));
        }
        
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var optionsAccessor = serviceProvider.GetRequiredService<IOptions<MvcOptions>>();
        var cacheProfile = GetCacheProfile(optionsAccessor.Value);
        
        // ResponseCacheFilter cannot take any null values. 
        // Hence, if there are any null values, the properties convert them to 
        // their defaults and are passed on.
        return new ResponseCacheFilter(cacheProfile, loggerFactory);
    }
}

```

###### 2.5.4.1 cache profile

```c#
public class CacheProfile
{    
    public int? Duration { get; set; }        
    public ResponseCacheLocation? Location { get; set; }        
    public bool? NoStore { get; set; }        
    public string VaryByHeader { get; set; }        
    public string[] VaryByQueryKeys { get; set; }
}

```

#### 2.6 exception filter 实现

##### 2.6.1 exception filter attribute

```c#
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method, 
    AllowMultiple = true, 
    Inherited = true)]
public abstract class ExceptionFilterAttribute : 
	Attribute, 
	IAsyncExceptionFilter, 
	IExceptionFilter, 
	IOrderedFilter
{
    /// <inheritdoc />
    public int Order { get; set; }
    
    /// <inheritdoc />
    public virtual Task OnExceptionAsync(ExceptionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        OnException(context);
        return Task.CompletedTask;
    }
    
    /// <inheritdoc />
    public virtual void OnException(ExceptionContext context)
    {
    }
}

```

#### 2.7 result filter 实现

##### 2.7.1 result filter attribute

```c#
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public abstract class ResultFilterAttribute : Attribute, IResultFilter, IAsyncResultFilter, IOrderedFilter
    {
        /// <inheritdoc />
        public int Order { get; set; }

        /// <inheritdoc />
        public virtual void OnResultExecuting(ResultExecutingContext context)
        {
        }

        /// <inheritdoc />
        public virtual void OnResultExecuted(ResultExecutedContext context)
        {
        }

        /// <inheritdoc />
        public virtual async Task OnResultExecutionAsync(
            ResultExecutingContext context,
            ResultExecutionDelegate next)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            OnResultExecuting(context);
            if (!context.Cancel)
            {
                OnResultExecuted(await next());
            }
        }
    }
```

##### 2.7.2 controller result filter

```c#
internal class ControllerResultFilter : IAsyncResultFilter, IOrderedFilter
{
    // Controller-filter methods run farthest from the result by default.    
    public int Order { get; set; } = int.MinValue;
    
    /// <inheritdoc />
    public Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (next == null)
        {
            throw new ArgumentNullException(nameof(next));
        }
        
        /* 解析 controller */
        var controller = context.Controller;
        if (controller == null)
        {
            throw new InvalidOperationException(
                Resources.FormatPropertyOfTypeCannotBeNull(
                    nameof(context.Controller),
                    nameof(ResultExecutingContext)));
        }
        // 如果 controller 实现了 async result filter 接口
        if (controller is IAsyncResultFilter asyncResultFilter)
        {
            return asyncResultFilter.OnResultExecutionAsync(context, next);
        }
        // 如果 controller 实现了 result filter 接口
        else if (controller is IResultFilter resultFilter)
        {
            return ExecuteResultFilter(context, next, resultFilter);
        }
        // 否则，调用 next 委托
        else
        {
            return next();
        }
    }
    
    private static async Task ExecuteResultFilter(
        ResultExecutingContext context,
        ResultExecutionDelegate next,
        IResultFilter resultFilter)
    {
        // 执行 executing 方法
        resultFilter.OnResultExecuting(context);
        // 执行 executed 方法
        if (!context.Cancel)
        {
            resultFilter.OnResultExecuted(await next());
        }
    }
}

```

##### 2.7.3 client error result filter

```c#
internal class ClientErrorResultFilter : 
	IAlwaysRunResultFilter, 
	IOrderedFilter
{
    internal const int FilterOrder = -2000;
    // Gets the filter order. Defaults to -2000 so that it runs early.    
    public int Order => FilterOrder;
    
    private readonly IClientErrorFactory _clientErrorFactory;
    private readonly ILogger<ClientErrorResultFilter> _logger;
    
    public ClientErrorResultFilter(
        IClientErrorFactory clientErrorFactory,
        ILogger<ClientErrorResultFilter> logger)
    {
        _clientErrorFactory = clientErrorFactory 
            					 ?? throw new ArgumentNullException(
            									 nameof(clientErrorFactory));
       
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        /* 如果 context 的 result 没有实现 client error action result 接口，
           直接返回 */
        if (!(context.Result is IClientErrorActionResult clientError))
        {
            return;
        }
        
        /* 如果 result 的 status code <400，即不是服务端错误，直接返回 */
        // We do not have an upper bound on the allowed status code. 
        // This allows this filter to be used for 5xx and later status codes.
        if (clientError.StatusCode < 400)
        {
            return;
        }
        
        /* 创建 client error result，注入到 result executing context */
        var result = _clientErrorFactory.GetClientError(context, clientError);
        if (result == null)
        {
            return;
        }
        
        _logger.TransformingClientError(
            		context.Result.GetType(), 
            		result.GetType(), 
            		clientError.StatusCode);

        context.Result = result;
    }    
        
    public void OnResultExecuted(ResultExecutedContext context)
    {
    }        
}

```

##### 2.7.4 client error result filter factory

```c#
internal sealed class ClientErrorResultFilterFactory : 
	IFilterFactory, 
	IOrderedFilter
{
    public int Order => ClientErrorResultFilter.FilterOrder;    
    public bool IsReusable => true;
    
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        var resultFilter = 
            ActivatorUtilities.CreateInstance<ClientErrorResultFilter>(serviceProvider);
        
        return resultFilter;
    }
}

```

##### 2.7.5 produce attribute

* 指定 action 的返回类型

```c#
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class ProducesAttribute : Attribute, IResultFilter, IOrderedFilter, IApiResponseMetadataProvider
{
    /// <summary>
        /// Initializes an instance of <see cref="ProducesAttribute"/>.
        /// </summary>
        /// <param name="type">The <see cref="Type"/> of object that is going to be written in the response.</param>
    public ProducesAttribute(Type type)
    {   
        Type = type ?? throw new ArgumentNullException(nameof(type));
        ContentTypes = new MediaTypeCollection();
    }
    
    /// <summary>
        /// Initializes an instance of <see cref="ProducesAttribute"/> with allowed content types.
        /// </summary>
        /// <param name="contentType">The allowed content type for a response.</param>
        /// <param name="additionalContentTypes">Additional allowed content types for a response.</param>
    public ProducesAttribute(string contentType, params string[] additionalContentTypes)
    {
        if (contentType == null)
        {
            throw new ArgumentNullException(nameof(contentType));
        }
        
        // We want to ensure that the given provided content types are valid values, so
        // we validate them using the semantics of MediaTypeHeaderValue.
        MediaTypeHeaderValue.Parse(contentType);
        
        for (var i = 0; i < additionalContentTypes.Length; i++)
        {
            MediaTypeHeaderValue.Parse(additionalContentTypes[i]);
        }
        
        ContentTypes = GetContentTypes(contentType, additionalContentTypes);
    }
    
    /// <inheritdoc />
    public Type Type { get; set; }
    
    /// <summary>
    /// Gets or sets the supported response content types. Used to set <see cref="ObjectResult.ContentTypes"/>.
        /// </summary>
    public MediaTypeCollection ContentTypes { get; set; }
    
    /// <inheritdoc />
    public int StatusCode => StatusCodes.Status200OK;
    
    /// <inheritdoc />
    public int Order { get; set; }
    
    /// <inheritdoc />
    public virtual void OnResultExecuting(ResultExecutingContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        if (context.Result is ObjectResult objectResult)
        {
            // Check if there are any IFormatFilter in the pipeline, and if any of them is active. If there is one,
                // do not override the content type value.
            for (var i = 0; i < context.Filters.Count; i++)
            {
                var filter = context.Filters[i] as IFormatFilter;
                
                if (filter?.GetFormat(context) != null)
                {
                    return;
                }
            }
            
            SetContentTypes(objectResult.ContentTypes);
        }
    }
    
    /// <inheritdoc />
    public virtual void OnResultExecuted(ResultExecutedContext context)
    {
    }
    
    /// <inheritdoc />
    public void SetContentTypes(MediaTypeCollection contentTypes)
    {
        contentTypes.Clear();
        foreach (var contentType in ContentTypes)
        {
            contentTypes.Add(contentType);
        }
    }
    
    private MediaTypeCollection GetContentTypes(string firstArg, string[] args)
    {
        var completeArgs = new List<string>(args.Length + 1);
        completeArgs.Add(firstArg);
        completeArgs.AddRange(args);
        var contentTypes = new MediaTypeCollection();
        foreach (var arg in completeArgs)
        {
            var contentType = new MediaType(arg);
            if (contentType.HasWildcard)
            {
                throw new InvalidOperationException(
                    Resources.FormatMatchAllContentTypeIsNotAllowed(arg));
            }
            
            contentTypes.Add(arg);
        }
        
        return contentTypes;
    }
}

```

