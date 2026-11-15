# Phase 1 — Prototype (the first 30 seconds)

**Goal:** a parent opens a link, taps "Start check", the child steps back until fully in view, a floor marker appears under the feet, and a posture line follows the body live. Lifting one shoulder turns the shoulder line orange; levelling it turns it back.

**Storyboard frames:** 2, 3, 4 (front view only).
**Design handoff needed by:** Nov 20 (the "Prototype: yes" rows of plan section 5).

## Scope

| Part | Notes |
| --- | --- |
| Config file with app + mascot names | Placeholders "App", "Mascot" |
| Start screen with "Start check" | Plain screen |
| Front camera, mirrored, full screen | |
| Setup hints until whole body in view | "Step back", "Move left", "Move right" |
| Floor marker under the feet | Placed from ankle landmarks 27, 28 |
| Mascot + speech bubble | Simplified: placeholder image + text, no voice/animation |
| Live posture line over the body | Core of the demo — cannot be cut |
| Shoulder line turns orange | Simplified: front view, shoulder tilt only, placeholder threshold |

**Not in Phase 1:** side view, mascot copying, guided poses, walking, results, parent screens, voice, saving.

## Modules

| Module | Phase 1 job |
| --- | --- |
| `config.ts` | Load `config.json` (names, colours, text lines, thresholds, asset file names) |
| `flow.ts` | State machine with 3 states: `start` → `setup` → `scan` |
| `camera.ts` | Open the front camera (`facingMode: "user"`), mirror the image |
| `pose.ts` | Run MediaPipe Pose Landmarker (lite, VIDEO mode) per frame, return 33 landmarks |
| `overlay.ts` | Draw the posture line and floor marker on the canvas |
| `mascot.ts` | Show mascot image and speech bubble text |
| `measure.ts` | Shoulder tilt angle, ~0.5 s smoothing, visibility checks, full-body-in-view check |

## Steps (build order)

The order follows plan section 7: get the risky parts working on real phones first.

### 1.1 Project setup
- Vite + TypeScript scaffold, `index.html` with `<video>` + `<canvas>` stacked full screen.
- Deploy to HTTPS on day 1 (GitHub Pages via Actions, or Netlify) so phones can test the camera.
- **Done:** the empty page loads over HTTPS on both test phones.

### 1.2 Camera (day 1–2)
- `camera.ts`: `getUserMedia` front camera, mirrored, fills screen, handles portrait and landscape.
- Clear message if permission is denied.
- **Done:** live mirrored camera on Android Chrome and iPhone Safari.

### 1.3 Pose tracking (day 1–2)
- `pose.ts`: load `@mediapipe/tasks-vision` + lite model, run on each video frame (`requestAnimationFrame` / `requestVideoFrameCallback`).
- Debug mode draws all 33 points.
- **Done:** landmarks track the body on both phones; note FPS on each.

### 1.4 Posture line
- `overlay.ts`: draw the body line (head → shoulder mid → hip mid → ankle mid) plus shoulder and hip lines, mirrored to match the video.
- **Done:** the line keeps up with normal movement on the demo phone.

### 1.5 Shoulder tilt colour
- `measure.ts`: angle of the line between landmarks 11 and 12 from horizontal; smooth over ~0.5 s; use only when visibility > 0.6.
- Hysteresis: turn orange above `threshold`, back to normal below `threshold − margin` to stop flicker.
- **Done:** lifting one shoulder 10 times turns the line orange each time and back again.

### 1.6 Setup hints
- Full-body check: nose (0) and both ankles (27, 28) visible and inside the frame with a margin.
- Hints from landmark positions: body too big / cut off → "Step back"; off-centre → "Move left" / "Move right" (mirrored correctly).
- **Done:** hints stop within 2 s of the body being fully in view.

### 1.7 Floor marker
- When the body has been in view and still for a moment, place the marker centred under the ankles, scaled by body height.
- Mascot line: "Stand on the star!"
- **Done:** marker appears under the feet at 2 m and 3 m.

### 1.8 Config, placeholders, start screen, mascot
- `config.json` + `config.ts`; `/assets` with placeholder mascot (grey circle) and marker (ellipse).
- Start screen with "Start check"; mascot greeting in a speech bubble; mascot lines per state.
- `flow.ts` wires `start` → `setup` → `scan`.
- **Done:** swapping mascot image, colours and names needs no code change.

## Done when (plan section 2)

- [ ] The link opens on one Android phone (Chrome) and one iPhone (Safari) over HTTPS
- [ ] The whole-body check stops hinting within 2 seconds of the body being fully in view
- [ ] The posture line keeps up with normal movement on the demo phone
- [ ] Lifting one shoulder turns the shoulder line orange; standing level turns it back
- [ ] Swapping the mascot image, colours and names needs no code change (config file and asset folder only)
- [ ] Nothing is recorded or uploaded

## Testing (about 1 hour)

- One Android phone in Chrome and one iPhone in Safari
- Normal room light and a dim room
- 2 m and 3 m from the phone; the whole body must fit
- Lift one shoulder 10 times: orange each time, back each time
- 3 people of different heights do the first 30 seconds with no help

## Risks

| Risk | Fallback |
| --- | --- |
| Tracking lags on older phones | Lite model, lower video resolution, demo on the fastest phone |
| Camera won't start on iPhone | HTTPS from day 1; test Safari in the first days |
| Room too small for whole body | "Step back" hints; allow landscape |
| Shoulder line flickers | ~0.5 s smoothing + hysteresis margin |
| Design assets arrive late | Placeholders work alone; swap files on arrival |

## Deliverables

- Working HTTPS link
- GitHub repository
- 30-second screen recording as backup for the live demo
