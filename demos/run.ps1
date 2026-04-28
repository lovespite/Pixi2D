[CmdletBinding()]
param(
    [Parameter(Position = 0)] [string] $Name,
    [switch] $List,
    [switch] $NoBuild,
    [switch] $NoWatch,
    [string] $Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSCommandPath
$repo = Split-Path -Parent $root
$exe  = Join-Path $repo "Pixi2D.Host\bin\$Configuration\net10.0-windows\win-x64\Pixi2D.Host.exe"

function Get-Demos {
    Get-ChildItem -Path $root -Directory |
        Where-Object { $_.Name -match '^\d{2}-' } |
        Sort-Object Name |
        ForEach-Object {
            $readme = Join-Path $_.FullName "README.md"
            $desc = ""
            if (Test-Path $readme) {
                $line = (Get-Content $readme -TotalCount 4 -ErrorAction SilentlyContinue) |
                    Where-Object { $_ -and -not ($_ -match '^#') } |
                    Select-Object -First 1
                if ($line) { $desc = $line.Trim() }
            }
            $pxml = Join-Path $_.FullName "main.pxml"
            $hasJs = Test-Path (Join-Path $_.FullName "main.js")
            [pscustomobject]@{
                Name    = $_.Name
                Pxml    = $pxml
                HasJs   = $hasJs
                Desc    = $desc
                Exists  = Test-Path $pxml
            }
        }
}

function Show-List {
    $demos = Get-Demos
    "{0,-26} {1,-3} {2}" -f "Demo", "JS", "Description"
    "{0,-26} {1,-3} {2}" -f ("-" * 26), "---", ("-" * 50)
    foreach ($d in $demos) {
        $js = if ($d.HasJs) { "✓" } else { "" }
        "{0,-26} {1,-3} {2}" -f $d.Name, $js, $d.Desc
    }
    ""
    "Usage: .\run.ps1 -Name <demo>     (e.g. counter, 02, 02-counter)"
}

if (-not $Name -or $List) { Show-List; return }

$demos = Get-Demos
$match = $demos | Where-Object { $_.Name -eq $Name -or $_.Name -like "$Name-*" -or $_.Name -like "*-$Name" -or $_.Name -like "*$Name*" }
if (-not $match) { Write-Error "No demo matches '$Name'. Run .\run.ps1 -List."; return }
if ($match.Count -gt 1) {
    Write-Host "Multiple matches:"; $match | ForEach-Object { "  $($_.Name)" }; return
}
$demo = $match[0]
if (-not $demo.Exists) { Write-Error "Missing main.pxml for $($demo.Name)"; return }

if (-not (Test-Path $exe) -and -not $NoBuild) {
    Write-Host "Building Pixi2D.Host ($Configuration)..."
    dotnet build (Join-Path $repo "Pixi2D.Host\Pixi2D.Host.csproj") -c $Configuration --nologo | Out-Host
}
if (-not (Test-Path $exe)) { Write-Error "Pixi2D.Host.exe not found at $exe"; return }

$cliArgs = @($demo.Pxml)
if (-not $NoWatch) { $cliArgs += "--watch" }
Write-Host "Launching $($demo.Name) → $exe $($cliArgs -join ' ')"
& $exe @cliArgs
