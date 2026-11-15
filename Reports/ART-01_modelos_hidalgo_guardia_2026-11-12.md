## Incremento ART-01: modelos del hidalgo (jugador) y del guardia

- **Objetivo/gate:** peticiones directas de Jorge: que el jugador sea un hidalgo medieval con pantalones anchos y texturas, y que el guardia parezca un árabe medieval gordo con cimitarra. Spec §7.3: empezar con modelos temporales y sustituirlos después. No avanza ningún gate por sí mismo.

- **Cambios de código/escenas/assets:**
  - `Scripts/Editor/CharacterKit.cs`: piezas comunes para construir personajes con primitivas de Unity y texturas de 128 px generadas por código (trama de tela, pliegues, cuero, liso).
  - `Scripts/Editor/HidalgoBuilder.cs`: protagonista con sayo rojo, capa corta verde, bonete azul, barba, cinturón con hebilla, espada recta al cinto, **calzones anchos** de pliegues recogidos bajo la rodilla y botas. 31 piezas.
  - `Scripts/Editor/GuardModelBuilder.cs`: guardia andalusí corpulento, con turbante de varias vueltas, túnica larga ceñida por una faja roja, calzones anchos, botas y **sable curvo** en la mano derecha (hoja de cinco tramos que dibujan la curva).
  - `Scripts/Simulation/Movement/WalkCycle.cs` (lógica pura) y `Scripts/Runtime/Prototype/CharacterWalkAnimator.cs`: piernas y brazos se balancean según la velocidad; pose de aire al saltar o trepar. Sirve para jugador y guardia porque mide el desplazamiento, no depende de quién lo mueva. El brazo del sable no se balancea.
  - `PrototypeSceneBuilder`: sustituye las dos cápsulas por los modelos. Escenas `Prototype` y `Z01_Relieve` regeneradas.
  - Texturas y materiales en `Assets/_Qazrix/Art/Characters/Hidalgo` y `/Guardia`.

- **Comandos ejecutados** (Unity 6000.3.6f1 sobre `C:\Users\Propietario\Qazrix`): `PrototypeSceneBuilder.RebuildBatch`, `Z01GeoBuilder.BuildBatch`, `PrototypePreview.CaptureBatch`, `VerifyProject.RunBatch`, tests EditMode y PlayMode. Todos con salida 0 a la primera.

- **Pruebas:**
  - Aprobadas: compilación; VerifyProject 0 errores, 1 aviso (pipeline provisional), incluida la comprobación de que ninguna textura queda sin fila de licencia; EditMode 69/69 (5 nuevos del ciclo de andar); PlayMode 25/25 (3 nuevos).
  - Pruebas en Play nuevas: el jugador tiene más de 25 piezas, todas con textura, los calzones miden casi el doble de ancho que la bota, lleva espada, mide 1,8 m y tiene los pies en el suelo; al andar, las piernas se balancean y lo hacen una contraria a la otra; el guardia tiene todas sus piezas con textura, lleva turbante y sable, la hoja es curva y es más ancho que el hidalgo.
  - No ejecutadas: valoración visual de Jorge; Play a mano; rendimiento.

- **Build y plataforma:** el build de Windows no está regenerado con estos modelos.

- **Evidencia:** `Reports/QA/personaje_hidalgo.png`, `Reports/QA/personaje_guardia.png`, `Reports/QA/play_hidalgo.png`, `playmode-results.xml`, `editmode-results.xml`.

- **Derechos/licencias de activos nuevos:** GEN-002 en `ASSET_LICENSES.csv`: texturas y materiales generados por código propio, sin activos externos.

- **Decisiones tomadas:** D43.

- **Problemas conocidos:**
  - **Son figuras de primitivas, no modelos esculpidos.** Se reconoce qué son, pero tienen aspecto de muñeco: sin cara, sin manos articuladas, sin rodillas ni codos. Un modelo de calidad necesita trabajo de arte 3D (Blender), que la spec señala como la carga principal del proyecto.
  - Al agacharse, el hidalgo se aplasta en vertical en lugar de adoptar una postura.
  - Sin animación de trepar, saltar obstáculos ni blandir el sable.
  - **Rigor histórico sin revisar [RESEARCH]:** el sable curvo es un anacronismo probable para 1229 (las espadas almohades documentadas son sobre todo rectas); el bonete, el sayo y los calzones anchos del hidalgo tampoco están contrastados con fuentes del siglo XIII. Queda para la revisión histórica.
  - **Sensibilidad cultural (spec §2.3):** el guardia es un individuo concreto, representado sin caricatura; si se añaden más guardias deben variar en complexión, edad e indumentaria para no convertir un rasgo en estereotipo.
  - El cuerpo físico del guardia sigue siendo una cápsula de 0,3 m de radio: el vientre sobresale un poco de ella.

- **Próximo paso más pequeño:** que Jorge mire los dos personajes en Play y diga qué cambiar (colores, proporciones, prendas). Para un salto real de calidad, decidir quién hará el modelado 3D (pregunta abierta de spec §17).
