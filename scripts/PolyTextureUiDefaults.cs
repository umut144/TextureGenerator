using Godot;

public static class PolyTextureUiDefaults
{
    public const double MinimumFineSpinStep = 0.1;
    private const float UiScale = 1.3f;
    private const int BaseFontSize = 16;
    private const int SpinBoxBaseFontSize = 16;

    public static void Apply(Control control)
    {
        int scaledFontSize = Mathf.RoundToInt(BaseFontSize * UiScale);
        control.AddThemeFontSizeOverride("font_size", scaledFontSize);

        if (control is Button or LineEdit or SpinBox)
        {
            control.CustomMinimumSize = new Vector2(
                control.CustomMinimumSize.X,
                Mathf.Max(control.CustomMinimumSize.Y, 32.0f * UiScale));
        }

        if (control is SpinBox spinBox)
        {
            spinBox.Step = NormalizeSpinStep(spinBox.Step);
            int spinBoxFontSize = Mathf.RoundToInt(SpinBoxBaseFontSize * UiScale);
            spinBox.AddThemeFontSizeOverride("font_size", spinBoxFontSize);
            spinBox.GetLineEdit().AddThemeFontSizeOverride("font_size", spinBoxFontSize);
        }

        foreach (Node child in control.GetChildren())
        {
            if (child is Control childControl)
            {
                Apply(childControl);
            }
        }
    }

    public static double NormalizeSpinStep(double requestedStep)
    {
        return requestedStep > 0.0 && requestedStep < MinimumFineSpinStep
            ? MinimumFineSpinStep
            : requestedStep;
    }
}
