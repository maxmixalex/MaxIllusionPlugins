using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace FlatLockBand
{
 [BepInPlugin("MaxMixAlex.FlatLockBand","FlatLock Elastic Band","5.4.0")]
 [BepInDependency("com.deathweasel.bepinex.uncensorselector",BepInDependency.DependencyFlags.HardDependency)]
 public sealed class FlatLockBandPlugin : BaseUnityPlugin
 {
  const float W=.0175f,T=.0018f,PITCH=.104f;
  ConfigEntry<bool> enabled,printed;
  ConfigEntry<string> fabric,snap,word,spade;
  ConfigEntry<KeyboardShortcut> hotkey;
  readonly Dictionary<int,Active> active=new Dictionary<int,Active>();
  Texture2D wordTex,spadeTex,clothTex;
  bool show;
  float nextScan;
  Rect window=new Rect(26,65,390,520);
  Color fColor,sColor,wColor,qColor;
  sealed class Active {
   public SkinnedMeshRenderer source,band;
   public Mesh srcMesh,mesh;
   public GameObject obj;
   public string guid;
   public Material[] materials;
  }
  void Awake()
  {
   enabled=Config.Bind("Band","Enabled",true,"Integrated elastic strap on selected FlatLock models.");
   printed=Config.Bind("Band","BLACKED + Q-spade print",true,"Use repeated BLACKED and Queen of Spades textile pattern.");
   fabric=Config.Bind("Colors","Band","#171719","Hex RGB of elastic fabric.");
   snap=Config.Bind("Colors","Snap buttons","#D5D5D5","Hex RGB of metal press studs.");
   word=Config.Bind("Colors","BLACKED text","#FFFFFF","Independent lettering tint.");
   spade=Config.Bind("Colors","Q spade","#FFFFFF","Independent playing-card emblem tint.");
   hotkey=Config.Bind("UI","Toggle color editor",new KeyboardShortcut(KeyCode.F8),"F8 opens the in-game four-channel editor.");
   wordTex=ReadPng("BLACKED_word.png");
   spadeTex=ReadPng("QSpade_symbol.png");
   clothTex=new Texture2D(32,32,TextureFormat.ARGB32,false);
   for(int y=0;y<32;y++)for(int x=0;x<32;x++){float v=.77f+((x%4)==0?.04f:0)+((y%4)==0?.04f:0);clothTex.SetPixel(x,y,new Color(v,v,v,1));}
   clothTex.Apply();clothTex.wrapMode=TextureWrapMode.Repeat;
   LoadColors();
   Logger.LogInfo("FlatLock Band 5.4 active; F8 for colors.");
  }
  Texture2D ReadPng(string name)
  {
   try {
    string file=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),name);
    if(!File.Exists(file)){Logger.LogWarning("Missing "+name);return null;}
    Texture2D t=new Texture2D(2,2,TextureFormat.ARGB32,false);
    if(!t.LoadImage(File.ReadAllBytes(file))){Destroy(t);return null;}
    t.wrapMode=TextureWrapMode.Repeat;t.filterMode=FilterMode.Bilinear;t.name=name;
    return t;
   } catch(Exception ex){Logger.LogWarning("Texture "+name+": "+ex.Message);return null;}
  }
  static Color Hex(string s,Color fallback)
  {
   if(String.IsNullOrEmpty(s))return fallback;
   s=s.Trim().TrimStart('#');
   if(s.Length!=6)return fallback;
   int n;
   if(!Int32.TryParse(s,System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out n))return fallback;
   return new Color(((n>>16)&255)/255f,((n>>8)&255)/255f,(n&255)/255f,1);
  }
  static string ToHex(Color c){return String.Format("#{0:X2}{1:X2}{2:X2}",Mathf.RoundToInt(Mathf.Clamp01(c.r)*255),Mathf.RoundToInt(Mathf.Clamp01(c.g)*255),Mathf.RoundToInt(Mathf.Clamp01(c.b)*255));}
  void LoadColors(){fColor=Hex(fabric.Value,Color.black);sColor=Hex(snap.Value,Color.white);wColor=Hex(word.Value,Color.white);qColor=Hex(spade.Value,Color.white);}
  void Update()
  {
   if(hotkey.Value.IsDown()){show=!show;LoadColors();}
   foreach(var entry in active)UpdateMaterials(entry.Value);
   if(Time.unscaledTime<nextScan)return;
   nextScan=Time.unscaledTime+.7f;
   var found=new HashSet<int>();
   foreach(ChaControl c in FindObjectsOfType<ChaControl>())
   {
    if(c==null)continue;
    int id=c.GetInstanceID();found.Add(id);
    string guid=PenisGuid(c);
    SkinnedMeshRenderer target=null;
    if(enabled.Value && guid!=null && guid.StartsWith("maxmixalex.flatlock.",StringComparison.OrdinalIgnoreCase) && !guid.EndsWith("source_flat",StringComparison.OrdinalIgnoreCase) && c.objBody!=null)
    {
     foreach(var r in c.objBody.GetComponentsInChildren<SkinnedMeshRenderer>(true))
      if(r.name=="o_dankon" || r.name=="cm_o_dan00"){target=r;break;}
    }
    if(target==null || target.sharedMesh==null || target.sharedMesh.vertexCount<15000){Drop(id);continue;}
    Active current;
    if(active.TryGetValue(id,out current) && current.source==target && current.srcMesh==target.sharedMesh && current.guid==guid && current.obj!=null)continue;
    Drop(id);
    try{active[id]=Attach(target,guid);Logger.LogInfo("Attached elastic to "+guid);}
    catch(Exception ex){Logger.LogWarning("Failed to attach elastic "+guid+": "+ex);}
   }
   var absent=new List<int>();
   foreach(var pair in active)if(!found.Contains(pair.Key))absent.Add(pair.Key);
   foreach(int id in absent)Drop(id);
  }
  static string PenisGuid(ChaControl chara)
  {
   foreach(MonoBehaviour mb in chara.GetComponents<MonoBehaviour>())
   {
    if(mb==null || mb.GetType().Name!="UncensorSelectorController")continue;
    try{PropertyInfo p=mb.GetType().GetProperty("PenisGUID",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
      return p==null?null:p.GetValue(mb,null) as string;}
    catch{return null;}
   }
   return null;
  }
  Active Attach(SkinnedMeshRenderer src,string guid)
  {
   bool ap=guid.IndexOf(".ap",StringComparison.OrdinalIgnoreCase)>=0;
   GameObject obj=new GameObject("FlatLockBand_Integrated_v54");
   obj.transform.SetParent(src.transform,false);
   obj.transform.localPosition=Vector3.zero;obj.transform.localRotation=Quaternion.identity;obj.transform.localScale=Vector3.one;
   SkinnedMeshRenderer sr=obj.AddComponent<SkinnedMeshRenderer>();
   Mesh mesh=new Builder(src,ap).Build();
   sr.sharedMesh=mesh;sr.bones=src.bones;sr.rootBone=src.rootBone;
   sr.updateWhenOffscreen=true;sr.localBounds=new Bounds(new Vector3(0,.97f,-.05f),new Vector3(.5f,.4f,.6f));
   Shader standard=Shader.Find("Legacy Shaders/Diffuse");
   if(standard==null)standard=Shader.Find("Standard");
   Shader transparent=Shader.Find("Unlit/Transparent");
   if(transparent==null)transparent=Shader.Find("Legacy Shaders/Transparent/Diffuse");
   if(transparent==null)transparent=standard;
   Material[] m=new Material[4];
   string[] names={"FlatLock_Elastic_Textile","FlatLock_Snap_Buttons","FlatLock_BLACKED_Letters","FlatLock_QSpade_Emblem"};
   for(int i=0;i<4;i++){m[i]=new Material(i<2?standard:transparent);m[i].name=names[i];}
   m[0].mainTexture=clothTex;m[2].mainTexture=wordTex==null?Texture2D.whiteTexture:wordTex;
   m[3].mainTexture=spadeTex==null?Texture2D.whiteTexture:spadeTex;
   sr.materials=m;
   var a=new Active{source=src,band=sr,srcMesh=src.sharedMesh,mesh=mesh,obj=obj,guid=guid,materials=m};
   UpdateMaterials(a);
   return a;
  }
  void UpdateMaterials(Active a)
  {
   if(a==null || a.band==null || a.materials==null)return;
   Color c0=show?fColor:Hex(fabric.Value,fColor),c1=show?sColor:Hex(snap.Value,sColor);
   Color c2=show?wColor:Hex(word.Value,wColor),c3=show?qColor:Hex(spade.Value,qColor);
   c2.a=c3.a=(printed.Value&&wordTex!=null&&spadeTex!=null)?1:0;
   a.materials[0].color=c0;a.materials[1].color=c1;a.materials[2].color=c2;a.materials[3].color=c3;
  }
  void Drop(int id)
  {
   Active a;if(!active.TryGetValue(id,out a))return;
   if(a.obj!=null)Destroy(a.obj);
   if(a.mesh!=null)Destroy(a.mesh);
   if(a.materials!=null)foreach(Material m in a.materials)if(m!=null)Destroy(m);
   active.Remove(id);
  }
  void OnDestroy()
  {
   foreach(int id in new List<int>(active.Keys))Drop(id);
   if(wordTex!=null)Destroy(wordTex);if(spadeTex!=null)Destroy(spadeTex);if(clothTex!=null)Destroy(clothTex);
  }
  void OnGUI(){if(show)window=GUI.Window(544024,window,DrawEditor,"FlatLock v5.4 · independent colors");}
  void DrawEditor(int id)
  {
   GUILayout.Label("F8 closes editor. Four independent channels.");
   enabled.Value=GUILayout.Toggle(enabled.Value,"Integrated elastic strap");
   printed.Value=GUILayout.Toggle(printed.Value,"BLACKED + Q♠ repeat");
   DrawChannel("Fabric",ref fColor);DrawChannel("Snap buttons",ref sColor);
   DrawChannel("BLACKED lettering",ref wColor);DrawChannel("Q-spade logo",ref qColor);
   GUILayout.BeginHorizontal();
   if(GUILayout.Button("Save")){fabric.Value=ToHex(fColor);snap.Value=ToHex(sColor);word.Value=ToHex(wColor);spade.Value=ToHex(qColor);Config.Save();}
   if(GUILayout.Button("Reset")){fColor=Hex("#171719",Color.black);sColor=Hex("#D5D5D5",Color.white);wColor=Color.white;qColor=Color.white;}
   GUILayout.EndHorizontal();
   GUILayout.Label("Snap geometry + print on same penis model.\nNo standalone waist accessory. Runtime fit is experimental.");
   GUI.DragWindow(new Rect(0,0,10000,22));
  }
  static void DrawChannel(string label,ref Color c)
  {
   GUILayout.Label(label+"  "+ToHex(c));
   GUILayout.BeginHorizontal();GUILayout.Label("R",GUILayout.Width(12));c.r=GUILayout.HorizontalSlider(c.r,0,1);
   GUILayout.Label("G",GUILayout.Width(12));c.g=GUILayout.HorizontalSlider(c.g,0,1);
   GUILayout.Label("B",GUILayout.Width(12));c.b=GUILayout.HorizontalSlider(c.b,0,1);GUILayout.EndHorizontal();
  }
  sealed class Builder
  {
   const float Width=.0175f,Thickness=.0018f,Repeat=.104f;
   readonly SkinnedMeshRenderer src;
   readonly List<Vector3> vertices=new List<Vector3>();
   readonly List<Vector2> uvs=new List<Vector2>();
   readonly List<BoneWeight> weights=new List<BoneWeight>();
   readonly List<int>[] tris={new List<int>(),new List<int>(),new List<int>(),new List<int>()};
   readonly List<F> frames=new List<F>();
   BoneWeight rootWeight,waistWeight;
   float length;
   struct F{public Vector3 p,t,w,n;public float s;}
   public Builder(SkinnedMeshRenderer r,bool ap)
   {
    src=r;
    BoneWeight[] bw=r.sharedMesh.boneWeights;
    rootWeight=bw.Length>14850?bw[14850]:new BoneWeight{boneIndex0=0,weight0=1};
    waistWeight=rootWeight;
    for(int i=0;i<r.bones.Length;i++)if(r.bones[i]!=null)
    {
     string n=r.bones[i].name.ToLowerInvariant();
     if(n.Contains("waist")||n.Contains("kosi")){waistWeight=new BoneWeight{boneIndex0=i,weight0=1};break;}
    }
    float x=ap?.02815f:.029f,y=ap?.93923f:.94387f,z=ap?.03431f:.03237f;
    Vector3[] control={
     new Vector3(-x,y,z),new Vector3(-.041f,.957f,z+.001f),new Vector3(-.070f,.971f,.046f),
     new Vector3(-.104f,.979f,.025f),new Vector3(-.132f,.981f,-.009f),
     new Vector3(-.145f,.984f,-.055f),new Vector3(-.131f,.984f,-.102f),
     new Vector3(-.087f,.984f,-.132f),new Vector3(0,.985f,-.144f),
     new Vector3(.087f,.984f,-.132f),new Vector3(.131f,.984f,-.102f),
     new Vector3(.145f,.984f,-.055f),new Vector3(.132f,.981f,-.009f),
     new Vector3(.104f,.979f,.025f),new Vector3(.070f,.971f,.046f),
     new Vector3(.041f,.957f,z+.001f),new Vector3(x,y,z)};
    var pts=new List<Vector3>();
    for(int i=0;i<control.Length-1;i++)for(int k=0;k<12;k++)
    {float t=k/12f,t2=t*t,t3=t2*t;Vector3 a=control[Math.Max(0,i-1)],b=control[i],c=control[i+1],d=control[Math.Min(control.Length-1,i+2)];
     pts.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t2+(-a+3*b-3*c+d)*t3));}
    pts.Add(control[control.Length-1]);
    for(int i=0;i<pts.Count;i++)
    {
     if(i>0)length+=Vector3.Distance(pts[i],pts[i-1]);
     Vector3 tan=(pts[Math.Min(pts.Count-1,i+1)]-pts[Math.Max(0,i-1)]).normalized;
     Vector3 w=(Vector3.up-tan*Vector3.Dot(Vector3.up,tan)).normalized;
     frames.Add(new F{p=pts[i],t=tan,w=w,n=Vector3.Cross(tan,w).normalized,s=length});
    }
   }
   BoneWeight BW(float s)
   {
    float k=Mathf.Clamp01(Math.Min(s,length-s)/.12f);
    if(rootWeight.boneIndex0==waistWeight.boneIndex0)return rootWeight;
    return new BoneWeight{boneIndex0=rootWeight.boneIndex0,weight0=1-k,boneIndex1=waistWeight.boneIndex0,weight1=k};
   }
   int V(Vector3 p,Vector2 uv,float s){int i=vertices.Count;vertices.Add(p);uvs.Add(uv);weights.Add(BW(s));return i;}
   void Q(int a,int b,int c,int d,int mat)
   {tris[mat].Add(a);tris[mat].Add(b);tris[mat].Add(c);tris[mat].Add(a);tris[mat].Add(c);tris[mat].Add(d);}
   void Ribbon(int start,int end,float lift,float width,float thickness)
   {
    int prev=-1;
    for(int i=start;i<=end;i++)
    {
     F f=frames[i];Vector3 p=f.p+f.n*lift;
     int a=V(p-f.w*width/2-f.n*thickness/2,new Vector2(f.s/Repeat,0),f.s);
     int b=V(p+f.w*width/2-f.n*thickness/2,new Vector2(f.s/Repeat,1),f.s);
     int c=V(p+f.w*width/2+f.n*thickness/2,new Vector2(f.s/Repeat,1),f.s);
     int d=V(p-f.w*width/2+f.n*thickness/2,new Vector2(f.s/Repeat,0),f.s);
     if(prev>=0)for(int j=0;j<4;j++)Q(prev+j,prev+(j+1)%4,a+(j+1)%4,a+j,0);
     prev=a;
    }
   }
   void Print(int start,int end,int layer,float lift)
   {
    int prev=-1,prevR=-1;
    for(int i=start;i<=end;i++)
    {
     F f=frames[i],dummy=f;Vector3 p=f.p+f.n*lift;
     int a=V(p-f.w*Width*.47f,new Vector2(f.s/Repeat,0),f.s);
     int b=V(p+f.w*Width*.47f,new Vector2(f.s/Repeat,1),f.s);
     if(prev>=0){Q(prev,a,b,prevR,layer);Q(prevR,b,a,prev,layer);}
     prev=a;prevR=b;
    }
   }
   int At(float s){int best=0;float delta=float.MaxValue;
    for(int i=0;i<frames.Count;i++){float d=Mathf.Abs(frames[i].s-s);if(d<delta){delta=d;best=i;}}return best;}
   void Button(int i)
   {
    F f=frames[i];Vector3 c=f.p+f.n*.0044f;int N=18;
    int[] a=new int[N],b=new int[N];
    for(int k=0;k<N;k++)
    {
     float t=2*Mathf.PI*k/N;Vector3 v=f.w*Mathf.Cos(t)+f.t*Mathf.Sin(t);
     a[k]=V(c+v*.00255f,new Vector2(.5f,.5f),f.s);
     b[k]=V(c+v*.00255f+f.n*.0012f,new Vector2(.5f,.5f),f.s);
    }
    int center=V(c+f.n*.0012f,new Vector2(.5f,.5f),f.s);
    for(int k=0;k<N;k++){int j=(k+1)%N;Q(a[k],a[j],b[j],b[k],1);tris[1].Add(center);tris[1].Add(b[k]);tris[1].Add(b[j]);}
   }
   public Mesh Build()
   {
    Ribbon(0,frames.Count-1,0,Width,Thickness);
    Ribbon(0,At(.082f),.0025f,Width*.96f,Thickness*.7f);
    Ribbon(At(length-.082f),frames.Count-1,.0025f,Width*.96f,Thickness*.7f);
    float[] studs={.016f,.045f,.074f};
    foreach(float d in studs){Button(At(d));Button(At(length-d));}
    int s=At(.014f),e=At(.225f),u=At(length-.225f),v=At(length-.014f);
    Print(s,e,2,.0049f);Print(u,v,2,.0049f);
    Print(s,e,3,.0051f);Print(u,v,3,.0051f);
    Mesh m=new Mesh();m.name="FlatLockBand_ElasticSnap_5_4";
    m.vertices=vertices.ToArray();m.uv=uvs.ToArray();m.boneWeights=weights.ToArray();m.bindposes=src.sharedMesh.bindposes;
    m.subMeshCount=4;for(int k=0;k<4;k++)m.SetTriangles(tris[k].ToArray(),k);
    m.RecalculateNormals();m.RecalculateBounds();return m;
   }
  }
 }
}
