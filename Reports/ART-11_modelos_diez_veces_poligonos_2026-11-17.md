## Incremento ART-11: personajes con diez veces los polígonos

- Objetivo/gate: que los modelos del hidalgo y del guardia tengan el décuple de polígonos (petición de Jorge, 2026-11-17).
- Cambios de código/escenas/assets:
  - `Scripts/Simulation/World/PrimitiveGeometry.cs` (nuevo, lógica pura): esfera, cápsula, cilindro y caja con el mismo tamaño que las primitivas de Unity y más subdivisiones; triángulos siempre orientados hacia fuera.
  - `CharacterKit.Part` deja de usar `CreatePrimitive`: usa `CharacterKit.MeshFor`, que guarda las mallas en `Assets/_Qazrix/Art/Characters/Meshes/Qazrix_*.asset` (cubiertas por GEN-002).
  - Escenas Prototype y Z01 regeneradas (los dos personajes y la capa del hidalgo usan las mallas nuevas).
  - Tests: `PrimitiveGeometryTests` (EditMode: ≥10× triángulos, mismo tamaño, caras hacia fuera, normales y UV válidas) y `CharacterModelTests.Characters_HaveTenTimesThePolygonsOfUnityPrimitives` (PlayMode).
- Comandos ejecutados (Unity 6000.3.6f1): RebuildBatch de Prototype, Z01GeoBuilder.BuildBatch, VerifyProject.RunBatch, tests EditMode y PlayMode (dos pasadas).
- Pruebas: aprobadas 131/131 EditMode y 37/37 PlayMode; VerifyProject 0 errores. La primera pasada falló 4 pruebas mías (cilindro con 768 triángulos frente a 800 exigidos, y la referencia de tamaño que usaba la malla equivocada de Unity); corregido y repetido.
- Resultado: esfera 768→8.256 (10,8×), cápsula 832→13.312 (16×), cilindro 80→960 (12×), cubo 12→192 (16×); hidalgo 25.868→331.904 triángulos (12,8×), guardia 12.754→158.208 (12,4×).
- Build y plataforma: sin build nuevo.
- Evidencia: `Reports/QA/play_hidalgo_alta_resolucion.png` (de espaldas: la capa tapa casi todo; no sirve para juzgar el redondeo).
- Derechos/licencias de activos nuevos: mallas generadas por código propio, dentro de la fila GEN-002.
- Decisiones tomadas: D60.
- Problemas conocidos: sin jugar a mano; el cubo solo se subdivide (caras planas); unos 490.000 triángulos entre los dos personajes (no medido el efecto en fps); el brazo, la espada y las demás piezas no cambian de forma, solo se ven más lisas.
- Próximo paso más pequeño: que Jorge lo vea en Unity y diga si nota el redondeo; si va justo de fps, reducir las subdivisiones de las piezas pequeñas (botones, ojos).
