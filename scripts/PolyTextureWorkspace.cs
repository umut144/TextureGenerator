using Godot;
using System;
using System.Collections.Generic;

public partial class PolyTextureWorkspace : Control
{
    private enum SweepCreationState
    {
        None,
        AwaitSource,
        AwaitTarget,
        Preview
    }

    private enum GuideDrawingState
    {
        None,
        Point,
        AxisStart,
        AxisEnd
    }

    private enum MirrorCreationState
    {
        None,
        AwaitSource,
        AwaitAxis,
        Preview
    }

    private static string DefaultSaveDirectory => ProjectSettings.GlobalizePath("res://savefiles");
    private static string DefaultDocumentPath => DefaultSaveDirectory.PathJoin("untitled.polytexture.json");
    private static readonly float[] SnapStepPresets = { 0.78125f, 1.5625f, 3.125f, 6.25f, 12.5f, 25.0f, 50.0f, 100.0f };
    private const int DrawCenterStrokeMenuId = 1;
    private const int DrawCenterPathMenuId = 2;
    private const int FinishDrawingMenuId = 3;
    private const int GenerateSweepMenuId = 1;
    private const int OperatorMirrorMenuId = 1;
    private const int GuidePointMenuId = 1;
    private const int GuideAxisMenuId = 2;
    private const int RectangleRegionMenuId = 3;

    private PolyTextureDocument _document;
    private string _documentPath = string.Empty;
    private PolyTextureCanvasView _canvasView;
    private PolyTextureInspector _inspector;
    private Tree _outlinerTree;
    private FileDialog _loadDialog;
    private FileDialog _saveAsDialog;
    private FileDialog _outputExportDialog;
    private LineEdit _newTextureNameLineEdit;
    private LineEdit _documentNameLineEdit;
    private Label _activeDocumentLabel;
    private Label _statusLabel;
    private Label _mousePositionLabel;
    private Button _undoButton;
    private Button _redoButton;
    private MenuButton _drawMenuButton;
    private MenuButton _generateMenuButton;
    private MenuButton _operatorMenuButton;
    private MenuButton _filterMenuButton;
    private MenuButton _guideMenuButton;
    private Button _pointPlacementButton;
    private HBoxContainer _elementActionBar;
    private CheckBox _snapCheckBox;
    private OptionButton _snapStepOptionButton;
    private readonly PolyTextureHistory _history = new();
    private bool _hasUnsavedChanges;
    private bool _refreshingOutliner;
    private bool _refreshingHeader;
    private bool _refreshingGlobalSettings;
    private bool _deferHistory;
    private bool _drawingCenterStroke;
    private bool _drawingCenterPath;
    private bool _refreshingElementActionBar;
    private bool _pointPlacementMode;
    private PolyTextureHandleView _handleView = PolyTextureHandleView.Width;
    private SweepCreationState _sweepCreationState;
    private SweepGeneratorElement _sweepDraft;
    private GuideDrawingState _guideDrawingState;
    private PolyTextureGuide _guideAxisDraft;
    private MirrorCreationState _mirrorCreationState;
    private MirrorGeneratorElement _mirrorDraft;
    private bool _exportNormalMap;

    public override void _Ready()
    {
        BuildUi();
        SetDocument(new PolyTextureDocument(), "New document.");
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey keyEvent
            || !keyEvent.Pressed
            || keyEvent.Echo)
        {
            return;
        }

        if (_sweepCreationState != SweepCreationState.None && keyEvent.Keycode == Key.Escape)
        {
            StepBackSweepCreation();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_sweepCreationState == SweepCreationState.Preview && keyEvent.Keycode is Key.Enter or Key.KpEnter)
        {
            CommitSweepDraft();
            GetViewport().SetInputAsHandled();
        }

        if (_mirrorCreationState != MirrorCreationState.None && keyEvent.Keycode == Key.Escape)
        {
            StepBackMirrorCreation();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_mirrorCreationState == MirrorCreationState.Preview && keyEvent.Keycode is Key.Enter or Key.KpEnter)
        {
            CommitMirrorDraft();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_guideDrawingState != GuideDrawingState.None && keyEvent.Keycode == Key.Escape)
        {
            CancelGuideDrawing();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledKeyInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo || IsTextInputFocused())
        {
            return;
        }

        if (keyEvent.Keycode == Key.Delete || keyEvent.Keycode == Key.Backspace)
        {
            DeleteActiveSelection();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_pointPlacementMode && keyEvent.Keycode == Key.Escape)
        {
            CancelPointPlacement();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_drawingCenterStroke && (keyEvent.Keycode == Key.Enter || keyEvent.Keycode == Key.Escape))
        {
            FinishCenterStrokeDrawing();
            GetViewport().SetInputAsHandled();
            return;
        }

        if ((keyEvent.CtrlPressed || keyEvent.MetaPressed) && keyEvent.Keycode == Key.Z)
        {
            if (keyEvent.ShiftPressed)
            {
                RedoDocument();
            }
            else
            {
                UndoDocument();
            }

            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildUi()
    {
        PanelContainer toolFrame = new()
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f
        };
        AddChild(toolFrame);

        MarginContainer rootMargin = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        rootMargin.AddThemeConstantOverride("margin_left", 12);
        rootMargin.AddThemeConstantOverride("margin_top", 12);
        rootMargin.AddThemeConstantOverride("margin_right", 12);
        rootMargin.AddThemeConstantOverride("margin_bottom", 12);
        toolFrame.AddChild(rootMargin);

        VBoxContainer mainLayout = new();
        mainLayout.AddThemeConstantOverride("separation", 8);
        rootMargin.AddChild(mainLayout);

        BuildDocumentBar(mainLayout);
        BuildGlobalSettingsBar(mainLayout);
        BuildActionBar(mainLayout);
        BuildWorkspaceSplit(mainLayout);
        BuildStatusBar(mainLayout);
        PolyTextureUiDefaults.Apply(this);
    }

    private void BuildGlobalSettingsBar(VBoxContainer mainLayout)
    {
        HBoxContainer settingsBar = new();
        settingsBar.AddThemeConstantOverride("separation", 8);
        mainLayout.AddChild(settingsBar);

        settingsBar.AddChild(new Label { Text = "Global" });

        _snapCheckBox = new CheckBox { Text = "Snap" };
        _snapCheckBox.Toggled += value => ApplyGlobalSettings(document => document.SnapEnabled = value);
        settingsBar.AddChild(_snapCheckBox);

        _snapStepOptionButton = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            CustomMinimumSize = new Vector2(72, 0)
        };
        foreach (float snapStep in SnapStepPresets)
        {
            _snapStepOptionButton.AddItem($"{snapStep:0.####} cm");
        }
        _snapStepOptionButton.ItemSelected += index => ApplyGlobalSettings(document => document.SnapStepCm = SnapStepPresets[Mathf.Clamp((int)index, 0, SnapStepPresets.Length - 1)]);
        settingsBar.AddChild(new Label { Text = "Snap Step" });
        settingsBar.AddChild(_snapStepOptionButton);
        settingsBar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
    }

    private void BuildDocumentBar(VBoxContainer mainLayout)
    {
        HBoxContainer documentBar = new();
        documentBar.AddThemeConstantOverride("separation", 8);
        mainLayout.AddChild(documentBar);

        _documentNameLineEdit = new LineEdit
        {
            PlaceholderText = "Document name",
            CustomMinimumSize = new Vector2(220, 0)
        };
        _documentNameLineEdit.TextSubmitted += ApplyDocumentName;
        _documentNameLineEdit.FocusExited += () => ApplyDocumentName(_documentNameLineEdit.Text);
        documentBar.AddChild(_documentNameLineEdit);

        Button newButton = new() { Text = "New" };
        newButton.Pressed += CreateNewDocument;
        documentBar.AddChild(newButton);

        _newTextureNameLineEdit = new LineEdit
        {
            PlaceholderText = "Texture name",
            CustomMinimumSize = new Vector2(220, 0)
        };
        _newTextureNameLineEdit.TextSubmitted += _ => AddTexture();
        documentBar.AddChild(_newTextureNameLineEdit);

        Button addTextureButton = new() { Text = "Add Texture" };
        addTextureButton.Pressed += AddTexture;
        documentBar.AddChild(addTextureButton);

        Button deleteTextureButton = new() { Text = "Delete Texture" };
        deleteTextureButton.Pressed += DeleteActiveTexture;
        documentBar.AddChild(deleteTextureButton);

        documentBar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        _undoButton = new Button { Text = "Undo" };
        _undoButton.Pressed += UndoDocument;
        documentBar.AddChild(_undoButton);

        _redoButton = new Button { Text = "Redo" };
        _redoButton.Pressed += RedoDocument;
        documentBar.AddChild(_redoButton);

        Button loadButton = new() { Text = "Load JSON..." };
        loadButton.Pressed += OpenLoadDialog;
        documentBar.AddChild(loadButton);

        Button saveButton = new() { Text = "Save" };
        saveButton.Pressed += SaveDocument;
        documentBar.AddChild(saveButton);

        Button saveAsButton = new() { Text = "Save As..." };
        saveAsButton.Pressed += OpenSaveAsDialog;
        documentBar.AddChild(saveAsButton);

        BuildFileDialogs();
    }

    private void BuildFileDialogs()
    {
        DirAccess.MakeDirRecursiveAbsolute(DefaultSaveDirectory);
        _loadDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Title = "Load PolyTexture JSON"
        };
        _loadDialog.AddFilter("*.polytexture.json ; PolyTexture JSON");
        _loadDialog.FileSelected += LoadDocumentFromPath;
        AddChild(_loadDialog);

        _saveAsDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.SaveFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Title = "Save PolyTexture JSON"
        };
        _saveAsDialog.AddFilter("*.polytexture.json ; PolyTexture JSON");
        _saveAsDialog.FileSelected += SaveDocumentAsPath;
        AddChild(_saveAsDialog);

        _outputExportDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.SaveFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Title = "Export Output PNG"
        };
        _outputExportDialog.AddFilter("*.png ; PNG Image");
        _outputExportDialog.FileSelected += ExportOutputToPath;
        AddChild(_outputExportDialog);
    }

    private void BuildActionBar(VBoxContainer mainLayout)
    {
        HBoxContainer toolbar = new();
        toolbar.AddThemeConstantOverride("separation", 8);
        mainLayout.AddChild(toolbar);

        _drawMenuButton = new MenuButton { Text = "Draw" };
        _drawMenuButton.GetPopup().IdPressed += OnDrawMenuPressed;
        toolbar.AddChild(_drawMenuButton);

        _guideMenuButton = new MenuButton { Text = "Sources" };
        PopupMenu guidePopup = _guideMenuButton.GetPopup();
        guidePopup.AddItem("Guide Point", GuidePointMenuId);
        guidePopup.AddItem("Guide Axis", GuideAxisMenuId);
        guidePopup.AddItem("Rectangle Region", RectangleRegionMenuId);
        guidePopup.IdPressed += OnGuideMenuPressed;
        toolbar.AddChild(_guideMenuButton);

        _generateMenuButton = new MenuButton { Text = "Generator" };
        PopupMenu generatePopup = _generateMenuButton.GetPopup();
        generatePopup.AddItem("Sweep", GenerateSweepMenuId);
        generatePopup.IdPressed += OnGenerateMenuPressed;
        _generateMenuButton.Disabled = true;
        toolbar.AddChild(_generateMenuButton);

        _operatorMenuButton = new MenuButton { Text = "Operator" };
        PopupMenu operatorPopup = _operatorMenuButton.GetPopup();
        operatorPopup.AddItem("Mirror", OperatorMirrorMenuId);
        operatorPopup.IdPressed += OnOperatorMenuPressed;
        _operatorMenuButton.Disabled = true;
        toolbar.AddChild(_operatorMenuButton);

        _filterMenuButton = new MenuButton { Text = "Filter", TooltipText = "Filters will be added by the first field-output vertical slices." };
        _filterMenuButton.Disabled = true;
        toolbar.AddChild(_filterMenuButton);

        RefreshDrawingMenu();

        toolbar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
    }

    private void BuildWorkspaceSplit(VBoxContainer mainLayout)
    {
        HSplitContainer outerSplit = new()
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SplitOffsets = new[] { 220 }
        };
        mainLayout.AddChild(outerSplit);

        PanelContainer outlinerPanel = new()
        {
            CustomMinimumSize = new Vector2(220, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        outerSplit.AddChild(outlinerPanel);

        MarginContainer outlinerMargin = new();
        outlinerMargin.AddThemeConstantOverride("margin_left", 10);
        outlinerMargin.AddThemeConstantOverride("margin_top", 10);
        outlinerMargin.AddThemeConstantOverride("margin_right", 10);
        outlinerMargin.AddThemeConstantOverride("margin_bottom", 10);
        outlinerPanel.AddChild(outlinerMargin);

        VBoxContainer outlinerStack = new();
        outlinerStack.AddThemeConstantOverride("separation", 8);
        outlinerMargin.AddChild(outlinerStack);
        outlinerStack.AddChild(new Label { Text = "Outliner" });

        _outlinerTree = new Tree
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HideRoot = true,
            Columns = 2,
            AllowReselect = false,
            SelectMode = Tree.SelectModeEnum.Row,
            ScrollHorizontalEnabled = false
        };
        _outlinerTree.SetColumnCustomMinimumWidth(0, 42);
        _outlinerTree.SetColumnExpand(0, false);
        _outlinerTree.SetColumnExpand(1, true);
        StyleBoxFlat selectedOutlinerItem = new()
        {
            BgColor = new Color(0.98f, 0.79f, 0.18f)
        };
        _outlinerTree.AddThemeStyleboxOverride("selected", selectedOutlinerItem);
        _outlinerTree.AddThemeStyleboxOverride("selected_focus", selectedOutlinerItem);
        _outlinerTree.AddThemeColorOverride("font_selected_color", Colors.Black);
        _outlinerTree.ItemSelected += OnOutlinerItemSelected;
        _outlinerTree.ItemEdited += OnOutlinerItemEdited;
        outlinerStack.AddChild(_outlinerTree);

        HBoxContainer outputButtons = new();
        Button addHeightOutputButton = new() { Text = "+ Height", TooltipText = "Bind one Height output to the visible evaluated graph." };
        addHeightOutputButton.Pressed += () => AddOutput(PolyTextureOutputKind.Height);
        outputButtons.AddChild(addHeightOutputButton);
        Button addMaskOutputButton = new() { Text = "+ Mask", TooltipText = "Bind a named mask to the visible evaluated graph." };
        addMaskOutputButton.Pressed += () => AddOutput(PolyTextureOutputKind.Mask);
        outputButtons.AddChild(addMaskOutputButton);
        outlinerStack.AddChild(outputButtons);

        HSplitContainer mainSplit = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SplitOffsets = new[] { -260 }
        };
        outerSplit.AddChild(mainSplit);

        VBoxContainer canvasColumn = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        canvasColumn.AddThemeConstantOverride("separation", 6);
        mainSplit.AddChild(canvasColumn);

        BuildElementDataActionBar(canvasColumn);

        _canvasView = new PolyTextureCanvasView
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _canvasView.SelectedPointChanged += OnSelectedPointChanged;
        _canvasView.DrawPointRequested += OnCanvasDrawPointRequested;
        _canvasView.PointPlacementConfirmed += OnPointPlacementConfirmed;
        _canvasView.ElementSelectedOnCanvas += OnCanvasElementSelected;
        _canvasView.GuidePointRequested += OnCanvasGuidePointRequested;
        _canvasView.GuideSelectedOnCanvas += OnCanvasGuideSelected;
        _canvasView.EditStarted += OnCanvasEditStarted;
        _canvasView.EditFinished += OnCanvasEditFinished;
        _canvasView.DocumentChanged += OnDocumentChanged;
        _canvasView.CursorChanged += OnCanvasCursorChanged;
        canvasColumn.AddChild(_canvasView);

        _inspector = new PolyTextureInspector
        {
            CustomMinimumSize = new Vector2(260, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _inspector.DocumentChanged += OnInspectorDocumentChanged;
        mainSplit.AddChild(_inspector);
    }

    private void BuildElementDataActionBar(VBoxContainer canvasColumn)
    {
        _elementActionBar = new HBoxContainer();
        _elementActionBar.AddThemeConstantOverride("separation", 8);
        canvasColumn.AddChild(_elementActionBar);
        RefreshElementActionBar();
    }

    private void RefreshElementActionBar()
    {
        if (_elementActionBar == null || _document == null)
        {
            return;
        }

        _refreshingElementActionBar = true;
        foreach (Node child in _elementActionBar.GetChildren())
        {
            _elementActionBar.RemoveChild(child);
            child.QueueFree();
        }
        _pointPlacementButton = null;

        PolyTextureItem texture = _document.ActiveTexture;
        if (_document.SelectionKind == PolyTextureSelectionKind.Element
            && _document.ActiveElement is CenterStrokeElement
            && _handleView != PolyTextureHandleView.Points)
        {
            _handleView = PolyTextureHandleView.Points;
            _canvasView.SetHandleView(_handleView);
        }

        if (_sweepCreationState != SweepCreationState.None && _sweepDraft != null)
        {
            BuildSweepDraftActionBar();
        }
        else if (_mirrorCreationState != MirrorCreationState.None && _mirrorDraft != null)
        {
            BuildMirrorDraftActionBar();
        }
        else if (_document.SelectionKind == PolyTextureSelectionKind.Guide && _document.ActiveGuide != null)
        {
            BuildGuideActionBar(_document.ActiveGuide);
        }
        else if (_document.SelectionKind == PolyTextureSelectionKind.Texture && texture != null)
        {
            _elementActionBar.AddChild(new Label { Text = "Surface Domain" });
            _elementActionBar.AddChild(new Label { Text = "Width (cm)" });
            SpinBox widthSpinBox = CreateDomainActionSpinBox(texture.DomainWidthCm);
            widthSpinBox.ValueChanged += value =>
            {
                if (!_refreshingElementActionBar && _document?.ActiveTexture != null)
                {
                    _inspector.ResizeActiveDomain((float)value, _document.ActiveTexture.DomainHeightCm);
                }
            };
            _elementActionBar.AddChild(widthSpinBox);

            _elementActionBar.AddChild(new Label { Text = "Height (cm)" });
            SpinBox heightSpinBox = CreateDomainActionSpinBox(texture.DomainHeightCm);
            heightSpinBox.ValueChanged += value =>
            {
                if (!_refreshingElementActionBar && _document?.ActiveTexture != null)
                {
                    _inspector.ResizeActiveDomain(_document.ActiveTexture.DomainWidthCm, (float)value);
                }
            };
            _elementActionBar.AddChild(heightSpinBox);
        }
        else if (_document.SelectionKind == PolyTextureSelectionKind.Output && _document.ActiveOutput != null)
        {
            BuildOutputActionBar(_document.ActiveOutput);
        }
        else if (_document.SelectionKind == PolyTextureSelectionKind.Point
            && _document.ActivePoint is CenterStrokePoint point)
        {
            OptionButton handleViewOptionButton = new() { CustomMinimumSize = new Vector2(100, 0) };
            bool supportsBezierHandles = _document.ActiveCenterStroke?.SupportsBezierHandles == true;
            if (!supportsBezierHandles && _handleView == PolyTextureHandleView.Bezier)
            {
                _handleView = PolyTextureHandleView.Width;
                _canvasView.SetHandleView(_handleView);
            }
            handleViewOptionButton.AddItem("Points");
            handleViewOptionButton.AddItem("Width");
            if (supportsBezierHandles)
            {
                handleViewOptionButton.AddItem("Bezier");
            }
            handleViewOptionButton.Selected = supportsBezierHandles ? (int)_handleView : Mathf.Min((int)_handleView, 1);
            handleViewOptionButton.ItemSelected += index => SetHandleView((PolyTextureHandleView)(int)index);
            _elementActionBar.AddChild(new Label { Text = "Handle View" });
            _elementActionBar.AddChild(handleViewOptionButton);

            if (_handleView == PolyTextureHandleView.Points)
            {
                AddPointPositionActionField("X", point.X, value => ApplyActionPointPositionChange(current => current.X = value));
                AddPointPositionActionField("Y", point.Y, value => ApplyActionPointPositionChange(current => current.Y = value));
            }
            else if (supportsBezierHandles && _handleView == PolyTextureHandleView.Bezier)
            {
                OptionButton handleModeOptionButton = new() { CustomMinimumSize = new Vector2(100, 0) };
                handleModeOptionButton.AddItem("Free");
                handleModeOptionButton.AddItem("Aligned");
                handleModeOptionButton.AddItem("Mirrored");
                handleModeOptionButton.Selected = HandleModeToActionIndex(point.HandleMode);
                handleModeOptionButton.ItemSelected += index => _inspector.SetActiveHandleMode(HandleModeFromIndex((int)index));
                _elementActionBar.AddChild(new Label { Text = "Handle Mode" });
                _elementActionBar.AddChild(handleModeOptionButton);

                AddPointHandleActionField("In X", point.InHandle.X, editable: true, value => ApplyActionPointChange(current => current.InHandle = new Vector2(value, current.InHandle.Y)));
                AddPointHandleActionField("In Y", point.InHandle.Y, editable: true, value => ApplyActionPointChange(current => current.InHandle = new Vector2(current.InHandle.X, value)));
                AddPointHandleActionField("Out X", point.OutHandle.X, editable: true, value => ApplyActionPointChange(current => current.OutHandle = new Vector2(value, current.OutHandle.Y)));
                AddPointHandleActionField("Out Y", point.OutHandle.Y, editable: true, value => ApplyActionPointChange(current => current.OutHandle = new Vector2(current.OutHandle.X, value)));
            }
            else if (_handleView == PolyTextureHandleView.Width)
            {
                AddPointWidthActionField("Left Width", point.LeftWidth, value => ApplyActionPointChange(current =>
                {
                    current.LeftWidth = value;
                    if (_document.ActiveCenterStroke?.Symmetry == true)
                    {
                        current.RightWidth = value;
                    }
                }));
                AddPointWidthActionField("Right Width", point.RightWidth, value => ApplyActionPointChange(current =>
                {
                    current.RightWidth = value;
                    if (_document.ActiveCenterStroke?.Symmetry == true)
                    {
                        current.LeftWidth = value;
                    }
                }));
            }
        }
        else if (_document.ActiveElement is CenterStrokeElement)
        {
            _pointPlacementButton = new Button { Text = "Add Point" };
            _pointPlacementButton.Pressed += TogglePointPlacement;
            _elementActionBar.AddChild(_pointPlacementButton);
        }
        else
        {
            _elementActionBar.AddChild(new Label { Text = "No actions available" });
        }

        _elementActionBar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        PolyTextureUiDefaults.Apply(_elementActionBar);
        _refreshingElementActionBar = false;
        RefreshGenerateAvailability();
    }

    private void BuildSweepDraftActionBar()
    {
        _elementActionBar.AddChild(new Label { Text = "Sweep" });
        _elementActionBar.AddChild(new Label { Text = $"Source: {DraftElementLabel(_sweepDraft.SourceElementId, "click an element")}" });
        _elementActionBar.AddChild(new Label { Text = $"Target: {DraftElementLabel(_sweepDraft.TargetElementId, "click an element")}" });

        AddSweepDraftSpinBox("Count", _sweepDraft.Count, 1, 256, 1, value => _sweepDraft.Count = (int)value);
        AddSweepDraftSpinBox("Start", _sweepDraft.StartT, 0, 1, 0.01, value => _sweepDraft.StartT = Mathf.Min((float)value, _sweepDraft.EndT));
        AddSweepDraftSpinBox("End", _sweepDraft.EndT, 0, 1, 0.01, value => _sweepDraft.EndT = Mathf.Max((float)value, _sweepDraft.StartT));

        OptionButton alignment = new() { CustomMinimumSize = new Vector2(90, 0) };
        alignment.AddItem("Tangent");
        alignment.AddItem("Normal");
        alignment.Selected = (int)_sweepDraft.Alignment;
        alignment.ItemSelected += index => UpdateSweepDraft(() => _sweepDraft.Alignment = (PolyTextureSweepAlignment)(int)index);
        _elementActionBar.AddChild(new Label { Text = "Align" });
        _elementActionBar.AddChild(alignment);

        OptionButton side = new() { CustomMinimumSize = new Vector2(76, 0) };
        side.AddItem("Left");
        side.AddItem("Right");
        side.AddItem("Both");
        side.Selected = (int)_sweepDraft.SideMode;
        side.ItemSelected += index => UpdateSweepDraft(() => _sweepDraft.SideMode = (PolyTextureSweepSideMode)(int)index);
        _elementActionBar.AddChild(new Label { Text = "Side" });
        _elementActionBar.AddChild(side);

        AddSweepDraftSpinBox("Rotation", _sweepDraft.RotationOffsetDegrees, -3600, 3600, 0.1, value => _sweepDraft.RotationOffsetDegrees = (float)value);
        AddSweepDraftSpinBox("Length Start", _sweepDraft.LengthScaleStart, 0.01, 100, 0.01, value => _sweepDraft.LengthScaleStart = (float)value);
        AddSweepDraftSpinBox("Length End", _sweepDraft.LengthScaleEnd, 0.01, 100, 0.01, value => _sweepDraft.LengthScaleEnd = (float)value);
        AddSweepDraftSpinBox("Width Start", _sweepDraft.WidthScaleStart, 0.01, 100, 0.01, value => _sweepDraft.WidthScaleStart = (float)value);
        AddSweepDraftSpinBox("Width End", _sweepDraft.WidthScaleEnd, 0.01, 100, 0.01, value => _sweepDraft.WidthScaleEnd = (float)value);

        CheckBox renderSource = new() { Text = "Render Source", ButtonPressed = _sweepDraft.RenderSource };
        renderSource.Toggled += value => UpdateSweepDraft(() => _sweepDraft.RenderSource = value);
        _elementActionBar.AddChild(renderSource);
    }

    private void BuildMirrorDraftActionBar()
    {
        _elementActionBar.AddChild(new Label { Text = "Mirror" });
        _elementActionBar.AddChild(new Label { Text = $"Source: {DraftElementLabel(_mirrorDraft.SourceElementId, "click an element")}" });
        _elementActionBar.AddChild(new Label { Text = $"Axis: {DraftElementLabel(_mirrorDraft.AxisGuideId, "click an axis")}" });

        CheckBox renderSource = new() { Text = "Render Source", ButtonPressed = _mirrorDraft.RenderSource };
        renderSource.Toggled += value => UpdateMirrorDraft(() => _mirrorDraft.RenderSource = value);
        _elementActionBar.AddChild(renderSource);

        PolyTextureGuide axis = _document.ActiveTexture?.GetGuide(_mirrorDraft.AxisGuideId);
        if (axis?.Type == PolyTextureGuideType.Axis)
        {
            AddMirrorAxisSpinBox("Axis X1", axis.Position.X, value => axis.Position = new Vector2((float)value, axis.Position.Y));
            AddMirrorAxisSpinBox("Axis Y1", axis.Position.Y, value => axis.Position = new Vector2(axis.Position.X, (float)value));
            AddMirrorAxisSpinBox("Axis X2", axis.AxisEnd.X, value => axis.AxisEnd = new Vector2((float)value, axis.AxisEnd.Y));
            AddMirrorAxisSpinBox("Axis Y2", axis.AxisEnd.Y, value => axis.AxisEnd = new Vector2(axis.AxisEnd.X, (float)value));
        }
    }

    private void BuildGuideActionBar(PolyTextureGuide guide)
    {
        _elementActionBar.AddChild(new Label { Text = guide.Type == PolyTextureGuideType.Axis ? "Guide Axis" : "Guide Point" });
        AddGuideSpinBox("X", guide.Position.X, value => guide.Position = new Vector2((float)value, guide.Position.Y));
        AddGuideSpinBox("Y", guide.Position.Y, value => guide.Position = new Vector2(guide.Position.X, (float)value));
        if (guide.Type == PolyTextureGuideType.Axis)
        {
            AddGuideSpinBox("X2", guide.AxisEnd.X, value => guide.AxisEnd = new Vector2((float)value, guide.AxisEnd.Y));
            AddGuideSpinBox("Y2", guide.AxisEnd.Y, value => guide.AxisEnd = new Vector2(guide.AxisEnd.X, (float)value));
        }
    }

    private void BuildOutputActionBar(PolyTextureOutputBinding output)
    {
        _elementActionBar.AddChild(new Label { Text = $"{(output.Kind == PolyTextureOutputKind.Height ? "Height" : "Mask")}: {output.Name}" });
        Button previewButton = new() { Text = "Preview" };
        previewButton.Pressed += () => PreviewOutput(normalMap: false);
        _elementActionBar.AddChild(previewButton);
        Button exportButton = new() { Text = "Export PNG..." };
        exportButton.Pressed += () => OpenOutputExportDialog(normalMap: false);
        _elementActionBar.AddChild(exportButton);
        if (output.Kind == PolyTextureOutputKind.Height)
        {
            Button normalPreviewButton = new() { Text = "Preview Normal" };
            normalPreviewButton.Pressed += () => PreviewOutput(normalMap: true);
            _elementActionBar.AddChild(normalPreviewButton);
            Button normalExportButton = new() { Text = "Export Normal..." };
            normalExportButton.Pressed += () => OpenOutputExportDialog(normalMap: true);
            _elementActionBar.AddChild(normalExportButton);
        }
        Button clearButton = new() { Text = "Clear Preview" };
        clearButton.Pressed += () => _canvasView.SetOutputPreview(null);
        _elementActionBar.AddChild(clearButton);
    }

    private void PreviewOutput(bool normalMap)
    {
        PolyTextureItem texture = _document?.ActiveTexture;
        PolyTextureOutputBinding output = _document?.ActiveOutput;
        if (texture == null || output == null)
        {
            return;
        }
        Image scalar = PolyTextureBakeService.BakeScalar(texture, output, texture.PreviewWidthPx, texture.PreviewHeightPx);
        Image preview = normalMap
            ? PolyTextureBakeService.DeriveNormalMap(scalar, texture, output.HeightAmplitudeCm)
            : scalar;
        _canvasView.SetOutputPreview(preview);
        SetStatus($"Previewing {(normalMap ? "Normal from " : string.Empty)}{output.Name} at {texture.PreviewWidthPx} x {texture.PreviewHeightPx} px.");
    }

    private void OpenOutputExportDialog(bool normalMap)
    {
        PolyTextureOutputBinding output = _document?.ActiveOutput;
        if (output == null)
        {
            return;
        }
        _exportNormalMap = normalMap;
        string suffix = normalMap ? "_normal" : output.Kind == PolyTextureOutputKind.Height ? "_height" : "_mask";
        _outputExportDialog.CurrentFile = $"{SanitizeId(output.Name)}{suffix}.png";
        _outputExportDialog.PopupCenteredRatio(0.6f);
    }

    private void ExportOutputToPath(string path)
    {
        PolyTextureItem texture = _document?.ActiveTexture;
        PolyTextureOutputBinding output = _document?.ActiveOutput;
        if (texture == null || output == null)
        {
            return;
        }
        if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            path += ".png";
        }
        Image scalar = PolyTextureBakeService.BakeScalar(texture, output, texture.PreviewWidthPx, texture.PreviewHeightPx);
        Image image = _exportNormalMap
            ? PolyTextureBakeService.DeriveNormalMap(scalar, texture, output.HeightAmplitudeCm)
            : scalar;
        Error result = PolyTextureBakeService.SavePng(path, image);
        SetStatus(result == Error.Ok ? $"Exported {path}." : $"Export failed: {result}.");
    }

    private void AddGuideSpinBox(string label, float value, Action<double> apply)
    {
        _elementActionBar.AddChild(new Label { Text = label });
        SpinBox spinBox = new()
        {
            MinValue = -8192,
            MaxValue = 8192,
            Step = 0.01,
            Rounded = false,
            Value = value,
            CustomMinimumSize = new Vector2(72, 0)
        };
        spinBox.ValueChanged += nextValue =>
        {
            apply(nextValue);
            _canvasView.QueueRedraw();
            MarkChanged("Guide changed.");
        };
        _elementActionBar.AddChild(spinBox);
    }

    private void AddMirrorAxisSpinBox(string label, float value, Action<double> apply)
    {
        _elementActionBar.AddChild(new Label { Text = label });
        SpinBox spinBox = new()
        {
            MinValue = -8192,
            MaxValue = 8192,
            Step = 0.01,
            Rounded = false,
            Value = value,
            CustomMinimumSize = new Vector2(72, 0)
        };
        spinBox.ValueChanged += nextValue =>
        {
            apply(nextValue);
            MarkChanged("Guide axis changed.");
            UpdateMirrorDraft(() => { });
        };
        _elementActionBar.AddChild(spinBox);
    }

    private void AddSweepDraftSpinBox(string label, double value, double minValue, double maxValue, double step, Action<double> apply)
    {
        _elementActionBar.AddChild(new Label { Text = label });
        SpinBox spinBox = new()
        {
            MinValue = minValue,
            MaxValue = maxValue,
            Step = step,
            Rounded = step >= 1.0,
            Value = value,
            CustomMinimumSize = new Vector2(72, 0)
        };
        spinBox.ValueChanged += nextValue => UpdateSweepDraft(() => apply(nextValue));
        _elementActionBar.AddChild(spinBox);
    }

    private static string DraftElementLabel(string elementId, string fallback)
    {
        return string.IsNullOrEmpty(elementId) ? fallback : elementId;
    }

    private void UpdateSweepDraft(Action apply)
    {
        if (_sweepDraft == null)
        {
            return;
        }

        apply();
        _canvasView.SetSweepDraft(_sweepDraft);
    }

    private void UpdateMirrorDraft(Action apply)
    {
        if (_mirrorDraft == null)
        {
            return;
        }

        apply();
        _canvasView.SetMirrorDraft(_mirrorDraft);
    }

    private void AddPointHandleActionField(string label, float value, bool editable, Action<float> apply)
    {
        _elementActionBar.AddChild(new Label { Text = label });
        SpinBox spinBox = CreatePreciseActionSpinBox(value, -4096, 4096);
        spinBox.Editable = editable;
        spinBox.MouseFilter = editable ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
        spinBox.ValueChanged += nextValue =>
        {
            if (!_refreshingElementActionBar)
            {
                apply((float)nextValue);
            }
        };
        _elementActionBar.AddChild(spinBox);
    }

    private void AddPointWidthActionField(string label, float value, Action<float> apply)
    {
        _elementActionBar.AddChild(new Label { Text = label });
        SpinBox spinBox = CreatePreciseActionSpinBox(value, 0, 4096);
        spinBox.ValueChanged += nextValue =>
        {
            if (!_refreshingElementActionBar)
            {
                apply((float)nextValue);
            }
        };
        _elementActionBar.AddChild(spinBox);
    }

    private void AddPointPositionActionField(string label, float value, Action<float> apply)
    {
        _elementActionBar.AddChild(new Label { Text = label });
        SpinBox spinBox = CreatePreciseActionSpinBox(value, -4096, 4096);
        spinBox.ValueChanged += nextValue =>
        {
            if (!_refreshingElementActionBar)
            {
                apply((float)nextValue);
            }
        };
        _elementActionBar.AddChild(spinBox);
    }

    private void ApplyActionPointChange(Action<CenterStrokePoint> apply)
    {
        if (_refreshingElementActionBar || _document?.ActivePoint is not CenterStrokePoint point)
        {
            return;
        }

        apply(point);
        if (_document.SelectedPointIndex == 0)
        {
            _document.ActiveCenterStroke?.NormalizeAnchor();
        }
        _canvasView.QueueRedraw();
        _inspector.Refresh();
        MarkChanged("Point handles changed.");
    }

    private void ApplyActionPointPositionChange(Action<CenterStrokePoint> apply)
    {
        if (_refreshingElementActionBar || _document?.ActivePoint is not CenterStrokePoint point)
        {
            return;
        }

        apply(point);
        _document.ActiveCenterStroke?.NormalizeLocalAxes();
        _canvasView.QueueRedraw();
        _inspector.Refresh();
        MarkChanged("Point position changed.");
    }

    private void SetHandleView(PolyTextureHandleView handleView)
    {
        _handleView = handleView;
        _canvasView.SetHandleView(handleView);
        RefreshElementActionBar();
    }

    private static PolyTextureHandleMode HandleModeFromIndex(int index)
    {
        return index switch
        {
            0 => PolyTextureHandleMode.Free,
            2 => PolyTextureHandleMode.Mirrored,
            _ => PolyTextureHandleMode.Aligned
        };
    }

    private static int HandleModeToActionIndex(PolyTextureHandleMode mode)
    {
        return mode switch
        {
            PolyTextureHandleMode.Free => 0,
            PolyTextureHandleMode.Mirrored => 2,
            _ => 1
        };
    }

    private static SpinBox CreateActionSpinBox(int value, int minValue = 1)
    {
        return new SpinBox
        {
            MinValue = minValue,
            MaxValue = 8192,
            Step = 1,
            Value = value,
            Rounded = true,
            CustomMinimumSize = new Vector2(86, 0)
        };
    }

    private static SpinBox CreatePreciseActionSpinBox(float value, float minValue, float maxValue)
    {
        return new SpinBox
        {
            MinValue = minValue,
            MaxValue = maxValue,
            Step = 0.01,
            Value = value,
            CustomMinimumSize = new Vector2(86, 0)
        };
    }

    private static SpinBox CreateDomainActionSpinBox(float value)
    {
        return new SpinBox
        {
            MinValue = 0.01,
            MaxValue = 100000,
            Step = 0.01,
            Value = value,
            CustomMinimumSize = new Vector2(100, 0)
        };
    }

    private void BuildStatusBar(VBoxContainer mainLayout)
    {
        PanelContainer statusPanel = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        mainLayout.AddChild(statusPanel);

        MarginContainer statusMargin = new();
        statusMargin.AddThemeConstantOverride("margin_left", 8);
        statusMargin.AddThemeConstantOverride("margin_top", 4);
        statusMargin.AddThemeConstantOverride("margin_right", 8);
        statusMargin.AddThemeConstantOverride("margin_bottom", 4);
        statusPanel.AddChild(statusMargin);

        HBoxContainer statusBar = new();
        statusBar.AddThemeConstantOverride("separation", 12);
        statusMargin.AddChild(statusBar);

        _activeDocumentLabel = new Label
        {
            Text = "-",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClipText = true
        };
        statusBar.AddChild(_activeDocumentLabel);

        _statusLabel = new Label
        {
            Text = "Ready",
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClipText = true
        };
        statusBar.AddChild(_statusLabel);

        _mousePositionLabel = new Label
        {
            Text = "Mouse: [0, 0]",
            HorizontalAlignment = HorizontalAlignment.Right,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClipText = true
        };
        statusBar.AddChild(_mousePositionLabel);
    }

    private void CreateNewDocument()
    {
        string documentName = _documentNameLineEdit?.Text.Trim() ?? string.Empty;
        _documentPath = string.Empty;
        PolyTextureDocument document = new();
        if (!string.IsNullOrEmpty(documentName))
        {
            document.Name = documentName;
        }

        SetDocument(document, "New document.");
        MarkChanged();
    }

    private void OpenLoadDialog()
    {
        string path = string.IsNullOrEmpty(_documentPath) ? DefaultDocumentPath : GlobalizeDocumentPath(_documentPath);
        _loadDialog.CurrentDir = path.GetBaseDir();
        _loadDialog.CurrentFile = string.Empty;
        _loadDialog.PopupCenteredRatio(0.6f);
    }

    private void OpenSaveAsDialog()
    {
        string path = string.IsNullOrEmpty(_documentPath) ? DefaultDocumentPath : GlobalizeDocumentPath(_documentPath);
        _saveAsDialog.CurrentDir = path.GetBaseDir();
        _saveAsDialog.CurrentFile = path.GetFile();
        _saveAsDialog.PopupCenteredRatio(0.6f);
    }

    private static string GlobalizeDocumentPath(string path)
    {
        return path.StartsWith("res://", StringComparison.Ordinal) || path.StartsWith("user://", StringComparison.Ordinal)
            ? ProjectSettings.GlobalizePath(path)
            : path;
    }

    private void LoadDocumentFromPath(string path)
    {
        if (PolyTextureStore.Load(path, out PolyTextureDocument loadedDocument, out string error))
        {
            _documentPath = path;
            SetDocument(loadedDocument, $"Loaded {path}.");
            _hasUnsavedChanges = false;
            RefreshHeader();
        }
        else
        {
            SetStatus($"Load failed: {error}");
        }
    }

    private void SaveDocument()
    {
        if (_document == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(_documentPath))
        {
            OpenSaveAsDialog();
            return;
        }

        SaveDocumentAsPath(_documentPath);
    }

    private void SaveDocumentAsPath(string path)
    {
        if (_document == null)
        {
            return;
        }

        path = NormalizeDocumentPath(path);
        if (PolyTextureStore.Save(path, _document, out string error))
        {
            _documentPath = path;
            _hasUnsavedChanges = false;
            SetStatus($"Saved {path}.");
            RefreshHeader();
        }
        else
        {
            SetStatus($"Save failed: {error}");
        }
    }

    private void AddTexture()
    {
        if (_document == null)
        {
            return;
        }

        bool wasEmpty = _document.Textures.Count == 0;
        string textureName = GetNewTextureName();
        PolyTextureItem texture = new()
        {
            Id = EnsureUniqueTextureId(textureName),
            Name = textureName,
            DomainWidthCm = PolyTextureUnits.DefaultDomainSizeCm,
            DomainHeightCm = PolyTextureUnits.DefaultDomainSizeCm,
            PreviewWidthPx = _document.DefaultPreviewWidthPx,
            PreviewHeightPx = _document.DefaultPreviewHeightPx,
            Visible = true
        };
        texture.Elements.Clear();
        _document.Textures.Add(texture);
        _document.ActiveTextureId = texture.Id;
        _document.ActiveElementId = string.Empty;
        _document.SelectedPointIndex = -1;
        _document.SelectionKind = PolyTextureSelectionKind.Texture;
        _canvasView.SelectPoint(-1);
        _newTextureNameLineEdit.Text = string.Empty;
        RefreshAll();
        if (wasEmpty)
        {
            _canvasView.FitView();
        }
        MarkChanged("Texture added.");
    }

    private string GetNewTextureName()
    {
        string requestedName = _newTextureNameLineEdit?.Text.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(requestedName))
        {
            return requestedName;
        }

        return $"Texture {_document.Textures.Count + 1}";
    }

    private void ApplyDocumentName(string value)
    {
        if (_refreshingHeader || _document == null)
        {
            return;
        }

        string nextName = value.Trim();
        if (string.IsNullOrEmpty(nextName) || nextName.Equals(_document.Name, StringComparison.Ordinal))
        {
            RefreshHeader();
            return;
        }

        _document.Name = nextName;
        MarkChanged("Document renamed.");
    }

    private void DuplicateTexture()
    {
        PolyTextureItem activeTexture = _document?.ActiveTexture;
        if (activeTexture == null)
        {
            return;
        }

        PolyTextureItem duplicate = activeTexture.Clone();
        duplicate.Id = EnsureUniqueTextureId($"{activeTexture.Id}_copy");
        duplicate.Name = $"{activeTexture.Name} Copy";
        _document.Textures.Add(duplicate);
        _document.ActiveTextureId = duplicate.Id;
        _document.ActiveElementId = duplicate.Elements.Count > 0 ? duplicate.Elements[0].Id : string.Empty;
        _document.SelectedPointIndex = -1;
        _document.SelectionKind = PolyTextureSelectionKind.Texture;
        RefreshAll();
        MarkChanged("Texture duplicated.");
    }

    private void DeleteActiveTexture()
    {
        PolyTextureItem activeTexture = _document?.ActiveTexture;
        if (_document == null || activeTexture == null)
        {
            SetStatus("No texture selected.");
            return;
        }

        int removedIndex = _document.Textures.IndexOf(activeTexture);
        _document.Textures.Remove(activeTexture);
        if (_document.Textures.Count > 0)
        {
            int nextIndex = Mathf.Clamp(removedIndex, 0, _document.Textures.Count - 1);
            _document.ActiveTextureId = _document.Textures[nextIndex].Id;
            _document.ActiveElementId = _document.ActiveTexture.Elements.Count > 0 ? _document.ActiveTexture.Elements[0].Id : string.Empty;
        }
        else
        {
            _document.ActiveTextureId = string.Empty;
            _document.ActiveElementId = string.Empty;
        }

        _document.SelectedPointIndex = -1;
        _document.SelectionKind = PolyTextureSelectionKind.Texture;
        _canvasView.SelectPoint(-1);
        RefreshAll();
        MarkChanged("Texture deleted.");
    }

    private void AddCenterStroke(bool drawCenterPath)
    {
        PolyTextureItem activeTexture = _document?.ActiveTexture;
        if (activeTexture == null)
        {
            return;
        }

        CenterStrokeElement element = drawCenterPath ? new CenterPathElement() : new CenterStrokeElement();
        element.Id = EnsureUniqueElementId(activeTexture, drawCenterPath ? "center_path" : "center_stroke");
        element.Name = drawCenterPath ? "Center Path" : "Center Stroke";
        element.Enabled = true;
        element.Opacity = 0.86f;
        element.Symmetry = true;
        element.Falloff = 0.0f;
        activeTexture.Elements.Add(element);
        _document.ActiveElementId = element.Id;
        _document.SelectedPointIndex = -1;
        _document.SelectionKind = PolyTextureSelectionKind.Element;
        _canvasView.SelectPoint(-1);
        RefreshAll();
        MarkChanged(drawCenterPath ? "Center path added." : "Center stroke added.");
    }

    private void ToggleCenterStrokeDrawing(bool drawCenterPath)
    {
        if (_drawingCenterStroke)
        {
            FinishCenterStrokeDrawing();
            return;
        }

        AddCenterStroke(drawCenterPath);
        if (_document?.ActiveCenterStroke == null)
        {
            return;
        }

        _drawingCenterStroke = true;
        _drawingCenterPath = drawCenterPath;
        _canvasView.SetDrawingMode(true);
        RefreshDrawingMenu();
        SetStatus(drawCenterPath ? "Drawing center path. Click in the canvas to place points." : "Drawing center stroke. Click in the canvas to place points.");
    }

    private void FinishCenterStrokeDrawing()
    {
        bool finishedCenterPath = _drawingCenterPath;
        _drawingCenterStroke = false;
        _drawingCenterPath = false;
        _canvasView?.SetDrawingMode(false);
        RefreshDrawingMenu();

        SetStatus(finishedCenterPath ? "Center path drawing finished." : "Center stroke drawing finished.");
    }

    private void RefreshDrawingMenu()
    {
        if (_drawMenuButton == null)
        {
            return;
        }

        PopupMenu popup = _drawMenuButton.GetPopup();
        popup.Clear();
        if (_drawingCenterStroke)
        {
            popup.AddItem("Finish Drawing", FinishDrawingMenuId);
            popup.AddSeparator();
        }

        popup.AddItem("Center Stroke", DrawCenterStrokeMenuId);
        popup.AddItem("Center Path", DrawCenterPathMenuId);
    }

    private void OnDrawMenuPressed(long id)
    {
        switch ((int)id)
        {
            case DrawCenterStrokeMenuId:
                if (_drawingCenterStroke)
                {
                    FinishCenterStrokeDrawing();
                }
                ToggleCenterStrokeDrawing(drawCenterPath: false);
                break;
            case DrawCenterPathMenuId:
                if (_drawingCenterStroke)
                {
                    FinishCenterStrokeDrawing();
                }
                ToggleCenterStrokeDrawing(drawCenterPath: true);
                break;
            case FinishDrawingMenuId:
                FinishCenterStrokeDrawing();
                break;
        }
    }

    private void OnGenerateMenuPressed(long id)
    {
        if ((int)id == GenerateSweepMenuId)
        {
            BeginSweepCreation();
        }
    }

    private void OnOperatorMenuPressed(long id)
    {
        if ((int)id == OperatorMirrorMenuId)
        {
            BeginMirrorCreation();
        }
    }

    private void OnGuideMenuPressed(long id)
    {
        if ((int)id == GuidePointMenuId)
        {
            BeginGuideDrawing(GuideDrawingState.Point);
        }
        else if ((int)id == GuideAxisMenuId)
        {
            BeginGuideDrawing(GuideDrawingState.AxisStart);
        }
        else if ((int)id == RectangleRegionMenuId)
        {
            AddRectangleRegion();
        }
    }

    private void AddRectangleRegion()
    {
        PolyTextureItem texture = _document?.ActiveTexture;
        if (texture == null)
        {
            SetStatus("Select or create a texture before adding a region.");
            return;
        }

        float width = Mathf.Min(80.0f, texture.DomainWidthCm);
        float height = Mathf.Min(40.0f, texture.DomainHeightCm);
        RectangleRegionElement region = new()
        {
            Id = EnsureUniqueElementId(texture, "rectangle_region"),
            Name = "Rectangle Region",
            WidthCm = width,
            HeightCm = height,
            Position = new Vector2(width * 0.5f, height * 0.5f)
        };
        texture.Elements.Add(region);
        _document.ActiveElementId = region.Id;
        _document.ActiveGuideId = string.Empty;
        _document.ActiveOutputId = string.Empty;
        _document.SelectedPointIndex = -1;
        _document.SelectionKind = PolyTextureSelectionKind.Element;
        RefreshAll();
        MarkChanged("Rectangle Region added.");
    }

    private void BeginGuideDrawing(GuideDrawingState state)
    {
        if (_document?.ActiveTexture == null)
        {
            SetStatus("Select or create a texture before drawing a guide.");
            return;
        }

        CancelPointPlacement();
        FinishCenterStrokeDrawing();
        _guideDrawingState = state;
        _guideAxisDraft = null;
        _canvasView.SetGuideDrawingMode(true);
        SetStatus(state == GuideDrawingState.Point
            ? "Guide point: click in the canvas to place it."
            : "Guide axis: click the first axis point.");
    }

    private void OnCanvasGuidePointRequested(Vector2 documentPosition)
    {
        PolyTextureItem texture = _document?.ActiveTexture;
        if (texture == null || _guideDrawingState == GuideDrawingState.None)
        {
            return;
        }

        if (_guideDrawingState == GuideDrawingState.Point)
        {
            PolyTextureGuide guide = new()
            {
                Id = EnsureUniqueGuideId(texture, "guide_point"),
                Name = "Guide Point",
                Type = PolyTextureGuideType.Point,
                Position = documentPosition,
                AxisEnd = documentPosition
            };
            texture.Guides.Add(guide);
            SelectGuide(guide);
            CancelGuideDrawing(clearStatus: false);
            RefreshAll();
            MarkChanged("Guide point added.");
            return;
        }

        if (_guideDrawingState == GuideDrawingState.AxisStart)
        {
            _guideAxisDraft = new PolyTextureGuide
            {
                Name = "Guide Axis",
                Type = PolyTextureGuideType.Axis,
                Position = documentPosition,
                AxisEnd = documentPosition
            };
            _guideDrawingState = GuideDrawingState.AxisEnd;
            _canvasView.SetGuideAxisPreview(_guideAxisDraft);
            SetStatus("Guide axis: click the second axis point.");
            return;
        }

        if (_guideDrawingState == GuideDrawingState.AxisEnd && _guideAxisDraft != null)
        {
            _guideAxisDraft.Id = EnsureUniqueGuideId(texture, "guide_axis");
            _guideAxisDraft.AxisEnd = documentPosition;
            texture.Guides.Add(_guideAxisDraft);
            SelectGuide(_guideAxisDraft);
            CancelGuideDrawing(clearStatus: false);
            RefreshAll();
            MarkChanged("Guide axis added.");
        }
    }

    private void CancelGuideDrawing(bool clearStatus = true)
    {
        _guideDrawingState = GuideDrawingState.None;
        _guideAxisDraft = null;
        _canvasView?.SetGuideDrawingMode(false);
        _canvasView?.SetGuideAxisPreview(null);
        if (clearStatus)
        {
            SetStatus("Guide drawing cancelled.");
        }
    }

    private void SelectGuide(PolyTextureGuide guide)
    {
        _document.ActiveGuideId = guide.Id;
        _document.SelectedPointIndex = -1;
        _document.SelectionKind = PolyTextureSelectionKind.Guide;
    }

    private void RefreshGenerateAvailability()
    {
        if (_generateMenuButton != null)
        {
            _generateMenuButton.Disabled = _sweepCreationState != SweepCreationState.None
                || _mirrorCreationState != MirrorCreationState.None
                || !CanBeginSweepCreation();
        }

        if (_operatorMenuButton != null)
        {
            _operatorMenuButton.Disabled = _sweepCreationState != SweepCreationState.None
                || _mirrorCreationState != MirrorCreationState.None
                || !CanBeginMirrorCreation();
        }
    }

    private bool CanBeginSweepCreation()
    {
        if (_document?.ActiveTexture == null)
        {
            return false;
        }

        int drawElementCount = 0;
        foreach (PolyTextureElement element in _document.ActiveTexture.Elements)
        {
            if (element is CenterStrokeElement)
            {
                drawElementCount++;
            }
        }
        return drawElementCount >= 2;
    }

    private bool CanBeginMirrorCreation()
    {
        if (_document?.ActiveTexture == null)
        {
            return false;
        }

        bool hasElement = false;
        foreach (PolyTextureElement element in _document.ActiveTexture.Elements)
        {
            hasElement |= element.Enabled;
        }
        foreach (PolyTextureGuide guide in _document.ActiveTexture.Guides)
        {
            if (guide.Type == PolyTextureGuideType.Axis)
            {
                return hasElement;
            }
        }
        return false;
    }

    private void BeginSweepCreation()
    {
        if (!CanBeginSweepCreation())
        {
            SetStatus("Create at least two strokes or paths before generating a sweep.");
            return;
        }

        _sweepDraft = new SweepGeneratorElement
        {
            Enabled = true,
            Opacity = 0.86f,
            SideMode = PolyTextureSweepSideMode.Right
        };
        _sweepCreationState = SweepCreationState.AwaitSource;
        _canvasView.SetSweepDraft(_sweepDraft);
        _canvasView.SetElementSelectionMode(true);
        RefreshElementActionBar();
        SetStatus("Sweep: select a source stroke or path.");
    }

    private void OnCanvasElementSelected(string elementId)
    {
        if (_sweepCreationState != SweepCreationState.None)
        {
            SelectSweepCandidate(elementId);
        }
        else if (_mirrorCreationState != MirrorCreationState.None)
        {
            SelectMirrorSource(elementId);
        }
    }

    private void OnCanvasGuideSelected(string guideId)
    {
        if (_mirrorCreationState != MirrorCreationState.None)
        {
            SelectMirrorAxis(guideId);
        }
    }

    private void SelectSweepCandidate(string elementId)
    {
        PolyTextureItem texture = _document?.ActiveTexture;
        if (texture?.GetElement(elementId) is not CenterStrokeElement element || _sweepCreationState == SweepCreationState.None)
        {
            return;
        }

        _document.ActiveElementId = element.Id;
        _document.SelectedPointIndex = -1;
        _document.SelectionKind = PolyTextureSelectionKind.Element;
        _canvasView.SetDocument(_document);
        _inspector.SetDocument(_document);
        RebuildOutliner();
        AssignSweepCandidate(element);
    }

    private void AssignSweepCandidate(CenterStrokeElement element)
    {
        if (_sweepDraft == null)
        {
            return;
        }

        if (_sweepCreationState == SweepCreationState.AwaitSource)
        {
            _sweepDraft.SourceElementId = element.Id;
            _sweepDraft.TargetElementId = string.Empty;
            _sweepCreationState = SweepCreationState.AwaitTarget;
            _canvasView.SetSweepDraft(_sweepDraft);
            RefreshElementActionBar();
            SetStatus("Sweep: select the target stroke or path.");
            return;
        }

        if (_sweepCreationState == SweepCreationState.AwaitTarget)
        {
            if (element.Id.Equals(_sweepDraft.SourceElementId, StringComparison.Ordinal))
            {
                SetStatus("Sweep: target must differ from the source.");
                return;
            }

            _sweepDraft.TargetElementId = element.Id;
            _sweepCreationState = SweepCreationState.Preview;
            _canvasView.SetElementSelectionMode(false);
            _canvasView.SetSweepDraft(_sweepDraft);
            RefreshElementActionBar();
            SetStatus("Sweep preview ready. Press Enter to create, or Escape to choose another target.");
        }
    }

    private void StepBackSweepCreation()
    {
        if (_sweepDraft == null)
        {
            return;
        }

        if (_sweepCreationState == SweepCreationState.Preview)
        {
            _sweepDraft.TargetElementId = string.Empty;
            _sweepCreationState = SweepCreationState.AwaitTarget;
            _canvasView.SetElementSelectionMode(true);
            _canvasView.SetSweepDraft(_sweepDraft);
            RefreshElementActionBar();
            SetStatus("Sweep: select another target stroke or path.");
            return;
        }

        if (_sweepCreationState == SweepCreationState.AwaitTarget)
        {
            _sweepDraft.SourceElementId = string.Empty;
            _sweepCreationState = SweepCreationState.AwaitSource;
            _canvasView.SetElementSelectionMode(true);
            _canvasView.SetSweepDraft(_sweepDraft);
            RefreshElementActionBar();
            SetStatus("Sweep: select another source stroke or path.");
            return;
        }

        CancelSweepCreation();
    }

    private void CommitSweepDraft()
    {
        if (_sweepCreationState != SweepCreationState.Preview || _sweepDraft == null || _document?.ActiveTexture == null)
        {
            return;
        }

        SweepGeneratorElement sweep = (SweepGeneratorElement)_sweepDraft.Clone();
        sweep.Id = EnsureUniqueElementId(_document.ActiveTexture, "sweep");
        sweep.Name = "Sweep";
        _document.ActiveTexture.Elements.Add(sweep);
        _document.ActiveElementId = sweep.Id;
        _document.SelectedPointIndex = -1;
        _document.SelectionKind = PolyTextureSelectionKind.Element;
        CancelSweepCreation(refresh: false);
        RefreshAll();
        MarkChanged("Live Sweep generator added.");
    }

    private void CancelSweepCreation(bool refresh = true)
    {
        _sweepCreationState = SweepCreationState.None;
        _sweepDraft = null;
        _canvasView.SetElementSelectionMode(false);
        _canvasView.SetSweepDraft(null);
        if (refresh)
        {
            RefreshElementActionBar();
        }
        SetStatus("Sweep creation cancelled.");
    }

    private void BeginMirrorCreation()
    {
        if (!CanBeginMirrorCreation())
        {
            SetStatus("Create an element and an axis guide before applying Mirror.");
            return;
        }

        CancelSweepCreation(refresh: false);
        _mirrorDraft = new MirrorGeneratorElement
        {
            Enabled = true,
            Opacity = 0.86f
        };
        _mirrorCreationState = MirrorCreationState.AwaitSource;
        _canvasView.SetMirrorDraft(_mirrorDraft);
        _canvasView.SetElementSelectionMode(true);
        _canvasView.SetGuideSelectionMode(false);
        RefreshElementActionBar();
        SetStatus("Mirror: select a source or generated element.");
    }

    private void SelectMirrorSource(string elementId)
    {
        PolyTextureItem texture = _document?.ActiveTexture;
        PolyTextureElement element = texture?.GetElement(elementId);
        if (element == null || element is MirrorGeneratorElement || _mirrorCreationState != MirrorCreationState.AwaitSource)
        {
            return;
        }

        _document.ActiveElementId = element.Id;
        _document.ActiveGuideId = string.Empty;
        _document.SelectedPointIndex = -1;
        _document.SelectionKind = PolyTextureSelectionKind.Element;
        _canvasView.SetDocument(_document);
        _inspector.SetDocument(_document);
        RebuildOutliner();
        _mirrorDraft.SourceElementId = element.Id;
        _mirrorCreationState = MirrorCreationState.AwaitAxis;
        _canvasView.SetElementSelectionMode(false);
        _canvasView.SetGuideSelectionMode(true);
        _canvasView.SetMirrorDraft(_mirrorDraft);
        RefreshElementActionBar();
        SetStatus("Mirror: select an axis guide.");
    }

    private void SelectMirrorAxis(string guideId)
    {
        PolyTextureItem texture = _document?.ActiveTexture;
        PolyTextureGuide guide = texture?.GetGuide(guideId);
        if (_mirrorCreationState != MirrorCreationState.AwaitAxis || guide?.Type != PolyTextureGuideType.Axis)
        {
            return;
        }

        _document.ActiveGuideId = guide.Id;
        _document.ActiveElementId = string.Empty;
        _document.SelectedPointIndex = -1;
        _document.SelectionKind = PolyTextureSelectionKind.Guide;
        _canvasView.SetDocument(_document);
        _inspector.SetDocument(_document);
        RebuildOutliner();
        _mirrorDraft.AxisGuideId = guide.Id;
        _mirrorCreationState = MirrorCreationState.Preview;
        _canvasView.SetGuideSelectionMode(false);
        _canvasView.SetMirrorDraft(_mirrorDraft);
        RefreshElementActionBar();
        SetStatus("Mirror preview ready. Press Enter to create, or Escape to choose another axis.");
    }

    private void StepBackMirrorCreation()
    {
        if (_mirrorDraft == null)
        {
            return;
        }

        if (_mirrorCreationState == MirrorCreationState.Preview)
        {
            _mirrorDraft.AxisGuideId = string.Empty;
            _mirrorCreationState = MirrorCreationState.AwaitAxis;
            _canvasView.SetGuideSelectionMode(true);
            _canvasView.SetMirrorDraft(_mirrorDraft);
            RefreshElementActionBar();
            SetStatus("Mirror: select another axis guide.");
            return;
        }

        if (_mirrorCreationState == MirrorCreationState.AwaitAxis)
        {
            _mirrorDraft.SourceElementId = string.Empty;
            _mirrorCreationState = MirrorCreationState.AwaitSource;
            _canvasView.SetElementSelectionMode(true);
            _canvasView.SetMirrorDraft(_mirrorDraft);
            RefreshElementActionBar();
            SetStatus("Mirror: select another source or generated element.");
            return;
        }

        CancelMirrorCreation();
    }

    private void CommitMirrorDraft()
    {
        if (_mirrorCreationState != MirrorCreationState.Preview || _mirrorDraft == null || _document?.ActiveTexture == null)
        {
            return;
        }

        MirrorGeneratorElement mirror = (MirrorGeneratorElement)_mirrorDraft.Clone();
        mirror.Id = EnsureUniqueElementId(_document.ActiveTexture, "mirror");
        mirror.Name = "Mirror";
        _document.ActiveTexture.Elements.Add(mirror);
        _document.ActiveElementId = mirror.Id;
        _document.ActiveGuideId = string.Empty;
        _document.SelectedPointIndex = -1;
        _document.SelectionKind = PolyTextureSelectionKind.Element;
        CancelMirrorCreation(refresh: false);
        RefreshAll();
        MarkChanged("Live Mirror operator added.");
    }

    private void CancelMirrorCreation(bool refresh = true)
    {
        _mirrorCreationState = MirrorCreationState.None;
        _mirrorDraft = null;
        _canvasView.SetElementSelectionMode(false);
        _canvasView.SetGuideSelectionMode(false);
        _canvasView.SetMirrorDraft(null);
        if (refresh)
        {
            RefreshElementActionBar();
        }
        SetStatus("Mirror creation cancelled.");
    }

    private void OnCanvasDrawPointRequested(Vector2 documentPosition)
    {
        if (!_drawingCenterStroke || _document?.ActiveCenterStroke == null)
        {
            return;
        }

        CenterStrokeElement centerStroke = _document.ActiveCenterStroke;
        Vector2 localPosition;
        if (centerStroke.Points.Count == 0)
        {
            centerStroke.Transform.Position = documentPosition;
            localPosition = Vector2.Zero;
        }
        else
        {
            localPosition = centerStroke.Transform.InverseTransformPoint(documentPosition);
        }
        CenterStrokePoint point = new()
        {
            X = localPosition.X,
            Y = localPosition.Y,
            LeftWidth = 12.0f,
            RightWidth = 12.0f,
            HandleMode = _document.ActiveCenterStroke.SupportsBezierHandles ? PolyTextureHandleMode.Aligned : PolyTextureHandleMode.Linear
        };
        centerStroke.Points.Add(point);
        SortCenterStrokePoints(centerStroke);
        centerStroke.NormalizeLocalAxes();
        _canvasView.SelectPoint(centerStroke.Points.IndexOf(point));
        RefreshAll();
        MarkChanged("Center stroke point added.");
    }

    private void BeginPointPlacement()
    {
        if (_document?.ActiveCenterStroke == null)
        {
            return;
        }

        _pointPlacementMode = true;
        _canvasView.SetPointPlacementMode(true);
        if (_pointPlacementButton != null)
        {
            _pointPlacementButton.Text = "Cancel Add Point";
        }
        SetStatus("Move along the center stroke and click to place a point.");
    }

    private void TogglePointPlacement()
    {
        if (_pointPlacementMode)
        {
            CancelPointPlacement();
            return;
        }

        BeginPointPlacement();
    }

    private void CancelPointPlacement()
    {
        _pointPlacementMode = false;
        _canvasView?.SetPointPlacementMode(false);
        if (_pointPlacementButton != null)
        {
            _pointPlacementButton.Text = "Add Point";
        }
        SetStatus("Point placement cancelled.");
    }

    private void OnPointPlacementConfirmed(Vector2 documentPosition)
    {
        if (!_pointPlacementMode || _document?.ActiveCenterStroke == null)
        {
            return;
        }

        CenterStrokeElement centerStroke = _document.ActiveCenterStroke;
        CenterStrokePoint point = CreatePointAtCenterStrokePosition(centerStroke, documentPosition);
        CancelPointPlacement();
        centerStroke.Points.Add(point);
        SortCenterStrokePoints(centerStroke);
        centerStroke.NormalizeLocalAxes();
        _canvasView.SelectPoint(centerStroke.Points.IndexOf(point));
        RefreshAll();
        MarkChanged("Center stroke point added.");
    }

    private static CenterStrokePoint CreatePointAtCenterStrokePosition(CenterStrokeElement centerStroke, Vector2 position)
    {
        Vector2 localPosition = centerStroke.Transform.InverseTransformPoint(position);
        if (centerStroke.Points.Count == 0)
        {
            centerStroke.Transform.Position = position;
            return new CenterStrokePoint { X = 0, Y = 0, LeftWidth = 12, RightWidth = 12, HandleMode = centerStroke.SupportsBezierHandles ? PolyTextureHandleMode.Aligned : PolyTextureHandleMode.Linear };
        }

        if (centerStroke.Points.Count == 1)
        {
            CenterStrokePoint onlyPoint = centerStroke.Points[0];
            return new CenterStrokePoint
            {
                X = localPosition.X,
                Y = localPosition.Y,
                LeftWidth = onlyPoint.LeftWidth,
                RightWidth = onlyPoint.RightWidth,
                HandleMode = centerStroke.SupportsBezierHandles ? PolyTextureHandleMode.Aligned : PolyTextureHandleMode.Linear
            };
        }

        float closestDistanceSquared = float.MaxValue;
        float closestLeftWidth = 12.0f;
        float closestRightWidth = 12.0f;
        for (int index = 0; index < centerStroke.Points.Count - 1; index++)
        {
            CenterStrokePoint start = centerStroke.Points[index];
            CenterStrokePoint end = centerStroke.Points[index + 1];
            Vector2 segment = end.Position - start.Position;
            float segmentLengthSquared = segment.LengthSquared();
            float interpolation = segmentLengthSquared <= 0.0001f
                ? 0.0f
                : Mathf.Clamp((localPosition - start.Position).Dot(segment) / segmentLengthSquared, 0.0f, 1.0f);
            Vector2 candidate = start.Position.Lerp(end.Position, interpolation);
            float distanceSquared = candidate.DistanceSquaredTo(localPosition);
            if (distanceSquared < closestDistanceSquared)
            {
                closestDistanceSquared = distanceSquared;
                closestLeftWidth = Mathf.Lerp(start.LeftWidth, end.LeftWidth, interpolation);
                closestRightWidth = Mathf.Lerp(start.RightWidth, end.RightWidth, interpolation);
            }
        }

        return new CenterStrokePoint
        {
            X = localPosition.X,
            Y = localPosition.Y,
            LeftWidth = closestLeftWidth,
            RightWidth = closestRightWidth,
            HandleMode = centerStroke.SupportsBezierHandles ? PolyTextureHandleMode.Aligned : PolyTextureHandleMode.Linear
        };
    }

    private void ClearCenterStroke()
    {
        CenterStrokeElement centerStroke = _document?.ActiveCenterStroke;
        if (centerStroke == null)
        {
            return;
        }

        centerStroke.Points.Clear();
        _canvasView.SelectPoint(-1);
        RefreshAll();
        MarkChanged("Center stroke cleared.");
    }

    private void DeleteSelectedPoint()
    {
        CenterStrokeElement centerStroke = _document?.ActiveCenterStroke;
        int selectedPointIndex = _canvasView?.SelectedPointIndex ?? -1;
        if (centerStroke == null || selectedPointIndex < 0 || selectedPointIndex >= centerStroke.Points.Count)
        {
            SetStatus("No point selected.");
            return;
        }

        centerStroke.Points.RemoveAt(selectedPointIndex);
        centerStroke.NormalizeLocalAxes();
        int nextIndex = centerStroke.Points.Count == 0 ? -1 : Mathf.Clamp(selectedPointIndex, 0, centerStroke.Points.Count - 1);
        _canvasView.SelectPoint(nextIndex);
        RefreshAll();
        MarkChanged("Point deleted.");
    }

    private void DeleteActiveSelection()
    {
        if (_document?.SelectionKind == PolyTextureSelectionKind.Point)
        {
            DeleteSelectedPoint();
            return;
        }

        if (_document?.SelectionKind == PolyTextureSelectionKind.Guide)
        {
            DeleteActiveGuide();
            return;
        }

        if (_document?.SelectionKind == PolyTextureSelectionKind.Output)
        {
            DeleteActiveOutput();
            return;
        }

        if (_document?.SelectionKind == PolyTextureSelectionKind.Element)
        {
            DeleteActiveElement();
            return;
        }

        if (_document?.SelectionKind == PolyTextureSelectionKind.Texture)
        {
            DeleteActiveTexture();
            return;
        }

        SetStatus("No deletable element or point selected.");
    }

    private void DeleteActiveGuide()
    {
        PolyTextureItem texture = _document?.ActiveTexture;
        PolyTextureGuide guide = _document?.ActiveGuide;
        if (texture == null || guide == null)
        {
            SetStatus("No guide selected.");
            return;
        }

        if (!PolyTextureDependencyService.CanDeleteGuide(texture, guide.Id, out string dependencyError))
        {
            SetStatus(dependencyError);
            return;
        }

        texture.Guides.Remove(guide);
        _document.ActiveGuideId = string.Empty;
        _document.SelectionKind = PolyTextureSelectionKind.Texture;
        RefreshAll();
        MarkChanged("Guide deleted.");
    }

    private void DeleteActiveElement()
    {
        PolyTextureItem texture = _document?.ActiveTexture;
        PolyTextureElement element = _document?.ActiveElement;
        if (texture == null || element == null)
        {
            SetStatus("No element selected.");
            return;
        }

        if (!PolyTextureDependencyService.CanDeleteElement(texture, element.Id, out string dependencyError))
        {
            SetStatus(dependencyError);
            return;
        }

        int removedIndex = texture.Elements.IndexOf(element);
        texture.Elements.Remove(element);
        if (texture.Elements.Count > 0)
        {
            int nextIndex = Mathf.Clamp(removedIndex, 0, texture.Elements.Count - 1);
            _document.ActiveElementId = texture.Elements[nextIndex].Id;
            _document.SelectionKind = PolyTextureSelectionKind.Element;
        }
        else
        {
            _document.ActiveElementId = string.Empty;
            _document.SelectionKind = PolyTextureSelectionKind.Texture;
        }

        _document.SelectedPointIndex = -1;
        _canvasView.SelectPoint(-1);
        RefreshAll();
        MarkChanged("Element deleted.");
    }

    private void DeleteActiveOutput()
    {
        PolyTextureItem texture = _document?.ActiveTexture;
        PolyTextureOutputBinding output = _document?.ActiveOutput;
        if (texture == null || output == null)
        {
            SetStatus("No output selected.");
            return;
        }

        texture.Outputs.Remove(output);
        _document.ActiveOutputId = string.Empty;
        _document.SelectionKind = PolyTextureSelectionKind.Texture;
        RefreshAll();
        MarkChanged("Output deleted.");
    }

    private void AddOutput(PolyTextureOutputKind kind)
    {
        PolyTextureItem texture = _document?.ActiveTexture;
        if (texture == null)
        {
            SetStatus("Create a texture before adding an output.");
            return;
        }
        if (kind == PolyTextureOutputKind.Height && texture.Outputs.Exists(output => output.Kind == PolyTextureOutputKind.Height))
        {
            SetStatus("This texture already has a Height output.");
            return;
        }

        PolyTextureEvaluationResult evaluation = PolyTextureEvaluator.Evaluate(texture);
        PolyTextureOutputBinding binding = new()
        {
            Id = EnsureUniqueOutputId(texture, kind == PolyTextureOutputKind.Height ? "height" : "mask"),
            Name = kind == PolyTextureOutputKind.Height ? "Height" : $"Mask {texture.Outputs.FindAll(output => output.Kind == PolyTextureOutputKind.Mask).Count + 1}",
            Kind = kind
        };
        foreach (PolyTextureElement element in texture.Elements)
        {
            if (element.Enabled && !evaluation.HiddenSourceIds.Contains(element.Id) && evaluation.GetGeometry(element.Id).Count > 0)
            {
                binding.SourceElementIds.Add(element.Id);
            }
        }
        if (binding.SourceElementIds.Count == 0)
        {
            SetStatus("The visible graph has no geometry to bind.");
            return;
        }

        texture.Outputs.Add(binding);
        _document.ActiveOutputId = binding.Id;
        _document.ActiveElementId = string.Empty;
        _document.ActiveGuideId = string.Empty;
        _document.SelectedPointIndex = -1;
        _document.SelectionKind = PolyTextureSelectionKind.Output;
        RefreshAll();
        MarkChanged($"{binding.Name} output added.");
    }

    private void FitView()
    {
        _canvasView?.FitView();
        SetStatus("View fitted.");
    }

    private void SortCenterStrokePoints(CenterStrokeElement centerStroke)
    {
        centerStroke.Points.Sort((left, right) => centerStroke.Transform.TransformPoint(left.Position).Y.CompareTo(centerStroke.Transform.TransformPoint(right.Position).Y));
    }

    private string EnsureUniqueTextureId(string baseId)
    {
        string sanitizedBaseId = SanitizeId(baseId);
        string candidate = sanitizedBaseId + "01";
        int suffix = 2;

        while (TextureIdExists(candidate))
        {
            candidate = $"{sanitizedBaseId}{suffix:00}";
            suffix++;
        }

        return candidate;
    }

    private bool TextureIdExists(string id)
    {
        foreach (PolyTextureItem texture in _document.Textures)
        {
            if (texture.Id.Equals(id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private string EnsureUniqueElementId(PolyTextureItem texture, string baseId)
    {
        string sanitizedBaseId = SanitizeId(baseId);
        string candidate = sanitizedBaseId;
        int suffix = 2;

        while (ElementIdExists(texture, candidate))
        {
            candidate = $"{sanitizedBaseId}_{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static bool ElementIdExists(PolyTextureItem texture, string id)
    {
        foreach (PolyTextureElement element in texture.Elements)
        {
            if (element.Id.Equals(id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private string EnsureUniqueGuideId(PolyTextureItem texture, string baseId)
    {
        string sanitizedBaseId = SanitizeId(baseId);
        string candidate = sanitizedBaseId + "01";
        int suffix = 2;
        while (GuideIdExists(texture, candidate))
        {
            candidate = $"{sanitizedBaseId}{suffix:00}";
            suffix++;
        }
        return candidate;
    }

    private static string EnsureUniqueOutputId(PolyTextureItem texture, string baseId)
    {
        string candidate = baseId;
        int suffix = 2;
        while (texture.GetOutput(candidate) != null)
        {
            candidate = $"{baseId}_{suffix}";
            suffix++;
        }
        return candidate;
    }

    private static bool GuideIdExists(PolyTextureItem texture, string id)
    {
        foreach (PolyTextureGuide guide in texture.Guides)
        {
            if (guide.Id.Equals(id, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static string SanitizeId(string value)
    {
        string sanitized = string.Empty;
        foreach (char current in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(current) || current == '_')
            {
                sanitized += current;
            }
            else if (char.IsWhiteSpace(current) || current == '-')
            {
                sanitized += "_";
            }
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "texture" : sanitized;
    }

    private void SetDocument(PolyTextureDocument document, string status, bool resetHistory = true)
    {
        CancelPointPlacement();
        FinishCenterStrokeDrawing();
        CancelSweepCreation(refresh: false);
        CancelMirrorCreation(refresh: false);
        CancelGuideDrawing(clearStatus: false);
        _document = document;
        _document.EnsureSelection();
        if (resetHistory)
        {
            _history.Reset(_document);
        }
        _canvasView.SetDocument(_document);
        _canvasView.RefreshCursor();
        _canvasView.SetHandleView(_handleView);
        _inspector.SetDocument(_document);
        RebuildOutliner();
        RefreshElementActionBar();
        RefreshGlobalSettings();
        SetStatus(status);
        RefreshHeader();
        RefreshHistoryButtons();
    }

    private void UndoDocument()
    {
        if (!_history.CanUndo)
        {
            return;
        }

        PolyTextureDocument restoredDocument = _history.Undo();
        SetDocument(restoredDocument, "Undo.", resetHistory: false);
        _hasUnsavedChanges = true;
        RefreshHeader();
        RefreshHistoryButtons();
    }

    private void RedoDocument()
    {
        if (!_history.CanRedo)
        {
            return;
        }

        PolyTextureDocument restoredDocument = _history.Redo();
        SetDocument(restoredDocument, "Redo.", resetHistory: false);
        _hasUnsavedChanges = true;
        RefreshHeader();
        RefreshHistoryButtons();
    }

    private void OnCanvasEditStarted()
    {
        _deferHistory = true;
    }

    private void OnCanvasEditFinished()
    {
        _deferHistory = false;
        if (_document != null)
        {
            _history.RecordCurrentState(_document);
            RefreshHistoryButtons();
        }
    }

    private void OnSelectedPointChanged(int selectedPointIndex)
    {
        _document.SelectedPointIndex = selectedPointIndex;
        _document.SelectionKind = selectedPointIndex >= 0 ? PolyTextureSelectionKind.Point : PolyTextureSelectionKind.Element;
        _inspector.SetDocument(_document);
        RebuildOutliner();
        RefreshElementActionBar();
    }

    private void OnDocumentChanged()
    {
        _canvasView.SetOutputPreview(null);
        MarkChanged();
        _canvasView.QueueRedraw();
        _inspector.Refresh();
        RebuildOutliner();
        RefreshElementActionBar();
    }

    private void OnInspectorDocumentChanged()
    {
        RebuildOutliner();
        OnDocumentChanged();
    }

    private void OnCanvasCursorChanged(PolyTextureCanvasCursor cursor)
    {
        if (cursor.SnapEnabled && cursor.IsInsideTexture)
        {
            _mousePositionLabel.Text = $"Mouse: X {cursor.DisplayPosition.X:0.##} cm, Y {cursor.DisplayPosition.Y:0.##} cm | Snap: X {cursor.SnappedDisplayPosition.X:0.##} cm, Y {cursor.SnappedDisplayPosition.Y:0.##} cm";
            return;
        }

        _mousePositionLabel.Text = $"Mouse: X {cursor.DisplayPosition.X:0.##} cm, Y {cursor.DisplayPosition.Y:0.##} cm";
    }

    private void RefreshAll()
    {
        RebuildOutliner();
        _canvasView.SetDocument(_document);
        _inspector.SetDocument(_document);
        _canvasView.SetHandleView(_handleView);
        RefreshElementActionBar();
    }

    private void RebuildOutliner()
    {
        if (_outlinerTree == null || _document == null)
        {
            return;
        }

        _refreshingOutliner = true;
        try
        {
            _outlinerTree.Clear();
            TreeItem root = _outlinerTree.CreateItem();
            if (root == null)
            {
                return;
            }

            foreach (PolyTextureItem texture in _document.Textures)
            {
                TreeItem textureItem = _outlinerTree.CreateItem(root);
                if (textureItem == null)
                {
                    continue;
                }

                textureItem.SetCellMode(0, TreeItem.TreeCellMode.Check);
                textureItem.SetChecked(0, texture.Visible);
                textureItem.SetEditable(0, true);
                textureItem.SetSelectable(0, true);
                textureItem.SetText(1, texture.Name);
                textureItem.SetSelectable(1, true);
                textureItem.SetMetadata(0, $"texture|{texture.Id}");
                textureItem.SetMetadata(1, $"texture|{texture.Id}");
                textureItem.Collapsed = false;
                if (_document.SelectionKind == PolyTextureSelectionKind.Texture && texture.Id.Equals(_document.ActiveTextureId, StringComparison.Ordinal))
                {
                    _outlinerTree.SetSelected(textureItem, 1);
                }

                TreeItem elementsGroup = CreateOutlinerGroup(textureItem, "Sources");
                TreeItem generatorsGroup = CreateOutlinerGroup(textureItem, "Generators");
                TreeItem operatorsGroup = CreateOutlinerGroup(textureItem, "Operators");
                TreeItem guidesGroup = CreateOutlinerGroup(textureItem, "Guides");
                TreeItem outputsGroup = CreateOutlinerGroup(textureItem, "Outputs");
                foreach (PolyTextureElement element in texture.Elements)
                {
                    TreeItem parent = generatorsGroup;
                    if (element is CenterStrokeElement or RectangleRegionElement)
                    {
                        parent = elementsGroup;
                    }
                    else if (element is MirrorGeneratorElement)
                    {
                        parent = operatorsGroup;
                    }
                    TreeItem elementItem = _outlinerTree.CreateItem(parent);
                    if (elementItem == null)
                    {
                        continue;
                    }

                    elementItem.SetSelectable(0, false);
                    elementItem.SetText(1, GetOutlinerElementLabel(element));
                    elementItem.SetSelectable(1, true);
                    elementItem.SetMetadata(0, $"element|{texture.Id}|{element.Id}");
                    elementItem.SetMetadata(1, $"element|{texture.Id}|{element.Id}");
                    elementItem.Collapsed = true;
                    if (_document.SelectionKind == PolyTextureSelectionKind.Element
                        && texture.Id.Equals(_document.ActiveTextureId, StringComparison.Ordinal)
                        && element.Id.Equals(_document.ActiveElementId, StringComparison.Ordinal))
                    {
                        _outlinerTree.SetSelected(elementItem, 1);
                    }

                    if (element is CenterStrokeElement centerStroke)
                    {
                        for (int pointIndex = 0; pointIndex < centerStroke.Points.Count; pointIndex++)
                        {
                            TreeItem pointItem = _outlinerTree.CreateItem(elementItem);
                            if (pointItem == null)
                            {
                                continue;
                            }

                            pointItem.SetSelectable(0, false);
                            pointItem.SetText(1, $"Point {pointIndex + 1}");
                            pointItem.SetSelectable(1, true);
                            pointItem.SetMetadata(0, $"point|{texture.Id}|{element.Id}|{pointIndex}");
                            pointItem.SetMetadata(1, $"point|{texture.Id}|{element.Id}|{pointIndex}");
                            if (_document.SelectionKind == PolyTextureSelectionKind.Point
                                && texture.Id.Equals(_document.ActiveTextureId, StringComparison.Ordinal)
                                && element.Id.Equals(_document.ActiveElementId, StringComparison.Ordinal)
                                && pointIndex == _document.SelectedPointIndex)
                            {
                                _outlinerTree.SetSelected(pointItem, 1);
                            }
                        }
                    }
                    else if (element is SweepGeneratorElement sweep)
                    {
                        AddGeneratorInfoItem(elementItem, $"Source: {sweep.SourceElementId}");
                        AddGeneratorInfoItem(elementItem, $"Target: {sweep.TargetElementId}");
                        int instanceCount = sweep.SideMode == PolyTextureSweepSideMode.Both ? sweep.Count * 2 : sweep.Count;
                        AddGeneratorInfoItem(elementItem, $"Instances: {instanceCount}");
                    }
                    else if (element is MirrorGeneratorElement mirror)
                    {
                        AddGeneratorInfoItem(elementItem, $"Source: {mirror.SourceElementId}");
                        AddGeneratorInfoItem(elementItem, $"Axis: {mirror.AxisGuideId}");
                    }
                }

                foreach (PolyTextureGuide guide in texture.Guides)
                {
                    TreeItem guideItem = _outlinerTree.CreateItem(guidesGroup);
                    if (guideItem == null)
                    {
                        continue;
                    }

                    guideItem.SetSelectable(0, false);
                    guideItem.SetText(1, guide.Name);
                    guideItem.SetSelectable(1, true);
                    guideItem.SetMetadata(0, $"guide|{texture.Id}|{guide.Id}");
                    guideItem.SetMetadata(1, $"guide|{texture.Id}|{guide.Id}");
                    if (_document.SelectionKind == PolyTextureSelectionKind.Guide
                        && texture.Id.Equals(_document.ActiveTextureId, StringComparison.Ordinal)
                        && guide.Id.Equals(_document.ActiveGuideId, StringComparison.Ordinal))
                    {
                        _outlinerTree.SetSelected(guideItem, 1);
                    }
                }

                foreach (PolyTextureOutputBinding output in texture.Outputs)
                {
                    TreeItem outputItem = _outlinerTree.CreateItem(outputsGroup);
                    if (outputItem == null)
                    {
                        continue;
                    }
                    outputItem.SetSelectable(0, false);
                    outputItem.SetText(1, $"{(output.Kind == PolyTextureOutputKind.Height ? "Height" : "Mask")}: {output.Name}");
                    outputItem.SetSelectable(1, true);
                    outputItem.SetMetadata(0, $"output|{texture.Id}|{output.Id}");
                    outputItem.SetMetadata(1, $"output|{texture.Id}|{output.Id}");
                    if (_document.SelectionKind == PolyTextureSelectionKind.Output
                        && texture.Id.Equals(_document.ActiveTextureId, StringComparison.Ordinal)
                        && output.Id.Equals(_document.ActiveOutputId, StringComparison.Ordinal))
                    {
                        _outlinerTree.SetSelected(outputItem, 1);
                    }
                    AddGeneratorInfoItem(outputItem, $"Sources: {string.Join(", ", output.SourceElementIds)}");
                }
            }
        }
        finally
        {
            _refreshingOutliner = false;
        }
    }

    private void AddGeneratorInfoItem(TreeItem parent, string text)
    {
        TreeItem item = _outlinerTree.CreateItem(parent);
        if (item == null)
        {
            return;
        }

        item.SetSelectable(0, false);
        item.SetSelectable(1, false);
        item.SetText(1, text);
    }

    private static string GetOutlinerElementLabel(PolyTextureElement element)
    {
        return string.IsNullOrWhiteSpace(element.Name) ? element.Id : element.Name;
    }

    private TreeItem CreateOutlinerGroup(TreeItem parent, string text)
    {
        TreeItem group = _outlinerTree.CreateItem(parent);
        group.SetSelectable(0, false);
        group.SetSelectable(1, false);
        group.SetText(1, text);
        group.Collapsed = true;
        return group;
    }

    private void OnOutlinerItemSelected()
    {
        if (_refreshingOutliner || _document == null)
        {
            return;
        }

        TreeItem selected = _outlinerTree.GetSelected();
        if (selected == null)
        {
            return;
        }

        ApplyOutlinerSelection(selected.GetMetadata(0).AsString());
    }

    private void OnOutlinerItemEdited()
    {
        if (_refreshingOutliner || _document == null)
        {
            return;
        }

        TreeItem item = _outlinerTree.GetEdited();
        int editedColumn = _outlinerTree.GetEditedColumn();
        if (editedColumn != 0)
        {
            return;
        }

        string metadata = item?.GetMetadata(0).AsString() ?? string.Empty;
        if (!metadata.StartsWith("texture|", StringComparison.Ordinal))
        {
            return;
        }

        PolyTextureItem texture = FindTexture(metadata.Split('|')[1]);
        if (texture == null)
        {
            return;
        }

        texture.Visible = item.IsChecked(0);
        _canvasView.QueueRedraw();
        _inspector.SetDocument(_document);
        MarkChanged(texture.Visible ? "Texture shown." : "Texture hidden.");
    }

    private void ApplyOutlinerSelection(string metadata)
    {
        string[] parts = metadata.Split('|');
        if (parts.Length < 2)
        {
            return;
        }

        PolyTextureItem texture = FindTexture(parts[1]);
        if (texture == null)
        {
            return;
        }

        _document.ActiveTextureId = texture.Id;
        _document.ActiveElementId = texture.Elements.Count > 0 ? texture.Elements[0].Id : string.Empty;
        _document.ActiveGuideId = string.Empty;
        _document.ActiveOutputId = string.Empty;
        _document.SelectedPointIndex = -1;
        _document.SelectionKind = PolyTextureSelectionKind.Texture;

        if (parts[0] == "output" && parts.Length >= 3)
        {
            PolyTextureOutputBinding output = texture.GetOutput(parts[2]);
            if (output != null)
            {
                _document.ActiveElementId = string.Empty;
                _document.ActiveOutputId = output.Id;
                _document.SelectionKind = PolyTextureSelectionKind.Output;
            }
        }
        else if (parts[0] == "guide" && parts.Length >= 3)
        {
            PolyTextureGuide guide = texture.GetGuide(parts[2]);
            if (guide != null)
            {
                _document.ActiveElementId = string.Empty;
                _document.ActiveGuideId = guide.Id;
                _document.SelectionKind = PolyTextureSelectionKind.Guide;
            }
        }
        else if (parts.Length >= 3)
        {
            PolyTextureElement element = texture.GetElement(parts[2]);
            if (element != null)
            {
                _document.ActiveElementId = element.Id;
                _document.SelectionKind = PolyTextureSelectionKind.Element;
            }
        }

        if (parts.Length >= 4 && int.TryParse(parts[3], out int pointIndex))
        {
            CenterStrokeElement centerStroke = _document.ActiveCenterStroke;
            if (centerStroke != null && pointIndex >= 0 && pointIndex < centerStroke.Points.Count)
            {
                _document.SelectedPointIndex = pointIndex;
                _document.SelectionKind = PolyTextureSelectionKind.Point;
            }
        }

        _canvasView.SetDocument(_document);
        if (_document.SelectionKind != PolyTextureSelectionKind.Output)
        {
            _canvasView.SetOutputPreview(null);
        }
        _inspector.SetDocument(_document);
        _canvasView.QueueRedraw();
        if (_sweepCreationState != SweepCreationState.None
            && _document.SelectionKind == PolyTextureSelectionKind.Element
            && _document.ActiveElement is CenterStrokeElement selectedCenterStroke)
        {
            AssignSweepCandidate(selectedCenterStroke);
        }
        else if (_mirrorCreationState == MirrorCreationState.AwaitSource
            && _document.SelectionKind == PolyTextureSelectionKind.Element
            && _document.ActiveElement is PolyTextureElement mirrorSource)
        {
            SelectMirrorSource(mirrorSource.Id);
        }
        else if (_mirrorCreationState == MirrorCreationState.AwaitAxis
            && _document.SelectionKind == PolyTextureSelectionKind.Guide
            && _document.ActiveGuide is PolyTextureGuide mirrorAxis)
        {
            SelectMirrorAxis(mirrorAxis.Id);
        }
        RefreshElementActionBar();
        RefreshHeader();
    }

    private PolyTextureItem FindTexture(string textureId)
    {
        foreach (PolyTextureItem texture in _document.Textures)
        {
            if (texture.Id.Equals(textureId, StringComparison.Ordinal))
            {
                return texture;
            }
        }

        return null;
    }

    private void MarkChanged(string status = "Document changed.")
    {
        if (!_deferHistory)
        {
            _history.RecordCurrentState(_document);
        }

        _hasUnsavedChanges = true;
        SetStatus(status);
        RefreshHeader();
        RefreshHistoryButtons();
    }

    private void ApplyGlobalSettings(Action<PolyTextureDocument> apply)
    {
        if (_refreshingGlobalSettings || _document == null)
        {
            return;
        }

        apply(_document);
        _canvasView.RefreshCursor();
        MarkChanged("Global settings changed.");
    }

    private void RefreshGlobalSettings()
    {
        if (_snapCheckBox == null || _snapStepOptionButton == null || _document == null)
        {
            return;
        }

        _refreshingGlobalSettings = true;
        _snapCheckBox.ButtonPressed = _document.SnapEnabled;
        _snapStepOptionButton.Selected = SnapStepToOptionIndex(_document.SnapStepCm);
        _refreshingGlobalSettings = false;
    }

    private static int SnapStepToOptionIndex(float snapStep)
    {
        int closestIndex = 0;
        float closestDistance = float.MaxValue;
        for (int index = 0; index < SnapStepPresets.Length; index++)
        {
            float distance = Mathf.Abs(SnapStepPresets[index] - snapStep);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = index;
            }
        }

        return closestIndex;
    }

    private void RefreshHistoryButtons()
    {
        if (_undoButton != null)
        {
            _undoButton.Disabled = !_history.CanUndo;
        }

        if (_redoButton != null)
        {
            _redoButton.Disabled = !_history.CanRedo;
        }
    }

    private void RefreshHeader()
    {
        if (_activeDocumentLabel == null || _document == null)
        {
            return;
        }

        _refreshingHeader = true;
        if (_documentNameLineEdit != null)
        {
            _documentNameLineEdit.Text = string.Empty;
        }

        string dirtySuffix = _hasUnsavedChanges ? " *" : string.Empty;
        string activeTextureName = _document.ActiveTexture?.Name ?? "-";
        string pathLabel = string.IsNullOrEmpty(_documentPath) ? "Unsaved" : GetPathFileName(_documentPath);
        _activeDocumentLabel.Text = $"{pathLabel} / {_document.Name} / {activeTextureName}{dirtySuffix}";
        _refreshingHeader = false;
    }

    private void SetStatus(string status)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = status;
        }
    }

    private bool IsTextInputFocused()
    {
        Control focusedControl = GetViewport()?.GuiGetFocusOwner();
        return focusedControl is LineEdit or SpinBox;
    }

    private static string GetPathFileName(string path)
    {
        int slashIndex = path.LastIndexOf('/');
        return slashIndex < 0 ? path : path[(slashIndex + 1)..];
    }

    private static string NormalizeDocumentPath(string path)
    {
        return path.EndsWith(".polytexture.json", StringComparison.Ordinal) ? path : $"{path}.polytexture.json";
    }
}
