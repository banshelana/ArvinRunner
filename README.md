# ArvinRunner

A 2D side-scrolling parkour runner in the spirit of Vector. The runner moves
forward on their own; everything else is a swipe.

Unity **2022.1.24f1**, Built-in Render Pipeline, legacy Input Manager.

---

## Quick start

1. Open the project in Unity.
2. Menu bar → **ArvinRunner → Build Playable Project**.
3. It opens `Assets/Scenes/Game.unity`. Press **Play**.

That one command generates placeholder art, the layers, the prefabs, 17 obstacle
chunks, 5 levels and a fully wired scene. It is safe to run again — it
overwrites only what it generated.

### Controls

| Action | On device | In the Editor |
| --- | --- | --- |
| Start the run | tap | Space, or click |
| Jump / double jump | swipe up | Space / W / ↑ |
| Slide (ground), dive (air) | swipe down | S / ↓ |
| Climb from a ledge grab | swipe up or right | D / → |

Vault, wall run, ledge grab and the hard-landing roll are **contextual** — the
same swipe up becomes a vault in front of a low crate and a wall jump against a
tower face. The sensors decide, not the player.

---

## Dropping in your own sprites

Nothing here is married to the placeholder art. Two ways to animate the runner:

### Option A — sprite flip-book (no Animator to wire)

`Assets/Art/Player/runner.png` is currently a **single pose**, assigned to every
slot. Because there is only one frame per slot, `PlayerAnimatorDriver` adds a
procedural footfall bob and a lean that changes with state — enough that the
runner does not look frozen. All of that switches itself off automatically as
soon as a slot has more than one frame, so it never fights real animation.

To animate properly, open `Assets/Data/PlayerAnimations.asset`. It already has
one slot per state. Import your sheet, slice it in the Sprite Editor, then drag
the frames into the matching slot and set the FPS.

The twelve slots the game drives:

| Slot | When it plays |
| --- | --- |
| `Idle` | on the start line, and after crossing the finish |
| `Run` | normal running |
| `JumpRise` | rising after a ground jump |
| `JumpFall` | falling |
| `DoubleJump` | the air jump |
| `Slide` | sliding under an overhang |
| `Roll` | hard landing, and the dive recovery |
| `Vault` | going over a low obstacle |
| `WallRun` | running up a wall |
| `LedgeGrab` | hanging on a ledge |
| `LedgeClimb` | pulling up over it |
| `Death` | hit a hazard or fell |

Any slot left empty falls back to the `fallback` clip, so a partial set still
runs.

### Option B — a Unity Animator

Assign an Animator on the `Visual` child instead. `PlayerAnimatorDriver` sets an
int parameter **`State`** (matching the `PlayerAnim` enum order above) and a
float **`Speed`**. Leave the `SpriteAnimationSet` field empty if you go this way.

### Obstacle props

The hand-made props live in `Assets/Art/Obstacles/` and are imported by
**ArvinRunner → Re-import Art Only**. That step does three things per PNG:

1. Trims transparent padding, so a sprite's bottom edge is the object's real
   base.
2. Splits sheets holding several objects — `cones.png` becomes `cone_large`,
   `cone_small` and `barricade` — by finding fully transparent columns.
3. Derives pixels-per-unit from the trimmed height, so each prop comes out at an
   exact world size with **no transform scaling anywhere**.

Every sprite gets a bottom-centre pivot, so placing one is just "put it at
ground level".

Current sizes, in world units:

| Prop | Size | Role |
| --- | --- | --- |
| `cone_small` | 0.53 × 0.60 | vault or hop |
| `cone_large` | 0.68 × 1.00 | vault or hop |
| `barricade` | 1.63 × 0.74 | vault |
| `barrier` | 2.89 × 1.05 | vault, wide enough to demand a real jump |
| `acunit` | 2.66 × 1.40 | vault, near the 1.6 ceiling |
| `dumpster` | 3.98 × 1.50 | vault onto the lid and run across |
| `crates` | 3.74 × 3.20 | too tall to clear — climb the shoulder in steps |

To change how big a prop is, edit `TallestHeight` in `ArtImport.cs` and
re-import. To change its collision shape, edit its `Boxes` in
`ChunkFactoryProps.cs` — those are normalised to the sprite, so they survive a
resize.

Props sit on the **Vaultable** layer deliberately. The wall probe only looks at
Ground and Wall, so jumping next to a skip never starts an accidental wall run.

- **Ground and rooftops** still use a plain white box sprite tinted per object.
  Replace the `SpriteRenderer.sprite` on any chunk prefab under
  `Assets/Prefabs/Chunks/`. Keep the renderers on **Tiled** draw mode so they
  resize without stretching.
- **The city backdrop** lives in `Assets/Data/Theme_Night.asset` and
  `Theme_Dusk.asset`. Each layer takes one horizontally-tileable sprite plus a
  `parallax` value: `0` is pinned to the camera (sky), `1` moves with the world.
  Import those sprites with **Mesh Type: Full Rect** and **Wrap Mode: Repeat**,
  or the tiling will show a seam.

---

## Making levels

A level is a `LevelDefinition` asset (`Assets/Data/Levels/`). Two modes:

- **Sequenced** — you list chunk prefabs in order. Used for levels 1–3, where
  each one teaches a move.
- **Assembled** — you give a pool, a target length and a difficulty curve, and
  the builder picks a varied mix. The seed is fixed, so a level is identical on
  every retry. Used for levels 4–5.

Add a level by creating the asset (**Create → ArvinRunner → Level**) and dropping
it into `Assets/Data/Campaign.asset`. Nothing else needs changing.

### Authoring a new obstacle chunk

Chunks are ordinary prefabs with a `LevelChunk` component. The one rule: **build
it with the walkable surface at the left edge sitting on local (0, 0)**. The
builder lays chunks end to end from there.

Fill in `length`, `entryHeight` / `exitHeight` (so a chunk can step up or down),
`difficulty` 1–5, the `skills` it demands, and a `variantTag` — two chunks
sharing a tag never get placed back to back.

### The obstacle kit

| Component | Behaviour |
| --- | --- |
| `Hazard` | ends the run on contact; `Armed` can be toggled |
| `CollapsingPlatform` | gives way shortly after you step on it |
| `MovingPlatform` | ping-pongs, and carries the player |
| `SwingingCrane` | pendulum wrecking ball |
| `LaserGate` | beam that pulses on and off, with a warning fade |
| `CrusherPress` | slams down on a cycle; slide through the gap |
| `BreakableGlass` | only smashes if you hit it fast enough |
| `Trampoline` | launches the runner and refreshes the double jump |
| `GustZone` | pulsing crosswind that shortens jumps |
| `Collectible` | pickup, feeds the score |

---

## Tuning the feel

Everything lives in `Assets/Data/PlayerConfig.asset` — run speed and its ramp,
jump heights, separate rise/fall gravity, slide and roll durations, vault reach,
wall-run duration and climb speed, ledge-grab tolerance, coyote time, and the
death slow-motion.

Two knobs worth knowing:

- **`riseGravity` vs `fallGravity`** — falling harder than you rise is what makes
  the arc feel snappy rather than floaty.
- **`bufferTime` on `SwipeInput`** — a swipe made slightly before landing still
  fires on touchdown. Raise it if inputs feel dropped, lower it if the runner
  feels like it acts on stale gestures.

## Layout

```
Assets/
  Scripts/
    Core/       game flow, layers, enums, save data
    Input/      swipe recognition and buffering
    Player/     state machine, sensors, tuning, animation
    Obstacles/  the hazard and trap kit
    Level/      chunks, level data, the builder
    Camera/     follow camera and parallax
    UI/         HUD and panels
  Editor/       the one-click setup tool (not shipped in builds)
  Data/         config, themes, levels, campaign
  Prefabs/      player, pads, finish line, chunk library
```

The layers `Ground`, `Wall`, `Vaultable`, `Hazard`, `Player` and `Collectible`
are written to slots 8–13 by the setup tool, matching `GameLayers.cs`. If you
reorder them in the Tags & Layers window, update that file too.
"# ArvinRunner" 
