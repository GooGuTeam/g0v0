$ErrorActionPreference = "Stop"

# Native commands don't raise terminating errors, so check each exit code explicitly.
# InspectCode must not run after a failed build: it would cache unresolved symbols from the
# cleaned output tree, and later runs reuse those cached results for unchanged files.
dotnet tool restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# clean is required to ensure all code style errors are (re-)raised by compiler
dotnet clean ./osu.Desktop.slnf --verbosity=q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build -c Debug -warnaserror osu.Desktop.slnf -p:EnforceCodeStyleInBuild=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

./CheckSanity.ps1
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet jb inspectcode "osu.Desktop.slnf" --no-build --format=Text --stdout --caches-home="inspectcode" --verbosity=WARN

exit $LASTEXITCODE
