# Calypso's Expansion Set

Invokyr 的非官方扩展模组，基于 Masaicker 开发的 More Idols 继续开发；原作者已允许本项目改名。原提交历史、作者署名和研究记录保留。

当前版本 **0.2.4**。本次是完整的工程与插件身份改名，没有借此宣称新增玩法或完成联机验收。

## 当前内容

- 跳跃雕像：持有时调用原版跳跃入口。
- 可扩展的雕像定义、注册与持有效果 API。
- 自然候选池接入、生成日志和多人兼容握手。
- 可选的 F8 调试窗口与 `list`、`status`、`spawn <key>` 命令。

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

目标为 Windows x64、Unity IL2CPP、BepInEx 6、`net6.0`。主游戏程序集是 `vhs.dll`。保持原工程的 .NET SDK 6 系列，验证工具链为 **6.0.428**；`global.json` 决定 SDK 选择。

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

## 从 More Idols 升级

退出游戏后再替换插件。旧 `MoreIdols.dll` 和新插件不能同时启用；新插件通过 `BepInIncompatibility` 明确拒绝与旧 GUID 一起加载。

```powershell
.\scripts\Deploy.ps1 -InvokyrPath 'D:\Games\Invokyr'
```

部署脚本会把标准位置中的旧 DLL 和已有新 DLL 备份到工作区 `.local/backups`，然后写入 `BepInEx/plugins/CalypsoExpansionSet/CalypsoExpansionSet.dll`，并核对哈希。放在自定义子目录的旧插件需自行移出 `BepInEx/plugins`。

旧配置是 `mhz.invokyr.moreidols.cfg`。需要保留设置时，在游戏关闭且新配置尚不存在的情况下，将旧配置复制为 `calypso.invokyr.expansionset.cfg`；若新配置已有设置，手动合并，不覆盖已有配置。

以后从资源管理器正常启动游戏，不附加脚本测试参数。

## 外部插件接入

依赖旧程序集、命名空间或 GUID 的外部插件需要更新引用并重新编译：

```csharp
using CalypsoExpansionSet;

[BepInDependency(CalypsoExpansionSetPlugin.PluginId)]
// 在自己的 Load 中调用 CalypsoExpansionSetApi.Register(definition)。
```

雕像配置 ID、Mirror assetId、消息号、协议字段和内容版本保持原值，避免把显示与代码名称的调整误当成物品身份迁移；这不等于已经实测新旧版本混合联机。

开发约定见 [AGENTS.md](AGENTS.md)，历史研究见 [DEVELOPMENT.md](DEVELOPMENT.md)。历史文档保留原名称，用于准确指向当时测试的版本。
