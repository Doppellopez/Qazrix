## Incremento ART-17: matacanes en torres y edificios altos

- Objetivo/gate: poner unos pocos matacanes en algunos edificios (petición de Jorge, 2026-11-17).
- Cambios de código/escenas/assets:
  - `Scripts/Simulation/World/Machicolations.cs` (nuevo, lógica pura): `MachicolationPlanner` (reparto determinista: uno por torre en su lado recto más largo y más abierto a la calle; uno de cada 30 edificios altos; tope 30; margen de 0,8 m a las esquinas), `MachicolationPlanner.BlocksOpening` (tramo reservado) y `MachicolationMesher` (losa, ménsulas de dos escalones, pretil, almenas y recuadros oscuros).
  - `FootprintOpenings.Place`: acepta los matacanes y no pone huecos en su tramo.
  - `CityBlocks`: `WithMachicolations`, `MachicolationCount`, `Machicolations`; mallas hijas `Ciudad_Matacanes` (material del muro) y `Ciudad_Matacanes_Huecos` (material de los huecos), sin colisionador. `Z01GeoBuilder` los activa.
  - Tests: `MachicolationsTests` (EditMode, 7) y `Z01SceneTests.City_HasAFewMachicolations_VisualOnly_AndOneIsPhotographedUpClose` (PlayMode: cantidad, sin colisionador, tres fotos y medidas de posición).
- Comandos ejecutados (Unity 6000.3.6f1): tests EditMode y PlayMode, Z01GeoBuilder.BuildBatch y VerifyProject.RunBatch.
- Pruebas: VerifyProject 0 errores; EditMode 181/181; PlayMode 41/41.
- Resultado en Z01: 30 matacanes (todos en torres); malla de 11.520 vértices y 5.760 triángulos; 0 colisionadores nuevos.
- Build y plataforma: sin build nuevo.
- Evidencia: `Reports/QA/play_z01_matacan.png` (de perfil: vuelo, pretil y almenas contra el cielo), `play_z01_matacan_frente.png` (de cerca y desde abajo: ménsulas, cara inferior de la losa y almenas) y `play_z01_matacan_abajo.png` (desde la calle).
- Derechos/licencias de activos nuevos: ninguno.
- Decisiones tomadas: D66.
- Problemas conocidos: sin jugar a mano; en Z01 hay más de 30 torres y llenan el tope, así que ningún edificio alto lleva matacán; la primera versión era casi invisible (0,5 m de vuelo y misma piedra que la pared) y se agrandó; el matacán usa la textura del muro sin variación; la cara inferior de la losa toma la textura de planta y se ve como la del tejado; no hay chorreras ni huecos reales en el suelo (son recuadros oscuros).
- Próximo paso más pequeño: que Jorge lo vea en Unity (desde la calle y desde el tejado) y decir si quiere más, más grandes o repartidos también entre los edificios altos.

### Ajuste posterior: muchos más matacanes (petición de Jorge)

- `MachicolationRules`: tope 300 (antes 30), lado mínimo de edificio alto 4 m (antes 5), separación mínima 6 m entre matacanes (`MinSeparation`) y 6 de 12 puntos de prueba abiertos a la calle (`MinOpen`); desaparece `EveryNthTall`.
- `MachicolationPlanner.Place`: primero un matacán por torre (prioridad); después, en los edificios altos, como mucho uno por lado recto largo que dé a la calle, en orden fijo (oeste a este, sur a norte), a intervalos regulares por toda la lista y, si queda cupo, rellenando con el resto; nunca a menos de 6 m de otro. Sin azar. Cada matacán lleva su `EdgeIndex`.
- Tests: `MachicolationsTests` pasa a 10 (separación, uno por lado, tope, prioridad de las torres, paredes pegadas a otro edificio) y el de PlayMode exige entre 150 y 400, más de 50 en edificios altos y ≥ 6 m entre ellos.
- Resultado en Z01: **300 matacanes (49 en torres, 251 en edificios altos)**; malla de 115.200 vértices y 57.600 triángulos; ciudad entera 141.857 triángulos, 0,38 s en levantarse.
- Pruebas tras el cambio: VerifyProject 0 errores; PlayMode 41/41 en la pasada completa; EditMode 203/203 en la repetición.
- Capturas nuevas: `play_z01_matacanes_calle.png` (tres fachadas seguidas, una en diagonal, cada una con su matacán bajo la cornisa y las ventanas respetando el tramo) y `play_z01_matacan_abajo.png` (uno de cerca, con sombra, ménsulas y almenas).
