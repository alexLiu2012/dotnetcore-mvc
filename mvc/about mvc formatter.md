## about mvc formatter

### 1. about



### 2. details



```c#

```

###### 

```c#

```

##### 



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









