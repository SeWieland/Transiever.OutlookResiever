
using System.Collections;
using System.Runtime.InteropServices;

namespace Transiever.OutlookResiever.Services;

internal static class OutlookCom
{
    public static IEnumerable<object?> AsEnumerable(object? value)
    {
        if (value is null)
            yield break;

        if (value is string)
        {
            yield return value;
            yield break;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
                yield return item;

            yield break;
        }

        yield return value;
    }

    public static bool SafeBool(Func<object?> getter)
    {
        try
        {
            var value = getter();

            return value switch
            {
                bool b => b,
                null => false,
                _ => Convert.ToBoolean(value)
            };
        }
        catch
        {
            return false;
        }
    }

    public static string SafeString(Func<object?> getter)
    {
        try
        {
            return getter()?.ToString() ?? "";
        }
        catch
        {
            return "";
        }
    }

    public static string TryGetRuleName(object? rule)
    {
        if (rule is null)
            return "<unknown>";

        try
        {
            dynamic dynRule = rule;
            return dynRule.Name?.ToString() ?? "<unknown>";
        }
        catch
        {
            return "<unknown>";
        }
    }

    public static void Release(object? value)
    {
        try
        {
            if (value is not null && Marshal.IsComObject(value))
                Marshal.FinalReleaseComObject(value);
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }
}
