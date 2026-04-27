# Prompt para Claude Design — Frontend de API-HU

Este es el prompt que debes copiar y pegar tal cual en **Claude Design** (o cualquier otra herramienta de generación de UI con IA) para que te genere el frontend de tu API-HU.

> **Cómo usarlo**:
> 1. Abre tu herramienta de diseño con IA (Claude Design, v0, bolt.new, etc.)
> 2. Copia TODO el contenido del bloque siguiente (entre las líneas `---PROMPT---`)
> 3. Pégalo como tu primer mensaje
> 4. Itera con preguntas cortas si necesitas ajustes específicos

---

## 📋 Stack tecnológico recomendado (justificación)

He elegido **Vite + React 18 + TypeScript + Tailwind CSS + shadcn/ui** porque:

| Pieza | Por qué |
|---|---|
| **Vite** | Build rápido, sin SSR (no lo necesitas, tu API es local), simple de desplegar como estático |
| **React 18 + TypeScript** | Estándar de la industria, máxima documentación y soporte, type-safety te evita bugs tontos |
| **Tailwind CSS** | Estilos rápidos sin escribir CSS, fácil de mantener, excelente con componentes |
| **shadcn/ui** | Componentes profesionales (botones, formularios, cards) que copias al proyecto y modificas. NO es una dependencia, es código tuyo |
| **TanStack Query** | Manejo de API calls con loading/error/cache automático |
| **react-hook-form + zod** | Formularios con validación type-safe |
| **lucide-react** | Iconos modernos (matching shadcn/ui) |
| **sonner** | Toast notifications elegantes |

Es el stack moderno más común de React hoy. Cualquier desarrollador React puede mantenerlo sin curva de aprendizaje.

---

## 🎨 Prompt para copiar/pegar

```text
---PROMPT---

Eres un diseñador frontend senior. Quiero que generes el frontend completo de mi aplicación "API-HU", una herramienta interna para generar Historias de Usuario (HUs) a partir de transcripciones de reuniones (Teams, Meet, etc).

## CONTEXTO DEL PRODUCTO

API-HU es un servicio que ya existe (corre en http://localhost:5000). Recibe texto crudo de una conversación y devuelve HUs estructuradas usando IA. Yo necesito construir la interfaz web para usarla.

Usuario objetivo: analista funcional o product owner que tiene una transcripción de reunión y quiere convertirla en HUs sin escribirlas a mano.

## STACK OBLIGATORIO

- Vite + React 18 + TypeScript
- Tailwind CSS v3
- shadcn/ui (componentes copiados al proyecto en src/components/ui/)
- TanStack Query (@tanstack/react-query) para llamadas a la API
- react-hook-form + zod para formularios
- lucide-react para iconos
- sonner para toasts

NO uses Next.js, NO uses styled-components, NO uses Material UI. NO añadas librerías que no estén en esta lista sin justificarlas explícitamente.

## ESTRUCTURA DE CARPETAS

Sigue esta estructura exacta:

```
src/
├── api/
│   ├── client.ts             # axios o fetch wrapper, base URL configurable
│   └── hu.ts                 # funciones tipadas: generateFromText(), getHealth()
├── components/
│   ├── ui/                   # componentes de shadcn/ui (button, card, input, etc.)
│   ├── HuGeneratorForm.tsx   # formulario principal
│   ├── HuResultCard.tsx      # tarjeta de una HU generada
│   ├── HuResultList.tsx      # lista de HUs con metadata
│   ├── HealthBadge.tsx       # indicador del estado del backend
│   └── ThemeToggle.tsx       # switch claro/oscuro
├── hooks/
│   ├── useGenerateHu.ts      # mutation con TanStack Query
│   └── useHealth.ts          # query con polling cada 30s
├── lib/
│   ├── utils.ts              # cn() de shadcn
│   └── storage.ts            # wrapper de localStorage para historial
├── pages/
│   ├── HomePage.tsx          # página principal
│   └── HistoryPage.tsx       # historial local de generaciones
├── types/
│   └── hu.ts                 # interfaces TypeScript de la API
├── App.tsx                   # router (react-router-dom)
└── main.tsx                  # entry point con providers (QueryClient, Theme)
```

## CONTRATOS DE LA API (TypeScript)

Genera estos tipos exactos en src/types/hu.ts:

```ts
export interface CriterioAceptacion {
  descripcion: string;
  orden: number;
  esObligatorio: boolean;
}

export interface TareaTecnica {
  descripcion: string;
  tipo: string; // 'Backend' | 'Frontend' | 'Database' | 'DevOps' | 'Testing' | 'Security'
  orden: number;
}

export interface HistoriaUsuario {
  titulo: string | null;
  como: string | null;
  quiero: string | null;
  para: string | null;
  descripcion: string | null;
  criteriosAceptacion: CriterioAceptacion[] | null;
  tareasTecnicas: TareaTecnica[] | null;
}

export interface GenerarHUMetadata {
  fechaGeneracion: string;
  proyecto: string | null;
  totalHUs: number;
  idioma: string | null;
  versionPrompt: string | null;
  duracionMs: number;
  respuestaTruncada: boolean;
  advertenciaTruncamiento: string | null;
}

export interface GenerarHUResponse {
  exitoso: boolean;
  mensaje: string;
  textoLimpio: string | null;
  historiasUsuario: HistoriaUsuario[];
  metadata: GenerarHUMetadata | null;
  generacionId: number | null;
  correlationId: string | null;
}

export interface HealthResponse {
  status: string;          // "Healthy"
  timestamp: string;
  service: string;
  pipeline: string;
  proveedorIA: string;     // "Gemini" | "Groq" | "OpenRouter" | "MultiProvider[...]"
  modelo: string;          // ej: "gemini-2.5-flash"
}

export interface ErrorResponse {
  tipo: string;            // "ValidationError" | "GenerationError" | "InternalError"
  mensaje: string;
  detalle: string | null;
  timestamp: string;
  correlationId: string | null;
}
```

## ENDPOINTS A CONSUMIR

Base URL configurable via VITE_API_BASE_URL (default http://localhost:5000).

1. **POST /api/hu/generate-from-text**
   - Headers: `Content-Type: text/plain`
   - Body: el texto crudo (string, sin escapar)
   - Query params:
     - `proyecto` (string opcional)
     - `maximoHUs` (number opcional — si vacío, el modelo decide)
     - `idioma` (string, default "es")
     - `versionPrompt` (string, default "v2")
     - `contexto` (string opcional, hasta 2000 chars)
   - Respuesta exitosa: GenerarHUResponse (status 200)
   - Respuesta error: ErrorResponse (status 400 o 500)

2. **GET /api/hu/health**
   - Sin body, sin params
   - Respuesta: HealthResponse (status 200)

## PÁGINAS Y COMPORTAMIENTO

### HomePage (/) — Generador de HUs

Layout:
- Header sticky con: logo "API-HU" a la izquierda, navegación al centro (Inicio / Historial), HealthBadge + ThemeToggle a la derecha
- Main con dos columnas en desktop, una sola en mobile:
  - **Columna izquierda (form)**:
    - Campo "Proyecto" (input)
    - Campo "Texto de la reunión" (textarea grande, mínimo 10 líneas, autoresize, contador de caracteres con indicación 20/10000)
    - Acordeón "Opciones avanzadas" colapsado por defecto, contiene:
      - "Contexto adicional" (textarea pequeño, max 2000 chars, con tooltip explicativo)
      - "Máximo de HUs" (number input, opcional, placeholder "Vacío = el modelo decide")
      - "Idioma" (select: es / en)
      - "Versión de prompt" (select: v2 / v1)
    - Botón principal "Generar HUs" (primario, disabled si texto < 20 chars)
    - Botón secundario "Limpiar" (resetea el form)
  - **Columna derecha (resultados)**:
    - Si no hay datos: ilustración o icono grande + texto "Pega una transcripción y genera HUs"
    - Si está cargando: skeleton de 2-3 cards + texto "Procesando con [modelo]..." (toma del HealthBadge)
    - Si hay error: alerta roja con el mensaje del backend + botón "Reintentar"
    - Si hay éxito:
      - Banner verde "Se generaron N HUs en X.X segundos"
      - Si `respuestaTruncada` es true: banner amarillo con `advertenciaTruncamiento`
      - Sección plegable "Texto limpio que se procesó" mostrando `textoLimpio`
      - Lista de HuResultCard, cada una expandible para ver criterios y tareas
      - Botón "Exportar como JSON" (descarga el response completo)
      - Botón "Copiar todo como Markdown" (copia al portapapeles las HUs en formato MD)

### HistoryPage (/history) — Historial local

- Lista las últimas 20 generaciones guardadas en localStorage (cada una con: timestamp, proyecto, número de HUs, modelo usado, primer título)
- Click en una entrada abre el detalle (mismas HuResultCard que en HomePage)
- Botón "Borrar historial"
- Si no hay historial: mensaje vacío con CTA "Volver al inicio"

## COMPONENTES CLAVE

### HuResultCard

- Card de shadcn con header, contenido y footer
- Header: badge con número de HU (ej: "HU 3"), título de la HU
- Contenido (siempre visible):
  - Línea con "Como [como]" en cursiva con color suave
  - Línea con "Quiero [quiero]"
  - Línea con "Para [para]" en cursiva
- Sección expandible (acordeón):
  - **Descripción** (texto)
  - **Criterios de aceptación** (lista numerada, cada criterio con icono check)
  - **Tareas técnicas** (cada tarea con badge del tipo coloreado: Backend=azul, Frontend=verde, Database=naranja, DevOps=púrpura, Testing=rosa, Security=rojo)
- Footer: botones pequeños "Copiar HU" y "Copiar como Markdown"

### HealthBadge

- Pequeño badge en el header
- Verde con punto pulsante si /health responde 200
- Rojo si no responde
- Texto: "[Proveedor] · [modelo]" (ej: "Gemini · gemini-2.5-flash")
- Tooltip al hover: timestamp del último check
- Polling cada 30 segundos

### ThemeToggle

- Switch claro/oscuro/sistema
- Persiste preferencia en localStorage
- Aplica clase `dark` al `<html>` cuando corresponde

## VISUAL DIRECTION

**Estilo general**: minimalista, profesional, con jerarquía clara. Nada cargado.

**Paleta** (define en tailwind.config.ts):
- Modo claro: blanco #ffffff base, slate-50 secundario, slate-200 borders
- Modo oscuro: slate-950 base, slate-900 secundario, slate-800 borders
- Color primario: violet-600 (botones, links, énfasis)
- Color éxito: emerald-500
- Color advertencia: amber-500
- Color error: rose-500
- Texto: slate-900 / slate-100 (claro/oscuro)

**Tipografía**:
- Sans: Inter (vía @fontsource/inter)
- Mono: JetBrains Mono para código y JSON exportado

**Espaciado y layout**:
- Container max-w-7xl mx-auto px-4 md:px-8
- Espaciado generoso entre secciones (space-y-8)
- Border radius: rounded-xl en cards, rounded-md en inputs

**Microinteracciones**:
- Transiciones suaves (transition-colors duration-200)
- Hover en cards: leve elevación (hover:shadow-md)
- Loading: skeleton con animate-pulse
- Toast con sonner para confirmaciones (copia al portapapeles, errores, etc.)

**Accesibilidad**:
- Labels en todos los inputs (asociados con htmlFor)
- Contraste AA mínimo
- Focus rings visibles
- Soporte completo de teclado

## ESTADOS DE LA UI A CUBRIR

Por cada acción debes mostrar el estado correcto:

| Acción | Estado vacío | Cargando | Error | Éxito |
|---|---|---|---|---|
| Generar HUs | Placeholder con CTA | Skeleton + texto del modelo en uso | Alerta + botón reintentar | Lista de HUs + banner + acciones |
| Verificar salud | Badge gris "..." | Badge gris pulsante | Badge rojo "API caída" | Badge verde con info |
| Cargar historial | Vacío con CTA | (instantáneo, no aplica) | (no aplica) | Lista de tarjetas |
| Copiar a portapapeles | (n/a) | (n/a) | Toast rojo | Toast verde "Copiado" |

## EXPORT MARKDOWN (función auxiliar)

Cuando el usuario hace clic en "Copiar como Markdown", convierte así:

```markdown
## HU 1: [titulo]

**Como** [como]
**Quiero** [quiero]
**Para** [para]

[descripcion]

### Criterios de aceptación
1. [criterio 1]
2. [criterio 2]
...

### Tareas técnicas
- [Backend] [tarea 1]
- [Frontend] [tarea 2]
...

---

## HU 2: ...
```

## ENTREGABLES

1. Estructura completa de archivos según el árbol arriba
2. package.json con todas las dependencias correctas y scripts (`dev`, `build`, `preview`)
3. tailwind.config.ts y postcss.config.js correctos
4. Configuración de shadcn/ui (components.json)
5. Variables de entorno documentadas en .env.example (VITE_API_BASE_URL)
6. README.md corto explicando cómo correr el frontend (`npm install`, `npm run dev`)
7. Todo el código tipado con TypeScript estricto (no `any`)

## PROHIBIDO

- NO uses Next.js, NO uses Material UI, NO uses Bootstrap
- NO añadas autenticación / login (no aplica todavía)
- NO añadas i18n (todo en español)
- NO uses Server Components ni RSC
- NO uses CSS-in-JS (solo Tailwind)
- NO escribas tests todavía (los añadiremos después)

## ENFOQUE PRIMERO

Empieza por:
1. La estructura de carpetas y archivos vacíos correctos
2. El package.json y configs (Tailwind, shadcn, Vite)
3. La página principal funcional con el formulario y un estado de éxito hardcodeado para ver el diseño
4. Conectarlo a la API real
5. Completar HistoryPage al final

Si una decisión no está clara, elige la opción más simple y mantenible. Comenta en el código las decisiones que tomes.

---PROMPT---
```

---

## 🚀 Cómo proceder paso a paso

### 1. Generar el proyecto inicial
1. Abre Claude Design (o tu herramienta favorita)
2. Pega el prompt completo de arriba
3. Deja que genere todo el scaffold + página principal

### 2. Iterar
Después del primer output, prueba con prompts cortos como:
- "El HuResultCard se ve apretado. Aumenta el padding y separa los criterios en una columna lateral"
- "El HealthBadge no se ve bien en mobile, pásalo a un drawer cuando width < 768"
- "Añade animación de entrada (fade-in con stagger) cuando aparecen las HUs"

### 3. Conectar al backend real
Una vez tengas el proyecto, configura el `.env`:
```
VITE_API_BASE_URL=http://localhost:5000
```

### 4. Probar contra tu API local
```bash
# Terminal 1: backend
cd src/APIHU && dotnet run

# Terminal 2: frontend
cd <ruta-del-frontend>
npm install
npm run dev
```

### 5. Si quieres deployarlo después
Como es estático, cualquiera de estas funciona gratis:
- **Vercel** — `vercel deploy` desde la carpeta del frontend
- **Netlify** — drag-and-drop de `dist/` después de `npm run build`
- **Cloudflare Pages** — conectas el repo y listo
- **GitHub Pages** — con `vite.config.ts` ajustando `base`

---

## 🎯 Por qué este stack es la elección correcta

Hace 2 años hubieras elegido Next.js. Hoy, para una app interna sin SEO ni SSR, Vite + React es **más simple, más rápido en dev, más fácil de mantener**.

shadcn/ui en particular es la decisión clave: en vez de depender de una librería de componentes (que algún día deprecará algo), copias el código a tu proyecto y lo modificas. Eres dueño del código.

TanStack Query elimina toda la complejidad de manejar loading/error/cache manual. Tres líneas de código y tienes una integración profesional con tu API.

Es exactamente el stack que está usando todo el ecosistema React moderno (Linear, Vercel dashboards, Cal.com, etc.).

---

## 🔮 Próximos pasos cuando tengas el frontend

Cuando ya tengas la UI generada y conectada, podemos añadir:

1. **CORS en el backend** para que el frontend pueda llamar a la API desde otro origen (puerto distinto)
2. **Persistencia real** del historial en la BD (el `generate-and-save` que dejamos preparado)
3. **Edición de HUs** generadas antes de exportar
4. **Export a Jira / Azure DevOps** vía sus APIs
5. **Streaming** de la respuesta para ver las HUs aparecer una por una

Pero primero: que el frontend exista y funcione.
