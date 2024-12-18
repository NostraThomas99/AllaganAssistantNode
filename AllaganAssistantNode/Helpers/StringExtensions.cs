namespace AllaganAssistantNode.Helpers;

public static class StringExtensions
{
    public static string ToPrettyNullString(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Loading...";
        return value;
    }
}