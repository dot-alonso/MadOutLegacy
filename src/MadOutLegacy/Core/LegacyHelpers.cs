using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
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

    public static string GetJsonPath(string arg, string json)
    {
        ArgParcer.ArgValue argValue = ArgParcer.GetArgValue($"{arg}");
        bool flag = argValue != null && argValue.isExists && !string.IsNullOrEmpty(argValue.strValue);
        string text;
        if (flag)
        {
            text = argValue.strValue;
        }
        else
        {
            string text2 = LegacyHelpers.GameRoot;
            try
            {
                bool flag2 = !string.IsNullOrEmpty(Application.dataPath);
                if (flag2)
                {
                    DirectoryInfo parent = Directory.GetParent(Application.dataPath);
                    bool flag3 = parent != null;
                    if (flag3)
                    {
                        text2 = parent.FullName;
                    }
                }
            }
            catch
            {
            }
            text = Path.Combine(Path.Combine(text2, "MadOutLegacy"), $"{json}");
        }
        return text;
    }

    public static bool CheckFileExistence(string path)
    {
        if (File.Exists(path))
        {
            return true;
        }
        else
        {
            Debug.LogError($"Failed to load {path}: File not found");
            return false;
        }
    }

    public static bool HasCommandLineArg(string arg1, string arg2 = null)
    {
        string[] commandLineArgs = Environment.GetCommandLineArgs();
        for (int i = 0; i < commandLineArgs.Length; i++)
        {
            if (commandLineArgs[i] == arg1 || commandLineArgs[i] == arg2)
            {
                return true;
            }
        }
        return false;
    }

    public static bool IsGameplayRunning()
    {
        if (NetManager.isServer)
        {
            return false;
        }

        if (!Nuligine.me || !Nuligine.RealGame)
        {
            return false;
        }

        if (!GameUI.me || !GameUI.me.isUsed())
        {
            return false;
        }

        if (Loading.me)
        {
            return false;
        }

        if (Scenes.nowBusy())
        {
            return false;
        }

        if (FadeUI.me && FadeUI.fadeState != FadeUI.FadeState.Unfaded)
        {
            return false;
        }

        if (MenuEsc.me || MenuEsc.needSelectWhere)
        {
            return false;
        }

        if (GamePhone.me && GamePhone.me.gameObject.activeSelf)
        {
            return false;
        }

        InputControl input = InputControl.GetFirstUser();

        if (!input)
        {
            return false;
        }

        if (!input.current)
        {
            return false;
        }

        if (!input.currentOrParent)
        {
            return false;
        }

        return true;
    }
}
