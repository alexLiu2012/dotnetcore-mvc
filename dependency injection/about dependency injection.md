## about dependency injection

相关程序集：

* microsoft.extensions.dependencyInjection.abstraction
* microsoft.extensions.dependencyInjection

----

### 1. about

#### 1.1 summary

* .net core 实现了 dependency injection

### 2. details

#### 2.1 service descriptor

* 描述服务的类型封装
* by:  service type, implementation, and lifetime

```c#
[DebuggerDisplay(
    "Lifetime = {Lifetime}, 
    "ServiceType = {ServiceType}, 
    "ImplementationType = {ImplementationType}")]
public class ServiceDescriptor
{
    public ServiceLifetime Lifetime { get; }    
    public Type ServiceType { get; }    
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type? ImplementationType { get; }
    
    public object? ImplementationInstance { get; }    
    public Func<IServiceProvider, object>? ImplementationFactory { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        string? lifetime = 
            $"{nameof(ServiceType)}: {ServiceType} 
        	 "{nameof(Lifetime)}: {Lifetime} ";
        
        if (ImplementationType != null)
        {
            return lifetime + 
                $"{nameof(ImplementationType)}: {ImplementationType}";
        }
        
        if (ImplementationFactory != null)
        {
            return lifetime + 
                $"{nameof(ImplementationFactory)}: {ImplementationFactory.Method}";
        }
        
        return lifetime + 
            $"{nameof(ImplementationInstance)}: {ImplementationInstance}";
    }
    
    internal Type GetImplementationType()
    {
        if (ImplementationType != null)
        {
            return ImplementationType;
        }
        else if (ImplementationInstance != null)
        {
            return ImplementationInstance.GetType();
        }
        else if (ImplementationFactory != null)
        {
            // factory<TService,TImplement>
            Type[]? typeArguments = ImplementationFactory.GetType().GenericTypeArguments; 
            Debug.Assert(typeArguments.Length == 2);
            // 返回 TImplement
            return typeArguments[1];
        }
        
        // 没有获取到 implementation type，
        Debug.Assert(
            false, 
            "ImplementationType, 
            "ImplementationInstance or 
            "ImplementationFactory must be non null");
        // 返回 null
        return null;
    }                                                 
}

```

##### 2.1.1 构造

###### 2.1.1.1 注入 service type + lifetime

```c#
public class ServiceDescriptor
{
    private ServiceDescriptor(Type serviceType, ServiceLifetime lifetime)
    {
        Lifetime = lifetime;
        ServiceType = serviceType;
    }
}

```

###### 2.1.1.2  by implementation type

```c#
public class ServiceDescriptor
{
    public ServiceDescriptor(
        Type serviceType,  
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors)] 
        Type implementationType,            
        ServiceLifetime lifetime)            
        	: this(serviceType, lifetime)
    {
        if (serviceType == null)
        {
            throw new ArgumentNullException(nameof(serviceType));
        }        
        if (implementationType == null)
        {
            throw new ArgumentNullException(nameof(implementationType));
        }
        
        ImplementationType = implementationType;
    }
}
         
```

###### 2.1.1.3 by implementation instance

```c#
public class ServiceDescriptor
{
    public ServiceDescriptor(
        Type serviceType,
        object instance)            
        	: this(serviceType, ServiceLifetime.Singleton)
    {
        if (serviceType == null)
        {
            throw new ArgumentNullException(nameof(serviceType));
        }
        
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }
        
        ImplementationInstance = instance;
    }
}

```

###### 2.1.1.4 by implementation factory

```c#
public class ServiceDescriptor
{
    public ServiceDescriptor(
        Type serviceType,
        Func<IServiceProvider, object> factory,
        ServiceLifetime lifetime)            
        	: this(serviceType, lifetime)
    {
        if (serviceType == null)
        {
            throw new ArgumentNullException(nameof(serviceType));
        }        
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }
        
        ImplementationFactory = factory;
    }
}

```

##### 2.1.2 describe 

###### 2.1.2.1 by implementation type

```c#
public class ServiceDescriptor
{
    // 泛型方法
    private static ServiceDescriptor Describe
        <TService, 
    	 [DynamicallyAccessedMembers(
             DynamicallyAccessedMemberTypes.PublicConstructors)] 
    	TImplementation>(ServiceLifetime lifetime)            
            where TService : class            
            where TImplementation : class, TService
    {
        return Describe(
            typeof(TService),
            typeof(TImplementation),
            lifetime: lifetime);
    }
    
    // 参数方法
    public static ServiceDescriptor Describe(
        Type serviceType,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors)] 
        Type implementationType,
        ServiceLifetime lifetime)
    {
        return new ServiceDescriptor(
            serviceType, 
            implementationType, 
            lifetime);
    }
}            
    
```

###### 2.1.2.2 by implementation factory

```c#
public class ServiceDescriptor
{
    public static ServiceDescriptor Describe(
        Type serviceType, 
        Func<IServiceProvider, object> implementationFactory, 
        ServiceLifetime lifetime)
    {
        return new ServiceDescriptor(
            serviceType, 
            implementationFactory, 
            lifetime);
    }
}

```

##### 2.1.3 singleton

###### 2.1.3.1 by implementation type

```c#
public class ServiceDescriptor
{
    // 泛型方法，反射注册的
    public static ServiceDescriptor Singleton
        <TService, 
    	 [DynamicallyAccessedMembers(
             DynamicallyAccessedMemberTypes.PublicConstructors)] 
    	 TImplementation>()            
             where TService : class            
             where TImplementation : class, TService
    {
        return Describe<TService, TImplementation>(ServiceLifetime.Singleton);
    }

    // 参数方法，反射注册的    
    public static ServiceDescriptor Singleton(
        Type service,     
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors)] 
        Type implementationType)
    {
        if (service == null)
        {
            throw new ArgumentNullException(nameof(service));
        }        
        if (implementationType == null)
        {
            throw new ArgumentNullException(nameof(implementationType));
        }
        
        return Describe(service, implementationType, ServiceLifetime.Singleton);
    }

    // 泛型方法    
    public static ServiceDescriptor Singleton<TService, TImplementation>(
        Func<IServiceProvider, TImplementation> implementationFactory)            
        	where TService : class            
            where TImplementation : class, TService
    {
        if (implementationFactory == null)
        {
            throw new ArgumentNullException(nameof(implementationFactory));
        }
        
        return Describe(typeof(TService), implementationFactory, ServiceLifetime.Singleton);
    }

        
    

        
    
```

###### 2.1.3.2 by implementation instance

```c#
public class ServiceDescriptor
{    
    // 泛型方法
    public static ServiceDescriptor Singleton<TService>(
        TService implementationInstance)            
        	where TService : class
    {
        if (implementationInstance == null)
        {
            throw new ArgumentNullException(nameof(implementationInstance));
        }
        
        return Singleton(
            typeof(TService), 
            implementationInstance);
    }
    
    // 参数方法
    public static ServiceDescriptor Singleton(
        Type serviceType,
        object implementationInstance)
    {
        if (serviceType == null)
        {
            throw new ArgumentNullException(nameof(serviceType));
        }        
        if (implementationInstance == null)
        {
            throw new ArgumentNullException(nameof(implementationInstance));
        }
        
        return new ServiceDescriptor(
            serviceType, 
            implementationInstance);
    }
}

```

###### 2.1.3.3 by implementation factory

```c#
public class ServiceDescriptor
{
    // 泛型方法
    public static ServiceDescriptor Singleton<TService>(
        Func<IServiceProvider, TService> implementationFactory)            
        	where TService : class
    {
        if (implementationFactory == null)
        {
            throw new ArgumentNullException(nameof(implementationFactory));
        }
        
        return Describe(
            typeof(TService), 
            implementationFactory, 
            ServiceLifetime.Singleton);
    }

    // 参数方法    
    public static ServiceDescriptor Singleton(
        Type serviceType,
        Func<IServiceProvider, object> implementationFactory)
    {
        if (serviceType == null)
        {
            throw new ArgumentNullException(nameof(serviceType));
        }        
        if (implementationFactory == null)
        {
            throw new ArgumentNullException(nameof(implementationFactory));
        }
        
        return Describe(
            serviceType, 
            implementationFactory, 
            ServiceLifetime.Singleton);
    }
}

```

##### 2.1.4 scoped

###### 2.1.4.1 by implementation type

```c#
public class ServiceDescriptor
{
    // 泛型方法
    public static ServiceDescriptor Scoped
        <TService, 
    	 DynamicallyAccessedMembers(
             DynamicallyAccessedMemberTypes.PublicConstructors)] 
         TImplementation>()            
             where TService : class            
             where TImplementation : class, TService
    {
        return Describe<TService, TImplementation>(ServiceLifetime.Scoped);
    }
    
    // 参数方法    
    public static ServiceDescriptor Scoped(
        Type service,       
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors)] 
        Type implementationType)
    {
        return Describe(
            service, 
            implementationType, 
            ServiceLifetime.Scoped);
    }

        
    

        
    
```

###### 2.1.4.2 by implementation factory

```c#
public class ServiceDescriptor
{    
    // 泛型方法    
    public static ServiceDescriptor Scoped<TService>(
        Func<IServiceProvider, TService> implementationFactory)            
        	where TService : class
    {
        if (implementationFactory == null)
        {
            throw new ArgumentNullException(nameof(implementationFactory));
        }
        
        return Describe(typeof(TService), implementationFactory, ServiceLifetime.Scoped);
    }
    
    // 参数方法
    public static ServiceDescriptor Scoped(
        Type service, 
        Func<IServiceProvider, object> implementationFactory)
    {
        if (service == null)
        {
            throw new ArgumentNullException(nameof(service));
        }        
        if (implementationFactory == null)
        {
            throw new ArgumentNullException(nameof(implementationFactory));
        }
        
        return Describe(service, implementationFactory, ServiceLifetime.Scoped);
    }
}
```

###### 2.1.4.3 by impl type and factory

```c#
public class ServiceDescriptor
{
    public static ServiceDescriptor Scoped<TService, TImplementation>(        
        Func<IServiceProvider, TImplementation> implementationFactory)            
        	where TService : class            
            where TImplementation : class, TService
    {
        if (implementationFactory == null)
        {
            throw new ArgumentNullException(nameof(implementationFactory));
        }
        
        return Describe(
            typeof(TService), 
            implementationFactory, 
            ServiceLifetime.Scoped);
    }
}

```

##### 2.1.5 transient

###### 2.1.5.1 by implementation type

```c#
public class ServiceDescriptor
{
    public static ServiceDescriptor Transient
        <TService, 
    	 [DynamicallyAccessedMembers(
             DynamicallyAccessedMemberTypes.PublicConstructors)] 
    	 TImplementation>()            
            where TService : class            
            where TImplementation : class, TService
    {
        return Describe<TService, TImplementation>(ServiceLifetime.Transient);
    }
           
    public static ServiceDescriptor Transient(
        Type service,         
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors)] 
        Type implementationType)
    {
        if (service == null)
        {
            throw new ArgumentNullException(nameof(service));
        }        
        if (implementationType == null)
        {
            throw new ArgumentNullException(nameof(implementationType));
        }
        
        return Describe(
            service, 
            implementationType, 
            ServiceLifetime.Transient);        
    }
}
                        
```

###### 2.1.5.2 by implementation factory

```c#
public class ServiceDescriptor
{
    // 泛型方法
    public static ServiceDescriptor Transient<TService>(
        Func<IServiceProvider, TService> implementationFactory)            
        	where TService : class
    {
        if (implementationFactory == null)
        {
            throw new ArgumentNullException(nameof(implementationFactory));
        }
        
        return Describe(
            typeof(TService), 
            implementationFactory, 
            ServiceLifetime.Transient);
    }
    
    // 参数方法
    public static ServiceDescriptor Transient(
        Type service, 
        Func<IServiceProvider, object> implementationFactory)
    {
        if (service == null)
        {
            throw new ArgumentNullException(nameof(service));
        }        
        if (implementationFactory == null)
        {
            throw new ArgumentNullException(nameof(implementationFactory));
        }
        
        return Describe(
            service, 
            implementationFactory, 
            ServiceLifetime.Transient);
    }
}

```

###### 2.1.5.3 by impl type and factory

```c#
public class ServiceDescriptor
{
    public static ServiceDescriptor Transient<TService, TImplementation>(
        Func<IServiceProvider, TImplementation> implementationFactory)        
        	where TService : class            
            where TImplementation : class, TService
    {
        if (implementationFactory == null)
        {
            throw new ArgumentNullException(nameof(implementationFactory));
        }
        
        return Describe(
            typeof(TService), 
            implementationFactory, 
            ServiceLifetime.Transient);
    }
}

```

#### 2.2 service collection

* service descriptor 容器

##### 2.2.1 接口

```c#
public interface IServiceCollection : IList<ServiceDescriptor>
{
}

```

##### 2.2.2 实现

```c#
public class ServiceCollection : IServiceCollection
{
    // 容器
    private readonly List<ServiceDescriptor> _descriptors = new List<ServiceDescriptor>();
        
    public int Count => _descriptors.Count;        
    public bool IsReadOnly => false;
    
    // 遍历器
    public ServiceDescriptor this[int index]
    {
        get
        {
            return _descriptors[index];
        }
        set
        {
            _descriptors[index] = value;
        }
    }
    
    /* get enumerator */
    
    public IEnumerator<ServiceDescriptor> GetEnumerator()
    {
        return _descriptors.GetEnumerator();
    }
    
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    
    
    /* crud */ 
    
    void ICollection<ServiceDescriptor>.Add(ServiceDescriptor item)
    {
        _descriptors.Add(item);
    }
    
    public void Insert(int index, ServiceDescriptor item)
    {
        _descriptors.Insert(index, item);
    }
    
    public bool Remove(ServiceDescriptor item)
    {
        return _descriptors.Remove(item);
    }
    
    public void RemoveAt(int index)
    {
        _descriptors.RemoveAt(index);
    }
            
    public void Clear()
    {
        _descriptors.Clear();
    }
        
    public bool Contains(ServiceDescriptor item)
    {
        return _descriptors.Contains(item);
    }
        
    public void CopyTo(ServiceDescriptor[] array, int arrayIndex)
    {
        _descriptors.CopyTo(array, arrayIndex);
    }
        
    public int IndexOf(ServiceDescriptor item)
    {
        return _descriptors.IndexOf(item);
    }       
}

```

##### 2.2.3 service descriptor 扩展方法

###### 2.2.3.1 add

```c#
public static class ServiceCollectionDescriptorExtensions
{
    
    public static IServiceCollection Add(
        this IServiceCollection collection,
        ServiceDescriptor descriptor)
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }        
        if (descriptor == null)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }
        
        collection.Add(descriptor);
        
        return collection;
    }
        
    public static IServiceCollection Add(
        this IServiceCollection collection,
        IEnumerable<ServiceDescriptor> descriptors)
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }
        
        if (descriptors == null)
        {
            throw new ArgumentNullException(nameof(descriptors));
        }
        
        foreach (ServiceDescriptor? descriptor in descriptors)
        {
            collection.Add(descriptor);
        }
        
        return collection;
    }

        
    public static void TryAdd(
        this IServiceCollection collection,
        ServiceDescriptor descriptor)
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }        
        if (descriptor == null)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }
        
        int count = collection.Count;
        for (int i = 0; i < count; i++)
        {
            if (collection[i].ServiceType == descriptor.ServiceType)
            {
                // Already added
                return;
            }
        }
        
        collection.Add(descriptor);
    }

        
    public static void TryAdd(
        this IServiceCollection collection,
        IEnumerable<ServiceDescriptor> descriptors)
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }
        
        if (descriptors == null)
        {
            throw new ArgumentNullException(nameof(descriptors));
        }
        
        foreach (ServiceDescriptor? d in descriptors)
        {
            collection.TryAdd(d);
        }
    }

        
        public static void TryAddTransient(
            this IServiceCollection collection,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type service)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            var descriptor = ServiceDescriptor.Transient(service, service);
            TryAdd(collection, descriptor);
        }

        
        public static void TryAddTransient(
            this IServiceCollection collection,
            Type service,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (implementationType == null)
            {
                throw new ArgumentNullException(nameof(implementationType));
            }

            var descriptor = ServiceDescriptor.Transient(service, implementationType);
            TryAdd(collection, descriptor);
        }

        
        public static void TryAddTransient(
            this IServiceCollection collection,
            Type service,
            Func<IServiceProvider, object> implementationFactory)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (implementationFactory == null)
            {
                throw new ArgumentNullException(nameof(implementationFactory));
            }

            var descriptor = ServiceDescriptor.Transient(service, implementationFactory);
            TryAdd(collection, descriptor);
        }

        
    
        public static void TryAddTransient<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IServiceCollection collection)
            where TService : class
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            TryAddTransient(collection, typeof(TService), typeof(TService));
        }

        
    
        public static void TryAddTransient<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(this IServiceCollection collection)
            where TService : class
            where TImplementation : class, TService
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            TryAddTransient(collection, typeof(TService), typeof(TImplementation));
        }

        
        public static void TryAddTransient<TService>(
            this IServiceCollection services,
            Func<IServiceProvider, TService> implementationFactory)
            where TService : class
        {
            services.TryAdd(ServiceDescriptor.Transient(implementationFactory));
        }

        
        public static void TryAddScoped(
            this IServiceCollection collection,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type service)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            var descriptor = ServiceDescriptor.Scoped(service, service);
            TryAdd(collection, descriptor);
        }

        
        public static void TryAddScoped(
            this IServiceCollection collection,
            Type service,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (implementationType == null)
            {
                throw new ArgumentNullException(nameof(implementationType));
            }

            var descriptor = ServiceDescriptor.Scoped(service, implementationType);
            TryAdd(collection, descriptor);
        }

        
        public static void TryAddScoped(
            this IServiceCollection collection,
            Type service,
            Func<IServiceProvider, object> implementationFactory)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (implementationFactory == null)
            {
                throw new ArgumentNullException(nameof(implementationFactory));
            }

            var descriptor = ServiceDescriptor.Scoped(service, implementationFactory);
            TryAdd(collection, descriptor);
        }

       
        public static void TryAddScoped<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IServiceCollection collection)
            where TService : class
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            TryAddScoped(collection, typeof(TService), typeof(TService));
        }

       
        public static void TryAddScoped<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(this IServiceCollection collection)
            where TService : class
            where TImplementation : class, TService
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            TryAddScoped(collection, typeof(TService), typeof(TImplementation));
        }

        
        public static void TryAddScoped<TService>(
            this IServiceCollection services,
            Func<IServiceProvider, TService> implementationFactory)
            where TService : class
        {
            services.TryAdd(ServiceDescriptor.Scoped(implementationFactory));
        }

        
        public static void TryAddSingleton(
            this IServiceCollection collection,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type service)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            var descriptor = ServiceDescriptor.Singleton(service, service);
            TryAdd(collection, descriptor);
        }

        
        public static void TryAddSingleton(
            this IServiceCollection collection,
            Type service,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (implementationType == null)
            {
                throw new ArgumentNullException(nameof(implementationType));
            }

            var descriptor = ServiceDescriptor.Singleton(service, implementationType);
            TryAdd(collection, descriptor);
        }

        
        public static void TryAddSingleton(
            this IServiceCollection collection,
            Type service,
            Func<IServiceProvider, object> implementationFactory)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (implementationFactory == null)
            {
                throw new ArgumentNullException(nameof(implementationFactory));
            }

            var descriptor = ServiceDescriptor.Singleton(service, implementationFactory);
            TryAdd(collection, descriptor);
        }

        
        public static void TryAddSingleton<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IServiceCollection collection)
            where TService : class
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            TryAddSingleton(collection, typeof(TService), typeof(TService));
        }

       
        public static void TryAddSingleton<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(this IServiceCollection collection)
            where TService : class
            where TImplementation : class, TService
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            TryAddSingleton(collection, typeof(TService), typeof(TImplementation));
        }

        
        public static void TryAddSingleton<TService>(this IServiceCollection collection, TService instance)
            where TService : class
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            var descriptor = ServiceDescriptor.Singleton(typeof(TService), instance);
            TryAdd(collection, descriptor);
        }

        
        public static void TryAddSingleton<TService>(
            this IServiceCollection services,
            Func<IServiceProvider, TService> implementationFactory)
            where TService : class
        {
            services.TryAdd(ServiceDescriptor.Singleton(implementationFactory));
        }

        
        public static void TryAddEnumerable(
            this IServiceCollection services,
            ServiceDescriptor descriptor)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            Type? implementationType = descriptor.GetImplementationType();

            if (implementationType == typeof(object) ||
                implementationType == descriptor.ServiceType)
            {
                throw new ArgumentException(
                    SR.Format(SR.TryAddIndistinguishableTypeToEnumerable,
                        implementationType,
                        descriptor.ServiceType),
                    nameof(descriptor));
            }

            int count = services.Count;
            for (int i = 0; i < count; i++)
            {
                ServiceDescriptor service = services[i];
                if (service.ServiceType == descriptor.ServiceType &&
                    service.GetImplementationType() == implementationType)
                {
                    // Already added
                    return;
                }
            }

            services.Add(descriptor);
        }

        /// <summary>
        /// Adds the specified <see cref="ServiceDescriptor"/>s if an existing descriptor with the same
        /// <see cref="ServiceDescriptor.ServiceType"/> and an implementation that does not already exist
        /// in <paramref name="services."/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="descriptors">The <see cref="ServiceDescriptor"/>s.</param>
        /// <remarks>
        /// Use <see cref="TryAddEnumerable(IServiceCollection, ServiceDescriptor)"/> when registering a service
        /// implementation of a service type that
        /// supports multiple registrations of the same service type. Using
        /// <see cref="Add(IServiceCollection, ServiceDescriptor)"/> is not idempotent and can add
        /// duplicate
        /// <see cref="ServiceDescriptor"/> instances if called twice. Using
        /// <see cref="TryAddEnumerable(IServiceCollection, ServiceDescriptor)"/> will prevent registration
        /// of multiple implementation types.
        /// </remarks>
        public static void TryAddEnumerable(
            this IServiceCollection services,
            IEnumerable<ServiceDescriptor> descriptors)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (descriptors == null)
            {
                throw new ArgumentNullException(nameof(descriptors));
            }

            foreach (ServiceDescriptor? d in descriptors)
            {
                services.TryAddEnumerable(d);
            }
        }

        /// <summary>
        /// Removes the first service in <see cref="IServiceCollection"/> with the same service type
        /// as <paramref name="descriptor"/> and adds <paramref name="descriptor"/> to the collection.
        /// </summary>
        /// <param name="collection">The <see cref="IServiceCollection"/>.</param>
        /// <param name="descriptor">The <see cref="ServiceDescriptor"/> to replace with.</param>
        /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
        public static IServiceCollection Replace(
            this IServiceCollection collection,
            ServiceDescriptor descriptor)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            // Remove existing
            int count = collection.Count;
            for (int i = 0; i < count; i++)
            {
                if (collection[i].ServiceType == descriptor.ServiceType)
                {
                    collection.RemoveAt(i);
                    break;
                }
            }

            collection.Add(descriptor);
            return collection;
        }

        /// <summary>
        /// Removes all services of type <typeparamref name="T"/> in <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="collection">The <see cref="IServiceCollection"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
        public static IServiceCollection RemoveAll<T>(this IServiceCollection collection)
        {
            return RemoveAll(collection, typeof(T));
        }

        /// <summary>
        /// Removes all services of type <paramref name="serviceType"/> in <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="collection">The <see cref="IServiceCollection"/>.</param>
        /// <param name="serviceType">The service type to remove.</param>
        /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
        public static IServiceCollection RemoveAll(this IServiceCollection collection, Type serviceType)
        {
            if (serviceType == null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            for (int i = collection.Count - 1; i >= 0; i--)
            {
                ServiceDescriptor? descriptor = collection[i];
                if (descriptor.ServiceType == serviceType)
                {
                    collection.RemoveAt(i);
                }
            }

            return collection;
        }
    }
```



##### 2.2.4 singleton, scoped, transient 扩展方法

```c#
public static class ServiceCollectionServiceExtensions
    {
        /// <summary>
        /// Adds a transient service of the type specified in <paramref name="serviceType"/> with an
        /// implementation of the type specified in <paramref name="implementationType"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <param name="serviceType">The type of the service to register.</param>
        /// <param name="implementationType">The implementation type of the service.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Transient"/>
        public static IServiceCollection AddTransient(
            this IServiceCollection services,
            Type serviceType,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (serviceType == null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            if (implementationType == null)
            {
                throw new ArgumentNullException(nameof(implementationType));
            }

            return Add(services, serviceType, implementationType, ServiceLifetime.Transient);
        }

        /// <summary>
        /// Adds a transient service of the type specified in <paramref name="serviceType"/> with a
        /// factory specified in <paramref name="implementationFactory"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <param name="serviceType">The type of the service to register.</param>
        /// <param name="implementationFactory">The factory that creates the service.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Transient"/>
        public static IServiceCollection AddTransient(
            this IServiceCollection services,
            Type serviceType,
            Func<IServiceProvider, object> implementationFactory)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (serviceType == null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            if (implementationFactory == null)
            {
                throw new ArgumentNullException(nameof(implementationFactory));
            }

            return Add(services, serviceType, implementationFactory, ServiceLifetime.Transient);
        }

        /// <summary>
        /// Adds a transient service of the type specified in <typeparamref name="TService"/> with an
        /// implementation type specified in <typeparamref name="TImplementation"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <typeparam name="TService">The type of the service to add.</typeparam>
        /// <typeparam name="TImplementation">The type of the implementation to use.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Transient"/>
        public static IServiceCollection AddTransient<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(this IServiceCollection services)
            where TService : class
            where TImplementation : class, TService
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            return services.AddTransient(typeof(TService), typeof(TImplementation));
        }

        /// <summary>
        /// Adds a transient service of the type specified in <paramref name="serviceType"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <param name="serviceType">The type of the service to register and the implementation to use.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Transient"/>
        public static IServiceCollection AddTransient(
            this IServiceCollection services,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type serviceType)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (serviceType == null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            return services.AddTransient(serviceType, serviceType);
        }

        /// <summary>
        /// Adds a transient service of the type specified in <typeparamref name="TService"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <typeparam name="TService">The type of the service to add.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Transient"/>
        public static IServiceCollection AddTransient<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IServiceCollection services)
            where TService : class
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            return services.AddTransient(typeof(TService));
        }

        /// <summary>
        /// Adds a transient service of the type specified in <typeparamref name="TService"/> with a
        /// factory specified in <paramref name="implementationFactory"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <typeparam name="TService">The type of the service to add.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <param name="implementationFactory">The factory that creates the service.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Transient"/>
        public static IServiceCollection AddTransient<TService>(
            this IServiceCollection services,
            Func<IServiceProvider, TService> implementationFactory)
            where TService : class
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (implementationFactory == null)
            {
                throw new ArgumentNullException(nameof(implementationFactory));
            }

            return services.AddTransient(typeof(TService), implementationFactory);
        }

        /// <summary>
        /// Adds a transient service of the type specified in <typeparamref name="TService"/> with an
        /// implementation type specified in <typeparamref name="TImplementation" /> using the
        /// factory specified in <paramref name="implementationFactory"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <typeparam name="TService">The type of the service to add.</typeparam>
        /// <typeparam name="TImplementation">The type of the implementation to use.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <param name="implementationFactory">The factory that creates the service.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Transient"/>
        public static IServiceCollection AddTransient<TService, TImplementation>(
            this IServiceCollection services,
            Func<IServiceProvider, TImplementation> implementationFactory)
            where TService : class
            where TImplementation : class, TService
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (implementationFactory == null)
            {
                throw new ArgumentNullException(nameof(implementationFactory));
            }

            return services.AddTransient(typeof(TService), implementationFactory);
        }

        /// <summary>
        /// Adds a scoped service of the type specified in <paramref name="serviceType"/> with an
        /// implementation of the type specified in <paramref name="implementationType"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <param name="serviceType">The type of the service to register.</param>
        /// <param name="implementationType">The implementation type of the service.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Scoped"/>
        public static IServiceCollection AddScoped(
            this IServiceCollection services,
            Type serviceType,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (serviceType == null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            if (implementationType == null)
            {
                throw new ArgumentNullException(nameof(implementationType));
            }

            return Add(services, serviceType, implementationType, ServiceLifetime.Scoped);
        }

        /// <summary>
        /// Adds a scoped service of the type specified in <paramref name="serviceType"/> with a
        /// factory specified in <paramref name="implementationFactory"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <param name="serviceType">The type of the service to register.</param>
        /// <param name="implementationFactory">The factory that creates the service.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Scoped"/>
        public static IServiceCollection AddScoped(
            this IServiceCollection services,
            Type serviceType,
            Func<IServiceProvider, object> implementationFactory)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (serviceType == null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            if (implementationFactory == null)
            {
                throw new ArgumentNullException(nameof(implementationFactory));
            }

            return Add(services, serviceType, implementationFactory, ServiceLifetime.Scoped);
        }

        /// <summary>
        /// Adds a scoped service of the type specified in <typeparamref name="TService"/> with an
        /// implementation type specified in <typeparamref name="TImplementation"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <typeparam name="TService">The type of the service to add.</typeparam>
        /// <typeparam name="TImplementation">The type of the implementation to use.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Scoped"/>
        public static IServiceCollection AddScoped<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(this IServiceCollection services)
            where TService : class
            where TImplementation : class, TService
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            return services.AddScoped(typeof(TService), typeof(TImplementation));
        }

        /// <summary>
        /// Adds a scoped service of the type specified in <paramref name="serviceType"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <param name="serviceType">The type of the service to register and the implementation to use.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Scoped"/>
        public static IServiceCollection AddScoped(
            this IServiceCollection services,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type serviceType)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (serviceType == null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            return services.AddScoped(serviceType, serviceType);
        }

        /// <summary>
        /// Adds a scoped service of the type specified in <typeparamref name="TService"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <typeparam name="TService">The type of the service to add.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Scoped"/>
        public static IServiceCollection AddScoped<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IServiceCollection services)
            where TService : class
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            return services.AddScoped(typeof(TService));
        }

        /// <summary>
        /// Adds a scoped service of the type specified in <typeparamref name="TService"/> with a
        /// factory specified in <paramref name="implementationFactory"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <typeparam name="TService">The type of the service to add.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <param name="implementationFactory">The factory that creates the service.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Scoped"/>
        public static IServiceCollection AddScoped<TService>(
            this IServiceCollection services,
            Func<IServiceProvider, TService> implementationFactory)
            where TService : class
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (implementationFactory == null)
            {
                throw new ArgumentNullException(nameof(implementationFactory));
            }

            return services.AddScoped(typeof(TService), implementationFactory);
        }

        /// <summary>
        /// Adds a scoped service of the type specified in <typeparamref name="TService"/> with an
        /// implementation type specified in <typeparamref name="TImplementation" /> using the
        /// factory specified in <paramref name="implementationFactory"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <typeparam name="TService">The type of the service to add.</typeparam>
        /// <typeparam name="TImplementation">The type of the implementation to use.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <param name="implementationFactory">The factory that creates the service.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Scoped"/>
        public static IServiceCollection AddScoped<TService, TImplementation>(
            this IServiceCollection services,
            Func<IServiceProvider, TImplementation> implementationFactory)
            where TService : class
            where TImplementation : class, TService
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (implementationFactory == null)
            {
                throw new ArgumentNullException(nameof(implementationFactory));
            }

            return services.AddScoped(typeof(TService), implementationFactory);
        }


        /// <summary>
        /// Adds a singleton service of the type specified in <paramref name="serviceType"/> with an
        /// implementation of the type specified in <paramref name="implementationType"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <param name="serviceType">The type of the service to register.</param>
        /// <param name="implementationType">The implementation type of the service.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Singleton"/>
        public static IServiceCollection AddSingleton(
            this IServiceCollection services,
            Type serviceType,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (serviceType == null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            if (implementationType == null)
            {
                throw new ArgumentNullException(nameof(implementationType));
            }

            return Add(services, serviceType, implementationType, ServiceLifetime.Singleton);
        }

        /// <summary>
        /// Adds a singleton service of the type specified in <paramref name="serviceType"/> with a
        /// factory specified in <paramref name="implementationFactory"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <param name="serviceType">The type of the service to register.</param>
        /// <param name="implementationFactory">The factory that creates the service.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Singleton"/>
        public static IServiceCollection AddSingleton(
            this IServiceCollection services,
            Type serviceType,
            Func<IServiceProvider, object> implementationFactory)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (serviceType == null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            if (implementationFactory == null)
            {
                throw new ArgumentNullException(nameof(implementationFactory));
            }

            return Add(services, serviceType, implementationFactory, ServiceLifetime.Singleton);
        }

        /// <summary>
        /// Adds a singleton service of the type specified in <typeparamref name="TService"/> with an
        /// implementation type specified in <typeparamref name="TImplementation"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <typeparam name="TService">The type of the service to add.</typeparam>
        /// <typeparam name="TImplementation">The type of the implementation to use.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Singleton"/>
        public static IServiceCollection AddSingleton<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(this IServiceCollection services)
            where TService : class
            where TImplementation : class, TService
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            return services.AddSingleton(typeof(TService), typeof(TImplementation));
        }

        /// <summary>
        /// Adds a singleton service of the type specified in <paramref name="serviceType"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <param name="serviceType">The type of the service to register and the implementation to use.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Singleton"/>
        public static IServiceCollection AddSingleton(
            this IServiceCollection services,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type serviceType)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (serviceType == null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            return services.AddSingleton(serviceType, serviceType);
        }

        /// <summary>
        /// Adds a singleton service of the type specified in <typeparamref name="TService"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <typeparam name="TService">The type of the service to add.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Singleton"/>
        public static IServiceCollection AddSingleton<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IServiceCollection services)
            where TService : class
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            return services.AddSingleton(typeof(TService));
        }

        /// <summary>
        /// Adds a singleton service of the type specified in <typeparamref name="TService"/> with a
        /// factory specified in <paramref name="implementationFactory"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <typeparam name="TService">The type of the service to add.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <param name="implementationFactory">The factory that creates the service.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Singleton"/>
        public static IServiceCollection AddSingleton<TService>(
            this IServiceCollection services,
            Func<IServiceProvider, TService> implementationFactory)
            where TService : class
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (implementationFactory == null)
            {
                throw new ArgumentNullException(nameof(implementationFactory));
            }

            return services.AddSingleton(typeof(TService), implementationFactory);
        }

        /// <summary>
        /// Adds a singleton service of the type specified in <typeparamref name="TService"/> with an
        /// implementation type specified in <typeparamref name="TImplementation" /> using the
        /// factory specified in <paramref name="implementationFactory"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <typeparam name="TService">The type of the service to add.</typeparam>
        /// <typeparam name="TImplementation">The type of the implementation to use.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <param name="implementationFactory">The factory that creates the service.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Singleton"/>
        public static IServiceCollection AddSingleton<TService, TImplementation>(
            this IServiceCollection services,
            Func<IServiceProvider, TImplementation> implementationFactory)
            where TService : class
            where TImplementation : class, TService
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (implementationFactory == null)
            {
                throw new ArgumentNullException(nameof(implementationFactory));
            }

            return services.AddSingleton(typeof(TService), implementationFactory);
        }

        /// <summary>
        /// Adds a singleton service of the type specified in <paramref name="serviceType"/> with an
        /// instance specified in <paramref name="implementationInstance"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <param name="serviceType">The type of the service to register.</param>
        /// <param name="implementationInstance">The instance of the service.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Singleton"/>
        public static IServiceCollection AddSingleton(
            this IServiceCollection services,
            Type serviceType,
            object implementationInstance)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (serviceType == null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            if (implementationInstance == null)
            {
                throw new ArgumentNullException(nameof(implementationInstance));
            }

            var serviceDescriptor = new ServiceDescriptor(serviceType, implementationInstance);
            services.Add(serviceDescriptor);
            return services;
        }

        /// <summary>
        /// Adds a singleton service of the type specified in <typeparamref name="TService" /> with an
        /// instance specified in <paramref name="implementationInstance"/> to the
        /// specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
        /// <param name="implementationInstance">The instance of the service.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <seealso cref="ServiceLifetime.Singleton"/>
        public static IServiceCollection AddSingleton<TService>(
            this IServiceCollection services,
            TService implementationInstance)
            where TService : class
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (implementationInstance == null)
            {
                throw new ArgumentNullException(nameof(implementationInstance));
            }

            return services.AddSingleton(typeof(TService), implementationInstance);
        }

        private static IServiceCollection Add(
            IServiceCollection collection,
            Type serviceType,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType,
            ServiceLifetime lifetime)
        {
            var descriptor = new ServiceDescriptor(serviceType, implementationType, lifetime);
            collection.Add(descriptor);
            return collection;
        }

        private static IServiceCollection Add(
            IServiceCollection collection,
            Type serviceType,
            Func<IServiceProvider, object> implementationFactory,
            ServiceLifetime lifetime)
        {
            var descriptor = new ServiceDescriptor(serviceType, implementationFactory, lifetime);
            collection.Add(descriptor);
            return collection;
        }
    }
```



#### 2.3 service provider

##### 2.3.1 接口

```c#
public interface IServiceProvider
{
    object? GetService(Type serviceType);
}

```

##### 2.3.2 实现

```c#
public sealed class ServiceProvider 
    : IServiceProvider, 
	  IDisposable, 
	  IServiceProviderEngineCallback, 
	  IAsyncDisposable
{
    private readonly IServiceProviderEngine _engine;    
    private readonly CallSiteValidator _callSiteValidator;
    
    internal ServiceProvider(
        IEnumerable<ServiceDescriptor> serviceDescriptors, 
        IServiceProviderEngine engine, 
        ServiceProviderOptions options)
    {
        _engine = engine;
        
        // 如果 service provider options 标记，
        // 验证 service scope
        if (options.ValidateScopes)
        {
            _engine.InitializeCallback(this);
            _callSiteValidator = new CallSiteValidator();
        }
        
        // 如果 service provider options 标记，
        // 验证 service descriptor
        if (options.ValidateOnBuild)
        {
            List<Exception> exceptions = null;
            
            foreach (ServiceDescriptor serviceDescriptor in serviceDescriptors)
            {
                try
                {
                    _engine.ValidateService(serviceDescriptor);
                }
                catch (Exception e)
                {
                    exceptions = exceptions ?? new List<Exception>();
                    exceptions.Add(e);
                }
            }
            
            if (exceptions != null)
            {
                throw new AggregateException(
                    "Some services are not able to be constructed", 
                    exceptions.ToArray());
            }
        }
    }
        
    // 实现 IServiceProvider 的 get service 方法
    public object GetService(Type serviceType) => _engine.GetService(serviceType);
      
    /* 实现 IServiceProviderFacotry 的方法 */
    void IServiceProviderEngineCallback.OnCreate(
        ServiceCallSite callSite)
    {
        _callSiteValidator.ValidateCallSite(callSite);
    }
    
    void IServiceProviderEngineCallback.OnResolve(
        Type serviceType, 
        IServiceScope scope)
    {
        _callSiteValidator.ValidateResolution(
            serviceType, 
            scope, 
            _engine.RootScope);
    }
        
    /* dispose */
    public void Dispose()
    {
        _engine.Dispose();
    }
          
    public ValueTask DisposeAsync()        
    {
        return _engine.DisposeAsync();
    }
}

```

##### 2.3.3 service provider options

```c#
public class ServiceProviderOptions
{    
    internal static readonly ServiceProviderOptions Default = new ServiceProviderOptions();    
    public bool ValidateScopes { get; set; }        
    public bool ValidateOnBuild { get; set; }
}

```

##### 2.3.4 service provider engine

* service 解析引擎，
* 递归解析

##### 2.3.5 service provider factory

###### 2.3.5.1 接口

```c#
public interface IServiceProviderFactory<TContainerBuilder> 
    where TContainerBuilder : notnull
{    
    TContainerBuilder CreateBuilder(IServiceCollection services);        
    IServiceProvider CreateServiceProvider(TContainerBuilder containerBuilder);
}

```

###### 2.3.5.2 实现

```c#
public class DefaultServiceProviderFactory 
    : IServiceProviderFactory<IServiceCollection>
{
    private readonly ServiceProviderOptions _options;
       
    public DefaultServiceProviderFactory() : this(ServiceProviderOptions.Default)
    {        
    }
        
    public DefaultServiceProviderFactory(ServiceProviderOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        _options = options;
    }
        
    public IServiceCollection CreateBuilder(IServiceCollection services)
    {
        return services;
    }
        
    public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
    {
        return containerBuilder.BuildServiceProvider(_options);
    }
}

```

###### 2.3.5.3 build service provider

```c#
public static class ServiceCollectionContainerBuilderExtensions
{
    // build sevice provider with default options
    public static ServiceProvider BuildServiceProvider(
        this IServiceCollection services)
    {
        return BuildServiceProvider(services, ServiceProviderOptions.Default);
    }
    
    // build service provider with validate scopes
    public static ServiceProvider BuildServiceProvider(
        this IServiceCollection services, 
        bool validateScopes)
    {
        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = validateScopes });
    }
          
    public static ServiceProvider BuildServiceProvider(
        this IServiceCollection services, 
        ServiceProviderOptions options)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }         
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        /* 创建 service provider engine */
        
        IServiceProviderEngine engine;
         
#if !NETCOREAPP
    	engine = new DynamicServiceProviderEngine(services);
#else
       if (RuntimeFeature.IsDynamicCodeCompiled)
       {
           engine = new DynamicServiceProviderEngine(services);
       }
        else
        {
            // Don't try to compile Expressions/IL if they are going to get interpreted
            engine = new RuntimeServiceProviderEngine(services);
        }
#endif
        // 创建 service provider 并返回
        return new ServiceProvider(services, engine, options);
     }
 }

```

##### 2.3.6 解析 service 的扩展方法

###### 2.3.6.1 get required service

```c#
public static class ServiceProviderServiceExtensions
{
    public static object GetRequiredService(
        this IServiceProvider provider, 
        Type serviceType)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }        
        if (serviceType == null)
        {
            throw new ArgumentNullException(nameof(serviceType));
        }
        
        if (provider is ISupportRequiredService requiredServiceSupportingProvider)
        {
            return requiredServiceSupportingProvider.GetRequiredService(serviceType);
        }
        
        object? service = provider.GetService(serviceType);
        
        if (service == null)
        {
            throw new InvalidOperationException(
                SR.Format(SR.NoServiceRegistered, serviceType));
        }
        
        return service;
    }
}
```

###### 2.3.6.2 get T

```c#
public static class ServiceProviderServiceExtensions
{    
    public static T? GetService<T>(
        this IServiceProvider provider)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }
        
        return (T?)provider.GetService(typeof(T));
    }
                    
    public static T GetRequiredService<T>(
        this IServiceProvider provider) 
        	where T : notnull
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }
        
        return (T)provider.GetRequiredService(typeof(T));
    }                         
}

```

###### 2.3.6.3 get services

```c#
public static class ServiceProviderServiceExtensions
{
    public static IEnumerable<T> GetServices<T>(
        this IServiceProvider provider)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }
        
        return provider.GetRequiredService<IEnumerable<T>>();
    }
           
    public static IEnumerable<object?> GetServices(
        this IServiceProvider provider, 
        Type serviceType)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }
        
        if (serviceType == null)
        {
            throw new ArgumentNullException(nameof(serviceType));
        }
        
        Type? genericEnumerable = 
            typeof(IEnumerable<>).MakeGenericType(serviceType);
        return (IEnumerable<object>)provider
            .GetRequiredService(genericEnumerable);
    }
}

```

###### 2.3.6.4 create scope

```c#
public static class ServiceProviderServiceExtensions
{
    public static IServiceScope CreateScope(
        this IServiceProvider provider)
    {
        return provider
            .GetRequiredService<IServiceScopeFactory>()
            .CreateScope();
    }
}

```

#### 2.4 service scope

##### 2.4.1 接口

```c#
public interface IServiceScope : IDisposable
{    
    IServiceProvider ServiceProvider { get; }
}

```

##### 2.4.2 实现

```c#
internal class ServiceProviderEngineScope : 	
	IServiceScope, 
	IServiceProvider, 
	IAsyncDisposable
{
    // For testing only
    internal Action<object> _captureDisposableCallback;
    
    private List<object> _disposables;    
    private bool _disposed;
    private readonly object _disposelock = new object();
        
    
    internal Dictionary<ServiceCacheKey, object> ResolvedServices { get; } = 
        new Dictionary<ServiceCacheKey, object>();
        
    public ServiceProviderEngine Engine { get; }
    public IServiceProvider ServiceProvider => this;
        
    public ServiceProviderEngineScope(ServiceProviderEngine engine)
    {
        Engine = engine;
    }
                    
    public object GetService(Type serviceType)
    {
        if (_disposed)
        {
            ThrowHelper.ThrowObjectDisposedException();
        }
        
        return Engine.GetService(serviceType, this);
    }
    
    
    
    internal object CaptureDisposable(object service)
    {
        _captureDisposableCallback?.Invoke(service);
        
        if (ReferenceEquals(this, service) || 
            !(service is IDisposable || service is IAsyncDisposable))
        {
            return service;
        }
        
        lock (_disposelock)
        {
            if (_disposed)
            {
                if (service is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                else
                {
                    // sync over async, 
                    // for the rare case that an object only implements 
                    // IAsyncDisposable and may end up starving the thread pool.
                    Task.Run(() => 
                    	((IAsyncDisposable)service)
                             .DisposeAsync()
                             .AsTask())
                        	 .GetAwaiter()
                        	 .GetResult();
                }
                
                ThrowHelper.ThrowObjectDisposedException();
            }
            
            if (_disposables == null)
            {
                _disposables = new List<object>();
            }
            
            _disposables.Add(service);
        }
        return service;
    }
    
    public void Dispose()
    {
        List<object> toDispose = BeginDispose();
        
        if (toDispose != null)
        {
            for (int i = toDispose.Count - 1; i >= 0; i--)
            {
                if (toDispose[i] is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                else
                {
                    throw new InvalidOperationException(
                        SR.Format(
                            SR.AsyncDisposableServiceDispose, 
                            TypeNameHelper
                            	.GetTypeDisplayName(toDispose[i])));
                }
            }
        }
    }
    
    public ValueTask DisposeAsync()
    {
        List<object> toDispose = BeginDispose();
        
        if (toDispose != null)
        {
            try
            {
                for (int i = toDispose.Count - 1; i >= 0; i--)
                {
                    object disposable = toDispose[i];
                    if (disposable is IAsyncDisposable asyncDisposable)
                    {
                        ValueTask vt = asyncDisposable.DisposeAsync();
                        if (!vt.IsCompletedSuccessfully)
                        {
                            return Await(i, vt, toDispose);
                        }
                        
                        // If its a IValueTaskSource backed ValueTask,
                        // inform it its result has been read so it can reset
                        vt.GetAwaiter().GetResult();
                    }
                    else
                    {
                        ((IDisposable)disposable).Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                return new ValueTask(Task.FromException(ex));
            }
        }
        
        return default;
        
        static async ValueTask Await(int i, ValueTask vt, List<object> toDispose)
        {
            await vt.ConfigureAwait(false);
            // vt is acting on the disposable at index i,
            // decrement it and move to the next iteration
            i--;
            
            for (; i >= 0; i--)
            {
                object disposable = toDispose[i];
                if (disposable is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    ((IDisposable)disposable).Dispose();
                }
            }
        }
    }
    
    private List<object> BeginDispose()
    {
        List<object> toDispose;
        lock (_disposelock)
        {
            if (_disposed)
            {
                return null;
            }
            
            _disposed = true;
            toDispose = _disposables;
            _disposables = null;
            
            // Not clearing ResolvedServices here because 
            // there might be a compilation running in background
            // trying to get a cached singleton service instance and if it won't find
            // it it will try to create a new one tripping the Debug.
            // Assert in CaptureDisposable
            // and leaking a Disposable object in Release mode
        }
        
        return toDispose;
    }
}

```

##### 2.4.3 service scope factory

###### 2.4.3.1 接口

```c#
public interface IServiceScopeFactory
    {
        
        IServiceScope CreateScope();
    }
```

###### 2.4.3.2 实现

* service provider engine 实现了 IServiceScopeFactory 接口

```c#
internal abstract class ServiceProviderEngine : 
	IServiceProviderEngine, 
	IServiceScopeFactory
{
    // ...
    
    public IServiceScope CreateScope()
    {
        if (_disposed)
        {
            ThrowHelper.ThrowObjectDisposedException();
        }
        
        return new ServiceProviderEngineScope(this);
    }
}

```

### 3. practice

#### 3.1 创建 service collection

#### 3.2 注册服务

#### 3.3 创建 service provider

