using System;
using Fika.Headless.Classes;
using HarmonyLib;
using WebSocketSharp;

namespace FikaHeadLessPatch.Patches;

[HarmonyPatch(typeof(HeadlessWebSocket), nameof(HeadlessWebSocket.Connect))]
internal static class HeadlessWebSocketConnectPatch
{
    private static readonly System.Reflection.FieldInfo? WebSocketField =
        AccessTools.Field(typeof(HeadlessWebSocket), "_webSocket");

    private static readonly System.Reflection.FieldInfo? AttemptsField =
        AccessTools.Field(typeof(HeadlessWebSocket), "_attempts");

    private static bool Prefix(HeadlessWebSocket __instance)
    {
        if (!FikaHeadlessStuckFixPlugin.AsyncWebSocketConnect.Value)
        {
            return true;
        }

        try
        {
            if (WebSocketField?.GetValue(__instance) is not WebSocket webSocket)
            {
                return true;
            }

            FikaHeadlessStuckFixPlugin.Log.LogInfo($"Attempting async websocket connection to {__instance.Url}...");
            webSocket.ConnectAsync();
            IncrementAttempts(__instance);
            return false;
        }
        catch (Exception ex)
        {
            FikaHeadlessStuckFixPlugin.Log.LogWarning($"Async websocket connect failed, falling back to original Connect(): {ex.Message}");
            return true;
        }
    }

    private static void IncrementAttempts(HeadlessWebSocket instance)
    {
        if (AttemptsField == null)
        {
            return;
        }

        var attempts = AttemptsField.GetValue(instance) as int? ?? 1;
        AttemptsField.SetValue(instance, attempts + 1);
    }
}
