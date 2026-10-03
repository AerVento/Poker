using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Content;
using Game.Editor;
using Game.Network;
using Game.SO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Game.UI.Editor
{
    public static class RemoteArtworkBuild
    {
        public const string Root = "Assets/RemoteContent";
        public const string Output = "Build/RemoteContent/Content/v1";

        [MenuItem("Poker/Remote Artwork/Prepare Catalogs and Groups")]
        public static void Prepare()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.BuildRemoteCatalog = true;
            // Pages read only the remote catalog named by content-version.txt (RemoteAssets); the built-in one is never used.
            settings.DisableCatalogUpdateOnStartup = true;
            settings.CatalogRequestsTimeout = 10;
            settings.MaxConcurrentWebRequests = 3;
            settings.OverridePlayerVersion = "v1";
            // Content is published on its own; building a page must not silently rebuild the content folder.
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            settings.profileSettings.SetValue(settings.activeProfileId, AddressableAssetSettings.kRemoteBuildPath, Output + "/[BuildTarget]");
            settings.profileSettings.SetValue(settings.activeProfileId, AddressableAssetSettings.kRemoteLoadPath, RemoteAssets.BuildOrigin + "/[BuildTarget]");
            settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);

            // Every namecard, card face and gift in Configs/Items gets its own bundle; the configuration names the address.
            // Group names keep the ID without the item prefix, as before items were unified.
            var definitions = CatalogConfiguration.Load();
            foreach (var card in definitions.Namecards.Where(card => !card.IsDefault))
                Register(settings, "Namecard_" + card.ItemId.Substring(NamecardDefinition.Prefix.Length),
                    RequireSource<NamecardArtworkSO>(card.Artwork, card.ItemId), card.Artwork);
            foreach (var face in definitions.CardFaces.Where(face => !face.IsDefault))
                Register(settings, "CardFace_" + face.ItemId.Substring(CardFaceDefinition.Prefix.Length),
                    RequireSource<PokerCardsSO>(face.Cards, face.ItemId), face.Cards);
            foreach (var gift in definitions.Gifts)
            {
                var path = RequireSource<GiftEffectSO>(gift.Effect, gift.ItemId);
                if (!AssetDatabase.LoadAssetAtPath<GiftEffectSO>(path).HasScript)
                    throw new InvalidOperationException("Gift " + gift.ItemId + " needs a Lua entry module: " + path);
                Register(settings, "Gift_" + gift.ItemId, path, gift.Effect);
            }
            // All item icons in one shared bundle, all card face previews in another; the classic preview stays local.
            Register(settings, "Icons_items", BuildSpriteSet(ItemArt.ItemIconsAddress, ItemIconRoot, _ => true), ItemArt.ItemIconsAddress);
            Register(settings, "Previews_cardfaces", BuildSpriteSet(ItemArt.CardFacePreviewsAddress, CardFacePreviewBuilder.DirectoryPath,
                name => name != definitions.FindCardFace(CardFaceDefinition.DefaultItemId)?.Preview), ItemArt.CardFacePreviewsAddress);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("Remote content prepared: " + (definitions.Namecards.Length - 1) + " namecards, " + (definitions.CardFaces.Length - 1) +
                " card faces, " + definitions.Gifts.Length + " gifts, item icons and card face previews.");
        }

        public const string ItemIconRoot = "Assets/Sprites/Items";

        private static string RequireSource<T>(string address, string id) where T : UnityEngine.Object
        {
            var path = RemoteAssets.SourcePath(address);
            if (AssetDatabase.LoadAssetAtPath<T>(path) == null)
                throw new InvalidOperationException("Item " + id + " has no " + typeof(T).Name + " at " + path);
            return path;
        }

        // Rebuilds a sprite set from the images in a folder; each sprite is named by its file name.
        private static string BuildSpriteSet(string address, string folder, Func<string, bool> include)
        {
            var path = RemoteAssets.SourcePath(address);
            Folder(path.Substring(0, path.LastIndexOf('/')));
            var set = AssetDatabase.LoadAssetAtPath<RemoteSpriteSetSO>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<RemoteSpriteSetSO>();
                AssetDatabase.CreateAsset(set, path);
            }
            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            // One image, one sprite: a sprite sheet would silently contribute only its first slice.
            var sheet = paths.FirstOrDefault(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<Sprite>().Count() > 1);
            if (sheet != null) throw new InvalidOperationException("Use Sprite Mode Single for " + sheet);
            var sprites = paths.Select(p => (Name: Path.GetFileNameWithoutExtension(p), Sprite: AssetDatabase.LoadAssetAtPath<Sprite>(p)))
                .Where(entry => entry.Sprite != null && include(entry.Name)).OrderBy(entry => entry.Name, StringComparer.Ordinal).ToArray();
            var data = new SerializedObject(set);
            var list = data.FindProperty("_sprites");
            list.arraySize = sprites.Length;
            for (var i = 0; i < sprites.Length; i++)
            {
                list.GetArrayElementAtIndex(i).FindPropertyRelative("_name").stringValue = sprites[i].Name;
                list.GetArrayElementAtIndex(i).FindPropertyRelative("_sprite").objectReferenceValue = sprites[i].Sprite;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            return path;
        }

        private static void Register(AddressableAssetSettings settings, string name, string path, string key)
        {
            var group = settings.FindGroup(name) ?? settings.CreateGroup(name, false, false, false, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            schema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            schema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.AppendHash;
            schema.Timeout = 15;
            schema.RetryCount = 1;
            schema.UseAssetBundleCache = true;
            schema.UseAssetBundleCrc = true;
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
            entry.address = key;
            EditorUtility.SetDirty(schema);
        }

        [MenuItem("Poker/Remote Artwork/Optimize Texture Imports")]
        public static void OptimizeTextures()
        {
            // Keep the source art. Change only the imported game textures, preserving all sprite IDs.
            var paths = AssetDatabase.GetDependencies(RemoteRoots(), true)
                .Where(p => p.StartsWith("Assets/Sprites/Namecards/") || p.StartsWith("Assets/Art/Gifts/") ||
                    p.StartsWith("Assets/Sprites/CardFaces/") && !p.StartsWith(CardFacePreviewBuilder.DirectoryPath + "/"))
                .Distinct().ToArray();
            foreach (var path in paths)
            {
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                importer.maxTextureSize = 1024;
                importer.isReadable = false;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.compressionQuality = 100;
                foreach (var platform in new[] { "Standalone", "WebGL" })
                {
                    var options = importer.GetPlatformTextureSettings(platform);
                    options.overridden = true;
                    options.maxTextureSize = 1024;
                    options.format = TextureImporterFormat.Automatic;
                    options.textureCompression = TextureImporterCompression.CompressedHQ;
                    options.compressionQuality = 100;
                    importer.SetPlatformTextureSettings(options);
                }
                importer.SaveAndReimport();
            }
            Debug.Log("Remote artwork texture imports optimized: " + paths.Length);
        }

        public static string[] RemoteRoots() => AssetDatabase.FindAssets("t:ScriptableObject", new[] { Root })
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();

        [MenuItem("Poker/Remote Artwork/Validate First Build Dependencies")]
        public static void Validate()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var definitions = CatalogConfiguration.Load();
            var roots = RemoteRoots();
            foreach (var root in roots)
            {
                var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(root));
                if (entry == null || entry.parentGroup.entries.Count != 1 ||
                    entry.parentGroup.GetSchema<BundledAssetGroupSchema>().BundleMode != BundledAssetGroupSchema.BundlePackingMode.PackTogether ||
                    !entry.parentGroup.GetSchema<BundledAssetGroupSchema>().LoadPath.GetValue(settings).StartsWith(RemoteAssets.BuildOrigin + "/"))
                    throw new InvalidOperationException("Each remote root requires its own bundle group: " + root);
            }
            var addresses = definitions.Namecards.Where(c => !c.IsDefault).Select(c => c.Artwork)
                .Concat(definitions.CardFaces.Where(f => !f.IsDefault).Select(f => f.Cards))
                .Concat(definitions.Gifts.Select(g => g.Effect))
                .Concat(new[] { ItemArt.ItemIconsAddress, ItemArt.CardFacePreviewsAddress });
            foreach (var address in addresses)
            {
                var path = RemoteAssets.SourcePath(address);
                if (!roots.Contains(path) || settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path))?.address != address)
                    throw new InvalidOperationException("Configured address is not prepared: " + address + ". Run Prepare Catalogs and Groups.");
            }
            var icons = AssetDatabase.LoadAssetAtPath<RemoteSpriteSetSO>(RemoteAssets.SourcePath(ItemArt.ItemIconsAddress));
            foreach (var item in definitions.Items)
                if (icons.Find(item.Icon) == null) throw new InvalidOperationException("Item " + item.Id + " icon " + item.Icon + " is not in " + ItemIconRoot);
            var previews = AssetDatabase.LoadAssetAtPath<RemoteSpriteSetSO>(RemoteAssets.SourcePath(ItemArt.CardFacePreviewsAddress));
            foreach (var face in definitions.CardFaces.Where(f => !f.IsDefault))
                if (previews.Find(face.Preview) == null)
                    throw new InvalidOperationException("Card face " + face.ItemId + " preview is missing; run Poker/Card Faces/Generate Previews.");
            // UI theme sprites and the contract icons (also leaderboard headings; contracts ship with the page) may be
            // copied into bundles; all other item art must never be in the page.
            var remote = new HashSet<string>(AssetDatabase.GetDependencies(roots, true).Where(p => !p.EndsWith(".cs", StringComparison.Ordinal) &&
                !p.StartsWith(GiftLuaRoot, StringComparison.Ordinal) && !p.StartsWith("Assets/Sprites/UITheme/", StringComparison.Ordinal) &&
                !p.StartsWith(ItemIconRoot + "/Contracts/", StringComparison.Ordinal)));
            var localRoots = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") &&
                p.Contains("/Resources/") && !AssetDatabase.IsValidFolder(p)).Concat(
                EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)).ToArray();
            var leaked = AssetDatabase.GetDependencies(localRoots, true).Where(remote.Contains).Distinct().ToArray();
            if (leaked.Length > 0) throw new InvalidOperationException("Remote art still referenced by the page:\n" + string.Join("\n", leaked));
            var giftAssets = new Dictionary<string, string>();
            foreach (var gift in definitions.Gifts)
            {
                var root = RemoteAssets.SourcePath(gift.Effect);
                var effect = AssetDatabase.LoadAssetAtPath<GiftEffectSO>(root);
                if (effect == null || !effect.HasScript) throw new InvalidOperationException("Gift requires a scripted remote effect: " + gift.ItemId);
                var dependencies = AssetDatabase.GetDependencies(root, true);
                if (dependencies.Any(p => p != root && p.StartsWith(Root + "/", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Gift must not depend on another remote content root: " + gift.ItemId);
                ValidateGiftScript(gift.ItemId, effect);
                // Gift bundles may share Lua module text, which is copied into each bundle, but never artwork or audio.
                foreach (var dependency in dependencies.Where(p => p != root && !p.EndsWith(".cs", StringComparison.Ordinal) &&
                    !p.StartsWith(GiftLuaRoot + "Shared/", StringComparison.Ordinal)))
                {
                    if (giftAssets.TryGetValue(dependency, out var other) && other != gift.ItemId)
                        throw new InvalidOperationException("Gifts " + other + " and " + gift.ItemId + " share " + dependency);
                    giftAssets[dependency] = gift.ItemId;
                }
            }
            var cards = ShopLocalArtSO.Instance != null ? ShopLocalArtSO.Instance.ClassicCards : null;
            if (cards == null || cards.Back == null || Poker.GetAll().Any(p => cards.GetSprite(p) == null))
                throw new InvalidOperationException("ShopLocalArt must contain the complete classic deck.");
            Debug.Log("Remote content validation passed: " + roots.Length + " isolated groups; the page references no remote art.");
        }

        private const string GiftLuaRoot = "Assets/Scripts/UI/Gifts/Lua/";

        // Every declared resource name exists once; modules live in the gift Lua folder; the module loads and validates.
        private static void ValidateGiftScript(string id, GiftEffectSO effect)
        {
            var names = new HashSet<string>();
            foreach (var resource in effect.Resources)
            {
                if (resource == null || string.IsNullOrWhiteSpace(resource.Name) || resource.Asset == null)
                    throw new InvalidOperationException("Gift " + id + " has an empty resource entry.");
                if (!names.Add(resource.Name)) throw new InvalidOperationException("Gift " + id + " repeats resource " + resource.Name);
            }
            if (!AssetDatabase.GetAssetPath(effect.Script).StartsWith(GiftLuaRoot, StringComparison.Ordinal))
                throw new InvalidOperationException("Gift " + id + " entry module must be in " + GiftLuaRoot);
            foreach (var module in effect.Modules)
                if (module == null || !AssetDatabase.GetAssetPath(module).StartsWith(GiftLuaRoot + "Shared/", StringComparison.Ordinal))
                    throw new InvalidOperationException("Gift " + id + " shared modules must be in " + GiftLuaRoot + "Shared/");
            effect.ReloadFromDisk();
            if (!effect.TryDefine(out var script)) throw new InvalidOperationException("Gift " + id + " script: " + effect.ScriptError);
            foreach (var name in script.Resources)
                if (!names.Contains(name)) throw new InvalidOperationException("Gift " + id + " declares missing resource " + name);
        }

        [MenuItem("Poker/Remote Artwork/Build for Active Platform")]
        public static void Build()
        {
            Validate();
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error)) throw new InvalidOperationException(result.Error);
            var folder = Output + "/" + EditorUserBuildSettings.activeBuildTarget;
            var catalog = "catalog_" + AddressableAssetSettingsDefaultObject.Settings.PlayerBuildVersion + ".json";
            var referenced = new HashSet<string>(ReferencedBundles(File.ReadAllText(Path.Combine(folder, catalog))));
            // The folder mirrors the site: only the bundles of the current catalog remain.
            foreach (var bundle in Directory.GetFiles(folder, "*.bundle").Where(p => !referenced.Contains(Path.GetFileName(p))))
                File.Delete(bundle);
            var format = ContentFormatVersion.Compute();
            var version = new RemoteContentVersion(DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'"), format, catalog);
            File.WriteAllText(Path.Combine(folder, RemoteContentVersion.FileName), version.ToString());
            var report = new List<string> { "Platform: " + EditorUserBuildSettings.activeBuildTarget, "Unity: " + Application.unityVersion,
                "Content: " + version.Version, "Format: " + format };
            foreach (var file in Directory.GetFiles(folder).Where(p => Path.GetFileName(p) != "files.txt").OrderBy(p => p))
                report.Add(new FileInfo(file).Length + "\t" + Path.GetFileName(file));
            File.WriteAllLines(folder + "/files.txt", report);
            Debug.Log("Remote content " + version.Version + " built: " + Path.GetFullPath(folder));
            var pageFormat = File.Exists(ClientBuildStamp.LastPageFormatPath) ? File.ReadAllText(ClientBuildStamp.LastPageFormatPath).Trim() : null;
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL && pageFormat != format)
                Debug.LogWarning("Content format " + format + " differs from the last WebGL page build (" + (pageFormat ?? "none") +
                    "). Deploy this content together with a newly built page; current pages will not adopt it.");
        }

        private static IEnumerable<string> ReferencedBundles(string catalogJson)
        {
            var locator = JsonUtility.FromJson<ContentCatalogData>(catalogJson).CreateLocator();
            return locator.Keys.SelectMany(key => locator.Locate(key, null, out var locations) ? locations : Enumerable.Empty<IResourceLocation>())
                .Select(location => location.InternalId).Where(id => id.EndsWith(".bundle", StringComparison.Ordinal))
                .Select(id => id.Substring(id.LastIndexOf('/') + 1)).Distinct();
        }

        [MenuItem("Poker/Remote Artwork/Build WebGL Deployment Example")]
        public static void BuildWebGLPlayer()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new InvalidOperationException("Switch the active build target to WebGL first.");
            Validate();
            const string destination = "Build/RemoteContent/WebGLGame";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = destination,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("WebGL build failed: " + report.summary.result);
            // This example sits next to Content/ rather than containing it.
            var configPath = destination + "/Configs/Network/client-network.json";
            var configuration = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(
                File.ReadAllText(configPath));
            using (var value = System.Text.Json.JsonDocument.Parse("\"../Content/v1\""))
                configuration["resource-base-url"] = value.RootElement.Clone();
            File.WriteAllText(configPath, System.Text.Json.JsonSerializer.Serialize(configuration));
            File.WriteAllText("Build/RemoteContent/WebGLBuild.txt", "Bytes: " + report.summary.totalSize +
                "\nErrors: " + report.summary.totalErrors + "\nWarnings: " + report.summary.totalWarnings);
            Debug.Log("WebGL deployment example built: " + report.summary.totalSize + " bytes.");
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = path.Substring(0, path.LastIndexOf('/'));
            Folder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
