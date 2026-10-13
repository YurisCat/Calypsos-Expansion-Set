# 当前设计

本文说明当前源码中的模块职责、接口与行为。当前构建/待办见 [TODO](../TODO.md)，使用方式见 [README](../README.md)，版本变化见 [CHANGELOG](../CHANGELOG.md)，原生研究与实际验收证据见 [DEVELOPMENT](../DEVELOPMENT.md)。本文的同步路径与流程说明不代替真实游戏验收。

## 术语与模块

| 术语 | 含义 |
| --- | --- |
| 原版 | Invokyr 游戏本体 |
| 雕像类型 | 具有稳定 key、配置 ID、assetId 和效果定义的一种内容 |
| 雕像实例 | 场景中某只物品，以 netId 标识；ScoreTool 属于 Item |
| 类型注册 | 创建配置、查找表条目和网络预制体；EntityManager.RegisterIdol 只登记已有实体 |
| 小局 | 确认骰子事件后探索变化地图、带回雕像的一轮 |
| 大局 | 整个房间/跑团会话 |
| 生成批次 | 一次实际原版生成调用；小局与生成批次不一定一一对应 |
| 入池 | 加入筛选前候选集合；不等于被抽中或已经生成 |

| 源码 | 职责 |
| --- | --- |
| [CalypsoExpansionSetPlugin.cs](../CalypsoExpansionSetPlugin.cs) | Load 登记内置定义、协调维护与卸载、服务器调试生成 |
| [IdolDefinition.cs](../IdolDefinition.cs) | 注册 API、定义字段、执行端与 HeldIdolEffect 生命周期 |
| [IdolRegistry.cs](../IdolRegistry.cs) | 克隆/查找/网络登记、兼容清单、视觉资源所有权 |
| [IdolSpawnIntegration.cs](../IdolSpawnIntegration.cs) | 当前 ItemSet 的批次副本、自然候选接入与结果统计 |
| [IdolEffectRuntime.cs](../IdolEffectRuntime.cs) | 识别当前持有物，按类型/执行端启动、更新和停止效果 |
| [JumpingIdolSync.cs](../JumpingIdolSync.cs) | 全类型兼容准入与服务器实例/调试来源记录；保留历史文件名 |
| [Idols/JumpingIdol.cs](../Idols/JumpingIdol.cs) | 原力度连续跳跃 |
| [Idols/SpringIdol.cs](../Idols/SpringIdol.cs) | 弹簧编排、原生单次力度补丁与可选测量 |
| [Idols/FovIdol.cs](../Idols/FovIdol.cs) | 近景持有者的本地 FOV 覆盖与恢复 |
| [Idols/VoiceIdol.cs](../Idols/VoiceIdol.cs)、[Idols/VoiceInputGate.cs](../Idols/VoiceInputGate.cs) | 声控持有者的方向输入替换、麦克风采样与音量判定 |
| [Idols/UpsideDownIdol.cs](../Idols/UpsideDownIdol.cs)、[Idols/DelayedMoveIdol.cs](../Idols/DelayedMoveIdol.cs)、[Idols/ExtremeSpeedIdol.cs](../Idols/ExtremeSpeedIdol.cs) | 渲染期间倒立、方向输入延迟与原生速度计算 |
| [Idols/DuckStepIdol.cs](../Idols/DuckStepIdol.cs)、[Audio/HeldIdolAudio.cs](../Audio/HeldIdolAudio.cs) | 原版脚步触发、共享一次性音效协议和 FMOD 资源 |
| [Visuals/StickyNoteVisuals.cs](../Visuals/StickyNoteVisuals.cs) | 便签网格、纹理、材质与视觉诊断资源 |
| [IdolDebugTools.cs](../IdolDebugTools.cs)、[IdolDebugTeleport.cs](../IdolDebugTeleport.cs) | F8 命令窗口、输入恢复与服务器调试传送 |
| [Testing/TestRoomLaunch.cs](../Testing/TestRoomLaunch.cs)、[Testing/TestRoomScene.cs](../Testing/TestRoomScene.cs) | 专用单人入口与陈列房；其他 Testing 文件负责布局、资源和模式补丁 |

## 类型注册

`CalypsoExpansionSetApi.Register(IdolDefinition)` 在插件 Load 阶段收集定义。外部插件引用本程序集并声明对 `calypso.invokyr.expansionset` 的 BepInDependency；首次运行期初始化后定义锁定。

定义必须有 key、显示名、非零且独立的 ulong 配置 ID / uint assetId、有效模板、效果工厂及执行端。重复 key/配置 ID/assetId、非法版本/权重会被拒绝。

| 可选字段 | 当前含义 |
| --- | --- |
| ContentVersion | 默认 1；手工维护玩法、配置和外观契约版本 |
| PoolTemplateId | 自然池匹配标记；为空时使用 TemplateId，必须是有效雕像配置 |
| InitialWeight | 初始相对权重，未指定使用原版默认值；不是百分比 |
| ConfigureData | 修改自有配置副本，不能改稳定身份 |
| ConfigurePrefab | 修改自有预制体，保留必要物品/网络结构 |
| CreateVisuals | 在自有预制体创建附件并返回 IDisposable；构造抛错前自行回滚 |

资源就绪后，注册器克隆 SOScoreTool 与预制体。预制体在非活跃父节点下创建，避免 Awake 提前看到模板身份；随后连接自有 gameplayElementData、清除 sceneId、设置 assetId、运行配置/视觉回调，最后接入 scoreTools、_scoreById 和 Mirror 预制体表。

```mermaid
flowchart LR
    A[Load 收集定义] --> B[等待模板和查找表]
    B --> C[克隆并配置自有预制体]
    C --> D[执行可选外观回调]
    D --> E[登记配置和网络预制体]
    E --> F[兼容准入与原版生成]
```

创建失败清理本次配置、预制体和视觉资源；整个注册/运行失败时不宣称 ready，并阻止新内容准入。场景重建预制体表或配置查找表时重新登记保持幂等，不覆盖冲突条目。活跃会话不热卸载。

兼容清单按配置 ID 排序，包含 key、配置 ID、assetId、TemplateId、实际 PoolTemplateId、ContentVersion 和执行端；不自动散列委托或资源文件。改变契约时必须提升对应内容版本。

## 内置雕像

身份参数的权威来源是各类型源码的 Definition。当前定义如下：

| 名称 | key | 配置 ID | assetId | ContentVersion | 执行端 |
| --- | --- | --- | --- | --- | --- |
| 跳跃雕像 | jumping | 0x4D49000000000001 | 0x4D490001 | 8 | OwnerNativeGameplay |
| 弹簧雕像 | spring | 0x4D49000000000002 | 0x4D490002 | 2 | OwnerNativeGameplay |
| 近景雕像 | near_view | 0x4D49000000000003 | 0x4D490003 | 1 | LocalPresentation |
| 声控雕像 | voice | 0x4D49000000000004 | 0x4D490004 | 1 | OwnerNativeGameplay |
| 倒立雕像 | upside_down | 0x4D49000000000008 | 0x4D490008 | 2 | LocalPresentation |
| 鸭步雕像 | duck_step | 0x4D49000000000005 | 0x4D490005 | 2 | OwnerNativeGameplay |
| 极速雕像 | extreme_speed | 0x4D49000000000006 | 0x4D490006 | 1 | OwnerNativeGameplay |
| 迟钝雕像 | delayed_move | 0x4D49000000000007 | 0x4D490007 | 1 | OwnerNativeGameplay |

八种均使用 Score 配置/外观模板 `7331884207844971565`，以 Slippery `13601636613369722279` 匹配自然池。基础属性继承模板，均未指定自定义初始权重。

0.2.17 合并并行开发时保留远端已发布声控的 ID/assetId 后缀 04，将尚未发布的倒立试验身份移到空闲后缀 08、ContentVersion=2。原七种试验构建的清单和游戏证据仍按当时身份记录；没有将旧本地试验的倒立存档自动迁移为新 ID，升级测试需重开房间，联机清单必须一致。

跳跃雕像在落地、没有原版待处理跳跃且未处于跳跃状态时，继续调用原版跳跃输入，保持原力度；不另加落地等待，也不在拿起时清空体力。

弹簧采用固定编排：四次小跳后正常跳，再五次小跳后稍高跳，循环执行。首次等待 0.15 秒，成功起跳后所有节拍落地等待 1 秒；小跳目标约半米，力度系数 0.58，并按原版 JumpForceMultiplier 把起跳速度上限限制为 3.6；正常/稍高节拍系数为 1 / 1.12。实际高度依赖原版重力、地形和角色条件，测得高度见对应版本验收记录。

弹簧先通过原版输入排队，再按物品、持有者、角色控制权、请求时间戳及超时核对待处理请求。在匹配的 HandleJumpRequest 单次调用内临时调整该角色 jumpForce，Finalizer 恢复自己的写入；不持久改共享配置、刚体速度或重力倍率。若其他 MOD 已写入不同力度，不覆盖其值。原生调用异常后停用本次效果直到释放。

两种均不主动写体力，继续沿用原版起跳体力处理。菜单/控制禁用时不新增动作；空中和既有请求优先完成。手动跳跃不强制变成弹簧节拍，可推迟自动节奏。放下/转交/失效时停止效果，但不截断已经由原版接受的跳跃。

近景雕像内置 FOV=15°，不提供 CFG 参数，不写原版 GraphicsSettings 或虚拟镜头配置。当前游戏原版默认 FOV=65°，设置范围为 30°～90°；这是设置范围，奔跑/抖动/取景等运行期叠加另行处理，原生证据见 [DEVELOPMENT](../DEVELOPMENT.md#fov-research-0214)。

近景在原版 SetFovShakeData.LateUpdate 前撤销仍属于自己的上次 FOV 写入，让原版计算正常视野；Finalizer 在原版成功返回后保存结果，再覆盖最终 Camera.fieldOfView。每次核对实际持有物、本机角色控制权、健康/Ready/兼容状态、控制可用及镜头关联的 playerPerlinShake，只作用于站立/蹲下视角。菜单/F8、放下、转交、死亡、角色/镜头替换、断线或卸载时恢复自己仍持有的写入；若游戏或其他 MOD 已写不同值则保留。异常时停用到释放，原生异常保持传播，不逐帧重试或刷屏。Unity 镜头更新顺序及与其他 MOD 并用仍需游戏验收。

声控只在当前持有者读取 `InputsManager.Move` 时用 Postfix 替换返回的 Vector2：声音满足条件输出向前，否则输出零。键盘、重绑定按键和手柄摇杆的方向输入都在此入口被替换；不写原版存储的输入值、不解绑动作订阅，转向、跳跃、蹲下、奔跑、交互和放下继续使用原版输入。取消前进输入不会清除原版惯性、重力或外力。

音频补丁在原版 `PlayerMicrophone.ProcessAudio` 完成后发布 `_correctedAmplitude` 和时间戳，主线程消费当前持有者麦克风的样本。启动阈值读取原版 `amplitudeThreshold`，这是数字音量幅值，不是物理声压分贝；只在原版值无效时采用保护值。短时平滑与较低的停止阈值避免临界抖动，过期/无效样本停止推进，因此启停时机与原版制造噪声的较长平均窗口并不完全相同。没有独立 CFG 阈值；具体平滑和保护参数以源码为准，原版证据见 [声控研究](../DEVELOPMENT.md#voice-research-0215)。

每次消费输入重新检查实际持有物、物品归属、本机角色控制权、健康、Ready/兼容和控制状态。静音、麦克风失效、菜单/F8 或控制禁用时清空采样并输出零；恢复后需要新声音样本。转交、放下、死亡、库存/角色切换、断线及卸载撤销覆盖，不必恢复输入字段或订阅。音频回调只发布样本/错误，主线程处理生命周期与异常日志；原生读取异常后停用到释放。原版按键说话决定可用声音输入，真实麦克风/原生补丁执行仍待游戏验收。

0.2.12 曾把弹簧保存在 jumping 身份下；拆分后 jumping 恢复跳跃，弹簧另用 spring，调试命令相应改变。没有自动迁移已有存档类型；升级变化见 [CHANGELOG](../CHANGELOG.md)，存档恢复仍待验收。

## 四种效果与共享音效

- **倒立**（[UpsideDownIdol](../Idols/UpsideDownIdol.cs)）：只在实际本机 POV 世界相机渲染期间临时绕视轴旋转 180°，在 EndCameraRendering 和渲染 Finalizer 释放；不把旋转保留到下一次输入/物理更新，不反转输入轴。筛选实际相机及站/蹲视角，HUD/菜单相机不修改。自身旧写入被其他代码替换时不覆盖。
- **迟钝**（[DelayedMoveIdol](../Idols/DelayedMoveIdol.cs)）：修改原版 InputsManager.Move getter 的本机返回值。方向与零输入均入固定容量的 0.3 秒队列，帧停顿取最新到期值；不存储或重放视角、交互、跳跃等动作。控制关闭、释放或角色失效清队列。
- **极速**（[ExtremeSpeedIdol](../Idols/ExtremeSpeedIdol.cs)）：在持有者的原版 PlayerMovement.FixedUpdate 与 Update 单次调用内，把 SpeedMultiplier 乘 4，Finalizer 恢复仍属于自己的字段；FixedUpdate 下的地面/流沙速度计算以及 Update 的动画归一化读到一致倍率。保留原版空中运动、加速度、碰撞、持有物减速、FOV 和体力流程，不直接写刚体速度。空中、冰面/流沙与极端速度的完整边界仍待实测。
- **鸭步**（[DuckStepIdol](../Idols/DuckStepIdol.cs)）：只在持有者原版 PlayFootSteps 成功重置步频计时后附加音效请求，核对地面、实际位移和当前持有物。停止、顶墙、腾空或控制禁用时不额外叫；奔跑频率继续跟原版步频。保留原版脚步，服务器每次确认鸭叫再添加一次原版怪物听觉事件。

前三种不新增自有网络状态：持有身份、动作和运动继续走原版同步。倒立仅本机显示；迟钝/极速仅有控制权的本机角色执行。释放/转交等清理沿用 HeldIdolEffect，其他玩家不会重复施加倍率或输入延迟。

[HeldIdolAudio](../Audio/HeldIdolAudio.cs) 为鸭步和弹簧共用的一次性表现协议，消息 ID=`0x4D4A`、协议版本=1；与兼容准入消息 `0x4D49` 分开。类型通过资源回调注册自己的 bank，不在主插件或网络层增加逐种分支。弹簧只在 native HandleJumpRequest 接受编排中的 accent 节拍后调用 Emit，小跳/普通跳不触发。

持有者客户端只报告雕像 netId 与递增序号，不决定片段或位置。服务器检验实际连接指针、认证、Ready、类型清单、实例、持有者归属、健康、手持状态、序号与最小间隔；鸭步还检查连续位置变化。随后服务器从当前 bank 中随机选一段，避免同持有者/实例/音效组的相邻重复，使用服务器角色位置广播。观察者和持有者都只播放这一次广播，不做本地预测双播。此路径是受验证的所有者动作通知，并非服务器重新模拟每一次脚步或整段弹簧编排。

自 0.2.16 起，bank 可以提供 `OnServerEmission` 回调；在上述校验通过并记下序号/冷却后、广播前仅执行一次。鸭步定义在自己的回调中调用 `HearingSenseManager.AddVolumeEvent(holder.transform, NoiseRange)`，内置 NoiseRange=18，来源为持有玩家，沿用普通双参数重载的 audience=null、ignoreRange=false 与原版寿命/清理。这里不要求服务器角色 isOwned 或等于 EntityManager.Me，以支持远端持有者；仅客户端播放、重复/拒绝请求均不会添加噪声。

怪物继续使用原版听觉列表和自身感知/目标选择，不强行修改怪物状态、听觉配置或目标。18 是声源范围参数，不保证半径内每只怪物立即追踪。DebugMode/玩家音量不控制听觉玩法；放下后不生成新鸭叫事件，已经发出的声音按原版寿命自然消失。弹簧没有这个回调。新增玩法用 duck_step.ContentVersion=2 纳入现有清单握手，无需改变音效报文格式。

报文固定长度，检查 magic/version/manifest、来源、通道、数量范围、未知 netId、重复序号及非有限位置。连接 ID 复用按实际连接指针清旧记录，处理器重建/卸载只管理自己的委托。事件无快照，不向晚加入者重放旧声响；当前实例与持有状态仍由原版生成/SyncVar 恢复。全部客户端需要兼容 bank 与内容版本。双端真实听感和时序仍待验收。

FMOD 资源创建、音量和源文件构建要求见 [音频说明](../Assets/Audio/README.md)。音频资源原始文件和本地裁剪产物不进入 Git，构建 DLL 包含运行所需 bank。注册失败回滚相应 bank、便签和自有协议处理器；不会修改原版 FMOD 全局监听器或播放其他类型的声音。

## 自然生成

服务端在原版 SrvSpawnItemsForData 开始前复制当前 ItemSet，再按每个定义的 `PoolTemplateId ?? TemplateId` 将自有配置加入对应雕像候选 entry。当前快照中这些类型匹配含 Slippery 的 9 个池；Score 本身主要位于答题池，外观模板不等于合适的普通地图匹配标记。

批次副本保留原版候选顺序、bonus、生成数量与难度字段，重复接入每种只加入一次。缺失注册/兼容或 NaturalSpawning=false 时排除自有候选；客户端不修改自然池。完成或异常后只释放批次副本。

已研究的原版资源/行为：

- defaultIdolWeight=100，idolWeightDivisor=2；选中类型后当前权重减半。自定义初始权重只在首次接入/原版重置时应用，不每小局重置。
- maxSpawnsPerIdolType 的资源默认为 2，计数属于该次生成调用；原版事件可忽略上限。多个独立调用分别统计，不把它扩大为整个小局或大局的总上限。
- 仍由原版处理 seed、难度、事件、biome/tag、掉落点、房间、皮肤与价值；加入候选不保证抽中。保留地图/恢复事件可能不调用新生成。

日志用事件 ID、seed、批次号关联定义、候选与结果：

| 日志 | 含义 |
| --- | --- |
| [Definition] | 收集定义 |
| [Register] / [Registry] ready | 自有资源创建 / 注册就绪 |
| [Pool] | 本批次原版筛选前的 MOD 候选 |
| [Natural spawn] / [Batch result] | 实际实例 / 批次完成或失败结果 |
| [Debug spawn] | 明确操作产生的手动实例，独立记录 |

## 执行端与同步

| 执行端 | 决策与执行 | 网络和恢复要求 |
| --- | --- | --- |
| LocalPresentation | 当前持有者客户端修改自己的画面/声音/UI | 持有身份沿用权威状态；各退出路径恢复自身表现 |
| OwnerNativeGameplay | 拥有角色控制权的客户端调用原版动作或提供原版运动输入 | 运动/动画继续走已确认的原版同步；服务端和观察者不重复执行 |
| ServerGameplay | 服务端验证并修改世界/跨玩家状态 | 效果自己调用实际网络 setter/RPC；自有稳定状态另设计快照 |

执行器通过原版配置 ID 识别类型，以 ItemOwner 与 InventoryNew.InHand 判断实际持有物；本地动作还要求本机角色有移动控制权、连接 Ready、清单匹配且游戏控制可用。EntityManager.Me 来自 UserInstance.Me.playerEntity，不用 PlayerEntity.isLocalPlayer 再拒绝可移动房主。

兼容协议由 JumpingIdolSync 统一处理，消息 ID=0x4D49、协议版本=3。客户端在 Ready 前发送清单握手，服务端暂缓 NetworkServer.SetClientReady，匹配后再恢复原版 Ready；缺失 MOD 超过 30 秒或版本/清单不匹配拒绝连接。连接 ID 复用核对实际连接身份，不继承旧兼容状态。

雕像生成、配置、物品持有状态及实例晚加入恢复使用原版网络 prefab/SyncVar；没有重复的自有实例 marker 快照。跳跃/弹簧只有持有者所属端编排动作，观察者依靠原版运动/动画同步看到结果；弹簧力度不另作自有网络字段广播。原版路径接入、托管模拟与真实双端验收是不同证据，当前边界查看 [TODO](../TODO.md)。

近景由当前持有者客户端执行，房主和观察者不重复修改画面，也不广播 FOV。转交后新持有者按原版持有状态启用自己的效果，晚加入按当前物品/持有状态恢复；没有新增权威玩法状态或随机结果。全部参与端仍需注册兼容类型，真实双端的持有/转交时序尚待验证。

声控由持有者所属且有移动控制权的客户端读取自己的麦克风，并向原版运动入口提供方向输入。房主观察远端持有者时不替其推进，不新增音量/前进消息；运动、动画、奔跑体力、碰撞和物品持有状态沿用原版路径。转交后新持有者以空采样开始；晚加入依靠原版物品/角色状态恢复，不重放历史声音。所有类型均须先注册并通过兼容准入，旧内容清单不兼容；真实双端、观察者、转交与晚加入仍需实测。

声明 ServerGameplay 不会自动同步字段。新增价值状态应使用 Network_currentValue 等实际原版入口；新增稳定玩法状态、随机结果、可重放事件或存档契约需要独立设计与验证。

## 便签资源与生命周期

跳跃/弹簧的图案 key 为 `jumping`，类型 key 与图案 key 分开，暂用同一图案。其余六种使用空白 paper.rgba，等待各自图案。弹簧和鸭步的 CreateVisuals 返回便签与音效 bank 的组合所有权，其他类型直接返回 StickyNoteVisuals。每种类型创建独立 Mesh、Material、Texture；同类型实例使用其预制体资源。

便签从 DLL 内嵌的 note.mesh.json 和按图案 key 选择的预合成 RGBA mip 链创建视觉对象，挂在自有 Score 预制体 `Dice/SM_IdoleBody/SM_IdolHead` 下。随后原版生成复制带附件的完整预制体；不在场景生成后扫描雕像补挂，不修改原版同模型雕像，也不添加拾取/物理/网络组件。

注册器持有返回的 IDisposable，后续创建失败或正常卸载清理。便签工厂在返回前抛错时自己回滚。指定图案缺失会注册失败，不自动降级为空白。外部注册 API 的视觉回调可选，不自动导入 FBX 或加载其他模型。

原始 bianqian.fbx 已导出为头部局部坐标网格；note.mesh.json 决定形状，paper.rgba 是底色，Baked/jumping.rgba 是带图案的运行贴图。完整资源来源、UV、导入及诊断方法见 [便签说明](../Assets/StickyNote/README.md)。

## 调试与测试房

调试 UI 是 MOD 自有 F8 窗口，不依赖场景中缺失的原版控制台。命令和配置的使用说明见 [README](../README.md)。

- DebugMode 默认 false，统一控制测试窗口、调试操作与诊断日志；关闭 F8 不改变配置开关。关闭 DebugMode 清理诊断活动，不影响雕像玩法，必要状态与异常日志保留。
- list / spawn <key> 从注册清单生成；快捷按钮只显示前三种，其他类型用命令。每种同时最多一只手动实例，出售/销毁后释放配额；自然实例不占配额。
- 直接 SrvSpawnToolOnGround 不包含自然批次的皮肤/价值初始化。调试生成读取 idolsScoreData，未指定皮肤时选 Bronze，用原版 Skin / CurrentValue 同步 setter 初始化；不要调用下节的越界包装。
- teleport 按 netId 循环查找存活自然/调试 MOD 雕像，验证地面与角色空间后调用原版 TargetTeleport；关闭窗口恢复控制后下一帧执行，角色/连接变化或超时取消。

专用测试房要求 --ces-test-room、DebugMode=true、TestRoomEnabled=true 同时满足，普通启动不会自动建房。它沿原版 KCP 流程开本机单人会话，跳过 Logo，在初始房门外建立陈列房、打开连接门并放置玩家；只允许本地连接，阻止事件/怪物生成和存档写入，不自动补刷展品。

后部摆放 20 种非怪物原版雕像（含两种特殊拼图；排除怪物 WanderingDice），两排各 10 个。中部按稳定 key 自动陈列每种 MOD 一只并计入各自调试限额。基座只读复用 SM_SocleStatue 网格/M_SocleStatue 材质，自建静态承托碰撞，不复制来源房间脚本。

布局由 [TestRoomLayout.cs](../Testing/TestRoomLayout.cs) 控制：横距 2.2 米、排距 3.2 米、每排最多 10 个，不满行居中，MOD 增加时延长房间；每区最多 128 种，超出时报错。MOD 类型按稳定 key 的 Ordinal 字母顺序排列，当前为 delayed_move → duck_step → extreme_speed → jumping → near_view → spring → upside_down → voice，新增时间不决定位置。八种合并布局尚待游戏验收；原七种试验构建的单人游戏证据见 DEVELOPMENT。顶部只过滤 UI/lobby/waitPlayerBeforeStart 对应键/译文，保留其他 HUD。

## Interop 禁用项

这些条目来自已记录的原生/游戏问题；更换游戏或 interop 后应复核，不从托管模拟通过推断封送正确。

| 路径 | 已确认问题及当前处理 |
| --- | --- |
| TryGetIdolSkinData(out FIdolScoreMaterialData) | 包装只分配 8 字节，原生值为 24 字节，存在越界；禁止调用，改读 idolsScoreData 装箱条目 |
| ImageConversion.LoadImage | 本机包装走缺失的 ReadOnlySpan 方法；便签使用已核对的 LoadRawTextureData(IntPtr,int) 上传完整 RGBA mip 链 |
| GUI.SetNextControlName / 自动聚焦流程 | 本机 SetNextControlName 触发 ReadOnlySpan.GetPinnableReference 缺失异常；窗口避免控件命名/FocusControl 自动聚焦，异常时关闭 |

详细发现和替代路径验证保留在 [DEVELOPMENT](../DEVELOPMENT.md) 的调试闪退、便签与测试窗口记录中。原生缓存位置/二进制哈希保存在本机 `.local/ENVIRONMENT.md`（若存在）；反汇编前核对实际 GameAssembly，不直接使用旧 RVA。原版资源快照见 [idol-defaults.json](../research/idol-defaults.json)、[itemset-defaults.json](../research/itemset-defaults.json)、[test-room-pedestal.json](../research/test-room-pedestal.json)。
