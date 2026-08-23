using Godot;
using System.Globalization;
using System.Text;

public static class PolyTextureStore
{
    public static bool Save(string path, PolyTextureDocument document, out string error)
    {
        error = string.Empty;

        if (!PolyTextureValidator.Validate(document, out error))
        {
            return false;
        }

        string json = ToJson(document);
        if (!PolyTextureValidator.ValidateJson(json, out error))
        {
            return false;
        }

        EnsureDirectory(GetPathDirectory(path));
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            error = $"could not open for write: {path}";
            return false;
        }

        file.StoreString(json);
        return true;
    }

    public static bool Load(string path, out PolyTextureDocument document, out string error)
    {
        document = null;
        error = string.Empty;

        if (!FileAccess.FileExists(path))
        {
            error = $"file not found: {path}";
            return false;
        }

        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            error = $"could not open for read: {path}";
            return false;
        }

        string json = file.GetAsText();
        if (!PolyTextureValidator.ValidateJson(json, out error))
        {
            return false;
        }

        Json parser = new();
        if (parser.Parse(json) != Error.Ok || parser.Data.VariantType != Variant.Type.Dictionary)
        {
            error = "validated JSON could not be parsed";
            return false;
        }

        document = FromDictionary(parser.Data.AsGodotDictionary());
        return PolyTextureValidator.Validate(document, out error);
    }

    public static string ToJson(PolyTextureDocument document)
    {
        document.EnsureSelection();

        StringBuilder builder = new();
        builder.AppendLine("{");
        builder.AppendLine($"  \"schema_version\": {document.SchemaVersion},");
        builder.AppendLine($"  \"document_type\": \"{EscapeJson(document.DocumentType)}\",");
        builder.AppendLine($"  \"name\": \"{EscapeJson(document.Name)}\",");
        builder.AppendLine("  \"preview_defaults\": {");
        builder.AppendLine($"    \"width_px\": {document.DefaultPreviewWidthPx},");
        builder.AppendLine($"    \"height_px\": {document.DefaultPreviewHeightPx}");
        builder.AppendLine("  },");
        builder.AppendLine($"  \"snap_enabled\": {JsonBool(document.SnapEnabled)},");
        builder.AppendLine($"  \"snap_step_cm\": {Number(document.SnapStepCm)},");
        builder.AppendLine($"  \"active_texture_id\": \"{EscapeJson(document.ActiveTextureId)}\",");
        builder.AppendLine($"  \"active_element_id\": \"{EscapeJson(document.ActiveElementId)}\",");
        builder.AppendLine($"  \"active_guide_id\": \"{EscapeJson(document.ActiveGuideId)}\",");
        builder.AppendLine($"  \"active_output_id\": \"{EscapeJson(document.ActiveOutputId)}\",");
        builder.AppendLine($"  \"selection_kind\": \"{EscapeJson(document.SelectionKind.ToString().ToLowerInvariant())}\",");
        builder.AppendLine($"  \"selected_point_index\": {document.SelectedPointIndex},");
        builder.AppendLine("  \"textures\": [");

        for (int textureIndex = 0; textureIndex < document.Textures.Count; textureIndex++)
        {
            PolyTextureItem texture = document.Textures[textureIndex];
            string textureSuffix = textureIndex == document.Textures.Count - 1 ? string.Empty : ",";
            builder.AppendLine("    {");
            builder.AppendLine($"      \"id\": \"{EscapeJson(texture.Id)}\",");
            builder.AppendLine($"      \"name\": \"{EscapeJson(texture.Name)}\",");
            builder.AppendLine($"      \"origin\": \"{EscapeJson(OriginModeToJson(texture.OriginMode))}\",");
            builder.AppendLine("      \"domain\": {");
            builder.AppendLine($"        \"width_cm\": {Number(texture.DomainWidthCm)},");
            builder.AppendLine($"        \"height_cm\": {Number(texture.DomainHeightCm)}");
            builder.AppendLine("      },");
            builder.AppendLine("      \"preview\": {");
            builder.AppendLine($"        \"width_px\": {texture.PreviewWidthPx},");
            builder.AppendLine($"        \"height_px\": {texture.PreviewHeightPx}");
            builder.AppendLine("      },");
            builder.AppendLine($"      \"visible\": {JsonBool(texture.Visible)},");
            builder.AppendLine("      \"elements\": [");
            for (int elementIndex = 0; elementIndex < texture.Elements.Count; elementIndex++)
            {
                string elementSuffix = elementIndex == texture.Elements.Count - 1 ? string.Empty : ",";
                AppendElement(builder, texture.Elements[elementIndex], "        ", elementSuffix);
            }
            builder.AppendLine("      ],");
            builder.AppendLine("      \"guides\": [");
            for (int guideIndex = 0; guideIndex < texture.Guides.Count; guideIndex++)
            {
                string guideSuffix = guideIndex == texture.Guides.Count - 1 ? string.Empty : ",";
                AppendGuide(builder, texture.Guides[guideIndex], "        ", guideSuffix);
            }
            builder.AppendLine("      ],");
            builder.AppendLine("      \"outputs\": [");
            for (int outputIndex = 0; outputIndex < texture.Outputs.Count; outputIndex++)
            {
                string outputSuffix = outputIndex == texture.Outputs.Count - 1 ? string.Empty : ",";
                AppendOutput(builder, texture.Outputs[outputIndex], "        ", outputSuffix);
            }
            builder.AppendLine("      ]");
            builder.AppendLine($"    }}{textureSuffix}");
        }

        builder.AppendLine("  ]");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendElement(StringBuilder builder, PolyTextureElement element, string indent, string suffix)
    {
        if (element is RectangleRegionElement rectangleRegion)
        {
            AppendRectangleRegion(builder, rectangleRegion, indent, suffix);
        }
        else if (element is MirrorGeneratorElement mirror)
        {
            AppendMirror(builder, mirror, indent, suffix);
        }
        else if (element is RepeatGridGeneratorElement repeat)
        {
            AppendRepeatGrid(builder, repeat, indent, suffix);
        }
        else if (element is InvertFilterElement invert)
        {
            AppendInvert(builder, invert, indent, suffix);
        }
        else if (element is SweepGeneratorElement sweep)
        {
            AppendSweep(builder, sweep, indent, suffix);
        }
        else if (element is CrackLineElement crackLine)
        {
            AppendCenterStroke(builder, crackLine, includeBezierData: true, indent, suffix);
        }
        else if (element is CenterPathElement centerPath)
        {
            AppendCenterStroke(builder, centerPath, includeBezierData: true, indent, suffix);
        }
        else if (element is CenterStrokeElement centerStroke)
        {
            AppendCenterStroke(builder, centerStroke, includeBezierData: false, indent, suffix);
        }
    }

    private static void AppendInvert(StringBuilder builder, InvertFilterElement invert, string indent, string suffix)
    {
        builder.AppendLine($"{indent}{{");
        builder.AppendLine($"{indent}  \"id\": \"{EscapeJson(invert.Id)}\",");
        builder.AppendLine($"{indent}  \"name\": \"{EscapeJson(invert.Name)}\",");
        builder.AppendLine($"{indent}  \"type\": \"{InvertFilterElement.ElementType}\",");
        builder.AppendLine($"{indent}  \"enabled\": {JsonBool(invert.Enabled)},");
        builder.AppendLine($"{indent}  \"opacity\": {Number(invert.Opacity)},");
        builder.AppendLine($"{indent}  \"source_element_id\": \"{EscapeJson(invert.SourceElementId)}\"");
        builder.AppendLine($"{indent}}}{suffix}");
    }

    private static void AppendRepeatGrid(StringBuilder builder, RepeatGridGeneratorElement repeat, string indent, string suffix)
    {
        builder.AppendLine($"{indent}{{");
        builder.AppendLine($"{indent}  \"id\": \"{EscapeJson(repeat.Id)}\",");
        builder.AppendLine($"{indent}  \"name\": \"{EscapeJson(repeat.Name)}\",");
        builder.AppendLine($"{indent}  \"type\": \"{RepeatGridGeneratorElement.ElementType}\",");
        builder.AppendLine($"{indent}  \"enabled\": {JsonBool(repeat.Enabled)},");
        builder.AppendLine($"{indent}  \"opacity\": {Number(repeat.Opacity)},");
        builder.AppendLine($"{indent}  \"source_element_id\": \"{EscapeJson(repeat.SourceElementId)}\",");
        builder.AppendLine($"{indent}  \"columns\": {repeat.Columns},");
        builder.AppendLine($"{indent}  \"rows\": {repeat.Rows},");
        builder.AppendLine($"{indent}  \"step_x_cm\": {Number(repeat.StepXCm)},");
        builder.AppendLine($"{indent}  \"step_y_cm\": {Number(repeat.StepYCm)},");
        builder.AppendLine($"{indent}  \"alternate_row_offset_x_cm\": {Number(repeat.AlternateRowOffsetXCm)}");
        builder.AppendLine($"{indent}}}{suffix}");
    }

    private static void AppendRectangleRegion(StringBuilder builder, RectangleRegionElement region, string indent, string suffix)
    {
        builder.AppendLine($"{indent}{{");
        builder.AppendLine($"{indent}  \"id\": \"{EscapeJson(region.Id)}\",");
        builder.AppendLine($"{indent}  \"name\": \"{EscapeJson(region.Name)}\",");
        builder.AppendLine($"{indent}  \"type\": \"{RectangleRegionElement.ElementType}\",");
        builder.AppendLine($"{indent}  \"enabled\": {JsonBool(region.Enabled)},");
        builder.AppendLine($"{indent}  \"opacity\": {Number(region.Opacity)},");
        builder.AppendLine($"{indent}  \"position\": {{ \"x\": {Number(region.Position.X)}, \"y\": {Number(region.Position.Y)} }},");
        builder.AppendLine($"{indent}  \"rotation_degrees\": {Number(region.RotationDegrees)},");
        builder.AppendLine($"{indent}  \"width_cm\": {Number(region.WidthCm)},");
        builder.AppendLine($"{indent}  \"height_cm\": {Number(region.HeightCm)},");
        builder.AppendLine($"{indent}  \"corner_radius_cm\": {Number(region.CornerRadiusCm)}");
        builder.AppendLine($"{indent}}}{suffix}");
    }

    private static void AppendGuide(StringBuilder builder, PolyTextureGuide guide, string indent, string suffix)
    {
        builder.AppendLine($"{indent}{{");
        builder.AppendLine($"{indent}  \"id\": \"{EscapeJson(guide.Id)}\",");
        builder.AppendLine($"{indent}  \"name\": \"{EscapeJson(guide.Name)}\",");
        builder.AppendLine($"{indent}  \"type\": \"{GuideTypeToJson(guide.Type)}\",");
        builder.AppendLine($"{indent}  \"position\": {{ \"x\": {Number(guide.Position.X)}, \"y\": {Number(guide.Position.Y)} }},");
        builder.AppendLine($"{indent}  \"axis_end\": {{ \"x\": {Number(guide.AxisEnd.X)}, \"y\": {Number(guide.AxisEnd.Y)} }}");
        builder.AppendLine($"{indent}}}{suffix}");
    }

    private static void AppendOutput(StringBuilder builder, PolyTextureOutputBinding output, string indent, string suffix)
    {
        builder.AppendLine($"{indent}{{");
        builder.AppendLine($"{indent}  \"id\": \"{EscapeJson(output.Id)}\",");
        builder.AppendLine($"{indent}  \"name\": \"{EscapeJson(output.Name)}\",");
        builder.AppendLine($"{indent}  \"kind\": \"{OutputKindToJson(output.Kind)}\",");
        builder.Append($"{indent}  \"source_element_ids\": [");
        for (int sourceIndex = 0; sourceIndex < output.SourceElementIds.Count; sourceIndex++)
        {
            string sourceSuffix = sourceIndex == output.SourceElementIds.Count - 1 ? string.Empty : ", ";
            builder.Append($"\"{EscapeJson(output.SourceElementIds[sourceIndex])}\"{sourceSuffix}");
        }
        builder.AppendLine("],");
        builder.AppendLine($"{indent}  \"enabled\": {JsonBool(output.Enabled)},");
        builder.AppendLine($"{indent}  \"value\": {Number(output.Value)},");
        builder.AppendLine($"{indent}  \"height_amplitude_cm\": {Number(output.HeightAmplitudeCm)}");
        builder.AppendLine($"{indent}}}{suffix}");
    }

    private static void AppendSweep(StringBuilder builder, SweepGeneratorElement sweep, string indent, string suffix)
    {
        builder.AppendLine($"{indent}{{");
        builder.AppendLine($"{indent}  \"id\": \"{EscapeJson(sweep.Id)}\",");
        builder.AppendLine($"{indent}  \"name\": \"{EscapeJson(sweep.Name)}\",");
        builder.AppendLine($"{indent}  \"type\": \"{SweepGeneratorElement.ElementType}\",");
        builder.AppendLine($"{indent}  \"enabled\": {JsonBool(sweep.Enabled)},");
        builder.AppendLine($"{indent}  \"opacity\": {Number(sweep.Opacity)},");
        builder.AppendLine($"{indent}  \"source_element_id\": \"{EscapeJson(sweep.SourceElementId)}\",");
        builder.AppendLine($"{indent}  \"target_element_id\": \"{EscapeJson(sweep.TargetElementId)}\",");
        builder.AppendLine($"{indent}  \"count\": {sweep.Count},");
        builder.AppendLine($"{indent}  \"start_t\": {Number(sweep.StartT)},");
        builder.AppendLine($"{indent}  \"end_t\": {Number(sweep.EndT)},");
        builder.AppendLine($"{indent}  \"alignment\": \"{SweepAlignmentToJson(sweep.Alignment)}\",");
        builder.AppendLine($"{indent}  \"side_mode\": \"{SweepSideModeToJson(sweep.SideMode)}\",");
        builder.AppendLine($"{indent}  \"rotation_offset_degrees\": {Number(sweep.RotationOffsetDegrees)},");
        builder.AppendLine($"{indent}  \"length_scale_start\": {Number(sweep.LengthScaleStart)},");
        builder.AppendLine($"{indent}  \"length_scale_end\": {Number(sweep.LengthScaleEnd)},");
        builder.AppendLine($"{indent}  \"width_scale_start\": {Number(sweep.WidthScaleStart)},");
        builder.AppendLine($"{indent}  \"width_scale_end\": {Number(sweep.WidthScaleEnd)},");
        builder.AppendLine($"{indent}  \"render_source\": {JsonBool(sweep.RenderSource)}");
        builder.AppendLine($"{indent}}}{suffix}");
    }

    private static void AppendMirror(StringBuilder builder, MirrorGeneratorElement mirror, string indent, string suffix)
    {
        builder.AppendLine($"{indent}{{");
        builder.AppendLine($"{indent}  \"id\": \"{EscapeJson(mirror.Id)}\",");
        builder.AppendLine($"{indent}  \"name\": \"{EscapeJson(mirror.Name)}\",");
        builder.AppendLine($"{indent}  \"type\": \"{MirrorGeneratorElement.ElementType}\",");
        builder.AppendLine($"{indent}  \"enabled\": {JsonBool(mirror.Enabled)},");
        builder.AppendLine($"{indent}  \"opacity\": {Number(mirror.Opacity)},");
        builder.AppendLine($"{indent}  \"source_element_id\": \"{EscapeJson(mirror.SourceElementId)}\",");
        builder.AppendLine($"{indent}  \"axis_guide_id\": \"{EscapeJson(mirror.AxisGuideId)}\",");
        builder.AppendLine($"{indent}  \"render_source\": {JsonBool(mirror.RenderSource)}");
        builder.AppendLine($"{indent}}}{suffix}");
    }

    private static void AppendCenterStroke(StringBuilder builder, CenterStrokeElement centerStroke, bool includeBezierData, string indent, string suffix)
    {
        builder.AppendLine($"{indent}{{");
        builder.AppendLine($"{indent}  \"id\": \"{EscapeJson(centerStroke.Id)}\",");
        builder.AppendLine($"{indent}  \"name\": \"{EscapeJson(centerStroke.Name)}\",");
        builder.AppendLine($"{indent}  \"type\": \"{EscapeJson(centerStroke.Type)}\",");
        builder.AppendLine($"{indent}  \"enabled\": {JsonBool(centerStroke.Enabled)},");
        builder.AppendLine($"{indent}  \"opacity\": {Number(centerStroke.Opacity)},");
        builder.AppendLine($"{indent}  \"symmetry\": {JsonBool(centerStroke.Symmetry)},");
        builder.AppendLine($"{indent}  \"falloff\": {Number(centerStroke.Falloff)},");
        builder.AppendLine($"{indent}  \"transform\": {{");
        builder.AppendLine($"{indent}    \"position\": {{ \"x\": {Number(centerStroke.Transform.Position.X)}, \"y\": {Number(centerStroke.Transform.Position.Y)} }},");
        builder.AppendLine($"{indent}    \"rotation_degrees\": {Number(centerStroke.Transform.RotationDegrees)},");
        builder.AppendLine($"{indent}    \"length_scale\": {Number(centerStroke.Transform.LengthScale)},");
        builder.AppendLine($"{indent}    \"width_scale\": {Number(centerStroke.Transform.WidthScale)}");
        builder.AppendLine($"{indent}  }},");
        builder.AppendLine($"{indent}  \"points\": [");

        for (int pointIndex = 0; pointIndex < centerStroke.Points.Count; pointIndex++)
        {
            CenterStrokePoint point = centerStroke.Points[pointIndex];
            string pointSuffix = pointIndex == centerStroke.Points.Count - 1 ? string.Empty : ",";
            builder.Append($"{indent}    {{ ");
            builder.Append($"\"x\": {Number(point.X)}, ");
            builder.Append($"\"y\": {Number(point.Y)}, ");
            builder.Append($"\"left_width\": {Number(point.LeftWidth)}, ");
            builder.Append($"\"right_width\": {Number(point.RightWidth)}");
            if (includeBezierData)
            {
                builder.Append($", \"handle_mode\": \"{EscapeJson(HandleModeToJson(point.HandleMode))}\"");
                builder.Append($", \"in_handle\": {{ \"x\": {Number(point.InHandle.X)}, \"y\": {Number(point.InHandle.Y)} }}");
                builder.Append($", \"out_handle\": {{ \"x\": {Number(point.OutHandle.X)}, \"y\": {Number(point.OutHandle.Y)} }}");
            }
            builder.Append(' ');
            builder.AppendLine($"}}{pointSuffix}");
        }

        builder.AppendLine($"{indent}  ]");
        builder.AppendLine($"{indent}}}{suffix}");
    }

    private static PolyTextureDocument FromDictionary(Godot.Collections.Dictionary root)
    {
        Godot.Collections.Dictionary previewDefaults = root["preview_defaults"].AsGodotDictionary();
        Godot.Collections.Array textures = root["textures"].AsGodotArray();
        int defaultPreviewWidth = ReadInt(previewDefaults, "width_px", 512);
        int defaultPreviewHeight = ReadInt(previewDefaults, "height_px", 512);
        PolyTextureDocument document = new()
        {
            SchemaVersion = PolyTextureDocument.CurrentSchemaVersion,
            DocumentType = root["document_type"].AsString(),
            Name = root.ContainsKey("name") && root["name"].VariantType == Variant.Type.String ? root["name"].AsString() : "Untitled",
            DefaultPreviewWidthPx = defaultPreviewWidth,
            DefaultPreviewHeightPx = defaultPreviewHeight,
            SnapEnabled = ReadBool(root, "snap_enabled", false),
            SnapStepCm = ReadFloat(root, "snap_step_cm", PolyTextureUnits.DefaultSnapStepCm),
            ActiveTextureId = root["active_texture_id"].AsString(),
            ActiveElementId = ReadString(root, "active_element_id", "center_stroke"),
            ActiveGuideId = ReadString(root, "active_guide_id", string.Empty),
            ActiveOutputId = ReadString(root, "active_output_id", string.Empty),
            SelectedPointIndex = ReadInt(root, "selected_point_index", -1),
            SelectionKind = ReadSelectionKind(ReadString(root, "selection_kind", "texture"))
        };

        document.Textures.Clear();
        foreach (Variant textureVariant in textures)
        {
            Godot.Collections.Dictionary texture = textureVariant.AsGodotDictionary();
            Godot.Collections.Dictionary preview = texture["preview"].AsGodotDictionary();
            Godot.Collections.Dictionary domain = texture["domain"].AsGodotDictionary();
            int previewWidth = ReadInt(preview, "width_px", defaultPreviewWidth);
            int previewHeight = ReadInt(preview, "height_px", defaultPreviewHeight);
            float domainWidthCm = ReadFloat(domain, "width_cm", PolyTextureUnits.DefaultDomainSizeCm);
            float domainHeightCm = ReadFloat(domain, "height_cm", PolyTextureUnits.DefaultDomainSizeCm);
            PolyTextureItem item = new()
            {
                Id = texture["id"].AsString(),
                Name = texture["name"].AsString(),
                OriginMode = ReadOriginMode(ReadString(texture, "origin", "bottom_left")),
                DomainWidthCm = domainWidthCm,
                DomainHeightCm = domainHeightCm,
                PreviewWidthPx = previewWidth,
                PreviewHeightPx = previewHeight,
                Visible = texture["visible"].AsBool()
            };

            item.Elements.Clear();
            foreach (Variant elementVariant in texture["elements"].AsGodotArray())
            {
                item.Elements.Add(ReadElement(elementVariant.AsGodotDictionary()));
            }

            if (texture.ContainsKey("guides") && texture["guides"].VariantType == Variant.Type.Array)
            {
                foreach (Variant guideVariant in texture["guides"].AsGodotArray())
                {
                    item.Guides.Add(ReadGuide(guideVariant.AsGodotDictionary()));
                }
            }

            foreach (Variant outputVariant in texture["outputs"].AsGodotArray())
            {
                item.Outputs.Add(ReadOutput(outputVariant.AsGodotDictionary()));
            }

            document.Textures.Add(item);
        }

        document.EnsureSelection();
        return document;
    }

    private static PolyTextureElement ReadElement(Godot.Collections.Dictionary element)
    {
        string type = element["type"].AsString();
        if (type.Equals(CenterStrokeElement.ElementType, System.StringComparison.Ordinal))
        {
            return ReadCenterStroke(element, supportsBezierHandles: false);
        }

        if (type.Equals(RectangleRegionElement.ElementType, System.StringComparison.Ordinal))
        {
            return ReadRectangleRegion(element);
        }

        if (type.Equals(CenterPathElement.ElementType, System.StringComparison.Ordinal))
        {
            return ReadCenterStroke(element, CenterPathElement.ElementType);
        }

        if (type.Equals(CrackLineElement.ElementType, System.StringComparison.Ordinal))
        {
            return ReadCenterStroke(element, CrackLineElement.ElementType);
        }

        if (type.Equals(SweepGeneratorElement.ElementType, System.StringComparison.Ordinal))
        {
            return ReadSweep(element);
        }

        if (type.Equals(RepeatGridGeneratorElement.ElementType, System.StringComparison.Ordinal))
        {
            return ReadRepeatGrid(element);
        }

        if (type.Equals(InvertFilterElement.ElementType, System.StringComparison.Ordinal))
        {
            return ReadInvert(element);
        }

        if (type.Equals(MirrorGeneratorElement.ElementType, System.StringComparison.Ordinal))
        {
            return ReadMirror(element);
        }

        return new CenterStrokeElement
        {
            Id = ReadString(element, "id", "center_stroke01")
        };
    }

    private static InvertFilterElement ReadInvert(Godot.Collections.Dictionary invert)
    {
        return new InvertFilterElement
        {
            Id = ReadString(invert, "id", "invert"),
            Name = ReadString(invert, "name", "Invert"),
            Enabled = ReadBool(invert, "enabled", true),
            Opacity = ReadFloat(invert, "opacity", 0.86f),
            SourceElementId = ReadString(invert, "source_element_id", string.Empty)
        };
    }

    private static RepeatGridGeneratorElement ReadRepeatGrid(Godot.Collections.Dictionary repeat)
    {
        return new RepeatGridGeneratorElement
        {
            Id = ReadString(repeat, "id", "repeat_grid"),
            Name = ReadString(repeat, "name", "Repeat Grid"),
            Enabled = ReadBool(repeat, "enabled", true),
            Opacity = ReadFloat(repeat, "opacity", 0.86f),
            SourceElementId = ReadString(repeat, "source_element_id", string.Empty),
            Columns = ReadInt(repeat, "columns", 1),
            Rows = ReadInt(repeat, "rows", 1),
            StepXCm = ReadFloat(repeat, "step_x_cm", 1.0f),
            StepYCm = ReadFloat(repeat, "step_y_cm", 1.0f),
            AlternateRowOffsetXCm = ReadFloat(repeat, "alternate_row_offset_x_cm", 0.0f)
        };
    }

    private static RectangleRegionElement ReadRectangleRegion(Godot.Collections.Dictionary region)
    {
        return new RectangleRegionElement
        {
            Id = ReadString(region, "id", "rectangle_region"),
            Name = ReadString(region, "name", "Rectangle Region"),
            Enabled = ReadBool(region, "enabled", true),
            Opacity = ReadFloat(region, "opacity", 0.86f),
            Position = ReadVector2(region, "position"),
            RotationDegrees = ReadFloat(region, "rotation_degrees", 0.0f),
            WidthCm = ReadFloat(region, "width_cm", 80.0f),
            HeightCm = ReadFloat(region, "height_cm", 40.0f),
            CornerRadiusCm = ReadFloat(region, "corner_radius_cm", 0.0f)
        };
    }

    private static SweepGeneratorElement ReadSweep(Godot.Collections.Dictionary sweep)
    {
        return new SweepGeneratorElement
        {
            Id = ReadString(sweep, "id", "sweep01"),
            Name = ReadString(sweep, "name", ReadString(sweep, "id", "Sweep")),
            Enabled = ReadBool(sweep, "enabled", true),
            Opacity = ReadFloat(sweep, "opacity", 0.86f),
            SourceElementId = ReadString(sweep, "source_element_id", string.Empty),
            TargetElementId = ReadString(sweep, "target_element_id", string.Empty),
            Count = ReadInt(sweep, "count", 8),
            StartT = ReadFloat(sweep, "start_t", 0.0f),
            EndT = ReadFloat(sweep, "end_t", 1.0f),
            Alignment = ReadSweepAlignment(ReadString(sweep, "alignment", "normal")),
            SideMode = ReadSweepSideMode(ReadString(sweep, "side_mode", "right")),
            RotationOffsetDegrees = ReadFloat(sweep, "rotation_offset_degrees", 0.0f),
            LengthScaleStart = ReadFloat(sweep, "length_scale_start", 1.0f),
            LengthScaleEnd = ReadFloat(sweep, "length_scale_end", 0.1f),
            WidthScaleStart = ReadFloat(sweep, "width_scale_start", 1.0f),
            WidthScaleEnd = ReadFloat(sweep, "width_scale_end", 0.1f),
            RenderSource = ReadBool(sweep, "render_source", false)
        };
    }

    private static MirrorGeneratorElement ReadMirror(Godot.Collections.Dictionary mirror)
    {
        return new MirrorGeneratorElement
        {
            Id = ReadString(mirror, "id", "mirror01"),
            Name = ReadString(mirror, "name", ReadString(mirror, "id", "Mirror")),
            Enabled = ReadBool(mirror, "enabled", true),
            Opacity = ReadFloat(mirror, "opacity", 0.86f),
            SourceElementId = ReadString(mirror, "source_element_id", string.Empty),
            AxisGuideId = ReadString(mirror, "axis_guide_id", string.Empty),
            RenderSource = ReadBool(mirror, "render_source", false)
        };
    }

    private static PolyTextureGuide ReadGuide(Godot.Collections.Dictionary guide)
    {
        return new PolyTextureGuide
        {
            Id = ReadString(guide, "id", "guide01"),
            Name = ReadString(guide, "name", ReadString(guide, "id", "Guide")),
            Type = ReadGuideType(ReadString(guide, "type", "point")),
            Position = ReadVector2(guide, "position"),
            AxisEnd = ReadVector2(guide, "axis_end")
        };
    }

    private static PolyTextureOutputBinding ReadOutput(Godot.Collections.Dictionary output)
    {
        PolyTextureOutputBinding binding = new()
        {
            Id = ReadString(output, "id", "output"),
            Name = ReadString(output, "name", "Output"),
            Kind = ReadOutputKind(ReadString(output, "kind", "mask")),
            Enabled = ReadBool(output, "enabled", true),
            Value = ReadFloat(output, "value", 1.0f),
            HeightAmplitudeCm = ReadFloat(output, "height_amplitude_cm", 1.0f)
        };
        foreach (Variant sourceVariant in output["source_element_ids"].AsGodotArray())
        {
            binding.SourceElementIds.Add(sourceVariant.AsString());
        }
        return binding;
    }

    private static CenterStrokeElement ReadCenterStroke(Godot.Collections.Dictionary centerStroke, bool supportsBezierHandles)
    {
        return ReadCenterStroke(centerStroke, supportsBezierHandles ? CenterPathElement.ElementType : CenterStrokeElement.ElementType);
    }

    private static CenterStrokeElement ReadCenterStroke(Godot.Collections.Dictionary centerStroke, string elementType)
    {
        bool supportsBezierHandles = !elementType.Equals(CenterStrokeElement.ElementType, System.StringComparison.Ordinal);
        CenterStrokeElement element = elementType.Equals(CrackLineElement.ElementType, System.StringComparison.Ordinal) ? new CrackLineElement
        {
            Id = ReadString(centerStroke, "id", "crack_line"),
            Name = ReadString(centerStroke, "name", ReadString(centerStroke, "id", "Crack Line")),
            Enabled = centerStroke["enabled"].AsBool(),
            Opacity = centerStroke["opacity"].AsSingle(),
            Symmetry = centerStroke["symmetry"].AsBool(),
            Falloff = centerStroke["falloff"].AsSingle()
        } : supportsBezierHandles ? new CenterPathElement
        {
            Id = ReadString(centerStroke, "id", "center_stroke"),
            Name = ReadString(centerStroke, "name", ReadString(centerStroke, "id", "Center Path")),
            Enabled = centerStroke["enabled"].AsBool(),
            Opacity = centerStroke["opacity"].AsSingle(),
            Symmetry = centerStroke["symmetry"].AsBool(),
            Falloff = centerStroke["falloff"].AsSingle()
        } : new CenterStrokeElement
        {
            Id = ReadString(centerStroke, "id", "center_stroke"),
            Name = ReadString(centerStroke, "name", ReadString(centerStroke, "id", "Center Stroke")),
            Enabled = centerStroke["enabled"].AsBool(),
            Opacity = centerStroke["opacity"].AsSingle(),
            Symmetry = centerStroke["symmetry"].AsBool(),
            Falloff = centerStroke["falloff"].AsSingle()
        };

        Godot.Collections.Array points = centerStroke["points"].AsGodotArray();
        foreach (Variant pointVariant in points)
        {
            Godot.Collections.Dictionary point = pointVariant.AsGodotDictionary();
            element.Points.Add(new CenterStrokePoint
            {
                X = point["x"].AsSingle(),
                Y = point["y"].AsSingle(),
                LeftWidth = point["left_width"].AsSingle(),
                RightWidth = point["right_width"].AsSingle(),
                HandleMode = supportsBezierHandles ? ReadPathHandleMode(ReadString(point, "handle_mode", "aligned")) : PolyTextureHandleMode.Linear,
                InHandle = supportsBezierHandles ? ReadVector2(point, "in_handle") : Vector2.Zero,
                OutHandle = supportsBezierHandles ? ReadVector2(point, "out_handle") : Vector2.Zero
            });
        }

        Godot.Collections.Dictionary transform = centerStroke["transform"].AsGodotDictionary();
        element.Transform.Position = ReadVector2(transform, "position");
        element.Transform.RotationDegrees = ReadFloat(transform, "rotation_degrees", 0.0f);
        element.Transform.LengthScale = ReadFloat(transform, "length_scale", 1.0f);
        element.Transform.WidthScale = ReadFloat(transform, "width_scale", 1.0f);

        element.NormalizeLocalAxes();

        return element;
    }

    private static string ReadString(Godot.Collections.Dictionary dictionary, string key, string fallback)
    {
        return dictionary.ContainsKey(key) && dictionary[key].VariantType == Variant.Type.String ? dictionary[key].AsString() : fallback;
    }

    private static int ReadInt(Godot.Collections.Dictionary dictionary, string key, int fallback)
    {
        return dictionary.ContainsKey(key)
            && (dictionary[key].VariantType == Variant.Type.Int || dictionary[key].VariantType == Variant.Type.Float)
            ? dictionary[key].AsInt32()
            : fallback;
    }

    private static bool ReadBool(Godot.Collections.Dictionary dictionary, string key, bool fallback)
    {
        return dictionary.ContainsKey(key) && dictionary[key].VariantType == Variant.Type.Bool ? dictionary[key].AsBool() : fallback;
    }

    private static float ReadFloat(Godot.Collections.Dictionary dictionary, string key, float fallback)
    {
        return dictionary.ContainsKey(key) && (dictionary[key].VariantType == Variant.Type.Float || dictionary[key].VariantType == Variant.Type.Int)
            ? dictionary[key].AsSingle()
            : fallback;
    }

    private static Vector2 ReadVector2(Godot.Collections.Dictionary dictionary, string key)
    {
        if (!dictionary.ContainsKey(key) || dictionary[key].VariantType != Variant.Type.Dictionary)
        {
            return Vector2.Zero;
        }

        Godot.Collections.Dictionary vector = dictionary[key].AsGodotDictionary();
        return new Vector2(ReadFloat(vector, "x", 0.0f), ReadFloat(vector, "y", 0.0f));
    }

    private static PolyTextureHandleMode ReadHandleMode(string value)
    {
        return value switch
        {
            "linear" => PolyTextureHandleMode.Linear,
            "free" => PolyTextureHandleMode.Free,
            "aligned" => PolyTextureHandleMode.Aligned,
            "mirrored" => PolyTextureHandleMode.Mirrored,
            _ => PolyTextureHandleMode.Linear
        };
    }

    private static PolyTextureHandleMode ReadPathHandleMode(string value)
    {
        PolyTextureHandleMode mode = ReadHandleMode(value);
        return mode == PolyTextureHandleMode.Linear ? PolyTextureHandleMode.Aligned : mode;
    }

    private static string HandleModeToJson(PolyTextureHandleMode mode)
    {
        return mode switch
        {
            PolyTextureHandleMode.Linear => "linear",
            PolyTextureHandleMode.Free => "free",
            PolyTextureHandleMode.Aligned => "aligned",
            PolyTextureHandleMode.Mirrored => "mirrored",
            _ => "free"
        };
    }

    private static PolyTextureSweepAlignment ReadSweepAlignment(string value)
    {
        return value == "tangent" ? PolyTextureSweepAlignment.Tangent : PolyTextureSweepAlignment.Normal;
    }

    private static string SweepAlignmentToJson(PolyTextureSweepAlignment alignment)
    {
        return alignment == PolyTextureSweepAlignment.Tangent ? "tangent" : "normal";
    }

    private static PolyTextureSweepSideMode ReadSweepSideMode(string value)
    {
        return value switch
        {
            "right" => PolyTextureSweepSideMode.Right,
            "both" => PolyTextureSweepSideMode.Both,
            _ => PolyTextureSweepSideMode.Left
        };
    }

    private static string SweepSideModeToJson(PolyTextureSweepSideMode sideMode)
    {
        return sideMode switch
        {
            PolyTextureSweepSideMode.Right => "right",
            PolyTextureSweepSideMode.Both => "both",
            _ => "left"
        };
    }

    private static PolyTextureGuideType ReadGuideType(string value)
    {
        return value == "axis" ? PolyTextureGuideType.Axis : PolyTextureGuideType.Point;
    }

    private static PolyTextureOutputKind ReadOutputKind(string value)
    {
        return value == "height" ? PolyTextureOutputKind.Height : PolyTextureOutputKind.Mask;
    }

    private static string OutputKindToJson(PolyTextureOutputKind kind)
    {
        return kind == PolyTextureOutputKind.Height ? "height" : "mask";
    }

    private static string GuideTypeToJson(PolyTextureGuideType type)
    {
        return type == PolyTextureGuideType.Axis ? "axis" : "point";
    }

    private static PolyTextureSelectionKind ReadSelectionKind(string value)
    {
        return value switch
        {
            "element" => PolyTextureSelectionKind.Element,
            "point" => PolyTextureSelectionKind.Point,
            "guide" => PolyTextureSelectionKind.Guide,
            "output" => PolyTextureSelectionKind.Output,
            _ => PolyTextureSelectionKind.Texture
        };
    }

    private static PolyTextureOriginMode ReadOriginMode(string value)
    {
        return value switch
        {
            "bottom_center" => PolyTextureOriginMode.BottomCenter,
            "center" => PolyTextureOriginMode.Center,
            _ => PolyTextureOriginMode.BottomLeft
        };
    }

    private static string OriginModeToJson(PolyTextureOriginMode originMode)
    {
        return originMode switch
        {
            PolyTextureOriginMode.BottomCenter => "bottom_center",
            PolyTextureOriginMode.Center => "center",
            _ => "bottom_left"
        };
    }

    private static string Number(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string JsonBool(bool value)
    {
        return value ? "true" : "false";
    }

    private static string EscapeJson(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\", System.StringComparison.Ordinal)
            .Replace("\"", "\\\"", System.StringComparison.Ordinal);
    }

    private static string GetPathDirectory(string path)
    {
        int slashIndex = path.LastIndexOf('/');
        return slashIndex < 0 ? string.Empty : path[..slashIndex];
    }

    private static void EnsureDirectory(string path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(path));
        }
    }
}
