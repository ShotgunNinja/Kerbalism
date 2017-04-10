@echo off

rem get parameters that are passed by visual studio post build event
SET outDllPath=%1

rem make sure the initial working directory is the one containing the current script
SET scriptPath=%~dp0
SET initialWD=%CD%

rem generate the MDB file needed by Monodevelop and UnityVS for debugging
rem see http://forum.kerbalspaceprogram.com/index.php?/topic/102909-ksp-plugin-debugging-for-visual-studio-and-monodevelop-on-all-os/&page=1
rem for information on how to setup your debugging environment

echo "Generating MonoDevelop Debug file..."
echo "Kerbalism.dll -> %outDllPath%Kerbalism.dll.mdb"
cd %outDllPath%
"%scriptPath%\pdb2mdb.exe" Kerbalism.dll

cd %initialWD%
