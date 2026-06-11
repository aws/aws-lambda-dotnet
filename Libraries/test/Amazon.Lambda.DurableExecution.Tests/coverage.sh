#!/usr/bin/env bash
set -e
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
PROJ="$HERE/Amazon.Lambda.DurableExecution.Tests.csproj"
OUT="$HERE/TestResults"

rm -rf "$OUT"
dotnet test "$PROJ" -c Release \
  --collect:"XPlat Code Coverage" \
  --settings "$HERE/coverage.runsettings" \
  --results-directory "$OUT"

REPORT_FILE=$(find "$OUT" -name "coverage.cobertura.xml" -type f | head -1)
if [ -z "$REPORT_FILE" ]; then
  echo "No coverage report found under $OUT"
  exit 1
fi

reportgenerator \
  "-reports:$REPORT_FILE" \
  "-targetdir:$OUT/report" \
  "-reporttypes:Html;TextSummary"

echo
echo "==================== Coverage Summary ===================="
cat "$OUT/report/Summary.txt"
echo "=========================================================="
echo "Full HTML report: $OUT/report/index.html"
