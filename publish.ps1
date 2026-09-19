<#
.SYNOPSIS
    Builds a clean copy of a module and, when asked, uploads it to the Steam Workshop.

.DESCRIPTION
    The csproj copies the built module into $(GameFolder)\Modules as part of every build,
    so this redirects that copy into .\publish and ships from there. What reaches the
    Workshop is then only what this script built, never whatever is in the live game
    folder from an afternoon of testing.

    It stages and checks by default. Uploading is the one thing it will not do unless
    you ask for it with -Upload.

.EXAMPLE
    .\publish.ps1
    Build and stage, then print the upload command without running it.

.EXAMPLE
    .\publish.ps1 -Upload
    Build, stage and upload an update (WorkshopUpdate.xml).

.EXAMPLE
    .\publish.ps1 -Upload -First
    Build, stage and create the Workshop item for the first time (WorkshopCreate.xml).
#>
[CmdletBinding()]
param(
    [string] $Module = 'ProgressionExpanded',

    # Actually send it to Steam. Without this the script only builds and checks.
    [switch] $Upload,

    # Use WorkshopCreate.xml instead of WorkshopUpdate.xml. First upload only:
    # running this twice creates two Workshop items.
    [switch] $First,

    [string] $GameFolder = 'V:\Games\Steam\steamapps\common\Mount & Blade II Bannerlord',
    [string] $WorkshopFolder = 'V:\Games\Steam\steamapps\workshop\content\261550'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$staging = Join-Path $root 'publish'
$moduleDir = Join-Path $staging "Modules\$Module"
$publishDir = Join-Path $root "$Module\_Publish"
$uploader = Join-Path $GameFolder 'bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.SteamWorkshop.exe'

# A stale staging folder is worse than no staging folder: a file removed from the
# module would keep shipping forever.
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }

Write-Host "Building $Module into $staging" -ForegroundColor Cyan
dotnet build (Join-Path $root "$Module\$Module.csproj") -c Release -p:Platform=x64 `
    -p:GameFolder=$staging -p:WorkshopFolder=$WorkshopFolder
if ($LASTEXITCODE -ne 0) { throw 'Build failed; nothing staged.' }

# What the game needs to load the module at all. If either is missing the upload
# would succeed and the mod would simply not appear in the launcher.
$subModule = Join-Path $moduleDir '_Module\SubModule.xml'
if (-not (Test-Path $subModule)) { $subModule = Join-Path $moduleDir 'SubModule.xml' }
$dll = Join-Path $moduleDir "bin\Win64_Shipping_Client\$Module.dll"

foreach ($required in @($subModule, $dll)) {
    if (-not (Test-Path $required)) { throw "Staged module is incomplete: $required is missing." }
}

$version = ([xml](Get-Content $subModule)).Module.Version.value
$name = ([xml](Get-Content $subModule)).Module.Name.value
Write-Host "Staged $name $version" -ForegroundColor Green
Get-ChildItem $moduleDir -Recurse -File |
    ForEach-Object { '  {0}' -f $_.FullName.Substring($moduleDir.Length + 1) }

$task = if ($First) { 'WorkshopCreate.xml' } else { 'WorkshopUpdate.xml' }
$taskFile = Join-Path $publishDir $task
if (-not (Test-Path $taskFile)) { throw "$taskFile not found." }

# The placeholder id is the one mistake that produces a confusing failure rather
# than an obvious one, so it is worth catching here.
if (-not $First) {
    $itemId = ([xml](Get-Content $taskFile)).Tasks.GetItem.ItemId.Value
    if ($itemId -eq '0000000000') {
        throw "$task still has the placeholder ItemId. Put the id from the Workshop URL in it first."
    }
}

$image = ([xml](Get-Content $taskFile)).Tasks.UpdateItem.Image.Value
if ($image -and -not (Test-Path $image)) {
    throw "Preview image not found: $image (Steam requires one, under 1 MB)."
}
if ($image -and (Get-Item $image).Length -gt 1MB) {
    throw "Preview image is over 1 MB: $image"
}

if (-not $Upload) {
    Write-Host ''
    Write-Host 'Staged only. To upload:' -ForegroundColor Yellow
    Write-Host "  & '$uploader' '$taskFile'"
    return
}

if (-not (Test-Path $uploader)) { throw "Workshop uploader not found at $uploader" }
if (-not (Get-Process -Name steam -ErrorAction SilentlyContinue)) {
    throw 'Steam is not running. The uploader signs in through it.'
}

Write-Host "Uploading with $task" -ForegroundColor Cyan
& $uploader $taskFile

# The uploader is known to sit printing "Status: k_EItemUpdateStatusInvalid 0/0"
# after a successful upload, so its exit is not a reliable verdict either way.
Write-Host ''
Write-Host 'Check the Workshop page before assuming either outcome.' -ForegroundColor Yellow
Write-Host 'Then rename or delete your local Modules\' -NoNewline
Write-Host "$Module" -NoNewline
Write-Host ' folder: with both present the game loads the Workshop copy.' -ForegroundColor Yellow
