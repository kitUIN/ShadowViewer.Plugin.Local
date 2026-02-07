using System.Collections.Generic;
using System.Numerics;

namespace ShadowViewer.Plugin.Local.Readers;

/// <summary>
/// 保存渲染引擎的运行状态，包括摄像机位置、缩放、滑动物理参数以及当前布局节点和阅读模式。
/// </summary>
public class EngineState
{
    // 摄像机属性

    /// <summary>
    /// 摄像机在世界坐标系中的中心位置。
    /// </summary>
    public Vector2 CameraPos = Vector2.Zero; 

    /// <summary>
    /// 缩放级别（1.0 表示原始大小）。
    /// </summary>
    public float Zoom = 1.0f;

    // 物理属性

    /// <summary>
    /// 当前滑动速度，用于实现惯性滚动。
    /// </summary>
    public Vector2 Velocity = Vector2.Zero;

    /// <summary>
    /// 摩擦系数，用于每秒衰减速度（参考值）。
    /// </summary>
    public float Friction = 5.0f;

    // 布局信息

    /// <summary>
    /// 当前可见或已布局的渲染节点列表。
    /// </summary>
    public List<RenderNode> LayoutNodes = [];

    /// <summary>
    /// 当前的阅读模式（例如滚动或分页）。
    /// </summary>
    public ReadingMode CurrentMode = ReadingMode.Scroll;
}
