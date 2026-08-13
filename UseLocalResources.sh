CSPROJ="osu.Game/osu.Game.csproj"
SLN="osu.sln"

dotnet remove $CSPROJ package ppy.osu.Game.Resources;
dotnet sln $SLN add osu.Game.Resources/osu.Game.Resources/osu.Game.Resources.csproj
dotnet add $CSPROJ reference osu.Game.Resources/osu.Game.Resources/osu.Game.Resources.csproj

SLNF="osu.Desktop.slnf"
TMP=$(mktemp)
jq '.solution.projects += ["osu.Game.Resources/osu.Game.Resources/osu.Game.Resources.csproj"]' $SLNF > $TMP
mv -f $TMP $SLNF
