#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Room0317.Apartment;
using Room0317.Dialogue;
using Room0317.Player;

namespace Room0317.EditorTools
{
    /// <summary>
    /// Master builder for the 5-room cohesive apartment level with:
    /// 1. Physical doorway interactions ([E] Enter Room) to transition between rooms.
    /// 2. Room-focused camera framing (each room framed individually, no neighboring rooms or full house visible).
    /// 3. Enlarged player scale (~0.72x) for prominent, readable character presence.
    /// 4. 5 separate rooms (Bedroom, Hallway, Living Room, Kitchen, Bathroom).
    /// 5. Type A (Normal) and Type B (Eerie) room sprite toggles.
    /// 6. VN dialogue box with Adrian portrait and animated mouth talk cycle.
    /// Menu: ROOM 03:17 > Build Apartment Cohesive Scene.
    /// </summary>
    [InitializeOnLoad]
    public static class ApartmentLevelBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/01_Apartment_Cohesive.unity";
        private const string TilesFolder = "Assets/_Project/Tiles";
        private const string ApartmentFolder = "Assets/Environment/Apartment";

        // Room dimensions: 16 x 9 units (16:9 aspect ratio)
        public const float RoomWidth = 16.0f;
        public const float RoomHeight = 9.0f;
        public const float RoomPPU = 104.5f;

        // Centers of the 5 rooms - spaced generously so each room is completely isolated visually
        public static readonly Vector2 PosBedroom = new Vector2(0f, 0f);
        public static readonly Vector2 PosHallway = new Vector2(40f, 0f);
        public static readonly Vector2 PosLivingRoom = new Vector2(80f, 0f);
        public static readonly Vector2 PosKitchen = new Vector2(80f, -25f);
        public static readonly Vector2 PosBathroom = new Vector2(40f, 25f);

        [MenuItem("ROOM 03:17/Rebuild Apartment Cohesive Scene (Warning: Replaces Scene)")]
        public static void BuildCohesiveApartmentScene()
        {
            if (File.Exists(ScenePath))
            {
                if (!EditorUtility.DisplayDialog("Rebuild Apartment Scene?",
                    "Rebuilding the scene will reset game objects to the initial template layout. Your current scene will be overwritten.\n\nAre you sure you want to proceed?",
                    "Yes, Rebuild", "Cancel"))
                {
                    return;
                }
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder("Assets/_Project");
            EnsureFolder("Assets/_Project/Tiles");
            EnsureFolder("Assets/_Project/Scenes");

            // 1. Prepare and import all normal and eerie room sprites as Single Sprites
            PrepareRoomTextures();

            // 2. Create Tile Assets
            var tiles = CreateTileAssets();

            // 3. Create New Scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 4. Build Grid & Tilemaps (Floor & boundary tiles for each room)
            var gridObj = BuildGridAndTilemaps(tiles);

            // 5. Build the 5 Rooms with Normal & Eerie Sprites, Colliders, Doors & Interactables
            var rooms = BuildRooms();

            // 6. Build 2D Lighting (Global ambient + warm point lamps per room)
            BuildLighting();

            // 7. Spawn Player Adrian (Enlarged to 0.72 scale)
            var player = BuildPlayer();

            // 8. Build Camera (Focused on Bedroom at start with 4.5 orthographic size)
            var cam = BuildCamera(PosBedroom);

            // 9. Build UI (ScreenFader, VN Dialogue Box with Adrian portrait + Testing HUD)
            BuildUI();

            // 10. Build ApartmentManager Coordinator
            BuildManager(rooms, cam);

            // 11. Wire Door Connections between Rooms
            WireDoorTransitions(rooms);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            Debug.Log("<color=green><b>[ROOM 03:17]</b> Scene built successfully! Doorway interactions & room-isolated camera configured.</color>");
        }

        // ====================================================================
        // 1. ROOM SPRITE IMPORTER
        // ====================================================================
        private static void PrepareRoomTextures()
        {
            string[] files = Directory.GetFiles(ApartmentFolder, "*.png");
            foreach (var file in files)
            {
                string relPath = file.Replace('\\', '/');
                if (relPath.Contains("Apartment floor map layout")) continue;

                var imp = (TextureImporter)AssetImporter.GetAtPath(relPath);
                if (imp != null)
                {
                    bool dirty = false;
                    if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; dirty = true; }
                    if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                    if (Mathf.Abs(imp.spritePixelsPerUnit - RoomPPU) > 0.1f) { imp.spritePixelsPerUnit = RoomPPU; dirty = true; }
                    if (imp.maxTextureSize != 2048) { imp.maxTextureSize = 2048; dirty = true; }
                    if (imp.filterMode != FilterMode.Bilinear) { imp.filterMode = FilterMode.Bilinear; dirty = true; }
                    if (!imp.alphaIsTransparency) { imp.alphaIsTransparency = true; dirty = true; }

                    if (dirty)
                    {
                        imp.SaveAndReimport();
                    }
                }
            }
            AssetDatabase.Refresh();
        }

        private static Sprite LoadRoomSprite(string name)
        {
            string path = $"{ApartmentFolder}/{name}.png";
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ====================================================================
        // 2. TILE ASSETS CREATION
        // ====================================================================
        public struct TileAssets
        {
            public Tile FloorWood;
            public Tile FloorCeramic;
            public Tile FloorCarpet;
            public Tile WallSolid;
        }

        private static TileAssets CreateTileAssets()
        {
            var res = new TileAssets();
            res.FloorWood = GetOrCreateTile("Tile_Floor_Wood", new Color(0.24f, 0.18f, 0.13f), new Color(0.19f, 0.14f, 0.10f), Tile.ColliderType.None);
            res.FloorCeramic = GetOrCreateTile("Tile_Floor_Ceramic", new Color(0.25f, 0.23f, 0.21f), new Color(0.18f, 0.16f, 0.15f), Tile.ColliderType.None);
            res.FloorCarpet = GetOrCreateTile("Tile_Floor_Carpet", new Color(0.20f, 0.14f, 0.13f), new Color(0.16f, 0.11f, 0.10f), Tile.ColliderType.None);
            res.WallSolid = GetOrCreateWallTile("Tile_Wall", new Color(0.11f, 0.10f, 0.10f), new Color(0.28f, 0.25f, 0.23f));
            AssetDatabase.SaveAssets();
            return res;
        }

        private static Tile GetOrCreateTile(string name, Color c1, Color c2, Tile.ColliderType colType)
        {
            string pngPath = $"{TilesFolder}/{name}.png";
            string assetPath = $"{TilesFolder}/{name}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
            if (existing != null && existing.sprite != null) return existing;

            int size = 32;
            var tex = new Texture2D(size, size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, (x == 0 || y == 0) ? c2 : c1);
            tex.Apply();
            File.WriteAllBytes(Path.GetFullPath(pngPath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(pngPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            imp.textureType = TextureImporterType.Sprite;
            imp.spritePixelsPerUnit = 32;
            imp.filterMode = FilterMode.Point;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.colliderType = colType;
            AssetDatabase.CreateAsset(tile, assetPath);
            return tile;
        }

        private static Tile GetOrCreateWallTile(string name, Color wallColor, Color bevelColor)
        {
            string pngPath = $"{TilesFolder}/{name}.png";
            string assetPath = $"{TilesFolder}/{name}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
            if (existing != null && existing.sprite != null) return existing;

            int size = 32;
            var tex = new Texture2D(size, size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, (y >= size - 4 || x == 0 || x == size - 1) ? bevelColor : wallColor);
            tex.Apply();
            File.WriteAllBytes(Path.GetFullPath(pngPath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(pngPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            imp.textureType = TextureImporterType.Sprite;
            imp.spritePixelsPerUnit = 32;
            imp.filterMode = FilterMode.Point;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.colliderType = Tile.ColliderType.Grid;
            AssetDatabase.CreateAsset(tile, assetPath);
            return tile;
        }

        // ====================================================================
        // 3. GRID & TILEMAPS
        // ====================================================================
        private static GameObject BuildGridAndTilemaps(TileAssets tiles)
        {
            var gridGo = new GameObject("Grid");
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = new Vector3(1.0f, 1.0f, 0f);

            var floorGo = new GameObject("Floor_Tilemap");
            floorGo.transform.SetParent(gridGo.transform, false);
            var floorTm = floorGo.AddComponent<Tilemap>();
            var floorRend = floorGo.AddComponent<TilemapRenderer>();
            floorRend.sortingLayerName = "Background";
            floorRend.sortingOrder = 0;

            var wallsGo = new GameObject("Walls_Tilemap");
            wallsGo.transform.SetParent(gridGo.transform, false);
            var wallsTm = wallsGo.AddComponent<Tilemap>();
            var wallsRend = wallsGo.AddComponent<TilemapRenderer>();
            wallsRend.sortingLayerName = "Props_Front";
            wallsRend.sortingOrder = 10;
            wallsGo.AddComponent<TilemapCollider2D>();

            // Paint floor & wall perimeters for each isolated room
            PaintRoomGrid(floorTm, wallsTm, PosBedroom, tiles.FloorWood, tiles.WallSolid);
            PaintRoomGrid(floorTm, wallsTm, PosHallway, tiles.FloorWood, tiles.WallSolid);
            PaintRoomGrid(floorTm, wallsTm, PosLivingRoom, tiles.FloorWood, tiles.WallSolid);
            PaintRoomGrid(floorTm, wallsTm, PosKitchen, tiles.FloorCeramic, tiles.WallSolid);
            PaintRoomGrid(floorTm, wallsTm, PosBathroom, tiles.FloorCeramic, tiles.WallSolid);

            return gridGo;
        }

        private static void PaintRoomGrid(Tilemap floorTm, Tilemap wallsTm, Vector2 roomCenter, Tile floorTile, Tile wallTile)
        {
            int cx = Mathf.RoundToInt(roomCenter.x);
            int cy = Mathf.RoundToInt(roomCenter.y);

            int xMin = cx - 8;
            int xMax = cx + 7;
            int yMin = cy - 4;
            int yMax = cy + 4;

            // Fill floor
            for (int x = xMin; x <= xMax; x++)
            {
                for (int y = yMin; y <= yMax; y++)
                {
                    floorTm.SetTile(new Vector3Int(x, y, 0), floorTile);
                }
            }

            // Outer wall perimeter
            for (int x = xMin; x <= xMax; x++)
            {
                wallsTm.SetTile(new Vector3Int(x, yMin, 0), wallTile);
                wallsTm.SetTile(new Vector3Int(x, yMax, 0), wallTile);
            }
            for (int y = yMin; y <= yMax; y++)
            {
                wallsTm.SetTile(new Vector3Int(xMin, y, 0), wallTile);
                wallsTm.SetTile(new Vector3Int(xMax, y, 0), wallTile);
            }
        }

        // ====================================================================
        // 4. THE 5 ROOMS & STATE SPRITES (TYPE A vs TYPE B)
        // ====================================================================
        private static List<ApartmentRoom> BuildRooms()
        {
            var roomsRoot = new GameObject("--- ROOMS ---");
            var list = new List<ApartmentRoom>();

            // 1. Bedroom (PosBedroom: 0, 0)
            list.Add(CreateRoomWithSprites(roomsRoot.transform, RoomIds.Bedroom, "Bedroom", PosBedroom,
                "normal_bedroom", "eerie_bedroom", BuildBedroomContent));

            // 2. Hallway (PosHallway: 40, 0)
            list.Add(CreateRoomWithSprites(roomsRoot.transform, RoomIds.Hallway, "Hallway", PosHallway,
                "normal_hallway", "eerie_hallway", BuildHallwayContent));

            // 3. Living Room (PosLivingRoom: 80, 0)
            list.Add(CreateRoomWithSprites(roomsRoot.transform, RoomIds.LivingRoom, "Living Room", PosLivingRoom,
                "normal_livingroom", "eerie_livingroom", BuildLivingRoomContent));

            // 4. Kitchen (PosKitchen: 80, -25)
            list.Add(CreateRoomWithSprites(roomsRoot.transform, RoomIds.Kitchen, "Kitchen", PosKitchen,
                "normal_kitchen", "eerie_kitchen", BuildKitchenContent));

            // 5. Bathroom (PosBathroom: 40, 25)
            list.Add(CreateRoomWithSprites(roomsRoot.transform, RoomIds.Bathroom, "Bathroom", PosBathroom,
                "normal_bathroom", "eerie_bathroom", BuildBathroomContent));

            return list;
        }

        private static ApartmentRoom CreateRoomWithSprites(Transform parent, string id, string name, Vector2 pos,
            string normalSpriteName, string eerieSpriteName,
            System.Action<Transform, ApartmentRoom> buildContent)
        {
            var roomGo = new GameObject("Room_" + id);
            roomGo.transform.SetParent(parent, false);
            roomGo.transform.position = pos;

            var room = roomGo.AddComponent<ApartmentRoom>();

            // State A Sprite (Normal)
            var visAGo = new GameObject("Visual_StateA");
            visAGo.transform.SetParent(roomGo.transform, false);
            var srA = visAGo.AddComponent<SpriteRenderer>();
            srA.sprite = LoadRoomSprite(normalSpriteName);
            srA.sortingLayerName = "Background";
            srA.sortingOrder = 1;

            // State B Sprite (Eerie)
            var visBGo = new GameObject("Visual_StateB");
            visBGo.transform.SetParent(roomGo.transform, false);
            var srB = visBGo.AddComponent<SpriteRenderer>();
            srB.sprite = LoadRoomSprite(eerieSpriteName);
            srB.sortingLayerName = "Background";
            srB.sortingOrder = 1;
            visBGo.SetActive(false);

            room.RegisterVisuals(srA, srB);

            // Invisible Detection Trigger covering entire room
            var triggerGo = new GameObject("RoomTrigger");
            triggerGo.transform.SetParent(roomGo.transform, false);
            triggerGo.transform.localPosition = Vector3.zero;

            var col = triggerGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(15.2f, 8.2f);

            var trigger = triggerGo.AddComponent<RoomTrigger>();
            trigger.Initialize(id, room);

            room.Initialize(id, name, trigger);

            // Solid Room Perimeter Colliders (ensures player cannot walk out of the room boundaries)
            var boundsGo = new GameObject("PerimeterColliders");
            boundsGo.transform.SetParent(roomGo.transform, false);
            AddObstacle(boundsGo.transform, "Wall_Top", new Vector2(0f, 4.4f), new Vector2(16.5f, 0.8f));
            AddObstacle(boundsGo.transform, "Wall_Bottom", new Vector2(0f, -4.4f), new Vector2(16.5f, 0.8f));
            AddObstacle(boundsGo.transform, "Wall_Left", new Vector2(-7.9f, 0f), new Vector2(0.8f, 9.5f));
            AddObstacle(boundsGo.transform, "Wall_Right", new Vector2(7.9f, 0f), new Vector2(0.8f, 9.5f));

            // Build furniture colliders, doorway transitions, and interactable points
            buildContent?.Invoke(roomGo.transform, room);

            return room;
        }

        // ====================================================================
        // 5. ROOM CONTENTS, DOORS & INTERACTABLES
        // ====================================================================
        private static void BuildBedroomContent(Transform parent, ApartmentRoom room)
        {
            var perm = new GameObject("FurnitureColliders");
            perm.transform.SetParent(parent, false);

            AddObstacle(perm.transform, "Col_Bed", new Vector2(-3.0f, 0.5f), new Vector2(4.5f, 5.0f));
            AddObstacle(perm.transform, "Col_Nightstand", new Vector2(-5.8f, -2.0f), new Vector2(1.8f, 1.8f));
            AddObstacle(perm.transform, "Col_Wardrobe", new Vector2(3.2f, 2.8f), new Vector2(3.0f, 2.2f));
            AddObstacle(perm.transform, "Col_Desk", new Vector2(5.5f, -0.5f), new Vector2(1.8f, 3.5f));

            // Interactable Points
            CreateInteractable(parent, "Inspect_Wardrobe", new Vector2(3.2f, 2.8f), room,
                new List<DialogueLine> {
                    new DialogueLine("Adrian", "Neutral", "My wardrobe. Securely shut. Extra winter blankets and jackets are stored inside.")
                },
                new List<DialogueLine> {
                    new DialogueLine("Adrian", "Scared", "THE WARDROBE DOOR IS WIDE OPEN! The wood around the latch was splintered from the INSIDE!"),
                    new DialogueLine("Adrian", "Scared", "And there are dark wet barefoot prints leading from my bed right into the wardrobe!")
                });

            CreateInteractable(parent, "Inspect_Nightstand_Clock", new Vector2(-5.8f, -2.0f), room,
                new List<DialogueLine> {
                    new DialogueLine("Adrian", "Exhausted", "03:17 AM. Red glowing digits. The alarm isn't set, but I wake up at this exact second every night.")
                },
                new List<DialogueLine> {
                    new DialogueLine("Adrian", "Nervous", "03:17 AM... the clock display is flickering erratically. The air around the bed is freezing cold.")
                });

            CreateInteractable(parent, "Inspect_Desk", new Vector2(5.5f, -0.5f), room,
                new List<DialogueLine> {
                    new DialogueLine("Adrian", "Neutral", "My study desk. The single-tenant apartment lease is sitting here. Resident name: Adrian.")
                },
                new List<DialogueLine> {
                    new DialogueLine("Adrian", "Scared", "The lease agreement has been scratched out with deep gouges. Beneath my name, carved into the wood: 'YOU NEVER LIVED ALONE'.")
                });

            // Doorway leading to Hallway (on right wall - user adjusted)
            CreateDoorObject(parent, "Door_To_Hallway", new Vector2(6.82f, -3.09f), RoomIds.Hallway, "Hallway", AdrianController.Facing.Right);

            // Spawn Point inside Bedroom (arriving from Hallway)
            CreateSpawnPoint(parent, "Spawn_From_Hallway", new Vector2(-5.5f, -2.2f));
        }

        private static void BuildHallwayContent(Transform parent, ApartmentRoom room)
        {
            var perm = new GameObject("FurnitureColliders");
            perm.transform.SetParent(parent, false);

            AddObstacle(perm.transform, "Col_Credenza", new Vector2(-1.5f, 0.5f), new Vector2(1.8f, 2.8f));

            CreateInteractable(parent, "Inspect_Painting", new Vector2(1.5f, 0.5f), room,
                new List<DialogueLine> {
                    new DialogueLine("Adrian", "Neutral", "A landscape painting in a dark wooden frame. Calm misty pine trees and distant hills.")
                },
                new List<DialogueLine> {
                    new DialogueLine("Adrian", "Scared", "THE PAINTING CHANGED! Two tall, dark figures are standing between the trees! They weren't there before!"),
                    new DialogueLine("Adrian", "Scared", "And dark liquid is dripping down the wallpaper beneath the frame...")
                });

            // Doors in Hallway: to Bedroom, Bathroom, Living Room + Front Door
            // Door to Bedroom
            CreateDoorObject(parent, "Door_To_Bedroom", new Vector2(-6.42f, 0.77f), RoomIds.Bedroom, "Bedroom", AdrianController.Facing.Left);
            CreateSpawnPoint(parent, "Spawn_From_Bedroom", new Vector2(-6.12f, -0.74f));

            // Door to Living Room
            CreateDoorObject(parent, "Door_To_LivingRoom", new Vector2(0.01f, -3.92f), RoomIds.LivingRoom, "Living Room", AdrianController.Facing.Down);
            CreateSpawnPoint(parent, "Spawn_From_LivingRoom", new Vector2(-0.05f, -3.8f));

            // Door to Bathroom
            CreateDoorObject(parent, "Door_To_Bathroom", new Vector2(6.61f, 0.6f), RoomIds.Bathroom, "Bathroom", AdrianController.Facing.Right);
            CreateSpawnPoint(parent, "Spawn_From_Bathroom", new Vector2(5.84f, 0.9f));

            // Front Exit Door (Bottom) - Locked
            CreateDoorObject(parent, "Door_FrontExit", new Vector2(0f, -3.8f), "", "Apartment Exit", AdrianController.Facing.Down, true,
                "The heavy deadbolt and security chain are secured from the inside. I can't leave this apartment right now.");
        }

        private static void BuildLivingRoomContent(Transform parent, ApartmentRoom room)
        {
            var perm = new GameObject("FurnitureColliders");
            perm.transform.SetParent(parent, false);

            AddObstacle(perm.transform, "Col_Sofa", new Vector2(-0.8f, 1.8f), new Vector2(4.5f, 2.0f));
            AddObstacle(perm.transform, "Col_CoffeeTable", new Vector2(-0.5f, -1.0f), new Vector2(3.6f, 1.6f));
            AddObstacle(perm.transform, "Col_Armchair", new Vector2(3.5f, -1.0f), new Vector2(1.8f, 1.8f));
            AddObstacle(perm.transform, "Col_TVConsole", new Vector2(-1.0f, -3.8f), new Vector2(3.8f, 0.8f));
            AddObstacle(perm.transform, "Col_Bookshelf", new Vector2(4.8f, 2.2f), new Vector2(2.0f, 3.0f));

            CreateInteractable(parent, "Inspect_CoffeeTable", new Vector2(-0.5f, -1.0f), room,
                new List<DialogueLine> {
                    new DialogueLine("Adrian", "Neutral", "A framed photograph sits on the coffee table. Two people laughing in front of the window. I recognize myself on the left..."),
                    new DialogueLine("Adrian", "Neutral", "Who is the other person? I don't remember taking this photo.")
                },
                new List<DialogueLine> {
                    new DialogueLine("Adrian", "Scared", "LOOK AT THE COFFEE TABLE! The photograph is gone! In its place is an old tarnished brass key!"),
                    new DialogueLine("Adrian", "Scared", "And there's a jagged crack running down the wall behind the lamp! Who was in here?!")
                });

            CreateInteractable(parent, "Inspect_Bookshelf", new Vector2(4.8f, 2.2f), room,
                new List<DialogueLine> {
                    new DialogueLine("Adrian", "Neutral", "Rows of college textbooks and worn paperbacks. Covered in fine dust.")
                },
                new List<DialogueLine> {
                    new DialogueLine("Adrian", "Nervous", "Several heavy encyclopedias have been pulled halfway off the shelf, pointing directly toward the hallway.")
                });

            // Doorway leading to Hallway
            CreateDoorObject(parent, "Door_To_Hallway", new Vector2(-5.97f, 2.42f), RoomIds.Hallway, "Hallway", AdrianController.Facing.Left);
            CreateSpawnPoint(parent, "Spawn_From_Hallway", new Vector2(-5.74f, 1.25f));

            // Doorway leading to Kitchen (moved from Hallway to Living Room)
            CreateDoorObject(parent, "Door_To_Kitchen", new Vector2(-0.04f, -3.8f), RoomIds.Kitchen, "Kitchen", AdrianController.Facing.Down);
            CreateSpawnPoint(parent, "Spawn_From_Kitchen", new Vector2(-0.04f, -2.8f));
        }

        private static void BuildKitchenContent(Transform parent, ApartmentRoom room)
        {
            var perm = new GameObject("FurnitureColliders");
            perm.transform.SetParent(parent, false);

            AddObstacle(perm.transform, "Col_Counters", new Vector2(-1.0f, 2.5f), new Vector2(7.5f, 1.8f));
            AddObstacle(perm.transform, "Col_Fridge", new Vector2(-4.8f, 2.0f), new Vector2(2.0f, 2.5f));
            AddObstacle(perm.transform, "Col_DiningTable", new Vector2(-1.0f, -2.0f), new Vector2(4.0f, 2.6f));

            CreateInteractable(parent, "Inspect_DiningTable", new Vector2(-1.0f, -2.0f), room,
                new List<DialogueLine> {
                    new DialogueLine("Adrian", "Neutral", "A wooden dining table with a potted fern and my single coffee mug from yesterday. Cold and untouched.")
                },
                new List<DialogueLine> {
                    new DialogueLine("Adrian", "Scared", "WHAT?! There are TWO coffee mugs on the dining table now! The second mug is steaming hot!"),
                    new DialogueLine("Adrian", "Scared", "And look at the floor by the doorway... a dark, wet footprint! I live alone!")
                });

            CreateInteractable(parent, "Inspect_Fridge", new Vector2(-4.8f, 2.0f), room,
                new List<DialogueLine> {
                    new DialogueLine("Adrian", "Neutral", "A humming white refrigerator covered in magnets and grocery reminders.")
                },
                new List<DialogueLine> {
                    new DialogueLine("Adrian", "Nervous", "There's a damp note stuck to the freezer door. The handwriting is shaky: 'YOU NEVER LIVED ALONE'.")
                });

            // Doorway leading to Living Room (replaces Hallway door)
            CreateDoorObject(parent, "Door_To_LivingRoom", new Vector2(6.6f, 2.2f), RoomIds.LivingRoom, "Living Room", AdrianController.Facing.Up);
            CreateSpawnPoint(parent, "Spawn_From_LivingRoom", new Vector2(5.5f, 2.2f));
        }

        private static void BuildBathroomContent(Transform parent, ApartmentRoom room)
        {
            var perm = new GameObject("FurnitureColliders");
            perm.transform.SetParent(parent, false);

            AddObstacle(perm.transform, "Col_Bathtub", new Vector2(-5.5f, -1.0f), new Vector2(2.2f, 5.5f));
            AddObstacle(perm.transform, "Col_Vanity", new Vector2(0.5f, 1.8f), new Vector2(4.0f, 2.0f));
            AddObstacle(perm.transform, "Col_Toilet", new Vector2(4.8f, 1.5f), new Vector2(1.8f, 2.5f));

            CreateInteractable(parent, "Inspect_Mirror", new Vector2(0.5f, 1.8f), room,
                new List<DialogueLine> {
                    new DialogueLine("Adrian", "Exhausted", "My own tired face in the clean bathroom mirror. Dark circles under my eyes. I can never sleep past 03:17.")
                },
                new List<DialogueLine> {
                    new DialogueLine("Adrian", "Scared", "THE MIRROR! It's covered in hot steam, and there is a wet handprint sliding down the inside of the glass!"),
                    new DialogueLine("Adrian", "Scared", "The sink water isn't even running! And there are footprints on the tiles leading toward the door!")
                });

            CreateInteractable(parent, "Inspect_Bathtub", new Vector2(-5.5f, -1.0f), room,
                new List<DialogueLine> {
                    new DialogueLine("Adrian", "Neutral", "A clean porcelain bathtub. The shower curtain is pulled back.")
                },
                new List<DialogueLine> {
                    new DialogueLine("Adrian", "Scared", "The shower curtain has been drawn shut. The fabric is dripping wet.")
                });

            // Doorway leading to Hallway
            CreateDoorObject(parent, "Door_To_Hallway", new Vector2(6.62f, -2.63f), RoomIds.Hallway, "Hallway", AdrianController.Facing.Right);
            CreateSpawnPoint(parent, "Spawn_From_Hallway", new Vector2(6.04f, -2.62f));
        }

        private static DoorTransition CreateDoorObject(Transform parent, string name, Vector2 localPos,
            string targetRoomId, string destinationLabel, AdrianController.Facing facing, bool locked = false, string lockedReason = "")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            // Trigger collider for detection
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 1.3f;

            var dt = go.AddComponent<DoorTransition>();
            dt.Configure(targetRoomId, null, destinationLabel, null, facing, locked);
            if (locked && !string.IsNullOrEmpty(lockedReason))
            {
                dt.SetLocked(true, lockedReason);
            }

            return dt;
        }

        private static Transform CreateSpawnPoint(Transform parent, string name, Vector2 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        private static void AddObstacle(Transform parent, string name, Vector2 localPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
        }

        private static void CreateInteractable(Transform parent, string name, Vector2 localPos, ApartmentRoom room,
            List<DialogueLine> aLines, List<DialogueLine> bLines)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var pt = go.AddComponent<InteractablePoint>();
            pt.Configure(name, room, aLines, bLines, 1.8f);
        }

        // ====================================================================
        // 6. 2D LIGHTING
        // ====================================================================
        private static void BuildLighting()
        {
            var lightsRoot = new GameObject("--- LIGHTS ---");

            var globalLight = new GameObject("Global_Light_2D");
            globalLight.transform.SetParent(lightsRoot.transform, false);
            var gLight = globalLight.AddComponent<Light2D>();
            gLight.lightType = Light2D.LightType.Global;
            gLight.color = new Color(0.72f, 0.75f, 0.82f);
            gLight.intensity = 0.55f;

            // Warm point lamps in each isolated room
            CreatePointLight(lightsRoot.transform, "Light_Bedroom", PosBedroom + new Vector2(-2.5f, 1.5f), new Color(1f, 0.85f, 0.55f), 1.3f, 5.5f);
            CreatePointLight(lightsRoot.transform, "Light_Hallway", PosHallway + new Vector2(0f, 0.5f), new Color(1f, 0.88f, 0.65f), 1.1f, 5.5f);
            CreatePointLight(lightsRoot.transform, "Light_LivingRoom", PosLivingRoom + new Vector2(2.5f, 1.5f), new Color(1f, 0.88f, 0.65f), 1.2f, 5.5f);
            CreatePointLight(lightsRoot.transform, "Light_Kitchen", PosKitchen + new Vector2(-1f, 1.5f), new Color(1f, 0.9f, 0.7f), 1.1f, 5.0f);
            CreatePointLight(lightsRoot.transform, "Light_Bathroom", PosBathroom + new Vector2(0.5f, 1.8f), new Color(1f, 0.85f, 0.6f), 1.3f, 4.5f);
        }

        private static void CreatePointLight(Transform parent, string name, Vector2 pos, Color color, float intensity, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * 0.25f;
            l.falloffIntensity = 0.5f;
        }

        // ====================================================================
        // 7. PLAYER ADRIAN (ENLARGED ~0.72 SCALE)
        // ====================================================================
        private static AdrianController BuildPlayer()
        {
            var playerGo = new GameObject("Player_Adrian") { tag = "Player" };
            // Spawn in Bedroom near bed
            playerGo.transform.position = PosBedroom + new Vector2(-1.0f, -1.0f);
            // Enlarged player scale as requested: 1.0 scale gives prominent, clear presence
            playerGo.transform.localScale = new Vector3(1.0f, 1.0f, 1f);

            var sr = playerGo.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Characters";
            sr.sortingOrder = 1;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;

            var anim = playerGo.AddComponent<Animator>();
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Animations/Adrian/Adrian.controller");
            if (controller != null) anim.runtimeAnimatorController = controller;

            var rb = playerGo.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = playerGo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.6f, 0.28f);
            col.offset = new Vector2(0f, 0.15f);

            var ctrl = playerGo.AddComponent<AdrianController>();
            var speedProp = typeof(AdrianController).GetField("moveSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            speedProp?.SetValue(ctrl, 3.4f);

            return ctrl;
        }

        // ====================================================================
        // 8. CAMERA (ROOM-FOCAL FRAMING)
        // ====================================================================
        private static Camera BuildCamera(Vector2 initialRoomPos)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4.5f; // Exactly 16:9 view framing 16x9 room
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.04f); // Pitch black backdrop
            camGo.transform.position = new Vector3(initialRoomPos.x, initialRoomPos.y, -10f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UniversalAdditionalCameraData>();
            return cam;
        }

        // ====================================================================
        // 9. UI: SCREEN FADER, VN DIALOGUE BOX & TESTING HUD
        // ====================================================================
        private static void BuildUI()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var canvasGo = new GameObject("UI Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // 1. HUD Banner
            var hudGo = new GameObject("ApartmentHUD", typeof(RectTransform));
            hudGo.transform.SetParent(canvasGo.transform, false);
            SetStretch((RectTransform)hudGo.transform);

            var bannerBg = new GameObject("BannerBg", typeof(RectTransform), typeof(Image));
            bannerBg.transform.SetParent(hudGo.transform, false);
            var bgImg = bannerBg.GetComponent<Image>();
            bgImg.sprite = rounded;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.1f, 0.09f, 0.1f, 0.90f);
            SetRect(bannerBg.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1100f, 110f));

            var bannerText = CreateText(bannerBg.transform, "RoomBannerText", font, 28, Color.white,
                "CURRENT ROOM: <b>BEDROOM</b>  |  STATE: <color=#8BE9FD><b>TYPE A (NORMAL)</b></color>", TextAnchor.MiddleCenter);
            SetRect(bannerText.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(0f, 0f));

            var statesText = CreateText(bannerBg.transform, "StatesListText", font, 22, new Color(0.85f, 0.85f, 0.85f),
                "[1] Bed: A    [2] Living: A    [3] Kitchen: A    [4] Bath: A    [5] Hall: A    |    Camera: <b>Focused on Bedroom</b>", TextAnchor.MiddleCenter);
            SetRect(statesText.rectTransform, new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(0f, 0f));

            var hintText = CreateText(hudGo.transform, "HintText", font, 22, new Color(0.9f, 0.85f, 0.75f, 0.9f),
                "WASD: Move  |  E: Open Door / Inspect  |  TAB: Toggle All Rooms (A/B)  |  1-5: Toggle Room", TextAnchor.LowerCenter);
            SetRect(hintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 25f), new Vector2(1200f, 40f));

            var hud = hudGo.AddComponent<ApartmentHUD>();
            hud.Configure(bannerText, statesText, hintText);

            // 2. VN Dialogue Box
            var dialogueRoot = new GameObject("DialogueRoot", typeof(RectTransform));
            dialogueRoot.transform.SetParent(canvasGo.transform, false);
            SetStretch((RectTransform)dialogueRoot.transform);

            const float boxMarginX = 90f;
            const float boxBottom = 30f;
            const float boxHeight = 230f;
            const float portraitSpacing = 60f;
            const float portraitHeight = 560f;
            const float portraitWidth = portraitHeight * 520f / 720f;

            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(dialogueRoot.transform, false);
            var portrait = portraitGo.GetComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            SetRect(portrait.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(boxMarginX + portraitSpacing, boxBottom + boxHeight - 6f),
                new Vector2(portraitWidth, portraitHeight));

            var boxGo = new GameObject("TextBox", typeof(RectTransform), typeof(Image), typeof(Outline));
            boxGo.transform.SetParent(dialogueRoot.transform, false);
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

            var tagGo = new GameObject("NameTag", typeof(RectTransform), typeof(Image));
            tagGo.transform.SetParent(dialogueRoot.transform, false);
            var tag = tagGo.GetComponent<Image>();
            tag.sprite = rounded;
            tag.type = Image.Type.Sliced;
            tag.color = new Color(0.45f, 0.33f, 0.37f, 1f);
            SetRect(tag.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f),
                new Vector2(boxMarginX + 30f, boxBottom + boxHeight), new Vector2(230f, 54f));

            var nameText = CreateText(tagGo.transform, "NameText", font, 32, new Color(0.97f, 0.94f, 0.9f), "Adrian", TextAnchor.MiddleCenter);
            nameText.fontStyle = FontStyle.BoldAndItalic;
            SetStretch(nameText.rectTransform);

            var portraitSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/Adrian Portraits.png").OfType<Sprite>().ToDictionary(s => s.name, s => s);
            var expressions = new List<PortraitExpression>
            {
                new PortraitExpression { name = "Neutral", frames = LoadFrames(portraitSprites, "Neutral") },
                new PortraitExpression { name = "Exhausted", frames = LoadFrames(portraitSprites, "Exhausted") },
                new PortraitExpression { name = "Scared", frames = LoadFrames(portraitSprites, "Scared") },
                new PortraitExpression { name = "Nervous", frames = LoadFrames(portraitSprites, "Nervous") }
            };

            var dialogueBox = canvasGo.AddComponent<DialogueBox>();
            dialogueBox.Configure(dialogueRoot, portrait, nameText, body, indicator, expressions);

            // 3. Screen Fader Overlay (for smooth room door transitions)
            var faderGo = new GameObject("ScreenFader", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            faderGo.transform.SetParent(canvasGo.transform, false);
            SetStretch((RectTransform)faderGo.transform);

            var faderImg = faderGo.GetComponent<Image>();
            faderImg.color = Color.black;
            faderImg.raycastTarget = false;

            var faderCg = faderGo.GetComponent<CanvasGroup>();
            faderCg.alpha = 0f;
            faderCg.blocksRaycasts = false;

            var screenFader = faderGo.AddComponent<ScreenFader>();
            screenFader.Configure(faderCg, faderImg);
        }

        private static Sprite[] LoadFrames(Dictionary<string, Sprite> dict, string expr)
        {
            var list = new List<Sprite>();
            for (int i = 0; i < 4; i++)
            {
                if (dict.TryGetValue($"Adrian_{expr}_{i}", out var s))
                    list.Add(s);
            }
            return list.ToArray();
        }

        // ====================================================================
        // 10. MANAGER COORDINATOR
        // ====================================================================
        private static void BuildManager(List<ApartmentRoom> rooms, Camera cam)
        {
            var mgrGo = new GameObject("--- APARTMENT MANAGER ---");
            var mgr = mgrGo.AddComponent<ApartmentManager>();
            var noiseClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SFX/Ingame Noise.wav");
            if (noiseClip != null)
            {
                var so = new SerializedObject(mgr);
                var prop = so.FindProperty("mainIngameBacksound");
                if (prop != null)
                {
                    prop.objectReferenceValue = noiseClip;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        // ====================================================================
        // 11. WIRE DOOR TRANSITIONS
        // ====================================================================
        private static void WireDoorTransitions(List<ApartmentRoom> rooms)
        {
            var roomDict = rooms.ToDictionary(r => r.RoomId, r => r);

            ApartmentRoom bed = roomDict[RoomIds.Bedroom];
            ApartmentRoom hall = roomDict[RoomIds.Hallway];
            ApartmentRoom liv = roomDict[RoomIds.LivingRoom];
            ApartmentRoom kit = roomDict[RoomIds.Kitchen];
            ApartmentRoom bath = roomDict[RoomIds.Bathroom];

            // 1. Bedroom <-> Hallway
            var bedDoor = bed.GetComponentsInChildren<DoorTransition>(true).FirstOrDefault(d => d.TargetRoomId == RoomIds.Hallway);
            var hallDoorToBed = hall.GetComponentsInChildren<DoorTransition>(true).FirstOrDefault(d => d.TargetRoomId == RoomIds.Bedroom);
            var hallSpawnBed = hall.transform.Find("Spawn_From_Bedroom");
            var bedSpawnHall = bed.transform.Find("Spawn_From_Hallway");

            if (bedDoor != null) { bedDoor.SetTargetRoom(hall); bedDoor.SetSpawnPoint(hallSpawnBed); }
            if (hallDoorToBed != null) { hallDoorToBed.SetTargetRoom(bed); hallDoorToBed.SetSpawnPoint(bedSpawnHall); }

            // 2. Living Room <-> Hallway
            var livDoor = liv.GetComponentsInChildren<DoorTransition>(true).FirstOrDefault(d => d.TargetRoomId == RoomIds.Hallway);
            var hallDoorToLiv = hall.GetComponentsInChildren<DoorTransition>(true).FirstOrDefault(d => d.TargetRoomId == RoomIds.LivingRoom);
            var hallSpawnLiv = hall.transform.Find("Spawn_From_LivingRoom");
            var livSpawnHall = liv.transform.Find("Spawn_From_Hallway");

            if (livDoor != null) { livDoor.SetTargetRoom(hall); livDoor.SetSpawnPoint(hallSpawnLiv); }
            if (hallDoorToLiv != null) { hallDoorToLiv.SetTargetRoom(liv); hallDoorToLiv.SetSpawnPoint(livSpawnHall); }

            // 3. Kitchen <-> Living Room (Kitchen connects directly to Living Room)
            var kitDoor = kit.GetComponentsInChildren<DoorTransition>(true).FirstOrDefault(d => d.TargetRoomId == RoomIds.LivingRoom);
            var livDoorToKit = liv.GetComponentsInChildren<DoorTransition>(true).FirstOrDefault(d => d.TargetRoomId == RoomIds.Kitchen);
            var livSpawnKit = liv.transform.Find("Spawn_From_Kitchen");
            var kitSpawnLiv = kit.transform.Find("Spawn_From_LivingRoom");

            if (kitDoor != null) { kitDoor.SetTargetRoom(liv); kitDoor.SetSpawnPoint(livSpawnKit); }
            if (livDoorToKit != null) { livDoorToKit.SetTargetRoom(kit); livDoorToKit.SetSpawnPoint(kitSpawnLiv); }

            // 4. Bathroom <-> Hallway
            var bathDoor = bath.GetComponentsInChildren<DoorTransition>(true).FirstOrDefault(d => d.TargetRoomId == RoomIds.Hallway);
            var hallDoorToBath = hall.GetComponentsInChildren<DoorTransition>(true).FirstOrDefault(d => d.TargetRoomId == RoomIds.Bathroom);
            var hallSpawnBath = hall.transform.Find("Spawn_From_Bathroom");
            var bathSpawnHall = bath.transform.Find("Spawn_From_Hallway");

            if (bathDoor != null) { bathDoor.SetTargetRoom(hall); bathDoor.SetSpawnPoint(hallSpawnBath); }
            if (hallDoorToBath != null) { hallDoorToBath.SetTargetRoom(bath); hallDoorToBath.SetSpawnPoint(bathSpawnHall); }
        }

        // ====================================================================
        // HELPERS
        // ====================================================================
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
