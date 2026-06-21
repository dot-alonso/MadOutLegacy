using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx;
using Newtonsoft.Json;
using RootMotion;
using UnityEngine;
using WebSocketSharp;
using static nNode;
using Debug = UnityEngine.Debug;

public class LegacyDebugOverlay : MonoBehaviour, OnGuiGlobal.IOnGuiNeed
{
	internal static void InitIfNeeded()
	{
		if (LegacyDebugOverlay.instance)
		{
			return;
		}

        if (!LegacyHelpers.HasCommandLineArg("-pointTool", "-debugoverlay"))
        {
            return;
        }

        GameObject gameObject = new GameObject("ModDebugOverlay");
		global::UnityEngine.Object.DontDestroyOnLoad(gameObject);
		LegacyDebugOverlay.instance = gameObject.AddComponent<LegacyDebugOverlay>();
        string text = getPosOutFilePath();
        if (File.Exists(text))
        {
            File.Delete(text);
        }
    }

	public bool isNeedOnGUI()
	{
		return true;
	}

    private bool ShowTeleportWindow = false;
    private bool jsonByPlayerPos = false;
    private bool UseVisualPoints = false;
    private string teleportX;
    private string teleportY;
    private string teleportZ;
    private string teleportJson;
    private int teleportParseMode = 0;
    private string[] teleportParseModes = { "RAW", "JSON" };
	private bool UseFade = false;
	private bool teleportWithoutCar = false;
    private bool isInteractive = false;

    public void OnGUI_Manual()
	{
		Matrix4x4 matrix = GUI.matrix;
		int depth = GUI.depth;
		GUI.matrix = Matrix4x4.identity;
		GUI.depth = -20000;

		Vector3 cameraPos = hCamera.firstCamPos;
		Vector3 playerPos = Vector3.zero;
		Quaternion cameraRot = Quaternion.identity;

		if (CamCurrent.curSceneLoad && CamCurrent.curSceneLoad.unityCam)
		{
			Transform transform = CamCurrent.curSceneLoad.unityCam.transform;
			cameraPos = transform.position;
			cameraRot = transform.rotation;
		}

		InputControl firstUser = InputControl.firstUser;
		if (firstUser && firstUser.currentOrParent)
		{
			playerPos = firstUser.currentOrParent.body_pos;
		}

        Vector3 jsonPos = jsonByPlayerPos ? playerPos : cameraPos;

        jsonCopyText = "{ \"x\": " + FormatJsonFloat(jsonPos.x) +
                       ", \"y\": " + FormatJsonFloat(jsonPos.y) +
                       ", \"z\": " + FormatJsonFloat(jsonPos.z) +
                       ", \"yaw\": " + FormatJsonFloat(cameraRot.eulerAngles.y) +
                       " }";

        string mainText = "<b>POINT TOOL v2</b>\n";
		mainText += "Camera pos: " + FormatVector(cameraPos) + "\n";
		mainText += "Camera rot: " + FormatVector(cameraRot.eulerAngles) + "\n";
		mainText += "Player pos: " + FormatVector(playerPos) + "\n";
		mainText += "Point JSON: " + jsonCopyText + "\n";
        //mainText += "(F8 toggle, F9 copy, X remove last, Z save)";

        Rect boxRect = new Rect(10f, 10f, 460f, 165f);
        GUI.Box(boxRect, mainText);
		GUI.BeginGroup(boxRect);

		float RectMainX = 10f;
        float RectLine1_Y = 100f;
        float RectLine2_Y = 130f;
        float RectButtonWidth = 75f;
		float RectHeight = 25f;

        Color32 hotkeysHintColor = new Color32(255, 255, 255, 192);
        GUIStyle hotkeysHint = new GUIStyle(GUI.skin.label);
        hotkeysHint.fontStyle = FontStyle.Italic;
        hotkeysHint.fontSize = 12;
        hotkeysHint.normal.textColor = hotkeysHintColor;

        GUI.Label(new Rect(105f, 82f, 300f, 50f), "<b>F8</b> - hide | <b>F9</b> - copy | <b>X</b> - remove last | <b>Z</b> - save", hotkeysHint);

        string TeleportButtonLabel = "▼ Teleport";

        if (ShowTeleportWindow)
        {
            TeleportButtonLabel = "▲ Close";
        }

        if (!NetManager.isOnlineClient)
        {
            if (GUI.Button(new Rect(RectMainX, RectLine2_Y, RectButtonWidth, RectHeight), TeleportButtonLabel))
            {
                ShowTeleportWindow = !ShowTeleportWindow;
            }
        }

        if (GUI.Button(new Rect(RectMainX + 365f, RectLine2_Y, RectButtonWidth, RectHeight), "Clear All"))
        {
            ClearAllPoints();
        }

        if (GUI.Button(new Rect(RectMainX + 260f, RectLine2_Y, RectButtonWidth + 25f, RectHeight), "Open out file"))
        {
            OpenPosOutFile();
        }

        Color32 separatorColor = new Color32(255, 255, 255, 128);
		GUIStyle separatorStyle = new GUIStyle();
		separatorStyle.fontSize = 20;
		separatorStyle.normal.textColor = separatorColor;

		float RectjsonByPlayerPosWidth = 135f;
		float RectToggleX = RectMainX + RectButtonWidth + 110f + RectjsonByPlayerPosWidth;

		string copyModeLabel;
		float RectjsonByPlayerPosX;

		if (!jsonByPlayerPos)
		{
			copyModeLabel = "JSON by Player pos";
			RectjsonByPlayerPosX = RectToggleX - RectjsonByPlayerPosWidth - 20f;
		}
		else
		{
			copyModeLabel = "JSON by Camera pos";
			RectjsonByPlayerPosX = RectToggleX - RectjsonByPlayerPosWidth - 28f;
		}


        float RectUseVisualX;

        if (UseVisualPoints)
        {
            RectUseVisualX = RectToggleX - 95f;
            jsonByPlayerPos = GUI.Toggle(new Rect(RectjsonByPlayerPosX - 95f, RectLine1_Y + 4f, RectjsonByPlayerPosWidth + 10f, RectHeight), jsonByPlayerPos, $" {copyModeLabel}");

            GUI.Label(new Rect(RectUseVisualX - 10f, RectLine1_Y + 2f, 10f, RectHeight), "|", separatorStyle);
            UseVisualPoints = GUI.Toggle(new Rect(RectUseVisualX, RectLine1_Y + 4f, 135f, RectHeight), UseVisualPoints, " Use visual points");

            GUI.Label(new Rect(RectToggleX + 29f, RectLine1_Y + 2f, 10f, RectHeight), "|", separatorStyle);
            isInteractive = GUI.Toggle(new Rect(RectToggleX + 38f, RectLine1_Y + 4f, 135f, RectHeight), isInteractive, " Interactive");
        }
        else
        {
            RectUseVisualX = RectToggleX;
            jsonByPlayerPos = GUI.Toggle(new Rect(RectjsonByPlayerPosX, RectLine1_Y + 4f, RectjsonByPlayerPosWidth + 10f, RectHeight), jsonByPlayerPos, $" {copyModeLabel}");
            GUI.Label(new Rect(RectToggleX - 11f, RectLine1_Y + 2f, 10f, RectHeight), "|", separatorStyle);
            UseVisualPoints = GUI.Toggle(new Rect(RectUseVisualX, RectLine1_Y + 4f, 135f, RectHeight), UseVisualPoints, " Use visual points");
            LegacyEventsConfig.ClearDebugRaceCheckpoints();
        }

        GUI.EndGroup();

        if (ShowTeleportWindow)
		{
            Rect boxTeleportRect = new Rect(10f, 180f, 460f, 80f);
            GUI.Box(boxTeleportRect, string.Empty);
			GUI.BeginGroup(boxTeleportRect);

			float RectTeleportX = 10f;
			float RectTeleportY = 10f;
			float RectFieldWidth = 120f;

			teleportParseMode = GUI.Toolbar(new Rect(RectTeleportX, 45f, 125f, RectHeight), teleportParseMode, teleportParseModes);

			if (teleportParseMode == 0)
			{
				GUI.Label(new Rect(RectTeleportX, RectTeleportY + 2f, 25f, RectHeight), "X: ");
				teleportX = GUI.TextField(new Rect(RectTeleportX + 20f, RectTeleportY, RectFieldWidth, RectHeight), teleportX);

				float RectFieldY_X = RectTeleportX + RectFieldWidth + 50f;
				GUI.Label(new Rect(RectFieldY_X - 20f, RectTeleportY + 2f, 25f, RectHeight), "Y: ");
				teleportY = GUI.TextField(new Rect(RectFieldY_X, RectTeleportY, RectFieldWidth, RectHeight), teleportY);

				float RectFieldZ_X = RectFieldY_X + RectFieldWidth + 30f;
				GUI.Label(new Rect(RectFieldZ_X - 20f, RectTeleportY + 2f, 25f, RectHeight), "Z: ");
				teleportZ = GUI.TextField(new Rect(RectFieldZ_X, RectTeleportY, RectFieldWidth, RectHeight), teleportZ);
			}
			else
			{
				GUI.Label(new Rect(RectTeleportX, RectTeleportY + 2f, 50f, RectHeight), "JSON: ");
				teleportJson = GUI.TextField(new Rect(RectTeleportX + 45f, RectTeleportY, 395f, RectHeight), teleportJson);
			}

            UseFade = GUI.Toggle(new Rect(RectTeleportX + 135f, 47f, 50f, RectHeight), UseFade, " Fade");
            teleportWithoutCar = GUI.Toggle(new Rect(RectTeleportX + 195f, 47f, 75f, RectHeight), teleportWithoutCar, " w/o car");

            if (GUI.Button(new Rect(RectTeleportX + 305f, 45f, 80f, RectHeight), "To CamPos"))
            {
                TeleportByPoint(cameraPos, UseFade, teleportWithoutCar);
            }

            if (GUI.Button(new Rect(RectTeleportX + 390f, 45f, 50f, RectHeight), "GO!"))
			{
				if (teleportParseMode == 0)
				{
					if (PosParser.TryParsePosFromString(teleportX, teleportY, teleportZ, out Vector3 teleportPos))
					{
						TeleportByPoint(teleportPos, UseFade, teleportWithoutCar);
					}
				}
				else
				{
                    if (PosParser.TryParsePosFromJson(teleportJson, out Vector3 teleportPos, out _))
                    {
                        TeleportByPoint(teleportPos, UseFade, teleportWithoutCar);
                    }
                }
			}

			GUI.EndGroup();
        }

        GUI.matrix = matrix;
        GUI.depth = depth;
    }

	private bool RemoveLastPoint()
	{
		string lastPoint = PeekLastPointFromFile();

        if (lastPoint == null)
        {
            return false;
        }

        if (!PosParser.TryParsePosFromJson(lastPoint, out Vector3 pointPos, out _))
        {
            string warningMessage = "Failed to parse last point from pos_out.txt";
            Debug.LogWarning("[MadOutLegacy] " + warningMessage + ": " + lastPoint);

            if (GameUI.me)
            {
                GameUI.me.ShowPopup(warningMessage, 0.75f, true);
            }

            return false;
        }

		if (UseVisualPoints)
		{
            if (!LegacyEventsConfig.RemoveDebugRaceCheckpoint(pointPos))
            {
                return false;
            }
        }

        return RemoveLastPointFromFile();
    }

	private static string PeekLastPointFromFile()
	{
		string filePath = getPosOutFilePath();

        if (!File.Exists(filePath))
        {
            string warningMessage = "pos_out.txt is empty or not exists";
            Debug.LogWarning("[MadOutLegacy] " + warningMessage);

            if (GameUI.me)
            {
                GameUI.me.ShowPopup(warningMessage, 0.75f, true);
            }

            return null;
        }

        string[] lines = File.ReadAllLines(filePath);

        if (lines.Length == 0)
        {
            string warningMessage = "pos_out.txt is empty or not exists";
            Debug.LogWarning("[MadOutLegacy] " + warningMessage);

            if (GameUI.me)
            {
                GameUI.me.ShowPopup(warningMessage, 0.75f, true);
            }

            return null;
        }

        for (int i = lines.Length - 1; i >= 0; i--)
        {
            string line = lines[i];

            if (!string.IsNullOrWhiteSpace(line))
            {
                return line.Trim().TrimEnd(',');
            }
        }

        string emptyWarningMessage = "pos_out.txt contains no valid points";
        Debug.LogWarning("[MadOutLegacy] " + emptyWarningMessage);

        if (GameUI.me)
        {
            GameUI.me.ShowPopup(emptyWarningMessage, 0.75f, true);
        }

        return null;
    }

    private void OpenPosOutFile()
    {
        if (!CheckPosOutFileExistsOrNotEmpty())
        {
            return;
        }

        string filePath = getPosOutFilePath();

        Process.Start(new ProcessStartInfo
        {
            FileName = filePath,
            UseShellExecute = true
        });
    }

    private static bool RemoveLastPointFromFile()
	{
		string filePath = getPosOutFilePath();

        if (!File.Exists(filePath))
        {
            return false;
        }

        var lines = File.ReadAllLines(filePath).ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count == 0)
        {
            File.WriteAllText(filePath, string.Empty);
            return false;
        }

        lines.RemoveAt(lines.Count - 1);

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count > 0)
        {
            string prevLast = lines[lines.Count - 1].TrimEnd();

            if (prevLast.EndsWith(",", StringComparison.Ordinal))
            {
                lines[lines.Count - 1] = prevLast.Substring(0, prevLast.Length - 1);
            }
        }

        File.WriteAllLines(filePath, lines);

        return true;
    }

    private bool CheckPosOutFileExistsOrNotEmpty()
    {
        string filePath = getPosOutFilePath();

        if (!File.Exists(filePath))
        {
            if (GameUI.me)
            {
                GameUI.me.ShowPopup("pos_out.txt is empty or not exists", 0.75f, true);
            }
            return false;
        }

        var lines = File.ReadAllLines(filePath).ToList();

        if (lines.Count == 0)
        {
            if (GameUI.me)
            {
                GameUI.me.ShowPopup("pos_out.txt is empty or not exists", 0.75f, true);
            }
            return false;
        }

        return true;
    }

    private void ClearAllPoints()
    {
        if (!CheckPosOutFileExistsOrNotEmpty())
        {
            return;
        }

        string filePath = getPosOutFilePath();

        AskBoxUI.Show("Clear ALL saved points?", delegate (AskBoxUI box)
        {
            if (box.isYes)
            {
                LegacyEventsConfig.ClearDebugRaceCheckpoints();
                string file = getPosOutFilePath();
                File.Delete(file);
                if (GameUI.me)
                {
                    GameUI.me.ShowPopup("All points cleared", 0.75f, true);
                }
            }
        });
    }

    public static class PosParser
    {
        public static bool TryParsePosFromJson(string json, out Vector3 returnPos, out float? returnYaw)
        {
            returnPos = Vector3.zero;
            float? yaw = null;
            returnYaw = yaw;

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                PosJson data = JsonConvert.DeserializeObject<PosJson>(json);

                if (data == null)
                {
                    return false;
                }

                returnPos = new Vector3(data.x, data.y, data.z);
                yaw = data.yaw;
                returnYaw = yaw;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryParsePosFromString(string x, string y, string z, out Vector3 teleportPos)
        {
            teleportPos = Vector3.zero;

            bool numX = TryParseFloat(x, out float xValue);
            bool numY = TryParseFloat(y, out float yValue);
            bool numZ = TryParseFloat(z, out float zValue);

            if (!numX || !numY || !numZ)
            {
                Debug.LogWarning("[MadOutLegacy] Teleport raw parse failed: x=" + x + ", y=" + y + ", z=" + z);
                return false;
            }

            teleportPos = new Vector3(xValue, yValue, zValue);
            return true;
        }

        private static bool TryParseFloat(string value, out float result)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                return true;
            }

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(value))
            {
                value = value.Replace(',', '.');
                return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
            }

            result = 0f;
            return false;
        }

        private class PosJson
        {
            public float x;
            public float y;
            public float z;
            public float yaw;
        }
    }

    private static PlayerTeleport activeTeleport;

    private static void TeleportByPoint(Vector3 point, bool fade, bool withoutCar)
    {
        InputControl input = InputControl.GetFirstUser();

        if (!input)
        {
            Debug.LogWarning("[MadOutLegacy] Teleport failed: first user input is null");
            return;
        }

        Control current = input.current;

        if (!current)
        {
            Debug.LogWarning("[MadOutLegacy] Teleport failed: current control is null");
            return;
        }

        if (activeTeleport)
        {
            UnityEngine.Object.Destroy(activeTeleport.gameObject);
            activeTeleport = null;
        }

        Quaternion rot = current.body_rot;

        activeTeleport = PlayerTeleport.CreatGo("MadOutLegacy_DebugTeleport");
        activeTeleport.transform.position = point;
        activeTeleport.transform.rotation = rot;

        activeTeleport.Init(
            fade,   // auto fade
            withoutCar,   // clear parent control
            current  // target control
        );

        Debug.Log("[MadOutLegacy] Teleport started: " + FormatVector(point));
    }

    private void OnGUI()
	{
        if (!LegacyHelpers.IsGameplayRunning())
        {
            return;
        }

        if (!_isVisible)
		{
			return;
		}

		OnGUI_Manual();
	}

    private static string FormatJsonFloat(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatVector(Vector3 v)
	{
		return string.Concat(new string[]
		{
			v.x.ToString("0.###"),
			", ",
			v.y.ToString("0.###"),
			", ",
			v.z.ToString("0.###")
		});
	}

	private void Update()
	{
        if (Input.GetKeyDown(KeyCode.F8))
        {
            _isVisible = !_isVisible;
        }

        if (!LegacyHelpers.IsGameplayRunning())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.F9))
		{
            if (!_isVisible)
            {
                return;
            }

            GUIUtility.systemCopyBuffer = this.jsonCopyText;
            if (GameUI.me)
			{
				GameUI.me.ShowPopup("JSON line copied into clipboard", 0.75f, true);
			}
		}

        if (Input.GetKeyDown(KeyCode.X))
        {
            if (!_isVisible)
            {
                return;
            }

            if (GameUI.me && RemoveLastPoint())
            {
                GameUI.me.ShowPopup("Last point removed", 0.75f, true);
            }
        }

        if (Input.GetKeyDown(KeyCode.Z))
		{
            if (!_isVisible)
            {
                return;
            }

            if (UseVisualPoints)
			{
                Vector3 pos;
                float? yaw = 0f;
                PosParser.TryParsePosFromJson(jsonCopyText, out pos, out yaw);
                if (isInteractive)
                {
                    LegacyEventsConfig.SpawnDebugRaceCheckpoint(pos, true, yaw.GetValueOrDefault());
                }
                else
                {
                    LegacyEventsConfig.SpawnDebugRaceCheckpoint(pos, false, yaw.GetValueOrDefault());
                }
            }

            this.writeToPosOutFile();
			if (GameUI.me)
			{
				GameUI.me.ShowPopup("JSON line saved in pos_out.txt", 0.75f, true);
			}
		}
    }

	private void writeToPosOutFile()
	{
		string file = getPosOutFilePath();
		string dir = Path.GetDirectoryName(file);
		if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
		{
			Directory.CreateDirectory(dir);
		}
		string text = this.jsonCopyText;
		string lb = ",\n";
		File.AppendAllText(file, string.Empty);

		if (new FileInfo(file).Length != 0)
		{
			File.AppendAllText(file, lb + text);
		}
		else
		{
			File.AppendAllText(file, text);
		}
	}

	private static string getPosOutFilePath()
	{
		string text = LegacyHelpers.GameRoot;
		try
		{
			if (!string.IsNullOrEmpty(Application.dataPath))
			{
				DirectoryInfo parent = Directory.GetParent(Application.dataPath);
				if (parent != null)
				{
					text = parent.FullName;
				}
			}
		}
		catch
		{
		}
		return Path.Combine(Path.Combine(text, "MadOutLegacy"), "pos_out.txt");
	}

	private static LegacyDebugOverlay instance;

	private string jsonCopyText;

	private bool _isVisible = true;
}
