#!/usr/bin/env bash
# Runs Living World's dependency-free behaviour suites without RimWorld, Unity, Harmony or a game
# install — the same approach as the Regions & Territories harness this residency suite moved from.
#
# The residency code (ResidentPawnComp, ResidencyUtility) is deliberately game-free: no Find, no
# Harmony, no Unity. That is what lets it compile against the hand-written doubles in RimWorldStubs.cs
# and ResidencyStubs.cs and run anywhere mono exists.
#
#   Ubuntu/WSL:  sudo apt-get install -y mono-mcs mono-runtime
#   Then:        Tests/run-tests.sh
#
# Exits non-zero if any suite fails to build or fails an assertion.
set -u

cd "$(dirname "$0")/.." || exit 1

SRC=Source
OUT="${TMPDIR:-/tmp}/lw-tests"
mkdir -p "$OUT"

MCS_FLAGS="-target:exe -langversion:latest -nowarn:0169,0414,0649,0219"
failures=0

run_suite() {
    name=$1; shift
    binary="$OUT/$name.exe"
    rm -f "$binary"

    if ! mcs $MCS_FLAGS -out:"$binary" "$@"; then
        echo "BUILD FAILED: $name"
        failures=$((failures + 1))
        return
    fi

    if ! mono "$binary"; then
        failures=$((failures + 1))
    fi
}

# ResidencyInjector is not here: it is startup-only def surgery (StaticConstructorOnStartup over
# DefDatabase) with no behaviour to assert away from a running game, like the map modes.
run_suite residency \
    Tests/RimWorldStubs.cs Tests/ResidencyStubs.cs Tests/ResidencyTests.cs \
    $SRC/Residency/ResidentPawnComp.cs $SRC/Residency/ResidencyUtility.cs

echo
if [ "$failures" -eq 0 ]; then
    echo "ALL SUITES PASSED"
    exit 0
fi

echo "$failures SUITE(S) FAILED"
exit 1
