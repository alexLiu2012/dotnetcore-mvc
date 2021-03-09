## about routing



### 1. about

route pattern 是路由模式，即框架使用的、解析后的路由信息的封装：

* route pattern path segment，表示 path，如 /controller/action
* route pattern part，表示 route pattern path segment 中的 part，分为3个类型
  * literal part，文本，即 path 本身
  * separator，分隔符，即"/"
  * parameter，参数，形如`{*name:policy=default?}`

创建 route pattern：

* route pattern parser，从 string 解析成 route pattern
* route pattern factory，由 path segment、part 构造函数创建

验证 (input request route info) 与 route pattern：

* route pattern matcher



route template 是路由模板，即面向用于（输入）的

* template segment，表示 path，对应 route pattern path segment
* template part，对应 route pattern part

创建 route template：

* template parser

验证 （input request route info）route template：

* template matcher
  * 实际调用 route pattern matcher 验证

### 2. details

#### 2.1 route pattern 

##### 2.1.1 route pattern path segment

```c#
[DebuggerDisplay("{DebuggerToString()}")]
public sealed class RoutePatternPathSegment
{
    public bool IsSimple => Parts.Count == 1;        
    public IReadOnlyList<RoutePatternPart> Parts { get; }
    
    internal RoutePatternPathSegment(IReadOnlyList<RoutePatternPart> parts)
    {
        Parts = parts;
    }
                
    internal string DebuggerToString()
    {
        return DebuggerToString(Parts);
    }
    
    internal static string DebuggerToString(IReadOnlyList<RoutePatternPart> parts)
    {
        return string.Join(
            string.Empty, 
            parts.Select(p => p.DebuggerToString()));
    }
}

```

##### 2.1.2 route pattern part

```c#
public abstract class RoutePatternPart
{
    public RoutePatternPartKind PartKind { get; }        
    
    // This class is **not** an extensibility point - every part of the routing system
    // needs to be aware of what kind of parts we support.       
    // It is abstract so we can add semantics later inside the library.
    private protected RoutePatternPart(RoutePatternPartKind partKind)
        
    {
        PartKind = partKind;
    }
    
    public bool IsLiteral => PartKind == RoutePatternPartKind.Literal;        
    public bool IsParameter => PartKind == RoutePatternPartKind.Parameter;        
    public bool IsSeparator => PartKind == RoutePatternPartKind.Separator;
        
    internal abstract string DebuggerToString();
}

public enum RoutePatternPartKind
{     
    Literal,          
    Parameter,          
    Separator,
}

```

###### 2.1.2.1 literal part

* 表示文本

```c#
[DebuggerDisplay("{DebuggerToString()}")]
public sealed class RoutePatternLiteralPart : RoutePatternPart
{
    public string Content { get; }
    
    internal RoutePatternLiteralPart(string content) 
        : base(RoutePatternPartKind.Literal)
    {
        Debug.Assert(!string.IsNullOrEmpty(content));
        Content = content;
    }
              
    internal override string DebuggerToString()
    {
        return Content;
    }
}

```

###### 2.1.2.2 separator part

* 表示分隔符

```c#
[DebuggerDisplay("{DebuggerToString()}")]
public sealed class RoutePatternSeparatorPart : RoutePatternPart
{
    public string Content { get; }
    
    internal RoutePatternSeparatorPart(string content)
        : base(RoutePatternPartKind.Separator)
    {
        Debug.Assert(!string.IsNullOrEmpty(content));            
        Content = content;
    }
            
    internal override string DebuggerToString()
    {
        return Content;
    }
}

```

###### 2.1.2.3 parameter part

* 表示参数，格式：
  * `{*name:policy=default?}`

```c#
[DebuggerDisplay("{DebuggerToString()}")]
public sealed class RoutePatternParameterPart : RoutePatternPart
{
    public string Name { get; }    
    public object? Default { get; }
    public RoutePatternParameterKind ParameterKind { get; }
    public IReadOnlyList<RoutePatternParameterPolicyReference> ParameterPolicies { get; }      
    public bool EncodeSlashes { get; }        
    
    public bool IsCatchAll => ParameterKind == RoutePatternParameterKind.CatchAll;        
    public bool IsOptional => ParameterKind == RoutePatternParameterKind.Optional;
    
    internal RoutePatternParameterPart(
        string parameterName,
        object? @default,
        RoutePatternParameterKind parameterKind,
        RoutePatternParameterPolicyReference[] parameterPolicies)
            : this(
                parameterName, 
                @default, 
                parameterKind, 
                parameterPolicies, 
                encodeSlashes: true)
    {
    }
    
    internal RoutePatternParameterPart(
        string parameterName,
        object? @default,
        RoutePatternParameterKind parameterKind,
        RoutePatternParameterPolicyReference[] parameterPolicies,
        bool encodeSlashes)
        	: base(RoutePatternPartKind.Parameter)
    {
        // See #475 - this code should have some asserts, 
        // but it can't because of the design of RouteParameterParser.

        Name = parameterName;
        Default = @default;
        ParameterKind = parameterKind;
        ParameterPolicies = parameterPolicies;
        EncodeSlashes = encodeSlashes;
    }
                    
    internal override string DebuggerToString()
    {
        var builder = new StringBuilder();
        builder.Append("{");
        
        if (IsCatchAll)
        {
            builder.Append("*");
            if (!EncodeSlashes)
            {
                builder.Append("*");
            }
        }
        
        builder.Append(Name);
        
        foreach (var constraint in ParameterPolicies)
        {
            builder.Append(":");
            builder.Append(constraint.ParameterPolicy);
        }
        
        if (Default != null)
        {
            builder.Append("=");
            builder.Append(Default);
        }
        
        if (IsOptional)
        {
            builder.Append("?");
        }
        
        builder.Append("}");
        return builder.ToString();
    }
}

// pattern parameter part 枚举
public enum RoutePatternParameterKind
{    
    Standard,        
    Optional,        
    CatchAll,
}

```

###### 2.1.2.4 pattern parameter policy reference

* `parameter policy` 的封装，包含 parameter policy 本身和原始 content string

```c#
[DebuggerDisplay("{DebuggerToString()}")]
public sealed class RoutePatternParameterPolicyReference
{
    public string? Content { get; }        
    public IParameterPolicy? ParameterPolicy { get; }
    
    internal RoutePatternParameterPolicyReference(string content)
    {
        Content = content;
    }
    
    internal RoutePatternParameterPolicyReference(IParameterPolicy parameterPolicy)
    {
        ParameterPolicy = parameterPolicy;
    }
                
    private string? DebuggerToString()
    {
        return Content;
    }
}

```

###### 2.1.2.5 pattern parameter parser

* 从 parameter string 解析`pattern parameter part`

```c#
internal static class RouteParameterParser
{
    // This code parses the inside of the route parameter 
    //
    // Ex: {hello} - this method is responsible for parsing 'hello'
    // The factoring between this class and RoutePatternParser is due to legacy.
    public static RoutePatternParameterPart ParseRouteParameter(string parameter)
    {
        // paramete string 为 null，抛出异常
        if (parameter == null)
        {
            throw new ArgumentNullException(nameof(parameter));
        }
        
        // parameter string 为 empty，创建 empty parameter part
        if (parameter.Length == 0)
        {
            return new RoutePatternParameterPart(
                string.Empty, 
                null, 
                RoutePatternParameterKind.Standard, 
                Array.Empty<RoutePatternParameterPolicyReference>());
        }
                
        var startIndex = 0;
        var endIndex = parameter.Length - 1;
        var encodeSlashes = true;        
        var parameterKind = RoutePatternParameterKind.Standard;
        
        // 处理前缀 prefix，如果有“**”或者“*”表示 catch all
        if (parameter.StartsWith("**", StringComparison.Ordinal))
        {
            encodeSlashes = false;
            parameterKind = RoutePatternParameterKind.CatchAll;
            startIndex += 2;
        }
        else if (parameter[0] == '*')
        {
            parameterKind = RoutePatternParameterKind.CatchAll;
            startIndex++;
        }
        // 处理后缀，如果有“？”表示 optional 
        if (parameter[endIndex] == '?')
        {
            parameterKind = RoutePatternParameterKind.Optional;
            endIndex--;
        }
        
        var currentIndex = startIndex;
        
        /* 解析 parameter name */
        
        var parameterName = string.Empty;
        while (currentIndex <= endIndex)
        {
            var currentChar = parameter[currentIndex];
            
            // 字符串中的 ':' 和 '='' 后面的是 constraint 或者 defualt，
            // 前面部分就是 parameter name
            // param name 可以由 ':' 和 '=' 开头，所以忽略第一个 ':' 或 '='，
            // 即 startIndex != currentIndex
            if ((currentChar == ':' || currentChar == '=') && 
                startIndex != currentIndex)
            {                
                parameterName = parameter.Substring(
                    startIndex, 
                    currentIndex - startIndex);
                
                // Roll the index back and move to the constraint parsing stage.
                currentIndex--;
                break;
            }
            else if (currentIndex == endIndex)
            {
                parameterName = parameter.Substring(
                    startIndex, 
                    currentIndex - startIndex + 1);
            }
            
            currentIndex++;
        }
		
        /* 解析 constraints */
        
        var parseResults = ParseConstraints(
            parameter, 
            currentIndex, 
            endIndex);
        
        /* 解析 default value */
        
        currentIndex = parseResults.CurrentIndex;        
        string? defaultValue = null;
        
        // 字符串中 '=' 后面的是 default value
        if (currentIndex <= endIndex &&
            parameter[currentIndex] == '=')
        {
            defaultValue = parameter.Substring(
                currentIndex + 1, 
                endIndex - currentIndex);
        }
        
        /* 创建 parameter part */
        
        return new RoutePatternParameterPart(
            parameterName,
            defaultValue,
            parameterKind,
            parseResults.ParameterPolicies,
            encodeSlashes);
    }           
}

```

###### 2.1.2.6 parse parameter constraint

* 解析`pattern parameter parser`时解析 parameter policy (constraint) 的方法

```c#
internal static class RouteParameterParser
{
    private enum ParseState
    {
        Start,				// 开始解析
        ParsingName,		// 找到了约束（“：”开头），开始解析约束
        InsideParenthesis,	// 找到了约束的参数（“（”开头），开始解析约束参数
        End					// 结束解析
    }
    
    /* parameter policy result 结构体 */
    private readonly struct ParameterPolicyParseResults
    {
        public readonly int CurrentIndex;        
        public readonly RoutePatternParameterPolicyReference[] ParameterPolicies;
        
        public ParameterPolicyParseResults(
            int currentIndex, 
            RoutePatternParameterPolicyReference[] parameterPolicies)
        {
            CurrentIndex = currentIndex;
            ParameterPolicies = parameterPolicies;
        }
    }
    
    /* 解析 constraint result */
    private static ParameterPolicyParseResults ParseConstraints(
        string text,
        int currentIndex,
        int endIndex)
    {
        // 初始化 constraints
        var constraints = new ArrayBuilder<RoutePatternParameterPolicyReference>(0);
        var state = ParseState.Start;
        var startIndex = currentIndex;
        
        do
        {
            // 解析当前字符
            var currentChar = currentIndex > endIndex 
                ? null 
                : (char?)text[currentIndex];
            
            switch (state)
            {
                // 开始解析
                case ParseState.Start:
                    switch (currentChar)
                    {
                        case null:
                            state = ParseState.End;                               
                            break; 
                        case ':':	// 找到“：”，有约束，后面是约束，
                            		// 转到约束解析过程
                            state = ParseState.ParsingName;
                            startIndex = currentIndex + 1;
                            break;
                        case '(':	// 找到“（”，由约束的参数，后面是参数，
                            		// 转到解析约束参数的过程
                            state = ParseState.InsideParenthesis;
                            break;
                        case '=':	// 找到“=”，没有约束，后面是 default value，
                            		// 解析结束
                            state = ParseState.End;
                            currentIndex--;
                            break;
                    }
                    break;
                // 解析约束的参数
                case ParseState.InsideParenthesis:
                    switch (currentChar)
                    {
                        case null:
                            state = ParseState.End;
                            var constraintText = text.Substring(
                                startIndex, 
                                currentIndex - startIndex);
                            constraints.Add(
                                RoutePatternFactory
                                	.ParameterPolicy(constraintText));
                            break;
                        case ')':
                            // Only consume a ')' token if
                            // (a) it is the last token
                            // (b) the next character is the start of the new constraint ':
                            // (c) the next character is the start of the default value.
                            var nextChar = currentIndex + 1 > endIndex 
                                ? null 
                                : (char?)text[currentIndex + 1];
                            switch (nextChar)
                            {
                                case null:
                                    state = ParseState.End;
                                    constraintText = text.Substring(
                                        startIndex, 
                                        currentIndex - startIndex + 1);
                                    constraints.Add(
                                        RoutePatternFactory
                                        	.ParameterPolicy(constraintText));
                                    break;
                                case ':':
                                    state = ParseState.Start;
                                    constraintText = text.Substring(
                                        startIndex, 
                                        currentIndex - startIndex + 1);
                                    constraints.Add(
                                        RoutePatternFactory
                                        	.ParameterPolicy(constraintText));
                                    startIndex = currentIndex + 1;
                                    break;
                                case '=':
                                    state = ParseState.End;
                                    constraintText = text.Substring(
                                        startIndex, 
                                        currentIndex - startIndex + 1);
                                    constraints.Add(
                                        RoutePatternFactory
                                        	.ParameterPolicy(constraintText));
                                    break;
                            }
                            break;
                        case ':':
                        case '=':
                            // In the original implementation, 
                            // the Regex would've backtracked if it encountered an
                            // unbalanced opening bracket followed by 
                            // (not necessarily immediatiely) a delimiter.
                            // Simply verifying that the parantheses will eventually 
                            // be closed should suffice to determine 
                            // if the terminator needs to be consumed as part of 
                            // the current constraint specification.
                            var indexOfClosingParantheses = text.IndexOf(
                                ')', 
                                currentIndex + 1);
                            
                            if (indexOfClosingParantheses == -1)
                            {
                                constraintText = text.Substring(
                                    startIndex, 
                                    currentIndex - startIndex);
                                    constraints.Add(
                                        RoutePatternFactory
                                        	.ParameterPolicy(constraintText));
                                
                                if (currentChar == ':')
                                {
                                    state = ParseState.ParsingName;
                                    startIndex = currentIndex + 1;
                                }
                                else
                                {
                                    state = ParseState.End;
                                    currentIndex--;
                                }
                            }
                            else
                            {
                                currentIndex = indexOfClosingParantheses;
                            }
                            
                            break;
                    }
                    break;
                // 解析约束
                case ParseState.ParsingName:
                    switch (currentChar)
                    {
                        case null:	// null，字符串已经结尾
                            		// 解析解析，
                            // 截取字符串为 constraint name，加入 constraints 集合
                            state = ParseState.End;
                            var constraintText = text.Substring(
                                startIndex, 
                                currentIndex - startIndex);
                            if (constraintText.Length > 0)
                            {                                    
                                constraints.Add(
                                RoutePatternFactory
                                	.ParameterPolicy(constraintText));
                            }
                            break;
                        case ':':	// 找到“：”，表示有另一一个约束   
                            // 截取字符串为 constraint name，加入 constraints 集合
                            constraintText = text.Substring(
                                startIndex, 
                                currentIndex - startIndex);
                            if (constraintText.Length > 0)
                            {
                                constraints.Add(
                                    RoutePatternFactory
                                    	.ParameterPolicy(constraintText));
                            }
                            startIndex = currentIndex + 1;
                            break;
                        case '(':	// 找到“（”，有参数，
                            		// 转到约束参数解析过程
                            state = ParseState.InsideParenthesis;
                            break;
                        case '=':	// 找到“=”，后面是 default value
                            		// 约束解析结束，
                            		// 截取字符串为 constraint name, 加入 constraints 集合
                            state = ParseState.End;
                            constraintText = text.Substring(
                                startIndex, 
                                currentIndex - startIndex);
                            if (constraintText.Length > 0)
                            {
                                constraints.Add(
                                    RoutePatternFactory
                                    	.ParameterPolicy(constraintText));
                            }
                            currentIndex--;
                            break;
                    }
                    break;
            }
            
            // 不是上述情况，即解析中的普通字符，
            // 移动字符指针
            currentIndex++;            
        } 
        while (state != ParseState.End);
        
        // 创建结果
        return new ParameterPolicyParseResults(
            currentIndex, 
            constraints.ToArray());
    }        
}

```

##### 2.1.3 route pattern

```c#
[DebuggerDisplay("{DebuggerToString()}")]
public sealed class RoutePattern
{
    /* 分隔符 */
    private const string SeparatorString = "/";            
    /* 任意 */
    public static readonly object RequiredValueAny = new RequiredValueAnySentinal();
    
    [DebuggerDisplay("{DebuggerToString(),nq}")]
    private class RequiredValueAnySentinal
    {
        private string DebuggerToString() => "*any*";
    }
        
    public string? RawText { get; }
    public IReadOnlyList<RoutePatternPathSegment> PathSegments { get; }
    public IReadOnlyList<RoutePatternParameterPart> Parameters { get; }      
    public IReadOnlyDictionary<
        string, 
    	IReadOnlyList<RoutePatternParameterPolicyReference>> 
            ParameterPolicies { get; }
    public IReadOnlyDictionary<string, object?> Defaults { get; }   
    public IReadOnlyDictionary<string, object?> RequiredValues { get; }       
             
    public decimal InboundPrecedence { get; }    
    public decimal OutboundPrecedence { get; }   
                                                                                               
    internal RoutePattern(
        string? rawText,
        IReadOnlyDictionary<string, object?> defaults,
        IReadOnlyDictionary<
        	string, 
        	IReadOnlyList<RoutePatternParameterPolicyReference>> 
        		parameterPolicies,
        IReadOnlyDictionary<string, object?> requiredValues,
        IReadOnlyList<RoutePatternParameterPart> parameters,
        IReadOnlyList<RoutePatternPathSegment> pathSegments)
    {
        Debug.Assert(defaults != null);
        Debug.Assert(parameterPolicies != null);
        Debug.Assert(parameters != null);
        Debug.Assert(requiredValues != null);
        Debug.Assert(pathSegments != null);
        
        RawText = rawText;
        PathSegments = pathSegments;
        Parameters = parameters;        
        ParameterPolicies = parameterPolicies;
        Defaults = defaults;
        RequiredValues = requiredValues;
                                        
        InboundPrecedence = RoutePrecedence.ComputeInbound(this);
        OutboundPrecedence = RoutePrecedence.ComputeOutbound(this);
    }
    
    // 判断 value 是不是 any
    internal static bool IsRequiredValueAny(object? value)
    {
        return object.ReferenceEquals(RequiredValueAny, value);
    }
    
    // get parameter                                                
    public RoutePatternParameterPart? GetParameter(string name)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        var parameters = Parameters;
        // Read interface .Count once rather than per iteration
        var parametersCount = parameters.Count;
        for (var i = 0; i < parametersCount; i++)
        {
            var parameter = parameters[i];
            if (string.Equals(
                	parameter.Name, 
                	name, 
                	StringComparison.OrdinalIgnoreCase))       
            { 
                return parameter;
            }
        }
        
        return null;
    }
    
    internal string DebuggerToString()
    {
        return RawText 
            ?? string.Join(
            	SeparatorString, 
            	PathSegments.Select(s => s.DebuggerToString()));
    }        
}

```

###### 2.1.3.1 route precedence - inbound

```c#
public static class RoutePrecedence
{    
    /* compute inbound with route template */
    
    // <example>
    //     e.g.: /api/template == 1.1
    //     /api/template/{id} == 1.13
    //     /api/{id:int} == 1.2
    //     /api/template/{id:int} == 1.12
    // </example>    
    public static decimal ComputeInbound(RouteTemplate template)
    {
        ValidateSegementLength(template.Segments.Count);

        // Each precedence digit corresponds to one decimal place. 
        // For example, 3 segments with precedences 2, 1, and 4 results in a combined 
        // precedence of 2.14 (decimal).
        var precedence = 0m;
        
        for (var i = 0; i < template.Segments.Count; i++)
        {
            var segment = template.Segments[i];
            
            var digit = ComputeInboundPrecedenceDigit(segment);
            Debug.Assert(digit >= 0 && digit < 10);
            
            precedence += decimal.Divide(digit, (decimal)Math.Pow(10, i));
        }
        
        return precedence;
    }
    
     // Segments have the following order:
    // 1 - Literal segments
    // 2 - Constrained parameter segments / Multi-part segments
    // 3 - Unconstrained parameter segments
    // 4 - Constrained wildcard parameter segments
    // 5 - Unconstrained wildcard parameter segments
    private static int ComputeInboundPrecedenceDigit(TemplateSegment segment)
    {
        if (segment.Parts.Count > 1)
        {
            // Multi-part segments should appear after literal segments and along with 
            // parameter segments
            return 2;
        }
        
        var part = segment.Parts[0];
        // Literal segments always go first
        if (part.IsLiteral)
        {
            return 1;
        }
        else
        {
            Debug.Assert(part.IsParameter);
            var digit = part.IsCatchAll ? 5 : 3;
            
            // If there is a route constraint for the parameter, reduce order by 1
            // Constrained parameters end up with order 2, Constrained catch alls end up 
            // with order 4
            if (part.InlineConstraints != null && part.InlineConstraints.Any())
            {
                digit--;
            }
            
            return digit;
        }
    }
    
    /* computer inbound with route pattern */
    
    // See description on ComputeInbound(RouteTemplate)
    internal static decimal ComputeInbound(RoutePattern routePattern)
    {
        ValidateSegementLength(routePattern.PathSegments.Count);
        
        var precedence = 0m;
        
        for (var i = 0; i < routePattern.PathSegments.Count; i++)
        {
            var segment = routePattern.PathSegments[i];
            
            var digit = ComputeInboundPrecedenceDigit(routePattern, segment);
            Debug.Assert(digit >= 0 && digit < 10);
            
            precedence += decimal.Divide(digit, (decimal)Math.Pow(10, i));
        }
        
        return precedence;
    }
    
    // see description on ComputeInboundPrecedenceDigit(TemplateSegment segment)
    //
    // With a RoutePattern, parameters with a required value are treated as a literal segment
    internal static int ComputeInboundPrecedenceDigit(
        RoutePattern routePattern, 
        RoutePatternPathSegment pathSegment)
    {
        if (pathSegment.Parts.Count > 1)
        {
            // Multi-part segments should appear after literal segments and along with 
            // parameter segments
            return 2;
        }
        
        var part = pathSegment.Parts[0];
        // Literal segments always go first
        if (part.IsLiteral)
        {
            return 1;
        }
        else if (part is RoutePatternParameterPart parameterPart)
        {
            // Parameter with a required value is matched as a literal
            if (routePattern.RequiredValues
                			.TryGetValue(
                                parameterPart.Name, 
                                out var requiredValue) &&
                !RouteValueEqualityComparer.Default
                						   .Equals(requiredValue, string.Empty))
            {
                return 1;
            }
            
            var digit = parameterPart.IsCatchAll ? 5 : 3;
            
            
            // If there is a route constraint for the parameter, reduce order by 1
            // Constrained parameters end up with order 2, Constrained catch alls end up 
            // with order 4
            if (parameterPart.ParameterPolicies.Count > 0)
            {
                digit--;
            }    
            
            return digit;
        }
        else
        {
            // Unreachable
            throw new NotSupportedException();
        }
    }
       
    /* validate segment length */           
    private static void ValidateSegementLength(int length)
    {
        if (length > 28)
        {
            // An OverflowException will be thrown by Math.Pow when greater than 28
            throw new InvalidOperationException(
                "Route exceeds the maximum number of allowed segments of 28 and 
                "is unable to be processed.");
        }
    }                            
}

```

###### 2.1.3.2 route precedence - outbound

```c#
public static class RoutePrecedence
{                         
    /* compute outbound with route template */
    
    // <example>
    //     e.g.: /api/template    == 5.5
    //     /api/template/{id}     == 5.53
    //     /api/{id:int}          == 5.4
    //     /api/template/{id:int} == 5.54
    // </example>    
    public static decimal ComputeOutbound(RouteTemplate template)
    {
        ValidateSegementLength(template.Segments.Count);
        
        // Each precedence digit corresponds to one decimal place.
        // For example, 3 segments with precedences 2, 1, and 4 results in a combined 
        // precedence of 2.14 (decimal).
        var precedence = 0m;
        
        for (var i = 0; i < template.Segments.Count; i++)
        {
            var segment = template.Segments[i];
            
            var digit = ComputeOutboundPrecedenceDigit(segment);
            Debug.Assert(digit >= 0 && digit < 10);
            
            precedence += decimal.Divide(digit, (decimal)Math.Pow(10, i));
        }
        
        return precedence;
    }
    
    // Segments have the following order:
    // 5 - Literal segments
    // 4 - Multi-part segments && Constrained parameter segments
    // 3 - Unconstrained parameter segements
    // 2 - Constrained wildcard parameter segments
    // 1 - Unconstrained wildcard parameter segments
    private static int ComputeOutboundPrecedenceDigit(TemplateSegment segment)
    {
        if(segment.Parts.Count > 1)
        {
            return 4;
        }
        
        var part = segment.Parts[0];
        if(part.IsLiteral)
        {
            return 5;
        }
        else
        {
            Debug.Assert(part.IsParameter);
            var digit = part.IsCatchAll ? 1 :  3;
            
            if (part.InlineConstraints != null && part.InlineConstraints.Any())
            {
                digit++;
            }
            
            return digit;
        }
    }
    
    
    /* compute outbound with route pattern */
    
    // see description on ComputeOutbound(RouteTemplate)
    internal static decimal ComputeOutbound(RoutePattern routePattern)
    {
        ValidateSegementLength(routePattern.PathSegments.Count);
        
        // Each precedence digit corresponds to one decimal place. 
        // For example, 3 segments with precedences 2, 1, and 4 results in a combined 
        // precedence of 2.14 (decimal).
        var precedence = 0m;
        
        for (var i = 0; i < routePattern.PathSegments.Count; i++)
        {
            var segment = routePattern.PathSegments[i];
            
            var digit = ComputeOutboundPrecedenceDigit(segment);
            Debug.Assert(digit >= 0 && digit < 10);
            
            precedence += decimal.Divide(digit, (decimal)Math.Pow(10, i));
        }
        
        return precedence;
    }
                   
    // See description on ComputeOutboundPrecedenceDigit(TemplateSegment segment)
    private static int ComputeOutboundPrecedenceDigit(RoutePatternPathSegment pathSegment)
    {
        if (pathSegment.Parts.Count > 1)
        {
            return 4;
        }
        
        var part = pathSegment.Parts[0];
        if (part.IsLiteral)
        {
            return 5;
        }
        else if (part is RoutePatternParameterPart parameterPart)
        {
            Debug.Assert(parameterPart != null);
            var digit = parameterPart.IsCatchAll ? 1 : 3;
            
            if (parameterPart.ParameterPolicies.Count > 0)
            {
                digit++;
            }
            
            return digit;
        }
        else
        {
            // Unreachable
            throw new NotSupportedException();
        }
    }
    
    // validate segment length
    private static void ValidateSegementLength(int length)
    {
        if (length > 28)
        {
            // An OverflowException will be thrown by Math.Pow when greater than 28
            throw new InvalidOperationException(
                "Route exceeds the maximum number of allowed segments of 28 and 
                "is unable to be processed.");
        }
    }                            
}

```

##### 2.1.4 route pattern parser

* 从字符串解析 route pattern 的 segments 集合，
* 然后由`route pattern factory`创建`route pattern`

```c#
internal static class RoutePatternParser
{
    private const char Separator = '/';
    private const char OpenBrace = '{';
    private const char CloseBrace = '}';
    private const char EqualsSign = '=';
    private const char QuestionMark = '?';
    private const char Asterisk = '*';
    private const string PeriodString = ".";
    
    internal static readonly char[] InvalidParameterNameChars = new char[]
    {
        Separator,
        OpenBrace,
        CloseBrace,
        QuestionMark,
        Asterisk
    };
    
    /* parse 方法 */
    
    public static RoutePattern Parse(string pattern)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }
        
        // 去掉 pattern 空格
        var trimmedPattern = TrimPrefix(pattern);        
        // 创建 pattern parse context
        var context = new Context(trimmedPattern);     
        
        /* 解析 segment */
        
        // 创建 pattern path segment（预结果）
        var segments = new List<RoutePatternPathSegment>();
        
        while (context.MoveNext())
        {
            var i = context.Index;
            
            // 如果当前字符是 separator，抛出异常
            if (context.Current == Separator)
            {
                // If we get here is means that there's a consecutive '/' character.
                // Templates don't start with a '/' and 
                // parsing a segment consumes the separator.
                throw new RoutePatternException(
                    pattern, 
                    Resources.TemplateRoute_CannotHaveConsecutiveSeparators);
            }
            
            // parse segment（同时递归 parse parameter），
            // 如果不成功，抛出异常
            if (!ParseSegment(context, segments))
            {
                throw new RoutePatternException(pattern, context.Error);
            }
            
            // A successful parse should always result in us 
            // being at the end or at a separator.
            Debug.Assert(context.AtEnd() || 
                         context.Current == Separator);
            
            if (context.Index <= i)
            {
                // This shouldn't happen, but we want to crash if it does.
                var message = "Infinite loop detected in the parser. Please open an issue.";
                throw new InvalidProgramException(message);
            }
        }
        
        // 如果 segments 有效，由 pattern factory 创建 pattern 并返回
        if (IsAllValid(context, segments))
        {
            return RoutePatternFactory.Pattern(pattern, segments);
        }
        // 否则，即 segments 无效，抛出异常
        else
        {
            throw new RoutePatternException(pattern, context.Error);
        }
    }
        
    // 判断 segment 有效
    private static bool IsAllValid(
        Context context, 
        List<RoutePatternPathSegment> segments)
    {
        // A catch-all parameter must be the last part of the last segment
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            for (var j = 0; j < segment.Parts.Count; j++)
            {
                var part = segment.Parts[j];
                if (part is RoutePatternParameterPart parameter && 
                    parameter.IsCatchAll &&
                    (i != segments.Count - 1 || j != segment.Parts.Count - 1))
                {
                    context.Error = Resources.TemplateRoute_CatchAllMustBeLast;
                    return false;
                }
            }
        }
        
        return true;
    }                        
    
    // 去掉 pattern string 的空格
    private static string TrimPrefix(string routePattern)
    {
        if (routePattern.StartsWith("~/", StringComparison.Ordinal))
        {
            return routePattern.Substring(2);
        }
        else if (routePattern.StartsWith("/", StringComparison.Ordinal))
        {
            return routePattern.Substring(1);
        }
        else if (routePattern.StartsWith("~", StringComparison.Ordinal))
        {
            throw new RoutePatternException(
                routePattern,
                Resources.TemplateRoute_InvalidRouteTemplate);
        }
        return routePattern;
    }                 
}

```

###### 2.1.4.1 (parse pattern) context

* 存储、截取 pattern string

```c#
internal static class RoutePatternParser
{    
    [DebuggerDisplay("{DebuggerToString()}")]
    private class Context
    {
        /* parameter name 集合，存储解析 parameter name 结果 */
        private readonly HashSet<string> _parameterNames = 
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        public HashSet<string> ParameterNames
        {
            get 
            { 
                return _parameterNames; 
            }
        }
        
        // pattern string
        private readonly string _template;     
        
        private int _index;
        private int? _mark;  
        
        public int Index => _index;
        public char Current
        {
            get 
            { 
                return (_index < _template.Length && 
                        _index >= 0) 
                    		? _template[_index] 
                    		: (char)0; 
            }
        }
        public string Error { get; set; }
                        
        // 构造函数，注入 pattern string，index=-1
        public Context(string template)
        {
            Debug.Assert(template != null);
            
            _template = template;            
            _index = -1;
        }
                                        
        public bool Back()
        {
            return --_index >= 0;
        }
        
        public bool AtEnd()
        {
            return _index >= _template.Length;
        }
        
        public bool MoveNext()
        {
            return ++_index < _template.Length;
        }
                                                                                               
        public void Mark()
        {
            Debug.Assert(_index >= 0);
            
            // Index is always the index of the character *past* Current 
            // - we want to 'mark' Current.
            _mark = _index;
        }
        
        public string Capture()
        {
            if (_mark.HasValue)
            {
                var value = _template.Substring(_mark.Value, _index - _mark.Value);
                _mark = null;
                return value;
            }
            else
            {
                return null;
            }
        }
        
        private string DebuggerToString()
        {
            if (_index == -1)
            {
                return _template;
            }
            else if (_mark.HasValue)
            {
                return _template.Substring(0, _mark.Value) +
	                   "|" +
    	               _template.Substring(_mark.Value, _index - _mark.Value) +
        	           "|" +
            	       _template.Substring(_index);
            }
            else
            {
                return _template.Substring(0, _index) + 
	                   "|" + 
    	               _template.Substring(_index);
            }
        }
    }
}
```

###### 2.1.4.2 parse & valid segment

```c#
internal static class RoutePatternParser
{
    private static bool ParseSegment(
        Context context, 
        List<RoutePatternPathSegment> segments)
    {
        Debug.Assert(context != null);
        Debug.Assert(segments != null);
        
        // 创建 route pattern part 集合
        var parts = new List<RoutePatternPart>();
        
        while (true)
        {
            var i = context.Index;
            
            // 如果是“{”，
            if (context.Current == OpenBrace)
            {
                // string 已经 end，即单独“{”，-> 错误
                if (!context.MoveNext())
                {
                    // This is a dangling open-brace, which is not allowed
                    context.Error = Resources.TemplateRoute_MismatchedParameter;
                    return false;
                }
                // 下一个 char 是“{”，
                if (context.Current == OpenBrace)
                {
                    // 解析 literal，保存到 parts，
                    // 如果解析失败 -> 错误
                    // This is an 'escaped' brace in a literal, like "{{foo"
                    context.Back();
                    if (!ParseLiteral(context, parts))
                    {
                        return false;
                    }
                }
                // 应该是 parameter，
                else
                {
                    // 解析 parameter，保存到 parts，
                    // 如果解析失败 -> 错误
                    // This is a parameter
                    context.Back();
                    if (!ParseParameter(context, parts))
                    {
                        return false;
                    }
                }
            }
            // 否则，即不是“{”，
            // 解析 literal part，保存到 parts，
            // 如果解析失败 -> 错误
            else
            {
                if (!ParseLiteral(context, parts))
                {
                    return false;
                }
            }
            // 如果是分隔符“/”或者 string 已经 end，-> 结束            
            if (context.Current == Separator || context.AtEnd())
            {
                // We've reached the end of the segment
                break;
            }
            
            if (context.Index <= i)
            {
                // This shouldn't happen, but we want to crash if it does.
                var message = "Infinite loop detected in the parser. Please open an issue.";
                throw new InvalidProgramException(message);
            }
        }
        
        // 如果 parts 合规，由 parts 创建 route pattern path segment，
        // 注入到 segment 
        if (IsSegmentValid(context, parts))
        {
            segments.Add(new RoutePatternPathSegment(parts));
            return true;
        }
        // 否则，返回 false
        else
        {
            return false;
        }
    }
    
    // 验证 parts 是否合规
    private static bool IsSegmentValid(
        Context context, 
        List<RoutePatternPart> parts)
    {
        /* 包含多个 part，且有 parameter part 标记为 catch all，错误 */           
        // If a segment has multiple parts, then it can't contain a catch all.
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            if (part is RoutePatternParameterPart parameter && 
                parameter.IsCatchAll && 
                parts.Count > 1)
            {
                context.Error = Resources.TemplateRoute_CannotHaveCatchAllInMultiSegment;
                return false;
            }
        }
        
        /* 包含多个 part，且有 parameter part 标记 optional。。。*/          
        // if a segment has multiple parts, then only the last one parameter can be optional
        // if it is following a optional seperator.
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            
            // 如果是 parameter part 且标记为 optional
            if (part is RoutePatternParameterPart parameter && 
                parameter.IsOptional && 
                parts.Count > 1)
            {
                /* 如果上述 parameter part 是最后一个（可选参数必须在最后），*/
                // This optional parameter is the last part in the segment
                if (i == parts.Count - 1)
                {
                    // 解析之前一个 parameter part
                    var previousPart = parts[i - 1];
                    
                    /* 如果 pre part 是 parameter part -> 错误 */                               
                    /* parameter part 之间只能用“.”连接 */                       
                    if (!previousPart.IsLiteral && 
                        !previousPart.IsSeparator)
                    {
                        // The optional parameter is preceded by something 
                        // that is not a literal or separator
                        // Example of error message:
                        // "In the segment '{RouteValue}{param?}', 
                        // the optional parameter 'param' is preceded 
                        // by an invalid segment '{RouteValue}'. 
                        // Only a period (.) can precede an optional parameter.
                        context.Error = 
                            Resources.FormatTemplateRoute
                            		  _OptionalParameterCanbBePrecededByPeriod(
                            			  RoutePatternPathSegment.DebuggerToString(parts),
                            			  parameter.Name,
                            			  parts[i - 1].DebuggerToString());
                        
                        return false;
                    }
                    /* 如果 pre part 是 literal part，但不是“."字符 -> 错误 */
                    /* parameter part 之间只能用“.”连接 */      
                    else if (previousPart is RoutePatternLiteralPart literal && 
                             literal.Content != PeriodString)
                    {
                        // The optional parameter is preceded by a literal other than period.
                        // Example of error message:
                        // "In the segment '{RouteValue}-{param?}', 
                        // the optional parameter 'param' is preceded
                        // by an invalid segment '-'. 
                        // Only a period (.) can precede an optional parameter.
                        context.Error = 
                            Resources.FormatTemplateRoute
		                              _OptionalParameterCanbBePrecededByPeriod(
          			                      RoutePatternPathSegment.DebuggerToString(parts),
                    			          parameter.Name,
                                		  parts[i - 1].DebuggerToString());
                        
                        return false;
                    }
                    
                    /* pre part 是 separator part（“."），
                       使用 route pattern factory 创建 separator part 并注入 "*/                 
                    parts[i - 1] = 
                        RoutePatternFactory.SeparatorPart(
                        	((RoutePatternLiteralPart)previousPart).Content);
                }
                /* 否则，即标记为 optional 的 parameter part 不是最后一个 -> 错误 */               
                else
                {
                    // This optional parameter is not the last one in the segment
                    // Example:
                    // An optional parameter must be at the end of the segment. 
                    // In the segment '{RouteValue?})',
                    // optional parameter 'RouteValue' is followed by ')'
                    context.Error = 
                        Resources.FormatTemplateRoute
		                          _OptionalParameterHasTobeTheLast(
          			                  RoutePatternPathSegment.DebuggerToString(parts),
                    			      parameter.Name,
		                              parts[i + 1].DebuggerToString());
                    
                    return false;
                }
            }
        }
        
        /* 如果有不止一个 parameter part -> 错误 */
        /* 一个 segment 只能有一个 parameter part */        
        var isLastSegmentParameter = false;
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            if (part.IsParameter && isLastSegmentParameter)
            {
                context.Error = Resources
                    .TemplateRoute_CannotHaveConsecutiveParameters;
                return false;
            }
            
            isLastSegmentParameter = part.IsParameter;
        }
        
        /* 没有上述情况，合规 */
        return true;
    }
}

```

###### 2.1.4.3 parse & valid paramter

```c#
internal static class RoutePatternParser
{
    private static bool ParseParameter(
        Context context, 
        List<RoutePatternPart> parts)
    {
        // 下一个，即保证去掉了代表 parameter part 开始的“{”
        Debug.Assert(context.Current == OpenBrace);
        context.Mark();        
        context.MoveNext();
        
        while (true)
        {
            // 如果是“{”
            if (context.Current == OpenBrace)
            {
                /* 没有 end 。。。*/
                // This is an open brace inside of a parameter, it has to be escaped
                if (context.MoveNext())
                {
                    /* 下一个字符不是“{”，即 包含单独的“{” -> 错误 */
                    if (context.Current != OpenBrace)
                    {
                        // If we see something like "{p1:regex(^\d{3", we will come here.
                        context.Error = Resources.TemplateRoute_UnescapedBrace;
                        return false;
                    }
                }
                /* 已经 end 。。。，即包含“}“ -> 错误 */
                else
                {
                    // This is a dangling open-brace, which is not allowed
                    // Example: "{p1:regex(^\d{"
                    context.Error = Resources.TemplateRoute_MismatchedParameter;
                    return false;
                }
            }
            // 如果是“}”
            else if (context.Current == CloseBrace)
            {
                /* 已经 end 。。。，退出 */
                // When we encounter Closed brace here, 
                // it either means end of the parameter or it is a closed
                // brace in the parameter, in that case it needs to be escaped.
                // Example: {p1:regex(([}}])\w+}. 
                // First pair is escaped one and last marks end of the parameter
                if (!context.MoveNext())
                {
                    // This is the end of the string -and we have a valid parameter
                    break;
                }
                /* 没有 end，且下一个字符（在上面代码中已经 move next）是“}”，
                   即包含“}}”，继续 */
                if (context.Current == CloseBrace)
                {
                    // This is an 'escaped' brace in a parameter name
                }
                /* 否则，即已经 end，且只有一个“}”，结束*/
                else
                {
                    // This is the end of the parameter
                    break;
                }
            }
            
            /* 已经 end，包含单独的“}” -> 错误 */
            if (!context.MoveNext())
            {
                // This is a dangling open-brace, which is not allowed
                context.Error = Resources.TemplateRoute_MismatchedParameter;
                return false;
            }
        }
        
        // 截取 context 中的 string text        
        var text = context.Capture();
        // 如果是“{}” -> 错误
        if (text == "{}")
        {
            context.Error = Resources
                .FormatTemplateRoute_InvalidParameterName(string.Empty);
            return false;
        }
        // 截取 parameter text，
        // 因为上面代码已经 move next
        var inside = text.Substring(1, text.Length - 2);
        // 替换“{{”和“}}”
        var decoded = inside.Replace("}}", "}").Replace("{{", "{");
        
        /* 使用 route parameter parser 解析 parameter part */
        // At this point, we need to parse the raw name for inline constraint,
        // default values and optional parameters.
        var templatePart = RouteParameterParser.ParseRouteParameter(decoded);
        
        // See #475 - this is here because InlineRouteParameterParser can't return errors
        
        /* 如果同时标记了通配符“*”和“？” -> 错误 */
        if (decoded.StartsWith("*", StringComparison.Ordinal) && 
            decoded.EndsWith("?", StringComparison.Ordinal))
        {
            context.Error = 
                Resources.TemplateRoute_CatchAllCannotBeOptional;
            return false;
        }
        /* 如果解析到 parameter part 是 optional 但是没有 default -> 错误 */
        if (templatePart.IsOptional && 
            templatePart.Default != null)
        {
            // Cannot be optional and have a default value.
            // The only way to declare an optional parameter is to have a ? at the end,
            // hence we cannot have both default value 
            // and optional parameter within the template.
            // A workaround is to add it as a separate entry in the defaults argument.
            context.Error = 
                Resources.TemplateRoute_OptionalCannotHaveDefaultValue;
            return false;
        }
                
        // 验证 parameter name
        var parameterName = templatePart.Name;        
        if (IsValidParameterName(context, parameterName))
        {
            // 合规，注入 parts
            parts.Add(templatePart);
            return true;
        }
        // parameter name 不合规，错误
        else
        {
            return false;
        }
    }
    
    // 验证 parameter name
    private static bool IsValidParameterName(
        Context context, 
        string parameterName)
    {
        // 如果 parameter name 为 empty，或者包含 invalid char -> 错误
        if (parameterName.Length == 0 || 
            parameterName.IndexOfAny(InvalidParameterNameChars) >= 0)
        {
            context.Error = 
                Resources.FormatTemplateRoute_InvalidParameterName(parameterName);
            return false;
        }
        // 如果 parameter name 无法注入 context，即有重名 parameter name -> 错误
        if (!context.ParameterNames.Add(parameterName))
        {
            context.Error = 
                Resources.FormatTemplateRoute_RepeatedParameter(parameterName);
            return false;
        }
        
        return true;
    }
}

```

###### 2.1.4.4 parse & valid literal

```c#
internal static class RoutePatternParser
{
    private static bool ParseLiteral(
        Context context, 
        List<RoutePatternPart> parts)
    {
        // 下一个，即保证去掉了代表 literal part 开头的“/“
        context.Mark();
        
        while (true)
        {
            /* 如果是“/”，结束 */
            if (context.Current == Separator)
            {
                // End of the segment
                break;
            }
            /* 如果是“{”，*/
            else if (context.Current == OpenBrace)
            {
                /* 如果 end，错误 */
                if (!context.MoveNext())
                {
                    // This is a dangling open-brace, which is not allowed
                    context.Error = Resources.TemplateRoute_MismatchedParameter;
                    return false;
                }
                /* 如果是“{”（已经 move next），即包含“{{”，继续 */
                if (context.Current == OpenBrace)
                {
                    // This is an 'escaped' brace in a literal, like "{{foo" - keep going.
                }
                /* 以“{”开始，说明是 parameter，context 回退（为了转向 parameter 解析）*/
                else
                {
                    // We've just seen the start of a parameter, so back up.
                    context.Back();
                    break;
                }
            }
            /* 如果是“}“*/
            else if (context.Current == CloseBrace)
            {
                /* 如果 end，错误 */
                if (!context.MoveNext())
                {
                    // This is a dangling close-brace, which is not allowed
                    context.Error = Resources.TemplateRoute_MismatchedParameter;
                    return false;
                }
                /* 如果是“}”（已经 move next），即包含“}}”，继续 */
                if (context.Current == CloseBrace)
                {
                    // This is an 'escaped' brace in a literal, like "{{foo" - keep going. 
                }
                /* 仅有一个“}”，错误 */
                else
                {
                    // This is an unbalanced close-brace, which is not allowed
                    context.Error = Resources.TemplateRoute_MismatchedParameter;
                    return false;
                }
            }
            /* 如果 end，结束 */
            if (!context.MoveNext())
            {
                break;
            }
        }
        
        // 获取 literal 字符，替换 “{{“、”}}“
        var encoded = context.Capture();
        var decoded = encoded.Replace("}}", "}").Replace("{{", "{");
        
        // 检查 valid
        if (IsValidLiteral(context, decoded))
        {
            parts.Add(RoutePatternFactory.LiteralPart(decoded));
            return true;
        }
        else
        {
            return false;
        }
    }
    
    // 验证 literal
    private static bool IsValidLiteral(Context context, string literal)
    {
        Debug.Assert(context != null);
        Debug.Assert(literal != null);
        
        // 如果包含“？”，错误
        if (literal.IndexOf(QuestionMark) != -1)
        {
            context.Error = Resources.FormatTemplateRoute_InvalidLiteral(literal);
            return false;
        }
        
        return true;
    }
}

```

##### 2.1.5 route pattern factory

* 创建`route pattern`
  * pattern 方法：直接由构造构造函数创建
  * parse 方法：调用`route pattern parser`解析 segments，然后注入其他元素如 defaults
* 创建 route pattern 元素

```c#
public static class RoutePatternFactory
{
    // empty dictionary，default、required value 的默认值
    private static readonly IReadOnlyDictionary<string, object?> 
        EmptyDictionary = 
        	new ReadOnlyDictionary<string, object?>(
        		new Dictionary<string, object?>());
    
    // empty parameter policy reference dictionary
    private static readonly IReadOnlyDictionary<
        string, 
    	IReadOnlyList<RoutePatternParameterPolicyReference>> 
            EmptyPoliciesDictionary =
            	new ReadOnlyDictionary<
            		string, 
    				IReadOnlyList<RoutePatternParameterPolicyReference>>(
                        new Dictionary<string, 
                        ReadOnlyList<RoutePatternParameterPolicyReference>>());                                                   
    /* 通过构造函数创建 route pattern */
    private static RoutePattern PatternCore(
        string? rawText,
        RouteValueDictionary? defaults,
        RouteValueDictionary? parameterPolicies,
        RouteValueDictionary? requiredValues,
        IEnumerable<RoutePatternPathSegment> segments)
    {
        // We want to merge the segment data with the 'out of line' defaults 
        // and parameter policies.
        //
        // This means that for parameters that have 'out of line' defaults we will modify
        // the parameter to contain the default (same story for parameter policies).
        //
        // We also maintain a collection of defaults and parameter policies that will also
        // contain the values that don't match a parameter.
        //
        // It's important that these two views of the data are consistent. We don't want
        // values specified out of line to have a different behavior.
        
        /* a- 注入 default values */
        
        // 创建 updated defaults 集合（预结果）
        Dictionary<string, object?>? updatedDefaults = null;        
        // 如果参数 defaults 有效（不为 null 且不为 empty）
        if (defaults != null && 
            defaults.Count > 0)
        {
            updatedDefaults = new Dictionary<string, object?>(
                defaults.Count, 
                StringComparer.OrdinalIgnoreCase);
            
            // 遍历参数 defaults，将值注入 updated defaults 
            foreach (var kvp in defaults)
            {
                updatedDefaults.Add(kvp.Key, kvp.Value);
            }
        }
        
        /* b- 注入 parameter policies */
        
        // 创建 updated parameter policy reference 集合（预结果）
        Dictionary<string, 
        		   List<RoutePatternParameterPolicyReference>>? 
        	updatedParameterPolicies = null;
        // 如果参数 policies 有效（不为 null 且不为 empty）
        if (parameterPolicies != null && 
            parameterPolicies.Count > 0)
        {
            updatedParameterPolicies = 
                new Dictionary<string, 
            				   List<RoutePatternParameterPolicyReference>>(
                                   parameterPolicies.Count, 
                                   StringComparer.OrdinalIgnoreCase);
            // 遍历参数 parameter policies，将有效值注入 updated policies
            foreach (var kvp in parameterPolicies)
            {
                var policyReferences = new List<RoutePatternParameterPolicyReference>();
                
                if (kvp.Value is IParameterPolicy parameterPolicy)
                {
                    policyReferences.Add(ParameterPolicy(parameterPolicy));
                }
                else if (kvp.Value is string)
                {
                    // Constraint will convert string values into regex constraints
                    policyReferences.Add(Constraint(kvp.Value));
                }
                else if (kvp.Value is IEnumerable multiplePolicies)
                {
                    foreach (var item in multiplePolicies)
                    {
                        // Constraint will convert string values into regex constraints
                        policyReferences.Add(
                            item is IParameterPolicy p 
                            	? ParameterPolicy(p) 
                            	: Constraint(item));
                    }
                }
                else
                {
                    throw new InvalidOperationException(
                        Resources.FormatRoutePattern_InvalidConstraintReference(
                            kvp.Value ?? "null",
                            typeof(IRouteConstraint)));
                }
                
                updatedParameterPolicies.Add(kvp.Key, policyReferences);
            }
        }
        
        /* c- 注入 segment 和 parameter */
        
        // 创建 parameter part 集合（预结果）
        List<RoutePatternParameterPart>? parameters = null;
        // 创建 segment 集合（预结果），初始为参数 segments 数组
        var updatedSegments = segments.ToArray();
        
        // 遍历参数 segment 数组
        for (var i = 0; i < updatedSegments.Length; i++)
        {
            // 刷新 segment
            var segment = VisitSegment(updatedSegments[i]);
            // 注入 updated segment
            updatedSegments[i] = segment;
            
            // 遍历 segment 中的 part
            for (var j = 0; j < segment.Parts.Count; j++)
            {
                // 将 parameter part 注入 parameter 集合（预结果）
                if (segment.Parts[j] is RoutePatternParameterPart parameter)
                {
                    if (parameters == null)
                    {
                        parameters = new List<RoutePatternParameterPart>();
                    }
                    
                    parameters.Add(parameter);
                }
            }
        }
        
        /* 检查参数 required value 是否有效，
           如果无效，抛出异常*/
        // Each Required Value either needs to either:
        // 1. be null-ish
        // 2. have a corresponding parameter
        // 3. have a corrsponding default that matches both key and value
        if (requiredValues != null)
        {
            foreach (var kvp in requiredValues)
            {
                // 1.be null-ish
                var found = RouteValueEqualityComparer
                    .Default
                    .Equals(string.Empty, kvp.Value);
                
                // 2. have a corresponding parameter
                if (!found && parameters != null)
                {
                    for (var i = 0; i < parameters.Count; i++)
                    {
                        if (string.Equals(
                            	kvp.Key, 
                            	parameters[i].Name, 
                            	StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    }
                }
                
                // 3. have a corrsponding default that matches both key and value
                if (!found &&
                    updatedDefaults != null &&
                    updatedDefaults.TryGetValue(kvp.Key, out var defaultValue) &&
                    RouteValueEqualityComparer.Default
                    						  .Equals(kvp.Value, defaultValue))
                {
                    found = true;
                }
                
                if (!found)
                {
                    throw new InvalidOperationException(
                        $"No corresponding parameter or default value 
                        "could be found for the required value " +
                        $"'{kvp.Key}={kvp.Value}'. A non-null required value 
                        "must correspond to a route parameter or the " +
                        $"route pattern must have a matching default value.");
                }
            }
        }
        
        // 创建 route pattern
        return new RoutePattern(
            // string
            rawText,
            // default values
            updatedDefaults ?? EmptyDictionary,
            // parameter policy
            updatedParameterPolicies != null
            	? updatedParameterPolicies
            		.ToDictionary(
                    	kvp => kvp.Key, 
                    	kvp => 
                    		(IReadOnlyList<RoutePatternParameterPolicyReference>)
                    			kvp.Value.ToArray())
            	: EmptyPoliciesDictionary,
            // required value
            requiredValues ?? EmptyDictionary,
            // parameter
            (IReadOnlyList<RoutePatternParameterPart>?)parameters 
            	?? Array.Empty<RoutePatternParameterPart>(),
            // segment
            updatedSegments);
                
        /* visit segment 方法 */
        RoutePatternPathSegment VisitSegment(RoutePatternPathSegment segment)
        {
            RoutePatternPart[]? updatedParts = null;
            for (var i = 0; i < segment.Parts.Count; i++)
            {
                var part = segment.Parts[i];
                var updatedPart = VisitPart(part);
                
                if (part != updatedPart)
                {
                    if (updatedParts == null)
                    {
                        updatedParts = segment.Parts.ToArray();
                    }
                    
                    updatedParts[i] = updatedPart;
                }
            }
            
            if (updatedParts == null)
            {
                // Segment has not changed
                return segment;
            }
            
            return new RoutePatternPathSegment(updatedParts);
        }
        
        /* visit part 方法 */
        RoutePatternPart VisitPart(RoutePatternPart part)
        {
            if (!part.IsParameter)
            {
                return part;
            }
            
            var parameter = (RoutePatternParameterPart)part;
            var @default = parameter.Default;
            
            if (updatedDefaults != null && 
                updatedDefaults.TryGetValue(parameter.Name, out var newDefault))
            {
                if (parameter.Default != null && 
                    !Equals(newDefault, parameter.Default))
                {
                    var message = Resources
                        .FormatTemplateRoute
                        _CannotHaveDefaultValueSpecifiedInlineAndExplicitly(parameter.Name);   
                    throw new InvalidOperationException(message);
                }
                
                if (parameter.IsOptional)
                {
                    var message = Resources
                        .TemplateRoute_OptionalCannotHaveDefaultValue;
                    throw new InvalidOperationException(message);
                }
                
                @default = newDefault;
            }
            
            if (parameter.Default != null)
            {
                if (updatedDefaults == null)
                {
                    updatedDefaults = 
                        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                }
                
                updatedDefaults[parameter.Name] = parameter.Default;
            }
            
            List<RoutePatternParameterPolicyReference>? parameterConstraints = null;
            if ((updatedParameterPolicies == null || 
                 !updatedParameterPolicies.TryGetValue(
                     parameter.Name, 
                     out parameterConstraints)) && 
                parameter.ParameterPolicies.Count > 0)
            {
                if (updatedParameterPolicies == null)
                {
                    updatedParameterPolicies = 
                        new Dictionary<
                        	string, 
                    		List<RoutePatternParameterPolicyReference>>
                        (StringComparer.OrdinalIgnoreCase);
                }
                
                parameterConstraints = 
                    new List<RoutePatternParameterPolicyReference>
                    	(parameter.ParameterPolicies.Count);
                
                updatedParameterPolicies.Add(
                    parameter.Name, 
                    parameterConstraints);
            }
            
            if (parameter.ParameterPolicies.Count > 0)
            {
                parameterConstraints!.AddRange(parameter.ParameterPolicies);
            }
            
            if (Equals(parameter.Default, @default) && 
                parameter.ParameterPolicies.Count == 0 && 
                (parameterConstraints?.Count ?? 0) == 0)
            {
                // Part has not changed
                return part;
            }
            
            return ParameterPartCore(
                parameter.Name,
                @default,
                parameter.ParameterKind,    
                parameterConstraints?.ToArray() 
                	?? Array.Empty<RoutePatternParameterPolicyReference>(),
                parameter.EncodeSlashes);
        }
    }                  
}

```

###### 2.1.5.1 pattern 方法

* 创建`route pattern`

```c#
public static class RoutePatternFactory
{
    public static RoutePattern Pattern(
        IEnumerable<RoutePatternPathSegment> segments)
    {
        if (segments == null)
        {
            throw new ArgumentNullException(nameof(segments));
        }
        
        return PatternCore(
            null, 
            null, 
            null, 
            null, 
            segments);
    }
    
    public static RoutePattern Pattern(
        string? rawText, 
        IEnumerable<RoutePatternPathSegment> segments)
    {
        if (segments == null)
        {
            throw new ArgumentNullException(nameof(segments));
        }
        
        return PatternCore(
            rawText, 
            null, 
            null, 
            null, 
            segments);
    }
        
    public static RoutePattern Pattern(
        object? defaults,
        object? parameterPolicies,
        IEnumerable<RoutePatternPathSegment> segments)
    {
        if (segments == null)
        {
            throw new ArgumentNullException(nameof(segments));
        }
        
        return PatternCore(
            null, 
            new RouteValueDictionary(defaults), 
            new RouteValueDictionary(parameterPolicies), 
            requiredValues: null, 
            segments);
    }
    
    public static RoutePattern Pattern(
        string? rawText,
        object? defaults,
        object? parameterPolicies,
        IEnumerable<RoutePatternPathSegment> segments)
    {
        if (segments == null)
        {
            throw new ArgumentNullException(nameof(segments));
        }
        
        return PatternCore(
            rawText, 
            new RouteValueDictionary(defaults), 
            new RouteValueDictionary(parameterPolicies), 
            requiredValues: null, 
            segments);
    }
           
    public static RoutePattern Pattern(
        params RoutePatternPathSegment[] segments)
    {
        if (segments == null)
        {
            throw new ArgumentNullException(nameof(segments));
        }
        
        return PatternCore(
            null, 
            null, 
            null, 
            requiredValues: null, 
            segments);
    }
    
    public static RoutePattern Pattern(
        string rawText, 
        params RoutePatternPathSegment[] segments)
    {
        if (segments == null)
        {
            throw new ArgumentNullException(nameof(segments));
        }
        
        return PatternCore(
            rawText, 
            null, 
            null, 
            requiredValues: null, 
            segments);
    }
            
    public static RoutePattern Pattern(
        object? defaults,
        object? parameterPolicies,
        params RoutePatternPathSegment[] segments)
    {
        if (segments == null)
        {
            throw new ArgumentNullException(nameof(segments));
        }
        
        return PatternCore(
            null, 
            new RouteValueDictionary(defaults), 
            new RouteValueDictionary(parameterPolicies), 
            requiredValues: null, 
            segments);
    }
    
    public static RoutePattern Pattern(
        string? rawText,
        object? defaults,
        object? parameterPolicies,
        params RoutePatternPathSegment[] segments)
    {
        if (segments == null)
        {
            throw new ArgumentNullException(nameof(segments));
        }
        
        return PatternCore(
            rawText, 
            new RouteValueDictionary(defaults), 
            new RouteValueDictionary(parameterPolicies), 
            requiredValues: null, 
            segments);
    }
}

```

###### 2.1.5.2 parse 方法

* 由`route pattern parse`解析`segment`，
* 然后注入其他元素

```c#
public static class RoutePatternFactory
{
    private static RouteValueDictionary? Wrap(object? values)
    {
        return values == null ? 
            null : 
        	new RouteValueDictionary(values);
    }                           
    
    public static RoutePattern Parse(string pattern)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }
        
        return RoutePatternParser.Parse(pattern);
    }
        
    public static RoutePattern Parse(
        string pattern, 
        object? defaults, 
        object? parameterPolicies)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }
        
        var original = RoutePatternParser.Parse(pattern);
        return PatternCore(
            original.RawText, 
            Wrap(defaults), 
            Wrap(parameterPolicies), 
            requiredValues: null, 
            original.PathSegments);
    }
        
    public static RoutePattern Parse(
        string pattern, 
        object? defaults, 
        object? parameterPolicies, 
        object? requiredValues)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }
        
        var original = RoutePatternParser.Parse(pattern);
        return PatternCore(
            original.RawText, 
            Wrap(defaults), 
            Wrap(parameterPolicies), 
            Wrap(requiredValues), 
            original.PathSegments);
    }
}

```

###### 2.1.5.3 pattern 元素 - 创建 segment

```c#
public static class RoutePatternFactory
{    
    // 真正创建 segment 的方法
    private static RoutePatternPathSegment SegmentCore(
        RoutePatternPart[] parts)
    {
        return new RoutePatternPathSegment(parts);
    }
    
    /* 扩展的构建方法 */
    
    public static RoutePatternPathSegment Segment(
        params RoutePatternPart[] parts)
    {
        if (parts == null)
        {
            throw new ArgumentNullException(nameof(parts));
        }
        
        return SegmentCore((RoutePatternPart[])parts.Clone());        
    }
            
    public static RoutePatternPathSegment Segment(
        IEnumerable<RoutePatternPart> parts)
    {
        if (parts == null)
        {
            hrow new ArgumentNullException(nameof(parts));
            
        }
        
        return SegmentCore(parts.ToArray());
    }
}

```

###### 2.1.5.4 pattern 元素 -  创建 literal part

```c#
public static class RoutePatternFactory
{
    // 真正创建 literal part 的方法
    private static RoutePatternLiteralPart LiteralPartCore(string content)
    {
        return new RoutePatternLiteralPart(content);
    }
    
    /* 扩展的构建方法 */
    public static RoutePatternLiteralPart LiteralPart(string content)
    {        
        if (string.IsNullOrEmpty(content))
        {
            throw new ArgumentException(
                Resources.Argument_NullOrEmpty, 
                nameof(content));
        }        
        // string 中包含“？”，抛出异常
        if (content.IndexOf('?') >= 0)
        {
            throw new ArgumentException(
                Resources.FormatTemplateRoute_InvalidLiteral(content));
        }
        
        return LiteralPartCore(content);
    }        
}

```

###### 2.1.5.5 pattern 元素 - 创建 separator part

```c#
public static class RoutePatternFactory
{
    // 真正创建 separator part 的方法
    private static RoutePatternSeparatorPart SeparatorPartCore(string content)
    {
        return new RoutePatternSeparatorPart(content);
    }
    
    /* 扩展的构建方法 */
    public static RoutePatternSeparatorPart SeparatorPart(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            throw new ArgumentException(
                Resources.Argument_NullOrEmpty, 
                nameof(content));
        }
        
        return SeparatorPartCore(content);
    }        
}

```

###### 2.1.5.6 pattern 元素 - 创建 parameter part

```c#
public static class RoutePatternFactory
{
    /* 真正创建 parameter part 的方法 */
    
    private static RoutePatternParameterPart ParameterPartCore(
        string parameterName,
        object? @default,
        RoutePatternParameterKind parameterKind,
        RoutePatternParameterPolicyReference[] parameterPolicies)
    {
        return ParameterPartCore(
            parameterName, 
            @default, 
            parameterKind, 
            parameterPolicies, 
            encodeSlashes: true);
    }
    
    private static RoutePatternParameterPart ParameterPartCore(
        string parameterName,
        object? @default,
        RoutePatternParameterKind parameterKind,
        RoutePatternParameterPolicyReference[] parameterPolicies,
        bool encodeSlashes)
    {
        return new RoutePatternParameterPart(
            parameterName,
            @default,
            parameterKind,
            parameterPolicies,
            encodeSlashes);
    }
    
    /* 扩展的构建方法 */
        
    public static RoutePatternParameterPart ParameterPart(
        string parameterName,
        object? @default,
        RoutePatternParameterKind parameterKind,
        params RoutePatternParameterPolicyReference[] parameterPolicies)
    {        
        // parameter name 为空，抛出异常
        if (string.IsNullOrEmpty(parameterName))
        {
            throw new ArgumentException(
                Resources.Argument_NullOrEmpty, 
                nameof(parameterName));
        }
        
        // parameter name 包含不合法字符，抛出异常
        if (parameterName.IndexOfAny(
            	RoutePatternParser.InvalidParameterNameChars) >= 0)
        {
            throw new ArgumentException(
                Resources
                	.FormatTemplateRoute_InvalidParameterName(parameterName));
        }
        
        // 注入了 default value，但是 parameter kind 是 optional，抛出异常
        if (@default != null && 
            parameterKind == RoutePatternParameterKind.Optional)
        {
            throw new ArgumentNullException(
                Resources.TemplateRoute_OptionalCannotHaveDefaultValue, 
                nameof(parameterKind));
        }
        
        if (parameterPolicies == null)
        {
            throw new ArgumentNullException(nameof(parameterPolicies));
        }
        
        return ParameterPartCore(
            parameterName: parameterName,
            @default: @default,
            parameterKind: parameterKind,
            parameterPolicies: 
            	(RoutePatternParameterPolicyReference[])
            		parameterPolicies.Clone());
    }
            
    public static RoutePatternParameterPart ParameterPart(
        string parameterName,
        object? @default,
        RoutePatternParameterKind parameterKind,
        IEnumerable<RoutePatternParameterPolicyReference> parameterPolicies)
    {
        // parameter name 为空，抛出异常
        if (string.IsNullOrEmpty(parameterName))
        {
            throw new ArgumentException(
                Resources.Argument_NullOrEmpty, 
                nameof(parameterName));
        }
        // parameter name 包含不合法字符，抛出异常
        if (parameterName.IndexOfAny(
            RoutePatternParser.InvalidParameterNameChars) >= 0)
        {
            throw new ArgumentException(
                Resources
                	.FormatTemplateRoute_InvalidParameterName(parameterName));
        }
        // 注入了 default value，但是 parameter kind 是 optional，抛出异常
        if (@default != null && 
            parameterKind == RoutePatternParameterKind.Optional)
        {
            throw new ArgumentNullException(
                Resources.TemplateRoute_OptionalCannotHaveDefaultValue, 
                nameof(parameterKind));
        }
        
        if (parameterPolicies == null)
        {
            throw new ArgumentNullException(nameof(parameterPolicies));
        }
        
        return ParameterPartCore(
            parameterName: parameterName,
            @default: @default,
            parameterKind: parameterKind,
            parameterPolicies: parameterPolicies.ToArray());
    }
    
    public static RoutePatternParameterPart ParameterPart(
        string parameterName,
        object? @default,
        RoutePatternParameterKind parameterKind)
    {
        // parameter name 为空，抛出异常
        if (string.IsNullOrEmpty(parameterName))
        {
            throw new ArgumentException(
                Resources.Argument_NullOrEmpty, 
                nameof(parameterName));
        }
        
        // parameter name 包含不合法字符，抛出异常
        if (parameterName.IndexOfAny(
            RoutePatternParser.InvalidParameterNameChars) >= 0)
        {
            throw new ArgumentException(
                Resources
                	.FormatTemplateRoute_InvalidParameterName(parameterName));
        }
        // 注入了 default value，但是 parameter kind 是 optional，抛出异常
        if (@default != null && 
            parameterKind == RoutePatternParameterKind.Optional)
        {
            throw new ArgumentNullException(
                Resources.TemplateRoute_OptionalCannotHaveDefaultValue, 
                nameof(parameterKind));
        }
        
        return ParameterPartCore(
            parameterName: parameterName,
            @default: @default,
            parameterKind: parameterKind,
            // empty parameter policy reference 集合
            parameterPolicies: Array.Empty<RoutePatternParameterPolicyReference>());
    }
    
    public static RoutePatternParameterPart ParameterPart(
        string parameterName, 
        object @default)
    {
        if (string.IsNullOrEmpty(parameterName))
        {
            throw new ArgumentException(
                Resources.Argument_NullOrEmpty, 
                nameof(parameterName));
        }
        // 包含不合法字符，抛出异常
        if (parameterName.IndexOfAny(
            	RoutePatternParser.InvalidParameterNameChars) >= 0)
        {
            throw new ArgumentException(
                Resources
                	.FormatTemplateRoute_InvalidParameterName(parameterName));
        }
        
        return ParameterPartCore(
            parameterName: parameterName,
            @default: @default,
            parameterKind: RoutePatternParameterKind.Standard,
            parameterPolicies: Array.Empty<RoutePatternParameterPolicyReference>());
    }
    
    public static RoutePatternParameterPart ParameterPart(string parameterName)
    {
        if (string.IsNullOrEmpty(parameterName))
        {
            throw new ArgumentException(
                Resources.Argument_NullOrEmpty, 
                nameof(parameterName));
        }
        // 包含不合法字符，抛出异常
        if (parameterName.IndexOfAny(
            RoutePatternParser.InvalidParameterNameChars) >= 0)
        {
            throw new ArgumentException(
                Resources.FormatTemplateRoute_InvalidParameterName(parameterName));
        }
        
        return ParameterPartCore(
            parameterName: parameterName,
            @default: null,
            parameterKind: RoutePatternParameterKind.Standard,
            parameterPolicies: Array.Empty<RoutePatternParameterPolicyReference>());
    }
}

```

###### 2.1.5.7 pattern 元素 - 创建 parameter constraint

```c#
public static class RoutePatternFactory
{    
    // 由 parameter policy 字符串 创建 parameter policy reference 
    private static RoutePatternParameterPolicyReference ParameterPolicyCore(
        string parameterPolicy)
    {
        return 
            new RoutePatternParameterPolicyReference(parameterPolicy);
    }
    
    // 由 parameter policy 创建 创建 parameter policy reference 
    private static RoutePatternParameterPolicyReference ParameterPolicyCore(
        IParameterPolicy parameterPolicy)
    {
        return 
            new RoutePatternParameterPolicyReference(parameterPolicy);
    }
    
    /* parameter policy */
    
    public static RoutePatternParameterPolicyReference 
        ParameterPolicy(IParameterPolicy parameterPolicy)
    {
        if (parameterPolicy == null)
        {
            throw new ArgumentNullException(nameof(parameterPolicy));
        }
        
        return ParameterPolicyCore(parameterPolicy);
    }
        
    public static RoutePatternParameterPolicyReference 
        ParameterPolicy(string parameterPolicy)
    {
        if (string.IsNullOrEmpty(parameterPolicy))
        {
            throw new ArgumentException(
                Resources.Argument_NullOrEmpty, 
                nameof(parameterPolicy));
        }
        
        return ParameterPolicyCore(parameterPolicy);
    }                
    
    /* constraint */
    
    public static RoutePatternParameterPolicyReference 
        Constraint(object constraint)
    {
        // Similar to RouteConstraintBuilder
        if (constraint is IRouteConstraint policy)
        {
            return ParameterPolicyCore(policy);
        }
        else if (constraint is string content)
        {
            return ParameterPolicyCore(
                new RegexRouteConstraint("^(" + content + ")$"));
        }
        else
        {
            throw new InvalidOperationException(
                Resources
                	.FormatRoutePattern_InvalidConstraintReference(
                        constraint ?? "null",
                        typeof(IRouteConstraint)));
        }
    }
           
    public static RoutePatternParameterPolicyReference 
        Constraint(IRouteConstraint constraint)
    {
        if (constraint == null)
        {
            throw new ArgumentNullException(nameof(constraint));
        }
        
        return ParameterPolicyCore(constraint);
    }
        
    public static RoutePatternParameterPolicyReference 
        Constraint(string constraint)
    {
        if (string.IsNullOrEmpty(constraint))
        {
            throw new ArgumentException(
                Resources.Argument_NullOrEmpty, 
                nameof(constraint));
        }
        
        return ParameterPolicyCore(constraint);
    }
}

```

##### 2.1.6 route pattern matcher

```c#
internal class RoutePatternMatcher
{
    private const string SeparatorString = "/";
    private const char SeparatorChar = '/';
    private static readonly char[] Delimiters = new char[] { SeparatorChar };
    
    // Perf: This is a cache to avoid looking things up in 'Defaults' each request.
    private readonly bool[] _hasDefaultValue;
    private readonly object[] _defaultValues;
    
    public RoutePattern RoutePattern { get; }
    public RouteValueDictionary Defaults { get; }
            
    public RoutePatternMatcher(
        RoutePattern pattern,
        RouteValueDictionary defaults)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }
        
        /* 注入 route pattern、defaults(route value dictionary) */
        RoutePattern = pattern;
        Defaults = defaults ?? new RouteValueDictionary();
        
        /* 创建 default values 集合（默认值） */
        // Perf: cache the default value for each parameter (other than complex segments).
        _hasDefaultValue = new bool[RoutePattern.PathSegments.Count];
        _defaultValues = new object[RoutePattern.PathSegments.Count];
        
        /* 加载参数 route pattern 中 parameter default */
        for (var i = 0; i < RoutePattern.PathSegments.Count; i++)
        {
            var segment = RoutePattern.PathSegments[i];
            if (!segment.IsSimple)
            {
                continue;
            }
            
            var part = segment.Parts[0];
            if (!part.IsParameter)
            {
                continue;
            }
            
            var parameter = (RoutePatternParameterPart)part;
            if (Defaults.TryGetValue(parameter.Name, out var value))
            {
                _hasDefaultValue[i] = true;
                _defaultValues[i] = value;
            }
        }
    }    
}

```

###### 2.1.6.1 try match

```c#
internal class RoutePatternMatcher
{
    // path = request path
    public bool TryMatch(PathString path, RouteValueDictionary values)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }
        
        var i = 0;        
        // 将 path 分散成 path tokenizer(string segment 集合)
        var pathTokenizer = new PathTokenizer(path);
                
        // Perf: We do a traversal of the request-segments + route-segments twice.
        //
        // For most segment-types, we only really need to any work on one of the two passes.
        //
        // On the first pass, we're just looking to see if there's anything that would 
        // disqualify us from matching.
        // The most common case would be a literal segment that doesn't match.
        //
        // On the second pass, we're almost certainly going to match the URL, so go ahead 
        // and allocate the 'values' and start capturing strings. 
        
        
        /* 第一次遍历，验证 request path 符合 pattern */
        
        // 先遍历 path string（request），逐一对比 route pattern，
        // 即确保 request path 的内容匹配 route pattern        
        foreach (var stringSegment in pathTokenizer)
        {
            /* 如果 string segment 为 empty，返回 false */
            if (stringSegment.Length == 0)
            {
                return false;
            }
            
            /* 解析 route pattern 中的 path segment（pattern），*/
            var pathSegment = i >= RoutePattern.PathSegments.Count 
                				  ? null 
                				  : RoutePattern.PathSegments[i];
            
            /* 如果 path segment（pattern）为 null，
               但是 string segment (request) 不为 empty，
               即 request 提供了 pattern 没有定义的内容，-> 错误 */               
            if (pathSegment == null && 
                stringSegment.Length > 0)
            {
                // If pathSegment is null, then we're out of route segments. 
                // All we can match is the empty string.
                return false;
            }
                        
            /* 如果 path segment (pattern) 是 simple，
               包含的 parameter part 是 catchall，-> 结束，转到第二次遍历 。。。*/
            else if (pathSegment.IsSimple && 
                     pathSegment.Parts[0] is RoutePatternParameterPart parameter && 
                     parameter.IsCatchAll)
            {
                // Nothing to validate for a catch-all - it can match any string, 
                // including the empty string.
                //
                // Also, a catch-all has to be the last part, so we're done.
                break;
            }
            
            /* 否则，尝试解析 literal，
               如果解析失败 -> 错误 */            
            if (!TryMatchLiterals(i++, stringSegment, pathSegment))
            {
                return false;
            }
        }
        
        // 如果此时 i < route pattern 中 path segment 数量，
        // 说明 request path 没有提供全部 route pattern 定义的内容，        
        // 此时需要验证这些 pattern 中的内容（path segment）
        for (; i < RoutePattern.PathSegments.Count; i++)
        {
            /* 解析 path segment（pattern）*/
            // We've matched the request path so far, but still have remaining 
            // route segments. 
            // These need to be all single-part parameter segments with default values 
            // or else they won't match.
            var pathSegment = RoutePattern.PathSegments[i];
            Debug.Assert(pathSegment != null);
            
            /* 如果 path segment（pattern）是 complex，它肯定包含 literals，
               而此时 request path 没有提供相应内容，-> 错误 */
            if (!pathSegment.IsSimple)
            {
                // If the segment is a complex segment, it MUST contain literals, 
                // and we've parsed the full path so far, so it can't match.
                return false;
            }
            
            /* path segment 是 simple，part（pattern）是 literal 或者 separator，
               而此时 request path 没有提供相应内容，-> 错误 */
            var part = pathSegment.Parts[0];
            if (part.IsLiteral || part.IsSeparator)
            {
                // If the segment is a simple literal - which need the URL to 
                // provide a value, so we don't match.
                return false;
            }
            
            /* path segment 是 simple，part（pattern）是 parameter，
               如果 parameter part（pattern）是 optiona，结束 */
            var parameter = (RoutePatternParameterPart)part;
            if (parameter.IsCatchAll)
            {
                // Nothing to validate for a catch-all - it can match any string, 
                // including the empty string.
                //
                // Also, a catch-all has to be the last part, so we're done.
                break;
            }
            
            /* path segment 是 simple，part（pattern）是 parameter，
               parameter part 不是 optional，且没有 default，
               而此时 request path 没有提供相应内容，-> 错误 */
            // If we get here, this is a simple segment with a parameter. 
            // We need it to be optional, or for the defaults to have a value.
            if (!_hasDefaultValue[i] && 
                !parameter.IsOptional)
            {
                // There's no default for this (non-optional) parameter so it can't match.
                return false;
            }
        }
        
        /* 第二次遍历，解析 request path 中的参数值 */
        // At this point we've very likely got a match, so start capturing values for real.
        
        i = 0;
        
        // 遍历 path string（request），逐一对比 route pattern，
        // 即确保 request path 提供了符合 route pattern 定义的 value
        foreach (var requestSegment in pathTokenizer)
        {
            /* 解析 path segment（pattern）*/
            var pathSegment = RoutePattern.PathSegments[i++];
            
            // 尝试解析 value，成功 -> 结束
            if (SavePathSegmentsAsValues(i, values, requestSegment, pathSegment))
            {
                break;
            }
            
            // 尝试解析 complex path segment，如果失败 -> 错误
            if (!pathSegment.IsSimple)
            {                 
                if (!MatchComplexSegment(pathSegment, requestSegment.AsSpan(), values))
                {
                    return false;
                }
            }
        }
        
        // 如果此时 i < route pattern 中 path segment 数量，
        // 说明 request path 没有提供全部 route pattern 定义的内容，        
        // 此时需要验证这些 pattern 中的内容（path segment）
        for (; i < RoutePattern.PathSegments.Count; i++)
        {
            /* 解析 path segment（pattern）*/
            // We've matched the request path so far, but still have remaining 
            // route segments. 
            // We already know these are simple parameters that either have a default, 
            // or don't need to produce a value.
            var pathSegment = RoutePattern.PathSegments[i];
            Debug.Assert(pathSegment != null);
            Debug.Assert(pathSegment.IsSimple);
            
            /* 解析 part（pattern）*/
            var part = pathSegment.Parts[0];
            Debug.Assert(part.IsParameter);
            
            /* 如果 part 是 parameter part，
               part 标记为 optional 或者 has default value*/
            // It's ok for a catch-all to produce a null value
            if (part is RoutePatternParameterPart parameter && 
                (parameter.IsCatchAll || _hasDefaultValue[i]))
            {
                /* 解析 default value，注入 route value dictionary */
                // Don't replace an existing value with a null.
                var defaultValue = _defaultValues[i];
                if (defaultValue != null || !values.ContainsKey(parameter.Name))
                {
                    values[parameter.Name] = defaultValue;
                }
            }
        }
        
        /* 加载 default 集合其他 value 到 route value dictionary，
           即优先解析 request path 提供的 value，此时忽略 参数 defaults 中的同名 value */
        // Copy all remaining default values to the route data
        foreach (var kvp in Defaults)
        {
#if RVD_TryAdd
    		values.TryAdd(kvp.Key, kvp.Value);
#else
            if (!values.ContainsKey(kvp.Key))
            {
                values.Add(kvp.Key, kvp.Value);
            }
#endif
        }
        
        return true;
    }
}

```

###### 2.1.6.2 try match literal

```c#
internal class RoutePatternMatcher
{
    private bool TryMatchLiterals(
        int index, 
        StringSegment stringSegment, 
        RoutePatternPathSegment pathSegment)
    {
        /* path segment (pattern) 是 simple，part 不是 parameter part */
        if (pathSegment.IsSimple && 
            !pathSegment.Parts[0].IsParameter)
        {
            /* 如果 part 是 literal part，
               比较 part（pattern） 与 string segment（request） */
            // This is a literal segment, so we need to match the text, 
            // or the route isn't a match.
            if (pathSegment.Parts[0].IsLiteral)
            {
                var part = (RoutePatternLiteralPart)pathSegment.Parts[0];
                // 如果内容不相同，错误
                if (!stringSegment.Equals(
                    	part.Content, 
	                    StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            /* 否则，part 是 separator part，
               比较 part（pattern） 与 string segment（request） */
            else
            {
                var part = (RoutePatternSeparatorPart)pathSegment.Parts[0];
                // 如果内容不相同，错误
                if (!stringSegment.Equals(
                    	part.Content, 
                    	StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }
        /* path segment（pattern）是 simple，part 是 parameter part */
        else if (pathSegment.IsSimple && 
                 pathSegment.Parts[0].IsParameter)
        {            
            // For a parameter, 
            // 	 validate that it's a has some length, 
            // 	 or we have a default, 
            // 	 or it's optional.
            /* 如果 parameter part（pattern）不是 optional，且没有 default，
               但是 string segment（request）为 empty，错误 */
            var part = (RoutePatternParameterPart)pathSegment.Parts[0];
            if (stringSegment.Length == 0 &&
                !_hasDefaultValue[index] &&
                !part.IsOptional)
            {
                // There's no value for this parameter, the route can't match.
                return false;
            }
        }
        /* 否则，path segment（pattern） 是 complex，不作处理（会转到后续）*/
        else
        {
            Debug.Assert(!pathSegment.IsSimple);
            // Don't attempt to validate a complex segment at this point other than 
            // being non-emtpy, do it in the second pass.
        }
        return true;
    }
}

```

###### 2.1.6.3 save segment as value

```c#
internal class RoutePatternMatcher
{
    private bool SavePathSegmentsAsValues(
        int index, 
        RouteValueDictionary values, 
        StringSegment requestSegment, 
        RoutePatternPathSegment pathSegment)
    {
        /* 如果 path segment（pattern）是 simple，part 是 parameter part 且是 optional*/
        if (pathSegment.IsSimple && 
            pathSegment.Parts[0] is RoutePatternParameterPart parameter && 
            parameter.IsCatchAll)
        {
            /* 截取 request segment 注入 route value dictionary（或 default value）*/
            
            // A catch-all captures til the end of the string.
            var captured = requestSegment.Buffer.Substring(requestSegment.Offset);
            if (captured.Length > 0)
            {
                values[parameter.Name] = captured;
            }
            else
            {
                // It's ok for a catch-all to produce a null value, 
                // so we don't check _hasDefaultValue.
                values[parameter.Name] = _defaultValues[index];
            }
            
            // A catch-all has to be the last part, so we're done.
            return true;
        }
        /* path segment（pattern）是 simple，parameter part（不是 optional）*/
        else if (pathSegment.IsSimple && pathSegment.Parts[0].IsParameter)
        {
            /* 截取 request segment 注入 route value dictionary（或 default value）*/
            
            // A simple parameter captures the whole segment, or a default value 
            // if nothing was provided.
            parameter = (RoutePatternParameterPart)pathSegment.Parts[0];
            if (requestSegment.Length > 0)
            {
                values[parameter.Name] = requestSegment.ToString();
            }
            else
            {
                if (_hasDefaultValue[index])
                {
                    values[parameter.Name] = _defaultValues[index];
                }
            }
        }
        
        /* 否则，即 path segment 不是 simple，或者 part 不是 parameter part， 返回 false */
        return false;
    }
}

```

###### 2.1.6.4 match complex 

```c#
internal class RoutePatternMatcher
{
    internal static bool MatchComplexSegment(
        RoutePatternPathSegment routeSegment,
        ReadOnlySpan<char> requestSegment,
        RouteValueDictionary values)
    {
        var indexOfLastSegment = routeSegment.Parts.Count - 1;
        
        // We match the request to the template starting at the rightmost parameter
        // If the last segment of template is optional, then request can match the 
        // template with or without the last parameter. So we start with regular matching,
        // but if it doesn't match, we start with next to last parameter. Example:
        // Template: {p1}/{p2}.{p3?}. If the request is one/two.three it will match right away
        // giving p3 value of three. But if the request is one/two, we start matching from the
        // rightmost giving p3 the value of two, then we end up not matching the segment.
        // In this case we start again from p2 to match the request and we succeed giving
        // the value two to p2
        if (routeSegment.Parts[indexOfLastSegment] is 
            	RoutePatternParameterPart parameter && 
            parameter.IsOptional &&
            routeSegment.Parts[indexOfLastSegment - 1].IsSeparator)
        {
            if (MatchComplexSegmentCore(
                routeSegment, 
                requestSegment, 
                values, 
                indexOfLastSegment))
            {
                return true;
            }
            else
            {
                var separator = (RoutePatternSeparatorPart)
                    routeSegment.Parts[indexOfLastSegment - 1];
                
                if (requestSegment.EndsWith(
                    separator.Content,
                    StringComparison.OrdinalIgnoreCase))
                    return false;
                
                return MatchComplexSegmentCore(
                    routeSegment,
                    requestSegment,
                    values,
                    indexOfLastSegment - 2);
            }
        }
        else
        {
            return MatchComplexSegmentCore(
                routeSegment, 
                requestSegment, 
                values, 
                indexOfLastSegment);
        }
    }
    
    private static bool MatchComplexSegmentCore(
        RoutePatternPathSegment routeSegment,
        ReadOnlySpan<char> requestSegment,
        RouteValueDictionary values,
        int indexOfLastSegmentUsed)
    {
        Debug.Assert(routeSegment != null);
        Debug.Assert(routeSegment.Parts.Count > 1);
        
        // Find last literal segment and get its last index in the string
        var lastIndex = requestSegment.Length;
        
        // Keeps track of a parameter segment that is pending a value
        RoutePatternParameterPart parameterNeedsValue = null; 
        // Keeps track of the left-most literal we've encountered
        RoutePatternPart lastLiteral = null; 
        
        var outValues = new RouteValueDictionary();
        
        while (indexOfLastSegmentUsed >= 0)
        {
            var newLastIndex = lastIndex;
            
            var part = routeSegment.Parts[indexOfLastSegmentUsed];
            if (part.IsParameter)
            {
                // Hold on to the parameter so that we can fill it in when we locate 
                // the next literal
                parameterNeedsValue = (RoutePatternParameterPart)part;
            }
            else
            {
                Debug.Assert(part.IsLiteral || part.IsSeparator);
                lastLiteral = part;
                
                var startIndex = lastIndex;
                // If we have a pending parameter subsegment, we must leave at least 
                // one character for that
                if (parameterNeedsValue != null)
                {
                    startIndex--;
                }
                
                if (startIndex == 0)
                {
                    return false;
                }
                
                int indexOfLiteral;
                if (part.IsLiteral)
                {
                    var literal = (RoutePatternLiteralPart)part;
                    indexOfLiteral = 
                        requestSegment.Slice(0, startIndex)
                        			  .LastIndexOf(
                        				  literal.Content,
                        				  StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    var literal = (RoutePatternSeparatorPart)part;
                    indexOfLiteral = 
                        requestSegment.Slice(0, startIndex)
                        			  .LastIndexOf(
                        				  literal.Content,
                        				  StringComparison.OrdinalIgnoreCase);
                }
                
                if (indexOfLiteral == -1)
                {
                    // If we couldn't find this literal index, this segment cannot match
                    return false;
                }
                // If the first subsegment is a literal, it must match at the 
                // right-most extent of the request URI.
                // Without this check if your route had "/Foo/" we'd match the request 
                // URI "/somethingFoo/".
                // This check is related to the check we do at the very end of this function.
                if (indexOfLastSegmentUsed == (routeSegment.Parts.Count - 1))
                {
                    if (part is RoutePatternLiteralPart literal && 
                        ((indexOfLiteral + literal.Content.Length) 
                         	!= requestSegment.Length))
                    {
                        return false;
                    }
                    else if (part is RoutePatternSeparatorPart separator && 
                             ((indexOfLiteral + separator.Content.Length) 
                              	!= requestSegment.Length))
                    {
                        return false;
                    }
                }
                
                newLastIndex = indexOfLiteral;
            }
            
            if ((parameterNeedsValue != null) &&
                (((lastLiteral != null) && !part.IsParameter) || 
                 (indexOfLastSegmentUsed == 0)))
            {
                // If we have a pending parameter that needs a value, grab that value
                
                int parameterStartIndex;
                int parameterTextLength;
                
                if (lastLiteral == null)
                {
                    if (indexOfLastSegmentUsed == 0)
                    {
                        parameterStartIndex = 0;
                    }
                    
                    else
                    {
                        parameterStartIndex = newLastIndex;
                        Debug.Assert(
                            false, 
                            "indexOfLastSegementUsed should always be 0 from the check above");
                    }
                    parameterTextLength = lastIndex;
                }
                else
                {
                    // If we're getting a value for a parameter that is somewhere 
                    // in the middle of the segment
                    if ((indexOfLastSegmentUsed == 0) && (part.IsParameter))
                    {
                        parameterStartIndex = 0;
                        parameterTextLength = lastIndex;
                    }
                    else
                    {
                        if (lastLiteral.IsLiteral)
                        {
                            var literal = (RoutePatternLiteralPart)lastLiteral;
                            parameterStartIndex = newLastIndex + literal.Content.Length;
                        }
                        else
                        {
                            var separator = (RoutePatternSeparatorPart)lastLiteral;
                            parameterStartIndex = newLastIndex + separator.Content.Length;
                        }
                        parameterTextLength = lastIndex - parameterStartIndex;
                    }
                }
                
                var parameterValueSpan = requestSegment.Slice(
                    parameterStartIndex, 
                    parameterTextLength);
                
                if (parameterValueSpan.Length == 0)
                {
                    // If we're here that means we have a segment that contains 
                    // multiple sub-segments.
                    // For these segments all parameters must have non-empty values. 
                    // If the parameter has an empty value it's not a match.                        
                    return false;                    
                }
                else
                {
                    // If there's a value in the segment for this parameter, 
                    // use the subsegment value
                    outValues.Add(parameterNeedsValue.Name, new string(parameterValueSpan));
                }
                
                parameterNeedsValue = null;
                lastLiteral = null;
            }
            
            lastIndex = newLastIndex;
            indexOfLastSegmentUsed--;
        }
        
        // If the last subsegment is a parameter, it's OK that we didn't parse all the 
        // way to the left extent of the string since the parameter will have consumed 
        // all the remaining text anyway. 
        // If the last subsegment is a literal then we *must* have consumed the entire text 
        // in that literal. 
        // Otherwise we end up matching the route "Foo" to the request URI "somethingFoo". 
        // Thus we have to check that we parsed the *entire* request URI in order for it 
        // to be a match.
        // This check is related to the check we do earlier in this function for 
        // LiteralSubsegments.
        if (lastIndex == 0 || 
            routeSegment.Parts[0].IsParameter)
        {
            foreach (var item in outValues)
            {
                values[item.Key] = item.Value;
            }
            
            return true;
        }
        
        return false;
    }
}

```

###### 2.1.6.5 path tokenizer

```c#
internal struct PathTokenizer : IReadOnlyList<StringSegment>
{
    private readonly string _path;
    private int _count;
    
    public PathTokenizer(PathString path)
    {
        _path = path.Value;
        _count = -1;
    }
    
    public int Count
    {
        get
        {
            if (_count == -1)
            {
                // We haven't computed the real count of segments yet.
                if (_path.Length == 0)
                {
                    // The empty string has length of 0.
                    _count = 0;
                    return _count;
                }
                
                // A string of length 1 must be "/" - all PathStrings start with '/'
                if (_path.Length == 1)
                {
                    // We treat this as empty - there's nothing to parse here for routing, 
                    // because routing ignores a trailing slash.
                    Debug.Assert(_path[0] == '/');
                    _count = 0;
                    return _count;
                }
                
                // This is a non-trival PathString
                _count = 1;
                
                // Since a non-empty PathString must begin with a `/`, we can just count 
                // the number of occurrences of `/` to find the number of segments. 
                // However, we don't look at the last character, because routing ignores a 
                // trailing slash.
                for (var i = 1; i < _path.Length - 1; i++)
                {
                    if (_path[i] == '/')
                    {
                        _count++;
                    }
                }
            }
            
            return _count;
        }
    }
    
    public StringSegment this[int index]
    {
        get
        {
            if (index >= Count)
            {
                throw new IndexOutOfRangeException();
            }
                        
            var currentSegmentIndex = 0;
            var currentSegmentStart = 1;
            
            // Skip the first `/`.
            var delimiterIndex = 1;
            while ((delimiterIndex = _path.IndexOf('/', delimiterIndex)) != -1)
            {
                if (currentSegmentIndex++ == index)
                {
                    return new StringSegment(
                        _path, 
                        currentSegmentStart, 
                        delimiterIndex - currentSegmentStart);
                }
                else
                {
                    currentSegmentStart = delimiterIndex + 1;
                    delimiterIndex++;
                }
            }
            
            // If we get here we're at the end of the string. The implementation 
            // of .Count should protect us from these cases. 
            Debug.Assert(_path[_path.Length - 1] != '/');
            Debug.Assert(currentSegmentIndex == index);
            
            return new StringSegment(
                _path, 
                currentSegmentStart, 
                _path.Length - currentSegmentStart);
        }
    }
    
    public Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }
    
    IEnumerator<StringSegment> IEnumerable<StringSegment>.GetEnumerator()
    {
        return GetEnumerator();
    }
    
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    
    public struct Enumerator : IEnumerator<StringSegment>
    {
        private readonly string _path;        
        private int _index;
        private int _length;
        
        public Enumerator(PathTokenizer tokenizer)
        {
            _path = tokenizer._path;            
            _index = -1;
            _length = -1;
        }
        
        public StringSegment Current
        {
            get
            {
                return new StringSegment(_path, _index, _length);
            }
        }
        
        object IEnumerator.Current
        {
            get
            {
                return Current;
            }
        }
        
        public void Dispose()
        {
        }
        
        public bool MoveNext()
        {
            if (_path == null || _path.Length <= 1)
            {
                return false;
            }
            
            if (_index == -1)
            {
                // Skip the first `/`.
                _index = 1;
            }
            else
            {
                // Skip to the end of the previous segment + the separator.
                _index += _length + 1;
            }
            
            if (_index >= _path.Length)
            {
                // We're at the end
                return false;
            }
            
            var delimiterIndex = _path.IndexOf('/', _index);
            if (delimiterIndex != -1)
            {
                _length = delimiterIndex - _index;
                return true;
            }
            
            // We might have some trailing text after the last separator.
            if (_path[_path.Length - 1] == '/')
            {
                // If the last char is a '/' then it's just a trailing slash, 
                // we don't have another segment.
                return false;
            }
            else
            {
                _length = _path.Length - _index;
                return true;
            }
        }
        
        public void Reset()
        {
            _index = -1;
            _length = -1;
        }
    }
}

```

##### 2.1.7 route pattern transformer

###### 2.1.7.1 抽象基类

```c#
public abstract class RoutePatternTransformer
{    
    // Substituting required values into a route pattern is intended for us with 
    // a general-purpose parameterize route specification that can match many logical
    // endpoints. 
    // Calling "SubstituteRequiredValues(RoutePattern, object)" produce a derived 
    // route pattern for each set of route values that corresponds to an endpoint.    
    // The substitution process considers default values and "IRouteConstraint"
    // implementations when examining a required value. 
    // "SubstituteRequiredValues(RoutePattern, object)" will return "null" if any 
    // required value cannot be substituted.    
    public abstract RoutePattern? SubstituteRequiredValues(
        RoutePattern original, 
        object requiredValues);
}

```

###### 2.1.7.2 default route pattern transformer

```c#
internal class DefaultRoutePatternTransformer : RoutePatternTransformer
{
    private readonly ParameterPolicyFactory _policyFactory;
    
    public DefaultRoutePatternTransformer(ParameterPolicyFactory policyFactory)
    {
        if (policyFactory == null)
        {
            throw new ArgumentNullException(nameof(policyFactory));
        }
        
        _policyFactory = policyFactory;
    }
    
    public override RoutePattern SubstituteRequiredValues(
        RoutePattern original, 
        object requiredValues)
    {
        if (original == null)
        {
            throw new ArgumentNullException(nameof(original));
        }
        
        return SubstituteRequiredValuesCore(
            original, 
            new RouteValueDictionary(requiredValues));
    }
    
    private RoutePattern SubstituteRequiredValuesCore(
        RoutePattern original, 
        RouteValueDictionary requiredValues)
    {
        // Process each required value in sequence. 
        // Bail if we find any rejection criteria. 
        // The goal of rejection is to avoid creating RoutePattern instances that can't 
        // *ever* match.
        // If we succeed, then we need to create a new RoutePattern with the provided 
        // required values.
        // Substitution can merge with existing RequiredValues already on the RoutePattern 
        // as long as all of the success criteria are still met at the end.
        foreach (var kvp in requiredValues)
        {
            // There are three possible cases here:
            // 1. Required value is null-ish
            // 2. Required value is *any*
            // 3. Required value corresponds to a parameter
            // 4. Required value corresponds to a matching default value
            //
            // If none of these are true then we can reject this substitution.
            RoutePatternParameterPart parameter;
            if (RouteValueEqualityComparer.Default.Equals(kvp.Value, string.Empty))
            {
                // 1. Required value is null-ish - check to make sure that this route 
                // doesn't have a parameter or filter-like default.                
                if (original.GetParameter(kvp.Key) != null)
                {
                    // Fail: we can't 'require' that a parameter be null. 
                    // In theory this would be possible for an optional parameter, but 
                    // that's not really in line with the usage of this feature so we 
                    // don't handle it.
                    //
                    // Ex: {controller=Home}/{action=Index}/{id?} - 
                    // 	   with required values: { controller = "" }
                    return null;
                }
                else if (original.Defaults.TryGetValue(kvp.Key, out var defaultValue) &&
                         !RouteValueEqualityComparer.Default
                         							.Equals(kvp.Value, defaultValue))
                {
                    // Fail: this route has a non-parameter default that doesn't match.
                    //
                    // Ex: Admin/{controller=Home}/{action=Index}/{id?} 
                    //     defaults: { area = "Admin" } 
                    //     - with required values: { area = "" }
                    return null;
                }
                
                // Success: (for this parameter at least)
                //
                // Ex: {controller=Home}/{action=Index}/{id?} 
                //     - with required values: { area = "", ... }
                continue;
            }
            else if (RoutePattern.IsRequiredValueAny(kvp.Value))
            {
                // 2. Required value is *any* - this is allowed for a parameter with 
                // a default, but not a non-parameter default.
                if (original.GetParameter(kvp.Key) == null &&
                    original.Defaults.TryGetValue(kvp.Key, out var defaultValue) &&
                    !RouteValueEqualityComparer.Default
                    						   .Equals(string.Empty, defaultValue))
                {
                    // Fail: this route as a non-parameter default that is stricter 
                    // than *any*.
                    //
                    // Ex: Admin/{controller=Home}/{action=Index}/{id?} 
                    //     defaults: { area = "Admin" } 
                    //     - with required values: { area = *any* }
                    return null;
                }
                
                // Success: (for this parameter at least)
                //
                // Ex: {controller=Home}/{action=Index}/{id?} 
                //     - with required values: { controller = *any*, ... }
                continue;
            }
            else if ((parameter = original.GetParameter(kvp.Key)) != null)
            {
                // 3. Required value corresponds to a parameter - check to make sure 
                // that this value matches any IRouteConstraint implementations.
                if (!MatchesConstraints(original, parameter, kvp.Key, requiredValues))
                {
                    // Fail: this route has a constraint that failed.
                    //
                    // Ex: Admin/{controller:regex(Home|Login)}/{action=Index}/{id?} 
                    //     - with required values: { controller = "Store" }
                    return null;
                }
                
                // Success: (for this parameter at least)
                //
                // Ex: {area}/{controller=Home}/{action=Index}/{id?} 
                //     - with required values: { area = "", ... }
                continue;
            }
            else if (original.Defaults.TryGetValue(kvp.Key, out var defaultValue) &&
                     RouteValueEqualityComparer.Default
                     						   .Equals(kvp.Value, defaultValue))
            {
                // 4. Required value corresponds to a matching default value - 
                // check to make sure that this value matches any IRouteConstraint 
                // implementations. 
                // It's unlikely that this would happen in practice but it doesn't
                // hurt for us to check.
                if (!MatchesConstraints(original, parameter: null, kvp.Key, requiredValues))
                {
                    // Fail: this route has a constraint that failed.
                    //
                    // Ex: 
                    //  Admin/Home/{action=Index}/{id?} 
                    //  defaults: { area = "Admin" }
                    //  constraints: { area = "Blog" }
                    //  with required values: { area = "Admin" }
                    return null;
                }
                
                // Success: (for this parameter at least)
                //
                // Ex: Admin/{controller=Home}/{action=Index}/{id?} 
                //     defaults: { area = "Admin" }
                //     - with required values: { area = "Admin", ... }
                continue;
            }
            else
            {
                // Fail: this is a required value for a key that doesn't appear 
                // in the templates, or the route pattern has a different default value 
                // for a non-parameter.
                //          
                // Ex: Admin/{controller=Home}/{action=Index}/{id?} 
                //     defaults: { area = "Admin" }
                //     - with required values: { area = "Blog", ... }
                // OR (less likely)
                // Ex: Admin/{controller=Home}/{action=Index}/{id?} with 
                //     required values: { page = "/Index", ... }
                return null;
            }
        }
        
        List<RoutePatternParameterPart> updatedParameters = null;
        List<RoutePatternPathSegment> updatedSegments = null;
        RouteValueDictionary updatedDefaults = null;
        
        // So if we get here, we're ready to update the route pattern. 
        // We need to update two things:
        // 1. Remove any default values that conflict with the required values.
        // 2. Merge any existing required values
        foreach (var kvp in requiredValues)
        {
            var parameter = original.GetParameter(kvp.Key);
            
            // We only need to handle the case where the required value maps to a parameter. 
            // That's the only case where we allow a default and a required value to 
            // disagree, and we already validated the other cases.
            //
            // If the required value is *any* then don't remove the default.
            if (parameter != null &&
                !RoutePattern.IsRequiredValueAny(kvp.Value) &&
                original.Defaults.TryGetValue(kvp.Key, out var defaultValue) && 
                !RouteValueEqualityComparer.Default
                						   .Equals(kvp.Value, defaultValue))
            {
                if (updatedDefaults == null && 
                    updatedSegments == null && 
                    updatedParameters == null)
                {
                    updatedDefaults = 
                        new RouteValueDictionary(original.Defaults);
                    updatedSegments = 
                        new List<RoutePatternPathSegment>(original.PathSegments);
                    updatedParameters = 
                        new List<RoutePatternParameterPart>(original.Parameters);
                }
                
                updatedDefaults.Remove(kvp.Key);
                RemoveParameterDefault(updatedSegments, updatedParameters, parameter);
            }
        }
        
        foreach (var kvp in original.RequiredValues)
        {
            requiredValues.TryAdd(kvp.Key, kvp.Value);
        }
        
        return new RoutePattern(
            original.RawText,
            updatedDefaults ?? original.Defaults,
            original.ParameterPolicies,
            requiredValues,
            updatedParameters ?? original.Parameters,
            updatedSegments ?? original.PathSegments);
    }
    
    private bool MatchesConstraints(
        RoutePattern pattern, 
        RoutePatternParameterPart parameter, 
        string key, 
        RouteValueDictionary requiredValues)
    {
        if (pattern.ParameterPolicies
            	   .TryGetValue(key, out var policies))
        {
            for (var i = 0; i < policies.Count; i++)
            {
                var policy = _policyFactory.Create(parameter, policies[i]);
                if (policy is IRouteConstraint constraint)
                {
                    if (!constraint.Match(
                        	httpContext: null, 
                        	NullRouter.Instance, 
                        	key, 
                        	requiredValues, 
                        	RouteDirection.IncomingRequest))
                    {
                        return false;
                    }
                }
            }
        }
        
        return true;
    }
    
    private void RemoveParameterDefault(
        List<RoutePatternPathSegment> segments, 
        List<RoutePatternParameterPart> parameters, 
        RoutePatternParameterPart parameter)
    {
        // We know that a parameter can only appear once, so we only need to rewrite 
        // one segment and one parameter.
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            for (var j = 0; j < segment.Parts.Count; j++)
            {
                if (object.ReferenceEquals(parameter, segment.Parts[j]))
                {
                    // Found it!
                    var updatedParameter = 
                        RoutePatternFactory.ParameterPart(
                        	parameter.Name, 
                        	@default: null, 
                        	parameter.ParameterKind, 
                        	parameter.ParameterPolicies);
                    
                    var updatedParts = new List<RoutePatternPart>(segment.Parts);
                    updatedParts[j] = updatedParameter;
                    segments[i] = RoutePatternFactory.Segment(updatedParts);
                    
                    for (var k = 0; k < parameters.Count; k++)
                    {
                        if (ReferenceEquals(parameter, parameters[k]))
                        {
                            parameters[k] = updatedParameter;
                            break;
                        }
                    }
                    
                    return;
                }
            }
        }
    }
}

```



#### 2.2 route template

##### 2.2.1 route template

```c#
[DebuggerDisplay("{DebuggerToString()}")]
public class RouteTemplate
{
    private const string SeparatorString = "/";
    
    public string? TemplateText { get; }      
    public IList<TemplateSegment> Segments { get; }
    public IList<TemplatePart> Parameters { get; }                    
        
    public RouteTemplate(RoutePattern other)
    {
        if (other == null)
        {
            throw new ArgumentNullException(nameof(other));
        }
        
        // RequiredValues will be ignored. RouteTemplate doesn't support them.
        
        // text
        TemplateText = other.RawText;
        // segments
        Segments = new List<TemplateSegment>(
            			   other.PathSegments
            					.Select(p => 
                                        	new TemplateSegment(p)));
        // parameters
        Parameters = new List<TemplatePart>();
        for (var i = 0; i < Segments.Count; i++)
        {
            var segment = Segments[i];
            for (var j = 0; j < segment.Parts.Count; j++)
            {
                var part = segment.Parts[j];
                if (part.IsParameter)
                {
                    Parameters.Add(part);
                }
            }
        }
    }
           
    public RouteTemplate(
        string template, 
        List<TemplateSegment> segments)
    {
        if (segments == null)
        {
            throw new ArgumentNullException(nameof(segments));
        }
        
        // text
        TemplateText = template;
        // segments
        Segments = segments;
        // parameters
        Parameters = new List<TemplatePart>();
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = Segments[i];
            for (var j = 0; j < segment.Parts.Count; j++)
            {
                var part = segment.Parts[j];
                if (part.IsParameter)
                {
                    Parameters.Add(part);
                }
            }
        }
    }
    
    // get segment
    public TemplateSegment? GetSegment(int index)
    {
        if (index < 0)
        {
            throw new IndexOutOfRangeException();
        }
        
        return index >= Segments.Count ? null : Segments[index];
    }
            
    // get parameter
    public TemplatePart? GetParameter(string name)
    {
        for (var i = 0; i < Parameters.Count; i++)
        {
            var parameter = Parameters[i];
            if (string.Equals(
                		   parameter.Name, 
                		   name, 
                		   StringComparison.OrdinalIgnoreCase))
            {
                return parameter;
            }
        }
        
        return null;
    }
    
    // to route pattern    
    public RoutePattern ToRoutePattern()
    {
        var segments = Segments.Select(s => 
                                       	   s.ToRoutePatternPathSegment());
        
        return RoutePatternFactory.Pattern(TemplateText, segments);
    }
    
    private string DebuggerToString()
    {
        return string.Join(
            SeparatorString, 
            Segments.Select(s => s.DebuggerToString()));
    }
}

```

###### 2.2.2.1 template segment

```c#
[DebuggerDisplay("{DebuggerToString()}")]
public class TemplateSegment
{
    public List<TemplatePart> Parts { get; }
    public bool IsSimple => Parts.Count == 1;           
        
    public TemplateSegment()
    {
        Parts = new List<TemplatePart>();
    }
       
    public TemplateSegment(RoutePatternPathSegment other)
    {
        if (other == null)
        {
            throw new ArgumentNullException(nameof(other));
        }
        
        var partCount = other.Parts.Count;
        Parts = new List<TemplatePart>(partCount);
        for (var i = 0; i < partCount; i++)
        {
            Parts.Add(new TemplatePart(other.Parts[i]));
        }
    }
    
    public RoutePatternPathSegment ToRoutePatternPathSegment()
    {
        var parts = Parts.Select(p => p.ToRoutePatternPart());
        return RoutePatternFactory.Segment(parts);
    }
    
    internal string DebuggerToString()
    {
        return string.Join(
            string.Empty, 
            Parts.Select(p => p.DebuggerToString()));
    }    
}

```

###### 2.2.2.2 template part

```c#
[DebuggerDisplay("{DebuggerToString()}")]
public class TemplatePart
{
    public bool IsCatchAll { get; private set; }    
    public bool IsLiteral { get; private set; }   
    public bool IsParameter { get; private set; }    
    public bool IsOptional { get; private set; }    
    public bool IsOptionalSeperator { get; set; }   
    
    public string? Name { get; private set; }    
    public string? Text { get; private set; }    
    public object? DefaultValue { get; private set; }
    
    public IEnumerable<InlineConstraint> 
        InlineConstraints { get; private set; } = Enumerable.Empty<InlineConstraint>();
        
    public TemplatePart()
    {
    }
    
    public TemplatePart(RoutePatternPart other)
    {
        IsLiteral = other.IsLiteral || other.IsSeparator;
        IsParameter = other.IsParameter;
        
        if (other.IsLiteral && 
            other is RoutePatternLiteralPart literal)
        {
            Text = literal.Content;
        }
        else if (other.IsParameter && 
                 other is RoutePatternParameterPart parameter)
        {
            // Text is unused by TemplatePart and assumed to be null when the part 
            // is a parameter.
            Name = parameter.Name;
            IsCatchAll = parameter.IsCatchAll;
            IsOptional = parameter.IsOptional;
            DefaultValue = parameter.Default;
            InlineConstraints = 
                parameter.ParameterPolicies
                		 ?.Select(p => new InlineConstraint(p)) 
                			 ?? Enumerable.Empty<InlineConstraint>();
        }
        else if (other.IsSeparator && 
                 other is RoutePatternSeparatorPart separator)
        {
            Text = separator.Content;
            IsOptionalSeperator = true;
        }
        else
        {
            // Unreachable
            throw new NotSupportedException();
        }
    }
        
    public static TemplatePart CreateLiteral(string text)
    {
        return new TemplatePart()
        {
            IsLiteral = true,
            Text = text,
        };
    }
        
    public static TemplatePart CreateParameter(
        string name,
        bool isCatchAll,
        bool isOptional,
        object? defaultValue,
        IEnumerable<InlineConstraint>? inlineConstraints)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        return new TemplatePart()
        {
            IsParameter = true,
            Name = name,
            IsCatchAll = isCatchAll,
            IsOptional = isOptional,
            DefaultValue = defaultValue,
            InlineConstraints = inlineConstraints ?? Enumerable.Empty<InlineConstraint>(),
        };
    }
                                       
    public RoutePatternPart ToRoutePatternPart()
    {
        if (IsLiteral && 
            IsOptionalSeperator)
        {
            return RoutePatternFactory.SeparatorPart(Text!);
        }
        else if (IsLiteral)
        {
            return RoutePatternFactory.LiteralPart(Text!);
        }
        else
        {
            var kind = IsCatchAll 
                		   ? RoutePatternParameterKind.CatchAll 
                		   : IsOptional 
                               ? RoutePatternParameterKind.Optional 
                               : RoutePatternParameterKind.Standard;
            
            var constraints = InlineConstraints.Select(
                	c => new RoutePatternParameterPolicyReference(c.Constraint));
            
            return RoutePatternFactory.ParameterPart(
                						   Name!, 
                						   DefaultValue, 
                						   kind, 
                						   constraints);
        }
    }
    
    internal string? DebuggerToString()
    {
        if (IsParameter)
        {
            return "{" + 
                	(IsCatchAll ? "*" : string.Empty) + 
                	Name + 
                	(IsOptional ? "?" : string.Empty) + 
                	"}";
        }
        else
        {
            return Text;
        }
    }
}

```

###### 2.2.2.3 inline route parameter resolver

```c#
public static class InlineRouteParameterParser
{
    
    public static TemplatePart ParseRouteParameter(string routeParameter)
    {
        if (routeParameter == null)
        {
            hrow new ArgumentNullException(nameof(routeParameter));
            
        }
        
        if (routeParameter.Length == 0)
        {
            return TemplatePart.CreateParameter(
                name: string.Empty,
                isCatchAll: false,
                isOptional: false,
                defaultValue: null,
                inlineConstraints: null);
        }
        
        var startIndex = 0;
        var endIndex = routeParameter.Length - 1;
        
        var isCatchAll = false;
        var isOptional = false;
        
        if (routeParameter[0] == '*')
        {
            isCatchAll = true;
            startIndex++;
        }
        
        if (routeParameter[endIndex] == '?')
        {
            isOptional = true;
            endIndex--;
        }
        
        
        var currentIndex = startIndex;
        
        // Parse parameter name
        var parameterName = string.Empty;
        
        while (currentIndex <= endIndex)
        {
            var currentChar = routeParameter[currentIndex];
            
            if ((currentChar == ':' || currentChar == '=') && startIndex != currentIndex)
            {
                // Parameter names are allowed to start with delimiters used to denote constraints or default values.
                // i.e. "=foo" or ":bar" would be treated as parameter names rather than default value or constraint
                // specifications.
                parameterName = routeParameter.Substring(startIndex, currentIndex - startIndex);
                
                // Roll the index back and move to the constraint parsing stage.
                currentIndex--;
                break;
            }
            else if (currentIndex == endIndex)
            {
                parameterName = routeParameter.Substring(startIndex, currentIndex - startIndex + 1);
            }
            
            currentIndex++;
        }
        
        var parseResults = ParseConstraints(routeParameter, currentIndex, endIndex);
        currentIndex = parseResults.CurrentIndex;
        
        string? defaultValue = null;
        if (currentIndex <= endIndex &&
            routeParameter[currentIndex] == '=')
        {
            defaultValue = routeParameter.Substring(currentIndex + 1, endIndex - currentIndex);
        }
        
        return TemplatePart.CreateParameter(parameterName,
                                            isCatchAll,
                                            isOptional,
                                            defaultValue,
                                            parseResults.Constraints);
    }
    
    private static ConstraintParseResults ParseConstraints(
        string routeParameter,
        int currentIndex,
        int endIndex)
    {
        var inlineConstraints = new List<InlineConstraint>();
        var state = ParseState.Start;
        var startIndex = currentIndex;
        do
        {
            var currentChar = currentIndex > endIndex 
                ? null 
                : (char?)routeParameter[currentIndex];
            
            switch (state)
            {
                case ParseState.Start:
                    switch (currentChar)
                    {
                        case null:
                            state = ParseState.End;
                            break;
                        case ':':
                            state = ParseState.ParsingName;
                            startIndex = currentIndex + 1;
                            break;
                        case '(':
                            state = ParseState.InsideParenthesis;
                            break;
                        case '=':
                            state = ParseState.End;
                            currentIndex--;
                            break;
                    }
                    break;
                    
                case ParseState.InsideParenthesis:
                    switch (currentChar)
                    {
                        case null:
                            state = ParseState.End;
                            var constraintText = routeParameter.Substring(
                                startIndex, 
                                currentIndex - startIndex);
                            inlineConstraints.Add(new InlineConstraint(constraintText));
                            break;
                        case ')':
                            // Only consume a ')' token if
                            // (a) it is the last token
                            // (b) the next character is the start of the new constraint ':'
                            // (c) the next character is the start of the default value.
                            
                            var nextChar = currentIndex + 1 > endIndex 
                                ? null 
                                : (char?)routeParameter[currentIndex + 1];
                            switch (nextChar)
                            {
                                case null:
                                    state = ParseState.End;
                                    constraintText = routeParameter.Substring(
                                        startIndex, 
                                        currentIndex - startIndex + 1);
                                    inlineConstraints.Add(
                                        new InlineConstraint(constraintText));
                                    break;
                                case ':':
                                    state = ParseState.Start;
                                    constraintText = routeParameter.Substring(
                                        startIndex, 
                                        currentIndex - startIndex + 1);
                                    inlineConstraints.Add(
                                        new InlineConstraint(constraintText));
                                    startIndex = currentIndex + 1;
                                    break;
                                case '=':
                                    state = ParseState.End;
                                    constraintText = routeParameter.Substring(
                                        startIndex, 
                                        currentIndex - startIndex + 1);
                                    inlineConstraints.Add(
                                        new InlineConstraint(constraintText));
                                    break;
                            }
                            break;
                        case ':':
                        case '=':
                            // In the original implementation, the Regex would've 
                            // backtracked if it encountered an unbalanced opening 
                            // bracket followed by (not necessarily immediatiely) a delimiter.
                            // Simply verifying that the parantheses will eventually be closed
                            // should suffice to determine if the terminator needs to be 
                            // consumed as part of the current constraint specification.
                            var indexOfClosingParantheses = r
                                outeParameter.IndexOf(')', currentIndex + 1);
                            if (indexOfClosingParantheses == -1)
                            {
                                constraintText = routeParameter.Substring(
                                    startIndex, 
                                    currentIndex - startIndex);
                                
                                inlineConstraints.Add(new InlineConstraint(constraintText));
                                
                                if (currentChar == ':')
                                {
                                    state = ParseState.ParsingName;
                                    startIndex = currentIndex + 1;
                                }
                                else
                                {
                                    state = ParseState.End;
                                    currentIndex--;
                                }
                            }
                            else
                            {
                                currentIndex = indexOfClosingParantheses;
                            }
                            
                            break;
                    }
                    break;
                case ParseState.ParsingName:
                    switch (currentChar)
                    {
                        case null:
                            state = ParseState.End;
                            var constraintText = routeParameter.Substring(
                                startIndex, 
                                currentIndex - startIndex);
                            inlineConstraints.Add(
                                new InlineConstraint(constraintText));
                            break;
                        case ':':
                            constraintText = routeParameter.Substring(
                                startIndex, 
                                currentIndex - startIndex);
                            inlineConstraints.Add(
                                new InlineConstraint(constraintText));
                            startIndex = currentIndex + 1;
                            break;
                        case '(':
                            state = ParseState.InsideParenthesis;
                            break;
                        case '=':
                            state = ParseState.End;
                            constraintText = routeParameter.Substring(
                                startIndex, 
                                currentIndex - startIndex);
                            inlineConstraints.Add(
                                new InlineConstraint(constraintText));
                            currentIndex--;
                            break;
                    }
                    break;
            }
            
            currentIndex++;
            
        } while (state != ParseState.End);
        
        return new ConstraintParseResults(currentIndex, inlineConstraints);
    }
    
    private enum ParseState
    {
        Start,
        ParsingName,
        InsideParenthesis,
        End
    }
    
    private readonly struct ConstraintParseResults
    {
        public readonly int CurrentIndex;        
        public readonly IEnumerable<InlineConstraint> Constraints;
        
        public ConstraintParseResults(
            int currentIndex, 
            IEnumerable<InlineConstraint> constraints)
        {
            CurrentIndex = currentIndex;
            Constraints = constraints;
        }
    }
}

```

##### 2.2.2 template parser

```c#
public static class TemplateParser
{        
    public static RouteTemplate Parse(string routeTemplate)
    {
        if (routeTemplate == null)
        {
            throw new ArgumentNullException(routeTemplate);
        }
        
        try
        {
            var inner = RoutePatternFactory.Parse(routeTemplate);
            return new RouteTemplate(inner);
        }
        catch (RoutePatternException ex)
        {
            // Preserving the existing behavior of this API even though the logic moved.
            throw new ArgumentException(ex.Message, nameof(routeTemplate), ex);
        }
    }
}

```

##### 2.2.3 template matcher

```c#
public class TemplateMatcher
{
    /* 分隔符 */
    private const string SeparatorString = "/";
    private const char SeparatorChar = '/';
    private static readonly char[] Delimiters = new char[] { SeparatorChar };
    
    // Perf: This is a cache to avoid looking things up in 'Defaults' each request.
    private readonly bool[] _hasDefaultValue;
    private readonly object?[] _defaultValues;
       
    // pattern matcher
    private RoutePatternMatcher _routePatternMatcher;
    
    public RouteTemplate Template { get; }
    public RouteValueDictionary Defaults { get; }
            
    public TemplateMatcher(
        RouteTemplate template,
        RouteValueDictionary defaults)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }
        
        /* 注入 route template、defautls(route value dictionary)  */
        Template = template;
        Defaults = defaults ?? new RouteValueDictionary();
        
        /* 创建 has default value、default value 集合（默认值）*/
        // Perf: cache the default value for each parameter (other than complex segments).
        _hasDefaultValue = new bool[Template.Segments.Count];
        _defaultValues = new object[Template.Segments.Count];
        
        /* 从参数 default(route value dictionary) 中查找 parameter 的 default value，
           注入 default values 集合 */
        for (var i = 0; i < Template.Segments.Count; i++)
        {
            var segment = Template.Segments[i];
            if (!segment.IsSimple)
            {
                continue;
            }
            
            var part = segment.Parts[0];
            if (!part.IsParameter)
            {
                continue;
            }
            
            if (Defaults.TryGetValue(part.Name!, out var value))
            {
                _hasDefaultValue[i] = true;
                _defaultValues[i] = value;
            }
        }
        
        /* 创建 route pattern matcher */
        // route template -> route pattern
        var routePattern = Template.ToRoutePattern();
        // 由 route pattern、defaults 创建 route pattern matcher
        _routePatternMatcher = new RoutePatternMatcher(routePattern, Defaults);
    }
    
    // 验证是否匹配，
    // 调用了 pattern matcher 的 match 方法
    public bool TryMatch(PathString path, RouteValueDictionary values)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }
        
        return _routePatternMatcher.TryMatch(path, values);        
    }
}

```

##### 2.2.4 template binder

```c#
public class TemplateBinder
{
    private readonly UrlEncoder _urlEncoder;
    private readonly ObjectPool<UriBuildingContext> _pool;
    
    private readonly (string parameterName, IRouteConstraint constraint)[] _constraints;
    private readonly RouteValueDictionary? _defaults;
    private readonly KeyValuePair<string, object?>[] _filters;
    private readonly (string parameterName, IOutboundParameterTransformer transformer)[] 
        _parameterTransformers;
    private readonly RoutePattern _pattern;
    private readonly string[] _requiredKeys;
    
    // A pre-allocated template for the 'known' route values that this template binder uses.
    //
    // We always make a copy of this and operate on the copy, so that we don't mutate shared 
    // state.
    private readonly KeyValuePair<string, object?>[] _slots;
        
    internal TemplateBinder(
        UrlEncoder urlEncoder,
        ObjectPool<UriBuildingContext> pool,
        RouteTemplate template,
        RouteValueDictionary defaults)            
        	: this(
                urlEncoder, 
                pool, 
                template?.ToRoutePattern()!, 
                defaults,
                requiredKeys: null, 
                parameterPolicies: null)
    {
    }
            
    internal TemplateBinder(
        UrlEncoder urlEncoder,
        ObjectPool<UriBuildingContext> pool,
        RoutePattern pattern,
        RouteValueDictionary? defaults,
        IEnumerable<string>? requiredKeys,
        IEnumerable<(string parameterName, IParameterPolicy policy)>? parameterPolicies)
    {
        if (urlEncoder == null)
        {
            throw new ArgumentNullException(nameof(urlEncoder));
        }        
        if (pool == null)
        {
            throw new ArgumentNullException(nameof(pool));
        }        
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }
        
        /* 注入 */
        _urlEncoder = urlEncoder;
        _pool = pool;
        _pattern = pattern;
        _defaults = defaults;
        _requiredKeys = requiredKeys?.ToArray() ?? Array.Empty<string>();
        
        /* 注入 filter */
        // Any default that doesn't have a corresponding parameter is a 'filter' and if a value
        // is provided for that 'filter' it must match the value in defaults.
        var filters = new RouteValueDictionary(_defaults);
        for (var i = 0; i < pattern.Parameters.Count; i++)
        {
            filters.Remove(pattern.Parameters[i].Name);
        }
        _filters = filters.ToArray();
        
        /* 注入 constraint */
        _constraints = parameterPolicies
            ?.Where(p => p.policy is IRouteConstraint)
            .Select(p => (p.parameterName, (IRouteConstraint)p.policy))
            .ToArray() ?? Array.Empty<(string, IRouteConstraint)>();
        
        /* 注入 parameter policy (transformers) */
        _parameterTransformers = parameterPolicies
            ?.Where(p => p.policy is IOutboundParameterTransformer)
            .Select(p => (p.parameterName, (IOutboundParameterTransformer)p.policy))
            .ToArray() ?? Array.Empty<(string, IOutboundParameterTransformer)>();
        
        /* 注入 slot */
        _slots = AssignSlots(_pattern, _filters);
    }
    
    internal TemplateBinder(
        UrlEncoder urlEncoder,
        ObjectPool<UriBuildingContext> pool,
        RoutePattern pattern,
        IEnumerable<(string parameterName, IParameterPolicy policy)> parameterPolicies)
    {
        if (urlEncoder == null)
        {
            throw new ArgumentNullException(nameof(urlEncoder));
        }
        
        if (pool == null)
        {
            throw new ArgumentNullException(nameof(pool));
        }
        
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }
        
        /* 注入 */        
        _urlEncoder = urlEncoder;
        _pool = pool;
        _pattern = pattern;
        _defaults = new RouteValueDictionary(pattern.Defaults);
        _requiredKeys = pattern.RequiredValues.Keys.ToArray();
        
        /* 注入 filter */
        // Any default that doesn't have a corresponding parameter is a 'filter' and if a value
        // is provided for that 'filter' it must match the value in defaults.
        var filters = new RouteValueDictionary(_defaults);
        for (var i = 0; i < pattern.Parameters.Count; i++)
        {
            filters.Remove(pattern.Parameters[i].Name);
        }
        _filters = filters.ToArray();
        
        /* 注入 constraint */
        _constraints = parameterPolicies
            ?.Where(p => p.policy is IRouteConstraint)
            .Select(p => (p.parameterName, (IRouteConstraint)p.policy))
            .ToArray() ?? Array.Empty<(string, IRouteConstraint)>();
        
        /* 注入 transformer */
        _parameterTransformers = parameterPolicies
            ?.Where(p => p.policy is IOutboundParameterTransformer)
            .Select(p => (p.parameterName, (IOutboundParameterTransformer)p.policy))
            .ToArray() ?? Array.Empty<(string, IOutboundParameterTransformer)>();
        
        /* 注入 slot */
        _slots = AssignSlots(_pattern, _filters);
    }
    
        
    public TemplateValuesResult? GetValues(
        RouteValueDictionary? ambientValues, 
        RouteValueDictionary values)
    {
        // Make a new copy of the slots array, we'll use this as 'scratch' space
        // and then the RVD will take ownership of it.
        var slots = new KeyValuePair<string, object?>[_slots.Length];
        Array.Copy(_slots, 0, slots, 0, slots.Length);
        
        // Keeping track of the number of 'values' we've processed can be used to avoid doing
        // some expensive 'merge' operations later.
        var valueProcessedCount = 0;
        
        // Start by copying all of the values out of the 'values' and into the slots. 
        // There's no success case where we *don't* use all of the 'values' so there's 
        // no reason not to do this up front to avoid visiting the values dictionary 
        // again and again.
        for (var i = 0; i < slots.Length; i++)
        {
            var key = slots[i].Key;
            if (values.TryGetValue(key, out var value))
            {
                // We will need to know later if the value in the 'values' was an null value.
                // This affects how we process ambient values. Since the 'slots' are 
                // initialized with null values, we use the null-object-pattern to track
                // 'explicit null', which means that null means omitted.
                value = IsRoutePartNonEmpty(value) ? value : SentinullValue.Instance;
                slots[i] = new KeyValuePair<string, object?>(key, value);
                
                // Track the count of processed values - this allows a fast path later.
                valueProcessedCount++;
            }
        }
        
        // In Endpoint Routing, patterns can have logical parameters that appear 
        // 'to the left' of the route template. 
        // This governs whether or not the template can be selected (they act like
        // filters), and whether the remaining ambient values should be used.
        // should be used.
        // For example, in case of MVC it flattens out a route template like below
        //   {controller}/{action}/{id?}
        // to
        //   Products/Index/{id?},
        //   defaults: new { controller = "Products", action = "Index" },
        //   requiredValues: new { controller = "Products", action = "Index" }
        // In the above example, "controller" and "action" are no longer parameters.
        var copyAmbientValues = ambientValues != null;
        if (copyAmbientValues)
        {
            var requiredKeys = _requiredKeys;
            for (var i = 0; i < requiredKeys.Length; i++)
            {
                // For each required key, 
                // the values and ambient values need to have the same value.
                var key = requiredKeys[i];
                var hasExplicitValue = values.TryGetValue(key, out var value);
                
                if (ambientValues == null || 
                    !ambientValues.TryGetValue(key, out var ambientValue))
                {
                    ambientValue = null;
                }
                
                // For now, only check ambient values with required values that don't 
                // have a parameter Ambient values for parameters are processed below
                var hasParameter = _pattern.GetParameter(key) != null;
                if (!hasParameter)
                {
                    if (!_pattern.RequiredValues
                        		 .TryGetValue(key, out var requiredValue))
                    {
                        throw new InvalidOperationException(
                            $"Unable to find required value '{key}' on route pattern.");
                    }
                    
                    if (!RoutePartsEqual(ambientValue, _pattern.RequiredValues[key]) &&
                        !RoutePattern.IsRequiredValueAny(_pattern.RequiredValues[key]))
                    {
                        copyAmbientValues = false;
                        break;
                    }
                    
                    if (hasExplicitValue && 
                        !RoutePartsEqual(value, ambientValue))
                    {
                        copyAmbientValues = false;
                        break;
                    }
                }
            }
        }
        
        // We can now process the rest of the parameters (from left to right) and copy 
        // the ambient values as long as the conditions are met.
        //
        // Find out which entries in the URI are valid for the URI we want to generate.
        // If the URI had ordered parameters a="1", b="2", c="3" and the new values
        // specified that b="9", then we need to invalidate everything after it. The new
        // values should then be a="1", b="9", c=<no value>.
        //
        // We also handle the case where a parameter is optional but has no value - we 
        // shouldn't accept additional parameters that appear *after* that parameter.
        var parameters = _pattern.Parameters;
        var parameterCount = _pattern.Parameters.Count;
        for (var i = 0; i < parameterCount; i++)
        {
            var key = slots[i].Key;
            var value = slots[i].Value;
            
            // Whether or not the value was explicitly provided is signficant when 
            // comparing ambient values. 
            // Remember that we're using a special sentinel value so that we can tell the
            // difference between an omitted value and an explicitly specified null.
            var hasExplicitValue = value != null;
            
            var hasAmbientValue = false;
            var ambientValue = (object?)null;
            
            var parameter = parameters[i];
            
            // We are copying **all** ambient values
            if (copyAmbientValues)
            {
                hasAmbientValue = ambientValues != null && 
                    			  ambientValues.TryGetValue(key, out ambientValue);
                if (hasExplicitValue && 
                    hasAmbientValue && 
                    !RoutePartsEqual(ambientValue, value))
                {
                    // Stop copying current values when we find one that doesn't match
                    copyAmbientValues = false;
                }
                
                if (!hasExplicitValue &&
                    !hasAmbientValue &&
                    _defaults?.ContainsKey(parameter.Name) != true)
                {
                    // This is an unsatisfied parameter value and there are no defaults. 
                    // We might still be able to generate a URL but we should stop 'accepting'
                    // ambient values.
                    //
                    // This might be a case like:
                    //   template: a/{b?}/{c?}
                    //   ambient: { c = 17 }
                    //   values: { }
                    //
                    // We can still generate a URL from this ("/a") but we shouldn't accept 
                    // 'c' because we can't use it.
                    //
                    // In the example above we should fall into this block for 'b'.
                    copyAmbientValues = false;
                }
            }
            
            // This might be an ambient value that matches a required value. 
            // We want to use these even if we're not bulk-copying ambient values.
            //
            // This comes up in a case like the following:
            //   ambient-values: { page = "/DeleteUser", area = "Admin", }
            //   values: { controller = "Home", action = "Index", }
            //   pattern: {area}/{controller}/{action}/{id?}
            //   required-values: { area = "Admin", controller = "Home", action = "Index", 
            //					  page = (string)null, }
            //
            // OR in plain English... when linking from a page in an area to an action in 
            // the same area, it should be possible to use the area as an ambient value.
            if (!copyAmbientValues && 
                !hasExplicitValue && 
                _pattern.RequiredValues.TryGetValue(key, out var requiredValue))
            {
                hasAmbientValue = ambientValues != null && 
                    			  ambientValues.TryGetValue(key, out ambientValue);
                if (hasAmbientValue &&
                    (RoutePartsEqual(requiredValue, ambientValue) || 
                     RoutePattern.IsRequiredValueAny(requiredValue)))
                {
                    // Treat this an an explicit value to *force it*.
                    slots[i] = new KeyValuePair<string, object?>(key, ambientValue);
                    hasExplicitValue = true;
                    value = ambientValue;
                }
            }
            
            // If the parameter is a match, add it to the list of values we will use 
            // for URI generation
            if (hasExplicitValue && 
                !ReferenceEquals(value, SentinullValue.Instance))
            {
                // Already has a value in the list, do nothing
            }
            else if (copyAmbientValues && hasAmbientValue)
            {
                slots[i] = new KeyValuePair<string, object?>(key, ambientValue);
            }
            else if (parameter.IsOptional || parameter.IsCatchAll)
            {
                // Value isn't needed for optional or catchall parameters - wipe out the key, 
                // so it will be omitted from the RVD.
                slots[i] = default;
            }
            else if (_defaults != null && 
                     _defaults.TryGetValue(parameter.Name, out var defaultValue))
            {
                
                // Add the default value only if there isn't already a new value for it and
                // only if it actually has a default value.
                slots[i] = new KeyValuePair<string, object?>(key, defaultValue);
            }
            else
            {      
                // If we get here, this parameter needs a value, but doesn't have one. 
                // This is a failure case.
                return null;
            }
        }
        
        // Any default values that don't appear as parameters are treated like filters. 
        // Any new values provided must match these defaults.
        var filters = _filters;
        for (var i = 0; i < filters.Length; i++)
        {
            var key = filters[i].Key;
            var value = slots[i + parameterCount].Value;
            
            // We use a sentinel value here so we can track the different between omission 
            // and explicit null.
            // 'real null' means that the value was omitted.
            var hasExplictValue = value != null;
            if (hasExplictValue)
            {
                // If there is a non-parameterized value in the route and there is a
                // new value for it and it doesn't match, this route won't match.
                if (!RoutePartsEqual(value, filters[i].Value))
                {
                    return null;
                }
            }
            else
            {
                // If no value was provided, then blank out this slot so that it doesn't 
                // show up in accepted values.
                slots[i + parameterCount] = default;
            }
        }
        
        // At this point we've captured all of the 'known' route values, but we have't
        // handled an extra route values that were provided in 'values'. 
        // These all need to be included in the accepted values.
        var acceptedValues = RouteValueDictionary.FromArray(slots);
        
        if (valueProcessedCount < values.Count)
        {
            // There are some values in 'value' that are unaccounted for, merge them into
            // the dictionary.
            foreach (var kvp in values)
            {
                if (!_defaults!.ContainsKey(kvp.Key))
                {
#if RVD_TryAdd
                    acceptedValues.TryAdd(kvp.Key, kvp.Value);
#else
                    if (!acceptedValues.ContainsKey(kvp.Key))
                    {
                        acceptedValues.Add(kvp.Key, kvp.Value);
                    }
#endif
                }
            }
        }
        
        // Currently this copy is required because BindValues will mutate the accepted 
        // values :(
        var combinedValues = new RouteValueDictionary(acceptedValues);
        
        // Add any ambient values that don't match parameters - they need to be visible 
        // to constraints but they will ignored by link generation.
        CopyNonParameterAmbientValues(
            ambientValues: ambientValues,
            acceptedValues: acceptedValues,
            combinedValues: combinedValues);
        
        return new TemplateValuesResult()
        {
            AcceptedValues = acceptedValues,
            CombinedValues = combinedValues,
        };
    }
    
    // Step 1.5: Process constraints
    // <summary>
    // Processes the constraints **if** they were passed in to the TemplateBinder constructor.
    // </summary>    
    public bool TryProcessConstraints(
        HttpContext? httpContext, 
        RouteValueDictionary combinedValues, 
        out string? parameterName, 
        out IRouteConstraint? constraint)
    {
        var constraints = _constraints;
        for (var i = 0; i < constraints.Length; i++)
        {
            (parameterName, constraint) = constraints[i];
            
            if (!constraint.Match(
                				httpContext, 
                				NullRouter.Instance, 
                				parameterName, 
                				combinedValues, 
                				RouteDirection.UrlGeneration))
            {
                return false;
            }
        }
        
        parameterName = null;
        constraint = null;
        return true;
    }
    
        // Step 2: If the route is a match generate the appropriate URI
        /// <summary>
        /// Returns a string representation of the URI associated with the route.
        /// </summary>
        /// <param name="acceptedValues">A dictionary that contains the parameters for the route.</param>
        /// <returns>The string representation of the route.</returns>
    public string? BindValues(RouteValueDictionary acceptedValues)
    {
        var context = _pool.Get();
        
        try
        {
            return TryBindValuesCore(context, acceptedValues) 
                	   ? context.ToString() 
                	   : null;
        }
        finally
        {
            _pool.Return(context);
        }
    }
    
    // Step 2: If the route is a match generate the appropriate URI
    internal bool TryBindValues(
        RouteValueDictionary acceptedValues,
        LinkOptions? options,
        LinkOptions globalOptions,
        out (PathString path, QueryString query) result)
    {
        var context = _pool.Get();
        
        context.AppendTrailingSlash = 
            options?.AppendTrailingSlash 
            	   ?? globalOptions.AppendTrailingSlash 
            	   ?? false;
        context.LowercaseQueryStrings = 
            options?.LowercaseQueryStrings 
            	   ?? globalOptions.LowercaseQueryStrings 
            	   ?? false;
        context.LowercaseUrls = 
            options?.LowercaseUrls 
            	   ?? globalOptions.LowercaseUrls 
	               ?? false;

        try
        {
            if (TryBindValuesCore(context, acceptedValues))
            {
                result = (context.ToPathString(), context.ToQueryString());
                return true;
            }
            
            result = default;
            return false;
        }
        finally
        {
            _pool.Return(context);
        }
    }
    
    private bool TryBindValuesCore(
        UriBuildingContext context, 
        RouteValueDictionary acceptedValues)
    {
        // If we have any output parameter transformers, allow them a chance to influence 
        // the parameter values before we build the URI.
        var parameterTransformers = _parameterTransformers;
        for (var i = 0; i < parameterTransformers.Length; i++)
        {
            (var parameterName, var transformer) = parameterTransformers[i];
            if (acceptedValues.TryGetValue(parameterName, out var value))
            {
                acceptedValues[parameterName] = transformer.TransformOutbound(value);
            }
        }
        
        var segments = _pattern.PathSegments;
        // Read interface .Count once rather than per iteration
        var segmentsCount = segments.Count;
        for (var i = 0; i < segmentsCount; i++)
        {
            Debug.Assert(context.BufferState == SegmentState.Beginning);
            Debug.Assert(context.UriState == SegmentState.Beginning);
            
            var parts = segments[i].Parts;
            // Read interface .Count once rather than per iteration
            var partsCount = parts.Count;
            for (var j = 0; j < partsCount; j++)
            {
                var part = parts[j];
                if (part is RoutePatternLiteralPart literalPart)
                {
                    if (!context.Accept(literalPart.Content))
                    {
                        return false;
                    }
                }
                else if (part is RoutePatternSeparatorPart separatorPart)
                {
                    if (!context.Accept(separatorPart.Content))
                    {
                        return false;
                    }
                }
                else if (part is RoutePatternParameterPart parameterPart)
                {
                    // If it's a parameter, get its value
                    acceptedValues.Remove(parameterPart.Name, out var value);
                    
                    var isSameAsDefault = false;
                    if (_defaults != null &&
                        _defaults.TryGetValue(parameterPart.Name, out var defaultValue) &&
                        RoutePartsEqual(value, defaultValue))
                    {
                        isSameAsDefault = true;
                    }
                    
                    var converted = Convert.ToString(value, CultureInfo.InvariantCulture);
                    if (isSameAsDefault)
                    {
                        // If the accepted value is the same as the default value buffer 
                        // it since we won't necessarily add it to the URI we generate.
                        if (!context.Buffer(converted))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        // If the value is not accepted, it is null or empty value in the
                        // middle of the segment. We accept this if the parameter is an
                        // optional parameter and it is preceded by an optional seperator.
                        // In this case, we need to remove the optional seperator that we
                        // have added to the URI
                        // Example: template = {id}.{format?}. parameters: id=5
                        // In this case after we have generated "5.", we wont find any value
                        // for format, so we remove '.' and generate 5.
                        if (!context.Accept(converted, parameterPart.EncodeSlashes))
                        {
                            RoutePatternSeparatorPart? nullablePart;
                            if (j != 0 && 
                                parameterPart.IsOptional && 
                                (nullablePart = parts[j - 1] as 
                                     RoutePatternSeparatorPart) != null)
                                {
                                    separatorPart = nullablePart;
                                    context.Remove(separatorPart.Content);
                                }
                                else
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }

            context.EndSegment();
        }
        
        // Generate the query string from the remaining values
        var wroteFirst = false;
        foreach (var kvp in acceptedValues)
        {
            if (_defaults != null && 
                _defaults.ContainsKey(kvp.Key))
            {
                // This value is a 'filter' we don't need to put it in the query string.
                continue;
            }
            
            var values = kvp.Value as IEnumerable;
            if (values != null && 
                !(values is string))
            {
                foreach (var value in values)
                {
                    wroteFirst |= AddQueryKeyValueToContext(
                        context, 
                        kvp.Key, 
                        value, 
                        wroteFirst);
                }
            }
            else
            {
                wroteFirst |= AddQueryKeyValueToContext(
                    context, 
                    kvp.Key, 
                    kvp.Value, 
                    wroteFirst);
            }
        }
        
        return true;
    }
    
    private bool AddQueryKeyValueToContext(
        UriBuildingContext context, 
        string key, 
        object? value, 
        bool wroteFirst)
    {
        var converted = Convert.ToString(value, CultureInfo.InvariantCulture);
        if (!string.IsNullOrEmpty(converted))
        {
            if (context.LowercaseQueryStrings)
            {
                key = key.ToLowerInvariant();
                converted = converted.ToLowerInvariant();
            }
            
            context.QueryWriter.Write(wroteFirst ? '&' : '?');
            _urlEncoder.Encode(context.QueryWriter, key);
            context.QueryWriter.Write('=');
            _urlEncoder.Encode(context.QueryWriter, converted);
            return true;
        }
        return false;
    }
    
    /// <summary>
        /// Compares two objects for equality as parts of a case-insensitive path.
        /// </summary>
        /// <param name="a">An object to compare.</param>
        /// <param name="b">An object to compare.</param>
        /// <returns>True if the object are equal, otherwise false.</returns>
    public static bool RoutePartsEqual(object? a, object? b)
    {
        var sa = a as string ?? (ReferenceEquals(SentinullValue.Instance, a) 
                                 	? string.Empty 
                                 	: null);
        var sb = b as string ?? (ReferenceEquals(SentinullValue.Instance, b) 
                                 	? string.Empty 
                                 	: null);
        
        // In case of strings, consider empty and null the same.
        // Since null cannot tell us the type, consider it to be a string if the other 
        // value is a string.
        if ((sa == string.Empty && sb == null) || 
            (sb == string.Empty && sa == null))
        {
            return true;
        }
        else if (sa != null && sb != null)
        {
            // For strings do a case-insensitive comparison
            return string.Equals(sa, sb, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            if (a != null && b != null)
            {
                // Explicitly call .Equals() in case it is overridden in the type
                return a.Equals(b);
            }
            else
            {
                // At least one of them is null. Return true if they both are
                return a == b;
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsRoutePartNonEmpty(object? part)
    {
        if (part == null)
        {
            return false;
        }
        
        if (ReferenceEquals(SentinullValue.Instance, part))
        {
            return false;
        }
        
        if (part is string stringPart && stringPart.Length == 0)
        {
            return false;
        }
        
        return true;
    }
    
    private void CopyNonParameterAmbientValues(
        RouteValueDictionary? ambientValues,
        RouteValueDictionary acceptedValues,
        RouteValueDictionary combinedValues)
    {
        if (ambientValues == null)
        {
            return;
        }
        
        foreach (var kvp in ambientValues)
        {
            if (IsRoutePartNonEmpty(kvp.Value))
            {
                var parameter = _pattern.GetParameter(kvp.Key);
                if (parameter == null && !acceptedValues.ContainsKey(kvp.Key))
                {
                    combinedValues.Add(kvp.Key, kvp.Value);
                }
            }
        }
    }
    
    private static KeyValuePair<string, object?>[] AssignSlots(
        RoutePattern pattern, 
        KeyValuePair<string, object?>[] filters)
    {
        var slots = 
            new KeyValuePair<string, object?>[pattern.Parameters.Count + filters.Length];
        
        for (var i = 0; i < pattern.Parameters.Count; i++)
        {
            slots[i] = new KeyValuePair<string, object?>(
                pattern.Parameters[i].Name, 
                null);
        }
        
        for (var i = 0; i < filters.Length; i++)
        {
            slots[i + pattern.Parameters.Count] = 
                new KeyValuePair<string, object?>(filters[i].Key, null);
        }
        
        return slots;
    }
    
    // This represents an 'explicit null' in the slots array.
    [DebuggerDisplay("explicit null")]
    private class SentinullValue
    {
        public static object Instance = new SentinullValue();
        
        private SentinullValue()
        {
        }
        
        public override string ToString() => string.Empty;
    }
}

```

###### 2.2.4.a template value result

```c#
public class TemplateValuesResult
{    
    public RouteValueDictionary AcceptedValues { get; set; } = default!;            
    public RouteValueDictionary CombinedValues { get; set; } = default!;
}

```

##### 2.2.5 template binder factory

###### 2.2.5.1 抽象基类

```c#
public abstract class TemplateBinderFactory
{    
    public abstract TemplateBinder Create(RoutePattern pattern);
    public abstract TemplateBinder Create(
        RouteTemplate template, 
        RouteValueDictionary defaults);            
}

```

###### 2.2.5.2 default template binder factory

```c#
internal sealed class DefaultTemplateBinderFactory : TemplateBinderFactory
{
    private readonly ParameterPolicyFactory _policyFactory;
    private readonly ObjectPool<UriBuildingContext> _pool;
    
    public DefaultTemplateBinderFactory(
        ParameterPolicyFactory policyFactory,
        ObjectPool<UriBuildingContext> pool)
    {
        if (policyFactory == null)
        {
            throw new ArgumentNullException(nameof(policyFactory));
        }        
        if (pool == null)
        {
            throw new ArgumentNullException(nameof(pool));
        }
        
        _policyFactory = policyFactory;
        _pool = pool;
        
    }
    
    public override TemplateBinder Create(
        RouteTemplate template, 
        RouteValueDictionary defaults)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }        
        if (defaults == null)
        {
            throw new ArgumentNullException(nameof(defaults));
        }
        
        return new TemplateBinder(
            UrlEncoder.Default, 
            _pool, 
            template, 
            defaults);
    }
    
    public override TemplateBinder Create(RoutePattern pattern)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }
        
        // Now create the constraints and parameter transformers from the pattern
        var policies = new List<(string parameterName, IParameterPolicy policy)>();
        foreach (var kvp in pattern.ParameterPolicies)
        {
            var parameterName = kvp.Key;
            
            // It's possible that we don't have an actual route parameter, 
            // we need to support that case.
            var parameter = pattern.GetParameter(parameterName);
            
            // Use the first parameter transformer per parameter
            var foundTransformer = false;
            for (var i = 0; i < kvp.Value.Count; i++)
            {
                var parameterPolicy = _policyFactory.Create(parameter, kvp.Value[i]);
                
                if (!foundTransformer && 
                    parameterPolicy is IOutboundParameterTransformer parameterTransformer)
                {
                    policies.Add((parameterName, parameterTransformer));
                    foundTransformer = true;
                }
                
                if (parameterPolicy is IRouteConstraint constraint)
                {
                    policies.Add((parameterName, constraint));
                }
            }
        }
        
        return new TemplateBinder(
            UrlEncoder.Default, 
            _pool, 
            pattern, 
            policies);
    }
}

```

###### ?2.1.8.3 inline constraint

* inline constraint string 的封装，
* 可以由`route pattern parameter policy reference`构建

```c#
public class InlineConstraint
    {
        /// <summary>
        /// Creates a new instance of <see cref="InlineConstraint"/>.
        /// </summary>
        /// <param name="constraint">The constraint text.</param>
        public InlineConstraint(string constraint)
        {
            if (constraint == null)
            {
                throw new ArgumentNullException(nameof(constraint));
            }

            Constraint = constraint;
        }

        /// <summary>
        /// Creates a new <see cref="InlineConstraint"/> instance given a <see cref="RoutePatternParameterPolicyReference"/>.
        /// </summary>
        /// <param name="other">A <see cref="RoutePatternParameterPolicyReference"/> instance.</param>
        public InlineConstraint(RoutePatternParameterPolicyReference other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            Constraint = other.Content!;
        }

        /// <summary>
        /// Gets the constraint text.
        /// </summary>
        public string Constraint { get; }
    }
```





#### 2.3 parameter policy

##### 2.3.1 接口

```c#
public interface IParameterPolicy
{
}

```

##### 2.3.2 parameter policy activator

```c#
internal static class ParameterPolicyActivator
{
    public static T ResolveParameterPolicy<T>(
        IDictionary<string, Type> inlineParameterPolicyMap,
        IServiceProvider serviceProvider,
        string inlineParameterPolicy,
        out string parameterPolicyKey)            where T : IParameterPolicy
    {
        // IServiceProvider could be null
        // DefaultInlineConstraintResolver can be created without an IServiceProvider 
        // and then call this method
        
        if (inlineParameterPolicyMap == null)
        {
            throw new ArgumentNullException(nameof(inlineParameterPolicyMap));
        }        
        if (inlineParameterPolicy == null)
        {
            throw new ArgumentNullException(nameof(inlineParameterPolicy));
        }
        
        // 创建 arguments（预结果）
        string argumentString;
        
        var indexOfFirstOpenParens = inlineParameterPolicy.IndexOf('(');
        
        /* 如果 inline policy string 包含“（”和“）”，即包含 argument */
        if (indexOfFirstOpenParens >= 0 && 
            inlineParameterPolicy.EndsWith(")", StringComparison.Ordinal))
        {
            // 截取 policy key（“（”之前的部分）
            parameterPolicyKey = 
                inlineParameterPolicy.Substring(
                	0, 
                	indexOfFirstOpenParens);
            // 截取 argument（“（xxx）“的部分）
            argumentString = 
                inlineParameterPolicy.Substring(
                	indexOfFirstOpenParens + 1,
	                inlineParameterPolicy.Length - indexOfFirstOpenParens - 2);
        }
        /* 否则，inline policy 就是 parameter key，argument 为 null  */
        else
        {
            parameterPolicyKey = inlineParameterPolicy;
            argumentString = null;
        }
        
        // 在 inline policy map 中查找 parameter policy key 对应的 type，
        // 如果找不到，返回 default
        if (!inlineParameterPolicyMap.TryGetValue(
            	parameterPolicyKey, 
            	out var parameterPolicyType))
        {
            return default;
        }
        
        /* 如果 T 没有继承 parameter policy type，*/
        if (!typeof(T).IsAssignableFrom(parameterPolicyType))
        {
            /* 如果 parameter policy type 没有实现 parameter policy 接口，错误 */
            if (!typeof(IParameterPolicy).IsAssignableFrom(parameterPolicyType))
            {
                // Error if type is not a parameter policy
                throw new RouteCreationException(
                    Resources.FormatDefaultInlineConstraintResolver
                    		  _TypeNotConstraint(
                                  parameterPolicyType, 
                                  parameterPolicyKey, typeof(T).Name));
            }
            
            /* parameter policy type 实现了 parameter policy接口，创建 default 并返回 */
            // Return null if type is parameter policy but is not the exact type
            // This is used by IInlineConstraintResolver for backwards compatibility
            // e.g. looking for an IRouteConstraint but get a different IParameterPolicy type
            return default;
        }
        
        /* T 继承了 parameter policy type */
        try
        {
            // 创建 parameter policy 并转换为 T
            return (T) CreateParameterPolicy(
                serviceProvider, 
                parameterPolicyType, 
                argumentString);
        }
        catch (RouteCreationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new RouteCreationException(
                $"An error occurred while trying to create an instance of 
                '{parameterPolicyType.FullName}'.",
                exception);
        }
    }                          
}

```

###### 2.3.2.1 create paremeter policy

```c#
internal static class ParameterPolicyActivator
{
    [UnconditionalSuppressMessage(
        "ReflectionAnalysis", 
        "IL2006:UnrecognizedReflectionPattern", 
        Justification = "This type comes from the ConstraintMap.")]
    private static IParameterPolicy CreateParameterPolicy(
        IServiceProvider serviceProvider, 
        Type parameterPolicyType, 
        string argumentString)
    {
        ConstructorInfo activationConstructor = null;
        object[] parameters = null;
        
        /* 获取 parameter policy type 所有的 constructor*/
        var constructors = parameterPolicyType.GetConstructors();
        
        // If there is only one constructor and it has a single parameter, pass the 
        // argument string directly
        // This is necessary for the Regex RouteConstraint to ensure that patterns are 
        // not split on commas.
        
        /* 如果只有1个 constructor，且 constructor 只有1个 parameter，*/
        if (constructors.Length == 1 && 
            GetNonConvertableParameterTypeCount(
                serviceProvider, 
                constructors[0].GetParameters()) == 1)
        {
            // 获取 constructor
            activationConstructor = constructors[0];
            // 从 argument string 解析 parameter
            parameters = ConvertArguments(
                serviceProvider, 
                activationConstructor.GetParameters(), 
                new string[] { argumentString });
        }
        /* 由多个 constructor，*/
        else
        {
            // 把 argument string 按照 “,“ 分割 -> arguments
            var arguments = argumentString?.Split(
                ',', 
                StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
            
            // We want to find the constructors that match the number of passed in arguments
            // We either want a single match, or a single best match. 
            // The best match is the one with the most arguments that can be resolved from DI
            //
            // For example, ctor(string, IService) will beat ctor(string)
            
            /* 过滤与 arguments 数量相同的 constructor 并按照 argument 数量降序 */
            var matchingConstructors = 
                constructors.Where(ci => 
                                   	   GetNonConvertableParameterTypeCount(
                                           serviceProvider, 
                                           ci.GetParameters()) == arguments.Length)
                			.OrderByDescending(ci => 
                                               	   ci.GetParameters().Length)
                			.ToArray();

            /* 如果没有匹配的 constructor，抛出异常 */
            if (matchingConstructors.Length == 0)
            {
                throw new RouteCreationException(
                    Resources.FormatDefaultInlineConstraintResolver
                    		  _CouldNotFindCtor(
                                  parameterPolicyType.Name, 
                                  arguments.Length));
            }
            /* 有匹配的 constructor，*/
            else
            {
                // When there are multiple matching constructors, 
                // choose the one with the most service arguments
                
                /* 如果只有1个匹配的 constructor，
                   或者 1st_constructor argument 数量 > 2nd_constructor argument 数量，
                   即没有相同数量的 constructors（constructors已经降序排序）。。。 */             
                if (matchingConstructors.Length == 1 || 
                    atchingConstructors[0].GetParameters()
                    					  .Length > 
                    matchingConstructors[1].GetParameters()
                    					   .Length)
                {
                    // 获取 constructor
                    activationConstructor = matchingConstructors[0];
                }
                /* 否则，即有多个 constructor，
                   且不止一个 argument 数量最多的 constructor（argument 数量相同），异常 */
                else
                {
                    throw new RouteCreationException(
                        Resources.FormatDefaultInlineConstraintResolver
                        		  _AmbiguousCtors(
                                      parameterPolicyType.Name, 
                                      matchingConstructors[0].GetParameters()
                                      						 .Length));
                }
                
                // 从 argument 解析 parameters
                parameters = ConvertArguments(
                    serviceProvider, 
                    activationConstructor.GetParameters(), 
                    arguments);
            }
        }
        
        /* 执行 constructor */
        return (IParameterPolicy)activationConstructor.Invoke(parameters);
    }
}

```

###### 2.3.2.2 convert & convertible parameter

```c#
internal static class ParameterPolicyActivator
{
    private static int GetNonConvertableParameterTypeCount(
        IServiceProvider serviceProvider, 
        ParameterInfo[] parameters)
    {
        if (serviceProvider == null)
        {
            return parameters.Length;
        }
        
        var count = 0;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (typeof(IConvertible).IsAssignableFrom(parameters[i].ParameterType))
            {
                count++;
            }
        }
        
        return count;
    }
    
    private static object[] ConvertArguments(
        IServiceProvider serviceProvider, 
        ParameterInfo[] parameterInfos, 
        string[] arguments)
    {
        var parameters = new object[parameterInfos.Length];
        var argumentPosition = 0;
        for (var i = 0; i < parameterInfos.Length; i++)
        {
            var parameter = parameterInfos[i];
            var parameterType = parameter.ParameterType;
            
            if (serviceProvider != null && 
                !typeof(IConvertible).IsAssignableFrom(parameterType))
            {
                parameters[i] = serviceProvider.GetRequiredService(parameterType);
            }
            else
            {
                parameters[i] = Convert.ChangeType(
                    arguments[argumentPosition], 
                    parameterType, 
                    CultureInfo.InvariantCulture);
                
                argumentPosition++;
            }
        }
        
        return parameters;
    }
}
```



##### 2.3.3 parameter policy factory

###### 2.3.3.1 抽象基类

```c#
public abstract class ParameterPolicyFactory
{                
    public IParameterPolicy Create(
        RoutePatternParameterPart? parameter, 
        RoutePatternParameterPolicyReference reference)
    {
        if (reference == null)
        {
            throw new ArgumentNullException(nameof(reference));
        }
        
        Debug.Assert(reference.ParameterPolicy != null || 
                     reference.Content != null);
        
        // 由 reference 的 parameter policy 创建 parameter policy
        if (reference.ParameterPolicy != null)
        {
            return Create(parameter, reference.ParameterPolicy);
        }
        
        // 由 reference 的 content (string) 创建 parameter policy
        if (reference.Content != null)
        {
            return Create(parameter, reference.Content);
        }
        
        // Unreachable
        throw new NotSupportedException();
    }
    
    // 由 string 创建 parameter policy，在派生类中实现
    public abstract IParameterPolicy Create(
        RoutePatternParameterPart? parameter, 
        string inlineText);
    
    // 由 parameter policy 创建 parameter policy，在派生类中实现    
    public abstract IParameterPolicy Create(
        RoutePatternParameterPart? parameter, 
        IParameterPolicy parameterPolicy);
    
}

```

###### 2.3.3.2 default parameter policy factory

```c#
internal class DefaultParameterPolicyFactory : ParameterPolicyFactory
{
    private readonly RouteOptions _options;
    private readonly IServiceProvider _serviceProvider;
    
    public DefaultParameterPolicyFactory(
        IOptions<RouteOptions> options,
        IServiceProvider serviceProvider)
    {
        _options = options.Value;
        _serviceProvider = serviceProvider;
    }
    
    /* 由 string 创建 parameter policy */
    public override IParameterPolicy Create(
        RoutePatternParameterPart? parameter, 
        string inlineText)
    {
        if (inlineText == null)
        {
            throw new ArgumentNullException(nameof(inlineText));
        }
        
        // 由 parameter policy activator 创建 parameter policy
        var parameterPolicy = 
            ParameterPolicyActivator.ResolveParameterPolicy<IParameterPolicy>(
            	_options.ConstraintMap,
	            _serviceProvider,
    	        inlineText,
        	    out var parameterPolicyKey);
        // parameter policy 为 null，抛出异常
        if (parameterPolicy == null)
        {
            throw new InvalidOperationException(
                Resources.FormatRoutePattern_ConstraintReferenceNotFound(
                    parameterPolicyKey,
                    typeof(RouteOptions),
                    nameof(RouteOptions.ConstraintMap)));
        }
        
        /* 如果 parameter policy 实现了 route constraint 接口 */
        if (parameterPolicy is IRouteConstraint constraint)
        {
            // 初始化 route constraint
            return InitializeRouteConstraint(
                parameter?.IsOptional ?? false, 
                constraint);
        }
        
        return parameterPolicy;
    }
    
    /* 由 parameter policy 创建 parameter policy */
    public override IParameterPolicy Create(
        RoutePatternParameterPart? parameter, 
        IParameterPolicy parameterPolicy)
    {
        if (parameterPolicy == null)
        {
            throw new ArgumentNullException(nameof(parameterPolicy));
        }
        
        if (parameterPolicy is IRouteConstraint routeConstraint)
        {
            return InitializeRouteConstraint(
                parameter?.IsOptional ?? false, 
                routeConstraint);
        }
        
        return parameterPolicy;
    }
    
    /* 初始化 route constraint*/    
    private IParameterPolicy InitializeRouteConstraint(
        bool optional,
        IRouteConstraint routeConstraint)
    {
        if (optional)
        {
            routeConstraint = new OptionalRouteConstraint(routeConstraint);
        }
        
        return routeConstraint;
    }
}

```

##### 2.3.4 outbound parameter transformer

###### 2.3.4.1 接口

```c#
public interface IOutboundParameterTransformer : IParameterPolicy
{    
    string? TransformOutbound(object? value);
}

```







##### 2.3.5 route constraint

###### 2.3.5.1 接口

```c#
public interface IRouteConstraint : IParameterPolicy
{    
    bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection);
}

public enum RouteDirection
{    
    IncomingRequest,        
    UrlGeneration,
}

```

###### 2.3.5.2 required route constraint

```c#
public class RequiredRouteConstraint : IRouteConstraint
{
    /// <inheritdoc />
    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection)
    {
        if (routeKey == null)
        {
            throw new ArgumentNullException(nameof(routeKey));
        }        
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }
        
        if (values.TryGetValue(routeKey, out var value) && 
            value != null)
        {
            // In routing the empty string is equivalent to null, 
            // which is equivalent to an unset value.
            var valueString = Convert.ToString(value, CultureInfo.InvariantCulture);
            return !string.IsNullOrEmpty(valueString);
        }
        
        return false;
    }
}

```

###### 2.3.5.3 optional route constraint

```c#
public class OptionalRouteConstraint : IRouteConstraint
{
    public IRouteConstraint InnerConstraint { get; }
    
    public OptionalRouteConstraint(IRouteConstraint innerConstraint)
    {
        if (innerConstraint == null)
        {
            throw new ArgumentNullException(nameof(innerConstraint));
        }
        
        InnerConstraint = innerConstraint;
    }
        
    /// <inheritdoc />
    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection)
    {
        if (routeKey == null)
        {
            throw new ArgumentNullException(nameof(routeKey));
        }        
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }
        
        if (values.TryGetValue(routeKey, out var value))
        {
            return InnerConstraint.Match(
                httpContext,
                route,
                routeKey,
                values,
                routeDirection);
        }
        
        return true;
    }
}

```

###### 2.3.5.4 composite route constraint

```c#
public class CompositeRouteConstraint : IRouteConstraint
{
    public IEnumerable<IRouteConstraint> Constraints { get; private set; }
    
    public CompositeRouteConstraint(IEnumerable<IRouteConstraint> constraints)
    {
        if (constraints == null)
        {
            throw new ArgumentNullException(nameof(constraints));
        }
        
        Constraints = constraints;
    }
    
    /// <inheritdoc />
    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection)
    {
        if (routeKey == null)
        {
            throw new ArgumentNullException(nameof(routeKey));
        }        
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }
        
        foreach (var constraint in Constraints)
        {
            if (!constraint.Match(
                	httpContext, 
                	route, 
                	routeKey, 
                	values, 
                	routeDirection))
            {
                return false;
            }
        }
        
        return true;
    }
}

```

###### 2.3.5.5 其他



##### 2.3.6 route constraint builder

```c#
public class RouteConstraintBuilder
{
    private readonly IInlineConstraintResolver _inlineConstraintResolver;
    private readonly string _displayName;  
    
    // constraint dictionary 容器
    private readonly Dictionary<string, List<IRouteConstraint>> _constraints;
    // constraint name 容器，for optional constraint
    private readonly HashSet<string> _optionalParameters;
    
    public RouteConstraintBuilder(
        IInlineConstraintResolver inlineConstraintResolver,
        string displayName)
    {
        if (inlineConstraintResolver == null)
        {
            throw new ArgumentNullException(nameof(inlineConstraintResolver));
        }        
        if (displayName == null)
        {
            throw new ArgumentNullException(nameof(displayName));
        }
        
        _inlineConstraintResolver = inlineConstraintResolver;
        _displayName = displayName;
        
        _constraints = 
            new Dictionary<string, List<IRouteConstraint>>(StringComparer.OrdinalIgnoreCase);
        _optionalParameters = 
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }        
}

```

###### 2.3.6.1 add

```c#
public class RouteConstraintBuilder
{
    private void Add(string key, IRouteConstraint constraint)
    {
        if (!_constraints.TryGetValue(key, out var list))
        {
            list = new List<IRouteConstraint>();
            _constraints.Add(key, list);
        }
        
        list.Add(constraint);
    }
                          
    // 注入 constraint，value 实现了 IRouteConstraint 接口
    public void AddConstraint(string key, object value)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }        
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        
        // 参数 value 转换为 IRouteConstraint
        var constraint = value as IRouteConstraint;
        
        // 如果无法转换，由 value 创建 regex constaint
        if (constraint == null)
        {
            var regexPattern = value as string;
            if (regexPattern == null)
            {
                throw new RouteCreationException(
                    Resources.FormatRouteConstraintBuilder
                    		  _ValidationMustBeStringOrCustomConstraint(
                                  key,
                                  value,
                                  _displayName,
                                  typeof(IRouteConstraint)));
            }
            
            var constraintsRegEx = "^(" + regexPattern + ")$";
            constraint = new RegexRouteConstraint(constraintsRegEx);
        }
        
        // 注入
        Add(key, constraint);
    }
         
    // 注入 constraint，由 inline constraint resolver 解析 string
    public void AddResolvedConstraint(string key, string constraintText)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }        
        if (constraintText == null)
        {
            throw new ArgumentNullException(nameof(constraintText));
        }
        
        // 将 constraint text（string）解析
        var constraint = _inlineConstraintResolver.ResolveConstraint(constraintText);
        // 如果解析失败，抛出异常
        if (constraint == null)
        {
            throw new InvalidOperationException(
                Resources.FormatRouteConstraintBuilder
                		  _CouldNotResolveConstraint(
                              key,
                              constraintText,
                              _displayName,
                              _inlineConstraintResolver.GetType().Name));
        }
        // 如果解析为 null route constraint，返回
        else if (constraint == NullRouteConstraint.Instance)
        {
            // A null route constraint can be returned for other parameter policy types
            return;
        }
        
        // 注入
        Add(key, constraint);
    }
}

```

###### 2.3.6.2 set optional

```c#
public class RouteConstraintBuilder
{
    public void SetOptional(string key)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }
        
        _optionalParameters.Add(key);
    }
}

```

###### 2.3.6.3 build

```c#
public class RouteConstraintBuilder
{
    public IDictionary<string, IRouteConstraint> Build()
    {        
        var constraints = 
            new Dictionary<string, IRouteConstraint>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var kvp in _constraints)
        {
            IRouteConstraint constraint;
            if (kvp.Value.Count == 1)
            {
                constraint = kvp.Value[0];
            }
            else
            {
                constraint = new CompositeRouteConstraint(kvp.Value.ToArray());
            }
            
            if (_optionalParameters.Contains(kvp.Key))
            {
                var optionalConstraint = new OptionalRouteConstraint(constraint);
                constraints.Add(kvp.Key, optionalConstraint);
            }
            else
            {
                constraints.Add(kvp.Key, constraint);
            }
        }
        
        return constraints;
    }
}

```

##### 2.3.7 inline constraint 

###### 2.3.7.1 inline constraint

```c#
public class InlineConstraint
{
    public string Constraint { get; }
    
    public InlineConstraint(string constraint)
    {
        if (constraint == null)
        {
            throw new ArgumentNullException(nameof(constraint));
        }
        
        Constraint = constraint;
    }
        
    public InlineConstraint(RoutePatternParameterPolicyReference other)
    {
        if (other == null)
        {
            throw new ArgumentNullException(nameof(other));
        }
        
        Constraint = other.Content!;
    }            
}

```

###### 2.3.7.2 inline constraint resolver 接口

```c#
public interface IInlineConstraintResolver
{    
    IRouteConstraint? ResolveConstraint(string inlineConstraint);
}

```

###### 2.3.7.3 default inline constraint resolver

```c#
public class DefaultInlineConstraintResolver : IInlineConstraintResolver
{
    private readonly IDictionary<string, Type> _inlineConstraintMap;
    private readonly IServiceProvider _serviceProvider;
            
    public DefaultInlineConstraintResolver(
        IOptions<RouteOptions> routeOptions, 
        IServiceProvider serviceProvider)
    {
        if (routeOptions == null)
        {
            throw new ArgumentNullException(nameof(routeOptions));
        }        
        if (serviceProvider == null)
        {
            throw new ArgumentNullException(nameof(serviceProvider));
        }
        
        _inlineConstraintMap = routeOptions.Value.ConstraintMap;
        _serviceProvider = serviceProvider;
    }

        /// <inheritdoc />
        /// <example>
        /// A typical constraint looks like the following
        /// "exampleConstraint(arg1, arg2, 12)".
        /// Here if the type registered for exampleConstraint has a single constructor with one argument,
        /// The entire string "arg1, arg2, 12" will be treated as a single argument.
        /// In all other cases arguments are split at comma.
        /// </example>
    public virtual IRouteConstraint? ResolveConstraint(string inlineConstraint)
    {
        if (inlineConstraint == null)
        {
            throw new ArgumentNullException(nameof(inlineConstraint));
        }
        
        // This will return null if the text resolves to a non-IRouteConstraint
        return ParameterPolicyActivator.ResolveParameterPolicy<IRouteConstraint>(
            _inlineConstraintMap,
            _serviceProvider,
            inlineConstraint,
            out _);
    }
}

```

##### 2.3.8 route constraint matcher

```c#
public static class RouteConstraintMatcher
{   
    public static bool Match(
        IDictionary<string, IRouteConstraint> constraints,
        RouteValueDictionary routeValues,
        HttpContext httpContext,
        IRouter route,
        RouteDirection routeDirection,
        ILogger logger)
    {
        if (routeValues == null)
        {
            throw new ArgumentNullException(nameof(routeValues));
        }        
        if (httpContext == null)
        {
            throw new ArgumentNullException(nameof(httpContext));
        }        
        if (route == null)
        {
            throw new ArgumentNullException(nameof(route));
        }        
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }        
        if (constraints == null || constraints.Count == 0)
        {
            return true;
        }
        
        foreach (var kvp in constraints)
        {
            var constraint = kvp.Value;
            if (!constraint.Match(
                	httpContext, 
                	route, 
                	kvp.Key, 
                	routeValues, 
                	routeDirection))
            {
                if (routeDirection.Equals(RouteDirection.IncomingRequest))
                {
                    routeValues.TryGetValue(kvp.Key, out var routeValue);                    
                    logger.ConstraintNotMatched(routeValue!, kvp.Key, kvp.Value);
                }
                
                return false;
            }
        }
        
        return true;
    }
}

```

#### 2.4 router

##### 2.4.1 router 接口

```c#
public interface IRouter
{
    // （正向）路由，从 url 路由到 (controller)
    Task RouteAsync(RouteContext context);
    
    // （逆向）路由，从 (controller) 返回 url    
    VirtualPathData? GetVirtualPath(VirtualPathContext context);
}

```

###### 2.4.1.1 route context

```c#
public class RouteContext
{
    private RouteData _routeData;
    public RouteData RouteData
    {
        get
        {
            return _routeData;
        }
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(RouteData));
            }
            
            _routeData = value;
        }
    }
    
    public HttpContext HttpContext { get; }
    public RequestDelegate? Handler { get; set; }
                    
    public RouteContext(HttpContext httpContext)
    {
        HttpContext = httpContext 
            ?? throw new ArgumentNullException(nameof(httpContext));     
        RouteData = new RouteData();
    }            
}

```

###### 2.4.1.2 route data

```c#
public class RouteData
{
    private RouteValueDictionary? _dataTokens;
    public RouteValueDictionary DataTokens
    {
        get
        {
            if (_dataTokens == null)
            {
                _dataTokens = new RouteValueDictionary();
            }
            
            return _dataTokens;
        }
    }
    
    private List<IRouter>? _routers;
    public IList<IRouter> Routers
    {
        get
        {
            if (_routers == null)
            {
                _routers = new List<IRouter>();
            }
            
            return _routers;
        }
    }
    
    private RouteValueDictionary? _values;
    public RouteValueDictionary Values
    {
        get
        {
            if (_values == null)
            {
                _values = new RouteValueDictionary();
            }
            
            return _values;
        }
    }
                    
    public RouteData()
    {
        // Perf: Avoid allocating collections unless needed.
    }
            
    public RouteData(RouteData other)
    {
        if (other == null)
        {
            throw new ArgumentNullException(nameof(other));
        }
        
        // Perf: Avoid allocating collections unless we need to make a copy.
        
        if (other._routers != null)
        {
            _routers = new List<IRouter>(other.Routers);
        }        
        if (other._dataTokens != null)
        {
            _dataTokens = new RouteValueDictionary(other._dataTokens);
        }        
        if (other._values != null)
        {
            _values = new RouteValueDictionary(other._values);
        }
    }

    public RouteData(RouteValueDictionary values)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }
        
        _values = values;
    }
        
    public RouteDataSnapshot PushState(
        IRouter? router, 
        RouteValueDictionary? values, 
        RouteValueDictionary? dataTokens)
    {
        /* 克隆 routers 集合 */
        // Perf: this is optimized for small list sizes, in particular to avoid overhead 
        // of a native call in Array.CopyTo inside the List(IEnumerable<T>) constructor.       
        List<IRouter>? routers = null;
        var count = _routers?.Count;
        if (count > 0)
        {
            Debug.Assert(_routers != null);
            
            routers = new List<IRouter>(count.Value);
            for (var i = 0; i < count.Value; i++)
            {
                routers.Add(_routers[i]);
            }
        }
        
        /* 将原有 route data 创建 snapshot */
        var snapshot = new RouteDataSnapshot(
            this,
            _dataTokens?.Count > 0 ? new RouteValueDictionary(_dataTokens) : null, 
            routers,
            _values?.Count > 0 ? new RouteValueDictionary(_values) : null);
        
        // 注入新的 router
        if (router != null)
        {
            Routers.Add(router);
        }
        // 注入新的 values
        if (values != null)
        {
            foreach (var kvp in values)
            {
                if (kvp.Value != null)
                {
                    Values[kvp.Key] = kvp.Value;
                }
            }
        }
        // 注入新的 data token
        if (dataTokens != null)
        {
            foreach (var kvp in dataTokens)
            {
                DataTokens[kvp.Key] = kvp.Value;
            }
        }
        
        return snapshot;
    }
    
    // snap shot 结构体
    public readonly struct RouteDataSnapshot
    {
        private readonly RouteData _routeData;
        private readonly RouteValueDictionary? _dataTokens;
        private readonly IList<IRouter>? _routers;
        private readonly RouteValueDictionary? _values;
                
        public RouteDataSnapshot(
            RouteData routeData,
            RouteValueDictionary? dataTokens,
            IList<IRouter>? routers,
            RouteValueDictionary? values)
        {
            if (routeData == null)
            {
                throw new ArgumentNullException(nameof(routeData));
            }
            
            _routeData = routeData;
            _dataTokens = dataTokens;
            _routers = routers;
            _values = values;
        }
        
        // 恢复，
        // _datatoken、_routes、_values 注入 _routedata
        public void Restore()
        {
            /* data tokens */
            
            if (_routeData._dataTokens == null && 
                _dataTokens == null)
            {
                // Do nothing
            }
            else if (_dataTokens == null)
            {
                _routeData._dataTokens!.Clear();
            }
            else
            {
                _routeData._dataTokens!.Clear();
                
                foreach (var kvp in _dataTokens)
                {
                    _routeData._dataTokens
                        	  .Add(kvp.Key, kvp.Value);
                }
            }
            
            /* routers */
            
            if (_routeData._routers == null 
                && _routers == null)
            {
                // Do nothing
            }
            else if (_routers == null)
            {
                // Perf: this is optimized for small list sizes, in particular to avoid 
                // overhead of a native call in Array.Clear inside the List.Clear() method.
                var routers = _routeData._routers!;
                for (var i = routers.Count - 1; i >= 0 ; i--)
                {
                    routers.RemoveAt(i);
                }
            }
            else
            {
                // Perf: this is optimized for small list sizes, in particular to avoid 
                // overhead of a native call in Array.Clear inside the List.Clear() method.  
                //
                // We want to basically copy the contents of _routers in
                // _routeData._routers - this change does that with the minimal number of 
                // reads/writes and without calling Clear().
                var routers = _routeData._routers!;
                var snapshotRouters = _routers;
                
                // This is made more complicated by the fact that List[int] throws if 
                // i == Count, so we have to do two loops and call Add for those cases.
                var i = 0;
                for (; i < snapshotRouters.Count && 
                     i < routers.Count; i++)
                {
                    routers[i] = snapshotRouters[i];
                }
                
                for (; i < snapshotRouters.Count; i++)
                {
                    routers.Add(snapshotRouters[i]);
                }
                
                // Trim excess - again avoiding RemoveRange because it uses native methods.
                for (i = routers.Count - 1; i >= snapshotRouters.Count; i--)
                {
                    routers.RemoveAt(i);
                }
            }
            
            /* values */
            
            if (_routeData._values == null && 
                _values == null)
            {
                // Do nothing
            }
            else if (_values == null)
            {
                _routeData._values!.Clear();
            }
            else
            {
                _routeData._values!.Clear();
                
                foreach (var kvp in _values)
                {
                    _routeData._values
                        	  .Add(kvp.Key, kvp.Value);
                }
            }
        }
    }
}

```

###### 2.4.1.3 virtual path data

```c#
public class VirtualPathData
{        
    /* data token */
    private RouteValueDictionary _dataTokens;
    public RouteValueDictionary DataTokens
    {
        get
        {
            if (_dataTokens == null)
            {
                _dataTokens = new RouteValueDictionary();
            }
            
            return _dataTokens;
        }
    }
    
    /* virtual path */
    private string _virtualPath;
    public string VirtualPath
    {
        get
        {
            return _virtualPath;
        }
        set
        {
            _virtualPath = NormalizePath(value);
        }
    }    
    // 加上“/”
    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }
        
        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            return "/" + path;
        }
        
        return path;
    }
        
    public IRouter Router { get; set; }
                
    public VirtualPathData(IRouter router, string virtualPath)
        : this(router, virtualPath, dataTokens: null)
    {
    }
        
    public VirtualPathData(
        IRouter router,
        string virtualPath,
        RouteValueDictionary dataTokens)
    {
        if (router == null)
        {
            throw new ArgumentNullException(nameof(router));
        }
        
        Router = router;
        VirtualPath = virtualPath;
        _dataTokens = dataTokens == null 
            			  ? null 
            			  : new RouteValueDictionary(dataTokens);
    }            
}

```

###### 2.4.1.4 virtual path context

```c#
public class VirtualPathContext
{
    public RouteValueDictionary AmbientValues { get; }        
    public HttpContext HttpContext { get; }        
    public string? RouteName { get; }        
    public RouteValueDictionary Values { get; set; }
       
    public VirtualPathContext(
        HttpContext httpContext,
        RouteValueDictionary ambientValues,
        RouteValueDictionary values)
            : this(httpContext, ambientValues, values, null)
    {
    }
        
    public VirtualPathContext(
        HttpContext httpContext,
        RouteValueDictionary ambientValues,
        RouteValueDictionary values,
        string? routeName)
    {
        HttpContext = httpContext;
        AmbientValues = ambientValues;
        Values = values;
        RouteName = routeName;
    }               
}

```

##### 2.4.2 route 派生接口

###### 2.4.2.1 named router 接口

```c#
public interface INamedRouter : IRouter
{    
    string? Name { get; }
}

```

##### 2.4.3 null router

```c#
internal class NullRouter : IRouter
{
    public static readonly NullRouter Instance = new NullRouter();
    
    private NullRouter()
    {
    }
    
    public Task RouteAsync(RouteContext context)
    {
        return Task.CompletedTask;
    }
    
    public VirtualPathData? GetVirtualPath(VirtualPathContext context)
    {
        return null;
    }        
}

```

##### 2.4.4 route

###### 2.4.4.1 route base 抽象基类

```c#
public abstract class RouteBase : IRouter, INamedRouter
{
    private readonly object _loggersLock = new object();
    
    // 匹配 request -> template
    private TemplateMatcher? _matcher;
    // 匹配 template -> virtual path
    private TemplateBinder? _binder;
    
    private ILogger? _logger;
    private ILogger? _constraintLogger;
           
    // name
    public virtual string? Name { get; protected set; }
    
    public virtual IDictionary<string, IRouteConstraint> Constraints { get; protected set; } 
    protected virtual IInlineConstraintResolver ConstraintResolver { get; set; }
    
    public virtual RouteValueDictionary DataTokens { get; protected set; }        
    public virtual RouteValueDictionary Defaults { get; protected set; }
           
    public virtual RouteTemplate ParsedTemplate { get; protected set; }
    
    /* 构造函数 */    
    public RouteBase(
        string? template,
        string? name,
        IInlineConstraintResolver constraintResolver,
        RouteValueDictionary? defaults,
        IDictionary<string, object>? constraints,
        RouteValueDictionary? dataTokens)
    {       
        if (constraintResolver == null)
        {
            throw new ArgumentNullException(nameof(constraintResolver));
        }
        
        // 注入 template string，如果是 null，转为 string.empty
        template = template ?? string.Empty;
        
        Name = name;                
        ConstraintResolver = constraintResolver;                
        DataTokens = dataTokens ?? new RouteValueDictionary();
        
        try
        {
            /* 1- 解析 route template，
               	  使用 template parser 从 template string 解析 route template */
            // Data we parse from the template will be used 
            // to fill in the rest of the constraints or defaults. 
            // The parser will throw for invalid routes.
            ParsedTemplate = TemplateParser.Parse(template);
            
            /* 2- 解析 route constraint，
                  使用 inline constraint resolver 从 route template 解析 route constraint */   
            Constraints = GetConstraints(
                			  constraintResolver, 
			                  ParsedTemplate, 
              				  constraints);
            
            /* 3- 获取 parameter 的 default value， 
            	  从 route template 解析 default value */            	  
            Defaults = GetDefaults(ParsedTemplate, defaults);
            
        }
        catch (Exception exception)
        {
            throw new RouteCreationException(
                Resources.FormatTemplateRoute_Exception(name, template), 
                exception);
        }
    }
                 
    /// <inheritdoc />
    public override string ToString()
    {
        return ParsedTemplate.TemplateText!;
    }
    
    [MemberNotNull(nameof(_logger), nameof(_constraintLogger))]
    private void EnsureLoggers(HttpContext context)
    {
        // We check first using the _logger to see 
        // if the loggers have been initialized to avoid taking
        // the lock on the most common case.
        if (_logger == null)
        {
            // We need to lock here to ensure that _constraintLogger 
            // and _logger get initialized atomically.
            lock (_loggersLock)
            {
                if (_logger != null)
                {
                    // Multiple threads might have tried to acquire 
                    // the lock at the same time. 
                    // Technically there is nothing wrong if things 
                    // get reinitialized by a second thread, 
                    // but its easy to prevent by just rechecking and returning here.
                    Debug.Assert(_constraintLogger != null);
                    
                    return;
                }
                
                // 解析 logger factory
                var factory = context.RequestServices
                    				 .GetRequiredService<ILoggerFactory>();
                
                // 创建 constraint logger
                _constraintLogger = 
                    factory.CreateLogger(typeof(RouteConstraintMatcher).FullName!);
                
                // 创建 logger
                _logger = factory.CreateLogger(typeof(RouteBase).FullName!);
            }            
        }
        
        Debug.Assert(_constraintLogger != null);
    }
}

```

###### 2.4.4.2 get constraint

```c#
public abstract class RouteBase : IRouter, INamedRouter
{
    protected static IDictionary<string, IRouteConstraint> GetConstraints(
        IInlineConstraintResolver inlineConstraintResolver,
        RouteTemplate parsedTemplate,
        IDictionary<string, object>? constraints)
    {
        // 创建 route constraint builder
        var constraintBuilder = new RouteConstraintBuilder(
            inlineConstraintResolver, 
            parsedTemplate.TemplateText!);
        
        // 将（传入的） constraints 注入constraint builder
        if (constraints != null)
        {
            foreach (var kvp in constraints)                
            {
                constraintBuilder.AddConstraint(kvp.Key, kvp.Value);
            }
        }
                
        // 遍历（传入的）route template 的 parameter part，
        foreach (var parameter in parsedTemplate.Parameters)
        {
            // 如果 parameter part 是 optional，标记
            if (parameter.IsOptional)
            {
                constraintBuilder.SetOptional(parameter.Name!);
            }
            // 遍历 parameter part 所有 inline constraint， 注入 constraint builder
            foreach (var inlineConstraint in parameter.InlineConstraints)
            {
                constraintBuilder.AddResolvedConstraint(
                    parameter.Name!, 
                    inlineConstraint.Constraint);
            }
        }
        
        // 构建 constraints dictionary
        return constraintBuilder.Build();
    }
}
```

###### 2.4.4.3  get defaults

```c#
public abstract class RouteBase : IRouter, INamedRouter
{
     protected static RouteValueDictionary GetDefaults(
        RouteTemplate parsedTemplate,
        RouteValueDictionary? defaults)
    {
        // 预结果，创建或者克隆（传入的）defaults
        var result = defaults == null 
            		 	? new RouteValueDictionary() 
			            : new RouteValueDictionary(defaults);
        
        // 遍历（传入的）route template 的 parameter part
        foreach (var parameter in parsedTemplate.Parameters)
        {
            // 如果 parameter part 的 default value 不为 null，注入 result
            if (parameter.DefaultValue != null)
            {
#if RVD_TryAdd
    			if (!result.TryAdd(
                    	parameter.Name, 
                    	parameter.DefaultValue))
                {
                    throw new InvalidOperationException(
                        Resources.FormatTemplateRoute
		                          _CannotHaveDefaultValueSpecifiedInlineAndExplicitly(
                                      parameter.Name));
                }
#else
                if (result.ContainsKey(parameter.Name!))
                {
                    throw new InvalidOperationException(
                        Resources.FormatTemplateRoute
                        		 _CannotHaveDefaultValueSpecifiedInlineAndExplicitly(
                                     parameter.Name));
                }
                else
                {
                    result.Add(
                        	   parameter.Name!, 
		                       parameter.DefaultValue);
                }
#endif
            }
        }
                
        return result;
    }                                
}
```

###### 2.4.4.4 接口方法 - route async

```c#
public abstract class RouteBase : IRouter, INamedRouter
{
    public virtual Task RouteAsync(RouteContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        // a- 确认 template matcher 不为 null（创建）
        EnsureMatcher();
        
        // 确认 logger 不为 null       
        EnsureLoggers(context.HttpContext);
                        
        // 从 http context 中解析 request path，
        var requestPath = context.HttpContext.Request.Path;     
        
        // 使用 template matche 验证 request path，不匹配 -> 结束
        if (!_matcher.TryMatch(
            	requestPath, 
            	context.RouteData.Values))
        {
            // If we got back a null value set, that means the URI did not match
            return Task.CompletedTask;
        }
        
        // b- 合并（额外的）data token
        // Perf: Avoid accessing dictionaries if you don't need to write to them, 
        // these dictionaries are all created lazily.
        if (DataTokens.Count > 0)
        {
            MergeValues(
                context.RouteData.DataTokens, 
                DataTokens);
        }
                
        // 使用 constraint matcher 验证，如果不匹配 -> 结束
        if (!RouteConstraintMatcher.Match(
	            Constraints,        
    	        context.RouteData.Values,
        	    context.HttpContext,
            	this,
            	RouteDirection.IncomingRequest,
            	_constraintLogger))
        {
            return Task.CompletedTask;
        }
        
        /* request path（http context 解析得到）匹配 route template 和  route constrain，*/
        
        // 记录日志
        _logger.RequestMatchedRoute(Name!, ParsedTemplate.TemplateText!);        
        // 触发 on route matched 钩子
        return OnRouteMatched(context);
    }
    
    // a- 由 route template（解析得到）创建 template matcher
    [MemberNotNull(nameof(_matcher))]
    private void EnsureMatcher()
    {
        if (_matcher == null)
        {
            _matcher = new TemplateMatcher(ParsedTemplate, Defaults);
        }
    }
    
    // b- 合并传入的 data token
    private static void MergeValues(
        RouteValueDictionary destination,
        RouteValueDictionary values)
    {
        foreach (var kvp in values)
        {
            // This will replace the original value for the specified key.
            // Values from the matched route will take preference over previous
            // data in the route context.
            destination[kvp.Key] = kvp.Value;
        }
    }
    
    // on route matched 钩子
    protected abstract Task OnRouteMatched(RouteContext context);        
}

```

###### 2.4.4.5 接口方法 - get virtaul path

```c#
public abstract class RouteBase : IRouter, INamedRouter
{
    public virtual VirtualPathData? GetVirtualPath(VirtualPathContext context)
    {
        // a- 确认 template binder 不为 null（创建）
        EnsureBinder(context.HttpContext);
        
        // 确认 logger 不为 null
        EnsureLoggers(context.HttpContext);
        
        // 使用 template binder 解析 template value result，
        var values = _binder.GetValues(
            					context.AmbientValues, 
					            context.Values);
        // 不能解析，返回 null
        if (values == null)
        {
            // We're missing one of the required values for this route.
            return null;
        }
                        
        // 使用 constraint matcher 验证 template value result 的 combined values，
        // 如果不匹配，返回 null
        if (!RouteConstraintMatcher.Match(
            	Constraints,
	            values.CombinedValues,
    	        context.HttpContext,
        	    this,
            	RouteDirection.UrlGeneration,
            	_constraintLogger))
        {
            return null;
        }
                
        // （通过 constraint 验证），
        // 将 template value result 的 combined values 注入到 virtual path context
        context.Values = values.CombinedValues;   
        
        // b- 使用 on virtual path generated 钩子创建 virtual path data
        var pathData = OnVirtualPathGenerated(context);
        // 如果创建成功，返回结果
        if (pathData != null)
        {
            // If the target generates a value then that can short circuit.
            return pathData;
        }
        
        /* 不能由 on virtual path generated 钩子创建 virtual path data，*/
        
        // 使用 template binder 将 template value result 绑定到 virtual path string
        var virtualPath = _binder.BindValues(values.AcceptedValues);
        // 如果不能绑定，返回 null
        if (virtualPath == null)
        {
            return null;
        }
        // 由 virtual path string 创建 virtual path data        
        pathData = new VirtualPathData(this, virtualPath);
        
        // 注入传入的 data token
        if (DataTokens != null)
        {
            foreach (var dataToken in DataTokens)
            {
                pathData.DataTokens
                    	.Add(dataToken.Key, dataToken.Value);
            }
        }
        
        return pathData;
    }
    
    // a- 使用 template binder factory（从 service provider 中解析），
    //	  由 route template（解析得到）创建 template binder    
    [MemberNotNull(nameof(_binder))]
    private void EnsureBinder(HttpContext context)
    {
        if (_binder == null)
        {
            var binderFactory = context
                .RequestServices
                .GetRequiredService<TemplateBinderFactory>();
            
            _binder = binderFactory.Create(ParsedTemplate, Defaults);
        }
    }
    
    // b- virtual path generated 钩子，由派生类实现
    protected abstract VirtualPathData? 
        OnVirtualPathGenerated(VirtualPathContext context);
}

```

###### 2.4.4.6 route

* 包裹其他 router

```c#
public class Route : RouteBase
{
    private readonly IRouter _target;
    
    public string? RouteTemplate => ParsedTemplate.TemplateText;
        
    public Route(
        IRouter target,
        string routeTemplate,
        IInlineConstraintResolver inlineConstraintResolver)
            : this(
                target,
                routeTemplate,
                defaults: null,
                constraints: null,
                dataTokens: null,
                inlineConstraintResolver: inlineConstraintResolver)
    {
    }
        
    public Route(
        IRouter target,
        string routeTemplate,
        RouteValueDictionary? defaults,
        IDictionary<string, object>? constraints,
        RouteValueDictionary? dataTokens,
        IInlineConstraintResolver inlineConstraintResolver)
            : this(
                target, 
                null, 
                routeTemplate, 
                defaults, 
                constraints, 
                dataTokens, 
                inlineConstraintResolver)
    {
    }
        
    public Route(
        IRouter target,
        string? routeName,
        string? routeTemplate,
        RouteValueDictionary? defaults,
        IDictionary<string, object>? constraints,
        RouteValueDictionary? dataTokens,
        IInlineConstraintResolver inlineConstraintResolver)
            : base(
                  routeTemplate,
                  routeName,
                  inlineConstraintResolver,
                  defaults,
                  constraints,
                  dataTokens)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        _target = target;
    }
        
    /// <inheritdoc />
    protected override Task OnRouteMatched(RouteContext context)
    {
        context.RouteData
               .Routers
               .Add(_target);
        
        return _target.RouteAsync(context);
    }
    
    /// <inheritdoc />
    protected override VirtualPathData? OnVirtualPathGenerated(VirtualPathContext context)
    {
        return _target.GetVirtualPath(context);
    }
}

```

##### 2.4.5 route collection

###### 2.4.5.1 接口

```c#
public interface IRouteCollection : IRouter
{    
    void Add(IRouter router);
}

```

###### 2.4.5.2 route collection

```c#
public class RouteCollection : IRouteCollection
{
    private readonly static char[] UrlQueryDelimiters = new char[] { '?', '#' };
    
    private readonly List<IRouter> _routes = new List<IRouter>();    
    private readonly List<IRouter> _unnamedRoutes = new List<IRouter>();
    private readonly Dictionary<string, INamedRouter> _namedRoutes =
        new Dictionary<string, INamedRouter>(StringComparer.OrdinalIgnoreCase);
    
    private RouteOptions? _options;
                           
    public int Count
    {
        get { return _routes.Count; }
    }
    
    /* add router */    
    public void Add(IRouter router)
    {
        if (router == null)
        {
            throw new ArgumentNullException(nameof(router));
        }
        
        // 如果 route 实现了 named router 接口，
        var namedRouter = router as INamedRouter;
        if (namedRouter != null)
        {
            if (!string.IsNullOrEmpty(namedRouter.Name))
            {
                // 注入 named route 集合
                _namedRoutes.Add(namedRouter.Name, namedRouter);
            }
        }
        // 否则，即没有实现 named router 接口，
        else
        {
            // 注入 unamed route 集合
            _unnamedRoutes.Add(router);
        }
        
        // 同时注入 routes 集合（无论是否实现 named router 接口）        
        _routes.Add(router);
    }
    
    public IRouter this[int index]
    {
        get { return _routes[index]; }
    }
    
    
    
    
    
    // 解析 route options
    [MemberNotNull(nameof(_options))]
    private void EnsureOptions(HttpContext context)
    {
        if (_options == null)
        {
            _options = context.RequestServices
                			  .GetRequiredService<IOptions<RouteOptions>>()
                			  .Value;
        }
    }
    
    
    
    
}

```

###### 2.4.5.3 接口方法 - route async

```c#
public class RouteCollection : IRouteCollection
{    
    public async virtual Task RouteAsync(RouteContext context)
    {
        // Perf: We want to avoid allocating a new RouteData for each route we 
        // need to process.
        // We can do this by snapshotting the state at the beginning and then restoring 
        // it for each router we execute.
        var snapshot = context.RouteData.PushState(null, values: null, dataTokens: null);
        
        // 遍历 route 集合，
        for (var i = 0; i < Count; i++)
        {
            // 将 route 注入 route context
            var route = this[i];
            context.RouteData
                   .Routers
                   .Add(route);
            
            try
            {
                // 执行 route 的 route async 方法
                await route.RouteAsync(context);                
                // 如果执行结果不为 null，结束
                if (context.Handler != null)
                {
                    break;
                }
            }
            finally
            {
                if (context.Handler == null)
                {
                    snapshot.Restore();
                }
            }
        }
    }
}
```

###### 2.4.5.4 接口方法 - get virtual path

```c#
public class RouteCollection : IRouteCollection
{
    /// <inheritdoc />
    public virtual VirtualPathData? GetVirtualPath(VirtualPathContext context)
    {
        // 解析 route options 
        EnsureOptions(context.HttpContext);
        
        /* 如果 virtual path context 中 route name 不为空  */
        if (!string.IsNullOrEmpty(context.RouteName))
        {
            // 预结果
            VirtualPathData? namedRoutePathData = null;
            
            // 从 named route 集合中找到匹配的 router，
            if (_namedRoutes.TryGetValue(
                				context.RouteName, 
                				out var matchedNamedRoute))
            {
                // 如果能找到，使用 route 解析 virtual path data
                namedRoutePathData = matchedNamedRoute.GetVirtualPath(context);
            }
            
            // a- 从 unamed route 集合中解析 virtual path data
            var pathData = GetVirtualPath(context, _unnamedRoutes);
                        
            // If the named route and one of the unnamed routes also matches, 
            // then we have an ambiguity.
            
            // 如果都能解析到 virtual path data，抛出异常
            if (namedRoutePathData != null && 
                pathData != null)
            {
                var message = Resources.FormatNamedRoutes
                    					_AmbiguousRoutesFound(context.RouteName);
                throw new InvalidOperationException(message);
            }
            
            /* b- 由 named route data 或者 unamed route data 创建 virtual path data */
            return NormalizeVirtualPath(namedRoutePathData ?? pathData);
        }
        /* 否则，即 route name 为空 */
        else
        {
            // a- & b-
            return NormalizeVirtualPath(GetVirtualPath(context, _routes));
        }
    }
        
    // a- 从 route 集合中 get virtual path
    private VirtualPathData? GetVirtualPath(
        VirtualPathContext context, 
        List<IRouter> routes)
    {
        // 遍历 route 集合解析 virtual path data，        
        for (var i = 0; i < routes.Count; i++)
        {
            var route = routes[i];
            
            var pathData = route.GetVirtualPath(context);
            if (pathData != null)
            {
                // 只要找到（第一个），返回结果
                return pathData;
            }
        }
        
        // 找不到，返回 null
        return null;
    }
    
    // b- 标准化 virtual path data
    private VirtualPathData? NormalizeVirtualPath(VirtualPathData? pathData)
    {
        if (pathData == null)
        {
            return pathData;
        }
        
        Debug.Assert(_options != null);
        
        var url = pathData.VirtualPath;
        
        if (!string.IsNullOrEmpty(url) && 
            (_options.LowercaseUrls || _options.AppendTrailingSlash))
        {
            var indexOfSeparator = url.IndexOfAny(UrlQueryDelimiters);
            var urlWithoutQueryString = url;
            var queryString = string.Empty;
            
            if (indexOfSeparator != -1)
            {
                urlWithoutQueryString = url.Substring(0, indexOfSeparator);
                queryString = url.Substring(indexOfSeparator);
            }
            
            if (_options.LowercaseUrls)
            {
                urlWithoutQueryString = urlWithoutQueryString.ToLowerInvariant();
            }
            
            if (_options.LowercaseUrls && 
                _options.LowercaseQueryStrings)
            {
                queryString = queryString.ToLowerInvariant();
            }
            
            if (_options.AppendTrailingSlash && 
                !urlWithoutQueryString.EndsWith("/", StringComparison.Ordinal))
            {
                urlWithoutQueryString += "/";
            }
            
            // queryString will contain the delimiter ? or # as the first character, 
            // so it's safe to append.
            url = urlWithoutQueryString + queryString;
            
            return new VirtualPathData(pathData.Router, url, pathData.DataTokens);
        }
        
        return pathData;
    }        
}

```

#### 2.5 route builder

##### 2.5.1 接口

```c#
public interface IRouteBuilder
{    
    IApplicationBuilder ApplicationBuilder { get; }        
    IRouter? DefaultHandler { get; set; }    
    IServiceProvider ServiceProvider { get; }            
    IList<IRouter> Routes { get; }
        
    IRouter Build();
}

```

##### 2.5.2 route builder

```c#
public class RouteBuilder : IRouteBuilder
{
    public IApplicationBuilder ApplicationBuilder { get; }      
    public IRouter? DefaultHandler { get; set; }       
    public IServiceProvider ServiceProvider { get; }       
    public IList<IRouter> Routes { get; }
            
    public RouteBuilder(IApplicationBuilder applicationBuilder)
        : this(
            applicationBuilder, 
            defaultHandler: null)
    {
    }
        
    public RouteBuilder(
        IApplicationBuilder applicationBuilder, 
        IRouter? defaultHandler)
    {
        if (applicationBuilder == null)
        {
            throw new ArgumentNullException(nameof(applicationBuilder));
        }
        
        // 如果 app builder 中没有注入 routing marker service，抛出异常        
        if (applicationBuilder.ApplicationServices
            				  .GetService(typeof(RoutingMarkerService)) == null)
        {
            throw new InvalidOperationException(
                Resources.FormatUnableToFindServices(
                    nameof(IServiceCollection),
                    nameof(RoutingServiceCollectionExtensions.AddRouting),
                    "ConfigureServices(...)"));
        }
        
        ApplicationBuilder = applicationBuilder;
        DefaultHandler = defaultHandler;
        ServiceProvider = applicationBuilder.ApplicationServices;
        
        // 创建 route 集合（默认）
        Routes = new List<IRouter>();
    }
        
    // 返回新的 route collection 实例
    public IRouter Build()
    {
        var routeCollection = new RouteCollection();
        
        foreach (var route in Routes)
        {
            routeCollection.Add(route);
        }
        
        return routeCollection;
    }
}

```

###### x2.5.2.1 route marker service

```c#
internal class RoutingMarkerService
{
}

```

##### 2.5.3 扩展方法 - map route

* 向`route builder`中注入 route

```c#
public static class MapRouteRouteBuilderExtensions
{    
    public static IRouteBuilder MapRoute(
        this IRouteBuilder routeBuilder,
        string? name,
        string? template)
    {
        return MapRoute(
            routeBuilder, 
            name, 
            template, 
            defaults: null);                
    }
        
    public static IRouteBuilder MapRoute(
        this IRouteBuilder routeBuilder,
        string? name,
        string? template,
        object? defaults)
    {
        return MapRoute(
            routeBuilder, 
            name, 
            template, 
            defaults, 
            constraints: null);
    }
            
    public static IRouteBuilder MapRoute(
        this IRouteBuilder routeBuilder,
        string? name,
        string? template,
        object? defaults,
        object? constraints)
    {
        return MapRoute(
            routeBuilder, 
            name, 
            template, 
            defaults, 
            constraints, 
            dataTokens: null);
    }
    
    // 向 route builder 中注入 route
    public static IRouteBuilder MapRoute(
        this IRouteBuilder routeBuilder,
        string? name,
        string? template,
        object? defaults,
        object? constraints,
        object? dataTokens)
    {
        if (routeBuilder.DefaultHandler == null)
        {
            throw new RouteCreationException(
                Resources.FormatDefaultHandler_MustBeSet(nameof(IRouteBuilder)));
        }
        
        routeBuilder.Routes
            		.Add(
            			new Route(
                            routeBuilder.DefaultHandler,
                            name,
                            template,
                            new RouteValueDictionary(defaults),
                            new RouteValueDictionary(constraints)!,
                            new RouteValueDictionary(dataTokens),
                            CreateInlineConstraintResolver(routeBuilder.ServiceProvider)));
        
        return routeBuilder;
    }            
}

```

###### 2.5.3.1 create inline constraint resolver

```c#
public static class MapRouteRouteBuilderExtensions
{
    private static IInlineConstraintResolver CreateInlineConstraintResolver(
        IServiceProvider serviceProvider)
    {
        var inlineConstraintResolver = 
            serviceProvider.GetRequiredService<IInlineConstraintResolver>();
        
        var parameterPolicyFactory = 
            serviceProvider.GetRequiredService<ParameterPolicyFactory>();
        
        // This inline constraint resolver will return a null constraint for 
        // non-IRouteConstraint parameter policies so Route does not error
        return new BackCompatInlineConstraintResolver(
            inlineConstraintResolver, 
            parameterPolicyFactory);
    }
    
    private class BackCompatInlineConstraintResolver : IInlineConstraintResolver
    {
        private readonly IInlineConstraintResolver _inner;
        private readonly ParameterPolicyFactory _parameterPolicyFactory;
        
        public BackCompatInlineConstraintResolver(
            IInlineConstraintResolver inner, 
            ParameterPolicyFactory parameterPolicyFactory)
        {
            _inner = inner;
            _parameterPolicyFactory = parameterPolicyFactory;
        }
        
        public IRouteConstraint? ResolveConstraint(string inlineConstraint)
        {
            // 使用 inner constraint resolver 解析 constraint，            
            var routeConstraint = _inner.ResolveConstraint(inlineConstraint);
            // 如果能解析到，返回 constraint
            if (routeConstraint != null)
            {
                return routeConstraint;
            }
            
            // 否则，即 inner constraint resolver 不能解析，
            // 由 parameter policy factory 创建 constraint，
            var parameterPolicy = _parameterPolicyFactory.Create(null!, inlineConstraint);
            // 如果能创建，返回 constraint
            if (parameterPolicy != null)
            {
                // Logic inside Route will skip adding NullRouteConstraint
                return NullRouteConstraint.Instance;
            }
            
            // 都不能，返回 null
            return null;
        }
    }
}

```

##### 2.5.4 扩展方法 

###### 2.2.4.1 map route with request delegate

```c#
public static class RequestDelegateRouteBuilderExtensions
{
    
    public static IRouteBuilder MapRoute(
        this IRouteBuilder builder, 
        string template, 
        RequestDelegate handler)
    {
        var route = new Route(
            new RouteHandler(handler),
            template,
            defaults: null,
            constraints: null,
            dataTokens: null,
            inlineConstraintResolver: GetConstraintResolver(builder));
        
        builder.Routes
	           .Add(route);
        
        return builder;
    }
    
    public static IRouteBuilder MapMiddlewareRoute(
        this IRouteBuilder builder, 
        string template, 
        Action<IApplicationBuilder> action)
    {
        var nested = builder.ApplicationBuilder
            				.New();
        action(nested);
        
        return builder.MapRoute(template, nested.Build());
    }                           
}

```

###### 2.2.4.2 map verb & variety

```c#
public static class RequestDelegateRouteBuilderExtensions
{
    /* map verb */
    public static IRouteBuilder MapVerb(
        this IRouteBuilder builder,
        string verb,
        string template,
        Func<HttpRequest, HttpResponse, RouteData, Task> handler)
    {
        RequestDelegate requestDelegate = 
            (httpContext) =>
        		{
            		return handler(
                    		   httpContext.Request, 
                    		   httpContext.Response, 
                    	   	   httpContext.GetRouteData());
        		};
        
        return builder.MapVerb(verb, template, requestDelegate);
    }   
    
    public static IRouteBuilder MapVerb(
        this IRouteBuilder builder,
        string verb,
        string template,
        RequestDelegate handler)
    {
        var route = new Route(
            new RouteHandler(handler),
            template,
            defaults: null,
            constraints: 
            	new RouteValueDictionary(
                    new 
                    { 
                        httpMethod = new HttpMethodRouteConstraint(verb) 
                    })!,
            dataTokens: null,
            inlineConstraintResolver: GetConstraintResolver(builder));
        
        builder.Routes
               .Add(route);
         
        return builder;
    }
                
    private static IInlineConstraintResolver GetConstraintResolver(IRouteBuilder builder)
    {
        return builder.ServiceProvider
            		  .GetRequiredService<IInlineConstraintResolver>();
    }
    
    /* map get */
    public static IRouteBuilder MapGet(
        this IRouteBuilder builder, 
        string template, 
        RequestDelegate handler)
    {
        return builder.MapVerb("GET", template, handler);
    }
                        
    public static IRouteBuilder MapGet(
        this IRouteBuilder builder,
        string template,
        Func<HttpRequest, HttpResponse, RouteData, Task> handler)
    {
        return builder.MapVerb("GET", template, handler);
    }
    
    /* map post */
    public static IRouteBuilder MapPost(
        this IRouteBuilder builder, 
        string template, 
        RequestDelegate handler)
    {
        return builder.MapVerb("POST", template, handler);
    }
                
    public static IRouteBuilder MapPost(
        this IRouteBuilder builder,
        string template,
        Func<HttpRequest, HttpResponse, RouteData, Task> handler)
    {
        return builder.MapVerb("POST", template, handler);
    }
    
    /* map put */
    public static IRouteBuilder MapPut(
        this IRouteBuilder builder, 
        string template, 
        RequestDelegate handler)
    {
        return builder.MapVerb("PUT", template, handler);
    }
               
    public static IRouteBuilder MapPut(
        this IRouteBuilder builder,
        string template,
        Func<HttpRequest, HttpResponse, RouteData, Task> handler)
    {
        return builder.MapVerb("PUT", template, handler);
    }
    
    /* map delete */
    public static IRouteBuilder MapDelete(
        this IRouteBuilder builder, 
        string template, 
        RequestDelegate handler)
    {
        return builder.MapVerb("DELETE", template, handler);
    }
                
    public static IRouteBuilder MapDelete(
        this IRouteBuilder builder,
        string template,
        Func<HttpRequest, HttpResponse, RouteData, Task> handler)
    {
        return builder.MapVerb("DELETE", template, handler);
    }                                              
}

```

###### 2.2.4.3 map middleware route & variety

```c#
public static class RequestDelegateRouteBuilderExtensions
{
    public static IRouteBuilder MapMiddlewareVerb(
        this IRouteBuilder builder,
        string verb,
        string template,
        Action<IApplicationBuilder> action)
    {
        var nested = builder.ApplicationBuilder
            				.New();
        action(nested);
        return builder.MapVerb(verb, template, nested.Build());
    }                                           
       
    // get
    public static IRouteBuilder MapMiddlewareGet(
        this IRouteBuilder builder, 
        string template, 
        Action<IApplicationBuilder> action)
    {
        return builder.MapMiddlewareVerb("GET", template, action);
    }
        
    // post
    public static IRouteBuilder MapMiddlewarePost(
        this IRouteBuilder builder, 
        string template, 
        Action<IApplicationBuilder> action)
    {
        return builder.MapMiddlewareVerb("POST", template, action);
    }
       
    // put
    public static IRouteBuilder MapMiddlewarePut(
        this IRouteBuilder builder, 
        string template, 
        Action<IApplicationBuilder> action)
    {
        return builder.MapMiddlewareVerb("PUT", template, action);
    }

    // delete
    public static IRouteBuilder MapMiddlewareDelete(
        this IRouteBuilder builder, 
        string template, 
        Action<IApplicationBuilder> action)
    {
        return builder.MapMiddlewareVerb("DELETE", template, action);
    }                                          
}

```

#### 2.6 routing feature



##### 2.6.3 接口



```c#
public interface IRoutingFeature
{        
    RouteData? RouteData { get; set; }
}

```

###### 2.6.3.2 routing feature

```c#
public class RoutingFeature : IRoutingFeature
{
    /// <inheritdoc />
    public RouteData? RouteData { get; set; }
}

```

###### 2.6.3.3 routing feature in http context

```c#
public static class RoutingHttpContextExtensions
{
    /// <summary>
        /// Gets the <see cref="RouteData"/> associated with the provided <paramref name="httpContext"/>.
        /// </summary>
        /// <param name="httpContext">The <see cref="HttpContext"/> associated with the current request.</param>
        /// <returns>The <see cref="RouteData"/>, or null.</returns>
    public static RouteData GetRouteData(this HttpContext httpContext)
    {
        if (httpContext == null)
        {
            throw new ArgumentNullException(nameof(httpContext));
        }
        
        var routingFeature = httpContext.Features.Get<IRoutingFeature>();
        return routingFeature?.RouteData ?? new RouteData(httpContext.Request.RouteValues);
    }
    
        /// <summary>
        /// Gets a route value from <see cref="RouteData.Values"/> associated with the provided
        /// <paramref name="httpContext"/>.
        /// </summary>
        /// <param name="httpContext">The <see cref="HttpContext"/> associated with the current request.</param>
        /// <param name="key">The key of the route value.</param>
        /// <returns>The corresponding route value, or null.</returns>
    public static object? GetRouteValue(this HttpContext httpContext, string key)
    {
        if (httpContext == null)
        {
            throw new ArgumentNullException(nameof(httpContext));
        }
        
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }
        
        return httpContext.Features.Get<IRouteValuesFeature>()?.RouteValues[key];
    }
}

```



