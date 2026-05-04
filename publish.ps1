# ============================================================
# Script para generar el publish listo para IIS (.NET 9)
# ============================================================
# Uso: .\publish.ps1
#
# Genera la carpeta publish/ con todo lo necesario para deployar
# en IIS, e inyecta una plantilla de variables de entorno en
# web.config (que dotnet publish regenera vacía cada vez).
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
Write-Host "Inyectando plantilla de variables de entorno en web.config..." -ForegroundColor Yellow

$webConfigPath = Join-Path $publishDir "web.config"
$webConfigContent = @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\APIHU.dll"
                  stdoutLogEnabled="true"
                  stdoutLogFile=".\logs\stdout"
                  hostingModel="inprocess">
        <environmentVariables>
          <!-- ============================================================ -->
          <!-- IMPORTANTE: rellena las API keys antes de iniciar el sitio.   -->
          <!-- Alternativa: configúralas en IIS Manager → Application Pool   -->
          <!-- → Advanced Settings → Environment Variables                   -->
          <!-- ============================================================ -->

          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />

          <!-- Cadena de proveedores (orden de fallback) -->
          <environmentVariable name="AI__ProviderChain" value="gemini,groq,openrouter" />

          <!-- API keys: REEMPLAZAR antes de iniciar el sitio -->
          <environmentVariable name="GEMINI_API_KEY" value="REEMPLAZAR_AQUI" />
          <environmentVariable name="GROQ_API_KEY" value="REEMPLAZAR_AQUI" />
          <environmentVariable name="OPENROUTER_API_KEY" value="REEMPLAZAR_AQUI" />

          <!-- Modelos (opcional, puede dejarse para usar defaults de appsettings.json) -->
          <environmentVariable name="Gemini__Modelo" value="gemini-2.5-flash" />
          <environmentVariable name="Groq__Modelo" value="llama-3.3-70b-versatile" />
          <environmentVariable name="OpenRouter__Modelo" value="google/gemma-3-27b-it:free" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
'@

[System.IO.File]::WriteAllText($webConfigPath, $webConfigContent, [System.Text.Encoding]::UTF8)

Write-Host ""
Write-Host "Publish completado en: $publishDir" -ForegroundColor Green
Write-Host ""
Write-Host "Tamano total:" -ForegroundColor Cyan
$size = (Get-ChildItem $publishDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host ("  {0:N2} MB" -f $size)
Write-Host ""
Write-Host "Proximo paso:" -ForegroundColor Cyan
Write-Host "  1. Edita $webConfigPath y rellena las API keys"
Write-Host "  2. Copia la carpeta al servidor IIS (D:\Inetpub\wwwroot\API-HU)"
Write-Host "  3. Servidor necesita .NET 9 Hosting Bundle instalado"
Write-Host "  4. Sigue docs\DEPLOY_IIS.md"
