## about http client



### 1. about



### 2. http header

#### 2.1 header value

##### 2.1.1 variety of header value

* alt svc header value
* authentication header value
* cache control header value
* content disposition header value
* content range header value
* entity tag header value
* media type header value
* media type with quality header value
* name value header value
* name value with parameters header value
* product header value
* product info header value
* range condition header value
* range header value
* range item header value
* retry condition header value
* string with quality header value
* transfer coding header value
* transfer coding with quality header value
* via header value 
* warning header value
* header string values

##### 2.1.2 header value collection

```c#
public sealed class HttpHeaderValueCollection<T> : ICollection<T> where T : class
{
    private readonly HeaderDescriptor _descriptor;
    private readonly HttpHeaders _store;
    private readonly Action<HttpHeaderValueCollection<T>, T>? _validator;
        
    // special value
    private readonly T? _specialValue;
    internal bool IsSpecialValueSet
    {
        get
        {
            // If this collection instance has a "special value", then check whether that value was already set.
            if (_specialValue == null)
            {
                return false;
            }
            
            return _store.ContainsParsedValue(_descriptor, _specialValue);
        }
    }
    
    // count
    public int Count
    {
        get { return GetCount(); }
    }
    private int GetCount()
    {                
        object? storeValue = _store.GetParsedValues(_descriptor);
        
        if (storeValue == null)
        {
            return 0;
        }
        
        List<object>? storeValues = storeValue as List<object>;
        
        if (storeValues == null)
        {
            return 1;
        }
        else
        {
            return storeValues.Count;
        }
    }
    
    // read only
    public bool IsReadOnly
    {
        get { return false; }
    }
        
    // ctor
    internal HttpHeaderValueCollection(
        HeaderDescriptor descriptor, 
        HttpHeaders store) : 
    		this(descriptor, store, null, null)
    {
    }
    
    internal HttpHeaderValueCollection(
        HeaderDescriptor descriptor, 
        HttpHeaders store,
        Action<HttpHeaderValueCollection<T>, T> validator) : 
    		this(descriptor, store, null, validator)
    {
    }
    
    internal HttpHeaderValueCollection(
        HeaderDescriptor descriptor, 
        HttpHeaders store, 
        T specialValue) : 
    		this(descriptor, store, specialValue, null)
    {
    }
    
    internal HttpHeaderValueCollection(
        HeaderDescriptor descriptor, 
        HttpHeaders store, 
        T? specialValue,
        Action<HttpHeaderValueCollection<T>, T>? validator)
    {
        Debug.Assert(descriptor.Name != null);
        Debug.Assert(store != null);
        
        _store = store;
        _descriptor = descriptor;
        _specialValue = specialValue;
        _validator = validator;
    }
    
    private void CheckValue(T item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }
        
        // If this instance has a custom validator for validating arguments, call it now.
        if (_validator != null)
        {
            _validator(this, item);
        }
    }
    
    // 方法- add
    public void Add(T item)
    {
        CheckValue(item);
        _store.AddParsedValue(_descriptor, item);
    }
    
    public void ParseAdd(string? input)
    {
        _store.Add(_descriptor, input);
    }
    
    public bool TryParseAdd(string? input)
    {
        return _store.TryParseAndAddValue(_descriptor, input);
    }
    
    // 方法- remove 
    public bool Remove(T item)
    {
        CheckValue(item);
        return _store.RemoveParsedValue(_descriptor, item);
    }
    
    public void Clear()
    {
        _store.Remove(_descriptor);
    }
    
    // 方法- contains
    public bool Contains(T item)
    {
        CheckValue(item);
        return _store.ContainsParsedValue(_descriptor, item);
    }
    
    // 方法- copy to
    public void CopyTo(T[] array, int arrayIndex)
    {
        if (array == null)
        {
            throw new ArgumentNullException(nameof(array));
        }
        // Allow arrayIndex == array.Length in case our own collection is empty
        if ((arrayIndex < 0) || (arrayIndex > array.Length))
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }
        
        object? storeValue = _store.GetParsedValues(_descriptor);
        
        if (storeValue == null)
        {
            return;
        }
        
        List<object>? storeValues = storeValue as List<object>;
        
        if (storeValues == null)
        {
            // We only have 1 value: If it is the "special value" just return, otherwise add the value to the array and return.
            Debug.Assert(storeValue is T);
            if (arrayIndex == array.Length)
            {
                throw new ArgumentException(SR.net_http_copyto_array_too_small);
            }
            array[arrayIndex] = (T)storeValue;
        }
        else
        {
            storeValues.CopyTo(array, arrayIndex);
        }
    }
    
    // 方法- set special value
    internal void SetSpecialValue()
    {
        Debug.Assert(
            _specialValue != null,
            "This method can only be used if the collection has a 'special value' set.");
        
        if (!_store.ContainsParsedValue(_descriptor, _specialValue))
        {
            _store.AddParsedValue(_descriptor, _specialValue);
        }
    }
    
    // 方法- remove special value
    internal void RemoveSpecialValue()
    {
        Debug.Assert(
            _specialValue != null,
            "This method can only be used if the collection has a 'special value' set.");
        
        // We're not interested in the return value. It's OK if the "special value" wasn't in the store
        // before calling RemoveParsedValue().
        _store.RemoveParsedValue(_descriptor, _specialValue);
    }
    
    
    // enumerator  
    public IEnumerator<T> GetEnumerator()
    {
        object? storeValue = _store.GetParsedValues(_descriptor);
        
        return storeValue is null 
            ? ((IEnumerable<T>)Array.Empty<T>()).GetEnumerator() 
            : Iterate(storeValue);
        
        static IEnumerator<T> Iterate(object storeValue)
        {
            if (storeValue is List<object> storeValues)
            {
                // We have multiple values. Iterate through the values and return them.
                foreach (object item in storeValues)
                {
                    Debug.Assert(item is T);
                    yield return (T)item;
                }
            }
            else
            {
                Debug.Assert(storeValue is T);
                yield return (T)storeValue;
            }
        }
    }
            
    Collections.IEnumerator Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
                
    public override string ToString()
    {
        return _store.GetHeaderString(_descriptor);
    }                        
}

```

#### 2.2 header parser

```c#
internal abstract class HttpHeaderParser
{
    internal const string DefaultSeparator = ", ";
    
    // support multiple values
    private readonly bool _supportsMultipleValues;        
    public bool SupportsMultipleValues
    {
        get { return _supportsMultipleValues; }
    }
    
    // separator
    private readonly string? _separator;
    public string? Separator
    {
        get
        {
            Debug.Assert(_supportsMultipleValues);
            return _separator;
        }
    }
    
    // comparer
    public virtual IEqualityComparer? Comparer
    {
        get { return null; }
    }
    
    protected HttpHeaderParser(bool supportsMultipleValues)
    {
        _supportsMultipleValues = supportsMultipleValues;
        
        if (supportsMultipleValues)
        {
            _separator = DefaultSeparator;
        }
    }
    
    protected HttpHeaderParser(bool supportsMultipleValues, string separator)
    {
        Debug.Assert(!string.IsNullOrEmpty(separator));
        
        _supportsMultipleValues = supportsMultipleValues;
        _separator = separator;
    }

    // 方法- try parse value，在派生类实现
    // If a parser supports multiple values, a call to ParseValue/TryParseValue should return a value for 'index'
    // pointing to the next non-whitespace character after a delimiter. 
    // E.g. if called with a start index of 0 for string "value , second_value", then after the call completes, 'index' must 
    // point to 's', i.e. the first non-whitespace after the separator ','.
    public abstract bool TryParseValue(
        string? value, 
        object? storeValue, 
        ref int index, 
        [NotNullWhen(true)] out object? parsedValue);
    
    // 方法- parse value (will throw exception if no value found)
    public object ParseValue(string? value, object? storeValue, ref int index)
    {
        // Index may be value.Length (e.g. both 0). 
        // This may be allowed for some headers (e.g. Accept but not allowed by others (e.g. Content-Length). 
        // The parser has to decide if this is valid or not.
        Debug.Assert((value == null) || ((index >= 0) && (index <= value.Length)));
        
        // If a parser returns 'null', it means there was no value, but that's valid (e.g. "Accept: "). 
        // The caller can ignore the value.
        if (!TryParseValue(value, storeValue, ref index, out object? result))
        {
            throw new FormatException(
                SR.Format(
                    System.Globalization.CultureInfo.InvariantCulture, 
                    SR.net_http_headers_invalid_value,
                    value == null ? "<null>" : value.Substring(index)));
        }
    
        return result;
    }
        
    public virtual string? ToString(object value)
    {
        Debug.Assert(value != null);
        
        return value.ToString();
    }
}

```

##### 2.2.1 variety of header parser

* alt svc header parser
* base header parser (abstract)
* byte array header parser
* cache control header parser
* date header parser
* generic header parser
* int32 number header parser
* int64 number header parser
* media type header parser
* production info header parser
* timespan header parser
* uri header parser

#### 2.3 known header

```c#
internal sealed partial class KnownHeader
{
    public string Name { get; }
    public HttpHeaderParser? Parser { get; }
    public HttpHeaderType HeaderType { get; }       
    public string[]? KnownValues { get; }
    public byte[] AsciiBytesWithColonSpace { get; }
    public HeaderDescriptor Descriptor => new HeaderDescriptor(this);
        
    public KnownHeader(
        string name, 
        int? http2StaticTableIndex = null, 
        int? http3StaticTableIndex = null) :    
    		this(
                name, 
                HttpHeaderType.Custom, 
                parser: null, 
                knownValues: null, 
                http2StaticTableIndex, 
                http3StaticTableIndex)
    {
        Debug.Assert(!string.IsNullOrEmpty(name));
        Debug.Assert(name[0] == ':' || 
                     HttpRuleParser.GetTokenLength(name, 0) == name.Length);
    }
    
    public KnownHeader(
        string name, 
        HttpHeaderType headerType, 
        HttpHeaderParser? parser, 
        string[]? knownValues = null, 
        int? http2StaticTableIndex = null, 
        int? http3StaticTableIndex = null)
    {
        Debug.Assert(!string.IsNullOrEmpty(name));
        Debug.Assert(name[0] == ':' || HttpRuleParser.GetTokenLength(name, 0) == name.Length);
        
        // 注入 header name
        Name = name;
        // 注入 header type
        HeaderType = headerType;
        // 注入 header value parser
        Parser = parser;
        // 注入 known values string
        KnownValues = knownValues;
        
        // 初始化 http2、http3 table
        Initialize(http2StaticTableIndex, http3StaticTableIndex);
        
        // 创建 header key 的 ascii bytes 数组
        var asciiBytesWithColonSpace = new byte[name.Length + 2]; 	// + 2 for ':' and ' '
        int asciiBytes = Encoding.ASCII.GetBytes(name, asciiBytesWithColonSpace);
        Debug.Assert(asciiBytes == name.Length);
        asciiBytesWithColonSpace[asciiBytesWithColonSpace.Length - 2] = (byte)':';
        asciiBytesWithColonSpace[asciiBytesWithColonSpace.Length - 1] = (byte)' ';
        AsciiBytesWithColonSpace = asciiBytesWithColonSpace;
    }
    
    partial void Initialize(int? http2StaticTableIndex, int? http3StaticTableIndex);        
}

internal sealed partial class KnownHeader
{
    public byte[] Http2EncodedName { get; private set; }
    public byte[] Http3EncodedName { get; private set; }
    
    [MemberNotNull(nameof(Http2EncodedName))]
    [MemberNotNull(nameof(Http3EncodedName))]
    partial void Initialize(int? http2StaticTableIndex, int? http3StaticTableIndex)
    {
        Http2EncodedName = http2StaticTableIndex.HasValue 
            ? HPackEncoder.EncodeLiteralHeaderFieldWithoutIndexingToAllocatedArray(http2StaticTableIndex.GetValueOrDefault()) 
            : HPackEncoder.EncodeLiteralHeaderFieldWithoutIndexingNewNameToAllocatedArray(Name);
        
        Http3EncodedName = http3StaticTableIndex.HasValue 
            ? QPack.QPackEncoder.EncodeLiteralHeaderFieldWithStaticNameReferenceToArray(http3StaticTableIndex.GetValueOrDefault()) 
            : QPack.QPackEncoder.EncodeLiteralHeaderFieldWithoutNameReferenceToArray(Name);
    }        
}

```

##### 2.3.1 http header type

```c#
[Flags]
internal enum HttpHeaderType : byte
{
    General = 0b1,
    Request = 0b10,
    Response = 0b100,
    Content = 0b1000,
    Custom = 0b10000,
    NonTrailing = 0b100000,
    
    All = 0b111111,
    None = 0
}

```

##### 2.3.2 http header descriptor

```c#
internal readonly struct HeaderDescriptor : IEquatable<HeaderDescriptor>
{       
    private readonly string _headerName;
    public string Name => _headerName;
    
    private readonly KnownHeader? _knownHeader;
    public HttpHeaderParser? Parser => _knownHeader?.Parser;
    public HttpHeaderType HeaderType => _knownHeader == null ? HttpHeaderType.Custom : _knownHeader.HeaderType;
    public KnownHeader? KnownHeader => _knownHeader;
    
    public HeaderDescriptor(KnownHeader knownHeader)
    {
        _knownHeader = knownHeader;
        _headerName = knownHeader.Name;
    }
    
    public HeaderDescriptor AsCustomHeader()
    {
        // know header 不为 null 且 type 不是 customer
        Debug.Assert(_knownHeader != null);
        Debug.Assert(_knownHeader.HeaderType != HttpHeaderType.Custom);

        // 创建 header descriptor（没有 know header）
        return new HeaderDescriptor(_knownHeader.Name);
    }
       
    internal HeaderDescriptor(string headerName)
    {
        _headerName = headerName;
        _knownHeader = null;
    }
    
     // Returns false for invalid header name.
    public static bool TryGet(string headerName, out HeaderDescriptor descriptor)
    {
        Debug.Assert(!string.IsNullOrEmpty(headerName));        
        KnownHeader? knownHeader = KnownHeaders.TryGetKnownHeader(headerName);
        
        if (knownHeader != null)
        {
            descriptor = new HeaderDescriptor(knownHeader);
            return true;
        }
        
        if (!HttpRuleParser.IsToken(headerName))
        {
            descriptor = default(HeaderDescriptor);
            return false;
        }
        
        descriptor = new HeaderDescriptor(headerName);
        return true;
    }
    
    // Returns false for invalid header name.
    public static bool TryGet(ReadOnlySpan<byte> headerName, out HeaderDescriptor descriptor)
    {
        Debug.Assert(headerName.Length > 0);        
        KnownHeader? knownHeader = KnownHeaders.TryGetKnownHeader(headerName);
        
        if (knownHeader != null)
        {
            descriptor = new HeaderDescriptor(knownHeader);
            return true;
        }
        
        if (!HttpRuleParser.IsToken(headerName))
        {
            descriptor = default(HeaderDescriptor);
            return false;
        }
        
        descriptor = new HeaderDescriptor(HttpRuleParser.GetTokenString(headerName));
        return true;
    }
    
    // 方法- get header value (string)
    public string GetHeaderValue(ReadOnlySpan<byte> headerValue, Encoding? valueEncoding)
    {
        if (headerValue.Length == 0)
        {
            return string.Empty;
        }
        
        // If it's a known header value, use the known value instead of allocating a new string.
        // 如果 known header 不为 null，
        if (_knownHeader != null)
        {
            // 解析 known header 的 known values，
            string[]? knownValues = _knownHeader.KnownValues;
            
            // a- known values 不为 null，比对传入的 header value 
            if (knownValues != null)
            {
                for (int i = 0; i < knownValues.Length; i++)
                {
                    if (ByteArrayHelpers.EqualsOrdinalAsciiIgnoreCase(knownValues[i], headerValue))
                    {
                        return knownValues[i];
                    }
                }
            }            
            // b- （known values 为 null），
            // 如果 known header 是 content type，解析 content type string 并返回
            if (_knownHeader == KnownHeaders.ContentType)
            {
                string? contentType = GetKnownContentType(headerValue);
                if (contentType != null)
                {
                    return contentType;
                }
            }
            // c- （known value 为 null，且 know header 不是 content type）
            // 如果 known header 是 location，解码 location utf8 并返回
            else if (_knownHeader == KnownHeaders.Location)
            {
                // Normally Location should be in ISO-8859-1 but occasionally some servers respond with UTF-8.
                if (TryDecodeUtf8(headerValue, out string? decoded))
                {
                    return decoded;
                }
            }
        }
        
        // 不是 known header，或者 known header 不是上述内容，-> encoding 解码并返回
        return (valueEncoding ?? HttpRuleParser.DefaultHttpEncoding).GetString(headerValue);
    }
    
    internal static string? GetKnownContentType(ReadOnlySpan<byte> contentTypeValue)
    {
        string? candidate = null;
        switch (contentTypeValue.Length)
        {
            case 8:
                switch (contentTypeValue[7] | 0x20)
                {
                    case 'l': candidate = "text/xml"; break; // text/xm[l]
                    case 's': candidate = "text/css"; break; // text/cs[s]
                    case 'v': candidate = "text/csv"; break; // text/cs[v]
                }
                break;
                
            case 9:
                switch (contentTypeValue[6] | 0x20)
                {
                    case 'g': candidate = "image/gif"; break; // image/[g]if
                    case 'p': candidate = "image/png"; break; // image/[p]ng
                    case 't': candidate = "text/html"; break; // text/h[t]ml
                }
                break;
                
            case 10:
                switch (contentTypeValue[0] | 0x20)
                {
                    case 't': candidate = "text/plain"; break; // [t]ext/plain
                    case 'i': candidate = "image/jpeg"; break; // [i]mage/jpeg
                }
                break;
                
            case 15:
                switch (contentTypeValue[12] | 0x20)
                {
                    case 'p': candidate = "application/pdf"; break; // application/[p]df
                    case 'x': candidate = "application/xml"; break; // application/[x]ml
                    case 'z': candidate = "application/zip"; break; // application/[z]ip
                }
                break;
                
            case 16:
                switch (contentTypeValue[12] | 0x20)
                {
                    case 'g': candidate = "application/grpc"; break; // application/[g]rpc
                    case 'j': candidate = "application/json"; break; // application/[j]son
                }
                break;
                
            case 19:
                candidate = "multipart/form-data"; // multipart/form-data
                break;
                
            case 22:
                candidate = "application/javascript"; // application/javascript
                break;
                
            case 24:
                switch (contentTypeValue[0] | 0x20)
                {
                    case 'a': candidate = "application/octet-stream"; break; // application/octet-stream
                    case 't': candidate = "text/html; charset=utf-8"; break; // text/html; charset=utf-8
                }
                break;
                
            case 25:
                candidate = "text/plain; charset=utf-8"; // text/plain; charset=utf-8
                break;
                
            case 31:
                candidate = "application/json; charset=utf-8"; // application/json; charset=utf-8
                break;
                
            case 33:
                candidate = "application/x-www-form-urlencoded"; // application/x-www-form-urlencoded
                break;
        }
        
        Debug.Assert(candidate is null || candidate.Length == contentTypeValue.Length);
        
        return candidate != null && 
               ByteArrayHelpers.EqualsOrdinalAsciiIgnoreCase(candidate, contentTypeValue) 
            	   ? candidate 
            	   : null;
    }
    
    private static bool TryDecodeUtf8(ReadOnlySpan<byte> input, [NotNullWhen(true)] out string? decoded)
    {
        char[] rented = ArrayPool<char>.Shared.Rent(input.Length);
        
        try
        {
            if (Utf8.ToUtf16(
                	input, 
                	rented, 
                	out _, 
                	out int charsWritten, 
                	replaceInvalidSequences: false) == OperationStatus.Done)
            {
                decoded = new string(rented, 0, charsWritten);
                return true;
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rented);
        }
        
        decoded = null;
        return false;
    }
    
    /* override equal */
    public bool Equals(HeaderDescriptor other) =>
        _knownHeader == null 
        	? string.Equals(_headerName, other._headerName, StringComparison.OrdinalIgnoreCase) 
        	: _knownHeader == other._knownHeader;
    
    public override int GetHashCode() => 
        _knownHeader?.GetHashCode() ?? StringComparer.OrdinalIgnoreCase.GetHashCode(_headerName);
    
    // Ensure this is never called, to avoid boxing
    public override bool Equals(object? obj) => throw new InvalidOperationException();   

       
    // get qpack header (http3)
    internal static bool TryGetStaticQPackHeader(
        int index, 
        out HeaderDescriptor descriptor, 
        [NotNullWhen(true)] out string? knownValue)
    {
        Debug.Assert(index >= 0);
        
        // Micro-opt: store field to variable to prevent Length re-read and use unsigned to avoid bounds check.
        (HeaderDescriptor descriptor, string value)[] qpackStaticTable = QPackStaticTable.HeaderLookup;
        Debug.Assert(qpackStaticTable.Length == 99);
        
        uint uindex = (uint)index;
        
        if (uindex < (uint)qpackStaticTable.Length)
        {
            (descriptor, knownValue) = qpackStaticTable[uindex];
            return true;
        }
        else
        {
            descriptor = default;
            knownValue = null;
            return false;
        }
    }                
}

```

##### 2.3.3 known headers

```c#
internal static class KnownHeaders
{
    public static readonly KnownHeader PseudoStatus = new KnownHeader(
        ":status", HttpHeaderType.Response, parser: null);
    
    public static readonly KnownHeader Accept = new KnownHeader(
        "Accept", HttpHeaderType.Request, MediaTypeHeaderParser.MultipleValuesParser, null, 
        H2StaticTable.Accept, H3StaticTable.AcceptAny);
    
    public static readonly KnownHeader AcceptCharset = new KnownHeader(
        "Accept-Charset", HttpHeaderType.Request, GenericHeaderParser.MultipleValueStringWithQualityParser, null, 
        H2StaticTable.AcceptCharset);
    
    public static readonly KnownHeader AcceptEncoding = new KnownHeader(
        "Accept-Encoding", HttpHeaderType.Request, GenericHeaderParser.MultipleValueStringWithQualityParser, null, 
        H2StaticTable.AcceptEncoding, H3StaticTable.AcceptEncodingGzipDeflateBr);
    
    public static readonly KnownHeader AcceptLanguage = new KnownHeader(
        "Accept-Language", HttpHeaderType.Request, GenericHeaderParser.MultipleValueStringWithQualityParser, null, 
        H2StaticTable.AcceptLanguage, H3StaticTable.AcceptLanguage);
    
    public static readonly KnownHeader AcceptPatch = new KnownHeader(
        "Accept-Patch");
    
    public static readonly KnownHeader AcceptRanges = new KnownHeader(
        "Accept-Ranges", HttpHeaderType.Response, GenericHeaderParser.TokenListParser, null, 
        H2StaticTable.AcceptRanges, H3StaticTable.AcceptRangesBytes);
    
    public static readonly KnownHeader AccessControlAllowCredentials = new KnownHeader(
        "Access-Control-Allow-Credentials", HttpHeaderType.Response, parser: null, 
        new string[] { "true" }, 
        http3StaticTableIndex: H3StaticTable.AccessControlAllowCredentials);
        
    public static readonly KnownHeader AccessControlAllowHeaders = new KnownHeader(
        "Access-Control-Allow-Headers", HttpHeaderType.Response, parser: null, 
        new string[] { "*" }, 
        http3StaticTableIndex: H3StaticTable.AccessControlAllowHeadersCacheControl);
    
    public static readonly KnownHeader AccessControlAllowMethods = new KnownHeader(
        "Access-Control-Allow-Methods", HttpHeaderType.Response, parser: null, 
        new string[] { "*" }, 
        http3StaticTableIndex: H3StaticTable.AccessControlAllowMethodsGet);
    
    public static readonly KnownHeader AccessControlAllowOrigin = new KnownHeader(
        "Access-Control-Allow-Origin", HttpHeaderType.Response, parser: null, 
        new string[] { "*", "null" }, 
        H2StaticTable.AccessControlAllowOrigin, H3StaticTable.AccessControlAllowOriginAny);
    
    public static readonly KnownHeader AccessControlExposeHeaders = new KnownHeader(
        "Access-Control-Expose-Headers", HttpHeaderType.Response, parser: null, 
        new string[] { "*" }, 
        H3StaticTable.AccessControlExposeHeadersContentLength);
    
    public static readonly KnownHeader AccessControlMaxAge = new KnownHeader(
        "Access-Control-Max-Age");
    
    public static readonly KnownHeader Age = new KnownHeader(
        "Age", HttpHeaderType.Response | HttpHeaderType.NonTrailing, TimeSpanHeaderParser.Parser, null,
        H2StaticTable.Age, H3StaticTable.Age0);
    
    public static readonly KnownHeader Allow = new KnownHeader(
        "Allow", HttpHeaderType.Content, GenericHeaderParser.TokenListParser, null, 
        H2StaticTable.Allow);
    
    public static readonly KnownHeader AltSvc = new KnownHeader(
        "Alt-Svc", HttpHeaderType.Response, GetAltSvcHeaderParser(), 
        http3StaticTableIndex: H3StaticTable.AltSvcClear);
    
    public static readonly KnownHeader AltUsed = new KnownHeader(
        "Alt-Used", HttpHeaderType.Request, parser: null);
    
    public static readonly KnownHeader Authorization = new KnownHeader(
        "Authorization", HttpHeaderType.Request | HttpHeaderType.NonTrailing, GenericHeaderParser.SingleValueAuthenticationParser, 
        null, 
        H2StaticTable.Authorization, H3StaticTable.Authorization);
    
    public static readonly KnownHeader CacheControl = new KnownHeader(
        "Cache-Control", HttpHeaderType.General | HttpHeaderType.NonTrailing, CacheControlHeaderParser.Parser, 
        new string[] { "must-revalidate", "no-cache", "no-store", "no-transform", "private", "proxy-revalidate", "public" }, 
        H2StaticTable.CacheControl, H3StaticTable.CacheControlMaxAge0);
    
    public static readonly KnownHeader Connection = new KnownHeader(
        "Connection", HttpHeaderType.General, GenericHeaderParser.TokenListParser, 
        new string[] { "close" });
    
    public static readonly KnownHeader ContentDisposition = new KnownHeader(
        "Content-Disposition", HttpHeaderType.Content | HttpHeaderType.NonTrailing, GenericHeaderParser.ContentDispositionParser, 
        new string[] { "inline", "attachment" }, 
        H2StaticTable.ContentDisposition, H3StaticTable.ContentDisposition);
    
    public static readonly KnownHeader ContentEncoding = new KnownHeader(
        "Content-Encoding", HttpHeaderType.Content | HttpHeaderType.NonTrailing, GenericHeaderParser.TokenListParser, 
        new string[] { "gzip", "deflate", "br", "compress", "identity" }, 
        H2StaticTable.ContentEncoding, H3StaticTable.ContentEncodingBr);
    
    public static readonly KnownHeader ContentLanguage = new KnownHeader(
        "Content-Language", HttpHeaderType.Content, GenericHeaderParser.TokenListParser, 
        null, 
        H2StaticTable.ContentLanguage);
    
    public static readonly KnownHeader ContentLength = new KnownHeader(
        "Content-Length", HttpHeaderType.Content | HttpHeaderType.NonTrailing, Int64NumberHeaderParser.Parser, 
        null, 
        H2StaticTable.ContentLength, H3StaticTable.ContentLength0);
    
    public static readonly KnownHeader ContentLocation = new KnownHeader(
        "Content-Location", HttpHeaderType.Content | HttpHeaderType.NonTrailing, UriHeaderParser.RelativeOrAbsoluteUriParser, 
        null, 
        H2StaticTable.ContentLocation);
    
    public static readonly KnownHeader ContentMD5 = new KnownHeader(
        "Content-MD5", HttpHeaderType.Content, ByteArrayHeaderParser.Parser);
    
    public static readonly KnownHeader ContentRange = new KnownHeader(
        "Content-Range", HttpHeaderType.Content | HttpHeaderType.NonTrailing, GenericHeaderParser.ContentRangeParser, 
        null, 
        H2StaticTable.ContentRange);
    
    public static readonly KnownHeader ContentSecurityPolicy = new KnownHeader(
        "Content-Security-Policy", 
        http3StaticTableIndex: H3StaticTable.ContentSecurityPolicyAllNone);
    
    public static readonly KnownHeader ContentType = new KnownHeader(
        "Content-Type", HttpHeaderType.Content | HttpHeaderType.NonTrailing, MediaTypeHeaderParser.SingleValueParser, 
        null, 
        H2StaticTable.ContentType, H3StaticTable.ContentTypeApplicationDnsMessage);
    
    public static readonly KnownHeader Cookie = new KnownHeader(
        "Cookie", H2StaticTable.Cookie, H3StaticTable.Cookie);
    
    public static readonly KnownHeader Cookie2 = new KnownHeader(
        "Cookie2");
    
    public static readonly KnownHeader Date = new KnownHeader(
        "Date", HttpHeaderType.General | HttpHeaderType.NonTrailing, DateHeaderParser.Parser, 
        null, 
        H2StaticTable.Date, H3StaticTable.Date);
    
    public static readonly KnownHeader ETag = new KnownHeader(
        "ETag", HttpHeaderType.Response, GenericHeaderParser.SingleValueEntityTagParser, 
        null, 
        H2StaticTable.ETag, H3StaticTable.ETag);
    
    public static readonly KnownHeader Expect = new KnownHeader(
        "Expect", HttpHeaderType.Request | HttpHeaderType.NonTrailing, GenericHeaderParser.MultipleValueNameValueWithParametersParser, 
        new string[] { "100-continue" }, 
        H2StaticTable.Expect);
    
    public static readonly KnownHeader ExpectCT = new KnownHeader(
        "Expect-CT");
    
    public static readonly KnownHeader Expires = new KnownHeader(
        "Expires", HttpHeaderType.Content | HttpHeaderType.NonTrailing, DateHeaderParser.Parser, 
        null, 
        H2StaticTable.Expires);
    
    public static readonly KnownHeader From = new KnownHeader(
        "From", HttpHeaderType.Request, GenericHeaderParser.SingleValueParserWithoutValidation, 
        null, 
        H2StaticTable.From);
    
    public static readonly KnownHeader GrpcEncoding = new KnownHeader(
        "grpc-encoding", HttpHeaderType.Custom, null, 
        new string[] { "identity", "gzip", "deflate" });
    
    public static readonly KnownHeader GrpcMessage = new KnownHeader(
        "grpc-message");
    
    public static readonly KnownHeader GrpcStatus = new KnownHeader(
        "grpc-status", HttpHeaderType.Custom, null, 
        new string[] { "0" });
    
    public static readonly KnownHeader Host = new KnownHeader(
        "Host", HttpHeaderType.Request | HttpHeaderType.NonTrailing, GenericHeaderParser.HostParser, 
        null, 
        H2StaticTable.Host);
    
    public static readonly KnownHeader IfMatch = new KnownHeader(
        "If-Match", HttpHeaderType.Request | HttpHeaderType.NonTrailing, GenericHeaderParser.MultipleValueEntityTagParser, 
        null, 
        H2StaticTable.IfMatch);
    
    public static readonly KnownHeader IfModifiedSince = new KnownHeader(
        "If-Modified-Since", HttpHeaderType.Request | HttpHeaderType.NonTrailing, DateHeaderParser.Parser, 
        null, 
        H2StaticTable.IfModifiedSince, H3StaticTable.IfModifiedSince);
    
    public static readonly KnownHeader IfNoneMatch = new KnownHeader(
        "If-None-Match", HttpHeaderType.Request | HttpHeaderType.NonTrailing, GenericHeaderParser.MultipleValueEntityTagParser, 
        null, 
        H2StaticTable.IfNoneMatch, H3StaticTable.IfNoneMatch);
    
    public static readonly KnownHeader IfRange = new KnownHeader(
        "If-Range", HttpHeaderType.Request | HttpHeaderType.NonTrailing, GenericHeaderParser.RangeConditionParser, 
        null, 
        H2StaticTable.IfRange, H3StaticTable.IfRange);
    
    public static readonly KnownHeader IfUnmodifiedSince = new KnownHeader(
        "If-Unmodified-Since", HttpHeaderType.Request | HttpHeaderType.NonTrailing, DateHeaderParser.Parser, 
        null, 
        H2StaticTable.IfUnmodifiedSince);
    
    public static readonly KnownHeader KeepAlive = new KnownHeader(
        "Keep-Alive");
    
    public static readonly KnownHeader LastModified = new KnownHeader(
        "Last-Modified", HttpHeaderType.Content, DateHeaderParser.Parser, 
        null, 
        H2StaticTable.LastModified, H3StaticTable.LastModified);
    
    public static readonly KnownHeader Link = new KnownHeader(
        "Link", H2StaticTable.Link, H3StaticTable.Link);
    
    public static readonly KnownHeader Location = new KnownHeader(
        "Location", HttpHeaderType.Response | HttpHeaderType.NonTrailing, UriHeaderParser.RelativeOrAbsoluteUriParser, 
        null, 
        H2StaticTable.Location, H3StaticTable.Location);
    
    public static readonly KnownHeader MaxForwards = new KnownHeader(
        "Max-Forwards", HttpHeaderType.Request | HttpHeaderType.NonTrailing, Int32NumberHeaderParser.Parser, 
        null, 
        H2StaticTable.MaxForwards);
    
    public static readonly KnownHeader Origin = new KnownHeader(
        "Origin", http3StaticTableIndex: H3StaticTable.Origin);
    
    public static readonly KnownHeader P3P = new KnownHeader(
        "P3P");
    
    public static readonly KnownHeader Pragma = new KnownHeader(
        "Pragma", HttpHeaderType.General | HttpHeaderType.NonTrailing, GenericHeaderParser.MultipleValueNameValueParser, 
        new string[] { "no-cache" });
    
    public static readonly KnownHeader ProxyAuthenticate = new KnownHeader(
        "Proxy-Authenticate", HttpHeaderType.Response | HttpHeaderType.NonTrailing, GenericHeaderParser.MultipleValueAuthenticationParser, 
        null, 
        H2StaticTable.ProxyAuthenticate);
    
    public static readonly KnownHeader ProxyAuthorization = new KnownHeader(
        "Proxy-Authorization", HttpHeaderType.Request | HttpHeaderType.NonTrailing, GenericHeaderParser.SingleValueAuthenticationParser, 
        null, 
        H2StaticTable.ProxyAuthorization);
    
    public static readonly KnownHeader ProxyConnection = new KnownHeader(
        "Proxy-Connection");
    
    public static readonly KnownHeader ProxySupport = new KnownHeader(
        "Proxy-Support");
    
    public static readonly KnownHeader PublicKeyPins = new KnownHeader(
        "Public-Key-Pins");
    
    public static readonly KnownHeader Range = new KnownHeader(
        "Range", HttpHeaderType.Request | HttpHeaderType.NonTrailing, GenericHeaderParser.RangeParser, 
        null, 
        H2StaticTable.Range, H3StaticTable.RangeBytes0ToAll);
    
    public static readonly KnownHeader Referer = new KnownHeader(
        "Referer", HttpHeaderType.Request, UriHeaderParser.RelativeOrAbsoluteUriParser, 
        null, 
        H2StaticTable.Referer, H3StaticTable.Referer); 
    
    public static readonly KnownHeader ReferrerPolicy = new KnownHeader(
        "Referrer-Policy", HttpHeaderType.Custom, null, 
        new string[] { "strict-origin-when-cross-origin", "origin-when-cross-origin", "strict-origin", "origin", "same-origin", "no-referrer-when-downgrade", "no-referrer", "unsafe-url" });
    
    public static readonly KnownHeader Refresh = new KnownHeader(
        "Refresh", H2StaticTable.Refresh);
    
    public static readonly KnownHeader RetryAfter = new KnownHeader(
        "Retry-After", HttpHeaderType.Response | HttpHeaderType.NonTrailing, GenericHeaderParser.RetryConditionParser, 
        null, 
        H2StaticTable.RetryAfter);
    
    public static readonly KnownHeader SecWebSocketAccept = new KnownHeader(
        "Sec-WebSocket-Accept");
    
    public static readonly KnownHeader SecWebSocketExtensions = new KnownHeader(
        "Sec-WebSocket-Extensions");
    
    public static readonly KnownHeader SecWebSocketKey = new KnownHeader(
        "Sec-WebSocket-Key");
    
    public static readonly KnownHeader SecWebSocketProtocol = new KnownHeader(
        "Sec-WebSocket-Protocol");    
    
    public static readonly KnownHeader SecWebSocketVersion = new KnownHeader(
        "Sec-WebSocket-Version");
    
    public static readonly KnownHeader Server = new KnownHeader(
        "Server", HttpHeaderType.Response, ProductInfoHeaderParser.MultipleValueParser, 
        null, 
        H2StaticTable.Server, H3StaticTable.Server);
    
    public static readonly KnownHeader ServerTiming = new KnownHeader(
        "Server-Timing");
    
    public static readonly KnownHeader SetCookie = new KnownHeader(
        "Set-Cookie", HttpHeaderType.Custom | HttpHeaderType.NonTrailing, null, 
        null, 
        H2StaticTable.SetCookie, H3StaticTable.SetCookie);
    
    public static readonly KnownHeader SetCookie2 = new KnownHeader(
        "Set-Cookie2", HttpHeaderType.Custom | HttpHeaderType.NonTrailing, null, 
        null);
    
    public static readonly KnownHeader StrictTransportSecurity = new KnownHeader(
        "Strict-Transport-Security", 
        H2StaticTable.StrictTransportSecurity, H3StaticTable.StrictTransportSecurityMaxAge31536000);
    
    public static readonly KnownHeader TE = new KnownHeader(
        "TE", HttpHeaderType.Request | HttpHeaderType.NonTrailing, TransferCodingHeaderParser.MultipleValueWithQualityParser, 
        new string[] { "trailers", "compress", "deflate", "gzip" });
    
    public static readonly KnownHeader TSV = new KnownHeader(
        "TSV");
    
    public static readonly KnownHeader Trailer = new KnownHeader(
        "Trailer", HttpHeaderType.General | HttpHeaderType.NonTrailing, GenericHeaderParser.TokenListParser);
    
    public static readonly KnownHeader TransferEncoding = new KnownHeader(
        "Transfer-Encoding", HttpHeaderType.General | HttpHeaderType.NonTrailing, TransferCodingHeaderParser.MultipleValueParser, 
        new string[] { "chunked", "compress", "deflate", "gzip", "identity" }, 
        H2StaticTable.TransferEncoding);
    
    public static readonly KnownHeader Upgrade = new KnownHeader(
        "Upgrade", HttpHeaderType.General, GenericHeaderParser.MultipleValueProductParser);
    
    public static readonly KnownHeader UpgradeInsecureRequests = new KnownHeader(
        "Upgrade-Insecure-Requests", HttpHeaderType.Custom, null, 
        new string[] { "1" }, 
        http3StaticTableIndex: H3StaticTable.UpgradeInsecureRequests1);
    
    public static readonly KnownHeader UserAgent = new KnownHeader(
        "User-Agent", HttpHeaderType.Request, ProductInfoHeaderParser.MultipleValueParser, 
        null, 
        H2StaticTable.UserAgent, H3StaticTable.UserAgent);
    
    public static readonly KnownHeader Vary = new KnownHeader(
        "Vary", HttpHeaderType.Response | HttpHeaderType.NonTrailing, GenericHeaderParser.TokenListParser, 
        new string[] { "*" }, 
        H2StaticTable.Vary, H3StaticTable.VaryAcceptEncoding);
    
    public static readonly KnownHeader Via = new KnownHeader(
        "Via", HttpHeaderType.General, GenericHeaderParser.MultipleValueViaParser, 
        null, 
        H2StaticTable.Via);
    
    public static readonly KnownHeader WWWAuthenticate = new KnownHeader(
        "WWW-Authenticate", HttpHeaderType.Response | HttpHeaderType.NonTrailing, GenericHeaderParser.MultipleValueAuthenticationParser, 
        null, H2StaticTable.WwwAuthenticate);
    
    public static readonly KnownHeader Warning = new KnownHeader(
        "Warning", HttpHeaderType.General | HttpHeaderType.NonTrailing, GenericHeaderParser.MultipleValueWarningParser);
    
    public static readonly KnownHeader XAspNetVersion = new KnownHeader(
        "X-AspNet-Version");
    
    public static readonly KnownHeader XCache = new KnownHeader(
        "X-Cache");
    
    public static readonly KnownHeader XContentDuration = new KnownHeader(
        "X-Content-Duration");
    
    public static readonly KnownHeader XContentTypeOptions = new KnownHeader(
        "X-Content-Type-Options", HttpHeaderType.Custom, null, new string[] { "nosniff" }, http3StaticTableIndex: H3StaticTable.XContentTypeOptionsNoSniff);
    
    public static readonly KnownHeader XFrameOptions = new KnownHeader(
        "X-Frame-Options", HttpHeaderType.Custom, null, new string[] { "DENY", "SAMEORIGIN" }, http3StaticTableIndex: H3StaticTable.XFrameOptionsDeny);
    
    public static readonly KnownHeader XMSEdgeRef = new KnownHeader(
        "X-MSEdge-Ref");
    
    public static readonly KnownHeader XPoweredBy = new KnownHeader(
        "X-Powered-By");
    
    public static readonly KnownHeader XRequestID = new KnownHeader(
        "X-Request-ID");
    
    public static readonly KnownHeader XUACompatible = new KnownHeader(
        "X-UA-Compatible");
    
    public static readonly KnownHeader XXssProtection = new KnownHeader(
        "X-XSS-Protection", HttpHeaderType.Custom, null, new string[] { "0", "1", "1; mode=block" });

    private static HttpHeaderParser? GetAltSvcHeaderParser() =>
#if TARGET_BROWSER
        // Allow for the AltSvcHeaderParser to be trimmed on Browser since Alt-Svc is only for SocketsHttpHandler, 
        // which isn't used on Browser.
        null;
#else
        AltSvcHeaderParser.Parser;
#endif
            
}

```

###### 2.3.3.1 get candidate

```c#
internal static class KnownHeaders
{
    // Helper interface for making GetCandidate generic over strings, utf8, etc
    private interface IHeaderNameAccessor
	{
    	int Length { get; }
    	char this[int index] { get; }
	}
                        
    // Matching is case-insensitive. 
    private static KnownHeader? GetCandidate<T>(T key) where T : struct, IHeaderNameAccessor     
    {
        // Lookup is performed by first switching on the header name's length, 
        // and then switching on the most unique position in that length's string.        
        int length = key.Length;
        
        switch (length)
        {
            case 2:
                return TE; // TE
                
            case 3:
                switch (key[0] | 0x20)
                {
                    case 'a': return Age; // [A]ge
                    case 'p': return P3P; // [P]3P
                    case 't': return TSV; // [T]SV
                    case 'v': return Via; // [V]ia
                }
                break;
                
            case 4:
                switch (key[0] | 0x20)
                {
                    case 'd': return Date; // [D]ate
                    case 'e': return ETag; // [E]Tag
                    case 'f': return From; // [F]rom
                    case 'h': return Host; // [H]ost
                    case 'l': return Link; // [L]ink
                    case 'v': return Vary; // [V]ary
                }
                break;
                
            case 5:                
                switch (key[0] | 0x20)
                {
                    case 'a': return Allow; // [A]llow
                    case 'r': return Range; // [R]ange
                }
                break;
                
            case 6:
                switch (key[0] | 0x20)
                {
                    case 'a': return Accept; // [A]ccept
                    case 'c': return Cookie; // [C]ookie
                    case 'e': return Expect; // [E]xpect
                    case 'o': return Origin; // [O]rigin
                    case 'p': return Pragma; // [P]ragma
                    case 's': return Server; // [S]erver
                }
                break;
                
            case 7:                
                switch (key[0] | 0x20)
                {
                    case ':': return PseudoStatus; // [:]status
                    case 'a': return AltSvc;  // [A]lt-Svc
                    case 'c': return Cookie2; // [C]ookie2
                    case 'e': return Expires; // [E]xpires
                    case 'r':
                        switch (key[3] | 0x20)
                        {
                            case 'e': return Referer; // [R]ef[e]rer
                            case 'r': return Refresh; // [R]ef[r]esh
                        }
                        break;
                    case 't': return Trailer; // [T]railer
                    case 'u': return Upgrade; // [U]pgrade
                    case 'w': return Warning; // [W]arning
                    case 'x': return XCache;  // [X]-Cache
                }
                break;
                
            case 8:
                switch (key[3] | 0x20)
                {
                    case '-': return AltUsed;  // Alt[-]Used
                    case 'a': return Location; // Loc[a]tion
                    case 'm': return IfMatch;  // If-[M]atch
                    case 'r': return IfRange;  // If-[R]ange
                }
                break;
                
            case 9:
                return ExpectCT; // Expect-CT
                
            case 10:
                switch (key[0] | 0x20)
                {
                    case 'c': return Connection; // [C]onnection
                    case 'k': return KeepAlive;  // [K]eep-Alive
                    case 's': return SetCookie;  // [S]et-Cookie
                    case 'u': return UserAgent;  // [U]ser-Agent
                }
                break;
                
            case 11:
                switch (key[0] | 0x20)
                {
                    case 'c': return ContentMD5; // [C]ontent-MD5
                    case 'g': return GrpcStatus; // [g]rpc-status
                    case 'r': return RetryAfter; // [R]etry-After
                    case 's': return SetCookie2; // [S]et-Cookie2
                }
                break;
                
            case 12:
                switch (key[5] | 0x20)
                {
                    case 'd': return XMSEdgeRef;  // X-MSE[d]ge-Ref
                    case 'e': return XPoweredBy;  // X-Pow[e]red-By
                    case 'm': return GrpcMessage; // grpc-[m]essage
                    case 'n': return ContentType; // Conte[n]t-Type
                    case 'o': return MaxForwards; // Max-F[o]rwards
                    case 't': return AcceptPatch; // Accep[t]-Patch
                    case 'u': return XRequestID;  // X-Req[u]est-ID
                }
                break;
                
            case 13:
                switch (key[12] | 0x20)
                {
                    case 'd': return LastModified;  // Last-Modifie[d]
                    case 'e': return ContentRange;  // Content-Rang[e]
                    case 'g':
                        switch (key[0] | 0x20)
                        {
                            case 's': return ServerTiming;  // [S]erver-Timin[g]
                            case 'g': return GrpcEncoding;  // [g]rpc-encodin[g]
                        }
                        break;
                    case 'h': return IfNoneMatch;   // If-None-Matc[h]
                    case 'l': return CacheControl;  // Cache-Contro[l]
                    case 'n': return Authorization; // Authorizatio[n]
                    case 's': return AcceptRanges;  // Accept-Range[s]                        
                    case 't': return ProxySupport;  // Proxy-Suppor[t]
                }
                break;
                
            case 14:
                switch (key[0] | 0x20)
                {
                    case 'a': return AcceptCharset; // [A]ccept-Charset
                    case 'c': return ContentLength; // [C]ontent-Length
                }
                break;
                
            case 15:
                switch (key[7] | 0x20)
                {
                    case '-': return XFrameOptions;  // X-Frame[-]Options
                    case 'e': return AcceptEncoding; // Accept-[E]ncoding
                    case 'k': return PublicKeyPins;  // Public-[K]ey-Pins
                    case 'l': return AcceptLanguage; // Accept-[L]anguage
                    case 'm': return XUACompatible;  // X-UA-Co[m]patible
                    case 'r': return ReferrerPolicy; // Referre[r]-Policy
                }
                break;
                
            case 16:
                switch (key[11] | 0x20)
                {
                    case 'a': return ContentLocation; // Content-Loc[a]tion
                    case 'c':
                        switch (key[0] | 0x20)
                        {
                            case 'p': return ProxyConnection; // [P]roxy-Conne[c]tion
                            case 'x': return XXssProtection;  // [X]-XSS-Prote[c]tion
                        }
                        break;
                    case 'g': return ContentLanguage; // Content-Lan[g]uage
                    case 'i': return WWWAuthenticate; // WWW-Authent[i]cate
                    case 'o': return ContentEncoding; // Content-Enc[o]ding
                    case 'r': return XAspNetVersion;  // X-AspNet-Ve[r]sion
                }
                break;
                
            case 17:
                switch (key[0] | 0x20)
                {
                    case 'i': return IfModifiedSince;  // [I]f-Modified-Since
                    case 's': return SecWebSocketKey;  // [S]ec-WebSocket-Key
                    case 't': return TransferEncoding; // [T]ransfer-Encoding
                }
                break;
                
            case 18:
                switch (key[0] | 0x20)
                {
                    case 'p': return ProxyAuthenticate; // [P]roxy-Authenticate
                    case 'x': return XContentDuration;  // [X]-Content-Duration
                }
                break;
                
            case 19:
                switch (key[0] | 0x20)
                {
                    case 'c': return ContentDisposition; // [C]ontent-Disposition
                    case 'i': return IfUnmodifiedSince;  // [I]f-Unmodified-Since
                    case 'p': return ProxyAuthorization; // [P]roxy-Authorization
                }
                break;
                
            case 20:
                return SecWebSocketAccept; // Sec-WebSocket-Accept
                
            case 21:
                return SecWebSocketVersion; // Sec-WebSocket-Version
                
            case 22:
                switch (key[0] | 0x20)
                {
                    case 'a': return AccessControlMaxAge;  // [A]ccess-Control-Max-Age
                    case 's': return SecWebSocketProtocol; // [S]ec-WebSocket-Protocol
                    case 'x': return XContentTypeOptions;  // [X]-Content-Type-Options
                }
                break;
                
            case 23:
                return ContentSecurityPolicy; // Content-Security-Policy
                
            case 24:
                return SecWebSocketExtensions; // Sec-WebSocket-Extensions
                
            case 25:
                switch (key[0] | 0x20)
                {
                    case 's': return StrictTransportSecurity; // [S]trict-Transport-Security
                    case 'u': return UpgradeInsecureRequests; // [U]pgrade-Insecure-Requests
                }
                break;
                
            case 27:
                return AccessControlAllowOrigin; // Access-Control-Allow-Origin
                
            case 28:
                switch (key[21] | 0x20)
                {
                    case 'h': return AccessControlAllowHeaders; // Access-Control-Allow-[H]eaders
                    case 'm': return AccessControlAllowMethods; // Access-Control-Allow-[M]ethods
                }
                break;
                
            case 29:
                return AccessControlExposeHeaders; // Access-Control-Expose-Headers
                
            case 32:
                return AccessControlAllowCredentials; // Access-Control-Allow-Credentials
        }
        
        return null;
    }
}

```

###### 2.3.3.2 方法- get known header by string

```c#
internal static class KnownHeaders
{
    internal static KnownHeader? TryGetKnownHeader(string name)
    {
        KnownHeader? candidate = GetCandidate(new StringAccessor(name));
        if (candidate != null && 
            StringComparer.OrdinalIgnoreCase.Equals(name, candidate.Name))
        {
            return candidate;
        }
        
        return null;
    }
    
    private readonly struct StringAccessor : IHeaderNameAccessor
    {
        private readonly string _string;        
        public int Length => _string.Length;
        public char this[int index] => _string[index];
        
        public StringAccessor(string s)
        {
            _string = s;
        }                
    }
}

```

###### 2.3.3.3 方法- get known header by byte

```c#
internal static class KnownHeaders
{
    internal static unsafe KnownHeader? TryGetKnownHeader(ReadOnlySpan<byte> name)
    {
        fixed (byte* p = &MemoryMarshal.GetReference(name))
        {
            KnownHeader? candidate = GetCandidate(new BytePtrAccessor(p, name.Length));
            if (candidate != null && 
                ByteArrayHelpers.EqualsOrdinalAsciiIgnoreCase(candidate.Name, name))
            {
                return candidate;
            }
        }
        
        return null;
    }
    
    // Can't use Span here as it's unsupported.
    private readonly unsafe struct BytePtrAccessor : IHeaderNameAccessor
    {
        private readonly byte* _p;
        private readonly int _length;
        
        public int Length => _length;
        public char this[int index] => (char)_p[index];
        
        public BytePtrAccessor(byte* p, int length)
        {
            _p = p;
            _length = length;
        }                
    }
}

```

#### 2.4 http header abstract

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{
    // This type is used to store a collection of headers in 'headerStore':
    // - A header can have multiple values.
    // - A header can have an associated parser which is able to parse the raw string value into a strongly typed object.
    // - If a header has an associated parser and the provided raw value can't be parsed, the value is considered
    //   invalid. Invalid values are stored if added using TryAddWithoutValidation(). If the value was added using Add(),
    //   Add() will throw FormatException.
    // - Since parsing header values is expensive and users usually only care about a few headers, header values are
    //   lazily initialized.
    //
    // Given the properties above, a header value can have three states:
    // - 'raw': The header value was added using TryAddWithoutValidation() and it wasn't parsed yet.
    // - 'parsed': The header value was successfully parsed. It was either added using Add() where the value was parsed
    //   immediately, or if added using TryAddWithoutValidation() a user already accessed a property/method triggering the
    //   value to be parsed.
    // - 'invalid': The header value was parsed, but parsing failed because the value is invalid. Storing invalid values
    //   allows users to still retrieve the value (by calling GetValues()), but it will not be exposed as strongly typed
    //   object. E.g. the client receives a response with the following header: 'Via: 1.1 proxy, invalid'
    //   - HttpHeaders.GetValues() will return "1.1 proxy", "invalid"
    //   - HttpResponseHeaders.Via collection will only contain one ViaHeaderValue object with value "1.1 proxy"
        
    // header store
    private Dictionary<HeaderDescriptor, object>? _headerStore;
    internal Dictionary<HeaderDescriptor, object>? HeaderStore => _headerStore;
        
    // allowed type
    private readonly HttpHeaderType _allowedHeaderTypes;
    
    // custom header type
    private readonly HttpHeaderType _treatAsCustomHeaderTypes;
    
    // non validated headers
    public HttpHeadersNonValidated NonValidated => new HttpHeadersNonValidated(this);
    
    // ctor
    protected HttpHeaders() : this(HttpHeaderType.All, HttpHeaderType.None)
    {
    }
    
    internal HttpHeaders(HttpHeaderType allowedHeaderTypes, HttpHeaderType treatAsCustomHeaderTypes)
    {
        // Should be no overlap
        Debug.Assert((allowedHeaderTypes & treatAsCustomHeaderTypes) == 0);
        
        _allowedHeaderTypes = allowedHeaderTypes & ~HttpHeaderType.NonTrailing;
        _treatAsCustomHeaderTypes = treatAsCustomHeaderTypes & ~HttpHeaderType.NonTrailing;
    }
        
    // enumerator                        
    public IEnumerator<KeyValuePair<string, IEnumerable<string>>> GetEnumerator() => 
        _headerStore != null && _headerStore.Count > 0 
        	? GetEnumeratorCore() 
        	: ((IEnumerable<KeyValuePair<string, IEnumerable<string>>>)
               		Array.Empty<KeyValuePair<string, IEnumerable<string>>>()).GetEnumerator();
    
    private IEnumerator<KeyValuePair<string, IEnumerable<string>>> GetEnumeratorCore()
    {
        foreach (KeyValuePair<HeaderDescriptor, object> header in _headerStore!)
        {
            HeaderDescriptor descriptor = header.Key;
            object value = header.Value;
            
            HeaderStoreItemInfo? info = value as HeaderStoreItemInfo;
            if (info is null)
            {
                // To retain consistent semantics, we need to upgrade a raw string to a HeaderStoreItemInfo during enumeration 
                // so that we can parse the raw value in order to a) return the correct set of parsed values, and b) update 
                // the instance for subsequent enumerations to reflect that parsing.
                _headerStore[descriptor] = info = new HeaderStoreItemInfo() { RawValue = value };
            }
            
            // Make sure we parse all raw values before returning the result. Note that this has to be done before we calculate 
            // the array length (next line): A raw value may contain a list of values.
            if (!ParseRawHeaderValues(descriptor, info, removeEmptyHeader: false))
            {
                // We have an invalid header value (contains invalid newline chars). Delete it.
                _headerStore.Remove(descriptor);
            }
            else
            {
                string[] values = GetStoreValuesAsStringArray(descriptor, info);
                yield return new KeyValuePair<string, IEnumerable<string>>(descriptor.Name, values);
            }
        }
    }
        
    Collections.IEnumerator Collections.IEnumerable.GetEnumerator() => GetEnumerator();
                            
                                                                                            
    private bool AreEqual(object value, object? storeValue, IEqualityComparer? comparer)
    {
        Debug.Assert(value != null);
        
        if (comparer != null)
        {
            return comparer.Equals(value, storeValue);
        }
        
        // We don't have a comparer, so use the Equals() method.
        return value.Equals(storeValue);
    }        
}

```

##### 2.4.1 header store item info

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{    
    internal sealed class HeaderStoreItemInfo
    {
        internal object? RawValue;
        internal object? InvalidValue;
        internal object? ParsedValue;
        
        internal HeaderStoreItemInfo() 
        {
        }
        
        // 方法- can add 
        internal bool CanAddParsedValue(HttpHeaderParser parser)
        {
            Debug.Assert(
                parser != null, 
                "There should be no reason to call CanAddValue if there is no parser for the current header.");
            
            // If the header only supports one value, and we have already a value set, then we can't add another value. 
            // (E.g. the 'Date' header only supports one value. We can't add multiple timestamps to 'Date'.)            
            return parser.SupportsMultipleValues || 
                ((InvalidValue == null) && (ParsedValue == null));
        }
        
        // 方法- is empty
        internal bool IsEmpty => (RawValue == null) && (InvalidValue == null) && (ParsedValue == null);
    }
}

```

###### 2.4.1.1 add value to header info

###### - add xxx value

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{ 
    // 向 header info 添加 value 用于存储（工具方法）
    private static void AddValueToStoreValue<T>(    	
        // value
        T value, 
        // container
        ref object? currentStoreValue) where T : class
    {            
            if (currentStoreValue == null)
            {
                currentStoreValue = value;
            }
            else
            {
                List<T>? storeValues = currentStoreValue as List<T>;

                if (storeValues == null)
                {
                    storeValues = new List<T>(2);
                    Debug.Assert(currentStoreValue is T);
                    storeValues.Add((T)currentStoreValue);
                    currentStoreValue = storeValues;
                }
                Debug.Assert(value is T);
                storeValues.Add((T)value);
            }
    }
    
    // add raw value（将 value 注入 header info 的 raw info）
    private static void AddRawValue(HeaderStoreItemInfo info, string value)
    {
        AddValueToStoreValue<string>(value, ref info.RawValue);
    }
    
    // add invalid value（将 value 注入 header info 的 invalid value）
    private static void AddInvalidValue(HeaderStoreItemInfo info, string value)
    {
        AddValueToStoreValue<string>(value, ref info.InvalidValue);
    }        
    
    // add parsed value（将 value 注入 header info 的 parsed info）
    private static void AddParsedValue(HeaderStoreItemInfo info, object value)
    {
        Debug.Assert(
            !(value is List<object>),
            "Header value types must not derive from List<object> since this type is used internally to store " +
            "lists of values. So we would not be able to distinguish between a single value and a list of values.");
        
        AddValueToStoreValue<object>(value, ref info.ParsedValue);
    }
    
    
}

```

###### - add header info (value copied)

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{
    private void AddHeaderInfo(
        HeaderDescriptor descriptor, 
        HeaderStoreItemInfo sourceInfo)
    {
        // 预结果
        HeaderStoreItemInfo destinationInfo = CreateAndAddHeaderToStore(descriptor);                
        
        // for raw value
        destinationInfo.RawValue = CloneStringHeaderInfoValues(sourceInfo.RawValue);
               
        // 如果 descriptor 的 parser 为 null（customer header value）
        if (descriptor.Parser == null)
        {
            // We have custom header values. The parsed values are strings.
            // Custom header values are always stored as string or list of strings.
            Debug.Assert(
                sourceInfo.InvalidValue == null, 
                "No invalid values expected for custom headers.");
            
            // 复制 parsed value 为 string
            destinationInfo.ParsedValue = CloneStringHeaderInfoValues(sourceInfo.ParsedValue);
        }
        // （否则，即 descriptor 有 parser）
        else
        {            
            // 复制 invalid value (always strings)
            destinationInfo.InvalidValue = CloneStringHeaderInfoValues(sourceInfo.InvalidValue);
            
            // 复制 parsed value
            if (sourceInfo.ParsedValue != null)
            {
                List<object>? sourceValues = sourceInfo.ParsedValue as List<object>;
                if (sourceValues == null)
                {
                    CloneAndAddValue(destinationInfo, sourceInfo.ParsedValue);
                }
                else
                {
                    foreach (object item in sourceValues)
                    {
                        CloneAndAddValue(destinationInfo, item);
                    }
                }
            }
        }
    }
    
    // 复制 string header value
    [return: NotNullIfNotNull("source")]
    private static object? CloneStringHeaderInfoValues(object? source)
    {
        if (source == null)
        {
            return null;
        }
        
        List<object>? sourceValues = source as List<object>;
        if (sourceValues == null)
        {
            // If we just have one value, 
            // return the reference to the string (strings are immutable so it's OK to use the reference).
            return source;
        }
        else
        {
            // If we have a list of strings, 
            // create a new list and copy all strings to the new list.
            return new List<object>(sourceValues);
        }
    }
    
    // 复制 non-string header value
    private static void CloneAndAddValue(HeaderStoreItemInfo destinationInfo, object source)
    {
        // We only have one value. Clone it and assign it to the store.
        if (source is ICloneable cloneableValue)
        {
            AddParsedValue(destinationInfo, cloneableValue.Clone());
        }
        else
        {
            // If it doesn't implement ICloneable, it's a value type or an immutable type like String/Uri.
            AddParsedValue(destinationInfo, source);
        }
    }
}
```

###### 2.4.1.2 crud value to header info

###### - parse and add value

* 解析 header value 并注入 header info 的 parsed value

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{
    private void ParseAndAddValue(
        HeaderDescriptor descriptor, 
        HeaderStoreItemInfo info, 
        string? value)
    {
        Debug.Assert(info != null);
        
        if (descriptor.Parser == null)
        {
            // If we don't have a parser for the header, we consider the value valid if it doesn't contains
            // invalid newline characters. We add the values as "parsed value". Note that we allow empty values.
            CheckInvalidNewLine(value);
            AddParsedValue(info, value ?? string.Empty);
            return;
        }
        
        // If the header only supports 1 value, we can add the current value only if there is no value already set.
        if (!info.CanAddParsedValue(descriptor.Parser))
        {
            throw new FormatException(
                SR.Format(
                    System.Globalization.CultureInfo.InvariantCulture, 
                    SR.net_http_headers_single_value_header, descriptor.Name));
        }
        
        int index = 0;
        object parsedValue = descriptor.Parser.ParseValue(value, info.ParsedValue, ref index);
        
        // The raw string only represented one value (which was successfully parsed). Add the value and return.
        // If value is null we still have to first call ParseValue() to allow the parser to decide whether null is
        // a valid value. If it is (i.e. no exception thrown), we set the parsed value (if any) and return.
        if ((value == null) || (index == value.Length))
        {
            // If the returned value is null, then it means the header accepts empty values. i.e. we don't throw
            // but we don't add 'null' to the store either.
            if (parsedValue != null)
            {
                AddParsedValue(info, parsedValue);
            }
            return;
        }
        Debug.Assert(index < value.Length, "Parser must return an index value within the string length.");
        
        // If we successfully parsed a value, but there are more left to read, store the results in a temp list. 
        // Only when all values are parsed successfully write the list to the store.
        List<object> parsedValues = new List<object>();
        if (parsedValue != null)
        {
            parsedValues.Add(parsedValue);
        }
        
        while (index < value.Length)            
        {
            parsedValue = descriptor.Parser.ParseValue(value, info.ParsedValue, ref index);
            if (parsedValue != null)
            {
                parsedValues.Add(parsedValue);
            }
        }
        
        // All values were parsed correctly. Copy results to the store.
        foreach (object item in parsedValues)
        {
            AddParsedValue(info, item);
        }
    }    
    
    // check invalid new line
    private static void CheckInvalidNewLine(string? value)
    {
        if (value == null)
        {
            return;
        }
        
        if (HttpRuleParser.ContainsInvalidNewLine(value))
        {
            throw new FormatException(SR.net_http_headers_no_newlines);
        }
    }    
}

```

###### - get/set parsed value

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{
    // get parsed values
    internal object? GetParsedValues(HeaderDescriptor descriptor)
    {
        if (!TryGetAndParseHeaderInfo(descriptor, out HeaderStoreItemInfo? info))
        {
            return null;
        }
        
        return info.ParsedValue;
    }
    
    // set parsed values
    internal void SetParsedValue(HeaderDescriptor descriptor, object value)
    {
        Debug.Assert(value != null);
        Debug.Assert(descriptor.Parser != null, "Can't add parsed value if there is no parser available.");
        
        // 解析 header info
        HeaderStoreItemInfo info = GetOrCreateHeaderInfo(descriptor, parseRawValues: true);
        // reset header info
        info.InvalidValue = null;
        info.ParsedValue = null;
        info.RawValue = null;
        // 注入 value (as parsed)
        AddParsedValue(info, value);
    }
    
    // set or remove parsed value
    internal void SetOrRemoveParsedValue(HeaderDescriptor descriptor, object? value)
    {
        // 如果 descriptor 为 null，-> 删除 descriptor
        if (value == null)
        {
            Remove(descriptor);
        }
        // （否则，即 descriptor 不为 null），-> set parsed value
        else
        {
            SetParsedValue(descriptor, value);
        }
    }
    
    // is contains    
    internal bool ContainsParsedValue(HeaderDescriptor descriptor, object value)
    {
        Debug.Assert(value != null);
        
        if (_headerStore == null)
        {
            return false;
        }
        
        // If we have a value for this header, then verify if we have a single value. 
        //   - If so, compare that value with 'item'. 
        //   - If we have a list of values, then compare each item in the list with 'item'.
        if (TryGetAndParseHeaderInfo(descriptor, out HeaderStoreItemInfo? info))
        {
            Debug.Assert(
                descriptor.Parser != null, 
                "Can't add parsed value if there is no parser available.");
            
            Debug.Assert("This method should not be used for single-value headers. Use equality comparer instead.");

            // If there is no entry, just return.
            if (info.ParsedValue == null)
            {
                return false;
            }
            
            List<object>? parsedValues = info.ParsedValue as List<object>;
            
            IEqualityComparer? comparer = descriptor.Parser.Comparer;
            
            if (parsedValues == null)
            {
                Debug.Assert(
                    info.ParsedValue.GetType() == value.GetType(),
                    "Stored value does not have the same type as 'value'.");
                
                return AreEqual(value, info.ParsedValue, comparer);
            }
            else
            {
                foreach (object item in parsedValues)
                {
                    Debug.Assert(
                        item.GetType() == value.GetType(),
                        "One of the stored values does not have the same type as 'value'.");
                    
                    if (AreEqual(value, item, comparer))
                    {
                        return true;
                    }
                }
                
                return false;
            }
        }
        
        return false;
    }
}

```

###### 2.4.1.3 crud of header info

###### - get or create header info

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{
    /* get or create */
    private HeaderStoreItemInfo GetOrCreateHeaderInfo(HeaderDescriptor descriptor, bool parseRawValues)
    {
        // 预结果
        HeaderStoreItemInfo? result = null;
        bool found;
        
        // 解析 raw value
        if (parseRawValues)
        {
            found = TryGetAndParseHeaderInfo(descriptor, out result);
        }
        // 不解析 raw value
        else
        {            
            found = TryGetHeaderValue(descriptor, out object? value);
            if (found)
            {
                if (value is HeaderStoreItemInfo hsti)
                {
                    result = hsti;
                }
                else
                {
                    Debug.Assert(value is string);
                    _headerStore![descriptor] = result = new HeaderStoreItemInfo { RawValue = value };
                }
            }
        }
        
        // 如果没有 found，-> 创建 header info（空的）并注入 header store
        if (!found)
        {            
            result = CreateAndAddHeaderToStore(descriptor);
        }
        
        Debug.Assert(result != null);
        return result;
    }
        
    // create (header info) and add (it) to store
    private HeaderStoreItemInfo CreateAndAddHeaderToStore(HeaderDescriptor descriptor)
    {        
        // 创建 header info（空的）
        HeaderStoreItemInfo result = new HeaderStoreItemInfo();                
        Debug.Assert((descriptor.HeaderType & _treatAsCustomHeaderTypes) == 0);
        
        // 注入 header store
        AddHeaderToStore(descriptor, result);        
        return result;
    }

    // 将 value (header info 或者 string) 注入 header store
    private void AddHeaderToStore(HeaderDescriptor descriptor, object value)
    {
        Debug.Assert(value is string || value is HeaderStoreItemInfo);
        (_headerStore ??= new Dictionary<HeaderDescriptor, object>()).Add(descriptor, value);
    }
            
    // try get header value（从 header store 解析 value）
    internal bool TryGetHeaderValue(
        HeaderDescriptor descriptor, 
        [NotNullWhen(true)] out object? value)
    {
        if (_headerStore == null)
        {
            value = null;
            return false;
        }
        
        return _headerStore.TryGetValue(descriptor, out value);
    }
}

```

###### - try get & parse header info

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{
    // try get & parse header info
    private bool TryGetAndParseHeaderInfo(
        HeaderDescriptor key, 
        [NotNullWhen(true)] out HeaderStoreItemInfo? info)
    {
        // 从 header store 解析 value (header info 或者 string)
        if (TryGetHeaderValue(key, out object? value))
        {
            // get the header info
            if (value is HeaderStoreItemInfo hsi)
            {
                info = hsi;
            }
            else
            {
                Debug.Assert(value is string);
                // string 转换成 header info
                _headerStore![key] = info = new HeaderStoreItemInfo() { RawValue = value };
            }
                        
            // 解析 header info 的 raw value
            return ParseRawHeaderValues(key, info, removeEmptyHeader: true);
        }
        
        info = null;
        return false;
    }   
    
    // this method tries to parse all non-validated header values (if any) before returning to the caller.
    private bool ParseRawHeaderValues(
        HeaderDescriptor descriptor, 
        HeaderStoreItemInfo info, 
        bool removeEmptyHeader)
    {        
        if (info.RawValue != null)
        {
            // 获取 header store item info 的 raw info
            List<string>? rawValues = info.RawValue as List<string>;
            
            // raw info 是 string
            if (rawValues == null)
            {
                // 1-
                ParseSingleRawHeaderValue(descriptor, info);
            }
            // raw info 是 list<string>
            else
            {
                // 2-
                ParseMultipleRawHeaderValues(descriptor, info, rawValues);
            }
            
            // Reset RawValue.
            info.RawValue = null;
            
            // 如果 invalid value 和 parsed value 都为 null，
            if ((info.InvalidValue == null) && (info.ParsedValue == null))
            {
                // 并且标记了 remove empty header，-> 从 header store 中删除 descriptor
                if (removeEmptyHeader)
                {                    
                    Debug.Assert(_headerStore != null);
                    _headerStore.Remove(descriptor);
                }
                return false;
            }
        }
        
        return true;
    }
    
    // 1- 
    private static void ParseSingleRawHeaderValue(
        HeaderDescriptor descriptor, 
        HeaderStoreItemInfo info)
    {
        // 获取 header info 的 raw value
        string? rawValue = info.RawValue as string;
        Debug.Assert(rawValue != null, "RawValue must either be List<string> or string.");
        
        // 如果 header descriptor 的 parser 为 null，
        if (descriptor.Parser == null)
        {
            // 如果 raw line 不包含 invalid new line，-> add parsed value
            if (!ContainsInvalidNewLine(rawValue, descriptor.Name))
            {
                AddParsedValue(info, rawValue);
            }
        }
        // （否则，即 header descriptor 的 parser 不为 null），-> try parse & add raw header value
        else
        {
            if (!TryParseAndAddRawHeaderValue(descriptor, info, rawValue, true))
            {
                if (NetEventSource.Log.IsEnabled()) NetEventSource.Log.HeadersInvalidValue(descriptor.Name, rawValue);
            }
        }
    }
    
    // 2- 
    private static void ParseMultipleRawHeaderValues(
        HeaderDescriptor descriptor, 
        HeaderStoreItemInfo info, 
        List<string> rawValues)
    {
        if (descriptor.Parser == null)
        {
            foreach (string rawValue in rawValues)
            {
                if (!ContainsInvalidNewLine(rawValue, descriptor.Name))
                {
                    AddParsedValue(info, rawValue);
                }
            }
        }
        else
        {
            foreach (string rawValue in rawValues)
            {
                if (!TryParseAndAddRawHeaderValue(descriptor, info, rawValue, true))
                {
                    if (NetEventSource.Log.IsEnabled()) NetEventSource.Log.HeadersInvalidValue(descriptor.Name, rawValue);
                }
            }
        }
    }
        
    // containsl invalid new line
    private static bool ContainsInvalidNewLine(string value, string name)
    {
        if (HttpRuleParser.ContainsInvalidNewLine(value))
        {
            if (NetEventSource.Log.IsEnabled()) NetEventSource.Error(
                	null, 
                	SR.Format(SR.net_http_log_headers_no_newlines, name, value));
            
            return true;
        }
        
        return false;
    }
}

```

###### - prepare header info to add

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{
    private void PrepareHeaderInfoForAdd(
        HeaderDescriptor descriptor, 
        out HeaderStoreItemInfo info, 
        out bool addToStore)
    {
        if (!IsAllowedHeaderName(descriptor))
        {
            throw new InvalidOperationException(
                SR.Format(
                    SR.net_http_headers_not_allowed_header_name, 
                    descriptor.Name));
        }
        
        // 标记不要 add to store
        addToStore = false;
        
        // 如果不能 get & parse header（不能从 header store 解析）
        if (!TryGetAndParseHeaderInfo(descriptor, out info!))
        {
            // 创建 header info（空的）
            info = new HeaderStoreItemInfo();
            // 标记 add to store（后续会注入 header store）
            addToStore = true;
        }
    }
    
    internal virtual bool IsAllowedHeaderName(HeaderDescriptor descriptor) => true;
}

```

###### 2.4.1.4 parse  value (string)

###### - try parse & add raw header value

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{
    private static bool TryParseAndAddRawHeaderValue(
        HeaderDescriptor descriptor, 
        HeaderStoreItemInfo info, 
        string? value, 
        bool addWhenInvalid)
    {
        Debug.Assert(info != null);
        Debug.Assert(descriptor.Parser != null);
        
        // Values are added as 'invalid'  
        //   - if we either can't parse the value OR 
        //   - if we already have a value and the current header doesn't support multiple values: 
        // (e.g. trying to add a date/time value to the 'Date' header if we already have a date/time value will result in 
        // the second value being added to the 'invalid' header values.)
        
        // a- add value to "invalid value"
        if (!info.CanAddParsedValue(descriptor.Parser))
        {
            if (addWhenInvalid)
            {
                AddInvalidValue(info, value ?? string.Empty);
            }
            return false;
        }
        
        
        int index = 0;
        
        if (descriptor.Parser.TryParseValue(value, info.ParsedValue, ref index, out object? parsedValue))
        {
            // value 为 null，或者 value 是单一值（parse 之后 index 在结尾处），-> add value to "parsed value"
            if ((value == null) || (index == value.Length))
            {
                if (parsedValue != null)
                {
                    AddParsedValue(info, parsedValue);
                }
                return true;
            }
            
            Debug.Assert(index < value.Length, "Parser must return an index value within the string length.");
            
            // If we successfully parsed a value, but there are more left to read, store the results in a temp list. 
            // Only when all values are parsed successfully write the list to the store.
            List<object> parsedValues = new List<object>();
            if (parsedValue != null)
            {
                parsedValues.Add(parsedValue);
            }
            // 遍历 parsed value 注入 parsed values (temp)
            while (index < value.Length)
            {
                if (descriptor.Parser.TryParseValue(value, info.ParsedValue, ref index, out parsedValue))
                {
                    if (parsedValue != null)
                    {
                        parsedValues.Add(parsedValue);
                    }
                }
                else
                {
                    if (!ContainsInvalidNewLine(value, descriptor.Name) && addWhenInvalid)
                    {
                        AddInvalidValue(info, value);
                    }
                    return false;
                }
            }
            
            // All values were parsed correctly. Copy results to the store.
            foreach (object item in parsedValues)
            {
                AddParsedValue(info, item);
            }
            return true;
        }
        
        // 如果标记了 add when invalid，-> 注入 "invalid value"
        Debug.Assert(value != null);
        if (!ContainsInvalidNewLine(value, descriptor.Name) && addWhenInvalid)
        {
            AddInvalidValue(info, value ?? string.Empty);
        }
        return false;
    }    
}

```

###### - try parse and add value

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{
    internal bool TryParseAndAddValue(HeaderDescriptor descriptor, string? value)
    {        
        HeaderStoreItemInfo info;
        bool addToStore;
        PrepareHeaderInfoForAdd(descriptor, out info, out addToStore);
        
        // 解析 value string 到 header info
        bool result = TryParseAndAddRawHeaderValue(descriptor, info, value, false);
        // 将 header info 注入 header store
        if (result && addToStore && (info.ParsedValue != null))
        {            
            AddHeaderToStore(descriptor, info);
        }
        
        return result;
    }        
}
```

##### 2.4.2 get header descriptor

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{
    // get header descriptor
    private HeaderDescriptor GetHeaderDescriptor(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException(SR.net_http_argument_empty_string, nameof(name));
        }
        
        // 解析 header descriptor
        if (!HeaderDescriptor.TryGet(name, out HeaderDescriptor descriptor))
        {
            throw new FormatException(SR.net_http_headers_invalid_header_name);
        }
        
        // 如果解析的 header descriptor 是 allowd header type，-> 返回 header descriptor
        if ((descriptor.HeaderType & _allowedHeaderTypes) != 0)
        {
            return descriptor;
        }
        // 如果解析的 header descriptor 是 treat as custom header type， -> header descriptor 转换为 custom header 并返回
        else if ((descriptor.HeaderType & _treatAsCustomHeaderTypes) != 0)
        {
            return descriptor.AsCustomHeader();
        }
        // 都不是，-> 抛出异常
        throw new InvalidOperationException(SR.Format(SR.net_http_headers_not_allowed_header_name, name));
    }
    
    // try get header descriptor（不抛出异常）
    private bool TryGetHeaderDescriptor(string name, out HeaderDescriptor descriptor)
    {
        if (string.IsNullOrEmpty(name))
        {
            descriptor = default;
            return false;
        }
        
        if (HeaderDescriptor.TryGet(name, out descriptor))
        {
            if ((descriptor.HeaderType & _allowedHeaderTypes) != 0)
            {
                return true;
            }
            
            if ((descriptor.HeaderType & _treatAsCustomHeaderTypes) != 0)
            {
                descriptor = descriptor.AsCustomHeader();
                return true;
            }
        }
        
        return false;
    }
}

```

##### 2.4.3 add 

###### 2.4.3.1 add header name & value

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{
    // add header by [name, value]
    public void Add(string name, string? value) => Add(GetHeaderDescriptor(name), value);
    
    internal void Add(HeaderDescriptor descriptor, string? value)
    {
        
        PrepareHeaderInfoForAdd(descriptor, out HeaderStoreItemInfo info, out bool addToStore);        
        ParseAndAddValue(descriptor, info, value);
                
        if (addToStore && (info.ParsedValue != null))
        {
            
            AddHeaderToStore(descriptor, info);
        }
    }        
    
    // add header by [name, values]
    public void Add(string name, IEnumerable<string?> values) => Add(GetHeaderDescriptor(name), values);
    
    internal void Add(HeaderDescriptor descriptor, IEnumerable<string?> values)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }
                
        PrepareHeaderInfoForAdd(descriptor, out HeaderStoreItemInfo info, out bool addToStore);
        
        try
        {            
            foreach (string? value in values)
            {                
                ParseAndAddValue(descriptor, info, value);
            }
        }
        finally
        {            
            if (addToStore && (info.ParsedValue != null))
            {                
                AddHeaderToStore(descriptor, info);
            }
        }
    }
    
    // add parsed value by 
    internal void AddParsedValue(HeaderDescriptor descriptor, object value)
    {
        Debug.Assert(value != null);
        Debug.Assert(
            descriptor.Parser != null, 
            "Can't add parsed value if there is no parser available.");      
        
        HeaderStoreItemInfo info = GetOrCreateHeaderInfo(descriptor, parseRawValues: true);
        
        // If the current header has only one value, we can't add another value. The strongly typed property must not call 
        // AddParsedValue(), but SetParsedValue(). E.g. for headers like 'Date', 'Host'.
        Debug.Assert(
            descriptor.Parser.SupportsMultipleValues, 
            $"Header '{descriptor.Name}' doesn't support multiple values");        
        AddParsedValue(info, value);
    }            
}

```

###### 2.4.3.2 try add without validation

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{
    // try add by [name, value]
    public bool TryAddWithoutValidation(string name, string? value) =>
        TryGetHeaderDescriptor(name, out HeaderDescriptor descriptor) &&
        TryAddWithoutValidation(descriptor, value);
    
    internal bool TryAddWithoutValidation(HeaderDescriptor descriptor, string? value)
    {
        // Normalize null values to be empty values, which are allowed. 
        // If the user adds multiple null/empty values, all of them are added to the collection. 
        // This will result in delimiter-only values, e.g. adding two null-strings (or empty, or whitespace-only) 
        // results in "My-Header: ,".
        value ??= string.Empty;
                
        _headerStore ??= new Dictionary<HeaderDescriptor, object>();
        
        // 如果能够从 header store 解析 value (header info 或者 string)，
        // 即 header 被注册过
        if (_headerStore.TryGetValue(descriptor, out object? currentValue))
        {
            if (currentValue is HeaderStoreItemInfo info)
            {
                // The header store already contained a HeaderStoreItemInfo, so add to it.
                AddRawValue(info, value);
            }
            else
            {
                // The header store contained a single raw string value, so promote it
                // to being a HeaderStoreItemInfo and add to it.
                Debug.Assert(currentValue is string);
                _headerStore[descriptor] = info = new HeaderStoreItemInfo() { RawValue = currentValue };
                AddRawValue(info, value);
            }
        }
        // 不能从 header store 解析 value，即 header 没有注册过，-> 注入 store
        else
        {
            // The header store did not contain the header.  Add the raw string.
            _headerStore.Add(descriptor, value);
        }
        
        return true;
    }
    
    // try add [name, values]
    public bool TryAddWithoutValidation(string name, IEnumerable<string?> values) =>
        TryGetHeaderDescriptor(name, out HeaderDescriptor descriptor) &&
        TryAddWithoutValidation(descriptor, values);
    
    internal bool TryAddWithoutValidation(HeaderDescriptor descriptor, IEnumerable<string?> values)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }
        
        using (IEnumerator<string?> enumerator = values.GetEnumerator())
        {
            if (enumerator.MoveNext())
            {
                TryAddWithoutValidation(descriptor, enumerator.Current);
                if (enumerator.MoveNext())
                {
                    HeaderStoreItemInfo info = GetOrCreateHeaderInfo(descriptor, parseRawValues: false);
                    do
                    {
                        AddRawValue(info, enumerator.Current ?? string.Empty);
                    }
                    while (enumerator.MoveNext());
                }
            }
        }
        
        return true;
    }            
}

```

###### 2.4.3.3 add parsed value

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{
    
}

```

###### 2.4.3.4 add headers

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{
    internal virtual void AddHeaders(HttpHeaders sourceHeaders)
    {
        Debug.Assert(sourceHeaders != null);
        Debug.Assert(
            GetType() == sourceHeaders.GetType(), 
            "Can only copy headers from an instance of the same type.");
        
        Dictionary<HeaderDescriptor, object>? sourceHeadersStore = sourceHeaders._headerStore;
        if (sourceHeadersStore is null || sourceHeadersStore.Count == 0)
        {
            return;
        }
        
        _headerStore ??= new Dictionary<HeaderDescriptor, object>();
        
        foreach (KeyValuePair<HeaderDescriptor, object> header in sourceHeadersStore)
        {
            // Only add header values if they're not already set on the message. 
            // Note that we don't merge collections: 
            //   If both the default headers and the message have set some values for a certain header, then we don't 
            //   try to merge the values.
            if (!_headerStore.ContainsKey(header.Key))
            {
                object sourceValue = header.Value;
                if (sourceValue is HeaderStoreItemInfo info)
                {
                    AddHeaderInfo(header.Key, info);
                }
                else
                {
                    Debug.Assert(sourceValue is string);
                    _headerStore.Add(header.Key, sourceValue);
                }
            }
        }
    }
}

```

##### 2.4.4 header to string

###### 2.4.4.1 get string for descriptor

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{
    internal string GetHeaderString(HeaderDescriptor descriptor)
    {
        if (TryGetHeaderValue(descriptor, out object? info))
        {
            GetStoreValuesAsStringOrStringArray(
                descriptor, 
                info, 
                out string? singleValue, 
                out string[]? multiValue);
            
            Debug.Assert(singleValue is not null ^ multiValue is not null);
            
            if (singleValue is not null)
            {
                return singleValue;
            }
            
            // Note that if we get multiple values for a header that doesn't support multiple values, we'll
            // just separate the values using a comma (default separator).
            string? separator = 
                descriptor.Parser != null && descriptor.Parser.SupportsMultipleValues 
                	? descriptor.Parser.Separator 
                	: HttpHeaderParser.DefaultSeparator;
            
            return string.Join(separator, multiValue!);
        }
        
        return string.Empty;
    }
}

```

###### 2.4.4.2 headers to string

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{
    public override string ToString()
    {
        // Return all headers as string similar to:
        // HeaderName1: Value1, Value2
        // HeaderName2: Value1
        // ...
        
        var vsb = new ValueStringBuilder(stackalloc char[512]);
        
        if (_headerStore is Dictionary<HeaderDescriptor, object> headerStore)
        {
            foreach (KeyValuePair<HeaderDescriptor, object> header in headerStore)
            {
                vsb.Append(header.Key.Name);
                vsb.Append(": ");
                
                GetStoreValuesAsStringOrStringArray(
                    header.Key, 
                    header.Value, 
                    out string? singleValue, 
                    out string[]? multiValue);
                
                Debug.Assert(singleValue is not null ^ multiValue is not null);
                
                if (singleValue is not null)
                {
                    vsb.Append(singleValue);
                }
                else
                {
                    // Note that if we get multiple values for a header that doesn't support multiple values, we'll
                    // just separate the values using a comma (default separator).
                    string? separator = 
                        header.Key.Parser is HttpHeaderParser parser && parser.SupportsMultipleValues 
                        	? parser.Separator 
                        	: HttpHeaderParser.DefaultSeparator;

                    for (int i = 0; i < multiValue!.Length; i++)
                    {
                        if (i != 0) vsb.Append(separator);
                        vsb.Append(multiValue[i]);
                    }
                }
                
                vsb.Append(Environment.NewLine);
            }
        }
        
        return vsb.ToString();
    }
}

```

###### 2.4.4.3 get value string

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{
    // get values string
    internal static string[] GetStoreValuesAsStringArray(
        HeaderDescriptor descriptor, 
        HeaderStoreItemInfo info)
    {
        GetStoreValuesAsStringOrStringArray(
            descriptor, 
            info, 
            out string? singleValue, 
            out string[]? multiValue);
        
        Debug.Assert(singleValue is not null ^ multiValue is not null);
        return multiValue ?? new[] { singleValue! };
    }
    
    // get values string 
    internal static void GetStoreValuesAsStringOrStringArray(
        HeaderDescriptor descriptor, 
        object sourceValues, 
        out string? singleValue, 
        out string[]? multiValue)
    {
        HeaderStoreItemInfo? info = sourceValues as HeaderStoreItemInfo;
        
        // source value 是 string
        if (info is null)
        {
            Debug.Assert(sourceValues is string);
            singleValue = (string)sourceValues;
            multiValue = null;
            return;
        }
        
        // source value 是 header info
        
        int length = GetValueCount(info);
        
        Span<string?> values;
        singleValue = null;
        if (length == 1)
        {
            multiValue = null;
            values = MemoryMarshal.CreateSpan(ref singleValue, 1);
        }
        else
        {
            values = multiValue = length != 0 ? new string[length] : Array.Empty<string>();
        }
        
        int currentIndex = 0;
        ReadStoreValues<string?>(values, info.RawValue, null, ref currentIndex);
        ReadStoreValues<object?>(values, info.ParsedValue, descriptor.Parser, ref currentIndex);
        ReadStoreValues<string?>(values, info.InvalidValue, null, ref currentIndex);
        Debug.Assert(currentIndex == length);
    }
    
    // get value string & add to byte[]
    internal static int GetStoreValuesIntoStringArray(
        HeaderDescriptor descriptor, 
        object sourceValues, 
        [NotNull] ref string[]? values)
    {
        values ??= Array.Empty<string>();        
        
        HeaderStoreItemInfo? info = sourceValues as HeaderStoreItemInfo;
        
        // source value 是 string
        if (info is null)
        {
            Debug.Assert(sourceValues is string);
            
            if (values.Length == 0)
            {
                values = new string[1];
            }
            
            values[0] = (string)sourceValues;
            return 1;
        }
        
        // source value 是 header info
        
        int length = GetValueCount(info);
        
        if (length > 0)
        {
            if (values.Length < length)
            {
                values = new string[length];
            }
            
            int currentIndex = 0;
            ReadStoreValues<string?>(values, info.RawValue, null, ref currentIndex);
            ReadStoreValues<object?>(values, info.ParsedValue, descriptor.Parser, ref currentIndex);
            ReadStoreValues<string?>(values, info.InvalidValue, null, ref currentIndex);
            Debug.Assert(currentIndex == length);
        }
        
        return length;
    }
    
    // get value count
    private static int GetValueCount(HeaderStoreItemInfo info)
    {
        Debug.Assert(info != null);
        
        int valueCount = Count<string>(info.RawValue);
        valueCount += Count<string>(info.InvalidValue);
        valueCount += Count<object>(info.ParsedValue);
        return valueCount;
        
        static int Count<T>(object? valueStore) =>
            valueStore is null 
            	? 0 
            	: valueStore is List<T> list 
                    ? list.Count 
                    : 1;
    }
    
    // read store value
    private static void ReadStoreValues<T>(
        Span<string?> values, 
        object? storeValue, 
        HttpHeaderParser? parser, 
        ref int currentIndex)
    {
        if (storeValue != null)
        {
            List<T>? storeValues = storeValue as List<T>;
            
            if (storeValues == null)
            {
                values[currentIndex] = parser == null ? storeValue.ToString() : parser.ToString(storeValue);
                currentIndex++;
            }
            else
            {
                foreach (object? item in storeValues)
                {
                    Debug.Assert(item != null);
                    values[currentIndex] = parser == null ? item.ToString() : parser.ToString(item);
                    currentIndex++;
                }
            }
        }
    }        
}

```

##### 2.4.5 get values (from headers)

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{
    // get values by [names]
    public IEnumerable<string> GetValues(string name) => GetValues(GetHeaderDescriptor(name));
    
    internal IEnumerable<string> GetValues(HeaderDescriptor descriptor)
    {
        if (TryGetValues(descriptor, out IEnumerable<string>? values))
        {
            return values;
        }
        
        throw new InvalidOperationException(SR.net_http_headers_not_found);
    }
    
    // try get values by [names]
    public bool TryGetValues(string name, [NotNullWhen(true)] out IEnumerable<string>? values)
    {
        if (TryGetHeaderDescriptor(name, out HeaderDescriptor descriptor))
        {
            return TryGetValues(descriptor, out values);
        }
        
        values = null;
        return false;
    }
    
    // try get values from descriptor
    internal bool TryGetValues(HeaderDescriptor descriptor, [NotNullWhen(true)] out IEnumerable<string>? values)
    {
        if (_headerStore != null && 
            TryGetAndParseHeaderInfo(descriptor, out HeaderStoreItemInfo? info))
        {
            values = GetStoreValuesAsStringArray(descriptor, info);
            return true;
        }
        
        values = null;
        return false;
    }       
}

```

##### 2.4.6 contains (header)

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{
    public bool Contains(string name) => Contains(GetHeaderDescriptor(name));
    
    internal bool Contains(HeaderDescriptor descriptor)
    {
        // We can't just call headerStore.ContainsKey() since after parsing the value the header may not exist anymore 
        // (if the value contains invalid newline chars, we remove the header). So try to parse the header value.
        return _headerStore != null && TryGetAndParseHeaderInfo(descriptor, out _);
    }
}

```

##### 2.4.7 remove (header)

```c#
public abstract class HttpHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
{
    // clear all
    public void Clear() => _headerStore?.Clear();
    
    // remove by [name]
    public bool Remove(string name) => Remove(GetHeaderDescriptor(name));
    
    // remove by [descriptor]
    internal bool Remove(HeaderDescriptor descriptor) => _headerStore != null && _headerStore.Remove(descriptor);
    
    // remove parsed value
    internal bool RemoveParsedValue(HeaderDescriptor descriptor, object value)
    {
        Debug.Assert(value != null);
        
        if (_headerStore == null)
        {
            return false;
        }
        
        // If we have a value for this header, then verify if we have a single value. If so, compare that
        // value with 'item'. If we have a list of values, then remove 'item' from the list.
        if (TryGetAndParseHeaderInfo(descriptor, out HeaderStoreItemInfo? info))
        {
            Debug.Assert(
                descriptor.Parser != null, 
                "Can't add parsed value if there is no parser available.");
            
            Debug.Assert(
                descriptor.Parser.SupportsMultipleValues,
                "This method should not be used for single-value headers. Use Remove(string) instead.");
            
            // If there is no entry, just return.
            if (info.ParsedValue == null)
            {
                return false;
            }
            
            bool result = false;
            IEqualityComparer? comparer = descriptor.Parser.Comparer;
            
            List<object>? parsedValues = info.ParsedValue as List<object>;
            if (parsedValues == null)
            {
                Debug.Assert(
                    info.ParsedValue.GetType() == value.GetType(),
                    "Stored value does not have the same type as 'value'.");
                
                if (AreEqual(value, info.ParsedValue, comparer))
                {
                    info.ParsedValue = null;
                    result = true;
                }
            }
            else
            {
                foreach (object item in parsedValues)
                {
                    Debug.Assert(
                        item.GetType() == value.GetType(),
                        "One of the stored values does not have the same type as 'value'.");
                    
                    if (AreEqual(value, item, comparer))
                    {
                        // Remove 'item' rather than 'value', since the 'comparer' may consider two values
                        // equal even though the default obj.Equals() may not (e.g. if 'comparer' does
                        // case-insensitive comparison for strings, but string.Equals() is case-sensitive).
                        result = parsedValues.Remove(item);
                        break;
                    }
                }
                
                // If we removed the last item in a list, remove the list.
                if (parsedValues.Count == 0)
                {
                    info.ParsedValue = null;
                }
            }
            
            // If there is no value for the header left, remove the header.
            if (info.IsEmpty)
            {
                bool headerRemoved = Remove(descriptor);
                Debug.Assert(headerRemoved, "Existing header '" + descriptor.Name + "' couldn't be removed.");
            }
            
            return result;
        }
        
        return false;
    }
}
    
```

#### 2.5 variety of http headers

##### 2.5.1 http headers non validated

```c#
public readonly struct HttpHeadersNonValidated : IReadOnlyDictionary<string, HeaderStringValues>
{    
    private readonly HttpHeaders? _headers;
    
    public int Count => _headers?.HeaderStore?.Count ?? 0;
    
    
    internal HttpHeadersNonValidated(HttpHeaders headers) => _headers = headers;
    
    
    public bool Contains(string headerName) =>
        _headers is HttpHeaders headers &&
        HeaderDescriptor.TryGet(headerName, out HeaderDescriptor descriptor) &&
        headers.TryGetHeaderValue(descriptor, out _);
    
   
    public HeaderStringValues this[string headerName]
    {
        get
        {
            if (TryGetValues(headerName, out HeaderStringValues values))
            {
                return values;
            }
            
            throw new KeyNotFoundException(SR.net_http_headers_not_found);
        }
    }
    
    public bool TryGetValues(string headerName, out HeaderStringValues values)
    {
        if (_headers is HttpHeaders headers &&
            HeaderDescriptor.TryGet(headerName, out HeaderDescriptor descriptor) &&
            headers.TryGetHeaderValue(descriptor, out object? info))
        {
            HttpHeaders.GetStoreValuesAsStringOrStringArray(descriptor, info, out string? singleValue, out string[]? multiValue);
            Debug.Assert(singleValue is not null ^ multiValue is not null);
            values = singleValue is not null ?
                new HeaderStringValues(descriptor, singleValue) :
            new HeaderStringValues(descriptor, multiValue!);
            return true;
        }
        
        values = default;
        return false;
    }
    
    // read only dictionary
    bool IReadOnlyDictionary<string, HeaderStringValues>.ContainsKey(string key) => Contains(key);
        
    bool IReadOnlyDictionary<string, HeaderStringValues>.TryGetValue(string key, out HeaderStringValues value) => 
        TryGetValues(key, out value);

    // enumerator    
    public Enumerator GetEnumerator() =>
        _headers is HttpHeaders headers && 
        headers.HeaderStore is Dictionary<HeaderDescriptor, object> store 
        	? new Enumerator(store.GetEnumerator()) 
        	: default;
        
    IEnumerator<KeyValuePair<string, HeaderStringValues>> IEnumerable<KeyValuePair<string, HeaderStringValues>>.GetEnumerator() => 
        GetEnumerator();
        
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
       
    IEnumerable<string> IReadOnlyDictionary<string, HeaderStringValues>.Keys
    {
        get
        {
            foreach (KeyValuePair<string, HeaderStringValues> header in this)
            {
                yield return header.Key;
            }
        }
    }
        
    IEnumerable<HeaderStringValues> IReadOnlyDictionary<string, HeaderStringValues>.Values
    {
        get
        {
            foreach (KeyValuePair<string, HeaderStringValues> header in this)
            {
                yield return header.Value;
            }
        }
    }
    
    /// <summary>Enumerates the elements of a <see cref="HttpHeadersNonValidated"/>.</summary>
    public struct Enumerator : IEnumerator<KeyValuePair<string, HeaderStringValues>>
    {        
        private Dictionary<HeaderDescriptor, object>.Enumerator _headerStoreEnumerator;
        
        private KeyValuePair<string, HeaderStringValues> _current;
        public KeyValuePair<string, HeaderStringValues> Current => _current;
        
        private bool _valid;
                         
        internal Enumerator(Dictionary<HeaderDescriptor, object>.Enumerator headerStoreEnumerator)
        {
            _headerStoreEnumerator = headerStoreEnumerator;
            _current = default;
            _valid = true;
        }
                
        public bool MoveNext()
        {
            if (_valid && _headerStoreEnumerator.MoveNext())
            {
                KeyValuePair<HeaderDescriptor, object> current = _headerStoreEnumerator.Current;
                
                HttpHeaders.GetStoreValuesAsStringOrStringArray(
                    current.Key, 
                    current.Value, 
                    out string? singleValue, 
                    out string[]? multiValue);
                
                Debug.Assert(singleValue is not null ^ multiValue is not null);
                
                _current = new KeyValuePair<string, HeaderStringValues>(
                    current.Key.Name,
                    singleValue is not null 
                    	? new HeaderStringValues(current.Key, singleValue) 
                    	: new HeaderStringValues(current.Key, multiValue!));
                
                return true;
            }
            
            _current = default;
            return false;
        }
                               
        public void Dispose() { }
        
        object IEnumerator.Current => _current;        
        
        void IEnumerator.Reset() => throw new NotSupportedException();
    }
}

```

##### 2.5.2 http general headers

```c#
internal sealed class HttpGeneralHeaders
{        
    // connnection
    private HttpHeaderValueCollection<string>? _connection;
    
    private HttpHeaderValueCollection<string> ConnectionCore
    {
        get
        {
            if (_connection == null)
            {
                _connection = new HttpHeaderValueCollection<string>(
                    KnownHeaders.Connection.Descriptor,
                    _parent, 
                    HeaderUtilities.ConnectionClose, 
                    HeaderUtilities.TokenValidator);
            }
            return _connection;
        }
    }    
    
    public HttpHeaderValueCollection<string> Connection
    {
        get 
        {
            return ConnectionCore; 
        }
    }
    
    // trailer
    private HttpHeaderValueCollection<string>? _trailer;
    
    public HttpHeaderValueCollection<string> Trailer
    {
        get
        {
            if (_trailer == null)
            {
                _trailer = new HttpHeaderValueCollection<string>(
                    KnownHeaders.Trailer.Descriptor,
                    _parent, HeaderUtilities.TokenValidator);
            }
            return _trailer;
        }
        
    }
    
    // te
    private HttpHeaderValueCollection<TransferCodingHeaderValue>? _transferEncoding;   
    
    private HttpHeaderValueCollection<TransferCodingHeaderValue> TransferEncodingCore
    {
        get
        {
            if (_transferEncoding == null)
            {
                _transferEncoding = new HttpHeaderValueCollection<TransferCodingHeaderValue>(
                    KnownHeaders.TransferEncoding.Descriptor, 
                    _parent, 
                    HeaderUtilities.TransferEncodingChunked);
            }
            return _transferEncoding;
        }
    }
    
    public HttpHeaderValueCollection<TransferCodingHeaderValue> TransferEncoding
    {
        get 
        {
            return TransferEncodingCore; 
        }
    }                                      
    
    // upgrade
    private HttpHeaderValueCollection<ProductHeaderValue>? _upgrade;
    
    public HttpHeaderValueCollection<ProductHeaderValue> Upgrade
    {
        get
        {
            if (_upgrade == null)
            {
                _upgrade = new HttpHeaderValueCollection<ProductHeaderValue>(
                    KnownHeaders.Upgrade.Descriptor, 
                    _parent);
            }
            
            return _upgrade;
        }
    }
    
    // via
    private HttpHeaderValueCollection<ViaHeaderValue>? _via;
    
    public HttpHeaderValueCollection<ViaHeaderValue> Via
    {
        get
        {
            if (_via == null)
            {
                _via = new HttpHeaderValueCollection<ViaHeaderValue>(
                    KnownHeaders.Via.Descriptor, 
                    _parent);
            }
            
            return _via;
        }
    }
    
    // warning
    private HttpHeaderValueCollection<WarningHeaderValue>? _warning;
    
    public HttpHeaderValueCollection<WarningHeaderValue> Warning
    {
        get
        {
            if (_warning == null)
            {
                _warning = new HttpHeaderValueCollection<WarningHeaderValue>(
                    KnownHeaders.Warning.Descriptor, 
                    _parent);
            }
            
            return _warning;
        }
    }
    
    // pragma
    private HttpHeaderValueCollection<NameValueHeaderValue>? _pragma;
    
    public HttpHeaderValueCollection<NameValueHeaderValue> Pragma
    {
        get
        {
            if (_pragma == null)
            {
                _pragma = new HttpHeaderValueCollection<NameValueHeaderValue>(
                    KnownHeaders.Pragma.Descriptor, 
                    _parent);
            }
            
            return _pragma;
        }
    }
                
    // transfer encdoing chunked
    private bool _transferEncodingChunkedSet;
    
    internal static bool? GetTransferEncodingChunked(HttpHeaders parent, HttpGeneralHeaders? headers)
    {
        // If we've already initialized the transfer encoding header value collection and it contains the special value, 
        // or if we haven't and the headers contain the parsed special value, return true.  
        // We don't just access TransferEncodingCore, as doing so will unnecessarily initialize the collection even if it's not needed.
        if (headers?._transferEncoding != null)
        {
            if (headers._transferEncoding.IsSpecialValueSet)
            {
                return true;
            }
        }
        else if (parent.ContainsParsedValue(
            		KnownHeaders.TransferEncoding.Descriptor, 
            		HeaderUtilities.TransferEncodingChunked))
        {
            return true;
        }
        
        if (headers != null && headers._transferEncodingChunkedSet)
        {
            return false;
        }
        
        return null;
    }
    
    public bool? TransferEncodingChunked
    {
        get
        {
            // Separated out into a static to enable access to TransferEncodingChunked without the caller needing to 
            // force the creation of HttpGeneralHeaders if it wasn't created for other reasons.
            return GetTransferEncodingChunked(_parent, this);
        }
        set
        {
            if (value == true)
            {
                _transferEncodingChunkedSet = true;
                TransferEncodingCore.SetSpecialValue();
            }
            else
            {
                _transferEncodingChunkedSet = value != null;
                TransferEncodingCore.RemoveSpecialValue();
            }
        }
    }
    
    // connection close
    private bool _connectionCloseSet;
    
    internal static bool? GetConnectionClose(HttpHeaders parent, HttpGeneralHeaders? headers)
    {
        // If we've already initialized the connection header value collection
        // and it contains the special value, or if we haven't and the headers contain
        // the parsed special value, return true.  We don't just access ConnectionCore,
        // as doing so will unnecessarily initialize the collection even if it's not needed.
        if (headers?._connection != null)
        {
            if (headers._connection.IsSpecialValueSet)
            {
                return true;
            }
        }
        else if (parent.ContainsParsedValue(KnownHeaders.Connection.Descriptor, HeaderUtilities.ConnectionClose))
        {
            return true;
        }
        if (headers != null && headers._connectionCloseSet)
        {
            return false;
        }
        return null;
    }
    
    public bool? ConnectionClose
    {
        get
        {
            // Separated out into a static to enable access to TransferEncodingChunked without the caller needing to 
            // force the creation of HttpGeneralHeaders if it wasn't created for other reasons.
            return GetConnectionClose(_parent, this);
        }
        set
        {
            if (value == true)
            {
                _connectionCloseSet = true;
                ConnectionCore.SetSpecialValue();
            }
            else
            {
                _connectionCloseSet = value != null;
                ConnectionCore.RemoveSpecialValue();
            }
        }
    }
    
    // cache control
    public CacheControlHeaderValue? CacheControl
    {
        get 
        { 
            return (CacheControlHeaderValue?)_parent.GetParsedValues(KnownHeaders.CacheControl.Descriptor); 
        }
        set 
        {
            _parent.SetOrRemoveParsedValue(KnownHeaders.CacheControl.Descriptor, value); 
        }
    }
    
    // date
    public DateTimeOffset? Date
    {
        get 
        {
            return HeaderUtilities.GetDateTimeOffsetValue(KnownHeaders.Date.Descriptor, _parent); 
        }
        set
        {
            _parent.SetOrRemoveParsedValue(KnownHeaders.Date.Descriptor, value); 
        }
    }                                                
    
    // ctor
    private readonly HttpHeaders _parent;
    
    internal HttpGeneralHeaders(HttpHeaders parent)
    {
        Debug.Assert(parent != null);        
        _parent = parent;
    }
    
    // 方法- add special headers (transfer encoding chunked & connection close)
    internal void AddSpecialsFrom(HttpGeneralHeaders sourceHeaders)
    {
        // Copy special values, but do not overwrite
        bool? chunked = TransferEncodingChunked;
        if (!chunked.HasValue)
        {
            TransferEncodingChunked = sourceHeaders.TransferEncodingChunked;
        }
        
        bool? close = ConnectionClose;
        if (!close.HasValue)
        {
            ConnectionClose = sourceHeaders.ConnectionClose;
        }
    }
}

```

##### 2.5.3 http request headers

```c#
public sealed class HttpRequestHeaders : HttpHeaders
{    
    /* header value collection slots */
    
    // special collection     
    private object[]? _specialCollectionsSlots;
    private const int NumCollectionsSlots = 8;
    private T GetSpecializedCollection<T>(int slot, Func<HttpRequestHeaders, T> creationFunc)
    {
        // 8 properties each lazily allocate a collection to store the value(s) for that property.
        // Rather than having a field for each of these, store them untyped in an array that's lazily
        // allocated.  Then we only pay for the 64 bytes for those fields when any is actually accessed.
        _specialCollectionsSlots ??= new object[NumCollectionsSlots];
        return (T)(_specialCollectionsSlots[slot] ??= creationFunc(this)!);
    }
    
    // accept
    private const int AcceptSlot = 0;
    public HttpHeaderValueCollection<MediaTypeWithQualityHeaderValue> Accept =>
        GetSpecializedCollection(
        	AcceptSlot, 
        	static thisRef => 
        		new HttpHeaderValueCollection<MediaTypeWithQualityHeaderValue>(
                    KnownHeaders.Accept.Descriptor, thisRef));
    
    // accept charset
    private const int AcceptCharsetSlot = 1;
    public HttpHeaderValueCollection<StringWithQualityHeaderValue> AcceptCharset =>
        GetSpecializedCollection(
        	AcceptCharsetSlot, 
        	static thisRef => 
        		new HttpHeaderValueCollection<StringWithQualityHeaderValue>(
                    KnownHeaders.AcceptCharset.Descriptor, thisRef));
    
    
    // accept encoding
    private const int AcceptEncodingSlot = 2;
    public HttpHeaderValueCollection<StringWithQualityHeaderValue> AcceptEncoding =>
        GetSpecializedCollection(
        	AcceptEncodingSlot, 
        	static thisRef => 
        		new HttpHeaderValueCollection<StringWithQualityHeaderValue>(
                    KnownHeaders.AcceptEncoding.Descriptor, thisRef));
    
    // accept language    
    private const int AcceptLanguageSlot = 3;
    public HttpHeaderValueCollection<StringWithQualityHeaderValue> AcceptLanguage =>
        GetSpecializedCollection(
        	AcceptLanguageSlot, 
        	static thisRef => 
        		new HttpHeaderValueCollection<StringWithQualityHeaderValue>(
                    KnownHeaders.AcceptLanguage.Descriptor, thisRef));
        
    // if match
    private const int IfMatchSlot = 4;
    public HttpHeaderValueCollection<EntityTagHeaderValue> IfMatch =>
        GetSpecializedCollection(
        	IfMatchSlot, 
        	static thisRef => new HttpHeaderValueCollection<EntityTagHeaderValue>(
                KnownHeaders.IfMatch.Descriptor, thisRef));
    
    // if none match
    private const int IfNoneMatchSlot = 5;
    public HttpHeaderValueCollection<EntityTagHeaderValue> IfNoneMatch =>
        GetSpecializedCollection(
        	IfNoneMatchSlot, 
        	static thisRef => new HttpHeaderValueCollection<EntityTagHeaderValue>(
                KnownHeaders.IfNoneMatch.Descriptor, thisRef));
    
    // transfer encoding
    private const int TransferEncodingSlot = 6;
    public HttpHeaderValueCollection<TransferCodingWithQualityHeaderValue> TE =>
        GetSpecializedCollection(
        	TransferEncodingSlot, 
        	static thisRef => new HttpHeaderValueCollection<TransferCodingWithQualityHeaderValue>(
                KnownHeaders.TE.Descriptor, thisRef));
    
    // user agent
    private const int UserAgentSlot = 7;
    public HttpHeaderValueCollection<ProductInfoHeaderValue> UserAgent =>
        GetSpecializedCollection(
        	UserAgentSlot, 
        	static thisRef => new HttpHeaderValueCollection<ProductInfoHeaderValue>(
                KnownHeaders.UserAgent.Descriptor, thisRef));
    
    // expect
    private HttpHeaderValueCollection<NameValueWithParametersHeaderValue>? _expect;
    
    private HttpHeaderValueCollection<NameValueWithParametersHeaderValue> ExpectCore =>
        _expect ??= new HttpHeaderValueCollection<NameValueWithParametersHeaderValue>(
        	KnownHeaders.Expect.Descriptor, this, HeaderUtilities.ExpectContinue);
    
    public HttpHeaderValueCollection<NameValueWithParametersHeaderValue> Expect
    {
        get { return ExpectCore; }
    }
            
    /* region Request Headers */

    // authorization
    public AuthenticationHeaderValue? Authorization
    {
        get 
        {
            return (AuthenticationHeaderValue?)GetParsedValues(KnownHeaders.Authorization.Descriptor); 
        }
        set 
        {
            SetOrRemoveParsedValue(KnownHeaders.Authorization.Descriptor, value); 
        }
    }
    
    // expect continue    
    private bool _expectContinueSet;
    
    public bool? ExpectContinue
    {
        get
        {
            // ExpectCore will force the collection into existence, so avoid accessing it if possible.
            if (_expectContinueSet || 
                ContainsParsedValue(KnownHeaders.Expect.Descriptor, HeaderUtilities.ExpectContinue))
            {
                if (ExpectCore.IsSpecialValueSet)
                {
                    return true;
                }
                if (_expectContinueSet)
                {
                    return false;
                }
            }
            
            return null;
        }
        set
        {
            if (value == true)
            {
                _expectContinueSet = true;
                ExpectCore.SetSpecialValue();
            }
            else
            {
                _expectContinueSet = value != null;
                ExpectCore.RemoveSpecialValue();
            }
        }
    }
    
    // from
    public string? From
    {
        get 
        { 
            return (string?)GetParsedValues(KnownHeaders.From.Descriptor); 
        }
        set
        {
            // Null and empty string are equivalent. 
            // In this case it means, remove the From header value (if any).
            if (value == string.Empty)
            {
                value = null;
            }
            
            SetOrRemoveParsedValue(KnownHeaders.From.Descriptor, value);
        }
    }
    
    // host
    public string? Host
    {
        get 
        {
            return (string?)GetParsedValues(KnownHeaders.Host.Descriptor); 
        }
        set
        {
            // Null and empty string are equivalent. 
            // In this case it means, remove the Host header value (if any).
            if (value == string.Empty)
            {
                value = null;
            }
            
            if ((value != null) && 
                (HttpRuleParser.GetHostLength(value, 0, false, out string? _) != value.Length))
            {
                throw new FormatException(SR.net_http_headers_invalid_host_header);
            }
            
            SetOrRemoveParsedValue(KnownHeaders.Host.Descriptor, value);
        }
    }
    
    // if modified since    
    public DateTimeOffset? IfModifiedSince
    {
        get { return HeaderUtilities.GetDateTimeOffsetValue(KnownHeaders.IfModifiedSince.Descriptor, this); }
        set { SetOrRemoveParsedValue(KnownHeaders.IfModifiedSince.Descriptor, value); }
    }
    
    // if range    
    public RangeConditionHeaderValue? IfRange
    {
        get { return (RangeConditionHeaderValue?)GetParsedValues(KnownHeaders.IfRange.Descriptor); }
        set { SetOrRemoveParsedValue(KnownHeaders.IfRange.Descriptor, value); }
    }
    
    // if unmodified since
    public DateTimeOffset? IfUnmodifiedSince
    {
        get { return HeaderUtilities.GetDateTimeOffsetValue(KnownHeaders.IfUnmodifiedSince.Descriptor, this); }
        set { SetOrRemoveParsedValue(KnownHeaders.IfUnmodifiedSince.Descriptor, value); }
    }
    
    // max forwards
    public int? MaxForwards
    {
        get
        {
            object? storedValue = GetParsedValues(KnownHeaders.MaxForwards.Descriptor);
            if (storedValue != null)
            {
                return (int)storedValue;
            }
            return null;
        }
        set { SetOrRemoveParsedValue(KnownHeaders.MaxForwards.Descriptor, value); }
    }
    
    // proxy authroization
    public AuthenticationHeaderValue? ProxyAuthorization
    {
        get { return (AuthenticationHeaderValue?)GetParsedValues(KnownHeaders.ProxyAuthorization.Descriptor); }
        set { SetOrRemoveParsedValue(KnownHeaders.ProxyAuthorization.Descriptor, value); }
    }
    
    // range
    public RangeHeaderValue? Range
    {
        get { return (RangeHeaderValue?)GetParsedValues(KnownHeaders.Range.Descriptor); }
        set { SetOrRemoveParsedValue(KnownHeaders.Range.Descriptor, value); }
    }
    
    // referrer
    public Uri? Referrer
    {
        get { return (Uri?)GetParsedValues(KnownHeaders.Referer.Descriptor); }
        set { SetOrRemoveParsedValue(KnownHeaders.Referer.Descriptor, value); }
    }
    
    /* General Headers */
    
    // general headers
    private HttpGeneralHeaders? _generalHeaders;
    private HttpGeneralHeaders GeneralHeaders => 
        _generalHeaders ?? (_generalHeaders = new HttpGeneralHeaders(this));
    
    // cache control
    public CacheControlHeaderValue? CacheControl
    {
        get { return GeneralHeaders.CacheControl; }
        set { GeneralHeaders.CacheControl = value; }
    }
    
    // connection
    public HttpHeaderValueCollection<string> Connection
    {
        get { return GeneralHeaders.Connection; }
    }
    
    // connection close
    public bool? ConnectionClose
    {
        // special-cased to avoid forcing _generalHeaders initialization
        get { return HttpGeneralHeaders.GetConnectionClose(this, _generalHeaders); } 
        set { GeneralHeaders.ConnectionClose = value; }
    }
    
    // date
    public DateTimeOffset? Date
    {
        get { return GeneralHeaders.Date; }
        set { GeneralHeaders.Date = value; }
    }
    
    // pragma
    public HttpHeaderValueCollection<NameValueHeaderValue> Pragma
    {
        get { return GeneralHeaders.Pragma; }
    }
    
    // trailer
    public HttpHeaderValueCollection<string> Trailer
    {
        get { return GeneralHeaders.Trailer; }
    }
    
    // transfer encoding
    public HttpHeaderValueCollection<TransferCodingHeaderValue> TransferEncoding
    {
        get { return GeneralHeaders.TransferEncoding; }
    }
    
    // transfer encoding chunked
    public bool? TransferEncodingChunked
    {
        // special-cased to avoid forcing _generalHeaders initialization
        get { return HttpGeneralHeaders.GetTransferEncodingChunked(this, _generalHeaders); } 
        set { GeneralHeaders.TransferEncodingChunked = value; }
    }
    
    // upgrade
    public HttpHeaderValueCollection<ProductHeaderValue> Upgrade
    {
        get { return GeneralHeaders.Upgrade; }
    }
    
    // via
    public HttpHeaderValueCollection<ViaHeaderValue> Via
    {
        get { return GeneralHeaders.Via; }
    }
    
    // warning
    public HttpHeaderValueCollection<WarningHeaderValue> Warning
    {
        get { return GeneralHeaders.Warning; }
    }
    
    // ctor
    internal HttpRequestHeaders() : 
    	base(
            HttpHeaderType.General | HttpHeaderType.Request | HttpHeaderType.Custom, 
            HttpHeaderType.Response)
    {
    }
    
    // 重写方法- add headers
    internal override void AddHeaders(HttpHeaders sourceHeaders)
    {
        base.AddHeaders(sourceHeaders);
        HttpRequestHeaders? sourceRequestHeaders = sourceHeaders as HttpRequestHeaders;
        Debug.Assert(sourceRequestHeaders != null);
        
        // Copy special values but do not overwrite.
        if (sourceRequestHeaders._generalHeaders != null)
        {
            GeneralHeaders.AddSpecialsFrom(sourceRequestHeaders._generalHeaders);
        }
        
        bool? expectContinue = ExpectContinue;
        if (!expectContinue.HasValue)
        {
            ExpectContinue = sourceRequestHeaders.ExpectContinue;
        }
    }        
}

```

##### 2.5.4 http response headers

```c#
public sealed class HttpResponseHeaders : HttpHeaders
{
    /* header collection slot */
    
    private object[]? _specialCollectionsSlots;
    private const int NumCollectionsSlots = 5;                        
    private T GetSpecializedCollection<T>(int slot, Func<HttpResponseHeaders, T> creationFunc)
    {
        // 5 properties each lazily allocate a collection to store the value(s) for that property.
        // Rather than having a field for each of these, store them untyped in an array that's lazily
        // allocated.  Then we only pay for the 45 bytes for those fields when any is actually accessed.
        object[] collections = _specialCollectionsSlots ?? (_specialCollectionsSlots = new object[NumCollectionsSlots]);
        object result = collections[slot];
        if (result == null)
        {
            collections[slot] = result = creationFunc(this)!;
        }
        return (T)result;
    }
    
    // accept range
    private const int AcceptRangesSlot = 0;
    public HttpHeaderValueCollection<string> AcceptRanges =>
        GetSpecializedCollection(
        	AcceptRangesSlot, 
        	static thisRef => new HttpHeaderValueCollection<string>(
                KnownHeaders.AcceptRanges.Descriptor, thisRef, HeaderUtilities.TokenValidator));
    
    // proxy authenticate
    private const int ProxyAuthenticateSlot = 1;
    public HttpHeaderValueCollection<AuthenticationHeaderValue> ProxyAuthenticate =>
        GetSpecializedCollection(
        	ProxyAuthenticateSlot, 
        	static thisRef => new HttpHeaderValueCollection<AuthenticationHeaderValue>(
                KnownHeaders.ProxyAuthenticate.Descriptor, thisRef));
    
    // server
    private const int ServerSlot = 2;
    public HttpHeaderValueCollection<ProductInfoHeaderValue> Server =>
        GetSpecializedCollection(
        	ServerSlot, 
        	static thisRef => new HttpHeaderValueCollection<ProductInfoHeaderValue>(
                KnownHeaders.Server.Descriptor, thisRef));
    
    // vary
    private const int VarySlot = 3;
    public HttpHeaderValueCollection<string> Vary =>
        GetSpecializedCollection(
        	VarySlot, 
        	static thisRef => new HttpHeaderValueCollection<string>(
                KnownHeaders.Vary.Descriptor, thisRef, HeaderUtilities.TokenValidator));
    
    // www authenticate
    private const int WwwAuthenticateSlot = 4;
    public HttpHeaderValueCollection<AuthenticationHeaderValue> WwwAuthenticate =>
        GetSpecializedCollection(
        	WwwAuthenticateSlot, 
        	static thisRef => new HttpHeaderValueCollection<AuthenticationHeaderValue>(
                KnownHeaders.WWWAuthenticate.Descriptor, thisRef));
    
    
   
    
    
   
    
    //region Response Headers

    
    
    
    // age
    public TimeSpan? Age
    {
        get { return HeaderUtilities.GetTimeSpanValue(KnownHeaders.Age.Descriptor, this); }
        set { SetOrRemoveParsedValue(KnownHeaders.Age.Descriptor, value); }
    }
    
    // etag
    public EntityTagHeaderValue? ETag
    {
        get { return (EntityTagHeaderValue?)GetParsedValues(KnownHeaders.ETag.Descriptor); }
        set { SetOrRemoveParsedValue(KnownHeaders.ETag.Descriptor, value); }
    }
    
    // location
    public Uri? Location
    {
        get { return (Uri?)GetParsedValues(KnownHeaders.Location.Descriptor); }
        set { SetOrRemoveParsedValue(KnownHeaders.Location.Descriptor, value); }
    }
    
    // retry after
    public RetryConditionHeaderValue? RetryAfter
    {
        get { return (RetryConditionHeaderValue?)GetParsedValues(KnownHeaders.RetryAfter.Descriptor); }
        set { SetOrRemoveParsedValue(KnownHeaders.RetryAfter.Descriptor, value); }
    }
    
    
    
   
    
    
    
    

    /* region General Headers */
    
    // general headers
    private HttpGeneralHeaders? _generalHeaders;
    private HttpGeneralHeaders GeneralHeaders => _generalHeaders ?? (_generalHeaders = new HttpGeneralHeaders(this));   
    
    // cache control
    public CacheControlHeaderValue? CacheControl
    {
        get { return GeneralHeaders.CacheControl; }
        set { GeneralHeaders.CacheControl = value; }
    }
    
    // connection
    public HttpHeaderValueCollection<string> Connection
    {
        get { return GeneralHeaders.Connection; }
    }
    
    // connection close
    public bool? ConnectionClose
    {            
        // special-cased to avoid forcing _generalHeaders initialization
        get { return HttpGeneralHeaders.GetConnectionClose(this, _generalHeaders); } 
        set { GeneralHeaders.ConnectionClose = value; }
    }
    
    // date
    public DateTimeOffset? Date
    {
        get { return GeneralHeaders.Date; }
        set { GeneralHeaders.Date = value; }
    }
    
    // pragma
    public HttpHeaderValueCollection<NameValueHeaderValue> Pragma
    {
        get { return GeneralHeaders.Pragma; }
    }
    
    // trailer
    public HttpHeaderValueCollection<string> Trailer
    {
        get { return GeneralHeaders.Trailer; }
    }
    
    // transfer encoding
    public HttpHeaderValueCollection<TransferCodingHeaderValue> TransferEncoding
    {
        get { return GeneralHeaders.TransferEncoding; }
    }
    
    // transfer encoding chunked
    public bool? TransferEncodingChunked
    {
        // special-cased to avoid forcing _generalHeaders initialization
        get { return HttpGeneralHeaders.GetTransferEncodingChunked(this, _generalHeaders); } 
        set { GeneralHeaders.TransferEncodingChunked = value; }
    }
    
    // upgrade
    public HttpHeaderValueCollection<ProductHeaderValue> Upgrade
    {
        get { return GeneralHeaders.Upgrade; }
    }
    
    // via
    public HttpHeaderValueCollection<ViaHeaderValue> Via
    {
        get { return GeneralHeaders.Via; }
    }
    
    // warning
    public HttpHeaderValueCollection<WarningHeaderValue> Warning
    {
        get { return GeneralHeaders.Warning; }
    }
    
    
    // ctor
    private bool _containsTrailingHeaders;
    internal bool ContainsTrailingHeaders => _containsTrailingHeaders;
    
    internal HttpResponseHeaders(bool containsTrailingHeaders = false) : 
    	base(
            containsTrailingHeaders 
            	? HttpHeaderType.All ^ HttpHeaderType.Request 
            	: HttpHeaderType.General | HttpHeaderType.Response | HttpHeaderType.Custom,
            HttpHeaderType.Request)
    {
        _containsTrailingHeaders = containsTrailingHeaders;
    }
    
    // 重写方法- add headers    
    internal override void AddHeaders(HttpHeaders sourceHeaders)
    {
        base.AddHeaders(sourceHeaders);
        HttpResponseHeaders? sourceResponseHeaders = sourceHeaders as HttpResponseHeaders;
        Debug.Assert(sourceResponseHeaders != null);
        
        // Copy special values, but do not overwrite
        if (sourceResponseHeaders._generalHeaders != null)
        {
            GeneralHeaders.AddSpecialsFrom(sourceResponseHeaders._generalHeaders);
        }
    }
    
    // 重写方法- is allowed header name
    internal override bool IsAllowedHeaderName(HeaderDescriptor descriptor)
    {
        if (!_containsTrailingHeaders)
            return true;
        
        KnownHeader? knownHeader = KnownHeaders.TryGetKnownHeader(descriptor.Name);
        if (knownHeader == null)
            return true;
        
        return (knownHeader.HeaderType & HttpHeaderType.NonTrailing) == 0;
    }        
}

```

##### 2.5.5 http content headers

```c#
public sealed class HttpContentHeaders : HttpHeaders
{        
    // content length
    private bool _contentLengthSet;
    public long? ContentLength
    {
        get
        {            
            object? storedValue = GetParsedValues(KnownHeaders.ContentLength.Descriptor);
            
            // Only try to calculate the length if the user didn't set the value explicitly using the setter.
            if (!_contentLengthSet && (storedValue == null))
            {
                // If we don't have a value for Content-Length in the store, try to let the content calculate it's length. 
                // If the content object is able to calculate the length, we'll store it in the store.
                long? calculatedLength = _parent.GetComputedOrBufferLength();
                
                if (calculatedLength != null)
                {
                    SetParsedValue(
                        KnownHeaders.ContentLength.Descriptor, 
                        (object)calculatedLength.Value);
                }
                
                return calculatedLength;
            }
            
            if (storedValue == null)
            {
                return null;
            }
            else
            {
                return (long)storedValue;
            }
        }
        set
        {
            SetOrRemoveParsedValue(KnownHeaders.ContentLength.Descriptor, value); 
            _contentLengthSet = true;
        }
    }
    
    // allow    
    private HttpHeaderValueCollection<string>? _allow;
    public ICollection<string> Allow
    {
        get
        {
            if (_allow == null)
            {
                _allow = new HttpHeaderValueCollection<string>(
                    KnownHeaders.Allow.Descriptor,
                    this, HeaderUtilities.TokenValidator);
            }
            return _allow;
        }
    }
    
    // conten encoding
    private HttpHeaderValueCollection<string>? _contentEncoding;
    // Must be a collection (and not provide properties like "GZip", "Deflate", etc.) since the order matters!
    public ICollection<string> ContentEncoding
    {
        get
        {
            if (_contentEncoding == null)
            {
                _contentEncoding = new HttpHeaderValueCollection<string>(
                    KnownHeaders.ContentEncoding.Descriptor,
                    this, HeaderUtilities.TokenValidator);
            }
            return _contentEncoding;
        }
    }
    
    // content language
    private HttpHeaderValueCollection<string>? _contentLanguage;
    public ICollection<string> ContentLanguage
    {
        get
        {
            if (_contentLanguage == null)
            {
                _contentLanguage = new HttpHeaderValueCollection<string>(
                    KnownHeaders.ContentLanguage.Descriptor,
                    this, HeaderUtilities.TokenValidator);
            }
            return _contentLanguage;
        }
    }
    
    // content disposition    
    public ContentDispositionHeaderValue? ContentDisposition
    {
        get { return (ContentDispositionHeaderValue?)GetParsedValues(KnownHeaders.ContentDisposition.Descriptor); }
        set { SetOrRemoveParsedValue(KnownHeaders.ContentDisposition.Descriptor, value); }
    }
    
    // content location                    
    public Uri? ContentLocation
    {
        get { return (Uri?)GetParsedValues(KnownHeaders.ContentLocation.Descriptor); }
        set { SetOrRemoveParsedValue(KnownHeaders.ContentLocation.Descriptor, value); }
    }
    
    // content md5
    public byte[]? ContentMD5
    {
        get { return (byte[]?)GetParsedValues(KnownHeaders.ContentMD5.Descriptor); }
        set { SetOrRemoveParsedValue(KnownHeaders.ContentMD5.Descriptor, value); }
    }
    
    // content range
    public ContentRangeHeaderValue? ContentRange
    {
        get { return (ContentRangeHeaderValue?)GetParsedValues(KnownHeaders.ContentRange.Descriptor); }
        set { SetOrRemoveParsedValue(KnownHeaders.ContentRange.Descriptor, value); }
    }
    
    // content type
    public MediaTypeHeaderValue? ContentType
    {
        get { return (MediaTypeHeaderValue?)GetParsedValues(KnownHeaders.ContentType.Descriptor); }
        set { SetOrRemoveParsedValue(KnownHeaders.ContentType.Descriptor, value); }
    }
    
    // expires
    public DateTimeOffset? Expires
    {
        get { return HeaderUtilities.GetDateTimeOffsetValue(KnownHeaders.Expires.Descriptor, this, DateTimeOffset.MinValue); }
        set { SetOrRemoveParsedValue(KnownHeaders.Expires.Descriptor, value); }
    }
    
    // last modified
    public DateTimeOffset? LastModified
    {
        get { return HeaderUtilities.GetDateTimeOffsetValue(KnownHeaders.LastModified.Descriptor, this); }
        set { SetOrRemoveParsedValue(KnownHeaders.LastModified.Descriptor, value); }
    }
    
    // ctor
    private readonly HttpContent _parent;
    
    internal HttpContentHeaders(HttpContent parent) : 
    	base(
            HttpHeaderType.Content | HttpHeaderType.Custom, 
            HttpHeaderType.None)   
    {
        parent = parent;        
    }
}

```

### 3. http

#### 3.1 http version policy

```c#
public enum HttpVersionPolicy
{    
    RequestVersionOrLower,    
    RequestVersionOrHigher,    
    RequestVersionExact
}

```

#### 3.2 http method

```c#
public partial class HttpMethod : IEquatable<HttpMethod>
{
    private readonly string _method;
    public string Method
    {
        get { return _method; }
    }
        
    private readonly int? _http3Index;        
    
    // get
    private static readonly HttpMethod s_getMethod = new HttpMethod(
        "GET", http3StaticTableIndex: H3StaticTable.MethodGet);
    public static HttpMethod Get
    {
        get { return s_getMethod; }
    }
    
    // put
    private static readonly HttpMethod s_putMethod = new HttpMethod(
        "PUT", http3StaticTableIndex: H3StaticTable.MethodPut);
    public static HttpMethod Put
    {
        get { return s_putMethod; }
    }
    
    // post
    private static readonly HttpMethod s_postMethod = new HttpMethod(
        "POST", http3StaticTableIndex: H3StaticTable.MethodPost);
    public static HttpMethod Post
    {
        get { return s_postMethod; }
    }
    
    // delete
    private static readonly HttpMethod s_deleteMethod = new HttpMethod(
        "DELETE", http3StaticTableIndex: H3StaticTable.MethodDelete);
    public static HttpMethod Delete
    {
        get { return s_deleteMethod; }
    }
    
    // head
    private static readonly HttpMethod s_headMethod = new HttpMethod(
        "HEAD", http3StaticTableIndex: H3StaticTable.MethodHead);
    public static HttpMethod Head
    {
        get { return s_headMethod; }
    }
    private static readonly HttpMethod s_optionsMethod = new HttpMethod(
        "OPTIONS", http3StaticTableIndex: H3StaticTable.MethodOptions);
    public static HttpMethod Options
    {
        get { return s_optionsMethod; }
    }
    
    // trace
    private static readonly HttpMethod s_traceMethod = new HttpMethod(
        "TRACE", -1);
    public static HttpMethod Trace
    {
        get { return s_traceMethod; }
    }
    
    // patch
    private static readonly HttpMethod s_patchMethod = new HttpMethod(
        "PATCH", -1);
    public static HttpMethod Patch
    {
        get { return s_patchMethod; }
    }
    
    // connect
    private static readonly HttpMethod s_connectMethod = new HttpMethod(
        "CONNECT", http3StaticTableIndex: H3StaticTable.MethodConnect);
    
    internal static HttpMethod Connect
    {
        get { return s_connectMethod; }
    }
                                                                    
    // ctor
    public HttpMethod(string method)
    {
        if (string.IsNullOrEmpty(method))
        {
            throw new ArgumentException(SR.net_http_argument_empty_string, nameof(method));
        }
        if (HttpRuleParser.GetTokenLength(method, 0) != method.Length)
        {
            throw new FormatException(SR.net_http_httpmethod_format_error);
        }
        
        _method = method;
    }
    
    private HttpMethod(string method, int http3StaticTableIndex)
    {
        _method = method;
        _http3Index = http3StaticTableIndex;
    }       
        
    public bool Equals([NotNullWhen(true)] HttpMethod? other)
    {
        if (other is null)
        {
            return false;
        }
        
        if (object.ReferenceEquals(_method, other._method))
        {
            // Strings are static, so there is a good chance that two equal methods use the same reference
            // (unless they differ in case).
            return true;
        }
        
        return string.Equals(_method, other._method, StringComparison.OrdinalIgnoreCase);
    }
            
    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return Equals(obj as HttpMethod);
    }
    
    private int _hashcode;
    public override int GetHashCode()
    {
        if (_hashcode == 0)
        {
            _hashcode = StringComparer.OrdinalIgnoreCase.GetHashCode(_method);
        }
        
        return _hashcode;
    }
    
    public override string ToString()
    {
        return _method;
    }
    
    public static bool operator ==(HttpMethod? left, HttpMethod? right)
    {
        return left is null || right is null 
            ? ReferenceEquals(left, right) 
            : left.Equals(right);
    }
    
    public static bool operator !=(HttpMethod? left, HttpMethod? right)
    {
        return !(left == right);
    }
                
    internal bool MustHaveRequestBody
    {
        get
        {            
            Debug.Assert(ReferenceEquals(this, Normalize(this)));
            
            return !ReferenceEquals(this, HttpMethod.Get) && 
                !ReferenceEquals(this, HttpMethod.Head) && 
                !ReferenceEquals(this, HttpMethod.Connect) &&
                !ReferenceEquals(this, HttpMethod.Options) && 
                !ReferenceEquals(this, HttpMethod.Delete);
        }
    }
    
    internal static HttpMethod Normalize(HttpMethod method)
    {
        Debug.Assert(method != null);
        Debug.Assert(!string.IsNullOrEmpty(method._method));
        
        // _http3Index is only set for the singleton instances, so if it's not null, we can avoid the lookup.  
        // Otherwise, look up the method instance and return the normalized instance if it's found.
        
        if (method._http3Index is null && 
            method._method.Length >= 3) // 3 == smallest known method
        {
            HttpMethod? match = (method._method[0] | 0x20) switch
            	{
                    'c' => s_connectMethod,
                    'd' => s_deleteMethod,
                    'g' => s_getMethod,
                    'h' => s_headMethod,
                    'o' => s_optionsMethod,
                    'p' => method._method.Length switch
                	    {
                            3 => s_putMethod,
                            4 => s_postMethod,
                            _ => s_patchMethod,
                    	},
                    't' => s_traceMethod,
                    _ => null,
            	};
            
            if (match is not null && 
                string.Equals(method._method, match._method, StringComparison.OrdinalIgnoreCase))
            {
                return match;
            }
        }
        
        return method;
    }
}

public partial class HttpMethod
{
    private byte[]? _http3EncodedBytes;
    
    internal byte[] Http3EncodedBytes
    {
        get
        {
            byte[]? http3EncodedBytes = Volatile.Read(ref _http3EncodedBytes);
            if (http3EncodedBytes is null)
            {
                Volatile.Write(
                    ref _http3EncodedBytes, 
                    http3EncodedBytes = _http3Index is int index && index >= 0 
                    	? QPackEncoder.EncodeStaticIndexedHeaderFieldToArray(index) 
                    	: QPackEncoder.EncodeLiteralHeaderFieldWithStaticNameReferenceToArray(
                              H3StaticTable.MethodGet, 
                              _method));
            }
            
            return http3EncodedBytes;
        }
    }
}

```

### 4. http content



### 5. http  message

#### 5.1 http request message

```c#
public class HttpRequestMessage : IDisposable
{
    private const int MessageNotYetSent = 0;
    private const int MessageAlreadySent = 1;
    
    // Track whether the message has been sent.
    // The message shouldn't be sent again if this field is equal to MessageAlreadySent.
    private int _sendStatus = MessageNotYetSent;
    internal bool WasSentByHttpClient() => _sendStatus == MessageAlreadySent;
    
    internal bool MarkAsSent()
    {
        return Interlocked.Exchange(ref _sendStatus, MessageAlreadySent) == MessageNotYetSent;
    }
    
    // version                               
    private Version _version;
    public Version Version
    {
        get { return _version; }
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            
            CheckDisposed();            
            _version = value;
        }
    }
    
    // version policy
    private HttpVersionPolicy _versionPolicy;   
    public HttpVersionPolicy VersionPolicy
    {
        get { return _versionPolicy; }
        set
        {
            CheckDisposed();            
            _versionPolicy = value;
        }
    }
    
    // method
    private HttpMethod _method;
    public HttpMethod Method
    {
        get { return _method; }
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            CheckDisposed();
            
            _method = value;
        }
    }
    
    // request uri
    private Uri? _requestUri;
    public Uri? RequestUri
    {
        get { return _requestUri; }
        set
        {
            if ((value != null) && 
                (value.IsAbsoluteUri) && 
                (!HttpUtilities.IsHttpUri(value)))
            {
                throw new ArgumentException(HttpUtilities.InvalidUriMessage, nameof(value));
            }
            CheckDisposed();
            
            // It's OK to set 'null'. HttpClient will add the 'BaseAddress'. 
            // If there is no 'BaseAddress' sending this message will throw.
            _requestUri = value;
        }
    }
    
    // request headers
    private HttpRequestHeaders? _headers;
    public HttpRequestHeaders Headers
    {
        get
        {
            if (_headers == null)
            {
                _headers = new HttpRequestHeaders();
            }
            return _headers;
        }
    }
    
    // content 
    private HttpContent? _content;
    public HttpContent? Content
    {
        get { return _content; }
        set
        {
            CheckDisposed();
            
            if (NetEventSource.Log.IsEnabled())
            {
                if (value == null)
                {
                    NetEventSource.ContentNull(this);
                }
                else
                {
                    NetEventSource.Associate(this, value);
                }
            }
            
            // It's OK to set a 'null' content, even if the method is POST/PUT.
            _content = value;
        }
    }
    
    // request options
    private HttpRequestOptions? _options;
    public HttpRequestOptions Options => _options ??= new HttpRequestOptions();
        
    internal bool HasHeaders => _headers != null;                 
    
    private bool _disposed;
    private void CheckDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(this.GetType().ToString());
        }
    }
    
    
    // ctor
    public HttpRequestMessage() : this(HttpMethod.Get, (Uri?)null)
    {
    }
    
    public HttpRequestMessage(HttpMethod method, Uri? requestUri)
    {
        InitializeValues(method, requestUri);
    }
    
    public HttpRequestMessage(HttpMethod method, string? requestUri)
    {
        // It's OK to have a 'null' request Uri. If HttpClient is used, the 'BaseAddress' will be added.
        // If there is no 'BaseAddress', sending this request message will throw.
        // Note that we also allow the string to be empty: null and empty are considered equivalent.
        if (string.IsNullOrEmpty(requestUri))
        {
            InitializeValues(method, null);
        }
        else
        {
            InitializeValues(method, new Uri(requestUri, UriKind.RelativeOrAbsolute));
        }
    }
    
    [MemberNotNull(nameof(_method))]
    [MemberNotNull(nameof(_version))]
    private void InitializeValues(HttpMethod method, Uri? requestUri)
    {
        if (method is null)
        {
            throw new ArgumentNullException(nameof(method));
        }
        if ((requestUri != null) && (requestUri.IsAbsoluteUri) && (!HttpUtilities.IsHttpUri(requestUri)))
        {
            throw new ArgumentException(HttpUtilities.InvalidUriMessage, nameof(requestUri));
        }
        
        // 注入 method
        _method = method;
        // 注入 uri
        _requestUri = requestUri;
        // 使用 default request version
        _version = HttpUtilities.DefaultRequestVersion;
        // 使用 default version policy
        _versionPolicy = HttpUtilities.DefaultVersionPolicy;
    }
    
    
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        
        sb.Append("Method: ");
        sb.Append(_method);
        
        sb.Append(", RequestUri: '");
        sb.Append(_requestUri == null ? "<null>" : _requestUri.ToString());
        
        sb.Append("', Version: ");
        sb.Append(_version);
        
        sb.Append(", Content: ");
        sb.Append(_content == null ? "<null>" : _content.GetType().ToString());
        
        sb.AppendLine(", Headers:");
        HeaderUtilities.DumpHeaders(sb, _headers, _content?.Headers);
        
        return sb.ToString();
    }                
                
    // dispose    
    protected virtual void Dispose(bool disposing)
    {
        // The reason for this type to implement IDisposable is that it contains instances of types that implement
        // IDisposable (content).
        if (disposing && !_disposed)
        {
            _disposed = true;
            if (_content != null)
            {
                _content.Dispose();
            }
        }
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }                
}

```

##### 5.1.1 http request options

```c#
public sealed class HttpRequestOptions : IDictionary<string, object?>
{
    // options 容器
    private Dictionary<string, object?> Options { get; } = new Dictionary<string, object?>();
    
    /* dictionary 属性 */
    ICollection<string> IDictionary<string, object?>.Keys => Options.Keys;
    ICollection<object?> IDictionary<string, object?>.Values => Options.Values;
    int ICollection<KeyValuePair<string, object?>>.Count => Options.Count;
    bool ICollection<KeyValuePair<string, object?>>.IsReadOnly => ((IDictionary<string, object?>)Options).IsReadOnly;
    
    /* dictionary 方法 */
    void IDictionary<string, object?>.Add(string key, object? value) => 
        Options.Add(key, value);
    void ICollection<KeyValuePair<string, object?>>.Add(KeyValuePair<string, object?> item) => 
        ((IDictionary<string, object?>)Options).Add(item);
    
    void ICollection<KeyValuePair<string, object?>>.Clear() => 
        Options.Clear();
    
    bool IDictionary<string, object?>.Remove(string key) => 
        Options.Remove(key);
    bool ICollection<KeyValuePair<string, object?>>.Remove(KeyValuePair<string, object?> item) => 
        ((IDictionary<string, object?>)Options).Remove(item);
    
    bool ICollection<KeyValuePair<string, object?>>.Contains(KeyValuePair<string, object?> item) => 
        ((IDictionary<string, object?>)Options).Contains(item);
    bool IDictionary<string, object?>.ContainsKey(string key) => 
        Options.ContainsKey(key);
    
    bool IDictionary<string, object?>.TryGetValue(string key, out object? value) => 
        Options.TryGetValue(key, out value);
    
    void ICollection<KeyValuePair<string, object?>>.CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex) =>
            ((IDictionary<string, object?>)Options).CopyTo(array, arrayIndex);
    
    IEnumerator<KeyValuePair<string, object?>> IEnumerable<KeyValuePair<string, object?>>.GetEnumerator() => 
        Options.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => 
        ((System.Collections.IEnumerable)Options).GetEnumerator();
                
    object? IDictionary<string, object?>.this[string key]
    {
        get
        {
            return Options[key];
        }
        set
        {
            Options[key] = value;
        }
    }
    
    // 方法- get
    public bool TryGetValue<TValue>(HttpRequestOptionsKey<TValue> key, [MaybeNullWhen(false)] out TValue value)
    {
        if (Options.TryGetValue(key.Key, out object? _value) && _value is TValue tvalue)
        {
            value = tvalue;
            return true;
        }
        
        value = default(TValue);
        return false;
    }
    
    // 方法- set
    public void Set<TValue>(HttpRequestOptionsKey<TValue> key, TValue value)
    {
        Options[key.Key] = value;
    }
}

public readonly struct HttpRequestOptionsKey<TValue>
{
    public string Key { get; }
    public HttpRequestOptionsKey(string key)
    {
        Key = key;
    }
}

```

##### 5.1.2 http utilites

```c#
internal static partial class HttpUtilities
{
    // 1- supported non secure scheme
    internal static bool IsSupportedNonSecureScheme(string scheme) =>
        string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase) || 
        IsNonSecureWebSocketScheme(scheme);
    
    internal static string InvalidUriMessage => SR.net_http_client_http_baseaddress_required;
}

internal static partial class HttpUtilities
{
    internal static Version DefaultRequestVersion => HttpVersion.Version11;    
    internal static Version DefaultResponseVersion => HttpVersion.Version11;
    
    internal static HttpVersionPolicy DefaultVersionPolicy => HttpVersionPolicy.RequestVersionOrLower;
    
    internal static bool IsHttpUri(Uri uri)
    {
        Debug.Assert(uri != null);
        return IsSupportedScheme(uri.Scheme);
    }
    
    // a- is supported scheme
    internal static bool IsSupportedScheme(string scheme) =>
        IsSupportedNonSecureScheme(scheme) ||
        IsSupportedSecureScheme(scheme);
    
    // 1- support non secure scheme in .anyos
    	
    internal static bool IsNonSecureWebSocketScheme(string scheme) =>
        string.Equals(scheme, "ws", StringComparison.OrdinalIgnoreCase);
    
   // 2- supported secure scheme
    internal static bool IsSupportedSecureScheme(string scheme) =>
        string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase) || 
        IsSecureWebSocketScheme(scheme);
    	
    internal static bool IsSecureWebSocketScheme(string scheme) =>
        string.Equals(scheme, "wss", StringComparison.OrdinalIgnoreCase);
    
    // b- is supported proxy scheme
    internal static bool IsSupportedProxyScheme(string scheme) =>
        string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase) || 
        IsSocksScheme(scheme);
        
    internal static bool IsSocksScheme(string scheme) =>
        string.Equals(scheme, "socks5", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(scheme, "socks4a", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(scheme, "socks4", StringComparison.OrdinalIgnoreCase);
                                           
    // Always specify TaskScheduler.Default to prevent us from using a user defined TaskScheduler.Current.
    //
    // Since we're not doing any CPU and/or I/O intensive operations, continue on the same thread.
    // This results in better performance since the continuation task doesn't get scheduled by the scheduler and there are 
    // no context switches required.
    internal static Task ContinueWithStandard<T>(
        this Task<T> task, 
        object state, 
        Action<Task<T>, object?> continuation)
    {
        return task.ContinueWith(
            continuation, 
            state, 
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}

```

#### 5.2 http response message?

```c#
public class HttpResponseMessage : IDisposable
{
    /* dispose */
    private bool _disposed;
    
    private void CheckDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(this.GetType().ToString());
        }
    }
    
    protected virtual void Dispose(bool disposing)
    {
        // The reason for this type to implement IDisposable is that it contains instances of types that 
        // implement IDisposable (content).
        if (disposing && !_disposed)
        {
            _disposed = true;
            if (_content != null)
            {
                _content.Dispose();
            }
        }
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
                   
    // version
    private Version _version;
    
    public Version Version
    {
        get { return _version; }
        set
        {
#if !PHONE
    	if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
#endif
    	CheckDisposed();            
        _version = value;
        }
    }
    
    internal void SetVersionWithoutValidation(Version value) => _version = value;
    
    // status code
    private const HttpStatusCode defaultStatusCode = HttpStatusCode.OK;
    
    private HttpStatusCode _statusCode;
    
    public HttpStatusCode StatusCode
    {
        get { return _statusCode; }
        set
        {
            if (((int)value < 0) || ((int)value > 999))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            CheckDisposed();
            
            _statusCode = value;
        }
    }
    
    internal void SetStatusCodeWithoutValidation(HttpStatusCode value) => _statusCode = value;
    
    public bool IsSuccessStatusCode
    {
        get { return ((int)_statusCode >= 200) && ((int)_statusCode <= 299); }
    }
    
    // reason phrase
    private string? _reasonPhrase;
    
    public string? ReasonPhrase
    {
        get
        {
            if (_reasonPhrase != null)
            {
                return _reasonPhrase;
            }
            // Provide a default if one was not set.
            return HttpStatusDescription.Get(StatusCode);
        }
        set
        {
            if ((value != null) && ContainsNewLineCharacter(value))
            {
                throw new FormatException(SR.net_http_reasonphrase_format_error);
            }
            CheckDisposed();
            
            _reasonPhrase = value; // It's OK to have a 'null' reason phrase.
        }
    }
    
    internal void SetReasonPhraseWithoutValidation(string value) => _reasonPhrase = value;
    
    
    // headers
    private HttpResponseHeaders? _headers;
    public HttpResponseHeaders Headers => _headers ??= new HttpResponseHeaders();
    
    // trailing headers
    private HttpResponseHeaders? _trailingHeaders;
    public HttpResponseHeaders TrailingHeaders => _trailingHeaders ??= new HttpResponseHeaders(containsTrailingHeaders: true);
                
    // In the common/desired case where response.TrailingHeaders isn't accessed until after the whole payload has been
    // received, "_trailingHeaders" will still be null, and we can simply store the supplied instance into "_trailingHeaders" 
    // and assume ownership of the instance. 
    // In the uncommon case where it was accessed, we add all of the headers to the existing instance.   
    internal void StoreReceivedTrailingHeaders(HttpResponseHeaders headers)
    {
        Debug.Assert(headers.ContainsTrailingHeaders);
        
        if (_trailingHeaders is null)
        {
            _trailingHeaders = headers;
        }
        else
        {
            _trailingHeaders.AddHeaders(headers);
        }
    }
    
    // content
    private HttpContent? _content;
    
    [AllowNull]
    public HttpContent Content
    {
        get { return _content ??= new EmptyContent(); }
        set
        {
            CheckDisposed();
            
            if (NetEventSource.Log.IsEnabled())
            {
                if (value == null)
                {
                    NetEventSource.ContentNull(this);
                }
                else
                {
                    NetEventSource.Associate(this, value);
                }
            }
            
            _content = value;
        }
    }
    
    // request message                          
    private HttpRequestMessage? _requestMessage;
    
    public HttpRequestMessage? RequestMessage
    {
        get { return _requestMessage; }
        set
        {
            CheckDisposed();
            if (value is not null && NetEventSource.Log.IsEnabled()) NetEventSource.Associate(this, value);
            _requestMessage = value;
        }
    }
    
    
    // ctor
    public HttpResponseMessage() : this(defaultStatusCode)
    {
    }
    
    public HttpResponseMessage(HttpStatusCode statusCode)
    {
        if (((int)statusCode < 0) || ((int)statusCode > 999))
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode));
        }
        
        _statusCode = statusCode;
        _version = HttpUtilities.DefaultResponseVersion;
    }
    
    public HttpResponseMessage EnsureSuccessStatusCode()
    {
        if (!IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                SR.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    SR.net_http_message_not_success_statuscode,
                    (int)_statusCode,
                    ReasonPhrase),
                inner: null,
                _statusCode);
        }
        
        return this;
    }
    
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        
        sb.Append("StatusCode: ");
        sb.Append((int)_statusCode);
        
        sb.Append(", ReasonPhrase: '");
        sb.Append(ReasonPhrase ?? "<null>");
        
        sb.Append("', Version: ");
        sb.Append(_version);
        
        sb.Append(", Content: ");
        sb.Append(_content == null ? "<null>" : _content.GetType().ToString());
        
        sb.AppendLine(", Headers:");
        HeaderUtilities.DumpHeaders(sb, _headers, _content?.Headers);
        
        if (_trailingHeaders != null)
        {
            sb.AppendLine(", Trailing Headers:");
            HeaderUtilities.DumpHeaders(sb, _trailingHeaders);
        }
        
        return sb.ToString();
    }
    
    private bool ContainsNewLineCharacter(string value)
    {
        foreach (char character in value)
        {
            if ((character == HttpRuleParser.CR) || (character == HttpRuleParser.LF))
            {
                return true;
            }
        }
        return false;
    }    
}

```

#### 5.3 http message handler

```c#
public abstract class HttpMessageHandler : IDisposable
{
    protected HttpMessageHandler()
    {
        if (NetEventSource.Log.IsEnabled()) NetEventSource.Info(this);
    }
    
    // 方法- send
    protected internal virtual HttpResponseMessage Send(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException(
            SR.Format(
                SR.net_http_missing_sync_implementation, 
                GetType(), 
                nameof(HttpMessageHandler), 
                nameof(Send)));
    }
    
    // 方法- send async
    protected internal abstract Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken);
    
    // dispose           
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        // Nothing to do in base class.
    }
}

```

##### 5.3.1 delegating handler

```c#
public abstract class DelegatingHandler : HttpMessageHandler
{
    /* operation stared flag */
    
    // This flags the handler instances as "active". 
    // I.e. we executed at least one request (or are in the process of doing so). 
    // This information is used to lock-down all property setters. Once a Send/SendAsync operation started, no property can be changed.
    private volatile bool _operationStarted;
    
    private void SetOperationStarted()
    {
        CheckDisposed();
        
        if (_innerHandler == null)
        {
            throw new InvalidOperationException(SR.net_http_handler_not_assigned);
        }
        
        if (!_operationStarted)
        {
            _operationStarted = true;
        }
    }
    
    private void CheckDisposedOrStarted()
    {
        CheckDisposed();
        
        if (_operationStarted)
        {
            throw new InvalidOperationException(SR.net_http_operation_started);
        }
    }
    
    /* check disposed */
    
    private volatile bool _disposed;
    
    private void CheckDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().ToString());
        }
    }
    
    // inner handler
    private HttpMessageHandler? _innerHandler;
    [DisallowNull]
    public HttpMessageHandler? InnerHandler
    {
        get
        {
            return _innerHandler;
        }
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            
            CheckDisposedOrStarted();
            
            if (NetEventSource.Log.IsEnabled()) NetEventSource.Associate(this, value);
            _innerHandler = value;
        }
    }
        
    // ctor
    protected DelegatingHandler()
    {
    }
    
    protected DelegatingHandler(HttpMessageHandler innerHandler)
    {
        InnerHandler = innerHandler;
    }
    
    // 重写方法- 使用 inner handler 的 send 方法
    protected internal override HttpResponseMessage Send(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request), SR.net_http_handler_norequest);
        }
        
        SetOperationStarted();
        return _innerHandler!.Send(request, cancellationToken);
    }
    
    // 重写方法- 使用 inner handler 的 send async 方法
    protected internal override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request), SR.net_http_handler_norequest);
        }
        
        SetOperationStarted();
        return _innerHandler!.SendAsync(request, cancellationToken);
    }
        
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            if (_innerHandler != null)
            {
                _innerHandler.Dispose();
            }
        }
        
        base.Dispose(disposing);
    }                       
}

```

###### 5.3.1.1 message processing handler

```c#
public abstract class MessageProcessingHandler : DelegatingHandler
{
    protected MessageProcessingHandler()
    {
    }
    
    protected MessageProcessingHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }
    
    // process request
    protected abstract HttpRequestMessage ProcessRequest(
        HttpRequestMessage request,
        CancellationToken cancellationToken);
    
    // process response
    protected abstract HttpResponseMessage ProcessResponse(
        HttpResponseMessage response,
        CancellationToken cancellationToken);
    
    // send
    protected internal sealed override HttpResponseMessage Send(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request), SR.net_http_handler_norequest);
        }
               
        HttpRequestMessage newRequestMessage = ProcessRequest(request, cancellationToken);
        // 调用 base.send 方法
        HttpResponseMessage response = base.Send(newRequestMessage, cancellationToken);
        HttpResponseMessage newResponseMessage = ProcessResponse(response, cancellationToken);
        
        return newResponseMessage;
    }
    
    // send async
    protected internal sealed override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request), SR.net_http_handler_norequest);
        }
        
        // ProcessRequest() and ProcessResponse() are supposed to be fast, so we call ProcessRequest() on the same
        // thread SendAsync() was invoked to avoid context switches. However, if ProcessRequest() throws, we have
        // to catch the exception since the caller doesn't expect exceptions when calling SendAsync(): The
        // expectation is that the returned task will get faulted on errors, but the async call to SendAsync()
        // should complete.
        
        var tcs = new SendState(this, cancellationToken);
        try
        {
            HttpRequestMessage newRequestMessage = ProcessRequest(request, cancellationToken);
            // 调用 base.send async 方法
            Task<HttpResponseMessage> sendAsyncTask = base.SendAsync(newRequestMessage, cancellationToken);
            
            // We schedule a continuation task once the inner handler completes in order to trigger the response
            // processing method. ProcessResponse() is only called if the task wasn't canceled before.
            sendAsyncTask.ContinueWithStandard(
                tcs, 
                static (task, state) =>
                {
                    var sendState = (SendState)state!;
                    MessageProcessingHandler self = sendState._handler;
                    CancellationToken token = sendState._token;
                    
                    if (task.IsFaulted)
                    {
                        sendState.TrySetException(task.Exception!.GetBaseException());
                        return;
                    }
                    
                    if (task.IsCanceled)
                    {
                        sendState.TrySetCanceled(token);
                        return;
                    }
                    
                    if (task.Result == null)
                    {
                        sendState.TrySetException(
                            ExceptionDispatchInfo.SetCurrentStackTrace(
                                new InvalidOperationException(SR.net_http_handler_noresponse)));
                        return;
                    }
                    
                    try
                    {
                        HttpResponseMessage responseMessage = self.ProcessResponse(task.Result, token);
                        sendState.TrySetResult(responseMessage);
                    }
                    catch (OperationCanceledException e)
                    {
                        // If ProcessResponse() throws an OperationCanceledException check whether it is related to
                        // the cancellation token we received from the user. If so, cancel the Task.
                        HandleCanceledOperations(token, sendState, e);
                    }
                    catch (Exception e)
                    {
                        sendState.TrySetException(e);
                    }
                    // We don't pass the cancellation token to the continuation task, since we want to get called even
                    // if the operation was canceled: We'll set the Task returned to the user to canceled. Passing the
                    // cancellation token here would result in the continuation task to not be called at all. I.e. we
                    // would never complete the task returned to the caller of SendAsync().
                });
        }
        catch (OperationCanceledException e)
        {
            HandleCanceledOperations(cancellationToken, tcs, e);
        }
        catch (Exception e)
        {
            tcs.TrySetException(e);
        }
        
        return tcs.Task;
    }
    
    private static void HandleCanceledOperations(
        CancellationToken cancellationToken,
        TaskCompletionSource<HttpResponseMessage> tcs, 
        OperationCanceledException e)
    {
        // Check if the exception was due to a cancellation. If so, check if the OperationCanceledException is
        // related to our CancellationToken. If it was indeed caused due to our cancellation token being
        // canceled, set the Task as canceled. Set it to faulted otherwise, since the OperationCanceledException
        // is not related to our cancellation token.
        if (cancellationToken.IsCancellationRequested && (e.CancellationToken == cancellationToken))
        {
            tcs.TrySetCanceled(cancellationToken);
        }
        else
        {
            tcs.TrySetException(e);
        }
    }
    
    // Private class used to capture the SendAsync state in a closure, while simultaneously avoiding a tuple allocation.
    private sealed class SendState : TaskCompletionSource<HttpResponseMessage>
    {
        internal readonly MessageProcessingHandler _handler;
        internal readonly CancellationToken _token;
        
        public SendState(MessageProcessingHandler handler, CancellationToken token)
        {
            Debug.Assert(handler != null);
            
            _handler = handler;
            _token = token;
        }
    }
}

```

###### 5.3.1.2 diagnostics handler

```c#
internal sealed class DiagnosticsHandler : DelegatingHandler
{    
    public DiagnosticsHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }
    
    internal static bool IsEnabled()
    {
        // check if there is a parent Activity (and propagation is not suppressed)
        // or if someone listens to HttpHandlerDiagnosticListener
        return IsGloballyEnabled() && 
            (Activity.Current != null || Settings.s_diagnosticListener.IsEnabled());
    }
    
    internal static bool IsGloballyEnabled()
    {
        return Settings.s_activityPropagationEnabled;
    }
    
    // 方法- send
    protected internal override HttpResponseMessage Send(
        HttpRequestMessage request, 
        CancellationToken cancellationToken) =>
        	SendAsyncCore(request, async: false, cancellationToken)
        		.AsTask()
        		.GetAwaiter()
        		.GetResult();
    
    // 方法- send async
    protected internal override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken) =>
        	SendAsyncCore(request, async: true, cancellationToken).AsTask();

    // send async core
    private async ValueTask<HttpResponseMessage> SendAsyncCore(
        HttpRequestMessage request, 
        bool async,
        CancellationToken cancellationToken)
    {
        // HttpClientHandler is responsible to call static DiagnosticsHandler.IsEnabled() before forwarding request here.
        // It will check if propagation is on (because parent Activity exists or there is a listener) or off (forcibly disabled)
        // This code won't be called unless consumer unsubscribes from DiagnosticListener right after the check.
        // So some requests happening right after subscription starts might not be instrumented. Similarly, when consumer unsubscribes, 
        // extra requests might be instrumented
        
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request), SR.net_http_handler_norequest);
        }
        
        Activity? activity = null;
        DiagnosticListener diagnosticListener = Settings.s_diagnosticListener;
        
        // if there is no listener, but propagation is enabled (with previous IsEnabled() check)
        // do not write any events just start/stop Activity and propagate Ids
        if (!diagnosticListener.IsEnabled())
        {
            activity = new Activity(DiagnosticsHandlerLoggingStrings.ActivityName);
            activity.Start();
            InjectHeaders(activity, request);
            
            try
            {
                return async 
                    ? await base.SendAsync(request, cancellationToken).ConfigureAwait(false) 
                    : base.Send(request, cancellationToken);
            }
            finally
            {
                activity.Stop();
            }
        }
        
        Guid loggingRequestId = Guid.Empty;
        
        // There is a listener. Check if listener wants to be notified about HttpClient Activities
        if (diagnosticListener.IsEnabled(DiagnosticsHandlerLoggingStrings.ActivityName, request))
        {
            activity = new Activity(DiagnosticsHandlerLoggingStrings.ActivityName);
            
            // Only send start event to users who subscribed for it, but start activity anyway
            if (diagnosticListener.IsEnabled(DiagnosticsHandlerLoggingStrings.ActivityStartName))
            {
                StartActivity(diagnosticListener, activity, new ActivityStartData(request));
            }
            else
            {
                activity.Start();
            }
        }
        
        // try to write System.Net.Http.Request event (deprecated)
        if (diagnosticListener.IsEnabled(DiagnosticsHandlerLoggingStrings.RequestWriteNameDeprecated))
        {
            long timestamp = Stopwatch.GetTimestamp();
            loggingRequestId = Guid.NewGuid();
            Write(
                diagnosticListener, 
                DiagnosticsHandlerLoggingStrings.RequestWriteNameDeprecated,
                new RequestData(request, loggingRequestId, timestamp));
        }
        
        // If we are on at all, we propagate current activity information
        Activity? currentActivity = Activity.Current;
        if (currentActivity != null)
        {
            InjectHeaders(currentActivity, request);
        }
        
        HttpResponseMessage? response = null;
        TaskStatus taskStatus = TaskStatus.RanToCompletion;
        try
        {
            response = async 
                ? await base.SendAsync(request, cancellationToken).ConfigureAwait(false) 
                : base.Send(request, cancellationToken);
            
            return response;
        }
        catch (OperationCanceledException)
        {
            taskStatus = TaskStatus.Canceled;
            
            // we'll report task status in HttpRequestOut.Stop
            throw;
        }
        catch (Exception ex)
        {
            taskStatus = TaskStatus.Faulted;
            
            if (diagnosticListener.IsEnabled(DiagnosticsHandlerLoggingStrings.ExceptionEventName))
            {
                // If request was initially instrumented, Activity.Current has all necessary context for logging
                // Request is passed to provide some context if instrumentation was disabled and to avoid
                // extensive Activity.Tags usage to tunnel request properties
                Write(
                    diagnosticListener, 
                    DiagnosticsHandlerLoggingStrings.ExceptionEventName, 
                    new ExceptionData(ex, request));
            }
            throw;
        }
        finally
        {
            // always stop activity if it was started
            if (activity != null)
            {
                StopActivity(
                    diagnosticListener, 
                    activity, 
                    new ActivityStopData(
                        response,
                        // If request is failed or cancelled, there is no response, therefore no information about request;
                        // pass the request in the payload, so consumers can have it in Stop for failed/canceled requests
                        // and not retain all requests in Start
                        request,
                        taskStatus));
            }
            // Try to write System.Net.Http.Response event (deprecated)
            if (diagnosticListener.IsEnabled(DiagnosticsHandlerLoggingStrings.ResponseWriteNameDeprecated))
            {
                long timestamp = Stopwatch.GetTimestamp();
                Write(
                    diagnosticListener, 
                    DiagnosticsHandlerLoggingStrings.ResponseWriteNameDeprecated,
                    new ResponseData(
                        response,
                        loggingRequestId,
                        timestamp,
                        taskStatus));
            }
        }
    }

        

        
        
        

        

    private static void InjectHeaders(Activity currentActivity, HttpRequestMessage request)
    {
        if (currentActivity.IdFormat == ActivityIdFormat.W3C)
        {
            if (!request.Headers.Contains(DiagnosticsHandlerLoggingStrings.TraceParentHeaderName))
            {
                request.Headers.TryAddWithoutValidation(
                    DiagnosticsHandlerLoggingStrings.TraceParentHeaderName,
                    currentActivity.Id);
                
                if (currentActivity.TraceStateString != null)
                {
                    request.Headers.TryAddWithoutValidation(
                        DiagnosticsHandlerLoggingStrings.TraceStateHeaderName, 
                        currentActivity.TraceStateString);
                }
            }
        }
        else
        {
            if (!request.Headers.Contains(DiagnosticsHandlerLoggingStrings.RequestIdHeaderName))
            {
                request.Headers.TryAddWithoutValidation(
                    DiagnosticsHandlerLoggingStrings.RequestIdHeaderName, 
                    currentActivity.Id);
            }
        }
        
        // we expect baggage to be empty or contain a few items
        using (IEnumerator<KeyValuePair<string, string?>> e = currentActivity.Baggage.GetEnumerator())
        {
            if (e.MoveNext())
            {
                var baggage = new List<string>();
                do
                {
                    KeyValuePair<string, string?> item = e.Current;
                    baggage.Add(
                        new NameValueHeaderValue(
                            WebUtility.UrlEncode(item.Key), 
                            WebUtility.UrlEncode(item.Value)).ToString());         
                }
                while (e.MoveNext());
                request.Headers.TryAddWithoutValidation(
                    DiagnosticsHandlerLoggingStrings.CorrelationContextHeaderName, 
                    baggage);
            }
        }
    }
    
    [UnconditionalSuppressMessage(
        "ReflectionAnalysis", 
        "IL2026:UnrecognizedReflectionPattern",
        Justification = "The values being passed into Write have the commonly used properties being preserved with DynamicDependency.")]
    private static void Write<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        DiagnosticSource diagnosticSource,
        string name,
        T value)
    {
        diagnosticSource.Write(name, value);
    }
    
    [UnconditionalSuppressMessage(
        "ReflectionAnalysis", 
        "IL2026:UnrecognizedReflectionPattern",
        Justification = "The args being passed into StartActivity have the commonly used properties being preserved with DynamicDependency.")]
    private static Activity StartActivity<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        DiagnosticSource diagnosticSource,
        Activity activity,
        T? args)
    {
        return diagnosticSource.StartActivity(activity, args);
    }
    
    [UnconditionalSuppressMessage(
        "ReflectionAnalysis", 
        "IL2026:UnrecognizedReflectionPattern",
        Justification = "The args being passed into StopActivity have the commonly used properties being preserved with DynamicDependency.")]
    private static void StopActivity<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        DiagnosticSource diagnosticSource,
        Activity activity,
        T? args)
    {
        diagnosticSource.StopActivity(activity, args);
    }        
}

```

###### - activity data

```c#
internal sealed class DiagnosticsHandler : DelegatingHandler
{   
    private sealed class ActivityStartData
    {
        public HttpRequestMessage Request { get; }
        
        [DynamicDependency(nameof(HttpRequestMessage.RequestUri), typeof(HttpRequestMessage))]
        [DynamicDependency(nameof(HttpRequestMessage.Method), typeof(HttpRequestMessage))]
        [DynamicDependency(nameof(HttpRequestMessage.RequestUri), typeof(HttpRequestMessage))]
        [DynamicDependency(nameof(Uri.Host), typeof(Uri))]
        [DynamicDependency(nameof(Uri.Port), typeof(Uri))]
        internal ActivityStartData(HttpRequestMessage request)
        {
            Request = request;
        }
        
        public override string ToString() => $"{{ {nameof(Request)} = {Request} }}";
    }
    
    private sealed class ActivityStopData
    {
        public HttpResponseMessage? Response { get; }
        public HttpRequestMessage Request { get; }
        public TaskStatus RequestTaskStatus { get; }
        
        internal ActivityStopData(
            HttpResponseMessage? response, 
            HttpRequestMessage request, 
            TaskStatus requestTaskStatus)
        {
            Response = response;
            Request = request;
            RequestTaskStatus = requestTaskStatus;
        }
                        
        public override string ToString() => 
            $"{{ {nameof(Response)} = {Response}, 
        		"{nameof(Request)} = {Request}, 
        		"{nameof(RequestTaskStatus)} = {RequestTaskStatus} }}";
    }
}

```

###### - exception data

```c#
internal sealed class DiagnosticsHandler : DelegatingHandler
{ 
    private sealed class ExceptionData
    {
        public Exception Exception { get; }
        public HttpRequestMessage Request { get; }
        
        // preserve the same properties as ActivityStartData above + common Exception properties
        [DynamicDependency(nameof(HttpRequestMessage.RequestUri), typeof(HttpRequestMessage))]
        [DynamicDependency(nameof(HttpRequestMessage.Method), typeof(HttpRequestMessage))]
        [DynamicDependency(nameof(HttpRequestMessage.RequestUri), typeof(HttpRequestMessage))]
        [DynamicDependency(nameof(Uri.Host), typeof(Uri))]
        [DynamicDependency(nameof(Uri.Port), typeof(Uri))]
        [DynamicDependency(nameof(System.Exception.Message), typeof(Exception))]
        [DynamicDependency(nameof(System.Exception.StackTrace), typeof(Exception))]
        internal ExceptionData(Exception exception, HttpRequestMessage request)
        {
            Exception = exception;
            Request = request;
        }
                
        public override string ToString() => 
            $"{{ {nameof(Exception)} = {Exception}, {nameof(Request)} = {Request} }}";
    }
}

```

###### - request & response data

```c#
internal sealed class DiagnosticsHandler : DelegatingHandler
{ 
    private sealed class RequestData
    {
        public HttpRequestMessage Request { get; }
        public Guid LoggingRequestId { get; }
        public long Timestamp { get; }
        
        // preserve the same properties as ActivityStartData above
        [DynamicDependency(nameof(HttpRequestMessage.RequestUri), typeof(HttpRequestMessage))]
        [DynamicDependency(nameof(HttpRequestMessage.Method), typeof(HttpRequestMessage))]
        [DynamicDependency(nameof(HttpRequestMessage.RequestUri), typeof(HttpRequestMessage))]
        [DynamicDependency(nameof(Uri.Host), typeof(Uri))]
        [DynamicDependency(nameof(Uri.Port), typeof(Uri))]
        internal RequestData(HttpRequestMessage request, Guid loggingRequestId, long timestamp)
        {
            Request = request;
            LoggingRequestId = loggingRequestId;
            Timestamp = timestamp;
        }
                        
        public override string ToString() => 
            $"{{ {nameof(Request)} = {Request}, 
	       		"{nameof(LoggingRequestId)} = {LoggingRequestId}, 
	  	       	"nameof(Timestamp)} = {Timestamp} }}";
    }

    private sealed class ResponseData
    {
        public HttpResponseMessage? Response { get; }
        public Guid LoggingRequestId { get; }
        public long Timestamp { get; }
        public TaskStatus RequestTaskStatus { get; }
        
        [DynamicDependency(nameof(HttpResponseMessage.StatusCode), typeof(HttpResponseMessage))]
        internal ResponseData(
            HttpResponseMessage? response, 
            Guid loggingRequestId, 
            long timestamp, 
            TaskStatus requestTaskStatus)
        {
            Response = response;
            LoggingRequestId = loggingRequestId;
            Timestamp = timestamp;
            RequestTaskStatus = requestTaskStatus;
        }
                        
        public override string ToString() => 
            $"{{ {nameof(Response)} = {Response}, 
        		"{nameof(LoggingRequestId)} = {LoggingRequestId}, 
	          	"nameof(Timestamp)} = {Timestamp}, 
		   		"{nameof(RequestTaskStatus)} = {RequestTaskStatus} }}";
    }
}

```

###### - settings

```c#
internal sealed class DiagnosticsHandler : DelegatingHandler
{ 
    private static class Settings
    {
        private const string EnableActivityPropagationEnvironmentVariableSettingName = 
            "DOTNET_SYSTEM_NET_HTTP_ENABLEACTIVITYPROPAGATION";
        private const string EnableActivityPropagationAppCtxSettingName = 
            "System.Net.Http.EnableActivityPropagation";
        
        public static readonly bool s_activityPropagationEnabled = GetEnableActivityPropagationValue();
        
        private static bool GetEnableActivityPropagationValue()
        {
            // First check for the AppContext switch, giving it priority over the environment variable.
            if (AppContext.TryGetSwitch(EnableActivityPropagationAppCtxSettingName, out bool enableActivityPropagation))
            {
                return enableActivityPropagation;
            }
            
            // AppContext switch wasn't used. Check the environment variable to determine which handler should be used.
            string? envVar = Environment.GetEnvironmentVariable(EnableActivityPropagationEnvironmentVariableSettingName);
            if (envVar != null && (envVar.Equals("false", StringComparison.OrdinalIgnoreCase) || envVar.Equals("0")))
            {
                // Suppress Activity propagation.
                return false;
            }
            
            // Defaults to enabling Activity propagation.
            return true;
        }
        
        public static readonly DiagnosticListener s_diagnosticListener =
            new DiagnosticListener(DiagnosticsHandlerLoggingStrings.DiagnosticListenerName);
    }
}

```

###### 5.3.1.3 logging handler

```c#
public class LoggingHttpMessageHandler : DelegatingHandler
{
    private ILogger _logger;
    private readonly HttpClientFactoryOptions _options;
    
    private static readonly Func<string, bool> _shouldNotRedactHeaderValue = (header) => false;
    
    public LoggingHttpMessageHandler(ILogger logger)
    {
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }
        
        _logger = logger;
    }
    
    public LoggingHttpMessageHandler(ILogger logger, HttpClientFactoryOptions options)
    {
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }
        
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        _logger = logger;
        _options = options;
    }
    
    // 重写方法- send async
    protected async override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        
        Func<string, bool> shouldRedactHeaderValue = _options?.ShouldRedactHeaderValue ?? _shouldNotRedactHeaderValue;
        
        // Not using a scope here because we always expect this to be at the end of the pipeline, 
        // thus there's not really anything to surround.
        Log.RequestStart(_logger, request, shouldRedactHeaderValue);
        var stopwatch = ValueStopwatch.StartNew();
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        Log.RequestEnd(_logger, response, stopwatch.GetElapsedTime(), shouldRedactHeaderValue);
        
        return response;
    }
    
    // Used in tests.
    internal static class Log
    {
        public static class EventIds
        {
            public static readonly EventId RequestStart = new EventId(100, "RequestStart");
            public static readonly EventId RequestEnd = new EventId(101, "RequestEnd");            
            public static readonly EventId RequestHeader = new EventId(102, "RequestHeader");
            public static readonly EventId ResponseHeader = new EventId(103, "ResponseHeader");
        }
        
        private static readonly Action<ILogger, HttpMethod, Uri, Exception> _requestStart = 
            LoggerMessage.Define<HttpMethod, Uri>(
	            LogLevel.Information,
    	        EventIds.RequestStart,
        	    "Sending HTTP request {HttpMethod} {Uri}");
        
        private static readonly Action<ILogger, double, int, Exception> _requestEnd = 
            LoggerMessage.Define<double, int>(
	            LogLevel.Information,
    	        EventIds.RequestEnd,
                "Received HTTP response headers after {ElapsedMilliseconds}ms - {StatusCode}");

        public static void RequestStart(
            ILogger logger, 
            HttpRequestMessage request, 
            Func<string, bool> shouldRedactHeaderValue)
        {
            _requestStart(logger, request.Method, request.RequestUri, null);
            
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.Log(
                    LogLevel.Trace,
                    EventIds.RequestHeader,
                    new HttpHeadersLogValue(
                        HttpHeadersLogValue.Kind.Request, 
                        request.Headers, 
                        request.Content?.Headers, 
                        shouldRedactHeaderValue),
                    null,
                    (state, ex) => state.ToString());
            }
        }
        
        public static void RequestEnd(
            ILogger logger, 
            HttpResponseMessage response,
            TimeSpan duration,
            Func<string, bool> shouldRedactHeaderValue)
        {
            _requestEnd(logger, duration.TotalMilliseconds, (int)response.StatusCode, null);
            
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.Log(
                    LogLevel.Trace,
                    EventIds.ResponseHeader,
                    new HttpHeadersLogValue(
                        HttpHeadersLogValue.Kind.Response, 
                        response.Headers, 
                        response.Content?.Headers, 
                        shouldRedactHeaderValue),
                    null,
                    (state, ex) => state.ToString());
            }
        }
    }
}

```

###### 5.3.1.4 logging scoped handler

```c#
public class LoggingScopeHttpMessageHandler : DelegatingHandler
{
    private ILogger _logger;
    private readonly HttpClientFactoryOptions _options;
    
    private static readonly Func<string, bool> _shouldNotRedactHeaderValue = (header) => false;
    
    public LoggingScopeHttpMessageHandler(ILogger logger)
    {
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }
        
        _logger = logger;
    }
    
    public LoggingScopeHttpMessageHandler(ILogger logger, HttpClientFactoryOptions options)
    {
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }
        
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        
        _logger = logger;
        _options = options;
    }
    
    protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        
        var stopwatch = ValueStopwatch.StartNew();
        
        Func<string, bool> shouldRedactHeaderValue = _options?.ShouldRedactHeaderValue ?? _shouldNotRedactHeaderValue;
        
        using (Log.BeginRequestPipelineScope(_logger, request))
        {
            Log.RequestPipelineStart(_logger, request, shouldRedactHeaderValue);
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            Log.RequestPipelineEnd(_logger, response, stopwatch.GetElapsedTime(), shouldRedactHeaderValue);
            
            return response;
        }
    }
    
    // Used in tests
    internal static class Log
    {
        public static class EventIds
        {
            public static readonly EventId PipelineStart = new EventId(100, "RequestPipelineStart");
            public static readonly EventId PipelineEnd = new EventId(101, "RequestPipelineEnd");            
            public static readonly EventId RequestHeader = new EventId(102, "RequestPipelineRequestHeader");
            public static readonly EventId ResponseHeader = new EventId(103, "RequestPipelineResponseHeader");
        }
        
        private static readonly Func<ILogger, HttpMethod, Uri, IDisposable> _beginRequestPipelineScope = 
            LoggerMessage.DefineScope<HttpMethod, Uri>(
            	"HTTP {HttpMethod} {Uri}");
        
        private static readonly Action<ILogger, HttpMethod, Uri, Exception> _requestPipelineStart = 
            LoggerMessage.Define<HttpMethod, Uri>(
                LogLevel.Information,
                EventIds.PipelineStart,
                "Start processing HTTP request {HttpMethod} {Uri}");

        private static readonly Action<ILogger, double, int, Exception> _requestPipelineEnd = 
            LoggerMessage.Define<double, int>(
                LogLevel.Information,
                EventIds.PipelineEnd,
                "End processing HTTP request after {ElapsedMilliseconds}ms - {StatusCode}");

        public static IDisposable BeginRequestPipelineScope(ILogger logger, HttpRequestMessage request)
        {
            return _beginRequestPipelineScope(logger, request.Method, request.RequestUri);
        }
        
        public static void RequestPipelineStart(
            ILogger logger, 
            HttpRequestMessage request, 
            Func<string, bool> shouldRedactHeaderValue)
        {
            _requestPipelineStart(logger, request.Method, request.RequestUri, null);
            
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.Log(
                    LogLevel.Trace,
                    EventIds.RequestHeader,
                    new HttpHeadersLogValue(
                        HttpHeadersLogValue.Kind.Request, 
                        request.Headers, 
                        request.Content?.Headers, 
                        shouldRedactHeaderValue),
                    null,
                    (state, ex) => state.ToString());
            }
        }
        
        public static void RequestPipelineEnd(
            ILogger logger, 
            HttpResponseMessage response, 
            TimeSpan duration, 
            Func<string, bool> shouldRedactHeaderValue)
        {
            _requestPipelineEnd(logger, duration.TotalMilliseconds, (int)response.StatusCode, null);
            
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.Log(
                    LogLevel.Trace,
                    EventIds.ResponseHeader,
                    new HttpHeadersLogValue(
                        HttpHeadersLogValue.Kind.Response, 
                        response.Headers, 
                        response.Content?.Headers, 
                        shouldRedactHeaderValue),
                    null,
                    (state, ex) => state.ToString());
            }
        }
    }
}

```



##### 5.3.2 http client handler

```c#
#if TARGET_BROWSER
using HttpHandlerType = System.Net.Http.BrowserHttpHandler;
#else
using HttpHandlerType = System.Net.Http.SocketsHttpHandler;
#endif

public partial class HttpClientHandler : HttpMessageHandler
{
    private readonly HttpHandlerType _underlyingHandler;
    private readonly DiagnosticsHandler? _diagnosticsHandler;
    private ClientCertificateOption _clientCertificateOptions;
           
    public HttpClientHandler()
    {
        // 根据 platform 创建 underlying handler
        _underlyingHandler = new HttpHandlerType();
        
        // 创建 diagnostics handler
        if (DiagnosticsHandler.IsGloballyEnabled())
        {
            _diagnosticsHandler = new DiagnosticsHandler(_underlyingHandler);
        }
        
        // 创建 client certificate option
        ClientCertificateOptions = ClientCertificateOption.Manual;
    }
    
    /* dispose */
    private volatile bool _disposed;
    
    private void CheckDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().ToString());
        }
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _underlyingHandler.Dispose();
        }
        
        base.Dispose(disposing);
    }
    
    /* http properties */
    
    public virtual bool SupportsAutomaticDecompression => HttpHandlerType.SupportsAutomaticDecompression;
    public virtual bool SupportsProxy => HttpHandlerType.SupportsProxy;
    public virtual bool SupportsRedirectConfiguration => HttpHandlerType.SupportsRedirectConfiguration;
    
    // use cookies
    [UnsupportedOSPlatform("browser")]
    public bool UseCookies
    {
        get => _underlyingHandler.UseCookies;
        set => _underlyingHandler.UseCookies = value;
    }
    
    // cookie container
    [UnsupportedOSPlatform("browser")]
    public CookieContainer CookieContainer
    {
        get => _underlyingHandler.CookieContainer;
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            
            _underlyingHandler.CookieContainer = value;
        }
    }
    
    // automatic decompression
    [UnsupportedOSPlatform("browser")]
    public DecompressionMethods AutomaticDecompression
    {
        get => _underlyingHandler.AutomaticDecompression;
        set => _underlyingHandler.AutomaticDecompression = value;
    }
            [UnsupportedOSPlatform("browser")]
    
    // use proxy
    public bool UseProxy
    {
        get => _underlyingHandler.UseProxy;
        set => _underlyingHandler.UseProxy = value;
    }
    
    // web proxy
    [UnsupportedOSPlatform("browser")]
    public IWebProxy? Proxy
    {
        get => _underlyingHandler.Proxy;
        set => _underlyingHandler.Proxy = value;
    }
    
    // default proxy credentials
    [UnsupportedOSPlatform("browser")]
    public ICredentials? DefaultProxyCredentials
    {
        get => _underlyingHandler.DefaultProxyCredentials;
        set => _underlyingHandler.DefaultProxyCredentials = value;
    }
    
    // pre authenticate
    [UnsupportedOSPlatform("browser")]
    public bool PreAuthenticate
    {
        get => _underlyingHandler.PreAuthenticate;
        set => _underlyingHandler.PreAuthenticate = value;
    }
    
    // use default credentials
    [UnsupportedOSPlatform("browser")]    
    public bool UseDefaultCredentials
    {
        // SocketsHttpHandler doesn't have a separate UseDefaultCredentials property.  
        // There is just a Credentials property. So, we need to map the behavior.
        get => _underlyingHandler.Credentials == CredentialCache.DefaultCredentials;
        set
        {
            if (value)
            {
                _underlyingHandler.Credentials = CredentialCache.DefaultCredentials;
            }
            else
            {
                if (_underlyingHandler.Credentials == CredentialCache.DefaultCredentials)
                {
                    // Only clear out the Credentials property if it was a DefaultCredentials.
                    _underlyingHandler.Credentials = null;
                }
            }
        }
    }
    
    // credentials
    [UnsupportedOSPlatform("browser")]
    public ICredentials? Credentials
    {
        get => _underlyingHandler.Credentials;
        set => _underlyingHandler.Credentials = value;
    }
    
    // allow auto redirect
    public bool AllowAutoRedirect
    {
        get => _underlyingHandler.AllowAutoRedirect;
        set => _underlyingHandler.AllowAutoRedirect = value;
    }
    
    // max automatic redirections
    [UnsupportedOSPlatform("browser")]
    public int MaxAutomaticRedirections
    {
        get => _underlyingHandler.MaxAutomaticRedirections;
        set => _underlyingHandler.MaxAutomaticRedirections = value;
    }
    
    // max connection perserver
    [UnsupportedOSPlatform("browser")]
    public int MaxConnectionsPerServer
    {
        get => _underlyingHandler.MaxConnectionsPerServer;
        set => _underlyingHandler.MaxConnectionsPerServer = value;
    }
    
    // max request content buffer size
    public long MaxRequestContentBufferSize
    {
        // This property is not supported. In the .NET Framework it was only used when the handler needed to
        // automatically buffer the request content. That only happened if neither 'Content-Length' nor
        // 'Transfer-Encoding: chunked' request headers were specified. So, the handler thus needed to buffer
        // in the request content to determine its length and then would choose 'Content-Length' semantics when
        // POST'ing. In .NET Core, the handler will resolve the ambiguity by always choosing
        // 'Transfer-Encoding: chunked'. The handler will never automatically buffer in the request content.
        get
        {
            // Returning zero is appropriate since in .NET Framework it means no limit.
            return 0; 
        }
        
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            
            if (value > HttpContent.MaxBufferSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), 
                    value,
                    SR.Format(
                        CultureInfo.InvariantCulture, 
                        SR.net_http_content_buffersize_limit,
                        HttpContent.MaxBufferSize));
            }
            
            CheckDisposed();
            
            // No-op on property setter.
        }
    }
    
    // max response headers length
    [UnsupportedOSPlatform("browser")]
    public int MaxResponseHeadersLength
    {
        get => _underlyingHandler.MaxResponseHeadersLength;
        set => _underlyingHandler.MaxResponseHeadersLength = value;
    }
    
    // client certificate options
    public ClientCertificateOption ClientCertificateOptions
    {
        get => _clientCertificateOptions;
        set
        {
            switch (value)
            {
                case ClientCertificateOption.Manual:
#if TARGET_BROWSER
                	_clientCertificateOptions = value;
#else
	                ThrowForModifiedManagedSslOptionsIfStarted();
    	            _clientCertificateOptions = value;
        	        _underlyingHandler.SslOptions.LocalCertificateSelectionCallback = 
            	        (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) => 	
                	    	CertificateHelper.GetEligibleClientCertificate(ClientCertificates)!;
#endif
                    break;
                    
                case ClientCertificateOption.Automatic:
#if TARGET_BROWSER
                    _clientCertificateOptions = value;
#else
                    ThrowForModifiedManagedSslOptionsIfStarted();
                    _clientCertificateOptions = value;
                    _underlyingHandler.SslOptions.LocalCertificateSelectionCallback = 
                        (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) => 
                        	CertificateHelper.GetEligibleClientCertificate()!;
#endif
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(value));
            }
        }
    }
    
    // client certificates
    [UnsupportedOSPlatform("browser")]
    public X509CertificateCollection ClientCertificates
    {
        get
        {
            if (ClientCertificateOptions != ClientCertificateOption.Manual)
            {
                throw new InvalidOperationException(
                    SR.Format(
                        SR.net_http_invalid_enable_first, 
                        nameof(ClientCertificateOptions), 
                        nameof(ClientCertificateOption.Manual)));
            }
            
            return _underlyingHandler.SslOptions.ClientCertificates ??
                (_underlyingHandler.SslOptions.ClientCertificates = new X509CertificateCollection());
        }
    }
    
    // server certificate customer validation callback
    [UnsupportedOSPlatform("browser")]
    public Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool>? ServerCertificateCustomValidationCallback
    {
#if TARGET_BROWSER
        get => throw new PlatformNotSupportedException();
        set => throw new PlatformNotSupportedException();
#else
        get => (_underlyingHandler.SslOptions.RemoteCertificateValidationCallback?.Target as 
                ConnectHelper.CertificateCallbackMapper)?.FromHttpClientHandler;
        set
        {
            ThrowForModifiedManagedSslOptionsIfStarted();
            _underlyingHandler.SslOptions.RemoteCertificateValidationCallback = 
                value != null 
                	? new ConnectHelper.CertificateCallbackMapper(value).ForSocketsHttpHandler 
                	: null;
        }
#endif
    }
    
    // chek certificate revocation list
    [UnsupportedOSPlatform("browser")]
    public bool CheckCertificateRevocationList
    {
        get => _underlyingHandler.SslOptions.CertificateRevocationCheckMode == X509RevocationMode.Online;
        set
        {
            ThrowForModifiedManagedSslOptionsIfStarted();
            _underlyingHandler.SslOptions.CertificateRevocationCheckMode = 
                value ? X509RevocationMode.Online : X509RevocationMode.NoCheck;
        }
    }
    
    // ssl protocols
    [UnsupportedOSPlatform("browser")]
    public SslProtocols SslProtocols
    {
        get => _underlyingHandler.SslOptions.EnabledSslProtocols;
        set
        {
            ThrowForModifiedManagedSslOptionsIfStarted();
            _underlyingHandler.SslOptions.EnabledSslProtocols = value;
        }
    }
    
    // properties
    public IDictionary<string, object?> Properties => _underlyingHandler.Properties;
    
    // 重写方法- send，
    // 使用 diagonstics handler 或者 underlying handler 的 send 方法
    [UnsupportedOSPlatform("browser")]
    protected internal override HttpResponseMessage Send(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return DiagnosticsHandler.IsEnabled() && _diagnosticsHandler != null 
            ? _diagnosticsHandler.Send(request, cancellationToken) 
            : _underlyingHandler.Send(request, cancellationToken);
    }
    
    // 重写方法- send async，
    // 使用 diagonstics handler 或者 underlying handler 的 send async 方法
    protected internal override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return DiagnosticsHandler.IsEnabled() && _diagnosticsHandler != null 
            ? _diagnosticsHandler.SendAsync(request, cancellationToken) 
            : _underlyingHandler.SendAsync(request, cancellationToken);
    }
    
    // lazy-load the validator func so it can be trimmed by the ILLinker if it isn't used.
    private static Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool>? 
        s_dangerousAcceptAnyServerCertificateValidator;
    
    [UnsupportedOSPlatform("browser")]
    public static Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool> 
        DangerousAcceptAnyServerCertificateValidator =>
            Volatile.Read(ref s_dangerousAcceptAnyServerCertificateValidator) ??
        	    Interlocked.CompareExchange(
        			ref s_dangerousAcceptAnyServerCertificateValidator, 
        			delegate { return true; }, null) 
        				?? s_dangerousAcceptAnyServerCertificateValidator;
    
    private void ThrowForModifiedManagedSslOptionsIfStarted()
    {
        // Hack to trigger an InvalidOperationException if a property that's stored on
        // SslOptions is changed, since SslOptions itself does not do any such checks.
        _underlyingHandler.SslOptions = _underlyingHandler.SslOptions;
    }        
}

```

###### 5.3.2.1 client certification options

```c#
public enum ClientCertificateOption
{
    Manual = 0,
    Automatic,
}

```

###### 5.3.2.2 browser handler

```c#
using JSObject = System.Runtime.InteropServices.JavaScript.JSObject;
using JSException = System.Runtime.InteropServices.JavaScript.JSException;
using HostObject = System.Runtime.InteropServices.JavaScript.HostObject;
using Uint8Array = System.Runtime.InteropServices.JavaScript.Uint8Array;
using Function = System.Runtime.InteropServices.JavaScript.Function;

internal sealed class BrowserHttpHandler : HttpMessageHandler
{
    // This partial implementation contains members common to Browser WebAssembly running on .NET Core.
    private static readonly JSObject? s_fetch = 
        (JSObject)System.Runtime.InteropServices.JavaScript.Runtime.GetGlobalObject("fetch");
    
    private static readonly JSObject? s_window = 
        (JSObject)System.Runtime.InteropServices.JavaScript.Runtime.GetGlobalObject("window");

    private static readonly HttpRequestOptionsKey<bool> EnableStreamingResponse = 
        new HttpRequestOptionsKey<bool>("WebAssemblyEnableStreamingResponse");
    
    private static readonly HttpRequestOptionsKey<IDictionary<string, object>> FetchOptions = 
        new HttpRequestOptionsKey<IDictionary<string, object>>("WebAssemblyFetchOptions");
    
    private bool _allowAutoRedirect = HttpHandlerDefaults.DefaultAutomaticRedirection;
    
    // flag to determine if the _allowAutoRedirect was explicitly set or not.
    private bool _isAllowAutoRedirectTouched;
    
        /// <summary>
        /// Gets whether the current Browser supports streaming responses
        /// </summary>
    private static bool StreamingSupported { get; } = GetIsStreamingSupported();
    private static bool GetIsStreamingSupported()
    {
        using (var streamingSupported = 
               	   new Function(
                       "return typeof Response !== 'undefined' && 
                       "'body' in Response.prototype && 
                       "typeof ReadableStream === 'function'"))
            
        	return (bool)streamingSupported.Call();
    }

    public bool UseCookies
    {
        get => throw new PlatformNotSupportedException();
        set => throw new PlatformNotSupportedException();
    }
    
    public CookieContainer CookieContainer
    {
        get => throw new PlatformNotSupportedException();
        set => throw new PlatformNotSupportedException();
    }
    
    public DecompressionMethods AutomaticDecompression
    {
        get => throw new PlatformNotSupportedException();
        set => throw new PlatformNotSupportedException();
    }
    
    public bool UseProxy
    {
        get => throw new PlatformNotSupportedException();
        set => throw new PlatformNotSupportedException();
    }
    
    public IWebProxy? Proxy
    {
        get => throw new PlatformNotSupportedException();
        set => throw new PlatformNotSupportedException();
    }
    
    public ICredentials? DefaultProxyCredentials
    {
        get => throw new PlatformNotSupportedException();
        set => throw new PlatformNotSupportedException();
    }
    
    public bool PreAuthenticate
    {
        get => throw new PlatformNotSupportedException();
        set => throw new PlatformNotSupportedException();
    }
    
    public ICredentials? Credentials
    {
        get => throw new PlatformNotSupportedException();
        set => throw new PlatformNotSupportedException();
    }
    
    public bool AllowAutoRedirect
    {
        get => _allowAutoRedirect;
        set
        {
            _allowAutoRedirect = value;
            _isAllowAutoRedirectTouched = true;
        }
    }
    
    public int MaxAutomaticRedirections
    {
        get => throw new PlatformNotSupportedException();
        set => throw new PlatformNotSupportedException();
    }
    
    public int MaxConnectionsPerServer
    {
        get => throw new PlatformNotSupportedException();
        set => throw new PlatformNotSupportedException();
    }
    
    public int MaxResponseHeadersLength
    {
        get => throw new PlatformNotSupportedException();
        set => throw new PlatformNotSupportedException();
    }
    
    public SslClientAuthenticationOptions SslOptions
    {
        get => throw new PlatformNotSupportedException();
        set => throw new PlatformNotSupportedException();
    }
    
    public const bool SupportsAutomaticDecompression = false;
    public const bool SupportsProxy = false;
    public const bool SupportsRedirectConfiguration = true;
    
    private Dictionary<string, object?>? _properties;
    public IDictionary<string, object?> Properties => _properties ??= new Dictionary<string, object?>();
    
    protected internal override HttpResponseMessage Send(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        throw new PlatformNotSupportedException ();
    }
    
    protected internal override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request), SR.net_http_handler_norequest);
        }
        
        try
        {
            var requestObject = new JSObject();
            
            if (request.Options.TryGetValue(FetchOptions, out IDictionary<string, object>? fetchOptions))
            {
                foreach (KeyValuePair<string, object> item in fetchOptions)
                {
                    requestObject.SetObjectProperty(item.Key, item.Value);
                }
            }
            
            requestObject.SetObjectProperty("method", request.Method.Method);
            
            // Only set if property was specifically modified and is not default value
            if (_isAllowAutoRedirectTouched)
            {
                // Allowing or Disallowing redirects.
                // Here we will set redirect to `manual` instead of error if AllowAutoRedirect is false so there is no exception thrown   
                requestObject.SetObjectProperty(
                    "redirect", 
                    AllowAutoRedirect ? "follow" : "manual");
            }

            // We need to check for body content
            if (request.Content != null)
            {
                if (request.Content is StringContent)
                {
                    requestObject.SetObjectProperty(
                        "body", 
                        await request.Content
                        			.ReadAsStringAsync(cancellationToken)
                        			.ConfigureAwait(continueOnCapturedContext: true));
                }
                else
                {
                    using (Uint8Array uint8Buffer = 
                           Uint8Array.From(
                               await request.Content
                               			   .ReadAsByteArrayAsync(cancellationToken)
                               			   .ConfigureAwait(continueOnCapturedContext: true)))
                    {
                        requestObject.SetObjectProperty("body", uint8Buffer);
                    }
                }
            }
            
            // Process headers
            // Cors has its own restrictions on headers.                
            using (HostObject jsHeaders = new HostObject("Headers"))
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
                {
                    foreach (string value in header.Value)
                    {
                        jsHeaders.Invoke("append", header.Key, value);
                    }
                }
                
                if (request.Content != null)
                {
                    foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
                    {
                        foreach (string value in header.Value)
                        {
                            jsHeaders.Invoke("append", header.Key, value);
                        }
                    }
                }
                
                requestObject.SetObjectProperty("headers", jsHeaders);
            }
            
            WasmHttpReadStream? wasmHttpReadStream = null;
            
            JSObject abortController = new HostObject("AbortController");
            JSObject signal = (JSObject)abortController.GetObjectProperty("signal");
            requestObject.SetObjectProperty("signal", signal);
            signal.Dispose();
            
            CancellationTokenSource abortCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationTokenRegistration abortRegistration = abortCts.Token.Register((Action)(() =>
                {
                    if (abortController.JSHandle != -1)
                    {
                        abortController.Invoke("abort");
                        abortController?.Dispose();
                    }
                    wasmHttpReadStream?.Dispose();
                    abortCts.Dispose();
                }));

            var args = new System.Runtime.InteropServices.JavaScript.Array();
            if (request.RequestUri != null)
            {
                args.Push(request.RequestUri.ToString());
                args.Push(requestObject);
            }
            
            requestObject.Dispose();
            
            var response = s_fetch?.Invoke("apply", s_window, args) as Task<object>;
            args.Dispose();
            if (response == null) throw new Exception(SR.net_http_marshalling_response_promise_from_fetch);
            
            JSObject t = (JSObject)await response.ConfigureAwait(continueOnCapturedContext: true);
            
            var status = new WasmFetchResponse(t, abortController, abortCts, abortRegistration);
            HttpResponseMessage httpResponse = new HttpResponseMessage((HttpStatusCode)status.Status);
            httpResponse.RequestMessage = request;
            
            // Here we will set the ReasonPhrase so that it can be evaluated later.
            // We do not have a status code but this will signal some type of what happened after interrogating the 
            // status code for success or not i.e. IsSuccessStatusCode
            //
            // https://developer.mozilla.org/en-US/docs/Web/API/Response/type
            // opaqueredirect: The fetch request was made with redirect: "manual".
            // The Response's status is 0, headers are empty, body is null and trailer is empty.
            if (status.ResponseType == "opaqueredirect")
            {
                httpResponse.SetReasonPhraseWithoutValidation(status.ResponseType);
            }
            
            bool streamingEnabled = false;
            if (StreamingSupported)
            {
                request.Options.TryGetValue(EnableStreamingResponse, out streamingEnabled);
            }
            
            httpResponse.Content = streamingEnabled
                ? new StreamContent(wasmHttpReadStream = new WasmHttpReadStream(status))
                : (HttpContent)new BrowserHttpContent(status);
            
            // Fill the response headers
            // CORS will only allow access to certain headers.
            // If a request is made for a resource on another origin which returns the CORs headers, then the type is cors.
            // cors and basic responses are almost identical except that a cors response restricts the headers you can view to
            // `Cache-Control`, `Content-Language`, `Content-Type`, `Expires`, `Last-Modified`, and `Pragma`.
            // View more information https://developers.google.com/web/updates/2015/03/introduction-to-fetch#response_types
            //
            // Note: Some of the headers may not even be valid header types in .NET thus we use TryAddWithoutValidation
            using (JSObject respHeaders = status.Headers)
            {
                if (respHeaders != null)
                {
                    using (var entriesIterator = (JSObject)respHeaders.Invoke("entries"))
                    {
                        JSObject? nextResult = null;
                        try
                        {
                            nextResult = (JSObject)entriesIterator.Invoke("next");
                            while (!(bool)nextResult.GetObjectProperty("done"))
                            {
                                using (var resultValue = 
                                       (System.Runtime.InteropServices.JavaScript.Array)nextResult.GetObjectProperty("value"))
                                {
                                    var name = (string)resultValue[0];
                                    var value = (string)resultValue[1];
                                    if (!httpResponse.Headers.TryAddWithoutValidation(name, value))
                                        httpResponse.Content.Headers.TryAddWithoutValidation(name, value);
                                }
                                
                                nextResult?.Dispose();
                                nextResult = (JSObject)entriesIterator.Invoke("next");
                            }
                        }
                        finally
                        {
                            nextResult?.Dispose();
                        }
                    }
                }
            }        	
            return httpResponse;
        }
        catch (JSException jsExc)
        {
            throw new System.Net.Http.HttpRequestException(jsExc.Message);
        }
    }
    
    private sealed class WasmFetchResponse : IDisposable
    {
        private readonly JSObject _fetchResponse;
        private readonly JSObject _abortController;
        private readonly CancellationTokenSource _abortCts;
        private readonly CancellationTokenRegistration _abortRegistration;
        private bool _isDisposed;
        
        public WasmFetchResponse(
            JSObject fetchResponse, 
            JSObject abortController, 
            CancellationTokenSource abortCts, 
            CancellationTokenRegistration abortRegistration)
        {
                _fetchResponse = fetchResponse ?? throw new ArgumentNullException(nameof(fetchResponse));
                _abortController = abortController ?? throw new ArgumentNullException(nameof(abortController));
                _abortCts = abortCts;
                _abortRegistration = abortRegistration;
        }
        
        public bool IsOK => (bool)_fetchResponse.GetObjectProperty("ok");
        public bool IsRedirected => (bool)_fetchResponse.GetObjectProperty("redirected");
        public int Status => (int)_fetchResponse.GetObjectProperty("status");
        public string StatusText => (string)_fetchResponse.GetObjectProperty("statusText");
        public string ResponseType => (string)_fetchResponse.GetObjectProperty("type");
        public string Url => (string)_fetchResponse.GetObjectProperty("url");
        public bool IsBodyUsed => (bool)_fetchResponse.GetObjectProperty("bodyUsed");
        public JSObject Headers => (JSObject)_fetchResponse.GetObjectProperty("headers");
        public JSObject Body => (JSObject)_fetchResponse.GetObjectProperty("body");
        
        public Task<object> ArrayBuffer() => (Task<object>)_fetchResponse.Invoke("arrayBuffer");
        public Task<object> Text() => (Task<object>)_fetchResponse.Invoke("text");
        public Task<object> JSON() => (Task<object>)_fetchResponse.Invoke("json");
        
        public void Dispose()
        {
            if (_isDisposed) return;
            
            _isDisposed = true;
            
            _abortCts.Cancel();
            _abortCts.Dispose();
            _abortRegistration.Dispose();
            
            _fetchResponse?.Dispose();
            _abortController?.Dispose();
        }
    }
    
    private sealed class BrowserHttpContent : HttpContent
    {
        private byte[]? _data;
        private readonly WasmFetchResponse _status;
        
        public BrowserHttpContent(WasmFetchResponse status)
        {
            _status = status ?? throw new ArgumentNullException(nameof(status));
        }
        
        private async Task<byte[]> GetResponseData()
        {
            if (_data != null)
            {
                return _data;
            }
            
            using (System.Runtime.InteropServices.JavaScript.ArrayBuffer dataBuffer = 
                   (System.Runtime.InteropServices.JavaScript.ArrayBuffer)
                   	   await _status.ArrayBuffer().ConfigureAwait(continueOnCapturedContext: true))
            {
                using (Uint8Array dataBinView = new Uint8Array(dataBuffer))
                {
                    _data = dataBinView.ToArray();
                    _status.Dispose();
                }
            }
            
            return _data;
        }
        
        protected override async Task<Stream> CreateContentReadStreamAsync()
        {
            byte[] data = await GetResponseData().ConfigureAwait(continueOnCapturedContext: true);
            return new MemoryStream(data, writable: false);
        }
        
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);
        
        protected override async Task SerializeToStreamAsync(
            Stream stream, 
            TransportContext? context, 
            CancellationToken cancellationToken)
        {
            byte[] data = await GetResponseData().ConfigureAwait(continueOnCapturedContext: true);
            await stream.WriteAsync(data, cancellationToken).ConfigureAwait(continueOnCapturedContext: true);
        }
        
        protected internal override bool TryComputeLength(out long length)
        {
            if (_data != null)
            {
                length = _data.Length;
                return true;
            }
            
            length = 0;
            return false;
        }
        
        protected override void Dispose(bool disposing)
        {
            _status?.Dispose();
            base.Dispose(disposing);
        }
    }
    
    private sealed class WasmHttpReadStream : Stream
    {
        private WasmFetchResponse? _status;
        private JSObject? _reader;
        
        private byte[]? _bufferedBytes;
        private int _position;
        
        public WasmHttpReadStream(WasmFetchResponse status)
        {
            _status = status;
        }
        
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            return ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
        }
        
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (_reader == null)
            {
                // If we've read everything, then _reader and _status will be null
                if (_status == null)
                {
                    return 0;
                }
                
                try
                {
                    using (JSObject body = _status.Body)
                    {
                        _reader = (JSObject)body.Invoke("getReader");
                    }
                }
                catch (JSException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw;
                }
            }
            
            if (_bufferedBytes != null && _position < _bufferedBytes.Length)
            {
                return ReadBuffered();
            }
            
            try
            {
                var t = (Task<object>)_reader.Invoke("read");
                using (var read = (JSObject)await t.ConfigureAwait(continueOnCapturedContext: true))
                {
                    if ((bool)read.GetObjectProperty("done"))
                    {
                        _reader.Dispose();
                        _reader = null;
                        
                        _status?.Dispose();
                        _status = null;
                        return 0;
                    }
                    
                    _position = 0;
                    // value for fetch streams is a Uint8Array
                    using (Uint8Array binValue = (Uint8Array)read.GetObjectProperty("value"))
                        _bufferedBytes = binValue.ToArray();
                }
            }
            catch (JSException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }
            
            return ReadBuffered();
            
            int ReadBuffered()
            {
                int n = Math.Min(_bufferedBytes.Length - _position, buffer.Length);
                if (n <= 0)
                {
                    return 0;
                }
                
                _bufferedBytes.AsSpan(_position, n).CopyTo(buffer.Span);
                _position += n;
                
                return n;
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            _reader?.Dispose();
            _status?.Dispose();
        }
        
        public override void Flush()
        {
        }
        
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException(SR.net_http_synchronous_reads_not_supported);
        }
        
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }
        
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
        
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}

```

###### - http utilities.browser

```c#
internal static partial class HttpUtilities
{
    internal static bool IsSupportedNonSecureScheme(string scheme) =>
        string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase) || 
        IsBlobScheme(scheme) || 
        IsNonSecureWebSocketScheme(scheme);

    internal static bool IsBlobScheme(string scheme) =>
        string.Equals(scheme, "blob", StringComparison.OrdinalIgnoreCase);
    
    internal static string InvalidUriMessage => SR.net_http_client_http_browser_baseaddress_required;
}

```

##### 5.3.2.3 sockets handler

```c#
[UnsupportedOSPlatform("browser")]
    public sealed class SocketsHttpHandler : HttpMessageHandler
    {
        private readonly HttpConnectionSettings _settings = new HttpConnectionSettings();
        private HttpMessageHandlerStage? _handler;
        private bool _disposed;

        private void CheckDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SocketsHttpHandler));
            }
        }

        private void CheckDisposedOrStarted()
        {
            CheckDisposed();
            if (_handler != null)
            {
                throw new InvalidOperationException(SR.net_http_operation_started);
            }
        }

        /// <summary>
        /// Gets a value that indicates whether the handler is supported on the current platform.
        /// </summary>
        [UnsupportedOSPlatformGuard("browser")]
        public static bool IsSupported => !OperatingSystem.IsBrowser();

        public bool UseCookies
        {
            get => _settings._useCookies;
            set
            {
                CheckDisposedOrStarted();
                _settings._useCookies = value;
            }
        }

        [AllowNull]
        public CookieContainer CookieContainer
        {
            get => _settings._cookieContainer ?? (_settings._cookieContainer = new CookieContainer());
            set
            {
                CheckDisposedOrStarted();
                _settings._cookieContainer = value;
            }
        }

        public DecompressionMethods AutomaticDecompression
        {
            get => _settings._automaticDecompression;
            set
            {
                CheckDisposedOrStarted();
                _settings._automaticDecompression = value;
            }
        }

        public bool UseProxy
        {
            get => _settings._useProxy;
            set
            {
                CheckDisposedOrStarted();
                _settings._useProxy = value;
            }
        }

        public IWebProxy? Proxy
        {
            get => _settings._proxy;
            set
            {
                CheckDisposedOrStarted();
                _settings._proxy = value;
            }
        }

        public ICredentials? DefaultProxyCredentials
        {
            get => _settings._defaultProxyCredentials;
            set
            {
                CheckDisposedOrStarted();
                _settings._defaultProxyCredentials = value;
            }
        }

        public bool PreAuthenticate
        {
            get => _settings._preAuthenticate;
            set
            {
                CheckDisposedOrStarted();
                _settings._preAuthenticate = value;
            }
        }

        public ICredentials? Credentials
        {
            get => _settings._credentials;
            set
            {
                CheckDisposedOrStarted();
                _settings._credentials = value;
            }
        }

        public bool AllowAutoRedirect
        {
            get => _settings._allowAutoRedirect;
            set
            {
                CheckDisposedOrStarted();
                _settings._allowAutoRedirect = value;
            }
        }

        public int MaxAutomaticRedirections
        {
            get => _settings._maxAutomaticRedirections;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, SR.Format(SR.net_http_value_must_be_greater_than, 0));
                }

                CheckDisposedOrStarted();
                _settings._maxAutomaticRedirections = value;
            }
        }

        public int MaxConnectionsPerServer
        {
            get => _settings._maxConnectionsPerServer;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, SR.Format(SR.net_http_value_must_be_greater_than, 0));
                }

                CheckDisposedOrStarted();
                _settings._maxConnectionsPerServer = value;
            }
        }

        public int MaxResponseDrainSize
        {
            get => _settings._maxResponseDrainSize;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, SR.ArgumentOutOfRange_NeedNonNegativeNum);
                }

                CheckDisposedOrStarted();
                _settings._maxResponseDrainSize = value;
            }
        }

        public TimeSpan ResponseDrainTimeout
        {
            get => _settings._maxResponseDrainTime;
            set
            {
                if ((value < TimeSpan.Zero && value != Timeout.InfiniteTimeSpan) ||
                    (value.TotalMilliseconds > int.MaxValue))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                CheckDisposedOrStarted();
                _settings._maxResponseDrainTime = value;
            }
        }

        public int MaxResponseHeadersLength
        {
            get => _settings._maxResponseHeadersLength;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, SR.Format(SR.net_http_value_must_be_greater_than, 0));
                }

                CheckDisposedOrStarted();
                _settings._maxResponseHeadersLength = value;
            }
        }

        [AllowNull]
        public SslClientAuthenticationOptions SslOptions
        {
            get => _settings._sslOptions ?? (_settings._sslOptions = new SslClientAuthenticationOptions());
            set
            {
                CheckDisposedOrStarted();
                _settings._sslOptions = value;
            }
        }

        public TimeSpan PooledConnectionLifetime
        {
            get => _settings._pooledConnectionLifetime;
            set
            {
                if (value < TimeSpan.Zero && value != Timeout.InfiniteTimeSpan)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                CheckDisposedOrStarted();
                _settings._pooledConnectionLifetime = value;
            }
        }

        public TimeSpan PooledConnectionIdleTimeout
        {
            get => _settings._pooledConnectionIdleTimeout;
            set
            {
                if (value < TimeSpan.Zero && value != Timeout.InfiniteTimeSpan)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                CheckDisposedOrStarted();
                _settings._pooledConnectionIdleTimeout = value;
            }
        }

        public TimeSpan ConnectTimeout
        {
            get => _settings._connectTimeout;
            set
            {
                if ((value <= TimeSpan.Zero && value != Timeout.InfiniteTimeSpan) ||
                    (value.TotalMilliseconds > int.MaxValue))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                CheckDisposedOrStarted();
                _settings._connectTimeout = value;
            }
        }

        public TimeSpan Expect100ContinueTimeout
        {
            get => _settings._expect100ContinueTimeout;
            set
            {
                if ((value < TimeSpan.Zero && value != Timeout.InfiniteTimeSpan) ||
                    (value.TotalMilliseconds > int.MaxValue))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                CheckDisposedOrStarted();
                _settings._expect100ContinueTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets the keep alive ping delay. The client will send a keep alive ping to the server if it
        /// doesn't receive any frames on a connection for this period of time. This property is used together with
        /// <see cref="SocketsHttpHandler.KeepAlivePingTimeout"/> to close broken connections.
        /// <para>
        /// Delay value must be greater than or equal to 1 second. Set to <see cref="Timeout.InfiniteTimeSpan"/> to
        /// disable the keep alive ping.
        /// Defaults to <see cref="Timeout.InfiniteTimeSpan"/>.
        /// </para>
        /// </summary>
        public TimeSpan KeepAlivePingDelay
        {
            get => _settings._keepAlivePingDelay;
            set
            {
                if (value.Ticks < TimeSpan.TicksPerSecond && value != Timeout.InfiniteTimeSpan)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, SR.Format(SR.net_http_value_must_be_greater_than_or_equal, value, TimeSpan.FromSeconds(1)));
                }

                CheckDisposedOrStarted();
                _settings._keepAlivePingDelay = value;
            }
        }

        /// <summary>
        /// Gets or sets the keep alive ping timeout. Keep alive pings are sent when a period of inactivity exceeds
        /// the configured <see cref="KeepAlivePingDelay"/> value. The client will close the connection if it
        /// doesn't receive any frames within the timeout.
        /// <para>
        /// Timeout must be greater than or equal to 1 second. Set to <see cref="Timeout.InfiniteTimeSpan"/> to
        /// disable the keep alive ping timeout.
        /// Defaults to 20 seconds.
        /// </para>
        /// </summary>
        public TimeSpan KeepAlivePingTimeout
        {
            get => _settings._keepAlivePingTimeout;
            set
            {
                if (value.Ticks < TimeSpan.TicksPerSecond && value != Timeout.InfiniteTimeSpan)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, SR.Format(SR.net_http_value_must_be_greater_than_or_equal, value, TimeSpan.FromSeconds(1)));
                }

                CheckDisposedOrStarted();
                _settings._keepAlivePingTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets the keep alive ping behaviour. Keep alive pings are sent when a period of inactivity exceeds
        /// the configured <see cref="KeepAlivePingDelay"/> value.
        /// </summary>
        public HttpKeepAlivePingPolicy KeepAlivePingPolicy
        {
            get => _settings._keepAlivePingPolicy;
            set
            {
                CheckDisposedOrStarted();
                _settings._keepAlivePingPolicy = value;
            }
        }

        /// <summary>
        /// Gets or sets a value that indicates whether additional HTTP/2 connections can be established to the same server
        /// when the maximum of concurrent streams is reached on all existing connections.
        /// </summary>
        public bool EnableMultipleHttp2Connections
        {
            get => _settings._enableMultipleHttp2Connections;
            set
            {
                CheckDisposedOrStarted();

                _settings._enableMultipleHttp2Connections = value;
            }
        }

        internal const bool SupportsAutomaticDecompression = true;
        internal const bool SupportsProxy = true;
        internal const bool SupportsRedirectConfiguration = true;

        /// <summary>
        /// When non-null, a custom callback used to open new connections.
        /// </summary>
        public Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>>? ConnectCallback
        {
            get => _settings._connectCallback;
            set
            {
                CheckDisposedOrStarted();
                _settings._connectCallback = value;
            }
        }

        /// <summary>
        /// Gets or sets a custom callback that provides access to the plaintext HTTP protocol stream.
        /// </summary>
        public Func<SocketsHttpPlaintextStreamFilterContext, CancellationToken, ValueTask<Stream>>? PlaintextStreamFilter
        {
            get => _settings._plaintextStreamFilter;
            set
            {
                CheckDisposedOrStarted();
                _settings._plaintextStreamFilter = value;
            }
        }

        /// <summary>
        /// Gets or sets the QUIC implementation to be used for HTTP3 requests.
        /// </summary>
        public QuicImplementationProvider? QuicImplementationProvider
        {
            // !!! NOTE !!!
            // This is temporary and will not ship.
            get => _settings._quicImplementationProvider;
            set
            {
                CheckDisposedOrStarted();
                _settings._quicImplementationProvider = value;
            }
        }

        public IDictionary<string, object?> Properties =>
            _settings._properties ?? (_settings._properties = new Dictionary<string, object?>());

        /// <summary>
        /// Gets or sets a callback that returns the <see cref="Encoding"/> to encode the value for the specified request header name,
        /// or <see langword="null"/> to use the default behavior.
        /// </summary>
        public HeaderEncodingSelector<HttpRequestMessage>? RequestHeaderEncodingSelector
        {
            get => _settings._requestHeaderEncodingSelector;
            set
            {
                CheckDisposedOrStarted();
                _settings._requestHeaderEncodingSelector = value;
            }
        }

        /// <summary>
        /// Gets or sets a callback that returns the <see cref="Encoding"/> to decode the value for the specified response header name,
        /// or <see langword="null"/> to use the default behavior.
        /// </summary>
        public HeaderEncodingSelector<HttpRequestMessage>? ResponseHeaderEncodingSelector
        {
            get => _settings._responseHeaderEncodingSelector;
            set
            {
                CheckDisposedOrStarted();
                _settings._responseHeaderEncodingSelector = value;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                _handler?.Dispose();
            }

            base.Dispose(disposing);
        }

        private HttpMessageHandlerStage SetupHandlerChain()
        {
            // Clone the settings to get a relatively consistent view that won't change after this point.
            // (This isn't entirely complete, as some of the collections it contains aren't currently deeply cloned.)
            HttpConnectionSettings settings = _settings.CloneAndNormalize();

            HttpConnectionPoolManager poolManager = new HttpConnectionPoolManager(settings);

            HttpMessageHandlerStage handler;

            if (settings._credentials == null)
            {
                handler = new HttpConnectionHandler(poolManager);
            }
            else
            {
                handler = new HttpAuthenticatedConnectionHandler(poolManager);
            }

            if (settings._allowAutoRedirect)
            {
                // Just as with WinHttpHandler, for security reasons, we do not support authentication on redirects
                // if the credential is anything other than a CredentialCache.
                // We allow credentials in a CredentialCache since they are specifically tied to URIs.
                HttpMessageHandlerStage redirectHandler =
                    (settings._credentials == null || settings._credentials is CredentialCache) ?
                    handler :
                    new HttpConnectionHandler(poolManager);        // will not authenticate

                handler = new RedirectHandler(settings._maxAutomaticRedirections, handler, redirectHandler);
            }

            if (settings._automaticDecompression != DecompressionMethods.None)
            {
                handler = new DecompressionHandler(settings._automaticDecompression, handler);
            }

            // Ensure a single handler is used for all requests.
            if (Interlocked.CompareExchange(ref _handler, handler, null) != null)
            {
                handler.Dispose();
            }

            return _handler;
        }

        protected internal override HttpResponseMessage Send(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request), SR.net_http_handler_norequest);
            }

            if (request.Version.Major >= 2)
            {
                throw new NotSupportedException(SR.Format(SR.net_http_http2_sync_not_supported, GetType()));
            }

            // Do not allow upgrades for synchronous requests, that might lead to asynchronous code-paths.
            if (request.VersionPolicy == HttpVersionPolicy.RequestVersionOrHigher)
            {
                throw new NotSupportedException(SR.Format(SR.net_http_upgrade_not_enabled_sync, nameof(Send), request.VersionPolicy));
            }

            CheckDisposed();

            cancellationToken.ThrowIfCancellationRequested();

            HttpMessageHandlerStage handler = _handler ?? SetupHandlerChain();

            Exception? error = ValidateAndNormalizeRequest(request);
            if (error != null)
            {
                throw error;
            }

            return handler.Send(request, cancellationToken);
        }

        protected internal override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request), SR.net_http_handler_norequest);
            }

            CheckDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<HttpResponseMessage>(cancellationToken);
            }

            HttpMessageHandler handler = _handler ?? SetupHandlerChain();

            Exception? error = ValidateAndNormalizeRequest(request);
            if (error != null)
            {
                return Task.FromException<HttpResponseMessage>(error);
            }

            return handler.SendAsync(request, cancellationToken);
        }

        private Exception? ValidateAndNormalizeRequest(HttpRequestMessage request)
        {
            if (request.Version.Major == 0)
            {
                return new NotSupportedException(SR.net_http_unsupported_version);
            }

            // Add headers to define content transfer, if not present
            if (request.HasHeaders && request.Headers.TransferEncodingChunked.GetValueOrDefault())
            {
                if (request.Content == null)
                {
                    return new HttpRequestException(SR.net_http_client_execution_error,
                        new InvalidOperationException(SR.net_http_chunked_not_allowed_with_empty_content));
                }

                // Since the user explicitly set TransferEncodingChunked to true, we need to remove
                // the Content-Length header if present, as sending both is invalid.
                request.Content.Headers.ContentLength = null;
            }
            else if (request.Content != null && request.Content.Headers.ContentLength == null)
            {
                // We have content, but neither Transfer-Encoding nor Content-Length is set.
                request.Headers.TransferEncodingChunked = true;
            }

            if (request.Version.Minor == 0 && request.Version.Major == 1 && request.HasHeaders)
            {
                // HTTP 1.0 does not support chunking
                if (request.Headers.TransferEncodingChunked == true)
                {
                    return new NotSupportedException(SR.net_http_unsupported_chunking);
                }

                // HTTP 1.0 does not support Expect: 100-continue; just disable it.
                if (request.Headers.ExpectContinue == true)
                {
                    request.Headers.ExpectContinue = false;
                }
            }

            return null;
        }
    }
```



#### 5.4 http message invoker

```c#
public class HttpMessageInvoker : IDisposable
{
    private volatile bool _disposed;
    private void CheckDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().ToString());
        }
    }
    
    private readonly bool _disposeHandler;    
    private readonly HttpMessageHandler _handler;
    
    public HttpMessageInvoker(HttpMessageHandler handler) : this(handler, true)
    {
    }
    
    public HttpMessageInvoker(HttpMessageHandler handler, bool disposeHandler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }
        
        if (NetEventSource.Log.IsEnabled()) NetEventSource.Associate(this, handler);
        
        // 注入 http message handler
        _handler = handler;
        _disposeHandler = disposeHandler;
    }
    
    // 方法- send
    [UnsupportedOSPlatformAttribute("browser")]
    public virtual HttpResponseMessage Send(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        
        CheckDisposed();
        
        if (HttpTelemetry.Log.IsEnabled() && 
            !request.WasSentByHttpClient() && 
            request.RequestUri != null)
        {
            HttpTelemetry.Log.RequestStart(request);
            
            try
            {
                // 调用 http message handler 的 send 方法
                return _handler.Send(request, cancellationToken);
            }
            catch when (LogRequestFailed(telemetryStarted: true))
            {
                // Unreachable as LogRequestFailed will return false
                throw;
            }
            finally
            {
                HttpTelemetry.Log.RequestStop();
            }
        }
        else
        {
            return _handler.Send(request, cancellationToken);
        }
    }
    
    // 方法- send async
    public virtual Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        CheckDisposed();
        
        if (HttpTelemetry.Log.IsEnabled() && 
            !request.WasSentByHttpClient() && 
            request.RequestUri != null)
        {            
            return SendAsyncWithTelemetry(_handler, request, cancellationToken);
        }
        
        // 调用 handler 的 send async 方法
        return _handler.SendAsync(request, cancellationToken);
        
        static async Task<HttpResponseMessage> SendAsyncWithTelemetry(
            HttpMessageHandler handler, 
            HttpRequestMessage request, 
            CancellationToken cancellationToken)
        {
            HttpTelemetry.Log.RequestStart(request);
            
            try
            {
                // 调用 handler 的 send async 方法
                return await handler.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch when (LogRequestFailed(telemetryStarted: true))
            {
                // Unreachable as LogRequestFailed will return false
                throw;
            }
            finally
            {
                HttpTelemetry.Log.RequestStop();
            }
        }
    }
    
    internal static bool LogRequestFailed(bool telemetryStarted)
    {
        if (HttpTelemetry.Log.IsEnabled() && telemetryStarted)
        {
            HttpTelemetry.Log.RequestFailed();
        }
        return false;
    }
    
    // dispose
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            
            if (_disposeHandler)
            {
                _handler.Dispose();
            }
        }
    }        
}

```

##### 5.4.1 http client

```c#
public partial class HttpClient : HttpMessageInvoker
{
    private volatile bool _operationStarted;
    private volatile bool _disposed;
                        
    private const HttpCompletionOption DefaultCompletionOption = HttpCompletionOption.ResponseContentRead;            
    private CancellationTokenSource _pendingRequestsCts;
                                   
    // version
    private Version _defaultRequestVersion = HttpUtilities.DefaultRequestVersion;
    public Version DefaultRequestVersion
    {
        get => _defaultRequestVersion;
        set
        {
            CheckDisposedOrStarted();
            _defaultRequestVersion = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    // version policy
    // Note that this property has no effect on any of the "Send(HttpRequestMessage)" and "SendAsync(HttpRequestMessage)"
    // overloads since they accept fully initialized "HttpRequestMessage".        
    private HttpVersionPolicy _defaultVersionPolicy = HttpUtilities.DefaultVersionPolicy;
    public HttpVersionPolicy DefaultVersionPolicy
    {
        get => _defaultVersionPolicy;
        set
        {
            CheckDisposedOrStarted();
            _defaultVersionPolicy = value;
        }
    }
    
    // base address
    private Uri? _baseAddress;
    public Uri? BaseAddress
    {
        get => _baseAddress;
        set
        {
            CheckBaseAddress(value, nameof(value));
            CheckDisposedOrStarted();
            
            if (NetEventSource.Log.IsEnabled()) NetEventSource.UriBaseAddress(this, value);            
            _baseAddress = value;
        }
    }
    
    // timeout
    private static readonly TimeSpan s_maxTimeout = TimeSpan.FromMilliseconds(int.MaxValue);
    private static readonly TimeSpan s_infiniteTimeout = Threading.Timeout.InfiniteTimeSpan;
    private TimeSpan _timeout;
    public TimeSpan Timeout
    {
        get => _timeout;
        set
        {
            if (value != s_infiniteTimeout && 
                (value <= TimeSpan.Zero || value > s_maxTimeout))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            CheckDisposedOrStarted();
            _timeout = value;
        }
    }
    
    // max response content buffer size
    private int _maxResponseContentBufferSize;
    public long MaxResponseContentBufferSize
    {
        get => _maxResponseContentBufferSize;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            if (value > HttpContent.MaxBufferSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), 
                    value,
                    SR.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        SR.net_http_content_buffersize_limit, HttpContent.MaxBufferSize));
            }
            CheckDisposedOrStarted();
            
            Debug.Assert(HttpContent.MaxBufferSize <= int.MaxValue);
            _maxResponseContentBufferSize = (int)value;
        }
    }

    // default request headers
    private HttpRequestHeaders? _defaultRequestHeaders;
    public HttpRequestHeaders DefaultRequestHeaders => _defaultRequestHeaders ??= new HttpRequestHeaders();   
    
    // default proxy
    private static IWebProxy? s_defaultProxy;
    public static IWebProxy DefaultProxy
    {
        get => LazyInitializer.EnsureInitialized(ref s_defaultProxy, () => SystemProxyInfo.Proxy);
        set => s_defaultProxy = value ?? throw new ArgumentNullException(nameof(value));
    }
    
    
    // ctor
    public HttpClient() : this(CreateDefaultHandler())
    {
    }
    
    public HttpClient(HttpMessageHandler handler) : this(handler, true)
    {
    }
    
    private static readonly TimeSpan s_defaultTimeout = TimeSpan.FromSeconds(100);
    
    public HttpClient(HttpMessageHandler handler, bool disposeHandler) : base(handler, disposeHandler)
    {
        _timeout = s_defaultTimeout;
        _maxResponseContentBufferSize = HttpContent.MaxBufferSize;
        _pendingRequestsCts = new CancellationTokenSource();
    }
    
    
    /* send */                                                                               
    [UnsupportedOSPlatform("browser")]
    public HttpResponseMessage Send(HttpRequestMessage request) =>
        Send(request, DefaultCompletionOption, cancellationToken: default);
    
    [UnsupportedOSPlatform("browser")]
    public HttpResponseMessage Send(
        HttpRequestMessage request, 
        HttpCompletionOption completionOption) =>
        	Send(request, completionOption, cancellationToken: default);
    
    [UnsupportedOSPlatform("browser")]
    public override HttpResponseMessage Send(
        HttpRequestMessage request, 
        CancellationToken cancellationToken) =>
        	Send(request, DefaultCompletionOption, cancellationToken);
    
    [UnsupportedOSPlatform("browser")]
    public HttpResponseMessage Send(
        HttpRequestMessage request, 
        HttpCompletionOption completionOption, 
        CancellationToken cancellationToken)
    {
        CheckRequestBeforeSend(request);
        (CancellationTokenSource cts, bool disposeCts, CancellationTokenSource pendingRequestsCts) = 
            PrepareCancellationTokenSource(cancellationToken);
        
        bool telemetryStarted = StartSend(request);
        bool responseContentTelemetryStarted = false;
        HttpResponseMessage? response = null;
        try
        {
            // Wait for the send request to complete, getting back the response.
            response = base.Send(request, cts.Token);
            ThrowForNullResponse(response);
            
            // Buffer the response content if we've been asked to.
            if (ShouldBufferResponse(completionOption, request))
            {
                if (HttpTelemetry.Log.IsEnabled() && telemetryStarted)
                {
                    HttpTelemetry.Log.ResponseContentStart();
                    responseContentTelemetryStarted = true;
                }
                
                response.Content.LoadIntoBuffer(_maxResponseContentBufferSize, cts.Token);
            }
            
            return response;
        }
        catch (Exception e)
        {
            HandleFailure(e, telemetryStarted, response, cts, cancellationToken, pendingRequestsCts);
            throw;
        }
        finally
        {
            FinishSend(cts, disposeCts, telemetryStarted, responseContentTelemetryStarted);
        }
    }
    
    /* send async */
    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request) =>
        SendAsync(request, DefaultCompletionOption, CancellationToken.None);
    
    public override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken) =>
        	SendAsync(request, DefaultCompletionOption, cancellationToken);
    
    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        HttpCompletionOption completionOption) =>
        	SendAsync(request, completionOption, CancellationToken.None);
    
    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        HttpCompletionOption completionOption, 
        CancellationToken cancellationToken)
    {
        // Called outside of async state machine to propagate certain exception even without awaiting the returned task.
        CheckRequestBeforeSend(request);
        (CancellationTokenSource cts, bool disposeCts, CancellationTokenSource pendingRequestsCts) = 
            PrepareCancellationTokenSource(cancellationToken);

        return Core(request, completionOption, cts, disposeCts, pendingRequestsCts, cancellationToken);
        
        async Task<HttpResponseMessage> Core(
            HttpRequestMessage request, 
            HttpCompletionOption completionOption,
            CancellationTokenSource cts, 
            bool disposeCts, 
            CancellationTokenSource pendingRequestsCts, 
            CancellationToken originalCancellationToken)
        {
            bool telemetryStarted = StartSend(request);
            bool responseContentTelemetryStarted = false;
            HttpResponseMessage? response = null;
            try
            {
                // Wait for the send request to complete, getting back the response.
                response = await base.SendAsync(request, cts.Token).ConfigureAwait(false);
                ThrowForNullResponse(response);
                
                // Buffer the response content if we've been asked to.
                if (ShouldBufferResponse(completionOption, request))
                {
                    if (HttpTelemetry.Log.IsEnabled() && telemetryStarted)
                    {
                        HttpTelemetry.Log.ResponseContentStart();
                        responseContentTelemetryStarted = true;
                    }
                    
                    await response.Content
                        		 .LoadIntoBufferAsync(_maxResponseContentBufferSize, cts.Token)
                        		 .ConfigureAwait(false);
                }
                
                return response;
            }
            catch (Exception e)
            {
                HandleFailure(e, telemetryStarted, response, cts, originalCancellationToken, pendingRequestsCts);
                throw;
            }
            finally
            {
                inishSend(cts, disposeCts, telemetryStarted, responseContentTelemetryStarted);                
            }
        }
    }
    
    private void CheckRequestBeforeSend(HttpRequestMessage request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request), SR.net_http_handler_norequest);
        }
        
        CheckDisposed();
        CheckRequestMessage(request);
        
        SetOperationStarted();
        
        // PrepareRequestMessage will resolve the request address against the base address.
        PrepareRequestMessage(request);
    }
    
    private static void ThrowForNullResponse([NotNull] HttpResponseMessage? response)
    {
        if (response is null)
        {
            throw new InvalidOperationException(SR.net_http_handler_noresponse);
        }
    }
    
    private static bool ShouldBufferResponse(HttpCompletionOption completionOption, HttpRequestMessage request) =>
        completionOption == HttpCompletionOption.ResponseContentRead &&
        !string.Equals(request.Method.Method, "HEAD", StringComparison.OrdinalIgnoreCase);
    
    private void HandleFailure(Exception e, bool telemetryStarted, HttpResponseMessage? response, CancellationTokenSource cts, CancellationToken cancellationToken, CancellationTokenSource pendingRequestsCts)
    {
        LogRequestFailed(telemetryStarted);
        
        response?.Dispose();
        
        Exception? toThrow = null;
        
        if (e is OperationCanceledException oce)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (oce.CancellationToken != cancellationToken)
                {
                    // We got a cancellation exception, and the caller requested cancellation, but the exception doesn't contain that token.
                    // Massage things so that the cancellation exception we propagate appropriately contains the caller's token (it's possible
                    // multiple things caused cancellation, in which case we can attribute it to the caller's token, or it's possible the
                    // exception contains the linked token source, in which case that token isn't meaningful to the caller).
                    e = toThrow = new TaskCanceledException(oce.Message, oce.InnerException, cancellationToken);
                }
            }
            else if (!pendingRequestsCts.IsCancellationRequested)
            {
                // If this exception is for cancellation, but cancellation wasn't requested, either by the caller's token or by the pending requests source,
                // the only other cause could be a timeout.  Treat it as such.
                e = toThrow = new TaskCanceledException(SR.Format(SR.net_http_request_timedout, _timeout.TotalSeconds), new TimeoutException(e.Message, e), oce.CancellationToken);
            }
        }
        else if (e is HttpRequestException && cts.IsCancellationRequested) // if cancellationToken is canceled, cts will also be canceled
        {
            // If the cancellation token source was canceled, race conditions abound, and we consider the failure to be
            // caused by the cancellation (e.g. WebException when reading from canceled response stream).
            e = toThrow = new OperationCanceledException(cancellationToken.IsCancellationRequested ? cancellationToken : cts.Token);
        }
        
        if (NetEventSource.Log.IsEnabled()) NetEventSource.Error(this, e);
        
        if (toThrow != null)
        {
            throw toThrow;
        }
    }
    
    private static bool StartSend(HttpRequestMessage request)
    {
        if (HttpTelemetry.Log.IsEnabled() && request.RequestUri != null)
        {
            HttpTelemetry.Log.RequestStart(request);
            return true;
        }
        
        return false;
    }
    
    private static void FinishSend(CancellationTokenSource cts, bool disposeCts, bool telemetryStarted, bool responseContentTelemetryStarted)
    {
        // Log completion.
        if (HttpTelemetry.Log.IsEnabled() && telemetryStarted)
        {
            if (responseContentTelemetryStarted)
            {
                HttpTelemetry.Log.ResponseContentStop();
            }
            
            HttpTelemetry.Log.RequestStop();
        }
        
        // Dispose of the CancellationTokenSource if it was created specially for this request
        // rather than being used across multiple requests.
        if (disposeCts)
        {
            cts.Dispose();
        }
        
            // This method used to also dispose of the request content, e.g.:
            //     request.Content?.Dispose();
            // This has multiple problems:
            //   1. It prevents code from reusing request content objects for subsequent requests,
            //      as disposing of the object likely invalidates it for further use.
            //   2. It prevents the possibility of partial or full duplex communication, even if supported
            //      by the handler, as the request content may still be in use even if the response
            //      (or response headers) has been received.
            // By changing this to not dispose of the request content, disposal may end up being
            // left for the finalizer to handle, or the developer can explicitly dispose of the
            // content when they're done with it.  But it allows request content to be reused,
            // and more importantly it enables handlers that allow receiving of the response before
            // fully sending the request.  Prior to this change, a handler that supported duplex communication
            // would fail trying to access certain sites, if the site sent its response before it had
            // completely received the request: CurlHandler might then find that the request content
            // was disposed of while it still needed to read from it.
    }
    
    public void CancelPendingRequests()
    {
        CheckDisposed();
        
        // With every request we link this cancellation token source.
        CancellationTokenSource currentCts = Interlocked.Exchange(ref _pendingRequestsCts, new CancellationTokenSource());
        
        currentCts.Cancel();
        currentCts.Dispose();
    }
    
       
        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;

                // Cancel all pending requests (if any). Note that we don't call CancelPendingRequests() but cancel
                // the CTS directly. The reason is that CancelPendingRequests() would cancel the current CTS and create
                // a new CTS. We don't want a new CTS in this case.
                _pendingRequestsCts.Cancel();
                _pendingRequestsCts.Dispose();
            }

            base.Dispose(disposing);
        }

        #endregion

        #region Private Helpers

        private void SetOperationStarted()
        {
            // This method flags the HttpClient instances as "active". I.e. we executed at least one request (or are
            // in the process of doing so). This information is used to lock-down all property setters. Once a
            // Send/SendAsync operation started, no property can be changed.
            if (!_operationStarted)
            {
                _operationStarted = true;
            }
        }

        private void CheckDisposedOrStarted()
        {
            CheckDisposed();
            if (_operationStarted)
            {
                throw new InvalidOperationException(SR.net_http_operation_started);
            }
        }

        private void CheckDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().ToString());
            }
        }

        private static void CheckRequestMessage(HttpRequestMessage request)
        {
            if (!request.MarkAsSent())
            {
                throw new InvalidOperationException(SR.net_http_client_request_already_sent);
            }
        }

        private void PrepareRequestMessage(HttpRequestMessage request)
        {
            Uri? requestUri = null;
            if ((request.RequestUri == null) && (_baseAddress == null))
            {
                throw new InvalidOperationException(SR.net_http_client_invalid_requesturi);
            }
            if (request.RequestUri == null)
            {
                requestUri = _baseAddress;
            }
            else
            {
                // If the request Uri is an absolute Uri, just use it. Otherwise try to combine it with the base Uri.
                if (!request.RequestUri.IsAbsoluteUri)
                {
                    if (_baseAddress == null)
                    {
                        throw new InvalidOperationException(SR.net_http_client_invalid_requesturi);
                    }
                    else
                    {
                        requestUri = new Uri(_baseAddress, request.RequestUri);
                    }
                }
            }

            // We modified the original request Uri. Assign the new Uri to the request message.
            if (requestUri != null)
            {
                request.RequestUri = requestUri;
            }

            // Add default headers
            if (_defaultRequestHeaders != null)
            {
                request.Headers.AddHeaders(_defaultRequestHeaders);
            }
        }

        private (CancellationTokenSource TokenSource, bool DisposeTokenSource, CancellationTokenSource PendingRequestsCts) PrepareCancellationTokenSource(CancellationToken cancellationToken)
        {
            // We need a CancellationTokenSource to use with the request.  We always have the global
            // _pendingRequestsCts to use, plus we may have a token provided by the caller, and we may
            // have a timeout.  If we have a timeout or a caller-provided token, we need to create a new
            // CTS (we can't, for example, timeout the pending requests CTS, as that could cancel other
            // unrelated operations).  Otherwise, we can use the pending requests CTS directly.

            // Snapshot the current pending requests cancellation source. It can change concurrently due to cancellation being requested
            // and it being replaced, and we need a stable view of it: if cancellation occurs and the caller's token hasn't been canceled,
            // it's either due to this source or due to the timeout, and checking whether this source is the culprit is reliable whereas
            // it's more approximate checking elapsed time.
            CancellationTokenSource pendingRequestsCts = _pendingRequestsCts;

            bool hasTimeout = _timeout != s_infiniteTimeout;
            if (hasTimeout || cancellationToken.CanBeCanceled)
            {
                CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, pendingRequestsCts.Token);
                if (hasTimeout)
                {
                    cts.CancelAfter(_timeout);
                }

                return (cts, DisposeTokenSource: true, pendingRequestsCts);
            }

            return (pendingRequestsCts, DisposeTokenSource: false, pendingRequestsCts);
        }

        private static void CheckBaseAddress(Uri? baseAddress, string parameterName)
        {
            if (baseAddress == null)
            {
                return; // It's OK to not have a base address specified.
            }

            if (!baseAddress.IsAbsoluteUri)
            {
                throw new ArgumentException(SR.net_http_client_absolute_baseaddress_required, parameterName);
            }

            if (!HttpUtilities.IsHttpUri(baseAddress))
            {
                throw new ArgumentException(HttpUtilities.InvalidUriMessage, parameterName);
            }
        }

        private static bool IsNativeHandlerEnabled()
        {
            if (!AppContext.TryGetSwitch("System.Net.Http.UseNativeHttpHandler", out bool isEnabled))
            {
                return false;
            }

            return isEnabled;
        }

        private Uri? CreateUri(string? uri) =>
            string.IsNullOrEmpty(uri) ? null : new Uri(uri, UriKind.RelativeOrAbsolute);

        private HttpRequestMessage CreateRequestMessage(HttpMethod method, Uri? uri) =>
            new HttpRequestMessage(method, uri) { Version = _defaultRequestVersion, VersionPolicy = _defaultVersionPolicy };
        #endregion Private Helpers
    }
```

###### 5.4.1.1 get

```c#
public partial class HttpClient : HttpMessageInvoker
{
    public Task<HttpResponseMessage> GetAsync(string? requestUri) =>
        GetAsync(CreateUri(requestUri));
    
    public Task<HttpResponseMessage> GetAsync(Uri? requestUri) =>
        GetAsync(requestUri, DefaultCompletionOption);
    
    public Task<HttpResponseMessage> GetAsync(
        string? requestUri, 
        HttpCompletionOption completionOption) =>
        	GetAsync(CreateUri(requestUri), completionOption);
    
    public Task<HttpResponseMessage> GetAsync(
        Uri? requestUri, 
        HttpCompletionOption completionOption) =>
        	GetAsync(requestUri, completionOption, CancellationToken.None);
    
    public Task<HttpResponseMessage> GetAsync(
        string? requestUri, 
        CancellationToken cancellationToken) =>
        	GetAsync(CreateUri(requestUri), cancellationToken);
    
    public Task<HttpResponseMessage> GetAsync(
        Uri? requestUri, 
        CancellationToken cancellationToken) =>
        	GetAsync(requestUri, DefaultCompletionOption, cancellationToken);
    
    public Task<HttpResponseMessage> GetAsync(
        string? requestUri, 
        HttpCompletionOption completionOption, 
        CancellationToken cancellationToken) =>
            GetAsync(CreateUri(requestUri), completionOption, cancellationToken);

    public Task<HttpResponseMessage> GetAsync(
        Uri? requestUri, 
        HttpCompletionOption completionOption, 
        CancellationToken cancellationToken) =>
            SendAsync(CreateRequestMessage(HttpMethod.Get, requestUri), completionOption, cancellationToken);
}

```

###### 5.4.1.2 get string

```c#
public partial class HttpClient : HttpMessageInvoker
{
    public Task<string> GetStringAsync(string? requestUri) =>
        GetStringAsync(CreateUri(requestUri));
    
    public Task<string> GetStringAsync(Uri? requestUri) =>
        GetStringAsync(requestUri, CancellationToken.None);
    
    public Task<string> GetStringAsync(string? requestUri, CancellationToken cancellationToken) =>
        GetStringAsync(CreateUri(requestUri), cancellationToken);
    
    public Task<string> GetStringAsync(Uri? requestUri, CancellationToken cancellationToken)
    {
        HttpRequestMessage request = CreateRequestMessage(HttpMethod.Get, requestUri);
        
        // Called outside of async state machine to propagate certain exception even without awaiting the returned task.
        CheckRequestBeforeSend(request);
        
        return GetStringAsyncCore(request, cancellationToken);
    }
    
    private async Task<string> GetStringAsyncCore(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        bool telemetryStarted = StartSend(request);
        bool responseContentTelemetryStarted = false;
        
        (CancellationTokenSource cts, bool disposeCts, CancellationTokenSource pendingRequestsCts) = 
            PrepareCancellationTokenSource(cancellationToken);
        
        HttpResponseMessage? response = null;
        try
        {
            // Wait for the response message and make sure it completed successfully.
            response = await base.SendAsync(request, cts.Token).ConfigureAwait(false);
            ThrowForNullResponse(response);
            response.EnsureSuccessStatusCode();
            
            // Get the response content.
            HttpContent c = response.Content;
            if (HttpTelemetry.Log.IsEnabled() && telemetryStarted)
            {
                HttpTelemetry.Log.ResponseContentStart();
                responseContentTelemetryStarted = true;
            }
            
            // Since the underlying byte[] will never be exposed, we use an ArrayPool-backed
            // stream to which we copy all of the data from the response.
            using Stream responseStream = c.TryReadAsStream() ?? await c.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
            using var buffer = new HttpContent.LimitArrayPoolWriteStream(
                _maxResponseContentBufferSize, 
                (int)c.Headers.ContentLength.GetValueOrDefault());
            
            try
            {
                await responseStream.CopyToAsync(buffer, cts.Token).ConfigureAwait(false);
            }
            catch (Exception e) when (HttpContent.StreamCopyExceptionNeedsWrapping(e))
            {
                throw HttpContent.WrapStreamCopyException(e);
            }
            
            if (buffer.Length > 0)
            {
                // Decode and return the data from the buffer.
                return HttpContent.ReadBufferAsString(buffer.GetBuffer(), c.Headers);
            }
            
            // No content to return.
            return string.Empty;
        }
        catch (Exception e)
        {
            HandleFailure(e, telemetryStarted, response, cts, cancellationToken, pendingRequestsCts);
            throw;
        }
        finally
        {
            FinishSend(cts, disposeCts, telemetryStarted, responseContentTelemetryStarted);
        }
    }
}

```

###### 5.4.1.3 get stream

```c#
public partial class HttpClient : HttpMessageInvoker
{
    public Task<Stream> GetStreamAsync(string? requestUri) =>
        GetStreamAsync(CreateUri(requestUri));
    
    public Task<Stream> GetStreamAsync(string? requestUri, CancellationToken cancellationToken) =>
        GetStreamAsync(CreateUri(requestUri), cancellationToken);
    
    public Task<Stream> GetStreamAsync(Uri? requestUri) =>
        GetStreamAsync(requestUri, CancellationToken.None);
    
    public Task<Stream> GetStreamAsync(Uri? requestUri, CancellationToken cancellationToken)
    {
        HttpRequestMessage request = CreateRequestMessage(HttpMethod.Get, requestUri);
        
        // Called outside of async state machine to propagate certain exception even without awaiting the returned task.
        CheckRequestBeforeSend(request);
        
        return GetStreamAsyncCore(request, cancellationToken);
    }
    
    private async Task<Stream> GetStreamAsyncCore(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        bool telemetryStarted = StartSend(request);
        
        (CancellationTokenSource cts, bool disposeCts, CancellationTokenSource pendingRequestsCts) = 
            PrepareCancellationTokenSource(cancellationToken);
        HttpResponseMessage? response = null;
        try
        {
            // Wait for the response message and make sure it completed successfully.
            response = await base.SendAsync(request, cts.Token).ConfigureAwait(false);
            ThrowForNullResponse(response);
            response.EnsureSuccessStatusCode();
            
            HttpContent c = response.Content;
            return c.TryReadAsStream() ?? await c.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            HandleFailure(e, telemetryStarted, response, cts, cancellationToken, pendingRequestsCts);
            throw;
        }
        finally
        {
            FinishSend(cts, disposeCts, telemetryStarted, responseContentTelemetryStarted: false);
        }
    }    
}

```

###### 5.4.1.4 get byte array

```c#
public partial class HttpClient : HttpMessageInvoker
{
    public Task<byte[]> GetByteArrayAsync(string? requestUri) =>
        GetByteArrayAsync(CreateUri(requestUri));
    
    public Task<byte[]> GetByteArrayAsync(Uri? requestUri) =>
        GetByteArrayAsync(requestUri, CancellationToken.None);
    
    public Task<byte[]> GetByteArrayAsync(string? requestUri, CancellationToken cancellationToken) =>
        GetByteArrayAsync(CreateUri(requestUri), cancellationToken);
    
    public Task<byte[]> GetByteArrayAsync(Uri? requestUri, CancellationToken cancellationToken)
    {
        HttpRequestMessage request = CreateRequestMessage(HttpMethod.Get, requestUri);
        
        // Called outside of async state machine to propagate certain exception even without awaiting the returned task.
        CheckRequestBeforeSend(request);
        
        return GetByteArrayAsyncCore(request, cancellationToken);
    }
    
    private async Task<byte[]> GetByteArrayAsyncCore(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        bool telemetryStarted = StartSend(request);
        bool responseContentTelemetryStarted = false;
        
        (CancellationTokenSource cts, bool disposeCts, CancellationTokenSource pendingRequestsCts) = 
            PrepareCancellationTokenSource(cancellationToken);
        HttpResponseMessage? response = null;
        try
        {
            // Wait for the response message and make sure it completed successfully.
            response = await base.SendAsync(request, cts.Token).ConfigureAwait(false);
            ThrowForNullResponse(response);
            response.EnsureSuccessStatusCode();
            
            // Get the response content.
            HttpContent c = response.Content;
            if (HttpTelemetry.Log.IsEnabled() && telemetryStarted)
            {
                HttpTelemetry.Log.ResponseContentStart();
                responseContentTelemetryStarted = true;
            }
            
                // If we got a content length, then we assume that it's correct and create a MemoryStream
                // to which the content will be transferred.  That way, assuming we actually get the exact
                // amount we were expecting, we can simply return the MemoryStream's underlying buffer.
                // If we didn't get a content length, then we assume we're going to have to grow
                // the buffer potentially several times and that it's unlikely the underlying buffer
                // at the end will be the exact size needed, in which case it's more beneficial to use
                // ArrayPool buffers and copy out to a new array at the end.
            long? contentLength = c.Headers.ContentLength;
            using Stream buffer = contentLength.HasValue 
                ? new HttpContent.LimitMemoryStream(_maxResponseContentBufferSize, (int)contentLength.GetValueOrDefault()) 
                : new HttpContent.LimitArrayPoolWriteStream(_maxResponseContentBufferSize);
            
            using Stream responseStream = c.TryReadAsStream() ?? await c.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
            try
            {
                await responseStream.CopyToAsync(buffer, cts.Token).ConfigureAwait(false);
            }
            catch (Exception e) when (HttpContent.StreamCopyExceptionNeedsWrapping(e))
            {
                throw HttpContent.WrapStreamCopyException(e);
            }
            
            return buffer.Length == 0 
                ? Array.Empty<byte>() 
                : buffer is HttpContent.LimitMemoryStream lms 
                    ? lms.GetSizedBuffer() 
                    : ((HttpContent.LimitArrayPoolWriteStream)buffer).ToArray();
        }
        catch (Exception e)
        {
            HandleFailure(e, telemetryStarted, response, cts, cancellationToken, pendingRequestsCts);
            throw;
        }
        finally
        {
            FinishSend(cts, disposeCts, telemetryStarted, responseContentTelemetryStarted);
        }
    }    
}

```

###### 5.4.1.5 post

```c#
public partial class HttpClient : HttpMessageInvoker
{
    public Task<HttpResponseMessage> PostAsync(string? requestUri, HttpContent? content) =>
        PostAsync(CreateUri(requestUri), content);
    
    public Task<HttpResponseMessage> PostAsync(Uri? requestUri, HttpContent? content) =>
        PostAsync(requestUri, content, CancellationToken.None);
    
    public Task<HttpResponseMessage> PostAsync(string? requestUri, HttpContent? content, CancellationToken cancellationToken) =>
        PostAsync(CreateUri(requestUri), content, cancellationToken);
    
    public Task<HttpResponseMessage> PostAsync(Uri? requestUri, HttpContent? content, CancellationToken cancellationToken)
    {
        HttpRequestMessage request = CreateRequestMessage(HttpMethod.Post, requestUri);
        request.Content = content;
        return SendAsync(request, cancellationToken);
    }
}

```

###### 5.4.1.x put

```c#
public partial class HttpClient : HttpMessageInvoker
{
    public Task<HttpResponseMessage> PutAsync(string? requestUri, HttpContent? content) =>
        PutAsync(CreateUri(requestUri), content);
    
    public Task<HttpResponseMessage> PutAsync(Uri? requestUri, HttpContent? content) =>
        PutAsync(requestUri, content, CancellationToken.None);
    
    public Task<HttpResponseMessage> PutAsync(string? requestUri, HttpContent? content, CancellationToken cancellationToken) =>
        PutAsync(CreateUri(requestUri), content, cancellationToken);
    
    public Task<HttpResponseMessage> PutAsync(Uri? requestUri, HttpContent? content, CancellationToken cancellationToken)
    {
        HttpRequestMessage request = CreateRequestMessage(HttpMethod.Put, requestUri);
        request.Content = content;
        return SendAsync(request, cancellationToken);
    }
}

```

###### y

```c#
public partial class HttpClient : HttpMessageInvoker
{
    public Task<HttpResponseMessage> PatchAsync(string? requestUri, HttpContent? content) =>
        PatchAsync(CreateUri(requestUri), content);
    
    public Task<HttpResponseMessage> PatchAsync(Uri? requestUri, HttpContent? content) =>
        PatchAsync(requestUri, content, CancellationToken.None);
    
    public Task<HttpResponseMessage> PatchAsync(string? requestUri, HttpContent? content, CancellationToken cancellationToken) =>
        PatchAsync(CreateUri(requestUri), content, cancellationToken);
    
    public Task<HttpResponseMessage> PatchAsync(Uri? requestUri, HttpContent? content, CancellationToken cancellationToken)
    {
        HttpRequestMessage request = CreateRequestMessage(HttpMethod.Patch, requestUri);
        request.Content = content;
        return SendAsync(request, cancellationToken);
    }
}

```

```c#
public partial class HttpClient : HttpMessageInvoker
{
    public Task<HttpResponseMessage> DeleteAsync(string? requestUri) =>
        DeleteAsync(CreateUri(requestUri));
    
    public Task<HttpResponseMessage> DeleteAsync(Uri? requestUri) =>
        DeleteAsync(requestUri, CancellationToken.None);
    
    public Task<HttpResponseMessage> DeleteAsync(string? requestUri, CancellationToken cancellationToken) =>
        DeleteAsync(CreateUri(requestUri), cancellationToken);
    
    public Task<HttpResponseMessage> DeleteAsync(Uri? requestUri, CancellationToken cancellationToken) =>
        SendAsync(CreateRequestMessage(HttpMethod.Delete, requestUri), cancellationToken);
}

```



### 6. http client factory service

#### 6.1 http message handler builder

##### 6.1.1 abstract

```c#
public abstract class HttpMessageHandlerBuilder
{        
    public abstract string Name { get; set; }   
    
    // This property is sensitive to the value of "HttpClientFactoryOptions.SuppressHandlerScope". 
    //   - If <c>true</c> this property will be a reference to the application's root service provider. 
    //   - If <c>false</c> (default) this will be a reference to a scoped service provider that has the same lifetime 
    //     as the handler being created.    
    public virtual IServiceProvider Services { get; }
    
    public abstract HttpMessageHandler PrimaryHandler { get; set; }        
    public abstract IList<DelegatingHandler> AdditionalHandlers { get; }
                
    // 方法- builde，在派生类实现
    public abstract HttpMessageHandler Build();
    
    // create handler pipeline
    protected internal static HttpMessageHandler CreateHandlerPipeline(
        HttpMessageHandler primaryHandler, 
        IEnumerable<DelegatingHandler> additionalHandlers)
    {        
        if (primaryHandler == null)
        {
            throw new ArgumentNullException(nameof(primaryHandler));
        }        
        if (additionalHandlers == null)
        {
            throw new ArgumentNullException(nameof(additionalHandlers));
        }
        
        IReadOnlyList<DelegatingHandler> additionalHandlersList = 
            additionalHandlers as IReadOnlyList<DelegatingHandler> ?? additionalHandlers.ToArray();
        
        HttpMessageHandler next = primaryHandler;
        
        // 遍历 additional handlers 构建（复合的）handler，
        // additional handler[0], [1] ... primary handler
        for (int i = additionalHandlersList.Count - 1; i >= 0; i--)
        {
            DelegatingHandler handler = additionalHandlersList[i];
            if (handler == null)
            {
                string message = SR.Format(
                    SR.HttpMessageHandlerBuilder_AdditionalHandlerIsNull, 
                    nameof(additionalHandlers));
                
                throw new InvalidOperationException(message);
            }
            
            // Checking for this allows us to catch cases where someone has tried to re-use a handler. 
            // That really won't work the way you want and it can be tricky for callers to figure out.
            if (handler.InnerHandler != null)
            {
                string message = SR.Format(
                    SR.HttpMessageHandlerBuilder_AdditionHandlerIsInvalid,
                    nameof(DelegatingHandler.InnerHandler),
                    nameof(DelegatingHandler),
                    nameof(HttpMessageHandlerBuilder),
                    Environment.NewLine,
                    handler);
                throw new InvalidOperationException(message);
            }
            
            handler.InnerHandler = next;
            next = handler;
        }
        
        return next;
    }
}

```

##### 6.1.2 default impl

```c#
internal sealed class DefaultHttpMessageHandlerBuilder : HttpMessageHandlerBuilder
{
    // name
    private string _name;    
    public override string Name
    {
        get => _name;
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            
            _name = value;
        }
    }
    
    public override IServiceProvider Services { get; }
    
    public override HttpMessageHandler PrimaryHandler { get; set; } = new HttpClientHandler();    
    public override IList<DelegatingHandler> AdditionalHandlers { get; } = new List<DelegatingHandler>();
            
    public DefaultHttpMessageHandlerBuilder(IServiceProvider services)
    {
        Services = services;
    }
    
    // 重写方法- build，由 create handler pipeline 构建 handler
    public override HttpMessageHandler Build()
    {
        if (PrimaryHandler == null)
        {
            string message = SR.Format(
                SR.HttpMessageHandlerBuilder_PrimaryHandlerIsNull, 
                nameof(PrimaryHandler));
            
            throw new InvalidOperationException(message);
        }
        
        return CreateHandlerPipeline(PrimaryHandler, AdditionalHandlers);
    }
}

```

##### 6.1.3 http message handler builder filter

```c#
public interface IHttpMessageHandlerBuilderFilter
{    
    Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next);
}

```

###### 6.1.3.1 logger fitler

```c#
internal sealed class LoggingHttpMessageHandlerBuilderFilter : IHttpMessageHandlerBuilderFilter
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IOptionsMonitor<HttpClientFactoryOptions> _optionsMonitor;
    
    public LoggingHttpMessageHandlerBuilderFilter(
        ILoggerFactory loggerFactory, 
        IOptionsMonitor<HttpClientFactoryOptions> optionsMonitor)
    {
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }
        
        if (optionsMonitor == null)
        {
            throw new ArgumentNullException(nameof(optionsMonitor));
        }

        _loggerFactory = loggerFactory;
        _optionsMonitor = optionsMonitor;
    }
    
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        if (next == null)
        {
            throw new ArgumentNullException(nameof(next));
        }
        
        return (builder) =>
        {
            // Run other configuration first, we want to decorate.
            next(builder);
            
            string loggerName = !string.IsNullOrEmpty(builder.Name) ? builder.Name : "Default";
            
            // We want all of our logging message to show up as-if they are coming from HttpClient,
            // but also to include the name of the client for more fine-grained control.
            ILogger outerLogger = _loggerFactory.CreateLogger($"System.Net.Http.HttpClient.{loggerName}.LogicalHandler");
            ILogger innerLogger = _loggerFactory.CreateLogger($"System.Net.Http.HttpClient.{loggerName}.ClientHandler");
            
            HttpClientFactoryOptions options = _optionsMonitor.Get(builder.Name);
            
            // The 'scope' handler goes first so it can surround everything.
            builder.AdditionalHandlers.Insert(0, new LoggingScopeHttpMessageHandler(outerLogger, options));
            
            // We want this handler to be last so we can log details about the request after
            // service discovery and security happen.
            builder.AdditionalHandlers.Add(new LoggingHttpMessageHandler(innerLogger, options));            
        };
    }
}

```



#### 6.2 http message handler & client factory

##### 6.2.1 http message handler factory

```c#
// 接口
public interface IHttpMessageHandlerFactory
{    
    // The default "IHttpMessageHandlerFactory" implementation may cache the underlying "HttpMessageHandler"
    // instances to improve performance.    
    HttpMessageHandler CreateHandler(string name);
}

// 扩展方法
public static class HttpMessageHandlerFactoryExtensions
{
    // create handler without name
    public static HttpMessageHandler CreateHandler(this IHttpMessageHandlerFactory factory)
    {
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }
        
        return factory.CreateHandler(Options.DefaultName);
    }
}

```

##### 6.2.2 http client factory

```c#
// 接口
public interface IHttpClientFactory
{    
    // Each call to "CreateClient(string)" is guaranteed to return a new "HttpClient" instance. 
    // It is generally not necessary to dispose of the "HttpClient" as the "IHttpClientFactory" tracks and 
    // disposes resources used by the "HttpClient".    
    // Callers are also free to mutate the returned "HttpClient" instance's public properties as desired.        
    HttpClient CreateClient(string name);
}

// 扩展方法
public static class HttpClientFactoryExtensions
{     
    // Creates a new "HttpClient" using the default configuration.    
    public static HttpClient CreateClient(this IHttpClientFactory factory)
    {
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }
        
        return factory.CreateClient(Options.DefaultName);
    }
}

```

##### 6.2.3 default http client (& handler) factory

```c#
internal class DefaultHttpClientFactory : IHttpClientFactory, IHttpMessageHandlerFactory
{
    /* core */
    
    private readonly ILogger _logger;
    private readonly IServiceProvider _services;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<HttpClientFactoryOptions> _optionsMonitor;
    private readonly IHttpMessageHandlerBuilderFilter[] _filters;
    private readonly Func<string, Lazy<ActiveHandlerTrackingEntry>> _entryFactory;
         
    /* cleanup */
    
    // We use a new timer for each regular cleanup cycle, protected with a lock. 
    // Note that this scheme doesn't give us anything to dispose, as the timer is started/stopped as needed.    
    // There's no need for the factory itself to be disposable. If you stop using it, eventually everything will
    // get reclaimed.
    private Timer _cleanupTimer;
    private readonly object _cleanupTimerLock;
    private readonly object _cleanupActiveLock;
    
    // Default time of 10s for cleanup seems reasonable.
    // Quick math: 10 distinct named clients * expiry time >= 1s = approximate cleanup queue of 100 items    
    private readonly TimeSpan DefaultCleanupInterval = TimeSpan.FromSeconds(10);
    
    // cleanup callback
    private static readonly TimerCallback _cleanupCallback = (s) => ((DefaultHttpClientFactory)s).CleanupTimer_Tick();
    
    /* handlers */
    
    // Collection of 'active' handlers.    
    // Using lazy for synchronization to ensure that only one instance of HttpMessageHandler is created for each name.    
    internal readonly ConcurrentDictionary<string, Lazy<ActiveHandlerTrackingEntry>> _activeHandlers;    
    
    // Collection of 'expired' but not yet disposed handlers.    
    // Used when we're rotating handlers so that we can dispose HttpMessageHandler instances once they are eligible for GC.    
    internal readonly ConcurrentQueue<ExpiredHandlerTrackingEntry> _expiredHandlers;
    
    // expiry call back
    private readonly TimerCallback _expiryCallback;
    
    public DefaultHttpClientFactory(
        IServiceProvider services,
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory,
        IOptionsMonitor<HttpClientFactoryOptions> optionsMonitor,
        IEnumerable<IHttpMessageHandlerBuilderFilter> filters)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }        
        if (scopeFactory == null)
        {
            throw new ArgumentNullException(nameof(scopeFactory));
        }        
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }        
        if (optionsMonitor == null)
        {
            throw new ArgumentNullException(nameof(optionsMonitor));
        }        
        if (filters == null)
        {
            throw new ArgumentNullException(nameof(filters));
        }
        
        // 注入 core  services
        _services = services;
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _filters = filters.ToArray();
        
        _logger = loggerFactory.CreateLogger<DefaultHttpClientFactory>();
        
        // 创建 active handlers 容器，case-sensitive because named options is.
        _activeHandlers = new ConcurrentDictionary<string, Lazy<ActiveHandlerTrackingEntry>>(StringComparer.Ordinal);
        // 
        _entryFactory = (name) =>
        {
            return new Lazy<ActiveHandlerTrackingEntry>(
                () =>
                	{
                        // 1- 
                        return CreateHandlerEntry(name);
                    }, 
                LazyThreadSafetyMode.ExecutionAndPublication);
        };
        // 
        _expiredHandlers = new ConcurrentQueue<ExpiredHandlerTrackingEntry>();
        
        // 2-
        _expiryCallback = ExpiryTimer_Tick;
        
        _cleanupTimerLock = new object();
        _cleanupActiveLock = new object();
    }
                    
    // 1- create handler entry
    internal ActiveHandlerTrackingEntry CreateHandlerEntry(string name)
    {
        // services => root services
        IServiceProvider services = _services;
        var scope = (IServiceScope)null;
        
        // 如果 http client factory options 没有标记 suppress handler scope，
        // 创建 scoped services
        HttpClientFactoryOptions options = _optionsMonitor.Get(name);
        if (!options.SuppressHandlerScope)
        {
            scope = _scopeFactory.CreateScope();
            services = scope.ServiceProvider;
        }
        
        try
        {
            // 解析 http message handler builder，注入 name
            HttpMessageHandlerBuilder builder = services.GetRequiredService<HttpMessageHandlerBuilder>();
            builder.Name = name;
                        
            // 1.1- 遍历 http client factory options 的 message handler builder actions，
            // 配置到 => configuration (action of hanlder builder)
            Action<HttpMessageHandlerBuilder> configure = Configure;
            // 遍历 http message hanlder builder filters，配置到 => configuration (action of handler builder)
            for (int i = _filters.Length - 1; i >= 0; i--)
            {
                configure = _filters[i].Configure(configure);
            }
            
            // 使用 action 配置 http message handler builder
            configure(builder);
            
            // 由 handler builder 构建 handler，封装到 lifetime tracking handler
            // Wrap the handler so we can ensure the inner handler outlives the outer handler.
            var handler = new LifetimeTrackingHttpMessageHandler(builder.Build());
            
            // 创建 active handler tracking entry 并返回
            // Note that we can't start the timer here. That would introduce a very very subtle race condition with 
            // very short expiry times. We need to wait until we've actually handed out the handler once to start the timer.            
            // Otherwise it would be possible that we start the timer here, immediately expire it (very short timer) and then 
            // dispose it without ever creating a client. 
            return new ActiveHandlerTrackingEntry(name, handler, scope, options.HandlerLifetime);
            
            // 1.1 -
            void Configure(HttpMessageHandlerBuilder b)
            {
                for (int i = 0; i < options.HttpMessageHandlerBuilderActions.Count; i++)
                {
                    options.HttpMessageHandlerBuilderActions[i](b);                    
                }
            }
        }
        catch
        {
            // If something fails while creating the handler, dispose the services.
            scope?.Dispose();
            throw;
        }
    }
    
    // 2-
    internal void ExpiryTimer_Tick(object state)
    {
        // 转换 state => active handler
        var active = (ActiveHandlerTrackingEntry)state;
        
        // remove handler
        // The timer callback should be the only one removing from the active collection. If we can't find
        // our entry in the collection, then this is a bug.        
        bool removed = _activeHandlers.TryRemove(
            active.Name, 
            out Lazy<ActiveHandlerTrackingEntry> found);
        
        Debug.Assert(
            removed, 
            "Entry not found. We should always be able to remove the entry");
        Debug.Assert(
            object.ReferenceEquals(active, found.Value), 
            "Different entry found. The entry should not have been replaced");
        
        // At this point the handler is no longer 'active' and will not be handed out to any new clients.
        // However we haven't dropped our strong reference to the handler, so we can't yet determine if
        // there are still any other outstanding references (we know there is at least one).
        //
        // We use a different state object to track expired handlers. This allows any other thread that acquired
        // the 'active' entry to use it without safety problems.
        var expired = new ExpiredHandlerTrackingEntry(active);
        _expiredHandlers.Enqueue(expired);
        
        Log.HandlerExpired(_logger, active.Name, active.Lifetime);
        
        StartCleanupTimer();
    }
        
    internal virtual void StartCleanupTimer()
    {
        lock (_cleanupTimerLock)
        {
            if (_cleanupTimer == null)
            {
                _cleanupTimer = NonCapturingTimer.Create(
                    _cleanupCallback, 
                    this, 
                    DefaultCleanupInterval, 
                    Timeout.InfiniteTimeSpan);
            }
        }
    }            
       
    internal void CleanupTimer_Tick()
    {
        // Stop any pending timers, we'll restart the timer if there's anything left to process after cleanup.
        //
        // With the scheme we're using it's possible we could end up with some redundant cleanup operations.
        // This is expected and fine.
        //
        // An alternative would be to take a lock during the whole cleanup process. This isn't ideal because it
        // would result in threads executing ExpiryTimer_Tick as they would need to block on cleanup to figure out
        // whether we need to start the timer.
        StopCleanupTimer();
        
        if (!Monitor.TryEnter(_cleanupActiveLock))
        {
            // We don't want to run a concurrent cleanup cycle. This can happen if the cleanup cycle takes
            // a long time for some reason. Since we're running user code inside Dispose, it's definitely
            // possible.
            //
            // If we end up in that position, just make sure the timer gets started again. It should be cheap
            // to run a 'no-op' cleanup.
            StartCleanupTimer();
            return;
        }
        
        try
        {
            int initialCount = _expiredHandlers.Count;
            Log.CleanupCycleStart(_logger, initialCount);
            
            var stopwatch = ValueStopwatch.StartNew();
            
            int disposedCount = 0;
            for (int i = 0; i < initialCount; i++)
            {
                // Since we're the only one removing from _expired, TryDequeue must always succeed.
                _expiredHandlers.TryDequeue(out ExpiredHandlerTrackingEntry entry);
                Debug.Assert(entry != null, "Entry was null, we should always get an entry back from TryDequeue");
                
                if (entry.CanDispose)
                {
                    try
                    {
                        entry.InnerHandler.Dispose();
                        entry.Scope?.Dispose();
                        disposedCount++;
                    }
                    catch (Exception ex)
                    {
                        Log.CleanupItemFailed(_logger, entry.Name, ex);
                    }
                }
                else
                {
                    // If the entry is still live, put it back in the queue so we can process it
                    // during the next cleanup cycle.
                    _expiredHandlers.Enqueue(entry);
                }
            }
            
            Log.CleanupCycleEnd(_logger, stopwatch.GetElapsedTime(), disposedCount, _expiredHandlers.Count);
        }
        finally
        {
            Monitor.Exit(_cleanupActiveLock);
        }
        
        // We didn't totally empty the cleanup queue, try again later.
        if (!_expiredHandlers.IsEmpty)
        {
            StartCleanupTimer();
        }
    }
    
    internal virtual void StopCleanupTimer()
    {
        lock (_cleanupTimerLock)
        {
            _cleanupTimer.Dispose();
            _cleanupTimer = null;
        }
    }
    
    private static class Log
    {
        public static class EventIds
        {
            public static readonly EventId CleanupCycleStart = new EventId(100, "CleanupCycleStart");
            public static readonly EventId CleanupCycleEnd = new EventId(101, "CleanupCycleEnd");
            public static readonly EventId CleanupItemFailed = new EventId(102, "CleanupItemFailed");
            public static readonly EventId HandlerExpired = new EventId(103, "HandlerExpired");
        }
        
        private static readonly Action<ILogger, int, Exception> _cleanupCycleStart = 
            LoggerMessage.Define<int>(
            	LogLevel.Debug,
	            EventIds.CleanupCycleStart,
    	        "Starting HttpMessageHandler cleanup cycle with {InitialCount} items");
        
        private static readonly Action<ILogger, double, int, int, Exception> _cleanupCycleEnd = 
            LoggerMessage.Define<double, int, int>(
	            LogLevel.Debug,
    	        EventIds.CleanupCycleEnd,
        	    "Ending HttpMessageHandler cleanup cycle after {ElapsedMilliseconds}ms - processed: {DisposedCount} items - remaining: {RemainingItems} items");
        
        private static readonly Action<ILogger, string, Exception> _cleanupItemFailed = 
            LoggerMessage.Define<string>(
	            LogLevel.Error,
    	        EventIds.CleanupItemFailed,
        	    "HttpMessageHandler.Dispose() threw an unhandled exception for client: '{ClientName}'");
        
        private static readonly Action<ILogger, double, string, Exception> _handlerExpired = 
            LoggerMessage.Define<double, string>(
	            LogLevel.Debug,
    	        EventIds.HandlerExpired,
        	    "HttpMessageHandler expired after {HandlerLifetime}ms for client '{ClientName}'");
                
        public static void CleanupCycleStart(ILogger logger, int initialCount)
        {
            _cleanupCycleStart(logger, initialCount, null);
        }
        
        public static void CleanupCycleEnd(ILogger logger, TimeSpan duration, int disposedCount, int finalCount)
        {
            _cleanupCycleEnd(logger, duration.TotalMilliseconds, disposedCount, finalCount, null);
        }
        
        public static void CleanupItemFailed(ILogger logger, string clientName, Exception exception)
        {
            _cleanupItemFailed(logger, clientName, exception);
        }
        
        public static void HandlerExpired(ILogger logger, string clientName, TimeSpan lifetime)
        {
            _handlerExpired(logger, lifetime.TotalMilliseconds, clientName, null);
        }
    }
}

```

###### 6.2.3.1 active handler tracking entry

```c#
internal sealed class ActiveHandlerTrackingEntry
{
    private static readonly TimerCallback _timerCallback = (s) => ((ActiveHandlerTrackingEntry)s).Timer_Tick();
    private readonly object _lock;
    private bool _timerInitialized;
    private Timer _timer;
    private TimerCallback _callback;
    
    public LifetimeTrackingHttpMessageHandler Handler { get; private set; }    
    public TimeSpan Lifetime { get; }    
    public string Name { get; }    
    public IServiceScope Scope { get; }
    
    public ActiveHandlerTrackingEntry(
        string name,
        LifetimeTrackingHttpMessageHandler handler,
        IServiceScope scope,
        TimeSpan lifetime)
    {
        Name = name;
        Handler = handler;
        Scope = scope;
        Lifetime = lifetime;
        
        _lock = new object();
    }
            
    public void StartExpiryTimer(TimerCallback callback)
    {        
        if (Lifetime == Timeout.InfiniteTimeSpan)
        {
            return; // never expires.
        }
        
        if (Volatile.Read(ref _timerInitialized))
        {
            return;
        }
        
        StartExpiryTimerSlow(callback);
    }
    
    private void StartExpiryTimerSlow(TimerCallback callback)
    {
        Debug.Assert(Lifetime != Timeout.InfiniteTimeSpan);
        
        lock (_lock)
        {
            if (Volatile.Read(ref _timerInitialized))
            {
                return;
            }
            
            _callback = callback;
            _timer = NonCapturingTimer.Create(_timerCallback, this, Lifetime, Timeout.InfiniteTimeSpan);
            _timerInitialized = true;
        }
    }
    
    private void Timer_Tick()
    {
        Debug.Assert(_callback != null);
        Debug.Assert(_timer != null);
        
        lock (_lock)
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
                
                _callback(this);
            }
        }
    }
}

// expired handler tracking entry
internal sealed class ExpiredHandlerTrackingEntry
{
    private readonly WeakReference _livenessTracker;
    
    public string Name { get; }    
    public IServiceScope Scope { get; }
    public HttpMessageHandler InnerHandler { get; }  
    
    public bool CanDispose => !_livenessTracker.IsAlive;
    
    // IMPORTANT: don't cache a reference to `other` or `other.Handler` here.
    // We need to allow it to be GC'ed.
    public ExpiredHandlerTrackingEntry(ActiveHandlerTrackingEntry other)
    {
        Name = other.Name;
        Scope = other.Scope;
        
        _livenessTracker = new WeakReference(other.Handler);
        InnerHandler = other.Handler.InnerHandler;
    }                      
}

```

###### 6.2.3.2 lifetime tracking http message handler

```c#
internal sealed class LifetimeTrackingHttpMessageHandler : DelegatingHandler
{
    public LifetimeTrackingHttpMessageHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }
    
    protected override void Dispose(bool disposing)
    {
        // The lifetime of this is tracked separately by ActiveHandlerTrackingEntry
    }
}

```

###### 6.2.3.1 接口方法- create http message handler

```c#
internal class DefaultHttpClientFactory : IHttpClientFactory, IHttpMessageHandlerFactory
{
    public HttpMessageHandler CreateHandler(string name)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        // 解析（或创建）active handler tracking entry
        ActiveHandlerTrackingEntry entry = _activeHandlers.GetOrAdd(name, _entryFactory).Value;
        // start entry timer
        StartHandlerEntryTimer(entry);
        
        return entry.Handler;
    }
    
    // start handler entry timer
    internal virtual void StartHandlerEntryTimer(ActiveHandlerTrackingEntry entry)
    {
        entry.StartExpiryTimer(_expiryCallback);
    }
}

```

###### 6.2.3.2 接口方法- create http client

```c#
internal class DefaultHttpClientFactory : IHttpClientFactory, IHttpMessageHandlerFactory
{
    public HttpClient CreateClient(string name)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        // 创建 handler
        HttpMessageHandler handler = CreateHandler(name);
        // 创建 http client
        var client = new HttpClient(handler, disposeHandler: false);
        
        // 配置 http client
        HttpClientFactoryOptions options = _optionsMonitor.Get(name);
        for (int i = 0; i < options.HttpClientActions.Count; i++)
        {
            options.HttpClientActions[i](client);
        }
        
        return client;
    }
}

```

##### 6.2.4 typed client factory

```c#
// This sample shows the basic pattern for defining a typed client class.
// class ExampleClient
// {
//     private readonly HttpClient _httpClient;
//     private readonly ILogger _logger;
//
//     // typed clients can use constructor injection to access additional services
//     public ExampleClient(HttpClient httpClient, ILogger&lt;ExampleClient&gt; logger)
//     {
//         _httpClient = httpClient;
//         _logger = logger;
//     }
//
//     // typed clients can expose the HttpClient for application code to call directly
//     public HttpClient HttpClient => _httpClient;
//
//     // typed clients can also define methods that abstract usage of the HttpClient
//     public async Task SendHelloRequest()
//     {
//         var response = await _httpClient.GetAsync("/helloworld");
//         response.EnsureSuccessStatusCode();
//     }
// }

// This sample shows how to consume a typed client from an ASP.NET Core middleware.
// public void Configure(IApplicationBuilder app, ExampleClient exampleClient)
// {
//     app.Run(async (context) =>
//     {
//         var response = await _exampleClient.GetAsync("/helloworld");
//         await context.Response.WriteAsync("Remote server said: ");
//         await response.Content.CopyToAsync(context.Response.Body);
//     });
// }

// This sample shows how to consume a typed client from an ASP.NET Core MVC Controller.
// public class HomeController : ControllerBase(IApplicationBuilder app, ExampleClient exampleClient)
// {
//     private readonly ExampleClient _exampleClient;
//
//     public HomeController(ExampleClient exampleClient)
//     {
//         _exampleClient = exampleClient;
//     }
//
//     public async Task&lt;IActionResult&gt; Index()
//     {
//         var response = await _exampleClient.GetAsync("/helloworld");
//         var text = await response.Content.ReadAsStringAsync();
//         return Content("Remote server said: " + text, "text/plain");
//     };
// }
public interface ITypedHttpClientFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TClient>
{    
    TClient CreateClient(HttpClient httpClient);
}

```

###### 6.2.4.1 default typed http client factory

```c#
internal sealed class DefaultTypedHttpClientFactory<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TClient> : ITypedHttpClientFactory<TClient>
{
    private readonly Cache _cache;
    private readonly IServiceProvider _services;
    
    public DefaultTypedHttpClientFactory(Cache cache, IServiceProvider services)
    {
        if (cache == null)
        {
            throw new ArgumentNullException(nameof(cache));
        }        
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        _cache = cache;
        _services = services;
    }
    
    public TClient CreateClient(HttpClient httpClient)
    {
        if (httpClient == null)
        {
            throw new ArgumentNullException(nameof(httpClient));
        }
        
        return (TClient)_cache.Activator(_services, new object[] { httpClient });
    }
    
    // The Cache should be registered as a singleton, so it that it can act as a cache for the Activator. 
    // This allows the outer class to be registered as a transient, so that it doesn't close over the application root service provider.
    public class Cache
    {
        private static readonly Func<ObjectFactory> _createActivator = 
            () => ActivatorUtilities.CreateFactory(typeof(TClient), new Type[] { typeof(HttpClient), });
        
        private ObjectFactory _activator;
        private bool _initialized;
        private object _lock;
        
        public ObjectFactory Activator => LazyInitializer.EnsureInitialized(
            ref _activator,
            ref _initialized,
            ref _lock,
            _createActivator);
    }
}

```

#### 6.4 http client factory options

```c#
public class HttpClientFactoryOptions
{    
    internal static readonly TimeSpan MinimumHandlerLifetime = TimeSpan.FromSeconds(1);
        
    // Each named client can have its own configured handler lifetime value. 
    // The default value of this property is two minutes. Set the lifetime to "Timeout.InfiniteTimeSpan" to disable handler expiry.    
    private TimeSpan _handlerLifetime = TimeSpan.FromMinutes(2);
    public TimeSpan HandlerLifetime
        {
            get => _handlerLifetime;
            set
            {
                if (value != Timeout.InfiniteTimeSpan && value < MinimumHandlerLifetime)
                {
                    throw new ArgumentException(SR.HandlerLifetime_InvalidValue, nameof(value));
                }

                _handlerLifetime = value;
            }
        }
    
    // http message handler builder actions 容器
    public IList<Action<HttpMessageHandlerBuilder>> HttpMessageHandlerBuilderActions { get; } = 
        new List<Action<HttpMessageHandlerBuilder>>();
    
    // http client actions 容器
    public IList<Action<HttpClient>> HttpClientActions { get; } = 
        new List<Action<HttpClient>>();
                       
    // The "Func{T, R}" which determines whether to redact the HTTP header value before logging.       
    public Func<string, bool> ShouldRedactHeaderValue { get; set; } = (header) => false;

        
    // Gets or sets a value that determines whether the "IHttpClientFactory" will create a dependency injection scope 
    // when building an "HttpMessageHandler".
    //   - If <c>false</c> (default), a scope will be created, 
    //   - otherwise a scope will not be created.
    //
    // This option is provided for compatibility with existing applications. 
    // It is recommended to use the default setting for new applications.    
    public bool SuppressHandlerScope { get; set; }
}

```

#### 6.5 http client builder

```c#
public interface IHttpClientBuilder
{    
    string Name { get; }        
    IServiceCollection Services { get; }
}

```

##### 6.5.1 default http client builder

```c#
internal sealed class DefaultHttpClientBuilder : IHttpClientBuilder
{
    public string Name { get; }    
    public IServiceCollection Services { get; }
    
    public DefaultHttpClientBuilder(IServiceCollection services, string name)
    {
        Services = services;
        Name = name;
    }        
}

```

##### 6.5.2 扩展方法

```c#
public static class HttpClientBuilderExtensions
{
    /* configure http client（向 options 注册 http client action）*/
    
    public static IHttpClientBuilder ConfigureHttpClient(
        this IHttpClientBuilder builder, 
        Action<HttpClient> configureClient)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (configureClient == null)
        {
            throw new ArgumentNullException(nameof(configureClient));
        }
        
        builder.Services.Configure<HttpClientFactoryOptions>(
            builder.Name, 
            options => options.HttpClientActions.Add(configureClient));
        
        return builder;
    }
        
    public static IHttpClientBuilder ConfigureHttpClient(
        this IHttpClientBuilder builder, 
        Action<IServiceProvider, HttpClient> configureClient)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (configureClient == null)
        {
            throw new ArgumentNullException(nameof(configureClient));
        }
        
        builder.Services.AddTransient<IConfigureOptions<HttpClientFactoryOptions>>(
            services =>
            {
                return new ConfigureNamedOptions<HttpClientFactoryOptions>(
                    builder.Name, 
                    (options) =>
                    {
                        options.HttpClientActions.Add(client => configureClient(services, client));
                    });
            });
        
        return builder;
    }
    
    /* add http message handler（向 options 注入 handler builder action）*/
    
    public static IHttpClientBuilder AddHttpMessageHandler(
        this IHttpClientBuilder builder, 
        Func<DelegatingHandler> configureHandler)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (configureHandler == null)
        {
            throw new ArgumentNullException(nameof(configureHandler));
        }
        
        builder.Services.Configure<HttpClientFactoryOptions>(
            builder.Name, 
            options =>
            {                                                                 
                options.HttpMessageHandlerBuilderActions.Add(
                    b => b.AdditionalHandlers.Add(configureHandler()));
            });
        
        return builder;
    }
          
    public static IHttpClientBuilder AddHttpMessageHandler(
        this IHttpClientBuilder builder, 
        Func<IServiceProvider, DelegatingHandler> configureHandler)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (configureHandler == null)
        {
            throw new ArgumentNullException(nameof(configureHandler));
        }
        
        builder.Services.Configure<HttpClientFactoryOptions>(
            builder.Name, 
            options =>
            {
                options.HttpMessageHandlerBuilderActions.Add(
                    b => b.AdditionalHandlers.Add(configureHandler(b.Services)));
            });
        
        return builder;
    }
                   
    public static IHttpClientBuilder AddHttpMessageHandler<THandler>(this IHttpClientBuilder builder)            
        where THandler : DelegatingHandler
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        builder.Services.Configure<HttpClientFactoryOptions>(
            builder.Name, 
            options =>
            {
                options.HttpMessageHandlerBuilderActions.Add(
                    b => b.AdditionalHandlers.Add(b.Services.GetRequiredService<THandler>()));
            });
        
        return builder;
    }
    
           
    public static IHttpClientBuilder ConfigurePrimaryHttpMessageHandler(
        this IHttpClientBuilder builder, 
        Func<HttpMessageHandler> configureHandler)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (configureHandler == null)
        {
            throw new ArgumentNullException(nameof(configureHandler));
        }
        
        builder.Services.Configure<HttpClientFactoryOptions>(
            builder.Name, 
            options =>
            {
                options.HttpMessageHandlerBuilderActions.Add(
                    b => b.PrimaryHandler = configureHandler());
            });
        
        return builder;
    }
    
   
        /// The <see paramref="configureHandler"/> delegate should return a new instance of the message handler each time it
        /// is invoked.
        
        /// The <see cref="IServiceProvider"/> argument provided to <paramref name="configureHandler"/> will be
        /// a reference to a scoped service provider that shares the lifetime of the handler being constructed.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder ConfigurePrimaryHttpMessageHandler(
        this IHttpClientBuilder builder, 
        Func<IServiceProvider, HttpMessageHandler> configureHandler)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }        
        if (configureHandler == null)
        {
            throw new ArgumentNullException(nameof(configureHandler));
        }
        
        builder.Services.Configure<HttpClientFactoryOptions>(
            builder.Name, 
            options =>
            {
                options.HttpMessageHandlerBuilderActions.Add(
                    b => b.PrimaryHandler = configureHandler(b.Services));
            });
        
        return builder;
    }
    
        
       
    public static IHttpClientBuilder ConfigurePrimaryHttpMessageHandler<THandler>(this IHttpClientBuilder builder)        
        where THandler : HttpMessageHandler
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        builder.Services.Configure<HttpClientFactoryOptions>(
            builder.Name, 
            options =>
            {
                options.HttpMessageHandlerBuilderActions.Add(
                    b => b.PrimaryHandler = b.Services.GetRequiredService<THandler>());
            });

        return builder;
    }
    
        
    public static IHttpClientBuilder ConfigureHttpMessageHandlerBuilder(
        this IHttpClientBuilder builder, 
        Action<HttpMessageHandlerBuilder> configureBuilder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        if (configureBuilder == null)
        {
            throw new ArgumentNullException(nameof(configureBuilder));
        }
        
        builder.Services.Configure<HttpClientFactoryOptions>(
            builder.Name, 
            options => options.HttpMessageHandlerBuilderActions.Add(configureBuilder));

        return builder;
    }
    
        /// <summary>
        /// Configures a binding between the <typeparamref name="TClient" /> type and the named <see cref="HttpClient"/>
        /// associated with the <see cref="IHttpClientBuilder"/>.
        /// </summary>
        /// <typeparam name="TClient">
        /// The type of the typed client. The type specified will be registered in the service collection as
        /// a transient service. See <see cref="ITypedHttpClientFactory{TClient}" /> for more details about authoring typed clients.
        /// </typeparam>
        /// <param name="builder">The <see cref="IHttpClientBuilder"/>.</param>
        /// <remarks>
        /// <para>
        /// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient" />
        /// can be retrieved from <see cref="IServiceProvider.GetService(Type)" /> (and related methods) by providing
        /// <typeparamref name="TClient"/> as the service type.
        /// </para>
        /// <para>
        /// Calling <see cref="HttpClientBuilderExtensions.AddTypedClient{TClient}(IHttpClientBuilder)"/> will register a typed
        /// client binding that creates <typeparamref name="TClient"/> using the <see cref="ITypedHttpClientFactory{TClient}" />.
        /// </para>
        /// <para>
        /// The typed client's service dependencies will be resolved from the same service provider
        /// that is used to resolve the typed client. It is not possible to access services from the
        /// scope bound to the message handler, which is managed independently.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddTypedClient<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TClient>(        this IHttpClientBuilder builder)            where TClient : class
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
            
        }
        
        return AddTypedClientCore<TClient>(builder, validateSingleType: false);
    }
    
    internal static IHttpClientBuilder AddTypedClientCore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TClient>(            this IHttpClientBuilder builder, bool validateSingleType)            where TClient : class
    {
        ReserveClient(builder, typeof(TClient), builder.Name, validateSingleType);
        
        builder.Services.AddTransient(s => AddTransientHelper<TClient>(s, builder));
        
        return builder;
    }
    
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2091:UnrecognizedReflectionPattern",
            Justification = "Workaround for https://github.com/mono/linker/issues/1416. Outer method has been annotated with DynamicallyAccessedMembers.")]
    private static TClient AddTransientHelper<TClient>(IServiceProvider s, IHttpClientBuilder builder) where TClient : class
    {
        IHttpClientFactory httpClientFactory = s.GetRequiredService<IHttpClientFactory>();
        HttpClient httpClient = httpClientFactory.CreateClient(builder.Name);
        
        ITypedHttpClientFactory<TClient> typedClientFactory = s.GetRequiredService<ITypedHttpClientFactory<TClient>>();
        return typedClientFactory.CreateClient(httpClient);
    }
    
        /// <summary>
        /// Configures a binding between the <typeparamref name="TClient" /> type and the named <see cref="HttpClient"/>
        /// associated with the <see cref="IHttpClientBuilder"/>. The created instances will be of type
        /// <typeparamref name="TImplementation"/>.
        /// </summary>
        /// <typeparam name="TClient">
        /// The declared type of the typed client. They type specified will be registered in the service collection as
        /// a transient service. See <see cref="ITypedHttpClientFactory{TImplementation}" /> for more details about authoring typed clients.
        /// </typeparam>
        /// <typeparam name="TImplementation">
        /// The implementation type of the typed client. The type specified by will be instantiated by the
        /// <see cref="ITypedHttpClientFactory{TImplementation}"/>.
        /// </typeparam>
        /// <param name="builder">The <see cref="IHttpClientBuilder"/>.</param>
        /// <remarks>
        /// <para>
        /// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient" />
        /// can be retrieved from <see cref="IServiceProvider.GetService(Type)" /> (and related methods) by providing
        /// <typeparamref name="TClient"/> as the service type.
        /// </para>
        /// <para>
        /// Calling <see cref="HttpClientBuilderExtensions.AddTypedClient{TClient,TImplementation}(IHttpClientBuilder)"/>
        /// will register a typed client binding that creates <typeparamref name="TImplementation"/> using the
        /// <see cref="ITypedHttpClientFactory{TImplementation}" />.
        /// </para>
        /// <para>
        /// The typed client's service dependencies will be resolved from the same service provider
        /// that is used to resolve the typed client. It is not possible to access services from the
        /// scope bound to the message handler, which is managed independently.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddTypedClient<TClient, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
            this IHttpClientBuilder builder)            where TClient : class        where TImplementation : class, TClient
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        return AddTypedClientCore<TClient, TImplementation>(builder, validateSingleType: false);
    }

        internal static IHttpClientBuilder AddTypedClientCore<TClient, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
            this IHttpClientBuilder builder, bool validateSingleType)
            where TClient : class
            where TImplementation : class, TClient
        {
            ReserveClient(builder, typeof(TClient), builder.Name, validateSingleType);

            builder.Services.AddTransient(s => AddTransientHelper<TClient, TImplementation>(s, builder));

            return builder;
        }

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2091:UnrecognizedReflectionPattern",
            Justification = "Workaround for https://github.com/mono/linker/issues/1416. Outer method has been annotated with DynamicallyAccessedMembers.")]
        private static TClient AddTransientHelper<TClient, TImplementation>(IServiceProvider s, IHttpClientBuilder builder) where TClient : class where TImplementation : class, TClient
        {
            IHttpClientFactory httpClientFactory = s.GetRequiredService<IHttpClientFactory>();
            HttpClient httpClient = httpClientFactory.CreateClient(builder.Name);

            ITypedHttpClientFactory<TImplementation> typedClientFactory = s.GetRequiredService<ITypedHttpClientFactory<TImplementation>>();
            return typedClientFactory.CreateClient(httpClient);
        }

        /// <summary>
        /// Configures a binding between the <typeparamref name="TClient" /> type and the named <see cref="HttpClient"/>
        /// associated with the <see cref="IHttpClientBuilder"/>.
        /// </summary>
        /// <typeparam name="TClient">
        /// The type of the typed client. They type specified will be registered in the service collection as
        /// a transient service.
        /// </typeparam>
        /// <param name="builder">The <see cref="IHttpClientBuilder"/>.</param>
        /// <param name="factory">A factory function that will be used to construct the typed client.</param>
        /// <remarks>
        /// <para>
        /// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient" />
        /// can be retrieved from <see cref="IServiceProvider.GetService(Type)" /> (and related methods) by providing
        /// <typeparamref name="TClient"/> as the service type.
        /// </para>
        /// <para>
        /// Calling <see cref="HttpClientBuilderExtensions.AddTypedClient{TClient}(IHttpClientBuilder,Func{HttpClient,TClient})"/>
        /// will register a typed client binding that creates <typeparamref name="TClient"/> using the provided factory function.
        /// </para>
        /// </remarks>
        public static IHttpClientBuilder AddTypedClient<TClient>(this IHttpClientBuilder builder, Func<HttpClient, TClient> factory)
            where TClient : class
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            return AddTypedClientCore<TClient>(builder, factory, validateSingleType: false);
        }

        internal static IHttpClientBuilder AddTypedClientCore<TClient>(this IHttpClientBuilder builder, Func<HttpClient, TClient> factory, bool validateSingleType)
            where TClient : class
        {
            ReserveClient(builder, typeof(TClient), builder.Name, validateSingleType);

            builder.Services.AddTransient<TClient>(s =>
            {
                IHttpClientFactory httpClientFactory = s.GetRequiredService<IHttpClientFactory>();
                HttpClient httpClient = httpClientFactory.CreateClient(builder.Name);

                return factory(httpClient);
            });

            return builder;
        }

        /// <summary>
        /// Configures a binding between the <typeparamref name="TClient" /> type and the named <see cref="HttpClient"/>
        /// associated with the <see cref="IHttpClientBuilder"/>.
        /// </summary>
        /// <typeparam name="TClient">
        /// The type of the typed client. They type specified will be registered in the service collection as
        /// a transient service.
        /// </typeparam>
        /// <param name="builder">The <see cref="IHttpClientBuilder"/>.</param>
        /// <param name="factory">A factory function that will be used to construct the typed client.</param>
        /// <remarks>
        /// <para>
        /// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient" />
        /// can be retrieved from <see cref="IServiceProvider.GetService(Type)" /> (and related methods) by providing
        /// <typeparamref name="TClient"/> as the service type.
        /// </para>
        /// <para>
        /// Calling <see cref="HttpClientBuilderExtensions.AddTypedClient{TClient}(IHttpClientBuilder,Func{HttpClient,IServiceProvider,TClient})"/>
        /// will register a typed client binding that creates <typeparamref name="TClient"/> using the provided factory function.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddTypedClient<TClient>(this IHttpClientBuilder builder, Func<HttpClient, IServiceProvider, TClient> factory)            where TClient : class       
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }
        
        return AddTypedClientCore<TClient>(builder, factory, validateSingleType: false);
    }
    
    internal static IHttpClientBuilder AddTypedClientCore<TClient>(this IHttpClientBuilder builder, Func<HttpClient, IServiceProvider, TClient> factory, bool validateSingleType)            where TClient : class
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }
        
        ReserveClient(builder, typeof(TClient), builder.Name, validateSingleType);
        
        builder.Services.AddTransient<TClient>(s =>
            {
                IHttpClientFactory httpClientFactory = s.GetRequiredService<IHttpClientFactory>();
                HttpClient httpClient = httpClientFactory.CreateClient(builder.Name);

                return factory(httpClient, s);
            });

        return builder;
    }
    
        /// <summary>
        /// Sets the <see cref="Func{T, R}"/> which determines whether to redact the HTTP header value before logging.
        /// </summary>
        /// <param name="builder">The <see cref="IHttpClientBuilder"/>.</param>
        /// <param name="shouldRedactHeaderValue">The <see cref="Func{T, R}"/> which determines whether to redact the HTTP header value before logging.</param>
        /// <returns>The <see cref="IHttpClientBuilder"/>.</returns>
        /// <remarks>The provided <paramref name="shouldRedactHeaderValue"/> predicate will be evaluated for each header value when logging. If the predicate returns <c>true</c> then the header value will be replaced with a marker value <c>*</c> in logs; otherwise the header value will be logged.
        /// </remarks>
    public static IHttpClientBuilder RedactLoggedHeaders(this IHttpClientBuilder builder, Func<string, bool> shouldRedactHeaderValue)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        if (shouldRedactHeaderValue == null)
        {
            throw new ArgumentNullException(nameof(shouldRedactHeaderValue));
        }
        
        builder.Services.Configure<HttpClientFactoryOptions>(builder.Name, options =>
            {
                options.ShouldRedactHeaderValue = shouldRedactHeaderValue;
            });

        return builder;
    }
    
        /// <summary>
        /// Sets the collection of HTTP headers names for which values should be redacted before logging.
        /// </summary>
        /// <param name="builder">The <see cref="IHttpClientBuilder"/>.</param>
        /// <param name="redactedLoggedHeaderNames">The collection of HTTP headers names for which values should be redacted before logging.</param>
        /// <returns>The <see cref="IHttpClientBuilder"/>.</returns>
    public static IHttpClientBuilder RedactLoggedHeaders(this IHttpClientBuilder builder, IEnumerable<string> redactedLoggedHeaderNames)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        if (redactedLoggedHeaderNames == null)
        {
            throw new ArgumentNullException(nameof(redactedLoggedHeaderNames));
        }
        
        builder.Services.Configure<HttpClientFactoryOptions>(builder.Name, options =>
                                                             {
                var sensitiveHeaders = new HashSet<string>(redactedLoggedHeaderNames, StringComparer.OrdinalIgnoreCase);

                options.ShouldRedactHeaderValue = (header) => sensitiveHeaders.Contains(header);
            });
        
        return builder;
    }
    
        /// <summary>
        /// Sets the length of time that a <see cref="HttpMessageHandler"/> instance can be reused. Each named
        /// client can have its own configured handler lifetime value. The default value is two minutes. Set the lifetime to
        /// <see cref="Timeout.InfiniteTimeSpan"/> to disable handler expiry.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The default implementation of <see cref="IHttpClientFactory"/> will pool the <see cref="HttpMessageHandler"/>
        /// instances created by the factory to reduce resource consumption. This setting configures the amount of time
        /// a handler can be pooled before it is scheduled for removal from the pool and disposal.
        /// </para>
        /// <para>
        /// Pooling of handlers is desirable as each handler typically manages its own underlying HTTP connections; creating
        /// more handlers than necessary can result in connection delays. Some handlers also keep connections open indefinitely
        /// which can prevent the handler from reacting to DNS changes. The value of <paramref name="handlerLifetime"/> should be
        /// chosen with an understanding of the application's requirement to respond to changes in the network environment.
        /// </para>
        /// <para>
        /// Expiry of a handler will not immediately dispose the handler. An expired handler is placed in a separate pool
        /// which is processed at intervals to dispose handlers only when they become unreachable. Using long-lived
        /// <see cref="HttpClient"/> instances will prevent the underlying <see cref="HttpMessageHandler"/> from being
        /// disposed until all references are garbage-collected.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder SetHandlerLifetime(this IHttpClientBuilder builder, TimeSpan handlerLifetime)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        
        if (handlerLifetime != Timeout.InfiniteTimeSpan && handlerLifetime < HttpClientFactoryOptions.MinimumHandlerLifetime)
        {
            throw new ArgumentException(SR.HandlerLifetime_InvalidValue, nameof(handlerLifetime));
        }
        
        builder.Services.Configure<HttpClientFactoryOptions>(builder.Name, options => options.HandlerLifetime = handlerLifetime);
        return builder;
    }
    
    // See comments on HttpClientMappingRegistry.
    private static void ReserveClient(IHttpClientBuilder builder, Type type, string name, bool validateSingleType)
    {
        var registry = (HttpClientMappingRegistry)builder.Services.Single(sd => sd.ServiceType == typeof(HttpClientMappingRegistry)).ImplementationInstance;
        Debug.Assert(registry != null);
        
        // Check for same name registered to two types. This won't work because we rely on named options for the configuration.
        if (registry.NamedClientRegistrations.TryGetValue(name, out Type otherType) &&
            
                // Allow using the same name with multiple types in some cases (see callers).
                validateSingleType &&

                // Allow registering the same name twice to the same type.
                type != otherType)
        {
            string message =
                $"The HttpClient factory already has a registered client with the name '{name}', bound to the type '{otherType.FullName}'. " +
                $"Client names are computed based on the type name without considering the namespace ('{otherType.Name}'). " +
                $"Use an overload of AddHttpClient that accepts a string and provide a unique name to resolve the conflict.";
            throw new InvalidOperationException(message);
        }
        
        if (validateSingleType)
        {
            registry.NamedClientRegistrations[name] = type;
        }
    }
}

```

#### 6.6 http client mapping registry

```c#
// Internal tracking for HTTP Client configuration. 
// This is used to prevent some common mistakes that are easy to make with HTTP Client registration.
internal sealed class HttpClientMappingRegistry
{
    public Dictionary<string, Type> NamedClientRegistrations { get; } = new Dictionary<string, Type>();
}

```

#### 6.7 add http client (factory)

```c#
public static class HttpClientFactoryServiceCollectionExtensions
{
    /// <summary>
    /// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddHttpClient(this IServiceCollection services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        services.AddLogging();
        services.AddOptions();
        
        
        // Core abstractions       
        services.TryAddTransient<HttpMessageHandlerBuilder, DefaultHttpMessageHandlerBuilder>();
        services.TryAddSingleton<DefaultHttpClientFactory>();
        services.TryAddSingleton<IHttpClientFactory>(
            serviceProvider => serviceProvider.GetRequiredService<DefaultHttpClientFactory>());
        services.TryAddSingleton<IHttpMessageHandlerFactory>(
            serviceProvider => serviceProvider.GetRequiredService<DefaultHttpClientFactory>());

        
        // Typed Clients       
        services.TryAdd(
            ServiceDescriptor.Transient(
                typeof(ITypedHttpClientFactory<>), typeof(DefaultTypedHttpClientFactory<>)));
        services.TryAdd(
            ServiceDescriptor.Singleton(
                typeof(DefaultTypedHttpClientFactory<>.Cache), typeof(DefaultTypedHttpClientFactory<>.Cache)));
                
        // Misc infrastructure        
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, 
            LoggingHttpMessageHandlerBuilderFilter>());
        
        // This is used to track state and report errors **DURING** service registration. This has to be an instance
        // because we access it by reaching into the service collection.
        services.TryAddSingleton(new HttpClientMappingRegistry());
        
        // Register default client as HttpClient
        services.TryAddTransient(s =>
            {
                return s.GetRequiredService<IHttpClientFactory>().CreateClient(string.Empty);
            });
        
        return services;
    }
    
    /// <summary>
    /// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/> and configures
        /// a named <see cref="HttpClient"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="name">The logical name of the <see cref="HttpClient"/> to configure.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
        /// <remarks>
        /// <para>
        /// <see cref="HttpClient"/> instances that apply the provided configuration can be retrieved using
        /// <see cref="IHttpClientFactory.CreateClient(string)"/> and providing the matching name.
        /// </para>
        /// <para>
        /// Use <see cref="Options.Options.DefaultName"/> as the name to configure the default client.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddHttpClient(this IServiceCollection services, string name)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }        
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        AddHttpClient(services);
        
        return new DefaultHttpClientBuilder(services, name);
    }

        /// <summary>
        /// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/> and configures
        /// a named <see cref="HttpClient"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="name">The logical name of the <see cref="HttpClient"/> to configure.</param>
        /// <param name="configureClient">A delegate that is used to configure an <see cref="HttpClient"/>.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
        /// <remarks>
        /// <para>
        /// <see cref="HttpClient"/> instances that apply the provided configuration can be retrieved using
        /// <see cref="IHttpClientFactory.CreateClient(string)"/> and providing the matching name.
        /// </para>
        /// <para>
        /// Use <see cref="Options.Options.DefaultName"/> as the name to configure the default client.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddHttpClient(this IServiceCollection services, string name, Action<HttpClient> configureClient)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        if (configureClient == null)
        {
            throw new ArgumentNullException(nameof(configureClient));
        }
        
        AddHttpClient(services);
        
        var builder = new DefaultHttpClientBuilder(services, name);
        builder.ConfigureHttpClient(configureClient);
        return builder;
    }
    
    /// <summary>
    /// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/> and configures
    /// a named <see cref="HttpClient"/>.
    /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="name">The logical name of the <see cref="HttpClient"/> to configure.</param>
        /// <param name="configureClient">A delegate that is used to configure an <see cref="HttpClient"/>.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
        /// <remarks>
        /// <para>
        /// <see cref="HttpClient"/> instances that apply the provided configuration can be retrieved using
        /// <see cref="IHttpClientFactory.CreateClient(string)"/> and providing the matching name.
        /// </para>
        /// <para>
        /// Use <see cref="Options.Options.DefaultName"/> as the name to configure the default client.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddHttpClient(
        this IServiceCollection services, 
        string name, 
        Action<IServiceProvider, HttpClient> configureClient)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }        
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }        
        if (configureClient == null)
        {
            throw new ArgumentNullException(nameof(configureClient));
        }

        AddHttpClient(services);
        
        var builder = new DefaultHttpClientBuilder(services, name);
        builder.ConfigureHttpClient(configureClient);
        return builder;
    }
    
        /// <summary>
        /// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/> and configures
        /// a binding between the <typeparamref name="TClient"/> type and a named <see cref="HttpClient"/>. The client name
        /// will be set to the type name of <typeparamref name="TClient"/>.
        /// </summary>
        /// <typeparam name="TClient">
        /// The type of the typed client. The type specified will be registered in the service collection as
        /// a transient service. See <see cref="ITypedHttpClientFactory{TClient}" /> for more details about authoring typed clients.
        /// </typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
        /// <remarks>
        /// <para>
        /// <see cref="HttpClient"/> instances that apply the provided configuration can be retrieved using
        /// <see cref="IHttpClientFactory.CreateClient(string)"/> and providing the matching name.
        /// </para>
        /// <para>
        /// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient" />
        /// can be retrieved from <see cref="IServiceProvider.GetService(Type)" /> (and related methods) by providing
        /// <typeparamref name="TClient"/> as the service type.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddHttpClient<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TClient>(this IServiceCollection services)            where TClient : class
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        AddHttpClient(services);
        
        string name = TypeNameHelper.GetTypeDisplayName(typeof(TClient), fullName: false);
        var builder = new DefaultHttpClientBuilder(services, name);
        builder.AddTypedClientCore<TClient>(validateSingleType: true);
        return builder;
    }
    
        /// <summary>
        /// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/> and configures
        /// a binding between the <typeparamref name="TClient" /> type and a named <see cref="HttpClient"/>. The client name will
        /// be set to the type name of <typeparamref name="TClient"/>.
        /// </summary>
        /// <typeparam name="TClient">
        /// The type of the typed client. The type specified will be registered in the service collection as
        /// a transient service. See <see cref="ITypedHttpClientFactory{TClient}" /> for more details about authoring typed clients.
        /// </typeparam>
        /// <typeparam name="TImplementation">
        /// The implementation type of the typed client. The type specified will be instantiated by the
        /// <see cref="ITypedHttpClientFactory{TImplementation}"/>
        /// </typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
        /// <remarks>
        /// <para>
        /// <see cref="HttpClient"/> instances that apply the provided configuration can be retrieved using
        /// <see cref="IHttpClientFactory.CreateClient(string)"/> and providing the matching name.
        /// </para>
        /// <para>
        /// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient" />
        /// can be retrieved from <see cref="IServiceProvider.GetService(Type)" /> (and related methods) by providing
        /// <typeparamref name="TClient"/> as the service type.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddHttpClient<
        TClient, 
    	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
            this IServiceCollection services)            where TClient : class            where TImplementation : class, TClient
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
            
        AddHttpClient(services);
            
        string name = TypeNameHelper.GetTypeDisplayName(typeof(TClient), fullName: false);
        var builder = new DefaultHttpClientBuilder(services, name);
        builder.AddTypedClientCore<TClient, TImplementation>(validateSingleType: true);
        return builder;
    }

        /// <summary>
        /// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/> and configures
        /// a binding between the <typeparamref name="TClient"/> type and a named <see cref="HttpClient"/>.
        /// </summary>
        /// <typeparam name="TClient">
        /// The type of the typed client. The type specified will be registered in the service collection as
        /// a transient service. See <see cref="ITypedHttpClientFactory{TClient}" /> for more details about authoring typed clients.
        /// </typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="name">The logical name of the <see cref="HttpClient"/> to configure.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
        /// <remarks>
        /// <para>
        /// <see cref="HttpClient"/> instances that apply the provided configuration can be retrieved using
        /// <see cref="IHttpClientFactory.CreateClient(string)"/> and providing the matching name.
        /// </para>
        /// <para>
        /// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient" />
        /// can be retrieved from <see cref="IServiceProvider.GetService(Type)" /> (and related methods) by providing
        /// <typeparamref name="TClient"/> as the service type.
        /// </para>
        /// <para>
        /// Use <see cref="Options.Options.DefaultName"/> as the name to configure the default client.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddHttpClient<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TClient>(            this IServiceCollection services, string name)        where TClient : class
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }        
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        AddHttpClient(services);
        
        var builder = new DefaultHttpClientBuilder(services, name);
        builder.AddTypedClientCore<TClient>(validateSingleType: false); // Name was explicitly provided.
        return builder;
    }
    
        /// <summary>
        /// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/> and configures
        /// a binding between the <typeparamref name="TClient" /> type and a named <see cref="HttpClient"/>. The client name will
        /// be set to the type name of <typeparamref name="TClient"/>.
        /// </summary>
        /// <typeparam name="TClient">
        /// The type of the typed client. The type specified will be registered in the service collection as
        /// a transient service. See <see cref="ITypedHttpClientFactory{TClient}" /> for more details about authoring typed clients.
        /// </typeparam>
        /// <typeparam name="TImplementation">
        /// The implementation type of the typed client. The type specified will be instantiated by the
        /// <see cref="ITypedHttpClientFactory{TImplementation}"/>
        /// </typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="name">The logical name of the <see cref="HttpClient"/> to configure.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
        /// <remarks>
        /// <para>
        /// <see cref="HttpClient"/> instances that apply the provided configuration can be retrieved using
        /// <see cref="IHttpClientFactory.CreateClient(string)"/> and providing the matching name.
        /// </para>
        /// <para>
        /// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient" />
        /// can be retrieved from <see cref="IServiceProvider.GetService(Type)" /> (and related methods) by providing
        /// <typeparamref name="TClient"/> as the service type.
        /// </para>
        /// <para>
        /// Use <see cref="Options.Options.DefaultName"/> as the name to configure the default client.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddHttpClient<        TClient,     	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(            this IServiceCollection services, string name)            where TClient : class            where TImplementation : class, TClient
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        AddHttpClient(services);
        
        var builder = new DefaultHttpClientBuilder(services, name);
        builder.AddTypedClientCore<TClient, TImplementation>(validateSingleType: false); // name was explicitly provided
        return builder;
    }
    
    /// <summary>
        /// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/> and configures
        /// a binding between the <typeparamref name="TClient" /> type and a named <see cref="HttpClient"/>. The client name will
        /// be set to the type name of <typeparamref name="TClient"/>.
        /// </summary>
        /// <typeparam name="TClient">
        /// The type of the typed client. The type specified will be registered in the service collection as
        /// a transient service. See <see cref="ITypedHttpClientFactory{TClient}" /> for more details about authoring typed clients.
        /// </typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="configureClient">A delegate that is used to configure an <see cref="HttpClient"/>.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
        /// <remarks>
        /// <para>
        /// <see cref="HttpClient"/> instances that apply the provided configuration can be retrieved using
        /// <see cref="IHttpClientFactory.CreateClient(string)"/> and providing the matching name.
        /// </para>
        /// <para>
        /// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient" />
        /// can be retrieved from <see cref="IServiceProvider.GetService(Type)" /> (and related methods) by providing
        /// <typeparamref name="TClient"/> as the service type.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddHttpClient<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TClient>(            this IServiceCollection services, Action<HttpClient> configureClient)            where TClient : class
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        if (configureClient == null)
        {
            throw new ArgumentNullException(nameof(configureClient));
        }
        
        AddHttpClient(services);
        
        string name = TypeNameHelper.GetTypeDisplayName(typeof(TClient), fullName: false);
        var builder = new DefaultHttpClientBuilder(services, name);
        builder.ConfigureHttpClient(configureClient);
        builder.AddTypedClientCore<TClient>(validateSingleType: true);
        return builder;
    }

        /// <summary>
        /// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/> and configures
        /// a binding between the <typeparamref name="TClient" /> type and a named <see cref="HttpClient"/>. The client name will
        /// be set to the type name of <typeparamref name="TClient"/>.
        /// </summary>
        /// <typeparam name="TClient">
        /// The type of the typed client. The type specified will be registered in the service collection as
        /// a transient service. See <see cref="ITypedHttpClientFactory{TClient}" /> for more details about authoring typed clients.
        /// </typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="configureClient">A delegate that is used to configure an <see cref="HttpClient"/>.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
        /// <remarks>
        /// <para>
        /// <see cref="HttpClient"/> instances that apply the provided configuration can be retrieved using
        /// <see cref="IHttpClientFactory.CreateClient(string)"/> and providing the matching name.
        /// </para>
        /// <para>
        /// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient" />
        /// can be retrieved from <see cref="IServiceProvider.GetService(Type)" /> (and related methods) by providing
        /// <typeparamref name="TClient"/> as the service type.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddHttpClient<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TClient>(            this IServiceCollection services, Action<IServiceProvider, HttpClient> configureClient)            where TClient : class
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        if (configureClient == null)
        {
            throw new ArgumentNullException(nameof(configureClient));
        }
        
        AddHttpClient(services);
        
        string name = TypeNameHelper.GetTypeDisplayName(typeof(TClient), fullName: false);
        var builder = new DefaultHttpClientBuilder(services, name);
        builder.ConfigureHttpClient(configureClient);
        builder.AddTypedClientCore<TClient>(validateSingleType: true);
        return builder;
    }
    
    /// <summary>
    /// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/> and configures
    /// a binding between the <typeparamref name="TClient" /> type and a named <see cref="HttpClient"/>. The client name will
    /// be set to the type name of <typeparamref name="TClient"/>.
    /// </summary>
    /// <typeparam name="TClient">
    /// The type of the typed client. The type specified will be registered in the service collection as
    /// a transient service. See <see cref="ITypedHttpClientFactory{TClient}" /> for more details about authoring typed clients.
    /// </typeparam>
    /// <typeparam name="TImplementation">
    /// The implementation type of the typed client. The type specified will be instantiated by the
        /// <see cref="ITypedHttpClientFactory{TImplementation}"/>
        /// </typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="configureClient">A delegate that is used to configure an <see cref="HttpClient"/>.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
        /// <remarks>
        /// <para>
        /// <see cref="HttpClient"/> instances that apply the provided configuration can be retrieved using
        /// <see cref="IHttpClientFactory.CreateClient(string)"/> and providing the matching name.
        /// </para>
        /// <para>
        /// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient" />
        /// can be retrieved from <see cref="IServiceProvider.GetService(Type)" /> (and related methods) by providing
        /// <typeparamref name="TClient"/> as the service type.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddHttpClient<TClient, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(            this IServiceCollection services, Action<HttpClient> configureClient)            where TClient : class            where TImplementation : class, TClient
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        if (configureClient == null)
        {
            throw new ArgumentNullException(nameof(configureClient));
        }
        
        AddHttpClient(services);
        
        string name = TypeNameHelper.GetTypeDisplayName(typeof(TClient), fullName: false);
        var builder = new DefaultHttpClientBuilder(services, name);
        builder.ConfigureHttpClient(configureClient);
        builder.AddTypedClientCore<TClient, TImplementation>(validateSingleType: true);
        return builder;
    }
    
        /// <summary>
        /// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/> and configures
        /// a binding between the <typeparamref name="TClient" /> type and a named <see cref="HttpClient"/>. The client name will
        /// be set to the type name of <typeparamref name="TClient"/>.
        /// </summary>
        /// <typeparam name="TClient">
        /// The type of the typed client. The type specified will be registered in the service collection as
        /// a transient service. See <see cref="ITypedHttpClientFactory{TClient}" /> for more details about authoring typed clients.
        /// </typeparam>
        /// <typeparam name="TImplementation">
        /// The implementation type of the typed client. The type specified will be instantiated by the
        /// <see cref="ITypedHttpClientFactory{TImplementation}"/>
        /// </typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="configureClient">A delegate that is used to configure an <see cref="HttpClient"/>.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
        /// <remarks>
        /// <para>
        /// <see cref="HttpClient"/> instances that apply the provided configuration can be retrieved using
        /// <see cref="IHttpClientFactory.CreateClient(string)"/> and providing the matching name.
        /// </para>
        /// <para>
        /// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient" />
        /// can be retrieved from <see cref="IServiceProvider.GetService(Type)" /> (and related methods) by providing
        /// <typeparamref name="TClient"/> as the service type.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddHttpClient<TClient, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(            this IServiceCollection services, Action<IServiceProvider, HttpClient> configureClient)            where TClient : class            where TImplementation : class, TClient
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        if (configureClient == null)
        {
            throw new ArgumentNullException(nameof(configureClient));
        }
        
        AddHttpClient(services);
        
        string name = TypeNameHelper.GetTypeDisplayName(typeof(TClient), fullName: false);
        var builder = new DefaultHttpClientBuilder(services, name);
        builder.ConfigureHttpClient(configureClient);
        builder.AddTypedClientCore<TClient, TImplementation>(validateSingleType: true);
        return builder;
    }
    
        /// <summary>
        /// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/> and configures
        /// a binding between the <typeparamref name="TClient" /> type and a named <see cref="HttpClient"/>.
        /// </summary>
        /// <typeparam name="TClient">
        /// The type of the typed client. The type specified will be registered in the service collection as
        /// a transient service. See <see cref="ITypedHttpClientFactory{TClient}" /> for more details about authoring typed clients.
        /// </typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="name">The logical name of the <see cref="HttpClient"/> to configure.</param>
        /// <param name="configureClient">A delegate that is used to configure an <see cref="HttpClient"/>.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
        /// <remarks>
        /// <para>
        /// <see cref="HttpClient"/> instances that apply the provided configuration can be retrieved using
        /// <see cref="IHttpClientFactory.CreateClient(string)"/> and providing the matching name.
        /// </para>
        /// <para>
        /// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient" />
        /// can be retrieved from <see cref="IServiceProvider.GetService(Type)" /> (and related methods) by providing
        /// <typeparamref name="TClient"/> as the service type.
        /// </para>
        /// <para>
        /// Use <see cref="Options.Options.DefaultName"/> as the name to configure the default client.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddHttpClient<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TClient>(            this IServiceCollection services, string name, Action<HttpClient> configureClient)            where TClient : class
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        if (configureClient == null)
        {
            throw new ArgumentNullException(nameof(configureClient));
        }
        
        AddHttpClient(services);
        
        var builder = new DefaultHttpClientBuilder(services, name);
        builder.ConfigureHttpClient(configureClient);
        builder.AddTypedClientCore<TClient>(validateSingleType: false); // name was explicitly provided
        return builder;
    }
    
        /// <summary>
        /// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/> and configures
        /// a binding between the <typeparamref name="TClient" /> type and a named <see cref="HttpClient"/>.
        /// </summary>
        /// <typeparam name="TClient">
        /// The type of the typed client. The type specified will be registered in the service collection as
        /// a transient service. See <see cref="ITypedHttpClientFactory{TClient}" /> for more details about authoring typed clients.
        /// </typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="name">The logical name of the <see cref="HttpClient"/> to configure.</param>
        /// <param name="configureClient">A delegate that is used to configure an <see cref="HttpClient"/>.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
        /// <remarks>
        /// <para>
        /// <see cref="HttpClient"/> instances that apply the provided configuration can be retrieved using
        /// <see cref="IHttpClientFactory.CreateClient(string)"/> and providing the matching name.
        /// </para>
        /// <para>
        /// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient" />
        /// can be retrieved from <see cref="IServiceProvider.GetService(Type)" /> (and related methods) by providing
        /// <typeparamref name="TClient"/> as the service type.
        /// </para>
        /// <para>
        /// Use <see cref="Options.Options.DefaultName"/> as the name to configure the default client.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddHttpClient<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TClient>(            this IServiceCollection services, string name, Action<IServiceProvider, HttpClient> configureClient)            where TClient : class
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        if (configureClient == null)
        {
            throw new ArgumentNullException(nameof(configureClient));
        }
        
        AddHttpClient(services);
        
        var builder = new DefaultHttpClientBuilder(services, name);
        builder.ConfigureHttpClient(configureClient);
        builder.AddTypedClientCore<TClient>(validateSingleType: false); // name was explictly provided
        return builder;
    }
    
        /// <summary>
        /// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/> and configures
        /// a binding between the <typeparamref name="TClient" /> type and a named <see cref="HttpClient"/>.
        /// </summary>
        /// <typeparam name="TClient">
        /// The type of the typed client. The type specified will be registered in the service collection as
        /// a transient service. See <see cref="ITypedHttpClientFactory{TClient}" /> for more details about authoring typed clients.
        /// </typeparam>
        /// <typeparam name="TImplementation">
        /// The implementation type of the typed client. The type specified will be instantiated by the
        /// <see cref="ITypedHttpClientFactory{TImplementation}"/>
        /// </typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="name">The logical name of the <see cref="HttpClient"/> to configure.</param>
        /// <param name="configureClient">A delegate that is used to configure an <see cref="HttpClient"/>.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
        /// <remarks>
        /// <para>
        /// <see cref="HttpClient"/> instances that apply the provided configuration can be retrieved using
        /// <see cref="IHttpClientFactory.CreateClient(string)"/> and providing the matching name.
        /// </para>
        /// <para>
        /// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient" />
        /// can be retrieved from <see cref="IServiceProvider.GetService(Type)" /> (and related methods) by providing
        /// <typeparamref name="TClient"/> as the service type.
        /// </para>
        /// <para>
        /// Use <see cref="Options.Options.DefaultName"/> as the name to configure the default client.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddHttpClient<TClient, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(            this IServiceCollection services, string name, Action<HttpClient> configureClient)            where TClient : class            where TImplementation : class, TClient
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        if (configureClient == null)
        {
            throw new ArgumentNullException(nameof(configureClient));
        }
        
        AddHttpClient(services);
        
        var builder = new DefaultHttpClientBuilder(services, name);
        builder.ConfigureHttpClient(configureClient);
        builder.AddTypedClientCore<TClient, TImplementation>(validateSingleType: false); // name was explicitly provided
        return builder;
    }
    
        /// <summary>
        /// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/> and configures
        /// a binding between the <typeparamref name="TClient" /> type and a named <see cref="HttpClient"/>.
        /// </summary>
        /// <typeparam name="TClient">
        /// The type of the typed client. The type specified will be registered in the service collection as
        /// a transient service. See <see cref="ITypedHttpClientFactory{TClient}" /> for more details about authoring typed clients.
        /// </typeparam>
        /// <typeparam name="TImplementation">
        /// The implementation type of the typed client. The type specified will be instantiated by the
        /// <see cref="ITypedHttpClientFactory{TImplementation}"/>
        /// </typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="name">The logical name of the <see cref="HttpClient"/> to configure.</param>
        /// <param name="configureClient">A delegate that is used to configure an <see cref="HttpClient"/>.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
        /// <remarks>
        /// <para>
        /// <see cref="HttpClient"/> instances that apply the provided configuration can be retrieved using
        /// <see cref="IHttpClientFactory.CreateClient(string)"/> and providing the matching name.
        /// </para>
        /// <para>
        /// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient" />
        /// can be retrieved from <see cref="IServiceProvider.GetService(Type)" /> (and related methods) by providing
        /// <typeparamref name="TClient"/> as the service type.
        /// </para>
        /// <para>
        /// Use <see cref="Options.Options.DefaultName"/> as the name to configure the default client.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddHttpClient<TClient, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(            this IServiceCollection services, string name, Action<IServiceProvider, HttpClient> configureClient)            where TClient : class            where TImplementation : class, TClient
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        if (configureClient == null)
        {
            throw new ArgumentNullException(nameof(configureClient));
        }
        
        AddHttpClient(services);
        
        var builder = new DefaultHttpClientBuilder(services, name);
        builder.ConfigureHttpClient(configureClient);
        builder.AddTypedClientCore<TClient, TImplementation>(validateSingleType: false); // name was explicitly provided
        return builder;
    }
    
        /// <summary>
        /// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/> and configures
        /// a binding between the <typeparamref name="TClient" /> type and a named <see cref="HttpClient"/>.
        /// </summary>
        /// <typeparam name="TClient">
        /// The type of the typed client. The type specified will be registered in the service collection as
        /// a transient service. See <see cref="ITypedHttpClientFactory{TClient}" /> for more details about authoring typed clients.
        /// </typeparam>
        /// <typeparam name="TImplementation">
        /// The implementation type of the typed client.
        /// </typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="factory">A delegate that is used to create an instance of <typeparamref name="TClient"/>.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
        /// <remarks>
        /// <para>
        /// <see cref="HttpClient"/> instances that apply the provided configuration can be retrieved using
        /// <see cref="IHttpClientFactory.CreateClient(string)"/> and providing the matching name.
        /// </para>
        /// <para>
        /// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient" />
        /// can be retrieved from <see cref="IServiceProvider.GetService(Type)" /> (and related methods) by providing
        /// <typeparamref name="TClient"/> as the service type.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddHttpClient<TClient, TImplementation>(this IServiceCollection services, Func<HttpClient, TImplementation> factory)            where TClient : class            where TImplementation : class, TClient
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }
        
        string name = TypeNameHelper.GetTypeDisplayName(typeof(TClient), fullName: false);
        return AddHttpClient<TClient, TImplementation>(services, name, factory);
    }

        /// <summary>
        /// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/> and configures
        /// a binding between the <typeparamref name="TClient" /> type and a named <see cref="HttpClient"/>.
        /// </summary>
        /// <typeparam name="TClient">
        /// The type of the typed client. The type specified will be registered in the service collection as
        /// a transient service. See <see cref="ITypedHttpClientFactory{TClient}" /> for more details about authoring typed clients.
        /// </typeparam>
        /// <typeparam name="TImplementation">
        /// The implementation type of the typed client.
        /// </typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="name">The logical name of the <see cref="HttpClient"/> to configure.</param>
        /// <param name="factory">A delegate that is used to create an instance of <typeparamref name="TClient"/>.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
        /// <remarks>
        /// <para>
        /// <see cref="HttpClient"/> instances that apply the provided configuration can be retrieved using
        /// <see cref="IHttpClientFactory.CreateClient(string)"/> and providing the matching name.
        /// </para>
        /// <para>
        /// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient" />
        /// can be retrieved from <see cref="IServiceProvider.GetService(Type)" /> (and related methods) by providing
        /// <typeparamref name="TClient"/> as the service type.
        /// </para>
        /// <typeparamref name="TImplementation">
        /// </typeparamref>
        /// </remarks>
    public static IHttpClientBuilder AddHttpClient<TClient, TImplementation>(this IServiceCollection services, string name, Func<HttpClient, TImplementation> factory)            where TClient : class            where TImplementation : class, TClient
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }
        
        AddHttpClient(services);
        
        var builder = new DefaultHttpClientBuilder(services, name);
        builder.AddTypedClient<TClient>(factory);
        return builder;
    }
    
    /// <summary>
        /// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/> and configures
        /// a binding between the <typeparamref name="TClient" /> type and a named <see cref="HttpClient"/>.
        /// </summary>
        /// <typeparam name="TClient">
        /// The type of the typed client. The type specified will be registered in the service collection as
        /// a transient service. See <see cref="ITypedHttpClientFactory{TClient}" /> for more details about authoring typed clients.
        /// </typeparam>
        /// <typeparam name="TImplementation">
        /// The implementation type of the typed client.
        /// </typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="factory">A delegate that is used to create an instance of <typeparamref name="TClient"/>.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
        /// <remarks>
        /// <para>
        /// <see cref="HttpClient"/> instances that apply the provided configuration can be retrieved using
        /// <see cref="IHttpClientFactory.CreateClient(string)"/> and providing the matching name.
        /// </para>
        /// <para>
        /// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient" />
        /// can be retrieved from <see cref="IServiceProvider.GetService(Type)" /> (and related methods) by providing
        /// <typeparamref name="TClient"/> as the service type.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddHttpClient<TClient, TImplementation>(this IServiceCollection services, Func<HttpClient, IServiceProvider, TImplementation> factory)            where TClient : class            where TImplementation : class, TClient
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }
        
        string name = TypeNameHelper.GetTypeDisplayName(typeof(TClient), fullName: false);
        return AddHttpClient<TClient, TImplementation>(services, name, factory);
    }
    
        /// <summary>
        /// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/> and configures
        /// a binding between the <typeparamref name="TClient" /> type and a named <see cref="HttpClient"/>.
        /// </summary>
        /// <typeparam name="TClient">
        /// The type of the typed client. The type specified will be registered in the service collection as
        /// a transient service. See <see cref="ITypedHttpClientFactory{TClient}" /> for more details about authoring typed clients.
        /// </typeparam>
        /// <typeparam name="TImplementation">
        /// The implementation type of the typed client.
        /// </typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="name">The logical name of the <see cref="HttpClient"/> to configure.</param>
        /// <param name="factory">A delegate that is used to create an instance of <typeparamref name="TClient"/>.</param>
        /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to configure the client.</returns>
        /// <remarks>
        /// <para>
        /// <see cref="HttpClient"/> instances that apply the provided configuration can be retrieved using
        /// <see cref="IHttpClientFactory.CreateClient(string)"/> and providing the matching name.
        /// </para>
        /// <para>
        /// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient" />
        /// can be retrieved from <see cref="IServiceProvider.GetService(Type)" /> (and related methods) by providing
        /// <typeparamref name="TClient"/> as the service type.
        /// </para>
        /// </remarks>
    public static IHttpClientBuilder AddHttpClient<TClient, TImplementation>(this IServiceCollection services, string name, Func<HttpClient, IServiceProvider, TImplementation> factory)            where TClient : class            where TImplementation : class, TClient
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }
        
        AddHttpClient(services);
        
        var builder = new DefaultHttpClientBuilder(services, name);
        builder.AddTypedClient<TClient>(factory);
        return builder;
    }
}

```















### 2. http

#### 2.2 http content abstract

```c#
public abstract class HttpContent : IDisposable
{               	
    // Stream or Task<Stream>
    private object? _contentReadStream; 
    
    private bool _disposed;
    private bool _canCalculateLength;
    
    private HttpContentHeaders? _headers;
    public HttpContentHeaders Headers
    {
        get
        {
            if (_headers == null)
            {
                _headers = new HttpContentHeaders(this);
            }
            return _headers;
        }
    }
    
    // buffer stream
    private MemoryStream? _bufferedContent;
    private bool IsBuffered
    {
        get { return _bufferedContent != null; }
    }
                    
    internal bool TryGetBuffer(out ArraySegment<byte> buffer)
    {
        // buffer content 不为 null，-> 返回 buffer content
        if (_bufferedContent != null)
        {
            return _bufferedContent.TryGetBuffer(out buffer);
        }
        
        // buffer content 为 null，-> 创建 default (memory stream) 并返回
        buffer = default;
        return false;
    }
    
    // TODO https://github.com/dotnet/runtime/issues/31316: Expose something to enable this publicly.  
    // For very specific HTTP/2 scenarios (e.g. gRPC), we need to be able to allow request content to continue sending after 
    // SendAsync has completed, which goes against the previous design of content, and which means that with some servers, 
    // even outside of desired scenarios we could end up unexpectedly having request content still sending even after the response
    // completes, which could lead to spurious failures in unsuspecting client code.  To mitigate that, we prohibit duplex
    // on all known HttpContent types, waiting for the request content to complete before completing the SendAsync task.
    internal virtual bool AllowDuplex => true;
    
    protected HttpContent()
    {
        // Log to get an ID for the current content. 
        // This ID is used when the content gets associated to a message.
        if (NetEventSource.Log.IsEnabled()) NetEventSource.Info(this);   
        
        // We start with the assumption that we can calculate the content length.
        _canCalculateLength = true;
    }
    
                                                                                                        
    // Derived types return true if they're able to compute the length. It's OK if derived types return false to
    // indicate that they're not able to compute the length. The transport channel needs to decide what to do in
    // that case (send chunked, buffer first, etc.).
    protected internal abstract bool TryComputeLength(out long length);
    
    internal long? GetComputedOrBufferLength()
    {
        CheckDisposed();
        
        if (IsBuffered)
        {
            return _bufferedContent!.Length;
        }
        
        // If we already tried to calculate the length, but the derived class returned 'false', then don't try
        // again; just return null.
        if (_canCalculateLength)
        {
            long length = 0;
            if (TryComputeLength(out length))
            {
                return length;
            }
            
            // Set flag to make sure next time we don't try to compute the length, since we know that we're unable
            // to do so.
            _canCalculateLength = false;
        }
        return null;
    }
    
    /* dispose */           
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            
            if (_contentReadStream != null)
            {
                Stream? s = _contentReadStream as Stream ??
                    (_contentReadStream is Task<Stream> t && 
                     t.Status == TaskStatus.RanToCompletion ? t.Result : null);
                
                s?.Dispose();
                _contentReadStream = null;
            }
            
            if (IsBuffered)
            {
                _bufferedContent!.Dispose();
            }
        }
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }    
        
    private void CheckDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(this.GetType().ToString());
        }
    }
    
    private void CheckTaskNotNull(Task task)
    {
        if (task == null)
        {
            var e = new InvalidOperationException(SR.net_http_content_no_task_returned);
            if (NetEventSource.Log.IsEnabled()) NetEventSource.Error(this, e);
            throw e;
        }
    }
    
    /* exceptions */
    internal static bool StreamCopyExceptionNeedsWrapping(Exception e) => 
        e is IOException || e is ObjectDisposedException;
    
    private static Exception GetStreamCopyException(Exception originalException)
    {
        // HttpContent derived types should throw HttpRequestExceptions if there is an error. However, since the stream
        // provided by CopyToAsync() can also throw, we wrap such exceptions in HttpRequestException. This way custom content
        // types don't have to worry about it. The goal is that users of HttpContent don't have to catch multiple
        // exceptions (depending on the underlying transport), but just HttpRequestExceptions
        // Custom stream should throw either IOException or HttpRequestException.
        // We don't want to wrap other exceptions thrown by Stream (e.g. InvalidOperationException), since we
        // don't want to hide such "usage error" exceptions in HttpRequestException.
        // ObjectDisposedException is also wrapped, since aborting HWR after a request is complete will result in
        // the response stream being closed.
        return StreamCopyExceptionNeedsWrapping(originalException) ?
            WrapStreamCopyException(originalException) :
        originalException;
    }
    
    internal static Exception WrapStreamCopyException(Exception e)
    {
        Debug.Assert(StreamCopyExceptionNeedsWrapping(e));
        return new HttpRequestException(SR.net_http_content_stream_copy_error, e);
    }
    
    private static Exception CreateOverCapacityException(int maxBufferSize)
    {
        return new HttpRequestException(SR.Format(SR.net_http_content_buffersize_exceeded, maxBufferSize));
    }
            
    // wait and return
    private static async Task<TResult> WaitAndReturnAsync<TState, TResult>(
        Task waitTask, 
        TState state, 
        Func<TState, TResult> returnFunc)
    {
        await waitTask.ConfigureAwait(false);
        return returnFunc(state);
    }
    
                
    internal sealed class LimitArrayPoolWriteStream : Stream
    {
        private const int InitialLength = 256;
        
        private readonly int _maxBufferSize;
        private byte[] _buffer;
        private int _length;
        
        public override long Length => _length;
        public override bool CanWrite => true;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        
        public LimitArrayPoolWriteStream(int maxBufferSize) : this(maxBufferSize, InitialLength) { }
        
        public LimitArrayPoolWriteStream(int maxBufferSize, long capacity)
        {
            if (capacity < InitialLength)
            {
                capacity = InitialLength;
            }
            else if (capacity > maxBufferSize)
            {
                throw CreateOverCapacityException(maxBufferSize);
            }
            
            _maxBufferSize = maxBufferSize;
            _buffer = ArrayPool<byte>.Shared.Rent((int)capacity);
        }
        
        protected override void Dispose(bool disposing)
        {
            Debug.Assert(_buffer != null);
            
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null!;
            
            base.Dispose(disposing);
        }
        
        public ArraySegment<byte> GetBuffer() => new ArraySegment<byte>(_buffer, 0, _length);
        
        public byte[] ToArray()
        {
            var arr = new byte[_length];
            Buffer.BlockCopy(_buffer, 0, arr, 0, _length);
            return arr;
        }
        
        private void EnsureCapacity(int value)
        {
            if ((uint)value > (uint)_maxBufferSize) // value cast handles overflow to negative as well
            {
                throw CreateOverCapacityException(_maxBufferSize);
            }
            else if (value > _buffer.Length)
            {
                Grow(value);
            }
        }
        
        private void Grow(int value)
        {
            Debug.Assert(value > _buffer.Length);
            
            // Extract the current buffer to be replaced.
            byte[] currentBuffer = _buffer;
            _buffer = null!;
            
            // Determine the capacity to request for the new buffer.  It should be at least twice as long as the current one, 
            // if not more if the requested value is more than that.  If the new value would put it longer than the max allowed 
            // byte array, than shrink to that (and if the required length is actually longer than that, we'll let the runtime throw).
            uint twiceLength = 2 * (uint)currentBuffer.Length;
            int newCapacity = twiceLength > Array.MaxLength 
                ? Math.Max(value, Array.MaxLength) 
                : Math.Max(value, (int)twiceLength);
            
            // Get a new buffer, copy the current one to it, return the current one, and set the new buffer as current.
            byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newCapacity);
            Buffer.BlockCopy(currentBuffer, 0, newBuffer, 0, _length);
            ArrayPool<byte>.Shared.Return(currentBuffer);
            _buffer = newBuffer;
        }
        
        public override void Write(byte[] buffer, int offset, int count)
        {
            Debug.Assert(buffer != null);
            Debug.Assert(offset >= 0);
            Debug.Assert(count >= 0);
            
            EnsureCapacity(_length + count);
            Buffer.BlockCopy(buffer, offset, _buffer, _length, count);
            _length += count;
        }
        
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureCapacity(_length + buffer.Length);
            buffer.CopyTo(new Span<byte>(_buffer, _length, buffer.Length));
            _length += buffer.Length;
        }
        
        public override Task WriteAsync(
            byte[] buffer, 
            int offset, 
            int count, 
            CancellationToken cancellationToken)
        {
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }
        
        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer, 
            CancellationToken cancellationToken = default)
        {
            Write(buffer.Span);
            return default;
        }
        
        public override IAsyncResult BeginWrite(
            byte[] buffer, 
            int offset, 
            nt count, 
            AsyncCallback? asyncCallback, 
            object? asyncState) =>
                TaskToApm.Begin(WriteAsync(buffer, offset, count, CancellationToken.None), asyncCallback, asyncState);
        
        public override void EndWrite(IAsyncResult asyncResult) =>
            TaskToApm.End(asyncResult);
        
        public override void WriteByte(byte value)
        {
            int newLength = _length + 1;
            EnsureCapacity(newLength);
            _buffer[_length] = value;
            _length = newLength;
        }
        
        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;        
        public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
        public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
    }
}

```

##### 2.1.0 assert

```c#
public abstract class HttpContent : IDisposable
{
    internal const int MaxBufferSize = int.MaxValue;
    internal static readonly Encoding DefaultStringEncoding = Encoding.UTF8;
    
    private const int UTF8CodePage = 65001;
    private const int UTF8PreambleLength = 3;
    private const byte UTF8PreambleByte0 = 0xEF;
    private const byte UTF8PreambleByte1 = 0xBB;
    private const byte UTF8PreambleByte2 = 0xBF;
    private const int UTF8PreambleFirst2Bytes = 0xEFBB;
    
    private const int UTF32CodePage = 12000;
    private const int UTF32PreambleLength = 4;
    private const byte UTF32PreambleByte0 = 0xFF;
    private const byte UTF32PreambleByte1 = 0xFE;
    private const byte UTF32PreambleByte2 = 0x00;
    private const byte UTF32PreambleByte3 = 0x00;
    private const int UTF32OrUnicodePreambleFirst2Bytes = 0xFFFE;
    
    private const int UnicodeCodePage = 1200;
    private const int UnicodePreambleLength = 2;
    private const byte UnicodePreambleByte0 = 0xFF;
    private const byte UnicodePreambleByte1 = 0xFE;
    
    private const int BigEndianUnicodeCodePage = 1201;
    private const int BigEndianUnicodePreambleLength = 2;
    private const byte BigEndianUnicodePreambleByte0 = 0xFE;
    private const byte BigEndianUnicodePreambleByte1 = 0xFF;
    private const int BigEndianUnicodePreambleFirst2Bytes = 0xFEFF;
    
#if DEBUG
    static HttpContent()
	{
    	// Ensure the encoding constants used in this class match the actual data from the Encoding class
    	AssertEncodingConstants(
            Encoding.UTF8, 
            UTF8CodePage, 
            UTF8PreambleLength, 
            UTF8PreambleFirst2Bytes,
            UTF8PreambleByte0,
            UTF8PreambleByte1,
            UTF8PreambleByte2);
    
    	// UTF32 not supported on Phone
    	AssertEncodingConstants(
            Encoding.UTF32, 
            UTF32CodePage, 
            UTF32PreambleLength, 
            UTF32OrUnicodePreambleFirst2Bytes,
            UTF32PreambleByte0,
            UTF32PreambleByte1,
            UTF32PreambleByte2,
            UTF32PreambleByte3);
    
    	AssertEncodingConstants(
            Encoding.Unicode, 
            UnicodeCodePage, 
            UnicodePreambleLength, 
            UTF32OrUnicodePreambleFirst2Bytes,
            UnicodePreambleByte0,
            UnicodePreambleByte1);
    
    	AssertEncodingConstants(
            Encoding.BigEndianUnicode, 
            BigEndianUnicodeCodePage, 
            BigEndianUnicodePreambleLength, 
            BigEndianUnicodePreambleFirst2Bytes,
            BigEndianUnicodePreambleByte0,
            BigEndianUnicodePreambleByte1);
	}
    
    private static void AssertEncodingConstants(
        Encoding encoding, 
        int codePage, 
        int preambleLength, 
        int first2Bytes, 
        params byte[] preamble)
    {
        Debug.Assert(encoding != null);
        Debug.Assert(preamble != null);
        
        Debug.Assert(
            codePage == encoding.CodePage,
            "Encoding code page mismatch for encoding: " + encoding.EncodingName,
            "Expected (constant): {0}, Actual (Encoding.CodePage): {1}", codePage, encoding.CodePage);
        
        byte[] actualPreamble = encoding.GetPreamble();
        
        Debug.Assert(
            preambleLength == actualPreamble.Length,
            "Encoding preamble length mismatch for encoding: " + encoding.EncodingName,
            "Expected (constant): {0}, Actual (Encoding.GetPreamble().Length): {1}", preambleLength, actualPreamble.Length);
        
        Debug.Assert(actualPreamble.Length >= 2);
        int actualFirst2Bytes = actualPreamble[0] << 8 | actualPreamble[1];
        
        Debug.Assert(
            first2Bytes == actualFirst2Bytes,
            "Encoding preamble first 2 bytes mismatch for encoding: " + encoding.EncodingName,
            "Expected (constant): {0}, Actual: {1}", first2Bytes, actualFirst2Bytes);
        
        Debug.Assert(
            preamble.Length == actualPreamble.Length,
            "Encoding preamble mismatch for encoding: " + encoding.EncodingName,
            "Expected (constant): {0}, Actual (Encoding.GetPreamble()): {1}",
            BitConverter.ToString(preamble),
            BitConverter.ToString(actualPreamble));
        
        for (int i = 0; i < preamble.Length; i++)
        {
            Debug.Assert(
                preamble[i] == actualPreamble[i],
                "Encoding preamble mismatch for encoding: " + encoding.EncodingName,
                "Expected (constant): {0}, Actual (Encoding.GetPreamble()): {1}",
                BitConverter.ToString(preamble),
                BitConverter.ToString(actualPreamble));
        }
    }
#endif
}

```



##### 2.1.1 serialize to stream

* 在派生类实现 序列化 方法

```c#
public abstract class HttpContent : IDisposable
{
    // 同步方法
    protected virtual void SerializeToStream(
        Stream stream, 
        TransportContext? context, 
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException(
            SR.Format(
                SR.net_http_missing_sync_implementation, 
                GetType(), 
                nameof(HttpContent), 
                nameof(SerializeToStream)));
    }
    
    // 异步方法
    protected virtual Task SerializeToStreamAsync(
        Stream stream, 
        TransportContext? context, 
        CancellationToken cancellationToken) => 
            SerializeToStreamAsync(stream, context);
        
    protected abstract Task SerializeToStreamAsync(
        Stream stream, 
        TransportContext? context);
}

```

##### 2.1.2 load into buffer

```c#
public abstract class HttpContent : IDisposable
{    
    // 同步方法
    internal void LoadIntoBuffer(
        long maxBufferSize, 
        CancellationToken cancellationToken)
    {
        CheckDisposed();
        
        // 1- create temporary buffer
        if (!CreateTemporaryBuffer(
            	maxBufferSize, 
            	out MemoryStream? tempBuffer, 
            	out Exception? error))
        {
            // If we already buffered the content, just return.
            return;
        }
        
        if (tempBuffer == null)
        {
            throw error!;
        }
                
        CancellationTokenRegistration cancellationRegistration = 
            cancellationToken.Register(static s => ((HttpContent)s!).Dispose(), this);
        
        try
        {            
            // 2- serialize "temp buffer" to stream
            SerializeToStream(tempBuffer, null, cancellationToken);
            // Rewind after writing data.
            tempBuffer.Seek(0, SeekOrigin.Begin); 
            _bufferedContent = tempBuffer;
        }
        catch (Exception e)
        {
            if (NetEventSource.Log.IsEnabled()) NetEventSource.Error(this, e);
            
            if (CancellationHelper.ShouldWrapInOperationCanceledException(e, cancellationToken))
            {
                throw CancellationHelper.CreateOperationCanceledException(e, cancellationToken);
            }
            
            if (StreamCopyExceptionNeedsWrapping(e))
            {
                throw GetStreamCopyException(e);
            }
            
            throw;
        }
        finally
        {            
            cancellationRegistration.Dispose();
        }
    }
    
    // 异步方法    
    public Task LoadIntoBufferAsync() => LoadIntoBufferAsync(MaxBufferSize);
    
    // No "CancellationToken" parameter needed since canceling the CTS will close the connection, 
    // resulting in an exception being thrown while we're buffering. 
    // If buffering is used without a connection, it is supposed to be fast, thus no cancellation required.
    public Task LoadIntoBufferAsync(long maxBufferSize) =>
        LoadIntoBufferAsync(maxBufferSize, CancellationToken.None);
    
    internal Task LoadIntoBufferAsync(CancellationToken cancellationToken) =>
        LoadIntoBufferAsync(MaxBufferSize, cancellationToken);
    
    internal Task LoadIntoBufferAsync(
        long maxBufferSize, 
        CancellationToken cancellationToken)
    {
        CheckDisposed();
                
        // 1- create temporay buffer
        if (!CreateTemporaryBuffer(
            	maxBufferSize, 
            	out MemoryStream? tempBuffer, 
            	out Exception? error))
        {
            // If we already buffered the content, just return a completed task.
            return Task.CompletedTask;
        }
        
        if (tempBuffer == null)
        {
            // We don't throw in LoadIntoBufferAsync(): return a faulted task.
            return Task.FromException(error!);
        }
        
        try
        {
            // 2- serialize "temp buffer" to stream async
            Task task = SerializeToStreamAsync(tempBuffer, null, cancellationToken);
            CheckTaskNotNull(task);
            return LoadIntoBufferAsyncCore(task, tempBuffer);
        }
        catch (Exception e) when (StreamCopyExceptionNeedsWrapping(e))
        {
            return Task.FromException(GetStreamCopyException(e));
        }
        // other synchronous exceptions from SerializeToStreamAsync/CheckTaskNotNull will propagate
    }
    
    private async Task LoadIntoBufferAsyncCore(
        Task serializeToStreamTask, 
        MemoryStream tempBuffer)
    {
        try
        {
            await serializeToStreamTask.ConfigureAwait(false);
        }
        catch (Exception e)
        {            
            tempBuffer.Dispose(); 
            Exception we = GetStreamCopyException(e);
            if (we != e) throw we;
            throw;
        }
        
        try
        {
            // Rewind after writing data.
            tempBuffer.Seek(0, SeekOrigin.Begin); 
            _bufferedContent = tempBuffer;
        }
        catch (Exception e)
        {
            if (NetEventSource.Log.IsEnabled()) NetEventSource.Error(this, e);
            throw;
        }
    }                
}
    
```

###### 2.1.2.1 create temporary buffer

```c#
public abstract class HttpContent : IDisposable
{   
    private bool CreateTemporaryBuffer(
        long maxBufferSize, 
        out MemoryStream? tempBuffer, 
        out Exception? error)
    {
        if (maxBufferSize > HttpContent.MaxBufferSize)
        {
            // This should only be hit when called directly; 
            // HttpClient/HttpClientHandler will not exceed this limit.
            throw new ArgumentOutOfRangeException(
                nameof(maxBufferSize), 
                maxBufferSize,
                SR.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    SR.net_http_content_buffersize_limit, HttpContent.MaxBufferSize));
        }
        
        if (IsBuffered)
        {
            // If we already buffered the content, just return false.
            tempBuffer = default;
            error = default;
            return false;
        }
        
        tempBuffer = CreateMemoryStream(maxBufferSize, out error);
        return true;
    }
}

```

###### 2.1.2.2 create memory stream

```c#
public abstract class HttpContent : IDisposable
{    
    private MemoryStream? CreateMemoryStream(long maxBufferSize, out Exception? error)
    {
        error = null;
        
        // If we have a Content-Length allocate the right amount of buffer up-front. 
        // Also check whether the content length exceeds the max. buffer size.
        long? contentLength = Headers.ContentLength;
        
        if (contentLength != null)
        {
            Debug.Assert(contentLength >= 0);
            
            if (contentLength > maxBufferSize)
            {
                error = new HttpRequestException(
                    SR.Format(
                        System.Globalization.CultureInfo.InvariantCulture, 
                        SR.net_http_content_buffersize_exceeded, maxBufferSize));
                
                return null;
            }
            
            // We can safely cast contentLength to (int) since we just checked that it is <= maxBufferSize.
            return new LimitMemoryStream((int)maxBufferSize, (int)contentLength);
        }
        
        // We couldn't determine the length of the buffer. Create a memory stream with an empty buffer.
        return new LimitMemoryStream((int)maxBufferSize, 0);
    }            
}

```

###### 2.1.2.3 limit memory stream

```c#
public abstract class HttpContent : IDisposable
{
    // limit memory stream
    internal sealed class LimitMemoryStream : MemoryStream
    {
        private readonly int _maxSize;
        
        public LimitMemoryStream(int maxSize, int capacity) : base(capacity)
        {
            Debug.Assert(capacity <= maxSize);
            _maxSize = maxSize;
        }
        
        private void CheckSize(int countToAdd)
        {
            if (_maxSize - Length < countToAdd)
            {
                throw CreateOverCapacityException(_maxSize);
            }
        }
        
        // get buffer
        public byte[] GetSizedBuffer()
        {
            ArraySegment<byte> buffer;
            return TryGetBuffer(out buffer) && 
                buffer.Offset == 0 && 
                buffer.Count == buffer.Array!.Length 
                	? buffer.Array 
                	: ToArray();
        }
        
        // write byte
        public override void WriteByte(byte value)
        {
            CheckSize(1);
            base.WriteByte(value);
        }
        
        // write
        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckSize(count);
            base.Write(buffer, offset, count);
        }
        
        // write async
        public override Task WriteAsync(
            byte[] buffer, 
            int offset, 
            int count, 
            CancellationToken cancellationToken)
        {
            CheckSize(count);
            return base.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer, 
            CancellationToken cancellationToken)
        {
            CheckSize(buffer.Length);
            return base.WriteAsync(buffer, cancellationToken);
        }
                
        // begin write
        public override IAsyncResult BeginWrite(
            byte[] buffer, 
            int offset, 
            int count, 
            AsyncCallback? callback, 
            object? state)
        {
            CheckSize(count);
            return base.BeginWrite(buffer, offset, count, callback, state);
        }
        
        // end write
        public override void EndWrite(IAsyncResult asyncResult)
        {
            base.EndWrite(asyncResult);
        }
        
        // copy to 
        public override Task CopyToAsync(
            Stream destination, 
            int bufferSize, 
            CancellationToken cancellationToken)
        {
            ArraySegment<byte> buffer;
            if (TryGetBuffer(out buffer))
            {
                ValidateCopyToArguments(destination, bufferSize);
                
                long pos = Position;
                long length = Length;
                Position = length;
                
                long bytesToWrite = length - pos;
                return destination.WriteAsync(
                    buffer.Array!, 
                    (int)(buffer.Offset + pos), 
                    (int)bytesToWrite, 
                    cancellationToken);
            }
            
            return base.CopyToAsync(destination, bufferSize, cancellationToken);
        }                
    }
}

```

##### 2.1.3 copy to

```c#
public abstract class HttpContent : IDisposable
{
    // 同步方法
    public void CopyTo(
        Stream stream, 
        TransportContext? context, 
        CancellationToken cancellationToken)
    {
        CheckDisposed();
        
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }
        
        try
        {
            // 1- get buffer
            if (TryGetBuffer(out ArraySegment<byte> buffer))
            {
                stream.Write(buffer.Array!, buffer.Offset, buffer.Count);
            }
            else
            {
                SerializeToStream(stream, context, cancellationToken);
            }
        }
        catch (Exception e) when (StreamCopyExceptionNeedsWrapping(e))
        {
            throw GetStreamCopyException(e);
        }
    }
    
    // 异步方法
    public Task CopyToAsync(Stream stream) => CopyToAsync(stream, CancellationToken.None);
    
    public Task CopyToAsync(
        Stream stream, 
        CancellationToken cancellationToken) =>
        	CopyToAsync(stream, null, cancellationToken);
    
    public Task CopyToAsync(
        Stream stream, 
        TransportContext? context) =>
        	CopyToAsync(stream, context, CancellationToken.None);
    
    public Task CopyToAsync(
        Stream stream, 
        TransportContext? context, 
        CancellationToken cancellationToken)
    {
        CheckDisposed();
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }
        
        try
        {
            return WaitAsync(InternalCopyToAsync(stream, context, cancellationToken));
        }
        catch (Exception e) when (StreamCopyExceptionNeedsWrapping(e))
        {
            return Task.FromException(GetStreamCopyException(e));
        }
        
        static async Task WaitAsync(ValueTask copyTask)
        {
            try
            {
                await copyTask.ConfigureAwait(false);
            }
            catch (Exception e) when (StreamCopyExceptionNeedsWrapping(e))
            {
                throw WrapStreamCopyException(e);
            }
        }
    }
    
    internal ValueTask InternalCopyToAsync(
        Stream stream, 
        TransportContext? context, 
        CancellationToken cancellationToken)
    {
        if (TryGetBuffer(out ArraySegment<byte> buffer))
        {
            return stream.WriteAsync(buffer, cancellationToken);
        }
        
        Task task = SerializeToStreamAsync(stream, context, cancellationToken);
        CheckTaskNotNull(task);
        return new ValueTask(task);
    }
}

```

##### 2.1.4 read as byte array

```c#
public abstract class HttpContent : IDisposable
{
    public Task<byte[]> ReadAsByteArrayAsync() => ReadAsByteArrayAsync(CancellationToken.None);
    
    public Task<byte[]> ReadAsByteArrayAsync(CancellationToken cancellationToken)
    {
        CheckDisposed();
        return WaitAndReturnAsync(
            LoadIntoBufferAsync(cancellationToken), 
            this, 
            static s => s.ReadBufferedContentAsByteArray());
    }
    
    internal byte[] ReadBufferedContentAsByteArray()
    {
        Debug.Assert(_bufferedContent != null);
        // The returned array is exposed out of the library, 
        // so use ToArray rather than TryGetBuffer in order to make a copy.
        return _bufferedContent.ToArray();
    }
}

```

##### 2.1.5 read as stream

```c#
public abstract class HttpContent : IDisposable
{
    /* 同步方法 */
    public Stream ReadAsStream() => ReadAsStream(CancellationToken.None);
    
    public Stream ReadAsStream(CancellationToken cancellationToken)
        
    {
        CheckDisposed();        
        
        // _contentReadStream will be either null (nothing yet initialized), 
        // a Stream (it was previously initialized in TryReadAsStream/ReadAsStream), 
        // or a Task<Stream> (it was previously initialized in ReadAsStreamAsync).        
        if (_contentReadStream == null) 
        {
            Stream s = TryGetBuffer(out ArraySegment<byte> buffer)                 
                ? new MemoryStream(buffer.Array!, buffer.Offset, buffer.Count, writable: false) 
                // 1- 
                : CreateContentReadStream(cancellationToken);
            
            _contentReadStream = s;
            return s;
        }
        else if (_contentReadStream is Stream stream) 
        {
            return stream;    
        }
        // have a Task<Stream>  
        else           
        {
            // Throw if ReadAsStreamAsync has been called previously since _contentReadStream contains a cached task.
            throw new HttpRequestException(SR.net_http_content_read_as_stream_has_task);
        }
    }
    
    /* 异步方法 */
    public Task<Stream> ReadAsStreamAsync() => ReadAsStreamAsync(CancellationToken.None);
    
    public Task<Stream> ReadAsStreamAsync(CancellationToken cancellationToken)
    {
        CheckDisposed();
        
        // _contentReadStream will be either null (nothing yet initialized), 
        // a Stream (it was previously initialized in TryReadAsStream/ReadAsStream), 
        // or a Task<Stream> (it was previously initialized here in ReadAsStreamAsync).        
        if (_contentReadStream == null) 
        {
            Task<Stream> t = TryGetBuffer(out ArraySegment<byte> buffer) 
                ? Task.FromResult<Stream>(new MemoryStream(buffer.Array!, buffer.Offset, buffer.Count, writable: false)) 
                // 2-
                : CreateContentReadStreamAsync(cancellationToken);
            
            _contentReadStream = t;
            return t;
        }
        // have a Task<Stream>
        else if (_contentReadStream is Task<Stream> t) 
        {
            return t;
        }
        else
        {
            Debug.Assert(
                _contentReadStream is Stream, 
                $"Expected a Stream, got ${_contentReadStream}");
            
            Task<Stream> ts = Task.FromResult((Stream)_contentReadStream);
            _contentReadStream = ts;
            return ts;
        }
    }
    
    /* try read */
    internal Stream? TryReadAsStream()
    {
        CheckDisposed();
        
        // _contentReadStream will be either null (nothing yet initialized), 
        // a Stream (it was previously initialized in TryReadAsStream/ReadAsStream), 
        // or a Task<Stream> (it was previously initialized here in ReadAsStreamAsync).
        
        if (_contentReadStream == null) 
        {
            Stream? s = TryGetBuffer(out ArraySegment<byte> buffer) 
                ? new MemoryStream(buffer.Array!, buffer.Offset, buffer.Count, writable: false) 
                // 3- 
                : TryCreateContentReadStream();
            
            _contentReadStream = s;
            return s;
        }
        else if (_contentReadStream is Stream s) 
        {
            return s;
        }
        // have a Task<Stream>
        else 
        {
            Debug.Assert(
                _contentReadStream is Task<Stream>, 
                $"Expected a Task<Stream>, got ${_contentReadStream}");
            
            Task<Stream> t = (Task<Stream>)_contentReadStream;
            return t.Status == TaskStatus.RanToCompletion ? t.Result : null;
        }
    }
                
}

```

###### 2.1.5.1 create content read stream

```c#
public abstract class HttpContent : IDisposable
{
    protected virtual Stream CreateContentReadStream(CancellationToken cancellationToken)
    {
        LoadIntoBuffer(MaxBufferSize, cancellationToken);
        return _bufferedContent!;
    }
}

```

###### 2.1.5.2 create content read stream async

```c#
public abstract class HttpContent : IDisposable
{
    protected virtual Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
    {        
        return CreateContentReadStreamAsync();
    }
    
    protected virtual Task<Stream> CreateContentReadStreamAsync()
    {
        // By default just buffer the content to a memory stream. 
        // Derived classes can override this behavior if there is a better way to retrieve the content as stream 
        // (e.g. byte array/string use a more efficient way, like wrapping a read-only MemoryStream around the bytes/string)
        return WaitAndReturnAsync(LoadIntoBufferAsync(), this, s => (Stream)s._bufferedContent!);
    }    
}

```

###### 2.1.5.3 try create content read stream

```c#
public abstract class HttpContent : IDisposable
{                
    // As an optimization for internal consumers of HttpContent (e.g. HttpClient.GetStreamAsync), and for
    // HttpContent-derived implementations that override CreateContentReadStreamAsync in a way that always
    // or frequently returns synchronously-completed tasks, we can avoid the task allocation by enabling
    // callers to try to get the Stream first synchronously.
    internal virtual Stream? TryCreateContentReadStream() => null;
}

```

##### 2.1.6 read as string

```c#
public abstract class HttpContent : IDisposable
{
    public Task<string> ReadAsStringAsync() => ReadAsStringAsync(CancellationToken.None);
    
    public Task<string> ReadAsStringAsync(CancellationToken cancellationToken)
    {
        CheckDisposed();
        return WaitAndReturnAsync(
            LoadIntoBufferAsync(cancellationToken), 
            this, 
            static s => s.ReadBufferedContentAsString());
    }
    
    private string ReadBufferedContentAsString()
    {
        Debug.Assert(IsBuffered);
        
        if (_bufferedContent!.Length == 0)
        {
            return string.Empty;
        }
        
        ArraySegment<byte> buffer;
        if (!TryGetBuffer(out buffer))
        {
            buffer = new ArraySegment<byte>(_bufferedContent.ToArray());
        }
        
        return ReadBufferAsString(buffer, Headers);
    }
    
    internal static string ReadBufferAsString(
        ArraySegment<byte> buffer, 
        HttpContentHeaders headers)
    {        
        Encoding? encoding = null;        
        int bomLength = -1;        
        string? charset = headers.ContentType?.CharSet;
        
        // If we do have encoding information in the 'Content-Type' header, 
        // use that information to convert the content to a string.
        if (charset != null)
        {
            try
            {
                // Remove at most a single set of quotes.
                if (charset.Length > 2 &&
                    charset[0] == '\"' &&
                    charset[charset.Length - 1] == '\"')
                {
                    encoding = Encoding.GetEncoding(charset.Substring(1, charset.Length - 2));
                }
                else
                {
                    encoding = Encoding.GetEncoding(charset);
                }
                
                // Byte-order-mark (BOM) characters may be present even if a charset was specified.
                // 1- 
                bomLength = GetPreambleLength(buffer, encoding);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException(SR.net_http_content_invalid_charset, e);
            }
        }
        
        // If no content encoding is listed in the ContentType HTTP header, or no Content-Type header present,
        // then check for a BOM in the data to figure out the encoding.
        if (encoding == null)
        {
            // 2-
            if (!TryDetectEncoding(buffer, out encoding, out bomLength))
            {
                // Use the default encoding (UTF8) if we couldn't detect one.
                encoding = DefaultStringEncoding;
                
                // We already checked to see if the data had a UTF8 BOM in TryDetectEncoding and DefaultStringEncoding is UTF8, 
                // so the bomLength is 0.
                bomLength = 0;
            }
        }
        
        // Drop the BOM when decoding the data.
        return encoding.GetString(
            buffer.Array!, 
            buffer.Offset + bomLength, 
            buffer.Count - bomLength);
    }
}

```

###### 2.1.6.1 get preamble length

```c#
public abstract class HttpContent : IDisposable
{
    private static int GetPreambleLength(ArraySegment<byte> buffer, Encoding encoding)
    {
        byte[]? data = buffer.Array;
        int offset = buffer.Offset;
        int dataLength = buffer.Count;
        
        Debug.Assert(data != null);
        Debug.Assert(encoding != null);
        
        switch (encoding.CodePage)
        {
            case UTF8CodePage:
                return (dataLength >= UTF8PreambleLength && 
                        data[offset + 0] == UTF8PreambleByte0 && 
                        data[offset + 1] == UTF8PreambleByte1 && 
                        data[offset + 2] == UTF8PreambleByte2) 
                    		? UTF8PreambleLength 
                    		: 0;
                
            case UTF32CodePage:
                    return (dataLength >= UTF32PreambleLength && 
                            data[offset + 0] == UTF32PreambleByte0 && 
                            data[offset + 1] == UTF32PreambleByte1 && 
                            data[offset + 2] == UTF32PreambleByte2 && 
                            data[offset + 3] == UTF32PreambleByte3) 
                        		? UTF32PreambleLength 
                        		: 0;
                
            case UnicodeCodePage:
                return (dataLength >= UnicodePreambleLength && 
                        data[offset + 0] == UnicodePreambleByte0 && 
                        data[offset + 1] == UnicodePreambleByte1) 
                    		? UnicodePreambleLength 
                    		: 0;

            case BigEndianUnicodeCodePage:
                return (dataLength >= BigEndianUnicodePreambleLength && 
                        data[offset + 0] == BigEndianUnicodePreambleByte0 && 
                        data[offset + 1] == BigEndianUnicodePreambleByte1) 
                    		? BigEndianUnicodePreambleLength 
                    		: 0;
                
            default:
                byte[] preamble = encoding.GetPreamble();
                return BufferHasPrefix(buffer, preamble) ? preamble.Length : 0;
        }
    }
    
    private static bool BufferHasPrefix(ArraySegment<byte> buffer, byte[] prefix)
    {
        byte[]? byteArray = buffer.Array;
        if (prefix == null || 
            byteArray == null || 
            prefix.Length > buffer.Count || 
            prefix.Length == 0)
            	return false;
        
        for (int i = 0, j = buffer.Offset; i < prefix.Length; i++, j++)
        {
            if (prefix[i] != byteArray[j])
                return false;
        }
        
        return true;
    }
}

```

###### 2.1.6.2 try detect encoding

```c#
public abstract class HttpContent : IDisposable
{
    private static bool TryDetectEncoding(
        ArraySegment<byte> buffer, 
        [NotNullWhen(true)] out Encoding? encoding, 
        out int preambleLength)
    {
        byte[]? data = buffer.Array;
        int offset = buffer.Offset;
        int dataLength = buffer.Count;
        
        Debug.Assert(data != null);
        
        if (dataLength >= 2)
        {
            int first2Bytes = data[offset + 0] << 8 | data[offset + 1];
            
            switch (first2Bytes)
            {
                case UTF8PreambleFirst2Bytes:
                    if (dataLength >= UTF8PreambleLength && 
                        data[offset + 2] == UTF8PreambleByte2)
                    {
                        encoding = Encoding.UTF8;
                        preambleLength = UTF8PreambleLength;
                        return true;
                    }
                    break;
                    
                case UTF32OrUnicodePreambleFirst2Bytes:
                    // UTF32 not supported on Phone
                    if (dataLength >= UTF32PreambleLength && 
                        data[offset + 2] == UTF32PreambleByte2 && 
                        data[offset + 3] == UTF32PreambleByte3)
                    {
                        encoding = Encoding.UTF32;
                        preambleLength = UTF32PreambleLength;
                    }
                    else
                    {
                        encoding = Encoding.Unicode;
                        preambleLength = UnicodePreambleLength;
                    }
                    return true;
                    
                case BigEndianUnicodePreambleFirst2Bytes:
                    encoding = Encoding.BigEndianUnicode;
                    preambleLength = BigEndianUnicodePreambleLength;
                    return true;
            }
        }
        
        encoding = null;
        preambleLength = 0;
        return false;
    }
}

```



#### 2.3 variety of http content

##### 2.3.1 empty content

```c#
internal sealed class EmptyContent : HttpContent
    {
        protected internal override bool TryComputeLength(out long length)
        {
            length = 0;
            return true;
        }

        protected override void SerializeToStream(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        { }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.CompletedTask;

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) =>
            cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) :
            SerializeToStreamAsync(stream, context);

        protected override Stream CreateContentReadStream(CancellationToken cancellationToken) =>
            EmptyReadStream.Instance;

        protected override Task<Stream> CreateContentReadStreamAsync() =>
            Task.FromResult<Stream>(EmptyReadStream.Instance);

        protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) =>
            cancellationToken.IsCancellationRequested ? Task.FromCanceled<Stream>(cancellationToken) :
            CreateContentReadStreamAsync();

        internal override Stream? TryCreateContentReadStream() => EmptyReadStream.Instance;

        internal override bool AllowDuplex => false;
    }
```



##### 2.2.1 byte array content

```c#
public class ByteArrayContent : HttpContent
{
    private readonly byte[] _content;
    private readonly int _offset;
    private readonly int _count;
    
    public ByteArrayContent(byte[] content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }
        
        _content = content;
        _count = content.Length;
    }
    
    public ByteArrayContent(byte[] content, int offset, int count)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }
        if ((offset < 0) || (offset > content.Length))
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        if ((count < 0) || (count > (content.Length - offset)))
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        
        _content = content;
        _offset = offset;
        _count = count;
    }
    
    internal override bool AllowDuplex => false;
    
    // compute length
    protected internal override bool TryComputeLength(out long length)
    {
        length = _count;
        return true;
    }
    
    // serialize 
    protected override void SerializeToStream(
        Stream stream, 
        TransportContext? context, 
        CancellationToken cancellationToken) =>
        	stream.Write(_content, _offset, _count);
    
    protected override Task SerializeToStreamAsync(
        Stream stream, 
        TransportContext? context) =>
        	SerializeToStreamAsyncCore(stream, default);
    
    protected override Task SerializeToStreamAsync(
        Stream stream, 
        TransportContext? context, 
        CancellationToken cancellationToken) =>
        	// Only skip the original protected virtual SerializeToStreamAsync if this isn't a derived type that 
        	// may have overridden the behavior.
        	GetType() == typeof(ByteArrayContent) 
        		? SerializeToStreamAsyncCore(stream, cancellationToken) 
        		: base.SerializeToStreamAsync(stream, context, cancellationToken);
    
    private protected Task SerializeToStreamAsyncCore(
        Stream stream, 
        CancellationToken cancellationToken) =>
        	stream.WriteAsync(_content, _offset, _count, cancellationToken);
    
    // create content read stream    
    protected override Stream CreateContentReadStream(CancellationToken cancellationToken) =>
        CreateMemoryStreamForByteArray();
    
    protected override Task<Stream> CreateContentReadStreamAsync() =>
        Task.FromResult<Stream>(CreateMemoryStreamForByteArray());
    
    internal override Stream? TryCreateContentReadStream() =>
        GetType() == typeof(ByteArrayContent) 
        ? CreateMemoryStreamForByteArray() 
        : null;
        
    internal MemoryStream CreateMemoryStreamForByteArray() => 
        new MemoryStream(_content, _offset, _count, writable: false);        
}

```

###### 2.2.1.1 string content

```c#
public class StringContent : ByteArrayContent
{
    private const string DefaultMediaType = "text/plain";
    
    public StringContent(string content) : this(content, null, null)
    {
    }
    
    public StringContent(string content, Encoding? encoding) : this(content, encoding, null)
    {
    }
    
    public StringContent(string content, Encoding? encoding, string? mediaType) : base(GetContentByteArray(content, encoding))
    {
        // Initialize the 'Content-Type' header with information provided by parameters.
        MediaTypeHeaderValue headerValue = new MediaTypeHeaderValue(
            (mediaType == null) ? DefaultMediaType : mediaType);
        headerValue.CharSet = (encoding == null) 
            ? HttpContent.DefaultStringEncoding.WebName 
            : encoding.WebName;  
        
        Headers.ContentType = headerValue;
    }
    
    // A StringContent is essentially a ByteArrayContent. We serialize the string into a byte-array in the constructor using 
    // encoding information provided by the caller (if any). When this content is sent, the Content-Length can be retrieved easily 
    // (length of the array).
    private static byte[] GetContentByteArray(string content, Encoding? encoding)
    {
        // In this case we treat 'null' strings different from string.Empty in order to be consistent with our
        // other *Content constructors: 'null' throws, empty values are allowed.
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }
        
        if (encoding == null)
        {
            encoding = HttpContent.DefaultStringEncoding;
        }
        
        return encoding.GetBytes(content);
    }
    
    // serialize
    protected override Task SerializeToStreamAsync(
        Stream stream, 
        TransportContext? context, 
        CancellationToken cancellationToken) =>
        	// Only skip the original protected virtual SerializeToStreamAsync if this
	        // isn't a derived type that may have overridden the behavior.
    	    GetType() == typeof(StringContent) 
        		? SerializeToStreamAsyncCore(stream, cancellationToken) 
        		: base.SerializeToStreamAsync(stream, context, cancellationToken);
    
    // try create content read stream
    internal override Stream? TryCreateContentReadStream() =>
        GetType() == typeof(StringContent) 
        	? CreateMemoryStreamForByteArray() 
	        : null;
}

```

###### 2.3.1.2 form url encoded content

```c#
public class FormUrlEncodedContent : ByteArrayContent
    {
        public FormUrlEncodedContent(IEnumerable<KeyValuePair<string?, string?>> nameValueCollection)
            : base(GetContentByteArray(nameValueCollection))
        {
            Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        }

        private static byte[] GetContentByteArray(IEnumerable<KeyValuePair<string?, string?>> nameValueCollection)
        {
            if (nameValueCollection == null)
            {
                throw new ArgumentNullException(nameof(nameValueCollection));
            }

            // Encode and concatenate data
            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string?, string?> pair in nameValueCollection)
            {
                if (builder.Length > 0)
                {
                    builder.Append('&');
                }

                builder.Append(Encode(pair.Key));
                builder.Append('=');
                builder.Append(Encode(pair.Value));
            }

            return HttpRuleParser.DefaultHttpEncoding.GetBytes(builder.ToString());
        }

        private static string Encode(string? data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return string.Empty;
            }
            // Escape spaces as '+'.
            return Uri.EscapeDataString(data).Replace("%20", "+");
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) =>
            // Only skip the original protected virtual SerializeToStreamAsync if this
            // isn't a derived type that may have overridden the behavior.
            GetType() == typeof(FormUrlEncodedContent) ? SerializeToStreamAsyncCore(stream, cancellationToken) :
            base.SerializeToStreamAsync(stream, context, cancellationToken);

        internal override Stream? TryCreateContentReadStream() =>
            GetType() == typeof(FormUrlEncodedContent) ? CreateMemoryStreamForByteArray() : // type check ensures we use possible derived type's CreateContentReadStreamAsync override
            null;
    }
```



##### 2.2.2 stream content

```c#
public class StreamContent : HttpContent
{
    private Stream _content;
    private int _bufferSize;
    private bool _contentConsumed;
    private long _start;
    
    internal override bool AllowDuplex => false;
    
    public StreamContent(Stream content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }
        
        // Indicate that we should use default buffer size by setting size to 0.
        InitializeContent(content, 0);
    }
    
    public StreamContent(Stream content, int bufferSize)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }
        if (bufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize));
        }
        
        InitializeContent(content, bufferSize);
    }
    
    [MemberNotNull(nameof(_content))]
    private void InitializeContent(Stream content, int bufferSize)
    {
        _content = content;
        _bufferSize = bufferSize;
        
        if (content.CanSeek)
        {
            _start = content.Position;
        }
        if (NetEventSource.Log.IsEnabled()) NetEventSource.Associate(this, content);
    }
    
    // compute length
    protected internal override bool TryComputeLength(out long length)
    {
        if (_content.CanSeek)
        {
            length = _content.Length - _start;
            return true;
        }
        else
        {
            length = 0;
            return false;
        }
    }
    
    // serialize 
    protected override void SerializeToStream(
        Stream stream, 
        TransportContext? context, 
        CancellationToken cancellationToken)
    {
        Debug.Assert(stream != null);
        PrepareContent();
        // If the stream can't be re-read, make sure that it gets disposed once it is consumed.
        StreamToStreamCopy.Copy(_content, stream, _bufferSize, !_content.CanSeek);
    }
    
    private void PrepareContent()
    {
        if (_contentConsumed)
        {
            // If the content needs to be written to a target stream a 2nd time, then the stream must support
            // seeking (e.g. a FileStream), otherwise the stream can't be copied a second time to a target
            // stream (e.g. a NetworkStream).
            if (_content.CanSeek)
            {
                _content.Position = _start;
            }
            else
            {
                throw new InvalidOperationException(SR.net_http_content_stream_already_read);
            }
        }
        
        _contentConsumed = true;
    }
    
    protected override Task SerializeToStreamAsync(
        Stream stream, 
        TransportContext? context) =>
        	SerializeToStreamAsyncCore(stream, default);
    
    protected override Task SerializeToStreamAsync(
        Stream stream, 
        TransportContext? context, 
        CancellationToken cancellationToken) =>
        	// Only skip the original protected virtual SerializeToStreamAsync if this
        	// isn't a derived type that may have overridden the behavior.
        	GetType() == typeof(StreamContent) 
        		? SerializeToStreamAsyncCore(stream, cancellationToken) 
        		: base.SerializeToStreamAsync(stream, context, cancellationToken);
    
    private Task SerializeToStreamAsyncCore(Stream stream, CancellationToken cancellationToken)
    {
        Debug.Assert(stream != null);
        PrepareContent();
        return StreamToStreamCopy.CopyAsync(
            _content,
            stream,
            _bufferSize,
            !_content.CanSeek, 	// If the stream can't be re-read, make sure that it gets disposed once it is consumed.
            cancellationToken);
    }
    
    // create content read stream
    protected override Stream CreateContentReadStream(CancellationToken cancellationToken) =>
        new ReadOnlyStream(_content);
    
    protected override Task<Stream> CreateContentReadStreamAsync()
    {
        // Wrap the stream with a read-only stream to prevent someone from writing to the stream.
        return Task.FromResult<Stream>(new ReadOnlyStream(_content));
    }
    
    internal override Stream? TryCreateContentReadStream() =>
        GetType() == typeof(StreamContent) 
        	? new ReadOnlyStream(_content) 
        	: null;
    
    private sealed class ReadOnlyStream : DelegatingStream
    {
        public override bool CanWrite
        {
            get { return false; }
        }
        
        public override int WriteTimeout
        {
            get { throw new NotSupportedException(SR.net_http_content_readonly_stream); }
            set { throw new NotSupportedException(SR.net_http_content_readonly_stream); }
        }
        
        public ReadOnlyStream(Stream innerStream) : base(innerStream)
        {
        }
        
        public override void Flush()
        {
            throw new NotSupportedException(SR.net_http_content_readonly_stream);
        }
        
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException(SR.net_http_content_readonly_stream);
        }
        
        public override void SetLength(long value)
        {
            throw new NotSupportedException(SR.net_http_content_readonly_stream);
        }
        
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException(SR.net_http_content_readonly_stream);
        }
        
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            throw new NotSupportedException(SR.net_http_content_readonly_stream);
        }
        
        public override void WriteByte(byte value)
        {
            throw new NotSupportedException(SR.net_http_content_readonly_stream);
        }
        
        public override Task WriteAsync(byte[] buffer, int offset, int count, Threading.CancellationToken cancellationToken)
        {
            throw new NotSupportedException(SR.net_http_content_readonly_stream);
        }
        
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(SR.net_http_content_readonly_stream);
        }
    }
    
    // dispose    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _content.Dispose();
        }
        base.Dispose(disposing);
    }                         
}

```

##### 2.2.3 read only memory content

```c#
public sealed class ReadOnlyMemoryContent : HttpContent
{
    private readonly ReadOnlyMemory<byte> _content;
    internal override bool AllowDuplex => false;
    
    public ReadOnlyMemoryContent(ReadOnlyMemory<byte> content) => _content = content;
        
    protected internal override bool TryComputeLength(out long length)
    {
        length = _content.Length;
        return true;
    }
    
    // serialize
    protected override void SerializeToStream(
        Stream stream, 
        TransportContext? context, 
        CancellationToken cancellationToken)
    {
        stream.Write(_content.Span);
    }
    
    protected override Task SerializeToStreamAsync(
        Stream stream, 
        TransportContext? context) =>
        	stream.WriteAsync(_content).AsTask();
    
    protected override Task SerializeToStreamAsync(
        Stream stream, 
        TransportContext? context, 
        CancellationToken cancellationToken) =>
        	stream.WriteAsync(_content, cancellationToken).AsTask();
    
    // create content read stream    
    protected override Stream CreateContentReadStream(CancellationToken cancellationToken) =>
        new ReadOnlyMemoryStream(_content);
    
    protected override Task<Stream> CreateContentReadStreamAsync() =>
        Task.FromResult<Stream>(new ReadOnlyMemoryStream(_content));
    
    internal override Stream TryCreateContentReadStream() =>
        new ReadOnlyMemoryStream(_content);        
}

```

##### 2.2.4 multipart content

```c#
public class MultipartContent : HttpContent, IEnumerable<HttpContent>
{       
    private const string CrLf = "\r\n";    
    private const int CrLfLength = 2;
    private const int DashDashLength = 2;
    private const int ColonSpaceLength = 2;
    private const int CommaSpaceLength = 2;
    
    private readonly List<HttpContent> _nestedContent;
    private readonly string _boundary;
    
    internal override bool AllowDuplex => false;
    
    public HeaderEncodingSelector<HttpContent>? HeaderEncodingSelector { get; set; }
    
    public MultipartContent() : this("mixed", GetDefaultBoundary())        
    { 
    }
    
    public MultipartContent(string subtype) : this(subtype, GetDefaultBoundary())
    {
    }
    
    public MultipartContent(string subtype, string boundary)
    {
        if (string.IsNullOrWhiteSpace(subtype))
        {
            throw new ArgumentException(SR.net_http_argument_empty_string, nameof(subtype));
        }
        
        // validate boundary
        ValidateBoundary(boundary);        
        _boundary = boundary;
        
        // quoted boundary
        string quotedBoundary = boundary;
        if (!quotedBoundary.StartsWith('\"'))
        {
            quotedBoundary = "\"" + quotedBoundary + "\"";
        }
        
        // http content header (type)
        MediaTypeHeaderValue contentType = new MediaTypeHeaderValue("multipart/" + subtype);
        contentType.Parameters.Add(new NameValueHeaderValue(nameof(boundary), quotedBoundary));
        Headers.ContentType = contentType;
        
        // 创建 nested content 集合
        _nestedContent = new List<HttpContent>();
    }
    
    private static void ValidateBoundary(string boundary)
    {
        // NameValueHeaderValue is too restrictive for boundary.
        // Instead validate it ourselves and then quote it.
        if (string.IsNullOrWhiteSpace(boundary))
        {
            throw new ArgumentException(SR.net_http_argument_empty_string, nameof(boundary));
        }
        
        // RFC 2046 Section 5.1.1
        // boundary := 0*69<bchars> bcharsnospace
        // bchars := bcharsnospace / " "
        // bcharsnospace := DIGIT / ALPHA / "'" / "(" / ")" / "+" / "_" / "," / "-" / "." / "/" / ":" / "=" / "?"
        if (boundary.Length > 70)
        {
            throw new ArgumentOutOfRangeException(
                nameof(boundary), 
                boundary,
                SR.Format(
                    System.Globalization.CultureInfo.InvariantCulture, 
                    R.net_http_content_field_too_long, 70));
        }
        // Cannot end with space.
        if (boundary.EndsWith(' '))
        {              
            throw new ArgumentException(
                SR.Format(
                    System.Globalization.CultureInfo.InvariantCulture, 
                    SR.net_http_headers_invalid_value, boundary), 
                nameof(boundary));
        }
        
        const string AllowedMarks = @"'()+_,-./:=? ";
        
        foreach (char ch in boundary)
        {
            if (('0' <= ch && ch <= '9') || // Digit.
                ('a' <= ch && ch <= 'z') || // alpha.
                ('A' <= ch && ch <= 'Z') || // ALPHA.
                (AllowedMarks.Contains(ch))) // Marks.
            {
                // Valid.
            }
            else
            {
                throw new ArgumentException(
                    SR.Format(
                        System.Globalization.CultureInfo.InvariantCulture, 
                        SR.net_http_headers_invalid_value, boundary), 
                    nameof(boundary));
            }
        }
    }
    
    private static string GetDefaultBoundary()
    {
        return Guid.NewGuid().ToString();
    }
    
    // 方法- add http conten
    public virtual void Add(HttpContent content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }
        
        _nestedContent.Add(content);
    }
           
    // dispose
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (HttpContent content in _nestedContent)
            {
                content.Dispose();
            }
            _nestedContent.Clear();
        }
        base.Dispose(disposing);
    }
         
    // enumerator
    public IEnumerator<HttpContent> GetEnumerator()
    {
        return _nestedContent.GetEnumerator();
    }
       
    Collections.IEnumerator Collections.IEnumerable.GetEnumerator()
    {
        return _nestedContent.GetEnumerator();
    }
    
                                                                 
    protected internal override bool TryComputeLength(out long length)
    {
        // Start Boundary.
        long currentLength = DashDashLength + _boundary.Length + CrLfLength;
        
        if (_nestedContent.Count > 1)
        {
            // Internal boundaries
            currentLength += (_nestedContent.Count - 1) * (CrLfLength + DashDashLength + _boundary.Length + CrLfLength);
        }
        
        foreach (HttpContent content in _nestedContent)
        {
            // Headers.
            foreach (KeyValuePair<string, HeaderStringValues> headerPair in content.Headers.NonValidated)
            {
                currentLength += headerPair.Key.Length + ColonSpaceLength;
                
                Encoding headerValueEncoding = HeaderEncodingSelector?.Invoke(headerPair.Key, content) 
                    ?? HttpRuleParser.DefaultHttpEncoding;
                
                int valueCount = 0;
                foreach (string value in headerPair.Value)
                {
                    currentLength += headerValueEncoding.GetByteCount(value);
                    valueCount++;
                }
                
                if (valueCount > 1)
                {
                    currentLength += (valueCount - 1) * CommaSpaceLength;
                }
                
                currentLength += CrLfLength;
            }
            
            currentLength += CrLfLength;
            
            // Content.
            if (!content.TryComputeLength(out long tempContentLength))
            {
                length = 0;
                return false;
            }
            currentLength += tempContentLength;
        }
        
        // Terminating boundary.
        currentLength += CrLfLength + DashDashLength + _boundary.Length + DashDashLength + CrLfLength;
        
        length = currentLength;
        return true;
    }                         
}

```

###### 2.2.4.1 重写- serialize to stream

```c#
public class MultipartContent : HttpContent, IEnumerable<HttpContent>
{
    protected override void SerializeToStream(
        Stream stream, 
        TransportContext? context, 
        CancellationToken cancellationToken)
    {
        Debug.Assert(stream != null);
        try
        {
            // 1-
            // Write start boundary.
            WriteToStream(stream, "--" + _boundary + CrLf);
            
            // Write each nested content.
            for (int contentIndex = 0; contentIndex < _nestedContent.Count; contentIndex++)
            {
                // Write divider, headers, and content.
                HttpContent content = _nestedContent[contentIndex];
                // 2-
                SerializeHeadersToStream(stream, content, writeDivider: contentIndex != 0);
                content.CopyTo(stream, context, cancellationToken);
            }
            
            // Write footer boundary.
            WriteToStream(stream, CrLf + "--" + _boundary + "--" + CrLf);
        }
        catch (Exception ex)
        {
            if (NetEventSource.Log.IsEnabled()) NetEventSource.Error(this, ex);
            throw;
        }
    }
    
    protected override Task SerializeToStreamAsync(
        Stream stream, 
        TransportContext? context) =>
        	SerializeToStreamAsyncCore(stream, context, default);
    
    protected override Task SerializeToStreamAsync(
        Stream stream, 
        TransportContext? context, 
        CancellationToken cancellationToken) =>
        	// Only skip the original protected virtual SerializeToStreamAsync if this
	        // isn't a derived type that may have overridden the behavior.
    	    GetType() == typeof(MultipartContent) 
        		? SerializeToStreamAsyncCore(stream, context, cancellationToken) 
        		: base.SerializeToStreamAsync(stream, context, cancellationToken);
    
    private protected async Task SerializeToStreamAsyncCore(
        Stream stream, 
        TransportContext? context, 
        CancellationToken cancellationToken)
    {
        Debug.Assert(stream != null);
        try
        {
            // 3-
            // Write start boundary.
            await EncodeStringToStreamAsync(stream, "--" + _boundary + CrLf, cancellationToken)
                .ConfigureAwait(false);
            
            // Write each nested content.
            var output = new MemoryStream();
            for (int contentIndex = 0; contentIndex < _nestedContent.Count; contentIndex++)
            {
                // Write divider, headers, and content.
                HttpContent content = _nestedContent[contentIndex];
                
                output.SetLength(0);
                SerializeHeadersToStream(output, content, writeDivider: contentIndex != 0);
                output.Position = 0;
                await output.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);                
                await content.CopyToAsync(stream, context, cancellationToken).ConfigureAwait(false);
            }
            
            // 3-
            // Write footer boundary.
            await EncodeStringToStreamAsync(stream, CrLf + "--" + _boundary + "--" + CrLf, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (NetEventSource.Log.IsEnabled()) NetEventSource.Error(this, ex);
            throw;
        }
    }
    
    // 1- 
    private static void WriteToStream(Stream stream, string content) =>
        WriteToStream(stream, content, HttpRuleParser.DefaultHttpEncoding);
    
    private static void WriteToStream(Stream stream, string content, Encoding encoding)
    {
        const int StackallocThreshold = 1024;
        
        int maxLength = encoding.GetMaxByteCount(content.Length);
        
        byte[]? rentedBuffer = null;
        Span<byte> buffer = maxLength <= StackallocThreshold
            ? stackalloc byte[StackallocThreshold]
            : (rentedBuffer = ArrayPool<byte>.Shared.Rent(maxLength));
        
        try
        {
            int written = encoding.GetBytes(content, buffer);
            stream.Write(buffer.Slice(0, written));
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }
    
    // 2-
    private void SerializeHeadersToStream(Stream stream, HttpContent content, bool writeDivider)
    {
        // Add divider.
        if (writeDivider) // Write divider for all but the first content.
        {
            WriteToStream(stream, CrLf + "--"); // const strings
            WriteToStream(stream, _boundary);
            WriteToStream(stream, CrLf);
        }
        
        // Add headers.
        foreach (KeyValuePair<string, HeaderStringValues> headerPair in content.Headers.NonValidated)
        {
            Encoding headerValueEncoding = HeaderEncodingSelector?.Invoke(headerPair.Key, content) 
                ?? HttpRuleParser.DefaultHttpEncoding;
            
            WriteToStream(stream, headerPair.Key);
            WriteToStream(stream, ": ");
            string delim = string.Empty;
            foreach (string value in headerPair.Value)
            {
                WriteToStream(stream, delim);
                WriteToStream(stream, value, headerValueEncoding);
                delim = ", ";
            }
            WriteToStream(stream, CrLf);
        }
        
        // Extra CRLF to end headers (even if there are no headers).
        WriteToStream(stream, CrLf);
    }
    
    // 3-
    private static ValueTask EncodeStringToStreamAsync(Stream stream, string input, CancellationToken cancellationToken)
    {
        byte[] buffer = HttpRuleParser.DefaultHttpEncoding.GetBytes(input);
        return stream.WriteAsync(new ReadOnlyMemory<byte>(buffer), cancellationToken);
    }    
}

```

###### 2.2.4.2 重写- create content read stream

```c#
public class MultipartContent : HttpContent, IEnumerable<HttpContent>
{
    protected override Stream CreateContentReadStream(CancellationToken cancellationToken)
    {
        ValueTask<Stream> task = CreateContentReadStreamAsyncCore(async: false, cancellationToken);
        Debug.Assert(task.IsCompleted);
        return task.GetAwaiter().GetResult();
    }
    
    protected override Task<Stream> CreateContentReadStreamAsync() =>
        CreateContentReadStreamAsyncCore(async: true, CancellationToken.None).AsTask();
    
    protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) =>
        // Only skip the original protected virtual CreateContentReadStreamAsync if this
        // isn't a derived type that may have overridden the behavior.
        GetType() == typeof(MultipartContent) 
        	? CreateContentReadStreamAsyncCore(async: true, cancellationToken).AsTask() 
        	: base.CreateContentReadStreamAsync(cancellationToken);
    
    private async ValueTask<Stream> CreateContentReadStreamAsyncCore(
        bool async, 
        CancellationToken cancellationToken)
    {
        try
        {
            var streams = new Stream[2 + (_nestedContent.Count * 2)];
            int streamIndex = 0;
            
            // 1-
            // Start boundary.
            streams[streamIndex++] = EncodeStringToNewStream("--" + _boundary + CrLf);
            
            // Each nested content.
            for (int contentIndex = 0; contentIndex < _nestedContent.Count; contentIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                HttpContent nestedContent = _nestedContent[contentIndex];
                // 2- 
                streams[streamIndex++] = EncodeHeadersToNewStream(nestedContent, writeDivider: contentIndex != 0);
                
                Stream readStream;
                if (async)
                {
                    readStream = nestedContent.TryReadAsStream() 
                        ?? await nestedContent.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    readStream = nestedContent.ReadAsStream(cancellationToken);
                }
                // Cannot be null, at least an empty stream is necessary.
                readStream ??= new MemoryStream();
                
                if (!readStream.CanSeek)
                {
                    // Seekability impacts whether HttpClientHandlers are able to rewind. To maintain compat
                    // and to allow such use cases when a nested stream isn't seekable (which should be rare),
                    // we fall back to the base behavior. We don't dispose of the streams already obtained
                    // as we don't necessarily own them yet.
                    
#pragma warning disable CA2016
                    // Do not pass a cancellationToken to base.CreateContentReadStreamAsync() as it would trigger 
    			   // an infinite loop => StackOverflow
                    return async 
    				  ? await base.CreateContentReadStreamAsync().ConfigureAwait(false) 
    				  : base.CreateContentReadStream(cancellationToken);
#pragma warning restore CA2016
                    }
                    streams[streamIndex++] = readStream;
                }

                // Footer boundary.
                streams[streamIndex] = EncodeStringToNewStream(CrLf + "--" + _boundary + "--" + CrLf);

            	// 3-
                return new ContentReadStream(streams);
            }
            catch (Exception ex)
            {
                if (NetEventSource.Log.IsEnabled()) NetEventSource.Error(this, ex);
                throw;
            }
    }

    // 1- 
    private static Stream EncodeStringToNewStream(string input)
    {
        return new MemoryStream(HttpRuleParser.DefaultHttpEncoding.GetBytes(input), writable: false);
    }
    
    // 2-
    private Stream EncodeHeadersToNewStream(HttpContent content, bool writeDivider)
    {
        var stream = new MemoryStream();
        SerializeHeadersToStream(stream, content, writeDivider);
        stream.Position = 0;
        return stream;
    }
        
    // 3- 
    private sealed class ContentReadStream : Stream
    {
        private readonly Stream[] _streams;
        private readonly long _length;
        
        private int _next;
        private Stream? _current;
                        
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        
        public override long Length => _length;
        public override void SetLength(long value) 
        { 
            throw new NotSupportedException(); 
        }
        
        private long _position;
        public override long Position
        {
            get { return _position; }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                
                long previousStreamsLength = 0;
                for (int i = 0; i < _streams.Length; i++)
                {
                    Stream curStream = _streams[i];
                    long curLength = curStream.Length;
                    
                    if (value < previousStreamsLength + curLength)
                    {
                        _current = curStream;
                        i++;
                        _next = i;
                        
                        curStream.Position = value - previousStreamsLength;
                        for (; i < _streams.Length; i++)
                        {
                            _streams[i].Position = 0;
                        }
                        
                        _position = value;
                        return;
                    }
                    
                    previousStreamsLength += curLength;
                }
                
                _current = null;
                _next = _streams.Length;
                _position = value;
            }
        }
        
        internal ContentReadStream(Stream[] streams)
        {
            Debug.Assert(streams != null);
            _streams = streams;
            foreach (Stream stream in streams)
            {
                _length += stream.Length;
            }
        }
        
        // dispose
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (Stream s in _streams)
                {
                    s.Dispose();
                }
            }
        }
        
        public override async ValueTask DisposeAsync()
        {
            foreach (Stream s in _streams)
            {
                await s.DisposeAsync().ConfigureAwait(false);
            }
        }
        
        // read        
        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            if (count == 0)
            {
                return 0;
            }
            
            while (true)
            {
                if (_current != null)
                {
                    int bytesRead = _current.Read(buffer, offset, count);
                    if (bytesRead != 0)
                    {
                        _position += bytesRead;
                        return bytesRead;
                    }
                    
                    _current = null;
                }
                
                if (_next >= _streams.Length)
                {
                    return 0;
                }
                
                _current = _streams[_next++];
            }
        }
        
        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                return 0;
            }
            
            while (true)
            {
                if (_current != null)
                {
                    int bytesRead = _current.Read(buffer);
                    if (bytesRead != 0)
                    {
                        _position += bytesRead;
                        return bytesRead;
                    }
                    
                    _current = null;
                }
                
                if (_next >= _streams.Length)
                {
                    return 0;
                }
                
                _current = _streams[_next++];
            }
        }
        
        // read async
        public override Task<int> ReadAsync(
            byte[] buffer, 
            int offset, 
            int count, 
            CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            return ReadAsyncPrivate(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
        }
        
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer, 
            CancellationToken cancellationToken = default) =>
            	ReadAsyncPrivate(buffer, cancellationToken);
        
        public async ValueTask<int> ReadAsyncPrivate(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (buffer.Length == 0)
            {
                return 0;
            }
            
            while (true)
            {
                if (_current != null)
                {
                    int bytesRead = await _current.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (bytesRead != 0)
                    {
                        _position += bytesRead;
                        return bytesRead;
                    }
                    
                    _current = null;
                }
                
                if (_next >= _streams.Length)
                {
                    return 0;
                }
                
                _current = _streams[_next++];
            }
        }
        
        // begin read
        public override IAsyncResult BeginRead(
            byte[] array,
            int offset, 
            int count, 
            AsyncCallback? asyncCallback, 
            object? asyncState) =>
            	TaskToApm.Begin(
            		ReadAsync(array, offset, count, CancellationToken.None), 
            		asyncCallback, 
            		asyncState);
        
        // end read
        public override int EndRead(IAsyncResult asyncResult) =>
            TaskToApm.End<int>(asyncResult);
        
              
        // seek
        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                    
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                    
                case SeekOrigin.End:
                    Position = _length + offset;
                    break;
                    
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin));
            }
            
            return Position;
        }                
        
        // flush
        public override void Flush() { }
        
        // write
        public override void Write(byte[] buffer, int offset, int count) 
        {
            throw new NotSupportedException(); 
        }
        
        public override void Write(ReadOnlySpan<byte> buffer) 
        {
            throw new NotSupportedException(); 
        }
        
        // write async
        public override Task WriteAsync(
            byte[] buffer, 
            int offset, 
            int count, 
            CancellationToken cancellationToken) 
        {
            throw new NotSupportedException(); 
        }
        
        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer, 
            CancellationToken cancellationToken = default) 
        {
            throw new NotSupportedException(); 
        }
    }    
}

```

##### 2.2.5 multipart form data content

```c#
public class MultipartFormDataContent : MultipartContent
{
    private const string formData = "form-data";
    
    public MultipartFormDataContent() : base(formData)
    {
    }
    
    public MultipartFormDataContent(string boundary) : base(formData, boundary)
    {
    }
    
    public override void Add(HttpContent content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }
        
        if (content.Headers.ContentDisposition == null)
        {
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue(formData);
        }
        
        base.Add(content);
    }
    
    public void Add(HttpContent content, string name)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(SR.net_http_argument_empty_string, nameof(name));
        }
        
        AddInternal(content, name, null);
    }
    
    public void Add(HttpContent content, string name, string fileName)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(SR.net_http_argument_empty_string, nameof(name));
        }
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException(SR.net_http_argument_empty_string, nameof(fileName));
        }
        
        AddInternal(content, name, fileName);
    }
    
    private void AddInternal(HttpContent content, string name, string? fileName)
    {
        if (content.Headers.ContentDisposition == null)
        {
            // 创建 content disposition header value
            ContentDispositionHeaderValue header = new ContentDispositionHeaderValue(formData);
            header.Name = name;
            header.FileName = fileName;
            header.FileNameStar = fileName;
            
            // 注入 http content headers
            content.Headers.ContentDisposition = header;
            
        }
        // 注入 http content 
        base.Add(content);
    }
    
    protected override Task SerializeToStreamAsync(
        Stream stream, 
        TransportContext? context, 
        CancellationToken cancellationToken) =>
        	// Only skip the original protected virtual SerializeToStreamAsync if this
        	// isn't a derived type that may have overridden the behavior.
        	GetType() == typeof(MultipartFormDataContent) 
        		? SerializeToStreamAsyncCore(stream, context, cancellatioen) 
        		: base.SerializeToStreamAsync(stream, context, cancellationToken);
}

```















#### 2.3 http message invoker

```c#

```



