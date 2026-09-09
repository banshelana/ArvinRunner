# ArvinRunner

A 2D side-scrolling parkour runner in the spirit of Vector. The runner moves
forward on their own; everything else is a swipe.

Unity **2022.1.24f1**, Built-in Render Pipeline, legacy Input Manager.

---

## Quick start

1. Open the project in Unity.
2. Menu bar → **ArvinRunner → Build Playable Project**.
3. It opens `Assets/Scenes/MainMenu.unity`. Press **Play**.

That one command generates the art, layers, prefabs, 46 obstacle chunks, the
runner's trick list, 10 levels, and both scenes — wired and added to the build
settings with the menu first. It is safe to run again; it overwrites only what
it generated.

There are two scenes. `MainMenu.unity` is the front end (play, level select,
reset progress) and `Game.unity` is the run itself. The menu tells the game
which level to load through `GameSession.RequestedLevel`; the game falls back to
saved progress when nothing was asked for. To skip the menu while iterating, open
`Game.unity` and set **startLevelIndex** on the `Game` object to the level you
want.

### Controls

| Action | On device | In the Editor |
| --- | --- | --- |
| Start the run | tap | Space, or click |
| Jump / double jump | swipe up | Space / W / ↑ |
| Slide (ground), dive (air) | swipe down | S / ↓ |
| Climb from a ledge grab | swipe up or right | D / → |
| Back to the main menu | back button | Esc |

Vault, wall run, ledge grab and the hard-landing roll are **contextual** — the
same swipe up becomes a vault in front of a low crate and a wall jump against a
tower face. The sensors decide, not the player.

Some obstacles have **two answers**. Anything whose underside sits above the
sliding height (0.79) but below the standing height (1.75) can be slid under;
anything whose top is under the jump height (3.2) can be hurdled. Put a bar in
both windows and the choice is yours — `Chunk_Rails`, `Chunk_Overpass` and
`Chunk_Scaffold` are built on exactly that, with the pickups deliberately split
between the high and low lines so collecting everything means switching routes.

---

## Dropping in your own sprites

Nothing here is married to the placeholder art. Two ways to animate the runner:

### Option A — sprite flip-book (no Animator to wire)

The drawn frames live in `Assets/Art/Player/PlayerAnimations/`, one folder per
clip, one PNG per frame, and are imported by **ArvinRunner → Re-import Player
Frames**. Add a folder, name it after the clip, drop numbered PNGs in it — the
importer handles the rest and `PlayerAnimations.asset` is rebuilt from it.

Three things it measures, because the frames arrive individually cropped and
importing them as-is makes the runner change size and hop about:

- **One pixels-per-unit for every frame**, taken from the tallest `Idle` pose so
  the standing runner comes out at 1.80 units. Scaling each clip to its own
  tallest frame would make the runner grow whenever a limb extends.
- **A pivot measured on the figure, not the canvas** — the bottom of the alpha
  bounds vertically, the alpha centroid horizontally. `Tackle1` carries 30
  transparent pixels below the body, so a canvas pivot would leave the runner
  hovering for the whole slide; and of the three candidates for the horizontal
  anchor, the centroid is the only one that holds still (canvas centre drifts
  28px across the jump, bounding-box centre 21px across the slide, and the feet
  swing 71px across the run cycle).
- **An exception to the shared scale**, for folders drawn at a different
  resolution. One scale for everything is what stops the runner resizing between
  clips, but it assumes every folder was drawn at the same size — and `lowFlip`
  and `Run` were not, both arriving at roughly twice the linear size of the
  others. Left alone, `Run` would render 3.1–3.5 units tall against a 1.80
  runner. `ScaleOverrides` in `PlayerAnimationImport.cs` gives such a folder the
  height its tallest frame should come out at; both are set to 1.94, which is
  what `Run` came out at before it was redrawn. Anything off-scale and unlisted
  gets a warning rather than silence.
- **An exception to the pivot rule**, for a folder drawn on one shared canvas
  instead of cropped frame by frame. This is the important one, and getting it
  the wrong way round is very visible.

  Cropped frames share no frame of reference, so each has to be measured and
  pinned on its own — that is what the two rules above are for. Frames sharing a
  canvas have already been positioned against each other by whoever drew them,
  and **the movement between them is the animation**. `Run` is 24 frames of one
  stride on a 772×897 canvas: the figure's lowest pixel sits on the canvas floor
  through the contact frames and lifts 35px through the flight phase, which is
  the runner leaving the ground. Measuring that per frame would pin the flight
  frames back down to the floor and delete the bounce from the run.

  So a shared-canvas clip gets **one anchor for the whole clip**: horizontally
  the mean centre of mass, which is where the body is across the cycle and lets
  the limbs swing around it; vertically the lowest pixel the clip ever reaches,
  which is the ground the runner stands on in the frames where they touch it.
  The importer tells the two cases apart by the canvases themselves rather than
  by a flag someone has to remember to set.

- **`LevelFrameHeights`** is a third exception, and is currently empty. `Run`
  needed it once, when its 16 frames arrived as two batches drawn 10% apart in
  size and the runner pulsed twice a second; it was redrawn on a shared canvas
  and the problem went with it. Levelling irons out a clip's genuine rise and
  fall, so it is only ever the lesser evil — reach for it when the importer
  complains about a spread it had to correct.

Every clip is paced by the thing it plays over, not by a hand-picked number.
`Slide` and `Roll` come from `slideDuration` and `rollDuration`; the jump clips
come from how long the rise actually lasts, which `TickAirborne` ends the instant
upward speed hits zero. That matters most for the eight-frame somersault: at a
plausible-looking 14fps it would be cut off three frames short of landing
upright on every single jump.

**`Run` is not paced by a frame rate at all.** It is advanced by *ground
covered* — `SpriteAnimationClip.strideDistance` — because a run cycle on a fixed
frame rate is correct at exactly one speed and wrong either side of it, and this
runner ramps from 9 to 15 across every level.

The numbers are worth having, because this is the dial for how the running
reads. The art draws a stride of **2.44 units**: across the 24 frames the
planting foot travels from 0.44 ahead of the body to 0.78 behind it, so the body
covers 1.22 units per footfall and 2.44 over a full cycle. Setting
`runStride` to exactly that locks the feet to the ground with no skating at all.

It is set to **3.0** instead, because the drawn stride is short for the speed
this game runs at:

| `runStride` | cycle at 9 u/s | footfalls/s | feet skate |
| --- | --- | --- | --- |
| 2.44 (as drawn) | 0.27s | 7.4 | none |
| **3.0 (current)** | **0.33s** | **6.0** | **+23%** |
| 3.5 | 0.39s | 5.1 | +43% |
| 4.5 (the old fixed cycle) | 0.50s | 4.0 | +84% |

Matching the art exactly reads as a scramble — 7.4 footfalls a second is about
half again as fast as a sprinter. The fixed 0.5s cycle this replaced had the
opposite problem and worse: the ground moved 84% further than the feet did at the
opening speed, and **207% by the time the runner ramped to 15**, which is where
the gliding came from. Raise `runStride` for longer, slower strides and more
slide; lower it for faster legs and less. Whatever it is set to, the relationship
now holds at every speed rather than at one.

The 24 frames are **one full cycle, not two**. The vertical extents repeat with
a period of 12 while the horizontal silhouettes do not, which is the signature of
two footfalls in one cycle — worth re-checking if the folder is redrawn, since
reading it wrong halves or doubles the apparent stride.

Two related things the flip-book does now. It steps **whole frames at a time**
rather than one per update, so a cycle that wants more frames per second than the
display can show drops frames to stay in time instead of falling behind and
playing in permanent slow motion — which matters here, since at 15 u/s the cycle
asks for 148fps and a 60Hz screen shows 10 of the 24. And clips with no
`strideDistance` are untouched: everything that is not locomotion still runs on
its own duration.

**Slots can hold more than one clip, and one is picked at random each time.**
That is how the jumps stop looking canned — `flipJump` sits alongside the plain
rise on `JumpRise`, and alongside `BigJump` on `DoubleJump`, so a ground jump and
an air jump each come out as a flip about half the time. Add a variant by adding
a second clip with the same `anim`; `SpriteAnimationSet.Get` does the rest. It is
the same trick `TrickSet` already uses for the procedural moves.

A slot holding a **single** pose keeps those procedural tricks — the spins and
squashes in `TrickSet.asset` still carry the vault, the wall run and the ledge
work. A slot with several frames switches them off. That is the knob: draw frames
for a slot and the tricks step aside for it, delete them and they come back.

The thirteen slots the game drives:

| Slot | When it plays | Source |
| --- | --- | --- |
| `Idle` | on the start line, and after crossing the finish | `Idle`, 6 frames |
| `Run` | normal running | `Run`, 24 frames |
| `JumpRise` | rising after a ground jump | `jump` 6 **or** `flipJump` 8, at random |
| `LowFlip` | a ground jump over something small | `lowFlip`, 6 frames |
| `JumpFall` | falling | `Fall`, 4 frames |
| `DoubleJump` | the air jump | `BigJump` 4 **or** `flipJump` 8, at random |
| `Slide` | sliding under an overhang | `Tackle`, 6 frames |
| `Roll` | hard landing, and the dive recovery | `Tackle`, 6 frames |
| `Vault` | going over a low obstacle | held `Jump2` + tricks |
| `WallRun` | running up a wall | held `Run4` + tricks |
| `LedgeGrab` | hanging on a ledge | held `Fall2` + tricks |
| `LedgeClimb` | pulling up over it | held `BigJump3` + tricks |
| `Death` | hit a hazard or fell | held `Tackle6` + tricks |

`LowFlip` is not a state of its own — it is the same `Jumping` state as
`JumpRise`, chosen instead of it when `PlayerSensors.LowObstacleAhead` is true at
the moment the runner leaves the ground. That probe looks four units ahead, well
past the 1.1 the vault probe reaches, because a jump is usually committed to long
before a vault would offer itself; "small" means a top no higher than
`maxVaultHeight`, reusing the line the game already draws. Select the player in
play mode to see it as an orange line in the scene view.

Add slots at the **end** of the `PlayerAnim` enum. `PlayerAnimatorDriver` hands
the enum's integer value to the optional Unity Animator, so inserting one in the
middle would silently renumber every slot after it.

Any slot left empty falls back to the `fallback` clip, so a partial set still
runs — and if the folders are missing entirely, every slot holds one pose and
the runner is animated procedurally, exactly as it was before the frames existed.

### Option B — a Unity Animator

Assign an Animator on the `Visual` child instead. `PlayerAnimatorDriver` sets an
int parameter **`State`** (matching the `PlayerAnim` enum order above) and a
float **`Speed`**. Leave the `SpriteAnimationSet` field empty if you go this way.

### Obstacle props

The hand-made props live in `Assets/Art/Obstacles/` and `Assets/Art/OstaclesNew/`
and are imported by **ArvinRunner → Re-import Art Only**. That step does three
things per PNG:

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

And the vehicles and site props, from `Art/OstaclesNew/`:

| Prop | Size | Role |
| --- | --- | --- |
| `pallet` | 1.07 × 1.15 | hop over |
| `tires` | 0.94 × 1.45 | hop over |
| `motorbike` | 2.27 × 1.30 | vault |
| `coupe` | 3.28 × 1.45 | vault onto the roof |
| `hatchback` | 3.32 × 1.50 | vault onto the roof |
| `taxi` | 4.64 × 1.45 | vault, four units of roof |
| `sedan` | 4.70 × 1.45 | vault, four units of roof |
| `pickup` | 4.53 × 1.55 | vault, the tallest thing still under the ceiling |
| `limo` | 5.92 × 1.40 | vault, six units of roof — a launch pad |
| `fence` | 2.96 × 2.10 | the one new prop meant to be cleared outright |
| `van` | 5.54 × 2.50 | jump onto the roof |
| `firetruck` | 8.70 × 2.70 | jump onto the roof |
| `bus` | 10.64 × 2.90 | jump onto the roof and run its length |
| `excavator` | 7.44 × 3.40 | climb: body at 1.90, then the cab |
| `scaffold` | 5.99 × 4.40 | wall run up it, grab the deck at 2.95 |

Those heights are the design, not the vehicles' real dimensions. Each one is
placed on a chosen side of one of four numbers — sliding height 0.79, vault
ceiling 1.60, standing height 1.75, jump height 3.20 — so what a prop demands is
decided by how tall it is, and nothing else.

Two consequences that are easy to get wrong when adding more:

- **A vault lands you *on* the obstacle, not past it.** So a car under 1.60 is
  not a hurdle, it is raised ground you run along. That is why the limo is worth
  more than its length suggests, and why nothing above the ceiling is placed
  without something to step off first.
- **Anything you intend as a step has to be about two units wide.** The vault
  puts the runner down somewhere in a unit-wide spread past the obstacle's front
  face; a pallet is narrower than that spread, so it is fine to hop over and
  useless to climb onto. Every step in the vehicle chunks is a car for this
  reason.

To change how big a prop is, edit `TallestHeight` in `ArtImport.cs` and
re-import. To change its collision shape, edit its `Boxes` in
`ChunkFactoryProps.cs` or `ChunkFactoryVehicles.cs` — those are normalised to
the sprite, so they survive a resize. Set `Flip` on a prop to mirror art and
colliders together, which matters for anything lopsided: the digger is flipped
so the runner meets its arm rather than its cab.

The five `_34` files in `Art/OstaclesNew/` are three-quarter views and are not
imported — the projection does not match a side-on camera.

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

## Audio

Tracks live in `Assets/Audio/` and are set up by **ArvinRunner → Re-import
Audio**. Two are wired by name:

| File | Where it plays |
| --- | --- |
| `chasing.wav` | the whole time you are in the Game scene |
| `mainmenu.wav` | the whole time you are on the front end |

Each scene owns its own `MusicPlayer` — one component, one `AudioSource`, a fade
in and a cross-fade — rather than one object surviving the scene change. The two
scenes want different music and a scene change is already a hard cut, so nothing
is gained by keeping something alive across it, and a persistent player would
have to be taught not to duplicate itself every time you came back to the menu.

The gameplay track keeps playing across a retry or a move to the next level,
because `LoadLevel` rebuilds in place instead of reloading the scene. That is
worth knowing before changing it: restarting the music on every death would be
the wrong feel, and it comes for free at the moment.

`LevelDefinition` has carried an unused `music` field since the start, and it is
now read — a level that names its own track cross-fades to it, a level that does
not keeps whatever the scene started with. Nothing in the campaign sets it yet.

**Escape leaves the run and goes back to the menu**, which is also what the
Android back button does. On the menu it steps back out of the level list. Note
that it is immediate: one press abandons the run with no confirmation. If you
would rather it opened the pause screen first, that already exists
(`GameManager.SetPaused` and the HUD's `pausePanel`) and it is a two-line change
in `GameHUD.Update`.

### A note on the source files

The two WAVs are 35MB and 11MB, and `chasing.wav` is 32-bit float PCM. Left on
Unity's defaults they would be decompressed into memory at load, which would cost
more RAM than the whole rest of the game — so the importer forces both to Vorbis
at 0.7 quality with **Streaming** load type. Music is the one thing streaming is
unambiguously right for: long, played once start to end, and nothing needs it to
begin on an exact frame.

Anything else dropped into `Assets/Audio/` gets the same treatment, which is
correct for music and wrong for short effects — a jump or a coin wants
**Decompress On Load** and no streaming. Split the settings by folder when the
first sound effect arrives.

---

## Making levels

A level is a `LevelDefinition` asset (`Assets/Data/Levels/`). Two modes:

- **Sequenced** — you list chunk prefabs in order. Used for levels 1–3 and 6–10.
- **Assembled** — you give a pool, a target length and a difficulty curve, and
  the builder picks a varied mix. The seed is fixed, so a level is identical on
  every retry. Used for levels 4–5.

Add a level by creating the asset (**Create → ArvinRunner → Level**) and dropping
it into `Assets/Data/Campaign.asset`. Nothing else needs changing — the menu grid
and the save data are both driven off `LevelSet.Count`.

### The campaign

| # | Name | Theme | Idea | Length | Difficulty |
| --- | --- | --- | --- | --- | --- |
| 1 | First Steps | Night | one move at a time | 222 | 2.0 |
| 2 | Rooftops | Night | obstacles with two answers | 306 | 2.4 |
| 3 | Construction | Dusk | wall run and ledge grab | 356 | 3.7 |
| 4 | Skyline | Dusk | assembled endurance | 414 | ~3 |
| 5 | Storm | Night | assembled, wind | 514 | ~4 |
| 6 | Rush Hour | Sodium | traffic and vehicle roofs | 382 | 2.9 |
| 7 | Demolition | Smog | nothing holds still | 420 | 3.3 |
| 8 | Air Support | Storm | the gunship | 398 | 3.6 |
| 9 | The Towers | Dawn | vertical, wall run throughout | 392 | 4.0 |
| 10 | Blackout | Blackout | everything at once | 454 | 4.4 |

Difficulty is the mean of a level's chunks **excluding the `Chunk_Flat` rests**,
which is the number worth watching — counting the rests in flatters a hard level
simply for giving the player somewhere to breathe.

Levels 6–10 each open by stating their own idea plainly, complicate it in the
middle, and close on the one chunk that combines it with something else. The
`Chunk_Flat` between the hardest pairs is not padding; it is where the player
gets to see what is coming.

Two things about the shape of the campaign are worth knowing before retuning it.
**Level 6 opens a new act, so it deliberately drops back** from level 5 — it has
to teach vehicle traffic before it can complicate it. And **level 3 is harder
than levels 6 and 7** at 3.7, which is inherited rather than designed; if the
early campaign ever feels like it spikes, that is where.

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
| `MovingObstacle` | travels leftward once the runner is near; despawns behind them |
| `HelicopterStrike` | fires one missile at a marked point as it passes |
| `MissileStrike` | the marker and the fire it leaves |
| `SpriteFlipbook` | cycles an obstacle's frames, or plays a slice of them once |

### Chunks that stack two mechanics

`ChunkFactoryAdvanced.cs` holds the chunks that put two demands on the same beat
— a beam standing in the middle of a gap, a wrecking ball swinging across one,
two presses half a cycle apart, a gunship strike that lands just before the roof
starts giving way. They exist because a library that teaches one thing per chunk
runs out of room to escalate: before them there was a single difficulty-5 chunk
in the whole game and only two that used the wall run.

The rule they follow: **two demands may overlap, but only one of them may be
invisible.** A beam over a gap is fair because both are on screen while there is
still time to act on either. What is avoided is stacking two things that each
need the same second of warning.

### Obstacles that come the other way

Everything else in the library is passed at the runner's own speed. The
motorbike and the gunship travel *against* the run, so the two close at the sum
of both speeds — which makes two things load-bearing that never mattered before.

**They must be moving before they are seen.** The camera shows roughly 18 units
ahead, so `MovingObstacle.activationDistance` has to exceed that or the player
watches the obstacle sit still on screen and then snap into motion. Both are set
to 30.

**Where the encounter happens is arithmetic, not placement.** The bike wakes 30
units out and the two close at 18, so they meet about 15 units along — but the
run speed ramps from 9 to 15 across a level, and at the top of that they meet at
19 instead. `Chunk_Oncoming` is flat and bare across the whole 15–19 window, and
its pickups are laid across it rather than at a point, so the arc that clears the
bike is the arc that collects wherever it is actually met.

The helicopter is not the obstacle — the fire it leaves is. It **fires off the
runner's time to the target, not its own position**, so the marker always ends
just as the runner arrives whatever speed they are doing. That has one
consequence worth knowing before retuning it: a faster runner triggers the shot
from *further back*, while the helicopter has had *less* time to close. The first
numbers here fired it from 23 units ahead at full run speed — five units off
screen, so only the marker ever appeared.

**Slowing the aircraft tightens the fast end, not the slow one**, which is the
counter-intuitive half: the less ground it covers before the trigger, the further
ahead it still is when it fires. At its current drift of 1.2 it shoots from 8.6
units ahead of a runner doing 9 and 15.4 ahead of one doing 15 — both inside what
the camera shows — and stays right of its target at every speed, so the missile
goes down and forward as the art draws it. That is close to as slow as it goes
before the shot starts happening off screen.

The marker is what makes the strike fair rather than memorised, and it is drawn
to the same radius as the blast that follows — a marker smaller than the fire
would be a lie. The marker and fireball are generated in `PlaceholderArt`
alongside the spikes and coins; the missile is not, because the helicopter's own
frames draw the launch and a second one would be two missiles for one shot.

The helicopter's frames are a **storyboard, not a cycle** — fly, descend,
fire_launch, missile_away, missile_far, recover. `SpriteFlipbook.PlayRange` plays
a slice, so the first two loop on the way in and the last four play once, on the
shot, holding on the recovery pose. They run at 5.5fps so those four span 0.73s
against the strike's 0.75s warning: the drawn missile leaves the frame on the
same beat the fire arrives. The split is a fact about these six drawings and is
written as one — dividing the folder in half instead would put the launch pose
inside the approach loop, and the aircraft would flash its muzzle over and over
while still only cruising.

Animated obstacle art lives in `Assets/Art/MovingObstacles/`, one folder per
obstacle, and is imported by **ArvinRunner → Re-import Moving Obstacles**. The
measuring is shared with the runner's frames (`SpriteMeasure`), but the scaling
is not: the runner's clips all share one pixels-per-unit because they are the
same character and it must not resize, whereas two unrelated vehicles get their
own scale each from a declared height in `MovingObstacleImport`.

**How a folder is anchored depends on how it was drawn, and the importer works
that out rather than being told.** Frames cropped individually — the bike, at
344x286, 310x285, 363x246 — share no frame of reference, so each is measured and
pinned on its own or the object jumps about as the crop changes under it. Frames
drawn on one shared canvas are the opposite case: the helicopter is six 1547x854
frames with the aircraft in the same place in every one, and what moves is the
missile leaving and the smoke trailing. Measuring those per frame would pin each
to its own centre of mass, and since the missile drags that centre 59px sideways
and 96px down, the aircraft would lurch across the sky chasing its own missile.
Frames that share a canvas size were laid out together, so that is the test, and
a shared canvas gets one anchor for the whole folder.

Both the anchor and the scale are measured on **solid pixels only**. Rotor blur,
exhaust and a smoke trail belong to the picture but not to where the object is,
and letting them into the measurement lets a machine follow its own smoke.

Their colliders are authored in world units rather than derived from the sprite,
because these frames change shape as the rider leans and the rotor turns — a
sprite-derived collider would breathe with them.

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
    ArvinRunnerSetup     builds everything; the menu items live here
    ArtImport            obstacle PNGs -> sized, pivoted sprites
    SpriteMeasure        the alpha bounds and centroid both importers read
    PlayerAnimationImport runner frames -> aligned, uniformly scaled sprites
    MovingObstacleImport  animated obstacle frames, one scale per obstacle
    AudioImport          music -> streamed Vorbis
    ChunkFactory*        the chunk library, split by kind of obstacle
    PlaceholderArt       generated stand-ins for anything not yet drawn
  Data/         config, themes, levels, campaign
  Prefabs/      player, pads, finish line, chunk library
  Audio/        chasing.wav (gameplay), mainmenu.wav (front end)
  Art/
    Obstacles/       rooftop props
    OstaclesNew/     vehicles and site props
    MovingObstacles/ animated obstacles, one folder each
    Player/PlayerAnimations/  one folder per clip, numbered frames
```

The layers `Ground`, `Wall`, `Vaultable`, `Hazard`, `Player` and `Collectible`
are written to slots 8–13 by the setup tool, matching `GameLayers.cs`. If you
reorder them in the Tags & Layers window, update that file too.
"# ArvinRunner" 
