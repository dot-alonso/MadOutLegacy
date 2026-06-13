@echo off
chcp 65001 >nul
cd %~dp0


:: -------- CONFIG ----------

set Port=7800
set MaxPlayers=16
set GameMode=None
set Master=true

set serversList=""
set eventsList=""
set verboseServerConsole=false
set masterDebug=false

:: --------------------------


set Args=
if [%Master%] == [true] (set Args=%Args%-masterServer )
if [%masterDebug%] == [true] (set Args=%Args%-masterDebug )
if [%verboseServerConsole%] == [true] (set Args=%Args%-verboseServerConsole )
set "match1=0"
if [%serversList%] == [""] set "match1=1"
if [%serversList%] == [] set "match1=1"
if %match1% == 0 (set Args=%Args%-serversList:%serversList% )
set "match2=0"
if [%eventsList%] == [""] set "match2=1"
if [%eventsList%] == [] set "match2=1"
if %match2% == 0 (set Args=%Args%-eventsList:%eventsList% )

game.exe -bend_GameServer -batchmode -nographics -port:%Port% -maxConn:%MaxPlayers% -Mode:%GameMode% %Args%