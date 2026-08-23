using System;
using System.Collections.Generic;

public static class PolyTextureDependencyService
{
    public static List<string> GetElementDependents(PolyTextureItem texture, string elementId)
    {
        List<string> dependents = new();
        if (texture == null || string.IsNullOrEmpty(elementId))
        {
            return dependents;
        }

        foreach (PolyTextureElement candidate in texture.Elements)
        {
            bool referencesElement = candidate switch
            {
                SweepGeneratorElement sweep => sweep.SourceElementId.Equals(elementId, StringComparison.Ordinal)
                    || sweep.TargetElementId.Equals(elementId, StringComparison.Ordinal),
                MirrorGeneratorElement mirror => mirror.SourceElementId.Equals(elementId, StringComparison.Ordinal),
                _ => false
            };

            if (referencesElement)
            {
                dependents.Add(candidate.Id);
            }
        }

        foreach (PolyTextureOutputBinding output in texture.Outputs)
        {
            if (output.SourceElementId.Equals(elementId, StringComparison.Ordinal))
            {
                dependents.Add(output.Id);
            }
        }

        return dependents;
    }

    public static List<string> GetGuideDependents(PolyTextureItem texture, string guideId)
    {
        List<string> dependents = new();
        if (texture == null || string.IsNullOrEmpty(guideId))
        {
            return dependents;
        }

        foreach (PolyTextureElement candidate in texture.Elements)
        {
            if (candidate is MirrorGeneratorElement mirror
                && mirror.AxisGuideId.Equals(guideId, StringComparison.Ordinal))
            {
                dependents.Add(candidate.Id);
            }
        }

        return dependents;
    }

    public static bool CanDeleteElement(PolyTextureItem texture, string elementId, out string error)
    {
        List<string> dependents = GetElementDependents(texture, elementId);
        if (dependents.Count == 0)
        {
            error = string.Empty;
            return true;
        }

        error = $"Cannot delete '{elementId}'; used by {string.Join(", ", dependents)}.";
        return false;
    }

    public static bool CanDeleteGuide(PolyTextureItem texture, string guideId, out string error)
    {
        List<string> dependents = GetGuideDependents(texture, guideId);
        if (dependents.Count == 0)
        {
            error = string.Empty;
            return true;
        }

        error = $"Cannot delete '{guideId}'; used by {string.Join(", ", dependents)}.";
        return false;
    }
}
