[CmdletBinding()]
param(
    [Parameter(Position = 0)] [string] $Target,
    [switch] $NoBuild,
    [switch] $NoWatch,
    [string] $Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSCommandPath
$repo = Split-Path -Parent (Split-Path -Parent $root)
$exe  = Join-Path $repo "Pixi2D.Host\bin\$Configuration\net10.0-windows\win-x64\Pixi2D.Host.exe"
$pxml = Join-Path $root "main.pxml"

if (-not (Test-Path $exe) -and -not $NoBuild) {
    Write-Host "Building Pixi2D.Host ($Configuration)..."
    dotnet build (Join-Path $repo "Pixi2D.Host\Pixi2D.Host.csproj") -c $Configuration --nologo | Out-Host
}
if (-not (Test-Path $exe)) { Write-Error "Pixi2D.Host.exe not found at $exe"; return }

$cliArgs = @($pxml)
if (-not $NoWatch) { $cliArgs += "--watch" }
$cliArgs += "--width"; $cliArgs += "1280"
$cliArgs += "--height"; $cliArgs += "800"
$cliArgs += "--title"; $cliArgs += "Pixi2D Preview"

if ($Target) {
    $resolved = Resolve-Path $Target -ErrorAction Stop
    $cliArgs += $resolved.Path
}

Write-Host "Launching Preview → $exe $($cliArgs -join ' ')"
& $exe @cliArgs
