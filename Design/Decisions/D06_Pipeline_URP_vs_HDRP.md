# D06 — URP o HDRP: decisión provisional y plan de prueba

**Estado:** pendiente de benchmark (Fase 0). **Provisional:** URP, solo para que F0-A arranque.
**Regla:** al cerrar la prueba se elige **uno** y se elimina el otro. Cambiar más tarde es caro.

## Por qué URP de forma provisional
- La plantilla Universal 3D funciona en más hardware y el hardware objetivo aún no está definido.
- El prototipo F0-A/F1 es geometría gris: el pipeline no condiciona nada todavía.
- Si HDRP gana, migrar ahora cuesta casi nada: no hay materiales ni iluminación propios.

## Antes de la prueba
1. **Fijar el PC de referencia** (pregunta abierta §17): CPU, GPU, RAM, resolución (1080p) y objetivo (60 fps).
   Sin esto el benchmark no decide nada.
2. Crear **dos proyectos de prueba desechables** (uno URP, otro HDRP) con el mismo editor 6000.3.x. No mezclar pipelines en el proyecto principal.

## Escena de prueba (idéntica en ambos)
- Una fachada de piedra detallada (malla CC0 o modelada a mano) con material PBR de granito y mampostería.
- Una callejuela estrecha con pendiente y tres o cuatro volúmenes de edificios.
- 4–6 personajes temporales (cápsulas o modelos CC0) y la cámara del prototipo.
- Tres iluminaciones del §7.1: **día despejado**, **atardecer** y **noche de sigilo** con 3–5 antorchas (luces puntuales con sombra).

## Qué medir (en **build** de Windows, no en el editor)
| Métrica | Cómo |
|---|---|
| Frametime medio, p95 y p99 | Profiler conectado al build, 60 s de recorrido fijo |
| Picos (>33 ms) | contar en el mismo recorrido |
| Memoria GPU y RAM | Memory Profiler |
| Tiempo de build y tamaño | registro del build |
| Calidad visual | 3 capturas por iluminación desde cámaras fijas |
| Coste de trabajo | horas para ajustar luz y materiales hasta un resultado aceptable |

## Criterios de decisión
- Si **URP** alcanza 60 fps en el PC de referencia con calidad aceptable en las tres iluminaciones → URP (más simple y más hardware).
- **HDRP** solo si el PC de referencia es de gama alta, la diferencia visual en piedra y noche es clara en las capturas **y** cumple 60 fps.
- Empate → URP.

## Resultado
Rellenar al cerrar: fecha, hardware, tabla de métricas, capturas en `Reports/Performance/` y línea en `DecisionLog.md`.
