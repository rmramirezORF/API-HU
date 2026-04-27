# Guía técnica de API-HU

Documento de referencia rápida sobre **qué hay**, **cuánto puedes pedir** y **cómo se configura**. Pensado para revisar en 5 minutos.

---

## 🧭 Qué hace la API

Recibe un texto crudo (transcripción de Teams, notas de reunión, requerimientos sueltos) y devuelve **Historias de Usuario estructuradas** con criterios de aceptación y tareas técnicas. Internamente hace 3 etapas con IA:

```
Texto crudo → [1] Limpieza → [2] Estructuración → [3] Generación HU → JSON con HUs
```

---

## 🌐 Endpoints

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/api/hu/generate-from-text` | **Recomendado.** Pegas el texto crudo en el body como `text/plain` (sin escapar `\n`). |
| `POST` | `/api/hu/generate` | Igual pero con JSON. Útil para integración programática (debes escapar saltos de línea como `\n`). |
| `GET` | `/api/hu/health` | Estado del servicio + proveedor IA activo. |

Swagger UI disponible en `http://localhost:5000/swagger`.

### Parámetros del endpoint principal

| Parámetro | Dónde | Obligatorio | Para qué |
|---|---|---|---|
| `(body)` | `text/plain` | Sí | El texto a procesar (20–10.000 caracteres) |
| `proyecto` | query | No | Nombre del proyecto |
| `maximoHUs` | query | **No (recomendado dejar vacío)** | Si lo dejas vacío el modelo decide cuántas HUs justifica el texto. Si lo pasas, actúa como techo duro. |
| `idioma` | query | No (default `es`) | `es` o `en` |
| `versionPrompt` | query | No (default `v2`) | `v1` (clásico) o `v2` (con detección de roles) |
| `contexto` | query | No (muy recomendado) | Texto libre con info adicional sobre roles, área, jerga del proyecto |

---

## 🤖 Proveedores (3 agentes) y modelos (16 totales)

La API usa **3 proveedores de IA en cascada con fallback automático**: si el primero falla, intenta el siguiente. Dentro de cada proveedor también hay modelos alternativos.

### Resumen rápido

| # | Proveedor | Modelo principal | Modelos extra | Total |
|---|---|---|---|---|
| 1 | **Gemini** (Google) | `gemini-2.5-flash` | 1 | 2 |
| 2 | **Groq** | `llama-3.3-70b-versatile` | 4 | 5 |
| 3 | **OpenRouter** | `google/gemma-3-27b-it:free` | 8 | 9 |
| | | | **TOTAL** | **16** |

Si quieres saber cuál está respondiendo en cada momento, mira el campo `metadata.modelo` de la respuesta o `/api/hu/health`.

### Detalle por proveedor

**Gemini (Google)** — el más preciso, con `:thinking` desactivado para respuestas rápidas.

| Modelo | Uso |
|---|---|
| `gemini-2.5-flash` | Principal — equilibrio calidad/velocidad |
| `gemini-2.5-flash-lite` | Fallback — más rápido, ligeramente menos preciso |

**Groq** — el más rápido cuando responde (LPU hardware propio).

| Modelo | Uso |
|---|---|
| `llama-3.3-70b-versatile` | Principal — Meta Llama 70B |
| `openai/gpt-oss-120b` | Fallback 1 — modelo open source de OpenAI |
| `qwen/qwen3-32b` | Fallback 2 — Alibaba Qwen |
| `openai/gpt-oss-20b` | Fallback 3 — versión más ligera |
| `llama-3.1-8b-instant` | Fallback 4 — el más rápido de Groq |

**OpenRouter** — más cantidad de modelos pero free tier inestable.

| Modelo | Uso |
|---|---|
| `google/gemma-3-27b-it:free` | Principal — Gemma 3 |
| `qwen/qwen3-next-80b-a3b-instruct:free` | Fallback 1 |
| `openai/gpt-oss-120b:free` | Fallback 2 |
| `openai/gpt-oss-20b:free` | Fallback 3 |
| `inclusionai/ling-2.6-flash:free` | Fallback 4 |
| `nvidia/nemotron-3-nano-30b-a3b:free` | Fallback 5 |
| `nousresearch/hermes-3-llama-3.1-405b:free` | Fallback 6 — 405B parámetros |
| `google/gemma-4-31b-it:free` | Fallback 7 — Gemma 4 |
| `z-ai/glm-4.5-air:free` | Fallback 8 |

---

## 📊 Capacidad diaria (free tier)

Cada generación de HUs hace **3 calls a la IA** (limpieza, estructuración, generación). Por eso la capacidad real en pipelines completos es 1/3 del límite por proveedor.

| Proveedor | Límite por día | Pipelines posibles/día |
|---|---|---|
| **Gemini** | 1.500 calls | ~500 pipelines |
| **Groq** | 14.400 calls | ~4.800 pipelines |
| **OpenRouter** (`:free`) | 200 calls | ~66 pipelines |
| | **TOTAL combinado** | **~5.366 pipelines/día** |

> En la práctica, el sistema usa Gemini primero. Solo cae a Groq u OpenRouter si Gemini está saturado. Por eso normalmente verás respuestas de Gemini.

### Límites por minuto

Adicional al límite diario, cada proveedor tiene un **cap por minuto**:

| Proveedor | Por minuto |
|---|---|
| Gemini 2.5 Flash | 10 calls/min |
| Groq Llama 3.3 70B | 30 calls/min |
| OpenRouter `:free` | 20 calls/min |

Una generación normal son 3 calls, así que **no hagas más de ~3 generaciones en un minuto si quieres usar Gemini exclusivamente**.

---

## ⏰ Cuándo se restablecen las cuotas

| Tipo | Cuándo se resetea |
|---|---|
| **Cuota por minuto** | Ventana rodante de 60 segundos (rolling) — espera 1 min |
| **Cuota diaria de Gemini** | A las **00:00 Pacific Time** (Colombia: 02:00 / España: 09:00) |
| **Cuota diaria de Groq** | A las **00:00 UTC** (Colombia: 19:00 / España: 01:00) |
| **Cuota diaria de OpenRouter** | A las **00:00 UTC** (Colombia: 19:00 / España: 01:00) |

> Si ves muchos `429 rate-limited` seguidos, espera **1–2 minutos** y reintenta.

---

## ⚙️ Configuración via `.env`

Las variables principales:

```env
# Selector de proveedores (orden de fallback)
AI__ProviderChain=gemini,groq,openrouter

# API Keys
GEMINI_API_KEY=AIza...
GROQ_API_KEY=gsk_...
OPENROUTER_API_KEY=sk-or-v1-...

# Cambiar modelo principal de cada proveedor
Gemini__Modelo=gemini-2.5-flash
Groq__Modelo=llama-3.3-70b-versatile
OpenRouter__Modelo=google/gemma-3-27b-it:free

# Subir el techo de tokens si tu texto genera 25+ HUs
Gemini__MaxTokens=16384
Groq__MaxTokens=16384
OpenRouter__MaxTokens=16384

# Subir timeout por call si tu output es muy grande
Gemini__TimeoutSegundos=120
```

Tras cambiar el `.env` debes **reiniciar el servidor** (`Ctrl+C` y `dotnet run` otra vez).

---

## 🔄 Cómo cambiar el orden de proveedores

```env
# Caso 1: Gemini primero (default - mejor calidad)
AI__ProviderChain=gemini,groq,openrouter

# Caso 2: Groq primero (más rápido y estable)
AI__ProviderChain=groq,gemini,openrouter

# Caso 3: solo Gemini
AI__ProviderChain=gemini

# Caso 4: solo OpenRouter (para experimentar con sus 9 modelos)
AI__ProviderChain=openrouter
```

---

## 🚨 Errores comunes y qué hacer

| Error que ves | Qué significa | Qué hacer |
|---|---|---|
| `429 rate-limited` repetido | Agotaste cuota por minuto | Esperar 1–2 min |
| `403 Forbidden` con `limit: 0` | Tu key no tiene free tier activo | Crear key nueva en otro proyecto de Google AI Studio |
| `respuestaTruncada: true` en metadata | El modelo no terminó de generar | Subir `Gemini__MaxTokens` o dividir el texto |
| `Generación HU = "Historia de Usuario"` con campos null | Parser cayó al fallback | Ya no debería pasar (parser inteligente). Si pasa, mira el log. |
| Timeout 120s | Modelo se quedó colgado | Espera y reintenta. Si ocurre repetidamente, baja `MaxTokens` o usa Groq |
| `400 JSON inválido` | Pegaste texto con saltos de línea reales en el endpoint JSON | Usa `/generate-from-text` que acepta texto plano |

---

## 💸 Costos reales

Hoy todo el sistema corre **gratis** con free tier. Si en algún momento necesitas garantía de respuesta sin rate limits compartidos:

- **OpenRouter $5 USD** → desbloquea modelos paid (5–10s/call). $5 dan para ~5.000 generaciones de pipeline.
- **Gemini paid** → $0.075/1M input + $0.30/1M output con `gemini-2.5-flash`. Una generación cuesta ~$0.001 (un milésimo de dólar).
- **Groq paid** → similar a OpenRouter, $5 mínimo para empezar.

---

## 📦 Pipeline interno (referencia rápida)

```
[Etapa 1: LIMPIEZA]      ~5–15s
  Quita ruido (saludos, timestamps, fillers)
  Preserva nombres y roles mencionados
  Output: textoLimpio

[Etapa 2: ESTRUCTURACIÓN]  ~5–15s
  Identifica requerimientos discretos
  Categoriza (CRUD, integración, autenticación, etc.)
  Output: lista de Requerimientos

[Etapa 3: GENERACIÓN HU]   ~10–40s
  Crea HUs con formato "Como [rol], quiero [X], para [Y]"
  Añade criterios de aceptación (Gherkin)
  Añade tareas técnicas categorizadas
  Output: lista de HistoriasUsuario

Total: 20–60 segundos en condiciones normales
```

Cada etapa hace UNA call a la IA. Las 3 son **secuenciales** (no en paralelo) porque cada una usa la salida de la anterior.
