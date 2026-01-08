public static class ListExtensions
{
    public static T? Next<T>(this List<T> list, T item) where T : class
    {
        if (item == null)
            return default;

        var index = list.IndexOf(item);
        if (index == -1)
            return default;

        if (item.Equals(list.LastOrDefault()))
            return default;

        return list[index + 1];
    }
}