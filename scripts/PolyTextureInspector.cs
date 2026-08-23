using Godot;
using System;

public partial class PolyTextureInspector : PanelContainer
{
    private PolyTextureDocument _document;
    private bool _refreshing;

    private VBoxContainer _textureInspector;
    private VBoxContainer _elementInspector;
    private VBoxContainer _sweepInspector;
    private VBoxContainer _pointInspector;
    private VBoxContainer _outputInspector;
    private VBoxContainer _regionInspector;
    private VBoxContainer _repeatInspector;
    private VBoxContainer _branchInspector;
    private VBoxContainer _filterInspector;
    private VBoxContainer _edgeFalloffInspector;

    private LineEdit _textureNameLineEdit;
    private OptionButton _textureOriginOptionButton;
    private SpinBox _domainWidthSpinBox;
    private SpinBox _domainHeightSpinBox;
    private SpinBox _previewWidthSpinBox;
    private SpinBox _previewHeightSpinBox;
    private CheckBox _textureVisibleCheckBox;
    private LineEdit _elementNameLineEdit;
    private CheckBox _elementEnabledCheckBox;
    private CheckBox _symmetryCheckBox;
    private SpinBox _opacitySpinBox;
    private SpinBox _falloffSpinBox;
    private SpinBox _elementPositionXSpinBox;
    private SpinBox _elementPositionYSpinBox;
    private SpinBox _elementRotationSpinBox;
    private SpinBox _elementLengthScaleSpinBox;
    private SpinBox _elementWidthScaleSpinBox;
    private OptionButton _sweepSourceOptionButton;
    private OptionButton _sweepTargetOptionButton;
    private SpinBox _sweepCountSpinBox;
    private SpinBox _sweepStartTSpinBox;
    private SpinBox _sweepEndTSpinBox;
    private OptionButton _sweepAlignmentOptionButton;
    private OptionButton _sweepSideModeOptionButton;
    private SpinBox _sweepRotationOffsetSpinBox;
    private SpinBox _sweepLengthStartSpinBox;
    private SpinBox _sweepLengthEndSpinBox;
    private SpinBox _sweepWidthStartSpinBox;
    private SpinBox _sweepWidthEndSpinBox;
    private CheckBox _sweepRenderSourceCheckBox;
    private SpinBox _pointXSpinBox;
    private SpinBox _pointYSpinBox;
    private SpinBox _leftWidthSpinBox;
    private SpinBox _rightWidthSpinBox;
    private OptionButton _handleModeOptionButton;
    private SpinBox _inHandleXSpinBox;
    private SpinBox _inHandleYSpinBox;
    private SpinBox _outHandleXSpinBox;
    private SpinBox _outHandleYSpinBox;
    private HBoxContainer _leftWidthRow;
    private HBoxContainer _rightWidthRow;
    private HBoxContainer _handleModeRow;
    private HBoxContainer _inHandleXRow;
    private HBoxContainer _inHandleYRow;
    private HBoxContainer _outHandleXRow;
    private HBoxContainer _outHandleYRow;
    private HBoxContainer _falloffRow;
    private HBoxContainer _elementPositionXRow;
    private HBoxContainer _elementPositionYRow;
    private HBoxContainer _elementRotationRow;
    private HBoxContainer _elementLengthScaleRow;
    private HBoxContainer _elementWidthScaleRow;
    private LineEdit _outputNameLineEdit;
    private CheckBox _outputEnabledCheckBox;
    private Label _outputKindLabel;
    private Label _outputSourcesLabel;
    private SpinBox _outputValueSpinBox;
    private SpinBox _outputHeightAmplitudeSpinBox;
    private HBoxContainer _outputHeightAmplitudeRow;
    private SpinBox _regionPositionXSpinBox;
    private SpinBox _regionPositionYSpinBox;
    private SpinBox _regionRotationSpinBox;
    private SpinBox _regionWidthSpinBox;
    private SpinBox _regionHeightSpinBox;
    private SpinBox _regionCornerRadiusSpinBox;
    private Label _repeatSourceLabel;
    private SpinBox _repeatColumnsSpinBox;
    private SpinBox _repeatRowsSpinBox;
    private SpinBox _repeatStepXSpinBox;
    private SpinBox _repeatStepYSpinBox;
    private SpinBox _repeatRowOffsetSpinBox;
    private Label _branchSourceLabel;
    private SpinBox _branchSeedSpinBox;
    private SpinBox _branchCountSpinBox;
    private SpinBox _branchSegmentsSpinBox;
    private SpinBox _branchStartTSpinBox;
    private SpinBox _branchEndTSpinBox;
    private SpinBox _branchLengthMinSpinBox;
    private SpinBox _branchLengthMaxSpinBox;
    private SpinBox _branchAngleMinSpinBox;
    private SpinBox _branchAngleMaxSpinBox;
    private SpinBox _branchWidthScaleSpinBox;
    private SpinBox _branchIrregularitySpinBox;
    private CheckBox _branchRenderSourceCheckBox;
    private Label _filterSourceLabel;
    private Label _edgeFalloffSourceLabel;
    private SpinBox _edgeFalloffRadiusSpinBox;
    private SpinBox _edgeFalloffExponentSpinBox;

    public event Action DocumentChanged;

    public override void _Ready()
    {
        BuildUi();
        PolyTextureUiDefaults.Apply(this);
    }

    public void SetDocument(PolyTextureDocument document)
    {
        _document = document;
        Refresh();
    }

    public void Refresh()
    {
        if (_textureInspector == null || _document == null)
        {
            return;
        }

        _refreshing = true;
        PolyTextureItem texture = _document.ActiveTexture;
        PolyTextureElement selectedElement = _document.ActiveElement;
        CenterStrokeElement element = selectedElement as CenterStrokeElement;
        SweepGeneratorElement sweep = selectedElement as SweepGeneratorElement;
        PolyTexturePoint point = _document.ActivePoint;
        CenterStrokePoint centerStrokePoint = point as CenterStrokePoint;
        bool hasTexture = texture != null;
        bool supportsBezierHandles = element?.SupportsBezierHandles == true;
        PolyTextureOutputBinding output = _document.ActiveOutput;
        RectangleRegionElement region = selectedElement as RectangleRegionElement;
        RepeatGridGeneratorElement repeat = selectedElement as RepeatGridGeneratorElement;
        BranchGeneratorElement branch = selectedElement as BranchGeneratorElement;
        InvertFilterElement invert = selectedElement as InvertFilterElement;
        EdgeFalloffFilterElement edgeFalloff = selectedElement as EdgeFalloffFilterElement;

        _textureInspector.Visible = _document.SelectionKind == PolyTextureSelectionKind.Texture;
        _elementInspector.Visible = _document.SelectionKind == PolyTextureSelectionKind.Element;
        _sweepInspector.Visible = _document.SelectionKind == PolyTextureSelectionKind.Element && sweep != null;
        _pointInspector.Visible = _document.SelectionKind == PolyTextureSelectionKind.Point;
        _outputInspector.Visible = _document.SelectionKind == PolyTextureSelectionKind.Output;
        _regionInspector.Visible = _document.SelectionKind == PolyTextureSelectionKind.Element && region != null;
        _repeatInspector.Visible = _document.SelectionKind == PolyTextureSelectionKind.Element && repeat != null;
        _branchInspector.Visible = _document.SelectionKind == PolyTextureSelectionKind.Element && branch != null;
        _filterInspector.Visible = _document.SelectionKind == PolyTextureSelectionKind.Element && invert != null;
        _edgeFalloffInspector.Visible = _document.SelectionKind == PolyTextureSelectionKind.Element && edgeFalloff != null;
        _textureNameLineEdit.Editable = hasTexture;
        _textureOriginOptionButton.Disabled = !hasTexture;
        _domainWidthSpinBox.Editable = hasTexture;
        _domainHeightSpinBox.Editable = hasTexture;
        _previewWidthSpinBox.Editable = hasTexture;
        _previewHeightSpinBox.Editable = hasTexture;
        _textureVisibleCheckBox.Disabled = !hasTexture;

        if (texture != null)
        {
            _textureNameLineEdit.Text = texture.Name;
            _textureOriginOptionButton.Selected = OriginModeToOptionIndex(texture.OriginMode);
            _domainWidthSpinBox.Value = texture.DomainWidthCm;
            _domainHeightSpinBox.Value = texture.DomainHeightCm;
            _previewWidthSpinBox.Value = texture.PreviewWidthPx;
            _previewHeightSpinBox.Value = texture.PreviewHeightPx;
            _textureVisibleCheckBox.ButtonPressed = texture.Visible;
        }
        else
        {
            _textureNameLineEdit.Text = string.Empty;
            _textureOriginOptionButton.Selected = 0;
            _domainWidthSpinBox.Value = 0;
            _domainHeightSpinBox.Value = 0;
            _previewWidthSpinBox.Value = 0;
            _previewHeightSpinBox.Value = 0;
            _textureVisibleCheckBox.ButtonPressed = false;
        }

        if (selectedElement != null)
        {
            _elementNameLineEdit.Text = selectedElement.Name;
            _elementEnabledCheckBox.ButtonPressed = selectedElement.Enabled;
            _opacitySpinBox.Value = selectedElement.Opacity;
        }
        else
        {
            _elementNameLineEdit.Text = string.Empty;
            _elementEnabledCheckBox.ButtonPressed = false;
            _opacitySpinBox.Value = 0;
        }

        bool hasCenterElement = element != null;
        _symmetryCheckBox.Visible = hasCenterElement;
        _falloffRow.Visible = hasCenterElement;
        _elementPositionXRow.Visible = hasCenterElement;
        _elementPositionYRow.Visible = hasCenterElement;
        _elementRotationRow.Visible = hasCenterElement;
        _elementLengthScaleRow.Visible = hasCenterElement;
        _elementWidthScaleRow.Visible = hasCenterElement;
        if (element != null)
        {
            _symmetryCheckBox.ButtonPressed = element.Symmetry;
            _falloffSpinBox.Value = element.Falloff;
            Vector2 displayAnchor = ToDisplayPosition(element.Transform.Position);
            _elementPositionXSpinBox.Value = displayAnchor.X;
            _elementPositionYSpinBox.Value = displayAnchor.Y;
            _elementRotationSpinBox.Value = element.Transform.RotationDegrees;
            _elementLengthScaleSpinBox.Value = element.Transform.LengthScale;
            _elementWidthScaleSpinBox.Value = element.Transform.WidthScale;
        }
        else
        {
            _symmetryCheckBox.ButtonPressed = false;
            _falloffSpinBox.Value = 0;
            _elementPositionXSpinBox.Value = 0;
            _elementPositionYSpinBox.Value = 0;
            _elementRotationSpinBox.Value = 0;
            _elementLengthScaleSpinBox.Value = 1;
            _elementWidthScaleSpinBox.Value = 1;
        }

        if (sweep != null)
        {
            RefreshSweepElementOptions(sweep);
            _sweepCountSpinBox.Value = sweep.Count;
            _sweepStartTSpinBox.Value = sweep.StartT;
            _sweepEndTSpinBox.Value = sweep.EndT;
            _sweepAlignmentOptionButton.Selected = (int)sweep.Alignment;
            _sweepSideModeOptionButton.Selected = (int)sweep.SideMode;
            _sweepRotationOffsetSpinBox.Value = sweep.RotationOffsetDegrees;
            _sweepLengthStartSpinBox.Value = sweep.LengthScaleStart;
            _sweepLengthEndSpinBox.Value = sweep.LengthScaleEnd;
            _sweepWidthStartSpinBox.Value = sweep.WidthScaleStart;
            _sweepWidthEndSpinBox.Value = sweep.WidthScaleEnd;
            _sweepRenderSourceCheckBox.ButtonPressed = sweep.RenderSource;
        }

        bool hasPoint = point != null;
        bool hasCenterStrokePoint = centerStrokePoint != null;
        _pointXSpinBox.Editable = hasPoint;
        _pointYSpinBox.Editable = hasPoint;
        _leftWidthSpinBox.Editable = hasCenterStrokePoint;
        _rightWidthSpinBox.Editable = hasCenterStrokePoint;
        _handleModeOptionButton.Disabled = !hasCenterStrokePoint || !supportsBezierHandles;
        _inHandleXSpinBox.Editable = hasCenterStrokePoint && supportsBezierHandles;
        _inHandleYSpinBox.Editable = hasCenterStrokePoint && supportsBezierHandles;
        _outHandleXSpinBox.Editable = hasCenterStrokePoint && supportsBezierHandles;
        _outHandleYSpinBox.Editable = hasCenterStrokePoint && supportsBezierHandles;
        _leftWidthRow.Visible = hasCenterStrokePoint;
        _rightWidthRow.Visible = hasCenterStrokePoint;
        _handleModeRow.Visible = hasCenterStrokePoint && supportsBezierHandles;
        _inHandleXRow.Visible = hasCenterStrokePoint && supportsBezierHandles;
        _inHandleYRow.Visible = hasCenterStrokePoint && supportsBezierHandles;
        _outHandleXRow.Visible = hasCenterStrokePoint && supportsBezierHandles;
        _outHandleYRow.Visible = hasCenterStrokePoint && supportsBezierHandles;

        if (hasPoint)
        {
            _pointXSpinBox.Value = point.X;
            _pointYSpinBox.Value = point.Y;
            _leftWidthSpinBox.Value = centerStrokePoint?.LeftWidth ?? 0;
            _rightWidthSpinBox.Value = centerStrokePoint?.RightWidth ?? 0;
            _handleModeOptionButton.Selected = HandleModeToOptionIndex(centerStrokePoint?.HandleMode ?? PolyTextureHandleMode.Linear);
            _inHandleXSpinBox.Value = centerStrokePoint?.InHandle.X ?? 0;
            _inHandleYSpinBox.Value = centerStrokePoint?.InHandle.Y ?? 0;
            _outHandleXSpinBox.Value = centerStrokePoint?.OutHandle.X ?? 0;
            _outHandleYSpinBox.Value = centerStrokePoint?.OutHandle.Y ?? 0;
        }
        else
        {
            _pointXSpinBox.Value = 0;
            _pointYSpinBox.Value = 0;
            _leftWidthSpinBox.Value = 0;
            _rightWidthSpinBox.Value = 0;
            _handleModeOptionButton.Selected = 0;
            _inHandleXSpinBox.Value = 0;
            _inHandleYSpinBox.Value = 0;
            _outHandleXSpinBox.Value = 0;
            _outHandleYSpinBox.Value = 0;
        }


        if (output != null)
        {
            _outputNameLineEdit.Text = output.Name;
            _outputEnabledCheckBox.ButtonPressed = output.Enabled;
            _outputKindLabel.Text = output.Kind == PolyTextureOutputKind.Height ? "Height" : "Mask";
            _outputSourcesLabel.Text = string.Join(", ", output.SourceElementIds);
            _outputValueSpinBox.Value = output.Value;
            _outputHeightAmplitudeSpinBox.Value = output.HeightAmplitudeCm;
            _outputHeightAmplitudeRow.Visible = output.Kind == PolyTextureOutputKind.Height;
        }
        else
        {
            _outputNameLineEdit.Text = string.Empty;
            _outputEnabledCheckBox.ButtonPressed = false;
            _outputKindLabel.Text = string.Empty;
            _outputSourcesLabel.Text = string.Empty;
            _outputValueSpinBox.Value = 1.0;
            _outputHeightAmplitudeSpinBox.Value = 1.0;
            _outputHeightAmplitudeRow.Visible = false;
        }

        if (region != null)
        {
            Vector2 displayPosition = ToDisplayPosition(region.Position);
            _regionPositionXSpinBox.Value = displayPosition.X;
            _regionPositionYSpinBox.Value = displayPosition.Y;
            _regionRotationSpinBox.Value = region.RotationDegrees;
            _regionWidthSpinBox.Value = region.WidthCm;
            _regionHeightSpinBox.Value = region.HeightCm;
            _regionCornerRadiusSpinBox.MaxValue = Mathf.Min(region.WidthCm, region.HeightCm) * 0.5f;
            _regionCornerRadiusSpinBox.Value = region.CornerRadiusCm;
        }
        if (repeat != null)
        {
            _repeatSourceLabel.Text = repeat.SourceElementId;
            _repeatColumnsSpinBox.Value = repeat.Columns;
            _repeatRowsSpinBox.Value = repeat.Rows;
            _repeatStepXSpinBox.Value = repeat.StepXCm;
            _repeatStepYSpinBox.Value = repeat.StepYCm;
            _repeatRowOffsetSpinBox.Value = repeat.AlternateRowOffsetXCm;
        }
        if (branch != null)
        {
            _branchSourceLabel.Text = branch.SourceElementId;
            _branchSeedSpinBox.Value = branch.Seed;
            _branchCountSpinBox.Value = branch.Count;
            _branchSegmentsSpinBox.Value = branch.Segments;
            _branchStartTSpinBox.Value = branch.StartT;
            _branchEndTSpinBox.Value = branch.EndT;
            _branchLengthMinSpinBox.Value = branch.LengthMinCm;
            _branchLengthMaxSpinBox.Value = branch.LengthMaxCm;
            _branchAngleMinSpinBox.Value = branch.AngleMinDegrees;
            _branchAngleMaxSpinBox.Value = branch.AngleMaxDegrees;
            _branchWidthScaleSpinBox.Value = branch.WidthScale;
            _branchIrregularitySpinBox.Value = branch.Irregularity;
            _branchRenderSourceCheckBox.ButtonPressed = branch.RenderSource;
        }
        if (invert != null)
        {
            _filterSourceLabel.Text = invert.SourceElementId;
        }
        if (edgeFalloff != null)
        {
            _edgeFalloffSourceLabel.Text = edgeFalloff.SourceElementId;
            _edgeFalloffRadiusSpinBox.Value = edgeFalloff.RadiusCm;
            _edgeFalloffExponentSpinBox.Value = edgeFalloff.Exponent;
        }

        _refreshing = false;
    }

    private void BuildUi()
    {
        MarginContainer margin = new()
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        AddChild(margin);

        VBoxContainer stack = new();
        stack.AddThemeConstantOverride("separation", 12);
        margin.AddChild(stack);

        _textureInspector = CreateSection(stack, "Inspector");
        _textureNameLineEdit = new LineEdit { PlaceholderText = "Texture name" };
        _textureNameLineEdit.TextSubmitted += value => ApplyTextureChange(texture => texture.Name = CleanName(value, texture.Name));
        _textureNameLineEdit.FocusExited += () => ApplyTextureChange(texture => texture.Name = CleanName(_textureNameLineEdit.Text, texture.Name));
        _textureInspector.AddChild(_textureNameLineEdit);

        _textureOriginOptionButton = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _textureOriginOptionButton.AddItem("Bottom Left", (int)PolyTextureOriginMode.BottomLeft);
        _textureOriginOptionButton.AddItem("Bottom Center", (int)PolyTextureOriginMode.BottomCenter);
        _textureOriginOptionButton.AddItem("Center", (int)PolyTextureOriginMode.Center);
        AddField(_textureInspector, "Origin", _textureOriginOptionButton);
        _textureOriginOptionButton.ItemSelected += index => ApplyTextureChange(texture => texture.OriginMode = OptionIndexToOriginMode((int)index));

        _domainWidthSpinBox = CreateSpinBox(0.01, 100000, 0.01);
        AddField(_textureInspector, "Domain Width (cm)", _domainWidthSpinBox);
        _domainWidthSpinBox.ValueChanged += value => ResizeActiveDomain((float)value, _document.ActiveTexture.DomainHeightCm);

        _domainHeightSpinBox = CreateSpinBox(0.01, 100000, 0.01);
        AddField(_textureInspector, "Domain Height (cm)", _domainHeightSpinBox);
        _domainHeightSpinBox.ValueChanged += value => ResizeActiveDomain(_document.ActiveTexture.DomainWidthCm, (float)value);

        _previewWidthSpinBox = CreateSpinBox(1, 16384, 1);
        AddField(_textureInspector, "Preview Width (px)", _previewWidthSpinBox);
        _previewWidthSpinBox.ValueChanged += value => SetActivePreviewResolution((int)value, _document.ActiveTexture.PreviewHeightPx);

        _previewHeightSpinBox = CreateSpinBox(1, 16384, 1);
        AddField(_textureInspector, "Preview Height (px)", _previewHeightSpinBox);
        _previewHeightSpinBox.ValueChanged += value => SetActivePreviewResolution(_document.ActiveTexture.PreviewWidthPx, (int)value);

        _textureVisibleCheckBox = new CheckBox { Text = "Visible" };
        _textureVisibleCheckBox.Toggled += value => ApplyTextureChange(texture => texture.Visible = value);
        _textureInspector.AddChild(_textureVisibleCheckBox);

        _elementInspector = CreateSection(stack, "Element");
        _elementNameLineEdit = new LineEdit { PlaceholderText = "Element name" };
        _elementNameLineEdit.TextSubmitted += ApplyElementName;
        _elementNameLineEdit.FocusExited += () => ApplyElementName(_elementNameLineEdit.Text);
        _elementInspector.AddChild(_elementNameLineEdit);

        _elementEnabledCheckBox = new CheckBox { Text = "Enabled" };
        _elementEnabledCheckBox.Toggled += value => ApplyElementChange(element => element.Enabled = value);
        _elementInspector.AddChild(_elementEnabledCheckBox);

        _symmetryCheckBox = new CheckBox { Text = "Symmetry" };
        _symmetryCheckBox.Toggled += value => ApplyCenterStrokeChange(element => element.Symmetry = value);
        _elementInspector.AddChild(_symmetryCheckBox);

        _opacitySpinBox = CreateSpinBox(0, 1, 0.01);
        AddField(_elementInspector, "Opacity", _opacitySpinBox);
        _opacitySpinBox.ValueChanged += value => ApplyElementChange(element => element.Opacity = (float)value);

        _falloffSpinBox = CreateSpinBox(0, 8, 0.05);
        _falloffRow = AddField(_elementInspector, "Falloff", _falloffSpinBox);
        _falloffSpinBox.ValueChanged += value => ApplyCenterStrokeChange(element => element.Falloff = (float)value);

        _elementPositionXSpinBox = CreateSpinBox(-8192, 8192, 0.01);
        _elementPositionXRow = AddField(_elementInspector, "Position X (cm)", _elementPositionXSpinBox);
        _elementPositionXSpinBox.ValueChanged += value => ApplyElementDisplayPositionChange(new Vector2((float)value, (float)_elementPositionYSpinBox.Value));

        _elementPositionYSpinBox = CreateSpinBox(-8192, 8192, 0.01);
        _elementPositionYRow = AddField(_elementInspector, "Position Y (cm)", _elementPositionYSpinBox);
        _elementPositionYSpinBox.ValueChanged += value => ApplyElementDisplayPositionChange(new Vector2((float)_elementPositionXSpinBox.Value, (float)value));

        _elementRotationSpinBox = CreateSpinBox(-3600, 3600, 0.1);
        _elementRotationRow = AddField(_elementInspector, "Rotation (deg CCW)", _elementRotationSpinBox);
        _elementRotationSpinBox.ValueChanged += value => ApplyCenterStrokeChange(element => element.Transform.RotationDegrees = (float)value);

        _elementLengthScaleSpinBox = CreateSpinBox(0.01, 100, 0.01);
        _elementLengthScaleRow = AddField(_elementInspector, "Length Scale", _elementLengthScaleSpinBox);
        _elementLengthScaleSpinBox.ValueChanged += value => ApplyCenterStrokeChange(element => element.Transform.LengthScale = (float)value);

        _elementWidthScaleSpinBox = CreateSpinBox(0.01, 100, 0.01);
        _elementWidthScaleRow = AddField(_elementInspector, "Width Scale", _elementWidthScaleSpinBox);
        _elementWidthScaleSpinBox.ValueChanged += value => ApplyCenterStrokeChange(element => element.Transform.WidthScale = (float)value);

        _sweepInspector = CreateSection(stack, "Sweep");
        _sweepSourceOptionButton = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddField(_sweepInspector, "Source", _sweepSourceOptionButton);
        _sweepSourceOptionButton.ItemSelected += index => ApplySweepChange(sweep => SetSweepSource(sweep, _sweepSourceOptionButton.GetItemText((int)index)));

        _sweepTargetOptionButton = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddField(_sweepInspector, "Target", _sweepTargetOptionButton);
        _sweepTargetOptionButton.ItemSelected += index => ApplySweepChange(sweep => sweep.TargetElementId = _sweepTargetOptionButton.GetItemText((int)index));

        _sweepCountSpinBox = CreateSpinBox(1, 256, 1);
        AddField(_sweepInspector, "Count", _sweepCountSpinBox);
        _sweepCountSpinBox.ValueChanged += value => ApplySweepChange(sweep => sweep.Count = (int)value);

        _sweepStartTSpinBox = CreateSpinBox(0, 1, 0.01);
        AddField(_sweepInspector, "Start", _sweepStartTSpinBox);
        _sweepStartTSpinBox.ValueChanged += value => ApplySweepChange(sweep => sweep.StartT = Mathf.Min((float)value, sweep.EndT));

        _sweepEndTSpinBox = CreateSpinBox(0, 1, 0.01);
        AddField(_sweepInspector, "End", _sweepEndTSpinBox);
        _sweepEndTSpinBox.ValueChanged += value => ApplySweepChange(sweep => sweep.EndT = Mathf.Max((float)value, sweep.StartT));

        _sweepAlignmentOptionButton = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _sweepAlignmentOptionButton.AddItem("Tangent");
        _sweepAlignmentOptionButton.AddItem("Normal");
        AddField(_sweepInspector, "Alignment", _sweepAlignmentOptionButton);
        _sweepAlignmentOptionButton.ItemSelected += index => ApplySweepChange(sweep => sweep.Alignment = (PolyTextureSweepAlignment)(int)index);

        _sweepSideModeOptionButton = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _sweepSideModeOptionButton.AddItem("Left");
        _sweepSideModeOptionButton.AddItem("Right");
        _sweepSideModeOptionButton.AddItem("Both");
        AddField(_sweepInspector, "Side", _sweepSideModeOptionButton);
        _sweepSideModeOptionButton.ItemSelected += index => ApplySweepChange(sweep => sweep.SideMode = (PolyTextureSweepSideMode)(int)index);

        _sweepRotationOffsetSpinBox = CreateSpinBox(-3600, 3600, 0.1);
        AddField(_sweepInspector, "Rotation Offset", _sweepRotationOffsetSpinBox);
        _sweepRotationOffsetSpinBox.ValueChanged += value => ApplySweepChange(sweep => sweep.RotationOffsetDegrees = (float)value);

        _sweepLengthStartSpinBox = CreateSpinBox(0.01, 100, 0.01);
        AddField(_sweepInspector, "Length Start", _sweepLengthStartSpinBox);
        _sweepLengthStartSpinBox.ValueChanged += value => ApplySweepChange(sweep => sweep.LengthScaleStart = (float)value);

        _sweepLengthEndSpinBox = CreateSpinBox(0.01, 100, 0.01);
        AddField(_sweepInspector, "Length End", _sweepLengthEndSpinBox);
        _sweepLengthEndSpinBox.ValueChanged += value => ApplySweepChange(sweep => sweep.LengthScaleEnd = (float)value);

        _sweepWidthStartSpinBox = CreateSpinBox(0.01, 100, 0.01);
        AddField(_sweepInspector, "Width Start", _sweepWidthStartSpinBox);
        _sweepWidthStartSpinBox.ValueChanged += value => ApplySweepChange(sweep => sweep.WidthScaleStart = (float)value);

        _sweepWidthEndSpinBox = CreateSpinBox(0.01, 100, 0.01);
        AddField(_sweepInspector, "Width End", _sweepWidthEndSpinBox);
        _sweepWidthEndSpinBox.ValueChanged += value => ApplySweepChange(sweep => sweep.WidthScaleEnd = (float)value);

        _sweepRenderSourceCheckBox = new CheckBox { Text = "Render Source" };
        _sweepRenderSourceCheckBox.Toggled += value => ApplySweepChange(sweep => sweep.RenderSource = value);
        _sweepInspector.AddChild(_sweepRenderSourceCheckBox);

        _pointInspector = CreateSection(stack, "Point");
        _pointXSpinBox = CreateSpinBox(-4096, 4096, 0.01);
        AddField(_pointInspector, "Local X (cm)", _pointXSpinBox);
        _pointXSpinBox.ValueChanged += value => ApplyPointChange(point => point.X = (float)value);

        _pointYSpinBox = CreateSpinBox(-4096, 4096, 0.01);
        AddField(_pointInspector, "Local Y (cm)", _pointYSpinBox);
        _pointYSpinBox.ValueChanged += value => ApplyPointChange(point => point.Y = (float)value);

        _leftWidthSpinBox = CreateSpinBox(0, 1024, 0.01);
        _leftWidthRow = AddField(_pointInspector, "Left Width (cm)", _leftWidthSpinBox);
        _leftWidthSpinBox.ValueChanged += value => ApplySelectedPointWidthChange(point =>
        {
            point.LeftWidth = (float)value;
            if (_document.ActiveCenterStroke?.Symmetry == true)
            {
                point.RightWidth = (float)value;
            }
        });

        _rightWidthSpinBox = CreateSpinBox(0, 1024, 0.01);
        _rightWidthRow = AddField(_pointInspector, "Right Width (cm)", _rightWidthSpinBox);
        _rightWidthSpinBox.ValueChanged += value => ApplySelectedPointWidthChange(point =>
        {
            point.RightWidth = (float)value;
            if (_document.ActiveCenterStroke?.Symmetry == true)
            {
                point.LeftWidth = (float)value;
            }
        });

        _handleModeOptionButton = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _handleModeOptionButton.AddItem("Free");
        _handleModeOptionButton.AddItem("Aligned");
        _handleModeOptionButton.AddItem("Mirrored");
        _handleModeRow = AddField(_pointInspector, "Handle Mode", _handleModeOptionButton);
        _handleModeOptionButton.ItemSelected += index => SetActiveHandleMode(OptionIndexToHandleMode((int)index));

        _inHandleXSpinBox = CreateSpinBox(-4096, 4096, 0.01);
        _inHandleXRow = AddField(_pointInspector, "In Handle X (cm)", _inHandleXSpinBox);
        _inHandleXSpinBox.ValueChanged += value => ApplyCenterStrokePointChange(point => point.InHandle = new Vector2((float)value, point.InHandle.Y));

        _inHandleYSpinBox = CreateSpinBox(-4096, 4096, 0.01);
        _inHandleYRow = AddField(_pointInspector, "In Handle Y (cm)", _inHandleYSpinBox);
        _inHandleYSpinBox.ValueChanged += value => ApplyCenterStrokePointChange(point => point.InHandle = new Vector2(point.InHandle.X, (float)value));

        _outHandleXSpinBox = CreateSpinBox(-4096, 4096, 0.01);
        _outHandleXRow = AddField(_pointInspector, "Out Handle X (cm)", _outHandleXSpinBox);
        _outHandleXSpinBox.ValueChanged += value => ApplyCenterStrokePointChange(point => point.OutHandle = new Vector2((float)value, point.OutHandle.Y));

        _outHandleYSpinBox = CreateSpinBox(-4096, 4096, 0.01);
        _outHandleYRow = AddField(_pointInspector, "Out Handle Y (cm)", _outHandleYSpinBox);
        _outHandleYSpinBox.ValueChanged += value => ApplyCenterStrokePointChange(point => point.OutHandle = new Vector2(point.OutHandle.X, (float)value));

        _outputInspector = CreateSection(stack, "Output");
        _outputNameLineEdit = new LineEdit { PlaceholderText = "Output name" };
        _outputNameLineEdit.TextSubmitted += ApplyOutputName;
        _outputNameLineEdit.FocusExited += () => ApplyOutputName(_outputNameLineEdit.Text);
        _outputInspector.AddChild(_outputNameLineEdit);

        _outputEnabledCheckBox = new CheckBox { Text = "Enabled" };
        _outputEnabledCheckBox.Toggled += value => ApplyOutputChange(output => output.Enabled = value);
        _outputInspector.AddChild(_outputEnabledCheckBox);

        _outputKindLabel = new Label();
        AddField(_outputInspector, "Kind", _outputKindLabel);
        _outputSourcesLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddField(_outputInspector, "Sources", _outputSourcesLabel);

        _outputValueSpinBox = CreateSpinBox(0, 1, 0.01);
        AddField(_outputInspector, "Value", _outputValueSpinBox);
        _outputValueSpinBox.ValueChanged += value => ApplyOutputChange(output => output.Value = (float)value);

        _outputHeightAmplitudeSpinBox = CreateSpinBox(0.01, 1000, 0.01);
        _outputHeightAmplitudeRow = AddField(_outputInspector, "Amplitude (cm)", _outputHeightAmplitudeSpinBox);
        _outputHeightAmplitudeSpinBox.ValueChanged += value => ApplyOutputChange(output => output.HeightAmplitudeCm = (float)value);

        _regionInspector = CreateSection(stack, "Rectangle Region");
        _regionPositionXSpinBox = CreateSpinBox(-100000, 100000, 0.01);
        AddField(_regionInspector, "Position X (cm)", _regionPositionXSpinBox);
        _regionPositionXSpinBox.ValueChanged += value => ApplyRegionDisplayPosition(new Vector2((float)value, (float)_regionPositionYSpinBox.Value));
        _regionPositionYSpinBox = CreateSpinBox(-100000, 100000, 0.01);
        AddField(_regionInspector, "Position Y (cm)", _regionPositionYSpinBox);
        _regionPositionYSpinBox.ValueChanged += value => ApplyRegionDisplayPosition(new Vector2((float)_regionPositionXSpinBox.Value, (float)value));
        _regionRotationSpinBox = CreateSpinBox(-3600, 3600, 0.1);
        AddField(_regionInspector, "Rotation (deg CCW)", _regionRotationSpinBox);
        _regionRotationSpinBox.ValueChanged += value => ApplyRegionChange(region => region.RotationDegrees = (float)value);
        _regionWidthSpinBox = CreateSpinBox(0.01, 100000, 0.01);
        AddField(_regionInspector, "Width (cm)", _regionWidthSpinBox);
        _regionWidthSpinBox.ValueChanged += value => ApplyRegionChange(region =>
        {
            region.WidthCm = (float)value;
            region.CornerRadiusCm = Mathf.Min(region.CornerRadiusCm, Mathf.Min(region.WidthCm, region.HeightCm) * 0.5f);
        });
        _regionHeightSpinBox = CreateSpinBox(0.01, 100000, 0.01);
        AddField(_regionInspector, "Height (cm)", _regionHeightSpinBox);
        _regionHeightSpinBox.ValueChanged += value => ApplyRegionChange(region =>
        {
            region.HeightCm = (float)value;
            region.CornerRadiusCm = Mathf.Min(region.CornerRadiusCm, Mathf.Min(region.WidthCm, region.HeightCm) * 0.5f);
        });
        _regionCornerRadiusSpinBox = CreateSpinBox(0, 50000, 0.01);
        AddField(_regionInspector, "Corner Radius (cm)", _regionCornerRadiusSpinBox);
        _regionCornerRadiusSpinBox.ValueChanged += value => ApplyRegionChange(region => region.CornerRadiusCm = Mathf.Min((float)value, Mathf.Min(region.WidthCm, region.HeightCm) * 0.5f));

        _repeatInspector = CreateSection(stack, "Repeat Grid");
        _repeatSourceLabel = new Label();
        AddField(_repeatInspector, "Source", _repeatSourceLabel);
        _repeatColumnsSpinBox = CreateSpinBox(1, 256, 1);
        AddField(_repeatInspector, "Columns", _repeatColumnsSpinBox);
        _repeatColumnsSpinBox.ValueChanged += value => ApplyRepeatChange(repeat => repeat.Columns = ClampRepeatCount((int)value, repeat.Rows));
        _repeatRowsSpinBox = CreateSpinBox(1, 256, 1);
        AddField(_repeatInspector, "Rows", _repeatRowsSpinBox);
        _repeatRowsSpinBox.ValueChanged += value => ApplyRepeatChange(repeat => repeat.Rows = ClampRepeatCount((int)value, repeat.Columns));
        _repeatStepXSpinBox = CreateSpinBox(0.01, 100000, 0.01);
        AddField(_repeatInspector, "Step X (cm)", _repeatStepXSpinBox);
        _repeatStepXSpinBox.ValueChanged += value => ApplyRepeatChange(repeat => repeat.StepXCm = (float)value);
        _repeatStepYSpinBox = CreateSpinBox(0.01, 100000, 0.01);
        AddField(_repeatInspector, "Step Y (cm)", _repeatStepYSpinBox);
        _repeatStepYSpinBox.ValueChanged += value => ApplyRepeatChange(repeat => repeat.StepYCm = (float)value);
        _repeatRowOffsetSpinBox = CreateSpinBox(-100000, 100000, 0.01);
        AddField(_repeatInspector, "Alternate Row Offset X (cm)", _repeatRowOffsetSpinBox);
        _repeatRowOffsetSpinBox.ValueChanged += value => ApplyRepeatChange(repeat => repeat.AlternateRowOffsetXCm = (float)value);

        _branchInspector = CreateSection(stack, "Branch");
        _branchSourceLabel = new Label();
        AddField(_branchInspector, "Source", _branchSourceLabel);
        _branchSeedSpinBox = CreateSpinBox(0, int.MaxValue, 1);
        AddField(_branchInspector, "Seed", _branchSeedSpinBox);
        _branchSeedSpinBox.ValueChanged += value => ApplyBranchChange(branch => branch.Seed = (int)value);
        _branchCountSpinBox = CreateSpinBox(1, 4096, 1);
        AddField(_branchInspector, "Count", _branchCountSpinBox);
        _branchCountSpinBox.ValueChanged += value => ApplyBranchChange(branch => branch.Count = (int)value);
        _branchSegmentsSpinBox = CreateSpinBox(1, 32, 1);
        AddField(_branchInspector, "Segments", _branchSegmentsSpinBox);
        _branchSegmentsSpinBox.ValueChanged += value => ApplyBranchChange(branch => branch.Segments = (int)value);
        _branchStartTSpinBox = CreateSpinBox(0, 1, 0.01);
        AddField(_branchInspector, "Start", _branchStartTSpinBox);
        _branchStartTSpinBox.ValueChanged += value => ApplyBranchChange(branch => branch.StartT = Mathf.Min((float)value, branch.EndT));
        _branchEndTSpinBox = CreateSpinBox(0, 1, 0.01);
        AddField(_branchInspector, "End", _branchEndTSpinBox);
        _branchEndTSpinBox.ValueChanged += value => ApplyBranchChange(branch => branch.EndT = Mathf.Max((float)value, branch.StartT));
        _branchLengthMinSpinBox = CreateSpinBox(0.01, 100000, 0.01);
        AddField(_branchInspector, "Length Min (cm)", _branchLengthMinSpinBox);
        _branchLengthMinSpinBox.ValueChanged += value => ApplyBranchChange(branch => branch.LengthMinCm = Mathf.Min((float)value, branch.LengthMaxCm));
        _branchLengthMaxSpinBox = CreateSpinBox(0.01, 100000, 0.01);
        AddField(_branchInspector, "Length Max (cm)", _branchLengthMaxSpinBox);
        _branchLengthMaxSpinBox.ValueChanged += value => ApplyBranchChange(branch => branch.LengthMaxCm = Mathf.Max((float)value, branch.LengthMinCm));
        _branchAngleMinSpinBox = CreateSpinBox(0, 180, 0.1);
        AddField(_branchInspector, "Angle Min (deg)", _branchAngleMinSpinBox);
        _branchAngleMinSpinBox.ValueChanged += value => ApplyBranchChange(branch => branch.AngleMinDegrees = Mathf.Min((float)value, branch.AngleMaxDegrees));
        _branchAngleMaxSpinBox = CreateSpinBox(0, 180, 0.1);
        AddField(_branchInspector, "Angle Max (deg)", _branchAngleMaxSpinBox);
        _branchAngleMaxSpinBox.ValueChanged += value => ApplyBranchChange(branch => branch.AngleMaxDegrees = Mathf.Max((float)value, branch.AngleMinDegrees));
        _branchWidthScaleSpinBox = CreateSpinBox(0.01, 100, 0.01);
        AddField(_branchInspector, "Width Scale", _branchWidthScaleSpinBox);
        _branchWidthScaleSpinBox.ValueChanged += value => ApplyBranchChange(branch => branch.WidthScale = (float)value);
        _branchIrregularitySpinBox = CreateSpinBox(0, 1, 0.01);
        AddField(_branchInspector, "Irregularity", _branchIrregularitySpinBox);
        _branchIrregularitySpinBox.ValueChanged += value => ApplyBranchChange(branch => branch.Irregularity = (float)value);
        _branchRenderSourceCheckBox = new CheckBox { Text = "Render Source" };
        _branchRenderSourceCheckBox.Toggled += value => ApplyBranchChange(branch => branch.RenderSource = value);
        _branchInspector.AddChild(_branchRenderSourceCheckBox);

        _filterInspector = CreateSection(stack, "Invert Filter");
        _filterSourceLabel = new Label();
        AddField(_filterInspector, "Source", _filterSourceLabel);

        _edgeFalloffInspector = CreateSection(stack, "Edge Falloff Filter");
        _edgeFalloffSourceLabel = new Label();
        AddField(_edgeFalloffInspector, "Source", _edgeFalloffSourceLabel);
        _edgeFalloffRadiusSpinBox = CreateSpinBox(0, 100000, 0.01);
        AddField(_edgeFalloffInspector, "Radius (cm)", _edgeFalloffRadiusSpinBox);
        _edgeFalloffRadiusSpinBox.ValueChanged += value => ApplyEdgeFalloffChange(falloff => falloff.RadiusCm = (float)value);
        _edgeFalloffExponentSpinBox = CreateSpinBox(0.01, 16, 0.01);
        AddField(_edgeFalloffInspector, "Exponent", _edgeFalloffExponentSpinBox);
        _edgeFalloffExponentSpinBox.ValueChanged += value => ApplyEdgeFalloffChange(falloff => falloff.Exponent = (float)value);
    }

    private static VBoxContainer CreateSection(VBoxContainer stack, string title)
    {
        VBoxContainer section = new();
        section.AddThemeConstantOverride("separation", 8);
        section.AddChild(new Label { Text = title });
        stack.AddChild(section);
        return section;
    }

    private static SpinBox CreateSpinBox(double minValue, double maxValue, double step)
    {
        return new SpinBox
        {
            MinValue = minValue,
            MaxValue = maxValue,
            Step = step,
            Rounded = step >= 1.0,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
    }

    private static HBoxContainer AddField(VBoxContainer stack, string label, SpinBox spinBox)
    {
        return AddField(stack, label, (Control)spinBox);
    }

    private static HBoxContainer AddField(VBoxContainer stack, string label, Control control)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label
        {
            Text = label,
            CustomMinimumSize = new Vector2(100, 0)
        });
        row.AddChild(control);
        stack.AddChild(row);
        return row;
    }

    private void ApplyTextureChange(Action<PolyTextureItem> apply)
    {
        if (_refreshing || _document?.ActiveTexture == null)
        {
            return;
        }

        apply(_document.ActiveTexture);
        DocumentChanged?.Invoke();
        Refresh();
    }

    public void ResizeActiveDomain(float widthCm, float heightCm)
    {
        if (_refreshing || _document?.ActiveTexture == null)
        {
            return;
        }

        PolyTextureItem texture = _document.ActiveTexture;
        if (!PolyTextureSurfaceService.SetDomainSize(texture, widthCm, heightCm))
        {
            return;
        }

        DocumentChanged?.Invoke();
        Refresh();
    }

    public void SetActivePreviewResolution(int widthPx, int heightPx)
    {
        if (_refreshing || _document?.ActiveTexture == null)
        {
            return;
        }

        PolyTextureItem texture = _document.ActiveTexture;
        if (!PolyTextureSurfaceService.SetPreviewResolution(texture, widthPx, heightPx))
        {
            return;
        }

        DocumentChanged?.Invoke();
        Refresh();
    }

    private void ApplyElementChange(Action<PolyTextureElement> apply)
    {
        if (_refreshing || _document?.ActiveElement == null)
        {
            return;
        }

        apply(_document.ActiveElement);
        DocumentChanged?.Invoke();
        Refresh();
    }

    private void ApplyOutputChange(Action<PolyTextureOutputBinding> apply)
    {
        if (_refreshing || _document?.ActiveOutput == null)
        {
            return;
        }

        apply(_document.ActiveOutput);
        DocumentChanged?.Invoke();
        Refresh();
    }

    private void ApplyOutputName(string value)
    {
        ApplyOutputChange(output => output.Name = CleanName(value, output.Name));
    }

    private void ApplyRegionChange(Action<RectangleRegionElement> apply)
    {
        if (_refreshing || _document?.ActiveElement is not RectangleRegionElement region)
        {
            return;
        }
        apply(region);
        DocumentChanged?.Invoke();
        Refresh();
    }

    private void ApplyRegionDisplayPosition(Vector2 displayPosition)
    {
        ApplyRegionChange(region => region.Position = ToStoredPosition(displayPosition));
    }

    private void ApplyRepeatChange(Action<RepeatGridGeneratorElement> apply)
    {
        if (_refreshing || _document?.ActiveElement is not RepeatGridGeneratorElement repeat)
        {
            return;
        }
        apply(repeat);
        DocumentChanged?.Invoke();
        Refresh();
    }

    private void ApplyBranchChange(Action<BranchGeneratorElement> apply)
    {
        if (_refreshing || _document?.ActiveElement is not BranchGeneratorElement branch)
        {
            return;
        }
        apply(branch);
        DocumentChanged?.Invoke();
        Refresh();
    }

    private void ApplyEdgeFalloffChange(Action<EdgeFalloffFilterElement> apply)
    {
        if (_refreshing || _document?.ActiveElement is not EdgeFalloffFilterElement falloff)
        {
            return;
        }
        apply(falloff);
        DocumentChanged?.Invoke();
        Refresh();
    }

    private static int ClampRepeatCount(int requested, int otherAxisCount)
    {
        return Mathf.Clamp(requested, 1, Mathf.Max(1, 16384 / Mathf.Max(1, otherAxisCount)));
    }

    private void ApplyElementName(string value)
    {
        if (_refreshing || _document?.ActiveTexture == null || _document.ActiveElement == null)
        {
            return;
        }

        PolyTextureElement element = _document.ActiveElement;
        string nextName = CleanName(value, element.Name);
        if (nextName.Equals(element.Name, StringComparison.Ordinal))
        {
            Refresh();
            return;
        }

        element.Name = nextName;
        DocumentChanged?.Invoke();
        Refresh();
    }

    private void ApplyCenterStrokeChange(Action<CenterStrokeElement> apply)
    {
        if (_refreshing || _document?.ActiveCenterStroke == null)
        {
            return;
        }

        apply(_document.ActiveCenterStroke);
        DocumentChanged?.Invoke();
        Refresh();
    }

    private void ApplySweepChange(Action<SweepGeneratorElement> apply)
    {
        if (_refreshing || _document?.ActiveElement is not SweepGeneratorElement sweep)
        {
            return;
        }

        apply(sweep);
        DocumentChanged?.Invoke();
        Refresh();
    }

    private void RefreshSweepElementOptions(SweepGeneratorElement sweep)
    {
        _sweepSourceOptionButton.Clear();
        _sweepTargetOptionButton.Clear();
        PolyTextureItem texture = _document.ActiveTexture;
        if (texture == null)
        {
            return;
        }

        int sourceIndex = -1;
        int targetIndex = -1;
        foreach (PolyTextureElement candidate in texture.Elements)
        {
            if (candidate is not CenterStrokeElement)
            {
                continue;
            }

            _sweepSourceOptionButton.AddItem(candidate.Id);
            if (candidate.Id.Equals(sweep.SourceElementId, StringComparison.Ordinal))
            {
                sourceIndex = _sweepSourceOptionButton.ItemCount - 1;
            }

            if (!candidate.Id.Equals(sweep.SourceElementId, StringComparison.Ordinal))
            {
                _sweepTargetOptionButton.AddItem(candidate.Id);
                if (candidate.Id.Equals(sweep.TargetElementId, StringComparison.Ordinal))
                {
                    targetIndex = _sweepTargetOptionButton.ItemCount - 1;
                }
            }
        }

        if (sourceIndex >= 0)
        {
            _sweepSourceOptionButton.Selected = sourceIndex;
        }
        if (targetIndex >= 0)
        {
            _sweepTargetOptionButton.Selected = targetIndex;
        }
    }

    private void SetSweepSource(SweepGeneratorElement sweep, string sourceElementId)
    {
        sweep.SourceElementId = sourceElementId;
        if (!sweep.TargetElementId.Equals(sourceElementId, StringComparison.Ordinal))
        {
            return;
        }

        foreach (PolyTextureElement candidate in _document.ActiveTexture.Elements)
        {
            if (candidate is CenterStrokeElement && !candidate.Id.Equals(sourceElementId, StringComparison.Ordinal))
            {
                sweep.TargetElementId = candidate.Id;
                return;
            }
        }
    }

    private void ApplyPointChange(Action<PolyTexturePoint> apply)
    {
        if (_refreshing || _document?.ActivePoint == null)
        {
            return;
        }

        apply(_document.ActivePoint);
        _document.ActiveCenterStroke?.NormalizeLocalAxes();
        DocumentChanged?.Invoke();
        Refresh();
    }

    private void ApplyCenterStrokePointChange(Action<CenterStrokePoint> apply)
    {
        if (_refreshing || _document?.ActivePoint is not CenterStrokePoint point)
        {
            return;
        }

        apply(point);
        if (_document.SelectedPointIndex == 0)
        {
            _document.ActiveCenterStroke?.NormalizeAnchor();
        }
        DocumentChanged?.Invoke();
        Refresh();
    }

    private void ApplySelectedPointWidthChange(Action<CenterStrokePoint> apply)
    {
        CenterStrokeElement element = _document?.ActiveCenterStroke;
        if (_refreshing || element == null || _document.SelectedPointIndices.Count == 0)
        {
            return;
        }
        foreach (int pointIndex in _document.SelectedPointIndices)
        {
            if (pointIndex >= 0 && pointIndex < element.Points.Count)
            {
                apply(element.Points[pointIndex]);
            }
        }
        DocumentChanged?.Invoke();
        Refresh();
    }

    public void SetActiveHandleMode(PolyTextureHandleMode mode)
    {
        if (_refreshing || _document?.ActivePoint is not CenterStrokePoint point)
        {
            return;
        }

        point.HandleMode = mode;
        if (point.InHandle.LengthSquared() < 0.0001f && point.OutHandle.LengthSquared() < 0.0001f)
        {
            Vector2 tangent = PolyTextureRenderer.GetTangent(_document.ActiveCenterStroke, _document.SelectedPointIndex);
            float handleLength = GetAdjacentPointDistance(_document.ActiveCenterStroke, _document.SelectedPointIndex) / 3.0f;
            point.InHandle = -tangent * handleLength;
            point.OutHandle = tangent * handleLength;
        }

        if (mode == PolyTextureHandleMode.Mirrored)
        {
            point.OutHandle = -point.InHandle;
        }
        else if (mode == PolyTextureHandleMode.Aligned && point.InHandle.LengthSquared() > 0.0001f)
        {
            point.OutHandle = point.OutHandle.LengthSquared() > 0.0001f
                ? -point.InHandle.Normalized() * point.OutHandle.Length()
                : -point.InHandle;
        }

        DocumentChanged?.Invoke();
        Refresh();
    }

    private static float GetAdjacentPointDistance(CenterStrokeElement element, int pointIndex)
    {
        if (element == null || element.Points.Count < 2)
        {
            return 48.0f;
        }

        int previousIndex = Mathf.Max(0, pointIndex - 1);
        int nextIndex = Mathf.Min(element.Points.Count - 1, pointIndex + 1);
        if (previousIndex == nextIndex)
        {
            return 48.0f;
        }

        return element.Points[previousIndex].Position.DistanceTo(element.Points[nextIndex].Position);
    }

    private void ApplyElementDisplayPositionChange(Vector2 displayPosition)
    {
        ApplyCenterStrokeChange(element => element.Transform.Position = ToStoredPosition(displayPosition));
    }

    private Vector2 ToDisplayPosition(Vector2 storedPosition)
    {
        PolyTextureItem texture = _document?.ActiveTexture;
        if (texture == null)
        {
            return storedPosition;
        }

        return texture.OriginMode switch
        {
            PolyTextureOriginMode.BottomCenter => storedPosition - new Vector2(texture.DomainWidthCm * 0.5f, 0.0f),
            PolyTextureOriginMode.Center => storedPosition - new Vector2(texture.DomainWidthCm * 0.5f, texture.DomainHeightCm * 0.5f),
            _ => storedPosition
        };
    }

    private Vector2 ToStoredPosition(Vector2 displayPosition)
    {
        PolyTextureItem texture = _document?.ActiveTexture;
        if (texture == null)
        {
            return displayPosition;
        }

        return texture.OriginMode switch
        {
            PolyTextureOriginMode.BottomCenter => displayPosition + new Vector2(texture.DomainWidthCm * 0.5f, 0.0f),
            PolyTextureOriginMode.Center => displayPosition + new Vector2(texture.DomainWidthCm * 0.5f, texture.DomainHeightCm * 0.5f),
            _ => displayPosition
        };
    }

    private static int OriginModeToOptionIndex(PolyTextureOriginMode originMode)
    {
        return originMode switch
        {
            PolyTextureOriginMode.BottomCenter => 1,
            PolyTextureOriginMode.Center => 2,
            _ => 0
        };
    }

    private static PolyTextureOriginMode OptionIndexToOriginMode(int index)
    {
        return index switch
        {
            1 => PolyTextureOriginMode.BottomCenter,
            2 => PolyTextureOriginMode.Center,
            _ => PolyTextureOriginMode.BottomLeft
        };
    }

    private static int HandleModeToOptionIndex(PolyTextureHandleMode mode)
    {
        return mode switch
        {
            PolyTextureHandleMode.Free => 0,
            PolyTextureHandleMode.Mirrored => 2,
            _ => 1
        };
    }

    private static PolyTextureHandleMode OptionIndexToHandleMode(int index)
    {
        return index switch
        {
            0 => PolyTextureHandleMode.Free,
            2 => PolyTextureHandleMode.Mirrored,
            _ => PolyTextureHandleMode.Aligned
        };
    }

    private static string CleanName(string value, string fallback)
    {
        string cleanValue = value?.Trim() ?? string.Empty;
        return string.IsNullOrEmpty(cleanValue) ? fallback : cleanValue;
    }
}
