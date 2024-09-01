using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Networking;

namespace RealRuins;

internal class APIService
{
	private const string APIRoot = "https://woolstrand.art/";

	private const string BucketRoot = "https://realruinsv2.sfo2.digitaloceanspaces.com/";

	private const string MapsRandomListPath = "maps/random";

	private const string MapsBySeedListPath = "maps/seed/";

	private const string MapUploadPath = "maps";

	public static void AwaitUnityDataWebResponse(UnityWebRequest request, Action<byte[]> onSuccess, Action<Exception> onFailure, HttpStatusCode successStatus = HttpStatusCode.OK, float timeout = 30f)
	{
		request.SendWebRequest();
		float timeoutTime = Time.unscaledTime + timeout;
		Action pollingAction = null;
		pollingAction = delegate
		{
			bool flag = Time.unscaledTime > timeoutTime;
			try
			{
				if (!request.isDone && !flag)
				{
					//HugsLibController.Instance.DoLater.DoNextUpdate(pollingAction);
				}
				else
				{
					if (flag)
					{
						if (!request.isDone)
						{
							request.Abort();
						}
						throw new Exception("timed out");
					}
					if (request.isHttpError || request.isNetworkError)
					{
						throw new Exception(request.error);
					}
					HttpStatusCode httpStatusCode = (HttpStatusCode)request.responseCode;
					if (httpStatusCode != successStatus)
					{
						throw new Exception($"{request.url} replied with {httpStatusCode}: {request.downloadHandler.text}");
					}
					onSuccess?.Invoke(request.downloadHandler.data);
				}
			}
			catch (Exception obj)
			{
				if (onFailure != null)
				{
					onFailure(obj);
				}
			}
		};
		pollingAction();
	}

	public void LoadRandomMapsList(Action<bool, List<string>> completionHandler)
	{
		LoadRandomMapsList(50, completionHandler);
	}

	public void LoadRandomMapsList(int limit, Action<bool, List<string>> completionHandler)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		//IL_003d: Expected O, but got Unknown
		string text = "https://woolstrand.art/maps/random";
		UnityWebRequest request = new UnityWebRequest(text + "?limit=" + limit, "GET")
		{
			downloadHandler = (DownloadHandler)new DownloadHandlerBuffer()
		};
		Action<byte[]> onSuccess = delegate(byte[] response)
		{
			string @string = Encoding.UTF8.GetString(response);
			List<string> list = new List<string>();
			JSONNode jSONNode = JSON.Parse(@string);
			if (jSONNode != null)
			{
				JSONNode.Enumerator enumerator = jSONNode.GetEnumerator();
				while (enumerator.MoveNext())
				{
					JSONNode jSONNode2 = enumerator.Current;
					string text2 = jSONNode2["nameInBucket"]?.Value;
					if (text2 != null)
					{
						list.Add(text2);
					}
				}
			}
			completionHandler(arg1: true, list);
		};
		AwaitUnityDataWebResponse(request, onSuccess, failureHandler);
		void failureHandler(Exception ex)
		{
			Debug.Warning("Loader", string.Format("Could not load maps list, but that's ok if you already have enough maps.", ex), true);
			completionHandler(arg1: false, null);
		}
	}

	public void LoadAllMapsForSeed(string seed, int mapSize, float coverage, Action<bool, List<PlanetTileInfo>> completionHandler)
	{
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Expected O, but got Unknown
		//IL_0081: Expected O, but got Unknown
		string text = "https://woolstrand.art/maps/seed/" + seed;
		string text2 = text + "?limit=9999&coverage=" + coverage + "&mapSize=" + mapSize;
		Debug.Log("Loader", "Loading all compatible blueprings by link {0}", text2);
		UnityWebRequest request = new UnityWebRequest(text2, "GET")
		{
			downloadHandler = (DownloadHandler)new DownloadHandlerBuffer()
		};
		Action<byte[]> onSuccess = delegate(byte[] response)
		{
			string @string = Encoding.UTF8.GetString(response);
			List<PlanetTileInfo> list = new List<PlanetTileInfo>();
			JSONNode jSONNode = JSON.Parse(@string);
			if (jSONNode != null)
			{
				JSONNode.Enumerator enumerator = jSONNode.GetEnumerator();
				while (enumerator.MoveNext())
				{
					JSONNode jSONNode2 = enumerator.Current;
					string text3 = jSONNode2["nameInBucket"]?.Value;
					if (text3 != null)
					{
						list.Add(new PlanetTileInfo
						{
							mapId = text3,
							tile = (jSONNode2["tileId"]?.AsInt ?? 0),
							biomeName = jSONNode2["biome"]?.Value,
							originX = (jSONNode2["originX"]?.AsInt ?? 0),
							originZ = (jSONNode2["originZ"]?.AsInt ?? 0)
						});
					}
				}
			}
			completionHandler(arg1: true, list);
		};
		AwaitUnityDataWebResponse(request, onSuccess, failureHandler);
		void failureHandler(Exception ex)
		{
			Debug.Error("Loader", $"Could not load maps list. Try not to alt-tab while waiting for maps loading.", true);
			completionHandler(arg1: false, null);
		}
	}

	public void LoadMap(string link, Action<bool, byte[]> completionHandler)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		//IL_0037: Expected O, but got Unknown
		string text = "https://realruinsv2.sfo2.digitaloceanspaces.com/" + link + ".bp";
		UnityWebRequest request = new UnityWebRequest(text, "GET")
		{
			downloadHandler = (DownloadHandler)new DownloadHandlerBuffer()
		};
		Action<byte[]> onSuccess = delegate(byte[] response)
		{
			completionHandler(arg1: true, response);
		};
		AwaitUnityDataWebResponse(request, onSuccess, failureHandler);
		void failureHandler(Exception ex)
		{
			Debug.Warning("Loader", $"Could not load a blueprint, but that's not a problem if you already have enough. If your storage is empty, try manual download and do not alt-tab while loading.", true);
			completionHandler(arg1: false, null);
		}
	}

	public bool UploadMap(string sourceFileName, Action<bool> completionHandler = null)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected O, but got Unknown
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Expected O, but got Unknown
		//IL_005b: Expected O, but got Unknown
		string text = "https://woolstrand.art/maps";
		byte[] array = File.ReadAllBytes(sourceFileName);
		if (array == null)
		{
			return false;
		}
		UnityWebRequest request = new UnityWebRequest(text, "POST")
		{
			downloadHandler = (DownloadHandler)new DownloadHandlerBuffer(),
			uploadHandler = (UploadHandler)new UploadHandlerRaw(array)
			{
				contentType = "binary/octet-stream"
			}
		};
		Action<byte[]> onSuccess = delegate
		{
			Debug.Log("Loader", "Map upload successful");
			completionHandler?.Invoke(obj: true);
		};
		AwaitUnityDataWebResponse(request, onSuccess, failureHandler);
		return true;
		void failureHandler(Exception ex)
		{
			Debug.Warning("Loader", "Exception during uploading: {0}", ex);
			completionHandler?.Invoke(obj: false);
		}
	}
}
