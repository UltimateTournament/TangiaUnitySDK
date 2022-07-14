# Tangia Unity Game SDK

This is the Unity SDK for integrating games with Tangia. Tangia lets audiences interact directly
with their streamers by paying for fun events to happen, customized items and more!

You simply need to register<sup>*</sup> at https://tangia.co , create your games and list the 
interactions/items that you want to offer. The IDs of the items you configure are passed to your
game in events you receive with this SDK.

_<sup>*</sup>currently in closed beta_

## Installation

- In the Unity Editor go to `Window > Package Manager`
- Click the + at the top left of the Package Manager window
- Select `Add package from git URL...`
- Paste the following URL `https://github.com/UltimateTournament/ArcadeUnitySDK.git` and click `Add`

## Usage

Implementing our SDK consists of only 3 parts

### Login

Content creators log into your game with a short code that they get from your Tangia profile.
You need to offer the content creator a way to configure this code, e.g. as a text box in your settings.

> Make sure to enable pasting the code, so your players don't have to type out their codes

These codes are one-time use: You send them to our API and get a session token that you need
to store, so the player doesn't have to re-enter the code every time

```cs
LoginResult login = null;
yield return api.Login(creatorShortCode, lr => login = lr);
if (login.Success)
    storeSessionKey(login.SessionKey);
```

### Polling and Confirming Events

If the user is logged in and within a game you have to poll our API for interaction events and for each event
either acknowledge or reject it, so we know if it was successful.

> You have to confirm events to get the next one

```cs
GameEventsResp resp = null;
yield return api.PollEvents(e => resp = e);
if (resp != null && resp.Events != null)
    foreach (var evt in resp.Events)
    {
        var couldHandleEvent = handleInteractionEvent(evt.InteractionID);
        if (couldHandleEvent)
            yield return api.AckEvent(evt.EventID);
        else
            yield return api.RejectEvent(evt.EventID, "the reason why we rejected");
```

### Stop Polling and Notify Game Stopped

You should only be polling for events when the player is actively playing the game
and interaction events _could_ be processed.
You should not poll when they are in a menu or lobby screen because, so their audience doesn't try to 
interact with them while it's not possible.

This is also when you should mark the session as "not playing", so the audience gets instant feedback.

```cs
StartCoroutine(api.StopPlaying());
```

### Complete Code Example

Below you can see an example of how you could use our SDK in your code. The 
`OnStreamerCodeEntered`, `OnStartPlaying` and `OnStopPlaying` could be called from your game as-is,
and the `PollEvents` would need to actually interact with your game, e.g. by spawning new items.


```cs
using System.Collections;
using UnityEngine;
using Tangia;

public class TangiaSpawner : MonoBehaviour
{
    // Get this ID from the Tangia game developer dashboard
    // The version of your game is used to ensure we only send interaction events this version understands
    TangiaAPI api = new TangiaAPI("game_abcdef123", "1.0.0");

    // In a real game, this should be stored permanently (e.g. on disk, or in browser local storage etc)
    private string sessionKey = null;

    public void OnStreamerCodeEntered(string code)
    {
        StartCoroutine(Login(code));
    }

    // The code that a streamer enters into your game is very short lived.
    // This call verifies it and exchanges it for a long lived session key
    IEnumerator Login(string code)
    {
        LoginResult login = null;
        yield return api.Login(code, lr => login = lr);
        if (login.Success)
        {
            sessionKey = login.SessionKey;
            api.SessionKey = sessionKey;
            // we don't want code that waits for login to wait forever
            StartCoroutine(nameof(PollEvents));
        }
        else
        {
            Debug.Log("login error: " + login.ErrorMessage);
        }
    }

    // Call this when the player starts playing and you'd be ready to accept events
    public void OnStartPlaying()
    {
        if (sessionKey != null)
        {
            api.SessionKey = sessionKey;
            StartCoroutine(nameof(PollEvents));
        }
    }

    // Call this when the player is not actively playing, e.g. when they're in a menu or
    // simply are closing the game
    public void OnStopPlaying()
    {
        StopCoroutine(nameof(PollEvents));
        StartCoroutine(api.StopPlaying());
    }

    // This constantly checks for new events and notifies our backend of success or failure.
    // You don't need to worry about performance of this loop as the API is "long-polling".
    // This means, if there is no event ready yet it will wait for up to a minute before returning,
    // So this loop actually doesn't run very often
    private IEnumerator PollEvents()
    {
        while (true)
        {
            GameEventsResp resp = null;
            yield return api.PollEvents(e => resp = e);
            if (resp == null || resp.Events == null)
            {
                yield return new WaitForSeconds(0.2f);
                continue;
            }
            foreach (var evt in resp.Events)
            {
                Debug.Log("we got an event: " + evt.ToString());
                // TODO: check if the game can handle this interaction event right now and process it
                //   e.g. by spawning a new item depending on `evt.InteractionID`
                var couldHandleEvent = true;
                if (couldHandleEvent)
                    yield return api.AckEvent(evt.EventID);
                else
                    yield return api.RejectEvent(evt.EventID, "the reason why we rejected");
            }
        }
    }
}
```
