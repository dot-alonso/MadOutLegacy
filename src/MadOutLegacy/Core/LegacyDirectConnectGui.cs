using UnityEngine;

internal sealed class LegacyDirectConnectGui : OnGuiGlobal.IOnGuiNeed
{
    private readonly NetUI_ClinetChoiseServer owner;
    private Rect rect;
    private string ip;
    private string port;
    private string status;
    private bool isDragging;
    private Vector2 dragOffset;
    private string masterHost;
    private string masterPort;
    private bool autoMaster;
    private int tab;

    public LegacyDirectConnectGui(NetUI_ClinetChoiseServer owner)
    {
        this.owner = owner;
        rect = GetStartRect();
        ip = PlayerPrefs.GetString("DirectConnect_IP", "127.0.0.1");
        port = PlayerPrefs.GetString("DirectConnect_Port", "7800");
        masterHost = LegacyMasterClient.MasterHost;
        masterPort = LegacyMasterClient.MasterPort.ToString();
        autoMaster = LegacyMasterClient.AutoResolve;
        tab = LegacyMasterClient.IsMasterListEnabled() ? 0 : 1;
        status = string.Empty;
        if (!string.IsNullOrEmpty(LegacyCommandLine.ConnectIP))
        {
            ip = LegacyCommandLine.ConnectIP;
        }
        if (LegacyCommandLine.ConnectPort > 0)
        {
            port = LegacyCommandLine.ConnectPort.ToString();
        }
    }

    public bool isNeedOnGUI()
    {
        return owner != null && owner.isActiveAndEnabled && owner.gameObject.activeInHierarchy;
    }

    public void OnGUI_Manual()
    {
        FixRect();
        Matrix4x4 matrix = GUI.matrix;
        Color color = GUI.color;
        Color background = GUI.backgroundColor;
        bool enabled = GUI.enabled;
        int depth = GUI.depth;
        GUI.matrix = Matrix4x4.identity;
        GUI.color = Color.white;
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
        GUI.depth = -10000;
        HandleDrag();
        DrawPanel();
        GUI.depth = depth;
        GUI.enabled = enabled;
        GUI.backgroundColor = background;
        GUI.color = color;
        GUI.matrix = matrix;
    }

    private Rect GetStartRect()
    {
        const float w = 345f;
        const float h = 250f;
        float x;
        float y;
        if (PlayerPrefs.HasKey("DirectConnect_WindowX") && PlayerPrefs.HasKey("DirectConnect_WindowY"))
        {
            x = PlayerPrefs.GetFloat("DirectConnect_WindowX", 230f);
            y = PlayerPrefs.GetFloat("DirectConnect_WindowY", 90f);
        }
        else
        {
            x = (Screen.width - w) * 0.5f;
            y = (Screen.height - h) * 0.5f;
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                x = 230f;
                y = 90f;
            }
        }
        return new Rect(x, y, w, h);
    }

    private void FixRect()
    {
        if (rect.width < 300f || rect.height < 200f)
        {
            rect = new Rect(230f, 90f, 345f, 250f);
        }
        float maxX = Mathf.Max(8f, Screen.width - rect.width - 8f);
        float maxY = Mathf.Max(8f, Screen.height - rect.height - 8f);
        rect.x = Mathf.Clamp(rect.x, 8f, maxX);
        rect.y = Mathf.Clamp(rect.y, 8f, maxY);
    }

    private void HandleDrag()
    {
        Event cur = Event.current;
        if (cur == null)
        {
            return;
        }
        Rect title = new Rect(rect.x, rect.y, rect.width, 30f);
        if (cur.type == EventType.MouseDown && cur.button == 0 && title.Contains(cur.mousePosition))
        {
            isDragging = true;
            dragOffset = cur.mousePosition - new Vector2(rect.x, rect.y);
            cur.Use();
            return;
        }
        if (cur.type == EventType.MouseDrag && isDragging)
        {
            rect.x = cur.mousePosition.x - dragOffset.x;
            rect.y = cur.mousePosition.y - dragOffset.y;
            FixRect();
            cur.Use();
            return;
        }
        if (cur.type == EventType.MouseUp && isDragging)
        {
            isDragging = false;
            PlayerPrefs.SetFloat("DirectConnect_WindowX", rect.x);
            PlayerPrefs.SetFloat("DirectConnect_WindowY", rect.y);
            PlayerPrefs.Save();
            cur.Use();
        }
    }

    private void DrawPanel()
    {
        GUI.Box(rect, string.Empty);
        GUIStyle title = new GUIStyle();
        title.fontSize = 16;
        title.normal.textColor = Color.white;
        GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 20f, 22f), "<b>Server connection</b> (Drag to move)", title);

        float x = rect.x + 15f;
        float y = rect.y + 42f;
        if (GUI.Toggle(new Rect(x, y, 120f, 26f), tab == 0, "Master"))
        {
            tab = 0;
        }
        if (GUI.Toggle(new Rect(x + 130f, y, 120f, 26f), tab == 1, "Direct"))
        {
            tab = 1;
        }
        y += 38f;
        if (tab == 0)
        {
            DrawMasterTab(x, y);
        }
        else
        {
            DrawDirectTab(x, y);
        }
    }

    private void DrawMasterTab(float x, float y)
    {
        GUI.Label(new Rect(x, y, 90f, 24f), "Master:");
        masterHost = GUI.TextField(new Rect(x + 100f, y - 2f, 215f, 26f), masterHost ?? string.Empty);
        y += 36f;
        GUI.Label(new Rect(x, y, 90f, 24f), "Port:");
        masterPort = GUI.TextField(new Rect(x + 100f, y - 2f, 120f, 26f), masterPort ?? string.Empty);
        y += 30f;
        autoMaster = GUI.Toggle(new Rect(x - 2f, y, 260f, 24f), autoMaster, " Auto resolve on start game");
        y += 24f;
        if (!string.IsNullOrEmpty(status))
        {
            GUI.Label(new Rect(x, y, 430f, 30f), status);
        }
        y += 28f;
        if (GUI.Button(new Rect(x, y, 150f, 32f), "<b>Resolve list</b>"))
        {
            ResolveMaster();
        }
        if (GUI.Button(new Rect(x + 164f, y, 150f, 32f), "Local master"))
        {
            masterHost = "127.0.0.1";
            masterPort = "35000";
            status = "Local master selected";
        }
    }

    private void DrawDirectTab(float x, float y)
    {
        GUI.Label(new Rect(x, y, 90f, 24f), "Game IP:");
        ip = GUI.TextField(new Rect(x + 100f, y - 2f, 215f, 26f), ip ?? string.Empty);
        y += 36f;
        GUI.Label(new Rect(x, y, 90f, 24f), "Port:");
        port = GUI.TextField(new Rect(x + 100f, y - 2f, 120f, 26f), port ?? string.Empty);
        y += 30f;
        if (!string.IsNullOrEmpty(status))
        {
            GUI.Label(new Rect(x, y, 430f, 44f), status);
        }
        y += 52f;
        if (GUI.Button(new Rect(x, y, 150f, 32f), "<b>Connect</b>"))
        {
            ConnectDirect();
        }
        if (GUI.Button(new Rect(x + 164f, y, 150f, 32f), "Set localhost"))
        {
            ip = "127.0.0.1";
            port = "7800";
            status = "Localhost selected";
        }
    }

    private void ConnectDirect()
    {
        string ipText = (ip ?? string.Empty).Trim();
        string portText = (port ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(ipText))
        {
            status = "IP is empty";
            return;
        }
        if (ipText.IndexOf(" ") != -1)
        {
            status = "IP/host contains spaces";
            return;
        }
        int p;
        if (!int.TryParse(portText, out p) || p < 1 || p > 65535)
        {
            status = "Bad port";
            return;
        }
        LegacyCommandLine.ConnectIP = ipText;
        LegacyCommandLine.ConnectPort = p;
        PlayerPrefs.SetString("DirectConnect_IP", ipText);
        PlayerPrefs.SetString("DirectConnect_Port", p.ToString());
        PlayerPrefs.Save();
        LegacyMasterClient.SetMaster(masterHost, GetMasterPortValue(), autoMaster, false);
        NetUIPatches.RefreshServerListNow(owner);
        if (NetManager.me == null)
        {
            status = "NetManager is null";
            return;
        }
        status = "Connecting to " + ipText + ":" + p.ToString();
        NetManager.me.ConnectTo(ipText, p);
    }

    private void ResolveMaster()
    {
        int p = GetMasterPortValue();
        if (p <= 0)
        {
            status = "Bad master port";
            return;
        }
        string host = (masterHost ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(host))
        {
            status = "Master host is empty";
            return;
        }
        LegacyMasterClient.SetMaster(host, p, autoMaster, true);
        if (LegacyMasterClient.GetMasterEndPoint() == null)
        {
            status = "Failed to resolve master";
            return;
        }
        status = "Resolving list from " + host + ":" + p.ToString();
        NetUIPatches.RefreshServerListNow(owner);
    }

    private int GetMasterPortValue()
    {
        int p;
        if (!int.TryParse((masterPort ?? string.Empty).Trim(), out p) || p < 1 || p > 65535)
        {
            return 0;
        }
        return p;
    }
}
