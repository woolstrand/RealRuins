using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

using SimpleJSON;

using UnityEngine;
using UnityEngine.Networking;
using System.Net;
using System.IO;
using System.Collections;

namespace RealRuins
{
    class APIService
    {

        private const string APIRoot = "https://woolstrand.art/";
        //private const string APIRoot = "http://173.23.44.32/"; //for testing unresponsive host
        private const string BucketRoot = "https://realruinsv2.sfo2.digitaloceanspaces.com/";

        private const string MapsRandomListPath = "maps/random";
        private const string MapsBySeedListPath = "maps/seed/";
        private const string MapUploadPath = "maps";
        public static void AwaitUnityDataWebResponse(UnityWebRequest request, Action<byte[]> onSuccess, Action<Exception> onFailure, HttpStatusCode successStatus = HttpStatusCode.OK, float timeout = 30f)
        {
            CoroutineManager.Instance.RunCoroutine(AwaitResponseCoroutine(request, onSuccess, onFailure, successStatus, timeout));
        }
        private static IEnumerator AwaitResponseCoroutine(UnityWebRequest request, Action<byte[]> onSuccess, Action<Exception> onFailure, HttpStatusCode successStatus, float timeout)
        {
            request.SendWebRequest();
            float timeoutTime = Time.unscaledTime + timeout;

            while (!request.isDone && Time.unscaledTime < timeoutTime)
            {
                yield return null;  // Wait until next frame
            }

            if (request.isDone)
            {
                if (request.isHttpError || request.isNetworkError)
                {
                    onFailure?.Invoke(new Exception(request.error));
                }
                else if ((HttpStatusCode)request.responseCode != successStatus)
                {
                    onFailure?.Invoke(new Exception($"{request.url} replied with {(HttpStatusCode)request.responseCode}: {request.downloadHandler.text}"));
                }
                else
                {
                    onSuccess?.Invoke(request.downloadHandler.data);
                }
            }
            else
            {
                request.Abort();
                onFailure?.Invoke(new Exception("Request timed out"));
            }
        }
        //local copy of hugslib method.
        //hugslib original method always returns string response, which renders it unusable for downloading binary data.
        //public static void AwaitUnityDataWebResponse(UnityWebRequest request, Action<byte[]> onSuccess, Action<Exception> onFailure, HttpStatusCode successStatus = HttpStatusCode.OK, float timeout = 30f)
        //{
        //    request.SendWebRequest();
        //    float timeoutTime = Time.unscaledTime + timeout;
        //    Action pollingAction = null;
        //    pollingAction = delegate {
        //        bool flag = Time.unscaledTime > timeoutTime;
        //        try
        //        {
        //            if (!request.isDone && !flag)
        //            {
        //                HugsLibController.Instance.DoLater.DoNextUpdate(pollingAction);
        //            }
        //            else
        //            {
        //                if (flag)
        //                {
        //                    if (!request.isDone)
        //                    {
        //                        request.Abort();
        //                    }
        //                    throw new Exception("timed out");
        //                }
        //                if (request.isHttpError || request.isNetworkError)
        //                {
        //                    throw new Exception(request.error);
        //                }
        //                HttpStatusCode httpStatusCode = (HttpStatusCode)request.responseCode;
        //                if (httpStatusCode != successStatus)
        //                {
        //                    throw new Exception($"{request.url} replied with {httpStatusCode}: {request.downloadHandler.text}");
        //                }
        //                onSuccess?.Invoke(request.downloadHandler.data);
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            if (onFailure != null)
        //            {
        //                onFailure(ex);
        //            }
        //        }
        //    };
        //    pollingAction();
        //}



        public void LoadRandomMapsList(Action<bool, List<string>> completionHandler)
        {
            LoadRandomMapsList(50, completionHandler);
        }


        public void LoadRandomMapsList(int limit, Action<bool, List<string>> completionHandler)
        {
            var path = APIRoot + MapsRandomListPath;

            UnityWebRequest request = new UnityWebRequest(path + "?limit=" + limit.ToString(), "GET")
            {
                downloadHandler = new DownloadHandlerBuffer()
            };

            Action<byte[]> internalSuccessHandler = delegate (byte[] response) {
                string jsonString = Encoding.UTF8.GetString(response);

                List<string> names = new List<string>();

                var json = JSON.Parse(jsonString);
                if (json != null)
                {
                    foreach (JSONNode node in json)
                    {
                        string name = node["nameInBucket"]?.Value;
                        if (name != null)
                        {
                            names.Add(name);
                        }
                    }
                }

                completionHandler(true, names);
            };

            void failureHandler(Exception ex)
            {
                Debug.Warning(Debug.Loader, string.Format("Could not load maps list, but that's ok if you already have enough maps.", ex), true);
                completionHandler(false, null);
            }

            AwaitUnityDataWebResponse(request, internalSuccessHandler, failureHandler);
        }

        public void LoadAllMapsForSeed(string seed, int mapSize, float coverage, Action<bool, List<PlanetTileInfo>> completionHandler)
        {
            var path = APIRoot + MapsBySeedListPath + seed;

            string requestLink = path + "?limit=9999&coverage=" + coverage + "&mapSize=" + mapSize;
            Debug.Log(Debug.Loader, "Loading all compatible blueprings by link {0}", requestLink);
            UnityWebRequest request = new UnityWebRequest(requestLink, "GET")
            {
                downloadHandler = new DownloadHandlerBuffer()
            };

            Action<byte[]> internalSuccessHandler = delegate (byte[] response) {
                string jsonString = Encoding.UTF8.GetString(response);

                List<PlanetTileInfo> tiles = new List<PlanetTileInfo>();

                var json = JSON.Parse(jsonString);
                if (json != null)
                {
                    foreach (JSONNode node in json)
                    {
                        string name = node["nameInBucket"]?.Value;
                        if (name != null)
                        {
                            PlanetTileInfo tileInfo = new PlanetTileInfo();
                            tileInfo.mapId = name;
                            tileInfo.tile = node["tileId"]?.AsInt ?? 0;
                            tileInfo.biomeName = node["biome"]?.Value;
                            tileInfo.originX = node["originX"]?.AsInt ?? 0;
                            tileInfo.originZ = node["originZ"]?.AsInt ?? 0;
                            tiles.Add(tileInfo);
                        }
                    }
                }

                completionHandler(true, tiles);
            };

            void failureHandler(Exception ex)
            {
                Debug.Error(Debug.Loader, string.Format("Could not load maps list. Try not to alt-tab while waiting for maps loading."), true);
                completionHandler(false, null);
            }

            AwaitUnityDataWebResponse(request, internalSuccessHandler, failureHandler);
        }



        public void LoadMap(string link, Action<bool, byte[]> completionHandler)
        {
            var path = BucketRoot + link + ".bp";

            UnityWebRequest request = new UnityWebRequest(path, "GET")
            {
                downloadHandler = new DownloadHandlerBuffer()
            };

            Action<byte[]> internalSuccessHandler = delegate (byte[] response) {
                completionHandler(true, response);
            };

            void failureHandler(Exception ex)
            {
                Debug.Warning(Debug.Loader, string.Format("Could not load a blueprint, but that's not a problem if you already have enough. If your storage is empty, try manual download and do not alt-tab while loading."), true);
                completionHandler(false, null);
            }

            AwaitUnityDataWebResponse(request, internalSuccessHandler, failureHandler);
        }

        public bool UploadMap(string sourceFileName, Action<bool> completionHandler = null)
        {
            var path = APIRoot + MapUploadPath;

            var data = File.ReadAllBytes(sourceFileName);
            if (data == null) return false;

            UnityWebRequest request = new UnityWebRequest(path, "POST")
            {
                downloadHandler = new DownloadHandlerBuffer(),
                uploadHandler = new UploadHandlerRaw(data)
                {
                    contentType = "binary/octet-stream"
                }
            };


            Action<byte[]> internalSuccessHandler = delegate (byte[] response) {
                Debug.Log(Debug.Loader, "Map upload successful");
                completionHandler?.Invoke(true);
            };

            void failureHandler(Exception ex)
            {
                Debug.Warning(Debug.Loader, "Exception during uploading: {0}", ex);
                completionHandler?.Invoke(false);
            }

            AwaitUnityDataWebResponse(request, internalSuccessHandler, failureHandler);
            return true;
        }

    }
}