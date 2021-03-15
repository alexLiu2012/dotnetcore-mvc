## about routing



### 1. about



### 2. details











#### 2.4 matcher 组件



















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

#### 2.6 创建 matcher





##### 2.6.2 dfa matcher builder

```c#

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













































