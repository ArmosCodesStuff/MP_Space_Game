// FALLBACK ONLY. GodotSharp.dll is present in this folder, so typecheck.sh
// references the real assembly and ignores this file entirely. It is kept for
// environments without the dll. Do not trust it over the real thing: it has
// twice passed code that could not compile in Godot.

// Minimal Godot surface so Roslyn can type-check the game's own logic.
namespace Godot {
 public struct Vector2 { public Vector2 Lerp(Vector2 to, float w) => this; public float X,Y; public Vector2(float x,float y){X=x;Y=y;}
   public float Length()=>0; public float LengthSquared()=>0; public Vector2 Normalized()=>this;
   public float DistanceTo(Vector2 o)=>0; public float DistanceSquaredTo(Vector2 o)=>0;
   public float Dot(Vector2 o)=>0; public float Angle()=>0; public Vector2 Rotated(float a)=>this;
   public Vector2 MoveToward(Vector2 t,float d)=>this;
   public static Vector2 Zero=>default; public static Vector2 One=>default; public static Vector2 Right=>default; public static Vector2 Left=>default; public static Vector2 Up=>default; public static Vector2 Down=>default; public static Vector2 operator+(Vector2 a,Vector2 b)=>a;
   public static Vector2 operator-(Vector2 a,Vector2 b)=>a; public static Vector2 operator-(Vector2 a)=>a;
   public static Vector2 operator*(Vector2 a,float b)=>a; public static Vector2 operator*(float b,Vector2 a)=>a;
   public static Vector2 operator/(Vector2 a,float b)=>a; public static bool operator==(Vector2 a,Vector2 b)=>true;
   public static bool operator!=(Vector2 a,Vector2 b)=>false; public override bool Equals(object o)=>true; public override int GetHashCode()=>0; }
 public struct Vector2I { public int X,Y; public Vector2I(int x,int y){X=x;Y=y;} }
 public struct Rect2I { public Vector2I Position,Size; }
 public struct Color { public float R,G,B,A; public Color(float r,float g,float b,float a=1){R=r;G=g;B=b;A=a;}
   public static Color operator*(Color a,float b)=>a; public Color Lightened(float f)=>this; public Color Darkened(float f)=>this; public Color Lerp(Color c,float f)=>this; }
 public static class Colors { public static Color White, Red, Green, Blue, Black; }
 public struct Rect2 { public Vector2 Position,Size; public Rect2(Vector2 p,Vector2 s){Position=p;Size=s;} public Rect2(float x,float y,float w,float h){Position=default;Size=default;} public Rect2 Abs()=>this; public bool HasPoint(Vector2 p)=>true; }
 public static class Mathf { public static float LerpAngle(float a, float b, float w)=>a; public static float Wrap(float v, float lo, float hi)=>v; public const float Tau=6.28f, Pi=3.14f;
   public static float Cos(float a)=>0; public static float Sin(float a)=>0; public static float Sqrt(float a)=>0; public static double Sqrt(double a)=>0;
   public static float Abs(float a)=>0; public static double Abs(double a)=>0; public static int Abs(int a)=>0;
   public static float Min(float a,float b)=>0; public static double Min(double a,double b)=>0; public static int Min(int a,int b)=>0;
   public static float Max(float a,float b)=>0; public static double Max(double a,double b)=>0; public static int Max(int a,int b)=>0;
   public static float Clamp(float v,float a,float b)=>0; public static double Clamp(double v,double a,double b)=>0; public static int Clamp(int v,int a,int b)=>0;
   public static float Pow(float a,float b)=>0; public static double Pow(double a,double b)=>0;
   public static float Sign(float a)=>0; public static double Sign(double a)=>0; public static int Sign(int a)=>0;
   public static int CeilToInt(float a)=>0; public static int CeilToInt(double a)=>0;
   public static int FloorToInt(float a)=>0; public static int FloorToInt(double a)=>0;
   public static int RoundToInt(float a)=>0; public static int RoundToInt(double a)=>0;
   public static float Round(float a)=>0; public static double Round(double a)=>0;
   public static float DegToRad(float a)=>0; public static double DegToRad(double a)=>0;
   public static float LinearToDb(float v)=>0; public static float Lerp(float a,float b,float t)=>0; public static double Lerp(double a,double b,double t)=>0;
   public static float Log(float a)=>0; public static double Log(double a)=>0; public static float Exp(float a)=>0; public static double Exp(double a)=>0; }
public static class Performance { public enum Monitor { ObjectCount, ObjectNodeCount, ObjectOrphanNodeCount, MemoryStatic } public static double GetMonitor(Monitor m)=>0; }
 public static class AudioServer { public static int GetBusIndex(string n)=>0; public static void SetBusMute(int i,bool m){} public static void SetBusVolumeDb(int i,float db){} }
 public static class GD { public static float Randf()=>0; public static uint Randi()=>0; public static void Print(object o){} public static void PushWarning(object o){}
   public static T Load<T>(string p) where T:class=>null; }
 public class GodotObject { public ulong GetInstanceId()=>0; public bool IsQueuedForDeletion()=>false; public void QueueFree(){} }
 public class Node : GodotObject { public Node GetParent()=>this; public MultiplayerApi Multiplayer => new MultiplayerApi();
   public void SetMultiplayerAuthority(int id, bool recursive = true) { }
   public int GetMultiplayerAuthority() => 1;
   public bool IsMultiplayerAuthority() => true;
   public void Rpc(string method, params object[] args) { }
   public void RpcId(long peerId, string method, params object[] args) { }
   public double GetProcessDeltaTime()=>0.016; public string Name; public Node Owner; public void AddChild(Node n){} public void RemoveChild(Node n){}
   public Godot.Collections.Array<Node> GetChildren()=>null; public int GetChildCount()=>0; public Node GetChild(int i)=>null;
   public SceneTree GetTree()=>null; public Viewport GetViewport()=>null; public Window GetWindow()=>null; public virtual void _Ready(){} public virtual void _Process(double d){}
   public virtual void _Input(InputEvent e){} public virtual void _UnhandledInput(InputEvent e){} public void SetProcess(bool b){} }
 public class CanvasItem : Node { public bool Visible; public Color Modulate; public int ZIndex; public TextureRepeatEnum TextureRepeat; public enum TextureRepeatEnum { Disabled, Enabled } public void QueueRedraw(){}
   public virtual void _Draw(){} public void DrawCircle(Vector2 p,float r,Color c){} public void DrawArc(Vector2 p,float r,float a,float b,int n,Color c,float w=1){}
   public void DrawLine(Vector2 a,Vector2 b,Color c,float w=1){} public void DrawRect(Rect2 r,Color c,bool f=true,float w=1){}
   public void DrawPolygon(Vector2[] p,Color[] c){} public void DrawTexture(Texture2D t,Vector2 p){} public void DrawSetTransform(Vector2 p,float r,Vector2 s){} public void DrawString(Font f,Vector2 p,string s,HorizontalAlignment h,float w,int sz,Color c){}
   public Vector2 GetGlobalMousePosition()=>default; }
 public class Node2D : CanvasItem { public Vector2 Position { get; set; } public Vector2 GlobalPosition { get; set; } public Vector2 Scale { get; set; } public float Rotation { get; set; } public Vector2 ToLocal(Vector2 v)=>v; }
 public class Sprite2D : Node2D { public Texture2D Texture; public bool Centered, RegionEnabled; public Rect2 RegionRect; }
 public class Area2D : Node2D { public bool Monitoring, Monitorable; }
 public class CollisionShape2D : Node2D { public Shape2D Shape; }
 public class Shape2D : Resource {} public class CircleShape2D : Shape2D { public float Radius; }
 public class Camera2D : Node2D { public Vector2 Zoom; public void MakeCurrent(){} }
 public class CanvasLayer : Node { public int Layer; public bool Visible; public Vector2 Scale { get; set; } }
 public class Resource : GodotObject {} public class ImageTexture : Texture2D { public static ImageTexture CreateFromImage(Image i)=>null; }
 public class Image : Resource { public enum Format { Rgba8 } public static Image CreateEmpty(int w,int h,bool m,Format f)=>null; public void Fill(Color c){} public void SetPixel(int x,int y,Color c){} }
 public class Texture2D : Resource { public int GetHeight()=>0; public int GetWidth()=>0; public Vector2 GetSize()=>default; } public class Font : Resource {
    public Vector2 GetStringSize(string t, HorizontalAlignment a=HorizontalAlignment.Left, float w=-1, int size=16)=>default;} public class Theme : Resource {
   public void SetStylebox(string a,string b,StyleBox s){} public void SetColor(string a,string b,Color c){} public void SetFontSize(string a,string b,int i){} }
 public class StyleBox : Resource {} public class StyleBoxFlat : StyleBox { public Color BgColor,BorderColor;
   public int CornerRadiusTopLeft,CornerRadiusTopRight,CornerRadiusBottomLeft,CornerRadiusBottomRight,BorderWidthTop,BorderWidthBottom,BorderWidthLeft,BorderWidthRight,ContentMarginLeft,ContentMarginRight,ContentMarginTop,ContentMarginBottom; }
 public static class ThemeDB { public static Font FallbackFont; }
 public class Control : CanvasItem { public virtual void _GuiInput(InputEvent e){} public void AcceptEvent(){} public MouseFilterEnum MouseFilter; public enum MouseFilterEnum { Stop, Pass, Ignore } public Vector2 Position,Size,CustomMinimumSize; public Theme Theme; public SizeFlags SizeFlagsHorizontal;
   public FocusModeEnum FocusMode; public void SetAnchorsPreset(LayoutPreset p){} public Vector2 GetCombinedMinimumSize()=>default; public void ReleaseFocus(){}
   public void AddThemeStyleboxOverride(string n,StyleBox s){} public void AddThemeColorOverride(string n,Color c){} public void AddThemeFontSizeOverride(string n,int i){} public void AddThemeConstantOverride(string n,int i){}
   public StyleBox GetThemeStylebox(string n)=>null;
   public enum SizeFlags { Fill, Expand, ExpandFill, ShrinkCenter } public enum LayoutPreset { FullRect } public enum FocusModeEnum { None, All } }
 public class Label : Control { public string Text; public bool ClipText; public TextServer.OverrunBehavior TextOverrunBehavior; public HorizontalAlignment HorizontalAlignment; public TextServer.AutowrapMode AutowrapMode; }
 public class Button : Control { public string Text; public bool Disabled; public HorizontalAlignment Alignment; public TextServer.AutowrapMode AutowrapMode;
   public bool ClipText; public TextServer.OverrunBehavior TextOverrunBehavior; public event System.Action Pressed; }
 public class LineEdit : Control { public string Text, PlaceholderText; public event System.Action<string> TextChanged; }
 public class BoxContainer : Control {} public class HBoxContainer : BoxContainer {} public class VBoxContainer : BoxContainer {}
 public class PanelContainer : Control {} public class CenterContainer : Control {}
 public class ScrollContainer : Control { public ScrollMode HorizontalScrollMode; public enum ScrollMode { Disabled, Auto } }
 public class ColorRect : Control { public Color Color; }
 public class HSeparator : Control {}
 public static class TextServer { public enum AutowrapMode { Off, WordSmart } public enum OverrunBehavior { TrimEllipsis } }
 public enum HorizontalAlignment { Left, Center, Right }
 public class SceneTree : GodotObject { public Error ChangeSceneToFile(string p)=>default; public void ReloadCurrentScene(){} public void Quit(){} }
 public class Viewport : GodotObject { public Vector2 GetMousePosition()=>default; public Rect2 GetVisibleRect()=>default; public void SetInputAsHandled(){} public Control GuiGetFocusOwner()=>null; }
 public class Window : Viewport { public Vector2I Size, ContentScaleSize, Position; public ModeEnum Mode; public bool Borderless; public ContentScaleModeEnum ContentScaleMode; public ContentScaleAspectEnum ContentScaleAspect; public enum ContentScaleModeEnum { Disabled, CanvasItems, Viewport } public enum ContentScaleAspectEnum { Ignore, Keep, Expand }
   public enum ModeEnum { Windowed, Fullscreen, ExclusiveFullscreen } }
 public class InputEvent : Resource { public bool IsAction(string a)=>false; }
 public class InputEventKey : InputEvent { public Key Keycode, PhysicalKeycode; public bool Pressed, Echo, CtrlPressed, MetaPressed, ShiftPressed, AltPressed; }
 public class InputEventMouseButton : InputEvent { public MouseButton ButtonIndex; public bool Pressed; public Vector2 Position; }
 public class InputEventMouseMotion : InputEvent { public Vector2 Position, Relative; }
 public static class Input { public static bool IsKeyPressed(Key k)=>false; public static bool IsMouseButtonPressed(MouseButton b)=>false; public static Vector2 GetMousePosition()=>default; }
 public enum Key { Key0, Key1, Key2, Key3, Key4, Key5, Key6, Key7, W,A,S,D,E,H,R,F,G,C,V,Escape,F1,F2,Ctrl,Shift,Alt,Space,Enter,Key8,Key9 } public enum MouseButton { Left,Right,WheelUp,WheelDown }
 public static class DisplayServer { public static Rect2I ScreenGetUsableRect()=>default; public static int WindowGetCurrentScreen()=>0; public static Rect2I ScreenGetUsableRect(int i)=>default; public static Vector2I ScreenGetSize(int i)=>default; public static Vector2I ScreenGetSize()=>default; }
 public static class ProjectSettings { public static string GlobalizePath(string p)=>p; }
 public class FileAccess : GodotObject, System.IDisposable { public enum ModeFlags { Read, Write } public static bool FileExists(string p)=>false;
   public static FileAccess Open(string p,ModeFlags m)=>null; public static Error GetOpenError()=>default;
   public string GetAsText()=>""; public void StoreString(string s){} public void Dispose(){} }
 public static class DirAccess { public static Error RemoveAbsolute(string p)=>default; public static Error RenameAbsolute(string a,string b)=>default; }
 public enum Error { Ok, Failed }
 public class AudioStreamPlayer : Node { public AudioStream Stream; public float VolumeDb; public bool Playing; public void Play(){}
   public AudioStreamGeneratorPlayback GetStreamPlayback()=>null; }
 public class AudioStream : Resource {} public class AudioStreamGenerator : AudioStream { public float MixRate, BufferLength; }
 public class AudioStreamGeneratorPlayback : Resource { public int GetFramesAvailable()=>0; public void PushBuffer(Vector2[] b){} }
 public struct Callable { public static Callable From(System.Action a)=>default; public void CallDeferred(){} }
 namespace Collections { public class Array<T> : System.Collections.Generic.List<T> {} }
}

// ── multiplayer surface used by Warships (stub only; the real API lives in Godot) ──
namespace Godot
{
    // Base types the appended stubs derive from. Without these, every class below
    // becomes an error type -- and Roslyn stops before it ever binds method bodies,
    // so NOTHING in scripts/ gets checked. That is how a broken stub hides real bugs.
    public class RefCounted : GodotObject { }
    public struct Variant
    {
        public static implicit operator Variant(int v) => default;
        public static implicit operator Variant(float v) => default;
        public static implicit operator Variant(double v) => default;
        public static implicit operator Variant(bool v) => default;
        public static implicit operator Variant(string v) => default;
        public static implicit operator Variant(Color v) => default;
        public static explicit operator int(Variant v) => 0;
        public static explicit operator float(Variant v) => 0f;
        public static explicit operator double(Variant v) => 0;
        public static explicit operator bool(Variant v) => false;
        public static explicit operator string(Variant v) => "";
        public static explicit operator Color(Variant v) => default;
    }

    public enum MultiplayerPeerConnectionStatus { Disconnected, Connecting, Connected }
    public partial class MultiplayerPeer : RefCounted
    {
        // Real Godot nests this on MultiplayerPeer. The stub previously declared a
        // top-level MultiplayerPeerTransferMode, which does not exist -- so code using
        // it typechecked here and failed in the engine. Names must match exactly.
        public enum TransferModeEnum { Unreliable, UnreliableOrdered, Reliable }
        public MultiplayerPeerConnectionStatus GetConnectionStatus() => MultiplayerPeerConnectionStatus.Connected;
        public void Close() { }
    }
    public partial class ENetMultiplayerPeer : MultiplayerPeer
    {
        public Error CreateServer(int port, int maxClients = 32) => Error.Ok;
        public Error CreateClient(string address, int port) => Error.Ok;
    }
    public partial class MultiplayerApi : RefCounted
    {
        public enum RpcMode { Disabled, AnyPeer, Authority }
        public MultiplayerPeer MultiplayerPeer { get; set; }
        public int GetUniqueId() => 1;
        public bool IsServer() => true;
        public int[] GetPeers() => new int[0];
        public int GetRemoteSenderId() => 1;
        public event System.Action<long> PeerConnected;
        public event System.Action<long> PeerDisconnected;
        public event System.Action ConnectedToServer;
        public event System.Action ConnectionFailed;
        public event System.Action ServerDisconnected;
    }
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class RpcAttribute : System.Attribute
    {
        public RpcAttribute() { }
        public RpcAttribute(MultiplayerApi.RpcMode mode) { }
        public MultiplayerApi.RpcMode Mode { get; set; }
        public bool CallLocal { get; set; }
        public MultiplayerPeer.TransferModeEnum TransferMode { get; set; }
    }
}

namespace Godot
{
    public partial class ColorPickerButton : Button
    {
        public Color Color { get; set; }
        public event System.Action<Color> ColorChanged;
    }
    public partial class ConfigFile : RefCounted
    {
        public void SetValue(string section, string key, Variant value) { }
        public Variant GetValue(string section, string key, Variant def = default) => def;
        public Error Save(string path) => Error.Ok;
        public Error Load(string path) => Error.Ok;
    }
}
