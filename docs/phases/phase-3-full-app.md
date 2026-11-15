# Phase 3 — Full app

**Goal:** turn the measurements into a clear, non-diagnostic result for the parent, a reward for the child, and the follow-up tools (report, specialist list, reminders), with voice and sound.

**Storyboard frames:** 2, 7, 8.
**Starts after:** Phase 2 gives stable values 3 times in a row.

## Scope

| Part | Frame | Notes |
| --- | --- | --- |
| Result logic | 7 | Flags from the five measurements |
| Badge for finishing | 7 | The child sees only the badge, never the flags |
| Parent result screen | 8 | "This is not a diagnosis." on every result |
| "Find a specialist" list + doctor report | 8 | Printable HTML page (browser "Save as PDF") |
| Saving results | 8 | IndexedDB/localStorage, angles and dates only |
| Child profiles | 2 | Pick/add a child before the check |
| Reminder + next-check date | 2, 8 | `.ics` calendar file; push only if installed as an app |
| Voice lines and sound effects | All | Audio files named by line id |

## Modules

| Module | Phase 3 work |
| --- | --- |
| `result.ts` | Measurements → flags → result text |
| `storage.ts` | Profiles, saved angles and dates |
| `report.ts` | Parent screen, specialist list, printable report, `.ics` reminder |
| `audio.ts` | Voice lines and sounds, keyed by line id |
| `flow.ts` | Add states: `profile`, `badge`, `parentResult` |

## Result rules (plan section 6)

- 0 flags: "Looks good. Next check in 3 months."
- 1+ flags: "We found signs worth showing a doctor. We suggest a check-up with a paediatrician or physiotherapist." Then the flagged signs in plain words.
- Every result screen says "This is not a diagnosis."
- Final wording and thresholds reviewed before the test (open question: can a physiotherapist check them? Ask the lecturer).

## Steps

1. **3.1 Result logic** — `result.ts` with unit tests on flag rules.
2. **3.2 Badge + parent result screen** — child/parent split.
3. **3.3 Storage + child profiles**.
4. **3.4 Report** — printable doctor report and specialist list.
5. **3.5 Reminders** — next-check date and `.ics` file.
6. **3.6 Voice and sound** — `audio.ts`, wire to mascot lines.
7. **3.7 Optional packaging** — PWA install (or Capacitor) if needed.

## Done when — usability test (5 adult testers, ~10 min each)

| What we measure | Target |
| --- | --- |
| Testers who finish with no help | 4 of 5 |
| Testers who say the next step is "see a doctor" when signs are found | 5 of 5 |
| Testers who think the app gave a diagnosis | 0 of 5 |

Plus: repeating one person's check 3 times gives the same flags.

## Needs from the design team

- Badge: PNG or SVG
- Result, referral and report screens: Figma frames
- Voice lines and sounds: MP3/M4A named by line id
- Result and referral wording: sheet, reviewed by the lecturer

## Risks

| Risk | Fallback |
| --- | --- |
| Result sounds like a diagnosis | "Not a diagnosis" on every result; wording reviewed before the test |
| Filming children | Test with adults; no video saved or sent |

## Deliverables

Result and parent screens, specialist list, report, saving, reminders, voice; usability test notes.
