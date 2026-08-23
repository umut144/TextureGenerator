# PolyTexture Document Model

## Target canonical model

```text
PolyTextureDocument
|- SurfaceDomain
|- Sources
|- Controls
|- OperationNodes
|- RecipeInstances
`- OutputBindings

PolyTextureEditorState
|- Selection
|- Outliner expansion
|- Preview mode
`- View transform

PolyTextureDerivedData
|- TransientPreview
`- AcceptedBakes
```

`PolyTextureEditorState` and `PolyTextureDerivedData` are not semantic source
content. They may be stored beside a document for convenience but must not
participate in source fingerprints.

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

## Animation metadata

Generated vector segments and instances may carry normalized `reveal_start`,
`reveal_end`, or `phase` attributes. A raster cache may equivalently contain an
arrival-time field. The static structure remains deterministic; runtime shaders
only reveal or modulate it.
