#!/bin/bash
# Build step of the companion host image: a navmesh for every map that has geometry, into /nav.
#
# The repository holds the navmeshes of the maps the natural runs play (game-server/data/nav). A companion goes
# wherever its player leads it, so the image bakes the rest itself, from the same geometry and with the same tool
# (tools/Aion.NavBake). /navcache is a build cache that outlives a build: a map is baked again only when the tool says
# its inputs or the bake settings changed, so a build after the first takes about the time it takes to check.
set -euo pipefail

root=/src
cache=/navcache
tool=(dotnet /navbake/Aion.NavBake.dll)
mkdir -p "$cache"

# check exits 1 when a map is missing or stale, and names each one: "<map id>: MISSING" or "<map id>: STALE (...)".
report=$("${tool[@]}" check --maps all --out "$cache" --root "$root" || true)
stale=$(printf '%s\n' "$report" | grep -E '^[0-9]+: (MISSING|STALE)' | cut -d: -f1 | paste -sd, - || true)
if [ -n "$stale" ]; then
    echo "Baking $(printf '%s\n' "$stale" | tr ',' '\n' | grep -c .) navmesh(es)."
    "${tool[@]}" bake --maps "$stale" --out "$cache" --root "$root" | grep -v ' tiles, ' || true
    # Every map current now, or the image is not built.
    report=$("${tool[@]}" check --maps all --out "$cache" --root "$root") || { printf '%s\n' "$report" | grep -v ': ok$'; exit 1; }
fi
if ! printf '%s\n' "$report" | grep -q ': ok$'; then
    echo "The bake tool named no map."
    exit 1
fi

mkdir -p /nav
cp -a "$cache"/. /nav/
echo "Navmeshes in the image: $(find /nav -name '*.navmesh' | wc -l)."
