using System.Runtime.InteropServices;
using System.Text.Json;
using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace Prospice.Diagnostics;

[BepInPlugin("local.prospice.diagnostics", "Prospice Read-Only Diagnostics", "0.1.0")]
public sealed class Plugin : BasePlugin
{
    public override void Load()
    {
        var output = Config.Bind("Diagnostics", "OutputDirectory", Path.Combine(Paths.BepInExRootPath, "diagnostics"), "Local read-only type inventory output.").Value;
        var types = new[] { "PlayerInteraction", "FPActions", "PlayerHeldItem", "Pickupable", "Packable", "TruckBed", "PlayerController", "PlayerCore", "PauseManager" };
        var domain = Native.il2cpp_domain_get();
        var assemblies = Native.il2cpp_domain_get_assemblies(domain, out var count);
        nint image = 0;
        for (nuint i = 0; i < count; i++)
        {
            var assembly = Marshal.ReadIntPtr(assemblies, checked((int)i * IntPtr.Size));
            var candidate = Native.il2cpp_assembly_get_image(assembly);
            if (Native.String(Native.il2cpp_image_get_name(candidate)) == "Assembly-CSharp.dll") image = candidate;
        }
        if (image == 0) throw new InvalidOperationException("Assembly-CSharp runtime image not found.");
        var report = new List<object>();
        foreach (var name in types)
        {
            var klass = Native.il2cpp_class_from_name(image, "", name);
            if (klass == 0) throw new InvalidOperationException($"Runtime class not found: {name}");
            var fields = new List<object>();
            nint iterator = 0, member;
            while ((member = Native.il2cpp_class_get_fields(klass, ref iterator)) != 0)
                fields.Add(new { name = Native.String(Native.il2cpp_field_get_name(member)), type = Native.TypeName(Native.il2cpp_field_get_type(member)), offset = (long)Native.il2cpp_field_get_offset(member), flags = Native.il2cpp_field_get_flags(member) });
            var methods = new List<object>();
            iterator = 0;
            while ((member = Native.il2cpp_class_get_methods(klass, ref iterator)) != 0)
            {
                var parameters = new List<object>();
                var n = Native.il2cpp_method_get_param_count(member);
                for (uint j = 0; j < n; j++) parameters.Add(new { name = Native.String(Native.il2cpp_method_get_param_name(member, j)), type = Native.TypeName(Native.il2cpp_method_get_param(member, j)) });
                methods.Add(new { name = Native.String(Native.il2cpp_method_get_name(member)), returns = Native.TypeName(Native.il2cpp_method_get_return_type(member)), parameters, token = Native.il2cpp_method_get_token(member) });
            }
            report.Add(new { name, fields, methods });
        }
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "runtime-types.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Log.LogInfo($"PROSPICE_DIAGNOSTICS_OK: inspected {report.Count} native classes; no gameplay methods invoked.");
    }
}

internal static class Native
{
    private const string Library = "GameAssembly";
    internal static string String(nint p) => Marshal.PtrToStringUTF8(p) ?? "";
    internal static string TypeName(nint p)
    {
        var name = il2cpp_type_get_name(p);
        try { return String(name); }
        finally { if (name != 0) il2cpp_free(name); }
    }
    [DllImport(Library)] internal static extern nint il2cpp_domain_get();
    [DllImport(Library)] internal static extern nint il2cpp_domain_get_assemblies(nint domain, out nuint count);
    [DllImport(Library)] internal static extern nint il2cpp_assembly_get_image(nint assembly);
    [DllImport(Library)] internal static extern nint il2cpp_image_get_name(nint image);
    [DllImport(Library)] internal static extern nint il2cpp_class_from_name(nint image, [MarshalAs(UnmanagedType.LPUTF8Str)] string ns, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport(Library)] internal static extern nint il2cpp_class_get_fields(nint klass, ref nint iterator);
    [DllImport(Library)] internal static extern nint il2cpp_field_get_name(nint field);
    [DllImport(Library)] internal static extern nint il2cpp_field_get_type(nint field);
    [DllImport(Library)] internal static extern nint il2cpp_field_get_offset(nint field);
    [DllImport(Library)] internal static extern int il2cpp_field_get_flags(nint field);
    [DllImport(Library)] internal static extern nint il2cpp_class_get_methods(nint klass, ref nint iterator);
    [DllImport(Library)] internal static extern nint il2cpp_method_get_name(nint method);
    [DllImport(Library)] internal static extern nint il2cpp_method_get_return_type(nint method);
    [DllImport(Library)] internal static extern uint il2cpp_method_get_param_count(nint method);
    [DllImport(Library)] internal static extern nint il2cpp_method_get_param(nint method, uint index);
    [DllImport(Library)] internal static extern nint il2cpp_method_get_param_name(nint method, uint index);
    [DllImport(Library)] internal static extern uint il2cpp_method_get_token(nint method);
    [DllImport(Library)] internal static extern nint il2cpp_type_get_name(nint type);
    [DllImport(Library)] internal static extern void il2cpp_free(nint pointer);
}
