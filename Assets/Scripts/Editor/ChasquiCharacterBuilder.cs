using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEditor.SceneManagement;

// Editor-only authoring: movement and camera follow are reserved for step 6.
[InitializeOnLoad]
public static class ChasquiCharacterBuilder
{
    const string Atlas="Assets/Sprites/Characters/Chasqui/ChasquiDirecciones.png";
    const string Prefab="Assets/Prefabs/Characters/ElPequenoChasqui.prefab";
    static readonly string[] Directions={"Down","Up","Left","Right"};
    static readonly string[] Poses={"Idle","WalkA","WalkB"};
    static ChasquiCharacterBuilder(){EditorApplication.delayCall+=TryBuild;}
    static void TryBuild()
    {
        if(File.Exists(Prefab)||SessionState.GetBool("ChasquiPaso5Attempt",false))return;
        if(EditorApplication.isUpdating||EditorApplication.isCompiling){EditorApplication.delayCall+=TryBuild;return;}
        SessionState.SetBool("ChasquiPaso5Attempt",true);
        try{Build();}catch(Exception e){Debug.LogException(e);}
    }
    static int PlayerLayer()
    {
        int n=LayerMask.NameToLayer("Personaje");if(n>=0)return n;
        var so=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var layers=so.FindProperty("layers");
        for(int i=8;i<32;i++)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)){layers.GetArrayElementAtIndex(i).stringValue="Personaje";so.ApplyModifiedProperties();return i;}
        throw new InvalidOperationException("No hay capa libre para Personaje.");
    }
    [MenuItem("Chasqui/Preparar personaje (Paso 5)")]
    public static void Build()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/SenderoDeLaAldea.unity"||scene.isDirty)throw new InvalidOperationException("Guarde SenderoDeLaAldea antes de preparar al personaje.");
        if(File.Exists(Prefab)||GameObject.Find("ElPequenoChasqui")!=null)throw new InvalidOperationException("El personaje ya existe; no se sobrescribira.");
        var importer=(TextureImporter)AssetImporter.GetAtPath(Atlas);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.spritePixelsPerUnit=170;importer.npotScale=TextureImporterNPOTScale.None;importer.isReadable=true;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=2048;importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
        var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Atlas);
        if(tex.width!=1086||tex.height!=1448)throw new InvalidOperationException("Dimensiones del atlas distintas de las revisadas.");
        // Separators measured in transparent gaps; the generated rows are not perfectly uniform.
        int[] rowTop={0,362,720,1064,1448};var rects=new SpriteRect[12];
        for(int row=0;row<4;row++)for(int col=0;col<3;col++){
            int x0=col*362,y0=tex.height-rowTop[row+1],height=rowTop[row+1]-rowTop[row];
            int minX=362,minY=height,maxX=0,maxY=0;
            for(int y=0;y<height;y++)for(int x=0;x<362;x++)if(tex.GetPixel(x0+x,y0+y).a>.08f){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
            minX=Math.Max(0,minX-2);minY=Math.Max(0,minY-2);maxX=Math.Min(361,maxX+2);maxY=Math.Min(height-1,maxY+2);
            rects[row*3+col]=new SpriteRect{name=Directions[row]+"_"+Poses[col],spriteID=GUID.Generate(),rect=new Rect(x0+minX,y0+minY,maxX-minX+1,maxY-minY+1),alignment=SpriteAlignment.Custom,pivot=new Vector2(.5f,.035f)};
        }
        var factories=new SpriteDataProviderFactories();factories.Init();var data=factories.GetSpriteEditorDataProviderFromObject(importer);data.InitSpriteEditorDataProvider();data.SetSpriteRects(rects);
        data.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));data.Apply();importer.SaveAndReimport();
        var sprites=AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().ToArray();if(sprites.Length!=12)throw new InvalidOperationException("Se esperaban 12 sprites.");
        var player=new GameObject("ElPequenoChasqui");player.tag="Player";player.layer=PlayerLayer();player.transform.position=new Vector3(-12,-1.9f,0);
        var sr=player.AddComponent<SpriteRenderer>();sr.sprite=sprites.Single(s=>s.name=="Down_Idle");sr.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Tilemaps/Sendero/TerrenoUnlit.mat");sr.spriteSortPoint=SpriteSortPoint.Pivot;sr.sortingOrder=1019;
        var rb=player.AddComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Dynamic;rb.gravityScale=0;rb.constraints=RigidbodyConstraints2D.FreezeRotation;rb.interpolation=RigidbodyInterpolation2D.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;rb.linearDamping=0;rb.angularDamping=0;
        var material=new PhysicsMaterial2D("ChasquiSinFriccion"){friction=0,bounciness=0};AssetDatabase.CreateAsset(material,"Assets/Materials/ChasquiSinFriccion.physicsMaterial2D");
        var collider=player.AddComponent<CapsuleCollider2D>();collider.direction=CapsuleDirection2D.Horizontal;collider.size=new Vector2(.52f,.30f);collider.offset=new Vector2(0,.13f);collider.sharedMaterial=material;
        Physics2D.SyncTransforms();int obstacles=1<<LayerMask.NameToLayer("Obstaculos");if(Physics2D.OverlapCapsule((Vector2)player.transform.position+collider.offset,collider.size,collider.direction,0,obstacles)!=null)throw new InvalidOperationException("El punto inicial coincide con un obstaculo.");
        PrefabUtility.SaveAsPrefabAssetAndConnect(player,Prefab,InteractionMode.AutomatedAction);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject=player;if(SceneView.lastActiveSceneView!=null){SceneView.lastActiveSceneView.in2DMode=true;SceneView.lastActiveSceneView.LookAt(player.transform.position+Vector3.up*.8f,Quaternion.identity,3.5f,true,true);}
        File.WriteAllText("Assets/Documentation/VerificacionPaso5.txt","12 sprites originales: Down, Up, Left, Right; Idle, WalkA y WalkB. Recortes adaptados a las filas del atlas. Prefab ElPequenoChasqui en (-12,-1.9), pivote de pies, orden 1019. Rigidbody2D Dynamic, gravedad cero, rotacion bloqueada, interpolacion y deteccion continua. CapsuleCollider2D 0.52 x 0.30, sin friccion, no trigger. Punto inicial libre. Movimiento, Animator y seguimiento de camara pendientes del Paso 6.");
        Debug.Log("CHASQUI PASO 5 OK: 12 sprites, personaje visible, Rigidbody2D y collider configurados; punto inicial libre.");
    }
}
