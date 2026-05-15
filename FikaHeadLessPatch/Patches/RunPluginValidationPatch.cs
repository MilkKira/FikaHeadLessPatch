using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx.Bootstrap;
using Diz.Utils;
using Fika.Core;
using Fika.Headless;
using HarmonyLib;
using UnityEngine;

namespace FikaHeadLessPatch.Patches;

[HarmonyPatch(typeof(FikaHeadlessPlugin), nameof(FikaHeadlessPlugin.RunPluginValidation))]
internal static class RunPluginValidationPatch
{
    private static bool Prefix(FikaHeadlessPlugin __instance, ref Task __result)
    {
        __result = SafeRunPluginValidation(__instance);
        return false;
    }

    private static async Task SafeRunPluginValidation(FikaHeadlessPlugin plugin)
    {
        if (HeadlessReflection.GetHasVerified(plugin))
        {
            return;
        }

        FikaHeadlessStuckFixPlugin.Log.LogInfo("Running safe Fika.Headless plugin validation");
        await WaitForLocales().ConfigureAwait(false);

        var invalidPlugins = FindInvalidPlugins();
        var invalidPluginsFound = invalidPlugins.Count > 0;
        HeadlessReflection.SetInvalidPluginsFound(plugin, invalidPluginsFound);

        if (invalidPluginsFound)
        {
            var modsString = string.Join("; ", invalidPlugins);
            FikaHeadlessStuckFixPlugin.Log.LogFatal(
                $"{invalidPlugins.Count} invalid plugins found. Remove these mods: {modsString}");

            HeadlessReflection.SetHasVerified(plugin, true);

            if (FikaHeadlessStuckFixPlugin.QuitOnInvalidPlugins.Value)
            {
                RunOnMainThread(() => Application.Quit(1));
            }

            return;
        }

        FikaHeadlessStuckFixPlugin.Log.LogInfo("Plugins verified successfully");

        await Task.Delay(1000).ConfigureAwait(false);
        ApplyHeadlessFikaSettings();

        var startupDelay = FikaHeadlessStuckFixPlugin.ValidationStartupDelaySeconds.Value;
        if (startupDelay > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(startupDelay)).ConfigureAwait(false);
        }

        RunOnMainThread(() => HeadlessReflection.InvokeCreateHeadlessWebsocket(plugin));
        HeadlessReflection.SetHasVerified(plugin, true);
    }

    private static async Task WaitForLocales()
    {
        var timeoutSeconds = FikaHeadlessStuckFixPlugin.LocaleLoadTimeoutSeconds.Value;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (!AreLocalesLoaded())
        {
            if (timeoutSeconds > 0 && stopwatch.Elapsed >= TimeSpan.FromSeconds(timeoutSeconds))
            {
                FikaHeadlessStuckFixPlugin.Log.LogWarning(
                    $"Timed out waiting {timeoutSeconds} seconds for Fika.Core locales; continuing validation.");
                return;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }
    }

    private static bool AreLocalesLoaded()
    {
        try
        {
            return FikaPlugin.Instance != null && FikaPlugin.Instance.LocalesLoaded;
        }
        catch
        {
            return false;
        }
    }

    private static List<string> FindInvalidPlugins()
    {
        var invalidPluginGuids = new HashSet<string>
        {
            "com.Amanda.Graphics",
            "com.Amanda.Sense",
            "VIP.TommySoucy.MoreCheckmarks",
            "com.kmyuhkyuk.EFTApi",
            "com.mpstark.DynamicMaps",
            "IhanaMies.LootValue",
            "com.cactuspie.ramcleanerinterval",
            "com.TYR.DeClutter",
            "SPTVRAMCleaner.UniqueGUID",
            "harmonyzt.sptleaderboard",
            "com.acidphantasm.stattrack"
        };

        return Chainloader.PluginInfos.Values
            .Where(info => invalidPluginGuids.Contains(info.Metadata.GUID))
            .Select(info => $"{info.Metadata.Name}, GUID: {info.Metadata.GUID}")
            .ToList();
    }

    private static void ApplyHeadlessFikaSettings()
    {
        try
        {
            FikaPlugin.Instance.Settings.AutoExtract.Value = true;
            FikaPlugin.Instance.Settings.QuestTypesToShareAndReceive.Value = 0;
            FikaPlugin.Instance.Settings.ConnectionTimeout.Value = 30;
            FikaPlugin.Instance.Settings.UseNamePlates.Value = false;

            // FikaPlugin.Instance.Settings.AllowFreeCam = true;
            // FikaPlugin.Instance.Settings.AllowSpectateFreeCam = true;
        }
        catch (Exception ex)
        {
            FikaHeadlessStuckFixPlugin.Log.LogWarning($"Could not apply Fika headless settings: {ex.Message}");
        }
    }

    private static void RunOnMainThread(Action action)
    {
        try
        {
            AsyncWorker.RunInMainTread(action);
        }
        catch (Exception ex)
        {
            FikaHeadlessStuckFixPlugin.Log.LogWarning($"Could not queue main-thread action, running inline: {ex.Message}");
            action();
        }
    }
}

internal static class HeadlessReflection
{
    private static readonly FieldInfo? HasVerifiedField =
        AccessTools.Field(typeof(FikaHeadlessPlugin), "_hasVerified");

    private static readonly FieldInfo? InvalidPluginsFoundField =
        AccessTools.Field(typeof(FikaHeadlessPlugin), "_invalidPluginsFound");

    private static readonly MethodInfo? CanHostSetter =
        AccessTools.PropertySetter(typeof(FikaHeadlessPlugin), nameof(FikaHeadlessPlugin.CanHost));

    private static readonly MethodInfo? CreateHeadlessWebsocketMethod =
        AccessTools.Method(typeof(FikaHeadlessPlugin), "CreateHeadlessWebsocket");

    public static bool GetHasVerified(FikaHeadlessPlugin plugin)
    {
        return HasVerifiedField?.GetValue(plugin) as bool? ?? false;
    }

    public static void SetHasVerified(FikaHeadlessPlugin plugin, bool value)
    {
        HasVerifiedField?.SetValue(plugin, value);
    }

    public static void SetInvalidPluginsFound(FikaHeadlessPlugin plugin, bool value)
    {
        InvalidPluginsFoundField?.SetValue(plugin, value);
    }

    public static void SetCanHost(FikaHeadlessPlugin plugin, bool value)
    {
        CanHostSetter?.Invoke(plugin, new object[] { value });
    }

    public static void InvokeCreateHeadlessWebsocket(FikaHeadlessPlugin plugin)
    {
        CreateHeadlessWebsocketMethod?.Invoke(plugin, null);
    }
}
