using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Net;
using UnityEngine;
using UnityEngine.Networking;

using HugsLib.Utils;
using HugsLib.Core;

using Verse;
using HugsLib;

namespace RealRuins
{

    class EncryptionHelper {

        #region Hash Hex Functions
        public static string HashHMACHex(string keyHex, string message)
        {
            byte[] hash = HashHMAC(HexDecode(keyHex), StringEncode(message));
            return HashEncode(hash);
        }

        public static string HMACSHARawStrings(string key, string message)
        {
            byte[] hash = HashHMAC(StringEncode(key), StringEncode(message));
            return HashEncode(hash);
        }

        private static string HashSHAHex(string innerKeyHex, string outerKeyHex, string message)
        {
            byte[] hash = HashSHA(HexDecode(innerKeyHex), HexDecode(outerKeyHex), StringEncode(message));
            return HashEncode(hash);
        }
        #endregion

        #region Hash Functions
        private static byte[] HashHMAC(byte[] key, byte[] message)
        {
            var hash = new HMACSHA256(key);
            return hash.ComputeHash(message);
        }

        private static byte[] HashSHA(byte[] innerKey, byte[] outerKey, byte[] message)
        {
            var hash = new SHA256Managed();

            // Compute the hash for the inner data first
            byte[] innerData = new byte[innerKey.Length + message.Length];
            Buffer.BlockCopy(innerKey, 0, innerData, 0, innerKey.Length);
            Buffer.BlockCopy(message, 0, innerData, innerKey.Length, message.Length);
            byte[] innerHash = hash.ComputeHash(innerData);

            // Compute the entire hash
            byte[] data = new byte[outerKey.Length + innerHash.Length];
            Buffer.BlockCopy(outerKey, 0, data, 0, outerKey.Length);
            Buffer.BlockCopy(innerHash, 0, data, outerKey.Length, innerHash.Length);
            byte[] result = hash.ComputeHash(data);

            return result;
        }
        #endregion

        #region Encoding Helpers
        private static byte[] StringEncode(string text)
        {
            Encoding encoding = Encoding.UTF8;
            return encoding.GetBytes(text);
        }

        public static string HashEncode(byte[] hash)
        {
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        private static byte[] HexDecode(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++) {
                bytes[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber);
            }
            return bytes;
        }
        #endregion
    }

    class AmazonS3Service
    {

        private static readonly string bucketName = "realruins";

        private static readonly string publicKey = "QQJEHNSD5POEM6MNBYIX";
        private static readonly string secretKey = "pgRLvv+jeDLiVxmD6fBwVRMnHdZycXlYc4Ri763vD4Y";
        private static readonly string region = "sfo2";
        private static readonly string s3host = bucketName + "." + region + ".digitaloceanspaces.com";


        private bool canProcess = false;

        private UnityWebRequest activeRequest;


        private UnityWebRequest AmazonS3SignedWebRequest(string method, string path, string query, byte[] body)
        {
            string contentSHA256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"; //empty data hash
            if (body != null) {
                contentSHA256 = EncryptionHelper.HashEncode(SHA256.Create().ComputeHash(body));
            }

            DateTime date = DateTime.UtcNow;
            string amzDate = date.ToString("yyyyMMddTHHmmssZ");
            string shortDate = date.ToString("yyyyMMdd");
            string headersList = "host;x-amz-content-sha256;x-amz-date";

            string canonicalRequest =
                method + "\n" +
                "/" + path + "\n" +
                (query ?? "") + "\n" +
                "host:" + s3host + "\n" +
                "x-amz-content-sha256:" + contentSHA256 + "\n" +
                "x-amz-date:" + amzDate + "\n" +
                "\n" +
                headersList + "\n" +
                contentSHA256;


            string requestHash = EncryptionHelper.HashEncode(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(canonicalRequest)));

            string stringToSign =
                "AWS4-HMAC-SHA256" + "\n" +
                amzDate + "\n" +
                shortDate + "/" + region + "/" + "s3" + "/aws4_request" + "\n" +
                requestHash;

            string dateKey = EncryptionHelper.HMACSHARawStrings("AWS4" + secretKey, shortDate);
            string dateRegionKey = EncryptionHelper.HashHMACHex(dateKey, region);
            string dateRegionServiceKey = EncryptionHelper.HashHMACHex(dateRegionKey, "s3");
            string signingKey = EncryptionHelper.HashHMACHex(dateRegionServiceKey, "aws4_request");

            string signature = EncryptionHelper.HashHMACHex(signingKey, stringToSign);
            string headerValue = "AWS4-HMAC-SHA256 Credential=" + publicKey + "/" + shortDate + "/" + region + "/s3/aws4_request,SignedHeaders=" + headersList + ",Signature=" + signature;

            UnityWebRequest request = new UnityWebRequest("https://" + s3host + "/" + path + ((query != null)?("?" + query):""), method);

            if (body != null) {
                request.uploadHandler = new UploadHandlerRaw(body) {
                    contentType = "application/xml"
                };
            }

            request.downloadHandler = new DownloadHandlerBuffer();

/*            if (prefix != null) {
                request.SetRequestHeader("Prefix", prefix);
            }*/

            request.SetRequestHeader("Authorization", headerValue);
            request.SetRequestHeader("x-amz-date", amzDate);
            request.SetRequestHeader("x-amz-content-sha256", contentSHA256);

            return request;
        }

        public bool AmazonS3Upload(string localFilePath, string subDirectoryInBucket, string fileNameInS3)
        {

            byte[] fileBuffer = System.IO.File.ReadAllBytes(localFilePath);

            UnityWebRequest request = AmazonS3SignedWebRequest("PUT", fileNameInS3, null, fileBuffer);

            Action<string> successHandler = delegate (string response) {
                Log.Message("Map upload completed");
            };

            Action<Exception> failureHandler = delegate (Exception ex) {
                Log.Message(string.Format("Exception during base blueprint upload: {0}", ex));
            };

            this.activeRequest = request;
            HugsLibUtility.AwaitUnityWebResponse(request, successHandler, failureHandler);
            GC.KeepAlive(this);
            return true;
        }

        public bool AmazonS3ListFiles(string prefix, Action<List<string>> successHandler) {

            string query = null;
            if (prefix != null) {
                query = "prefix=" + prefix;
            }
            
            UnityWebRequest request = AmazonS3SignedWebRequest("GET", "", query, null);

            void InternalSuccessHandler(string response) {
                XmlDocument filesListDocument = new XmlDocument();
                filesListDocument.LoadXml(response);

                XmlNodeList filesList = filesListDocument.GetElementsByTagName("Contents");
                Debug.Message("Got names list");

                List<string> namesList = new List<string>();

                foreach (XmlNode node in filesList) {
                    if (node.HasChildNodes) {
                        foreach (XmlNode innerNode in node.ChildNodes) {
                            if (innerNode.Name == "Key") {
                                string value = innerNode.InnerText;
                                if (value != null) {
                                    namesList.Add(value);
                                }
                            }
                        }
                    }
                }

                successHandler(namesList);
            }

            Action<Exception> failureHandler = delegate (Exception ex) {
                Log.Message(string.Format("Exception during loading list {0}", ex), true);
            };

            this.activeRequest = request;
            HugsLibUtility.AwaitUnityWebResponse(request, InternalSuccessHandler, failureHandler);

            return true;
        }

        public bool AmazonS3DownloadSnapshot(string snapshotName, Action<bool, byte[]> completionHandler) {

            UnityWebRequest request = AmazonS3SignedWebRequest("GET", snapshotName, null, null);

            Action<byte[]> internalSuccessHandler = delegate (byte[] response) {
                completionHandler(true, response);
            };

            void failureHandler(Exception ex) {
                Log.Message(string.Format("Exception during loading object: {0}", ex), true);
                completionHandler(false, null);
            }

            this.activeRequest = request;
            AwaitUnityDataWebResponse(request, internalSuccessHandler, failureHandler);

            return true;
        }


        //local copy of hugslib method.
        //hugslib original method always returns string response, which renders it unusable for downloading binary data.
        public static void AwaitUnityDataWebResponse(UnityWebRequest request, Action<byte[]> onSuccess, Action<Exception> onFailure, HttpStatusCode successStatus = HttpStatusCode.OK, float timeout = 30f) {
            request.Send();
            float timeoutTime = Time.unscaledTime + timeout;
            Action pollingAction = null;
            pollingAction = delegate
            {
                bool flag = Time.unscaledTime > timeoutTime;
                try {
                    if (!request.isDone && !flag) {
                        HugsLibController.Instance.DoLater.DoNextUpdate(pollingAction);
                    } else {
                        if (flag) {
                            if (!request.isDone) {
                                request.Abort();
                            }
                            throw new Exception("timed out");
                        }
                        if (request.isError) {
                            throw new Exception(request.error);
                        }
                        HttpStatusCode httpStatusCode = (HttpStatusCode)request.responseCode;
                        if (httpStatusCode != successStatus) {
                            throw new Exception($"{request.url} replied with {httpStatusCode}: {request.downloadHandler.text}");
                        }
                        onSuccess?.Invoke(request.downloadHandler.data);
                    }
                } catch (Exception ex) {
                    if (onFailure != null) {
                        onFailure(ex);
                    }
                }
            };
            pollingAction();
        }



    }

}
