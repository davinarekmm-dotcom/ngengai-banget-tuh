#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Room0317.Apartment;
using Room0317.Chapter2;
using Room0317.Dialogue;
using Room0317.Inventory;

namespace Room0317.EditorTools
{
    /// <summary>
    /// Editor-only setup tool for Chapter 2: The Cold Corridor.
    /// Configures TextureImporter settings (matching Chapter 1 PPU/filtering)
    /// and generates Chapter 2 Environment and Evidence prefabs without modifying
    /// any gameplay scripts or Chapter 1 assets.
    /// Menu: ROOM 03:17 > Setup Chapter 2 Assets & Prefabs
    /// </summary>
    public static class Chapter2AssetSetupBuilder
    {
        private const string EnvArtFolder = "Assets/Environment/Chapter2";
        private const string ObjArtFolder = "Assets/Objects/Chapter2";
        private const string PrefabsRoot = "Assets/_Project/Prefabs/Chapter2";
        private const string EvidencePrefabsFolder = PrefabsRoot + "/Evidence";
        private const string EnvPrefabsFolder = PrefabsRoot + "/Environment";

        private const float RoomPPU = 104.5f;
        private const float ObjectPPU = 100.0f;

        [MenuItem("ROOM 03:17/Setup Chapter 2 Assets & Prefabs")]
        public static void SetupChapter2AssetsAndPrefabs()
        {
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder(PrefabsRoot);
            EnsureFolder(EvidencePrefabsFolder);
            EnsureFolder(EnvPrefabsFolder);

            ConfigureEnvironmentImports();
            ConfigureObjectImports();
            AssetDatabase.Refresh();

            BuildEnvironmentPrefabs();
            BuildEvidencePrefabs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green><b>[ROOM 03:17]</b> Chapter 2 textures configured and prefabs generated in Assets/_Project/Prefabs/Chapter2/.</color>");
        }

        public const string Chapter2ScenePath = "Assets/_Project/Scenes/04_Chapter2_ColdCorridor.unity";

        [MenuItem("ROOM 03:17/Create Chapter 2 Scene (04_Chapter2_ColdCorridor)")]
        public static void CreateChapter2Scene()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ROOM 03:17] Exit Play Mode before creating the Chapter 2 scene.");
                return;
            }

            if (File.Exists(Chapter2ScenePath))
            {
                if (!EditorUtility.DisplayDialog(
                    "Rebuild Chapter 2 Scene?",
                    "Rebuilding 04_Chapter2_ColdCorridor.unity will reset manual collider and object edits in that scene.\n\nProceed?",
                    "Yes, Rebuild", "Cancel"))
                {
                    return;
                }
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            SetupChapter2AssetsAndPrefabs();
            BuildDedicatedChapter2Scene();
        }

        [MenuItem("ROOM 03:17/Test Chapter 2 Only (Setup & Play)")]
        public static void CreateAndPlayChapter2TestScene()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ROOM 03:17] Exit Play Mode before launching Chapter 2.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            bool needsBuild = !File.Exists(Chapter2ScenePath);
            if (!needsBuild)
            {
                var opened = EditorSceneManager.OpenScene(Chapter2ScenePath, OpenSceneMode.Single);
                if (GameObject.Find(Chapter2RoomBuilder.RootObjectName) == null)
                {
                    needsBuild = true;
                }
            }

            if (needsBuild)
            {
                SetupChapter2AssetsAndPrefabs();
                BuildDedicatedChapter2Scene();
            }

            Debug.Log($"<color=cyan><b>[ROOM 03:17]</b> Launching dedicated Chapter 2 scene ({Chapter2ScenePath})...</color>");
            EditorApplication.isPlaying = true;
        }

        private static void BuildDedicatedChapter2Scene()
        {
            const string sourceScene = "Assets/_Project/Scenes/03_Room0317_Game.unity";
            if (!File.Exists(sourceScene))
            {
                Debug.LogError($"[ROOM 03:17] Could not find source scene: {sourceScene}");
                return;
            }

            if (File.Exists(Chapter2ScenePath))
            {
                AssetDatabase.DeleteAsset(Chapter2ScenePath);
            }
            AssetDatabase.CopyAsset(sourceScene, Chapter2ScenePath);
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(Chapter2ScenePath, OpenSceneMode.Single);

            // Remove all Chapter 1 rooms, tile grids, and room-specific point lights
            foreach (var room in Object.FindObjectsByType<ApartmentRoom>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (room != null) Object.DestroyImmediate(room.gameObject);
            }
            foreach (var item in Object.FindObjectsByType<CollectibleItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (item != null) Object.DestroyImmediate(item.gameObject);
            }
            foreach (var pt in Object.FindObjectsByType<InteractablePoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (pt != null) Object.DestroyImmediate(pt.gameObject);
            }
            foreach (var light in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light != null && light.lightType != Light2D.LightType.Global)
                {
                    Object.DestroyImmediate(light.gameObject);
                }
            }
            var grid = GameObject.Find("Grid");
            if (grid != null) Object.DestroyImmediate(grid);

            // Remove any serialized StoryProgressionCoordinator if present
            foreach (var spc in Object.FindObjectsByType<Room0317.Story.StoryProgressionCoordinator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (spc != null && spc.GetComponent<ApartmentManager>() == null)
                {
                    Object.DestroyImmediate(spc.gameObject);
                }
                else if (spc != null)
                {
                    Object.DestroyImmediate(spc);
                }
            }

            // Attach Chapter2Coordinator in standalone mode
            var existing = Object.FindFirstObjectByType<Chapter2Coordinator>();
            GameObject coordGo = existing != null ? existing.gameObject : new GameObject("--- CHAPTER 2 COORDINATOR ---");
            var coord = coordGo.GetComponent<Chapter2Coordinator>() ?? coordGo.AddComponent<Chapter2Coordinator>();

            var so = new SerializedObject(coord);
            var prop = so.FindProperty("startStandaloneOnStart");
            if (prop != null) prop.boolValue = true;

            AssignSprite(so, "corridorWestSprite", $"{EnvArtFolder}/ch2_env_corridor_west.png");
            AssignSprite(so, "corridorEastSprite", $"{EnvArtFolder}/ch2_env_corridor_east.png");
            AssignSprite(so, "roofLandingSprite", $"{EnvArtFolder}/ch2_env_roof_landing.png");
            AssignSprite(so, "umbrellaSprite", $"{ObjArtFolder}/ch2_ev_umbrella_green.png");
            AssignSprite(so, "intercomSprite", $"{ObjArtFolder}/ch2_ev_wall_intercom.png");
            AssignSprite(so, "compactMirrorSprite", $"{ObjArtFolder}/ch2_ev_compact_mirror.png");
            AssignSprite(so, "fleeceJacketSprite", $"{ObjArtFolder}/ch2_ev_fleece_jacket.png");
            AssignSprite(so, "movingBoxSprite", $"{ObjArtFolder}/ch2_ev_moving_box.png");
            AssignSprite(so, "pharmacyReceiptInspectSprite", $"{ObjArtFolder}/ch2_ui_pharmacy_receipt_placeholder.png");
            AssignSprite(so, "intercomLogInspectSprite", $"{ObjArtFolder}/ch2_ui_intercom_log.png");
            AssignSprite(so, "compactNoteInspectSprite", $"{ObjArtFolder}/ch2_ui_compact_note_placeholder.png");

            var fpProp = so.FindProperty("footprintSprites");
            if (fpProp != null)
            {
                var fpSprites = AssetDatabase.LoadAllAssetsAtPath($"{ObjArtFolder}/ch2_ev_footprints_decal.png")
                    .OfType<Sprite>()
                    .OrderBy(s => s.name)
                    .ToArray();
                fpProp.arraySize = fpSprites.Length;
                for (int i = 0; i < fpSprites.Length; i++)
                {
                    fpProp.GetArrayElementAtIndex(i).objectReferenceValue = fpSprites[i];
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            // Bake the editable Chapter 2 rooms, PerimeterColliders, PropColliders, doors, lights, and evidence into the scene
            var builtEnv = Chapter2RoomBuilder.Build(coord, bindCallbacks: false);
            var player = Object.FindFirstObjectByType<Room0317.Player.AdrianController>();
            if (player != null && builtEnv.WestEntrySpawn != null)
            {
                player.transform.position = new Vector3(
                    builtEnv.WestEntrySpawn.position.x,
                    builtEnv.WestEntrySpawn.position.y,
                    player.transform.position.z);
            }
            if (Camera.main != null)
            {
                Camera.main.transform.position = new Vector3(0f, 0f, -10f);
            }

            EditorSceneManager.SaveScene(scene, Chapter2ScenePath);
            AddSceneToBuildSettings(Chapter2ScenePath);
            Debug.Log($"<color=green><b>[ROOM 03:17]</b> Created clean editable Chapter 2 scene: {Chapter2ScenePath}</color>");
        }

        private static void AssignSprite(SerializedObject so, string propName, string assetPath)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
            {
                prop.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            }
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == scenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ConfigureEnvironmentImports()
        {
            if (!Directory.Exists(EnvArtFolder)) return;

            foreach (var file in Directory.GetFiles(EnvArtFolder, "*.png"))
            {
                string path = file.Replace('\\', '/');
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;

                bool dirty = false;
                if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; dirty = true; }
                if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                if (Mathf.Abs(imp.spritePixelsToUnits - RoomPPU) > 0.1f) { imp.spritePixelsToUnits = RoomPPU; dirty = true; }
                if (imp.maxTextureSize != 2048) { imp.maxTextureSize = 2048; dirty = true; }
                if (imp.filterMode != FilterMode.Bilinear) { imp.filterMode = FilterMode.Bilinear; dirty = true; }
                if (!imp.alphaIsTransparency) { imp.alphaIsTransparency = true; dirty = true; }

                if (dirty) imp.SaveAndReimport();
            }
        }

        private static void ConfigureObjectImports()
        {
            if (!Directory.Exists(ObjArtFolder)) return;

            foreach (var file in Directory.GetFiles(ObjArtFolder, "*.png"))
            {
                string path = file.Replace('\\', '/');
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;

                bool isFootprintSheet = path.EndsWith("ch2_ev_footprints_decal.png");
                bool dirty = false;

                if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; dirty = true; }
                if (Mathf.Abs(imp.spritePixelsToUnits - ObjectPPU) > 0.1f) { imp.spritePixelsToUnits = ObjectPPU; dirty = true; }
                if (imp.maxTextureSize != 2048) { imp.maxTextureSize = 2048; dirty = true; }
                if (imp.filterMode != FilterMode.Bilinear) { imp.filterMode = FilterMode.Bilinear; dirty = true; }
                if (!imp.alphaIsTransparency) { imp.alphaIsTransparency = true; dirty = true; }

                if (isFootprintSheet)
                {
                    if (imp.spriteImportMode != SpriteImportMode.Multiple)
                    {
                        imp.spriteImportMode = SpriteImportMode.Multiple;
                        dirty = true;
                    }

                    // Slice 2 rows x 3 columns if not yet sliced
                    if (imp.spritesheet == null || imp.spritesheet.Length != 6)
                    {
                        int texW = 0, texH = 0;
                        imp.GetSourceTextureWidthAndHeight(out texW, out texH);
                        if (texW > 0 && texH > 0)
                        {
                            int cols = 3;
                            int rows = 2;
                            float cellW = texW / (float)cols;
                            float cellH = texH / (float)rows;
                            var metas = new List<SpriteMetaData>();
                            int idx = 0;
                            for (int r = rows - 1; r >= 0; r--)
                            {
                                for (int c = 0; c < cols; c++)
                                {
                                    metas.Add(new SpriteMetaData
                                    {
                                        name = $"ch2_ev_footprint_{idx:00}",
                                        rect = new Rect(c * cellW, r * cellH, cellW, cellH),
                                        alignment = (int)SpriteAlignment.Center,
                                        pivot = new Vector2(0.5f, 0.5f)
                                    });
                                    idx++;
                                }
                            }
                            imp.spritesheet = metas.ToArray();
                            dirty = true;
                        }
                    }
                }
                else
                {
                    if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                }

                if (dirty) imp.SaveAndReimport();
            }
        }

        private static void BuildEnvironmentPrefabs()
        {
            CreateRoomPrefab(
                "Room_Ch2_CorridorWest",
                $"{EnvArtFolder}/ch2_env_corridor_west.png",
                new Vector2(0f, 2.2f),
                new Color(1.0f, 0.72f, 0.35f), // Sickly amber emergency sconce
                1.15f,
                5.5f
            );

            CreateRoomPrefab(
                "Room_Ch2_CorridorEast",
                $"{EnvArtFolder}/ch2_env_corridor_east.png",
                new Vector2(-4.2f, 2.0f),
                new Color(1.0f, 0.70f, 0.32f), // Sickly amber emergency sconce
                1.05f,
                5.2f
            );

            CreateRoomPrefab(
                "Room_Ch2_RoofLanding",
                $"{EnvArtFolder}/ch2_env_roof_landing.png",
                new Vector2(4.5f, 2.8f),
                new Color(0.68f, 0.80f, 0.95f), // Cold pale blue-grey winter skylight
                0.95f,
                6.0f
            );
        }

        private static void CreateRoomPrefab(string prefabName, string spritePath, Vector2 lightOffset, Color lightColor, float intensity, float radius)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            var root = new GameObject(prefabName);

            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = "Background";
            sr.sortingOrder = 0;

            var lightGo = new GameObject("EmergencyLight_2D");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = lightOffset;
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = lightColor;
            light.intensity = intensity;
            light.pointLightOuterRadius = radius;
            light.pointLightInnerRadius = radius * 0.25f;
            light.falloffIntensity = 0.55f;

            string prefabPath = $"{EnvPrefabsFolder}/{prefabName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }

        private static void BuildEvidencePrefabs()
        {
            // Evidence 1: Wet Footprints (6 sliced decal prefabs)
            var footprintSprites = AssetDatabase.LoadAllAssetsAtPath($"{ObjArtFolder}/ch2_ev_footprints_decal.png")
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();

            for (int i = 0; i < footprintSprites.Length; i++)
            {
                var fp = new GameObject($"Decal_WetFootprint_{i:00}");
                fp.transform.localScale = new Vector3(0.14f, 0.14f, 1f);
                var sr = fp.AddComponent<SpriteRenderer>();
                sr.sprite = footprintSprites[i];
                sr.sortingLayerName = "Props_Back";
                sr.sortingOrder = 2;
                sr.color = new Color(1f, 1f, 1f, 0.85f);

                PrefabUtility.SaveAsPrefabAsset(fp, $"{EvidencePrefabsFolder}/Decal_WetFootprint_{i:00}.prefab");
                Object.DestroyImmediate(fp);
            }

            // Evidence 2: Soaked Green Umbrella (CollectibleItem + Pharmacy Receipt placeholder inspectSprite)
            var umbrellaSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ObjArtFolder}/ch2_ev_umbrella_green.png");
            var receiptPlaceholder = AssetDatabase.LoadAssetAtPath<Sprite>($"{ObjArtFolder}/ch2_ui_pharmacy_receipt_placeholder.png");
            CreateCollectiblePrefab(
                "Item_SoakedUmbrella",
                umbrellaSprite,
                receiptPlaceholder != null ? receiptPlaceholder : umbrellaSprite,
                "ch2_umbrella_green",
                "Soaked Green Umbrella",
                "Clara's compact dark green umbrella, still dripping freezing water. A cracked resin tag hangs from the strap, with a Nov 14 02:45 AM pharmacy receipt jammed inside.",
                0.12f,
                4.0f
            );

            // Evidence 3: Wall Intercom (InteractablePoint)
            var intercomSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ObjArtFolder}/ch2_ev_wall_intercom.png");
            CreateInteractablePropPrefab(
                "Prop_WallIntercom",
                intercomSprite,
                0.10f,
                4,
                "Wall Intercom",
                new List<DialogueLine>
                {
                    new DialogueLine("Adrian", "Neutral", "The unit intercom. Three missed call alerts from Unit 317's outside buzzer: 03:14, 03:15, and 03:16 AM.")
                }
            );

            // Evidence 4: Cracked Silver Compact Mirror (CollectibleItem + Compact Note placeholder inspectSprite)
            var compactSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ObjArtFolder}/ch2_ev_compact_mirror.png");
            var compactNotePlaceholder = AssetDatabase.LoadAssetAtPath<Sprite>($"{ObjArtFolder}/ch2_ui_compact_note_placeholder.png");
            CreateCollectiblePrefab(
                "Item_CrackedCompactMirror",
                compactSprite,
                compactNotePlaceholder != null ? compactNotePlaceholder : compactSprite,
                "ch2_compact_mirror",
                "Cracked Silver Compact Mirror",
                "A round silver compact mirror with spiderweb-fractured glass and a bent latch. Inside is a folded note: '03:17 - If you won't let me in, I'll wait on the roof stairs where the heater vent blows.'",
                0.09f,
                4.0f
            );

            // Evidence 5: Frozen Fleece Jacket (InteractablePoint)
            var jacketSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ObjArtFolder}/ch2_ev_fleece_jacket.png");
            CreateInteractablePropPrefab(
                "Prop_FrozenFleeceJacket",
                jacketSprite,
                0.12f,
                5,
                "Frozen Fleece Jacket",
                new List<DialogueLine>
                {
                    new DialogueLine("Adrian", "Scared", "Her fleece jacket... wedged in the frame to keep the roof door from locking her out. Ice crystals dust the collar.")
                }
            );

            // Evidence 6: Clara's Moving Box (InteractablePoint)
            var boxSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ObjArtFolder}/ch2_ev_moving_box.png");
            CreateInteractablePropPrefab(
                "Prop_ClaraMovingBox",
                boxSprite,
                0.14f,
                6,
                "Clara's Moving Box",
                new List<DialogueLine>
                {
                    new DialogueLine("Adrian", "Exhausted", "A frost-bitten cardboard moving box, taped shut. My name is written across the top in Clara's neat marker script.")
                }
            );
        }

        private static void CreateCollectiblePrefab(
            string prefabName,
            Sprite worldSprite,
            Sprite inspectSprite,
            string itemId,
            string displayName,
            string description,
            float scale,
            float colliderRadius)
        {
            var go = new GameObject(prefabName);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = worldSprite;
            sr.sortingLayerName = "Props_Back";
            sr.sortingOrder = 5;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = colliderRadius;

            var item = go.AddComponent<CollectibleItem>();
            var data = new InventoryItem(itemId, displayName, description, worldSprite, inspectSprite, true);
            item.Configure(data, 1.4f);

            PrefabUtility.SaveAsPrefabAsset(go, $"{EvidencePrefabsFolder}/{prefabName}.prefab");
            Object.DestroyImmediate(go);
        }

        private static void CreateInteractablePropPrefab(
            string prefabName,
            Sprite worldSprite,
            float scale,
            int sortingOrder,
            string pointName,
            List<DialogueLine> lines)
        {
            var go = new GameObject(prefabName);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = worldSprite;
            sr.sortingLayerName = "Props_Back";
            sr.sortingOrder = sortingOrder;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 4.5f;

            var pt = go.AddComponent<InteractablePoint>();
            pt.Configure(pointName, null, lines, lines, 1.5f);

            PrefabUtility.SaveAsPrefabAsset(go, $"{EvidencePrefabsFolder}/{prefabName}.prefab");
            Object.DestroyImmediate(go);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
            string name = Path.GetFileName(folderPath);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
