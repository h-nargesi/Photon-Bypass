using System.Text.RegularExpressions;

namespace PhotonBypass.Sql;

public static class SqlFileDependencyHelper
{
    public static async Task<List<string>> GetSortedFiles(string path)
    {
        if (!Directory.Exists(path))
        {
            return [];
        }

        var context = new List<Node>();
        await LoadFiles(context, path);

        var graph = BuildGraph(context);

        if (HasCycle(graph, out var message))
        {
            throw new Exception("Cycle detected: " + message);
        }

        return TopologicalSort(graph);
    }

    private static async Task LoadFiles(List<Node> context, string path)
    {
        foreach (var dir in Directory.GetDirectories(path))
            await LoadFiles(context, dir);

        var list = Directory.GetFiles(path)
            .Where(p => Path.GetExtension(p) == ".sql")
            .Select(p => new Node(Path.GetFileNameWithoutExtension(p), File.ReadAllText(p)));

        context.AddRange(list);
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

    private static bool HasCycle(Graph graph, out string? message)
    {
        var states = new Dictionary<string, int>();

        foreach (var node in graph.Nodes)
            states[node.Name] = 0;

        var result = graph.Nodes.Any(node => states[node.Name] == 0 && Dfs(node.Name, graph, states));
        message = states.Where(p => p.Value == 3).Select(p => p.Key).FirstOrDefault();
        return result;
    }

    private static bool Dfs(string name, Graph graph, Dictionary<string, int> states)
    {
        states[name] = 1;

        if (graph.AdjList[name].Any(neighbor => states[neighbor.Name] == 1 ||
                                                states[neighbor.Name] == 0 && Dfs(neighbor.Name, graph, states)))
        {
            states[name] = 3;
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
        public Dictionary<string, HashSet<Node>> AdjList { get; } = [];
        public Dictionary<string, int> InDegree { get; } = [];
    }

    private class Node
    {
        public Node(string name, string content)
        {
            if (content.StartsWith("-- Object:"))
            {
                var end = content.IndexOf('\n');
                name = content[10..end].Trim();
            }

            Name = name;
            Content = content;
            Regex = new Regex($@"\b{Name}\b", RegexOptions.Compiled);
        }

        public string Name { get; }

        public string Content { get; }

        public Regex Regex { get; }

        public override string ToString() => Name;
    }
}