using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace SimpleJsonRpc.Delegates;

internal abstract class TypeUtils
{
    public static bool IsSerializable(Type type, JsonSerializerOptions options)
    {
        if (type == typeof(void)) return true;
        try
        {
            object? instance = RuntimeHelpers.GetUninitializedObject(type);
            JsonSerializer.Serialize(instance, type, options);
            return true;
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or JsonException)
        {
            return false;
        }
    }

    public static bool IsDeserializable(Type type, JsonSerializerOptions options)
    {
        try
        {
            return options.TryGetTypeInfo(type, out var info) && info.CreateObject != null;
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentNullException)
        {
            return false;
        }
    }

    public static Type ExtractType(Type type)
    {
        if (type == typeof(ValueTask) || type == typeof(Task))
            return typeof(void);


        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>))
            return type.GetGenericArguments()[0];


        if (typeof(Task).IsAssignableFrom(type))
        {
            for (var baseType = type; baseType != null; baseType = baseType.BaseType)
            {
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    return baseType.GetGenericArguments()[0];
                }
            }

            return typeof(void);
        }

        return type;
    }
}