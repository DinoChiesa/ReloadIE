@echo off
goto START

-------------------------------------------------------
 MakeZips.cmd

 Makes the zips for ReloadIt

 Mon, 05 Sep 2011  19:07

-------------------------------------------------------

:START
setlocal

set zipit=c:\users\dino\bin\zipit.exe
set stamp=%DATE% %TIME%
set stamp=%stamp:/=-%
set stamp=%stamp: =-%
set stamp=%stamp::=%

:: get the version
for /f "delims==" %%I in ('type ReloadIt\Properties\AssemblyInfo.cs ^| c:\bin\grep AssemblyVersion ^| c:\bin\sed -e "s/^.*(.\(.*\).).*/\1/"') do set longversion=%%I

set version=%longversion:~0,3%
echo version is %version%

set rdir=%cd%\releases\v%longversion%

echo making release dir %rdir%
if exist %rdir% (
  echo Error: Release dir already exists.
  goto ALL_DONE
)
mkdir %rdir%

echo built on %stamp% > %rdir%\BuiltOn-%stamp%.txt

c:\.net4.0\msbuild.exe /p:Configuration=Release /t:Clean
c:\.net4.0\msbuild.exe /p:Configuration=Release

call :MakeBinZip

c:\.net4.0\msbuild.exe /p:Configuration=Debug /t:Clean

call :MakeSourceZip

goto ALL_DONE


-------------------------------------------------------
:CleanJunk

  echo.
  echo +++++++++++++++++++++++++++++++++++++++++++++++++++++++
  echo.
  echo Removing junk files...
  echo.

  del ReloadIt\*_flymake.cs
  del ReloadIt\Properties\*_flymake.cs

  goto :EOF
-------------------------------------------------------


-------------------------------------------------------
:MakeBinZip

  echo.
  echo +++++++++++++++++++++++++++++++++++++++++++++++++++++++
  echo.
  echo Making the Bin zip...
  echo.

set binzip=%rdir%\ReloadIt-v%longversion%-bin.zip
%zipit%  %binzip%  -s Readme.txt "This is the binary distribution for ReloadIt v%version%. Packed %stamp%.  Instructions: Unpack these files into a directory and run the ReloadIt.exe.  See http://reloadit.codeplex.com for more information, or for updates."  License.txt -d . -D ReloadIt\bin\Release -E *.*

  goto :EOF
-------------------------------------------------------


-------------------------------------------------------
:MakeSourceZip

  echo.
  echo +++++++++++++++++++++++++++++++++++++++++++++++++++++++
  echo.
  echo Making the source zip...
  echo.

cd ..
set srczip=%rdir%\ReloadIt-v%longversion%-src.zip
%zipit%  %srczip%  -s Readme.txt  "This is the source distribution for ReloadIt v%version%. Packed %stamp%."   -r+  -D ReloadIt -E License.txt  -E  "((name = *.cs) or (name = *.xaml) or (name = *.ico) or (name = *.resx) or (name = *.settings) or (name = *.csproj) or (name = *.sln) or (name = *.png)) and (name != *\obj\*.*) and (name != *\bin\*.*) and (name != *\notused\*.*) and (name != *_flymake.cs)"

cd ReloadIt

  goto :EOF
-------------------------------------------------------


:ALL_DONE

  echo.
  echo +++++++++++++++++++++++++++++++++++++++++++++++++++++++
  echo.
  echo done.
  echo.

endlocal
