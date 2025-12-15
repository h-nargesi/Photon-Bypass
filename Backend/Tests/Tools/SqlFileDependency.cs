using System.Text.RegularExpressions;

namespace PhotonBypass.Test.Tools;

public static class SqlFileDependencyHelper
{
    public static async Task<List<string>> GetSortedFiles(string path)
    {
        var context = new List<Node>();
        await LoadFiles(context, path);

        var graph = BuildGraph(context);

        if (HasCycle(graph))
        {
            throw new Exception("Cycle detected");
        }

        return TopologicalSort(graph);
    }

    private static async Task LoadFiles(List<Node> context, string path)
    {
        foreach (var dir in Directory.GetDirectories(path))
            await LoadFiles(context, dir);

        var list = Directory.GetFiles(path)
            .Select(p => new
            {
                Name = Path.GetFileNameWithoutExtension(p),
                Content = File.ReadAllTextAsync(p),
            });

        foreach (var file in list)
        {
            if (Path.GetExtension(file.Name) != "sql") continue;

            context.Add(new Node(file.Name, await file.Content));
        }
    }

    private static Graph BuildGraph(List<Node> records)
    {
        var graph = new Graph(records);

        foreach (var r in records)
        {
            graph.AdjList[r.Name] = [];
            graph.InDegree[r.Name] = 0;
        }

        foreach (var target in records)
        {
            foreach (var source in records.Where(source => source != target &&
                                                           source.Regex.IsMatch(target.Content)))
            {
                graph.AdjList[source.Name].Add(target);
                graph.InDegree[target.Name]++;
            }
        }

        return graph;
    }

    private static bool HasCycle(Graph graph)
    {
        var states = new Dictionary<string, int>();

        foreach (var node in graph.Nodes)
            states[node.Name] = 0;

        return graph.Nodes.Any(node => states[node.Name] == 0 && Dfs(node.Name, graph, states));
    }

    private static bool Dfs(string name, Graph graph, Dictionary<string, int> states)
    {
        states[name] = 1;

        if (graph.AdjList[name].Any(neighbor => states[neighbor.Name] == 1 ||
                                                states[neighbor.Name] == 0 && Dfs(neighbor.Name, graph, states)))
        {
            return true;
        }

        states[name] = 2;
        return false;
    }

    private static List<string> TopologicalSort(Graph graph)
    {
        var queue = new Queue<Node>();
        var result = new List<string>();

        foreach (var node in graph.Nodes.Where(node => graph.InDegree[node.Name] == 0))
            queue.Enqueue(node);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current.Content);

            foreach (var neighbor in graph.AdjList[current.Name])
            {
                graph.InDegree[neighbor.Name]--;
                if (graph.InDegree[neighbor.Name] == 0)
                    queue.Enqueue(neighbor);
            }
        }

        return result.Count != graph.Nodes.Count
            ? throw new Exception("Cycle detected – Topological sort not possible")
            : result;
    }

    private class Graph(List<Node> nodes)
    {
        public List<Node> Nodes { get; } = nodes;
        public Dictionary<string, List<Node>> AdjList { get; } = [];
        public Dictionary<string, int> InDegree { get; } = [];
    }

    private class Node(string name, string content)
    {
        public string Name { get; } = name;

        public string Content { get; } = content;

        public Regex Regex { get; } = new Regex($@"\b{name}\b");
    }
}