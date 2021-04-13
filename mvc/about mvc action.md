## about mvc action



### 1. about



### 2. detaiils

#### 2.3 resource invoker

```c#
internal abstract class ResourceInvoker
{
    protected readonly DiagnosticListener _diagnosticListener;
    protected readonly ILogger _logger;
    protected readonly IActionContextAccessor _actionContextAccessor;
    protected readonly IActionResultTypeMapper _mapper;
    protected readonly ActionContext _actionContext;
    protected readonly IFilterMetadata[] _filters;
    protected readonly IList<IValueProviderFactory> _valueProviderFactories;
            
    private AuthorizationFilterContextSealed? _authorizationContext;    
    private ResourceExecutingContextSealed? _resourceExecutingContext;
    private ResourceExecutedContextSealed? _resourceExecutedContext;    
    private ExceptionContextSealed? _exceptionContext;  
    
    private ResultExecutingContextSealed? _resultExecutingContext;
    private ResultExecutedContextSealed? _resultExecutedContext;
            
    // Do not make this readonly, it's mutable. We don't want to make a copy.
    // https://blogs.msdn.microsoft.com/ericlippert/2008/05/14/mutating-readonly-structs/
    protected FilterCursor _cursor;
    
    protected IActionResult? _result;
    protected object? _instance;
    
    // 注入相关服务
    public ResourceInvoker(
        DiagnosticListener diagnosticListener,
        ILogger logger,
        IActionContextAccessor actionContextAccessor,
        IActionResultTypeMapper mapper,
        ActionContext actionContext,
        IFilterMetadata[] filters,
        IList<IValueProviderFactory> valueProviderFactories)
    {
        _diagnosticListener = 
            diagnosticListener ?? throw new ArgumentNullException(
            									nameof(diagnosticListener));        
        _logger = 
            logger ?? throw new ArgumentNullException(nameof(logger));
        
        _actionContextAccessor = 
            actionContextAccessor ?? throw new ArgumentNullException(
            									   nameof(actionContextAccessor));        
        _mapper = 
            mapper ?? throw new ArgumentNullException(nameof(mapper));
        
        _actionContext = 
            actionContext ?? throw new ArgumentNullException(
            							   nameof(actionContext));        
        _filters = 
            filters ?? throw new ArgumentNullException(nameof(filters));
        
        _valueProviderFactories = 
            valueProviderFactories ?? throw new ArgumentNullException(
            										nameof(valueProviderFactories));        
        _cursor = new FilterCursor(filters);
    }
    
    /* invoke async 方法 */
    public virtual Task InvokeAsync()
    {
        if (_diagnosticListener.IsEnabled() || 
            _logger.IsEnabled(LogLevel.Information))
        {
            return Logged(this);
        }
        
        _actionContextAccessor.ActionContext = _actionContext;
        var scope = _logger.ActionScope(_actionContext.ActionDescriptor);
        
        Task task;
        try
        {
            task = InvokeFilterPipelineAsync();
        }
        catch (Exception exception)
        {
            return Awaited(this, Task.FromException(exception), scope);
        }
        
        if (!task.IsCompletedSuccessfully)
        {
            return Awaited(this, task, scope);
        }
        
        return ReleaseResourcesCore(scope).AsTask();
        
        static async Task Awaited(
            ResourceInvoker invoker, 
            Task task, 
            IDisposable? scope)
        {
            try
            {
                await task;
            }
            finally
            {
                await invoker.ReleaseResourcesCore(scope);
            }
        }
        
        static async Task Logged(ResourceInvoker invoker)
        {
            var actionContext = invoker._actionContext;               
            invoker._actionContextAccessor.ActionContext = actionContext;
            try
            {
                var logger = invoker._logger;
                
                invoker._diagnosticListener.BeforeAction(
                    actionContext.ActionDescriptor,
                    actionContext.HttpContext,
                    actionContext.RouteData);
                
                var actionScope = logger.ActionScope(actionContext.ActionDescriptor);
                
                logger.ExecutingAction(actionContext.ActionDescriptor);
                
                var filters = invoker._filters;
                logger.AuthorizationFiltersExecutionPlan(filters);
                logger.ResourceFiltersExecutionPlan(filters);
                logger.ActionFiltersExecutionPlan(filters);
                logger.ExceptionFiltersExecutionPlan(filters);
                logger.ResultFiltersExecutionPlan(filters);
                
                var stopwatch = ValueStopwatch.StartNew();
                
                try
                {
                    await invoker.InvokeFilterPipelineAsync();
                }
                finally
                {
                    await invoker.ReleaseResourcesCore(actionScope);
                    logger.ExecutedAction(
                        	   actionContext.ActionDescriptor, 
                        	   stopwatch.GetElapsedTime());
                }
            }
            finally
            {
                invoker._diagnosticListener
                       .AfterAction(
                    		actionContext.ActionDescriptor,
		                    actionContext.HttpContext,
        		            actionContext.RouteData);
            }
        }
    } 
}

```

##### 2.3.1 组件

###### 2.3.1.1 filter context sealed

```c#
internal abstract class ResourceInvoker
{
    private sealed class AuthorizationFilterContextSealed : AuthorizationFilterContext
    {
        public AuthorizationFilterContextSealed(
            ActionContext actionContext, 
            IList<IFilterMetadata> filters) 
            	: base(actionContext, filters) 
        {
        }
    }
    
    private sealed class ResourceExecutingContextSealed : ResourceExecutingContext
    {
        public ResourceExecutingContextSealed(
            ActionContext actionContext, 
            IList<IFilterMetadata> filters, 
            IList<IValueProviderFactory> valueProviderFactories) 
            	: base(actionContext, filters, valueProviderFactories) 
        {
        }
    }
    
    private sealed class ResourceExecutedContextSealed : ResourceExecutedContext
    {
        public ResourceExecutedContextSealed(
            ActionContext actionContext, 
            IList<IFilterMetadata> filters) 
            	: base(actionContext, filters) 
        {
        }
    }
    
    private sealed class ExceptionContextSealed : ExceptionContext
    {
        public ExceptionContextSealed(
            ActionContext actionContext, 
            IList<IFilterMetadata> filters) 
            	: base(actionContext, filters) 
        {
        }
    }
    
    private sealed class ResultExecutingContextSealed : ResultExecutingContext
    {
        public ResultExecutingContextSealed(
            ActionContext actionContext,
            IList<IFilterMetadata> filters,
            IActionResult result,
            object controller)
            	: base(actionContext, filters, result, controller)
        {
        }
    }
    
    private sealed class ResultExecutedContextSealed : ResultExecutedContext
    {
        public ResultExecutedContextSealed(
            ActionContext actionContext,
            IList<IFilterMetadata> filters,
            IActionResult result,
            object controller)
            	: base(actionContext, filters, result, controller) 
        {
        }
    }                            
}

```

###### 2.3.1.2 filter type constants

```c#
internal abstract class ResourceInvoker
{
    private static class FilterTypeConstants
    {
        public const string AuthorizationFilter = "Authorization Filter";
        public const string ResourceFilter = "Resource Filter";
        public const string ActionFilter = "Action Filter";
        public const string ExceptionFilter = "Exception Filter";
        public const string ResultFilter = "Result Filter";
        public const string AlwaysRunResultFilter = "Always Run Result Filter";
    }        
}

```

###### 2.3.1.3 scope enum

```c#
internal abstract class ResourceInvoker
{
    private enum Scope
    {
        Invoker,
        Resource,
        Exception,
        Result,
    }
}

```

###### 2.3.1.4 state enum

```c#
internal abstract class ResourceInvoker
{
    private enum State
    {
        InvokeBegin,
        AuthorizationBegin,
        AuthorizationNext,
        AuthorizationAsyncBegin,
        AuthorizationAsyncEnd,
        AuthorizationSync,
        AuthorizationShortCircuit,
        AuthorizationEnd,
        ResourceBegin,
        ResourceNext,
        ResourceAsyncBegin,
        ResourceAsyncEnd,
        ResourceSyncBegin,
        ResourceSyncEnd,
        ResourceShortCircuit,
        ResourceInside,
        ResourceInsideEnd,
        ResourceEnd,
        ExceptionBegin,
        ExceptionNext,
        ExceptionAsyncBegin,
        ExceptionAsyncResume,
        ExceptionAsyncEnd,
        ExceptionSyncBegin,
        ExceptionSyncEnd,
        ExceptionInside,
        ExceptionHandled,
        ExceptionEnd,
        ActionBegin,
        ActionEnd,
        ResultBegin,
        ResultNext,
        ResultAsyncBegin,
        ResultAsyncEnd,
        ResultSyncBegin,
        ResultSyncEnd,
        ResultInside,
        ResultEnd,
        InvokeEnd,
    }
}
```

###### 2.3.1.5 filter cursor

```c#
internal readonly struct FilterCursorItem<TFilter, TFilterAsync>
{
    public TFilter Filter { get; }    
    public TFilterAsync FilterAsync { get; }
    
    public FilterCursorItem(TFilter filter, TFilterAsync filterAsync)
    {
        Filter = filter;
        FilterAsync = filterAsync;
    }        
}

internal struct FilterCursor
{
    private readonly IFilterMetadata[] _filters;
    private int _index;
    
    public FilterCursor(IFilterMetadata[] filters)
    {
        _filters = filters;
        _index = 0;
    }
    
    public void Reset()
    {
        _index = 0;
    }
    
    public FilterCursorItem<TFilter, TFilterAsync> 
        GetNextFilter<TFilter, TFilterAsync>()        
        	where TFilter : class            
            where TFilterAsync : class
    {
        while (_index < _filters.Length)
        {
            var filter = _filters[_index] as TFilter;
            var filterAsync = _filters[_index] as TFilterAsync;
            
            _index += 1;
            
            if (filter != null || filterAsync != null)
            {
                return new FilterCursorItem<TFilter, TFilterAsync>(filter, filterAsync);
            }
        }
        
        return default(FilterCursorItem<TFilter, TFilterAsync>);
    }
}

```

##### 2.3.1 release resources core

```c#
internal abstract class ResourceInvoker
{
    internal ValueTask ReleaseResourcesCore(IDisposable? scope)
    {
        Exception? releaseException = null;
        ValueTask releaseResult = default;
        try
        {
            // 具体 release 方法
            releaseResult = ReleaseResources();
                        
            if (!releaseResult.IsCompletedSuccessfully)
            {
                return HandleAsyncReleaseErrors(releaseResult, scope);
            }
        }
        catch (Exception exception)
        {
            releaseException = exception;
        }
        
        return HandleReleaseErrors(scope, releaseException);
        
        // 异步 release error handler
        static async ValueTask HandleAsyncReleaseErrors(
            					   ValueTask releaseResult, 
					               IDisposable? scope)
        {
            Exception? releaseException = null;
            try
            {
                await releaseResult;
            }
            catch (Exception exception)
            {
                releaseException = exception;
            }
            
            await HandleReleaseErrors(scope, releaseException);
        }
        
        // 同步 release errors handler
        static ValueTask HandleReleaseErrors(
            				 IDisposable? scope, 
            				 Exception? releaseException)
        {
            Exception? scopeException = null;
            try
            {
                scope?.Dispose();
            }
            catch (Exception exception)
            {
                scopeException = exception;
            }
            
            if (releaseException == null && 
                scopeException == null)
            {
                return default;
            }
            else if (releaseException != null && 
                     scopeException != null)
            {
                return ValueTask.FromException(
                    new AggregateException(releaseException, scopeException));
            }
            else if (releaseException != null)
            {
                return ValueTask.FromException(releaseException);
            }
            else
            {
                return ValueTask.FromException(scopeException!);
            }
        }
    }
        
    /* 具体 release 方法，在派生类中实现 */
    // In derived types, releases resources such as controller, model, or page instances 
    // created as part of invoking the inner pipeline.     
    protected abstract ValueTask ReleaseResources();
}

```

##### 2.3.2 invoke filter pipeline async

```c#
internal abstract class ResourceInvoker
{
    private Task InvokeFilterPipelineAsync()
    {
        var next = State.InvokeBegin;
        
        // The `scope` tells the `Next` method who the caller is, and what kind of state to 
        // initialize to communicate a result. 
        // The outermost scope is `Scope.Invoker` and doesn't require any type of context 
        // or result other than throwing.
        var scope = Scope.Invoker;
        
        // The `state` is used for internal state handling during transitions between states. 
        // In practice this means storing a filter instance in `state` and then retrieving 
        // it in the next state.
        var state = (object?)null;
        
        // `isCompleted` will be set to true when we've reached a terminal state.
        var isCompleted = false;
        try
        {
            // 阻塞，执行 filter
            while (!isCompleted)
            {
                var lastTask = Next(ref next, ref scope, ref state, ref isCompleted);
                if (!lastTask.IsCompletedSuccessfully)
                {
                    // 异步等待
                    return Awaited(this, lastTask, next, scope, state, isCompleted);
                }
            }
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            // Wrap non task-wrapped exceptions in a Task,
            // as this isn't done automatically since the method is not async.
            return Task.FromException(ex);
        }
                
        // 异步等待 next
        static async Task Awaited(
            ResourceInvoker invoker, 
            Task lastTask, 
            State next, 
            Scope scope, 
            object? state, 
            bool isCompleted)
        {
            await lastTask;
            
            while (!isCompleted)
            {
                await invoker.Next(ref next, ref scope, ref state, ref isCompleted);
            }
        }
    }
}

```

##### 2.3.3  next

* 状态机

* xxxFilter -> xxxFitlerNext -> async -> short circuit / end

  ​													-> sync

```c#
internal abstract class ResourceInvoker
{
    private Task Next(
        ref State next, 
        ref Scope scope, 
        ref object? state, 
        ref bool isCompleted)
    {
        switch (next)
        {
            case State.InvokeBegin:
                {
                    goto case State.AuthorizationBegin;
                }
                
            // ...                                                                                                                                                    
            case State.InvokeEnd:
                {
                    isCompleted = true;
                    return Task.CompletedTask;
                }
                
            default:
                throw new InvalidOperationException();
        }
    }                
}
```

###### 2.3.3.1 authorization filter

```c#
internal abstract class ResourceInvoker
{
    private Task Next(
        ref State next, 
        ref Scope scope, 
        ref object? state, 
        ref bool isCompleted)
    {
        switch (next)
        {
            case State.AuthorizationBegin:
                 {
                     // filter cursor 复位，即从头再次遍历
                     _cursor.Reset();
                     goto case State.AuthorizationNext;
                 }

            case State.AuthorizationNext:
                 {
                     // 解析 filter cursor item
                     var current = _cursor.GetNextFilter<
                         				   IAuthorizationFilter, 
                    					   IAsyncAuthorizationFilter>();
                     
                     /* 如果是 asnyc authorization filter，
                        创建 context，转到 authorization async */
                     if (current.FilterAsync != null)
                     {
                         if (_authorizationContext == null)
                         {
                             _authorizationContext = 
                                 new AuthorizationFilterContextSealed(
                                 		_actionContext, 
                                		_filters);
                         }
                         // 封装当前解析到的 async authorization filter 
                         state = current.FilterAsync;
                         goto case State.AuthorizationAsyncBegin;
                     }
                     /* 如果是 (sync) authorization filter，
                        创建 context，转到 authorization sync */
                     else if (current.Filter != null)
                     {
                         if (_authorizationContext == null)
                         {
                             _authorizationContext = 
                                 new AuthorizationFilterContextSealed(
                         	         	_actionContext, 
                            	     	_filters);
                         }
                         // 封装当前解析到的 (sync) authorization filter
                         state = current.Filter;
                         goto case State.AuthorizationSync;
                     }
                     /* 如果不是 authorization filter，转到 authorization end
                        authorization end 进而 转到 resource filter 执行流 */
                     else
                     {
                         goto case State.AuthorizationEnd;
                     }
                 }
                
            /* authorization async 处理 */            
            case State.AuthorizationAsyncBegin:
                 {
                     Debug.Assert(state != null);
                     Debug.Assert(_authorizationContext != null);
                     
                     var filter = (IAsyncAuthorizationFilter)state;
                     var authorizationContext = _authorizationContext;
                     
                     /* 诊断和日志 */
                     _diagnosticListener.BeforeOnAuthorizationAsync(
                         					authorizationContext, 
                         					filter);                    
                     _logger.BeforeExecutingMethodOnFilter(
                         		FilterTypeConstants.AuthorizationFilter,
                         		nameof(IAsyncAuthorizationFilter.OnAuthorizationAsync),
                         		filter);
                     
                     // 执行 async filter 的 on authorization async 方法
                     var task = filter.OnAuthorizationAsync(authorizationContext);
                     
                     /* 转到 end */                     
                     if (!task.IsCompletedSuccessfully)
                     {
                         next = State.AuthorizationAsyncEnd;
                         return task;
                     }                     
                     goto case State.AuthorizationAsyncEnd;
                 }                
            case State.AuthorizationAsyncEnd:
                 {
                     Debug.Assert(state != null);
                     Debug.Assert(_authorizationContext != null);
                     
                     var filter = (IAsyncAuthorizationFilter)state;
                     var authorizationContext = _authorizationContext;
                     
                     /* 诊断和日志 */
                     _diagnosticListener.AfterOnAuthorizationAsync(
                         					authorizationContext, 
                         					filter);
                     _logger.AfterExecutingMethodOnFilter(
                         		FilterTypeConstants.AuthorizationFilter,
                         		nameof(IAsyncAuthorizationFilter.OnAuthorizationAsync),
                         		filter);
                     
                     // 如果获得 result，转向 auth short circuit
                     if (authorizationContext.Result != null)
                     {
                         goto case State.AuthorizationShortCircuit;
                     }
                     
                     // 转到 authorization next，开始执行下一个 filter
                     // ！都是从 authorization filter 开始执行 ！
                     goto case State.AuthorizationNext;
                 }
                
            /* authorization sync 处理 */                
            case State.AuthorizationSync:
                 {
                     Debug.Assert(state != null);
                     Debug.Assert(_authorizationContext != null);
                     
                     var filter = (IAuthorizationFilter)state;
                     var authorizationContext = _authorizationContext;
                     
                     /* 诊断和日志 */
                     _diagnosticListener.BeforeOnAuthorization(
                         					authorizationContext, 
                         					filter);
                     _logger.BeforeExecutingMethodOnFilter(
                         		FilterTypeConstants.AuthorizationFilter,
                         		nameof(IAuthorizationFilter.OnAuthorization),
                         		filter);
                     
                     // 执行 (sync) filter 的 on authorization 方法
                     filter.OnAuthorization(authorizationContext);
                     
                     /* 诊断和日志 */
                     _diagnosticListener.AfterOnAuthorization(
                         					authorizationContext, 
                         					filter);
                     _logger.AfterExecutingMethodOnFilter(
                         		FilterTypeConstants.AuthorizationFilter,
                         		nameof(IAuthorizationFilter.OnAuthorization),
                         		filter);
                     
                     // 如果获得 result，转向 autho short circuit
                     if (authorizationContext.Result != null)
                     {
                         goto case State.AuthorizationShortCircuit;
                     }
                     
                     // 转到 authorization next，开始执行下一个 filter
                     // ！都是从 authorization filter 开始执行 ！
                     goto case State.AuthorizationNext;
                 }
                          
            /* authorization short circuit 处理
               即中断 authorization filters 执行流 */    
            case State.AuthorizationShortCircuit:
                 {
                     Debug.Assert(state != null);
                     Debug.Assert(_authorizationContext != null);
                     Debug.Assert(_authorizationContext.Result != null);
                     
                     _logger.AuthorizationFailure((IFilterMetadata)state);
                     
                     // This is a short-circuit - execute relevant result filters + 
                     // result and complete this invocation.
                     isCompleted = true;
                     _result = _authorizationContext.Result;
                     
                     
                     return InvokeAlwaysRunResultFilters();
                 }
                                
            case State.AuthorizationEnd:
                 {
                     goto case State.ResourceBegin;
                 }
            
            // ...
        }
    }
}

```

###### 2.3.3.2 resource filter

```c#
internal abstract class ResourceInvoker
{
    private Task Next(
        ref State next, 
        ref Scope scope, 
        ref object? state, 
        ref bool isCompleted)
    {
        switch (next)
        {
        	case State.ResourceBegin:
                 {
                     // 复位 filter cursor，即从头再次遍历 filters
                     _cursor.Reset();
                     goto case State.ResourceNext;
                 }     
                
            case State.ResourceNext:
                 {
                     // 解析 filter cursor item
                     var current = _cursor.GetNextFilter<
                         					   IResourceFilter, 
                     						   IAsyncResourceFilter>();
                     
                     /* 如果是 async resource filter，
                        创建 context，转到 resource async 处理 */
                     if (current.FilterAsync != null)
                     {
                         if (_resourceExecutingContext == null)
                         {
                             _resourceExecutingContext = 
                                 new ResourceExecutingContextSealed(
                                 		_actionContext,
                                 		_filters,
                                 		_valueProviderFactories);
                         }
                         // 封装当前解析到的 async resource filter
                         state = current.FilterAsync;
                         goto case State.ResourceAsyncBegin;
                     }
                     /* 如果是 (sync) resource filter，
                        创建 context，转到 resource sync 处理 */
                     else if (current.Filter != null)
                     {
                         if (_resourceExecutingContext == null)
                         {
                             _resourceExecutingContext = 
                                 new ResourceExecutingContextSealed(
                                 		_actionContext,
                                 		_filters,
                                 		_valueProviderFactories);
                         }
                         // 封装当前解析到的 (sync) resource filter 
                         state = current.Filter;
                         goto case State.ResourceSyncBegin;
                     }
                     /* 如果不是 resource filter，转到 resource inside，
                        resource inside 进而转到 exception filter 执行流 */
                     else
                     {
                         // All resource filters are currently on the stack - 
                         // now execute the 'inside'.
                         goto case State.ResourceInside;
                     }
                 }
                
            /* resource async 处理 */
            case State.ResourceAsyncBegin:
                 {
                     Debug.Assert(state != null);
                     Debug.Assert(_resourceExecutingContext != null);
                     
                     var filter = (IAsyncResourceFilter)state;
                     var resourceExecutingContext = _resourceExecutingContext;
                     
                     /* 诊断和日志 */
                     _diagnosticListener.BeforeOnResourceExecution(
                         					resourceExecutingContext, 
                         					filter);
                     _logger.BeforeExecutingMethodOnFilter(
                         		FilterTypeConstants.ResourceFilter,
                         		nameof(IAsyncResourceFilter.OnResourceExecutionAsync),
                         		filter);
                     
                     // 执行 async resource filter 的 on resource excution async 方法
                     var task = filter.OnResourceExecutionAsync(
                         				   resourceExecutingContext, 
                         					//
				                           InvokeNextResourceFilterAwaitedAsync);
                     
                     /* 转到 resource async end */
                     if (!task.IsCompletedSuccessfully)
                     {
                         next = State.ResourceAsyncEnd;
                         return task;
                     }                     
                     goto case State.ResourceAsyncEnd;
                 }                
            case State.ResourceAsyncEnd:
                 {
                     Debug.Assert(state != null);
                     Debug.Assert(_resourceExecutingContext != null);
                     
                     var filter = (IAsyncResourceFilter)state;
                     
                     /* 如果没有 resource excuted context，*/
                     if (_resourceExecutedContext == null)
                     {
                         // If we get here then the filter didn't call 'next' indicating 
                         // a short circuit.
                         _resourceExecutedContext = 
                             new ResourceExecutedContextSealed(
                             		_resourceExecutingContext, 
		                            _filters)
                         {
                             Canceled = true,
                             Result = _resourceExecutingContext.Result,
                         };
                         
                         _diagnosticListener.AfterOnResourceExecution(
                             					_resourceExecutedContext, 
					                            filter);
                         _logger.AfterExecutingMethodOnFilter(
                    		         FilterTypeConstants.ResourceFilter,
                            		 nameof(IAsyncResourceFilter.OnResourceExecutionAsync),
		                             filter);
                         
                         /* 如果获得了 result，转到 resource short circuit 处理 */
                         // A filter could complete a Task without setting a result
                         if (_resourceExecutingContext.Result != null)
                         {
                             goto case State.ResourceShortCircuit;
                         }
                     }
                                          
                     goto case State.ResourceEnd;
                 }
            
            /* resource sync 处理 */
            case State.ResourceSyncBegin:
                 {
                     Debug.Assert(state != null);
                     Debug.Assert(_resourceExecutingContext != null);
                     
                     var filter = (IResourceFilter)state;
                     var resourceExecutingContext = _resourceExecutingContext;
                     
                     /* 诊断和日志 */
                     _diagnosticListener.BeforeOnResourceExecuting(
                         					resourceExecutingContext, 
                         					filter);
                     _logger.BeforeExecutingMethodOnFilter(
                                 FilterTypeConstants.ResourceFilter,
                         		nameof(IResourceFilter.OnResourceExecuting),
                         		filter);
                     
                     // 执行 (sync) resource filter 的 on resource executing 方法
                     filter.OnResourceExecuting(resourceExecutingContext);
                     
                     /* 诊断和日志 */
                     _diagnosticListener.AfterOnResourceExecuting(
                         					resourceExecutingContext, 
                         					filter);
                     _logger.AfterExecutingMethodOnFilter(
                         		FilterTypeConstants.ResourceFilter,
                         		nameof(IResourceFilter.OnResourceExecuting),
                         		filter);
                     
                     // 如果获得 result，转到 resource short circuit 处理
                     if (resourceExecutingContext.Result != null)
                     {
                         _resourceExecutedContext = 
                             new ResourceExecutedContextSealed(
                             	resourceExecutingContext, 
                             	_filters)
                         {
                             Canceled = true,
                             Result = _resourceExecutingContext.Result,
                         };
                         
                         goto case State.ResourceShortCircuit;
                     }
                     
                     // 
                     var task = InvokeNextResourceFilter();
                     
                     /* 转到 resource sync end */
                     if (!task.IsCompletedSuccessfully)
                     {
                         next = State.ResourceSyncEnd;
                         return task;
                     }                     
                     goto case State.ResourceSyncEnd;
                 }                
            case State.ResourceSyncEnd:
                 {
                     Debug.Assert(state != null);
                     Debug.Assert(_resourceExecutingContext != null);
                     Debug.Assert(_resourceExecutedContext != null);
                     
                     var filter = (IResourceFilter)state;
                     var resourceExecutedContext = _resourceExecutedContext;
                     
                     /* 诊断和日志 */
                     _diagnosticListener.BeforeOnResourceExecuted(
                         					resourceExecutedContext, filter);
                     _logger.BeforeExecutingMethodOnFilter(
                         					FilterTypeConstants.ResourceFilter,
                         					nameof(IResourceFilter.OnResourceExecuted),
                         					filter);
                     
                     // 执行 resource filter 的 on resource executed 方法
                     filter.OnResourceExecuted(resourceExecutedContext);
                     
                     /* 诊断和日志 */
                     _diagnosticListener.AfterOnResourceExecuted(
                         					resourceExecutedContext, filter);
                     _logger.AfterExecutingMethodOnFilter(
                         					FilterTypeConstants.ResourceFilter,
                         					nameof(IResourceFilter.OnResourceExecuted),
                         					filter);
                     
                     /* 转到 resource end */
                     goto case State.ResourceEnd;
                 }
            
            /* resource short circuit，
               即中断 resource filter 执行流 */
            case State.ResourceShortCircuit:
                 {
                     Debug.Assert(state != null);
                     Debug.Assert(_resourceExecutingContext != null);
                     Debug.Assert(_resourceExecutedContext != null);
                     
                     /* 诊断和日志 */
                     _logger.ResourceFilterShortCircuited((IFilterMetadata)state);
                     
                     _result = _resourceExecutingContext.Result;
                     
                     var task = InvokeAlwaysRunResultFilters();
                     
                     /* 转到 resource end*/
                     if (!task.IsCompletedSuccessfully)
                     {
                         next = State.ResourceEnd;
                         return task;
                     }                     
                     goto case State.ResourceEnd;
                 }
            
            case State.ResourceInside:
                 {
                     goto case State.ExceptionBegin;
                 }                                                                
        }
    }
}

```

###### 2.3.3.3 next resource filter

```c#
internal abstract class ResourceInvoker
{
    private Task<ResourceExecutedContext> InvokeNextResourceFilterAwaitedAsync()
    {
        Debug.Assert(_resourceExecutingContext != null);
        
        if (_resourceExecutingContext.Result != null)
        {
            // If we get here, it means that an async filter set a result AND called next(). 
            // This is forbidden.
            return Throw();
        }
        
        var task = InvokeNextResourceFilter();
        
        if (!task.IsCompletedSuccessfully)
        {
            return Awaited(this, task);
        }
        
        Debug.Assert(_resourceExecutedContext != null);        
        return Task.FromResult<ResourceExecutedContext>(_resourceExecutedContext);
        
        static async Task<ResourceExecutedContext> Awaited(
            										   ResourceInvoker invoker, 
            										   Task task)
        {
            await task;
            
            Debug.Assert(invoker._resourceExecutedContext != null);
            return invoker._resourceExecutedContext;
        }
        
#pragma warning disable CS1998
        static async Task<ResourceExecutedContext> Throw()
		{
    		var message = Resources.FormatAsyncResourceFilter_InvalidShortCircuit(
                						typeof(IAsyncResourceFilter).Name,
						                nameof(ResourceExecutingContext.Result),
						                typeof(ResourceExecutingContext).Name,
						                typeof(ResourceExecutionDelegate).Name);
    
    		throw new InvalidOperationException(message);            
		}
#pragma warning restore CS1998
    }
    
    private Task InvokeNextResourceFilter()
    {
        try
        {
            var scope = Scope.Resource;
            var next = State.ResourceNext;
            var state = (object?)null;
            var isCompleted = false;
            
            while (!isCompleted)
            {
                var lastTask = Next(ref next, ref scope, ref state, ref isCompleted);
                if (!lastTask.IsCompletedSuccessfully)
                {
                    return Awaited(this, lastTask, next, scope, state, isCompleted);
                }
            }
        }
        catch (Exception exception)
        {
            _resourceExecutedContext = 
                new ResourceExecutedContextSealed(
                _resourceExecutingContext!, 
                _filters)
            {
                ExceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception),
            };
        }
        
        Debug.Assert(_resourceExecutedContext != null);
        return Task.CompletedTask;
        
        static async Task Awaited(
            ResourceInvoker invoker, 
            Task lastTask, 
            State next, 
            Scope scope, 
            object? state, 
            bool isCompleted)
        {
            try
            {
                await lastTask;
                
                while (!isCompleted)
                {
                    await invoker.Next(ref next, ref scope, ref state, ref isCompleted);
                }
            }
            catch (Exception exception)
            {
                invoker._resourceExecutedContext = 
                    new ResourceExecutedContextSealed(
                    		invoker._resourceExecutingContext!, 
		                    invoker._filters)
                {
                    ExceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception),
                };
            }
            
            Debug.Assert(invoker._resourceExecutedContext != null);
        }
    }        
}

```

###### 2.3.3.4 rethrow resource executed context

```c#
internal abstract class ResourceInvoker
{
    private static void Rethrow(ResourceExecutedContextSealed context)
    {
        if (context == null)
        {
            return;
        }
        
        if (context.ExceptionHandled)
        {
            return;
        }
        
        if (context.ExceptionDispatchInfo != null)
        {
            context.ExceptionDispatchInfo.Throw();
        }
        
        if (context.Exception != null)
        {
            throw context.Exception;
        }
    }
}

```

###### 2.3.3.5 exception filter

```c#
internal abstract class ResourceInvoker
{
    private Task Next(
        ref State next, 
        ref Scope scope, 
        ref object? state, 
        ref bool isCompleted)
    {
        switch (next)
        {
            case State.ExceptionBegin:
                 {
                     // 复位 filter cursor
                     _cursor.Reset();
                     goto case State.ExceptionNext;
                 }
                
            case State.ExceptionNext:
                 {
                     // 解析 filter cursor item
                     var current = _cursor.GetNextFilter<
                         					   IExceptionFilter, 
                     						   IAsyncExceptionFilter>();
                     
                     /* 如果是 async exception filter，
                        转到 exception async 处理 */
                     if (current.FilterAsync != null)
                     {
                         // 封装当前解析到的 async exception filter
                         state = current.FilterAsync;
                         goto case State.ExceptionAsyncBegin;
                     }
                     /* 如果是 (sync) exception filter，
                        转到 exception (sync) 处理 */
                     else if (current.Filter != null)
                     {
                         // 封装当前解析到的 (sync) exception filter
                         state = current.Filter;
                         goto case State.ExceptionSyncBegin;
                     }
                     /* 如果不是 exception filter，且 scope 是 exception
                        转到 exception inside 处理 */
                     else if (scope == Scope.Exception)
                     {
                         // All exception filters are on the stack already - 
                         // so execute the inside'.
                         goto case State.ExceptionInside;
                     }
                     /* 否则，即不是 exception filter，scope 是 invoker 或 resource，
                        转到 action begin 处理 */
                     else
                     {
                         // There are no exception filters - so jump right to the action.
                         Debug.Assert(scope == Scope.Invoker || 
                                      scope == Scope.Resource);
                         goto case State.ActionBegin;
                     }
                 }
                
            /* asyn exception 处理 */    
            case State.ExceptionAsyncBegin:
                 {
                     // 
                     var task = InvokeNextExceptionFilterAsync();
                     
                     /* 转到 exception async resume 处理 */
                     if (!task.IsCompletedSuccessfully)
                     {
                         next = State.ExceptionAsyncResume;
                         return task;
                     }                     
                     goto case State.ExceptionAsyncResume;
                 }                
            case State.ExceptionAsyncResume:
                 {
                     Debug.Assert(state != null);
                     
                     var filter = (IAsyncExceptionFilter)state;
                     var exceptionContext = _exceptionContext;
                     
                     /* 如果有未处理的异常，*/
                     // When we get here we're 'unwinding' the stack of exception filters. 
                     // If we have an unhandled exception, we'll call the filter. 
                     // Otherwise there's nothing to do.
                     if (exceptionContext?.Exception != null && 
                         !exceptionContext.ExceptionHandled)
                     {
                         /* 诊断和日志 */
                         _diagnosticListener.BeforeOnExceptionAsync(
                             					exceptionContext, 
                             					filter);
                         _logger.BeforeExecutingMethodOnFilter(
                             		FilterTypeConstants.ExceptionFilter,
                             		nameof(IAsyncExceptionFilter.OnExceptionAsync),
                             		filter);
                         
                         // 执行 async exception filter 的 on exception async 方法
                         var task = filter.OnExceptionAsync(exceptionContext);
                         
                         /* 转到 exception async end 处理 */
                         if (!task.IsCompletedSuccessfully)
                         {
                             next = State.ExceptionAsyncEnd;
                             return task;
                         }                         
                         goto case State.ExceptionAsyncEnd;
                     }
                     
                     /* （没有未处理异常），转到 exception end 处理 */
                     goto case State.ExceptionEnd;
                 }                
            case State.ExceptionAsyncEnd:
                 {
                     Debug.Assert(state != null);
                     Debug.Assert(_exceptionContext != null);
                     
                     var filter = (IAsyncExceptionFilter)state;
                     var exceptionContext = _exceptionContext;
                     
                     /* 诊断和日志 */
                     _diagnosticListener.AfterOnExceptionAsync(
                         					exceptionContext, 
                         					filter);
                     _logger.AfterExecutingMethodOnFilter(
                         		FilterTypeConstants.ExceptionFilter,
                        		nameof(IAsyncExceptionFilter.OnExceptionAsync),
                         		filter);
                     
                     // 如果没有未处理异常，记录日志
                     if (exceptionContext.Exception == null || 
                         exceptionContext.ExceptionHandled)
                     {
                         // We don't need to do anything to trigger a short circuit. 
                         // If there's another exception filter on the stack it will check 
                         // the same set of conditions and then just skip itself.
                         _logger.ExceptionFilterShortCircuited(filter);
                     }
                     
                     // （由未处理异常），转到 exception end 处理
                     goto case State.ExceptionEnd;
                 }
            
            /* exception sync 处理 */
            case State.ExceptionSyncBegin:
                 {
                     //
                     var task = InvokeNextExceptionFilterAsync();
                     
                     /* 转到 exception sync end */
                     if (!task.IsCompletedSuccessfully)
                     {
                         next = State.ExceptionSyncEnd;
                         return task;
                     }                     
                     goto case State.ExceptionSyncEnd;
                 }                
            case State.ExceptionSyncEnd:
                 {
                     Debug.Assert(state != null);
                     
                     var filter = (IExceptionFilter)state;
                     var exceptionContext = _exceptionContext;
                     
                     /* 如果有未处理异常 */
                     // When we get here we're 'unwinding' the stack of exception filters. 
                     // If we have an unhandled exception, we'll call the filter. 
                     // Otherwise there's nothing to do.
                     if (exceptionContext?.Exception != null && 
                         !exceptionContext.ExceptionHandled)
                     {
                         /* 诊断和日志 */
                         _diagnosticListener.BeforeOnException(
                             					exceptionContext, 
                             					filter);
                         _logger.BeforeExecutingMethodOnFilter(
		                             FilterTypeConstants.ExceptionFilter,
        		                     nameof(IExceptionFilter.OnException),
                		             filter);
                         
                         // 执行 exception filter 的 on exception 方法
                         filter.OnException(exceptionContext);
                         
                         /* 诊断和日志 */
                         _diagnosticListener.AfterOnException(
                             					 exceptionContext, 
                             					 filter);
                         _logger.AfterExecutingMethodOnFilter(
		                             FilterTypeConstants.ExceptionFilter,
        		                     nameof(IExceptionFilter.OnException),
                		             filter);
                         
                         // 如果没有未处理异常，转到 exception short circuit 处理
                         if (exceptionContext.Exception == null || 
                             exceptionContext.ExceptionHandled)
                         {
                             // We don't need to do anything to trigger a short circuit. 
                             // If here's another exception filter on the stack it will 
                             // check the same set of conditions and then just skip itself.
                             _logger.ExceptionFilterShortCircuited(filter);
                         }
                     }
                     
                     /* 如果没有未处理异常，转到 exception end */
                     goto case State.ExceptionEnd;
                 }
                                
            case State.ExceptionInside:
                 {
                     goto case State.ActionBegin;
                 }
                
            case State.ExceptionHandled:
                 {
                     // We arrive in this state when an exception happened, but was handled 
                     // by exception filters either by setting ExceptionHandled, or nulling 
                     // out the Exception or setting a result on the ExceptionContext.
                     //
                     // We need to execute the result (if any) and then exit gracefully which
                     // unwinding Resource filters.
                     
                     Debug.Assert(state != null);
                     Debug.Assert(_exceptionContext != null);
                     
                     // result，没有则创建 empty result
                     if (_exceptionContext.Result == null)
                     {
                         _exceptionContext.Result = new EmptyResult();
                     }
                     
                     _result = _exceptionContext.Result;
                     
                     //
                     var task = InvokeAlwaysRunResultFilters();
                     
                     /* 转到 resource inside end */
                     if (!task.IsCompletedSuccessfully)
                     {
                         next = State.ResourceInsideEnd;
                         return task;
                     }                     
                     goto case State.ResourceInsideEnd;
                 }
                
            case State.ExceptionEnd:
                 {
                     var exceptionContext = _exceptionContext;
                     
                     // 如果 scope 是 exception，返回
                     if (scope == Scope.Exception)
                     {
                         isCompleted = true;
                         return Task.CompletedTask;
                     }
                     
                     // 如果 exception context 不为 null，
                     if (exceptionContext != null)
                     {
                         // 没有未处理异常，且获取了 result，
                         // 转到 exception handled 处理
                         if (exceptionContext.Result != null ||
                             exceptionContext.Exception == null ||
                             exceptionContext.ExceptionHandled)
                         {                             
                             goto case State.ExceptionHandled;
                         }
                         
                         // 否则，重新抛出异常
                         Rethrow(exceptionContext);
                         Debug.Fail("unreachable");
                     }
                     
                     // 
                     var task = InvokeResultFilters();
                     
                     /* 转到 resource inside end */
                     if (!task.IsCompletedSuccessfully)
                     {
                         next = State.ResourceInsideEnd;
                         return task;
                     }                     
                     goto case State.ResourceInsideEnd;
                 } 
                
        }
    }
}

```

###### 2.3.3.6 next exception filter

```c#
internal abstract class ResourceInvoker
{
    private Task InvokeNextExceptionFilterAsync()
    {
        try
        {
            var next = State.ExceptionNext;
            var state = (object?)null;
            var scope = Scope.Exception;
            var isCompleted = false;
            
            while (!isCompleted)
            {
                var lastTask = Next(ref next, ref scope, ref state, ref isCompleted);
                if (!lastTask.IsCompletedSuccessfully)
                {
                    return Awaited(this, lastTask, next, scope, state, isCompleted);
                }
            }
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            // Wrap non task-wrapped exceptions in a Task,
            // as this isn't done automatically since the method is not async.
            return Task.FromException(ex);
        }
        
        static async Task Awaited(
            ResourceInvoker invoker, 
            Task lastTask, 
            State next, 
            Scope scope, 
            object? state, 
            bool isCompleted)
        {
            try
            {
                await lastTask;
                
                while (!isCompleted)
                {
                    await invoker.Next(ref next, ref scope, ref state, ref isCompleted);
                }
            }
            catch (Exception exception)
            {
                invoker._exceptionContext = 
                    new ExceptionContextSealed(invoker._actionContext, invoker._filters)
                {
                    ExceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception),
                };
            }
        }
    }
}
```

###### 2.3.3.7 rethrow exception context

```c#
internal abstract class ResourceInvoker
{
    private static void Rethrow(ExceptionContextSealed context)
    {
        if (context == null)
        {
            return;
        }
        
        if (context.ExceptionHandled)
        {
            return;
        }
        
        if (context.ExceptionDispatchInfo != null)
        {
            context.ExceptionDispatchInfo.Throw();
        }
        
        if (context.Exception != null)
        {
            throw context.Exception;
        }
    }
}

```

###### 2.3.3.8 action filter

```c#
internal abstract class ResourceInvoker
{
    private Task Next(
        ref State next, 
        ref Scope scope, 
        ref object? state, 
        ref bool isCompleted)
    {
        switch (next)
        {                
            case State.ActionBegin:
                 {
                     //
                     var task = InvokeInnerFilterAsync();
                     
                     /* 转到 action end */
                     if (!task.IsCompletedSuccessfully)
                     {
                         next = State.ActionEnd;
                         return task;
                     }                     
                     goto case State.ActionEnd;
                 }
                
            case State.ActionEnd:
                 {
                     // 如果 scope 是 exception，返回
                     if (scope == Scope.Exception)
                     {
                         // If we're inside an exception filter, let's allow those filters 
                         // to 'unwind' before the result.
                         isCompleted = true;
                         return Task.CompletedTask;
                     }
                     
                     Debug.Assert(scope == Scope.Invoker || 
                                  scope == Scope.Resource);
                     
                     // 
                     var task = InvokeResultFilters();
                     
                     /* 转到 resource inside end */
                     if (!task.IsCompletedSuccessfully)
                     {
                         next = State.ResourceInsideEnd;
                         return task;
                     }                     
                     goto case State.ResourceInsideEnd;
                 }
                
            case State.ResourceInsideEnd:
                 {
                     // 如果 scope 是 resource，转到 resource end
                     if (scope == Scope.Resource)
                     {
                         _resourceExecutedContext = 
                             new ResourceExecutedContextSealed(
                             		_actionContext, 
                             		_filters)
                         {
                             Result = _result,
                         };
                         
                         goto case State.ResourceEnd;
                     }
                     
                     // （否则），转到 invoke end
                     goto case State.InvokeEnd;
                 }
                
            case State.ResourceEnd:
                 {
                     // scope 是 resource，返回
                     if (scope == Scope.Resource)
                     {
                         isCompleted = true;
                         return Task.CompletedTask;
                     }
                     
                     Debug.Assert(scope == Scope.Invoker);
                     Rethrow(_resourceExecutedContext!);
                     
                     // （否则），转到 invoke end
                     goto case State.InvokeEnd;
                 }
        }
    }
}

```

###### 2.3.3.9 invoke inner filter

```c#
internal abstract class ResourceInvoker
{
    protected abstract Task InvokeInnerFilterAsync();
}

```

##### 2.3.4 result filters

###### 2.3.4.1 invoke result filters

```c#
internal abstract class ResourceInvoker
{
    private Task InvokeResultFilters()
    {
        try
        {
            var next = State.ResultBegin;
            var scope = Scope.Invoker;
            var state = (object?)null;
            var isCompleted = false;
            
            while (!isCompleted)
            {
                var lastTask = 
                    ResultNext<IResultFilter, IAsyncResultFilter>(
                    	ref next, 
                    	ref scope, 
                    	ref state, 
                    	ref isCompleted);
                
                if (!lastTask.IsCompletedSuccessfully)
                {
                    return Awaited(this, lastTask, next, scope, state, isCompleted);
                }
            }
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            // Wrap non task-wrapped exceptions in a Task,
            // as this isn't done automatically since the method is not async.
            return Task.FromException(ex);
        }
        
        static async Task Awaited(
            ResourceInvoker invoker, 
            Task lastTask, 
            State next, 
            Scope scope, 
            object? state, 
            bool isCompleted)
        {
            await lastTask;
            
            while (!isCompleted)
            {
                await invoker.ResultNext<IResultFilter, IAsyncResultFilter>(
                    	  ref next, 
                    	  ref scope, 
                     	  ref state, 
                       	  ref isCompleted);
            }
        }
    }
}

```

###### 2.3.4.2 invoke always run result filters

```c#
internal abstract class ResourceInvoker
{
    private Task InvokeAlwaysRunResultFilters()
    {
        try
        {
            var next = State.ResultBegin;
            var scope = Scope.Invoker;
            var state = (object?)null;
            var isCompleted = false;
            
            while (!isCompleted)
            {
                var lastTask = 
                    ResultNext<IAlwaysRunResultFilter, IAsyncAlwaysRunResultFilter>(
                    	ref next, 
                    	ref scope, 
                    	ref state, 
                    	ref isCompleted);
                
                if (!lastTask.IsCompletedSuccessfully)
                {
                    return Awaited(this, lastTask, next, scope, state, isCompleted);
                }
            }
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            // Wrap non task-wrapped exceptions in a Task,
            // as this isn't done automatically since the method is not async.
            return Task.FromException(ex);
        }
        
        static async Task Awaited(
            ResourceInvoker invoker, 
            Task lastTask, 
            State next, 
            Scope scope, 
            object? state, 
            bool isCompleted)
        {
            await lastTask;
            
            while (!isCompleted)
            {
                await invoker.ResultNext<
                    			  IAlwaysRunResultFilter, 
                				  IAsyncAlwaysRunResultFilter>(
                                      ref next, 
                                      ref scope, 
                                      ref state, 
                                      ref isCompleted);
            }
        }
    }
}
    
```

##### 2.3.5 result next

```c#
internal abstract class ResourceInvoker
{
    private Task ResultNext<TFilter, TFilterAsync>(
        ref State next, 
        ref Scope scope, 
        ref object? state, 
        ref bool isCompleted)        
        	where TFilter : class, IResultFilter            
            where TFilterAsync : class, IAsyncResultFilter
    {
        var resultFilterKind = 
            typeof(TFilter) == typeof(IAlwaysRunResultFilter) 
            	? FilterTypeConstants.AlwaysRunResultFilter 
            	: FilterTypeConstants.ResultFilter;
        
        switch (next)
        {
            case State.ResultBegin:
                 {
                     _cursor.Reset();
                     goto case State.ResultNext;
                 }
                
            case State.ResultNext:
                 {
                     // 解析 filter cusor item
                     var current = _cursor.GetNextFilter<TFilter, TFilterAsync>();
                     
                     /* 如果是 async result filter，
                        创建 context，转到 result async 处理 */
                     if (current.FilterAsync != null)
                     {
                         if (_resultExecutingContext == null)
                         {
                             _resultExecutingContext = 
                                 new ResultExecutingContextSealed(
                                 		_actionContext, 
                                 		_filters, 
                                 		_result!, 
                                 		_instance!);
                         }
                         // 封装当前解析到的 async result filter
                         state = current.FilterAsync;
                         goto case State.ResultAsyncBegin;
                     }
                     /* 如果是 (sync) result filter，
                        创建 context，转到 result sync 处理 */
                     else if (current.Filter != null)
                     {
                         if (_resultExecutingContext == null)
                         {
                             _resultExecutingContext = 
                                 new ResultExecutingContextSealed(
                                 		_actionContext, 
                                 		_filters, 
                                 		_result!, 
                                 		_instance!);
                         }
                         // 封装当前解析到的 result filter
                         state = current.Filter;
                         goto case State.ResultSyncBegin;
                     }
                     /* 不是 result filter，转到 result inside  */
                     else
                     {
                         goto case State.ResultInside;
                     }
                 }
                
            /* result async */
            case State.ResultAsyncBegin:
                 {
                     Debug.Assert(state != null);
                     Debug.Assert(_resultExecutingContext != null);
                     
                     var filter = (TFilterAsync)state;
                     var resultExecutingContext = _resultExecutingContext;
                     
                     /* 诊断和日志 */
                     _diagnosticListener.BeforeOnResultExecution(
                         					resultExecutingContext, 
                         					filter);
                     _logger.BeforeExecutingMethodOnFilter(
                         		resultFilterKind,
                         		nameof(IAsyncResultFilter.OnResultExecutionAsync),
                         		filter);
                     
                     // 执行 async result filter 的 on result execution async 方法
                     var task = filter.OnResultExecutionAsync(
                         resultExecutingContext, 
                         InvokeNextResultFilterAwaitedAsync<TFilter, TFilterAsync>);
                     
                     /* 转到 result async end */
                     if (!task.IsCompletedSuccessfully)
                     {
                         next = State.ResultAsyncEnd;
                         return task;
                     }                     
                     goto case State.ResultAsyncEnd;
                 }                
            case State.ResultAsyncEnd:
                 {
                     Debug.Assert(state != null);
                     Debug.Assert(_resultExecutingContext != null);
                     
                     var filter = (TFilterAsync)state;
                     var resultExecutingContext = _resultExecutingContext;
                     var resultExecutedContext = _resultExecutedContext;
                     
                     /* 如果没有 executed context，或者 executing context 标记了 cancel，
                        转到 result end */
                     if (resultExecutedContext == null || 
                         resultExecutingContext.Cancel)
                     {
                         // Short-circuited by not calling next || Short-circuited 
                         // by setting Cancel == true
                         _logger.ResultFilterShortCircuited(filter);
                         
                         _resultExecutedContext = 
                             new ResultExecutedContextSealed(
                             		_actionContext,
		                            _filters,
        		                    resultExecutingContext.Result,
                		            _instance!)
                         {
                             Canceled = true,
                         };
                     }
                     
                     /* 诊断和日志 */
                     _diagnosticListener.AfterOnResultExecution(
                         					_resultExecutedContext, filter);
                     _logger.AfterExecutingMethodOnFilter(
                         		resultFilterKind,
                         		nameof(IAsyncResultFilter.OnResultExecutionAsync),
                         		filter);
                     
                     goto case State.ResultEnd;
                 }
                
            /* result sync */
            case State.ResultSyncBegin:
                 {
                     Debug.Assert(state != null);
                     Debug.Assert(_resultExecutingContext != null);
                     
                     var filter = (TFilter)state;
                     var resultExecutingContext = _resultExecutingContext;
                     
                     /* 诊断和日志 */
                     _diagnosticListener.BeforeOnResultExecuting(
                         resultExecutingContext, filter);
                     _logger.BeforeExecutingMethodOnFilter(
                         resultFilterKind,
                         nameof(IResultFilter.OnResultExecuting),
                         filter);
                     
                     // 执行 (sync) result filter 的 on result executing 方法
                     filter.OnResultExecuting(resultExecutingContext);
                     
                     /* 诊断和日志 */
                     _diagnosticListener.AfterOnResultExecuting(
                         resultExecutingContext, 
                         filter);
                     _logger.AfterExecutingMethodOnFilter(
                         resultFilterKind,
                         nameof(IResultFilter.OnResultExecuting),
                         filter);
                     
                     /* 如果 executing context 标记了 cancel，
                        转到 result end */
                     if (_resultExecutingContext.Cancel)
                     {
                         // Short-circuited by setting Cancel == true
                         _logger.ResultFilterShortCircuited(filter);
                         
                         _resultExecutedContext = 
                             new ResultExecutedContextSealed(
                             		resultExecutingContext,
		                            _filters,
        		                    resultExecutingContext.Result,
                		            _instance!)
                         {
                             Canceled = true,
                         };
                         
                         goto case State.ResultEnd;
                     }
                     
                     // 
                     var task = InvokeNextResultFilterAsync<TFilter, TFilterAsync>();
                     
                     /* 转到 result */
                     if (!task.IsCompletedSuccessfully)
                     {
                         next = State.ResultSyncEnd;
                         return task;
                     }                     
                     goto case State.ResultSyncEnd;
                 }                
            case State.ResultSyncEnd:
                 {
                     Debug.Assert(state != null);
                     Debug.Assert(_resultExecutingContext != null);
                     Debug.Assert(_resultExecutedContext != null);
                     
                     var filter = (TFilter)state;
                     var resultExecutedContext = _resultExecutedContext;
                     
                     /* 诊断和日志 */
                     _diagnosticListener.BeforeOnResultExecuted(
                         					resultExecutedContext, 
                         					filter);
                     _logger.BeforeExecutingMethodOnFilter(
                         		resultFilterKind,
                         		nameof(IResultFilter.OnResultExecuted),
                         		filter);
                     
                     // 执行 (sync) result filter 的 on result executed 方法
                     filter.OnResultExecuted(resultExecutedContext);
                     
                     /* 诊断和日志 */
                     _diagnosticListener.AfterOnResultExecuted(
                         					resultExecutedContext, 
                         					filter);
                     _logger.AfterExecutingMethodOnFilter(
                         		resultFilterKind,
                         		nameof(IResultFilter.OnResultExecuted),
                         		filter);
                     
                     goto case State.ResultEnd;
                 }
                
            /* result inside */
            case State.ResultInside:
                 {
                     /* 确保 result，没有则创建 empty result */
                     // If we executed result filters then we need to grab the result 
                     // from here.
                     if (_resultExecutingContext != null)
                     {
                         _result = _resultExecutingContext.Result;
                     }                     
                     if (_result == null)
                     {
                         // The empty result is always flowed back as the 'executed' result 
                         // if we don't have one.
                         _result = new EmptyResult();
                     }
                     
                     // 执行 invoke result async
                     var task = InvokeResultAsync(_result);
                     
                     /* 转到 result end */
                     if (!task.IsCompletedSuccessfully)
                     {
                         next = State.ResultEnd;
                         return task;
                     }                     
                     goto case State.ResultEnd;
                 }
                
            /* result end */
            case State.ResultEnd:
                 {
                     var result = _result;
                     isCompleted = true;
                     
                     // 如果 scope 是 result，返回
                     if (scope == Scope.Result)
                     {
                         if (_resultExecutedContext == null)
                         {
                             _resultExecutedContext = 
                                 new ResultExecutedContextSealed(
                                 		_actionContext, 
		                                _filters, 
        		                        result!, 
                		                _instance!);
                         }
                         
                         return Task.CompletedTask;
                     }
                     
                     // （否则），抛出异常
                     Rethrow(_resultExecutedContext!);
                     return Task.CompletedTask;
                 }
                
            default:
                throw new InvalidOperationException(); // Unreachable.
        }
    }
}
    
```

###### 2.3.5.1 next result filter

```c#
internal abstract class ResourceInvoker
{
    private Task<ResultExecutedContext> 
        InvokeNextResultFilterAwaitedAsync<TFilter, TFilterAsync>()            
        	where TFilter : class, IResultFilter            
            where TFilterAsync : class, IAsyncResultFilter
    {
        Debug.Assert(_resultExecutingContext != null);
        if (_resultExecutingContext.Cancel)
        {
            // If we get here, it means that an async filter set cancel == true AND 
            // called next().
            // This is forbidden.
            return Throw();
        }
        
                // 
        var task = InvokeNextResultFilterAsync<TFilter, TFilterAsync>();                
        if (!task.IsCompletedSuccessfully)
        {
            return Awaited(this, task);
        }
        
        Debug.Assert(_resultExecutedContext != null);
        return Task.FromResult<ResultExecutedContext>(_resultExecutedContext);
        
                
        static async Task<ResultExecutedContext> Awaited(
            										 ResourceInvoker invoker, 
            										 Task task)
        {
            await task;
            
            Debug.Assert(invoker._resultExecutedContext != null);
            return invoker._resultExecutedContext;
        }
                
#pragma warning disable CS1998
        static async Task<ResultExecutedContext> Throw()
        {
		    var message = Resources.FormatAsyncResultFilter_InvalidShortCircuit(
                						typeof(IAsyncResultFilter).Name,
						                nameof(ResultExecutingContext.Cancel),
						                typeof(ResultExecutingContext).Name,
						                typeof(ResultExecutionDelegate).Name);
    
    		throw new InvalidOperationException(message);
		}
#pragma warning restore CS1998
    }
    
    private Task InvokeNextResultFilterAsync<TFilter, TFilterAsync>()        
        where TFilter : class, IResultFilter            
        where TFilterAsync : class, IAsyncResultFilter
    {
        try
        {
            var next = State.ResultNext;
            var state = (object?)null;
            var scope = Scope.Result;
            var isCompleted = false;
            
            while (!isCompleted)
            {
                var lastTask = ResultNext<TFilter, TFilterAsync>(
                    			   ref next, 
                    			   ref scope, 
                    			   ref state, 
                    			   ref isCompleted);
                
                if (!lastTask.IsCompletedSuccessfully)
                {
                    return Awaited(this, lastTask, next, scope, state, isCompleted);
                }
            }
        }
        catch (Exception exception)
        {
            _resultExecutedContext = 
                new ResultExecutedContextSealed(
                		_actionContext, 
                		_filters, 
                		_result!, 
                		_instance!)
            {
                ExceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception),
            };
        }
        
        Debug.Assert(_resultExecutedContext != null);        
        return Task.CompletedTask;
        
            
        static async Task Awaited(
            ResourceInvoker invoker, 
            Task lastTask, 
            State next, 
            Scope scope, 
            object? state, 
            bool isCompleted)
        {
            try
            {
                await lastTask;
                
                while (!isCompleted)
                {
                    await invoker.ResultNext<TFilter, TFilterAsync>(
                        	  ref next, 
                        	  ref scope, 
                        	  ref state, 
                        	  ref isCompleted);
                }
            }
            catch (Exception exception)
            {
                invoker._resultExecutedContext = 
                    new ResultExecutedContextSealed(
                    		invoker._actionContext, 
                    		invoker._filters, 
                    		invoker._result!, 
                    		invoker._instance!)
                {
                    ExceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception),
                };
            }
            
            Debug.Assert(invoker._resultExecutedContext != null);
        }
    }        
}

```

###### 2.3.5.2 invoke result async

```c#
internal abstract class ResourceInvoker
{
    protected virtual Task InvokeResultAsync(IActionResult result)
    {
        if (_diagnosticListener.IsEnabled() || 
            _logger.IsEnabled(LogLevel.Trace))
        {
            return Logged(this, result);
        }
        
        return result.ExecuteResultAsync(_actionContext);
        
        static async Task Logged(ResourceInvoker invoker, IActionResult result)
        {
            var actionContext = invoker._actionContext;
            
            invoker._diagnosticListener.BeforeActionResult(actionContext, result);
            invoker._logger.BeforeExecutingActionResult(result);
            
            try
            {
                await result.ExecuteResultAsync(actionContext);
            }
            finally
            {
                invoker._diagnosticListener.AfterActionResult(actionContext, result);
                invoker._logger.AfterExecutingActionResult(result);
            }
        }
    }     
}

```

###### 2.3.5.3 rethrow result executed context

```c#
internal abstract class ResourceInvoker
{
    private static void Rethrow(ResultExecutedContextSealed context)
    {
        if (context == null)
        {
            return;
        }
        
        if (context.ExceptionHandled)
        {
            return;
        }
        
        if (context.ExceptionDispatchInfo != null)
        {
            context.ExceptionDispatchInfo.Throw();
        }
        
        if (context.Exception != null)
        {
            throw context.Exception;
        }
    }                       
}

```





#### 2.4 action invoker







###### 2.4.2.1 action context

```c#
public class ActionContext
{
    public ActionDescriptor ActionDescriptor { get; set; } = default!;        
    public HttpContext HttpContext { get; set; } = default!;        
    public ModelStateDictionary ModelState { get; } = default!;        
    public RouteData RouteData { get; set; } = default!;
    
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
        	: this(
                httpContext, 
                routeData, 
                actionDescriptor, 
                new ModelStateDictionary())
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
}

```

##### 2.4.3 controller action invoker provider













