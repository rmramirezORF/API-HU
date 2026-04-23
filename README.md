# API de Generación de Historias de Usuario v2.0 - Production Ready

## 📋 Descripción

API REST empresarial desarrollada en ASP.NET Core (.NET 10) con **Clean Architecture** lista para producción. Utiliza inteligencia artificial para generar Historias de Usuario estructuradas a partir de texto.

---

## 🏗️ Arquitectura Clean Architecture

```
APIHU/
├── API/                          # Capa de presentación
│   └── Controllers/              # Endpoints REST
├── Application/                  # Capa de aplicación
│   ├── DTOs/                     # Data Transfer Objects
│   ├── Interfaces/               # Contratos de servicios
│   └── Services/                 # Servicios + Orchestrator + Validator
├── Domain/                       # Capa de dominio
│   ├── Entities/                 # Entidades del negocio
│   └── Interfaces/               # Contratos de repositorios
├── Infrastructure/               # Capa de infraestructura
│   ├── AI/                       # Proveedores de IA
│   │   ├── Prompts/              # Prompts versionados
│   │   └── OpenAIProviderService.cs
│   ├── Middleware/               # Correlation ID, Rate Limiting, API Key
│   ├── BackgroundServices/       # Procesamiento asíncrono
│   ├── Logging/                  # Configuración de logging
│   └── Persistence/              # DbContext y repositorios
├── Database/                     # Scripts SQL + Migraciones
├── Program.cs                    # Punto de entrada
└── appsettings.json              # Configuración
```

---

## 🔄 Pipeline de Procesamiento (3 Etapas)

```
Texto Original
     │
     ▼
┌─────────────────┐
│  1. LIMPIEZA   │  → Elimina ruido, normaliza texto
└────────┬────────┘
         │
         ▼
┌─────────────────────┐
│ 2. ESTRUCTURACIÓN  │  → Identifica requerimientos
└────────┬────────────┘
         │
         ▼
┌─────────────────┐
│ 3. GENERACIÓN   │  → Crea HUs con criterios y tareas
└────────┬────────┘
         │
         ▼
   Validación HU → Response
```

---

## 🛡️ Características de Producción

| Característica | Descripción |
|----------------|-------------|
| **Correlation ID** | Trazabilidad única por request |
| **Rate Limiting** | Límite de requests por IP (configurable) |
| **API Key** | Seguridad con header X-API-Key |
| **HuProcessingOrchestrator** | Control central del pipeline con logging y medición de tiempo |
| **HuValidatorService** | Valida estructura, criterios, duplicados y coherencia |
| **Background Service** | Preparado para procesamiento asíncrono |
| **Serilog** | Logging estructurado con CorrelationId |

---

## 🤖 IA Desacoplada

```csharp
// Cambiar proveedor es simple:
builder.Services.AddScoped<IAIProviderService, AzureOpenAIProviderService>();
```

- **OpenAIProviderService**: Implementación actual
- **AzureOpenAIService**: Preparado para futuro
- **MockAIProviderService**: Para testing

---

## 🧾 Gestión de Prompts Versionados

```
Infrastructure/AI/Prompts/
├── v1/
│   ├── limpieza.txt
│   ├── estructuracion.txt
│   └── hu.txt
└── v2/
```

---

## 🗄️ Base de Datos (v2.0 Production)

**Tablas:**
- **GeneracionesHU**: Registro con estado, duración, modelo IA, tokens, correlationId
- **HistoriasUsuario**: Relacionada con GeneracionesHU
- **CriteriosAceptacion**: Criterios de cada HU
- **TareasTecnicas**: Tareas técnicas de cada HU

**Campos adicionales en GeneracionesHU:**
- `Estado` (Procesando/Completado/Error)
- `DuracionMs` (duración del procesamiento)
- `ModeloIA` (modelo usado)
- `TokensConsumidos` (tracking)
- `CorrelationId` (trazabilidad)
- `ClientIP` / `UserAgent`

---

## 📊 Logging Estructurado (Serilog)

```
2026-04-21 10:30:45.123 [INFO] [A1B2C3D4] INICIO - Pipeline de procesamiento
2026-04-21 10:30:46.456 [INFO] [A1B2C3D4] ▶ ETAPA 1: LIMPIEZA - Iniciando
2026-04-21 10:30:47.789 [INFO] [A1B2C3D4] ✓ ETAPA 1: LIMPIEZA - Completada en 1333ms
2026-04-21 10:30:52.012 [INFO] [A1B2C3D4] Response A1B2C3D4 | 200 | Duration: 6500ms
```

---

## ⚙️ Configuración (appsettings.json)

```json
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "TimeoutSegundos": 120,
    "EstrategiaCorreccion": true
  },
  "RateLimiting": {
    "Enabled": true,
    "MaxRequests": 100,
    "Window": "00:01:00"
  },
  "ApiKey": {
    "Enabled": false,
    "ValidKeys": ["dev-key-001"]
  },
  "Pipeline": {
    "ValidarHUs": true,
    "MaximoHUsPorRequest": 10
  }
}
```

---

## 📡 Endpoints

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| POST | `/api/hu/generate` | Generar HUs (sin guardar) |
| POST | `/api/hu/generate-and-save` | Generar y guardar en BD |
| GET | `/api/hu/prompts/versions` | Obtener versiones de prompts |
| GET | `/api/hu/health` | Health check |

**Headers de respuesta:**
- `X-Correlation-ID`: ID único para trazabilidad
- `X-RateLimit-Limit`: Límite de requests
- `X-RateLimit-Remaining`: Requests restantes

---

## 🚀 Ejecución

```bash
cd src/APIHU
dotnet build
dotnet run
```

Swagger: `http://localhost:5000/swagger`

---

## 🧪 Ejemplo de Request

```json
POST /api/hu/generate
{
  "texto": "Reunión con cliente: Necesitamos un sistema de inventario...",
  "proyecto": "Inventario v2",
  "maximoHUs": 5,
  "idioma": "es",
  "versionPrompt": "v1"
}
```

**Response incluye:**
```json
{
  "exitoso": true,
  "correlationId": "A1B2C3D4-20260421103045",
  "metadata": {
    "duracionMs": 6500
  }
}
```

---

## 🔐 Seguridad

- **API Key**: Header `X-API-Key` (configurable)
- **Rate Limiting**: Por IP o API Key
- **CORS**: Configurado para desarrollo
- **Validación**: Data Annotations en todos los inputs

---

## 📈 Evolución

| Versión | Características |
|---------|-----------------|
| **v1.0** | API básica con OpenAI |
| **v2.0** | Clean Architecture, pipeline 3 etapas |
| **v2.0 Production** | Correlation ID, Rate Limiting, API Key, Orchestrator, Validator, Background Service |

---

## 🔮 Mejoras Futuras

1. **Caché**: Redis para evitar llamadas repetidas
2. **Azure OpenAI**: Proveedor alternativo
3. **Exportación**: Word, Jira, Markdown
4. **Autenticación**: JWT
5. **Métricas**: Prometheus/Grafana

---

## 📄 Licencia

MIT License - Feel free to use and modify.