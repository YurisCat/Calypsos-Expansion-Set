# Calypso's Expansion Set 更新记录

本文件记录 0.2.7 起的开发版本变化。更早版本及详细研究、编译、模拟和游戏验收证据见 [DEVELOPMENT.md](DEVELOPMENT.md)；未完成事项见 [TODO.md](TODO.md)。

## 0.2.74 — 2026-10-19（单人规则筛选 / Solo rule filtering）

- 修复单人时仍会抽到伪装大师规则；至少两名玩家时沿用原有抽取与效果。
  Prevents the Disguise master-board rule from being rolled in solo play, preserving its existing selection and effects with two or more players.
- 统一大厅盒子介绍的列表标点，并注明伪装玩法仅限多人。
  Standardizes lobby-box list punctuation and clarifies that Disguise is a multiplayer feature.

## 0.2.73 — 2026-10-19（尸体标记 / Body pings）

- 修复落地尸体无法标记，描边包含完整身体；搬起或复活后清除，活人不会被标记。
  Fixes pings on bodies on the ground and outlines the complete body. Picking up a body or reviving the player clears its pings; living players remain excluded.

## 0.2.72 — 2026-10-19（标记清除 / Clearing pings）

- 标记持续时间延长至 60 秒；长按标记键 0.5 秒可提前清除自己的标记，无需瞄准。默认仍为鼠标中键／手柄十字键上。
  Extends pings to 60 seconds. Hold the ping button for 0.5 seconds to clear your own ping without aiming. Defaults remain middle mouse / D-pad up.
- 短按松开时新增、替换或刷新标记；长按不会先生成标记，松开后也不补标。
  A short release adds, replaces or refreshes a ping. Holding does not create a ping before clearing or on release.

## 0.2.71 — 2026-10-19（标记远近与配色 / Ping distance and colors）

- 地点图钉的大小与淡化集中在明确的近远区间内变化，远近差异更明显，保留远处可读下限与平滑过渡。
  Concentrates location-pin scaling and fading within a defined distance range, making distance differences clearer while preserving readable minimums and smooth transitions.
- 玩家标记改用柔和配色，沿用头像槽位对应的固定色序；图钉、描边与箭头保持一致。
  Uses softer player colors in the same fixed portrait-slot order, shared by pins, outlines and arrows.

## 0.2.70 — 2026-10-19（标记显示 / Ping presentation）

- 同一物品优先显示最后标记者的完整颜色，保留其他玩家的标记及剩余时间；优先者换目标或离开后，恢复仍有效的前一个标记。
  Shows the latest player's complete color on a shared item, preserving other players' pings and remaining time. Falls back to an earlier valid ping when that player changes targets or leaves.
- 地点图钉随当前距离平滑变化，近大远小、近实远淡，并保留远处可读下限；屏幕边缘箭头保持清晰。同一物品不再按玩家重复合成描边。
  Location pins smoothly become smaller and fainter with distance, with readable minimums. Screen-edge arrows stay clear, and shared items no longer repeat outline compositing per player.

## 0.2.69 — 2026-10-19（物品选点与后处理 / Item targeting and post-processing）

- 物品选点包含原版交互触发体，修复部分雕像和道具无法标记；描边匹配当前镜头的 Panini 与镜头畸变，修正移动视角后的错位。
  Includes native interaction triggers when targeting items, restoring pings on affected idols and tools. Matches item outlines to the current camera's Panini projection and lens distortion to correct drift during camera movement.

## 0.2.68 — 2026-10-19（标记选点 / Ping targeting）

- 标记选点跳过玩家、怪物及持有物，继续寻找后面的物品或场景；墙壁仍阻挡选点。保留落地尸体标记，搬起后清除。
  Ping targeting skips players, monsters and held items to reach items or scenery behind them, while walls still block targeting. Dropped bodies remain markable; carrying them clears their pings.

## 0.2.67 — 2026-10-19（物品标记描边 / Item ping outlines）

- 修正物品标记描边的屏幕坐标，移除物品轮廓的局部裁剪，解决转动视角时轮廓漂移、裁切及小物品漏显。
  Corrects item-ping screen coordinates and removes local outline clipping to fix drift during camera movement, cut-off outlines and disappearing small-item outlines.

## 0.2.66 — 2026-10-19（队友标记 / Shared pings）

- 新增队友共享标记：地点图钉、物品空心描边与屏幕边缘箭头，穿墙显示 15 秒，每人一个，新标记覆盖旧标记；物品被拾取后清除。颜色按原版头像槽位固定分配。
  Adds shared location pins, hollow item outlines and screen-edge arrows, visible through walls for 15 seconds. Each player has one ping; a new ping replaces it, and pickup clears item pings. Colors follow native portrait slots.
- 默认鼠标中键／手柄十字键上，CFG 可分别改键；大厅盒子七语言在“游戏性提升”中简述标记。
  Defaults to middle mouse / D-pad up, configurable separately in CFG. The seven-language lobby-box introduction lists pings under gameplay improvements.

## 0.2.65 — 2026-10-18（伪装诅咒与规则 / Disguise curse and rule）

- 新增骰子伪装诅咒与大师后的棋盘规则，互换第三人称外观与头顶昵称。骰子诅咒持续本小局，棋盘规则每小局重新分配；至少两名玩家才能触发。
  Adds a dice disguise curse and post-master board rule, swapping third-person appearances and overhead names. The dice curse lasts one round; the board rule reassigns players each round. Requires at least two players.
- 配有诅咒栏图标、顶部提示和身体变换特效；规则在同一棋盘仅首次变换时提示，重复骰中仍提示并保留配对。支持角色／衣装变化、死亡复活与搬运尸体，其他身份界面保持原版。
  Includes a curse-bar icon, acquisition message and transformation effect. Rules announce only their first transformation per board; repeated dice results announce again while keeping assignments. Handles character/outfit changes, death, revival and carrying bodies; other identity displays remain native.
- 修复放弃后重试时大厅名字颜色提示失效，完善相关模块在场景切换后的恢复。
  Fixes lobby name colors becoming unavailable after abandoning and retrying, and improves related modules' recovery across scenes.
- 本机多人测试新增 `finish`、`rules`、`money` 指令，支持手动结算、规则选关与加钱，测试会话不创建存档；适配游戏更新后的测试接口重建。
  Adds local multiplayer test commands for board completion, rule selection and funds, without creating saves. Allows test instances to rebuild outdated interop after game updates.

## 0.2.64 — 2026-10-17（七语言本地化 / Seven-language localization）

- 新增繁体中文、日语、俄语、巴西葡萄牙语和西班牙语，与现有简中、英文共支持七种语言。自动跟随本机游戏语言，无需额外配置；已有中英文文案保留。
  Adds Traditional Chinese, Japanese, Russian, Brazilian Portuguese and Spanish, bringing support to seven languages. Automatically follows each player's game language without extra configuration, preserving the existing Chinese and English text.
- 本地化原版 21 条及 MOD 14 条箴言和对应雕像名称。新箴言结合各语言的节奏、拟声与押韵表达原有效果；终局雕像和缺失拼图继续使用原版名称。
  Localizes 21 vanilla and 14 MOD idol verses and their associated names. New verses use rhythm, wordplay and rhyme suited to each language while preserving their gameplay meaning. Endgame idols and missing pieces retain their native names.
- 大厅盒子的完整介绍、查看／关闭／滚动提示和制作人员职责同步支持七语言。阅读中切换语言会刷新字体、换行和滚动范围，较长的署名及提示自动适配可用空间。
  Extends all seven languages to the full lobby-box introduction, read/close/scroll prompts and credit roles. Language changes refresh fonts, wrapping and scroll bounds while reading; longer credits and prompts fit their available space.

## 0.2.63 — 2026-10-17（本机多人测试 / Local multiplayer testing）

- F8 新增“本机联机”，可分别设置昵称、建房及加入本机，使用正常大厅和地图；测试会话禁止存档写入。
  Adds an F8 Local multiplayer page with separate nicknames, hosting and joining on localhost, using normal lobbies and maps. Test sessions do not save.
- 新增多实例启动脚本，隔离插件、配置与日志，通过独立启动配置解除原版单实例限制；支持仅准备文件。
  Adds a launcher for multiple game instances with separate plugins, configs and logs. Uses an isolated boot configuration to lift the native single-instance restriction, with a prepare-only option.

## 0.2.62 — 2026-10-17（房内名字提示 / Room presence names）

- 用头像下方名字颜色替换房间指示灯：青色表示玩家在初始房内，其余情况恢复原色。保留原版文字特效、透明度、骷髅与语音绿光，沿用现有房间同步及死亡实体位置判定；大厅盒子中英提示同步更新。
  Replaces room-presence lights with nickname colors below portraits: cyan means the player is in the starting room; otherwise the original color is restored. Preserves native text effects, opacity, skulls and voice glow, using existing room synchronization and dead-player positions. Updates the bilingual box tip.

## 0.2.61 — 2026-10-17（名称整理与提示 / Naming and tips）

- 通用同步类与更新组件改名为 `ExpansionSetSync`、`ExpansionSetDriver`，同步修改文件名与引用；现有 CFG、消息编号及旧版兼容入口保持。
  Renames the shared synchronization class and update component to `ExpansionSetSync` and `ExpansionSetDriver`, including files and references. Keeps current CFG settings, message IDs and legacy compatibility handling.
- 大厅盒子补充头像灯、商店体力和独立箴言开关提示；生活质量说明精简为四条，合并箴言与名称配置，保留欢迎语及玩法介绍。README 补充必要用法，NEXUS.txt 仅补充新 CFG 及名称／箴言开关独立的说明。
  Adds box tips for portrait lights, shop stamina and the separate advice option. Condenses quality-of-life guidance to four points, combining advice/name settings while retaining the welcome and gameplay introduction. Adds essential usage to README; NEXUS.txt changes are limited to CFG guidance.

## 0.2.60 — 2026-10-17（指示灯位置 / Portrait light placement）

- 将每人的指示灯移至各自头像框顶部中央，避开下方昵称牌；房内／房外两种颜色及原版头像、骷髅和语音绿光保持。
  Moves each presence light above the center of its portrait frame to clear the nickname plate. Keeps the two occupancy colors and native portraits, skulls and voice glow.

## 0.2.59 — 2026-10-17（房间提示与商店体力 / Room presence and shop stamina）

- 初始房头像增加指示灯：青色表示房内、灰色表示房外，空头像隐藏；房主判定后同步给兼容玩家。
  Adds starting-room portrait lights: cyan for inside, gray for outside, hidden for empty slots. The host determines occupancy and synchronizes it to compatible players.
- 进入商店回满一次体力，商店范围内跑跳不耗体力，离店恢复正常消耗和恢复规则。
  Refills stamina once on entering the shop, waives running/jumping costs inside, and restores normal stamina rules on leaving.
- 新增默认开启的 `[UI] ShowIdolAdvice`，可独立关闭原版／MOD 雕像顶部箴言，不影响名称、原版教程或重要消息。
  Adds `[UI] ShowIdolAdvice`, enabled by default, to hide vanilla/MOD idol advice independently of names, native tutorials and important messages.
- 兼容握手协议提升至 4，排除不支持新房间消息的旧版；联机各端需更新兼容版本并重开房间。托管检查不代替实际联机验收，验证证据见 DEVELOPMENT。
  Raises the handshake protocol to 4 to exclude older clients without room-message support. All players need compatible updates and a new session. Managed checks do not replace real multiplayer validation; see DEVELOPMENT for evidence.

## 0.2.58 — 2026-10-16（盒子滚动与制作人员 / Box scrolling and credits）

- 长正文限制在阅读区内，滚轮每格直接移动两行，也支持绑定的移动键和手柄摇杆；制作人员固定在下方，以较小字号和不同颜色显示，补齐中英文昵称及昵称后的 B 站图标。正式介绍保留欢迎语，移除临时测试文本。
  Clips long introductions to the reading area, with two lines per wheel notch and support for bound movement controls and controller sticks. Credits stay below the text with smaller type, distinct colors, localized names and Bilibili icons after the applicable names. Retains the welcome message and removes temporary test text.
- 阅读不再限制所持雕像的使用，丢下或切换物品后可继续阅读。仅关闭并暂时禁用自己的手电，退出恢复使用资格，不自动重新开灯。
  Reading no longer restricts the held idol; dropping or switching items keeps the introduction open. Only the reader's flashlight is switched off and temporarily disabled. Closing restores permission without switching it back on.
- 修复宽体状态下交互提示与按键检测使用不同镜头位置，导致瞄准盒子却无法打开的问题。
  Aligns the interaction check with the lowered wide-body camera, fixing cases where the box prompt appeared but interaction failed.
- Release 编译 0 警告／0 错误，1,977 项托管断言通过；最终滚轮手感已有用户试玩确认。本轮未部署或启动游戏，完整镜头过渡、显示和多人边界仍待实机回测。
  Release builds without warnings or errors; 1,977 managed assertions pass. The final wheel behavior has user playtest confirmation. Not deployed or launched this round; full camera transitions, rendering and multiplayer boundaries still require in-game validation.

## 0.2.57 — 2026-10-16（盒子阅读布局与过渡 / Box reading layout and transitions）

- 介绍字号缩小 20%，正文区域上移并向右、向下扩大，按屏幕比例保留边距；原版 HUD 和重要消息保持在介绍之上。
  Reduces introduction text size by 20%, moves the reading area upward and expands it to the right and downward, with proportional margins. Native HUD and important messages remain above the overlay.
- 拉近镜头期间隐藏关闭提示并忽略关闭操作，原版镜头到位后才显示提示；补齐过渡取消、超时和旧回调隔离。
  Hides the close prompt and ignores close presses until the native camera transition completes. Handles cancellation, timeouts and stale callbacks.
- 文字与侧边渐变一起淡出；受伤、死亡、传送等打断立即结束查看，不等待淡出后才交还控制。界面失效、断线、场景清理或新的查看接管时直接释放。
  Fades the text and side gradient together. Damage, death, teleportation and other interruptions end inspection immediately without waiting for the fade before releasing control. Invalid UI, disconnects, scene cleanup and a new inspection release the old overlay immediately.
- Release 0 警告／0 错误，1,912 项托管断言通过；本轮未部署或启动游戏，实际排版、过渡和生命周期仍待游戏回测。
  Release builds without warnings or errors; 1,912 managed assertions pass. Not deployed or launched this round; layout, transitions and lifecycle behavior still require in-game validation.

## 0.2.56 — 2026-10-16（盒子查看修正 / Box inspection fixes）

- 修复先看书／棋盘再看盒子时，关闭镜头跳回上次目标的问题；盒子取景保留到原版返回过渡结束后再交还共享相机，支持快速重开和原版再次接管。
  Keeps the box camera target through the native return transition, preventing a jump to the previously inspected book/board. Handles rapid reopening and native target changes.
- 查看期间仅显示右下角“关闭 / Close”及原版交互图标；打开前保留名称／查看介绍。字号与正文布局暂不调整，待进一步讨论。
  Shows only the interaction icon and “Close” near the bottom-right while reading, retaining the title/read prompt before opening. Text size and body layout remain unchanged pending discussion.
- Release 0 警告／0 错误，1,713 项托管断言通过；本轮未部署或启动游戏，实际镜头过渡和 UI 待回测。
  Release builds without warnings or errors; 1,713 managed assertions pass. Not deployed or launched this round; camera transitions and UI still need in-game validation.

## 0.2.55 — 2026-10-16（盒子查看开发版 / Box inspection development build）

- 盒子改用原版检查物品相机，按交互键进入／关闭：盒子在右侧，介绍在左侧，水平翻转原版书本的横向黑色渐变；移除旧世界文字面板，文字滚动／裁切暂缓。
  Uses the native item inspection camera: interact to open/close, with the box on the right and the introduction on the left. Mirrors the book's horizontal black gradient and replaces the world-space panel. Scrolling and clipping remain deferred.
- 进入查看暂停移动／转向与工具使用，退出恢复；补齐独立维护和事件清理，原版成功切换镜头或接管控制时退出。摆放、补光与测试房家具逻辑保持。
  Pauses movement, look and tool use while reading, with independent lifecycle checks and event cleanup. Releases inspection when native camera/control handling takes over. Placement, lighting and test-room furniture behavior are retained.
- Release 0 警告／0 错误，1,681 项托管断言通过；未部署或启动游戏，真实画面、输入与多人验收待进行。
  Release builds without warnings or errors; 1,681 managed assertions pass. Not deployed or launched; visuals, input and multiplayer still require in-game validation.

## 0.2.54 — 2026-10-16（大厅盒子开发版 / Lobby box development build）

- 新增固定桌游盒子，使用已保存的挂墙位置；原版样式交互点支持键盘改键与手柄，中英介绍显示在盒子上方并向封面外移。针对白块图标重建边框与文字组件，靠墙交互统一检查正面交互点。
  Adds a fixed board-game box at the saved wall placement, with a vanilla-style interaction marker, rebound keyboard/controller input, and a Chinese/English introduction above and in front of the cover. Rebuilds the key graphics to address white blocks and uses one front-facing point for wall-adjacent interaction.
- F8 新增模型摆放页：按住数值左右拖动调整位置、旋转、缩放和亮度，近处精细、远处加速；保留数字输入、移到面前、保存／读取和历史记录。
  Adds an F8 placement editor with variable-speed dragging for position, rotation, scale and brightness, plus numeric input, bring-here, save/load and placement history.
- 提供五组随盒子移动的补光与 0～5 倍亮度调节，跟随原大厅灯具的停电、波动和恢复；方案与亮度一并保存，兼容旧摆放记录。
  Adds five attached lighting presets and a 0–5× brightness multiplier that follows native room-light outages, flicker and recovery. Saves both settings while accepting older placement files.
- 专用单人测试房可临时补齐实际商店陈设，修正额外生成门铃的问题；原有家具保留，沿用测试模式的存档写入保护。
  The isolated solo test room can temporarily add purchasable furniture without the extra doorbell. Existing furniture and the test mode's save-write protection are preserved.
- Release 编译 0 警告／0 错误，664 项托管检查通过；最新图标、介绍遮挡、灯光效果与真实多人仍待游戏回测。构建、历史截图和验证范围见 DEVELOPMENT。
  Release builds with no warnings or errors; 664 managed checks pass. The revised icons, panel occlusion, lighting and real multiplayer still require in-game testing. See DEVELOPMENT for evidence and validation limits.

## 0.2.53 — 盒子交互点、拖动调节与补光（未发布）

- 盒子改用正面独立交互点和原版 UI 样式，移除初始小面板；名称／动作及展开介绍显示在交互点右侧。阅读期间沿用同一可见点，修正中心嵌墙后立即收起的问题。
- F8 数值支持按住后左右拖动：近距精细、远距加速、回到起点停止；保留数字输入，失焦或关闭面板取消调整。
- 新增五组随模型移动的聚光／点光补光，一次选择一组，跟随大厅原灯的熄灭、波动和恢复；编号随摆放记录保存，旧记录兼容。
- 满配按实际商店清单生成，修正枚举中 `DOORBELL` 导致的额外铃铛。
- 以更新后的本机游戏接口编译；原生 UI、拖动手感、墙边交互和灯光联动待实机确认。

## 0.2.52 — 大厅盒子摆放开发版（未发布）

- 新增固定桌游盒子，交互后在盒子上方显示中英介绍；沿用原版交互输入并适配改键／手柄提示。
- F8 新增模型摆放页：三轴位置、旋转、缩放、分档微调、移到面前、保存与读取。保存数据用于确认最终大厅位置，默认位置仍为临时值。
- 专用单人测试房默认临时启用全部大厅陈设，支持关闭恢复；沿用禁止存档写入保护。
- 仅完成资源、编译与托管检查，模型显示、交互、真实多人和最终摆放待游戏验证。

## 未发布 — 本地图鉴

- 修正预览选择范围：按绘画参考的蓝框内侧裁剪源画布，使用纯黄色纸底；框外笔迹不再出现在图鉴中。游戏贴图和源绘画保持。
- 新增默认开启的紧凑便签模式，可切换大图，并记住预览大小。
- 新增中文／English 切换，名称与箴言直接沿用游戏本地化，玩法简述和页面操作同时切换；支持双语搜索、保存语言偏好和带语言的雕像链接。

## 0.2.51 — 2026-10-15

- 整合远端 0.2.50 与本地手绘/本地化分支，保留十四种启用内容、怪相候选/动画/攻击/短预览、相机逆序恢复、F8 扩展及棋盘配置迁移。
- 十四种便签换为正式手绘，均在远端内容版本基础上递增；稳定身份与远端玩法保持。所有联机端需使用匹配版本并重开房间。
- 指向名称与箴言支持中英文并随本机游戏语言切换；怪相持有时的提示隐藏优先于语言选择，放下后恢复对应语言。其他语言暂回退英文。
- 本地工作台图鉴展示十四种启用雕像的正面裁图、名称、箴言和简述，支持搜索、分类与放大；同步修正怪相状态和疑神声源说明。
- Release 编译 0 警告/0 错误；622 项实际提示/本地化、43 项注册/成品程序集、88 项便签解码/内嵌资源检查通过。本次未部署或进行游戏/联机验收；历史证据保留各自版本边界。

## 0.2.50 — 2026-10-15

- 怪相动画每帧只更新一次，同帧额外渲染复用姿态，修正持续移动时反复切回待机、重置走路动画而出现卡腿滑步的问题；模型位置、显隐和世界外观隐藏仍按每次渲染处理。
- 移除此次排查使用的周期动画日志、骨骼检查和画面提示；取消 DebugMode 下的 200 秒预览，恢复普通短预览，保留进出过渡与原有 F8 工具。
- 怪相 ContentVersion=13。用户确认修复后的雪怪效果良好；最终清理构建的编译与托管回归通过，实测范围及待验项见 DEVELOPMENT。

## 0.2.49 — 2026-10-15

- 怪相按原版实体类型排除假队友 CursedPlayer 与门怪 DoorMonsterEntity，覆盖已生成历史、当前排队及兜底入口；修正抽到假队友模板缺少可复制网格而变身失败的问题。共享声音目录与原版怪物生成保持。
- 怪相 ContentVersion=12；临时 200 秒诊断预览和原有动画规则保持。Release 编译 0 警告/0 错误，56 项候选过滤托管检查通过；过滤结果仍待游戏/真实多人验收。
- 用户 0.2.48 日志中的雪怪长段采样持续推进，未捕获持续卡帧；门怪记录为无动画可采样，不能当作雪怪滑步复现。详细证据见 DEVELOPMENT。

## 0.2.48 — 2026-10-15

- 临时怪相动画诊断：DebugMode 开启时，背后第三人称预览延长到 200 秒，保留 0.25 秒进出过渡与失去状态的退出；关闭调试仍为普通短预览。
- 采集实际显示模型的片段选择、位移／渲染时序、片段重置及骨骼姿态变化，每两秒汇总并限制每轮总量；本机画面被动提示疑似异常或对照样本已记录，无需打开 F8。不改变动画选择、移动判定或采样推进，诊断不代表已修复／确认雪怪实机原因。
- 仅本机诊断变化，内容版本与网络协议保持；编译、托管验证与待验边界见 DEVELOPMENT。

## 0.2.47 — 2026-10-15

- 修正怪相退出过渡期间换拿宽体，可能留下中间投影、导致画面持续放大／拉伸的问题；保留怪相 0.25 秒进入与返回过渡。
- 怪相、宽体、倒立统一按固定顺序应用相机修改、逆序恢复，覆盖正常结束及异常/生命周期退出；倒立作为防御覆盖，未将其记为用户已发现故障。项目开发约定加入视野效果的逆序恢复规则。
- 怪相／宽体／倒立 ContentVersion 分别提升为 11／4／7。Release 编译 0 警告/0 错误，1,403 项托管检查及 15,360 个取景角点检查通过；未部署或追加游戏/真实多人验收。

## 0.2.46 — 2026-10-15

- 怪相攻击实际发声时，沿用声音雕像的共用听觉入口，以持有者为来源、范围参数 18 产生一次噪声，可能引来有听觉的真实怪物；无声动作不产生额外噪声，重复请求和晚加入不补发。怪相 ContentVersion=10。
- Release 编译 0 警告/0 错误，172 项实际源码托管检查通过；未部署或游戏/真实多人引怪验收。

## 0.2.45 — 2026-10-15

- 怪相持有时左键可表演当前怪物的攻击动作和匹配声音；手柄默认 RT／R2，沿用原版改键。动作/声音独立，缺失部分跳过，仅作表演，无伤害或 AI 攻击。
- 移动不打断，动作及短恢复期间连按忽略；放下、转交、死亡等清除动作，已发出的声音自然结束。新持有 Revision 拒绝旧请求，一次性动作不进入晚加入快照；怪相 ContentVersion=9。
- Release 编译 0 警告/0 错误，145 项攻击相关托管检查与 173 项预览/白雾回归检查通过；未部署或游戏/真实多人验收。

## 0.2.44 — 2026-10-15

- 修正 F8 雪人／猎人生成的导航检查，绕开当前 interop 中 IL 错误的过滤器版路径接口，改用直接转发原生的整数区域掩码接口；模板导航类型异常时仅提示，不生成。
- 保留前方 10～15 米可达空地、每种一只及只移除自有实例的行为。Release 编译 0 警告/0 错误，177 项托管检查及成品接口 IL 检查通过；未部署或游戏/真实多人回测。

## 0.2.43 — 2026-10-15

- 怪相预览进入、到时返回及放下/换拿返回增加 0.25 秒镜头过渡；提前放下从当前进度返回，转场贴近身体时临时隐藏本机身体，避免看到模型内部。死亡、菜单或相机被接管时立即释放。
- 观察者端临时停用怪相持有者的世界嘴部白雾，释放后恢复并按当前地图/存活状态刷新；持有者自己的第一人称白雾保持原版。怪相 ContentVersion=8。
- Release 编译 0 警告/0 错误，173 项托管检查及 15,360 个网格角点投影检查通过；未部署或追加游戏/真实多人验收。

## 0.2.42 — 2026-10-15

- F8 原版生成指令无同名 MOD 时可省略 `original:`，例如 `spawn miniature`；同名时短名称保持 MOD 优先，原版旧写法继续可用，`list` 显示对应短名称。
- Release 编译 0 警告/0 错误，174 项实际源码托管检查通过；未部署或游戏实测。

## 0.2.41 — 2026-10-15

- F8 `list` / `spawn` 自动读取普通原版雕像，使用 `original:` 名称或配置 ID；MOD 沿用注册清单，结果区支持滚动，不增加原版按钮。
- 新增雪人、猎人两个独立生成／移除按钮，仅房主正常关卡可用；限定前方约 10～15 米可达空地，每种一只，只移除对应手动实例。保留原版 AI，未修改雕像玩法。
- Release 编译 0 警告/0 错误，169 项实际源码托管检查通过；未部署或游戏实测，验证范围见 DEVELOPMENT。

## 0.2.40 — 2026-10-15

- 怪相拾取后的自动预览改为背后第三人称，按怪物网格范围和屏幕宽高比适配取景；预览期间固定尺寸测量，避免动画引起镜头忽远忽近。
- 预览相机仅在绘制期间临时调整，保持原版输入和角色转向，约 2.25 秒后回到第一人称；不再使用换装镜头的人形跟随目标。怪相 ContentVersion=7。
- Release 编译 0 警告/0 错误，62 项托管检查与 15,360 个角点投影检查通过；未部署或游戏实测，实际抖动改善、逐种怪物取景及墙体遮挡待验。

## 0.2.39 — 2026-10-15

- 怪相保留原版怪物尺寸，不再统一为人体高度；仅改变视觉外观，碰撞、移动、下蹲/奔跑/跳跃及受击规则沿用原版。
- 持有怪相时临时隐藏头顶昵称及该物品的名称/箴言，放下恢复，保留玩家设置；每次拿起后自动借用原版换装镜头预览约 2.25 秒，再返回第一人称。
- 修正共享外观转交时的清理顺序，避免新持有者保存旧效果的隐藏值；渲染前释放已失去持有关系的外观。怪相 ContentVersion=6，联机各端需匹配并重新建房。
- Release 编译 0 警告/0 错误，126 项相关托管检查通过；未部署或进行游戏/真实多人验收，全部怪物尺寸、预览取景及死亡旁观仍待回测。

## 0.2.38 — 2026-10-15

- F8 NEXT 增加原版 Flow 播放状态检查：播放期间拒绝新请求，已进入推进等待阶段的请求等待 Flow 结束；其他行为保持。
- Release 编译 0 警告/0 错误，97 项实际 F8 源码托管检查通过；未部署或游戏实测，独立特殊事件仍不保证覆盖。

## 0.2.37 — 2026-10-15

- F8 四个 DEV 按钮同排缩小为“下一关 / 复活 / 无敌：关/开 / 传送”，雕像生成与循环定位布局保持。
- 新增房主自身的原版伤害免疫开关，退出会话、角色替换、死亡或关闭调试时恢复；新增将所有存活且连接就绪的玩家送回各自原版出生点的按钮。
- NEXT 行为保持；用户 0.2.36 日志仍确认异步事件重入，等待生成/特殊事件完成的方案仅作分析，未实现。验证结果见 DEVELOPMENT。
- Release 编译 0 警告/0 错误，93 项实际 F8 源码托管检查通过；未部署或游戏实测。

## 0.2.36 — 2026-10-15

- F8 `next` 在棋盘未放上桌时只提示原因，不执行；修正将原版棋子完成回调当作查询调用的问题，避免检查本身触发后续玩法。未增加桌上现存骰子的额外限制。
- Release 编译 0 警告/0 错误，48 项实际 F8 源码托管检查通过；修正后未部署或游戏实测。

## 0.2.35 — 2026-10-15

- F8 新增下一小局按钮/`next`：跳过收集与手动投骰，以随机 1–6 点推进棋子并沿原版事件处理外部房间；棋盘或房间过渡期间拒绝，专用陈列测试房不可用。
- F8 新增复活死亡玩家并全员满血按钮/`revive`，也为存活受伤者补满血，按各玩家血量上限恢复，允许死亡房主发起；两项仅房主执行，受 DebugMode 控制。
- Release 编译 0 警告/0 错误，43 项实际 F8 源码托管检查通过；未部署或进行游戏/真实多人验收。

## 0.2.34 — 2026-10-15

- 疑神的随机怪声与原版听觉事件统一从持有者身上发出，避免假声位置与原版玩家调查目标不一致；保留随机怪声、触发间隔与噪声范围，是否前来调查由原版怪物行为决定。
- 疑神 ContentVersion 提升至 3，其余雕像的身份与版本保持；联机各端需更新匹配版本并重新建房。实际有怪地图寻声与真实多人仍待验证。
- Release 编译 0 警告/0 错误，32 项实际声音/听觉源码托管检查通过；本次未部署或追加游戏验收。

## 0.2.33 — 2026-10-15

- 整合 0.2.30 公开稳定分支，保留 0.2.32 的怪相修复并恢复稳定版曾暂停的注册；当前启用十四种 MOD 雕像。怪相 ContentVersion=5，其他类型身份与版本保持；联机各端须匹配并重新建房。
- 保留 CFG 双语说明、英文棋盘开关及旧值迁移，合入公开分发包默认仅包含 MOD DLL 的约定。
- 同号 0.2.30 的公开稳定版与本地开发构建分开记录，历史验证仍归于各自构建；本次未追加游戏或真实联机验收。

## 0.2.32 — 2026-10-15

- 怪相隐藏仅作用于观察者看到的原身体、手持雕像、手电外观及灯光；持有者第一人称保留手持雕像显示和手电照明。从观察视角切回第一人称时释放隐藏状态。
- 怪相 ContentVersion 提升至 5，其他类型身份和版本保持；联机各端须匹配。Release 编译 0 警告/0 错误，55 项源码托管检查通过，实际第一人称显示/照明及观察者联机表现仍待游戏回测。

## 0.2.31 — 2026-10-15

- CFG 说明统一为精简中英文，以换行和 `/` 分隔；棋盘碰撞开关更名为 `DisableBoardPawnCollision`，自动保留旧中文键的值，新旧键并存时优先英文键。
- 修正怪相在棋盘完成标记为 true 时错误清空历史并跳过待生成队列的问题；该状态下已经生成、当前排队的怪物仍参与选择，取消的队列不进入历史。显式游戏重置、棋盘更换和会话退出清理历史。
- 怪相 ContentVersion 提升至 4，其他类型保持；联机各端须匹配。Release 编译 0 警告/0 错误，45 项实际源码托管检查通过，包括用户日志中的 Mimic 排队场景；修正后的真实游戏候选、外观与联机仍待回测。

## 0.2.31 本地手绘与本地化分支 — 2026-10-15

- 十三种启用雕像的临时图标替换为独立正式手绘；暂停的怪相同步保存新图案，仍不注册。保持原便签网格、纸底、胶带及 UV 映射，十四个定义各自提升 ContentVersion。
- 本地工作台新增「雕像图鉴」，展示正面裁图、名称、八字箴言和简述，支持搜索、分类、暂停状态筛选、放大及单条链接。
- Release 编译 0 警告/0 错误；实际解码、内嵌资源、图鉴接口与工作台保存回归通过。未进行本次图案的游戏、联机验收，详情见 DEVELOPMENT。

- 雕像名称与顶部箴言随本机游戏语言切换，中文保留原文，英文使用原创押韵双句，其他尚无译文的语言暂回退英文。补齐 21 原版/14 MOD 文案；怪相译文仅保留，仍不启用。两个终局物件继续使用原版本地化名称。
- 统一名称也随语言切换，沿用 ShowIndividualIdolNames；不新增语言 CFG；本地化部分不修改雕像玩法、公开 API 或 ContentVersion。Release 编译及 592 项实际提示源码托管检查通过；未部署或进行游戏显示/真实多人验收。

## 0.2.30 — 2026-10-15

### 公开稳定版

- 暂停怪相雕像注册，当前启用十三种 MOD 雕像；自然池、F8、测试房与兼容清单均不包含怪相，不初始化其专用外观/快照运行时。源码、图案和独立身份保留，疑神共享怪物音库保持。
- 其余雕像的玩法、名称、箴言与 ContentVersion 保持。所有联机参与者须更新到同一版本，退出旧会话后重新建房；不迁移旧怪相实例。
- 提供用于 Nexus Mods 上传的精简 ZIP，包内只放 MOD DLL；依赖、安装与更新说明放发布页，不附带个人 CFG、游戏/BepInEx 依赖或测试启动器。
- Release 编译 0 警告/0 错误，40 项实际程序集/注册托管检查通过；本轮未启动游戏或进行真实联机验收。

### 本地开发构建

- 怪相为非 Legacy 动画采样补齐无控制器的 Animator，关闭自动播放、动画事件和 root motion；外观跟随显示角色的朝向，采样后重新应用挂接位置。
- 补充原版事件管理器的怪物模板与服务器登记入口，保留模板未加载时的真实生成记录；拾取时立即刷新候选。DebugMode 开启时输出抽中名称、完整候选、兜底标记及活跃实体/队列来源。
- 持有怪相时隐藏手持雕像、手电外观和照明，包含持有者第一人称；放下或对象替换后释放自有显示修改。手电的原版开关和玩法状态保持。
- 怪相 ContentVersion 提升至 3；联机各端须匹配。Release 编译及 37 项源码托管检查通过，候选、动画、实际光照与真实多人仍待游戏回测；用户测试的中间构建仍出现空池兜底，未将其记为修复验收。

## 0.2.29 — 2026-10-15

- 表内 21 种原版物件接入顶部箴言；开启 `[UI] ShowIndividualIdolNames` 时显示 19 个指定名称，均含“雕像”后缀。终局雕像与缺失拼图保留原名。关闭开关时原版名称保持、MOD 继续显示统一名称，箴言照常显示。
- 按确认表格更新倒立、迟钝、怪相和大头的箴言，其余十条 MOD 文案保持。简介不加入游戏，雕像玩法、稳定身份和 ContentVersion 保持。
- Release 编译及 217 项提示源码托管检查通过；已生成测试包，后续按授权安装本机 Steam，核对安装哈希和唯一插件副本。未追加游戏测试，原版实际名称/字体及正式局显示交人工回测。

## 0.2.28 — 2026-10-15

- “马赛克雕像”更名为“像素雕像”，“大头鱼眼雕像”更名为“大头雕像”；同步游戏名称、调试入口、测试房标签、便签清单、当前文档与本地待办清单。箴言及原有效果保留。

- 修正宽体临时投影恢复后残留自定义模式，导致近景缩放和测试房视距失效的问题；按原来的自动、物理或自定义投影模式恢复，只释放仍属于宽体的写入。
- 宽体在常规相机参数覆盖之后读取当帧投影，保留当前视距和 FOV；近景、测试房及宽体身体/手持外观逻辑保持。宽体 ContentVersion 提升为 3，其余内容版本不变。
- Release 编译、37 项源码托管检查与 12 项原生 Unity 相机检查通过；隔离单人游戏确认宽体拿放、远处墙地显示，以及随后近景的实际放大/恢复。正式地图与真实多人仍待回测，证据边界见 DEVELOPMENT。

## 0.2.27 — 2026-10-14

- 推送前同步远端测试房修复：按组件排除 MissingPiece/Final 终局展品及怪物物件，其余原版与十四种 MOD 陈列按实际清单排列；测试房的相机视距覆盖按渲染恢复，普通启动不启用。整合编译与托管检查通过，当前整合版仍待游戏验证。
- 新增 `[QualityOfLife] 关闭棋盘上棋子的碰撞箱`，默认 true：关闭代表玩家进度的棋子的实体碰撞，方便在棋盘上移动和踢骰子；保留棋子显示、进度与移动动画。
- 仅处理当前棋盘的进度棋子，不修改棋盘、骰子或事件中的国际象棋棋子。各端按本机 CFG 生效，独立于 DebugMode；关闭开关、换棋盘、退出房间与正常卸载时释放本功能的修改。
- Release 编译和 25 项托管检查通过，中文配置保存使用实际 BepInEx.Core 验证；尚未部署或进行该功能的游戏/联机验收，研究与验证边界见 DEVELOPMENT。

## 0.2.26 — 2026-10-14

- 弹跳每次被服务器接受的随机弹簧音效新增一条原版怪物听觉事件；客户端播放不重复产生噪声。
- 核对疑神每次怪声原已有听觉事件，并与弹跳统一服务器听觉入口，保留同次声音位置、原版玩家来源、范围与寿命；待办清单更正旧“不引怪”提案。
- 便签改为一份共享无损压缩纸底及逐图案压缩 XOR 差异，加载时还原原 RGBA32 mip 链。十五份图案与纸底的运行资源减少 94.9%，原画质、UV 与材质保持；不再逐份内嵌完整未压缩贴图。
- 全部十四种启用雕像的 ContentVersion 提升，稳定身份保留；所有参与者需要匹配新版本。编译、资源检查与单人/多人验证边界见 DEVELOPMENT。

## 0.2.25 — 2026-10-14

- 测试房不再陈列 MissingPiece 特殊剧情道具，去掉对应钻石、基座和标签，其余原版展品自动重新排列；避免拾取该独立展品触发原版装备流程的空引用。原版正常生成与八种 MOD 玩法保持原有规则，游戏效果待实测。
- 测试房创建后，按陈列房尺寸临时扩大本机游戏相机的远裁剪距离，包含相连初始房；每次渲染后恢复原值，普通启动不启用。远处墙地的实际显示与性能仍待游戏验证。

- 修正正式局箴言可能被原版消息队列、残留显示标记或隐藏 UI 父节点持续挡住的路径。使用独立画布，普通消息与箴言分行显示，全屏旁白和菜单仍优先。
- 增加统一雕像外观状态接口：声控红蓝眼睛与接力绿红眼睛共用服务器转发、完整快照和资源清理。声控只同步允许前进的状态，不发送麦克风音频；接力直接使用服务器疲劳值。晚加入可恢复当前外观。
- 声控与接力 ContentVersion 提升至 4，新增外观协议 `0x4D4D`；全部联机参与者需要匹配版本。其他部位的动态外观可通过同一接口接入。
- 十四种雕像顶部箴言更新为已确认的四字双句；当前文案及后续尽量押韵的约定见 [箴言文案](docs/IDOL_ADVICE.md)。
- 新增 `[UI] ShowIndividualIdolNames`，默认 false：物品名统一显示“卡里普索拓展包雕像”；设为 true 显示各自名称。顶部箴言仍按类型显示，配置仅影响本机显示。

## 0.2.24 — 2026-10-14

### 远端接力更新

- 接力按存活人数分两档：单人从零疲劳累计持有 60 秒降至正常对应动作速度的 35%；至少两人统一为 20 秒／25%，死亡旁观者不计入。
- 服务器持续检查存活人数；死亡、复活及进出房间自动切换累计速率和速度下限，已有疲劳保留。任意两人交替即可归零，同人放下重拿仍续接。
- 补齐接力空中水平速度的疲劳倍率，覆盖起跳、离开平台及下落；保留原版蹲行、奔跑、体力、空中转向、跳高与重力，不强制地空瞬时同速。
- 接力 ContentVersion 提升至 3，快照协议提升至 2，携带存活人数并用于客户端速度计算；稳定身份保留，所有参与端需匹配新版本。编译与托管检查见 DEVELOPMENT，真实游戏和联机待验收。

### 本地六种雕像开发构建

- 新增疑神、宽体、怪相、马赛克、描边、大头鱼眼六种雕像，沿用统一注册、自然候选池、便签和指向提示。
- 疑神由服务器决定随机空间怪声，通过原版音效 RPC 向已准入客户端播放，并在同一位置添加原版听觉事件。
- 宽体和大头使用独立的视觉骨架；怪相由服务器从本局已生成、排队生成与待重生种类中选取，每次拾取重新抽取，池为空使用哭泣人偶。只复制视觉网格，不复制怪物 AI、碰撞或网络组件。
- 怪相使用独立完整快照同步当前持有者与所选外观，支持晚加入/重连恢复并校验消息来源、清单、数量、顺序与原版资源身份。
- 马赛克、黑底白线与鱼眼作用于最终场景及 HUD；菜单/失去本地操作资格时停用。着色器作为自制资源随 DLL 内嵌。
- F8 支持 `teleport <key>` 定位指定种类；显式单人测试房支持 `inspect` 切换外观检查视角。
- 当前十四种内容；六种新类型使用独立稳定身份和 ContentVersion 1。全部参与端须更新匹配清单；编译、托管与单人测试的范围见 DEVELOPMENT，真实多人另验。

## 0.2.23 — 2026-10-14

- 八种启用雕像使用待办清单同款临时图标，放在便签模板建议绘图区；保留原手绘素材，待正式绘制后逐张替换。
- 指向 MOD 雕像时，原版物品名位置显示正式名称，顶部显示一句效果短说明；移开、菜单或失去控制后隐藏，原版教程和重要消息优先。原版雕像的提示保留。
- `jumping` 显示名统一为“弹跳雕像”。新增可选 `IdolDefinition.ShortDescription`，外部内容默认不启用。
- 八种内容均提升 ContentVersion，稳定身份、玩法参数和网络报文格式不变；所有参与端需更新匹配内容。编译、离线与实际游戏验证分别见 DEVELOPMENT。
- 合入 F8 窗口优化：标题栏支持拖动并在本次插件运行期间保留位置；全部注册雕像提供滚动生成按钮，类型增加不扩大窗口。
- 修复 F8 命令输入框获得焦点后回车不执行的问题，主键盘与数字小键盘回车均触发 Run。此项游戏操作验收仍待完成。

## 0.2.22 — 2026-10-14

- 合并远端 0.2.21 的极速水平恒速、空中锁向与奔跑禁用修复。
- 新增接力雕像（`relay`）：按同一人的累计持有时间逐步降低移动速度；放下暂停累积，同人再拿不重置，换人接手归零。人数越少，后续减速越慢，持有中进出房间也会重新计算人数。
- 眼睛随疲劳由绿色渐变为红色，持有者和观察者读取同一份服务器状态；放下保留颜色，换人恢复绿色。
- 服务器维护每只接力雕像的独立疲劳，使用带兼容与来源校验的完整快照恢复晚加入/重连；持有者在原版速度计算中临时施加倍率并恢复，键盘和手柄沿用同一运动路径。
- 新增可选 `IdolDefinition.CreateRuntime` 与 `IdolContentRuntime`，为跨持有周期的状态提供按类型创建、更新、回滚和清理。声控与接力共用自有眼睛覆盖网格的资源流程，声控红蓝规则保持。
- 当前启用八种内容；relay 使用独立身份和 `ContentVersion=1`，所有参与端需要更新匹配清单。整合编译、托管检查、部署及独立单人启动范围见 DEVELOPMENT；完整玩法和真实联机待验收，接力疲劳不持久化到原版存档。

## 0.2.21 — 2026-10-14

- 极速固定无阻碍时的水平推进速度，站蹲、起跳与空中保持同一五倍步行基准；保留原版纵向运动和碰撞。
- 起跳／离地时锁定世界水平行进方向，空中方向输入和转镜头不改变轨迹，落地恢复方向采样。方向记忆、菜单行为及其他雕像不变。
- 持有极速时禁用原版奔跑动作，覆盖键盘重绑定、手柄及拿起前已在奔跑的情况；放下恢复。
- 极速 ContentVersion 提升至 4，沿用原版角色运动同步，联机各端需更新匹配内容。编译和托管模拟通过，游戏与真实双端仍待验收。

## 0.2.20 — 2026-10-14

- 极速拿起即向前，改为记忆原版统一移动向量，支持重绑定、手柄和斜向；松键／摇杆回中不停，轻推超过死区后也以完整目标速度移动。鼠标／右摇杆转向、蹲姿和跳跃保留。
- 极速以站立步行速度为基准，地面与空中统一五倍水平目标速度，后退／侧移／斜向／蹲行一致，奔跑不再额外加速。保留原版加速度、惯性、地形、碰撞、跳高和重力，实际速度仍有过渡。
- 极速在菜单、聊天及 F8 窗口期间持续沿锁定方向移动，只暂停方向采样；返回后先释放方向输入，再接受新方向。真正全局物理暂停仍遵守，其他雕像门禁不变。
- 极速 ContentVersion 提升至 3，稳定 ID 和网络协议格式不变，联机各端需更新匹配内容。编译／模拟与尚待游戏验收的范围见 DEVELOPMENT。

## 0.2.19 — 2026-10-14

- 本次源码更新同时包含下方 0.2.18 的全部修复；0.2.18 未单独发布。
- 近景雕像增加镜头缩放过程：拿起时平滑缩小视野，放下时平滑恢复。菜单、死亡或断线等失效状态立即释放覆盖，不改玩家保存的 FOV 设置。
- 声控雕像为持有者显示发光眼睛：默认红色，声音满足前进条件时快速转蓝，停止后恢复红色。提示与移动使用相同判定；静音、无有效声音或菜单中保持红色，放下后恢复原版外观。暂不向队友同步颜色。
- 倒立雕像追加 A/D 左右移动反向，保留 W/S 前后方向、180° 画面旋转与双轴视角反向；菜单及放下恢复。
- 近景、声控、倒立的 ContentVersion 分别提升至 2、2、4，稳定 ID 与协议格式不变；联机各端需更新匹配内容。编译、模拟及单人验证范围见 DEVELOPMENT。

## 0.2.18（随 0.2.19 合并发布）— 2026-10-14

- 暂停注册弹簧雕像，从自然池、F8 列表和测试房陈列移除，保留源码及独立身份。当前启用七种雕像。
- 将四段弹簧音效接到跳跃雕像：每次成功自动起跳随机播放一段，避免相邻重复；手动跳、失败/过期请求和释放后不补播。同一次起跳只发一次声音，保留原力度、动画和体力。
- 极速雕像增加 WASD 按键粘滞：最后新按下的方向持续生效，松键不停止，新方向替换旧方向，放下后恢复。只保留一个方向，菜单暂停推进并忽略菜单输入；保留四倍速度与其他动作。
- 倒立雕像在保持 180° 画面旋转的同时反转水平、垂直视角输入。原版灵敏度和反转设置不被改写，HUD、菜单和鼠标指针保持正常；放下和控制禁用时恢复。
- 效果启停诊断改由 DebugMode 控制；起跳、音效、粘滞方向、速度和倒立视角日志均受调试开关控制。关闭调试不关闭玩法，保留注册/兼容状态和异常日志。
- 跳跃、极速、倒立的 ContentVersion 分别提升至 9、2、3；稳定 ID 不变，音频文件及报文格式不变。更新后重开房间，联机各端需匹配内容。单人验证和多人待验边界见 DEVELOPMENT。

## 0.2.17 — 2026-10-13

- 在已发布声控雕像的基础上，新增倒立、鸭步、极速和迟钝雕像，共八种内容；保留声控的原版麦克风阈值、输入控制和稳定身份。
- 倒立将世界画面旋转 180°，HUD/菜单正立；极速使用四倍原版移动倍率；迟钝让方向及松键延迟 0.3 秒，视角和交互即时。
- 鸭步跟随实际脚步随机播放六段短叫声，服务器同步为持有玩家添加一次原版听觉事件，范围参数 18，按怪物自身规则判断察觉；弹簧仅在大跳成功起跳时随机播放四段音效之一。两组都避免相邻重复。
- 解决并行开发的身份冲突：声控保留已发布 ID/assetId 后缀 04；尚未发布的倒立改为后缀 08、ContentVersion=2，鸭步/极速/迟钝保留后缀 05/06/07。所有端须更新兼容内容，旧本地试验的倒立身份不作自动存档迁移，测试房需退出后重开。
- 八种内容均通过统一注册接入自然候选、F8 和测试陈列；新雕像使用原版模型与空白便签，不增加原版自然生成数量。
- 构建修正：两份运行音效及裁剪/哈希记录随源码放入 `Assets/Audio`，工程和资源生成脚本默认使用该目录，解决另一台机器拉取后缺少音效无法构建的问题；音频内容及运行期版本契约不变。
- 合并后 Release 编译及 106 项托管检查通过，包括声控/迟钝在两种输入补丁顺序下的切换恢复；本次未重新部署。八种同时运行、真实双端、怪物实际寻声及极速入口边界仍待游戏验收，详见 DEVELOPMENT 和 TODO。

## 本地试验 0.2.16（未发布）— 2026-10-13

- 鸭步雕像的叫声接入原版怪物听觉：服务器每确认一次鸭叫，便在持有玩家处添加一次原版噪声事件，范围参数 18；怪物按原有灵敏度、遮挡及行为规则判断是否察觉。
- 噪声只在认证、Ready、兼容、持有/归属、序号和节流通过后由服务器触发，客户端播放和重复请求不重复制造噪声；玩家音量和 DebugMode 不关闭这一玩法。
- 保留原版脚步，噪声寿命/清理由原版管理。弹簧音效不增加这个听觉回调。
- `duck_step.ContentVersion=2`，联机各端须更新兼容内容；消息格式和其他雕像参数不变。验证范围见 DEVELOPMENT。

## 本地试验 0.2.15（未发布，四种雕像）— 2026-10-13

- 新增倒立雕像 `upside_down`：持有者的世界画面旋转 180°，HUD/菜单保持正立，不改变输入轴；菜单或放下时恢复。
- 新增鸭步雕像 `duck_step`：跟随实际原版脚步播放六段短叫声之一，由服务器选片，附近玩家听到同一次音效，避免相邻重复。
- 新增极速雕像 `extreme_speed`：在原版移动计算中施加四倍速度倍率，保留原版持有物减速、体力、FOV 和碰撞规则。
- 新增迟钝雕像 `delayed_move`：移动方向及松键延迟 0.3 秒，视角/交互即时；放下或菜单清理输入队列。
- 弹簧大跳成功起跳时，随机播放四段卡通音效之一；小跳、普通跳和未成功起跳不触发。`spring.ContentVersion=2`，其他弹簧节奏和力度不变。
- 四种新内容均沿用原版雕像模型和空白便签，通过统一注册加入自然候选、F8 命令和测试房。测试房中部现在共七种模组雕像，仍不增加原版自然生成总数。
- 新增 FMOD 空间音效和受兼容准入保护的一次性音效协议，音量跟随原版设置；源音频与裁剪 bank 保持本地，源码构建需先按 [音频说明](Assets/Audio/README.md) 准备资源。
- 类型/资源清单改变，联机各端需更新兼容内容。编译、源码模拟和单人游戏证据见 DEVELOPMENT；真实双端听感、转交、观察者与晚加入仍待验收。

## 0.2.15 — 2026-10-13

- 新增声控雕像（`voice`）：持有时屏蔽键盘与手柄的方向输入，以麦克风音量驱动前进；保留转向、跳跃、蹲下、奔跑与其他动作。
- 启动阈值沿用原版麦克风音量阈值，没有固定物理分贝要求或独立 CFG 参数。加入短时平滑、较低的停止阈值和过期样本保护；沿用原版静音与按键说话设置。
- 只由持有者所属客户端替换方向输入，运动、体力、碰撞和动画继续走原版路径。菜单/F8 暂停时清空声音样本，放下、转交或角色失效时释放效果；原生采样异常后停用到释放。
- 使用独立身份和空白便签，通过统一注册接入自然候选池、`spawn voice` 与测试房。四种陈列顺序为 jumping → near_view → spring → voice；快捷生成按钮仍只显示前三种。
- 类型清单改变，联机各端需更新兼容内容。Release 编译与 83 项托管模拟通过；尚未部署，游戏与真实双端待验收。

## 0.2.14 — 2026-10-13

- 新增近景雕像（`near_view`）：持有时将本机 FOV 固定为内置 15°，使用空白便签；不公开 CFG 参数，不改玩家保存的原版 FOV 设置。
- 放下、转交、死亡、换角色、断线和菜单/F8 暂停时释放镜头覆盖；保留游戏或其他 MOD 后写入的不同值。异常后停用到释放。
- 新类型通过统一注册接入自然候选池、F8 命令与测试房陈列。测试房仍按稳定 key 字母排序，三种顺序为 jumping → near_view → spring。
- 类型清单改变，联机各端需更新兼容内容。仅编译与模拟通过，游戏画面及真实双端待验收。

## 0.2.13 — 2026-10-13

- 恢复独立的跳跃雕像（`jumping`）：持有时落地后立即继续按原力度跳跃，删除早期拿起时清空体力的行为。保留原配置 ID/assetId，`ContentVersion=8`。
- 将现有弹簧玩法新增为弹簧雕像（`spring`），使用独立配置 ID/assetId，`ContentVersion=1`；保留约半米小跳、正常/稍高跳编排及落地等待 1 秒。
- 两种均挂便签，暂时复用现有弹簧图案，视觉资源独立；F8 类型列表、调试生成与测试房陈列通过注册清单接入。
- 明确 DebugMode 同时控制弹簧起跳/落地测量日志；关闭后取消尚未完成的测量，雕像效果继续运行。保留注册、异常与效果开始/停止等少量状态日志。
- 类型清单改变，联机各端需要安装相同内容。游戏内同时陈列、拾取/切换及真实双端待实测；构建与模拟记录见 [DEVELOPMENT.md](DEVELOPMENT.md)。

## 0.2.12 — 2026-10-13

### 弹簧雕像

- 将显示名改为“弹簧雕像”，保留稳定 key `jumping`、配置 ID 和 Mirror assetId。
- 小跳按约 50 厘米目标校准，所有节拍落地后等待 1 秒。保留“四次小跳后正常跳、五次小跳后稍高跳”的循环。
- 接入用户绘制的临时弹簧便签图案，保留纸张、胶带和模型法线。
- `jumping.ContentVersion` 升至 7。联机参与端需要兼容的内容版本。

### 图案制作

- 新增 `scripts/PrepareNoteArtwork.py`：把透明正方形 PNG 按稳定 key 合成到便签正面，生成内嵌运行纹理和来源哈希记录。
- 原图、纸底、胶带、背面和折边分别保留；纸张材质仍为不透明。
- 指定图案缺失时注册失败并清理资源。更换图案需要重新导入、编译、部署和重启，不支持运行期文件夹监控或热加载。
- 导入步骤见 [便签资源说明](Assets/StickyNote/README.md)。

### 验证

- Release/x64/net6.0 编译通过，0 警告、0 错误；84 项实际源码模拟和 7 项离线资源检查通过。
- 单人测试房连续采样：33 次小跳均约 0.511 米，落地到下次起跳约 0.999～1.019 秒；正常跳约 1.4 米，稍高跳约 1.7 米。
- 实机确认图案在陈列、持有和放下时可见；F8 暂停/恢复、放下停止、重新拿起恢复通过。
- 高度和间隔是当前平地场景的帧采样结果；真实双端、晚加入及特殊地形/重力条件仍待验收。

## 0.2.11 — 2026-10-13

- 根据反馈加高小跳，并将小跳落地等待从 0.10 秒延长到 0.45 秒；正常/稍高跳幅度不变。
- `jumping.ContentVersion` 升至 6。
- 交付 1024×1024 透明绘画模板和独立参考底图，当时尚未接入按 key 导入。
- Release 编译通过，82 项源码模拟通过；单人平地实测小跳多数约 13～15 厘米。

## 0.2.10 — 2026-10-13

- 将每五秒一次自动跳跃改为弹簧节奏：连续小跳，穿插正常跳或略高跳；使用固定编排，不在各客户端独立抽取随机结果。
- 只由持有者所属客户端调用原版动作入口，在对应原生起跳调用内临时调整力度并恢复。待处理请求在放下、转交、死亡、超时等情况下清理。
- 保留原版动作条件和运动/动画同步入口；`jumping.ContentVersion` 升至 5。
- Release 编译通过，73 项源码模拟通过；节点单人实测三档跳跃、F8 暂停恢复和放下停止。小跳当时约 6～10 厘米。

## 0.2.9 — 2026-10-13

- 将模型师制作的独立便签挂到自有雕像头部，加入 `CreateVisuals` 资源生命周期回调，支持注册失败回滚和正常卸载清理。
- 采用不透明纸张/胶带版本，保留原网格法线；通过已核对的指针/长度入口上传预解码 RGBA mip 链，绕开不可用的 `LoadImage` 包装。
- 加入便签本地视觉诊断命令 `visual <key> texture|white|flat`。
- 自动跳跃调整为每五秒请求一次，沿用原版动作条件；`jumping.ContentVersion` 升至 4。
- Release 编译及 33 项源码模拟通过；单人实机确认便签陈列/持有/放下可见、周期跳跃、F8 暂停恢复与放下停止。沿用并复核 0.2.8 陈列布局。

## 0.2.8 — 2026-10-13

- 20 种原版雕像收拢至测试房后部，两排各 10 个，横向间距 2.2 米、排距 3.2 米。
- 异步加载游戏自带基座的网格和材质，在自有视觉对象上只读复用，并增加静态承托碰撞。
- 中部按注册清单的稳定 key 自动陈列每种 MOD 一只，计入该类型调试限额；新增类型无需单独编写陈列分支。
- 不自动补刷已取走或销毁的雕像；MOD 类型增多时扩展房间深度。
- Release 编译、布局/启动/消息过滤源码模拟通过；实机确认基座、分区、20 种原版与 1 种 MOD 陈列及重复生成限制。

## 0.2.7 — 2026-10-12

- 新增专用单人测试房入口，需要 `--ces-test-room`、`DebugMode=true` 和 `TestRoomEnabled=true` 同时满足。
- 跳过 Logo，沿原版 KCP 流程创建本机单人房间；在初始房门外连接明亮的空旷陈列区、打开连接门并放置玩家。
- 陈列 20 种原版可拿取雕像；按无怪要求排除实际为怪物的 WanderingDice。
- 测试会话限制远端加入、骰子事件、怪物生成和存档写入；正常启动不自动建房。
- 顶部仅过滤“棋盘开启后将无法加入游戏”对应消息，保留其他提示与 HUD。
- 建立被 Git 忽略的“人类工作间”，用户导出交付统一放在其“接收文件”目录。
- Release 编译、启动/布局/提示过滤模拟通过；单人实机确认 Logo 跳过、建房、陈列、开门、玩家落点、照明和指定提示过滤。

真实双端、晚加入、全部原版雕像效果和未覆盖的生命周期边界不因上述编译或模拟通过而视为已验收。
