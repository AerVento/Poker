using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Game.Network.Accounts;
using Game.SO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    public sealed class NamecardCatalogExport : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;
        public void OnPreprocessBuild(BuildReport report) => Export();

        [MenuItem("Poker/Shop/Export Server Catalog")]
        public static void Export()
        {
            var source = Resources.Load<NamecardCatalogSO>("SO/Shop/NamecardCatalog");
            if (source == null)
                throw new BuildFailedException("Missing NamecardCatalog SO.");
            var products = source.Products;
            if (products == null || products.Any(p => p == null || p.Artwork == null ||
                string.IsNullOrWhiteSpace(p.DisplayName)) ||
                products.Count(p => p.IsDefault) != 1 || !products[0].IsDefault ||
                products[0].Id != "default")
                throw new BuildFailedException("Invalid namecard catalog: check default, product references, names and artwork.");
            var catalog = new NamecardServerCatalog
            {
                Products = products.Select(p => new NamecardOffer { Id = p.Id, Price = p.Price, Listed = p.IsListed && p.CanPurchase }).ToArray()
            };
            catalog.Validate();
            const string path = "Assets/Resources/SO/Shop/NamecardServerCatalog.json";
            var json = JsonSerializer.Serialize(catalog.Products, new JsonSerializerOptions { WriteIndented = true });
            if (!File.Exists(path) || File.ReadAllText(path) != json)
            {
                File.WriteAllText(path, json);
                AssetDatabase.ImportAsset(path);
            }
            Debug.Log("Namecard server catalog exported: " + catalog.Products.Length + " products, revision " + catalog.Revision);
        }
    }
}
