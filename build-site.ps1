# Builds the market structure term library into artifacts/site.
# Usage: ./build-site.ps1 [-Open]
param([switch]$Open)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out = Join-Path $root 'artifacts/site'

dotnet run --project (Join-Path $root 'CryptoAnalysis.Site') -c Release -- (Join-Path $root 'CryptoAnalysis.Test/TestData/Terms') $out
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($Open) { Start-Process (Join-Path $out 'index.html') }
