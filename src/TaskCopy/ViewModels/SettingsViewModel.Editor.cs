using TaskCopy.Models;

namespace TaskCopy.ViewModels;

/// <summary>Editor-specific delegation kept separate from Settings orchestration.</summary>
public partial class SettingsViewModel
{
    private string RenderEditorPreview() => _editor.Preview(EditBody);

    private void PersistEditorChanges(Snippet snippet) => _editor.Save(snippet);
}
