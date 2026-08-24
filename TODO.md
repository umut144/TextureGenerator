# Temporary Session Handoff

Delete this file once the next PolyTexture session has read and incorporated
the remaining product context. The durable technical contracts live in
`AGENTS.md`, `docs/ARCHITECTURE.md`, and `docs/DOCUMENT_MODEL.md`.

## Current direction

PolyTexture is a vector-first, procedural texture authoring tool for finished
meshes. Artists should author resolution-independent semantic information with
Sources, Generators, Operators, Filters, and eventually reusable Recipes.
Raster textures and shaders are evaluation/export targets rather than the
canonical source of truth.

The UI intentionally offers tools at different abstraction levels. The current
low-level technical-art controls establish the primitives; later Recipes and
domain tools should package them with curated parameters instead of creating a
second authoring model.

## Accepted vertical slices

- Brick wall: Rectangle Region, Repeat Grid, staggered rows, Invert for mortar,
  and separate Mask/Height outputs.
- Cracks: semantic Crack Line paths, default width for subsequent points,
  multi-point width editing, deterministic recursive Branch generation, and
  Edge Falloff with a readable scalar debug preview.
- Mold colonies: Ellipse Region prototypes, optional region bounds, clustered
  Scatter, deterministic seed, count, cluster strength/radius, scale variation,
  and rotation variation. Manual feedback confirmed that bounds selection and
  cluster behavior are understandable and initially feel correct; longer-term
  tuning should follow real use.

## Confirmed UX and product decisions

- Main tool groups are Draw, Sources, Generator, Operator, and Filter.
- Draw is an interaction mode, not itself a Source.
- Mirror is an Operator; Sweep, Repeat Grid, Branch, and Scatter are Generators.
- Material-bound procedural textures are intended to become a reusable library.
- Object-bound graphics/decals, material-bound texture fields, and independent
  semantic Masks are all required authoring categories.
- Mask channels are independent information sources; RGBA packing is only an
  export optimization.
- Height alone is authored; Normal is derived from Height.
- Stable IDs are preferred, legacy prototype authoring data need not be kept,
  and automated tests are required for robustness.
- Scalar filters use an opaque contrast-enhanced debug heatmap so radius,
  exponent, and low-magnitude changes remain visible.
- Numeric spin-box arrows use practical increments rather than 0.01 steps.
- Saving uses the document name; JSON loading starts in `savefiles/`; PNG
  exports use `output/`.
- Creating an output with an operation selected binds only that operation, so
  helper geometry such as Scatter bounds is excluded.

## Suggested next slice

The next recommended slice is the pair of books from the concept drawings. It
should test both object-bound symbols/decals and reusable material-bound texture
recipes. Before implementation, define the smallest Source/Operator set needed
for placing and composing vector symbols on a bounded object surface, then
decide how a material texture is saved and instantiated as a versioned Recipe.

Pause and ask the user if implementation exposes a product decision that is
not settled by the documents. The user wants a manual acceptance pass after
each practical slice and a Git commit after every completed change.

## Checkpoint

- Working implementation is committed through `e39574a`.
- Build and Godot editor startup pass without warnings or errors.
- All 28 automated tests pass.
- No known open defect remains from the accepted slices.
