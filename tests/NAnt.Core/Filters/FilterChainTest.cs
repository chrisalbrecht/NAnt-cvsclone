// NAnt - A .NET build tool
// Copyright (C) 2002-2003 Scott Hernandez
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

using System;
using System.Text;
using NAnt.Core.Filters;
using NUnit.Framework;

namespace Tests.NAnt.Core.Filters {
    /// <summary>
    /// Tests the FilterChain classes.
    /// </summary>
    [TestFixture]
    public class FilterChainTest : FilterTestBase {
        [Test]
        public void NoFilerTest () {
            base.TestFilter("", " ", " ");
        }

        [Test]
        public void NoFilterEmptyFileTest () {
            base.TestFilter(@"", "", "");
        }

        [Test]
        [ExpectedException(typeof(TestBuildException))]
        public void UnknownFilterTest () {
            base.TestFilter(@"<blah />", " ", " ");
        }

        [Test]
        public void FilterOrderTest1a () {
            base.TestFilter(@"<replacecharacter from=""^"" to=""$"" />
                    <expandproperties />", "^{'la' + 'la'}", "lala");
        }

        [Test]
        public void FilterOrderTest1b () {
            base.TestFilter(@"<expandproperties />
                    <replacecharacter from=""^"" to=""$"" />", "^{'la' + 'la'}", "${'la' + 'la'}");
        }
    }
}
