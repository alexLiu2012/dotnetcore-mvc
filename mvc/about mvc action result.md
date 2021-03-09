## about mvc action result



### 1. about



### 2. action result

#### 2.1 action result 抽象

##### 2.1.1 action result

* 表示结果的模型

###### 2.1.1.1 接口

```c#
public interface IActionResult
{    
    Task ExecuteResultAsync(ActionContext context);
}

```

###### 2.1.1.2 抽象基类

```c#
public abstract class ActionResult : IActionResult
{    
    public virtual Task ExecuteResultAsync(ActionContext context)
    {
        ExecuteResult(context);
        return Task.CompletedTask;
    }
        
    public virtual void ExecuteResult(ActionContext context)
    {
    }
}

```

##### 2.1.2 action result executor

* 真正执行`action result`中`execute`方法的执行器

```c#
public interface IActionResultExecutor<in TResult> 
    where TResult : notnull, IActionResult
{    
    Task ExecuteAsync(ActionContext context, TResult result);
}

```

##### 2.1.3 action result type mapper

* 提供给上层服务使用的接口，
* 可以将 value 转换成指定的`action result`

###### 2.1.3.1 接口

```c#
public interface IActionResultTypeMapper
{        
    Type GetResultDataType(Type returnType);        
    IActionResult Convert(object? value, Type returnType);
}

```

###### 2.1.3.2 实现

```c#
internal class ActionResultTypeMapper : IActionResultTypeMapper
{
    public Type GetResultDataType(Type returnType)
    {
        if (returnType == null)
        {
            throw new ArgumentNullException(nameof(returnType));
        }
        // 如果 return type 是泛型类型，即 result<T>，
        // 获取泛型类型，即 T 的类型
        if (returnType.IsGenericType &&
            returnType.GetGenericTypeDefinition() == typeof(ActionResult<>))
        {
            return returnType.GetGenericArguments()[0];
        }
        
        return returnType;
    }
    
    public IActionResult Convert(object? value, Type returnType)
    {
        if (returnType == null)
        {
            throw new ArgumentNullException(nameof(returnType));
        }
        // 如果 return type 实现了 IConvertToActionResult 接口，
        if (value is IConvertToActionResult converter)
        {
            // 调用接口的 convert 方法
            return converter.Convert();
        }
        // 否则，创建 object result
        return new ObjectResult(value)
        {
            DeclaredType = returnType,
        };
    }
}

```

###### 2.1.3.3 convert to result 接口

```c#
public interface IConvertToActionResult
{    
    IActionResult Convert();
}

```

#### 2.2 empty result

```c#
public class EmptyResult : ActionResult
{
    /// <inheritdoc />
    public override void ExecuteResult(ActionContext context)
    {
    }
}

```

#### 2.3 file result

##### 2.3.1 file result 抽象

###### 2.3.1.1 file result 抽象基类

* 定义了基本属性，由`executor`真正执行`execute`方法

```c#
public abstract class FileResult : ActionResult
{        
    // http media 的 content type
    public string ContentType { get; }
    
    /* for client cache */
    public DateTimeOffset? LastModified { get; set; }        
    public EntityTagHeaderValue EntityTag { get; set; }        
    public bool EnableRangeProcessing { get; set; }
    
    /* file download name */
    private string _fileDownloadName;
    public string FileDownloadName
    {
        get 
        {
            return _fileDownloadName ?? string.Empty; 
        }
        set 
        {
            _fileDownloadName = value; 
        }
    }
    
    protected FileResult(string contentType)
    {
        if (contentType == null)
        {
            throw new ArgumentNullException(nameof(contentType));
        }
        
        ContentType = contentType;
    }                                   
}

```

###### 2.3.1.2 file result executor base

```c#
public class FileResultExecutorBase
{        
    private const string AcceptRangeHeaderValue = "bytes";        
    protected const int BufferSize = 64 * 1024;
    
    protected ILogger Logger { get; }                    
    public FileResultExecutorBase(ILogger logger)
    {
        Logger = logger;
    }
                                                                  
    protected static ILogger CreateLogger<T>(ILoggerFactory factory)
    {
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }
        
        return factory.CreateLogger<T>();
    }
                
    private static DateTimeOffset RoundDownToWholeSeconds(DateTimeOffset dateTimeOffset)
    {
        var ticksToRemove = dateTimeOffset.Ticks % TimeSpan.TicksPerSecond;
        return dateTimeOffset.Subtract(TimeSpan.FromTicks(ticksToRemove));
    }
}

```

###### 2.3.1.3 write async 

```c#
public class FileResultExecutorBase
{
    protected static async Task WriteFileAsync(
        HttpContext context, 
        Stream fileStream, 
        RangeItemHeaderValue? range, 
        long rangeLength)
    {
        // 获取 http response body 的引用
        var outputStream = context.Response.Body;
        
        using (fileStream)
        {
            try
            {
                // 如果没有指定 range header value
                if (range == null)
                {
                    // 将 file stream 复制到 http response body(stream)
                    await StreamCopyOperation.CopyToAsync(
                        fileStream, 
                        outputStream, 
                        count: null, 
                        bufferSize: BufferSize, 
                        cancel: context.RequestAborted);
                }
                // 如果指定了 range header value
                else
                {
                    // 从 file stream 中 截取符合 range 的部分
                    fileStream.Seek(range.From!.Value, SeekOrigin.Begin);
                    // 将截取部分复制到 http response body(stream)
                    await StreamCopyOperation.CopyToAsync(
                        fileStream, 
                        outputStream, 
                        rangeLength, 
                        BufferSize, 
                        context.RequestAborted);
                }
            }
            catch (OperationCanceledException)
            {
                // Don't throw this exception, 
                // it's most likely caused by the client disconnecting.
                // However, if it was cancelled for any other reason 
                // we need to prevent empty responses.
                context.Abort();
            }
        }
    }
}

```

###### 2.3.1.4 set header and log

```c#
public class FileResultExecutorBase
{
    // set headers and log
    protected virtual 
        (RangeItemHeaderValue? range, 
         long rangeLength, 
         bool serveBody) SetHeadersAndLog(
            ActionContext context,
            FileResult result,
            long? fileLength,
            bool enableRangeProcessing,
            DateTimeOffset? lastModified = null,
            EntityTagHeaderValue? etag = null)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }
        
        /* 解析 http request header */
        var request = context.HttpContext.Request;
        var httpRequestHeaders = request.GetTypedHeaders();
        
        /* 解析 last modified，
           如果 last modified 有值，取整到 second */
        // Since the 'Last-Modified' and other similar http date headers 
        // are rounded down to whole seconds,
        // round down current file's last modified to whole seconds for correct comparison.
        if (lastModified.HasValue)
        {
            lastModified = RoundDownToWholeSeconds(lastModified.Value);
        }
        
        /* a - 获取 pre condition state */
        var preconditionState = GetPreconditionState(
            						httpRequestHeaders, 
            						lastModified, 
            						etag);
        
        /* b - 设置 last modified and etag header */
        var response = context.HttpContext.Response;
        SetLastModifiedAndEtagHeaders(
            response, 
            lastModified, 
            etag);

        /* 如果解析到的 precondition 是 304 或者 412，
           返回对应的元组 */
        // Short circuit if the preconditional headers process to 
        // 304 (NotModified) or 412 (PreconditionFailed)
        if (preconditionState == PreconditionState.NotModified)
        {
            response.StatusCode = StatusCodes.Status304NotModified;
            return (range: null, 
                    rangeLength: 0, 
                    serveBody: false);
        }
        else if (preconditionState == PreconditionState.PreconditionFailed)
        {
            response.StatusCode = StatusCodes.Status412PreconditionFailed;
            return (range: null, 
                    rangeLength: 0, 
                    serveBody: false);
        }
        
        /* c - 设置 content type */
        SetContentType(context, result);
        SetContentDispositionHeader(context, result);
        
        /* file length 有值，即 file 不为空 */
        if (fileLength.HasValue)
        {
            /* 设置 content length */
            // Assuming the request is not a range request, 
            // and the response body is not empty, the Content-Length header is set to 
            // the length of the entire file. 
            // If the request is a valid range request, 
            // this header is overwritten with the length of the range as part of the 
            // range processing (see method SetContentLength).            
            response.ContentLength = fileLength.Value;
            
            /* 设置 accept range header */
            // Handle range request
            if (enableRangeProcessing)
            {
                /* d1 - 设置 accept range header */
                SetAcceptRangeHeader(response);
                
                // If the request method is HEAD or GET, 
                // PreconditionState is Unspecified or ShouldProcess, 
                // and IfRange header is valid,
                // range should be processed and Range headers should be set
                if ((HttpMethods.IsHead(request.Method) || 
                     HttpMethods.IsGet(request.Method)) 
                    && 
                    (preconditionState == PreconditionState.Unspecified || 
                     preconditionState == PreconditionState.ShouldProcess)
                    && 
                    /* d2 - 判断 range valid */
                    (IfRangeValid(
                        httpRequestHeaders, 
                        lastModified, 
                        etag)))
                {
                    /* d3 - 设置 range headers */
                    return SetRangeHeaders(context, 
                                           httpRequestHeaders, 
                                           fileLength.Value);
                }
            }            
            else
            {
                Logger.NotEnabledForRangeProcessing();
            }
        }
        
        return (range: null, 
                rangeLength: 0, 
                serveBody: !HttpMethods.IsHead(request.Method));
    }        
}

```

######  a - get precondition state

```c#
public class FileResultExecutorBase
{
    // precondition 枚举    
    internal enum PreconditionState
    {
        Unspecified,
        NotModified,
        ShouldProcess,
        PreconditionFailed
    }
    
    internal PreconditionState GetPreconditionState(
        RequestHeaders httpRequestHeaders,
        DateTimeOffset? lastModified,
        EntityTagHeaderValue? etag)
    {
        var ifMatchState = PreconditionState.Unspecified;
        var ifNoneMatchState = PreconditionState.Unspecified;
        var ifModifiedSinceState = PreconditionState.Unspecified;
        var ifUnmodifiedSinceState = PreconditionState.Unspecified;
        
        // 14.24 If-Match
        var ifMatch = httpRequestHeaders.IfMatch;
        if (etag != null)
        {
            ifMatchState = GetEtagMatchState(
                useStrongComparison: true,
                etagHeader: ifMatch,
                etag: etag,
                matchFoundState: PreconditionState.ShouldProcess,
                matchNotFoundState: PreconditionState.PreconditionFailed);
            
            if (ifMatchState == PreconditionState.PreconditionFailed)
            {
                Logger.IfMatchPreconditionFailed(etag);
            }
        }
        
        // 14.26 If-None-Match
        var ifNoneMatch = httpRequestHeaders.IfNoneMatch;
        if (etag != null)
        {
            ifNoneMatchState = GetEtagMatchState(
                useStrongComparison: false,
                etagHeader: ifNoneMatch,
                etag: etag,
                matchFoundState: PreconditionState.NotModified,
                matchNotFoundState: PreconditionState.ShouldProcess);
        }
        
        var now = RoundDownToWholeSeconds(DateTimeOffset.UtcNow);
        
        // 14.25 If-Modified-Since
        var ifModifiedSince = httpRequestHeaders.IfModifiedSince;
        if (lastModified.HasValue && 
            ifModifiedSince.HasValue && 
            ifModifiedSince <= now)
        {
            var modified = ifModifiedSince < lastModified;
            ifModifiedSinceState = modified 
                ? PreconditionState.ShouldProcess 
                : PreconditionState.NotModified;
        }
        
        // 14.28 If-Unmodified-Since
        var ifUnmodifiedSince = httpRequestHeaders.IfUnmodifiedSince;
        if (lastModified.HasValue && 
            ifUnmodifiedSince.HasValue && 
            ifUnmodifiedSince <= now)
        {
            var unmodified = ifUnmodifiedSince >= lastModified;
            ifUnmodifiedSinceState = unmodified 
                ? PreconditionState.ShouldProcess 
                : PreconditionState.PreconditionFailed;
            
            if (ifUnmodifiedSinceState == PreconditionState.PreconditionFailed)
            {
                Logger.IfUnmodifiedSincePreconditionFailed(
                    lastModified, 
                    ifUnmodifiedSince);
            }
        }
        
        var state = GetMaxPreconditionState(
            ifMatchState, 
            ifNoneMatchState, 
            ifModifiedSinceState, 
            ifUnmodifiedSinceState);
        
        return state;
    }    
    
    // 通过 etag 判断 precondition 状态
    private static PreconditionState GetEtagMatchState(
        bool useStrongComparison,
        IList<EntityTagHeaderValue> etagHeader,
        EntityTagHeaderValue etag,
        PreconditionState matchFoundState,
        PreconditionState matchNotFoundState)
    {
        if (etagHeader?.Count > 0)
        {
            var state = matchNotFoundState;
            foreach (var entityTag in etagHeader)
            {
                if (entityTag.Equals(EntityTagHeaderValue.Any) || 
                    entityTag.Compare(etag, useStrongComparison))
                {
                    state = matchFoundState;
                    break;
                }
            }
            
            return state;
        }
        
        return PreconditionState.Unspecified;
    }
    
    // 从多个 precondition state 中找到最大值
    private static PreconditionState GetMaxPreconditionState(
        params PreconditionState[] states)
    {
        var max = PreconditionState.Unspecified;
        for (var i = 0; i < states.Length; i++)
        {
            if (states[i] > max)
            {
                max = states[i];
            }
        }
        
        return max;
    }
}

```

###### b - set last modified & etag header

```c#
public class FileResultExecutorBase
{
    private static void SetLastModifiedAndEtagHeaders(
        HttpResponse response, 
        DateTimeOffset? lastModified, 
        EntityTagHeaderValue? etag)
    {
        var httpResponseHeaders = response.GetTypedHeaders();
        if (lastModified.HasValue)
        {
            httpResponseHeaders.LastModified = lastModified;
        }
        if (etag != null)
        {
            httpResponseHeaders.ETag = etag;
        }
    }
}

```

###### c - set content | disposition

```c#
public class FileResultExecutorBase
{
    // 设置 content type
    private static void SetContentType(
        ActionContext context, 
        FileResult result)
    {
        var response = context.HttpContext.Response;
        response.ContentType = result.ContentType;
    }
    
    // 设置 content disposition（attachment）
    private static void SetContentDispositionHeader(        
        ActionContext context, 
        FileResult result)
    {
        if (!string.IsNullOrEmpty(result.FileDownloadName))
        {
            // From RFC 2183, Sec. 2.3:
            // The sender may want to suggest a filename to be used if the entity is
            // detached and stored in a separate file. If the receiving MUA writes
            // the entity to a file, the suggested filename should be used as a
            // basis for the actual filename, where possible.
            var contentDisposition = new ContentDispositionHeaderValue("attachment");
            contentDisposition.SetHttpFileName(result.FileDownloadName);
            context.HttpContext
                   .Response
                   .Headers[HeaderNames.ContentDisposition] = contentDisposition.ToString();
        }
    }
}

```

###### d - 设置 range header

```c#
public class FileResultExecutorBase
{
    // d1 - 设置 accept range header（bytes）
    private static void SetAcceptRangeHeader(HttpResponse response)
    {
        // 常量 AcceptRangeHeaderValue -> "bytes"
        response.Headers[HeaderNames.AcceptRanges] = AcceptRangeHeaderValue;
    }
    
    // d2 - 判断 range valid
    internal bool IfRangeValid(
        RequestHeaders httpRequestHeaders,
        DateTimeOffset? lastModified,
        EntityTagHeaderValue? etag)
    {
        // 14.27 If-Range
        var ifRange = httpRequestHeaders.IfRange;
        if (ifRange != null)
        {
            // If the validator given in the If-Range header field matches the
            // current validator for the selected representation of the target
            // resource, then the server SHOULD process the Range header field as
            // requested.  If the validator does not match, the server MUST ignore
            // the Range header field.
            if (ifRange.LastModified.HasValue)
            {
                if (lastModified.HasValue && 
                    lastModified > ifRange.LastModified)
                {
                    Logger.IfRangeLastModifiedPreconditionFailed(
                        lastModified, 
                        ifRange.LastModified);
                    return false;
                }
            }
            else if (etag != null && 
                     ifRange.EntityTag != null && 
                     !ifRange
                     .EntityTag
                     .Compare(etag, useStrongComparison: true))
            {
                Logger.IfRangeETagPreconditionFailed(
                    etag, 
                    ifRange.EntityTag);
                return false;
            }
        }
        
        return true;
    }
    
    // d3 - 设置 range header
    private (RangeItemHeaderValue? range, 
             long rangeLength, 
             bool serveBody) SetRangeHeaders(
        		ActionContext context,
        		RequestHeaders httpRequestHeaders,
        		long fileLength)
    {
        var response = context.HttpContext.Response;
        // 获取 header
        var httpResponseHeaders = response.GetTypedHeaders();
        // 获取 body
        var serveBody = !HttpMethods.IsHead(context.HttpContext.Request.Method);
                
        /* 判断 range request，计算 range */
        // Range may be null for empty range header, invalid ranges, parsing errors, 
        // multiple ranges and when the file length is zero.
        var (isRangeRequest, range) = 
            RangeHelper.ParseRange(
            	context.HttpContext,
            	httpRequestHeaders,
            	fileLength,
            	Logger);
        
        /* 如果没有 range request，返回 range = null */
        if (!isRangeRequest)
        {
            return (range: null, rangeLength: 0, serveBody);
        }
        
        /* 请求 range request，但是 range = null，返回 416 */
        // Requested range is not satisfiable
        if (range == null)
        {
            // 14.16 Content-Range - A server sending a response with status code 416 
            // (Requested range not satisfiable)
            // SHOULD include a Content-Range field with a byte-range-resp-spec of "*". 
            // The instance-length specifies the current length of the selected resource.  
            // e.g. */length
            response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
            httpResponseHeaders.ContentRange = new ContentRangeHeaderValue(fileLength);
            response.ContentLength = 0;
            
            return (range: null, rangeLength: 0, serveBody: false);
        }
        
        /* 请求 range request，且计算得 range 不为null，
           返回 206 和 range header */
        response.StatusCode = StatusCodes.Status206PartialContent;
        httpResponseHeaders.ContentRange = 
            new ContentRangeHeaderValue(
            	range.From!.Value,
            	range.To!.Value,
	            fileLength);
        
        // Overwrite the Content-Length header for valid range 
        // requests with the range length.
        var rangeLength = SetContentLength(response, range);
        
        return (range, rangeLength, serveBody);
    }
    
    private static long SetContentLength(
        HttpResponse response, 
        RangeItemHeaderValue range)
    {
        var start = range.From!.Value;
        var end = range.To!.Value;
        var length = end - start + 1;
        response.ContentLength = length;
        return length;
    }
}

```

##### 2.3.2 file content result

###### 2.3.2.1 file content result

```c#
public class FileContentResult : FileResult
{
    /* file content */
    private byte[] _fileContents;        
    public byte[] FileContents
    {
        get => _fileContents;
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            
            _fileContents = value;
        }
    }
    
    public FileContentResult(
        byte[] fileContents, 
        string contentType)            
        	: this(
                fileContents, 
                MediaTypeHeaderValue.Parse(contentType))
    {
    }
        
    public FileContentResult(
        byte[] fileContents, 
        MediaTypeHeaderValue contentType)            
        	: base(contentType?.ToString())
    {
        if (fileContents == null)
        {
            throw new ArgumentNullException(nameof(fileContents));
        }
        
        FileContents = fileContents;
    }
                    
    public override Task ExecuteResultAsync(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));            
        }
        
        // 从 action context 解析 file content result executor
        var executor = context
            .HttpContext
            .RequestServices
            .GetRequiredService<IActionResultExecutor<FileContentResult>>();
        // 用 file content result executor 执行 result
        return executor.ExecuteAsync(context, this);
    }
}

```

###### 2.3.2.2 file content result executor

```c#
public class FileContentResultExecutor : 
	FileResultExecutorBase, 
	IActionResultExecutor<FileContentResult>
{
    
    public FileContentResultExecutor(ILoggerFactory loggerFactory)            
        : base(CreateLogger<FileContentResultExecutor>(loggerFactory))
    {
    }
    
    /// <inheritdoc />
    public virtual Task ExecuteAsync(
        ActionContext context, 
        FileContentResult result)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }
        
        Logger.ExecutingFileResult(result);
        
        /* 1 - 设置 header */
        var (range, rangeLength, serveBody) = 
            SetHeadersAndLog(
            	context,
            	result,
            	result.FileContents.Length,
            	result.EnableRangeProcessing,
	            result.LastModified,	
            	result.EntityTag);
		
        /* 2 - 写入 body */
        if (!serveBody)
        {
            return Task.CompletedTask;
        }        
        return WriteFileAsync(context, result, range, rangeLength);
    }
            
    protected virtual Task WriteFileAsync(
        ActionContext context, 
        FileContentResult result, 
        RangeItemHeaderValue? range, 
        long rangeLength)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }
        
        if (range != null && rangeLength == 0)
        {
            return Task.CompletedTask;
        }
        
        if (range != null)
        {
            Logger.WritingRangeToBody();
        }
        
        var fileContentStream = new MemoryStream(result.FileContents);
        
        return WriteFileAsync(
            context.HttpContext, 
            fileContentStream, 
            range, 
            rangeLength);
    }
}

```

##### 2.3.3 file stream result

###### 2.3.3.1 file stream result

```c#
public class FileStreamResult : FileResult
{
    /* file stream */
    private Stream _fileStream;
    public Stream FileStream
    {
        get => _fileStream;
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            
            _fileStream = value;
        }
    }
    
    public FileStreamResult(
        Stream fileStream, 
        string contentType)
        	: this(
                fileStream, 
                MediaTypeHeaderValue.Parse(contentType))
    {
    }
        
    public FileStreamResult(
        Stream fileStream, 
        MediaTypeHeaderValue contentType)
        	: base(contentType?.ToString())
    {
        if (fileStream == null)
        {
            throw new ArgumentNullException(nameof(fileStream));
        }

        FileStream = fileStream;
    }
                
    /// <inheritdoc />
    public override Task ExecuteResultAsync(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        // 从 action context 中解析 filt stream result executor
        var executor = 
            context.HttpContext
            	   .RequestServices
            	   .GetRequiredService<IActionResultExecutor<FileStreamResult>>();
        // 使用 file stream executor 执行 execute        
        return executor.ExecuteAsync(context, this);
    }
}

```

###### 2.3.3.2 file stream result executor

```c#
public class FileStreamResultExecutor : 
	FileResultExecutorBase, 
	IActionResultExecutor<FileStreamResult>
{
    
    public FileStreamResultExecutor(ILoggerFactory loggerFactory)            
        : base(CreateLogger<FileStreamResultExecutor>(loggerFactory))
    {
    }
    
    /// <inheritdoc />
    public virtual async Task ExecuteAsync(
        ActionContext context, 
        FileStreamResult result)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }
        
        using (result.FileStream)
        {
            Logger.ExecutingFileResult(result);
            
            long? fileLength = null;
            if (result.FileStream.CanSeek)
            {
                fileLength = result.FileStream.Length;
            }
            
            /* 1 - 设置 header */
            var (range, rangeLength, serveBody) = 
                SetHeadersAndLog(
                	context,
                	result,
                	fileLength,
                	result.EnableRangeProcessing,
                	result.LastModified,
                	result.EntityTag);
            /* 2 - 写入 body */
            if (!serveBody)
            {
                return;
            }            
            await WriteFileAsync(context, result, range, rangeLength);
        }
    }
           
    protected virtual Task WriteFileAsync(
        ActionContext context,
        FileStreamResult result,
        RangeItemHeaderValue? range,
        long rangeLength)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }
        
        if (range != null && rangeLength == 0)
        {
            return Task.CompletedTask;
        }
        
        if (range != null)
        {
            Logger.WritingRangeToBody();
        }
        
        return WriteFileAsync(
            context.HttpContext, 
            result.FileStream, 
            range, rangeLength);
    }
}

```

##### 2.3.4 physical file result

###### 2.3.4.1 physical file result

```c#
public class PhysicalFileResult : FileResult
{
    /* file name */
    private string _fileName;
    public string FileName
    {
        get => _fileName;
        set => _fileName = value ?? throw new ArgumentNullException(nameof(value));
    }
        
    public PhysicalFileResult(
        string fileName, 
        string contentType)            
        	: this(
                fileName, 
                MediaTypeHeaderValue.Parse(contentType))
    {
        if (fileName == null)
        {
            throw new ArgumentNullException(nameof(fileName));
        }
    }
           
    public PhysicalFileResult(
        string fileName, 
        MediaTypeHeaderValue contentType)            
        	: base(contentType?.ToString())
    {
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
    }
                
    /// <inheritdoc />
    public override Task ExecuteResultAsync(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        // 从 action context 中解析 physical file result executor
        var executor = 
            context.HttpContext
	               .RequestServices
       		       .GetRequiredService<IActionResultExecutor<PhysicalFileResult>>();
        // 使用 physical file result executor 执行 execute
        return executor.ExecuteAsync(context, this);
    }
}

```

###### 2.3.4.2 physical file result executor

```c#
public class PhysicalFileResultExecutor : 
	FileResultExecutorBase, 
	IActionResultExecutor<PhysicalFileResult>
{
        
    public PhysicalFileResultExecutor(ILoggerFactory loggerFactory)            
        : base(CreateLogger<PhysicalFileResultExecutor>(loggerFactory))
    {
    }
    
    /// <inheritdoc />
    public virtual Task ExecuteAsync(
        ActionContext context, 
        PhysicalFileResult result)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }   
        
        // 从 result 解析 file info，
        // 如果 file 不存在，抛出异常
        var fileInfo = GetFileInfo(result.FileName);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(
                Resources.FormatFileResult_InvalidPath(result.FileName), 
                result.FileName);
        }
        
        Logger.ExecutingFileResult(result, result.FileName);
        
        /* 1 - 设置 header */
        // last modified 设置为 file 的 modified 时间
        var lastModified = result.LastModified ?? fileInfo.LastModified;
        var (range, rangeLength, serveBody) = 
            SetHeadersAndLog(
	            context,
    	        result,
        	    fileInfo.Length,
            	result.EnableRangeProcessing,
	            lastModified,
    	        result.EntityTag);
        /* 2 - 写入 body */
        if (serveBody)
        {
            return WriteFileAsync(context, result, range, rangeLength);
        }        
        return Task.CompletedTask;
    }
    
    /// <inheritdoc/>
    protected virtual Task WriteFileAsync(
        ActionContext context, 
        PhysicalFileResult result, 
        RangeItemHeaderValue range, 
        long rangeLength)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }
        
        if (range != null && rangeLength == 0)
        {
            return Task.CompletedTask;
        }
        
        var response = context.HttpContext.Response;
        if (!Path.IsPathRooted(result.FileName))
        {
            throw new NotSupportedException(
                Resources.FormatFileResult_PathNotRooted(result.FileName));
        }
        
        if (range != null)
        {
            Logger.WritingRangeToBody();
        }
        
        // 如果 range 不为 null，从 range 返回 range length 的 file 内容
        if (range != null)
        {
            return response.SendFileAsync(
                				result.FileName,
                				offset: range.From ?? 0L,
                				count: rangeLength);
        }
        // 否则，即 range 为 null，从头（offset=0）返回 file 内容
        return response.SendFileAsync(
            					result.FileName,
            					offset: 0,
            					count: null);
    }
    
    /* get file info */
                       
    protected virtual FileMetadata GetFileInfo(string path)
    {
        var fileInfo = new FileInfo(path);
        return new FileMetadata
        {
            Exists = fileInfo.Exists,
            Length = fileInfo.Length,
            LastModified = fileInfo.LastWriteTimeUtc,
        };
    }
            
    protected class FileMetadata
    {        
        public bool Exists { get; set; }                
        public long Length { get; set; }                
        public DateTimeOffset LastModified { get; set; }
    }
}

```

##### 2.3.5 virtual file result

###### 2.3.5.1 virtual file result

```c#
public class VirtualFileResult : FileResult
{
    /* file name */
    private string _fileName;
    public string FileName
    {
        get => _fileName;
        set => _fileName = value ?? throw new ArgumentNullException(nameof(value));
    }

    public IFileProvider FileProvider { get; set; }
    
    public VirtualFileResult(
        string fileName, 
        string contentType)            
        	: this(
                fileName, 
                MediaTypeHeaderValue.Parse(contentType))
    {
    }
            
    public VirtualFileResult(
        string fileName, 
        MediaTypeHeaderValue contentType)            
        	: base(contentType?.ToString())
    {
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
    }
                            
    /// <inheritdoc />
    public override Task ExecuteResultAsync(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        // 从 action context 解析 virtual file result executor
        var executor = 
            context.HttpContext
	               .RequestServices
       		       .GetRequiredService<IActionResultExecutor<VirtualFileResult>>();
        // 使用 virtual file result executor 执行 execute
        return executor.ExecuteAsync(context, this);
    }
}

```

###### 2.3.5.2 virtual file result executor

```c#
public class VirtualFileResultExecutor : 
	FileResultExecutorBase, 
	IActionResultExecutor<VirtualFileResult>
{
    private readonly IWebHostEnvironment _hostingEnvironment;
            
    public VirtualFileResultExecutor(
        ILoggerFactory loggerFactory, 
        IWebHostEnvironment hostingEnvironment)            
        	: base(CreateLogger<VirtualFileResultExecutor>(loggerFactory))
    {
        if (hostingEnvironment == null)
        {
            throw new ArgumentNullException(nameof(hostingEnvironment));
        }
        
        _hostingEnvironment = hostingEnvironment;
    }
    
    /// <inheritdoc />
    public virtual Task ExecuteAsync(
        ActionContext context, 
        VirtualFileResult result)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }
        // 获取 file info，
        // 如果 file 不存在，抛出异常
        var fileInfo = GetFileInformation(result);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(
                Resources.FormatFileResult_InvalidPath(result.FileName), 
                result.FileName);
        }
        
        Logger.ExecutingFileResult(result, result.FileName);
        
        /* 1 - 设置 last modified */
        // 使用 file 的 modified 时间作为 last modified
        var lastModified = result.LastModified ?? fileInfo.LastModified;
        var (range, rangeLength, serveBody) = 
            SetHeadersAndLog(
	            context,
    	        result,
        	    fileInfo.Length,
            	result.EnableRangeProcessing,
	            lastModified,
    	        result.EntityTag);
        /* 2 - 写入 body */
        if (serveBody)
        {
            return WriteFileAsync(context, result, fileInfo, range, rangeLength);
        }        
        return Task.CompletedTask;
    }
    
    /// <inheritdoc/>
    protected virtual Task WriteFileAsync(
        ActionContext context, 
        VirtualFileResult result, 
        IFileInfo fileInfo, 
        RangeItemHeaderValue? range, 
        long rangeLength)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }
        
        if (range != null && rangeLength == 0)
        {
            return Task.CompletedTask;
        }
        
        var response = context.HttpContext.Response;
        
        if (range != null)
        {
            Logger.WritingRangeToBody();
        }
        
        if (range != null)
        {
            return response.SendFileAsync(
                				fileInfo,
                				offset: range.From ?? 0L,
                				count: rangeLength);
        }
        
        return response.SendFileAsync(
            					fileInfo,
            					offset: 0,
            					count: null);
    }
    
    /* 获取 file info */
        
    private IFileInfo GetFileInformation(VirtualFileResult result)
    {
        var fileProvider = GetFileProvider(result);
        
        if (fileProvider is NullFileProvider)
        {
            throw new InvalidOperationException(
                Resources.VirtualFileResultExecutor_NoFileProviderConfigured);
        }
        
        var normalizedPath = result.FileName;
        if (normalizedPath.StartsWith("~", StringComparison.Ordinal))
        {
            normalizedPath = normalizedPath.Substring(1);
        }
        
        var fileInfo = fileProvider.GetFileInfo(normalizedPath);
        return fileInfo;
    }
    
    // 获取 file provider
    private IFileProvider GetFileProvider(VirtualFileResult result)
    {
        if (result.FileProvider != null)
        {
            return result.FileProvider;
        }
        
        // 从 hosting environment 获取 web root file provider
        result.FileProvider = _hostingEnvironment.WebRootFileProvider;
        return result.FileProvider;
    }            
}

```

#### 2.4 redirect result

##### 2.4.1 url helper

###### 2.4.1.1 url helper 接口

```c#
public interface IUrlHelper
{    
    ActionContext ActionContext { get; }
    
    // 判断是否为 local url，
    //   1 - absolute path，但是不包含 host / authority    
    //   2 - virtual path（~/开头）         		
    //
    // local url
    //  /Views/Default/Index.html
    // 	~/Index.html
    // 非 local url
    //  ../Index.html
    //  http://www.contoso.com/
    // 	http://localhost/Index.html           
    bool IsLocalUrl([NotNullWhen(true)] string? url);
    
    // 由 url action context 生成 absolute path        
    string? Action(UrlActionContext actionContext);

    // 由 content path 生成 absolute path，
    // 将 virtual path（~/开头）转换为 absolute path    
    [return: NotNullIfNotNull("contentPath")]
    string? Content(string? contentPath);
            
    // 由 url route context 生成 absolute path   
    string? RouteUrl(UrlRouteContext routeContext);

    // 由 route name 生成 absolute path    
    string? Link(string? routeName, object? values);
}

```

###### 2.4.1.2 url helper 扩展

```c#
public static class UrlHelperExtensions
{        
    public static string Action(this IUrlHelper helper)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.Action(
            action: null,
            controller: null,
            values: null,
            protocol: null,
            host: null,
            fragment: null);
    }
            
    public static string Action(
        this IUrlHelper helper, 
        string action)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.Action(
            action, 
            controller: null, 
            values: null, 
            protocol: null, 
            host: null, 
            fragment: null);
    }
        
    public static string Action(
        this IUrlHelper helper, 
        string action, 
        object values)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.Action(
            action, 
            controller: null, 
            values: values, 
            protocol: null, 
            host: null, 
            fragment: null);
    }
        
    public static string Action(
        this IUrlHelper helper, 
        string action, 
        string controller)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.Action(
            action, 
            controller, 
            values: null, 
            protocol: null, 
            host: null, 
            fragment: null);
    }
       
    public static string Action(
        this IUrlHelper helper, 
        string action, 
        string controller, 
        object values)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.Action(
            action, 
            controller, 
            values, 
            protocol: null, 
            host: null, 
            fragment: null);
    }

            
    public static string Action(
        this IUrlHelper helper,
        string action,
        string controller,
        object values,
        string protocol)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.Action(
            action, 
            controller, 
            values, 
            protocol, 
            host: null, 
            fragment: null);
    }
        
    public static string Action(
        this IUrlHelper helper,
        string action,
        string controller,
        object values,
        string protocol,
        string host)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.Action(
            action, 
            controller, 
            values, 
            protocol, 
            host, 
            fragment: null);
    }
                
    public static string Action(
        this IUrlHelper helper,
        string action,
        string controller,
        object values,
        string protocol,
        string host,
        string fragment)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.Action(
            new UrlActionContext()
            {
                Action = action,
                Controller = controller,
                Host = host,                
                Values = values,
                Protocol = protocol,
                Fragment = fragment
            });
    }
    
    /* 扩展 route url */
    
    public static string RouteUrl(
        this IUrlHelper helper, 
        object values)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.RouteUrl(
            routeName: null, 
            values: values, 
            protocol: null, 
            host: null, 
            fragment: null);
    }
            
    public static string RouteUrl(
        this IUrlHelper helper, 
        string routeName)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.RouteUrl(
            routeName, 
            values: null, 
            protocol: null, 
            host: null, 
            fragment: null);
    }
                
    public static string RouteUrl(
        this IUrlHelper helper, 
        string routeName, 
        object values)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.RouteUrl(
            routeName, 
            values, 
            protocol: null, 
            host: null, 
            fragment: null);
    }
                            
    public static string RouteUrl(
        this IUrlHelper helper,
        string routeName,
        object values,
        string protocol)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.RouteUrl(
            routeName, 
            values, 
            protocol, 
            host: null, 
            fragment: null);
    }
            
    public static string RouteUrl(
        this IUrlHelper helper,
        string routeName,
        object values,
        string protocol,
        string host)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.RouteUrl(
            routeName, 
            values, 
            protocol, 
            host, 
            fragment: null);
    }
        
    public static string RouteUrl(
        this IUrlHelper helper,
        string routeName,
        object values,
        string protocol,
        string host,
        string fragment)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        return helper.RouteUrl(
            new UrlRouteContext()
            {
                RouteName = routeName,
                Values = values,
                Protocol = protocol,
                Host = host,
                Fragment = fragment
            });
    }
        
    /* 扩展 page */
    
    public static string Page(
        this IUrlHelper urlHelper, 
        string pageName) => 
        	Page(
        		urlHelper, 
        		pageName, 
        		values: null);
        
    public static string Page(
        this IUrlHelper urlHelper, 
        string pageName, 
        string pageHandler) => 
        	Page(
        		urlHelper, 
        		pageName, 
        		pageHandler, 
        		values: null);
       
    public static string Page(
        this IUrlHelper urlHelper, 
        string pageName, 
        object values) => 
        	Page(
        		urlHelper, 
        		pageName, 
        		pageHandler: null, 
        		values: values);
        
    public static string Page(
        this IUrlHelper urlHelper,
        string pageName,
        string pageHandler,
        object values) => 
        	Page(
        		urlHelper, 
        		pageName, 
        		pageHandler, 
        		values, 
        		protocol: null);
        
    public static string Page(
        this IUrlHelper urlHelper,            
        string pageName,
        string pageHandler,
        object values,
        string protocol) => 
        	Page(
        		urlHelper, 
        		pageName, 
        		pageHandler, 
        		values, 
        		protocol, 
        		host: null, 
        		fragment: null);
        
    public static string Page(
        this IUrlHelper urlHelper,
        string pageName,
        string pageHandler,
        object values,
        string protocol,
        string host) => 
        	Page(
        		urlHelper, 
        		pageName, 
        		pageHandler, 
        		values, 
        		protocol, 
        		host, 
        		fragment: null);
        
    public static string Page(
        this IUrlHelper urlHelper,
        string pageName,
        string pageHandler,
        object values,
        string protocol,
        string host,
        string fragment)
    {
        if (urlHelper == null)
        {
            throw new ArgumentNullException(nameof(urlHelper));
        }
        
        var routeValues = new RouteValueDictionary(values);
        var ambientValues = urlHelper.ActionContext.RouteData.Values;
        
        UrlHelperBase.NormalizeRouteValuesForPage(
            			urlHelper.ActionContext, 
            			pageName, 
            			pageHandler, 
            			routeValues, 
            			ambientValues);

        return urlHelper.RouteUrl(
            				routeName: null,
            				values: routeValues,
            				protocol: protocol,
            				host: host,
            				fragment: fragment);
    }
        
    /* 扩展 action link */
    
    public static string ActionLink(
        this IUrlHelper helper,
        string action = null,
        string controller = null,
        object values = null,
        string protocol = null,
        string host = null,
        string fragment = null)
    {
        if (helper == null)
        {
            throw new ArgumentNullException(nameof(helper));
        }
        
        var httpContext = helper.ActionContext.HttpContext;
        
        if (protocol == null)
        {
            protocol = httpContext.Request.Scheme;
        }
        
        if (host == null)
        {
            host = httpContext.Request.Host.ToUriComponent();
        }
        
        return Action(
            helper, action, controller, values, protocol, host, fragment);
    }
        
    /* 扩展 page link */
    
    public static string PageLink(
        this IUrlHelper urlHelper,
        string pageName = null,
        string pageHandler = null,
        object values = null,
        string protocol = null,
        string host = null,
        string fragment = null)
    {
        if (urlHelper == null)
        {
            throw new ArgumentNullException(nameof(urlHelper));
        }
        
        var httpContext = urlHelper.ActionContext.HttpContext;
        
        if (protocol == null)
        {
            protocol = httpContext.Request.Scheme;
        }
        
        if (host == null)
        {
            host = httpContext.Request.Host.ToUriComponent();
        }
        
        return Page(
            urlHelper, pageName, pageHandler, values, protocol, host, fragment);
    }
}

```

###### 2.4.1.3 url helper base

```c#
public abstract class UrlHelperBase : IUrlHelper
{
    // Perf: Share the StringBuilder object across multiple calls of GenerateURL 
    // for this UrlHelper
    private StringBuilder? _stringBuilder;
    
    // Perf: Reuse the RouteValueDictionary across multiple calls of Action 
    // for this UrlHelper
    private readonly RouteValueDictionary _routeValueDictionary;
        
    protected RouteValueDictionary AmbientValues { get; }       
    public ActionContext ActionContext { get; }
            
    protected UrlHelperBase(ActionContext actionContext)
    {
        if (actionContext == null)
        {
            throw new ArgumentNullException(nameof(actionContext));
        }
        
        ActionContext = actionContext;
        AmbientValues = actionContext.RouteData.Values;
        _routeValueDictionary = new RouteValueDictionary();
    }
            
    /// <inheritdoc />
    public virtual bool IsLocalUrl([NotNullWhen(true)] string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }        
        // Allows "/" or "/foo" but not "//" or "/\".
        if (url[0] == '/')
        {
            // url is exactly "/"
            if (url.Length == 1)
            {
                return true;
            }            
            // url doesn't start with "//" or "/\"
            if (url[1] != '/' && url[1] != '\\')
            {
                return !HasControlCharacter(url.AsSpan(1));
            }
            
            return false;
        }        
        // Allows "~/" or "~/foo" but not "~//" or "~/\".
        if (url[0] == '~' && url.Length > 1 && url[1] == '/')
        {
            // url is exactly "~/"
            if (url.Length == 2)
            {
                return true;
            }            
            // url doesn't start with "~//" or "~/\"
            if (url[2] != '/' && url[2] != '\\')
            {
                return !HasControlCharacter(url.AsSpan(2));
            }
            
            return false;
        }
        
        return false;
        
        static bool HasControlCharacter(ReadOnlySpan<char> readOnlySpan)
        {
            // URLs may not contain ASCII control characters.
            for (var i = 0; i < readOnlySpan.Length; i++)
            {
                if (char.IsControl(readOnlySpan[i]))
                {
                    return true;
                }
            }
            
            return false;
        }
    }
    
    /// <inheritdoc />
    [return: NotNullIfNotNull("contentPath")]
    public virtual string? Content(string? contentPath)
    {
        if (string.IsNullOrEmpty(contentPath))
        {
            return null;
        }
        else if (contentPath[0] == '~')
        {
            var segment = new PathString(contentPath.Substring(1));
            var applicationPath = ActionContext.HttpContext.Request.PathBase;
            
            var path = applicationPath.Add(segment);
            Debug.Assert(path.HasValue);
            return path.Value;
        }
        
        return contentPath;
    }
    
    /// <inheritdoc />
    public virtual string? Link(string? routeName, object? values)
    {
        return RouteUrl(
            new UrlRouteContext()
            {
                RouteName = routeName,
                Values = values,
                Protocol = ActionContext.HttpContext.Request.Scheme,
                Host = ActionContext.HttpContext.Request.Host.ToUriComponent()
            });
    }

    /// <inheritdoc />
    public abstract string? Action(UrlActionContext actionContext);
    
    /// <inheritdoc />
    public abstract string? RouteUrl(UrlRouteContext routeContext);

        /// <summary>
        /// Gets a <see cref="RouteValueDictionary"/> using the specified values.
        /// </summary>
        /// <param name="values">The values to use.</param>
        /// <returns>A <see cref="RouteValueDictionary"/> with the specified values.</returns>
    protected RouteValueDictionary GetValuesDictionary(object? values)
    {
        // Perf: RouteValueDictionary can be cast to IDictionary<string, object>, 
        // but it is special cased to avoid allocating boxed Enumerator.
        if (values is RouteValueDictionary routeValuesDictionary)
        {
            _routeValueDictionary.Clear();
            foreach (var kvp in routeValuesDictionary)
            {
                _routeValueDictionary.Add(kvp.Key, kvp.Value);
            }
            
            return _routeValueDictionary;
        }
        
        if (values is IDictionary<string, object> dictionaryValues)
        {
            _routeValueDictionary.Clear();
            foreach (var kvp in dictionaryValues)
            {
                _routeValueDictionary.Add(kvp.Key, kvp.Value);
            }
            
            return _routeValueDictionary;
        }
        
        return new RouteValueDictionary(values);
    }
            
    protected string? GenerateUrl(
        string? protocol, 
        string? host, 
        string? virtualPath, 
        string? fragment)
    {
        if (virtualPath == null)
        {
            return null;
        }
        
        // Perf: In most of the common cases, GenerateUrl is called with a null protocol, 
        // host and fragment.
        // In such cases, we might not need to build any URL as the url generated 
        // is mostly same as the virtual path available in pathData.
        // For such common cases, this FastGenerateUrl method saves a string allocation 
        // per GenerateUrl call.
        if (TryFastGenerateUrl(protocol, host, virtualPath, fragment, out var url))
        {
            return url;
        }
        
        var builder = GetStringBuilder();
        try
        {
            var pathBase = ActionContext.HttpContext.Request.PathBase;
            
            if (string.IsNullOrEmpty(protocol) && string.IsNullOrEmpty(host))
            {
                AppendPathAndFragment(builder, pathBase, virtualPath, fragment);
                // We're returning a partial URL (just path + query + fragment), 
                // but we still want it to be rooted.
                if (builder.Length == 0 || builder[0] != '/')
                {
                    builder.Insert(0, '/');
                }
            }
            else
            {
                protocol = string.IsNullOrEmpty(protocol) ? "http" : protocol;
                builder.Append(protocol);
                
                builder.Append(Uri.SchemeDelimiter);
                
                host = string.IsNullOrEmpty(host) 
                    ? ctionContext.HttpContext.Request.Host.Value 
                    : host;
                builder.Append(host);
                AppendPathAndFragment(builder, pathBase, virtualPath, fragment);
            }
            
            var path = builder.ToString();
            return path;
        }
        finally
        {
            // Clear the StringBuilder so that it can reused for the next call.
            builder.Clear();
        }
    }
        
    protected string? GenerateUrl(string? protocol, string? host, string? path)
    {
        // This method is similar to GenerateUrl, but it's used for EndpointRouting. 
        // It ignores pathbase and fragment because those have already been incorporated.
        if (path == null)
        {
            return null;
        }
        
        // Perf: In most of the common cases, GenerateUrl is called with a null protocol, 
        // host and fragment.
        // In such cases, we might not need to build any URL as the url generated 
        // is mostly same as the virtual path available in pathData.
        // For such common cases, this FastGenerateUrl method saves a string allocation 
        // per GenerateUrl call.
        if (TryFastGenerateUrl(protocol, host, path, fragment: null, out var url))
        {
            return url;
        }
        
        var builder = GetStringBuilder();
        try
        {
            if (string.IsNullOrEmpty(protocol) && string.IsNullOrEmpty(host))
            {
                AppendPathAndFragment(builder, pathBase: null, path, fragment: null);
                
                // We're returning a partial URL (just path + query + fragment), 
                // but we still want it to be rooted.
                if (builder.Length == 0 || builder[0] != '/')
                {
                    builder.Insert(0, '/');
                }
            }
            else
            {
                protocol = string.IsNullOrEmpty(protocol) ? "http" : protocol;
                builder.Append(protocol);
                
                builder.Append(Uri.SchemeDelimiter);
                
                host = string.IsNullOrEmpty(host) 
                    ? ActionContext.HttpContext.Request.Host.Value 
                    : host;
                builder.Append(host);
                AppendPathAndFragment(builder, pathBase: null, path, fragment: null);
            }
            
            return builder.ToString();
        }
        finally
        {
            // Clear the StringBuilder so that it can reused for the next call.
            builder.Clear();
        }
    }
    
    internal static void NormalizeRouteValuesForAction(
        string? action,
        string? controller,
        RouteValueDictionary values,
        RouteValueDictionary? ambientValues)
    {
        object? obj = null;
        if (action == null)
        {
            if (!values.ContainsKey("action") &&
                (ambientValues?.TryGetValue("action", out obj) ?? false))
            {
                values["action"] = obj;
            }
        }
        else
        {
            values["action"] = action;
        }
        
        if (controller == null)
        {
            if (!values.ContainsKey("controller") &&
                (ambientValues?.TryGetValue("controller", out obj) ?? false))
            {
                values["controller"] = obj;
            }
        }
        else
        {
            values["controller"] = controller;
        }
    }
    
    internal static void NormalizeRouteValuesForPage(
        ActionContext? context,
        string? page,
        string? handler,
        RouteValueDictionary values,
        RouteValueDictionary? ambientValues)
    {
        object? value = null;
        if (string.IsNullOrEmpty(page))
        {
            if (!values.ContainsKey("page") &&
                (ambientValues?.TryGetValue("page", out value) ?? false))
            {
                values["page"] = value;
            }
        }
        else
        {
            values["page"] = CalculatePageName(context, ambientValues, page);
        }
        
        if (string.IsNullOrEmpty(handler))
        {
            if (!values.ContainsKey("handler") &&
                (ambientValues?.ContainsKey("handler") ?? false))
            {
                // Clear out form action unless it's explicitly specified in the routeValues.
                values["handler"] = null;
            }
        }
        else
        {
            values["handler"] = handler;
        }
    }
    
    private static object CalculatePageName(
        ActionContext? context, 
        RouteValueDictionary? ambientValues, 
        string pageName)
    {
        Debug.Assert(pageName.Length > 0);
        // Paths not qualified with a leading slash are treated as relative 
        // to the current page.
        if (pageName[0] != '/')
        {
            // OK now we should get the best 'normalized' version of the page route value 
            // that we can.
            string? currentPagePath;
            if (context != null)
            {
                currentPagePath = 
                    NormalizedRouteValue.GetNormalizedRouteValue(context, "page");
            }
            else if (ambientValues != null)
            {
                currentPagePath = Convert.ToString(
                    				  ambientValues["page"], 
                    				  CultureInfo.InvariantCulture);
            }
            else
            {
                currentPagePath = null;
            }
            
            if (string.IsNullOrEmpty(currentPagePath))
            {
                // Disallow the use sibling page routing, a Razor page specific feature, 
                // from a non-page action.
                // OR - this is a call from LinkGenerator 
                // where the HttpContext was not specified.
                //
                // We can't use a relative path in either case, 
                // because we don't know the base path.
                throw new InvalidOperationException(
                    Resources.FormatUrlHelper_RelativePagePathIsNotSupported(
                        		pageName,
                        		nameof(LinkGenerator),
                        		nameof(HttpContext)));
            }
            
            return ViewEnginePath.CombinePath(currentPagePath, pageName);
        }
        
        return pageName;
    }
    
    // for unit testing
    internal static void AppendPathAndFragment(
        StringBuilder builder, 
        PathString pathBase, 
        string virtualPath, 
        string? fragment)
    {
        if (!pathBase.HasValue)
        {
            if (virtualPath.Length == 0)
            {
                builder.Append('/');
            }
            else
            {
                if (!virtualPath.StartsWith('/'))
                {
                    builder.Append('/');
                }
                
                builder.Append(virtualPath);
            }
        }
        else
        {
            if (virtualPath.Length == 0)
            {
                builder.Append(pathBase.Value);
            }
            else
            {
                builder.Append(pathBase.Value);
                
                if (pathBase.Value.EndsWith("/", StringComparison.Ordinal))
                {
                    builder.Length--;
                }
                
                if (!virtualPath.StartsWith("/", StringComparison.Ordinal))
                {
                    builder.Append('/');
                }
                
                builder.Append(virtualPath);
            }
        }
        
        if (!string.IsNullOrEmpty(fragment))
        {
            builder.Append('#').Append(fragment);
        }
    }
    
    private bool TryFastGenerateUrl(
        string? protocol,
        string? host,
        string virtualPath,
        string? fragment,
        [NotNullWhen(true)] out string? url)
    {
        var pathBase = ActionContext.HttpContext.Request.PathBase;
        url = null;
        
        if (string.IsNullOrEmpty(protocol)
            && string.IsNullOrEmpty(host)
            && string.IsNullOrEmpty(fragment)
            && !pathBase.HasValue)
        {
            if (virtualPath.Length == 0)
            {
                url = "/";
                return true;
            }
            else if (virtualPath.StartsWith("/", StringComparison.Ordinal))
            {
                url = virtualPath;
                return true;
            }
        }
        
        return false;
    }
    
    private StringBuilder GetStringBuilder()
    {
        if (_stringBuilder == null)
        {
            _stringBuilder = new StringBuilder();
        }
        
        return _stringBuilder;
    }
}

```

###### 2.4.1.4 url helper

```c#
public class UrlHelper : UrlHelperBase
{       
    protected HttpContext HttpContext => ActionContext.HttpContext;
            
    protected IRouter Router
    {
        get
        {
            var routers = ActionContext.RouteData.Routers;
            if (routers.Count == 0)
            {
                throw new InvalidOperationException(
                    "Could not find an IRouter associated with the ActionContext. " + 
                    "If your application is using endpoint routing then you can 
                    "get a IUrlHelperFactory with " + 
                    "dependency injection and use it to create a UrlHelper, 
                    "or use Microsoft.AspNetCore.Routing.LinkGenerator.");
            }
            
            return routers[0];
        }
    }
    
    public UrlHelper(ActionContext actionContext) : base(actionContext)
    {
    }
                   
    /// <inheritdoc />
    public override string? Action(UrlActionContext actionContext)
    {
        if (actionContext == null)
        {
            throw new ArgumentNullException(nameof(actionContext));
        }
        
        var valuesDictionary = GetValuesDictionary(actionContext.Values);
        
        NormalizeRouteValuesForAction(
            actionContext.Action, 
            actionContext.Controller, 
            valuesDictionary, 
            AmbientValues);

        var virtualPathData = 
            GetVirtualPathData(routeName: null, values: valuesDictionary);
        
        return GenerateUrl(
            actionContext.Protocol, 
            actionContext.Host, 
            virtualPathData, 
            actionContext.Fragment);
    }
    
    /// <inheritdoc />
    public override string? RouteUrl(UrlRouteContext routeContext)
    {
        if (routeContext == null)
        {
            throw new ArgumentNullException(nameof(routeContext));
        }
        
        var valuesDictionary = 
            routeContext.Values as RouteValueDictionary 
            	?? GetValuesDictionary(routeContext.Values);
        var virtualPathData = 
            GetVirtualPathData(routeContext.RouteName, valuesDictionary);
        
        return GenerateUrl(
            routeContext.Protocol, 
            routeContext.Host, 
            virtualPathData, 
            routeContext.Fragment);
    }

        
    protected virtual VirtualPathData? GetVirtualPathData(
        string? routeName, 
        RouteValueDictionary values)
    {
        var context = 
            new VirtualPathContext(
            		HttpContext, 
            		AmbientValues, 
            		values, 
            		routeName);
        
        return Router.GetVirtualPath(context);
    }
           
    protected virtual string? GenerateUrl(
        string? protocol, 
        string? host, 
        VirtualPathData? pathData, 
        string? fragment)
    {
        return GenerateUrl(
            protocol, 
            host, 
            pathData?.VirtualPath, 
            fragment);
    }
}

```

###### 2.4.1.5 url helper factory 接口

```c#
public interface IUrlHelperFactory
{    
    IUrlHelper GetUrlHelper(ActionContext context);
}

```

###### 2.4.1.5 url helper factory

```c#
public class UrlHelperFactory : IUrlHelperFactory
{
    /// <inheritdoc />
    public IUrlHelper GetUrlHelper(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        var httpContext = context.HttpContext;
        
        if (httpContext == null)
        {
            throw new ArgumentException(
                Resources.FormatPropertyOfTypeCannotBeNull(
                	nameof(ActionContext.HttpContext),
	                nameof(ActionContext)));
        }

        if (httpContext.Items == null)
        {
            throw new ArgumentException(
                Resources.FormatPropertyOfTypeCannotBeNull(
                    nameof(HttpContext.Items),
                    nameof(HttpContext)));
        }
        
        // Perf: Create only one UrlHelper per context
        if (httpContext.Items
            		   .TryGetValue(
                           typeof(IUrlHelper), 
                           out var value) && 
            value is IUrlHelper urlHelper)
        {
            return urlHelper;
        }
        
        var endpointFeature = httpContext.Features.Get<IEndpointFeature>();
        if (endpointFeature?.Endpoint != null)
        {
            var services = httpContext.RequestServices;
            var linkGenerator = services.GetRequiredService<LinkGenerator>();
            var logger = services.GetRequiredService<ILogger<EndpointRoutingUrlHelper>>();
            
            urlHelper = 
                new EndpointRoutingUrlHelper(
                	context,
                	linkGenerator,
                	logger);
        }
        else
        {
            urlHelper = new UrlHelper(context);
        }
        
        httpContext.Items[typeof(IUrlHelper)] = urlHelper;
        
        return urlHelper;
    }
}

```

##### 2.4.1 local redirect

###### 2.4.1.1 local redirect result

```c#
public class LocalRedirectResult : ActionResult
{    
    private string _localUrl;
     public string Url
    {
        get => _localUrl;
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(
                    Resources.ArgumentCannotBeNullOrEmpty, 
                    nameof(value));
            }
            
            _localUrl = value;
        }
    }
    
    public bool Permanent { get; set; }        
    public bool PreserveMethod { get; set; }	// for http post            
    public IUrlHelper UrlHelper { get; set; }
    
    public LocalRedirectResult(string localUrl)
        : this(
            localUrl, 
            permanent: false)
    {
    }

    public LocalRedirectResult(
        string localUrl, 
        bool permanent)
        	: this(
	            localUrl, 
    	        permanent, 
        	    preserveMethod: false)
    {
    }
        
    public LocalRedirectResult(
        string localUrl, 
        bool permanent, 
        bool preserveMethod)
    {
        if (string.IsNullOrEmpty(localUrl))
        {
            throw new ArgumentException(
                Resources.ArgumentCannotBeNullOrEmpty, 
                nameof(localUrl));
        }
        
        Permanent = permanent;
        PreserveMethod = preserveMethod;
        Url = localUrl;
    }
                
    /// <inheritdoc />
    public override Task ExecuteResultAsync(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        // 从 action context 解析 local redirect result executor
        var executor = 
            context.HttpContext
            	   .RequestServices
            	   .GetRequiredService<IActionResultExecutor<LocalRedirectResult>>();
        // 使用 local redirect result executor 执行 execute
        return executor.ExecuteAsync(context, this);
    }
}

```

###### 2.4.1.2 local redirect result executor

```c#
public class LocalRedirectResultExecutor : IActionResultExecutor<LocalRedirectResult>
{
    private readonly ILogger _logger;       
    private readonly IUrlHelperFactory _urlHelperFactory;
        
    public LocalRedirectResultExecutor(
        ILoggerFactory loggerFactory, 
        IUrlHelperFactory urlHelperFactory)
    {
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }        
        if (urlHelperFactory == null)
        {
            throw new ArgumentNullException(nameof(urlHelperFactory));
        }
        
        _logger = loggerFactory.CreateLogger<LocalRedirectResultExecutor>();
        _urlHelperFactory = urlHelperFactory;
    }
    
    /// <inheritdoc />
    public virtual Task ExecuteAsync(
        ActionContext context, 
        LocalRedirectResult result)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }
        
        var urlHelper = result.UrlHelper ?? _urlHelperFactory.GetUrlHelper(context);
        
        // IsLocalUrl is called to handle Urls starting with '~/'.
        if (!urlHelper.IsLocalUrl(result.Url))
        {
            throw new InvalidOperationException(Resources.UrlNotLocal);
        }
        
        // 解析 destination result
        var destinationUrl = urlHelper.Content(result.Url);
        _logger.LocalRedirectResultExecuting(destinationUrl);
        
        /* 如果 preserve method = true，*/
        if (result.PreserveMethod)
        {
            // 返回 307(temporary)、308(permanent)
            context.HttpContext
                   .Response
                   .StatusCode = result.Permanent 
                					? StatusCodes.Status308PermanentRedirect 
                					: StatusCodes.Status307TemporaryRedirect;
            // 重写 http response header 的 location
            context.HttpContext
                   .Response
                   .Headers[HeaderNames.Location] = destinationUrl;
        }
        /* 否则，即preserve method = false，*/
        else
        {
            // 调用 http response 的 redirect 方法
            context.HttpContext
                   .Response
                   .Redirect(destinationUrl, result.Permanent);
        }
        
        return Task.CompletedTask;
    }
}

```

##### 2.4.2 redirect result（基类）

###### 2.4.2.1 keep temp data result 接口

```c#
public interface IKeepTempDataResult : IActionResult
{
}

```

###### 2.4.2.2 redirect result

```c#
public class RedirectResult : 
	ActionResult, 
	IKeepTempDataResult
{
    private string _url;
    public string Url
    {
        get => _url;
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(
                    Resources.ArgumentCannotBeNullOrEmpty, 
                    nameof(value));
            }
            
            _url = value;
        }
    }
    
    public bool Permanent { get; set; }            
    public bool PreserveMethod { get; set; }	// for http post            
    public IUrlHelper UrlHelper { get; set; }
    
    public RedirectResult(string url) 
        : this(
            url, 
            permanent: false)
    {
        if (url == null)
        {
            throw new ArgumentNullException(nameof(url));
        }
    }
        
    public RedirectResult(
        string url, 
        bool permanent)            
        	: this(
                url, 
                permanent, 
                preserveMethod: false)
    {
    }
       
    public RedirectResult(
        string url, 
        bool permanent, 
        bool preserveMethod)
    {
        if (url == null)
        {
            throw new ArgumentNullException(nameof(url));
        }        
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentException(
                Resources.ArgumentCannotBeNullOrEmpty, 
                nameof(url));
        }
        
        Permanent = permanent;
        PreserveMethod = preserveMethod;
        Url = url;
    }
              
    /// <inheritdoc />
    public override Task ExecuteResultAsync(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        // 解析 content result executor
        var executor = 
            context.HttpContext
            	   .RequestServices
            	   .GetRequiredService<IActionResultExecutor<RedirectResult>>();
        // 使用 content result executor 的 execute 方法
        return executor.ExecuteAsync(context, this);
    }
}

```

###### 2.4.2.3 redirect result executor

```c#
public class RedirectResultExecutor : IActionResultExecutor<RedirectResult>
{
    private readonly ILogger _logger;
    private readonly IUrlHelperFactory _urlHelperFactory;
        
    public RedirectResultExecutor(
        ILoggerFactory loggerFactory, 
        IUrlHelperFactory urlHelperFactory)
    {
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }        
        if (urlHelperFactory == null)
        {
            throw new ArgumentNullException(nameof(urlHelperFactory));
        }
        
        _logger = loggerFactory.CreateLogger<RedirectResultExecutor>();
        _urlHelperFactory = urlHelperFactory;
    }
    
    /// <inheritdoc />
    public virtual Task ExecuteAsync(
        ActionContext context, 
        RedirectResult result)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }
        
        // 从 redirect result 中解析或者创建 url helper
        var urlHelper = result.UrlHelper ?? _urlHelperFactory.GetUrlHelper(context);
        
        /* 解析 destinational url，
           如果是本地 url，处理 ~/ 符号 */
        // IsLocalUrl is called to handle URLs starting with '~/'.
        var destinationUrl = result.Url;
        if (urlHelper.IsLocalUrl(destinationUrl))
        {
            destinationUrl = urlHelper.Content(result.Url);
        }
        
        _logger.RedirectResultExecuting(destinationUrl);
        
        /* 如果标记了 preserve method，
           返回 307(temporary) 或 308(permanent) */        
        if (result.PreserveMethod)
        {
            context.HttpContext.Response.StatusCode = 
                result.Permanent 
                	? StatusCodes.Status308PermanentRedirect 
                	: StatusCodes.Status307TemporaryRedirect;
            // 重写 response header 的 location
            context.HttpContext
                   .Response
                   .Headers[HeaderNames.Location] = destinationUrl;
        }
        /* 否则，即 preserve method = false，
           调用 response 的 redirect 方法 */        
        else
        {
            context.HttpContext
                   .Response
                   .Redirect(destinationUrl, result.Permanent);
        }
        
        return Task.CompletedTask;
    }
}

```

##### 2.4.3 redirect to action result

###### 2.4.3.1 redirect to action result

```c#
public class RedirectToActionResult : 	
	ActionResult, 	
	IKeepTempDataResult
{        
    public IUrlHelper UrlHelper { get; set; }
        
    public string ActionName { get; set; }        
    public string ControllerName { get; set; }      
    public RouteValueDictionary RouteValues { get; set; }       
    public bool Permanent { get; set; }        
    public bool PreserveMethod { get; set; }        
    public string Fragment { get; set; }
        
        
    public RedirectToActionResult(
        string actionName,
        string controllerName,
        object routeValues)
        	: this(
                actionName, 
                controllerName, 
                routeValues, 
                permanent: false)
    {
    }
        
    public RedirectToActionResult(
        string actionName,
        string controllerName,
        object routeValues,
        string fragment)
        	: this(
                actionName, 
                controllerName, 
                routeValues, 
                permanent: false, 
                fragment: fragment)
    {
    }
       
    public RedirectToActionResult(
        string actionName,
        string controllerName,
        object routeValues,
        bool permanent)
        	: this(
                actionName, 
                controllerName, 
                routeValues, 
                permanent, 
                fragment: null)
    {
    }
        
    public RedirectToActionResult(
        string actionName,
        string controllerName,
        object routeValues,
        bool permanent,
        bool preserveMethod)
        	: this(
                actionName, 
                controllerName, 
                routeValues, 
                permanent, 
                preserveMethod, 
                fragment: null)
    {
    }
        
    public RedirectToActionResult(
        string actionName,
        string controllerName,
        object routeValues,
        bool permanent,
        string fragment)
        	: this(
                actionName, 
                controllerName, 
                routeValues, 
                permanent, 
                preserveMethod: false, 
                fragment: fragment)
    {
    }
       
    public RedirectToActionResult(
        string actionName,
        string controllerName,
        object routeValues,
        bool permanent,
        bool preserveMethod,
        string fragment)
    {
        ActionName = actionName;
        ControllerName = controllerName;
        RouteValues = routeValues == null ? null : new RouteValueDictionary(routeValues);
        Permanent = permanent;
        PreserveMethod = preserveMethod;
        Fragment = fragment;
    }
           
    /// <inheritdoc />
    public override Task ExecuteResultAsync(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        // 从 action context 中解析 redirect to action result executor
        var executor = 
            context.HttpContext
      	           .RequestServices
        	       .GetRequiredService<IActionResultExecutor<RedirectToActionResult>>();
        // 使用 executor 执行 execute
        return executor.ExecuteAsync(context, this);
    }
}

```

###### 2.4.3.2 redirect to action result executor

```c#
public class RedirectToActionResultExecutor 
    : IActionResultExecutor<RedirectToActionResult>
{
    private readonly ILogger _logger;
    private readonly IUrlHelperFactory _urlHelperFactory;
           
    public RedirectToActionResultExecutor(
        ILoggerFactory loggerFactory, 
        IUrlHelperFactory urlHelperFactory)
    {
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }        
        if (urlHelperFactory == null)
        {
            throw new ArgumentNullException(nameof(urlHelperFactory));
        }
        
        _logger = loggerFactory.CreateLogger<RedirectToActionResult>();
        _urlHelperFactory = urlHelperFactory;
    }
    
        /// <inheritdoc />
    public virtual Task ExecuteAsync(
        ActionContext context, 
        RedirectToActionResult result)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }
        
        /* 解析 destination url */
        var urlHelper = result.UrlHelper ?? _urlHelperFactory.GetUrlHelper(context);
        
        var destinationUrl = urlHelper.Action(
            result.ActionName,
            result.ControllerName,
            result.RouteValues,
            protocol: null,
            host: null,
            fragment: result.Fragment);
        
        if (string.IsNullOrEmpty(destinationUrl))
        {
            throw new InvalidOperationException(Resources.NoRoutesMatched);
        }

        _logger.RedirectToActionResultExecuting(destinationUrl);
        
        /* 如果 preserve method = true，*/
        if (result.PreserveMethod)
        {
            // 返回 307(temporary) 或 308(permanent)
            context.HttpContext
                   .Response
                   .StatusCode = result.Permanent 
                					? StatusCodes.Status308PermanentRedirect 
                					: StatusCodes.Status307TemporaryRedirect;
            // 重写 http response header 的 location
            context.HttpContext
                   .Response
                   .Headers[HeaderNames.Location] = destinationUrl;
        }
        /* 否则，即preserve method = false */
        else
        {
            // 调用 http response 的 redirect 方法
            context.HttpContext
                   .Response
                   .Redirect(destinationUrl, result.Permanent);
        }
        
        return Task.CompletedTask;
    }
}

```

##### 2.4.4 redirect to page result

###### 2.4.4.1 redirect to page result

```c#
public class RedirectToPageResult : ActionResult, IKeepTempDataResult
{
    public IUrlHelper UrlHelper { get; set; }    
    
    public string PageName { get; set; }        
    public string PageHandler { get; set; }        
    public RouteValueDictionary RouteValues { get; set; }        
    public bool Permanent { get; set; }        
    public bool PreserveMethod { get; set; }        
    public string Fragment { get; set; }    
    
    public string Protocol { get; set; }        
    public string Host { get; set; }
    
    public RedirectToPageResult(string pageName)
        : this(
            pageName, 
            routeValues: null)
    {
    }
        
    public RedirectToPageResult(
        string pageName, 
        string pageHandler)
        	: this(
                pageName, 
                pageHandler, 
                routeValues: null)
    {
    }
        
    public RedirectToPageResult(
        string pageName, 
        object routeValues)
        	: this(
                pageName, 
                pageHandler: null, 
                routeValues: routeValues, 
                permanent: false)
    {
    }
        
    public RedirectToPageResult(
        string pageName, 
        string pageHandler, 
        object routeValues)
        	: this(
                pageName, 
                pageHandler, 
                routeValues, 
                permanent: false)
    {
    }
        
    public RedirectToPageResult(
        string pageName,
        string pageHandler,
        object routeValues,
        bool permanent)
        	: this(
                pageName, 
                pageHandler, 
                routeValues, 
                permanent, 
                fragment: null)
    {
    }
        
    public RedirectToPageResult(
        string pageName,
        string pageHandler,
        object routeValues,
        bool permanent,
        bool preserveMethod)
	        : this(
                pageName, 
                pageHandler, 
                routeValues, 
                permanent, 
                preserveMethod, 
                fragment: ull)
    {
    }
        
    public RedirectToPageResult(
        string pageName,
        string pageHandler,
        object routeValues,
        string fragment)
        	: this(
                pageName, 
                pageHandler, 
                routeValues, 
                permanent: false, 
                fragment: fragment)
    {
    }
       
    public RedirectToPageResult(
        string pageName,
        string pageHandler,
        object routeValues,
        bool permanent,
        string fragment)
        	: this(
                pageName, 
                pageHandler, 
                routeValues, 
                permanent, 
                preserveMethod: false, 
                fragment: fragment)
    {
    }
        
    public RedirectToPageResult(
        string pageName,
        string pageHandler,
        object routeValues,
        bool permanent,
        bool preserveMethod,
        string fragment)
    {
        PageName = pageName;
        PageHandler = pageHandler;
        RouteValues = routeValues == null ? null : new RouteValueDictionary(routeValues);
        PreserveMethod = preserveMethod;
        Permanent = permanent;
        Fragment = fragment;
    }
                
    /// <inheritdoc />
    public override Task ExecuteResultAsync(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        // 从 action context 中解析 redirect to page result executor
        var executor = 
            context.HttpContext
	               .RequestServices
       		       .GetRequiredService<IActionResultExecutor<RedirectToPageResult>>();
        // 使用 executor 执行 execute
        return executor.ExecuteAsync(context, this);
    }
}

```

###### 2.4.4.2 redirect to page result executor

```c#
public class RedirectToPageResultExecutor 
    : IActionResultExecutor<RedirectToPageResult>
{
    private readonly ILogger _logger;
    private readonly IUrlHelperFactory _urlHelperFactory;
        
    public RedirectToPageResultExecutor(
        ILoggerFactory loggerFactory, 
        IUrlHelperFactory urlHelperFactory)
    {
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }        
        if (urlHelperFactory == null)
        {
            throw new ArgumentNullException(nameof(urlHelperFactory));
        }
        
        _logger = loggerFactory.CreateLogger<RedirectToRouteResult>();
        _urlHelperFactory = urlHelperFactory;
    }
    
        /// <inheritdoc />
    public virtual Task ExecuteAsync(
        ActionContext context, 
        RedirectToPageResult result)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }
        
        /* 解析 destination url */
        var urlHelper = result.UrlHelper ?? _urlHelperFactory.GetUrlHelper(context);
        
        var destinationUrl = urlHelper.Page(
            result.PageName,
            result.PageHandler,
            result.RouteValues,
            result.Protocol,
            result.Host,
            fragment: result.Fragment);
        
        if (string.IsNullOrEmpty(destinationUrl))
        {
            throw new InvalidOperationException(
                Resources.FormatNoRoutesMatchedForPage(result.PageName));
        }
        
        _logger.RedirectToPageResultExecuting(result.PageName);
        
        /* 如果 preserve method = true */
        if (result.PreserveMethod)
        {
            // 返回 307 或 308
            context.HttpContext
                   .Response
                   .StatusCode = result.Permanent 
                					? StatusCodes.Status308PermanentRedirect 
                					: StatusCodes.Status307TemporaryRedirect;
            // 重写 http response header 的 location
            context.HttpContext
                   .Response
                   .Headers[HeaderNames.Location] = destinationUrl;
        }
        /* 否则，即 preserve method = false */
        else
        {
            // 调用 http response 的 redirect 方法
            context.HttpContext.Response.Redirect(destinationUrl, result.Permanent);
        }
        
        return Task.CompletedTask;
    }
}

```

##### 2.4.5 redirect to route result

###### 2.4.5.1 redirect to route result

```c#
public class RedirectToRouteResult : 
	ActionResult, 
	IKeepTempDataResult
{
    public IUrlHelper UrlHelper { get; set; }
        
    public string RouteName { get; set; }        
    public RouteValueDictionary RouteValues { get; set; }        
    public bool Permanent { get; set; }       
    public bool PreserveMethod { get; set; }        
    public string Fragment { get; set; }
    
    public RedirectToRouteResult(object routeValues)
        : this(
            routeName: null, 
            routeValues: routeValues)
    {
    }
        
    public RedirectToRouteResult(
        string routeName,
        object routeValues)
        	: this(
                routeName, 
                routeValues, 
                permanent: false)
    {
    }
        
    public RedirectToRouteResult(
        string routeName,
        object routeValues,
        bool permanent)
        	: this(
                routeName, 
                routeValues, 
                permanent, 
                fragment: null)
    {
    }
        
    public RedirectToRouteResult(
        string routeName,
        object routeValues,
        bool permanent,
        bool preserveMethod)
        	: this(
                routeName, 
                routeValues, 
                permanent, 
                preserveMethod, 
                fragment: null)
    {
    }
        
    public RedirectToRouteResult(
        string routeName,
        object routeValues,
        string fragment)
        	: this(
                routeName, 
                routeValues, 
                permanent: false, 
                fragment: fragment)
    {
    }
        
    public RedirectToRouteResult(
        string routeName,
        object routeValues,
        bool permanent,
        string fragment)
        	: this(
                routeName, 
                routeValues, 
                permanent, 
                preserveMethod: false, 
                fragment: fragment)
    {
    }
        
    public RedirectToRouteResult(
        string routeName,
        object routeValues,
        bool permanent,
        bool preserveMethod,
        string fragment)
    {
        RouteName = routeName;
        RouteValues = routeValues == null ? null : new RouteValueDictionary(routeValues);
        PreserveMethod = preserveMethod;
        Permanent = permanent;
        Fragment = fragment;
    }
            
    /// <inheritdoc />
    public override Task ExecuteResultAsync(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        // 从 action context 解析 redirect to route result executor
        var executor = 
            context.HttpContext
	               .RequestServices
    	           .GetRequiredService<IActionResultExecutor<RedirectToRouteResult>>();
        // 使用 executor 执行 execute
        return executor.ExecuteAsync(context, this);
    }
}

```

###### 2.4.5.2 redirect to route result executor

```c#
public class RedirectToRouteResultExecutor : IActionResultExecutor<RedirectToRouteResult>
{
    private readonly ILogger _logger;
    private readonly IUrlHelperFactory _urlHelperFactory;
        
    public RedirectToRouteResultExecutor(
        ILoggerFactory loggerFactory, 
        IUrlHelperFactory urlHelperFactory)
    {
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }        
        if (urlHelperFactory == null)
        {
            throw new ArgumentNullException(nameof(urlHelperFactory));
        }
        
        _logger = loggerFactory.CreateLogger<RedirectToRouteResult>();
        _urlHelperFactory = urlHelperFactory;
    }
    
    /// <inheritdoc />
    public virtual Task ExecuteAsync(
        ActionContext context, 
        RedirectToRouteResult result)
    {
        /* 解析 destination url */
        var urlHelper = result.UrlHelper ?? _urlHelperFactory.GetUrlHelper(context);
        
        var destinationUrl = urlHelper.RouteUrl(
            result.RouteName,
            result.RouteValues,
            protocol: null,
            host: null,
            fragment: result.Fragment);
        
        if (string.IsNullOrEmpty(destinationUrl))
        {
            throw new InvalidOperationException(Resources.NoRoutesMatched);
        }
        
        _logger.RedirectToRouteResultExecuting(destinationUrl, result.RouteName);
        
        /* 如果 preserve method = true */
        if (result.PreserveMethod)
        {
            // 返回 307 或 308 
            context.HttpContext
                   .Response
                   .StatusCode = result.Permanent 
                					? StatusCodes.Status308PermanentRedirect 
                					: StatusCodes.Status307TemporaryRedirect;
            // 重写 http response header 的 location
            context.HttpContext
                   .Response
                   .Headers[HeaderNames.Location] = destinationUrl;
        }
        /* 否则，即 preserve method = false */
        else
        {
            // 调用 http response 的 redirect 方法
            context.HttpContext.Response.Redirect(destinationUrl, result.Permanent);
        }
        
        return Task.CompletedTask;
    }
}

```

#### 2.5 status code action result 接口扩展

##### 2.5.1 status code action  result 接口

```c#
public interface IStatusCodeActionResult : IActionResult
{    
    int? StatusCode { get; }
}

```

##### 2.5.2 content result

###### 2.5.2.1 content result

```c#
public class ContentResult : 
	ActionResult, 
	IStatusCodeActionResult
{    
    public string Content { get; set; }        
    public string ContentType { get; set; }        
    public int? StatusCode { get; set; }
    
    /// <inheritdoc />
    public override Task ExecuteResultAsync(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        // 解析 content result executor 
        var executor = 
            context.HttpContext
            	   .RequestServices
            	   .GetRequiredService<IActionResultExecutor<ContentResult>>();
        // 使用 content result executor 的 execute 方法
        return executor.ExecuteAsync(context, this);
    }
}

```

###### 2.5.2.2 content result executor

```c#
public class ContentResultExecutor : IActionResultExecutor<ContentResult>
{
    private const string DefaultContentType = "text/plain; charset=utf-8";
    
    private readonly ILogger<ContentResultExecutor> _logger;
    private readonly IHttpResponseStreamWriterFactory _httpResponseStreamWriterFactory;
        
    public ContentResultExecutor(
        ILogger<ContentResultExecutor> logger, 
        IHttpResponseStreamWriterFactory httpResponseStreamWriterFactory)
    {
        _logger = logger;
        _httpResponseStreamWriterFactory = httpResponseStreamWriterFactory;
    }
    
    /// <inheritdoc />
    public virtual async Task ExecuteAsync(
        ActionContext context, 
        ContentResult result)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }
        
        var response = context.HttpContext.Response;
        
        // 解析 resolved content type 和 resolved content type encoding
        ResponseContentTypeHelper
            .ResolveContentTypeAndEncoding(
	            result.ContentType,
    	        response.ContentType,
        	    DefaultContentType,
            	out var resolvedContentType,
	            out var resolvedContentTypeEncoding);
        
        // 重写 http response 的 content type
        response.ContentType = resolvedContentType;
        
        // 重写 http response 的 status code
        if (result.StatusCode != null)
        {
            response.StatusCode = result.StatusCode.Value;
        }
        
        _logger.ContentResultExecuting(resolvedContentType);
        
        // 向 http response 中写入 result.content        
        if (result.Content != null)
        {
            response.ContentLength = 
                resolvedContentTypeEncoding.GetByteCount(result.Content);
            
            // 使用注入的 http response stream writer factory 创建 writer
            await using (var textWriter = _
                         httpResponseStreamWriterFactory
                         	.CreateWriter(
                                response.Body, 
                                resolvedContentTypeEncoding))
            {
                /* 写入 content */
                await textWriter.WriteAsync(result.Content);
                
                // Flushing the HttpResponseStreamWriter does not flush the underlying stream.
                // This just flushes the buffered text in the writer.
                // We do this rather than letting dispose handle it because 
                // dispose would call Write and we want to call WriteAsync.
                await textWriter.FlushAsync();
            }
        }
    }
}

```

##### 2.5.3 json result

###### 2.5.3.1 json result

```c#
public class JsonResult : 
	ActionResult, 
	IStatusCodeActionResult
{
    public string ContentType { get; set; }                    
    public int? StatusCode { get; set; }
    
    public object Value { get; set; } 
    public object SerializerSettings { get; set; }
            
    public JsonResult(object value)
    {
        Value = value;
    }
            
    public JsonResult(object value, object serializerSettings)
    {
        Value = value;
        SerializerSettings = serializerSettings;
    }
                   
    /// <inheritdoc />
    public override Task ExecuteResultAsync(ActionContext context)
    {
        if (context == null)
        {                
            throw new ArgumentNullException(nameof(context));            
        }
        // 从 action context 解析 json result executor
        var services = context.HttpContext.RequestServices;
        var executor = services.GetRequiredService<IActionResultExecutor<JsonResult>>();
        // 使用 executor 执行 execute
        return executor.ExecuteAsync(context, this);
    }
}

```

###### 2.5.3.2 system text json result executor

```c#
internal sealed class SystemTextJsonResultExecutor : IActionResultExecutor<JsonResult>
{
    // content type = “application/json"
    private static readonly string DefaultContentType = 
        new MediaTypeHeaderValue("application/json")
    	{
        	Encoding = Encoding.UTF8
    	}
    	.ToString();
    
    private readonly JsonOptions _options;
    private readonly ILogger<SystemTextJsonResultExecutor> _logger;
    private readonly AsyncEnumerableReader _asyncEnumerableReaderFactory;
    
    public SystemTextJsonResultExecutor(
        IOptions<JsonOptions> options,
        ILogger<SystemTextJsonResultExecutor> logger,
        IOptions<MvcOptions> mvcOptions)
    {
        _options = options.Value;
        _logger = logger;
        _asyncEnumerableReaderFactory = new AsyncEnumerableReader(mvcOptions.Value);
    }
    
    public async Task ExecuteAsync(
        ActionContext context, 
        JsonResult result)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }
        
        var jsonSerializerOptions = GetSerializerOptions(result);        
        var response = context.HttpContext.Response;
        
        // 解析 content type 和 content type encoding
        ResponseContentTypeHelper.ResolveContentTypeAndEncoding(
            result.ContentType,
            response.ContentType,
            DefaultContentType,
            out var resolvedContentType,
            out var resolvedContentTypeEncoding);
        
        // 向 response 中写入 content type        
        response.ContentType = resolvedContentType;
        
        if (result.StatusCode != null)
        {
            response.StatusCode = result.StatusCode.Value;
        }
        
        Log.JsonResultExecuting(_logger, result.Value);
        
        /* 异步读取 result 的 value */
        var value = result.Value;
        if (value != null && 
            _asyncEnumerableReaderFactory
            	.TryGetReader(value.GetType(), out var reader))
        {
            Log.BufferingAsyncEnumerable(_logger, value);
            value = await reader(value);
        }
        
        var objectType = value?.GetType() ?? typeof(object);
        
        // Keep this code in sync with SystemTextJsonOutputFormatter
        var responseStream = response.Body;
        
        /* 如果是 utf8 */
        if (resolvedContentTypeEncoding.CodePage == Encoding.UTF8.CodePage)
        {
            // 序列化 value
            await JsonSerializer.SerializeAsync(
                responseStream, 
                value, 
                objectType, 
                jsonSerializerOptions);
            // 写入 response
            await responseStream.FlushAsync();
        }
        /* 否则，即不是 utf8 */
        else
        {
            /* 编码 */
            // JsonSerializer only emits UTF8 encoded output, 
            // but we need to write the response in the encoding specified by
            // selectedEncoding
            var transcodingStream = Encoding.CreateTranscodingStream(
                response.Body, 
                resolvedContentTypeEncoding, 
                Encoding.UTF8, 
                leaveOpen: true);
            
            ExceptionDispatchInfo? exceptionDispatchInfo = null;
            try
            {
                // 序列化编码后的 value
                await JsonSerializer.SerializeAsync(
                    transcodingStream, 
                    value, 
                    objectType, 
                    jsonSerializerOptions);
                // 写入 response
                await transcodingStream.FlushAsync();
            }
            catch (Exception ex)
            {
                // TranscodingStream may write to the inner stream as part of it's disposal.
                // We do not want this exception "ex" to be eclipsed 
                // by any exception encountered during the write. 
                // We will stash it and explicitly rethrow it during the finally block.
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
    
    // 解析 json serialize options    
    private JsonSerializerOptions GetSerializerOptions(JsonResult result)
    {
        var serializerSettings = result.SerializerSettings;
        if (serializerSettings == null)
        {
            return _options.JsonSerializerOptions;
        }
        else
        {
            if (serializerSettings is not JsonSerializerOptions settingsFromResult)
            {
                throw new InvalidOperationException(
                    Resources.FormatProperty_MustBeInstanceOfType(
	                    nameof(JsonResult),
    	                nameof(JsonResult.SerializerSettings),
        	            typeof(JsonSerializerOptions)));
            }
            
            return settingsFromResult;
        }
    }
    
    private static class Log
    {
        private static readonly Action<ILogger, string?, Exception?> 
            _jsonResultExecuting = 
            	LoggerMessage.Define<string?>(
		            LogLevel.Information,
    		        new EventId(1, "JsonResultExecuting"),
        		    "Executing JsonResult, writing value of type '{Type}'.");
        
        private static readonly Action<ILogger, string?, Exception?> 
            _bufferingAsyncEnumerable = 
            	LoggerMessage.Define<string?>(
		            LogLevel.Debug,
        		    new EventId(2, "BufferingAsyncEnumerable"),
		            "Buffering IAsyncEnumerable instance of type '{Type}'.");
        
        public static void JsonResultExecuting(ILogger logger, object value)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                var type = value == null ? "null" : value.GetType().FullName;
                _jsonResultExecuting(logger, type, null);
            }
        }
        
        public static void BufferingAsyncEnumerable(ILogger logger, object asyncEnumerable)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                _bufferingAsyncEnumerable(logger, asyncEnumerable.GetType().FullName, null);
            }
        }
    }
}

```

##### 2.5.4 object result

###### 2.5.4.1 object result

```c#
public class ObjectResult : 
	ActionResult, 
	IStatusCodeActionResult
{
    private MediaTypeCollection _contentTypes;
    public MediaTypeCollection ContentTypes
    {
        get => _contentTypes;
        set => _contentTypes = value ?? throw new ArgumentNullException(nameof(value));
    }
        
    public int? StatusCode { get; set; }    
    [ActionResultObjectValue]
    public object Value { get; set; }
    public Type DeclaredType { get; set; }    
    public FormatterCollection<IOutputFormatter> Formatters { get; set; }
                                                
    public ObjectResult(object value)
    {
        Value = value;
        Formatters = new FormatterCollection<IOutputFormatter>();
        _contentTypes = new MediaTypeCollection();
    }
                
    /// <inheritdoc/>
    public override Task ExecuteResultAsync(ActionContext context)
    {
        // 从 action context 中解析 object result executor
        var executor = 
            context.HttpContext
	               .RequestServices
       		       .GetRequiredService<IActionResultExecutor<ObjectResult>>();
        // 使用 executor 执行 execute
        return executor.ExecuteAsync(context, this);
    }
    
    // for executor
    public virtual void OnFormatting(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        if (Value is ProblemDetails details)
        {
            if (details.Status != null && 
                StatusCode == null)
            {
                StatusCode = details.Status;
            }
            else if (details.Status == null && 
                     StatusCode != null)
            {
                details.Status = StatusCode;
            }
        }
        
        if (StatusCode.HasValue)
        {
            context.HttpContext
                   .Response
                   .StatusCode = StatusCode.Value;
        }
    }
}

```

###### 2.5.4.2 object result executor

```c#
public class ObjectResultExecutor : IActionResultExecutor<ObjectResult>
{    
    private readonly AsyncEnumerableReader _asyncEnumerableReaderFactory;
    
    protected OutputFormatterSelector FormatterSelector { get; }
    protected Func<Stream, Encoding, TextWriter> WriterFactory { get; }
    protected ILogger Logger { get; }
                       
    public ObjectResultExecutor(
        OutputFormatterSelector formatterSelector,
        IHttpResponseStreamWriterFactory writerFactory,
        ILoggerFactory loggerFactory,
        IOptions<MvcOptions> mvcOptions)
    {
        if (formatterSelector == null)
        {
            throw new ArgumentNullException(nameof(formatterSelector));
        }        
        if (writerFactory == null)
        {
            throw new ArgumentNullException(nameof(writerFactory));
        }        
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }
        
        /* 注入服务*/
        FormatterSelector = formatterSelector;
        WriterFactory = writerFactory.CreateWriter;
        Logger = loggerFactory.CreateLogger<ObjectResultExecutor>();
        
        /* 创建 async reader */
        var options = mvcOptions?.Value ?? throw new ArgumentNullException(nameof(mvcOptions));
        _asyncEnumerableReaderFactory = new AsyncEnumerableReader(options);
    }
                   
    public virtual Task ExecuteAsync(
        ActionContext context, 
        ObjectResult result)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }        
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }
        
        // a - 推断 content type
        InferContentTypes(context, result);
        
        /* 设置 object type，
           从 result 中的 declare type 解析，
           如果 declare type 为 null 或者 object，设置为 value 的类型 */
        var objectType = result.DeclaredType;
        
        if (objectType == null || 
            objectType == typeof(object))
        {
            objectType = result.Value?.GetType();
        }
        
        /* 异步解析 value */
        var value = result.Value;
        
        if (value != null && 
            _asyncEnumerableReaderFactory
            	.TryGetReader(value.GetType(), out var reader))
        {
            // b - execute enumerable
            return ExecuteAsyncEnumerable(context, result, value, reader);
        }
        
        // c - execute core
        return ExecuteAsyncCore(context, result, objectType, value);
    }
    
    // a - 推断 content type
    private static void InferContentTypes(
        ActionContext context, 
        ObjectResult result)
    {
        /* 如果 result 中存在 content type，
           即认为 result 的 content type 完成设置，返回 */
        Debug.Assert(result.ContentTypes != null);
        if (result.ContentTypes.Count != 0)
        {
            return;
        }
        
        /* 添加 content type */
        // If the user sets the content type both on the ObjectResult 
        // (example: by Produces) and Response object,
        // then the one set on ObjectResult takes precedence over the Response object
        var responseContentType = context.HttpContext.Response.ContentType;
        if (!string.IsNullOrEmpty(responseContentType))
        {
            // 如果 http response 的 content type 不为空，添加
            result.ContentTypes.Add(responseContentType);
        }
        else if (result.Value is ProblemDetails)
        {
            // 如果 result.value 是 problemdetails，添加下列 content type            
            result.ContentTypes.Add("application/problem+json");
            result.ContentTypes.Add("application/problem+xml");
        }
    }
    
    // b -
    private async Task ExecuteAsyncEnumerable(
        ActionContext context, 
        ObjectResult result, 
        object asyncEnumerable, 
        Func<object, Task<ICollection>> reader)
    {
        Log.BufferingAsyncEnumerable(Logger, asyncEnumerable);
        // 获取 enumerable 类型
        var enumerated = await reader(asyncEnumerable);
        await ExecuteAsyncCore(context, result, enumerated.GetType(), enumerated);
    }
    
    // c -
    private Task ExecuteAsyncCore(
        ActionContext context, 
        ObjectResult result, 
        Type? objectType, 
        object? value)
    {
        // 创建 formatter context，即注入 object 相关信息
        var formatterContext = new OutputFormatterWriteContext(
            context.HttpContext,
            WriterFactory,
            objectType,
            value);
        
        // 解析 formatter
        var selectedFormatter = FormatterSelector.SelectFormatter(
            formatterContext,
            (IList<IOutputFormatter>)result.Formatters ?? Array.Empty<IOutputFormatter>(),
            result.ContentTypes);
        
        // formatter 为 null，即不支持 format，返回 406
        if (selectedFormatter == null)
        {
            // No formatter supports this.
            Logger.NoFormatter(formatterContext, result.ContentTypes);
            
            context.HttpContext.Response.StatusCode = StatusCodes.Status406NotAcceptable;
            return Task.CompletedTask;
        }
        
        Logger.ObjectResultExecuting(result, value);
        
        // 使用 formatter 将 result value 写入 http response
        result.OnFormatting(context);
        return selectedFormatter.WriteAsync(formatterContext);
    }
            
    private static class Log
    {
        private static readonly Action<ILogger, string?, Exception?> _bufferingAsyncEnumerable;
        
        static Log()
        {
            _bufferingAsyncEnumerable = LoggerMessage.Define<string?>(
                LogLevel.Debug,
                new EventId(1, "BufferingAsyncEnumerable"),
                "Buffering IAsyncEnumerable instance of type '{Type}'.");
        }
        
        public static void BufferingAsyncEnumerable(ILogger logger, object asyncEnumerable)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                _bufferingAsyncEnumerable(logger, asyncEnumerable.GetType().FullName, null);
            }
        }
    }
}

```

###### 2.5.4.3 default states code attribute

```c#
[AttributeUsage(
    AttributeTargets.Class, 
    AllowMultiple = false, 
    Inherited = true)]
public sealed class DefaultStatusCodeAttribute : Attribute
{
    public int StatusCode { get; }
    
    public DefaultStatusCodeAttribute(int statusCode)
    {
        StatusCode = statusCode;
    }            
}

```

###### 2.5.4.4 action result object value attribute

```c#
// Attribute annotated on ActionResult constructor, helper method parameters, 
// and properties to indicate that the parameter or property is used 
// to set the "value" for ActionResult.
[AttributeUsage(
    AttributeTargets.Parameter | AttributeTargets.Property, 
    AllowMultiple = false, Inherited = false)]
public sealed class ActionResultObjectValueAttribute : Attribute
{
}

```

##### 2.5.5 object result 扩展

###### 2.5.5.1 -200 ok object result

```c#
[DefaultStatusCode(DefaultStatusCode)]
public class OkObjectResult : ObjectResult
{
    private const int DefaultStatusCode = StatusCodes.Status200OK;
        
    public OkObjectResult(object value) : base(value)
    {
        StatusCode = DefaultStatusCode;
    }
}

```

###### 2.5.5.2 -201 created result

```c#
[DefaultStatusCode(DefaultStatusCode)]
public class CreatedResult : ObjectResult
{
    private const int DefaultStatusCode = StatusCodes.Status201Created;
    
    private string _location;
    public string Location
    {
        get => _location;
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            
            _location = value;
        }
    }
        
    public CreatedResult(
        string location, 
        object value) : base(value)
    {
        if (location == null)
        {
            throw new ArgumentNullException(nameof(location));
        }
        
        Location = location;
        StatusCode = DefaultStatusCode;
    }
            
    public CreatedResult(
        Uri location, 
        object value) : base(value)
    {
        if (location == null)
        {
            throw new ArgumentNullException(nameof(location));
        }
        
        if (location.IsAbsoluteUri)
        {
            Location = location.AbsoluteUri;
        }
        else
        {
            Location = location.GetComponents(
                					UriComponents.SerializationInfoString, 
                					UriFormat.UriEscaped);
        }
        
        StatusCode = DefaultStatusCode;
    }
            
    public override void OnFormatting(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        base.OnFormatting(context);        
        context.HttpContext
               .Response
               .Headers[HeaderNames.Location] = Location;
    }
}

```

###### 2.5.5.3 -201 created at action result

```c#
[DefaultStatusCode(DefaultStatusCode)]
public class CreatedAtActionResult : ObjectResult
{
    private const int DefaultStatusCode = StatusCodes.Status201Created;
    
    public IUrlHelper UrlHelper { get; set; }
        
    public string ActionName { get; set; }        
    public string ControllerName { get; set; }        
    public RouteValueDictionary RouteValues { get; set; }
    
    public CreatedAtActionResult(
        string actionName,
        string controllerName,
        object routeValues,
        [ActionResultObjectValue] object value) : base(value)
    {
        ActionName = actionName;
        ControllerName = controllerName;
        RouteValues = routeValues == null ? null : new RouteValueDictionary(routeValues);
        StatusCode = DefaultStatusCode;
    }
                   
    /// <inheritdoc />
    public override void OnFormatting(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        base.OnFormatting(context);
                
        var request = context.HttpContext.Request;
        
        /* 解析 url */
        var urlHelper = UrlHelper;
        if (urlHelper == null)
        {
            var services = context.HttpContext.RequestServices;
            urlHelper = services.GetRequiredService<IUrlHelperFactory>().GetUrlHelper(context);
        }
        
        var url = urlHelper.Action(
            					ActionName,
					            ControllerName,
					            RouteValues,
					            request.Scheme,
					            request.Host.ToUriComponent());
        
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException(Resources.NoRoutesMatched);
        }
        
        /* 重写 http response header 的 location */
        context.HttpContext.Response.Headers[HeaderNames.Location] = url;
    }
}

```

###### 2.5.5.4 -201 created at route result

```c#
[DefaultStatusCode(DefaultStatusCode)]
public class CreatedAtRouteResult : ObjectResult
{
    private const int DefaultStatusCode = StatusCodes.Status201Created;
        
    public IUrlHelper UrlHelper { get; set; }       
    public string RouteName { get; set; }        
    public RouteValueDictionary RouteValues { get; set; }
    
    public CreatedAtRouteResult(
        object routeValues, 
        [ActionResultObjectValue] object value)            
        	: this(
                routeName: null, 
                routeValues: 
                routeValues, 
                value: value)        
    {
    }
            
    public CreatedAtRouteResult(
        string routeName,
        object routeValues,
        [ActionResultObjectValue] object value) : base(value)
    {
        RouteName = routeName;
        RouteValues = routeValues == null ? null : new RouteValueDictionary(routeValues);
        StatusCode = DefaultStatusCode;
    }
                
    /// <inheritdoc />
    public override void OnFormatting(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        base.OnFormatting(context);
        
        /* 解析 url */
        var urlHelper = UrlHelper;
        if (urlHelper == null)
        {
            var services = context.HttpContext.RequestServices;
            urlHelper = services.GetRequiredService<IUrlHelperFactory>().GetUrlHelper(context);
        }
        
        var url = urlHelper.Link(RouteName, RouteValues);
        
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException(Resources.NoRoutesMatched);
        }
        
        /* 重写 http response header 的 location */
        context.HttpContext.Response.Headers[HeaderNames.Location] = url;
    }
}

```

###### 2.5.5.5 -202 accepted result

```c#
[DefaultStatusCode(DefaultStatusCode)]
public class AcceptedResult : ObjectResult
{
    private const int DefaultStatusCode = StatusCodes.Status202Accepted;
    
    public string Location { get; set; }
    
    public AcceptedResult() : base(value: null)
    {
        StatusCode = DefaultStatusCode;
    }
            
    public AcceptedResult(
        string location, 
        [ActionResultObjectValue] object value) : base(value)
    {
        Location = location;
        StatusCode = DefaultStatusCode;
    }
            
    public AcceptedResult(
        Uri locationUri, 
        [ActionResultObjectValue] object value) : base(value)
    {
        if (locationUri == null)
        {
            throw new ArgumentNullException(nameof(locationUri));
        }
        
        if (locationUri.IsAbsoluteUri)
        {
            Location = locationUri.AbsoluteUri;
        }
        else
        {
            Location = locationUri.GetComponents(
                					   UriComponents.SerializationInfoString, 
                					   UriFormat.UriEscaped);
        }
        
        StatusCode = DefaultStatusCode;
    }
        
    /// <inheritdoc />
    public override void OnFormatting(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        base.OnFormatting(context);
        
        if (!string.IsNullOrEmpty(Location))
        {
            // 重写 http response header 的 location
            context.HttpContext.Response.Headers[HeaderNames.Location] = Location;
        }
    }
}

```

###### 2.5.5.6 -202 accepted at action result

```c#
[DefaultStatusCode(DefaultStatusCode)]
public class AcceptedAtActionResult : ObjectResult
{
    private const int DefaultStatusCode = StatusCodes.Status202Accepted;
    
    public IUrlHelper UrlHelper { get; set; }
        
    public string ActionName { get; set; }        
    public string ControllerName { get; set; }        
    public RouteValueDictionary RouteValues { get; set; }
        
    public AcceptedAtActionResult(
        string actionName,
        string controllerName,
        object routeValues,
        [ActionResultObjectValue] object value) : base(value)
    {
        ActionName = actionName;
        ControllerName = controllerName;
        RouteValues = routeValues == null ? null : new RouteValueDictionary(routeValues);
        StatusCode = DefaultStatusCode;
    }
                
    /// <inheritdoc />
    public override void OnFormatting(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        base.OnFormatting(context);
        
        var request = context.HttpContext.Request;
        
        /* 解析 url */
        var urlHelper = UrlHelper;
        if (urlHelper == null)
        {
            var services = context.HttpContext.RequestServices;
            urlHelper = services.GetRequiredService<IUrlHelperFactory>().GetUrlHelper(context);
        }
        
        var url = urlHelper.Action(
            ActionName,
            ControllerName,
            RouteValues,
            request.Scheme,
            request.Host.ToUriComponent());
        
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException(Resources.NoRoutesMatched);
        }
        
        /* 重写 http response header 的 location 为 url */
        context.HttpContext.Response.Headers[HeaderNames.Location] = url;
    }
}

```

###### 2.5.5.7 -202 accepted at route result

```c#
[DefaultStatusCode(DefaultStatusCode)]
public class AcceptedAtRouteResult : ObjectResult
{
    private const int DefaultStatusCode = StatusCodes.Status202Accepted;
           
    public IUrlHelper UrlHelper { get; set; }
            
    public string RouteName { get; set; }        
    public RouteValueDictionary RouteValues { get; set; }
    
    public AcceptedAtRouteResult(
        object routeValues, 
        [ActionResultObjectValue] object value)            
        	: this(
                routeName: null, 
                routeValues: routeValues, 
                value: value)
    {
    }
            
    public AcceptedAtRouteResult(
        string routeName,
        object routeValues,
        [ActionResultObjectValue] object value) : base(value)
    {
        RouteName = routeName;
        RouteValues = routeValues == null ? null : new RouteValueDictionary(routeValues);
        StatusCode = DefaultStatusCode;
    }
    
    /// <inheritdoc />
    public override void OnFormatting(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        base.OnFormatting(context);
        
        /* 解析 url */
        var urlHelper = UrlHelper;
        if (urlHelper == null)
        {
            var services = context.HttpContext.RequestServices;
            urlHelper = services.GetRequiredService<IUrlHelperFactory>().GetUrlHelper(context);
        }
        
        var url = urlHelper.Link(RouteName, RouteValues);
        
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException(Resources.NoRoutesMatched);
        }
        
        /* 重写 http response header 的 location 为 url */
        context.HttpContext.Response.Headers[HeaderNames.Location] = url;
    }
}

```

###### 2.5.5.8 -400 bad request object result

```c#
[DefaultStatusCode(DefaultStatusCode)]
public class BadRequestObjectResult : ObjectResult
{
    private const int DefaultStatusCode = StatusCodes.Status400BadRequest;
            
    public BadRequestObjectResult([ActionResultObjectValue] object error) : base(error)
    {
        StatusCode = DefaultStatusCode;
    }
            
    public BadRequestObjectResult(
        [ActionResultObjectValue] ModelStateDictionary modelState)            
        	: base(new SerializableError(modelState))
    {
        if (modelState == null)
        {
            throw new ArgumentNullException(nameof(modelState));
        }
        
        StatusCode = DefaultStatusCode;
    }
}

```

###### 2.5.5.9 -401 unauthorized object result

```c#
[DefaultStatusCode(DefaultStatusCode)]
public class UnauthorizedObjectResult : ObjectResult
{
    private const int DefaultStatusCode = StatusCodes.Status401Unauthorized;
        
    public UnauthorizedObjectResult(
        [ActionResultObjectValue] object value) : base(value)
    {
        StatusCode = DefaultStatusCode;
    }
}

```

###### 2.5.5.10 -404 not found object result

```c#
[DefaultStatusCode(DefaultStatusCode)]
public class NotFoundObjectResult : ObjectResult
{
    private const int DefaultStatusCode = StatusCodes.Status404NotFound;
        
    public NotFoundObjectResult([ActionResultObjectValue] object value) : base(value)
    {
        StatusCode = DefaultStatusCode;
    }
}

```

###### 2.5.5.11 -409 conflict object result

```c#
[DefaultStatusCode(DefaultStatusCode)]
public class ConflictObjectResult : ObjectResult
{
    private const int DefaultStatusCode = StatusCodes.Status409Conflict;
        
    public ConflictObjectResult([ActionResultObjectValue] object error) : base(error)
    {
        StatusCode = DefaultStatusCode;
    }
        
    public ConflictObjectResult(
        [ActionResultObjectValue] ModelStateDictionary modelState)            
        	: base(new SerializableError(modelState))
    {
        if (modelState == null)
        {
            throw new ArgumentNullException(nameof(modelState));
        }
        
        StatusCode = DefaultStatusCode;
    }
}

```

###### 2.5.5.12 -422 unprocessable entity object result

```c#
[DefaultStatusCode(DefaultStatusCode)]
public class UnprocessableEntityObjectResult : ObjectResult
{
    private const int DefaultStatusCode = StatusCodes.Status422UnprocessableEntity;
            
    public UnprocessableEntityObjectResult(
        [ActionResultObjectValue] ModelStateDictionary modelState)            
        	: this(new SerializableError(modelState))
    {
    }
            
    public UnprocessableEntityObjectResult(
        [ActionResultObjectValue] object error) : base(error)
    {
        StatusCode = DefaultStatusCode;
    }
}

```

##### 2.5.5 action result  泛型

###### 2.5.5.1 convert to action result 接口

```c#
public interface IConvertToActionResult
{    
    IActionResult Convert();
}

```

###### 2.5.5.2 action result T

```c#
public sealed class ActionResult<TValue> : IConvertToActionResult
{
    private const int DefaultStatusCode = StatusCodes.Status200OK;
    
    public ActionResult Result { get; }        
    public TValue Value { get; }
    
    /* 由 tValue 创建 action result*/
    public ActionResult(TValue value)
    {
        if (typeof(IActionResult).IsAssignableFrom(typeof(TValue)))
        {
            var error = Resources.FormatInvalidTypeTForActionResultOfT(
                			  		  typeof(TValue), 
		                			  "ActionResult<T>");
            throw new ArgumentException(error);
        }
        
        Value = value;
    }
    
     public static implicit operator ActionResult<TValue>(TValue value)
    {
        return new ActionResult<TValue>(value);
    }
        
    /* 由 action result 创建 action result */
    public ActionResult(ActionResult result)
    {
        if (typeof(IActionResult).IsAssignableFrom(typeof(TValue)))
        {
            var error = Resources.FormatInvalidTypeTForActionResultOfT(
                					  typeof(TValue), 
                					  "ActionResult<T>");
            throw new ArgumentException(error);
        }
        
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }                               
            
    public static implicit operator ActionResult<TValue>(ActionResult result)
    {
        return new ActionResult<TValue>(result);
    }
    
    /* 实现 convert to action result 接口 */
    IActionResult IConvertToActionResult.Convert()
    {
        if (Result != null)
        {
            return Result;
        }
        
        int statusCode;
        if (Value is ProblemDetails problemDetails && problemDetails.Status != null)
        {
            statusCode = problemDetails.Status.Value;
        }
        else
        {
            statusCode = DefaultStatusCode;
        }
        
        return new ObjectResult(Value)
        {
            DeclaredType = typeof(TValue),
            StatusCode = statusCode
        };
    }
}

```

#### 2.6 problem details

##### 2.6.1 problem details

###### 2.6.1.1 problem details

```c#
[JsonConverter(typeof(ProblemDetailsJsonConverter))]
public class ProblemDetails
{    
    [JsonPropertyName("type")]
    public string Type { get; set; }
        
    [JsonPropertyName("title")]
    public string Title { get; set; }
        
    [JsonPropertyName("status")]
    public int? Status { get; set; }
        
    [JsonPropertyName("detail")]
    public string Detail { get; set; }
        
    [JsonPropertyName("instance")]
    public string Instance { get; set; }
        
    [JsonExtensionData]
    public IDictionary<string, object> Extensions { get; } = 
        new Dictionary<string, object>(StringComparer.Ordinal);
}

```

###### 2.6.1.2 problem details json convert

```c#
internal class ProblemDetailsJsonConverter : JsonConverter<ProblemDetails>
{
    private static readonly JsonEncodedText Type = JsonEncodedText.Encode("type");
    private static readonly JsonEncodedText Title = JsonEncodedText.Encode("title");
    private static readonly JsonEncodedText Status = JsonEncodedText.Encode("status");
    private static readonly JsonEncodedText Detail = JsonEncodedText.Encode("detail");
    private static readonly JsonEncodedText Instance = JsonEncodedText.Encode("instance");
    
    /* read 方法 */
    public override ProblemDetails Read(
        ref Utf8JsonReader reader, 
        Type typeToConvert, 
        JsonSerializerOptions options)
    {
        var problemDetails = new ProblemDetails();
        
        // 如果不是 start object，抛出异常
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException(Resources.UnexpectedJsonEnd);
        }
        // reader 一直读取
        while (reader.Read() && 
               reader.TokenType != JsonTokenType.EndObject)
        {
            ReadValue(ref reader, problemDetails, options);
        }
        // 如果 reader 不是 end object，抛出异常
        if (reader.TokenType != JsonTokenType.EndObject)
        {
            throw new JsonException(Resources.UnexpectedJsonEnd);
        }
        
        return problemDetails;
    }
    
    

    /* write 方法 */
    public override void Write(
        Utf8JsonWriter writer, 
        ProblemDetails value, 
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        WriteProblemDetails(writer, value, options);
        writer.WriteEndObject();
    }

    internal static void ReadValue(
        ref Utf8JsonReader reader, 
        ProblemDetails value, 
        JsonSerializerOptions options)
    {
        // for type
        if (TryReadStringProperty(ref reader, Type, out var propertyValue))
        {
            value.Type = propertyValue;
        }
        // for title
        else if (TryReadStringProperty(ref reader, Title, out propertyValue))
        {
            value.Title = propertyValue;
        }
        // for details
        else if (TryReadStringProperty(ref reader, Detail, out propertyValue))
        {
            value.Detail = propertyValue;
        }
        // for instance
        else if (TryReadStringProperty(ref reader, Instance, out propertyValue))
        {
            value.Instance = propertyValue;
        }
        // status
        else if (reader.ValueTextEquals(Status.EncodedUtf8Bytes))
        {
            reader.Read();
            if (reader.TokenType == JsonTokenType.Null)
            {
                // Nothing to do here.
            }
            else
            {
                value.Status = reader.GetInt32();
            }
        }
        // extensions
        else
        {
            var key = reader.GetString();
            reader.Read();
            value.Extensions[key] = JsonSerializer.Deserialize(
                									  ref reader, 
                									  typeof(object), 
                									  options);
        }
    }
    
    internal static bool TryReadStringProperty(
        ref Utf8JsonReader reader, 
        JsonEncodedText propertyName, 
        [NotNullWhen(true)] out string? value)
    {
        if (!reader.ValueTextEquals(propertyName.EncodedUtf8Bytes))
        {
            value = default;
            return false;
        }
        
        reader.Read();
        value = reader.GetString()!;
        return true;
    }
    
    /* write 方法 */
    
    internal static void WriteProblemDetails(
        Utf8JsonWriter writer, 
        ProblemDetails value, 
        JsonSerializerOptions options)
    {
        // for type
        if (value.Type != null)
        {
            writer.WriteString(Type, value.Type);
        }
        // for title
        if (value.Title != null)
        {
            writer.WriteString(Title, value.Title);
        }
        // for status
        if (value.Status != null)
        {
            writer.WriteNumber(Status, value.Status.Value);
        }
        // for details
        if (value.Detail != null)
        {
            writer.WriteString(Detail, value.Detail);
        }
        // for instance
        if (value.Instance != null)
        {
            writer.WriteString(Instance, value.Instance);
        }
        // for extensions
        foreach (var kvp in value.Extensions)
        {
            writer.WritePropertyName(kvp.Key);
            JsonSerializer.Serialize(
                			   writer, 
                			   kvp.Value, 
                			   kvp.Value?.GetType() ?? typeof(object), 
                			   options);
        }
    }
}

```

##### 2.6.2 validation problem details

###### 2.6.2.1 validation problem details

```c#
[JsonConverter(typeof(ValidationProblemDetailsJsonConverter))]
public class ValidationProblemDetails : ProblemDetails
{    
    [JsonPropertyName("errors")]
    public IDictionary<string, string[]> Errors { get; } = 
        new Dictionary<string, string[]>(StringComparer.Ordinal);
    
    public ValidationProblemDetails()
    {
        Title = Resources.ValidationProblemDescription_Title;
    }
        
    /* 由 model state dictionary 创建 validation problem details  */
    public ValidationProblemDetails(ModelStateDictionary modelState)      
        : this()
    {
        if (modelState == null)
        {
            throw new ArgumentNullException(nameof(modelState));
        }
        
        foreach (var keyModelStatePair in modelState)
        {
            var key = keyModelStatePair.Key;
            var errors = keyModelStatePair.Value.Errors;
            if (errors != null && errors.Count > 0)
            {
                if (errors.Count == 1)
                {
                    var errorMessage = GetErrorMessage(errors[0]);
                    Errors.Add(key, new[] { errorMessage });
                }
                else
                {
                    var errorMessages = new string[errors.Count];
                    for (var i = 0; i < errors.Count; i++)
                    {
                        errorMessages[i] = GetErrorMessage(errors[i]);
                    }
                    
                    Errors.Add(key, errorMessages);
                }
            }
        }
        
        string GetErrorMessage(ModelError error)
        {
            return string.IsNullOrEmpty(error.ErrorMessage) 
                ? Resources.SerializableError_DefaultError 
                : error.ErrorMessage;            
        }
    }
            
    /* 由 error dictionary 创建 validation problemdetails */
    public ValidationProblemDetails(IDictionary<string, string[]> errors)        
        : this()
    {
        if (errors == null)
        {
            throw new ArgumentNullException(nameof(errors));
        }
        
        Errors = new Dictionary<string, string[]>(errors, StringComparer.Ordinal);
    }          
}

```

###### 2.6.2.2 validation problem details json convert

```c#
internal class ValidationProblemDetailsJsonConverter : JsonConverter<ValidationProblemDetails>
{
    private static readonly JsonEncodedText Errors = JsonEncodedText.Encode("errors");
    
    /* read 方法 */
    public override ValidationProblemDetails Read(
        ref Utf8JsonReader reader, 
        Type typeToConvert, 
        JsonSerializerOptions options)
    {
        var problemDetails = new ValidationProblemDetails();
        
        // 如果不是 start object，抛出异常
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException(Resources.UnexpectedJsonEnd);
        }
        // reader 一直读取
        while (reader.Read() && 
               reader.TokenType != JsonTokenType.EndObject)
        {
            // 如果读到 "error"，
            if (reader.ValueTextEquals(Errors.EncodedUtf8Bytes))
            {
                // 逆序列化 errors
                var errors = 
                    JsonSerializer.Deserialize<Dictionary<string, string[]>>(
                    				   ref reader, 
                    				   options);
                // 将 errors 注入 problem details
                if (errors is not null)
                {
                    foreach (var item in errors)
                    {
                        problemDetails.Errors[item.Key] = item.Value;
                    }
                }
            }
            // 否则，调用基类方法读取数据（即 title、type等）
            else
            {
                ReadValue(ref reader, problemDetails, options);
            }
        }
        // 如果不是 end object，抛出异常
        if (reader.TokenType != JsonTokenType.EndObject)
        {
            throw new JsonException(Resources.UnexpectedJsonEnd);
        }
        
        return problemDetails;
    }
    
    /* write 方法  */
    public override void Write(
        Utf8JsonWriter writer, 
        ValidationProblemDetails value, 
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        WriteProblemDetails(writer, value, options);
        
        writer.WriteStartObject(Errors);
        foreach (var kvp in value.Errors)
        {
            writer.WritePropertyName(kvp.Key);
            JsonSerializer.Serialize(
                			   writer, 
                			   kvp.Value, 
                			   kvp.Value?.GetType() ?? typeof(object), 
                			   options);
        }
        writer.WriteEndObject();
        
        writer.WriteEndObject();
    }
}

```

##### 2.6.2 problem details factory

###### 2.6.2.1 problem details factory

```c#
public abstract class ProblemDetailsFactory
{    
    public abstract ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null);
        
    public abstract ValidationProblemDetails CreateValidationProblemDetails(
        HttpContext httpContext,
        ModelStateDictionary modelStateDictionary,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null);
}

```

###### 2.6.2.2 default problem details factory

```c#
internal sealed class DefaultProblemDetailsFactory : ProblemDetailsFactory
{
    private readonly ApiBehaviorOptions _options;
    
    public DefaultProblemDetailsFactory(IOptions<ApiBehaviorOptions> options)
    {
        _options = options?.Value 
            			   ?? throw new ArgumentNullException(nameof(options));
    }
    
    /* 创建 problem details */
    public override ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null)
    {
        statusCode ??= 500;
        
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = instance,
        };
                
        ApplyProblemDetailsDefaults(
            httpContext, 
            problemDetails, 
            statusCode.Value);
        
        return problemDetails;
    }
    
    /* 创建 validation problem details */
    public override ValidationProblemDetails CreateValidationProblemDetails(
        HttpContext httpContext,
        ModelStateDictionary modelStateDictionary,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null)
    {
        if (modelStateDictionary == null)
        {
            throw new ArgumentNullException(nameof(modelStateDictionary));
        }
        
        statusCode ??= 400;
        
        var problemDetails = new ValidationProblemDetails(modelStateDictionary)
        {
            Status = statusCode,
            Type = type,
            Detail = detail,
            Instance = instance,
        };
        
        if (title != null)
        {
            // For validation problem details, don't overwrite the default title with null.
            problemDetails.Title = title;
        }
        
        ApplyProblemDetailsDefaults(
            httpContext, 
            problemDetails, 
            statusCode.Value);
        
        return problemDetails;
    }
    
    // 应用 defaults
    private void ApplyProblemDetailsDefaults(
        HttpContext httpContext, 
        ProblemDetails problemDetails, 
        int statusCode)
    {
        problemDetails.Status ??= statusCode;
        
        if (_options.ClientErrorMapping
            		.TryGetValue(
                        statusCode, 
                        out var clientErrorData))
        {
            problemDetails.Title ??= clientErrorData.Title;
            problemDetails.Type ??= clientErrorData.Link;
        }
        
        var traceId = Activity.Current
            				  ?.Id 
            				  ?? httpContext
            				  ?.TraceIdentifier;
        if (traceId != null)
        {
            problemDetails.Extensions["traceId"] = traceId;
        }
    }
}

```

###### 2.6.2.3 problem details client error factory

```c#
public interface IClientErrorFactory
{    
    IActionResult? GetClientError(
        ActionContext actionContext, 
        IClientErrorActionResult clientError);
}

```



```c#
internal class ProblemDetailsClientErrorFactory : IClientErrorFactory
{
    private readonly ProblemDetailsFactory _problemDetailsFactory;
    
    public ProblemDetailsClientErrorFactory(ProblemDetailsFactory problemDetailsFactory)
    {
        _problemDetailsFactory = 
            problemDetailsFactory 
            	?? throw new ArgumentNullException(nameof(problemDetailsFactory));
    }
    
    public IActionResult GetClientError(
        ActionContext actionContext, 
        IClientErrorActionResult clientError)
    {
        var problemDetails = _problemDetailsFactory.CreateProblemDetails(
            											actionContext.HttpContext, 
            											clientError.StatusCode);
        
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status,
            ContentTypes =
            {
                "application/problem+json",
                "application/problem+xml",
            },
        };
    }
}

```



#### 2.6 client error action result 扩展

##### 2.6.1 client error action result 接口

```c#
public interface IClientErrorActionResult : IStatusCodeActionResult
{
}

```

##### 2.6.2 status code result（基类）

###### 2.6.2.1 status code result

```c#
public class StatusCodeResult : 
	ActionResult, 
	IClientErrorActionResult
{
    public int StatusCode { get; }    
    int? IStatusCodeActionResult.StatusCode => StatusCode;
    
    public StatusCodeResult([ActionResultStatusCode] int statusCode)
    {
        StatusCode = statusCode;
    }
                
    /// <inheritdoc />
    public override void ExecuteResult(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        /* 解析 logger 并创建日志 */
        var factory = context
            .HttpContext
            .RequestServices
            .GetRequiredService<ILoggerFactory>();
        
        var logger = factory.CreateLogger<StatusCodeResult>();        
        logger.HttpStatusCodeResultExecuting(StatusCode);
        
        /* 设置 status code */
        context.HttpContext.Response.StatusCode = StatusCode;
    }
}

```

###### 2.6.2.2 action result status code attribute

```c#
// Attribute annotated on ActionResult constructor and 
// helper method parameters to indicate that the parameter 
// is used to set the "statusCode" for the ActionResult.    
[AttributeUsage(
    AttributeTargets.Parameter, 
    AllowMultiple = false, 
    Inherited = false)]
public sealed class ActionResultStatusCodeAttribute : Attribute
{
}

```

##### 2.6.3 status code result 扩展

###### 2.6.3.1 -200 ok result

```c#
[DefaultStatusCode(DefaultStatusCode)]
public class OkResult : StatusCodeResult
{
    private const int DefaultStatusCode = StatusCodes.Status200OK;
        
    public OkResult() : base(DefaultStatusCode)
    {
    }
}

```

###### 2.6.3.2 -204 no content result

```c#
[DefaultStatusCode(DefaultStatusCode)]
public class NoContentResult : StatusCodeResult
{
    private const int DefaultStatusCode = StatusCodes.Status204NoContent;
        
    public NoContentResult() : base(DefaultStatusCode)
    {
    }
}

```

###### 2.6.3.3 -400 bad request result

```c#
[DefaultStatusCode(DefaultStatusCode)]
public class BadRequestResult : StatusCodeResult
{
    private const int DefaultStatusCode = StatusCodes.Status400BadRequest;
        
    public BadRequestResult() : base(DefaultStatusCode)
    {
    }
}

```

###### 2.6.3.4 antiforgery validation failed result

```c#
public interface IAntiforgeryValidationFailedResult : IActionResult
{
}

public class AntiforgeryValidationFailedResult : 
	BadRequestResult, 
	IAntiforgeryValidationFailedResult
{
}

```

###### 2.6.3.5 -401 unauthorized result

```c#
[DefaultStatusCode(DefaultStatusCode)]
public class UnauthorizedResult : StatusCodeResult
{
    private const int DefaultStatusCode = StatusCodes.Status401Unauthorized;
        
    public UnauthorizedResult() : base(DefaultStatusCode)
    {
    }
}

```

###### 2.6.3.6 -404 not found result

```c#
[DefaultStatusCode(DefaultStatusCode)]
public class NotFoundResult : StatusCodeResult
{
    private const int DefaultStatusCode = StatusCodes.Status404NotFound;
        
    public NotFoundResult() : base(DefaultStatusCode)
    {
    }
}

```

###### 2.6.3.7 -409 conflict result

```c#
[DefaultStatusCode(DefaultStatusCode)]
public class ConflictResult : StatusCodeResult
{
    private const int DefaultStatusCode = StatusCodes.Status409Conflict;
        
    public ConflictResult() : base(DefaultStatusCode)
    {
    }
}

```

###### 2.6.3.8 -415 unsupported media type result

```c#
[DefaultStatusCode(DefaultStatusCode)]
public class UnsupportedMediaTypeResult : StatusCodeResult
{
    private const int DefaultStatusCode = StatusCodes.Status415UnsupportedMediaType;
        
    public UnsupportedMediaTypeResult() : base(DefaultStatusCode)
    {
    }
}

```

###### 2.6.3.9 -422 unprocessable entity result

```c#
[DefaultStatusCode(DefaultStatusCode)]
public class UnprocessableEntityResult : StatusCodeResult
{
    private const int DefaultStatusCode = StatusCodes.Status422UnprocessableEntity;
        
    public UnprocessableEntityResult() : base(DefaultStatusCode)
    {
    }
}

```

#### 2.7 authenticate 相关 result

##### 2.7.1 challenge result

```c#
public class ChallengeResult : ActionResult
{
    public IList<string> AuthenticationSchemes { get; set; }        
    public AuthenticationProperties Properties { get; set; }
    
    public ChallengeResult() : this(Array.Empty<string>())        
    {
    }
        
    public ChallengeResult(string authenticationScheme)            
        : this(new[] { authenticationScheme })
    {        
    }
        
    public ChallengeResult(IList<string> authenticationSchemes)            
        : this(authenticationSchemes, properties: null)
    {
    }
        
    public ChallengeResult(AuthenticationProperties properties)            
        : this(Array.Empty<string>(), properties)
    {
    }
        
    public ChallengeResult(
        string authenticationScheme, 
        AuthenticationProperties properties)            
        	: this(new[] { authenticationScheme }, properties)
    {
    }
        
    public ChallengeResult(
        IList<string> authenticationSchemes, 
        AuthenticationProperties properties)
    {
        AuthenticationSchemes = authenticationSchemes;
        Properties = properties;
    }
                
    /// <inheritdoc />
    public override async Task ExecuteResultAsync(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        /* 解析 logger 并创建日志 */
        var loggerFactory = context
            .HttpContext
            .RequestServices
            .GetRequiredService<ILoggerFactory>();
        
        var logger = loggerFactory.CreateLogger<ChallengeResult>();        
        logger.ChallengeResultExecuting(AuthenticationSchemes);
        
        if (AuthenticationSchemes != null && 
            AuthenticationSchemes.Count > 0)
        {
            foreach (var scheme in AuthenticationSchemes)
            {
                await context.HttpContext.ChallengeAsync(scheme, Properties);
            }
        }
        else
        {
            await context.HttpContext.ChallengeAsync(Properties);
        }
    }
}

```

##### 2.7.2 forbid result

```c#
public class ForbidResult : ActionResult
{
    public IList<string> AuthenticationSchemes { get; set; }        
    public AuthenticationProperties Properties { get; set; }
       
    public ForbidResult() : this(Array.Empty<string>())
    {
    }
       
    public ForbidResult(string authenticationScheme)      
        : this(new[] { authenticationScheme })
    {
    }
            
    public ForbidResult(IList<string> authenticationSchemes)            
        : this(authenticationSchemes, properties: null)
    {
    }
            
    public ForbidResult(AuthenticationProperties properties)            
        : this(Array.Empty<string>(), properties)
    {
    }
            
    public ForbidResult(
        string authenticationScheme, 
        AuthenticationProperties properties)            
        	: this(new[] { authenticationScheme }, properties)
    {
    }
            
    public ForbidResult(
        IList<string> authenticationSchemes, 
        AuthenticationProperties properties)
    {
        AuthenticationSchemes = authenticationSchemes;
        Properties = properties;
    }
                   
    /// <inheritdoc />
    public override async Task ExecuteResultAsync(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        /* 解析 logger 并创建日志 */
        var loggerFactory = context
            .HttpContext
            .RequestServices
            .GetRequiredService<ILoggerFactory>();
        
        var logger = loggerFactory.CreateLogger<ForbidResult>();        
        logger.ForbidResultExecuting(AuthenticationSchemes);
        
        if (AuthenticationSchemes != null && 
            AuthenticationSchemes.Count > 0)
        {
            for (var i = 0; i < AuthenticationSchemes.Count; i++)
            {
                await context
                    	  .HttpContext
                    	  .ForbidAsync(AuthenticationSchemes[i], Properties);
            }
        }
        else
        {
            await context.HttpContext.ForbidAsync(Properties);
        }
    }
}

```

##### 2.7.3 sign in result

```c#
public class SignInResult : ActionResult
{
    public string AuthenticationScheme { get; set; }        
    public ClaimsPrincipal Principal { get; set; }        
    public AuthenticationProperties Properties { get; set; }
    
    public SignInResult(ClaimsPrincipal principal)        
        : this(authenticationScheme: null, principal, properties: null)
    {
    }
        
    public SignInResult(
        string authenticationScheme, 
        ClaimsPrincipal principal)            
        	: this(authenticationScheme, principal, properties: null)
    {
    }
        
    public SignInResult(
        ClaimsPrincipal principal, 
        AuthenticationProperties properties)            
        	: this(authenticationScheme: null, principal, properties)
    {
    }
        
    public SignInResult(
        string authenticationScheme, 
        ClaimsPrincipal principal, 
        AuthenticationProperties properties)
    {
        Principal = principal ?? throw new ArgumentNullException(nameof(principal));
        AuthenticationScheme = authenticationScheme;        
        Properties = properties;
    }
                    
    /// <inheritdoc />
    public override async Task ExecuteResultAsync(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        /* 解析 logger 并创建日志 */
        var loggerFactory = context
            .HttpContext
            .RequestServices
            .GetRequiredService<ILoggerFactory>();
        
        var logger = loggerFactory.CreateLogger<SignInResult>();        
        logger.SignInResultExecuting(AuthenticationScheme, Principal);
        
        await context.HttpContext.SignInAsync(
            AuthenticationScheme, 
            Principal, 
            Properties);
    }
}

```

##### 2.7.4 sign out result

```c#
public class SignOutResult : ActionResult
{
    public IList<string> AuthenticationSchemes { get; set; }        
    public AuthenticationProperties Properties { get; set; }
    
    public SignOutResult() : this(Array.Empty<string>())
    {
    }
        
    public SignOutResult(AuthenticationProperties properties)            
        : this(Array.Empty<string>(), properties)
    {
    }
        
    public SignOutResult(string authenticationScheme)        
        : this(new[] { authenticationScheme })
    {
    }
        
    public SignOutResult(IList<string> authenticationSchemes)            
        : this(authenticationSchemes, properties: null)
    {
    }
        
    public SignOutResult(
        string authenticationScheme, 
        AuthenticationProperties properties)            
        	: this(new[] { authenticationScheme }, properties)
    {
    }
        
    public SignOutResult(
        IList<string> authenticationSchemes, 
        AuthenticationProperties properties)
    {
        AuthenticationSchemes = authenticationSchemes 
            ?? throw new ArgumentNullException(nameof(authenticationSchemes));
        Properties = properties;
    }
               
    /// <inheritdoc />
    public override async Task ExecuteResultAsync(ActionContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        
        if (AuthenticationSchemes == null)
        {
            throw new InvalidOperationException(
                Resources.FormatPropertyOfTypeCannotBeNull(
                    /* property: */ 
                    nameof(AuthenticationSchemes),
                    /* type: */ 
                    nameof(SignOutResult)));
        }
        
        var loggerFactory = context
            .HttpContext
            .RequestServices
            .GetRequiredService<ILoggerFactory>();
        
        var logger = loggerFactory.CreateLogger<SignOutResult>();        
        logger.SignOutResultExecuting(AuthenticationSchemes);
        
        if (AuthenticationSchemes.Count == 0)            
        {
            await context.HttpContext.SignOutAsync(Properties);
        }
        else
        {
            for (var i = 0; i < AuthenticationSchemes.Count; i++)
            {
                await context.HttpContext.SignOutAsync(
                    AuthenticationSchemes[i], 
                    Properties);
            }
        }
    }
}

```

##### authentication schemes???

#### 2.8 action result type mapper

##### 2.8.1 接口

```c#
public interface IActionResultTypeMapper
{    
    Type GetResultDataType(Type returnType);        
    IActionResult Convert(object? value, Type returnType);
}

```

##### 2.8.2 实现

```c#
internal class ActionResultTypeMapper : IActionResultTypeMapper
{
    // 获取 data type，即 T 的类型，不适用于 void
    public Type GetResultDataType(Type returnType)
    {
        if (returnType == null)
        {
            throw new ArgumentNullException(nameof(returnType));
        }
        
        if (returnType.IsGenericType &&
            returnType.GetGenericTypeDefinition() == typeof(ActionResult<>))
        {
            return returnType.GetGenericArguments()[0];
        }
        
        return returnType;
    }
    
    // 转换，不适用于 void
    public IActionResult Convert(object? value, Type returnType)
    {
        if (returnType == null)
        {
            throw new ArgumentNullException(nameof(returnType));
        }
        
        // 如果实现了 convert to action result（action result T 类型），
        if (value is IConvertToActionResult converter)
        {
            // 使用 convert to action result 的 convert 方法
            return converter.Convert();
        }
        // 否则封装 value 到 object result
        return new ObjectResult(value)
        {
            DeclaredType = returnType,
        };
    }
}

```

### 3. problem details

#### 3.1 problem details

##### 3.1.1 problem details

```c#

```

```c#

```



##### 3.1.2 

##### 3.1.2 validation problem details

```c#
[JsonConverter(typeof(ValidationProblemDetailsJsonConverter))]
    public class ValidationProblemDetails : ProblemDetails
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ValidationProblemDetails"/>.
        /// </summary>
        public ValidationProblemDetails()
        {
            Title = Resources.ValidationProblemDescription_Title;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ValidationProblemDetails"/> using the specified <paramref name="modelState"/>.
        /// </summary>
        /// <param name="modelState"><see cref="ModelStateDictionary"/> containing the validation errors.</param>
        public ValidationProblemDetails(ModelStateDictionary modelState)
            : this()
        {
            if (modelState == null)
            {
                throw new ArgumentNullException(nameof(modelState));
            }

            foreach (var keyModelStatePair in modelState)
            {
                var key = keyModelStatePair.Key;
                var errors = keyModelStatePair.Value.Errors;
                if (errors != null && errors.Count > 0)
                {
                    if (errors.Count == 1)
                    {
                        var errorMessage = GetErrorMessage(errors[0]);
                        Errors.Add(key, new[] { errorMessage });
                    }
                    else
                    {
                        var errorMessages = new string[errors.Count];
                        for (var i = 0; i < errors.Count; i++)
                        {
                            errorMessages[i] = GetErrorMessage(errors[i]);
                        }

                        Errors.Add(key, errorMessages);
                    }
                }
            }

            string GetErrorMessage(ModelError error)
            {
                return string.IsNullOrEmpty(error.ErrorMessage) ?
                    Resources.SerializableError_DefaultError :
                    error.ErrorMessage;
            }
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ValidationProblemDetails"/> using the specified <paramref name="errors"/>.
        /// </summary>
        /// <param name="errors">The validation errors.</param>
        public ValidationProblemDetails(IDictionary<string, string[]> errors)
            : this()
        {
            if (errors == null)
            {
                throw new ArgumentNullException(nameof(errors));
            }

            Errors = new Dictionary<string, string[]>(errors, StringComparer.Ordinal);
        }

        /// <summary>
        /// Gets the validation errors associated with this instance of <see cref="ValidationProblemDetails"/>.
        /// </summary>
        [JsonPropertyName("errors")]
        public IDictionary<string, string[]> Errors { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal);
    }
```



