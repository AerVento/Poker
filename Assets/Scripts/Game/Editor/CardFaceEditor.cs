using System;
using System.Linq;
using Game.Content;
using Game.Network;
using Game.SO;
using Game.UI;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class CardFaceEditor
    {
        [MenuItem("Poker/Card Faces/Validate Catalog")]
        public static void Validate()
        {
            var local = ShopLocalArtSO.Instance;
            var view = local != null ? local.CardFaceView : null;
            if (view == null || view.Image == null || view.GetComponentsInChildren<PokerElement>(true).Length != 0 ||
                view.GetComponentsInChildren<Mirror.NetworkIdentity>(true).Length != 0)
                throw new InvalidOperationException("ShopLocalArt needs a card face view with an image and no gameplay components.");
            Check("default", local.ClassicCards);
            if (local.ClassicPreview == null) throw new InvalidOperationException("ShopLocalArt needs the classic preview.");
            var faces = ShopConfiguration.Load().CardFaces;
            foreach (var face in faces.Where(face => !face.IsDefault))
                Check(face.Id, AssetDatabase.LoadAssetAtPath<PokerCardsSO>(RemoteAssets.SourcePath(face.Cards)));
            Debug.Log("Card faces validated: " + faces.Length);
        }

        private static void Check(string id, PokerCardsSO cards)
        {
            if (cards == null || cards.Back == null || Poker.GetAll().Any(card => cards.GetSprite(card) == null))
                throw new InvalidOperationException("Card face " + id + " needs all 52 faces and a back.");
        }
    }
}
