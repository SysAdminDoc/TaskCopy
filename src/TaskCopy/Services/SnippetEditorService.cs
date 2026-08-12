using TaskCopy.Data;
using TaskCopy.Models;

namespace TaskCopy.Services;

/// <summary>
/// Persistence and preview logic for the Settings snippet editor. Keeping the
/// database/history write and placeholder preview together makes editor
/// behavior independently testable and keeps SettingsViewModel focused on
/// binding state.
/// </summary>
public sealed class SnippetEditorService
{
    private readonly SnippetDatabase _db;

    public SnippetEditorService(SnippetDatabase db)
    {
        _db = db;
    }

    public void Save(Snippet snippet)
    {
        _db.Update(snippet.Id, snippet.Title, snippet.Body);
        _db.RecordBodyHistory(snippet.Id, snippet.Body);
    }

    public string Preview(string body)
    {
        if (string.IsNullOrEmpty(body)) return string.Empty;
        try
        {
            var context = new TemplatingContext
            {
                PreviousClipboard = "<clipboard>",
                PromptFor = field => $"<{field}>",
                PromptForMany = fields => fields.ToDictionary(
                    field => field,
                    field => $"<{field}>",
                    StringComparer.OrdinalIgnoreCase),
                Now = DateTime.Now,
            };
            return SnippetTemplating.Expand(body, context).Body;
        }
        catch
        {
            return body;
        }
    }
}
