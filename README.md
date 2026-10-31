# ONI-RingCloudTech

在《缺氧》(Oxygen Not Included) 上的「环云科技」模组。

> RingCloudTech — a content mod for Oxygen Not Included.

环云科技将会长期进行更新。因为技术力所限，本项目依然会使用 AI 辅助开发。

## 当前内容

- **可乐（Cola）** —— 自定义液体元素，相变参数与体温对照原版水编写。
- **饮水机饮品** —— 可乐可加入饮水机的饮品选项，饮用后获得「喝了可乐」效果：
  - 压力 -20 / 周期
  - 体力 +20 / 周期
  - 卡路里 +500 千卡 / 周期
  - 士气 +3
  - 运动 +4
- **饮料调配机（Beverage Mixer）** —— 2×2 配方生产建筑，`FoodRepurposing` 科技解锁：
  - 输入：水 80 kg（液体管道）+ 火椒粒 0.91 kg（搬运）+ 蔗糖 14.54 kg（搬运），合计正好 100 kg
  - 输出：可乐 100 kg（液体管道）
  - 未排产时自动暂停材料搬运
- **甜蔗（Sweet Cane）** —— 自定义作物，外形与手感对照原版「咸蔗」编写：
  - 生长：6 周期（养殖）/ 12 周期（野生），1 格宽 2 格高
  - 灌溉：净水 10 kg/周期
  - 收获：每次 1 份，每份 1200 千卡
  - 食用效果：士气 +1，但会口渴（`Thirsty`）
  - 加工：碎石机 5 kg 甜蔗（6000 千卡）→ 蔗糖 8 kg + 植物外壳 4 kg
  - 拔除：掉落 2 份植物外壳
  - 支持农场站微肥照料（`can_tinker`）
  - 种子会出现在打印舱补给包中，也有专属数据库条目
- **咖啡树（Coffee Tree）** —— 1 格宽 2 格高的作物，**只需要光照**：既不需要灌溉，也不需要施肥：
  - 生长：18 周期，光照下限 900 lux
  - 温度：263.15–343.15 K 为适宜区间，223.15 / 393.15 K 致死
  - 收获：每次 4 份咖啡豆，并额外掉落 50 kg 木材
  - 支持农场站微肥照料（加速生长）
  - 咖啡豆可作食物原料，有专属种子、数据库条目与打印舱补给包条目
- **超级电容库（Super Capacitor）** —— 4×3 储能建筑：
  - 容量 800 kJ，每周期泄漏 60 J（`JOULES_LOST_PER_SECOND = 0.1`）
  - 自身发热 0.05 kW，过热温度 50 °C，熔点 800 °C
  - 建造材料 1200 kg，建造时间 60 s
- **时间之弓（Time Bow）** —— 4×3 氦气发电机，外形参考反应炉 + 出料口，表盘指针在运行时会加速转动：
  - 发电 800 W；每周期只烧 30 kg **氦气**（气体管道输入）
  - 不产生热量；装饰 +45（范围 8 格）；掩埋时停止工作
  - 额外产出：每周期 500 kg **中子质**（原版 `Unobtanium`），从机侧出料口落地；原版没有中子质的可落地物件，模组在 `GeneratedOre` 加载阶段补了一个（贴图暂借铁矿石）
  - 建造 120 s；材料：精炼金属 400 kg + 玻璃 200 kg + 塑料 50 kg
  - 科技：自建节点 **无源能源**（基础 70 / 高级 100 / 辐射 370 / 天文 250，前置「多路传输」）
- **智能液冷机（Smart Chiller）** —— 4×3 液冷建筑，**可设定目标温度**：
  - 目标温度 −100 ~ 200 °C，详情面板里有滑块 + 数字输入框（走原版「温度阈值」侧屏）
  - 进液温度高于目标 → 一次性冷却到目标温度输出；低于等于目标 → **原样直通、不耗电**
  - 抽走的热量灌回建筑本体（机器会发烫，和原版液冷机一致）
  - 耗电 1800 W（只在真降温时计费）；可潜水；过热 200 °C；导热系数 8
  - 建造 180 s；材料：精炼金属 1200 kg + 密封圈 200 kg
  - 三层动态表现：涡轮随液流旋转、运行指针随通电顺时针转、冷却指针在降温时 ±45° 往复、屏幕读数跟随设定值实时刷新
  - 管路：中排左进右出；底排为电源口与信号口
  - 科技：原版「液体温度」（LiquidTemperature），与液冷机同节点
- **自建研究节点** —— 「无源能源」（时间之弓）与「口袋宇宙」（鸿蒙之眼，基础 200 / 高级 260 / 辐射 900 / 天文 800，前置「多路传输」+「低温燃料推进」）；节点位置由代码按前置科技自动排布
- **鸿蒙之眼（Harmony Eye）** `[ALN联动建筑第一版]` —— 9×9 巨型装置，美术与机制参考 GTNH 的同名多方块：
  - 电力：20 kW；房间类型：工业。**电力由建筑自己吃电网，不托管给 ALN 能量池**
  - **无管道 / 运输端口**：氢气与产物全部走 ALN 频道（需要安装 Applied Logistics Network）
    - 进气：每秒从所属频道取氢，补满内部 100 吨储气（`StandardSealedStorage`，不漏温）
    - 出料：配方产物**先进机器内部缓冲**（随存档保存），每秒按队列顺序向频道调度 —— 能一次全进就全进，装不下就按 ALN **该品类**（固/液/气分开算）的剩余容量装一部分，剩下的留给下一秒；频道满时驻留缓冲、不丢产物
    - 频道绑定：详情面板里的「ALN 频道」页签，用 ALN 原生频道下拉选择（与驱动器、存储单元同一个控件），选择随存档保存
    - **未安装 ALN 时整个建筑不注册**：`GeneratedBuildings.LoadGeneratedBuildings` 的 Prefix 会把本配置类型从注册列表里剔除，建造菜单、调试菜单、`Assets` 里都不存在；装回 ALN 后重启即恢复，不影响存档
  - 两条配方，各消耗 50 吨氢气（耗时见下方配置项，默认 1800 s / 3 周期）：
    - **熔融金属小行星** —— 熔融铁 / 铜 / 铝 / 钴 / 金 / 锌 / 银 / 镍 / 铱 / 铅 / 钨 / 铌 与岩浆，每种 50 吨
    - **理想生活小行星** —— 砂岩、沉积岩、藻类、铜矿、铁矿、铝矿、金汞齐、镍矿、锌矿、黑钨矿、石墨、花岗岩、黑曜石、菌泥、银金矿、泥土、煤炭各 50 吨，石油 / 水 / 盐水 / 浓盐水 / 污染水各 50 吨，氯气 50 吨
  - 分层渲染：环体常驻；断电时黑球与空间扭曲消失；未排产时彩球消失
  - 建造材料：30 吨中子质
  - **外部配置**：`harmonyeye.cfg`（mod 根目录，与 dll 同级）

### 鸿蒙之眼配置（harmonyeye.cfg）

改完保存、重启游戏生效；文件被删会自动重新生成一份默认配置。

```ini
recipe_time = 60      # 配方耗时（秒），两种配方共用；默认 1800
output_rate = 5000    # 每秒最多往 ALN 频道推多少 kg 产物
```

`#` / `;` / `//` 开头的行是注释，行尾也可以跟 `#` 注释；键名不区分大小写；数值会被夹到 `0.1 ~ 1000000`。
- **灭菌（Germ Sanitizer）** —— 把盐水与浓盐水登记为游戏原生「灭菌物」（写入各病菌的 `growthRules`），并让铜制管道与储库杀灭运输/储存物上的食物中毒、粘液肺与僵尸孢子：
  - 铜制液体 / 气体管道：每秒衰减 50%（`COPPER_PIPE_KILL = 0.50`）
  - 铜制储液库 / 储气库：每秒衰减 35%（`COPPER_STORAGE_KILL = 0.35`）

## 联动

本模组与下列模组做了**软依赖联动**（对方未安装时相关代码自动跳过，不影响本模组）：

### Better Food Effects

> Steam 创意工坊 ID `3796760626`　staticID `BetterFoodEffect`

该模组会按食物所用食材的组成，给予复制人 1 周期的营养效果（蔬菜 / 蛋白质 / 甜美水果 / 碳水 / 油脂 / 海鲜 / 辛辣）。

本模组把**甜蔗登记为「甜美水果（Fruit）」食材**，因此用甜蔗制作的食物会触发甜美水果效果。

实现方式：对方未提供公开的注册接口（`IngredientTagDatabase.Tags` 是私有静态字段），
本模组通过反射写入，并校验对方的 `Signature` 常量；版本不符时只警告、不中断。


## 安装

有两条路，按需求选：

| 渠道 | 稳定性 | 说明 |
|---|---|---|
| **Steam 创意工坊** | 稳定版 | 尚未发布，敬请期待 |
| **本仓库（自行编译）** | 测试版 | 包含最新改动，**不保证稳定**，适合想尝鲜或帮忙测试的人 |

### 通过本仓库安装（测试版）

clone 仓库并自行编译，这样拿到的是最新代码，可能带有未经充分验证的改动：

```bash
git clone https://github.com/yifencass/ONI-RingCloudTech.git
cd ONI-RingCloudTech
dotnet build -c Release
```

然后把编译产物和资源按下面的结构放到 mod 目录：

```
文档\Klei\OxygenNotIncluded\mods\Local\RingCloudTech
```

（即 `%USERPROFILE%\Documents\Klei\OxygenNotIncluded\mods\Local\RingCloudTech`）

因为 `anim/`、`elements/`、`translations/`、`codex/` 都已入库，最省事的做法是把
仓库内容整个复制过去，再放上编译出的 DLL：

```
RingCloudTech/
├── RingCloudTech.dll        ← dotnet build 产物，从 bin/ 复制
├── mod.yaml
├── mod_info.yaml
├── anim/assets/             ← 已入库，直接复制
├── timebow_clock/           ← 时间之弓的表盘指针层
├── smartchiller_layers/     ← 智能液冷机的涡轮 / 指针 / 字形层
├── codex/Plants/
├── elements/
│   └── cola.yaml
└── translations/
    ├── zh.po
    └── en.po
```

> `bin/`、`obj/`、`*.dll` 都在 `.gitignore` 里，克隆后需要自己编译一次。

## 构建

需要 [.NET SDK](https://dotnet.microsoft.com/download)（netstandard2.1 目标，SDK 6 及以上均可）。

```bash
dotnet build -c Release
```

默认引用 Steam 默认安装位置下的游戏 DLL。如果游戏装在别处，用环境变量指定：

```powershell
$env:ONI_MANAGED_PATH = "<游戏安装目录>\OxygenNotIncluded_Data\Managed"
dotnet build -c Release
```

或者命令行传参：

```bash
dotnet build -c Release -p:ManagedPath="<游戏安装目录>\OxygenNotIncluded_Data\Managed"
```

编译产物在 `bin\RingCloudTech.dll`，复制到 mod 目录即可。

## 项目结构

| 文件 | 说明 |
|---|---|
| `RingCloudTech.cs` | 入口 + 全部 Harmony 补丁 |
| `BeverageMixerConfig.cs` | 饮料调配机建筑定义与配方 |
| `CoffeeTreeRegistrar.cs` | 咖啡树的物品 / 食物 / 作物定义与注册 |
| `CoffeeTreePlantConfig.cs` | 咖啡树植物本体 |
| `CoffeeBeanConfig.cs` | 咖啡豆食物定义 |
| `SuperCapacitorConfig.cs` | 超级电容库建筑定义 |
| `TimeBowConfig.cs` | 时间之弓建筑定义（800 W 氦气发电机） |
| `TimeBowSolidSpawner.cs` | 时间之弓的中子质出料 |
| `TimeBowClock.cs` | 时间之弓表盘：运行时叠加三根会转的指针 |
| `SmartChillerConfig.cs` | 智能液冷机建筑定义 |
| `SmartChiller.cs` | 智能液冷机冷却逻辑 + 目标温度阈值侧屏（`IThresholdSwitch`） |
| `SmartChillerVisual.cs` | 智能液冷机图像层：涡轮 / 两根指针 / 屏幕温度文字 |
| `CustomTechs.cs` | 自建研究节点（无源能源 / 口袋宇宙）与位置排布 |
| `Diagnostics.cs` | 调试用诊断（研究卡片布局 dump、元素块温度检查） |
| `HarmonyEyeConfig.cs` | 鸿蒙之眼建筑定义与两条配方 |
| `HarmonyEyeVisual.cs` | 鸿蒙之眼的分层渲染与彩球运动 |
| `HarmonyEyeInfo.cs` | 鸿蒙之眼的氢气储量状态条目 |
| `HarmonyEyeCodex.cs` | 鸿蒙之眼的数据库条目 |
| `GermSanitizer.cs` | 盐水灭菌规则与铜制管道 / 储库灭菌 |
| `SweetCanePlantConfig.cs` | 甜蔗植物本体（作物、灌溉、种子） |
| `SweetCaneFoodConfig.cs` | 甜蔗可食用产物 |
| `SweetCaneRegistrar.cs` | 甜蔗的食物定义与作物定义注册 |
| `SweetCaneCarePackage.cs` | 把甜蔗种子加入打印舱补给包 |
| `STRINGS.cs` | 全部可翻译文本（默认英文） |
| `LocalizationPatch.cs` | 注册 STRINGS 树并加载 `translations/*.po` |
| `elements/cola.yaml` | 可乐元素定义 |
| `codex/Plants/` | 甜蔗的数据库条目 |
| `translations/` | 语言文件，新增语言只需添加 `<code>.po` |
| `anim/` | 动画资源，已入库，克隆即可用 |
| `_assets/` | 素材原始工作区（**不入库**，仅本地） |

### 动画资源

`anim/` 已纳入版本库，克隆后即可直接组装出可用 mod，不需要额外拷贝素材。

```
anim/assets/
├── harmonyeye/          # 鸿蒙之眼（kanim + 分层裸贴图 *.raw）
├── supercapacitor/      # 超级电容库
├── timebow/             # 时间之弓
├── smartchiller/        # 智能液冷机
├── coffee_tree/         # 咖啡树植株
├── coffee_tree_seed/    # 咖啡树种子
├── coffee_bean/         # 咖啡豆
├── sweet_cane/          # 甜蔗植株
├── seed_sweet_cane/     # 甜蔗种子
└── sweet_cane_food/     # 甜蔗产物
```

另外两个目录放**运行时叠加的贴图层**（不走 kanim，用 `SpriteRenderer` 直接叠在建筑上）：

- `timebow_clock/`：`hand_hour.png` / `hand_minute.png` / `hand_second.png`（以枢轴为中心裁好的方块图）
- `smartchiller_layers/`：`turbine.png` / `needle_run.png` / `needle_cool.png` / `glyphs.png`

每个目录三个文件：`<名>.png`、`<名>_anim.bytes`、`<名>_build.bytes`。
kanim 名 = 目录名 + `_kanim`。

> ⚠️ `anim/` 下**只能**放 `<分类>/<kanim名>/` 这两层目录。
> 多出任何目录（比如备份），只要凑够两层就会被当成 kanim 加载；
> 而同名 kanim 是**后来者覆盖**，静默无报错 —— 很容易出现「文件改对了但游戏里没变」。
>
> ⚠️ `_anim.bytes` / `_build.bytes` 内部嵌着原始名，改名时要一起改写，
> 否则内部自引用会和目录名不一致。注意内部名**不一定等于**文件名
> （原版就存在 `batteryMed` 对 `batterymed` 这种驼峰差异）。

#### 鸿蒙之眼的分层贴图

鸿蒙之眼的环体用 kanim 加载，其余六层（空间扭曲 / 黑球 / 彩球 / 十字 / 矩阵）
走 `MeshRenderer` + 手动 `renderQueue`，因为 ONI 里所有 `KBatchedAnimController`
共用批次，队列是批次级的，无法单独抬高某一层。

这些层用裸 RGBA 贴图：`anim/assets/harmonyeye/{sphere,cross,ball,matrix0..3}.raw`，
格式为 `int32 宽 + int32 高 + 自底向上 RGBA`，运行时用 `File.ReadAllBytes` 读入后
逐像素 `SetPixels32`（不用 `LoadRawTextureData`）。

> 这些贴图与 `harmonyeye.png` 在生成脚本里由同一份参数产出，
> 改尺寸时两边要一起重新导出。

## 多语言

文本集中在 `STRINGS.cs`，默认值为英文。翻译放在 `translations/<语言代码>.po`：

```po
msgctxt "STRINGS.ELEMENTS.COLA.NAME"
msgid "Cola"
msgstr "可乐"
```

目前提供中文（`zh`）与英文（`en`）。新增语言时复制一份改 `msgstr` 并把文件名改成对应语言代码即可，无需改代码。

## 版权与协议

- 本项目**代码**采用 **MIT 协议** 开源，详见 [LICENSE](LICENSE)。
- 所有来自《缺氧》的贴图、动画与代码，版权归 **Klei Entertainment** 所有。
- 本模组**打包了基于原版资源的衍生动画**（`anim/` 目录，如甜蔗复用咸蔗动画、
  鸿蒙之眼/超级电容的占位动画复用原版建筑）。这些资产仅供本模组在游戏内使用，
  不构成原创作品，也不得单独提取分发。
- 若 Klei 对本项目打包衍生资产有异议，会立即移除 `anim/` 并改为运行时引用方案。

## 感谢

- **[ChiYuKe/ONIModTutorial](https://github.com/ChiYuKe/ONIModTutorial)** —— 《缺氧》Mod 开发笔记与教程。本项目的元素 `Substance` 补齐、`ComplexRecipe` 配方写法、`LocString` 本地化流程等多处实现，都参考了这份资料，省下大量逆向摸索的时间。
- **[GTNewHorizons/GT-New-Horizons-Modpack](https://github.com/GTNewHorizons/GT-New-Horizons-Modpack)** —— Minecraft 的 GregTech: New Horizons 整合包。本模组的「鸿蒙之眼」在设计理念上参考了整合包中的同名多方块结构，视觉母题（环绕的合金环、中心的绝对黑暗）亦由此而来。
- **Better Food Effects** —— 本模组与它的「食物营养效果」系统做了联动。
- **Klei Entertainment** —— 创造了《缺氧》以及开放的 Mod 接口。
- 以及所有在开发过程中提供反馈、帮忙测试的人。

## 关于 RingCloud

这事实上是个虚拟代称。因为有时能收到其他人的帮助，所以在制作方上习惯以完全虚构的创作团队——**环云制作组**自居。

这个制作团队完全是虚构的代号，没有固定成员，也没有规范组织，只是自称罢了 :-)
