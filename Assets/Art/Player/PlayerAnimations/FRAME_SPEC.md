# Runner frame spec — black silhouette

A full replacement set of player frames: one solid black silhouette runner in the
style of *Vector*, animated as a real parkour athlete moves. Hand this file to
whoever is producing them; `AI_BRIEF.md` next to it is the short form to paste
into a chat.

The importer is `Assets/Editor/PlayerAnimationImport.cs`; the durations below come
from `Assets/Data/PlayerConfig.asset` and are what each clip is actually paced
across in play.

---

## Backgrounds (resolved)

The night themes gave a black figure 1.1:1 against the sky. They have been
replaced by seven daylight themes where the sky is 14–16:1 against the runner and
the nearest towers 8:1 — see *The day palette* in the README.

---

## Why the old set is being replaced

Not style — the motion. Comparing each frame with the next, after aligning out
any body drift so only the pose is being measured:

| clip | silhouette that changes between consecutive frames | worst pair |
| --- | --- | --- |
| `Run` | 54% | 91% |
| `climb` | 68% | 107% |
| `flipJump` | 46% | 84% |

Smooth animation sits at 10–20%. At 54% each frame is most of a different
drawing; at 107% two consecutive frames barely overlap at all. **The frames were
not in-betweened** — each is an independent pose — and no frame rate fixes that.
Adding more frames of the same kind makes it worse, not better.

Alongside that: the figure is clipped by the canvas edge in 6 of 6 `lowFlip`
frames (all four sides on 5 of them), 6 of 6 `Idle`, 6 of 12 `climb`, 4 of 6
`jump`, 4 of 6 `Tackle`, 3 of 4 `BigJump` and 5 of 24 `Run`. `BigJump` is a
mid-air somersault in **4 frames**. And `Vault` and `Death` have no art at all —
they are single held poses from other clips, spun by code.

**A silhouette is the easy way out of most of this.** The hardest thing about
generating 245 frames of a character is keeping them the same person — same
build, same clothes, same hair, frame after frame. A flat black shape has none of
that to drift. What it costs instead is on one axis only, and the next section is
that axis.

---

## The one rule that matters for a silhouette

**Every pose has to read from the outline alone.** There is no shading, no
internal line, no colour. A limb held in front of the torso is not a limb — it is
nothing. So:

- **Separate the limbs in the outline.** A visible gap between a swinging arm and
  the body wherever the pose allows one. When a limb genuinely has to cross the
  torso, carry it far enough across that it breaks the outline on the far side
  rather than disappearing into the middle.
- **Both legs must be distinguishable** through the whole run cycle and every
  flip. The scissor of a stride is the most important shape in the game and it
  only exists if the two legs are separately visible.
- **Distinct extremities.** Shoes as a clear wedge so toe reads differently from
  heel. Hands as mittens — no separated fingers, they turn to mush at size.
- **A profile head.** Nose, chin and back-of-skull as a recognisable outline, so
  which way the runner faces is never in doubt. A round blob is not enough.
- **Fitted clothing, nothing loose.** No hood, no bag, no cape, no baggy trousers.
  *Vector*'s runner wears fitted clothes for exactly this reason: loose fabric
  adds blobs to the outline that the eye cannot assign to a body part.
- **Test every frame alone.** Pause on any single frame with no context: the pose
  should be unambiguous. If you cannot tell which arm is forward, the frame is
  wrong even if the sequence looks fine at speed.

---

## The frames

One folder per clip, one PNG per frame, named `<clip>_00.png` upward, zero
padded, **numbered in playback order**. A single sprite sheet on a uniform grid is
also fine — say the grid and it can be sliced.

Counts are each clip's real duration at **30fps**. 30 is the target rather than
the film-standard 24 because the game renders at 60Hz: at 30fps every drawn frame
gets exactly two display frames, and at 24 it alternates two and three, which is
visible as judder on slow moves. If a route makes frames expensive, 24fps is the
floor — multiply the duration by 24 instead.

| Folder | The move | Plays over | Frames @30 | Loops |
| --- | --- | --- | --- | --- |
| `Idle` | standing, breathing, weight shifting | 0.80s | 24 | yes |
| `Run` | **one full cycle — two footfalls** | 0.42s | 24 | yes |
| `jump` | ground jump: crouch, drive, rise, apex, drop, reach for the ground | 0.69s | 21 | no |
| `flipJump` | the same jump as a full front somersault, landing on the feet | 0.69s | 21 | no |
| `BigJump` | second jump taken in mid-air: tuck, kick down, rise, drop | 0.62s | 19 | no |
| `lowFlip` | a fast low flip over something knee-high | 0.69s | 21 | no |
| `Fall` | falling with no jump behind it, arms out, balancing | 0.50s | 15 | yes |
| `Tackle` | feet-first slide: drop to the hip, slide, hold | 0.70s | 21 | no |
| `Roll` | forward shoulder roll out of a hard landing, back onto the feet | 0.50s | 15 | no |
| `climb` | **one full cycle** of climbing a wall, hand over hand | 0.85s | 26 | yes |
| `Vault` | kong vault: hands to a waist-high obstacle, legs tucked through | 0.35s | 11 | no |
| `Death` | trip, tumble, come to rest face down and still | 0.90s | 27 | no |

245 frames. Three more slots are covered by re-using these, so nothing extra is
needed: the super jump re-uses `flipJump` stretched over its longer 0.97s hang,
and the ledge grab and ledge climb re-use `climb`.

`Run` stays at 24 rather than 13 because its cycle is the one clip fast enough
that extra frames buy real smoothness — 24 over 0.42s is 57fps, near one display
frame each.

---

## Rules every frame has to follow

**Pure black, alpha is the shape.** RGB `#000000` everywhere the character is,
fully transparent everywhere else. No gradient, no shading, no internal detail,
no outline, no rim light, no glow.

**Soft edges no wider than 1–2px.** The importer treats anything above alpha
12/255 as body, so a wide feathered edge measures as part of the figure and
shifts the pivot. Anti-alias the boundary and nothing more.

**One canvas, every frame, every clip.** 1024 wide × 1024 high — not per-clip,
not per-frame. This is the most useful line on the page: it lets the importer
anchor a whole clip once instead of measuring each frame, and it makes the runner
the same size in every clip with no correction table.

**Standing height 700px**, measured on the `Idle` pose, head to heel. Every other
clip is the same figure at the same scale — a crouch is shorter because the body
is crouching, not because the frame was drawn smaller.

**At least 100px clear on all four sides.** No pixel of the figure may touch a
canvas edge in any frame, including at full extension mid-flip. That is what the
1024 canvas buys against a 700px figure.

**A fixed ground line at y = 900** (124px up from the bottom). In any frame where
a foot is on the ground, the sole sits on that line — the same line in every
clip. In flight frames the figure leaves it, and that vertical travel *is* the
animation, so do not re-seat them.

**Side view, facing right.** Orthographic or a long lens. The camera never moves,
rotates or changes distance — between frames or between clips.

**Consecutive frames are 1/30s apart.** Each frame is what the body looks like
one thirtieth of a second after the last — an in-between, not a new idea. As an
acceptance test: **no more than about 20% of the silhouette changes from one
frame to the next.**

### One more, for `Run`

**The stride has to be a sprint.** Each step must cover about **1.2× the standing
height** of ground, so the full two-step cycle covers about 2.4× — at a 700px
figure, 840px of ground per step.

The old art drew 0.86× per step, which is a jog, while the game moved the ground
underneath at sprint speed; that mismatch is what makes the feet skate. Keep a
**flight phase of about four frames where neither foot is touching** — a run has
one, a walk does not, and its absence is what made the old cycle read as
scurrying.

---

## Checking what comes back

All measurable, and worth measuring before committing to a set:

1. Every PNG the same dimensions.
2. No frame's alpha touching a canvas edge.
3. Aligned frame-to-frame silhouette change under ~20%, no pair over 35%.
4. Contact frames putting the sole on the same y in every clip.
5. `Run` covering ~2.4 standing heights of ground per cycle, with a flight phase.
6. RGB flat black wherever alpha is above the threshold — no stray colour.
7. The frame counts in the table above.

If they hold, `ScaleOverrides` and the per-frame pivot inference in the importer
can both be dropped: the whole set imports on one scale with one anchor, which is
what that machinery was working around all along.
