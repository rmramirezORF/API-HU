# API-HU — Generación de Historias de Usuario con Claude

API REST en **ASP.NET Core (.NET 10)** que convierte texto crudo (transcripciones de Teams, notas de reunión, requerimientos) en **Historias de Usuario estructuradas** con criterios de aceptación y tareas técnicas, usando **Anthropic Claude**.

---

## 🏗️ Arquitectura (Clean Architecture)

```
src/APIHU/
├── API/Controllers/          # Endpoints REST
├── Application/              # Casos de uso
│   ├── DTOs/                 # Contratos request/response
│   ├── Interfaces/           # Contratos de servicios
│   └── Services/             # Orchestrator, Validator, PromptService
├── Domain/                   # Entidades + interfaces puras
│   ├── Entities/
│   └── Interfaces/
├── Infrastructure/           # Adaptadores externos
│   ├── AI/                   # AnthropicProviderService + Prompts/
│   ├── BackgroundServices/
│   ├── Logging/              # Serilog
│   ├── Middleware/           # CorrelationId, RateLimiting, ApiKey
│   └── Persistence/          # EF Core + SQL Server
├── Migrations/               # EF Core migrations
├── Program.cs
├── appsettings.json          # Config pública (sin secretos)
└── appsettings.Example.json  # Plantilla
```

---

## 🔄 Pipeline (3 etapas)

```
Texto crudo
    ↓
┌─────────────────┐
│ 1. LIMPIEZA     │  → Elimina fillers, repeticiones, ruido
└────────┬────────┘
         ↓
┌─────────────────┐
│ 2. ESTRUCTURACIÓN │  → Identifica y categoriza requerimientos
└────────┬────────┘
         ↓
┌─────────────────┐
│ 3. GENERACIÓN HU│  → Crea HUs con criterios y tareas técnicas
└────────┬────────┘
         ↓
    VALIDACIÓN → Response JSON
```

Cada etapa usa un prompt versionado en `Infrastructure/AI/Prompts/v{n}/`.

---

## 🤖 Proveedor de IA: Anthropic Claude

Implementado en [AnthropicProviderService.cs](src/APIHU/Infrastructure/AI/AnthropicProviderService.cs).

**Modelos soportados** (configurable en `.env` o `appsettings.json`):

| Modelo | Uso recomendado |
|---|---|
| `claude-sonnet-4-6` | **Por defecto** — balance calidad/velocidad/coste |
| `claude-opus-4-6` | Máxima calidad, más caro |
| `claude-haiku-4-5-20251001` | Más rápido y barato |

**Robustez incluida:**
- Reintentos con **exponential backoff + jitter**
- Respeto al header `Retry-After`
- Manejo específico de `429`/`529`/`5xx` (transitorios) vs `400`/`401`/`404` (errores definitivos)
- **Tracking real de tokens** (input/output) persistido en BD
- Timeout configurable por request

La interfaz `IAIProviderService` está desacoplada. Para cambiar de proveedor basta con añadir una nueva implementación y registrarla en `Program.cs`.

---

## 🛡️ Middlewares de producción

| Middleware | Función |
|---|---|
| `CorrelationIdMiddleware` | ID único por request para trazabilidad |
| `RateLimitingMiddleware` | Límite de requests por IP (configurable) |
| `ApiKeyMiddleware` | Valida header `X-API-Key` (opcional) |

---

## 📡 Endpoints

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/hu/generate` | Genera HUs (sin guardar) |
| POST | `/api/hu/generate-and-save` | Genera y persiste en BD |
| GET | `/api/hu/prompts/versions` | Lista versiones de prompts |
| GET | `/api/hu/health` | Health check (incluye modelo activo) |

Todas las respuestas incluyen `X-Correlation-ID` para trazabilidad.

---

## ⚙️ Configuración

### Variables de entorno (`.env`)

Copia `.env.example` a `.env` y rellena:

```env
ANTHROPIC_API_KEY=sk-ant-...
Anthropic__Modelo=claude-sonnet-4-6
ConnectionStrings__DefaultConnection=Server=localhost,1433;Database=APIHU;...
PORT=5000
```

El archivo `.env` está en `.gitignore` — nunca se sube al repo.

### Precedencia de configuración

```
.env  >  Variables de entorno del SO  >  appsettings.json
```

El convenio estándar de ASP.NET Core aplica: `Seccion__Clave` en env var sobrescribe `Seccion:Clave` en appsettings.

---

## 🚀 Ejecución

```bash
cd src/APIHU
dotnet restore
dotnet run
```

- Swagger: `http://localhost:5000/swagger`
- Health:  `http://localhost:5000/api/hu/health`

---

## 🧪 Ejemplo de request

```http
POST /api/hu/generate
Content-Type: application/json

{
  "texto": "Reunión 15/04: necesitamos un sistema para gestionar inventario. El usuario admin debe poder registrar productos, ver stock y recibir alertas cuando algo baje del mínimo. También hay que exportar reportes.",
  "proyecto": "Inventario v2",
  "maximoHUs": 5,
  "idioma": "es",
  "versionPrompt": "v1"
}
```

**Response:**

```json
{
  "exitoso": true,
  "mensaje": "Se generaron 3 historias de usuario exitosamente",
  "correlationId": "...",
  "textoLimpio": "...",
  "historiasUsuario": [ ... ],
  "metadata": {
    "totalHUs": 3,
    "duracionMs": 8420,
    "versionPrompt": "v1"
  }
}
```

---

## 🗄️ Base de datos

**SQL Server** (EF Core). Las migraciones corren automáticamente al arrancar.

**Tablas:**
- `GeneracionesHU` — registro de cada ejecución (correlationId, modelo, tokens, duración, estado)
- `HistoriasUsuario` — HUs generadas
- `CriteriosAceptacion` — criterios por HU
- `TareasTecnicas` — tareas técnicas por HU

---

## 📊 Logging

Serilog estructurado → consola + archivo rotativo diario (`logs/apihu-YYYYMMDD.log`).

Cada línea incluye `CorrelationId`. Ejemplo:

```
[10:30:45 INF] [A1B2] ▶ ETAPA 1: LIMPIEZA - Iniciando
[10:30:47 INF] [A1B2] ✓ ETAPA 1: LIMPIEZA - Completada en 1333ms
[10:30:52 INF] [A1B2] FIN - Pipeline completado | Tokens in=1240 out=890 total=2130
```

---

## 🔐 Seguridad

- **Secretos fuera del repo**: `.env` + `.gitignore`
- **Rate limiting** por IP
- **API Key** opcional (`X-API-Key` header)
- **Validación de inputs** con Data Annotations
- **Correlation ID** para auditar cualquier request

---

## 📄 Licencia

MIT
