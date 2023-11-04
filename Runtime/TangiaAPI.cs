using System;
using System.Collections;
using UnityEngine.Networking;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace Tangia
{
    public class TangiaAPI
    {
        private readonly string apiAddr;
        private readonly string gameVersion;

        public string AccountKey { private get; set; }

        public TangiaAPI(string gameVersion, string apiAddr = "https://api.tangia.co")
        {
            this.apiAddr = apiAddr;
            this.gameVersion = gameVersion;
        }

        public IEnumerator Login(string code, Action<LoginResult> callback)
        {
            return httpCall("POST", "/v2/actions/login", null, new GameLoginReq { VersionInfo = this.gameVersion, Code = code },
                webReq => callback(new LoginResult { Success = true, AccountKey = JsonConvert.DeserializeObject<GameLoginResp>(webReq.text).AccountKey }),
                err => callback(new LoginResult { Success = false, ErrorMessage = err })
                );
        }

        public IEnumerator Logout()
        {
            return httpCall("POST", "/v2/actions/logout", AccountKey, null,
                webReq => { },
                err => { }
                );
        }

        // poll game-sepcific events
        public IEnumerator PollEvents<T>(Action<GameEventsResp<T>> callback)
        {
            return httpCall("GET", "/v2/actions/pending", AccountKey, null,
                webReq => callback(JsonConvert.DeserializeObject<GameEventsResp<T>>(webReq.text)),
                err => callback(new GameEventsResp<T> { Error = err })
                );
        }

        public IEnumerator AckEvent(string eventID)
        {
            return httpCall("POST", "/v2/actions/ack/" + eventID, AccountKey, null,
                webReq => { },
                err => { }
                );
        }

        public IEnumerator RejectEvent(string eventID, string reason)
        {
            return httpCall("POST", "/v2/actions/nack/" + eventID + "?reason=" + reason, AccountKey, null,
                webReq => { },
                err => { }
                );
        }

        private IEnumerator httpCall(string method, string path, string authToken, object body, Action<DownloadHandlerBuffer> callback, Action<string> errorCallback)
        {
            using (var webReq = new UnityWebRequest(this.apiAddr + path, method))
            {
                var dl = new DownloadHandlerBuffer();
                webReq.downloadHandler = dl;
                if (authToken != null)
                {
                    webReq.SetRequestHeader("Authorization", "Bearer " + authToken);
                }
                if (body != null)
                {
                    var json = JsonConvert.SerializeObject(body);
                    webReq.SetRequestHeader("Content-Type", "application/json");
                    webReq.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                }
                // for long-polling we need to set long timeouts
                webReq.timeout = 60;
                yield return webReq.SendWebRequest();

                switch (webReq.result)
                {
                    case UnityWebRequest.Result.ConnectionError:
                    case UnityWebRequest.Result.DataProcessingError:
                    case UnityWebRequest.Result.ProtocolError:
                        errorCallback("Error: " + webReq.error);
                        break;
                    case UnityWebRequest.Result.Success:
                        if (webReq.responseCode >= 201 || webReq.responseCode >= 500 || webReq.responseCode == 429)
                        {
                            // just retry
                            yield return new WaitForSeconds(1);
                            var res = httpCall(method, path, authToken, body, callback, errorCallback);
                            while (res.MoveNext())
                            {
                                yield return res.Current;
                            }
                        }
                        else if (webReq.responseCode >= 400)
                        {
                            // we're assuming all other 4xx errors are permanent
                            errorCallback("HTTP Status: " + webReq.responseCode);
                        }
                        else
                        {
                            callback(dl);
                        }
                        break;
                }
                dl.Dispose();
                if (webReq.uploadHandler != null)
                    webReq.uploadHandler.Dispose();
            }
        }
    }

    class GameLoginReq
    {
        [JsonProperty(PropertyName = "VersionInfo")]
        public string VersionInfo { get; set; }

        [JsonProperty(PropertyName = "Code")]
        public string Code { get; set; }
    }

    class GameLoginResp
    {

        [JsonProperty(PropertyName = "SessionID")]
        public string SessionID { get; set; }
    }

    public class LoginResult
    {
        [JsonProperty(PropertyName = "AccountKey")]
        public string AccountKey { get; set; }
    }

    public class ActionExecution
    {
        [JsonProperty(PropertyName = "ID")]
        public String ID { get; set; }

        [JsonProperty(PropertyName = "Trigger")]
        public String Trigger { get; set; }

        [JsonProperty(PropertyName = "Body")]
        public T Body { get; set; }

        [JsonProperty(PropertyName = "Ttl")]
        public String Ttl { get; set; }
    }

    public class GameEventsResp<T>
    {
        public string Error { get; set; }

        [JsonProperty(PropertyName = "ActionExecutions")]
        public ActionExecution[] ActionExecutions { get; set; }
    }
}
