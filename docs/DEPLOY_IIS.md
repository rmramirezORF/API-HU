# Deploy de API-HU en IIS (Windows Server)

Guía paso a paso para desplegar el backend en un servidor IIS, manteniendo Swagger funcional.

---

## 📋 Pre-requisitos en el servidor IIS

Antes de copiar nada al servidor, instala lo siguiente **en el servidor**:

### 1. IIS habilitado
Server Manager → Add Roles and Features → **Web Server (IIS)**.

### 2. ASP.NET Core 9 Hosting Bundle
**Crítico**. IIS no sabe ejecutar apps .NET sin esto.

- Descarga: https://dotnet.microsoft.com/download/dotnet/9.0
- Sección "Hosting Bundle" (NO "SDK", NO "Runtime" — el **Hosting Bundle** específicamente)
- Instalación: doble click → siguiente → siguiente → reiniciar IIS:
  ```powershell
  net stop was /y
  net start w3svc
  ```

> ⚠️ **Aviso de soporte**: el proyecto está en .NET 9 (STS). Microsoft termina soporte de seguridad el **12 de mayo de 2026**. La app seguirá funcionando, pero ya no recibirá parches de seguridad. Se recomienda planear migración a .NET 10 LTS (soporte hasta noviembre 2028) cuando sea posible instalar el Hosting Bundle 10 en el servidor.

### 3. Verificar instalación
```powershell
dotnet --list-runtimes
```
Debe listar `Microsoft.AspNetCore.App 9.x` y `Microsoft.NETCore.App 9.x`.

Si no aparece, el Hosting Bundle no se instaló correctamente.

---

## 📦 Generar el publish localmente

Desde tu equipo (donde está el código):

```bash
cd C:\Users\rmramirez\Desktop\API-HU\src\APIHU
dotnet publish -c Release -o C:\Users\rmramirez\Desktop\API-HU\publish
```

Eso genera la carpeta `publish/` con todo lo necesario (~3.6 MB).

**Estructura esperada del publish:**
```
publish/
├── APIHU.dll                 ← La app
├── APIHU.exe                 ← Wrapper (no se usa en IIS, IIS llama a APIHU.dll)
├── APIHU.deps.json
├── APIHU.runtimeconfig.json
├── appsettings.json
├── web.config                ← Configuración para IIS (auto-generado)
├── Prompts/                  ← Prompts v1 y v2 (¡no borrar!)
└── *.dll                     ← Dependencias (Serilog, Swashbuckle, etc.)
```

---

## 🚀 Deploy paso a paso

### 1. Copia el publish al servidor
Comprime la carpeta `publish/` y cópiala al servidor (RDP, copia de red, FTP, lo que tengas).

Descomprime, por ejemplo, en:
```
C:\inetpub\wwwroot\API-HU
```

### 2. Edita las API keys en el `web.config`
Abre `C:\inetpub\wwwroot\API-HU\web.config` con un editor de texto y reemplaza los placeholders:

```xml
<environmentVariable name="GEMINI_API_KEY" value="AIzaSy..." />
<environmentVariable name="GROQ_API_KEY" value="gsk_..." />
<environmentVariable name="OPENROUTER_API_KEY" value="sk-or-v1-..." />
```

> 🔒 **Alternativa más segura**: en vez de poner las keys en `web.config`, configúralas en IIS Manager → tu sitio → Configuration Editor → `system.webServer/aspNetCore/environmentVariables`. O en el Application Pool → Advanced Settings → Environment Variables.

### 3. Crea el Application Pool
1. Abre IIS Manager
2. Application Pools → **Add Application Pool**
3. Configuración:
   - **Name**: `API-HU-Pool`
   - **.NET CLR version**: **"No Managed Code"** ⚠️ (importante — ASP.NET Core no usa el CLR de IIS)
   - **Managed pipeline mode**: Integrated
4. OK
5. Click derecho en `API-HU-Pool` → **Advanced Settings**
6. **Identity**: ApplicationPoolIdentity (default está bien)
7. **Start Mode**: AlwaysRunning (recomendado, evita el cold start)

### 4. Crea el sitio web
1. IIS Manager → Sites → **Add Website**
2. Configuración:
   - **Site name**: `API-HU`
   - **Application pool**: `API-HU-Pool`
   - **Physical path**: `C:\inetpub\wwwroot\API-HU`
   - **Binding**:
     - Type: `http` (o `https` si tienes certificado)
     - Port: `5000` (o el que prefieras, 80 si es el único sitio)
     - Host name: vacío o tu dominio
3. OK

### 5. Da permisos a la carpeta
La identidad del Application Pool necesita leer y escribir (logs):

```powershell
icacls "C:\inetpub\wwwroot\API-HU" /grant "IIS AppPool\API-HU-Pool:(OI)(CI)RX"
icacls "C:\inetpub\wwwroot\API-HU\logs" /grant "IIS AppPool\API-HU-Pool:(OI)(CI)F" /T
```

Si no existe la carpeta `logs/`, créala primero:
```powershell
mkdir "C:\inetpub\wwwroot\API-HU\logs"
```

### 6. Probar
- Browser: `http://servidor:5000/api/hu/health`
- Debería responder JSON con `"status": "Healthy"`
- Browser: `http://servidor:5000/swagger`
- Debería abrir la UI de Swagger

---

## 🔍 Si algo falla

### "HTTP Error 500.30 - ASP.NET Core app failed to start"
- Mira `C:\inetpub\wwwroot\API-HU\logs\stdout_*.log`
- Causas típicas:
  - Faltan API keys en `web.config`
  - Hosting Bundle no instalado
  - El Application Pool tiene CLR distinto a "No Managed Code"

### "HTTP Error 502.5 - Process Failure"
- Falta el Hosting Bundle o no se reinició IIS después de instalarlo:
  ```powershell
  net stop was /y && net start w3svc
  ```

### Swagger 404
- Verifica que la URL es `/swagger` (no `/swagger/index.html` directo, aunque también funciona)
- Si tu IIS tiene la app bajo un virtual path (ej: `/api-hu`), prueba `/api-hu/swagger`

### Cold start lento
Si la primera request tras estar inactivo tarda mucho:
1. Application Pool → Advanced Settings → **Start Mode = AlwaysRunning**
2. Application Pool → Advanced Settings → **Idle Time-out = 0** (no se duerme nunca)
3. Sites → tu sitio → Advanced Settings → **Preload Enabled = True**

### Logs de la app
Los logs estructurados de Serilog van a `logs/apihu-YYYYMMDD.log` (configurado en `appsettings.json`).
Los logs de stdout/stderr de IIS van a `logs/stdout_*.log`.

---

## ✅ Checklist post-deploy

- [ ] `http://servidor/api/hu/health` responde 200 con JSON
- [ ] `http://servidor/swagger` muestra la UI con los endpoints
- [ ] `POST /api/hu/generate-from-text` con body de prueba devuelve HUs
- [ ] Logs en `logs/` se están escribiendo
- [ ] Application Pool sigue corriendo después de 30 minutos sin tráfico

---

## 🔄 Para actualizar el deploy más adelante

Cuando hagas cambios al código:

1. En tu equipo: `dotnet publish -c Release -o publish`
2. **Detén el sitio** en IIS (sino, los .dll están en uso y no se pueden sobrescribir):
   - IIS Manager → Sites → API-HU → Stop
3. Copia el contenido nuevo de `publish/` sobre `C:\inetpub\wwwroot\API-HU\`
   - **NO borres `web.config`** (tiene tus keys editadas)
   - Si quieres mantenerlo: copia todo excepto `web.config` o cópialo al final
4. **Inicia el sitio** otra vez

> 💡 Tip pro: usa un script de deploy para automatizar esto. Ejemplo en PowerShell:
> ```powershell
> $publish = "C:\Users\rmramirez\Desktop\API-HU\publish"
> $iis = "C:\inetpub\wwwroot\API-HU"
> Stop-Website -Name "API-HU"
> robocopy $publish $iis /MIR /XF web.config
> Start-Website -Name "API-HU"
> ```

---

## 📊 Diferencias con el deploy en Render

| Aspecto | Render (gratis) | IIS (servidor propio) |
|---|---|---|
| Cold start | Sí, 50s tras 15 min inactivo | No (con AlwaysRunning) |
| Custom domain | Subdominio gratis | Tu dominio o IP |
| HTTPS | Auto | Necesitas certificado |
| CI/CD auto | Push a git → redeploy | Manual o con script |
| Costo | $0 | Servidor propio |
| Control | Limitado | Total |

Si tu equipo ya tiene un IIS corriendo, el deploy ahí es la opción más rápida y barata para uso interno.
