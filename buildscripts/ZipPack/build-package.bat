@echo off

rem get parameters that are passed by visual studio post build event
SET outDllPath=%1

rem make sure the initial working directory is the one containing the current script
SET scriptPath=%~dp0
SET initialWD=%CD%
cd %scriptPath%

cd ..\..\
xcopy /y src\bin\Release\Kerbalism.dll GameData\Kerbalism\Kerbalism.dll*
xcopy /y src\bin\Release\Kerbalism.dll %outDllPath%\Kerbalism.dll*

rd /s /q package
mkdir package
cd package
mkdir GameData
cd GameData
mkdir Kerbalism
cd Kerbalism

xcopy /y /e ..\..\..\GameData\Kerbalism\* .
xcopy /y ..\..\..\CHANGELOG.md .
xcopy /y ..\..\..\License .
xcopy /y ..\..\..\README.md .

IF EXIST ..\..\..\Kerbalism.zip del ..\..\..\Kerbalism.zip
"%scriptPath%7za.exe" a ../../../Kerbalism.zip ../../GameData
cd "%scriptPath%..\..\"
rd /s /q package

cd %initialWD%
