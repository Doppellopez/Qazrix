using Qazrix.Gameplay;
using UnityEditor;
using UnityEngine;
using static Qazrix.EditorTools.CharacterKit;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Modelo de la mujer andalusí que espera en la X del mapa (el contacto de la misión, spec §4.2): esbelta,
    /// con túnica larga violeta de líneas curvas, faja dorada y un velo lila; sobre el rostro, una gasa translúcida.
    /// Primitivas de Unity y texturas generadas por código; sustituto hasta tener modelo esculpido.
    ///
    /// Criterio (spec §2.3): es una persona con aspecto cuidado y dignidad, no un tópico exótico ni un premio.
    /// Vestuario sobrio y cubierto; la gasa del rostro es translúcida y deja ver los rasgos. [RESEARCH] El color violeta y el corte de la túnica
    /// son de dirección artística, no verificados históricamente para 1229: queda anotado para revisión (D72).
    /// </summary>
    public static class ContactModelBuilder
    {
        public const string ArtFolder = "Assets/_Qazrix/Art/Characters/Contacto";

        /// <summary>Crea el personaje (pies en el origen, mirando a +Z) con su componente de colocación, desactivado.</summary>
        public static ObjectiveContact Build(Transform parent)
        {
            Material dress = Textured(ArtFolder, "Contacto_Tunica", new Color(0.44f, 0.22f, 0.66f), Pattern.Weave, 5f);
            Material pleats = Textured(ArtFolder, "Contacto_Pliegues", new Color(0.38f, 0.18f, 0.58f), Pattern.Pleats, 1f);
            Material veil = Textured(ArtFolder, "Contacto_Velo", new Color(0.74f, 0.64f, 0.86f), Pattern.Pleats, 1f);
            Material gold = Textured(ArtFolder, "Contacto_Oro", new Color(0.84f, 0.67f, 0.28f), Pattern.Plain, 1f, metal: true);
            Material skin = Textured(ArtFolder, "Contacto_Piel", new Color(0.72f, 0.54f, 0.40f), Pattern.Plain, 1f);
            Material dark = Textured(ArtFolder, "Contacto_Oscuro", new Color(0.07f, 0.05f, 0.05f), Pattern.Plain, 1f);
            Material lips = Textured(ArtFolder, "Contacto_Labios", new Color(0.62f, 0.30f, 0.30f), Pattern.Plain, 1f);
            Material leather = Textured(ArtFolder, "Contacto_Cuero", new Color(0.30f, 0.19f, 0.12f), Pattern.Leather, 2f);
            Material gauze = Translucent(Textured(ArtFolder, "Contacto_Gasa", new Color(0.80f, 0.70f, 0.92f), Pattern.Pleats, 1f), 0.42f);

            var root = new GameObject("Contacto");
            root.transform.SetParent(parent, false);
            Transform model = Pivot("Contacto_modelo", root.transform, Vector3.zero);

            // --- Túnica larga de líneas curvas: caderas llenas, cintura fina, falda en campana con ribete dorado ---
            Part("Falda_baja", PrimitiveType.Sphere, model, new Vector3(0f, 0.27f, 0f), new Vector3(0.46f, 0.52f, 0.42f), pleats);
            Part("Falda_media", PrimitiveType.Sphere, model, new Vector3(0f, 0.66f, 0f), new Vector3(0.36f, 0.60f, 0.31f), dress);
            Part("Caderas", PrimitiveType.Sphere, model, new Vector3(0f, 0.90f, 0f), new Vector3(0.35f, 0.26f, 0.27f), dress);
            Part("Ribete", PrimitiveType.Cylinder, model, new Vector3(0f, 0.12f, 0f), new Vector3(0.405f, 0.012f, 0.37f), gold);
            Part("Faja", PrimitiveType.Cylinder, model, new Vector3(0f, 1.00f, 0f), new Vector3(0.21f, 0.03f, 0.18f), gold);
            Part("Busto", PrimitiveType.Capsule, model, new Vector3(0f, 1.20f, 0f), new Vector3(0.29f, 0.20f, 0.20f), dress);
            Part("Pecho", PrimitiveType.Sphere, model, new Vector3(0f, 1.25f, 0.035f), new Vector3(0.26f, 0.15f, 0.20f), dress);
            Part("Escote_ribete", PrimitiveType.Cylinder, model, new Vector3(0f, 1.385f, 0f), new Vector3(0.17f, 0.012f, 0.13f), gold);
            Part("Hombro_izq", PrimitiveType.Sphere, model, new Vector3(-0.155f, 1.36f, 0f), new Vector3(0.105f, 0.10f, 0.105f), dress);
            Part("Hombro_der", PrimitiveType.Sphere, model, new Vector3(0.155f, 1.36f, 0f), new Vector3(0.105f, 0.10f, 0.105f), dress);

            // --- Cabeza: rostro suave, ojos oscuros, velo lila que enmarca la cara y cae por la espalda ---
            Part("Cuello", PrimitiveType.Cylinder, model, new Vector3(0f, 1.45f, 0f), new Vector3(0.085f, 0.05f, 0.085f), skin);
            Part("Cabeza", PrimitiveType.Sphere, model, new Vector3(0f, 1.56f, 0.01f), new Vector3(0.185f, 0.225f, 0.20f), skin);
            Part("Barbilla", PrimitiveType.Sphere, model, new Vector3(0f, 1.475f, 0.045f), new Vector3(0.10f, 0.08f, 0.10f), skin);
            Part("Nariz", PrimitiveType.Sphere, model, new Vector3(0f, 1.55f, 0.115f), new Vector3(0.03f, 0.045f, 0.04f), skin);
            Part("Boca", PrimitiveType.Sphere, model, new Vector3(0f, 1.505f, 0.10f), new Vector3(0.05f, 0.016f, 0.02f), lips);
            foreach (float side in new[] { -1f, 1f })
            {
                Part("Ojo", PrimitiveType.Sphere, model, new Vector3(side * 0.04f, 1.585f, 0.095f), new Vector3(0.026f, 0.020f, 0.016f), dark);
                Part("Ceja", PrimitiveType.Cube, model, new Vector3(side * 0.042f, 1.612f, 0.096f), new Vector3(0.05f, 0.008f, 0.01f), dark, new Vector3(0f, 0f, side * -6f));
            }
            Part("Cabello_flequillo", PrimitiveType.Sphere, model, new Vector3(0f, 1.655f, 0.045f), new Vector3(0.15f, 0.03f, 0.12f), dark);
            Part("Velo_cabeza", PrimitiveType.Sphere, model, new Vector3(0f, 1.585f, -0.03f), new Vector3(0.255f, 0.27f, 0.25f), veil);
            Part("Velo_borde", PrimitiveType.Cylinder, model, new Vector3(0f, 1.655f, 0.03f), new Vector3(0.19f, 0.012f, 0.18f), gold, new Vector3(-10f, 0f, 0f));
            Part("Velo_hombros", PrimitiveType.Capsule, model, new Vector3(0f, 1.40f, -0.01f), new Vector3(0.11f, 0.21f, 0.12f), veil, new Vector3(0f, 0f, 90f));
            // Velo fino y translúcido que cubre el rostro: se ven los rasgos a través de la gasa.
            Transform faceVeil = Part("Velo_cara", PrimitiveType.Sphere, model, new Vector3(0f, 1.565f, 0.025f), new Vector3(0.215f, 0.255f, 0.235f), gauze);
            Transform faceDrape = Part("Velo_cara_caida", PrimitiveType.Cube, model, new Vector3(0f, 1.455f, 0.085f), new Vector3(0.17f, 0.11f, 0.012f), gauze, new Vector3(-12f, 0f, 0f));
            foreach (Transform t in new[] { faceVeil, faceDrape }) t.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Part("Velo_caida", PrimitiveType.Cube, model, new Vector3(0f, 1.05f, -0.15f), new Vector3(0.27f, 0.74f, 0.025f), veil, new Vector3(-3f, 0f, 0f));

            // --- Brazos finos con mangas largas y manos ---
            foreach (float side in new[] { -1f, 1f })
            {
                Transform arm = Pivot(side < 0f ? "Brazo_izq" : "Brazo_der", model, new Vector3(side * 0.165f, 1.37f, 0f), new Vector3(0f, 0f, side * -4f));
                Part("Manga", PrimitiveType.Capsule, arm, new Vector3(0f, -0.23f, 0f), new Vector3(0.095f, 0.245f, 0.095f), dress);
                Part("Puno", PrimitiveType.Cylinder, arm, new Vector3(0f, -0.455f, 0f), new Vector3(0.075f, 0.012f, 0.075f), gold);
                Part("Mano", PrimitiveType.Sphere, arm, new Vector3(0f, -0.50f, 0f), new Vector3(0.065f, 0.095f, 0.05f), skin);
            }

            // --- Zapatillas bajo el borde de la túnica ---
            foreach (float side in new[] { -1f, 1f })
                Part("Zapatilla", PrimitiveType.Cube, model, new Vector3(side * 0.08f, 0.03f, 0.13f), new Vector3(0.09f, 0.05f, 0.18f), leather);

            // Cuerpo físico: el jugador no la atraviesa.
            var body = root.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0f, 0.85f, 0f);
            body.radius = 0.3f;
            body.height = 1.7f;

            var contact = root.AddComponent<ObjectiveContact>();
            root.SetActive(false); // aparece cuando el mapa muestra la X
            return contact;
        }

        /// <summary>Convierte un material en translúcido (URP Lit, superficie transparente) con la opacidad indicada.</summary>
        private static Material Translucent(Material material, float alpha)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_SrcBlendAlpha")) material.SetFloat("_SrcBlendAlpha", 1f);
            if (material.HasProperty("_DstBlendAlpha")) material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_BlendModePreserveSpecular")) material.SetFloat("_BlendModePreserveSpecular", 0f); // mezcla alfa simple, sin premultiplicar
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);
            Color c = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
            c.a = alpha;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", c);
            if (material.HasProperty("_Color")) material.SetColor("_Color", c);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}