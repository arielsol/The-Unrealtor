# Edgar System - Designer Guide

## Overview

The Edgar System is a visual scripting tool for creating and managing character sequences. It allows you to create complex behaviors for Edgar (the character) by combining simple actions into sequences.

**Key Concepts:**
- **Points**: Locations in the scene where Edgar can appear or move to
- **Steps**: Individual actions (move, play VO, animate, etc.)
- **Sequences**: Ordered lists of steps that execute in sequence
- **Database**: Central collection of all sequences

---

## Table of Contents

1. [Initial Setup](#initial-setup)
2. [Creating Edgar Points](#creating-edgar-points)
3. [Creating Sequences](#creating-sequences)
4. [Action Types Reference](#action-types-reference)
5. [Editor Tools](#editor-tools)
6. [Testing & Debugging](#testing--debugging)
7. [Best Practices](#best-practices)
8. [Troubleshooting](#troubleshooting)

---

## Initial Setup

### 1. Scene Setup

1. **Create EdgarManager GameObject**
   - Add an empty GameObject to your scene
   - Add the `EdgarManager` component
   - Name it "EdgarManager"

2. **Configure EdgarManager**
   - **Edgar Points Holder**: Create an empty GameObject, name it "EdgarPointsHolder", and drag it here
   - **Voice Source**: (Optional) Drag an AudioSource component for voice playback
   - **Edgar Full Body Prefab**: (Optional) Drag the full-body Edgar prefab
   - **Edgar Face Only Prefab**: (Optional) Drag the face-only Edgar prefab (for wall appearances)
   - **Edgar Spawn Parent**: (Optional) Parent transform for spawned Edgar instances
   - **Sequence Database**: Create and assign an Edgar Sequence Database (see below)

### 2. Create Sequence Database

1. Right-click in Project window → `Create → Edgar → Edgar Sequence Database`
2. Name it (e.g., "EdgarSequenceDatabase")
3. Drag it to the EdgarManager's "Sequence Database" field

---

## Creating Edgar Points

Edgar Points define locations where Edgar can appear, move to, or perform actions.

### Steps:

1. **Create Point GameObject**
   - In your scene, create an empty GameObject under "EdgarPointsHolder"
   - Position it where you want Edgar to appear/move to
   - Add the `EdgarPoint` component

2. **Configure Point**
   - **Point ID**: Unique identifier (e.g., "Intro_Start", "Hint1_WallFace")
     - ⚠️ **Important**: The GameObject name will automatically sync with Point ID
   - **Default VO ID**: (Optional) Default voice line to play at this point
   - **Default Animation**: (Optional) Default animation trigger name
   - **Look At Player By Default**: (Optional) Whether Edgar should face the player

### Point ID Naming Convention

Use descriptive, hierarchical names:
- `Intro_Start`
- `Intro_Mid`
- `Puzzle1_Hint`
- `Puzzle1_Solution`
- `End_Celebration`

**Tip**: The Point ID dropdown in sequences will automatically show all available points!

---

## Creating Sequences

Sequences are ScriptableObjects that contain ordered lists of steps.

### Steps:

1. **Create Sequence Asset**
   - Right-click in Project window → `Create → Edgar → Edgar Sequence`
   - Name the asset (e.g., "IntroSequence")
   - ⚠️ **Important**: The asset name should match the Sequence ID (see Editor Tools section)

2. **Set Sequence ID**
   - Open the sequence asset
   - Enter a unique `Sequence ID` (e.g., "IntroSequence")
   - Use the sync tool to match asset name and Sequence ID (see Editor Tools)

3. **Add Steps**
   - Click the `+` button to add steps
   - Configure each step (see Action Types Reference below)
   - Steps execute in order from top to bottom

4. **Add to Database**
   - Open your `EdgarSequenceDatabase` asset
   - Drag your sequence into the "Sequences" list

---

## Action Types Reference

Each step has an **Action Type** that determines what Edgar does. Only relevant fields are shown based on the selected action type.

### Movement / Position

#### `GoToPoint`
- **Description**: Edgar walks to a point using NavMesh
- **Required**: Point ID
- **Behavior**: Waits until Edgar reaches the point before continuing

#### `SnapToPoint`
- **Description**: Edgar instantly teleports to a point (no movement animation)
- **Required**: Point ID
- **Use Case**: Quick repositioning, appearing from nowhere

### Appearance / Visibility

#### `ShowFullBodyAtPoint`
- **Description**: Spawns/shows full-body Edgar at a point
- **Required**: Point ID
- **Behavior**: If already spawned, just moves to the point

#### `ShowFaceAtPoint`
- **Description**: Spawns/shows face-only Edgar at a point (wall face appearance)
- **Required**: Point ID
- **Use Case**: Edgar appearing in walls, paintings, etc.

#### `HideEdgar`
- **Description**: Hides Edgar (or plays disappear animation then hides)
- **Required**: None
- **Use Case**: Edgar leaving the scene

### VO / Audio

#### `PlayVO`
- **Description**: Plays a voice line
- **Required**: VO ID (or Point ID with default VO)
- **Fields**:
  - **VO ID**: Voice line identifier
  - **Point ID**: (Optional) If VO ID is empty, uses point's default VO
- **Note**: Does not wait for VO to finish (add WaitSeconds if needed)

### Animation / Pose

#### `PlayAnimation`
- **Description**: Plays an Animator trigger
- **Required**: Animation Trigger
- **Fields**:
  - **Animation Trigger**: Trigger name (e.g., "Punch", "SpecialPose", "Sink")

#### `PlayPointDefaultAnimation`
- **Description**: Plays the point's default animation
- **Required**: Point ID (with default animation set)
- **Use Case**: When a point always uses the same animation

### Look / Orientation

#### `LookAtPlayer`
- **Description**: Edgar turns to face the player
- **Required**: None
- **Note**: Currently placeholder (implementation pending)

#### `LookAtPoint`
- **Description**: Edgar turns to face a specific point
- **Required**: Point ID
- **Note**: Currently placeholder (implementation pending)

### State / Timing

#### `SetTalking`
- **Description**: Sets Edgar's talking state (for lip sync)
- **Required**: Talking (bool)
- **Fields**:
  - **Talking**: `true` = talking, `false` = not talking
- **Use Case**: Enable lip sync before playing VO

#### `WaitSeconds`
- **Description**: Waits for a specified duration
- **Required**: Wait Seconds
- **Fields**:
  - **Wait Seconds**: Duration in seconds (e.g., 2.5)
- **Use Case**: Pause between actions, wait for VO to finish

---

## Editor Tools

### Sequence Editor (Individual Sequence)

When you select an `EdgarSequence` asset, you'll see sync tools at the top:

- **Asset Name**: Shows the current asset filename
- **Sequence ID**: The ID used to reference this sequence in code
- **Sync Buttons**:
  - **"Set Asset Name → Sequence ID"**: Updates Sequence ID to match asset name
  - **"Set Sequence ID → Asset Name"**: Renames asset to match Sequence ID

**Best Practice**: Keep asset names and Sequence IDs in sync!

### Database Editor (Sequence Database)

When you select the `EdgarSequenceDatabase` asset:

- **Mismatch Detection**: Shows all sequences with mismatched names
- **Bulk Sync Buttons**:
  - **"Sync All: Asset Name → Sequence ID"**: Updates all Sequence IDs from asset names
  - **"Sync All: Sequence ID → Asset Name"**: Renames all assets to match Sequence IDs
- **Validate All Sequences**: Checks for:
  - Empty Sequence IDs
  - Name mismatches
  - Sequences with no steps
  - Other validation issues

### Point ID Dropdown

When editing sequences, the Point ID field shows a dropdown with all available points from the scene. This prevents typos and makes it easy to select points.

---

## Testing & Debugging

### Runtime Debug Tools

The `EdgarManager` has built-in debug tools:

1. **Debug Sequence ID Field**
   - In Play Mode, find the "Debug (Runtime Only)" section
   - Enter a sequence ID
   - Right-click the component → `Debug: Play Sequence`

2. **From Code**
   ```csharp
   EdgarManager.Instance.DebugPlaySequence("YourSequenceId");
   ```

### Common Issues

- **Sequence not found**: Check that the sequence is in the database and Sequence ID matches
- **Point not found**: Verify the Point ID exists in the scene and is under EdgarPointsHolder
- **Edgar doesn't move**: Check NavMesh setup and that the point is on a walkable surface
- **VO doesn't play**: Verify VO ID is correct and voice system is connected

---

## Best Practices

### Naming Conventions

1. **Point IDs**: Use descriptive, hierarchical names
   - ✅ Good: `Intro_Start`, `Puzzle1_Hint`, `End_Celebration`
   - ❌ Bad: `Point1`, `p1`, `test`

2. **Sequence IDs**: Match asset names
   - ✅ Good: Asset "IntroSequence" → Sequence ID "IntroSequence"
   - ❌ Bad: Asset "IntroSequence" → Sequence ID "intro"

### Sequence Design

1. **Always start with appearance**
   - Use `ShowFullBodyAtPoint` or `ShowFaceAtPoint` first
   - Or use `GoToPoint` if Edgar is already in the scene

2. **Set talking state before VO**
   ```
   Step 1: SetTalking (talking = true)
   Step 2: PlayVO
   Step 3: WaitSeconds (wait for VO duration)
   Step 4: SetTalking (talking = false)
   ```

3. **Add waits for timing**
   - Wait after VO to let it finish
   - Wait between actions for pacing
   - Wait before hiding Edgar

4. **Use SnapToPoint for instant moves**
   - When you don't want walking animation
   - For appearing/disappearing effects

### Organization

1. **Group related points**
   - Keep points for the same area/puzzle together in hierarchy
   - Use descriptive parent GameObjects

2. **Organize sequences**
   - Create folders for different areas (Intro, Puzzles, Endings)
   - Name sequences descriptively

3. **Document complex sequences**
   - Add comments in sequence names or create documentation
   - Note any special requirements or dependencies

---

## Troubleshooting

### Edgar doesn't appear
- ✅ Check that `ShowFullBodyAtPoint` or `ShowFaceAtPoint` is in the sequence
- ✅ Verify the point exists and has a valid Point ID
- ✅ Check that prefabs are assigned in EdgarManager

### Sequence doesn't play
- ✅ Verify sequence is in the database
- ✅ Check Sequence ID matches exactly (case-sensitive)
- ✅ Ensure EdgarManager has the database assigned

### Point not found in dropdown
- ✅ Verify point is under EdgarPointsHolder
- ✅ Check Point ID is not empty
- ✅ Ensure scene is saved (points are cached on Awake)

### Fields are collapsed/overlapping
- ✅ This is a known Unity issue - try:
  - Closing and reopening the Inspector
  - Reimporting the script
  - Restarting Unity

### Sequence ID and asset name don't match
- ✅ Use the sync tools in the Sequence Editor
- ✅ Or use bulk sync in Database Editor

---

## Quick Reference

### Creating a Simple Sequence

1. Create Point → Set Point ID → Position in scene
2. Create Sequence → Set Sequence ID → Add to Database
3. Add Steps:
   - `ShowFullBodyAtPoint` → Select point
   - `SetTalking` → true
   - `PlayVO` → Enter VO ID
   - `WaitSeconds` → 3.0
   - `SetTalking` → false
   - `GoToPoint` → Select destination point
   - `HideEdgar`

### Common Step Patterns

**Edgar appears and talks:**
```
ShowFullBodyAtPoint → SetTalking(true) → PlayVO → WaitSeconds → SetTalking(false)
```

**Edgar moves and animates:**
```
GoToPoint → PlayAnimation → WaitSeconds
```

**Edgar appears in wall:**
```
ShowFaceAtPoint → SetTalking(true) → PlayVO → WaitSeconds → HideEdgar
```

---

## Support

For technical issues or questions, contact the programming team.

For workflow questions or feature requests, discuss with the team lead.

---

**Last Updated**: [Current Date]
**Version**: 1.0

