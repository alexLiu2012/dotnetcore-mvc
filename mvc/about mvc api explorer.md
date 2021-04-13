## about api explorer



### 1. about



### 2. details





```c#

```

##### 2.3.1 on providers executing

```c#
public class DefaultApiDescriptionProvider : IApiDescriptionProvider
{
    
}

```

##### 2.3.2 a- 解析 http method

###### 2.3.2.1 api description action data

```c#
public class ApiDescriptionActionData
{    
    public string GroupName { get; set; }
}

```

###### 2.3.2.2 get http methods

```c#

```

##### 2.3.3  b- 创建 api description

```c#

```

###### 2.3.3.1 b1- parse template

```c#
public class DefaultApiDescriptionProvider : IApiDescriptionProvider
{
    private RouteTemplate ParseTemplate(ControllerActionDescriptor action)
    {
        if (action.AttributeRouteInfo?.Template != null)
        {
            return TemplateParser.Parse(action.AttributeRouteInfo.Template);
        }
        
        return null;
    }
}

```

###### 2.3.3.2 b2- get relative path

```c#
public class DefaultApiDescriptionProvider : IApiDescriptionProvider
{
    private string GetRelativePath(RouteTemplate parsedTemplate)
    {
        if (parsedTemplate == null)
        {
            return null;
        }
        
        var segments = new List<string>();
        
        foreach (var segment in parsedTemplate.Segments)
        {
            var currentSegment = string.Empty;
            foreach (var part in segment.Parts)
            {
                if (part.IsLiteral)
                {
                    currentSegment += 
                        _routeOptions.LowercaseUrls 
                        	? part.Text.ToLowerInvariant() 
                        	: part.Text;
                }
                else if (part.IsParameter)
                {
                    currentSegment += "{" + part.Name + "}";
                }
            }
            
            segments.Add(currentSegment);
        }
        
        return string.Join("/", segments);
    }
}
```

###### 2.3.3.3 b3- api parameter context

```c#
internal class ApiParameterContext
{
    public IModelMetadataProvider MetadataProvider { get; }   
    public ControllerActionDescriptor ActionDescriptor { get; }    
    public IReadOnlyList<TemplatePart> RouteParameters { get; } 
    
    public IList<ApiParameterDescription> Results { get; }    
    
    public ApiParameterContext(
        IModelMetadataProvider metadataProvider,
        ControllerActionDescriptor actionDescriptor,
        IReadOnlyList<TemplatePart> routeParameters)
    {
        MetadataProvider = metadataProvider;
        ActionDescriptor = actionDescriptor;
        RouteParameters = routeParameters;
        
        Results = new List<ApiParameterDescription>();
    }            
}

```

##### 2.3.3.4 b4- get parameters

```c#
public class DefaultApiDescriptionProvider : IApiDescriptionProvider
{
    private IList<ApiParameterDescription> GetParameters(ApiParameterContext context)
    {        
        // First, get parameters from the model-binding/parameter-binding side of the world.   
        /* context 中 action description 有 parameter，*/
        if (context.ActionDescriptor.Parameters != null)
        {
            foreach (var actionParameter in context.ActionDescriptor.Parameters)
            {
                var visitor = new PseudoModelBindingVisitor(context, actionParameter);
                
                /* 解析 metadata */
                
                ModelMetadata metadata;
                
                /* 如果 parameter 是 controller parameter descriptior，
                   并且 model metadata provider 是 model metadata provider */
                if (actionParameter is ControllerParameterDescriptor 
                    controllerParameterDescriptor &&
                    _modelMetadataProvider is ModelMetadataProvider provider)
                {
                    /* 调用 model metadata provider 的 get metadata for parameter 方法 */
                    // The default model metadata provider derives from 
                    // ModelMetadataProvider and can therefore supply information 
                    // about attributes applied to parameters.
                    metadata = 
                        provider.GetMetadataForParameter(
                        	controllerParameterDescriptor.ParameterInfo);
                }
                /*否则，fallback，即调用 provider 的 get metadata for type 方法 */
                else
                {
                    // For backward compatibility, 
                    // if there's a custom model metadata provider that
                    // only implements the older IModelMetadataProvider interface, 
                    // access the more limited metadata information it supplies. 
                    // In this scenario, validation attributes
                    // are not supported on parameters.
                    metadata = _modelMetadataProvider.GetMetadataForType(
                        actionParameter.ParameterType);
                }
                
                /* 创建 api parameter description context */
                var bindingContext = 
                    ApiParameterDescriptionContext.GetContext(
								                       metadata,
								                       actionParameter.BindingInfo,
								                       propertyName: actionParameter.Name);
                /* 创建 api parameter description */
                visitor.WalkParameter(bindingContext);
            }
        }
        
        /* context 中 action descriptor 的 bound property 有值 */
        if (context.ActionDescriptor.BoundProperties != null)
        {
            foreach (var actionParameter in context.ActionDescriptor.BoundProperties)
            {
                var visitor = 
                    new PseudoModelBindingVisitor(context, actionParameter);

                // 解析 metadata
                var modelMetadata = 
                    context.MetadataProvider
                    	   .GetMetadataForProperty(
                    			containerType: context.ActionDescriptor
                    								  .ControllerTypeInfo
                    								  .AsType(),
                    			propertyName: actionParameter.Name);
                
                // 创建 api parameter description context
                var bindingContext = 
                    ApiParameterDescriptionContext.GetContext(
                    								   modelMetadata,
                    								   actionParameter.BindingInfo,
                    								   propertyName: actionParameter.Name);
                
                // 创建 api parameter description
                visitor.WalkParameter(bindingContext);
            }
        }
        
        /* 删除 binding source 不是 isFromRequest 的 api parameter description */
        for (var i = context.Results.Count - 1; i >= 0; i--)
        {
            // Remove any 'hidden' parameters. 
            // These are things that can't come from user input,
            // so they aren't worth showing.
            if (!context.Results[i].Source.IsFromRequest)
            {
                context.Results.RemoveAt(i);
            }
        }
        
        // Next, we want to join up any route parameters 
        // with those discovered from the action's parameters.
        // This will result us in creating a parameter representation 
        // for each route parameter that does not have a mapping parameter or bound property.
        ProcessRouteParameters(context);
        
        // Set IsRequired=true
        ProcessIsRequired(context, _mvcOptions);
        
        // Set DefaultValue
        ProcessParameterDefaultValue(context);
        
        return context.Results;
    }
}

```

###### b4-0-a pseudo model binding vistor

```c#
public class DefaultApiDescriptionProvider : IApiDescriptionProvider
{
    private class PseudoModelBindingVisitor
    {
        public ApiParameterContext Context { get; }        
        public ParameterDescriptor Parameter { get; }        
        // Avoid infinite recursion by tracking properties.
        private HashSet<PropertyKey> Visited { get; }
        
        public PseudoModelBindingVisitor(
            ApiParameterContext context, 
            ParameterDescriptor parameter)
        {
            Context = context;
            Parameter = parameter;            
            Visited = new HashSet<PropertyKey>(new PropertyKeyEqualityComparer());
        }
                        
        public void WalkParameter(ApiParameterDescriptionContext context)
        {
            // Attempt to find a binding source for the parameter            
            // The default is ModelBinding (aka all default value providers)
            var source = BindingSource.ModelBinding;
            Visit(context, source, containerName: string.Empty);
        }
        
        private void Visit(
            ApiParameterDescriptionContext bindingContext,
            BindingSource ambientSource,
            string containerName)
        {
            var source = bindingContext.BindingSource;
            if (source != null && source.IsGreedy)
            {
                // We have a definite answer for this model. This is a greedy source like
                // [FromBody] so there's no need to consider properties.
                Context.Results.Add(
                    CreateResult(
                        bindingContext, 
                        source, 
                        containerName));
                return;
            }
            
            var modelMetadata = bindingContext.ModelMetadata;
            
            // For any property which is a leaf node, 
    		// we don't want to keep traversing:
            //
            //  1)  Collections - while it's possible to have binder attributes 
            //		on the inside of a collection, it hardly seems useful, 
            //		and would result in some very weird binding.
            //
            //  2)  Simple Types - These are generally part of the .net framework - primitives,
            //		or types which have a type converter from string.
            //
            //  3)  Types with no properties. Obviously nothing to explore there.
            //
            if (modelMetadata.IsEnumerableType ||
                !modelMetadata.IsComplexType ||
                modelMetadata.Properties.Count == 0)
            {
                Context.Results.Add(
                    CreateResult(
                        bindingContext, 
                        source ?? ambientSource, 
                        containerName));
                return;
            }
            
            // This will come from composite model binding - so investigate 
            // what's going on with each property.
            //
            // Ex:
            //
            //      public IActionResult PlaceOrder(OrderDTO order) {...}
            //
            //      public class OrderDTO
            //      {
            //          public int AccountId { get; set; }
            //
            //          [FromBody]
            //          public Order { get; set; }
            //      }
            //
            // This should result in two parameters:
            //
            //  AccountId - source: Any
            //  Order - source: Body
            //            
            // We don't want to append the **parameter** name when building a model name.       
            var newContainerName = containerName;
            if (modelMetadata.ContainerType != null)
            {
                newContainerName = GetName(containerName, bindingContext);
            }
            
            var metadataProperties = modelMetadata.Properties;
            var metadataPropertiesCount = metadataProperties.Count;
            for (var i = 0; i < metadataPropertiesCount; i++)
            {
                var propertyMetadata = metadataProperties[i];
                var key = new PropertyKey(propertyMetadata, source);
                var bindingInfo = 
                    BindingInfo.GetBindingInfo(
                    				Enumerable.Empty<object>(), 
                    				propertyMetadata);
                
                var propertyContext = 
                    ApiParameterDescriptionContext.GetContext(
                    								   propertyMetadata,
								                       bindingInfo: bindingInfo,
								                       propertyName: null);
                
                if (Visited.Add(key))
                {
                    Visit(
                        propertyContext, 
                        source ?? ambientSource, 
                        newContainerName);
                    
                    Visited.Remove(key);
                }
                else
                {
                    // This is cycle, so just add a result rather than traversing.
                    Context.Results.Add(
                        CreateResult(
                            propertyContext, 
                            source ?? ambientSource, 
                            newContainerName));
                }
            }
        }
        
        private ApiParameterDescription CreateResult(
            ApiParameterDescriptionContext bindingContext,
            BindingSource source,
            string containerName)
        {
            return new ApiParameterDescription()
            {
                ModelMetadata = bindingContext.ModelMetadata,
                Name = GetName(containerName, bindingContext),
                Source = source,
                Type = bindingContext.ModelMetadata.ModelType,
                ParameterDescriptor = Parameter,
                BindingInfo = bindingContext.BindingInfo
            };
        }
        
        private static string GetName(
            string containerName, 
            ApiParameterDescriptionContext metadata)
        {
            var propertyName = 
                !string.IsNullOrEmpty(metadata.BinderModelName) 
                	? metadata.BinderModelName 
                	: metadata.PropertyName;
            
            return ModelNames.CreatePropertyModelName(
                				  containerName, 
                				  propertyName);
        }
        
        private readonly struct PropertyKey
        {
            public readonly Type ContainerType;            
            public readonly string PropertyName;            
            public readonly BindingSource Source;
            
            public PropertyKey(
                ModelMetadata metadata, 
                BindingSource source)
            {                
                ContainerType = metadata.ContainerType;
                PropertyName = metadata.PropertyName;
                Source = source;
            }
        }
        
        private class PropertyKeyEqualityComparer : IEqualityComparer<PropertyKey>
        {
            public bool Equals(PropertyKey x, PropertyKey y)
            {
                return
                    x.ContainerType == y.ContainerType &&
                    x.PropertyName == y.PropertyName &&
                    x.Source == y.Source;
            }
            
            public int GetHashCode(PropertyKey obj)
            {
                var hashCodeCombiner = new HashCode();
                hashCodeCombiner.Add(obj.ContainerType);
                hashCodeCombiner.Add(obj.PropertyName);
                hashCodeCombiner.Add(obj.Source);
                return hashCodeCombiner.ToHashCode();
            }
        }
    }
}

```

###### b4-0-b api parameter description context

```c#
public class DefaultApiDescriptionProvider : IApiDescriptionProvider
{
    private class ApiParameterDescriptionContext
    {
        public ModelMetadata ModelMetadata { get; set; }        
        public string BinderModelName { get; set; }        
        public BindingSource BindingSource { get; set; }        
        public string PropertyName { get; set; }        
        public BindingInfo BindingInfo { get; set; }
        
        public static ApiParameterDescriptionContext GetContext(
            ModelMetadata metadata,
            BindingInfo bindingInfo,
            string propertyName)
        {
            // BindingMetadata can be null if the metadata represents properties.
            return new ApiParameterDescriptionContext
            {
                ModelMetadata = metadata,
                BinderModelName = bindingInfo?.BinderModelName,
                BindingSource = bindingInfo?.BindingSource,
                PropertyName = propertyName ?? metadata.Name,
                BindingInfo = bindingInfo,
            };
        }
    }
}
```

###### b4-1 process route parameter

```c#
public class DefaultApiDescriptionProvider : IApiDescriptionProvider
{
    private void ProcessRouteParameters(ApiParameterContext context)
    {
        /* 解析 api parameter context 中的 route parameter */
        var routeParameters =
            new Dictionary<string, ApiParameterRouteInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var routeParameter in context.RouteParameters)
        {
            routeParameters.Add(
                routeParameter.Name, 
                CreateRouteInfo(routeParameter));
        }
        
        // 遍历 api parameter context 中的 api parameter description 集合
        foreach (var parameter in context.Results)
        {
            // 如果 api parameter description 是
            //   - source = path
            //   - source = model binding
            //   - source = custom
            if (parameter.Source == BindingSource.Path ||
                parameter.Source == BindingSource.ModelBinding ||
                parameter.Source == BindingSource.Custom)
            {
                // 解析 api parameter route info
                if (routeParameters.TryGetValue(parameter.Name, out var routeInfo))
                {
                    // 移动到 api parameter context 的 result 中
                    parameter.RouteInfo = routeInfo;
                    routeParameters.Remove(parameter.Name);
                                        
                    if (parameter.Source == BindingSource.ModelBinding &&
                        !parameter.RouteInfo.IsOptional)
                    {
                        // If we didn't see any information about the parameter, but we have
                        // a route parameter that matches, let's switch it to path.
                        parameter.Source = BindingSource.Path;
                    }
                }
            }
        }
        
        /* 为 其余 route parameter 创建 api parameter description，
           并注入 context 的 result 中 */
        // Lastly, create a parameter representation for each route parameter that did not find
        // a partner.
        foreach (var routeParameter in routeParameters)
        {
            context.Results.Add(
                new ApiParameterDescription()
                {
                    Name = routeParameter.Key,
                    RouteInfo = routeParameter.Value,
                    Source = BindingSource.Path,
                });
        }
    }
    
    private ApiParameterRouteInfo CreateRouteInfo(TemplatePart routeParameter)
    {
        var constraints = new List<IRouteConstraint>();
        if (routeParameter.InlineConstraints != null)
        {
            foreach (var constraint in routeParameter.InlineConstraints)
            {                    
                constraints.Add(
                	_constraintResolver.ResolveConstraint(constraint.Constraint));
            }
        }
        
        return new ApiParameterRouteInfo()
        {
            Constraints = constraints,
            DefaultValue = routeParameter.DefaultValue,
            IsOptional = routeParameter.IsOptional || 
                		 routeParameter.DefaultValue != null,
        };
    }
}

```

###### b4-2 process is required

```c#
public class DefaultApiDescriptionProvider : IApiDescriptionProvider
{
    internal static void ProcessIsRequired(
        ApiParameterContext context, 
        MvcOptions mvcOptions)
    {
        foreach (var parameter in context.Results)
        {
            if (parameter.Source == BindingSource.Body)
            {
                if (parameter.BindingInfo == null || 
                    parameter.BindingInfo.EmptyBodyBehavior == EmptyBodyBehavior.Default)
                {
                    parameter.IsRequired = 
                        !mvcOptions.AllowEmptyInputInBodyModelBinding;
                }
                else
                {
                    parameter.IsRequired = 
                        !(parameter.BindingInfo.EmptyBodyBehavior == EmptyBodyBehavior.Allow);
                }
            }
            
            if (parameter.ModelMetadata != null && 
                parameter.ModelMetadata.IsBindingRequired)
            {
                parameter.IsRequired = true;
            }
            
            if (parameter.Source == BindingSource.Path && 
                parameter.RouteInfo != null && 
                !parameter.RouteInfo.IsOptional)
            {
                parameter.IsRequired = true;
            }
        }
    }
}

```

###### b4-3 process parameter default value

```c#
public class DefaultApiDescriptionProvider : IApiDescriptionProvider
{
    internal static void ProcessParameterDefaultValue(ApiParameterContext context)
    {
        foreach (var parameter in context.Results)
        {
            if (parameter.Source == BindingSource.Path)
            {
                parameter.DefaultValue = parameter.RouteInfo?.DefaultValue;
            }
            else
            {
                if (parameter.ParameterDescriptor is ControllerParameterDescriptor 
                    controllerParameter && 
                    ParameterDefaultValues
                    	.TryGetDeclaredParameterDefaultValue(
	                        controllerParameter.ParameterInfo, 
		                    out var defaultValue))
                {
                    parameter.DefaultValue = defaultValue;
                }
            }
        }
    }
}

```

###### b5- get request metadata attribute

```c#
public class DefaultApiDescriptionProvider : IApiDescriptionProvider
{
    private IApiRequestMetadataProvider[] GetRequestMetadataAttributes(
        ControllerActionDescriptor action)
    {
        if (action.FilterDescriptors == null)
        {
            return null;
        }
        
        // This technique for enumerating filters will intentionally ignore any filter 
        // that is an IFilterFactory while searching for a filter that 
        // implements IApiRequestMetadataProvider.
        //
        // The workaround for that is to implement the metadata interface on the FilterFactory.
        return action.FilterDescriptors
            		 .Select(fd => fd.Filter)
		             .OfType<IApiRequestMetadataProvider>()
         		     .ToArray();
    }
}


```



###### b6- get declare content type

```c#
public class DefaultApiDescriptionProvider : IApiDescriptionProvider
{
    private static MediaTypeCollection GetDeclaredContentTypes(
        IApiRequestMetadataProvider[] requestMetadataAttributes)
    {
        // Walk through all 'filter' attributes in order, 
        // and allow each one to see or override the results of the previous ones. 
        // This is similar to the execution path for content-negotiation.
        var contentTypes = new MediaTypeCollection();
        if (requestMetadataAttributes != null)
        {
            foreach (var metadataAttribute in requestMetadataAttributes)
            {
                metadataAttribute.SetContentTypes(contentTypes);
            }
        }
        
        return contentTypes;
    }
}

```

###### b7- get support format

```c#
public class DefaultApiDescriptionProvider : IApiDescriptionProvider
{
    private IReadOnlyList<ApiRequestFormat> GetSupportedFormats(
        MediaTypeCollection contentTypes, 
        Type type)
    {
        if (contentTypes.Count == 0)
        {
            contentTypes = new MediaTypeCollection
            {
                (string)null,
            };
        }
        
        var results = new List<ApiRequestFormat>();
        foreach (var contentType in contentTypes)
        {
            foreach (var formatter in _mvcOptions.InputFormatters)
            {
                if (formatter is IApiRequestFormatMetadataProvider 
                    requestFormatMetadataProvider)
                {
                    var supportedTypes = requestFormatMetadataProvider
                        .GetSupportedContentTypes(contentType, type);
                    
                    if (supportedTypes != null)
                    {
                        foreach (var supportedType in supportedTypes)
                        {
                            results.Add(
                                new ApiRequestFormat()
                                {
                                    Formatter = formatter,
                                    MediaType = supportedType,
                                });
                        }
                    }
                }
            }
        }
        
        return results;
    }
}

```

#### 2.3 api description group

##### 2.3.1 api description group collection

###### 2.3.1.1 api description group

```c#

```

###### 2.3.1.2 api description group collection

```c#

```

##### 2.3.2 api description group collection provider

###### 2.3.2.1 接口

```c#

```

###### 2.3.2.1 实现

```c#

```

#### 2.4 add api explorer

```c#
public static class MvcApiExplorerMvcCoreBuilderExtensions
{        
    public static IMvcCoreBuilder AddApiExplorer(this IMvcCoreBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        AddApiExplorerServices(builder.Services);
        return builder;
    }
            
    internal static void AddApiExplorerServices(IServiceCollection services)
    {
        services.TryAddSingleton<
            IApiDescriptionGroupCollectionProvider, 
        	ApiDescriptionGroupCollectionProvider>();
        
        services.TryAddEnumerable(
            ServiceDescriptor.Transient<
            					IApiDescriptionProvider, 
            					DefaultApiDescriptionProvider>());
    }
}

```

### 3. practice

