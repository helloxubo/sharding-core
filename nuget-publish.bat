:start
::定义版本
set EFCORE2=2.4.1.07
set EFCORE3=3.4.1.07
set EFCORE5=5.4.1.07
set EFCORE6=6.4.1.07

::删除所有bin与obj下的文件
@echo off
echo input nuget api key
set /p apiKey=

IF "apiKey"=="" (
    ECHO No NuGet API key provided &goto :start
)

set nowpath=%cd%
cd \
cd %nowpath%
::delete specify file(*.pdb,*.vshost.*)
for /r %nowpath% %%i in (*.pdb,*.vshost.*) do (del %%i && echo delete %%i)
 
::delete specify folder(obj,bin)
for /r %nowpath% %%i in (obj,bin) do (IF EXIST %%i (RD /s /q %%i && echo delete %%i))

echo clear complete

::构建
dotnet build -c Release
::publishing....
for /r %nowpath% %%i in (*.nupkg) do (dotnet nuget push %%i -k %apiKey% --source https://api.nuget.org/v3/index.json)

echo complete
pause