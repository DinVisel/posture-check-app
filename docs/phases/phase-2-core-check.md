# Phase 2 — Core check

**Goal:** every measurement step of the check works end to end and gives stable values. Results are computed but not yet shown to parents (that is Phase 3).

**Storyboard frames:** 4 (front + side), 5, 6.
**Starts after:** Phase 1 checklist passes.

## Scope

| Part | Frame | Notes |
| --- | --- | --- |
| Side-view scan | 4 | Child turns sideways; head-forward measurement |
| Mascot copies the child's posture | 4 | Needs mascot mood/pose assets (sprite sheet or 3D model with animations) |
| Guided poses with ghost outline and hold timer | 5 | Ghost drawn relative to body landmarks |
| Walking check on stepping stones | 6 | Stones placed on the scanned floor plane; cut first if time runs short |
| All five measurements | 4, 6 | Shoulder tilt, hip tilt, head forward, knee alignment, foot angle |

## Scripts

| Script | Phase 2 work |
| --- | --- |
| `FlowController.cs` | Add states: `ScanSide`, `Poses`, `Walk` (front scan now continues instead of ending) |
| `Measure.cs` | All five measurements; median over a 2 s still window; visibility > 0.6; "could not measure" after two failed tries |
| `Overlay.cs` | Ghost outline, hold-timer ring, stepping stones placed in 3D on the scanned floor |
| `Mascot.cs` | Moods and posture copying (2D sprites or a rigged 3D model, as the design team decides) |

## Measurements (plan section 6)

| Measure | Landmarks | Calculation |
| --- | --- | --- |
| Shoulder tilt | 11, 12 | Angle of shoulder line from horizontal |
| Hip tilt | 23, 24 | Angle of hip line from horizontal |
| Head forward | 7/8, 11/12 (side) | Ear's horizontal distance in front of shoulder ÷ torso length |
| Knee alignment | 23-25-27, 24-26-28 | Angle at each knee |
| Foot angle (walking) | 29/30 → 31/32 | Each foot's angle to walking direction, averaged over steps |

Thresholds stay as placeholders in `config.json` and are labelled "not clinically checked".

## Steps

1. **2.1 Measurement engine** — still-window detection, median, visibility gating, retry → "could not measure".
2. **2.2 Front measurements** — add hip tilt and knee alignment to the front scan.
3. **2.3 Side-view scan** — turn-sideways guidance, side detection, head-forward value.
4. **2.4 Guided poses** — ghost outline, match check, hold timer.
5. **2.5 Mascot copying** — mascot mirrors shoulder/hip tilt using design team's mood assets.
6. **2.6 Walking check** — stepping stones on the floor line, step detection, foot angle per step.

## Done when

- [ ] One person can do the whole check and get stable values 3 times in a row
- [ ] Every measurement either gives a value or says "could not measure" — never a guess

## Needs from the design team

- Mascot moods (copying, happy, waving): sprite sheet, or 3D model with animations
- Ghost outline and stepping stones: PNG or 3D model
- Lines for side view, poses and walking

## Risks

| Risk | Fallback |
| --- | --- |
| Side view tracks badly | Median over 2 s, visibility check, "could not measure" |
| Not enough time | Cut walking check first |
