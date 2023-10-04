using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

public class UnityDresser : EditorWindow
{
    private const string ver = "1.0.0";
    private static GameObject avatarRef;
    private static GameObject clothingRef;
    private static string prefix;
    private static bool groupRender;
    private static string log;

    [MenuItem("Tools/Polar/Unity Dresser")]
    private static void GetWindow()
    {
        var win = EditorWindow.GetWindow(typeof(UnityDresser));
        win.minSize = new Vector2(350, 400);
        win.maxSize = new Vector2(550, 600);
        win.titleContent = new GUIContent("Unity Dresser");
    }

    private static void AddLog(string input)
    {
        log += $"[{DateTime.Now.ToString("HH:mm:ss")}] {input}\n";
    }
    private void OnGUI()
    {
        var font = new GUIStyle(GUI.skin.label);
        font.fontSize = 16;
        font.fontStyle = FontStyle.Bold;
        EditorGUILayout.LabelField("Unity Dresser by Polar", font);
        EditorGUILayout.Space();

        avatarRef = EditorGUILayout.ObjectField("Avatar Root", avatarRef, typeof(GameObject), true) as GameObject;
        clothingRef = EditorGUILayout.ObjectField("Clothing Prefab", clothingRef, typeof(GameObject), true) as GameObject;
        prefix = EditorGUILayout.TextField("Custom Prefix", prefix);
        groupRender = EditorGUILayout.Toggle("Group Renderers", groupRender);

        if (GUILayout.Button("Apply Clothes"))
        {
            log = "";
            var stop = false;
            if (avatarRef == null)
            {
                AddLog("Avatar is null or invalid!");
                stop = true;
            }

            if (clothingRef == null)
            {
                AddLog("Clothing is null or invalid!");
                stop = true;
            }

            if (stop)
            {
                AddLog("Stopped clothing process.");
                return;
            }

                UnityDresser.ApplyClothes();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Output Log");
        EditorGUILayout.TextArea(log, GUILayout.ExpandHeight(true));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Version {ver}");
    }

    private static void ApplyClothes()
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Unity Dresser");

        var clothing = GameObject.Instantiate(clothingRef);
        clothing.name = clothing.name.Replace("(Clone)", "");
        if (string.IsNullOrEmpty(prefix))
        {
            prefix = clothing.name;
            AddLog("No custom prefix provided, using clothes name.");
        }

        Undo.RegisterCreatedObjectUndo(clothing, "clothing");
        Undo.DestroyObjectImmediate(clothingRef);

        clothing.transform.localScale = avatarRef.transform.localScale;
        clothing.transform.position = avatarRef.transform.position;

        #region "Creating dictionaries with avatar and clothing bones"
        var avatarBones = new Dictionary<string, Transform>();

        foreach (var transform in avatarRef.transform.Find("Armature").GetComponentsInChildren<Transform>())
            avatarBones.Add(transform.name, transform);

        var clothingBones = new Dictionary<string, Transform>();

        foreach (var transform in clothing.transform.Find("Armature").GetComponentsInChildren<Transform>())
            clothingBones.Add(transform.name, transform);
        #endregion

        var meshes = new List<Renderer>(clothing.GetComponentsInChildren<Renderer>());

        foreach (var obj in clothing.GetComponentsInChildren<Transform>())
            obj.name = $"({prefix}) {obj.name}";
        AddLog("Finished prefixing gameobjects.");

        #region "Renderer related things"
        if (groupRender)
        {
            var meshParent = new GameObject(prefix);
            meshParent.transform.SetParent(avatarRef.transform, false);
            Undo.RegisterCreatedObjectUndo(meshParent, "clothing");

            foreach (var mesh in meshes)
            {
                if (mesh.transform.parent != clothing.transform)
                {
                    var constraint = mesh.gameObject.AddComponent<ParentConstraint>();
                    var source = new ConstraintSource();
                    source.sourceTransform = mesh.transform.parent;
                    source.weight = 1;
                    constraint.AddSource(source);
                    constraint.constraintActive = true;
                }
                mesh.transform.SetParent(meshParent.transform);
            }
        }
        else
        {
            foreach (var mesh in meshes)
            {
                Undo.RegisterCreatedObjectUndo(mesh.gameObject, "clothing");
                var avatarParent = avatarRef.transform;
                if (mesh.transform.parent != clothing.transform)
                {
                    avatarBones.TryGetValue(mesh.name, out Transform avatarParentSet);
                    avatarParent = avatarParentSet;
                }
                mesh.transform.SetParent(avatarParent);
            }
        }
        #endregion
        AddLog("Finished moving renderers.");

        foreach (var bone in clothingBones)
        {
            Undo.RegisterCreatedObjectUndo(bone.Value.gameObject, "clothing");
            if (avatarBones.TryGetValue(bone.Key, out Transform avatarBone))
                bone.Value.SetParent(avatarBone, true);
        }
        AddLog("Finished parenting bones.");

        Undo.DestroyObjectImmediate(clothing);
        clothingRef = null;
        prefix = null;
    }
}
