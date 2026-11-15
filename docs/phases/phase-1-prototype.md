# Phase 1 — Prototype (scan, turn, posture line)

**Goal:** a parent opens the app, taps "Start check", scans the floor with the back camera, turns the phone around and puts it on a shelf. The child steps into view, a star appears on the scanned spot, and a posture line follows the body live. Lifting one shoulder turns the shoulder line orange; levelling it turns it back.

**Storyboard frames:** 2, 3, 4 (front view only).
**Design handoff needed by:** Nov 20 (the "Prototype: yes" rows of plan section 5).

## Scope

| Part | Notes |
| --- | --- |
| Config file with app + mascot names | Placeholders "App", "Mascot" |
| Start screen with "Start check" | Plain screen |
| Floor scan with the back camera | AR Foundation plane detection; parent aims at the child's spot |
| Turn the phone around | Attitude sensor measures the turn; floor carried into the front camera's view |
| Front camera, mirrored, full screen | `WebCamTexture` |
| Setup hints until whole body in view | "Step back", "Move left", "Move right" |
| Floor star on the scanned spot | 3D on the scanned floor; ankle-based fallback |
| Mascot + speech bubble | Simplified: placeholder image + text, no voice/animation |
| Live posture line over the body | Core of the demo, cannot be cut |
| Shoulder line turns orange | Simplified: front view, shoulder tilt only, placeholder threshold |

**Not in Phase 1:** side view, mascot copying, guided poses, walking, results, parent screens, voice, saving.

## Scripts

| Script | Phase 1 job |
| --- | --- |
| `AppConfig.cs` | Load `StreamingAssets/config.json` (names, colours, text lines, thresholds, front camera field of view, asset file names) |
| `FlowController.cs` | State machine: `Start` → `FloorScan` → `TurnPhone` → `Setup` → `ScanFront` |
| `FloorScanner.cs` | AR session on the back camera, horizontal plane detection, raycast from screen centre to pick the child's spot |
| `TurnTracker.cs` | Record phone attitude at the end of the scan; measure the turn; express floor plane + spot in the front camera's frame |
| `FrontCamera.cs` | Open the front camera, mirror the image, fill the screen |
| `PoseTracker.cs` | BlazePose on each frame through Unity Inference Engine, return 33 landmarks + visibility |
| `Overlay.cs` | Posture line (2D over the image) and floor star (3D camera matched to the front camera's field of view) |
| `Mascot.cs` | Mascot image and speech bubble text |
| `Measure.cs` | Shoulder tilt against gravity, ~0.5 s smoothing, visibility checks, whole-body-in-view check |

## How the floor gets from the back camera to the front camera

1. **Scan:** during the AR session, store the floor plane (height below the phone, normal) and the chosen spot in the phone's own coordinates, plus the phone's attitude from the sensor at that moment.
2. **Turn:** stop the AR session. While the parent turns the phone around and puts it down, read the attitude sensor. When the phone has been still for about 1 second, compute the rotation since the scan.
3. **Carry over:** rotate the stored plane and spot by that rotation into the front camera's frame. Assume the phone is put down where it was during the scan (small position error is accepted).
4. **Draw:** a Unity camera at the origin with the front camera's field of view draws the star on that plane, over the mirrored camera image.
5. **Check:** when the ankles are visible and close to the star, snap the star under them. If the scan was skipped or failed, place the star from the ankles only.

## Steps (build order)

The order follows plan section 7: get the risky parts working on real phones first.

### 1.1 Project setup
- Unity 6 LTS project on the Mac, URP, iOS and Android build modules.
- Packages: AR Foundation, ARKit XR Plugin, ARCore XR Plugin (XR Plug-in Management), Input System, Inference Engine.
- Camera permission texts (iOS `NSCameraUsageDescription`), portrait + landscape.
- Unity `.gitignore` and Git LFS for images, models and audio.
- **Done:** an empty scene builds and runs on the Android phone and the iPhone.

### 1.2 Front camera + BlazePose (day 1–3, riskiest)
- `FrontCamera.cs`: front camera, mirrored, fills the screen, handles orientation.
- `PoseTracker.cs`: BlazePose (detector + landmark model) through Inference Engine, GPU backend; debug mode draws all 33 points.
- **Done:** landmarks track the body on both phones; note FPS on each. If it cannot keep up, try the MediaPipe Unity Plugin before going on.

### 1.3 Posture line
- `Overlay.cs`: body line (head → shoulder mid → hip mid → ankle mid) plus shoulder and hip lines, aligned with the mirrored image.
- **Done:** the line keeps up with normal movement on the demo phone.

### 1.4 Shoulder tilt colour
- `Measure.cs`: angle of the line between landmarks 11 and 12 from horizontal (gravity from the attitude sensor); smooth over ~0.5 s; use only when visibility > 0.6.
- Hysteresis: turn orange above `threshold`, back below `threshold − margin` to stop flicker.
- **Done:** lifting one shoulder 10 times turns the line orange each time and back again.

### 1.5 Floor scan
- `FloorScanner.cs`: AR session, horizontal plane detection, raycast from the screen centre; preview star on the floor; "Skip" button.
- Hint if no floor after 5 s: "Point at the floor in a lit area".
- **Done:** finds the floor within 10 s on plain, patterned and rug floors.

### 1.6 Turn the phone + camera switch
- `TurnTracker.cs`: attitude at scan end, stillness detection after the turn, plane and spot carried into the front camera's frame.
- Stop the AR session fully before opening the front camera; retry once on failure.
- Placeholder "turn the phone around" screen with an arrow.
- **Done:** the switch works on both phones; the star lands on the scanned spot in 10 tries (note the error each time).

### 1.7 Setup hints + floor star
- Whole-body check: nose (0) and both ankles (27, 28) visible and inside the frame with a margin.
- Hints from landmark positions: too close or cut off → "Step back"; off-centre → "Move left" / "Move right" (mirrored correctly).
- Star shown on the scanned spot (or from the ankles); mascot: "Stand on the star!"
- Front camera field of view from `config.json`, tuned per test phone.
- **Done:** hints stop within 2 s of the body being fully in view; the star is under the feet at 2 m and 3 m.

### 1.8 Config, placeholders, start screen, mascot
- `config.json` + `AppConfig.cs`; `Assets/Content` with placeholder mascot (grey circle) and star.
- Start screen with "Start check"; mascot speech bubble with lines per state.
- `FlowController.cs` wires all five states.
- **Done:** swapping mascot image, colours and names needs no code change.

## Done when (plan section 2)

- [ ] The app installs and runs on one Android phone and one iPhone
- [ ] The floor scan finds the floor within 10 seconds on a normal floor
- [ ] After the turn, the star sits on the scanned spot: a person standing on it has the star under their feet
- [ ] If the scan is skipped or fails, the star still appears under the feet from the ankle landmarks
- [ ] The whole-body check stops hinting within 2 seconds of the body being fully in view
- [ ] The posture line keeps up with normal movement on the demo phone
- [ ] Lifting one shoulder turns the shoulder line orange; standing level turns it back
- [ ] Swapping the mascot image, colours and names needs no code change (config file and content folder only)
- [ ] Nothing is recorded or uploaded

## Testing (about 1.5 hours)

- One Android phone and one iPhone
- Normal room light and a dim room
- 2 m and 3 m from the phone; the whole body must fit
- Floor scan on 3 floor types (plain, patterned, rug); note time to find the floor
- Scan, turn and place the phone 10 times; note how far the star lands from the scanned spot
- Lift one shoulder 10 times: orange each time, back each time
- 3 people of different heights do the flow with no help; note whether the turn step confuses them

## Risks

| Risk | Fallback |
| --- | --- |
| Tracking lags on older phones | Lite model, lower resolution, GPU inference, demo on the fastest phone |
| BlazePose in Unity doesn't work well | MediaPipe Unity Plugin |
| Floor not found | Lighting hint, "Skip", ankle-based star |
| Star drifts after the turn | Put the phone down where scanned; snap to ankles; ankle-based star |
| Turn step confuses parents | Clear arrow screen; measure in testing; drop the scan if it hurts more than it helps |
| Camera switch slow or fails | Fully stop the AR session first; retry; "Tap to continue" |
| Shoulder line flickers | ~0.5 s smoothing + hysteresis margin |
| Design assets arrive late | Placeholders work alone; swap files on arrival |

## Deliverables

- Android APK and iPhone build (TestFlight or installed from Xcode)
- GitHub repository
- Screen recording of the flow as a backup for the live demo
