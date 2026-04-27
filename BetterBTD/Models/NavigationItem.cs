namespace BetterBTD.Models;

public sealed class NavigationItem
{
    public NavigationItem(string key, string icon, string title, string description)
    {
        Key = key;
        Icon = icon;
        Title = title;
        Description = description;
    }

    public string Key { get; }
    public string Icon { get; }
    public string Title { get; }
    public string Description { get; }
}
