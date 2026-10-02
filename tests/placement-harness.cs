using System;
struct Vector3 {
 public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;}
 public static Vector3 up=>new Vector3(0,1,0); public static Vector3 down=>new Vector3(0,-1,0); public static Vector3 forward=>new Vector3(0,0,1);
 public static Vector3 right=>new Vector3(1,0,0); public static float Distance(Vector3 a,Vector3 b)=>(float)Math.Sqrt((a-b).sqrMagnitude); public float sqrMagnitude=>x*x+y*y+z*z;public void Normalize(){float m=(float)Math.Sqrt(sqrMagnitude);x/=m;y/=m;z/=m;}
 public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
 public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
 public static Vector3 operator *(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
}
static class Mathf { public const float PI=(float)Math.PI; public static float Abs(float v)=>Math.Abs(v); public static float Sin(float v)=>(float)Math.Sin(v);public static float Cos(float v)=>(float)Math.Cos(v); public static int FloorToInt(float v)=>(int)Math.Floor(v); }
class Transform { public Vector3 forward=Vector3.forward;public Vector3 right=Vector3.right;public Transform parent; public bool IsChildOf(Transform t)=>parent==t||(parent!=null&&parent.IsChildOf(t)); }
class Entity { public Transform transform=new Transform();public Vector3 position; }
class EntityDrone:Entity {public Entity Owner;public int entityId=1;public int Moves;public void TeleportToPosition(Vector3 t){position=t;Moves++;}} class EntityPlayerLocal:Entity {}
class Collider { public Transform transform=new Transform(); }
struct RaycastHit {public Collider collider;public Vector3 normal,point;public float distance;}
struct Quaternion {public static Quaternion identity=>new Quaternion();}
enum QueryTriggerInteraction {Ignore}
static class Origin {public static Vector3 position;}
class Block {public bool IsCollideMovement=true;}
struct BlockValue {public bool isair;public Block Block;}
class World {public bool Support=true;public Func<int,int,int,BlockValue> Query; public BlockValue GetBlock(int x,int y,int z)=>Query!=null?Query(x,y,z):new BlockValue{isair=!Support,Block=new Block()};public float GetHeightAt(float x,float z)=>throw new Exception("Global height query forbidden");}
class GameManager {public static GameManager Instance=new GameManager();public World World=new World();}
class ExcuseMeDroneConfig {public static ExcuseMeDroneConfig Current;public float BrokenLiftThreshold=.25f,BrokenMinimumAwakeSeconds=1.5f,BrokenReviveTimeoutSeconds=6;public bool GroundSnapEnabled=true;public float BehindDistance=.6f,SideOffset=-1.8f,HeightOffset=1.1f,ImmediateRescueDistance=20,StuckDistance=8,StuckSeconds=3,StuckMovementThreshold=.2f;public float GroundClearance=.1f, BrokenSummonHeightOffset=.8f, SummonForwardDistance=1.5f,SummonHeightOffset=1.2f;}
static class Physics {
 public const int DefaultRaycastLayers=-5;
 public static Func<Vector3,RaycastHit[]> Rays;public static Func<Vector3,Collider[]> Overlaps;
 public static Vector3 LastRay,LastCenter;
 public static RaycastHit[] RaycastAll(Vector3 s,Vector3 dir,float length,int mask,QueryTriggerInteraction q){LastRay=s;var hits=Rays(s);foreach(var h in hits)if(h.distance<0||h.distance>length)throw new Exception("Invalid simulated ray distance");return hits;}
 public static int OverlapBoxNonAlloc(Vector3 c,Vector3 size,Collider[] buffer,Quaternion rotation,int mask,QueryTriggerInteraction q){LastCenter=c;var hits=Overlaps(c);Array.Copy(hits,buffer,Math.Min(hits.Length,buffer.Length));return Math.Min(hits.Length,buffer.Length);}
}
static class Time {public static float time;public static int frameCount;}
class Harness {
 class RuntimeState {public bool PendingBrokenGroundTeleportApplied,PendingBrokenReshutdown;public float PendingBrokenGroundY,PendingBrokenStartedAt;public int PendingBrokenStartFrame;public float NextRescueAttemptTime,LastSampleTime,StuckAccumulated;public Vector3 LastSamplePosition;public bool HasSample,PlacementBlockedLogged;}
 static int Shutdowns;static bool ApplyBrokenDroneSuicide(EntityDrone d){Shutdowns++;return true;}
 static readonly Collider[] PlacementOverlaps=new Collider[32];static void DebugLog(string s){} static int passed;
 // PRODUCTION_METHODS
 static RaycastHit Floor(Vector3 ray,float y){return new RaycastHit{collider=new Collider(),normal=Vector3.up,point=new Vector3(ray.x,y-Origin.position.y,ray.z),distance=ray.y+Origin.position.y-y};}
 static EntityPlayerLocal Setup(float y=10){GameManager.Instance=new GameManager();Origin.position=new Vector3();var p=new EntityPlayerLocal{position=new Vector3(0,y,0)};Physics.Rays=s=>new[]{Floor(s,y)};Physics.Overlaps=c=>Array.Empty<Collider>();return p;}
 static void Assert(bool condition,string title){if(!condition)throw new Exception(title);passed++;Console.WriteLine("PASS "+title);}
 static bool Near(float a,float b)=>Math.Abs(a-b)<.001;
 static void Main(){var cfg=new ExcuseMeDroneConfig();Vector3 t;var drone=new EntityDrone();var p=Setup();
 Assert(TryFindTeleportTarget(p,new Vector3(0,11.2f,1.5f),drone,cfg,true,out t)&&Near(t.y,10.1f),"current floor instead of height map");
 p=Setup(-12);Assert(TryFindTeleportTarget(p,new Vector3(0,-10.8f,1.5f),drone,cfg,true,out t)&&Near(t.y,-11.9f),"underground floor");
 p=Setup(80);Assert(TryFindTeleportTarget(p,new Vector3(0,81.2f,1.5f),drone,cfg,true,out t)&&Near(t.y,80.1f),"upper storey floor");
 p=Setup();Physics.Overlaps=c=>c.z>1.4f?new[]{new Collider()}:Array.Empty<Collider>();Assert(TryFindTeleportTarget(p,new Vector3(0,11.2f,1.5f),drone,cfg,true,out t)&&t.z<1.4f,"blocked forward position selects side");
 p=Setup();Physics.Overlaps=c=>new[]{new Collider()};Assert(!TryFindTeleportTarget(p,new Vector3(0,11.2f,1.5f),drone,cfg,true,out t),"automatic placement refuses all blocked candidates");
 t=FindManualSummonTarget(p,drone,cfg,false);Assert(Near(t.x,p.position.x)&&Near(t.z,p.position.z)&&Near(t.y,10.1f),"manual recall preserves ground redraw despite blocked candidates");
 p=Setup();Physics.Rays=s=>Array.Empty<RaycastHit>();Assert(!TryFindTeleportTarget(p,new Vector3(0,11.2f,1.5f),drone,cfg,true,out t),"no remote floor searched across void");
 t=FindManualSummonTarget(p,drone,cfg,false);Assert(Near(t.y,10.1f),"manual recall preserves owner-foot baseline when ground lookup fails");
 p=Setup();cfg.GroundSnapEnabled=false;t=FindManualSummonTarget(p,drone,cfg,false);Assert(Near(t.y,11.2f),"ground snap disabled retains configured height");cfg.GroundSnapEnabled=true;
 p=Setup();t=FindManualSummonTarget(p,drone,cfg,true);Assert(Near(t.y,10.8f),"broken initial recall keeps player-relative height");
 p=Setup();Origin.position=new Vector3(1000,8,2000);Assert(TryPlacement(p,new Vector3(2,11.2f,3),drone,cfg,true,false,out t)&&Near(t.y,10.1f)&&Near(Physics.LastRay.x,-998)&&Near(Physics.LastCenter.z,-1997),"floating origin conversion in both physics queries");
 p=Setup();var self=new Collider{transform=new Transform{parent=drone.transform}};Physics.Overlaps=c=>new[]{self};Assert(TryPlacement(p,new Vector3(0,11.2f,1.5f),drone,cfg,true,false,out t),"own child collider ignored");
 p=Setup();var ownerCollider=new Collider{transform=p.transform};Physics.Overlaps=c=>new[]{ownerCollider};Assert(!TryPlacement(p,new Vector3(0,11.2f,1.5f),drone,cfg,true,false,out t)&&TryPlacement(p,new Vector3(0,11.2f,0),drone,cfg,true,true,out t),"owner overlap allowed only at final center candidate");
 p=Setup();GameManager.Instance.World.Support=false;Assert(!TryPlacement(p,new Vector3(0,11.2f,1.5f),drone,cfg,true,false,out t),"entity collider without supporting world block rejected as floor");
 p=Setup();Physics.Rays=s=>{var h=Floor(s,10);h.normal=new Vector3(1,0,0);return new[]{h};};Assert(!TryPlacement(p,new Vector3(0,11.2f,1.5f),drone,cfg,true,false,out t),"vertical wall rejected as floor");
 p=Setup();Physics.Overlaps=c=>new Collider[32];Assert(!TryPlacement(p,new Vector3(0,11.2f,1.5f),drone,cfg,true,false,out t),"full collider buffer handled conservatively");
 p=Setup();Physics.Rays=s=>new[]{Floor(s,9.5f),Floor(s,10)};Assert(TryPlacement(p,new Vector3(0,11.2f,1.5f),drone,cfg,true,false,out t)&&Near(t.y,10.1f),"nearest support chosen regardless of hit order");
 p=Setup();GameManager.Instance.World=null;t=FindManualSummonTarget(p,drone,cfg,false);Assert(Near(t.y,10.1f),"missing world never falls back to global height");
 p=Setup();drone=new EntityDrone{position=new Vector3(0,13.5f,0)};var state=new RuntimeState();for(int i=0;i<=3;i++){Time.time=i;UpdateStuckRescue(drone,p,state,cfg);}Assert(drone.Moves==1,"nearby stationary other-floor drone rescued after three seconds");
 p=Setup();drone=new EntityDrone{position=new Vector3(0,11.5f,0)};state=new RuntimeState();for(int i=0;i<=4;i++){Time.time=i;UpdateStuckRescue(drone,p,state,cfg);}Assert(drone.Moves==0,"normal nearby hover not falsely rescued");
 p=Setup();Physics.Overlaps=c=>new[]{new Collider()};drone=new EntityDrone{position=new Vector3(0,30,0)};state=new RuntimeState();Time.time=0;UpdateStuckRescue(drone,p,state,cfg);Assert(drone.Moves==0&&Near(state.NextRescueAttemptTime,1),"blocked automatic rescue remains in place and backs off");
 p=Setup();Physics.Overlaps=c=>new[]{new Collider()};drone=new EntityDrone{Owner=p,position=new Vector3(0,10.8f,1.5f)};state=new RuntimeState{PendingBrokenReshutdown=true,PendingBrokenStartFrame=0};ExcuseMeDroneConfig.Current=cfg;Time.frameCount=1;Time.time=0;Shutdowns=0;UpdatePendingBrokenReshutdown(drone,state);Assert(drone.Moves==1&&state.PendingBrokenGroundTeleportApplied&&Near(drone.position.y,10.1f)&&Shutdowns==0,"broken redraw executes ground teleport before marking applied even on failed clearance search");
 Time.frameCount=2;Time.time=1;drone.position=new Vector3(0,10.4f,0);UpdatePendingBrokenReshutdown(drone,state);Assert(Shutdowns==0,"broken redraw waits minimum awake time after lift");
 Time.frameCount=3;Time.time=1.6f;UpdatePendingBrokenReshutdown(drone,state);Assert(Shutdowns==1&&!state.PendingBrokenReshutdown,"broken redraw restores shutdown after ground, lift and grace period");
 p=Setup();drone=new EntityDrone{Owner=p,position=new Vector3(0,10.8f,1.5f)};state=new RuntimeState{PendingBrokenReshutdown=true,PendingBrokenStartFrame=0};Time.frameCount=1;Time.time=0;Shutdowns=0;UpdatePendingBrokenReshutdown(drone,state);Time.frameCount=2;Time.time=6;UpdatePendingBrokenReshutdown(drone,state);Assert(Shutdowns==1&&drone.Moves==1,"broken redraw restores shutdown on lift timeout");
 Console.WriteLine(passed+" placement cases PASS (production methods, physics/world doubles)");}
}
