# Calypso's Expansion Set

Invokyr 的内容扩展 MOD，为游戏添加具有特殊效果的新雕像。

当前版本 **0.2.6**。

## 当前内容

- 跳跃雕像：持有时调用原版跳跃入口。
- 可扩展的雕像定义、注册与持有效果 API。
- 自然候选池接入、生成日志和多人兼容握手。
- 可选的 F8 调试窗口与 `list`、`status`、`spawn <key>`、`teleport` 命令。

`DebugMode=true` 时，房主可在 F8 窗口点击 **Teleport to next MOD idol**，循环传送到当前存活的 MOD 雕像旁边；自然生成和手动生成的实例都可定位。

自然生成、多人同步及其他未完成项目以 [TODO.md](TODO.md) 为准。未来计划加入更多雕像、骰子和生活质量改善。

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
