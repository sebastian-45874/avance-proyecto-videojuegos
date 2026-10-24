using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
[InitializeOnLoad]
public static class ChasquiEnemyArtSetup
{
    const string Atlas="Assets/Sprites/Enemies/EnemigosDelDocumento.png";
    static ChasquiEnemyArtSetup(){EditorApplication.delayCall+=Apply;}
    [MenuItem("Chasqui/Aplicar enemigos del PDF")]
    static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(EditorApplication.isUpdating||EditorApplication.isCompiling){EditorApplication.delayCall+=Apply;return;}
        var scene=EditorSceneManager.GetActiveScene(); if(scene.path!="Assets/Scenes/ArenaDeLosQuipus.unity"||scene.isDirty)return;
        var arena=Object.FindAnyObjectByType<ChasquiArena>(); if(arena==null||arena.stoneSprite!=null)return;
        var importer=(TextureImporter)AssetImporter.GetAtPath(Atlas);if(importer==null)return;
        importer.textureType=TextureImporterType.Default; importer.isReadable=true;importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.SaveAndReimport();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Atlas);
        Sprite[] sprites=new Sprite[2];
        for(int i=0;i<2;i++) {
            int half=texture.width/2,minX=half,minY=texture.height,maxX=0,maxY=0;
            for(int y=0;y<texture.height;y++)for(int x=0;x<half;x++)if(texture.GetPixel(i*half+x,y).a>.1f){minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);}
            var rect=new Rect(i*half+minX,minY,maxX-minX+1,maxY-minY+1);
            var sp=Sprite.Create(texture,rect,new Vector2(.5f,.08f),rect.height/(i==0?1.8f:2f),0,SpriteMeshType.FullRect);sp.name=i==0?"VigiaDePiedra":"HonderoDeSombra";
            AssetDatabase.CreateAsset(sp,"Assets/Sprites/Enemies/"+sp.name+".asset");sprites[i]=sp;
        }
        arena.stoneSprite=sprites[0];arena.slingerSprite=sprites[1];
        EditorUtility.SetDirty(arena);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("CHASQUI: enemigos del PDF aplicados, controles por clic guardados.");
    }
}
