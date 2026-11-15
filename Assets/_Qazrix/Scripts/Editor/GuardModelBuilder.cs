using Qazrix.Prototype;
using UnityEngine;
using static Qazrix.EditorTools.CharacterKit;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Modelo del guardia de la ciudad: un hombre corpulento de la guardia andalusí, con turbante,
    /// túnica larga ceñida por una faja, calzones anchos y un sable curvo en la mano derecha.
    /// Primitivas de Unity y texturas generadas por código; sustituto hasta tener modelo esculpido.
    ///
    /// Criterio (spec §2.3): es un guardia concreto, no «el» andalusí. La corpulencia es un rasgo de
    /// este personaje y se representa con dignidad, sin caricatura. Otros guardias deben variar en
    /// complexión, edad e indumentaria. [RESEARCH] El sable curvo es un anacronismo probable para
    /// 1229 (las espadas almohades documentadas son sobre todo rectas): se mantiene por decisión de
    /// dirección y queda anotado para revisión histórica (D43).
    /// </summary>
    public static class GuardModelBuilder
    {
        public const string ArtFolder = "Assets/_Qazrix/Art/Characters/Guardia";

        /// <summary>Crea el modelo como hijo de <paramref name="guard"/> (pies en su origen) y devuelve su animador.</summary>
        public static CharacterWalkAnimator Build(Transform guard, CharacterController body)
        {
            Material robe = Textured(ArtFolder, "Guardia_Tunica", new Color(0.86f, 0.80f, 0.66f), Pattern.Weave, 5f);
            Material sash = Textured(ArtFolder, "Guardia_Faja", new Color(0.55f, 0.12f, 0.14f), Pattern.Weave, 3f);
            Material turban = Textured(ArtFolder, "Guardia_Turbante", new Color(0.93f, 0.91f, 0.84f), Pattern.Pleats, 1f);
            Material breeches = Textured(ArtFolder, "Guardia_Calzones", new Color(0.24f, 0.30f, 0.42f), Pattern.Pleats, 1f);
            Material leather = Textured(ArtFolder, "Guardia_Cuero", new Color(0.40f, 0.26f, 0.14f), Pattern.Leather, 2f);
            Material skin = Textured(ArtFolder, "Guardia_Piel", new Color(0.66f, 0.47f, 0.33f), Pattern.Plain, 1f);
            Material beard = Textured(ArtFolder, "Guardia_Barba", new Color(0.10f, 0.08f, 0.07f), Pattern.Leather, 3f);
            Material steel = Textured(ArtFolder, "Guardia_Acero", new Color(0.74f, 0.76f, 0.78f), Pattern.Plain, 1f, metal: true);
            Material brass = Textured(ArtFolder, "Guardia_Laton", new Color(0.78f, 0.60f, 0.22f), Pattern.Plain, 1f, metal: true);

            Transform model = Pivot("Guardia_modelo", guard, Vector3.zero);

            // --- Tronco: pecho ancho y vientre prominente bajo la túnica ---
            Part("Pecho", PrimitiveType.Capsule, model, new Vector3(0f, 1.30f, 0f), new Vector3(0.52f, 0.26f, 0.36f), robe);
            Part("Vientre", PrimitiveType.Sphere, model, new Vector3(0f, 1.06f, 0.05f), new Vector3(0.66f, 0.52f, 0.60f), robe);
            Part("Falda_tunica", PrimitiveType.Cylinder, model, new Vector3(0f, 0.72f, 0.01f), new Vector3(0.60f, 0.17f, 0.50f), robe);
            Part("Faja", PrimitiveType.Cylinder, model, new Vector3(0f, 0.98f, 0.03f), new Vector3(0.66f, 0.06f, 0.59f), sash);
            Part("Faja_nudo", PrimitiveType.Cube, model, new Vector3(0.22f, 0.88f, 0.20f), new Vector3(0.09f, 0.26f, 0.05f), sash, new Vector3(0f, 0f, -8f));

            // --- Cabeza: turbante de varias vueltas, barba cerrada ---
            Part("Cuello", PrimitiveType.Cylinder, model, new Vector3(0f, 1.55f, 0f), new Vector3(0.16f, 0.05f, 0.16f), skin);
            Part("Cabeza", PrimitiveType.Sphere, model, new Vector3(0f, 1.68f, 0.01f), new Vector3(0.25f, 0.26f, 0.25f), skin);
            Part("Papada", PrimitiveType.Sphere, model, new Vector3(0f, 1.59f, 0.05f), new Vector3(0.21f, 0.12f, 0.18f), skin);
            Part("Barba", PrimitiveType.Sphere, model, new Vector3(0f, 1.61f, 0.08f), new Vector3(0.20f, 0.15f, 0.13f), beard);
            Part("Bigote", PrimitiveType.Cube, model, new Vector3(0f, 1.665f, 0.125f), new Vector3(0.11f, 0.02f, 0.02f), beard);
            Part("Nariz", PrimitiveType.Cube, model, new Vector3(0f, 1.695f, 0.13f), new Vector3(0.035f, 0.055f, 0.045f), skin);
            Part("Turbante_base", PrimitiveType.Cylinder, model, new Vector3(0f, 1.79f, 0f), new Vector3(0.30f, 0.045f, 0.30f), turban);
            Part("Turbante_medio", PrimitiveType.Sphere, model, new Vector3(0f, 1.85f, -0.01f), new Vector3(0.33f, 0.16f, 0.33f), turban);
            Part("Turbante_alto", PrimitiveType.Sphere, model, new Vector3(0.01f, 1.92f, -0.01f), new Vector3(0.24f, 0.11f, 0.24f), turban);
            Part("Turbante_cola", PrimitiveType.Cube, model, new Vector3(-0.12f, 1.62f, -0.13f), new Vector3(0.09f, 0.30f, 0.025f), turban, new Vector3(-8f, 0f, 6f));

            // --- Piernas: calzones anchos y botas blandas ---
            Transform leftLeg = Leg("Pierna_izq", model, -0.15f, breeches, leather);
            Transform rightLeg = Leg("Pierna_der", model, 0.15f, breeches, leather);

            // --- Brazos ---
            Transform leftArm = Arm("Brazo_izq", model, -0.34f, robe, skin);
            Transform rightArm = Arm("Brazo_der", model, 0.34f, robe, skin);
            rightArm.localRotation = Quaternion.identity;
            BuildSabre(rightArm, steel, brass, leather);

            var animator = model.gameObject.AddComponent<CharacterWalkAnimator>();
            // El brazo derecho no se balancea: lleva el sable.
            animator.Configure(guard, body, leftLeg, rightLeg, leftArm, rightArm, rightArmSwings: false);
            return animator;
        }

        /// <summary>
        /// Sable curvo en la mano: la hoja se hace con cinco tramos, cada uno girado un poco más que el
        /// anterior, de modo que dibujan la curva. Apunta hacia delante y algo hacia abajo.
        /// </summary>
        private static void BuildSabre(Transform arm, Material steel, Material brass, Material leather)
        {
            Transform grip = Pivot("Sable", arm, new Vector3(0f, -0.60f, 0.03f), new Vector3(100f, 0f, 0f));
            Part("Empunadura", PrimitiveType.Cylinder, grip, new Vector3(0f, -0.02f, 0f), new Vector3(0.035f, 0.07f, 0.035f), leather);
            Part("Pomo", PrimitiveType.Sphere, grip, new Vector3(0f, -0.10f, 0f), new Vector3(0.055f, 0.055f, 0.055f), brass);
            Part("Guarda", PrimitiveType.Cube, grip, new Vector3(0f, 0.06f, 0f), new Vector3(0.03f, 0.025f, 0.17f), brass);

            Transform joint = Pivot("Hoja", grip, new Vector3(0f, 0.07f, 0f));
            const int segments = 5;
            const float length = 0.17f;
            for (int i = 0; i < segments; i++)
            {
                // Cada tramo se ensancha un poco hacia la punta, como en un sable, salvo el último.
                float width = i == segments - 1 ? 0.035f : 0.045f + 0.008f * i;
                Part($"Tramo_{i + 1}", PrimitiveType.Cube, joint, new Vector3(0f, length * 0.5f, 0f), new Vector3(0.012f, length * 1.04f, width), steel);
                joint = Pivot($"Curva_{i + 1}", joint, new Vector3(0f, length, 0f), new Vector3(-9f, 0f, 0f));
            }
        }

        private static Transform Leg(string name, Transform model, float x, Material breeches, Material leather)
        {
            Transform pivot = Pivot(name, model, new Vector3(x, 0.86f, 0f));
            Part("Calzon", PrimitiveType.Sphere, pivot, new Vector3(0f, -0.30f, 0f), new Vector3(0.34f, 0.62f, 0.36f), breeches);
            Part("Banda", PrimitiveType.Cylinder, pivot, new Vector3(0f, -0.58f, 0f), new Vector3(0.17f, 0.02f, 0.17f), leather);
            Part("Bota", PrimitiveType.Cylinder, pivot, new Vector3(0f, -0.70f, 0f), new Vector3(0.15f, 0.12f, 0.15f), leather);
            Part("Pie", PrimitiveType.Cube, pivot, new Vector3(0f, -0.82f, 0.07f), new Vector3(0.13f, 0.08f, 0.30f), leather);
            Part("Punta", PrimitiveType.Sphere, pivot, new Vector3(0f, -0.80f, 0.22f), new Vector3(0.11f, 0.09f, 0.10f), leather);
            return pivot;
        }

        private static Transform Arm(string name, Transform model, float x, Material sleeve, Material skin)
        {
            Transform pivot = Pivot(name, model, new Vector3(x, 1.46f, 0f));
            Part("Manga", PrimitiveType.Capsule, pivot, new Vector3(0f, -0.27f, 0f), new Vector3(0.17f, 0.29f, 0.17f), sleeve);
            Part("Mano", PrimitiveType.Sphere, pivot, new Vector3(0f, -0.59f, 0f), new Vector3(0.11f, 0.12f, 0.11f), skin);
            return pivot;
        }
    }
}
