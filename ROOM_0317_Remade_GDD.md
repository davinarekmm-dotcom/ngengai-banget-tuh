# ROOM 03:17

## Game Design Document (GDD)

------------------------------------------------------------------------

## 1. Executive Summary

-   **Title:** ROOM 03:17
-   **Genre:** Psychological Horror, Mystery, Hidden Object
-   **Target Audience:** 18--35 years
-   **Platform:** PC
-   **Engine:** Unity
-   **High Concept:** Adrian, an apartment resident, begins receiving
    mysterious messages every night at 03:17. As he investigates the
    apartment, he discovers that its rooms change whenever he leaves and
    enters them again. Objects mysteriously appear, disappear, or move,
    and these changes gradually reveal the truth about a roommate he
    cannot remember.

### Unique Selling Point (USP): The Changing Room

The apartment changes its appearance every time the player leaves and
re-enters a room. Players must carefully observe their surroundings and
identify objects that have mysteriously changed, disappeared, or
appeared. As the investigation progresses, these changes reveal clues
about the mysterious roommate and the supernatural events occurring
inside the apartment. The horror experience focuses on environmental
changes, observation, and finding hidden objects.

The USP is designed around readable environmental differences and
object-state changes. It does not require a complex time-manipulation
system or advanced enemy AI.

------------------------------------------------------------------------

## 2. Gameplay and Mechanics

### 2.1 Core Gameplay Loop

1.  Enter and observe an apartment room.
2.  Search for objects and details that appear unusual.
3.  Leave the room and enter it again.
4.  Compare the room's current state with what the player remembers.
5.  Identify objects that have appeared, disappeared, or moved.
6.  Inspect changed objects to discover clues.
7.  Use the clues to progress the investigation and uncover the mystery.

### 2.2 Main Player Actions

-   **Observe:** Look carefully at the room and its contents.
-   **Move:** Explore the apartment.
-   **Search:** Look for objects and environmental clues.
-   **Inspect:** Examine selected objects more closely.
-   **Interact:** Pick up or activate relevant objects.
-   **Compare:** Identify differences between the room's previous and
    current states.

### 2.3 Main Gameplay Mechanics

#### A. Room State Changes

Each room can have multiple predefined states. When the player leaves
and re-enters a room, the game can switch to another state.

Examples: - A chair changes position. - A photograph appears or
disappears. - A door that was previously closed is now open. - An
unfamiliar object appears on a table. - A familiar object is replaced
with a distorted version.

The changes should be intentional and designed as clues or horror
events, rather than random changes that make the game unfair.

#### B. Hidden Object Investigation

Players search for specific objects or visual details. Some objects are
only available in particular room states. Inspecting them can reveal
written messages, memories, or other clues.

#### C. Environmental Anomalies

The apartment gradually becomes more unsettling as the player
investigates. Anomalies can include: - Objects appearing in impossible
locations. - Photographs changing. - Furniture moving between visits. -
Unexpected shadows or silhouettes. - Small changes in lighting or room
decoration.

Anomalies should be used to build tension and communicate story
information.

#### D. Evidence and Clue Progression

Important discoveries are recorded as clues. Players use these clues to
understand the connection between Adrian, the apartment, and The
Roommate.

### 2.4 Win Condition

The player discovers all required clues and completes the investigation,
revealing the truth about The Roommate and the events occurring in the
apartment.

### 2.5 Lose Condition

The player fails to identify or correctly interpret important clues and
becomes unable to progress until they investigate the room states again.
If a specific failure sequence is needed, it can be introduced as a
scripted horror event rather than a complex enemy system.

------------------------------------------------------------------------

## 3. World and Entities

### 3.1 Story Background

Adrian lives alone in an apartment. Every night at exactly 03:17, he
receives a mysterious message. At first, the messages seem unrelated,
but they begin referring to objects and locations inside his apartment.

While investigating, Adrian notices that the rooms do not always look
the same after he leaves and returns. Furniture moves, photographs
change, and unfamiliar objects appear. These changes suggest that
someone else may have lived in the apartment---or may still be there.

As Adrian discovers more clues, he begins to question his memories and
whether he has ever truly lived alone.

### 3.2 Main Character: Adrian

-   **Name:** Adrian
-   **Role:** Main Character / Apartment Resident
-   **Main Stats:** Awareness (100), Mental Stability (100)
-   **Description:** Adrian is the protagonist who investigates the
    strange messages and changes occurring inside his apartment.
-   **Gameplay Role:** The player controls Adrian to explore rooms,
    inspect objects, identify environmental changes, and collect clues.

### 3.3 Main Antagonist: The Roommate

-   **Name:** The Roommate
-   **Role:** Mysterious Entity / Main Antagonist
-   **Main Traits:** Highly aware, capable of influencing the apartment,
    moderately aggressive.
-   **Special Ability:** The Roommate can alter room states by moving,
    changing, hiding, or revealing objects. These changes mislead Adrian
    and interfere with his investigation.
-   **Gameplay Role:** The Roommate is primarily represented through
    environmental changes and clues. Direct enemy AI or combat is not
    required for the core experience.

------------------------------------------------------------------------

## 4. Level and Content Scope

### 4.1 Main Environment

-   One apartment as the primary setting.
-   Several explorable rooms within the apartment.
-   Each room uses a small number of predefined visual states.
-   Objects and environmental details provide the main clues.

### 4.2 Planned Game Systems

-   Player movement and room navigation.
-   Room state management.
-   Object interaction and inspection.
-   Hidden-object searching.
-   Anomaly detection through visual comparison.
-   Clue collection and progression.
-   Environmental storytelling.
-   Horror audio and visual effects.

### 4.3 Technical Scope

**Estimated Scope: Small**

The game focuses on a limited environment and reusable interaction
systems. Room changes can be implemented by enabling, disabling,
replacing, or repositioning objects using predefined room states.

The project does not require: - A full time-manipulation system. -
Complex enemy pathfinding or combat AI. - Procedural room generation. -
Large open-world environments.

### 4.4 AI-Assisted Production

AI-generated content can support production of: - Apartment background
concepts. - Room and prop illustrations. - Object sprites and alternate
object appearances. - Character concept art and sprite references. -
Photographs, notes, and other investigation props. - Mood boards and
visual references.

Generated assets should be reviewed and adjusted to maintain consistent
art direction, scale, perspective, and visual readability. Room-state
differences should remain clear enough for players to notice.

------------------------------------------------------------------------

## 5. Engine and Platform

-   **Primary Platform:** PC
-   **Secondary Platform:** Android (optional, after the PC version is
    completed)
-   **Recommended Engine:** Unity

Unity is selected for its support for 2D development, object
interaction, UI, audio, lighting, and scene management. C# scripts can
manage room states, object interactions, clue progression, and scene
transitions. The project can target PC first and consider Android later.

------------------------------------------------------------------------

## 6. Prototype Priorities

The prototype should focus on proving the main USP before expanding the
story or content.

Recommended development order:

1.  **Room State System** --- create two or more predefined states for
    one room.
2.  **Room Re-entry Trigger** --- change the room state when the player
    leaves and returns.
3.  **Visual Differences** --- implement objects that appear, disappear,
    move, or change.
4.  **Player Interaction** --- allow the player to inspect relevant
    objects.
5.  **Clue Collection** --- record discoveries and unlock progression.
6.  **Horror Presentation** --- add lighting, sound, and scripted
    anomalies.
7.  **Narrative Progression** --- connect discoveries to the mystery of
    The Roommate.

------------------------------------------------------------------------

## 7. Example Gameplay Sequence

1.  Adrian enters the living room and notices a photograph on a table.
2.  The player inspects the photograph but finds no clear explanation.
3.  Adrian leaves the living room to explore another part of the
    apartment.
4.  When the player returns, the photograph is gone and a key has
    appeared in its place.
5.  The player inspects the key and discovers a clue connected to
    another room.
6.  Further room visits reveal more changes and evidence about The
    Roommate.
7.  The player connects the clues to uncover what happened in the
    apartment.

------------------------------------------------------------------------

## 8. Game Identity Summary

**ROOM 03:17** is a psychological horror and hidden-object mystery game
in which the apartment changes whenever the player leaves and re-enters
a room. Players investigate these environmental differences, inspect
strange objects, and collect clues to uncover the truth about a
mysterious roommate.

The game's central horror comes from a simple question:

**If the room changes when you are not looking, who---or what---is
changing it?**
