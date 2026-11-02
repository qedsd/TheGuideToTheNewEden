<#
.SYNOPSIS
  一键发布绿色版：dotnet publish → 完整 exe（GitHub）→ Gitee 分片 → update.json。

.DESCRIPTION
  默认不需要任何参数：
    ./tools/packaging/make-update-package.ps1
  会读取 csproj 的版本号（VersionPrefix-VersionSuffix），执行单文件发布，并生成：
    update-package/TheGuideToTheNewEden_v4.0.0.exe        完整包（上传 GitHub）
    update-package/TheGuideToTheNewEden_v4.0.0.exe.001    Gitee 分片
    update-package/TheGuideToTheNewEden_v4.0.0.exe.002    Gitee 分片
    update-package/update.json

  仓库、Gitee 地址、输出目录、分片大小都有固定默认值，正常发版不用手输。
  需要发正式版时：
    ./tools/packaging/make-update-package.ps1 -Version 4.0.0
  （-Version 会同时传给 dotnet publish，保证 exe 内部版本与清单一致。）

  只重新生成清单/分片、不重新编译时：
    ./tools/packaging/make-update-package.ps1 -SkipPublish

.EXAMPLE
  # 一键发布当前 csproj 版本
  ./tools/packaging/make-update-package.ps1

.EXAMPLE
  # 发正式版 4.0.0（覆盖 csproj 的 alpha/beta 后缀）
  ./tools/packaging/make-update-package.ps1 -Version 4.0.0

.EXAMPLE
  # 用已有 exe 生成发布物（不编译）
  ./tools/packaging/make-update-package.ps1 -SkipPublish
#>
[CmdletBinding()]
param(
    # 留空则读 csproj 的 VersionPrefix / VersionSuffix
    [string]$Version = "",

    # 留空则用 v$Version；必须与 GitHub/Gitee 的 Release tag 一致
    [string]$Tag = "",

    # 留空则自动发布到 artifacts\publish-single 并取 TheGuideToTheNewEden.exe
    [string]$ExePath = "",

    # 留空则用 <仓库根>\update-package
    [string]$OutputDir = "",

    # 跳过 dotnet publish，直接用已有 exe
    [switch]$SkipPublish,

    [string]$PublishProfile = "win-x64-single-file",

    [string]$GitHubRepo = "qedsd/TheGuideToTheNewEden",

    [string]$GiteeRepo = "qedsd/TheGuideToTheNewEden",

    # 留空则用 https://gitee.com/<GiteeRepo>/releases/download/<Tag>
    [string]$GiteeBaseUrl = "",

    [int]$MaxPartMB = 90,

    # 自定义完整包名；留空则 <原名>_<Tag>.exe
    [string]$FullName = "",

    [string]$Notes = "",

    [string]$NotesFile = ""
)

$ErrorActionPreference = "Stop"

function Get-ProjectVersion {
    param([string]$ProjectPath)

    [xml]$project = Get-Content -LiteralPath $ProjectPath -Raw -Encoding UTF8
    $prefix = @($project.Project.PropertyGroup | ForEach-Object { $_.VersionPrefix } | Where-Object { $_ }) | Select-Object -First 1
    $suffix = @($project.Project.PropertyGroup | ForEach-Object { $_.VersionSuffix } | Where-Object { $_ }) | Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($prefix)) {
        throw "读不到 csproj 的 VersionPrefix：$ProjectPath"
    }

    if ([string]::IsNullOrWhiteSpace($suffix)) {
        return [string]$prefix
    }

    return "$prefix-$suffix"
}

# 脚本放在 <仓库根>\tools\packaging\ 下
$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$ProjectPath = Join-Path $RepoRoot "TheGuideToTheNewEden\TheGuideToTheNewEden.WPF\TheGuideToTheNewEden.WPF.csproj"
$DefaultPublishDir = Join-Path $RepoRoot "artifacts\publish-single"
$DefaultExePath = Join-Path $DefaultPublishDir "TheGuideToTheNewEden.exe"

if (-not (Test-Path -LiteralPath $ProjectPath)) {
    throw "找不到 WPF 工程：$ProjectPath（脚本应位于 <仓库根>\tools\packaging\）"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-ProjectVersion -ProjectPath $ProjectPath
}
if ([string]::IsNullOrWhiteSpace($Tag)) {
    $Tag = "v$Version"
}

if ($MaxPartMB -lt 10 -or $MaxPartMB -gt 99) {
    throw "MaxPartMB 必须在 10..99 之间（Gitee 单附件上限 100 MB）。"
}

# ---------------------------------------------------------------- 发布
$published = $false
if ([string]::IsNullOrWhiteSpace($ExePath) -and -not $SkipPublish) {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        throw "找不到 dotnet，无法执行发布。装上 .NET SDK 或用 -ExePath 指定已有 exe。"
    }

    Write-Host "== 发布 =="
    Write-Host "  版本：$Version"
    Write-Host "  工程：$ProjectPath"
    Write-Host "  配置：$PublishProfile"
    Write-Host "  输出：$DefaultPublishDir"

    if (Test-Path -LiteralPath $DefaultPublishDir) {
        Remove-Item -LiteralPath $DefaultPublishDir -Recurse -Force
    }

    $publishDirArg = $DefaultPublishDir.TrimEnd('\') + '\'
    & dotnet publish $ProjectPath `
        -c Release `
        "-p:PublishProfile=$PublishProfile" `
        "-p:Version=$Version" `
        "-p:PublishDir=$publishDirArg"
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish 失败（exit $LASTEXITCODE）。若程序正在运行，请先退出再重试。"
    }

    $ExePath = $DefaultExePath
    $published = $true
}
elseif ([string]::IsNullOrWhiteSpace($ExePath)) {
    $ExePath = $DefaultExePath
}

if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "找不到 exe：$ExePath。先执行发布，或去掉 -SkipPublish。"
}
$ExePath = (Resolve-Path -LiteralPath $ExePath).Path
$exeInfo = Get-Item -LiteralPath $ExePath

$productVersion = ($exeInfo.VersionInfo.ProductVersion -split '\+')[0]
if (-not [string]::IsNullOrWhiteSpace($productVersion) -and $productVersion -ne $Version) {
    Write-Warning "exe 的 ProductVersion=$productVersion，与清单 Version=$Version 不一致；更新后应用可能仍认为自己不是新版本。"
}

if ($published) {
    Write-Host "  发布完成：$ExePath"
}
else {
    Write-Host "== 使用已有 exe（未重新发布）=="
    Write-Host "  $ExePath"
}

# ---------------------------------------------------------------- 输出目录与清单参数
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $RepoRoot "update-package"
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$OutputDir = (Resolve-Path -LiteralPath $OutputDir).Path

if ([string]::IsNullOrWhiteSpace($GiteeBaseUrl)) {
    $GiteeBaseUrl = "https://gitee.com/$GiteeRepo/releases/download/$Tag"
}
$giteeBase = $GiteeBaseUrl.TrimEnd('/')

$size = $exeInfo.Length
$sha = (Get-FileHash -LiteralPath $ExePath -Algorithm SHA256).Hash.ToLowerInvariant()

if ([string]::IsNullOrWhiteSpace($FullName)) {
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($exeInfo.Name)
    $baseName = $baseName -replace '[_\-\.]v?\d+(\.\d+){1,3}([-\.][0-9A-Za-z]+)*$', ''
    $safeTag = $Tag -replace '[\\/:*?"<>|]', '_'
    $FullName = "${baseName}_${safeTag}.exe"
}
else {
    $FullName = [System.IO.Path]::GetFileName($FullName)
}

if (-not $FullName.EndsWith('.exe', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "FullName 必须以 .exe 结尾：$FullName"
}

if (-not [string]::IsNullOrWhiteSpace($NotesFile)) {
    # 发布说明按 UTF-8 读；PowerShell 5.1 默认按 ANSI，会把中文说明读成乱码。
    $Notes = Get-Content -LiteralPath $NotesFile -Raw -Encoding UTF8
}

$fullPath = Join-Path $OutputDir $FullName
$githubUrl = "https://github.com/$GitHubRepo/releases/download/$Tag/$FullName"

Write-Host "== 生成发布物 =="
Write-Host "  Tag：$Tag"
Write-Host "  输出目录：$OutputDir"
Write-Host "  Gitee 前缀：$giteeBase"

# 清掉上一次同名产物（完整包 + 分片），避免旧分片残留；其它版本的文件不动。
Get-ChildItem -LiteralPath $OutputDir -Filter "$FullName.*" -File -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue
if (Test-Path -LiteralPath $fullPath) {
    Remove-Item -LiteralPath $fullPath -Force
}

Copy-Item -LiteralPath $ExePath -Destination $fullPath -Force
$fullInfo = Get-Item -LiteralPath $fullPath
if ($fullInfo.Length -ne $size) {
    throw "复制完整 exe 后大小不一致：$($fullInfo.Length) != $size"
}

$sources = @()
$maxPartBytes = [int64]$MaxPartMB * 1MB
$partCount = [Math]::Max(1, [int][math]::Ceiling($size / $maxPartBytes))

Write-Host "  完整 exe（GitHub）：$fullPath  $([math]::Round($size / 1MB, 1)) MB"
Write-Host "  生成 $partCount 个 Gitee 分片（每片 ≤ $MaxPartMB MB）："

$partEntries = @()
$buffer = New-Object byte[] (1MB)
$input = [System.IO.File]::OpenRead($fullPath)
try {
    for ($i = 0; $i -lt $partCount; $i++) {
        $partName = "{0}.{1:D3}" -f $FullName, ($i + 1)
        $partPath = Join-Path $OutputDir $partName
        $remaining = [math]::Min($maxPartBytes, $size - ($i * $maxPartBytes))

        $output = [System.IO.File]::Create($partPath)
        try {
            $left = [int64]$remaining
            while ($left -gt 0) {
                $want = [int][math]::Min($buffer.Length, $left)
                $read = $input.Read($buffer, 0, $want)
                if ($read -le 0) { throw "读取 exe 提前结束。" }
                $output.Write($buffer, 0, $read)
                $left -= $read
            }
        }
        finally {
            $output.Dispose()
        }

        $partInfo = Get-Item -LiteralPath $partPath
        $partSha = (Get-FileHash -LiteralPath $partPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $partEntries += [ordered]@{
            name   = $partName
            url    = "$giteeBase/$partName"
            size   = $partInfo.Length
            sha256 = $partSha
        }
        Write-Host ("    {0}  {1,8:N1} MB  {2}" -f $partName, ($partInfo.Length / 1MB), $partSha)
    }
}
finally {
    $input.Dispose()
}

$sources += [ordered]@{
    name  = "Gitee"
    parts = $partEntries
}
$sources += [ordered]@{
    name   = "GitHub"
    url    = $githubUrl
    size   = $size
    sha256 = $sha
}

$manifest = [ordered]@{
    version     = $Version
    tag         = $Tag
    file        = $FullName
    size        = $size
    sha256      = $sha
    publishedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    notes       = $Notes
    sources     = $sources
}

$manifestPath = Join-Path $OutputDir "update.json"
$manifestJson = $manifest | ConvertTo-Json -Depth 8
# 清单必须是 UTF-8 无 BOM：System.Text.Json 对字符串开头的 BOM 会报 "0xEF invalid start of a value"。
[System.IO.File]::WriteAllText($manifestPath, $manifestJson, (New-Object System.Text.UTF8Encoding($false)))

Write-Host ""
Write-Host "== 完成 =="
Write-Host "  完整 exe（GitHub）：$fullPath"
Write-Host "  Gitee 分片：$OutputDir\$FullName.001 ..."
Write-Host "  清单：$manifestPath"
Write-Host "  整包 SHA-256：$sha"
Write-Host ""
Write-Host "下一步："
Write-Host "  1) GitHub Release：上传完整 exe（$FullName）+ update.json。"
Write-Host "  2) Gitee 发行版：上传分片（每个 ≤100 MB）+ 把 update.json 提交到仓库根目录。"
Write-Host "  3) 核对清单里的 Gitee 地址与页面上的附件下载地址一致；不一致时改 -GiteeBaseUrl 重跑。"
