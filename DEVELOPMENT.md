# Calypso's Expansion Set 开发路线与研究记录

## 2026-10-12：0.2.4 项目配置与验证

当前项目为 **Calypso's Expansion Set 0.2.4**。入口是 `CalypsoExpansionSetPlugin`，程序集与命名空间是 `CalypsoExpansionSet`，BepInEx/Harmony GUID 是 `calypso.invokyr.expansionset`，公开注册入口为 `CalypsoExpansionSetApi.Register`。当前构建、部署和使用说明见 [README.md](README.md)。

早期框架、测试构建与当前内容均属于本项目。下方按开发版本保留研究和验收证据，历史日志、产物路径及测试结果按当时的实际值记录；其中的 `MoreIdols` 仅是早期框架使用的代码或产物标识。文档中的“原版”指 Invokyr 游戏本体。0.2.4 保留了 0.2.3 的跳跃效果、自然池算法、稳定配置 ID、assetId、网络消息号和协议格式。

0.2.4 的 Release 编译、程序集元数据及源码对照检查已完成，范围和结果见 [工程与插件验证记录](docs/RENAME-VERIFICATION.md)；该版本未进行游戏实测。当前事实与待办见 [TODO.md](TODO.md)。

---

## 开发阶段研究与验收记录

研究记录日期：2026-10-12。以下内容按各开发阶段记录实现、研究与验收结果，所述版本和测试入口以对应阶段为准。

**当前结论：雕像属于 Item 体系，核心实体类是 `ScoreTool`。0.2.0 已写入模板克隆注册、持有效果 API、按类型调试、自然候选池接入与多人兼容握手；独立注册和自然生成尚待游戏内验收。** 不同雕像可以复用这些共同流程，具体功能和需要同步的状态仍由效果作者实现。当前项目约定见 [AGENTS.md](AGENTS.md)。

## 1. 0.2.0 阶段状态与证据范围

- [x] 核实实体、配置、效果的继承关系和接口签名。
- [x] 读取原版资源中的雕像配置、预制体引用和管理器默认参数。
- [x] 检查原生代码中的权重读取、抽取和衰减逻辑。
- [x] 配置游戏程序集引用和 Publicizer，并完成构建与 API 引用编译检查。
- [x] 完成正式插件入口、F8 附近生成入口与持有期间自动跳跃实现，并通过 Release 编译。
- [x] 修正本机角色判断，实现服务器测试标识同步、持有者客户端跳跃、晚加入/重连重同步，并配置构建成功后自动部署。
- [x] 在游戏中生成测试雕像，并由小马确认持有期间持续跳跃（0.1.1 单人房主）。
- [x] 增加 cfg 调试开关、重复按键生成及多实例同步；模拟传输验证通过。
- [x] 0.2.0：独立配置/网络预制体注册、公开持有效果 API、三种执行端、自然池及实际生成日志、控制台命令与备用面板。
- [x] 0.2.0：实际同步源码 44 项、效果运行器 26 项、注册器 39 项模拟检查通过，共 109 项。
- [ ] 游戏内验收 0.2.0 独立类型、控制台/面板、自然生成和双端同步。

0.2.0 阶段的工程目标为 `net6.0` / x64，插件版本 `0.2.0`。跳跃内容位于 [Idols/JumpingIdol.cs](Idols/JumpingIdol.cs)，通用定义/注册/执行分别位于 [IdolDefinition.cs](IdolDefinition.cs)、[IdolRegistry.cs](IdolRegistry.cs)、[IdolEffectRuntime.cs](IdolEffectRuntime.cs)，测试及入池位于 [IdolDebugTools.cs](IdolDebugTools.cs)、[IdolSpawnIntegration.cs](IdolSpawnIntegration.cs)。`JumpingIdolSync.cs` 保留历史文件名，现在负责全部类型的兼容握手和服务器调试实例计数；新类型直接使用原版配置 ID、网络 prefab 和物品持有状态识别，不再重复同步一份 marker 集合。

**小马已实测的是 0.1.1 的单人生成和持续跳跃；0.2.0 的编译/模拟检查不能替代 Unity IL2CPP、原版注册与真实联机验收。** 主机和客户端都需要安装兼容版本及相同注册内容；DebugMode 只控制测试入口，客户端不开启也可使用雕像。

本机研究环境：

| 项目 | 已确认值 |
| --- | --- |
| 游戏目录 | `E:\Steam\steamapps\common\Invokyr` |
| BepInEx | `6.0.0-be.788` |
| Unity | `6000.3.14f1` |
| 宿主运行时 | `.NET 6.0.7` |
| 当前项目目标 | `net6.0` |
| 主游戏 API 代理程序集 | `BepInEx\interop\vhs.dll` |

证据分为三类：

1. **代理源码**：确认类型、字段、方法签名和继承关系。`../libs/vhs/` 是 IL2CPP interop 代理，通常只有原生调用包装，不能从这些方法体推断完整游戏逻辑。
2. **序列化资源**：直接读取 Addressables bundle 的类型树，确认配置值和资源引用。
3. **原生指令**：检查 `GameAssembly.dll`，确认部分实际执行逻辑。本文只记录已检查的路径，不将其扩大为所有模式下的行为保证。

这些结论对应本机当前安装版本；游戏更新后需要复查签名、资源、ID 和接入时序。代理源码的限制见 [反编译记录](../libs/DECOMPILATION_NOTES.md)。

### 1.1 当前工程配置、Publicizer 与构建参考

工程配置见 [CalypsoExpansionSet.csproj](CalypsoExpansionSet.csproj)，SDK 选择见 [global.json](global.json)。配置参考 EnhancedControls 的本地 DLL 引用方式，调整为 Invokyr 的程序集与路径。

| 设置 | 当前值与用途 |
| --- | --- |
| 目标框架 / 输出 / 架构 | `net6.0` / `Library` / `x64`。 |
| 程序集名 / 根命名空间 / 版本 | `CalypsoExpansionSet` / `CalypsoExpansionSet` / `0.2.4`。 |
| C# 设置 | 隐式 using 开启；Nullable 开启；`LangVersion = latest`；允许 unsafe。 |
| SDK | 最低选择版本为 `6.0.100`，`rollForward = latestMinor`；本机实际选用 `6.0.428`。`6.0.0` 不是合法的 SDK 版本号。 |
| `InvokyrPath` | 默认 `E:\Steam\steamapps\common\Invokyr`，表示游戏根目录。 |
| `InteropPath` | 默认 `$(InvokyrPath)\BepInEx\interop`。 |
| `BepPath` | 默认 `$(InvokyrPath)\BepInEx\core`。 |
| `PluginPath` | 默认 `$(InvokyrPath)\BepInEx\plugins\CalypsoExpansionSet`，部署 `CalypsoExpansionSet.dll`。 |
| `DeployToGame` | 默认 `true`；实际 Build 成功后自动复制 DLL，Debug / Release 均适用。设计时构建不部署；设为 `false` 可只编译。 |
| 游戏引用 | `vhs.dll`、`vhs.Audio.dll`、`Assembly-CSharp*`、Unity、Mirror、IL2CPP 基础库。主游戏代码在 `vhs.dll`。 |
| 插件引用 | BepInEx Core/Unity Common/Unity IL2CPP、Il2CppInterop Common/Runtime、`0Harmony.dll`。 |
| 本地 DLL 的 `Private` | 全部为 `false`，这些依赖由游戏和 BepInEx 提供，不复制到 MOD 输出目录。 |
| 缺失路径检查 | 构建前检查 `vhs.dll` 和 `BepInEx.Unity.IL2CPP.dll`，缺失时给出明确错误。 |

Publicizer 的关键配置为：

```xml
<PackageReference Include="BepInEx.AssemblyPublicizer.MSBuild" Version="0.4.3"
                  PrivateAssets="all" IncludeAssets="build;contentFiles" />
<Reference Include="$(InteropPath)\vhs.dll" Private="false" Publicize="true" />
```

`PackageReference` 提供构建任务，`Publicize="true"` 明确指定要处理的程序集；仅添加包引用不会自动公开所有游戏依赖。当前处理范围为 `vhs.dll`。需要处理其他程序集时，应在对应的 `Reference` 上明确设置并重新验证。

构建任务在 `obj/<配置>/net6.0/publicized/vhs.dll` 生成公开副本，供编译器使用，同时生成针对 `vhs` 的 `IgnoresAccessChecksToAttribute`。它读取游戏的 interop 程序集，不覆盖游戏安装中的 DLL。`PrivateAssets="all"` 限制包向引用本项目的工程传播；`IncludeAssets="build;contentFiles"` 保留构建任务和属性源码，排除工具 DLL 的编译/运行资产，因此无需将 Publicizer 工具 DLL 随 MOD 分发。

Publicizer 处理托管代理的访问权限；自定义 IL2CPP 类型注入、网络预制体登记和原生虚方法回调，仍需按开发阶段分别验证。

通常使用 `scripts/Build.ps1` 仅编译，再退出游戏后通过 `scripts/Deploy.ps1` 部署。以下命令说明工程自身的自动部署行为；在项目目录运行：

```powershell
dotnet build .\CalypsoExpansionSet.csproj -c Release
```

输出为 `bin\Release\net6.0\CalypsoExpansionSet.dll`，构建成功后自动部署到 `<InvokyrPath>\BepInEx\plugins\CalypsoExpansionSet\CalypsoExpansionSet.dll`。复制失败会让构建报告部署错误，避免误以为游戏已经更新。游戏运行中时，新 DLL 需重启游戏后才会加载；文件被占用时先关闭游戏再构建。游戏位于其他目录时可覆盖根路径，例如：

```powershell
dotnet build .\CalypsoExpansionSet.csproj -c Release '-p:InvokyrPath=D:\Games\Invokyr'
```

也可单独通过 `-p:InteropPath=...` 或 `-p:BepPath=...` 指定目录。路径本身不需要末尾反斜杠。

只编译、不复制到游戏目录：

```powershell
dotnet build .\CalypsoExpansionSet.csproj -c Release -p:DeployToGame=false
```

部署到指定测试目录可以传 `-p:PluginPath=...`。自动部署目标位于 `AfterTargets="Build"`，编译或前置检查失败时不会执行复制；目标只复制本插件 DLL，依赖继续由游戏提供。同一插件只保留一份可被加载的 DLL。

0.1.1 已执行一次默认 Release 构建与自动覆盖，0 警告、0 错误；游戏目录中的 DLL 与构建输出 SHA-256 一致，程序集版本为 `0.1.1.0`。复制完成不代表当前进程已经重新加载，新版游戏行为仍待重启后验证。

0.1.2 同样已完成默认 Release 构建与自动部署，0 警告、0 错误；游戏目录 DLL 与输出 SHA-256 一致，程序集版本为 `0.1.2.0`。重启后先确认 cfg 的 DebugMode，再进行多实例和双端测试。

已完成的构建验证：Release 构建、插件基类/Harmony/IL2CPP 注入/生成 API 引用编译检查，以及读取原本私有的 `ScoreTool.NativeFieldInfoPtr_scoreToolEffect` 的编译检查，均为 0 警告、0 错误。私有字段访问仅用于检查 Publicizer 生效，不作为正式功能代码；编译检查文件和产物位于已忽略的 `obj/ReferenceCheck`、`bin/ReferenceCheck` 中。

基础属性与资源默认值见第 2.3、2.4 节；21 条配置的基础分值见第 3.1 节；玩家旁边生成示例见第 5.3 节；当前跳跃雕像的实现、使用和限制见第 5.4 节；自然生成权重见第 6 节。

## 2. 雕像的“基类”与对象关系

### 2.1 三条继承链

```text
实体：GameplayElement（NetworkBehaviour）
      → EnvironmentElement → Item → Tool → CheatTool → ScoreTool

数据：SOGameplayElement（ScriptableObject）
      → SOItem → SOTool → SOScoreTool

效果：SOToolEffect（ScriptableObject）→ SOScoreToolEffect
```

因此，雕像既是 `Item`，也有更专门的 `ScoreTool`。开发时应优先沿用这一体系，取得拾取、持有、丢弃、价值、皮肤和网络状态等已有能力。

| 对象 | 职责与已确认关系 |
| --- | --- |
| `ScoreTool` | 场景中的雕像实体；有 `CurrentValue`、皮肤、效果引用及相关生命周期接口。 |
| `SOScoreTool` | 雕像配置；自身新增基础分值 `scoreBasePoint`，其他配置继承自父类。 |
| `SOItem.id` | `ulong` 类型的物品配置 ID。 |
| `SOTool.prefab` | 对应实体预制体；提供模型、碰撞和组件组合，持有等行为还需结合配置。 |
| `GameplayElement.gameplayElementData` | 实体所引用的数据配置。 |
| `ScoreTool.scoreToolEffect` | 实体上的 `SOScoreToolEffect` 引用，不能误认为它是 `SOScoreTool` 自身新增的字段。 |

类型依据：[ScoreTool](../libs/vhs/ScoreTool.cs)、[SOScoreTool](../libs/vhs/Items/SOScoreTool.cs)、[SOTool](../libs/vhs/Items/SOTool.cs)、[SOItem](../libs/vhs/Items/SOItem.cs)、[GameplayElement](../libs/vhs/Gameplay_Elements/GameplayElement.cs)。

**找到基类不等于可以用 `new ScoreTool()` 创建完整游戏物品。** 应通过原版配置、预制体和服务器生成流程创建实例。自定义 IL2CPP 派生类、虚方法回调和自定义网络行为还需要单独验证。

### 2.2 原版允许继承，也允许组合组件

- [SlipperyIdol](../libs/vhs/_200_Dev/EnvironmentElements/Player/SlipperyIdol.cs) 和 [MeduzaHead](../libs/vhs/_200_Dev/EnvironmentElements/Player/MeduzaHead.cs) 继承 `ScoreTool`。
- [MusicalIdol](../libs/vhs/Items/MusicalIdol.cs) 继承 `NetworkBehaviour`，通过 `_tool` 引用 `ScoreTool`，说明专有行为也可以由额外组件承担。
- `Bronze`、`Silver`、`Gold`、`Crystal` 是 `ItemDropManager.EIdolSkin` 的皮肤枚举值，应与雕像功能类型分开理解。

第一轮验证可以从现有事件或局部补丁接入行为，暂时无需证明自定义继承路线可用。

### 2.3 原版配置属性与普通雕像的资源默认值

原版已经有配置数据结构：`SOScoreTool : SOTool : SOItem : SOGameplayElement`。它是 ScriptableObject，不需要先为每种雕像重新定义一套数据结构。

下表以 `Data-Treasure-Score` 为基准，值来自序列化资源。**这是该资源的默认配置，不是所有雕像的统一默认值，也不是构造函数默认值或生成后的最终状态。** 完整的 21 条配置、所选根组件及管理器参数保存在 [资源属性快照](research/idol-defaults.json)；该快照未遍历预制体子物体，不能据此认定子物体缺少某组件。

| 配置层 | 属性 | 普通雕像的资源值 |
| --- | --- | --- |
| `SOGameplayElement` | `elementNameTerm` / `actionNameTerm` | 名称词条为空；交互词条为 `UI/Interact/Pickup`。 |
| `SOGameplayElement` | `elementDescription` | `Item/desc/dice1`，是资源中的实际词条。 |
| `SOGameplayElement` | `overrideInteractionRange` / `showCloseInteractionIndicator` | `0` / `false`；未确认 `0` 的运行时回退含义，不能当作不能交互。 |
| `SOGameplayElement` | `_tags` | 标签 ID 列表 `[35]`；标签语义待查。 |
| `SOItem` | `id` | `7331884207844971565UL`。 |
| `SOItem` | `holdSlot` / `holdPose` | `TwoHanded (3)` / `Idol (1)`，双手持有、雕像姿势。 |
| `SOItem` | `localAnchorOverride` / `worldAnchorOverride` | `FromSlot (0)` / `RightHand (1)`。 |
| `SOItem` | `hideFpHands` | `false`。 |
| `SOItem` | `difficultyRange` | `{ min = 1, max = 5 }`；虽名为 `FloatRange`，其两个字段实际为 `int`。 |
| `SOItem` | `useActionState` / `useActionStateHash` | 空字符串 / `0`。 |
| `SOItem` | `useRootTransformWhenReparenting` / `disableNtOnEquip` | `false` / `true`。 |
| `SOTool` | `prefab` | `PF_Treasure_Score`。 |
| `SOItem` / `SOTool` | `icon` / `iconUsed` | 两者指向同一图标资源。 |
| `SOTool` | `isTogglable` / `isToggledByDefault` | `false` / `false`。 |
| `SOTool` | `useMode` | `None (0)`；专有行为还需看效果和组件，不能据此推断完全没有效果。 |
| `SOTool` | `useRequireBoard` / `useRequireHitBoard` / `useRequireNoEvent` | 全部为 `false`。 |
| `SOTool` | `visibilityMethod` / `isHiddenOnEquipOnOwner` | `GameObject (1)` / `false`。 |
| `SOTool` | `unequippedScale` | `1`。 |
| `SOTool` | `localizationMainUseKey` / `localizationSecondaryUseKey` | 两者均为空字符串。 |
| `SOTool` | `useBoundsForSpawn` / `ignoreBoundsCenter` | `false` / `true`。 |
| `SOTool` | `useColliderForSpawn` / `havePivotOnCenter` / `loadInShelf` | `true` / `false` / `true`。 |
| `SOTool` | `twoHandedSlowPower` | `1`；Heavy 为 `1.5`，不能直接解释成速度百分比。 |
| `SOTool` | `canBeSpawnedByEvent` / `eventSpawnChance` | `false` / `1`，不能将后一个值当作普通刷新概率。 |
| `SOTool` | `itemAlias` | `tr_score`。 |
| `SOTool` | `voiceEffect` / `voiceEffectMode` | 空引用 / `None (0)`。 |
| `SOTool` | `audioFeature` / `audioFeatureMode` | 空引用 / `None (0)`。 |
| `SOScoreTool` | `scoreBasePoint` | `1`，是配置基础分值，不能直接认定最终售价为 1。 |

持有变换与包围盒如下，浮点数显示值经过取舍；完整精度见快照：

| 属性 | 普通雕像的资源值 |
| --- | --- |
| 四组单手位置/旋转（本地右手、本地左手、世界右手、世界左手） | 位置均为 `(0, 0, 0)`，旋转均为四元数 `(0, 0, 0, 1)`。 |
| `localPosition2HLocalRig` | `(0.84, -0.155, 0.42)`。 |
| `localRotation2HLocalRig` | 四元数 `(0.952662, -0.203188, -0.179397, 0.137718)`。 |
| `localPosition2HWorldRig` | `(-0.125, 0.022, -0.287)`。 |
| `localRotation2HWorldRig` | 四元数 `(0.606110, 0.373593, -0.298757, 0.635455)`。 |
| `localRotationOffset` | `(0, 0, 0)`。 |
| `meshBounds` | center ≈ `(0, 0.353766, 0.025893)`，extents ≈ `(0.213027, 0.358834, 0.189395)`。 |
| `colliderBounds` | center ≈ `(-0.001047, 0.344613, 0.025139)`，extents ≈ `(0.210966, 0.361800, 0.193160)`。 |

自定义类型的第一版可以克隆这些配置，先保留持有姿势、边界和网络相关设置，再逐项修改。`extents` 是半尺寸；旋转四元数不能当作欧拉角直接填写。

### 2.4 普通雕像预制体的物理与初始状态

| 对象 | 属性 | `PF_Treasure_Score` 的序列化值 |
| --- | --- | --- |
| Rigidbody | mass / linearDamping / angularDamping | `5` / `0` / `0.05`。 |
| Rigidbody | useGravity / isKinematic | `true` / `true`；生命周期可能切换运动学状态，不能据此认定生成后不会落地。 |
| Rigidbody | constraints / interpolation / collisionDetection | 均为 `0`。 |
| BoxCollider | enabled / isTrigger | `true` / `false`。 |
| BoxCollider | size / center | size ≈ `(0.421933, 0.723600, 0.386319)`；center ≈ `(-0.001047, 0.344613, 0.025139)`。 |
| ScoreTool | `scoreToolEffect` | 引用 `Data-ToolEffect-Score`。 |
| ScoreTool | `skin` | `Bronze (1)`，是预制体初值，不能保证每次随机生成都使用青铜。 |
| ScoreTool | `effectActive` / `effectApplied` / `effectIntensity` | `false` / `false` / `0`。 |
| ScoreTool | `usageAllowed` / `isHoldActive` / `isToggled` | `true` / `false` / `false`。 |
| ScoreTool | `_isAvailable` / `_isHoverable` / `_isInteractLocked` | `true` / `true` / `false`。 |
| 效果配置 | `destroyOnExecution` / `timeBeforeDestruction` | `false` / `0`。 |

运行中的 `CurrentValue` 没有出现在这份预制体的序列化字段中，应在生成和生命周期初始化后读取。Heavy 的根 Rigidbody mass 同样为 `5`，已确认的差异之一是配置中的 `twoHandedSlowPower = 1.5`，不能仅根据名称把它实现为增加质量。

## 3. 外观、原版配置与可复用资源

### 3.1 可以直接使用原版预制体

原版 `Item Lookup.scoreTools` 中有 21 条配置，每条都引用预制体。下表是资源实读结果；其中包含 `MissingPiece`、`Final` 等特殊条目，**不能将 21 条都视为普通随机池中的候选**。

| 配置名 | 预制体名 | 配置中的难度范围 | `scoreBasePoint` |
| --- | --- | --- | --- |
| `Data-MissingPiece` | `PF_MissingPiece` | 1–5 | 0 |
| `Data-Treasure-Blind` | `PF_Treasure_Blind` | 1–5 | 7 |
| `Data-Treasure-Camera` | `PF_Treasure_Camera` | 3–5 | 10 |
| `Data-Treasure-DiceThrower` | `PF_Treasure_DiceThrower` | 3–5 | 7 |
| `Data-Treasure-Final` | `PF_Treasure_Final` | 1–5 | 0 |
| `Data-Treasure-Flamethrower` | `PF_Treasure_Flamethrower` | 1–5 | 4 |
| `Data-Treasure-Hallucination` | `PF_Treasure_Hallucination` | 2–5 | 4 |
| `Data-Treasure-Heavy` | `PF_Treasure_Heavy` | 1–5 | 1 |
| `Data-Treasure-InvertedView` | `PF_Treasure_InvertedView` | 1–5 | 1 |
| `Data-Treasure-Lightning` | `PF_Treasure_Lightning` | 2–5 | 7 |
| `Data-Treasure-Linked` | `PF_Treasure_Linked` | 1–5 | 1 |
| `Data-Treasure-Meduza` | `PF_Treasure_Meduza` | 1–5 | 4 |
| `Data-Treasure-Miniature` | `PF_Treasure_Miniature` | 1–5 | 1 |
| `Data-Treasure-Musical` | `PF_Treasure_Musical` | 1–5 | 4 |
| `Data-Treasure-Prankster` | `PF_Treasure_Prankster` | 2–5 | 1 |
| `Data-Treasure-Puppet` | `PF_Treasure_Puppet` | 2–5 | 10 |
| `Data-Treasure-ReverseInputs` | `PF_Treasure_ReverseInputs` | 1–5 | 4 |
| `Data-Treasure-Score` | `PF_Treasure_Score` | 1–5 | 1 |
| `Data-Treasure-Slippery` | `PF_Treasure_Slippery` | 1–5 | 1 |
| `Data-Treasure-Vanishing` | `PF_Treasure_Vanishing` | 2–5 | 4 |
| `Data-Treasure-WanderingDice` | `PF-Treasure_WanderingDice` | 1–5 | 4 |

这些条目引用不同预制体；是否分别使用独立网格、是否共享材质或模型，需要进一步检查，不能仅凭预制体名认定每种外观完全不同。

第一轮不必制作新模型。推荐用 `Data-Treasure-Score` 验证，其当前配置 ID 为：

```csharp
7331884207844971565UL
```

可通过 `SOItemLookup.TryGetScoreTool(ulong id, out SOScoreTool tool)` 查找。运行时需确认查找表已经加载，并检查返回值和 `prefab`，不能只依赖静态资源中存在该条目。

### 3.2 资源证据位置

资源包目录：

```text
E:\Steam\steamapps\common\Invokyr\Invokyr_Data\StreamingAssets\aa\StandaloneWindows64\
```

| 内容 | bundle 文件 | 资源标识 |
| --- | --- | --- |
| `ItemDropManager` 预制体及参数 | `1c9d48c90c1d72c9edf97d635f7176d7.bundle` | Addressables GUID：`45278579f78df8d4fbe04317877ceac4` |
| `Item Lookup` 及配置引用 | `74e8a9e18f190d086b727c44312b5efb.bundle` | Addressables GUID：`4bc87f1524dbccf4882b957bf1a60f18` |

复查定位：管理器 root GameObject pathID 为 `-2548074962865596105`，管理器 MonoBehaviour pathID 为 `-2547964336888973890`；查找表 MonoBehaviour pathID 为 `4066309041710096134`。

此次读取使用 UnityPy 1.21.3，并设置 `UnityPy.config.FALLBACK_UNITY_VERSION = "6000.3.14f1"`。资源带完整类型树；这些配置值来自实际资源，不是根据字段名猜测。

## 4. 原版的登记机制各自负责什么

目前没有找到面向 MOD 作者、一次调用即可完成全部接入的 `RegisterNewIdol` API。已确认存在以下机制：

| 机制 | 职责 | 开发时的含义 |
| --- | --- | --- |
| `SOItemLookup.scoreTools` / `_scoreById` | 配置列表与 ID 查找 | 新配置应能被正确查到；只追加列表可能没有更新已构建的缓存。 |
| `ItemSet.spawnItems` / `ItemSetEntry.items` | 生成候选 | 要自然刷新，需要接入实际使用的生成池。 |
| `ItemSetEntry.ResolveSpawnCount` | 根据难度解析生成数量 | 数量来自 `spawnCountAtMinDifficulty` / `spawnCountAtMaxDifficulty` 等配置，与权重分开。 |
| `EntityManager.RegisterIdol` / `UnRegisterIdol` | 维护 `_idols : HashSet<ScoreTool>` 中的现存实例 | 管理实例，不负责定义新雕像类型。 |
| `ItemDropManager.Register(ItemDropPoint, Item)` | 关联掉落点和物品实例 | 属于掉落管理，不是类型注册。 |
| `Mirror.NetworkClient.RegisterPrefab(GameObject)` | 客户端网络生成预制体登记 | 新网络预制体需要正确处理此路径和 `NetworkIdentity.assetId : uint`。 |

原版生命周期可能已经自动登记实例。开发时应先追踪调用时序，避免手工重复登记。

依据：[SOItemLookup](../libs/vhs/Items/SOItemLookup.cs)、[ItemSet](../libs/vhs/Items/ItemSet.cs)、[ItemSetEntry](../libs/vhs/Items/ItemSetEntry.cs)、[EntityManager](../libs/vhs/_200_Dev/Entities/EntityManager.cs)、[ItemDropManager](../libs/vhs/Items/ItemDropManager.cs)。

## 5. 生成入口与效果接入点

### 5.1 已确认的生成签名

以下为 [ItemDropManager](../libs/vhs/Items/ItemDropManager.cs) 的接口签名参考，尚不是完整插件代码：

```csharp
Item SrvSpawnItem(
    SOTool tool, ItemDropPoint dropPoint,
    Room zoneLoadRoom = null,
    Il2CppSystem.Nullable<int> score = null);

Tool SrvSpawnToolOnGround(
    SOTool toolData, Vector3 position,
    Il2CppSystem.Nullable<Quaternion> rotation = null);

void SrvSpawnItemsForData(
    int seed, ItemSet set,
    Il2CppSystem.Collections.Generic.List<Collider> filters,
    GameEventData gameEventData,
    bool ignoreIdolSpawnTypeCap = false);
```

第一轮可优先尝试 `SrvSpawnToolOnGround`，在玩家附近生成原版雕像；若完整掉落流程依赖掉落点或房间，则改用 `SrvSpawnItem` 并提供相应上下文。两条路线的实际表现均待游戏内验证。

调用条件应包括：服务器端、场景与管理器就绪、配置及预制体可用、位置有效。插件 `Load` 适合设置入口和挂钩，不能据此认定已经可以生成游戏物品。

`PlaceIdolForDebug(ScoreTool idol, Vector3 position)` 接收**已有实例**，不能把它当作创建接口。

### 5.2 已确认的效果接口

[SOToolEffect](../libs/vhs/SOToolEffect.cs) 提供 `SrvExecuteEffect`、`CliExecuteEffect`、`SrvStopEffect`、`CliStopEffect`。

[SOScoreToolEffect](../libs/vhs/SOScoreToolEffect.cs) 提供：

- 服务器侧：`SrvOnStartInternal`、`SrvOnDropInternal`、`SrvOverrideDropPosition`、`SrvOverrideSoldItem`。
- 客户端侧：`CliOnStartInternal`、`CliFixUpdateInternal`、`CliOnDisable`、`CliOnDestroy`。

此外，`ScoreTool` 有 `SrvPickedUp` 静态事件相关入口，参数为 `ScoreTool, ItemDropPoint`。代理暴露了 IL2CPP 委托字段和 `add_SrvPickedUp` / `remove_SrvPickedUp` 方法；订阅方式、触发时序和退订需要实测，不能直接假设与普通托管 C# 事件完全相同。

价值相关接口包含 `CurrentValue` 和 Mirror 生成的 `Network_currentValue`；应核实服务器写入与客户端同步链路。出售入口之一是 [BoardHoldEnvElementWrapper.CmdSellIdol](../libs/vhs/_200_Dev/EnvironmentElements/BoardHoldEnvElementWrapper.cs)，参数为 `ScoreTool, PlayerEntity`。

这些接口说明存在效果与生命周期接入点，尚未证明注入自定义 `SOScoreToolEffect` 后原生虚调用一定会进入托管实现。

### 5.3 测试时直接刷在玩家旁边

**用手动入口指定配置和位置；自然生成权重不参与这一测试。** 当前已实现 F8 快捷键，见第 5.4 节。

已检查 `SrvSpawnToolOnGround` 的原生正常路径：验证服务器和配置、实例化 `toolData.prefab`、设置传入位置及可选旋转、应用 `unequippedScale`，然后调用 Mirror `NetworkServer.Spawn`。它不做类型权重抽取，也没有自动向下检测地面的代码；名称中的 `OnGround` 不能理解为自动找到地面。

建议过程为：取得房主角色 → 计算前方约 1.5 个 Unity 单位的位置 → 向下检测可行走地面 → 处理模型高度偏移 → 调用生成接口 → 记录返回实例及运行时属性。

以下为接入示例，接口与签名已确认，核心调用已通过编译检查，**尚未作为游戏插件加载或运行**。`floorMask` 应由调用者提供，只包含合适的地面层；完整入口还需做空间占用检查、日志和实例管理。

```csharp
using Items;
using Mirror;
using UnityEngine;
using _200_Dev.Entities;

Tool? SpawnTestNearHost(int floorMask)
{
    if (!NetworkServer.active)
        return null;

    var player = EntityManager.Me;
    var manager = ItemDropManager.instance;
    if (player == null || manager == null)
        return null;

    if (!SOItemLookup.TryGetScoreTool(7331884207844971565UL, out var data)
        || data == null || data.prefab == null)
        return null;

    var forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
    var target = player.transform.position + forward * 1.5f;
    if (!Physics.Raycast(target + Vector3.up * 2f, Vector3.down,
        out var hit, 4f, floorMask, QueryTriggerInteraction.Ignore))
        return null;

    var position = hit.point + Vector3.up * (ItemDropManager.GetItemYOffset(data) + 0.05f);
    var rotation = new Il2CppSystem.Nullable<Quaternion>(Quaternion.identity);
    return manager.SrvSpawnToolOnGround(data, position, rotation);
}
```

`EntityManager.Me` 用于房主本机测试；不能假设纯服务器或其他客户端发起请求时它也代表目标玩家。面向其他玩家的生成入口应明确指定服务器上的 `PlayerEntity`。

注意 IL2CPP 参数包装：代理签名把可空旋转的可选值显示为 `null`，但调用包装会对传入的 IL2CPP 对象取指针并拆箱。示例显式创建 `Il2CppSystem.Nullable<Quaternion>`，避免把托管 `null` 直接传进去；若要表示无旋转覆盖，可创建空的 `new Il2CppSystem.Nullable<Quaternion>()`。`SrvSpawnItem` 的可空 `score` 参数也应核实同一问题。

原生定位：`SrvSpawnToolOnGround` VA 为 `0x182EBA580`，`GetItemYOffset` VA 为 `0x182EB2580`。直接生成后的皮肤、价值及登记结果仍需实测；不要假设它与 `SrvSpawnItem` 的完整掉落初始化流程完全相同。

### 5.4 历史实现（0.1.0–0.1.2）：持有期间自动跳跃的测试雕像

**目标：房主开启调试模式后，F8 在附近生成一只雕像，可重复按键生成多只；任意安装同版 MOD 的玩家拿在手里后，每次落地继续请求原版跳跃；放下后停止新增请求。** 当前复用 `Data-Treasure-Score` 的配置、预制体、外观和原版效果，通过服务器同步的 `netId` 集合识别测试实例。它们还不是拥有独立配置 ID 的新雕像类型，也没有进入自然生成池。

#### 实现与原版限制

| 接入点 | 当前用途 |
| --- | --- |
| `BasePlugin.Load` / `AddComponent<JumpingIdolDriver>()` | 注册并添加一个普通 Unity 更新组件到 BepInEx 全局管理对象，记录加载日志。没有替换原版雕像组件或添加自定义网络行为。 |
| `JumpingIdolDriver.Update` | 驱动本机测试逻辑；没有测试实例且未按 F8 时提前返回。 |
| `NetworkServer.active`、`EntityManager.Me`、`PlayerMovement.isOwned` | 生成只能由房主执行；每端只为原版 `Me` 指向且本端拥有移动控制权的角色请求跳跃。**不使用 `PlayerEntity.isLocalPlayer` 判断本机角色。** |
| `ItemDropManager.SrvSpawnToolOnGround` | 通过原版服务器生成路径创建实体；不使用随机权重。 |
| `IsClientIdol(InHand.netId) && InHand.ItemOwner == player` | 同时检查服务器同步的测试标识集合、实际手持对象与原版同步所有者。每端只处理自己的持有者；放下或转交后原持有者停止，接收方确认持有后开始。 |
| `InputsManager.Instance.onJump.enabled`、`Time.timeScale` 与组件状态 | 游戏跳跃输入启用、时间未暂停、本机移动组件有控制权且相关组件启用时，才尝试新增跳跃请求。 |
| `PlayerMovement._isGrounded`、`_jumpRequested`、`PlayerLocomotionController.IsJumping` | 落地后且没有待处理请求或正在跳跃时才调用入口；空中不重复提交请求。 |
| `PlayerLocomotionController.OnJumpInputPerformed_OnceState` | 调用原版跳跃按键处理方法，保留其资格判断和跑步/蹲下状态处理，再由原版提交 `RequestJump`。 |

已核对本机 `GameAssembly.dll` 的原生路径：

```text
OnJumpInputPerformed_OnceState
  → 与 CanJump 相同的资格检查（当前代码中被内联）
  → 检查 PlayerMovement，处理跑步/蹲下状态，设置 IsJumping
  → PlayerMovement.RequestJump
  → 原版物理更新中的 HandleJumpRequest
  → 原版跳跃、落地状态及相关反馈
```

原版资格检查包含跳跃锁、生命状态、正在跳跃状态，以及蹲下时的头顶障碍检查。`HandleJumpRequest` 保留原版请求超时、地面/土狼时间判断与请求消费规则；起跳状态 `PlayerJumpStartState.OnStateEnter` 会调用 `CliUpdateStaminaDiscrete` 处理原版体力扣除，落地状态 `PlayerJumpLandState.OnStateEnter` 会复位跳跃状态。当前实现没有自行修改速度、跳跃力、体力或强制切换状态。**已确认会走原版体力处理，不应因此额外推断“体力不足必定不能跳”：能否起跳仍以本版本原版路径的实际规则为准。**

本版本的原生跳跃按键方法没有读取 `CallbackContext`。但是 interop 将它包装成 IL2CPP 引用类型，代理调用会要求非空对象并拆箱，不能传托管 `default` / `null`。当前只创建一次 `new InputAction.CallbackContext()` 并复用，避免每次跳跃重新分配。游戏更新或其他 MOD 改写此回调后，需要复核是否开始读取上下文。

| 原生方法 | 本机当前版本 VA |
| --- | --- |
| `PlayerLocomotionController.CanJump` | `0x183212B10` |
| `PlayerLocomotionController.OnJumpInputPerformed_OnceState` | `0x183213E10` |
| `PlayerMovement.RequestJump` | `0x182FA2300` |
| `PlayerMovement.HandleJumpRequest` | `0x182F9EEB0` |
| `PlayerJumpStartState.OnStateEnter` | `0x1832106C0` |
| `PlayerJumpLandState.OnStateEnter` | `0x183210540` |

这些地址仅是研究定位，不写入插件；插件通过 interop 调用方法。

#### 本机角色判断修正与多人同步

`EntityManager.Me` 的原生 getter（VA `0x1830ED6F0`）实际取得 `UserInstance.Me.playerEntity`。`UserInstance` 是用户对应的 Mirror 对象，游戏为实际角色单独调用 `NetworkServer.Spawn(..., ownerConnection)` 并分配控制权；拥有实际角色不等于该角色自身是 Mirror 的 `localPlayer`。0.1.0 的 `!player.isLocalPlayer` 检查会错误拒绝已经能移动的房主，0.1.1 已移除，保留原版 `Me` 与移动组件的 `isOwned` 检查。F8 的失败日志现在分别记录服务器、连接、角色、输入、组件和控制权状态。

同步分工如下：

- **服务器权威标识：** 服务器保存所有测试雕像实体及其 `netId`；客户端不能提交或修改测试标识。生成第二只不会覆盖第一只。原版雕像生成、拾取、丢弃、所有者和实体位置同步仍由游戏负责。
- **客户端执行跳跃：** 每端通过原版同步的 `ItemOwner` 和自己的 `InventoryNew.InHand` 确认持有者，只调用本端有控制权的移动角色。其他客户端和服务器不会重复对远端角色起跳；动画、状态和位置继续走原版跳跃网络路径。
- **可靠标识消息：** `JumpingIdolSync` 在 Mirror 可靠通道 `0` 发送单独消息（ID `0x4D49`）。Mirror 头部为 `ushort` 消息 ID；负载为 magic `0x4D49444C`、协议版本 `2`、`byte` 消息种类、`ushort` 实例数量，以及对应数量的 `uint netId`，共 `8 + 4 × 数量` 字节负载。Hello 的数量为 0，Snapshot 的数量最多 128。客户端完整校验长度、数量、非零 ID 及重复 ID 后才替换集合；没有修改原版价值、皮肤或效果字段来承载标识。0.1.1 与 0.1.2 协议不兼容，需要所有端一起更新。
- **握手和晚加入：** 已认证且 Ready 的客户端请求快照；服务器验证真实连接并返回当前标识。新加入、重连或换场景重新 Ready 的客户端重新握手，响应未到时每 5 秒重试，并给出等待提示。快照可以先于实体出现，本地只在实际持有对象匹配后触发。
- **销毁与会话清理：** 服务器观察到实体销毁、`netId` 改变或从 Mirror `spawned` 注销后，移除对应标识并广播剩余集合；全部移除时发送空集合。客户端断线或非 Ready 时立即清除本地标识。房主死亡/角色替换不会删掉仍存活雕像的服务器标记。服务器停止、插件卸载和故障停用时清理状态及自己注册的消息处理器。
- **协议接入保护：** 只给完成握手的客户端发送标识，校验认证、连接身份、负载长度、magic 与协议版本。消息 ID 已被其他处理器占用时停止驱动并报错，不覆盖他人的处理器；断开的连接从订阅列表移除。

主机与客户端需要同版本 MOD 才能验证完整效果；未安装 MOD 的客户端只能看到原版雕像，不会执行自动跳跃。**这些同步路径已经实现并编译，转交、晚加入、重连和远端观察者的运动表现尚未进行双端游戏验证，不能标记为已保证通过。**

#### 生成和清理行为

- 在玩家前方约 `1.5` 单位、上方 `2` 单位处向下投射 `4` 单位；使用 `PlayerMovement.objectsToDetectLayerMask`，忽略触发器，拒绝物品、角色或坡度过大的命中。
- 按 `GetItemYOffset(data) + 0.08` 处理高度，并按配置的碰撞边界检查所有物理层中的非触发器障碍；依次尝试前方及相邻的 9 个位置，避免重复按键把实体堆在同一点。全部受阻时不生成，换到空地后可再按 F8。
- 服务器保存每只实例引用，并在生成日志记录 `netId`、位置、`CurrentValue` 和当前测试数量。重复按 F8 可生成多只，默认同时存活最多 16 只，由 cfg 调整；普通自然生成的同款雕像不受影响。0.1.1 的单实例限制是插件测试代码主动设置，0.1.2 已移除。
- 放下后停止新增请求，再次拿起同一实例恢复。已经由原版接受的那次跳跃会自然完成，插件不取消原版请求或截断空中运动。
- 放下、死亡或本机角色失效时停止本机请求；实例销毁及会话结束时清理同步标识。卸载时销毁本插件的更新组件并注销消息处理器；已生成实体继续由游戏管理。
- 未预期异常会记录一次并停用测试驱动，避免每帧重复异常日志；修复后重启游戏。

#### 0.1.2 框架测试步骤（历史记录）

以下命令、产物名称和配置对应当时的框架构建；当前操作见 README.md。

1. 执行 `dotnet build .\MoreIdols.csproj -c Release`。
2. 默认构建已自动覆盖 `<InvokyrPath>\BepInEx\plugins\MoreIdols.dll`。如果禁用了部署，再手动复制 DLL。客户端也要安装同版本；不需要复制游戏引用或 Publicizer 工具 DLL。
3. 首次加载 0.1.2 后生成 cfg；退出游戏，将第 5.5 节的 `DebugMode` 改为 `true`，再重启。作为房主进入可自由移动的场景；日志应包含 0.1.2 的加载信息、`DebugMode=True` 和 `Jumping idol markers synchronized`，生成前数量为 0。
4. 在空地按 F8，确认附近生成一个普通外观雕像，日志出现 `Jumping test idol spawned`。重复按键应继续生成，并保留之前每只的跳跃效果；若附近位置全部被占用，移动后再按。按住按键不会逐帧生成。
5. 拿起 F8 生成的雕像，不按跳跃键，观察每次落地后是否继续正常跳跃，动画、音效和体力处理是否与手动跳跃一致。
6. 放下或出售雕像，观察当前跳跃自然完成后是否停止；再次拿起未销毁的同一实例应恢复。拾取自然生成的同款普通雕像，应没有自动跳跃。
7. 验证蹲下且头顶受阻、禁止移动/跳跃的状态、暂停/菜单、死亡/复活、销毁雕像及退出重开；记录现象与日志。
8. 双端安装 0.1.2，房主生成至少两只，分别由房主和客户端拿起；确认各自能跳跃，转交时只有当前持有者自动跳跃，双方看到的雕像、运动和动画一致。晚加入或断线重连应收到包含全部存活实例的快照。销毁一只不影响其余实例；全部销毁后快照数量为 0。客户端无需开启 DebugMode 就能使用房主生成的雕像；单人房主也通过同一握手/快照路径。

**验证状态：小马已确认 0.1.1 生成和持续跳跃成功；本机日志记录了 `netId=77`、`205` 的生成、持有、放下及销毁标识清理，生成时 `CurrentValue=0`。出售计价、体力边界与双端表现尚未确认。0.1.2 Release 编译通过，0 警告、0 错误；使用实际 `JumpingIdolSync.cs` 加模拟 Mirror 传输的 37 项检查通过，覆盖多实例保留、晚加入、重连、单实例清理、空快照、认证/连接筛选、非法快照拒绝和容量边界。该验证不替代真实游戏网络与物理测试。**

### 5.5 历史配置（0.1.2）：cfg 调试模式与重复生成

配置路径：`<InvokyrPath>\BepInEx\config\mhz.invokyr.moreidols.cfg`。插件首次加载时由 BepInEx 自动生成，默认 DebugMode 为 false。测试时退出游戏，修改后重启：

```ini
[Debug]
DebugMode = true
SpawnKey = F8
MaxTestIdols = 16
```

| 字段 | 默认值 | 用途 |
| --- | --- | --- |
| `DebugMode` | `false` | 控制房主调试生成入口；不控制已生成雕像的跳跃效果。客户端可保持 false。 |
| `SpawnKey` | `F8` | Unity Input System 的 `Key` 名称，例如 F7、F8；None 关闭按键。每按下一次生成一只，按住不连刷。 |
| `MaxTestIdols` | `16` | 同时存活的测试实例上限，允许 1–128；销毁或出售并注销的实例释放名额。与自然生成的每类型限制无关。 |

目前只有“跳跃”一种自制效果，所以重复生成的是该效果的多个实例；尚未实现不同 MOD 类型之间的轮换。手动生成直接指定配置与位置，不经过自然权重抽取。附近 9 个位置都被占用时应移动到空地继续测试，而不是让实体互相穿插。

### 5.6 历史状态（0.1.2）：模块化程度与游戏自带控制台

当前有插件入口、Unity 更新驱动和独立同步模块，但**尚不是可供其他作者注册新雕像的框架**。生成配置 ID、手持效果和跳跃调用仍固定在插件内，同步集合也只识别“跳跃测试实例”，没有携带通用效果类型 ID。新增另一种效果仍需要修改现有接入逻辑，不能声称只调用注册接口就能完成。

下一步适合先加入第二种真实内容，再提炼共同的配置、生成、实例标识/类型、持有生命周期与同步流程。各效果保留独立实现；建立稳定接口后才开放给其他作者。独立配置 ID、查找表缓存、网络预制体注册和自然生成池仍按阶段 3–4 验证。

本机存在 `BepInEx\interop\EnhancedFramework.DeveloperConsole.dll`，已核实：

- `EnhancedFramework.DeveloperConsoleSystem.DeveloperConsole` 提供 `Open()`、`Close()`、`RunCommand(string)`、`AddCommand(DeveloperConsoleCommand)`、`GetCommand(...)` 和 `IsOpen`。
- 控制台有 `OpenConsoleInput` 输入资源及命令初始化路径；游戏主程序集包含 `OnDevelperConsoleGameState_ManageInputs`，用于控制台游戏状态下管理输入。
- `ItemDropManager` 包含 `TeleportIdolsToGameRoom`、`DumpIdolWeights` 等调试方法；代理方法存在不代表已确认对应的输入命令名称。

这说明游戏带有控制台基础设施；正式版是否启用、实际快捷键、命令名称和托管 MOD 命令注册仍未做游戏内验证。因此当前直接测试采用上述 cfg + 按键入口，没有依赖未验证的控制台命令。

### 5.7 注册与模块化方式（0.2.0 引入）

现在是供本项目继续添加内容的轻量公共接入层，支持 **从模板创建独立雕像类型 + 持有期间效果**。下一个持有效果无需改同步模块、控制台命令或自然生成补丁；添加定义并注册即可。出售触发、地面光环、自定义网络状态、独立模型资源加载等仍需对应的实现和验证，当前 API 没有自动实现这些能力。

以下示例使用当前的程序集和 API 名称：

```csharp
// 本项目内容在 CalypsoExpansionSetPlugin.Load 注册。
// 外部 BepInEx 插件声明 [BepInDependency(CalypsoExpansionSetPlugin.PluginId)]，在自己的 Load 注册。
CalypsoExpansionSetApi.Register(new IdolDefinition(
    "my_idol", "My Idol",
    ConfigId: 0x4D49000000000100UL, // 示例；每个作者必须选用自己的稳定且不冲突的 ID
    AssetId: 0x4D490100U,
    TemplateId: 7331884207844971565UL,
    Authority: IdolEffectAuthority.ServerGameplay,
    CreateEffect: () => new MyHeldEffect())
{
    ContentVersion = 1,
    ConfigureData = data => data.scoreBasePoint = 20,
    ConfigurePrefab = prefab => { /* 只修改自己的外观；保留原版网络/拾取组件 */ }
    // InitialWeight 不指定时使用原版默认；通常无需调整。
});
```

效果继承 `HeldIdolEffect`，实现 `Tick(HeldIdolContext context)`；可选 `Start` 和 `Dispose`。运行器按持有者/物品处理开始与清理，转交、死亡、放下、断线或场景未就绪后停止旧效果。每次创建独立效果实例，勿把可变状态放在所有雕像共享的静态字段。`GameplayReady=false` 时不新增玩法动作；本地表现也需处理菜单/暂停和退出时的恢复。

| 执行端 | 用途 | 同步责任 |
| --- | --- | --- |
| `LocalPresentation` | 仅当前持有者的画面/声音干扰 | 表现本身可不广播；物品和持有身份仍来自原版权威状态，释放时恢复自己的修改。 |
| `OwnerNativeGameplay` | 跳跃等所有者动作 | 持有者有控制权的客户端调用原版入口，运动/动画沿用原版同步；房主不能再重复调用。 |
| `ServerGameplay` | 价值、血量、生成/销毁、跨玩家状态 | 服务器执行，再调用原版网络 setter/RPC；声明执行端不会自动同步普通字段。 |

当前跳跃雕像的 key 为 `jumping`，配置 ID 为 `0x4D49000000000001UL`，assetId 为 `0x4D490001U`；模板仍为 `Data-Treasure-Score`（`7331884207844971565UL`），初始属性和外观继承模板。配置、网络预制体和原版同款已经分开；原版同款不因同模型而获得跳跃效果。具体模板基础属性仍见第 4 节及 `research/idol-defaults.json`。

注册器维护 `SOItemLookup.scoreTools` 与 `_scoreById`、检查 key/ID/assetId 冲突、克隆自有 SO/预制体并连接 `gameplayElementData`，网络表重建后幂等登记。首次初始化后锁定定义；活跃房间内禁止热卸载，避免存活物品引用被销毁的配置。修改玩法、配置或外观契约时提升 `ContentVersion`；类型清单包含 ID、模板、版本和执行端，不会自动散列代码或整个资源。

### 5.8 历史测试入口（0.2.0）：内置控制台与备用选择面板

首次运行生成或更新 `BepInEx/config/mhz.invokyr.moreidols.cfg`。退出游戏后设置并重启：

```ini
[Debug]
DebugMode = true
SpawnKey = F8
PanelKey = F9
ConsoleKey = F10
TestIdol = jumping
MaxTestIdols = 16

[Gameplay]
NaturalSpawning = true
```

`DebugMode` 默认 false；其余默认值如上。**16 是全部 MOD 类型同时存活的调试实例合计，不是种类数量，不是每个类型 16，也不限制自然生成。** 当前只注册跳跃一种；新增内容后所有测试类型共用配额。手动生成不受自然权重/每类型 2 个限制；每次操作只生成一只，没有自动循环。

| 入口 | 操作 |
| --- | --- |
| F8 | 房主在可移动、地面有空间的位置生成当前选定类型。 |
| F9 备用面板 | PageUp/PageDown 切换类型，F8 生成一只；选择写回 `TestIdol`。无需解锁鼠标，面板不改变游戏本身的输入状态。 |
| F10 内置控制台 | MOD 调用游戏 `DeveloperConsole.Open/Close`；当前场景缺失控制台 UI 时显示备用面板。F10 是 MOD 默认键，并非已确认的原版默认键。 |
| `mi.list` | 列出所有注册类型、执行端和各自生成命令。 |
| `mi.status` | 显示注册/握手状态、全部类型的调试实例合计和自然生成开关。 |
| `mi.spawn.jumping` | 房主在身边生成一只跳跃雕像；其他内容自动拥有 `mi.spawn.<key>` 命令。 |

命令只在 DebugMode 开启时登记，不覆盖占用的同名命令。生成命令会关闭控制台，等待原版输入恢复后再调用；玩家/会话变化或 2 秒内输入未恢复则取消，避免在菜单中或对旧角色生成。只在房主执行物品创建；客户端可使用同步的雕像。原版控制台 API 已从程序集核实，具体 UI 在正式版中的实际表现仍需实测；备用面板不依赖它。

### 5.9 自然生成、小局与日志

已核实地图初始生成链：

```text
ZoneManager.SrvDoInitialSpawn
  → 当前事件 ID、seed、ItemSet、区域 collider
  → ItemDropManager.SrvSpawnItemsForData
  → 原版筛选、权重抽取、类型计数、皮肤/计价与掉落点
  → SrvSpawnItem
```

0.2.0 在这次 `SrvSpawnItemsForData` 开始前复制本次 ItemSet 和候选列表，在含模板的 entry 中追加 MOD 配置。保留 entry 的生成数量、难度/事件/seed、原版筛选和初始化；不向 guaranteed bonus 列表追加内容，也不永久改共享地图资源。原生空候选 fallback 继续从本次 ItemSet 的其他 entry 选取；本次研究未发现它直接用 `SOItemLookup.scoreTools` 构建全部候选。关闭自然生成时，本次列表会排除本框架已注册的 MOD 类型。

小马说的小局，通常在骰子确认造成新地图/区域生成后走这条链；**保留地图、恢复已有地图等事件不保证重新生成，不能按每次确认骰子计数。** 因此日志以真实生成批次、事件 ID、seed 为准。

“每种最多 2 只”具体指：**本次自然生成调用中，某一种最多被选中两次**（资源默认值 2）。下一次新生成批次重新计数；即使背包/棋盘上仍保留上次两只，也不等于以后不能再刷。事件可能忽略上限；手动生成也不走这项限制。它不是权重，也不是整个大局累计最多两只。**如果一个骰子小局触发多个独立生成调用，各批次分别计数，整个小局合计可能超过 2；已经核实的是生成方法的局部计数，不能扩大为整个骰子轮次的统一上限。**

| 日志 | 含义 |
| --- | --- |
| `[Definition]` | 托管内容定义已登记；此时不一定已加载模板。 |
| `[Register]`、`[Registry] ready` | 自有配置/预制体和查找接入，类型清单可用于握手。 |
| `[Compatibility]` | 服务器/客户端的协议和内容清单验证。 |
| `[Pool] batch=…, eventId=…, seed=…` | 本次加入的 MOD 候选、当前权重、原版上限与事件是否忽略上限；这里的候选尚未经过原版全部条件筛选。 |
| `[Natural spawn]` | 本批次实际生成的 MOD 实例，含类型、netId、价值。 |
| `[Batch result]` | 本批次实际 MOD 类型/数量汇总；`[]` 表示未刷出，`completed=false` 表示原版/补丁调用未完整结束。 |
| `[Debug spawn]` | 手动生成和全部类型合计调试配额，与自然批次分开。 |

加入池不保证每个小局出现；出现频率仍受条件和权重衰减影响。默认 `NaturalSpawning=true`；所有雕像都使用独立类型 ID 参与原版类型计数和动态权重。可选自定义初始权重只在会话首次准备或原版 `ResetIdolWeights` 时应用，不逐帧/每批次重置。

### 5.10 多人准入与本轮验证边界

独立 assetId 需要所有客户端提前登记。协议 v3 在原版 Ready 前发送内容清单握手，服务端暂缓 `SetClientReady`，匹配后恢复原版场景物品发送；不匹配拒绝连接，缺失握手 30 秒后拒绝。未就绪的新连接不会阻塞正在进行的兼容玩家生成；如果出现已 Ready 却不兼容的连接，阻止新的 MOD 自然/调试生成。Host 共享自己的注册资源，单人仍使用原版网络生成流程。

新类型的晚加入恢复依赖原版 prefab/config ID、物品所有者、生成与 SyncVar；额外自定义的数值或状态仍需作者提供网络字段/快照。效果运行器不会替作者同步任意字段。

编译和模拟检查已完成：

- `JumpingIdolSync.cs` 实际源码 + 模拟 Mirror：44 项，覆盖 Ready 前握手、待注册、缺失/错误版本与清单、身份伪造、错误包、重连/连接 ID 复用、缺失 MOD 超时、跨类型调试总数、自然实例不占配额、实例清理和会话重开。
- `IdolEffectRuntime.cs` 与 `IdolDefinition.cs` 实际源码 + 模拟游戏状态：26 项，覆盖所有者/服务器执行分离、持有/转交/死亡/复活、原版同款不受影响、控制禁用、断线/未就绪清理、异常隔离、重新拾取、服务器效果中生成物品及重复卸载清理。
- `IdolRegistry.cs` 实际源码 + 模拟原生资源：39 项，覆盖两类型独立注册、原版资源不改动、列表/cache/prefab 一致、重复初始化、表重建/替换、类型清单顺序与版本、ID/handler 冲突、非法属性、配置回调破坏身份、附件失败回滚和只清理自有资源。它不证明 Unity 克隆、Awake 和 Mirror 原生实现。

本机模拟程序位于 `obj/SyncVerification`、`obj/EffectVerification`、`obj/RegistryVerification`，属于未提交的研究/验证产物；游戏验收仍是下一步。

未实测：Unity 原生克隆/Awake 时序、网络预制体登记、实际控制台/面板、自然抽取和出售计价、真实双端入场/持有/转交/晚加入。先按以下顺序验收：

1. 重启游戏，确认 `0.2.0 loaded`、`[Register] jumping`、`[Registry] ready`；没有注册异常。
2. DebugMode 开启；F9 查看类型，F8 生成；F10 尝试 `mi.list`、`mi.status`、`mi.spawn.jumping`。附近生成多只后分别拾取/放下，验证原版同款没有跳跃效果。
3. 确认骰子进入新生成小局，核对 `[Pool]` 和 `[Batch result]`；没有抽中不等于失败。重复小局观察权重与每类型计数，不用调试实例来验证自然上限。
4. 双端装同版本，远端入场后拿取/转交；观察者看到运动与雕像位置一致；晚加入能够正确创建已有独立类型。远端 DebugMode 可以关闭。
5. 测试缺失 MOD/内容版本错误时准入拒绝，以及结束大局、重开、死亡/复活、菜单、出售/销毁后无效果残留。存档恢复和自定义资源需要单独验收。

## 6. 刷新权重：初始相同，选中后衰减

### 6.1 已读取的原版默认参数

| 参数 | 序列化默认值 | 含义 |
| --- | --- | --- |
| `defaultIdolWeight` | `100` | 动态权重中没有覆盖值时的默认权重。 |
| `idolWeightDivisor` | `2` | 选中一种雕像后，当前权重除以此值。 |
| `maxSpawnsPerIdolType` | `2` | `SrvSpawnItemsForData` 根据该次调用的类型计数过滤候选，达到 2 时通常排除该类型；不是所有入口整局最多两只的绝对限制。 |
| `scorePointsPerRoomDistance` | `4` | 价值相关配置，不是类型抽取权重。 |

另有原版皮肤数值表 `idolsScoreData`：Bronze 的 `skinValue = 100`，Silver 为 `150`，Gold 为 `200`，Crystal 为 `300`。它与 `scoreBasePoint`、运行时 `CurrentValue` 是不同的数据；最终计价公式尚未完整核实，不能直接给出相加或相乘公式。

雕像配置没有单独的普通刷新 `weight` 字段；已确认的类型权重由 `ItemDropManager` 的默认参数和动态权重状态维护。

类型上限与权重是两回事：前者决定类型还能否作为候选，后者决定合法候选之间的抽取比例。已复核类型计数位于 `__c__DisplayClass45_0.idolSpawnCountByType`，在 `SrvSpawnItemsForData` 内创建；筛选回调 `<SrvSpawnItemsForData>b__21` 在未忽略上限时比较当前计数与管理器 `maxSpawnsPerIdolType`。因此应表述为该自然生成批次中的类型上限，不能把它改写成整个游戏会话所有入口的上限。`ignoreIdolSpawnTypeCap=true` 可跳过这项过滤；当前 F8 调用的 `SrvSpawnToolOnGround` 没有调用该批次筛选，也不受这一参数限制。

### 6.2 已确认的实际逻辑

原生代码显示：`GetIdolWeight` 有覆盖值就取覆盖值，否则取默认值；`SrvSpawnItemsForData` 累计候选权重，根据随机阈值选择类型，然后用 `SetIdolWeight` 写回衰减后的权重。

```text
未覆盖的类型：100
选中该类型一次：100 / 2 = 50
再次选中该类型：50 / 2 = 25
```

准确表述是：**每选中一次，该类型当前权重减半。** 权重更新发生在后续实例化调用之前，不能改写成“成功生成一个实体后才减半”。

所以不能简单回答“所有雕像永远平均刷新”，也没有证据说明某一种始终最高。候选条件和当前权重相同的时候，抽取才对称；选中后，其他未衰减的候选会有更高的相对权重。

例：若某次抽取只有 A、B、C 三个合法候选，权重为 `50、100、100`，则按当前权重计算为 `20%、40%、40%`。这个例子不代表原版始终只有三个候选，也不代表整局最终出现率。

最终频率还受实际生成池、难度、玩家人数、biome tags、生成数量和类型上限影响。配置中的难度范围也有差异，因此不能用 `1/21` 作为通用刷新概率。

另外，21 条配置的 `eventSpawnChance` 都为 `1`；它是事件生成相关字段，不能等同于普通雕像类型权重。皮肤 `distribution` 数值合计也不是 1，尚未确认其完整算法，本文不将它们直接写成百分概率。

### 6.3 原生定位与未确认边界

当前二进制中的虚拟地址（VA）：

- `GetIdolWeight`：`0x182EB21D0`。
- `SetIdolWeight`：`0x182EB4470`。
- `SrvSpawnItemsForData`：`0x182EB6F10`。
- 选中后的除法及权重更新：`0x182EB9743`–`0x182EB9772`；后续 `SrvSpawnItem` 调用位于 `0x182EBA1CA`。

这些地址只用于复查当前二进制，不能作为跨版本稳定的补丁地址。权重何时重置、跨场景是否保留、存档恢复如何处理，仍待核实。

## 7. 分阶段开发与验收

已完成的代码与运行验证分别勾选；0.1.1 单人生成与持续跳跃已有用户反馈和日志证据。0.2.0 根据小马希望开发初期准备公共基础的要求，先实现独立注册与自然接入；各阶段的游戏验收仍分别保留，不把先写入代码视为阶段验收通过。结果记入第 10 节。

### 阶段 0：插件加载和诊断入口

- [x] 保持 `net6.0`，补齐 BepInEx IL2CPP、Il2CppInterop、`vhs`、Unity 和实际所需的 Mirror/Harmony 引用。
- [x] 接入 Publicizer，明确公开 `vhs.dll`，验证公开副本、私有字段访问编译及输出依赖。
- [x] 建立插件入口和加载日志，生成前检查本机角色与控制状态；源码编译通过。
- [x] 增加 F8 手动测试入口，检查服务器、管理器、配置、玩家位置、地面与空间占用。
- [x] 在游戏中确认 0.1.1 插件加载成功、角色就绪后可生成。

**验收：** 插件正常加载；在条件不足时可诊断并退出；场景就绪后能查到 `Data-Treasure-Score`，且预制体有效。

### 阶段 1：生成一个原版雕像

- [x] 使用原版配置及预制体，在服务器端手动生成测试实例（0.1.1）。
- [ ] 验证地面检测、位置和高度偏移，并记录实例的皮肤、`CurrentValue` 与物理状态。
- [x] 实现实例引用保存并记录 `netId`；0.1.1 单实例提示已实测，0.1.2 改为多实例集合和 cfg 上限。
- [ ] 验证可见、碰撞、拾取、持有、丢弃、再次拾取和带回出售。
- [x] 本机日志确认 0.1.1 实例销毁后清理同步标识；0.1.2 及换场景清理待游戏内复测。

**验收：** 一个原版实例完整走通交互和出售流程，日志没有相关异常。此阶段仅证明生成路线可用，尚未证明新增类型注册。

### 阶段 2：附加一个小功能

当前验证用例由小马确定：**玩家持有指定测试雕像时持续跳跃，使用原版跳跃入口和原版限制，并按多人同步设计实现。** 实现详见第 5.4 节。

- [x] 核实原版跳跃按键入口、请求消费、起跳体力处理和落地复位路径。
- [x] 仅处理 F8 生成的测试实例，通过所有者与实际手持对象判断生效范围。
- [x] 实现持有者客户端请求跳跃、放下后停止新增请求，以及实例/玩家/会话失效时的状态清理。
- [x] 小马确认单人房主持有时持续跳跃，日志记录多次拿起/放下；0.1.1 基础效果通过。
- [ ] 游戏内逐项验证原版跳跃限制和体力边界，以及 0.1.2 多实例表现。
- [ ] 游戏内验证其他普通雕像不受影响，销毁、死亡/复活和换局不会残留效果。

**验收：** 持有时按原版规则连续跳跃，放下后不再自动起跳；空中不额外起跳；原版状态限制、反馈和体力处理正常；其他原版雕像不受影响。

0.1.0–0.1.2 测试行为只绑定实例；0.2.0 改为独立类型识别。两者均不修改共享原版 SO、原版跳跃参数或其他原版类型；独立注册后的效果需重新做游戏验收。

### 阶段 3：建立独立的雕像类型

- [x] 0.2.0 已实现自有配置/预制体克隆和实体配置连接；Unity 原生运行待验收。
- [x] 分配稳定独立的配置 ID 与 assetId，检查冲突并保留原版条目。
- [x] 实现 `SOItemLookup` 列表/cache 维护、幂等登记和替换查找表时的自有条目清理。
- [x] 实现网络预制体登记和重建后恢复；真实联机时序待验收。
- [ ] 手动生成新类型，复验完整交互流程；原版同源类型行为保持正确。

**验收：** 新旧类型能按各自 ID 正确查找，新类型使用自己的行为与状态。配置 ID 和网络 `assetId` 职责不同，不能混为一个注册动作。

复用原版外观即可完成此阶段。自定义效果对象、组件注入、网络行为和虚方法回调可分别验证，避免同时引入全部未知因素。

### 阶段 4：接入自然生成

- [x] 找到地图生成入口使用的 ItemSet，0.2.0 在本次候选列表接入独立配置；自然出现待实测。
- [x] 代码保留原版筛选、总数与类型上限；这些规则的运行效果待游戏验证。
- [x] 可选初始权重只在会话首次准备/原版重置时应用；跳跃未覆盖默认权重，存档恢复和全部重置时机仍待实测。
- [ ] 对多个 seed 和难度记录候选、选中结果与当前权重，区分“未入池”“被过滤”和“未抽中”。

**验收：** 无需手动生成也能出现新雕像；出现条件与配置一致；原版生成保持正常。少量样本只能验证流程，不能证明精确长期概率。

### 阶段 5：多人和生命周期验证

- [x] 实现服务器测试标识快照、本机控制权判断、晚加入/重连重新握手，以及实体销毁/会话结束清理；待双端运行验收。
- [ ] 主机和客户端加载所需配置、预制体与行为；记录双方 MOD 版本。
- [ ] 验证双方可见、拾取/丢弃同步、价值同步和出售结果。
- [ ] 验证换场景、再次开局、断线重连或晚加入（游戏支持的情况下）。
- [ ] 核实状态清理、重复登记、退订，以及存档恢复路径（如适用）。

**验收：** 双方对实例和关键状态一致，换局不会积累旧状态；明确记录安装要求。

只调整原版生成数量可能仅需要服务器逻辑；新增模型、客户端效果或网络预制体时，尚不能承诺“只装房主即可”。

### 阶段 6：根据已实现内容提炼薄注册 API

- [ ] 用两三种不同机制的雕像验证共同接入需求，例如一次性效果、持有期间效果、独立表现组件。
- [ ] 将稳定 ID、配置/预制体接入、生成池、权重策略和生命周期清理集中到内部流程。
- [ ] 为重复 ID、重复注册、注册时机错误及缺失资源提供清楚的诊断。
- [ ] 再将稳定的公共部分开放给其他作者，提供一个最小示例。

**验收：** 第二个作者无需复制内部补丁和查找表维护逻辑即可接入内容；专有行为仍能使用自己的代码。

## 8. 内容与框架的取舍

**当前选择：先做内容，把接入过程集中管理，跑通后开放接口。** 第一轮目标是“游戏中出现一个可交互雕像，并验证一个小功能”。

框架适合统一配置、预制体、查找表、生成池及共同生命周期。不同雕像各自的数值规则、特殊交互、表现和同步需求，应保留扩展能力，由作者实现。

目前无需设计全能效果基类、通用技能描述语言或完整插件生态。先用实际内容证明哪些部分稳定重复，再决定 API 形式。

| 工作 | 当前难度判断 | 依据与主要未知项 |
| --- | --- | --- |
| 复用原版资源手动生成 | 较低至中等 | 已有配置、预制体和生成入口；需解决加载时序与场景上下文。 |
| 给一个实例附加简单效果 | 中等 | 有事件和价值接口；需验证事件时序、实例隔离与同步写入。 |
| 新 ID、新配置、独立预制体 | 中等偏高 | 涉及缓存、配置引用、网络登记和重复初始化。 |
| 自制模型、自定义 IL2CPP 行为及联机 | 较高 | 资源制作、类型注入、原生回调与网络兼容均需验证。 |
| 对外稳定框架 | 应后置 | 需要多个真实内容验证接口，当前还缺运行证据。 |

这只是基于已知入口的工程判断，不是完成工期承诺。

## 9. 已排除的误读与待查问题

已排除的误读：

- `idolSpawnCountByType` 位于编译器闭包 `ItemDropManager.__c__DisplayClass45_0` 内，不是管理器的长期字段；生成结束后把统计乘倍数不会凭空增加实体。
- `ignoreIdolSpawnTypeCap = true` 表示忽略类型数量上限，不等于增加总数量或新增种类。
- `EntityManager.RegisterIdol` 登记实例，不等于登记新配置类型。
- `PlaceIdolForDebug` 移动或处理已有实例，不是凭空创建雕像。
- 查找表有 21 条、`eventSpawnChance = 1`、皮肤 `distribution` 等信息，都不能直接换算成普通刷新概率。

待查问题：

- [ ] 运行时查找表、管理器与 Addressables 资源的加载顺序。
- [ ] 两个手动生成入口对雕像、掉落点、房间和网络状态的具体要求。
- [ ] `SrvPickedUp` 触发前后的价值变化，以及委托正确订阅/退订方式。
- [ ] `CurrentValue` 与 `Network_currentValue` 的服务器写入及客户端更新关系。
- [ ] 原版实例自动登记/注销的完整生命周期。
- [ ] 自定义 IL2CPP 组件或效果类型的注入、虚调用及网络序列化限制。
- [ ] 类型 ID 和 `assetId` 如何稳定分配、查重及在双方保持一致。
- [ ] 动态权重的初始化、重置、跨场景和存档恢复行为。
- [ ] 实际生成池、过滤顺序、特殊条目和上限的作用范围。
- [ ] 各预制体共享哪些模型/材质，以及新外观的资源制作流程。

## 10. 后续验证记录

每完成一阶段，更新对应勾选项，并填写运行证据；出现与静态研究不一致的结果时，修正文档结论。

| 日期 / 版本 | 阶段与用例 | 实际结果 | 日志或证据位置 | 尚未解决的问题 |
| --- | --- | --- | --- | --- |
| 2026-10-12 / 工程 0.1.0 | 阶段 0：Release 构建、API 引用与 Publicizer 私有字段访问编译检查 | 均通过，0 警告、0 错误 | `bin/Release/net6.0`、`obj/Release/net6.0/publicized`、`bin/ReferenceCheck` | 插件加载及游戏内行为尚未测试 |
| 2026-10-12 / 插件 0.1.0 | 阶段 0–2：F8 生成与持有自动跳跃实现、原生路径核对及静态生命周期检查 | 功能源码 Release 编译通过，0 警告、0 错误；原版按键、请求、体力处理与落地复位路径已核对 | `MoreIdolsPlugin.cs`、`JumpingIdolDriver.cs`、第 5.4 节 | 尚未部署；游戏内加载、地面/空间检测、跳跃时序及交互流程待实测 |
| 2026-10-12 / 插件 0.1.0 实测 | 插件加载、F8 回调；房主角色已能移动 | 加载和回调通过，但 `isLocalPlayer` 错误拒绝了生成 | 本机 `BepInEx/LogOutput.log` 与小马反馈 | 已在 0.1.1 移除该条件，修正后需复测 |
| 2026-10-12 / 插件 0.1.1 | 修正角色判断，加入服务器标识快照、持有者客户端跳跃及构建自动部署 | 编译与自动覆盖通过，0 警告、0 错误；部署 DLL 与输出哈希一致；角色与 Mirror 消息原生路径已核对 | 第 5.4 节、`JumpingIdolSync.cs`、`MoreIdols.csproj`、游戏 `BepInEx/plugins/MoreIdols.dll` | 重启后的生成和双端同步尚未验收 |
| 2026-10-12 / 插件 0.1.1 实测 | 单人房主生成、持有期间持续跳跃与销毁清理 | 小马确认跳跃功能成功；日志记录两次不同 netId 的生成、持有/放下与销毁同步标识清理 | 小马反馈、游戏 `BepInEx/LogOutput.log` | 体力/状态边界、出售计价、双端同步未逐项验收 |
| 2026-10-12 / 插件 0.1.2 | cfg 调试开关、重复按键生成及多实例同步 | Release 编译及自动部署通过，0 警告、0 错误，部署哈希一致；实际同步源码配合模拟 Mirror 传输的 37 项检查通过 | 第 5.5–5.6 节、`obj/SyncVerification`、游戏 `BepInEx/plugins/MoreIdols.dll` | 多实例物理位置、真实双端转交/晚加入/重连待实测 |
