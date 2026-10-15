# 大厅桌游盒子资源

`Source/` 保存授权提供的 FBX 和三张 PNG。运行资源 `box.cesmodel` 内嵌到插件，分发包只需 DLL；Blender 工程及原始交付 ZIP 保留在本地交付目录。

资源以 FBX 的静态导入结果为准：两个网格、2,292 个三角形。盒盖使用 `BOX_B.png`；盒底材质在 FBX 中没有贴图连接，保留其漫反射颜色。`BOX_S.png` 是附带的其他材质输入，`BOX_upper_B.png` 没有 FBX 连接；两者保留为源文件，不猜测游戏着色器的纹理通道。模型动画不导入。

导出后最长边为 0.4 米，底部中心为坐标原点，保持内部相对比例和位置。位置、旋转和缩放由大厅摆放数据控制；0.2.54 起默认位置与旋转采用已保存的挂墙记录，参数以源码为准。

使用已安装并激活的 Unity 6000.0.62f1 编辑器：

```powershell
.\scripts\BuildLobbyBox.ps1 -UnityEditor "<Unity Editor/Unity.exe>"
.\scripts\Build.ps1
```

脚本在忽略目录内建立独立转换项目，仅使用编辑器导入 FBX 和读取像素；导出压缩二进制网格、法线、UV、索引和 RGBA，不创建跨版本 AssetBundle。运行时有界解码，以已使用的 `LoadRawTextureData(IntPtr, int)` 加载像素，克隆游戏材质，清理自有模型、网格、材质和纹理。

家具测试、交互和位置记录契约见 [大厅盒子](../../docs/LOBBY_BOX.md)。编辑器导出和像素检查不能代替游戏光照、字体、交互或联机验收。
