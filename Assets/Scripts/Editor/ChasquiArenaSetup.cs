using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
[InitializeOnLoad]
public static class ChasquiArenaSetup
{
    const string Path="Assets/Scenes/ArenaDeLosQuipus.unity";
    static ChasquiArenaSetup(){EditorApplication.delayCall+=Build;}
    [MenuItem("Chasqui/Crear arena de quipus")]
    static void Build()
    {
        if(File.Exists(Path)||EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Build;return;}
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/SenderoDeLaAldea.unity"||scene.isDirty)return;
        EditorSceneManager.SaveScene(scene,Path,true);
        scene=EditorSceneManager.OpenScene(Path);
        var player=GameObject.Find("ElPequenoChasqui");
        var arena=new GameObject("Arena_Enemigos_Quipus_Poderes").AddComponent<ChasquiArena>();
        arena.player=player.transform;
        arena.enemySprite=AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Characters/Chasqui/ChasquiDirecciones.png").OfType<Sprite>().First(s=>s.name=="Down_Idle");
        arena.spriteMaterial=player.GetComponent<SpriteRenderer>().sharedMaterial;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        var list=EditorBuildSettings.scenes.ToList(); if(!list.Any(s=>s.path==Path))list.Insert(0,new EditorBuildSettingsScene(Path,true));EditorBuildSettings.scenes=list.ToArray();
        File.WriteAllText("Assets/Documentation/ArenaQuipus.md","Arena de una escena. WASD/flechas, clic/espacio lanza hacia raton, E ultima habilidad. 25% portadores morados: quipu; normales: oro. Quipu pausa y permite elegir coca, vasija o corona. Enemigos usan temporalmente el sprite original recoloreado como sombras; pendiente arte propio de enemigos y pictogramas definitivos. Escena original conservada.\n");
        Debug.Log("CHASQUI ARENA: escena creada y guardada.");
    }
}
