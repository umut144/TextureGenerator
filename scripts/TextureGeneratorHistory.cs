using System.Collections.Generic;

public sealed class TextureGeneratorHistory
{
    private const int MaxEntries = 100;
    private readonly List<TextureGeneratorDocument> _undoStates = new();
    private readonly List<TextureGeneratorDocument> _redoStates = new();
    private TextureGeneratorDocument _currentState;

    public bool CanUndo => _undoStates.Count > 0;
    public bool CanRedo => _redoStates.Count > 0;

    public void Reset(TextureGeneratorDocument document)
    {
        _undoStates.Clear();
        _redoStates.Clear();
        _currentState = document?.Clone();
    }

    public void RecordCurrentState(TextureGeneratorDocument document)
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

        string currentJson = TextureGeneratorStore.ToJson(_currentState);
        string nextJson = TextureGeneratorStore.ToJson(document);
        if (currentJson == nextJson)
        {
            return;
        }

        AddLimited(_undoStates, _currentState);
        _currentState = document.Clone();
        _redoStates.Clear();
    }

    public TextureGeneratorDocument Undo()
    {
        if (!CanUndo)
        {
            return null;
        }

        AddLimited(_redoStates, _currentState);
        _currentState = TakeLast(_undoStates);
        return _currentState.Clone();
    }

    public TextureGeneratorDocument Redo()
    {
        if (!CanRedo)
        {
            return null;
        }

        AddLimited(_undoStates, _currentState);
        _currentState = TakeLast(_redoStates);
        return _currentState.Clone();
    }

    private static void AddLimited(List<TextureGeneratorDocument> states, TextureGeneratorDocument document)
    {
        states.Add(document.Clone());
        if (states.Count > MaxEntries)
        {
            states.RemoveAt(0);
        }
    }

    private static TextureGeneratorDocument TakeLast(List<TextureGeneratorDocument> states)
    {
        int lastIndex = states.Count - 1;
        TextureGeneratorDocument document = states[lastIndex];
        states.RemoveAt(lastIndex);
        return document;
    }
}
