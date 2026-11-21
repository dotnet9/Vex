@echo off
call "D:\Program Files\Microsoft Visual Studio\18\Insiders\VC\Auxiliary\Build\vcvars64.bat" >nul 2>&1
cl /LD /O2 /MT synch_shim.c shim.def /Fe:api-ms-win-core-synch-l1-2-0.dll
