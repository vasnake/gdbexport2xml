// GdbExport2Xml is .NET console program for one task:
// do the same things as ESRI ArcCatalog do by context menu command
// "Export - XML Workspace Document", I meant SDE GDB featureclasses.
//
// Copyright (C) 1996-2010, ALGIS LLC
// Originally by Valik <vasnake@gmail.com>, 2010
// Based on code from http://edndoc.esri.com/arcobjects/9.2/NET/10fe68bf-01c4-4b50-8458-04cba2cd230a.htm
//
//    This file is part of GdbExport2Xml.
//
//    GdbExport2Xml is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    GdbExport2Xml is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with GdbExport2Xml.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.GeoDatabaseDistributed;
//using Mono.Options;

namespace gdbexport2xml {
    public enum progResultCode : int {
        good = 0,
        bad = 1,
        unknown = 2
    }

    class Log {
        public static int level = 1;

        public static void p(string str, int dbgLev, string dst) {
            if (level < dbgLev) {
                return;
            }
            if (dst == "err" || dst == "both") {
                System.Console.Error.Write(str + "\n");
            }
            if (dst == "both" || dst == "log") {
                System.Console.Out.Write(str + "\n");
            }
        }
        public static void p(string str, int dbgLev) {
            p(str, dbgLev, "err");
        }
        public static void p(string str, string dst) {
            p(str, 1, dst);
        }
        public static void p(string str) {
            p(str, 1, "err");
        }

        public static void rp(string str) {
            System.Console.Write(str);
        }
    } // class Log


    class expGDB2XML {
        public IAoInitialize
            mLicInit = null;
        public String
            mSdeConnFileName = "", mTabName = "", mExpFileName = "";
        public bool
            mNeedData = false, mNeedMeta = false;

        public
            expGDB2XML(String sdeConnFileName, String tabName,
                bool expData, bool getMeta, string fname) {
            mSdeConnFileName = sdeConnFileName;
            mTabName = tabName;
            mExpFileName = fname;
            mNeedData = expData;
            mNeedMeta = getMeta;
        } // constructor expGDB2XML


        public void
            initLic() {
            try {
                IAoInitialize ini = new AoInitializeClass();
                mLicInit = ini;
                esriLicenseProductCode pCode =
                    esriLicenseProductCode.esriLicenseProductCodeArcEditor;
                //esriLicenseProductCode.esriLicenseProductCodeArcServer;
                // work on cli: esriLicenseProductCode.esriLicenseProductCodeArcView;
                // work on srv: esriLicenseProductCode.esriLicenseProductCodeArcServer;
                esriLicenseStatus licstat = ini.Initialize(pCode);
                Log.p("Lic.stat is [" + licstat + "]");
                if (licstat == esriLicenseStatus.esriLicenseAlreadyInitialized ||
                    licstat == esriLicenseStatus.esriLicenseAvailable ||
                    licstat == esriLicenseStatus.esriLicenseCheckedOut) {
                    //good
                    Log.p("Lic.available");
                }
                else {
                    //bad
                    Log.p("Lic.not available, try another");
                    pCode = esriLicenseProductCode.esriLicenseProductCodeArcServer;
                    licstat = ini.Initialize(pCode);
                    Log.p("Lic.stat2 is [" + licstat + "]");
                }
                if (ini.InitializedProduct() == pCode) {
                    Log.p("OK, have good lic.");
                }
                else {
                    Log.p("prod.code is [" + pCode + "] but inited prod.code is [" +
                        ini.InitializedProduct() + "]");
                }
            }
            catch (Exception e) {
                Log.p("ERR, initLic exception: " + e.Message);
                throw e;
            }
        } // method initLic


        public void shutdown() {
            if (mLicInit != null) mLicInit.Shutdown();
        }


        private IEnumNameEdit getFCNames(IWorkspace wsp, String tabnames) {
            Log.p("get featureclass list...");
            IEnumNameEdit edtNames = new NamesEnumeratorClass();
            String[] tabs = tabnames.Split(',');
            int numItems = 0;
            IEnumDatasetName dsetNames = wsp.get_DatasetNames(
                esriDatasetType.esriDTFeatureClass);
            //esriDatasetType.esriDTFeatureDataset);

            IDatasetName dsetName = null;
            foreach (String t in tabs) {
                Log.p(String.Format("    tabname is [{0}]", t));
                dsetNames.Reset();
                while (true) {
                    dsetName = dsetNames.Next();
                    if (dsetName == null) {
                        break;
                    }
                    //Log.p(String.Format("    check feature class name [{0}]", dsetName.Name));
                    if (t.ToUpper() == dsetName.Name.ToUpper()) {
                        Log.p(String.Format("    add feature class name [{0}]", dsetName.Name));
                        edtNames.Add((IName)dsetName);
                        numItems += 1;
                        break;
                    }
                }
                if (dsetName == null) {
                    Log.p(String.Format("Error, tabname [{0}] will not exported", t), "both");
                }
            }

            if (numItems != tabs.GetLength(0)) {
                throw new ArgumentException("Can't find all of tables in gdb.");
            }
            return edtNames;
        } // getFCNames


        public void
            doWork() {
            Log.p("doWork started...");
            String sdeconnfname = mSdeConnFileName; // "c:\\t\\test.sde";
            String tabnames = mTabName; // "T.TAB1,T.TAB2";            

            Log.p("Open the source gdb");
            IWorkspaceFactory wspFact = new SdeWorkspaceFactoryClass();
            IWorkspace wsp = wspFact.OpenFromFile(sdeconnfname, 0);

            Log.p("Get FC names");
            IEnumNameEdit edtNames = getFCNames(wsp, tabnames);
            IEnumName names = (IEnumName)edtNames;

            Log.p("Create a scratch workspace factory");
            IScratchWorkspaceFactory scrWspFact = new ScratchWorkspaceFactoryClass();
            IWorkspace scrWsp = scrWspFact.CreateNewScratchWorkspace();
            IDataset dset = (IDataset)scrWsp;
            IName scrWspName = dset.FullName;

            Log.p("Create a Transfer object and a name mapping");
            IGeoDBDataTransfer trans = new GeoDBDataTransferClass();
            IEnumNameMapping nameMaps = null;
            Boolean hasConflicts = trans.GenerateNameMapping(
                names, scrWspName, out nameMaps);
            if (hasConflicts) {
                throw new ArgumentException("Name mapping has conflicts.");
            }

            bool expData = mNeedData;
            string fname = mExpFileName;
            bool getMeta = mNeedMeta;
            bool compressed = false;
            bool binaryGeom = true;
            IGdbXmlExport exp = new GdbExporterClass();
            if (expData == false) {
                Log.p(String.Format("Export schema (u need sdeexport for data); file [{0}], metadata [{1}]",
                    fname, getMeta));
                exp.ExportDatasetsSchema(nameMaps, fname, compressed, getMeta);
            }
            else {
                Log.p(String.Format("Export schema&data; file [{0}], metadata [{1}]",
                    fname, getMeta));
                exp.ExportDatasets(nameMaps, fname, binaryGeom, compressed, getMeta);
            }

            Log.p("OK, xml writed.", "both");
        } // method doWork

    } // class expGDB2XML


    class Program {
        public static expGDB2XML mTask = null;

        static void parseArgs(string[] args) {
            String sde = "", tab = "";
            bool expData = false, getMeta = false;
            string fname = "gdbexp.xml";
            bool showHelp = false;

            var opt = new Mono.Options.OptionSet() {
                {"h|?|help", "Show usage help", v => showHelp = true},
                {"c=", "Sde connection filename (c:\\db.sde)", v => sde = v},
                {"t=", "Tab names list (T.TAB1,T.TAB2)", v => tab = v},
                {"d", "If set, export data (not only schema)", v => expData = true},
                {"m", "If set, export metadata", v => getMeta = true},
                {"f=", "Output filename (gdbexp.xml by default)", v => fname = v},
            };
            opt.Parse(args);

            if (sde == "" || tab == "" || showHelp != false) {
                opt.WriteOptionDescriptions(Console.Out);
                throw (new Exception("Wrong args, " +
                    "usage example: gdbexport2xml.exe -c=c:\\t\\test.sde -t=T.TAB1,T.TAB2"));
            }

            Log.p(String.Format("get params: sdeConnFname [{0}], tabNames [{1}], " +
                "expData [{2}], expMeta [{3}], expFile [{4}]",
                sde, tab, expData, getMeta, fname));
            expGDB2XML p = new expGDB2XML(sde, tab, expData, getMeta, fname);
            mTask = p;
        } // method parseArgs


        static void Main(string[] args) {
            Log.level = 3;
            Log.p("export GDB2XML program working...");
            Environment.ExitCode = (int)progResultCode.good;
            try {
                parseArgs(args);
                mTask.initLic();
                mTask.doWork();
                //finally: mTask.shutdown();
            }
            catch (COMException e) {
                Log.p("COM Error", "both"); Log.p(e.Message);
                Environment.ExitCode = (int)progResultCode.bad;
            }
            catch (Exception e) {
                Log.p("Error", "both"); Log.p(e.Message);
                Environment.ExitCode = (int)progResultCode.bad;
            }
            finally {
                Log.p("Program done.");
                if (mTask != null) mTask.shutdown();
            }
        } // method Main
    }
}
