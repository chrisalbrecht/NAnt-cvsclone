// NAnt - A .NET build tool
// Copyright (C) 2002-2003 Gerry Shaw
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//

// Scott Hernandez(ScottHernandez@hotmail.com)

using System;
using System.IO;
using System.Configuration;
using System.Reflection;
using System.Globalization;

namespace SourceForge.NAnt {
    /// <summary>
    /// Stub used to created AppDamain and launch real ConsoleDriver class in Core assembly.
    /// </summary>
    public class ConsoleStub {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Entry point for executable
        /// </summary>
        /// <param name="args">Command Line arguments</param>
        /// <returns>The result of the real execution</returns>
        public static int Main(string[] args) {
            AppDomain cd = AppDomain.CurrentDomain;
            AppDomain executionAD = cd;

            string nantShadowCopyFilesSetting = ConfigurationSettings.AppSettings.Get("nant.shadowfiles");
            string nantCleanupShadowCopyFilesSetting = ConfigurationSettings.AppSettings.Get("nant.shadowfiles.cleanup");

            logger.Debug(string.Format(
                CultureInfo.InvariantCulture,
                "Shadowing files({0}) -- cleanup={1}", 
                nantShadowCopyFilesSetting, 
                nantCleanupShadowCopyFilesSetting));

            if(nantShadowCopyFilesSetting != null && bool.Parse(nantShadowCopyFilesSetting) == true) {

                System.AppDomainSetup myDomainSetup = new System.AppDomainSetup();

                myDomainSetup.PrivateBinPath = myDomainSetup.ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;

                logger.Debug(string.Format(
                                CultureInfo.InvariantCulture,
                                "NAntDomain.PrivateBinPath={0}", 
                                myDomainSetup.PrivateBinPath));

                myDomainSetup.ApplicationName = "NAnt";
                
                //copy the config file location
                myDomainSetup.ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
                
                logger.Debug(string.Format(
                    CultureInfo.InvariantCulture,
                    "NAntDomain.ConfigurationFile={0}", 
                    myDomainSetup.ConfigurationFile));

                //yes, cache the files
                myDomainSetup.ShadowCopyFiles = "true";

                //but what files you say... everything in ".", "./bin", "./tasks" .
                myDomainSetup.ShadowCopyDirectories=myDomainSetup.PrivateBinPath + ";" + Path.Combine(myDomainSetup.PrivateBinPath,"bin") + ";" + Path.Combine(myDomainSetup.PrivateBinPath,"tasks") + ";";
                
                logger.Debug(string.Format(
                    CultureInfo.InvariantCulture,
                    "NAntDomain.ShadowCopyDirectories={0}", 
                    myDomainSetup.ShadowCopyDirectories));

                //try to cache in .\cache folder, if that fails, let the system figure it out.
                string cachePath = Path.Combine(myDomainSetup.ApplicationBase, "cache");
                DirectoryInfo cachePathInfo = null;
                try{
                   cachePathInfo = Directory.CreateDirectory(cachePath);
                }
                catch(Exception e){
                    Console.WriteLine("Failed to create: {0}. Using default CachePath.", cachePath);
                }
                finally{
                    if(cachePathInfo != null)
                        myDomainSetup.CachePath = cachePathInfo.FullName;

                    logger.Debug(string.Format(
                        CultureInfo.InvariantCulture,
                        "NAntDomain.CachePath={0}", 
                        myDomainSetup.CachePath));
                }

                //create the domain.
                executionAD = AppDomain.CreateDomain(myDomainSetup.ApplicationName, null, myDomainSetup);

                logger.Debug(string.Format(
                    CultureInfo.InvariantCulture,
                    "NAntDomain.SetupInfo:\n{0}", 
                    executionAD.SetupInformation));
            }

            //use helper object to hold (and serialize) args for callback.
            logger.Debug(string.Format(
                CultureInfo.InvariantCulture,
                "Creating HelperArgs({0})", 
                args.ToString()));
            
            helperArgs helper = new helperArgs(args);
            executionAD.DoCallBack(new CrossAppDomainDelegate(helper.CallConsoleRunner));

            //unload if remote/new appdomain
            if(!cd.Equals(executionAD)) {
                
                /*
                Console.WriteLine();
                Console.WriteLine("> Unloading AppDomain and Assemblies");
                foreach(Assembly ass in executionAD.GetAssemblies()){
                    Console.WriteLine("> {0}", ass.Location, ass.CodeBase);
                }
                */

                string cachePath = executionAD.SetupInformation.CachePath;

                logger.Debug(string.Format(
                    CultureInfo.InvariantCulture,
                    "Unloading '{0}' AppDomain", 
                    executionAD.FriendlyName));

                AppDomain.Unload(executionAD);
                if(nantCleanupShadowCopyFilesSetting != null && bool.Parse(nantCleanupShadowCopyFilesSetting) == true) {
                    logger.Debug(string.Format(
                        CultureInfo.InvariantCulture,
                        "Unloading '{0}' AppDomain", 
                        executionAD.FriendlyName));
                    try {
                        logger.Debug(string.Format(
                            CultureInfo.InvariantCulture,
                            "Cleaning up CacheFiles in '{0}'", 
                            cachePath));

                        Directory.Delete(cachePath, true);
                    }
                    catch (FileNotFoundException fnf){
                        logger.Debug(string.Format(
                            CultureInfo.InvariantCulture,
                            "Files not found...?: {0}", 
                            fnf));
                    }
                    catch (Exception e) {
                        Console.WriteLine("Unable to delete cache({1}).\n {0}", e.ToString(), cachePath);
                    }
                }
            }

            if(helper == null || helper.Return == -1) {
                logger.Debug(string.Format(
                    CultureInfo.InvariantCulture,
                    "Return Code null or -1"));
                throw new ApplicationException("No return code set!");
            } else {
                logger.Debug(string.Format(
                    CultureInfo.InvariantCulture,
                    "Return Code = {0}", 
                    helper.Return));
                return helper.Return;
            }
        }

        [Serializable]
        public class helperArgs : MarshalByRefObject {
            private string[] args = null;
            private int ret = -1;

            private helperArgs(){}
            public helperArgs(string[] args0) {this.args = args0;}

            public void CallConsoleRunner() {

                //load the core by name!
                Assembly nantCore = AppDomain.CurrentDomain.Load("NAnt.Core");

                logger.Info(string.Format(
                    CultureInfo.InvariantCulture,
                    "NAnt.Core Loaded: {0}", 
                    nantCore.FullName));

                //get the ConsoleDriver by name
                Type consoleDriverType = nantCore.GetType("SourceForge.NAnt.ConsoleDriver", true, true);
                MethodInfo mainMethodInfo = null;
                //find the Main Method, this method is less than optimal, but other methods failed.
                foreach(MethodInfo meth in consoleDriverType.GetMethods(BindingFlags.Static | BindingFlags.Public)) {
                    if(meth.Name.Equals("Main"))
                        mainMethodInfo = meth;
                }
                ret = (int) mainMethodInfo.Invoke(null, new Object[] {args});

                logger.Debug(string.Format(
                    CultureInfo.InvariantCulture,
                    "'{0}' returned {1}", 
                    mainMethodInfo.ToString(), ret));
            }
            public int Return { get { return ret;}}
        }
    }
}
