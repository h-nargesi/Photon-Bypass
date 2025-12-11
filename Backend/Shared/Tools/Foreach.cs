namespace PhotonBypass.Tools;

public static class LoopExtensions
{
    public static void Foreach<T>(this IEnumerable<T> list, Action<T> action)
    {
        foreach (var item in list) action.Invoke(item);
    }
}