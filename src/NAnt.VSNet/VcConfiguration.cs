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
// Dmitry Jemerov <yole@yole.ru>

using System;
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;

namespace NAnt.VSNet {
    /// <summary>
    /// A single build configuration for a Visual C++ project or for a specific
    /// file in the project.
    /// </summary>
    internal class VcConfiguration {

        internal VcConfiguration(XmlElement elem): this(elem, null) {
        }

        internal VcConfiguration(XmlElement elem, VcConfiguration parent) {
            _parent = parent;
            _name = elem.GetAttribute("Name");
            _outputDir = elem.GetAttribute("OutputDirectory");
            _intermediateDir = elem.GetAttribute("IntermediateDirectory");

            if (String.Compare(elem.GetAttribute("WholeProgramOptimization"), "TRUE", true, CultureInfo.InvariantCulture) == 0) {
                _wholeProgramOptimization = true;
            } 

            _htMacros = CollectionsUtil.CreateCaseInsensitiveHashtable();
            _htMacros ["OutDir"] = _outputDir;
            _rxMacro = new Regex(@"\$\((\w+)\)");

            _htTools = CollectionsUtil.CreateCaseInsensitiveHashtable();

            XmlNodeList tools = elem.GetElementsByTagName("Tool");
            foreach(XmlElement toolElem in tools) {
            string toolName = toolElem.GetAttribute("Name");
            Hashtable htToolSettings = CollectionsUtil.CreateCaseInsensitiveHashtable();
            foreach(XmlAttribute attr in toolElem.Attributes) {                    if (attr.Name != "Name") {                        htToolSettings [attr.Name] = attr.Value;                    }                }                _htTools [toolName] = htToolSettings;            }        }
        #region Internal Instance Properties        internal string Name {            get {                int index = _name.IndexOf("|");                if (index >= 0) {                    return _name.Substring(0, index);
                }
                else {
                    return _name;
                }
            }
        }
        internal string FullName {            get { return _name; }        }
        internal string IntermediateDir {            get { return _intermediateDir; }        }
        internal bool WholeProgramOptimization {            get { return _wholeProgramOptimization; }        }
        #endregion
        #region Internal Instance Methods
        internal string GetToolSetting(string toolName, string settingName) {            Hashtable toolSettings = (Hashtable) _htTools [toolName];            if (toolSettings != null) {                string setting = (string) toolSettings [settingName];                if (setting != null) {                    return ExpandMacros(setting);
                }            }            if (_parent != null) {                return _parent.GetToolSetting(toolName, settingName);            }            return null;        }
        internal string[] GetToolArguments(string toolName, VcArgumentMap argMap) {            ArrayList args = new ArrayList();            Hashtable toolSettings = (Hashtable) _htTools [toolName];            if (toolSettings != null) {                foreach(DictionaryEntry de in toolSettings) {                    string arg = argMap.GetArgument((string) de.Key, ExpandMacros((string) de.Value));                    if (arg != null) {                        args.Add(arg);
                    }                }            }            return (string[]) args.ToArray(typeof(string));        }
        internal string ExpandMacros(string s) {            return _rxMacro.Replace(s, new MatchEvaluator(EvaluateMacro));        }
        private string EvaluateMacro(Match m) {            string macroValue = (string) _htMacros [m.Groups [1].Value];            if (macroValue != null) {                return macroValue;
            }            return m.Value;        }
        #endregion
        #region Private Instance Fields
        private string          _name;        private VcConfiguration _parent;        private Hashtable       _htTools;        private string          _outputDir;        private string          _intermediateDir;        private Hashtable       _htMacros;        private Regex           _rxMacro;        private bool            _wholeProgramOptimization = false;
        #endregion
    }
}
