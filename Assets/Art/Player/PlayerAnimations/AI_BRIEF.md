# Briefs to hand to another AI

For the black silhouette runner. Copy-paste these; `FRAME_SPEC.md` next to this
file is the full spec and the reasoning.

**The character, worded once.** Use this exact text every time it is referenced,
in every prompt, for every clip:

> a solid pure-black silhouette of a lean athletic parkour traceur, fitted
> clothing with no loose fabric, no bag or hood, hands as simple mittens with no
> separated fingers, shoes as clear wedges, head in sharp profile with a defined
> nose, chin and back of skull

A silhouette is the easy mode here: the hardest part of generating hundreds of
character frames is normally keeping them the same person, and a flat black shape
has no clothing, hair or colour to drift. What it costs instead is **outline
readability** — with no shading, a limb in front of the torso is invisible. Every
brief below pushes on that, and it is the thing to reject frames over.

---

## Brief 3 first — mocap, no AI. This is what *Vector* actually did.

*Vector*'s runner looks real because the animation came from a real parkour
athlete, not from drawings. The same route is open and free, and for a silhouette
it is close to unbeatable: render a mocap clip with a pure black unlit material
and you get a perfect *Vector*-style silhouette where **every frame is an
interpolation of the last by construction** — the 54% problem in `FRAME_SPEC.md`
cannot physically happen.

1. **mixamo.com** — free with an Adobe account. Pick **one** character and use it
   for every clip. Search: *running*, *jumping*, *front flip*, *falling idle*,
   *running slide*, *forward roll*, *climbing*, *braced hang*, *vault*, *falling
   back death*, *idle*.
2. Download each as **FBX for Unity**, 30fps, with skin.
3. In Blender, once, and then reuse for every clip:
   - Import the FBX.
   - Assign every mesh a single material: base colour pure black, **roughness 1,
     metallic 0, and no lights in the scene at all** — an unlit black body is the
     silhouette, and with no lights you cannot accidentally get a highlight.
   - Camera: **orthographic**, on the -Z axis, pointing straight at the
     character, locked. Do not move it between clips.
   - Output: 1024×1024, **film transparent**, PNG with RGBA.
   - Render the frame range.
4. One camera and one character for all 245 frames, so scale and registration
   come out identical everywhere with no correction.

This also hands you a sprint stride for free: Mixamo's run is captured from a
person, so the step length is a person's rather than an illustrator's guess — the
exact thing the old art got wrong.

---

## Brief 1 — for a video model

One clip at a time. Video models keep frames related to each other, which is the
property the old frames are missing.

> A locked-off side view of **[CHARACTER]** performing **[MOVE]**, as a 2D
> silhouette animation in the visual style of the mobile game *Vector*.
>
> The figure is **solid pure black with no internal detail whatsoever** — no
> shading, no highlights, no outline, no facial features, no clothing detail. A
> flat black cut-out. The background is **flat pure white**, evenly lit, with no
> texture, gradient, shadow or vignette.
>
> He faces and travels to the **right**. The camera is locked — it does not move,
> pan, zoom, rotate or change distance at any point. Long lens or orthographic,
> so there is no perspective distortion and his proportions never change.
>
> **Every pose must read from the outline alone.** Arms held clear of the body
> with a visible gap wherever the pose allows; both legs distinguishable at all
> times; no limb disappearing into the torso. If a limb must cross the body it
> carries far enough to break the outline on the far side.
>
> No motion blur, no depth of field, no film grain, no slow-motion ramping —
> constant speed throughout. He is fully in frame with clear space around him; no
> limb ever reaches the edge of frame, including at full extension. He occupies
> about 70% of the frame height when standing.
>
> The clip is exactly **[DURATION]** long and shows **one single [MOVE]**,
> starting and ending at the poses described, with nothing before or after it.
>
> The motion must be that of a real parkour athlete — correct weight, real
> ground contact, momentum carried through the whole body. Not stylised, not
> floaty, not exaggerated.

Then per clip:

| `[MOVE]` | `[DURATION]` | starts / ends |
| --- | --- | --- |
| a full sprint stride cycle, both feet passing through, with a clear airborne phase where neither foot is down | 0.42s | seamless loop — the last frame flows into the first |
| a standing jump: crouch, drive up, peak, drop, reaching down for the landing | 0.69s | feet on the ground / feet reaching for the ground |
| a running front somersault, one full rotation, landing upright on his feet | 0.69s | feet leaving the ground / feet about to touch |
| a second jump taken in mid-air — tuck, kick the legs down, rise again, then drop | 0.62s | already airborne / still airborne |
| a fast low front flip over a knee-high obstacle | 0.69s | feet leaving the ground / feet about to touch |
| falling through the air, arms out, balancing, with no jump before it | 0.50s | seamless loop |
| a feet-first baseball slide: drop onto the hip, slide along the ground, hold | 0.70s | running / lying extended along the ground |
| a forward shoulder roll out of a hard landing, coming back up onto his feet | 0.50s | landing / standing |
| climbing a vertical wall hand over hand, one full cycle of reach and pull | 0.85s | seamless loop |
| a kong vault: hands onto a waist-high obstacle, legs tucked through between the arms | 0.35s | approaching / landing beyond it |
| tripping and tumbling to the ground, coming to rest face down and motionless | 0.90s | running / still |
| standing still, breathing, weight shifting slightly | 0.80s | seamless loop |

For the sprint cycle, add:

> This is a **sprint**, not a jog: each step covers about 1.2× his standing
> height of ground. Four of the frames have neither foot touching.

Extract at 30fps and drop the white to alpha — a silhouette makes this trivially
clean, since there is nothing to matte but a hard black-on-white edge:

```bash
ffmpeg -i clip.mp4 -vf "fps=30,scale=-1:1024" -pix_fmt rgba frame_%03d.png
magick frame_*.png -colorspace Gray -negate -alpha copy -channel RGB -evaluate set 0 +channel out_%03d.png
```

That second line turns brightness into alpha and forces every remaining pixel to
pure black, which is exactly the spec. Check the results against **Checking what
comes back** in `FRAME_SPEC.md`.

---

## Brief 2 — for a per-frame image model

Only worth trying with a model that accepts the previous frame as an image input.
Without that, each frame comes back as an unrelated pose and you get the old
problem back — that is almost certainly how the current frames were made.

Paste this once to set it up:

> I need a sprite animation for a 2D side-scrolling parkour game in the visual
> style of the mobile game *Vector*. I will ask for frames one at a time and
> **give you the previous frame as a reference image**. Every frame must be a
> genuine in-between of the one before it — the body may only move as far as it
> could in 1/30 of a second. If a frame looks like a new pose rather than a
> continuation of the last, it is wrong.
>
> The character, for every frame: **[CHARACTER]**.
>
> Fixed for every single frame:
> - **Solid pure black `#000000`, no internal detail at all** — no shading, no
>   highlights, no outline, no face, no clothing detail. A flat cut-out.
> - Fully transparent background. PNG with alpha. Anti-alias the boundary by no
>   more than 1–2px and nothing beyond that.
> - Pure side view, facing right. Orthographic — no perspective. The camera never
>   moves, rotates or changes distance.
> - Canvas exactly 1024×1024. He stands 700px tall head to heel and never comes
>   within 100px of any canvas edge, even at full extension.
> - The ground is an invisible line at y=900. Any foot touching the ground sits on
>   it. In airborne frames he leaves it — do not put him back down.
> - **The pose must read from the outline alone.** Arms clear of the body with a
>   visible gap wherever possible; both legs distinguishable; no limb vanishing
>   into the torso. I will reject any frame where I cannot tell which arm is
>   forward.
> - Real parkour motion — correct weight, real ground contact, momentum carried
>   through the body. Not stylised, not floaty.
>
> Confirm you have all of this, then I will ask for the first clip.

Then per clip:

> Clip **[NAME]**: **[MOVE]**, spread evenly over **[N] frames** at 30fps.
> Give me frame 1 as the starting pose. I will then ask for each following frame
> with the previous one attached.

with `[N]` from the table in `FRAME_SPEC.md` — `Run` 24, `jump` 21, `flipJump`
21, `BigJump` 19, `lowFlip` 21, `Fall` 15, `Tackle` 21, `Roll` 15, `climb` 26,
`Vault` 11, `Death` 27, `Idle` 24.

For `Run`, add:

> This is a **sprint**, not a jog: each step must cover about 840px of ground —
> 1.2× his standing height. There must be an airborne phase of about four frames
> where neither foot is touching. The 24 frames are one full cycle of two steps
> and the last frame must flow back into the first.
