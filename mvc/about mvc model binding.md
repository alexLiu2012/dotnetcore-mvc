## about mvc model binding

### 1. about



### 2. value provider

* 从不同数据源获取数据（value）

#### 2.1 value provider 抽象

##### 2.1.1 value provider

###### 2.1.1.1 value provider 接口

```c#
public interface IValueProvider
{    
    bool ContainsPrefix(string prefix);       
    ValueProviderResult GetValue(string key);
}

```

###### 2.1.1.2 value provider result

* 表示 value provider 结果的封装

```c#
public readonly struct ValueProviderResult : 
	IEquatable<ValueProviderResult>, 
	IEnumerable<string>
{
    // 静态实例
    public static ValueProviderResult None = new ValueProviderResult(new string[0]);
    // default culture_info    
    private static readonly CultureInfo _invariantCulture = CultureInfo.InvariantCulture;       
        
    public CultureInfo Culture { get; }       
    public StringValues Values { get; }
        
    public int Length => Values.Count;
    
    public string? FirstValue
    {
        get
        {
            if (Values.Count == 0)
            {
                return null;
            }
            return Values[0];
        }
    }
                
    public ValueProviderResult(StringValues values) 
        : this(
            values, 
            _invariantCulture)
    {
    }
        
    public ValueProviderResult(
        StringValues values, 
        CultureInfo? culture)
    {
        Values = values;
        Culture = culture ?? _invariantCulture;
    }
                
    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        var other = obj as ValueProviderResult?;
        return other.HasValue && Equals(other.Value);
    }
    
    /// <inheritdoc />
    public bool Equals(ValueProviderResult other)
    {
        if (Length != other.Length)
        {
            return false;
        }
        else
        {
            var x = Values;
            var y = other.Values;
            for (var i = 0; i < x.Count; i++)
            {
                if (!string.Equals(
                    	x[i], 
                    	y[i], 
                    	StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }
    }
    
    /// <inheritdoc />
    public override int GetHashCode()
    {
        return ToString().GetHashCode();
    }
    
    /// <inheritdoc />
    public override string ToString()
    {
        return Values.ToString();
    }
        
    public IEnumerator<string> GetEnumerator()
    {
        return ((IEnumerable<string>)Values).GetEnumerator();        
    }
    
    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
        
    public static explicit operator string(ValueProviderResult result)
    {
        return result.Values;
    }
        
    public static explicit operator string[](ValueProviderResult result)
    {
        return result.Values;
    }
        
    public static bool operator ==(ValueProviderResult x, ValueProviderResult y)
    {
        return x.Equals(y);
    }
        
    public static bool operator !=(ValueProviderResult x, ValueProviderResult y)
    {
        return !x.Equals(y);
    }
}

```

##### 2.1.2 value provider factory

###### 2.1.2.1 value provider factory 接口

```c#
public interface IValueProviderFactory
{    
    Task CreateValueProviderAsync(ValueProviderFactoryContext context);
}

```

###### 2.1.2.2 value provider factory context

* 保存创建 value provider 的上下文信息，
* 和创建的 value provider（结果）

```c#
public class ValueProviderFactoryContext
{
    public ActionContext ActionContext { get; }        
    public IList<IValueProvider> ValueProviders { get; } = new List<IValueProvider>();
    
    public ValueProviderFactoryContext(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        ActionContext = context;
    }            
}

```

###### 2.1.2.3 value provider factory 扩展

```c#
public static class ValueProviderFactoryExtensions
{    
    public static void RemoveType<TValueProviderFactory>(
        this IList<IValueProviderFactory> list) 
        	where TValueProviderFactory : IValueProviderFactory
    {
        if (list == null)
        {
            throw new ArgumentNullException(nameof(list));
        }
        
        RemoveType(list, typeof(TValueProviderFactory));
    }
        
    public static void RemoveType(
        this IList<IValueProviderFactory> list, 
        Type type)
    {
        if (list == null)
        {
            throw new ArgumentNullException(nameof(list));
        }        
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }
        
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var valueProviderFactory = list[i];
            if (valueProviderFactory.GetType() == type)
            {
                list.RemoveAt(i);
            }
        }
    }
}

```

##### 2.1.3 composite value provider

* 组合模式的 value provider

```c#
public class CompositeValueProvider :        
	Collection<IValueProvider>,        
	IEnumerableValueProvider,        
	IBindingSourceValueProvider,       
	IKeyRewriterValueProvider
{        
    public CompositeValueProvider()
    {
    }
        
    public CompositeValueProvider(IList<IValueProvider> valueProviders)        
        : base(valueProviders)
    {
    }
        
    public static async Task<CompositeValueProvider> 
        CreateAsync(ControllerContext controllerContext)
    {
        if (controllerContext == null)
        {
            throw new ArgumentNullException(nameof(controllerContext));
        }
        // 从 controller context 中解析 value provider factory
        var factories = controllerContext.ValueProviderFactories;
        // 由 controller context、valuep provider factory 创建 composite value provider
        return await CreateAsync(controllerContext, factories);
    }
        
    /* 创建 composite value provider */
    public static async Task<CompositeValueProvider> CreateAsync(
        ActionContext actionContext,
        IList<IValueProviderFactory> factories)
    {
        // 创建 value provider factory context
        var valueProviderFactoryContext = new ValueProviderFactoryContext(actionContext);
        
        // 遍历传入的 value provider factory，
        // 由 factory 创建 value provider，并注入 value provider factory context
        for (var i = 0; i < factories.Count; i++)
        {
            var factory = factories[i];
            await factory.CreateValueProviderAsync(valueProviderFactoryContext);
        }
        
        // 使用 value provider factory context 中的 value provider，
        // 创建 composite value provider
        return new CompositeValueProvider(valueProviderFactoryContext.ValueProviders);
    }
    
    internal static async 
        ValueTask<(bool success, CompositeValueProvider? valueProvider)> 
        	TryCreateAsync(
        		ActionContext actionContext,
	            IList<IValueProviderFactory> factories)
    {
        try
        {
            var valueProvider = await CreateAsync(actionContext, factories);
            return (true, valueProvider);
        }
        catch (ValueProviderException exception)
        {
            actionContext.ModelState.TryAddModelException(key: string.Empty, exception);
            return (false, null);
        }
    }
    
    /* 实现 IValueProvider 接口 */
    /// <inheritdoc />
    public virtual bool ContainsPrefix(string prefix)
    {
        for (var i = 0; i < Count; i++)
        {
            if (this[i].ContainsPrefix(prefix))
            {
                return true;
            }
        }
        return false;
    }    
    /// <inheritdoc />
    public virtual ValueProviderResult GetValue(string key)
    {
        // Performance-sensitive
        // Caching the count is faster for IList<T>
        var itemCount = Items.Count;
        for (var i = 0; i < itemCount; i++)
        {
            var valueProvider = Items[i];
            var result = valueProvider.GetValue(key);
            if (result != ValueProviderResult.None)
            {
                return result;
            }
        }
        
        return ValueProviderResult.None;
    }
    
    /* 实现 IEnumerableValueProvider 接口 */
    /// <inheritdoc />
    public virtual IDictionary<string, string> GetKeysFromPrefix(string prefix)
    {
        foreach (var valueProvider in this)
        {
            if (valueProvider is IEnumerableValueProvider enumeratedProvider)
            {
                var result = enumeratedProvider.GetKeysFromPrefix(prefix);
                if (result != null && result.Count > 0)
                {
                    return result;
                }
            }
        }
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
    
    /* 实现 collection<value provider> 接口 */
    /// <inheritdoc />
    protected override void InsertItem(int index, IValueProvider item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }
        
        base.InsertItem(index, item);
    }
    
    /// <inheritdoc />
    protected override void SetItem(int index, IValueProvider item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }
        
        base.SetItem(index, item);
    }
    
    /* 实现 IBindingSourceValueProvider 接口，
       过滤出 binding source value provider */
    /// <inheritdoc />
    public IValueProvider? Filter(BindingSource bindingSource)
    {
        if (bindingSource == null)
        {
            throw new ArgumentNullException(nameof(bindingSource));
        }
        
        var shouldFilter = false;
        for (var i = 0; i < Count; i++)
        {
            var valueProvider = Items[i];
            if (valueProvider is IBindingSourceValueProvider)
            {
                shouldFilter = true;
                break;
            }
        }
        
        if (!shouldFilter)
        {
            // No inner IBindingSourceValueProvider implementations. Result will be empty.
            return null;
        }
        
        var filteredValueProviders = new List<IValueProvider>();
        for (var i = 0; i < Count; i++)
        {
            var valueProvider = Items[i];
            if (valueProvider is IBindingSourceValueProvider bindingSourceValueProvider)
            {
                var result = bindingSourceValueProvider.Filter(bindingSource);
                if (result != null)
                {
                    filteredValueProviders.Add(result);
                }
            }
        }
        
        if (filteredValueProviders.Count == 0)
        {
            // Do not create an empty CompositeValueProvider.
            return null;
        }
        
        return new CompositeValueProvider(filteredValueProviders);
    }
    
    /* 实现 IKeyRewriteValueProvider 接口，
       过滤 */
    /// <inheritdoc />        
    public IValueProvider? Filter()
    {
        var shouldFilter = false;
        for (var i = 0; i < Count; i++)
        {
            var valueProvider = Items[i];
            if (valueProvider is IKeyRewriterValueProvider)
            {
                shouldFilter = true;
                break;
            }
        }
        
        if (!shouldFilter)
        {
            // No inner IKeyRewriterValueProvider implementations. Nothing to exclude.
            return this;
        }
        
        var filteredValueProviders = new List<IValueProvider>();
        for (var i = 0; i < Count; i++)
        {
            var valueProvider = Items[i];
            if (valueProvider is IKeyRewriterValueProvider keyRewriterValueProvider)
            {
                var result = keyRewriterValueProvider.Filter();
                if (result != null)
                {
                    filteredValueProviders.Add(result);
                }
            }
            else
            {
                // Assume value providers that aren't rewriter-aware do not rewrite their keys.
                filteredValueProviders.Add(valueProvider);
            }
        }
        
        if (filteredValueProviders.Count == 0)
        {
            // Do not create an empty CompositeValueProvider.
            return null;
        }
        
        return new CompositeValueProvider(filteredValueProviders);
    }
}

```

###### 2.1.3.1 IBindingSourceValueProvider

```c#
public interface IBindingSourceValueProvider : IValueProvider
{        
    IValueProvider? Filter(BindingSource bindingSource);
}

```

###### 2.1.3.2 IEnumerableValueProvider

```c#
public interface IEnumerableValueProvider : IValueProvider
{    
    IDictionary<string, string> GetKeysFromPrefix(string prefix);
}

```

###### 2.1.3.3 IKeyRewriteValueProvider

```c#
public interface IKeyRewriterValueProvider : IValueProvider
{    
    IValueProvider? Filter();
}

```

#### 2.2 value provider 派生

* 针对具体数据源的 value provider 和 value provider factory

##### 2.2.1 form file value 

###### 2.2.1.1 form file value provider

```c#
public sealed class FormFileValueProvider : IValueProvider
{
    private readonly IFormFileCollection _files;   
    
    private PrefixContainer? _prefixContainer;
    private PrefixContainer PrefixContainer
    {
        get
        {
            _prefixContainer ??= CreatePrefixContainer(_files);
            return _prefixContainer;
        }
    }
    private static PrefixContainer CreatePrefixContainer(IFormFileCollection formFiles)
    {
        var fileNames = new List<string>();
        var count = formFiles.Count;
        for (var i = 0; i < count; i++)
        {
            var file = formFiles[i];
            
            // If there is an <input type="file" ... /> in the form and is left blank.
            // This matches the filtering behavior from FormFileModelBinder
            if (file.Length == 0 && 
                string.IsNullOrEmpty(file.FileName))
            {
                continue;
            }
            
            fileNames.Add(file.Name);
        }
        
        return new PrefixContainer(fileNames);
    }
    
    public FormFileValueProvider(IFormFileCollection files)
    {
        _files = files ?? throw new ArgumentNullException(nameof(files));
    }
                    
    /// <inheritdoc />
    public bool ContainsPrefix(string prefix) => PrefixContainer.ContainsPrefix(prefix);
    
    /// <inheritdoc />
    public ValueProviderResult GetValue(string key) => ValueProviderResult.None;
}

```

###### 2.3.1.2 form file value provider factory

```c#
public sealed class FormFileValueProviderFactory : IValueProviderFactory
{
    /// <inheritdoc />
    public Task CreateValueProviderAsync(ValueProviderFactoryContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        /* 解析 http request */
        var request = context.ActionContext.HttpContext.Request;
        if (request.HasFormContentType)
        {
            /* 如果 http request 包含 form content，
               创建 form file value provider，
               并注入 value provider factory context */
            // Allocating a Task only when the body is multipart form.
            return AddValueProviderAsync(context, request);
        }
        
        return Task.CompletedTask;
    }
    
    private static async Task AddValueProviderAsync(
        ValueProviderFactoryContext context, 
        HttpRequest request)
    {
        IFormCollection form;
        
        try
        {
            /* 获取 request form */
            form = await request.ReadFormAsync();
        }
        catch (InvalidDataException ex)
        {
            // ReadFormAsync can throw InvalidDataException if the form content is malformed.
            // Wrap it in a ValueProviderException that 
            // the CompositeValueProvider special cases.
            throw new ValueProviderException(
                Resources.FormatFailedToReadRequestForm(ex.Message), 
                ex);
        }
        catch (IOException ex)
        {
            // ReadFormAsync can throw IOException if the client disconnects.
            // Wrap it in a ValueProviderException that 
            // the CompositeValueProvider special cases.
            throw new ValueProviderException(
                Resources.FormatFailedToReadRequestForm(ex.Message), 
                ex);
        }
        
        // 如果 request form 中 files 不为空，
        // 创建 form file value provider 并注入 value provider factory context
        if (form.Files.Count > 0)
        {
            var valueProvider = new FormFileValueProvider(form.Files);
            context.ValueProviders.Add(valueProvider);
        }
    }
}

```

##### 2.3.2 elemental value

###### 2.3.2.1 elemental value provider

```c#
internal class ElementalValueProvider : IValueProvider
{
    public CultureInfo Culture { get; }    
    public string Key { get; }    
    public string? Value { get; }
    
    public ElementalValueProvider(
        string key, 
        string? value, 
        CultureInfo culture)
    {
        Key = key;
        Value = value;
        Culture = culture;
    }        
    
    public bool ContainsPrefix(string prefix)
    {
        return ModelStateDictionary.StartsWithPrefix(prefix, Key);
    }
    
    public ValueProviderResult GetValue(string key)
    {
        if (string.Equals(
            key, 
            Key, 
            StringComparison.OrdinalIgnoreCase))
        {
            return new ValueProviderResult(Value, Culture);
        }
        else
        {
            return ValueProviderResult.None;
        }
    }
}

```

##### 2.3.3 binding source provider

###### 2.3.3.1 抽象基类

```c#
public abstract class BindingSourceValueProvider : IBindingSourceValueProvider
{
    protected BindingSource BindingSource { get; }
    
    public BindingSourceValueProvider(BindingSource bindingSource)
    {
        if (bindingSource == null)
        {
            throw new ArgumentNullException(nameof(bindingSource));
        }
        
        if (bindingSource.IsGreedy)
        {
            var message = Resources.FormatBindingSource_CannotBeGreedy(
                bindingSource.DisplayName,
                nameof(BindingSourceValueProvider));
            throw new ArgumentException(message, nameof(bindingSource));
        }
        
        if (bindingSource is CompositeBindingSource)
        {
            var message = Resources.FormatBindingSource_CannotBeComposite(
                bindingSource.DisplayName,
                nameof(BindingSourceValueProvider));
            throw new ArgumentException(message, nameof(bindingSource));
        }
        
        BindingSource = bindingSource;
    }
               
    /// <inheritdoc />
    public abstract bool ContainsPrefix(string prefix);
    
    /// <inheritdoc />
    public abstract ValueProviderResult GetValue(string key);
    
    /// <inheritdoc />
    public virtual IValueProvider? Filter(BindingSource bindingSource)
    {
        if (bindingSource == null)
        {
            throw new ArgumentNullException(nameof(bindingSource));
        }
        
        if (bindingSource.CanAcceptDataFrom(BindingSource))
        {
            return this;
        }
        else
        {
            return null;
        }
    }
}

```

###### 2.3.3.2 form value provider

```c#
public class FormValueProvider : 
	BindingSourceValueProvider, 
	IEnumerableValueProvider
{
    private readonly IFormCollection _values;
    
    public CultureInfo? Culture { get; }
    private PrefixContainer? _prefixContainer;
    protected PrefixContainer PrefixContainer
    {
        get
        {
            if (_prefixContainer == null)
            {
                _prefixContainer = new PrefixContainer(_values.Keys);
            }
            
            return _prefixContainer;
        }
    }
        
    public FormValueProvider(
        BindingSource bindingSource,
        IFormCollection values,
        CultureInfo? culture)        	
        	: base(bindingSource)
    {
        if (bindingSource == null)
        {
            throw new ArgumentNullException(nameof(bindingSource));
        }        
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }
        
        _values = values;
        Culture = culture;
    }
                
    /// <inheritdoc />
    public override bool ContainsPrefix(string prefix)
    {
        return PrefixContainer.ContainsPrefix(prefix);
    }
    
    /// <inheritdoc />
    public virtual IDictionary<string, string> GetKeysFromPrefix(string prefix)
    {
        if (prefix == null)
        {
            throw new ArgumentNullException(nameof(prefix));
        }
        
        return PrefixContainer.GetKeysFromPrefix(prefix);
    }
    
    /// <inheritdoc />
    public override ValueProviderResult GetValue(string key)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }
        
        if (key.Length == 0)
        {
            // Top level parameters will fall back to an empty prefix 
            // when the parameter name does not appear in any value provider. 
            // This would result in the parameter binding to a form parameter
            // with a empty key (e.g. Request body looks like "=test") which 
            // isn't a scenario we want to support.
            // Return a "None" result in this event.
            return ValueProviderResult.None;
        }
        
        var values = _values[key];
        if (values.Count == 0)
        {
            return ValueProviderResult.None;
        }
        else
        {
            return new ValueProviderResult(values, Culture);
        }
    }
}

```

###### 2.3.3.3 form value provider factory

```c#
public class FormValueProviderFactory : IValueProviderFactory
{
    /// <inheritdoc />
    public Task CreateValueProviderAsync(ValueProviderFactoryContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        var request = context.ActionContext.HttpContext.Request;
        if (request.HasFormContentType)
        {
            // Allocating a Task only when the body is form data.
            return AddValueProviderAsync(context);
        }
        
        return Task.CompletedTask;
    }
    
    private static async Task AddValueProviderAsync(ValueProviderFactoryContext context)
    {
        var request = context.ActionContext.HttpContext.Request;
        IFormCollection form;
        
        try
        {
            form = await request.ReadFormAsync();
        }
        catch (InvalidDataException ex)
        {
            // ReadFormAsync can throw InvalidDataException if the form content is malformed.
            // Wrap it in a ValueProviderException that 
            // the CompositeValueProvider special cases.
            throw new ValueProviderException(
                Resources.FormatFailedToReadRequestForm(ex.Message), 
                ex);
        }
        catch (IOException ex)
        {
            // ReadFormAsync can throw IOException if the client disconnects.
            // Wrap it in a ValueProviderException that 
            // the CompositeValueProvider special cases.
            throw new ValueProviderException(
                Resources.FormatFailedToReadRequestForm(ex.Message), 
                ex);
        }
        
        var valueProvider = new FormValueProvider(
            BindingSource.Form,
            form,
            CultureInfo.CurrentCulture);
        
        context.ValueProviders.Add(valueProvider);
    }
}

```

###### 2.3.3.4 query string value provider

```c#
public class QueryStringValueProvider : BindingSourceValueProvider, IEnumerableValueProvider
{
    private readonly IQueryCollection _values;
    private PrefixContainer? _prefixContainer;
    
    
    public QueryStringValueProvider(
        BindingSource bindingSource,
        IQueryCollection values,
        CultureInfo? culture)            : base(bindingSource)
    {
        if (bindingSource == null)
        {
            throw new ArgumentNullException(nameof(bindingSource));
        }
        
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }
        
        _values = values;
        Culture = culture;
    }
    
    
    public CultureInfo? Culture { get; }
    
    /// <summary>
    // The <see cref="PrefixContainer"/>.
    /// </summary>
    protected PrefixContainer PrefixContainer
    {
        get
        {
            if (_prefixContainer == null)
            {
                _prefixContainer = new PrefixContainer(_values.Keys);
            }
            
            return _prefixContainer;
        }
    }
    
    /// <inheritdoc />
    public override bool ContainsPrefix(string prefix)
    {
        return PrefixContainer.ContainsPrefix(prefix);
    }
    
    /// <inheritdoc />
    public virtual IDictionary<string, string> GetKeysFromPrefix(string prefix)
    {
        if (prefix == null)
        {
            throw new ArgumentNullException(nameof(prefix));
        }
        
        return PrefixContainer.GetKeysFromPrefix(prefix);
    }
    
    /// <inheritdoc />
    public override ValueProviderResult GetValue(string key)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }
        
        if (key.Length == 0)
        {
            // Top level parameters will fall back to an empty prefix 
            // when the parameter name does not appear in any value provider. 
            // This would result in the parameter binding to a query string
            // parameter with a empty key (e.g. /User?=test) which 
            // isn't a scenario we want to support.
            // Return a "None" result in this event.
            return ValueProviderResult.None;
        }
        
        var values = _values[key];
        if (values.Count == 0)
        {
            return ValueProviderResult.None;
        }
        else
        {
            return new ValueProviderResult(values, Culture);
        }
    }
}

```



###### 2.3.3.5 query string value provider factory

```c#
public class QueryStringValueProviderFactory : IValueProviderFactory
{
    /// <inheritdoc />
    public Task CreateValueProviderAsync(ValueProviderFactoryContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        var query = context.ActionContext.HttpContext.Request.Query;
        if (query != null && query.Count > 0)
        {
            var valueProvider = new QueryStringValueProvider(
                BindingSource.Query,
                query,
                CultureInfo.InvariantCulture);
            
            context.ValueProviders.Add(valueProvider);
        }
        
        return Task.CompletedTask;
    }
}

```

###### 2.3.3.6 route value provider

```c#
public class RouteValueProvider : BindingSourceValueProvider
{
    private readonly RouteValueDictionary _values;
    private PrefixContainer? _prefixContainer;
        
    public RouteValueProvider(
        BindingSource bindingSource,
        RouteValueDictionary values) 
        	: this(
              	bindingSource, 
                values, 
                CultureInfo.InvariantCulture)
    {
    }
    
    
    public RouteValueProvider(
        BindingSource bindingSource, 
        RouteValueDictionary values, 
        CultureInfo culture)            
        	: base(bindingSource)
    {
        if (bindingSource == null)
        {
            throw new ArgumentNullException(nameof(bindingSource));
        }        
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }        
        if (culture == null)
        {
            throw new ArgumentNullException(nameof(culture));
        }
        
        _values = values;
        Culture = culture;
    }
    
    /// <summary>
    /// The prefix container.
    /// </summary>
    protected PrefixContainer PrefixContainer
    {
        get
        {
            if (_prefixContainer == null)
            {
                _prefixContainer = new PrefixContainer(_values.Keys);
            }
            
            return _prefixContainer;
        }
    }
    
    /// <summary>
    /// The culture to use.
    /// </summary>
    protected CultureInfo Culture { get; }
    
    /// <inheritdoc />
    public override bool ContainsPrefix(string key)
    {
        return PrefixContainer.ContainsPrefix(key);
    }
    
    /// <inheritdoc />
    public override ValueProviderResult GetValue(string key)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }
        
        if (key.Length == 0)
        {
            // Top level parameters will fall back to an empty prefix when the parameter name does not
            // appear in any value provider. This would result in the parameter binding to a route value
            // an empty key which isn't a scenario we want to support.
            // Return a "None" result in this event.
            return ValueProviderResult.None;
        }
        
        if (_values.TryGetValue(key, out var value))
        {
            var stringValue = value as string ?? Convert.ToString(value, Culture) ?? string.Empty;
            return new ValueProviderResult(stringValue, Culture);
        }
        else
        {
            return ValueProviderResult.None;
        }
    }
}

```

###### 2.3.3.7 route value provider factory

```c#
public class RouteValueProviderFactory : IValueProviderFactory
{
    /// <inheritdoc />
    public Task CreateValueProviderAsync(ValueProviderFactoryContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        var valueProvider = new RouteValueProvider(
            BindingSource.Path,
            context.ActionContext.RouteData.Values);
        
        context.ValueProviders.Add(valueProvider);
        
        return Task.CompletedTask;
    }
}

```

##### 2.3.4 jQuery value provider

###### 2.3.4.1 抽象基类

```c#
public abstract class JQueryValueProvider :        
	BindingSourceValueProvider,        
	IEnumerableValueProvider,        
	IKeyRewriterValueProvider
{
    private readonly IDictionary<string, StringValues> _values;
    private PrefixContainer? _prefixContainer;
        
    protected JQueryValueProvider(
        BindingSource bindingSource,
        IDictionary<string, StringValues> values,
        CultureInfo? culture)            : base(bindingSource)
    {
        if (bindingSource == null)
        {
            throw new ArgumentNullException(nameof(bindingSource));
        }        
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }
        
        _values = values;
        Culture = culture;
    }
    
        
    public CultureInfo? Culture { get; }
    
    /// <inheritdoc />
    protected PrefixContainer PrefixContainer
    {
        get
        {
            if (_prefixContainer == null)
            {
                _prefixContainer = new PrefixContainer(_values.Keys);
            }
            
            return _prefixContainer;
        }
    }
    
    /// <inheritdoc />
    public override bool ContainsPrefix(string prefix)
    {
        return PrefixContainer.ContainsPrefix(prefix);
    }
    
    /// <inheritdoc />
    public IDictionary<string, string> GetKeysFromPrefix(string prefix)
    {
        return PrefixContainer.GetKeysFromPrefix(prefix);
    }
    
    /// <inheritdoc />
    public override ValueProviderResult GetValue(string key)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }
        
        if (_values.TryGetValue(key, out var values) && values.Count > 0)
        {
            return new ValueProviderResult(values, Culture);
        }
        
        return ValueProviderResult.None;
    }
    
   
    public IValueProvider? Filter()
    {
        return null;
    }
}

```

###### 2.3.4.2 jQuery form value provider

```c#
public class JQueryFormValueProvider : JQueryValueProvider
{
    
    public JQueryFormValueProvider(
        BindingSource bindingSource,
        IDictionary<string, StringValues> values,
        CultureInfo? culture)
	        : base(bindingSource, values, culture)
    {
    }
}

```

###### 2.3.4.3 jQuery form value provider factory

```c#
public class JQueryFormValueProviderFactory : IValueProviderFactory
{
    /// <inheritdoc />
    public Task CreateValueProviderAsync(ValueProviderFactoryContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        var request = context.ActionContext.HttpContext.Request;
        if (request.HasFormContentType)
        {
            // Allocating a Task only when the body is form data.
            return AddValueProviderAsync(context);
        }
        
        return Task.CompletedTask;
    }
    
    private static async Task AddValueProviderAsync(ValueProviderFactoryContext context)
    {
        var request = context.ActionContext.HttpContext.Request;
        
        IFormCollection formCollection;
        try
        {
            formCollection = await request.ReadFormAsync();
        }
        catch (InvalidDataException ex)
        {
            // ReadFormAsync can throw InvalidDataException if the form content is malformed.
            // Wrap it in a ValueProviderException that 
            the CompositeValueProvider special cases.
            throw new ValueProviderException(
                Resources.FormatFailedToReadRequestForm(ex.Message), 
                ex);
        }
        catch (IOException ex)
        {
            // ReadFormAsync can throw IOException if the client disconnects.
            // Wrap it in a ValueProviderException that 
            the CompositeValueProvider special cases.
            throw new ValueProviderException(
                Resources.FormatFailedToReadRequestForm(ex.Message), 
                ex);
        }
        
        var valueProvider = new JQueryFormValueProvider(
            BindingSource.Form,
            JQueryKeyValuePairNormalizer
            	.GetValues(formCollection, formCollection.Count),
            CultureInfo.CurrentCulture);
        
        context.ValueProviders.Add(valueProvider);
    }
}

```



###### 2.3.4.4 jQuery query string value provider

```c#
public class JQueryQueryStringValueProvider : JQueryValueProvider
{
    
    public JQueryQueryStringValueProvider(
        BindingSource bindingSource,
        IDictionary<string, StringValues> values,
        CultureInfo? culture)            : base(bindingSource, values, culture)
    {
    }
}

```

###### 2.3.4.5 jQuery query string value provider factory

```c#
public class JQueryQueryStringValueProviderFactory : IValueProviderFactory
{
    /// <inheritdoc />
    public Task CreateValueProviderAsync(ValueProviderFactoryContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        var query = context.ActionContext.HttpContext.Request.Query;
        if (query != null && query.Count > 0)
        {
            var valueProvider = new JQueryQueryStringValueProvider(
                BindingSource.Query,
                JQueryKeyValuePairNormalizer.GetValues(query, query.Count),
                CultureInfo.InvariantCulture);
            
            context.ValueProviders.Add(valueProvider);
        }
        
        return Task.CompletedTask;
    }
}

```







### 3. model binder

#### 3.1 binding info

```c#
/// <summary>
/// Binding info which represents metadata associated to an action parameter.
/// </summary>
public class BindingInfo
{    
    // binding source
    public BindingSource? BindingSource { get; set; }       
    // model name
    public string? BinderModelName { get; set; }        
    // model binder type
    private Type? _binderType;
    public Type? BinderType
    {
        get => _binderType;
        set
        {
            if (value != null && 
                !typeof(IModelBinder).IsAssignableFrom(value))
            {
                throw new ArgumentException(
                    Resources.FormatBinderType_MustBeIModelBinder(
                        value.FullName,
                        typeof(IModelBinder).FullName),
                    nameof(value));
            }
            
            _binderType = value;
        }
    }
    // property filter provider        
    public IPropertyFilterProvider? PropertyFilterProvider { get; set; }    
    // predicate -> 是否从 http request 绑定
    public Func<ActionContext, bool>? RequestPredicate { get; set; }
    // 处理 empty body 的行为
    public EmptyBodyBehavior EmptyBodyBehavior { get; set; }
    
    public BindingInfo()
    {
    }
        
    public BindingInfo(BindingInfo other)
    {
        if (other == null)
        {
            throw new ArgumentNullException(nameof(other));
        }
        
        BindingSource = other.BindingSource;
        BinderModelName = other.BinderModelName;
        BinderType = other.BinderType;
        PropertyFilterProvider = other.PropertyFilterProvider;
        RequestPredicate = other.RequestPredicate;
        EmptyBodyBehavior = other.EmptyBodyBehavior;
    }
        
    
    
    
    
    
    private class CompositePropertyFilterProvider : IPropertyFilterProvider
    {
        private readonly IEnumerable<IPropertyFilterProvider> _providers;
        
        public CompositePropertyFilterProvider(IEnumerable<IPropertyFilterProvider> providers)
        {
            _providers = providers;
        }
        
        public Func<ModelMetadata, bool> PropertyFilter => CreatePropertyFilter();
        
        private Func<ModelMetadata, bool> CreatePropertyFilter()
        {
            var propertyFilters = _providers
                .Select(p => p.PropertyFilter)
                .Where(p => p != null);
            
            return (m) =>
            {
                foreach (var propertyFilter in propertyFilters)
                {
                    if (!propertyFilter(m))
                    {
                        return false;
                    }
                }
                
                return true;
            };
        }
    }
}

```

##### 3.1.1 get binding info via  attribute

```c#
public class BindingInfo
{
    /* 静态方法，从标记特性创建 binding info  */
    public static BindingInfo? GetBindingInfo(IEnumerable<object> attributes)
    {
        var bindingInfo = new BindingInfo();
        var isBindingInfoPresent = false;
        
        /* a - 通过 model name provider 配置 model name */
        // BinderModelName
        foreach (var binderModelNameAttribute in attributes.OfType<IModelNameProvider>())
        {
            isBindingInfoPresent = true;
            if (binderModelNameAttribute?.Name != null)
            {
                bindingInfo.BinderModelName = binderModelNameAttribute.Name;
                break;
            }
        }
        
        /* b - 通过 binding type provider metadata 配置 model binder type */
        // BinderType
        foreach (var binderTypeAttribute in attributes.OfType<IBinderTypeProviderMetadata>())
        {
            isBindingInfoPresent = true;
            if (binderTypeAttribute.BinderType != null)
            {
                bindingInfo.BinderType = binderTypeAttribute.BinderType;
                break;
            }
        }
        
        /* c - 通过 binding source metadata 特性配置 binding source */
        // BindingSource
        foreach (var bindingSourceAttribute in attributes.OfType<IBindingSourceMetadata>())
        {
            isBindingInfoPresent = true;
            if (bindingSourceAttribute.BindingSource != null)
            {
                bindingInfo.BindingSource = bindingSourceAttribute.BindingSource;
                break;
            }
        }
        
        /* d - 通过 property filter provider 特性配置 property filter provider */
        // PropertyFilterProvider        
        var propertyFilterProviders = attributes.OfType<IPropertyFilterProvider>().ToArray();
        if (propertyFilterProviders.Length == 1)
        {
            isBindingInfoPresent = true;
            bindingInfo.PropertyFilterProvider = propertyFilterProviders[0];
        }
        else if (propertyFilterProviders.Length > 1)
        {
            isBindingInfoPresent = true;
            bindingInfo.PropertyFilterProvider = 
                new CompositePropertyFilterProvider(propertyFilterProviders);
        }
        
        /* e - 通过 request predicate provider 特性配置 request predicate provider */
        // RequestPredicate
        foreach (var requestPredicateProvider in 
                 attributes.OfType<IRequestPredicateProvider>())
        {
            isBindingInfoPresent = true;
            if (requestPredicateProvider.RequestPredicate != null)
            {
                bindingInfo.RequestPredicate = requestPredicateProvider.RequestPredicate;
                break;
            }
        }
        
        /* f - 通过 configure empty body behavior 特性配置 empty body behavior */
        foreach (var configureEmptyBodyBehavior in 
                 attributes.OfType<IConfigureEmptyBodyBehavior>())
        {
            isBindingInfoPresent = true;
            bindingInfo.EmptyBodyBehavior = configureEmptyBodyBehavior.EmptyBodyBehavior;
            break;
        }
        
        return isBindingInfoPresent ? bindingInfo : null;
    }
}
```

##### 3.1.2 try get binding info via attribute and metadata

```c#
public class BindingInfo
{
    public static BindingInfo? GetBindingInfo(
        IEnumerable<object> attributes, 
        ModelMetadata modelMetadata)
    {
        if (attributes == null)
        {
            throw new ArgumentNullException(nameof(attributes));
        }
        if (modelMetadata == null)
        {
            throw new ArgumentNullException(nameof(modelMetadata));
        }
        
        // 通过 attribute 获取 binding info
        var bindingInfo = GetBindingInfo(attributes);
        var isBindingInfoPresent = bindingInfo != null;
        
        if (bindingInfo == null)
        {
            bindingInfo = new BindingInfo();
        }
        
        // 通过 model metadata 配置 binding info
        isBindingInfoPresent |= bindingInfo.TryApplyBindingInfo(modelMetadata);
        
        return isBindingInfoPresent ? bindingInfo : null;
    }
    
    
    public bool TryApplyBindingInfo(ModelMetadata modelMetadata)
    {
        if (modelMetadata == null)
        {
            throw new ArgumentNullException(nameof(modelMetadata));
        }
        
        var isBindingInfoPresent = false;
        
        // a - 设置 model name
        if (BinderModelName == null && 
            modelMetadata.BinderModelName != null)
        {
            isBindingInfoPresent = true;
            BinderModelName = modelMetadata.BinderModelName;
        }
        // b - 设置 model binder type
        if (BinderType == null && 
            modelMetadata.BinderType != null)
        {
            isBindingInfoPresent = true;
            BinderType = modelMetadata.BinderType;
        }
        // c - 设置 binding source
        if (BindingSource == null && 
            modelMetadata.BindingSource != null)
        {
            isBindingInfoPresent = true;
            BindingSource = modelMetadata.BindingSource;
        }
        // d - 设置 property filter provider
        if (PropertyFilterProvider == null && 
            modelMetadata.PropertyFilterProvider != null)
        {
            isBindingInfoPresent = true;
            PropertyFilterProvider = modelMetadata.PropertyFilterProvider;
        }
        
        // There isn't a ModelMetadata feature to configure AllowEmptyInputInBodyModelBinding, 
        // so nothing to infer from it.
        
        return isBindingInfoPresent;
    }
}
```

##### 3.1.3 配置 binding info 的接口

###### 3.1.3.1 model name provider

```c#
public interface IModelNameProvider
{    
    string Name { get; }
}

```

###### 3.1.3.2 binding type provider metadata

```c#
public interface IBinderTypeProviderMetadata : IBindingSourceMetadata
{    
    Type BinderType { get; }
}

```

###### 3.1.3.3 binding source metadata

```c#
public interface IBindingSourceMetadata
{    
    BindingSource? BindingSource { get; }
}

```

###### 3.1.3.4 property filter provider

```c#
public interface IPropertyFilterProvider
{    
    // Gets a predicate which can determines which model properties 
    // should be bound by model binding.    
    // This predicate is also used to determine which parameters 
    // are bound when a model's constructor is bound.    
    Func<ModelMetadata, bool> PropertyFilter { get; }
}

```

###### 3.1.3.5 request predicate provider

```c#
public interface IRequestPredicateProvider
{    
    // Gets a function which determines whether or not the model object should be bound based
    // on the current request.    
    Func<ActionContext, bool> RequestPredicate { get; }
}

```

###### 3.1.3.6 configure empty body behavior

```c#
internal interface IConfigureEmptyBodyBehavior
{
    public EmptyBodyBehavior EmptyBodyBehavior { get; }
}

```

```c#
public enum EmptyBodyBehavior
{    
    // Uses the framework default behavior for processing empty bodies.
    // This is typically configured using "MvcOptions.AllowEmptyInputInBodyModelBinding"    
    Default,
    
    /// Empty bodies are treated as valid inputs.    
    Allow,
        
    // Empty bodies are treated as invalid inputs.    
    Disallow,
}

```

##### 3.1.4 配置 binding info 的 attribute

###### 3.1.4.1 model binder attribute

```c#
[AttributeUsage(
    // Support method parameters in actions.
    AttributeTargets.Parameter |    
    // Support properties on model DTOs.
    AttributeTargets.Property |    
    // Support model types.
    AttributeTargets.Class |
    AttributeTargets.Enum |
    AttributeTargets.Struct,    
    AllowMultiple = false,
    Inherited = true)]
public class ModelBinderAttribute : 
	Attribute, 
	IModelNameProvider, 
	IBinderTypeProviderMetadata 
{
    // binding source
    private BindingSource _bindingSource;
    public virtual BindingSource BindingSource
    {
        get
        {
            if (_bindingSource == null && BinderType != null)
            {
                return BindingSource.Custom;
            }
            
            return _bindingSource;
        }
        protected set
        {
            _bindingSource = value;
        }
    }
       
    // binder type
    private Type _binderType;
    public Type BinderType
    {
        get => _binderType;
        set
        {
            if (value != null && !typeof(IModelBinder).IsAssignableFrom(value))
            {
                throw new ArgumentException(
                    Resources.FormatBinderType_MustBeIModelBinder(
                        value.FullName,
                        typeof(IModelBinder).FullName),
                    nameof(value));
            }
            
            _binderType = value;
        }
    }
        
    /// <inheritdoc />
    public string Name { get; set; }
    
    public ModelBinderAttribute()
    {
    }
        
    public ModelBinderAttribute(Type binderType)
    {
        if (binderType == null)
        {
            throw new ArgumentNullException(nameof(binderType));
        }
        
        BinderType = binderType;
    }                                    
}

```

###### 3.1.4.2 bind attribute

```c#
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Parameter, 
    AllowMultiple = false, 
    Inherited = true)]
public class BindAttribute : 
	Attribute, 
	IModelNameProvider, 
	IPropertyFilterProvider
{
    private static readonly Func<ModelMetadata, bool> _default = (m) => true;
        
    // Gets the names of properties to include in model binding.    
    public string[] Include { get; }
        
    /* name */
    // Allows a user to specify a particular prefix to match during model binding.    
    // This property is exposed for back compat reasons.
    public string Prefix { get; set; }        
    // Represents the model name used during model binding.    
    string IModelNameProvider.Name => Prefix;
    
    /* property filter */
    private Func<ModelMetadata, bool> _propertyFilter;
    /// <inheritdoc />
    public Func<ModelMetadata, bool> PropertyFilter
    {
        get
        {
            if (Include != null && Include.Length > 0)
            {
                _propertyFilter ??= PropertyFilter;
                return _propertyFilter;
            }
            else
            {
                return _default;
            }
            
            bool PropertyFilter(ModelMetadata modelMetadata)
            {
                if (modelMetadata.MetadataKind == ModelMetadataKind.Parameter)
                {
                    return Include.Contains(
                        modelMetadata.ParameterName, 
                        StringComparer.Ordinal);
                }
                
                return Include.Contains(
                    modelMetadata.PropertyName, 
                    StringComparer.Ordinal);
            }
        }
    }
    
    public BindAttribute(params string[] include)
    {
        var items = new List<string>(include.Length);
        foreach (var item in include)
        {
            items.AddRange(SplitString(item));
        }
        
        Include = items.ToArray();
    }
                        
    private static IEnumerable<string> SplitString(string original) => 
        original
        	?.Split(
	        	',', 
    	    	StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) 
        	?? Array.Empty<string>();
}

```

###### 3.1.4.3 bind property attribute

```c#
[AttributeUsage(
    AttributeTargets.Property, 
    AllowMultiple = false, 
    Inherited = true)]
public class BindPropertyAttribute : 
	Attribute, 
	IModelNameProvider, 
	BinderTypeProviderMetadata, 
	IRequestPredicateProvider
{
    public bool SupportsGet { get; set; }
            
    // binding source
    private BindingSource _bindingSource;
    public virtual BindingSource BindingSource
    {
        get
        {
            if (_bindingSource == null && BinderType != null)
            {
                return BindingSource.Custom;
            }
            
            return _bindingSource;
        }
        protected set => _bindingSource = value;
    }
    
    // binder type
    private Type _binderType;
    public Type BinderType
    {
        get => _binderType;
        set
        {
            if (value != null && !typeof(IModelBinder).IsAssignableFrom(value))                
            {
                throw new ArgumentException(
                    Resources.FormatBinderType_MustBeIModelBinder(
                        value.FullName,
                        typeof(IModelBinder).FullName),
                    nameof(value));
            }
            
            _binderType = value;
        }
    }
                                            
    /// <inheritdoc />
    public string Name { get; set; }
    
    /* request predicate 属性 */
    Func<ActionContext, bool> IRequestPredicateProvider.RequestPredicate => 
        SupportsGet ? _supportsAllRequests : _supportsNonGetRequests;
        
    private static readonly Func<ActionContext, bool> 
        _supportsAllRequests = (c) => true;
        
    private static readonly Func<ActionContext, bool> 
        _supportsNonGetRequests = IsNonGetRequest;
    
    private static bool IsNonGetRequest(ActionContext context)
    {
        return !HttpMethods.IsGet(context.HttpContext.Request.Method);
    }
}

```

###### 3.1.4.4 from body attribute

```c#
[AttributeUsage(
    AttributeTargets.Parameter | AttributeTargets.Property, 
    AllowMultiple = false, 
    Inherited = true)]
public class FromBodyAttribute : 
	Attribute, 
	IBindingSourceMetadata, 
	IConfigureEmptyBodyBehavior
{    
    public BindingSource BindingSource => BindingSource.Body;            
    public EmptyBodyBehavior EmptyBodyBehavior { get; set; }
}

```

###### 3.1.4.5 from form attribute

```c#
[AttributeUsage(
    AttributeTargets.Parameter | AttributeTargets.Property, 
    AllowMultiple = false, 
    Inherited = true)]
public class FromFormAttribute : 
	Attribute, 
	IBindingSourceMetadata, 
	IModelNameProvider
{
    /// <inheritdoc />
    public BindingSource BindingSource => BindingSource.Form;    
    /// <inheritdoc />
    public string Name { get; set; }
}

```

###### 3.1.4.6 from header attribute

```c#
[AttributeUsage(
    AttributeTargets.Parameter | AttributeTargets.Property, 
    AllowMultiple = false, 
    Inherited = true)]
public class FromHeaderAttribute : 
	Attribute, 
	IBindingSourceMetadata, 
	IModelNameProvider
{
    /// <inheritdoc />
    public BindingSource BindingSource => BindingSource.Header;    
    /// <inheritdoc />
    public string Name { get; set; }
}

```

###### 3.1.4.7 from query attribute

```c#
[AttributeUsage(
    AttributeTargets.Parameter | AttributeTargets.Property, 
    AllowMultiple = false, 
    Inherited = true)]
public class FromQueryAttribute : 
	Attribute, 
	IBindingSourceMetadata, 
	IModelNameProvider
{
    /// <inheritdoc />
    public BindingSource BindingSource => BindingSource.Query;    
    /// <inheritdoc />
    public string Name { get; set; }
}

```

###### 3.1.4.8 from route attribute

```c#
[AttributeUsage(
    AttributeTargets.Parameter | AttributeTargets.Property, 
    AllowMultiple = false, 
    Inherited = true)]
public class FromRouteAttribute : 
	Attribute, 
	IBindingSourceMetadata, 
	IModelNameProvider
{
    /// <inheritdoc />
    public BindingSource BindingSource => BindingSource.Path;    
    /// <inheritdoc />
    public string Name { get; set; }
}

```

###### 3.1.4.9 from service attribute

```c#
[AttributeUsage(
    AttributeTargets.Parameter, 
    AllowMultiple = false, 
    Inherited = true)]
public class FromServicesAttribute : 
	Attribute, 
	IBindingSourceMetadata
{
    /// <inheritdoc />
    public BindingSource BindingSource => BindingSource.Services;
}

```

##### 3.1.5 binding source

```c#
public class BindingSource : IEquatable<BindingSource?>
{                        
    public string Id { get; }
    public string DisplayName { get; }                    
    public bool IsGreedy { get; }                            
    public bool IsFromRequest { get; }
    
    public BindingSource(
        string id, 
        string displayName, 
        bool isGreedy, 
        bool isFromRequest)
    {
        if (id == null)
        {
            throw new ArgumentNullException(nameof(id));
        }
        
        Id = id;
        DisplayName = displayName;        
        IsGreedy = isGreedy;
        IsFromRequest = isFromRequest;
    }
                
    // Gets a value indicating whether or not the <see cref="BindingSource"/> can accept
    // data from <paramref name="bindingSource"/>.
    //
    // When using this method, it is expected that the left-hand-side is metadata specified
    // on a property or parameter for model binding, and the right hand side is a source of    
    // data used by a model binder or value provider.
    //
    // This distinction is important as the left-hand-side may be a composite, 
    // but the right may not.    
    public virtual bool CanAcceptDataFrom(BindingSource bindingSource)
    {
        if (bindingSource == null)
        {
            throw new ArgumentNullException(nameof(bindingSource));
        }
        
        if (bindingSource is CompositeBindingSource)
        {
            var message = Resources.FormatBindingSource_CannotBeComposite(
                bindingSource.DisplayName,
                nameof(CanAcceptDataFrom));
            throw new ArgumentException(message, nameof(bindingSource));
        }
        
        if (this == bindingSource)
        {
            return true;
        }
        
        if (this == ModelBinding)
        {
            return bindingSource == Form || 
                   bindingSource == Path || 
                   bindingSource == Query;
        }
        
        return false;
    }
    
    /// <inheritdoc />
    public bool Equals(BindingSource? other)
    {
        return string.Equals(
            other?.Id, 
            Id, 
            StringComparison.Ordinal);
    }    
    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as BindingSource);
    }    
    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }    
    /// <inheritdoc />
    public static bool operator ==(BindingSource? s1, BindingSource? s2)
    {
        if (s1 is null)
        {
            return s2 is null;
        }
        
        return s1.Equals(s2);
    }    
    /// <inheritdoc />
    public static bool operator !=(BindingSource? s1, BindingSource? s2)
    {
        return !(s1 == s2);
    }
}

```

###### 3.1.5.1 静态实例

```c#
public class BindingSource : IEquatable<BindingSource?>
{    
    public static readonly BindingSource Body = new BindingSource(
        "Body",
        Resources.BindingSource_Body,
        isGreedy: true,
        isFromRequest: true);
        
    public static readonly BindingSource Custom = new BindingSource(
        "Custom",
        Resources.BindingSource_Custom,
        isGreedy: true,
        isFromRequest: true);
        
    public static readonly BindingSource Form = new BindingSource(
        "Form",
        Resources.BindingSource_Form,
        isGreedy: false,
        isFromRequest: true);
        
    public static readonly BindingSource Header = new BindingSource(
        "Header",
        Resources.BindingSource_Header,
        isGreedy: true,
        isFromRequest: true);
        
    public static readonly BindingSource ModelBinding = new BindingSource(
        "ModelBinding",
        Resources.BindingSource_ModelBinding,
        isGreedy: false,
        isFromRequest: true);
            
    public static readonly BindingSource Path = new BindingSource(
        "Path",
        Resources.BindingSource_Path,
        isGreedy: false,
        isFromRequest: true);
        
    public static readonly BindingSource Query = new BindingSource(
        "Query",
        Resources.BindingSource_Query,
        isGreedy: false,
        isFromRequest: true);
        
    public static readonly BindingSource Services = new BindingSource(
        "Services",
        Resources.BindingSource_Services,
        isGreedy: true,
        isFromRequest: false);
        
    public static readonly BindingSource Special = new BindingSource(
        "Special",
        Resources.BindingSource_Special,
        isGreedy: true,
        isFromRequest: false);            
}

```

###### 3.1.5.2 composite binding source

```c#
public class CompositeBindingSource : BindingSource
{
    public IEnumerable<BindingSource> BindingSources { get; }
    
    private CompositeBindingSource(
        string id,
        string displayName,
        IEnumerable<BindingSource> bindingSources)        	
        	: base(
                id, 
                displayName, 
                isGreedy: false, 
                isFromRequest: true)
    {
        if (id == null)
        {
            throw new ArgumentNullException(nameof(id));
        }        
        if (bindingSources == null)
        {
            throw new ArgumentNullException(nameof(bindingSources));
        }
        
        BindingSources = bindingSources;
    }
    
    // 静态构造函数，单例模式    
    public static CompositeBindingSource Create(
        IEnumerable<BindingSource> bindingSources,
        string displayName)
    {
        if (bindingSources == null)
        {
            throw new ArgumentNullException(nameof(bindingSources));
        }
        
        foreach (var bindingSource in bindingSources)
        {
            if (bindingSource.IsGreedy)
            {
                var message = Resources.FormatBindingSource_CannotBeGreedy(
                    bindingSource.DisplayName,
                    nameof(CompositeBindingSource));
                throw new ArgumentException(message, nameof(bindingSources));
            }
            
            if (!bindingSource.IsFromRequest)
            {
                var message = Resources.FormatBindingSource_MustBeFromRequest(
                    bindingSource.DisplayName,
                    nameof(CompositeBindingSource));
                throw new ArgumentException(message, nameof(bindingSources));
            }
            
            if (bindingSource is CompositeBindingSource)
            {
                var message = Resources.FormatBindingSource_CannotBeComposite(
                    bindingSource.DisplayName,
                    nameof(CompositeBindingSource));
                throw new ArgumentException(message, nameof(bindingSources));
            }
        }
        
        var id = string.Join(
            "&", 
            bindingSources
            	.Select(s => s.Id)
            	.OrderBy(s => s, StringComparer.Ordinal));
        
        return new CompositeBindingSource(id, displayName, bindingSources);
    }
                        
    /// <inheritdoc />
    public override bool CanAcceptDataFrom(BindingSource bindingSource)
    {
        if (bindingSource is null)
        {
            throw new ArgumentNullException(nameof(bindingSource));
        }        
        if (bindingSource is CompositeBindingSource)
        {
            var message = Resources.FormatBindingSource_CannotBeComposite(
                bindingSource.DisplayName,
                nameof(CanAcceptDataFrom));
            throw new ArgumentException(message, nameof(bindingSource));
        }
        
        foreach (var source in BindingSources)
        {
            if (source.CanAcceptDataFrom(bindingSource))
            {
                return true;
            }
        }
        
        return false;
    }
}

```

##### 3.1.6 default property filter provider

```c#
public class DefaultPropertyFilterProvider<TModel> 
    : IPropertyFilterProvider where TModel : class
{
    private static readonly Func<ModelMetadata, bool> _default = (m) => true;
        
    public virtual string Prefix => string.Empty;        
    public virtual IEnumerable<Expression<Func<TModel, object>>>? 
        PropertyIncludeExpressions => null;
    
    /// <inheritdoc />
    public virtual Func<ModelMetadata, bool> PropertyFilter
    {
        get
        {
            if (PropertyIncludeExpressions == null)
            {
                return _default;
            }
            
            // We do not cache by default.
            return GetPropertyFilterFromExpression(PropertyIncludeExpressions);
        }
    }
    
    private Func<ModelMetadata, bool> GetPropertyFilterFromExpression(
        IEnumerable<Expression<Func<TModel, object>>> includeExpressions)
    {
        var expression = ModelBindingHelper
            .GetPropertyFilterExpression(includeExpressions.ToArray());
        
        return expression.Compile();
    }
}

```

model binder helper???

#### 3.2 model metadata

##### 3.2.1 model metadata

```c#
[DebuggerDisplay("{DebuggerToString(),nq}")]
public abstract class ModelMetadata : 
	IEquatable<ModelMetadata?>, 
	IModelMetadataProvider
{        
    public static readonly int DefaultOrder = 10000;    
    private int? _hashCode;                                            
           
    /* 基本属性，与 binding info 相关 */
    public abstract string? BinderModelName { get; }        
    public abstract Type? BinderType { get; }        
    public abstract BindingSource? BindingSource { get; }   
    public abstract IPropertyFilterProvider? PropertyFilterProvider { get; }
        
    public abstract ModelBindingMessageProvider ModelBindingMessageProvider { get; }
    public abstract IReadOnlyDictionary<object, object> AdditionalValues { get; }
        
    /* 显示 */          
    public abstract string? DataTypeName { get; }        
    public abstract string? Description { get; }        
    public abstract string? DisplayFormatString { get; }        
    public abstract string? DisplayName { get; }        
    public abstract string? EditFormatString { get; }
    public abstract string? SimpleDisplayProperty { get; }        
    public abstract string? TemplateHint { get; }
    public abstract string? Placeholder { get; }       
    public abstract string? NullDisplayText { get; }    
                
    public abstract bool ConvertEmptyStringToNull { get; }  
    public abstract bool HasNonDefaultEditFormat { get; }
    public abstract bool HtmlEncode { get; }        
    public abstract bool HideSurroundingHtml { get; }
    // 标记 readonly   
    public abstract bool ShowForDisplay { get; }
    // 标记 edit
    public abstract bool ShowForEdit { get; }

                                
    /* 注入 model metadata identity，从而解析。。。 */   
    protected internal ModelMetadataIdentity Identity { get; }                         
    public Type ModelType => Identity.ModelType;
    public ModelMetadataKind MetadataKind => Identity.MetadataKind;
    // model name，（parameter name 或者 property name，如果是相应 kind）      
    public string? Name => Identity.Name;        
    public string? ParameterName => 
        MetadataKind == ModelMetadataKind.Parameter ? Identity.Name : null;        
    public string? PropertyName => 
        MetadataKind == ModelMetadataKind.Property ? Identity.Name : null;     
    // container type，如果 model 是 property    
    public Type? ContainerType => Identity.ContainerType;    
        
    // 表示 container 的 metadata，如果有 model 表示 property   
    public virtual ModelMetadata? ContainerMetadata
    {
        get
        {
            throw new NotImplementedException();
        }
    }
    // 表示 泛型参数 T 的 metadata，如果 model 实现了 IEnumerable<T> 接口
    public abstract ModelMetadata? ElementMetadata { get; }
    // 表示绑定的 constructor 的 metadata
    public virtual ModelMetadata? BoundConstructor { get; } 
    // 表示 constructor parameter 的 metadata 集合，如果 model 表示 constructor                   
    public virtual IReadOnlyList<ModelMetadata>? BoundConstructorParameters { get; }    
                    
        
    /* property */
    public abstract ModelPropertyCollection Properties { get; }
        
    /* bound property，即包含 constructor parameter 中与 property 不同名的 parameter */        
    private IReadOnlyList<ModelMetadata>? _boundProperties;
    internal IReadOnlyList<ModelMetadata> BoundProperties
    {
        get
        {
            // In record types, each constructor parameter in the primary constructor 
            // is also a settable property with the same name.
            //
            // Executing model binding on these parameters twice may have detrimental effects, 
            // such as duplicate ModelState entries, or failures if a model expects 
            // to be bound exactly ones.
            //
            // Consequently when binding to a constructor, we only bind and validate 
            // the subset of properties whose names haven't appeared as parameters.
            
            // 如果 model 不是 constructor，直接返回 property
            if (BoundConstructor is null)
            {
                return Properties;
            }
            // 如果 model 是 constructor，
            if (_boundProperties is null)
            {
                // 获取 constructor parameter，
                var boundParameters = BoundConstructor.BoundConstructorParameters!;
                var boundProperties = new List<ModelMetadata>();
                // 查找 parameter name 不同于 property name 的，
                foreach (var metadata in Properties)
                {
                    if (!boundParameters.Any(p =>
                        	string.Equals(
                                p.ParameterName, 
                                metadata.PropertyName, 
                                StringComparison.Ordinal) && 
                            p.ModelType == metadata.ModelType))
                    {
                        // 注入 bound properties
                        boundProperties.Add(metadata);
                    }
                }
                
                _boundProperties = boundProperties;
            }
            
            return _boundProperties;
        }
    }
        
    /* for properties */    
    public abstract bool IsBindingAllowed { get; }        
    public abstract bool IsBindingRequired { get; }
    public abstract bool IsReadOnly { get; }       
    public abstract bool IsRequired { get; }                            
    public abstract int Order { get; }    
        
    // 从 model 获取 property
    public abstract Func<object, object?>? PropertyGetter { get; }
    // 设置 model 的 property
    public abstract Action<object, object?>? PropertySetter { get; }
    // 调用 constructor    
    public virtual Func<object?[], object>? BoundConstructorInvoker => null;
        
    
    /* 构造函数 */                                         
    protected ModelMetadata(ModelMetadataIdentity identity)
    {
        // 注入 model metadata identity
        Identity = identity;        
        // 判断 model type 类型并设置标志
        InitializeTypeInformation();
    }                                                                                                                                                                                   
    public string GetDisplayName()
    {
        return DisplayName ?? Name ?? ModelType.Name;
    }
    
    /// <inheritdoc />
    public virtual ModelMetadata GetMetadataForType(Type modelType)
    {
        throw new NotImplementedException();
    }
    
    /// <inheritdoc />
    public virtual IEnumerable<ModelMetadata> GetMetadataForProperties(Type modelType)
    {
        throw new NotImplementedException();
    }
        
    /// <inheritdoc />
    public bool Equals(ModelMetadata? other)
    {
        if (object.ReferenceEquals(this, other))
        {
            return true;
        }
        
        if (other == null)
        {
            return false;
        }
        else
        {
            return Identity.Equals(other.Identity);
        }
    }
    
    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as ModelMetadata);
    }
    
    /// <inheritdoc />
    public override int GetHashCode()
    {
        // Normally caching the hashcode would be dangerous, 
        // but Identity is deeply immutable so this is safe.
        if (_hashCode == null)
        {
            _hashCode = Identity.GetHashCode();
        }
        
        return _hashCode.Value;
    }
            
    private string DebuggerToString()
    {
        switch (MetadataKind)
        {
            case ModelMetadataKind.Parameter:
                return 
                    $"ModelMetadata (Parameter: '{ParameterName}' 
                    "Type: '{ModelType.Name}')";
            case ModelMetadataKind.Property:
                return 
                    $"ModelMetadata (Property: '{ContainerType!.Name}.{PropertyName}' 
                    "Type: '{ModelType.Name}')";
            case ModelMetadataKind.Type:
                return $"ModelMetadata (Type: '{ModelType.Name}')";
            case ModelMetadataKind.Constructor:
                return $"ModelMetadata (Constructor: '{ModelType.Name}')";
            default:
                return $"Unsupported MetadataKind '{MetadataKind}'.";
        }
    }        
}

```

###### 3.2.1.1. 判断 model 类别

```c#
public abstract class ModelMetadata 
{    
    // 复杂类型
    public bool IsComplexType { get; private set; }    
        
    // 可空 值类型 
    public bool IsNullableValueType { get; private set; }   
    // 可空 值类型 的 基础类型，即 nullable<T> 的 T
    public Type UnderlyingOrModelType { get; private set; } = default!;     
        
    // 引用类型（可空）
    public bool IsReferenceOrNullableType { get; private set; }    
           
    // 枚举类型
    public abstract bool IsEnum { get; }        
    // flag 枚举
    public abstract bool IsFlagsEnum { get; }    
    // 分组的枚举值字符串
	public abstract IEnumerable<KeyValuePair<EnumGroupAndName, string>>? 
        EnumGroupedDisplayNamesAndValues { get; }      
    // 枚举值字符串
    public abstract IReadOnlyDictionary<string, string>? EnumNamesAndValues { get; }
        
    // collection 类型，即实现了 ICollection<T>
    public bool IsCollectionType { get; private set; }    
        
    // enumerable 类型，即实现了 IEnumerable<T>
    public bool IsEnumerableType { get; private set; }    
    // IEnumerable<T> 的 T          
    public Type? ElementType { get; private set; }
    
    
    // 判断 model type，设置对应的标志    
    private void InitializeTypeInformation()
    {
        Debug.Assert(ModelType != null);
        
        // model 是 复杂类型
        IsComplexType = 
            !TypeDescriptor
            	.GetConverter(ModelType)
            	.CanConvertFrom(typeof(string));
        
        // model 是 可空值类型，即继承自 nullable<T>
        IsNullableValueType = Nullable.GetUnderlyingType(ModelType) != null;
        
        // model 是 可空引用类型
        IsReferenceOrNullableType = !ModelType.IsValueType || IsNullableValueType;
        
        // 泛型参数中的具体类型（如果是泛型类型，否则是 model type 本身）
        UnderlyingOrModelType = Nullable.GetUnderlyingType(ModelType) ?? ModelType;
        
        // model 是 collection类型，即实现了 ICollection<T>
        var collectionType = ClosedGenericMatcher
            .ExtractGenericInterface(ModelType, typeof(ICollection<>));
        IsCollectionType = collectionType != null;
        
        if (ModelType == typeof(string) || 
            !typeof(IEnumerable).IsAssignableFrom(ModelType))
        {
            // Do nothing, not Enumerable.
        }
        else if (ModelType.IsArray)
        {
            /* model 是 enumerable 类型 */
            IsEnumerableType = true;
            ElementType = ModelType.GetElementType()!;
        }
        else
        {
            /* model 是 enumerable<T> 类型 */
            IsEnumerableType = true;
            
            var enumerableType = 
                ClosedGenericMatcher.ExtractGenericInterface(
                	ModelType, 
                	typeof(IEnumerable<>));
            
            ElementType = enumerableType?.GenericTypeArguments[0]!;
            
            if (ElementType == null)
            {
                // ModelType implements IEnumerable but not IEnumerable<T>.
                ElementType = typeof(object);
            }
            
            Debug.Assert(
                ElementType != null,
                $"Unable to find element type for '{ModelType.FullName}' 
                "though IsEnumerableType is true.");
        }
    }      
}

```

###### 3.2.1.2 设置 parameter - property 映射

```c#
public abstract class ModelMetadata 
{
    private Exception? _recordTypeValidatorsOnPropertiesError;        
    private bool _recordTypeConstructorDetailsCalculated;
    
    /* parameter -> property 映射关系 */
    private IReadOnlyDictionary<ModelMetadata, ModelMetadata>? _parameterMapping;        
    internal IReadOnlyDictionary<ModelMetadata, ModelMetadata> 
        BoundConstructorParameterMapping
    {
        get
        {
            Debug.Assert(
                BoundConstructor != null, 
                "This API can be only called for types with bound constructors.");
            CalculateRecordTypeConstructorDetails();
            
            return _parameterMapping;
        }
    }
    
    /* property -> parameter 映射关系 */
    private IReadOnlyDictionary<ModelMetadata, ModelMetadata>? 
        _boundConstructorPropertyMapping;        
        
    internal IReadOnlyDictionary<ModelMetadata, ModelMetadata> 
        BoundConstructorPropertyMapping
    {
        get
        {
            Debug.Assert(
                BoundConstructor != null, 
                "This API can be only called for types with bound constructors.");
            CalculateRecordTypeConstructorDetails();
            
            return _boundConstructorPropertyMapping;
        }
    }
            
    // 设置 parameter - property 双向映射，
    // 同时验证 record type 没有设置 validation
    [MemberNotNull(
        nameof(_parameterMapping), 
        nameof(_boundConstructorPropertyMapping))]
    private void CalculateRecordTypeConstructorDetails()
    {
        if (_recordTypeConstructorDetailsCalculated)
        {
            Debug.Assert(_parameterMapping != null);
            Debug.Assert(_boundConstructorPropertyMapping != null);
            return;
        }
        
        // 获取 constructor parameter
        var boundParameters = BoundConstructor!.BoundConstructorParameters!;
        /* 创建 parameter - property 映射集合 */
        var parameterMapping = new Dictionary<ModelMetadata, ModelMetadata>();
        var propertyMapping = new Dictionary<ModelMetadata, ModelMetadata>();
        
        // 遍历 constructor parameter，
        foreach (var parameter in boundParameters)
        {
            // 获取 properties 中和 constructor parameter 同名的 property，
            var property = Properties.FirstOrDefault(p =>
                           	   string.Equals(
                                   p.Name, 
                                   parameter.ParameterName, 
                                   StringComparison.Ordinal) &&
                               p.ModelType == parameter.ModelType);
            
            if (property != null)
            {
                /* 将找到的 property 注入 parameter - property 映射集合 */
                parameterMapping[parameter] = property;
                propertyMapping[property] = parameter;
                
                // 注册错误信息
                if (property.PropertyHasValidators)
                {
                    // When constructing the mapping of paramets -> properties, 
                    // also determine if the property has any validators 
                    // (without looking at metadata on the type).
                    // This will help us throw during validation if a user 
                    // defines validation attributes on the property of a record type.
                    _recordTypeValidatorsOnPropertiesError = 
                        new InvalidOperationException(
                        	Resources.FormatRecordTypeHasValidationOnProperties(
                                ModelType, 
                                property.Name));
                }
            }
        }
        
        // 设置 constructor details calculated 标志位
        _recordTypeConstructorDetailsCalculated = true;
        
        /* 赋值 parameter mapping & property mapping */
        _parameterMapping = parameterMapping;
        _boundConstructorPropertyMapping = propertyMapping;
    }
}

```



###### 3.2.1.3 验证 property 相关

```c#
public abstract class ModelMetadata 
{ 
    public virtual IPropertyValidationFilter? PropertyValidationFilter => null;   
    public abstract IReadOnlyList<object> ValidatorMetadata { get; }
    
    public abstract bool ValidateChildren { get; }        
    public virtual bool? HasValidators { get; }        
    internal virtual bool PropertyHasValidators => false;
    
    
      
    
    
    
    internal void ThrowIfRecordTypeHasValidationOnProperties()
    {
        CalculateRecordTypeConstructorDetails();
        if (_recordTypeValidatorsOnPropertiesError != null)
        {
            throw _recordTypeValidatorsOnPropertiesError;
        }
    }
    
    
```

##### 3.2.2 model metadata 组件

###### 3.2.2.1 model metadata identity

```c#
public readonly struct ModelMetadataIdentity : IEquatable<ModelMetadataIdentity>
{
    /* 静态单例方法 */
    
    // for type
    public static ModelMetadataIdentity ForType(Type modelType)
    {
        if (modelType == null)
        {
            throw new ArgumentNullException(nameof(modelType));
        }
        
        return new ModelMetadataIdentity(modelType);
    }
    // for constructor
    public static ModelMetadataIdentity ForConstructor(
        ConstructorInfo constructor, 
        Type modelType)
    {
        if (constructor == null)
        {
            throw new ArgumentNullException(nameof(constructor));
        }        
        if (modelType == null)
        {
            throw new ArgumentNullException(nameof(modelType));
        }
        
        return new ModelMetadataIdentity(
            modelType, 
            constructor.Name, 
            constructorInfo: constructor);
    }
    // for property
    public static ModelMetadataIdentity ForProperty(
        PropertyInfo propertyInfo,
        Type modelType,
        Type containerType)
    {
        if (propertyInfo == null)
        {
            throw new ArgumentNullException(nameof(propertyInfo));
        }        
        if (modelType == null)
        {
            throw new ArgumentNullException(nameof(modelType));
        }        
        if (containerType == null)
        {
            throw new ArgumentNullException(nameof(containerType));
        }
        
        return new ModelMetadataIdentity(
            modelType, 
            propertyInfo.Name, 
            containerType, 
            fieldInfo: propertyInfo);
    }
    // for parameter
    public static ModelMetadataIdentity ForParameter(ParameterInfo parameter) => 
        ForParameter(parameter, parameter.ParameterType);    
    public static ModelMetadataIdentity ForParameter(
        ParameterInfo parameter, 
        Type modelType)
    {
        if (parameter == null)
        {
            throw new ArgumentNullException(nameof(parameter));
        }        
        if (modelType == null)
        {
            throw new ArgumentNullException(nameof(modelType));
        }
        
        return new ModelMetadataIdentity(
            modelType, 
            parameter.Name, 
            fieldInfo: parameter);
    }
    
    /* end of 静态方法 */
         
    // 如果 model 表示 property，container type 表示 property 隶属的类的 type，
    // 如果 model 表示 type，container type 为 null
    public Type? ContainerType { get; }
    // model type    
    public Type ModelType { get; }
    // model kind，枚举
    // type, property, parameter, constructor
    public ModelMetadataKind MetadataKind
    {
        get
        {
            if (ParameterInfo != null)
            {
                return ModelMetadataKind.Parameter;
            }
            else if (ConstructorInfo != null)
            {
                return ModelMetadataKind.Constructor;
            }
            else if (ContainerType != null && Name != null)
            {
                return ModelMetadataKind.Property;
            }
            else
            {
                return ModelMetadataKind.Type;
            }
        }
    }        
    // property name，如果 model 不是 property，name = null      
    public string? Name { get; }    
    // field
    private object? FieldInfo { get; }        
    // parameter info
    public ParameterInfo? ParameterInfo => FieldInfo as ParameterInfo;       
    // property info
    public PropertyInfo? PropertyInfo => FieldInfo as PropertyInfo;   
    // constructor info
    public ConstructorInfo? ConstructorInfo { get; }

    // 私有的构造函数
    private ModelMetadataIdentity(
        Type modelType,
        string? name = null,
        Type? containerType = null,
        object? fieldInfo = null,
        ConstructorInfo? constructorInfo = null)
    {
        ModelType = modelType;
        Name = name;
        ContainerType = containerType;
        FieldInfo = fieldInfo;
        ConstructorInfo = constructorInfo;
    }
                                                                       
    /// <inheritdoc />
    public bool Equals(ModelMetadataIdentity other)
    {
        return
            ContainerType == other.ContainerType &&
            ModelType == other.ModelType &&
            Name == other.Name &&
            ParameterInfo == other.ParameterInfo &&
            PropertyInfo == other.PropertyInfo &&
            ConstructorInfo == other.ConstructorInfo;
    }
    
    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        var other = obj as ModelMetadataIdentity?;
        return other.HasValue && Equals(other.Value);
    }
    
    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ContainerType);
        hash.Add(ModelType);
        hash.Add(Name, StringComparer.Ordinal);
        hash.Add(ParameterInfo);
        hash.Add(PropertyInfo);
        hash.Add(ConstructorInfo);
        return hash.ToHashCode();
    }
}

```

###### 3.2.2.2 model binding message provider

```c#
public abstract class ModelBindingMessageProvider
{        
    public virtual Func<string, string> 
        MissingBindRequiredValueAccessor { get; } = default!;   
    public virtual Func<string> 
        MissingKeyOrValueAccessor { get; } = default!;        
    public virtual Func<string> 
        MissingRequestBodyRequiredValueAccessor { get; } = default!;   
    public virtual Func<string, string> 
        ValueMustNotBeNullAccessor { get; } = default!
    public virtual Func<string, string, string> 
        AttemptedValueIsInvalidAccessor { get; } = default!;
    public virtual Func<string, string> 
        NonPropertyAttemptedValueIsInvalidAccessor { get; } = default!;        
    public virtual Func<string, string> 
        UnknownValueIsInvalidAccessor { get; } = default!;     
    public virtual Func<string> 
        NonPropertyUnknownValueIsInvalidAccessor { get; } = default!;   
    public virtual Func<string, string> 
        ValueIsInvalidAccessor { get; } = default!;        
    public virtual Func<string, string> 
        ValueMustBeANumberAccessor { get; } = default!;        
    public virtual Func<string> 
        NonPropertyValueMustBeANumberAccessor { get; } = default!;
}

```

##### 3.2.3 default model metadata

```c#
public class DefaultModelMetadata : ModelMetadata
{
    private readonly IModelMetadataProvider _provider;
    private readonly ICompositeMetadataDetailsProvider _detailsProvider;
    private readonly DefaultMetadataDetails _details;
    
    // Default message provider for all DefaultModelMetadata instances; 
    // cloned before exposing to IBindingMetadataProvider instances to ensure 
    // customizations are not accidentally shared.
    private readonly DefaultModelBindingMessageProvider _modelBindingMessageProvider;
    
    private ReadOnlyDictionary<object, object>? _additionalValues;
    private ModelMetadata? _elementMetadata;
    private ModelMetadata? _constructorMetadata;
    private bool? _isBindingRequired;
    private bool? _isReadOnly;
    private bool? _isRequired;
    private ModelPropertyCollection? _properties;
    private bool? _validateChildren;
    private bool? _hasValidators;
    private ReadOnlyCollection<object>? _validatorMetadata;
        
    public DefaultModelMetadata(
        IModelMetadataProvider provider,
        ICompositeMetadataDetailsProvider detailsProvider,
        DefaultMetadataDetails details)
        	: this(
                provider, 
                detailsProvider, 
                details, 
                new DefaultModelBindingMessageProvider())
    {
    }
        
    public DefaultModelMetadata(
        IModelMetadataProvider provider,
        ICompositeMetadataDetailsProvider detailsProvider,
        DefaultMetadataDetails details,
        DefaultModelBindingMessageProvider modelBindingMessageProvider)
        	: base(details.Key)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }                
        if (detailsProvider == null)
        {
            throw new ArgumentNullException(nameof(detailsProvider));
        }
        if (details == null)
        {
            throw new ArgumentNullException(nameof(details));
        }
        if (modelBindingMessageProvider == null)
        {
            throw new ArgumentNullException(nameof(modelBindingMessageProvider));
        }

        _provider = provider;
        _detailsProvider = detailsProvider;
        _details = details;
        _modelBindingMessageProvider = modelBindingMessageProvider;
    }

        
    public ModelAttributes Attributes => _details.ModelAttributes;
    
    /// <inheritdoc />
    public override ModelMetadata? ContainerMetadata => _details.ContainerMetadata;

        /// <summary>
        /// Gets the <see cref="Metadata.BindingMetadata"/> for the current instance.
        /// </summary>
        /// <remarks>
        /// Accessing this property will populate the <see cref="Metadata.BindingMetadata"/> if necessary.
        /// </remarks>
    public BindingMetadata BindingMetadata
    {
        get
        {
            if (_details.BindingMetadata == null)
            {
                var context = 
                    new BindingMetadataProviderContext(
                    	Identity, 
                    	_details.ModelAttributes);
                
                // Provide a unique ModelBindingMessageProvider instance 
                // so providers' customizations are per-type.
                context
                    .BindingMetadata
                    .ModelBindingMessageProvider =
                    	new DefaultModelBindingMessageProvider(_modelBindingMessageProvider);
                
                _detailsProvider.CreateBindingMetadata(context);
                _details.BindingMetadata = context.BindingMetadata;
            }
            
            return _details.BindingMetadata;
        }
    }
    /// <summary>
        /// Gets the <see cref="Metadata.DisplayMetadata"/> for the current instance.
        /// </summary>
        /// <remarks>
        /// Accessing this property will populate the <see cref="Metadata.DisplayMetadata"/> if necessary.
        /// </remarks>
    public DisplayMetadata DisplayMetadata
    {
        get
        {
            if (_details.DisplayMetadata == null)
            {
                var context = 
                    new DisplayMetadataProviderContext(
                    	Identity, 
                    	_details.ModelAttributes);
                _detailsProvider.CreateDisplayMetadata(context);
                _details.DisplayMetadata = context.DisplayMetadata;
            }
            
            return _details.DisplayMetadata;
        }
    }
    
    /// <summary>
        /// Gets the <see cref="Metadata.ValidationMetadata"/> for the current instance.
        /// </summary>
        /// <remarks>
        /// Accessing this property will populate the <see cref="Metadata.ValidationMetadata"/> if necessary.
        /// </remarks>
    public ValidationMetadata ValidationMetadata
    {
        get
        {
            if (_details.ValidationMetadata == null)
            {
                var context = 
                    new ValidationMetadataProviderContext(
                    	Identity, 
                    	_details.ModelAttributes);
                _detailsProvider.CreateValidationMetadata(context);
                _details.ValidationMetadata = context.ValidationMetadata;
            }
            
            return _details.ValidationMetadata;
        }
    }
    
    /// <inheritdoc />
    public override IReadOnlyDictionary<object, object> AdditionalValues
    {
        get
        {
            if (_additionalValues == null)
            {
                _additionalValues = 
                    new ReadOnlyDictionary<object, object>(DisplayMetadata.AdditionalValues);
            }
            
            return _additionalValues;
        }
    }
    
    /// <inheritdoc />
    public override BindingSource? BindingSource => BindingMetadata.BindingSource;
    
    /// <inheritdoc />
    public override string? BinderModelName => BindingMetadata.BinderModelName;
    
    /// <inheritdoc />
    public override Type? BinderType => BindingMetadata.BinderType;
    
    /// <inheritdoc />
    public override bool ConvertEmptyStringToNull => DisplayMetadata.ConvertEmptyStringToNull;
    
    /// <inheritdoc />
    public override string? DataTypeName => DisplayMetadata.DataTypeName;
    
    /// <inheritdoc />
    public override string? Description
    {
        get
        {
            if (DisplayMetadata.Description == null)
            {
                return null;
            }
            
            return DisplayMetadata.Description();
        }
    }
    
    /// <inheritdoc />
    public override string? DisplayFormatString => DisplayMetadata.DisplayFormatStringProvider();
    
    /// <inheritdoc />
    public override string? DisplayName
    {
        get
        {
            if (DisplayMetadata.DisplayName == null)
            {
                return null;
            }
            
            return DisplayMetadata.DisplayName();
        }
    }
    
    /// <inheritdoc />
    public override string? EditFormatString => DisplayMetadata.EditFormatStringProvider();
    
    /// <inheritdoc />
    public override ModelMetadata? ElementMetadata
    {
        get
        {
            if (_elementMetadata == null && ElementType != null)
            {
                _elementMetadata = _provider.GetMetadataForType(ElementType);
            }
            
            return _elementMetadata;
        }
    }
    
    /// <inheritdoc />
    public override IEnumerable<KeyValuePair<EnumGroupAndName, string>>? EnumGroupedDisplayNamesAndValues
        => DisplayMetadata.EnumGroupedDisplayNamesAndValues;
    
    /// <inheritdoc />
    public override IReadOnlyDictionary<string, string>? EnumNamesAndValues => DisplayMetadata.EnumNamesAndValues;
    
    /// <inheritdoc />
    public override bool HasNonDefaultEditFormat => DisplayMetadata.HasNonDefaultEditFormat;
    
    /// <inheritdoc />
    public override bool HideSurroundingHtml => DisplayMetadata.HideSurroundingHtml;
    
    /// <inheritdoc />
    public override bool HtmlEncode => DisplayMetadata.HtmlEncode;
    
    /// <inheritdoc />
    public override bool IsBindingAllowed
    {
        get
        {
            if (MetadataKind == ModelMetadataKind.Type)
            {
                return true;
            }
            else
            {
                return BindingMetadata.IsBindingAllowed;
            }
        }
    }
    
    /// <inheritdoc />
    public override bool IsBindingRequired
    {
        get
        {
            if (!_isBindingRequired.HasValue)
            {
                if (MetadataKind == ModelMetadataKind.Type)
                {
                    _isBindingRequired = false;
                }
                else
                {
                    _isBindingRequired = BindingMetadata.IsBindingRequired;
                }
            }
            
            return _isBindingRequired.Value;
        }
    }
    
    /// <inheritdoc />
    public override bool IsEnum => DisplayMetadata.IsEnum;
    
    /// <inheritdoc />
    public override bool IsFlagsEnum => DisplayMetadata.IsFlagsEnum;
    
    /// <inheritdoc />
    public override bool IsReadOnly
    {
        get
        {
            if (!_isReadOnly.HasValue)
            {
                if (MetadataKind == ModelMetadataKind.Type)
                {
                    _isReadOnly = false;
                }
                else if (BindingMetadata.IsReadOnly.HasValue)
                {
                    _isReadOnly = BindingMetadata.IsReadOnly;
                }
                else
                {
                    _isReadOnly = _details.PropertySetter == null;
                }
            }
            
            return _isReadOnly.Value;
        }
    }
    
    /// <inheritdoc />
    public override bool IsRequired
    {
        get
        {
            if (!_isRequired.HasValue)
            {
                if (ValidationMetadata.IsRequired.HasValue)
                {
                    _isRequired = ValidationMetadata.IsRequired;
                }
                else
                {
                    // Default to IsRequired = true for non-Nullable<T> value types.
                    _isRequired = !IsReferenceOrNullableType;
                }
            }
            
            return _isRequired.Value;
        }
    }
    
    /// <inheritdoc />
    public override ModelBindingMessageProvider ModelBindingMessageProvider =>            BindingMetadata.ModelBindingMessageProvider!;
    
    /// <inheritdoc />
    public override string? NullDisplayText => DisplayMetadata.NullDisplayTextProvider();
    
    /// <inheritdoc />
    public override int Order => DisplayMetadata.Order;
    
    /// <inheritdoc />
    public override string? Placeholder
    {
        get
        {
            if (DisplayMetadata.Placeholder == null)
            {
                return null;
            }
            
            return DisplayMetadata.Placeholder();
        }
    }
    
    /// <inheritdoc />
    public override ModelPropertyCollection Properties
    {
        get
        {
            if (_properties == null)
            {
                var properties = _provider.GetMetadataForProperties(ModelType);
                properties = properties.OrderBy(p => p.Order);
                _properties = new ModelPropertyCollection(properties);
            }
            
            return _properties;
        }
    }
    
    /// <inheritdoc />
    public override ModelMetadata? BoundConstructor
    {
        get
        {
            if (BindingMetadata.BoundConstructor == null)
            {
                return null;
            }
            
            if (_constructorMetadata == null)
            {
                var modelMetadataProvider = (ModelMetadataProvider)_provider;
                _constructorMetadata = modelMetadataProvider.GetMetadataForConstructor(BindingMetadata.BoundConstructor, ModelType);
            }
            
            return _constructorMetadata;
        }
    }
    
    /// <inheritdoc/>
    public override IReadOnlyList<ModelMetadata>? BoundConstructorParameters => _details.BoundConstructorParameters;
    
    /// <inheritdoc />
    public override IPropertyFilterProvider? PropertyFilterProvider => BindingMetadata.PropertyFilterProvider;
    
    /// <inheritdoc />
    public override bool ShowForDisplay => DisplayMetadata.ShowForDisplay;
    
    /// <inheritdoc />
    public override bool ShowForEdit => DisplayMetadata.ShowForEdit;
    /// <inheritdoc />
    public override string? SimpleDisplayProperty => DisplayMetadata.SimpleDisplayProperty;
    
    /// <inheritdoc />
    public override string? TemplateHint => DisplayMetadata.TemplateHint;
    
    /// <inheritdoc />
    public override IPropertyValidationFilter? PropertyValidationFilter => ValidationMetadata.PropertyValidationFilter;
    
    /// <inheritdoc />
    public override bool ValidateChildren
    {
        get
        {
            if (!_validateChildren.HasValue)
            {
                if (ValidationMetadata.ValidateChildren.HasValue)
                {
                    _validateChildren = ValidationMetadata.ValidateChildren.Value;
                }
                else if (IsComplexType || IsEnumerableType)
                {
                    _validateChildren = true;
                }
                else
                {
                    _validateChildren = false;
                }
            }
            
            return _validateChildren.Value;
        }
    }
    
    /// <inheritdoc />
    public override bool? HasValidators
    {
        get
        {
            if (!_hasValidators.HasValue)
            {
                var visited = new HashSet<DefaultModelMetadata>();
                
                _hasValidators = CalculateHasValidators(visited, this);
            }
            
            return _hasValidators.Value;
        }
    }
    
    internal override bool PropertyHasValidators => ValidationMetadata.PropertyHasValidators;
    
    internal static bool CalculateHasValidators(HashSet<DefaultModelMetadata> visited, ModelMetadata metadata)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        
        if (metadata?.GetType() != typeof(DefaultModelMetadata))
        {
            
            // The calculation is valid only for DefaultModelMetadata instances. Null, other ModelMetadata instances
            // or subtypes of DefaultModelMetadata will be treated as always requiring validation.
            return true;
        }
        
        var defaultModelMetadata = (DefaultModelMetadata)metadata;
        
        if (defaultModelMetadata._hasValidators.HasValue)
        {
            // Return a previously calculated value if available.
            return defaultModelMetadata._hasValidators.Value;
        }
        
        if (defaultModelMetadata.ValidationMetadata.HasValidators != false)
        {
            // Either the ModelMetadata instance has some validators (HasValidators = true) or it is non-deterministic (HasValidators = null).
            // In either case, assume it has validators.
            return true;
        }
        
        // Before inspecting properties or elements of a collection, ensure we do not have a cycle.
            // Consider a model like so
            //
            // Employee { BusinessDivision Division; int Id; string Name; }
            // BusinessDivision { int Id; List<Employee> Employees }
            //
            // If we get to the Employee element from Employee.Division.Employees, we can return false for that instance
            // and allow other properties of BusinessDivision and Employee to determine if it has validators.
        if (!visited.Add(defaultModelMetadata))
        {
            return false;
        }
        
        // We have inspected the current element. Inspect properties or elements that may contribute to this value.
        if (defaultModelMetadata.IsEnumerableType)
        {
            if (CalculateHasValidators(visited, defaultModelMetadata.ElementMetadata!))
            {
                return true;
            }
        }
        else if (defaultModelMetadata.IsComplexType)
        {
            var parameters = defaultModelMetadata.BoundConstructor?.BoundConstructorParameters ?? Array.Empty<ModelMetadata>();
            foreach (var parameter in parameters)
            {
                if (CalculateHasValidators(visited, parameter))
                {
                    return true;
                }
            }
            
            foreach (var property in defaultModelMetadata.BoundProperties)
            {
                if (CalculateHasValidators(visited, property))
                {
                    return true;
                }
            }
        }
        
        // We've come this far. The ModelMetadata does not have any validation
        return false;
    }
    
    /// <inheritdoc />
    public override IReadOnlyList<object> ValidatorMetadata
    {
        get
        {
            if (_validatorMetadata == null)
            {
                _validatorMetadata = new ReadOnlyCollection<object>(ValidationMetadata.ValidatorMetadata);
            }
            
            return _validatorMetadata;
        }
    }
    
    /// <inheritdoc />
    public override Func<object, object?>? PropertyGetter => _details.PropertyGetter;
    
    /// <inheritdoc />
    public override Action<object, object?>? PropertySetter => _details.PropertySetter;
    
    /// <inheritdoc/>
    public override Func<object?[], object>? BoundConstructorInvoker => _details.BoundConstructorInvoker;
    
    internal DefaultMetadataDetails Details => _details;
    
    /// <inheritdoc />
    public override ModelMetadata GetMetadataForType(Type modelType)
    {
        return _provider.GetMetadataForType(modelType);
    }
    
    /// <inheritdoc />
    public override IEnumerable<ModelMetadata> GetMetadataForProperties(Type modelType)
    {
        return _provider.GetMetadataForProperties(modelType);
    }
}

```

###### 3.2.3.1 default model binding message provider

```c#
public class DefaultModelBindingMessageProvider : ModelBindingMessageProvider
{                                                                                            
    public DefaultModelBindingMessageProvider()
    {
        // 1 -
        SetMissingBindRequiredValueAccessor(
            Resources.FormatModelBinding_MissingBindRequiredMember);
        // 2 -
        SetMissingKeyOrValueAccessor(() => 
            Resources.KeyValuePair_BothKeyAndValueMustBePresent);
        // 3 -
        SetMissingRequestBodyRequiredValueAccessor(() => 
            Resources.ModelBinding_MissingRequestBodyRequiredMember);
        // 4 -
        SetValueMustNotBeNullAccessor(
            Resources.FormatModelBinding_NullValueNotValid);
        // 5 -   
        SetAttemptedValueIsInvalidAccessor(
            Resources.FormatModelState_AttemptedValueIsInvalid);
        // 6 -
		SetNonPropertyAttemptedValueIsInvalidAccessor(
            Resources.FormatModelState_NonPropertyAttemptedValueIsInvalid);
        // 7 -
        SetUnknownValueIsInvalidAccessor(
            Resources.FormatModelState_UnknownValueIsInvalid);
        // 8 -
        SetNonPropertyUnknownValueIsInvalidAccessor(() => 
            Resources.ModelState_NonPropertyUnknownValueIsInvalid);
        // 9 -
        SetValueIsInvalidAccessor(
            Resources.FormatHtmlGeneration_ValueIsInvalid);
        // 10 -
        SetValueMustBeANumberAccessor(
            Resources.FormatHtmlGeneration_ValueMustBeNumber);
        // 11 -
        SetNonPropertyValueMustBeANumberAccessor(() => 
            Resources.HtmlGeneration_NonPropertyValueMustBeNumber);
    }
        
    public DefaultModelBindingMessageProvider
        (DefaultModelBindingMessageProvider originalProvider)
    {
        if (originalProvider == null)
        {
            throw new ArgumentNullException(nameof(originalProvider));
        }
        // 1 -
        SetMissingBindRequiredValueAccessor(
            originalProvider.MissingBindRequiredValueAccessor);
        // 2 -
        SetMissingKeyOrValueAccessor(
            originalProvider.MissingKeyOrValueAccessor);
        // 3 -
        SetMissingRequestBodyRequiredValueAccessor(
            originalProvider.MissingRequestBodyRequiredValueAccessor);
        // 4 -
        SetValueMustNotBeNullAccessor(
            originalProvider.ValueMustNotBeNullAccessor);
        // 5 -
        SetAttemptedValueIsInvalidAccessor(
            originalProvider.AttemptedValueIsInvalidAccessor);
        // 6 -
        SetNonPropertyAttemptedValueIsInvalidAccessor(
            originalProvider.NonPropertyAttemptedValueIsInvalidAccessor);
        // 7 -
        SetUnknownValueIsInvalidAccessor(
            originalProvider.UnknownValueIsInvalidAccessor);
        // 8 - 
        SetNonPropertyUnknownValueIsInvalidAccessor(
            originalProvider.NonPropertyUnknownValueIsInvalidAccessor);
        // 9 -
        SetValueIsInvalidAccessor(
            originalProvider.ValueIsInvalidAccessor);
        // 10 -
        SetValueMustBeANumberAccessor(
            originalProvider.ValueMustBeANumberAccessor);
        // 11 -
        SetNonPropertyValueMustBeANumberAccessor(
            originalProvider.NonPropertyValueMustBeANumberAccessor);
    }
    
    /* 1 - */
    private Func<string, string> _missingBindRequiredValueAccessor;
    
    public override Func<string, string> MissingBindRequiredValueAccessor => 
        _missingBindRequiredValueAccessor;
           
    [MemberNotNull(nameof(_missingBindRequiredValueAccessor))]
    public void SetMissingBindRequiredValueAccessor(
        Func<string, string> missingBindRequiredValueAccessor)
    {
        if (missingBindRequiredValueAccessor == null)
        {
            throw new ArgumentNullException(
                nameof(missingBindRequiredValueAccessor));
        }
        
        _missingBindRequiredValueAccessor = missingBindRequiredValueAccessor;
    }
    
    /* 2 - */
    private Func<string> _missingKeyOrValueAccessor;
    
    public override Func<string> MissingKeyOrValueAccessor => _missingKeyOrValueAccessor;
        
    [MemberNotNull(nameof(_missingKeyOrValueAccessor))]
    public void SetMissingKeyOrValueAccessor(Func<string> missingKeyOrValueAccessor)
    {
        if (missingKeyOrValueAccessor == null)
        {
            throw new ArgumentNullException(
                nameof(missingKeyOrValueAccessor));
        }
        
        _missingKeyOrValueAccessor = missingKeyOrValueAccessor;
    }
    
    /* 3 - */
    private Func<string> _missingRequestBodyRequiredValueAccessor;
    
    public override Func<string> MissingRequestBodyRequiredValueAccessor => 
        _missingRequestBodyRequiredValueAccessor;
            
    [MemberNotNull(nameof(_missingRequestBodyRequiredValueAccessor))]
    public void SetMissingRequestBodyRequiredValueAccessor(
        Func<string> missingRequestBodyRequiredValueAccessor)
    {
        if (missingRequestBodyRequiredValueAccessor == null)
        {
            throw new ArgumentNullException(
                nameof(missingRequestBodyRequiredValueAccessor));
        }
        
        _missingRequestBodyRequiredValueAccessor = missingRequestBodyRequiredValueAccessor;
    }
    
    /* 4 - */
    private Func<string, string> _valueMustNotBeNullAccessor;
    
    public override Func<string, string> ValueMustNotBeNullAccessor => 
        _valueMustNotBeNullAccessor;
        
    [MemberNotNull(nameof(_valueMustNotBeNullAccessor))]
    public void SetValueMustNotBeNullAccessor(
        Func<string, string> valueMustNotBeNullAccessor)
    {
        if (valueMustNotBeNullAccessor == null)
        {
            throw new ArgumentNullException(
                nameof(valueMustNotBeNullAccessor));
        }
        
        _valueMustNotBeNullAccessor = valueMustNotBeNullAccessor;
    }
        
    /* 5 - */
    private Func<string, string, string> _attemptedValueIsInvalidAccessor;
    
    public override Func<string, string, string> AttemptedValueIsInvalidAccessor => 
        _attemptedValueIsInvalidAccessor;
            
    [MemberNotNull(nameof(_attemptedValueIsInvalidAccessor))]
    public void SetAttemptedValueIsInvalidAccessor(
        Func<string, string, string> attemptedValueIsInvalidAccessor)
    {
        if (attemptedValueIsInvalidAccessor == null)
        {
            throw new ArgumentNullException(
                nameof(attemptedValueIsInvalidAccessor));
        }
        
        _attemptedValueIsInvalidAccessor = attemptedValueIsInvalidAccessor;
    }
    
    /* 6 - */
    private Func<string, string> _nonPropertyAttemptedValueIsInvalidAccessor;
    
    public override Func<string, string> NonPropertyAttemptedValueIsInvalidAccessor => 
        _nonPropertyAttemptedValueIsInvalidAccessor;
        
    [MemberNotNull(nameof(_nonPropertyAttemptedValueIsInvalidAccessor))]
    public void SetNonPropertyAttemptedValueIsInvalidAccessor(
        Func<string, string> nonPropertyAttemptedValueIsInvalidAccessor)
    {
        if (nonPropertyAttemptedValueIsInvalidAccessor == null)
        {
            throw new ArgumentNullException(
                nameof(nonPropertyAttemptedValueIsInvalidAccessor));
        }
        
        _nonPropertyAttemptedValueIsInvalidAccessor = 
            nonPropertyAttemptedValueIsInvalidAccessor;
    }
    
    /* 7 - */
    private Func<string, string> _unknownValueIsInvalidAccessor;
    
    public override Func<string, string> UnknownValueIsInvalidAccessor => 
        _unknownValueIsInvalidAccessor;
        
    [MemberNotNull(nameof(_unknownValueIsInvalidAccessor))]
    public void SetUnknownValueIsInvalidAccessor(
        Func<string, string> unknownValueIsInvalidAccessor)
    {
        if (unknownValueIsInvalidAccessor == null)
        {
            throw new ArgumentNullException(
                nameof(unknownValueIsInvalidAccessor));
        }
        
        _unknownValueIsInvalidAccessor = unknownValueIsInvalidAccessor;
    }
    
    /* 8 - */
    private Func<string> _nonPropertyUnknownValueIsInvalidAccessor;
    
    public override Func<string> NonPropertyUnknownValueIsInvalidAccessor => 
        _nonPropertyUnknownValueIsInvalidAccessor;
        
    [MemberNotNull(nameof(_nonPropertyUnknownValueIsInvalidAccessor))]
    public void SetNonPropertyUnknownValueIsInvalidAccessor(
        Func<string> nonPropertyUnknownValueIsInvalidAccessor)
    {
        if (nonPropertyUnknownValueIsInvalidAccessor == null)
        {
            throw new ArgumentNullException(
                nameof(nonPropertyUnknownValueIsInvalidAccessor));
        }
        
        _nonPropertyUnknownValueIsInvalidAccessor = 
            nonPropertyUnknownValueIsInvalidAccessor;
    }
    
    /* 9 - */
    private Func<string, string> _valueIsInvalidAccessor;
    
    public override Func<string, string> ValueIsInvalidAccessor => _valueIsInvalidAccessor;
            
    [MemberNotNull(nameof(_valueIsInvalidAccessor))]
    public void SetValueIsInvalidAccessor(
        Func<string, string> valueIsInvalidAccessor)
    {
        if (valueIsInvalidAccessor == null)
        {
            throw new ArgumentNullException(
                nameof(valueIsInvalidAccessor));
        }
        
        _valueIsInvalidAccessor = valueIsInvalidAccessor;
    }
    
    /* 10 - */
    private Func<string, string> _valueMustBeANumberAccessor;
    
    public override Func<string, string> ValueMustBeANumberAccessor => 
        _valueMustBeANumberAccessor;
        
    [MemberNotNull(nameof(_valueMustBeANumberAccessor))]
    public void SetValueMustBeANumberAccessor(
        Func<string, string> valueMustBeANumberAccessor)
    {
        if (valueMustBeANumberAccessor == null)
        {
            throw new ArgumentNullException(
                nameof(valueMustBeANumberAccessor));
        }
        
        _valueMustBeANumberAccessor = valueMustBeANumberAccessor;
    }
    
    /* 11 - */
    private Func<string> _nonPropertyValueMustBeANumberAccessor;
    
    public override Func<string> NonPropertyValueMustBeANumberAccessor => 
        _nonPropertyValueMustBeANumberAccessor;

    [MemberNotNull(nameof(_nonPropertyValueMustBeANumberAccessor))]
    public void SetNonPropertyValueMustBeANumberAccessor(
        Func<string> nonPropertyValueMustBeANumberAccessor)
    {
        if (nonPropertyValueMustBeANumberAccessor == null)
        {
            throw new ArgumentNullException(
                nameof(nonPropertyValueMustBeANumberAccessor));
        }
        
        _nonPropertyValueMustBeANumberAccessor = nonPropertyValueMustBeANumberAccessor;
    }
}

```

##### 3.2.4 metadata details

###### 3.2.4.1 default metadata details

```c#
public class DefaultMetadataDetails
{    
    public ModelMetadata? ContainerMetadata { get; set; }
    public BindingMetadata? BindingMetadata { get; set; }        
    public DisplayMetadata? DisplayMetadata { get; set; }
	public ValidationMetadata? ValidationMetadata { get; set; }        
                        
    public ModelMetadata[]? Properties { get; set; }        
    public ModelMetadata[]? BoundConstructorParameters { get; set; }
        
    public Func<object, object?>? PropertyGetter { get; set; }        
    public Action<object, object?>? PropertySetter { get; set; }        
    public Func<object?[], object>? BoundConstructorInvoker { get; set; }

    public ModelAttributes ModelAttributes { get; }  
    public ModelMetadataIdentity Key { get; }
       
    // 构造函数，注入 attributes 和 model metadata identity
    public DefaultMetadataDetails(
        ModelMetadataIdentity key, 
        ModelAttributes attributes)
    {
        if (attributes == null)
        {
            throw new ArgumentNullException(nameof(attributes));
        }
        
        Key = key;
        ModelAttributes = attributes;
    }                
}

```

###### 3.2.4.2 metadata details provider 接口

```c#
public interface IMetadataDetailsProvider
{
}

```

###### 3.2.4.3 metadata details provider 接口扩展

```c#
public static class MetadataDetailsProviderExtensions
{    
    public static void RemoveType<TMetadataDetailsProvider>(
        this IList<IMetadataDetailsProvider> list) 
        	where TMetadataDetailsProvider : IMetadataDetailsProvider
    {
        if (list == null)
        {
            throw new ArgumentNullException(nameof(list));
        }
        
        RemoveType(
            list, 
            typeof(TMetadataDetailsProvider));
    }
        
    public static void RemoveType(
        this IList<IMetadataDetailsProvider> list, 
        Type type)
    {
        if (list == null)
        {
            throw new ArgumentNullException(nameof(list));
        }        
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }
        
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var metadataDetailsProvider = list[i];
            if (metadataDetailsProvider.GetType() == type)
            {
                list.RemoveAt(i);
            }
        }
    }
}

```

###### 3.2.4.4 composite metadata details provider 接口

```c#
public interface ICompositeMetadataDetailsProvider : 
	IBindingMetadataProvider, 
	IDisplayMetadataProvider, 
	IValidationMetadataProvider
{
}
```

###### 3.2.4.5 default composite metadata details provider

```c#
internal class DefaultCompositeMetadataDetailsProvider : ICompositeMetadataDetailsProvider
{
    // 注入 metadata details provider 集合
    private readonly IEnumerable<IMetadataDetailsProvider> _providers;        
    public DefaultCompositeMetadataDetailsProvider(
        IEnumerable<IMetadataDetailsProvider> providers)
    {
        _providers = providers;
    }
    
    /// <inheritdoc />
    public void CreateBindingMetadata(
        BindingMetadataProviderContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        foreach (var provider in 
                 _providers.OfType<IBindingMetadataProvider>())
        {
            provider.CreateBindingMetadata(context);
        }
    }    
    /// <inheritdoc />
    public void CreateDisplayMetadata(
        DisplayMetadataProviderContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        foreach (var provider in 
                 _providers.OfType<IDisplayMetadataProvider>())
        {
            provider.CreateDisplayMetadata(context);
        }
    }    
    /// <inheritdoc />
    public void CreateValidationMetadata(
        ValidationMetadataProviderContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        foreach (var provider in 
                 _providers.OfType<IValidationMetadataProvider>())
        {
            provider.CreateValidationMetadata(context);
        }
    }
}

```

##### 3.2.4 binding metadata

```c#
public class BindingMetadata
{
    // binder type
    private Type? _binderType;  
    public Type? BinderType
    {
        get => _binderType;
        set
        {
            if (value != null && !typeof(IModelBinder).IsAssignableFrom(value))
            {
                throw new ArgumentException(
                    Resources.FormatBinderType_MustBeIModelBinder(
                        value.FullName,
                        typeof(IModelBinder).FullName),
                    nameof(value));
            }
            
            _binderType = value;
        }
    }
    
    // model binding message provider
    private DefaultModelBindingMessageProvider? _messageProvider;
    [DisallowNull]
    public DefaultModelBindingMessageProvider? ModelBindingMessageProvider
    {
        get => _messageProvider;
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            
            _messageProvider = value;
        }
    }
    
    public BindingSource? BindingSource { get; set; }        
    public string? BinderModelName { get; set; }                
    public bool IsBindingAllowed { get; set; } = true;        
    public bool IsBindingRequired { get; set; }        
    public bool? IsReadOnly { get; set; }                    
    public IPropertyFilterProvider? PropertyFilterProvider { get; set; }        
    public ConstructorInfo? BoundConstructor { get; set; }
}

```

###### 3.2.4.2 binding metadata provider 接口

```c#
public interface IBindingMetadataProvider : IMetadataDetailsProvider
{        
    void CreateBindingMetadata(BindingMetadataProviderContext context);
}

```

###### 3.2.4.3 binding metadata provider context

```c#
public class BindingMetadataProviderContext
{
    public IReadOnlyList<object> Attributes { get; }        
    public ModelMetadataIdentity Key { get; }
        
    public IReadOnlyList<object>? ParameterAttributes { get; }        
    public IReadOnlyList<object>? PropertyAttributes { get; }        
    public IReadOnlyList<object>? TypeAttributes { get; }
        
    public BindingMetadata BindingMetadata { get; }
    
    public BindingMetadataProviderContext(
        ModelMetadataIdentity key,
        ModelAttributes attributes)
    {
        if (attributes == null)
        {
            throw new ArgumentNullException(nameof(attributes));
        }
        // 注入
        Key = key;
        Attributes = attributes.Attributes;
        // 解析 attribute
        ParameterAttributes = attributes.ParameterAttributes;
        PropertyAttributes = attributes.PropertyAttributes;
        TypeAttributes = attributes.TypeAttributes;
        // 结果
        BindingMetadata = new BindingMetadata();
    }            
}

```

###### 3.2.4.4 default binding metadata provider

```c#
internal class DefaultBindingMetadataProvider : IBindingMetadataProvider
{
    public void CreateBindingMetadata(BindingMetadataProviderContext context)
    {
        if (context == null)            
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        /* 通过 attribute 获取 binder model name */
        // BinderModelName
        foreach (var binderModelNameAttribute in 
                 context.Attributes.OfType<IModelNameProvider>())
        {
            if (binderModelNameAttribute.Name != null)
            {
                context.BindingMetadata.BinderModelName = binderModelNameAttribute.Name;
                break;
            }
        }
          
        /* 通过 attribute 获取 binder type */
        // BinderType
        foreach (var binderTypeAttribute in 
                 context.Attributes.OfType<IBinderTypeProviderMetadata>())
        {
            if (binderTypeAttribute.BinderType != null)
            {
                context.BindingMetadata.BinderType = binderTypeAttribute.BinderType;
                break;
            }
        }
        
        /* 通过 attribute 获取 binding source */
        // BindingSource
        foreach (var bindingSourceAttribute in 
                 context.Attributes.OfType<IBindingSourceMetadata>())
        {
            if (bindingSourceAttribute.BindingSource != null)
            {
                context.BindingMetadata.BindingSource = bindingSourceAttribute.BindingSource;
                break;
            }
        }
        
        /* 通过 attribute 获取 property filter provider */
        // PropertyFilterProvider
        var propertyFilterProviders = 
            context.Attributes.OfType<IPropertyFilterProvider>().ToArray();
        if (propertyFilterProviders.Length == 0)
        {
            context.BindingMetadata.PropertyFilterProvider = null;
        }
        else if (propertyFilterProviders.Length == 1)
        {
            context.BindingMetadata.PropertyFilterProvider = propertyFilterProviders[0];
        }
        else
        {
            // 如果 property filter provider 不唯一，
            // 创建 composie property filter provider
            var composite = new CompositePropertyFilterProvider(propertyFilterProviders);
            context.BindingMetadata.PropertyFilterProvider = composite;
        }
        
        /* 获取 binding behavior */
        var bindingBehavior = FindBindingBehavior(context);
        if (bindingBehavior != null)
        {
            context.BindingMetadata.IsBindingAllowed = 
                bindingBehavior.Behavior != BindingBehavior.Never;
            context.BindingMetadata.IsBindingRequired = 
                bindingBehavior.Behavior == BindingBehavior.Required;
        }
        
        /* 获取 bound constructor */
        if (GetBoundConstructor(context.Key.ModelType) is ConstructorInfo constructorInfo)
        {
            context.BindingMetadata.BoundConstructor = constructorInfo;
        }
    }
    
    // 获取 binding behavior
    private static BindingBehaviorAttribute? FindBindingBehavior(
        BindingMetadataProviderContext context)
    {
        switch (context.Key.MetadataKind)
        {
            case ModelMetadataKind.Property:
                // BindingBehavior can fall back to attributes on the Container Type, 
                // but we should ignore attributes on the Property Type.
                var matchingAttributes = 
                    context
                    	.PropertyAttributes!
                    	.OfType<BindingBehaviorAttribute>();                
                return matchingAttributes.FirstOrDefault()
                    ?? context.Key.ContainerType!
                    .GetCustomAttributes(
                    	typeof(BindingBehaviorAttribute), 
                    	inherit: true)
                    .OfType<BindingBehaviorAttribute>()
                    .FirstOrDefault();
                
            case ModelMetadataKind.Parameter:
                return context
                    	  .ParameterAttributes!
                    	  .OfType<BindingBehaviorAttribute>()
                     	  .FirstOrDefault();
            default:
                return null;
        }
    }        
    
    // 获取 bound constructor
    internal static ConstructorInfo? GetBoundConstructor(Type type)
    {
        if (type.IsAbstract || 
            type.IsValueType || 
            type.IsInterface)
        {
            return null;
        }
        
        var constructors = type.GetConstructors();
        if (constructors.Length == 0)
        {
            return null;
        }
        
        return GetRecordTypeConstructor(type, constructors);
    }
    
    private static ConstructorInfo? GetRecordTypeConstructor(
        Type type, 
        ConstructorInfo[] constructors)
    {
        if (!IsRecordType(type))
        {
            return null;
        }
        
        // For record types, we will support binding and validating the primary constructor.
        //
        // There isn't metadata to identify a primary constructor. Our heuristic is:
        // We require exactly one constructor to be defined on the type, 
        // and that every parameter on that constructor is mapped to a property 
        // with the same name and type.        
        if (constructors.Length > 1)
        {
            return null;
        }
        
        var constructor = constructors[0];
        
        var parameters = constructor.GetParameters();
        if (parameters.Length == 0)
        {
            // We do not need to do special handling for parameterless constructors.
            return null;
        }
        
        // 获取 type 的 property
        var properties = PropertyHelper.GetVisibleProperties(type);
        
        for (var i = 0; i < parameters.Length; i++)
        {
            // 遍历 constructor parameter，
            var parameter = parameters[i];
            
            // 查找与 parameter 同名且同类型的 property
            var mappedProperty = properties.FirstOrDefault(property => 
            	string.Equals(
                    property.Name, 
                    parameter.Name, 
                    StringComparison.Ordinal) &&
                property.Property.PropertyType == parameter.ParameterType);
            
            // 如果没找，说明不是 record type，返回 null
            if (mappedProperty is null)
            {
                // No property found, this is not a primary constructor.
                return null;
            }
        }
        
        return constructor;
        
        static bool IsRecordType(Type type)
        {
            // Based on the state of the art as described in
            // https://github.com/dotnet/roslyn/issues/45777
            var cloneMethod = type.GetMethod(
                "<Clone>$", 
                BindingFlags.Public | BindingFlags.Instance);
            
            return cloneMethod != null && 
                   cloneMethod.ReturnType == type;
        }
    }        
}

```

###### 3.2.3.3 composite property filter provider

```c#
internal class DefaultBindingMetadataProvider : IBindingMetadataProvider
{
    private class CompositePropertyFilterProvider : IPropertyFilterProvider
    {
        // 通过注入 property filter provider 集合构造         
        public CompositePropertyFilterProvider(
            IEnumerable<IPropertyFilterProvider> providers)
        {
            _providers = providers;
        }
        
        public Func<ModelMetadata, bool> PropertyFilter => CreatePropertyFilter();
        
        private readonly IEnumerable<IPropertyFilterProvider> _providers;
        private Func<ModelMetadata, bool> CreatePropertyFilter()
        {
            var propertyFilters = _providers
                .Select(p => p.PropertyFilter)
                .Where(p => p != null);
            
            return (m) =>
            {
                foreach (var propertyFilter in propertyFilters)
                {
                    if (!propertyFilter(m))
                    {
                        return false;
                    }
                }
                
                return true;
            };
        }
    }
}
```

###### 3.2.3.4 exclude binding metadata provider

```c#
public class ExcludeBindingMetadataProvider : IBindingMetadataProvider
{
    private readonly Type _type;
        
    public ExcludeBindingMetadataProvider(Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }     
        
        _type = type;
    }
        
    public void CreateBindingMetadata(BindingMetadataProviderContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }                
        // No-op if the metadata is not for the target type
        if (!_type.IsAssignableFrom(context.Key.ModelType))
        {
            return;
        }
        
        context.BindingMetadata.IsBindingAllowed = false;
    }
}

```

###### 3.2.3.5 binding source metadata provider

```c#
public class BindingSourceMetadataProvider : IBindingMetadataProvider
{
    public Type Type { get; }        
    public BindingSource? BindingSource { get; }
    
    public BindingSourceMetadataProvider(Type type, BindingSource? bindingSource)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }
        
        Type = type;
        BindingSource = bindingSource;
    }
                    
    /// <inheritdoc />
    public void CreateBindingMetadata(BindingMetadataProviderContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        if (Type.IsAssignableFrom(context.Key.ModelType))
        {
            context.BindingMetadata.BindingSource = BindingSource;
        }
    }
}

```



##### 3.2.5 display metadata

```c#
public class DisplayMetadata
{                    
    public IDictionary<object, object> AdditionalValues { get; } = 
        new Dictionary<object, object>();
        
    public bool ConvertEmptyStringToNull { get; set; } = true;        
    public string? DataTypeName { get; set; }        
    public Func<string?>? Description { get; set; }
        
    public string? DisplayFormatString
    {
        get
        {
            return DisplayFormatStringProvider();
        }
        set
        {
            DisplayFormatStringProvider = () => value;
        }
    }
    
    private Func<string?> _displayFormatStringProvider = () => null;
    public Func<string?> DisplayFormatStringProvider
    {
        get
        {
            return _displayFormatStringProvider;
        }
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            
            _displayFormatStringProvider = value;
        }
    }
                
    public Func<string?>? DisplayName { get; set; }
        
    public string? EditFormatString
    {
        get
        {
            return EditFormatStringProvider();
        }
        set
        {
            EditFormatStringProvider = () => value;
        }
    }
    
    private Func<string?> _editFormatStringProvider = () => null;    
    public Func<string?> EditFormatStringProvider
    {
        get
        {
            return _editFormatStringProvider;
        }
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            
            _editFormatStringProvider = value;
        }
    }
            
    public IEnumerable<KeyValuePair<EnumGroupAndName, string>>? 
        EnumGroupedDisplayNamesAndValues { get; set; }        
    public IReadOnlyDictionary<string, string>? EnumNamesAndValues { get; set; }        
    public bool HasNonDefaultEditFormat { get; set; }        
    public bool HideSurroundingHtml { get; set; }        
    public bool HtmlEncode { get; set; } = true;        
    public bool IsEnum { get; set; }        
    public bool IsFlagsEnum { get; set; }

       
    public string? NullDisplayText
    {
        get
        {
            return NullDisplayTextProvider();
        }
        set
        {
            NullDisplayTextProvider = () => value;
        }
    }

    private Func<string?> _nullDisplayTextProvider = () => null;    
    public Func<string?> NullDisplayTextProvider
    {
        get
        {
            return _nullDisplayTextProvider;
        }
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            
            _nullDisplayTextProvider = value;
        }
    }
        
    public int Order { get; set; } = 10000;        
    public Func<string?>? Placeholder { get; set; }        
    public bool ShowForDisplay { get; set; } = true;        
    public bool ShowForEdit { get; set; } = true;        
    public string? SimpleDisplayProperty { get; set; }        
    public string? TemplateHint { get; set; }
}

```

###### 3.2.5.2 display metadata provider 接口

```c#
public interface IDisplayMetadataProvider : IMetadataDetailsProvider
{    
    void CreateDisplayMetadata(DisplayMetadataProviderContext context);
}

```

###### 3.2.5.3 display metadata provider context

```c#
public class DisplayMetadataProviderContext
{
    public ModelMetadataIdentity Key { get; }
    public IReadOnlyList<object> Attributes { get; }
    
    public IReadOnlyList<object>? PropertyAttributes { get; }        
    public IReadOnlyList<object>? TypeAttributes { get; }
    
    public DisplayMetadata DisplayMetadata { get; }
        
    public DisplayMetadataProviderContext(
        ModelMetadataIdentity key,
        ModelAttributes attributes)
    {
        if (attributes == null)
        {
            throw new ArgumentNullException(nameof(attributes));
        }
        // 注入
        Key = key;            
        Attributes = attributes.Attributes;
        // 解析 attribute
        PropertyAttributes = attributes.PropertyAttributes;
        TypeAttributes = attributes.TypeAttributes;
        // 结果
        DisplayMetadata = new DisplayMetadata();
    }                                           
}

```

##### 3.2.6 validation metadata

```c#
public class ValidationMetadata
{   
    public bool? IsRequired { get; set; }        
    public IPropertyValidationFilter? PropertyValidationFilter { get; set; }        
    public bool? ValidateChildren { get; set; }
    public bool? HasValidators { get; set; }        
    internal bool PropertyHasValidators { get; set; }
    
    public IList<object> ValidatorMetadata { get; } = new List<object>();            
}

```

###### 3.2.6.1 validation metadata provider 接口

```c#
public interface IValidationMetadataProvider : IMetadataDetailsProvider
{    
    void CreateValidationMetadata(ValidationMetadataProviderContext context);
}

```

###### 3.2.6.2 validation metadata provider context

```c#
public class ValidationMetadataProviderContext
{
    public ModelMetadataIdentity Key { get; }    
    public IReadOnlyList<object> Attributes { get; }
    
    public IReadOnlyList<object>? ParameterAttributes { get; }        
    public IReadOnlyList<object>? PropertyAttributes { get; }        
    public IReadOnlyList<object>? TypeAttributes { get; }
    
    public ValidationMetadata ValidationMetadata { get; }
    
    public ValidationMetadataProviderContext(
        ModelMetadataIdentity key,
        ModelAttributes attributes)
    {
        if (attributes == null)
        {
            throw new ArgumentNullException(nameof(attributes));
        }
        // 注入
        Key = key;
        Attributes = attributes.Attributes;
        // 解析 attribute
        ParameterAttributes = attributes.ParameterAttributes;
        PropertyAttributes = attributes.PropertyAttributes;
        TypeAttributes = attributes.TypeAttributes;
        // 结果
        ValidationMetadata = new ValidationMetadata();
    }                   
}

```

###### 3.2.6.3 default validation metadata provider

```c#
internal class DefaultValidationMetadataProvider : IValidationMetadataProvider
{
    /// <inheritdoc />
    public void CreateValidationMetadata(ValidationMetadataProviderContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        foreach (var attribute in context.Attributes)
        {
            if (attribute is IModelValidator || 
                attribute is IClientModelValidator)
            {
                // If another provider has already added this attribute, do not repeat it.
                // This will prevent attributes like RemoteAttribute 
                // (which implement ValidationAttribute and IClientModelValidator) 
                // to be added to the ValidationMetadata twice.
                // This is to ensure we do not end up with duplication validation rules 
                // on the client side.
                if (!context.ValidationMetadata.ValidatorMetadata.Contains(attribute))
                {
                    context.ValidationMetadata.ValidatorMetadata.Add(attribute);
                }
            }
        }
        
        // IPropertyValidationFilter attributes on a type affect properties in that type, 
        // not properties that have that type. 
        // Thus, we ignore context.TypeAttributes for properties and 
        // not check at all for types.
        if (context.Key.MetadataKind == ModelMetadataKind.Property)
        {
            var validationFilter = 
                context
                	.PropertyAttributes!
                	.OfType<IPropertyValidationFilter>()
                	.FirstOrDefault();
            if (validationFilter == null)
            {
                // No IPropertyValidationFilter attributes on the property.
                // Check if container has such an attribute.
                validationFilter = context.Key.ContainerType!
                    .GetCustomAttributes(inherit: true)
                    .OfType<IPropertyValidationFilter>()
                    .FirstOrDefault();
            }
            
            context.ValidationMetadata.PropertyValidationFilter = validationFilter;
        }
        else if (context.Key.MetadataKind == ModelMetadataKind.Parameter)
        {
            var validationFilter = 
                context
                	.ParameterAttributes!
                	.OfType<IPropertyValidationFilter>()
                	.FirstOrDefault();
            context.ValidationMetadata.PropertyValidationFilter = validationFilter;
        }
    }
}

```

###### 3.2.6.4 has validator validation metadata provider

```c#
internal class HasValidatorsValidationMetadataProvider : IValidationMetadataProvider
{
    private readonly bool _hasOnlyMetadataBasedValidators;
    private readonly IMetadataBasedModelValidatorProvider[]? _validatorProviders;
    
    public HasValidatorsValidationMetadataProvider(IList<IModelValidatorProvider> modelValidatorProviders)
    {
        if (modelValidatorProviders.Count > 0 && modelValidatorProviders.All(p => p is IMetadataBasedModelValidatorProvider))
        {
            _hasOnlyMetadataBasedValidators = true;
            _validatorProviders = modelValidatorProviders.Cast<IMetadataBasedModelValidatorProvider>().ToArray();
        }
    }
    
    public void CreateValidationMetadata(ValidationMetadataProviderContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        if (!_hasOnlyMetadataBasedValidators)
        {
            return;
        }
        
        for (var i = 0; i < _validatorProviders!.Length; i++)
        {
            var provider = _validatorProviders[i];
            if (provider.HasValidators(context.Key.ModelType, context.ValidationMetadata.ValidatorMetadata))
            {
                context.ValidationMetadata.HasValidators = true;
                
                if (context.Key.MetadataKind == ModelMetadataKind.Property)
                {
                    // For properties, additionally determine that if there's validators defined exclusively
                    // from property attributes. This is later used to produce a error for record types
                    // where a record type property that is bound as a parameter defines validation attributes.
                    
                    if (context.PropertyAttributes is not IList<object> propertyAttributes)
                    {
                        propertyAttributes = context.PropertyAttributes!.ToList();
                    }
                    
                    if (provider.HasValidators(typeof(object), propertyAttributes))
                    {
                        context.ValidationMetadata.PropertyHasValidators = true;
                    }
                }
            }
        }
        
        if (context.ValidationMetadata.HasValidators == null)
        {
            context.ValidationMetadata.HasValidators = false;
        }
    }
}

```



##### 3.2.2 model metadata provider 抽象

###### 3.2.2.1 接口

```c#
public interface IModelMetadataProvider
{ 
    // for type
    ModelMetadata GetMetadataForType(Type modelType);        
    // for properties
    IEnumerable<ModelMetadata> GetMetadataForProperties(Type modelType);
}

```

###### 3.2.2.2 抽象基类

```c#
public abstract class ModelMetadataProvider : IModelMetadataProvider
{
    /* 实现 model metadata provider 接口 */
    // for properties 
    public abstract IEnumerable<ModelMetadata> GetMetadataForProperties(Type modelType);      
    // for type
    public abstract ModelMetadata GetMetadataForType(Type modelType);
     
    /* for parameter */
    public abstract ModelMetadata GetMetadataForParameter(ParameterInfo parameter);       
    
    public virtual ModelMetadata GetMetadataForParameter(
        ParameterInfo parameter, 
        Type modelType)
    {
        throw new NotSupportedException();
    }
    
    /* for property */
    public virtual ModelMetadata GetMetadataForProperty(
        PropertyInfo propertyInfo, 
        Type modelType)
    {
        throw new NotSupportedException();
    }
    
    /* for constructor */
    public virtual ModelMetadata GetMetadataForConstructor(
        ConstructorInfo constructor, 
        Type modelType)
    {
        throw new NotSupportedException();
    }
}

```

###### 3.2.2.3 扩展方法

```c#
public static class ModelMetadataProviderExtensions
{    
    public static ModelMetadata GetMetadataForProperty(
        this IModelMetadataProvider provider,
        Type containerType,
        string propertyName)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }        
        if (containerType == null)
        {
            throw new ArgumentNullException(nameof(containerType));
        }        
        if (propertyName == null)
        {
            throw new ArgumentNullException(nameof(propertyName));
        }
        
        var containerMetadata = provider.GetMetadataForType(containerType);
        
        var propertyMetadata = containerMetadata.Properties[propertyName];
        if (propertyMetadata == null)
        {
            var message = Resources.FormatCommon_PropertyNotFound(containerType, propertyName);
            throw new ArgumentException(message, nameof(propertyName));
        }
        
        return propertyMetadata;
    }
}

```

##### 3.2.3 model metadata provider 具体实现

###### 3.2.3.1 default model metadata provider

```c#
public class DefaultModelMetadataProvider : ModelMetadataProvider
{
    private readonly ModelMetadataCache _modelMetadataCache = new ModelMetadataCache();
    private readonly Func<ModelMetadataIdentity, ModelMetadataCacheEntry> _cacheEntryFactory;
    private readonly ModelMetadataCacheEntry _metadataCacheEntryForObjectType;
    
    /// <summary>
    /// Creates a new <see cref="DefaultModelMetadataProvider"/>.
        /// </summary>
        /// <param name="detailsProvider">The <see cref="ICompositeMetadataDetailsProvider"/>.</param>
    public DefaultModelMetadataProvider(ICompositeMetadataDetailsProvider detailsProvider)
        : this(
            detailsProvider, 
            new DefaultModelBindingMessageProvider())
    {
    }

        /// <summary>
        /// Creates a new <see cref="DefaultModelMetadataProvider"/>.
        /// </summary>
        /// <param name="detailsProvider">The <see cref="ICompositeMetadataDetailsProvider"/>.</param>
        /// <param name="optionsAccessor">The accessor for <see cref="MvcOptions"/>.</param>
    public DefaultModelMetadataProvider(
        ICompositeMetadataDetailsProvider detailsProvider,
        IOptions<MvcOptions> optionsAccessor)
        	: this(
                detailsProvider, 
                GetMessageProvider(optionsAccessor))
    {
    }
    
    private DefaultModelMetadataProvider(
        ICompositeMetadataDetailsProvider detailsProvider,
        DefaultModelBindingMessageProvider modelBindingMessageProvider)
    {
        if (detailsProvider == null)
        {
            throw new ArgumentNullException(nameof(detailsProvider));
        }
        
        DetailsProvider = detailsProvider;
        ModelBindingMessageProvider = modelBindingMessageProvider;
        
        _cacheEntryFactory = CreateCacheEntry;
        _metadataCacheEntryForObjectType = GetMetadataCacheEntryForObjectType();
    }
    
    /// <summary>
    /// Gets the <see cref="ICompositeMetadataDetailsProvider"/>.
    /// </summary>
    protected ICompositeMetadataDetailsProvider DetailsProvider { get; }
    
        /// <summary>
        /// Gets the <see cref="Metadata.DefaultModelBindingMessageProvider"/>.
        /// </summary>
        /// <value>Same as <see cref="MvcOptions.ModelBindingMessageProvider"/> in all production scenarios.</value>    
    protected DefaultModelBindingMessageProvider ModelBindingMessageProvider { get; }
    
    /// <inheritdoc />
    public override IEnumerable<ModelMetadata> GetMetadataForProperties(Type modelType)
    {
        if (modelType == null)
        {
            throw new ArgumentNullException(nameof(modelType));
        }
        
        var cacheEntry = GetCacheEntry(modelType);
        
        // We're relying on a safe race-condition for Properties - take care only
        // to set the value once the properties are fully-initialized.
        if (cacheEntry.Details.Properties == null)
        {
            var key = ModelMetadataIdentity.ForType(modelType);
            var propertyDetails = CreatePropertyDetails(key);
            
            var properties = new ModelMetadata[propertyDetails.Length];
            for (var i = 0; i < properties.Length; i++)
            {
                propertyDetails[i].ContainerMetadata = cacheEntry.Metadata;
                properties[i] = CreateModelMetadata(propertyDetails[i]);
            }
            
            cacheEntry.Details.Properties = properties;
        }
        
        return cacheEntry.Details.Properties;
    }
    
    /// <inheritdoc />
    public override ModelMetadata GetMetadataForParameter(ParameterInfo parameter) => 
        GetMetadataForParameter(parameter, parameter.ParameterType);

    /// <inheritdoc />
    public override ModelMetadata GetMetadataForParameter(
        ParameterInfo parameter, 
        Type modelType)
    {
        if (parameter == null)
        {
            throw new ArgumentNullException(nameof(parameter));
        }        
        if (modelType == null)
        {
            throw new ArgumentNullException(nameof(modelType));
        }
        
        var cacheEntry = GetCacheEntry(parameter, modelType);        
        return cacheEntry.Metadata;
    }
    
    /// <inheritdoc />
    public override ModelMetadata GetMetadataForType(Type modelType)
    {
        if (modelType == null)
        {
            throw new ArgumentNullException(nameof(modelType));
        }
        
        var cacheEntry = GetCacheEntry(modelType);        
        return cacheEntry.Metadata;
    }
    
    /// <inheritdoc />
    public override ModelMetadata GetMetadataForProperty(
        PropertyInfo propertyInfo, 
        Type modelType)
    {
        if (propertyInfo == null)
        {
            throw new ArgumentNullException(nameof(propertyInfo));
        }        
        if (modelType == null)
        {
            throw new ArgumentNullException(nameof(modelType));
        }
        
        var cacheEntry = GetCacheEntry(propertyInfo, modelType);        
        return cacheEntry.Metadata;
    }
    
    /// <inheritdoc />
    
    public override ModelMetadata GetMetadataForConstructor(
        ConstructorInfo constructorInfo, 
        Type modelType)
    {
        if (constructorInfo is null)
        {
            throw new ArgumentNullException(nameof(constructorInfo));
        }
        
        var cacheEntry = GetCacheEntry(constructorInfo, modelType);
        return cacheEntry.Metadata;
    }
    
    private static DefaultModelBindingMessageProvider GetMessageProvider(
        IOptions<MvcOptions> optionsAccessor)
    {
        if (optionsAccessor == null)
        {
            throw new ArgumentNullException(nameof(optionsAccessor));
        }
        
        return optionsAccessor.Value.ModelBindingMessageProvider;
    }
    
    private ModelMetadataCacheEntry GetCacheEntry(Type modelType)
    {
        ModelMetadataCacheEntry cacheEntry;
        
        // Perf: We cached model metadata cache entry for "object" type to save ConcurrentDictionary lookups.
        if (modelType == typeof(object))
        {
            cacheEntry = _metadataCacheEntryForObjectType;
        }
        else
        {
            var key = ModelMetadataIdentity.ForType(modelType);
            
            cacheEntry = _modelMetadataCache.GetOrAdd(
                								key, 
                								_cacheEntryFactory);
        }
        
        return cacheEntry;
    }
    
    private ModelMetadataCacheEntry GetCacheEntry(
        ParameterInfo parameter, 
        Type modelType)
    {
        return _modelMetadataCache.GetOrAdd(
            ModelMetadataIdentity.ForParameter(parameter, modelType),
            _cacheEntryFactory);
    }
    
    private ModelMetadataCacheEntry GetCacheEntry(
        PropertyInfo property, 
        Type modelType)
    {
        return _modelMetadataCache.GetOrAdd(
            ModelMetadataIdentity.ForProperty(
                property, 
                modelType, 
                property.DeclaringType!),
            _cacheEntryFactory);
    }
    
    private ModelMetadataCacheEntry GetCacheEntry(
        ConstructorInfo constructor, 
        Type modelType)
    {
        return _modelMetadataCache.GetOrAdd(
            ModelMetadataIdentity.ForConstructor(constructor, modelType),
            _cacheEntryFactory);
    }
    
    private ModelMetadataCacheEntry CreateCacheEntry(ModelMetadataIdentity key)
    {
        DefaultMetadataDetails details;
        
        if (key.MetadataKind == ModelMetadataKind.Constructor)
        {
            details = CreateConstructorDetails(key);
        }
        else if (key.MetadataKind == ModelMetadataKind.Parameter)
        {
            details = CreateParameterDetails(key);
        }
        else if (key.MetadataKind == ModelMetadataKind.Property)
        {
            details = CreateSinglePropertyDetails(key);
        }
        else
        {
            details = CreateTypeDetails(key);
        }
        
        var metadata = CreateModelMetadata(details);
        return new ModelMetadataCacheEntry(metadata, details);
    }
    
    private DefaultMetadataDetails CreateSinglePropertyDetails(
        ModelMetadataIdentity propertyKey)
    {
        var propertyHelpers = PropertyHelper.GetVisibleProperties(propertyKey.ContainerType!);
        for (var i = 0; i < propertyHelpers.Length; i++)
        {
            var propertyHelper = propertyHelpers[i];
            if (propertyHelper.Name == propertyKey.Name)
            {
                return CreateSinglePropertyDetails(propertyKey, propertyHelper);
            }
        }
        
        Debug.Fail(
            $"Unable to find property '{propertyKey.Name}' on type 
            '{propertyKey.ContainerType}.");
        return null;
    }
    
    private DefaultMetadataDetails CreateConstructorDetails(
        ModelMetadataIdentity constructorKey)
    {
        var constructor = constructorKey.ConstructorInfo;
        var parameters = constructor!.GetParameters();
        var parameterMetadata = new ModelMetadata[parameters.Length];
        var parameterTypes = new Type[parameters.Length];
        
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var parameterDetails = 
                CreateParameterDetails(
                	ModelMetadataIdentity.ForParameter(parameter));
            
            parameterMetadata[i] = CreateModelMetadata(parameterDetails);
            
            parameterTypes[i] = parameter.ParameterType;
        }
        
        var constructorDetails = 
            new DefaultMetadataDetails(
            	constructorKey, 
            	ModelAttributes.Empty);
        constructorDetails.BoundConstructorParameters = parameterMetadata;
        constructorDetails.BoundConstructorInvoker = CreateObjectFactory(constructor);
        
        return constructorDetails;
        
        static Func<object?[], object> CreateObjectFactory(ConstructorInfo constructor)
        {
            var args = Expression.Parameter(typeof(object?[]), "args");
            var factoryExpressionBody = BuildFactoryExpression(constructor, args);
            
            var factoryLamda = 
                Expression.Lambda<Func<object?[], object>>(
                									factoryExpressionBody, 
                									args);
            
            return factoryLamda.Compile();
        }
    }
    
    private static Expression BuildFactoryExpression(
        ConstructorInfo constructor,
        Expression factoryArgumentArray)
    {
        var constructorParameters = constructor.GetParameters();
        var constructorArguments = new Expression[constructorParameters.Length];
        
        for (var i = 0; i < constructorParameters.Length; i++)
        {
            var constructorParameter = constructorParameters[i];
            var parameterType = constructorParameter.ParameterType;
            
            constructorArguments[i] = 
                Expression.ArrayAccess(
                	factoryArgumentArray, 
                	Expression.Constant(i));
            
            if (ParameterDefaultValue.TryGetDefaultValue(
                	constructorParameter, 
                	out var defaultValue))
            {
                // We have a default value;
            }
            else if (parameterType.IsValueType)
            {
                defaultValue = Activator.CreateInstance(parameterType);
            }
            
            if (defaultValue != null)
            {
                var defaultValueExpression = Expression.Constant(defaultValue);
                constructorArguments[i] = 
                    Expression.Coalesce(
                    	constructorArguments[i], 
                    	defaultValueExpression);
            }
            
            constructorArguments[i] = 
                Expression.Convert(
                	constructorArguments[i], 
                	parameterType);
        }
        
        return Expression.New(constructor, constructorArguments);
    }
    
    private ModelMetadataCacheEntry GetMetadataCacheEntryForObjectType()
    {
        var key = ModelMetadataIdentity.ForType(typeof(object));
        var entry = CreateCacheEntry(key);
        return entry;
    }
    
    /// <summary>
        /// Creates a new <see cref="ModelMetadata"/> from a <see cref="DefaultMetadataDetails"/>.
        /// </summary>
        /// <param name="entry">The <see cref="DefaultMetadataDetails"/> entry with cached data.</param>
        /// <returns>A new <see cref="ModelMetadata"/> instance.</returns>
        /// <remarks>
        /// <see cref="DefaultModelMetadataProvider"/> will always create instances of
        /// <see cref="DefaultModelMetadata"/> .Override this method to create a <see cref="ModelMetadata"/>
        /// of a different concrete type.
        /// </remarks>
    protected virtual ModelMetadata CreateModelMetadata(DefaultMetadataDetails entry)
    {
        return new DefaultModelMetadata(
            this, 
            DetailsProvider, 
            entry, 
            ModelBindingMessageProvider);
    }
    
        /// <summary>
        /// Creates the <see cref="DefaultMetadataDetails"/> entries for the properties of a model
        /// <see cref="Type"/>.
        /// </summary>
        /// <param name="key">
        /// The <see cref="ModelMetadataIdentity"/> identifying the model <see cref="Type"/>.
        /// </param>
        /// <returns>A details object for each property of the model <see cref="Type"/>.</returns>
        /// <remarks>
        /// The results of this method will be cached and used to satisfy calls to
        /// <see cref="GetMetadataForProperties(Type)"/>. Override this method to provide a different
        /// set of property data.
        /// </remarks>
    protected virtual DefaultMetadataDetails[] CreatePropertyDetails(ModelMetadataIdentity key)
    {
        var propertyHelpers = PropertyHelper.GetVisibleProperties(key.ModelType);
        
        var propertyEntries = new List<DefaultMetadataDetails>(propertyHelpers.Length);
        for (var i = 0; i < propertyHelpers.Length; i++)
        {
            var propertyHelper = propertyHelpers[i];
            
            var propertyKey = ModelMetadataIdentity.ForProperty(
                propertyHelper.Property,
                propertyHelper.Property.PropertyType,
                key.ModelType);
            
            var propertyEntry = CreateSinglePropertyDetails(propertyKey, propertyHelper);
            propertyEntries.Add(propertyEntry);
        }
        
        return propertyEntries.ToArray();
    }

    private DefaultMetadataDetails CreateSinglePropertyDetails(
        ModelMetadataIdentity propertyKey,
        PropertyHelper propertyHelper)
    {
        Debug.Assert(propertyKey.MetadataKind == ModelMetadataKind.Property);
        var containerType = propertyKey.ContainerType!;
        
        var attributes = ModelAttributes.GetAttributesForProperty(
            containerType,
            propertyHelper.Property,
            propertyKey.ModelType);
        
        var propertyEntry = 
            new DefaultMetadataDetails(propertyKey, attributes);
        if (propertyHelper.Property.CanRead && 
            propertyHelper.Property.GetMethod?.IsPublic == true)
        {
            var getter = 
                PropertyHelper
                	.MakeNullSafeFastPropertyGetter(propertyHelper.Property);
            propertyEntry.PropertyGetter = getter;
        }
        
        if (propertyHelper.Property.CanWrite &&
            propertyHelper.Property.SetMethod?.IsPublic == true &&
            !containerType.IsValueType)
        {
            propertyEntry.PropertySetter = propertyHelper.ValueSetter;
        }
        
        return propertyEntry;
    }
    
    /// <summary>
    /// Creates the <see cref="DefaultMetadataDetails"/> entry for a model <see cref="Type"/>.
    /// </summary>
    /// <param name="key">
    /// The <see cref="ModelMetadataIdentity"/> identifying the model <see cref="Type"/>.
    /// </param>
    /// <returns>A details object for the model <see cref="Type"/>.</returns>
    /// <remarks>
    /// The results of this method will be cached and used to satisfy calls to
    /// <see cref="GetMetadataForType(Type)"/>. Override this method to provide a different
        /// set of attributes.
        /// </remarks>
    protected virtual DefaultMetadataDetails CreateTypeDetails(ModelMetadataIdentity key)
    {
        return new DefaultMetadataDetails(
            key,
            ModelAttributes.GetAttributesForType(key.ModelType));
    }
    
    /// <summary>
        /// Creates the <see cref="DefaultMetadataDetails"/> entry for a parameter <see cref="Type"/>.
        /// </summary>
        /// <param name="key">
        /// The <see cref="ModelMetadataIdentity"/> identifying the parameter <see cref="Type"/>.
        /// </param>
        /// <returns>A details object for the parameter.</returns>
    protected virtual DefaultMetadataDetails CreateParameterDetails(ModelMetadataIdentity key)
    {
        return new DefaultMetadataDetails(
            key,
            ModelAttributes.GetAttributesForParameter(
                key.ParameterInfo!, 
                key.ModelType));
    }
    
    private class ModelMetadataCache 
        : ConcurrentDictionary<ModelMetadataIdentity, ModelMetadataCacheEntry>
    {
    }
    
    private readonly struct ModelMetadataCacheEntry
    {
        public ModelMetadata Metadata { get; }        
        public DefaultMetadataDetails Details { get; }
        
        public ModelMetadataCacheEntry(
            ModelMetadata metadata, 
            DefaultMetadataDetails details)
        {
            Metadata = metadata;
            Details = details;
        }
        
        
    }
}

```

###### 3.2.3.2 empty model metadata provider

```c#
public class EmptyModelMetadataProvider : DefaultModelMetadataProvider
{
    /// <summary>
    /// Initializes a new <see cref="EmptyModelMetadataProvider"/>.
    /// </summary>
    public EmptyModelMetadataProvider()
        : base(
            new DefaultCompositeMetadataDetailsProvider(
                new List<IMetadataDetailsProvider>()),
            new OptionsAccessor())
    {
    }
    
    private class OptionsAccessor : IOptions<MvcOptions>
    {
        public MvcOptions Value { get; } = new MvcOptions();
    }
}

```



#### 3.2 model binder

##### 3.2.1 接口

```c#
public interface IModelBinder
{    
    Task BindModelAsync(ModelBindingContext bindingContext);
}

```

* context

```c#
public abstract class ModelBindingContext
{    
    public abstract ActionContext ActionContext { get; set; }        
    public abstract string? BinderModelName { get; set; }        
    public abstract BindingSource? BindingSource { get; set; }        
    public abstract string FieldName { get; set; }        
    public virtual HttpContext HttpContext => ActionContext?.HttpContext!;        
    public abstract bool IsTopLevelObject { get; set; }        
    public abstract object? Model { get; set; }        
    public abstract ModelMetadata ModelMetadata { get; set; }        
    public abstract string ModelName { get; set; }        
    public string OriginalModelName { get; protected set; } = default!;        
    public abstract ModelStateDictionary ModelState { get; set; }        
    public virtual Type ModelType => ModelMetadata.ModelType;        
    public abstract Func<ModelMetadata, bool>? PropertyFilter { get; set; }        
    public abstract ValidationStateDictionary ValidationState { get; set; }        
    public abstract IValueProvider ValueProvider { get; set; }        
    public abstract ModelBindingResult Result { get; set; }
    
    public abstract NestedScope EnterNestedScope(
        ModelMetadata modelMetadata,
        string fieldName,
        string modelName,
        object? model);
        
    public abstract NestedScope EnterNestedScope();
            
    protected abstract void ExitNestedScope();
        
    public readonly struct NestedScope : IDisposable
    {
        private readonly ModelBindingContext _context;
                
        public NestedScope(ModelBindingContext context)
        {
            _context = context;
        }
                
        public void Dispose()
        {
            _context.ExitNestedScope();
        }
    }
}

```

* model binding result

```c#
public readonly struct ModelBindingResult : IEquatable<ModelBindingResult>
{    
    public static ModelBindingResult Failed()
    {
        return new ModelBindingResult(model: null, isModelSet: false);
    }
        
    public static ModelBindingResult Success(object? model)
    {
        return new ModelBindingResult(model, isModelSet: true);
    }
            
    public object? Model { get; }
    public bool IsModelSet { get; }
    
    private ModelBindingResult(object? model, bool isModelSet)
    {
        Model = model;
        IsModelSet = isModelSet;
    }
                
    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        var other = obj as ModelBindingResult?;
        if (other == null)
        {
            return false;
        }
        else
        {
            return Equals(other.Value);
        }
    }
    
    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hashCodeCombiner = new HashCode();
        
        hashCodeCombiner.Add(IsModelSet);
        hashCodeCombiner.Add(Model);
        
        return hashCodeCombiner.ToHashCode();
    }
    
    /// <inheritdoc />
    public bool Equals(ModelBindingResult other)
    {
        return IsModelSet == other.IsModelSet &&
               object.Equals(Model, other.Model);
    }
    
    /// <inheritdoc />
    public override string ToString()
    {
        if (IsModelSet)
        {
            return $"Success '{Model}'";
        }
        else
        {
            return "Failed";
        }
    }
        
    public static bool operator ==(ModelBindingResult x, ModelBindingResult y)
    {
        return x.Equals(y);
    }
        
    public static bool operator !=(ModelBindingResult x, ModelBindingResult y)
    {
        return !x.Equals(y);
    }
}

```



##### 3.2.2 model binder provider

```c#
public interface IModelBinderProvider
{    
    IModelBinder? GetBinder(ModelBinderProviderContext context);
}

```

* context

```c#
public abstract class ModelBinderProviderContext
{
    public abstract BindingInfo BindingInfo { get; }        
    public abstract ModelMetadata Metadata { get; }        
    public abstract IModelMetadataProvider MetadataProvider { get; }        
    public virtual IServiceProvider Services { get; } = default!;
    
    public abstract IModelBinder CreateBinder(
        ModelMetadata metadata);
        
    public virtual IModelBinder CreateBinder(
        ModelMetadata metadata, 
        BindingInfo bindingInfo)
    {
        throw new NotSupportedException();
    }            
}

```

* 扩展

```c#
public static class ModelBinderProviderExtensions
{    
    public static void RemoveType<TModelBinderProvider>(this IList<IModelBinderProvider> list) 
        where TModelBinderProvider : IModelBinderProvider
    {
        if (list == null)
        {
            throw new ArgumentNullException(nameof(list));
        }
        
        RemoveType(list, typeof(TModelBinderProvider));
    }
        
    public static void RemoveType(this IList<IModelBinderProvider> list, Type type)
    {
        if (list == null)
        {
            throw new ArgumentNullException(nameof(list));
        }        
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }
        
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var modelBinderProvider = list[i];
            if (modelBinderProvider.GetType() == type)
            {
                list.RemoveAt(i);
            }
        }
    }
}

```

#### 3.3 model binder factory

##### 3.3.1 接口

```c#
public interface IModelBinderFactory
{    
    IModelBinder CreateBinder(ModelBinderFactoryContext context);
}

```

##### 3.3.2 context

```c#
public class ModelBinderFactoryContext
{    
    public BindingInfo? BindingInfo { get; set; }        
    public ModelMetadata Metadata { get; set; } = default!;        
    public object? CacheToken { get; set; }
}

```

##### 3.3.3 实现

```c#
public class ModelBinderFactory : IModelBinderFactory
{
    private readonly IModelMetadataProvider _metadataProvider;
    private readonly IModelBinderProvider[] _providers;
    private readonly ConcurrentDictionary<Key, IModelBinder> _cache;
    private readonly IServiceProvider _serviceProvider;
    
    
    public ModelBinderFactory(
        IModelMetadataProvider metadataProvider,
        IOptions<MvcOptions> options,
        IServiceProvider serviceProvider)
    {
        _metadataProvider = metadataProvider;
        _providers = options.Value.ModelBinderProviders.ToArray();
        _serviceProvider = serviceProvider;
        _cache = new ConcurrentDictionary<Key, IModelBinder>();
        
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ModelBinderFactory>();
        logger.RegisteredModelBinderProviders(_providers);
    }
    
    /// <inheritdoc />
    public IModelBinder CreateBinder(ModelBinderFactoryContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        if (_providers.Length == 0)
        {
            throw new InvalidOperationException(
                Resources.FormatModelBinderProvidersAreRequired(
                    typeof(MvcOptions).FullName,
                    nameof(MvcOptions.ModelBinderProviders),
                    typeof(IModelBinderProvider).FullName));
        }
        
        if (TryGetCachedBinder(
	            context.Metadata, 
    	        context.CacheToken, 
        	    out var binder))
        {
            return binder;
        }

        // Perf: We're calling the Uncached version of the API here so we can:
        // 1. avoid allocating a context when the value is already cached
        // 2. avoid checking the cache twice when the value is not cached
        var providerContext = new DefaultModelBinderProviderContext(this, context);
        binder = CreateBinderCoreUncached(
            providerContext, 
            context.CacheToken);
        if (binder == null)
        {
            var message = Resources.FormatCouldNotCreateIModelBinder(
                providerContext.Metadata.ModelType);
            throw new InvalidOperationException(message);
        }
        
        Debug.Assert(!(binder is PlaceholderBinder));
        AddToCache(context.Metadata, context.CacheToken, binder);
        
        return binder;
    }
    
    // Called by the DefaultModelBinderProviderContext when 
    // we're recursively creating a binder so that all intermediate results can be cached.
    private IModelBinder CreateBinderCoreCached(
        DefaultModelBinderProviderContext providerContext, 
        object? token)
    {
        if (TryGetCachedBinder(
	            providerContext.Metadata, 
    	        token, 
        	    out var binder))
        {
            return binder;
        }
        
        // We're definitely creating a binder for an non-root node here, 
        // so it's OK for binder creation to fail.
        binder = CreateBinderCoreUncached(providerContext, token) ?? NoOpBinder.Instance;
        
        if (!(binder is PlaceholderBinder))
        {
            AddToCache(providerContext.Metadata, token, binder);
        }
        
        return binder;
    }
    
    private IModelBinder? CreateBinderCoreUncached(
        DefaultModelBinderProviderContext providerContext, 
        object? token)
    {
        if (!providerContext.Metadata.IsBindingAllowed)
        {
            return NoOpBinder.Instance;
        }
        
        // A non-null token will usually be passed in at the top level 
        // (ParameterDescriptor likely).
        // This prevents us from treating a parameter the same as 
        // a collection-element - which could happen looking at just model metadata.
        var key = new Key(providerContext.Metadata, token);
        
        // The providerContext.Visited is used here to break cycles in recursion. 
        // We need a separate per-operation cache for cycle breaking 
        // because the global cache (_cache) needs to always stay in a valid state.
        //
        // We store null as a sentinel inside the providerContext.
        // Visited to track the fact that we've visited a given node 
        // but haven't yet created a binder for it. 
        // We don't want to eagerly create a PlaceholderBinder because that 
        // would result in lots of unnecessary indirection and allocations.
        var visited = providerContext.Visited;
        
        if (visited.TryGetValue(key, out var binder))
        {
            if (binder != null)
            {
                return binder;
            }
            
            // If we're currently recursively building a binder for this type, just return
            // a PlaceholderBinder. We'll fix it up later to point to the 'real' binder
            // when the stack unwinds.
            binder = new PlaceholderBinder();
            visited[key] = binder;
            return binder;
        }
        
        // OK this isn't a recursive case (yet) so add an entry and then ask the providers
        // to create the binder.
        visited.Add(key, null);
        
        IModelBinder? result = null;
        
        for (var i = 0; i < _providers.Length; i++)
        {
            var provider = _providers[i];
            result = provider.GetBinder(providerContext);
            if (result != null)
            {
                break;
            }
        }
        
        // If the PlaceholderBinder was created, 
        // then it means we recursed. Hook it up to the 'real' binder.
        if (visited[key] is PlaceholderBinder placeholderBinder)
        {
            // It's also possible that user code called into `CreateBinder` 
            // but then returned null, 
            // we don't want to create something that will null-ref later 
            // so just hook this up to the no-op binder.
            placeholderBinder.Inner = result ?? NoOpBinder.Instance;
        }
        
        if (result != null)
        {
            visited[key] = result;
        }
        
        return result;
    }
    
    private void AddToCache(
        ModelMetadata metadata, 
        object? cacheToken, 
        IModelBinder binder)
    {
        Debug.Assert(metadata != null);
        Debug.Assert(binder != null);
        
        if (cacheToken == null)
        {
            return;
        }
        
        _cache.TryAdd(new Key(metadata, cacheToken), binder);
    }
    
    private bool TryGetCachedBinder(
        ModelMetadata metadata, 
        object? cacheToken, 
        [NotNullWhen(true)] out IModelBinder? binder)
    {
        Debug.Assert(metadata != null);
        
        if (cacheToken == null)
        {
            binder = null;
            return false;
        }
        
        return _cache.TryGetValue(
            new Key(metadata, cacheToken), 
            out binder);
    }
    
    private class DefaultModelBinderProviderContext : ModelBinderProviderContext
    {
        private readonly ModelBinderFactory _factory;
        
        public DefaultModelBinderProviderContext(
            ModelBinderFactory factory,
            ModelBinderFactoryContext factoryContext)
        {
            _factory = factory;
            Metadata = factoryContext.Metadata;
            BindingInfo bindingInfo;
            if (factoryContext.BindingInfo != null)
            {
                bindingInfo = new BindingInfo(factoryContext.BindingInfo);
            }
            else
            {
                bindingInfo = new BindingInfo();
            }
            
            bindingInfo.TryApplyBindingInfo(Metadata);
            BindingInfo = bindingInfo;
            
            MetadataProvider = _factory._metadataProvider;
            Visited = new Dictionary<Key, IModelBinder?>();
        }
        
        private DefaultModelBinderProviderContext(
            DefaultModelBinderProviderContext parent,
            ModelMetadata metadata,
            BindingInfo bindingInfo)
        {
            Metadata = metadata;
            
            _factory = parent._factory;
            MetadataProvider = parent.MetadataProvider;
            Visited = parent.Visited;
            BindingInfo = bindingInfo;
        }
        
        public override BindingInfo BindingInfo { get; }
        
        public override ModelMetadata Metadata { get; }
        
        public override IModelMetadataProvider MetadataProvider { get; }
        
        public Dictionary<Key, IModelBinder?> Visited { get; }
        
        public override IServiceProvider Services => _factory._serviceProvider;
        
        public override IModelBinder CreateBinder(ModelMetadata metadata)
        {
            var bindingInfo = new BindingInfo();
            bindingInfo.TryApplyBindingInfo(metadata);
            
            return CreateBinder(metadata, bindingInfo);
        }
        
        public override IModelBinder CreateBinder(
            ModelMetadata metadata, 
            BindingInfo bindingInfo)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }
            if (bindingInfo == null)
            {
                throw new ArgumentNullException(nameof(bindingInfo));
            }
            
            // For non-root nodes we use the ModelMetadata as the cache token. This ensures that all non-root
            // nodes with the same metadata will have the same binder. This is OK because for an non-root
            // node there's no opportunity to customize binding info like there is for a parameter.
            var token = metadata;
            
            var nestedContext = new DefaultModelBinderProviderContext(this, metadata, bindingInfo);
            return _factory.CreateBinderCoreCached(nestedContext, token);
        }
    }
    
    // This key allows you to specify a ModelMetadata which represents the type/property being boun
    
    // and a 'token' which acts as an arbitrary discriminator.
    //
    // This is necessary because the same metadata might be bound as a top-level parameter (with BindingInfo on
    // the ParameterDescriptor) or in a call to TryUpdateModel (no BindingInfo) or as a collection element.
    //
    // We need to be able to tell the difference between these things to avoid over-caching.
    private readonly struct Key : IEquatable<Key>
    {
        private readonly ModelMetadata _metadata;
        private readonly object? _token; // Explicitly using ReferenceEquality for tokens.
        
        public Key(ModelMetadata metadata, object? token)
        {
            _metadata = metadata;
            _token = token;
        }
        
        public bool Equals(Key other)
        {
            return _metadata.Equals(other._metadata) && object.ReferenceEquals(_token, other._token);
        }
        
        public override bool Equals(object? obj)
        {
            return obj is Key other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(_metadata);
            hash.Add(RuntimeHelpers.GetHashCode(_token));
            return hash.ToHashCode();
        }
        
        public override string ToString()
        {
            switch (_metadata.MetadataKind)
            {
                case ModelMetadataKind.Parameter:
                    return 
                        $"{_token} (Parameter: '{_metadata.ParameterName}' 
                        "Type: '{_metadata.ModelType.Name}')";
                case ModelMetadataKind.Property:
                    return 
                        $"{_token} (Property: '{_metadata.ContainerType!.Name}.{_metadata.PropertyName}' " +
                        $"Type: '{_metadata.ModelType.Name}')";
                case ModelMetadataKind.Type:
                    return $"{_token} (Type: '{_metadata.ModelType.Name}')";
                default:
                    return $"Unsupported MetadataKind '{_metadata.MetadataKind}'.";
            }
        }
    }
}


```



