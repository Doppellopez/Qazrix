using System.Reflection;
using UnityEditor;
using UnityEditor.AI;
using UnityEngine;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// La capa azul que el editor pinta sobre suelo y tejados es la ayuda visual de la malla de
    /// navegación del guardia. Tapa las texturas, así que se oculta al abrir el editor; se puede volver
    /// a ver desde Qazrix → Ver → Malla de navegación (capa azul). En el build nunca se dibuja.
    /// </summary>
    [InitializeOnLoad]
    public static class NavMeshOverlayToggle
    {
        private const string MenuPath = "Qazrix/Ver/Malla de navegación (capa azul)";
        private const string SessionKey = "Qazrix.NavMeshOverlayToggle.hiddenThisSession";

        // El interruptor «Show NavMesh» del panel AI Navigation es interno del editor: se llega por reflexión.
        private static readonly PropertyInfo ShowNavMesh = typeof(NavMeshVisualizationSettings).GetProperty(
            "showNavMesh", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        static NavMeshOverlayToggle()
        {
            // Una sola vez por sesión del editor: después manda lo que elija quien lo usa.
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += () => Set(false);
        }

        /// <summary>Si el editor está pintando la capa azul; falso si no se puede saber.</summary>
        public static bool Visible => ShowNavMesh != null && (bool)ShowNavMesh.GetValue(null);

        [MenuItem(MenuPath, priority = 70)]
        private static void Toggle() => Set(!Visible);

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Visible);
            return ShowNavMesh != null;
        }

        public static void Set(bool visible)
        {
            if (ShowNavMesh == null)
            {
                Debug.LogWarning("[Qazrix] Esta versión del editor no expone «Show NavMesh»: usa el panel AI Navigation de la vista Scene.");
                return;
            }
            ShowNavMesh.SetValue(null, visible);
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
}
