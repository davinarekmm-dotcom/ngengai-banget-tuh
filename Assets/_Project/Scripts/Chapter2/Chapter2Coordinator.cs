using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Room0317.Apartment;
using Room0317.Audio;
using Room0317.Dialogue;
using Room0317.Inventory;
using Room0317.Objectives;
using Room0317.Player;
using Room0317.Story;
using Room0317.UI;

namespace Room0317.Chapter2
{
    /// <summary>
    /// Isolated narrative and gameplay coordinator for Chapter 2: The Cold Corridor.
    /// Hooks into Chapter 1 completion via StoryProgressionCoordinator.OnChapter1Completed
    /// (or can be started standalone) and reuses all existing Chapter 1 systems.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class Chapter2Coordinator : MonoBehaviour
    {
        public static Chapter2Coordinator Instance { get; private set; }

        [Header("Startup Mode")]
        [SerializeField] private bool startStandaloneOnStart = false;

        [Header("Progression State")]
        [SerializeField] private Chapter2Step currentStep = Chapter2Step.NotStarted;
        [SerializeField] private bool hasInitialized = false;

        [Header("Environment Sprites (Optional Inspector Overrides)")]
        [SerializeField] private Sprite corridorWestSprite;
        [SerializeField] private Sprite corridorEastSprite;
        [SerializeField] private Sprite roofLandingSprite;

        [Header("Evidence & Inspection Sprites (Optional Inspector Overrides)")]
        [SerializeField] private Sprite[] footprintSprites;
        [SerializeField] private Sprite umbrellaSprite;
        [SerializeField] private Sprite intercomSprite;
        [SerializeField] private Sprite compactMirrorSprite;
        [SerializeField] private Sprite fleeceJacketSprite;
        [SerializeField] private Sprite movingBoxSprite;
        [SerializeField] private Sprite pharmacyReceiptInspectSprite;
        [SerializeField] private Sprite intercomLogInspectSprite;
        [SerializeField] private Sprite compactNoteInspectSprite;

        public Sprite CorridorWestSprite => corridorWestSprite;
        public Sprite CorridorEastSprite => corridorEastSprite;
        public Sprite RoofLandingSprite => roofLandingSprite;
        public Sprite[] FootprintSprites => footprintSprites;
        public Sprite UmbrellaSprite => umbrellaSprite;
        public Sprite IntercomSprite => intercomSprite;
        public Sprite CompactMirrorSprite => compactMirrorSprite;
        public Sprite FleeceJacketSprite => fleeceJacketSprite;
        public Sprite MovingBoxSprite => movingBoxSprite;

        public Chapter2Step CurrentStep => currentStep;

        private Chapter2RoomBuilder.BuiltChapter2Environment env;
        private DialogueBox dialogueBox;
        private AdrianController player;
        private ScreenFader screenFader;
        private bool isSequenceRunning = false;
        private bool exitsInspected = false;
        private bool umbrellaInspected = false;
        private bool intercomInvestigated = false;
        private bool compactInspected = false;
        private bool jacketRemoved = false;
        private bool endingTriggered = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (startStandaloneOnStart)
            {
                StoryProgressionCoordinator.SuppressChapter1 = true;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                StoryProgressionCoordinator.SuppressChapter1 = false;
            }
        }

        private void OnEnable()
        {
            StoryProgressionCoordinator.OnChapter1Completed += HandleChapter1Completed;
        }

        private void OnDisable()
        {
            StoryProgressionCoordinator.OnChapter1Completed -= HandleChapter1Completed;
        }

        private void Start()
        {
            dialogueBox = FindFirstObjectByType<DialogueBox>();
            player = FindFirstObjectByType<AdrianController>();
            screenFader = FindFirstObjectByType<ScreenFader>();

            if (startStandaloneOnStart && !hasInitialized)
            {
                StartCoroutine(BeginChapter2Routine());
            }
        }

        private void HandleChapter1Completed()
        {
            if (!hasInitialized)
            {
                StartCoroutine(BeginChapter2Routine());
            }
        }

        public void BeginChapter2()
        {
            if (!hasInitialized)
            {
                StartCoroutine(BeginChapter2Routine());
            }
        }

        private IEnumerator BeginChapter2Routine()
        {
            if (hasInitialized) yield break;
            hasInitialized = true;
            currentStep = Chapter2Step.EnterCorridor;

            yield return null; // Allow scene managers (ApartmentManager, ObjectiveManager, Clock) to finish Start()

            var oldIntros = FindObjectsByType<IntroAwakening>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var intro in oldIntros) intro.enabled = false;
            var clueCoord = FindFirstObjectByType<ClueSpawnCoordinator>();
            if (clueCoord != null) clueCoord.CoordinatorEnabled = false;

            if (ApartmentClock.Instance != null)
            {
                ApartmentClock.Instance.SetTime(3, 17);
            }

            BeginSequence();

            if (screenFader != null)
            {
                yield return screenFader.FadeOut(0.8f);
            }

            // Bind existing scene hierarchy if present (preserves manual Scene View collider/object edits), or build fallback
            env = Chapter2RoomBuilder.BuildOrBind(this, bindCallbacks: true);
            if (env.FootprintsGroup != null) env.FootprintsGroup.SetActive(false);
            if (env.CompactMirrorObject != null) env.CompactMirrorObject.SetActive(false);

            // Configure Chapter 2 objectives using existing ObjectiveManager
            ConfigureChapter2Objectives();

            // Move player to Apartment 317 corridor threshold & snap camera
            if (player == null) player = FindFirstObjectByType<AdrianController>();
            if (player != null && env.WestEntrySpawn != null)
            {
                player.Teleport(env.WestEntrySpawn.position, AdrianController.Facing.Down);
            }

            if (ApartmentManager.Instance != null && env.WestRoom != null)
            {
                ApartmentManager.Instance.SetCurrentRoom(env.WestRoom);
                ApartmentManager.Instance.SnapCameraToRoom(env.WestRoom);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayDoorTransition();
                AudioManager.Instance.SetAmbientDuck(0.75f, 1.5f);
            }

            if (screenFader != null)
            {
                yield return screenFader.FadeIn(1.2f);
            }

            // Scene 1 — Stepping Beyond
            yield return PlayDialogueRoutine(new List<DialogueLine>
            {
                new DialogueLine("Adrian", "Exhausted", "...It's even colder out here. Like walking straight into a meat locker."),
                new DialogueLine("Adrian", "Nervous", "The hallway is completely deserted. Not a single light on in any neighboring unit."),
                new DialogueLine("Adrian", "Scared", "Clara? Are you out here?")
            });

            currentStep = Chapter2Step.InspectExits;
            EndSequence();
        }

        private void ConfigureChapter2Objectives()
        {
            if (ObjectiveManager.Instance == null) return;

            var ch2Objectives = new List<ObjectiveData>
            {
                new ObjectiveData("ch2_obj_exits", "Inspect the Corridor Exits",
                    "Step out into the 3rd-floor corridor and check the elevator and emergency stairwell.",
                    "Head east to the elevator landing and inspect the exits.", "03:17 AM"),
                new ObjectiveData("ch2_obj_footprints", "Follow the Wet Footprints",
                    "Fresh damp footprints lead from Room 317 toward the trash chute alcove.",
                    "Follow the wet footprints in the West Corridor to the trash chute.", "03:17 AM"),
                new ObjectiveData("ch2_obj_intercom", "Investigate the Wall Intercom",
                    "Check the building intercom terminal near the elevator.",
                    "Head to the East Corridor and inspect the wall-mounted intercom.", "03:17 AM"),
                new ObjectiveData("ch2_obj_compact", "Search Near the Junction Box",
                    "Something clattered to the floor near the corner junction box.",
                    "Return to the West Corridor and check beneath the baseboard heater.", "03:17 AM"),
                new ObjectiveData("ch2_obj_roof_door", "Proceed to the Roof Access Door",
                    "Clara's note says she waited on the roof stairs at 03:17.",
                    "Head to the heavy metal door at the end of the East Corridor.", "03:17 AM"),
                new ObjectiveData("ch2_obj_jacket", "Inspect the Frozen Jacket",
                    "An old fleece jacket is wedged into the roof access door frame.",
                    "Examine and remove the frozen fleece jacket at the East Corridor exit.", "03:17 AM"),
                new ObjectiveData("ch2_obj_moving_box", "Investigate Clara's Moving Box",
                    "Step onto the roof landing and inspect what was left on the top step.",
                    "Enter the Roof Landing and examine Clara's moving box.", "03:17 AM")
            };

            ObjectiveManager.Instance.ConfigureObjectives(ch2Objectives);
        }

        // ====================================================================
        // SCENE 2 & 3 — ELEVATOR, STAIRWELL & GHOST SMS
        // ====================================================================
        public void OnInspectElevator()
        {
            if (isSequenceRunning || InteractionBlocker.IsBlocked(dialogueBox)) return;
            StartCoroutine(InspectExitRoutine(isStairwell: false));
        }

        public void OnInspectStairwell()
        {
            if (isSequenceRunning || InteractionBlocker.IsBlocked(dialogueBox)) return;
            StartCoroutine(InspectExitRoutine(isStairwell: true));
        }

        private IEnumerator InspectExitRoutine(bool isStairwell)
        {
            BeginSequence();

            if (!isStairwell)
            {
                yield return PlayDialogueRoutine(new List<DialogueLine>
                {
                    new DialogueLine("Adrian", "Neutral", "Elevator call button is completely dead. The display isn't shifting from 3.")
                });
            }
            else
            {
                yield return PlayDialogueRoutine(new List<DialogueLine>
                {
                    new DialogueLine("Adrian", "Nervous", "The emergency stairwell door is locked tight. That's a fire code violation... unless someone deliberately barred it.")
                });
            }

            if (!exitsInspected)
            {
                exitsInspected = true;
                currentStep = Chapter2Step.ReceiveGhostSms;

                yield return PlayDialogueRoutine(new List<DialogueLine>
                {
                    new DialogueLine("Adrian", "Scared", "Wait. If the stairs and elevator are unusable, where could she have gone?")
                });

                // Scene 3 — Ghost Contact: The Trail
                if (PhoneNotificationUI.Instance != null)
                {
                    yield return PhoneNotificationUI.Instance.ShowNotificationRoutine(
                        "Unknown Number",
                        "You stood with your ear pressed against the wood for twenty minutes.",
                        3.5f);
                }

                yield return PlayDialogueRoutine(new List<DialogueLine>
                {
                    new DialogueLine("Unknown Number", "Neutral", "You stood with your ear pressed against the wood for twenty minutes."),
                    new DialogueLine("Adrian", "Scared", "I... I thought you were trying to break in. I didn't recognize your voice through the door."),
                    new DialogueLine("Unknown Number", "Neutral", "You recognized it. You just hated that I was right. Look at your feet, Adrian.")
                });

                // Reveal wet footprint trail
                if (env != null && env.FootprintsGroup != null)
                {
                    env.FootprintsGroup.SetActive(true);
                }

                yield return PlayDialogueRoutine(new List<DialogueLine>
                {
                    new DialogueLine("Adrian", "Nervous", "Water stains... fresh water stains on the carpet. Leading down the hall."),
                    new DialogueLine("Adrian", "Exhausted", "She wasn't wearing shoes when she knocked. It was raining that night... freezing rain.")
                });

                currentStep = Chapter2Step.FollowFootprints;
                CompleteCurrentObjective("ch2_obj_exits");
            }

            EndSequence();
        }

        // ====================================================================
        // SCENE 4 — SOAKED GREEN UMBRELLA & PHARMACY RECEIPT
        // ====================================================================
        public void OnInspectUmbrella()
        {
            if (isSequenceRunning || InteractionBlocker.IsBlocked(dialogueBox)) return;

            if (currentStep < Chapter2Step.FollowFootprints)
            {
                StartCoroutine(PlaySimpleLineRoutine("Adrian", "Neutral", "A wet umbrella leaning by the trash chute. I should check the hallway exits first."));
                return;
            }

            if (umbrellaInspected)
            {
                StartCoroutine(PlaySimpleLineRoutine("Adrian", "Exhausted", "Clara's soaked green umbrella and the 02:45 AM pharmacy receipt. She went out in the sleet for my medicine."));
                return;
            }

            StartCoroutine(InspectUmbrellaRoutine());
        }

        private IEnumerator InspectUmbrellaRoutine()
        {
            BeginSequence();
            umbrellaInspected = true;

            yield return PlayDialogueRoutine(new List<DialogueLine>
            {
                new DialogueLine("Adrian", "Nervous", "Clara's green umbrella. The one she bought during our trip up north."),
                new DialogueLine("Adrian", "Exhausted", "The fabric is soaked through. It hasn't dried at all... as if she collapsed it just seconds ago.")
            });

            // Register evidence in InventoryManager & open InspectionModal with Pharmacy Receipt
            Sprite inspectSprite = ResolveSprite(pharmacyReceiptInspectSprite, "Assets/Objects/Chapter2/ch2_ui_pharmacy_receipt_placeholder.png");
            Sprite iconSprite = env != null && env.UmbrellaObject != null
                ? env.UmbrellaObject.GetComponent<SpriteRenderer>()?.sprite
                : null;

            var receiptItem = new InventoryItem(
                "ch2_pharmacy_receipt",
                "Soaked Umbrella & Pharmacy Receipt",
                "CITY PHARMACY - NOV 14, 02:45 AM. Prescribed emergency inhalers and fever medicine.",
                iconSprite != null ? iconSprite : inspectSprite,
                inspectSprite != null ? inspectSprite : iconSprite,
                true
            );

            if (InventoryManager.Instance != null && !InventoryManager.Instance.HasItem(receiptItem.id))
            {
                InventoryManager.Instance.AddItem(receiptItem);
            }

            if (InspectionModal.Instance != null && receiptItem.inspectionSprite != null)
            {
                bool modalClosed = false;
                InspectionModal.Instance.ShowItem(receiptItem, null, () => modalClosed = true);
                while (!modalClosed && InspectionModal.Instance.IsOpen) yield return null;
            }

            yield return PlayDialogueRoutine(new List<DialogueLine>
            {
                new DialogueLine("Adrian", "Scared", "A pharmacy receipt dated November 14... 02:45 AM. Prescribed emergency inhalers and fever medicine."),
                new DialogueLine("Adrian", "Exhausted", "She didn't storm out after our fight to leave me. She walked six blocks in a sleet storm to get medicine for my bronchitis."),
                new DialogueLine("Adrian", "Scared", "And when she got back... I had locked the safety chain.")
            });

            if (PhoneNotificationUI.Instance != null)
            {
                yield return PhoneNotificationUI.Instance.ShowNotificationRoutine(
                    "Unknown Number",
                    "You screamed through the door that if I walked out, I was dead to you.",
                    3.2f);
            }

            yield return PlayDialogueRoutine(new List<DialogueLine>
            {
                new DialogueLine("Unknown Number", "Neutral", "You screamed through the door that if I walked out, I was dead to you. You wouldn't even look through the peephole."),
                new DialogueLine("Adrian", "Nervous", "The building intercom... She tried to buzz the apartment when her key wouldn't turn. The wall terminal by the elevator.")
            });

            currentStep = Chapter2Step.InvestigateIntercom;
            CompleteCurrentObjective("ch2_obj_footprints");
            EndSequence();
        }

        // ====================================================================
        // SCENE 5 — WALL INTERCOM
        // ====================================================================
        public void OnInspectIntercom()
        {
            if (isSequenceRunning || InteractionBlocker.IsBlocked(dialogueBox)) return;

            if (currentStep < Chapter2Step.InvestigateIntercom)
            {
                StartCoroutine(PlaySimpleLineRoutine("Adrian", "Neutral", "The building intercom panel. Static crackles faintly from the speaker."));
                return;
            }

            if (intercomInvestigated)
            {
                StartCoroutine(PlaySimpleLineRoutine("Adrian", "Scared", "Three missed buzzes at 03:14, 03:15, and 03:16 AM. Something dropped near the junction box when I shut it off."));
                return;
            }

            StartCoroutine(InvestigateIntercomRoutine());
        }

        private IEnumerator InvestigateIntercomRoutine()
        {
            BeginSequence();
            intercomInvestigated = true;

            Sprite logSprite = ResolveSprite(intercomLogInspectSprite, "Assets/Objects/Chapter2/ch2_ui_intercom_log.png");
            if (InspectionModal.Instance != null && logSprite != null)
            {
                bool modalClosed = false;
                InspectionModal.Instance.Show(
                    logSprite,
                    "Building Intercom — Call Log",
                    "UNIT 317 MISSED BUZZ: 03:14 AM [NO ANS], 03:15 AM [NO ANS], 03:16 AM [NO ANS].",
                    null,
                    () => modalClosed = true);
                while (!modalClosed && InspectionModal.Instance.IsOpen) yield return null;
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayHorrorStinger();
            }

            yield return PlayDialogueRoutine(new List<DialogueLine>
            {
                new DialogueLine("Adrian", "Neutral", "The unit intercom. It records time-stamped call attempts between units and the lobby entrance."),
                new DialogueLine("Adrian", "Nervous", "Three missed call alerts from Unit 317's outside buzzer. All logged at 03:14, 03:15, and 03:16 AM."),
                new DialogueLine("Adrian", "Exhausted", "I unplugged the handset inside because I thought it was a drunk neighbor buzzing the wrong flat."),
                new DialogueLine("Clara (Intercom)", "Scared", "\"Adrian... please, it's freezing out here. The lock won't catch. My hands are too numb... please open the door...\""),
                new DialogueLine("Adrian", "Scared", "Turn it off... shut it off!")
            });

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayKeyPickup(); // Metallic clatter cue
            }

            // Reveal Cracked Compact Mirror near junction box
            if (env != null && env.CompactMirrorObject != null)
            {
                env.CompactMirrorObject.SetActive(true);
            }

            currentStep = Chapter2Step.DiscoverCompactMirror;
            CompleteCurrentObjective("ch2_obj_intercom");
            EndSequence();
        }

        // ====================================================================
        // SCENE 6 — CRACKED SILVER COMPACT MIRROR
        // ====================================================================
        public void OnInspectCompactMirror()
        {
            if (isSequenceRunning || InteractionBlocker.IsBlocked(dialogueBox)) return;

            if (compactInspected)
            {
                StartCoroutine(PlaySimpleLineRoutine("Adrian", "Nervous", "'03:17 - If you won't let me in, I'll wait on the roof stairs where the heater vent blows.'"));
                return;
            }

            StartCoroutine(InspectCompactRoutine());
        }

        private IEnumerator InspectCompactRoutine()
        {
            BeginSequence();
            compactInspected = true;

            yield return PlayDialogueRoutine(new List<DialogueLine>
            {
                new DialogueLine("Adrian", "Neutral", "Her compact mirror. The silver casing matches the pendant in the photograph."),
                new DialogueLine("Adrian", "Nervous", "The latch is bent. It looks like it dropped onto the tile with force.")
            });

            Sprite noteSprite = ResolveSprite(compactNoteInspectSprite, "Assets/Objects/Chapter2/ch2_ui_compact_note_placeholder.png");
            Sprite iconSprite = env != null && env.CompactMirrorObject != null
                ? env.CompactMirrorObject.GetComponent<SpriteRenderer>()?.sprite
                : null;

            var compactItem = new InventoryItem(
                "ch2_compact_mirror",
                "Cracked Silver Compact Mirror",
                "A spiderweb-fractured silver compact. Inside is a folded note: '03:17 - If you won't let me in, I'll wait on the roof stairs where the heater vent blows.'",
                iconSprite != null ? iconSprite : noteSprite,
                noteSprite != null ? noteSprite : iconSprite,
                true
            );

            if (InventoryManager.Instance != null && !InventoryManager.Instance.HasItem(compactItem.id))
            {
                InventoryManager.Instance.AddItem(compactItem);
            }

            if (InspectionModal.Instance != null && compactItem.inspectionSprite != null)
            {
                bool modalClosed = false;
                InspectionModal.Instance.ShowItem(compactItem, null, () => modalClosed = true);
                while (!modalClosed && InspectionModal.Instance.IsOpen) yield return null;
            }

            yield return PlayDialogueRoutine(new List<DialogueLine>
            {
                new DialogueLine("Adrian", "Scared", "There's something written inside: '03:17 - If you won't let me in, I'll wait on the roof stairs where the heater vent blows.'"),
                new DialogueLine("Adrian", "Exhausted", "The roof access... It's at the end of the east corridor."),
                new DialogueLine("Adrian", "Nervous", "The heating vents on that landing were decommissioned two winters ago. There was no heat up there.")
            });

            currentStep = Chapter2Step.ReachRoofDoor;
            CompleteCurrentObjective("ch2_obj_compact");
            EndSequence();
        }

        // ====================================================================
        // SCENE 7 — FROZEN FLEECE JACKET & ROOF ACCESS DOOR
        // ====================================================================
        public void OnInspectFleeceJacket()
        {
            if (isSequenceRunning || InteractionBlocker.IsBlocked(dialogueBox)) return;

            if (currentStep < Chapter2Step.ReachRoofDoor)
            {
                StartCoroutine(PlaySimpleLineRoutine("Adrian", "Nervous", "Someone wedged an old fleece jacket into the roof fire-exit frame. I need to find out what happened in the hallway first."));
                return;
            }

            if (jacketRemoved) return;
            StartCoroutine(InspectJacketRoutine());
        }

        private IEnumerator InspectJacketRoutine()
        {
            BeginSequence();
            jacketRemoved = true;
            currentStep = Chapter2Step.InspectFleeceJacket;

            yield return PlayDialogueRoutine(new List<DialogueLine>
            {
                new DialogueLine("Adrian", "Scared", "Her fleece jacket... she used it to keep the door from locking her out on the roof landing."),
                new DialogueLine("Adrian", "Exhausted", "I woke up at 07:00 AM that morning. The storm had stopped. I unlocked the front door to check the hallway, and found the medicine bag sitting on the doormat."),
                new DialogueLine("Adrian", "Nervous", "I called her phone. It went straight to voicemail. I told myself she just went to stay with her sister... I convinced myself she left on purpose."),
                new DialogueLine("Adrian", "Scared", "I never checked the stairs. I never went up.")
            });

            if (env != null && env.FleeceJacketObject != null)
            {
                env.FleeceJacketObject.SetActive(false);
            }

            if (env != null && env.RoofDoorTransition != null)
            {
                env.RoofDoorTransition.SetLocked(false);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySubtleCreak();
                AudioManager.Instance.PlayPhoneVibration();
            }

            currentStep = Chapter2Step.ReachMovingBox;
            CompleteCurrentObjective("ch2_obj_roof_door");
            CompleteCurrentObjective("ch2_obj_jacket");
            EndSequence();
        }

        // ====================================================================
        // SCENE 8 — CLARA'S MOVING BOX & 03:18 ENDING
        // ====================================================================
        public void OnInspectMovingBox()
        {
            if (isSequenceRunning || InteractionBlocker.IsBlocked(dialogueBox) || endingTriggered) return;

            if (currentStep < Chapter2Step.ReachMovingBox)
            {
                StartCoroutine(PlaySimpleLineRoutine("Adrian", "Neutral", "A frost-damaged moving box on the top step."));
                return;
            }

            StartCoroutine(FinalRevelationRoutine());
        }

        private IEnumerator FinalRevelationRoutine()
        {
            BeginSequence();
            endingTriggered = true;
            currentStep = Chapter2Step.FinalRevelation;

            yield return PlayDialogueRoutine(new List<DialogueLine>
            {
                new DialogueLine("Unknown Number", "Neutral", "Do you know why the alarm rings at 03:17, Adrian?"),
                new DialogueLine("Adrian", "Exhausted", "Because 03:16 was the last time you buzzed."),
                new DialogueLine("Adrian", "Scared", "At 03:17... you stopped knocking. That was the minute I decided to go back to bed and ignore you."),
                new DialogueLine("Unknown Number", "Neutral", "I didn't leave you that night. The paramedics found me on this landing before dawn. Severe hypothermia."),
                new DialogueLine("Adrian", "Exhausted", "You survived... but you never came back to 317. Your family came while I was at work and packed your things."),
                new DialogueLine("Unknown Number", "Neutral", "And you spent the last six months throwing out everything that reminded you of what you did, until your mind turned the guilt into a ghost."),
                new DialogueLine("Adrian", "Neutral", "You're not outside the door, Clara. You haven't been here for months."),
                new DialogueLine("Adrian", "Exhausted", "I'm the one who's still trapped in Room 317.")
            });

            // Stabilize corridor lights & reduce oppressive ambient audio
            if (env != null)
            {
                foreach (var l in env.CorridorLights)
                {
                    if (l != null)
                    {
                        l.color = new Color(1.0f, 0.90f, 0.72f);
                        l.intensity = 1.15f;
                    }
                }
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetHeartbeatIntensity(0f);
                AudioManager.Instance.SetAmbientDuck(0.15f, 2.0f);
                AudioManager.Instance.PlayClockTick();
                AudioManager.Instance.PlayEndingResonance();
            }

            // Advance phone/clock display from 03:17 to 03:18 AM
            if (ApartmentClock.Instance != null)
            {
                ApartmentClock.Instance.SetTime(3, 18);
            }

            currentStep = Chapter2Step.Completed;

            if (EndingScreen.Instance != null)
            {
                EndingScreen.Instance.SetCustomEnding(
                    title: "ROOM 03:17 — CHAPTER 2",
                    subtitle: "The Cold Corridor · 03:18 AM",
                    quoteNarrative: "\"The corridor lights hum steadily. For the first time, the clock moves forward: 03:18 AM.\""
                );
            }

            if (ObjectiveManager.Instance != null)
            {
                // Completing the final objective fires OnAllObjectivesCompleted -> EndingScreen.ShowEndingSequenceRoutine()
                CompleteCurrentObjective("ch2_obj_moving_box");
            }
            else if (EndingScreen.Instance != null)
            {
                yield return new WaitForSeconds(1.5f);
                EndingScreen.Instance.TriggerCustomEnding(
                    quote: "\"The corridor lights hum steadily. For the first time, the clock moves forward: 03:18 AM.\"",
                    title: "ROOM 03:17 — CHAPTER 2",
                    subtitle: "The Cold Corridor · 03:18 AM"
                );
            }
        }

        // ====================================================================
        // HELPERS
        // ====================================================================
        private void BeginSequence()
        {
            isSequenceRunning = true;
            if (player == null) player = FindFirstObjectByType<AdrianController>();
            if (player != null) player.SetCanMove(false);
        }

        private void EndSequence()
        {
            isSequenceRunning = false;
            if (player == null) player = FindFirstObjectByType<AdrianController>();
            if (player != null && !endingTriggered) player.SetCanMove(true);
        }

        private IEnumerator PlaySimpleLineRoutine(string speaker, string expression, string text)
        {
            BeginSequence();
            yield return PlayDialogueRoutine(new List<DialogueLine> { new DialogueLine(speaker, expression, text) });
            EndSequence();
        }

        private IEnumerator PlayDialogueRoutine(List<DialogueLine> lines)
        {
            if (dialogueBox == null) dialogueBox = FindFirstObjectByType<DialogueBox>();
            if (dialogueBox == null) yield break;

            dialogueBox.StartDialogue(lines);
            while (dialogueBox.IsOpen)
            {
                yield return null;
            }
        }

        private void CompleteCurrentObjective(string objectiveId)
        {
            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.CompleteObjective(objectiveId);
            }
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
    }
}
