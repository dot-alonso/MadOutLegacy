#!/usr/bin/env bash
cd "$(dirname "$0")" || exit 1


# -------- CONFIG ----------

Port=7800
MaxPlayers=16
GameMode="None"
Master=true

serversList=""
eventsList=""
verboseServerConsole=false
masterDebug=false

# --------------------------


Args=()
[[ "$Master" == "true" ]] && Args+=("-masterServer")
[[ "$masterDebug" == "true" ]] && Args+=("-masterDebug")
[[ "$verboseServerConsole" == "true" ]] && Args+=("-verboseServerConsole")
[[ -n "$serversList" ]] && Args+=("-serversList:\"$serversList\"")
[[ -n "$eventsList" ]] && Args+=("-eventsList:\"$eventsList\"")

./run_bepinex.sh ./game -bend_GameServer -batchmode -nographics "-port:$Port" "-maxConn:$MaxPlayers" "-Mode:$GameMode" "${Args[@]}"
