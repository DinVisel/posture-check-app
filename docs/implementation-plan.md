# Posture Check App — Implementation Plan

Nov 15, 2026 · @Arda

This plan covers the whole app in three phases. **Phase 1 is the prototype: the first 30 seconds of the flow**, from "Start check" to the live posture line on the child's body. Every part below is marked **Prototype**, **Prototype (simplified)** or **Full app only**.

Arda builds the app; the design team (İrem, Furkan, Bekir) owns all visuals, the mascot and the wording, so this plan makes no design decisions. The app and mascot names may change, so the plan says "the app" and "the mascot", and the code keeps both names in one config file.

## 1. Scope

The prototype needs 8 of the 20 parts, two of them simplified; everything after the live posture line is full app only.

| Part | Storyboard frame | Phase | Prototype note |
| --- | --- | --- | --- |
| Names of app and mascot in one config file | All | Prototype | Placeholder names until the team decides |
| Start screen with a "Start check" button | 2 | Prototype | Plain screen, final look comes later |
| Front camera, mirrored, full screen | 3 | Prototype |  |
| Setup guidance until the whole body is in view | 3 | Prototype | Text hints: "step back", "move left" |
| Floor marker placed under the child's feet | 3 | Prototype | Placed from the ankle landmarks |
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

## 2. The prototype: the first 30 seconds

The prototype ends when the posture line is drawn live on the child's body and the uneven shoulder turns orange. That is the core interaction from Frame 4, reached in about 30 seconds.

| Time | What the user does | What the app does |
| --- | --- | --- |
| 0–5 s | Parent opens the link and taps "Start check" | Asks for the camera; the mascot greets the child in a speech bubble |
| 5–15 s | Puts the phone on a shelf and steps back | Live mirrored camera; hints until the whole body is in view ("Step back", "Move left") |
| 15–20 s | Child stands still | Floor marker appears under the feet; mascot: "Stand on the star!" |
| 20–30 s | Child stands naturally, then lifts one shoulder | Posture line follows the body live; the shoulder line turns orange when tilted past the placeholder threshold |

**Done when**

- [ ] The link opens on one Android phone (Chrome) and one iPhone (Safari) over HTTPS
- [ ] The whole-body check stops hinting within 2 seconds of the body being fully in view
- [ ] The posture line keeps up with normal movement on the demo phone
- [ ] Lifting one shoulder turns the shoulder line orange; standing level turns it back
- [ ] Swapping the mascot image, colours and names needs no code change (config file and asset folder only)
- [ ] Nothing is recorded or uploaded

**Not in the prototype:** side view, guided poses, walking, results, parent screens, voice, saving. The code is structured so these slot in later without rewriting the prototype.

## 3. Tech approach and stack

Build a mobile web app that opens from a link, with Google's MediaPipe Pose Landmarker tracking the body in the browser. The prototype uses the same stack as the full app, so nothing is thrown away.

**Why a web app:** one developer, no app store, and the whole team can open it on their own phones. It can be wrapped as an installable app later (a PWA, or Capacitor for the stores) if needed.

**Why the AR layer is drawn on the camera image:** the child must see the screen, so the app uses the front camera. Room-anchored AR (ARKit, ARCore, WebXR) works with the back camera, and Safari on iPhone does not support WebXR ([WebXR device support](https://abratabia.com/webxr-games/webxr-device-support.php)). So the marker, posture line and later the ghost and stones are placed relative to the body landmarks, with the feet giving the floor line.

| Part | Tool | Prototype |
| --- | --- | --- |
| App | Vite + TypeScript, one HTML canvas over the video | Yes |
| Body tracking | [MediaPipe Pose Landmarker](https://ai.google.dev/edge/mediapipe/solutions/vision/pose_landmarker/web_js) (`@mediapipe/tasks-vision`, lite model), 33 landmarks per frame | Yes |
| Config | `config.json`: app name, mascot name, colours, text lines, thresholds | Yes |
| Assets | `/assets` folder the design team fills; code loads by file name | Yes, with placeholders |
| Hosting | GitHub Pages or Netlify (camera needs HTTPS) | Yes |
| Sound and voice | Audio files from the design team | No |
| Saving results | Browser IndexedDB or localStorage, angles and dates only | No |
| Report export | Printable HTML page (browser "Save as PDF") | No |
| Reminders | Calendar file (.ics) the parent adds; push notifications only if installed as an app | No |

Storyboard fix: MediaPipe tracks 33 landmarks, not 17. Change Frame 4 to "33 body points" or "key body points".

## 4. App structure

The app is one page with a state machine: each storyboard step is a state. The prototype builds the first three states and the modules under them, so later steps plug in without rewriting.

&#91;embedded content: check flow · prototype part marked\]

In the prototype, "Posture scan" is the front view only and ends there; the side view and everything after it are full app only.

| Module | Job | Phase |
| --- | --- | --- |
| `config.ts` | Loads names, colours, text lines and thresholds from `config.json` | Prototype |
| `flow.ts` | State machine: current step, mascot line, when to move on | Prototype (3 states) |
| `camera.ts` | Opens the front camera, mirrors the image | Prototype |
| `pose.ts` | Runs MediaPipe on each video frame, returns the 33 landmarks | Prototype |
| `overlay.ts` | Draws on the canvas: posture line and floor marker; later the ghost and stones | Prototype (line + marker) |
| `mascot.ts` | Shows the mascot and its speech bubble; later moods and copying | Prototype (image + bubble) |
| `measure.ts` | Angles, smoothing, visibility checks | Prototype (shoulder tilt only) |
| `result.ts` | Turns measurements into flags and the result | Full app |
| `storage.ts` | Saves angles and dates on the phone | Full app |
| `report.ts` | Parent screen, specialist list, printable report | Full app |
| `audio.ts` | Voice lines and sounds | Full app |

**Data and privacy (all phases):** video frames are processed on the phone and thrown away. Nothing is sent to a server; the full app saves only angles and dates.

## 5. Handoff from the design team

The code ships with neutral placeholders and loads every visual, name and line from the config file and the asset folder. The design team replaces those files; the developer makes no design choices.

| What the code needs | Format | Prototype | Placeholder until delivered |
| --- | --- | --- | --- |
| App name and mascot name | Text in `config.json` | Yes | "App", "Mascot" |
| Mascot, idle pose | PNG or SVG, transparent background, about 512 px | Yes | Grey circle |
| Colours: UI, posture line normal, posture line outside range | Hex values in `config.json` | Yes | Grey, white, orange |
| Floor marker | PNG or SVG | Yes | Plain ellipse |
| Start screen and speech bubble layout | Figma frame | Yes | Plain button and text box |
| Mascot's lines for the first 30 seconds | Sheet: line id, text | Yes | Storyboard text |
| Mascot moods (copying, happy, waving) | Sprite sheet or Lottie JSON | No |  |
| Ghost outline, stepping stones, badge | PNG or SVG | No |  |
| Result, referral and report screens | Figma frames | No |  |
| Voice lines and sounds | MP3 or M4A, named by line id | No |  |
| Result and referral wording | Sheet, reviewed by the lecturer | No |  |

Agree file names in week 1 so a new asset is a file swap, not a code change.

## 6. Measurements and result logic

The full app measures five values; the prototype uses only shoulder tilt, live, to colour the line. If any value is past its threshold, the result says "signs found, we suggest a check-up". Thresholds are blanks: fill them from published sources and label them in the app as not clinically checked.

| Measure | Frame | Landmarks (MediaPipe index) | How it is calculated | Flag when | Prototype |
| --- | --- | --- | --- | --- | --- |
| Shoulder tilt | 4, front | shoulders 11, 12 | Angle of the shoulder line from horizontal | over \[\_\_\]° | Yes, live colour only |
| Hip tilt | 4, front | hips 23, 24 | Angle of the hip line from horizontal | over \[\_\_\]° | No |
| Head forward | 4, side | ear 7 or 8, shoulder 11 or 12 | Ear's distance in front of the shoulder, divided by torso length | over \[\_\_\] | No |
| Knee alignment | 4, front | hip, knee, ankle 23-25-27 and 24-26-28 | Angle at each knee | outside \[**\]–\[**\]° | No |
| Foot angle when walking | 6 | heel 29, 30 and foot tip 31, 32 | Each foot's angle to the walking direction, averaged over steps | over \[\_\_\]° | No |

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
| 1 Prototype | Config and asset loading, start screen, camera, setup hints, floor marker, live posture line, shoulder tilt colour, mascot image + bubble | The 30-second checklist in section 2 passes |
| 2 Core check | Side-view scan, mascot copying the posture, guided poses with ghost and hold timer, walking check, all five measurements | One person can do the whole check and get stable values 3 times in a row |
| 3 Full app | Result logic, badge, parent result screen, specialist list, printable report, saving, reminders, voice and sounds | The usability targets in section 8 are met |

**Order inside Phase 1:** camera and MediaPipe on both phones first (day 1–2), then the posture line, then setup hints and the floor marker, then config, placeholders and the start screen. If time runs short, the posture line is the one part the demo cannot lose.

**What the developer needs from the design team for Phase 1:** the "Prototype: yes" rows in section 5, by Nov 20.

## 8. Testing per phase

Testers are adults (classmates, Hamit, family members) acting out the flow. Test with children only if the lecturer approves and a parent agrees and is present.

**Phase 1, prototype (technical checks, about 1 hour)**

- One Android phone in Chrome and one iPhone in Safari
- Normal room light and a dim room
- 2 m and 3 m from the phone; the whole body must fit
- Lift one shoulder 10 times: the line turns orange each time and back again
- 3 people of different heights do the first 30 seconds with no help

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

For the prototype the main risks are lag and the camera on iPhone; side-view tracking is the main full-app risk.

| Risk | Phase | What goes wrong | Fallback |
| --- | --- | --- | --- |
| Tracking lags on older phones | Prototype | Line trails behind the body | Lite model, lower video resolution, demo on the fastest phone |
| Camera will not start on iPhone | Prototype | No demo on iOS | HTTPS from day 1; test Safari in the first days |
| Room too small for the whole body | Prototype | Setup never finishes | Hints to step back; allow landscape mode |
| Shoulder line flickers orange | Prototype | Looks unreliable in the demo | Smooth the angle over about 0.5 s and add a small margin before switching colour |
| Design assets arrive late | Prototype | Demo looks unfinished | Placeholders work on their own; swap files the day they arrive |
| One developer, many features | Full app | Full app not done in time | Build in the order of section 7; cut walking first if needed |
| Side view tracks badly | Full app | Head-forward value jumps | Median over 2 s, visibility check, "could not measure" |
| Result sounds like a diagnosis | Full app | Parents worry or misread it | "Not a diagnosis" on every result; wording reviewed before the test |
| Filming children | All | Test can't go ahead | Test with adults; no video saved or sent |

## 10. Prototype demo and deliverables

The demo is the 30-second flow from section 2, done live with one teammate as the child.

1. Open the link on the demo phone and tap "Start check".
2. Put the phone on a shelf; the teammate steps back until the hints stop.
3. The floor marker appears under their feet and the mascot says to stand on it.
4. They stand naturally: the posture line follows them.
5. They lift one shoulder: the shoulder line turns orange. They level it: it turns back.

**Deliverables**

| Phase | Deliverable |
| --- | --- |
| 1 Prototype | Working link (HTTPS), GitHub repository, 30-second screen recording as a backup for the live demo |
| 2 Core check | Side view, guided poses, walking check, measurements |
| 3 Full app | Result and parent screens, specialist list, report, saving, reminders, voice; usability test notes |

## Sources

- [Pose landmark detection guide for Web](https://ai.google.dev/edge/mediapipe/solutions/vision/pose_landmarker/web_js), Google AI Edge
- [Pose landmark detection guide](https://developers.google.com/edge/mediapipe/solutions/vision/pose_landmarker) (landmark list 0–32), Google AI Edge
- [WebXR device support](https://abratabia.com/webxr-games/webxr-device-support.php), on Safari for iPhone not supporting WebXR
