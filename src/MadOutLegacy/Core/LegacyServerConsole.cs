using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

public sealed class LegacyServerConsole : MonoBehaviour
{
    private const int MaxLogLinesCount = 1000000;

    private static LegacyServerConsole instance;
    private static bool windowsInitTried;
    private static bool windowsReady;
    private static bool linuxInitTried;
    private static bool linuxReady;
    private static volatile bool quitRequested;
    private static ConsoleCtrlHandler consoleCtrlHandler;
    private static UnixSignalHandler unixSignalHandler;
    private static int linuxStdoutFd = -1;
    private static int linuxStderrFd = -1;
    private static bool readyStatusPrinted;
    private static IntPtr cachedBatchWindow = IntPtr.Zero;

    private int logLinesCount;
    private bool batchWindowHideLogged;
    private bool batchWindowHideStopped;
    private int batchWindowHideAttempts;
    private float nextBatchWindowHideTryTime;

    public static void PrepareForServerMode()
    {
        if (!LegacyCommandLine.HasServerArg())
        {
            return;
        }
        if (LegacyCommandLine.HasArg("-keepBepInExConsoleLog") || LegacyCommandLine.HasArg("-verboseServerConsole"))
        {
            return;
        }
        TryDisableBepInExConsoleListener();
    }

    private static void TryDisableBepInExConsoleListener()
    {
        try
        {
            Type loggerType = typeof(BepInEx.Logging.Logger);
            PropertyInfo listenersProperty = loggerType.GetProperty("Listeners", BindingFlags.Public | BindingFlags.Static);
            if (listenersProperty == null)
            {
                return;
            }
            object listeners = listenersProperty.GetValue(null, null);
            if (listeners == null)
            {
                return;
            }
            IEnumerable enumerable = listeners as IEnumerable;
            if (enumerable == null)
            {
                return;
            }
            List<object> toRemove = new List<object>();
            foreach (object listener in enumerable)
            {
                if (listener == null)
                {
                    continue;
                }
                Type listenerType = listener.GetType();
                string typeName = listenerType.FullName ?? listenerType.Name;
                if (typeName.IndexOf("ConsoleLogListener", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    toRemove.Add(listener);
                }
            }
            if (toRemove.Count == 0)
            {
                return;
            }
            MethodInfo removeMethod = listeners.GetType().GetMethod("Remove");
            for (int i = 0; i < toRemove.Count; i++)
            {
                removeMethod.Invoke(listeners, new object[] { toRemove[i] });
            }
        }
        catch
        {
        }
    }

    public static void CreateIfNeeded()
    {
        if (instance)
        {
            return;
        }
        if (!LegacyCommandLine.HasServerArg())
        {
            return;
        }
        GameObject host = new GameObject("MadOutLegacy_ServerConsole");
        UnityEngine.Object.DontDestroyOnLoad(host);
        instance = host.AddComponent<LegacyServerConsole>();
    }

    private void OnEnable()
    {
        Application.logMessageReceived -= HandleLog;
        Application.logMessageReceived += HandleLog;
        TryInitWindowsServerConsole();
        TryInitLinuxServerConsole();
        TryHideBatchModeWindow();
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
        FlushWindowsServerConsole();
        FlushLinuxServerConsole();
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        TryHideBatchModeWindow();
        if (quitRequested)
        {
            quitRequested = false;
            Debug.Log("FORCE_EXIT: Ctrl+C / console close requested");
            Application.Quit();
            return;
        }
        if (logLinesCount > MaxLogLinesCount)
        {
            Debug.Log("FORCE_EXIT: Log So Big, log lines count: " + logLinesCount.ToString());
            Application.Quit();
        }
    }

    private void HandleLog(string message, string stackTrace, LogType type)
    {
        logLinesCount++;
        TryPrintServerReadyStatus(message);
        if (ShouldSuppressServerConsoleLog(message, stackTrace, type))
        {
            return;
        }
        WriteWindowsServerConsoleLog(message, stackTrace, type);
        WriteLinuxServerConsoleLog(message, stackTrace, type);
    }

    private static bool IsDedicatedServerConsoleWanted()
    {
        return LegacyCommandLine.HasServerArg() && !LegacyCommandLine.HasArg("-noServerConsole");
    }

    private void TryInitWindowsServerConsole()
    {
        if (windowsInitTried)
        {
            return;
        }
        windowsInitTried = true;
        if (Application.platform != RuntimePlatform.WindowsPlayer || !IsDedicatedServerConsoleWanted())
        {
            return;
        }
        try
        {
            bool forceAlloc = LegacyCommandLine.HasArg("-serverConsole") || LegacyCommandLine.HasArg("-console");
            bool attached = AttachConsole(uint.MaxValue);
            if (!attached && forceAlloc)
            {
                attached = AllocConsole();
            }
            if (!attached)
            {
                return;
            }
            windowsReady = true;
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
            if (consoleCtrlHandler == null)
            {
                consoleCtrlHandler = OnWindowsConsoleCtrl;
                SetConsoleCtrlHandler(consoleCtrlHandler, true);
            }
            try
            {
                Console.Title = "MadOut2 Dedicated Server";
            }
            catch
            {
            }
            Console.WriteLine("------------------------------------");
            Console.WriteLine("MadOut2 " + LegacyMasterServer.ReturnServerModeName() + " Server | Game ver " + Application.version + " | MadOutLegacy v" + MadOutLegacyPlugin.ModVersionName);
            Console.WriteLine("Press Ctrl+C to stop server");
            Console.WriteLine("------------------------------------");
            Console.WriteLine("[Status] Loading world...\n");
        }
        catch (Exception ex)
        {
            windowsReady = false;
            Debug.LogWarning("Failed to init Windows server console: " + ex.Message);
        }
    }

    private void WriteWindowsServerConsoleLog(string message, string stackTrace, LogType type)
    {
        if (!windowsReady)
        {
            return;
        }
        try
        {
            bool isError = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            string text = "[" + type.ToString() + "] " + message;
            if (isError)
            {
                Console.Error.WriteLine(text);
                if (!string.IsNullOrEmpty(stackTrace))
                {
                    Console.Error.WriteLine(stackTrace);
                }
            }
            else
            {
                Console.Out.WriteLine(text);
            }
        }
        catch
        {
            windowsReady = false;
        }
    }

    private static void FlushWindowsServerConsole()
    {
        if (!windowsReady)
        {
            return;
        }
        try
        {
            Console.Out.Flush();
            Console.Error.Flush();
        }
        catch
        {
        }
    }

    private static bool OnWindowsConsoleCtrl(uint ctrlType)
    {
        if (ctrlType == 0U || ctrlType == 1U || ctrlType == 2U || ctrlType == 5U || ctrlType == 6U)
        {
            quitRequested = true;
            return true;
        }
        return false;
    }

    private void TryInitLinuxServerConsole()
    {
        if (linuxInitTried)
        {
            return;
        }
        linuxInitTried = true;
        if (Application.platform != RuntimePlatform.LinuxPlayer || !IsDedicatedServerConsoleWanted())
        {
            return;
        }
        try
        {
            linuxStdoutFd = dup(1);
            linuxStderrFd = dup(2);
            if (linuxStdoutFd < 0 && linuxStderrFd < 0)
            {
                return;
            }
            if (linuxStderrFd < 0)
            {
                linuxStderrFd = linuxStdoutFd;
            }
            int nullFd = open("/dev/null", 1);
            if (nullFd >= 0)
            {
                dup2(nullFd, 1);
                dup2(nullFd, 2);
                close(nullFd);
            }
            linuxReady = true;
            if (unixSignalHandler == null)
            {
                unixSignalHandler = OnUnixSignal;
                signal(2, unixSignalHandler);
                signal(15, unixSignalHandler);
                signal(1, unixSignalHandler);
                signal(3, unixSignalHandler);
            }
            WriteLinuxConsoleRaw(false, "\u001b[3J\u001b[2J\u001b[H");
            WriteLinuxConsoleRaw(false, "\n");
            WriteLinuxConsoleRaw(false, "------------------------------------\n");
            WriteLinuxConsoleRaw(false, "MadOut2 " + LegacyMasterServer.ReturnServerModeName() + " Server | Game ver " + Application.version + " | MadOutLegacy v" + MadOutLegacyPlugin.ModVersionName + "\n");
            WriteLinuxConsoleRaw(false, "Press Ctrl+C to stop server\n");
            WriteLinuxConsoleRaw(false, "------------------------------------\n");
            WriteLinuxConsoleRaw(false, "[Status] Loading world...\n");
        }
        catch (Exception ex)
        {
            linuxReady = false;
            Debug.LogWarning("Failed to init Linux server console: " + ex.Message);
        }
    }

    private void WriteLinuxServerConsoleLog(string message, string stackTrace, LogType type)
    {
        if (!linuxReady)
        {
            return;
        }
        try
        {
            bool isError = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            WriteLinuxConsoleRaw(isError, "[" + type.ToString() + "] " + message + "\n");
            if (isError && !string.IsNullOrEmpty(stackTrace))
            {
                WriteLinuxConsoleRaw(true, stackTrace + "\n");
            }
        }
        catch
        {
            linuxReady = false;
        }
    }

    private static void FlushLinuxServerConsole()
    {
    }

    private static void WriteLinuxConsoleRaw(bool error, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        int fd = error ? linuxStderrFd : linuxStdoutFd;
        if (fd < 0)
        {
            return;
        }
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        write(fd, bytes, new IntPtr(bytes.Length));
    }

    private static void OnUnixSignal(int signal)
    {
        quitRequested = true;
    }

    private static bool ShouldSuppressServerConsoleLog(string message, string stackTrace, LogType type)
    {
        if (!LegacyCommandLine.HasServerArg())
        {
            return false;
        }
        if (!LegacyCommandLine.HasBatchMode() && !LegacyCommandLine.HasNoGraphics())
        {
            return false;
        }
        if (LegacyCommandLine.HasArg("-verboseServerConsole"))
        {
            return false;
        }
        if (message == null)
        {
            message = string.Empty;
        }
        if (stackTrace == null)
        {
            stackTrace = string.Empty;
        }
        return message.IndexOf("NullReferenceException") != -1
            || (message.IndexOf("Load Time") != -1 && message.IndexOf("Map.bytes") != -1)
            || message.IndexOf("Textures was remed") != -1
            || message.IndexOf("Can't remove Light because DistanceViewLight") != -1
            || message.IndexOf("GarageDoor, interCount") != -1
            || stackTrace.IndexOf("GarageDoor.OnEnable") != -1
            || (message.IndexOf("Magic number is wrong") != -1 && stackTrace.IndexOf("System.TermInfo") != -1)
            || stackTrace.IndexOf("System.ConsoleDriver") != -1
            || stackTrace.IndexOf("System.TermInfoReader") != -1
            || message.IndexOf("UnloadAsset can only be used on assets") != -1
            || message.IndexOf("Windows Unity.BatchModeWindow hidden") != -1
            || message.IndexOf("Parce:-masterServer") != -1
            || message.IndexOf("FileBlocks_Loader. Fail to stop thread") != -1;
    }

    private static void TryPrintServerReadyStatus(string message)
    {
        if (readyStatusPrinted || !LegacyCommandLine.HasServerArg() || (!LegacyCommandLine.HasBatchMode() && !LegacyCommandLine.HasNoGraphics()) || string.IsNullOrEmpty(message))
        {
            return;
        }
        if (message.IndexOf("Textures was remed") == -1)
        {
            return;
        }
        readyStatusPrinted = true;
        string text = "[Status] Server ready. World loaded successfully.\n";
        if (windowsReady)
        {
            try
            {
                Console.Out.WriteLine(text);
            }
            catch
            {
                windowsReady = false;
            }
        }
        if (linuxReady)
        {
            WriteLinuxConsoleRaw(false, text);
        }
    }

    private void TryHideBatchModeWindow()
    {
        if (batchWindowHideStopped)
        {
            return;
        }
        if (Application.platform != RuntimePlatform.WindowsPlayer)
        {
            batchWindowHideStopped = true;
            return;
        }
        if (!LegacyCommandLine.HasServerArg() || (!LegacyCommandLine.HasNoGraphics() && !LegacyCommandLine.HasHideWindow()))
        {
            return;
        }
        if (nextBatchWindowHideTryTime > Time.realtimeSinceStartup)
        {
            return;
        }
        nextBatchWindowHideTryTime = Time.realtimeSinceStartup + 0.25f;
        batchWindowHideAttempts++;
        if (HideUnityBatchModeWindowForThisProcess())
        {
            if (!batchWindowHideLogged)
            {
                batchWindowHideLogged = true;
                Debug.Log("Windows Unity.BatchModeWindow hidden");
            }
            batchWindowHideAttempts = 0;
            return;
        }
        if (!batchWindowHideLogged && batchWindowHideAttempts > 80)
        {
            batchWindowHideStopped = true;
            Debug.LogWarning("Windows Unity.BatchModeWindow was not found");
        }
    }

    private static bool HideUnityBatchModeWindowForThisProcess()
    {
        if (Application.platform != RuntimePlatform.WindowsPlayer)
        {
            return false;
        }
        if (cachedBatchWindow != IntPtr.Zero)
        {
            if (IsWindow(cachedBatchWindow))
            {
                HideWindow(cachedBatchWindow);
                return true;
            }
            cachedBatchWindow = IntPtr.Zero;
        }
        int currentProcessId = Process.GetCurrentProcess().Id;
        IntPtr foundWindow = IntPtr.Zero;
        EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
        {
            uint processId;
            GetWindowThreadProcessId(hWnd, out processId);
            if (processId != (uint)currentProcessId)
            {
                return true;
            }
            StringBuilder className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            string text = className.ToString();
            if (text == "Unity.BatchModeWindow" || text.IndexOf("Unity.BatchModeWindow") != -1)
            {
                foundWindow = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        if (foundWindow == IntPtr.Zero)
        {
            return false;
        }
        cachedBatchWindow = foundWindow;
        HideWindow(foundWindow);
        return true;
    }

    private static void HideWindow(IntPtr hWnd)
    {
        ShowWindow(hWnd, 0);
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, 151U);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandler handlerRoutine, bool add);

    [DllImport("libc")]
    private static extern int dup(int oldfd);

    [DllImport("libc")]
    private static extern int dup2(int oldfd, int newfd);

    [DllImport("libc", CharSet = CharSet.Ansi)]
    private static extern int open(string pathname, int flags);

    [DllImport("libc")]
    private static extern int close(int fd);

    [DllImport("libc")]
    private static extern IntPtr write(int fd, byte[] buffer, IntPtr count);

    [DllImport("libc")]
    private static extern IntPtr signal(int signum, UnixSignalHandler handler);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private delegate bool ConsoleCtrlHandler(uint ctrlType);
    private delegate void UnixSignalHandler(int signal);
}
