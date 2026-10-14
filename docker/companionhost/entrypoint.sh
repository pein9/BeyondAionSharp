#!/bin/bash
# Runs the companion host (scenario CH-01 of tools/Aion.LiveBots) against the stack's own login and game servers, and
# keeps it running. Each start is a run of its own under /runs, where its traces and problems are; runs older than
# AION_COMPANION_KEEP_DAYS are removed.
set -u

switch=$(printf '%s' "${AION_COMPANIONS:-true}" | tr '[:upper:]' '[:lower:]')
if [ "$switch" != "true" ]; then
    echo "Companion host: companions are off (AION_COMPANIONS=${AION_COMPANIONS:-}); nothing to play."
    exec sleep infinity
fi

most=${AION_COMPANION_MAX_SEATS:-50}
spare=${AION_COMPANION_SPARE_SEATS:-5}
login_name=${AION_COMPANION_LOGIN_HOST:-loginserver}
game_name=${AION_COMPANION_GAME_HOST:-gameserver}
runs=${AION_COMPANION_RUN_DIR:-/runs}
keep_days=${AION_COMPANION_KEEP_DAYS:-14}

# The program takes addresses, not names.
address() { getent hosts "$1" | awk '{ print $1; exit }'; }

stopping=0
stop_file=""
trap 'stopping=1; [ -n "$stop_file" ] && touch "$stop_file"' TERM INT

while [ "$stopping" = 0 ]; do
    login=$(address "$login_name")
    game=$(address "$game_name")
    if [ -z "$login" ] || [ -z "$game" ]; then
        echo "Companion host: waiting for $login_name and $game_name."
        sleep 5 & wait $!
        continue
    fi
    find "$runs" -mindepth 1 -maxdepth 1 -type d -mtime +"$keep_days" -exec rm -rf {} + 2>/dev/null
    run="ch-$(date -u +%Y%m%d-%H%M%S)"
    out="$runs/$run"
    stop_file="$out/attach.stop"
    mkdir -p "$out"
    cd /repo
    echo "Companion host: run $run, login $login_name ($login), game $game_name ($game), $spare seats waiting, $most at the most."
    dotnet /app/Aion.LiveBots.dll --run "$run" --output "$out" --scenario CH-01 --bots "$most" --companion-spare "$spare" \
        --attach-target aion --stop-file "$stop_file" --host "$game" --login-host "$login" \
        --login-port "${AION_COMPANION_LOGIN_PORT:-2106}" --game-port "${AION_COMPANION_GAME_PORT:-7777}" --chat-port 10241 \
        --connect-timeout-seconds 10 --step-timeout-seconds 120 --seed 1 --git-sha "${AION_SOURCE_REVISION:-stack}" \
        --profile companion-host-aion --time-zone UTC --dashboard-port 0 &
    child=$!
    # A server's container that is made anew can come back at another address. The seats then cannot find it: the
    # program is asked to stop and is started again with the new one. A server that is only down keeps its address,
    # and the seats log in again by themselves.
    while kill -0 "$child" 2>/dev/null; do
        sleep 15 & wait $!
        [ "$stopping" = 1 ] && break
        now_login=$(address "$login_name")
        now_game=$(address "$game_name")
        if { [ -n "$now_login" ] && [ "$now_login" != "$login" ]; } || { [ -n "$now_game" ] && [ "$now_game" != "$game" ]; }; then
            echo "Companion host: a server has a new address; starting again."
            touch "$stop_file"
            break
        fi
    done
    wait "$child"
    echo "Companion host: the program ended with exit code $?."
    [ "$stopping" = 1 ] && break
    sleep 10 & wait $!
done
