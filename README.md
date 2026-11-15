# Posture Check App

A mobile AR app that checks a child's posture. The parent scans the floor with the back camera, turns the phone around, and the app tracks the child's body with the front camera. Body tracking runs on the phone; nothing is recorded or uploaded.

> The app and mascot names are placeholders and will live in `config.json`.

- [Implementation plan](docs/implementation-plan.md)
- [Phases and status](docs/phases/README.md)

## Stack

Unity 6 LTS (iOS + Android) · AR Foundation with ARKit / ARCore (floor scan) · Unity Inference Engine running BlazePose (body tracking, front camera)

## Repository

Binary assets (images, models, audio) are stored with Git LFS. Run `git lfs install` once after cloning.
