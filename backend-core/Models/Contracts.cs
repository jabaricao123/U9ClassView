namespace ClassView.Backend.Models;

public sealed class DbConnectionProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = ".env";
    public string Server { get; set; } = "";
    public string DatabaseName { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public bool IsDefault { get; set; } = true;
    public bool HasPassword { get; set; }
    public bool KeepPassword { get; set; }
}

public sealed class FavoriteToggleRequest
{
    public string ItemType { get; set; } = "";
    public string ItemKey { get; set; } = "";
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string? ExtraJson { get; set; }
}

public sealed class NoteSaveRequest
{
    public string ItemType { get; set; } = "";
    public string ItemKey { get; set; } = "";
    public string Note { get; set; } = "";
}

public sealed class RecentRecordRequest
{
    public string ItemType { get; set; } = "";
    public string ItemKey { get; set; } = "";
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string? ExtraJson { get; set; }
}

public sealed class RecentClickRequest
{
    public string ItemType { get; set; } = "";
    public string ItemKey { get; set; } = "";
}

public sealed class ClickInfo
{
    public int ClickCount { get; set; }
    public DateTime? LastClickedAt { get; set; }
}
