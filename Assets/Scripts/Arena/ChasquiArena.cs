using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Single-scene prototype. All session state belongs to this component.
public sealed class ChasquiArena : MonoBehaviour
{
    public Transform player;
    public Sprite enemySprite;
    public Sprite stoneSprite, slingerSprite;
    public Material spriteMaterial;
    [Range(0,1)] public float eliteChance = .25f;
    public int maxEnemies = 18;
    public float spawnInterval = 2.5f;
    class Enemy { public GameObject go; public Rigidbody2D body; public SpriteRenderer sprite; public int hp; public bool elite, ranged; public float attackAt; public Vector2 lockedAim; public float chargeUntil; }
    class Shot { public GameObject go; public Vector2 velocity; public float life; public bool hostile; }
    class Loot { public GameObject go; public bool quipu; }
    readonly List<Enemy> enemies = new List<Enemy>();
    readonly List<Shot> shots = new List<Shot>();
    readonly List<Loot> loot = new List<Loot>();
    float nextSpawn, nextShot, hurtUntil, ultimateReady, elapsed;
    int health = 5, gold, kills, quipus, coca, vase, crown;
    bool choosing, ended;
    Vector2 destination; bool movingToClick; Enemy selectedEnemy; float clickGuard;
    ChasquiMovement movement;
    Sprite ringSprite, spearSprite, goldSprite, quipuSprite;
    SpriteRenderer aura;
    GUIStyle titleStyle, textStyle, buttonStyle;
    public int EnemyCount => enemies.Count;
    public int Gold => gold;
    public int Quipus => quipus;
    public int Kills => kills;
    public bool Choosing => choosing;
    void Start()
    {
        Time.timeScale = 1;
        movement = player.GetComponent<ChasquiMovement>(); movement.ClickOnly=true; destination=player.position;
        ringSprite = DrawSprite(0); spearSprite = DrawSprite(1); goldSprite = DrawSprite(2); quipuSprite = DrawSprite(3);
        var halo = Visual("Aura del Inti", ringSprite, player.position, new Color(1,.75f,.12f,.8f), 1.8f);
        halo.transform.SetParent(player, true); halo.transform.localPosition = new Vector3(0,.35f,0);
        aura = halo.GetComponent<SpriteRenderer>(); aura.enabled = false;
        nextSpawn = Time.time + 1;
    }
    // Small original procedural icons, no third-party art.
    Sprite DrawSprite(int kind)
    {
        var t = new Texture2D(64,64,TextureFormat.RGBA32,false); t.filterMode = FilterMode.Bilinear;
        for(int y=0;y<64;y++) for(int x=0;x<64;x++) {
            float dx=x-31.5f,dy=y-31.5f,r=Mathf.Sqrt(dx*dx+dy*dy); Color c=Color.clear;
            if(kind==0 && r>23 && r<30) c=Color.white;
            if(kind==1 && ((Mathf.Abs(dy)<2 && x>4 && x<49)||(x>=44 && x<61 && Mathf.Abs(dy)<(61-x)*.55f))) c=x<44?new Color(.48f,.28f,.12f):new Color(.85f,.93f,.9f);
            if(kind==2 && r<23) c=r>18?new Color(.65f,.36f,.06f):new Color(1,.8f,.16f);
            if(kind==3 && ((y>45&&y<50&&x>10&&x<55)||(y>10&&y<49&&(Mathf.Abs(x-17)<2||Mathf.Abs(x-31)<2||Mathf.Abs(x-46)<2))||((y==18||y==27||y==36)&&x>13&&x<50))) c=new Color(1,.66f,.3f);
            t.SetPixel(x,y,c);
        }
        t.Apply(); return Sprite.Create(t,new Rect(0,0,64,64),new Vector2(.5f,.5f),64);
    }
    GameObject Visual(string name, Sprite sprite, Vector3 position, Color color, float size)
    {
        var go=new GameObject(name); go.transform.position=position; go.transform.localScale=Vector3.one*size;
        var sr=go.AddComponent<SpriteRenderer>(); sr.sprite=sprite; sr.sharedMaterial=spriteMaterial; sr.color=color; sr.sortingOrder=1200;
        go.transform.SetParent(transform); return go;
    }
    void Update()
    {
        if(ended || choosing) return;
        elapsed+=Time.deltaTime;
        if(Time.time>=nextSpawn) { SpawnEnemy(); nextSpawn=Time.time+Mathf.Max(.8f,spawnInterval-elapsed/160); }
        if(Mouse.current!=null && Mouse.current.leftButton.wasPressedThisFrame && Time.unscaledTime>clickGuard) {
            Vector2 screen=Mouse.current.position.ReadValue();
            Vector2 ui=new Vector2(screen.x/Screen.width*1280,(1-screen.y/Screen.height)*720);
            if(!new Rect(1030,16,234,230).Contains(ui) && !new Rect(16,16,420,108).Contains(ui) && ui.y<664)
                WorldClick(Camera.main.ScreenToWorldPoint(screen));
        }
        if(selectedEnemy!=null && selectedEnemy.go!=null) {
            Vector2 d=selectedEnemy.go.transform.position-player.position;
            if(d.magnitude>9 || Physics2D.Linecast(player.position,selectedEnemy.go.transform.position,LayerMask.GetMask("Obstaculos"))) {
                destination=selectedEnemy.go.transform.position; movingToClick=true;
            } else { movingToClick=false; Attack(); }
        }
        Vector2 walk=destination-(Vector2)player.position;
        movement.SetTouchInput(movingToClick && walk.magnitude>.2f?walk.normalized:Vector2.zero);
        if(walk.magnitude<=.2f)movingToClick=false;
        for(int i=shots.Count-1;i>=0;i--) {
            var s=shots[i]; Vector2 from=s.go.transform.position; Vector2 to=from+s.velocity*Time.deltaTime;
            bool consumed=Physics2D.Linecast(from,to,LayerMask.GetMask("Obstaculos"));
            if(!consumed && s.hostile && Vector2.Distance(to,(Vector2)player.position+Vector2.up*.35f)<.5f) { Hurt(); consumed=true; }
            if(!consumed && !s.hostile) for(int j=enemies.Count-1;j>=0;j--) {
                Vector2 center=(Vector2)enemies[j].go.transform.position+Vector2.up*.4f;
                Vector2 delta=to-from; float t=delta.sqrMagnitude>0?Mathf.Clamp01(Vector2.Dot(center-from,delta)/delta.sqrMagnitude):0;
                if(Vector2.Distance(center,from+delta*t)<.5f) { DamageEnemy(j,1); consumed=true; break; }
            }
            s.go.transform.position=to; s.life-=Time.deltaTime;
            if(consumed||s.life<=0) { Destroy(s.go); shots.RemoveAt(i); }
        }
        for(int i=loot.Count-1;i>=0;i--) if(Vector2.Distance(player.position,loot[i].go.transform.position)<.85f) {
            bool q=loot[i].quipu; Destroy(loot[i].go); loot.RemoveAt(i);
            if(q) { quipus++; choosing=true; Time.timeScale=0; break; } else gold+=5;
        }
        if(aura!=null) { aura.enabled=crown>0; aura.sortingOrder=player.GetComponent<SpriteRenderer>().sortingOrder-1; }
    }
    void FixedUpdate()
    {
        if(ended||choosing) return;
        foreach(var e in enemies) {
            Vector2 d=(Vector2)player.position-(Vector2)e.go.transform.position;
            // Steer around static scenery rather than walking straight through it.
            Vector2 direction=d.normalized;
            if(Physics2D.CircleCast((Vector2)e.go.transform.position+Vector2.up*.2f,.3f,direction,1,LayerMask.GetMask("Obstaculos"))) {
                Vector2 left=new Vector2(-direction.y,direction.x), right=-left;
                direction=!Physics2D.CircleCast(e.go.transform.position,.3f,left,1,LayerMask.GetMask("Obstaculos"))?left:right;
            }
            if(e.ranged) {
                direction=d.magnitude<3?-direction:d.magnitude<6?Vector2.zero:direction;
                if(d.magnitude<9 && Time.time>=e.attackAt && !Physics2D.Linecast(e.go.transform.position,player.position,LayerMask.GetMask("Obstaculos"))) {
                    var stone=Visual("Piedra de honda",goldSprite,e.go.transform.position+Vector3.up*.4f,new Color(.5f,.47f,.42f),.3f);
                    shots.Add(new Shot{go=stone,velocity=d.normalized*4,life=3,hostile=true}); e.attackAt=Time.time+2.5f;
                }
                e.body.linearVelocity=direction*1.5f;
            } else {
                if(d.magnitude<4 && Time.time>=e.attackAt) { e.lockedAim=d.normalized; e.chargeUntil=Time.time+1.15f; e.attackAt=Time.time+3.5f; }
                float phase=e.chargeUntil-Time.time;
                e.body.linearVelocity=phase>.55f?Vector2.zero:phase>0?e.lockedAim*4:direction*.95f;
                e.sprite.color=phase>.55f?new Color(1,.55f,.35f):Color.white;
            }
            e.sprite.sortingOrder=1000-Mathf.RoundToInt(e.go.transform.position.y*10);
            if(d.magnitude<.8f) Hurt();
        }
    }
    public bool SpawnEnemy()
    {
        if(enemies.Count>=maxEnemies) return false;
        Vector2 pos=Vector2.zero; bool found=false;
        for(int i=0;i<40;i++) {
            pos=new Vector2(Random.Range(-16f,16f),Random.Range(-10f,10f));
            if(Vector2.Distance(pos,player.position)>6 && !Physics2D.OverlapCircle(pos+Vector2.up*.3f,.65f,LayerMask.GetMask("Obstaculos","Personaje"))) { found=true; break; }
        }
        if(!found) return false;
        bool elite=Random.value<eliteChance;
        bool ranged=Random.value<.5f;
        Sprite design=ranged?slingerSprite:stoneSprite;
        var go=Visual((ranged?"Hondero de Sombra":"Vigia de Piedra")+(elite?" - portador de quipu":""),design!=null?design:enemySprite,pos,Color.white,1f);
        var rb=go.AddComponent<Rigidbody2D>(); rb.gravityScale=0; rb.freezeRotation=true; rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        var collider=go.AddComponent<CircleCollider2D>(); collider.radius=.35f; collider.offset=new Vector2(0,.25f);
        if(elite) { var halo=Visual("Aura morada",ringSprite,pos+Vector2.up*.45f,new Color(.75f,.2f,1,.85f),1.6f); halo.transform.SetParent(go.transform,true); }
        enemies.Add(new Enemy{go=go,body=rb,sprite=go.GetComponent<SpriteRenderer>(),elite=elite,ranged=ranged,attackAt=Time.time+2,hp=(ranged?2:4)+(elite?2:0)}); return true;
    }
    public void Attack()
    {
        if(choosing||ended||Time.time<nextShot) return;
        nextShot=Time.time+.42f;
        Vector2 origin=(Vector2)player.position+Vector2.up*.45f, aim=Vector2.right;
        if(selectedEnemy!=null && selectedEnemy.go!=null) aim=(Vector2)selectedEnemy.go.transform.position+Vector2.up*.4f-origin;
        else if(Mouse.current!=null) aim=(Vector2)Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue())-origin;
        if(aim.sqrMagnitude<.01f) aim=Vector2.right; aim.Normalize();
        Fire(origin,aim);
        if(vase>0) Fire(origin+new Vector2(-aim.y,aim.x)*.32f,aim);
    }
    void Fire(Vector2 origin,Vector2 direction)
    {
        var go=Visual("Lanza",spearSprite,origin,Color.white,1.05f); go.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
        shots.Add(new Shot{go=go,velocity=direction*12,life=1.4f});
    }
    void DamageEnemy(int index,int amount)
    {
        var e=enemies[index]; e.hp-=amount; if(e.hp>0)return;
        var drop=Visual(e.elite?"Quipu":"Oro",e.elite?quipuSprite:goldSprite,e.go.transform.position+Vector3.up*.2f,Color.white,.7f);
        loot.Add(new Loot{go=drop,quipu=e.elite}); Destroy(e.go); enemies.RemoveAt(index); kills++;
    }
    public void ChoosePower(int index)
    {
        if(!choosing)return;
        if(index==0) { coca++; movement.SpeedMultiplier=1+Mathf.Min(coca,5)*.15f; }
        if(index==1) { vase=1; health=Mathf.Min(5,health+1); }
        if(index==2) { crown++; ultimateReady=Time.time; }
        choosing=false; Time.timeScale=1; clickGuard=Time.unscaledTime+.25f;
    }
    public void Ultimate()
    {
        if(crown==0||choosing||ended||Time.time<ultimateReady)return;
        ultimateReady=Time.time+15;
        for(int i=enemies.Count-1;i>=0;i--) if(Vector2.Distance(player.position,enemies[i].go.transform.position)<6) DamageEnemy(i,4+crown);
        var flash=Visual("Destello del Inti",ringSprite,player.position+Vector3.up*.4f,new Color(1,.8f,.1f,.7f),12); Destroy(flash,.35f);
    }
    public void WorldClick(Vector2 point)
    {
        if(choosing||ended)return;
        for(int i=loot.Count-1;i>=0;i--) if(Vector2.Distance(point,loot[i].go.transform.position)<.75f) {
            bool q=loot[i].quipu; Destroy(loot[i].go); loot.RemoveAt(i);
            if(q){quipus++;choosing=true;Time.timeScale=0;}else gold+=5;
            return;
        }
        selectedEnemy=null;
        foreach(var e in enemies) if(e.sprite.bounds.Contains(new Vector3(point.x,point.y,e.sprite.bounds.center.z))) {selectedEnemy=e;movingToClick=false;return;}
        if(Physics2D.OverlapCircle(point,.3f,LayerMask.GetMask("Obstaculos")))return;
        destination=new Vector2(Mathf.Clamp(point.x,-17.3f,17.3f),Mathf.Clamp(point.y,-11.3f,11.3f)); movingToClick=true;
    }
    void Hurt()
    {
        if(Time.time<hurtUntil||ended)return;
        health--; hurtUntil=Time.time+1.2f;
        if(health<=0){ended=true;Time.timeScale=0;}
    }
    void OnGUI()
    {
        if(titleStyle==null) { titleStyle=new GUIStyle(GUI.skin.label){fontSize=25,fontStyle=FontStyle.Bold}; textStyle=new GUIStyle(GUI.skin.label){fontSize=17,wordWrap=true}; buttonStyle=new GUIStyle(GUI.skin.button){fontSize=17,wordWrap=true}; }
        var previous=GUI.matrix; GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1280f,Screen.height/720f,1));
        GUI.Box(new Rect(16,16,420,108),""); GUI.Label(new Rect(30,23,400,32),"EL PEQUEÑO CHASQUI",titleStyle);
        GUI.Label(new Rect(30,60,400,55),$"Vida {health}/5   Oro {gold}   Quipus {quipus}\nDerrotados {kills}   Tiempo {Mathf.FloorToInt(elapsed)} s",textStyle);
        GUI.Box(new Rect(1030,16,234,230),""); GUI.Label(new Rect(1044,28,215,32),"PODERES DEL CHASQUI",textStyle);
        GUI.Label(new Rect(1044,66,210,40),$"COCA • Velocidad +{Mathf.Min(coca,5)*15}%",textStyle);
        GUI.Label(new Rect(1044,110,210,40),vase>0?"VASIJA • Doble lanza":"VASIJA • Sin desbloquear",textStyle);
        string status=crown==0?"Bloqueada":Time.time>=ultimateReady?"Lista":"Espera "+Mathf.CeilToInt(ultimateReady-Time.time)+" s";
        if(GUI.Button(new Rect(1044,164,205,62),"CORONA DEL INTI\n"+status,buttonStyle))Ultimate();
        GUI.Box(new Rect(16,664,1248,40),"Clic en suelo: caminar   |   Clic en enemigo: atacar   |   Clic en oro/quipu: recoger   |   Corona: especial");
        if(choosing) {
            GUI.Box(new Rect(215,220,850,270),""); GUI.Label(new Rect(250,240,780,45),"¡QUIPU RECUPERADO! Elige una bendición",titleStyle);
            if(GUI.Button(new Rect(245,310,245,140),"HOJA DE COCA\n+15% velocidad\nMáximo +75%",buttonStyle))ChoosePower(0);
            if(GUI.Button(new Rect(515,310,245,140),"VASIJA\nDos lanzas por ataque\nRecupera una vida",buttonStyle))ChoosePower(1);
            if(GUI.Button(new Rect(785,310,245,140),"CORONA DEL INTI\nAura dorada y poder con clic\nDaño en área cada 15 s",buttonStyle))ChoosePower(2);
        }
        if(ended) { GUI.Box(new Rect(390,245,500,220),""); GUI.Label(new Rect(425,270,440,70),"FIN DE LA DEFENSA\n"+kills+" enemigos • "+gold+" de oro",titleStyle); if(GUI.Button(new Rect(470,370,340,60),"VOLVER A INTENTAR",buttonStyle)){Time.timeScale=1;SceneManager.LoadScene(SceneManager.GetActiveScene().name);} }
        GUI.matrix=previous;
    }
    void OnDestroy(){Time.timeScale=1;}
}
