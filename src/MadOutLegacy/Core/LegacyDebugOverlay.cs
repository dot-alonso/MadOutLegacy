using System;
using System.IO;
using UnityEngine;

public class LegacyDebugOverlay : MonoBehaviour, OnGuiGlobal.IOnGuiNeed
{
	internal static void InitIfNeeded()
	{
		if (LegacyDebugOverlay.instance)
		{
			return;
		}
		if (!LegacyDebugOverlay.HasCommandLineArg("-debugoverlay"))
		{
			return;
		}
		GameObject gameObject = new GameObject("ModDebugOverlay");
		global::UnityEngine.Object.DontDestroyOnLoad(gameObject);
		LegacyDebugOverlay.instance = gameObject.AddComponent<LegacyDebugOverlay>();
	}

	private static bool HasCommandLineArg(string arg)
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		for (int i = 0; i < commandLineArgs.Length; i++)
		{
			if (commandLineArgs[i] == arg)
			{
				return true;
			}
		}
		return false;
	}

	private void OnEnable()
	{
		OnGuiGlobal.items.Remove(this);
		OnGuiGlobal.items.Add(this);
		string text = getPosOutFilePath();
		if (File.Exists(text))
		{
			File.Delete(text);
		}
	}

	private void OnDisable()
	{
		OnGuiGlobal.items.Remove(this);
	}

	public bool isNeedOnGUI()
	{
		return true;
	}

	public void OnGUI_Manual()
	{
		Matrix4x4 matrix = GUI.matrix;
		int depth = GUI.depth;
		GUI.matrix = Matrix4x4.identity;
		GUI.depth = -20000;
		Vector3 vector = hCamera.firstCamPos;
		Vector3 vector2 = Vector3.zero;
		Quaternion quaternion = Quaternion.identity;
		if (CamCurrent.curSceneLoad && CamCurrent.curSceneLoad.unityCam)
		{
			Transform transform = CamCurrent.curSceneLoad.unityCam.transform;
			vector = transform.position;
			quaternion = transform.rotation;
		}
		InputControl firstUser = InputControl.firstUser;
		if (firstUser && firstUser.currentOrParent)
		{
			vector2 = firstUser.currentOrParent.body_pos;
		}
		this.jsonCopyText = string.Concat(new string[]
		{
			"{ \"x\": ",
			vector.x.ToString("0.###"),
			", \"y\": ",
			vector.y.ToString("0.###"),
			", \"z\": ",
			vector.z.ToString("0.###"),
			", \"yaw\": ",
			quaternion.eulerAngles.y.ToString("0.###"),
			" }"
		});
		string text = "<b>DEBUG OVERLAY</b>\n";
		text = text + "Camera pos: " + LegacyDebugOverlay.FormatVector(vector) + "\n";
		text = text + "Camera rot: " + LegacyDebugOverlay.FormatVector(quaternion.eulerAngles) + "\n";
		text = text + "Player pos: " + LegacyDebugOverlay.FormatVector(vector2) + "\n";
		text = text + "Point JSON: " + this.jsonCopyText + "\n";
		text += "(Press X to copy, Z to write in file)";
		GUI.Box(new Rect(10f, 10f, 600f, 100f), text);
		GUI.matrix = matrix;
		GUI.depth = depth;
	}

	private void OnGUI()
	{
		OnGUI_Manual();
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
		if (Input.GetKeyDown(KeyCode.X))
		{
			GUIUtility.systemCopyBuffer = this.jsonCopyText;
			if (GameUI.me)
			{
				GameUI.me.ShowPopup("JSON line copied into clipboard", 1.5f, true);
			}
		}

		if (Input.GetKeyDown(KeyCode.Z))
		{
			this.writeToPosOutFile();
			if (GameUI.me)
			{
				GameUI.me.ShowPopup("JSON line saved in pos_out.txt", 1.5f, true);
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
}
