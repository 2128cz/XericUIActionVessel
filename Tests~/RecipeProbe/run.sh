#!/usr/bin/env bash
# 字段配方的离线门：dotnet 编包里的纯逻辑并跑断言。exit 0=PASS, 1=FAIL, 2=跑不起来
set -u
HERE=$(cd "$(dirname "$0")" && pwd)
OUT="$HERE/.out"
mkdir -p "$OUT"
if ! command -v dotnet >/dev/null 2>&1; then
  echo "VERDICT RECIPE-PROBE INSTRUMENT-BAD (dotnet not on PATH)"; exit 2
fi
dotnet run --project "$HERE/RecipeProbe.csproj" -c Release --nologo 2>&1 | tee "$OUT/run.log"
rc=${PIPESTATUS[0]}
if ! grep -q "VERDICT RECIPE-PROBE" "$OUT/run.log"; then
  echo "VERDICT RECIPE-PROBE INSTRUMENT-BAD (no verdict line; see $OUT/run.log)"; exit 2
fi
exit "$rc"
