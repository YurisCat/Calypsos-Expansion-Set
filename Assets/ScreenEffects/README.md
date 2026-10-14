# 最终画面效果

`IdolScreen.shader` 为自制着色器，包含像素化、黑底白线与径向鱼眼三个模式。`idol_screen.bundle` 为 Windows D3D11/D3D12 构建，内嵌到 MOD DLL；普通编译不需要 Unity Editor。

修改着色器后使用安装的 Unity 6 Editor 执行：

```powershell
.\scripts\BuildScreenEffects.ps1 -UnityEditor "<Unity Editor/Unity.exe>"
.\scripts\Build.ps1
```

资源构建在忽略的 `.local/screen-effects-builder` 中运行，使用独立空项目，不更改其他 Unity 项目。`-noUpm` 避免无依赖资源项目启动包管理器；失败检查该目录的 `build.log`。生成后将新的 bundle 与源码一起保留，并提升受影响的已发布类型 ContentVersion。

运行时将内嵌 bundle 按 SHA256 写入 BepInEx 缓存，自行验证已有文件，调用当前游戏保留的 `AssetBundle.LoadFromFileAsync`、`LoadAssetAsync` 原生路径。着色器设置 `DontUnloadUnusedAsset`，避免进入地图时被原版未使用资源回收；最后一个类型资源所有者释放时统一卸载。

画面副本使用 RenderTexture 和当前游戏保留的 CommandBuffer Blit，按窗口尺寸重建。D3D 最终画面的纹理坐标只翻转一次；不得把本通道替换成当前游戏中未保留的 `ScreenCapture` 或材质版 `Graphics.Blit` 恢复包装。着色器和原生调用是否可用必须分别通过实际渲染验证。
