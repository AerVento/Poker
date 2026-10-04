using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Gifts
{
    /// <summary>A short-lived frozen screen beneath a gift, with a radial grayscale distortion.</summary>
    internal sealed class GiftScreenEffect : MonoBehaviour
    {
        private RawImage _image;
        private Material _material;
        private RenderTexture _capture;
        private Canvas _giftCanvas;
        private bool _canvasWasEnabled;
        private bool _capturing;

        public void Configure(Material source, Canvas giftCanvas)
        {
            _image = GetComponent<RawImage>();
            _image.raycastTarget = false;
            _image.enabled = false;
            _material = new Material(source);
            _image.material = _material;
            _giftCanvas = giftCanvas;
            _material.SetFloat("_Radius", 0f);
            _material.SetFloat("_Strength", 1f);
            if (Application.isPlaying) Capture().Forget();
        }

        public void SetParameter(string parameter, float value)
        {
            if (parameter != "radius" && parameter != "strength")
                throw new ArgumentException("Unknown screen effect parameter: " + parameter);
            _material.SetFloat(parameter == "radius" ? "_Radius" : "_Strength", Mathf.Clamp01(value));
        }

        private async UniTaskVoid Capture()
        {
            try
            {
                // Capture the table without this gift. No CPU readback and no change to game time.
                _canvasWasEnabled = _giftCanvas.enabled;
                _capturing = true;
                _giftCanvas.enabled = false;
                await UniTask.WaitForEndOfFrame(this, this.GetCancellationTokenOnDestroy());
                if (this == null) return;
                _capture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
                _capture.name = "Gift frozen screen";
                _capture.Create();
                ScreenCapture.CaptureScreenshotIntoRenderTexture(_capture);
                _image.texture = _capture;
                _material.SetFloat("_Aspect", (float)Screen.width / Screen.height);
                _image.enabled = true;
            }
            catch (OperationCanceledException) { }
            finally { RestoreCanvas(); }
        }

        private void RestoreCanvas()
        {
            if (!_capturing) return;
            if (_giftCanvas != null) _giftCanvas.enabled = _canvasWasEnabled;
            _capturing = false;
        }

        private void OnDestroy()
        {
            RestoreCanvas();
            if (_capture != null) { _capture.Release(); Destroy(_capture); }
            if (_material != null)
            {
                if (Application.isPlaying) Destroy(_material);
                else DestroyImmediate(_material);
            }
        }
    }
}
