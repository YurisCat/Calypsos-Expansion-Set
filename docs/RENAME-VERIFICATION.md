# Calypso's Expansion Set 0.2.4 工程与插件验证

日期：2026-10-12。以本项目 0.2.3 的提交 `28818f60ae60fdaa8a5a61e8cf068c6cc7c9f432` 为对照基准。

## 实际执行的检查

- .NET SDK 6.0.428，使用项目的 .NET 6 工具链；官方下载 ZIP 已与发布元数据中的 SHA-512 对照。
- 在最终工作区执行 Release 构建，0 警告、0 错误。Publicizer 配置保留，Nullable 仍开启。
- 读取编译产物的 CLI 元数据，程序集、命名空间、产品名称、版本和插件属性符合下表。
- 9 个源文件按标识调整规则归一化后，与对照提交中的玩法/联网方法体一致。新加的插件元数据是早期框架 GUID 的互斥声明与 Invokyr 进程限制。
- 配置 ID、Mirror assetId、消息号、协议版本、内容清单计算及跳跃效果均保留。
- 部署脚本在独立假目录验证：早期框架测试 DLL 与已有的当前 DLL 得到备份，无关文件未改动，目标 DLL 哈希与构建产物一致。
- PowerShell 脚本语法检查通过。

| 验证项 | 实际值 |
| --- | --- |
| DLL / 程序集 | `CalypsoExpansionSet.dll` / `CalypsoExpansionSet` |
| 主命名空间 | `CalypsoExpansionSet` |
| 插件类 / API | `CalypsoExpansionSetPlugin` / `CalypsoExpansionSetApi` |
| BepInEx 加载名称 / Product | `Calypso's Expansion Set` |
| BepInEx GUID / Harmony ID | `calypso.invokyr.expansionset` |
| 插件版本 / 程序集版本 | `0.2.4` / `0.2.4.0` |
| 早期框架构建互斥 GUID | `mhz.invokyr.moreidols` |
| 目标进程 | `Invokyr.exe` |

最终本机 DLL SHA-256：

```text
6f4091b53cf87852ebf7493655c0225862540929618995e4ecde857b798f0609
```

本次没有启动游戏，也没有把本次构建部署到真实游戏目录。以上检查不等于游戏内或双端联机验收；历史版本的实测范围继续以 `DEVELOPMENT.md` 和 `TODO.md` 为准。

引用早期框架命名空间、DLL 或 GUID 的测试代码需更新引用并重新编译；早期测试构建与当前构建不能同时加载，避免重复注册相同的内容和消息 ID。当前接入方式见 `README.md`。

本机完整编译日志和元数据验证摘要位于 `.local/logs`、`.local/verification`；未将游戏 DLL、SDK、缓存或私人工作路径提交到仓库。
