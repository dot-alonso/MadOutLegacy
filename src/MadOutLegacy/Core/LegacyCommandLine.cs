using System;
using System.Linq;
using UnityEngine;

internal static class LegacyCommandLine
{
    internal static bool AutoConnectFromArgs;
    internal static string ConnectIP;
    internal static int ConnectPort;
    internal static string BindIP;
    internal static bool BindToSpecificIP;

    internal static void UpdateFromArgs()
    {
        string[] args = Environment.GetCommandLineArgs();
        ConnectIP = GetArgValue(args, "-connectIP:") ?? GetArgValue(args, "-connectIP=") ?? ConnectIP;
        int port;
        string portText = GetArgValue(args, "-connectPort:") ?? GetArgValue(args, "-connectPort=");
        if (!string.IsNullOrEmpty(portText) && int.TryParse(portText, out port) && port > 0)
        {
            ConnectPort = port;
        }

        BindIP = GetArgValue(args, "-bindIP:") ?? GetArgValue(args, "-bindIP=") ?? GetArgValue(args, "-ip:") ?? BindIP;
        BindToSpecificIP = !string.IsNullOrEmpty(BindIP) || HasArg("-bindToIP") || HasArg("-bindSpecificIP");
        AutoConnectFromArgs = !string.IsNullOrEmpty(ConnectIP) && ConnectPort > 0;

        LegacyServerLimits.UpdateFromArgs(args);

        if (AutoConnectFromArgs)
        {
            Debug.Log("MadOutLegacy direct connect from args: " + ConnectIP + ":" + ConnectPort.ToString());
        }
        if (BindToSpecificIP)
        {
            Debug.Log("MadOutLegacy server bind IP from args: " + BindIP);
        }
    }

    internal static bool HasArg(string arg)
    {
        return Environment.GetCommandLineArgs().Any(t => string.Equals(t, arg, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool HasServerArg()
    {
        return Environment.GetCommandLineArgs().Any(t => t.IndexOf("bend_GameServer", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    internal static bool HasBatchMode()
    {
        return Application.isBatchMode || HasArg("-batchmode");
    }

    internal static bool HasNoGraphics()
    {
        return HasArg("-nographics");
    }

    internal static bool HasHideWindow()
    {
        return HasArg("-hideWindow") || HasArg("-hideBatchWindow");
    }

    private static string GetArgValue(string[] args, string prefix)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return arg.Substring(prefix.Length).Trim();
            }
        }
        return null;
    }
}
