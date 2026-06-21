using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

internal static class LegacyHelpers
{
    internal static string GameRoot
    {
        get
        {
            string root = Directory.GetCurrentDirectory();
            try
            {
                if (!string.IsNullOrEmpty(Application.dataPath))
                {
                    DirectoryInfo parent = Directory.GetParent(Application.dataPath);
                    if (parent != null)
                    {
                        root = parent.FullName;
                    }
                }
            }
            catch { }
            return root;
        }
    }

    internal static string VersionLine
    {
        get { return "MadOutLegacy v" + MadOutLegacyPlugin.ModVersionName + " by .alonso | Game ver " + Application.version; }
    }

    internal static void SafeInvoke(object instance, string methodName)
    {
        if (instance is Type)
        {
            SafeInvoke((Type)instance, null, methodName);
            return;
        }
        SafeInvoke(instance.GetType(), instance, methodName);
    }

    internal static void SafeInvoke(Type type, object instance, string methodName)
    {
        try
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null)
            {
                method.Invoke(instance, null);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("MadOutLegacy SafeInvoke failed: " + methodName + " -> " + ex.Message);
        }
    }

    internal static void EnsureServerNetChat(NetManager manager)
    {
        if (!manager || !NetManager.isServer || !NetworkServer.active)
        {
            return;
        }
        if (NetChat.me)
        {
            return;
        }
        if (!manager.chat)
        {
            Debug.LogError("Cannot spawn server NetChat: NetManager.chat is null");
            return;
        }
        GameObject go = manager.chat.InstanciateSafe();
        if (!go)
        {
            Debug.LogError("Cannot spawn server NetChat: InstanciateSafe returned null");
            return;
        }
        go.name = "NetChat_Server";
        go.SetActive(true);
        NetChat chat = go.GetComponent<NetChat>();
        if (!chat || !go.GetComponent<NetworkIdentity>())
        {
            Debug.LogError("Cannot spawn server NetChat: invalid prefab instance", go);
            UnityEngine.Object.Destroy(go);
            return;
        }
        NetChat.me = chat;
        try
        {
            NetworkServer.Spawn(go);
        }
        catch (Exception ex)
        {
            Debug.LogError("Cannot spawn server NetChat: " + ex.Message, go);
            if (NetChat.me == chat)
            {
                NetChat.me = null;
            }
            UnityEngine.Object.Destroy(go);
        }
    }

    internal static void EnsureServerUnetSettings(NetManager manager)
    {
        if (!manager || !NetManager.isServer || !NetworkServer.active)
        {
            return;
        }
        if (!manager.unet_settings)
        {
            return;
        }
        UNet_Settings existing = UnityEngine.Object.FindObjectOfType<UNet_Settings>();
        if (existing)
        {
            return;
        }
        GameObject go = UnityEngine.Object.Instantiate(manager.unet_settings.gameObject);
        go.name = "UNet_Settings_Server";
        NetworkServer.Spawn(go);
    }
}
