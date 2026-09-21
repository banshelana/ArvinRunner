# Flood art spec

What the flood in level 14 ("High Water") needs to stop looking like a blue
rectangle and start looking like a flood. Hand this file to whoever is making it;
the **Brief** section at the bottom is the short form to paste into a chat.

The code that draws it is `Assets/Scripts/Level/FloodChase.cs`. The frames are
imported by `Assets/Editor/MovingObstacleImport.cs`, the same way the crocodile,
snake and fire are.

---

## What is wrong with it now

Three things, and only the third one is about art.

1. **It is a wall, not a flood.** It fills the screen from the bottom edge to the
   top, like a curtain. Real flood water has a surface. It sits at a height and
   the sky shows above it.
2. **It is the wrong colour.** It is clean blue. Flood water carries mud and is
   opaque brown or grey-brown, with white only where it breaks.
3. **Nothing moves the way water moves.** The front is a stack of rectangles that
   slide back and forth. Water rolls forward, curls over, breaks and churns, and
   no amount of code on rectangles gets there. That part has to be drawn.

Items 1 and 2 are code fixes, and they come with the art. Item 3 is what this
spec is for.

---

## What to make, most important first

Put each piece in its own folder under `Assets/Art/MovingObstacles/`, with files
numbered from 1, the way `crocodile/` and `fire/` are.

| # | Folder | What it is | Frames | Needed? |
|---|---|---|---|---|
| 1 | `floodFront` | The leading surge: the wave head that is chasing the runner | 16–24, looping | **Yes.** This is what is on screen when the flood is close. |
| 2 | `floodBody` | The flood surface behind the front, as a strip that repeats sideways | 12–16, looping | **Yes.** Without it, the front has nothing behind it. |
| 3 | `floodDebris` | Loose things riding the water: planks, a barrel, a tyre, a cone, a crate | 4–6 stills | Recommended. It is the fastest way to say "flood" rather than "sea". |
| 4 | `floodSpray` | A soft cloud of spray and mist | 1–3 stills | Optional. It replaces the banded mist that runs ahead of the water. |
| 5 | `Assets/Audio/flood.wav` | A roar of moving water, looping | — | Strongly recommended. It is what lets the player feel the flood before it is on screen. |

If you can only make one thing, make **1**.

---

## Rules for every piece

- **PNG with real transparency.** Transparent means an alpha channel. A grey and
  white checkerboard painted into the picture is not transparent. AI tools often
  do this, so check it.
- **One canvas size for the whole folder, and do not crop.** Every frame in a
  folder should be the same width and height, with the water in the same place.
  This is the most important technical rule. When frames are cropped one at a
  time, as the crocodile's were, the game has to work out where each one belongs,
  and the water jitters. When they share one canvas, it holds still for free.
- **Draw the water only.** No ground, no buildings, no sky, no people, no
  watermark. The game draws those.
- **It flows to the right.** The runner runs left to right with the flood behind
  him, so the front faces right.
- **The frames have to follow on from each other.** This is the lesson from the
  runner's old frames (see `Player/PlayerAnimations/FRAME_SPEC.md`). Smooth
  animation changes about 10–20% of the picture from one frame to the next. If
  each frame is its own separate drawing, the water will flicker, and adding more
  frames makes it worse, not better.
- **It has to loop.** The last frame should flow into the first as naturally as
  any other two frames do.
- **Style:** painted-realistic, to match the crocodile, snake and fire. Not
  cartoon.

---

## 1. `floodFront`: the surge

This is a dam-break surge, a wave of flood water rolling forward over the ground.
It is not an ocean wave.

- **Canvas:** 1024 × 1024 (or any size, as long as every frame is the same).
- **Shape:** a steep, sloped front of churning brown water, rising from the
  ground at the right to a crest that curls forward and breaks into white foam.
  Behind the crest, to the left, the water settles into a rough surface.
- **Size against a person:** the crest about 3½ times the height of a standing
  person. On a 1024 canvas that means the crest is about 900 px tall, where a
  person would be about 260 px. Do not draw the person.
- **Three lines have to stay put in every frame.** They are what let the pieces
  join up and the wave hold still:
  - **The base** is the bottom edge of the canvas, and the water touches it across
    the full width.
  - **The leading edge**, the right-most point where the water meets the ground,
    stays at the same x in every frame, about 85% of the way across. Spray can
    fly past it. The water itself does not.
  - **The left edge** is full water from its surface down to the bottom, with the
    surface at the same height in every frame. This is where `floodBody` joins
    on. The game measures both pieces and lines them up, so this height only has
    to stay the same from one frame to the next. It does not have to match
    anything else.
- **Motion:** the crest rolls forward and over, breaks, the foam tumbles down the
  face, and the whole face churns. Mud and foam should visibly travel to the
  right. It must not look like it is standing still.

## 2. `floodBody`: the water behind

- **Canvas:** 1024 × 512.
- **It must repeat sideways without a seam, in every frame.** The game lays copies
  of it side by side, so the right edge has to continue into the left edge.
- **Surface:** a rough, fast-moving flood surface with foam streaks and small
  breaking waves, at the same height in every frame, about a quarter of the way
  down from the top edge.
- **Below the surface:** solid, opaque, muddy water all the way to the bottom edge,
  with no transparency. The game extends it downwards with the same colour.
- **Motion:** the surface flows to the right and churns. Foam streaks travel right.

## 3. `floodDebris`: things in the water

Separate stills, one object per file, each on its own transparent canvas:
planks or a broken board, a barrel, a tyre, a traffic cone, a wooden crate. Draw
each one half-sunk, with the waterline drawn across it. The game bobs and tumbles
them on the surface.

## 4. `floodSpray`: spray

One to three soft, irregular clouds of fine spray and mist, white to pale grey,
with soft transparent edges. The game drifts them ahead of the front and off the
crest.

## 5. `flood.wav`: the roar

A deep, continuous roar of rushing water, 5–15 seconds, that loops without a
click. The game turns it up as the flood gets closer.

---

## The most reliable way to get smooth water

The runner's spec points to mocap, because recorded motion follows on from frame
to frame by construction. Water has the same kind of shortcut:

- **Blender:** the Ocean modifier makes water that loops and repeats sideways by
  design. That is exactly what `floodBody` needs. A fluid simulation of a
  dam-break gives the surge for `floodFront`. Render with a transparent
  background (Film → Transparent).
- **Stock sprite sheets:** search for *flood wave sprite sheet*, *tsunami wave
  animation transparent* or *water surge 2D animation*. Check the licence.
- **AI image tools** are the least reliable route for this, for the reason in the
  rules above: they draw every frame from scratch. If you use one, generate a
  single frame you like, then ask for small changes to that same frame, one step
  at a time. Reject any frame that jumps.

---

## Brief (paste into a chat)

> A 2D side-view game animation, painted-realistic style, of a flood surge: a
> steep, churning wall of muddy brown flood water rolling to the RIGHT over flat
> ground. The crest curls forward and breaks into white foam, and foam and mud
> tumble down the face. The water only: no ground, sky, buildings or people,
> transparent PNG background (real alpha, no checkerboard). 1024×1024 canvas,
> identical for every frame, not cropped. The water's base touches the bottom
> edge across the full width. The leading edge where water meets the ground stays
> at about 85% across in every frame. The left edge is full water up to a surface
> line at the same height in every frame. The crest is about 900 px tall. 16–24
> frames forming a seamless loop, each frame a small in-between step from the
> last (no frame is a new drawing).

> A 2D side-view game animation, painted-realistic style, of the surface of a
> fast muddy brown flood flowing to the RIGHT, with foam streaks and small
> breaking waves. 1024×512 canvas. The strip must tile seamlessly left to right
> in every frame. The surface line is at the same height in every frame. Below the
> surface, solid opaque muddy water to the bottom edge. Transparent above the
> surface (real alpha). 12–16 frames forming a seamless loop, each a small step
> from the last.
