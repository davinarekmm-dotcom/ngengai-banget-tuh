using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Room0317.Apartment;
using Room0317.Audio;
using Room0317.Dialogue;
using Room0317.Horror;
using Room0317.Inventory;
using Room0317.Objectives;
using Room0317.Player;
using Room0317.UI;

namespace Room0317.Story
{
    public enum StoryRoomState
    {
        S0_Normal,
        S1_Altered,
        S2_Distorted
    }

    /// <summary>
    /// Master narrative controller for ROOM 03:17: The Space Between Us.
    /// Manages the story sequence, clue visibility across rooms (Bathroom Key, Kitchen Note, Second Chair),
    /// the flip-arrow picture frame mechanic to discover the hidden note last, and smooth opening sequence.
    /// </summary>
    public class StoryProgressionCoordinator : MonoBehaviour
    {
        public static StoryProgressionCoordinator Instance { get; private set; }
        public static event Action OnChapter1Completed;
        public static bool SuppressChapter1 = false;

        [Header("Progression Flags")]
        [SerializeField] private bool alarmSilenced = false;
        [SerializeField] private bool photoInitialInspected = false;
        [SerializeField] private bool initialPhotoDialoguePlayed = false;
        [SerializeField] private bool photoAlteredInspected = false;
        [SerializeField] private bool event3SmsPlayed = false;
        [SerializeField] private bool keyCollected = false;
        [SerializeField] private bool chairInspected = false;
        [SerializeField] private bool chairDialoguePlayed = false;
        [SerializeField] private bool noteCollected = false;
        [SerializeField] private bool confessionCollected = false;
        [SerializeField] private bool endingTriggered = false;

        [Header("State Tracking")]
        [SerializeField] private StoryRoomState currentLivingRoomState = StoryRoomState.S0_Normal;
        [SerializeField] private StoryRoomState pendingLivingRoomState = StoryRoomState.S0_Normal;

        [Header("Clue Sprites (Auto-Resolved if Unset)")]
        [SerializeField] private Sprite normalPhotoSprite;
        [SerializeField] private Sprite alteredPhotoSprite;
        [SerializeField] private Sprite keySprite;
        [SerializeField] private Sprite secondChairSprite;
        [SerializeField] private Sprite noteSprite;
        [SerializeField] private Sprite hiddenNoteSprite;

        // In-world Clue References
        private GameObject coffeeTableObject;
        private GameObject bathroomKeyObject;
        private GameObject secondChairObject;
        private GameObject kitchenNoteObject;
        private GameObject alarmClockObject;

        private ApartmentRoom livingRoomRef;
        private ApartmentRoom hallwayRef;
        private ApartmentRoom kitchenRef;
        private ApartmentRoom bathroomRef;
        private ApartmentRoom bedroomRef;

        private AdrianController player;
        private DialogueBox dialogueBox;
        private ScreenFader screenFader;
        private bool hasInitialized = false;

        public StoryRoomState CurrentState => currentLivingRoomState;
        public bool IsPhotoInitialInspected => photoInitialInspected;
        public bool IsInitialPhotoDialoguePlayed => initialPhotoDialoguePlayed;
        public bool IsPhotoAlteredInspected => photoAlteredInspected;
        public bool IsKeyCollected => keyCollected;
        public bool IsChairInspected => chairInspected;
        public bool IsNoteCollected => noteCollected;
        public bool IsConfessionCollected => confessionCollected;
        public bool IsEndingTriggered => endingTriggered;
        public bool IsStorySequenceActive { get; private set; }

        private void BeginStorySequence()
        {
            IsStorySequenceActive = true;
            if (player == null) player = FindFirstObjectByType<AdrianController>();
            if (player != null) player.SetCanMove(false);
        }

        private void EndStorySequence()
        {
            IsStorySequenceActive = false;
            if (player == null) player = FindFirstObjectByType<AdrianController>();
            if (player != null && !endingTriggered) player.SetCanMove(true);
        }

        private bool CanStartObjectInteraction()
        {
            return !InteractionBlocker.IsBlocked(dialogueBox);
        }

        private void Awake()
        {
            if (SuppressChapter1)
            {
                enabled = false;
                return;
            }

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Immediately freeze player and start in blackness
            player = FindFirstObjectByType<AdrianController>();
            if (player != null) player.SetCanMove(false);

            screenFader = FindFirstObjectByType<ScreenFader>();
            if (screenFader != null) screenFader.SetInstantBlack();

            // Disable all legacy IntroAwakening instances to prevent duplicate auto-playing monologues
            var oldIntros = FindObjectsByType<IntroAwakening>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var i in oldIntros) i.enabled = false;

            // Disable and clean up fallback ClueSpawnCoordinator to prevent duplicate spawned prefabs
            var clueCoord = FindFirstObjectByType<ClueSpawnCoordinator>();
            if (clueCoord != null) clueCoord.CoordinatorEnabled = false;

            // Thorough cleanup of any stray/legacy notes from early prototypes
            var strayNote = GameObject.Find("Item_HiddenNote");
            if (strayNote != null) Destroy(strayNote);
            var strayDiary = GameObject.Find("Item_TornDiaryPage");
            if (strayDiary != null) Destroy(strayDiary);
            var legacyBedNote = GameObject.Find("Clue_BedroomNote");
            if (legacyBedNote != null) Destroy(legacyBedNote);
            var legacyHandwritten = GameObject.Find("Clue_HandwrittenNote");
            if (legacyHandwritten != null) Destroy(legacyHandwritten);

            var allItems = FindObjectsByType<CollectibleItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var item in allItems)
            {
                if (item == null) continue;
                string itemName = item.gameObject.name;
                string itemId = (item.ItemData != null) ? item.ItemData.id : "";
                if (itemName.Contains("Diary") || itemName.Contains("Torn") || itemId == "note_diary" || itemId == "note_hidden")
                {
                    Destroy(item.gameObject);
                }
            }
        }

        private void Start()
        {
            if (SuppressChapter1)
            {
                enabled = false;
                return;
            }
            StartCoroutine(InitializeStoryRoutine());
        }

        private IEnumerator InitializeStoryRoutine()
        {
            yield return null; // Wait 1 frame for all systems to initialize

            player = FindFirstObjectByType<AdrianController>();
            dialogueBox = FindFirstObjectByType<DialogueBox>();
            screenFader = FindFirstObjectByType<ScreenFader>();

            // Freeze player immediately during startup
            if (player != null)
            {
                player.SetCanMove(false);
            }

            // Disable standard intro awakening monologue to prioritize Event 1 Opening Message
            var introAwakening = FindFirstObjectByType<IntroAwakening>();
            if (introAwakening != null)
            {
                introAwakening.SkipIntro = true;
            }

            // Disable fallback clue spawning to prevent duplication
            var clueCoord = FindFirstObjectByType<ClueSpawnCoordinator>();
            if (clueCoord != null)
            {
                clueCoord.CoordinatorEnabled = false;
            }

            ResolveRoomReferences();
            LoadSpritesFallback();
            SetupSceneClueObjects();
            ConfigureGddObjectives();

            // Subscribe to door transitions and room changes
            ChangingRoomManager.OnDoorwayTransitioning += HandleDoorwayTransition;
            ApartmentManager.OnCurrentRoomChanged += HandleCurrentRoomChanged;

            // Apply S0 state initially: all collected clue objects are disabled
            ApplyLivingRoomState(StoryRoomState.S0_Normal);

            // Execute Event 1: Smooth Awakening and Opening Message
            yield return StartCoroutine(PlaySmoothOpeningSequence());

            hasInitialized = true;
        }

        private void OnDestroy()
        {
            ChangingRoomManager.OnDoorwayTransitioning -= HandleDoorwayTransition;
            ApartmentManager.OnCurrentRoomChanged -= HandleCurrentRoomChanged;
        }

        private void ResolveRoomReferences()
        {
            var rooms = FindObjectsByType<ApartmentRoom>(FindObjectsSortMode.None);
            foreach (var r in rooms)
            {
                if (r.RoomId == RoomIds.LivingRoom) livingRoomRef = r;
                else if (r.RoomId == RoomIds.Hallway) hallwayRef = r;
                else if (r.RoomId == RoomIds.Kitchen) kitchenRef = r;
                else if (r.RoomId == RoomIds.Bathroom) bathroomRef = r;
                else if (r.RoomId == RoomIds.Bedroom) bedroomRef = r;
            }

            // Ensure the bedroom is completely clean: no stray notes, pages, or diary items
            if (bedroomRef != null)
            {
                var bedroomItems = bedroomRef.GetComponentsInChildren<CollectibleItem>(true);
                foreach (var bItem in bedroomItems)
                {
                    Destroy(bItem.gameObject);
                }
                for (int i = bedroomRef.transform.childCount - 1; i >= 0; i--)
                {
                    var child = bedroomRef.transform.GetChild(i);
                    if (child.name.Contains("Note") || child.name.Contains("Diary") || child.name.Contains("Page"))
                    {
                        Destroy(child.gameObject);
                    }
                }
            }
        }

        public static Sprite ResolveObjectSprite(string relativePath, string subAssetName = null)
        {
#if UNITY_EDITOR
            try
            {
                var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(relativePath);
                if (assets != null && assets.Length > 0)
                {
                    if (!string.IsNullOrEmpty(subAssetName))
                    {
                        foreach (var a in assets)
                        {
                            if (a is Sprite spr && spr.name.Equals(subAssetName, StringComparison.OrdinalIgnoreCase))
                                return spr;
                        }
                    }
                    foreach (var a in assets)
                    {
                        if (a is Sprite spr) return spr;
                    }
                }
            }
            catch { }
#endif
            // 2. Try Resources.Load
            try
            {
                string baseName = System.IO.Path.GetFileNameWithoutExtension(relativePath);
                var resSprites = Resources.LoadAll<Sprite>("Objects/" + baseName);
                if (resSprites != null && resSprites.Length > 0)
                {
                    if (!string.IsNullOrEmpty(subAssetName))
                    {
                        foreach (var s in resSprites)
                        {
                            if (s.name.Equals(subAssetName, StringComparison.OrdinalIgnoreCase))
                                return s;
                        }
                    }
                    return resSprites[0];
                }
            }
            catch { }

            // 3. Fallback: Load image bytes directly from disk
            try
            {
                string fullPath = System.IO.Path.Combine(Application.dataPath, "..", relativePath);
                if (System.IO.File.Exists(fullPath))
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(fullPath);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))
                    {
                        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    }
                }
            }
            catch { }

            return null;
        }

        public static Sprite ResolveClueSprite(string clueIdOrName)
        {
            if (string.IsNullOrEmpty(clueIdOrName)) return null;
            string t = clueIdOrName.ToLowerInvariant();
            if (t.Contains("key")) return ResolveObjectSprite("Assets/Objects/key.png", "key_0");
            if (t.Contains("chair")) return ResolveObjectSprite("Assets/Objects/chair.png", "chair_0");
            if (t.Contains("altered") || t.Contains("eerie")) return ResolveObjectSprite("Assets/Objects/eerie_frame.png", "eerie_frame_0");
            if (t.Contains("photo") || t.Contains("frame")) return ResolveObjectSprite("Assets/Objects/normal_frame.png", "normal_frame_1");
            if (t.Contains("hidden") || t.Contains("confession")) return ResolveObjectSprite("Assets/Objects/hidden_note.png", "hidden_note_0");
            if (t.Contains("note") || t.Contains("page") || t.Contains("diary")) return ResolveObjectSprite("Assets/Objects/note.png", "note_0");
            return null;
        }

        private void LoadSpritesFallback()
        {
            if (normalPhotoSprite == null) normalPhotoSprite = ResolveObjectSprite("Assets/Objects/normal_frame.png", "normal_frame_1");
            if (alteredPhotoSprite == null) alteredPhotoSprite = ResolveObjectSprite("Assets/Objects/eerie_frame.png", "eerie_frame_0");
            if (keySprite == null) keySprite = ResolveObjectSprite("Assets/Objects/key.png", "key_0");
            if (secondChairSprite == null) secondChairSprite = ResolveObjectSprite("Assets/Objects/chair.png", "chair_0");
            if (noteSprite == null) noteSprite = ResolveObjectSprite("Assets/Objects/note.png", "note_0");
            if (hiddenNoteSprite == null) hiddenNoteSprite = ResolveObjectSprite("Assets/Objects/hidden_note.png", "hidden_note_0");

            if (hiddenNoteSprite == null) hiddenNoteSprite = noteSprite;
        }

        // ====================================================================
        // GDD OBJECTIVES CONFIGURATION
        // ====================================================================
        private void ConfigureGddObjectives()
        {
            if (ObjectiveManager.Instance == null) return;

            var gddObjectives = new List<ObjectiveData>
            {
                new ObjectiveData("obj_00_silence_alarm", "Silence the bedside alarm clock.", "A piercing electronic buzzing is echoing from the bedside table.", "Press [E] facing the alarm clock on the nightstand.", "03:17"),
                new ObjectiveData("obj_01_enter_living", "Enter the living room and investigate.", "Walk through the hallway into the living room.", "Follow the hallway toward the living room.", "03:17"),
                new ObjectiveData("obj_02_inspect_photo", "Inspect the coffee table.", "Examine the wooden coffee table in the center of the living room.", "Press [E] facing the coffee table.", "03:17"),
                new ObjectiveData("obj_03_leave_return_s1", "Leave the room and return.", "Step out through the doorway into the hallway, then turn back.", "Exit the living room.", "03:17"),
                new ObjectiveData("obj_04_inspect_changed_photo", "Inspect the changed photograph on the table.", "Examine the picture on the coffee table once more.", "Look at the photo on the coffee table.", "03:17"),
                new ObjectiveData("obj_05_take_spare_key", "Search the apartment for the old spare key.", "Look through other rooms in the apartment. Perhaps the bathroom?", "Check the bathroom counter.", "03:17"),
                new ObjectiveData("obj_06_leave_return_s2", "Investigate the kitchen.", "With the spare key found, head into the kitchen to search for more clues.", "Walk through the hallway into the kitchen.", "03:17"),
                new ObjectiveData("obj_07_inspect_second_chair", "Inspect the second chair in the kitchen.", "A wooden chair has appeared beside the dining table in the kitchen.", "Examine the wooden chair on the right side of the kitchen table.", "03:17"),
                new ObjectiveData("obj_08_search_kitchen", "Search the kitchen for the handwritten note.", "Search the kitchen counter for further clues.", "Look closely at the kitchen counter.", "03:17"),
                new ObjectiveData("obj_09_read_note", "Take and read the note in the kitchen.", "Read the folded paper left on the kitchen counter.", "Press [E] to read the note on the kitchen counter.", "03:17"),
                new ObjectiveData("obj_10_examine_photo_back", "Examine behind the photograph on the living room table.", "The note mentioned looking behind the photograph. Return to the table.", "Inspect the coffee table and turn over the frame.", "03:17"),
                new ObjectiveData("obj_11_read_confession", "Take and read the hidden confession.", "Turn over the picture frame to reveal the note taped behind it.", "Press [F] or click Turn Over on the photograph.", "03:17"),
                new ObjectiveData("obj_12_discover_truth", "Discover the truth about The Roommate.", "The memories have returned. Uncover what really happened.", "Listen to the apartment.", "03:17")
            };

            ObjectiveManager.Instance.ConfigureObjectives(gddObjectives);

            if (ApartmentClock.Instance != null)
            {
                ApartmentClock.Instance.SetTimeString("03:17");
            }
        }

        // ====================================================================
        // EVENT 1 — SMOOTH AWAKENING & INTERACTIVE OPENING MESSAGE
        // ====================================================================
        private IEnumerator PlaySmoothOpeningSequence()
        {
            BeginStorySequence();
            if (screenFader != null) screenFader.SetInstantBlack();

            // 1. Initial dark silence with faint heartbeat
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetHeartbeatIntensity(0.35f);
            }

            yield return new WaitForSeconds(0.8f);

            // 2. Eye-Blink Awakening Animation (Fade in -> flutter shut -> open fully)
            if (screenFader != null)
            {
                yield return screenFader.FadeIn(0.65f);
                yield return new WaitForSeconds(0.18f);
                yield return screenFader.FadeOut(0.25f);
                yield return new WaitForSeconds(0.12f);
                yield return screenFader.FadeIn(0.85f);
            }
            else
            {
                yield return new WaitForSeconds(1.0f);
            }

            // Blinking animation is now COMPLETELY finished
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetHeartbeatIntensity(0.04f);
                AudioManager.Instance.StartAlarmBuzzing();
            }

            if (ApartmentClock.Instance != null)
            {
                ApartmentClock.Instance.SetTimeString("03:17");
            }

            // 3. Brief stillness before Adrian speaks to himself
            yield return new WaitForSeconds(1.2f);

            // 4. Initial monologue: Adrian speaking to himself about waking up and the buzzing alarm
            if (dialogueBox != null)
            {
                bool finished = false;
                Action onDone = null;
                onDone = () =>
                {
                    finished = true;
                    dialogueBox.OnDialogueFinished -= onDone;
                };
                dialogueBox.OnDialogueFinished += onDone;

                dialogueBox.StartDialogue(new List<DialogueLine>
                {
                    new DialogueLine("Adrian", "Exhausted", "...Cold. Why is it always so freezing in here?"),
                    new DialogueLine("Adrian", "Nervous", "03:17 again. Woke up right on the second... and that piercing alarm buzzing won't stop."),
                    new DialogueLine("Adrian", "Exhausted", "I have to shut that damn clock off before my head splits.")
                });

                while (!finished && dialogueBox.IsOpen)
                {
                    yield return null;
                }
            }

            // 5. Hand over full movement controls to player so they can walk to the nightstand to silence the clock
            EndStorySequence();
        }

        // ====================================================================
        // SCENE CLUE OBJECTS SETUP
        // ====================================================================
        private void SetupSceneClueObjects()
        {
            // 0. Bedside Alarm Clock Interaction in Room_Bedroom
            alarmClockObject = GameObject.Find("Inspect_Nightstand_Clock");
            if (alarmClockObject == null && bedroomRef != null)
            {
                alarmClockObject = new GameObject("Inspect_Nightstand_Clock");
                alarmClockObject.transform.SetParent(bedroomRef.transform, false);
                alarmClockObject.transform.localPosition = new Vector3(-4.67f, -2.0f, 0f);
            }

            if (alarmClockObject != null)
            {
                var pt = alarmClockObject.GetComponent<InteractablePoint>();
                if (pt != null) pt.enabled = false;

                var trigger = alarmClockObject.GetComponent<StoryInteractableTrigger>();
                if (trigger == null) trigger = alarmClockObject.AddComponent<StoryInteractableTrigger>();
                trigger.Initialize("Alarm Clock", 2.2f, OnAlarmClockInteracted);
            }

            // 1. Coffee Table Interaction (NO world sprite on table!)
            coffeeTableObject = GameObject.Find("Inspect_CoffeeTable");
            if (coffeeTableObject == null && livingRoomRef != null)
            {
                coffeeTableObject = new GameObject("Inspect_CoffeeTable");
                coffeeTableObject.transform.SetParent(livingRoomRef.transform, false);
                coffeeTableObject.transform.localPosition = new Vector3(-0.34f, -0.84f, 0f);
            }

            if (coffeeTableObject != null)
            {
                // Remove any existing SpriteRenderer so the coffee table remains clean and uncluttered
                var sr = coffeeTableObject.GetComponent<SpriteRenderer>();
                if (sr != null) Destroy(sr);

                var visual = coffeeTableObject.transform.Find("PhotoFrameVisual");
                if (visual != null) Destroy(visual.gameObject);

                var pt = coffeeTableObject.GetComponent<InteractablePoint>();
                if (pt != null) pt.enabled = false;

                var trigger = coffeeTableObject.GetComponent<StoryInteractableTrigger>();
                if (trigger == null) trigger = coffeeTableObject.AddComponent<StoryInteractableTrigger>();
                trigger.Initialize("Coffee Table", 1.8f, OnCoffeeTableInteracted);
            }

            // Remove any stray enlarged photograph or frame sprites in the living room
            if (livingRoomRef != null)
            {
                var srs = livingRoomRef.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var s in srs)
                {
                    if (s == null || s.gameObject == livingRoomRef.gameObject) continue;
                    if (s.name == "Visual_StateA" || s.name == "Visual_StateB") continue;
                    if (s.sprite != null && (s.sprite.name.Contains("frame") || s.sprite.name.Contains("photo")))
                    {
                        Destroy(s);
                    }
                }
            }

            // 2. Old Spare Key in the BATHROOM (Placed directly on the bathroom sink counter)
            Transform bathroomParent = bathroomRef != null ? bathroomRef.transform : transform;
            bathroomKeyObject = GameObject.Find("Clue_BathroomKey");
            if (bathroomKeyObject == null)
            {
                bathroomKeyObject = new GameObject("Clue_BathroomKey");
                bathroomKeyObject.transform.SetParent(bathroomParent, false);
            }
            else
            {
                bathroomKeyObject.transform.SetParent(bathroomParent, false);
            }

            // Precisely position on the bathroom sink counter directly in front of the player standing spot
            // (Note: Room_Bathroom has 180 deg Y-rotation, so local -X corresponds to world right)
            bathroomKeyObject.transform.localPosition = new Vector3(-1.35f, 0.65f, 0f);
            bathroomKeyObject.transform.localScale = Vector3.one * 0.065f; // Small, realistic key scale (0.7 world units)

            var keySr = bathroomKeyObject.GetComponent<SpriteRenderer>();
            if (keySr == null) keySr = bathroomKeyObject.AddComponent<SpriteRenderer>();
            keySr.sprite = keySprite != null ? keySprite : ResolveClueSprite("key");
            keySr.sortingLayerName = "Props_Back";
            keySr.sortingOrder = 10;

            var keyTrigger = bathroomKeyObject.GetComponent<StoryInteractableTrigger>();
            if (keyTrigger == null) keyTrigger = bathroomKeyObject.AddComponent<StoryInteractableTrigger>();
            // Generous 2.4m interaction radius so Adrian standing in front of the vanity counter easily reaches it
            keyTrigger.Initialize("Old Spare Key", 2.4f, OnBathroomKeyInteracted);

            // 3. Second Chair in the KITCHEN (Invisible interaction trigger on the right side of the dining table)
            // Note: The second chair is part of the eerie room background art (eerie_kitchen.png).
            // It does not have an in-world SpriteRenderer. Interacting with the chair area pops up the inspection modal like the portrait.
            Transform kitchenParent = kitchenRef != null ? kitchenRef.transform : transform;
            secondChairObject = GameObject.Find("Clue_SecondChair");
            if (secondChairObject == null)
            {
                secondChairObject = new GameObject("Clue_SecondChair");
                secondChairObject.transform.SetParent(kitchenParent, false);
            }
            else
            {
                secondChairObject.transform.SetParent(kitchenParent, false);
            }

            // Remove any SpriteRenderer so no physical chair sprite is drawn in the world
            var chairSrs = secondChairObject.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in chairSrs)
            {
                if (sr != null) Destroy(sr);
            }

            // Clean up any stray duplicate chair sprites in the kitchen
            if (kitchenRef != null)
            {
                var srs = kitchenRef.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var s in srs)
                {
                    if (s == null || s.gameObject == kitchenRef.gameObject) continue;
                    if (s.name == "Visual_StateA" || s.name == "Visual_StateB") continue;
                    if (s.sprite != null && s.sprite.name.ToLower().Contains("chair"))
                    {
                        Destroy(s.gameObject);
                    }
                }
            }

            // Placed neatly at the right chair of the dining table in Room_Kitchen
            secondChairObject.transform.localPosition = new Vector3(0.55f, -1.85f, 0f);
            secondChairObject.transform.localScale = Vector3.one;

            var chairTrigger = secondChairObject.GetComponent<StoryInteractableTrigger>();
            if (chairTrigger == null) chairTrigger = secondChairObject.AddComponent<StoryInteractableTrigger>();
            chairTrigger.Initialize("Second Chair", 2.4f, OnSecondChairInteracted);

            // 4. Handwritten Note in the KITCHEN (On kitchen counter surface to the left of the sink)
            kitchenNoteObject = GameObject.Find("Clue_KitchenNote");
            if (kitchenNoteObject == null)
            {
                kitchenNoteObject = new GameObject("Clue_KitchenNote");
                kitchenNoteObject.transform.SetParent(kitchenParent, false);
            }
            else
            {
                kitchenNoteObject.transform.SetParent(kitchenParent, false);
            }

            // Realistic note scale resting flat on the kitchen counter slab to the left side of the sink
            kitchenNoteObject.transform.localPosition = new Vector3(-2.4f, 1.75f, 0f);
            kitchenNoteObject.transform.localScale = Vector3.one * 0.035f;

            var noteSr = kitchenNoteObject.GetComponent<SpriteRenderer>();
            if (noteSr == null) noteSr = kitchenNoteObject.AddComponent<SpriteRenderer>();
            noteSr.sprite = noteSprite != null ? noteSprite : ResolveClueSprite("note");
            noteSr.sortingLayerName = "Props_Back";
            noteSr.sortingOrder = 10;

            var noteTrigger = kitchenNoteObject.GetComponent<StoryInteractableTrigger>();
            if (noteTrigger == null) noteTrigger = kitchenNoteObject.AddComponent<StoryInteractableTrigger>();
            noteTrigger.Initialize("Kitchen Note", 2.4f, OnKitchenNoteInteracted);

            // Clean up any legacy misplaced note in bedroom or living room
            var legacyNote = GameObject.Find("Clue_HandwrittenNote");
            if (legacyNote != null) Destroy(legacyNote);
            var legacyKey = GameObject.Find("Clue_OldSpareKey");
            if (legacyKey != null) Destroy(legacyKey);
            var legacyChairInLiving = livingRoomRef != null ? livingRoomRef.transform.Find("Clue_SecondChair") : null;
            if (legacyChairInLiving != null && legacyChairInLiving.gameObject != secondChairObject) Destroy(legacyChairInLiving.gameObject);
        }

        // ====================================================================
        // STATE APPLIER (S0, S1, S2): ENABLES CLUES IN THE SCENE AS STORY GOES
        // ====================================================================
        public void ApplyLivingRoomState(StoryRoomState state)
        {
            currentLivingRoomState = state;

            // Bathroom Key: Only active in S1 or S2 (if not yet collected)
            if (bathroomKeyObject != null)
            {
                bathroomKeyObject.SetActive(!keyCollected && (state == StoryRoomState.S1_Altered || state == StoryRoomState.S2_Distorted));
            }

            // Second Chair: Only active in S2 (and kitchen shifts to eerie State B)
            if (state == StoryRoomState.S2_Distorted)
            {
                if (ChangingRoomManager.Instance != null)
                {
                    ChangingRoomManager.Instance.QueueRoomChange(RoomIds.Kitchen, RoomState.B);
                }
                if (kitchenRef != null)
                {
                    kitchenRef.SetState(RoomState.B);
                }
            }

            if (secondChairObject != null)
            {
                secondChairObject.SetActive(state == StoryRoomState.S2_Distorted);
            }

            // Kitchen Note: Only active in S2 (if not yet collected)
            if (kitchenNoteObject != null)
            {
                kitchenNoteObject.SetActive(!noteCollected && state == StoryRoomState.S2_Distorted);
            }

            Debug.Log($"<color=cyan><b>[StoryProgression]</b> Apartment state updated to {state}.</color>");
        }

        // ====================================================================
        // TRANSITION HOOKS: SEAMLESS COMMITS DURING DOORWAY BLACKOUT
        // ====================================================================
        private void HandleDoorwayTransition(ApartmentRoom fromRoom, ApartmentRoom toRoom)
        {
            if (fromRoom == null || toRoom == null) return;

            // S1 Transition: Leaving Living Room into Hallway after inspecting initial photo
            if (fromRoom.RoomId == RoomIds.LivingRoom && toRoom.RoomId == RoomIds.Hallway)
            {
                if (currentLivingRoomState == StoryRoomState.S0_Normal && photoInitialInspected)
                {
                    pendingLivingRoomState = StoryRoomState.S1_Altered;
                }
            }
            // S1 Activation: Re-entering Living Room from Hallway
            else if (fromRoom.RoomId == RoomIds.Hallway && toRoom.RoomId == RoomIds.LivingRoom)
            {
                if (pendingLivingRoomState == StoryRoomState.S1_Altered && currentLivingRoomState != StoryRoomState.S1_Altered)
                {
                    ApplyLivingRoomState(StoryRoomState.S1_Altered);

                    if (GetCurrentObjectiveId() == "obj_03_leave_return_s1")
                    {
                        ObjectiveManager.Instance.AdvanceToNextObjective(); // -> obj_04_inspect_changed_photo
                    }
                }
            }

            // S2 Transition: Once the spare key is taken from the bathroom, moving through any doorway activates S2 (Distorted)
            if (keyCollected && currentLivingRoomState == StoryRoomState.S1_Altered)
            {
                pendingLivingRoomState = StoryRoomState.S2_Distorted;
                ApplyLivingRoomState(StoryRoomState.S2_Distorted);

                if (GetCurrentObjectiveId() == "obj_06_leave_return_s2")
                {
                    ObjectiveManager.Instance.AdvanceToNextObjective(); // -> obj_07_inspect_second_chair
                }
            }
        }

        private void HandleCurrentRoomChanged(ApartmentRoom room)
        {
            if (room == null || !hasInitialized) return;

            // Event 1 Completion: Player enters Living Room
            if (room.RoomId == RoomIds.LivingRoom && GetCurrentObjectiveId() == "obj_01_enter_living")
            {
                ObjectiveManager.Instance.AdvanceToNextObjective(); // -> obj_02_inspect_photo
            }

            // In S2 (or once spare key is collected), entering the Kitchen ensures it is shifted to the eerie State B
            if (room.RoomId == RoomIds.Kitchen && (currentLivingRoomState == StoryRoomState.S2_Distorted || keyCollected))
            {
                if (kitchenRef != null && kitchenRef.CurrentState != RoomState.B)
                {
                    kitchenRef.SetState(RoomState.B);
                }
            }
        }

        private string GetCurrentObjectiveId()
        {
            return (ObjectiveManager.Instance != null && ObjectiveManager.Instance.CurrentObjective != null)
                ? ObjectiveManager.Instance.CurrentObjective.Id
                : "";
        }

        private IEnumerator ShowDialogueAndWait(List<DialogueLine> lines)
        {
            if (dialogueBox == null || lines == null || lines.Count == 0) yield break;

            bool finished = false;
            Action onDone = null;
            onDone = () =>
            {
                finished = true;
                if (dialogueBox != null) dialogueBox.OnDialogueFinished -= onDone;
            };
            dialogueBox.OnDialogueFinished += onDone;
            dialogueBox.StartDialogue(lines);

            while (!finished && dialogueBox.IsOpen)
            {
                yield return null;
            }
        }

        // ====================================================================
        // EVENT 0: SILENCE ALARM CLOCK (OPENING OBJECTIVE)
        // ====================================================================
        private void OnAlarmClockInteracted()
        {
            if (!CanStartObjectInteraction()) return;

            if (alarmSilenced)
            {
                if (dialogueBox != null)
                {
                    dialogueBox.StartDialogue(new List<DialogueLine>
                    {
                        new DialogueLine("Adrian", "Neutral", "03:17 AM. The digits are frozen red. The clock is already silenced.")
                    });
                }
                return;
            }

            alarmSilenced = true;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopAlarmBuzzing();
                AudioManager.Instance.PlayNotePickup();
            }

            if (GetCurrentObjectiveId() == "obj_00_silence_alarm")
            {
                ObjectiveManager.Instance.AdvanceToNextObjective(); // -> obj_01_enter_living
            }

            BeginStorySequence();
            StartCoroutine(AlarmClockSequenceRoutine());
        }

        private IEnumerator AlarmClockSequenceRoutine()
        {
            BeginStorySequence();

            yield return ShowDialogueAndWait(new List<DialogueLine>
            {
                new DialogueLine("Adrian", "Neutral", "Finally, quiet."),
                new DialogueLine("Adrian", "Nervous", "Wait... the alarm switch is set to OFF. It wasn't even set to go off."),
                new DialogueLine("Adrian", "Scared", "Then what was making that sound?")
            });

            yield return new WaitForSeconds(0.4f);

            // Phone notification pop up with Cracked Smartphone sprite before initiating dialogue
            if (PhoneNotificationUI.Instance != null)
            {
                yield return PhoneNotificationUI.Instance.ShowNotificationRoutine(
                    "Unknown Number",
                    "You moved everything again."
                );
            }

            // Chilling first contact from the ghost
            yield return ShowDialogueAndWait(new List<DialogueLine>
            {
                new DialogueLine("Unknown Number", "Neutral", "You moved everything again."),
                new DialogueLine("Adrian", "Nervous", "Moved what? Who is this? How did you get my number?"),
                new DialogueLine("Unknown Number", "Neutral", "You took down the frame. You put away our chair. You thought if you hid the key, you could pretend I was never here."),
                new DialogueLine("Adrian", "Scared", "'Our'? ...No. That's impossible. I live alone."),
                new DialogueLine("Adrian", "Nervous", "Someone has my number and knows this apartment. If they were in here, they left a trail. Living room first.")
            });

            EndStorySequence();
        }

        // ====================================================================
        // COFFEE TABLE & FRAMED PHOTOGRAPH (WITH FLIP-ARROW MECHANIC)
        // ====================================================================
        private void OnCoffeeTableInteracted()
        {
            if (!CanStartObjectInteraction() || endingTriggered) return;

            string curObj = GetCurrentObjectiveId();

            // EVENT 6: FINAL CLUE — FLIPPING THE EERIE PHOTOGRAPH TO REVEAL THE HIDDEN NOTE
            if (noteCollected)
            {
                if (curObj == "obj_10_examine_photo_back")
                {
                    ObjectiveManager.Instance.AdvanceToNextObjective(); // -> obj_11_read_confession
                }

                if (InspectionModal.Instance != null)
                {
                    bool flipJuiceTriggered = false;
                    // Presents the eerie photograph with the flip arrow to turn it over and reveal the hidden note!
                    InspectionModal.Instance.ShowFlippable(
                        frontSpr: alteredPhotoSprite,
                        frontTtl: "Altered Photograph",
                        frontDsc: "Someone is standing beside me in the picture. Why can't I see their face?\n\n<color=#F1FA8C><i>There is a fold along the tape on the back of the frame...</i></color>",
                        backSpr: hiddenNoteSprite,
                        backTtl: "Hidden Note (Back of Photograph)",
                        backDsc: "\"To Clara.\n\nPlease forgive me.\n\nIt was raining. It was 03:17 AM. We were screaming, and I slammed the deadbolt shut.\n\nWhen I finally opened the door, the corridor was empty.\n\nI erased your room. I erased your chair. I tried to live as if you never existed, so I wouldn't have to face what I did.\n\nI'm so sorry.\"",
                        onCollect: () =>
                        {
                            confessionCollected = true;
                            if (InventoryManager.Instance != null)
                            {
                                InventoryManager.Instance.AddItem(new InventoryItem("clue_hidden_confession", "Hidden Confession", "A confession hidden behind the framed photograph.", hiddenNoteSprite, hiddenNoteSprite));
                            }
                            if (GetCurrentObjectiveId() == "obj_11_read_confession")
                            {
                                ObjectiveManager.Instance.AdvanceToNextObjective(); // -> obj_12_discover_truth
                            }
                            // Trigger Event 7 Ending Sequence
                            BeginStorySequence();
                            StartCoroutine(PlayEvent7EndingSequence());
                        },
                        onClose: null,
                        onFlipped: () =>
                        {
                            if (flipJuiceTriggered) return;
                            flipJuiceTriggered = true;
                            if (AudioManager.Instance != null) AudioManager.Instance.PlayRoomShiftSwell();
                            if (ApartmentManager.Instance != null) ApartmentManager.Instance.TriggerCameraMicroShake(0.03f, 0.22f);
                        }
                    );
                }
                return;
            }

            // EVENT 2: INITIAL PHOTOGRAPH INSPECTION (Normal Frame)
            if (currentLivingRoomState == StoryRoomState.S0_Normal)
            {
                photoInitialInspected = true;
                if (curObj == "obj_02_inspect_photo")
                {
                    ObjectiveManager.Instance.AdvanceToNextObjective(); // -> obj_03_leave_return_s1
                }

                // Log evidence item into Evidence Inventory
                if (InventoryManager.Instance != null && !InventoryManager.Instance.HasItem("clue_photo_normal"))
                {
                    InventoryManager.Instance.AddItem(new InventoryItem(
                        "clue_photo_normal",
                        "Framed Photograph",
                        "A photograph from last winter. An empty space sits beside me on the sofa, as if someone was cropped out.",
                        normalPhotoSprite,
                        normalPhotoSprite
                    ));
                }

                bool shouldPlayInitialDialogue = !initialPhotoDialoguePlayed;

                if (InspectionModal.Instance != null)
                {
                    InspectionModal.Instance.Show(
                        normalPhotoSprite,
                        "Framed Photograph",
                        "A photograph from last winter. Me standing in the living room... smiling.\n\n<color=#A0A5B5>An empty space rests beside me on the sofa. Like someone was edited out of the frame.</color>\n\n— Adrian's Thought: 'Why can't I remember who took this?'",
                        onCollect: null,
                        onClose: () =>
                        {
                            if (shouldPlayInitialDialogue && !initialPhotoDialoguePlayed)
                            {
                                initialPhotoDialoguePlayed = true;
                                BeginStorySequence();
                                StartCoroutine(PlayInitialPhotoDialogueRoutine());
                            }
                        }
                    );
                }
                return;
            }

            // EVENT 3: ALTERED PHOTOGRAPH INSPECTION (Eerie Frame)
            if (currentLivingRoomState == StoryRoomState.S1_Altered || currentLivingRoomState == StoryRoomState.S2_Distorted)
            {
                bool firstAlteredInspect = !photoAlteredInspected;
                photoAlteredInspected = true;

                if (firstAlteredInspect && AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayRoomShiftSwell();
                }

                // Log altered evidence item into Evidence Inventory
                if (InventoryManager.Instance != null && !InventoryManager.Instance.HasItem("clue_photo_altered"))
                {
                    InventoryManager.Instance.AddItem(new InventoryItem(
                        "clue_photo_altered",
                        "Altered Photograph",
                        "Someone is standing beside me. Why can't I see their face?",
                        alteredPhotoSprite,
                        alteredPhotoSprite
                    ));
                }

                bool shouldTriggerSms = !event3SmsPlayed;

                if (InspectionModal.Instance != null)
                {
                    InspectionModal.Instance.Show(
                        alteredPhotoSprite,
                        "Altered Photograph",
                        "Someone is standing beside me. Why can't I see their face?\n\n<color=#A0A5B5>The woman beside me wears a delicate silver pendant shaped like an swallow.</color>",
                        onCollect: null,
                        onClose: () =>
                        {
                            if (shouldTriggerSms && !event3SmsPlayed)
                            {
                                event3SmsPlayed = true;
                                BeginStorySequence();
                                StartCoroutine(PlayEvent3IncomingSms());
                            }
                        }
                    );
                }
            }
        }

        private IEnumerator PlayInitialPhotoDialogueRoutine()
        {
            BeginStorySequence();
            yield return new WaitForSeconds(0.2f);
            yield return ShowDialogueAndWait(new List<DialogueLine>
            {
                new DialogueLine("Adrian", "Nervous", "Someone is missing from this picture. I'd remember cutting them out... wouldn't I?"),
                new DialogueLine("Adrian", "Exhausted", "I can't think straight in here. I'll step out for a moment, then look at this table again.")
            });
            EndStorySequence();
        }

        private IEnumerator PlayEvent3IncomingSms()
        {
            BeginStorySequence();
            yield return new WaitForSeconds(0.2f);

            // Phone notification pop up with Cracked Smartphone sprite before initiating dialogue
            if (PhoneNotificationUI.Instance != null)
            {
                yield return PhoneNotificationUI.Instance.ShowNotificationRoutine(
                    "Unknown Number",
                    "You used to keep that picture right by the front door."
                );
            }

            yield return ShowDialogueAndWait(new List<DialogueLine>
            {
                new DialogueLine("Unknown Number", "Neutral", "You used to keep that picture right by the front door."),
                new DialogueLine("Adrian", "Scared", "Stop messaging me! Who is this?!"),
                new DialogueLine("Unknown Number", "Neutral", "Look closer at the photo. You gave me that silver pendant for our first winter here. Before you locked me out."),
                new DialogueLine("Adrian", "Scared", "Locked you out...? No. That's a lie. I don't know any girl with that pendant!"),
                new DialogueLine("Adrian", "Nervous", "She mentioned a key. If it's real, I hid it somewhere. If it isn't, this is a prank and I'll prove it. Bathroom first.")
            });

            if (GetCurrentObjectiveId() == "obj_04_inspect_changed_photo")
            {
                ObjectiveManager.Instance.AdvanceToNextObjective(); // -> obj_05_take_spare_key
            }

            EndStorySequence();
        }

        // ====================================================================
        // EVENT 3: BATHROOM SPARE KEY
        // ====================================================================
        private void OnBathroomKeyInteracted()
        {
            if (keyCollected || !CanStartObjectInteraction()) return;

            if (InspectionModal.Instance != null)
            {
                InspectionModal.Instance.Show(
                    keySprite,
                    "Old Spare Key",
                    "A spare key resting on the bathroom counter. There are two scratches on the handle. I remember making them... but not who I gave it to.\n\n<color=#A0A5B5>Label: \"For when you come home late.\"</color>",
                    onCollect: () =>
                    {
                        keyCollected = true;
                        if (bathroomKeyObject != null) bathroomKeyObject.SetActive(false);

                        if (AudioManager.Instance != null) AudioManager.Instance.PlayKeyPickup();

                        if (InventoryManager.Instance != null)
                        {
                            InventoryManager.Instance.AddItem(new InventoryItem("clue_spare_key", "Old Spare Key", "A scratched spare key found in the bathroom.", keySprite, keySprite));
                        }

                        if (GetCurrentObjectiveId() == "obj_05_take_spare_key")
                        {
                            ObjectiveManager.Instance.AdvanceToNextObjective(); // -> obj_06_leave_return_s2
                        }

                        BeginStorySequence();
                        StartCoroutine(PlayKeyDialogueRoutine());
                    },
                    onClose: null
                );
            }
        }

        private IEnumerator PlayKeyDialogueRoutine()
        {
            BeginStorySequence();
            yield return new WaitForSeconds(0.2f);
            yield return ShowDialogueAndWait(new List<DialogueLine>
            {
                new DialogueLine("Adrian", "Neutral", "The spare brass key. I hid this in the drain months ago."),
                new DialogueLine("Adrian", "Exhausted", "Why did I hide it? ...Right. So she couldn't come back inside that night."),
                new DialogueLine("Adrian", "Scared", "Wait... why did I think that? Who couldn't come inside?"),
                new DialogueLine("Adrian", "Scared", "I'm not finding evidence against a stranger. I'm finding things I put here myself."),
                new DialogueLine("Adrian", "Exhausted", "I can't stop now. If I do, I'll wake up at 03:17 again and still not know. Something else is out of place. The kitchen.")
            });
            EndStorySequence();
        }

        // ====================================================================
        // EVENT 4: SECOND CHAIR IN KITCHEN (INSPECTION MODAL POP-UP)
        // ====================================================================
        private void OnSecondChairInteracted()
        {
            if (!CanStartObjectInteraction()) return;

            bool firstChairInspect = !chairInspected;
            chairInspected = true;
            if (secondChairSprite == null) secondChairSprite = ResolveClueSprite("chair");

            if (firstChairInspect)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayHorrorStinger();
                if (ApartmentManager.Instance != null) ApartmentManager.Instance.TriggerCameraMicroShake(0.035f, 0.22f);
            }

            // Advance objective immediately upon inspection
            if (GetCurrentObjectiveId() == "obj_07_inspect_second_chair")
            {
                ObjectiveManager.Instance.AdvanceToNextObjective(); // -> obj_08_search_kitchen
            }

            // Log second chair clue into Evidence Inventory
            if (InventoryManager.Instance != null && !InventoryManager.Instance.HasItem("clue_second_chair"))
            {
                InventoryManager.Instance.AddItem(new InventoryItem(
                    "clue_second_chair",
                    "Second Dining Chair",
                    "A second wooden chair beside the kitchen table. Inscribed: \"Don't forget our place.\"",
                    secondChairSprite,
                    secondChairSprite
                ));
            }

            bool shouldPlayChairDialogue = !chairDialoguePlayed;

            if (InspectionModal.Instance != null)
            {
                // Pops up the chair object with close-up inspection just like other clues
                InspectionModal.Instance.Show(
                    secondChairSprite,
                    "Second Wooden Chair",
                    "Another chair resting beside the kitchen table. The floor beneath it is worn, like it's been here for years.\n\n<color=#A0A5B5>Carved Inscription: \"Don't forget our place.\"</color>\n\n— Adrian's Thought: 'Why does this feel familiar?'",
                    onCollect: null, // Stays in the room (not picked up)
                    onClose: () =>
                    {
                        if (shouldPlayChairDialogue && !chairDialoguePlayed)
                        {
                            chairDialoguePlayed = true;
                            BeginStorySequence();
                            StartCoroutine(PlayChairDialogueRoutine());
                        }
                    }
                );
            }
        }

        private IEnumerator PlayChairDialogueRoutine()
        {
            BeginStorySequence();
            yield return new WaitForSeconds(0.2f);

            // Part A: Adrian observes the chair
            yield return ShowDialogueAndWait(new List<DialogueLine>
            {
                new DialogueLine("Adrian", "Nervous", "The second dining chair. I dragged it to the trash chute three weeks ago."),
                new DialogueLine("Adrian", "Scared", "Yet here it sits at the table, right across from my bowl. Still warm.")
            });

            yield return new WaitForSeconds(0.3f);

            // Phone notification pop up before Unknown Number speaks
            if (PhoneNotificationUI.Instance != null)
            {
                yield return PhoneNotificationUI.Instance.ShowNotificationRoutine(
                    "Unknown Number",
                    "We had breakfast here every morning, Adrian."
                );
            }

            // Part B: Unknown Number texts Adrian, and Adrian's reaction
            yield return ShowDialogueAndWait(new List<DialogueLine>
            {
                new DialogueLine("Unknown Number", "Neutral", "We had breakfast here every morning, Adrian. You can't throw away a person with the garbage."),
                new DialogueLine("Adrian", "Exhausted", "Stop... please. Why does my chest hurt so much?"),
                new DialogueLine("Adrian", "Exhausted", "It hurts because part of me already knows."),
                new DialogueLine("Adrian", "Nervous", "I never trusted my own memory. If I did all this, I'd have written it down somewhere. The counter.")
            });

            EndStorySequence();
        }

        // ====================================================================
        // EVENT 5: KITCHEN NOTE INTERACTION
        // ====================================================================
        private void OnKitchenNoteInteracted()
        {
            if (noteCollected || !CanStartObjectInteraction()) return;
            if (noteSprite == null) noteSprite = ResolveClueSprite("note");

            if (InspectionModal.Instance != null)
            {
                InspectionModal.Instance.Show(
                    noteSprite,
                    "Kitchen Note",
                    "\"I took down the pictures.\n\nI put away the second chair.\n\nI hid the key.\n\nI thought if I removed everything, I could stop remembering what happened.\n\nBut every night at 03:17, she puts it all back.\n\nLook behind the photograph.\"",
                    onCollect: () =>
                    {
                        noteCollected = true;
                        if (kitchenNoteObject != null) kitchenNoteObject.SetActive(false);

                        if (AudioManager.Instance != null) AudioManager.Instance.PlayNotePickup();

                        if (InventoryManager.Instance != null)
                        {
                            InventoryManager.Instance.AddItem(new InventoryItem("clue_kitchen_note", "Kitchen Note", "A folded note from the kitchen instructing to look behind the photograph.", noteSprite, noteSprite));
                        }

                        while (ObjectiveManager.Instance != null && ObjectiveManager.Instance.CurrentObjective != null &&
                               (ObjectiveManager.Instance.CurrentObjective.Id == "obj_07_inspect_second_chair" ||
                                ObjectiveManager.Instance.CurrentObjective.Id == "obj_08_search_kitchen" ||
                                ObjectiveManager.Instance.CurrentObjective.Id == "obj_09_read_note"))
                        {
                            ObjectiveManager.Instance.AdvanceToNextObjective();
                        }

                        BeginStorySequence();
                        StartCoroutine(PlayKitchenNoteDialogueRoutine());
                    },
                    onClose: null
                );
            }
        }

        private IEnumerator PlayKitchenNoteDialogueRoutine()
        {
            BeginStorySequence();
            yield return new WaitForSeconds(0.2f);
            yield return ShowDialogueAndWait(new List<DialogueLine>
            {
                new DialogueLine("Adrian", "Scared", "It's my handwriting... but I don't remember writing this."),
                new DialogueLine("Adrian", "Exhausted", "How many nights have I done this? How many nights have I tried to forget?"),
                new DialogueLine("Adrian", "Nervous", "'Look behind the photograph.' The one on the coffee table. The one I've already looked at twice."),
                new DialogueLine("Adrian", "Exhausted", "Every night I find the pieces, and every morning I forget them. Not tonight. Back to the living room.")
            });
            EndStorySequence();
        }

        // ====================================================================
        // EVENT 7 — ENDING SEQUENCE
        // ====================================================================
        private IEnumerator PlayEvent7EndingSequence()
        {
            if (endingTriggered) yield break;
            endingTriggered = true;
            BeginStorySequence();

            // 1. Apartment ambience becomes quiet and room lighting dims
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetHeartbeatIntensity(0f);
                AudioManager.Instance.SetAmbientDuck(0.32f, 1.8f);
            }
            if (HorrorEventManager.Instance != null)
            {
                HorrorEventManager.Instance.DimActiveRoomForEnding(2.2f);
            }

            // 2. Second chair moves slightly toward the table
            if (secondChairObject != null)
            {
                Vector3 startPos = secondChairObject.transform.localPosition;
                Vector3 targetPos = startPos + new Vector3(-0.35f, 0f, 0f);
                float elapsed = 0f;
                while (elapsed < 1.2f)
                {
                    elapsed += Time.deltaTime;
                    secondChairObject.transform.localPosition = Vector3.Lerp(startPos, targetPos, elapsed / 1.2f);
                    yield return null;
                }
            }

            yield return new WaitForSeconds(0.4f);

            // Phone notification pop up with Cracked Smartphone sprite before final dialogue
            if (PhoneNotificationUI.Instance != null)
            {
                yield return PhoneNotificationUI.Instance.ShowNotificationRoutine(
                    "Unknown Number",
                    "You remember my name now, don't you?"
                );
            }

            // Keep apartment hushed during final exchange
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetAmbientDuck(0.28f, 0.8f);
            }

            // 3. Adrian receives final messages
            if (dialogueBox != null)
            {
                bool finished = false;
                Action onDone = null;
                onDone = () =>
                {
                    finished = true;
                    dialogueBox.OnDialogueFinished -= onDone;
                };
                dialogueBox.OnDialogueFinished += onDone;

                dialogueBox.StartDialogue(new List<DialogueLine>
                {
                    new DialogueLine("Unknown Number", "Neutral", "You remember my name now, don't you?"),
                    new DialogueLine("Adrian", "Exhausted", "Clara... It was 03:17 AM. You were crying on the other side of the door, and I wouldn't open it."),
                    new DialogueLine("Adrian", "Scared", "You didn't haunt this place to hurt me. You were waiting for me to unseal the door."),
                    new DialogueLine("Unknown Number", "Neutral", "The latch is unbolted, Adrian. You don't have to hide in this room anymore."),
                    new DialogueLine("Adrian", "Neutral", "I'm coming outside, Clara.")
                });

                while (!finished && dialogueBox.IsOpen)
                {
                    yield return null;
                }
            }

            // 4. Fade ambient sound out and play restrained ending resonance
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetAmbientDuck(0f, 2.0f);
                AudioManager.Instance.PlayEndingResonance();
            }

            yield return new WaitForSeconds(0.8f);

            // 5. Fade screen to black
            if (screenFader != null)
            {
                yield return screenFader.FadeOut(2.0f);
            }
            else
            {
                yield return new WaitForSeconds(1.5f);
            }

            // Optional Chapter 2 scene transition (preserves Chapter 1 ending if Chapter 2 scene is absent)
            if (OnChapter1Completed != null)
            {
                OnChapter1Completed.Invoke();
                yield break;
            }

            if (UnityEngine.Application.CanStreamedLevelBeLoaded("04_Chapter2_ColdCorridor"))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("04_Chapter2_ColdCorridor");
                yield break;
            }

            // 6, 7, 8: Show Ending Screen
            if (EndingScreen.Instance != null)
            {
                EndingScreen.Instance.TriggerCustomEnding(
                    quote: "\"Some memories don't disappear. They wait on the other side of the door.\"",
                    title: "ROOM 03:17",
                    subtitle: "You finally answered."
                );
            }
        }
    }

    /// <summary>
    /// Interaction trigger with debounce protection to prevent accidental re-triggering on close.
    /// </summary>
    public class StoryInteractableTrigger : MonoBehaviour
    {
        private string displayName = "Object";
        private float radius = 1.5f;
        private Action onInteract;
        private bool isPlayerInRange = false;
        private GameObject promptVisual;
        private AdrianController player;
        private float nextAllowedInteractTime = 0f;
        private SpriteRenderer targetRenderer;
        private Color originalRendererColor = Color.white;
        private bool rendererCached = false;

        public void Initialize(string name, float interactRadius, Action callback)
        {
            displayName = name;
            radius = interactRadius;
            onInteract = callback;
            CreatePrompt();
        }

        private void Start()
        {
            if (promptVisual == null) CreatePrompt();
            CacheRenderer();
        }

        private void CacheRenderer()
        {
            if (rendererCached) return;
            targetRenderer = GetComponent<SpriteRenderer>();
            if (targetRenderer != null)
            {
                originalRendererColor = targetRenderer.color;
            }
            rendererCached = true;
        }

        private void CreatePrompt()
        {
            if (promptVisual != null) return;

            promptVisual = new GameObject("Prompt_" + gameObject.name);
            promptVisual.transform.localScale = Vector3.one;
            promptVisual.transform.rotation = Quaternion.identity;
            promptVisual.transform.position = transform.position + new Vector3(0f, 0.75f, 0f);

            var tm = promptVisual.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            tm.fontSize = 38;
            tm.characterSize = 0.024f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.95f, 0.65f, 0.95f);
            string actionVerb = displayName.ToLower().Contains("chair") || displayName.ToLower().Contains("table") ? "Examine" : "Take";
            tm.text = $"[E] {actionVerb} {displayName}";

            var mr = promptVisual.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingLayerName = "Props_Front";
            mr.sortingOrder = 200;

            promptVisual.SetActive(false);
        }

        private void OnDisable()
        {
            if (promptVisual != null) promptVisual.SetActive(false);
            if (targetRenderer != null) targetRenderer.color = originalRendererColor;
        }

        private void OnDestroy()
        {
            if (promptVisual != null) Destroy(promptVisual);
        }

        private void Update()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<AdrianController>();
                if (player == null) return;
            }
            if (!rendererCached) CacheRenderer();

            float dist = Vector2.Distance(transform.position, player.transform.position);
            bool inRange = dist <= radius;

            bool overlayActive = InteractionBlocker.IsBlocked();
            bool uiBlocked = overlayActive || Time.unscaledTime < nextAllowedInteractTime;

            if (overlayActive)
            {
                // Push interaction debounce forward while any modal, phone notification, dialogue, menu, or story sequence is active
                nextAllowedInteractTime = Time.unscaledTime + 0.4f;
            }

            if (inRange != isPlayerInRange)
            {
                isPlayerInRange = inRange;
                SetPromptActive(isPlayerInRange && !uiBlocked);
            }

            bool showFocus = isPlayerInRange && !uiBlocked;
            if (targetRenderer != null)
            {
                if (showFocus)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3.5f);
                    targetRenderer.color = Color.Lerp(originalRendererColor, new Color(1f, 0.94f, 0.68f, originalRendererColor.a), 0.35f + 0.35f * pulse);
                }
                else
                {
                    targetRenderer.color = originalRendererColor;
                }
            }

            if (showFocus)
            {
                if (promptVisual != null)
                {
                    SetPromptActive(true);
                    float hover = Mathf.Sin(Time.time * 4f) * 0.06f;
                    promptVisual.transform.position = transform.position + new Vector3(0f, 0.75f + hover, 0f);
                    promptVisual.transform.rotation = Quaternion.identity;
                    promptVisual.transform.localScale = Vector3.Lerp(promptVisual.transform.localScale, Vector3.one, Time.unscaledDeltaTime * 14f);
                }

                var kb = UnityEngine.InputSystem.Keyboard.current;
                var pad = UnityEngine.InputSystem.Gamepad.current;
                bool interactPressed = (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                    || (pad != null && pad.buttonWest.wasPressedThisFrame);

                if (interactPressed)
                {
                    nextAllowedInteractTime = Time.unscaledTime + 0.45f;
                    SetPromptActive(false);
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayUiConfirm();
                    onInteract?.Invoke();
                }
            }
            else if (promptVisual != null && promptVisual.activeSelf)
            {
                SetPromptActive(false);
            }
        }

        private void SetPromptActive(bool active)
        {
            if (promptVisual == null || promptVisual.activeSelf == active) return;
            if (active)
            {
                promptVisual.transform.localScale = new Vector3(0.78f, 0.78f, 1f);
            }
            promptVisual.SetActive(active);
        }
    }
}
