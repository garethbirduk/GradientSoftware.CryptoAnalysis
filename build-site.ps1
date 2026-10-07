# Builds the market structure term library into artifacts/site.
# Usage: ./build-site.ps1 [-Open] [-Serve] [-Port 5178]
#   -Open   opens the static page in the browser.
#   -Serve  then serves the site on http://localhost:<Port>, with replay for the full history (Ctrl+C to stop).
param([switch]$Open, [switch]$Serve, [int]$Port = 5178)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out = Join-Path $root 'artifacts/site'

if ($Open -and -not $Serve) { $openAfter = $true }
$serveArgs = if ($Serve) { @('--serve', "--port=$Port") } else { @() }

if ($Serve) { Write-Host "When the build finishes, open http://localhost:$Port" }
dotnet run --project (Join-Path $root 'CryptoAnalysis.Site') -c Release -- (Join-Path $root 'CryptoAnalysis.Test/TestData/Terms') $out @serveArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($openAfter) { Start-Process (Join-Path $out 'index.html') }
