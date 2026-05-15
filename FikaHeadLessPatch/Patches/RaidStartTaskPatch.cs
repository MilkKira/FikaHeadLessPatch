using System;
using System.Reflection;
using System.Threading.Tasks;
using Comfort.Common;
using Diz.Jobs;
using Diz.Utils;
using Fika.Core.Main.Utils;
using Fika.Headless;
using HarmonyLib;
using UnityEngine;

namespace FikaHeadLessPatch.Patches;

[HarmonyPatch]
internal static class RaidStartTaskPatch
{
    private static MethodBase? TargetMethod()
    {
        return AccessTools.Method(typeof(FikaHeadlessPlugin), "BeginFikaStartRaid");
    }

    private static void Postfix(FikaHeadlessPlugin __instance, ref Task __result)
    {
        if (__result == null)
        {
            return;
        }

        var raidCountBeforeStart = __instance.CurrentRaidCount;
        __result = WatchRaidStart(__instance, __result, raidCountBeforeStart);
    }

    private static async Task WatchRaidStart(FikaHeadlessPlugin plugin, Task originalTask, int raidCountBeforeStart)
    {
        var timeoutSeconds = FikaHeadlessStuckFixPlugin.RaidStartTimeoutSeconds.Value;
        if (timeoutSeconds <= 0)
        {
            await originalTask.ConfigureAwait(false);
            return;
        }

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
        var completedTask = await Task.WhenAny(originalTask, timeoutTask).ConfigureAwait(false);

        if (completedTask == originalTask)
        {
            await originalTask.ConfigureAwait(false);
            RecoverIfStartReturnedEarly(plugin, raidCountBeforeStart);
            return;
        }

        _ = originalTask.ContinueWith(LogFaultedOriginalTask, TaskContinuationOptions.OnlyOnFaulted);

        FikaHeadlessStuckFixPlugin.Log.LogError(
            $"Fika.Headless raid start task did not finish within {timeoutSeconds} seconds.");

        RunOnMainThread(() =>
        {
            ReleaseJobSchedulerForceMode();

            if (FikaHeadlessStuckFixPlugin.QuitOnRaidStartTimeout.Value)
            {
                FikaHeadlessStuckFixPlugin.Log.LogError("Quitting headless client to avoid a stuck process.");
                Application.Quit(1);
                return;
            }

            FikaHeadlessStuckFixPlugin.Log.LogWarning("Recovering CanHost after raid start timeout because quitting is disabled.");
            FikaBackendUtils.IsHeadlessGame = false;
            HeadlessReflection.SetCanHost(plugin, true);
        });
    }

    private static void RecoverIfStartReturnedEarly(FikaHeadlessPlugin plugin, int raidCountBeforeStart)
    {
        if (!FikaHeadlessStuckFixPlugin.RecoverCanHostAfterStartAbort.Value)
        {
            return;
        }

        if (plugin.CanHost || plugin.CurrentRaidCount != raidCountBeforeStart)
        {
            return;
        }

        RunOnMainThread(() =>
        {
            ReleaseJobSchedulerForceMode();
            FikaBackendUtils.IsHeadlessGame = false;
            HeadlessReflection.SetCanHost(plugin, true);
            FikaHeadlessStuckFixPlugin.Log.LogWarning("Raid start returned before entering a raid; restored CanHost.");
        });
    }

    private static void ReleaseJobSchedulerForceMode()
    {
        try
        {
            Singleton<JobScheduler>.Instance.SetForceMode(false);
        }
        catch (Exception ex)
        {
            FikaHeadlessStuckFixPlugin.Log.LogDebug($"Could not release JobScheduler force mode: {ex.Message}");
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

    private static void LogFaultedOriginalTask(Task task)
    {
        if (task.Exception != null)
        {
            FikaHeadlessStuckFixPlugin.Log.LogError(task.Exception);
        }
    }
}
