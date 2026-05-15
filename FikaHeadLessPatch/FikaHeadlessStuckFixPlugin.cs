using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace FikaHeadLessPatch
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.fika.headless", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("Tyfon.UIFixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class FikaHeadlessStuckFixPlugin : BaseUnityPlugin
    {
        private const string PluginGuid = "com.fika.headless.stuckfix";
        private const string PluginName = "Fika.Headless.StuckFix";
        private const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log { get; private set; } = null!;

        internal static ConfigEntry<int> LocaleLoadTimeoutSeconds { get; private set; } = null!;
        internal static ConfigEntry<int> ValidationStartupDelaySeconds { get; private set; } = null!;
        internal static ConfigEntry<bool> QuitOnInvalidPlugins { get; private set; } = null!;
        internal static ConfigEntry<int> RaidStartTimeoutSeconds { get; private set; } = null!;
        internal static ConfigEntry<bool> QuitOnRaidStartTimeout { get; private set; } = null!;
        internal static ConfigEntry<bool> RecoverCanHostAfterStartAbort { get; private set; } = null!;
        internal static ConfigEntry<bool> AsyncWebSocketConnect { get; private set; } = null!;
        internal static ConfigEntry<bool> ForceClearWeather { get; private set; } = null!;
        internal static ConfigEntry<bool> DisableCustomWeatherWhenForcingClear { get; private set; } = null!;
        internal static ConfigEntry<bool> DisableTyfonUiFixesFikaSync { get; private set; } = null!;

        private Harmony? _harmony;

        private void Awake()
        {
            Log = Logger;
            BindConfig();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
            Logger.LogInfo($"无头修复补丁初始化完成");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        
        private void BindConfig()
        {
            LocaleLoadTimeoutSeconds = Config.Bind(
                "Validation",
                "Locale Load Timeout Seconds",
                60,
                new ConfigDescription("Maximum time to wait for Fika.Core locales before continuing validation. Set 0 to wait forever.",
                    new AcceptableValueRange<int>(0, 600)));

            ValidationStartupDelaySeconds = Config.Bind(
                "Validation",
                "Validation Startup Delay Seconds",
                15,
                new ConfigDescription("Delay kept from Fika.Headless before creating the headless websocket.",
                    new AcceptableValueRange<int>(0, 60)));

            QuitOnInvalidPlugins = Config.Bind(
                "Validation",
                "Quit On Invalid Plugins",
                false,
                "Quit instead of sleeping forever when Fika.Headless detects unsupported plugins.");

            RaidStartTimeoutSeconds = Config.Bind(
                "Raid Start",
                "Raid Start Timeout Seconds",
                240,
                new ConfigDescription("Maximum time allowed for the Fika.Headless raid start task. Set 0 to disable this watchdog.",
                    new AcceptableValueRange<int>(0, 1200)));

            QuitOnRaidStartTimeout = Config.Bind(
                "Raid Start",
                "Quit On Raid Start Timeout",
                true,
                "Quit the headless client if raid startup does not complete before the watchdog timeout.");

            RecoverCanHostAfterStartAbort = Config.Bind(
                "Raid Start",
                "Recover CanHost After Start Abort",
                true,
                "Restore CanHost when raid startup returns early without entering a raid.");

            AsyncWebSocketConnect = Config.Bind(
                "WebSocket",
                "Async WebSocket Connect",
                true,
                "Use websocket-sharp ConnectAsync to avoid blocking Unity's main thread during headless websocket connection attempts.");

            ForceClearWeather = Config.Bind(
                "Weather",
                "Force Clear Weather",
                true,
                "Force headless raids to use clear weather instead of random/requested weather.");

            DisableCustomWeatherWhenForcingClear = Config.Bind(
                "Weather",
                "Disable Custom Weather When Forcing Clear",
                true,
                "Disable Fika custom weather curves when Force Clear Weather is enabled.");

            DisableTyfonUiFixesFikaSync = Config.Bind(
                "Compatibility",
                "Disable Tyfon UIFixes Fika Sync",
                true,
                "Skip Tyfon.UIFixes Fika peer sync packets on headless hosts when its Fika serializer is incompatible.");
        }
    }
}



