# PolyTexture

**A vector-first, procedural texture authoring tool for finished 3D meshes, built with Godot 4 and C#.**

Instead of painting pixels, artists author *resolution-independent semantic information* (regions, paths, rules) and
let a deterministic evaluator turn it into textures. Raster images and shaders are evaluation and export targets, never
the source of truth. Change the resolution, the seed, or a single parameter, and the whole result updates without
losing quality or repainting anything.

## What it does

- **Procedural, non-destructive workflow.** A texture is a stack of Sources, Generators, Operators and Filters. Every
  step stays editable, and results are derived live from the canonical data.
- **Resolution-independent authoring.** All geometry is stored in physical units (centimeters). Preview and bake
  resolution are separate evaluation settings, so changing resolution never rescales or degrades authored data.
- **Deterministic output.** The same document and seed always produce the same result, which makes bakes reproducible,
  testable and diff-able.
- **Semantic outputs instead of baked-in channels.** Color, Height and named Masks are authored as independent
  information. Normal maps are always *derived* from Height, and RGBA channel packing is only an export optimization.
- **Live canvas, inspector and outliner** with Bezier path editing, multi-point selection, undo/redo history and
  a debug heatmap view for intermediate scalar fields.

## Tool model

| Category | Purpose | Examples |
| --- | --- | --- |
| **Source** | Authored points, paths and regions | Rectangle Region, Ellipse Region, Crack Line |
| **Generator** | Creates new structure from inputs | Sweep, Repeat Grid, Branch, Scatter |
| **Operator** | Transforms or combines existing structure | Mirror |
| **Filter** | Changes a generated field | Invert, Edge Falloff |
| **Output** | Gives an evaluated field its meaning | Color, Height, named Mask |

Low-level tools give technical artists full control. The architecture is designed so that reusable, versioned
**Recipes** and higher-level domain tools (for example "Cracked Surface" or "Mold Colonies") can later be composed from
the same primitives rather than introducing a second authoring model.

## Example results (implemented vertical slices)

- **Brick wall:** Rectangle Region, Repeat Grid with staggered rows, Invert for mortar, exported as separate Mask and
  Height maps.
- **Cracks:** hand-drawn semantic crack paths with per-point width, deterministic recursive Branch generation and
  Edge Falloff for a physically sized gradient.
- **Mold colonies:** Ellipse Region prototypes distributed by a clustered, seeded Scatter with scale and rotation
  variation, optionally restricted to a bounding region.

## Engineering highlights

- **Clean separation of canonical data and derived data.** Sources, operations and output bindings are the only
  persisted truth. Evaluation results, previews and bakes are rebuildable and carry provenance.
- **Pure evaluator.** A deterministic evaluation stage never mutates the document; canvas, inspector, bake service
  and export all consume the same evaluated result.
- **Stable IDs everywhere.** References use immutable technical IDs, so renaming never breaks the graph. Duplication
  remaps references atomically, and deletion is dependency-aware.
- **Graph validation.** Missing inputs, incompatible port types and cycles are rejected before evaluation.
- **Explicit schema policy.** The document format is versioned, and unsupported schema versions are rejected instead
  of being silently migrated.
- **Automated tests.** A headless test runner with 28 tests covers serialization round trips, determinism of every
  generator, validation, dependency handling and baking (including derived normal maps).

Roughly 11,000 lines of C# across the document model, evaluator, renderer, bake and export services, and the editor UI.

## Tech stack

- Godot 4.7 (Forward Plus renderer) with C# / .NET
- JSON document persistence, PNG export for baked maps

## Getting started

1. Install the **.NET-enabled (Mono) build of Godot 4.7**.
2. Clone the repository and open `project.godot` in the Godot editor.
3. Run the project. It starts in `scenes/PolyTextureWorkspace.tscn`.

Saved documents go to `savefiles/`, and PNG bakes are written to `output/`. Both folders are git-ignored.

### Build and test

```bash
dotnet build --no-restore
godot --headless --path . -s res://tests/PolyTextureTestRunner.cs
```

## Documentation

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md): product vocabulary, evaluation boundary, dependency rules and
  tool abstraction levels
- [`docs/DOCUMENT_MODEL.md`](docs/DOCUMENT_MODEL.md): canonical document model, identity, coordinates and outputs
- [`AGENTS.md`](AGENTS.md): ownership rules and verification steps for contributors

## Status and roadmap

PolyTexture is an actively developed personal project. The next planned slice covers object-bound symbols and decals
(for example a pair of books) together with reusable, versioned material Recipes, plus a library of material-bound
procedural textures and animation-ready reveal metadata for runtime shaders.
