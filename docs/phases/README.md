# Phases

The app is built in three phases. Each phase leaves a working app, and each builds on the code of the one before (nothing is thrown away). Source: [implementation plan](../implementation-plan.md), sections 1, 7 and 8.

| Phase | Goal | Done when | Details |
| --- | --- | --- | --- |
| 1 Prototype | "Start check" → floor scan (back camera) → turn the phone → live posture line, shoulder turns orange | The prototype checklist passes on one Android phone and one iPhone | [phase-1-prototype.md](phase-1-prototype.md) |
| 2 Core check | Every measurement step of the check works and gives stable values | One person does the whole check and gets stable values 3 times in a row | [phase-2-core-check.md](phase-2-core-check.md) |
| 3 Full app | Results, parent screens, saving, reminders, voice | Usability targets met (4/5 finish unaided, 5/5 "see a doctor", 0/5 "diagnosis") | [phase-3-full-app.md](phase-3-full-app.md) |

## Status

| Phase | Status |
| --- | --- |
| 1 Prototype | In progress: 1.1 project set up (test builds on phones pending) |
| 2 Core check | Not started |
| 3 Full app | Not started |

## Ground rules (all phases)

- **No design decisions in code.** Names, colours, text lines and thresholds live in `StreamingAssets/config.json`; images live in `Assets/Content` and are loaded by file name. Placeholders until the design team (İrem, Furkan, Bekir) delivers.
- **Privacy.** Camera frames are processed on the phone and discarded. Nothing is uploaded. Only angles and dates are ever saved (Phase 3).
- **Stack.** Unity 6 LTS for iOS and Android. AR Foundation (ARKit/ARCore) scans the floor with the back camera; the phone is turned around and BlazePose (Unity Inference Engine) tracks the body on the front camera. No XR Interaction Toolkit.
- **Structure.** One scene driven by a state machine (`FlowController.cs`); each storyboard step is a state, so later phases add states and modules without rewriting earlier ones.
