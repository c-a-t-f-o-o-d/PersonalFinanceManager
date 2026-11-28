using System.Text.Json;

namespace PersonalFinanceManager.Data.Repositories;

internal static class RecurringBillAliasJson
{
    public static IReadOnlyList<string> Read(string value)
    {
        return JsonSerializer.Deserialize<string[]>(value) ?? [];
    }

    public static string Write(IReadOnlyList<string> values)
    {
        return JsonSerializer.Serialize(values);
    }
}
