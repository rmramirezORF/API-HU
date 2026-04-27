# Deploy de API-HU en Render (gratis)

Guía paso a paso para desplegar el backend en **Render** sin pagar. Tiempo estimado: **15 minutos**.

> **Por qué Render** y no otros: tiene la combinación más generosa de free tier para .NET (750 horas/mes, sin tarjeta), buen soporte de Docker, deploys automáticos desde GitHub e HTTPS gratis. Más abajo hay alternativas.

---

## ⚡ TL;DR (lectura rápida)

1. Cuenta gratis en [render.com](https://render.com) (con GitHub)
2. New → Web Service → conectas el repo `API-HU`
3. Render detecta el `Dockerfile` automáticamente
4. Añades 4 variables de entorno (las API keys + la chain de providers)
5. Deploy → en ~5 min tienes una URL pública tipo `https://api-hu.onrender.com`

---

## 📦 Lo que ya está listo en el repo

Antes de empezar, esto ya lo dejé preparado:

| Archivo | Función |
|---|---|
| `Dockerfile` | Multi-stage build para .NET 10 |
| `.dockerignore` | Excluye `bin/`, `obj/`, `.env`, `docs/`, etc. |
| `Program.cs` | Detecta si hay BD; si no, usa InMemory automáticamente |
| `APIHU.csproj` | Tiene `Microsoft.EntityFrameworkCore.InMemory` para el fallback |

No tienes que tocar código. Solo seguir la guía.

---

## 🚀 Despliegue paso a paso en Render

### Paso 1 — Crear cuenta

1. Ve a **https://render.com**
2. Click **"Get Started"** → **"GitHub"** para registrarte con tu cuenta de GitHub
3. Autoriza a Render a leer tus repos públicos

### Paso 2 — Crear el Web Service

1. En el dashboard de Render, click **"New +"** → **"Web Service"**
2. Te muestra una lista de tus repos. Selecciona **`rmramirezORF/API-HU`**
   - Si no aparece, click **"Configure GitHub App"** y dale acceso al repo
3. Click **"Connect"**

### Paso 3 — Configurar el servicio

Render te pide rellenar un formulario. Pon estos valores exactos:

| Campo | Valor |
|---|---|
| **Name** | `api-hu` (o el que quieras) |
| **Region** | `Oregon (US West)` (más rápido para Latam) |
| **Branch** | `main` |
| **Root Directory** | (déjalo vacío) |
| **Runtime / Language** | `Docker` (lo detecta solo si dejas en "Auto") |
| **Dockerfile Path** | `./Dockerfile` (default) |
| **Plan** | **Free** |

> ⚠️ **NO** elijas el plan Standard ni Starter, marca explícitamente **Free**.

### Paso 4 — Variables de entorno (la parte crítica)

Antes de hacer click en "Create Web Service", baja a la sección **"Environment Variables"** y añade:

| Key | Value | Notas |
|---|---|---|
| `AI__ProviderChain` | `gemini,groq,openrouter` | Orden de fallback |
| `GEMINI_API_KEY` | `AIza...` | La tuya |
| `GROQ_API_KEY` | `gsk_...` | La tuya |
| `OPENROUTER_API_KEY` | `sk-or-v1-...` | La tuya |

> **Importante**: pega cada key SIN comillas. Render las maneja como strings automáticamente.

> Lo de `ConnectionStrings__DefaultConnection` **NO LO PONGAS**. Eso activa el modo InMemory que es justo lo que queremos para deploy gratis.

### Paso 5 — Health check (opcional pero recomendado)

Si Render lo permite en tu plan free, configura:

- **Health Check Path**: `/api/hu/health`

Esto le dice a Render que verifique que el servicio responde antes de marcarlo como vivo.

### Paso 6 — Deploy

1. Click **"Create Web Service"**
2. Render empezará a:
   - Clonar el repo
   - Construir la imagen Docker (`dotnet restore` + `dotnet publish`)
   - Lanzar el contenedor
3. Mira los logs en vivo. Verás algo como:

```
[build] Determining projects to restore...
[build] Restored APIHU.csproj (in 3.4s)
[build] APIHU -> /app/publish/APIHU.dll
[build] ==> Build successful 🎉
[deploy] Starting service...
>> BD: en memoria (sin persistencia)
>> AI Provider Chain solicitado: [gemini → groq → openrouter]
  [ok]   gemini          (gemini-2.5-flash)
  [ok]   groq            (llama-3.3-70b-versatile)
  [ok]   openrouter      (google/gemma-3-27b-it:free, +8 fallbacks internos)
[deploy] ==> Your service is live 🎉
```

4. Al final, Render te da una URL pública: `https://api-hu-xxxx.onrender.com`

### Paso 7 — Probar el deploy

Reemplaza `api-hu-xxxx` por tu URL real:

```bash
# 1. Health check
curl https://api-hu-xxxx.onrender.com/api/hu/health

# Respuesta esperada:
# {"status":"Healthy","proveedorIA":"MultiProvider[Gemini,Groq,OpenRouter]","modelo":"gemini-2.5-flash"}

# 2. Generar HUs
curl -X POST "https://api-hu-xxxx.onrender.com/api/hu/generate-from-text?proyecto=Test" \
  -H "Content-Type: text/plain" \
  --data "Necesito un sistema para gestionar tareas con prioridades y fechas límite."
```

También puedes abrir Swagger en `https://api-hu-xxxx.onrender.com/swagger`.

---

## ⚠️ Limitaciones del free tier de Render

| Limitación | Qué significa |
|---|---|
| **Sleep tras 15 min de inactividad** | Si nadie llama a la API en 15 min, el contenedor se duerme. La siguiente llamada tarda **~30s extras** en arrancar (cold start). |
| **750 horas/mes** | Casi 31 días continuos. Si tu servicio está siempre activo, está cubierto. |
| **512 MB RAM** | .NET arranca con ~150 MB. Tu app llega a ~300-400 MB. **Justo dentro del límite.** |
| **0.1 CPU compartida** | Lento bajo carga. Para uso individual o demos: bien. Para producción real: insuficiente. |
| **Sin BD persistente** | Por eso usamos InMemory. Si reinicias el servicio, se pierden las generaciones guardadas. |

### Cómo evitar el cold start (truco)

Configura un **uptime monitor gratuito** que haga un GET a `/api/hu/health` cada 10 minutos:

- **UptimeRobot** ([uptimerobot.com](https://uptimerobot.com)) — 50 monitors gratis, intervalo mínimo 5 min
- **Cron-job.org** ([cron-job.org](https://cron-job.org)) — gratis ilimitado

Esto mantiene el servicio "caliente" y evita el cold start de 30s.

---

## 🔧 Cambios en el código que se hicieron para esto

Por si te interesa entender qué se modificó:

### `Program.cs` — BD opcional
Antes: si no había `ConnectionStrings:DefaultConnection`, el app **lanzaba excepción** al arrancar.

Ahora: si no hay connection string, usa `Microsoft.EntityFrameworkCore.InMemory` para satisfacer el contrato de DI sin necesitar SQL Server. El endpoint `/api/hu/generate-from-text` no toca BD, así que funciona perfecto.

```csharp
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var bdSqlServerEnabled = !string.IsNullOrWhiteSpace(connectionString);

builder.Services.AddDbContext<APIHUDbContext>(options =>
{
    if (bdSqlServerEnabled)
        options.UseSqlServer(connectionString!, ...);
    else
        options.UseInMemoryDatabase("APIHU-Memory");
});
```

### `Dockerfile` — multi-stage build
Stage 1 usa `mcr.microsoft.com/dotnet/sdk:10.0` para compilar.
Stage 2 usa `mcr.microsoft.com/dotnet/aspnet:10.0` (sin SDK, mucho más liviano) para correr.

Imagen final: ~250 MB. Cabe en cualquier free tier.

---

## 🔄 Re-deploys automáticos

Render hace **deploy automático** cada vez que pushees a `main` en GitHub. Sin que hagas nada más.

Si quieres apagar esto:
- Service → Settings → Build & Deploy → desactiva "Auto-Deploy"

---

## 🆘 Troubleshooting

| Síntoma | Causa probable | Solución |
|---|---|---|
| Build falla con `Couldn't find image dotnet/sdk:10.0` | .NET 10 no listado en Docker Hub para tu arquitectura | Cambia a `mcr.microsoft.com/dotnet/sdk:9.0` y `aspnet:9.0` (downgrade el `<TargetFramework>` también) |
| Service crashea al arrancar con `OutOfMemoryException` | 512 MB RAM se queda corto | Reduce el `MaxTokens` en variables de entorno: `Gemini__MaxTokens=4096` |
| Health check siempre falla | Path mal escrito en config de Render | Pon exactamente `/api/hu/health` (sin trailing slash) |
| Errores 502/504 frecuentes | Cold start desde sleep | Configura UptimeRobot o paga $7/mes para tier Starter |
| Logs no muestran las HUs generadas | Render trunca logs grandes | Mira en Service → Logs y filtra por `INF` |

---

## 🌐 Alternativas a Render (si quieres comparar)

| Plataforma | Free tier | Pros | Contras |
|---|---|---|---|
| **Render** ⭐ | 750h/mes, sleep 15min | Más generoso, fácil GitHub | 512 MB RAM, sleep |
| **Fly.io** | 3 VMs 256 MB | Sin sleep, deploy en cualquier región | Necesitas tarjeta (no se cobra), CLI obligatorio |
| **Railway** | $5 crédito/mes | UI muy buena, sin sleep si pagas | Free tier limitadísimo desde 2024 |
| **Azure App Service F1** | 60 min CPU/día | Sin sleep, soporte .NET nativo | Solo 60 min CPU/día = inútil para uso real |
| **Koyeb** | 1 servicio | Sin sleep, HTTPS automático | Free tier muy básico, posibles cobros |

Si Render no te convence, el siguiente que recomiendo es **Fly.io**. Necesitarás:
1. `fly.toml` (lo genero con `flyctl launch`)
2. Tarjeta (no se cobra dentro del free tier)
3. Más comandos CLI

Si llegas ahí, dime y te ayudo con el deploy en Fly.

---

## 🎯 Después del deploy

Cuando tengas la URL pública funcionando, podrías:

1. **Conectar un frontend** — usa la URL en `VITE_API_BASE_URL` cuando construyas la UI
2. **Activar BD persistente** — si decides pagar:
   - Render PostgreSQL (free tier 90 días, luego $7/mes)
   - Cualquier SQL Server cloud (Azure SQL serverless, etc.)
   - Solo seteas `ConnectionStrings__DefaultConnection` y la API detecta automáticamente
3. **Restringir CORS** — actualmente permite todo origen. Para prod:
   ```csharp
   policy.WithOrigins("https://tu-frontend.com")
   ```

---

## 📞 Si algo se rompe

1. Mira los logs en Render → Service → "Logs"
2. Busca líneas con `[ERR]` o `Unhandled`
3. Las causas más comunes: API key mal pegada, Dockerfile build error, OOM (memoria)
