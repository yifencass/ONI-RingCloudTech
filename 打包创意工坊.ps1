# ============================================================
#  环云科技（RingCloudTech）· Steam 创意工坊打包脚本
#
#  用法（Windows PowerShell / pwsh 均可）：
#    powershell -NoProfile -ExecutionPolicy Bypass -File .\打包创意工坊.ps1
#    powershell -NoProfile -ExecutionPolicy Bypass -File .\打包创意工坊.ps1 -Install
#    powershell -NoProfile -ExecutionPolicy Bypass -File .\打包创意工坊.ps1 -Version 1.0.1
#    powershell -NoProfile -ExecutionPolicy Bypass -File .\打包创意工坊.ps1 -SkipBuild
#
#  路径可覆盖（本机默认值写在下面，换机器时用环境变量指定）：
#    $env:ONI_BUILD_DIR   编译目录（默认：仓库同级 oni_mod_build\RingCloudTech）
#    $env:ONI_WORKSHOP_DIR 打包输出根目录（默认：仓库同级 oni_mod_build\workshop）
#    $env:ONI_MANAGED_PATH 游戏 Managed 目录（默认：Steam 默认安装位置）
#    $env:ONI_LOCAL_MODS   mods\Local 目录（默认：文档目录下）
#
#  产物：
#    <ONI_WORKSHOP_DIR>\RingCloudTech\             ← 可直接上传/放入 mods 的干净目录
#    <ONI_WORKSHOP_DIR>\RingCloudTech_v<版本>.zip  ← 备份/分发用压缩包
# ============================================================
param(
    [string]$Version = "",
    [switch]$Install,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

# 仓库根 = 本脚本所在目录
$Repo = Split-Path -Parent $MyInvocation.MyCommand.Path
$Sibling = Split-Path -Parent $Repo

$BuildDir  = if ($env:ONI_BUILD_DIR)    { $env:ONI_BUILD_DIR }    else { Join-Path $Sibling "oni_mod_build\RingCloudTech" }
$OutRoot   = if ($env:ONI_WORKSHOP_DIR) { $env:ONI_WORKSHOP_DIR } else { Join-Path $Sibling "oni_mod_build\workshop" }
$Managed   = if ($env:ONI_MANAGED_PATH) { $env:ONI_MANAGED_PATH } else { "D:\SteamLibrary\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed" }
$LocalMods = if ($env:ONI_LOCAL_MODS)   { $env:ONI_LOCAL_MODS }   else { Join-Path ([Environment]::GetFolderPath("MyDocuments")) "Klei\OxygenNotIncluded\mods\Local" }

$PkgName   = "RingCloudTech"
$PkgDir    = Join-Path $OutRoot $PkgName
$ArtDir    = Join-Path $Repo "_assets"

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

# ── 0. 版本号（不指定就沿用 mod_info.yaml 里的） ─────────────────
$infoPath = Join-Path $Repo "mod_info.yaml"
$infoRaw  = Get-Content $infoPath -Raw -Encoding UTF8
if ($Version -ne "") {
    $infoRaw = $infoRaw -replace 'version:\s*"[^"]*"', "version: `"$Version`""
    [System.IO.File]::WriteAllText($infoPath, $infoRaw, (New-Object System.Text.UTF8Encoding($false)))
    Write-Step "mod_info.yaml 版本号已改为 $Version"
}
$modVersion = ([regex]::Match($infoRaw, 'version:\s*"([^"]*)"')).Groups[1].Value
if ($modVersion -eq "") { $modVersion = "1.0.0" }

# ── 1. 编译 ────────────────────────────────────────────────────
if (-not $SkipBuild) {
    Write-Step "同步源码到编译目录并编译"
    $exclude = @("$Repo\_archive", "$Repo\_assets", "$Repo\sp_editor", "$Repo\.git",
                 "$Repo\timebow_clock", "$Repo\smartchiller_layers",
                 "$BuildDir\bin", "$BuildDir\obj")
    $xd = @()
    foreach ($p in $exclude) { $xd += "/XD"; $xd += $p }
    robocopy $Repo $BuildDir /E /NFL /NDL /NJH /NJS /NP @xd | Out-Null

    $log = Join-Path $env:TEMP "ringcloudtech_workshop_build.log"
    dotnet build (Join-Path $BuildDir "RingCloudTech.csproj") -c Release "-p:ManagedPath=$Managed" > $log 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "编译失败，日志：" -ForegroundColor Red
        Select-String -Path $log -Pattern "error" | ForEach-Object { $_.Line.Trim() }
        exit 1
    }
    $errs = (Select-String -Path $log -Pattern "error CS").Count
    Write-Step "编译完成：$errs 个错误"
}

# 编译产物在 bin\ 下；优先取最新的那份（仓库根目录偶尔残留旧 dll，不能误用）
$dllCandidates = @(
    (Join-Path $BuildDir "bin\RingCloudTech.dll"),
    (Join-Path $BuildDir "bin\Release\netstandard2.1\RingCloudTech.dll"),
    (Join-Path $BuildDir "RingCloudTech.dll")
)
$dll = $dllCandidates | Where-Object { Test-Path $_ } |
       Sort-Object { (Get-Item $_).LastWriteTime } -Descending | Select-Object -First 1
if (-not $dll) { Write-Host "找不到 RingCloudTech.dll" -ForegroundColor Red; exit 1 }
Write-Step ("使用 dll：{0}（{1:N0} 字节，{2}）" -f $dll, (Get-Item $dll).Length, (Get-Item $dll).LastWriteTime)

# ── 2. 组装干净包目录 ──────────────────────────────────────────
Write-Step "组装包目录：$PkgDir"
if (Test-Path $PkgDir) { Remove-Item $PkgDir -Recurse -Force }
New-Item -ItemType Directory -Path $PkgDir -Force | Out-Null

# 运行期必需的目录（整目录复制）
foreach ($d in @("anim", "elements", "codex", "translations", "smartchiller_layers", "timebow_clock")) {
    $src = Join-Path $Repo $d
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $PkgDir $d) -Recurse -Force
    }
}

# 运行期必需的文件
$files = @(
    @{ From = (Join-Path $Repo "mod.yaml");          To = "mod.yaml" },
    @{ From = $infoPath;                              To = "mod_info.yaml" },
    @{ From = (Join-Path $Repo "harmonyeye.cfg");     To = "harmonyeye.cfg" },
    @{ From = (Join-Path $Repo "LICENSE");            To = "LICENSE" },
    @{ From = (Join-Path $Repo "README.md");          To = "README.md" },
    @{ From = $dll;                                   To = "RingCloudTech.dll" }
)
foreach ($f in $files) {
    if (-not (Test-Path $f.From)) { Write-Host "缺少文件：$($f.From)" -ForegroundColor Red; exit 1 }
    Copy-Item $f.From (Join-Path $PkgDir $f.To) -Force
}

# ── 3. 生成创意工坊封面 preview.png（1280×720） ─────────────────
Write-Step "生成 preview.png"
Add-Type -AssemblyName System.Drawing
$W = 1280; $H = 720
$bmp = New-Object System.Drawing.Bitmap $W, $H
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = "AntiAlias"
$g.InterpolationMode = "HighQualityBicubic"
$g.TextRenderingHint = "ClearTypeGridFit"

# 背景：深色金属渐变 + 细网格
$rect = New-Object System.Drawing.Rectangle 0, 0, $W, $H
$bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect,
      ([System.Drawing.Color]::FromArgb(255, 17, 23, 30)),
      ([System.Drawing.Color]::FromArgb(255, 40, 54, 66)), 90.0
$g.FillRectangle($bg, $rect)
$gridPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(26, 120, 200, 220)), 1
for ($x = 0; $x -lt $W; $x += 64) { $g.DrawLine($gridPen, $x, 0, $x, $H) }
for ($y = 0; $y -lt $H; $y += 64) { $g.DrawLine($gridPen, 0, $y, $W, $y) }

# 底部危险条纹
$hazardPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 232, 185, 58)), 14
$hazardBg  = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 32, 36, 42))
$g.FillRectangle($hazardBg, 0, $H - 26, $W, 26)
for ($x = -40; $x -lt $W + 40; $x += 56) { $g.DrawLine($hazardPen, $x, $H - 4, $x + 30, $H - 22) }

# 两台机器（贴图坐标 = 1024×768 原画，1 格 = 256 px）
$artDir = Join-Path $ArtDir "smartchiller_layers"
$bodyPng   = Join-Path $ArtDir "..\smart_chiller_body.png"
$bowPng    = Join-Path $ArtDir "..\time_bow_preview.png"
$leftX = 26;  $leftY = 246; $leftW = 508; $leftH = 381          # 智能液冷机
$rightX = 736; $rightY = 246; $rightW = 508; $rightH = 381      # 时间之弓
$sc = $leftW / 1024.0

function Draw-Rot([System.Drawing.Image]$img, [double]$cx, [double]$cy, [double]$scale, [double]$angle) {
    $dw = $img.Width * $scale; $dh = $img.Height * $scale
    $g.TranslateTransform([float]$cx, [float]$cy)
    $g.RotateTransform([float]$angle)
    $g.DrawImage($img, [float](-$dw / 2), [float](-$dh / 2), [float]$dw, [float]$dh)
    $g.ResetTransform()
}
function Art-Pt([double]$ax, [double]$ay) {
    return @(($leftX + $ax * $sc), ($leftY + $ay * $sc))
}

if (Test-Path $bodyPng) {
    $body = [System.Drawing.Image]::FromFile($bodyPng)
    $g.DrawImage($body, [float]$leftX, [float]$leftY, [float]$leftW, [float]$leftH)
    $body.Dispose()

    # 涡轮（枢轴 = 画布 (378,404)）
    $p = Art-Pt 378 404
    $turb = [System.Drawing.Image]::FromFile((Join-Path $artDir "turbine.png"))
    Draw-Rot $turb $p[0] $p[1] $sc 18
    $turb.Dispose()

    # 机器运行指针（枢轴 (700,450)）：顺时针偏 35°
    $p = Art-Pt 700 450
    $nr = [System.Drawing.Image]::FromFile((Join-Path $artDir "needle_run.png"))
    Draw-Rot $nr $p[0] $p[1] $sc 35
    $nr.Dispose()

    # 冷却进行指针（枢轴 (872,450)）：逆时针偏 40°
    $p = Art-Pt 872 450
    $nc = [System.Drawing.Image]::FromFile((Join-Path $artDir "needle_cool.png"))
    Draw-Rot $nc $p[0] $p[1] $sc -40
    $nc.Dispose()

    # 屏幕上动态温度字形「-20.0 °C」
    $glyphsPng = Join-Path $artDir "glyphs.png"
    if (Test-Path $glyphsPng) {
        $glyphs = [System.Drawing.Image]::FromFile($glyphsPng)
        $chars = "0123456789-.°CFK"
        function Draw-GlyphText([string]$text, [double]$ax, [double]$ay) {
            $i = 0
            foreach ($ch in $text.ToCharArray()) {
                $idx = $chars.IndexOf($ch)
                if ($idx -ge 0) {
                    $dx = $leftX + ($ax + $i * 34) * $sc
                    $dy = $leftY + $ay * $sc
                    $srcR = New-Object System.Drawing.Rectangle ($idx * 34), 0, 34, 80
                    $dstR = New-Object System.Drawing.RectangleF ([float]$dx), ([float]$dy), ([float](34 * $sc)), ([float](80 * $sc))
                    $g.DrawImage($glyphs, $dstR, $srcR, [System.Drawing.GraphicsUnit]::Pixel)
                }
                $i++
            }
        }
        Draw-GlyphText "-20.0" 660 158
        Draw-GlyphText "°C" 834 158
        $glyphs.Dispose()
    }
}

if (Test-Path $bowPng) {
    $bow = [System.Drawing.Image]::FromFile($bowPng)
    $g.DrawImage($bow, [float]$rightX, [float]$rightY, [float]$rightW, [float]$rightH)
    $bow.Dispose()
}

# 标题与副标题
$fontBig  = New-Object System.Drawing.Font "Microsoft YaHei", 72, ([System.Drawing.FontStyle]::Bold)
$fontSub  = New-Object System.Drawing.Font "Consolas", 26, ([System.Drawing.FontStyle]::Bold)
$titleBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 236, 246, 250))
$glowBrush  = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 79, 208, 224))
$shadowBrush= New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(170, 0, 0, 0))
$g.DrawString("环云科技", $fontBig, $shadowBrush, 56, 42)
$g.DrawString("环云科技", $fontBig, $titleBrush, 52, 38)
$g.DrawString("RING CLOUD TECH", $fontSub, $glowBrush, 56, 150)

# 特性条（居中、单行、不越界）
$fontTag = New-Object System.Drawing.Font "Microsoft YaHei", 16
$tagBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 176, 196, 208))
$tagText = "可设目标温度的液冷机  ·  氦气驱动的时间之弓  ·  口袋宇宙里的鸿蒙之眼  ·  自建研究节点  ·  双作物与饮料产业"
$tagSize = $g.MeasureString($tagText, $fontTag)
$g.DrawString($tagText, $fontTag, $tagBrush, [float](($W - $tagSize.Width) / 2), 196)

# 机器名标签
$fontLabel = New-Object System.Drawing.Font "Microsoft YaHei", 19, ([System.Drawing.FontStyle]::Bold)
$labelBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 224, 238, 244))
$g.DrawString("智能液冷机", $fontLabel, $labelBrush, [float]($leftX + 12), 628)
$g.DrawString("时间之弓",   $fontLabel, $labelBrush, [float]($rightX + 12), 628)

# 版本角标
$chipBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 79, 208, 224))
$g.FillRectangle($chipBrush, $W - 206, 24, 182, 40)
$fontChip = New-Object System.Drawing.Font "Consolas", 20, ([System.Drawing.FontStyle]::Bold)
$chipTextBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 12, 18, 22))
$g.DrawString("v$modVersion", $fontChip, $chipTextBrush, ($W - 192), 30)

$previewPath = Join-Path $PkgDir "preview.png"
$bmp.Save($previewPath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Step "preview.png 已生成（1280×720）"

# ── 4. 可选：覆盖安装到本地 mods\Local ──────────────────────────
if ($Install) {
    $target = Join-Path $LocalMods $PkgName
    Write-Step "安装到 $target"
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    Copy-Item $PkgDir $target -Recurse -Force
}

# ── 5. 打 zip ─────────────────────────────────────────────────
$zip = Join-Path $OutRoot ("{0}_v{1}.zip" -f $PkgName, $modVersion)
if (Test-Path $zip) { Remove-Item $zip -Force }
Write-Step "压缩包：$zip"
Compress-Archive -Path $PkgDir -DestinationPath $zip -CompressionLevel Optimal

# ── 6. 汇总 ───────────────────────────────────────────────────
$total = (Get-ChildItem $PkgDir -Recurse -File | Measure-Object -Property Length -Sum).Sum
Write-Host ""
Write-Host "打包完成" -ForegroundColor Green
Write-Host ("  版本   : v{0}" -f $modVersion)
Write-Host ("  目录   : {0}" -f $PkgDir)
Write-Host ("  压缩包 : {0}  ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))
Write-Host ("  解压后 : {0:N1} MB，{1} 个文件" -f ($total / 1MB), (Get-ChildItem $PkgDir -Recurse -File).Count)
if ($Install) { Write-Host ("  已安装 : {0}" -f (Join-Path $LocalMods $PkgName)) }
