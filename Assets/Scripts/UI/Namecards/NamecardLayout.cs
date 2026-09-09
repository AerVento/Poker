using UnityEngine;

namespace Game.UI
{
    /// <summary>Shared artwork and UI geometry, measured on the 2048 x 768 master.</summary>
    public static class NamecardLayout
    {
        public const int SourceWidth = 2048;
        public const int SourceHeight = 768;
        public const int MaxTextureSize = 1024;
        public const int CompactWidth = 1024;
        public const int CompactHeight = 768;
        public const int CompactSourceMaxWidth = 2048;
        public const int CompactSourceMaxHeight = 1536;
        public const float CompactAspect = CompactWidth / (float)CompactHeight;
        public static Vector2 CompactSafeMin => new Vector2(ContentInset / (float)CompactWidth, ContentInset / (float)CompactHeight);
        public const int FrameInset = 64;
        public const int ContentInset = 96;
        public const float Aspect = SourceWidth / (float)SourceHeight;
        public static Vector2 SafeMin => new Vector2(ContentInset / (float)SourceWidth, ContentInset / (float)SourceHeight);
        public static Vector2 SafeMax => Vector2.one - SafeMin;

        public static void FitSafeArea(RectTransform area)
        {
            area.anchorMin = SafeMin;
            area.anchorMax = SafeMax;
            area.offsetMin = Vector2.zero;
            area.offsetMax = Vector2.zero;
        }
    }
}
