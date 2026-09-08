using System;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    // Each label owns its outline material; shared font assets and other players stay untouched.
    internal sealed class InfernalNameStyle : IDisposable
    {
        private TMP_Text _label;
        private Color _originalColor;
        private bool _originalGradient;
        private bool _originalOverrideColors;
        private Material _originalMaterial;
        private Material _material;

        public void Present(TMP_Text label, ulong serviceYears)
        {
            if (_label != label || !InfernalNamecard.IsForced(serviceYears))
                Dispose();
            if (label == null || !InfernalNamecard.IsForced(serviceYears))
                return;
            if (_label == null)
            {
                _label = label;
                _originalColor = label.color;
                _originalGradient = label.enableVertexGradient;
                _originalOverrideColors = label.overrideColorTags;
                _originalMaterial = label.fontSharedMaterial;
                if (_originalMaterial != null)
                {
                    _material = new Material(_originalMaterial) { name = _originalMaterial.name + " (Infernal Name)" };
                    _material.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                    _material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
                    _label.fontSharedMaterial = _material;
                    _label.UpdateMeshPadding();
                }
            }
            _label.enableVertexGradient = false;
            _label.overrideColorTags = true;
            _label.color = new Color32(255, 24, 24, 255);
        }

        public void Dispose()
        {
            if (_label != null)
            {
                _label.color = _originalColor;
                _label.enableVertexGradient = _originalGradient;
                _label.overrideColorTags = _originalOverrideColors;
                _label.fontSharedMaterial = _originalMaterial;
                _label.UpdateMeshPadding();
            }
            if (_material != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_material);
                else UnityEngine.Object.DestroyImmediate(_material);
            }
            _label = null;
            _material = null;
            _originalMaterial = null;
        }
    }
}