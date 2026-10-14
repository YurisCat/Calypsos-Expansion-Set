# Calypso's Expansion Set

Invokyr 的内容扩展 MOD，为游戏添加具有特殊效果的新雕像。版本与更新记录见 [CHANGELOG](CHANGELOG.md)。

## 当前内容

- **跳跃雕像**：持有时按原力度自动连续跳跃，成功自动起跳时随机播放音效。
- **近景雕像**：拿起时镜头平滑放大，放下时平滑恢复视野。
- **声控雕像**：用声音驱动前进，替代键盘与手柄的方向输入，其他动作保留；眼睛待机时红色、允许前进时蓝色，仅持有者可见。
- **倒立雕像**：画面倒立，视角双轴和左右移动反向，前后方向不变；HUD、菜单与鼠标指针正常。
- **鸭步雕像**：走路时配合脚步随机发出短叫声，附近玩家可听见，也会作为噪声进入原版怪物听觉系统。
- **极速雕像**：四倍移动速度，保持最后按下的一个 WASD 方向；松键不停，新方向替换旧方向，放下后恢复，菜单中暂停。
- **迟钝雕像**：移动方向与松键延迟 0.3 秒，视角和交互即时。

弹簧雕像暂时停用，旧实例不会自动转换为跳跃雕像。升级后重开房间。

## 安装

需要 Windows x64 版 Invokyr 和 BepInEx 6 IL2CPP。退出游戏后，将 `CalypsoExpansionSet.dll` 放入 `BepInEx/plugins/CalypsoExpansionSet/`，再正常启动游戏。联机时所有玩家都需安装兼容版本。

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
| `list` / `status` / `help` | 查看类型、状态与帮助 |
| `spawn <key>` | 房主在附近生成雕像，如 `spawn jumping`；可用 key 由 `list` 查看，每种同时最多一只手动实例 |
| `teleport` | 房主循环传送到存活的 MOD 雕像旁 |
| `visual <key> texture / white / flat` | 比较该类型便签的图案、纯白底色和逐面法线；结束时用 `texture` 恢复 |

出售/销毁手动实例后可再次生成，自然实例不占调试限额。窗口开启会暂停原版动作并解锁鼠标，关闭后恢复；在游戏内控制正常时使用生成命令。

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

参数与两个开关必须同时满足。游戏自动开本机单人会话，在初始房门外建立陈列房并将玩家放到前方。房内陈列原版雕像和每种已启用的 MOD 雕像；MOD 展品按 key 字母顺序排列，占用各自调试限额。

测试房禁止远端加入、骰子事件、常规怪物生成和存档写入。取走或销毁的雕像不会自动补刷；重置时退出游戏，用相同参数重启。普通游玩不附加此参数，并关闭 DebugMode。

## 构建

使用 .NET SDK 6（版本选择见 `global.json`），并准备游戏的 BepInEx、interop 程序集。默认游戏路径见工程的 `InvokyrPath`，路径一致时运行：

```powershell
.\scripts\Build.ps1
```

游戏路径不同时，指定自己的安装目录；仅编译务必传 `DeployToGame=false`：

```powershell
dotnet build .\CalypsoExpansionSet.csproj -c Release -p:DeployToGame=false '-p:InvokyrPath=D:\Games\Invokyr'
```

`Build.ps1` 仅编译，输出为 `bin/Release/net6.0/CalypsoExpansionSet.dll`。音效随源码保存在 `Assets/Audio`，依赖 DLL 不随插件分发。

本机部署可在退出游戏后运行以下脚本；它会备份旧插件并核对新 DLL 哈希，完成后正常启动游戏：

```powershell
.\scripts\Deploy.ps1 -InvokyrPath 'D:\Games\Invokyr'
```

## 开发与验证

- [设计说明](docs/ARCHITECTURE.md)：模块、效果、同步与外部插件注册 API；内置效果源码见 [Idols](Idols)。
- [TODO](TODO.md) 与 [DEVELOPMENT](DEVELOPMENT.md)：当前待办和各版本验证证据。多人及完整生命周期仍待验收。
- [便签说明](Assets/StickyNote/README.md)、[音频说明](Assets/Audio/README.md)：资源制作与导入。
- [AGENTS](AGENTS.md)：开发约定。
