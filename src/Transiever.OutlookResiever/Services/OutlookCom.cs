
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

        dynamic dynValue = value;
        if (TryGetInt(() => dynValue.Count, out int count))
        {
            for (var index = 1; index <= count; index++)
            {
                object? item = null;
                try
                {
                    item = dynValue.Item(index);
                }
                catch
                {
                    yield break;
                }

                yield return item;
            }

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

    public static int SafeInt(Func<object?> getter, int fallback = 0)
    {
        try
        {
            var value = getter();

            return value switch
            {
                int i => i,
                null => fallback,
                _ => Convert.ToInt32(value)
            };
        }
        catch
        {
            return fallback;
        }
    }

    private static bool TryGetInt(Func<object?> getter, out int result)
    {
        try
        {
            result = Convert.ToInt32(getter());
            return true;
        }
        catch
        {
            result = 0;
            return false;
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
