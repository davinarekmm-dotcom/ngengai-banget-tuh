#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Room0317.Dialogue;
using Room0317.Player;

namespace Room0317.EditorTools
{
    /// <summary>
    /// One-click setup for Adrian:
    ///  1. Slices "Adrian Animation Sheet.png" into 8 animations x 4 frames (bottom-centre pivots, feet aligned).
    ///  2. Slices "Adrian Portraits.png" into 4 expressions x 4 talking frames.
    ///  3. Generates AnimationClips + an AnimatorController.
    ///  4. Builds Assets/_Project/Scenes/Adrian_TestScene.unity with a playable Adrian, an animation showcase row,
    ///     and a visual-novel dialogue box (portrait above the box's left side).
    /// Menu: ROOM 03:17 > Build Adrian Test Scene. Runs automatically once if the test scene does not exist yet.
    /// </summary>
    [InitializeOnLoad]
    public static class AdrianSetupBuilder
    {
        private const string AnimSheetPath = "Assets/Characters/Adrian Animation Sheet.png";
        private const string PortraitSheetPath = "Assets/Characters/Adrian Portraits.png";
        private const string AnimFolder = "Assets/_Project/Animations/Adrian";
        private const string ControllerPath = AnimFolder + "/Adrian.controller";
        private const string ScenePath = "Assets/_Project/Scenes/Adrian_TestScene.unity";

        // Animation sheet: 8192 x 4096. Each entry: name, fps, baseline (feet row, top-left origin), 4 frame centre X values.
        private const float AnimPPU = 400f;
        private const int AnimCellW = 460;
        private const int AnimCellH = 700;

        private struct AnimDef
        {
            public string Name; public float Fps; public int Baseline; public int[] Centers;
            public AnimDef(string n, float fps, int b, params int[] c) { Name = n; Fps = fps; Baseline = b; Centers = c; }
        }

        private static readonly AnimDef[] Anims =
        {
            new AnimDef("Walk_Right", 8f, 687,  291,  835, 1379, 1922),
            new AnimDef("Idle_Left",  4f, 1411, 282,  827, 1369, 1913),
            new AnimDef("Idle_Up",    4f, 1402, 2407, 2912, 3448, 3950),
            new AnimDef("Idle_Right", 4f, 2130, 290,  834, 1378, 1919),
            new AnimDef("Idle_Down",  4f, 2115, 2474, 2948, 3447, 3936),
            new AnimDef("Walk_Left",  8f, 2851, 282,  825, 1369, 1913),
            new AnimDef("Walk_Down",  8f, 3555, 326,  799, 1296, 1787),
            new AnimDef("Walk_Up",    8f, 3539, 2377, 2920, 3463, 4007),
        };

        // Portrait sheet: 4096 x 4096, 4 rows (expressions) x 4 talking frames.
        private const int PortraitCellW = 520;
        private const int PortraitCellH = 720;

        private static readonly AnimDef[] Portraits =
        {
            new AnimDef("Neutral",   0f, 729,  302, 841, 1384, 1926),
            new AnimDef("Exhausted", 0f, 1468, 284, 828, 1371, 1916),
            new AnimDef("Scared",    0f, 2190, 264, 809, 1352, 1894),
            new AnimDef("Nervous",   0f, 2917, 260, 803, 1345, 1889),
        };

        static AdrianSetupBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (File.Exists(ScenePath)) return;
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(AnimSheetPath) == null) return;
                Build();
            };
        }

        [MenuItem("ROOM 03:17/Build Adrian Test Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder("Assets/_Project");
            EnsureFolder("Assets/_Project/Animations");
            EnsureFolder(AnimFolder);
            EnsureFolder("Assets/_Project/Scenes");

            SliceSheet(AnimSheetPath, AnimPPU, Anims, AnimCellW, AnimCellH, 8192);
            SliceSheet(PortraitSheetPath, 100f, Portraits, PortraitCellW, PortraitCellH, 4096);

            var animSprites = LoadSprites(AnimSheetPath);
            var portraitSprites = LoadSprites(PortraitSheetPath);

            var controller = BuildAnimator(animSprites);
            BuildScene(controller, animSprites, portraitSprites);

            Debug.Log("<color=green><b>[ROOM 03:17]</b> Adrian sliced, animated, and Adrian_TestScene built.</color>");
        }

        // ------------------------------------------------------------------ SLICING
        private static void SliceSheet(string path, float ppu, AnimDef[] defs, int cellW, int cellH, int maxSize)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) { Debug.LogError("Missing texture: " + path); return; }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = ppu;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            // Source size (unaffected by maxTextureSize)
            importer.GetSourceTextureWidthAndHeight(out int texW, out int texH);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            var existing = dp.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            var rects = new List<SpriteRect>();

            foreach (var def in defs)
            {
                for (int i = 0; i < def.Centers.Length; i++)
                {
                    string name = $"Adrian_{def.Name}_{i}";
                    int h = Mathf.Min(cellH, def.Baseline + 1);
                    float x = Mathf.Clamp(def.Centers[i] - cellW / 2f, 0, texW - cellW);
                    float y = texH - def.Baseline - 1; // convert top-left origin -> bottom-left origin

                    rects.Add(new SpriteRect
                    {
                        name = name,
                        rect = new Rect(x, y, cellW, h),
                        alignment = SpriteAlignment.Custom,
                        pivot = new Vector2(0.5f, 0f), // feet / bust bottom
                        spriteID = existing.TryGetValue(name, out var id) ? id : GUID.Generate()
                    });
                }
            }

            dp.SetSpriteRects(rects.ToArray());
            var nameIds = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIds?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            dp.Apply();
            importer.SaveAndReimport();
        }

        private static Dictionary<string, Sprite> LoadSprites(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name, s => s);
        }

        private static Sprite[] Frames(Dictionary<string, Sprite> sprites, string anim)
        {
            var list = new List<Sprite>();
            for (int i = 0; i < 4; i++)
                if (sprites.TryGetValue($"Adrian_{anim}_{i}", out var s)) list.Add(s);
            return list.ToArray();
        }

        // ------------------------------------------------------------------ ANIMATION
        private static AnimatorController BuildAnimator(Dictionary<string, Sprite> sprites)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = controller.layers[0].stateMachine;

            foreach (var def in Anims)
            {
                var clip = BuildClip(def.Name, def.Fps, Frames(sprites, def.Name));
                var state = sm.AddState(def.Name);
                state.motion = clip;
                if (def.Name == "Idle_Down") sm.defaultState = state;
            }

            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimationClip BuildClip(string name, float fps, Sprite[] frames)
        {
            string path = $"{AnimFolder}/Adrian_{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            clip.frameRate = fps;

            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < frames.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
            // Hold the last frame for its full duration before looping back to frame 0.
            keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / fps, value = frames[frames.Length - 1] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        // ------------------------------------------------------------------ SCENE
        private static void BuildScene(AnimatorController controller, Dictionary<string, Sprite> anim, Dictionary<string, Sprite> portraits)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.11f, 0.10f, 0.10f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<UniversalAdditionalCameraData>();

            // Global 2D light so Sprite-Lit materials are visible
            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            BuildFloor();
            BuildBounds();

            // Showcase row: every animation looping side by side
            var showcaseRoot = new GameObject("--- ANIMATION SHOWCASE ---");
            float startX = -5.6f;
            for (int i = 0; i < Anims.Length; i++)
            {
                var go = new GameObject("Showcase_" + Anims[i].Name);
                go.transform.SetParent(showcaseRoot.transform);
                go.transform.position = new Vector3(startX + i * 1.6f, 1.0f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Frames(anim, Anims[i].Name).FirstOrDefault();
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                var a = go.AddComponent<Animator>();
                a.runtimeAnimatorController = controller;
                go.AddComponent<AnimationShowcase>().StateName = Anims[i].Name;

                CreateWorldLabel(go.transform, Anims[i].Name.Replace('_', ' '), new Vector3(0f, -0.15f, 0f));
            }

            // Playable Adrian
            var player = new GameObject("Player_Adrian") { tag = "Player" };
            player.transform.position = new Vector3(0f, -1.6f, 0f);
            var psr = player.AddComponent<SpriteRenderer>();
            psr.sprite = Frames(anim, "Idle_Down").FirstOrDefault();
            psr.spriteSortPoint = SpriteSortPoint.Pivot;
            var pa = player.AddComponent<Animator>();
            pa.runtimeAnimatorController = controller;
            var rb = player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var col = player.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.5f, 0.2f);
            col.offset = new Vector2(0f, 0.1f);
            var controllerComp = player.AddComponent<AdrianController>();

            // Dialogue UI
            var dialogueBox = BuildDialogueUI(portraits);

            var testerGo = new GameObject("DialogueTester");
            var tester = testerGo.AddComponent<DialogueTester>();
            var so = new SerializedObject(tester);
            so.FindProperty("dialogueBox").objectReferenceValue = dialogueBox;
            so.FindProperty("adrian").objectReferenceValue = controllerComp;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
        }

        private static void BuildFloor()
        {
            // Simple generated checker floor so movement is easy to read.
            string full = Path.GetFullPath("Assets/_Project/Animations/TestFloor.png");
            if (!File.Exists(full))
            {
                var tex = new Texture2D(64, 64);
                var c1 = new Color(0.20f, 0.17f, 0.15f);
                var c2 = new Color(0.17f, 0.145f, 0.13f);
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                        tex.SetPixel(x, y, ((x / 32) + (y / 32)) % 2 == 0 ? c1 : c2);
                File.WriteAllBytes(full, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset("Assets/_Project/Animations/TestFloor.png");
                var imp = (TextureImporter)AssetImporter.GetAtPath("Assets/_Project/Animations/TestFloor.png");
                imp.textureType = TextureImporterType.Sprite;
                imp.spritePixelsPerUnit = 64;
                imp.filterMode = FilterMode.Point;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.mipmapEnabled = false;
                var s = new TextureImporterSettings();
                imp.ReadTextureSettings(s);
                s.spriteMeshType = SpriteMeshType.FullRect;
                imp.SetTextureSettings(s);
                imp.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Animations/TestFloor.png");
            var floor = new GameObject("Floor");
            var sr = floor.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(16f, 9f);
            sr.sortingOrder = -100;
        }

        private static void BuildBounds()
        {
            var root = new GameObject("--- BOUNDS ---");
            AddWall(root.transform, "Wall_Top", new Vector2(0f, 3.0f), new Vector2(16f, 0.4f));
            AddWall(root.transform, "Wall_Bottom", new Vector2(0f, -4.1f), new Vector2(16f, 0.4f));
            AddWall(root.transform, "Wall_Left", new Vector2(-7.3f, 0f), new Vector2(0.4f, 9f));
            AddWall(root.transform, "Wall_Right", new Vector2(7.3f, 0f), new Vector2(0.4f, 9f));
        }

        private static void AddWall(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.AddComponent<BoxCollider2D>().size = size;
        }

        private static void CreateWorldLabel(Transform parent, string text, Vector3 localPos)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = 0.035f;
            tm.anchor = TextAnchor.UpperCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.85f, 0.8f, 0.72f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = 50;
        }

        // ------------------------------------------------------------------ DIALOGUE UI
        private static DialogueBox BuildDialogueUI(Dictionary<string, Sprite> portraits)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var canvasGo = new GameObject("Dialogue Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Controls hint (always visible)
            var hint = CreateText(canvasGo.transform, "ControlsHint", font, 26, new Color(0.85f, 0.8f, 0.72f, 0.85f),
                "WASD / Arrows: Move    |    E / Space / Click: Advance dialogue    |    T: Replay dialogue", TextAnchor.UpperLeft);
            SetRect(hint.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -30), new Vector2(1400, 40));

            // Layout constants (reference resolution 1920x1080)
            const float boxMarginX = 90f;
            const float boxBottom = 30f;
            const float boxHeight = 230f;
            const float portraitSpacing = 60f;   // gap between the box's left edge and Adrian
            const float portraitHeight = 560f;
            const float portraitWidth = portraitHeight * PortraitCellW / PortraitCellH;

            var root = new GameObject("DialogueRoot", typeof(RectTransform));
            root.transform.SetParent(canvasGo.transform, false);
            SetStretch((RectTransform)root.transform);

            // 1) Portrait – above the box, on its left side, tucked slightly behind the top edge.
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(root.transform, false);
            var portrait = portraitGo.GetComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            portrait.sprite = portraits.TryGetValue("Adrian_Neutral_0", out var p0) ? p0 : null;
            SetRect(portrait.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(boxMarginX + portraitSpacing, boxBottom + boxHeight - 6f),
                new Vector2(portraitWidth, portraitHeight));

            // 2) Text box
            var boxGo = new GameObject("TextBox", typeof(RectTransform), typeof(Image), typeof(Outline));
            boxGo.transform.SetParent(root.transform, false);
            var box = boxGo.GetComponent<Image>();
            box.sprite = rounded;
            box.type = Image.Type.Sliced;
            box.color = new Color(0.14f, 0.12f, 0.115f, 0.94f);
            var outline = boxGo.GetComponent<Outline>();
            outline.effectColor = new Color(0.78f, 0.71f, 0.62f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);
            var boxRt = box.rectTransform;
            boxRt.anchorMin = new Vector2(0f, 0f);
            boxRt.anchorMax = new Vector2(1f, 0f);
            boxRt.pivot = new Vector2(0.5f, 0f);
            boxRt.sizeDelta = new Vector2(-boxMarginX * 2f, boxHeight);
            boxRt.anchoredPosition = new Vector2(0f, boxBottom);

            var body = CreateText(boxGo.transform, "BodyText", font, 34, new Color(0.93f, 0.89f, 0.83f), "", TextAnchor.UpperLeft);
            body.lineSpacing = 1.15f;
            var bodyRt = body.rectTransform;
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(45f, 30f);
            bodyRt.offsetMax = new Vector2(-70f, -50f);

            var indicatorGo = new GameObject("ContinueIndicator", typeof(RectTransform), typeof(Image));
            indicatorGo.transform.SetParent(boxGo.transform, false);
            var indicator = indicatorGo.GetComponent<Image>();
            indicator.color = new Color(0.78f, 0.71f, 0.62f);
            SetRect(indicator.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-40f, 35f), new Vector2(16f, 16f));
            indicator.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            // 3) Name tag – sits on the box's top-left edge (overlapping the portrait like the reference).
            var tagGo = new GameObject("NameTag", typeof(RectTransform), typeof(Image));
            tagGo.transform.SetParent(root.transform, false);
            var tag = tagGo.GetComponent<Image>();
            tag.sprite = rounded;
            tag.type = Image.Type.Sliced;
            tag.color = new Color(0.45f, 0.33f, 0.37f, 1f);
            SetRect(tag.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f),
                new Vector2(boxMarginX + 30f, boxBottom + boxHeight), new Vector2(230f, 54f));

            var nameText = CreateText(tagGo.transform, "NameText", font, 32, new Color(0.97f, 0.94f, 0.9f), "Adrian", TextAnchor.MiddleCenter);
            nameText.fontStyle = FontStyle.BoldAndItalic;
            SetStretch(nameText.rectTransform);

            // Expressions
            var expressions = new List<PortraitExpression>();
            foreach (var def in Portraits)
                expressions.Add(new PortraitExpression { name = def.Name, frames = Frames(portraits, def.Name) });

            var dialogueBox = canvasGo.AddComponent<DialogueBox>();
            dialogueBox.Configure(root, portrait, nameText, body, indicator, expressions);
            EditorUtility.SetDirty(dialogueBox);
            return dialogueBox;
        }

        private static Text CreateText(Transform parent, string name, Font font, int size, Color color, string content, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.text = content;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void SetStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------ UTIL
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
