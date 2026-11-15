# Posture Check App

A mobile web app that checks a child's posture with the phone's front camera. Body tracking runs in the browser with MediaPipe Pose Landmarker; nothing is recorded or uploaded.

> The app and mascot names are placeholders and will live in `config.json`.

- [Implementation plan](docs/implementation-plan.md)
- [Phases and status](docs/phases/README.md)

## Stack

Vite + TypeScript · MediaPipe Pose Landmarker (`@mediapipe/tasks-vision`, lite model) · HTTPS hosting (GitHub Pages or Netlify)
