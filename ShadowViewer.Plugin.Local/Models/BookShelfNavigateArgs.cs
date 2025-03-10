namespace ShadowViewer.Plugin.Local.Models;

/// <summary>
/// µ¼º½²ÎÊý
/// </summary>
/// <param name="IsFolder"></param>
/// <param name="Id"></param>
/// <param name="ParentId"></param>
public record BookShelfNavigateArgs(bool IsFolder, long Id, long ParentId);