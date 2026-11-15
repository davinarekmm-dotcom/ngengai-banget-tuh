using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Room0317.Apartment;
using Room0317.Player;
using Room0317.Story;

namespace Room0317.Chapter2
{
    /// <summary>
    /// Builds or binds the Chapter 2: The Cold Corridor environment hierarchy.
    /// Can be invoked in Edit Mode to bake rooms, PerimeterColliders, PropColliders,
    /// doors, lights, and evidence objects directly into 04_Chapter2_ColdCorridor.unity
    /// for manual Scene View editing, and bound at runtime by Chapter2Coordinator.
    /// </summary>
    public static class Chapter2RoomBuilder
    {
        public const string RootObjectName = "--- CHAPTER 2: THE COLD CORRIDOR ---";

        public static readonly Vector2 PosCorridorWest = new Vector2(0f, 0f);
        public static readonly Vector2 PosCorridorEast = new Vector2(40f, 0f);
        public static readonly Vector2 PosRoofLanding = new Vector2(80f, 0f);

        private const float RoomWidth = 16.0f;
        private const float RoomHeight = 9.0f;

        public class BuiltChapter2Environment
        {
            public GameObject Root;
            public ApartmentRoom WestRoom;
            public ApartmentRoom EastRoom;
            public ApartmentRoom RoofRoom;
            public Transform WestEntrySpawn;
            public DoorTransition RoofDoorTransition;
            public GameObject FootprintsGroup;
            public GameObject UmbrellaObject;
            public GameObject IntercomObject;
            public GameObject CompactMirrorObject;
            public GameObject FleeceJacketObject;
            public GameObject MovingBoxObject;
            public List<Light2D> CorridorLights = new List<Light2D>();
        }

        public static BuiltChapter2Environment BuildOrBind(Chapter2Coordinator coordinator, bool bindCallbacks = true)
        {
            var existingRoot = GameObject.Find(RootObjectName);
            if (existingRoot != null)
            {
                return BindExisting(existingRoot, coordinator, bindCallbacks);
            }

            return Build(coordinator, bindCallbacks);
        }

        public static BuiltChapter2Environment Build(Chapter2Coordinator coordinator, bool bindCallbacks = true)
        {
            var env = new BuiltChapter2Environment();
            env.Root = new GameObject(RootObjectName);

            // 1. Create the 3 isolated rooms (matching Chapter 1 Visual_StateA / Visual_StateB structure)
            env.WestRoom = CreateRoom(env.Root.transform, "Ch2_CorridorWest", "3rd Floor Corridor (West)", PosCorridorWest,
                ResolveSprite(coordinator != null ? coordinator.CorridorWestSprite : null, "Assets/Environment/Chapter2/ch2_env_corridor_west.png"));

            env.EastRoom = CreateRoom(env.Root.transform, "Ch2_CorridorEast", "3rd Floor Corridor (East)", PosCorridorEast,
                ResolveSprite(coordinator != null ? coordinator.CorridorEastSprite : null, "Assets/Environment/Chapter2/ch2_env_corridor_east.png"));

            env.RoofRoom = CreateRoom(env.Root.transform, "Ch2_RoofLanding", "Roof Stairwell Landing", PosRoofLanding,
                ResolveSprite(coordinator != null ? coordinator.RoofLandingSprite : null, "Assets/Environment/Chapter2/ch2_env_roof_landing.png"));

            // 2. Editable Perimeter & Prop Colliders (structured like Chapter 1)
            BuildWestColliders(env.WestRoom.transform);
            BuildEastColliders(env.EastRoom.transform);
            BuildRoofColliders(env.RoofRoom.transform);

            // 3. Lighting (Sickly amber emergency sconces + cold roof skylight)
            env.CorridorLights.Add(CreatePointLight(env.WestRoom.transform, "Light_WestSconce", new Vector2(0f, 2.3f), new Color(1f, 0.72f, 0.35f), 1.15f, 5.5f));
            env.CorridorLights.Add(CreatePointLight(env.EastRoom.transform, "Light_EastSconce", new Vector2(-4.2f, 2.1f), new Color(1f, 0.70f, 0.32f), 1.05f, 5.2f));
            env.CorridorLights.Add(CreatePointLight(env.EastRoom.transform, "Light_EastColdDraft", new Vector2(6.0f, 0.2f), new Color(0.65f, 0.78f, 0.92f), 0.75f, 4.2f));
            CreatePointLight(env.RoofRoom.transform, "Light_RoofSkylight", new Vector2(4.5f, 2.8f), new Color(0.68f, 0.80f, 0.95f), 0.95f, 6.0f);

            // 4. Spawn Points & DoorTransitions
            env.WestEntrySpawn = CreateSpawn(env.WestRoom.transform, "Spawn_FromApt317", new Vector2(-2.2f, 0.6f));
            var westFromEastSpawn = CreateSpawn(env.WestRoom.transform, "Spawn_FromEast", new Vector2(5.6f, -0.3f));
            var eastFromWestSpawn = CreateSpawn(env.EastRoom.transform, "Spawn_FromWest", new Vector2(-5.6f, -0.3f));
            var eastFromRoofSpawn = CreateSpawn(env.EastRoom.transform, "Spawn_FromRoof", new Vector2(5.2f, -0.3f));
            var roofFromEastSpawn = CreateSpawn(env.RoofRoom.transform, "Spawn_FromEast", new Vector2(-4.5f, 0.6f));

            CreateDoor(env.WestRoom.transform, "Door_ToCorridorEast", new Vector2(6.8f, -0.2f),
                "Ch2_CorridorEast", env.EastRoom, "East Corridor", eastFromWestSpawn, AdrianController.Facing.Right, false);

            CreateDoor(env.EastRoom.transform, "Door_ToCorridorWest", new Vector2(-6.8f, -0.2f),
                "Ch2_CorridorWest", env.WestRoom, "West Corridor", westFromEastSpawn, AdrianController.Facing.Left, false);

            env.RoofDoorTransition = CreateDoor(env.EastRoom.transform, "Door_ToRoofLanding", new Vector2(6.5f, -0.2f),
                "Ch2_RoofLanding", env.RoofRoom, "Roof Landing", roofFromEastSpawn, AdrianController.Facing.Right, true,
                "A frozen fleece jacket is wedged tightly in the roof door frame.");

            CreateDoor(env.RoofRoom.transform, "Door_BackToEastCorridor", new Vector2(-5.2f, 1.3f),
                "Ch2_CorridorEast", env.EastRoom, "East Corridor", eastFromRoofSpawn, AdrianController.Facing.Left, false);

            // 5. Scene 2: Non-functional Elevator & Stairwell Interactables (East Corridor)
            CreateTriggerProp(env.EastRoom.transform, "Prop_ElevatorIndicator", new Vector2(-2.1f, 1.5f), null, 1f, 0,
                "Elevator Display", 1.7f, bindCallbacks && coordinator != null ? () => coordinator.OnInspectElevator() : null);

            CreateTriggerProp(env.EastRoom.transform, "Prop_ElevatorCallButton", new Vector2(-0.6f, 1.3f), null, 1f, 0,
                "Elevator Button", 1.5f, bindCallbacks && coordinator != null ? () => coordinator.OnInspectElevator() : null);

            CreateTriggerProp(env.EastRoom.transform, "Prop_LockedStairwellDoor", new Vector2(2.8f, 1.4f), null, 1f, 0,
                "Emergency Stairwell", 1.6f, bindCallbacks && coordinator != null ? () => coordinator.OnInspectStairwell() : null);

            // 6. Evidence 1: Wet Footprint Trail (West Corridor)
            env.FootprintsGroup = BuildFootprintTrail(env.WestRoom.transform, coordinator);

            // 7. Evidence 2: Soaked Green Umbrella (West Corridor, Trash Chute Alcove)
            var umbrellaSprite = ResolveSprite(coordinator != null ? coordinator.UmbrellaSprite : null, "Assets/Objects/Chapter2/ch2_ev_umbrella_green.png");
            env.UmbrellaObject = CreateTriggerProp(env.WestRoom.transform, "Evidence_SoakedUmbrella", new Vector2(2.5f, 1.25f),
                umbrellaSprite, 0.11f, 5, "Green Umbrella", 1.5f,
                bindCallbacks && coordinator != null ? () => coordinator.OnInspectUmbrella() : null);

            // 8. Evidence 3: Wall Intercom (East Corridor)
            var intercomSprite = ResolveSprite(coordinator != null ? coordinator.IntercomSprite : null, "Assets/Objects/Chapter2/ch2_ev_wall_intercom.png");
            env.IntercomObject = CreateTriggerProp(env.EastRoom.transform, "Evidence_WallIntercom", new Vector2(0.55f, 1.65f),
                intercomSprite, 0.08f, 4, "Wall Intercom", 1.6f,
                bindCallbacks && coordinator != null ? () => coordinator.OnInspectIntercom() : null);

            // 9. Evidence 4: Cracked Silver Compact Mirror (West Corridor, near south junction box)
            var compactSprite = ResolveSprite(coordinator != null ? coordinator.CompactMirrorSprite : null, "Assets/Objects/Chapter2/ch2_ev_compact_mirror.png");
            env.CompactMirrorObject = CreateTriggerProp(env.WestRoom.transform, "Evidence_CrackedCompact", new Vector2(-3.2f, -2.0f),
                compactSprite, 0.09f, 5, "Compact Mirror", 1.5f,
                bindCallbacks && coordinator != null ? () => coordinator.OnInspectCompactMirror() : null);

            // 10. Evidence 5: Frozen Fleece Jacket (East Corridor, wedged in roof access door)
            var jacketSprite = ResolveSprite(coordinator != null ? coordinator.FleeceJacketSprite : null, "Assets/Objects/Chapter2/ch2_ev_fleece_jacket.png");
            env.FleeceJacketObject = CreateTriggerProp(env.EastRoom.transform, "Evidence_FrozenJacket", new Vector2(5.9f, -0.35f),
                jacketSprite, 0.11f, 6, "Fleece Jacket", 1.6f,
                bindCallbacks && coordinator != null ? () => coordinator.OnInspectFleeceJacket() : null);

            // 11. Evidence 6: Clara's Moving Box (Roof Landing, top step)
            var boxSprite = ResolveSprite(coordinator != null ? coordinator.MovingBoxSprite : null, "Assets/Objects/Chapter2/ch2_ev_moving_box.png");
            env.MovingBoxObject = CreateTriggerProp(env.RoofRoom.transform, "Evidence_ClaraMovingBox", new Vector2(2.6f, 0.65f),
                boxSprite, 0.13f, 6, "Moving Box", 1.8f,
                bindCallbacks && coordinator != null ? () => coordinator.OnInspectMovingBox() : null);

            return env;
        }

        public static BuiltChapter2Environment BindExisting(GameObject root, Chapter2Coordinator coordinator, bool bindCallbacks = true)
        {
            var env = new BuiltChapter2Environment { Root = root };

            var westTf = root.transform.Find("Ch2_CorridorWest");
            var eastTf = root.transform.Find("Ch2_CorridorEast");
            var roofTf = root.transform.Find("Ch2_RoofLanding");

            env.WestRoom = westTf != null ? westTf.GetComponent<ApartmentRoom>() : null;
            env.EastRoom = eastTf != null ? eastTf.GetComponent<ApartmentRoom>() : null;
            env.RoofRoom = roofTf != null ? roofTf.GetComponent<ApartmentRoom>() : null;

            if (westTf != null)
            {
                env.WestEntrySpawn = westTf.Find("Spawn_FromApt317");
                env.FootprintsGroup = westTf.Find("Evidence_WetFootprints")?.gameObject;
                env.UmbrellaObject = westTf.Find("Evidence_SoakedUmbrella")?.gameObject;
                env.CompactMirrorObject = westTf.Find("Evidence_CrackedCompact")?.gameObject;
                AddRoomLights(westTf, env.CorridorLights);
            }

            if (eastTf != null)
            {
                env.RoofDoorTransition = eastTf.Find("Door_ToRoofLanding")?.GetComponent<DoorTransition>();
                env.IntercomObject = eastTf.Find("Evidence_WallIntercom")?.gameObject;
                env.FleeceJacketObject = eastTf.Find("Evidence_FrozenJacket")?.gameObject;
                AddRoomLights(eastTf, env.CorridorLights);
            }

            if (roofTf != null)
            {
                env.MovingBoxObject = roofTf.Find("Evidence_ClaraMovingBox")?.gameObject;
            }

            if (bindCallbacks && coordinator != null)
            {
                BindTrigger(eastTf, "Prop_ElevatorIndicator", "Elevator Display", 1.7f, () => coordinator.OnInspectElevator());
                BindTrigger(eastTf, "Prop_ElevatorCallButton", "Elevator Button", 1.5f, () => coordinator.OnInspectElevator());
                BindTrigger(eastTf, "Prop_LockedStairwellDoor", "Emergency Stairwell", 1.6f, () => coordinator.OnInspectStairwell());
                BindTrigger(westTf, "Evidence_SoakedUmbrella", "Green Umbrella", 1.5f, () => coordinator.OnInspectUmbrella());
                BindTrigger(eastTf, "Evidence_WallIntercom", "Wall Intercom", 1.6f, () => coordinator.OnInspectIntercom());
                BindTrigger(westTf, "Evidence_CrackedCompact", "Compact Mirror", 1.5f, () => coordinator.OnInspectCompactMirror());
                BindTrigger(eastTf, "Evidence_FrozenJacket", "Fleece Jacket", 1.6f, () => coordinator.OnInspectFleeceJacket());
                BindTrigger(roofTf, "Evidence_ClaraMovingBox", "Moving Box", 1.8f, () => coordinator.OnInspectMovingBox());
            }

            return env;
        }

        private static void AddRoomLights(Transform roomTf, List<Light2D> list)
        {
            foreach (var l in roomTf.GetComponentsInChildren<Light2D>(true))
            {
                if (l != null && l.lightType == Light2D.LightType.Point) list.Add(l);
            }
        }

        private static void BindTrigger(Transform roomTf, string childName, string displayName, float radius, System.Action callback)
        {
            if (roomTf == null) return;
            var child = roomTf.Find(childName);
            if (child == null) return;
            var trigger = child.GetComponent<StoryInteractableTrigger>() ?? child.gameObject.AddComponent<StoryInteractableTrigger>();
            trigger.Initialize(displayName, radius, callback);
        }

        private static ApartmentRoom CreateRoom(Transform parent, string id, string displayName, Vector2 center, Sprite bgSprite)
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            go.transform.position = center;

            var visAGo = new GameObject("Visual_StateA");
            visAGo.transform.SetParent(go.transform, false);
            var srA = visAGo.AddComponent<SpriteRenderer>();
            srA.sprite = bgSprite;
            srA.sortingLayerName = "Background";
            srA.sortingOrder = 1;

            var visBGo = new GameObject("Visual_StateB");
            visBGo.transform.SetParent(go.transform, false);
            var srB = visBGo.AddComponent<SpriteRenderer>();
            srB.sprite = bgSprite;
            srB.sortingLayerName = "Background";
            srB.sortingOrder = 1;
            visBGo.SetActive(false);

            var triggerGo = new GameObject("RoomTrigger_" + id);
            triggerGo.transform.SetParent(go.transform, false);
            triggerGo.tag = "RoomTrigger";
            var box = triggerGo.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(RoomWidth - 1.5f, RoomHeight - 1.5f);
            var rt = triggerGo.AddComponent<RoomTrigger>();

            var room = go.AddComponent<ApartmentRoom>();
            room.Initialize(id, displayName, rt);
            room.RegisterVisuals(srA, srB);
            rt.Initialize(id, room);

            return room;
        }

        private static void BuildWestColliders(Transform roomTransform)
        {
            var perim = CreateGroup(roomTransform, "PerimeterColliders");
            AddColliderBox(perim, "Wall_North", new Vector2(0f, 2.4f), new Vector2(RoomWidth, 2.4f));
            AddColliderBox(perim, "Wall_South", new Vector2(0f, -RoomHeight * 0.5f + 0.55f), new Vector2(RoomWidth, 1.1f));
            AddColliderBox(perim, "Wall_West", new Vector2(-RoomWidth * 0.5f + 0.55f, 0f), new Vector2(1.1f, RoomHeight));
            AddColliderBox(perim, "Wall_East_Upper", new Vector2(RoomWidth * 0.5f - 0.35f, 2.3f), new Vector2(0.7f, 4.4f));
            AddColliderBox(perim, "Wall_East_Lower", new Vector2(RoomWidth * 0.5f - 0.35f, -2.7f), new Vector2(0.7f, 3.6f));

            var props = CreateGroup(roomTransform, "PropColliders");
            AddColliderBox(props, "Col_JunctionBox", new Vector2(-3.7f, -2.95f), new Vector2(1.2f, 0.75f));
            AddColliderBox(props, "Col_BaseboardHeater", new Vector2(3.9f, -2.95f), new Vector2(3.0f, 0.75f));
        }

        private static void BuildEastColliders(Transform roomTransform)
        {
            var perim = CreateGroup(roomTransform, "PerimeterColliders");
            AddColliderBox(perim, "Wall_North", new Vector2(0f, 2.4f), new Vector2(RoomWidth, 2.4f));
            AddColliderBox(perim, "Wall_South", new Vector2(0f, -RoomHeight * 0.5f + 0.55f), new Vector2(RoomWidth, 1.1f));
            AddColliderBox(perim, "Wall_West_Upper", new Vector2(-RoomWidth * 0.5f + 0.35f, 2.3f), new Vector2(0.7f, 4.4f));
            AddColliderBox(perim, "Wall_West_Lower", new Vector2(-RoomWidth * 0.5f + 0.35f, -2.7f), new Vector2(0.7f, 3.6f));
            AddColliderBox(perim, "Wall_East", new Vector2(RoomWidth * 0.5f - 0.55f, 0f), new Vector2(1.1f, RoomHeight));

            CreateGroup(roomTransform, "PropColliders");
        }

        private static void BuildRoofColliders(Transform roomTransform)
        {
            var perim = CreateGroup(roomTransform, "PerimeterColliders");
            AddColliderBox(perim, "Wall_North", new Vector2(0f, 2.35f), new Vector2(RoomWidth, 2.5f));
            AddColliderBox(perim, "Wall_South", new Vector2(0f, -RoomHeight * 0.5f + 0.55f), new Vector2(RoomWidth, 1.1f));
            AddColliderBox(perim, "Wall_West", new Vector2(-RoomWidth * 0.5f + 0.55f, 0f), new Vector2(1.1f, RoomHeight));
            AddColliderBox(perim, "Wall_East", new Vector2(RoomWidth * 0.5f - 0.55f, 0f), new Vector2(1.1f, RoomHeight));

            var props = CreateGroup(roomTransform, "PropColliders");
            AddColliderBox(props, "Obstacle_StairRailing", new Vector2(3.9f, -0.35f), new Vector2(4.2f, 2.8f));
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void AddColliderBox(Transform parent, string name, Vector2 localPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
        }

        private static Light2D CreatePointLight(Transform parent, string name, Vector2 localPos, Color color, float intensity, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * 0.25f;
            l.falloffIntensity = 0.55f;
            return l;
        }

        private static Transform CreateSpawn(Transform parent, string name, Vector2 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        private static DoorTransition CreateDoor(
            Transform parent, string name, Vector2 localPos,
            string targetId, ApartmentRoom targetRoom, string label,
            Transform spawnPoint, AdrianController.Facing facing, bool locked, string lockedReason = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var dt = go.AddComponent<DoorTransition>();
            dt.Configure(targetId, targetRoom, label, spawnPoint, facing, locked);
            if (locked && !string.IsNullOrEmpty(lockedReason))
            {
                dt.SetLocked(true, lockedReason);
            }
            return dt;
        }

        private static GameObject CreateTriggerProp(
            Transform parent, string name, Vector2 localPos,
            Sprite sprite, float scale, int sortingOrder,
            string displayName, float interactRadius, System.Action onInteract)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(scale, scale, 1f);

            if (sprite != null)
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingLayerName = "Props_Back";
                sr.sortingOrder = sortingOrder;
            }

            var trigger = go.AddComponent<StoryInteractableTrigger>();
            if (onInteract != null)
            {
                trigger.Initialize(displayName, interactRadius, onInteract);
            }
            return go;
        }

        private static GameObject BuildFootprintTrail(Transform westRoom, Chapter2Coordinator coordinator)
        {
            var group = new GameObject("Evidence_WetFootprints");
            group.transform.SetParent(westRoom, false);

            var footprintSprites = ResolveFootprintSprites(coordinator);
            Vector2[] positions =
            {
                new Vector2(-2.1f, 0.55f),
                new Vector2(-1.3f, 0.25f),
                new Vector2(-0.5f, 0.45f),
                new Vector2(0.4f, 0.25f),
                new Vector2(1.3f, 0.55f),
                new Vector2(2.1f, 0.85f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var fp = new GameObject($"Decal_Footprint_{i:00}");
                fp.transform.SetParent(group.transform, false);
                fp.transform.localPosition = positions[i];
                fp.transform.localRotation = Quaternion.Euler(0f, 0f, -72f + (i % 2 == 0 ? 8f : -8f));
                fp.transform.localScale = new Vector3(0.085f, 0.085f, 1f);

                if (footprintSprites.Length > 0)
                {
                    var sr = fp.AddComponent<SpriteRenderer>();
                    sr.sprite = footprintSprites[i % footprintSprites.Length];
                    sr.sortingLayerName = "Props_Back";
                    sr.sortingOrder = 2;
                    sr.color = new Color(0.85f, 0.92f, 1f, 0.82f);
                }
            }

            return group;
        }

        private static Sprite ResolveSprite(Sprite serialized, string assetPath)
        {
            if (serialized != null) return serialized;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
#else
            return null;
#endif
        }

        private static Sprite[] ResolveFootprintSprites(Chapter2Coordinator coordinator)
        {
            if (coordinator != null && coordinator.FootprintSprites != null && coordinator.FootprintSprites.Length > 0)
            {
                return coordinator.FootprintSprites;
            }
#if UNITY_EDITOR
            var objs = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Objects/Chapter2/ch2_ev_footprints_decal.png");
            var list = new List<Sprite>();
            foreach (var o in objs)
            {
                if (o is Sprite s) list.Add(s);
            }
            return list.ToArray();
#else
            return System.Array.Empty<Sprite>();
#endif
        }
    }
}
