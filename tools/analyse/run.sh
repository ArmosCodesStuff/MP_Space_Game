#!/bin/sh
# Code analysers over the game scripts (unused private members, unread fields, unused
# assignments and parameters, needless usings, unreachable code). Needs the smoke test's
# restored build folder (run tools/smoketest/run.sh once first): package restore is not
# possible offline. Prints each finding; the last line is the count.
set -e
B=/tmp/warships_smoke
[ -d "$B/obj" ] || { echo "run tools/smoketest/run.sh first (it restores the build)"; exit 2; }
cp "$(dirname "$0")/analysers.editorconfig" "$B/.editorconfig"
cd "$B"
dotnet build --no-restore -v q -nologo -p:EnforceCodeStyleInBuild=true -p:GenerateDocumentationFile=true \
  -p:NoWarn=CS1591%3BCS1573%3BCS1587 > analyse.log 2>&1 || true
rm -f "$B/.editorconfig"
grep -E "warning (IDE|CS)[0-9]+" analyse.log | grep -v "_Test.cs" | sed 's#.*/scripts/##; s/ \[.*//' | sort -u > findings.txt || true
cat findings.txt
echo "ANALYSERS: $(wc -l < findings.txt) findings"
