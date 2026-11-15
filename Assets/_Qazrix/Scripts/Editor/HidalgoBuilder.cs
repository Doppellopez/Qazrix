using Qazrix.Prototype;
using UnityEngine;
using static Qazrix.EditorTools.CharacterKit;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Construye el modelo del protagonista: un hidalgo con sayo, capa corta, bonete, espada y
    /// calzones anchos recogidos bajo la rodilla. Está hecho con primitivas de Unity y texturas
    /// generadas por código (tela, pliegues, cuero, piel): es un sustituto con licencia propia hasta
    /// que haya un modelo esculpido (spec §7.3). Medidas en metros, pies en el origen, mirando a +Z.
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

            // Deshace la escala de cápsula del nodo Visual: a partir de aquí, 1 unidad = 1 metro.
            Vector3 s = visual.localScale;
            Transform model = Pivot("Hidalgo", visual, new Vector3(0f, -1f, 0f)); // el centro de la cápsula está a 1 unidad de sus pies
            model.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);

            // --- Tronco ---
            Part("Sayo", PrimitiveType.Capsule, model, new Vector3(0f, 1.24f, 0f), new Vector3(0.42f, 0.30f, 0.27f), tunic);
            Part("Faldon", PrimitiveType.Cylinder, model, new Vector3(0f, 0.98f, 0f), new Vector3(0.50f, 0.11f, 0.35f), tunic);
            Part("Cinturon", PrimitiveType.Cylinder, model, new Vector3(0f, 1.05f, 0f), new Vector3(0.46f, 0.025f, 0.31f), leather);
            Part("Hebilla", PrimitiveType.Cube, model, new Vector3(0f, 1.05f, 0.155f), new Vector3(0.07f, 0.06f, 0.02f), steel);
            Part("Cadera", PrimitiveType.Sphere, model, new Vector3(0f, 0.90f, 0f), new Vector3(0.52f, 0.30f, 0.38f), breeches);

            // --- Cabeza ---
            Part("Cuello", PrimitiveType.Cylinder, model, new Vector3(0f, 1.52f, 0f), new Vector3(0.11f, 0.05f, 0.11f), skin);
            Part("Cabeza", PrimitiveType.Sphere, model, new Vector3(0f, 1.65f, 0.01f), new Vector3(0.21f, 0.24f, 0.22f), skin);
            Part("Pelo", PrimitiveType.Sphere, model, new Vector3(0f, 1.66f, -0.03f), new Vector3(0.23f, 0.24f, 0.21f), hair);
            Part("Barba", PrimitiveType.Sphere, model, new Vector3(0f, 1.57f, 0.07f), new Vector3(0.15f, 0.10f, 0.10f), hair);
            Part("Nariz", PrimitiveType.Cube, model, new Vector3(0f, 1.64f, 0.115f), new Vector3(0.03f, 0.05f, 0.04f), skin);
            Part("Bonete", PrimitiveType.Sphere, model, new Vector3(0.02f, 1.775f, -0.01f), new Vector3(0.29f, 0.10f, 0.29f), hat);
            Part("Bonete_borde", PrimitiveType.Cylinder, model, new Vector3(0f, 1.74f, 0f), new Vector3(0.24f, 0.02f, 0.24f), hat);

            // --- Capa corta a la espalda ---
            Part("Capa_hombros", PrimitiveType.Cube, model, new Vector3(0f, 1.46f, -0.02f), new Vector3(0.52f, 0.07f, 0.30f), cape);
            Part("Capa", PrimitiveType.Cube, model, new Vector3(0f, 1.14f, -0.17f), new Vector3(0.48f, 0.66f, 0.04f), cape, new Vector3(6f, 0f, 0f));

            // --- Espada recta al cinto, en el costado izquierdo ---
            Transform sword = Pivot("Espada", model, new Vector3(-0.27f, 1.02f, -0.02f), new Vector3(25f, 0f, 0f));
            Part("Vaina", PrimitiveType.Cube, sword, new Vector3(0f, -0.36f, 0f), new Vector3(0.035f, 0.72f, 0.05f), leather);
            Part("Guarda", PrimitiveType.Cube, sword, new Vector3(0f, 0.01f, 0f), new Vector3(0.04f, 0.03f, 0.16f), steel);
            Part("Empunadura", PrimitiveType.Cylinder, sword, new Vector3(0f, 0.08f, 0f), new Vector3(0.03f, 0.06f, 0.03f), leather);
            Part("Pomo", PrimitiveType.Sphere, sword, new Vector3(0f, 0.15f, 0f), new Vector3(0.05f, 0.05f, 0.05f), steel);

            // --- Piernas: pivote en la cadera, calzón ancho hasta bajo la rodilla y bota ---
            Transform leftLeg = Leg("Pierna_izq", model, -0.115f, breeches, leather);
            Transform rightLeg = Leg("Pierna_der", model, 0.115f, breeches, leather);

            // --- Brazos: pivote en el hombro ---
            Transform leftArm = Arm("Brazo_izq", model, -0.26f, tunic, skin);
            Transform rightArm = Arm("Brazo_der", model, 0.26f, tunic, skin);

            var animator = visual.gameObject.AddComponent<CharacterWalkAnimator>();
            animator.Configure(characterRoot, body, leftLeg, rightLeg, leftArm, rightArm);
            return animator;
        }

        private static Transform Leg(string name, Transform model, float x, Material breeches, Material leather)
        {
            Transform pivot = Pivot(name, model, new Vector3(x, 0.90f, 0f));
            // El calzón es lo más ancho del personaje: 30 cm por pierna, recogido en una banda bajo la rodilla.
            Part("Calzon", PrimitiveType.Sphere, pivot, new Vector3(0f, -0.26f, 0f), new Vector3(0.30f, 0.56f, 0.32f), breeches);
            Part("Banda", PrimitiveType.Cylinder, pivot, new Vector3(0f, -0.53f, 0f), new Vector3(0.15f, 0.02f, 0.15f), leather);
            Part("Canya", PrimitiveType.Cylinder, pivot, new Vector3(0f, -0.70f, 0f), new Vector3(0.13f, 0.16f, 0.13f), leather);
            Part("Pie", PrimitiveType.Cube, pivot, new Vector3(0f, -0.86f, 0.06f), new Vector3(0.11f, 0.08f, 0.27f), leather);
            return pivot;
        }

        private static Transform Arm(string name, Transform model, float x, Material sleeve, Material skin)
        {
            Transform pivot = Pivot(name, model, new Vector3(x, 1.43f, 0f));
            Part("Manga", PrimitiveType.Capsule, pivot, new Vector3(0f, -0.27f, 0f), new Vector3(0.12f, 0.29f, 0.12f), sleeve);
            Part("Mano", PrimitiveType.Sphere, pivot, new Vector3(0f, -0.59f, 0f), new Vector3(0.09f, 0.11f, 0.09f), skin);
            return pivot;
        }
    }
}
