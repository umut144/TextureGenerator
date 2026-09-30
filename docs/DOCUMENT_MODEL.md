# TextureGenerator Document Model

## Target canonical model

```text
TextureGeneratorDocument
|- SurfaceDomain
|- Sources
|- Controls
|- OperationNodes
|- RecipeInstances
`- OutputBindings

TextureGeneratorEditorState
|- Selection
|- Outliner expansion
|- Preview mode
`- View transform

TextureGeneratorDerivedData
|- TransientPreview
`- AcceptedBakes
```

`TextureGeneratorEditorState` and `TextureGeneratorDerivedData` are not semantic source
content. They may be stored beside a document for convenience but must not
participate in source fingerprints.

Point selection may contain multiple indices for batch property editing while
retaining one primary point for position and Bezier-handle inspection. Normal
click selects one point; Shift-click toggles membership. Batch width edits
apply to every selected point, while position and handle edits remain scoped to
the primary point.

## Identity

Every addressable canonical record has:

```text
id    immutable stable technical identity
name  mutable user-facing label
```

Duplication allocates new IDs and atomically remaps internal references.
Deletion is dependency-aware and must either be blocked with a diagnostic or
performed through an explicit operation that also removes affected bindings.

## Coordinates and resolution

Authored geometry uses a physical, resolution-independent surface domain.
Preview and export resolution are evaluation settings. Changing resolution
must never scale canonical points, widths, handles, or guides.

The canonical unit is centimeters. A new texture defaults to a `400 x 400 cm`
domain and a separate `512 x 512 px` preview. These defaults do not imply a
pixel density: either value can change without modifying the other.

## Outputs

The first supported semantic outputs are:

- `color`: object-bound color field;
- `height`: scalar height field from which normals can be derived;
- `mask:<name>`: independent named scalar information.

RGBA packing is an optional export optimization and is not part of the
canonical output model.

Each output binding references one or more sources or operations by stable ID. Height
bindings additionally declare their physical amplitude in centimeters. Mask
bindings use their mutable display name as the artist-facing semantic label;
their stable ID remains the technical identity.

Creating an output while an element is selected binds that element alone. This
keeps helper inputs such as Scatter bounds out of the semantic result. Creating
an output from the texture selection instead binds the complete visible graph.

The current PNG bake writes one grayscale file per scalar output. A Height
binding may additionally export a tangent-space Normal image derived from the
same rasterized Height field. Bake files are derived data and are not embedded
in the canonical document.

## Animation metadata

Generated vector segments and instances may carry normalized `reveal_start`,
`reveal_end`, or `phase` attributes. A raster cache may equivalently contain an
arrival-time field. The static structure remains deterministic; runtime shaders
only reveal or modulate it.
