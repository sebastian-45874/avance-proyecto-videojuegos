using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class ChasquiArenaCheck
{
    static int stage; static double waitUntil; static ChasquiArena arena; static Vector3 start; static int oldGold; static string result;
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static ChasquiArenaCheck(){EditorApplication.update+=Tick;}
    [MenuItem("Chasqui/Probar arena y recompensas")]
    public static void Begin(){SessionState.SetBool("ArenaCheck",true);EditorApplication.isPlaying=true;}
    static void Assert(bool ok,string message){if(!ok)throw new Exception(message);result+="OK: "+message+"\n";}
    static void Tick()
    {
        if(!SessionState.GetBool("ArenaCheck",false)||!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<waitUntil)return;
        try {
            if(stage==0){arena=UnityEngine.Object.FindAnyObjectByType<ChasquiArena>();if(arena==null)return;start=arena.player.position;result="Prueba automatizada en Play, arena de quipus\n";Assert(arena.stoneSprite!=null && arena.slingerSprite!=null && arena.stoneSprite!=arena.enemySprite,"Sprites del Vigia y Hondero asignados");waitUntil=EditorApplication.timeSinceStartup+2;stage++;return;}
            if(stage==1){arena.eliteChance=0;Assert(arena.SpawnEnemy(),"Aparece enemigo normal en espacio libre");oldGold=arena.Gold;KillLast();stage++;waitUntil=EditorApplication.timeSinceStartup+1;return;}
            if(stage==2){Assert(arena.Gold==oldGold+5&&!arena.Choosing,"Normal entrega oro, sin menu de quipu");arena.eliteChance=1;Assert(arena.SpawnEnemy(),"Aparece enemigo portador morado");KillLast();stage++;waitUntil=EditorApplication.timeSinceStartup+1;return;}
            if(stage==3){Assert(arena.Quipus==1&&arena.Choosing&&Time.timeScale==0,"Quipu recogido pausa y abre seleccion");arena.ChoosePower(0);Assert(arena.player.GetComponent<ChasquiMovement>().SpeedMultiplier>1&&Time.timeScale==1,"Coca aumenta velocidad y reanuda");typeof(ChasquiArena).GetField("choosing",Hidden).SetValue(arena,true);arena.ChoosePower(1);var shots=(IList)typeof(ChasquiArena).GetField("shots",Hidden).GetValue(arena);int before=shots.Count;typeof(ChasquiArena).GetField("nextShot",Hidden).SetValue(arena,0f);arena.Attack();Assert(shots.Count==before+2,"Vasija produce dos lanzas");typeof(ChasquiArena).GetField("choosing",Hidden).SetValue(arena,true);arena.ChoosePower(2);arena.Ultimate();Assert((float)typeof(ChasquiArena).GetField("ultimateReady",Hidden).GetValue(arena)>Time.time,"Corona activa especial y enfriamiento");arena.player.position=start;arena.WorldClick((Vector2)start+Vector2.right*2);stage++;waitUntil=EditorApplication.timeSinceStartup+.5;return;}
            if(stage==4){Assert(arena.player.position.x>start.x,"Clic en suelo desplaza al personaje sin teclado");arena.player.GetComponent<ChasquiMovement>().ReleaseTouchInput();Finish(result);}
        }catch(Exception e){Finish(result+"FALLO: "+e+"\n");Debug.LogException(e);}
    }
    static void KillLast(){var list=(IList)typeof(ChasquiArena).GetField("enemies",Hidden).GetValue(arena);var e=list[list.Count-1];var go=(GameObject)e.GetType().GetField("go").GetValue(e);Vector2 dropPoint=(Vector2)go.transform.position+Vector2.up*.2f;arena.WorldClick((Vector2)go.transform.position+Vector2.up*.5f);Assert(typeof(ChasquiArena).GetField("selectedEnemy",Hidden).GetValue(arena)!=null,"Clic en enemigo selecciona objetivo de ataque");typeof(ChasquiArena).GetMethod("DamageEnemy",Hidden).Invoke(arena,new object[]{list.Count-1,100});arena.WorldClick(dropPoint);}
    static void Finish(string text){File.WriteAllText("Assets/Documentation/PruebaArena.txt",text);SessionState.SetBool("ArenaCheck",false);stage=0;EditorApplication.isPlaying=false;Debug.Log("CHASQUI PRUEBA ARENA: "+text);}
}




