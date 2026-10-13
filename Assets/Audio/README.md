# 雕像音效

本目录随源码提供构建需要的全部音效 bank，编译时内嵌到模组 DLL。运行时使用游戏已有的 FMOD Core 系统和监听器播放。

## 拉取后构建

| 文件 | 用途 |
| --- | --- |
| `duck_steps.pcm` | 鸭步的 6 段短叫声 |
| `spring_accent.pcm` | 弹簧大跳的 4 段完整音效 |
| `cuts.json` | 来源、裁剪位置、采样数和 SHA256 |

按项目 README 配好游戏 interop、BepInEx 和 SDK 后，在仓库根目录运行：

```powershell
.\scripts\Build.ps1
```

默认直接使用 `Assets/Audio`，普通构建不需要另取音频包、原始 WAV、Soundly 或 Python。两份 PCM 以二进制形式跟踪，`.gitattributes` 禁止 Git 改写其字节。缺失时应完整同步本目录；高级用法仍可通过 MSBuild 属性 `IdolAudioPath` 覆盖资源目录。

## 重新裁剪或替换

原始素材为：

- `Toys, Misc, Rubber, Squeaky SND29638.wav`
- `Cartoon, Impact, Bonk, Crash, Hits 01 SND11776.wav`

两份输入都是 96 kHz / 24-bit / 双声道 PCM WAV。在仓库根目录运行（Python 3.10+，不依赖第三方包）：

```powershell
python scripts/PrepareIdolAudio.py `
  --duck-source "本机路径\Toys, Misc, Rubber, Squeaky SND29638.wav" `
  --spring-source "本机路径\Cartoon, Impact, Bonk, Crash, Hits 01 SND11776.wav"
.\scripts\Build.ps1
```

脚本下混单声道、降采样为 48 kHz，处理片段边缘与响度，默认更新 `Assets/Audio/duck_steps.pcm`、`spring_accent.pcm` 和带来源/结果 SHA256 的 `cuts.json`。可用 `--preview-dir` 将试听 WAV 输出到自己的本地目录。裁剪起止点在脚本 `RANGES` 中；改动音频契约后提升对应雕像的 `ContentVersion`。

原始完整录音和试听预览由本地工作目录保存，构建只使用本目录的两份 bank。bank 采用项目定义的头部和浮点 PCM 布局，不能仅把其他音频改成 `.pcm` 后替换。只安装已构建 DLL 的玩家不需要本目录。

## 播放与同步

持有者所属客户端报告已发生的原版脚步或弹簧大跳。服务器校验连接、认证、Ready、兼容清单、持有物、角色归属、序号和节流，选择一个片段并避开上一次选择，执行该 bank 可选的服务器回调，再广播一次事件。回调位于请求去重/节流之后，客户端播放不执行回调。客户端只接收当前服务器的事件，拒绝重复/非法序号与越界片段。声音按事件位置在 18 米内衰减，使用原版总音量和音效音量设置；不会给晚加入者重放历史叫声。

同一连接切换不同音效组会重新选择起始片段。原版脚步保留。自 0.2.16 起，鸭步的每次服务器确认声响额外调用原版 `HearingSenseManager.AddVolumeEvent(holder.transform, 18f)`：来源为持有玩家，不限制某个怪物、不忽略距离，寿命和清理由原版管理。18 是原版噪声范围参数，最终是否察觉仍受怪物听觉范围、灵敏度、遮挡与行为规则影响，不是强制索敌半径。音量设置或 DebugMode 不改变此玩法。

弹簧 bank 没有该听觉回调；小跳、普通跳、手动跳和未成功的自动跳均不触发大跳音效。新增听觉行为只属于鸭步，`duck_step.ContentVersion=2`，联机各端需要匹配内容。

音效资源由类型注册生命周期持有；FMOD `OPENMEMORY | OPENRAW | CREATESAMPLE` 同步复制浮点 PCM，创建完成后释放托管 pin。只停止/释放本模组自己的声道和声音，不创建或接管游戏的全局监听器。
