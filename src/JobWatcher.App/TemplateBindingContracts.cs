namespace JobWatcher.App;

public interface IProfileListItem
{
    string Name { get; }
    string Adapter { get; }
}

public interface ISelectableChoice
{
    string DisplayName { get; }
    bool IsSelected { get; set; }
}

public interface ICategoryGroupHeader
{
    string Category { get; }
}

public interface IRegionGroupHeader
{
    string Region { get; }
}

public interface IZoneGroupHeader
{
    string Zone { get; }
}

public interface IGroupHeader
{
    string Group { get; }
}
