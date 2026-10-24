using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class ChasquiMotionSetup
{
    const string Folder = "Assets/Animations/Chasqui";
    const string Report = "Assets/Documentation/VerificacionPaso6.txt";
    static ChasquiMotionSetup() { EditorApplication.delayCall += TrySetup; }
    static void TrySetup()
    {
        if (File.Exists(Report) || SessionState.GetBool("ChasquiPaso6Attempt", false)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += TrySetup; return; }
        SessionState.SetBool("ChasquiPaso6Attempt", true);
        try { Setup(); } catch (Exception e) { Debug.LogException(e); }
    }
    [MenuItem("Chasqui/Configurar movimiento y camara (Paso 6)")]
    public static void Setup()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SenderoDeLaAldea.unity" || scene.isDirty)
            throw new InvalidOperationException("Guarde SenderoDeLaAldea antes de configurar movimiento.");
        if (Directory.Exists(Folder)) throw new InvalidOperationException("Las animaciones ya existen; no se sobrescribiran.");
        var player = GameObject.Find("ElPequenoChasqui");
        if (player == null || Camera.main == null) throw new InvalidOperationException("Faltan personaje o camara.");
        var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Characters/Chasqui/ChasquiDirecciones.png").OfType<Sprite>().ToDictionary(s => s.name);
        if (sprites.Count != 12) throw new InvalidOperationException("Se requieren los 12 sprites del personaje.");
        AssetDatabase.CreateFolder("Assets/Animations", "Chasqui");
        var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/Chasqui.controller");
        foreach (string direction in new[] { "Down", "Up", "Left", "Right" })
        foreach (bool walk in new[] { false, true })
        {
            string name = direction + (walk ? "_Walk" : "_Idle");
            var clip = new AnimationClip { name = name, frameRate = 8 };
            string[] poses = walk ? new[] { "WalkA", "Idle", "WalkB", "Idle", "WalkA" } : new[] { "Idle", "Idle" };
            var keys = poses.Select((pose, i) => new ObjectReferenceKeyframe { time = walk ? i / 8f : i, value = sprites[direction + "_" + pose] }).ToArray();
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, Folder + "/" + name + ".anim");
            var state = controller.layers[0].stateMachine.AddState(name); state.motion = clip;
            if (name == "Down_Idle") controller.layers[0].stateMachine.defaultState = state;
        }
        var animator = player.AddComponent<Animator>(); animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
        player.AddComponent<ChasquiMovement>();
        PrefabUtility.ApplyPrefabInstance(player, InteractionMode.AutomatedAction);
        var camera = Camera.main; camera.orthographic = true; camera.orthographicSize = 5.5f;
        camera.gameObject.AddComponent<ChasquiCameraFollow>().target = player.transform;
        camera.transform.position = new Vector3(-18 + 5.5f * 16 / 9, player.transform.position.y + .6f, -10);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        if (controller.animationClips.Length != 8 || player.GetComponent<Rigidbody2D>().gravityScale != 0 || player.GetComponent<Collider2D>().isTrigger)
            throw new InvalidOperationException("La verificacion de componentes fallo.");
        File.WriteAllText(Report, "PASO 6: 8 clips Idle/Walk y controlador Animator; movimiento cardinal WASD/flechas; entrada tactil preparada sin interfaz; orden visual por Y; camara ortografica con seguimiento y limites. Escena y prefab guardados. Compilacion y componentes verificados. Pruebas Play pendientes del Paso 7.\n");
        Debug.Log("CHASQUI PASO 6 OK: 8 animaciones, movimiento y camara guardados. Pruebas Play pendientes.");
    }
}
