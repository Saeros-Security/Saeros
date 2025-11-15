using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Collector.Databases.Implementation.Contexts.Tracing;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Users;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Workstations;
using Collector.Databases.Implementation.Stores.Tracing.Data;
using QuikGraph;
using Shared.Models.Console.Requests;

namespace Collector.Databases.Implementation.Stores.Tracing;

internal sealed class GraphBuilder(TracingContext context, UserBucket userBucket, WorkstationBucket workstationBucket)
{
    private static bool TryGetDataFromProcessNode(TracingNode node, [MaybeNullWhen(false)] out string sid, [MaybeNullWhen(false)] out string workstationName)
    {
        sid = null;
        workstationName = null;
        var userData = JsonSerializer.Deserialize<JsonDocument>(node.JsonUserData)?.RootElement;
        if (userData.HasValue &&
            userData.Value.TryGetProperty(nameof(ProcessData.UserSid), out var userSid) &&
            userData.Value.TryGetProperty(nameof(ProcessData.WorkstationName), out var workstation) &&
            userData.Value.TryGetProperty(nameof(ProcessData.ProcessId), out var pId) &&
            userData.Value.TryGetProperty(nameof(ProcessData.ProcessName), out var pName))
        {
            sid = userSid.GetString();
            workstationName = workstation.GetString();
            var processId = pId.GetString();
            var processName = pName.GetString();
            if (!string.IsNullOrEmpty(sid) && !string.IsNullOrEmpty(workstationName) && !string.IsNullOrEmpty(processId) && !string.IsNullOrEmpty(processName))
            {
                return true;
            }
        }

        return false;
    }
    
    private static bool TryGetDataFromWorkstationNode(TracingNode node, [MaybeNullWhen(false)] out string workstationName)
    {
        workstationName = null;
        var userData = JsonSerializer.Deserialize<JsonDocument>(node.JsonUserData)?.RootElement;
        if (userData.HasValue && userData.Value.TryGetProperty(nameof(WorkstationData.WorkstationName), out var name))
        {
            workstationName = name.GetString();
            return !string.IsNullOrEmpty(workstationName);
        }

        return false;
    }
    
    private static bool TryGetDataFromUserNode(TracingNode node, [MaybeNullWhen(false)] out string sid)
    {
        sid = null;
        var userData = JsonSerializer.Deserialize<JsonDocument>(node.JsonUserData)?.RootElement;
        if (userData.HasValue && userData.Value.TryGetProperty(nameof(UserData.Sid), out var userSid))
        {
            sid = userSid.GetString();
            return !string.IsNullOrEmpty(sid);
        }

        return false;
    }

    public async Task<AdjacencyGraph<TracingNode, IEdge<TracingNode>>> BuildAsync(TracingQuery query, CancellationToken cancellationToken)
    {
        var graph = new AdjacencyGraph<TracingNode, IEdge<TracingNode>>(allowParallelEdges: false);

        var currentUsers = new HashSet<TracingNode>();
        var currentWorkstations = new HashSet<TracingNode>();
        var currentProcesses = new HashSet<TracingNode>();
        
        await using var sqliteConnection = context.CreateConnection();
        await sqliteConnection.OpenAsync(cancellationToken);
        var exit = false;
        await foreach (var userEdge in userBucket.EnumerateEdgesAsync(sqliteConnection, query, cancellationToken))
        {
            if (exit) break;
            var userWorkstationGraph = new AdjacencyGraph<TracingNode, IEdge<TracingNode>>(allowParallelEdges: false);
            Add(userWorkstationGraph, Edge.Create(userEdge.SourceNode, userEdge.TargetNode), Commit.User | Commit.Workstation);
            if (query.SearchTerms.ContainsKey(TracingSearchType.ProcessName) || query.SearchTerms.ContainsKey(TracingSearchType.ProcessTime))
            {
                await foreach (var workstationEdge in workstationBucket.EnumerateEdgesAsync(sqliteConnection, userEdge.Key, userEdge.Target, userEdge.TargetNode, query, cancellationToken))
                {
                    if (exit) break;
                    var workstationProcessGraph = new AdjacencyGraph<TracingNode, IEdge<TracingNode>>(allowParallelEdges: false);
                    Add(userWorkstationGraph, Edge.Create(workstationEdge.SourceNode, workstationEdge.TargetNode), Commit.Process, workstationProcessGraph);
                }
            }
        }
        
        if (query.SearchTerms.ContainsKey(TracingSearchType.LogonId) && currentUsers.Count == 0)
        {
            return new AdjacencyGraph<TracingNode, IEdge<TracingNode>>();
        }
        
        if (query.SearchTerms.ContainsKey(TracingSearchType.UserName) && currentUsers.Count == 0)
        {
            return new AdjacencyGraph<TracingNode, IEdge<TracingNode>>();
        }

        if (query.SearchTerms.ContainsKey(TracingSearchType.UserSid) && currentUsers.Count == 0)
        {
            return new AdjacencyGraph<TracingNode, IEdge<TracingNode>>();
        }
        
        if (query.SearchTerms.ContainsKey(TracingSearchType.IpAddressUser) && currentUsers.Count == 0)
        {
            return new AdjacencyGraph<TracingNode, IEdge<TracingNode>>();
        }

        if (query.SearchTerms.ContainsKey(TracingSearchType.WorkstationName) && currentWorkstations.Count == 0)
        {
            return new AdjacencyGraph<TracingNode, IEdge<TracingNode>>();
        }

        if (query.SearchTerms.ContainsKey(TracingSearchType.ProcessName) && currentProcesses.Count == 0)
        {
            return new AdjacencyGraph<TracingNode, IEdge<TracingNode>>();
        }
        
        var workstationNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var userSids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (query.SearchTerms.ContainsKey(TracingSearchType.ProcessName) || query.SearchTerms.ContainsKey(TracingSearchType.ProcessTime))
        {
            foreach (var process in currentProcesses)
            {
                if (TryGetDataFromProcessNode(process, out var sid, out var workstationName))
                {
                    userSids.Add(sid);
                    workstationNames.Add(workstationName);
                }
            }
        }

        foreach (var workstation in currentWorkstations)
        {
            if (TryGetDataFromWorkstationNode(workstation, out var workstationName))
            {
                if (workstationNames.Count > 0 && !workstationNames.Contains(workstationName))
                {
                    graph.RemoveVertex(workstation);
                }
            }
        }

        foreach (var user in currentUsers)
        {
            if (TryGetDataFromUserNode(user, out var sid))
            {
                if (userSids.Count > 0 && !userSids.Contains(sid))
                {
                    graph.RemoveVertex(user);
                }
            }
        }

        return graph;

        void Add(AdjacencyGraph<TracingNode, IEdge<TracingNode>> userWorkstationGraph,
            IEdge<TracingNode> edge,
            Commit commit,
            AdjacencyGraph<TracingNode, IEdge<TracingNode>>? workstationProcessGraph = null)
        {
            if (query.SearchTerms.Any())
            {
                if (commit.HasFlag(Commit.User) || commit.HasFlag(Commit.Workstation))
                {
                    userWorkstationGraph.AddVerticesAndEdge(edge);
                }
                else if (commit.HasFlag(Commit.Process) && workstationProcessGraph is not null)
                {
                    workstationProcessGraph.AddVerticesAndEdge(edge);
                }
            }
            else
            {
                if (commit.HasFlag(Commit.User) || commit.HasFlag(Commit.Workstation))
                {
                    if (currentUsers.Count >= query.MaxUsers && !currentUsers.Contains(edge.Source) && currentWorkstations.Count > 0)
                    {
                        exit = true;
                        return;
                    }

                    if (currentWorkstations.Count >= query.MaxWorkstations && !currentWorkstations.Contains(edge.Target))
                    {
                        exit = true;
                        return;
                    }

                    graph.AddVerticesAndEdge(edge);
                    currentUsers.Add(edge.Source);
                    currentWorkstations.Add(edge.Target);
                }
                else if (commit.HasFlag(Commit.Process))
                {
                    if (currentWorkstations.Count >= query.MaxWorkstations && !currentWorkstations.Contains(edge.Source) && currentProcesses.Count > 0)
                    {
                        exit = true;
                        return;
                    }

                    if (currentProcesses.Count >= query.MaxProcesses && !currentProcesses.Contains(edge.Target))
                    {
                        exit = true;
                        return;
                    }

                    graph.AddVerticesAndEdge(edge);
                    currentWorkstations.Add(edge.Source);
                    currentProcesses.Add(edge.Target);
                }

                return;
            }

            if (commit.HasFlag(Commit.User))
            {
                foreach (var item in userWorkstationGraph.Edges)
                {
                    if (currentUsers.Count >= query.MaxUsers && !currentUsers.Contains(edge.Source) && currentWorkstations.Count > 0)
                    {
                        return;
                    }

                    if (currentWorkstations.Count >= query.MaxWorkstations && !currentWorkstations.Contains(edge.Target))
                    {
                        return;
                    }

                    graph.AddVerticesAndEdge(item);
                    currentUsers.Add(edge.Source);
                    currentWorkstations.Add(edge.Target);
                }
            }

            if (commit.HasFlag(Commit.Process) && workstationProcessGraph is not null)
            {
                foreach (var item in workstationProcessGraph.Edges)
                {
                    if (currentWorkstations.Count >= query.MaxWorkstations && !currentWorkstations.Contains(edge.Source) && currentProcesses.Count > 0)
                    {
                        return;
                    }

                    if (currentProcesses.Count >= query.MaxProcesses && !currentProcesses.Contains(edge.Target))
                    {
                        return;
                    }

                    graph.AddVerticesAndEdge(item);
                    currentWorkstations.Add(edge.Source);
                    currentProcesses.Add(edge.Target);
                }
            }
        }
    }

    [Flags]
    private enum Commit
    {
        User = 1,
        Workstation = 2,
        Process = 4
    }
}