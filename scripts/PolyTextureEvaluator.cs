public static class PolyTextureEvaluator
{
    public static PolyTextureEvaluationResult Evaluate(PolyTextureItem texture)
    {
        PolyTextureEvaluationResult result = new();
        if (texture == null)
        {
            return result;
        }

        foreach (PolyTextureElement operation in texture.Elements)
        {
            if (!operation.Enabled)
            {
                continue;
            }

            if (operation is SweepGeneratorElement sweep)
            {
                if (!sweep.RenderSource)
                {
                    result.HiddenSourceIds.Add(sweep.SourceElementId);
                }
                result.LiveSweeps.Add(sweep);
            }
            else if (operation is MirrorGeneratorElement mirror)
            {
                if (!mirror.RenderSource)
                {
                    result.HiddenSourceIds.Add(mirror.SourceElementId);
                }
                result.LiveMirrors.Add(mirror);
            }
        }

        return result;
    }
}
