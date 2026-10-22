using Brawler.Player;
using Brawler.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Brawler.Editor
{
    internal static class EditorUtil
    {
        internal static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }

        internal static void Set(Object target, System.Action<SerializedObject> edit)
        {
            var so = new SerializedObject(target);
            edit(so);
            so.ApplyModifiedProperties();
        }

        internal static void SetArray(SerializedProperty property, params Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        internal static GameObject CreateRoot(string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            return go;
        }

        internal static Transform FindOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                return child;
            }

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        internal static Transform PlayerCamera(PlayerController player)
        {
            BillboardSprite billboard = player.GetComponentInChildren<BillboardSprite>(true);
            return billboard != null ? billboard.CameraTransform : null;
        }
    }
}
