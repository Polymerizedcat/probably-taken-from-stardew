# 构建说明

## 环境

- Windows（两边的构建脚本都按 Windows 路径写成，Linux/macOS 把路径分隔符换掉即可）
- [.NET SDK](https://dotnet.microsoft.com/download) 6.0 或更高
- 一份《Probably Stolen》安装（提供 `Assembly-CSharp.dll` 等引用）
- 一份《Stardew Valley》1.6+ 安装（星露谷侧引用 SMAPI 与游戏程序集）

下面用两个环境变量指路：

```bash
set PS=C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen
set SV=C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley
```

---

## 一、当铺侧（StardewPortal）

不依赖 NuGet，直接用 Roslyn 编译器：

```bash
bash src/ProbablyStolen/build.sh
```

脚本会读取 `%PS%\Probably Stolen_Data\Managed` 里的程序集，产出
`src/ProbablyStolen/out/StardewPortal.dll`，并复制 `manifest.xml`。

产物安装到 `%PS%\Mods\StardewPortal\`（连同 `Textures/`、`Bridge/payload.json`、
`Data/types.json`），见 `release/StardewPortal/` 的目录形状。

## 二、星露谷侧（DeepSpacePortal）

```bash
cd src/StardewValley
dotnet build -p:GamePath="%SV%" -p:RefsPath="%SV%"
```

`RefsPath` 指向包含 `StardewModdingAPI.dll` 与 `Newtonsoft.Json.dll` 的目录
（SMAPI 安装目录本身就是）。

产物：`bin/Debug/net6.0/DeepSpacePortal.dll`。

## 三、打包桥接文件

当铺侧通过 `Bridge/payload.json`（base64 + gzip）携带星露谷子模组与 SMAPI，
避免模组目录里出现未声明的 DLL（游戏扫描器会拒绝启用）。

```bash
python src/StardewValley/pack_payload.py --sv "%SV%"
```

脚本把以下内容写进 payload：

- `Mods/DeepSpacePortal/{manifest.json, DeepSpacePortal.dll, assets/portal.png}`
- SMAPI 本体（`StardewModdingAPI.exe`、`.dll`、`StardewModdingAPI.deps.json`、
  `smapi-internal/**`，若存在）

校验：

```bash
python src/StardewValley/verify_payload.py
```

预期输出 `RESULT: PASS (payload round-trips to identical bytes)`。

> 注意：`StardewModdingAPI.deps.json` 必须是 `Stardew Valley.deps.json` 的副本，
> 否则 SelfContained 的 apphost 会退回到 muxer 并打印 `host-options` 后闪退。

## 四、美术与预览图

```bash
python tools/make_panel.py          # 货单纸页 + 按钮图标
python tools/make_icons.py          # 书本图标
python tools/make_preview_final.py  # 创意工坊预览图（需要 .dsh_tools/preview_src 素材）
```

## 五、规则表

`Data/types.json` 由代码内嵌的默认值生成（**文件存在时绝不覆盖**），字段：

| 字段 | 作用 |
|---|---|
| `byCategory` / `byId` / `byObjectType` | 星露谷类别 → 游戏内类型 |
| `extraTypes` / `removeTypes` | 追加/摘除标签（如加工品加 `FOOD`、咖啡去掉 `FOOD`） |
| `gridSize` | 形状图案（`"1"`、`"1,1;1,1"`、`"0,1;1,0"`） |
| `basePrice` / `valueScale` / `value` | 价格：原始标价覆盖、系数、固定值、全局系数 |
| `flavor` | 风味文案（`byId` 空串表示不套用类别文案） |
| `chemComposition` | 化学成分（水/咖啡因/化学污染物/梦帽菇提取物） |
| `contrabandLevelById` / `valueMultiplierById` | 违禁品等级与倍率 |
| `rejectCategories` | 拒收类别 |
| `caloriesPerEdibility` / `minCalories` | 热量换算 |
