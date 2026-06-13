using System;
using System.IO;
using System.Xml;
using UnityEngine;

public class LegacyEventsConfig
{
	internal static bool TryLoadAndBuild()
	{
		if (LegacyEventsConfig.wasTried)
		{
			return LegacyEventsConfig.isReady;
		}
		LegacyEventsConfig.wasTried = true;
		if (!NetManagerTools.isCommandLineArgHaveServerStr())
		{
			return false;
		}
		string configPath = LegacyEventsConfig.GetConfigPath();
		LegacyEventsConfig.loadedConfigPath = configPath;
		if (!File.Exists(configPath))
		{
			LegacyEventsConfig.TryCreateExampleConfig(configPath);
			Debug.LogWarning("Events config not found: " + configPath + ". Example file was created. Event modes will work after you fill it.");
			return false;
		}
		bool flag;
		try
		{
			LegacyEventsConfig.Config config = LegacyJsonConfig.FromJson<LegacyEventsConfig.Config>(File.ReadAllText(configPath), configPath);
			if (config == null || config.events == null || config.events.Length == 0)
			{
				Debug.LogWarning("Events config is empty: " + configPath);
				flag = false;
			}
			else
			{
				LegacyEventsConfig.BuildRuntimeEventLists(config);
				LegacyEventsConfig.isReady = !EventsList.instances.isEmpty<EventsList>();
				Debug.Log(string.Concat(new string[]
				{
					"Events config loaded: ",
					configPath,
					", ready: ",
					LegacyEventsConfig.isReady.ToString(),
					", ",
					LegacyEventsConfig.GetDebugSummary()
				}));
				flag = LegacyEventsConfig.isReady;
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("Failed to load events config: " + configPath + "\n" + ex.ToString());
			flag = false;
		}
		return flag;
	}

	private static string GetConfigPath()
	{
		ArgParcer.ArgValue argValue = ArgParcer.GetArgValue("-eventsList");
		if (argValue != null && argValue.isExists && !string.IsNullOrEmpty(argValue.strValue))
		{
			return argValue.strValue;
		}
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
		return Path.Combine(Path.Combine(text, "MadOutLegacy"), "Events.json");
	}

	private static void TryCreateExampleConfig(string path)
	{
		try
		{
			string directoryName = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			if (!File.Exists(path))
			{
				File.WriteAllText(path, LegacyEventsConfig.GetExampleConfigText());
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("Failed to create example events config: " + ex.Message);
		}
	}

	private static string GetExampleConfigText()
	{
		return "{\n  \"version\": 1,\n  \"events\": [\n    {\n      \"mode\": \"Race\",\n      \"name\": \"example_race\",\n      \"displayName\": \"Example Race\",\n      \"isSprint\": true,\n      \"useRespawn\": false,\n      \"laps\": 3,\n      \"start\": [\n        { \"x\": 0, \"y\": 0, \"z\": 0, \"yaw\": 0 },\n        { \"x\": 1, \"y\": 1, \"z\": 1, \"yaw\": 0 }\n      ],\n      \"points\": [\n        { \"x\": 1, \"y\": 1, \"z\": 1, \"yaw\": 0 },\n        { \"x\": 2, \"y\": 2, \"z\": 2, \"yaw\": 90 },\n        { \"x\": 3, \"y\": 3, \"z\": 3, \"yaw\": 180 }\n      ]\n    },\n    {\n      \"mode\": \"CS\",\n      \"name\": \"example_cs\",\n      \"displayName\": \"Example CS\",\n      \"counter\": [ { \"x\": 1, \"y\": 1, \"z\": 1, \"yaw\": 90 } ],\n      \"terror\": [ { \"x\": 2, \"y\": 2, \"z\": 2, \"yaw\": -90 } ]\n    }\n  ]\n}\n";
	}

	private static void BuildRuntimeEventLists(LegacyEventsConfig.Config config)
	{
		if (LegacyEventsConfig.runtimeRoot)
		{
			global::UnityEngine.Object.Destroy(LegacyEventsConfig.runtimeRoot);
			LegacyEventsConfig.runtimeRoot = null;
		}
		LegacyEventsConfig.runtimeRoot = new GameObject("ModEvents_RuntimeDatabase");
		global::UnityEngine.Object.DontDestroyOnLoad(LegacyEventsConfig.runtimeRoot);
		LegacyEventsConfig.CreateList(config, "Race", "NetRace");
		LegacyEventsConfig.CreateList(config, "RaceParcur", "Parkurs");
		LegacyEventsConfig.CreateList(config, "Parkur", "Parkurs");
		LegacyEventsConfig.CreateList(config, "CS", "CS");
		LegacyEventsConfig.CreateList(config, "CS_Sur", "CS_Sur");
		LegacyEventsConfig.CreateList(config, "Survival", "CS_Sur");
	}

	private static void CreateList(LegacyEventsConfig.Config config, string mode, string savePostfix)
	{
		GameObject gameObject = new GameObject("ModEvents_" + savePostfix);
		gameObject.SetActive(false);
		gameObject.transform.SetParent(LegacyEventsConfig.runtimeRoot.transform, false);
		int num = 0;
		for (int i = 0; i < config.events.Length; i++)
		{
			LegacyEventsConfig.EventDef eventDef = config.events[i];
			if (eventDef != null && string.Equals(eventDef.mode, mode, StringComparison.OrdinalIgnoreCase))
			{
				Transform transform = LegacyEventsConfig.CreateEventMap(eventDef, savePostfix, num);
				if (transform)
				{
					transform.SetParent(gameObject.transform, false);
					num++;
				}
			}
		}
		if (num == 0)
		{
			global::UnityEngine.Object.Destroy(gameObject);
			return;
		}
		gameObject.AddComponent<EventsList>().savePostfix = savePostfix;
		gameObject.SetActive(true);
		Debug.Log("Mod events list created: " + savePostfix + ", count: " + num.ToString());
	}

	private static Transform CreateEventMap(LegacyEventsConfig.EventDef def, string savePostfix, int index)
	{
		if (savePostfix == "NetRace" || savePostfix == "Parkurs")
		{
			return LegacyEventsConfig.CreateRaceMap(def, savePostfix, index);
		}
		return LegacyEventsConfig.CreateCsMap(def, savePostfix, index);
	}

	private static Transform CreateRaceMap(LegacyEventsConfig.EventDef def, string savePostfix, int index)
	{
		if (def.points == null || def.points.Length < 2)
		{
			Debug.LogWarning("Skip race event '" + def.name + "': need at least 2 points");
			return null;
		}
		if (def.start == null || def.start.Length == 0)
		{
			Debug.LogWarning("Skip race event '" + def.name + "': need at least 1 start position");
			return null;
		}
		GameObject gameObject = new GameObject(LegacyEventsConfig.GetSafeName(def, savePostfix, index));
		gameObject.SetActive(false);
		RaceDirectionPoints raceDirectionPoints = gameObject.AddComponent<RaceDirectionPoints>();
		raceDirectionPoints.isSprint = def.isSprint;

		if (def.laps > 1 || def.minLaps > 1 || def.maxLaps > 1)
		{
			raceDirectionPoints.isSprint = false;
		}

		LegacyRaceDirectionMetadata.Get(raceDirectionPoints).onlineLapsCount = def.laps;
		LegacyRaceDirectionMetadata.Get(raceDirectionPoints).onlineMinLapsCount = def.minLaps;
		LegacyRaceDirectionMetadata.Get(raceDirectionPoints).onlineMaxLapsCount = def.maxLaps;

		raceDirectionPoints.useRespaum = def.useRespawn;
		raceDirectionPoints.dontUseOnline = false;
		gameObject.AddComponent<StartPoses>().items = LegacyEventsConfig.ToStartItems(def.start);
		UI_Event_Item_Info ui_Event_Item_Info = gameObject.AddComponent<UI_Event_Item_Info>();
		ui_Event_Item_Info.txt = LegacyEventsConfig.GetDisplayName(def, gameObject.name);
		ui_Event_Item_Info.WinCoins = ((def.reward > 0) ? def.reward : 1000);
		for (int i = 0; i < def.points.Length; i++)
		{
			LegacyEventsConfig.CreateRacePoint(gameObject.transform, def.points[i], i);
		}
		return gameObject.transform;
	}

	private static Transform CreateCsMap(LegacyEventsConfig.EventDef def, string savePostfix, int index)
	{
		if (savePostfix == "CS")
		{
			if (def.counter == null || def.counter.Length == 0 || def.terror == null || def.terror.Length == 0)
			{
				Debug.LogWarning("Skip CS event '" + def.name + "': need counter and terror positions");
				return null;
			}
		}
		else if (def.start == null || def.start.Length == 0)
		{
			Debug.LogWarning("Skip CS_Sur event '" + def.name + "': need start positions");
			return null;
		}
		GameObject gameObject = new GameObject(LegacyEventsConfig.GetSafeName(def, savePostfix, index));
		gameObject.SetActive(false);
		UI_Event_Item_Info ui_Event_Item_Info = gameObject.AddComponent<UI_Event_Item_Info>();
		ui_Event_Item_Info.txt = LegacyEventsConfig.GetDisplayName(def, gameObject.name);
		ui_Event_Item_Info.WinCoins = ((def.reward > 0) ? def.reward : 1000);
		if (savePostfix == "CS")
		{
			LegacyEventsConfig.CreateStartPosesChild(gameObject.transform, "CS_Conter", def.counter);
			LegacyEventsConfig.CreateStartPosesChild(gameObject.transform, "CS_Terror", def.terror);
		}
		else
		{
			gameObject.AddComponent<StartPoses>().items = LegacyEventsConfig.ToStartItems(def.start);
		}
		return gameObject.transform;
	}

	private static void CreateStartPosesChild(Transform parent, string name, LegacyEventsConfig.PosRot[] positions)
	{
		GameObject gameObject = new GameObject(name);
		gameObject.SetActive(false);
		gameObject.transform.SetParent(parent, false);
		gameObject.AddComponent<StartPoses>().items = LegacyEventsConfig.ToStartItems(positions);
	}

	private static void CreateRacePoint(Transform parent, LegacyEventsConfig.PointDef point, int index)
	{
		LegacyEventsConfig.CreateFallbackRacePoint(parent, point.ToVector3(), new Vector3(0f, point.yaw, 0f), point.radius, index);
	}

	private static Transform CreateChildTransform(Transform parent, string name)
	{
		GameObject gameObject = new GameObject(name);
		gameObject.transform.SetParent(parent, false);
		return gameObject.transform;
	}

	internal static void SaveRuntimeRacePointsToXml(RaceDirectionPoints points, XmlElement root)
	{
		RaceDirection[] componentsInChildren = points.GetComponentsInChildren<RaceDirection>(true);
		if (componentsInChildren == null || componentsInChildren.Length == 0)
		{
			return;
		}
		XmlElement xmlElement = root.Add("modPoints");
		foreach (RaceDirection raceDirection in componentsInChildren)
		{
			XmlElement xmlElement2 = xmlElement.Add("p");
			xmlElement2.SetVec3("pos", raceDirection.transform.position);
			xmlElement2.SetVec3("rot", raceDirection.transform.rotation.eulerAngles);
			float num = 8f;
			SphereCollider component = raceDirection.GetComponent<SphereCollider>();
			if (component)
			{
				num = component.radius;
			}
			xmlElement2.SetFloat("radius", num);
		}
	}

	internal static void LoadRuntimeRacePointsFromXml(RaceDirectionPoints points, XmlElement root)
	{
		XmlElement canNull = root.GetCanNull("modPoints");
		if (canNull == null)
		{
			return;
		}
		int num = 0;
		for (XmlElement xmlElement = (XmlElement)canNull.FirstChild; xmlElement != null; xmlElement = (XmlElement)xmlElement.NextSibling)
		{
			Vector3 vec = xmlElement.GetVec3("pos");
			Vector3 vec2 = xmlElement.GetVec3("rot");
			float @float = xmlElement.GetFloat("radius", 8f);
			LegacyEventsConfig.CreateRacePointInternal(points.transform, vec, vec2, @float, num);
			num++;
		}
	}

	private static StartPoses.Item[] ToStartItems(LegacyEventsConfig.PosRot[] positions)
	{
		if (positions == null)
		{
			return new StartPoses.Item[0];
		}
		StartPoses.Item[] array = new StartPoses.Item[positions.Length];
		for (int i = 0; i < positions.Length; i++)
		{
			array[i].pos = positions[i].ToVector3();
			array[i].rot = new Vector3(0f, positions[i].yaw, 0f);
		}
		return array;
	}

	private static string GetSafeName(LegacyEventsConfig.EventDef def, string savePostfix, int index)
	{
		if (def != null && !string.IsNullOrEmpty(def.name))
		{
			return def.name;
		}
		return savePostfix + "_" + index.ToString("00");
	}

	private static string GetDisplayName(LegacyEventsConfig.EventDef def, string fallback)
	{
		if (def != null && !string.IsNullOrEmpty(def.displayName))
		{
			return def.displayName;
		}
		if (def != null && !string.IsNullOrEmpty(def.name))
		{
			return def.name;
		}
		return fallback;
	}

	internal static string GetDebugSummary()
	{
		string text = "EventsList count: " + EventsList.instances.Count.ToString();
		for (int i = 0; i < EventsList.instances.Count; i++)
		{
			EventsList eventsList = EventsList.instances[i];
			if (eventsList)
			{
				text = string.Concat(new string[]
				{
					text,
					" [",
					eventsList.savePostfix,
					"=",
					eventsList.evenTransforms.Count.ToString(),
					"]"
				});
			}
		}
		return text;
	}

	private static RaceDirection CreateFallbackRacePoint(Transform parent, Vector3 pos, Vector3 rot, float radius, int index)
	{
		GameObject gameObject = new GameObject("Point_" + index.ToString("00"));
		gameObject.transform.SetParent(parent, false);
		gameObject.transform.position = pos;
		gameObject.transform.rotation = Quaternion.Euler(rot);
		gameObject.SetActive(false);
		RaceDirection raceDirection = gameObject.AddComponent<RaceDirection>();
		RaceSpawnPoint raceSpawnPoint = gameObject.AddComponent<RaceSpawnPoint>();
		raceDirection.ourPoint = raceSpawnPoint;
		raceSpawnPoint.thisDirection = raceDirection;
		SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
		sphereCollider.isTrigger = true;
		sphereCollider.radius = ((radius > 0f) ? radius : 8f);
		raceDirection.nextPosMiniMapIcon = LegacyEventsConfig.CreateChildTransform(gameObject.transform, "NextPosMiniMapIcon");
		LegacyEventsConfig.SetupFallbackMiniMapIcon(raceDirection.nextPosMiniMapIcon.gameObject);
		raceDirection.lookAtCam = LegacyEventsConfig.CreateChildTransform(gameObject.transform, "LookAtCam");
		raceDirection.rotByDirection = LegacyEventsConfig.CreateChildTransform(gameObject.transform, "Direction");
		raceDirection.lookAt_Test = LegacyEventsConfig.CreateChildTransform(gameObject.transform, "LookAtTest");
		return raceDirection;
	}

	private static RaceDirection CreateRacePointInternal(Transform parent, Vector3 pos, Vector3 rot, float radius, int index)
	{
		RaceDirection racePointTemplate = LegacyEventsConfig.GetRacePointTemplate();
		if (racePointTemplate)
		{
			RaceDirection raceDirection = LegacyEventsConfig.CreateRacePointFromTemplate(racePointTemplate, parent, pos, rot, radius, index);
			if (raceDirection)
			{
				return raceDirection;
			}
		}
		return LegacyEventsConfig.CreateFallbackRacePoint(parent, pos, rot, radius, index);
	}

	private static RaceDirection GetRacePointTemplate()
	{
		if (LegacyEventsConfig.racePointTemplateSearchDone)
		{
			return LegacyEventsConfig.cachedRacePointTemplate;
		}
		LegacyEventsConfig.racePointTemplateSearchDone = true;
		RaceDirection raceDirection = LegacyEventsConfig.TryFindRacePointTemplateInRaceObjects();
		if (raceDirection)
		{
			LegacyEventsConfig.cachedRacePointTemplate = raceDirection;
			Debug.Log("ModEvents: race checkpoint template found in RaceObjects: " + LegacyEventsConfig.GetTransformPath(raceDirection.transform));
			return LegacyEventsConfig.cachedRacePointTemplate;
		}
		RaceDirection[] array = Resources.FindObjectsOfTypeAll<RaceDirection>();
		RaceDirection raceDirection2 = null;
		int num = -1;
		foreach (RaceDirection raceDirection3 in array)
		{
			if (raceDirection3 && raceDirection3.gameObject)
			{
				string transformPath = LegacyEventsConfig.GetTransformPath(raceDirection3.transform);
				if (transformPath.IndexOf("ModEvents_RuntimeDatabase") == -1 && transformPath.IndexOf("Used RacePoints") == -1)
				{
					int racePointTemplateScore = LegacyEventsConfig.GetRacePointTemplateScore(raceDirection3);
					if (racePointTemplateScore > num)
					{
						num = racePointTemplateScore;
						raceDirection2 = raceDirection3;
					}
				}
			}
		}
		LegacyEventsConfig.cachedRacePointTemplate = raceDirection2;
		if (raceDirection2)
		{
			Debug.Log("ModEvents: race checkpoint template found: " + LegacyEventsConfig.GetTransformPath(raceDirection2.transform) + ", score: " + num.ToString());
		}
		else
		{
			Debug.LogWarning("ModEvents: race checkpoint template was not found. Fallback checkpoint will be used.");
		}
		return LegacyEventsConfig.cachedRacePointTemplate;
	}

	private static int GetRacePointTemplateScore(RaceDirection point)
	{
		if (!point)
		{
			return -1;
		}
		if (!point.ourPoint)
		{
			return -1;
		}
		int num = 0;
		if (point.GetComponent<Collider>())
		{
			num += 10;
		}
		if (point.GetComponentInChildren<MeshRenderer>(true))
		{
			num += 10;
		}
		if (point.nextPosMiniMapIcon)
		{
			num += 20;
			if (point.nextPosMiniMapIcon.GetComponentInChildren<MiniMap_Icon>(true))
			{
				num += 30;
			}
			if (point.nextPosMiniMapIcon.GetComponentInChildren<TargetPointer>(true))
			{
				num += 10;
			}
		}
		if (point.lookAtCam)
		{
			num += 5;
		}
		if (point.rotByDirection)
		{
			num += 5;
		}
		if (point.lookAt_Test)
		{
			num += 5;
		}
		return num;
	}

	private static RaceDirection CreateRacePointFromTemplate(RaceDirection template, Transform parent, Vector3 pos, Vector3 rot, float radius, int index)
	{
		if (!template)
		{
			return null;
		}
		RaceDirection raceDirection = global::UnityEngine.Object.Instantiate<RaceDirection>(template);
		if (!raceDirection)
		{
			return null;
		}
		GameObject gameObject = raceDirection.gameObject;
		gameObject.name = "Point_" + index.ToString("00");
		gameObject.transform.SetParent(parent, false);
		gameObject.transform.position = pos;
		gameObject.transform.rotation = Quaternion.Euler(rot);
		gameObject.SetActive(false);
		RaceSpawnPoint raceSpawnPoint = raceDirection.ourPoint;
		if (!raceSpawnPoint)
		{
			raceSpawnPoint = gameObject.GetComponent<RaceSpawnPoint>();
		}
		if (!raceSpawnPoint)
		{
			raceSpawnPoint = gameObject.GetComponentInChildren<RaceSpawnPoint>(true);
		}
		if (!raceSpawnPoint)
		{
			raceSpawnPoint = gameObject.AddComponent<RaceSpawnPoint>();
		}
		raceDirection.ourPoint = raceSpawnPoint;
		raceSpawnPoint.thisDirection = raceDirection;
		Collider component = gameObject.GetComponent<Collider>();
		if (!component)
		{
			SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
			sphereCollider.isTrigger = true;
			sphereCollider.radius = ((radius > 0f) ? radius : 8f);
		}
		else
		{
			component.isTrigger = true;
			SphereCollider sphereCollider2 = component as SphereCollider;
			if (sphereCollider2 && radius > 0f)
			{
				sphereCollider2.radius = radius;
			}
		}
		if (!raceDirection.nextPosMiniMapIcon)
		{
			raceDirection.nextPosMiniMapIcon = LegacyEventsConfig.CreateChildTransform(gameObject.transform, "NextPosMiniMapIcon");
			LegacyEventsConfig.SetupFallbackMiniMapIcon(raceDirection.nextPosMiniMapIcon.gameObject);
		}
		if (!raceDirection.lookAtCam)
		{
			raceDirection.lookAtCam = LegacyEventsConfig.CreateChildTransform(gameObject.transform, "LookAtCam");
		}
		if (!raceDirection.rotByDirection)
		{
			raceDirection.rotByDirection = LegacyEventsConfig.CreateChildTransform(gameObject.transform, "Direction");
		}
		if (!raceDirection.lookAt_Test)
		{
			raceDirection.lookAt_Test = LegacyEventsConfig.CreateChildTransform(gameObject.transform, "LookAtTest");
		}
		return raceDirection;
	}

	private static void SetupFallbackMiniMapIcon(GameObject gameObject)
	{
		if (!gameObject)
		{
			return;
		}
		MiniMap_Icon miniMap_Icon = gameObject.GetComponent<MiniMap_Icon>();
		if (!miniMap_Icon)
		{
			miniMap_Icon = gameObject.AddComponent<MiniMap_Icon>();
		}
		if (RaceSettings.me)
		{
			miniMap_Icon.spriteTexture = RaceSettings.me.miniMapIcon;
		}
		miniMap_Icon.color = new Color(0.2f, 0.8f, 1f, 1f);
		miniMap_Icon.iconScale = 1.2f;
		miniMap_Icon.opacity = 100;
		miniMap_Icon.order = 20;
		miniMap_Icon.dontUseShadow = true;
		TargetPointer targetPointer = gameObject.GetComponent<TargetPointer>();
		if (!targetPointer)
		{
			targetPointer = gameObject.AddComponent<TargetPointer>();
		}
		targetPointer.customTarget = gameObject.transform;
		targetPointer.imgColor = miniMap_Icon.color;
		targetPointer.linesColor = miniMap_Icon.color;
		targetPointer.mul_color_opacity = 0.8f;
		if (RaceSettings.me)
		{
			targetPointer.custom_sprite = RaceSettings.me.miniMapIcon;
		}
		miniMap_Icon.enableDisable = targetPointer;
	}

	private static string GetTransformPath(Transform transform)
	{
		if (!transform)
		{
			return "null";
		}
		string text = transform.name;
		while (transform.parent)
		{
			transform = transform.parent;
			text = transform.name + "/" + text;
		}
		return text;
	}

	private static RaceDirection TryFindRacePointTemplateInRaceObjects()
	{
		LinkResource[] array = Resources.LoadAll<LinkResource>("RaceObjects");
		RaceDirection raceDirection = null;
		int num = -1;
		foreach (LinkResource linkResource in array)
		{
			if (linkResource && linkResource.resource is GameObject)
			{
				foreach (RaceDirection raceDirection2 in (linkResource.resource as GameObject).GetComponentsInChildren<RaceDirection>(true))
				{
					int racePointTemplateScore = LegacyEventsConfig.GetRacePointTemplateScore(raceDirection2);
					if (racePointTemplateScore > num)
					{
						num = racePointTemplateScore;
						raceDirection = raceDirection2;
					}
				}
			}
		}
		if (raceDirection)
		{
			Debug.Log("ModEvents: loaded RaceObjects resources: " + array.Length.ToString() + ", best checkpoint score: " + num.ToString());
		}
		else
		{
			Debug.LogWarning("ModEvents: RaceObjects resources loaded, but checkpoint template was not found. Resources count: " + array.Length.ToString());
		}
		return raceDirection;
	}

	private static bool wasTried;

	private static bool isReady;

	internal static string loadedConfigPath;

	private static GameObject runtimeRoot;

	private static RaceDirection cachedRacePointTemplate;

	private static bool racePointTemplateSearchDone;

	[Serializable]
	public class Config
	{
		public int version;

		public LegacyEventsConfig.EventDef[] events;
	}

	[Serializable]
	public class EventDef
	{
		public string mode;

		public string name;

		public string displayName;

		public int reward;

		public bool isSprint = true;

		public bool useRespawn;

		public int laps = 1;

		public int minLaps;

		public int maxLaps;

		public LegacyEventsConfig.PosRot[] start;

		public LegacyEventsConfig.PosRot[] counter;

		public LegacyEventsConfig.PosRot[] terror;

		public LegacyEventsConfig.PointDef[] points;
	}

	[Serializable]
	public class PosRot
	{
		public Vector3 ToVector3()
		{
			return new Vector3(this.x, this.y, this.z);
		}

		public float x;

		public float y;

		public float z;

		public float yaw;
	}

	[Serializable]
	public class PointDef : LegacyEventsConfig.PosRot
	{
		public float radius = 8f;
	}
}
