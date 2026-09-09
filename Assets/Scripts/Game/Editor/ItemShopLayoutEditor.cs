using Game.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    public static class ItemShopLayoutEditor
    {
        [MenuItem("Poker/UI/Update Item Detail Scroll")]
        public static void UpdateDetailScroll()
        {
            const string path = "Assets/Prefabs/UI/Panels/ShopPanel/ShopPanel.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponentInChildren<ItemBrowserView>(true);
                var serialized = new SerializedObject(view);
                if (serialized.FindProperty("_detailScroll").objectReferenceValue != null) return;
                var description = (TextMeshProUGUI)serialized.FindProperty("_detailDescription").objectReferenceValue;
                var original = description.rectTransform;
                var viewport = new GameObject("ItemDescriptionViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
                var rect = (RectTransform)viewport.transform;
                rect.SetParent(original.parent, false);
                rect.SetSiblingIndex(original.GetSiblingIndex());
                rect.anchorMin = original.anchorMin;
                rect.anchorMax = original.anchorMax;
                rect.pivot = original.pivot;
                rect.anchoredPosition = original.anchoredPosition;
                rect.sizeDelta = original.sizeDelta;
                viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0);
                original.SetParent(rect, false);
                original.anchorMin = new Vector2(0, 1);
                original.anchorMax = new Vector2(1, 1);
                original.pivot = new Vector2(0, 1);
                original.anchoredPosition = Vector2.zero;
                original.sizeDelta = new Vector2(-14, 0);
                description.enableAutoSizing = false;
                description.fontSize = 19;
                description.overflowMode = TextOverflowModes.Overflow;
                description.raycastTarget = false;
                original.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var scroll = viewport.GetComponent<ScrollRect>();
                scroll.viewport = rect;
                scroll.content = original;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 28;

                var barObject = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
                var barRect = (RectTransform)barObject.transform;
                barRect.SetParent(rect, false);
                barRect.anchorMin = new Vector2(1, 0);
                barRect.anchorMax = new Vector2(1, 1);
                barRect.pivot = new Vector2(1, 0.5f);
                barRect.sizeDelta = new Vector2(5, 0);
                barObject.GetComponent<Image>().color = new Color(0.13f, 0.23f, 0.18f, 0.7f);
                var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
                var handleRect = (RectTransform)handle.transform;
                handleRect.SetParent(barRect, false);
                handleRect.anchorMin = Vector2.zero;
                handleRect.anchorMax = Vector2.one;
                handleRect.sizeDelta = Vector2.zero;
                handle.GetComponent<Image>().color = new Color(0.76f, 0.65f, 0.40f, 0.8f);
                var bar = barObject.GetComponent<Scrollbar>();
                bar.handleRect = handleRect;
                bar.targetGraphic = handle.GetComponent<Image>();
                bar.direction = Scrollbar.Direction.BottomToTop;
                scroll.verticalScrollbar = bar;
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                serialized.FindProperty("_detailScroll").objectReferenceValue = scroll;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
