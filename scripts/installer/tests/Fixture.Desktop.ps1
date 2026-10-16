Add-Type -AssemblyName System.Drawing
if (-not ('CodeWFSetupFixtureDesktop' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
public static class CodeWFSetupFixtureDesktop {
 [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct StartupInfo { public int cb; public string reserved,desktop,title; public int x,y,cx,cy,charsX,charsY,fill,flags; public short show,reserved2; public IntPtr reservedPtr,input,output,error; }
 [StructLayout(LayoutKind.Sequential)] public struct ProcessInfo { public IntPtr process,thread; public int processId,threadId; }
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int left,top,right,bottom; }
 [StructLayout(LayoutKind.Sequential)] public struct GuiThreadInfo { public int size,flags;public IntPtr active,focus,capture,menuOwner,moveSize,caret;public Rect caretBounds; }
 public delegate bool EnumProc(IntPtr window,IntPtr data);
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,int flags,uint access,IntPtr security);
 [DllImport("user32.dll")] public static extern bool CloseDesktop(IntPtr desktop);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern bool CreateProcess(string application,StringBuilder command,IntPtr processAttr,IntPtr threadAttr,bool inherit,uint flags,IntPtr environment,string directory,ref StartupInfo startup,out ProcessInfo process);
 [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr handle);
 [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access,bool inherit,uint processId);
 [DllImport("kernel32.dll")] public static extern bool TerminateProcess(IntPtr process,uint exitCode);
 [DllImport("user32.dll")] public static extern bool EnumDesktopWindows(IntPtr desktop,EnumProc callback,IntPtr data);
 [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent,EnumProc callback,IntPtr data);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr window,StringBuilder text,int count);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr window,StringBuilder text,int count);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window,out Rect rect);
 [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr window);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
 [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr window);
 [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr window);
 [DllImport("user32.dll",EntryPoint="GetWindowLongW")] public static extern int GetWindowStyle(IntPtr window,int index);
 [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr window);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window,int command);
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern bool PostMessage(IntPtr window,uint message,IntPtr wp,IntPtr lp);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr window,IntPtr dc,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr wp,IntPtr lp,uint flags,uint timeout,out UIntPtr result);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
 [DllImport("user32.dll",SetLastError=true)] public static extern bool GetGUIThreadInfo(uint threadId,ref GuiThreadInfo info);
 [DllImport("user32.dll",SetLastError=true)] public static extern bool SetThreadDesktop(IntPtr desktop);
 [DllImport("user32.dll")] public static extern int GetWindowRgn(IntPtr window,IntPtr region);
 [DllImport("gdi32.dll")] public static extern IntPtr CreateRectRgn(int left,int top,int right,int bottom);
 [DllImport("gdi32.dll")] public static extern bool PtInRegion(IntPtr region,int x,int y);
 [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr region);
 public static string Text(IntPtr window) { IntPtr buffer=Marshal.AllocHGlobal(16384); try {Marshal.WriteInt16(buffer,0);Send(window,13,8192,buffer);return Marshal.PtrToStringUni(buffer) ?? "";} finally {Marshal.FreeHGlobal(buffer);} }
 public static string Class(IntPtr window) { var text=new StringBuilder(512); GetClassName(window,text,text.Capacity); return text.ToString(); }
 public static Rect VisibleBounds(IntPtr control,IntPtr root) {Rect result;GetWindowRect(control,out result);var parent=GetParent(control);while(parent!=IntPtr.Zero){Rect bounds;GetWindowRect(parent,out bounds);result.left=Math.Max(result.left,bounds.left);result.top=Math.Max(result.top,bounds.top);result.right=Math.Min(result.right,bounds.right);result.bottom=Math.Min(result.bottom,bounds.bottom);if(parent==root)break;parent=GetParent(parent);}return result;}
 public static List<IntPtr> Windows(IntPtr desktop) { var windows=new List<IntPtr>(); EnumDesktopWindows(desktop,(w,d)=>{windows.Add(w);return true;},IntPtr.Zero); return windows; }
 public static List<IntPtr> Children(IntPtr parent) { var windows=new List<IntPtr>(); EnumChildWindows(parent,(w,d)=>{windows.Add(w);return true;},IntPtr.Zero); return windows; }
 public static ulong Send(IntPtr window,uint message) { return Send(window,message,0,IntPtr.Zero); }
 public static ulong Send(IntPtr window,uint message,long wp) { return Send(window,message,wp,IntPtr.Zero); }
 public static ulong Send(IntPtr window,uint message,long wp,IntPtr lp) { UIntPtr result; if(SendMessageTimeout(window,message,new IntPtr(wp),lp,2,5000,out result)==IntPtr.Zero) throw new InvalidOperationException("Fixture window did not respond."); return result.ToUInt64(); }
 public static void SetText(IntPtr window,string text) { IntPtr pointer=Marshal.StringToHGlobalUni(text); try {Send(window,12,0,pointer);} finally {Marshal.FreeHGlobal(pointer);} }
 public static IntPtr Focus(IntPtr desktop,IntPtr window) {IntPtr focus=IntPtr.Zero;Exception failure=null;var thread=new System.Threading.Thread(()=>{try{if(!SetThreadDesktop(desktop))throw new InvalidOperationException("Cannot access fixture desktop: "+Marshal.GetLastWin32Error());uint processId;var threadId=GetWindowThreadProcessId(window,out processId);var info=new GuiThreadInfo();info.size=Marshal.SizeOf<GuiThreadInfo>();if(!GetGUIThreadInfo(threadId,ref info))throw new InvalidOperationException("Fixture GUI thread is unavailable: "+Marshal.GetLastWin32Error());focus=info.focus;}catch(Exception error){failure=error;}});thread.Start();thread.Join();if(failure!=null)throw failure;return focus;}
 public static string ListItemText(IntPtr window,int index) { IntPtr buffer=Marshal.AllocHGlobal(16384);try {Marshal.WriteInt16(buffer,0);Send(window,393,index,buffer);return Marshal.PtrToStringUni(buffer) ?? "";}finally {Marshal.FreeHGlobal(buffer);} }
 public static bool HasShapedRegion(IntPtr window) { IntPtr region=CreateRectRgn(0,0,0,0); try {Rect r;GetWindowRect(window,out r);return GetWindowRgn(window,region)>1 && !PtInRegion(region,0,0) && PtInRegion(region,(r.right-r.left)/2,(r.bottom-r.top)/2);} finally {DeleteObject(region);} }
 public static void Capture(IntPtr window,string path) { Rect r;if(!GetWindowRect(window,out r) || r.right<=r.left || r.bottom<=r.top) throw new InvalidOperationException("Fixture window bounds invalid.");using(var b=new Bitmap(r.right-r.left,r.bottom-r.top)){using(var g=Graphics.FromImage(b)){IntPtr dc=g.GetHdc();try {if(!PrintWindow(window,dc,2)) throw new InvalidOperationException("PrintWindow failed.");} finally {g.ReleaseHdc(dc);}} b.Save(path);} }
 public static double PixelLightness(string path,double x,double y) { using(var b=new Bitmap(path)){var c=b.GetPixel((int)(b.Width*x),(int)(b.Height*y));return (c.R+c.G+c.B)/3.0;} }
 public static int InkPixels(string path,IntPtr control,IntPtr root,bool light,int inset,int maximumWidth,int maximumHeight) {Rect r=VisibleBounds(control,root),w;GetWindowRect(root,out w);using(var b=new Bitmap(path)){int count=0,left=Math.Max(0,r.left-w.left+inset),top=Math.Max(0,r.top-w.top+4),right=Math.Min(b.Width,Math.Min(r.right-w.left-6,left+maximumWidth)),bottom=Math.Min(b.Height,Math.Min(r.bottom-w.top-4,top+maximumHeight));for(int y=top;y<bottom;y++)for(int x=left;x<right;x++){var c=b.GetPixel(x,y);double value=(c.R+c.G+c.B)/3.0;if(light ? value<150 : value>190)count++;}return count;} }
 public static int PurplePixels(string path,IntPtr control,IntPtr root) {Rect r=VisibleBounds(control,root),w;GetWindowRect(root,out w);using(var b=new Bitmap(path)){int count=0;for(int y=Math.Max(0,r.top-w.top);y<Math.Min(b.Height,r.bottom-w.top);y++)for(int x=Math.Max(0,r.left-w.left);x<Math.Min(b.Width,r.right-w.left);x++){var c=b.GetPixel(x,y);if(c.B>150 && c.B>c.R*1.2 && c.R>c.G*1.2)count++;}return count;} }
 public static string CropHash(string path,double x,double y,double width,double height) {using(var b=new Bitmap(path)){var r=new Rectangle((int)(b.Width*x),(int)(b.Height*y),(int)(b.Width*width),(int)(b.Height*height));using(var crop=b.Clone(r,b.PixelFormat))using(var stream=new System.IO.MemoryStream()){crop.Save(stream,System.Drawing.Imaging.ImageFormat.Png);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream.ToArray()));}}}
}
'@ -ReferencedAssemblies System.Drawing.Common,System.Runtime,System.Collections,System.Drawing.Primitives,System.Private.Windows.GdiPlus,System.Private.Windows.Core,System.Security.Cryptography,System.Threading.Thread
}
