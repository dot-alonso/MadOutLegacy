#!/usr/bin/env bash
cd "$(dirname "$0")" || exit 1


# -------- CONFIG ----------

Port=7800
MaxPlayers=16
GameMode="None"
Master=true

maxCars=100
maxNikLength=20

serversList=""
eventsList=""
respawnPoints=""
useVanillaRespawn=false
verboseServerConsole=false
noServerConsole=false
masterDebug=false

# --------------------------


Args=()
[[ "$Master" == "true" ]] && Args+=("-masterServer")
[[ "$masterDebug" == "true" ]] && Args+=("-masterDebug")
[[ "$verboseServerConsole" == "true" ]] && Args+=("-verboseServerConsole")
[[ "$useVanillaRespawn" == "true" ]] && Args+=("-useVanillaRespawn")
[[ "$noServerConsole" == "true" ]] && Args+=("-noServerConsole")
[[ -n "$serversList" ]] && Args+=("-serversList:\"$serversList\"")
[[ -n "$eventsList" ]] && Args+=("-eventsList:\"$eventsList\"")
[[ -n "$respawnPoints" ]] && Args+=("-respawnPoints:\"$respawnPoints\"")

./run_bepinex.sh ./game -bend_GameServer -batchmode -nographics "-port:$Port" "-maxConn:$MaxPlayers" "-Mode:$GameMode" "-maxCars:$maxCars "-maxNikLength:$maxNikLength" "${Args[@]}"