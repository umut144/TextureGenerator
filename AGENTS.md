# PolyTexture Agent Guide

Read `docs/ARCHITECTURE.md` and `docs/DOCUMENT_MODEL.md` before changing the
document model, procedural evaluation, persistence, or bakes.

## Ownership

- Authored sources, operation recipes, controls, and output bindings are
  canonical document data.
- Generator and operator results are derived. They must not be copied back into
  canonical sources unless the user explicitly invokes a Freeze/Make Editable
  operation.
- Accepted bakes are rebuildable derived data with source provenance. They are
  never edited as sources.
- Stable IDs are immutable technical identity. User-facing names may change
  without invalidating references.
- Authoring coordinates are resolution-independent. Pixel resolution belongs
  to preview and bake profiles, not source geometry.

## Verification

Run after changes:

```bash
dotnet build --no-restore
/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --path . -s res://tests/PolyTextureTestRunner.cs
/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --path . --editor --quit
git diff --check
```
