## about mvc formatter

### 1. about



### 2. details

#### 2.1 media type

##### 2.1.1 media type

* http request media type 的封装

```c#
public readonly struct MediaType
{
    private static readonly StringSegment QualityParameter = new StringSegment("q");
    
    private readonly MediaTypeParameterParser _parameterParser;            
    
    public StringSegment Type { get; }
	public StringSegment SubType { get; }    
    public StringSegment SubTypeSuffix { get; }
    public StringSegment SubTypeWithoutSuffix { get; }             
    
    public MediaType(string mediaType)
        : this(mediaType, 0, mediaType.Length)
    {
    }
        
    public MediaType(StringSegment mediaType)
        : this(mediaType.Buffer, mediaType.Offset, mediaType.Length)
    {
    }
        
    public MediaType(string mediaType, int offset, int? length)
    {
        if (mediaType == null)
        {
            throw new ArgumentNullException(nameof(mediaType));
        }        
        if (offset < 0 || 
            offset >= mediaType.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }        
        if (length != null)
        {
            if(length < 0 || 
               length > mediaType.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            
            if (offset > mediaType.Length - length)
            {
                throw new ArgumentException(
                    Resources.FormatArgument_InvalidOffsetLength(
                        		  nameof(offset), 
                        		  nameof(length)));
            }
        }
        
        _parameterParser = default(MediaTypeParameterParser);
        
        var typeLength = GetTypeLength(
            				 mediaType, 
            				 offset, 
            				 out var type);
        if (typeLength == 0)
        {
            Type = new StringSegment();
            SubType = new StringSegment();
            SubTypeWithoutSuffix = new StringSegment();
            SubTypeSuffix = new StringSegment();
            return;
        }
        else
        {
            Type = type;
        }
        
        var subTypeLength = GetSubtypeLength(
            					mediaType, 
            					offset + typeLength, 
            					out var subType);
        if (subTypeLength == 0)
        {
            SubType = new StringSegment();
            SubTypeWithoutSuffix = new StringSegment();
            SubTypeSuffix = new StringSegment();
            return;
        }
        else
        {
            SubType = subType;
            
            if (TryGetSuffixLength(subType, out var subtypeSuffixLength))
            {
                SubTypeWithoutSuffix = 
                    subType.Subsegment(
                    	0, 
                    	subType.Length - subtypeSuffixLength - 1);
                SubTypeSuffix = 
                    subType.Subsegment(
                    	subType.Length - subtypeSuffixLength, 
                    	subtypeSuffixLength);
            }
            else
            {
                SubTypeWithoutSuffix = SubType;
                SubTypeSuffix = new StringSegment();
            }
        }
        
        _parameterParser = 
            new MediaTypeParameterParser(
            		mediaType, 
            		offset + typeLength + subTypeLength, 
            		length);
    }        
    
    // All GetXXXLength methods work in the same way. 
    // They expect to be on the right position for the token they are parsing, 
    // for example, the beginning of the media type or the delimiter from a previous token, 
    // like '/', ';' or '='.
    // Each method consumes the delimiter token if any, the leading whitespace, 
    // then the given token itself, and finally the trailing whitespace.
    private static int GetTypeLength(
        string input, 
        int offset, 
        out StringSegment type)
    {
        if (offset < 0 || 
            offset >= input.Length)
        {
            type = default(StringSegment);
            return 0;
        }
        
        var current = offset + 
            		  HttpTokenParsingRules.GetWhitespaceLength(input, offset);
        
        // Parse the type, 
        // i.e. <type> in media type string "<type>/<subtype>; param1=value1; param2=value2"
        var typeLength = HttpTokenParsingRules.GetTokenLength(input, current);
        if (typeLength == 0)
        {
            type = default(StringSegment);
            return 0;
        }
        
        type = new StringSegment(input, current, typeLength);
        
        current += typeLength;
        current += HttpTokenParsingRules.GetWhitespaceLength(input, current);
        
        return current - offset;
    }
    
    private static int GetSubtypeLength(
        string input, 
        int offset, 
        out StringSegment subType)
    {
        var current = offset;
        
        // Parse the separator between type and subtype
        if (current < 0 || 
            current >= input.Length || 
            input[current] != '/')
        {
            subType = default(StringSegment);
            return 0;
        }
        
        current++; // skip delimiter.
        current +=  HttpTokenParsingRules.GetWhitespaceLength(input, current);
        
        var subtypeLength = HttpTokenParsingRules.GetTokenLength(input, current);
        if (subtypeLength == 0)
        {
            subType = default(StringSegment);
            return 0;
        }
        
        subType = new StringSegment(input, current, subtypeLength);
        
        current +=  subtypeLength;
        current +=  HttpTokenParsingRules.GetWhitespaceLength(input, current);
        
        return current - offset;
    }
    
    private static bool TryGetSuffixLength(
        StringSegment subType, 
        out int suffixLength)
    {
        // Find the last instance of '+', if there is one
        var startPos = subType.Offset + subType.Length - 1;
        for (var currentPos = startPos; currentPos >= subType.Offset; currentPos--)
        {
            if (subType.Buffer[currentPos] == '+')
            {
                suffixLength = startPos - currentPos;
                return true;
            }
        }
        
        suffixLength = 0;
        return false;
    }

     public bool MatchesAllTypes => 
        Type.Equals("*", StringComparison.OrdinalIgnoreCase);       
    public bool MatchesAllSubTypes => 
        SubType.Equals("*", StringComparison.OrdinalIgnoreCase);        
    public bool MatchesAllSubTypesWithoutSuffix => 
        SubTypeWithoutSuffix.Equals("*", StringComparison.OrdinalIgnoreCase);
	public bool HasWildcard
    {
        get
        {
            return MatchesAllTypes ||
                   MatchesAllSubTypesWithoutSuffix ||
                   GetParameter("*").Equals("*", StringComparison.OrdinalIgnoreCase);
        }
    }
       
    public bool IsSubsetOf(MediaType set)
    {
        return MatchesType(set) &&
               MatchesSubtype(set) &&
               ContainsAllParameters(set._parameterParser);
    }    
                                       
    public Encoding Encoding => GetEncodingFromCharset(GetParameter("charset"));        
    public StringSegment Charset => GetParameter("charset");
    
    private static Encoding GetEncodingFromCharset(StringSegment charset)
    {
        if (charset.Equals("utf-8", StringComparison.OrdinalIgnoreCase))
        {
            // This is an optimization for utf-8 that prevents the Substring caused by
            // charset.Value
            return Encoding.UTF8;
        }
        
        try
        {
            // charset.Value might be an invalid encoding name as in charset=invalid.
            // For that reason, we catch the exception thrown by Encoding.GetEncoding
            // and return null instead.
            return charset.HasValue ? Encoding.GetEncoding(charset.Value) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
    
    public StringSegment GetParameter(string parameterName)
    {
        return GetParameter(new StringSegment(parameterName));
    }
       
    public StringSegment GetParameter(StringSegment parameterName)
    {
        var parametersParser = _parameterParser;
        
        while (parametersParser.ParseNextParameter(out var parameter))
        {
            if (parameter.HasName(parameterName))
            {
                return parameter.Value;
            }
        }
        
        return new StringSegment();
    }
    
    public static Encoding GetEncoding(string mediaType)
    {
        return GetEncoding(new StringSegment(mediaType));
    }
        
    public static Encoding GetEncoding(StringSegment mediaType)
    {
        var parsedMediaType = new MediaType(mediaType);
        return parsedMediaType.Encoding;
    }
                                               
    public static string ReplaceEncoding(string mediaType, Encoding encoding)
    {
        return ReplaceEncoding(new StringSegment(mediaType), encoding);
    }
            
    public static string ReplaceEncoding(StringSegment mediaType, Encoding encoding)
    {
        var parsedMediaType = new MediaType(mediaType);
        var charset = parsedMediaType.GetParameter("charset");
        
        if (charset.HasValue && 
            charset.Equals(encoding.WebName, StringComparison.OrdinalIgnoreCase))
        {
            return mediaType.Value;
        }
        
        if (!charset.HasValue)
        {
            return CreateMediaTypeWithEncoding(mediaType, encoding);
        }
        
        var charsetOffset = charset.Offset - mediaType.Offset;
        var restOffset = charsetOffset + charset.Length;
        var restLength = mediaType.Length - restOffset;
        var finalLength = charsetOffset + encoding.WebName.Length + restLength;
        
        var builder = 
            new StringBuilder(
            		mediaType.Buffer, 
            		mediaType.Offset, 
            		charsetOffset, 
            		finalLength);
        
        builder.Append(encoding.WebName);
        builder.Append(mediaType.Buffer, restOffset, restLength);
        
        return builder.ToString();
    }
                            
    public static MediaTypeSegmentWithQuality CreateMediaTypeSegmentWithQuality(
        string mediaType, 
        int start)
    {
        var parsedMediaType = new MediaType(mediaType, start, length: null);
        
        // Short-circuit use of the MediaTypeParameterParser 
        // if constructor detected an invalid type or subtype.
        // Parser would set ParsingFailed==true in this case. 
        // But, we handle invalid parameters as a separate case.
        if (parsedMediaType.Type.Equals(default(StringSegment)) ||
            parsedMediaType.SubType.Equals(default(StringSegment)))
        {
            return default(MediaTypeSegmentWithQuality);
        }
        
        var parser = parsedMediaType._parameterParser;
        
        var quality = 1.0d;
        while (parser.ParseNextParameter(out var parameter))
        {
            if (parameter.HasName(QualityParameter))
            {
                // If media type contains two `q` values i.e. it's invalid in an uncommon way, 
                // pick last value.
                quality = double.Parse(
                    				parameter.Value.Value, 
                    				NumberStyles.AllowDecimalPoint,
                    				NumberFormatInfo.InvariantInfo);
            }
        }
        
        // We check if the parsed media type has a value at this stage when we have iterated
        // over all the parameters and we know if the parsing was successful.
        if (parser.ParsingFailed)
        {
            return default(MediaTypeSegmentWithQuality);
        }
        
        return new MediaTypeSegmentWithQuality(
            new StringSegment(mediaType, start, parser.CurrentOffset - start),
            quality);
    }
    
        
    private static string CreateMediaTypeWithEncoding(
        StringSegment mediaType, 
        Encoding encoding)
    {
        return $"{mediaType.Value}; charset={encoding.WebName}";
    }
    
    private bool MatchesType(MediaType set)
    {
        return set.MatchesAllTypes ||
               set.Type.Equals(Type, StringComparison.OrdinalIgnoreCase);
    }
    
    private bool MatchesSubtype(MediaType set)
    {
        if (set.MatchesAllSubTypes)
        {
            return true;
        }
        
        if (set.SubTypeSuffix.HasValue)
        {
            if (SubTypeSuffix.HasValue)
            {
                // Both the set and the media type being checked have suffixes, 
                // so both parts must match.
                return MatchesSubtypeWithoutSuffix(set) && MatchesSubtypeSuffix(set);
            }
            else
            {
                // The set has a suffix, but the media type being checked doesn't. 
                // We never consider this to match.
                return false;
            }
        }
        else
        {
            // If this subtype or suffix matches the subtype of the set,
            // it is considered a subtype.
            // Ex: application/json > application/val+json
            return MatchesEitherSubtypeOrSuffix(set);
        }
    }
    
    private bool MatchesSubtypeWithoutSuffix(MediaType set)
    {
        return set.MatchesAllSubTypesWithoutSuffix ||
               set.SubTypeWithoutSuffix.Equals(
            								SubTypeWithoutSuffix, 
            								StringComparison.OrdinalIgnoreCase);
    }
    
    private bool MatchesSubtypeSuffix(MediaType set)
    {
        // We don't have support for wildcards on suffixes alone (e.g., "application/entity+*")
        // because there's no clear use case for it.
        return set.SubTypeSuffix.Equals(SubTypeSuffix, StringComparison.OrdinalIgnoreCase);
    }
    
    private bool MatchesEitherSubtypeOrSuffix(MediaType set)
    {
        return set.SubType.Equals(SubType, StringComparison.OrdinalIgnoreCase) ||
               set.SubType.Equals(SubTypeSuffix, StringComparison.OrdinalIgnoreCase);
    }
    
    private bool ContainsAllParameters(MediaTypeParameterParser setParameters)
    {
        var parameterFound = true;
        while (setParameters.ParseNextParameter(out var setParameter) && parameterFound)
        {
            if (setParameter.HasName("q"))
            {
                // "q" and later parameters are not involved in media type matching. 
                // Quoting the RFC: The first "q" parameter (if any) separates the 
                // media-range parameter(s) from the accept-params.
                break;
            }
            
            if (setParameter.HasName("*"))
            {
                // A parameter named "*" has no effect on media type matching, 
                // as it is only used as an indication that the entire media type string 
                // should be treated as a wildcard.
                continue;
            }
            
            // Copy the parser as we need to iterate multiple times over it.
            // We can do this because it's a struct
            var subSetParameters = _parameterParser;
            parameterFound = false;
            while (subSetParameters.ParseNextParameter(out var subSetParameter) && 
                   !parameterFound)
            {
                parameterFound = subSetParameter.Equals(setParameter);
            }
        }
        
        return parameterFound;
    }                
}

```

###### 2.1.1.1 media type segment with quality 

```c#
public readonly struct MediaTypeSegmentWithQuality
{
    public StringSegment MediaType { get; }
    public double Quality { get; }
    
    public MediaTypeSegmentWithQuality(StringSegment mediaType, double quality)
    {
        MediaType = mediaType;
        Quality = quality;
    }
    
    public override string ToString()
    {
        // For logging purposes
        return MediaType.ToString();
    }
}

```

###### 2.1.1.2 media type parameter

```c#
public readonly struct MediaType
{        
    private readonly struct MediaTypeParameter : IEquatable<MediaTypeParameter>
    {
        public StringSegment Name { get; }    
        public StringSegment Value { get; }
        
        public MediaTypeParameter(StringSegment name, StringSegment value)
        {
            Name = name;
            Value = value;
        }
                        
        public bool HasName(string name)
        {
            return HasName(new StringSegment(name));
        }
        
        public bool HasName(StringSegment name)
        {
            return Name.Equals(name, StringComparison.OrdinalIgnoreCase);
        }
        
        public bool Equals(MediaTypeParameter other)
        {
            return HasName(other.Name) && 
                   Value.Equals(other.Value, StringComparison.OrdinalIgnoreCase);
        }
        
        public override string ToString() => $"{Name}={Value}";
    }
}

```

###### 2.1.1.2 media type parameter parser

```c#
public readonly struct MediaType
{
    private struct MediaTypeParameterParser
    {
        private readonly string _mediaTypeBuffer;
        private readonly int? _length;
        
        public int CurrentOffset { get; private set; }        
        public bool ParsingFailed { get; private set; }
        
        public MediaTypeParameterParser(
            string mediaTypeBuffer, 
            int offset, 
            int? length)
        {
            _mediaTypeBuffer = mediaTypeBuffer;
            _length = length;
            CurrentOffset = offset;
            ParsingFailed = false;
        }
                        
        public bool ParseNextParameter(out MediaTypeParameter result)
        {
            if (_mediaTypeBuffer == null)
            {
                ParsingFailed = true;
                result = default(MediaTypeParameter);
                return false;
            }
            
            var parameterLength = GetParameterLength(
                					  _mediaTypeBuffer, 
                					  CurrentOffset, 
                					  out result);
            
            CurrentOffset +=  parameterLength;
            
            if (parameterLength == 0)
            {
                ParsingFailed = _length != null && 
                    			CurrentOffset < _length;
                return false;
            }
            
            return true;
        }
        
        private static int GetParameterLength(
            string input, 
            int startIndex, 
            out MediaTypeParameter parsedValue)
        {
            if (OffsetIsOutOfRange(startIndex, input.Length) || 
                input[startIndex] != ';')
            {
                parsedValue = default(MediaTypeParameter);
                return 0;
            }
            
            var nameLength = GetNameLength(input, startIndex, out var name);
            
            var current = startIndex + nameLength;
            
            if (nameLength == 0 || 
                OffsetIsOutOfRange(current, input.Length) || 
                input[current] != '=')
            {
                if (current == input.Length && 
                    name.Equals("*", StringComparison.OrdinalIgnoreCase))
                {
                    // As a special case, we allow a trailing ";*" to indicate a wildcard       
                    // string allowing any other parameters. It's the same as ";*=*".
                    var asterisk = new StringSegment("*");
                    parsedValue = new MediaTypeParameter(asterisk, asterisk);
                    return current - startIndex;
                }
                else
                {
                    parsedValue = default(MediaTypeParameter);
                    return 0;
                }
            }
            
            var valueLength = GetValueLength(input, current, out var value);
            
            parsedValue = new MediaTypeParameter(name, value);
            current +=  valueLength;
            
            return current - startIndex;
        }
        
        private static int GetNameLength(
            string input, 
            int startIndex, 
            out StringSegment name)
        {
            var current = startIndex;
            
            current++; // skip ';'
            current +=  HttpTokenParsingRules.GetWhitespaceLength(input, current);
            
            var nameLength = HttpTokenParsingRules.GetTokenLength(input, current);
            if (nameLength == 0)
            {
                name = default(StringSegment);
                return 0;
            }
            
            name = new StringSegment(input, current, nameLength);
            
            current +=  nameLength;
            current +=  HttpTokenParsingRules.GetWhitespaceLength(input, current);
            
            return current - startIndex;
        }
        
        private static int GetValueLength(
            string input, 
            int startIndex, 
            out StringSegment value)
        {
            var current = startIndex;
            
            current++; // skip '='.
            current +=  HttpTokenParsingRules.GetWhitespaceLength(input, current);
            
            var valueLength = HttpTokenParsingRules.GetTokenLength(input, current);
            
            if (valueLength == 0)
            {
                // A value can either be a token or a quoted string. 
                // Check if it is a quoted string.
                var result = 
                    HttpTokenParsingRules.GetQuotedStringLength(
                    						  input, 
                    						  current, 
                    						  out valueLength);
                if (result != HttpParseResult.Parsed)
                {
                    // We have an invalid value. Reset the name and return.
                    value = default(StringSegment);
                    return 0;
                }
                
                // Quotation marks are not part of a quoted parameter value.
                value = new StringSegment(input, current + 1, valueLength - 2);
            }
            else
            {
                value = new StringSegment(input, current, valueLength);
            }
            
            current +=  valueLength;
            current +=  HttpTokenParsingRules.GetWhitespaceLength(input, current);
            
            return current - startIndex;
        }
        
        private static bool OffsetIsOutOfRange(int offset, int length)
        {
            return offset < 0 || offset >= length;
        }
    }
```

##### 2.1.2 media type collection

```c#
public class MediaTypeCollection : Collection<string>
{       
    public void Add(MediaTypeHeaderValue item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));            
        }
        
        Add(item.ToString());
    }
       
    public void Insert(int index, MediaTypeHeaderValue item)
    {
        if (index < 0 || index > Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }
        
        Insert(index, item.ToString());
    }
           
    public bool Remove(MediaTypeHeaderValue item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }
        
        return Remove(item.ToString());
    }
}

```

###### 2.1.2.1 media type header value

```c#
internal static class MediaTypeHeaderValues
{
    public static readonly MediaTypeHeaderValue ApplicationJson
        = MediaTypeHeaderValue.Parse("application/json").CopyAsReadOnly();
    
    public static readonly MediaTypeHeaderValue TextJson
        = MediaTypeHeaderValue.Parse("text/json").CopyAsReadOnly();
    
    public static readonly MediaTypeHeaderValue ApplicationAnyJsonSyntax
        = MediaTypeHeaderValue.Parse("application/*+json").CopyAsReadOnly();
    
    public static readonly MediaTypeHeaderValue ApplicationXml
        = MediaTypeHeaderValue.Parse("application/xml").CopyAsReadOnly();
    
    public static readonly MediaTypeHeaderValue TextXml
        = MediaTypeHeaderValue.Parse("text/xml").CopyAsReadOnly();
    
    public static readonly MediaTypeHeaderValue ApplicationAnyXmlSyntax
        = MediaTypeHeaderValue.Parse("application/*+xml").CopyAsReadOnly();
}

```

#### 2.2 input formatter

##### 2.2.1 input formatter 抽象

###### 2.2.1.1 input formatter 接口

```c#
public interface IInputFormatter
{    
    bool CanRead(InputFormatterContext context);        
    Task<InputFormatterResult> ReadAsync(InputFormatterContext context);
}

```

###### 2.2.1.2 input formatter context

```c#
public class InputFormatterContext
{
    public bool TreatEmptyInputAsDefaultValue { get; }        
    public HttpContext HttpContext { get; }       
    public string ModelName { get; }       
    public ModelStateDictionary ModelState { get; }       
    public ModelMetadata Metadata { get; }        
    public Type ModelType { get; }        
    public Func<Stream, Encoding, TextReader> ReaderFactory { get; }
    
    public InputFormatterContext(
        HttpContext httpContext,
        string modelName,
        ModelStateDictionary modelState,
        ModelMetadata metadata,        
        Func<Stream, Encoding, TextReader> readerFactory)
            : this(
                httpContext, 
                modelName, 
                modelState, 
                metadata, 
                readerFactory, 
                treatEmptyInputAsDefaultValue: false)
    {
    }
       
    public InputFormatterContext(
        HttpContext httpContext,
        string modelName,
        ModelStateDictionary modelState,
        ModelMetadata metadata,
        Func<Stream, Encoding, TextReader> readerFactory,
        bool treatEmptyInputAsDefaultValue)
    {
        if (httpContext == null)
        {
            throw new ArgumentNullException(nameof(httpContext));
        }        
        if (modelName == null)
        {
            throw new ArgumentNullException(nameof(modelName));
        }        
        if (modelState == null)
        {
            throw new ArgumentNullException(nameof(modelState));
        }        
        if (metadata == null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }        
        if (readerFactory == null)
        {
            throw new ArgumentNullException(nameof(readerFactory));
        }
        
        HttpContext = httpContext;
        ModelName = modelName;
        ModelState = modelState;
        Metadata = metadata;
        ReaderFactory = readerFactory;
        TreatEmptyInputAsDefaultValue = treatEmptyInputAsDefaultValue;
        ModelType = metadata.ModelType;
    }                
}

```

##### 2.2.2 input formatter 抽象基类

```c#
public abstract class InputFormatter : 
	IInputFormatter, 
	IApiRequestFormatMetadataProvider
{        
    public MediaTypeCollection SupportedMediaTypes { get; } = new MediaTypeCollection();

    /* can read */
    
    /// <inheritdoc />
    public virtual bool CanRead(InputFormatterContext context)
    {
        if (SupportedMediaTypes.Count == 0)
        {
            var message = 
                Resources.FormatFormatter_NoMediaTypes(
	                GetType().FullName,                    
    	            nameof(SupportedMediaTypes));
            
            throw new InvalidOperationException(message);
        }
        
        // can read type 方法，在派生类中重写
        if (!CanReadType(context.ModelType))
        {
            return false;
        }
        
        // 解析 content type
        var contentType = context.HttpContext.Request.ContentType;
        if (string.IsNullOrEmpty(contentType))
        {
            return false;
        }
        
        // Confirm the request's content type is more specific than 
        // a media type this formatter supports e.g. OK if
        // client sent "text/plain" data and this formatter supports "text/*".
        return IsSubsetOfAnySupportedContentType(contentType);
    }
           
    private bool IsSubsetOfAnySupportedContentType(string contentType)
    {
        // 由 content type 创建 media type
        var parsedContentType = new MediaType(contentType);
        // 通过 media type 的方法判断 content type 是否 supported media type 的子集
        for (var i = 0; i < SupportedMediaTypes.Count; i++)
        {
            var supportedMediaType = new MediaType(SupportedMediaTypes[i]);
            if (parsedContentType.IsSubsetOf(supportedMediaType))
            {
                return true;
            }
        }
        return false;
    }
    
     protected virtual bool CanReadType(Type type)
    {
        return true;
    }
    
    /* read async */
    
    /// <inheritdoc />
    public virtual Task<InputFormatterResult> ReadAsync(InputFormatterContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        // 解析 http request
        var request = context.HttpContext.Request;
        
        // 如果 request 为 empty，创建...
        if (request.ContentLength == 0)
        {
            if (context.TreatEmptyInputAsDefaultValue)
            {
                return InputFormatterResult
                    .SuccessAsync(GetDefaultValueForType(context.ModelType));
            }
            
            return InputFormatterResult.NoValueAsync();
        }
        
        // 调用具体方法，在派生类中实现
        return ReadRequestBodyAsync(context);
    }
        
    protected virtual object GetDefaultValueForType(Type modelType)
    {
        if (modelType == null)            
        {
            throw new ArgumentNullException(nameof(modelType));
        }        
        if (modelType.IsValueType)
        {
            return Activator.CreateInstance(modelType);
        }
        
        return null;
    }                              
        
    // 在派生类实现具体 read 方法，
    // 结果封装为 input format result（model是最终绑定结果）
    public abstract Task<InputFormatterResult> ReadRequestBodyAsync(
        InputFormatterContext context);
                  
    /* get supported content type */
    
    /// <inheritdoc />
    public virtual IReadOnlyList<string> GetSupportedContentTypes(
        string contentType, 
        Type objectType)
    {
        if (SupportedMediaTypes.Count == 0)
        {
            var message = Resources.FormatFormatter_NoMediaTypes(
                GetType().FullName,
                nameof(SupportedMediaTypes));
            
            throw new InvalidOperationException(message);
        }
        
        if (!CanReadType(objectType))
        {
            return null;
        }
        
        // 如果 content type 为 null，即 request 没有指定 content type，
        // 所有 formatter 支持的 media type 都是 valid
        if (contentType == null)
        {
            // If contentType is null, then any type we support is valid.
            return SupportedMediaTypes;
        }
        else
        {
            // 由 content type 创建 media type
            var parsedContentType = new MediaType(contentType);
            
            List<string> mediaTypes = null;
            
            // Confirm this formatter supports a more specific media type than 
            // requested e.g. OK if "text/*" requested and formatter supports "text/plain". 
            // Treat contentType like it came from an Content-Type header.
            foreach (var mediaType in SupportedMediaTypes)
            {
                var parsedMediaType = new MediaType(mediaType);
                /* 通过 media type 的方法判断，
                   如果 content type 是 supported type 的子集，
                   将 content type 添加到 supported media type */
                if (parsedMediaType.IsSubsetOf(parsedContentType))
                {
                    if (mediaTypes == null)
                    {
                        mediaTypes = new List<string>(SupportedMediaTypes.Count);
                    }
                    
                    mediaTypes.Add(mediaType);
                }
            }
            
            return mediaTypes;
        }
    }                    
}

```

###### 2.2.2.1 input formatter rusult

```c#
public class InputFormatterResult
{
    public bool HasError { get; }        
    public bool IsModelSet { get; }        
    public object? Model { get; }
    
     private InputFormatterResult(object model)
    {
        Model = model;
        IsModelSet = true;
    }
    
    private InputFormatterResult(bool hasError)
    {
        HasError = hasError;
    }
       
    /* for static success instance */
    public static InputFormatterResult Success(object model)
    {
        return new InputFormatterResult(model);
    }
        
    public static Task<InputFormatterResult> SuccessAsync(object model)
    {
        return Task.FromResult(Success(model));
    }
        
    /* for static failure instance */
    private static readonly InputFormatterResult _failure = 
        new InputFormatterResult(hasError: true);
            
    public static InputFormatterResult Failure()        
    {
        return _failure;
    }
    
    private static readonly Task<InputFormatterResult> _failureAsync = 
        Task.FromResult(_failure);
           
    public static Task<InputFormatterResult> FailureAsync()
    {
        return _failureAsync;
    }
    
    /* for static no value instance */    
    private static readonly InputFormatterResult _noValue = 
        new InputFormatterResult(hasError: false);
    
    public static InputFormatterResult NoValue()
    {
        return _noValue;
    }
        
    private static readonly Task<InputFormatterResult> _noValueAsync = 
        Task.FromResult(_noValue);

    public static Task<InputFormatterResult> NoValueAsync()
    {
        return _noValueAsync;
    }    
}

```

##### 2.2.3 text input formatter

###### 2.2.3.1 抽象基类

```c#
public abstract class TextInputFormatter : InputFormatter
{       
    protected static readonly Encoding UTF8EncodingWithoutBOM = 
        new UTF8Encoding(
        		encoderShouldEmitUTF8Identifier: false, 
        		throwOnInvalidBytes: true);
       
    protected static readonly Encoding UTF16EncodingLittleEndian = 
        new UnicodeEncoding(
        		bigEndian: false, 
        		byteOrderMark: true, 
        		throwOnInvalidBytes: true);
        
    public IList<Encoding> SupportedEncodings { get; } = new List<Encoding>();

    /// <inheritdoc />
    public override Task<InputFormatterResult> ReadRequestBodyAsync(
        InputFormatterContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        // 解析 encoding
        var selectedEncoding = SelectCharacterEncoding(context);
        // 如果 encoding 为 null，抛出异常
        if (selectedEncoding == null)
        {
            var message = Resources.FormatUnsupportedContentType(
                context.HttpContext.Request.ContentType);            
            var exception = new UnsupportedContentTypeException(message);
            
            context.ModelState
                   .AddModelError(
                		context.ModelName, 
                		exception, 
                		context.Metadata);
            
            return InputFormatterResult.FailureAsync();
        }
        // 使用具体 read body 方法，在派生类中实现
        return ReadRequestBodyAsync(context, selectedEncoding);
    }                            
    
    // 解析 encoding
    protected Encoding SelectCharacterEncoding(InputFormatterContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (SupportedEncodings.Count == 0)
        {
            var message = 
                Resources.FormatTextInputFormatter_SupportedEncodingsMustNotBeEmpty(
                    nameof(SupportedEncodings));            
            throw new InvalidOperationException(message);
        }
        
        /* 解析 content type 对应的 media type */
        var requestContentType = context.HttpContext.Request.ContentType;
        var requestMediaType = string.IsNullOrEmpty(requestContentType) 
            					   ? default 
            					   : new MediaType(requestContentType);
        
        /* 如果 media type 指定了 charset， */
        if (requestMediaType.Charset.HasValue)
        {
            /* 如果 supported encoding 中存在与 media encoding 同名的 encoding，
               返回该 encoding */
            // Create Encoding based on requestMediaType.Charset to support charset aliases 
            // and custom Encoding providers. Charset -> Encoding -> encoding.WebName chain 
            // canonicalizes the charset name.
            var requestEncoding = requestMediaType.Encoding;
            if (requestEncoding != null)
            {                
                for (int i = 0; i < SupportedEncodings.Count; i++)
                {
                    if (string.Equals(
                        	requestEncoding.WebName,
	                        SupportedEncodings[i].WebName,
    	                    StringComparison.OrdinalIgnoreCase))
                    {
                        return SupportedEncodings[i];
                    }
                }
            }
            /* 否则，即 supported encoding 中不存在同名 encoding，
               返回 null */            
            // The client specified an encoding in the content type header of the request
            // but we don't understand it. 
            // In this situation we don't try to pick any other encoding from the list of 
            // supported encodings and read the body with that encoding.
            // Instead, we return null and that will translate later on into a 415 Unsupported 
            // Media Type response.
            return null;        
        }
		
        /* media type 没有指定charset */
        // We want to do our best effort to read the body of the request even in the
        // cases where the client doesn't send a content type header or sends a content
        // type header without encoding. 
        // For that reason we pick the first encoding of the  list of supported encodings 
        // and try to use that to read the body. 
        // This encoding is UTF-8 by default in our formatters, which generally is a safe 
        // choice for the encoding.
        return SupportedEncodings[0];
    }
    
    // 在派生类中实现具体 read 方法
    public abstract Task<InputFormatterResult> ReadRequestBodyAsync(
        InputFormatterContext context,
        Encoding encoding);
}

```

###### 2.2.3.2 input formatter exception policy 接口

```c#
public interface IInputFormatterExceptionPolicy
{   
    InputFormatterExceptionPolicy ExceptionPolicy { get; }
}

public enum InputFormatterExceptionPolicy
{     
    AllExceptions = 0,             
    MalformedInputExceptions = 1,
}

```

###### 2.2.3.3 json input formatter

```c#
public class SystemTextJsonInputFormatter : 
	TextInputFormatter, 
	IInputFormatterExceptionPolicy
{
    private readonly ILogger<SystemTextJsonInputFormatter> _logger;
     /// <inheritdoc />
    InputFormatterExceptionPolicy IInputFormatterExceptionPolicy.ExceptionPolicy => 
        InputFormatterExceptionPolicy.MalformedInputExceptions;
        
    public JsonSerializerOptions SerializerOptions { get; }
        
    public SystemTextJsonInputFormatter(
        JsonOptions options,
        ILogger<SystemTextJsonInputFormatter> logger)
    {
        /* 注入 options 和 logger */
        SerializerOptions = options.JsonSerializerOptions;
        _logger = logger;
        
        /* 添加 supported encoding */
        SupportedEncodings.Add(UTF8EncodingWithoutBOM);
        SupportedEncodings.Add(UTF16EncodingLittleEndian);
        
        /* 添加 supported media type*/
        SupportedMediaTypes.Add(MediaTypeHeaderValues.ApplicationJson);
        SupportedMediaTypes.Add(MediaTypeHeaderValues.TextJson);
        SupportedMediaTypes.Add(MediaTypeHeaderValues.ApplicationAnyJsonSyntax);
    }
           
    /* 实现 read body 方法 */
    /// <inheritdoc />
    public sealed override async Task<InputFormatterResult> ReadRequestBodyAsync(
        InputFormatterContext context,
        Encoding encoding)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (encoding == null)
        {
            throw new ArgumentNullException(nameof(encoding));
        }
        
        // 解析 http context
        var httpContext = context.HttpContext;
        // read 到 stream
        var (inputStream, usesTranscodingStream) = GetInputStream(httpContext, encoding);
        
        object model;
        
        try
        {
            // 逆序列化 stream 到 model
            model = await JsonSerializer.DeserializeAsync(
                							inputStream, 
                							context.ModelType, 
                							SerializerOptions);
        }
        catch (JsonException jsonException)
        {
            var path = jsonException.Path;
            
            var formatterException = 
                new InputFormatterException(jsonException.Message, jsonException);
            
            context.ModelState
                   .TryAddModelError(
                		path, 
                		formatterException, 
                		context.Metadata);
            
            Log.JsonInputException(_logger, jsonException);            
            return InputFormatterResult.Failure();
        }
        catch (Exception exception) when (exception is FormatException || 
                                          exception is OverflowException)
        {
            // The code in System.Text.Json never throws these exceptions. 
            // However a custom converter could produce these errors for instance when
            // parsing a value. 
            // These error messages are considered safe to report to users using ModelState.   
            context.ModelState
                   .TryAddModelError(
                		string.Empty, 
                		exception, 
                		context.Metadata);
            
            Log.JsonInputException(_logger, exception);            
            return InputFormatterResult.Failure();
        }
        finally
        {
            if (usesTranscodingStream)
            {
                await inputStream.DisposeAsync();
            }
        }
        
        /* model 为 null，且 no default value，
           返回 no value result */
        if (model == null && 
            !context.TreatEmptyInputAsDefaultValue)
        {
            // Some nonempty inputs might deserialize as null, for example whitespace,
            // or the JSON-encoded value "null". The upstream BodyModelBinder needs to
            // be notified that we don't regard this as a real input so it can register
            // a model binding error.
            return InputFormatterResult.NoValue();
        }
        /* model 不为 null，返回封装 model 的 formatter result */
        else
        {
            Log.JsonInputSuccess(_logger, context.ModelType);
            return InputFormatterResult.Success(model);
        }
    }
    
    // 读取 http request body 到 input stream
    private (Stream inputStream, bool usesTranscodingStream) 
        GetInputStream(HttpContext httpContext, Encoding encoding)
    {
        if (encoding.CodePage == Encoding.UTF8.CodePage)
        {
            return (httpContext.Request.Body, false);
        }
        
        var inputStream = Encoding.CreateTranscodingStream(
            						   httpContext.Request.Body, 
            						   encoding, 
            						   Encoding.UTF8, 
            						   leaveOpen: true);
        return (inputStream, true);
    }
    
    private static class Log
    {
        private static readonly Action<ILogger, string, Exception> 
            _jsonInputFormatterException;
        private static readonly Action<ILogger, string, Exception> 
            _jsonInputSuccess;
        
        static Log()
        {
            _jsonInputFormatterException = LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(1, "SystemTextJsonInputException"),
                "JSON input formatter threw an exception: {Message}");
            _jsonInputSuccess = LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(2, "SystemTextJsonInputSuccess"),
                "JSON input formatter succeeded, deserializing to type '{TypeName}'");
        }
        
        public static void JsonInputException(ILogger logger, Exception exception) 
            => _jsonInputFormatterException(logger, exception.Message, exception);
        
        public static void JsonInputSuccess(ILogger logger, Type modelType)
            => _jsonInputSuccess(logger, modelType.FullName, null);
    }
}

```

#### 2.3 output formatter

##### 2.3.1 output formatter 抽象

###### 2.3.1.1 output formatter 接口

```c#
public interface IOutputFormatter
{        
    bool CanWriteResult(OutputFormatterCanWriteContext context);           
    Task WriteAsync(OutputFormatterWriteContext context);
}

```

###### 2.3.1.2 output formatter can write context

```c#
public abstract class OutputFormatterCanWriteContext
{
    public virtual HttpContext HttpContext { get; protected set; }        
    public virtual StringSegment ContentType { get; set; }        
    public virtual bool ContentTypeIsServerDefined { get; set; }       
    public virtual object? Object { get; protected set; }        
    public virtual Type? ObjectType { get; protected set; }
    
    protected OutputFormatterCanWriteContext(HttpContext httpContext)
    {
        if (httpContext == null)
        {
            throw new ArgumentNullException(nameof(httpContext));
        }
        
        HttpContext = httpContext;
    }                
}

```

###### 2.3.1.3 output formatter write context

```c#
public class OutputFormatterWriteContext : OutputFormatterCanWriteContext
{    
    // 创建 text write 的委托，
    // 创建的 text write 按照 encoding 写入 http response body
    public virtual Func<Stream, Encoding, TextWriter> WriterFactory { get; protected set; }
    
    public OutputFormatterWriteContext(
        HttpContext httpContext, 
        Func<Stream, Encoding, TextWriter> writerFactory, 
        Type? objectType, 
        object? @object)            
        	: base(httpContext)
    {
        if (writerFactory == null)
        {
            throw new ArgumentNullException(nameof(writerFactory));
        }
        
        WriterFactory = writerFactory;
        ObjectType = objectType;
        Object = @object;
    }        
}

```

##### 2.3.2 output formatter 抽象基类

```c#
public abstract class OutputFormatter : 
	IOutputFormatter, 
	IApiResponseTypeMetadataProvider
{        
    public MediaTypeCollection SupportedMediaTypes { get; } = new MediaTypeCollection();
                
    /* can write result */
    
    /// <inheritdoc />
    public virtual bool CanWriteResult(OutputFormatterCanWriteContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (SupportedMediaTypes.Count == 0)
        {
            var message = Resources.FormatFormatter_NoMediaTypes(
                						GetType().FullName,
						                nameof(SupportedMediaTypes));
            
            throw new InvalidOperationException(message);
        }
        
        if (!CanWriteType(context.ObjectType))
        {
            return false;
        }
        
        /* 如果 content type 没有赋值，*/
        if (!context.ContentType.HasValue)
        {
            /* 将 supported media type 写入 content type，
               并返回 true */
            // If the desired content type is set to null, 
            // then the current formatter can write anything it wants.
            context.ContentType = new StringSegment(SupportedMediaTypes[0]);
            return true;
        }
        /* 否则，即 content type 指定了内容，*/
        else
        {
            /* 如果 content type 是 supported type 的子集，
               返回 true */
            var parsedContentType = new MediaType(context.ContentType);
            for (var i = 0; i < SupportedMediaTypes.Count; i++)
            {
                var supportedMediaType = new MediaType(SupportedMediaTypes[i]);
                if (supportedMediaType.HasWildcard)
                {
                    // For supported media types that are wildcard patterns, confirm that 
                    // the requested media type satisfies the wildcard pattern (e.g., 
                    // if "text/entity+json;v=2" requested and formatter supports 
                    // "text/*+json").
                    // We only do this when comparing against server-defined content types 
                    // (e.g., those from [Produces] or Response.ContentType), otherwise 
                    // we'd potentially be reflecting back arbitrary Accept header values.
                    if (context.ContentTypeIsServerDefined && 
                        parsedContentType.IsSubsetOf(supportedMediaType))
                    {
                        return true;
                    }
                }
                else
                {
                    // For supported media types that are not wildcard patterns, 
                    // confirm that this formatter supports a more specific media type 
                    // than requested e.g. OK if "text/*" requested and formatter supports 
                    // "text/plain".contentType is typically what we got in an Accept header.
                    if (supportedMediaType.IsSubsetOf(parsedContentType))
                    {
                        context.ContentType = new StringSegment(SupportedMediaTypes[i]);
                        return true;
                    }
                }
            }
        }
        
        return false;
    }
    
    /* write async */
    
    /// <inheritdoc />
    public virtual Task WriteAsync(OutputFormatterWriteContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        // 写入 header        
        WriteResponseHeaders(context);
        // 写入 body，在派生类中实现
        return WriteResponseBodyAsync(context);
    }
        
    public virtual void WriteResponseHeaders(OutputFormatterWriteContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        var response = context.HttpContext.Response;
        // 将 content type 写入 response
        response.ContentType = context.ContentType.Value;
    }
        
    // 在派生类中实现具体 write 方法
    public abstract Task WriteResponseBodyAsync(OutputFormatterWriteContext context);
        
    /* get supported content type */
    
    /// <inheritdoc />
    public virtual IReadOnlyList<string> GetSupportedContentTypes(
        string contentType,
        Type objectType)
    {
        if (SupportedMediaTypes.Count == 0)
        {
            var message = Resources.FormatFormatter_NoMediaTypes(
                GetType().FullName,
                nameof(SupportedMediaTypes));
            
            throw new InvalidOperationException(message);
        }
        
        if (!CanWriteType(objectType))
        {
            return null;
        }
        
        List<string> mediaTypes = null;
        
        // 解析 content type 对应的 media type
        var parsedContentType = contentType != null 
            						? new MediaType(contentType) 
            						: default(MediaType);
        
        // 如果 content type 是 supported media type 的子集，
        // 将 supported type 注入 media types
        foreach (var mediaType in SupportedMediaTypes)
        {
            var parsedMediaType = new MediaType(mediaType);
            if (parsedMediaType.HasWildcard)
            {
                // For supported media types that are wildcard patterns, confirm that the 
                // requested media type satisfies the wildcard pattern 
                // (e.g., if "text/entity+json;v=2" requested and formatter supports 
                // "text/*+json").
                // Treat contentType like it came from a [Produces] attribute.
                if (contentType != null && 
                    parsedContentType.IsSubsetOf(parsedMediaType))
                {
                    if (mediaTypes == null)
                    {
                        mediaTypes = new List<string>(SupportedMediaTypes.Count);
                    }
                    
                    mediaTypes.Add(contentType);
                }
            }
            else
            {
                // Confirm this formatter supports a more specific media type than 
                // requested e.g. OK if "text/*" requested and formatter supports "text/plain".
                // Treat contentType like it came from an Accept header.
                if (contentType == null || 
                    parsedMediaType.IsSubsetOf(parsedContentType))
                {
                    if (mediaTypes == null)
                    {
                        mediaTypes = new List<string>(SupportedMediaTypes.Count);
                    }
                    
                    mediaTypes.Add(mediaType);
                }
            }
        }
        
        // 返回结果
        return mediaTypes;
    }
    
    protected virtual bool CanWriteType(Type type)
    {
        return true;
    }
}

```

##### 2.3.3 text output formatter 抽象基类

```c#
public abstract class TextOutputFormatter : OutputFormatter
{
    private IDictionary<string, string> _outputMediaTypeCache;
    private IDictionary<string, string> OutputMediaTypeCache
    {
        get
        {
            if (_outputMediaTypeCache == null)
            {
                var cache = new Dictionary<string, string>();
                foreach (var mediaType in SupportedMediaTypes)
                {
                    cache.Add(
                        mediaType, 
                        MediaType.ReplaceEncoding(
                            		mediaType, 
                            		Encoding.UTF8));
                }

                // Safe race condition, worst case scenario we initialize the field 
                // multiple times with dictionaries containing the same values.
                _outputMediaTypeCache = cache;
            }
            
            return _outputMediaTypeCache;
        }
    }
    
    public IList<Encoding> SupportedEncodings { get; }
            
    protected TextOutputFormatter()
    {
        SupportedEncodings = new List<Encoding>();
    }
                                
    /// <inheritdoc />
    public override Task WriteAsync(OutputFormatterWriteContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        /* 解析 content type */
        var selectedMediaType = context.ContentType;
        // 如果 content type 没有 value
        if (!selectedMediaType.HasValue)
        {
            /* 将 supported media types[0] 作为 media type 注入到 content type */
            // If content type is not set then set it based on supported media types.
            if (SupportedEncodings.Count > 0)
            {
                selectedMediaType = new StringSegment(SupportedMediaTypes[0]);
            }
            /* 如果 supported encoding 为 null，抛出异常 */
            else
            {
                throw new InvalidOperationException(
                    Resources.FormatOutputFormatterNoMediaType(GetType().FullName));
            }
        }
        
        /* 解析 encoding */
        // a- select charset encoding
        var selectedEncoding = SelectCharacterEncoding(context);
        // 如果 encoding 不为 null，
        // 将 content type、encoding 做出 charset，注入 content type
        if (selectedEncoding != null)
        {
            // b- get media type with charset 
            // Override the content type value even if one already existed.
            var mediaTypeWithCharset = 
                GetMediaTypeWithCharset(
                	selectedMediaType.Value, 
                	selectedEncoding);
            selectedMediaType = new StringSegment(mediaTypeWithCharset);
        }
        // 否则，即 encoding 为 null，返回 406       
        else
        {
            var response = context.HttpContext.Response;
            response.StatusCode = StatusCodes.Status406NotAcceptable;
            return Task.CompletedTask;
        }
        
        context.ContentType = selectedMediaType;
        
        // 写入 header
        WriteResponseHeaders(context);
        // 写入 body，在派生类中实现
        return WriteResponseBodyAsync(context, selectedEncoding);
    }
    
    /// <inheritdoc />
    public sealed override Task WriteResponseBodyAsync(
        OutputFormatterWriteContext context)
    {
        var message = 
            Resources.FormatTextOutputFormatter_WriteResponseBodyAsyncNotSupported(
            	$"{nameof(WriteResponseBodyAsync)}({nameof(OutputFormatterWriteContext)})",
                nameof(TextOutputFormatter),
                $"{nameof(WriteResponseBodyAsync)}({nameof(OutputFormatterWriteContext)},
            	"{nameof(Encoding)})");

        throw new InvalidOperationException(message);
    }
            
    // 在派生类中实现具体的 body 写入方法
    public abstract Task WriteResponseBodyAsync(
        OutputFormatterWriteContext context, 
        Encoding selectedEncoding);
    
    /* a- 解析 accept charset（header value）*/
        
    public virtual Encoding SelectCharacterEncoding(OutputFormatterWriteContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (SupportedEncodings.Count == 0)
        {
            var message = Resources.FormatTextOutputFormatter_SupportedEncodingsMustNotBeEmpty(
                    nameof(SupportedEncodings));
            throw new InvalidOperationException(message);
        }
        
        // 解析 accept charset（header value）
        var acceptCharsetHeaderValues = GetAcceptCharsetHeaderValues(context);
        // 解析 accept charset 对应的 encoding
        var encoding = MatchAcceptCharacterEncoding(acceptCharsetHeaderValues);
        
        /* 如果 encoding 不为 null，返回 encoding */
        if (encoding != null)
        {
            return encoding;
        }
        /* 否则，即 encoding 为 null，
           返回 supported encoding 中找到与 content type charset 同名的 encoding */
        if (context.ContentType.HasValue)
        {
            var parsedContentType = new MediaType(context.ContentType);
            var contentTypeCharset = parsedContentType.Charset;
            if (contentTypeCharset.HasValue)
            {
                for (var i = 0; i < SupportedEncodings.Count; i++)
                {
                    var supportedEncoding = SupportedEncodings[i];
                    if (contentTypeCharset.Equals(
                        	supportedEncoding.WebName, 
                        	StringComparison.OrdinalIgnoreCase))
                    {
                        // This is supported.
                        return SupportedEncodings[i];
                    }
                }
            }
        }
        /* 如果没有同名 encoding，返回 encodings[0] */
        return SupportedEncodings[0];
    }
                    
    internal static IList<StringWithQualityHeaderValue> 
        GetAcceptCharsetHeaderValues(OutputFormatterWriteContext context)
    {
        var request = context.HttpContext.Request;
        if (StringWithQualityHeaderValue.TryParseList(
            	request.Headers[HeaderNames.AcceptCharset], 
            	out IList<StringWithQualityHeaderValue> result))
        {
            return result;
        }
        
        return Array.Empty<StringWithQualityHeaderValue>();
    }
        
    private Encoding MatchAcceptCharacterEncoding(
        IList<StringWithQualityHeaderValue> acceptCharsetHeaders)
    {
        if (acceptCharsetHeaders != null && 
            acceptCharsetHeaders.Count > 0)
        {
            var acceptValues = Sort(acceptCharsetHeaders);
            for (var i = 0; i < acceptValues.Count; i++)
            {
                var charset = acceptValues[i].Value;
                if (!StringSegment.IsNullOrEmpty(charset))
                {
                    for (var j = 0; j < SupportedEncodings.Count; j++)
                    {
                        var encoding = SupportedEncodings[j];
                        if (charset.Equals(
                            	encoding.WebName, 
                            	StringComparison.OrdinalIgnoreCase) ||
                            charset.Equals("*", StringComparison.Ordinal))
                        {
                            return encoding;
                        }
                    }
                }
            }
        }
        
        return null;
    }
    
    // There's no allocation-free way to sort an IList and we may have to filter anyway,
    // so we're going to have to live with the copy + insertion sort.
    private IList<StringWithQualityHeaderValue> 
        Sort(IList<StringWithQualityHeaderValue> values)
    {
        var sortNeeded = false;
        
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (value.Quality == HeaderQuality.NoMatch)
            {
                // Exclude this one
            }
            else if (value.Quality != null)
            {
                sortNeeded = true;
            }
        }
        
        if (!sortNeeded)
        {
            return values;
        }
        
        var sorted = new List<StringWithQualityHeaderValue>();
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (value.Quality == HeaderQuality.NoMatch)
            {
                // Exclude this one
            }
            else
            {
                // Doing an insertion sort.
                var position = sorted.BinarySearch(
                    				value, 
                    				StringWithQualityHeaderValueComparer.QualityComparer);     
                if (position >= 0)
                {
                    sorted.Insert(position + 1, value);
                }
                else
                {
                    sorted.Insert(~position, value);
                }
            }
        }
        
        // We want a descending sort, but BinarySearch does ascending
        sorted.Reverse();
        return sorted;
    }
    
    /* b- 解析 charset 对应的 encoding */
    private string GetMediaTypeWithCharset(
        			   string mediaType, 
        			   Encoding encoding)
    {
        if (string.Equals(
            	encoding.WebName, 
            	Encoding.UTF8.WebName, 
            	StringComparison.OrdinalIgnoreCase) &&
            OutputMediaTypeCache.ContainsKey(mediaType))
        {
            return OutputMediaTypeCache[mediaType];
        }
        
        return MediaType.ReplaceEncoding(mediaType, encoding);
    }                
}

```

###### 2.3.3.1 system text json output formatter

```c#
public class SystemTextJsonOutputFormatter : TextOutputFormatter
{
    public JsonSerializerOptions SerializerOptions { get; }
    
    public SystemTextJsonOutputFormatter(JsonSerializerOptions jsonSerializerOptions)
    {
        /* 注入 serialize options */
        SerializerOptions = jsonSerializerOptions;
        
        /* 注入 support encodings */
        SupportedEncodings.Add(Encoding.UTF8);
        SupportedEncodings.Add(Encoding.Unicode);
        
        /* 注入 support media type */
        SupportedMediaTypes.Add(MediaTypeHeaderValues.ApplicationJson);
        SupportedMediaTypes.Add(MediaTypeHeaderValues.TextJson);
        SupportedMediaTypes.Add(MediaTypeHeaderValues.ApplicationAnyJsonSyntax);
    }
    
    // 实现 write body 方法
    /// <inheritdoc />
    public sealed override async Task WriteResponseBodyAsync(
        OutputFormatterWriteContext context, 
        Encoding selectedEncoding)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (selectedEncoding == null)
        {
            throw new ArgumentNullException(nameof(selectedEncoding));
        }
        
        var httpContext = context.HttpContext;
        
        // context.ObjectType reflects the declared model type when specified.
        // For polymorphic scenarios where the user declares a return type, but returns a 
        // derived type, we want to serialize all the properties on the derived type. 
        // This keeps parity with the behavior you get when the user does not declare 
        // the return type and with Json.Net at least at the top level.
        var objectType = context.Object
            					?.GetType() 
            					?? context.ObjectType 
            					?? typeof(object);
        
        // 解析 response（write）stream
        var responseStream = httpContext.Response.Body;
        
        /* 如果 encoding 是 utf8 */
        if (selectedEncoding.CodePage == Encoding.UTF8.CodePage)
        {
            // 序列化 object 并写入 response stream
            await JsonSerializer.SerializeAsync(
                					responseStream, 
                					context.Object, 
                					objectType, 
                					SerializerOptions);
            
            await responseStream.FlushAsync();
        }
        /* 否则，即不是 utf8 */
        else
        {
            // 创建 transcoding stream
            // JsonSerializer only emits UTF8 encoded output, but we need to write 
            // the response in the encoding specified by selectedEncoding
            var transcodingStream = 
                Encoding.CreateTranscodingStream(
                			httpContext.Response.Body, 
                			selectedEncoding, 
                			Encoding.UTF8, 
                			leaveOpen: true);
            
            ExceptionDispatchInfo exceptionDispatchInfo = null;
            
            // 序列化 object 并写入 transcoding stream
            try
            {
                await JsonSerializer.SerializeAsync(
                    					transcodingStream, 
                    					context.Object, 
                    					objectType, 
                    					SerializerOptions);
                
                await transcodingStream.FlushAsync();
            }
            catch (Exception ex)
            {
                // TranscodingStream may write to the inner stream as part of it's disposal.
                // We do not want this exception "ex" to be eclipsed by any exception 
                // encountered during the write. We will stash it and explicitly 
                // rethrow it during the finally block.
                exceptionDispatchInfo = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                try
                {
                    await transcodingStream.DisposeAsync();
                }
                catch when (exceptionDispatchInfo != null)
                {
                }
                
                exceptionDispatchInfo?.Throw();
            }
        }
    }
    
    /* 创建 json output formatter 的静态方法 */        
    internal static SystemTextJsonOutputFormatter CreateFormatter(JsonOptions jsonOptions)
    {
        var jsonSerializerOptions = jsonOptions.JsonSerializerOptions;
        
        if (jsonSerializerOptions.Encoder is null)
        {
            // If the user hasn't explicitly configured the encoder, 
            // use the less strict encoder that does not encode all non-ASCII characters.
            jsonSerializerOptions = new JsonSerializerOptions(jsonSerializerOptions)
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
        }
        
        return new SystemTextJsonOutputFormatter(jsonSerializerOptions);
    }                
}

```

###### 2.3.3.2 string content formatter

```c#
public class StringOutputFormatter : TextOutputFormatter
{    
    public StringOutputFormatter()
    {
        SupportedEncodings.Add(Encoding.UTF8);
        SupportedEncodings.Add(Encoding.Unicode);           
        SupportedMediaTypes.Add("text/plain");    
    }
    
    /// <inheritdoc/>
    public override bool CanWriteResult(OutputFormatterCanWriteContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        if (context.ObjectType == typeof(string) || 
            context.Object is string)
        {
            // Call into base to check if the current request's content type 
            // is a supported media type.
            return base.CanWriteResult(context);
        }
        
        return false;
    }
    
    /// <inheritdoc/>
    public override Task WriteResponseBodyAsync(
        OutputFormatterWriteContext context, 
        Encoding encoding)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (encoding == null)
        {
            throw new ArgumentNullException(nameof(encoding));
        }
        
        var valueAsString = (string)context.Object;
        
        if (string.IsNullOrEmpty(valueAsString))
        {
            return Task.CompletedTask;
        }
        
        var response = context.HttpContext.Response;
        return response.WriteAsync(valueAsString, encoding);
    }
}

```

##### 2.3.4 stream output formatter

```c#
public class StreamOutputFormatter : IOutputFormatter
{       
    /// <inheritdoc />
    public bool CanWriteResult(OutputFormatterCanWriteContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        // Ignore the passed in content type, if the object is a Stream.
        if (context.Object is Stream)
        {
            return true;
        }
        
        return false;
    }
    
    /// <inheritdoc />
    public async Task WriteAsync(OutputFormatterWriteContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        using (var valueAsStream = ((Stream)context.Object))
        {
            var response = context.HttpContext.Response;
            
            if (context.ContentType != null)
            {
                response.ContentType = context.ContentType.ToString();
            }
            
            await valueAsStream.CopyToAsync(response.Body);
        }
    }
}

```

##### 2.3.5 http no content formatter

```c#
public class HttpNoContentOutputFormatter : IOutputFormatter
{        
    public bool TreatNullValueAsNoContent { get; set; } = true;
    
    /// <inheritdoc />
    public bool CanWriteResult(OutputFormatterCanWriteContext context)
    {
        // ignore the contentType and just look at the content.
        // This formatter will be selected if the content is null.
        // We check for Task as a user can directly create an ObjectContentResult 
        // with the unwrapped type.
        if (context.ObjectType == typeof(void) || 
            context.ObjectType == typeof(Task))
        {
            return true;
        }
        
        return TreatNullValueAsNoContent && 
               context.Object == null;
    }
    
    /// <inheritdoc />
    public Task WriteAsync(OutputFormatterWriteContext context)
    {
        var response = context.HttpContext.Response;
        response.ContentLength = 0;
        
        if (response.StatusCode == StatusCodes.Status200OK)
        {
            response.StatusCode = StatusCodes.Status204NoContent;
        }
        
        return Task.CompletedTask;
    }
}

```

#### 2.4 response content type helper

```c#
internal static class ResponseContentTypeHelper
{
    /* 解析 content type 和 encoding
    	The priority for selecting the content type is:
    	1. ContentType property set on the action result
    	2. HttpResponse.ContentType property set on HttpResponse
    	3. Default content type set on the action result */
        
    // The user supplied content type is not modified and is used as is. 
    // For example, if user sets the content type to be "text/plain" without any encoding, 
    // then the default content type's encoding is used to write the response and the 
    // ContentType header is set to be "text/plain" without any "charset" information.    
    public static void ResolveContentTypeAndEncoding(
        string actionResultContentType,
        string httpResponseContentType,
        string defaultContentType,
        out string resolvedContentType,
        out Encoding resolvedContentTypeEncoding)
    {
        Debug.Assert(defaultContentType != null);
        
        var defaultContentTypeEncoding = MediaType.GetEncoding(defaultContentType);
        Debug.Assert(defaultContentTypeEncoding != null);
        
        // 1. User sets the ContentType property on the action result
        if (actionResultContentType != null)
        {
            // 解析 content type
            resolvedContentType = actionResultContentType;
            // 解析 encoding
            var actionResultEncoding = MediaType.GetEncoding(actionResultContentType);
            resolvedContentTypeEncoding = actionResultEncoding ?? defaultContentTypeEncoding;
            
            return;
        }
        
        // 2. User sets the ContentType property on the http response directly
        if (!string.IsNullOrEmpty(httpResponseContentType))
        {
            var mediaTypeEncoding = MediaType.GetEncoding(httpResponseContentType);
            if (mediaTypeEncoding != null)
            {
                resolvedContentType = httpResponseContentType;
                resolvedContentTypeEncoding = mediaTypeEncoding;
            }
            else
            {
                resolvedContentType = httpResponseContentType;
                resolvedContentTypeEncoding = defaultContentTypeEncoding;
            }
            
            return;
        }
        
        // 3. Fall-back to the default content type
        resolvedContentType = defaultContentType;
        resolvedContentTypeEncoding = defaultContentTypeEncoding;
    }
}

```

#### 2.5 formatter selector

##### 2.5.1 抽象基类

```c#
public abstract class OutputFormatterSelector
{    
    public abstract IOutputFormatter? SelectFormatter(
        OutputFormatterCanWriteContext context, 
        IList<IOutputFormatter> formatters, 
        MediaTypeCollection mediaTypes);
}

```

##### 2.5.2 default formatter selector

```c#
public class DefaultOutputFormatterSelector : OutputFormatterSelector
{
    private static readonly Comparison<MediaTypeSegmentWithQuality> _sortFunction = 
        (left, right) =>
    		{
        		return left.Quality > right.Quality 
                    ? -1 
                    : (left.Quality == right.Quality ? 0 : );
    		};
    
    private readonly ILogger _logger;
    private readonly IList<IOutputFormatter> _formatters;
    private readonly bool _respectBrowserAcceptHeader;
    private readonly bool _returnHttpNotAcceptable;
        
    public DefaultOutputFormatterSelector(
        IOptions<MvcOptions> options, 
        ILoggerFactory loggerFactory)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }        
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }
        
        // 注入 logger
        _logger = loggerFactory.CreateLogger<DefaultOutputFormatterSelector>();
        // 创建 formatter 集合
        _formatters = new ReadOnlyCollection<IOutputFormatter>(options.Value.OutputFormatters);
        /* 从 mvc options 中解析 */
        _respectBrowserAcceptHeader = options.Value.RespectBrowserAcceptHeader;
        _returnHttpNotAcceptable = options.Value.ReturnHttpNotAcceptable;
    }
    
    /// <inheritdoc/>
    public override IOutputFormatter? SelectFormatter(
        OutputFormatterCanWriteContext context, 
        IList<IOutputFormatter> formatters, 
        MediaTypeCollection contentTypes)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (formatters == null)
        {
            throw new ArgumentNullException(nameof(formatters));
        }        
        if (contentTypes == null)
        {
            throw new ArgumentNullException(nameof(contentTypes));
        }
        
        // a- 验证 content type
        ValidateContentTypes(contentTypes);
        
        // 如果输入的备选 formatter 为 empty，
        // 用 selector 的 formatter 作为备选；   
        // 如果 selector formatter 也为 empty，抛出异常
        if (formatters.Count == 0)
        {
            formatters = _formatters;
            if (formatters.Count == 0)
            {
                throw new InvalidOperationException(
                    Resources.FormatOutputFormattersAreRequired(
                        typeof(MvcOptions).FullName,
                        nameof(MvcOptions.OutputFormatters),
                        typeof(IOutputFormatter).FullName));
            }
        }
        
        _logger.RegisteredOutputFormatters(formatters);
        
        // 解析 http request
        var request = context.HttpContext.Request;
        // b- 从 request 解析 media type
        var acceptableMediaTypes = GetAcceptableMediaTypes(request);
        
        var selectFormatterWithoutRegardingAcceptHeader = false;        
        IOutputFormatter? selectedFormatter = null;
        
        // accept media type 为 empty，
        if (acceptableMediaTypes.Count == 0)
        {
            // There is either no Accept header value, or it contained */* and we
            // are not currently respecting the 'browser accept header'.
            _logger.NoAcceptForNegotiation();
            
            
            selectFormatterWithoutRegardingAcceptHeader = true;
        }
        else
        {
            // content types 为 empty
            if (contentTypes.Count == 0)
            {
                _logger.SelectingOutputFormatterUsingAcceptHeader(acceptableMediaTypes);
                
                /* 1- formatter using sorted accept header */
                // Use whatever formatter can meet the client's request
                selectedFormatter = SelectFormatterUsingSortedAcceptHeaders(
                    context,
                    formatters,
                    acceptableMediaTypes);
            }
            // content types 不为 empty
            else
            {
                _logger.SelectingOutputFormatterUsingAcceptHeaderAndExplicitContentTypes(
                    acceptableMediaTypes, 
                    contentTypes);
                
                /* 2- formatter using sorted accept header & content type */
                // Verify that a content type from the context is 
                // compatible with the client's request
                selectedFormatter = SelectFormatterUsingSortedAcceptHeadersAndContentTypes(
                    context,
                    formatters,
                    acceptableMediaTypes,
                    contentTypes);
            }
            
            // 如果没有选择到 formatter，
            if (selectedFormatter == null)
            {
                _logger.NoFormatterFromNegotiation(acceptableMediaTypes);
                
                if (!_returnHttpNotAcceptable)
                {
                    selectFormatterWithoutRegardingAcceptHeader = true;
                }
            }
        }
        
        // 标记了 without regarding accept header 
        if (selectFormatterWithoutRegardingAcceptHeader)
        {
            // content types 为 empty
            if (contentTypes.Count == 0)
            {
                _logger.SelectingOutputFormatterWithoutUsingContentTypes();
                
                /* 3- formatter without content type */
                selectedFormatter = SelectFormatterNotUsingContentType(
                    context,
                    formatters);
            }
            // content types 不为 empty
            else
            {
                _logger.SelectingOutputFormatterUsingContentTypes(contentTypes);
                
                /* 4- formatter using any acceptable content type */
                selectedFormatter = SelectFormatterUsingAnyAcceptableContentType(
                    context,
                    formatters,
                    contentTypes);
            }
        }
        
        if (selectedFormatter != null)
        {
            _logger.FormatterSelected(selectedFormatter, context);
        }
        
        return selectedFormatter;
    }
    
    // a- 验证 content type
    private void ValidateContentTypes(MediaTypeCollection contentTypes)
    {
        for (var i = 0; i < contentTypes.Count; i++)
        {
            var contentType = contentTypes[i];
            
            var parsedContentType = new MediaType(contentType);
            
            if (parsedContentType.HasWildcard)
            {
                var message = Resources.FormatObjectResult_MatchAllContentType(
                        contentType,
                        nameof(ObjectResult.ContentTypes));
                throw new InvalidOperationException(message);
            }
        }
    }
    
    // b- 从 http request 解析 accept media type
    private List<MediaTypeSegmentWithQuality> GetAcceptableMediaTypes(HttpRequest request)
    {
        var result = new List<MediaTypeSegmentWithQuality>();
        
        // 解析 accept header 到 result
        AcceptHeaderParser.ParseAcceptHeader(
            request.Headers[HeaderNames.Accept], 
            result);
        
        // 遍历header，如果有 match all types，
        // 清空 result 并 返回
        for (var i = 0; i < result.Count; i++)
        {
            var mediaType = new MediaType(result[i].MediaType);
            if (!_respectBrowserAcceptHeader && 
                mediaType.MatchesAllSubTypes && 
                mediaType.MatchesAllTypes)
            {
                result.Clear();
                return result;
            }
        }
        
        result.Sort(_sortFunction);        
        return result;
    }
    
    /* 1- */
    private IOutputFormatter? SelectFormatterUsingSortedAcceptHeaders(
        OutputFormatterCanWriteContext formatterContext,
        IList<IOutputFormatter> formatters,
        IList<MediaTypeSegmentWithQuality> sortedAcceptHeaders)
    {
        for (var i = 0; i < sortedAcceptHeaders.Count; i++)
        {
            var mediaType = sortedAcceptHeaders[i];
            
            formatterContext.ContentType = mediaType.MediaType;
            formatterContext.ContentTypeIsServerDefined = false;
            
            for (var j = 0; j < formatters.Count; j++)
            {
                var formatter = formatters[j];
                if (formatter.CanWriteResult(formatterContext))
                {
                    
                    return formatter;
                }
            }
        }
        
        return null;
    }
    
    /* 2- */
    private IOutputFormatter? SelectFormatterUsingSortedAcceptHeadersAndContentTypes(
        OutputFormatterCanWriteContext formatterContext,
        IList<IOutputFormatter> formatters,
        IList<MediaTypeSegmentWithQuality> sortedAcceptableContentTypes,
        MediaTypeCollection possibleOutputContentTypes)
    {
        for (var i = 0; i < sortedAcceptableContentTypes.Count; i++)
        {
            var acceptableContentType = 
                new MediaType(sortedAcceptableContentTypes[i].MediaType);
            for (var j = 0; j < possibleOutputContentTypes.Count; j++)
            {
                var candidateContentType = new MediaType(possibleOutputContentTypes[j]);
                if (candidateContentType.IsSubsetOf(acceptableContentType))
                {
                    for (var k = 0; k < formatters.Count; k++)
                    {
                        var formatter = formatters[k];
                        formatterContext.ContentType = 
                            new StringSegment(possibleOutputContentTypes[j]);
                        formatterContext.ContentTypeIsServerDefined = true;
                        if (formatter.CanWriteResult(formatterContext))
                        {
                            return formatter;
                        }
                    }
                }
            }
        }
        
        return null;
    }
    
    /* 3- */
    private IOutputFormatter? SelectFormatterNotUsingContentType(
        OutputFormatterCanWriteContext formatterContext,
        IList<IOutputFormatter> formatters)
    {
        _logger.SelectFirstCanWriteFormatter();
        
        foreach (var formatter in formatters)
        {
            formatterContext.ContentType = new StringSegment();
            formatterContext.ContentTypeIsServerDefined = false;
            
            if (formatter.CanWriteResult(formatterContext))
            {
                return formatter;
            }
        }
        
        return null;
    }
            
    /* 4- */
    private IOutputFormatter? SelectFormatterUsingAnyAcceptableContentType(
        OutputFormatterCanWriteContext formatterContext,
        IList<IOutputFormatter> formatters,
        MediaTypeCollection acceptableContentTypes)
    {
        foreach (var formatter in formatters)
        {
            foreach (var contentType in acceptableContentTypes)
            {
                formatterContext.ContentType = new StringSegment(contentType);
                formatterContext.ContentTypeIsServerDefined = true;

                if (formatter.CanWriteResult(formatterContext))
                {
                    return formatter;
                }
            }
        }
        
        return null;
    }                
}

```

#### 2.5 formatter collection

```c#
public class FormatterCollection<TFormatter> : 
	Collection<TFormatter> where TFormatter : notnull
{
   
    public FormatterCollection()
    {
    }
            
    public FormatterCollection(IList<TFormatter> list) : base(list)
    {
    }
        
    public void RemoveType<T>() where T : TFormatter
    {
        RemoveType(typeof(T));
    }
            
    public void RemoveType(Type formatterType)
    {
        for (var i = Count - 1; i >= 0; i--)
        {
            var formatter = this[i];
            if (formatter.GetType() == formatterType)
            {
                RemoveAt(i);
            }
        }
    }
}

```





