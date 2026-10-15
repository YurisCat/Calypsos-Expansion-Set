# 便签附件

来自用户提供的模型师交付 `便签.zip`；此目录只包含自制便签的派生资源，不包含原版雕像网格或贴图。

- 源 FBX：`bianqian.fbx`，SHA256 `613da0a3ff533559eba959a89efdd1bb708bcbc1c6efed2e264708d5bce97c2d`。
- `note.mesh.json`：将 FBX 变换烘焙到原版 `SM_IdolHead` 的局部坐标。保留原来的 64 个三角面、角点法线与 UV；为保留分裂法线/UV，每角点独立，共 192 个渲染顶点。坐标转换时同时变换法线并调整三角面绕序，没有修复或重新建模。
- `paper.png`：模型师提供的“不透明胶带”版本底色图，512×512。SHA256 `163b596530a68838c81f656fc49334d72eb942b1b5df4af0b4051ca859cc7824`。作为所有图案的空白底图保留。
- `paper.note`：所有图案共用的纸底，CSN1 格式头与 zlib 无损压缩。还原为自底向上扫描行、512×512 到 1×1 的完整 RGBA32 mip 链，共 1,398,100 字节；加载后通过已核对的 `LoadRawTextureData(IntPtr,int)` 上传，绕开本机 `LoadImage` 的缺失 ReadOnlySpan 包装。PNG 保留为可编辑来源。
- `Artwork/jumping.png`：用户提供的临时弹簧图案，1024×1024 透明 PNG，保留提交文件的全部字节。SHA256 `fc19f7d1e12a8a200924be243a2217a6546ff96874d1ff2d4eaeab650951a8eb`。
- `Baked/jumping.notedelta`：上述图案合成后与共享纸底的逐字节 XOR 差异，经 zlib 无损压缩；解码时还原原来的完整 mip 链。`Baked/jumping.json` 记录源图、底图、网格、原 RGBA 和压缩输出哈希。原手绘素材保留，派生资源随模组内嵌。
- `TemporaryIcons.json`、`Artwork/todo_*.png`、`Baked/todo_*`：0.2.23 的八种临时图案及 0.2.24 新增的六种图案，来自待办清单的同款 SVG 几何。正式手绘完成前使用；原 `jumping` 图案保留，不覆盖。

## 正式手绘与图鉴裁图

0.2.31 导入十四张正式手绘，`Artwork/handdrawn_*.png` 完整保留原 PNG 字节，`Baked/handdrawn_*` 保存合成资源及哈希。0.2.51 整合远端后，十四种启用内容均使用 `handdrawn_<类型 key>`；怪相沿用远端恢复后的玩法，旧 spring 使用的 `jumping` 素材保持。临时图标留作历史来源，不再内嵌到 DLL。

游戏导入保持原规则：1024×1024 透明绘画画布整体缩放映射到纸面正面，原 512×512 图集的正面矩形为 `(10,66)` 到 `(503,507)`。这是图集坐标，不是图鉴展示的裁剪范围。

图鉴按绘画参考底图的**蓝框内侧**裁剪源画布：1024×1024 坐标 `(105,187)` 到 `(919,919)`，右/下边界不包含，输出 **814×732**。原蓝框为 `(102,184)` 到 `(921,921)` 的三像素线条，预览不包含边框与参考文字；框外墨迹直接裁掉，不缩回框内。该选择按用户指定的蓝框展示范围执行，不能将建议绘图区宣称为精确的三维胶带遮挡蒙版。

`PreviewLayout.json` 保存参考画布、裁剪矩形和纯黄色 `#f7e7a3`；`Previews/handdrawn_*.png` 从透明源 PNG 裁出后合成黄色底，不读取游戏纸张纹理。不同尺寸源画布按比例换算相同选择范围。预览仅供网页使用，不内嵌到游戏，也不改变源绘画、游戏 UV、纸底或运行资源。

```powershell
python scripts/PrepareNoteArtwork.py --key handdrawn_duck_step --input "<透明图案 PNG 路径>"
python scripts/ExportNotePreviews.py --key handdrawn_duck_step
python scripts/ExportNotePreviews.py --key handdrawn_duck_step --verify
```

裁图工具检查源 PNG 与导入元数据哈希，按蓝框裁剪并保留原方向、笔迹颜色和透明边缘；只在导出时需要 Pillow，网页后台仅提供静态图片。二维预览不模拟纸张弯曲、胶带几何遮挡或光照，游戏贴边显示仍需游戏检查。旧版图鉴裁的是整块图集正面，相关历史记录保留，但已由蓝框预览替代。

## 临时图标

历史临时图案使用 `todo_<类型 key>`，当前定义已切换正式手绘。线稿居中放在 1024 画布的建议绘图区，中心为 `(512,554)`；图标范围为 `(212,254)` 至 `(812,854)`，位于模板安全区内。只导出透明线稿，不把模板、虚线或说明文字烘焙到纸面。

`scripts/PrepareTodoNoteIcons.py` 从仓库内的几何清单生成透明 PNG 和 SVG，需要 Pillow、CairoSVG；之后用下述导入脚本生成运行资源。输出目录须为新目录，脚本拒绝覆盖已有图案。普通构建只读取已提交的资源，不依赖本地待办清单或 Python 绘图环境。

```powershell
python scripts/PrepareTodoNoteIcons.py --output "<新的输出目录>"
python scripts/PrepareNoteArtwork.py --key todo_relay --input "<输出目录>/todo_relay.png"
```

正式手绘使用新的图案 key 导入，并修改相应定义的视觉回调、提升 ContentVersion。临时图案与已有手绘来源各自保留，便于核对。

## 导入图案

每种雕像复用同一便签网格和纸底，透明图案使用其稳定 key。需要 Python 3 和 Pillow；普通 MOD 编译/运行不需要 Python。导入步骤：

```powershell
python scripts/PrepareNoteArtwork.py --key jumping --input "<透明图案 PNG 路径>"
```

脚本保留输入文件，将原图复制为 `Artwork/<key>.png`，写入 `Baked/<key>.notedelta` 和哈希记录。先在导入工具中合成完整贴图与所有 mip，再计算相对纸底的差异；运行时无需重新缩放或混合图案。支持正方形透明 PNG（包括 1024×1024、1080×1080）；如果误导出整张不透明底图会报错。可选 `--preview "<新的预览 PNG 路径>"` 导出完整图集预览，拒绝覆盖已有预览文件。

`scripts/NoteTextureFormat.py` 定义编码，`Visuals/NoteTextureCodec.cs` 解码。格式校验版本、解压长度、压缩 payload 哈希、共享纸底哈希与还原 mip 链哈希；缺失、损坏或底图不匹配时注册失败，不降级为空白。单份纸底在本地注册期间解码一次，最后一份便签资源释放时清除缓存；各类型仍分别拥有上传后的完整纹理，减少的是储存和 DLL 大小，不改变 GPU 纹理格式。

需要从现有 PNG 和哈希记录重新打包或核对所有资源时：

```powershell
python scripts/PackNoteResources.py
python scripts/PackNoteResources.py --verify
```

重新打包要求来源及还原后的 mip 哈希与记录一致。修改图案使用导入脚本更新记录；修改纸底需要重新确认全部图案和资源契约。普通编译只读取已有压缩资源，不执行 Python。旧的逐图案完整 `.rgba` 运行文件已迁移，不再重复保存或内嵌。

画布横向映射到 U=0.020310～0.982463；顶部对应 V=0.871263、底部 V=0.010619。不翻转或镜像图案；按当前网格朝向，画布左侧对应头部局部坐标 +X。只合成纸面正面矩形，胶带、背面、折边使用原底图，最终材质保持不透明。透明的是绘画素材，不是纸本身。

定义中使用 `CreateVisuals = prefab => StickyNoteVisuals.Create(prefab, "<图案 key>")`；类型 key 与图案 key 可以不同。带音效或额外资源的类型保留组合资源所有权。当前十四种启用内容均使用各自正式手绘；停用的 spring 不创建资源。无图案时可用单参数 `Create` 加载空白纸底。指定图案缺失则注册失败并回滚资源，不静默退回空白。所有客户端随同版 DLL 加载；每种类型拥有独立的材质/纹理生命周期。

更新图案后提升定义的 `ContentVersion`，重新编译、部署并重启游戏。此流程是显式导入工具，不会监控用户文件夹或热替换运行中的纹理。

运行时只在自有 Score 预制体的头部下创建 MeshFilter/MeshRenderer。使用原版已加载 shader 的自有材质副本，保留身体、拾取、碰撞和网络组件。资源内嵌到 DLL，所有客户端随同版 MOD 加载，不依赖本机“人类工作间”路径。

当前接入用于游戏内比较几何和底色：不用雕像原有的物理/法线纹理，也未接入交付的 Height、DirectX normal 或透明胶带版本。原版 shader 的 `_Smoothness` 是自定义 PhysicalMap 纹理，不能按普通粗糙度浮点或未经确认的通道格式传入。

DebugMode 开启时，F8 命令：

- `visual jumping texture`：对应类型的已合成图案＋纸底＋原模型法线，默认。
- `visual jumping white`：对应类型的纯白底色＋原模型法线，隔离底色图影响。
- `visual jumping flat`：对应类型的纯白底色＋逐面几何法线，进一步比较法线插值。

将 `jumping` 换成其他已注册类型的 key 可检查其便签。

诊断仅改变本端该类型的便签显示，不修改原模型文件、其他类型、玩法或网络状态；结束比较后使用该类型的 `visual <key> texture` 恢复。便签资源随注册失败回滚或退出会话后的正常卸载清理，活跃房间禁止热卸载。
