# 队友标记资源

`SharedPing.shader` 的三个 pass 分别绘制私有物品遮罩、遮罩外侧的空心轮廓、程序生成的图钉／方向箭头。遮罩使用真实相机的纹理 GPU 投影，不读取场景深度，独立绕过近远裁剪；不修改物品原材质或扫描效果。运行期按当前相机实际执行的 Panini pass 重投影遮罩，轮廓以匹配 LensDistortion 的 Blit UV 全屏采样合成，不做物品局部裁剪。每个物品只合成一次完整轮廓，采用房主最后接受的有效标记者颜色；优先级与独立期限由快照保留。

图钉／箭头只绘制周边区域。地点图钉大小与不透明度由 `PingPresentation` 根据当前镜头距离计算，shader 同时缩放图形与绘制区域，并保持尖端锚定；不透明度作用于彩色主体及暗色外缘。边缘箭头保持固定大小与不透明度。

`shared_pings.bundle` 作为程序集资源发布，不需要玩家额外安装。用已激活的 Unity 编辑器重新生成：

```powershell
.\scripts\BuildPings.ps1 -UnityEditor '<Unity 编辑器>/Editor/Unity.exe'
```

脚本在忽略的 `.local/ping-builder/` 构建 Windows D3D11/D3D12 shader bundle，不部署或启动游戏。运行期沿用已确认的异步文件／资源请求加载路径；缓存使用资源 SHA256 命名并校验。编辑器版本、编译和 GPU 检查证据见 DEVELOPMENT；隔离编辑器通过不等于游戏内通过。
