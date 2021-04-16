## about dependency injection

相关程序集：

* microsoft.extensions.dependencyInjection.abstraction
* microsoft.extensions.dependencyInjection

----

### 1. about

#### 1.1 summary

IoC 是现代程序设计框架都会采用的设计模式（其实不能称其为设计模式）。

它反转了控制关系，由程序 application（programmer 控制）反转为框架控制，即框架会自动注入模块需要的依赖项，programmer 只需要集中精力在其关注的模块（方法）即可。

在 dotnet core 之前已经存在很多优秀的 ioc (di) 框架，如 autofac、wisdow castle，dotnet core 自己也实现了 di，并且支持集成其他 ioc 框架

* .net core 实现了 dependency injection
* DI 是 .net core 以及 asp.net core 程序的基础
* 尽量使用 di 注册、解析服务（object），因为 di 可以
  * 很好的处理依赖（dependency）
  * 处理 dispose

#### 1.2 how designed

##### 1.2.1 service descriptor

* 描述托管的服务
  * 三要素：service_type, implementation_type, service_lifetime

##### 1.2.2 service collection

* 注册服务描述（service descriptor）的容器
* 由其构建 service provider

##### 1.2.3 service provider

###### 1.2.3.1. service provider

* （已注册）服务提供者

###### 1.2.3.2 service provider factory

* 构建 service provider 的工厂方法
* ms 默认使用 default service provider factory
* 可以替换为第三方 service provider factory

##### 1.2.4 service provider engine

* ms service provider 使用的、实际解析服务的引擎

##### 1.2.5 service scope

服务都有归属的 scope，框架默认使用一个 scope，可以创建不同的scope
web request 将创建新的 service scope

----

#### 1.3 how to use

##### 1.3.1 注入服务

通过`service collection`的相关扩展方法，向`service collection`注册不同的服务；注入服务时可以验证（去重）

* try add，如果 di 已经包含 service type，不再注册 service type 的 impl（不运行、不报错）；
* try add enumerable (transient)，如果 di 已经包含 service type 的某个 impl type（一般由 func 提供），不再注册 service type 的 impl（不运行、不报错）

##### 1.3.2 解析服务

由`service collection`构建`service provider`，再通过`service provider`的相关扩展方法，解析 service

* get service，解析 service，如果不能解析，-> 返回 null；
* get required service，解析 service，如果不能解析，-> 抛出异常

##### 1.3.3 创建 service scope（解析 scoped service）

`service provider.create service scope`

----

### 2. details

#### 2.1 service descriptor

* 描述服务的类型封装
* 三要素:  service_type, implementation_type, service_lifetime

```c#
[DebuggerDisplay(
    "Lifetime = {Lifetime}, 
    "ServiceType = {ServiceType}, 
    "ImplementationType = {ImplementationType}")]
public class ServiceDescriptor
{
    public ServiceLifetime Lifetime { get; }    
    public Type ServiceType { get; }    
    
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type? ImplementationType { get; }    
    public object? ImplementationInstance { get; }    
    public Func<IServiceProvider, object>? ImplementationFactory { get; }
    
    private ServiceDescriptor(Type serviceType, ServiceLifetime lifetime)
    {
        Lifetime = lifetime;
        ServiceType = serviceType;
    }
    
    // get implementation type
    internal Type GetImplementationType()
    {
        // 如果 implementation type 不为 null，
        if (ImplementationType != null)
        {
            return ImplementationType;
        }
        // 如果 implementation instance 不为 null
        else if (ImplementationInstance != null)
        {
            return ImplementationInstance.GetType();
        }
        // 如果 implementation factory (func) 不为 null
        else if (ImplementationFactory != null)
        {
            // 获取 implementationFactory 的泛型参数,
            // factory<TService,TImplementation> 的 TImplementation
            Type[]? typeArguments = ImplementationFactory
                .GetType()
                .GenericTypeArguments; 
            // 如果 TImplementation 不为空，
            // 返回 TImplementation (type)
            Debug.Assert(typeArguments.Length == 2);            
            return typeArguments[1];
        }
        
        // 没有获取到 implementation type，
        // 返回 null
        Debug.Assert(
            false, 
            "ImplementationType, 
            "ImplementationInstance or 
            "ImplementationFactory must be non null");        
        return null;
    }                                         
    
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
}

```

##### 2.1.1 构造函数

###### 2.1.1.1  by impl type

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

###### 2.1.1.2 by impl instance

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

###### 2.1.1.3 by impl factory (func)

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

##### 2.1.2 静态方法 - describe

* 创建 service descriptor

###### 2.1.2.1 by impl type

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

###### 2.1.2.2 by impl factory

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

##### 2.1.3 静态方法 - singleton

* 创建 singleton service descriptor

###### 2.1.3.1 by impl type

```c#
public class ServiceDescriptor
{
    // 泛型方法
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

    // 参数方法    
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
        
        return Describe(
            service, 
            implementationType, 
            ServiceLifetime.Singleton);
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
        
        return Describe(
            typeof(TService), 
            implementationFactory, 
            ServiceLifetime.Singleton);
    }                    
    
```

###### 2.1.3.2 by impl instance

```c#
public class ServiceDescriptor
{    
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
}

```

###### 2.1.3.3 by impl factory

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

##### 2.1.4 静态方法 - scoped

* 创建 scoped service descriptor

###### 2.1.4.1 by impl type

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
}

```

###### 2.1.4.2 by impl factory

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
        
        return Describe(
            typeof(TService), 
            implementationFactory, 
            ServiceLifetime.Scoped);
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
        
        return Describe(
            service, 
            implementationFactory, 
            ServiceLifetime.Scoped);
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

##### 2.1.5 静态方法 - transient

* 创建 transient service descriptor

###### 2.1.5.1 by impl type

```c#
public class ServiceDescriptor
{
    // 泛型方法
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
    
    // 参数方法
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

###### 2.1.5.2 by impl factory

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
       
    /* enumerator */
    
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

##### 2.2.3 扩展方法 - by descriptor

###### 2.2.3.1 add 

```c#
public static class ServiceCollectionDescriptorExtensions
{
    // add descriptor
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
    
    // add IEnumerable<descriptor>
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
    }                                                                                            }
```

###### 2.2.3.2 try add

* 如果 descriptor 的 service_type 已经注册，
* 不添加 descriptor

```c#
public static class ServiceCollectionDescriptorExtensions
{
    // try add descriptor    
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
                // 已经注册了 (service_type) 的 descriptor，退出
                // Already added
                return;
            }
        }
        
        collection.Add(descriptor);
    }

    // try add descriptors    
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
}

```

###### 2.2.3.3 try add enumerable

* 如果：
  * descriptor 中的 imple_type 是具体 object，
  * imple_type 与 service_type 相同
* 不添加 descriptor

```c#
public static class ServiceCollectionDescriptorExtensions
{
    // try add enumerable descriptor
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
    
    // try add enumerable IEnumerable<descriptor>
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
}

```

###### 2.2.3.4 remove all

```c#
public static class ServiceCollectionDescriptorExtensions
{
    public static IServiceCollection RemoveAll(
        this IServiceCollection collection, 
        Type serviceType)
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
    
    public static IServiceCollection RemoveAll<T>(this IServiceCollection collection)
    {
        return RemoveAll(collection, typeof(T));
    }
}

```

###### 2.3.3.5 replace

```c#
public static class ServiceCollectionServiceExtensions
{
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
}

```

##### 2.2.4 扩展方法 - by service type

###### 2.2.4.1 add

* 通过三要素向 service collection 中注册 service

```c#
public static class ServiceCollectionServiceExtensions
{
    // by impl type
    private static IServiceCollection Add(
        IServiceCollection collection,
        Type serviceType,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] Type implementationType,
        ServiceLifetime lifetime)
    {
        var descriptor = new ServiceDescriptor(
            serviceType, 
            implementationType, 
            lifetime);
        collection.Add(descriptor);
        return collection;
    }
    
    // by impl factory (func)
    private static IServiceCollection Add(
        IServiceCollection collection,
        Type serviceType,
        Func<IServiceProvider, object> implementationFactory,
        ServiceLifetime lifetime)
    {
        var descriptor = new ServiceDescriptor(
            serviceType, 
            implementationFactory, 
            lifetime);
        collection.Add(descriptor);
        return collection;
    }
}

```

###### 2.2.4.1 add transient

```c#
public static class ServiceCollectionServiceExtensions
{
    /* by service type*/
    
    public static IServiceCollection AddTransient(
        this IServiceCollection services,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] Type serviceType)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }        
        if (serviceType == null)
        {
            throw new ArgumentNullException(nameof(serviceType));
        }
        
        return services.AddTransient(
            serviceType, 
            serviceType);
    }
    
    public static IServiceCollection AddTransient<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] TService>(
        this IServiceCollection services)
            where TService : class
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        return services.AddTransient(typeof(TService));
    }
    
    /* by impl type */
    
    public static IServiceCollection AddTransient(
        this IServiceCollection services,
        Type serviceType,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] Type implementationType)
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
        
        return Add(
            services, 
            serviceType, 
            implementationType, 
            ServiceLifetime.Transient);    
    }
    
    public static IServiceCollection AddTransient<
        TService, 
    	[DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] TImplementation>(
        this IServiceCollection services)
            where TService : class
            where TImplementation : class, TService
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        return services.AddTransient(
            typeof(TService), 
            typeof(TImplementation));
    }
    
    /* by impl factory */
    
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
        
        return Add(
            services, 
            serviceType, 
            implementationFactory, 
            ServiceLifetime.Transient);
    }
        
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
                
        return services.AddTransient(
            typeof(TService), 
            implementationFactory);
    }
        
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

        return services.AddTransient(
            typeof(TService), 
            implementationFactory);
    }           
}

```

###### 2.2.4.2 add scoped

```c#
public static class ServiceCollectionServiceExtensions
{
    /* by service type */
    
    public static IServiceCollection AddScoped(
        this IServiceCollection services,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] Type serviceType)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }        
        if (serviceType == null)
        {
            throw new ArgumentNullException(nameof(serviceType));
        }
        
        return services.AddScoped(
            serviceType, 
            serviceType);
    }
    
    public static IServiceCollection AddScoped<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] TService>(
        this IServiceCollection services)            
        	where TService : class
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        return services.AddScoped(typeof(TService));
    }
    
    /* by impl type */
    
    public static IServiceCollection AddScoped(
        this IServiceCollection services,
        Type serviceType,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] Type implementationType)
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
        
        return Add(
            services, 
            serviceType, 
            implementationType, 
            ServiceLifetime.Scoped);
    }
    
    public static IServiceCollection AddScoped<
        TService, 
    	[DynamicallyAccessedMembers(
        	DynamicallyAccessedMemberTypes
            	.PublicConstructors)] TImplementation>(
        this IServiceCollection services)            
            where TService : class            
            where TImplementation : class, TService
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        return services.AddScoped(
            typeof(TService), 
            typeof(TImplementation));
    }
    
    /* by impl factory */
                
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
        
        return Add(
            services, 
            serviceType, 
            implementationFactory, 
            ServiceLifetime.Scoped);
    }
                                           
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
        
        return services.AddScoped(
            typeof(TService), 
            implementationFactory);
    }
        
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
        
        return services.AddScoped(
            typeof(TService), 
            implementationFactory);
    }
}

```

###### 2.2.4.3 add singleton

```c#
public static class ServiceCollectionServiceExtensions
{
    /* by service type */
    
    public static IServiceCollection AddSingleton(
        this IServiceCollection services,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] Type serviceType)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }        
        if (serviceType == null)
        {
            throw new ArgumentNullException(nameof(serviceType));
        }
        
        return services.AddSingleton(
            serviceType, 
            serviceType);
    }
        
    public static IServiceCollection AddSingleton<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] TService>(
        this IServiceCollection services)            
        	where TService : class
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        return services.AddSingleton(typeof(TService));
    }
    
    /* by impl type */
    
    public static IServiceCollection AddSingleton(
        this IServiceCollection services,
        Type serviceType,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] Type implementationType)
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
        
        return Add(
            services, 
            serviceType, 
            implementationType, 
            ServiceLifetime.Singleton);
    }
    
    public static IServiceCollection AddSingleton<
        TService, 
    	[DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] TImplementation>(
        this IServiceCollection services)            
            where TService : class            
            where TImplementation : class, TService
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        return services.AddSingleton(
            typeof(TService), 
            typeof(TImplementation));
    }
    
    /* by impl factory */
    
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
        
        return Add(
            services, 
            serviceType, 
            implementationFactory, 
            ServiceLifetime.Singleton);
    }
                                                        
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
        
        return services.AddSingleton(
            typeof(TService), 
            implementationFactory);
    }
        
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
        
        return services.AddSingleton(
            typeof(TService), 
            implementationFactory);
    }
    
    
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
        
        var serviceDescriptor = new ServiceDescriptor(
            serviceType, 
            implementationInstance);
        services.Add(serviceDescriptor);
        
        return services;
    }
    
    /* by impl instance */
    
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
        
        return services.AddSingleton(
            typeof(TService), 
            implementationInstance);
    }
}

```

###### 2.2.4.4 try add transient

```c#
public static class ServiceCollectionDescriptorExtensions
{
    /* by service_type */
    
     public static void TryAddTransient(
        this IServiceCollection collection,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] Type service)
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }        
        if (service == null)
        {
            throw new ArgumentNullException(nameof(service));
        }
        
        var descriptor = ServiceDescriptor.Transient(
            service, 
            service);         
        TryAdd(collection, descriptor);
    }
    
    public static void TryAddTransient
        <[DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] TService>(
        this IServiceCollection collection)        
        	where TService : class
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }
        
        TryAddTransient(
            collection, 
            typeof(TService), 
            typeof(TService));
    }        
                
    /* by service_type & impl_type */
    
    public static void TryAddTransient(
        this IServiceCollection collection,
        Type service,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] Type implementationType)
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
        
        var descriptor = ServiceDescriptor.Transient(
            service, 
            implementationType);        
        TryAdd(collection, descriptor);
    }               
    
    public static void TryAddTransient
        <TService, 
    	 [DynamicallyAccessedMembers(
             DynamicallyAccessedMemberTypes
             	.PublicConstructors)] TImplementation>(
        this IServiceCollection collection)            
        	where TService : class            
            where TImplementation : class, TService
	{
    	if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }
             
        TryAddTransient(
            collection, 
            typeof(TService), 
            typeof(TImplementation));
    } 
    
    /* by impl factory */
    
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
        
        var descriptor = ServiceDescriptor.Transient(
            service, 
            implementationFactory);
        TryAdd(collection, descriptor);
    }
        
    public static void TryAddTransient<TService>(
        this IServiceCollection services,
        Func<IServiceProvider, TService> implementationFactory)
        	where TService : class
    {
        services.TryAdd(
            ServiceDescriptor.Transient(implementationFactory));
    }
}

```

###### 2.2.4.5 try add scoped

```c#
public static class ServiceCollectionDescriptorExtensions
{
    /* by service type */
    
    public static void TryAddScoped(
        this IServiceCollection collection,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] Type service)
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }        
        if (service == null)
        {
            throw new ArgumentNullException(nameof(service));
        }
        
        var descriptor = ServiceDescriptor.Scoped(
            service, 
            service);        
        TryAdd(collection, descriptor);
    }

    public static void TryAddScoped<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] TService>(
        this IServiceCollection collection)            
        	where TService : class
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }
        
        TryAddScoped(
            collection, 
            typeof(TService), 
            typeof(TService));
    }
    
    /* by implementation type */
    
    public static void TryAddScoped(
        this IServiceCollection collection,
        Type service,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] Type implementationType)
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
        
        var descriptor = ServiceDescriptor.Scoped(
            service, 
            implementationType);
        TryAdd(collection, descriptor);
    }
    
    public static void TryAddScoped<
        TService, 
    	DynamicallyAccessedMembers(
        	DynamicallyAccessedMemberTypes
            	.PublicConstructors)] TImplementation>(
        this IServiceCollection collection)
            where TService : class
            where TImplementation : class, TService
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }
                
        TryAddScoped(
            collection, 
            typeof(TService), 
            typeof(TImplementation));
    }

    /* by impl type and impl factory */
    
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
        
        var descriptor = ServiceDescriptor.Scoped(
            service, 
            implementationFactory);
        TryAdd(collection, descriptor);
    }
                            
    public static void TryAddScoped<TService>(
        this IServiceCollection services,
        Func<IServiceProvider, TService> implementationFactory)
        	where TService : class
    {
        services.TryAdd(
            ServiceDescriptor.Scoped(implementationFactory));
    }
}

```

###### 2.2.3.6 try add singleton 

```c#
public static class ServiceCollectionDescriptorExtensions
{
    /* by service type */
    
    public static void TryAddSingleton(
        this IServiceCollection collection,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] Type service)
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }        
        if (service == null)
        {
            throw new ArgumentNullException(nameof(service));
        }
        
        var descriptor = ServiceDescriptor.Singleton(
            service, 
            service);
        TryAdd(collection, descriptor);
    }
    
    public static void TryAddSingleton<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] TService>(
        this IServiceCollection collection)
            where TService : class
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }
                
        TryAddSingleton(
            collection, 
            typeof(TService), 
            typeof(TService));
    }
    
    /* by impl type */
    
    public static void TryAddSingleton(
        this IServiceCollection collection,
        Type service,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] Type implementationType)
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
        
        var descriptor = ServiceDescriptor.Singleton(
            service, 
            implementationType);
        TryAdd(collection, descriptor);
    }
    
    public static void TryAddSingleton<
        TService, 
    	[DynamicallyAccessedMembers(
        	DynamicallyAccessedMemberTypes
            	.PublicConstructors)] TImplementation>(
        this IServiceCollection collection)
            where TService : class
            where TImplementation : class, TService
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }
                
        TryAddSingleton(
            collection, 
            typeof(TService), 
            typeof(TImplementation));
    }
    
    /* by impl type factory */
    
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
        
        var descriptor = ServiceDescriptor.Singleton(
            service, 
            implementationFactory);
        TryAdd(collection, descriptor);
    }
    
    public static void TryAddSingleton<TService>(
        this IServiceCollection services,
        Func<IServiceProvider, TService> implementationFactory)
        	where TService : class
    {
        services.TryAdd(
            ServiceDescriptor.Singleton(implementationFactory));
    }
    
    /* by impl instance */
        
    public static void TryAddSingleton<TService>(
        this IServiceCollection collection, 
        TService instance)
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
                
        var descriptor = ServiceDescriptor.Singleton(
            typeof(TService), 
            instance);
        TryAdd(collection, descriptor);
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

##### 2.3.2 service provider

* ms（默认）实现

```c#
public sealed class ServiceProvider : 
	IServiceProvider, 
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
        
    /* 实现 IServiceProvider 的 get service 方法 */
          
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

###### 2.3.2.1 service provider options

* 单例

```c#
public class ServiceProviderOptions
{    
    internal static readonly ServiceProviderOptions Default = new ServiceProviderOptions();    
    public bool ValidateScopes { get; set; }        
    public bool ValidateOnBuild { get; set; }
}

```

###### 2.3.2.2 service provider engine

* service 解析引擎，
* 递归解析

##### 2.3.3 扩展方法

###### 2.3.3.1 get required service

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
        
        // 如果实现了 ISupportRequiredService 接口，
        // 直接调用 ISupportRequiredService 对应方法
        if (provider is ISupportRequiredService 
            requiredServiceSupportingProvider)
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

###### 2.3.3.2 support required service 接口

```c#
public interface ISupportRequiredService
{    
    object GetRequiredService(Type serviceType);
}

```

###### 2.3.3.3 get T service

```c#
public static class ServiceProviderServiceExtensions
{    
    public static T? GetService<T>(this IServiceProvider provider)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }
        
        return (T?)provider.GetService(typeof(T));
    }
                    
    public static T GetRequiredService<T>(this IServiceProvider provider) 
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

###### 2.3.3.4 get enumerable services

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

###### 2.3.3.5 create scope

```c#
public static class ServiceProviderServiceExtensions
{
    public static IServiceScope CreateScope(
        this IServiceProvider provider)
    {
        return provider.GetRequiredService<IServiceScopeFactory>()
            		   .CreateScope();
    }
}

```



##### 2.3.4 service provider factory

###### 2.3.4.1 接口

```c#
public interface IServiceProviderFactory<TContainerBuilder> 
    where TContainerBuilder : notnull
{    
    TContainerBuilder CreateBuilder(IServiceCollection services);        
    IServiceProvider CreateServiceProvider(TContainerBuilder containerBuilder);
}

```

###### 2.3.4.2 default service provider factory

* 使用`IServiceCollection`作为`TContainerBuilder`
* 调用 service collection 的扩展方法
  * 创建 service provider engine
  * 用 service provider engine 构建 service provider

```c#
public class DefaultServiceProviderFactory : IServiceProviderFactory<IServiceCollection>
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

###### 2.3.4.3 扩展方法 - build service provider

* 真正构建 service provider 的方法

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
        // 创建 ms service provider 并返回
        return new ServiceProvider(services, engine, options);
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

##### 2.4.2 service provider engine scope

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

###### 2.4.3.2 service provider engine

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

#### 2.5 service provider engine

##### 2.5.1 接口

```c#
internal interface IServiceProviderEngine : 
	IServiceProvider, 
	IDisposable, 
	IAsyncDisposable
{
    IServiceScope RootScope { get; }
    void InitializeCallback(IServiceProviderEngineCallback callback);
    void ValidateService(ServiceDescriptor descriptor);
}

```

##### 2.5.2 service provider engine

```c#
internal abstract class ServiceProviderEngine : 
	IServiceProviderEngine, 
	IServiceScopeFactory
{
    private bool _disposed;
        
    private IServiceProviderEngineCallback _callback;   
        
    private readonly Func<Type, Func<ServiceProviderEngineScope, object>> 
        _createServiceAccessor;               
    internal ConcurrentDictionary<Type, Func<ServiceProviderEngineScope, object>> 
        RealizedServices { get; }
    
    internal CallSiteFactory CallSiteFactory { get; }    
    protected CallSiteRuntimeResolver RuntimeResolver { get; }
    
    public ServiceProviderEngineScope Root { get; }    
    public IServiceScope RootScope => Root;
    
    protected ServiceProviderEngine(
        IEnumerable<ServiceDescriptor> serviceDescriptors)
    {
        // 创建 service accessor
        _createServiceAccessor = CreateServiceAccessor;
        // 创建 engine scope
        Root = new ServiceProviderEngineScope(this);
        // 创建 callsite runtime resolver
        RuntimeResolver = new CallSiteRuntimeResolver();
        
        /* 创建 callsite factory，
           注册 serviceprovider，
           注册 service scope factory */
        CallSiteFactory = new CallSiteFactory(serviceDescriptors);
        CallSiteFactory.Add(
            typeof(IServiceProvider), 
            new ServiceProviderCallSite());
        CallSiteFactory.Add(
            typeof(IServiceScopeFactory), 
            new ServiceScopeFactoryCallSite());
        
        // 创建 realized services
        RealizedServices = new ConcurrentDictionary<
            Type, 
        	Func<ServiceProviderEngineScope, object>>();
    }
        
    // 创建 service accessor，
    // 在 service provider callback 中注册 service_type 的 callsite
    private Func<ServiceProviderEngineScope, object> CreateServiceAccessor(Type serviceType)
    {
        ServiceCallSite callSite = CallSiteFactory.GetCallSite(
            serviceType, 
            new CallSiteChain());
        
        if (callSite != null)
        {
            DependencyInjectionEventSource.Log
                						  .CallSiteBuilt(serviceType, callSite);
            
            _callback?.OnCreate(callSite);
            return RealizeService(callSite);
        }
        
        return _ => null;
    }    
        
    // 从 scope 解析 service object 的方法，
    // 由派生类实现
    protected abstract Func<ServiceProviderEngineScope, object> RealizeService(
        ServiceCallSite callSite);
                                      
    void IServiceProviderEngine.InitializeCallback(
        IServiceProviderEngineCallback callback)
    {
        _callback = callback;
    }   
        
    public void Dispose()
    {
        _disposed = true;
        Root.Dispose();
    }
    
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return Root.DisposeAsync();
    }    
}

```

###### 2.5.2.1 validate

```c#
internal abstract class ServiceProviderEngine 
{
    public void ValidateService(ServiceDescriptor descriptor)
    {
        if (descriptor.ServiceType.IsGenericType && 
            !descriptor.ServiceType.IsConstructedGenericType)
        {
            return;
        }
        
        try
        {
            ServiceCallSite callSite = CallSiteFactory.GetCallSite(
                descriptor, 
                new CallSiteChain());
            
            if (callSite != null)
            {
                _callback?.OnCreate(callSite);
            }
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                $"Error while validating the service descriptor 
                '{descriptor}': {e.Message}", 
                e);
        }
    }
}

```

###### 2.5.2.2 get service

```c#
internal abstract class ServiceProviderEngine 
{
    public object GetService(Type serviceType) => GetService(serviceType, Root);
        
    internal object GetService(
        Type serviceType, 
        ServiceProviderEngineScope serviceProviderEngineScope)
    {
        if (_disposed)
        {
            ThrowHelper.ThrowObjectDisposedException();
        }
        
        Func<ServiceProviderEngineScope, object> realizedService = 
            RealizedServices.GetOrAdd(
            	serviceType, 
            	_createServiceAccessor);
        
        _callback?.OnResolve(
            serviceType, 
            serviceProviderEngineScope);
        
        DependencyInjectionEventSource.Log.ServiceResolved(serviceType);
        
        return realizedService.Invoke(serviceProviderEngineScope);
    }    
}

```

###### 2.5.2.3 create scope

```c#
internal abstract class ServiceProviderEngine 
{
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

##### 2.5.3 runtime service provider engine

```c#
internal class RuntimeServiceProviderEngine : ServiceProviderEngine
{
    public RuntimeServiceProviderEngine(
        IEnumerable<ServiceDescriptor> serviceDescriptors) : 
    		base(serviceDescriptors)
    {
    }

    protected override Func<ServiceProviderEngineScope, object> 
        RealizeService(ServiceCallSite callSite)
    {
        return scope =>
        {
            Func<ServiceProviderEngineScope, object> realizedService = p => 
                RuntimeResolver.Resolve(callSite, p);
            
            RealizedServices[callSite.ServiceType] = realizedService;

            return realizedService(scope);
        };
    }
}

```

##### 2.5.4 compiled service provider engine

```c#
internal abstract class CompiledServiceProviderEngine : ServiceProviderEngine
{
#if IL_EMIT
    public ILEmitResolverBuilder ResolverBuilder { get; }
#else
    public ExpressionResolverBuilder ResolverBuilder { get; }
#endif
    
    public CompiledServiceProviderEngine(
    	IEnumerable<ServiceDescriptor> serviceDescriptors) : 
    		base(serviceDescriptors)
    {
#if IL_EMIT
        ResolverBuilder = new ILEmitResolverBuilder(RuntimeResolver, this, Root);
#else
        ResolverBuilder = new ExpressionResolverBuilder(RuntimeResolver, this, Root);
#endif
    }

    protected override Func<ServiceProviderEngineScope, object> 
        RealizeService(ServiceCallSite callSite)
    {
        Func<ServiceProviderEngineScope, object> realizedService = 
            ResolverBuilder.Build(callSite);
        
        RealizedServices[callSite.ServiceType] = realizedService;
        
        return realizedService;
    }
}

```

##### 2.5.5 dynamic service provider engine

```c#
internal class DynamicServiceProviderEngine : CompiledServiceProviderEngine
{
    public DynamicServiceProviderEngine(
        IEnumerable<ServiceDescriptor> serviceDescriptors) : 
    		base(serviceDescriptors)
    {
    }
    
    protected override Func<ServiceProviderEngineScope, object> 
        RealizeService(ServiceCallSite callSite)
    {
        int callCount = 0;
        return scope =>
        {
            // Resolve the result before we increment the call count, 
            // this ensures that singletons won't cause any 
            // side effects during the compilation of the resolve function.
            var result = RuntimeResolver.Resolve(callSite, scope);
            
            if (Interlocked.Increment(ref callCount) == 2)
            {
                // Don't capture the ExecutionContext when 
                // forking to build the compiled version of the resolve function
                ThreadPool.UnsafeQueueUserWorkItem(
                    state =>
                    {
                        try
                        {
                            base.RealizeService(callSite);
                        }
                        catch
                        {
                            // Swallow the exception, 
                            // we should log this via the event source in a non-patched release
                        }
                    },
                    null);
            }
            
            return result;
        };
    }
}

```

#### 2.6 activator utilities

##### 2.6.1 activator utilities

```c#
public static class ActivatorUtilities
{
    private static readonly MethodInfo GetServiceInfo =
        GetMethodInfo<
        	Func<IServiceProvider, Type, Type, bool, object?>>((sp, t, r, c) => 
	        	GetService(sp, t, r, c));	
        
    private static object? GetService(
        IServiceProvider sp, 
        Type type, 
        Type requiredBy, 
        bool isDefaultParameterRequired)
    {
        object? service = sp.GetService(type);
        
        if (service == null && !isDefaultParameterRequired)
        {
            string? message = 
                $"Unable to resolve service for type '{type}' 
                "while attempting to activate '{requiredBy}'.";
            throw new InvalidOperationException(message);
        }
        
        return service;
    }
                                                                        
    
    private static MethodInfo GetMethodInfo<T>(Expression<T> expr)
    {
        var mc = (MethodCallExpression)expr.Body;
        return mc.Method;
    }
                                        
    private static void ThrowMultipleCtorsMarkedWithAttributeException()
    {
        throw new InvalidOperationException(
            $"Multiple constructors were marked with 
            "{nameof(ActivatorUtilitiesConstructorAttribute)}.");
    }
    
    private static void ThrowMarkedCtorDoesNotTakeAllProvidedArguments()
    {
        throw new InvalidOperationException(
            $"Constructor marked with 
            "{nameof(ActivatorUtilitiesConstructorAttribute)} 
            'does not accept all given argument types.");
    }
}

```

##### 2.6.1 create instance

###### 2.6.1.1 constructor matcher

```c#
public static class ActivatorUtilities
{
    private struct ConstructorMatcher
    {
        private readonly ConstructorInfo _constructor;
        private readonly ParameterInfo[] _parameters;
        private readonly object?[] _parameterValues;
        
        // 注入 constructor_info，
        // 从中解析 parameter_type 和 parameter_value
        public ConstructorMatcher(ConstructorInfo constructor)
        {
            _constructor = constructor;
            _parameters = _constructor.GetParameters();
            _parameterValues = new object?[_parameters.Length];
        }
        
        public int Match(object[] givenParameters)
        {
            int applyIndexStart = 0;
            int applyExactLength = 0;
            for (int givenIndex = 0; 
                 givenIndex != givenParameters.Length; 
                 givenIndex++)
            {
                Type? givenType = givenParameters[givenIndex]?.GetType();
                bool givenMatched = false;
                
                for (int applyIndex = applyIndexStart; 
                     givenMatched == false && applyIndex != _parameters.Length; 
                     ++applyIndex)
                {
                    if (_parameterValues[applyIndex] == null &&
                        _parameters[applyIndex]
                        	.ParameterType.IsAssignableFrom(givenType))
                    {
                        givenMatched = true;
                        _parameterValues[applyIndex] = givenParameters[givenIndex];
                        if (applyIndexStart == applyIndex)
                        {
                            applyIndexStart++;
                            if (applyIndex == givenIndex)
                            {
                                applyExactLength = applyIndex;
                            }
                        }
                    }
                }
                
                if (givenMatched == false)
                {
                    return -1;
                }
            }
            
            return applyExactLength;
        }
        
        public object CreateInstance(IServiceProvider provider)
        {
            for (int index = 0; index != _parameters.Length; index++)
            {
                if (_parameterValues[index] == null)
                {
                    object? value = provider.GetService(
                        _parameters[index].ParameterType);
                    if (value == null)
                    {
                        if (!ParameterDefaultValue.TryGetDefaultValue(
                            	_parameters[index], 
                            	out object? defaultValue))
                        {
                            throw new InvalidOperationException(
                                $"Unable to resolve service for type
                                '{_parameters[index].ParameterType}' 
                                'while attempting to activate 
                                '{_constructor.DeclaringType}'.");
                        }
                        else
                        {
                            _parameterValues[index] = defaultValue;
                        }
                    }
                    else
                    {
                        _parameterValues[index] = value;
                    }
                }
            }
            
#if NETCOREAPP
    		return _constructor.Invoke(
                BindingFlags.DoNotWrapExceptions, 
                binder: null, 
                parameters: _parameterValues, 
                culture: null);
#else
            try
            {
                return _constructor.Invoke(_parameterValues);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo
                    .Capture(ex.InnerException)
                    .Throw();
                // The above line will always throw, 
                // but the compiler requires we throw explicitly.
                throw;
            }
#endif
        }
    }
}

```

###### 2.6.1.2 create object

```c#
public static class ActivatorUtilities
{
    public static object CreateInstance(
        IServiceProvider provider,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] Type instanceType,
        params object[] parameters)
    {
        int bestLength = -1;
        bool seenPreferred = false;
        
        ConstructorMatcher bestMatcher = default;
        
        if (!instanceType.IsAbstract)
        {
            foreach (ConstructorInfo? constructor in 
                     instanceType.GetConstructors())
            {
                var matcher = new ConstructorMatcher(constructor);
                bool isPreferred = constructor.IsDefined(
                    typeof(ActivatorUtilitiesConstructorAttribute), 
                    false);
                int length = matcher.Match(parameters);
                
                if (isPreferred)
                {
                    if (seenPreferred)
                    {
                        ThrowMultipleCtorsMarkedWithAttributeException();
                    }
                    
                    if (length == -1)
                    {
                        ThrowMarkedCtorDoesNotTakeAllProvidedArguments();
                    }
                }
                
                if (isPreferred || bestLength < length)
                {
                    bestLength = length;
                    bestMatcher = matcher;
                }
                
                seenPreferred |= isPreferred;
            }
        }
        
        if (bestLength == -1)
        {
            string? message = 
                $"A suitable constructor for type '{instanceType}' 
                'could not be located. Ensure the type is concrete 
                'and all parameters of a public constructor 
                'are either registered as services or passed as arguments. 
                'Also ensure no extraneous arguments are provided.";
            throw new InvalidOperationException(message);
        }
        
        return bestMatcher.CreateInstance(provider);
    }
}

```

###### 2.6.1.3 create T

```c#
public static class ActivatorUtilities
{
    public static T CreateInstance<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] T>(
        IServiceProvider provider, 
        params object[] parameters)
    {
        return (T)CreateInstance(provider, typeof(T), parameters);
    }
}
```

###### 2.6.1.4 get or create

```c#
public static class ActivatorUtilities
{
    public static object GetServiceOrCreateInstance(
        IServiceProvider provider,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] Type type)
    {
        return provider.GetService(type) ?? CreateInstance(provider, type);
    }
    
    public static T GetServiceOrCreateInstance<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] T>(IServiceProvider provider)
    {
        return (T)GetServiceOrCreateInstance(provider, typeof(T));
    }                
}

```

##### 2.6.2 create object factory

###### 2.6.2.1 object factory

```c#
public delegate object ObjectFactory(
     IServiceProvider serviceProvider, 
     object?[]? arguments);
     
```

###### 2.6.2.2 create factory

```c#
public static class ActivatorUtilities
{
    public static ObjectFactory CreateFactory(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] Type instanceType,
        Type[] argumentTypes)
    {
        /* 获取 constructor */
        
        FindApplicableConstructor(
            instanceType, 
            argumentTypes, 
            out ConstructorInfo? constructor, 
            out int?[]? parameterMap);
        
        /* 构建 expression */
        
        ParameterExpression? provider = Expression.Parameter(
            typeof(IServiceProvider), 
            "provider");
        
        ParameterExpression? argumentArray = Expression.Parameter(
            typeof(object[]), 
            "argumentArray");
        
        Expression? factoryExpressionBody = BuildFactoryExpression(
            constructor, 
            parameterMap, 
            provider, 
            argumentArray);
        
        var factoryLambda = Expression.Lambda<Func<IServiceProvider, object[], object>>(
            	factoryExpressionBody, 
            	provider, 
            	argumentArray);
        
        Func<IServiceProvider, object[], object>? result = factoryLambda.Compile();
        
        return result.Invoke;
    }
}

```

###### 2.6.2.3 find constructor

```c#
public static class ActivatorUtilities
{
    private static void FindApplicableConstructor(
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes
                	.PublicConstructors)] Type instanceType,
            Type[] argumentTypes,
            out ConstructorInfo matchingConstructor,
            out int?[] matchingParameterMap)
    {
        ConstructorInfo? constructorInfo = null;
        int?[]? parameterMap = null;
        
        if (!TryFindPreferredConstructor(
            	instanceType, 
            	argumentTypes, 
            	ref constructorInfo, 
            	ref parameterMap) &&
            !TryFindMatchingConstructor(
                instanceType, 
                argumentTypes, 
                ref constructorInfo, 
                ref parameterMap))
        {
            string? message = 
                $"A suitable constructor for type '{instanceType}' 
                'could not be located. Ensure the type is concrete 
                'and all parameters of a public constructor are 
                'either registered as services or passed as arguments. 
                'Also ensure no extraneous arguments are provided.";
            throw new InvalidOperationException(message);
        }
        
        matchingConstructor = constructorInfo;
        matchingParameterMap = parameterMap;
    }
}

```

###### 2.6.2.4 find preferred constructor

```c#
public static class ActivatorUtilities
{
    private static bool TryFindPreferredConstructor(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] Type instanceType,
        Type[] argumentTypes,
        [NotNullWhen(true)] ref ConstructorInfo? matchingConstructor,
        [NotNullWhen(true)] ref int?[]? parameterMap)
    {
        bool seenPreferred = false;
        foreach (ConstructorInfo? constructor in 
                 instanceType.GetConstructors())
        {
            if (constructor.IsDefined(
                typeof(ActivatorUtilitiesConstructorAttribute), 
                false))
            {
                if (seenPreferred)
                {
                    ThrowMultipleCtorsMarkedWithAttributeException();
                }
                
                if (!TryCreateParameterMap(
                    	constructor.GetParameters(), 
                    	argumentTypes, 
                    	out int?[] tempParameterMap))
                {
                    ThrowMarkedCtorDoesNotTakeAllProvidedArguments();
                }
                
                matchingConstructor = constructor;
                parameterMap = tempParameterMap;
                seenPreferred = true;
            }
        }
        
        if (matchingConstructor != null)
        {
            Debug.Assert(parameterMap != null);
            return true;
        }
        
        return false;
    }
}

```

###### 2.6.2.4 find matching constructor

```c#
public static class ActivatorUtilities
{
    private static bool TryFindMatchingConstructor(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes
            	.PublicConstructors)] Type instanceType,
        Type[] argumentTypes,
        [NotNullWhen(true)] ref ConstructorInfo? matchingConstructor,
        [NotNullWhen(true)] ref int?[]? parameterMap)
    {
        foreach (ConstructorInfo? constructor in 
                 instanceType.GetConstructors())
        {
            if (TryCreateParameterMap(
                	constructor.GetParameters(), 
                	argumentTypes, 
                	out int?[] tempParameterMap))
            {
                if (matchingConstructor != null)
                {
                    throw new InvalidOperationException(
                        $"Multiple constructors accepting all given 
                        "argument types have been found in type '{instanceType}'. 
                        'There should only be one applicable constructor.");
                }
                
                matchingConstructor = constructor;
                parameterMap = tempParameterMap;
            }
        }
        
        if (matchingConstructor != null)
        {
            Debug.Assert(parameterMap != null);
            return true;
        }
        
        return false;
    }
}
                        
```

###### 2.6.2.5 create parameter map

```c#
public static class ActivatorUtilities
{
    private static bool TryCreateParameterMap(
        ParameterInfo[] constructorParameters, 
        Type[] argumentTypes, 
        out int?[] parameterMap)
    {
        parameterMap = new int?[constructorParameters.Length];
        
        for (int i = 0; i < argumentTypes.Length; i++)
        {
            bool foundMatch = false;
            Type? givenParameter = argumentTypes[i];
            
            for (int j = 0; j < constructorParameters.Length; j++)
            {
                if (parameterMap[j] != null)
                {
                    // This ctor parameter has already been matched
                    continue;
                }
                
                if (constructorParameters[j]
                    	.ParameterType
                    	.IsAssignableFrom(givenParameter))
                {
                    foundMatch = true;
                    parameterMap[j] = i;
                    break;
                }
            }
            
            if (!foundMatch)
            {
                return false;
            }
        }
        
        return true;
    }
}

```

###### 2.6.2.6 build factory expression

```c#
public static class ActivatorUtilities
{
    private static Expression BuildFactoryExpression(
        ConstructorInfo constructor,
        int?[] parameterMap,
        Expression serviceProvider,
        Expression factoryArgumentArray)
    {
        ParameterInfo[]? constructorParameters = constructor.GetParameters();
        var constructorArguments = new Expression[constructorParameters.Length];
        
        for (int i = 0; i < constructorParameters.Length; i++)
        {
            ParameterInfo? constructorParameter = constructorParameters[i];
            Type? parameterType = constructorParameter.ParameterType;
            bool hasDefaultValue = ParameterDefaultValue.TryGetDefaultValue(
                constructorParameter, 
                out object? defaultValue);
            
            if (parameterMap[i] != null)
            {
                constructorArguments[i] = Expression.ArrayAccess(
                    factoryArgumentArray, 
                    Expression.Constant(parameterMap[i]));
            }
            else
            {
                var parameterTypeExpression = new Expression[] 
                {
                    serviceProvider,
                    Expression.Constant(parameterType, typeof(Type)),
                    Expression.Constant(constructor.DeclaringType, typeof(Type)),
                    Expression.Constant(hasDefaultValue) 
                };
                
                constructorArguments[i] = Expression.Call(
                    GetServiceInfo, 
                    parameterTypeExpression);
            }
            
            // Support optional constructor arguments by passing in the default value
            // when the argument would otherwise be null.
            if (hasDefaultValue)
            {
                ConstantExpression? defaultValueExpression = 
                    Expression.Constant(defaultValue);
                constructorArguments[i] = Expression.Coalesce(
                    constructorArguments[i], 
                    defaultValueExpression);
            }
            
            constructorArguments[i] = Expression.Convert(
                constructorArguments[i], 
                parameterType);
        }
        
        return Expression.New(constructor, constructorArguments);
    }
}

```

##### 2.6.3 attribute

```c#
[AttributeUsage(AttributeTargets.All)]
public class ActivatorUtilitiesConstructorAttribute : Attribute
{
}

```

### 3. practice

#### 3.1 创建 service 容器（provider）

##### 3.1.1 手动创建

```c#
var serviceCollection = new ServiceCollection();
serviceCollection.Add(/* */);
var serviceProvider = serviceCollection.BuildServiceProvider();

```

##### 3.1.2 by host framework

```c#
var hostBuilder = Host.CreateDefaultBuilder(args)
    				  .ConfigureServices((_, services) =>
                                         	services.Add(/**/));

var host = hostBuilder.Build();
    
```

* 框架自动注入服务（create default builder 方法中注册）
  * 见 Host.CrearteDefaultBuilder()

##### 3.1.3 第三方 service provider

* 实现 IServiceProviderFactory 方法

#### 3.2 注册服务

##### 3.2.1 Addxxx

* 可以多次执行，
* 不同的 impl type 保存在 enumerable impl type 集合中

##### 3.2.2 TryAddXxx

* 如果 service type 已经存在，不再注册 service (descriptor with impl type)

##### 3.2.3 TryAddEnumerable

* 如果 imple type 已经存在，不再注册 service descriptor

#### 3.3 解析服务

##### 3.3.1 通过 IServiceProvider 解析

* 解析 object
* 解析 T
* 解析 IEnumerate<T>

##### 3.3.2 通过 activator utilities 创建

* 参数必须能分配 default value
* 必须是 public constructor，
* 且标记 activator utilities 特性，只能标记一个

##### 3.3.3 service scope 验证

* root service provider 由 `build service provider` 方法创建
* scope 由 root service provider 创建，
  * 其中包含 (sub) service provider
  * scope 由 container（root service provider）释放
  * scope service 不能是从 root service provider 解析的

* scope_service 不在 scope 内解析，自动转换为 singleton !!!
  * 这回造成内存泄漏





