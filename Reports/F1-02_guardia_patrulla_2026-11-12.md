## Incremento F1-02: guardia con patrulla, vista y oído

- **Objetivo/gate:** segundo corte de F1 (spec §10: «NPC con patrulla y 4 estados de percepción») y casos de aceptación de sigilo de spec §6.2. Conecta al juego la máquina de alerta escrita en F0-A.

- **Cambios de código/escenas/assets:**
  - `Scripts/Simulation/AI/StealthRules.cs` (lógica pura): `VisionCone` (cono horizontal de 50° de medio ángulo, alcance 14 m, atenuación con la distancia, visión periférica, percepción cercana de 1,5 m, nunca a través de obstáculos), `StealthRules` (exposición: agachado 0,5; ruido: andar 2,5 m, correr 8 m, caída 7 m, agachado o quieto 0) y `VisionConfig`.
  - `AlertStateMachine.GiveUp()`: el guardia da por terminada la alarma tras alcanzar al intruso.
  - `Scripts/Runtime/AI/StealthTarget.cs`: contrato de lo que un guardia sabe de quien vigila.
  - `Scripts/Runtime/AI/GuardController.cs`: sentidos (rayo de visión contra la geometría, oído por radio) y movimiento por estado: recorre la ruta, se para y mira al sospechar, va al último punto conocido al investigar o buscar, corre en alarma, vuelve al punto de ruta más cercano. Puede ser centinela fijo (`SetPost`). Señal sobre la cabeza: color por estado y tamaño por sospecha. Gizmos del cono al seleccionarlo.
  - `Scripts/Runtime/Prototype/PlayerStealthTarget.cs`: el jugador como objetivo detectable; al ser alcanzado vuelve al punto de salida.
  - `PrototypePlayerController`: expone `IsRunning`, `LastLandingTime`, `CurrentHeight` y `Respawn()`.
  - `PrototypeHud`: línea con el estado del guardia y el porcentaje de sospecha.
  - `PrototypeSceneBuilder`: guardia con ruta rectangular alrededor del circuito (esquinas en (16, 17), (−12, 17), (−12, −5) y (16, −5)); materiales `Proto_Guardia` y `Proto_Senal`. Escenas `Prototype` y `Z01_Relieve` regeneradas (Z01 sin guardia).
  - `VerifyProject`: la escena debe tener guardia y un jugador detectable.

- **Comandos ejecutados** (Unity 6000.3.6f1 sobre `C:\Users\Propietario\Qazrix`): `PrototypeSceneBuilder.RebuildBatch`, `Z01GeoBuilder.BuildBatch`, `VerifyProject.RunBatch`, `-runTests -testPlatform EditMode` (los cuatro con `-batchmode`) y `-runTests -testPlatform PlayMode` (con ventana). Todos con salida 0 a la primera.

- **Pruebas:**
  - Aprobadas: compilación; VerifyProject 0 errores, 1 aviso (pipeline provisional); EditMode 42/42 (11 nuevos); PlayMode 9/9 (5 nuevos).
  - Casos de spec §6.2 y cómo quedan cubiertos:

    | Caso | Regla pura (EditMode) | En Play (escena Prototype) |
    |---|---|---|
    | Detección frontal | sí | sí: pasa por sospecha, da la alarma y alcanza al jugador |
    | Lateral, fuera del cono | sí | no |
    | Tras obstáculo | sí | sí: pared alta en medio, a 12 m y de frente, visibilidad 0 |
    | A cubierto (agachado) | sí, exposición a la mitad | no |
    | Sonido fuera de la vista | sí | sí: correr a su espalda se oye y se investiga, sin alarma |
    | Andar agachado en silencio | sí | sí |
    | Pérdida de contacto y búsqueda | sí (máquina de alerta) | no |
    | Retorno a patrulla | sí (máquina de alerta) | parcial: vuelve tras alcanzar al jugador |

  - No ejecutadas: **Play a mano**. Nadie ha jugado contra el guardia; los alcances, tiempos y velocidades son de partida y no están ajustados.

- **Build y plataforma:** sin build.

- **Evidencia:** `Reports/QA/play_guardia_investiga.png` (el recuadro muestra «Guardia: investiga · sospecha 18 %»), `Reports/QA/playmode-results.xml`, `Reports/QA/editmode-results.xml`, `Reports/QA/verify-project-latest.md`.

- **Derechos/licencias de activos nuevos:** ninguno externo; dos materiales de color plano propios.

- **Decisiones tomadas:** D37.

- **Problemas conocidos:**
  - **Sin navegación:** el guardia va en línea recta a su destino. Si hay un obstáculo en medio (la pared alta, el túnel, el mirador), se queda empujando contra él hasta que cambia de estado. La ruta de patrulla está trazada por fuera del circuito para evitarlo, pero al investigar o perseguir puede atascarse. Usar NavMesh requiere el paquete AI Navigation, que no está instalado y necesita permiso.
  - No hay volúmenes de sombra ni escondites: la única cobertura es la geometría y agacharse.
  - El oído no tiene en cuenta paredes: un ruido se oye igual con un muro en medio.
  - Ser alcanzado devuelve al punto de salida sin más: no hay pantalla, daño ni checkpoint propio.
  - Un solo guardia y solo en `Prototype`; `Z01_Relieve` no tiene.
  - La señal de estado usa color y tamaño, y el recuadro lo dice con texto; falta una señal no cromática sobre el propio guardia (§6.7).
  - Los estados Investigate y Search comparten comportamiento (ir al último punto y mirar alrededor).

- **Próximo paso más pequeño:** que Jorge juegue la escena `Prototype`: dejarse ver, esconderse tras la pared alta, pasar agachado por detrás del guardia y correr cerca de él, y diga qué se siente injusto o confuso. Después, siguiente corte de F1: parkour contextual mínimo, o decidir si se instala AI Navigation para que el guardia rodee obstáculos.
