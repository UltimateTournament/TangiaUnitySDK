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
        private readonly string gameVersion;

        public string SessionKey { private get; set; }

        /*
	        
	        
	        
	        /game/interactions/stop_playing
         */

        public TangiaAPI(string gameToken, string gameVersion)
        {
            this.apiAddr = Environment.GetEnvironmentVariable("TANGIA_ADDR") ?? "https://api.tangia.co";
            this.gameToken = gameToken;
            this.gameVersion = gameVersion;
        }

        public IEnumerator Login(string code, Action<LoginResult> callback)
        {
            return httpCall("POST", "/game/login", null, new GameLoginReq { GameID = gameToken, Code = code },
                webReq => callback(new LoginResult { Success = true, SessionKey = JsonConvert.DeserializeObject<GameLoginResp>(webReq.text).SessionID }),
                err => callback(new LoginResult { Success = false, ErrorMessage = err })
                );
        }

        public IEnumerator PollEvents(Action<GameEventsResp> callback)
        {
            return httpCall("POST", "/game/interactions/poll", SessionKey, new GameEventsReq { GameVersion = gameVersion },
                webReq => callback(JsonConvert.DeserializeObject<GameEventsResp>(webReq.text)),
                err => callback(null)
                );
        }

        public IEnumerator AckEvent(string eventID)
        {
            var result = new AckInteractionEventsReq { EventResults = new[] { new EventResult { EventID = eventID, Executed=true } } };
            return httpCall("POST", "/game/interactions/ack", SessionKey, result,
                webReq => { },
                err => { }
                );
        }

        public IEnumerator RejectEvent(string eventID, string reason)
        {
            var result = new AckInteractionEventsReq { EventResults = new[] { new EventResult { EventID = eventID, Executed = false, Message= reason } } };
            return httpCall("POST", "/game/interactions/ack", SessionKey, result,
                webReq => { },
                err => { }
                );
        }

        public IEnumerator StopPlaying()
        {
            return httpCall("POST", "/game/interactions/ack", SessionKey, null,
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
            }
        }
    }

    class GameLoginReq
    {

        [JsonProperty(PropertyName = "GameID")]
        public string GameID { get; set; }

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
        public bool Success { get; set; }
        public string SessionKey { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class GameEventsReq
    {
        [JsonProperty(PropertyName = "GameVersion")]
        public string GameVersion { get; set; }
    }
    public class GameEventsResp
    {
        [JsonProperty(PropertyName = "Events")]
        public GameEvent[] Events { get; set; }
    }
    public class GameEvent
    {
        [JsonProperty(PropertyName = "EventID")]
        public string EventID { get; set; }

        [JsonProperty(PropertyName = "InteractionID")]
        public string InteractionID { get; set; }

        [JsonProperty(PropertyName = "Price")]
        public long Price { get; set; }

        [JsonProperty(PropertyName = "BuyerName")]
        public string BuyerName { get; set; }
    }

    public class AckInteractionEventsReq
    {
        [JsonProperty(PropertyName = "EventResults")]
        public EventResult[] EventResults { get; set; }
    }

    public class EventResult
    {
        [JsonProperty(PropertyName = "EventID")]
        public string EventID { get; set; }

        [JsonProperty(PropertyName = "Executed")]
        public bool Executed { get; set; }

        [JsonProperty(PropertyName = "Message")]
        public string Message { get; set; }
    }

}