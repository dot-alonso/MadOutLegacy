using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Smooth;
using UnityEngine;

[HarmonyPatch]
internal static class NetConnectAndControlPatches
{
    private static readonly HashSet<NetConnect> WaitingForWorld = new HashSet<NetConnect>();

    [HarmonyPatch(typeof(NetConnect), "Cmd_SetPrivateID")]
    [HarmonyPrefix]
    private static bool NetConnect_CmdSetPrivateID_Prefix(NetConnect __instance, string setPrivateID, string setNikName, string setPlayerXml, RuntimePlatform platform)
    {
        __instance.privateGUID = setPrivateID;
        __instance.nikName = NetManagerTools.RemoveRitchText(setNikName);
        if (NetManager.isLogType(NetManager.mLogType.Info))
        {
            Debug.Log("Player: " + __instance.nikName + " guid: " + __instance.privateGUID + " PlayerXml: " + setPlayerXml);
        }
        if (NetManagerTools.isCommandLineArgHaveServerStr())
        {
            if (!WaitingForWorld.Contains(__instance))
            {
                WaitingForWorld.Add(__instance);
                __instance.StartCoroutine(InitInputControlWhenWorldReady(__instance, setPlayerXml, platform));
            }
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(NetConnect), "InitInputControl")]
    [HarmonyPrefix]
    private static bool NetConnect_InitInputControl_Prefix(NetConnect __instance)
    {
        string reason;
        if (NetManagerTools.isCommandLineArgHaveServerStr() && !IsServerPlayerCreateReady(out reason))
        {
            Debug.LogWarning("InitInputControl cancelled: server is not ready, reason: " + reason);
            if (__instance.conn != null)
            {
                __instance.conn.Disconnect();
            }
            return false;
        }
        LegacyHelpers.EnsureServerNetChat(NetManager.me);
        return true;
    }

    private static IEnumerator InitInputControlWhenWorldReady(NetConnect connect, string setPlayerXml, RuntimePlatform platform)
    {
        float started = nTime.Ms;
        float nextLogAt = started;
        float timeoutAt = started + 60000f;
        string reason;
        while (!IsServerPlayerCreateReady(out reason) && nTime.Ms < timeoutAt)
        {
            if (nTime.Ms >= nextLogAt)
            {
                nextLogAt = nTime.Ms + 2000f;
                Debug.Log("Waiting server world for player '" + connect.nikName + "': " + reason);
            }
            yield return null;
        }
        WaitingForWorld.Remove(connect);
        if (!IsServerPlayerCreateReady(out reason))
        {
            Debug.LogError("Server world was not ready for player '" + connect.nikName + "' in " + (nTime.Ms - started).ToString() + " ms. Reason: " + reason);
            if (connect.conn != null)
            {
                connect.conn.Disconnect();
            }
            yield break;
        }
        Debug.Log("Server world ready for player '" + connect.nikName + "' in " + (nTime.Ms - started).ToString() + " ms");
        connect.InitInputControl(setPlayerXml, platform);
    }

    private static bool IsServerPlayerCreateReady(out string reason)
    {
        reason = string.Empty;
        bool spawnsReady = RaceAndWorldPatches.TryEnsureNetSpawns();
        bool groundReady = GroundLoader.me == null || GroundLoader.isAllLoaded;
        bool mapReady = MapProLoader.me == null || MapProLoader.canControlsLoad();
        if (!spawnsReady || !groundReady || !mapReady)
        {
            reason = "SpawnPos/Map/Ground is not ready";
            return false;
        }
        if (!NetManager.me)
        {
            reason = "NetManager.me is null";
            return false;
        }
        if (!NetManager.me.netControl)
        {
            reason = "NetManager.me.netControl prefab is null";
            return false;
        }
        if (!NetManager.me.player_prefab)
        {
            reason = "NetManager.me.player_prefab is null";
            return false;
        }
        if (!Created.me)
        {
            reason = "Created.me is null";
            return false;
        }
        if (!Created.me.inputControls)
        {
            reason = "Created.me.inputControls is null";
            return false;
        }
        if (!Created.me.Humans)
        {
            reason = "Created.me.Humans is null";
            return false;
        }
        ControlArray.InitControlItems();
        ControlArray humans = ControlArray.byType(ControlArray.Type.Human);
        if (!humans)
        {
            reason = "ControlArray.Human is null";
            return false;
        }
        if (humans.controls == null || humans.controls.Count == 0)
        {
            reason = "ControlArray.Human has no controls";
            return false;
        }
        ControlArray.ControlItem male = humans.GetByName("Male");
        if (male == null)
        {
            if (humans.GetReadyControl() == null)
            {
                reason = "No ready human control";
                return false;
            }
            return true;
        }
        if (!male.control)
        {
            reason = "Human control 'Male' is not loaded";
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(NetInputControl), "Cmd_AddMessage")]
    [HarmonyPrefix]
    private static bool NetInputControl_CmdAddMessage_Prefix(NetInputControl __instance, string s)
    {
        LegacyHelpers.EnsureServerNetChat(NetManager.me);
        if (NetChat.me)
        {
            NetChat.me.AddMessage(__instance, s, true, 0f);
        }
        else
        {
            Debug.LogWarning("Chat message ignored because NetChat.me is null");
        }
        return false;
    }

    [HarmonyPatch(typeof(NetControl), "ActualCreateControl")]
    [HarmonyPrefix]
    private static bool NetControl_ActualCreateControl_Prefix(NetControl __instance)
    {
        __instance.move2cacheCount = 0;
        if (__instance.objItem)
        {
            if (NetManager.isLogType(NetManager.mLogType.Info))
            {
                Debug.Log("Create By name: " + __instance.objName + " but alredy created");
            }
            return false;
        }
        if (NetManager.isLogType(NetManager.mLogType.Info))
        {
            NetControl parent = __instance.parentNetControl ? __instance.parentNetControl.GetComponent<NetControl>() : null;
            Debug.Log("NetControl.CreateByName, objName: " + __instance.objName + (parent ? ("   parent: " + parent.objName) : null), __instance);
        }
        ControlArray humans = ControlArray.byType(ControlArray.Type.Human);
        if (humans)
        {
            __instance.objItem = humans.GetOrCreateByName(__instance.objName, __instance);
            if (!__instance.objItem && __instance.objName == "Male")
            {
                ControlArray.ControlItem ready = humans.GetReadyControl();
                if (ready != null)
                {
                    Debug.LogWarning("NetControl.CreateByName fallback human: Male -> " + ready.name);
                    __instance.NetworkobjName = ready.name;
                    __instance.objItem = humans.GetOrCreateByName(ready.name, __instance);
                }
            }
        }
        else
        {
            Debug.LogError("NetControl.CreateByName failed: Human ControlArray is null, objName: " + __instance.objName, __instance);
        }
        if (__instance.objItem)
        {
            if (__instance.syncPlayer)
            {
                __instance.syncPlayer.enabled = true;
            }
        }
        else
        {
            ControlArray cars = ControlArray.byType(ControlArray.Type.Car);
            if (cars)
            {
                __instance.objItem = cars.GetOrCreateByName(__instance.objName, __instance);
            }
            else
            {
                Debug.LogError("NetControl.CreateByName failed: Car ControlArray is null, objName: " + __instance.objName, __instance);
            }
        }
        if (!__instance.objItem || !__instance.objItem.control)
        {
            Debug.LogError("NetControl.CreateByName failed: object not found or not loaded, objName: " + __instance.objName, __instance);
            __instance.objItem = null;
            __instance.control = null;
            return false;
        }
        __instance.control = __instance.objItem.control;
        __instance.objItem.holdNotBackToCach = true;
        __instance.lastCheckDist = nTime.GameThisFrame;
        if (__instance.smoothSync)
        {
            __instance.smoothSync.Init(__instance.control);
            if (NetManager.isServer && __instance.objItem.control is CarControl)
            {
                __instance.radioSelect = Radio.GetRandomRadio();
            }
            __instance.InitAllDataToControl_IfTheyCreated();
            __instance.UpObjNameInEditor();
        }
        else
        {
            Debug.LogError("NetControl.CreateByName failed: smoothSync is null, objName: " + __instance.objName, __instance);
        }
        return false;
    }
}
