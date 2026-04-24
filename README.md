# API-HU — Generación de Historias de Usuario con IA

API REST en **ASP.NET Core (.NET 10)** que convierte texto crudo (transcripciones de Teams, notas de reunión, requerimientos) en **Historias de Usuario estructuradas** con criterios de aceptación y tareas técnicas.

Soporta múltiples proveedores de IA con **fallback automático entre ellos**: Gemini, OpenRouter y Groq.

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
│   ├── AI/                   # Providers (Gemini/OpenRouter/Groq) + Prompts/
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

## 🤖 Proveedores de IA soportados

Todos con **tier gratuito** (sin tarjeta de crédito). El proyecto permite combinarlos con **fallback automático entre ellos**.

| Proveedor | Archivo | Free tier | Estabilidad |
|---|---|---|---|
| **OpenRouter** | [OpenRouterProviderService.cs](src/APIHU/Infrastructure/AI/OpenRouterProviderService.cs) | 200 req/día en modelos `:free` | ⚠️ Variable (upstream compartido) |
| **Groq** | [GroqProviderService.cs](src/APIHU/Infrastructure/AI/GroqProviderService.cs) | 14,400 req/día (Llama 3.3 70B) | ✅ Muy estable |
| **Gemini** | [GeminiProviderService.cs](src/APIHU/Infrastructure/AI/GeminiProviderService.cs) | 1,500 req/día (Gemini 2.0 Flash) | ✅ Estable |

### Fallback en 2 niveles

1. **Dentro de un proveedor** (OpenRouter): si el modelo principal falla, prueba con modelos alternativos de la misma API.
2. **Entre proveedores** ([MultiProviderFallbackService.cs](src/APIHU/Infrastructure/AI/MultiProviderFallbackService.cs)): si un proveedor entero falla, pasa al siguiente.

```
OpenRouter (modelo A → B → C → D)
        │ si todos fallan
        ▼
Groq (Llama 3.3 70B)
        │ si falla
        ▼
Gemini (2.0 Flash)
```

**Robustez incluida en cada proveedor:**
- Reintentos con **exponential backoff + jitter**
- Respeto al header `Retry-After`
- Manejo específico de `429`/`503`/`5xx` (transitorios) vs `400`/`401` (errores definitivos)
- **Tracking real de tokens** (input/output) persistido en BD
- Timeout configurable por request

La interfaz `IAIProviderService` está desacoplada — añadir un nuevo proveedor es crear una implementación más.

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
# Orden de proveedores (fallback automático)
AI__ProviderChain=openrouter,groq,gemini

# Configura solo las keys que tengas (las demás se saltan al arrancar)
OPENROUTER_API_KEY=sk-or-v1-...
GROQ_API_KEY=gsk_...
GEMINI_API_KEY=AIza...

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
