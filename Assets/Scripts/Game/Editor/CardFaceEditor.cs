using System;
using System.Linq;
using Game.SO;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class CardFaceEditor
    {
        private const string Folder = "Assets/Resources/SO/CardFaces";
        private const string VisualPath = "Assets/Prefabs/UI/Panels/GamePanel/ClassicCardFace.prefab";

        [MenuItem("Poker/Card Faces/Install Classic and Shop Tab")]
        public static void Install()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources/SO", "CardFaces");
            var visual = AssetDatabase.LoadAssetAtPath<CardFaceView>(VisualPath);
            if (visual == null)
            {
                var root = new GameObject("ClassicCardFace", typeof(RectTransform), typeof(Image), typeof(CardFaceView));
                try
                {
                    ((RectTransform)root.transform).sizeDelta = new Vector2(204, 288);
                    var image = root.GetComponent<Image>();
                    image.raycastTarget = false;
                    image.preserveAspect = true;
                    var cards = AssetDatabase.LoadAssetAtPath<PokerCardsSO>("Assets/Resources/SO/PokerCardsSO.asset");
                    image.sprite = cards.GetSprite(new Poker(PokerSuit.Spade, PokerRank.Ace));
                    var so = new SerializedObject(root.GetComponent<CardFaceView>());
                    so.FindProperty("_image").objectReferenceValue = image;
                    so.FindProperty("_cards").objectReferenceValue = cards;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    visual = PrefabUtility.SaveAsPrefabAsset(root, VisualPath).GetComponent<CardFaceView>();
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            var product = AssetDatabase.LoadAssetAtPath<CardFaceProductSO>(Folder + "/Classic.asset");
            if (product == null)
            {
                product = ScriptableObject.CreateInstance<CardFaceProductSO>();
                var so = new SerializedObject(product);
                so.FindProperty("_prefab").objectReferenceValue = visual;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(product, Folder + "/Classic.asset");
            }
            var catalog = AssetDatabase.LoadAssetAtPath<CardFaceCatalogSO>(Folder + "/CardFaceCatalog.asset");
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CardFaceCatalogSO>();
                var so = new SerializedObject(catalog);
                var products = so.FindProperty("_products");
                products.arraySize = 1;
                products.GetArrayElementAtIndex(0).objectReferenceValue = product;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(catalog, Folder + "/CardFaceCatalog.asset");
            }
            const string path = "Assets/Prefabs/UI/Panels/ShopPanel/ShopPanel.prefab";
            var panelRoot = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var panel = panelRoot.GetComponent<ShopPanel>();
                var so = new SerializedObject(panel);
                if (so.FindProperty("_cardFacesTab").objectReferenceValue == null)
                {
                    var original = (Button)so.FindProperty("_namecardsTab").objectReferenceValue;
                    var tab = UnityEngine.Object.Instantiate(original, original.transform.parent);
                    tab.name = "CardFacesTab";
                    ((RectTransform)tab.transform).anchoredPosition = new Vector2(-90, 320);
                    var label = tab.GetComponentInChildren<TextMeshProUGUI>();
                    label.text = "牌面商店";
                    so.FindProperty("_cardFacesTab").objectReferenceValue = tab;
                    so.FindProperty("_cardFacesTabLabel").objectReferenceValue = label;
                    var root = new GameObject("CardFacesView", typeof(RectTransform), typeof(CardFaceBrowserView));
                    var rect = (RectTransform)root.transform;
                    rect.SetParent(original.transform.parent, false);
                    rect.anchoredPosition = new Vector2(0, -30);
                    rect.sizeDelta = new Vector2(1460, 620);
                    var view = root.GetComponent<CardFaceBrowserView>();
                    var viewData = new SerializedObject(view);
                    viewData.FindProperty("_labelTemplate").objectReferenceValue = so.FindProperty("_subtitle").objectReferenceValue;
                    viewData.ApplyModifiedPropertiesWithoutUndo();
                    so.FindProperty("_cardFacesView").objectReferenceValue = view;
                    root.SetActive(false);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(panelRoot, path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(panelRoot); }
            UpdateShopTheme();
            AssetDatabase.SaveAssets();
            Validate();
        }

        [MenuItem("Poker/Card Faces/Update Shop Theme")]
        public static void UpdateShopTheme()
        {
            const string cardPath = "Assets/Prefabs/UI/Panels/ShopPanel/CardFaceShopCard.prefab";
            var cardPrefab = AssetDatabase.LoadAssetAtPath<CardFaceShopCard>(cardPath);
            if (cardPrefab == null)
            {
                var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/Panels/ShopPanel/ShopItemCard.prefab");
                try
                {
                    root.SetActive(false);
                    root.name = "CardFaceShopCard";
                    var old = root.GetComponent<ShopItemCard>();
                    var source = new SerializedObject(old);
                    var component = root.AddComponent<CardFaceShopCard>();
                    var target = new SerializedObject(component);
                    foreach (var field in new[] { "_border", "_name", "_description", "_price", "_status", "_action", "_actionLabel" })
                        target.FindProperty(field).objectReferenceValue = source.FindProperty(field).objectReferenceValue;
                    var artwork = (Image)source.FindProperty("_artwork").objectReferenceValue;
                    var preview = (RectTransform)artwork.transform.parent;
                    artwork.gameObject.SetActive(false);
                    preview.GetComponent<Image>().color = new Color(0.085f, 0.16f, 0.145f, 1);
                    var status = (TextMeshProUGUI)source.FindProperty("_status").objectReferenceValue;
                    status.rectTransform.SetParent(preview, false);
                    status.rectTransform.anchorMin = status.rectTransform.anchorMax = status.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    status.rectTransform.anchoredPosition = new Vector2(230, 100);
                    status.rectTransform.sizeDelta = new Vector2(150, 28);
                    status.alignment = TextAlignmentOptions.Right;
                    status.fontSize = 18;
                    target.FindProperty("_preview").objectReferenceValue = preview;
                    target.ApplyModifiedPropertiesWithoutUndo();
                    UnityEngine.Object.DestroyImmediate(old);
                    root.SetActive(true);
                    cardPrefab = PrefabUtility.SaveAsPrefabAsset(root, cardPath).GetComponent<CardFaceShopCard>();
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var skinRoot = PrefabUtility.LoadPrefabContents(cardPath);
            try
            {
                foreach (var text in skinRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
                    if (text.name == "PlayerName" || text.name == "PlayerChips") text.gameObject.SetActive(false);
                var artwork = skinRoot.GetComponentsInChildren<Image>(true).First(image => image.name == "Artwork");
                artwork.gameObject.SetActive(true);
                artwork.sprite = skinRoot.GetComponent<Image>().sprite;
                artwork.type = Image.Type.Sliced;
                artwork.preserveAspect = false;
                artwork.color = new Color(0.085f, 0.16f, 0.145f, 1);
                artwork.raycastTarget = false;
                PrefabUtility.SaveAsPrefabAsset(skinRoot, cardPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(skinRoot); }
            const string panelPath = "Assets/Prefabs/UI/Panels/ShopPanel/ShopPanel.prefab";
            var panelRoot = PrefabUtility.LoadPrefabContents(panelPath);
            try
            {
                var panelData = new SerializedObject(panelRoot.GetComponent<ShopPanel>());
                var view = (CardFaceBrowserView)panelData.FindProperty("_cardFacesView").objectReferenceValue;
                var so = new SerializedObject(view);
                so.FindProperty("_cardPrefab").objectReferenceValue = cardPrefab;
                so.FindProperty("_navigationTemplate").objectReferenceValue = panelData.FindProperty("_previous").objectReferenceValue;
                so.ApplyModifiedPropertiesWithoutUndo();
                view.transform.localScale = new Vector3(0.91f, 0.91f, 0.91f);
                PrefabUtility.SaveAsPrefabAsset(panelRoot, panelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(panelRoot); }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Poker/Card Faces/Validate Catalog")]
        public static void Validate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CardFaceCatalogSO>(Folder + "/CardFaceCatalog.asset");
            if (catalog == null || catalog.Default == null || catalog.Products.Any(p => p == null) ||
                catalog.Products.Select(p => p.Id).Distinct().Count() != catalog.Products.Count)
                throw new InvalidOperationException("Card faces require unique IDs and a default product.");
            foreach (var product in catalog.Products)
            {
                if (!Network.Accounts.CardFaceServerCatalog.IsValidId(product.Id) || product.Prefab == null ||
                    product.Prefab.Image == null || product.Prefab.Cards == null || product.Prefab.Cards.Back == null ||
                    Poker.GetAll().Any(card => product.Prefab.Cards.GetSprite(card) == null) ||
                    product.Prefab.GetComponentsInChildren<PokerElement>(true).Length != 0 ||
                    product.Prefab.GetComponentsInChildren<Mirror.NetworkIdentity>(true).Length != 0)
                    throw new InvalidOperationException("Incomplete or unsafe card face: " + product.Id);
            }
            Debug.Log("Card face catalog validated: " + catalog.Products.Count);
        }
    }
}
