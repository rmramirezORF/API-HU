# ============================================================
# Script para generar el publish listo para IIS
# ============================================================
# Uso: .\publish.ps1
# ============================================================

$ErrorActionPreference = "Stop"

$projectDir = Join-Path $PSScriptRoot "src\APIHU"
$publishDir = Join-Path $PSScriptRoot "publish"

Write-Host "Limpiando publish anterior..." -ForegroundColor Yellow
if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}

Write-Host "Generando publish (Release)..." -ForegroundColor Yellow
dotnet publish $projectDir -c Release -o $publishDir --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: dotnet publish falló" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Publish completado en: $publishDir" -ForegroundColor Green
Write-Host ""
Write-Host "Tamaño total:" -ForegroundColor Cyan
$size = (Get-ChildItem $publishDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host ("  {0:N2} MB" -f $size)
Write-Host ""
Write-Host "Próximo paso:" -ForegroundColor Cyan
Write-Host "  1. Edita $publishDir\web.config y rellena las API keys"
Write-Host "  2. Copia la carpeta al servidor IIS"
Write-Host "  3. Sigue docs\DEPLOY_IIS.md"
