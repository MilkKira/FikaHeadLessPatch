using System;
using System.Reflection;
using EFT.Weather;
using Fika.Core.Networking.Models.Headless;
using Fika.Headless;
using HarmonyLib;

namespace FikaHeadLessPatch.Patches;

[HarmonyPatch(typeof(FikaHeadlessPlugin), nameof(FikaHeadlessPlugin.OnFikaStartRaid))]
internal static class ForceClearWeatherPatch
{
    private static void Prefix(ref StartHeadlessRequest request)
    {
        if (!FikaHeadlessStuckFixPlugin.ForceClearWeather.Value)
        {
            return;
        }

        var weather = request.TimeAndWeatherSettings;
        weather.IsRandomWeather = false;
        weather.CloudinessType = ECloudinessType.Clear;
        weather.RainType = ERainType.NoRain;
        weather.FogType = EFogType.NoFog;
        weather.WindType = EWindSpeed.Light;

        request.TimeAndWeatherSettings = weather;

        if (FikaHeadlessStuckFixPlugin.DisableCustomWeatherWhenForcingClear.Value)
        {
            TryDisableCustomWeather(ref request);
        }

        FikaHeadlessStuckFixPlugin.Log.LogInfo("Force Clear Weather enabled: using Clear / NoRain / NoFog / Light wind.");
    }

    private static void TryDisableCustomWeather(ref StartHeadlessRequest request)
    {
        var requestObject = (object)request;
        SetBooleanMember(requestObject, "CustomWeather", false);

        var customRaidSettings = GetMemberValue(requestObject, "CustomRaidSettings");
        if (customRaidSettings == null)
        {
            return;
        }

        try
        {
            var type = customRaidSettings.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // var property = AccessTools.Property(type, "UseCustomWeather");
            var property = type.GetProperty("UseCustomWeather", flags);
            if (property?.CanWrite == true)
            {
                property.SetValue(customRaidSettings, false);
                return;
            }

            var field = type.GetField("UseCustomWeather", flags)
                ?? type.GetField("useCustomWeather", flags)
                ?? type.GetField("CustomWeather", flags)
                ?? type.GetField("customWeather", flags);

            if (field != null && field.FieldType == typeof(bool))
            {
                field.SetValue(customRaidSettings, false);
            }
        }
        catch (Exception ex)
        {
            FikaHeadlessStuckFixPlugin.Log.LogWarning($"Could not disable custom weather flag: {ex.Message}");
        }
    }

    private static void SetBooleanMember(object instance, string name, bool value)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = instance.GetType();

        // var property = AccessTools.Property(type, name);
        var property = type.GetProperty(name, flags);
        if (property?.CanWrite == true)
        {
            property.SetValue(instance, value);
            return;
        }

        var field = type.GetField(name, flags)
            ?? type.GetField($"<{name}>k__BackingField", flags)
            ?? type.GetField(char.ToLowerInvariant(name[0]) + name.Substring(1), flags);

        if (field != null && field.FieldType == typeof(bool))
        {
            field.SetValue(instance, value);
        }
    }

    private static object? GetMemberValue(object instance, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = instance.GetType();
        
        var property = type.GetProperty(name, flags);
        // var property = AccessTools.Property(type, name);
        if (property != null)
        {
            return property.GetValue(instance);
        }

        return type.GetField(name, flags)?.GetValue(instance);
    }
}
