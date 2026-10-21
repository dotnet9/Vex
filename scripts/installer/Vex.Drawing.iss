[Code]
{ Vex 安装界面的抗锯齿圆角绘制，使用 Windows 自带 GDI+。 }
type
  TVDrawingStartup = record
    Version, DebugCallback: LongWord;
    SuppressBackgroundThread, SuppressExternalCodecs: LongBool;
  end;

function VDrawingStartup(var Token: LongWord; var Input: TVDrawingStartup; Output: LongWord): Integer;
  external 'GdiplusStartup@gdiplus.dll stdcall';
procedure VDrawingShutdown(Token: LongWord);
  external 'GdiplusShutdown@gdiplus.dll stdcall';
function VDrawingCreate(DC: LongWord; var Graphics: LongWord): Integer;
  external 'GdipCreateFromHDC@gdiplus.dll stdcall';
function VDrawingDelete(Graphics: LongWord): Integer;
  external 'GdipDeleteGraphics@gdiplus.dll stdcall';
function VDrawingSmoothing(Graphics: LongWord; Mode: Integer): Integer;
  external 'GdipSetSmoothingMode@gdiplus.dll stdcall';
function VDrawingPathCreate(Mode: Integer; var Path: LongWord): Integer;
  external 'GdipCreatePath@gdiplus.dll stdcall';
function VDrawingPathDelete(Path: LongWord): Integer;
  external 'GdipDeletePath@gdiplus.dll stdcall';
function VDrawingArc(Path: LongWord; X,Y,Width,Height,StartAngle,SweepAngle: Single): Integer;
  external 'GdipAddPathArc@gdiplus.dll stdcall';
function VDrawingClose(Path: LongWord): Integer;
  external 'GdipClosePathFigure@gdiplus.dll stdcall';
function VDrawingBrushCreate(Color: LongWord; var Brush: LongWord): Integer;
  external 'GdipCreateSolidFill@gdiplus.dll stdcall';
function VDrawingBrushDelete(Brush: LongWord): Integer;
  external 'GdipDeleteBrush@gdiplus.dll stdcall';
function VDrawingFill(Graphics, Brush, Path: LongWord): Integer;
  external 'GdipFillPath@gdiplus.dll stdcall';
function VDrawingPenCreate(Color: LongWord; Width: Single; UnitMode: Integer; var Pen: LongWord): Integer;
  external 'GdipCreatePen1@gdiplus.dll stdcall';
function VDrawingPenDelete(Pen: LongWord): Integer;
  external 'GdipDeletePen@gdiplus.dll stdcall';
function VDrawingOutline(Graphics, Pen, Path: LongWord): Integer;
  external 'GdipDrawPath@gdiplus.dll stdcall';
function VDrawingCopy(Target: LongWord; X,Y,Width,Height: Integer; Source: LongWord; SourceX,SourceY: Integer; Operation: LongWord): Boolean;
  external 'BitBlt@gdi32.dll stdcall';

var VDrawingToken: LongWord;

procedure VDrawingInitialize;
var Input: TVDrawingStartup;
begin
  if VDrawingToken<>0 then exit;
  Input.Version:=1; Input.DebugCallback:=0;
  Input.SuppressBackgroundThread:=False; Input.SuppressExternalCodecs:=False;
  if VDrawingStartup(VDrawingToken,Input,0)<>0 then RaiseException('Cannot initialize Vex installer drawing.');
end;

procedure VDrawingRound(DC: LongWord; X,Y,Width,Height,Radius: Integer; Color: TColor; Outline: Boolean);
var Graphics, Path, Brush, Pen, ARGB: LongWord; Left,Top,W,H,Diameter: Single;
begin
  if (Width<=0) or (Height<=0) then exit;
  VDrawingInitialize;
  Graphics:=0; Path:=0; Brush:=0; Pen:=0;
  Left:=X; Top:=Y; W:=Width; H:=Height;
  if Outline then begin Left:=Left+0.5; Top:=Top+0.5; W:=W-1; H:=H-1; end;
  Diameter:=ScaleX(Radius*2);
  if Diameter>W then Diameter:=W;
  if Diameter>H then Diameter:=H;
  ARGB:=$FF000000 or ((Color and $FF) shl 16) or (Color and $FF00) or ((Color shr 16) and $FF);
  try
    if VDrawingCreate(DC,Graphics)<>0 then exit;
    VDrawingSmoothing(Graphics,4);
    if VDrawingPathCreate(0,Path)<>0 then exit;
    VDrawingArc(Path,Left,Top,Diameter,Diameter,180,90);
    VDrawingArc(Path,Left+W-Diameter,Top,Diameter,Diameter,270,90);
    VDrawingArc(Path,Left+W-Diameter,Top+H-Diameter,Diameter,Diameter,0,90);
    VDrawingArc(Path,Left,Top+H-Diameter,Diameter,Diameter,90,90);
    VDrawingClose(Path);
    if Outline then begin
      if VDrawingPenCreate(ARGB,1,2,Pen)=0 then VDrawingOutline(Graphics,Pen,Path);
    end else if VDrawingBrushCreate(ARGB,Brush)=0 then VDrawingFill(Graphics,Brush,Path);
  finally
    if Pen<>0 then VDrawingPenDelete(Pen);
    if Brush<>0 then VDrawingBrushDelete(Brush);
    if Path<>0 then VDrawingPathDelete(Path);
    if Graphics<>0 then VDrawingDelete(Graphics);
  end;
end;
