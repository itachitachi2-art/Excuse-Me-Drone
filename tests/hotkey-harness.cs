using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Itachi.ExcuseMeDrone;
namespace HarmonyLib { public class HarmonyPatch:Attribute { public HarmonyPatch(Type t,string n) {} } }
namespace UnityEngine {
 public enum KeyCode { None=0, At=64, Colon=58, F8=289,F9=290,F10=291,Home=278 }
 public struct Vector3 { public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 zero{get{return new Vector3();}}public static Vector3 up{get{return new Vector3(0,1,0);}}public static Vector3 forward{get{return new Vector3(0,0,1);}}public float sqrMagnitude{get{return x*x+y*y+z*z;}}public float magnitude{get{return (float)Math.Sqrt(sqrMagnitude);}}public Vector3 normalized{get{var a=this;a.Normalize();return a;}}public void Normalize(){float m=magnitude;if(m>0){x/=m;y/=m;z/=m;}}public static Vector3 operator+(Vector3 a,Vector3 b){return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}public static Vector3 operator-(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}public static Vector3 operator*(Vector3 a,float f){return new Vector3(a.x*f,a.y*f,a.z*f);} }
 public class Transform {public Vector3 forward=Vector3.forward;}
 public class Collider {public Entity entity;}
 public struct RaycastHit {public Collider collider;}
 public enum QueryTriggerInteraction {Ignore}
 public static class Physics {public const int DefaultRaycastLayers=1;public static RaycastHit[] Hits=new RaycastHit[0];public static RaycastHit[] RaycastAll(Vector3 a,Vector3 b,float c,int d,QueryTriggerInteraction e){return Hits;} }
 public static class Input {public static bool anyKeyDown=true;public static KeyCode Key=KeyCode.None;public static string inputString="";public static bool GetKeyDown(KeyCode k){return Key==k;} }
 public static class Time {public static int frameCount;public static float time;}
 public static class Debug {public static void Log(object o){}public static void LogWarning(object o){}public static void LogError(object o){} }
 public static class Mathf {public static float Clamp(float f,float a,float b){return Math.Min(b,Math.Max(a,f));} }
}
public class Entity {public int entityId;public Vector3 position;public Transform transform=new Transform();}
public class EntityPlayerLocal:Entity {public bool dead;public bool IsDead(){return dead;} }
public static class DroneWeapons {public class StunBeamWeapon{} }
public class EntityDrone:Entity {public int transitionState;public object activeWeapon;public List<object> weapons=new List<object>();public List<object> GetInstalledWeapons(){return weapons;}public Entity Owner;public int Health=100,state,followCalls,activationCalls,positionCalls,pathCalls;public string command;public Vector3 motion;public int GetState(){return state;}public void FollowMode(){followCalls++;}public void OnEntityActivated(EntityActivationCommand c,EntityPlayerLocal p){activationCalls++;command=c.name;}public void clearCurrentPath(){pathCalls++;}public void SetPosition(Vector3 p,bool physics){position=p;positionCalls++;} }
public class EntityActivationCommand {public string name;public EntityActivationCommand(string a,string b,string c,string d){name=a;} }
public class EntityCreationData {public int belongsPlayerId,id;}
public class World {public Dictionary<int,Entity> entities=new Dictionary<int,Entity>();public Entity GetEntity(int id){Entity e;return entities.TryGetValue(id,out e)?e:null;} }
public class GameManager {public static GameManager Instance;public World World;}
public class DroneManager {public static DroneManager Instance;public List<EntityCreationData> list=new List<EntityCreationData>();public List<EntityCreationData> GetAllDronesECD(){return list;} }
public class ConnectionManager {public static ConnectionManager Instance;public bool IsServer=true;}
public static class Constants {public const float cDigAndBuildDistance=4f;}
public static class Origin {public static Vector3 position;}
public class GUIWindow {}
public class XUiWindowGroup:GUIWindow {public object Controller;}
public class XUiC_BagStorageWindowGroup {public static string ID="storage";public Entity Entity;}
public class GUIWindowManager {public bool typing,open;public int closes;public GUIWindow modal;public XUiWindowGroup group;public bool IsUGUIInputActive(){return typing;}public GUIWindow GetWindow(string s){return group;}public bool IsWindowOpen(GUIWindow g){return open;}public GUIWindow GetModalWindow(){return modal;}public void Close(GUIWindow g,bool b){closes++;open=false;modal=null;} }
public class LocalPlayerUI {public EntityPlayerLocal entityPlayer;public object xui=new object();public GUIWindowManager windowManager=new GUIWindowManager();}
namespace Itachi.ExcuseMeDrone {internal static class ExcuseMeDroneController {internal static bool CanLock(EntityDrone d){return d.Health>1&&d.state!=5;}internal static bool Placement=true;internal static int Summons;internal static void SummonDrone(EntityDrone d,EntityPlayerLocal p,ExcuseMeDroneConfig c){Summons++;ExcuseMeDroneHotkeys.ReleaseWithoutSummon(d);}internal static bool TryPlacement(EntityPlayerLocal p,Vector3 v,EntityDrone d,ExcuseMeDroneConfig c,bool g,bool o,out Vector3 result){result=v;return Placement;}internal static bool IsEntityCollider(Collider c,Entity e){return c.entity==e;} } }
class HotkeyTests {
 static int checks;static EntityDrone drone;static EntityPlayerLocal player;static LocalPlayerUI ui;
 static void Check(bool b,string name){checks++;if(!b)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static bool Near(float a,float b){return Math.Abs(a-b)<0.001;}
 static void Setup(){ExcuseMeDroneHotkeys.Reset();ExcuseMeDroneConfig.Load("/nonexistent-test-config");Time.frameCount++;Time.time=10;Input.anyKeyDown=true;Input.Key=KeyCode.None;Input.inputString="";Physics.Hits=new RaycastHit[0];Origin.position=Vector3.zero;player=new EntityPlayerLocal{entityId=1};drone=new EntityDrone{entityId=2,Owner=player};GameManager.Instance=new GameManager{World=new World()};GameManager.Instance.World.entities[2]=drone;DroneManager.Instance=new DroneManager();DroneManager.Instance.list.Add(new EntityCreationData{belongsPlayerId=1,id=2});ConnectionManager.Instance=new ConnectionManager();ui=new LocalPlayerUI{entityPlayer=player};ExcuseMeDroneController.Placement=true;ExcuseMeDroneController.Summons=0;}
 static void Press(KeyCode k){Time.frameCount++;Input.Key=k;Input.inputString="";ExcuseMeDroneHotkeys.Handle(ui);Input.Key=KeyCode.None;}
 static void Lock(){Press(KeyCode.At);}
 static void Main(){
 Setup();Check(ExcuseMeDroneConfig.Current.BehindLockDistance==3,"default distance");Lock();Check(ExcuseMeDroneHotkeys.IsLocked(drone),"enable");Check(Near(drone.position.z,-3)&&Near(drone.position.y,1.1f),"default behind target");Press(KeyCode.At);Check(!ExcuseMeDroneHotkeys.IsLocked(drone)&&drone.followCalls==1&&ExcuseMeDroneController.Summons==1,"disable follow and front summon route");
 Setup();Lock();ExcuseMeDroneHotkeys.ReleaseWithoutSummon(drone);Check(!ExcuseMeDroneHotkeys.IsLocked(drone)&&ExcuseMeDroneController.Summons==0,"F10 release helper");
 Setup();Lock();ExcuseMeDroneHotkeys.Reset();Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"session reset");
 Setup();drone.Owner=new EntityPlayerLocal();Lock();Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"foreign owner rejected");
 Setup();GameManager.Instance.World.entities.Clear();Lock();Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"unloaded rejected");
 Setup();player.dead=true;Lock();Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"dead player rejected");
 Setup();drone.Health=1;Lock();Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"broken rejected");
 Setup();drone.state=5;Lock();Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"shutdown rejected");
 Setup();ConnectionManager.Instance.IsServer=false;Lock();Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"remote positioning rejected");
 Setup();Lock();player.dead=true;Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"death clears lock");
 Setup();Lock();drone.Health=1;Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"damage clears lock");
 Setup();Lock();GameManager.Instance.World.entities.Clear();Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"stale entity clears lock");
 Setup();ui.windowManager.typing=true;Lock();Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"typing guard");Press(KeyCode.Colon);Check(drone.activationCalls==0,"typing inventory guard");
 Setup();ui.windowManager.modal=new GUIWindow();Lock();Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"modal lock guard");Press(KeyCode.Colon);Check(drone.activationCalls==0,"modal storage guard");
 Setup();Press(KeyCode.Colon);Check(drone.activationCalls==1&&drone.command=="storage","inventory native route");
 Setup();drone.position=new Vector3(5,0,0);Press(KeyCode.Colon);Check(drone.activationCalls==1,"inventory boundary inclusive");
 Setup();drone.position=new Vector3(5.1f,0,0);Press(KeyCode.Colon);Check(drone.activationCalls==0,"distant inventory rejected");
 Setup();ui.windowManager.group=new XUiWindowGroup{Controller=new XUiC_BagStorageWindowGroup{Entity=drone}};ui.windowManager.open=true;ui.windowManager.modal=ui.windowManager.group;Press(KeyCode.Colon);Check(ui.windowManager.closes==1&&drone.activationCalls==0,"own inventory native close");
 Setup();ui.windowManager.group=new XUiWindowGroup{Controller=new XUiC_BagStorageWindowGroup{Entity=new EntityDrone()}};ui.windowManager.open=true;ui.windowManager.modal=ui.windowManager.group;Press(KeyCode.Colon);Check(ui.windowManager.closes==0&&drone.activationCalls==0,"foreign inventory preserved");
 Setup();ui.windowManager.group=new XUiWindowGroup{Controller=new XUiC_BagStorageWindowGroup{Entity=drone}};ui.windowManager.open=true;ui.windowManager.modal=new GUIWindow();Press(KeyCode.Colon);Check(ui.windowManager.closes==0,"overlaid modal preserved");
 Setup();Time.frameCount++;Input.inputString="@";ExcuseMeDroneHotkeys.Handle(ui);Check(ExcuseMeDroneHotkeys.IsLocked(drone),"JIS at fallback");ExcuseMeDroneHotkeys.Handle(ui);Check(ExcuseMeDroneHotkeys.IsLocked(drone),"one toggle per frame");
 Setup();Time.frameCount++;Input.anyKeyDown=false;Input.inputString="@";ExcuseMeDroneHotkeys.Handle(ui);Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"character auto-repeat ignored");
 Setup();Time.frameCount++;Input.inputString=":";ExcuseMeDroneHotkeys.Handle(ui);Check(drone.activationCalls==1,"JIS colon fallback");
 var p=ExcuseMeDroneHotkeys.BehindTarget(new Vector3(10,2,4),new Vector3(2,5,0),3,1);Check(Near(p.x,7)&&Near(p.y,3)&&Near(p.z,4),"horizontal normalized facing");p=ExcuseMeDroneHotkeys.BehindTarget(Vector3.zero,new Vector3(0,9,0),3,1);Check(Near(p.z,-3),"vertical facing fallback");
 Setup();Physics.Hits=new[]{new RaycastHit{collider=new Collider()}};Lock();Check(drone.positionCalls==0,"wall blocks placement");
 Setup();Physics.Hits=new[]{new RaycastHit{collider=new Collider{entity=player}},new RaycastHit{collider=new Collider{entity=drone}}};Lock();Check(drone.positionCalls==1,"owner and drone ray ignored");
 Setup();ExcuseMeDroneController.Placement=false;Lock();Check(drone.positionCalls==0,"clearance blocks placement");
 Setup();drone.motion=new Vector3(1,2,3);Lock();Check(drone.motion.sqrMagnitude==0&&drone.pathCalls==1,"movement and path reset");
 Setup();Lock();player.position=new Vector3(10,0,0);player.transform.forward=new Vector3(1,0,0);ExcuseMeDroneHotkeys.UpdateLocked(drone);Check(Near(drone.position.x,7),"follow updated position and facing");
 var method=typeof(DroneLockedStatePatch).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static);Check(!(bool)method.Invoke(null,new object[]{drone}),"locked state patch skips");ExcuseMeDroneHotkeys.ReleaseWithoutSummon(drone);Check((bool)method.Invoke(null,new object[]{drone}),"released state patch runs");
 Setup();Lock();var stun=new DroneWeapons.StunBeamWeapon();drone.weapons.Add(stun);drone.activeWeapon=stun;drone.state=4;Check(!ExcuseMeDroneHotkeys.IsLocked(drone)&&ExcuseMeDroneHotkeys.IsLockRequested(drone),"stun combat suspends but retains intent");int moves=drone.positionCalls;ExcuseMeDroneHotkeys.UpdateLocked(drone);Check(drone.positionCalls==moves,"combat does not reposition");Check((bool)method.Invoke(null,new object[]{drone}),"combat native state allowed");drone.state=0;Time.time=11.99f;Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"combat return grace");Time.time=12;Check(ExcuseMeDroneHotkeys.IsLocked(drone),"combat returns at grace boundary");
 Setup();Lock();stun=new DroneWeapons.StunBeamWeapon();drone.weapons.Add(stun);drone.activeWeapon=stun;drone.transitionState=4;Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"transition attack suspends");drone.transitionState=0;Time.time=11;drone.state=4;Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"repeat attack refreshes delay");drone.state=0;Time.time=12.5f;Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"repeat attack grace renewed");Time.time=13;drone.state=3;Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"native heal delays return");drone.state=0;drone.transitionState=3;Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"heal transition delays return");drone.transitionState=0;Check(ExcuseMeDroneHotkeys.IsLocked(drone),"heal end resumes");
 Setup();Lock();drone.state=4;drone.activeWeapon=new DroneWeapons.StunBeamWeapon();Check(ExcuseMeDroneHotkeys.IsLocked(drone),"no installed baton retains lock");
 Setup();Lock();drone.weapons.Add(new DroneWeapons.StunBeamWeapon());drone.state=4;drone.activeWeapon=new object();Check(ExcuseMeDroneHotkeys.IsLocked(drone),"non-stun active weapon retains lock");
 Setup();Lock();drone.weapons.Add(new DroneWeapons.StunBeamWeapon());drone.state=3;Check(ExcuseMeDroneHotkeys.IsLocked(drone),"heal alone not treated as combat");
 Setup();Lock();stun=new DroneWeapons.StunBeamWeapon();drone.weapons.Add(stun);drone.activeWeapon=stun;drone.state=4;Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"combat cancel setup");Press(KeyCode.At);Check(!ExcuseMeDroneHotkeys.IsLockRequested(drone)&&ExcuseMeDroneController.Summons==1&&drone.followCalls==1,"at cancels suspended intent and recalls");Time.time=100;drone.state=0;Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"cancel does not auto resume");
 Setup();Lock();stun=new DroneWeapons.StunBeamWeapon();drone.weapons.Add(stun);drone.activeWeapon=stun;drone.state=4;ExcuseMeDroneHotkeys.IsLocked(drone);ExcuseMeDroneHotkeys.ReleaseWithoutSummon(drone);Check(!ExcuseMeDroneHotkeys.IsLockRequested(drone),"F10 releases suspended intent");
 Setup();Lock();stun=new DroneWeapons.StunBeamWeapon();drone.weapons.Add(stun);drone.activeWeapon=stun;drone.state=4;ExcuseMeDroneHotkeys.IsLocked(drone);drone.weapons.Clear();drone.state=0;Time.time=11;Check(!ExcuseMeDroneHotkeys.IsLocked(drone),"baton removed grace remains");Time.time=12;Check(ExcuseMeDroneHotkeys.IsLocked(drone),"baton removed eventual return");
 Setup();Lock();drone.state=5;Check(!ExcuseMeDroneHotkeys.IsLockRequested(drone),"shutdown state clears intent even healthy");
 string dir=Path.Combine(Path.GetTempPath(),"drone-config-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(dir,"Config"));string f=Path.Combine(dir,"Config","ExcuseMeDrone.cfg");try{File.WriteAllText(f,"BehindLockKey=f8\nInventoryKey=F9\nBehindLockDistance=4.5\n");ExcuseMeDroneConfig.Load(dir);Check(ExcuseMeDroneConfig.Current.BehindLockKey==KeyCode.F8&&ExcuseMeDroneConfig.Current.InventoryKey==KeyCode.F9,"config keys case insensitive");Check(Near(ExcuseMeDroneConfig.Current.BehindLockDistance,4.5f),"config distance");File.WriteAllText(f,"BehindLockKey=none\nInventoryKey=Invalid\nBehindLockDistance=99\n");ExcuseMeDroneConfig.Load(dir);Check(ExcuseMeDroneConfig.Current.BehindLockKey==KeyCode.At&&ExcuseMeDroneConfig.Current.InventoryKey==KeyCode.Colon,"invalid key fallback");Check(Near(ExcuseMeDroneConfig.Current.BehindLockDistance,6),"distance upper clamp");File.WriteAllText(f,"BehindLockDistance=-9\nMenuKey=Home\n");ExcuseMeDroneConfig.Load(dir);Check(Near(ExcuseMeDroneConfig.Current.BehindLockDistance,.6f),"distance lower clamp");Check(ExcuseMeDroneConfig.Current.SummonKey==KeyCode.Home,"legacy summon key");}finally{Directory.Delete(dir,true);}
 Console.WriteLine("PASS "+checks+" controlled assertions; no actual game, physics or Harmony patch execution.");
 }
}
