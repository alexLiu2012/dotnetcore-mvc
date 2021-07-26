## about configuration and options



### 1. about

#### 1.1 summary

.net core 框架提供了对外部配置信息读取操作的抽象`IConfiguration`。框架从不同配置源读取配置信息，将其保存在一个 kv pair 的集合（字典）中，扁平化了数据源（用“：”分隔层级关系），并由`IConfiguration`统一读取。

`options`是 .net core 提供的对配置对象的强类型封装，它可以通过 configuration 获取源数据，然后 bind 到具体类型

#### 1.2 how desgined

##### 1.2.1 configuration

* `IConfiguration` 是框架对上层服务提供配置信息的统一接口，采用组合模式设计，`IConfigurationRoot`和`IConfigurationSection`是其派生接口
* `ConfigurationBuilder`是 configuration 的构建器，通过向其注入不同的 configuration source 增加配置源，最终构建为 configuration
* `ConfigurationSource`是 configuration 的配置（提供）源，它内部会使用 configuration provider 负责具体提供配置信息。一个 configuration source 一般对应一个 configuration provider，使用 configuration source 可以提供其他必须的元素（configuration provider）的超集。
* services 扩展方法注入配置源
  * add memory collection
  * add command line
  * add environment virables
  * add user secret
  * add ini / json / xml

##### 1.2.2 options

###### 1.2.2.1 options factory

创建强类型配置 toptions 的工厂方法，通过3个扩展接口配置 toptions

* IConfigureOptions，用于 configure（配置）options
* IPostConfigureOptions，用于 post configure（后配置）options
* IValidateOptions，用于 validate（验证）options

###### 1.2.2.2 options 接口

IOptions 接口向上层应用提供服务，除此以外，框架还提供了`IOptionsMonitor`接口，可以定义 on change listener 监听 options 绑定来源的变化；`IOptionsSnapshot`自动追踪更新后的 options（一直是最新的，transient，不能用于 singleton、scoped）

上述2个接口的实现在内部使用 options factory 创建 options<T>，并缓存创建的 options，change token 用于触发更新 options（重新创建）

###### 1.2.2.3 options builder

向 di 注入上述3个扩展接口，

###### 1.2.2.3 options service

* add options 用于注册 options 必须的服务，例如 IOptions<> 等；add options <T> 在 add options 基础上注入 options builder<T>
* configure<t> 方法、post configure <T> 方法注册 IConfigureOptions、IPostConfigureOptions 等接口

#### 1.3 how to use

* 使用 options factory 直接创建 toptions  -- 不常用
* 使用 di 解析 toptions（注入后使用）-- 常用

### 2. configuration

#### 2.1 configuration

```c#
public interface IConfiguration
{    
    string this[string key] { get; set; }
        
    IConfigurationSection GetSection(string key);        
    IEnumerable<IConfigurationSection> GetChildren();        
    IChangeToken GetReloadToken();
}

```

##### 2.1.1 扩展方法

```c#
public static class ConfigurationExtensions
{
    // get connection string
    public static string GetConnectionString(this IConfiguration configuration, string name)
    {
        return configuration?.GetSection("ConnectionStrings")?[name];
    }
           
    // get required section (in .net 6)
    public static IConfigurationSection GetRequiredSection(this IConfiguration configuration, string key)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }
        
        IConfigurationSection section = configuration.GetSection(key);
        if (section.Exists())
        {
            return section;
        }
        
        throw new InvalidOperationException(SR.Format(SR.InvalidSectionName, key));
    }
    
    // convert to enumerable
    public static IEnumerable<KeyValuePair<string, string>> AsEnumerable(this IConfiguration configuration) => 
        configuration.AsEnumerable(makePathsRelative: false);
        
    public static IEnumerable<KeyValuePair<string, string>> AsEnumerable(
        this IConfiguration configuration, 
        bool makePathsRelative)
    {
        var stack = new Stack<IConfiguration>();
        stack.Push(configuration);
        var rootSection = configuration as IConfigurationSection;
        int prefixLength = (makePathsRelative && rootSection != null) ? rootSection.Path.Length + 1 : 0;
        while (stack.Count > 0)
        {
            IConfiguration config = stack.Pop();
            // Don't include the sections value if we are removing paths, since it will be an empty key
            if (config is IConfigurationSection section && 
                (!makePathsRelative || config != configuration))
            {
                yield return new KeyValuePair<string, string>(
                    section.Path.Substring(prefixLength), 
                    section.Value);
            }
            foreach (IConfigurationSection child in config.GetChildren())
            {
                stack.Push(child);
            }
        }
    }                    
}

```

##### 2.1.2 configuration section（实现）

```c#
// 扩展接口
public interface IConfigurationSection : IConfiguration
{    
    string Path { get; }          
    string Key { get; }             
    string Value { get; set; }
}

// 实现
public class ConfigurationSection : IConfigurationSection
{       
    // path
    private readonly string _path;    
    public string Path => _path;

    // key
    private string _key; 
    public string Key
    {
        get
        {
            if (_key == null)
            {
                // Key is calculated lazily as last portion of Path
                _key = ConfigurationPath.GetSectionKey(_path);
            }
            return _key;
        }
    }
    
    // root configuration
    private readonly IConfigurationRoot _root;
    // value
    public string Value
    {
        get
        {
            return _root[Path];
        }
        set
        {
            _root[Path] = value;
        }
    }
    
    public string this[string key]
    {
        get
        {
            return _root[ConfigurationPath.Combine(Path, key)];
        }
        
        set
        {
            _root[ConfigurationPath.Combine(Path, key)] = value;
        }
    }
    
    public ConfigurationSection(IConfigurationRoot root, string path)
    {
        if (root == null)
        {
            throw new ArgumentNullException(nameof(root));
        }        
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }
        
        _root = root;
        _path = path;
    }    
    
    /* 方法，调用 configuration root 的对应方法 */
    
    // get section
    public IConfigurationSection GetSection(string key) => _root.GetSection(ConfigurationPath.Combine(Path, key));        
    // get children
    public IEnumerable<IConfigurationSection> GetChildren() => _root.GetChildrenImplementation(Path);        
    // get reload token
    public IChangeToken GetReloadToken() => _root.GetReloadToken();
}

public static class ConfigurationExtensions
{    
    // check configuration section exist
    public static bool Exists(this IConfigurationSection section)
    {
        if (section == null)
        {
            return false;
        }
        return section.Value != null || section.GetChildren().Any();
    }        
}

```

##### 2.1.3 configuration root（实现）

```c#
// 扩展接口
public interface IConfigurationRoot : IConfiguration
{
    IEnumerable<IConfigurationProvider> Providers { get; }
    void Reload();            
}

// 实现
public class ConfigurationRoot : IConfigurationRoot, IDisposable
{
    // configuration provider 集合
    private readonly IList<IConfigurationProvider> _providers;
    public IEnumerable<IConfigurationProvider> Providers => _providers;
    
    // change token 集合
    private readonly IList<IDisposable> _changeTokenRegistrations;
    private ConfigurationReloadToken _changeToken = new ConfigurationReloadToken();
            
    public string this[string key]
    {
        get
        {
            for (int i = _providers.Count - 1; i >= 0; i--)
            {
                IConfigurationProvider provider = _providers[i];
                
                if (provider.TryGet(key, out string value))
                {
                    return value;
                }
            }
            
            return null;
        }
        set
        {
            if (_providers.Count == 0)
            {
                throw new InvalidOperationException(SR.Error_NoSources);
            }
            
            foreach (IConfigurationProvider provider in _providers)
            {
                provider.Set(key, value);
            }
        }
    }
    
    public ConfigurationRoot(IList<IConfigurationProvider> providers)
    {
        if (providers == null)
        {
            throw new ArgumentNullException(nameof(providers));
        }
        
        _providers = providers;
        _changeTokenRegistrations = new List<IDisposable>(providers.Count);
        
        foreach (IConfigurationProvider p in providers)
        {
            p.Load();
            _changeTokenRegistrations.Add(
                // 绑定 token 和 token consumer，
                // 即每个 provider 的变化，都调用 raise changed（configuration 的 token）
                ChangeToken.OnChange(
                    () => p.GetReloadToken(), 
                    () => RaiseChanged()));
        }
    }
    
    private void RaiseChanged()
    {
        // 重新创建 change token（token 只能使用一次）
        ConfigurationReloadToken previousToken = Interlocked.Exchange(ref _changeToken, new ConfigurationReloadToken());
        previousToken.OnReload();
    }
       
    public void Dispose()
    {
        // dispose change token registrations
        foreach (IDisposable registration in _changeTokenRegistrations)
        {
            registration.Dispose();
        }        
        // dispose providers
        foreach (IConfigurationProvider provider in _providers)
        {
            (provider as IDisposable)?.Dispose();
        }
    }
    
    // 方法 - get reload token
    public IChangeToken GetReloadToken() => _changeToken;
    
    // 方法 - reload
    public void Reload()
    {
        foreach (IConfigurationProvider provider in _providers)
        {
            provider.Load();
        }
        
        RaiseChanged();
    }
    
    // 方法 - get section，-> new，不为 null & 不抛出异常
    public IConfigurationSection GetSection(string key) => new ConfigurationSection(this, key);
                                   
    // get children
    public IEnumerable<IConfigurationSection> GetChildren() => this.GetChildrenImplementation(null);
}

internal static class InternalConfigurationRootExtensions
{
   
    internal static IEnumerable<IConfigurationSection> GetChildrenImplementation(
        this IConfigurationRoot root, 
        string path)
    {
        return root.Providers
       		   	   .Aggregate(Enumerable.Empty<string>(), (seed, source) => source.GetChildKeys(seed, path))
            	   .Distinct(StringComparer.OrdinalIgnoreCase)
            	   .Select(key => root.GetSection(path == null ? key : ConfigurationPath.Combine(path, key)));
    }
}

```

###### 2.1.3.1 扩展方法 - debug view

```c#
public static class ConfigurationRootExtensions
{    
    public static string GetDebugView(this IConfigurationRoot root)
    {
        void RecurseChildren(
            StringBuilder stringBuilder,
            IEnumerable<IConfigurationSection> children,
            string indent)
        {
            foreach (IConfigurationSection child in children)
            {
                (string Value, IConfigurationProvider Provider) valueAndProvider = GetValueAndProvider(root, child.Path);
                
                if (valueAndProvider.Provider != null)
                {
                    stringBuilder.Append(indent)
                        		.Append(child.Key)
                        		.Append('=')
                        		.Append(valueAndProvider.Value)
                        		.Append(" (")
                        		.Append(valueAndProvider.Provider)
                        		.AppendLine(")");
                }
                else
                {
                    stringBuilder.Append(indent)
                        		.Append(child.Key)
                        		.AppendLine(":");
                }
                
                RecurseChildren(stringBuilder, child.GetChildren(), indent + "  ");
            }
        }
        
        var builder = new StringBuilder();        
        RecurseChildren(builder, root.GetChildren(), "");
        
        return builder.ToString();
    }
    
    private static (string Value, IConfigurationProvider Provider) GetValueAndProvider(
        IConfigurationRoot root,
        string key)
    {
        foreach (IConfigurationProvider provider in root.Providers.Reverse())
        {
            if (provider.TryGet(key, out string value))
            {
                return (value, provider);
            }
        }
        
        return (null, null);
    }
}

```

###### 2.1.3.2 configuration reload token

```c#
public class ConfigurationReloadToken : IChangeToken
{
    // 使用 cancellation token source
    private CancellationTokenSource _cts = new CancellationTokenSource();        

    public bool ActiveChangeCallbacks => true;        
    public bool HasChanged => _cts.IsCancellationRequested;
        
    public IDisposable RegisterChangeCallback(Action<object> callback, object state) => _cts.Token.Register(callback, state);        
    public void OnReload() => _cts.Cancel();
}

```

#### 2.2 configuration provider

```c#
public interface IConfigurationProvider
{    
    bool TryGet(string key, out string value);    
    void Set(string key, string value);    
    IChangeToken GetReloadToken();    
    void Load();    
    IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string parentPath);
}

```

##### 2.2.1 configuration provider (base)

```c#
public abstract class ConfigurationProvider : IConfigurationProvider
{
    // change token
    private ConfigurationReloadToken _reloadToken = new ConfigurationReloadToken();
    // kv 容器
    protected IDictionary<string, string> Data { get; set; }
    
    protected ConfigurationProvider()
    {
        Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
    
    // try get           
    public virtual bool TryGet(string key, out string value) => Data.TryGetValue(key, out value);
    // set
    public virtual void Set(string key, string value) => Data[key] = value;       
    // load
    public virtual void Load() { }

    // get child keys
    public virtual IEnumerable<string> GetChildKeys(
        IEnumerable<string> earlierKeys,
        string parentPath)
    {
        // 预结果
        var results = new List<string>();
        
        // 如果 parent path 为 null，results 中注入 empty kv_segment
        if (parentPath is null)
        {
            foreach (KeyValuePair<string, string> kv in Data)
            {
                results.Add(Segment(kv.Key, 0));
            }
        }
        // 否则，即 parent path 不为 null，
        else
        {
            Debug.Assert(ConfigurationPath.KeyDelimiter == ":");
            
            // 遍历 kv 容器的 kv pair 
            foreach (KeyValuePair<string, string> kv in Data)
            {
                // 如果 key 长度 > parent path 长度（即 key 是 parent path 的子串）
                if (kv.Key.Length > parentPath.Length &&
                    // key 以 parent path 开头
                    kv.Key.StartsWith(parentPath, StringComparison.OrdinalIgnoreCase) &&
                    // 中间以 “：” 分隔
                    kv.Key[parentPath.Length] == ':')
                {
                    // key 注入 results（预结果）
                    results.Add(Segment(kv.Key, parentPath.Length + 1));
                }
            }
        }
        
        // results 注入传入的 earliy keys？
        results.AddRange(earlierKeys);    
        // 排序
        results.Sort(ConfigurationKeyComparer.Comparison);
        
        return results;
    }
    
    private static string Segment(string key, int prefixLength)
    {
        int indexOf = key.IndexOf(
            ConfigurationPath.KeyDelimiter, 
            prefixLength, 
            StringComparison.OrdinalIgnoreCase);
        
        return indexOf < 0 
            ? key.Substring(prefixLength) 
            : key.Substring(prefixLength, indexOf - prefixLength);
    }
    
    // get reload token
    public IChangeToken GetReloadToken()
    {
        return _reloadToken;
    }
    
    // on reload
    protected void OnReload()
    {
        ConfigurationReloadToken previousToken = Interlocked.Exchange(ref _reloadToken, new ConfigurationReloadToken());
        previousToken.OnReload();
    }
        
    public override string ToString() => $"{GetType().Name}";
}

```

##### 2.2.2 configuration key comparer

```c#
public class ConfigurationKeyComparer : IComparer<string>
{
    private static readonly string[] _keyDelimiterArray = new[] { ConfigurationPath.KeyDelimiter };
        
    public static ConfigurationKeyComparer Instance { get; } = new ConfigurationKeyComparer();        
    internal static Comparison<string> Comparison { get; } = Instance.Compare;
            
    public int Compare(string x, string y)
    {
        string[] xParts = x?.Split(
            _keyDelimiterArray, 
            StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        string[] yParts = y?.Split(
            _keyDelimiterArray, 
            StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        
        // Compare each part until we get two parts that are not equal
        for (int i = 0; i < Math.Min(xParts.Length, yParts.Length); i++)
        {
            x = xParts[i];
            y = yParts[i];
            
            int value1 = 0;
            int value2 = 0;
            
            bool xIsInt = x != null && int.TryParse(x, out value1);
            bool yIsInt = y != null && int.TryParse(y, out value2);
            
            int result;
            
            if (!xIsInt && !yIsInt)
            {
                // Both are strings
                result = string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            }
            else if (xIsInt && yIsInt)
            {
                // Both are int
                result = value1 - value2;
            }
            else
            {
                // Only one of them is int
                result = xIsInt ? -1 : 1;
            }
            
            if (result != 0)
            {
                // One of them is different
                return result;
            }
        }
        
        // If we get here, the common parts are equal.
        // If they are of the same length, then they are totally identical
        return xParts.Length - yParts.Length;
    }
}

```

#### 2.3 configuration source

```c#
public interface IConfigurationSource
{    
    IConfigurationProvider Build(IConfigurationBuilder builder);
}

```

#### 2.4 configuration builder

```c#
public interface IConfigurationBuilder
{    
    IDictionary<string, object> Properties { get; }        
    IList<IConfigurationSource> Sources { get; }    
    
    IConfigurationBuilder Add(IConfigurationSource source);        
    IConfigurationRoot Build();
}

```

##### 2.4.1 configuration builder（实现）

```c#
public class ConfigurationBuilder : IConfigurationBuilder
{   
    // configuration source 集合
    public IList<IConfigurationSource> Sources { get; } = new List<IConfigurationSource>();            
    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();
        
    public IConfigurationBuilder Add(IConfigurationSource source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        
        Sources.Add(source);
        return this;
    }
        
    public IConfigurationRoot Build()
    {
        var providers = new List<IConfigurationProvider>();
        foreach (IConfigurationSource source in Sources)
        {
            IConfigurationProvider provider = source.Build(this);
            providers.Add(provider);
        }
        return new ConfigurationRoot(providers);
    }
}

```

##### 2.4.2 扩展方法 - add source (action)

```c#
public static class ConfigurationExtensions
{
    public static IConfigurationBuilder Add<TSource>(
        this IConfigurationBuilder builder, 
        Action<TSource> configureSource) where TSource : IConfigurationSource, new()
    {
        var source = new TSource();
        configureSource?.Invoke(source);
        return builder.Add(source);
    }   
}

```

#### 2.5 configuration binder

```c#
public static class ConfigurationBinder
{
    private const BindingFlags DeclaredOnlyLookup = BindingFlags.Public | 
        										BindingFlags.NonPublic | 
										        BindingFlags.Instance | 
										        BindingFlags.Static | 
										        BindingFlags.DeclaredOnly;
    
    // convert value if possible
    private static bool TryConvertValue(
        Type type, 
        string value, 
        string path, 
        out object result, 
        out Exception error)
    {
        error = null;
        result = null;
        
        if (type == typeof(object))
        {
            result = value;
            return true;
        }
        
        if (type.IsGenericType && 
            type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }
            
            return TryConvertValue(Nullable.GetUnderlyingType(type), value, path, out result, out error);
        }
        
        TypeConverter converter = TypeDescriptor.GetConverter(type);
        
        if (converter.CanConvertFrom(typeof(string)))
        {
            try
            {
                result = converter.ConvertFromInvariantString(value);
            }
            catch (Exception ex)
            {
                error = new InvalidOperationException(SR.Format(SR.Error_FailedBinding, path, type), ex);
            }
            return true;
        }
        
        if (type == typeof(byte[]))
        {
            try
            {
                result = Convert.FromBase64String(value);
            }
            catch (FormatException ex)
            {
                error = new InvalidOperationException(SR.Format(SR.Error_FailedBinding, path, type), ex);
            }
            return true;
        }
        
        return false;
    }
    
    
    private static Type FindOpenGenericInterface(Type expected, Type actual)
    {
        if (actual.IsGenericType &&
            actual.GetGenericTypeDefinition() == expected)
        {
            return actual;
        }
        
        Type[] interfaces = actual.GetInterfaces();
        foreach (Type interfaceType in interfaces)
        {
            if (interfaceType.IsGenericType &&
                interfaceType.GetGenericTypeDefinition() == expected)
            {
                return interfaceType;
            }
        }
        return null;
    }                                          
}

```

##### 2.5.1 binder options

```c#
public class BinderOptions
{    
    public bool BindNonPublicProperties { get; set; }
}

```

##### 2.5.2 get

```c#
public static class ConfigurationBinder
{
    // by t
    public static T Get<T>(this IConfiguration configuration) => configuration.Get<T>(_ => { });
        
    // by t & options
    public static T Get<T>(
        this IConfiguration configuration, 
        Action<BinderOptions> configureOptions)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }
        
        object result = configuration.Get(typeof(T), configureOptions);
        
        if (result == null)
        {
            return default(T);
        }
        return (T)result;
    }
            
    // by type
    public static object Get(this IConfiguration configuration, Type type) => configuration.Get(type, _ => { });
       
    // by type & options
    public static object Get(
        this IConfiguration configuration, 
        Type type, 
        Action<BinderOptions> configureOptions)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }
        
        // 配置 binder options
        var options = new BinderOptions();
        configureOptions?.Invoke(options);
        
        // 调用 bind instance
        return BindInstance(type, instance: null, config: configuration, options: options);
    }
}

```

##### 2.5.3 get value

```c#
public static class ConfigurationBinder
{
    // by t & default
    public static T GetValue<T>(
        this IConfiguration configuration, 
        string key)
    {
        return GetValue(configuration, key, default(T));
    }
         
    // by t & specific default 
    public static T GetValue<T>(
        this IConfiguration configuration, 
        string key, 
        T defaultValue)
    {
        return (T)GetValue(configuration, typeof(T), key, defaultValue);
    }
    
    // by type & null as default
    public static object GetValue(
        this IConfiguration configuration, 
        Type type, 
        string key)
    {
        return GetValue(configuration, type, key, defaultValue: null);
    }
         
    // by type & specific default
    public static object GetValue(
        this IConfiguration configuration, 
        Type type, 
        string key, 
        object defaultValue)
    {
        // 先解析 configuration section
        IConfigurationSection section = configuration.GetSection(key);
        
        string value = section.Value;
        if (value != null)
        {
            return ConvertValue(type, value, section.Path);
        }
        
        return defaultValue;
    }
    
    // convert value
    private static object ConvertValue(Type type, string value, string path)
    {
        object result;
        Exception error;
        
        TryConvertValue(type, value, path, out result, out error);
        
        if (error != null)
        {
            throw error;
        }
        
        return result;
    }
}

```

##### 2.5.4 bind

```c#
public static class ConfigurationBinder
{
    // bind configuration[key] to "instance"
    public static void Bind(this IConfiguration configuration, string key, object instance) => 
        configuration.GetSection(key).Bind(instance);

    // bind configuration to "instance"
    public static void Bind(this IConfiguration configuration, object instance) => 
        configuration.Bind(instance, o => { });

    // bind configuration to "instance"    
    public static void Bind(
        this IConfiguration configuration, 
        object instance, 
        Action<BinderOptions> configureOptions)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }
        
        if (instance != null)
        {
            // 配置 binder options
            var options = new BinderOptions();
            configureOptions?.Invoke(options);
            
            // 调用 bind instance
            BindInstance(instance.GetType(), instance, configuration, options);
        }
    }
    
    /* bind instance，configuration => instance (object) */
    private static object BindInstance(
        Type type, 
        object instance, 
        IConfiguration config, 
        BinderOptions options)
    {
        // if binding IConfigurationSection, break early
        if (type == typeof(IConfigurationSection))
        {
            return config;
        }
        
        // configuration 转换（协变）为 configuration section
        var section = config as IConfigurationSection;
        
        // 初始化
        string configValue = section?.Value;
        object convertedValue;
        Exception error;
        
        // 如果 config value 不为 null，
        if (configValue != null && 
            // 并且 config value 可以 convert，-> 返回 converted value 或抛出异常       
            TryConvertValue(type, configValue, section.Path, out convertedValue, out error))
        {
            // -> 返回 convented value 或者 抛出异常            
            if (error != null)
            {
                throw error;
            }            
            // Leaf nodes are always reinitialized
            return convertedValue;
        }
        
        //（由上），config value 为 null（collection 或者 complex type），
        //  或者 config value 不能 convert
        
        // 如果 config 不为 null 且有 child configuration section（嵌套 configuration，是 collection）
        if (config != null &&             
            config.GetChildren().Any())
        {
            // 如果 instance（预结果）为 null，
            if (instance == null)
            {
                // -> try bind collection
                // We are already done if binding to a new collection instance worked
                instance = AttemptBindToCollectionInterfaces(type, config, options);                        
                if (instance != null)
                {
                    return instance;
                }
                
                // （由上），不能 bind collection，-> 创建 instance
                instance = CreateInstance(type);
            }
            
            // （由上）否则，即 instance（结果） 不为 null
            
            // 如果 type 实现了 dictionary<,> 接口，-> bind dictionary
            Type collectionInterface = FindOpenGenericInterface(typeof(IDictionary<,>), type);
            if (collectionInterface != null)
            {
                BindDictionary(instance, collectionInterface, config, options);
            }
            // 如果 type 是 array，-> bind array
            else if (type.IsArray)
            {
                instance = BindArray((Array)instance, config, options);
            }
            // 否则，
            else
            {
                // 如果 type 实现了 collection<> 接口，-> bind collection
                collectionInterface = FindOpenGenericInterface(typeof(ICollection<>), type);
                if (collectionInterface != null)
                {
                    BindCollection(instance, collectionInterface, config, options);
                }
                // 否则，-> bind non scalar
                else
                {
                    BindNonScalar(config, instance, options);
                }
            }
        }
        
        // （由上），
        // config 为 null（即没有对应的 configuration，key 不匹配），
        // 或者 config 没有 children configuration（null），
        // -> 返回 instance => bypass bind process
        return instance;
    }    
}

```

##### 2.5.4-a for null instance

###### 2.5.4.a1 attempt bind to collection interface

```c#
public static class ConfigurationBinder
{
    // Try to create an array/dictionary instance to back various collection interfaces
    private static object AttemptBindToCollectionInterfaces(
        // the instance type
        Type type, 
        IConfiguration config, 
        BinderOptions options)
    {
        if (!type.IsInterface)
        {
            return null;
        }
        
        // 如果 (instance) type 实现了 readOnlyList<> 接口，-> bind to collection
        Type collectionInterface = FindOpenGenericInterface(typeof(IReadOnlyList<>), type);
        if (collectionInterface != null)
        {
            // IEnumerable<T> is guaranteed to have exactly one parameter
            return BindToCollection(type, config, options);
        }
        
        // 如果 (instance) type 实现了 readOnlyDictionary<,> 接口
        collectionInterface = FindOpenGenericInterface(typeof(IReadOnlyDictionary<,>), type);
        if (collectionInterface != null)
        {
            // 解析 dictionary type
            Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(
                type.GenericTypeArguments[0], 
                type.GenericTypeArguments[1]);
            // 根据 dictionary type 创建 instance
            object instance = Activator.CreateInstance(dictionaryType);
            
            // bind dictionary，返回 instance
            BindDictionary(instance, dictionaryType, config, options);
            return instance;
        }
        
        // 如果 (instance) type 实现了 dictionary<,> 接口
        collectionInterface = FindOpenGenericInterface(typeof(IDictionary<,>), type);
        if (collectionInterface != null)
        {
            // 创建 instance
            object instance = Activator.CreateInstance(
                typeof(Dictionary<,>).MakeGenericType(
                    type.GenericTypeArguments[0], 
                    type.GenericTypeArguments[1]));
            
            // bind  dictionary, 返回 instance
            BindDictionary(instance, collectionInterface, config, options);
            return instance;
        }
        
        // 如果 (instance) type 实现了 readOnlyCollection<> 接口，-> bind to collection
        collectionInterface = FindOpenGenericInterface(typeof(IReadOnlyCollection<>), type);
        if (collectionInterface != null)
        {
            // IReadOnlyCollection<T> is guaranteed to have exactly one parameter
            return BindToCollection(type, config, options);
        }
        
        // 如果 (instance) type 实现了 collection<> 接口，-> bind to collection
        collectionInterface = FindOpenGenericInterface(typeof(ICollection<>), type);
        if (collectionInterface != null)
        {
            // ICollection<T> is guaranteed to have exactly one parameter
            return BindToCollection(type, config, options);
        }
        
        // 如果 (instance) type 实现了 enumerable<> 接口，-> bind to collection
        collectionInterface = FindOpenGenericInterface(typeof(IEnumerable<>), type);
        if (collectionInterface != null)
        {
            // IEnumerable<T> is guaranteed to have exactly one parameter
            return BindToCollection(type, config, options);
        }
        
        return null;
    }     
    
    // bind to collection
    private static object BindToCollection(Type type, IConfiguration config, BinderOptions options)
    {
        // 解析 (instance) type
        Type genericType = typeof(List<>).MakeGenericType(type.GenericTypeArguments[0]);
        // 根据 type 创建 instance
        object instance = Activator.CreateInstance(genericType);
        
        // 调用 bind collection，返回 instance
        BindCollection(instance, genericType, config, options);
        return instance;
    }
}

```

###### 2.5.4.a2 create instance

```c#
public static class ConfigurationBinder
{
    // create instance object
    private static object CreateInstance(Type type)
    {
        // (instance) type 是 interface 或者 abstract，-> 抛出异常
        if (type.IsInterface || type.IsAbstract)
        {
            throw new InvalidOperationException(
                SR.Format(SR.Error_CannotActivateAbstractOrInterface, type));
        }
        
        // (instanc) type 是 array，
        if (type.IsArray)
        {
            // 如果是 多维数组 ，-> 抛出异常
            if (type.GetArrayRank() > 1)
            {
                throw new InvalidOperationException(
                    SR.Format(SR.Error_UnsupportedMultidimensionalArray, type));
            }
            // （由上，单维度数组），-> create array instance
            return Array.CreateInstance(type.GetElementType(), 0);
        }
        
        // 如果 (instance) type 不是 value type（complex type，即复合类型，class）
        if (!type.IsValueType)
        {
            // 如果 type 没有 default constructor（public & no parameter），-> 抛出异常
            bool hasDefaultConstructor = type.GetConstructors(DeclaredOnlyLookup)
                						   .Any(ctor => ctor.IsPublic && ctor.GetParameters().Length == 0);
            if (!hasDefaultConstructor)
            {
                throw new InvalidOperationException(
                    SR.Format(SR.Error_MissingParameterlessConstructor, type));
            }
        }
        
        try
        {
            // 由 activator 创建 instance
            return Activator.CreateInstance(type);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(SR.Format(SR.Error_FailedToActivate, type), ex);
        }
    }
}

```

##### 2.5.4-b bind by type

###### 2.5.4.b1 bind dictionary

```c#
public static class ConfigurationBinder
{
    private static void BindDictionary(
        object dictionary, 
        Type dictionaryType, 
        IConfiguration config, 
        BinderOptions options)
    {
        // IDictionary<K,V> is guaranteed to have exactly two parameters
        
        // 解析 key type
        Type keyType = dictionaryType.GenericTypeArguments[0];
        // 解析 value type
        Type valueType = dictionaryType.GenericTypeArguments[1];
        
        // 如果 key type 不是 string 和 enum，-> 不支持，抛出异常
        bool keyTypeIsEnum = keyType.IsEnum;        
        if (keyType != typeof(string) && !keyTypeIsEnum)
        {
            // We only support string and enum keys
            return;
        }
        
        // 解析 property "item" setter
        PropertyInfo setter = dictionaryType.GetProperty("Item", DeclaredOnlyLookup);
        
        // 遍历 configuration section 的 child，
        foreach (IConfigurationSection child in config.GetChildren())
        {
            // （递归）bind instance（dictionary item value）
            object item = BindInstance(
                type: valueType,
                instance: null,
                config: child,
                options: options);
            
            // 将 item & key 注入 dictionary
            if (item != null)
            {
                if (keyType == typeof(string))
                {
                    string key = child.Key;
                    setter.SetValue(dictionary, item, new object[] { key });
                }
                else if (keyTypeIsEnum)
                {
                    object key = Enum.Parse(keyType, child.Key);
                    setter.SetValue(dictionary, item, new object[] { key });
                }
            }
        }
    }
}

```

###### 2.5.4.b2 bind array

```c#
public static class ConfigurationBinder
{
    private static Array BindArray(
        Array source, 
        IConfiguration config, 
        BinderOptions options)
    {
        // 解析 configura 的 全部 child
        IConfigurationSection[] children = config.GetChildren().ToArray();
        // 解析 array 原有数据项长度
        int arrayLength = source.Length;
        // 解析 array 的 element type
        Type elementType = source.GetType().GetElementType();
        
        // 创建新的 array（预结果，克隆原有数据，追加 configuration 元素）
        var newArray = Array.CreateInstance(elementType, arrayLength + children.Length);
        
        // 如果 array 有数据，-> 克隆到 new array
        if (arrayLength > 0)
        {
            Array.Copy(source, newArray, arrayLength);
        }
        
        // 遍历 configuration child
        for (int i = 0; i < children.Length; i++)
        {
            try
            {
                // 调用 bind instance -> item
                object item = BindInstance(
                    type: elementType,
                    instance: null,
                    config: children[i],
                    options: options);
                
                // 如果 item 不为 null，注入 new array
                if (item != null)
                {
                    newArray.SetValue(item, arrayLength + i);
                }
            }
            catch
            {
            }
        }
        
        return newArray;
    }
}

```

###### 2.5.4.b3 bind collection

```c#
public static class ConfigurationBinder
{    
    private static void BindCollection(
        object collection, 
        Type collectionType, 
        IConfiguration config, 
        BinderOptions options)
    {
        // ICollection<T> is guaranteed to have exactly one parameter
        
        // 解析 collection 的 element type
        Type itemType = collectionType.GenericTypeArguments[0];
        
        // 解析 collection 的 add 方法
        MethodInfo addMethod = collectionType.GetMethod("Add", DeclaredOnlyLookup);
        
        // 遍历 configuration 的 child，
        foreach (IConfigurationSection section in config.GetChildren())
        {
            try
            {
                // 调用 bind instance -> collection item
                object item = BindInstance(
                    type: itemType,
                    instance: null,
                    config: section,
                    options: options);
                
                // 如果 item 不为 null，注入 collection
                if (item != null)
                {
                    addMethod.Invoke(collection, new[] { item });
                }
            }
            catch
            {
            }
        }
    }
}

```

###### 2.5.4.b4 bind nonscalar

```c#
public static class ConfigurationBinder
{    
    private static void BindNonScalar(
        this IConfiguration configuration, 
        object instance, 
        BinderOptions options)
    {
        if (instance != null)
        {
            // 遍历 instance 的 property，
            foreach (PropertyInfo property in GetAllProperties(instance.GetType()))
            {
                // bind property
                BindProperty(property, instance, configuration, options);
            }
        }
    }
    
    // get all property
    private static IEnumerable<PropertyInfo> GetAllProperties(Type type)
    {
        // 预结果
        var allProperties = new List<PropertyInfo>();
        
        // 遍历注入
        do
        {
            allProperties.AddRange(type.GetProperties(DeclaredOnlyLookup));
            type = type.BaseType;
        }
        while (type != typeof(object));
        
        return allProperties;
    }        
}

```

###### 2.5.4.b5 bind property

```c#
public static class ConfigurationBinder
{
    private static void BindProperty(
        PropertyInfo property, 
        object instance, 
        IConfiguration config, 
        BinderOptions options)
    {
        // We don't support set only, non public, or indexer properties
        
        // 如果 get 为 null，
        // 或者 bind options 没有标记 bind non public property，但是 property 不是 public，
        // 或者 property 的 setter 需要参数，
        // -> 不支持，直接返回（不报错！）
        if (property.GetMethod == null ||
            (!options.BindNonPublicProperties && !property.GetMethod.IsPublic) ||
            property.GetMethod.GetParameters().Length > 0)
        {
            return;
        }
        
        // 从 instance 解析 property value
        object propertyValue = property.GetValue(instance);
        // property 是否有 setter
        bool hasSetter = property.SetMethod != null && 
            			(property.SetMethod.IsPublic || options.BindNonPublicProperties);
        
        // 如果 property value 为 null，并且没有 setter，直接返回（不报错！）
        if (propertyValue == null && !hasSetter)
        {
            // Property doesn't have a value and we cannot set it so there is no point in going further down the graph
            return;
        }
        
        // 从 configuration 解析 property value
        propertyValue = GetPropertyValue(property, instance, config, options);
        // 注入 property value
        if (propertyValue != null && hasSetter)
        {
            property.SetValue(instance, propertyValue);
        }
    }
           
    // get property value from configuration
    private static object GetPropertyValue(
        PropertyInfo property, 
        object instance, 
        IConfiguration config, 
        BinderOptions options)
    {
        string propertyName = GetPropertyName(property);
        return BindInstance(
            property.PropertyType,
            property.GetValue(instance),
            config.GetSection(propertyName),
            options);
    }
    
    private static string GetPropertyName(MemberInfo property)
    {
        if (property == null)
        {
            throw new ArgumentNullException(nameof(property));
        }
        
        // 如果 property 标记了 configuration key name attribute，
        // 返回 attribute name
        // （instance 的 property 标记同 configuration section 同名的 attribute）
        foreach (var attributeData in property.GetCustomAttributesData())
        {
            if (attributeData.AttributeType != typeof(ConfigurationKeyNameAttribute))
            {
                continue;
            }
            
            // Ensure ConfigurationKeyName constructor signature matches expectations
            if (attributeData.ConstructorArguments.Count != 1)
            {
                break;
            }
            
            // Assumes ConfigurationKeyName constructor first arg is the string key name
            string name = attributeData.ConstructorArguments[0]
                					 .Value?
                					 .ToString();
            
            return !string.IsNullOrWhiteSpace(name) ? name : property.Name;
        }
        
        return property.Name;
    }
}

```

###### 2.5.4.b6 configuration key name attribute

```c#
[AttributeUsage(AttributeTargets.Property)]
public sealed class ConfigurationKeyNameAttribute : Attribute
{
    public string Name { get; }
    public ConfigurationKeyNameAttribute(string name) => Name = name;        
}

```

### 3. variety of configuration

#### 3.1 chained configuration

```c#
public static class ChainedBuilderExtensions
{    
    public static IConfigurationBuilder AddConfiguration(
        this IConfigurationBuilder configurationBuilder, 
        IConfiguration config) => AddConfiguration(configurationBuilder, config, shouldDisposeConfiguration: false);
        
    public static IConfigurationBuilder AddConfiguration(
        this IConfigurationBuilder configurationBuilder, 
        IConfiguration config, 
        bool shouldDisposeConfiguration)
    {
        if (configurationBuilder == null)
        {
            throw new ArgumentNullException(nameof(configurationBuilder));
        }
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }
        
        configurationBuilder.Add(
            new ChainedConfigurationSource
            {
                Configuration = config,
                ShouldDisposeConfiguration = shouldDisposeConfiguration,
            });
        
        return configurationBuilder;
    }
}
```

##### 3.1.1 chained configuration source

```c#
public class ChainedConfigurationSource : IConfigurationSource
{    
    public IConfiguration Configuration { get; set; }        
    public bool ShouldDisposeConfiguration { get; set; }
        
    public IConfigurationProvider Build(IConfigurationBuilder builder) => new ChainedConfigurationProvider(this);
}

```

##### 3.1.2 chained configuration provider

```c#
public class ChainedConfigurationProvider : IConfigurationProvider, IDisposable
{
    private readonly IConfiguration _config;
    private readonly bool _shouldDisposeConfig;
        
    public ChainedConfigurationProvider(ChainedConfigurationSource source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        if (source.Configuration == null)
        {
            throw new ArgumentException(
                SR.Format(SR.InvalidNullArgument, "source.Configuration"), 
                nameof(source));
        }
        
        _config = source.Configuration;
        _shouldDisposeConfig = source.ShouldDisposeConfiguration;
    }
        
    public bool TryGet(string key, out string value)
    {
        value = _config[key];
        return !string.IsNullOrEmpty(value);
    }
   
    public void Set(string key, string value) => _config[key] = value;
        
    public IChangeToken GetReloadToken() => _config.GetReloadToken();
           
    public void Load() { }
        
    public IEnumerable<string> GetChildKeys(
        IEnumerable<string> earlierKeys,
        string parentPath)
    {
        // 解析 parent path 下的 section，（如果 parent path 为 null，解析到 root）
        IConfiguration section = parentPath == null ? _config : _config.GetSection(parentPath);
        
        // 遍历 section 的 child，注入 key 到 keys（预结果）
        var keys = new List<string>();
        foreach (IConfigurationSection child in section.GetChildren())
        {
            keys.Add(child.Key);
        }
        // 注入原有数据
        keys.AddRange(earlierKeys);
        // 排序
        keys.Sort(ConfigurationKeyComparer.Comparison);
        
        return keys;
    }
       
    public void Dispose()
    {
        // 如果标记了 should dispose config，dispose 包裹的 config
        if (_shouldDisposeConfig)
        {
            (_config as IDisposable)?.Dispose();
        }
    }
}

```

#### 3.2 memory configuration

```c#
public static class MemoryConfigurationBuilderExtensions
{    
    public static IConfigurationBuilder AddInMemoryCollection(this IConfigurationBuilder configurationBuilder)
    {
        if (configurationBuilder == null)
        {
            throw new ArgumentNullException(nameof(configurationBuilder));
        }
        
        configurationBuilder.Add(new MemoryConfigurationSource());
        return configurationBuilder;
    }
    
    public static IConfigurationBuilder AddInMemoryCollection(
        this IConfigurationBuilder configurationBuilder,
        IEnumerable<KeyValuePair<string, string>> initialData)
    {
        if (configurationBuilder == null)
        {
            throw new ArgumentNullException(nameof(configurationBuilder));
        }
        
        configurationBuilder.Add(
            new MemoryConfigurationSource { InitialData = initialData });
        return configurationBuilder;
    }
}

```

##### 3.2.1 memory configuration source

```c#
public class MemoryConfigurationSource : IConfigurationSource
{        
    public IEnumerable<KeyValuePair<string, string>> InitialData { get; set; }
       
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new MemoryConfigurationProvider(this);
    }
}

```

##### 3.2.2 memory configuration provider

```c#
public class MemoryConfigurationProvider : ConfigurationProvider, IEnumerable<KeyValuePair<string, string>>
{
    private readonly MemoryConfigurationSource _source;
        
    public MemoryConfigurationProvider(MemoryConfigurationSource source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        
        _source = source;
        
        if (_source.InitialData != null)
        {
            foreach (KeyValuePair<string, string> pair in _source.InitialData)
            {
                Data.Add(pair.Key, pair.Value);
            }
        }
    }
    
    public void Add(string key, string value)
    {
        Data.Add(key, value);
    }
    
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        return Data.GetEnumerator();
    }
    
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

```

#### 3.3 command line configuration

```c#
public static class CommandLineConfigurationExtensions
{
  
    // The values passed on the command line, in the <c>args</c> string array, should be a set of keys prefixed with 
    // two dashes ("--") and then values, separate by either the equals sign ("=") or a space (" ").   
    // 
    // A forward slash ("/") can be used as an alternative prefix, with either equals or space, and when using 
    // an equals sign the prefix can be left out altogether.
    //
    // There are five basic alternative formats for arguments:
    //   key1=value1 --key2=value2 /key3=value3 --key4 value4 /key5 value5
    //
    // A simple console application that has five values.
    //   dotnet run key1=value1 --key2=value2 /key3=value3 --key4 value4 /key5 value5
    
    public static IConfigurationBuilder AddCommandLine(
        this IConfigurationBuilder configurationBuilder, 
        string[] args)
    {
        return configurationBuilder.AddCommandLine(args, switchMappings: null);
    }
        
    // The "switchMappings" allows additional formats for alternative short and alias keys to be used from the command line. 
    // Also see the basic version of <c>AddCommandLine</c> for the standard formats supported.
    //
    // Short keys start with a single dash ("-") and are mapped to the main key name (without prefix), and can be used 
    // with either equals or space. The single dash mappings are intended to be used for shorter alternative switches.
    
    // Note that a single dash switch cannot be accessed directly, but must have a switch mapping defined and accessed 
    // using the full key. Passing an undefined single dash argument will cause as <c>FormatException</c>.
    
    // There are two formats for short arguments:
    //   <c>-k1=value1 -k2 value2</c>.
   
    // Alias key definitions start with two dashes ("--") and are mapped to the main key name (without prefix), and can be 
    // used in place of the normal key. They also work when a forward slash prefix is used in the command line (but not with 
    // the no prefix equals format).
    
    // here are only four formats for aliased arguments:
    //   "--alt3=value3 /alt4=value4 --alt5 value5 /alt6 value6"
   
    // A simple console application that has two short and four alias switch mappings defined.    
    //   dotnet run -k1=value1 -k2 value2 --alt3=value2 /alt4=value3 --alt5 value5 /alt6 value6
    
    /*
         using Microsoft.Extensions.Configuration;
         using System;
         using System.Collections.Generic;
    
         namespace CommandLineSample
         {
            public class Program
            {
                public static void Main(string[] args)
                {
                    var switchMappings = new Dictionary&lt;string, string&gt;()
                    {
                        { "-k1", "key1" },
                        { "-k2", "key2" },
                        { "--alt3", "key3" },
                        { "--alt4", "key4" },
                        { "--alt5", "key5" },
                        { "--alt6", "key6" },
                    };
                    var builder = new ConfigurationBuilder();
                    builder.AddCommandLine(args, switchMappings);
    
                    var config = builder.Build();
    
                    Console.WriteLine($"Key1: '{config["Key1"]}'");
                    Console.WriteLine($"Key2: '{config["Key2"]}'");
                    Console.WriteLine($"Key3: '{config["Key3"]}'");
                    Console.WriteLine($"Key4: '{config["Key4"]}'");
                    Console.WriteLine($"Key5: '{config["Key5"]}'");
                    Console.WriteLine($"Key6: '{config["Key6"]}'");
                }
            }
         }
    */
    
    public static IConfigurationBuilder AddCommandLine(
        this IConfigurationBuilder configurationBuilder,
        string[] args,
        IDictionary<string, string> switchMappings)
    {
        configurationBuilder.Add(
            new CommandLineConfigurationSource 
            { 
                Args = args, 
                SwitchMappings = switchMappings 
		   });
        return configurationBuilder;
    }
       
    public static IConfigurationBuilder AddCommandLine(
        this IConfigurationBuilder builder, 
        Action<CommandLineConfigurationSource> configureSource) => builder.Add(configureSource);
}

```

##### 3.3.1 command line configuration source

```c#
public class CommandLineConfigurationSource : IConfigurationSource
{    
    public IDictionary<string, string> SwitchMappings { get; set; }    
    public IEnumerable<string> Args { get; set; }
        
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new CommandLineConfigurationProvider(Args, SwitchMappings);
    }
}

```

##### 3.3.2 command line configuration provider

```c#
public class CommandLineConfigurationProvider : ConfigurationProvider
{
    private readonly Dictionary<string, string> _switchMappings;
    
    protected IEnumerable<string> Args { get; private set; }
        
    public CommandLineConfigurationProvider(
        IEnumerable<string> args, 
        IDictionary<string, string> switchMappings = null)
    {
        Args = args ?? throw new ArgumentNullException(nameof(args));
        
        if (switchMappings != null)
        {
            _switchMappings = GetValidatedSwitchMappingsCopy(switchMappings);
        }
    }
    
    private Dictionary<string, string> GetValidatedSwitchMappingsCopy(IDictionary<string, string> switchMappings)
    {
        // The dictionary passed in might be constructed with a case-sensitive comparer
        // However, the keys in configuration providers are all case-insensitive
        // So we check whether the given switch mappings contain duplicated keys with case-insensitive comparer
        
        // 创建 switch mapping copy（克隆），忽略大小写（预结果）
        var switchMappingsCopy = new Dictionary<string, string>(switchMappings.Count, StringComparer.OrdinalIgnoreCase);
        
        // 遍历传入的 switch mapping，
        foreach (KeyValuePair<string, string> mapping in switchMappings)
        {
            // 如果 mapping key 不是 "-" 或者 "--" 开头，-> 抛出异常
            // only keys start with "--" or "-" are acceptable           
            if (!mapping.Key.StartsWith("-") && !mapping.Key.StartsWith("--"))
            {
                throw new ArgumentException(
                    SR.Format(SR.Error_InvalidSwitchMapping, mapping.Key),
                    nameof(switchMappings));
            }
            // 如果 mapping key 重复，-> 抛出异常
            if (switchMappingsCopy.ContainsKey(mapping.Key))
            {
                throw new ArgumentException(
                    SR.Format(SR.Error_DuplicatedKeyInSwitchMappings, mapping.Key),
                    nameof(switchMappings));
            }
            
            // （由上，key 是 "-" 或者 "--" 开头，且没有重复），-> 注入 mapping copy
            switchMappingsCopy.Add(mapping.Key, mapping.Value);
        }
        
        return switchMappingsCopy;
    }
    
    public override void Load()
    {
        // 预结果
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string key, value;
        
        // 遍历 args，（处理字符串）
        using (IEnumerator<string> enumerator = Args.GetEnumerator())
        {
            while (enumerator.MoveNext())
            {
                string currentArg = enumerator.Current;
                int keyStartIndex = 0;
                
                if (currentArg.StartsWith("--"))
                {
                    keyStartIndex = 2;
                }
                else if (currentArg.StartsWith("-"))
                {
                    keyStartIndex = 1;
                }
                else if (currentArg.StartsWith("/"))
                {
                    // "/SomeSwitch" is equivalent to "--SomeSwitch" when interpreting switch mappings
                    // So we do a conversion to simplify later processing
                    currentArg = $"--{currentArg.Substring(1)}";
                    keyStartIndex = 2;
                }
                
                int separator = currentArg.IndexOf('=');
                
                if (separator < 0)
                {
                    // If there is neither equal sign nor prefix in current arugment, it is an invalid format
                    if (keyStartIndex == 0)
                    {
                        // Ignore invalid formats
                        continue;
                    }
                    
                    // If the switch is a key in given switch mappings, interpret it
                    if (_switchMappings != null && _switchMappings.TryGetValue(currentArg, out string mappedKey))
                    {
                        key = mappedKey;
                    }
                    // If the switch starts with a single "-" and it isn't in given mappings , it is an invalid usage so ignore it
                    else if (keyStartIndex == 1)
                    {
                        continue;
                    }
                    // Otherwise, use the switch name directly as a key
                    else
                    {
                        key = currentArg.Substring(keyStartIndex);
                    }
                    
                    string previousKey = enumerator.Current;
                    if (!enumerator.MoveNext())
                    {
                        // ignore missing values
                        continue;
                    }
                    
                    value = enumerator.Current;
                }
                else
                {
                    string keySegment = currentArg.Substring(0, separator);
                    
                    // If the switch is a key in given switch mappings, interpret it
                    if (_switchMappings != null && _switchMappings.TryGetValue(keySegment, out string mappedKeySegment))
                    {
                        key = mappedKeySegment;
                    }
                    // If the switch starts with a single "-" and it isn't in given mappings , it is an invalid usage
                    else if (keyStartIndex == 1)
                    {
                        throw new FormatException(SR.Format(SR.Error_ShortSwitchNotDefined, currentArg));
                    }
                    // Otherwise, use the switch name directly as a key
                    else
                    {
                        key = currentArg.Substring(keyStartIndex, separator - keyStartIndex);
                    }
                    
                    value = currentArg.Substring(separator + 1);
                }
                
                // Override value when key is duplicated. So we always have the last argument win.
                data[key] = value;
            }
        }
        
        Data = data;
    }        
}

```

#### 3.4 environment variable configuration

```c#
public static class EnvironmentVariablesExtensions
{    
    public static IConfigurationBuilder AddEnvironmentVariables(this IConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Add(new EnvironmentVariablesConfigurationSource());
        return configurationBuilder;
    }

    public static IConfigurationBuilder AddEnvironmentVariables(
        this IConfigurationBuilder configurationBuilder,
        string prefix)
    {
        configurationBuilder.Add(
            new EnvironmentVariablesConfigurationSource 
            { 
                Prefix = prefix 
            });
        return configurationBuilder;
    }
    
    public static IConfigurationBuilder AddEnvironmentVariables(
        this IConfigurationBuilder builder, 
        Action<EnvironmentVariablesConfigurationSource> configureSource) => builder.Add(configureSource);
}

```

##### 3.4.1  environment variable configuration source

```c#
public class EnvironmentVariablesConfigurationSource : IConfigurationSource
{    
    public string Prefix { get; set; }
    
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new EnvironmentVariablesConfigurationProvider(Prefix);
    }
}

```

##### 3.4.2 environment variable configuration provider

```c#
public class EnvironmentVariablesConfigurationProvider : ConfigurationProvider
{
    private const string MySqlServerPrefix = "MYSQLCONNSTR_";
    private const string SqlAzureServerPrefix = "SQLAZURECONNSTR_";
    private const string SqlServerPrefix = "SQLCONNSTR_";
    private const string CustomPrefix = "CUSTOMCONNSTR_";
    
    private readonly string _prefix;        
    
    public EnvironmentVariablesConfigurationProvider() => _prefix = string.Empty;        
    public EnvironmentVariablesConfigurationProvider(string prefix) => _prefix = prefix ?? string.Empty;
        
    public override void Load() => Load(Environment.GetEnvironmentVariables());
    
    internal void Load(IDictionary envVariables)
    {
        // 预结果
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // 遍历 environment variables，
        IDictionaryEnumerator e = envVariables.GetEnumerator();
        try
        {
            while (e.MoveNext())
            {
                DictionaryEntry entry = e.Entry;
                string key = (string)entry.Key;
                string provider = null;
                string prefix;
                
                if (key.StartsWith(MySqlServerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    // "MYSQLCONNSTR_"
                    prefix = MySqlServerPrefix;
                    provider = "MySql.Data.MySqlClient";
                }
                else if (key.StartsWith(SqlAzureServerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    // "SQLAZURECONNSTR_"
                    prefix = SqlAzureServerPrefix;
                    provider = "System.Data.SqlClient";
                }
                else if (key.StartsWith(SqlServerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    // "SQLCONNSTR_"
                    prefix = SqlServerPrefix;
                    provider = "System.Data.SqlClient";
                }
                else if (key.StartsWith(CustomPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    // "CUSTOMCONNSTR_"
                    prefix = CustomPrefix;
                }
                else if (key.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase))
                {        
                    // 将 "__" 变为 delimiter
                    key = NormalizeKey(key.Substring(_prefix.Length));
                    // 注入 data（预结果）
                    data[key] = entry.Value as string;
                    
                    continue;
                }
                else
                {
                    continue;
                }
                
                /* 由上，没有 prefix */
                
                // 将 "__" 变为 delimiter
                key = NormalizeKey(key.Substring(prefix.Length));
                
                AddIfPrefixed(data, $"ConnectionStrings:{key}", (string)entry.Value);
                if (provider != null)
                {
                    AddIfPrefixed(data, $"ConnectionStrings:{key}_ProviderName", provider);
                }
            }
        }
        finally
        {
            (e as IDisposable)?.Dispose();
        }
        
        Data = data;
    }
    
    private void AddIfPrefixed(Dictionary<string, string> data, string key, string value)
    {
        if (key.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase))
        {
            key = key.Substring(_prefix.Length);
            data[key] = value;
        }
    }
    
    private static string NormalizeKey(string key) => key.Replace("__", ConfigurationPath.KeyDelimiter);
}

```

##### 3.4.3 configuration path

```c#
public static class ConfigurationPath
{    
    public static readonly string KeyDelimiter = ":";
    
    public static string Combine(params string[] pathSegments)
    {
        if (pathSegments == null)
        {
            throw new ArgumentNullException(nameof(pathSegments));
        }
        return string.Join(KeyDelimiter, pathSegments);
    }

    public static string Combine(IEnumerable<string> pathSegments)
    {
        if (pathSegments == null)
        {
            throw new ArgumentNullException(nameof(pathSegments));
        }
        return string.Join(KeyDelimiter, pathSegments);
    }
    
    public static string GetSectionKey(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }
        
        int lastDelimiterIndex = path.LastIndexOf(KeyDelimiter, StringComparison.OrdinalIgnoreCase);
        return lastDelimiterIndex == -1 ? path : path.Substring(lastDelimiterIndex + 1);
    }
    
    public static string GetParentPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
        
        int lastDelimiterIndex = path.LastIndexOf(KeyDelimiter, StringComparison.OrdinalIgnoreCase);
        return lastDelimiterIndex == -1 ? null : path.Substring(0, lastDelimiterIndex);
    }
}

```

#### 3.5 user secret configuration

```c#
public static class UserSecretsConfigurationExtensions
{    
    /* user secret id by attribute */
    public static IConfigurationBuilder AddUserSecrets<T>(
        this IConfigurationBuilder configuration) where T : class => 
        	configuration.AddUserSecrets(typeof(T).Assembly, optional: false, reloadOnChange: false);

    public static IConfigurationBuilder AddUserSecrets<T>(
        this IConfigurationBuilder configuration, 
        bool optional) where T : class => 
        	configuration.AddUserSecrets(typeof(T).Assembly, optional, reloadOnChange: false);

    public static IConfigurationBuilder AddUserSecrets<T>(
        this IConfigurationBuilder configuration, 
        bool optional, 
        bool reloadOnChange) where T : class => 
        	configuration.AddUserSecrets(typeof(T).Assembly, optional, reloadOnChange);

    public static IConfigurationBuilder AddUserSecrets(
        this IConfigurationBuilder configuration, 
        Assembly assembly) => 
        	configuration.AddUserSecrets(assembly, optional: false, reloadOnChange: false);

    public static IConfigurationBuilder AddUserSecrets(
        this IConfigurationBuilder configuration, 
        Assembly assembly, 
        bool optional) => 
        	configuration.AddUserSecrets(assembly, optional, reloadOnChange: false);

    public static IConfigurationBuilder AddUserSecrets(
        this IConfigurationBuilder configuration, 
        Assembly assembly, 
        bool optional, 
        bool reloadOnChange)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }        
        if (assembly == null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }
        
        // 从 assembly 解析 user secrets id attribute
        UserSecretsIdAttribute attribute = assembly.GetCustomAttribute<UserSecretsIdAttribute>();
        // 如果成功（attribute 不为 null），-> internal
        if (attribute != null)
        {
            return AddUserSecretsInternal(configuration, attribute.UserSecretsId, optional, reloadOnChange);
        }
        
        if (!optional)
        {
            throw new InvalidOperationException(SR.Format(
                    SR.Error_Missing_UserSecretsIdAttribute, 
                	assembly.GetName().Name));
        }
        
        return configuration;
    }
       
    /* 给定 user secret id */
    public static IConfigurationBuilder AddUserSecrets(
        this IConfigurationBuilder configuration, 
        string userSecretsId) => 
        	configuration.AddUserSecrets(userSecretsId, reloadOnChange: false);
        
    public static IConfigurationBuilder AddUserSecrets(
        this IConfigurationBuilder configuration, 
        string userSecretsId, bool reloadOnChange) => 
        	AddUserSecretsInternal(configuration, userSecretsId, true, reloadOnChange);

    /* 真正的功能 */
    private static IConfigurationBuilder AddUserSecretsInternal(
        IConfigurationBuilder configuration, 
        string userSecretsId, 
        bool optional, 
        bool reloadOnChange)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }        
        if (userSecretsId == null)
        {
            throw new ArgumentNullException(nameof(userSecretsId));
        }
        
        return AddSecretsFile(
            configuration, 
            // get file path by "user secret id"
            PathHelper.GetSecretsPathFromSecretsId(userSecretsId), 
            optional, 
            reloadOnChange);
    }
    
    private static IConfigurationBuilder AddSecretsFile(
        IConfigurationBuilder configuration, 
        string secretPath, 
        bool optional, 
        bool reloadOnChange)
    {
        string directoryPath = Path.GetDirectoryName(secretPath);
        PhysicalFileProvider fileProvider = Directory.Exists(directoryPath)
            ? new PhysicalFileProvider(directoryPath)
            : null;

        return configuration.AddJsonFile(fileProvider, PathHelper.SecretsFileName, optional, reloadOnChange);
    }
}

```

##### 3.5.1 user secret path attribute

```c#
[AttributeUsage(
    AttributeTargets.Assembly, 
    Inherited = false, 
    AllowMultiple = false)]
public class UserSecretsIdAttribute : Attribute
{
    public string UserSecretsId { get; }
    
    public UserSecretsIdAttribute(string userSecretId)
    {
        if (string.IsNullOrEmpty(userSecretId))
        {
            throw new ArgumentException(SR.Common_StringNullOrEmpty, nameof(userSecretId));
        }
        
        UserSecretsId = userSecretId;
    }            
}

```

##### 3.5.2 path helper

```c#
public class PathHelper
{
    // 默认的 secret file 文件名
    internal const string SecretsFileName = "secrets.json";
       
    public static string GetSecretsPathFromSecretsId(string userSecretsId)
    {
        if (string.IsNullOrEmpty(userSecretsId))
        {
            throw new ArgumentException(SR.Common_StringNullOrEmpty, nameof(userSecretsId));
        }
        
        // 如果 use secret id 包含 invalid char，-> 抛出异常
        int badCharIndex = userSecretsId.IndexOfAny(Path.GetInvalidFileNameChars());
        if (badCharIndex != -1)
        {
            throw new InvalidOperationException(
                string.Format(
                    SR.Error_Invalid_Character_In_UserSecrets_Id,
                    userSecretsId[badCharIndex],
                    badCharIndex));
        }
        
        const string userSecretsFallbackDir = "DOTNET_USER_SECRETS_FALLBACK_DIR";
        
        // For backwards compat, this checks env vars first before using Env.GetFolderPath
        string appData = Environment.GetEnvironmentVariable("APPDATA");
        
        string root = 
            // On Windows it goes to %APPDATA%\Microsoft\UserSecrets\
            appData                                                                   
            // On Mac/Linux it goes to ~/.microsoft/usersecrets/
            ?? Environment.GetEnvironmentVariable("HOME")                        
            ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            // this fallback is an escape hatch if everything else fails
            ?? Environment.GetEnvironmentVariable(userSecretsFallbackDir);            

        if (string.IsNullOrEmpty(root))
        {
            throw new InvalidOperationException(
                SR.Format(SR.Error_Missing_UserSecretsLocation, userSecretsFallbackDir));
        }
        
        return !string.IsNullOrEmpty(appData)
            ? Path.Combine(root, "Microsoft", "UserSecrets", userSecretsId, SecretsFileName)
            : Path.Combine(root, ".microsoft", "usersecrets", userSecretsId, SecretsFileName);
    }
}

```

#### 3.6 file / stream configuration (abstract)

##### 3.6.1 file configuration

```c#
public static class FileConfigurationExtensions
{
    private static string FileProviderKey = "FileProvider";
    private static string FileLoadExceptionHandlerKey = "FileLoadExceptionHandler";            
        
    // get file provider,
    // 从 configuration builder 的 property 集合解析 file provider，
    // 如果不能解析，创建 physical file provider
    public static IFileProvider GetFileProvider(this IConfigurationBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        if (builder.Properties.TryGetValue(FileProviderKey, out object provider))
        {
            return provider as IFileProvider;
        }
        
        return new PhysicalFileProvider(AppContext.BaseDirectory ?? string.Empty);
    }
        
    // set file provider，
    // 将 file provider 注入 configuration builder 的 property 集合
    // （ filter provider key 是固定的，所以新注入的 file provider 会替换之前的）
    public static IConfigurationBuilder SetFileProvider(
        this IConfigurationBuilder builder, 
        IFileProvider fileProvider)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        builder.Properties[FileProviderKey] = fileProvider ?? throw new ArgumentNullException(nameof(fileProvider));
        return builder;
    }
    
    // set base path，
    // 由 base path 创建 physical file provider，并注入 configuration builder   
    public static IConfigurationBuilder SetBasePath(
        this IConfigurationBuilder builder, 
        string basePath)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (basePath == null)
        {
            throw new ArgumentNullException(nameof(basePath));
        }
        
        return builder.SetFileProvider(new PhysicalFileProvider(basePath));
    }
     
    // get file load exception handler，
    // 从 configuration builder 的 property 集合解析
    public static Action<FileLoadExceptionContext> GetFileLoadExceptionHandler(this IConfigurationBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        if (builder.Properties.TryGetValue(FileLoadExceptionHandlerKey, out object handler))
        {
            return handler as Action<FileLoadExceptionContext>;
        }
        return null;
    }
    
    // set file load exception handler，
    // 注入 configuration builder 的 property 集合
    public static IConfigurationBuilder SetFileLoadExceptionHandler(
        this IConfigurationBuilder builder, 
        Action<FileLoadExceptionContext> handler)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        builder.Properties[FileLoadExceptionHandlerKey] = handler;
        return builder;
    }
       
    
}

```

###### 3.6.1.1 file load exception context

```c#
public class FileLoadExceptionContext
{    
    public FileConfigurationProvider Provider { get; set; }        
    public Exception Exception { get; set; }        
    public bool Ignore { get; set; }
}

```

###### 3.6.1.2 file configuration source

```c#
public abstract class FileConfigurationSource : IConfigurationSource
{        
    public string Path { get; set; }        
    public bool Optional { get; set; }        
    public bool ReloadOnChange { get; set; }        
    public int ReloadDelay { get; set; } = 250;   
    
    public IFileProvider FileProvider { get; set; }        
    public Action<FileLoadExceptionContext> OnLoadException { get; set; }
        
    public abstract IConfigurationProvider Build(IConfigurationBuilder builder);
        
    public void EnsureDefaults(IConfigurationBuilder builder)
    {
        FileProvider = FileProvider ?? builder.GetFileProvider();
        OnLoadException = OnLoadException ?? builder.GetFileLoadExceptionHandler();
    }
        
    public void ResolveFileProvider()
    {
        // 如果 file provider 为 null，path 不为空，且是 rooted path       
        if (FileProvider == null &&
            !string.IsNullOrEmpty(Path) &&
            System.IO.Path.IsPathRooted(Path))
        {
            // 解析 directory name
            string directory = System.IO.Path.GetDirectoryName(Path);
            // 解析 file name
            string pathToFile = System.IO.Path.GetFileName(Path);
            
            // 如果 directory 不为空，并且 directory 不存在？
            while (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                pathToFile = System.IO.Path.Combine(System.IO.Path.GetFileName(directory), pathToFile);
                directory = System.IO.Path.GetDirectoryName(directory);
            }
            
            if (Directory.Exists(directory))
            {
                FileProvider = new PhysicalFileProvider(directory);
                Path = pathToFile;
            }
        }
    }    
}

```

###### 3.6.1.2 file configuration provider

```c#
public abstract class FileConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly IDisposable _changeTokenRegistration;
    
    public FileConfigurationSource Source { get; }
    
    public FileConfigurationProvider(FileConfigurationSource source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        
        // 注入 file configuration source
        Source = source;
        // 注入 on change token
        if (Source.ReloadOnChange && Source.FileProvider != null)
        {
            _changeTokenRegistration = ChangeToken.OnChange(
                () => Source.FileProvider.Watch(Source.Path),
                () =>
                	{
                        Thread.Sleep(Source.ReloadDelay);
                        Load(reload: true);
                    });
        }
    }
                   
    private void Load(bool reload)
    {
        // 从 file configuration source 的 file provider 解析 file info
        IFileInfo file = Source.FileProvider?.GetFileInfo(Source.Path);
        
        // 如果 file 为 null 或者不存在
        if (file == null || !file.Exists)
        {
            // 如果 file configuration source 标记了 optional，-> 创建 data
            // Always optional on reload
            if (Source.Optional || reload) 
            {
                Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            // 否则，即 file 为 null 或者不存在，且 file configuration source 没有标记 optional 或 reload，
            // -> 注入异常
            else
            {
                var error = new StringBuilder($"The configuration file '{Source.Path}' was not found and is not optional.");
                if (!string.IsNullOrEmpty(file?.PhysicalPath))
                {
                    error.Append($" The physical path is '{file.PhysicalPath}'.");
                }
                HandleException(ExceptionDispatchInfo.Capture(new FileNotFoundException(error.ToString())));
            }
        }
        // file 不为 null 且 存在，
        else
        {            
            static Stream OpenRead(IFileInfo fileInfo)
            {
                if (fileInfo.PhysicalPath != null)
                {
                    // The default physical file info assumes asynchronous IO which results in unnecessary overhead
                    // especially since the configuration system is synchronous. This uses the same settings
                    // and disables async IO.
                    return new FileStream(
                        fileInfo.PhysicalPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite,
                        bufferSize: 1,
                        FileOptions.SequentialScan);
                }
                
                return fileInfo.CreateReadStream();
            }
            
            // 创建 file stream 并 read stream（之后 dispose by using）
            using Stream stream = OpenRead(file);
            
            try
            {
                Load(stream);
            }
            catch (Exception e)
            {
                if (reload)
                {
                    Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                HandleException(ExceptionDispatchInfo.Capture(e));
            }
        }
        // REVIEW: Should we raise this in the base as well / instead?
        OnReload();
    }
    
    // 在派生类实现
    public abstract void Load(Stream stream);       
            
    private void HandleException(ExceptionDispatchInfo info)
    {
        bool ignoreException = false;
        if (Source.OnLoadException != null)
        {
            var exceptionContext = new FileLoadExceptionContext
            {
                Provider = this,
                Exception = info.SourceException
            };
            Source.OnLoadException.Invoke(exceptionContext);
            ignoreException = exceptionContext.Ignore;
        }
        if (!ignoreException)
        {
            info.Throw();
        }
    }
       
    public override void Load()
    {
        Load(reload: false);
    }
    
    public void Dispose() => Dispose(true);
        
    protected virtual void Dispose(bool disposing)
    {
        _changeTokenRegistration?.Dispose();
    }
    
    public override string ToString()
        => $"{GetType().Name} for '{Source.Path}' ({(Source.Optional ? "Optional" : "Required")})";
}

```

##### 3.6.2 stream configuration

###### 3.6.2.1 stream configuration source

```c#
public abstract class StreamConfigurationSource : IConfigurationSource
{    
    public Stream Stream { get; set; }        
    public abstract IConfigurationProvider Build(IConfigurationBuilder builder);
}

```

###### 3.6.2.2 stream configuration provider

```c#
public abstract class StreamConfigurationProvider : ConfigurationProvider
{
    // loaded 标记
    private bool _loaded;
    public StreamConfigurationSource Source { get; }
                
    public StreamConfigurationProvider(StreamConfigurationSource source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }
    
    // 在派生类中实现
    public abstract void Load(Stream stream);
        
    public override void Load()
    {
        // 如果 loaded，-> 抛出异常
        if (_loaded)
        {
            throw new InvalidOperationException(SR.StreamConfigurationProvidersAlreadyLoaded);
        }
        
        // load by stream，       
        Load(Source.Stream);
        // 标记 loaded
        _loaded = true;
    }
}

```

#### 3.7 ini configuration

##### 3.7.1 ini file configuration

```c#
public static class IniConfigurationExtensions
{    
    public static IConfigurationBuilder AddIniFile(
        this IConfigurationBuilder builder, 
        string path)
    {
        return AddIniFile(
            builder, 
            provider: null, 
            path: path, 
            optional: false, 
            reloadOnChange: false);
    }
        
    public static IConfigurationBuilder AddIniFile(
        this IConfigurationBuilder builder, 
        string path, 
        bool optional)
    {
        return AddIniFile(
            builder, 
            provider: null, 
            path: path, 
            optional: optional, 
            reloadOnChange: false);
    }
        
    public static IConfigurationBuilder AddIniFile(
        this IConfigurationBuilder builder, 
        string path, 
        bool optional, 
        bool reloadOnChange)
    {
        return AddIniFile(
            builder, 
            provider: null, 
            path: path, 
            optional: optional, 
            reloadOnChange: reloadOnChange);
    }
        
    public static IConfigurationBuilder AddIniFile(
        this IConfigurationBuilder builder, 
        IFileProvider provider, 
        string path, 
        bool optional, 
        bool reloadOnChange)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException(SR.Error_InvalidFilePath, nameof(path));
        }
        
        return builder.AddIniFile(s =>
                                  {
                                      s.FileProvider = provider;
                                      s.Path = path;
                                      s.Optional = optional;
                                      s.ReloadOnChange = reloadOnChange;
                                      s.ResolveFileProvider();
                                  });
    }
        
    public static IConfigurationBuilder AddIniFile(
        this IConfigurationBuilder builder, 
        Action<IniConfigurationSource> configureSource) => builder.Add(configureSource);                
}

```

###### 3.7.1.1 ini (file) configuration source 

```c#
public class IniConfigurationSource : FileConfigurationSource
{    
    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new IniConfigurationProvider(this);
    }
}

```

###### 3.7.1.2 ini (file) configuration provider

```c#
public class IniConfigurationProvider : FileConfigurationProvider
{   
    public IniConfigurationProvider(IniConfigurationSource source) : base(source) 
    { 
    }
       
    public override void Load(Stream stream) => Data = IniStreamConfigurationProvider.Read(stream);
}

```

##### 3.7.2 ini stream configuration

```c#
public static class IniConfigurationExtensions
{ 
    public static IConfigurationBuilder AddIniStream(
        this IConfigurationBuilder builder, 
        Stream stream)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        return builder.Add<IniStreamConfigurationSource>(s => s.Stream = stream);
    }
}

```

###### 3.7.2.1 ini stream configuration source

```c#
public class IniStreamConfigurationSource : StreamConfigurationSource
{    
    public override IConfigurationProvider Build(IConfigurationBuilder builder) => new IniStreamConfigurationProvider(this);
}

```

###### 3.7.2.2 ini stream configuration provider

```c#
public class IniStreamConfigurationProvider : StreamConfigurationProvider
{    
    public IniStreamConfigurationProvider(IniStreamConfigurationSource source) : base(source) 
    {
    }
      
    public override void Load(Stream stream)
    {
        Data = Read(stream);
    }
    
    public static IDictionary<string, string> Read(Stream stream)
    {
        // 预结果
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        using (var reader = new StreamReader(stream))
        {
            string sectionPrefix = string.Empty;
            
            while (reader.Peek() != -1)
            {
                string rawLine = reader.ReadLine();
                string line = rawLine.Trim();
                
                // Ignore blank lines
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                // Ignore comments
                if (line[0] == ';' || line[0] == '#' || line[0] == '/')
                {
                    continue;
                }
                // [Section:header]
                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    // remove the brackets
                    sectionPrefix = line.Substring(1, line.Length - 2) + ConfigurationPath.KeyDelimiter;
                    continue;
                }
                
                // key = value OR "value"
                int separator = line.IndexOf('=');
                if (separator < 0)
                {
                    throw new FormatException(SR.Format(SR.Error_UnrecognizedLineFormat, rawLine));
                }
                
                string key = sectionPrefix + line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();
                
                // Remove quotes
                if (value.Length > 1 && value[0] == '"' && value[value.Length - 1] == '"')
                {
                    value = value.Substring(1, value.Length - 2);
                }
                
                if (data.ContainsKey(key))
                {
                    throw new FormatException(SR.Format(SR.Error_KeyIsDuplicated, key));
                }
                
                data[key] = value;
            }
        }
        
        return data;
    }           
}

```

#### 3.8 json configuration

##### 3.8.1 json file configuration

```c#
public static class JsonConfigurationExtensions
{    
    public static IConfigurationBuilder AddJsonFile(
        this IConfigurationBuilder builder, 
        string path)
    {
        return AddJsonFile(
            builder, 
            provider: null, 
            path: path, 
            optional: false, 
            reloadOnChange: false);
    }
        
    public static IConfigurationBuilder AddJsonFile(
        this IConfigurationBuilder builder, 
        string path, 
        bool optional)
    {
        return AddJsonFile(
            builder, 
            provider: null, 
            path: path, 
            optional: optional, 
            reloadOnChange: false);
    }
        
    public static IConfigurationBuilder AddJsonFile(
        this IConfigurationBuilder builder, 
        string path, 
        bool optional, 
        bool reloadOnChange)
    {
        return AddJsonFile(
            builder, 
            provider: null, 
            path: path, 
            optional: optional, 
            reloadOnChange: reloadOnChange);
    }
        
    public static IConfigurationBuilder AddJsonFile(
        this IConfigurationBuilder builder, 
        IFileProvider provider, 
        string path, 
        bool optional, 
        bool reloadOnChange)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException(SR.Error_InvalidFilePath, nameof(path));
        }
        
        return builder.AddJsonFile(s =>
                                   {
                                       s.FileProvider = provider;
                                       s.Path = path;
                                       s.Optional = optional;
                                       s.ReloadOnChange = reloadOnChange;
                                       s.ResolveFileProvider();
                                   });
    }
        
    public static IConfigurationBuilder AddJsonFile(
        this IConfigurationBuilder builder, 
        Action<JsonConfigurationSource> configureSource) => 
        	builder.Add(configureSource);        
}

```

###### 3.8.1.1 json (file) configuration source

```c#
public class JsonConfigurationSource : FileConfigurationSource
{    
    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new JsonConfigurationProvider(this);
    }
}

```

###### 3.8.1.2 json (file) configuration provider

```c#
public class JsonConfigurationProvider : FileConfigurationProvider
{   
    public JsonConfigurationProvider(JsonConfigurationSource source) : base(source) 
    {
    }
        
    public override void Load(Stream stream)
    {
        try
        {
            Data = JsonConfigurationFileParser.Parse(stream);
        }
        catch (JsonException e)
        {
            throw new FormatException(SR.Error_JSONParseError, e);
        }
    }
}

```

###### 3.8.1.3 json configuration file parser

```c#
internal sealed class JsonConfigurationFileParser
{
    // 初始化
    private readonly Dictionary<string, string> _data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly Stack<string> _paths = new Stack<string>();
    
    private JsonConfigurationFileParser() 
    { 
    }
    
    public static IDictionary<string, string> Parse(Stream input) => new JsonConfigurationFileParser().ParseStream(input);
    
    private IDictionary<string, string> ParseStream(Stream input)
    {
        var jsonDocumentOptions = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        
        using (var reader = new StreamReader(input))
            using (JsonDocument doc = JsonDocument.Parse(reader.ReadToEnd(), jsonDocumentOptions))
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException(SR.Format(
                    SR.Error_InvalidTopLevelJSONElement, 
                    doc.RootElement.ValueKind));
            }
            
            VisitElement(doc.RootElement);
        }
        
        return _data;
    }
    
    private void VisitElement(JsonElement element)
    {
        var isEmpty = true;
        
        foreach (JsonProperty property in element.EnumerateObject())
        {
            isEmpty = false;
            EnterContext(property.Name);
            VisitValue(property.Value);
            ExitContext();
        }
        
        if (isEmpty && _paths.Count > 0)
        {
            _data[_paths.Peek()] = null;
        }
    }
    
    private void VisitValue(JsonElement value)
    {
        Debug.Assert(_paths.Count > 0);
        
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                VisitElement(value);
                break;
                
            case JsonValueKind.Array:
                int index = 0;
                foreach (JsonElement arrayElement in value.EnumerateArray())
                {
                    EnterContext(index.ToString());
                    VisitValue(arrayElement);
                    ExitContext();
                    index++;
                }
                break;
                
            case JsonValueKind.Number:
            case JsonValueKind.String:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                string key = _paths.Peek();
                if (_data.ContainsKey(key))
                {
                    throw new FormatException(SR.Format(SR.Error_KeyIsDuplicated, key));
                }
                _data[key] = value.ToString();
                break;
                
            default:
                throw new FormatException(SR.Format(
                    SR.Error_UnsupportedJSONToken, 
                    value.ValueKind));
        }
    }
    
    private void EnterContext(string context) =>
        _paths.Push(_paths.Count > 0 
                    	? _paths.Peek() + ConfigurationPath.KeyDelimiter + context 
                    	: context);
    
    private void ExitContext() => _paths.Pop();
}

```

##### 3.8.2 json stream configuration

```c#
public static class JsonConfigurationExtensions
{    
    public static IConfigurationBuilder AddJsonStream(
        this IConfigurationBuilder builder, 
        Stream stream)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        return builder.Add<JsonStreamConfigurationSource>(s => s.Stream = stream);
    }
}

```

###### 3.8.2.1 json stream configuration source

```c#
public class JsonStreamConfigurationSource : StreamConfigurationSource
{    
    public override IConfigurationProvider Build(IConfigurationBuilder builder) => 
        new JsonStreamConfigurationProvider(this);
}

```

###### 3.8.2.2 json stream configuration provider

```c#
public class JsonStreamConfigurationProvider : StreamConfigurationProvider
{       
    public JsonStreamConfigurationProvider(JsonStreamConfigurationSource source) : base(source) 
    {
    }
        
    public override void Load(Stream stream)
    {
        Data = JsonConfigurationFileParser.Parse(stream);
    }
}

```

#### 3.9 xml configuration

##### 3.9.1 xml file configuration

```c#
public static class XmlConfigurationExtensions
{   
    public static IConfigurationBuilder AddXmlFile(
        this IConfigurationBuilder builder, 
        string path)
    {
        return AddXmlFile(
            builder, 
            provider: null, 
            path: path, 
            optional: false, 
            reloadOnChange: false);
    }
          
    public static IConfigurationBuilder AddXmlFile(
        this IConfigurationBuilder builder, 
        string path, 
        bool optional)
    {
        return AddXmlFile(
            builder, 
            provider: null, 
            path: path, 
            optional: optional, 
            reloadOnChange: false);
    }
            
    public static IConfigurationBuilder AddXmlFile(
        this IConfigurationBuilder builder, 
        string path, 
        bool optional, 
        bool reloadOnChange)
    {
        return AddXmlFile(
            builder, 
            provider: null, 
            path: path, 
            optional: optional, 
            reloadOnChange: reloadOnChange);
    }
          
    public static IConfigurationBuilder AddXmlFile(
        this IConfigurationBuilder builder, 
        IFileProvider provider, 
        string path, 
        bool optional, 
        bool reloadOnChange)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException(SR.Error_InvalidFilePath, nameof(path));
        }
        
        return builder.AddXmlFile(s =>
                                  {
                                      s.FileProvider = provider;
                                      s.Path = path;
                                      s.Optional = optional;
                                      s.ReloadOnChange = reloadOnChange;
                                      s.ResolveFileProvider();
                                  });
    }
       
    public static IConfigurationBuilder AddXmlFile(
        this IConfigurationBuilder builder, 
        Action<XmlConfigurationSource> configureSource) => 
        	builder.Add(configureSource);    
}

```

###### 3.9.1.1 xml (file) configuration source

```c#
public class XmlConfigurationSource : FileConfigurationSource
{    
    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new XmlConfigurationProvider(this);
    }
}

```

###### 3.9.1.2 xml (file) configuration provider

```c#
public class XmlConfigurationProvider : FileConfigurationProvider
{
    internal XmlDocumentDecryptor Decryptor { get; set; } = XmlDocumentDecryptor.Instance;
    
    public XmlConfigurationProvider(XmlConfigurationSource source) : base(source) 
    {
    }
    
    public override void Load(Stream stream)
    {
        Data = XmlStreamConfigurationProvider.Read(stream, Decryptor);
    }
}

```

###### 3.9.1.3 xml document decryptor

```c#
public class XmlDocumentDecryptor
{
    private readonly Func<XmlDocument, EncryptedXml> _encryptedXmlFactory;
    private static EncryptedXml DefaultEncryptedXmlFactory(XmlDocument document) => new EncryptedXml(document);
   
    public static readonly XmlDocumentDecryptor Instance = new XmlDocumentDecryptor();
              
    protected XmlDocumentDecryptor() : this(DefaultEncryptedXmlFactory)
    {
    }
        
    internal XmlDocumentDecryptor(Func<XmlDocument, EncryptedXml> encryptedXmlFactory)
    {
        _encryptedXmlFactory = encryptedXmlFactory;
    }
    
    private static bool ContainsEncryptedData(XmlDocument document)
    {
        // EncryptedXml will simply decrypt the document in-place without telling us that it did so, so we need to perform 
        // a check to see if EncryptedXml will actually do anything. 
        // The below check for an encrypted data blob is the same one that EncryptedXml would have performed.
        var namespaceManager = new XmlNamespaceManager(document.NameTable);
        namespaceManager.AddNamespace("enc", "http://www.w3.org/2001/04/xmlenc#");
        return (document.SelectSingleNode("//enc:EncryptedData", namespaceManager) != null);
    }
       
    public XmlReader CreateDecryptingXmlReader(Stream input, XmlReaderSettings settings)
    {
        // XML-based configurations aren't really all that big, so we can buffer the whole thing in memory 
        // while we determine decryption operations.
        var memStream = new MemoryStream();
        input.CopyTo(memStream);
        memStream.Position = 0;
        
        // First, consume the entire XmlReader as an XmlDocument.
        var document = new XmlDocument();
        using (var reader = XmlReader.Create(memStream, settings))
        {
            document.Load(reader);
        }
        memStream.Position = 0;
        
        if (ContainsEncryptedData(document))
        {
            return DecryptDocumentAndCreateXmlReader(document);
        }
        else
        {
            // If no decryption would have taken place, return a new fresh reader
            // based on the memory stream (which doesn't need to be disposed).
            return XmlReader.Create(memStream, settings);
        }
    }
       
    protected virtual XmlReader DecryptDocumentAndCreateXmlReader(XmlDocument document)
    {
        // Perform the actual decryption step, updating the XmlDocument in-place.
        EncryptedXml encryptedXml = _encryptedXmlFactory(document);
        encryptedXml.DecryptDocument();
        
        // Finally, return the new XmlReader from the updated XmlDocument.
        // Error messages based on this XmlReader won't show line numbers,
        // but that's fine since we transformed the document anyway.
        return document.CreateNavigator().ReadSubtree();
    }        
}

```

##### 3.9.2 xml stream configuration

```c#
public static class XmlConfigurationExtensions
{   
    public static IConfigurationBuilder AddXmlStream(
        this IConfigurationBuilder builder, 
        Stream stream)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        return builder.Add<XmlStreamConfigurationSource>(s => s.Stream = stream);
    }
}

```

###### 3.9.2.1 xml stream configuration source

```c#
public class XmlStreamConfigurationSource : StreamConfigurationSource
{    
    public override IConfigurationProvider Build(IConfigurationBuilder builder) => 
        new XmlStreamConfigurationProvider(this);
}

```

###### 3.9.2.2 xml stream configuration provider

```c#
public class XmlStreamConfigurationProvider : StreamConfigurationProvider
{
    private const string NameAttributeKey = "Name";
        
    public XmlStreamConfigurationProvider(XmlStreamConfigurationSource source) : base(source) 
    {
    }
        
    public static IDictionary<string, string> Read(
        Stream stream, 
        XmlDocumentDecryptor decryptor)
    {
        var readerSettings = new XmlReaderSettings()
        {
            CloseInput = false, // caller will close the stream
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = true,
            IgnoreWhitespace = true
        };
        
        XmlConfigurationElement root = null;
        
        using (XmlReader reader = decryptor.CreateDecryptingXmlReader(stream, readerSettings))
        {
            // keep track of the tree we followed to get where we are (breadcrumb style)
            var currentPath = new Stack<XmlConfigurationElement>();
            
            XmlNodeType preNodeType = reader.NodeType;
            
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:                        
                        var element = new XmlConfigurationElement(reader.LocalName, GetName(reader));                        
                        if (currentPath.Count == 0)
                        {
                            root = element;
                        }
                        else
                        {
                            var parent = currentPath.Peek();
                            
                            // If parent already has a dictionary of children, update the collection accordingly
                            if (parent.ChildrenBySiblingName != null)
                            {
                                // check if this element has appeared before, 
                                // elements are considered siblings if their SiblingName properties match
                                if (!parent.ChildrenBySiblingName.TryGetValue(element.SiblingName, out var siblings))
                                {
                                    siblings = new List<XmlConfigurationElement>();
                                    parent.ChildrenBySiblingName.Add(element.SiblingName, siblings);
                                }
                                siblings.Add(element);
                            }
                            else
                            {
                                // Performance optimization: parents with a single child don't even initialize a dictionary
                                if (parent.SingleChild == null)
                                {
                                    parent.SingleChild = element;
                                }
                                else
                                {
                                    // If we encounter a second child after assigning "SingleChild", 
                                    // we clear SingleChild and initialize the dictionary
                                    var children = 
                                        new Dictionary<string, List<XmlConfigurationElement>>(StringComparer.OrdinalIgnoreCase);

                                    // Special case: the first and second child have the same sibling name
                                    if (string.Equals(
                                        	parent.SingleChild.SiblingName, 
                                        	element.SiblingName, 
                                        	StringComparison.OrdinalIgnoreCase))
                                    {
                                        children.Add(
                                            element.SiblingName, 
                                            new List<XmlConfigurationElement>
                                            {
                                                parent.SingleChild,
                                                element
                                            });
                                    }
                                    else
                                    {
                                        children.Add(
                                            parent.SingleChild.SiblingName, 
                                            new List<XmlConfigurationElement> 
                                            { 
                                                parent.SingleChild 
                                            });
                                        children.Add(
                                            element.SiblingName, 
                                            new List<XmlConfigurationElement> 
                                            { 
                                                element 
                                            });
                                    }
                                    
                                    parent.ChildrenBySiblingName = children;
                                    parent.SingleChild = null;
                                }
                                
                            }
                        }
                        
                        currentPath.Push(element);
                        
                        ReadAttributes(reader, element);
                        
                        // If current element is self-closing
                        if (reader.IsEmptyElement)
                        {
                            currentPath.Pop();
                        }
                        break;
                        
                    case XmlNodeType.EndElement:
                        if (currentPath.Count != 0)
                        {
                            XmlConfigurationElement parent = currentPath.Pop();
                            
                            // If this EndElement node comes right after an Element node,
                            // it means there is no text/CDATA node in current element
                            if (preNodeType == XmlNodeType.Element)
                            {
                                var lineInfo = reader as IXmlLineInfo;
                                var lineNumber = lineInfo?.LineNumber;
                                var linePosition = lineInfo?.LinePosition;
                                parent.TextContent = 
                                    new XmlConfigurationElementTextContent(string.Empty, lineNumber, linePosition);
                            }
                        }
                        break;
                        
                    case XmlNodeType.CDATA:
                    case XmlNodeType.Text:
                        if (currentPath.Count != 0)
                        {
                            var lineInfo = reader as IXmlLineInfo;
                            var lineNumber = lineInfo?.LineNumber;
                            var linePosition = lineInfo?.LinePosition;
                            
                            XmlConfigurationElement parent = currentPath.Peek();
                            
                            parent.TextContent = new XmlConfigurationElementTextContent(reader.Value, lineNumber, linePosition);
                        }
                        break;
                        
                    case XmlNodeType.XmlDeclaration:
                    case XmlNodeType.ProcessingInstruction:
                    case XmlNodeType.Comment:
                    case XmlNodeType.Whitespace:
                        // Ignore certain types of nodes
                        break;
                        
                    default:
                        throw new FormatException(SR.Format(SR.Error_UnsupportedNodeType, reader.NodeType, GetLineInfo(reader)));
                }
                
                preNodeType = reader.NodeType;
                
                // If this element is a self-closing element,
                // we pretend that we just processed an EndElement node
                // because a self-closing element contains an end within itself
                if (preNodeType == XmlNodeType.Element && reader.IsEmptyElement)
                {
                    preNodeType = XmlNodeType.EndElement;
                }
            }
        }
        
        return ProvideConfiguration(root);
    }
    
    public override void Load(Stream stream)
    {
        Data = Read(stream, XmlDocumentDecryptor.Instance);
    }
    
    private static string GetLineInfo(XmlReader reader)
    {
        var lineInfo = reader as IXmlLineInfo;
        return lineInfo == null ? string.Empty :
        SR.Format(SR.Msg_LineInfo, lineInfo.LineNumber, lineInfo.LinePosition);
    }
    
    private static void ReadAttributes(
        XmlReader reader, 
        XmlConfigurationElement element)
    {
        if (reader.AttributeCount > 0)
        {
            element.Attributes = new List<XmlConfigurationElementAttributeValue>();
        }
        
        var lineInfo = reader as IXmlLineInfo;
        
        for (int i = 0; i < reader.AttributeCount; i++)
        {
            reader.MoveToAttribute(i);
            
            var lineNumber = lineInfo?.LineNumber;
            var linePosition = lineInfo?.LinePosition;
            
            // If there is a namespace attached to current attribute
            if (!string.IsNullOrEmpty(reader.NamespaceURI))
            {
                throw new FormatException(SR.Format(SR.Error_NamespaceIsNotSupported, GetLineInfo(reader)));
            }
            
            element.Attributes.Add(
                new XmlConfigurationElementAttributeValue(
                    reader.LocalName, 
                    reader.Value, 
                    lineNumber, 
                    linePosition));
        }
        
        // Go back to the element containing the attributes we just processed
        reader.MoveToElement();
    }
    
    // The special attribute "Name" only contributes to prefix
    // This method retrieves the Name of the element, if the attribute is present Unfortunately XmlReader.GetAttribute cannot be used,
    // as it does not support looking for attributes in a case insensitive manner
    private static string GetName(XmlReader reader)
    {
        string name = null;
        
        while (reader.MoveToNextAttribute())
        {
            if (string.Equals(
                	reader.LocalName, 
                	NameAttributeKey, 
                	StringComparison.OrdinalIgnoreCase))
            {
                // If there is a namespace attached to current attribute
                if (!string.IsNullOrEmpty(reader.NamespaceURI))
                {
                    throw new FormatException(
                        SR.Format(SR.Error_NamespaceIsNotSupported, GetLineInfo(reader)));
                }
                name = reader.Value;
                break;
            }
        }
        
        // Go back to the element containing the name we just processed
        reader.MoveToElement();
        
        return name;
    }
    
    private static IDictionary<string, string> ProvideConfiguration(XmlConfigurationElement root)
    {
        var configuration = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        if (root == null)
        {
            return configuration;
        }
        
        var rootPrefix = new Prefix();
        
        // The root element only contributes to the prefix via its Name attribute
        if (!string.IsNullOrEmpty(root.Name))
        {
            rootPrefix.Push(root.Name);
        }
        
        ProcessElementAttributes(rootPrefix, root);
        ProcessElementContent(rootPrefix, root);
        ProcessElementChildren(rootPrefix, root);
        
        return configuration;
        
        void ProcessElement(Prefix prefix, XmlConfigurationElement element)
        {
            ProcessElementAttributes(prefix, element);
            
            ProcessElementContent(prefix, element);
            
            ProcessElementChildren(prefix, element);
        }
        
        void ProcessElementAttributes(Prefix prefix, XmlConfigurationElement element)
        {
            // Add attributes to configuration values
            if (element.Attributes != null)
            {
                for (var i = 0; i < element.Attributes.Count; i++)
                {
                    var attribute = element.Attributes[i];
                    
                    prefix.Push(attribute.Attribute);
                    
                    AddToConfiguration(prefix.AsString, attribute.Value, attribute.LineNumber, attribute.LinePosition);
                    
                    prefix.Pop();
                }
            }
        }
        
        void ProcessElementContent(Prefix prefix, XmlConfigurationElement element)
        {
            // Add text content to configuration values
            if (element.TextContent != null)
            {
                var textContent = element.TextContent;
                AddToConfiguration(prefix.AsString, textContent.TextContent, textContent.LineNumber, textContent.LinePosition);
            }
        }
        
        void ProcessElementChildren(Prefix prefix, XmlConfigurationElement element)
        {
            if (element.SingleChild != null)
            {
                var child = element.SingleChild;
                
                ProcessElementChild(prefix, child, null);
                
                return;
            }
            
            if (element.ChildrenBySiblingName == null)
            {
                return;
            }
            
            // Recursively walk through the children of this element
            foreach (var childrenWithSameSiblingName in element.ChildrenBySiblingName.Values)
            {
                if (childrenWithSameSiblingName.Count == 1)
                {
                    var child = childrenWithSameSiblingName[0];
                    
                    ProcessElementChild(prefix, child, null);
                }
                else
                {
                    // Multiple children with the same sibling name. Add the current index to the prefix
                    for (int i = 0; i < childrenWithSameSiblingName.Count; i++)
                    {
                        var child = childrenWithSameSiblingName[i];
                        
                        ProcessElementChild(prefix, child, i);
                    }
                }
            }
        }
        
        void ProcessElementChild(Prefix prefix, XmlConfigurationElement child, int? index)
        {
            // Add element name to prefix
            prefix.Push(child.ElementName);
            
            // Add value of name attribute to prefix
            var hasName = !string.IsNullOrEmpty(child.Name);
            if (hasName)
            {
                prefix.Push(child.Name);
            }
            
            // Add index to the prefix
            if (index != null)
            {
                prefix.Push(index.Value.ToString(CultureInfo.InvariantCulture));
            }
            
            ProcessElement(prefix, child);
            
            // Remove index
            if (index != null)
            {
                prefix.Pop();
            }
            
            // Remove 'Name' attribute
            if (hasName)
            {
                prefix.Pop();
            }
            
            // Remove element name
            prefix.Pop();
        }
        
        void AddToConfiguration(string key, string value, int? lineNumber, int? linePosition)
        {
#if NETSTANDARD2_1
            if (!configuration.TryAdd(key, value))
            {
                var lineInfo = lineNumber == null || linePosition == null
                    ? string.Empty
                    : SR.Format(SR.Msg_LineInfo, lineNumber.Value, linePosition.Value);
                
                throw new FormatException(SR.Format(SR.Error_KeyIsDuplicated, key, lineInfo));
            }
#else
            if (configuration.ContainsKey(key))
            {
                var lineInfo = lineNumber == null || linePosition == null
                    ? string.Empty
                    : SR.Format(SR.Msg_LineInfo, lineNumber.Value, linePosition.Value);
                
                throw new FormatException(SR.Format(SR.Error_KeyIsDuplicated, key, lineInfo));
            }
            
            configuration.Add(key, value);
#endif
        }
    }
}

internal sealed class Prefix
{
    private readonly StringBuilder _sb;
    private readonly Stack<int> _lengths;
    
    public Prefix()
    {
        _sb = new StringBuilder();
        _lengths = new Stack<int>();
    }
    
    public string AsString => _sb.ToString();
    
    public void Push(string value)
    {
        if (_sb.Length != 0)
        {
            _sb.Append(ConfigurationPath.KeyDelimiter);
            _sb.Append(value);
            _lengths.Push(value.Length + ConfigurationPath.KeyDelimiter.Length);
        }
        else
        {
            _sb.Append(value);            
            _lengths.Push(value.Length);
        }
    }
    
    public void Pop()
    {
        var length = _lengths.Pop();        
        _sb.Remove(_sb.Length - length, length);
    }
}

```

###### 3.9.2.3 xml configuration element

```c#
internal sealed class XmlConfigurationElement
{
    public string ElementName { get; }    
    public string Name { get; }       
    public string SiblingName { get; }    
    public IDictionary<string, List<XmlConfigurationElement>> ChildrenBySiblingName { get; set; }    
    public XmlConfigurationElement SingleChild { get; set; }    
    public XmlConfigurationElementTextContent TextContent { get; set; }    
    public List<XmlConfigurationElementAttributeValue> Attributes { get; set; }
    
    public XmlConfigurationElement(string elementName, string name)
    {
        ElementName = elementName ?? throw new ArgumentNullException(nameof(elementName));
        Name = name;
        SiblingName = string.IsNullOrEmpty(Name) ? ElementName : ElementName + ":" + Name;
    }
}

```

###### 3.9.2.4 xml configuration element attribute value

```c#
internal sealed class XmlConfigurationElementAttributeValue
{
    public string Attribute { get; }    
    public string Value { get; }    
    public int? LineNumber { get; }    
    public int? LinePosition { get; }
    
    public XmlConfigurationElementAttributeValue(string attribute, string value, int? lineNumber, int? linePosition)
    {
        Attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));        
        Value = value ?? throw new ArgumentNullException(nameof(value));
        LineNumber = lineNumber;
        LinePosition = linePosition;
    }        
}

```

###### 3.9.2.5 xml configuration element text context

```c#
internal sealed class XmlConfigurationElementTextContent
{
    public string TextContent { get; }    
    public int? LineNumber { get; }    
    public int? LinePosition { get; }
    
    public XmlConfigurationElementTextContent(string textContent, int? linePosition, int? lineNumber)
    {
        TextContent = textContent ?? throw new ArgumentNullException(nameof(textContent));
        LineNumber = lineNumber;
        LinePosition = linePosition;
    }        
}

```

### 4. options

#### 4.1 t options

##### 4.1.1 configure options

```c#
public interface IConfigureOptions<in TOptions> where TOptions : class
{    
    void Configure(TOptions options);
}

```

###### 4.1.1.1 configure options（实现）

```c#
public class ConfigureOptions<TOptions> : IConfigureOptions<TOptions> where TOptions : class
{
    public Action<TOptions> Action { get; }
    
    public ConfigureOptions(Action<TOptions> action)
    {
        Action = action;
    }
                    
    public virtual void Configure(TOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        Action?.Invoke(options);
    }
}

```

###### 4.1.1.2 configure named options

```c#
// 接口
public interface IConfigureNamedOptions<in TOptions> : IConfigureOptions<TOptions> where TOptions : class
{    
    void Configure(string name, TOptions options);
}

// 实现
public class ConfigureNamedOptions<TOptions> : IConfigureNamedOptions<TOptions> where TOptions : class
{
    public string Name { get; }        
    public Action<TOptions> Action { get; }
    
    public ConfigureNamedOptions(string name, Action<TOptions> action)
    {
        Name = name;
        Action = action;
    }
       
    public void Configure(TOptions options) => Configure(Options.DefaultName, options);
    
    public virtual void Configure(string name, TOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        // 如果 name=Name，或者 name 为 null，-> invoke action
        if (Name == null || name == Name)
        {
            Action?.Invoke(options);
        }
    }           
}

```

###### 4.1.1.3 configure named options with dependencies

```c#
// 1 dep
public class ConfigureNamedOptions<TOptions, TDep> : IConfigureNamedOptions<TOptions> 
    where TOptions : class 
    where TDep : class
{
    public string Name { get; }        
    public Action<TOptions, TDep> Action { get; }        
    public TDep Dependency { get; }
    
    public ConfigureNamedOptions(
        string name, 
        TDep dependency, 
        Action<TOptions, TDep> action)
    {
        Name = name;
        Action = action;
        Dependency = dependency;
    }
       
    public void Configure(TOptions options) => Configure(Options.DefaultName, options);
    
    public virtual void Configure(string name, TOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        // Null name is used to configure all named options.
        if (Name == null || name == Name)
        {
            Action?.Invoke(options, Dependency);
        }
    }            
}

// 2 dep   
public class ConfigureNamedOptions<TOptions, TDep1, TDep2> : IConfigureNamedOptions<TOptions>    
    where TOptions : class    
    where TDep1 : class            
    where TDep2 : class
{
    public string Name { get; }                
    public Action<TOptions, TDep1, TDep2> Action { get; }        
    public TDep1 Dependency1 { get; }
    public TDep2 Dependency2 { get; }
        
    public ConfigureNamedOptions(
        string name, 
        TDep1 dependency, 
        TDep2 dependency2, 
        Action<TOptions, TDep1, TDep2> action)
    {
        Name = name;
        Action = action;
        Dependency1 = dependency;
        Dependency2 = dependency2;
    }
        
    public void Configure(TOptions options) => Configure(Options.DefaultName, options);
        
    public virtual void Configure(string name, TOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        // Null name is used to configure all named options.
        if (Name == null || name == Name)
        {
            Action?.Invoke(options, Dependency1, Dependency2);
        }
    }        
}

// 3 dep    
public class ConfigureNamedOptions<TOptions, TDep1, TDep2, TDep3> : IConfigureNamedOptions<TOptions>        
    where TOptions : class       
    where TDep1 : class   
    where TDep2 : class 
    where TDep3 : class
{
    public string Name { get; }        
    public Action<TOptions, TDep1, TDep2, TDep3> Action { get; }    
    public TDep1 Dependency1 { get; }        
    public TDep2 Dependency2 { get; }        
    public TDep3 Dependency3 { get; }
    
    public ConfigureNamedOptions(
        string name, 
        TDep1 dependency, 
        TDep2 dependency2, 
        TDep3 dependency3, 
        Action<TOptions, TDep1, TDep2, TDep3> action)
    {
        Name = name;
        Action = action;
        Dependency1 = dependency;
        Dependency2 = dependency2;
        Dependency3 = dependency3;
    }
    
    public void Configure(TOptions options) => Configure(Options.DefaultName, options);
    
    public virtual void Configure(string name, TOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        // Null name is used to configure all named options.
        if (Name == null || name == Name)
        {
            Action?.Invoke(options, Dependency1, Dependency2, Dependency3);
        }
    }            
}

// 4 dep   
public class ConfigureNamedOptions<TOptions, TDep1, TDep2, TDep3, TDep4> : IConfigureNamedOptions<TOptions>        
    where TOptions : class    
    where TDep1 : class        
    where TDep2 : class        
    where TDep3 : class        
    where TDep4 : class
{
    public string Name { get; }    
    public Action<TOptions, TDep1, TDep2, TDep3, TDep4> Action { get; }    
    public TDep1 Dependency1 { get; }    
    public TDep2 Dependency2 { get; }    
    public TDep3 Dependency3 { get; }        
    public TDep4 Dependency4 { get; }
    
    public ConfigureNamedOptions(
        string name, 
        TDep1 dependency1, 
        TDep2 dependency2, 
        TDep3 dependency3, 
        TDep4 dependency4, 
        Action<TOptions, TDep1, TDep2, TDep3, TDep4> action)
    {
        Name = name;
        Action = action;
        Dependency1 = dependency1;
        Dependency2 = dependency2;
        Dependency3 = dependency3;
        Dependency4 = dependency4;
    }
    
    public void Configure(TOptions options) => Configure(Options.DefaultName, options);
    
    public virtual void Configure(string name, TOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        // Null name is used to configure all named options.
        if (Name == null || name == Name)
        {
            Action?.Invoke(options, Dependency1, Dependency2, Dependency3, Dependency4);
        }
    }        
}

// 5 dep   
public class ConfigureNamedOptions<TOptions, TDep1, TDep2, TDep3, TDep4, TDep5> : IConfigureNamedOptions<TOptions>    
    where TOptions : class        
    where TDep1 : class            
    where TDep2 : class                
    where TDep3 : class                    
    where TDep4 : class                        
    where TDep5 : class
{
    public string Name { get; }        
    public Action<TOptions, TDep1, TDep2, TDep3, TDep4, TDep5> Action { get; }        
    public TDep1 Dependency1 { get; }        
    public TDep2 Dependency2 { get; }        
    public TDep3 Dependency3 { get; }        
    public TDep4 Dependency4 { get; }        
    public TDep5 Dependency5 { get; }    
    
    public ConfigureNamedOptions(
        string name, 
        TDep1 dependency1, 
        TDep2 dependency2, 
        TDep3 dependency3, 
        TDep4 dependency4, 
        TDep5 dependency5, 
        Action<TOptions, TDep1, TDep2, TDep3, TDep4, TDep5> action)
    {
        Name = name;
        Action = action;
        Dependency1 = dependency1;
        Dependency2 = dependency2;
        Dependency3 = dependency3;
        Dependency4 = dependency4;
        Dependency5 = dependency5;
    }
    
    public void Configure(TOptions options) => Configure(Options.DefaultName, options);
        
    public virtual void Configure(string name, TOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        // Null name is used to configure all named options.
        if (Name == null || name == Name)
        {
            Action?.Invoke(options, Dependency1, Dependency2, Dependency3, Dependency4, Dependency5);
        }
    }            
}

```

##### 4.1.2 post configure options

```c#
public interface IPostConfigureOptions<in TOptions> where TOptions : class
{    
    void PostConfigure(string name, TOptions options);
}

```

###### 4.1.2.1 post configure (named) options（实现）

```c#
public class PostConfigureOptions<TOptions> : IPostConfigureOptions<TOptions> where TOptions : class
{
    public string Name { get; }
    public Action<TOptions> Action { get; }
    
    public PostConfigureOptions(string name, Action<TOptions> action)
    {
        Name = name;
        Action = action;
    }
    
    public virtual void PostConfigure(string name, TOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        // Null name is used to initialize all named options.
        if (Name == null || name == Name)
        {
            Action?.Invoke(options);
        }
    }
}

```

###### 4.1.2.2 post configure (named) options with dependencies

```c#
// 1 dep
public class PostConfigureOptions<TOptions, TDep> : IPostConfigureOptions<TOptions>    
    where TOptions : class        
    where TDep : class
{
    public string Name { get; }       
    public Action<TOptions, TDep> Action { get; }
    public TDep Dependency { get; }
    
    public PostConfigureOptions(string name, TDep dependency, Action<TOptions, TDep> action)
    {
        Name = name;
        Action = action;
        Dependency = dependency;
    }
    
    public void PostConfigure(TOptions options) => PostConfigure(Options.DefaultName, options);
    
    public virtual void PostConfigure(string name, TOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        // Null name is used to configure all named options.
        if (Name == null || name == Name)
        {
            Action?.Invoke(options, Dependency);
        }
    }                
}

// 2 dep  
public class PostConfigureOptions<TOptions, TDep1, TDep2> : IPostConfigureOptions<TOptions>     
    where TOptions : class     
    where TDep1 : class     
    where TDep2 : class
{
    public string Name { get; }    
    public Action<TOptions, TDep1, TDep2> Action { get; }    
    public TDep1 Dependency1 { get; }    
    public TDep2 Dependency2 { get; }
    
    public PostConfigureOptions(
        string name, 
        TDep1 dependency, 
        TDep2 dependency2, 
        Action<TOptions, TDep1, TDep2> action)
    {
        Name = name;
        Action = action;
        Dependency1 = dependency;
        Dependency2 = dependency2;
    }
    
    public void PostConfigure(TOptions options) => PostConfigure(Options.DefaultName, options);
    
    public virtual void PostConfigure(string name, TOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        // Null name is used to configure all named options.
        if (Name == null || name == Name)
        {
            Action?.Invoke(options, Dependency1, Dependency2);
        }
    }            
}

// 3 dep    
public class PostConfigureOptions<TOptions, TDep1, TDep2, TDep3> : IPostConfigureOptions<TOptions>        
    where TOptions : class        
    where TDep1 : class        
    where TDep2 : class        
    where TDep3 : class
{
    public string Name { get; }
    public Action<TOptions, TDep1, TDep2, TDep3> Action { get; }    
    public TDep1 Dependency1 { get; }    
    public TDep2 Dependency2 { get; }    
    public TDep3 Dependency3 { get; }
    
    public PostConfigureOptions(
        string name, 
        TDep1 dependency, 
        TDep2 dependency2, 
        TDep3 dependency3, 
        Action<TOptions, TDep1, TDep2, TDep3> action)
    {
        Name = name;
        Action = action;
        Dependency1 = dependency;
        Dependency2 = dependency2;
        Dependency3 = dependency3;
    }
    
    public void PostConfigure(TOptions options) => PostConfigure(Options.DefaultName, options);   
    
    public virtual void PostConfigure(string name, TOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        // Null name is used to configure all named options.
        if (Name == null || name == Name)
        {
            Action?.Invoke(options, Dependency1, Dependency2, Dependency3);
        }
    }            
}

// 4 dep    
public class PostConfigureOptions<TOptions, TDep1, TDep2, TDep3, TDep4> : IPostConfigureOptions<TOptions>        
    where TOptions : class        
    where TDep1 : class        
    where TDep2 : class        
    where TDep3 : class        
    where TDep4 : class
{
    public string Name { get; }    
    public Action<TOptions, TDep1, TDep2, TDep3, TDep4> Action { get; }    
    public TDep1 Dependency1 { get; }        
    public TDep2 Dependency2 { get; }    
    public TDep3 Dependency3 { get; }    
    public TDep4 Dependency4 { get; }
    
    public PostConfigureOptions(
        string name, 
        TDep1 dependency1, 
        TDep2 dependency2, 
        TDep3 dependency3, 
        TDep4 dependency4, 
        Action<TOptions, TDep1, TDep2, TDep3, TDep4> action)
    {
        Name = name;
        Action = action;
        Dependency1 = dependency1;
        Dependency2 = dependency2;
        Dependency3 = dependency3;
        Dependency4 = dependency4;
    }
    
    public void PostConfigure(TOptions options) => PostConfigure(Options.DefaultName, options);
    
    public virtual void PostConfigure(string name, TOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        // Null name is used to configure all named options.
        if (Name == null || name == Name)
        {
            Action?.Invoke(options, Dependency1, Dependency2, Dependency3, Dependency4);
        }
    }            
}

// 5 dep
public class PostConfigureOptions<TOptions, TDep1, TDep2, TDep3, TDep4, TDep5> : IPostConfigureOptions<TOptions>    
    where TOptions : class        
    where TDep1 : class        
    where TDep2 : class        
    where TDep3 : class 
    where TDep4 : class    
    where TDep5 : class
{
    public string Name { get; }        
    public Action<TOptions, TDep1, TDep2, TDep3, TDep4, TDep5> Action { get; }    
    public TDep1 Dependency1 { get; }        
    public TDep2 Dependency2 { get; }        
    public TDep3 Dependency3 { get; }        
    public TDep4 Dependency4 { get; }        
    public TDep5 Dependency5 { get; }
    
    public PostConfigureOptions(
        string name, 
        TDep1 dependency1, 
        TDep2 dependency2, 
        TDep3 dependency3, 
        TDep4 dependency4, 
        TDep5 dependency5, 
        Action<TOptions, TDep1, TDep2, TDep3, TDep4, TDep5> action)
    {
        Name = name;
        Action = action;
        Dependency1 = dependency1;
        Dependency2 = dependency2;
        Dependency3 = dependency3;
        Dependency4 = dependency4;
        Dependency5 = dependency5;
    }
    
    public void PostConfigure(TOptions options) => PostConfigure(Options.DefaultName, options);
    
    public virtual void PostConfigure(string name, TOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        // Null name is used to configure all named options.
        if (Name == null || name == Name)
        {
            Action?.Invoke(options, Dependency1, Dependency2, Dependency3, Dependency4, Dependency5);
        }
    }           
}

```

##### 4.1.3 validate options

```c#
public interface IValidateOptions<TOptions> where TOptions : class
{    
    ValidateOptionsResult Validate(string name, TOptions options);
}

```

###### 4.1.3.1 validate options result

```c#
public class ValidateOptionsResult
{    
    public static readonly ValidateOptionsResult Skip = new ValidateOptionsResult() { Skipped = true };        
    public static readonly ValidateOptionsResult Success = new ValidateOptionsResult() { Succeeded = true };
        
    public bool Succeeded { get; protected set; }        
    public bool Skipped { get; protected set; }        
    public bool Failed { get; protected set; }        
    public string FailureMessage { get; protected set; }        
    public IEnumerable<string> Failures { get; protected set; }
        
    public static ValidateOptionsResult Fail(string failureMessage) => new ValidateOptionsResult 
    { 
        Failed = true, 
        FailureMessage = failureMessage, 
        Failures = new string[] { failureMessage } 
    };
    
    public static ValidateOptionsResult Fail(IEnumerable<string> failures) => new ValidateOptionsResult 
    { 
        Failed = true, 
        FailureMessage = string.Join("; ", failures), 
        Failures = failures 
    };
}

```

###### 4.1.3.2 validate (named) options

```c#
public class ValidateOptions<TOptions> : IValidateOptions<TOptions> where TOptions : class
{
    public string Name { get; }      
    public string FailureMessage { get; }
    public Func<TOptions, bool> Validation { get; }
           
    public ValidateOptions(
        string name, 
        Func<TOptions, bool> validation, 
        string failureMessage)
    {
        Name = name;
        Validation = validation;
        FailureMessage = failureMessage;
    }
    
    public ValidateOptionsResult Validate(string name, TOptions options)
    {
        // 如果 name=Name，或者 name = null，-> invoke
        if (Name == null || name == Name)
        {
            if ((Validation?.Invoke(options)).Value)
            {
                return ValidateOptionsResult.Success;
            }
            return ValidateOptionsResult.Fail(FailureMessage);
        }
        
        // 否则，即 name != Name（不为 null），-> 跳过 validate（返回 skip result）
        return ValidateOptionsResult.Skip;
    }
}

```

###### 4.1.3.3 validate (named) options with dependencies

```c#
// 1 dep
public class ValidateOptions<TOptions, TDep> : IValidateOptions<TOptions> where TOptions : class
{
    public string Name { get; }
    public Func<TOptions, TDep, bool> Validation { get; }    
    public string FailureMessage { get; }    
    public TDep Dependency { get; }
    
    public ValidateOptions(
        string name, 
        TDep dependency, 
        Func<TOptions, TDep, bool> validation, 
        string failureMessage)
    {
        Name = name;
        Validation = validation;
        FailureMessage = failureMessage;
        Dependency = dependency;
    }
    
    public ValidateOptionsResult Validate(string name, TOptions options)
    {
        // null name is used to configure all named options
        if (Name == null || name == Name)
        {
            if ((Validation?.Invoke(options, Dependency)).Value)
            {
                return ValidateOptionsResult.Success;
            }
            return ValidateOptionsResult.Fail(FailureMessage);
        }
        
        // ignored if not validating this instance
        return ValidateOptionsResult.Skip;
    }
}

// 2 dep
public class ValidateOptions<TOptions, TDep1, TDep2> : IValidateOptions<TOptions> where TOptions : class
{
    public string Name { get; }    
    public Func<TOptions, TDep1, TDep2, bool> Validation { get; }        
    public string FailureMessage { get; }    
    public TDep1 Dependency1 { get; }    
    public TDep2 Dependency2 { get; }
    
    public ValidateOptions(
        string name, 
        TDep1 dependency1, 
        TDep2 dependency2, 
        Func<TOptions, TDep1, TDep2, bool> validation, 
        string failureMessage)
    {
        Name = name;
        Validation = validation;
        FailureMessage = failureMessage;
        Dependency1 = dependency1;
        Dependency2 = dependency2;
    }
        
    public ValidateOptionsResult Validate(string name, TOptions options)
    {
        // null name is used to configure all named options
        if (Name == null || name == Name)
        {
            if ((Validation?.Invoke(options, Dependency1, Dependency2)).Value)
            {
                return ValidateOptionsResult.Success;
            }
            return ValidateOptionsResult.Fail(FailureMessage);
        }
        
        // ignored if not validating this instance
        return ValidateOptionsResult.Skip;
    }
}

// 3 dep
public class ValidateOptions<TOptions, TDep1, TDep2, TDep3> : IValidateOptions<TOptions> where TOptions : class
{
    public string Name { get; }        
    public Func<TOptions, TDep1, TDep2, TDep3, bool> Validation { get; }    
    public string FailureMessage { get; }    
    public TDep1 Dependency1 { get; }    
    public TDep2 Dependency2 { get; }
    public TDep3 Dependency3 { get; }
    
    public ValidateOptions(
        string name, 
        TDep1 dependency1, 
        TDep2 dependency2, 
        TDep3 dependency3, 
        Func<TOptions, TDep1, TDep2, TDep3, bool> validation, 
        string failureMessage)
    {
        Name = name;
        Validation = validation;        
        FailureMessage = failureMessage;
        Dependency1 = dependency1;
        Dependency2 = dependency2;
        Dependency3 = dependency3;
    }
        
    public ValidateOptionsResult Validate(string name, TOptions options)
    {
        // null name is used to configure all named options
        if (Name == null || name == Name)
        {
            if ((Validation?.Invoke(options, Dependency1, Dependency2, Dependency3)).Value)
            {
                return ValidateOptionsResult.Success;
            }
            return ValidateOptionsResult.Fail(FailureMessage);
        }
        
        // ignored if not validating this instance
        return ValidateOptionsResult.Skip;
    }
}

// 4 dep
public class ValidateOptions<TOptions, TDep1, TDep2, TDep3, TDep4> : IValidateOptions<TOptions> where TOptions : class
{
    public string Name { get; }        
    public Func<TOptions, TDep1, TDep2, TDep3, TDep4, bool> Validation { get; }        
    public string FailureMessage { get; }        
    public TDep1 Dependency1 { get; }        
    public TDep2 Dependency2 { get; }        
    public TDep3 Dependency3 { get; }        
    public TDep4 Dependency4 { get; }
    
    public ValidateOptions(
        string name, 
        TDep1 dependency1, 
        TDep2 dependency2, 
        TDep3 dependency3, 
        TDep4 dependency4, 
        Func<TOptions, TDep1, TDep2, TDep3, TDep4, bool> validation, string failureMessage)
    {
        Name = name;
        Validation = validation;
        FailureMessage = failureMessage;
        Dependency1 = dependency1;
        Dependency2 = dependency2;
        Dependency3 = dependency3;
        Dependency4 = dependency4;
    }
    
    public ValidateOptionsResult Validate(string name, TOptions options)
    {
        // null name is used to configure all named options
        if (Name == null || name == Name)
        {
            if ((Validation?.Invoke(options, Dependency1, Dependency2, Dependency3, Dependency4)).Value)
            {
                return ValidateOptionsResult.Success;
            }
            return ValidateOptionsResult.Fail(FailureMessage);
        }
        
        // ignored if not validating this instance
        return ValidateOptionsResult.Skip;
    }
}

// 5 dep
public class ValidateOptions<TOptions, TDep1, TDep2, TDep3, TDep4, TDep5> : IValidateOptions<TOptions> where TOptions : class
{
    public string Name { get; }        
    public Func<TOptions, TDep1, TDep2, TDep3, TDep4, TDep5, bool> Validation { get; }        
    public string FailureMessage { get; }        
    public TDep1 Dependency1 { get; }        
    public TDep2 Dependency2 { get; }                
    public TDep3 Dependency3 { get; }        
    public TDep4 Dependency4 { get; }        
    public TDep5 Dependency5 { get; }
    
    public ValidateOptions(
        string name, 
        TDep1 dependency1, 
        TDep2 dependency2, 
        TDep3 dependency3, 
        TDep4 dependency4, 
        TDep5 dependency5, 
        Func<TOptions, TDep1, TDep2, TDep3, TDep4, TDep5, bool> validation, string failureMessage)
    {
        Name = name;
        Validation = validation;
        FailureMessage = failureMessage;
        Dependency1 = dependency1;
        Dependency2 = dependency2;
        Dependency3 = dependency3;
        Dependency4 = dependency4;
        Dependency5 = dependency5;
    }
        
    public ValidateOptionsResult Validate(string name, TOptions options)
    {
        // null name is used to configure all named options
        if (Name == null || name == Name)
        {
            if ((Validation?.Invoke(options, Dependency1, Dependency2, Dependency3, Dependency4, Dependency5)).Value)
            {
                return ValidateOptionsResult.Success;
            }
            return ValidateOptionsResult.Fail(FailureMessage);
        }
        
        // ignored if not validating this instance
        return ValidateOptionsResult.Skip;
    }
}

```

##### 4.1.4 options factory

```c#
public interface IOptionsFactory<[DynamicallyAccessedMembers(Options.DynamicallyAccessedMembers)] TOptions> where TOptions : class
{    
    TOptions Create(string name);
}

```

###### 4.1.4.1 options factory（实现）

```c#
public class OptionsFactory<[DynamicallyAccessedMembers(Options.DynamicallyAccessedMembers)] TOptions> :        
	IOptionsFactory<TOptions> where TOptions : class
{
    private readonly IConfigureOptions<TOptions>[] _setups;
    private readonly IPostConfigureOptions<TOptions>[] _postConfigures;
    private readonly IValidateOptions<TOptions>[] _validations;
        
    public OptionsFactory(
        IEnumerable<IConfigureOptions<TOptions>> setups, 
        IEnumerable<IPostConfigureOptions<TOptions>> postConfigures) : 
    		this(
                setups, 
                postConfigures, 
                validations: Array.Empty<IValidateOptions<TOptions>>())
    {
    }
    
    public OptionsFactory(
        IEnumerable<IConfigureOptions<TOptions>> setups, 
        IEnumerable<IPostConfigureOptions<TOptions>> postConfigures, 
        IEnumerable<IValidateOptions<TOptions>> validations)
    {
        _setups = setups as IConfigureOptions<TOptions>[] ?? setups.ToArray();
        _postConfigures = postConfigures as IPostConfigureOptions<TOptions>[] ?? postConfigures.ToArray();
        _validations = validations as IValidateOptions<TOptions>[] ?? validations.ToArray();
    }
        
    public TOptions Create(string name)
    {
        // 创建 toptions（预结果）
        TOptions options = CreateInstance(name);
        
        // 遍历 configure options，
        foreach (IConfigureOptions<TOptions> setup in _setups)
        {
            if (setup is IConfigureNamedOptions<TOptions> namedSetup)
            {
                namedSetup.Configure(name, options);
            }
            else if (name == Options.DefaultName)
            {
                setup.Configure(options);
            }
        }
        
        // 遍历 post configure options，
        foreach (IPostConfigureOptions<TOptions> post in _postConfigures)
        {
            post.PostConfigure(name, options);
        }
        
        // 如果 validate options 不为 null，遍历 validate options
        if (_validations != null)
        {
            var failures = new List<string>();
            foreach (IValidateOptions<TOptions> validate in _validations)
            {
                ValidateOptionsResult result = validate.Validate(name, options);
                if (result is not null && result.Failed)
                {
                    failures.AddRange(result.Failures);
                }
            }
            if (failures.Count > 0)
            {
                throw new OptionsValidationException(name, typeof(TOptions), failures);
            }
        }
        
        return options;
    }
        
    protected virtual TOptions CreateInstance(string name)
    {
        return Activator.CreateInstance<TOptions>();
    }
}

```

##### 4.1.5 options 静态类

```c#
public static class Options
{    
    internal const DynamicallyAccessedMemberTypes DynamicallyAccessedMembers = 
        DynamicallyAccessedMemberTypes.PublicParameterlessConstructor;
    
    public static readonly string DefaultName = string.Empty;
        
    public static IOptions<TOptions> Create<[DynamicallyAccessedMembers(DynamicallyAccessedMembers)] TOptions>(TOptions options)    
        where TOptions : class
    {
        return new OptionsWrapper<TOptions>(options);
    }
}

```

###### 4.1.5.1 options wrapper

```c#
public class OptionsWrapper<[DynamicallyAccessedMembers(Options.DynamicallyAccessedMembers)] TOptions> :        
	IOptions<TOptions> where TOptions : class
{
    public TOptions Value { get; }
    
    public OptionsWrapper(TOptions options)
    {
        Value = options;
    }    
}

```

#### 4.2 i options

##### 4.2.1 options

```c#
public interface IOptions<[DynamicallyAccessedMembers(Options.DynamicallyAccessedMembers)] out TOptions> where TOptions : class
{   
    TOptions Value { get; }
}

```

###### 4.2.1.1 unnamed options manager

```c#
internal sealed class UnnamedOptionsManager<[DynamicallyAccessedMembers(Options.DynamicallyAccessedMembers)] TOptions> :		
	IOptions<TOptions> where TOptions : class
{
    private readonly IOptionsFactory<TOptions> _factory;
    private volatile object _syncObj;
    
    private volatile TOptions _value;            
    public TOptions Value
    {
        get
        {
            if (_value is TOptions value)
            {
                return value;
            }
            
            lock (_syncObj ?? Interlocked.CompareExchange(ref _syncObj, new object(), null) ?? _syncObj)
            {
                return _value ??= _factory.Create(Options.DefaultName);
            }
        }
    }
    
    public UnnamedOptionsManager(IOptionsFactory<TOptions> factory) => _factory = factory;
}

```

##### 4.2.2 options monitor

```c#
public interface IOptionsMonitor<[DynamicallyAccessedMembers(Options.DynamicallyAccessedMembers)] out TOptions>
{    
    TOptions CurrentValue { get; }
    
    TOptions Get(string name);       
    IDisposable OnChange(Action<TOptions, string> listener);
}

```

###### 4.2.2.1 opitons monitor（实现）

```c#
public class OptionsMonitor<[DynamicallyAccessedMembers(Options.DynamicallyAccessedMembers)] TOptions> :  
	IOptionsMonitor<TOptions>,        
	IDisposable        
        where TOptions : class
{
    private readonly IOptionsMonitorCache<TOptions> _cache;
    private readonly IOptionsFactory<TOptions> _factory;
    private readonly List<IDisposable> _registrations = new List<IDisposable>();
    internal event Action<TOptions, string> _onChange;
      
    public TOptions CurrentValue
    {
        get => Get(Options.DefaultName);
    }
            
    public OptionsMonitor(
        IOptionsFactory<TOptions> factory, 
        IEnumerable<IOptionsChangeTokenSource<TOptions>> sources, 
        IOptionsMonitorCache<TOptions> cache)
    {
        // 注入 options factory
        _factory = factory;
        // 注入 options monitor cache
        _cache = cache;
        
        // 遍历 options change token source，
        foreach (IOptionsChangeTokenSource<TOptions> source in 
                 (sources as IOptionsChangeTokenSource<TOptions>[] ?? sources.ToArray()))
        {
            // 由 options change token source 创建 disposable registration
            IDisposable registration = ChangeToken.OnChange(               
                () => source.GetChangeToken(),	// 注入 get token 钩子
                (name) => InvokeChanged(name),	// 注入 invoke changed 钩子
                source.Name);				   // 注入 options name
            
            // 注入到 registration 集合
            _registrations.Add(registration);
        }
    }
    
    // changed 钩子
    private void InvokeChanged(string name)
    {
        // 解析 options name
        name = name ?? Options.DefaultName;
        // 清除 cache
        _cache.TryRemove(name);
        // 解析 options(with name as parameter)，没有则用 options factory 创建并注入 cache
        TOptions options = Get(name);
        
        // 调用 on change action（customized options changed 钩子）
        if (_onChange != null)
        {
            _onChange.Invoke(options, name);
        }
    }
            
    // cache 解析、注入
    public virtual TOptions Get(string name)
    {
        name = name ?? Options.DefaultName;
        return _cache.GetOrAdd(name, () => _factory.Create(name));
    }
    
    // customized on change handler
    // TOptions 的 实例就是 newer instance，string 是 name！！！
    public IDisposable OnChange(Action<TOptions, string> listener)
    {
        var disposable = new ChangeTrackerDisposable(this, listener);
        _onChange += disposable.OnChange;
        return disposable;
    }
    
    public void Dispose()
    {
        // Remove all subscriptions to the change tokens
        foreach (IDisposable registration in _registrations)
        {
            registration.Dispose();
        }
        
        _registrations.Clear();
    }
    
    internal sealed class ChangeTrackerDisposable : IDisposable
    {
        private readonly Action<TOptions, string> _listener;
        private readonly OptionsMonitor<TOptions> _monitor;
        
        public ChangeTrackerDisposable(OptionsMonitor<TOptions> monitor, Action<TOptions, string> listener)
        {
            _listener = listener;
            _monitor = monitor;
        }
        
        public void OnChange(TOptions options, string name) => _listener.Invoke(options, name);        
        public void Dispose() => _monitor._onChange -= OnChange;
    }
}

```

###### 4.2.2.2 options monitor cache

```c#
public interface IOptionsMonitorCache<[DynamicallyAccessedMembers(Options.DynamicallyAccessedMembers)] TOptions>    
    where TOptions : class
{    
    TOptions GetOrAdd(string name, Func<TOptions> createOptions);        
    bool TryAdd(string name, TOptions options);        
    bool TryRemove(string name);        
    void Clear();
}

```

###### 4.2.2.3 options change token source

```c#
public interface IOptionsChangeTokenSource<out TOptions>
{    
    string Name { get; }
    IChangeToken GetChangeToken();        
}

```

###### 4.2.2.4 扩展方法 - on change

```c#
public static class OptionsMonitorExtensions
{   
    public static IDisposable OnChange<[DynamicallyAccessedMembers(Options.DynamicallyAccessedMembers)] TOptions>(
        this IOptionsMonitor<TOptions> monitor,
        Action<TOptions> listener) => 
        	monitor.OnChange((o, _) => listener(o));
}

```

##### 4.2.3 options snapshot

```c#
public interface IOptionsSnapshot<[DynamicallyAccessedMembers(Options.DynamicallyAccessedMembers)] out TOptions> :      
	IOptions<TOptions> where TOptions : class
{    
    TOptions Get(string name);
}

```

###### 4.2.3.1 options manager（实现）

```c#
public class OptionsManager<[DynamicallyAccessedMembers(Options.DynamicallyAccessedMembers)] TOptions> :        
	IOptions<TOptions>,        
	IOptionsSnapshot<TOptions>        
        where TOptions : class
{
    private readonly IOptionsFactory<TOptions> _factory;
    private readonly OptionsCache<TOptions> _cache = new OptionsCache<TOptions>();
    
    // 获取 options（name = default name）
    public TOptions Value => Get(Options.DefaultName);
    
    public OptionsManager(IOptionsFactory<TOptions> factory)
    {
        // 注入 options factory
        _factory = factory;
    }
        
    public virtual TOptions Get(string name)
    {
        // 解析 options name
        name = name ?? Options.DefaultName;
        
        // 如果 options cache 不能解析 options，
        if (!_cache.TryGetValue(name, out TOptions options))
        {            
            IOptionsFactory<TOptions> localFactory = _factory;
            string localName = name;
            // 从 options cache 解析、注入
            options = _cache.GetOrAdd(name, () => localFactory.Create(localName));
        }
        
        // （由上），返回 options cache 解析的 options
        return options;
    }
}

```

###### 4.2.3.2 options cache

```c#
public class OptionsCache<[DynamicallyAccessedMembers(Options.DynamicallyAccessedMembers)] TOptions> :   
	IOptionsMonitorCache<TOptions> where TOptions : class
{
    private readonly ConcurrentDictionary<string, Lazy<TOptions>> _cache = 
        new ConcurrentDictionary<string, Lazy<TOptions>>(
        	concurrencyLevel: 1, 
	        // 31 == default capacity
        	capacity: 31, 
        	StringComparer.Ordinal); 
       
    // get or add
    public virtual TOptions GetOrAdd(string name, Func<TOptions> createOptions)
    {
        if (createOptions == null)
        {
            throw new ArgumentNullException(nameof(createOptions));
        }
        
        name = name ?? Options.DefaultName;
        Lazy<TOptions> value;

#if NETSTANDARD2_1
        value = _cache.GetOrAdd(name, static (name, createOptions) => new Lazy<TOptions>(createOptions), createOptions);
#else
        if (!_cache.TryGetValue(name, out value))
        {
            value = _cache.GetOrAdd(name, new Lazy<TOptions>(createOptions));
        }
#endif

        return value.Value;
    }

    // try add
    public virtual bool TryAdd(string name, TOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        return _cache.TryAdd(name ?? Options.DefaultName, new Lazy<TOptions>(
#if !NETSTANDARD2_1
           () =>
#endif
           options));
    }
        
    internal bool TryGetValue(string name, out TOptions options)
    {
        if (_cache.TryGetValue(name ?? Options.DefaultName, out Lazy<TOptions> lazy))
        {
            options = lazy.Value;
            return true;
        }
        
        options = default;
        return false;
    }
                  
    // try remove
    public virtual bool TryRemove(string name) => _cache.TryRemove(name ?? Options.DefaultName, out _);
    
    // clear
    public void Clear() => _cache.Clear();
}

```

#### 4.3 options builder

```c#
public class OptionsBuilder<TOptions> where TOptions : class
{
    private const string DefaultValidationFailureMessage = "A validation error has occurred.";
    
    public string Name { get; }        
    public IServiceCollection Services { get; }
           
    public OptionsBuilder(IServiceCollection services, string name)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        Services = services;
        Name = name ?? Options.DefaultName;
    }
}

```

##### 4.3.1 configure options

###### 4.3.1.1 configure options

```c#
public class OptionsBuilder<TOptions> where TOptions : class
{    
    public virtual OptionsBuilder<TOptions> Configure(Action<TOptions> configureOptions)
    {
        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }
        
        // 注入 configure options t
        Services.AddSingleton<IConfigureOptions<TOptions>>(
            new ConfigureNamedOptions<TOptions>(
                Name, 
                configureOptions));
        
        return this;
    }
}

```

###### 4.3.1.2 configure options with dependencies

```c#
public class OptionsBuilder<TOptions> where TOptions : class
{
    // 注入 configure options t with dependencies
    
    // 1 dep
    public virtual OptionsBuilder<TOptions> Configure<TDep>(
        Action<TOptions, TDep> configureOptions) 
        	where TDep : class
    {
        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }
        
        Services.AddTransient<IConfigureOptions<TOptions>>(
            sp => new ConfigureNamedOptions<TOptions, TDep>(
                	Name, 
                	sp.GetRequiredService<TDep>(), 
                	configureOptions));
        
        return this;
    }
     
    // 2 dep
    public virtual OptionsBuilder<TOptions> Configure<TDep1, TDep2>(
        Action<TOptions, TDep1, TDep2> configureOptions)            
        	where TDep1 : class            
        	where TDep2 : class
    {
        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }
        
        Services.AddTransient<IConfigureOptions<TOptions>>(
            sp => new ConfigureNamedOptions<TOptions, TDep1, TDep2>(
                Name, 
                sp.GetRequiredService<TDep1>(), 
                sp.GetRequiredService<TDep2>(), 
                configureOptions));
        
        return this;
    }
       
    // 3 dep
    public virtual OptionsBuilder<TOptions> Configure<TDep1, TDep2, TDep3>(
        Action<TOptions, TDep1, TDep2, TDep3> configureOptions)            
        	where TDep1 : class            
	        where TDep2 : class            
         	where TDep3 : class
    {
        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }
        
        Services.AddTransient<IConfigureOptions<TOptions>>(
            sp => new ConfigureNamedOptions<TOptions, TDep1, TDep2, TDep3>(
                Name,
                sp.GetRequiredService<TDep1>(),
                sp.GetRequiredService<TDep2>(),
                sp.GetRequiredService<TDep3>(),
                configureOptions));
        
        return this;
    }
     
    // 4 dep
    public virtual OptionsBuilder<TOptions> Configure<TDep1, TDep2, TDep3, TDep4>(
        Action<TOptions, TDep1, TDep2, TDep3, TDep4> configureOptions)       
        	where TDep1 : class        
           	where TDep2 : class      
           	where TDep3 : class       
          	where TDep4 : class
    {
        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }
        
        Services.AddTransient<IConfigureOptions<TOptions>>(
            sp => new ConfigureNamedOptions<TOptions, TDep1, TDep2, TDep3, TDep4>(
                Name,
                sp.GetRequiredService<TDep1>(),
                sp.GetRequiredService<TDep2>(),
                sp.GetRequiredService<TDep3>(),
                sp.GetRequiredService<TDep4>(),
                configureOptions));
        
        return this;
    }
      
    // 5 dep
    public virtual OptionsBuilder<TOptions> Configure<TDep1, TDep2, TDep3, TDep4, TDep5>(
        Action<TOptions, TDep1, TDep2, TDep3, TDep4, TDep5> configureOptions)         
        	where TDep1 : class         
	        where TDep2 : class        
	        where TDep3 : class      
	        where TDep4 : class        
	        where TDep5 : class
    {
        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }
        
        Services.AddTransient<IConfigureOptions<TOptions>>(
            sp => new ConfigureNamedOptions<TOptions, TDep1, TDep2, TDep3, TDep4, TDep5>(
                Name,
                sp.GetRequiredService<TDep1>(),
                sp.GetRequiredService<TDep2>(),
                sp.GetRequiredService<TDep3>(),
                sp.GetRequiredService<TDep4>(),
                sp.GetRequiredService<TDep5>(),
                configureOptions));
        
        return this;
    }
}

```

##### 4.3.2 post configure options

###### 4.3.2.1 post configure options

```c#
public class OptionsBuilder<TOptions> where TOptions : class
{            
    public virtual OptionsBuilder<TOptions> PostConfigure(Action<TOptions> configureOptions)
    {
        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }
        
        // 注入 post configure options t
        Services.AddSingleton<IPostConfigureOptions<TOptions>>(
            new PostConfigureOptions<TOptions>(Name, configureOptions));
        
        return this;
    }    
}

```

###### 4.3.2.2 post configure options with dependencies

```c#
public class OptionsBuilder<TOptions> where TOptions : class
{         
    // 注入 post configure options t with dependencies
    
    // 1 dep   
    public virtual OptionsBuilder<TOptions> PostConfigure<TDep>(
        Action<TOptions, TDep> configureOptions) 
        	where TDep : class
    {
        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }
        
        Services.AddTransient<IPostConfigureOptions<TOptions>>(
            sp => new PostConfigureOptions<TOptions, TDep>(
                Name, 
                sp.GetRequiredService<TDep>(), 
                configureOptions));
                
        return this;
    }
        
    // 2 dep
    public virtual OptionsBuilder<TOptions> PostConfigure<TDep1, TDep2>(
        Action<TOptions, TDep1, TDep2> configureOptions)           
        	where TDep1 : class 
          	where TDep2 : class
    {
        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }
        
        Services.AddTransient<IPostConfigureOptions<TOptions>>(
            sp => new PostConfigureOptions<TOptions, TDep1, TDep2>(
                Name, 
                sp.GetRequiredService<TDep1>(), 
                sp.GetRequiredService<TDep2>(), 
                configureOptions));
        
        return this;
    }
        
    // 3 dep
    public virtual OptionsBuilder<TOptions> PostConfigure<TDep1, TDep2, TDep3>(
        Action<TOptions, TDep1, TDep2, TDep3> configureOptions)            
        	where TDep1 : class     
         	where TDep2 : class   
        	where TDep3 : class
    {
        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }
        
        Services.AddTransient<IPostConfigureOptions<TOptions>>(
            sp => new PostConfigureOptions<TOptions, TDep1, TDep2, TDep3>(
                Name,
                sp.GetRequiredService<TDep1>(),
                sp.GetRequiredService<TDep2>(),
                sp.GetRequiredService<TDep3>(),
                configureOptions));
        
        return this;
    }
       
    // 4 dep
    public virtual OptionsBuilder<TOptions> PostConfigure<TDep1, TDep2, TDep3, TDep4>(
        Action<TOptions, TDep1, TDep2, TDep3, TDep4> configureOptions)        
        	where TDep1 : class
	        where TDep2 : class         
    	    where TDep3 : class    
        	where TDep4 : class
    {
        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }
        
        Services.AddTransient<IPostConfigureOptions<TOptions>>(
            sp => new PostConfigureOptions<TOptions, TDep1, TDep2, TDep3, TDep4>(
                Name,
                sp.GetRequiredService<TDep1>(),
                sp.GetRequiredService<TDep2>(),
                sp.GetRequiredService<TDep3>(),
                sp.GetRequiredService<TDep4>(),
                configureOptions));
        
        return this;
    }
        
    // 5 dep
    public virtual OptionsBuilder<TOptions> PostConfigure<TDep1, TDep2, TDep3, TDep4, TDep5>(
        Action<TOptions, TDep1, TDep2, TDep3, TDep4, TDep5> configureOptions)         
	        where TDep1 : class        
            where TDep2 : class      
            where TDep3 : class          
            where TDep4 : class         
            where TDep5 : class
    {
        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }
        
        Services.AddTransient<IPostConfigureOptions<TOptions>>(
            sp => new PostConfigureOptions<TOptions, TDep1, TDep2, TDep3, TDep4, TDep5>(
                Name,
                sp.GetRequiredService<TDep1>(),
                sp.GetRequiredService<TDep2>(),
                sp.GetRequiredService<TDep3>(),
                sp.GetRequiredService<TDep4>(),
                sp.GetRequiredService<TDep5>(),
                configureOptions));
        
        return this;
    }
}

```

##### 4.3.3 validate configure options

###### 4.3.3.1 validate configure options

```c#
public class OptionsBuilder<TOptions> where TOptions : class
{   
    // 注入 validate options t
    public virtual OptionsBuilder<TOptions> Validate(Func<TOptions, bool> validation) => 
        Validate(validation: validation, failureMessage: DefaultValidationFailureMessage);
        
    public virtual OptionsBuilder<TOptions> Validate(Func<TOptions, bool> validation, string failureMessage)
    {
        if (validation == null)
        {
            throw new ArgumentNullException(nameof(validation));
        }
        
        Services.AddSingleton<IValidateOptions<TOptions>>(
            new ValidateOptions<TOptions>(Name, validation, failureMessage));
        return this;
    }        
}

```

###### 4.3.3.2 validate configure options with dependencies

```c#
public class OptionsBuilder<TOptions> where TOptions : class
{           
    // 注入 validate options t with dependencies
    
    // 1 dep
    public virtual OptionsBuilder<TOptions> Validate<TDep>(
        Func<TOptions, TDep, bool> validation) => 
        	Validate(
        		validation: validation, 
        		failureMessage: DefaultValidationFailureMessage);
        
    public virtual OptionsBuilder<TOptions> Validate<TDep>(
        Func<TOptions, TDep, bool> validation, 
        string failureMessage)
    {
        if (validation == null)
        {
            throw new ArgumentNullException(nameof(validation));
        }
        
        Services.AddTransient<IValidateOptions<TOptions>>(
            sp => new ValidateOptions<TOptions, TDep>(
                Name, 
                sp.GetRequiredService<TDep>(), 
                validation, 
                failureMessage));
        
        return this;
    }
        
    // 2 dep
    public virtual OptionsBuilder<TOptions> Validate<TDep1, TDep2>(
        Func<TOptions, TDep1, TDep2, bool> validation) => 
        	Validate(
        		validation: validation, 
        		failureMessage: DefaultValidationFailureMessage);
        
    public virtual OptionsBuilder<TOptions> Validate<TDep1, TDep2>(
        Func<TOptions, TDep1, TDep2, bool> validation, 
        string failureMessage)
    {
        if (validation == null)
        {
            throw new ArgumentNullException(nameof(validation));
        }
        
        Services.AddTransient<IValidateOptions<TOptions>>(
            sp => new ValidateOptions<TOptions, TDep1, TDep2>(
                Name,
                sp.GetRequiredService<TDep1>(),
                sp.GetRequiredService<TDep2>(),
                validation,
                failureMessage));
        
        return this;
    }
    
    // 3 dep
    public virtual OptionsBuilder<TOptions> Validate<TDep1, TDep2, TDep3>(
        Func<TOptions, TDep1, TDep2, TDep3, bool> validation) => 
        	Validate(
        		validation: validation, 
        		failureMessage: DefaultValidationFailureMessage);
        
    public virtual OptionsBuilder<TOptions> Validate<TDep1, TDep2, TDep3>(
        Func<TOptions, TDep1, TDep2, TDep3, bool> validation, 
        string failureMessage)
    {
        if (validation == null)
        {
            throw new ArgumentNullException(nameof(validation));
        }
        
        Services.AddTransient<IValidateOptions<TOptions>>(
            sp => new ValidateOptions<TOptions, TDep1, TDep2, TDep3>(
                Name,
                sp.GetRequiredService<TDep1>(),
                sp.GetRequiredService<TDep2>(),
                sp.GetRequiredService<TDep3>(),
                validation,
                failureMessage));
        
        return this;
    }    
    
    // 4 dep
    public virtual OptionsBuilder<TOptions> Validate<TDep1, TDep2, TDep3, TDep4>(
        Func<TOptions, TDep1, TDep2, TDep3, TDep4, bool> validation) => 
        	Validate(
        		validation: validation, 
        		failureMessage: DefaultValidationFailureMessage);
        
    public virtual OptionsBuilder<TOptions> Validate<TDep1, TDep2, TDep3, TDep4>(
        Func<TOptions, TDep1, TDep2, TDep3, TDep4, bool> validation, 
        string failureMessage)
    {
        if (validation == null)
        {
            throw new ArgumentNullException(nameof(validation));
        }
        
        Services.AddTransient<IValidateOptions<TOptions>>(
            sp => new ValidateOptions<TOptions, TDep1, TDep2, TDep3, TDep4>(
                Name,
                sp.GetRequiredService<TDep1>(),
                sp.GetRequiredService<TDep2>(),
                sp.GetRequiredService<TDep3>(),
                sp.GetRequiredService<TDep4>(),
                validation,
                failureMessage));
        
        return this;
    }
    
    // 5 dep
    public virtual OptionsBuilder<TOptions> Validate<TDep1, TDep2, TDep3, TDep4, TDep5>(
        Func<TOptions, TDep1, TDep2, TDep3, TDep4, TDep5, bool> validation) => 
        	Validate(
        		validation: validation, 
        		failureMessage: DefaultValidationFailureMessage);
        
    public virtual OptionsBuilder<TOptions> Validate<TDep1, TDep2, TDep3, TDep4, TDep5>(
        Func<TOptions, TDep1, TDep2, TDep3, TDep4, TDep5, bool> validation, 
        string failureMessage)
    {
        if (validation == null)
        {
            throw new ArgumentNullException(nameof(validation));
        }
        
        Services.AddTransient<IValidateOptions<TOptions>>(
            sp => new ValidateOptions<TOptions, TDep1, TDep2, TDep3, TDep4, TDep5>(
                Name,
                sp.GetRequiredService<TDep1>(),
                sp.GetRequiredService<TDep2>(),
                sp.GetRequiredService<TDep3>(),
                sp.GetRequiredService<TDep4>(),
                sp.GetRequiredService<TDep5>(),
                validation,
                failureMessage));
        
        return this;
    }
}

```

#### 4.4 options services

##### 4.4.1 add options

```c#
public static class OptionsServiceCollectionExtensions
{    
    // add options，注入 options service 必须的组件
    public static IServiceCollection AddOptions(this IServiceCollection services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        // 注入 ioptions
        services.TryAdd(ServiceDescriptor.Singleton(typeof(IOptions<>), typeof(UnnamedOptionsManager<>)));
        // 注入 ioptions snapshot
        services.TryAdd(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(OptionsManager<>)));
        // 注入 ioptions monitor
        services.TryAdd(ServiceDescriptor.Singleton(typeof(IOptionsMonitor<>), typeof(OptionsMonitor<>)));
        // 注入 option factory
        services.TryAdd(ServiceDescriptor.Transient(typeof(IOptionsFactory<>), typeof(OptionsFactory<>)));
        // 注入 option monitor cache
        services.TryAdd(ServiceDescriptor.Singleton(typeof(IOptionsMonitorCache<>), typeof(OptionsCache<>)));
        
        return services;
    }                                                                                                                       
}

```

##### 4.4.2 add options of t

```c#
public static class OptionsServiceCollectionExtensions
{        
    // add TOptions,
    // 返回 options builder，可以用 options builder 配置 TOptions
    public static OptionsBuilder<TOptions> AddOptions<TOptions>(this IServiceCollection services) 
        where TOptions : class => 
            services.AddOptions<TOptions>(Options.Options.DefaultName);
        
    public static OptionsBuilder<TOptions> AddOptions<TOptions>(this IServiceCollection services, string name)        
        where TOptions : class
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        services.AddOptions();
        return new OptionsBuilder<TOptions>(services, name);
    }
}

```

##### 4.4.3 configure options

###### 4.4.3.1 configure options of t

```c#
public static class OptionsServiceCollectionExtensions
{   
    public static IServiceCollection Configure<TOptions>(
        this IServiceCollection services, 
        Action<TOptions> configureOptions) 
        	where TOptions : class => 
            	services.Configure(Options.Options.DefaultName, configureOptions);
        
    public static IServiceCollection Configure<TOptions>(
        this IServiceCollection services, 
        string name, 
        Action<TOptions> configureOptions)
            where TOptions : class
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }                
        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }
                
        // 注入 options 服务
        services.AddOptions();
        // 注入 options with name（configure named options）
        services.AddSingleton<IConfigureOptions<TOptions>>(
            new ConfigureNamedOptions<TOptions>(name, configureOptions));
                
        return services;
    }
    
    public static IServiceCollection ConfigureAll<TOptions>(
        this IServiceCollection services, 
        Action<TOptions> configureOptions) where TOptions : class => 
        	services.Configure(name: null, configureOptions: configureOptions);
}

```

###### 4.4.3.2 configure options by type

```c#
public static class OptionsServiceCollectionExtensions
{   
    public static IServiceCollection ConfigureOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TConfigureOptions>(this IServiceCollection services)
        where TConfigureOptions : class => 
            services.ConfigureOptions(typeof(TConfigureOptions));

    public static IServiceCollection ConfigureOptions(
        this IServiceCollection services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type configureType)
    {
        // 注册 options 服务
        services.AddOptions();
        
        bool added = false;
        
        // 遍历解析到的 TOptions 配置接口，即 IConfigureOptions、IPostConfigureOptions、IValidateOptions
        foreach (Type serviceType in FindConfigurationServices(configureType))
        {
            // 注册 TOptions，对应的 instance type 就是 configure type
            services.AddTransient(serviceType, configureType);
            added = true;
        }
        
        if (!added)
        {
            ThrowNoConfigServices(configureType);
        }
        
        return services;
    }
    
    /* 解析 configuration service 实现的 toptions 配置接口 */
    private static IEnumerable<Type> FindConfigurationServices(Type type)
    {
        // 遍历 type 的 interface
        foreach (Type t in type.GetInterfaces())
        {
            // 如果 interface 是 generic interface
            if (t.IsGenericType)
            {
                // 如果 interface 是 IConfigureOptions / IPostConfigureOptions / IValidateOptions，
                // 递归，即找到上述接口的 T，它是 TOptions
                Type gtd = t.GetGenericTypeDefinition();
                if (gtd == typeof(IConfigureOptions<>) ||
                    gtd == typeof(IPostConfigureOptions<>) ||
                    gtd == typeof(IValidateOptions<>))
                {
                    yield return t;
                }
            }
        }
    }
    
    private static void ThrowNoConfigServices(Type type) =>
        throw new InvalidOperationException(
        	type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Action<>) ?
        	SR.Error_NoConfigurationServicesAndAction :
        	SR.Error_NoConfigurationServices);
}

```

###### 4.4.3.3 configure options by instance

```c#
public static class OptionsServiceCollectionExtensions
{ 
    public static IServiceCollection ConfigureOptions(this IServiceCollection services, object configureInstance)
    {
        // 注入 options 服务
        services.AddOptions();
        // 解析 configure type
        Type configureType = configureInstance.GetType();
        
        bool added = false;
        
        // 遍历 configure type 中 toptions 配置接口，即 IConfigureOptions、IPostConfigureOptions、IValidateOptions
        foreach (Type serviceType in FindConfigurationServices(configureType))
        {
            services.AddSingleton(serviceType, configureInstance);
            added = true;
        }
        
        if (!added)
        {
            ThrowNoConfigServices(configureType);
        }
        
        return services;
    }
}

```

##### 4.4.4 post configure options

```c#
public static class OptionsServiceCollectionExtensions
{ 
    public static IServiceCollection PostConfigure<TOptions>(
        this IServiceCollection services, 
        Action<TOptions> configureOptions) where TOptions : class => 
        	services.PostConfigure(Options.Options.DefaultName, configureOptions);
        
    public static IServiceCollection PostConfigure<TOptions>(
        this IServiceCollection services, 
        string name, 
        Action<TOptions> configureOptions) where TOptions : class
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }        
        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }
        
        // 注册 options 服务
        services.AddOptions();
        // 注入 post configure options（post configure options T）
        services.AddSingleton<IPostConfigureOptions<TOptions>>(
            new PostConfigureOptions<TOptions>(name, configureOptions));
        return services;
    }
    
     public static IServiceCollection PostConfigureAll<TOptions>(
        this IServiceCollection services, 
        Action<TOptions> configureOptions) where TOptions : class => 
        	services.PostConfigure(name: null, configureOptions: configureOptions);      
}

```

#### 4.5 options with annotation

##### 4.5.1 data annotation validate options

```c#
public class DataAnnotationValidateOptions<TOptions> : IValidateOptions<TOptions> where TOptions : class
{
    public string Name { get; }
    
    public DataAnnotationValidateOptions(string name)
    {
        Name = name;
    }
                        
    public ValidateOptionsResult Validate(string name, TOptions options)
    {
        // Null name is used to configure all named options.
        if (Name == null || name == Name)
        {
            var validationResults = new List<ValidationResult>();
            
            // 使用 (data annotation) Validator 验证
            if (Validator.TryValidateObject(
                	options,
                	new ValidationContext(options, serviceProvider: null, items: null),
                	validationResults,
                	validateAllProperties: true))
            {
                return ValidateOptionsResult.Success;
            }
            
            var errors = new List<string>();
            foreach (ValidationResult r in validationResults)
            {
                errors.Add($"DataAnnotation validation failed for members: '{string.Join(",", r.MemberNames)}' 
                           "with the error: '{r.ErrorMessage}'.");
            }
            return ValidateOptionsResult.Fail(errors);
        }
        
        // Ignored if not validating this instance.
        return ValidateOptionsResult.Skip;
    }
}

```

##### 4.5.2 options builder data annotation extension

```c#
public static class OptionsBuilderDataAnnotationsExtensions
{    
    public static OptionsBuilder<TOptions> ValidateDataAnnotations<TOptions>(this OptionsBuilder<TOptions> optionsBuilder) 
        where TOptions : class
    {
        optionsBuilder.Services
            		 .AddSingleton<IValidateOptions<TOptions>>(new DataAnnotationValidateOptions<TOptions>(optionsBuilder.Name));
        
        return optionsBuilder;
    }
}

```

#### 4.6 options with configuration

##### 4.6.1 configure options with configuration

###### 4.6.1.1 configure from configuration options

```c#
public class ConfigureFromConfigurationOptions<TOptions> : 
	ConfigureOptions<TOptions> where TOptions : class
{    
    public ConfigureFromConfigurationOptions(IConfiguration config) : 
    	base(options => ConfigurationBinder.Bind(config, options))
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }
    }
}

```

###### 4.6.1.2 named configure from configuration opitons

```c#
public class NamedConfigureFromConfigurationOptions<TOptions> : 
	ConfigureNamedOptions<TOptions> where TOptions : class
{        
    public NamedConfigureFromConfigurationOptions(
        string name, 
        IConfiguration config) : 
        	this(
                name, 
                config, _ => { })
    {
    }
        
    public NamedConfigureFromConfigurationOptions(
        string name, 
        IConfiguration config, 
        Action<BinderOptions> configureBinder) : 
        	base(
                name, 
                options => config.Bind(options, configureBinder))
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }
    }
}

```

##### 4.6.2 configuration change token source

```c#
public class ConfigurationChangeTokenSource<TOptions> : IOptionsChangeTokenSource<TOptions>
{
    private IConfiguration _config;
    public string Name { get; }
        
    public ConfigurationChangeTokenSource(IConfiguration config) : this(Options.DefaultName, config)
    {
    }
        
    public ConfigurationChangeTokenSource(
        string name, 
        IConfiguration config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }
        
        _config = config;
        Name = name ?? Options.DefaultName;
    }
        
    public IChangeToken GetChangeToken()
    {
        return _config.GetReloadToken();
    }
}

```

##### 4.6.3 扩展方法 - by option builder

```c#
public static class OptionsBuilderConfigurationExtensions
{    
    public static OptionsBuilder<TOptions> Bind<TOptions>(
        this OptionsBuilder<TOptions> optionsBuilder, 
        IConfiguration config) 
        	where TOptions : class => 
        		optionsBuilder.Bind(config, _ => { });
        
    public static OptionsBuilder<TOptions> Bind<TOptions>(
        this OptionsBuilder<TOptions> optionsBuilder, 
        IConfiguration config, 
        Action<BinderOptions> configureBinder) 
        	where TOptions : class
    {
        if (optionsBuilder == null)
        {
            throw new ArgumentNullException(nameof(optionsBuilder));
        }
        
        optionsBuilder.Services.Configure<TOptions>(
            optionsBuilder.Name, 
            config, 
            configureBinder);
                
        return optionsBuilder;
    }
        
    public static OptionsBuilder<TOptions> BindConfiguration<TOptions>(
        this OptionsBuilder<TOptions> optionsBuilder,
        string configSectionPath,
        Action<BinderOptions> configureBinder = null)            
        	where TOptions : class
    {
        _ = optionsBuilder ?? throw new ArgumentNullException(nameof(optionsBuilder));
        _ = configSectionPath ?? throw new ArgumentNullException(nameof(configSectionPath));
        
        optionsBuilder.Configure<IConfiguration>(
            (opts, config) =>
            {
                IConfiguration section = string.Equals(
                    "", 
                    configSectionPath, 
                    tringComparison.OrdinalIgnoreCase)
                    	? config
                    	: config.GetSection(configSectionPath);
                
                section.Bind(opts, configureBinder);
            });
        
        return optionsBuilder;
    }
}

```

##### 4.6.4 扩展方法 - by options server

```c#
public static class OptionsConfigurationServiceCollectionExtensions
{       
    public static IServiceCollection Configure<TOptions>(
        this IServiceCollection services, 
        IConfiguration config) 
        	where TOptions : class => 
                services.Configure<TOptions>(Options.Options.DefaultName, config);
        
    public static IServiceCollection Configure<TOptions>(
        this IServiceCollection services, 
        string name, 
        IConfiguration config) 
        	where TOptions : class => 
                services.Configure<TOptions>(name, config, _ => { });
       
    public static IServiceCollection Configure<TOptions>(
        this IServiceCollection services, 
        IConfiguration config, 
        Action<BinderOptions> configureBinder)
            where TOptions : class => 
                services.Configure<TOptions>(Options.Options.DefaultName, config, configureBinder);
        
    public static IServiceCollection Configure<TOptions>(
        this IServiceCollection services, 
        string name, 
        IConfiguration config, 
        Action<BinderOptions> configureBinder)            
        	where TOptions : class
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }        
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }
        
        // 注册 options 服务
        services.AddOptions();
                
        // 注入 configuration change token
        services.AddSingleton<IOptionsChangeTokenSource<TOptions>>(
            new ConfigurationChangeTokenSource<TOptions>(name, config));
        
        // 注入 named configure from configuration options
        return services.AddSingleton<IConfigureOptions<TOptions>>(
            new NamedConfigureFromConfigurationOptions<TOptions>(name, config, configureBinder));
    }
}

```









