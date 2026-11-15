# Phases

The app is built in three phases. Each phase leaves a working app, and each builds on the code of the one before (nothing is thrown away). Source: [implementation plan](../implementation-plan.md), sections 1, 7 and 8.

| Phase | Goal | Done when | Details |
| --- | --- | --- | --- |
| 1 Prototype | The first 30 seconds: "Start check" → live posture line, shoulder turns orange | The 30-second checklist passes on Android Chrome + iPhone Safari | [phase-1-prototype.md](phase-1-prototype.md) |
| 2 Core check | Every measurement step of the check works and gives stable values | One person does the whole check and gets stable values 3 times in a row | [phase-2-core-check.md](phase-2-core-check.md) |
| 3 Full app | Results, parent screens, saving, reminders, voice | Usability targets met (4/5 finish unaided, 5/5 "see a doctor", 0/5 "diagnosis") | [phase-3-full-app.md](phase-3-full-app.md) |

## Status

| Phase | Status |
| --- | --- |
| 1 Prototype | Not started |
| 2 Core check | Not started |
| 3 Full app | Not started |

## Ground rules (all phases)

- **No design decisions in code.** Names, colours, text lines and thresholds live in `config.json`; images live in `/assets` and are loaded by file name. Placeholders until the design team (İrem, Furkan, Bekir) delivers.
- **Privacy.** Video frames are processed on the phone and discarded. Nothing is uploaded. Only angles and dates are ever saved (Phase 3).
- **Stack.** Vite + TypeScript, one canvas over the mirrored front-camera video, MediaPipe Pose Landmarker (lite model) in the browser, served over HTTPS.
- **Structure.** One page driven by a state machine (`flow.ts`); each storyboard step is a state, so later phases add states and modules without rewriting earlier ones.
