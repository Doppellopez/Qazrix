using UnityEngine;

namespace Qazrix.Gameplay
{
    /// <summary>La llave del juego como objeto 3D: unos 12 cm de anilla, vástago y dos dientes, de primitivas, sin colisión. La hoja apunta a +Z.</summary>
    public static class KeyProp
    {
        public const string Name = "Llave";

        public static Transform Build(Material gold, Vector3 position)
        {
            var root = new GameObject(Name).transform;
            root.position = position;
            Part(root, "Anilla", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.045f), new Vector3(0.035f, 0.004f, 0.035f), new Vector3(0f, 0f, 90f), gold);
            Part(root, "Vastago", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.01f), new Vector3(0.008f, 0.05f, 0.008f), new Vector3(90f, 0f, 0f), gold);
            Part(root, "Diente_1", PrimitiveType.Cube, new Vector3(0f, -0.012f, 0.05f), new Vector3(0.006f, 0.022f, 0.012f), Vector3.zero, gold);
            Part(root, "Diente_2", PrimitiveType.Cube, new Vector3(0f, -0.006f, 0.032f), new Vector3(0.006f, 0.014f, 0.01f), Vector3.zero, gold);
            return root;
        }

        private static void Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Vector3 euler, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
