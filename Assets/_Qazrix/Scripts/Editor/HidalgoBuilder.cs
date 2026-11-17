using Qazrix.Prototype;
using Qazrix.Simulation.Movement;
using UnityEngine;
using static Qazrix.EditorTools.CharacterKit;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Construye el modelo del protagonista: un hidalgo con sayo abotonado, golilla de lino, capa corta,
    /// bonete, espada y calzones anchos recogidos bajo la rodilla. Está hecho con primitivas de Unity y
    /// texturas generadas por código (tela, pliegues, cuero, piel): es un sustituto con licencia propia
    /// hasta que haya un modelo esculpido (spec §7.3). Medidas en metros, pies en el origen, mirando a +Z.
    /// Un solo pivote por extremidad: sin rodillas ni codos.
    /// </summary>
    public static class HidalgoBuilder
    {
        public const string ArtFolder = "Assets/_Qazrix/Art/Characters/Hidalgo";

        /// <summary>Crea el modelo dentro de <paramref name="visual"/> y devuelve su animador.</summary>
        /// <param name="visual">
        /// Transform «Visual» del jugador. El controlador lo escala como a una cápsula de Unity (2 m de
        /// alto, centrada), así que el modelo cuelga de un nodo que deshace esa escala.
        /// </param>
        public static CharacterWalkAnimator Build(Transform visual, Transform characterRoot, CharacterController body)
        {
            Material tunic = Textured(ArtFolder, "Hidalgo_Sayo", new Color(0.52f, 0.13f, 0.12f), Pattern.Weave, 4f);
            Material breeches = Textured(ArtFolder, "Hidalgo_Calzones", new Color(0.80f, 0.69f, 0.47f), Pattern.Pleats, 1f);
            Material cape = Textured(ArtFolder, "Hidalgo_Capa", new Color(0.16f, 0.27f, 0.20f), Pattern.Weave, 5f);
            Material leather = Textured(ArtFolder, "Hidalgo_Cuero", new Color(0.33f, 0.21f, 0.12f), Pattern.Leather, 2f);
            Material skin = Textured(ArtFolder, "Hidalgo_Piel", new Color(0.82f, 0.62f, 0.48f), Pattern.Plain, 1f);
            Material hat = Textured(ArtFolder, "Hidalgo_Bonete", new Color(0.14f, 0.16f, 0.30f), Pattern.Weave, 3f);
            Material steel = Textured(ArtFolder, "Hidalgo_Acero", new Color(0.70f, 0.72f, 0.75f), Pattern.Plain, 1f, metal: true);
            Material hair = Textured(ArtFolder, "Hidalgo_Pelo", new Color(0.18f, 0.12f, 0.08f), Pattern.Leather, 3f);
            Material linen = Textured(ArtFolder, "Hidalgo_Lino", new Color(0.90f, 0.88f, 0.80f), Pattern.Weave, 6f);
            Material dark = Textured(ArtFolder, "Hidalgo_Oscuro", new Color(0.07f, 0.05f, 0.04f), Pattern.Plain, 1f);

            // Deshace la escala de cápsula del nodo Visual: a partir de aquí, 1 unidad = 1 metro.
            Vector3 s = visual.localScale;
            Transform model = Pivot("Hidalgo", visual, new Vector3(0f, -1f, 0f)); // el centro de la cápsula está a 1 unidad de sus pies
            model.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);

            // --- Tronco: pecho ancho, cintura más estrecha y hombros redondeados ---
            Part("Sayo", PrimitiveType.Capsule, model, new Vector3(0f, 1.27f, 0f), new Vector3(0.42f, 0.26f, 0.27f), tunic);
            Part("Cintura", PrimitiveType.Capsule, model, new Vector3(0f, 1.09f, 0f), new Vector3(0.34f, 0.15f, 0.23f), tunic);
            Part("Faldon", PrimitiveType.Cylinder, model, new Vector3(0f, 0.98f, 0f), new Vector3(0.50f, 0.11f, 0.35f), tunic);
            Part("Cinturon", PrimitiveType.Cylinder, model, new Vector3(0f, 1.05f, 0f), new Vector3(0.46f, 0.025f, 0.31f), leather);
            Part("Hebilla", PrimitiveType.Cube, model, new Vector3(0f, 1.05f, 0.155f), new Vector3(0.07f, 0.06f, 0.02f), steel);
            Part("Cadera", PrimitiveType.Sphere, model, new Vector3(0f, 0.90f, 0f), new Vector3(0.52f, 0.30f, 0.38f), breeches);
            Part("Hombro_izq", PrimitiveType.Sphere, model, new Vector3(-0.235f, 1.43f, 0f), new Vector3(0.16f, 0.15f, 0.16f), tunic);
            Part("Hombro_der", PrimitiveType.Sphere, model, new Vector3(0.235f, 1.43f, 0f), new Vector3(0.16f, 0.15f, 0.16f), tunic);

            // Tres botones de latón en el pecho y una bolsa de cuero colgada del cinto.
            for (int i = 0; i < 3; i++)
            {
                float y = 1.38f - i * 0.075f;
                Part("Boton_" + (i + 1), PrimitiveType.Sphere, model, new Vector3(0f, y, 0.14f), new Vector3(0.024f, 0.024f, 0.016f), steel);
            }
            Part("Bolsa", PrimitiveType.Sphere, model, new Vector3(0.20f, 0.97f, 0.10f), new Vector3(0.10f, 0.12f, 0.06f), leather);
            Part("Bolsa_solapa", PrimitiveType.Cube, model, new Vector3(0.20f, 1.02f, 0.128f), new Vector3(0.09f, 0.035f, 0.012f), leather);

            // --- Cabeza ---
            Part("Cuello", PrimitiveType.Cylinder, model, new Vector3(0f, 1.52f, 0f), new Vector3(0.11f, 0.05f, 0.11f), skin);
            Part("Golilla", PrimitiveType.Cylinder, model, new Vector3(0f, 1.52f, 0f), new Vector3(0.19f, 0.012f, 0.19f), linen);
            Part("Golilla_borde", PrimitiveType.Cylinder, model, new Vector3(0f, 1.535f, 0f), new Vector3(0.165f, 0.008f, 0.165f), linen);
            Part("Cabeza", PrimitiveType.Sphere, model, new Vector3(0f, 1.65f, 0.01f), new Vector3(0.21f, 0.24f, 0.22f), skin);
            Part("Pelo", PrimitiveType.Sphere, model, new Vector3(0f, 1.66f, -0.03f), new Vector3(0.23f, 0.24f, 0.21f), hair);
            Part("Pelo_nuca", PrimitiveType.Sphere, model, new Vector3(0f, 1.60f, -0.07f), new Vector3(0.20f, 0.16f, 0.14f), hair);
            Part("Barba", PrimitiveType.Sphere, model, new Vector3(0f, 1.57f, 0.07f), new Vector3(0.15f, 0.10f, 0.10f), hair);
            Part("Bigote", PrimitiveType.Sphere, model, new Vector3(0f, 1.605f, 0.105f), new Vector3(0.075f, 0.022f, 0.035f), hair);
            Part("Nariz", PrimitiveType.Cube, model, new Vector3(0f, 1.64f, 0.115f), new Vector3(0.03f, 0.05f, 0.04f), skin, new Vector3(-12f, 0f, 0f));
            Part("Ojo_izq", PrimitiveType.Sphere, model, new Vector3(-0.045f, 1.67f, 0.102f), new Vector3(0.022f, 0.022f, 0.022f), dark);
            Part("Ojo_der", PrimitiveType.Sphere, model, new Vector3(0.045f, 1.67f, 0.102f), new Vector3(0.022f, 0.022f, 0.022f), dark);
            Part("Ceja_izq", PrimitiveType.Cube, model, new Vector3(-0.045f, 1.70f, 0.097f), new Vector3(0.055f, 0.012f, 0.014f), hair, new Vector3(0f, 0f, -6f));
            Part("Ceja_der", PrimitiveType.Cube, model, new Vector3(0.045f, 1.70f, 0.097f), new Vector3(0.055f, 0.012f, 0.014f), hair, new Vector3(0f, 0f, 6f));
            Part("Oreja_izq", PrimitiveType.Sphere, model, new Vector3(-0.107f, 1.645f, 0.005f), new Vector3(0.030f, 0.060f, 0.040f), skin);
            Part("Oreja_der", PrimitiveType.Sphere, model, new Vector3(0.107f, 1.645f, 0.005f), new Vector3(0.030f, 0.060f, 0.040f), skin);
            Part("Bonete", PrimitiveType.Sphere, model, new Vector3(0.02f, 1.775f, -0.01f), new Vector3(0.29f, 0.10f, 0.29f), hat);
            Part("Bonete_borde", PrimitiveType.Cylinder, model, new Vector3(0f, 1.74f, 0f), new Vector3(0.24f, 0.02f, 0.24f), hat);
            Part("Bonete_cinta", PrimitiveType.Cylinder, model, new Vector3(0f, 1.755f, 0f), new Vector3(0.245f, 0.010f, 0.245f), leather);

            // --- Capa corta a la espalda, con dos pliegues y un broche al cuello ---
            Part("Capa_hombros", PrimitiveType.Cube, model, new Vector3(0f, 1.455f, -0.065f), new Vector3(0.52f, 0.07f, 0.24f), cape);
            // Cuelga de tres tramos articulados (cada uno hijo del anterior) que mueve CapeAnimator con un
            // resorte por tramo: al andar, saltar y caer la capa se separa, ondea y se columpia.
            var capeSegments = new Transform[CapeSim.Segments];
            Transform capeParent = model;
            for (int i = 0; i < CapeSim.Segments; i++)
            {
                const float segmentHeight = CapeSim.SegmentLength;
                // z = -0,222: la cara interior de la tela (z + 0,015 más el pliegue de 0,008) queda por detrás de la
                // cadera (atrás en -0,19), para que ni en reposo ni al doblarse el cuerpo asome por la capa.
                Vector3 pivotAt = i == 0 ? new Vector3(0f, CapeMeshLayout.PivotY, CapeMeshLayout.PivotZ) : new Vector3(0f, -segmentHeight, 0f);
                Transform segment = Pivot(i == 0 ? "Capa_articulacion_1" : $"Capa_articulacion_{i + 1}", capeParent, pivotAt, new Vector3(CapeSim.RestLift, 0f, 0f));
                capeSegments[i] = segment;
                capeParent = segment;
            }

            // La tela es una sola malla continua (CapeSkin) que sale de los hombros (el canesú cruza su cresta) y sigue a los
            // tramos por la espalda: sin piezas sueltas ni escalones.
            // Se crea al empezar la partida; en la escena solo queda este objeto con su material.
            var skinObject = new GameObject("Capa_malla");
            skinObject.transform.SetParent(model, false);
            skinObject.AddComponent<CapeSkin>().Configure(capeSegments, cape);
            skinObject.GetComponent<MeshRenderer>().sharedMaterial = cape;
            Part("Broche", PrimitiveType.Sphere, model, new Vector3(0f, 1.465f, 0.125f), new Vector3(0.04f, 0.04f, 0.025f), steel);

            // --- Espada recta al cinto, en el costado izquierdo ---
            Transform sword = Pivot("Espada", model, new Vector3(-0.27f, 1.02f, -0.02f), new Vector3(25f, 0f, 0f));
            Part("Vaina", PrimitiveType.Cube, sword, new Vector3(0f, -0.36f, 0f), new Vector3(0.035f, 0.72f, 0.05f), leather);
            Part("Guarda", PrimitiveType.Cube, sword, new Vector3(0f, 0.01f, 0f), new Vector3(0.04f, 0.03f, 0.16f), steel);
            Part("Empunadura", PrimitiveType.Cylinder, sword, new Vector3(0f, 0.08f, 0f), new Vector3(0.03f, 0.06f, 0.03f), leather);
            Part("Pomo", PrimitiveType.Sphere, sword, new Vector3(0f, 0.15f, 0f), new Vector3(0.05f, 0.05f, 0.05f), steel);

            // --- Piernas: pivote en la cadera, calzón ancho hasta bajo la rodilla y bota ---
            Transform leftLeg = Leg("Pierna_izq", model, -0.115f, breeches, leather, dark);
            Transform rightLeg = Leg("Pierna_der", model, 0.115f, breeches, leather, dark);

            // La capa conoce las piernas: si una se echa hacia atrás (salto, trepada), la capa se aparta.
            visual.gameObject.AddComponent<CapeAnimator>().Configure(characterRoot, capeSegments, leftLeg, rightLeg);

            // --- Brazos: pivote en el hombro ---
            Transform leftArm = Arm("Brazo_izq", model, -0.26f, tunic, skin, linen);
            Transform rightArm = Arm("Brazo_der", model, 0.26f, tunic, skin, linen);

            var animator = visual.gameObject.AddComponent<CharacterWalkAnimator>();
            animator.Configure(characterRoot, body, leftLeg, rightLeg, leftArm, rightArm);
            return animator;
        }

        private static Transform Leg(string name, Transform model, float x, Material breeches, Material leather, Material sole)
        {
            Transform pivot = Pivot(name, model, new Vector3(x, 0.90f, 0f));
            // El calzón es lo más ancho del personaje: 30 cm por pierna, recogido en una banda bajo la rodilla.
            Part("Calzon", PrimitiveType.Sphere, pivot, new Vector3(0f, -0.26f, 0f), new Vector3(0.30f, 0.56f, 0.32f), breeches);
            Part("Banda", PrimitiveType.Cylinder, pivot, new Vector3(0f, -0.53f, 0f), new Vector3(0.15f, 0.02f, 0.15f), leather);
            Part("Canya", PrimitiveType.Cylinder, pivot, new Vector3(0f, -0.70f, 0f), new Vector3(0.13f, 0.16f, 0.13f), leather);
            // Borde doblado de la bota, algo más ancho que la caña.
            Part("Bota_borde", PrimitiveType.Cylinder, pivot, new Vector3(0f, -0.605f, 0f), new Vector3(0.165f, 0.025f, 0.165f), leather);
            // Pie como cápsula tumbada (puntera redonda) sobre una suela fina; el fondo queda en y = -0,90.
            Part("Pie", PrimitiveType.Capsule, pivot, new Vector3(0f, -0.845f, 0.055f), new Vector3(0.10f, 0.125f, 0.10f), leather, new Vector3(90f, 0f, 0f));
            Part("Suela", PrimitiveType.Cube, pivot, new Vector3(0f, -0.89f, 0.06f), new Vector3(0.105f, 0.02f, 0.27f), sole);
            return pivot;
        }

        private static Transform Arm(string name, Transform model, float x, Material sleeve, Material skin, Material linen)
        {
            Transform pivot = Pivot(name, model, new Vector3(x, 1.43f, 0f));
            // Manga abullonada arriba, antebrazo más ceñido, puño de lino y mano aplanada con pulgar.
            Part("Manga", PrimitiveType.Capsule, pivot, new Vector3(0f, -0.15f, 0f), new Vector3(0.14f, 0.17f, 0.14f), sleeve);
            Part("Antebrazo", PrimitiveType.Capsule, pivot, new Vector3(0f, -0.38f, 0f), new Vector3(0.10f, 0.15f, 0.10f), sleeve);
            Part("Puno", PrimitiveType.Cylinder, pivot, new Vector3(0f, -0.525f, 0f), new Vector3(0.11f, 0.014f, 0.11f), linen);
            Part("Mano", PrimitiveType.Sphere, pivot, new Vector3(0f, -0.60f, 0f), new Vector3(0.085f, 0.11f, 0.055f), skin);
            Part("Pulgar", PrimitiveType.Sphere, pivot, new Vector3(Mathf.Sign(x) * -0.03f, -0.585f, 0.025f), new Vector3(0.028f, 0.05f, 0.028f), skin);
            return pivot;
        }
    }
}
