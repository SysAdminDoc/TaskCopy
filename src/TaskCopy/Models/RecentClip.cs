namespace TaskCopy.Models;

public sealed class RecentClip
{
    public long Id { get; init; }
    public string Body { get; init; } = string.Empty;
    public long CopiedAt { get; init; }

    public string Preview
    {
        get
        {
            if (string.IsNullOrEmpty(Body)) return string.Empty;
            var idx = Body.IndexOfAny(['\r', '\n']);
            var line = (idx < 0 ? Body : Body[..idx]).Trim();
            return line.Length > 80 ? line[..80] + "…" : line;
        }
    }
}
