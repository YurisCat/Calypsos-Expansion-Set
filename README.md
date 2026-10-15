# Calypso's Expansion Set

Invokyr 的内容扩展 MOD，为游戏添加具有特殊效果的新雕像及可配置的便利功能。版本与更新记录见 [CHANGELOG](CHANGELOG.md)。

## 当前内容

- **弹跳雕像**：持有时自动连续跳跃，伴随随机音效，可能引来怪物。
- **近景雕像**：拿起时镜头平滑放大，放下时平滑恢复视野。
- **声控雕像**：用声音驱动前进，替代键盘与手柄的方向输入，其他动作保留；眼睛待机时红色、允许前进时蓝色，所有玩家可见。
- **倒立雕像**：画面倒立，视角双轴和左右移动反向，前后方向不变；HUD、菜单与鼠标指针正常。
- **鸭步雕像**：走路时随机发出鸭叫，附近玩家可听见，也可能引来怪物。
- **极速雕像**：拿起即以五倍水平速度持续移动，蹲下、跳跃不减速，空中锁定方向，奔跑无效；支持键盘和手柄，菜单中不停，放下恢复。
- **迟钝雕像**：移动方向与松键延迟 0.3 秒，视角和交互即时。
- **接力雕像**：持有越久移动越慢，放下暂停、换人接手恢复；眼睛随疲劳由绿渐红。
- **疑神雕像**：持有者身上随机传出原版怪物声音，附近玩家可听见，也可能引来有听觉的怪物。
- **宽体雕像**：身体变宽、变矮，镜头降低并略微横向拉伸；原版碰撞和过门能力保持。
- **怪相雕像**：每次拿起随机伪装成本局怪物，并短暂预览外观；按使用键（默认左键／RT／R2）表演攻击，无伤害，发声可能引来怪物。
- **像素雕像**：场景与 HUD 一起像素化，打开菜单恢复清晰。
- **描边雕像**：场景与 HUD 变为黑底白线，打开菜单恢复清晰；不透视遮挡物。
- **大头雕像**：他人眼中的角色头部变大，持有者的场景与 HUD 产生强烈鱼眼畸变；菜单正常。

每种 MOD 雕像均带独立手绘便签，指向时显示箴言，名称与文案跟随游戏语言。文案见 [顶部箴言](docs/IDOL_ADVICE.md)，详细玩法与限制见 [设计说明](docs/ARCHITECTURE.md#内置雕像)。

大厅桌游盒子可交互查看介绍，镜头到位后再按交互键关闭，文字与背景渐隐。操作与摆放工具见 [大厅盒子](docs/LOBBY_BOX.md)。

## 安装

需要 Windows x64 版 Invokyr 和 BepInEx 6 IL2CPP。退出游戏后，将 `CalypsoExpansionSet.dll` 放入 `BepInEx/plugins/CalypsoExpansionSet/` 或 `BepInEx/plugins/`，再正常启动游戏。联机各端需安装兼容版本，升级后重开房间。

## 配置与 F8 调试

配置文件为 `BepInEx/config/calypso.invokyr.expansionset.cfg`。

| 配置项 | 默认值 | 用途 |
| --- | --- | --- |
| [Gameplay] NaturalSpawning | true | 让 MOD 雕像参与自然生成，保留原版生成总数 |
| [QualityOfLife] DisableBoardPawnCollision | true | 关闭棋盘进度棋子的实体碰撞；联机建议各端一致 |
| [UI] ShowIndividualIdolNames | false | 开启后显示具体名称；默认 MOD 使用统一名称 |
| [Debug] DebugMode | false | 开启 F8 测试工具和诊断日志 |
| [Debug] WindowKey | F8 | 打开/关闭测试窗口；None 禁用按键 |
| [Debug] TestRoomEnabled | false | 允许专用测试房启动，仍需参数与 DebugMode |

修改配置后重启游戏。普通游玩保持 `DebugMode=false`。

`DebugMode=true` 时按 F8，点击雕像按钮生成，或输入命令后按回车／Run 执行：

| 命令 | 操作 |
| --- | --- |
| `list` / `status` / `help` | 查看生成名、状态与帮助 |
| `spawn <key>` | 在附近生成雕像，如 `spawn jumping`、`spawn miniature`；每种最多一只手动实例 |
| `teleport` | 房主循环传送到存活的 MOD 雕像旁 |
| `teleport <key>` | 房主定位指定种类，例如 `teleport wide_body` |
| `next` | 按随机骰点进入下一小局；先将棋盘放上桌，等待房间生成及事件结束 |
| `revive` | 复活死亡玩家，并为存活受伤玩家补满血 |
| `inspect` | 仅单人测试房：切换角色外观检查视角，再次执行恢复 |
| `visual <key> texture / white / flat` | 比较该类型便签的图案、纯白底色和逐面法线；结束时用 `texture` 恢复 |

改变游戏状态的调试操作仅房主可用；客户端可查询和使用本地诊断。更多工具与详细规则见 [调试与测试房](docs/ARCHITECTURE.md#调试与测试房)。

## 单人测试房

退出当前游戏，在 CFG 中设定：

```ini
[Debug]
DebugMode = true
TestRoomEnabled = true
```

启动时附加 `--ces-test-room`，例如通过 Steam 启动（替换为自己的 Steam 安装目录）：

```powershell
& '<Steam 安装目录>\steam.exe' -applaunch 3883570 --ces-test-room
```

参数与两个开关需同时启用，游戏自动进入单人雕像陈列房，该模式不写入存档。重置展品时退出并用相同参数重启；普通游玩移除参数并关闭 `DebugMode`。详细规则见 [测试房设计](docs/ARCHITECTURE.md#调试与测试房)。

## 构建

使用 .NET SDK 6（版本选择见 `global.json`），并准备游戏的 BepInEx、interop 程序集。默认游戏路径见工程的 `InvokyrPath`，路径一致时运行：

```powershell
.\scripts\Build.ps1
```

游戏路径不同时，指定自己的安装目录；仅编译务必传 `DeployToGame=false`：

```powershell
dotnet build .\CalypsoExpansionSet.csproj -c Release -p:DeployToGame=false '-p:InvokyrPath=D:\Games\Invokyr'
```

`Build.ps1` 仅编译，输出为 `bin/Release/net6.0/CalypsoExpansionSet.dll`。

部署前退出游戏，运行以下脚本后正常启动：

```powershell
.\scripts\Deploy.ps1 -InvokyrPath 'D:\Games\Invokyr'
```

## 开发与验证

- [本地需求工作台](docs/WORKBENCH.md)：方案选择、进度与 [雕像图鉴](docs/WORKBENCH.md#雕像图鉴)。
- [设计说明](docs/ARCHITECTURE.md)：详细玩法、实现与外部插件接入。
- [TODO](TODO.md) 与 [DEVELOPMENT](DEVELOPMENT.md)：当前待办和各版本验证证据。
- [便签说明](Assets/StickyNote/README.md)、[音频说明](Assets/Audio/README.md)：资源制作与导入。
- [Nexus Mods 发布操作](docs/NEXUS_PUBLISH.md)：发布流程。
- [AGENTS](AGENTS.md)：开发约定。
