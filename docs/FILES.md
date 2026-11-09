# 文件访问清单（给创意工坊验证用）

两个模组都只声明了 `files` 能力。下面是代码里**全部**的文件系统操作。

---

## 一、StardewPortal（当铺侧，Probably Stolen）

### 1. 读自己的模组文件夹（只读）

| 路径 | 用途 |
|---|---|
| `<mod>/Data/types.json` | 规则表 |
| `<mod>/Data/food_calories.csv` | 热量覆盖（可选） |
| `<mod>/Data/inbox.json` | 星露谷送来的货物清单（只读） |
| `<mod>/Data/state.json` | 已领取的 uuid（只读） |
| `<mod>/Textures/Items/<name>.png` | 物品图标 |
| `<mod>/Textures/Paper/<name>.png` | 货单 UI 美术 |
| `<mod>/Bridge/payload.json` | 星露谷子模组 + SMAPI 的 base64+gzip 打包 |

### 2. 写自己的模组文件夹

| 路径 | 用途 |
|---|---|
| `<mod>/Data/types.json` | **仅在不存在时**写入默认规则表 |
| `<mod>/Data/state.json` | 记录已领取的 uuid（防重复领取） |
| `<mod>/Textures/Items/<name>.png` | 从星露谷贴图裁出的物品图标（16/32/48 px） |

### 3. 写另一个游戏（这是声明 `files` 的原因）

启动时如果检测到同一个 `steamapps/common` 下存在《Stardew Valley》：

| 路径 | 用途 |
|---|---|
| `<common>/Stardew Valley/Mods/DeepSpacePortal/**` | 解包 `payload.json`：子模组 DLL、manifest、资源 |
| `<common>/Stardew Valley/StardewModdingAPI.exe`（及其依赖） | **仅当 SMAPI 缺失时**安装 |
| `<common>/Stardew Valley/Mods/DeepSpacePortal/bridge.json` | 写入数据路径与价格倍率 |

### 4. 只做存在性检查

- `Application.dataPath` 的父目录，用于定位游戏根目录（工坊模组位于
  `steamapps/workshop/content/<appid>/<id>`，不能靠向上遍历）
- `<common>/Stardew Valley/StardewModdingAPI.exe` / `.dll` 是否存在

### 5. 不做的

- ❌ 不读游戏存档（`Saves/`）——存档数据全部走 `ModHelper` 的 ModData 系列 API
- ❌ 不读 Steam 配置、注册表、凭证、浏览器数据、钱包
- ❌ 不遍历用户目录，不做通配扫描
- ❌ 不发网络请求
- ❌ 不启动进程（不执行 shell / 外部程序）
- ❌ 不使用反射、P/Invoke、动态代码、线程

---

## 二、DeepSpacePortal（星露谷侧，SMAPI）

| 路径 | 用途 |
|---|---|
| `<save>/<UniqueID>.json` | 传送门箱内的物品（SMAPI `Helper.Data.WriteSaveData`，由 SMAPI 管理） |
| `<probably stolen>/Mods/StardewPortal/Textures/Items/*.png` | 发货时导出物品图标（路径来自 `bridge.json`） |
| `<probably stolen>/Mods/StardewPortal/Data/inbox.json` | 追加待收货物记录（路径来自 `bridge.json`） |

路径来自当铺侧写入的 `bridge.json`；若该文件不存在，星露谷侧不做任何跨目录写入。

---

## 三、验证方式

游戏内 F8 控制台（游戏根目录放空的 `dev.txt` 后可用）：

```
mod-scan StardewPortal
```

预期：`scanned capabilities: files`，无 deprecated / hard-blocked 条目。
