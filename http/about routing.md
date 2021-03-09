## about routing



### 1. about



### 2. details











#### 2.4 matcher 组件

##### 2.4.1 candidate

* 封装 endpoint、route value 和 score

```c#
internal readonly struct Candidate
{
    public readonly Endpoint Endpoint;
    
    // Used to optimize out operations that modify route values.
    public readonly CandidateFlags Flags;
    
    // Data for creating the RouteValueDictionary. We assign each key its own slot
    // and we fill the values array with all of the default values.
    //
    // Then when we process parameters, we don't need to operate on the RouteValueDictionary
    // we can just operate on an array, which is much much faster.
    public readonly KeyValuePair<string, object>[] Slots;
    
    // List of parameters to capture. Segment is the segment index, index is the 
    // index into the values array.
    public readonly (string parameterName, int segmentIndex, int slotIndex)[] Captures;
    
    // Catchall parameter to capture (limit one per template).
    public readonly (string parameterName, int segmentIndex, int slotIndex) CatchAll;
    
    // Complex segments are processed in a separate pass because they require a
    // RouteValueDictionary.
    public readonly (RoutePatternPathSegment pathSegment, int segmentIndex)[] ComplexSegments;
    
    public readonly KeyValuePair<string, IRouteConstraint>[] Constraints;
    
    // Score is a sequential integer value that in determines the priority of an Endpoint.
    // Scores are computed within the context of candidate set, and are meaningless when
    // applied to endpoints not in the set.
    //
    // The score concept boils down the system of comparisons done when ordering Endpoints
    // to a single value that can be compared easily. This can be defeated by having 
    // int32.MaxValue + 1 endpoints in a single set, but you would have other problems by 
    // that point.
    //
    // Score is not part of the Endpoint itself, because it's contextual based on where
    // the endpoint appears. An Endpoint is often be a member of multiple candiate sets.
    public readonly int Score;
    
    // 构造 empty candidate
    // Used in tests.
    public Candidate(Endpoint endpoint)
    {
        Endpoint = endpoint;        
        Slots = Array.Empty<KeyValuePair<string, object>>();
        Captures = Array.Empty<(string parameterName, int segmentIndex, int slotIndex)>();
        CatchAll = default;
        ComplexSegments = Array.Empty<(
            RoutePatternPathSegment pathSegment, int segmentIndex)>();
        Constraints = Array.Empty<KeyValuePair<string, IRouteConstraint>>();
        Score = 0;        
        Flags = CandidateFlags.None;
    }
    
    public Candidate(
        Endpoint endpoint,
        int score,
        KeyValuePair<string, object>[] slots,
        (string parameterName, int segmentIndex, int slotIndex)[] captures,
        in (string parameterName, int segmentIndex, int slotIndex) catchAll,
        (RoutePatternPathSegment pathSegment, int segmentIndex)[] complexSegments,
        KeyValuePair<string, IRouteConstraint>[] constraints)
    {
        Endpoint = endpoint;
        Score = score;
        Slots = slots;
        Captures = captures;
        CatchAll = catchAll;
        ComplexSegments = complexSegments;
        Constraints = constraints;
        
        Flags = CandidateFlags.None;
        
        // 设置 flags
        for (var i = 0; i < slots.Length; i++)
        {
            if (slots[i].Key != null)
            {
                Flags |= CandidateFlags.HasDefaults;
            }
        }        
        if (captures.Length > 0)
        {
            Flags |= CandidateFlags.HasCaptures;
        }        
        if (catchAll.parameterName != null)
        {
            Flags |= CandidateFlags.HasCatchAll;
        }        
        if (complexSegments.Length > 0)
        {
            Flags |= CandidateFlags.HasComplexSegments;
        }        
        if (constraints.Length > 0)
        {
            Flags |= CandidateFlags.HasConstraints;
        }
    }        
}

```

###### 2.4.1.1 candidate flag

```c#
internal readonly struct Candidate
{
    [Flags]
    public enum CandidateFlags
    {
        None = 0,
        HasDefaults = 1,
        HasCaptures = 2,
        HasCatchAll = 4,
        HasSlots = HasDefaults | HasCaptures | HasCatchAll,
        HasComplexSegments = 8,
        HasConstraints = 16,
    }
}

```





##### 2.4.2 endpoint selector

* 从 candidate 中选择适合的 endpoint，
* 并注入 http context

###### 2.4.2.1 抽象基类

```c#
public abstract class EndpointSelector
{    
    public abstract Task SelectAsync(
        HttpContext httpContext, 
        CandidateSet candidates);
}

```

###### 2.4.2.2 默认实现

```c#
internal sealed class DefaultEndpointSelector : EndpointSelector
{
    public override Task SelectAsync(
        HttpContext httpContext,
        CandidateSet candidateSet)
    {
        if (httpContext == null)
        {
            throw new ArgumentNullException(nameof(httpContext));
        }        
        if (candidateSet == null)
        {
            throw new ArgumentNullException(nameof(candidateSet));
        }
        
        Select(httpContext, candidateSet.Candidates);
        return Task.CompletedTask;
    }
    
    internal static void Select(
        HttpContext httpContext, 
        CandidateState[] candidateState)
    {
        // Fast path: We can specialize for trivial numbers of candidates 
        // since there can be no ambiguities
        switch (candidateState.Length)
        {
            case 0:
                {
                    /* 没有 candidate */
                    // Do nothing
                    break;
                }                
            case 1:
                {
                    /* 只有1个 candidate，*/
                    // 验证 validate，如果 valid，注入 http context
                    ref var state = ref candidateState[0];
                    if (CandidateSet.IsValidCandidate(ref state))
                    {
                        httpContext.SetEndpoint(state.Endpoint);
                        httpContext.Request.RouteValues = state.Values!;
                    }
                    
                    break;
                }                
            default:
                {
                    /* 多个 candidate */
                    // Slow path: 
                    //   There's more than one candidate (to say nothing of validity) 
                    //   so we have to process for ambiguities.
                    ProcessFinalCandidates(httpContext, candidateState);
                    break;
                }
        }
    }       
}

```

###### 2.4.2.3 process candidates

* 从 candidates 中查找适合的 endpoint，并注入 http context

```c#
internal sealed class DefaultEndpointSelector : EndpointSelector
{
     private static void ProcessFinalCandidates(
        HttpContext httpContext,
        CandidateState[] candidateState)
    {
        // 初始化，置 null
        Endpoint? endpoint = null;
        RouteValueDictionary? values = null;
        int? foundScore = null;
        
        // 遍历 candidate state 集合，
        // 按 score 查找 endpoint（score 较小的）       
        for (var i = 0; i < candidateState.Length; i++)
        {
            ref var state = ref candidateState[i];
            
            if (!CandidateSet.IsValidCandidate(ref state))
            {
                continue;
            }
            
            if (foundScore == null)
            {
                // This is the first match we've seen - speculatively assign it.
                endpoint = state.Endpoint;
                values = state.Values;
                foundScore = state.Score;
            }
            else if (foundScore < state.Score)
            {
                // This candidate is lower priority than the one we've seen
                // so far, we can stop.
                //
                // Don't worry about the 'null < state.Score' case, it returns false.
                break;
            }
            else if (foundScore == state.Score)
            {
                // This is the second match we've found of the same score, so there
                // must be an ambiguity.
                //
                // Don't worry about the 'null == state.Score' case, it returns false.
                
                ReportAmbiguity(candidateState);
                
                // Unreachable, ReportAmbiguity always throws.
                throw new NotSupportedException();
            }
        }
        
        // 向 http context 注入 endpoint 和 route value dictionary
        if (endpoint != null)
        {
            httpContext.SetEndpoint(endpoint);
            httpContext.Request.RouteValues = values!;
        }
    }
    
    private static void ReportAmbiguity(CandidateState[] candidateState)
    {
        // If we get here it's the result of an ambiguity - we're OK with this
        // being a littler slower and more allocatey.
        var matches = new List<Endpoint>();
        for (var i = 0; i < candidateState.Length; i++)
        {
            ref var state = ref candidateState[i];
            if (CandidateSet.IsValidCandidate(ref state))
            {
                matches.Add(state.Endpoint);
            }
        }
        
        var message = Resources.FormatAmbiguousEndpoints(
            Environment.NewLine,
            string.Join(
                Environment.NewLine, 
                matches.Select(e => e.DisplayName)));
        
        throw new AmbiguousMatchException(message);
    }
}

```









##### 2.4.4 host matcher policy

```c#

```

###### 2.4.4.1 实现 node builder policy

```c#

```

* get edge

```c#
public sealed class HostMatcherPolicy 
{
    public IReadOnlyList<PolicyNodeEdge> GetEdges(IReadOnlyList<Endpoint> endpoints)
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }
        
        // The algorithm here is designed to be preserve the order of the endpoints
        // while also being relatively simple. Preserving order is important.
        
        // First, build a dictionary of all of the hosts that are included
        // at this node.
        //
        // For now we're just building up the set of keys. We don't add any endpoints
        // to lists now because we don't want ordering problems.
        var edges = new Dictionary<EdgeKey, List<Endpoint>>();
        for (var i = 0; i < endpoints.Count; i++)
        {
            var endpoint = endpoints[i];
            var hosts = endpoint
                .Metadata
                .GetMetadata<IHostMetadata>()?
                .Hosts
                .Select(h => 
                 	CreateEdgeKey(h)).ToArray();
            
            if (hosts == null || hosts.Length == 0)
            {
                hosts = new[] 
                { 
                    EdgeKey.WildcardEdgeKey 
                };
            }
            
            for (var j = 0; j < hosts.Length; j++)
            {
                var host = hosts[j];
                if (!edges.ContainsKey(host))
                {
                    edges.Add(host, new List<Endpoint>());
                }
            }
        }
        
        // Now in a second loop, add endpoints to these lists. We've enumerated all of
        // the states, so we want to see which states this endpoint matches.
        for (var i = 0; i < endpoints.Count; i++)
        {
            var endpoint = endpoints[i];
            
            var endpointKeys = endpoint
                .Metadata
                .GetMetadata<IHostMetadata>()
                ?.Hosts
                .Select(h => 
                	CreateEdgeKey(h)).ToArray() 
                		?? Array.Empty<EdgeKey>();
            
            if (endpointKeys.Length == 0)
            {
                // OK this means that this endpoint matches *all* hosts.
                // So, loop and add it to all states.
                foreach (var kvp in edges)
                {
                    kvp.Value.Add(endpoint);
                }
            }
            else
            {
                // OK this endpoint matches specific hosts
                foreach (var kvp in edges)
                {
                    // The edgeKey maps to a possible request header value
                    var edgeKey = kvp.Key;
                    
                    for (var j = 0; j < endpointKeys.Length; j++)
                    {
                        var endpointKey = endpointKeys[j];
                        
                        if (edgeKey.Equals(endpointKey))
                        {
                            kvp.Value.Add(endpoint);
                            break;
                        }
                        else if (edgeKey.HasHostWildcard && 
                                 endpointKey.HasHostWildcard &&                                 
                                 edgeKey.Port == endpointKey.Port && 
                                 edgeKey.MatchHost(endpointKey.Host))
                        {
                            kvp.Value.Add(endpoint);
                            break;
                        }
                    }
                }
            }
        }
        
        return edges
            .Select(kvp => new PolicyNodeEdge(kvp.Key, kvp.Value))
            .ToArray();
    }
    
    private static EdgeKey CreateEdgeKey(string host)
    {
        if (host == null)
        {
            return EdgeKey.WildcardEdgeKey;
        }
        
        var hostParts = host.Split(':');
        if (hostParts.Length == 1)
        {
            if (!string.IsNullOrEmpty(hostParts[0]))
            {
                return new EdgeKey(hostParts[0], null);
            }
        }
        if (hostParts.Length == 2)
        {
            if (!string.IsNullOrEmpty(hostParts[0]))
            {
                if (int.TryParse(hostParts[1], out var port))
                {
                    return new EdgeKey(hostParts[0], port);
                }
                else if (string.Equals(
                    hostParts[1], 
                    WildcardHost, 
                    StringComparison.Ordinal))
                {
                    return new EdgeKey(hostParts[0], null);
                }
            }
        }
        
        throw new InvalidOperationException($"Could not parse host: {host}");
    }
    
    private readonly struct EdgeKey 
        : IEquatable<EdgeKey>, 
    	IComparable<EdgeKey>, 
    	IComparable
    {
        internal static readonly EdgeKey WildcardEdgeKey = new EdgeKey(null, null);
        
        public readonly int? Port;
        public readonly string Host;        
        private readonly string? _wildcardEndsWith;
            
        public bool HasHostWildcard { get; }        
        public bool MatchesHost => 
            !string.Equals(Host, WildcardHost, StringComparison.Ordinal);        
        public bool MatchesPort => Port != null;        
        public bool MatchesAll => !MatchesHost && !MatchesPort;
        
        public EdgeKey(string? host, int? port)
        {
            Host = host ?? WildcardHost;
            Port = port;
            
            HasHostWildcard = Host.StartsWith(
                WildcardPrefix, 
                StringComparison.Ordinal);
            
            _wildcardEndsWith = HasHostWildcard 
                ? Host.Substring(1) 
                : null;
        }
                        
        public int CompareTo(EdgeKey other)
        {
            var result = Comparer<string>.Default.Compare(Host, other.Host);
            if (result != 0)
            {
                return result;
            }
            
            return Comparer<int?>.Default.Compare(Port, other.Port);
        }
        
        public int CompareTo(object? obj)
        {
            return CompareTo((EdgeKey)obj!);
        }
        
        public bool Equals(EdgeKey other)
        {
            return string.Equals(
                	Host, 
                	other.Host, 
                	StringComparison.Ordinal) && 
                Port == other.Port;
        }
        
        public bool MatchHost(string host)
        {
            if (MatchesHost)
            {
                if (HasHostWildcard)
                {
                    return host.EndsWith(
                        _wildcardEndsWith!, 
                        StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    return string.Equals(
                        host, 
                        Host, 
                        StringComparison.OrdinalIgnoreCase);
                }
            }
            
            return true;
        }
                        
        public override int GetHashCode()
        {
            return (Host?.GetHashCode() ?? 0) ^ (Port?.GetHashCode() ?? 0);
        }
        
        public override bool Equals(object? obj)
        {
            if (obj is EdgeKey key)
            {
                return Equals(key);
            }
            
            return false;
        }
        
        public override string ToString()
        {
            return $"{Host}:{Port?.ToString(CultureInfo.InvariantCulture) ?? WildcardHost}";
        }
    }              
}

```

* build jump table

```c#
public sealed class HostMatcherPolicy 
{
    public PolicyJumpTable BuildJumpTable(
        int exitDestination, 
        IReadOnlyList<PolicyJumpTableEdge> edges)
    {
        if (edges == null)
        {
            throw new ArgumentNullException(nameof(edges));
        }
        
        // Since our 'edges' can have wildcards, 
        // we do a sort based on how wildcard-ey they
        // are then then execute them in linear order.
        var ordered = edges
            .Select(e => 
            	(host: (EdgeKey)e.State, 
                 destination: e.Destination))
            .OrderBy(e => 
                GetScore(e.host))
            .ToArray();
        
        return new HostPolicyJumpTable(exitDestination, ordered);
    }
    
    private int GetScore(in EdgeKey key)
    {
        // Higher score == lower priority.
        if (key.MatchesHost && !key.HasHostWildcard && key.MatchesPort)
        {
            return 1; // Has host AND port, e.g. www.consoto.com:8080
        }
        else if (key.MatchesHost && !key.HasHostWildcard)
        {
            return 2; // Has host, e.g. www.consoto.com
        }
        else if (key.MatchesHost && key.MatchesPort)
        {
            return 3; // Has wildcard host AND port, e.g. *.consoto.com:8080
        }
        else if (key.MatchesHost)
        {
            return 4; // Has wildcard host, e.g. *.consoto.com
        }
        else if (key.MatchesPort)
        {
            return 5; // Has port, e.g. *:8080
        }
        else
        {
            return 6; // Has neither, e.g. *:* (or no metadata)
        }
    }   
    
    private class HostPolicyJumpTable : PolicyJumpTable
    {
        private int _exitDestination;
        private (EdgeKey host, int destination)[] _destinations;
                
        public HostPolicyJumpTable(
            int exitDestination, 
            (EdgeKey host, int destination)[] destinations)
        {
            _exitDestination = exitDestination;
            _destinations = destinations;
        }
        
        public override int GetDestination(HttpContext httpContext)
        {
            // HostString can allocate when accessing the host or port
            // Store host and port locally and reuse
            var (host, port) = GetHostAndPort(httpContext);
            
            var destinations = _destinations;
            for (var i = 0; i < destinations.Length; i++)
            {
                var destination = destinations[i];
                
                if ((!destination.host.MatchesPort || 
                     destination.host.Port == port) &&
                    destination.host.MatchHost(host))
                {
                    return destination.destination;
                }
            }
            
            return _exitDestination;
        }
    }
}

```



##### 2.4.5 host method matcher policy



###### 2.4.5.1 实现 node builder policy



* get edge

```c#
public sealed class HttpMethodMatcherPolicy
{     
    public IReadOnlyList<PolicyNodeEdge> GetEdges(IReadOnlyList<Endpoint> endpoints)
    {
        // The algorithm here is designed to be preserve the order of the endpoints
        // while also being relatively simple. Preserving order is important.
        
        // First, build a dictionary of all possible HTTP method/CORS combinations
        // that exist in this list of endpoints.
        //
        // For now we're just building up the set of keys. We don't add any endpoints
        // to lists now because we don't want ordering problems.
        var allHttpMethods = new List<string>();
        var edges = new Dictionary<EdgeKey, List<Endpoint>>();
        for (var i = 0; i < endpoints.Count; i++)
        {
            var endpoint = endpoints[i];
            var (httpMethods, acceptCorsPreFlight) = GetHttpMethods(endpoint);
            
            // If the action doesn't list HTTP methods then it supports all methods.
            // In this phase we use a sentinel value to represent the *other* HTTP method
            // a state that represents any HTTP method that doesn't have a match.
            if (httpMethods.Count == 0)
            {
                httpMethods = new[] { AnyMethod, };
            }
            
            for (var j = 0; j < httpMethods.Count; j++)
            {
                // An endpoint that allows CORS reqests will match both CORS and non-CORS
                // so we model it as both.
                var httpMethod = httpMethods[j];
                var key = new EdgeKey(httpMethod, acceptCorsPreFlight);
                if (!edges.ContainsKey(key))
                {
                    edges.Add(key, new List<Endpoint>());
                }

                    // An endpoint that allows CORS reqests will match both CORS and non-CORS
                    // so we model it as both.
                    if (acceptCorsPreFlight)
                    {
                        key = new EdgeKey(httpMethod, false);
                        if (!edges.ContainsKey(key))
                        {
                            edges.Add(key, new List<Endpoint>());
                        }
                    }
                
                	// Also if it's not the *any* method key, then track it.
                    if (!string.Equals(
                        	AnyMethod, 
                        	httpMethod, 
                        	StringComparison.OrdinalIgnoreCase))
                    {
                        if (!ContainsHttpMethod(allHttpMethods, httpMethod))
                        {
                            allHttpMethods.Add(httpMethod);
                        }
                    }
                }
            }

            allHttpMethods.Sort(StringComparer.OrdinalIgnoreCase);

            // Now in a second loop, add endpoints to these lists. 
        	// We've enumerated all of the states, 
        	// so we want to see which states this endpoint matches.
            for (var i = 0; i < endpoints.Count; i++)
            {
                var endpoint = endpoints[i];
                var (httpMethods, acceptCorsPreFlight) = GetHttpMethods(endpoint);

                if (httpMethods.Count == 0)
                {
                    // OK this means that this endpoint matches *all* HTTP methods.
                    // So, loop and add it to all states.
                    foreach (var kvp in edges)
                    {
                        if (acceptCorsPreFlight || !kvp.Key.IsCorsPreflightRequest)
                        {
                            kvp.Value.Add(endpoint);
                        }
                    }
                }
                else
                {
                    // OK this endpoint matches specific methods.
                    for (var j = 0; j < httpMethods.Count; j++)
                    {
                        var httpMethod = httpMethods[j];
                        var key = new EdgeKey(httpMethod, acceptCorsPreFlight);

                        edges[key].Add(endpoint);

                        // An endpoint that allows CORS reqests will match 
                        // both CORS and non-CORS so we model it as both.
                        if (acceptCorsPreFlight)
                        {
                            key = new EdgeKey(httpMethod, false);
                            edges[key].Add(endpoint);
                        }
                    }
                }
            }

            // Adds a very low priority endpoint that will reject the request with
            // a 405 if nothing else can handle this verb. This is only done if
            // no other actions exist that handle the 'all verbs'.
            //
            // The rationale for this is that we want to report a 405 if none of
            // the supported methods match, but we don't want to report a 405 in a
            // case where an application defines an endpoint that handles all verbs, but
            // a constraint rejects the request, or a complex segment fails to parse. We
            // consider a case like that a 'user input validation' failure  rather than
            // a semantic violation of HTTP.
            //
            // This will make 405 much more likely in API-focused applications, and somewhat
            // unlikely in a traditional MVC application. That's good.
            //
            // We don't bother returning a 405 when the CORS preflight method doesn't exist.
            // The developer calling the API will see it as a CORS error, which is fine because
            // there isn't an endpoint to check for a CORS policy.
            if (!edges.TryGetValue(new EdgeKey(AnyMethod, false), out var matches))
            {
                // Methods sorted for testability.
                var endpoint = CreateRejectionEndpoint(allHttpMethods);
                matches = new List<Endpoint>() { endpoint, };
                edges[new EdgeKey(AnyMethod, false)] = matches;
            }

            var policyNodeEdges = new PolicyNodeEdge[edges.Count];
            var index = 0;
            foreach (var kvp in edges)
            {
                policyNodeEdges[index++] = new PolicyNodeEdge(kvp.Key, kvp.Value);
            }

            return policyNodeEdges;

            (IReadOnlyList<string> httpMethods, bool acceptCorsPreflight) 
            	GetHttpMethods(Endpoint e)
            {
                var metadata = e.Metadata.GetMetadata<IHttpMethodMetadata>();
                return metadata == null 
                    ? (Array.Empty<string>(), false) 
                    : (metadata.HttpMethods, metadata.AcceptCorsPreflight);
            }
    }
    
    private static bool ContainsHttpMethod(List<string> httpMethods, string httpMethod)
    {
        var methods = CollectionsMarshal.AsSpan(httpMethods);
        for (var i = 0; i < methods.Length; i++)
        {
            // This is a fast path for when everything is using static HttpMethods instances.
            if (object.ReferenceEquals(methods[i], httpMethod))
            {
                return true;
            }
        }
        
        for (var i = 0; i < methods.Length; i++)
        {
            if (HttpMethods.Equals(methods[i], httpMethod))
            {
                return true;
            }
        }
        
        return false;
    }
    
    internal readonly struct EdgeKey : IEquatable<EdgeKey>, IComparable<EdgeKey>, IComparable
    {
        // Note that in contrast with the metadata, 
        // the edge represents a possible state change rather than a list of what's allowed. 
        // We represent CORS and non-CORS requests as separate states.
        public readonly bool IsCorsPreflightRequest;
        public readonly string HttpMethod;
        
        public EdgeKey(string httpMethod, bool isCorsPreflightRequest)
        {
            HttpMethod = httpMethod;
            IsCorsPreflightRequest = isCorsPreflightRequest;
        }
        
        // These are comparable so they can be sorted in tests.
        public int CompareTo(EdgeKey other)
        {
            var compare = string.Compare(
                HttpMethod, 
                other.HttpMethod, 
                StringComparison.Ordinal);
            if (compare != 0)
            {
                return compare;
            }
            
            return IsCorsPreflightRequest.CompareTo(other.IsCorsPreflightRequest);
        }
        
        public int CompareTo(object? obj)
        {
            return CompareTo((EdgeKey)obj!);
        }
        
        public bool Equals(EdgeKey other)
        {
            return
                IsCorsPreflightRequest == other.IsCorsPreflightRequest &&
                HttpMethods.Equals(HttpMethod, other.HttpMethod);
        }
        
        public override bool Equals(object? obj)
        {
            var other = obj as EdgeKey?;
            return other == null ? false : Equals(other.Value);
        }
        
        public override int GetHashCode()
        {
            var hash = new HashCodeCombiner();
            hash.Add(IsCorsPreflightRequest ? 1 : 0);
            hash.Add(HttpMethod, StringComparer.Ordinal);
            return hash;
        }
        
        // Used in GraphViz output.
        public override string ToString()
        {
            return IsCorsPreflightRequest 
                ? $"CORS: {HttpMethod}" 
                : $"HTTP: {HttpMethod}";
        }
    }
}

```

* build jump table

```c#
public sealed class HttpMethodMatcherPolicy
{
    public PolicyJumpTable BuildJumpTable(
        int exitDestination, 
        IReadOnlyList<PolicyJumpTableEdge> edges)
    {
        Dictionary<string, int>? destinations = null;
        Dictionary<string, int>? corsPreflightDestinations = null;
        for (var i = 0; i < edges.Count; i++)
        {
            // We create this data, so it's safe to cast it.
            var key = (EdgeKey)edges[i].State;
            if (key.IsCorsPreflightRequest)
            {
                if (corsPreflightDestinations == null)
                {
                    corsPreflightDestinations = 
                        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                }
                
                corsPreflightDestinations.Add(key.HttpMethod, edges[i].Destination);
            }
            else
            {
                if (destinations == null)
                {
                    destinations = 
                        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                }
                
                destinations.Add(key.HttpMethod, edges[i].Destination);
            }
        }
        
        int corsPreflightExitDestination = exitDestination;
        if (corsPreflightDestinations != null && 
            corsPreflightDestinations.TryGetValue(
                AnyMethod, out var matchesAnyVerb))
        {
            // If we have endpoints that match any HTTP method, use that as the exit.
            corsPreflightExitDestination = matchesAnyVerb;
            corsPreflightDestinations.Remove(AnyMethod);
        }
        
        if (destinations != null && 
            destinations.TryGetValue(AnyMethod, out matchesAnyVerb))
        {
            // If we have endpoints that match any HTTP method, use that as the exit.
            exitDestination = matchesAnyVerb;
            destinations.Remove(AnyMethod);
        }
        
        if (destinations?.Count == 1)
        {
            // If there is only a single valid HTTP method then use an optimized jump table.
            // It avoids unnecessary dictionary lookups with the method name.
            var httpMethodDestination = destinations.Single();
            var method = httpMethodDestination.Key;
            var destination = httpMethodDestination.Value;
            var supportsCorsPreflight = false;
            var corsPreflightDestination = 0;
            
            if (corsPreflightDestinations?.Count > 0)
            {
                supportsCorsPreflight = true;
                corsPreflightDestination = corsPreflightDestinations.Single().Value;
            }
            
            return new HttpMethodSingleEntryPolicyJumpTable(
                exitDestination,
                method,
                destination,
                supportsCorsPreflight,
                corsPreflightExitDestination,
                corsPreflightDestination);
        }
        else
        {
            return new HttpMethodDictionaryPolicyJumpTable(
                exitDestination,
                destinations,
                corsPreflightExitDestination,
                corsPreflightDestinations);
        }
    }
    
    internal static bool IsCorsPreflightRequest(
        HttpContext httpContext, 
        string httpMethod, 
        out StringValues accessControlRequestMethod)
    {
        accessControlRequestMethod = default;
        var headers = httpContext.Request.Headers;
        
        return HttpMethods.Equals(httpMethod, PreflightHttpMethod) &&
            headers.ContainsKey(HeaderNames.Origin) &&
            headers.TryGetValue(
            	HeaderNames.AccessControlRequestMethod, 
	            out accessControlRequestMethod) &&
            !StringValues.IsNullOrEmpty(accessControlRequestMethod);
    }
}

```







#### 2.5 matcher





##### 2.5.2 data source dependent matcher

```c#

```

###### 2.5.2.1 lifetime

```c#

```



###### 2.5.2.3 suppress matching metadata

```c#

```

```c#

```

##### 2.5.3 dfa matcher

```c#
internal sealed class DfaMatcher : Matcher
{
    private readonly ILogger _logger;
    private readonly EndpointSelector _selector;
    private readonly DfaState[] _states;
    rivate readonly int _maxSegmentCount;
    
    private readonly bool _isDefaultEndpointSelector;
    
    public DfaMatcher(
        ILogger<DfaMatcher> logger, 
        EndpointSelector selector,
        DfaState[] states, 
        int maxSegmentCount)
    {
        // 注入服务，        
        _logger = logger;
        _selector = selector;
        _states = states;
        _maxSegmentCount = maxSegmentCount;
        
        _isDefaultEndpointSelector = selector is DefaultEndpointSelector;
    }
    
    public sealed override Task MatchAsync(HttpContext httpContext)
    {
        if (httpContext == null)
        {
            throw new ArgumentNullException(nameof(httpContext));
        }
        
        // All of the logging we do here is at level debug, 
        // so we can get away with doing a single check.
        var log = _logger.IsEnabled(LogLevel.Debug);
        
        // The sequence of actions we take is optimized to avoid doing expensive work
        // like creating substrings, creating route value dictionaries, and calling
        // into policies like versioning.
        var path = httpContext.Request.Path.Value!;
        
        /* 获取 candicate 和 policy 集合  */
        
        // First tokenize the path into series of segments.
        Span<PathSegment> buffer = stackalloc PathSegment[_maxSegmentCount];
        var count = FastPathTokenizer.Tokenize(path, buffer);
        var segments = buffer.Slice(0, count);
        
        // FindCandidateSet will process the DFA and return a candidate set. 
        // This does some preliminary matching of the URL (mostly the literal segments).
        var (candidates, policies) = FindCandidateSet(httpContext, path, segments);
        
        var candidateCount = candidates.Length;
        if (candidateCount == 0)
        {
            if (log)
            {
                Logger.CandidatesNotFound(_logger, path);
            }
            
            return Task.CompletedTask;
        }
        
        if (log)
        {
            Logger.CandidatesFound(_logger, path, candidates);
        }
        
        var policyCount = policies.Length;
        
        /* 如果只有1个 candidate，且没有 policy，且使用 default selector，
           注入 http context */
        
        // This is a fast path for single candidate, 0 policies and default selector
        if (candidateCount == 1 && 
            policyCount == 0 && 
            _isDefaultEndpointSelector)
        {
            ref readonly var candidate = ref candidates[0];
            
            // Just strict path matching (no route values)
            if (candidate.Flags == Candidate.CandidateFlags.None)
            {
                httpContext.SetEndpoint(candidate.Endpoint);
                
                // We're done
                return Task.CompletedTask;
            }
        }
        
        /* 封装 candidate 到 candidate state 集合，
           使用 endpoint selector */
        
        // At this point we have a candidate set, defined as a list of endpoints in
        // priority order.
        //
        // We don't yet know that any candidate can be considered a match, because
        // we haven't processed things like route constraints and complex segments.
        //
        // Now we'll iterate each endpoint to capture route values, process constraints,
        // and process complex segments.
        
        // `candidates` has all of our internal state that we use to process the
        // set of endpoints before we call the EndpointSelector.
        //
        // `candidateSet` is the mutable state that we pass to the EndpointSelector.
        var candidateState = new CandidateState[candidateCount];
        
        for (var i = 0; i < candidateCount; i++)
        {
            // PERF: using ref here to avoid copying around big structs.
            //
            // Reminder!
            // candidate: readonly data about the endpoint and how to match
            // state: mutable storarge for our processing
            ref readonly var candidate = ref candidates[i];
            ref var state = ref candidateState[i];
            state = new CandidateState(candidate.Endpoint, candidate.Score);
            
            var flags = candidate.Flags;
            
            // First process all of the parameters and defaults.
            if ((flags & Candidate.CandidateFlags.HasSlots) != 0)
            {
                // The Slots array has the default values of the route values in it.
                //
                // We want to create a new array for the route values based on Slots
                // as a prototype.
                var prototype = candidate.Slots;
                var slots = new KeyValuePair<string, object?>[prototype.Length];
                
                if ((flags & 
                     Candidate.CandidateFlags.HasDefaults) != 0)
                {
                    Array.Copy(prototype, 0, slots, 0, prototype.Length);
                }
                
                if ((flags & 
                     Candidate.CandidateFlags.HasCaptures) != 0)
                {
                    ProcessCaptures(slots, candidate.Captures, path, segments);
                }
                
                if ((flags & 
                     Candidate.CandidateFlags.HasCatchAll) != 0)
                {
                    ProcessCatchAll(slots, candidate.CatchAll, path, segments);
                }
                
                state.Values = RouteValueDictionary.FromArray(slots);
            }
            
            // Now that we have the route values, we need to process complex segments.
            // Complex segments go through an old API that requires a fully-materialized
            // route value dictionary.
            var isMatch = true;
            if ((flags & 
                 Candidate.CandidateFlags.HasComplexSegments) != 0)
            {
                state.Values ??= new RouteValueDictionary();
                if (!ProcessComplexSegments(
                    	candidate.Endpoint, 
                    	candidate.ComplexSegments, 
                    	path, 
                    	segments, 
                    	state.Values))
                {
                    CandidateSet.SetValidity(ref state, false);
                    isMatch = false;
                }
            }
            
            if ((flags & 
                 Candidate.CandidateFlags.HasConstraints) != 0)
            {
                state.Values ??= new RouteValueDictionary();
                if (!ProcessConstraints(
                    	candidate.Endpoint, 
                    	candidate.Constraints, 
                    	httpContext, 
                    	state.Values))
                {
                    CandidateSet.SetValidity(ref state, false);
                    isMatch = false;
                }
            }
            
            if (log)
            {
                if (isMatch)
                {
                    Logger.CandidateValid(_logger, path, candidate.Endpoint);
                }
                else
                {
                    Logger.CandidateNotValid(_logger, path, candidate.Endpoint);
                }
            }
        }
        
        // 如果 policy = 0
        if (policyCount == 0 && 
            _isDefaultEndpointSelector)
        {
            // Fast path that avoids allocating the candidate set.
            //
            // We can use this when there are no policies and we're using the default selector.
            DefaultEndpointSelector.Select(httpContext, candidateState);
            return Task.CompletedTask;
        }
        else if (policyCount == 0)
        {
            // Fast path that avoids a state machine.
            //
            // We can use this when there are no policies and a non-default selector.
            return _selector.SelectAsync(
                httpContext, 
                new CandidateSet(candidateState));
        }
        
        // policy != 0 并且 selector 不是 default
        return SelectEndpointWithPoliciesAsync(
            httpContext, 
            policies, 
            new CandidateSet(candidateState));
    }                                               
}

```

###### 2.5.3.1 find candidates

* 解析 candidate、endpoint selector policy 集合

```c#
internal sealed class DfaMatcher : Matcher
{
    internal (Candidate[] candidates, IEndpointSelectorPolicy[] policies) FindCandidateSet(
        HttpContext httpContext,
        string path,
        ReadOnlySpan<PathSegment> segments)
    {
        //?
        var states = _states;
        
        // Process each path segment
        var destination = 0;
        for (var i = 0; i < segments.Length; i++)
        {
            destination = states[destination]
                .PathTransitions
                .GetDestination(path, segments[i]);
        }
        
        // Process an arbitrary number of policy-based decisions
        var policyTransitions = states[destination].PolicyTransitions;
        while (policyTransitions != null)
        {
            destination = policyTransitions.GetDestination(httpContext);
            policyTransitions = states[destination].PolicyTransitions;
        }
        
        return (states[destination].Candidates, states[destination].Policies);
    }
}

```

###### 2.5.3.2 process capture

```c#
internal sealed class DfaMatcher : Matcher
{
    private void ProcessCaptures(
        KeyValuePair<string, object?>[] slots,
        (string parameterName, int segmentIndex, int slotIndex)[] captures,
        string path,
        ReadOnlySpan<PathSegment> segments)
    {
        for (var i = 0; i < captures.Length; i++)
        {
            (var parameterName, var segmentIndex, var slotIndex) = captures[i];
            
            if ((uint)segmentIndex < (uint)segments.Length)
            {
                var segment = segments[segmentIndex];
                if (parameterName != null && segment.Length > 0)
                {
                    slots[slotIndex] = new KeyValuePair<string, object?>(
                        parameterName,
                        path.Substring(segment.Start, segment.Length));
                }
            }
        }
    }
}

```

###### 2.5.3.3 process catch all

```c#
internal sealed class DfaMatcher : Matcher
{
    private void ProcessCatchAll(
        KeyValuePair<string, object?>[] slots,
        in (string parameterName, int segmentIndex, int slotIndex) catchAll,
        string path,
        ReadOnlySpan<PathSegment> segments)
    {
        // Read segmentIndex to local both to skip double read from stack value
        // and to use the same in-bounds validated variable to access the array.
        var segmentIndex = catchAll.segmentIndex;
        if ((uint)segmentIndex < (uint)segments.Length)
        {
            var segment = segments[segmentIndex];
            slots[catchAll.slotIndex] = new KeyValuePair<string, object?>(
                catchAll.parameterName,
                path.Substring(segment.Start));
        }
    }
}

```

###### 2.5.3.4 process complex segment

```c#
internal sealed class DfaMatcher : Matcher
{
     private bool ProcessComplexSegments(
        Endpoint endpoint,
        (RoutePatternPathSegment pathSegment, int segmentIndex)[] complexSegments,
        string path,
        ReadOnlySpan<PathSegment> segments,
        RouteValueDictionary values)
    {
        for (var i = 0; i < complexSegments.Length; i++)
        {
            (var complexSegment, var segmentIndex) = complexSegments[i];
            var segment = segments[segmentIndex];
            var text = path.AsSpan(segment.Start, segment.Length);
            if (!RoutePatternMatcher
                .MatchComplexSegment(complexSegment, text, values))
            {
                Logger.CandidateRejectedByComplexSegment(
                    _logger, 
                    path, 
                    endpoint, 
                    complexSegment);
                return false;
            }
        }
        
        return true;
    }
}

```

###### 2.5.3.5. process constraint

```c#
internal sealed class DfaMatcher : Matcher
{
    private bool ProcessConstraints(
        Endpoint endpoint,
        KeyValuePair<string, IRouteConstraint>[] constraints,
        HttpContext httpContext,
        RouteValueDictionary values)
    {
        for (var i = 0; i < constraints.Length; i++)
        {
            var constraint = constraints[i];
            if (!constraint.Value.Match(
                	httpContext, 
                	NullRouter.Instance, 
                	constraint.Key, 
                	values, 
                	RouteDirection.IncomingRequest))
            {
                Logger.CandidateRejectedByConstraint(
                    _logger, 
                    httpContext.Request.Path, 
                    endpoint, 
                    constraint.Key, 
                    constraint.Value, 
                    values[constraint.Key]);
                return false;
            }
        }
        
        return true;
    }
}

```

###### 2.5.3.6 select endpoint with policy

```c#
internal sealed class DfaMatcher : Matcher
{
    private async Task SelectEndpointWithPoliciesAsync(
        HttpContext httpContext,
        IEndpointSelectorPolicy[] policies,
        CandidateSet candidateSet)
    {
        for (var i = 0; i < policies.Length; i++)
        {
            var policy = policies[i];
            await policy.ApplyAsync(httpContext, candidateSet);
            if (httpContext.GetEndpoint() != null)
            {
                // This is a short circuit, the selector chose an endpoint.
                return;
            }
        }
        
        await _selector.SelectAsync(httpContext, candidateSet);
    }            
}

```

###### 2.5.3.7 log

```c#
internal sealed class DfaMatcher : Matcher
{
    internal static class EventIds
    {
        public static readonly EventId CandidatesNotFound = 
            new EventId(1000, "CandidatesNotFound");
        public static readonly EventId CandidatesFound = 
            new EventId(1001, "CandidatesFound");        
        public static readonly EventId CandidateRejectedByComplexSegment = 
            new EventId(1002, "CandidateRejectedByComplexSegment");
        public static readonly EventId CandidateRejectedByConstraint = 
            new EventId(1003, "CandidateRejectedByConstraint");        
        public static readonly EventId CandidateNotValid = 
            new EventId(1004, "CandiateNotValid");
        public static readonly EventId CandidateValid = 
            new EventId(1005, "CandiateValid");
    }
    
    #nullable disable
    private static class Logger
    {
        private static readonly Action<ILogger, string, Exception> 
            _candidatesNotFound = LoggerMessage.Define<string>(
            	LogLevel.Debug,
            	EventIds.CandidatesNotFound,
	            "No candidates found for the request path '{Path}'");
        
        private static readonly Action<ILogger, int, string, Exception> 
            _candidatesFound = LoggerMessage.Define<int, string>(
            	LogLevel.Debug,
	            EventIds.CandidatesFound,
    	        "{CandidateCount} candidate(s) found for the request path '{Path}'");
        
        private static readonly Action<ILogger, string, string, string, string, Exception> 
            _candidateRejectedByComplexSegment = 
            	LoggerMessage.Define<string, string, string, string>(
            		LogLevel.Debug,
		            EventIds.CandidateRejectedByComplexSegment,
		            "Endpoint '{Endpoint}' with route pattern '{RoutePattern}' was rejected by complex segment '{Segment}' for the request path '{Path}'");
        
        private static readonly 
            Action<ILogger, string, string, string, string, object, string, Exception> 
            	_candidateRejectedByConstraint = 
            		LoggerMessage.Define<string, string, string, string, object, string>(
			            LogLevel.Debug,
            			EventIds.CandidateRejectedByConstraint,
			            "Endpoint '{Endpoint}' with route pattern '{RoutePattern}' was rejected by constraint '{ConstraintName}':'{Constraint}' with value '{RouteValue}' for the request path '{Path}'");
        
        private static readonly Action<ILogger, string, string, string, Exception> 
            _candidateNotValid = LoggerMessage.Define<string, string, string>(
            	LogLevel.Debug,
	            EventIds.CandidateNotValid,
    	        "Endpoint '{Endpoint}' with route pattern '{RoutePattern}' is not valid for the request path '{Path}'");
        
        private static readonly Action<ILogger, string, string, string, Exception> 
            _candidateValid = LoggerMessage.Define<string, string, string>(
            	LogLevel.Debug,
            	EventIds.CandidateValid,
	            "Endpoint '{Endpoint}' with route pattern '{RoutePattern}' is valid for the request path '{Path}'");
        
        public static void CandidatesNotFound(
            ILogger logger, 
            string path)
        {
            _candidatesNotFound(logger, path, null);
        }
        
        public static void CandidatesFound(
            ILogger logger, 
            string path, 
            Candidate[] candidates)
        {
            _candidatesFound(logger, candidates.Length, path, null);
        }
        
        public static void CandidateRejectedByComplexSegment(
            ILogger logger, 
            string path, 
            Endpoint endpoint, 
            RoutePatternPathSegment segment)
        {
            // This should return a real pattern 
            // since we're processing complex segments.... but just in case.
            if (logger.IsEnabled(LogLevel.Debug))
            {
                var routePattern = GetRoutePattern(endpoint);
                _candidateRejectedByComplexSegment(
                    logger, 
                    endpoint.DisplayName, 
                    routePattern, 
                    segment.DebuggerToString(), 
                    path, 
                    null);
            }
        }
        
        public static void CandidateRejectedByConstraint(
            ILogger logger, 
            string path, 
            Endpoint endpoint, 
            string constraintName, 
            IRouteConstraint constraint, 
            object value)
        {
            // This should return a real pattern 
            // since we're processing constraints.... but just in case.
            if (logger.IsEnabled(LogLevel.Debug))
            {
                var routePattern = GetRoutePattern(endpoint);
                _candidateRejectedByConstraint(
                    logger, 
                    endpoint.DisplayName, 
                    routePattern, 
                    constraintName, 
                    constraint.ToString(), 
                    value, 
                    path, 
                    null);
            }
        }
        
        public static void CandidateNotValid(
            ILogger logger, 
            string path, 
            Endpoint endpoint)
        {
            // This can be the fallback value because it really might not be a route endpoint
            if (logger.IsEnabled(LogLevel.Debug))
            {
                var routePattern = GetRoutePattern(endpoint);
                _candidateNotValid(
                    logger, 
                    endpoint.DisplayName, 
                    routePattern, 
                    path, 
                    null);
            }
        }
        
        public static void CandidateValid(
            ILogger logger, 
            string path, 
            Endpoint endpoint)
        {
            // This can be the fallback value because it really might not be a route endpoint
            if (logger.IsEnabled(LogLevel.Debug))
            {
                var routePattern = GetRoutePattern(endpoint);
                _candidateValid(
                    logger, 
                    endpoint.DisplayName, 
                    routePattern, 
                    path, 
                    null);
            }
        }
        
        private static string GetRoutePattern(Endpoint endpoint)
        {
            return (endpoint as RouteEndpoint)
                ?.RoutePattern
                ?.RawText 
                ?? "(none)";
        }
    }
}

```

#### 2.6 创建 matcher





##### 2.6.2 dfa matcher builder

```c#
internal class DfaMatcherBuilder : MatcherBuilder
{
    private readonly List<RouteEndpoint> _endpoints = new List<RouteEndpoint>();
    
    private readonly ILoggerFactory _loggerFactory;
    private readonly ParameterPolicyFactory _parameterPolicyFactory;
    private readonly EndpointSelector _selector;
    private readonly IEndpointSelectorPolicy[] _endpointSelectorPolicies;
    private readonly INodeBuilderPolicy[] _nodeBuilders;
    private readonly EndpointComparer _comparer;
    
    // These collections are reused when building candidates
    private readonly 
        Dictionary<string, int> _assignments;
    private readonly 
        List<KeyValuePair<string, object>> _slots;
    private readonly 
        List<(string parameterName, int segmentIndex, int slotIndex)> _captures;
    private readonly 
        List<(RoutePatternPathSegment pathSegment, int segmentIndex)> _complexSegments;
    private readonly 
        List<KeyValuePair<string, IRouteConstraint>> _constraints;
    
    private int _stateIndex;
    
    // Used in tests
    internal EndpointComparer Comparer => _comparer;    
    // Used in tests
    internal bool UseCorrectCatchAllBehavior { get; set; }
    
    public DfaMatcherBuilder(
        ILoggerFactory loggerFactory,
        ParameterPolicyFactory parameterPolicyFactory,
        EndpointSelector selector,
        IEnumerable<MatcherPolicy> policies)
    {
        // 注入服务，
        // logger, parameter policy factory, endpoint selector
        _loggerFactory = loggerFactory;
        _parameterPolicyFactory = parameterPolicyFactory;
        _selector = selector;
        
        if (AppContext.TryGetSwitch(
            	"Microsoft.AspNetCore.Routing.UseCorrectCatchAllBehavior", 
            	out var enabled))
        {
            UseCorrectCatchAllBehavior = enabled;
        }
        else
        {
            UseCorrectCatchAllBehavior = true; // default to correct behavior
        }
        
        // 从注入的 policies 中抽取，
        // node builder policy，endpoint comparer policy 和 endpointselector policy
        var (nodeBuilderPolicies, 
             endpointComparerPolicies, 
             endpointSelectorPolicies) = ExtractPolicies(policies.OrderBy(p => p.Order));
        
        _endpointSelectorPolicies = endpointSelectorPolicies;
        _nodeBuilders = nodeBuilderPolicies;
        
        // 创建 candidate 组件（empty）        
        _comparer = new EndpointComparer(endpointComparerPolicies);        
        _assignments = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _slots = new List<KeyValuePair<string, object>>();
        _captures = new List<(string parameterName, int segmentIndex, int slotIndex)>();
        _complexSegments = new List<(RoutePatternPathSegment pathSegment, int segmentIndex)>();
        _constraints = new List<KeyValuePair<string, IRouteConstraint>>();
    }
    
    /* 实现接口方法 add endpoint */    
    public override void AddEndpoint(RouteEndpoint endpoint)
    {
        _endpoints.Add(endpoint);
    }
        
    /* 实现接口方法 build (matcher) */
    public override Matcher Build()
    {
#if DEBUG
    	var includeLabel = true;
#else
	    var includeLabel = false;
#endif
    	/* a - build dfa tree */
    	var root = BuildDfaTree(includeLabel);
        
        // State count is the number of nodes plus an exit state
        var stateCount = 1;
        var maxSegmentCount = 0;
        root.Visit((node) =>
        	{
                stateCount++;
                maxSegmentCount = Math.Max(maxSegmentCount, node.PathDepth);
            });
        _stateIndex = 0;
        
        // The max segment count is the maximum path-node-depth +1. We need
        // the +1 to capture any additional content after the 'last' segment.
        maxSegmentCount++;
        
        /* b - add node */
        var states = new DfaState[stateCount];
        var exitDestination = stateCount - 1;
        AddNode(root, states, exitDestination);
        
        // The root state only has a jump table.
        states[exitDestination] = new DfaState(
            Array.Empty<Candidate>(),
            Array.Empty<IEndpointSelectorPolicy>(),
            JumpTableBuilder.Build(exitDestination, exitDestination, null),
            null);
        
        /* 创建 dfa matcher */
        return new DfaMatcher(
            _loggerFactory.CreateLogger<DfaMatcher>(), 
            _selector, 
            states, 
            maxSegmentCount);
    }                                                                            
}

```

###### 2.6.2.1 extra policy

* 如果实现了对应接口，
* 将自身注册到对应接口的集合中

```c#
internal class DfaMatcherBuilder : MatcherBuilder
{
    private static (INodeBuilderPolicy[] nodeBuilderPolicies, 
                    IEndpointComparerPolicy[] endpointComparerPolicies, 
                    IEndpointSelectorPolicy[] endpointSelectorPolicies) 
        ExtractPolicies(IEnumerable<MatcherPolicy> policies)
    {
        var nodeBuilderPolicies = new List<INodeBuilderPolicy>();
        var endpointComparerPolicies = new List<IEndpointComparerPolicy>();
        var endpointSelectorPolicies = new List<IEndpointSelectorPolicy>();
        
        foreach (var policy in policies)
        {
            if (policy is INodeBuilderPolicy nodeBuilderPolicy)
            {
                nodeBuilderPolicies.Add(nodeBuilderPolicy);
            }
            
            if (policy is IEndpointComparerPolicy endpointComparerPolicy)
            {
                endpointComparerPolicies.Add(endpointComparerPolicy);
            }
            
            if (policy is IEndpointSelectorPolicy endpointSelectorPolicy)
            {
                endpointSelectorPolicies.Add(endpointSelectorPolicy);
            }
        }
        
        return (nodeBuilderPolicies.ToArray(), 
                endpointComparerPolicies.ToArray(), 
                endpointSelectorPolicies.ToArray());
    }
}

```

###### 2.6.2.2 try get required value

```c#
internal class DfaMatcherBuilder : MatcherBuilder
{
    private static bool TryGetRequiredValue(
        RoutePattern routePattern, 
        RoutePatternParameterPart parameterPart, 
        out object value)
    {
        if (!routePattern
            	.RequiredValues
            	.TryGetValue(parameterPart.Name, out value))
        {
            return false;
        }
        
        return !RouteValueEqualityComparer
            .Default
            .Equals(value, string.Empty);
    }
}

```

##### 2.6.3 build matcher - build dfa tree

```c#
internal class DfaMatcherBuilder : MatcherBuilder
{
    public DfaNode BuildDfaTree(bool includeLabel = false)
    {
        if (!UseCorrectCatchAllBehavior)
        {
            // In 3.0 we did a global sort of the endpoints up front. 
            // This was a bug, because we actually want
            // do do the sort at each level of the tree based on precedence.
            //
            // _useLegacy30Behavior enables opt-out via an AppContext switch.
            _endpoints.Sort(_comparer);
        }
        
        // Since we're doing a BFS we will process each 'level' of the tree in stages
        // this list will hold the set of items we need to process at the current
        // stage.
        var work = 
            new List<(RouteEndpoint endpoint, 
                      int precedenceDigit, 
                      List<DfaNode> parents)>(_endpoints.Count);
        
        List<(RouteEndpoint endpoint, 
              int precedenceDigit, 
              List<DfaNode> parents)> previousWork = null;
        
        var root = new DfaNode() 
        { 
            PathDepth = 0, 
            Label = includeLabel ? "/" : null 
        };
        
        // To prepare for this we need to compute the max depth, as well as
        // a seed list of items to process (entry, root).
        var maxDepth = 0;
        for (var i = 0; i < _endpoints.Count; i++)
        {
            var endpoint = _endpoints[i];
            var precedenceDigit = 
                GetPrecedenceDigitAtDepth(endpoint, depth: 0);
            work.Add((endpoint, precedenceDigit, new List<DfaNode>() { root, }));
            
            maxDepth = Math.Max(maxDepth, endpoint.RoutePattern.PathSegments.Count);
        }
        
        var workCount = work.Count;
        
        // Sort work at each level by *PRECEDENCE OF THE CURRENT SEGMENT*.
        //
        // We build the tree by doing a BFS over the list of entries. This is important
        // because a 'parameter' node can also traverse the same paths that literal nodes
        // traverse. This means that we need to order the entries first, or else we will
        // miss possible edges in the DFA.
        //
        // We'll sort the matches again later using the *real* comparer once building the
        // precedence part of the DFA is over.
        var precedenceDigitComparer = 
            Comparer<(RouteEndpoint endpoint, 
                      int precedenceDigit, 
                      List<DfaNode> parents)>
            .Create((x, y) =>
            {
                return x
                    .precedenceDigit
                    .CompareTo(y.precedenceDigit);
            });
        
        // Now we process the entries a level at a time.
        for (var depth = 0; depth <= maxDepth; depth++)
        {
            // As we process items, collect the next set of items.
            List<(RouteEndpoint endpoint, 
                  int precedenceDigit, 
                  List<DfaNode> parents)> nextWork;
            
            var nextWorkCount = 0;
            if (previousWork == null)
            {
                nextWork = 
                    new List<(RouteEndpoint endpoint, 
                              int precedenceDigit, 
                              List<DfaNode> parents)>();
            }
            else
            {
                // Reuse previous collection for the next collection
                // Don't clear the list so nested lists can be reused
                nextWork = previousWork;
            }
            
            if (UseCorrectCatchAllBehavior)
            {
                // The fix for the 3.0 sorting behavior bug.
                
                // See comments on precedenceDigitComparer
                work.Sort(0, workCount, precedenceDigitComparer);
            }
            
            for (var i = 0; i < workCount; i++)
            {
                var (endpoint, _, parents) = work[i];
                
                if (!HasAdditionalRequiredSegments(endpoint, depth))
                {
                    for (var j = 0; j < parents.Count; j++)
                    {
                        var parent = parents[j];
                        parent.AddMatch(endpoint);
                    }
                }
                
                // Find the parents of this edge at the current depth
                List<DfaNode> nextParents;
                if (nextWorkCount < nextWork.Count)
                {
                    nextParents = nextWork[nextWorkCount].parents;
                    nextParents.Clear();
                    
                    var nextPrecedenceDigit = GetPrecedenceDigitAtDepth(endpoint, depth + 1);
                    nextWork[nextWorkCount] = (endpoint, nextPrecedenceDigit, nextParents);
                }
                else
                {
                    nextParents = new List<DfaNode>();
                    
                    // Add to the next set of work now so the list will be reused
                    // even if there are no parents
                    var nextPrecedenceDigit = GetPrecedenceDigitAtDepth(endpoint, depth + 1);
                    nextWork.Add((endpoint, nextPrecedenceDigit, nextParents));
                }
                
                var segment = GetCurrentSegment(endpoint, depth);
                if (segment == null)
                {
                    continue;
                }
                
                for (var j = 0; j < parents.Count; j++)
                {
                    var parent = parents[j];
                    var part = segment.Parts[0];
                    var parameterPart = part as RoutePatternParameterPart;
                    if (segment.IsSimple && 
                        part is RoutePatternLiteralPart literalPart)
                    {
                        AddLiteralNode(
                            includeLabel, 
                            nextParents, 
                            parent, 
                            literalPart.Content);
                    }
                    else if (segment.IsSimple && 
                             parameterPart != null && 
                             parameterPart.IsCatchAll)
                    {
                        // A catch all should traverse all literal nodes 
                        // as well as parameter nodes we don't need 
                        // to create the parameter node here because of ordering
                        // all catchalls will be processed after all parameters.
                        if (parent.Literals != null)
                        {
                            nextParents.AddRange(parent.Literals.Values);
                        }
                        if (parent.Parameters != null)
                        {
                            nextParents.Add(parent.Parameters);
                        }
                        
                        // We also create a 'catchall' here. We don't do further traversals
                        // on the catchall node because only catchalls can end up here. The
                        // catchall node allows us to capture an unlimited amount of segments
                        // and also to match a zero-length segment, which a parameter node
                        // doesn't allow.
                        if (parent.CatchAll == null)
                        {
                            parent.CatchAll = new DfaNode()
                            {
                                PathDepth = parent.PathDepth + 1,
                                Label = includeLabel ? parent.Label + "{*...}/" : null,
                            };
                            
                            // The catchall node just loops.
                            parent.CatchAll.Parameters = parent.CatchAll;
                            parent.CatchAll.CatchAll = parent.CatchAll;
                        }
                        
                        parent.CatchAll.AddMatch(endpoint);
                    }
                    else if (segment.IsSimple && 
                             parameterPart != null && 
                             TryGetRequiredValue(
                                 endpoint.RoutePattern, 
                                 parameterPart, 
                                 out var requiredValue))
                    {
                        // If the parameter has a matching required value, 
                        // replace the parameter with the required value as a literal. 
                        // This should use the parameter's transformer (if present) e.g. 
                        // Template: Home/{action}, 
                        // Required values: { action = "Index" }, Result: Home/Index
                        
                        if (endpoint
                            	.RoutePattern
                            	.ParameterPolicies
                            	.TryGetValue(
                                    parameterPart.Name, 
                                    out var parameterPolicyReferences))
                        {
                            for (var k = 0; k < parameterPolicyReferences.Count; k++)
                            {
                                var reference = parameterPolicyReferences[k];
                                var parameterPolicy = _parameterPolicyFactory.Create(
                                    parameterPart, 
                                    reference);
                                if (parameterPolicy is 
                                    IOutboundParameterTransformer parameterTransformer)
                                {
                                    requiredValue = parameterTransformer
                                        .TransformOutbound(requiredValue);
                                    break;
                                }
                            }
                        }
                        
                        var literalValue = requiredValue
                            ?.ToString() 
                            ?? throw new InvalidOperationException(
                            	$"Required value for literal '{parameterPart.Name}' 
                            	"must evaluate to a non-null string.");
                        
                        AddLiteralNode(
                            includeLabel, 
                            nextParents, 
                            parent, 
                            literalValue);
                    }
                    else if (segment.IsSimple && 
                             parameterPart != null)
                    {
                        if (parent.Parameters == null)
                        {
                            parent.Parameters = new DfaNode()
                            {
                                PathDepth = parent.PathDepth + 1,
                                Label = includeLabel ? parent.Label + "{...}/" : null,
                            };
                        }
                        
                        // A parameter should traverse all literal nodes 
                        // as well as the parameter node
                        if (parent.Literals != null)
                        {
                            nextParents.AddRange(parent.Literals.Values);
                        }
                        nextParents.Add(parent.Parameters);
                    }
                    else
                    {
                        // Complex segment - we treat these are parameters here and do the
                        // expensive processing later. We don't want to spend time processing
                        // complex segments unless they are the best match, and treating them
                        // like parameters in the DFA allows us to do just that.
                        if (parent.Parameters == null)
                        {
                            parent.Parameters = new DfaNode()
                            {
                                PathDepth = parent.PathDepth + 1,
                                Label = includeLabel ? parent.Label + "{...}/" : null,
                            };
                        }
                        
                        if (parent.Literals != null)
                        {
                            nextParents.AddRange(parent.Literals.Values);
                        }
                        nextParents.Add(parent.Parameters);
                    }
                }
                
                if (nextParents.Count > 0)
                {
                    nextWorkCount++;
                }
            }
            
            // Prepare the process the next stage.
            previousWork = work;
            work = nextWork;
            workCount = nextWorkCount;
        }
        
        // Build the trees of policy nodes (like HTTP methods). Post-order traversal
        // means that we won't have infinite recursion.
        root.Visit(ApplyPolicies);
        
        return root;
    }                
}

```

###### 2.6.3.1 get precedence digit at depth

```c#
internal class DfaMatcherBuilder : MatcherBuilder
{
    private static int GetPrecedenceDigitAtDepth(
        RouteEndpoint endpoint, 
        int depth)
    {
        var segment = GetCurrentSegment(endpoint, depth);
        if (segment is null)
        {
            // Treat "no segment" as high priority. 
            // it won't effect the algorithm, but we need to define a sort-order.
            return 0;
        }
        
        return RoutePrecedence
            .ComputeInboundPrecedenceDigit(endpoint.RoutePattern, segment);
    }
}

```

###### 2.6.3.2 has additional required segment

```c#
internal class DfaMatcherBuilder : MatcherBuilder
{
    private static bool HasAdditionalRequiredSegments(
        RouteEndpoint endpoint, 
        int depth)
    {
        for (var i = depth; i < endpoint.RoutePattern.PathSegments.Count; i++)
        {
            var segment = endpoint.RoutePattern.PathSegments[i];
            if (!segment.IsSimple)
            {
                // Complex segments always require more processing
                return true;
            }
            
            var parameterPart = segment.Parts[0] as RoutePatternParameterPart;
            if (parameterPart == null)
            {
                // It's a literal
                return true;
            }
            
            if (!parameterPart.IsOptional &&
                !parameterPart.IsCatchAll &&
                parameterPart.Default == null)
            {
                return true;
            }
        }
        
        return false;
    }
}

```

###### 2.6.3.3 get current segment

```c#
internal class DfaMatcherBuilder : MatcherBuilder
{
    private static RoutePatternPathSegment GetCurrentSegment(
        RouteEndpoint endpoint, 
        int depth)
    {
        if (depth < endpoint.RoutePattern.PathSegments.Count)
        {
            return endpoint.RoutePattern.PathSegments[depth];
        }
        
        if (endpoint.RoutePattern.PathSegments.Count == 0)
        {
            return null;
        }
        
        var lastSegment = endpoint
            .RoutePattern
            .PathSegments[
            	endpoint
            		.RoutePattern
            		.PathSegments.Count - 1];
        
        if (lastSegment.IsSimple && 
            lastSegment.Parts[0] is RoutePatternParameterPart parameterPart && 
            parameterPart.IsCatchAll)
        {
            return lastSegment;
        }
        
        return null;
    }
}

```

###### 2.6.3.4 add literal node

```c#
internal class DfaMatcherBuilder : MatcherBuilder
{
    private static void AddLiteralNode(
        bool includeLabel, 
        List<DfaNode> nextParents, 
        DfaNode parent, 
        string literal)
    {
        DfaNode next = null;
        if (parent.Literals == null ||
            !parent.Literals.TryGetValue(literal, out next))
        {
            next = new DfaNode()
            {
                PathDepth = parent.PathDepth + 1,
                Label = includeLabel ? parent.Label + literal + "/" : null,
            };
            parent.AddLiteral(literal, next);
        }
        
        nextParents.Add(next);
    }
}

```

###### 2.6.3.5 apply policies

```c#
internal class DfaMatcherBuilder : MatcherBuilder
{
    private void ApplyPolicies(DfaNode node)
    {
        if (node.Matches == null || node.Matches.Count == 0)
        {
            return;
        }
        
        // We're done with the precedence based work. Sort the endpoints
        // before applying policies for simplicity in policy-related code.
        node.Matches.Sort(_comparer);
        
        // Start with the current node as the root.
        var work = new List<DfaNode>() { node, };
        List<DfaNode> previousWork = null;
        for (var i = 0; i < _nodeBuilders.Length; i++)
        {
            var nodeBuilder = _nodeBuilders[i];
            
            // Build a list of each
            List<DfaNode> nextWork;
            if (previousWork == null)
            {
                nextWork = new List<DfaNode>();
            }
            else
            {
                // Reuse previous collection for the next collection
                previousWork.Clear();
                nextWork = previousWork;
            }
            
            for (var j = 0; j < work.Count; j++)
            {
                var parent = work[j];
                if (!nodeBuilder
                    	.AppliesToEndpoints(
                            parent.Matches 
                            	?? (IReadOnlyList<Endpoint>)Array.Empty<Endpoint>()))
                {
                    // This node-builder doesn't care about this node, so add it to the list
                    // to be processed by the next node-builder.
                    nextWork.Add(parent);
                    continue;
                }
                
                // This node-builder does apply to this node, 
                // so we need to create new nodes for each edge,
                // and then attach them to the parent.
                var edges = nodeBuilder.GetEdges(
                    parent.Matches 
                    	?? (IReadOnlyList<Endpoint>)Array.Empty<Endpoint>());
                for (var k = 0; k < edges.Count; k++)
                {
                    var edge = edges[k];
                    
                    var next = new DfaNode()
                    {
                        // If parent label is null then labels are not being included
                        Label = (parent.Label != null) 
                            ? parent.Label + " " + edge.State.ToString() 
                            : null,
                    };
                    
                    if (edge.Endpoints.Count > 0)
                    {
                        next.AddMatches(edge.Endpoints);
                    }
                    nextWork.Add(next);
                    
                    parent.AddPolicyEdge(edge.State, next);
                }
                
                // Associate the node-builder so we can build a jump table later.
                parent.NodeBuilder = nodeBuilder;
                
                // The parent no longer has matches, it's not considered a terminal node.
                parent.Matches?.Clear();
            }
            
            previousWork = work;
            work = nextWork;
        }
    }                        
}

```

##### 2.6.4 build matche - add node

```c#
internal class DfaMatcherBuilder : MatcherBuilder
{
    private int AddNode(
        DfaNode node,
        DfaState[] states,
        int exitDestination)
    {
        node.Matches?.Sort(_comparer);
        
        var currentStateIndex = _stateIndex;
        
        var currentDefaultDestination = exitDestination;
        var currentExitDestination = exitDestination;
        (string text, int destination)[] pathEntries = null;
        PolicyJumpTableEdge[] policyEntries = null;
        
        if (node.Literals != null)
        {
            pathEntries = new (string text, int destination)[node.Literals.Count];
            
            var index = 0;
            foreach (var kvp in node.Literals)
            {
                var transition = Transition(kvp.Value);
                pathEntries[index++] = (kvp.Key, transition);
            }
        }
        
        if (node.Parameters != null &&
            node.CatchAll != null &&
            ReferenceEquals(node.Parameters, node.CatchAll))
        {
            // This node has a single transition to but it should accept zero-width segments
            // this can happen when a node only has catchall parameters.
            currentExitDestination 
                = currentDefaultDestination 
                = Transition(node.Parameters);
        }
        else if (node.Parameters != null && 
                 node.CatchAll != null)
        {
            // This node has a separate transition for zero-width segments
            // this can happen when a node has both parameters and catchall parameters.
            currentDefaultDestination = Transition(node.Parameters);
            currentExitDestination = Transition(node.CatchAll);
        }
        else if (node.Parameters != null)
        {
            // This node has paramters but no catchall.
            currentDefaultDestination = Transition(node.Parameters);
        }
        else if (node.CatchAll != null)
        {
            // This node has a catchall but no parameters
            currentExitDestination 
                = currentDefaultDestination 
                = Transition(node.CatchAll);
        }
        
        if (node.PolicyEdges != null && 
            node.PolicyEdges.Count > 0)
        {
            policyEntries = new PolicyJumpTableEdge[node.PolicyEdges.Count];
            
            var index = 0;
            foreach (var kvp in node.PolicyEdges)
            {
                policyEntries[index++] = new PolicyJumpTableEdge(
                    kvp.Key, 
                    Transition(kvp.Value));
            }
        }
        
        var candidates = CreateCandidates(node.Matches);
        
        // Perf: most of the time there aren't any endpoint selector policies, create
        // this lazily.
        List<IEndpointSelectorPolicy> endpointSelectorPolicies = null;
        if (node.Matches?.Count > 0)
        {
            for (var i = 0; i < _endpointSelectorPolicies.Length; i++)
            {
                var endpointSelectorPolicy = _endpointSelectorPolicies[i];
                if (endpointSelectorPolicy.AppliesToEndpoints(node.Matches))
                {
                    if (endpointSelectorPolicies == null)
                    {
                        endpointSelectorPolicies = new List<IEndpointSelectorPolicy>();
                    }
                    
                    endpointSelectorPolicies.Add(endpointSelectorPolicy);
                }
            }
        }
        
        states[currentStateIndex] = new DfaState(
            candidates,
            endpointSelectorPolicies
            	?.ToArray() ?? Array.Empty<IEndpointSelectorPolicy>(),
            JumpTableBuilder.Build(
                currentDefaultDestination, 
                currentExitDestination, 
                pathEntries),
            // Use the final exit destination when building the policy state.
            // We don't want to use either of the current destinations 
            // because they refer routing states,
            // and a policy state should never transition back to a routing state.
            BuildPolicy(exitDestination, node.NodeBuilder, policyEntries));
        
        return currentStateIndex;
        
        int Transition(DfaNode next)
        {
            // Break cycles
            if (ReferenceEquals(node, next))
            {
                return _stateIndex;
            }
            else
            {
                _stateIndex++;
                return AddNode(next, states, exitDestination);
            }
        }
    }
}

```

###### 2.6.4.1 create candidates

```c#
internal class DfaMatcherBuilder : MatcherBuilder
{
    internal Candidate[] CreateCandidates(IReadOnlyList<Endpoint> endpoints)
    {
        if (endpoints == null || endpoints.Count == 0)
        {
            return Array.Empty<Candidate>();
        }
        
        var candiates = new Candidate[endpoints.Count];
        
        var score = 0;
        var examplar = endpoints[0];
        candiates[0] = CreateCandidate(examplar, score);
        
        for (var i = 1; i < endpoints.Count; i++)
        {
            var endpoint = endpoints[i];
            if (!_comparer.Equals(examplar, endpoint))
            {
                // This endpoint doesn't have the same priority.
                examplar = endpoint;
                score++;
            }
            
            candiates[i] = CreateCandidate(endpoint, score);
        }
        
        return candiates;
    }

    internal Candidate CreateCandidate(Endpoint endpoint, int score)
    {
        (string parameterName, 
         int segmentIndex, 
         int slotIndex) catchAll = default;
        
        if (endpoint is RouteEndpoint routeEndpoint)
        {
            _assignments.Clear();
            _slots.Clear();
            _captures.Clear();
            _complexSegments.Clear();
            _constraints.Clear();
            
            foreach (var kvp in routeEndpoint.RoutePattern.Defaults)
            {
                _assignments.Add(kvp.Key, _assignments.Count);
                _slots.Add(kvp);
            }
            
            for (var i = 0; i < routeEndpoint.RoutePattern.PathSegments.Count; i++)
            {
                var segment = routeEndpoint.RoutePattern.PathSegments[i];
                if (!segment.IsSimple)
                {
                    continue;
                }
                
                var parameterPart = segment.Parts[0] as RoutePatternParameterPart;
                if (parameterPart == null)
                {
                    continue;
                }
                
                if (!_assignments.TryGetValue(
                    	parameterPart.Name, 
                    	out var slotIndex))
                {
                    slotIndex = _assignments.Count;
                    _assignments.Add(parameterPart.Name, slotIndex);
                    
                    // A parameter can have a required value, default value/catch all, 
                    // or be a normal parameter
                    // Add the required value or default value as the slot's initial value
                    if (TryGetRequiredValue(
                        	routeEndpoint.RoutePattern, 
                        	parameterPart, 
                        	out var requiredValue))
                    {
                        _slots.Add(
                            new KeyValuePair<string, object>(
                                parameterPart.Name, 
                                requiredValue));
                    }
                    else
                    {
                        var hasDefaultValue = 
                            parameterPart.Default != null || 
                            parameterPart.IsCatchAll;
                        
                        _slots.Add(hasDefaultValue 
                                       ? new KeyValuePair<string, object>(
                                           parameterPart.Name, 
                                           parameterPart.Default) 
                                   	   : default);
                    }
                }
                
                if (TryGetRequiredValue(
                    	routeEndpoint.RoutePattern, 
                    	parameterPart, 
                    	out _))
                {
                    // Don't capture a parameter if it has a required value
                    // There is no need because a parameter 
                    // with a required value is matched as a literal
                }
                else if (parameterPart.IsCatchAll)
                {
                    catchAll = (parameterPart.Name, i, slotIndex);
                }
                else
                {
                    _captures.Add((parameterPart.Name, i, slotIndex));
                }
            }
            
            for (var i = 0; i < routeEndpoint.RoutePattern.PathSegments.Count; i++)
            {
                var segment = routeEndpoint.RoutePattern.PathSegments[i];
                if (segment.IsSimple)
                {
                    continue;
                }
                
                _complexSegments.Add((segment, i));
            }
            
            foreach (var kvp in routeEndpoint.RoutePattern.ParameterPolicies)
            {
                // may be null, that's ok
                var parameter = 
                    routeEndpoint
                    	.RoutePattern
                    	.GetParameter(kvp.Key); 
                var parameterPolicyReferences = kvp.Value;
                for (var i = 0; i < parameterPolicyReferences.Count; i++)
                {
                    var reference = parameterPolicyReferences[i];
                    var parameterPolicy =
                        _parameterPolicyFactory
                        	.Create(parameter, reference);
                    if (parameterPolicy is IRouteConstraint routeConstraint)
                    {
                        _constraints.Add(
                            new KeyValuePair<string, IRouteConstraint>(
                                kvp.Key, 
                                routeConstraint));
                    }
                }
            }
            
            return new Candidate(
                endpoint,
                score,
                _slots.ToArray(),
                _captures.ToArray(),
                catchAll,
                _complexSegments.ToArray(),
                _constraints.ToArray());
        }
        else
        {
            return new Candidate(
                endpoint,
                score,
                Array.Empty<KeyValuePair<string, object>>(),
                Array.Empty<(string parameterName, int segmentIndex, int slotIndex)>(),
                catchAll,
                Array.Empty<(RoutePatternPathSegment pathSegment, int segmentIndex)>(),
                Array.Empty<KeyValuePair<string, IRouteConstraint>>());
        }
    }
}

```

###### 2.6.4.2 build policy

```c#
internal class DfaMatcherBuilder : MatcherBuilder
{
    private static PolicyJumpTable BuildPolicy(
        int exitDestination, 
        INodeBuilderPolicy nodeBuilder, 
        PolicyJumpTableEdge[] policyEntries)
    {
        if (policyEntries == null)
        {
            return null;
        }
        
        return nodeBuilder.BuildJumpTable(exitDestination, policyEntries);
    }
}

```

##### 2.6.5 dfa matcher factory

```c#
internal class DfaMatcherFactory : MatcherFactory
{
    private readonly IServiceProvider _services;
    
    // Using the service provider here so we can avoid coupling to the dependencies
    // of DfaMatcherBuilder.
    public DfaMatcherFactory(IServiceProvider services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        
        _services = services;
    }
    
    public override Matcher CreateMatcher(EndpointDataSource dataSource)
    {
        if (dataSource == null)
        {
            throw new ArgumentNullException(nameof(dataSource));
        }
        
        // Creates a tracking entry in DI to stop listening for change events
        // when the services are disposed.
        var lifetime = _services.GetRequiredService<DataSourceDependentMatcher.Lifetime>();
        
        return new DataSourceDependentMatcher(dataSource, lifetime, () =>
        	{
                return _services.GetRequiredService<DfaMatcherBuilder>();
            });
    }
}

```















































##### 2.1.4 route value dictionary

```c#
public class RouteValueDictionary : 	
	IDictionary<string, object?>, 	
	IReadOnlyDictionary<string, object?>
{
    // 4 is a good default capacity here because 
    // that leaves enough space for area/controller/action/id
    private const int DefaultCapacity = 4;
        
    internal KeyValuePair<string, object?>[] _arrayStorage;
    internal PropertyStorage? _propertyStorage;
      
    bool ICollection<KeyValuePair<string, object?>>.IsReadOnly => false;
    
    private int _count;     
    public int Count => _count;
        
    // keys    
    public ICollection<string> Keys
    {
        get
        {
            EnsureCapacity(_count);
            
            var array = _arrayStorage;
            var keys = new string[_count];
            for (var i = 0; i < keys.Length; i++)
            {
                keys[i] = array[i].Key;
            }
            
            return keys;
        }
    }        
    IEnumerable<string> IReadOnlyDictionary<string, object?>.Keys => Keys;
        
    // values
    public ICollection<object?> Values
    {
        get
        {
            EnsureCapacity(_count);
            
            var array = _arrayStorage;
            var values = new object?[_count];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = array[i].Value;
            }
            
            return values;
        }
    }        
    IEnumerable<object?> IReadOnlyDictionary<string, object?>.Values => Values;
                           
    public IEqualityComparer<string> Comparer => StringComparer.OrdinalIgnoreCase;                                
    /// <inheritdoc />
    void ICollection<KeyValuePair<string, object?>>.CopyTo(
        KeyValuePair<string, object?>[] array,
        int arrayIndex)
    {
        if (array == null)
        {
            throw new ArgumentNullException(nameof(array));
        }
        
        if (arrayIndex < 0 || 
            arrayIndex > array.Length || 
            array.Length - arrayIndex < this.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }
        
        if (Count == 0)
        {
            return;
        }
        
        EnsureCapacity(Count);
        
        var storage = _arrayStorage;
        Array.Copy(storage, 0, array, arrayIndex, _count);
    }
                                       
    [DoesNotReturn]
    private static void ThrowArgumentNullExceptionForKey()
    {
        throw new ArgumentNullException("key");
    }            
}

```

###### 2.1.4.1 构造函数

```c#
public class RouteValueDictionary	
{
    // for empty dictionary
    public RouteValueDictionary()
    {
        _arrayStorage = Array.Empty<KeyValuePair<string, object?>>();
    }
      
    // by values
    public RouteValueDictionary(object? values)
    {
        /* values 是 route value dictionary */        
        if (values is RouteValueDictionary dictionary)
        {
            // 如果 property storage 不为 null，直接复制
            if (dictionary._propertyStorage != null)
            {
                // PropertyStorage is immutable so we can just copy it.
                _propertyStorage = dictionary._propertyStorage;
                _count = dictionary._count;
                _arrayStorage = Array.Empty<KeyValuePair<string, object?>>();
                return;
            }
            
            // 否则（property storage 为 null），
            // 从 array storage 复制
            var count = dictionary._count;
            if (count > 0)
            {
                var other = dictionary._arrayStorage;
                var storage = new KeyValuePair<string, object?>[count];
                Array.Copy(other, 0, storage, 0, count);
                _arrayStorage = storage;
                _count = count;
            }
            else
            {
                // array storage 为 null，建立 empty array
                _arrayStorage = Array.Empty<KeyValuePair<string, object?>>();
            }
            
            return;
        }
        
        /* values 实现了 IEnumerable<kvp<string,object>> 接口 */
        if (values is IEnumerable<KeyValuePair<string, object>> keyValueEnumerable)
        {
            // 复制到 array storage
            _arrayStorage = Array.Empty<KeyValuePair<string, object?>>();
                        
            foreach (var kvp in keyValueEnumerable)
            {
                Add(kvp.Key, kvp.Value);
            }
            
            return;
        }
        
        /* values 实现了 IEnumerable<kvp<string,string>> 接口 */
        if (values is IEnumerable<KeyValuePair<string, string>> stringValueEnumerable)
        {
            // 复制到 array storage 
            _arrayStorage = Array.Empty<KeyValuePair<string, object?>>();
            
            foreach (var kvp in stringValueEnumerable)
            {
                Add(kvp.Key, kvp.Value);
            }
            
            return;
        }
        
        /* values 为 null，创建
             - empty property storage
             - empty array storage */
        if (values != null)
        {
            var storage = new PropertyStorage(values);
            _propertyStorage = storage;
            _count = storage.Properties.Length;
            _arrayStorage = Array.Empty<KeyValuePair<string, object?>>();
        }
        else
        {
            _arrayStorage = Array.Empty<KeyValuePair<string, object?>>();
        }
    }        
}

```

###### 2.1.4.2 静态方法

```c#
public class RouteValueDictionary	
{
    public static RouteValueDictionary FromArray(KeyValuePair<string, object?>[] items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }
        
        // We need to compress the array by removing non-contiguous items. We
        // typically have a very small number of items to process. We don't need
        // to preserve order.
        var start = 0;
        var end = items.Length - 1;
        
        // We walk forwards from the beginning of the array and fill in 'null' slots.
        // We walk backwards from the end of the array end move items in non-null' slots
        // into whatever start is pointing to. O(n)
        while (start <= end)
        {
            if (items[start].Key != null)
            {
                start++;
            }
            else if (items[end].Key != null)
            {
                // Swap this item into start and advance
                items[start] = items[end];
                items[end] = default;
                start++;
                end--;
            }
            else
            {
                // Both null, we need to hold on 'start' since we
                // still need to fill it with something.
                end--;
            }
        }
        
        return new RouteValueDictionary()
        {
            _arrayStorage = items!,
            _count = start,
        };
    }
}

```

###### 2.1.4.3 enumerator

```c#
public class RouteValueDictionary	
{
    /// <inheritdoc />
    public Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }
    
    /// <inheritdoc />
    IEnumerator<KeyValuePair<string, object?>> 
        IEnumerable<KeyValuePair<string, object?>>.GetEnumerator()
    {
        return GetEnumerator();
    }
    
    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    
    public struct Enumerator : IEnumerator<KeyValuePair<string, object?>>
    {
        private readonly RouteValueDictionary _dictionary;
        private int _index;
        object IEnumerator.Current => Current;
        
        public KeyValuePair<string, object?> Current { get; private set; }
        
        public Enumerator(RouteValueDictionary dictionary)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException();
            }
            
            _dictionary = dictionary;            
            Current = default;
            _index = 0;
        }
                                                                   
        [MethodImpl(MethodImplOptions.AggressiveInlining)]        
        public bool MoveNext()            
        {
            var dictionary = _dictionary;
            
            // The uncommon case is that the propertyStorage is in use
            if (dictionary._propertyStorage == null && 
                ((uint)_index < (uint)dictionary._count))
            {
                Current = dictionary._arrayStorage[_index];
                _index++;
                return true;
            }
            
            return MoveNextRare();
        }

        private bool MoveNextRare()
        {
            var dictionary = _dictionary;
            if (dictionary._propertyStorage != null && 
                ((uint)_index < (uint)dictionary._count))
            {
                var storage = dictionary._propertyStorage;
                var property = storage.Properties[_index];
                Current = new KeyValuePair<string, object?>(
                    property.Name, 
                    property.GetValue(storage.Value));
                _index++;
                return true;
            }
            
            _index = dictionary._count;
            Current = default;
            return false;
        }
        
        public void Reset()
        {
            Current = default;
            _index = 0;
        }
        
        public void Dispose()
        {
        }
    }
}

```

###### 2.1.4.4 add

```c#
public class RouteValueDictionary	
{
    void ICollection<KeyValuePair<string, object?>>.Add(KeyValuePair<string, object?> item)
    {
        Add(item.Key, item.Value);
    }
    
    /// <inheritdoc />
    public void Add(string key, object? value)
    {
        if (key == null)
        {
            ThrowArgumentNullExceptionForKey();
        }
        
        EnsureCapacity(_count + 1);
        
        if (ContainsKeyArray(key))
        {
            var message = Resources
                .FormatRouteValueDictionary_DuplicateKey(
                	key, 
                	nameof(RouteValueDictionary));
            
            throw new ArgumentException(message, nameof(key));
        }
        
        _arrayStorage[_count] = new KeyValuePair<string, object?>(key, value);
        _count++;
    }
    
    public bool TryAdd(string key, object? value)
    {
        if (key == null)
        {
            ThrowArgumentNullExceptionForKey();
        }
        
        if (ContainsKeyCore(key))
        {
            return false;
        }
        
        EnsureCapacity(Count + 1);
        _arrayStorage[Count] = new KeyValuePair<string, object?>(key, value);
        _count++;
        return true;
    }
    
    
}
```



###### 2.1.4.5 remove

```c#
public class RouteValueDictionary	
{
    /// <inheritdoc />
    bool ICollection<KeyValuePair<string, object?>>
        .Remove(KeyValuePair<string, object?> item)
    {
        if (Count == 0)
        {
            return false;
        }
        
        Debug.Assert(_arrayStorage != null);        
        EnsureCapacity(Count);
        
        var index = FindIndex(item.Key);
        var array = _arrayStorage;
        if (index >= 0 && 
            EqualityComparer<object>
            	.Default.Equals(array[index].Value, item.Value))
        {
            Array.Copy(
                array, 
                index + 1, 
                array, 
                index, 
                _count - index);
            _count--;
            array[_count] = default;
            return true;
        }
        
        return false;
    }
    
    /// <inheritdoc />
    public bool Remove(string key)
    {
        if (key == null)
        {
            ThrowArgumentNullExceptionForKey();
        }        
        if (Count == 0)
        {
            eturn false;
            
        }
        
        // Ensure property storage is converted to array storage as we'll be
        // applying the lookup and removal on the array
        EnsureCapacity(_count);
        
        var index = FindIndex(key);
        if (index >= 0)
        {
            _count--;
            var array = _arrayStorage;
            Array.Copy(array, index + 1, array, index, _count - index);
            array[_count] = default;
            
            return true;
        }
        
        return false;
    }
            
    public bool Remove(string key, out object? value)
    {
        if (key == null)
        {
            ThrowArgumentNullExceptionForKey();
        }
        
        if (_count == 0)
        {
            value = default;
            return false;
        }
        
        // Ensure property storage is converted to array storage as we'll be
        // applying the lookup and removal on the array
        EnsureCapacity(_count);
        
        var index = FindIndex(key);
        if (index >= 0)
        {
            _count--;
            var array = _arrayStorage;
            value = array[index].Value;
            Array.Copy(array, index + 1, array, index, _count - index);
            array[_count] = default;
            
            return true;
        }
        
        value = default;
        return false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindIndex(string key)
    {
        // Generally the bounds checking here will be elided 
        // by the JIT because this will be called on the 
        // same code path as EnsureCapacity.
        var array = _arrayStorage;
        var count = _count;
        
        for (var i = 0; i < count; i++)
        {
            if (string.Equals(array[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        
        return -1;
    }
    
    /// <inheritdoc />
    public void Clear()
    {
        if (_count == 0)
        {
            return;
        }
        
        if (_propertyStorage != null)
        {
            _arrayStorage = Array.Empty<KeyValuePair<string, object?>>();
            _propertyStorage = null;
            _count = 0;
            return;
        }
        
        Array.Clear(_arrayStorage, 0, _count);
        _count = 0;
    }
}

```

###### 2.1.4.6 get

```c#
public class RouteValueDictionary	
{
    /// <inheritdoc />
    public bool TryGetValue(string key, out object? value)
    {
        if (key == null)
        {
            ThrowArgumentNullExceptionForKey();
        }
        if (_propertyStorage == null)
        {
            return TryFindItem(key, out value);
        }
        
        return TryGetValueSlow(key, out value);
    }
    
    private bool TryGetValueSlow(string key, out object? value)
    {
        if (_propertyStorage != null)
        {
            var storage = _propertyStorage;
            for (var i = 0; i < storage.Properties.Length; i++)
            {
                if (string.Equals(
                    	storage.Properties[i].Name, 
                    	key, 
                    	StringComparison.OrdinalIgnoreCase))
                    {
                        value = storage
                            .Properties[i]
                            .GetValue(storage.Value);
                        return true;
                    }
                }
            }

        value = default;
        return false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryFindItem(string key, out object? value)
    {
        var array = _arrayStorage;
        var count = _count;
        
        // Elide bounds check for indexing.
        if ((uint)count <= (uint)array.Length)
        {
            for (var i = 0; i < count; i++)
            {
                if (string.Equals(array[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = array[i].Value;
                    return true;
                }
            }
        }
        
        value = null;
        return false;
    }
}

```

###### 2.1.4.7 contains

```c#
public class RouteValueDictionary	
{
    /// <inheritdoc />
    bool ICollection<KeyValuePair<string, object?>>
        .Contains(KeyValuePair<string, object?> item)
    {
        return TryGetValue(item.Key, out var value) && 
               EqualityComparer<object>.Default.Equals(value, item.Value);
    }
    
    /// <inheritdoc />
    public bool ContainsKey(string key)
    {
        if (key == null)
        {
            ThrowArgumentNullExceptionForKey();
        }
        
        return ContainsKeyCore(key);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ContainsKeyCore(string key)
    {
        if (_propertyStorage == null)
        {
            return ContainsKeyArray(key);
        }
        
        return ContainsKeyProperties(key);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ContainsKeyArray(string key)
    {
        var array = _arrayStorage;
        var count = _count;
        
        // Elide bounds check for indexing.
        if ((uint)count <= (uint)array.Length)
        {
            for (var i = 0; i < count; i++)
            {
                if (string.Equals(
                    array[i].Key, 
                    key, 
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ContainsKeyProperties(string key)
    {
        Debug.Assert(_propertyStorage != null);
        
        var properties = _propertyStorage.Properties;
        for (var i = 0; i < properties.Length; i++)
        {
            if (string.Equals(
                properties[i].Name, 
                key, 
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        return false;
    }
}
```

###### 2.1.4.8 ensure capacity

```c#
public class RouteValueDictionary	
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int capacity)
    {
        if (_propertyStorage != null || _arrayStorage.Length < capacity)
        {
            EnsureCapacitySlow(capacity);
        }
    }
    
    private void EnsureCapacitySlow(int capacity)
    {
        if (_propertyStorage != null)
        {
            var storage = _propertyStorage;
            
            // If we're converting from properties, 
            // it's likely due to an 'add' to make sure we have at least
            // the default amount of space.
            capacity = Math.Max(
                DefaultCapacity, 
                Math.Max(
                    torage.Properties.Length, 
                    capacity));
            
            var array = new KeyValuePair<string, object?>[capacity];
            
            for (var i = 0; i < storage.Properties.Length; i++)
            {
                var property = storage.Properties[i];
                array[i] = new KeyValuePair<string, object?>(
                    property.Name, 
                    property.GetValue(storage.Value));
            }
            
            _arrayStorage = array;
            _propertyStorage = null;
            return;
        }
        
        if (_arrayStorage.Length < capacity)
        {
            capacity = _arrayStorage.Length == 0 
                ? DefaultCapacity 
                : _arrayStorage.Length * 2;
            var array = new KeyValuePair<string, object?>[capacity];
            if (_count > 0)
            {
                Array.Copy(_arrayStorage, 0, array, 0, _count);
            }
            
            _arrayStorage = array;
        }
    }    
}

```

###### 2.1.4.9 property store

```c#
public class RouteValueDictionary	
{
    internal class PropertyStorage
    {
        private static readonly ConcurrentDictionary<Type, PropertyHelper[]> 
            _propertyCache = new ConcurrentDictionary<Type, PropertyHelper[]>();
        
        public readonly object Value;
        public readonly PropertyHelper[] Properties;
        
        public PropertyStorage(object value)
        {
            Debug.Assert(value != null);
            Value = value;
            
            // Cache the properties so we can know 
            // if we've already validated them for duplicates.
            var type = Value.GetType();
            if (!_propertyCache.TryGetValue(type, out Properties!))
            {
                Properties = PropertyHelper.GetVisibleProperties(type);
                ValidatePropertyNames(type, Properties);
                _propertyCache.TryAdd(type, Properties);
            }
        }
        
        private static void ValidatePropertyNames(
            Type type, 
            PropertyHelper[] properties)
        {       
            var names = new Dictionary<string, PropertyHelper>
                (StringComparer.OrdinalIgnoreCase);
            
            for (var i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                
                if (names.TryGetValue(property.Name, out var duplicate))
                {
                    var message = Resources
                        .FormatRouteValueDictionary_DuplicatePropertyName(
                        	type.FullName,
                        	property.Name,
                        	duplicate.Name,
                        	nameof(RouteValueDictionary));
                    throw new InvalidOperationException(message);
                }
                
                names.Add(property.Name, property);
            }
        }
    }
}

```

#### 

##### 



###### 



###### 

















