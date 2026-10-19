# Nexus Mods 发布操作

本文件约定 `Calypso's Expansion Set` 的 N 网发布目标、凭据填写及后续执行流程。官方接口核对日期：**2026-10-19**。发布方式采用本机直接调用 Nexus API，使用本地凭据和本机已验证的安装包。实际发布与 API／网页核验范围见下方发布记录。

## 何时执行上传

只有当次明确提出“上传 N 网”“上传到 Nexus Mods”或同等明确的发布指令时，才执行上传。引用这些词、讨论发布、要求写文档、填写 API Key、编译、打包、提交或推送 Git，都不构成上传授权。

明确授权后，先完成构建、包检查和发布说明，报告目标、版本及包内容，然后执行这次发布；无需对同一次已授权上传重复请求确认。缺少凭据、无法确定目标主文件、版本不一致或权限不足时，只询问缺少的具体信息。

不设置随 Git push、tag、GitHub Release 或定时任务自动上传的规则。一次上传授权不延续为以后每次修改后的自动发布。

同一轮需要发布 N 网并推送 GitHub 时，先完成 N 网发布及记录，再统一提交并推送源码与文档，避免为发布记录另推一次。两项操作仍分别以当次明确授权为准。

## 固定发布目标

| 项目 | 值 |
| --- | --- |
| 模组页面 | [Calypso's Expansion Set](https://www.nexusmods.com/invokyr/mods/1) |
| 游戏 domain | `invokyr` |
| 网页中的模组编号 | `1`，即游戏内编号 `game_scoped_id` |
| 默认操作 | 向该页面**现有主文件添加新版本** |
| 发布包 | ZIP 根目录默认只放 `CalypsoExpansionSet.dll` |
| 发布文案来源 | 源码与 `CHANGELOG.md` 提供变化事实，按下方文案规则精简；现有线上格式及手动校订文案作为基线；`NEXUS.txt` 仅为整页介绍草稿 |

网址中的 `1` 不应直接填进 v3 的内部 `mod_id` 或 `file_id`。这两个 ID 通过下面的只读查询获得，也可从网站 Files 页的 Advanced 或 Manage Files 编辑入口复制。[官方 ID 说明](https://github.com/Nexus-Mods/upload-action#how-to-find-the-file-id--mod-id)

## 发布文案与现有格式

发布文案面向下载和游玩的玩家，区分以下两个位置；每个版本的中文与英文放在一起，便于整块复制和阅读。

| 位置 | 内容与篇幅 |
| --- | --- |
| Files 页 Main files 中对应版本的 File description（下载文件描述） | 本次更新的快速预览，中英文各一至两句，突出最主要的变化。这里的“包体描述”指这个字段，ZIP 默认仍只放 DLL |
| Changelogs 中对应版本的更新记录 | 言简意赅地保留核心变化：主要新增内容、玩法变化、重要修复及必要的兼容/更新要求；合并相关小修，按内容多少使用少量短条目 |

- **沿用现有 N 网格式**：发布前读取对应文件描述与近期 Changelogs，沿用已采用的中英顺序、标题、列表和换行方式；同一版本内中英文内容对应，不把两个语言拆成重复的版本条目。读回的 `<br />` 等展示转换须按实际字段处理，不把转义或标记原样堆进正文。
- **保留手动校订**：以发布时线上已有的日志和文件描述为基线，保留用户手动校订。发布新版本只添加该版文案；本地旧草稿、`CHANGELOG.md` 和历史发布回执不能用于自动覆盖已有文字。
- **提炼玩家关心的变化**：以之前的公开版本为比较基准，归并中间开发版本；完整保留核心效果与重要限制，不逐条照搬开发流水账。编译检查数量、哈希、内部 ContentVersion、排查过程及本地工作台改动放在开发/发布记录中；验证范围只在影响玩家判断时简短说明，不能把编译或模拟写成实机/联机通过。
- **区分两处文案**：下载文件描述便于快速浏览，Changelogs 稍详细；两处都使用中英文，避免将同一份长日志原样填入两个字段。不例行追加“全员更新并重开房间”等固定后缀；通用联机要求放安装说明，仅在本版确有特殊兼容或迁移要求时单独说明。
- **无法读取现有格式时**：先完成中英草稿，并说明尚未核对线上版式；取得现有文案或可读页面后再执行依赖版式的写入，不根据旧回执猜测并覆盖。一般发布授权不包含重写已经手动校订的旧版日志或整页介绍。

## 获取 API Key

1. 登录拥有该模组编辑权限的 Nexus Mods 账号。
2. 打开 [API Key 设置](https://www.nexusmods.com/settings/api-keys)。
3. 找到页面底部的个人 API Key（Personal API Key）区域，按页面提供的按钮生成或显示密钥。
4. 复制**完整密钥**，填入下方配置的 `apiKey`。只填写密钥本身，不加 `Bearer `、`apikey:` 或额外空格。

这是 Nexus Mods 的个人密钥，不是 GitHub Token，也不是密码。官方 v3 API 使用请求头 `apikey`；官方作者工具也指向同一个个人密钥入口。[官方作者工具说明](https://github.com/Nexus-Mods/NexusModsAuthorToolsUE/blob/master/Resources/Markdown/Using.md) · [官方 API](https://api-docs.nexusmods.com/)

密钥只保存在本机配置中，不写进聊天、公共 Markdown、Git、发布包或日志。填写后只需说明“API 已填写，可以检查”；这句话仅授权检查，不会触发上传。

## 填写本地配置

打开项目根目录下的 **`.local/nexusmods.publish.json`**。本机已创建以下空模板；`.local/` 已被 Git 忽略。

```json
{
  "schema": "ces-nexus-publish-v1",
  "modPageUrl": "https://www.nexusmods.com/invokyr/mods/1",
  "gameDomain": "invokyr",
  "gameScopedModId": "1",
  "apiKey": "",
  "modId": "",
  "fileId": "",
  "archiveExistingVersion": false,
  "updateModVersion": true,
  "updateDescription": false
}
```

| 字段 | 填写方式 |
| --- | --- |
| `apiKey` | 必填，将完整个人 API Key 粘贴到双引号内 |
| `modPageUrl`、`gameDomain`、`gameScopedModId` | 已按本模组填写，保持即可 |
| `modId` | 可先留空；填写 v3 查询结果的内部模组 ID，以字符串保存 |
| `fileId` | 可先留空；填写本次要更新的**主文件** ID，不是某个旧版本的 ID |
| `archiveExistingVersion` | 默认 `false`，不自动归档旧版本 |
| `updateModVersion` | 默认 `true`，上传新文件版本时同步模组显示版本 |
| `updateDescription` | 默认 `false`，不自动用 `NEXUS.txt` 覆盖整页描述 |

这些是本项目的配置约定，不是游戏 CFG，也不会仅因填写就自动生效。当前没有安装自动发布工作流；后续明确授权发布时读取此配置。

## 检查密钥并查出内部 ID（只读）

可以让后续会话执行“检查 N 网配置”，也可在项目根目录的 PowerShell 中运行以下代码。代码只发出 `GET` 请求，不上传文件、不修改网站；输出仅包含模组名、ID 和文件列表，不显示密钥或完整认证响应。

```powershell
$nexusConfig = Get-Content -LiteralPath '.local/nexusmods.publish.json' -Encoding UTF8 -Raw |
    ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($nexusConfig.apiKey)) {
    throw '请先在本地配置中填写 apiKey。'
}
if ($nexusConfig.gameDomain -ne 'invokyr' -or
    [string]$nexusConfig.gameScopedModId -ne '1') {
    throw '发布目标不符合本项目约定，请先核对配置。'
}

$nexusHeaders = @{ apikey = $nexusConfig.apiKey; Accept = 'application/json' }
$nexusMod = Invoke-RestMethod -Method Get `
    -Uri 'https://api.nexusmods.com/v3/games/invokyr/mods/1' `
    -Headers $nexusHeaders -TimeoutSec 30
$nexusModId = [string]$nexusMod.data.id
if ([string]::IsNullOrWhiteSpace($nexusModId) -or
    [string]$nexusMod.data.game_scoped_id -ne '1' -or
    $nexusMod.data.name -ne "Calypso's Expansion Set") {
    throw '查询结果与目标模组不符，停止后续操作。'
}
$nexusFilesUri = 'https://api.nexusmods.com/v3/mods/' +
    [Uri]::EscapeDataString($nexusModId) + '/files'
$nexusFiles = Invoke-RestMethod -Method Get -Uri $nexusFilesUri `
    -Headers $nexusHeaders -TimeoutSec 30

[pscustomobject]@{ ModName = $nexusMod.data.name; ModId = $nexusModId }
$nexusFiles.data.mod_files | Select-Object id, name, is_active, versions_count
$nexusHeaders.Clear()
$nexusConfig.apiKey = $null
```

将输出的 `ModId` 填入 `modId`；将目标主文件那一行的 `id` 填入 `fileId`。存在多个候选文件时按文件名及用途确认，不默认选列表第一项。读取得到文件信息不等于账户已经具备写入权限，上传结果仍须以实际响应为准。

`401/403` 时核对完整密钥、登录账号及模组团队权限；`404` 时核对游戏 domain 和 ID 种类。不要通过重复创建文件或更换目标模组来绕过错误。[字段与响应以官方 OpenAPI 为准](https://raw.githubusercontent.com/Nexus-Mods/Vortex/master/packages/nexus-api-v3/schema/openapi.yaml)

## 明确要求“上传 N 网”后的执行流程

1. **确定本次内容**：优先采用明确指定的版本/文件；未指定时使用本项目当前源码经检查的 Release 构建，核对源码版本、DLL 版本和哈希。不能仅按文件修改时间挑选旧 DLL。发布授权不自动包含游戏部署、游戏启动或 Git 提交/推送。
2. **准备交付**：使用 `scripts/Build.ps1` 仅编译，按实际变更完成必要检查；把单 DLL ZIP 与分别用于 File description、Changelogs 的中英文精简文案放入新的 `人类工作间/接收文件/` 子目录，遵守上方文案规则。核对 ZIP 清单、包内 DLL 与构建哈希，保留署名及真实验证边界。
3. **准备发布摘要**：说明目标页面、待发布版本、ZIP 文件名/大小/哈希、简短变更，以及是否同步整页描述。默认只更新主文件版本、模组版本和对应更新记录；不新建重复模组页面、不删除旧文件、不改封面/许可/依赖设置。
4. **核对远端**：重新读取目标、文件版本、现有 File description 与近期 Changelogs，确认内部 ID 仍对应正确主文件，并按当前线上格式整理本次文案、保留手动校订；相同版本/包已经存在时先检查先前结果，避免重复发布。接口尚有 Experimental 标记，执行时重新核对官方文档。
5. **上传**：优先使用官方 v3 API，或锁定已核对版本的官方 `Nexus-Mods/upload-action`。不使用旧的 `/update-groups/{group_id}/versions` 路径；当前已有文件的新版本入口为 `POST /mod-files/{id}/versions`。
6. **验证并记录**：读取新文件版本并检查网页可见状态，记录版本、文件/版本 ID、链接、包哈希及时间。只有全部必要步骤成功后才报告发布完成；文件上传成功但更新记录/页面版本失败时分别报告，不能重新盲目上传整包。

官方上传动作是独立的文件发布操作。其参数 `file_id`、`mod_id` 分别对应本地 `fileId`、`modId`；`api_key` 对应 `apiKey`。如以后使用 GitHub Actions，密钥放入 `NEXUSMODS_API_KEY` Secret，只使用手动触发，并且仍遵循当次明确发布指令；不要直接照搬示例中的自动 release 触发器。[官方 Action](https://github.com/Nexus-Mods/upload-action) · [官方示例](https://github.com/Nexus-Mods/API-Example)

### 直接调用 v3 时的顺序

API 基地址为 `https://api.nexusmods.com/v3`。以下是执行说明，不是会自动运行的上传脚本。

| 步骤 | 请求 |
| --- | --- |
| 创建上传会话 | `POST /uploads`，提供包名、字节数和 MD5 |
| 传输 ZIP | 向响应的预签名 URL 发出 `PUT`，上传实际包字节 |
| 结束传输 | `POST /uploads/{upload_id}/finalise` |
| 等待就绪 | `GET /uploads/{upload_id}`，直到 `state=available` |
| 添加现有文件的新版本 | `POST /mod-files/{fileId}/versions` |
| 添加更新记录 | `POST /mods/{modId}/changelogs` |

当次明确授权同步整页介绍时，使用 `PATCH /mods/{modId}`，仅传 `description`，正文读取已确认的本地 `NEXUS.txt`；省略 `summary` 等字段以保留其现有内容。这不改变本地 `updateDescription=false` 的默认值。

小于或等于 100 MiB 的包可以使用单段上传；更大的包使用 multipart。单段 `PUT` 按官方签名要求携带 `Content-Disposition` 和 `Content-MD5`，不向存储域名转发 Nexus 的 `apikey`。MD5 请求字段用十六进制，`Content-MD5` 用相同摘要的 Base64；官方标明 **2026-12-01 起**单段创建会话将要求 MD5，本流程从现在就提供。

本次实传确认签名还包含 `Content-Type`，必须使用 **`application/octet-stream`**；即使文件是 ZIP，也不能替换为 `application/zip`，否则存储服务返回 `403 SignatureDoesNotMatch`。预签名 URL 只在创建响应中提供，不能依赖后续查询再次取得；仅在内存中使用，不写日志。

存储地址以 Nexus 返回的 HTTPS 预签名 URL 为准，不固定存储域名。正文回读可能以 `\n<br />` 表示换行，比较时按一次换行归一。v1 模组详情存在缓存；版本／介绍尚未刷新时，可通过官方 v2 GraphQL 的 `mod(modId, gameId)` 查询 `version`、`description` 和 `summary` 交叉核对，不因旧缓存重复写入。

新版本请求按当次 schema 填写 `upload_id`、`name`、`version`、`file_category` 等字段，版本取本次构建，不固定为文档编写时的版本。传输完成或会话可用都不等于文件版本已发布。更新记录接口是追加操作，超时后先读取/核对结果，再决定是否重试，避免重复文本。

创建版本成功后先保存返回标识，再用目标主文件的版本列表核对归属；响应中的 `UploadModFile.id` 不应当作持久主文件组 ID 比较。旧版在 `archive_existing_file=false` 时仍会转到 `old_version` 分类，并非删除或归档。v1 文件说明会把换行转成 `<br />`，更新记录返回非空行数组，核验时按这些格式归一后比较完整内容。模组显示版本可能短暂读取到缓存值，先只读复核，不重复创建版本。

接口细节及后续变化：[官方 v3 文档](https://api-docs.nexusmods.com/) · [官方维护的 OpenAPI](https://raw.githubusercontent.com/Nexus-Mods/Vortex/master/packages/nexus-api-v3/schema/openapi.yaml)。

## 2026-10-15 配置检查记录

- 2026-10-15，本地 JSON、固定目标和布尔选项有效；密钥已填写，配置仍被 Git 忽略。
- 密钥验证、v3 模组查询、v3 文件列表、上传者交叉核对均为 GET，四项均返回 HTTP 200；认证账号与目标模组上传者 ID 一致。
- 已查得内部模组 ID 和唯一启用文件 ID；本地 `modId`、`fileId` 保持留空，凭据配置文件未修改。后续发布前仍实时查询并核对目标文件。
- 此次线上查询版本为 0.2.30，仅代表检查时状态。
- 未创建上传会话、上传文件或修改网站；只读成功不代表实际写入已经验收。
- 后续实际发布需要当次明确指令；本文和配置文件本身不会触发发布。

## 最近一次实际发布

- **2026-10-19 19:51:37（UTC+8）**，按当次授权将现有主文件从 0.2.65 更新至 **0.2.73**，归并中间开发版的共享标记功能，同步模组显示版本、中英文下载说明／更新记录，以及 `NEXUS.txt` 的精简标记简介和独立改键配置。线上旧日志、手动校订、旧版本及简介保留。
- 内部模组 ID `45638322487297`、持久主文件 ID `8155314`、新版本 ID `45638322487307`、游戏内文件 ID `11`。
- 发布包 `CalypsoExpansionSet-0.2.73.zip`，**6,053,877 字节**，根目录仅 `CalypsoExpansionSet.dll`；SHA256 `48DA3613E55FD523A7DD7B4F2631FE0B6652B56EF9899372B96D609E42DC2C38`，线上大小及扫描元数据 SHA256 与本地一致。DLL 版本 0.2.73.0，哈希与本轮已验证构建一致。
- API 已核验主下载／模组版本、完整双语文件说明与更新记录、整页介绍及旧记录保留。公开 Files／Description 已读取到新版本和已精简的文案，配置路径正常显示；初次文件页显示病毒扫描中，随后 API 已返回匹配的扫描 SHA256。后续公开 Files／Logs 读取超时，未确认下载按钮最终状态；下载 API 因账号不具备 Premium 权限返回 403，未下载回验包字节。
- 用户明确反馈本轮实机测试无问题；发布前核对最新本机多人三端日志，均加载 0.2.73 并建立尸体描边目标，无标记异常。重新 Release 仅编译 0 警告／0 错误，单 DLL ZIP、CRC、BBCode 和双语文案检查通过。未部署或启动游戏，未暂存、提交或推送 GitHub，等待用户后续确认。
- 包、发布前后快照、三端日志核对、公开页面结果及脱敏回执保存在本机 `人类工作间/接收文件/Nexus-0.2.73-20261019/`。本次介绍同步仅按当次授权执行，凭据配置与 `updateDescription=false` 默认值保持；盒子文本按用户最新确认保留。

## 2026-10-18 0.2.65 实际发布记录

- **2026-10-18 22:54:11（UTC+8）**，按当次授权将现有主文件从 0.2.64 更新至 **0.2.65**，同步模组显示版本、精简中英说明／更新记录及整页介绍中的伪装、生活质量提升和七语言说明；保留线上版式、手动校订、旧版本与简介。
- 内部模组 ID `45638322487297`、持久主文件 ID `8155314`、新版本 ID `45638322487303`、游戏内文件 ID `7`。
- 发布包 `CalypsoExpansionSet-0.2.65.zip`，**6,028,421 字节**，根目录仅 `CalypsoExpansionSet.dll`；SHA256 `401555A3EC2A0A21A5A1116F6B6FFC77FB87E6A015CDDC8AE8AF9E7C113B9670`，线上大小及扫描元数据 SHA256 与本地一致。
- API 已核验主下载／模组显示版本、完整中英文文件说明和更新记录、整页介绍，以及旧文件／历史文案／简介保留。TinyFish 公开 Files／Description 读取失败，网页实际渲染未确认。
- Release 编译 0 警告／0 错误，包清单、CRC、解压字节、BBCode 与双语文案检查通过。游戏验收依据用户本轮测试完成反馈，本轮未部署或启动游戏。回执与前后快照见本机交付目录，研究记录见 DEVELOPMENT；凭据配置及整页介绍自动同步默认值保持，本次同步仅按当次明确授权执行。

## 2026-10-17 0.2.64 实际发布记录

- **2026-10-17 20:45:34（UTC+8）**，按当次授权将现有主文件从 0.2.63 更新至 **0.2.64**，同步模组显示版本及对应中英说明／更新记录；保留旧版本、手动校订、整页介绍和简介。
- 内部模组 ID `45638322487297`、持久主文件 ID `8155314`、新版本 ID `45638322487302`、游戏内文件 ID `6`。
- 发布包 `CalypsoExpansionSet-0.2.64.zip`，**5,891,605 字节**，根目录仅 `CalypsoExpansionSet.dll`；SHA256 `E82F4E76025C36F081240FC40DD41F88AC16E71575EB631E83F168096BC0BD92`，线上大小及扫描元数据 SHA256 与本地一致。
- API 已核验主下载／模组显示版本、完整中英文文件说明和更新记录，以及旧文件／历史文案保留。公开 Files／Logs 网页读取超时，未确认实际渲染。
- 本轮重新 Release 编译 0 警告／0 错误，源码／程序集版本、包清单、CRC、解压字节与双语文案检查通过；未追加游戏测试或部署。完整证据、前后快照及脱敏回执见本机交付目录，研究记录见 DEVELOPMENT。此次未同步整页介绍，配置默认值保持。

## 2026-10-17 0.2.63 实际发布记录（按回执补记）

- **2026-10-17 15:22:25（UTC+8）**，现有主文件从 0.2.61 更新至 **0.2.63**，同步模组显示版本及中英说明／更新记录；整页介绍与简介保持。
- 内部模组 ID `45638322487297`、持久主文件 ID `8155314`、新版本 ID `45638322487301`、游戏内文件 ID `5`。
- 发布包 `CalypsoExpansionSet-0.2.63.zip`，**5,873,355 字节**，根目录仅 `CalypsoExpansionSet.dll`；SHA256 `AF058E3A7F86EA38CC625AC6DC65741C6F88E8CD2101491523F25AF819938698`。原回执记录版本、包大小／扫描哈希、完整文案及旧记录保留检查通过；本次重新核对本地 ZIP 并与 0.2.64 发布取得的线上快照一致。
- 原 Files 网页遭反爬拦截、Logs 读取超时，未确认实际渲染。原文档改动在后续同步前回滚，现按本机回执补齐，不新增上传或游戏验收；当前手动校订后的文件说明保留。详细证据见 DEVELOPMENT。

## 2026-10-17 0.2.61 实际发布记录

- **2026-10-17 13:48:00（UTC+8）**，按当次授权将现有主文件从 0.2.58 更新至 **0.2.61**，保留旧版本及历史文案；另按明确授权用本地 `NEXUS.txt` 替换介绍正文。
- 内部模组 ID `45638322487297`、持久主文件 ID `8155314`、新版本 ID `45638322487300`、游戏内文件 ID `4`。
- 发布包 `CalypsoExpansionSet-0.2.61.zip`，**5,872,053 字节**，根目录仅 `CalypsoExpansionSet.dll`；SHA256 `08D385B918AB341F037B74755D17BC89C9A3575F90459E43EDB31CADB3E6E123`，线上大小及扫描元数据 SHA256 与本地一致。
- API 已核验主下载／模组显示版本、中英下载说明、完整中英更新记录、介绍正文与本地一致，简介及历史记录保留。公开 Files／Description 网页复核超时，未确认实际渲染。
- Release 编译 0 警告／0 错误，包清单、解压字节、BBCode 与双语文案检查通过；未追加游戏测试。完整证据、前后快照及脱敏回执见本机交付目录，研究记录见 DEVELOPMENT。此次介绍同步仅按当次授权执行，配置默认值保持。

## 2026-10-15 实际发布记录

- **2026-10-15 16:58:39（UTC+8）**，按当次明确授权向现有主文件发布 **0.2.51**；当前主下载为新版本，0.2.30 保留在旧版本分类。
- 内部模组 ID `45638322487297`，持久主文件 ID `8155314`，新版本 ID `45638322487298`，游戏内文件 ID `2`。主文件组内部名称仍为最初的 0.2.30，实际新版本显示名称为 `Calypso's Expansion Set 0.2.51`，后续选择以稳定 ID 与版本列表为准。
- 发布包 `CalypsoExpansionSet-0.2.51.zip`，**3,569,989 字节**，根目录仅 `CalypsoExpansionSet.dll`；SHA256 `E6793302917507AAA546CD7013CA2B28EE3728D2970CFBBD739D49DA14B1EE21`，线上大小及扫描元数据 SHA256 与本地一致。
- API 已确认主文件版本、模组显示版本、中英文文件说明及完整中英文更新记录；更新记录仅追加一次。整页介绍按配置保持，密钥配置未修改。
- 浏览器重新访问 Files 页停在 Cloudflare 验证页，因此未确认更新后的网页渲染；这与已成功完成的 API 上传及核验分别记录。文件页入口见本页“固定发布目标”。
- 本次重新 Release 编译为 0 警告/0 错误，DLL 与此前通过整合检查的 0.2.51 构建哈希相同。未追加游戏/真实多人验收。包、发布说明和脱敏回执保存在本机交付目录，详细证据见 DEVELOPMENT。
- 本次上传授权已执行，后续版本仍需要新的明确发布指令。
