[Code]
{ Signal readiness only after the installer window is shown.
  The Inno bootstrap process alone cannot confirm readiness. }
function VOpenUpdateEvent(Access: LongWord; Inherit: Boolean; Name: String): LongWord;
  external 'OpenEventW@kernel32.dll stdcall';
function VSetUpdateEvent(Handle: LongWord): Boolean;
  external 'SetEvent@kernel32.dll stdcall';
function VCloseUpdateEvent(Handle: LongWord): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';

procedure VSignalUpdateReady;
var Name: String; Handle: LongWord;
begin
  if WizardSilent then exit;
  Name:=ExpandConstant('{param:VEXUPDATEREADY|}');
  if (Pos('Local\Vex.Update.',Name)<>1) or (Length(Name)<>49) then exit;
  Handle:=VOpenUpdateEvent(2,False,Name);
  if Handle=0 then exit;
  try
    VSetUpdateEvent(Handle);
    Log('Vex update: installer window ready.');
  finally
    VCloseUpdateEvent(Handle);
  end;
end;
