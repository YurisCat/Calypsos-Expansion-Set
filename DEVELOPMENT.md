# Calypso's Expansion Set 开发路线与研究记录

本文保留按版本/日期记录的研究、原生证据与编译、模拟、游戏验收历史。历史章节中的“当前”属于记录当时的语境；查询现行实现请看 [设计说明](docs/ARCHITECTURE.md)，最新状态看 [TODO](TODO.md)，使用方式看 [README](README.md)。

## 2026-10-13：文档职责整理

按用户要求，AGENTS 收敛为长期约定与阅读入口；新增当前设计说明，集中登记模块、类型、自然生成、同步、便签、调试与 Interop 禁用项。README 维护操作说明，TODO 保留最新状态和未完成项，本机路径/缓存/观测迁入忽略目录。旧版实现、验收结果及原 TODO 完成记录保留为历史证据。公共文档使用通用称呼；DebugMode 的配置与使用说明统一描述测试工具和诊断日志，不逐种罗列。

文档整理阶段调整文档和 DebugMode 配置说明文案，未改玩法或配置值；后续仅编译结果见本节提交前检查，未部署或启动游戏。

- **文档检查**：94 个本地文件链接/锚点全部有效，`git diff --check` 通过；本机环境说明确认受 Git 忽略。
- **历史与待办**：整理前 DEVELOPMENT 的研究记录按原顺序保留；原 TODO 完整归档，14 条未完成事项全部保留在最新 TODO，另补调试日志、联机及生命周期验收项。个人称呼改为通用表述，版本、日期及验收结论保持。
- **源码核对**：核对两种内容的身份/版本/执行端、弹簧编排与日志开关、注册/兼容流程、便签加载和测试房布局/补丁。说明沿用现有证据边界，不把源码核对算作新一轮游戏或联机验收。
- **提交前检查**：C# MOD 差异审查未发现阻止提交的引擎、生命周期或资源问题；配置说明统一为通用调试开关后，重新执行 `scripts/Build.ps1`，Release 0 警告、0 错误。当前输出 `0.2.13.0`，SHA256 `865312E71A42BA24DA572682FEA40E4A669438DE668F6D15ED0BFEEAB6C10907`；旧构建哈希作为历史证据保留。本轮未重复行为/网络模拟，未部署或进行游戏/联机实测。

## 2026-10-13：0.2.13 追补弹簧测量日志门禁

用户要求非测试时不要持续输出弹簧日志。原有起跳测量已经由 `[Debug] DebugMode` 控制（默认 false）；但起跳时开启 Debug、落地前关闭时，既有 `_observingFlight` 仍可能输出一次落地测量。本次在采样入口重新核对 DebugMode，关闭时撤销测量，避免残留日志或重新开启后的补发。

雕像玩法、类型 ID/ContentVersion、兼容清单与物理节奏保持。配置说明和 README 明确：DebugMode 同时控制 F8 测试窗口与 `[Spring]` 起跳/落地测量；关闭 F8 不等于关闭 DebugMode。注册、异常和效果开始/停止等少量状态日志继续保留，关闭 Debug 不关闭雕像效果。未改本机 CFG，本轮读取到 DebugMode=true；普通游玩可设 false 并重启。

- **源码模拟**：实际定义/效果、执行器、注册器和布局程序共 51 项断言通过，包含新增的 8 项测量与动作检查：Debug=false 不记起跳/落地但动作继续，Debug=true 成对记录，飞行中关闭取消落地测量，重新开启不重放旧测量。Unity/原生动作和便签构建仍为模拟对象。
- **编译**：`scripts/Build.ps1`，Release 0 警告、0 错误，仍为未发布的 `0.2.13.0`；本次当前输出 SHA256 **`644A8165C86912BDFD32EA61B49B4A935A6CA0F70EF419F2076057FB2C38DC7F`**。下节 `F878...` 属于追补前已验证的同版本构建，保留原证据。
- **未执行**：部署与游戏/联机实测。本次不涉及候选池或网络实现，复用上一轮的 47 项兼容模拟和 183 项候选池模拟，不声称重新执行它们。

## 2026-10-13：0.2.13 恢复跳跃雕像并独立保留弹簧雕像

按用户要求，恢复早期跳跃雕像并将弹簧另增为独立类型；补充指示明确删除早期“拿起时清空体力”的行为。两种目前都使用便签，暂时复用已有弹簧图案。本轮完成源码、文档、仅编译和模拟检查；未部署、启动游戏、暂存或提交。

| 内容 | 类型 key | 配置 ID | Mirror assetId | ContentVersion | 持有行为 |
| --- | --- | --- | --- | --- | --- |
| 跳跃雕像 | `jumping` | `0x4D49000000000001` | `0x4D490001` | 8 | 落地后立即继续按原力度请求原版跳跃 |
| 弹簧雕像 | `spring` | `0x4D49000000000002` | `0x4D490002` | 1 | 保留半米小跳目标、正常/稍高跳编排和落地等待 1 秒 |

两种均为 `OwnerNativeGameplay`，由持有者所属且有控制权的客户端执行，动作、体力处理、运动和动画继续走原版入口及同步。拿起时均不写体力，不增加自有玩法状态；晚加入物品/持有状态仍使用原版配置和网络预制体，但真实双端、晚加入、手动跳跃和生命周期边界需实测。

`JumpingIdol.cs` 恢复连续原力度跳跃；现有弹簧逻辑转入 `SpringIdol.cs`，原生单次力度补丁只处理该效果自己的待处理请求。两种定义都通过统一注册器接入，Score 属性模板、Slippery 入池模板和默认权重保持；未改自然生成总数、原版筛选、权重衰减或批次类型上限。F8 列表/快捷按钮和测试房从注册清单接入两种，不添加逐种 UI/陈列分支。

两种 `CreateVisuals` 都显式使用图案 key `jumping`，从内嵌网格/图案创建各自便签资源，挂在各自预制体头部；类型 key 与图案 key 分开。未修改网格、纸底、图案原文件或共享原版材质。旧 `jumping` 身份恢复为跳跃效果，弹簧使用新 `spring` 身份；没有对已有存档做自动类型迁移，存档恢复仍未验收。

### 验证结果与边界

- **源码模拟**：直接编译实际两种定义/效果、执行器、注册器及测试房布局，43 项断言通过。覆盖连续跳与一秒等待的区别、拿起不写体力、切换后待处理力度清理、手动请求替换、放下/死亡/非拥有者/NotReady 门禁、原版同模型不受影响、便签工厂作用于自有预制体、独立资源所有权、失败回滚及正常卸载。便签工厂/Unity/IL2CPP/原生动作在模拟中为替代对象；检查最终 DLL 的内嵌网格和图案存在。
- **兼容/调试模拟**：实际 `JumpingIdolSync.cs` 加模拟 Mirror，47 项检查通过。使用实际注册器计算的新清单 `BDD529BD5AECA74D` 与旧 0.2.12 清单 `100EF9F7784C0502`，覆盖 Ready 前握手、不匹配拒绝、缺失 MOD 超时、重连/连接 ID 复用，以及 `jumping`/`spring` 各自调试限额查询与释放。消息协议仍为 3，类型清单变更要求联机各端更新。
- **候选池模拟**：实际两种定义与 `IdolSpawnIntegration.cs` 加 14 个原版 ItemSet 资源快照，183 项断言通过。两种均匹配原来的 9 个候选池，各加入一次；重复接入无重复条目，原版候选、bonus、难度数量保持，嵌套生成批次按两种 key 分别统计并只清理临时副本。未运行游戏抽取，不声称实际出现率。
- **编译**：`scripts/Build.ps1`，Release/x64/net6.0，0 警告、0 错误；输出 DLL 为 `0.2.13.0`，SHA256 **`F8780856D6DD2EDAB1EB54FFEE6F38103A9E4EE4AC2A344D351E54D0083C2862`**。
- **未执行**：部署/游戏实测，两种同时陈列与便签显示、拾取/放下/反复切换/转交/死亡/换角色、手动跳跃竞争、自然实例拾取/出售、低顶/蹲下/特殊重力、重开/存档、真实双端/观察者/晚加入/重连及其他 MOD 并用。0.2.12 的平地高度和单人证据属于旧版，不直接作为 0.2.13 两种同时运行的验收。

本轮模拟共 273 项检查，本机程序与替代接口分别位于 `.local/verification/IdolSplit`、`IdolSplitSync`、`IdolSplitPools`。当前工作站读取到游戏目录仍为 0.2.12，DebugMode=true、TestRoomEnabled=false；文档中旧桌面“测试”快捷方式在当前用户/公共桌面未找到，本轮未修改这些本机设置。

## 2026-10-13：0.2.12 GitHub 提交整理

用户明确要求写好 CL 并推送 GitHub。本次获取远端后，`main` 与 `origin/main` 均为 `63d4362`，没有待合入的新提交；暂存区原本为空。整理本工作区积累的 0.2.7～0.2.12 测试房、便签、弹簧效果与图案导入改动，新增 `CHANGELOG.md` 并从 README 链接。

发布材料包含模组源码、模型师/用户制作的便签运行资源及相关文档；原版雕像导出包、游戏 DLL、本机节点脚本、日志、缓存和整个“人类工作间”继续留在忽略目录。新增 `.gitattributes` 将 `.rgba` 标记为二进制，避免不同工作站的文本换行处理修改原始纹理字节，并核对暂存资源与工作区字节一致。本轮只补充更新记录与提交材料，复用下文同一 0.2.12 构建的编译、84 项源码模拟、7 项资源检查与节点实机证据，不另行声称新增游戏或联机验收。各历史阶段“未提交/推送”的记录保留其当时含义。

## 2026-10-13：0.2.12 弹簧雕像半米小跳与用户便签图案

按用户要求将显示名改为“弹簧雕像”，小跳按约 50 厘米目标校准，所有节拍落地后统一等待 1 秒；四小跳后正常跳、五小跳后稍高跳的固定编排保留。稳定 key `jumping`、配置 ID、assetId、模板/自然池规则不变；`ContentVersion=7`，兼容 manifest **`100EF9F7784C0502`**。本轮只部署狐狸节点测试副本。

最终小跳倍率为 0.58，带原版倍率后的起跳速度上限 **3.6**。当前角色原力度 6.250、原倍率 1 时，小跳实际使用 3.600；正常跳保持 6.250，稍高跳 7.000。依旧只在对应的原版起跳调用内临时设置并恢复该角色力度，保留动作/落地条件；不改重力、刚体速度或共享配置。DebugMode 起跳/落地日志新增 `Time.time`，用于将高度和落地等待分开观察。

### 便签图案接入

用户文件为本地 `人类工作间/接收文件/便签图案绘画模板_1024_v1/弹簧雕像.png`，1024×1024 RGBA，透明通道范围 0～255，SHA256 **`fc19f7d1e12a8a200924be243a2217a6546ff96874d1ff2d4eaeab650951a8eb`**。原文件未修改，按相同字节复制为 `Assets/StickyNote/Artwork/jumping.png`。

新增显式导入工具 `scripts/PrepareNoteArtwork.py`（开发时需要 Python/Pillow）：以稳定 key 为输出名，只把透明绘画合成到当前网格的正面矩形 UV，生成 512×512 RGBA32 完整 mip 链及来源哈希记录。保持原纸底/胶带/背面/折边与不透明材质；编译时将 `Baked/<key>.rgba` 内嵌 DLL。普通 MOD 编译/运行不读取人类工作间，也不依赖 Python。

`StickyNoteVisuals.Create(prefab, artworkKey)` 按 key 加载专属纹理，单参数形式仍支持空白纸底；指定图案缺失时报错并由现有资源生命周期回滚，不静默忽略。未增加网络组件、全局共享材质修改或运行期图片监控。每种类型仍拥有自己的 Mesh/Material/Texture；后续新图案沿此流程接入，并按要求提升 ContentVersion。

### 校准与验证

- **源码模拟**：84 项通过，直接编译实际效果/定义/注册器源码；覆盖 1 秒等待期间不连发、空中不续跳、原请求/力度恢复、所有权/异常清理和新名称/内容版本。Unity/IL2CPP、物理、网络仍是替代对象。
- **资源检查**：7 项通过。空白透明图案不改变任一底图像素；用户图案只改变正面范围；最终全 mip 链 Alpha=255；RGBA 行序/通道和 PNG 一致；原图复制字节一致；误导出不透明参考底图会被拒绝。没有用这些离线检查替代游戏显示验收。
- **编译/部署**：最终 Release/x64/net6.0，0 警告、0 错误。本机输出与节点部署 SHA256 均为 **`4E6EE285D35E1F5080E3D000DA3577114F699C1D27A0D776DD6E73F5BEA9469A`**。
- **校准历史**：首个内部试调构建上限 4.0、SHA256 `B21C24C28FD7FD57EF767DA9018887BC3009A7A165F091039424D4B653BAE667`，会话 `test-20261013-142854146` 实测约 0.622～0.623 米，超过目标，未作为最终结果。正常关闭后重新编译部署上限 3.6 的构建。
- **最终实际单人房**：会话 `test-20261013-143652898`、实际 PID 10100，日志确认 0.2.12、专属 `artwork=jumping`、匹配 manifest、`READY: originals=20, mods=1`。暂停前连续段中，33 次小跳采样均为 **0.511 米**；4 次正常跳为 1.399 米；3 次稍高跳为 1.707～1.708 米。39 次落地至下一次起跳的游戏时间采样为 **0.999～1.019 秒**，中位数 1.011 秒；这是帧采样与日志舍入结果，配置等待为 1 秒。
- **图案实机**：陈列、持有、放下均可见；弹簧由左向右、圆点在右上，与用户图案方向一致，纸底和胶带保留。这个图案的可见性/方向已核对，不宣称精密测量了所有边缘位置与胶带遮挡。
- **暂停/释放**：打开 F8 后四秒起跳计数 `40→40`，关闭后继续；G 放下后四秒 `46→46`，出现 Effect stop。对应记录为 `.local/verification/spring-0212-pause.json`、`spring-0212-drop.json`。最后重新拿起，游戏保持打开供用户试手感。

最终高度/间隔汇总见 `.local/verification/spring-0212-foxy-result.json`，资源检查见 `.local/verification/note-0212-assets.json`。原始截图、日志、输入回执位于本地 `人类工作间/接收文件/狐狸节点验证_20261013/` 的上述会话目录。用户交付目录新增合成图集预览并更新模板说明；原文件与旧版本历史证据保留，未暂存、提交或推送。

**未验收边界**：真实双端/晚加入、第二种不同图案同时显示、低顶/蹲下、坡面/移动地面、重力事件、死亡/换角色及其他 MOD 并用。半米是当前平地目标和采样，不保证特殊地形/重力下强制精确到厘米。原版 Steam/interop/渲染警告未在本轮修复。

## 2026-10-13：0.2.11 小跳加高、减频与绘画模板

用户反馈小跳过快、过密，本轮将小跳倍率调整为 0.36～0.40、带原版倍率后的起跳速度上限由 1.8 提高至 2.4，小跳落地等待由 0.10 秒延长到 0.45 秒。四小跳后正常跳、五小跳后稍高跳的编排不变；正常跳保持 1 倍、稍高跳保持 1.12 倍，二者落地等待仍为 0.18 秒。继续沿用 0.2.10 已核对的原生动作入口与单次力度恢复机制，不增加状态或消息。

插件/程序集升为 **0.2.11**，`jumping.ContentVersion` 从 5 升至 **6**，manifest 为 **`29E29FFDCF275303`**。只有狐狸节点测试副本部署本轮 DLL，本机正式游戏仍保留此前版本；未暂存、提交或推送。

### 本轮证据与边界

- **编译**：Release/x64/net6.0，0 警告、0 错误。
- **源码模拟**：82 项通过。在既有原版请求、恢复和生命周期检查上，增加较长落地等待期间不连发的断言，并更新小跳上限与内容版本兼容检查。模拟中的 Unity/IL2CPP、物理和网络仍是替代对象，不等于游戏验收。
- **部署**：本机输出和节点部署 DLL SHA256 均为 `CBD8BC667D0C1CEBA7AED31D3D2348915135D5C6C6E431FAC4CB520DC020B644`，节点回执版本 `0.2.11.0`，旧 DLL 已备份。
- **真实单人房**：`test-20261013-135427604` 的启动日志确认 0.2.11、便签加载、匹配的 manifest 和 `READY: originals=20, mods=1`。E 拿起后产生真实原生起跳与角色高度采样，无需发送跳跃按键。34 次小跳采样中位数 0.139 米、范围 0.090～0.145 米，多数约 13～15 厘米；4 次正常跳为 1.399～1.400 米，3 次稍高跳均为 1.707 米。小跳力度 2.250～2.400，正常 6.250，稍高 7.000；每次读取的原力度均为 6.250，没有累积缩放。完整范围保留异常较低的一次采样，不把它静默排除。
- **重开**：用户误关游戏后，确认该进程已退出，按授权重新打开同一构建。新会话 `test-20261013-140606008`、实际游戏 PID 11140，再次确认 READY、E 持有和小/正常/稍高三档起跳；截图确认便签可见。测试游戏保持打开，角色持有雕像供用户查看。
- **边界**：0.45 秒是代码中的落地等待配置与模拟验证结果；日志没有完整墙钟起跳时间戳，不据此宣称精确测量了每次总周期。高度是此房间角色位置采样，不能外推所有地形/重力条件。本轮没有重新完成 F8 四秒暂停计数或放下四秒停止计数，历史证据属于 0.2.10；真实双端、晚加入、低顶/蹲下、移动地面、死亡/换角色及其他 MOD 并用仍待验收。原版/Steam/可选 interop 警告不记为已修复。

采样汇总在本地 `.local/verification/spring-0211-foxy-result.json`；原始日志、输入回执和截图在 `人类工作间/接收文件/狐狸节点验证_20261013/` 的上述两个会话目录，均不进入 Git。

### 便签绘画方案与实际交付

从现有 `Assets/StickyNote/note.mesh.json` 核对纸面正面占独立矩形 UV：U 约 0.020310～0.982463，V 约 0.010619～0.871263。现有整张纹理为 512×512，胶带、背面和边缘另有区域，因此不应直接把用户的正面画布覆盖整张图集。

交付 `人类工作间/接收文件/便签图案绘画模板_1024_v1/`：1024×1024 全透明 RGBA PNG、从现有正面提取的单独参考底图、使用说明与拟定映射约定。检查确认空白模板所有像素 Alpha=0。参考安全边距只是绘画建议，尚不是精确胶带遮挡蒙版。

建议后续统一流程为透明图案 `<key>.png` → 打包时缩放/合成到正面矩形 → 按雕像定义加载；纸张仍为不透明材质，保留现有胶带/背面/边缘。可以复用便签模型，不要求每种雕像另做模型。**本轮没有实现按 key 自动合成/加载，也未将模板声明为游戏显示验收通过的导入契约**；接入时需要上下左右标记测试图校准方向、比例与胶带遮挡。1080×1080 也可作为绘画原图，1024 是此模板的统一尺寸。

## 2026-10-13：0.2.10 弹簧节奏与狐狸节点实测

按用户要求，将 `jumping` 从每五秒一次改为连续轻小跳，穿插正常跳或略高跳。**0.2.10 已部署狐狸节点并进入专用测试房**；本轮图形操作均使用节点已有交互助手，未抢占本机游戏窗口。保留便签、自有配置 ID / Mirror assetId、Score 模板、Slippery 入池标记和自然生成规则；ContentVersion 从 4 提升为 5，兼容 manifest 为 `EA99D5E5B8C4342C`。

### 原生路径与实现

当前 GameAssembly SHA256 为 `FF7BE800E1BF47E078D2A2F37129E7B113EC4E5691144FA7D80A6E884658ED4B`。发现 2026-10-11 的 `code-addresses` RVA 已无法对应该文件，本轮重新用 Cpp2IL 提取到 `.local/research/code-20261013/`，再核对原生调用；旧地址反汇编结果没有用作实现依据。

当前原生路径为 `OnJumpInputPerformed_OnceState`（RVA `321DFD0`）→ `RequestJump`（`2FA6B90`）排队，物理更新的 `HandleJumpRequest`（`2FA3740`）检查落地/请求状态，经原版动作与 NetworkAnimator 后，用 `jumpForce * JumpForceMultiplier` 调用 `Rigidbody.AddForce(..., VelocityChange)`。原版起跳状态继续处理体力。MOD 仍只由持有者所属客户端调用原版输入，不直接写刚体速度、跳跃状态或体力；动作/运动继续走原版同步，真实双端尚未验收。

节奏使用固定编排：四次小跳后一次正常跳，五次小跳后一次稍高跳，再循环；小跳内部有轻微力度变化。小跳倍率约为原力度的 0.27～0.32，并将带原版倍率后的起跳速度上限设为 1.8；正常跳保持原力度，稍高跳为 1.12 倍力度。小跳落地后只等待 0.10 秒，正常/稍高跳后等待 0.18 秒；空中、原版动作尚未结束、禁用控制和菜单期间不推进下一次自动请求。没有新增各客户端独立随机结果或自有网络消息。

输入回调接受请求后，只记录该角色指针、原生请求时间戳和节拍。在匹配的 `HandleJumpRequest` 调用内临时设置角色自身 `jumpForce`，Harmony Finalizer 恢复自己的写入；另一 MOD 已写入不同值时不覆盖它。持有者/物品/控制权变化、死亡、原生请求被替换、超时或效果清理时撤销待处理记录；原生调用异常时恢复参数并停用本次效果直到释放。没有跨帧保持被修改的跳跃力度，也不通过原版重力接口改其他倍率。DebugMode 记录实际起跳力度与落地前观察到的最高角色位置。

### 验证

- **编译**：Release/x64/net6.0，0 警告、0 错误。
- **源码模拟**：直接编译实际 JumpingIdol、定义和注册器源码，73 项通过。覆盖小跳上限、正常/稍高节拍、落地/空中等待、停顿帧不连发、既有手动请求不被修改、原版拒绝后的重试间隔、菜单计时暂停、放下/死亡/请求替换/超时、非拥有者、原生异常恢复、其他 MOD 的不同写入保留、旧定义兼容及外观生命周期回滚。模拟中的 Unity/IL2CPP/物理与网络为替代对象。
- **节点部署/启动**：固定公钥身份核验为 FOXY-NODE/FOXY，交互桌面 `Default`。远端基础游戏/interop 与本机哈希匹配，部署回执确认 `0.2.10.0`；本次会话 `test-20261013-132544303`，实际游戏 PID 14952，日志确认便签加载、注册/握手、`READY: originals=20, mods=1`。
- **节点实际跳跃**：通过原版 E 拿起 `netId=97`。没有发送跳跃按键，连续记录小跳、正常跳、稍高跳的原生起跳及角色位置。当前平地测得小跳约 6～10 厘米、正常跳约 1.4 米、稍高跳约 1.7 米；日志每次仍读到原力度 6.250，三档分别约 1.688～1.800、6.250、7.000，未累积缩放。位置采样数值用于此场景观察，不是所有重力/坡度下的理论最大高度。
- **节点暂停/释放**：F8 打开后的四秒内起跳计数 `209→209`，关闭后继续增长。G 放下后四秒计数 `462→462` 且记录 Effect stop；再次 E 拿起后出现新的 Effect start，节奏从小跳开始。回执与计数保留在 `.local/verification/spring-foxy-pause.json` 和 `spring-foxy-drop.json`。
- **边界**：没有完成真实双端、晚加入、低顶/蹲下、斜坡/移动地面、重力事件、死亡/角色替换或与其他 MOD 并用的完整游戏验收。没有将节点 Steam 初始化错误、缺失可选 interop 模块及原版渲染/组件警告描述为全游戏无异常。

本机输出与节点部署 SHA256 均为 **`F66C10022887B4CEDBEA4627BA45C1CA5027028F58844F6AC8C94B633B22E38C`**。实际输入回执、日志和截图在 `人类工作间/接收文件/狐狸节点验证_20261013/test-20261013-132544303/`；旧插件由节点部署脚本备份。本机正式游戏目录仍保留此前版本。按用户要求，测试完成后狐狸节点游戏保持打开，角色再次持有弹跳雕像供查看；未暂存、提交或推送。

## 2026-10-13：可选远端工作站与无 Steam 单人测试

按用户授权从本机维护工作区连接其副机，复用已有固定公钥 SSH 和交互桌面助手；机器身份/账号/会话核验通过。节点资料、游戏副本、脚本和回执均保留在本机忽略目录或副机，不成为其他作者的构建依赖。共享 AGENTS 只增加可选读取 `.local/WORKSTATION.md` 的通用规则；原来的本机 Build/Deploy 行为不变。

**本轮没有修改或重新编译 MOD 玩法源码**：部署既有 0.2.9.0，源/目标 DLL SHA256 均为 `B96EBB06A9F3EABA60967489AC51692A74B0BD3FD4B0053EB9F7942C9FA4405D`。独立游戏/BepInEx 基础包和关键程序集经散列核验，未复制本机存档或 Steam 认证资料。

**实际副机启动对照**：首次直接启动加载 MOD 并登记雕像后自行退出，退出码 0，未出现测试房 READY，日志未明确说明退出原因。第二次只在独立副本加入内容为 `3883570` 的 `steam_appid.txt`；SteamAPI_InitEx 明确报告无法确定 Steam 客户端安装目录，但原游戏继续运行，经原版 KCP 创建单人测试房，日志确认 `READY: originals=20, mods=1`。未替换 Steam DLL、未修改游戏二进制或 SteamManager，不能把 App ID 提示文件说成 Steam API 的替代实现。

**实际副机图形与交互**：成功回传游戏窗口截图；F8 面板、鼠标点击传送到 `jumping`、转动视角、E 拾取和 G 放下均有画面或原生日志证据。持有时记录多次每五秒的原版跳跃请求，放下记录 `[Effect stop] netId=97`。本轮没有录制连续运动视频，不单凭请求日志再次宣称完整跳跃动作验收。初期前台检查曾拒绝输入，保留保护后正常激活精确游戏窗口再核验；通用瞬时键未打开 F8，项目辅助改用 120ms 按键后面板实际出现。鼠标视角只在指针受限于游戏窗口内时执行。

本地 `.local/foxy-node/` 提供远端 Deploy、Start-Test、Game-Input、Collect、Stop-Test；使用主机现有编译脚本，通过节点原有交互任务启动，日志/截图回传 `人类工作间/接收文件/狐狸节点验证_20261013/`。实际成功会话为 `test-20261013-123652302`；人类说明见该交付目录的《狐狸节点测试方案.md》。脚本回执、画面和原生日志分别核验，不把派发或输入注入成功直接当作游戏结果。

**边界**：此结果仅覆盖当前游戏构建与 0.2.9 的专用单人测试房；Steam 大厅、真实双端、自然探索抽中、晚加入、断线重连和所有雕像玩法未在本轮验收。Steam 初始化错误及原版场景/组件警告仍保留，不声称日志全无异常。游戏更新后须重核基础文件/interop 及该启动路径，不安装额外模型代理或更改节点系统安全设置。

**收尾**：持有期间累计 23 次跳跃请求，放下后为 0 次新增。通过副机交互桌面对精确游戏窗口执行 Alt+F4，确认本次进程已退出并收集最终日志；没有取得退出码，不将其记为 0。SSH Session 0 无法直接取得交互主窗口，关闭流程和其他图形操作均保留在已有交互助手中。

## 2026-10-13：测试房紧凑陈列与 0.2.9 衔接核对

本任务开始时 fetch 远端，`HEAD` 与 `origin/main` 都是 `63d4362`，没有待拉取的新提交，保留工作区已有修改。紧凑陈列最初以 **0.2.8** 编译、部署和实测；另一个任务随后在同一工作区加入便签与弹跳效果并发布 **0.2.9**。按用户要求暂停后恢复，本轮核对当前 0.2.9，确认陈列与新外观已共存，没有回退另一个任务的实现。未暂存、提交或推送。

### 分区与原版基座

- 原版清单继续去重并排除实际为怪物的 WanderingDice，20 种各一只。后部两排各 10 个，横向间距 2.2 米、排距 3.2 米；房间局部纵坐标为 28.8 米和 32 米，玩家入口空地保留。
- 中部从 `Registry.Entries` 按稳定 key 排列，每种生成一只，使用同一生成流程及原版 Skin/CurrentValue setter；不添加逐种陈列分支。当前只有带便签的 `jumping`。不足一排时居中，前三排可容纳 30 种；更多类型时向后延长房间并保持与原版区的分隔。布局支持每区至多 128 种，超出时明确报错。
- 中部实例通过 `Sync.Track(idol, true)` 计入每类型一只的调试上限。已取走但仍存活的展品不会补刷；出售/销毁后才允许手动生成。新定义需完成注册、编译部署并重启专用测试游戏，不在已运行的房间热追加。
- 从原版序列化资源确认 `PF_SocleStatue` 的视觉为 `SM_SocleStatue` 与 `M_SocleStatue`，网格尺寸约 **1.439 × 0.602 × 2.074 米**；来源与 Addressables key 见 `research/test-room-pedestal.json`。基座之间横向净距约 0.761 米、纵向净距约 1.126 米。模型和材质只读共享，测试房创建自己的视觉对象及静态承托盒，不克隆来源房间或附带玩法/网络脚本。
- 首次 0.2.8 实机发现初始房没有加载此基座，直接扫描已加载资源会失败。修正为异步请求原版 Addressables 的模型和材质，等待成功并校验名称/尺寸后建房；两个 handle 持有到测试房清理，失败/超时同样进入清理。没有把原版资源二进制打包进 MOD。

### 编译、模拟与实机边界

**0.2.8 编译/部署**：Release/x64/net6.0，0 警告、0 错误；最终通过实机的 DLL SHA256 为 `64A2B8BC598A25934E8890ED66FB44A3F485BBF37FF706218CFEDA0327A58EB2`，后续 0.2.9 部署前的备份仍保留。该版本已实测原版基座显示、后部 20 种和中部 1 种生成、F8 重复生成拦截及传送到中部展位。证据为 `.local/logs/test-room-028-final-bepinex.log` 与 `人类工作间/接收文件/测试房_紧凑陈列_20261013.png`。

**恢复后的源码模拟**：直接编译当前 `TestRoomLaunch`、`TestRoomLayout`、`TestRoomMessageFilter` 源码；专用参数模式 156,803 次断言、普通模式 156,781 次断言通过。数量包括多组布局的逐对净空检查，不表示这么多个独立游戏场景。覆盖原版/空 MOD 清单、未满行、多行扩展、两区隔离、墙/入口净空、资源等待期间只发起一次加载、失败/超时清理，以及既有启动和精确消息过滤用例。Unity、资源加载与 Mirror 在模拟中是替代对象。

**当前 0.2.9 核对**：源码输出与游戏目录 DLL 均为 `0.2.9.0`，SHA256 均为 `B96EBB06A9F3EABA60967489AC51692A74B0BD3FD4B0053EB9F7942C9FA4405D`。当前游戏日志确认便签资源成功挂头、注册清单 `types=1`、原版 20 个不同配置/实例和中部 `jumping` 各一次生成；原版生成记录的两排间距为 3.2 米、每排十个间距为 2.2 米，最终 `READY: originals=20, mods=1`。恢复后截取实际游戏窗口，确认后部雕像与石质基座、中部便签展品仍可见；打开 F8 显示调试实例 1，再点击生成后已有实例警告计数 `1→2`，没有新增雕像，面板正常关闭。当前 BepInEx 日志无 Error/Fatal。

恢复后只补齐记录及资源来源字段、复跑布局模拟、核对当前会话；未修改 0.2.9 玩法或外观源码，也未重新编译/部署游戏插件。本版本便签持有/放下及周期跳跃的编译和实测记录见下一节。最新布局证据在 `.local/verification/test-room-layout-029.json`、`.local/logs/test-room-029-layout-bepinex.log`、`.local/logs/test-room-029-layout-player.log`；交付截图为 `人类工作间/接收文件/测试房_便签与分区_20261013.png`。

**未验收**：多种真实 MOD 同时陈列（当前只有一种，未来容量仅做布局模拟）、全部原版效果、完整门口步行往返、长时间运行与真实双端。画面中的背景墙仍有局部黑色区域，已记录待查；不据当前雕像/基座可见声称全部墙面渲染问题已修复。Unity Player.log 的原版警告与历史异常不改写为全游戏无异常。

## 2026-10-13：0.2.9 便签弹跳雕像接入与本机游戏验证

**0.2.9 已编译、部署，并通过本机专用单人测试房的外观/持有/暂停/放下检查。** 本轮沿用当前工作区 0.2.8 的台座异步加载和“后部原版、中部 MOD”陈列，不为弹跳雕像添加单独的陈列分支。真实双端、晚加入及本版本自然探索中的实际抽中仍未验收。

### 条纹原因与资源处理

模型师的便签/胶带闭合网格有向体积为正，没有非双面邻接边；192 个角点法线与面方向点积最低约 `0.999324`，没有反向角点。这些结果排除本次交付中明显的面翻转或法线翻转。

对原 `.blend` 做同网格、同 UV、同照明的单输入对照：断开高度图或法线图后仍有三角明暗条纹；**仅断开接往 Alpha 的连接后条纹消失**。这把此前 Blender 预览的条纹定位到原材质的透明度分支；没有据此把任何其他材质/渲染器的所有问题概括为同一原因。原模型师 ZIP/FBX/工程保持原样。对照图与游戏截图在本地 `人类工作间/接收文件/弹跳雕像游戏验证_20261013/`。

将独立便签对象的变换烘焙到原版 `SM_IdolHead` 局部坐标，保留 64 三角面、原角点法线及 UV，渲染顶点按角点拆分为 192 个；不重算法线或改变原版雕像。使用交付的“不透明胶带”底色图，复制已加载的 `SG_VHS_Default` 材质作为自有副本，关闭借自原雕像的法线/环境/反射等贡献。便签仅增加 MeshFilter/MeshRenderer，随头部运动，不增加碰撞、刚体或网络组件。当前没有启用 Height、细节法线、半透明胶带或制作新的弹跳图案，512×512 图集也还不是面向绘画的最终矩形模板。

初次游戏验证在 `ImageConversion.LoadImage` 触发 `ReadOnlySpan.GetPinnableReference` 缺失异常，注册失败停止运行；不能用先前编译成功推断图片包装可调用。随后核对本机 Texture2D 的 `LoadRawTextureData(IntPtr,int)` 包装，改为预解码 RGBA32 完整 mip 链、固定托管缓冲区、通过指针/长度入口上传，调用后释放固定句柄。最终游戏日志确认贴图/网格加载和自有材质创建成功。

### 效果、注册与资源生命周期

- 升级现有 `jumping` 定义，显示名“弹跳雕像”，保留配置 ID `0x4D49000000000001`、assetId `0x4D490001`、Score 模板及 Slippery 入池标记；`ContentVersion` 从 3 提高到 4。最终兼容 manifest 为 `4AFFCBEC37F5060D`。
- 由持有者所属客户端每 5 秒尝试一次原版跳跃输入，动作和运动继续沿用原版同步。等待落地/已有跳跃结束；禁用控制或 F8 打开时不推进计时，不做补偿连跳。移除拿起即清空体力的行为；不写速度、跳跃状态或体力。固定间隔不引入独立随机结果/协议。
- 新增可选 `IdolDefinition.CreateVisuals`：在自有预制体上创建资源并返回 `IDisposable`；构建过程抛错前由工厂清理自身已分配资源，返回后的注册失败由注册器回滚，正常卸载时统一释放。旧 `ConfigurePrefab` 和未提供此回调的定义继续可用。这是外观资源生命周期入口，不是通用 FBX 自动加载器。
- 便签的网格/底色内嵌 DLL，所有端安装兼容版本即可取得资源，不依赖“人类工作间”路径。原版材质、配置、预制体及其他 MOD 类型不被替换。
- 自然生成入口及规则没有另增数量、重置权重或扩大池范围；CFG 的 `NaturalSpawning=true`，注册就绪后继续按 Slippery 标记参与原版候选。本轮测试房禁止原版事件，所以没有把陈列生成称为自然抽中。

### 检查与实际游戏证据

- **编译**：`scripts/Build.ps1`，Release/x64/net6.0，0 警告、0 错误。
- **源码模拟**：直接编译产品 `JumpingIdol.cs`、`IdolRegistry.cs`、`IdolDefinition.cs`，33 项通过。覆盖五秒间隔、菜单暂停/恢复、空中等待、已有跳跃去重、原版动作拒绝后不逐帧重试、重新拿起/独立持有者状态、稳定 ID/入池标记、兼容清单变更、外观资源生命周期、注册失败回滚、重复卸载以及旧定义无外观回调的兼容。Unity、IL2CPP、动作入口和网络在该模拟中均为替代对象，不构成原生或多人验收。
- **游戏注册/陈列**：通过桌面“测试”启动实际安装，日志确认 0.2.9、便签挂头成功、192 角点/64 三角面、原版游戏 shader、兼容握手和 `[Registry] ready`。测试房 `originals=20, mods=1`，`jumping` 位于中部，实例 `netId=97`、皮肤 Bronze、价值 100。
- **游戏外观/持有**：截图确认便签在陈列和持有时可见，当前不透明版本没有此前的明显三角条纹。E 拿起后出现 `[Effect start] jumping: netId=97, holder=73`；不按跳跃键连续录下约 8.95 秒画面，捕获与周期请求对应的视角起落。日志多次记录五秒原版跳跃请求。G 放下后出现 `[Effect stop] netId=97`，附件仍可见。
- **游戏暂停/放下**：打开 F8 的六秒内，请求计数 `45→45`；关闭后恢复，计数到 47；放下后等待约六秒，计数 `47→47`。原始计数在 `.local/verification/note-game-hold-release.json`。
- **未执行/未确认**：当前版本自然实例抽中及出售、全部动作边界、死亡/角色替换、长期资源清理、远端/晚加入/重连；游戏内 `visual` 三模式命令的自动输入没有取得切换成功日志，因此不把该对照计作已实测。条纹的单变量定位来自 Blender 对照，游戏证据只覆盖默认不透明材质。

最终输出及部署 DLL 均为 `0.2.9.0`，SHA256 **`B96EBB06A9F3EABA60967489AC51692A74B0BD3FD4B0053EB9F7942C9FA4405D`**。更新前正常退出专用测试会话，旧插件由 Deploy 脚本备份；未修改模型师原包，未暂存、提交或推送。当前游戏保留在测试房，测试完成时已经放下雕像。

## 2026-10-13：模型师便签交付检查

本轮只检查用户提供的本地 `人类工作间/模型/便签.zip`，没有修改原包、玩法或部署。检查副本、JSON 证据、中文报告与三张静态预览放在 `人类工作间/接收文件/便签检查_20261013/`，该目录继续忽略 Git。

`bianqian.fbx` 仅含独立附件 `Cube.002`，36 顶点、64 三角面，纸片/胶带为同一对象中的两个网格部分。`Invokyr_Score_Reference.blend` 保留原版身体、头、眼睛以及该便签。逐部件对照前轮原版 Unity 网格数据：三部件顶点位置误差和 UV 误差均为 0，三角面索引相同；没有重做或修改这些雕像几何。FBX 和 `.blend` 中便签的顶点/三角面数据散列相同。

资源尚需整理：便签未挂在头部下、无约定附件根节点且缩放未归一；贴图为 512×512 的纸面/胶带共用图集，未附分层绘画模板；主目录 Normal 是纯平图，带细节的法线另在 DirectX 图及子目录版本中；`.blend` 的反相 Alpha 连接没有保留到 FBX 导入材质。按交付主目录材质渲染可见纸面三角形明暗条纹，原因未进一步定位。原版雕像底色仍依赖前轮参考包。以上均为文件/Blender 观察，不是游戏显示或联机验收，也没有执行修复。

## 2026-10-12：0.2.7 单人雕像测试房与桌面入口

已快进同步远端 `63d4362`（属性继承/修改示例和文档，无新增玩法），保留本地未提交的资源导出记录与工作间规则。本轮在此基础上实现并部署 **0.2.7**；以下是新增单人测试入口的验收，不能替代原有自然生成或双端验收。

### 行为与本机交付

工作目录未发现游戏副本，按用户授权使用本机 Steam 安装。桌面“测试”快捷方式使用原游戏 EXE 的图标，调用 Steam `-applaunch 3883570 --ces-test-room`；CFG 的 `[Debug] DebugMode` 与 `TestRoomEnabled` 均开启，`WindowKey=F8`。只有两个开关和参数同时存在时，测试启动及相关补丁才生效。旧 DLL/PDB、CFG 和安装记录留在 `.local/backups/`、`.local/test-shortcut-installation.json`，未进入 Git。

`Testing/TestRoomLaunch.cs` 沿已核对的原版 `VhsNetworkManager.HostKcp` / `CommandHostKcp` / Mirror `StartHost` 流程建立单人房间，等待菜单加载、注册表、角色控制权、初始房和门就绪。创建入口有 150 秒超时和阶段日志；每进程仅自动创建一次，失败清理本次创建内容，断开后不自动重建。测试模式限制一名玩家并拒绝远端连接，地形不作为多人地图发布。默认 Steam 启动不会自动创建这个测试房。

`Testing/TestRoomScene.cs` 在初始房的一扇门外建立 28 米宽、30 米深的陈列区与 6 米连接走廊，配置独立的原版 `Room`/`Zone`、碰撞、邻接关系和灯光。按原版 `scoreTools` 清单去重，排除 MOD 配置与实际为 `MonsterEntity` 的 WanderingDice。其余 **20 个配置各生成一只**（包含 MissingPiece 和 Final 两种特殊拼图条目），五列、4.8 米间距；使用原版 `SrvSpawnToolOnGround` 及 Skin/CurrentValue 同步 setter 初始化，不改自然池或原版模板。测试陈列不占 MOD 手动生成限额，也不自动补刷。

原版房门经原生解锁/打开接口处理，玩家经原版 `TargetTeleport` 移到测试房前端空地，落点先做角色胶囊检查。测试会话阻止骰子事件、常规怪物生成、区域回合伤害及 AutoSave/SaveToSlot/收藏写入；这不是对全部原版雕像效果或任意第三方 MOD 的防伤害保证。

### 资源和原生调用核对

- 当前游戏 Unity 为 `6000.3.14f1`。Logo 属于 Unity PlayerSettings 的启动屏；Unity 6000.3 的 `SplashScreen.Stop(StopImmediate)` 对应零参数 `CancelSplashScreen` 引擎绑定。本游戏 interop 裁掉了托管 SplashScreen 类型，使用 `IL2CPP.ResolveICall` 定位该绑定，未写硬编码地址。实机日志确认 `wasFinished=False, isFinished=True`。另保留 EnhancedFramework SplashManager 的空动画列表处理，继续其原有场景加载/状态清理；本次实机实际命中的是 Unity Logo 路径。
- 房间 shader 无法通过 `Shader.Find` 取得，最终复制已加载的原版 Score 材质作为自有副本。资源核对 `SG_VHS_Default` 的真实属性：颜色为 `_AlbedoColor`，`_Smoothness` 是 PhysicalMap 纹理，不是浮点粗糙度。房间副本使用白色底图、关闭原雕像法线/反射贡献并设置浅色墙地；不修改共享材质。指定测试主光源、全渲染层顶灯、环境补光并关闭该测试场景的全局雾，记录先前设置，清理时在仍持有对应值的条件下恢复。
- 首次实机发现对 `SrvBeginInternal(int, Nullable<int>)` 做 Harmony 包装会在 native→managed 参数转换中触发 Memmove 空引用。已撤掉此补丁，改为只拦截参数可用的三个公开 `SrvBegin` 入口；后续实机不再出现该异常。编译/模拟没有覆盖这个原生参数包装问题。
- 原版 Linked 的 `ToolExtensionLinked.OnStartServer` 启动 `WaitDiceInitCoroutine`，0.5 秒后直接向 `spawnTool` 父对象的缓存登记。独立地面生成时该父引用为空，会抛原版协程异常。测试模式仅在缺失该父对象时跳过该缓存登记协程；未移除 Linked 雕像或全局禁用其效果。最终启动日志未再出现该异常，绳索效果本身未验收。

### 精确提示过滤

用户明确要求只去掉“棋盘开启后将无法加入游戏”。已从本地化资源定位独立键 **`UI/lobby/waitPlayerBeforeStart`**，从原生代码确认 `UIManager.OnStartClient` 会把对应译文传给 `CliShowGlobalMessage`。补丁只在测试模式下、该键或当前语言对应译文完全相等时取消这一次显示；不隐藏 globalMessageUi、Canvas 或顶部容器，也不关闭其他消息系统。未来指向雕像的效果提示仍待实现。

### 验证结果与边界

- **编译**：`scripts/Build.ps1`，Release/x64/net6.0，0 警告、0 错误。没有随插件复制游戏/工具依赖。
- **源码模拟**：直接编译产品的 TestRoomLaunch、TestRoomLayout、TestRoomMessageFilter 源码，使用 Unity/Mirror 替代对象；测试参数模式通过 10,821 次断言，普通启动模式通过 10,803 次断言（包含布局各组合的逐对距离检查）。覆盖开关/参数门禁、等待/已有会话、单次建房/传送、超时、构建/落点失败清理、断开后清理、不补刷，及只过滤指定提示、保留拾取/背包/未来雕像说明等其他文本。模拟不证明原生调用和渲染可用。
- **实际游戏**：通过桌面快捷方式启动当前 Steam 安装；最终日志确认 Unity Logo 从未结束变为已结束、KCP 本机连接和 MOD 握手、20 个互异原版配置创建、门打开、传送到 `(-16.00, 0.15, -0.48)`、角色血量 100，并输出 `[Test room] READY`。观察截图确认身处陈列区、雕像排列可见，照明已按用户反馈加强，指定顶部提示消失而血量/背包/其他 HUD 保留。最终 BepInEx 日志没有 Error；Unity Player.log 仍有游戏自身的缺失脚本/渲染资源及 GraphicsBuffer 警告，未将其改写为“整个游戏无异常”。
- **未执行**：逐种原版效果、拿放/出售及特殊拼图的完整验证；初始房门实际步行往返；长时间测试、退出房间后照明/资源恢复、房间内重连和真实多人验收。该测试房当前明确限制单人。

最终 DLL 与游戏目录哈希一致：版本 `0.2.7.0`，SHA256 **`11A84ECCD06CCADEF5BBDFE8314E5E9218631D659BD886D0A8F4D9852830CE2D`**。实机记录在 `.local/logs/test-room-final-bepinex.log`、`.local/logs/test-room-final-player.log` 和 `.local/verification/test-room-game.json`；最终截图交付在 `人类工作间/接收文件/测试房_20261012.png`。本轮未暂存、提交或推送修改。

## 2026-10-12：原版雕像模型师参考包

本轮完成资源导出和建模软件回读核验，插件版本仍为 0.2.6；未修改玩法、编译、部署或进行新外观的游戏/联机实测。

从当前本机 Invokyr 的 Addressables 资源实读 `Data-Treasure-Score`（配置 ID `7331884207844971565`）对应的 `PF_Treasure_Score`。Unity 版本由本机 `globalgamemanagers` 与 `UnityPlayer.dll` 确认为 `6000.3.14f1`；bundle 省略的版本字段使用该已确认版本解析。用 UnityPy 1.25.4 提取身体、头部、眼睛三个静态网格及原始 UV0、法线和局部变换，合计 3,962 顶点、4,600 三角面。原版隐藏的骰子替身、VFX、碰撞和脚本不进入建模参考。

四张原版贴图为 `T_Idol_Bronze_ALB` / `T_Idol_Bronze_ORMN`（2048×2048）和 `T_Idol_Eyes_ALB` / `T_Idol_Eyes_ORMN`（512×512），均解码为 RGBA PNG。保留原材质属性与资源来源；未从 ORMN 名称推断通道意义。Blender/FBX 使用原版底色图与简化预览材质，没有重建游戏自定义 shader、眼睛状态或皮肤切换效果，预览图不是游戏截图。

Blender 5.2.2 LTS 后台生成 FBX 与含内嵌底色图的 `.blend`，保持身体/头部/眼睛层级和对位。完整尺寸约为宽 0.426054 米、深 0.378789 米、高 0.717668 米。另一个全新 Blender 进程重新导入 FBX，逐部件对照原始 Unity 网格/变换：顶点与三角面数量相同、层级正确、最大顶点位置误差 `1.1920928955078125e-7` 米、UV 误差 0、法线方向核对通过、外部底色贴图路径可解析。另行打开 `.blend` 确认三个网格和两张内嵌底色图完整。12 张 PNG 完整性检查及 ZIP 内文件 CRC/SHA256 检查通过。

交付在本地 `人类工作间/接收文件/Invokyr_Score_Reference_20261012/` 及同名 ZIP，含模型、贴图、四视图/总览、512×384 便利贴绘画辅助图、中文制作委托和技术核验记录。ZIP 共 22 个文件，SHA256 为 `94c16821546c1e73496fe50bc36d47c5018ad4b3c5aa92438978060e59339551`。整个 `人类工作间/` 已加入 Git 忽略规则，并记录为用户原文件/杂物的保留目录；所有交付导出物放在其 `接收文件/` 下。未上传原版素材、暂存或提交修改。

下一步由模型师制作独立便利贴网格及 UV，以头部原点交付附件。便利贴资源加载、按定义 key 选图、材质染色和完整生命周期/联机显示仍未实现或验证；当前参考包不表示这些能力已经接入。

## 2026-10-12：0.2.6 调试闪退修复与循环定位

当前版本为 **0.2.6**，由用户手动部署并复测，反馈手动生成和传送正常，未再反馈生成闪退。Release 0 警告、0 错误；调试生成 20 项、循环传送 26 项、菜单/请求时序 12 项源码模拟通过。积分/出售显示、多场景边界和真实双端同步仍待单独验收。

### 0.2.5 自然生成的游戏证据

用户提供 `actually spawned MOD idols=[jumping x1]` 后，本机日志确认加载 0.2.5，在 eventId=41、seed=-1960054746 的 `ItemSet_Large` 批次中，筛选前候选为 `jumping(weight=100)`，服务端创建 netId=223 的 jumping，批次正常结束。Unity `Player.log` 也记录 `Item spawn: CalypsoExpansionSet.jumping`。此证据确认一次真实自然实体生成；用户因地图较大尚未找到实体，不能据此勾选可见、拾取、出售或多人验收。`OnStartServer` 日志中的 value=21 是该生命周期时刻的值，不作为后续皮肤分配后的最终售价。

### 调试生成崩溃的定位与修复

用户反馈 F8 菜单正常，点击生成后卡住并闪退。BepInEx 日志没有托管异常或调试生成完成记录，`Player.log` 也未记录具体崩溃栈；Windows Application 事件 1000 在 20:26:09 记录 Invokyr.exe / coreclr.dll、异常代码 `0xc0000005`、偏移 `0x1d1fdd`，事件 1001 对应 APPCRASH。未取得可还原调用栈的 dump，因此不能凭该事件唯一认定崩溃函数。

按 Unity IL2CPP/BepInEx 运行边界复核发现 **[CRASH]**：0.2.5 新增的 `TryGetIdolSkinData(out FIdolScoreMaterialData)` 调用使用了错误的生成包装。`../libs/vhs/Items/ItemDropManager.cs` 中包装把输出当作一个 `nint`，只有 8 字节缓冲区；该类型实际为原生值类型，包含 skin、skinValue、material 与 distribution。`GameAssembly.dll` 中 VA `0x182EBBA60` 的方法向输出写入 24 字节，成功路径在 `0x182EBBB4D`–`0x182EBBB5F`，未找到条目的清零路径在 `0x182EBBBB7`–`0x182EBBBBA`。两条路径都超出包装缓冲区，还会把写入的枚举/分值误当作对象指针。该调用存在已确认的内存破坏风险，与此次无托管异常的崩溃相符；此前 20 项托管模拟未覆盖这种封送错误，不能作为该原生调用安全的证据。

0.2.6 移除该调用，按索引读取 `idolsScoreData` 中装箱的皮肤条目，只读取 skin/skinValue 字段；随后使用原版 `Skin` 与 `CurrentValue` 同步 setter，不向原生方法传递该结构体。缺失资源不创建实体，初始化 setter 异常仍清理本次创建的实体。新增准备/已创建两个诊断边界，若仍有崩溃，可从最后一条 `[Debug spawn]` 判断进行到了哪里。新版实际 helper 的 20 项模拟通过，模拟中的危险查询和结构体初始化入口直接抛错，确保产品代码不会调用它们；随后用户在游戏中确认手动生成正常。

### 循环传送调试入口

F8 窗口新增 **Teleport to next MOD idol** 按钮，等价命令 `teleport`，只允许 `DebugMode=true` 的房主使用。每次请求从当前服务器实体表查找有效的自有配置类型，包含自然与调试实例，按 netId 循环，不长期缓存 Unity 实体引用。已出售/销毁/未激活实例不作为目标，角色/连接变化或关闭 DebugMode 时重置循环。

与生成操作一样，点击后关闭窗口、恢复自身暂停的输入，下一帧在角色/会话仍一致且控制可用时执行；超时或退出会话取消。目标旁边检查地面法线与角色胶囊空间，跳过无法站立的目标；无实例或没有安全落点时只报告原因。房主调用本机角色原版 `PlayerMovement.TargetTeleport`（VA `0x182FA5310`），其原生 TargetRpc 进入 `CliTeleport`（`0x182F9D760`），设置角色刚体位置/朝向并复位速度；不直接写玩家 transform 或增加自定义网络协议。原版运动同步和远端观察效果需游戏实测。

`IdolDebugTeleport.cs` 实际源码的 26 项模拟覆盖开关、房主/Ready/注册/控制/死亡门禁、目标顺序与循环、实例销毁、受阻/无地面、胶囊检查、普通雕像排除、连接变化及清理。`IdolDebugTools.cs` 实际队列/更新/输入方法的 12 项模拟覆盖关闭菜单后下一帧执行一次、关 DebugMode、角色/连接变化、控制锁、超时、结束会话和运行故障取消。模拟使用替代 Mirror 与物理查询，不证明真实碰撞场景、TargetRpc 传输或菜单显示已验收。

最终输出 `bin/Release/net6.0/CalypsoExpansionSet.dll`，版本 0.2.6.0，SHA256 `90FDC8D1CD233864754CEB54CBF94F036C3B401387F9A1C8019914AF9CA74283`。用户手动部署后反馈“测试没什么问题”，确认生成的雕像可以传送过去。这次反馈确认手动生成与传送入口可用；没有单独确认自然实例拾取/出售、积分显示、同类型自然与手动实例共存、多只目标循环、销毁/换场景边界或真实双端同步。

### 属性修改入口说明

`Idols/JumpingIdol.cs` 的 `Definition` 已标明第五个参数为配置/外观 `TemplateId`，并添加 `InitialWeight`、`ConfigureData` 与 `ConfigurePrefab` 的注释示例，列出当前模板的分值、难度、持有减速、缩放和质量。原来的继承在 `IdolRegistry.Create` 中完成：先 `Instantiate(template)`，再调用 `ConfigureData`；预制体也独立克隆后调用 `ConfigurePrefab`。示例未启用覆盖，实际属性、版本和效果保持现有值。README 同步说明直接编辑入口、基础分值与最终价值的区别以及构建生效步骤。

此补充只涉及源码注释和文档，`git diff --check` 通过；未重新编译、部署或新增游戏验收结果。

## 2026-10-12：0.2.5 自然池匹配与调试价值修复

0.2.5 阶段已定位 0.2.4 自然候选为空的原因并修正源码；以下保留当时的编译/模拟记录，随后自然生成与调试闪退的游戏证据见上方 0.2.6 记录。

### 自然候选为空的原因

本机 0.2.4 游戏日志记录三个完成的原版生成批次：`ItemSet_Large`（eventId=4、seed=814397706）、名为 `None` 的 ItemSet（eventId=13、seed=-51679945）和 `ItemSet_Chessboard`（eventId=2、seed=-417972950）。它们均为 `enabled=True`、原版筛选前 MOD 候选 `[]`、实际 MOD 实例 `[]`。此时尚未参与权重抽取，不能解释为权重低、基础积分低或“已经入池但没抽中”。`None` 是资源名称，不是空对象；空对象不会进入这条批次日志。

用 UnityPy 1.21.3 读取本机 822 个 Addressables bundle，得到 14 个 ItemSet，全部引用解析成功。池资源均在 `74e8a9e18f190d086b727c44312b5efb.bundle`，配置 ID 和候选数量快照见 [research/itemset-defaults.json](research/itemset-defaults.json)。资源证明：

| ItemSet | 原版雕像候选 | 原版雕像生成数量 | 是否含原来的 Score 模板 |
| --- | --- | --- | --- |
| `ItemSet_Large` | 14 种，含 Slippery | 最低/最高难度均为 8 | 否 |
| `ItemSet_Chessboard` | 10 种，含 Slippery | 最低/最高难度均为 1 | 否 |
| `ItemSet_Trivia` | 只有 Score | 最低/最高难度均为 3 | 是 |
| `None`、`ItemSet_Twister` | 无候选 entry | 无 | 否 |

原策略要求 entry 包含定义的 `TemplateId`。跳跃使用的 `Data-Treasure-Score`（`7331884207844971565`）只在答题池中，作为外观/属性模板有效，但无法匹配普通地图的雕像池。补丁已进入真实生成入口；本轮查到的失败点是模板和池的绑定。

0.2.5 为定义增加可选 `PoolTemplateId`，默认仍使用 `TemplateId`。跳跃保留 Score 的配置、外观与原有效果，使用 Slippery（`13601636613369722279`）作为自然池匹配标记；不复制 Slippery 的配置、预制体或效果。它会加入包含该标记的 9 个雕像 entry：Ballerina、Chessboard、WeepingRoom、Large_Demo、Boulder、Large、Jump、Inception、TrapCorridor；不加入 Trivia、Lava、End、None 或 Twister，不追加 bonus、额外 entry 或生成数量。

`PoolTemplateId` 必须非零且能从游戏雕像查找表中读取；它的实际匹配值加入兼容清单。跳跃 `ContentVersion` 从 2 升至 3，插件/程序集版本升为 0.2.5；配置 ID、assetId、GUID、消息号和协议格式保留。所有参与玩家需使用相同内容清单。跳跃仍未覆盖 `InitialWeight`，克隆配置的难度范围仍为 1–5、标签仍为 `[35]`，与资源中的 Blind/Slippery 相同；积分和重量不参与入池模板匹配。

### 调试雕像积分为零的原因

用户反馈手持和出售处均不显示积分，本机日志也记录多个 `[Debug spawn] value=0`。原生研究确认 `SrvSpawnToolOnGround`（VA `0x182EBA580`）只完成地面实例化与网络生成，跳过自然批次的皮肤/价值分配。`ScoreTool.OnStartServer`（`0x1832B5DF0`）负责登记实体，不补价值。

调试生成现先检查游戏皮肤表，生成后调用原版 `SrvApplyIdolSkin`（`0x182EB4710`）。模板明确指定的皮肤保留，未指定时使用 Bronze；当前资源表 Bronze=100、Silver=150、Gold=200、Crystal=300，实际赋值读取表而非硬编码。缺失皮肤数据时不创建实体，原生返回错误类型或初始化异常时清理本次生成的实体。

原生路径确认 `SrvApplyIdolSkin` 设置 Skin 和 CurrentValue；`set_CurrentValue` 与 `set_Network_currentValue` 同址 `0x1832B7190`，沿用 Mirror dirty bit `0x8000`。这些地址只对应当前安装的游戏版本。基础 `scoreBasePoint=1` 与实例的最终价值不同；自然批次仍使用原版皮肤/计价流程，未套用调试 Bronze 默认值。

### 验证结果与边界

- **源码与资源模拟**：直接编译当前 `IdolSpawnIntegration.cs`、`IdolDefinition.cs`、`Idols/JumpingIdol.cs`，使用上述 14 个真实池快照。重现旧策略仅匹配 Trivia，验证新策略匹配 9 个目标 entry；覆盖原版列表/数量/bonus 不变、重复接入、两类型共池、禁用/不兼容门禁、客户端不修改池、空 entry、嵌套批次、非 MOD 实例不统计、异常清理。
- **抽取模型**：9 个目标池，各测试难度 1–5 与 100 个 seed，共 4,500 个模型批次；默认权重下都能抽到跳跃，模型中的请求总数和每类型上限保持，跨批次计数重新开始、权重继续衰减。整套程序通过 13,660 次断言，其中包含重复批次检查。原生指令已复核难度筛选、权重累计抽取、选中后除以 2、局部类型计数与事件忽略上限的路径。
- **注册模拟**：直接编译当前 `IdolRegistry.cs` 与 `IdolDefinition.cs`，47 项通过；覆盖独立克隆与查找/prefab 表，以及新入池模板的零值拒绝、缺失/恢复、ready 清理、属性模板隔离和兼容清单差异。
- **调试价值模拟**：从当前插件源码抽取完整 `SpawnDebugIdol` 方法，20 项通过；覆盖真实表值使用、皮肤选择、模板不改动、多个独立实例、资源缺失及错误类型/初始化异常回滚。
- **编译**：`dotnet build .\CalypsoExpansionSet.csproj -c Release -p:DeployToGame=false --no-restore --nologo`，SDK 6.0.428，0 警告、0 错误，输出版本 0.2.5.0。

最终构建位于 `bin/Release/net6.0/CalypsoExpansionSet.dll`，SHA256：`4589363EEAB5E2B17DC883D4BD500742B58D69C96B4CCA5B9DB4F3E5ECACDA6B`。用户选择手动部署，本轮未覆盖游戏目录；手动部署和游戏复测待完成。检查时本机已有 DLL 的版本同为 0.2.5.0，但 SHA256 为 `570F962E5A79E250D10E83EE7761A7EBAC2F48CE73185AD9E3CC0DCA11B4A4E9`，不能仅凭版本号认定它就是最终构建。

本机模拟产物在 `obj/NaturalSpawnVerification`、`obj/RegistryVerification` 与 `obj/DebugSpawnVerification`。模拟替代了 Unity/Mirror 原生接口，抽取模型假定掉落点足够、事件/biome/玩家条件允许；没有复现完整地图物理、房间分布、原版随机数消耗顺序、Harmony IL2CPP 参数替换或网络传输。它证明当前源码能把新配置加入真实池快照并在已核对的规则模型中被选择，不证明实际游戏或联机已验收，也不提供真实出现率或对具体游戏 seed 的结果预测。

游戏复测时先确认 `0.2.5 loaded` 和 `[Definition] poolTemplateId=13601636613369722279`。普通地图生成批次应能看到 `[Pool]` 中的 `jumping(weight=…)`；候选存在而 `[Batch result]` 为空仍可能是正常未抽中。`None` 等没有目标 entry 的事件仍应为空。用 F8 窗口的 `spawn jumping` 验证调试日志的 skin/value、手持与出售显示，再验证自然实例、转交、晚加入及双方价值同步。

## 2026-10-12：0.2.4 项目配置与验证

0.2.4 阶段的项目名称为 **Calypso's Expansion Set**。入口是 `CalypsoExpansionSetPlugin`，程序集与命名空间是 `CalypsoExpansionSet`，BepInEx/Harmony GUID 是 `calypso.invokyr.expansionset`，公开注册入口为 `CalypsoExpansionSetApi.Register`。当前构建、部署和使用说明见 [README.md](README.md)。

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
- [x] 在游戏中生成测试雕像，并由用户确认持有期间持续跳跃（0.1.1 单人房主）。
- [x] 增加 cfg 调试开关、重复按键生成及多实例同步；模拟传输验证通过。
- [x] 0.2.0：独立配置/网络预制体注册、公开持有效果 API、三种执行端、自然池及实际生成日志、控制台命令与备用面板。
- [x] 0.2.0：实际同步源码 44 项、效果运行器 26 项、注册器 39 项模拟检查通过，共 109 项。
- [ ] 游戏内验收 0.2.0 独立类型、控制台/面板、自然生成和双端同步。

0.2.0 阶段的工程目标为 `net6.0` / x64，插件版本 `0.2.0`。跳跃内容位于 [Idols/JumpingIdol.cs](Idols/JumpingIdol.cs)，通用定义/注册/执行分别位于 [IdolDefinition.cs](IdolDefinition.cs)、[IdolRegistry.cs](IdolRegistry.cs)、[IdolEffectRuntime.cs](IdolEffectRuntime.cs)，测试及入池位于 [IdolDebugTools.cs](IdolDebugTools.cs)、[IdolSpawnIntegration.cs](IdolSpawnIntegration.cs)。`JumpingIdolSync.cs` 保留历史文件名，现在负责全部类型的兼容握手和服务器调试实例计数；新类型直接使用原版配置 ID、网络 prefab 和物品持有状态识别，不再重复同步一份 marker 集合。

**用户已实测的是 0.1.1 的单人生成和持续跳跃；0.2.0 的编译/模拟检查不能替代 Unity IL2CPP、原版注册与真实联机验收。** 主机和客户端都需要安装兼容版本及相同注册内容；DebugMode 只控制测试入口，客户端不开启也可使用雕像。

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
| 程序集名 / 根命名空间 / 版本 | `CalypsoExpansionSet` / `CalypsoExpansionSet` / `0.2.6`。 |
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

**验证状态：用户已确认 0.1.1 生成和持续跳跃成功；本机日志记录了 `netId=77`、`205` 的生成、持有、放下及销毁标识清理，生成时 `CurrentValue=0`。出售计价、体力边界与双端表现尚未确认。0.1.2 Release 编译通过，0 警告、0 错误；使用实际 `JumpingIdolSync.cs` 加模拟 Mirror 传输的 37 项检查通过，覆盖多实例保留、晚加入、重连、单实例清理、空快照、认证/连接筛选、非法快照拒绝和容量边界。该验证不替代真实游戏网络与物理测试。**

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

用户说的小局，通常在骰子确认造成新地图/区域生成后走这条链；**保留地图、恢复已有地图等事件不保证重新生成，不能按每次确认骰子计数。** 因此日志以真实生成批次、事件 ID、seed 为准。

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

已完成的代码与运行验证分别勾选；0.1.1 单人生成与持续跳跃已有用户反馈和日志证据。0.2.0 根据用户希望开发初期准备公共基础的要求，先实现独立注册与自然接入；各阶段的游戏验收仍分别保留，不把先写入代码视为阶段验收通过。结果记入第 10 节。

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

当前验证用例由用户确定：**玩家持有指定测试雕像时持续跳跃，使用原版跳跃入口和原版限制，并按多人同步设计实现。** 实现详见第 5.4 节。

- [x] 核实原版跳跃按键入口、请求消费、起跳体力处理和落地复位路径。
- [x] 仅处理 F8 生成的测试实例，通过所有者与实际手持对象判断生效范围。
- [x] 实现持有者客户端请求跳跃、放下后停止新增请求，以及实例/玩家/会话失效时的状态清理。
- [x] 用户确认单人房主持有时持续跳跃，日志记录多次拿起/放下；0.1.1 基础效果通过。
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
| 2026-10-12 / 插件 0.1.0 实测 | 插件加载、F8 回调；房主角色已能移动 | 加载和回调通过，但 `isLocalPlayer` 错误拒绝了生成 | 本机 `BepInEx/LogOutput.log` 与用户反馈 | 已在 0.1.1 移除该条件，修正后需复测 |
| 2026-10-12 / 插件 0.1.1 | 修正角色判断，加入服务器标识快照、持有者客户端跳跃及构建自动部署 | 编译与自动覆盖通过，0 警告、0 错误；部署 DLL 与输出哈希一致；角色与 Mirror 消息原生路径已核对 | 第 5.4 节、`JumpingIdolSync.cs`、`MoreIdols.csproj`、游戏 `BepInEx/plugins/MoreIdols.dll` | 重启后的生成和双端同步尚未验收 |
| 2026-10-12 / 插件 0.1.1 实测 | 单人房主生成、持有期间持续跳跃与销毁清理 | 用户确认跳跃功能成功；日志记录两次不同 netId 的生成、持有/放下与销毁同步标识清理 | 用户反馈、游戏 `BepInEx/LogOutput.log` | 体力/状态边界、出售计价、双端同步未逐项验收 |
| 2026-10-12 / 插件 0.1.2 | cfg 调试开关、重复按键生成及多实例同步 | Release 编译及自动部署通过，0 警告、0 错误，部署哈希一致；实际同步源码配合模拟 Mirror 传输的 37 项检查通过 | 第 5.5–5.6 节、`obj/SyncVerification`、游戏 `BepInEx/plugins/MoreIdols.dll` | 多实例物理位置、真实双端转交/晚加入/重连待实测 |

## 文档整理前的待办快照（2026-10-13）

<a id="todo-snapshot-20261013"></a>

以下保留本次整理前的 TODO 内容，作为历史快照，包含当时的完成标记和本机观测。最新状态只维护在 [TODO](TODO.md)，当前实现只维护在 [设计说明](docs/ARCHITECTURE.md)。这份快照不随后续开发刷新，不能把其中的“当前”或“尚待实现”解释为最新结论。

<details>
<summary>展开整理前的 TODO 历史记录</summary>

### 原 TODO 内容

当前源码/构建基准：Calypso's Expansion Set 0.2.13，2026-10-13；本机游戏已安装版本仍为 0.2.12，本轮未部署。本文记录当前事实、待决策项和待验证项；持续适用的开发规则见 [AGENTS.md](AGENTS.md)，Invokyr 游戏数据与开发记录见 [DEVELOPMENT.md](DEVELOPMENT.md)。

## 当前事实

- 0.2.13 将两种独立保留：`jumping` 恢复落地立即继续原力度跳跃，ContentVersion=8，保留原 ID；弹簧新增为 `spring`，独立 ID、ContentVersion=1，保留半米小跳目标和落地等待 1 秒。两种均不在拿起时清空体力，均通过各自 `CreateVisuals` 挂便签并暂用现有 `jumping` 图案，资源所有权独立。Release 编译 0 警告/错误，273 项源码/兼容/14 个候选池资源模拟通过；两种的实际游戏验收待执行。
- 同版本追补 DebugMode=false 时撤销待结束的弹簧落地测量；效果/注册/布局源码模拟现为 51 项通过，编译 0 警告/错误。DebugMode 控制重复的 `[Spring]` 日志，关闭 F8 不会关闭此开关；基本状态和异常日志保留。未部署或修改本机 DebugMode=true 配置。
- 已实测可选远端工作站的单人测试链路：固定公钥连接、独立副本/0.2.9 DLL 散列校验、无 Steam 客户端的 App ID 提示启动、测试房 READY、远端 F8/鼠标/拾取/放下和证据回传。具体机器配置在忽略的 `.local/WORKSTATION.md`，其他作者无需该设备；此项不替代 Steam 联机和完整玩法验收。

- 0.2.9 曾通过桌面“测试”实测：后部原版 20 种各一只、两排各 10 个，中部按注册清单自动陈列 MOD，每只均使用原版基座模型/材质。横向间距 2.2 米、排距 3.2 米；当时带便签的弹跳雕像已自动进入中部，F8 重复生成被拦截。0.2.8 的布局/资源加载与 0.2.9 的衔接均有实机日志和截图，启动/分区/净空/精确提示过滤模拟通过。Logo 停止、开门及仅过滤指定棋盘提示的行为保留；WanderingDice 是怪物，继续排除。全部原版效果、行走往返及多人场景仍未逐项验收。当前用户/公共桌面未找到这个快捷方式，本机 CFG 的 TestRoomEnabled=false。
- 0.2.12 已部署狐狸节点并实测。`jumping` 显示名改为“弹簧雕像”，加入用户临时图案，小跳目标约半米；校准后起跳速度上限 3.6，33 次平地高度采样均为 0.511 米，所有节拍落地等待 1 秒。正常跳约 1.4 米、稍高跳约 1.7 米。固定编排节奏，拥有控制权的客户端沿用原版动作/同步；仅在该次原版起跳内缩放力度并恢复，不写刚体速度/跳跃状态/体力。该版本复核 F8 暂停/恢复、放下停止，便签在陈列/持有/放下可见。ContentVersion=7；Score 模板、Slippery 入池标记、默认权重及稳定 key/ID 保留。该版本是一种 MOD，测试房后部原版 20 种、中部 MOD 1 种，后者计入调试限额。
- `NaturalSpawning` 默认 true，按 `PoolTemplateId ?? TemplateId` 匹配 entry。0.2.4 实测候选为空的原因是 Score 模板只在答题池中。0.2.5 修正后，游戏日志已确认 `ItemSet_Large` 自然生成 jumping x1（netId=223）；该次自然实例的可见、拾取和出售仍待验收。
- 0.2.5 手动生成触发闪退，Windows 记录 coreclr.dll / 0xc0000005。已确认新增的皮肤查询 interop 输出缓冲区为 8 字节，原生写入 24 字节；0.2.6 移除该调用，直接读取皮肤表并使用同步 setter。新版通过 20 项模拟，用户手动部署后反馈生成正常；积分/出售显示仍待单独确认。
- F8 窗口新增循环传送按钮和 `teleport` 命令，仅 DebugMode=true 的房主可用，定位当前自然/调试 MOD 雕像；实际传送源码 26 项、菜单/请求队列 12 项模拟通过，用户已在游戏中确认可以传送到生成的雕像旁边。多目标循环和场景/联机边界仍待验证。
- `[Definition]`、`[Register]`、`[Registry] ready`、`[Pool]`、`[Natural spawn]`、`[Batch result]` 和 `[Debug spawn]` 已实现。入池只是参与原版筛选和抽取，不保证每局出现。
- 每种可通过 `ConfigureData` 调整基础属性、通过 `InitialWeight` 设置初始相对权重、通过 `ConfigurePrefab` 修改自有预制体；0.2.9 新增 `CreateVisuals` 回调管理外观资源生命周期，并实现便签专用的嵌入网格/纹理加载。这不是通用 FBX 导入器，参数尚未统一开放为 CFG。
- 用户已反馈 0.2.3 测试正常，日志确认注册、F8 手动生成和多次持有/释放。测试窗口自动支持新注册类型的 `list` 与 `spawn <key>`，快捷按钮目前只显示前三种。真实双端同步仍需单独验收。

## 2.5 自然入池与生成验证

- [x] 读取游戏 14 个 ItemSet 的真实资源结构，定位 Score 只在答题池；快照见 `research/itemset-defaults.json`。
- [x] 分开外观模板和入池模板，跳跃雕像只加入含 Slippery 的 9 个雕像池，不新增强制入池 CFG 或额外生成数量。
- [x] 实际入池源码配合 14 个资源快照、4,500 个抽取模型批次通过；覆盖去重、数量保留、批次隔离、异常清理与禁用/不兼容门禁。注册源码的入池模板校验与兼容清单检查通过。
- [x] 0.2.5 游戏日志至少确认一次服务端自然生成：eventId=41、seed=-1960054746、ItemSet_Large、jumping x1。
- [ ] 用多个实际批次继续验证换小局不重复追加、自然实体的可见/拾取/出售和调试实例不消耗自然抽取配额。

## 3. 日志与出现概率

- [x] 已有定义、注册、入池、实际生成和调试生成的独立日志；其含义和解读规则已写入 AGENTS.md。
- [ ] 结合真实生成批次验证候选条件、当前权重、原版筛选和类型上限，记录“未入池”“入池但未生成”的实际案例；不从少量样本宣称固定出现概率。

## 4. 基础属性与外观

- [x] 导出 Score 原版雕像的 FBX、Blender 工程和四张原版 PNG，保留头部层级、UV 和法线；独立 FBX 回读核验通过。模型师参考包和便利贴制作说明位于本地 `人类工作间/接收文件/`，整个工作间忽略 Git；这是资源交付，不是新外观游戏验收。
- [x] 2026-10-13 检查模型师 `便签.zip`：FBX 只有独立便签/胶带，Blender 工程包含未改动网格/UV/位置的原版参考雕像；资源检查记录在本地 `人类工作间/接收文件/便签检查_20261013/`。
- [x] 将便签变换烘焙到头部局部坐标并接入自有预制体；网格法线保留。Blender 单输入对照把条纹定位到 Alpha 分支；游戏默认不透明版本已确认在陈列、持有和放下后可见，没有此前明显条纹。
- [x] 绕开本机 LoadImage 的 ReadOnlySpan 缺失包装，采用已核对的指针/长度 RGBA 上传；33 项实际源码模拟及本机持有/暂停/放下检查通过，资源回滚和正常卸载纳入注册器管理。
- [x] 核对现有图集的独立正面 UV，交付 1024×1024 全透明 PNG 绘画模板、单独参考底图和说明，放在本地 `人类工作间/接收文件/便签图案绘画模板_1024_v1/`。这是绘画材料和拟定流程，未实现自动导入。
- [x] 0.2.12 实现显式透明 PNG 导入/正面 UV 合成和按 key 加载内嵌纹理；用户的临时弹簧图案已接入。7 项离线资源检查通过，游戏陈列/持有/放下确认图案正向，纸/胶带保持不透明。用户原文件保留，不做运行期文件夹监控或热加载。
- [ ] 增加本体染色；需要贴边图案时单独校准比例和胶带精细遮挡，需要细节法线/半透明胶带时单独接入并验证。第二种不同图案同时显示、真实远端及晚加入显示仍待实测。
- [x] 用户手动部署 0.2.6 后复测手动生成及传送入口，反馈运行正常，未再反馈生成闪退。
- [ ] 用循环传送定位自然实例，单独核对 skin/value、手持积分、带回出售和双方价值同步；继续验证多目标循环及销毁/换场景边界。
- [ ] 用下一种实际需要的雕像验证 `ConfigureData` 独立调整属性，确认原版模板和其他 MOD 类型不被修改。
- [ ] 按实际调参需求评估开放哪些 CFG 字段，例如基础分值、难度范围、初始权重；无需一次开放全部字段。明确参数生效时机，以及玩法/外观参数在房主与客户端间的一致性要求。
- [ ] 需要其他原版外观时，选用有效雕像模板，验证继承属性、自然池匹配、碰撞、持有姿态和网络生成。
- [ ] 便签之外的自制模型按实际需要扩展加载；继续校验拾取、物理、缩放/高度和必要网络组件。所有需要显示的客户端须加载兼容资源，并验证晚加入与版本不一致的处理。
- [x] 0.2.10 弹簧节奏、单次力度恢复、异常清理、菜单/所有权/转交门禁及版本兼容等 73 项源码模拟通过；狐狸节点实测小/正常/稍高三档、F8 暂停恢复、放下停止。
- [x] 0.2.11 调高小跳并延长落地等待；82 项实际源码模拟通过，包含较长等待期间不连发；Release 编译通过，狐狸节点部署哈希一致，实测三档起跳且原力度无累积缩放。
- [x] 0.2.12 半米小跳/统一落地等待 1 秒与更名：84 项实际源码模拟通过，Release 0 警告/错误；节点实际小跳 0.511 米，连续段落地等待采样 0.999～1.019 秒。F8 暂停和放下各四秒无新增跳跃，关闭 F8 后恢复，图案在各姿态可见。
- [x] 0.2.13 跳跃/弹簧拆分、两种便签和拿起不清空体力；273 项实际源码/兼容/候选池资源模拟通过，Release 0 警告/错误。
- [ ] 部署 0.2.13 后实测两种同时陈列、独立便签/调试限额、拿起不清空体力、连续跳与弹簧节奏区别、反复切换/转交/放下和原版同模型不受影响；真实双端与晚加入另验。
- [ ] 继续验收弹簧效果在低顶/蹲下、斜坡/移动地面、重力变化、死亡/角色替换、手动跳跃与其他 MOD 同时改力度、远端持有/观察者和晚加入时的表现。

## 5. 单人测试房与提示

- [x] 创建桌面“测试”快捷方式，使用原版游戏图标，通过 Steam 携带 `--ces-test-room` 启动；本机 CFG 开启 DebugMode/TestRoomEnabled，旧插件及 CFG 已备份。
- [x] 原版 KCP 单人启动；初始房门外创建 28 米宽陈列区与连接走廊，玩家传送到房间内空地；Logo 停止和开门状态有实机日志。
- [x] 20 种原版非怪物雕像收拢到后部两排、每排 10 个，横向 2.2 米、排距 3.2 米；异步加载游戏原版基座模型/材质并增加自有承托碰撞。
- [x] 中部按注册清单的 key 排列，每种 MOD 一只并计入调试上限；空清单、未满行、多行扩展及分区间距有源码模拟，0.2.9 带便签的弹跳雕像已实机确认自动陈列、仍只允许一只调试实例。多种真实 MOD 同时陈列尚待新类型实现后验收。
- [x] 加强测试场景光照、使用自有浅色低反射材质，截图复核可清楚辨认陈列；仅精确过滤 `UI/lobby/waitPlayerBeforeStart`，其他 HUD 保留。
- [x] 源码模拟覆盖开关/启动参数、等待、超时、单次创建、失败清理、断开清理、布局间距及其他提示不被过滤；实机修复 Linked 独立生成时无 SpawnTool 父对象的缓存登记协程错误。
- [ ] 在游戏中逐种验证原版雕像拾取、效果、放下及特殊拼图行为；Linked 单只陈列不构成牵引/绳索效果验收。
- [ ] 实际走通初始房与测试房的往返路径，验证退出房间/重开及长时间测试的资源清理和照明恢复。
- [ ] 检查背景墙局部黑色区域的渲染原因；当前实际截图中雕像、基座及活动地面可见，此项不记为已修复。
- [ ] 后续实现“看向雕像时顶部显示名称＋极简效果描述”；当前仅保留提示入口，没有提前实现此功能。

</details>
