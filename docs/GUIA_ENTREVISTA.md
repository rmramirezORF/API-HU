# Cómo entrevistar al cliente para que la API genere mejores HUs

La calidad de las HUs depende **80% del texto que le pasas** y solo 20% del modelo. Esta guía es práctica: te dice **qué preguntar, qué evitar, y cómo usar el parámetro `contexto`** para compensar conversaciones imperfectas.

---

## 🎯 La regla de oro

Cada HU bien hecha responde **3 preguntas**:
1. **¿Quién** la usa? (rol específico, no "usuario")
2. **¿Qué** hace? (acción concreta)
3. **¿Para qué?** (beneficio medible)

Tu trabajo en la entrevista es asegurar que esas 3 cosas queden **explícitas en el texto**, o que las puedas dar tú via `contexto`.

---

## 📋 Estructura ideal de la entrevista (15–20 min)

```
1. APERTURA (2 min)
   ¿Quién eres y qué área?
   ¿A quién supervisas o quién te reporta?

2. NECESIDAD GENERAL (3 min)
   ¿Qué problema estás tratando de resolver?
   ¿Qué hace hoy que no debería?
   ¿Qué te falta?

3. DETALLE DE FUNCIONALIDADES (8 min)
   Por cada cosa que mencione: ¿quién la usa?, ¿con qué frecuencia?,
   ¿qué pasa si falla?, ¿qué datos maneja?

4. INTEGRACIONES Y RESTRICCIONES (3 min)
   ¿Con qué sistemas debe integrarse?
   ¿Hay reglas de negocio o normas legales?
   ¿Plazos o presupuesto?

5. CIERRE (2 min)
   ¿Qué es lo MÁS importante para ti de todo esto?
```

---

## ✅ Preguntas que ayudan al modelo

### Para identificar el rol del usuario

| Mala (genera "como usuario") | Buena (genera el rol concreto) |
|---|---|
| "¿Qué quieres que haga el sistema?" | "¿Tú lo vas a usar tú directamente, o tu equipo?" |
| "¿Qué necesitan los usuarios?" | "¿Quién en tu área usa esta funcionalidad? ¿En qué cargo?" |
| "Cuéntame el flujo" | "Si yo fuera nuevo en tu equipo, ¿desde qué cargo estaría haciendo esto?" |

### Para obtener requerimientos discretos

| Mala (devuelve 1 HU épica) | Buena (separa en HUs claras) |
|---|---|
| "¿Qué quieres que haga la app?" | "¿Cuáles son las **acciones distintas** que un usuario hará?" |
| "Quiero que gestione todo" | "Si tuvieras que listar **botones o pantallas separadas**, ¿cuáles serían?" |

### Para sacar criterios de aceptación

Estas preguntas hacen que el cliente describa los criterios sin saberlo:

- *"¿Qué tendría que pasar para que tú dieras esta funcionalidad por terminada?"*
- *"¿Qué pasa si el usuario hace algo mal? ¿Y si no hay datos?"*
- *"¿Cómo sabrás que está funcionando bien una vez en producción?"*

### Para extraer reglas de negocio

- *"¿Hay límites? ¿Mínimos, máximos, validaciones?"*
- *"¿Qué información NO puede ver un usuario que no debería?"*
- *"Hay alguna regla del sector / legal que debamos respetar?"*

---

## ❌ Qué evitar

### 1. Preguntas técnicas al cliente
**Mal**: *"¿Quieres una API REST o GraphQL?"*  
**Bien**: *"¿Esto debe funcionar también en el celular?"*

### 2. Que el cliente describa la solución, no el problema
**Mal**: *"Necesito un botón que envíe un correo cuando..."*  
**Bien**: *"Cuando un cliente cumple X, ¿cómo te enteras hoy?"*

### 3. Preguntas demasiado abiertas
**Mal**: *"¿Qué quieres que haga el sistema?"* → recibe respuestas vagas  
**Bien**: *"Cuéntame un día típico de tu equipo. ¿Qué pasos hacen?"*

### 4. No interrumpir cuando el cliente se pierde
Si el cliente empieza a dar saludos, anécdotas o se va por las ramas, **redirige**. Esos minutos son ruido que el modelo tendrá que limpiar.

---

## 🧠 Uso estratégico del parámetro `contexto`

Aunque hagas la entrevista perfecta, hay cosas que **el cliente no te va a decir explícitamente** porque le parecen obvias. Para eso está `contexto`.

### Reglas de oro de `contexto`

✅ **Sí incluir:**
- Cargo y área del solicitante
- Cuántas personas afecta la funcionalidad
- Sistema actual que se va a reemplazar (si aplica)
- Sector de la empresa (banca, salud, retail…)
- Restricciones que no salen en la conversación

❌ **No incluir:**
- Texto que ya dice la transcripción
- Detalles técnicos (frameworks, BDs)
- Opiniones tuyas

### Plantilla de `contexto` que funciona

```
[Nombre del solicitante] es [cargo concreto] del área de [departamento].
Tiene a su cargo [N personas / equipo]. Trabajan en [sector].

El proyecto reemplaza/complementa [sistema actual / proceso manual].
Restricciones clave: [normativa, plazo, presupuesto si los hay].
```

### Ejemplos reales

**Caso 1 — Conversación pobre, contexto rico**

Texto pegado en API:
> "Necesitamos un sistema para guardar los datos de las personas."

Contexto:
> "Nicoll es coordinadora de cumplimiento normativo en una entidad financiera regulada por la Superintendencia. Supervisa a 12 analistas que reportan funciones diarias. El aplicativo reemplaza un Excel compartido."

Resultado: HU rica con rol "coordinadora de cumplimiento", criterios sobre auditoría y seguridad.

**Caso 2 — Conversación rica, contexto innecesario**

Texto pegado en API:
> "Carlos como gerente de la tienda online quiere que los clientes registrados puedan ver su historial de compras de los últimos 6 meses para poder hacer recompras..."

Contexto: vacío (no hace falta, ya está todo en el texto).

---

## 📐 Sobre `maximoHUs` (cuántas HUs pedir)

Regla:

| Tu situación | `maximoHUs` recomendado |
|---|---|
| Quieres TODO lo que el texto justifique | **vacío** (el modelo decide) — ESTA ES LA OPCIÓN POR DEFAULT |
| Solo te interesan las 3–5 más importantes | `5` |
| Solo quieres UNA HU épica (sprint corto) | `1` |
| Texto muy largo y solo te interesa la primera entrega | `5–10` |

**Aviso importante**: si pasas `maximoHUs=2` y el texto tiene 8 requerimientos, el modelo descartará 6 sin avisarte. **Mejor pasarse por alto que quedarse corto** — el modelo nunca infla.

---

## 📝 Antes de la entrevista: prepara estas 5 cosas

1. **¿Quién va a estar en la reunión?** Si hay dudas sobre cargos, pregunta antes.
2. **¿Cuál es el objetivo de la conversación?** (modulo nuevo, mejora, integración…)
3. **¿Tienes 15 min de buffer al final?** Para repasar y completar lo que falte.
4. **¿Estás grabando o tomando notas?** El modelo trabaja con cualquier formato, pero las transcripciones de Teams son ideales.
5. **¿Vas a pasar contexto a la API?** Decide qué info adicional vas a darle.

---

## 🎬 Plantilla de entrevista mínima viable (10 minutos)

Si solo tienes 10 minutos, haz estas 7 preguntas en orden:

1. *"¿Cuál es tu cargo y a quién le reportas / quién te reporta?"*
2. *"En una frase, ¿qué problema queremos resolver?"*
3. *"Listemos las acciones distintas que el usuario va a hacer. Yo voy anotando."*
4. *"Por cada acción, ¿qué pasa cuando todo va bien? ¿Y cuando algo falla?"*
5. *"¿Hay límites, validaciones o reglas de negocio que mencionar?"*
6. *"¿Con qué otros sistemas hay que integrarse?"*
7. *"Si tuvieras que priorizar, ¿qué es lo MÁS importante de todo esto?"*

Pasa la transcripción al endpoint con un `contexto` que diga el cargo y área.

---

## 🔬 Ejemplo completo: misma reunión, dos entrevistas

### ❌ Mala entrevista (genera HUs genéricas)

> Entrevistador: ¿Qué necesitas?
> Cliente: Pues que el sistema haga reportes.
> Entrevistador: ¿Reportes de qué?
> Cliente: De ventas, ya sabes, lo normal.

### ✅ Buena entrevista (genera HUs precisas)

> Entrevistador: Hola Juan, antes de empezar — ¿tu cargo es gerente comercial o coordinador?
> Juan: Gerente comercial regional, llevo el equipo de ventas externas de 8 personas.
> Entrevistador: Vale. ¿Qué te falta hoy en el sistema?
> Juan: Necesito ver cómo va cada vendedor sin tener que pedir al equipo de BI un reporte cada lunes.
> Entrevistador: ¿Lo verías tú nada más, o también los vendedores?
> Juan: Yo, mis 3 jefes de zona, y los vendedores ven solo sus propias cifras.
> Entrevistador: Si tuvieras que listar las cosas distintas que el sistema haría, ¿cuáles serían?
> Juan: Reporte mensual, reporte semanal, comparativa entre vendedores, exportar a Excel, gráficas.

Diferencia: la segunda transcripción ya **te dice el rol, el alcance, los permisos por nivel y los entregables**. La API generará 4–5 HUs precisas con criterios reales.

---

## 🧪 Cómo validar que entrevistaste bien

Antes de pasar el texto a la API, revisa estas 5 cosas:

- [ ] ¿El **rol del usuario** está mencionado al menos 2 veces?
- [ ] ¿Hay **acciones concretas** (verbos) y no solo deseos vagos?
- [ ] ¿Se mencionaron al menos **3 escenarios distintos** (caso feliz, error, límite)?
- [ ] ¿Aparecen **datos concretos** (cantidades, plazos, IDs)?
- [ ] ¿Sabes con **qué otro sistema** debe integrarse?

Si dices "sí" a 4 de las 5: tu transcripción está lista para la API. Si dices "no" a 3 o más: vuelve a la conversación o usa `contexto` para llenar los huecos.
