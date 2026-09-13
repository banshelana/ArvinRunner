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
| Super jump (when charged) | swipe left | A / ← |

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

The folders are **raw material, not finished clips**. The current set is a
black silhouette, generated a frame at a time, and every frame is cropped to its
own figure. `ArvinRunnerSetup.CreateAnimationSet` cuts the clips out of it, and
four things decide how.

**Scale.** Cropping throws away what size each folder was drawn at, and they are
not all the same — `Idle` is drawn about a fifth larger than the rest, `Fall`
about a tenth smaller. Height cannot measure it, because every clip is a
different pose, so the same pose was compared across folders instead (standing,
running, deep crouch) by silhouette area and, where the pose allows, by height.
`DrawingScale` in `PlayerAnimationImport.cs` holds the result, and the base scale
comes from the jump's upright first and last frames at 1.80 units:

| Folder | Idle | Run | Tackle | LowFlip | Jump | FlipJump | BigJump | Climb | Fall | lose | handJump |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| drawn at | 1.18 | 1.02 | 0.86 | 1.04 | 1.00 | 1.00 | 0.965 | 0.96 | 0.89 | 0.78 | 0.85 |

`Tackle` was redrawn after the first measurement — a lunge into a crawl rather
than a feet-first slide — and re-measured on its running frames.

A redrawn folder that is off-scale and unlisted gets a warning rather than
silence.

**Order.** Most folders are not filed in the order the body moves. `Run` cycles
between push-off, foot strike and knee drive out of sequence, and played as
numbered 38% of the silhouette changed every frame, worst step 55%. The change
between every pair of frames in each folder was measured, the smoothest order
solved for, and then checked by eye — the smoothest order is not always one a
body can perform. The run in leg order is 27%, worst 41%. The frame lists in
`CreateAnimationSet` are file numbers, so each clip can be followed in its
folder, and **they need redoing if a folder is redrawn**.

**Timing.** Drawings are not evenly spaced in time — eleven of the run's are one
instant of the foot strike, and near-duplicates sit side by side in most folders
— so a clip can carry `weights`, each frame's share of the move.

**Registration.** Where one cropped frame sits against the next depends on what
the body is doing, so it is decided per clip rather than per drawing, and stored
as per-frame `offsets` on the clip:

- **Feet** — upright on the ground: soles on the ground, head column still. A
  sprinter's head barely moves, while the centre of mass swings 0.11 units between
  run frames with the arms and legs.
- **Mass** — lying, sliding, on a wall: soles down, centre of mass across.
- **Air** — turning: centre of mass held at a standing runner's height, the point
  physics is carrying along the arc. Anchored on its lowest pixel a somersault
  bobs by half the body's height as the tuck opens and closes.

Offsets rather than pivots because one drawing can serve two uses — the backward
lean in `Fall` is both the fall loop and the death — and a sprite has one pivot.
The importer measures every frame's bounds, centre of mass and head column while
the textures are readable; the setup reads them back through
`PlayerAnimationImport.TryShape`, which is why the animation set must be built by
**Build Playable Project** and not on its own.

The source frames had baked-in background — a grey checkerboard in `Fall`, a pale
haze in `lowFlip` — which was stripped: any pixel lighter than the silhouette's
brightest real detail that is not solid. The originals are in
`ArtSource/PlayerAnimations_original/`, outside `Assets` so Unity ignores them.

### How each clip is driven

**Jumps follow the arc, not a clock.** A clip with `followJump` takes its frame
from the runner's vertical speed, which falls in a straight line from the launch
speed to zero at the apex and grows in a straight line on the way down — so
`apexFrame` is on screen at the top and the last frame at touchdown, for a hop
onto a crate and a super jump alike. Each starts at the instant of leaving the
ground: the crouch drawn before it is skipped, because the swipe is the takeoff.
A jump that runs long off an edge hands over to the fall loop once the runner is
falling 25% faster than its landing would have been.

**The run follows the ground.** Its frames advance by distance covered, and each
drawing is given exactly the stretch of ground its planted foot covers, measured
against the head. Near-duplicates pass in a blink; the frames where the leg
really travels get the time. The flight gets 35px a frame, which makes a step
1.26× the runner's height. The foot strike cannot be honest — its foot does not
move across eleven drawings — so it gets 4px each, and those 40px (about 0.4
units a step) are the only place the feet slide.

| speed | step | steps/s | drawings on screen per step | change between them |
| --- | --- | --- | --- | --- |
| 9 (opening) | 2.28 | 3.95 | 12 | 32%, worst 41% |
| 10 | 2.49 | 4.01 | 11 | 33%, worst 41% |
| 11 (ramped) | 2.71 | 4.07 | 11 | 34%, worst 42% |

The step lengthens with speed (`strideGrowth` 0.85) rather than the legs
quickening, which is how runners go faster. The 24 frames are **one step, not
two**: a silhouette shows one side, so both legs read as one.

**The vault follows the vault.** Its frames are keyed to the move itself
(`followVault`): the reach at take-off, the hand-plant frame appearing exactly as
the hands reach the obstacle's top, and the last frame as the vault ends — see
*The hand vault* below.

**Everything else runs on its own duration** — slide, roll, the wall moves and
the cling come straight from `PlayerConfig`.

### Landings, and handing back to the run

On touching down the run does not start straight away. `PlayerController`
paints a `Land` over it — or `GetUp` after a slide — and gives the slot back when
it has played; the state machine never waits for it. A jump can name its own
landing, so a somersault lands out of the somersault, and a named clip is only
ever reached by its name.

Every clip that hands over to the run **ends at running height**, and names the
point of the stride the run should carry on from (`exitToFrame`). The first cut
stopped the landings in the squat, and the head jumped up to half a unit into the
first stride frame — a pop at the end of every jump. Measured at the hand-over:

| into the run from | head moved, ending crouched | now |
| --- | --- | --- |
| plain landing | +0.50 | +0.10 |
| flip landing | +0.67 | +0.16 |
| low flip landing | +0.44 | 0.00 |
| double jump landing | +0.62 | +0.21 |
| getting up from a slide | +0.70 | +0.06 |
| roll | +0.26 | +0.21 |

One thing worth knowing when watching landings: a flat jump comes down at about
21 u/s against a `hardLandingSpeed` of 16, and whether that counts depends on
whether the 0.1-unit ground probe sees the roof one physics step before contact.
About one flat landing in four is caught that way and rolls. That predates the
new art.

### The slots

| Slot | When it plays | Frames (file numbers) |
| --- | --- | --- |
| `Idle` | on the start line, and after the finish | `Idle` 1–28 |
| `Run` | normal running | `Run` 13 19 17 18 12 23 21 1 4 22 24 8 14 7 9 10 6 3 5 2 20 11 16 15 |
| `JumpRise` | a ground jump | `jump` 4–16 **or** `flipJump` 9–20, at random |
| `LowFlip` | a ground jump over something small | `lowFlip` 9 10 11 12 15 13 14 17 16 18 |
| `DoubleJump` | the air jump | `BigJump` 8 9 6 7 10–14 **or** `flipJump` 9–20 |
| `SuperJump` | the charged jump | `flipJump` 9–20 |
| `Land` | touching down | `jump` 17–23; flip `flipJump` 22–24; low flip `lowFlip` 22–24; double `BigJump` 15 16 20 21 22; vault `handJump` 19 20 17 21–24 |
| `JumpFall` | falling with no jump behind it | `Fall` 12–16 and back |
| `Slide` | sliding under an overhang | `Tackle` 8 9 13 12 10 11, held on 11 |
| `GetUp` | the end of a slide | `Tackle` 14–19 |
| `Roll` | hard landing | `BigJump` 16–22 |
| `Vault` | a hand vault over or onto a low obstacle | `handJump` 4 11 10 5 6 7 12 13 |
| `WallRun` | running up a wall | `climb` 8 14 16 11 10 9 12 18 17 13 15 |
| `LedgeGrab` | hanging on a ledge | `climb` 1 2 3 2 |
| `LedgeClimb` | pulling up over a ledge | `climb` 19–22 |
| `Climb` | stuck against something | `climb` 1–7 |
| `Death` | hit an obstacle | `lose` 10–24 |
| `DeathFall` | fell out of the level | `Fall` 5–16 |

The death starts at the impact: `lose` 1–9 are the runner still running into
whatever it was, and by the time `Death` plays that has happened. Every frame is
anchored on the ground: anchoring the flung frames in the air, as the jumps are,
sank the feet 0.2 units into the roof. The clip lasts 0.95s against the 1.05s of
game time before the death panel, so the quarter-speed slow motion lands on the
impact. `Kill` also throws the body back at 2.5 u/s and brings it to rest with
drag, so it is not carried forward underneath a drawing of it flying backwards.
Falling out of the level keeps the tumble instead: kneeling on a floor that is
not there looks wrong. `lose` is drawn at 0.78 of the jump's size.

Left out on purpose: `flipJump` 21, an extended leap that belongs to no part of
the rotation (70% unlike both neighbours), and the standing starts of the jump
folders. Every slot is a drawn flip-book, so the procedural tricks in
`TrickSet.asset` stand down for all of them; they only come back if a folder
goes missing.

**`LowFlip`** is not a state of its own — it is the same `Jumping` state as
`JumpRise`, chosen instead of it when `PlayerSensors.LowObstacleAhead` is true at
the moment the runner leaves the ground. That probe looks four units ahead, well
past the 2.4 the vault probe reaches, and "small" means a top no higher than
`maxVaultHeight`.

### The hand vault

A swipe up within **2.4 units** of an obstacle no taller than 1.60 becomes a
vault over it with the hands. The probe used to reach 1.1 — a tenth of a second
at running speed — so vaults barely happened; further out the take-off is simply
longer, which is how the move is really done.

The old vault was a fixed hop, 2.3 units in 0.35s whatever was in front. That is
6.6 u/s against a run of 9 to 11, so every vault braked visibly, and nothing tied
it to the obstacle. `PlayerController.BeginVault` now builds it off the
obstacle's actual bounds, in three parts with one constant horizontal speed:

1. **Up to the plant.** Rising from the run and easing in, so the body arrives
   with the hands on the top just past the leading edge. The plant frame draws
   the hands `vaultHandReach` (0.33, measured off the art) ahead of the body, so
   the body is put that far short of the spot.
2. **Weight on the hands**, level with the top, for `vaultSupportShare` (0.4) of
   the rest — the share of the clip the three planted frames take.
3. **Over**, on a shallow arc, to the end.

Obstacles up to **2.0 wide** (`vaultOverWidth`) — cones, tyres, pallets, the
barricade — are vaulted clean over, if there is room to stand beyond. Wider ones
— the barrier, AC unit, motorbike, dumpster, every car — are vaulted up onto, and
the run carries on along the top.

This was checked by simulating the vault with the same maths over a cone, a
crate and a sedan at run speeds 9 and 11: in all 22 display frames that draw the
hands planted, the hands are on the obstacle's top (within 0.01 units vertically,
0.01–0.34 in from the edge). Two faults were caught that way first — the plant
frame keyed by its middle showed the hands 0.4 units short, and arcing from the
instant of contact lifted them off the top a frame later.

The folder is several takes rather than one, so `handJump` 14–16 (a dive onto
the hands) and 1–3 (the approach, which the game's own run supplies) go unused.

**`Climb`** is not a state either. The runner is still `Running` — still driving
into the wall, still killed by `CheckCrash` when `wallCrashGrace` runs out — and
`PlayerController.SetClinging` paints the climb over the run for as long as they
are stuck. The frames are paced to exactly that grace, so the scramble running
out is the moment the runner does. It only draws from the upright states, and
never over the death pose.

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

- **A vault over something wider than 2.0 lands you *on* it, not past it.** So
  a car under 1.60 is not a hurdle, it is raised ground you run along. That is
  why the limo is worth more than its length suggests, and why nothing above the
  ceiling is placed without something to step off first.
- **Anything you intend as a step has to be wider than 2.0.** Narrower obstacles
  are vaulted clean over, so a pallet is fine to vault and useless to climb onto.
  Every step in the vehicle chunks is a car for this reason.

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
- **The city backdrop** lives in the seven `Assets/Data/Theme_*.asset` files
  (see *The day palette* below). Each layer takes one horizontally-tileable sprite plus a
  `parallax` value: `0` is pinned to the camera (sky), `1` moves with the world.
  Import those sprites with **Mesh Type: Full Rect** and **Wrap Mode: Repeat**,
  or the tiling will show a seam.

---

## The super jump

A meter fills over **15 seconds of running** and buys one super jump: roughly
twice the height of a normal one, with a forward carry, on its own gesture.

| | value | where |
| --- | --- | --- |
| charge time | 15s | `PlayerConfig.superJumpChargeTime` |
| height | 6.4 (normal is 3.2) | `PlayerConfig.superJumpHeight` |
| forward carry | 1.35x | `PlayerConfig.superJumpSpeedMultiplier` |

That puts the runner in the air for 0.97s and covers **11.8 units of ground at
the speed a level opens on, 19.6 at the speed it ramps to**. The longest gap in
the library is 8, so a charged player clears any of them outright — but it does
*not* reach the 7.0 tower face in `Chunk_WallClimb`, so the wall run keeps its
job. A par run of level 1 banks about three of them; level 10, about seven.

Three decisions in it worth keeping:

**It is its own gesture, not an upgraded jump.** Swipe left, or A / ← — the one
direction `SwipeInput` already detected and nothing consumed. Folding it into
swipe-up would mean a charged player sometimes gets a jump far bigger than the
one they asked for, which is worse than an extra gesture to learn. It also
deliberately ignores `VaultAhead`: a normal jump in front of a low obstacle
becomes a vault, and the whole point of this one is to go over the thing.

**It charges only while actually running.** `PlayerController` ticks the meter
from inside `FixedUpdate`, past the guards that stop the run, so it does not fill
on the start line, while paused, after the finish, or while dead. A meter that
charged through the death screen would hand out a free jump for having just
failed.

**A full meter waits rather than draining.** Hesitating is never punished, and
the player can hold it for a stretch they know is coming.

The gauge sits bottom-left, clear of the progress bar along the top and the
pause button in the corner. It changes colour and pulses when full rather than
only changing length, so "ready" reads from the corner of the eye.

Two things it does not do, both deliberate: there is **no invulnerability** — it
opens the high route rather than cancelling danger, so hazards still matter on
the way up and down — and it cannot be fired **out of a slide**, only from a
run. The double jump is still available afterwards, which stacks to about 9.0
units for a player who spends the charge and then times an air jump off the apex.

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

### The day palette

The runner is a black silhouette, so every level is in daylight. On the old night
skies he had **1.1:1** of contrast against the sky — invisible the moment he left
the ground. The values were chosen against contrast targets, measured the proper
way (sRGB linearised, WCAG luminance), not by eye.

The backdrop is five layers, far to near: **the sun, a far ridge of hills, nearer
hills, a band of towers, and a street of houses**. `BackdropPainter` paints each
as a tileable strip in a light neutral grey, and each theme is one tint over all
of them. A tint can only darken, so the textures carry the brightness and the
theme carries the colour.

Depth is carried three ways at once, because any one alone reads flat:

- **Parallax.** Further layers scroll slower and follow the camera less
  vertically: ridge 0.08, hills 0.18, towers 0.38, houses 0.62.
- **Air.** Further layers are lighter, as distance washes colour out, and each
  step is at least 1.12:1 from the next. The first cut had the hills within a
  few percent of the sky and they vanished.
- **Size.** Trees are drawn a few pixels tall on the far ridge and several times
  that between the houses, so the eye measures the distance between them.

The towers thin out between their clusters, leaving gaps of open sky so the hills
show through. The houses stand on a deep solid street band, which is what shows
through a gap between rooftops and reaches the bottom of the screen even when
the camera drops.

| layer | grey | against the runner | against the layer behind |
| --- | --- | --- | --- |
| sky | 1.00 → 0.93 | 14.2–15.7:1 | — |
| far ridge | 0.88 | 12.7–13.9:1 | 1.3:1 |
| hills | 0.82 | — | 1.2:1 |
| towers | 0.74 | 8.9–9.8:1 | 1.2:1 |
| houses | 0.64 | 6.8–7.4:1 | 1.3:1 |
| house windows | 0.64 × 0.80 | 4.5–4.9:1 | — |
| walls (`WallTone`) | 0.40 0.42 0.50 | 4.0:1 | — |
| rooftops (`Rooftop`) | 0.34 0.36 0.42 | 3.1:1 | — |

Measured across all seven themes. The houses stay 1.7:1 or more lighter than the
walls in front of them and 2.2:1 lighter than the rooftops, so the foreground
never merges into the street behind it.

Things that only read because the sky used to be dark were
changed with it: coins have a dark rim and a deeper gold, glass is a deeper blue,
the finish pole is dark, and HUD text carries a dark outline over dark-glass
buttons.

| theme | tint | levels |
| --- | --- | --- |
| Morning | 0.84 0.92 1.00 | 1, 2, 5 |
| Afternoon | 1.00 0.91 0.80 | 3, 4 |
| Golden | 1.00 0.88 0.68 | 6 |
| Haze | 0.96 0.94 0.84 | 7 |
| Overcast | 0.87 0.90 0.95 | 8 |
| Sunrise | 1.00 0.88 0.85 | 9 |
| Fog | 0.90 0.90 0.93 | 10 |

A new theme is one `CreateTheme` call with a tint. Keep each channel at 0.68 or
above and the targets above still hold.

### The campaign

| # | Name | Theme | Idea | Length | Difficulty |
| --- | --- | --- | --- | --- | --- |
| 1 | First Steps | Morning | one move at a time | 222 | 2.0 |
| 2 | Rooftops | Morning | obstacles with two answers | 306 | 2.4 |
| 3 | Construction | Afternoon | wall run and ledge grab | 356 | 3.7 |
| 4 | Skyline | Afternoon | assembled endurance | 414 | ~3 |
| 5 | Storm | Morning | assembled, wind | 514 | ~4 |
| 6 | Rush Hour | Golden | traffic and vehicle roofs | 382 | 2.9 |
| 7 | Demolition | Haze | nothing holds still | 420 | 3.3 |
| 8 | Air Support | Overcast | the gunship | 398 | 3.6 |
| 9 | The Towers | Sunrise | vertical, wall run throughout | 392 | 4.0 |
| 10 | Blackout | Fog | everything at once | 454 | 4.4 |

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
| `LaserGate` | beam that pulses on and off as the runner approaches |
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

### Laser gates, and why they run on distance

`LaserGate` takes its phase from **how far the runner still is from it**, not
from the clock. The obvious version does not work in an auto-runner, and the way
it failed is worth keeping written down.

Timed off `Time.time`, a beam shows whatever phase it happens to be in when the
runner arrives — and the runner cannot stop, cannot slow down, and gets there at
a moment decided by everything earlier in the level. Worse, `LoadLevel` rebuilds
in place without reloading the scene, so the clock never resets and **every retry
showed a different pattern**. Dying taught the player nothing.

`Chunk_Lasers` was outright impossible because of it. The beams were 4.5 tall
against a 3.2 jump and stood on the floor, so there was no way over or under;
the only answer was to arrive during a 1.0s dark phase. They sat 7 apart with
cycles staggered 0.8s, and at 9–15 u/s seven units take 0.47–0.78s — so each
beam's phase had moved on 1.27–1.58s from the last, further than the dark phase
was wide. A phase that cleared the first beam had always rotated past clear by
the second. **A sweep of 2,000 speeds against 400 entry phases found nothing that
passed**; the only speed that worked at all was 5.38, and the runner never goes
below 9.

Three things fix it, each doing a different job:

- **`arrivalPhase`** is the authoring knob. It is the phase the beam holds at the
  moment the runner reaches it — under `armedFraction` it is lit and must be
  answered, over it the runner goes straight through. Deterministic, so a
  corridor is a fixed rhythm rather than a dice roll. `Chunk_Lasers` now reads
  **lit, open, lit**, with the pickups over the open one so the collecting line
  is the one that reads the middle gate instead of jumping it out of habit.
- **2.6 tall**, so a beam can be hurdled. A misread costs a jump rather than the
  run, and the chunk joins the two-answer family the rails and the overpass
  already belong to.
- **10 apart**, so consecutive jumps fit. A jump is airborne 0.686s and covers
  6.2–10.3 units depending on speed; at the old 7 spacing the runner was still in
  the air over the next beam with no way to answer it.

`lockDistance` settles the beam into its arrival state a couple of units out, so
it cannot flip on the frame of contact — which would be both unreadable and
unfair. A beam over a gap (`Chunk_LaserGap`) is deliberately set **open** on
arrival: lit, it would be unanswerable, since the runner is airborne with nowhere
to land and nothing to jump from.

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
run speed ramps from 9 to 11 across a level, and at the top of that they meet at
16.5 instead. `Chunk_Oncoming` is flat and bare across the whole window, and
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
goes down and forward. That is close to as slow as it goes
before the shot starts happening off screen.

The launch point is under the front of the cabin, between the skids.

The marker is what makes the strike fair rather than memorised, and it is drawn
to the same radius as the blast that follows — a marker smaller than the fire
would be a lie. The marker and fireball are generated in `PlaceholderArt`
alongside the spikes and coins, and so is the missile, which `MissileStrike` flies
from the helicopter's muzzle down to the marker.

The helicopter's frames are **one flying loop**: 24 frames in which the rotor
swaps between a spread blade and an edge-on one every frame and the body bobs
through the whole cycle. They play at 24fps — the bob takes a second and the
blade flickers at 12 a second, which reads as spinning. Nothing in them draws the
shot, so the loop keeps playing through it (`HelicopterStrike.launchDrawn` is off)
and the strike's own missile shows it. An earlier set was a storyboard — fly,
descend, launch, missile away, recover — and switched to its firing frames on the
shot, holding the last; on this art that would stop the rotor the moment it fired.

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
drawn on one shared canvas are the opposite case: the helicopter is 24 frames on
one 1448x653 canvas, and what moves between them is the animation — a 31px bob
and a rotor blade swapping pose. Measured per frame, the blade alone would move
the solid box about 40px sideways on alternate frames, so the aircraft would
shudder twelve times a second and the bob would be measured away.
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
