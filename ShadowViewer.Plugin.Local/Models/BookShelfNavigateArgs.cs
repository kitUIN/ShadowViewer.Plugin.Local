namespace ShadowViewer.Plugin.Local.Models;

/// <summary>
/// 
/// </summary>
/// <param name="IsFolder"></param>
/// <param name="Id"></param>
/// <param name="ParentId"></param>
public record BookShelfNavigateArgs(bool IsFolder, long Id, long ParentId);