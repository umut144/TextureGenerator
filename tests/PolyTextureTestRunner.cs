using System;
using System.Collections.Generic;
using Godot;

public partial class PolyTextureTestRunner : SceneTree
{
    private readonly List<string> _failures = new();
    private int _testCount;

    public override void _Initialize()
    {
        Run("schema 1 is the only accepted document contract", TestSchemaContract);
        Run("document round trip preserves names and references", TestRoundTrip);
        Run("semantic outputs round trip with stable bindings", TestOutputRoundTrip);
        Run("preview resolution is independent from vector sources", TestResolutionIndependence);
        Run("rectangle regions remain parametric across round trips", TestRectangleRegionRoundTrip);
        Run("crack lines remain semantic editable paths", TestCrackLineRoundTrip);
        Run("sweep evaluation is deterministic", TestSweepDeterminism);
        Run("repeat grid produces deterministic staggered regions", TestRepeatGrid);
        Run("branch generator produces deterministic crack structures", TestBranchGenerator);
        Run("evaluator keeps operations live", TestLiveEvaluation);
        Run("mirror evaluation reflects across its axis", TestMirror);
        Run("mirror consumes live generator geometry", TestMirrorGeneratorChain);
        Run("height and mask baking is deterministic", TestDeterministicBake);
        Run("normal maps derive only from height", TestNormalDerivation);
        Run("invert filter derives a complementary mortar field", TestInvertMortarField);
        Run("edge falloff derives a physical crack gradient", TestEdgeFalloff);
        Run("display rename preserves stable references", TestRenamePreservesReferences);
        Run("dependency-aware deletion blocks used inputs", TestDependencyDeletion);
        Run("validator rejects missing generator inputs", TestMissingReferenceValidation);
        Run("validator rejects missing output inputs", TestMissingOutputReferenceValidation);

        if (_failures.Count == 0)
        {
            GD.Print($"PolyTexture tests passed: {_testCount}");
            Quit(0);
            return;
        }

        GD.PushError($"PolyTexture tests failed: {_failures.Count}/{_testCount}");
        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        Quit(1);
    }

    private void Run(string name, Action test)
    {
        _testCount++;
        try
        {
            test();
            GD.Print($"PASS: {name}");
        }
        catch (Exception exception)
        {
            _failures.Add($"FAIL: {name}: {exception.Message}");
        }
    }

    private static void TestSchemaContract()
    {
        PolyTextureDocument document = CreateGeneratorDocument();
        string json = PolyTextureStore.ToJson(document);
        Assert(json.Contains("\"schema_version\": 1", StringComparison.Ordinal), "schema 1 missing");
        Assert(json.Contains("\"domain\"", StringComparison.Ordinal), "physical domain missing");
        Assert(json.Contains("\"preview\"", StringComparison.Ordinal), "preview settings missing");
        Assert(!json.Contains("\"canvas\"", StringComparison.Ordinal), "obsolete canvas contract persisted");
        Assert(!json.Contains("baked_from_generator_id", StringComparison.Ordinal), "obsolete bake provenance persisted");

        string unsupported = json.Replace("\"schema_version\": 1", "\"schema_version\": 4", StringComparison.Ordinal);
        Assert(!PolyTextureValidator.ValidateJson(unsupported, out string error), "unsupported schema should be rejected");
        Assert(error.Contains("must be 1", StringComparison.Ordinal), "schema diagnostic should name the accepted version");
    }

    private static void TestRoundTrip()
    {
        PolyTextureDocument document = CreateGeneratorDocument();
        string path = "user://polytexture_test_roundtrip.polytexture.json";
        Assert(PolyTextureStore.Save(path, document, out string saveError), saveError);
        Assert(PolyTextureStore.Load(path, out PolyTextureDocument loaded, out string loadError), loadError);
        Equal("Source Stroke", loaded.Textures[0].GetElement("source").Name, "source name");
        SweepGeneratorElement sweep = loaded.Textures[0].GetElement("sweep") as SweepGeneratorElement;
        Assert(sweep != null, "sweep missing after round trip");
        Equal("source", sweep.SourceElementId, "sweep source reference");
        Equal("target", sweep.TargetElementId, "sweep target reference");
    }

    private static void TestResolutionIndependence()
    {
        PolyTextureDocument document = CreateGeneratorDocument();
        PolyTextureItem texture = document.Textures[0];
        CenterStrokeElement source = texture.GetCenterStroke("source");
        CenterStrokeElement before = (CenterStrokeElement)source.Clone();

        Assert(PolyTextureSurfaceService.SetPreviewResolution(texture, 2048, 1024), "preview resolution should change");
        Equal(2048, texture.PreviewWidthPx, "preview width update");
        Equal(1024, texture.PreviewHeightPx, "preview height update");
        Near(before.Transform.Position, source.Transform.Position, 0.0001f, "preview transform invariance");
        Near(before.Points[1].Position, source.Points[1].Position, 0.0001f, "preview point invariance");
        Near(before.Points[1].LeftWidth, source.Points[1].LeftWidth, 0.0001f, "preview width invariance");

        Assert(PolyTextureSurfaceService.SetDomainSize(texture, 250.0f, 180.0f), "domain size should change");
        Near(before.Points[1].Position, source.Points[1].Position, 0.0001f, "domain resize point invariance");
    }

    private static void TestOutputRoundTrip()
    {
        PolyTextureDocument document = CreateGeneratorDocument();
        document.ActiveOutputId = "height";
        document.SelectionKind = PolyTextureSelectionKind.Output;
        string path = "user://polytexture_test_outputs.polytexture.json";
        Assert(PolyTextureStore.Save(path, document, out string saveError), saveError);
        Assert(PolyTextureStore.Load(path, out PolyTextureDocument loaded, out string loadError), loadError);
        Equal(2, loaded.Textures[0].Outputs.Count, "output count");
        PolyTextureOutputBinding height = loaded.Textures[0].GetOutput("height");
        Equal(PolyTextureOutputKind.Height, height.Kind, "height kind");
        Equal("sweep", height.SourceElementIds[0], "height source");
        Equal(2, height.SourceElementIds.Count, "height source count");
        Near(0.8f, height.HeightAmplitudeCm, 0.0001f, "height amplitude");
        Equal("Leaf Veins", loaded.Textures[0].GetOutput("veins").Name, "mask name");
        Equal(PolyTextureSelectionKind.Output, loaded.SelectionKind, "output selection kind");
        Equal("height", loaded.ActiveOutputId, "active output id");
    }

    private static void TestSweepDeterminism()
    {
        PolyTextureDocument document = CreateGeneratorDocument();
        PolyTextureItem texture = document.Textures[0];
        SweepGeneratorElement sweep = texture.GetElement("sweep") as SweepGeneratorElement;
        List<CenterStrokeElement> first = PolyTextureRenderer.BuildSweepInstances(texture, sweep);
        List<CenterStrokeElement> second = PolyTextureRenderer.BuildSweepInstances(texture, sweep);
        Equal(4, first.Count, "sweep count");
        Equal(first.Count, second.Count, "deterministic count");
        for (int index = 0; index < first.Count; index++)
        {
            Near(first[index].Transform.Position, second[index].Transform.Position, 0.0001f, $"instance {index} position");
            Near(first[index].Transform.RotationDegrees, second[index].Transform.RotationDegrees, 0.0001f, $"instance {index} rotation");
        }
    }

    private static void TestRectangleRegionRoundTrip()
    {
        PolyTextureDocument document = new()
        {
            ActiveTextureId = "wall",
            ActiveElementId = "brick",
            SelectionKind = PolyTextureSelectionKind.Element
        };
        PolyTextureItem texture = new() { Id = "wall", Name = "Wall" };
        RectangleRegionElement region = new()
        {
            Id = "brick",
            Name = "Brick Region",
            Position = new Vector2(50.0f, 30.0f),
            WidthCm = 80.0f,
            HeightCm = 40.0f,
            CornerRadiusCm = 5.0f,
            RotationDegrees = 12.0f
        };
        texture.Elements.Add(region);
        document.Textures.Add(texture);
        Assert(PolyTextureValidator.Validate(document, out string validationError), validationError);

        List<Vector2> polygon = PolyTextureRenderer.BuildRectangleRegion(region);
        Equal(28, polygon.Count, "rounded rectangle sample count");
        string path = "user://polytexture_test_rectangle.polytexture.json";
        Assert(PolyTextureStore.Save(path, document, out string saveError), saveError);
        Assert(PolyTextureStore.Load(path, out PolyTextureDocument loaded, out string loadError), loadError);
        RectangleRegionElement loadedRegion = loaded.Textures[0].GetElement("brick") as RectangleRegionElement;
        Assert(loadedRegion != null, "rectangle region missing after round trip");
        Near(region.Position, loadedRegion.Position, 0.0001f, "rectangle position");
        Near(region.WidthCm, loadedRegion.WidthCm, 0.0001f, "rectangle width");
        Near(region.CornerRadiusCm, loadedRegion.CornerRadiusCm, 0.0001f, "rectangle radius");
    }

    private static void TestLiveEvaluation()
    {
        PolyTextureDocument document = CreateGeneratorDocument();
        PolyTextureEvaluationResult evaluation = PolyTextureEvaluator.Evaluate(document.Textures[0]);
        Equal(1, evaluation.LiveSweeps.Count, "live sweep count");
        Assert(evaluation.HiddenSourceIds.Contains("source"), "hidden source binding");

        SweepGeneratorElement sweep = document.Textures[0].GetElement("sweep") as SweepGeneratorElement;
        sweep.Count = 9;
        List<CenterStrokeElement> instances = PolyTextureRenderer.BuildSweepInstances(document.Textures[0], evaluation.LiveSweeps[0]);
        Equal(9, instances.Count, "live parameter update");
        Assert(document.Textures[0].Elements.Count == 3, "evaluation must not append editable copies");
    }

    private static void TestCrackLineRoundTrip()
    {
        PolyTextureDocument document = new()
        {
            ActiveTextureId = "cracks",
            ActiveElementId = "main_crack",
            SelectionKind = PolyTextureSelectionKind.Element
        };
        PolyTextureItem texture = new() { Id = "cracks", Name = "Cracks" };
        CrackLineElement crack = new()
        {
            Id = "main_crack",
            Name = "Main Crack",
            Falloff = 1.5f
        };
        crack.Points.Add(new CenterStrokePoint
        {
            X = 20.0f,
            Y = 30.0f,
            LeftWidth = 5.0f,
            RightWidth = 5.0f,
            HandleMode = PolyTextureHandleMode.Aligned
        });
        crack.Points.Add(new CenterStrokePoint
        {
            X = 80.0f,
            Y = 110.0f,
            LeftWidth = 2.0f,
            RightWidth = 2.0f,
            HandleMode = PolyTextureHandleMode.Aligned
        });
        texture.Elements.Add(crack);
        document.Textures.Add(texture);

        Assert(PolyTextureValidator.Validate(document, out string validationError), validationError);
        string path = "user://polytexture_test_crack_line.polytexture.json";
        Assert(PolyTextureStore.Save(path, document, out string saveError), saveError);
        Assert(PolyTextureStore.Load(path, out PolyTextureDocument loaded, out string loadError), loadError);
        CrackLineElement loadedCrack = loaded.Textures[0].GetElement("main_crack") as CrackLineElement;
        Assert(loadedCrack != null, "semantic crack line missing after round trip");
        Equal(CrackLineElement.ElementType, loadedCrack.Type, "crack line type");
        Assert(loadedCrack.SupportsBezierHandles, "crack line must keep Bezier editing");
        Near(1.5f, loadedCrack.Falloff, 0.0001f, "crack falloff");
        Equal(2, loadedCrack.Points.Count, "crack point count");
    }

    private static void TestRepeatGrid()
    {
        PolyTextureDocument document = CreateWallDocument();
        PolyTextureItem texture = document.Textures[0];
        RepeatGridGeneratorElement repeat = texture.GetElement("repeat") as RepeatGridGeneratorElement;
        PolyTextureEvaluationResult first = PolyTextureEvaluator.Evaluate(texture);
        PolyTextureEvaluationResult second = PolyTextureEvaluator.Evaluate(texture);
        Equal(6, first.GetGeometry(repeat.Id).Count, "repeat instance count");
        Equal(first.GetGeometry(repeat.Id).Count, second.GetGeometry(repeat.Id).Count, "repeat deterministic count");
        List<List<Vector2>> rawGrid = PolyTextureRenderer.BuildRepeatGridPolygons(first.GetGeometry("brick"), repeat);
        Vector2 firstPoint = rawGrid[0][0];
        Vector2 staggeredPoint = rawGrid[3][0];
        Near(firstPoint + new Vector2(repeat.AlternateRowOffsetXCm, repeat.StepYCm), staggeredPoint, 0.0001f, "staggered row offset");
        foreach (List<Vector2> polygon in first.GetGeometry(repeat.Id))
        {
            foreach (Vector2 point in polygon)
            {
                Assert(point.X >= 0.0f && point.X <= texture.DomainWidthCm
                    && point.Y >= 0.0f && point.Y <= texture.DomainHeightCm, "repeat geometry must be clipped to the surface domain");
            }
        }
        Assert(first.HiddenSourceIds.Contains("brick"), "repeat should replace source preview");
        Assert(PolyTextureValidator.Validate(document, out string error), error);

        string path = "user://polytexture_test_repeat.polytexture.json";
        Assert(PolyTextureStore.Save(path, document, out string saveError), saveError);
        Assert(PolyTextureStore.Load(path, out PolyTextureDocument loaded, out string loadError), loadError);
        RepeatGridGeneratorElement loadedRepeat = loaded.Textures[0].GetElement("repeat") as RepeatGridGeneratorElement;
        Equal(3, loadedRepeat.Columns, "repeat columns round trip");
        Near(-50.0f, loadedRepeat.AlternateRowOffsetXCm, 0.0001f, "repeat offset round trip");
    }

    private static void TestBranchGenerator()
    {
        PolyTextureDocument document = CreateCrackDocument();
        PolyTextureItem texture = document.Textures[0];
        BranchGeneratorElement branch = texture.GetElement("branches") as BranchGeneratorElement;
        Assert(branch != null, "branch generator missing");
        Assert(PolyTextureValidator.Validate(document, out string validationError), validationError);

        List<CenterStrokeElement> first = PolyTextureRenderer.BuildBranchStrokes(texture, branch);
        List<CenterStrokeElement> second = PolyTextureRenderer.BuildBranchStrokes(texture, branch);
        Equal(branch.Count, first.Count, "branch count");
        Equal(first.Count, second.Count, "deterministic branch count");
        for (int index = 0; index < first.Count; index++)
        {
            Equal(branch.Segments + 1, first[index].Points.Count, $"branch {index} segment points");
            for (int pointIndex = 0; pointIndex < first[index].Points.Count; pointIndex++)
            {
                Near(first[index].Points[pointIndex].Position, second[index].Points[pointIndex].Position, 0.0001f, $"branch {index} point {pointIndex}");
            }
            Assert(first[index].Points[^1].LeftWidth < first[index].Points[0].LeftWidth, "branch width must taper");
        }

        PolyTextureEvaluationResult evaluation = PolyTextureEvaluator.Evaluate(texture);
        Assert(evaluation.HiddenSourceIds.Contains("main_crack"), "branch should replace source preview");
        Equal(branch.Count + 1, evaluation.GetGeometry(branch.Id).Count, "combined source and branch polygons");

        string path = "user://polytexture_test_branch.polytexture.json";
        Assert(PolyTextureStore.Save(path, document, out string saveError), saveError);
        Assert(PolyTextureStore.Load(path, out PolyTextureDocument loaded, out string loadError), loadError);
        BranchGeneratorElement loadedBranch = loaded.Textures[0].GetElement("branches") as BranchGeneratorElement;
        Assert(loadedBranch != null, "branch missing after round trip");
        Equal(branch.Seed, loadedBranch.Seed, "branch seed round trip");
        Near(branch.Irregularity, loadedBranch.Irregularity, 0.0001f, "branch irregularity round trip");

        Image mask = PolyTextureBakeService.BakeScalar(texture, texture.GetOutput("crack_mask"), 96, 96);
        bool hasCrack = false;
        for (int y = 0; y < mask.GetHeight() && !hasCrack; y++)
        {
            for (int x = 0; x < mask.GetWidth(); x++)
            {
                hasCrack |= mask.GetPixel(x, y).R > 0.5f;
            }
        }
        Assert(hasCrack, "branch mask bake should contain crack structure");
    }

    private static void TestMirror()
    {
        PolyTextureDocument document = CreateGeneratorDocument();
        PolyTextureItem texture = document.Textures[0];
        PolyTextureGuide axis = new()
        {
            Id = "axis",
            Name = "Axis",
            Type = PolyTextureGuideType.Axis,
            Position = Vector2.Zero,
            AxisEnd = Vector2.Up
        };
        texture.Guides.Add(axis);
        MirrorGeneratorElement mirror = new()
        {
            Id = "mirror",
            Name = "Mirror",
            SourceElementId = "source",
            AxisGuideId = "axis"
        };
        texture.Elements.Add(mirror);

        CenterStrokeElement mirrored = PolyTextureRenderer.BuildMirrorElement(texture, mirror);
        Assert(mirrored != null, "mirror evaluation returned null");
        Vector2 original = texture.GetCenterStroke("source").Transform.TransformPoint(texture.GetCenterStroke("source").Points[1].Position);
        Vector2 reflected = mirrored.Transform.TransformPoint(mirrored.Points[1].Position);
        Near(-original.X, reflected.X, 0.001f, "mirrored x");
        Near(original.Y, reflected.Y, 0.001f, "mirrored y");
    }

    private static void TestRenamePreservesReferences()
    {
        PolyTextureDocument document = CreateGeneratorDocument();
        PolyTextureItem texture = document.Textures[0];
        CenterStrokeElement source = texture.GetCenterStroke("source");
        source.Name = "Renamed Source";
        SweepGeneratorElement sweep = texture.GetElement("sweep") as SweepGeneratorElement;
        Equal("source", source.Id, "stable source id");
        Equal("source", sweep.SourceElementId, "stable sweep reference");
        Assert(PolyTextureValidator.Validate(document, out string error), error);
    }

    private static void TestInvertMortarField()
    {
        PolyTextureDocument document = CreateWallDocument();
        PolyTextureItem texture = document.Textures[0];
        InvertFilterElement invert = new()
        {
            Id = "invert",
            Name = "Mortar Field",
            SourceElementId = "repeat"
        };
        texture.Elements.Add(invert);
        PolyTextureOutputBinding bricks = new()
        {
            Id = "bricks",
            Name = "Bricks",
            Kind = PolyTextureOutputKind.Mask
        };
        bricks.SourceElementIds.Add("repeat");
        texture.Outputs.Add(bricks);
        PolyTextureOutputBinding mortar = new()
        {
            Id = "mortar",
            Name = "Mortar",
            Kind = PolyTextureOutputKind.Mask
        };
        mortar.SourceElementIds.Add(invert.Id);
        texture.Outputs.Add(mortar);

        Assert(PolyTextureValidator.Validate(document, out string validationError), validationError);
        Image brickImage = PolyTextureBakeService.BakeScalar(texture, bricks, 96, 64);
        Image mortarImage = PolyTextureBakeService.BakeScalar(texture, mortar, 96, 64);
        bool foundBrick = false;
        bool foundMortar = false;
        for (int y = 0; y < brickImage.GetHeight(); y++)
        {
            for (int x = 0; x < brickImage.GetWidth(); x++)
            {
                float brickValue = brickImage.GetPixel(x, y).R;
                float mortarValue = mortarImage.GetPixel(x, y).R;
                Near(1.0f, brickValue + mortarValue, 0.01f, $"complementary field {x},{y}");
                foundBrick |= brickValue > 0.5f;
                foundMortar |= mortarValue > 0.5f;
            }
        }
        Assert(foundBrick && foundMortar, "wall fields should contain bricks and mortar");

        string path = "user://polytexture_test_wall.polytexture.json";
        Assert(PolyTextureStore.Save(path, document, out string saveError), saveError);
        Assert(PolyTextureStore.Load(path, out PolyTextureDocument loaded, out string loadError), loadError);
        InvertFilterElement loadedInvert = loaded.Textures[0].GetElement("invert") as InvertFilterElement;
        Assert(loadedInvert != null, "invert filter missing after round trip");
        Equal("repeat", loadedInvert.SourceElementId, "invert source round trip");
    }

    private static void TestEdgeFalloff()
    {
        PolyTextureDocument document = CreateCrackDocument();
        PolyTextureItem texture = document.Textures[0];
        EdgeFalloffFilterElement falloff = new()
        {
            Id = "soft_cracks",
            Name = "Soft Cracks",
            SourceElementId = "branches",
            RadiusCm = 12.0f,
            Exponent = 1.4f
        };
        texture.Elements.Add(falloff);
        PolyTextureOutputBinding output = new()
        {
            Id = "soft_mask",
            Name = "Soft Crack Mask",
            Kind = PolyTextureOutputKind.Mask
        };
        output.SourceElementIds.Add(falloff.Id);
        texture.Outputs.Add(output);
        Assert(PolyTextureValidator.Validate(document, out string validationError), validationError);

        Image image = PolyTextureBakeService.BakeScalar(texture, output, 128, 128);
        bool hasBackground = false;
        bool hasGradient = false;
        for (int y = 0; y < image.GetHeight(); y++)
        {
            for (int x = 0; x < image.GetWidth(); x++)
            {
                float value = image.GetPixel(x, y).R;
                hasBackground |= value <= 0.0001f;
                hasGradient |= value > 0.0001f && value < 0.999f;
            }
        }
        Assert(hasBackground && hasGradient, "edge falloff should contain background and a soft physical gradient");

        string path = "user://polytexture_test_edge_falloff.polytexture.json";
        Assert(PolyTextureStore.Save(path, document, out string saveError), saveError);
        Assert(PolyTextureStore.Load(path, out PolyTextureDocument loaded, out string loadError), loadError);
        EdgeFalloffFilterElement loadedFalloff = loaded.Textures[0].GetElement("soft_cracks") as EdgeFalloffFilterElement;
        Assert(loadedFalloff != null, "edge falloff missing after round trip");
        Near(falloff.RadiusCm, loadedFalloff.RadiusCm, 0.0001f, "edge falloff radius round trip");
        Near(falloff.Exponent, loadedFalloff.Exponent, 0.0001f, "edge falloff exponent round trip");
    }

    private static void TestDeterministicBake()
    {
        PolyTextureDocument document = CreateGeneratorDocument();
        PolyTextureItem texture = document.Textures[0];
        PolyTextureOutputBinding output = texture.GetOutput("height");
        Image first = PolyTextureBakeService.BakeScalar(texture, output, 64, 48);
        Image second = PolyTextureBakeService.BakeScalar(texture, output, 64, 48);
        Equal(64, first.GetWidth(), "bake width");
        Equal(48, first.GetHeight(), "bake height");
        int litPixels = 0;
        for (int y = 0; y < first.GetHeight(); y++)
        {
            for (int x = 0; x < first.GetWidth(); x++)
            {
                Near(first.GetPixel(x, y).R, second.GetPixel(x, y).R, 0.0001f, $"deterministic pixel {x},{y}");
                litPixels += first.GetPixel(x, y).R > 0.0f ? 1 : 0;
            }
        }
        Assert(litPixels > 0 && litPixels < first.GetWidth() * first.GetHeight(), "bake should contain foreground and background");
        Assert(PolyTextureBakeService.SavePng("user://polytexture_test_height.png", first) == Error.Ok, "PNG export failed");
        Assert(FileAccess.FileExists("user://polytexture_test_height.png"), "PNG export file missing");
    }

    private static void TestNormalDerivation()
    {
        PolyTextureDocument document = CreateGeneratorDocument();
        PolyTextureItem texture = document.Textures[0];
        PolyTextureOutputBinding output = texture.GetOutput("height");
        Image height = PolyTextureBakeService.BakeScalar(texture, output, 64, 64);
        Image normal = PolyTextureBakeService.DeriveNormalMap(height, texture, output.HeightAmplitudeCm);
        Equal(Image.Format.Rgb8, normal.GetFormat(), "normal format");
        bool hasSlope = false;
        for (int y = 0; y < normal.GetHeight() && !hasSlope; y++)
        {
            for (int x = 0; x < normal.GetWidth(); x++)
            {
                Color pixel = normal.GetPixel(x, y);
                if (Mathf.Abs(pixel.R - 0.5f) > 0.01f || Mathf.Abs(pixel.G - 0.5f) > 0.01f)
                {
                    hasSlope = true;
                    break;
                }
            }
        }
        Assert(hasSlope, "derived normal should contain slopes at height boundaries");
    }

    private static void TestMirrorGeneratorChain()
    {
        PolyTextureDocument document = CreateGeneratorDocument();
        PolyTextureItem texture = document.Textures[0];
        texture.Guides.Add(new PolyTextureGuide
        {
            Id = "axis",
            Name = "Leaf Axis",
            Type = PolyTextureGuideType.Axis,
            Position = Vector2.Zero,
            AxisEnd = Vector2.Up
        });
        MirrorGeneratorElement mirror = new()
        {
            Id = "mirror",
            Name = "Mirror Veins",
            SourceElementId = "sweep",
            AxisGuideId = "axis",
            RenderSource = true
        };
        texture.Elements.Add(mirror);

        PolyTextureEvaluationResult evaluation = PolyTextureEvaluator.Evaluate(texture);
        Equal(8, evaluation.GetGeometry("mirror").Count, "combined mirrored sweep polygon count");
        Assert(evaluation.HiddenSourceIds.Contains("sweep"), "mirror should replace its upstream preview");
        Assert(PolyTextureValidator.Validate(document, out string error), error);
    }

    private static void TestDependencyDeletion()
    {
        PolyTextureDocument document = CreateGeneratorDocument();
        Assert(!PolyTextureDependencyService.CanDeleteElement(document.Textures[0], "source", out string error), "used source deletion should be blocked");
        Assert(error.Contains("sweep", StringComparison.Ordinal), "diagnostic should name dependent sweep");
        Assert(!PolyTextureDependencyService.CanDeleteElement(document.Textures[0], "sweep", out string outputError), "output-bound generator deletion should be blocked");
        Assert(outputError.Contains("height", StringComparison.Ordinal), "diagnostic should name dependent output");
    }

    private static void TestMissingReferenceValidation()
    {
        PolyTextureDocument document = CreateGeneratorDocument();
        SweepGeneratorElement sweep = document.Textures[0].GetElement("sweep") as SweepGeneratorElement;
        sweep.SourceElementId = "missing";
        Assert(!PolyTextureValidator.Validate(document, out string error), "missing source should fail validation");
        Assert(error.Contains("preceding", StringComparison.Ordinal), "missing reference diagnostic should be concrete");
    }

    private static void TestMissingOutputReferenceValidation()
    {
        PolyTextureDocument document = CreateGeneratorDocument();
        document.Textures[0].GetOutput("height").SourceElementIds[0] = "missing";
        Assert(!PolyTextureValidator.Validate(document, out string error), "missing output source should fail validation");
        Assert(error.Contains("existing element", StringComparison.Ordinal), "missing output diagnostic should be concrete");
    }

    private static PolyTextureDocument CreateGeneratorDocument()
    {
        PolyTextureDocument document = new()
        {
            Name = "Test",
            ActiveTextureId = "texture",
            ActiveElementId = "source",
            SelectionKind = PolyTextureSelectionKind.Element
        };
        PolyTextureItem texture = new()
        {
            Id = "texture",
            Name = "Texture"
        };
        CenterStrokeElement source = new()
        {
            Id = "source",
            Name = "Source Stroke"
        };
        source.Points.Add(new CenterStrokePoint { X = 12, Y = 10, LeftWidth = 2, RightWidth = 2 });
        source.Points.Add(new CenterStrokePoint { X = 28, Y = 20, LeftWidth = 1, RightWidth = 1 });
        CenterPathElement target = new()
        {
            Id = "target",
            Name = "Target Path"
        };
        target.Points.Add(new CenterStrokePoint
        {
            X = 0,
            Y = 0,
            LeftWidth = 1,
            RightWidth = 1,
            HandleMode = PolyTextureHandleMode.Aligned,
            InHandle = new Vector2(0, -16),
            OutHandle = new Vector2(0, 16)
        });
        target.Points.Add(new CenterStrokePoint
        {
            X = 0,
            Y = 100,
            LeftWidth = 1,
            RightWidth = 1,
            HandleMode = PolyTextureHandleMode.Aligned,
            InHandle = new Vector2(0, -16),
            OutHandle = new Vector2(0, 16)
        });
        SweepGeneratorElement sweep = new()
        {
            Id = "sweep",
            Name = "Sweep",
            SourceElementId = source.Id,
            TargetElementId = target.Id,
            Count = 4,
            StartT = 0,
            EndT = 1,
            SideMode = PolyTextureSweepSideMode.Right
        };
        texture.Elements.Add(source);
        texture.Elements.Add(target);
        texture.Elements.Add(sweep);
        texture.Outputs.Add(new PolyTextureOutputBinding
        {
            Id = "height",
            Name = "Height",
            Kind = PolyTextureOutputKind.Height,
            HeightAmplitudeCm = 0.8f
        });
        texture.Outputs[^1].SourceElementIds.Add(sweep.Id);
        texture.Outputs[^1].SourceElementIds.Add(target.Id);
        texture.Outputs.Add(new PolyTextureOutputBinding
        {
            Id = "veins",
            Name = "Leaf Veins",
            Kind = PolyTextureOutputKind.Mask,
        });
        texture.Outputs[^1].SourceElementIds.Add(sweep.Id);
        document.Textures.Add(texture);
        return document;
    }

    private static PolyTextureDocument CreateWallDocument()
    {
        PolyTextureDocument document = new()
        {
            Name = "Wall",
            ActiveTextureId = "wall",
            ActiveElementId = "repeat",
            SelectionKind = PolyTextureSelectionKind.Element
        };
        PolyTextureItem texture = new() { Id = "wall", Name = "Wall" };
        RectangleRegionElement brick = new()
        {
            Id = "brick",
            Name = "Brick",
            Position = new Vector2(40.0f, 20.0f),
            WidthCm = 80.0f,
            HeightCm = 40.0f,
            CornerRadiusCm = 2.0f
        };
        RepeatGridGeneratorElement repeat = new()
        {
            Id = "repeat",
            Name = "Brick Repeat",
            SourceElementId = brick.Id,
            Columns = 3,
            Rows = 2,
            StepXCm = 100.0f,
            StepYCm = 50.0f,
            AlternateRowOffsetXCm = -50.0f
        };
        texture.Elements.Add(brick);
        texture.Elements.Add(repeat);
        document.Textures.Add(texture);
        return document;
    }

    private static PolyTextureDocument CreateCrackDocument()
    {
        PolyTextureDocument document = new()
        {
            Name = "Cracks",
            ActiveTextureId = "cracks",
            ActiveElementId = "branches",
            SelectionKind = PolyTextureSelectionKind.Element
        };
        PolyTextureItem texture = new() { Id = "cracks", Name = "Cracks" };
        CrackLineElement crack = new()
        {
            Id = "main_crack",
            Name = "Main Crack",
            Transform = new PolyTextureElementTransform { Position = new Vector2(200.0f, 80.0f) }
        };
        crack.Points.Add(new CenterStrokePoint { X = 0.0f, Y = 0.0f, LeftWidth = 5.0f, RightWidth = 5.0f, HandleMode = PolyTextureHandleMode.Aligned });
        crack.Points.Add(new CenterStrokePoint { X = 12.0f, Y = 120.0f, LeftWidth = 4.0f, RightWidth = 4.0f, HandleMode = PolyTextureHandleMode.Aligned });
        crack.Points.Add(new CenterStrokePoint { X = -8.0f, Y = 240.0f, LeftWidth = 2.0f, RightWidth = 2.0f, HandleMode = PolyTextureHandleMode.Aligned });
        BranchGeneratorElement branches = new()
        {
            Id = "branches",
            Name = "Crack Branches",
            SourceElementId = crack.Id,
            Seed = 42,
            Count = 6,
            Segments = 3,
            LengthMinCm = 20.0f,
            LengthMaxCm = 45.0f,
            Irregularity = 0.35f,
            RenderSource = true
        };
        texture.Elements.Add(crack);
        texture.Elements.Add(branches);
        PolyTextureOutputBinding mask = new()
        {
            Id = "crack_mask",
            Name = "Cracks",
            Kind = PolyTextureOutputKind.Mask
        };
        mask.SourceElementIds.Add(branches.Id);
        texture.Outputs.Add(mask);
        document.Textures.Add(texture);
        return document;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Equal<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
        }
    }

    private static void Near(float expected, float actual, float tolerance, string label)
    {
        if (Mathf.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
        }
    }

    private static void Near(Vector2 expected, Vector2 actual, float tolerance, string label)
    {
        if (expected.DistanceTo(actual) > tolerance)
        {
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
        }
    }
}
