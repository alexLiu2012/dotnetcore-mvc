## about mvc action constraints



### 1. about



### 2. details

#### 2.1 action constraint 抽象

##### 2.1.1 action constraint metadata

* 标记类型

```c#
public interface IActionConstraintMetadata
{
}

```

##### 2.1.2 action constraint 接口

```c#
public interface IActionConstraint : IActionConstraintMetadata
{    
    int Order { get; }        
    bool Accept(ActionConstraintContext context);
}

```

###### 2.1.2.1 action constraint context

```c#
public class ActionConstraintContext
{        
    public RouteContext RouteContext { get; set; } = default!;
    public ActionSelectorCandidate CurrentCandidate { get; set; } = default!;
    public IReadOnlyList<ActionSelectorCandidate> Candidates { get; set; } = 
        Array.Empty<ActionSelectorCandidate>();                       
}

```

###### 2.1.2.2 action selector candidate

```c#
public readonly struct ActionSelectorCandidate
{
    public ActionDescriptor Action { get; }            
    public IReadOnlyList<IActionConstraint> Constraints { get; }
    
    public ActionSelectorCandidate(
        ActionDescriptor action, 
        IReadOnlyList<IActionConstraint> constraints)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }
        
        Action = action;
        Constraints = constraints;
    }                
}

```

##### 2.1.3 consume action constraint 接口

```c#
internal interface IConsumesActionConstraint : IActionConstraint
{
}

```

##### 2.1.4 action constraint factory 接口

```c#
public interface IActionConstraintFactory : IActionConstraintMetadata
{    
    bool IsReusable { get; }        
    IActionConstraint CreateInstance(IServiceProvider services);
}

```

#### 2.2 action constraint 实现

##### 2.2.1 http method action constraint

```c#
public class HttpMethodActionConstraint : IActionConstraint
{    
    public static readonly int HttpMethodConstraintOrder = 100;
    
    private readonly IReadOnlyList<string> _httpMethods;
    public IEnumerable<string> HttpMethods => _httpMethods;
    /// <inheritdoc />
    public int Order => HttpMethodConstraintOrder;
    
    // 由 http method （string）集合创建 constraint
    public HttpMethodActionConstraint(IEnumerable<string> httpMethods)
    {
        if (httpMethods == null)
        {
            throw new ArgumentNullException(nameof(httpMethods));
        }
        
        var methods = new List<string>();
        
        foreach (var method in httpMethods)
        {
            if (string.IsNullOrEmpty(method))
            {
                throw new ArgumentException("httpMethod cannot be null or empty");
            }
            
            methods.Add(method);
        }
        
        _httpMethods = new ReadOnlyCollection<string>(methods);
    }
            
    /// <inheritdoc />
    public virtual bool Accept(ActionConstraintContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        /* 如果内部 http method 集合为 empty，返回 true，
            即支持任何 http method */
        if (_httpMethods.Count == 0)
        {
            return true;
        }
        
        /* 解析 http request method */
        var request = context.RouteContext.HttpContext.Request;
        var method = request.Method;
        
        // 遍历内部 http method 集合
        for (var i = 0; i < _httpMethods.Count; i++)
        {
            /* 如果解析的 http request method 包含在内部 http method，
               返回 true */
            var supportedMethod = _httpMethods[i];
            if (string.Equals(
                	supportedMethod, 
                	method, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        /* 否则，返回 false */
        return false;
    }
}

```

#### 2.2 action constraint provider

##### 2.2.1 接口

```c#
public interface IActionConstraintProvider
{
        
    // Providers are executed in an ordering determined by an ascending sort of the "Order".
    //
    // A provider with a lower numeric value of "Order" will have its "OnProvidersExecuting"
    // called before that of a provider with a higher numeric value of "Order". 
    //
    // The "OnProvidersExecuted" method is called in the reverse ordering after all calls 
    // to "OnProvidersExecuting".
    //
    // A provider with a lower numeric value of "Order" will have its "OnProvidersExecuted" 
    // method called after that of a provider with a higher numeric value of "Order".   
    // 
    // If two providers have the same numeric value of "Order", then their relative 
    // execution order is undefined.        
    int Order { get; }
        
    void OnProvidersExecuting(ActionConstraintProviderContext context);       
    void OnProvidersExecuted(ActionConstraintProviderContext context);
}

```

###### 2.2.1.1 action constraint provider context

```c#
public class ActionConstraintProviderContext
{
    public HttpContext HttpContext { get; }       
    public ActionDescriptor Action { get; }            
    public IList<ActionConstraintItem> Results { get; }
        
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
}

```

###### 2.2.1.2 action constraint item

```c#
public class ActionConstraintItem
{
    public IActionConstraint Constraint { get; set; } = default!;        
    public IActionConstraintMetadata Metadata { get; }        
    public bool IsReusable { get; set; }
        
    public ActionConstraintItem(IActionConstraintMetadata metadata)
    {
        if (metadata == null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }
        
        Metadata = metadata;
    }                
}

```

##### 2.2.2 default action constraint provider

```c#
internal class DefaultActionConstraintProvider : IActionConstraintProvider
{
    /// <inheritdoc />
    public int Order => -1000;
    
    /// <inheritdoc />
    public void OnProvidersExecuting(ActionConstraintProviderContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        for (var i = 0; i < context.Results.Count; i++)
        {
            ProvideConstraint(
                context.Results[i], 
                context.HttpContext.RequestServices);
        }
    }
    
    private void ProvideConstraint(
        ActionConstraintItem item, 
        IServiceProvider services)
    {
        // Don't overwrite anything that was done by a previous provider.
        if (item.Constraint != null)
        {
            return;
        }
        
        if (item.Metadata is IActionConstraint constraint)
        {
            item.Constraint = constraint;
            item.IsReusable = true;
            return;
        }
        
        if (item.Metadata is IActionConstraintFactory factory)
        {
            item.Constraint = factory.CreateInstance(services);
            item.IsReusable = factory.IsReusable;
            return;
        }
    }
    
    /// <inheritdoc />
    public void OnProvidersExecuted(ActionConstraintProviderContext context)
    {
    }        
}

```

