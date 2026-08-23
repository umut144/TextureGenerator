using Godot;
using System.Collections.Generic;

public static class PolyTextureValidator
{
    public static bool Validate(PolyTextureDocument document, out string error)
    {
        error = string.Empty;

        if (document == null)
        {
            error = "document is null";
            return false;
        }

        if (document.SchemaVersion != PolyTextureDocument.CurrentSchemaVersion)
        {
            error = $"schema_version must be {PolyTextureDocument.CurrentSchemaVersion}";
            return false;
        }

        if (!document.DocumentType.Equals("polytexture", System.StringComparison.Ordinal))
        {
            error = "document_type must be polytexture";
            return false;
        }

        if (document.DefaultPreviewWidthPx <= 0 || document.DefaultPreviewHeightPx <= 0)
        {
            error = "default preview size must be positive";
            return false;
        }

        if (!float.IsFinite(document.SnapStepCm) || document.SnapStepCm <= 0.0f)
        {
            error = "snap_step_cm must be finite and positive";
            return false;
        }

        HashSet<string> ids = new(System.StringComparer.Ordinal);
        for (int textureIndex = 0; textureIndex < document.Textures.Count; textureIndex++)
        {
            PolyTextureItem texture = document.Textures[textureIndex];
            string path = $"textures[{textureIndex}]";

            if (string.IsNullOrWhiteSpace(texture.Id))
            {
                error = $"{path}.id is required";
                return false;
            }

            if (!ids.Add(texture.Id))
            {
                error = $"{path}.id must be unique";
                return false;
            }

            if (string.IsNullOrWhiteSpace(texture.Name))
            {
                error = $"{path}.name is required";
                return false;
            }

            if (!float.IsFinite(texture.DomainWidthCm) || !float.IsFinite(texture.DomainHeightCm)
                || texture.DomainWidthCm <= 0.0f || texture.DomainHeightCm <= 0.0f)
            {
                error = $"{path}.domain size must be finite and positive";
                return false;
            }

            if (texture.PreviewWidthPx <= 0 || texture.PreviewHeightPx <= 0)
            {
                error = $"{path}.preview size must be positive";
                return false;
            }

            HashSet<string> elementIds = new(System.StringComparer.Ordinal);
            for (int elementIndex = 0; elementIndex < texture.Elements.Count; elementIndex++)
            {
                PolyTextureElement element = texture.Elements[elementIndex];
                string elementPath = $"{path}.elements[{elementIndex}]";
                if (string.IsNullOrWhiteSpace(element.Id))
                {
                    error = $"{elementPath}.id is required";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(element.Name))
                {
                    error = $"{elementPath}.name is required";
                    return false;
                }

                if (!elementIds.Add(element.Id))
                {
                    error = $"{elementPath}.id must be unique within texture";
                    return false;
                }

                if (!ValidateElement(element, elementPath, out error))
                {
                    return false;
                }
            }

            HashSet<string> guideIds = new(System.StringComparer.Ordinal);
            for (int guideIndex = 0; guideIndex < texture.Guides.Count; guideIndex++)
            {
                PolyTextureGuide guide = texture.Guides[guideIndex];
                string guidePath = $"{path}.guides[{guideIndex}]";
                if (string.IsNullOrWhiteSpace(guide.Id) || !guideIds.Add(guide.Id))
                {
                    error = $"{guidePath}.id must be unique within texture";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(guide.Name))
                {
                    error = $"{guidePath}.name is required";
                    return false;
                }
                if (!float.IsFinite(guide.Position.X) || !float.IsFinite(guide.Position.Y)
                    || !float.IsFinite(guide.AxisEnd.X) || !float.IsFinite(guide.AxisEnd.Y))
                {
                    error = $"{guidePath} must contain finite coordinates";
                    return false;
                }
            }

            HashSet<string> outputIds = new(System.StringComparer.Ordinal);
            bool hasHeightOutput = false;
            for (int outputIndex = 0; outputIndex < texture.Outputs.Count; outputIndex++)
            {
                PolyTextureOutputBinding output = texture.Outputs[outputIndex];
                string outputPath = $"{path}.outputs[{outputIndex}]";
                if (string.IsNullOrWhiteSpace(output.Id) || !outputIds.Add(output.Id))
                {
                    error = $"{outputPath}.id must be unique within texture";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(output.Name))
                {
                    error = $"{outputPath}.name is required";
                    return false;
                }
                if (output.SourceElementIds.Count == 0)
                {
                    error = $"{outputPath}.source_element_ids must contain at least one element";
                    return false;
                }
                HashSet<string> outputSourceIds = new(System.StringComparer.Ordinal);
                foreach (string sourceElementId in output.SourceElementIds)
                {
                    if (!elementIds.Contains(sourceElementId) || !outputSourceIds.Add(sourceElementId))
                    {
                        error = $"{outputPath}.source_element_ids must contain unique existing elements";
                        return false;
                    }
                }
                if (!float.IsFinite(output.Value) || output.Value < 0.0f || output.Value > 1.0f)
                {
                    error = $"{outputPath}.value must be between 0 and 1";
                    return false;
                }
                if (!float.IsFinite(output.HeightAmplitudeCm) || output.HeightAmplitudeCm <= 0.0f)
                {
                    error = $"{outputPath}.height_amplitude_cm must be finite and positive";
                    return false;
                }
                if (output.Kind == PolyTextureOutputKind.Height && hasHeightOutput)
                {
                    error = $"{path} may contain only one height output";
                    return false;
                }
                hasHeightOutput |= output.Kind == PolyTextureOutputKind.Height;
            }

            HashSet<string> evaluatedElementIds = new(System.StringComparer.Ordinal);
            foreach (PolyTextureElement element in texture.Elements)
            {
                if (element is SweepGeneratorElement sweep)
                {
                    if (texture.GetCenterStroke(sweep.SourceElementId) == null
                        || texture.GetCenterStroke(sweep.TargetElementId) == null
                        || !evaluatedElementIds.Contains(sweep.SourceElementId)
                        || !evaluatedElementIds.Contains(sweep.TargetElementId))
                    {
                        error = $"{path}.elements[{element.Id}] must reference preceding stroke or path inputs";
                        return false;
                    }
                }
                else if (element is MirrorGeneratorElement mirror)
                {
                    PolyTextureGuide axis = texture.GetGuide(mirror.AxisGuideId);
                    if (!evaluatedElementIds.Contains(mirror.SourceElementId)
                        || axis?.Type != PolyTextureGuideType.Axis)
                    {
                        error = $"{path}.elements[{element.Id}] must reference a preceding source and an existing axis guide";
                        return false;
                    }
                }
                else if (element is RepeatGridGeneratorElement repeat)
                {
                    if (!evaluatedElementIds.Contains(repeat.SourceElementId))
                    {
                        error = $"{path}.elements[{element.Id}] must reference a preceding source";
                        return false;
                    }
                }
                else if (element is BranchGeneratorElement branch)
                {
                    if (texture.GetElement(branch.SourceElementId) is not CrackLineElement
                        || !evaluatedElementIds.Contains(branch.SourceElementId))
                    {
                        error = $"{path}.elements[{element.Id}] must reference a preceding crack line";
                        return false;
                    }
                }
                else if (element is InvertFilterElement invert)
                {
                    if (!evaluatedElementIds.Contains(invert.SourceElementId))
                    {
                        error = $"{path}.elements[{element.Id}] must reference a preceding field source";
                        return false;
                    }
                }
                else if (element is EdgeFalloffFilterElement falloff)
                {
                    if (!evaluatedElementIds.Contains(falloff.SourceElementId))
                    {
                        error = $"{path}.elements[{element.Id}] must reference a preceding field source";
                        return false;
                    }
                }
                evaluatedElementIds.Add(element.Id);
            }
        }

        if (document.Textures.Count == 0)
        {
            return true;
        }

        if (document.SelectionKind == PolyTextureSelectionKind.Guide && document.ActiveGuide == null)
        {
            error = "active_guide_id must refer to a guide on the active texture";
            return false;
        }

        if (document.SelectionKind == PolyTextureSelectionKind.Output && document.ActiveOutput == null)
        {
            error = "active_output_id must refer to an output on the active texture";
            return false;
        }

        if (document.SelectionKind is not PolyTextureSelectionKind.Texture and not PolyTextureSelectionKind.Guide and not PolyTextureSelectionKind.Output
            && document.ActiveElement == null)
        {
            error = "active_element_id must refer to an element on the active texture";
            return false;
        }

        if (document.SelectionKind == PolyTextureSelectionKind.Point && document.ActivePoint == null)
        {
            error = "selected_point_index must refer to a point when selection_kind is point";
            return false;
        }

        bool activeTextureExists = false;
        foreach (PolyTextureItem texture in document.Textures)
        {
            activeTextureExists = activeTextureExists || texture.Id.Equals(document.ActiveTextureId, System.StringComparison.Ordinal);
        }

        if (!activeTextureExists)
        {
            error = "active_texture_id must refer to a texture";
            return false;
        }

        return true;
    }

    private static bool ValidateElement(PolyTextureElement element, string path, out string error)
    {
        error = string.Empty;

        if (element.Opacity < 0.0f || element.Opacity > 1.0f)
        {
            error = $"{path}.opacity must be between 0 and 1";
            return false;
        }

        if (element is CenterStrokeElement centerStroke && !ValidateCenterStroke(centerStroke, path, out error))
        {
            return false;
        }

        if (element is RectangleRegionElement rectangleRegion && !ValidateRectangleRegion(rectangleRegion, path, out error))
        {
            return false;
        }

        if (element is SweepGeneratorElement sweep && !ValidateSweep(sweep, path, out error))
        {
            return false;
        }

        if (element is RepeatGridGeneratorElement repeat && !ValidateRepeatGrid(repeat, path, out error))
        {
            return false;
        }

        if (element is BranchGeneratorElement branch && !ValidateBranch(branch, path, out error))
        {
            return false;
        }

        if (element is InvertFilterElement invert && string.IsNullOrWhiteSpace(invert.SourceElementId))
        {
            error = $"{path}.source_element_id is required";
            return false;
        }

        if (element is EdgeFalloffFilterElement falloff
            && (string.IsNullOrWhiteSpace(falloff.SourceElementId)
                || !float.IsFinite(falloff.RadiusCm) || falloff.RadiusCm < 0.0f
                || !float.IsFinite(falloff.Exponent) || falloff.Exponent <= 0.0f))
        {
            error = $"{path} contains invalid edge falloff values";
            return false;
        }

        if (element is MirrorGeneratorElement mirror
            && (string.IsNullOrWhiteSpace(mirror.SourceElementId) || string.IsNullOrWhiteSpace(mirror.AxisGuideId)))
        {
            error = $"{path} source element and axis guide ids are required";
            return false;
        }

        return true;
    }

    private static bool ValidateSweep(SweepGeneratorElement sweep, string path, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(sweep.SourceElementId) || string.IsNullOrWhiteSpace(sweep.TargetElementId))
        {
            error = $"{path} source and target element ids are required";
            return false;
        }
        if (sweep.Count < 1 || sweep.StartT < 0.0f || sweep.EndT > 1.0f || sweep.StartT > sweep.EndT)
        {
            error = $"{path} sweep range or count is invalid";
            return false;
        }
        if (sweep.LengthScaleStart <= 0.0f || sweep.LengthScaleEnd <= 0.0f
            || sweep.WidthScaleStart <= 0.0f || sweep.WidthScaleEnd <= 0.0f)
        {
            error = $"{path} sweep scales must be positive";
            return false;
        }
        return true;
    }

    private static bool ValidateRectangleRegion(RectangleRegionElement region, string path, out string error)
    {
        error = string.Empty;
        float maximumRadius = Mathf.Min(region.WidthCm, region.HeightCm) * 0.5f;
        if (!float.IsFinite(region.Position.X) || !float.IsFinite(region.Position.Y)
            || !float.IsFinite(region.RotationDegrees)
            || !float.IsFinite(region.WidthCm) || !float.IsFinite(region.HeightCm)
            || !float.IsFinite(region.CornerRadiusCm)
            || region.WidthCm <= 0.0f || region.HeightCm <= 0.0f
            || region.CornerRadiusCm < 0.0f || region.CornerRadiusCm > maximumRadius)
        {
            error = $"{path} contains invalid rectangle region values";
            return false;
        }
        return true;
    }

    private static bool ValidateRepeatGrid(RepeatGridGeneratorElement repeat, string path, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(repeat.SourceElementId)
            || repeat.Columns < 1 || repeat.Rows < 1 || repeat.Columns * repeat.Rows > 16384
            || !float.IsFinite(repeat.StepXCm) || !float.IsFinite(repeat.StepYCm)
            || !float.IsFinite(repeat.AlternateRowOffsetXCm)
            || repeat.StepXCm <= 0.0f || repeat.StepYCm <= 0.0f)
        {
            error = $"{path} contains invalid repeat grid values";
            return false;
        }
        return true;
    }

    private static bool ValidateBranch(BranchGeneratorElement branch, string path, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(branch.SourceElementId)
            || branch.Seed < 0 || branch.Count < 1 || branch.Count > 4096
            || branch.Segments < 1 || branch.Segments > 32
            || !float.IsFinite(branch.StartT) || !float.IsFinite(branch.EndT)
            || branch.StartT < 0.0f || branch.EndT > 1.0f || branch.StartT > branch.EndT
            || !float.IsFinite(branch.LengthMinCm) || !float.IsFinite(branch.LengthMaxCm)
            || branch.LengthMinCm <= 0.0f || branch.LengthMaxCm < branch.LengthMinCm
            || !float.IsFinite(branch.AngleMinDegrees) || !float.IsFinite(branch.AngleMaxDegrees)
            || branch.AngleMinDegrees < 0.0f || branch.AngleMaxDegrees < branch.AngleMinDegrees || branch.AngleMaxDegrees > 180.0f
            || !float.IsFinite(branch.WidthScale) || branch.WidthScale <= 0.0f
            || !float.IsFinite(branch.Irregularity) || branch.Irregularity < 0.0f || branch.Irregularity > 1.0f)
        {
            error = $"{path} contains invalid branch generator values";
            return false;
        }
        return true;
    }

    private static bool ValidateCenterStroke(CenterStrokeElement centerStroke, string path, out string error)
    {
        error = string.Empty;

        if (centerStroke.Falloff < 0.0f)
        {
            error = $"{path}.falloff must be >= 0";
            return false;
        }

        if (centerStroke.Transform.LengthScale <= 0.0f || centerStroke.Transform.WidthScale <= 0.0f)
        {
            error = $"{path}.transform scales must be positive";
            return false;
        }

        for (int pointIndex = 0; pointIndex < centerStroke.Points.Count; pointIndex++)
        {
            CenterStrokePoint point = centerStroke.Points[pointIndex];
            if (point.LeftWidth < 0.0f || point.RightWidth < 0.0f)
            {
                error = $"{path}.points[{pointIndex}] widths must be >= 0";
                return false;
            }
        }

        return true;
    }

    public static bool ValidateJson(string json, out string error)
    {
        Json parser = new();
        Error parseResult = parser.Parse(json);

        if (parseResult != Error.Ok)
        {
            error = $"parse error at line {parser.GetErrorLine()}: {parser.GetErrorMessage()}";
            return false;
        }

        if (parser.Data.VariantType != Variant.Type.Dictionary)
        {
            error = "root must be an object";
            return false;
        }

        return ValidateDictionary(parser.Data.AsGodotDictionary(), out error);
    }

    private static bool ValidateDictionary(Godot.Collections.Dictionary root, out string error)
    {
        error = string.Empty;

        if (!RequireInt(root, "schema_version", out int schemaVersion, out error)
            || schemaVersion != PolyTextureDocument.CurrentSchemaVersion)
        {
            error = string.IsNullOrEmpty(error) ? $"schema_version must be {PolyTextureDocument.CurrentSchemaVersion}" : error;
            return false;
        }

        if (!RequireString(root, "document_type", out string documentType, out error) || !documentType.Equals("polytexture", System.StringComparison.Ordinal))
        {
            error = string.IsNullOrEmpty(error) ? "document_type must be polytexture" : error;
            return false;
        }

        if (!RequireDictionary(root, "preview_defaults", out Godot.Collections.Dictionary previewDefaults, out error)
            || !RequireInt(previewDefaults, "width_px", out int width, out error, "preview_defaults")
            || !RequireInt(previewDefaults, "height_px", out int height, out error, "preview_defaults")
            || !RequireBool(root, "snap_enabled", out _, out error)
            || !RequireString(root, "active_texture_id", out string activeTextureId, out error)
            || !RequireArray(root, "textures", out Godot.Collections.Array textures, out error))
        {
            return false;
        }

        if (width <= 0 || height <= 0)
        {
            error = "preview default size must be positive";
            return false;
        }

        if (!RequireNumber(root, "snap_step_cm", out float snapStepCm, out error)
            || !float.IsFinite(snapStepCm)
            || snapStepCm <= 0.0f)
        {
            error = string.IsNullOrEmpty(error) ? "snap_step_cm must be finite and positive" : error;
            return false;
        }

        bool activeTextureExists = false;
        HashSet<string> ids = new(System.StringComparer.Ordinal);
        for (int textureIndex = 0; textureIndex < textures.Count; textureIndex++)
        {
            if (textures[textureIndex].VariantType != Variant.Type.Dictionary)
            {
                error = $"textures[{textureIndex}] must be an object";
                return false;
            }

            Godot.Collections.Dictionary texture = textures[textureIndex].AsGodotDictionary();
            string texturePath = $"textures[{textureIndex}]";
            if (!RequireString(texture, "id", out string id, out error, texturePath)
                || !RequireString(texture, "name", out _, out error, texturePath)
                || !RequireBool(texture, "visible", out _, out error, texturePath))
            {
                return false;
            }

            if (!RequireDictionary(texture, "domain", out Godot.Collections.Dictionary domain, out error, texturePath)
                || !RequireNumber(domain, "width_cm", out float domainWidthCm, out error, $"{texturePath}.domain")
                || !RequireNumber(domain, "height_cm", out float domainHeightCm, out error, $"{texturePath}.domain")
                || !RequireDictionary(texture, "preview", out Godot.Collections.Dictionary preview, out error, texturePath)
                || !RequireInt(preview, "width_px", out int previewWidthPx, out error, $"{texturePath}.preview")
                || !RequireInt(preview, "height_px", out int previewHeightPx, out error, $"{texturePath}.preview"))
            {
                return false;
            }

            if (!float.IsFinite(domainWidthCm) || !float.IsFinite(domainHeightCm)
                || domainWidthCm <= 0.0f || domainHeightCm <= 0.0f
                || previewWidthPx <= 0 || previewHeightPx <= 0)
            {
                error = $"{texturePath} domain and preview sizes must be positive";
                return false;
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                error = $"{texturePath}.id is required";
                return false;
            }

            if (!ids.Add(id))
            {
                error = $"{texturePath}.id must be unique";
                return false;
            }

            activeTextureExists = activeTextureExists || id.Equals(activeTextureId, System.StringComparison.Ordinal);
            if (!RequireArray(texture, "elements", out Godot.Collections.Array elements, out error, texturePath)
                || !ValidateElementsDictionary(elements, $"{texturePath}.elements", out error))
            {
                return false;
            }

            if (!RequireArray(texture, "guides", out Godot.Collections.Array guides, out error, texturePath)
                || !ValidateGuidesDictionary(guides, $"{texturePath}.guides", out error))
            {
                return false;
            }

            if (!RequireArray(texture, "outputs", out Godot.Collections.Array outputs, out error, texturePath)
                || !ValidateOutputsDictionary(outputs, $"{texturePath}.outputs", elements, out error))
            {
                return false;
            }
        }

        if (textures.Count > 0 && !activeTextureExists)
        {
            error = "active_texture_id must refer to a texture";
            return false;
        }

        return true;
    }

    private static bool ValidateElementsDictionary(Godot.Collections.Array elements, string path, out string error)
    {
        error = string.Empty;
        HashSet<string> elementIds = new(System.StringComparer.Ordinal);
        for (int elementIndex = 0; elementIndex < elements.Count; elementIndex++)
        {
            if (elements[elementIndex].VariantType != Variant.Type.Dictionary)
            {
                error = $"{path}[{elementIndex}] must be an object";
                return false;
            }

            Godot.Collections.Dictionary element = elements[elementIndex].AsGodotDictionary();
            string elementPath = $"{path}[{elementIndex}]";
            if (!RequireString(element, "id", out string id, out error, elementPath)
                || !RequireString(element, "type", out string type, out error, elementPath))
            {
                return false;
            }

            if (!RequireString(element, "name", out _, out error, elementPath))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                error = $"{elementPath}.id is required";
                return false;
            }

            if (!elementIds.Add(id))
            {
                error = $"{elementPath}.id must be unique within texture";
                return false;
            }

            bool isCenterStroke = type.Equals(CenterStrokeElement.ElementType, System.StringComparison.Ordinal);
            bool isCenterPath = type.Equals(CenterPathElement.ElementType, System.StringComparison.Ordinal);
            bool isCrackLine = type.Equals(CrackLineElement.ElementType, System.StringComparison.Ordinal);
            if (isCenterStroke || isCenterPath || isCrackLine)
            {
                if (!ValidateCenterStrokeDictionary(element, elementPath, requireIdentity: true, supportsBezierHandles: isCenterPath || isCrackLine, out error))
                {
                    return false;
                }
            }
            else if (type.Equals(RectangleRegionElement.ElementType, System.StringComparison.Ordinal))
            {
                if (!ValidateRectangleRegionDictionary(element, elementPath, out error))
                {
                    return false;
                }
            }
            else if (type.Equals(SweepGeneratorElement.ElementType, System.StringComparison.Ordinal))
            {
                if (!ValidateSweepDictionary(element, elementPath, out error))
                {
                    return false;
                }
            }
            else if (type.Equals(RepeatGridGeneratorElement.ElementType, System.StringComparison.Ordinal))
            {
                if (!ValidateRepeatGridDictionary(element, elementPath, out error))
                {
                    return false;
                }
            }
            else if (type.Equals(BranchGeneratorElement.ElementType, System.StringComparison.Ordinal))
            {
                if (!ValidateBranchDictionary(element, elementPath, out error))
                {
                    return false;
                }
            }
            else if (type.Equals(InvertFilterElement.ElementType, System.StringComparison.Ordinal))
            {
                if (!ValidateInvertDictionary(element, elementPath, out error))
                {
                    return false;
                }
            }
            else if (type.Equals(EdgeFalloffFilterElement.ElementType, System.StringComparison.Ordinal))
            {
                if (!ValidateEdgeFalloffDictionary(element, elementPath, out error))
                {
                    return false;
                }
            }
            else if (type.Equals(MirrorGeneratorElement.ElementType, System.StringComparison.Ordinal))
            {
                if (!ValidateMirrorDictionary(element, elementPath, out error))
                {
                    return false;
                }
            }
            else
            {
                error = $"{elementPath}.type is unsupported";
                return false;
            }
        }

        return true;
    }

    private static bool ValidateGuidesDictionary(Godot.Collections.Array guides, string path, out string error)
    {
        error = string.Empty;
        HashSet<string> ids = new(System.StringComparer.Ordinal);
        for (int guideIndex = 0; guideIndex < guides.Count; guideIndex++)
        {
            if (guides[guideIndex].VariantType != Variant.Type.Dictionary)
            {
                error = $"{path}[{guideIndex}] must be an object";
                return false;
            }

            Godot.Collections.Dictionary guide = guides[guideIndex].AsGodotDictionary();
            string guidePath = $"{path}[{guideIndex}]";
            if (!RequireString(guide, "id", out string id, out error, guidePath)
                || !RequireString(guide, "type", out string type, out error, guidePath)
                || !ValidateHandleVector(guide, "position", guidePath, out error)
                || !ValidateHandleVector(guide, "axis_end", guidePath, out error))
            {
                return false;
            }

            if (!RequireString(guide, "name", out _, out error, guidePath))
            {
                return false;
            }

            if (!ids.Add(id) || (type != "point" && type != "axis"))
            {
                error = $"{guidePath} has an invalid id or type";
                return false;
            }
        }
        return true;
    }

    private static bool ValidateOutputsDictionary(Godot.Collections.Array outputs, string path, Godot.Collections.Array elements, out string error)
    {
        error = string.Empty;
        HashSet<string> elementIds = new(System.StringComparer.Ordinal);
        foreach (Variant elementVariant in elements)
        {
            elementIds.Add(elementVariant.AsGodotDictionary()["id"].AsString());
        }

        HashSet<string> outputIds = new(System.StringComparer.Ordinal);
        bool hasHeightOutput = false;
        for (int outputIndex = 0; outputIndex < outputs.Count; outputIndex++)
        {
            if (outputs[outputIndex].VariantType != Variant.Type.Dictionary)
            {
                error = $"{path}[{outputIndex}] must be an object";
                return false;
            }

            Godot.Collections.Dictionary output = outputs[outputIndex].AsGodotDictionary();
            string outputPath = $"{path}[{outputIndex}]";
            if (!RequireString(output, "id", out string id, out error, outputPath)
                || !RequireString(output, "name", out _, out error, outputPath)
                || !RequireString(output, "kind", out string kind, out error, outputPath)
                || !RequireArray(output, "source_element_ids", out Godot.Collections.Array sourceIds, out error, outputPath)
                || !RequireBool(output, "enabled", out _, out error, outputPath)
                || !RequireNumber(output, "value", out float value, out error, outputPath)
                || !RequireNumber(output, "height_amplitude_cm", out float amplitude, out error, outputPath))
            {
                return false;
            }

            bool isHeight = kind.Equals("height", System.StringComparison.Ordinal);
            HashSet<string> uniqueSourceIds = new(System.StringComparer.Ordinal);
            bool validSources = sourceIds.Count > 0;
            foreach (Variant sourceVariant in sourceIds)
            {
                validSources &= sourceVariant.VariantType == Variant.Type.String
                    && elementIds.Contains(sourceVariant.AsString())
                    && uniqueSourceIds.Add(sourceVariant.AsString());
            }

            if ((!isHeight && !kind.Equals("mask", System.StringComparison.Ordinal))
                || string.IsNullOrWhiteSpace(id)
                || !outputIds.Add(id)
                || !validSources
                || !float.IsFinite(value) || value < 0.0f || value > 1.0f
                || !float.IsFinite(amplitude) || amplitude <= 0.0f
                || isHeight && hasHeightOutput)
            {
                error = $"{outputPath} contains an invalid output binding";
                return false;
            }
            hasHeightOutput |= isHeight;
        }

        return true;
    }

    private static bool ValidateSweepDictionary(Godot.Collections.Dictionary sweep, string path, out string error)
    {
        if (!RequireBool(sweep, "enabled", out _, out error, path)
            || !RequireNumber(sweep, "opacity", out _, out error, path)
            || !RequireString(sweep, "source_element_id", out _, out error, path)
            || !RequireString(sweep, "target_element_id", out _, out error, path)
            || !RequireInt(sweep, "count", out int count, out error, path)
            || !RequireNumber(sweep, "start_t", out float startT, out error, path)
            || !RequireNumber(sweep, "end_t", out float endT, out error, path)
            || !RequireString(sweep, "alignment", out _, out error, path)
            || !RequireString(sweep, "side_mode", out _, out error, path)
            || !RequireNumber(sweep, "rotation_offset_degrees", out _, out error, path)
            || !RequireNumber(sweep, "length_scale_start", out float lengthStart, out error, path)
            || !RequireNumber(sweep, "length_scale_end", out float lengthEnd, out error, path)
            || !RequireNumber(sweep, "width_scale_start", out float widthStart, out error, path)
            || !RequireNumber(sweep, "width_scale_end", out float widthEnd, out error, path)
            || !RequireBool(sweep, "render_source", out _, out error, path))
        {
            return false;
        }

        if (count < 1 || startT < 0.0f || endT > 1.0f || startT > endT
            || lengthStart <= 0.0f || lengthEnd <= 0.0f || widthStart <= 0.0f || widthEnd <= 0.0f)
        {
            error = $"{path} contains invalid sweep values";
            return false;
        }
        return true;
    }

    private static bool ValidateRectangleRegionDictionary(Godot.Collections.Dictionary region, string path, out string error)
    {
        if (!RequireBool(region, "enabled", out _, out error, path)
            || !RequireNumber(region, "opacity", out float opacity, out error, path)
            || !ValidateHandleVector(region, "position", path, out error)
            || !RequireNumber(region, "rotation_degrees", out float rotation, out error, path)
            || !RequireNumber(region, "width_cm", out float width, out error, path)
            || !RequireNumber(region, "height_cm", out float height, out error, path)
            || !RequireNumber(region, "corner_radius_cm", out float radius, out error, path))
        {
            return false;
        }
        if (!float.IsFinite(opacity) || opacity < 0.0f || opacity > 1.0f
            || !float.IsFinite(rotation) || !float.IsFinite(width) || !float.IsFinite(height) || !float.IsFinite(radius)
            || width <= 0.0f || height <= 0.0f || radius < 0.0f || radius > Mathf.Min(width, height) * 0.5f)
        {
            error = $"{path} contains invalid rectangle region values";
            return false;
        }
        return true;
    }

    private static bool ValidateRepeatGridDictionary(Godot.Collections.Dictionary repeat, string path, out string error)
    {
        if (!RequireBool(repeat, "enabled", out _, out error, path)
            || !RequireNumber(repeat, "opacity", out float opacity, out error, path)
            || !RequireString(repeat, "source_element_id", out string sourceId, out error, path)
            || !RequireInt(repeat, "columns", out int columns, out error, path)
            || !RequireInt(repeat, "rows", out int rows, out error, path)
            || !RequireNumber(repeat, "step_x_cm", out float stepX, out error, path)
            || !RequireNumber(repeat, "step_y_cm", out float stepY, out error, path)
            || !RequireNumber(repeat, "alternate_row_offset_x_cm", out float rowOffset, out error, path))
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(sourceId) || opacity < 0.0f || opacity > 1.0f
            || columns < 1 || rows < 1 || columns * rows > 16384
            || !float.IsFinite(stepX) || !float.IsFinite(stepY) || !float.IsFinite(rowOffset)
            || stepX <= 0.0f || stepY <= 0.0f)
        {
            error = $"{path} contains invalid repeat grid values";
            return false;
        }
        return true;
    }

    private static bool ValidateBranchDictionary(Godot.Collections.Dictionary branch, string path, out string error)
    {
        if (!RequireBool(branch, "enabled", out _, out error, path)
            || !RequireNumber(branch, "opacity", out float opacity, out error, path)
            || !RequireString(branch, "source_element_id", out string sourceId, out error, path)
            || !RequireInt(branch, "seed", out int seed, out error, path)
            || !RequireInt(branch, "count", out int count, out error, path)
            || !RequireInt(branch, "segments", out int segments, out error, path)
            || !RequireNumber(branch, "start_t", out float startT, out error, path)
            || !RequireNumber(branch, "end_t", out float endT, out error, path)
            || !RequireNumber(branch, "length_min_cm", out float lengthMin, out error, path)
            || !RequireNumber(branch, "length_max_cm", out float lengthMax, out error, path)
            || !RequireNumber(branch, "angle_min_degrees", out float angleMin, out error, path)
            || !RequireNumber(branch, "angle_max_degrees", out float angleMax, out error, path)
            || !RequireNumber(branch, "width_scale", out float widthScale, out error, path)
            || !RequireNumber(branch, "irregularity", out float irregularity, out error, path)
            || !RequireBool(branch, "render_source", out _, out error, path))
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(sourceId) || opacity < 0.0f || opacity > 1.0f
            || seed < 0 || count < 1 || count > 4096 || segments < 1 || segments > 32
            || startT < 0.0f || endT > 1.0f || startT > endT
            || lengthMin <= 0.0f || lengthMax < lengthMin
            || angleMin < 0.0f || angleMax < angleMin || angleMax > 180.0f
            || widthScale <= 0.0f || irregularity < 0.0f || irregularity > 1.0f)
        {
            error = $"{path} contains invalid branch generator values";
            return false;
        }
        return true;
    }

    private static bool ValidateInvertDictionary(Godot.Collections.Dictionary invert, string path, out string error)
    {
        if (!RequireBool(invert, "enabled", out _, out error, path)
            || !RequireNumber(invert, "opacity", out float opacity, out error, path)
            || !RequireString(invert, "source_element_id", out string sourceId, out error, path))
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(sourceId) || !float.IsFinite(opacity) || opacity < 0.0f || opacity > 1.0f)
        {
            error = $"{path} contains invalid invert filter values";
            return false;
        }
        return true;
    }

    private static bool ValidateEdgeFalloffDictionary(Godot.Collections.Dictionary falloff, string path, out string error)
    {
        if (!RequireBool(falloff, "enabled", out _, out error, path)
            || !RequireNumber(falloff, "opacity", out float opacity, out error, path)
            || !RequireString(falloff, "source_element_id", out string sourceId, out error, path)
            || !RequireNumber(falloff, "radius_cm", out float radius, out error, path)
            || !RequireNumber(falloff, "exponent", out float exponent, out error, path))
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(sourceId) || opacity < 0.0f || opacity > 1.0f
            || !float.IsFinite(radius) || radius < 0.0f
            || !float.IsFinite(exponent) || exponent <= 0.0f)
        {
            error = $"{path} contains invalid edge falloff values";
            return false;
        }
        return true;
    }

    private static bool ValidateMirrorDictionary(Godot.Collections.Dictionary mirror, string path, out string error)
    {
        if (!RequireBool(mirror, "enabled", out _, out error, path)
            || !RequireNumber(mirror, "opacity", out _, out error, path)
            || !RequireString(mirror, "source_element_id", out string sourceId, out error, path)
            || !RequireString(mirror, "axis_guide_id", out string axisId, out error, path)
            || !RequireBool(mirror, "render_source", out _, out error, path))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(axisId))
        {
            error = $"{path} source and axis are required";
            return false;
        }
        return true;
    }

    private static bool ValidateCenterStrokeDictionary(Godot.Collections.Dictionary centerStroke, string path, bool requireIdentity, bool supportsBezierHandles, out string error)
    {
        if (requireIdentity
            && (!RequireString(centerStroke, "id", out _, out error, path)
                || !RequireString(centerStroke, "type", out _, out error, path)))
        {
            return false;
        }

        if (!RequireBool(centerStroke, "enabled", out _, out error, path)
            || !RequireBool(centerStroke, "symmetry", out _, out error, path)
            || !RequireNumber(centerStroke, "opacity", out float opacity, out error, path)
            || !RequireNumber(centerStroke, "falloff", out float falloff, out error, path)
            || !RequireDictionary(centerStroke, "transform", out Godot.Collections.Dictionary transform, out error, path)
            || !ValidateHandleVector(transform, "position", $"{path}.transform", out error)
            || !RequireNumber(transform, "rotation_degrees", out float rotationDegrees, out error, $"{path}.transform")
            || !RequireNumber(transform, "length_scale", out float lengthScale, out error, $"{path}.transform")
            || !RequireNumber(transform, "width_scale", out float widthScale, out error, $"{path}.transform")
            || !RequireArray(centerStroke, "points", out Godot.Collections.Array points, out error, path))
        {
            return false;
        }

        if (opacity < 0.0f || opacity > 1.0f)
        {
            error = $"{path}.opacity must be between 0 and 1";
            return false;
        }

        if (falloff < 0.0f)
        {
            error = $"{path}.falloff must be >= 0";
            return false;
        }

        if (!float.IsFinite(rotationDegrees) || !float.IsFinite(lengthScale) || !float.IsFinite(widthScale)
            || lengthScale <= 0.0f || widthScale <= 0.0f)
        {
            error = $"{path}.transform must contain finite values and positive scales";
            return false;
        }

        for (int pointIndex = 0; pointIndex < points.Count; pointIndex++)
        {
            if (points[pointIndex].VariantType != Variant.Type.Dictionary)
            {
                error = $"{path}.points[{pointIndex}] must be an object";
                return false;
            }

            Godot.Collections.Dictionary point = points[pointIndex].AsGodotDictionary();
            string pointPath = $"{path}.points[{pointIndex}]";
            if (!RequireNumber(point, "x", out _, out error, pointPath)
                || !RequireNumber(point, "y", out _, out error, pointPath)
                || !RequireNumber(point, "left_width", out float leftWidth, out error, pointPath)
                || !RequireNumber(point, "right_width", out float rightWidth, out error, pointPath))
            {
                return false;
            }

            if (leftWidth < 0.0f || rightWidth < 0.0f)
            {
                error = $"{pointPath} widths must be >= 0";
                return false;
            }

            if (point.ContainsKey("handle_mode"))
            {
                if (!RequireString(point, "handle_mode", out string handleMode, out error, pointPath)
                    || !handleMode.Equals("linear", System.StringComparison.Ordinal)
                    && !handleMode.Equals("free", System.StringComparison.Ordinal)
                    && !handleMode.Equals("aligned", System.StringComparison.Ordinal)
                    && !handleMode.Equals("mirrored", System.StringComparison.Ordinal))
                {
                    error = $"{pointPath}.handle_mode is invalid";
                    return false;
                }
            }

            if (supportsBezierHandles
                && (!RequireString(point, "handle_mode", out string pathHandleMode, out error, pointPath)
                    || pathHandleMode.Equals("linear", System.StringComparison.Ordinal)
                    || !ValidateHandleVector(point, "in_handle", pointPath, out error)
                    || !ValidateHandleVector(point, "out_handle", pointPath, out error)))
            {
                error = string.IsNullOrEmpty(error) ? $"{pointPath} must contain non-linear Bezier handle data" : error;
                return false;
            }

            if (point.ContainsKey("in_handle") && !ValidateHandleVector(point, "in_handle", pointPath, out error))
            {
                return false;
            }

            if (point.ContainsKey("out_handle") && !ValidateHandleVector(point, "out_handle", pointPath, out error))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateHandleVector(Godot.Collections.Dictionary point, string key, string path, out string error)
    {
        error = string.Empty;
        if (!RequireDictionary(point, key, out Godot.Collections.Dictionary handle, out error, path)
            || !RequireNumber(handle, "x", out float x, out error, $"{path}.{key}")
            || !RequireNumber(handle, "y", out float y, out error, $"{path}.{key}"))
        {
            return false;
        }

        if (!float.IsFinite(x) || !float.IsFinite(y))
        {
            error = $"{path}.{key} must contain finite values";
            return false;
        }

        return true;
    }

    private static bool RequireDictionary(Godot.Collections.Dictionary dictionary, string key, out Godot.Collections.Dictionary value, out string error, string path = "")
    {
        error = string.Empty;
        value = null;

        if (!dictionary.ContainsKey(key) || dictionary[key].VariantType != Variant.Type.Dictionary)
        {
            error = $"{FieldPath(path, key)} must be an object";
            return false;
        }

        value = dictionary[key].AsGodotDictionary();
        return true;
    }

    private static bool RequireArray(Godot.Collections.Dictionary dictionary, string key, out Godot.Collections.Array value, out string error, string path = "")
    {
        error = string.Empty;
        value = null;

        if (!dictionary.ContainsKey(key) || dictionary[key].VariantType != Variant.Type.Array)
        {
            error = $"{FieldPath(path, key)} must be an array";
            return false;
        }

        value = dictionary[key].AsGodotArray();
        return true;
    }

    private static bool RequireString(Godot.Collections.Dictionary dictionary, string key, out string value, out string error, string path = "")
    {
        error = string.Empty;
        value = string.Empty;

        if (!dictionary.ContainsKey(key) || dictionary[key].VariantType != Variant.Type.String)
        {
            error = $"{FieldPath(path, key)} must be a string";
            return false;
        }

        value = dictionary[key].AsString();
        return true;
    }

    private static bool RequireBool(Godot.Collections.Dictionary dictionary, string key, out bool value, out string error, string path = "")
    {
        error = string.Empty;
        value = false;

        if (!dictionary.ContainsKey(key) || dictionary[key].VariantType != Variant.Type.Bool)
        {
            error = $"{FieldPath(path, key)} must be a bool";
            return false;
        }

        value = dictionary[key].AsBool();
        return true;
    }

    private static bool RequireInt(Godot.Collections.Dictionary dictionary, string key, out int value, out string error, string path = "")
    {
        error = string.Empty;
        value = 0;

        if (!dictionary.ContainsKey(key))
        {
            error = $"{FieldPath(path, key)} must be an int";
            return false;
        }

        Variant variant = dictionary[key];
        if (variant.VariantType != Variant.Type.Int && variant.VariantType != Variant.Type.Float)
        {
            error = $"{FieldPath(path, key)} must be an int";
            return false;
        }

        value = variant.AsInt32();
        return true;
    }

    private static bool RequireNumber(Godot.Collections.Dictionary dictionary, string key, out float value, out string error, string path = "")
    {
        error = string.Empty;
        value = 0.0f;

        if (!dictionary.ContainsKey(key))
        {
            error = $"{FieldPath(path, key)} must be a number";
            return false;
        }

        Variant variant = dictionary[key];
        if (variant.VariantType != Variant.Type.Int && variant.VariantType != Variant.Type.Float)
        {
            error = $"{FieldPath(path, key)} must be a number";
            return false;
        }

        value = variant.AsSingle();
        return true;
    }

    private static string FieldPath(string path, string key)
    {
        return string.IsNullOrEmpty(path) ? key : $"{path}.{key}";
    }
}
