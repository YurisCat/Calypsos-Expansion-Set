# 雕像顶部箴言

顶部箴言以短句提示雕像特征。中文优先采用四字双句，尽量押韵、保持节奏对称，已经确认的文案按原文保留。英文采用原创尾韵双句，参考《勇敢者游戏》（1995）棋盘谜语的节奏与诙谐警告语气；不照搬电影台词，不为押韵添加未经确认的玩法。详细机制及参数放在使用说明和设计文档中，箴言不承担完整玩法说明。

中文 MOD 文案来自类型定义的 `ShortDescription`，中文原版文案来自 `UI/OriginalIdolAdvice.cs`；英文名称与箴言集中在 `UI/IdolTextLocalization.cs`，按实际配置 ID、key 和原版/MOD 身份匹配。英文用分号分隔两句，同样不含换行且不超过 80 字符。简介仅供开发参考，不作为游戏提示。

名称和箴言跟随本机游戏的 I2 语言设置：中文及其地区变体保留现有中文，英文及其他尚无译文的语言使用英文；这不代表已提供其他语言翻译。运行中切换语言后，最多约半秒更新语言选择，下次指向刷新采用新文案，无需重启或新增 CFG。未初始化时先使用英文；读取异常保留上次语言并重试。真实字体、长句布局和原生交互 UI 刷新仍需游戏验收。

`[UI] ShowIndividualIdolNames=true` 时显示以下正式名称；关闭时原版保留原名，MOD 按语言统一显示“卡里普索拓展包雕像”或“Calypso's Expansion Set Idol”。名称开关不影响顶部箴言。终局雕像、缺失拼图的名称未指定，两个开关状态都保留原名。

## 原版雕像

| 原版类型 | 中文名称 | 中文箴言 | 英文名称 | 英文箴言 |
| --- | --- | --- | --- | --- |
| 普通记分 | 普通雕像 | 普普通通，搬运轻松 | Plain Idol | No hidden hex, no secret bite; just bear this burden home tonight. |
| 游荡 | 游荡雕像 | 雕像长腿，不追后悔 | Wandering Idol | It sprouts two legs and flees your hold; give chase before the trail grows cold. |
| 音乐／红灯 | 红灯雕像 | 红头别走，小心扎手 | Red Light Idol | While music plays, step light and spry; when red lights glow, move and you cry. |
| 打滑 | 滑冰雕像 | 打呲溜滑，远离悬崖 | Skating Idol | Your feet will slip, your steps will slide; beware the cliff beside your stride. |
| 反向操控 | 反向雕像 | 走左往右，按前马后 | Reverse Idol | Your steps and gaze defy your will; reverse your instincts, test your skill. |
| 翻转视野 | 镜像雕像 | 左右翻转，方向难判 | Mirror Idol | The mirrored world will twist your view; beware which way is right for you. |
| 蒙蔽／黑雾 | 致盲雕像 | 迷雾环绕，撞怪吓尿 | Blinding Idol | A shroud of black conceals the way; set down your load to spy the fray. |
| 消隐 | 水晶雕像 | 越近越显，越远越险 | Crystal Idol | Far friends and foes escape your sight; draw near to bring them back to light. |
| 缩小 | 变小雕像 | 叽里呱啦，小小一趴 | Shrinking Idol | Your body shrinks, your pace runs slow; mind every ledge before you go. |
| 喷火 | 喷火雕像 | 转头喷火，不分敌我 | Firebreathing Idol | Its turning head will spit a flame; both friend and foe can burn the same. |
| 雷击 | 雷电雕像 | 天雷滚滚，化为齑粉 | Lightning Idol | The warning marks where bolts will bite; step clear before the flash of light. |
| 吐骰／混沌 | 吐骰雕像 | 烂骰乱吐，没啥好处 | Dice-Spitting Idol | It spits out dice that bounce and roll; their wicked tricks may take their toll. |
| 傀儡 | 傀儡雕像 | 呼朋唤友，提线木偶 | Puppet Idol | Another's hands direct your stride; agree which way before the ride. |
| 借眼／摄像 | 摄像雕像 | 第二人称，容易头昏 | Camera Idol | A friend's own eyes become your view; ask them to keep a watch on you. |
| 恶作剧／传送 | 传送雕像 | 跳刀闪现，瞬间不见 | Teleporting Idol | One sudden blink, you're far away; find where you are before you stray. |
| 重型 | 重型雕像 | 不痒不痛，单纯很重 | Heavy Idol | No secret curse to make you quake; just heavy steps with each you take. |
| 幻觉 | 幻觉雕像 | 亦假亦真，小心别疯 | Hallucination Idol | False monsters mingle with the true; ask friends to check what stalks your view. |
| 美杜莎／凝视 | 凝视雕像 | 瞅你咋地，看我干啥 | Gazing Idol | A staring friend will freeze in place; warn them to turn from its cold face. |
| 连锁／绳索 | 绳索雕像 | 两两相绑，有福同享 | Rope Idol | A rope unites your fates as one; agree your path before you run. |
| 终局雕像 | 保留原名 | 拿了快跑，回家才好 | 保留原版本地化名称 | Take up the last and head for home; no time remains for you to roam. |
| 缺失拼图 | 保留原名 | 还没玩到，莫名其妙 | 保留原版本地化名称 | A missing piece, a puzzling plight; heed the quest to set things right. |

## MOD 雕像

当前启用十四种；0.2.30 公开稳定版曾暂停怪相，0.2.33 整合版保留修正后的怪相注册。下表按当前启用清单列出已确认文案。

| 雕像 | key | 中文箴言 | 英文名称 | 英文箴言 |
| --- | --- | --- | --- | --- |
| 大头雕像 | big_head_fisheye | 大头大头，凹眼凸头 | Big Head Idol | Your head swells large, the edges bend; a fish-eyed world awaits you, friend. |
| 迟钝雕像 | delayed_move | 延迟输入，容易走步 | Sluggish Idol | You bid your feet to turn or stay; they heed your wish a beat away. |
| 鸭步雕像 | duck_step | 走路鸭叫，别被听到 | Duckstep Idol | Your waddling steps may quack a tune; those hunting ears may find you soon. |
| 极速雕像 | extreme_speed | 神行飞速，长驱直入 | Speed Idol | With racing feet you dash and fly; let go, and still the world goes by. |
| 弹跳雕像 | jumping | 落地就跳，开怀大笑 | Bouncing Idol | Your feet touch down, then up you bound; beware the ears that catch the sound. |
| 怪相雕像 | monster_disguise | 怪物模样，队友着相 | Monster Disguise Idol | A monster's mask, a borrowed face; no borrowed powers join the chase. |
| 像素雕像 | mosaic | 方格景色，麦块福特 | Pixel Idol | The world breaks down to squares of light; a blocky veil now fills your sight. |
| 近景雕像 | near_view | 拿起放大，注意脚下 | Zoom Idol | The world draws close, the edges hide; look down before your next bold stride. |
| 描边雕像 | outline | 黑底白线，轮廓浮现 | Outline Idol | On blackest night, white edges glow; no hidden things these outlines show. |
| 接力雕像 | relay | 越走越慢，快找伙伴 | Relay Idol | The longer held, the slower you; pass on the weight to start anew. |
| 疑神雕像 | suspicious | 怪声四起，干吓自己 | Paranoia Idol | False cries resound where none appear; yet living hunters still may hear. |
| 倒立雕像 | upside_down | 上下翻转，左右不管 | Upside-Down Idol | The floor swings high, the ceiling low; mind which way your glances go. |
| 声控雕像 | voice | 发声前进，闭嘴立定 | Voice Idol | Speak aloud to spur your stride; hold your tongue and steps subside. |
| 宽体雕像 | wide_body | 压缩拉长，宽体横梁 | Widebody Idol | Your shape grows wide, your view sinks low; a flattened friend puts on a show. |

配置方式见 [README](../README.md#配置与-f8-调试)，显示流程与原版 UI 边界见 [设计说明](ARCHITECTURE.md#指向名称与短说明)。
