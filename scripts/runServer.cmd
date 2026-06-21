@echo off
chcp 65001 >nul
cd %~dp0


:: -------- CONFIG ----------

set Port=7800
set MaxPlayers=16
set GameMode=None
set Master=true

set maxCars=100
set maxNikLength=20

set serversList=""
set eventsList=""
set respawnPoints=""
set useVanillaRespawn=false
set verboseServerConsole=false
set noServerConsole=false
set masterDebug=false

:: --------------------------


set Args=
if [%Master%] == [true] (set Args=%Args%-masterServer )
if [%masterDebug%] == [true] (set Args=%Args%-masterDebug )
if [%verboseServerConsole%] == [true] (set Args=%Args%-verboseServerConsole )
if [%useVanillaRespawn%] == [true] (set Args=%Args%-useVanillaRespawn )
if [%noServerConsole%] == [true] (set Args=%Args%-noServerConsole )
set "match1=0"
if [%serversList%] == [""] set "match1=1"
if [%serversList%] == [] set "match1=1"
if %match1% == 0 (set Args=%Args%-serversList:%serversList% )
set "match2=0"
if [%eventsList%] == [""] set "match2=1"
if [%eventsList%] == [] set "match2=1"
if %match2% == 0 (set Args=%Args%-eventsList:%eventsList% )
set "match3=0"
if [%respawnPoints%] == [""] set "match3=1"
if [%respawnPoints%] == [] set "match3=1"
if %match3% == 0 (set Args=%Args%-respawnPoints:%respawnPoints% )

game.exe -bend_GameServer -batchmode -nographics -port:%Port% -maxConn:%MaxPlayers% -maxCars:%maxCars% -maxNikLength:%maxNikLength% -Mode:%GameMode% %Args%