# PolyTexture Architecture

PolyTexture is a vector-first procedural surface authoring tool. Its canonical
documents describe resolution-independent sources and deterministic operations.
Shaders and raster images are evaluation targets, not authoring sources of
truth.

Canonical surface coordinates use centimeters. Preview and bake resolution are
separate pixel-space evaluation settings and never rescale authored geometry.

## Product vocabulary

- **Draw** is an interaction mode that creates or edits Sources.
- **Source** owns authored points, paths, regions, constants, or references and
  has no upstream detail input.
- **Generator** creates additional structure from inputs, such as Sweep,
  Repeat, Scatter, or Branch.
- **Operator** transforms or combines existing structure, such as Mirror,
  Transform, Clip, or Merge.
- **Filter** changes a field or generated structure, such as Noise Warp,
  Smooth, Threshold, or Remap.
- **Recipe** is a reusable versioned subgraph with explicit inputs and exposed
  parameters.
- **Output** assigns meaning to an evaluated field: Object Color, Height, or a
  named scalar Mask.
- **Bake** is an immutable, rebuildable evaluation result with provenance.
- **Freeze/Make Editable** explicitly promotes generated vector output into new
  canonical Sources and disconnects it from the generating operation.

Mirror is an Operator even when its artist-facing `keep_source` option makes
the combined result look generative. Sweep is a Generator.

## Evaluation boundary

The target flow is:

```text
Canonical document
  Sources + Controls + Operations + Outputs
                    |
                    v
Pure deterministic evaluator
                    |
          +---------+---------+
          |                   |
   transient preview     accepted bake
                              |
                        runtime export
```

The evaluator must not mutate the document. Canvas, Inspector, bake services,
and export must consume the same evaluated result.

Scalar bakes rasterize that evaluated vector geometry deterministically at the
selected preview/export resolution. Height and named Masks remain separate
images. Normal maps are derived from the Height image plus its physical
amplitude and surface-domain texel spacing; they are never authored outputs.

## Dependency rules

References use stable IDs, never display names. Graph validation rejects
missing inputs, incompatible port types, and cycles before evaluation. Renaming
does not rewrite references because identity does not change.

Operations are evaluated in document order. An operation may only reference a
preceding element, which gives the current stack UI deterministic dependency
order without a separate node editor. Mirror accepts evaluated geometry from a
Source or Generator; its result can therefore continue a live chain such as
`side vein -> Sweep -> Mirror`.

Initial port types are `PointSet`, `PathSet`, `RegionSet`, `InstanceSet`,
`ScalarField`, and `ColorField`.

`Rectangle Region` is the first parametric `RegionSet` Source. Its canonical
data is position, physical size, rotation, and corner radius; polygon samples
are always derived by the evaluator.

`Crack Line` is a semantic `PathSet` Source. It stores an editable physical
center path with per-point widths and Bezier handles. Its dedicated type keeps
crack-specific generators and future reveal metadata distinct from generic
drawn paths while reusing the same path editing interaction.

`Repeat Grid` consumes preceding evaluated region geometry and produces a
deterministic grid of translated instances. Signed alternate-row offset is a
general parameter; a half-step offset is merely the brick-wall configuration.
Generated polygons are clipped to the physical Surface Domain before preview
or bake consumers receive them.

`Invert` is the first `ScalarField` Filter. It evaluates its preceding input at
bake resolution and returns the exact scalar complement. The source vector
graph remains canonical and unchanged.

## Format policy

The procedural document format starts at schema 1. Unsupported schema versions
are rejected explicitly. There is no compatibility layer for earlier prototype
documents; canonical data and evaluator behavior therefore have one contract.
