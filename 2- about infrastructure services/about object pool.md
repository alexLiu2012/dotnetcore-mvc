## about object pool



### 1. about



### 2. object pool

#### 2.1 object pool policy

```c#
public interface IPooledObjectPolicy<T> where T : notnull
{    
    T Create();    
    bool Return(T obj);
}

```

##### 2.1.1 pooled object policy

```c#
 public abstract class PooledObjectPolicy<T> : IPooledObjectPolicy<T> where T : notnull
 {     
     public abstract T Create();          
     public abstract bool Return(T obj);
 }

```

##### 2.1.1 default pooled object policy

```c#
public class DefaultPooledObjectPolicy<T> : PooledObjectPolicy<T> where T : class, new()
{    
    public override T Create()
    {
        return new T();
    }
       
    public override bool Return(T obj)
    {
        // DefaultObjectPool<T> doesn't call 'Return' for the default policy.
        // So take care adding any logic to this method, as it might require changes elsewhere.
        return true;
    }
}

```

##### 2.1.3 string builder policy

```c#
public class StringBuilderPooledObjectPolicy : PooledObjectPolicy<StringBuilder>
{    
    public int InitialCapacity { get; set; } = 100;        
    public int MaximumRetainedCapacity { get; set; } = 4 * 1024;
       
    public override StringBuilder Create()
    {
        return new StringBuilder(InitialCapacity);
    }
        
    public override bool Return(StringBuilder obj)
    {
        if (obj.Capacity > MaximumRetainedCapacity)
        {
            // Too big. Discard this one.
            return false;
        }
        
        obj.Clear();
        return true;
    }
}

```

#### 2.1 object pool

```c#
public abstract class ObjectPool<T> where T : class
{    
    public abstract T Get();       
    public abstract void Return(T obj);
}

    
    public static class ObjectPool
    {
        /// <inheritdoc />
        public static ObjectPool<T> Create<T>(IPooledObjectPolicy<T>? policy = null) where T : class, new()
        {
            var provider = new DefaultObjectPoolProvider();
            return provider.Create(policy ?? new DefaultPooledObjectPolicy<T>());
        }
    }
```

##### 2.1.1 default object pool

```c#
public class DefaultObjectPool<T> : ObjectPool<T> where T : class
{
    private protected readonly ObjectWrapper[] _items;
    private protected readonly IPooledObjectPolicy<T> _policy;
    private protected readonly bool _isDefaultPolicy;
    private protected T? _firstItem;
    
    // This class was introduced in 2.1 to avoid the interface call where possible
    private protected readonly PooledObjectPolicy<T>? _fastPolicy;
    
       
    public DefaultObjectPool(IPooledObjectPolicy<T> policy)            : this(policy, Environment.ProcessorCount * 2)
    {
    }
    
        /// <summary>
        /// Creates an instance of <see cref="DefaultObjectPool{T}"/>.
        /// </summary>
        /// <param name="policy">The pooling policy to use.</param>
        /// <param name="maximumRetained">The maximum number of objects to retain in the pool.</param>
    public DefaultObjectPool(IPooledObjectPolicy<T> policy, int maximumRetained)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _fastPolicy = policy as PooledObjectPolicy<T>;
        _isDefaultPolicy = IsDefaultPolicy();
        
        // -1 due to _firstItem
        _items = new ObjectWrapper[maximumRetained - 1];
        
        bool IsDefaultPolicy()
        {
            var type = policy.GetType();            
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(DefaultPooledObjectPolicy<>);
        }
    }
    
    /// <inheritdoc />
    public override T Get()
    {
        var item = _firstItem;
        if (item == null || Interlocked.CompareExchange(ref _firstItem, null, item) != item)
        {
            var items = _items;
            for (var i = 0; i < items.Length; i++)
            {
                item = items[i].Element;
                if (item != null && Interlocked.CompareExchange(ref items[i].Element, null, item) == item)
                {
                    return item;
                }
            }
            
            item = Create();
        }
        
        return item;
    }
    
    // Non-inline to improve its code quality as uncommon path
    [MethodImpl(MethodImplOptions.NoInlining)]
    private T Create() => _fastPolicy?.Create() ?? _policy.Create();
    
    /// <inheritdoc />
    public override void Return(T obj)
    {
        if (_isDefaultPolicy || (_fastPolicy?.Return(obj) ?? _policy.Return(obj)))
        {
            if (_firstItem != null || Interlocked.CompareExchange(ref _firstItem, obj, null) != null)
            {
                var items = _items;
                for (var i = 0; i < items.Length && Interlocked.CompareExchange(ref items[i].Element, obj, null) != null; ++i)
                {
                }
            }
        }
    }
    
    // PERF: the struct wrapper avoids array-covariance-checks from the runtime when assigning to elements of the array.
    [DebuggerDisplay("{Element}")]
    private protected struct ObjectWrapper
    {
        public T? Element;
    }
}

```

##### 2.1.2 disposable object pool

```c#
internal sealed class DisposableObjectPool<T> : DefaultObjectPool<T>, IDisposable where T : class
{
    private volatile bool _isDisposed;
    
    public DisposableObjectPool(IPooledObjectPolicy<T> policy)        : base(policy)
    {
    }
    
    public DisposableObjectPool(IPooledObjectPolicy<T> policy, int maximumRetained)                : base(policy, maximumRetained)
    {
    }
    
    public override T Get()
    {
        if (_isDisposed)
        {
            ThrowObjectDisposedException();
        }
        
        return base.Get();
        
        void ThrowObjectDisposedException()
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }
    
    public override void Return(T obj)
    {
        // When the pool is disposed or the obj is not returned to the pool, dispose it
        if (_isDisposed || !ReturnCore(obj))
        {
            DisposeItem(obj);
        }
    }
    
    private bool ReturnCore(T obj)
    {
        bool returnedTooPool = false;
        
        if (_isDefaultPolicy || (_fastPolicy?.Return(obj) ?? _policy.Return(obj)))
        {
            if (_firstItem == null && Interlocked.CompareExchange(ref _firstItem, obj, null) == null)
            {
                returnedTooPool = true;
            }
            else
            {
                var items = _items;
                for (var i = 0; i < items.Length && !(returnedTooPool = Interlocked.CompareExchange(ref items[i].Element, obj, null) == null); i++)
                {
                }
            }
        }
        
        return returnedTooPool;
    }
    
    public void Dispose()
    {
        _isDisposed = true;
        
        DisposeItem(_firstItem);
        _firstItem = null;
        
        ObjectWrapper[] items = _items;
        for (var i = 0; i < items.Length; i++)
        {
            DisposeItem(items[i].Element);
            items[i].Element = null;
        }
    }
    
    private void DisposeItem(T? item)
    {
        if (item is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

```

##### 2.1.3 leak pool

```c#
public class LeakTrackingObjectPool<T> : ObjectPool<T> where T : class
{
    private readonly ConditionalWeakTable<T, Tracker> _trackers = new ConditionalWeakTable<T, Tracker>();
    private readonly ObjectPool<T> _inner;
        
    public LeakTrackingObjectPool(ObjectPool<T> inner)
    {
        if (inner == null)
        {
            throw new ArgumentNullException(nameof(inner));
        }
        
        _inner = inner;
    }
    
    /// <inheritdoc/>
    public override T Get()
    {
        var value = _inner.Get();
        _trackers.Add(value, new Tracker());
        return value;
    }
    
    /// <inheritdoc/>
    public override void Return(T obj)
    {
        if (_trackers.TryGetValue(obj, out var tracker))
        {
            _trackers.Remove(obj);
            
            tracker.Dispose();
        }
        
        _inner.Return(obj);
    }
    
    private class Tracker : IDisposable
    {
        private readonly string _stack;
        private bool _disposed;
        
        public Tracker()
        {
            _stack = Environment.StackTrace;
        }
        
        public void Dispose()
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
        
        ~Tracker()
        {
            if (!_disposed && !Environment.HasShutdownStarted)
            {
                Debug.Fail($"{typeof(T).Name} was leaked. Created at: {Environment.NewLine}{_stack}");
            }
        }
    }
}

```

#### 2.2 object pool provider

```c#
public abstract class ObjectPoolProvider
{
    
    public ObjectPool<T> Create<T>() where T : class, new()
    {
        return Create<T>(new DefaultPooledObjectPolicy<T>());
    }
    
    
    public abstract ObjectPool<T> Create<T>(IPooledObjectPolicy<T> policy) where T : class;
}

```

##### 2.2.1 扩展方法

```c#
public static class ObjectPoolProviderExtensions
{
    public static ObjectPool<StringBuilder> CreateStringBuilderPool(this ObjectPoolProvider provider)
    {
        return provider.Create<StringBuilder>(new StringBuilderPooledObjectPolicy());
    }
    
    
    public static ObjectPool<StringBuilder> CreateStringBuilderPool(
        this ObjectPoolProvider provider,
        int initialCapacity,
        int maximumRetainedCapacity)
    {
        var policy = new StringBuilderPooledObjectPolicy()
        {
            InitialCapacity = initialCapacity,
            MaximumRetainedCapacity = maximumRetainedCapacity,
        };
        
        return provider.Create<StringBuilder>(policy);
        
    }
}

```

##### 2.2.2 default provider

```c#
public class DefaultObjectPoolProvider : ObjectPoolProvider
{    
    public int MaximumRetained { get; set; } = Environment.ProcessorCount * 2;
        
    public override ObjectPool<T> Create<T>(IPooledObjectPolicy<T> policy)
    {
        if (policy == null)
        {
            throw new ArgumentNullException(nameof(policy));
        }        
        if (typeof(IDisposable).IsAssignableFrom(typeof(T)))
        {
            return new DisposableObjectPool<T>(policy, MaximumRetained);
        }
        
        return new DefaultObjectPool<T>(policy, MaximumRetained);
    }
}

```

##### 2.2.3 leak provider

```c#
public class LeakTrackingObjectPoolProvider : ObjectPoolProvider
{
    private readonly ObjectPoolProvider _inner;
        
    public LeakTrackingObjectPoolProvider(ObjectPoolProvider inner)
    {
        if (inner == null)
        {
            throw new ArgumentNullException(nameof(inner));
        }
        
        _inner = inner;
    }
        
    public override ObjectPool<T> Create<T>(IPooledObjectPolicy<T> policy)
    {
        var inner = _inner.Create<T>(policy);
        return new LeakTrackingObjectPool<T>(inner);
    }
}

```

