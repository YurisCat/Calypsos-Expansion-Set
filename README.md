# Calypso's Expansion Set

Invokyr 的内容扩展 MOD，为游戏添加具有特殊效果的新雕像。

当前版本 **0.2.12**。

版本变化见 [更新记录（CL）](CHANGELOG.md)，详细验收证据见 [DEVELOPMENT.md](DEVELOPMENT.md)。

## 当前内容

- 弹簧雕像（`jumping`）：额头便签显示用户绘制的弹簧图案；持有时约半米小跳，穿插正常跳或略高跳，呈现弹簧玩具的节奏。每次落地后等待 1 秒再跳，放下停止，菜单/F8 开启时暂停。沿用原版跳跃条件和体力处理。
- 可扩展的雕像定义、注册与持有效果 API。
- 自然候选池接入、生成日志和多人兼容握手。
- 可选的 F8 调试窗口与 `list`、`status`、`spawn <key>`、`teleport` 命令。

便签网格和按雕像 key 合成的纹理内嵌在 DLL 中。当前采用模型师的不透明胶带版本，保留原网格法线，未启用交付的高度图和细节法线图。图案用透明 PNG 绘制，通过 `scripts/PrepareNoteArtwork.py` 合成到纸面正面，再编译部署；这不是运行期热加载。使用方法见 [便签资源说明](Assets/StickyNote/README.md)。DebugMode 下还提供 `visual jumping texture|white|flat` 命令，对比已合成图案、纯白底色和纯白逐面法线；这是本端视觉诊断，不改玩法。完成对照后用 `visual jumping texture` 恢复。

`DebugMode=true` 时，房主可在 F8 窗口点击 **Teleport to next MOD idol**，循环传送到当前存活的 MOD 雕像旁边；自然生成和手动生成的实例都可定位。

自然生成、多人同步及其他未完成项目以 [TODO.md](TODO.md) 为准。未来计划加入更多雕像、骰子和生活质量改善。

## 单人测试房

专用启动参数为 `--ces-test-room`，同时需要 CFG 中 `[Debug]` 的 `DebugMode=true` 和 `TestRoomEnabled=true`。本机桌面“测试”快捷方式已配置为通过 Steam 启动，并使用原游戏图标。

该入口停止 Unity Logo，沿原版 KCP 主机流程进入单人场景，在初始房间门外创建明亮的空旷陈列房、打开连接门，将玩家传送到陈列区前方。20 种原版可拿取雕像放在房间后部，两排、每排 10 个，横向间距 2.2 米、排距 3.2 米，包括两种特殊拼图雕像；每只下方使用游戏自带的 `SM_SocleStatue` 基座模型及 `M_SocleStatue` 材质。查找表中另一个 WanderingDice 条目实际是怪物实体，按无怪要求排除。

中部按注册清单的稳定 key 排列 MOD 雕像，每种一只，目前是 `jumping`。以后新增定义、编译部署并重新通过“测试”启动，即可自动出现在中部，不需要为每种内容写陈列分支。不满一排时居中；超过三排时自动向后延长房间，保持与原版区分隔。MOD 陈列实例计入该类型的调试上限，仍存活时 F8 不重复生成；出售/销毁后才可再次手动生成。

测试会话阻止远端加入、骰子事件、常规怪物生成和存档写入，不自动补刷已取走或销毁的雕像。需要重置时退出游戏并重新双击“测试”。顶部仅过滤本地化键 `UI/lobby/waitPlayerBeforeStart` 对应的“棋盘开启后将无法加入游戏”，其他提示与 HUD 保留，未实现的雕像效果说明也不会被这一过滤规则屏蔽。

0.2.7 已完成本机单人启动、20 种陈列、玩家落点、开门状态、Logo 停止和提示过滤的实机检查，以及画面亮度复核；尚未逐种验收全部原版效果、行走往返或多人行为。该测试房限定单人使用。源码/模拟/游戏证据边界详见 [DEVELOPMENT.md](DEVELOPMENT.md)。

0.2.9 本机实测确认中部 MOD 展位生成弹跳雕像，便签在陈列、拿起和放下后可见；持有时有周期跳跃，打开 F8 暂停、关闭后恢复，放下停止。本轮没有重新运行自然探索抽取或真实双端测试，进入候选池仍不保证每局出现。

测试房布局已在 0.2.9 中复核：后部两排原版陈列、游戏原版基座、中部便签雕像及 F8 重复生成限制均保留。新增类型只需完成注册、编译部署并重启测试游戏；当前已运行的房间不会热追加或自动补刷。

0.2.11 根据手感反馈调高小跳、拉长间隔：小跳落地后等待从 0.10 秒增加到 0.45 秒，起跳速度上限从 1.8 调至 2.4；正常跳和稍高跳幅度不变。狐狸节点单人测试房的小跳位置采样多数约 13～15 厘米，正常跳约 1.4 米，稍高跳约 1.7 米；数值只代表该场景观察，不是所有地形/重力条件下的保证。节奏仍为四次小跳后正常跳、五次小跳后稍高跳，循环执行。只在对应的原版起跳调用内临时调整力度，随后恢复；没有各客户端独立抽取随机结果。`jumping` 的 ContentVersion=6，所有联机端须更新兼容内容版本，真实双端仍待验收。

0.2.12 将显示名改为“弹簧雕像”，稳定 key/配置 ID/assetId 保留；小跳目标提高至约 50 厘米，所有节拍落地等待统一为 1 秒，正常/稍高跳的力度不变。增加用户提供的临时便签图案，`ContentVersion=7`。实际校准记录与当前构建的验收边界见 [DEVELOPMENT.md](DEVELOPMENT.md)；旧版本的高度记录不代表当前参数。

## 统一名称

| 用途 | 名称 |
| --- | --- |
| 对外显示 / BepInEx 加载名称 | `Calypso's Expansion Set` |
| 工程 / 解决方案 / 命名空间 | `CalypsoExpansionSet` |
| 输出 DLL | `CalypsoExpansionSet.dll` |
| 插件类 | `CalypsoExpansionSetPlugin` |
| BepInEx GUID / Harmony ID | `calypso.invokyr.expansionset` |
| 配置文件 | `BepInEx/config/calypso.invokyr.expansionset.cfg` |
| 内容注册 API | `CalypsoExpansionSetApi.Register(...)` |

## 构建

目标为 Windows x64、Unity IL2CPP、BepInEx 6、`net6.0`。主游戏程序集是 `vhs.dll`。使用 .NET SDK 6 系列，已验证的工具链为 **6.0.428**；`global.json` 决定 SDK 选择。

游戏路径可以通过命令行属性指定：

```powershell
dotnet build .\CalypsoExpansionSet.csproj -c Release -p:DeployToGame=false '-p:InvokyrPath=D:\Games\Invokyr'
```

也可在不提交的 `.local/Game.local.props` 配置本机路径：

```xml
<Project>
  <PropertyGroup>
    <InvokyrPath>D:\Games\Invokyr</InvokyrPath>
    <DeployToGame>false</DeployToGame>
  </PropertyGroup>
</Project>
```

本机脚本优先使用 `.local/dotnet/dotnet.exe`，没有时使用系统 `dotnet`：

```powershell
.\scripts\Build.ps1
```

这个脚本仅编译，输出为 `bin/Release/net6.0/CalypsoExpansionSet.dll`。BepInEx、Unity、Mirror、游戏 interop 与 Publicizer 工具 DLL 不随插件输出分发。

## 安装与部署

退出游戏后运行部署脚本：

```powershell
.\scripts\Deploy.ps1 -InvokyrPath 'D:\Games\Invokyr'
```

部署脚本会把标准位置中已有的插件 DLL 备份到工作区 `.local/backups`，然后写入 `BepInEx/plugins/CalypsoExpansionSet/CalypsoExpansionSet.dll`，并核对哈希。

部署完成后，从资源管理器正常启动游戏。

## 外部插件接入

外部插件需引用 `CalypsoExpansionSet.dll`，并声明对本插件的依赖：

```csharp
using CalypsoExpansionSet;

[BepInDependency(CalypsoExpansionSetPlugin.PluginId)]
// 在自己的 Load 中调用 CalypsoExpansionSetApi.Register(definition)。
```

雕像配置 ID、Mirror assetId 和消息号必须保持稳定；参与联机的玩家需安装兼容版本及相同的注册内容。

`TemplateId` 选择克隆的配置和外观；可选 `PoolTemplateId` 选择自然池的匹配模板，未指定时沿用 `TemplateId`。两者都使用游戏雕像配置 ID，入池后仍由原版规则筛选和抽取。

## 修改跳跃雕像属性

直接编辑 [Idols/JumpingIdol.cs](Idols/JumpingIdol.cs) 顶部的 `Definition`。第五个参数 `7331884207844971565UL` 是 `TemplateId`，目前从 Score 模板复制基础配置和外观；`PoolTemplateId` 只决定匹配哪些自然池。

文件内已留有带模板值的注释示例：取消 `ConfigureData` 块的注释，只保留需要调整的分值、难度、持有减速或缩放字段；质量等组件属性在 `ConfigurePrefab` 块中调整。没有覆盖的属性继续继承模板。抽取权重可单独设置 `InitialWeight`，不是百分比。

`scoreBasePoint` 是基础分值，实例显示与出售使用的 `CurrentValue` 仍由生成后的计价流程设置。改变基础分值不会固定最终售价。实际修改属性后提升 `ContentVersion`，使用 `scripts/Build.ps1` 重新编译，手动部署并重启游戏；联机各端需更新兼容构建。完整模板属性见 [DEVELOPMENT.md](DEVELOPMENT.md#23-原版配置属性与普通雕像的资源默认值)。

开发约定见 [AGENTS.md](AGENTS.md)，研究与验收记录见 [DEVELOPMENT.md](DEVELOPMENT.md)。
