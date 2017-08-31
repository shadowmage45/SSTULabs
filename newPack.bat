md Build
md Build\Source
md Build\Source\Plugin
md Build\Source\Shaders
rd GameData\KSPWheel /s /q
cd..
cd KSPWheel
git checkout master
xcopy GameData ..\SSTULabs\GameData /e /i
git checkout dev
cd..
cd SSTULabs
xcopy GameData Build\GameData /e /i
copy LICENSE-ASSETS.txt Build\GameData\LICENSE-ASSETS.txt
copy LICENSE-SOURCE.txt Build\GameData\LICENSE-SOURCE.txt
copy Installation_Instructions.txt Build\Installation_Instructions.txt
xcopy Plugin\SSTUTools\SSTUTools Build\Source\Plugin /e /i
rd Build\Source\Plugin\bin /s /q
rd Build\Source\Plugin\libs /s /q
rd Build\Source\Plugin\obj /s /q
rd Build\GameData\SSTU-OptionalPatches /s /q
rd Build\GameData\SSTU-TextureSets /s /q
del Build\Source\Plugin\.gitignore
del Build\Source\Plugin\SSTUTools.csproj
del Build\Source\Plugin\SSTUTools.csproj.user
xcopy CustomShaders Build\Source\Shaders
powershell.exe -nologo -noprofile -command "& { Add-Type -A 'System.IO.Compression.FileSystem'; [IO.Compression.ZipFile]::CreateFromDirectory('Build', '../SSTU.zip'); }"
rd build /s /q
pause