using System.Collections.Generic;

public sealed class PolyTextureHistory
{
    private const int MaxEntries = 100;
    private readonly List<PolyTextureDocument> _undoStates = new();
    private readonly List<PolyTextureDocument> _redoStates = new();
    private PolyTextureDocument _currentState;

    public bool CanUndo => _undoStates.Count > 0;
    public bool CanRedo => _redoStates.Count > 0;

    public void Reset(PolyTextureDocument document)
    {
        _undoStates.Clear();
        _redoStates.Clear();
        _currentState = document?.Clone();
    }

    public void RecordCurrentState(PolyTextureDocument document)
    {
        if (document == null)
        {
            return;
        }

        if (_currentState == null)
        {
            Reset(document);
            return;
        }

        string currentJson = PolyTextureStore.ToJson(_currentState);
        string nextJson = PolyTextureStore.ToJson(document);
        if (currentJson == nextJson)
        {
            return;
        }

        AddLimited(_undoStates, _currentState);
        _currentState = document.Clone();
        _redoStates.Clear();
    }

    public PolyTextureDocument Undo()
    {
        if (!CanUndo)
        {
            return null;
        }

        AddLimited(_redoStates, _currentState);
        _currentState = TakeLast(_undoStates);
        return _currentState.Clone();
    }

    public PolyTextureDocument Redo()
    {
        if (!CanRedo)
        {
            return null;
        }

        AddLimited(_undoStates, _currentState);
        _currentState = TakeLast(_redoStates);
        return _currentState.Clone();
    }

    private static void AddLimited(List<PolyTextureDocument> states, PolyTextureDocument document)
    {
        states.Add(document.Clone());
        if (states.Count > MaxEntries)
        {
            states.RemoveAt(0);
        }
    }

    private static PolyTextureDocument TakeLast(List<PolyTextureDocument> states)
    {
        int lastIndex = states.Count - 1;
        PolyTextureDocument document = states[lastIndex];
        states.RemoveAt(lastIndex);
        return document;
    }
}
