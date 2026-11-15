#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Room0317.Apartment;
using Room0317.Dialogue;
using Room0317.Inventory;

namespace Room0317.EditorTools
{
    /// <summary>
    /// Remakes and regenerates the 5 object and clue prefabs using the newly sliced sprites in Assets/Objects:
    /// 1. Item_TarnishedKey: sprite 'key_0' (world scale 0.08, close-up inspection)
    /// 2. Item_TornDiaryPage: sprite 'note_0' (world scale 0.10, close-up inspection)
    /// 3. Item_HiddenNote: sprite 'hidden_note_0' (world scale 0.10, close-up inspection)
    /// 4. Prop_ChangeableFrame: 'normal_frame_1' vs 'eerie_frame_0' (world scale 0.18)
    /// 5. Prop_ChangeableChair: 'chair_0' (world scale 0.12)
    /// Menu: ROOM 03:17 > Remake Object Prefabs from Sliced Sprites
    /// </summary>
    [InitializeOnLoad]
    public static class ObjectPrefabsBuilder
    {
        private const string PrefabsFolder = "Assets/_Project/Prefabs/Objects";

        [MenuItem("ROOM 03:17/Remake Object Prefabs from Sliced Sprites")]
        public static void CreateAllObjectPrefabs()
        {
            EnsureFolder("Assets/_Project");
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder(PrefabsFolder);

            // 1. Create Key Prefab
            CreateKeyPrefab();

            // 2. Create Note Prefab
            CreateNotePrefab();

            // 3. Create Hidden Note Prefab
            CreateHiddenNotePrefab();

            // 4. Create Changeable Frame Prefab
            CreateChangeableFramePrefab();

            // 5. Create Changeable Chair Prefab
            CreateChangeableChairPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green><b>[ROOM 03:17]</b> Successfully remade all 5 Object Prefabs from newly sliced sprites!</color>");
        }

        private static void CreateKeyPrefab()
        {
            string path = $"{PrefabsFolder}/Item_TarnishedKey.prefab";
            var sprites = LoadSprites("Assets/Objects/key.png");
            sprites.TryGetValue("key_0", out var keySprite);

            var go = new GameObject("Item_TarnishedKey");
            go.transform.localScale = new Vector3(0.08f, 0.08f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = keySprite;
            sr.sortingLayerName = "Props_Back";
            sr.sortingOrder = 5;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 4.0f; // in local space (with scale 0.08, ~0.32 world units)

            var item = go.AddComponent<CollectibleItem>();
            var data = new InventoryItem(
                "key_brass",
                "Tarnished Brass Key",
                "An old brass key found in the apartment. Its teeth are worn and dark reddish stains mark the barrel. It looks like it fits the locked wardrobe in the bedroom.",
                keySprite,
                keySprite,
                true
            );

            item.Configure(data, 1.4f);

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static void CreateNotePrefab()
        {
            string path = $"{PrefabsFolder}/Item_TornDiaryPage.prefab";
            var sprites = LoadSprites("Assets/Objects/note.png");
            sprites.TryGetValue("note_0", out var noteSprite);

            var go = new GameObject("Item_TornDiaryPage");
            go.transform.localScale = new Vector3(0.10f, 0.10f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = noteSprite;
            sr.sortingLayerName = "Props_Back";
            sr.sortingOrder = 5;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 3.5f;

            var item = go.AddComponent<CollectibleItem>();
            var data = new InventoryItem(
                "note_diary",
                "Torn Diary Page",
                "A crumpled page from a journal: '03:17... every night at 03:17 the air turns freezing cold. I hear something breathing behind the wardrobe door. But the front deadbolt is always locked from the inside.'",
                noteSprite,
                noteSprite,
                true
            );

            item.Configure(data, 1.4f);

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static void CreateHiddenNotePrefab()
        {
            string path = $"{PrefabsFolder}/Item_HiddenNote.prefab";
            var sprites = LoadSprites("Assets/Objects/hidden_note.png");
            sprites.TryGetValue("hidden_note_0", out var hiddenSprite);

            var go = new GameObject("Item_HiddenNote");
            go.transform.localScale = new Vector3(0.10f, 0.10f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = hiddenSprite;
            sr.sortingLayerName = "Props_Back";
            sr.sortingOrder = 5;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 3.5f;

            var item = go.AddComponent<CollectibleItem>();
            var data = new InventoryItem(
                "note_hidden",
                "Scratch-Marked Note",
                "A damp note hidden behind the furniture. Shaky handwriting: 'YOU NEVER LIVED ALONE'. The ink is still fresh.",
                hiddenSprite,
                hiddenSprite,
                true
            );

            item.Configure(data, 1.4f);

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static void CreateChangeableFramePrefab()
        {
            string path = $"{PrefabsFolder}/Prop_ChangeableFrame.prefab";
            var normalSprites = LoadSprites("Assets/Objects/normal_frame.png");
            var eerieSprites = LoadSprites("Assets/Objects/eerie_frame.png");

            normalSprites.TryGetValue("normal_frame_1", out var normalSprite);
            eerieSprites.TryGetValue("eerie_frame_0", out var eerieSprite);

            var go = new GameObject("Prop_ChangeableFrame");
            go.transform.localScale = new Vector3(0.18f, 0.18f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = normalSprite;
            sr.sortingLayerName = "Props_Back";
            sr.sortingOrder = 3;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 3.5f;

            var prop = go.AddComponent<ChangeableProp>();
            prop.Configure(
                null,
                normalSprite,
                eerieSprite,
                normalSprite,
                eerieSprite,
                "Framed Artwork",
                "A calm landscape painting in a dark wooden frame.",
                "The painting has changed! Dark distorted figures stand between the trees!"
            );

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static void CreateChangeableChairPrefab()
        {
            string path = $"{PrefabsFolder}/Prop_ChangeableChair.prefab";
            var sprites = LoadSprites("Assets/Objects/chair.png");
            sprites.TryGetValue("chair_0", out var chairSprite);

            var go = new GameObject("Prop_ChangeableChair");
            go.transform.localScale = new Vector3(0.12f, 0.12f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = chairSprite;
            sr.sortingLayerName = "Props_Back";
            sr.sortingOrder = 2;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(6.5f, 11f);

            var prop = go.AddComponent<ChangeableProp>();
            prop.Configure(
                null,
                chairSprite,
                chairSprite,
                chairSprite,
                chairSprite,
                "Old Wooden Chair",
                "An old wooden dining chair with deep claw-like grooves scratched into the legs.",
                "The chair seems slightly tilted, as if someone just stood up from it."
            );

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static System.Collections.Generic.Dictionary<string, Sprite> LoadSprites(string assetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Sprite>()
                .ToDictionary(s => s.name, s => s);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
