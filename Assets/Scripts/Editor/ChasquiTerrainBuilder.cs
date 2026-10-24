using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Editor-only authoring. Generates original terrain assets once; no runtime generation.
[InitializeOnLoad]
public static class ChasquiTerrainBuilder
{
    const string Root = "Assets/Tilemaps/Sendero";
    const string Done = "Library/ChasquiPaso3.done";
    const int W=36, H=24, P=32;
    static ChasquiTerrainBuilder() { EditorApplication.delayCall += AutoBuild; }
    static void AutoBuild()
    {
        if (File.Exists(Done) && !File.Exists("Library/ChasquiPaso3Path.done")) { RepairPath(); return; }
        if (File.Exists(Done) || SessionState.GetBool("ChasquiTerrainAttempt",false)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += AutoBuild; return; }
        SessionState.SetBool("ChasquiTerrainAttempt",true);
        try { Build(); } catch(Exception e) { Debug.LogException(e); }
    }
    static float Distance(Vector2 p, Vector2 a, Vector2 b)
    { Vector2 v=b-a; return Vector2.Distance(p,a+v*Mathf.Clamp01(Vector2.Dot(p-a,v)/v.sqrMagnitude)); }
    static float RouteDistance(float x,float y)
    {
        var p=new Vector2(x,y); float d=100;
        Vector2 previous=new Vector2(-3,9);
        for(int i=1;i<=84;i++) { float t=i/84f; float xx=-3+42*t; var next=new Vector2(xx,10.5f+2.4f*Mathf.Sin((xx-7)*0.18f)); d=Mathf.Min(d,Distance(p,previous,next));previous=next; }
        // Quiet northern branch reserved for the future village.
        d=Mathf.Min(d,Distance(p,new Vector2(22,12),new Vector2(24,18))+.4f);
        d=Mathf.Min(d,Distance(p,new Vector2(24,18),new Vector2(29,20))+.4f);
        return d;
    }
    static Texture2D SaveTexture(Color[] pixels,int width,int height,string path)
    {
        var tex=new Texture2D(width,height,TextureFormat.RGBA32,false); tex.SetPixels(pixels); tex.Apply(); File.WriteAllBytes(path,tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.textureType=TextureImporterType.Default; importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.filterMode=FilterMode.Bilinear; importer.wrapMode=TextureWrapMode.Clamp; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.maxTextureSize=2048; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static Tile MakeTile(Texture2D tex,Rect rect,string name)
    {
        var tile=ScriptableObject.CreateInstance<Tile>(); tile.name=name; tile.colliderType=Tile.ColliderType.None;
        string path=Root+"/Tiles/"+name+".asset"; AssetDatabase.CreateAsset(tile,path);
        var sprite=Sprite.Create(tex,rect,new Vector2(.5f,.5f),P,0,SpriteMeshType.FullRect); sprite.name=name+"_Sprite"; AssetDatabase.AddObjectToAsset(sprite,tile); tile.sprite=sprite; EditorUtility.SetDirty(tile); return tile;
    }
    static Tilemap Map(Transform grid,string name,int order,Material material)
    {
        var obj=new GameObject(name,typeof(Tilemap),typeof(TilemapRenderer)); obj.transform.SetParent(grid,false);
        var renderer=obj.GetComponent<TilemapRenderer>(); renderer.sortingOrder=order; renderer.sharedMaterial=material;
        return obj.GetComponent<Tilemap>();
    }
    static void RepairPath()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += AutoBuild; return; }
        var path=GameObject.Find("Caminos_Tierra").GetComponent<Tilemap>();
        int aw=W*P,ah=H*P; var pixels=new Color[aw*ah];bool[,] occupied=new bool[W,H];
        for(int y=0;y<ah;y++) for(int x=0;x<aw;x++) {
            float wx=(x+.5f)/P,wy=(y+.5f)/P,n=Mathf.PerlinNoise(wx*3.2f+10,wy*3.2f+10);
            float d=RouteDistance(wx,wy),edge=1.65f+(n-.5f)*.16f;
            float alpha=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(edge-.05f,edge+.07f,d));
            if(alpha<=0)continue;occupied[x/P,y/P]=true;
            float border=Mathf.SmoothStep(0,1,Mathf.InverseLerp(edge-.32f,edge-.04f,d));
            var c=Color.Lerp(new Color(.73f,.55f,.32f),new Color(.48f,.39f,.20f),border*.55f);
            c+=new Color(1,.88f,.63f,0)*((n-.5f)*.065f+Mathf.Clamp01(1-d/1.3f)*.055f);
            if((x*37+y*109)%991<5)c*=.91f;c.a=alpha;pixels[y*aw+x]=c;
        }
        var tex=SaveTexture(pixels,aw,ah,"Assets/Sprites/Terrain/SenderoOriginal.png");int count=0;
        for(int y=0;y<H;y++)for(int x=0;x<W;x++)if(occupied[x,y]) {
            string name="Sendero_"+x+"_"+y;
            var tile=AssetDatabase.LoadAssetAtPath<Tile>(Root+"/Tiles/"+name+".asset");
            if(tile==null)tile=MakeTile(tex,new Rect(x*P,y*P,P,P),name);
            path.SetTile(new Vector3Int(x-W/2,y-H/2,0),tile);count++;
        }
        path.RefreshAllTiles();path.CompressBounds();AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(path.gameObject.scene);EditorSceneManager.SaveScene(path.gameObject.scene);
        File.WriteAllText("Library/ChasquiPaso3Path.done",count.ToString());
        Debug.Log("CHASQUI: camino visible corregido, "+count+" tiles guardados.");
    }
    [MenuItem("Chasqui/Preparar terreno (Paso 3)")]
    public static void Build()
    {
        const string scenePath="Assets/Scenes/SenderoDeLaAldea.unity";
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!=scenePath || scene.isDirty) throw new InvalidOperationException("Abra y guarde SenderoDeLaAldea antes de preparar el terreno.");
        if(GameObject.Find("Terreno_Aldea")!=null || Directory.Exists(Root)) throw new InvalidOperationException("El terreno ya existe. No se sobrescribira.");
        Directory.CreateDirectory(Root+"/Tiles"); Directory.CreateDirectory("Assets/Sprites/Terrain"); AssetDatabase.Refresh();
        var grass=new Color[P*P*4];
        for(int v=0;v<4;v++) for(int y=0;y<P;y++) for(int x=0;x<P;x++) {
            float n=Mathf.PerlinNoise((x+v*P)*.15f,y*.15f); float tone=(n-.5f)*.055f;
            Color c=new Color(.38f+tone,.55f+tone,.27f+tone*.6f,1);
            // Restrained tiny grass strokes, baked into the floor tiles.
            int hash=(x*73+y*131+v*977)%541; if(hash<3)c=new Color(.53f,.65f,.33f,1);
            grass[y*(P*4)+v*P+x]=c;
        }
        Texture2D grassTex=SaveTexture(grass,P*4,P,"Assets/Sprites/Terrain/CespedOriginal.png");
        Tile[] floorTiles=new Tile[4]; for(int i=0;i<4;i++) floorTiles[i]=MakeTile(grassTex,new Rect(i*P,0,P,P),"Cesped_"+i);
        int aw=W*P, ah=H*P; var pathPixels=new Color[aw*ah]; bool[,] occupied=new bool[W,H];
        for(int y=0;y<ah;y++) for(int x=0;x<aw;x++) {
            float wx=(x+.5f)/P, wy=(y+.5f)/P;
            float n=Mathf.PerlinNoise(wx*3.2f+10,wy*3.2f+10); float d=RouteDistance(wx,wy); float edge=1.65f+(n-.5f)*.16f;
            float alpha=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(edge-.05f,edge+.07f,d));
            if(alpha<=0)continue;
            occupied[x/P,y/P]=true;
            float border=Mathf.SmoothStep(0,1,Mathf.InverseLerp(edge-.32f,edge-.04f,d)); float center=Mathf.Clamp01(1-d/1.3f);
            Color c=Color.Lerp(new Color(.73f,.55f,.32f),new Color(.48f,.39f,.20f),border*.55f);
            c+=new Color(1,.88f,.63f,0)*((n-.5f)*.065f+center*.055f);
            if((x*37+y*109)%991<5)c*=.91f;
            c.a=alpha;pathPixels[y*aw+x]=c;
        }
        Texture2D pathTex=SaveTexture(pathPixels,aw,ah,"Assets/Sprites/Terrain/SenderoOriginal.png");
        Shader shader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if(shader==null) throw new InvalidOperationException("No se encontro el shader 2D de URP.");
        var material=new Material(shader); AssetDatabase.CreateAsset(material,Root+"/TerrenoUnlit.mat");
        var gridObj=new GameObject("Terreno_Aldea",typeof(Grid));
        var floor=Map(gridObj.transform,"Suelo_Cesped",-30,material);
        var path=Map(gridObj.transform,"Caminos_Tierra",-20,material);
        Map(gridObj.transform,"Decoracion",-10,material); Map(gridObj.transform,"Obstaculos",0,material);
        AssetDatabase.StartAssetEditing();
        try { for(int y=0;y<H;y++) for(int x=0;x<W;x++) {
            var cell=new Vector3Int(x-W/2,y-H/2,0); floor.SetTile(cell,floorTiles[(x*13+y*7+x*y)%4]);
            // Continuous large-scale grass tint avoids a checkerboard appearance.
            float t=Mathf.PerlinNoise(x*.14f+2,y*.14f+3); floor.SetTileFlags(cell,TileFlags.None); floor.SetColor(cell,Color.Lerp(new Color(.91f,.96f,.85f),Color.white,t));
            if(occupied[x,y])path.SetTile(cell,MakeTile(pathTex,new Rect(x*P,y*P,P,P),"Sendero_"+x+"_"+y));
        } } finally { AssetDatabase.StopAssetEditing(); }
        floor.CompressBounds();path.CompressBounds();
        var cam=Camera.main; if(cam!=null) { cam.orthographic=true;cam.orthographicSize=13.5f;cam.transform.position=new Vector3(0,0,-10);cam.backgroundColor=new Color(.16f,.24f,.17f);cam.clearFlags=CameraClearFlags.SolidColor; }
        if(SceneView.lastActiveSceneView!=null) { SceneView.lastActiveSceneView.in2DMode=true;SceneView.lastActiveSceneView.LookAt(Vector3.zero,Quaternion.identity,23,true,true); }
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Done,"Paso 3 completado. 36x24, suelo y camino; decoracion y obstaculos vacios.");
        Selection.activeGameObject=gridObj;
        Debug.Log("CHASQUI PASO 3 OK: 864 casillas de cesped, sendero continuo, escena guardada.");
    }
}
