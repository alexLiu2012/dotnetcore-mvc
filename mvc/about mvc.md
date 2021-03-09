## about mvc



### 1. about



### 2. details



#### 2.1 action descriptor



#### 2.2 route



#### 2.3 action constraint



#### 2.4 parameter



#### 2.5 filter

##### 2.5.1 a

###### 2.5.1.1 IFilterMetadata

```c#
public interface IFilterMetadata
{
}

```

###### 2.5.1.2 filter context

```c#
public abstract class FilterContext : ActionContext
{
    public virtual IList<IFilterMetadata> Filters { get; }
    
    public FilterContext(
        ActionContext actionContext,
        IList<IFilterMetadata> filters) : base(actionContext)
    {
        if (filters == null)
        {
            throw new ArgumentNullException(nameof(filters));
        }
        
        Filters = filters;
    }            
               
    public bool IsEffectivePolicy<TMetadata>(TMetadata policy) 
        where TMetadata : IFilterMetadata
    {
        if (policy == null)
        {
            throw new ArgumentNullException(nameof(policy));
        }
            
        var effective = FindEffectivePolicy<TMetadata>();
        return ReferenceEquals(policy, effective);
    }
    
    
    [return: MaybeNull]
    public TMetadata FindEffectivePolicy<TMetadata>() 
        where TMetadata : IFilterMetadata
    {
        // The most specific policy is the one closest to the action 
        // (nearest the end of the list).
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

###### 2.5.1.3 filter factory

```c#
public interface IFilterFactory : IFilterMetadata
{    
    bool IsReusable { get; }        
    IFilterMetadata CreateInstance(IServiceProvider serviceProvider);
}

```

###### 2.5.1.4 filter container

```c#
public interface IFilterContainer
{        
    IFilterMetadata FilterDefinition { get; set; }
}

```

##### 2.5.2 filter provider

###### 2.5.2.1 接口

```c#
public interface IFilterProvider
{    
    int Order { get; }
        
    void OnProvidersExecuting(FilterProviderContext context);        
    void OnProvidersExecuted(FilterProviderContext context);
}

```

###### 2.5.2.2 filter provider context

```c#
public class FilterProviderContext
{
    public ActionContext ActionContext { get; set; }        
    public IList<FilterItem> Results { get; set; }
    
    public FilterProviderContext(ActionContext actionContext, IList<FilterItem> items)
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

###### 2.5.2.3 filter item

```c#
[DebuggerDisplay("FilterItem: {Filter}")]
public class FilterItem
{
    public FilterDescriptor Descriptor { get; } = default!;        
    public IFilterMetadata Filter { get; set; } = default!;        
    public bool IsReusable { get; set; }
    
    public FilterItem(
        FilterDescriptor descriptor)
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

###### 2.5.2.4 filter descriptor

```c#
[DebuggerDisplay("Filter = {Filter.ToString(),nq}, Order = {Order}")]
    public class FilterDescriptor
    {
        /// <summary>
        /// Creates a new <see cref="FilterDescriptor"/>.
        /// </summary>
        /// <param name="filter">The <see cref="IFilterMetadata"/>.</param>
        /// <param name="filterScope">The filter scope.</param>
        /// <remarks>
        /// If the <paramref name="filter"/> implements <see cref="IOrderedFilter"/>, then the value of
        /// <see cref="Order"/> will be taken from <see cref="IOrderedFilter.Order"/>. Otherwise the value
        /// of <see cref="Order"/> will default to <c>0</c>.
        /// </remarks>
        public FilterDescriptor(IFilterMetadata filter, int filterScope)
        {
            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            Filter = filter;
            Scope = filterScope;


            if (Filter is IOrderedFilter orderedFilter)
            {
                Order = orderedFilter.Order;
            }
        }

        /// <summary>
        /// The <see cref="IFilterMetadata"/> instance.
        /// </summary>
        public IFilterMetadata Filter { get; }

        /// <summary>
        /// The filter order.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// The filter scope.
        /// </summary>
        public int Scope { get; }
    }
```



##### 2.5.2 action filter

###### 2.5.2.1 IActionFilter

```c#
public interface IActionFilter : IFilterMetadata
{    
    void OnActionExecuting(ActionExecutingContext context);        
    void OnActionExecuted(ActionExecutedContext context);
}

```

###### 2.5.2.2 IAsyncActionFilter

```c#
public interface IAsyncActionFilter : IFilterMetadata
{    
    Task OnActionExecutionAsync(
        ActionExecutingContext context, 
        ActionExecutionDelegate next);
}

```

###### 2.5.2.3 action executing context

```c#
public class ActionExecutingContext : FilterContext
    {
        /// <summary>
        /// Instantiates a new <see cref="ActionExecutingContext"/> instance.
        /// </summary>
        /// <param name="actionContext">The <see cref="ActionContext"/>.</param>
        /// <param name="filters">All applicable <see cref="IFilterMetadata"/> implementations.</param>
        /// <param name="actionArguments">
        /// The arguments to pass when invoking the action. Keys are parameter names.
        /// </param>
        /// <param name="controller">The controller instance containing the action.</param>
        public ActionExecutingContext(
            ActionContext actionContext,
            IList<IFilterMetadata> filters,
            IDictionary<string, object?> actionArguments,
            object controller)
            : base(actionContext, filters)
        {
            if (actionArguments == null)
            {
                throw new ArgumentNullException(nameof(actionArguments));
            }

            ActionArguments = actionArguments;
            Controller = controller;
        }

        /// <summary>
        /// Gets or sets the <see cref="IActionResult"/> to execute. Setting <see cref="Result"/> to a non-<c>null</c>
        /// value inside an action filter will short-circuit the action and any remaining action filters.
        /// </summary>
        public virtual IActionResult? Result { get; set; }

        /// <summary>
        /// Gets the arguments to pass when invoking the action. Keys are parameter names.
        /// </summary>
        public virtual IDictionary<string, object?> ActionArguments { get; }

        /// <summary>
        /// Gets the controller instance containing the action.
        /// </summary>
        public virtual object Controller { get; }
    }
```

###### 2.5.2.4 action executed context

```c#
public class ActionExecutedContext : FilterContext
    {
        private Exception? _exception;
        private ExceptionDispatchInfo? _exceptionDispatchInfo;

        /// <summary>
        /// Instantiates a new <see cref="ActionExecutingContext"/> instance.
        /// </summary>
        /// <param name="actionContext">The <see cref="ActionContext"/>.</param>
        /// <param name="filters">All applicable <see cref="IFilterMetadata"/> implementations.</param>
        /// <param name="controller">The controller instance containing the action.</param>
        public ActionExecutedContext(
            ActionContext actionContext,
            IList<IFilterMetadata> filters,
            object controller)
            : base(actionContext, filters)
        {
            Controller = controller;
        }

        /// <summary>
        /// Gets or sets an indication that an action filter short-circuited the action and the action filter pipeline.
        /// </summary>
        public virtual bool Canceled { get; set; }

        /// <summary>
        /// Gets the controller instance containing the action.
        /// </summary>
        public virtual object Controller { get; }

        /// <summary>
        /// Gets or sets the <see cref="System.Exception"/> caught while executing the action or action filters, if
        /// any.
        /// </summary>
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

        /// <summary>
        /// Gets or sets the <see cref="System.Runtime.ExceptionServices.ExceptionDispatchInfo"/> for the
        /// <see cref="Exception"/>, if an <see cref="System.Exception"/> was caught and this information captured.
        /// </summary>
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

        /// <summary>
        /// Gets or sets an indication that the <see cref="Exception"/> has been handled.
        /// </summary>
        public virtual bool ExceptionHandled { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="IActionResult"/>.
        /// </summary>
        public virtual IActionResult? Result { get; set; }
    }
```

###### 2.5.2.5 action execution delegate

```c#
public delegate Task<ActionExecutedContext> ActionExecutionDelegate();
```



##### 2.5.3 authorization filter

###### 2.5.3.1 IAuthorizationFilter

```c#
public interface IAuthorizationFilter : IFilterMetadata
{        
    void OnAuthorization(AuthorizationFilterContext context);
}

```

###### 2.5.3.2 IAsyncAuthorizationFilter

```c#
public interface IAsyncAuthorizationFilter : IFilterMetadata
{    
    Task OnAuthorizationAsync(AuthorizationFilterContext context);
}

```

###### 2.5.3.3 allow anonymous filter

```c#
public interface IAllowAnonymousFilter : IFilterMetadata
{
}
```

###### 2.5.3.4 authorization filter context

```c#
public class AuthorizationFilterContext : FilterContext
{    
    public AuthorizationFilterContext(
        ActionContext actionContext,
        IList<IFilterMetadata> filters)
        	: base(actionContext, filters)
    {
    }
        
    public virtual IActionResult? Result { get; set; }
}

```

##### 2.5.4 exception filter

###### 2.5.4.1 IExceptionFilter

```c#
public interface IExceptionFilter : IFilterMetadata
{    
    void OnException(ExceptionContext context);
}

```

###### 2.5.4.2 IAsyncExceptionFilter

```c#
public interface IAsyncExceptionFilter : IFilterMetadata
{    
    Task OnExceptionAsync(ExceptionContext context);
}

```

###### 2.5.4.3 exception context

```c#
public class ExceptionContext : FilterContext
    {
        private Exception? _exception;
        private ExceptionDispatchInfo? _exceptionDispatchInfo;

        /// <summary>
        /// Instantiates a new <see cref="ExceptionContext"/> instance.
        /// </summary>
        /// <param name="actionContext">The <see cref="ActionContext"/>.</param>
        /// <param name="filters">All applicable <see cref="IFilterMetadata"/> implementations.</param>
        public ExceptionContext(ActionContext actionContext, IList<IFilterMetadata> filters)
            : base(actionContext, filters)
        {
        }

        /// <summary>
        /// Gets or sets the <see cref="System.Exception"/> caught while executing the action.
        /// </summary>
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

        /// <summary>
        /// Gets or sets the <see cref="System.Runtime.ExceptionServices.ExceptionDispatchInfo"/> for the
        /// <see cref="Exception"/>, if this information was captured.
        /// </summary>
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

        /// <summary>
        /// Gets or sets an indication that the <see cref="Exception"/> has been handled.
        /// </summary>
        public virtual bool ExceptionHandled { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="IActionResult"/>.
        /// </summary>
        public virtual IActionResult? Result { get; set; }
    }
```

##### 2.5.5 order filter

```c#
public interface IOrderedFilter : IFilterMetadata
{
    int Order { get; }
}

```

##### 2.5.6 resource filter

###### 2.5.6.1 IResourceFilter

```c#
public interface IResourceFilter : IFilterMetadata
{    
    void OnResourceExecuting(ResourceExecutingContext context);
        
    void OnResourceExecuted(ResourceExecutedContext context);
}

```

###### 2.5.6.2 IAsyncResourceFilter

```c#
public interface IAsyncResourceFilter : IFilterMetadata
{    
    Task OnResourceExecutionAsync(
            ResourceExecutingContext context,
            ResourceExecutionDelegate next);
}

```

###### 2.5.6.3 resource executing context

```c#
public class ResourceExecutingContext : FilterContext
    {
        /// <summary>
        /// Creates a new <see cref="ResourceExecutingContext"/>.
        /// </summary>
        /// <param name="actionContext">The <see cref="ActionContext"/>.</param>
        /// <param name="filters">The list of <see cref="IFilterMetadata"/> instances.</param>
        /// <param name="valueProviderFactories">The list of <see cref="IValueProviderFactory"/> instances.</param>
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

        /// <summary>
        /// Gets or sets the result of the action to be executed.
        /// </summary>
        /// <remarks>
        /// Setting <see cref="Result"/> to a non-<c>null</c> value inside a resource filter will
        /// short-circuit execution of additional resource filters and the action itself.
        /// </remarks>
        public virtual IActionResult? Result { get; set; }

        /// <summary>
        /// Gets the list of <see cref="IValueProviderFactory"/> instances used by model binding.
        /// </summary>
        public IList<IValueProviderFactory> ValueProviderFactories { get; }
    }
```

###### 2.5.6.4 resource executed context

```c#
public class ResourceExecutedContext : FilterContext
    {
        private Exception? _exception;
        private ExceptionDispatchInfo? _exceptionDispatchInfo;

        /// <summary>
        /// Creates a new <see cref="ResourceExecutedContext"/>.
        /// </summary>
        /// <param name="actionContext">The <see cref="ActionContext"/>.</param>
        /// <param name="filters">The list of <see cref="IFilterMetadata"/> instances.</param>
        public ResourceExecutedContext(ActionContext actionContext, IList<IFilterMetadata> filters)
            : base(actionContext, filters)
        {
        }

        /// <summary>
        /// Gets or sets a value which indicates whether or not execution was canceled by a resource filter.
        /// If true, then a resource filter short-circuited execution by setting
        /// <see cref="ResourceExecutingContext.Result"/>.
        /// </summary>
        public virtual bool Canceled { get; set; }

        /// <summary>
        /// Gets or set the current <see cref="Exception"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Setting <see cref="Exception"/> or <see cref="ExceptionDispatchInfo"/> to <c>null</c> will treat
        /// the exception as handled, and it will not be rethrown by the runtime.
        /// </para>
        /// <para>
        /// Setting <see cref="ExceptionHandled"/> to <c>true</c> will also mark the exception as handled.
        /// </para>
        /// </remarks>
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

        /// <summary>
        /// Gets or set the current <see cref="Exception"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Setting <see cref="Exception"/> or <see cref="ExceptionDispatchInfo"/> to <c>null</c> will treat
        /// the exception as handled, and it will not be rethrown by the runtime.
        /// </para>
        /// <para>
        /// Setting <see cref="ExceptionHandled"/> to <c>true</c> will also mark the exception as handled.
        /// </para>
        /// </remarks>
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

        /// <summary>
        /// <para>
        /// Gets or sets a value indicating whether or not the current <see cref="Exception"/> has been handled.
        /// </para>
        /// <para>
        /// If <c>false</c> the <see cref="Exception"/> will be rethrown by the runtime after resource filters
        /// have executed.
        /// </para>
        /// </summary>
        public virtual bool ExceptionHandled { get; set; }

        /// <summary>
        /// Gets or sets the result.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="Result"/> may be provided by execution of the action itself or by another
        /// filter.
        /// </para>
        /// <para>
        /// The <see cref="Result"/> has already been written to the response before being made available
        /// to resource filters.
        /// </para>
        /// </remarks>
        public virtual IActionResult? Result { get; set; }
    }
```

###### 2.5.6.5 resource execution delegate

```c#
public delegate Task<ResourceExecutedContext> ResourceExecutionDelegate();
```



##### 2.5.7 result filter

###### 2.5.7.1 IResultFilter

```c#
public interface IResultFilter : IFilterMetadata
{    
    void OnResultExecuting(ResultExecutingContext context);
    
    void OnResultExecuted(ResultExecutedContext context);
}

```

###### 2.5.7.2 IAsyncResultFilter

```c#
public interface IAsyncResultFilter : IFilterMetadata
{       
    Task OnResultExecutionAsync(
        ResultExecutingContext context, 
        ResultExecutionDelegate next);
}

```

###### 2.5.7.3 result executing context

```c#
public class ResultExecutingContext : FilterContext
    {
        /// <summary>
        /// Instantiates a new <see cref="ResultExecutingContext"/> instance.
        /// </summary>
        /// <param name="actionContext">The <see cref="ActionContext"/>.</param>
        /// <param name="filters">All applicable <see cref="IFilterMetadata"/> implementations.</param>
        /// <param name="result">The <see cref="IActionResult"/> of the action and action filters.</param>
        /// <param name="controller">The controller instance containing the action.</param>
        public ResultExecutingContext(
            ActionContext actionContext,
            IList<IFilterMetadata> filters,
            IActionResult result,
            object controller)
            : base(actionContext, filters)
        {
            Result = result;
            Controller = controller;
        }

        /// <summary>
        /// Gets the controller instance containing the action.
        /// </summary>
        public virtual object Controller { get; }

        /// <summary>
        /// Gets or sets the <see cref="IActionResult"/> to execute. Setting <see cref="Result"/> to a non-<c>null</c>
        /// value inside a result filter will short-circuit the result and any remaining result filters.
        /// </summary>
        public virtual IActionResult Result { get; set; }

        /// <summary>
        /// Gets or sets an indication the result filter pipeline should be short-circuited.
        /// </summary>
        public virtual bool Cancel { get; set; }
    }
```

###### 2.5.7.4 result executed context

```c#
public class ResultExecutedContext : FilterContext
    {
        private Exception? _exception;
        private ExceptionDispatchInfo? _exceptionDispatchInfo;

        /// <summary>
        /// Instantiates a new <see cref="ResultExecutedContext"/> instance.
        /// </summary>
        /// <param name="actionContext">The <see cref="ActionContext"/>.</param>
        /// <param name="filters">All applicable <see cref="IFilterMetadata"/> implementations.</param>
        /// <param name="result">
        /// The <see cref="IActionResult"/> copied from <see cref="ResultExecutingContext.Result"/>.
        /// </param>
        /// <param name="controller">The controller instance containing the action.</param>
        public ResultExecutedContext(
            ActionContext actionContext,
            IList<IFilterMetadata> filters,
            IActionResult result,
            object controller)
            : base(actionContext, filters)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            Result = result;
            Controller = controller;
        }

        /// <summary>
        /// Gets or sets an indication that a result filter set <see cref="ResultExecutingContext.Cancel"/> to
        /// <c>true</c> and short-circuited the filter pipeline.
        /// </summary>
        public virtual bool Canceled { get; set; }

        /// <summary>
        /// Gets the controller instance containing the action.
        /// </summary>
        public virtual object Controller { get; }

        /// <summary>
        /// Gets or sets the <see cref="System.Exception"/> caught while executing the result or result filters, if
        /// any.
        /// </summary>
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

        /// <summary>
        /// Gets or sets the <see cref="System.Runtime.ExceptionServices.ExceptionDispatchInfo"/> for the
        /// <see cref="Exception"/>, if an <see cref="System.Exception"/> was caught and this information captured.
        /// </summary>
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

        /// <summary>
        /// Gets or sets an indication that the <see cref="Exception"/> has been handled.
        /// </summary>
        public virtual bool ExceptionHandled { get; set; }

        /// <summary>
        /// Gets the <see cref="IActionResult"/> copied from <see cref="ResultExecutingContext.Result"/>.
        /// </summary>
        public virtual IActionResult Result { get; }
    }
```

###### 2.5.7.5 result execution delegate

```c#
public delegate Task<ResultExecutedContext> ResultExecutionDelegate();
```



##### 2.5.8 always run result filter

```c#
public interface IAlwaysRunResultFilter : IResultFilter
{
}

```



##### 2.5.4 实现

###### 2.5.4.1 controller action filter





#### 2.6 action invoker



#### 2.7 action result

##### 2.7.1 接口

```c#
public interface IActionResult
{    
    Task ExecuteResultAsync(ActionContext context);
}

```

```c#
public class ActionContext
{
    
    public ActionContext()
    {
        ModelState = new ModelStateDictionary();
    }
    
    
    public ActionContext(ActionContext actionContext)
        : this(
            actionContext.HttpContext,
            actionContext.RouteData,
            actionContext.ActionDescriptor,
            actionContext.ModelState)
        {
        }
    
    
    public ActionContext(
        HttpContext httpContext,
        RouteData routeData,
        ActionDescriptor actionDescriptor)
        : this(httpContext, routeData, actionDescriptor, new ModelStateDictionary())
        {
        }
    
    
    public ActionContext(
        HttpContext httpContext,
        RouteData routeData,
        ActionDescriptor actionDescriptor,
        ModelStateDictionary modelState)
    {
        if (httpContext == null)
        {
            throw new ArgumentNullException(nameof(httpContext));
        }        
        if (routeData == null)
        {
            throw new ArgumentNullException(nameof(routeData));
        }        
        if (actionDescriptor == null)
        {
            throw new ArgumentNullException(nameof(actionDescriptor));
        }        
        if (modelState == null)
        {
            throw new ArgumentNullException(nameof(modelState));
        }
        
        HttpContext = httpContext;
        RouteData = routeData;
        ActionDescriptor = actionDescriptor;
        ModelState = modelState;
    }

        
    public ActionDescriptor ActionDescriptor { get; set; } = default!;
    
    
    public HttpContext HttpContext { get; set; } = default!;
    
    
    public ModelStateDictionary ModelState { get; } = default!;
    
    
    public RouteData RouteData { get; set; } = default!;
}

```



#### 2.1 api explorer

##### 2.1.1 api descriptor

```c#
[DebuggerDisplay("{ActionDescriptor.DisplayName,nq}")]
public class ApiDescription
{
    
    public ActionDescriptor ActionDescriptor { get; set; } = default!;        
    public string? GroupName { get; set; }        
    public string? HttpMethod { get; set; }
    
    
    public IList<ApiParameterDescription> ParameterDescriptions { get; } = 
        new List<ApiParameterDescription>();
        
    public IDictionary<object, object> Properties { get; } = 
        new Dictionary<object, object>();
        
    public string RelativePath { get; set; } = default!;
        
    public IList<ApiRequestFormat> SupportedRequestFormats { get; } = 
        new List<ApiRequestFormat>();
        
    public IList<ApiResponseType> SupportedResponseTypes { get; } = 
        new List<ApiResponseType>();
}

```

##### 2.1.2 api parameter description

```c#
public class ApiParameterDescription
{    
    public ModelMetadata ModelMetadata { get; set; } = default!;        
    public string Name { get; set; } = default!;        
    public ApiParameterRouteInfo? RouteInfo { get; set; }        
    public BindingSource Source { get; set; } = default!;        
    public BindingInfo? BindingInfo { get; set; }        
    public Type Type { get; set; } = default!;        
    public ParameterDescriptor ParameterDescriptor { get; set; } = default!;        
    public bool IsRequired { get; set; }        
    public object? DefaultValue { get; set; }
}

```

###### 2.1.2.1 api parameter route info

```c#
public class ApiParameterRouteInfo
    {
        /// <summary>
        /// Gets or sets the set of <see cref="IRouteConstraint"/> objects for the parameter.
        /// </summary>
        /// <remarks>
        /// Route constraints are only applied when a value is bound from a URL's path. See
        /// <see cref="ApiParameterDescription.Source"/> for the data source considered.
        /// </remarks>
        public IEnumerable<IRouteConstraint>? Constraints { get; set; }

        /// <summary>
        /// Gets or sets the default value for the parameter.
        /// </summary>
        public object? DefaultValue { get; set; }

        /// <summary>
        /// Gets a value indicating whether not a parameter is considered optional by routing.
        /// </summary>
        /// <remarks>
        /// An optional parameter is considered optional by the routing system. This does not imply
        /// that the parameter is considered optional by the action.
        ///
        /// If the parameter uses <see cref="ModelBinding.BindingSource.ModelBinding"/> for the value of
        /// <see cref="ApiParameterDescription.Source"/> then the value may also come from the
        /// URL query string or form data.
        /// </remarks>
        public bool IsOptional { get; set; }
    }
```

###### 2.1.2.2 paramter descriptor

```c#

```

##### 2.1.3 api request format

```c#
public class ApiRequestFormat
    {
        /// <summary>
        /// The formatter used to read this request.
        /// </summary>
        public IInputFormatter Formatter { get; set; } = default!;

        /// <summary>
        /// The media type of the request.
        /// </summary>
        public string MediaType { get; set; } = default!;
    }
```

##### 2.1.4 api response type

```c#
public class ApiResponseType
    {
        /// <summary>
        /// Gets or sets the response formats supported by this type.
        /// </summary>
        public IList<ApiResponseFormat> ApiResponseFormats { get; set; } = new List<ApiResponseFormat>();

        /// <summary>
        /// Gets or sets <see cref="ModelBinding.ModelMetadata"/> for the <see cref="Type"/> or null.
        /// </summary>
        /// <remarks>
        /// Will be null if <see cref="Type"/> is null or void.
        /// </remarks>
        public ModelMetadata? ModelMetadata { get; set; }

        /// <summary>
        /// Gets or sets the CLR data type of the response or null.
        /// </summary>
        /// <remarks>
        /// Will be null if the action returns no response, or if the response type is unclear. Use
        /// <c>Microsoft.AspNetCore.Mvc.ProducesAttribute</c> or <c>Microsoft.AspNetCore.Mvc.ProducesResponseTypeAttribute</c> on an action method
        /// to specify a response type.
        /// </remarks>
        public Type? Type { get; set; }

        /// <summary>
        /// Gets or sets the HTTP response status code.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the response type represents a default response.
        /// </summary>
        /// <remarks>
        /// If an <see cref="ApiDescription"/> has a default response, then the <see cref="StatusCode"/> property should be ignored. This response
        /// will be used when a more specific response format does not apply. The common use of a default response is to specify the format
        /// for communicating error conditions.
        /// </remarks>
        public bool IsDefaultResponse { get; set; }
    }
```

###### 2.1.4.1 api response format

```c#
 public class ApiResponseFormat
    {
        /// <summary>
        /// Gets or sets the formatter used to output this response.
        /// </summary>
        public IOutputFormatter Formatter { get; set; } = default!;

        /// <summary>
        /// Gets or sets the media type of the response.
        /// </summary>
        public string MediaType { get; set; } = default!;
    }
```

##### 2.1.5 api descriptor provider

```c#
public interface IApiDescriptionProvider
    {
        /// <summary>
        /// Gets the order value for determining the order of execution of providers. Providers execute in
        /// ascending numeric value of the <see cref="Order"/> property.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Providers are executed in an ordering determined by an ascending sort of the <see cref="Order"/> property.
        /// A provider with a lower numeric value of <see cref="Order"/> will have its
        /// <see cref="OnProvidersExecuting"/> called before that of a provider with a higher numeric value of
        /// <see cref="Order"/>. The <see cref="OnProvidersExecuted"/> method is called in the reverse ordering after
        /// all calls to <see cref="OnProvidersExecuting"/>. A provider with a lower numeric value of
        /// <see cref="Order"/> will have its <see cref="OnProvidersExecuted"/> method called after that of a provider
        /// with a higher numeric value of <see cref="Order"/>.
        /// </para>
        /// <para>
        /// If two providers have the same numeric value of <see cref="Order"/>, then their relative execution order
        /// is undefined.
        /// </para>
        /// </remarks>
        int Order { get; }

        /// <summary>
        /// Creates or modifies <see cref="ApiDescription"/>s.
        /// </summary>
        /// <param name="context">The <see cref="ApiDescriptionProviderContext"/>.</param>
        void OnProvidersExecuting(ApiDescriptionProviderContext context);

        /// <summary>
        /// Called after <see cref="IApiDescriptionProvider"/> implementations with higher <see cref="Order"/> values have been called.
        /// </summary>
        /// <param name="context">The <see cref="ApiDescriptionProviderContext"/>.</param>
        void OnProvidersExecuted(ApiDescriptionProviderContext context);
    }
```

###### 2.1.5.1 api description provider context

```c#
public class ApiDescriptionProviderContext
    {
        /// <summary>
        /// Creates a new instance of <see cref="ApiDescriptionProviderContext"/>.
        /// </summary>
        /// <param name="actions">The list of actions.</param>
        public ApiDescriptionProviderContext(IReadOnlyList<ActionDescriptor> actions)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            Actions = actions;

            Results = new List<ApiDescription>();
        }

        /// <summary>
        /// The list of actions.
        /// </summary>
        public IReadOnlyList<ActionDescriptor> Actions { get; }

        /// <summary>
        /// The list of resulting <see cref="ApiDescription"/>.
        /// </summary>
        public IList<ApiDescription> Results { get; }
    }
```

#### 2.2 action descriptor

##### 2.2.1 action descriptor

###### 2.2.1.1 基类

```c#
 public class ActionDescriptor
 {
     public string Id { get; }
     public virtual string? DisplayName { get; set; }
      public IDictionary<object, object> Properties { get; set; } = 
         default!;
     
     // for convention routing（约定路由）
     public IDictionary<string, string> RouteValues { get; set; }  
     // for attribute routing（特性路由）     
     public AttributeRouteInfo? AttributeRouteInfo { get; set; }
          
     public IList<IActionConstraintMetadata>? ActionConstraints { get; set; }          
     public IList<object> EndpointMetadata { get; set; } = 
         Array.Empty<ParameterDescriptor>();          
     public IList<ParameterDescriptor> Parameters { get; set; } = 
         Array.Empty<ParameterDescriptor>();          
     public IList<ParameterDescriptor> BoundProperties { get; set; } = 
         Array.Empty<ParameterDescriptor>();          
     public IList<FilterDescriptor> FilterDescriptors { get; set; } = 
         Array.Empty<FilterDescriptor>();
    
     
     public ActionDescriptor()
     {
         Id = Guid.NewGuid().ToString();
         Properties = new Dictionary<object, object>();
         RouteValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
     }                                      
 }

```

###### 2.2.1.2 扩展 - set & get property

```c#
public static class ActionDescriptorExtensions
{
    // get property
    public static T GetProperty<T>(
        this ActionDescriptor actionDescriptor)
    {
        if (actionDescriptor == null)
        {
            throw new ArgumentNullException(nameof(actionDescriptor));
        }
        
        if (actionDescriptor
            	.Properties
            	.TryGetValue(typeof(T), out var value))
        {
            return (T)value;
        }
        else
        {
            return default!;
        }
    }
    
    // set property
    public static void SetProperty<T>(
        this ActionDescriptor actionDescriptor, 
        T value)
    {
        if (actionDescriptor == null)
        {
            throw new ArgumentNullException(nameof(actionDescriptor));
        }        
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        
        actionDescriptor.Properties[typeof(T)] = value;
    }
}

```

##### 2.2.2 route

###### 2.2.2.1 route value



###### 2.2.2.2 attribute route info

```c#
public class AttributeRouteInfo
    {
        /// <summary>
        /// The route template. May be <see langword="null" /> if the action has no attribute routes.
        /// </summary>
        public string? Template { get; set; }

        /// <summary>
        /// Gets the order of the route associated with a given action. This property determines
        /// the order in which routes get executed. Routes with a lower order value are tried first. In case a route
        /// doesn't specify a value, it gets a default order of 0.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Gets the name of the route associated with a given action. This property can be used
        /// to generate a link by referring to the route by name instead of attempting to match a
        /// route by provided route data.
        /// </summary>
        public string Name { get; set; } = default!;

        /// <summary>
        /// Gets or sets a value that determines if the route entry associated with this model participates in link generation.
        /// </summary>
        public bool SuppressLinkGeneration { get; set; }

        /// <summary>
        /// Gets or sets a value that determines if the route entry associated with this model participates in path matching (inbound routing).
        /// </summary>
        public bool SuppressPathMatching { get; set; }
    }
```



##### 2.2.3  contstraint

###### 2.2.3.1 IConstraintMetadata

```c#
public interface IActionConstraintMetadata
{
}

```

###### 2.2.3.2 IActionConstraint

```c#
public interface IActionConstraint : IActionConstraintMetadata
{    
    bool Accept(ActionConstraintContext context);
}

```

###### 2.2.3.3 action constraint context

```c#
public class ActionConstraintContext
{
    
    public IReadOnlyList<ActionSelectorCandidate> Candidates { get; set; } = Array.Empty<ActionSelectorCandidate>();

        
        public ActionSelectorCandidate CurrentCandidate { get; set; } = default!;

        
        public RouteContext RouteContext { get; set; } = default!;
    }
```

###### 2.2.3.4 action selector candidate

```c#
public readonly struct ActionSelectorCandidate
    {
        /// <summary>
        /// Creates a new <see cref="ActionSelectorCandidate"/>.
        /// </summary>
        /// <param name="action">The <see cref="ActionDescriptor"/> representing a candidate for selection.</param>
        /// <param name="constraints">
        /// The list of <see cref="IActionConstraint"/> instances associated with <paramref name="action"/>.
        /// </param>
        public ActionSelectorCandidate(ActionDescriptor action, IReadOnlyList<IActionConstraint> constraints)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            Action = action;
            Constraints = constraints;
        }

        /// <summary>
        /// The <see cref="ActionDescriptor"/> representing a candidate for selection.
        /// </summary>
        public ActionDescriptor Action { get; }

        /// <summary>
        /// The list of <see cref="IActionConstraint"/> instances associated with <see name="Action"/>.
        /// </summary>
        public IReadOnlyList<IActionConstraint> Constraints { get; }
    }
```

###### 2.2.3.5 action constraint factory

```c#
public interface IActionConstraintFactory : IActionConstraintMetadata
    {
        /// <summary>
        /// Gets a value that indicates if the result of <see cref="CreateInstance(IServiceProvider)"/>
        /// can be reused across requests.
        /// </summary>
        bool IsReusable { get; }

        /// <summary>
        /// Creates a new <see cref="IActionConstraint"/>.
        /// </summary>
        /// <param name="services">The per-request services.</param>
        /// <returns>An <see cref="IActionConstraint"/>.</returns>
        IActionConstraint CreateInstance(IServiceProvider services);
    }
```



###### 2.2.3.5 action proivider

```c#
public interface IActionConstraintProvider
    {
        /// <summary>
        /// Gets the order value for determining the order of execution of providers. Providers execute in
        /// ascending numeric value of the <see cref="Order"/> property.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Providers are executed in an ordering determined by an ascending sort of the <see cref="Order"/> property.
        /// A provider with a lower numeric value of <see cref="Order"/> will have its
        /// <see cref="OnProvidersExecuting"/> called before that of a provider with a higher numeric value of
        /// <see cref="Order"/>. The <see cref="OnProvidersExecuted"/> method is called in the reverse ordering after
        /// all calls to <see cref="OnProvidersExecuting"/>. A provider with a lower numeric value of
        /// <see cref="Order"/> will have its <see cref="OnProvidersExecuted"/> method called after that of a provider
        /// with a higher numeric value of <see cref="Order"/>.
        /// </para>
        /// <para>
        /// If two providers have the same numeric value of <see cref="Order"/>, then their relative execution order
        /// is undefined.
        /// </para>
        /// </remarks>
        int Order { get; }

        /// <summary>
        /// Called to execute the provider. 
        /// <see cref="Order"/> for details on the order of execution of <see cref="OnProvidersExecuting(ActionConstraintProviderContext)"/>.
        /// </summary>
        /// <param name="context">The <see cref="ActionConstraintProviderContext"/>.</param>
        void OnProvidersExecuting(ActionConstraintProviderContext context);

        /// <summary>
        /// Called to execute the provider, after the <see cref="OnProvidersExecuting"/> methods of all providers,
        /// have been called.
        /// <see cref="Order"/> for details on the order of execution of <see cref="OnProvidersExecuted(ActionConstraintProviderContext)"/>.
        /// </summary>
        /// <param name="context">The <see cref="ActionConstraintProviderContext"/>.</param>
        void OnProvidersExecuted(ActionConstraintProviderContext context);
    }
```

###### 2.2.3.6 action constraint provider context

```C#
 public class ActionConstraintProviderContext
    {
        /// <summary>
        /// Creates a new <see cref="ActionConstraintProviderContext"/>.
        /// </summary>
        /// <param name="context">The <see cref="Http.HttpContext"/> associated with the request.</param>
        /// <param name="action">The <see cref="ActionDescriptor"/> for which constraints are being created.</param>
        /// <param name="items">The list of <see cref="ActionConstraintItem"/> objects.</param>
        public ActionConstraintProviderContext(
            HttpContext context,
            ActionDescriptor action,
            IList<ActionConstraintItem> items)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            HttpContext = context;
            Action = action;
            Results = items;
        }

        /// <summary>
        /// The <see cref="Http.HttpContext"/> associated with the request.
        /// </summary>
        public HttpContext HttpContext { get; }

        /// <summary>
        /// The <see cref="ActionDescriptor"/> for which constraints are being created.
        /// </summary>
        public ActionDescriptor Action { get; }

        /// <summary>
        /// The list of <see cref="ActionConstraintItem"/> objects.
        /// </summary>
        public IList<ActionConstraintItem> Results { get; }
    }
```

###### 2.2.3.7 action constraint item

```c#
public class ActionConstraintItem
    {
        /// <summary>
        /// Creates a new <see cref="ActionConstraintItem"/>.
        /// </summary>
        /// <param name="metadata">The <see cref="IActionConstraintMetadata"/> instance.</param>
        public ActionConstraintItem(IActionConstraintMetadata metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            Metadata = metadata;
        }

        /// <summary>
        /// The <see cref="IActionConstraint"/> associated with <see cref="Metadata"/>.
        /// </summary>
        public IActionConstraint Constraint { get; set; } = default!;

        /// <summary>
        /// The <see cref="IActionConstraintMetadata"/> instance.
        /// </summary>
        public IActionConstraintMetadata Metadata { get; }

        /// <summary>
        /// Gets or sets a value indicating whether or not <see cref="Constraint"/> can be reused across requests.
        /// </summary>
        public bool IsReusable { get; set; }
    }
```



##### 2.2.4 parameter

```c#
public class ParameterDescriptor
    {
        /// <summary>
        /// Gets or sets the parameter name.
        /// </summary>
        public string Name { get; set; } = default!;

        /// <summary>
        /// Gets or sets the type of the parameter.
        /// </summary>
        public Type ParameterType { get; set; } = default!;

        /// <summary>
        /// Gets or sets the <see cref="ModelBinding.BindingInfo"/> for the parameter.
        /// </summary>
        public BindingInfo BindingInfo { get; set; } = default!;
    }
```



##### 2.2.5 filter

###### 2.2.5.1 filter desciptor

```c#
/// <summary>
    /// Descriptor for an <see cref="IFilterMetadata"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="FilterDescriptor"/> describes an <see cref="IFilterMetadata"/> with an order and scope.
    ///
    /// Order and scope control the execution order of filters. Filters with a higher value of Order execute
    /// later in the pipeline.
    ///
    /// When filters have the same Order, the Scope value is used to determine the order of execution. Filters
    /// with a higher value of Scope execute later in the pipeline. See <c>Microsoft.AspNetCore.Mvc.FilterScope</c>
    /// for commonly used scopes.
    ///
    /// For <see cref="IExceptionFilter"/> implementations, the filter runs only after an exception has occurred,
    /// and so the observed order of execution will be opposite that of other filters.
    /// </remarks>
    [DebuggerDisplay("Filter = {Filter.ToString(),nq}, Order = {Order}")]
    public class FilterDescriptor
    {
        /// <summary>
        /// Creates a new <see cref="FilterDescriptor"/>.
        /// </summary>
        /// <param name="filter">The <see cref="IFilterMetadata"/>.</param>
        /// <param name="filterScope">The filter scope.</param>
        /// <remarks>
        /// If the <paramref name="filter"/> implements <see cref="IOrderedFilter"/>, then the value of
        /// <see cref="Order"/> will be taken from <see cref="IOrderedFilter.Order"/>. Otherwise the value
        /// of <see cref="Order"/> will default to <c>0</c>.
        /// </remarks>
        public FilterDescriptor(IFilterMetadata filter, int filterScope)
        {
            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            Filter = filter;
            Scope = filterScope;


            if (Filter is IOrderedFilter orderedFilter)
            {
                Order = orderedFilter.Order;
            }
        }

        /// <summary>
        /// The <see cref="IFilterMetadata"/> instance.
        /// </summary>
        public IFilterMetadata Filter { get; }

        /// <summary>
        /// The filter order.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// The filter scope.
        /// </summary>
        public int Scope { get; }
    }
```

###### 2.2.5.2 IFilterMetadata

```c#
public interface IFilterMetadata
    {
    }
```

###### 2.2.5.3 很多 filter



###### 2.2.5.4 filter context









##### 2.2.2 action descriptor provider

###### 2.2.2.1 接口

```c#
public interface IActionDescriptorProvider
{    
    int Order { get; }
        
    void OnProvidersExecuting(ActionDescriptorProviderContext context); 
    void OnProvidersExecuted(ActionDescriptorProviderContext context);
}

```

###### 2.2.2.2 action descriptor provider context

```c#
public class ActionDescriptorProviderContext
{    
    public IList<ActionDescriptor> Results { get; } = new List<ActionDescriptor>();
}

```

##### 2.2.3 action invoker

###### 2.2.3.1 接口

```c#
public interface IActionInvoker
{    
    Task InvokeAsync();
}

```

##### 2.2.4 action invoker provider

###### 2.2.4.1 接口

```c#
public interface IActionInvokerProvider
{    
    int Order { get; }
                
    void OnProvidersExecuting(ActionInvokerProviderContext context);   
    void OnProvidersExecuted(ActionInvokerProviderContext context);
}

```

###### 2.2.4.2 action invoker provider context

```c#
public class ActionInvokerProviderContext
{
    public ActionContext ActionContext { get; }        
    public IActionInvoker? Result { get; set; }
    
    public ActionInvokerProviderContext(ActionContext actionContext)
    {
        if (actionContext == null)
        {
            throw new ArgumentNullException(nameof(actionContext));
        }
        
        ActionContext = actionContext;
    }           
}

```



