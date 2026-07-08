using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Utils
{
    public static class AssetLoader
    {
        /// <summary>
        /// Loads an asset at the specified path.
        ///
        /// This function only works on editor mode, it will return null
        /// in actual builds
        ///
        /// This is intended to be used in reset functions to load assets
        /// if a safe way. Don't use it in game code, it's for editor-related utilities
        /// </summary>
        /// <param name="path">Path to the asset to load</param>
        /// <typeparam name="T">An unity object type</typeparam>
        /// <returns>Your asset in editor mode, or null in an actual build</returns>
        public static T LoadAtPath<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<T>(path);
#else
            return null;
#endif
        }

        public static T LoadByGuid<T>(string guid) where T : Object
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
#else
            return null;
#endif
        }
    }
}
