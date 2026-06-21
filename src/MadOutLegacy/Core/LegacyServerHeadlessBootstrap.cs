using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LegacyServerHeadlessBootstrap : MonoBehaviour
{
	public static bool IsHeadlessServer
	{
		get
		{
			if (!NetManagerTools.isCommandLineArgHaveServerStr())
			{
				return false;
			}
			if (Application.isBatchMode)
			{
				return true;
			}
			string[] commandLineArgs = Environment.GetCommandLineArgs();
			for (int i = 0; i < commandLineArgs.Length; i++)
			{
				if (commandLineArgs[i] == "-nographics")
				{
					return true;
				}
			}
			return false;
		}
	}

	public static void Install(GameObject host)
	{
		if (!LegacyServerHeadlessBootstrap.IsHeadlessServer || LegacyServerHeadlessBootstrap.installed || host == null)
		{
			return;
		}
		host.GetOrAddComponent<LegacyServerHeadlessBootstrap>();
		LegacyServerHeadlessBootstrap.installed = true;
	}

	private void OnEnable()
	{
		SceneManager.sceneLoaded += this.OnSceneLoaded;
		base.StartCoroutine(this.StripAllLoadedScenesDeferred());
	}

	private void OnDisable()
	{
		SceneManager.sceneLoaded -= this.OnSceneLoaded;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		base.StartCoroutine(this.StripSceneDeferred(scene));
	}

	private IEnumerator StripAllLoadedScenesDeferred()
	{
		yield return null;
		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			Scene sceneAt = SceneManager.GetSceneAt(i);
			if (sceneAt.IsValid() && sceneAt.isLoaded)
			{
				this.StripScene(sceneAt);
			}
		}
		yield return Resources.UnloadUnusedAssets();
		GC.Collect();
		yield break;
	}

	private IEnumerator StripSceneDeferred(Scene scene)
	{
		yield return null;
		this.StripScene(scene);
		yield return Resources.UnloadUnusedAssets();
		yield break;
	}

	private void StripScene(Scene scene)
	{
		GameObject[] rootGameObjects = scene.GetRootGameObjects();
		for (int i = 0; i < rootGameObjects.Length; i++)
		{
			this.StripRecursive(rootGameObjects[i].transform);
		}
	}

	private void StripRecursive(Transform tr)
	{
		Camera component = tr.GetComponent<Camera>();
		if (component != null)
		{
			component.enabled = false;
		}
		AudioListener component2 = tr.GetComponent<AudioListener>();
		if (component2 != null)
		{
			global::UnityEngine.Object.Destroy(component2);
		}
		AudioSource component3 = tr.GetComponent<AudioSource>();
		if (component3 != null)
		{
			global::UnityEngine.Object.Destroy(component3);
		}
		Light component4 = tr.GetComponent<Light>();
		if (component4 != null)
		{
			global::UnityEngine.Object.Destroy(component4);
		}
		ReflectionProbe component5 = tr.GetComponent<ReflectionProbe>();
		if (component5 != null)
		{
			global::UnityEngine.Object.Destroy(component5);
		}
		Canvas component6 = tr.GetComponent<Canvas>();
		if (component6 != null)
		{
			component6.enabled = false;
		}
		Graphic[] components = tr.GetComponents<Graphic>();
		for (int i = 0; i < components.Length; i++)
		{
			components[i].enabled = false;
		}
		ParticleSystem component7 = tr.GetComponent<ParticleSystem>();
		if (component7 != null)
		{
			global::UnityEngine.Object.Destroy(component7);
		}
		TrailRenderer component8 = tr.GetComponent<TrailRenderer>();
		if (component8 != null)
		{
			global::UnityEngine.Object.Destroy(component8);
		}
		LineRenderer component9 = tr.GetComponent<LineRenderer>();
		if (component9 != null)
		{
			global::UnityEngine.Object.Destroy(component9);
		}
		Renderer[] components2 = tr.GetComponents<Renderer>();
		for (int j = 0; j < components2.Length; j++)
		{
			global::UnityEngine.Object.Destroy(components2[j]);
		}
		LODGroup component10 = tr.GetComponent<LODGroup>();
		if (component10 != null)
		{
			global::UnityEngine.Object.Destroy(component10);
		}
		for (int k = 0; k < tr.childCount; k++)
		{
			this.StripRecursive(tr.GetChild(k));
		}
	}

	private static bool installed;
}
