# 便签附件

来自用户提供的模型师交付 `便签.zip`；此目录只包含自制便签的派生资源，不包含原版雕像网格或贴图。

- 源 FBX：`bianqian.fbx`，SHA256 `613da0a3ff533559eba959a89efdd1bb708bcbc1c6efed2e264708d5bce97c2d`。
- `note.mesh.json`：将 FBX 变换烘焙到原版 `SM_IdolHead` 的局部坐标。保留原来的 64 个三角面、角点法线与 UV；为保留分裂法线/UV，每角点独立，共 192 个渲染顶点。坐标转换时同时变换法线并调整三角面绕序，没有修复或重新建模。
- `paper.png`：模型师提供的“不透明胶带”版本底色图，512×512。SHA256 `163b596530a68838c81f656fc49334d72eb942b1b5df4af0b4051ca859cc7824`。作为所有图案的空白底图保留。
- `paper.rgba`：该 PNG 解码后的 RGBA32 数据，每级按自底向上的扫描行保存，包含 512×512 到 1×1 的完整 mip 链，共 1,398,100 字节。游戏内通过已核对的 `LoadRawTextureData(IntPtr,int)` 包装上传并生成 mip，绕开本机 `LoadImage` 的缺失 ReadOnlySpan 包装。PNG 保留为可编辑来源，运行时内嵌 RGBA。
- `Artwork/jumping.png`：用户提供的临时弹簧图案，1024×1024 透明 PNG，保留提交文件的全部字节。SHA256 `fc19f7d1e12a8a200924be243a2217a6546ff96874d1ff2d4eaeab650951a8eb`。
- `Baked/jumping.rgba`：将上述图案合成到纸面正面 UV 后的专属完整 mip 链。`Baked/jumping.json` 记录源图、底图、网格和输出哈希；该派生运行资源随模组编译内嵌，不访问用户工作间。

## 导入图案

每种雕像复用同一便签网格和纸底，透明图案使用其稳定 key。需要 Python 3 和 Pillow；普通 MOD 编译/运行不需要 Python。导入步骤：

```powershell
python scripts/PrepareNoteArtwork.py --key jumping --input "<透明图案 PNG 路径>"
```

脚本保留输入文件，将原图复制为 `Artwork/<key>.png`，合成 `Baked/<key>.rgba` 并写入哈希记录。支持正方形透明 PNG（包括 1024×1024、1080×1080）；如果误导出整张不透明底图会报错。可选 `--preview "<新的预览 PNG 路径>"` 导出完整图集预览，拒绝覆盖已有预览文件。

画布横向映射到 U=0.020310～0.982463；顶部对应 V=0.871263、底部 V=0.010619。不翻转或镜像图案；按当前网格朝向，画布左侧对应头部局部坐标 +X。只合成纸面正面矩形，胶带、背面、折边使用原底图，最终材质保持不透明。透明的是绘画素材，不是纸本身。

定义中使用 `CreateVisuals = prefab => StickyNoteVisuals.Create(prefab, "<key>")`；无图案时可用单参数 `Create` 加载空白纸底。指定图案缺失则注册失败并回滚资源，不静默退回空白。所有客户端随同版 DLL 加载；每种类型拥有独立的材质/纹理生命周期。

更新图案后提升定义的 `ContentVersion`，重新编译、部署并重启游戏。此流程是显式导入工具，不会监控用户文件夹或热替换运行中的纹理。

运行时只在自有 Score 预制体的头部下创建 MeshFilter/MeshRenderer。使用原版已加载 shader 的自有材质副本，保留身体、拾取、碰撞和网络组件。资源内嵌到 DLL，所有客户端随同版 MOD 加载，不依赖本机“人类工作间”路径。

当前接入用于游戏内比较几何和底色：不用雕像原有的物理/法线纹理，也未接入交付的 Height、DirectX normal 或透明胶带版本。原版 shader 的 `_Smoothness` 是自定义 PhysicalMap 纹理，不能按普通粗糙度浮点或未经确认的通道格式传入。

DebugMode 开启时，F8 命令：

- `visual jumping texture`：已合成的弹簧图案＋纸底＋原模型法线，默认。
- `visual jumping white`：纯白底色＋原模型法线，隔离底色图影响。
- `visual jumping flat`：纯白底色＋逐面几何法线，进一步比较法线插值。

诊断仅改变本端该类型的便签显示，不修改原模型文件、其他类型、玩法或网络状态；结束比较后使用 `visual jumping texture` 恢复。便签资源随注册失败回滚或退出会话后的正常卸载清理，活跃房间禁止热卸载。
