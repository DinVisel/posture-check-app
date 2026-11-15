# Posture Check App — Implementation Plan

Nov 15, 2026 · @Arda · Revised Nov 15, 2026: stack changed from a web app to Unity, and the floor is scanned with the back camera before the check.

This plan covers the whole app in three phases. **Phase 1 is the prototype: from "Start check" to the live posture line on the child's body**, including the floor scan before it. Every part below is marked **Prototype**, **Prototype (simplified)** or **Full app only**.

Arda builds the app; the design team (İrem, Furkan, Bekir) owns all visuals, the mascot and the wording, so this plan makes no design decisions. The app and mascot names may change, so the plan says "the app" and "the mascot", and the code keeps both names in one config file.

## 1. Scope

The prototype needs 10 of the 22 parts, two of them simplified; everything after the live posture line is full app only.

| Part | Storyboard frame | Phase | Prototype note |
| --- | --- | --- | --- |
| Names of app and mascot in one config file | All | Prototype | Placeholder names until the team decides |
| Start screen with a "Start check" button | 2 | Prototype | Plain screen, final look comes later |
| Floor scan with the back camera | 2–3 | Prototype | Parent points the phone at the spot where the child will stand |
| Turn the phone around to the front camera | 3 | Prototype | Floor position carried over with the motion sensors |
| Front camera, mirrored, full screen | 3 | Prototype |  |
| Setup guidance until the whole body is in view | 3 | Prototype | Text hints: "step back", "move left" |
| Floor marker on the scanned spot | 3 | Prototype | Drawn in 3D on the scanned floor; falls back to the ankle landmarks if the scan fails |
| Mascot on screen with a speech bubble | 3–8 | Prototype (simplified) | Placeholder image + text, no voice or animation |
| Live posture line over the body | 4 | Prototype | The core of the demo |
| Joints outside range turn orange | 4 | Prototype (simplified) | Front view, shoulder tilt only, placeholder threshold |
| Mascot copies the child's posture | 4 | Full app only |  |
| Side-view scan | 4 | Full app only |  |
| Guided poses with ghost outline and hold timer | 5 | Full app only |  |
| Walking check on stepping stones | 6 | Full app only |  |
| Full measurements and result logic | 7 | Full app only |  |
| Badge for finishing | 7 | Full app only |  |
| Parent result screen | 8 | Full app only |  |
| "Find a specialist" list and doctor report | 8 | Full app only |  |
| Reminder notification and next-check date | 2, 8 | Full app only |  |
| Child profiles | 2 | Full app only |  |
| Voice lines and sound effects | All | Full app only |  |
| Saving results on the phone | 8 | Full app only |  |

## 2. The prototype: from "Start check" to the posture line

The prototype ends when the posture line is drawn live on the child's body and the uneven shoulder turns orange. That is the core interaction from Frame 4, reached in about 45 seconds: about 15 seconds of floor scan, then the original 30-second child flow.

| Time | What the user does | What the app does |
| --- | --- | --- |
| 0–5 s | Parent opens the app and taps "Start check" | Asks for the camera; the mascot greets the parent in a speech bubble |
| 5–15 s | Parent stands where the phone will go and points the back camera at the floor where the child will stand | Detects the floor, shows a preview of the star on it; "Floor found!" |
| 15–20 s | Parent turns the phone around and puts it on the shelf, screen facing the room | Tracks the turn with the motion sensors, then switches to the mirrored front camera |
| 20–30 s | Child steps into view | Hints until the whole body is in view ("Step back", "Move left"); star appears on the scanned spot; mascot: "Stand on the star!" |
| 30–45 s | Child stands naturally, then lifts one shoulder | Posture line follows the body live; the shoulder line turns orange when tilted past the placeholder threshold |

The scan step adds time and a phone-handling step to the flow. The prototype tests whether that is worth it: if the scan fails or the parent skips it, the app places the star from the ankle landmarks instead, so the child flow never breaks.

**Done when**

- [ ] The app installs and runs on one Android phone and one iPhone
- [ ] The floor scan finds the floor within 10 seconds on a normal floor
- [ ] After the turn, the star sits on the scanned spot: a person standing on it has the star under their feet
- [ ] If the scan is skipped or fails, the star still appears under the feet from the ankle landmarks
- [ ] The whole-body check stops hinting within 2 seconds of the body being fully in view
- [ ] The posture line keeps up with normal movement on the demo phone
- [ ] Lifting one shoulder turns the shoulder line orange; standing level turns it back
- [ ] Swapping the mascot image, colours and names needs no code change (config file and content folder only)
- [ ] Nothing is recorded or uploaded

**Not in the prototype:** side view, guided poses, walking, results, parent screens, voice, saving. The code is structured so these slot in later without rewriting the prototype.

## 3. Tech approach and stack

Build a native mobile app in Unity. The back camera scans the floor with AR Foundation (ARKit on iPhone, ARCore on Android); the front camera then tracks the body with the BlazePose model running on the phone. The prototype uses the same stack as the full app, so nothing is thrown away.

**Why Unity:** the AR parts (star on the floor, ghost outline, stepping stones, 3D mascot) are easier to build and look better in a 3D engine, and one Unity project builds for both iPhone and Android. The team has a Mac for the iPhone builds.

**Why scan with the back camera, then turn to the front:** the child must see the screen, so the check itself uses the front camera. But AR Foundation can only find the floor with the back camera, and neither ARKit nor ARCore tracks a body with the front camera (ARKit body tracking is back camera only; ARCore has none). So:

1. The back camera finds the floor plane and the spot where the child will stand.
2. When the parent turns the phone around, AR tracking stops, and the phone's motion sensor measures the turn.
3. The floor plane and spot are carried into the front camera's view, assuming the phone is put down where it was during the scan.
4. BlazePose on the front camera gives the 33 body landmarks; the posture line is drawn over the image and the star is drawn in 3D on the floor.

The XR Interaction Toolkit is not used: it handles controller and hand input in XR scenes, not body tracking. It can be added later if a feature needs it.

| Part | Tool | Prototype |
| --- | --- | --- |
| Engine | Unity 6 LTS, URP, builds for iOS and Android | Yes |
| Floor scan | AR Foundation + ARKit XR Plugin + ARCore XR Plugin, horizontal plane detection and raycast | Yes |
| Turn tracking | Phone attitude sensor (Unity Input System `AttitudeSensor`) | Yes |
| Front camera | `WebCamTexture`, front-facing, mirrored | Yes |
| Body tracking | Unity Inference Engine (formerly Sentis) running BlazePose, 33 landmarks per frame (same numbering as MediaPipe). Fallback: MediaPipe Unity Plugin | Yes |
| Config | `StreamingAssets/config.json`: app name, mascot name, colours, text lines, thresholds, camera field of view | Yes |
| Assets | `Assets/Content` folder the design team fills; code loads by file name | Yes, with placeholders |
| Builds | Android APK shared directly; iPhone through Xcode on the Mac, TestFlight for testers | Yes |
| Sound and voice | Audio files from the design team, Unity `AudioSource` | No |
| Saving results | JSON file in the app's data folder, angles and dates only | No |
| Report export | PDF made on the phone, shared through the phone's share menu | No |
| Reminders | Local notifications (Unity Mobile Notifications package) | No |

Storyboard fix: BlazePose tracks 33 landmarks, not 17. Change Frame 4 to "33 body points" or "key body points".

## 4. App structure

The app is one Unity scene with a state machine: each storyboard step is a state. The prototype builds the first five states and the scripts under them, so later steps plug in without rewriting.

&#91;embedded content: check flow · prototype part marked\]

Prototype states: `Start` → `FloorScan` → `TurnPhone` → `Setup` → `ScanFront`. In the prototype, "Posture scan" is the front view only and ends there; the side view and everything after it are full app only.

| Script | Job | Phase |
| --- | --- | --- |
| `AppConfig.cs` | Loads names, colours, text lines, thresholds and camera settings from `config.json` | Prototype |
| `FlowController.cs` | State machine: current step, mascot line, when to move on | Prototype (5 states) |
| `FloorScanner.cs` | Runs the AR session on the back camera, finds the floor and the child's spot | Prototype |
| `TurnTracker.cs` | Measures the turn with the attitude sensor, moves the floor into the front camera's view | Prototype |
| `FrontCamera.cs` | Opens the front camera, mirrors the image | Prototype |
| `PoseTracker.cs` | Runs BlazePose on each camera frame, returns the 33 landmarks | Prototype |
| `Overlay.cs` | Draws the posture line and the floor star; later the ghost and stones | Prototype (line + star) |
| `Mascot.cs` | Shows the mascot and its speech bubble; later moods and copying | Prototype (image + bubble) |
| `Measure.cs` | Angles, smoothing, visibility checks | Prototype (shoulder tilt only) |
| `ResultLogic.cs` | Turns measurements into flags and the result | Full app |
| `Storage.cs` | Saves angles and dates on the phone | Full app |
| `Report.cs` | Parent screen, specialist list, PDF report | Full app |
| `AudioPlayer.cs` | Voice lines and sounds | Full app |

**Data and privacy (all phases):** camera frames are processed on the phone and thrown away. Nothing is sent to a server; the full app saves only angles and dates.

## 5. Handoff from the design team

The code ships with neutral placeholders and loads every visual, name and line from the config file and the content folder. The design team replaces those files; the developer makes no design choices. A swap needs a new build, but no code change.

| What the code needs | Format | Prototype | Placeholder until delivered |
| --- | --- | --- | --- |
| App name and mascot name | Text in `config.json` | Yes | "App", "Mascot" |
| Mascot, idle pose | PNG, transparent background, about 512 px (or a 3D model, if the team chooses) | Yes | Grey circle |
| Colours: UI, posture line normal, posture line outside range | Hex values in `config.json` | Yes | Grey, white, orange |
| Floor marker (star) | PNG with transparent background | Yes | Plain star shape |
| Start, scan and turn-the-phone screens, speech bubble layout | Figma frames | Yes | Plain button, text and arrow |
| Mascot's lines for the prototype flow | Sheet: line id, text | Yes | Storyboard text |
| Mascot moods (copying, happy, waving) | Sprite sheet, or 3D model with animations | No |  |
| Ghost outline, stepping stones, badge | PNG (or 3D model) | No |  |
| Result, referral and report screens | Figma frames | No |  |
| Voice lines and sounds | MP3 or WAV, named by line id | No |  |
| Result and referral wording | Sheet, reviewed by the lecturer | No |  |

Agree file names and whether the mascot is 2D or 3D in week 1, so a new asset is a file swap, not a code change.

## 6. Measurements and result logic

The full app measures five values; the prototype uses only shoulder tilt, live, to colour the line. If any value is past its threshold, the result says "signs found, we suggest a check-up". Thresholds are blanks: fill them from published sources and label them in the app as not clinically checked.

| Measure | Frame | Landmarks (BlazePose index) | How it is calculated | Flag when | Prototype |
| --- | --- | --- | --- | --- | --- |
| Shoulder tilt | 4, front | shoulders 11, 12 | Angle of the shoulder line from horizontal | over \[\_\_\]° | Yes, live colour only |
| Hip tilt | 4, front | hips 23, 24 | Angle of the hip line from horizontal | over \[\_\_\]° | No |
| Head forward | 4, side | ear 7 or 8, shoulder 11 or 12 | Ear's distance in front of the shoulder, divided by torso length | over \[\_\_\] | No |
| Knee alignment | 4, front | hip, knee, ankle 23-25-27 and 24-26-28 | Angle at each knee | outside \[**\]–\[**\]° | No |
| Foot angle when walking | 6 | heel 29, 30 and foot tip 31, 32 | Each foot's angle to the walking direction, averaged over steps | over \[\_\_\]° | No |

"Horizontal" uses the phone's gravity direction from the motion sensor, so a phone leaning on the shelf does not tilt the measurement.

**Rules for a reliable value (full app)**

- Use a landmark only when its visibility is above 0.6.
- Take the median over a 2-second still window, not one frame.
- After two failed tries, show "could not measure" instead of guessing.

**Result (full app)**

- 0 flags: "Looks good. Next check in 3 months."
- 1 or more flags: "We found signs worth showing a doctor. We suggest a check-up with a paediatrician or physiotherapist." Then the flagged signs in plain words.
- Every result screen says "This is not a diagnosis." The child sees only the badge, never the flags.

Open question: who reviews the thresholds and wording? Ask the lecturer whether a physiotherapist can check them before the full-app test.

## 7. Phases and timeline

Three phases, built in this order so each one leaves a working app. The dates are assumptions; send the real prototype and final deadlines and they move.

&#91;embedded content: timeline · 3 phases, prototype first\]

| Phase | Builds | Done when |
| --- | --- | --- |
| 1 Prototype | Unity project and builds, config and content loading, start screen, floor scan, turn tracking, front camera, BlazePose, setup hints, floor star, live posture line, shoulder tilt colour, mascot image + bubble | The checklist in section 2 passes |
| 2 Core check | Side-view scan, mascot copying the posture, guided poses with ghost and hold timer, walking check, all five measurements | One person can do the whole check and get stable values 3 times in a row |
| 3 Full app | Result logic, badge, parent result screen, specialist list, PDF report, saving, reminders, voice and sounds | The usability targets in section 8 are met |

**Order inside Phase 1:** empty builds on both phones, then front camera and BlazePose on both phones (day 1–3), then the posture line and shoulder colour, then the floor scan and the turn, then setup hints and the star, then config, placeholders and the start screen. If time runs short, the posture line is the one part the demo cannot lose; the floor scan can fall back to the ankle-based star.

**What the developer needs from the design team for Phase 1:** the "Prototype: yes" rows in section 5, by Nov 20.

## 8. Testing per phase

Testers are adults (classmates, Hamit, family members) acting out the flow. Test with children only if the lecturer approves and a parent agrees and is present.

**Phase 1, prototype (technical checks, about 1.5 hours)**

- One Android phone and one iPhone
- Normal room light and a dim room
- 2 m and 3 m from the phone; the whole body must fit
- Floor scan on 3 floor types (plain, patterned, rug); note time to find the floor
- Scan, turn and place the phone 10 times: note how far the star lands from the scanned spot
- Lift one shoulder 10 times: the line turns orange each time and back again
- 3 people of different heights do the flow with no help; note whether the turn step confuses them

**Phase 2 and 3, full app (usability test, 5 testers, about 10 minutes each)**

1. Hand the phone over at "Start check" and say nothing else.
2. Note every time the tester stops, looks confused or asks for help.
3. At the result screen ask: "What would you do next?" and "Did the app say your child has a condition?"
4. Repeat one person's check 3 times: the flags should come out the same.

| What we measure (full app) | Target |
| --- | --- |
| Testers who finish with no help | 4 of 5 |
| Testers who say the next step is "see a doctor" when signs are found | 5 of 5 |
| Testers who think the app gave a diagnosis | 0 of 5 |

## 9. Risks and fallbacks

For the prototype the main risks are lag, the camera switch and the star drifting after the turn; side-view tracking is the main full-app risk.

| Risk | Phase | What goes wrong | Fallback |
| --- | --- | --- | --- |
| Tracking lags on older phones | Prototype | Line trails behind the body | Lite BlazePose model, lower camera resolution, GPU inference, demo on the fastest phone |
| Front-camera body tracking unavailable | Prototype | No posture line | Test BlazePose on both phones in the first days; switch to the MediaPipe Unity Plugin if needed |
| Floor not found | Prototype | Scan never finishes | Hint to point at a lit, textured floor; "Skip" button; ankle-based star |
| Star drifts after the turn | Prototype | Star is not under the feet | Ask to put the phone down where it was scanned; snap the star to the ankles when they are close; ankle-based star |
| Turn step confuses parents | Prototype | Flow breaks, parent gives up | Clear arrow animation and line from the design team; measure in testing; drop the scan if it hurts more than it helps |
| Camera switch is slow or fails | Prototype | Black screen after the turn | Stop the AR session fully before opening the front camera; retry once; show a "Tap to continue" button |
| Front camera field of view unknown | Prototype | Star drawn too big or small | Field of view per test phone in `config.json`; tune on both phones |
| Room too small for the whole body | Prototype | Setup never finishes | Hints to step back; allow landscape mode |
| Shoulder line flickers orange | Prototype | Looks unreliable in the demo | Smooth the angle over about 0.5 s and add a small margin before switching colour |
| Design assets arrive late | Prototype | Demo looks unfinished | Placeholders work on their own; swap files the day they arrive |
| One developer, many features | Full app | Full app not done in time | Build in the order of section 7; cut walking first if needed |
| Side view tracks badly | Full app | Head-forward value jumps | Median over 2 s, visibility check, "could not measure" |
| Result sounds like a diagnosis | Full app | Parents worry or misread it | "Not a diagnosis" on every result; wording reviewed before the test |
| Filming children | All | Test can't go ahead | Test with adults; no video saved or sent |

## 10. Prototype demo and deliverables

The demo is the flow from section 2, done live with one teammate as the child.

1. Open the app on the demo phone and tap "Start check".
2. Point the back camera at the floor where the teammate will stand until the star appears on it.
3. Turn the phone around and put it on the shelf; the teammate steps back until the hints stop.
4. The star is under their feet and the mascot says to stand on it.
5. They stand naturally: the posture line follows them.
6. They lift one shoulder: the shoulder line turns orange. They level it: it turns back.

**Deliverables**

| Phase | Deliverable |
| --- | --- |
| 1 Prototype | Android APK and iPhone build (TestFlight or installed from Xcode), GitHub repository, screen recording of the flow as a backup for the live demo |
| 2 Core check | Side view, guided poses, walking check, measurements |
| 3 Full app | Result and parent screens, specialist list, report, saving, reminders, voice; usability test notes |

## Sources

- [AR Foundation](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.0/manual/index.html), Unity: plane detection and raycasts
- [Unity Inference Engine](https://docs.unity3d.com/Packages/com.unity.ai.inference@latest), Unity: running ML models such as BlazePose on the phone
- [Pose landmark detection guide](https://developers.google.com/edge/mediapipe/solutions/vision/pose_landmarker) (landmark list 0–32), Google AI Edge
- [ARKit body tracking](https://developer.apple.com/documentation/arkit/arbodytrackingconfiguration), Apple: back camera only
