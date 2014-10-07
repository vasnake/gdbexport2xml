GdbExport2Xml is .NET console program for one task:
do the same things as ESRI ArcCatalog do by context menu command
"Export - XML Workspace Document". Woks fine with SDE GDB featureclasses.

Copyright (C) 1996-2010, ALGIS LLC
Originally by Valik <vasnake@gmail.com>, 2010
Based on code from http://edndoc.esri.com/arcobjects/9.2/NET/10fe68bf-01c4-4b50-8458-04cba2cd230a.htm

Licensed under GNU GENERAL PUBLIC LICENSE (http://www.gnu.org/licenses/gpl.txt)

usage example: make CMD file with code like this:

@echo off
set wd=%~dp0
pushd "%wd%"
pushd c:\VisualStudio2008\Projects\gdbexport2xml\gdbexport2xml\bin\Release

gdbexport2xml.exe -c=c:\sde\rngis.rgo.sde -t=RGO.DEPOSITS_A,RGO.GEOCHEM_L -f=rgo.xml
@rem rngis.rgo.sde -- link to geodatabase created in ArcCatalog
@rem RGO.DEPOSITS_A,RGO.GEOCHEM_L -- featureclasses that will be exported
@rem rgo.xml -- output file name

popd
popd
exit
