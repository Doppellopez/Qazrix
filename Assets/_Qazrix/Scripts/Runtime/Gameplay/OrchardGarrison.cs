using System.Collections.Generic;
using Qazrix.Simulation.Movement;
using Qazrix.Simulation.World;
using UnityEngine;

namespace Qazrix.Gameplay
{
    /// <summary>
    /// La guarnición que espera a la salida del pasadizo: un capitán y 50 soldados con el emblema de San Jorge (cruz roja sobre campo
    /// blanco) en el escudo, formados delante de quien sale (<see cref="GarrisonLayout"/>). Cada uno es un modelo de primitivas ligero
    /// (unas catorce piezas) que comparte materiales con los demás; esperan quietos, respirando y moviendo un poco la cabeza.
    /// </summary>
    public sealed class OrchardGarrison : MonoBehaviour
    {
        public const string SoldierName = "Soldado";
        public const string CaptainName = "Capitan";

        private readonly List<Transform> _members = new List<Transform>();

        /// <summary>Número de soldados (sin contar al capitán) y si hay capitán.</summary>
        public int SoldierCount { get; private set; }
        public bool HasCaptain { get; private set; }

        /// <summary>El escudo con la cruz de San Jorge (textura generada).</summary>
        public Texture2D ShieldTexture { get; private set; }

        /// <summary>Cruz de San Jorge: roja sobre blanco, con filete oscuro. Cuadrada de <paramref name="size"/> píxeles.</summary>
        public static Texture2D MakeShieldTexture(int size = 128)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Escudo_San_Jorge", wrapMode = TextureWrapMode.Clamp };
            var white = new Color(0.96f, 0.95f, 0.92f);
            var red = new Color(0.78f, 0.06f, 0.08f);
            var rim = new Color(0.12f, 0.10f, 0.09f);
            int arm = Mathf.Max(2, size * 3 / 16);            // grosor de cada brazo de la cruz
            int c = size / 2, border = Mathf.Max(2, size / 32);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x < border || y < border || x >= size - border || y >= size - border;
                    bool cross = Mathf.Abs(x - c) < arm / 2 || Mathf.Abs(y - c) < arm / 2;
                    tex.SetPixel(x, y, edge ? rim : (cross ? red : white));
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>Levanta la guarnición alrededor de <paramref name="exit"/>, mirando hacia <paramref name="facingYaw"/> (de quien sale).</summary>
        public void Build(Material baseMaterial, Vector3 exit, float facingYaw, System.Func<Vector2, Vector3> surface, int soldiers = GarrisonLayout.DefaultSoldiers)
        {
            ShieldTexture = MakeShieldTexture();
            Material steel = Plain(baseMaterial, "Soldado_acero", new Color(0.62f, 0.64f, 0.68f), 0.6f);
            Material surcoat = Plain(baseMaterial, "Soldado_sobreveste", new Color(0.90f, 0.89f, 0.85f), 0f);
            Material hose = Plain(baseMaterial, "Soldado_calzas", new Color(0.30f, 0.30f, 0.33f), 0f);
            Material skin = Plain(baseMaterial, "Soldado_piel", new Color(0.78f, 0.58f, 0.45f), 0f);
            Material wood = Plain(baseMaterial, "Soldado_madera", new Color(0.35f, 0.22f, 0.11f), 0f);
            Material red = Plain(baseMaterial, "Soldado_rojo", new Color(0.72f, 0.07f, 0.09f), 0f);
            Material gold = Plain(baseMaterial, "Soldado_oro", new Color(0.84f, 0.67f, 0.28f), 0.5f);
            Material shield = Plain(baseMaterial, "Escudo_San_Jorge", Color.white, 0.1f);
            foreach (string property in new[] { "_BaseMap", "_MainTex" })
                if (shield.HasProperty(property)) shield.SetTexture(property, ShieldTexture);

            var materials = new Materials { Steel = steel, Surcoat = surcoat, Hose = hose, Skin = skin, Wood = wood, Red = red, Gold = gold, Shield = shield };

            foreach (GarrisonSlot slot in GarrisonLayout.Compute(exit.x, exit.z, facingYaw, soldiers))
            {
                Vector3 ground = surface(new Vector2((float)slot.X, (float)slot.Z));
                Transform member = BuildSoldier(slot.IsCaptain, materials);
                member.SetParent(transform, false);
                member.SetPositionAndRotation(ground, Quaternion.Euler(0f, (float)slot.YawDegrees, 0f));
                _members.Add(member);
                if (slot.IsCaptain) HasCaptain = true; else SoldierCount++;
            }
        }

        private struct Materials
        {
            public Material Steel, Surcoat, Hose, Skin, Wood, Red, Gold, Shield;
        }

        private Transform BuildSoldier(bool captain, Materials m)
        {
            var root = new GameObject(captain ? CaptainName : SoldierName) { hideFlags = HideFlags.DontSave }.transform;
            float s = captain ? 1.08f : 1f;
            var model = new GameObject("Modelo").transform;
            model.SetParent(root, false);
            model.localScale = Vector3.one * s;

            // Calzas, bota, sobreveste blanca (la guarnición de San Jorge), brazos, cabeza y yelmo.
            foreach (float side in new[] { -1f, 1f })
            {
                Part(model, "Pierna", PrimitiveType.Capsule, new Vector3(side * 0.11f, 0.47f, 0f), new Vector3(0.17f, 0.47f, 0.17f), m.Hose);
                Part(model, "Bota", PrimitiveType.Cube, new Vector3(side * 0.11f, 0.05f, 0.04f), new Vector3(0.15f, 0.10f, 0.26f), m.Wood);
            }
            Part(model, "Sobreveste", PrimitiveType.Capsule, new Vector3(0f, 1.18f, 0f), new Vector3(0.50f, 0.42f, 0.30f), m.Surcoat);
            Part(model, "Cinto", PrimitiveType.Cylinder, new Vector3(0f, 0.93f, 0f), new Vector3(0.27f, 0.02f, 0.20f), m.Wood);
            Transform torso = model;
            Part(torso, "Cabeza", PrimitiveType.Sphere, new Vector3(0f, 1.62f, 0f), new Vector3(0.20f, 0.23f, 0.21f), m.Skin);
            Part(torso, "Yelmo", PrimitiveType.Sphere, new Vector3(0f, 1.68f, 0f), new Vector3(0.25f, 0.20f, 0.26f), m.Steel);
            Part(torso, "Nasal", PrimitiveType.Cube, new Vector3(0f, 1.62f, 0.12f), new Vector3(0.025f, 0.12f, 0.02f), m.Steel);

            // Brazo izquierdo con el escudo; derecho con la lanza apoyada en el suelo.
            Part(model, "Brazo_izq", PrimitiveType.Capsule, new Vector3(-0.30f, 1.22f, 0.08f), new Vector3(0.11f, 0.26f, 0.11f), m.Surcoat, new Vector3(25f, 0f, 8f));
            // Escudo: disco de cara plana con la cruz de San Jorge mirando hacia delante (el del capitán, algo mayor y con borde dorado).
            float shieldSize = captain ? 0.50f : 0.42f;
            Transform shieldDisc = Part(model, "Escudo", PrimitiveType.Cylinder, new Vector3(-0.38f, 1.12f, 0.24f), new Vector3(shieldSize, 0.025f, shieldSize), m.Shield, new Vector3(90f, 0f, 0f));
            FixShieldUvs(shieldDisc);
            if (captain) Part(model, "Escudo_borde", PrimitiveType.Cylinder, new Vector3(-0.38f, 1.12f, 0.228f), new Vector3(shieldSize + 0.07f, 0.02f, shieldSize + 0.07f), m.Gold, new Vector3(90f, 0f, 0f));

            if (!captain)
            {
                // Brazo derecho articulado en el hombro y lanza apoyada en el suelo, que alzan al recibir el capitán la llave (SoldierIdle).
                var armPivot = new GameObject("Brazo_der_pivote").transform;
                armPivot.SetParent(model, false);
                armPivot.localPosition = new Vector3(0.31f, 1.38f, 0.05f);
                armPivot.localEulerAngles = new Vector3(10f, 0f, -6f);
                Part(armPivot, "Brazo", PrimitiveType.Capsule, new Vector3(0f, -0.19f, 0f), new Vector3(0.11f, 0.26f, 0.11f), m.Surcoat);
                Part(armPivot, "Mano", PrimitiveType.Sphere, new Vector3(0f, -0.47f, 0f), new Vector3(0.075f, 0.10f, 0.06f), m.Skin);
                var spear = new GameObject("Lanza_grupo").transform;
                spear.SetParent(model, false);
                spear.localPosition = new Vector3(0.40f, 1.15f, 0.12f);
                Part(spear, "Lanza", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.025f, 1.15f, 0.025f), m.Wood);
                Part(spear, "Punta", PrimitiveType.Cylinder, new Vector3(0f, 1.21f, 0f), new Vector3(0.04f, 0.09f, 0.04f), m.Steel);
                _spearArms.Add(armPivot);
                _spears.Add(spear);
            }

            if (captain)
            {
                // Brazo derecho articulado en el hombro, con mano: es el que recibe la llave. Lleva espada al cinto en vez de lanza.
                var arm = new GameObject("Brazo_der_pivote").transform;
                arm.SetParent(model, false);
                arm.localPosition = new Vector3(0.31f, 1.38f, 0.05f);
                arm.localEulerAngles = new Vector3(10f, 0f, -6f);
                Part(arm, "Brazo", PrimitiveType.Capsule, new Vector3(0f, -0.19f, 0f), new Vector3(0.11f, 0.26f, 0.11f), m.Surcoat);
                Transform hand = Part(arm, "Mano", PrimitiveType.Sphere, new Vector3(0f, -0.47f, 0f), new Vector3(0.075f, 0.10f, 0.06f), m.Skin);
                Part(model, "Espada", PrimitiveType.Cube, new Vector3(-0.28f, 0.80f, -0.05f), new Vector3(0.04f, 0.85f, 0.02f), m.Steel, new Vector3(0f, 0f, 14f));
                root.gameObject.AddComponent<CaptainKeyGive>().Configure(arm, hand, m.Gold);
                // --- Lo que lo distingue como capitán ---
                // Yelmo: cresta dorada, ala dorada en la frente y un penacho rojo alto con una pluma más pequeña a su lado.
                Part(model, "Yelmo_cresta", PrimitiveType.Cube, new Vector3(0f, 1.80f, 0f), new Vector3(0.035f, 0.07f, 0.26f), m.Gold);
                Part(model, "Yelmo_ala", PrimitiveType.Cylinder, new Vector3(0f, 1.62f, 0f), new Vector3(0.275f, 0.012f, 0.285f), m.Gold);
                Part(model, "Penacho", PrimitiveType.Capsule, new Vector3(0f, 1.97f, -0.03f), new Vector3(0.08f, 0.22f, 0.08f), m.Red, new Vector3(-12f, 0f, 0f));
                Part(model, "Penacho_pluma", PrimitiveType.Capsule, new Vector3(0.05f, 1.93f, -0.05f), new Vector3(0.05f, 0.15f, 0.05f), m.Red, new Vector3(-18f, 0f, -22f));
                // Torso: la cruz de San Jorge en el pecho, gorguera y hombreras doradas, cinto ancho con hebilla y faja roja.
                Part(model, "Cruz_vertical", PrimitiveType.Cube, new Vector3(0f, 1.16f, 0.145f), new Vector3(0.07f, 0.34f, 0.015f), m.Red);
                Part(model, "Cruz_horizontal", PrimitiveType.Cube, new Vector3(0f, 1.18f, 0.145f), new Vector3(0.27f, 0.07f, 0.015f), m.Red);
                Part(model, "Gola", PrimitiveType.Cylinder, new Vector3(0f, 1.42f, 0f), new Vector3(0.27f, 0.03f, 0.24f), m.Gold);
                foreach (float side in new[] { -1f, 1f })
                {
                    Part(model, "Hombrera", PrimitiveType.Sphere, new Vector3(side * 0.30f, 1.40f, 0.04f), new Vector3(0.17f, 0.12f, 0.17f), m.Gold);
                    Part(model, "Cana", PrimitiveType.Cylinder, new Vector3(side * 0.11f, 0.20f, 0f), new Vector3(0.19f, 0.03f, 0.19f), m.Gold);
                }
                Part(model, "Cinto_ancho", PrimitiveType.Cylinder, new Vector3(0f, 0.93f, 0f), new Vector3(0.29f, 0.05f, 0.22f), m.Gold);
                Part(model, "Hebilla", PrimitiveType.Cube, new Vector3(0f, 0.93f, 0.115f), new Vector3(0.07f, 0.06f, 0.02f), m.Steel);
                Part(model, "Faja", PrimitiveType.Cube, new Vector3(0.12f, 0.80f, 0.115f), new Vector3(0.05f, 0.30f, 0.02f), m.Red, new Vector3(0f, 0f, -6f));
                // Capa roja larga hasta las pantorrillas con ribete dorado, y la espada con empuñadura de oro.
                Part(model, "Capa", PrimitiveType.Cube, new Vector3(0f, 1.0f, -0.20f), new Vector3(0.58f, 1.15f, 0.03f), m.Red, new Vector3(-4f, 0f, 0f));
                Part(model, "Capa_ribete", PrimitiveType.Cube, new Vector3(0f, 0.43f, -0.225f), new Vector3(0.60f, 0.05f, 0.032f), m.Gold, new Vector3(-4f, 0f, 0f));
                Part(model, "Espada_guarda", PrimitiveType.Cube, new Vector3(-0.31f, 1.07f, -0.05f), new Vector3(0.20f, 0.035f, 0.035f), m.Gold, new Vector3(0f, 0f, 14f));
                Part(model, "Espada_pomo", PrimitiveType.Sphere, new Vector3(-0.31f, 1.15f, -0.05f), new Vector3(0.06f, 0.06f, 0.06f), m.Gold);
            }

            // Cuerpo sólido: no se les atraviesa.
            var body = root.gameObject.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0f, 0.9f * s, 0f);
            body.radius = 0.32f * s;
            body.height = 1.8f * s;

            var idle = root.gameObject.AddComponent<SoldierIdle>();
            idle.Configure(model, Random.value * 30f);
            if (!captain && _spears.Count > 0) idle.ConfigureSalute(_spearArms[_spearArms.Count - 1], _spears[_spears.Count - 1]);
            return root;
        }

        private readonly List<Transform> _spearArms = new List<Transform>(), _spears = new List<Transform>();

        /// <summary>Soldados que ya alzan la lanza (para pruebas).</summary>
        public IEnumerable<SoldierIdle> Soldiers()
        {
            foreach (Transform member in _members)
            {
                var idle = member.GetComponent<SoldierIdle>();
                if (idle != null && idle.HasSpear) yield return idle;
            }
        }

        /// <summary>
        /// El capitán ha recibido la llave: los soldados alzan las lanzas al descompás, cada uno en su momento y a su ritmo
        /// (<see cref="SpearSalute"/>). Una sola vez.
        /// </summary>
        public void RaiseSpears()
        {
            if (SpearsRaised) return;
            SpearsRaised = true;
            int index = 0;
            foreach (SoldierIdle soldier in Soldiers()) soldier.Salute(SpearSalute.ScheduleFor(index++));
        }

        public bool SpearsRaised { get; private set; }

        /// <summary>Los cilindros de Unity reparten la textura por la tapa de un modo que no vale para un escudo: se le dan UV de disco a la tapa.</summary>
        private static Mesh _shieldMesh; // una sola malla de escudo para todos los soldados

        private static void FixShieldUvs(Transform disc)
        {
            var filter = disc.GetComponent<MeshFilter>();
            if (_shieldMesh == null)
            {
                Mesh mesh = Object.Instantiate(filter.sharedMesh);
                mesh.name = "Escudo_San_Jorge";
                Vector3[] v = mesh.vertices;
                var uv = new Vector2[v.Length];
                // La tapa que mira hacia +Y del cilindro (que queda hacia delante tras girarlo 90° en X) usa x, z en [-0,5; 0,5] como UV.
                for (int i = 0; i < v.Length; i++) uv[i] = new Vector2(v[i].x + 0.5f, 0.5f - v[i].z);
                mesh.uv = uv;
                _shieldMesh = mesh;
            }
            filter.sharedMesh = _shieldMesh;
        }

        private static Material Plain(Material baseMaterial, string name, Color color, float smoothness)
        {
            var m = new Material(baseMaterial) { name = name };
            foreach (string property in new[] { "_BaseMap", "_MainTex", "_BumpMap" })
                if (m.HasProperty(property)) m.SetTexture(property, null);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        private static Transform Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Vector3? euler = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.hideFlags = HideFlags.DontSave;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localEulerAngles = euler ?? Vector3.zero;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }
    }

    /// <summary>Espera de un soldado en formación: respira y mueve la cabeza muy poco, cada uno a su ritmo (<see cref="ContactIdle"/>).</summary>
    public sealed class SoldierIdle : MonoBehaviour
    {
        private Transform _model;
        private float _offset;
        private Vector3 _basePosition;

        // Alzar la lanza (SpearSalute): el brazo derecho sube hacia delante y la lanza se eleva.
        private Transform _arm, _spear;
        private Quaternion _armRest;
        private Vector3 _spearRest;
        private SpearSalute.Schedule _schedule;
        private float _saluteStart = -1f;

        /// <summary>Tiene lanza que alzar (los soldados; el capitán no).</summary>
        public bool HasSpear => _spear != null;

        /// <summary>Altura actual de la lanza, de 0 (apoyada) a 1 (alzada del todo).</summary>
        public float SaluteWeight { get; private set; }

        public void Configure(Transform model, float timeOffset)
        {
            _model = model;
            _offset = timeOffset;
            _basePosition = model.localPosition;
        }

        public void ConfigureSalute(Transform arm, Transform spear)
        {
            _arm = arm;
            _spear = spear;
            _armRest = arm.localRotation;
            _spearRest = spear.localPosition;
        }

        /// <summary>Alza la lanza según este horario, contando desde ahora.</summary>
        public void Salute(SpearSalute.Schedule schedule)
        {
            if (_spear == null) return;
            _schedule = schedule;
            _saluteStart = Time.time;
        }

        private void Update()
        {
            if (_model == null) return;
            if (_saluteStart >= 0f)
            {
                SaluteWeight = SpearSalute.Weight(Time.time - _saluteStart, _schedule);
                _spear.localPosition = _spearRest + Vector3.up * SpearSalute.Lift(SaluteWeight);
                _arm.localRotation = _armRest * Quaternion.Euler(-SpearSalute.ArmDegrees(SaluteWeight), 0f, 0f);
            }
            ContactIdlePose pose = ContactIdle.Sample(Time.time + _offset);
            // Respira (sube un par de centímetros) y se balancea apenas sobre los pies.
            _model.localPosition = _basePosition + new Vector3(0f, pose.BreathLift * 0.5f, 0f);
            _model.localRotation = Quaternion.Euler(0f, pose.HeadYaw * 0.25f, pose.SwayRoll * 0.5f);
        }
    }
}
