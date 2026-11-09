# 虚空当铺：可能是从牢乡那拿的（Probably Taken From Stardew）

一个把 **《深空当铺：可能是偷的》** 和 **《星露谷物语》** 连起来的双向 Mod。

- 在星露谷里摆一个**传送门**，把手里的水果、蔬菜、加工品放进去、发货；
- 回到深空当铺，翻开**星露谷传送门**这本货单，按「领取」把货收进店里上架出售；
- 货物会沿用**星露谷原版的 16×16 图标**（子 Mod 在发货时直接从星露谷的资源里抠图写出来），并自动生成对应的物品属性（名称、价格、类型）。

---

## 1. 安装（一次性）

1. 把整个 `StardewPortal` 文件夹放进
   ```
   ...\steamapps\common\Probably Stolen\Mods\
   ```
2. 启动《深空当铺》→ 打开游戏内的**模组菜单** → 找到 **星露谷传送门 (Stardew Valley Portal)** → 启用。
   它声明了一个 `files` 能力（要读写存档侧的文件），游戏会弹一次权限确认，正常同意即可。
3. 启用成功后，这个 Mod 在下次启动时会自动完成下面这些事（控制台日志里能看到 `[StardewPortal]`）：
   - 在同级的 `steamapps\common\Stardew Valley` 里检测星露谷；
   - 如果星露谷还没装 **SMAPI**，把随 Mod 附带的 SMAPI 装进去；
   - 把星露谷侧的子 Mod `DeepSpacePortal` 装进 `Stardew Valley\Mods\`；
   - 写一份 `bridge.json`（告诉星露谷侧把货物写到哪）。

> **星露谷怎么启动（重要）**：装了 SMAPI 之后，请**不要**直接双击 `StardewModdingAPI.exe`（那样会丢 Steam、也可能因为缺 deps 闪退）。
> 正确做法是在 Steam 里设置启动选项：
>
> 库 → 右键《Stardew Valley》→ 属性 → 通用 → 启动选项，填：
> ```
> "D:\EntertainmentsPro\SteamLibrary\steamapps\common\Stardew Valley\StardewModdingAPI.exe" %command%
> ```
> 然后照常从 Steam 点「开始游戏」。会先弹出 SMAPI 控制台，再进游戏；每次启动都能在控制台看到
> `Loaded N mods` 和 `虚空当铺：可能是从牢乡那拿的`。

## 2. 在星露谷里发货

- 第一次读档时，Mod 会给你一个 **深空当铺传送门** 道具，并尝试在农舍里摆一个。
- 拿着传送门对着地面右键可以自己摆一个（最多摆几个都行，共用同一个传送门库存）。
- 对着传送门右键 → 打开箱子界面 → 把要卖的东西放进去。
- **关掉箱盖（或按 F）＝ 发货**。货物会立刻出现在深空当铺的货单里。
- 控制台命令：`portal_info`（看路径）、`portal_give`（再要一个门）、`portal_send`（把门里的东西直接发走）。

## 3. 在深空当铺里收货

- 货单是道具 **「星露谷传送门」**（第一次读档时自动放进背包）。
- 翻开它：第一页是状态和「领取全部 / 刷新清单」，后面每页 6 件货。
- 点「领取」→ 物品直接进背包/仓库，之后就像普通货物一样摆到柜台卖。
- 价格＝星露谷售价 × `priceMultiplier`（默认 **2**，可在 `config.json` 里改）。

## 4. 文件都放在哪

```
Mods/StardewPortal/
├─ manifest.xml            模组清单（游戏读它）
├─ StardewPortal.dll       深空当铺侧逻辑
├─ config.json             价格倍率等
├─ install-to-stardew.bat.txt   手动兜底安装脚本（用之前把名字改回 .bat）
├─ Textures/Items/         物品图标（16×16 PNG，文件名＝sprite 键）
├─ Data/
│  ├─ inbox.json           星露谷发来的货物清单（子 Mod 写、本 Mod 读）
│  └─ state.json           已领取记录（本 Mod 写）
└─ Bridge/
   └─ payload.json         星露谷子 Mod + SMAPI，base64+gzip 打包成一个纯文本文件
```

> **为什么子 Mod 是"打包"的？** 游戏的模组扫描器（`payload-file`）会**硬拦截** Mod 目录里任何
> `.dll / .exe / .bat`（递归检查子目录），也会拦高熵的未知二进制。所以子 Mod 和 SMAPI 只能以
> 纯文本形式随 Mod 携带，启用时由 `StardewPortal.dll` 解码、解压、写到星露谷目录里。
> **千万别把这些文件解出来放回 Mod 目录**，否则模组会直接变成"无法激活"。

`inbox.json` 结构（可以手工加条目，游戏重启后生效）：

```json
{
  "version": 1,
  "priceMultiplier": 2.0,
  "items": [
    {
      "uuid": "唯一id",
      "key": "sd_o256",
      "name": "番茄",
      "sprite": "sd_o256",
      "desc": "……",
      "category": "vegetable",
      "price": 60,
      "qty": 3
    }
  ]
}
```

- `sprite` 对应 `Textures/Items/<sprite>.png`，**必须是 16 的倍数**（原版是 16×16），否则格子形状会对不齐；
- 同一个 `key` 只注册一次物品定义，之后再加条目就是"再来一单"；
- `category` 会映射成游戏内的物品类型：`drink/wine/juice` → DRINK，`treat/sweet` → TREAT，`material/resource` → MATERIAL，其它 → VEGETABLE（基础类型一律是 FOOD）。

## 5. 排错

| 现象 | 处理 |
| --- | --- |
| 模组菜单里看不到 / 不能启用 | 看 `%LOCALAPPDATA%Low\Questing Goose Studio\Probably Stolen\Player.log` 里的 `[ModLoader]` 行，通常会写明原因 |
| 货单里没货 | 先确认星露谷那边真的点了发货；再看 `Data/inbox.json` 是否被更新 |
| 图标是空白 | 看 `Textures/Items/` 里有没有对应的 PNG；子 Mod 抠图失败会在 SMAPI 控制台留警告 |
| 提示「payload-file … 无法激活」 | 说明 Mod 目录里被放进了 .dll/.exe/.bat。删掉它们，只保留 `Bridge/payload.json`；或者直接重新解压一份干净的 Mod |
| 星露谷里没有传送门 | 子 Mod 没装好，或没启用；启用一次深空当铺侧（它会在 `Stardew Valley\Mods\DeepSpacePortal` 里写 `DeepSpacePortal.dll` + `manifest.json`），也可以跑 `install-to-stardew.bat.txt`（改名成 .bat） |
| `StardewModdingAPI.exe` 一闪就没 | 99% 是缺 `StardewModdingAPI.deps.json`。把游戏目录里的 `Stardew Valley.deps.json` 复制一份、改名成 `StardewModdingAPI.deps.json` 即可（本 Mod 启用时也会自动补） |
| 星露谷启动崩 | 确认 SMAPI 版本 ≥ 4.0，并且不是用旧的 `Stardew Valley.exe` 启动 |
