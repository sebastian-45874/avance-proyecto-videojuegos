using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;

// One-time editor authoring; all resulting objects are saved in the scene.
[InitializeOnLoad]
public static class ChasquiVillageBuilder
{
    const string Root="Assets/Prefabs/Aldea";
    const string Atlas="Assets/Sprites/Environment/AldeaAndinaAtlas.png";
    static readonly string[] Names={"CasaPaja","CasaTeja","Rocas","Arbol","Maiz","Arbusto"};
    static readonly float[] Widths={5.2f,5f,2.2f,3.5f,2.5f,1.25f};
    static ChasquiVillageBuilder(){EditorApplication.delayCall+=TryBuild;}
    static void TryBuild()
    {
        if(Directory.Exists(Root)||SessionState.GetBool("ChasquiPaso4Attempt",false))return;
        if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=TryBuild;return;}
        SessionState.SetBool("ChasquiPaso4Attempt",true);
        try {Build();}catch(Exception e){Debug.LogException(e);}
    }
    static int ObstacleLayer()
    {
        int layer=LayerMask.NameToLayer("Obstaculos");if(layer>=0)return layer;
        var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers=settings.FindProperty("layers");
        for(int i=8;i<32;i++)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)){
            layers.GetArrayElementAtIndex(i).stringValue="Obstaculos";settings.ApplyModifiedProperties();return i;
        }
        throw new InvalidOperationException("No queda una capa disponible para Obstaculos.");
    }
    static Sprite MakeSprite(Texture2D tex,int index)
    {
        int cw=tex.width/3,ch=tex.height/2,x0=(index%3)*cw,y0=(1-index/3)*ch;
        int minX=cw,minY=ch,maxX=0,maxY=0;
        for(int y=0;y<ch;y++)for(int x=0;x<cw;x++)if(tex.GetPixel(x0+x,y0+y).a>.1f){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
        minX=Math.Max(0,minX-2);minY=Math.Max(0,minY-2);maxX=Math.Min(cw-1,maxX+2);maxY=Math.Min(ch-1,maxY+2);
        Rect rect=new Rect(x0+minX,y0+minY,maxX-minX+1,maxY-minY+1);
        var tile=ScriptableObject.CreateInstance<Tile>();tile.name=Names[index];tile.colliderType=Tile.ColliderType.None;
        AssetDatabase.CreateAsset(tile,Root+"/"+Names[index]+".asset");
        var sprite=Sprite.Create(tex,rect,new Vector2(.5f,.1f),rect.width/Widths[index],0,SpriteMeshType.FullRect);sprite.name=Names[index];AssetDatabase.AddObjectToAsset(sprite,tile);tile.sprite=sprite;EditorUtility.SetDirty(tile);return sprite;
    }
    static GameObject MakePrefab(int index,Sprite sprite,Material material,int layer)
    {
        var go=new GameObject(Names[index]);var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sharedMaterial=material;sr.spriteSortPoint=SpriteSortPoint.Pivot;
        if(index<=1){var c=go.AddComponent<BoxCollider2D>();c.size=new Vector2(Widths[index]*.78f,1.8f);c.offset=new Vector2(0,.75f);go.layer=layer;}
        if(index==2){var c=go.AddComponent<CapsuleCollider2D>();c.direction=CapsuleDirection2D.Horizontal;c.size=new Vector2(1.85f,.9f);c.offset=new Vector2(0,.35f);go.layer=layer;}
        if(index==3){var c=go.AddComponent<CircleCollider2D>();c.radius=.32f;c.offset=new Vector2(0,.2f);go.layer=layer;}
        // Original temporary authoring object becomes the prefab instance; no source asset is removed.
        var prefab=PrefabUtility.SaveAsPrefabAssetAndConnect(go,Root+"/"+Names[index]+".prefab",InteractionMode.AutomatedAction);
        go.SetActive(false);go.name="Plantilla_"+Names[index];return go;
    }
    static GameObject Place(GameObject template,Transform parent,float x,float y,float scale=1)
    {
        var asset=PrefabUtility.GetCorrespondingObjectFromSource(template);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);go.name=asset.name;go.transform.position=new Vector3(x,y,0);go.transform.localScale=Vector3.one*scale;go.SetActive(true);
        go.GetComponent<SpriteRenderer>().sortingOrder=1000-Mathf.RoundToInt(y*10);return go;
    }
    static void Boundary(Transform parent,string name,Vector2 pos,Vector2 size,int layer)
    {var g=new GameObject(name);g.transform.SetParent(parent);g.transform.position=pos;g.layer=layer;g.AddComponent<BoxCollider2D>().size=size;}
    [MenuItem("Chasqui/Decorar aldea (Paso 4)")]
    public static void Build()
    {
        var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/SenderoDeLaAldea.unity"||scene.isDirty)throw new InvalidOperationException("Guarde SenderoDeLaAldea antes de decorar.");
        if(Directory.Exists(Root)||GameObject.Find("Aldea_Entorno")!=null)throw new InvalidOperationException("La aldea ya existe; no se sobrescribira.");
        int layer=ObstacleLayer();Directory.CreateDirectory(Root);AssetDatabase.Refresh();
        var importer=(TextureImporter)AssetImporter.GetAtPath(Atlas);importer.textureType=TextureImporterType.Default;importer.npotScale=TextureImporterNPOTScale.None;importer.isReadable=true;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
        var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Atlas);var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Tilemaps/Sendero/TerrenoUnlit.mat");
        var root=new GameObject("Aldea_Entorno");var houses=new GameObject("Casas").transform;houses.SetParent(root.transform);var rocks=new GameObject("Rocas").transform;rocks.SetParent(root.transform);var trees=new GameObject("Arboles").transform;trees.SetParent(root.transform);
        var templates=new GameObject("Plantillas_Inactivas");templates.transform.SetParent(root.transform);templates.SetActive(false);
        var prefabs=new GameObject[6];for(int i=0;i<6;i++){prefabs[i]=MakePrefab(i,MakeSprite(tex,i),mat,layer);prefabs[i].transform.SetParent(templates.transform);}
        Place(prefabs[0],houses,-11,3);Place(prefabs[1],houses,-3.5f,5);Place(prefabs[0],houses,11.5f,1.7f,.9f);
        Vector2[] rockPositions={new(-8,-4.7f),new(.5f,-6),new(13,-8),new(14,8),new(-15,7.5f),new(2,8),new(-16,-8.5f),new(7,-9),new(16,-.5f)};
        for(int i=0;i<rockPositions.Length;i++)Place(prefabs[2],rocks,rockPositions[i].x,rockPositions[i].y,.7f+(i%3)*.17f);
        Vector2[] treePositions={new(-16.5f,-10.5f),new(-12.8f,-10.8f),new(-3,-10.5f),new(1,-10.8f),new(5,-10.5f),new(10,-10.7f),new(15.8f,-10.4f),new(-16.7f,2.5f),new(-16.5f,5.5f),new(-16,9),new(-11.5f,9.2f),new(-7,9),new(-2.5f,9.6f),new(3,9.2f),new(16.3f,6.5f),new(16.5f,2.6f),new(16.2f,-8),new(-6.8f,4),new(8.8f,-6.7f)};
        for(int i=0;i<treePositions.Length;i++)Place(prefabs[3],trees,treePositions[i].x,treePositions[i].y,.85f+(i%3)*.1f);
        var decoration=GameObject.Find("Decoracion").GetComponent<Tilemap>();
        var maize=AssetDatabase.LoadAssetAtPath<Tile>(Root+"/Maiz.asset");var shrub=AssetDatabase.LoadAssetAtPath<Tile>(Root+"/Arbusto.asset");
        Vector3Int[] crops={new(-13,-8,0),new(-10,-8,0),new(-13,-6,0),new(-10,-6,0),new(2,4,0),new(2,6,0)};
        foreach(var cell in crops)decoration.SetTile(cell,maize);
        Vector3Int[] bushes={new(-14,1,0),new(-8,2,0),new(-1,4,0),new(8,1,0),new(14,0,0),new(-5,-5,0),new(4,-6,0),new(12,-6,0),new(-15,-10,0),new(8,9,0),new(-7,8,0),new(14,6,0)};
        foreach(var cell in bushes)decoration.SetTile(cell,shrub);decoration.CompressBounds();
        // Keep obstacle objects under the existing dedicated Tilemap for clear organization.
        root.transform.SetParent(GameObject.Find("Obstaculos").transform,false);
        var limits=new GameObject("Limites_Jugables").transform;limits.SetParent(root.transform);
        Boundary(limits,"Limite_Norte",new Vector2(0,12.2f),new Vector2(37,.4f),layer);
        Boundary(limits,"Limite_Sur",new Vector2(0,-12.2f),new Vector2(37,.4f),layer);
        Boundary(limits,"Limite_Oeste",new Vector2(-18.2f,0),new Vector2(.4f,25),layer);
        Boundary(limits,"Limite_Este",new Vector2(18.2f,0),new Vector2(.4f,25),layer);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Physics2D.SyncTransforms();
        Validate();Selection.activeGameObject=root;Debug.Log("CHASQUI PASO 4: 3 casas, 9 grupos de rocas, 19 arboles, cultivos, arbustos y 4 limites guardados.");
    }
    [MenuItem("Chasqui/Verificar colisiones de la aldea")]
    public static void Validate()
    {
        Physics2D.SyncTransforms();int mask=1<<LayerMask.NameToLayer("Obstaculos");int count=0;
        foreach(var c in GameObject.Find("Aldea_Entorno").GetComponentsInChildren<Collider2D>())if(c.gameObject.layer==LayerMask.NameToLayer("Obstaculos")){
            if(c.isTrigger||!c.enabled||!c.OverlapPoint(c.bounds.center))throw new InvalidOperationException("Collider incorrecto: "+c.name);count++;
        }
        for(float x=-16.5f;x<=16.5f;x+=.25f){float y=-1.5f+2.4f*Mathf.Sin((x+11)*.18f);if(Physics2D.OverlapCircle(new Vector2(x,y),.4f,mask)!=null)throw new InvalidOperationException("Camino bloqueado cerca de "+x);}
        Vector2[] branch={new(4,0),new(6,6),new(11,8)};
        for(int i=0;i<2;i++)for(int j=0;j<=24;j++) {
            Vector2 sample=Vector2.Lerp(branch[i],branch[i+1],j/24f);
            if(Physics2D.OverlapCircle(sample,.4f,mask)!=null)throw new InvalidOperationException("Desvio bloqueado en "+sample);
        }
        if(count!=35)throw new InvalidOperationException("Se esperaban 35 colliders activos, encontrados: "+count);
        File.WriteAllText("Assets/Documentation/VerificacionPaso4.txt","35 colliders estaticos activos, no triggers; bases verificadas con Physics2D. Camino central libre para radio 0.4 en 133 muestras; desvio verificado en 50 muestras. 4 limites perimetrales. Prueba de movimiento con personaje pendiente de pasos 5-7.");
        Debug.Log("CHASQUI VALIDACION OK: 35 colliders y camino central despejado.");
    }
}
