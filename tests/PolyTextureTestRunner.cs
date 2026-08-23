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
        Run("sweep evaluation is deterministic", TestSweepDeterminism);
        Run("evaluator keeps operations live", TestLiveEvaluation);
        Run("mirror evaluation reflects across its axis", TestMirror);
        Run("mirror consumes live generator geometry", TestMirrorGeneratorChain);
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
        string path = "user://polytexture_test_outputs.polytexture.json";
        Assert(PolyTextureStore.Save(path, document, out string saveError), saveError);
        Assert(PolyTextureStore.Load(path, out PolyTextureDocument loaded, out string loadError), loadError);
        Equal(2, loaded.Textures[0].Outputs.Count, "output count");
        PolyTextureOutputBinding height = loaded.Textures[0].GetOutput("height");
        Equal(PolyTextureOutputKind.Height, height.Kind, "height kind");
        Equal("sweep", height.SourceElementIds[0], "height source");
        Near(0.8f, height.HeightAmplitudeCm, 0.0001f, "height amplitude");
        Equal("Leaf Veins", loaded.Textures[0].GetOutput("veins").Name, "mask name");
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
