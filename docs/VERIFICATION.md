# 创意工坊 `files` 能力验证 · 说明材料

## 为什么需要 `files`

这个模组是**跨游戏**的：它把《Stardew Valley》的物品送进《Probably Stolen》，
因此必须在运行时把星露谷侧的配套模组安装到隔壁游戏目录里。这超出了游戏 API 的范围，
只能声明 `files`。

## 具体读写了什么

完整清单见 [FILES.md](FILES.md)。摘要：

**只读（自己的模组目录）**
`Data/types.json`、`Data/food_calories.csv`、`Data/inbox.json`、`Data/state.json`、
`Textures/Items/*.png`、`Textures/Paper/*.png`、`Bridge/payload.json`

**写（自己的模组目录）**
`Data/types.json`（仅缺失时写入默认表）、`Data/state.json`（已领取 uuid）、
`Textures/Items/*.png`（从星露谷贴图裁出的物品图标）

**写（隔壁游戏的模组目录 —— 声明 `files` 的唯一原因）**
`<steamapps>/common/Stardew Valley/Mods/DeepSpacePortal/**`（解包自 `Bridge/payload.json`）、
`bridge.json`，以及 **仅在 SMAPI 缺失时** 安装 `StardewModdingAPI.exe` 及其依赖。

**存在性检查**
`Application.dataPath` 的父目录（定位游戏根目录，因为创意工坊模组位于
`steamapps/workshop/content/<appid>/<id>`，无法靠向上遍历找到游戏目录）、
`StardewModdingAPI.exe` / `.dll` 是否存在。

## 明确没有做的事

- 不读游戏存档目录（存档数据走 `ModHelper` 的 ModData 系列 API）
- 不读 Steam 配置、注册表、凭证、浏览器数据、钱包、其它用户目录
- 不发网络请求、不启动进程、不修改游戏程序集
- 不使用反射、P/Invoke、动态代码、线程

## 自查

游戏根目录放一个空的 `dev.txt`，F8 控制台执行：

```
mod-scan StardewPortal
```

预期输出 `scanned capabilities: files`，并且没有 deprecated / hard-blocked 条目
（本次构建在 scanner v4 下通过：`files` 是扫描器唯一检出的能力）。

## 构建产物校验

```
DLL SHA-256
StardewPortal.dll      DBA0B220D54CDC5BCC7381411B9AA0A317BE31FECCFDF7AAE0BEE99DCA99A538
DeepSpacePortal.dll    EEA47B0F7144DBEB9BB1214633B58203E246280C35455F6A9175079486D97EFA
```

`release/StardewPortal/` 是创意工坊上传的完整内容（含 `Bridge/payload.json`，
星露谷侧子模组与 SMAPI 以 base64+gzip 文本形式存放，脚本 `verify_payload.py`
可逐字节还原校验）。

## 申请渠道

游戏官方社区的 modding 频道（开发者维护签名白名单）。申请时附上创意工坊物品链接、
`mod-scan` 输出、DLL 校验值与本仓库地址。
