# 鸿蒙之眼 × Applied Logistics Network 联动研究

> 基于 ALN 9.3.0（工坊 3753045682）反编译源码调研。
> 反编译产物在 `C:\Users\Lenovo\tools\aln-src\`，共 362 个 `.cs`。

## 一、ALN 的核心模型

ALN 的一切围绕 **频道（channel）**：一个 int 标识符，玩家可在建筑上改名（`ALN_GlobalState`）。
所有建筑按角色注册进 `ALN_NetworkRegistry` 的分频道表：

| 角色 | 组件 | 作用 |
|---|---|---|
| 驱动器 | `ALN_DriveController` | 把物品在「世界 ↔ 网络」之间搬运，带缓冲 |
| 存储单元 | `ALN_StorageCellController` | 网络的容量来源（固体/液体/气体单元） |
| 装配接口 | `ALN_AssemblyInterfaceBase` | **接管**附近建筑，替它从网络取料/送料 |
| 输入/输出接口 | `ALN_ImportInterfaceBase` / `ALN_ExportInterfaceBase` | 管道/轨道与网络的边界 |
| 能量单元 | `ALN_EnergyCellController` | 能量池容量 |
| 能量池电池 | `ALN_EnergyPoolBattery` | 像原版电池一样从电网充能 |
| 合成中心 | `ALN_CraftCenter` / `ALN_OrderCenter` | 远端排产与订单 |

注册 API 全部是 `public static`，第三方可直接调用：

```csharp
ALN_NetworkRegistry.RegisterAssemblyInterface(ctrl);
ALN_NetworkRegistry.TryGetAssemblyInterfaces(int channel, out List<ALN_AssemblyInterfaceBase> list);
ALN_NetworkRegistry.GetImportInterfaces(channel);
```

`ALN_NetworkRegistry` 里还提供了 `AssignChannel()` / `AutoDetectChannel()` / `GetActiveChannels()`。

## 二、关键发现：ALN 会自动接管任何 ComplexFabricator

`ALN_Preload_RequesterPatch`（Harmony 挂在 `GeneratedBuildings.LoadGeneratedBuildings` 的 Postfix）会遍历**全部 prefab**，给下面四类建筑无差别挂 `ALN_MaterialRequester`：

```csharp
Assets.GetPrefabsWithComponent<ComplexFabricator>()
Assets.GetPrefabsWithComponent<ElementConverter>()   // 排除同时有 ComplexFabricator 的
Assets.GetPrefabsWithComponent<EnergyGenerator>()
Assets.GetPrefabsWithComponent<SteamTurbine>()
```

鸿蒙之眼在 `ConfigureBuildingTemplate` 里 `AddOrGet<ComplexFabricator>()`，因此**只要装了 ALN，鸿蒙之眼身上就已经有一个 `ALN_MaterialRequester`**——不需要 RingCloudTech 做任何事。

真正缺的是「谁给它频道」：

```
ALN_MaterialRequester.Channel => _managingInterface?.channelID ?? -1
```

`_managingInterface` 由附近的 **装配接口** 调用 `TryAdoptRequester` 时写入。

## 三、装配接口的接管规则

- 合成接口建筑 ID `ALN_CRAFTINGINTERFACE`，2×2，`Kind = Crafting`
- 上限 `MaxManagedTargets` 默认 16（`ALN_Options.AssemblyMaxManaged`）
- 扫描半径 `AssemblyScanRadius` 默认 8
- 接管条件（`IsInAutoRange`）：

```csharp
// 默认：切比雪夫距离 ≤ 半径
Math.Max(|tx - fx|, |ty - fy|) <= radius
```

- `AcceptsRequester` 要求 `requester.craftingControlEnabled == (Kind == Crafting)`，
  即：**合成接口只接管「开启合成控制」的机器，供应接口只接管不控合成的**

拒绝原因枚举：`NotEligible / NotSupplyInterface / WrongWorld / OwnedByOther / OutOfRange / LimitReached`。

### ⚠️ 9×9 建筑的原点陷阱

`targetCell` 取的是**建筑的原点格**（左下角）。鸿蒙之眼 9×9，如果接口贴在右上角一侧，
距离原点可能已经 8 格开外 → 判定 `OutOfRange`，接管失败。

**结论：合成接口必须放在鸿蒙之眼左下角附近（切比雪夫距离 ≤ 8）。**

## 四、三方可直接调用的静态 API

```csharp
// 取存量 / 容量
float ALN_NetworkStorage.GetChannelAmount(int channel, Tag tag);
float ALN_NetworkStorage.GetChannelCapacity(int channel);
bool  ALN_NetworkStorage.IsChannelFull(int channel);

// 存入网络
float ALN_NetworkStorage.DepositToChannel(int channel, Tag tag, float amount, int fromCell,
        float temperature = -1f, float freshness = -1f, byte diseaseIdx = 255, int diseaseCount = 0,
        Tag subSpeciesID = default, bool seedAnalyzed = false, List<string> seedMutations = null,
        float eggViability = -1f, float eggIncubation = -1f,
        ALN_MaterialRequester productionRequester = null, bool skipEnergyCharge = false);

// 从网络取出并塞进指定 Storage
float ALN_NetworkStorage.WithdrawAndStore(int channel, Tag tag, float amount,
        Storage targetStorage, string targetName, bool preventMerge = false,
        Tag seedBaseTag = default, float targetTemp = -1f);

// 能量池
ALN_NetworkEnergy.GetChannelEnergy(int channel, out float storedKJ, out float capacityKJ);
ALN_NetworkEnergy.TryDeductOperationEnergy(int channel, float baseJ, int fromCell, string opName,
        bool silentIfInsufficient = false);

// 供电状态
ALN_NetworkPower.IsNetworkWorking(int channel);
ALN_NetworkPower.CalculatePowerConsumption(int channel);
```

注意：`IALN_ChannelCarrier`（`int ChannelID { get; }`）是 **internal**，跨程序集无法实现，
所以第三方建筑只能「被接管」，不能自己声明成频道载体。

## 五、鸿蒙之眼现状

| 项 | 值 |
|---|---|
| 逻辑 | `ComplexFabricator`，`duplicantOperated = false` |
| 配方 | 输入氢气 50 t，耗时 1800 s |
| 产物 | 熔融 12 种 / 理想生活 23 种，每种 50 t |
| 进气 | `ConduitConsumer`（气体管道）+ 独立 `Storage` 100 t |
| 出料 | `ConduitDispenser` + `SolidConduitDispenser` + 3 个副端口占位 |
| 电力 | 20 kW，`EnergyConsumer` |
| 储库 | 全部 `StandardSealedStorage`（不漏温） |

## 六、三种联动方案

### 方案 A：纯配置联动（零代码）

装 ALN + 在鸿蒙之眼左下角附近放一个**合成接口**，同一个频道。
ALN 自动接管 → 自动从网络拉 50 t 氢气进 `inStorage`、可远程排队配方。

- ✅ 无代码、无维护成本、ALN 升级不会挂
- ⚠️ 输出可能被 ALN 接管进网络：一次 1,400 t 产物，远超网络容量 → 需要实测
- ⚠️ `ALN_BuildingDefaults` 可能给未知建筑套默认的 `bypassPipeCheck` 等参数

### 方案 B：主动桥接组件（推荐）

在 RingCloudTech 里写一个软依赖组件 `HarmonyEyeAlnBridge`：

1. 启动时若 ALN 未安装 → 直接 `return`（反射探测 `AccessTools.TypeByName("Applied_Logistics_Network.ALN_NetworkStorage")`）
2. 找到自己的装配接口（`GetComponent` 或扫描半径内 `ALN_AssemblyInterfaceBase`）→ 拿 `channelID`
3. 每秒：
   - `GetChannelAmount(channel, hydrogen)`，缺口按 `WithdrawAndStore(...)` 灌进 `inStorage`
   - 产物按 `DepositToChannel(...)` 直接写进网络（可加节流/容量判断）
   - 可选：`TryDeductOperationEnergy` 按运营耗电扣 ALN 能量

- ✅ 行为可控（限流、只导氢气、产物可选是否入网）
- ✅ 不依赖 ALN 的自动接管逻辑，版本变动影响小
- ⚠️ 需要反射封装 + 版本校验

### 方案 C：电力联动

让鸿蒙之眼的 20 kW 由 ALN 能量池供给（`ALN_NetworkPower` / `ALN_NetworkEnergy`），
类似 `ALN_DriveController` 主动从电网抽电的做法。工程量最大，收益最小。

## 七、建议

1. **先验证方案 A**：开一局，装 ALN，放合成接口在左下角，看日志里有没有
   `ALN_MaterialRequester` 被接管、氢气是否自动补满。这一步不写代码就能确认
   「鸿蒙之眼是否天生兼容」。
2. 若 A 的输出接管行为不可接受 → 转 **方案 B**，用主动桥接精确控制进出。
3. 记录 `ALN_MaterialRequester.Mode`（`CFOnly` 表示只认 ComplexFabricator），
   确认 `duplicantOperated=false` 不影响拉料。

## 八、待验证

- [ ] 9×9 建筑能否被接管（原点距离假设）
- [ ] 一次 1,400 t 产物进网络会不会撑爆存储单元
- [ ] `craftingControlEnabled` 对 `duplicantOperated=false` 的机器是否自动开启
- [ ] ALN 的 `ALN_Patch_*` 是否会误伤鸿蒙之眼的 `ConduitConsumer` / `ConduitDispenser`
