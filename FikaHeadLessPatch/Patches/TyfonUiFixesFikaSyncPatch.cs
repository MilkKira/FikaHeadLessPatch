using System;
using System.Reflection;
using HarmonyLib;

namespace FikaHeadLessPatch.Patches;

[HarmonyPatch]
internal static class TyfonUiFixesFikaSyncPatch
{
    private static bool _logged;

    private static bool Prepare()
    {
        return FikaHeadlessStuckFixPlugin.DisableTyfonUiFixesFikaSync.Value;
    }

    private static MethodBase? TargetMethod()
    {
        var type = FindType("UIFixes.Fika.Sync");
        return type == null ? null : AccessTools.Method(type, "OnPeerConnected");
    }

    private static bool Prefix()
    {
        if (!_logged)
        {
            FikaHeadlessStuckFixPlugin.Log.LogWarning(
                "Skipping Tyfon.UIFixes Fika peer sync on headless host to avoid incompatible NetDataWriter.Put(string).");
            _logged = true;
        }

        return false;
    }

    private static Type? FindType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? type = null;

            try
            {
                type = assembly.GetType(fullName, false);
            }
            catch
            {
                // Some game assemblies can throw while resolving metadata; they are irrelevant here.
            }

            if (type != null)
            {
                return type;
            }
        }

        return null;
    }
}
