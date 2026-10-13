# Calypso's Expansion Set

Invokyr 的内容扩展 MOD，为游戏添加具有特殊效果的新雕像。当前开发版本 **0.2.14**，更新记录见 [CHANGELOG](CHANGELOG.md)。

## 当前内容

- **跳跃雕像**：持有时自动连续跳跃。
- **弹簧雕像**：持有时有节奏地弹跳，小跳中穿插大跳。
- **近景雕像**：持有时视野变窄，眼前景物放大。

## 配置与 F8 调试

配置文件为 `BepInEx/config/calypso.invokyr.expansionset.cfg`。

| 配置项 | 默认值 | 用途 |
| --- | --- | --- |
| [Gameplay] NaturalSpawning | true | 加入匹配的原版自然候选池，保留原版生成总数 |
| [Debug] DebugMode | false | 开启 MOD 调试模式，启用测试工具和诊断日志；不影响正常玩法 |
| [Debug] WindowKey | F8 | 打开/关闭测试窗口；None 禁用按键 |
| [Debug] TestRoomEnabled | false | 允许专用测试房启动，仍需参数与 DebugMode |

修改 CFG 后重启游戏。普通游玩设 DebugMode=false，关闭测试工具和诊断日志，保留必要状态与异常日志。只关闭 F8 窗口不会关闭调试模式。

DebugMode=true 时按 F8，点击命令输入框输入：

| 命令 | 操作 |
| --- | --- |
| list / status / help | 查看类型、状态与帮助 |
| spawn <key> | 房主在附近生成对应雕像，如 jumping、spring、near_view；每种同时最多一只手动实例 |
| teleport | 房主循环传送到存活的 MOD 雕像旁；也可点击 Teleport to next MOD idol |
| visual <key> texture / white / flat | 比较该类型便签的图案、纯白底色和逐面法线；结束时用 texture 恢复 |

出售/销毁手动实例后可再次生成，自然实例不占调试限额。新类型的列表与命令自动来自注册清单；快捷生成按钮只显示前三种。窗口开启会暂停原版动作并解锁鼠标，关闭后恢复；不要在菜单等控制禁用状态强行生成。

## 单人测试房

退出当前游戏，在 CFG 中设定：

```ini
[Debug]
DebugMode = true
TestRoomEnabled = true
```

启动时附加 `--ces-test-room`。若已配置专用“测试”快捷方式可直接双击；也可以通过 Steam 启动，以下路径替换成自己的 Steam 安装目录：

```powershell
& '<Steam 安装目录>\steam.exe' -applaunch 3883570 --ces-test-room
```

参数与两个开关必须同时满足。游戏自动开本机单人会话、跳过 Logo，在初始房门外建立明亮陈列房并将玩家放到前方。后部是 20 种非怪物原版雕像，中部按注册清单陈列每种 MOD 一只，使用原版石质基座，各占自己的调试限额。MOD 展品按稳定 key 的字母顺序排列，当前为 jumping → near_view → spring；新增类型会按 key 插入相应位置。

测试房阻止远端加入、骰子事件、常规怪物生成和存档写入，只过滤指定的棋盘加入提示。不会自动补刷取走或销毁的雕像；重置时退出游戏，用相同参数重启。普通启动不自动建房。布局与补丁范围见 [设计说明](docs/ARCHITECTURE.md#调试与测试房)，对应版本实际验收见 [DEVELOPMENT](DEVELOPMENT.md)。

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

联机时，所有玩家都需安装兼容版本的 MOD。

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

雕像配置 ID、Mirror assetId 和消息号必须保持稳定。

`TemplateId` 选择克隆的配置和外观；可选 `PoolTemplateId` 选择自然池的匹配模板，未指定时沿用 `TemplateId`。两者都使用游戏雕像配置 ID，入池后仍由原版规则筛选和抽取。

## 调整雕像属性与外观

跳跃雕像编辑 [Idols/JumpingIdol.cs](Idols/JumpingIdol.cs)，弹簧雕像编辑 [Idols/SpringIdol.cs](Idols/SpringIdol.cs)，近景雕像编辑 [Idols/FovIdol.cs](Idols/FovIdol.cs) 的 Definition 与内置 FieldOfView。ConfigureData 修改自有配置，ConfigurePrefab 修改自有预制体，InitialWeight 设置初始相对权重；没有覆盖的属性继承模板。

基础分值不同于实例显示/出售的 CurrentValue，修改 scoreBasePoint 不会固定售价。改变玩法、配置或外观契约后提升 ContentVersion，重新编译、部署并重启，联机各端更新兼容构建。字段含义与资源生命周期见 [类型注册](docs/ARCHITECTURE.md#类型注册)，原版属性证据见 [DEVELOPMENT](DEVELOPMENT.md#23-原版配置属性与普通雕像的资源默认值)。

## 文档分工

| 文档 | 内容 |
| --- | --- |
| [AGENTS](AGENTS.md) | 长期开发约定、禁止事项和工作阅读入口 |
| [当前设计](docs/ARCHITECTURE.md) | 当前模块、注册、效果、同步、资源与调试实现 |
| [TODO](TODO.md) | 最新状态、未完成事项与下一步验收 |
| [CHANGELOG](CHANGELOG.md) | 各版本对用户可见的变化 |
| [DEVELOPMENT](DEVELOPMENT.md) | 研究与验证历史，按当时版本保留证据 |
| [便签说明](Assets/StickyNote/README.md) | 网格/贴图来源、导入工具与视觉诊断 |
