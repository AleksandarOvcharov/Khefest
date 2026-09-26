using System.Reflection;
using System.Text;

namespace Khefest.Tests;

/// <summary>
/// Extracts deterministic, canonical API signatures for public types and members
/// to detect breaking changes against locked API baselines.
/// </summary>
public static class ApiSignatureExtractor
{
    public static List<string> ExtractPublicSignatures(params Assembly[] assemblies)
    {
        var signatures = new List<string>();

        foreach (var assembly in assemblies.OrderBy(a => a.GetName().Name))
        {
            var types = assembly.GetExportedTypes()
                .Where(t => !IsInternalDriverType(t))
                .OrderBy(t => t.FullName)
                .ToList();

            foreach (var type in types)
            {
                // Format type declaration
                signatures.Add(FormatTypeDeclaration(type));

                // Constructors
                foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).OrderBy(c => FormatParameters(c.GetParameters())))
                {
                    signatures.Add($"  ctor {type.Name}({FormatParameters(ctor.GetParameters())})");
                }

                // Properties
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(p => p.Name))
                {
                    var accessors = (prop.CanRead ? "get;" : "") + (prop.CanWrite ? "set;" : "");
                    signatures.Add($"  prop {FormatTypeName(prop.PropertyType)} {prop.Name} {{ {accessors} }}");
                }

                // Methods (excluding property getters/setters and event add/remove)
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName)
                    .OrderBy(m => m.Name)
                    .ThenBy(m => FormatParameters(m.GetParameters())))
                {
                    var genericArgs = method.IsGenericMethod ? $"<{string.Join(", ", method.GetGenericArguments().Select(g => g.Name))}>" : "";
                    signatures.Add($"  method {FormatTypeName(method.ReturnType)} {method.Name}{genericArgs}({FormatParameters(method.GetParameters())})");
                }

                // Events
                foreach (var evt in type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(e => e.Name))
                {
                    signatures.Add($"  event {FormatTypeName(evt.EventHandlerType!)} {evt.Name}");
                }

                // Fields (for enums, constants, palettes)
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(f => f.Name))
                {
                    signatures.Add($"  field {FormatTypeName(field.FieldType)} {field.Name}");
                }
            }
        }

        return signatures;
    }

    private static bool IsInternalDriverType(Type t)
    {
        var ns = t.Namespace ?? "";
        return ns.EndsWith(".Windows") || ns.Contains(".Windows.") || t.Name.Contains("Native");
    }

    private static string FormatTypeDeclaration(Type t)
    {
        string kind = t.IsInterface ? "interface" : (t.IsValueType ? (t.IsEnum ? "enum" : "struct") : "class");
        var interfaces = t.GetInterfaces().Select(i => FormatTypeName(i)).OrderBy(s => s).ToList();
        var ifaceStr = interfaces.Count > 0 ? " : " + string.Join(", ", interfaces) : "";
        return $"{kind} {t.Namespace}.{t.Name}{ifaceStr}";
    }

    private static string FormatParameters(ParameterInfo[] parameters)
    {
        return string.Join(", ", parameters.Select(p =>
        {
            var modifier = p.IsIn ? "in " : (p.IsOut ? "out " : (p.ParameterType.IsByRef ? "ref " : ""));
            return $"{modifier}{FormatTypeName(p.ParameterType)} {p.Name}";
        }));
    }

    private static string FormatTypeName(Type type)
    {
        if (type.IsByRef)
            return FormatTypeName(type.GetElementType()!);

        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition().Name;
            var backtick = genericDef.IndexOf('`');
            if (backtick > 0) genericDef = genericDef[..backtick];
            var args = string.Join(", ", type.GetGenericArguments().Select(FormatTypeName));
            return $"{genericDef}<{args}>";
        }

        return type.Name switch
        {
            "Void" => "void",
            "Int32" => "int",
            "Single" => "float",
            "Double" => "double",
            "Boolean" => "bool",
            "Byte" => "byte",
            "Int64" => "long",
            "String" => "string",
            _ => type.Name
        };
    }
}
