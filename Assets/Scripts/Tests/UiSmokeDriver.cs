using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Network;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Temporary test instrumentation. Removed from Assets after the smoke build.
public sealed class UiSmokeDriver : MonoBehaviour
{
    [Serializable] public class Command { public int id; public string action; public string path; public string text; public int width; public int height; }
    private string _directory;
    private int _last;
    private bool _busy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        string directory = Environment.GetEnvironmentVariable("POKER_UI_SMOKE");
        if (string.IsNullOrEmpty(directory)) return;
        if (Game.LoginCredentialStore.SavedUserName.StartsWith("Gift", StringComparison.Ordinal))
            Game.LoginCredentialStore.Clear();
        var driver = new GameObject("UiSmokeDriver").AddComponent<UiSmokeDriver>();
        driver._directory = directory;
        Directory.CreateDirectory(directory);
        DontDestroyOnLoad(driver.gameObject);
        SceneManager.sceneLoaded += driver.Configure;
        Application.logMessageReceived += driver.Log;
        Application.runInBackground = true;
    }

    private void Configure(Scene scene, LoadSceneMode mode)
    {
        var auth = FindObjectOfType<AccountAuthenticator>();
        if (auth != null) auth.ConfigureDatabaseFileName("RaiseGiftFix-20260912.db");
    }

    private void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Warning)
            File.AppendAllText(Path.Combine(_directory, "errors.log"), type + ": " + message + "\n" + stack + "\n");
    }

    private float _nextGiftTrace;
    private int _lastEffects;
    private void TraceGifts()
    {
        if (Time.unscaledTime < _nextGiftTrace) return;
        _nextGiftTrace = Time.unscaledTime + 0.2f;
        var gifts = Game.UI.Gifts.GiftInteractionController.Active;
        if (gifts == null) return;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        Func<string, object> field = name => gifts.GetType().GetField(name, flags).GetValue(gifts);
        var effects = gifts.GetComponentsInChildren<Game.UI.Gifts.RottenEggGiftEffect>(true);
        var trace = new { time = Mirror.NetworkTime.time, seen = ((System.Collections.ICollection)field("_seenEventOrder")).Count,
            queue = ((System.Collections.ICollection)field("_playback")).Count, routine = field("_playbackRoutine") != null,
            effects = effects.Select(e => new { e.IsPlaying, e.Elapsed, e.Phase, active = e.gameObject.activeInHierarchy }).ToArray() };
        File.AppendAllText(Path.Combine(_directory, "gifts.jsonl"), System.Text.Json.JsonSerializer.Serialize(trace) + "\n");
        if (effects.Length > 0 && _lastEffects == 0) StartCoroutine(CaptureGift());
        _lastEffects = effects.Length;
    }
    private IEnumerator CaptureGift()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(_directory, "gift-playing.png"));
    }
    private void Update()
    {
        TraceGifts();
        if (_busy || string.IsNullOrEmpty(_directory)) return;
        string file = Path.Combine(_directory, "command.json");
        if (!File.Exists(file)) return;
        Command command;
        try { command = JsonUtility.FromJson<Command>(File.ReadAllText(file)); }
        catch { return; }
        if (command == null || command.id <= _last) return;
        _last = command.id;
        StartCoroutine(Execute(command));
    }

    private IEnumerator Execute(Command command)
    {
        _busy = true;
        string error = null;
        try
        {
            if (command.action == "quit") Application.Quit();
            else if (command.action == "resize") Screen.SetResolution(command.width, command.height, false);
            else if (command.action == "disconnect") Mirror.NetworkManager.singleton.StopClient();
            else if (command.action == "click" || command.action == "input")
            {
                var target = Resources.FindObjectsOfTypeAll<Transform>().Single(t => t.gameObject.activeInHierarchy && PathOf(t) == command.path).gameObject;
                var rect = target.GetComponent<RectTransform>();
                var canvas = target.GetComponentInParent<Canvas>();
                var point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center));
                var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(data, hits);
                if (hits.Count == 0 || !hits[0].gameObject.transform.IsChildOf(target.transform))
                    throw new InvalidOperationException("Blocked raycast: " + (hits.Count == 0 ? "none" : PathOf(hits[0].gameObject.transform)));
                var selectable = target.GetComponent<Selectable>();
                if (selectable != null && !selectable.IsInteractable()) throw new InvalidOperationException("Control disabled");
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
                if (command.action == "input")
                {
                    var input = target.GetComponent<TMP_InputField>();
                    input.text = command.text;
                    input.onEndEdit.Invoke(command.text);
                    input.DeactivateInputField();
                }
            }
        }
        catch (Exception exception) { error = exception.ToString(); }
        yield return new WaitForSecondsRealtime(0.5f);
        yield return new WaitForEndOfFrame();
        var snapshot = new
        {
            command.id, error, width = Screen.width, height = Screen.height,
            connected = Mirror.NetworkClient.isConnected,
            authenticated = Mirror.NetworkClient.connection != null && Mirror.NetworkClient.connection.isAuthenticated,
            accountAuthenticated = Game.GameController.Available && Game.GameController.Instance.State.IsAuthenticated,
            serverActive = Mirror.NetworkServer.active,
            connectionType = Mirror.NetworkClient.connection == null ? null : Mirror.NetworkClient.connection.GetType().Name,
            controls = FindObjectsOfType<Selectable>().Where(c => c.gameObject.activeInHierarchy).Select(c => new { path = PathOf(c.transform), type = c.GetType().Name, enabled = c.IsInteractable(), text = c is TMP_InputField ? "[input]" : string.Join(" ", c.GetComponentsInChildren<TMP_Text>().Select(t => t.text)) }).ToArray(),
            labels = FindObjectsOfType<TMP_Text>().Where(t => t.gameObject.activeInHierarchy && t.GetComponentInParent<TMP_InputField>() == null).Select(t => new { path = PathOf(t.transform), text = t.text }).ToArray(),
            panels = FindObjectsOfType<Framework.UI.BasePanel>().Select(p => p.name).ToArray(),
            game = FindObjectOfType<Game.UI.GamePanel>() == null ? null : (object)FindObjectOfType<Game.UI.GamePanel>().Info
        };
        File.WriteAllText(Path.Combine(_directory, "state-" + command.id + ".json"), System.Text.Json.JsonSerializer.Serialize(snapshot));
        ScreenCapture.CaptureScreenshot(Path.Combine(_directory, "screen-" + command.id + ".png"));
        _busy = false;
    }

    private static string PathOf(Transform target)
    {
        return target.parent == null ? target.name : PathOf(target.parent) + "/" + target.name;
    }
}
