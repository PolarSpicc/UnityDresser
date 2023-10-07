using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Dynamics.PhysBone.Components;

public class UnityDresser : EditorWindow
{
    private const string ver = "1.3.1";
    private static GameObject avatarRef;
    private static GameObject clothingRef;
    private static string prefix;
    private static bool groupRender;
    private static bool makeAnims;
    private static bool defaultOff;
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
        defaultOff = EditorGUILayout.Toggle("Default Off", defaultOff);
        makeAnims = EditorGUILayout.Toggle("Make Toggle Anims", makeAnims);

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

            if (defaultOff)
                meshParent.SetActive(false);

            if (makeAnims)
            {
                createAnim(meshParent, true, false);
                createAnim(meshParent, false, false);
            }

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
                if (mesh.transform.parent == clothing.transform)
                {
                    mesh.transform.SetParent(avatarRef.transform);

                    if (defaultOff)
                        mesh.gameObject.SetActive(false);

                    if (makeAnims)
                    {
                        createAnim(mesh.gameObject, true, false);
                        createAnim(mesh.gameObject, false, false);
                    }
                }
            }
        }
        #endregion
        AddLog("Finished moving renderers.");

        foreach (var bone in clothingBones)
        {
            if (groupRender && bone.Value.GetComponent<Renderer>())
                continue;

            Undo.RegisterCreatedObjectUndo(bone.Value.gameObject, "clothing");
            if (avatarBones.TryGetValue(bone.Key, out Transform avatarBone))
                bone.Value.SetParent(avatarBone, true);

            if (defaultOff)
                bone.Value.gameObject.SetActive(false);

            if (makeAnims)
            {
                createAnim(bone.Value.gameObject, true, true);
                createAnim(bone.Value.gameObject, false, true);
            }
        }
        AddLog("Finished parenting bones.");

        Thread.Sleep(100);
        foreach (var bone in avatarBones)
        {
            var physcomp = bone.Value.GetComponent<VRCPhysBone>();

            if (physcomp == null)
                continue;

            Undo.RecordObject(physcomp, "clothing");
            foreach (var child in bone.Value.GetComponentsInChildren<Transform>())
            {
                if (child.name.StartsWith($"({prefix}) "))
                    physcomp.ignoreTransforms.Add(child);
            }
        }
        AddLog("Finished setting ignored transforms in physbones.");

        if (makeAnims)
            AddLog("Created toggle animations.");

        Undo.DestroyObjectImmediate(clothing);
        clothingRef = null;
        prefix = null;
    }

    private static void createAnim(GameObject obj, bool state, bool nested)
    {
        string name = nested ? GetNested(obj.transform) : obj.name;
        string statusString = state ? "on" : "off";

        if (!AssetDatabase.IsValidFolder("Assets/Polar/UnityDresser/Anims"))
            AssetDatabase.CreateFolder("Assets/Polar/UnityDresser", "Anims");

        string filename = $"Assets/Polar/UnityDresser/Anims/{prefix}-{statusString}.anim";

        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(filename);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, filename);
        }

        Keyframe key = new Keyframe(0.0f, state ? 1.0f : 0.0f);
        Keyframe key2 = new Keyframe(0.01f, state ? 1.0f : 0.0f);
        AnimationCurve animationCurve = new AnimationCurve(key, key2);
        clip.SetCurve(name, typeof(GameObject), "m_IsActive", animationCurve);
    }

    public static string GetNested(Transform current)
    {
        if (current.parent != null && current.parent.parent == null)
            return current.name;

        return GetNested(current.parent) + "/" + current.name;
    }

}
