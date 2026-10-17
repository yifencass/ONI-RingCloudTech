# ONI-RingCloudTech

在《缺氧》(Oxygen Not Included) 上的「环云科技」模组。

> RingCloudTech — a content mod for Oxygen Not Included.

## 当前内容

- **可乐（Cola）** —— 自定义液体元素，相变参数与体温对照原版水编写。
- **饮水机饮品** —— 可乐可加入饮水机的饮品选项，饮用后获得「喝了可乐」效果：
  - 压力 -20 / 周期
  - 体力 +20 / 周期
  - 卡路里 +500 千卡 / 周期
  - 士气 +3
  - 运动 +4
- **饮料调配机（Beverage Mixer）** —— 2×2 配方生产建筑，`FoodRepurposing` 科技解锁：
  - 输入：水 90.91 kg（液体管道）+ 二氧化碳 4.55 kg（搬运）+ 火椒粒 4.55 kg（搬运）
  - 输出：可乐 100 kg（液体管道）
  - 未排产时自动暂停材料搬运

## 安装

1. 从 Releases 下载，或自行编译（见下）。
2. 把整个目录放到：
   ```
   文档\Klei\OxygenNotIncluded\mods\Local\RingCloudTech
   ```
   （即 `%USERPROFILE%\Documents\Klei\OxygenNotIncluded\mods\Local\RingCloudTech`）
3. 目录结构应如下，`RingCloudTech.dll` 必须与 `elements/`、`translations/` 同级：
   ```
   RingCloudTech/
   ├── RingCloudTech.dll
   ├── mod.yaml
   ├── mod_info.yaml
   ├── elements/
   │   └── cola.yaml
   └── translations/
       ├── zh.po
       └── en.po
   ```

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
| `STRINGS.cs` | 全部可翻译文本（默认英文） |
| `LocalizationPatch.cs` | 注册 STRINGS 树并加载 `translations/*.po` |
| `elements/cola.yaml` | 可乐元素定义 |
| `translations/` | 语言文件，新增语言只需添加 `<code>.po` |

## 多语言

文本集中在 `STRINGS.cs`，默认值为英文。翻译放在 `translations/<语言代码>.po`：

```po
msgctxt "STRINGS.ELEMENTS.COLA.NAME"
msgid "Cola"
msgstr "可乐"
```

目前提供中文（`zh`）与英文（`en`）。新增语言时复制一份改 `msgstr` 并把文件名改成对应语言代码即可，无需改代码。

## 版权与协议

- 本项目代码采用 **MIT 协议** 开源，详见 [LICENSE](LICENSE)。
- 所有来自《缺氧》的贴图、动画与代码，版权归 **Klei Entertainment** 所有。本模组仅通过游戏内既有资源引用，未重新分发任何 Klei 资产。
- 模组中复用的原版建筑贴图（汽水机 `sodamaker_kanim`）同理属于 Klei。

## 关于 RingCloud

这事实上是个虚拟代称。因为有时能收到其他人的帮助，所以在制作方上习惯以完全虚构的创作团队——**环云制作组**自居。

这个制作团队完全是虚构的代号，没有固定成员，也没有规范组织，只是自称罢了 :-)
