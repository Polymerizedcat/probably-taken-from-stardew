# 虚空当铺：可能是从牢乡那拿的

**Probably Stolen × Stardew Valley 双向传送门** —— 把星露谷的作物、鱼、料理、加工品、矿石和武器，
经一道传送门送进《Probably Stolen》的当铺里，生成同名、同图标、同类别的新物品上架出售。

> **Probably Taken From Stardew** — a two-way bridge between
> *Probably Stolen* and *Stardew Valley*. Ship Stardew goods through a portal; they arrive
> in the pawnshop as real items with the original art, calories, chemistry and prices.

---

## 这两个模组

| 组件 | 游戏 | ID | 说明 |
|---|---|---|---|
| `StardewPortal` | Probably Stolen | `StardewPortal` | 当铺侧：货单 UI、物品定义、接收与上架、自动安装另一侧 |
| `DeepSpacePortal` | Stardew Valley | `NaHz.DeepSpacePortal` | 星露谷侧：传送门物体、箱子 UI、发货到当铺的数据目录 |

当铺侧在启用时会把星露谷侧的子模组（以及 SMAPI，如果缺失）自动安装到同一
`steamapps/common` 下的《Stardew Valley》目录里。

## 功能

- **货单界面**：当铺里的一本星空纸页，逐页列出待收货物，一键领取上架
- **原版美术**：物品图标直接取自星露谷的贴图集（运行时裁切导出），不是替代品
- **完整属性映射**：类型（食物/加工品/酒/材料/矿石/武器/违禁品）、占格、热量、
  化学成分、违禁品等级、价格换算
- **拒收规则**：种子、肥料、钓具、工具、饰品、家具会在星露谷弹提示并留在传送门里
- **数据驱动**：`Data/types.json` 一处控制类型、占格、价格、风味文案、化学成分

## 安装

**创意工坊**：订阅 → 在「模组」菜单里**激活** → 重启游戏。
首次启用会请求 `files` 能力（要读写星露谷目录），请确认同意。

**手动**：把 `release/StardewPortal/` 整个复制到 `<Probably Stolen>/Mods/`。

**星露谷侧**：不需要手动装。当铺模组启用时会把子模组（和缺失的 SMAPI）装到
`<steamapps>/common/Stardew Valley/`；之后用 `StardewModdingAPI.exe` 启动星露谷即可。

## 目录结构

```
src/ProbablyStolen/    当铺侧源码（csc 编译，无 NuGet 依赖）
src/StardewValley/     星露谷侧源码（SMAPI 4.x，net6.0）
tools/                 贴图/预览图/表格生成脚本
docs/                  构建说明、文件访问清单、验证说明
release/StardewPortal/ 可直接安装/上传创意工坊的成品
```

## 构建

见 [docs/BUILD.md](docs/BUILD.md)。

## 能力声明与验证

两个 manifest 都只声明 **`files`**；没有 `network`、`process`、`steam`、`dynamic-code`、
`native`、`reflection-external`、`threads`。
所有文件访问的完整清单见 [docs/FILES.md](docs/FILES.md)，创意工坊验证说明见
[docs/VERIFICATION.md](docs/VERIFICATION.md)。

## 许可

MIT。星露谷侧的 SMAPI 由 Pathoschild 以 MIT 授权，随包分发遵循其许可。
星露谷的贴图在运行时读取，不随本模组分发。
