// NAnt - A .NET build tool
// Copyright (C) 2001-2003 Gerry Shaw
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
// Clayton Harbour (claytonharbour@sporadicism.com)

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

using ICSharpCode.SharpCvsLib.Client;
using ICSharpCode.SharpCvsLib.Commands;
using ICSharpCode.SharpCvsLib.Messages;
using ICSharpCode.SharpCvsLib.Misc;
using ICSharpCode.SharpCvsLib.FileSystem;

using NAnt.Core;
using NAnt.Core.Attributes;
using NAnt.Core.Tasks;
using NAnt.Core.Types;
using NAnt.Core.Util;

namespace NAnt.SourceControl.Tasks {
    /// <summary>
    /// A base class for creating tasks for executing CVS client commands on a 
    /// CVS repository.
    /// </summary>
    public abstract class AbstractCvsTask : ExternalProgramBase {
		#region Protected Static Fields

		/// <summary>
		/// Name of the environmental variable specifying a users' home
		///		in a *nix environment.
		/// </summary>
		protected const String HOME = "HOME";
		/// <summary>
		/// Name of the password file that cvs stores pserver 
		///		cvsroot/ password pairings.
		/// </summary>
		protected const String CVS_PASSFILE = ".cvspass";
		/// <summary>
		/// The default compression level to use for cvs commands.
		/// </summary>
		protected const int DEFAULT_COMPRESSION_LEVEL = 3;
		/// <summary>
		/// The default use of binaries, defaults to use sharpcvs
		///		<code>true</code>.
		/// </summary>
        protected const bool DEFAULT_USE_SHARPCVSLIB = true;

		/// <summary>
		/// The name of the cvs executable.
		/// </summary>
		protected const string CVS_EXE = "cvs.exe";
		/// <summary>
		/// Environment variable that holds the executable name that is used for
		///		ssh communication.
		/// </summary>
		protected const string CVS_RSH = "CVS_RSH";
		/// <summary>
		/// Property value used to specify on a project level whether sharpcvs is
		///		used or not.
		/// </summary>
		protected const string USE_SHARPCVS = "sourcecontrol.usesharpcvslib";

		#endregion

        #region Private Instance Fields

        private string _cvsRoot;
        private string _module;
        private DirectoryInfo _destinationDirectory;
        private string _password;
        private string _passFile;
        private bool _useSharpCvsLib = DEFAULT_USE_SHARPCVSLIB;

		private string _commandName = null;
		private string _commandLine = null;
        private OptionCollection _commandOptions = new OptionCollection();
		private string _commandLineArguments;
        private OptionCollection _globalOptions = new OptionCollection();

		private string _exeName = CVS_EXE;
		private string _cvsRsh;
        private FileSet _fileset = new FileSet();

        #endregion Private Instance Fields

        #region Private Static Fields

        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        #endregion Private Static Fields

        #region Protected Instance Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AbstractCvsTask" /> 
        /// class.
        /// </summary>
        protected AbstractCvsTask () {
        }

        #endregion Protected Instance Constructors

        #region Public Instance Properties

		/// <summary>
		/// The name of the cvs executable.
		/// </summary>
		public override string ExeName {
			get {return _exeName;}
			set {this._exeName = value;}
		}

        /// <summary>
        /// <para>
        /// The cvs root variable has the following components.  The examples used is for the
        ///     NAnt cvsroot.
        ///     
        ///     protocol:       ext
        ///     username:       [username]
        ///     servername:     cvs.sourceforge.net
        ///     server path:    /cvsroot/nant
        /// </para>
        /// <para>
        /// Currently supported protocols include:
        /// </para>
        /// <list type="table">
        ///     <item>
        ///         <term>ext</term>
        ///         <description>
        ///         Used for securely checking out sources from a cvs repository.  
        ///         This checkout method uses a local ssh binary to communicate 
        ///         with the repository.  If you would like to secure password 
        ///         information then this method can be used along with public/private 
        ///         key pairs to authenticate against a remote server.
        ///         Please see: http://sourceforge.net/docman/display_doc.php?docid=761&amp;group_id=1
        ///         for information on how to do this for http://sourceforge.net.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>ssh</term>
        ///         <description>
        ///         Similar to the ext method.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>pserver</term>
        ///         <description>
        ///         The pserver authentication method is used to checkout sources 
        ///         without encryption.  Passwords are stored as plain text and 
        ///         all files are transported unencrypted.
        ///         </description>
        ///     </item>
        /// </list>
        /// </summary>
        /// <example>
        ///   <para>NAnt anonymous cvsroot:</para>
        ///   <code>
        ///   :pserver:anonymous@cvs.sourceforge.net:/cvsroot/nant
        ///   </code>
        /// </example>
        /// <example>
        ///   <para>Sharpcvslib anonymous cvsroot:</para>
        ///   <code>
        ///   :pserver:anonymous@cvs.sourceforge.net:/cvsroot/sharpcvslib
        ///   </code>
        /// </example>
        [TaskAttribute("cvsroot", Required=true)]
        [StringValidator(AllowEmpty=false)]
        public string CvsRoot {
            get { return this._cvsRoot; }
            set { _cvsRoot = StringUtils.ConvertEmptyToNull(value); }
        }

        /// <summary>
        /// The module to perform an operation on.
        /// </summary>
        /// <value>
        /// The module to perform an operation on.
        /// </value>
        /// <example>
        ///   <para>In Nant the module name would be:</para>
        ///   <code>nant</code>
        /// </example>
        [TaskAttribute("module", Required=true)]
        [StringValidator(AllowEmpty=false)]
        public string Module {
            get { return _module; }
            set { _module = StringUtils.ConvertEmptyToNull(value); }
        }

        /// <summary>
        /// Destination directory for the checked out / updated files.
        /// </summary>
        /// <value>
        /// The destination directory for the checked out or updated files.
        /// </value>
        /// <remarks>
        /// <para>
        /// This is the current working directory that will be modifed.
        /// </para>
        /// </remarks>
        [TaskAttribute("destination", Required=true)]
        public DirectoryInfo DestinationDirectory {
            get { return _destinationDirectory; }
            set { _destinationDirectory = value; }
        }

        /// <summary>
        /// The password for logging in to the CVS repository.
        /// </summary>
        /// <value>
        /// The password for logging in to the CVS repository.
        /// </value>
        [TaskAttribute("password")]
        public string Password {
            get { return _password;}
            set { _password = StringUtils.ConvertEmptyToNull(value); }
        }

        /// <summary>
        /// The full path to the .cvspass file (including the .cvspass file name).
        ///     This overrides the environment variable CVS_PASSFILE.
        /// </summary>
        [TaskAttribute("passfile")]
        public string PassFile {
            get {return this._passFile;}
            set {this._passFile = value;}
        }

        /// <summary>
        /// A collection of options that can be used to modify cvs 
        /// checkouts/updates.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Valid options include:
        /// </para>
        /// <list type="table">
        ///     <listheader>
        ///         <term>Name</term>
        ///         <description>Description</description>
        ///     </listheader>
        ///     <item>
        ///         <term>sticky-tag</term>
        ///         <description>
        ///         A revision tag or branch tag that has been placed on the 
        ///         repository using the 'rtag' or 'tag' commands.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>override-directory</term>
        ///         <description>
        ///         A directory to substitute for the module name as the top level 
        ///         directory name.  For instance specifying 'nant-cvs' for this
        ///         value while checking out NAnt would checkout the source files 
        ///         into a top level directory named 'nant-cvs' instead of nant.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>compression-level</term>
        ///         <description>
        ///         The compression level that files will be transported to and 
        ///         from the server at.  Valid numbers include 1 to 9.
        ///         </description>
        ///     </item>
        /// </list>
        /// </remarks>
        [BuildElementCollection("commandoptions", "option")]
        public OptionCollection CommandOptions {
            get { return _commandOptions;}
			set { this._commandOptions = value; }
        }

        /// <summary>
        /// Use commandOptions.
        /// </summary>
        [BuildElementCollection("options", "option")]
        public OptionCollection Options {
            get {return _commandOptions;}
        }

		/// <summary>
		/// The command-line arguments for the program.
		/// </summary>
        [TaskAttribute("commandline")]
		public string CommandLineArguments {
			get {return this._commandLineArguments;}
			set {this._commandLineArguments = StringUtils.ConvertEmptyToNull(value);}
		}

		/// <summary>
		/// Get the command line arguments for the task.
		/// </summary>
		public override string ProgramArguments {
			get {
				return this._commandLine;
			}
		}

		/// <summary>
		/// Holds a collection of globally available cvs options.
		/// </summary>
		[BuildElementCollection("globaloptions", "option")]
			public OptionCollection GlobalOptions {
			get {return this._globalOptions;}
		}

        /// <summary>
        /// <code>true</code> if the SharpCvsLib binaries that come bundled with 
        ///     NAnt should be used to perform the cvs commands, <code>false</code>
        ///     otherwise.  
        ///     
        ///     <warn>If you choose not to use SharpCvsLib to checkout from 
        ///         cvs you will need to include a cvs.exe binary in your
        ///         path.</warn>
        /// </summary>
        [TaskAttribute("usesharpcvslib", Required=false)]
        public bool UseSharpCvsLib {
			get {
				
 				System.Console.WriteLine("_useSharpCvsLib: " + _useSharpCvsLib);
				System.Console.WriteLine("Properties[USE_SHARPCVS]: " + 
					System.Convert.ToString((null == Properties[USE_SHARPCVS] || 
					System.Convert.ToBoolean(Properties[USE_SHARPCVS]))));
				return (_useSharpCvsLib && (null != Properties[USE_SHARPCVS] && 
					System.Convert.ToBoolean(Properties[USE_SHARPCVS])));
				//return _useSharpCvsLib;
			}
            set {this._useSharpCvsLib = value;}
        }

		/// <summary>
		/// The name of the cvs command that is going to be executed.
		/// </summary>
		public virtual string CommandName {
			get {return this._commandName;}
			set {this._commandName = value;}
		}

		/// <summary>
		/// Used to select the files to copy. 
		/// </summary>
		[BuildElement("fileset")]
		public virtual FileSet CvsFileSet {
			get { return _fileset; }
			set { _fileset = value; }
		}

		/// <summary>
		/// The executable to use for ssh communication.
		/// </summary>
		[TaskAttribute("cvsrsh", Required=false)]
		public string CvsRsh {
			get {return this._cvsRsh;}
			set {this._cvsRsh = StringUtils.ConvertEmptyToNull(value);}
		}

        #endregion Public Instance Properties

		#region Override Task Implementation
		/// <summary>
		/// 
		/// </summary>
		protected override void ExecuteTask () {
			try {
				base.ExecuteTask();
				
			} catch (Exception e) {
				Logger.Error(e);
				throw e;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="process"></param>
		protected override void PrepareProcess (Process process) {
			
			Logger.Debug("number of arguments: " + Arguments.Count);
			if (null == this.Arguments || 0 == this.Arguments.Count) {
				if (IsModuleNeeded) {
					this.Arguments.Add(new Argument("-d" + this.CvsRoot));
				}
				this.AppendGlobalOptions();
				this.Arguments.Add(new Argument(this.CommandName));

				Logger.Debug("commandline args null: " + ((null == this.CommandLineArguments) ? "yes" : "no"));
				if (null == this.CommandLineArguments) {
					this.AppendCommandOptions();
				}

				this.AppendFiles();
				if (this.IsModuleNeeded) {
					this.Arguments.Add(new Argument(this.Module));
				}
			}
			System.Console.WriteLine("Using sharpcvs" + this.UseSharpCvsLib);
			if (this.UseSharpCvsLib) {
				this.ExeName = 
					Path.Combine (System.AppDomain.CurrentDomain.BaseDirectory, CVS_EXE);
			} else {
				this.ExeName = GetCvsFromPath();
			}
			if (!Directory.Exists(this.DestinationDirectory.FullName)) {
				Directory.CreateDirectory(this.DestinationDirectory.FullName);
			}
			base.PrepareProcess(process);
			System.Console.WriteLine("exe name: " + process.StartInfo.FileName);

			if (this.CvsRsh != null ) {
				try {
					process.StartInfo.EnvironmentVariables.Add(CVS_RSH, this.CvsRsh);
				} catch (System.ArgumentException e) {
					Logger.Warn("Possibility cvs_rsh key has already been added.", e);
				}
			}

			process.StartInfo.WorkingDirectory = this.DestinationDirectory.FullName;
			Logger.Debug("working directory: " + process.StartInfo.WorkingDirectory);
			Logger.Debug("executable: " + process.StartInfo.FileName);
			Logger.Debug("arguments: " + process.StartInfo.Arguments);
		}

		#endregion

        #region Private Instance Methods

        private void LogCvsMessage(string message) {
            Log(Level.Debug, LogPrefix + message);
        }

		/*********************** Moved from the Cvs.cs file ********/
		private String GetPassFile () {
			if (this.PassFile == null) {
				string userHome =
					System.Environment.GetEnvironmentVariable(HOME);

				// if the user home is null then try to get the rooted path,
				//  this will be cvs' behavior as well
				if (null == userHome) {
					userHome = 
						Path.GetPathRoot(System.Environment.CurrentDirectory);
				}

				this.PassFile = Path.Combine(userHome, CVS_PASSFILE);
			}
			return this.PassFile;
		}

		private String GetCvsVersion (String fileName) {
			ProcessStartInfo versionStartInfo = 
				new ProcessStartInfo(fileName, "--version");
			versionStartInfo.UseShellExecute = false;
			versionStartInfo.WorkingDirectory = this.DestinationDirectory.FullName;
			versionStartInfo.RedirectStandardOutput = true;
			versionStartInfo.CreateNoWindow = true;

			// Run the process
			Process cvsProcess = new Process();
			cvsProcess.StartInfo = versionStartInfo;
			try {
				cvsProcess.Start();
			} catch (Exception e) {
				Log(Level.Debug, LogPrefix + "Exception getting version.  Exception: " +
					e);
			}

			string versionInfo = cvsProcess.StandardOutput.ReadToEnd();

			return versionInfo;
		}

		private void AppendGlobalOptions () {
			foreach (Option option in this.GlobalOptions) {
				if (!IfDefined || UnlessDefined) {
					// skip option
					continue;
				}

				Logger.Debug ("option.OptionName=[" + option.OptionName + "]");
				Logger.Debug ("option.Value=[" + option.Value + "]");
				switch (option.OptionName) {
					case "cvsroot-prefix":
					case "-D":
					case "temp-dir":
					case "-T":
					case "editor":
					case "-e":
					case "compression":
					case "-z":
					case "variable":
					case "-s": 
						Arguments.Add(new Argument(option.OptionName));
						Arguments.Add(new Argument(option.Value));
						Logger.Debug ("setting option" + option.OptionName + 
							"=[" + option.Value + "]");
						break;
					default:
						Arguments.Add(new Argument(option.OptionName));
						Logger.Debug("setting prune to true.");
						break;
				}
																																																								}
		}

		private void AppendCommandOptions () {
			foreach (Option option in this.CommandOptions) {
				if (!IfDefined || UnlessDefined) {
					// skip option
					continue;
				}

				Logger.Debug ("option.OptionName=[" + option.OptionName + "]");
				Logger.Debug ("option.Value=[" + option.Value + "]");
				switch (option.OptionName) {
					case "sticky-tag":
					case "-r":
					case "override-directory":
					case "-d":
					case "join":
					case "-j":
					case "revision-date":
					case "-D":
					case "rcs-kopt":
					case "-k":
					case "message":
					case "-m":
						Arguments.Add(new Argument(option.OptionName));
						Arguments.Add(new Argument(option.Value));
						Logger.Debug ("setting option" + option.OptionName + 
							"=[" + option.Value + "]");
						break;
					default:
						Arguments.Add(new Argument(option.OptionName));
						Logger.Debug("adding command option: " + option.OptionName);
						break;
				}
			}
		}

		private void AppendFiles () {
			foreach (string pathname in this.CvsFileSet.FileNames) {
				Arguments.Add(new Argument(pathname));
			}
		}

		private bool IsModuleNeeded {
			get {
				if ("checkout".Equals(this.CommandName)) {
					return true;
				} else {
					return false;
				}
			}
		}

		private String GetCvsFromPath () {
			String fileName = null;

			String path = Environment.GetEnvironmentVariable("PATH");
			String[] pathElements = path.Split(';');
			foreach (String pathElement in pathElements) {
				try {
					String[] files = Directory.GetFiles(pathElement, "*.exe");
					foreach (String file in files) {
						if (Path.GetFileName(file).ToLower().IndexOf("cvs") >= 0) {
							Log(Level.Debug, LogPrefix + "Using file " + file + 
								"; file.ToLower().IndexOf(\"cvs\") >=0: " + file.ToLower().IndexOf("cvs"));
							fileName = file;
							break;
						}
					}
				} catch (DirectoryNotFoundException) {
					// expected, happens if the path contains an old directory.
					Log(Level.Debug, LogPrefix + "Path does not exist: " + pathElement);
				} catch (ArgumentException) {
					Log(Level.Debug, LogPrefix + "Path does not exist: " + pathElement);
				}
				if (null != fileName) {
					break;
				}
			}

			if (null == fileName) {
				throw new BuildException ("Cvs binary not specified.");
			}
			return fileName;
		}

        #endregion Private Instance Methods
    }
}