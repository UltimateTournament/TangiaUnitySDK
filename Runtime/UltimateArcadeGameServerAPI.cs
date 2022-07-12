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
        private readonly string gameToken;

        public TangiaAPI(string gameToken)
        {
            this.apiAddr = Environment.GetEnvironmentVariable("TANGIA_ADDR") ?? "https://api.tangia.co";
            this.gameToken = gameToken;
        }

        public IEnumerator Init(string playerCode, string gameVersion, Action<GameEventsResp> callback, Action<string> errorCallback)
        {
            return httpCall("POST", "/game-interactions/" + gameToken, playerCode, new GameEventsReq { GameVersion = gameVersion }, 
                webReq => callback(JsonConvert.DeserializeObject<GameEventsResp>(webReq.text)), 
                errorCallback);
        }


        //TODO change thos actions to be coroutines, too
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
            }
        }
    }

    public class GameEventsReq
    {
        [JsonProperty(PropertyName = "GameVersion")]
        public string GameVersion { get; set; }
    }
    public class GameEventsResp
    {
        [JsonProperty(PropertyName = "Events")]
        GameEvent[] Events { get; set; }
    }
    public class GameEvent
    {
        [JsonProperty(PropertyName = "ID")]
        public string ID { get; set; }
        [JsonProperty(PropertyName = "Price")]
        public long Price { get; set; }
        [JsonProperty(PropertyName = "BuyerName")]
        public string BuyerName { get; set; }
    }

}