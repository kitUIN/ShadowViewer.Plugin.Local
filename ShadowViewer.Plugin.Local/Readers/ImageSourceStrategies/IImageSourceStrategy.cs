using System;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;

/// <summary>
/// 表示用于根据不同资源类型加载图像的策略接口。
/// 实现者负责判断是否能处理给定的资源并完成初始化加载流程。
/// </summary>
public interface IImageSourceStrategy
{
    /// <summary>
    /// 判断此策略是否可以处理给定的资源标识。
    /// </summary>
    /// <param name="source">要检查的资源标识（例如文件路径、URI 或其他自定义标识）。</param>
    /// <returns>如果策略可以处理该资源则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    bool CanHandle(object source);

    /// <summary>
    /// 使用指定的加载上下文初始化图像（例如读取字节、设置尺寸等）。
    /// 此方法通常负责准备 <see cref="ImageLoadingContext"/> 中所需的数据，但不负责直接创建 UI 资源。
    /// </summary>
    /// <param name="ctx">包含资源标识和目标尺寸等信息的加载上下文。</param>
    /// <returns>表示异步初始化操作的任务。</returns>
    Task InitImageAsync(ImageLoadingContext ctx);

}
