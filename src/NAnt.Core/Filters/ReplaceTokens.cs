// NAnt - A .NET build tool
// Copyright (C) 2001 Gerry Shaw
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
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

using NAnt.Core;
using NAnt.Core.Attributes;
using NAnt.Core.Types;

namespace NAnt.Core.Filters {
    /// <summary>
    /// Replaces tokens in the original input with user-supplied values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This filter replaces all token surrounded by a beginning and ending
    /// token. The default beginning and ending tokens both default to '@'. The 
    /// optional <see cref="BeginToken" /> and <see cref="EndToken" /> attributes
    /// can be specified to change either token.
    /// </para>
    /// <para>
    /// Tokens are specified by using the <see cref="Token" /> element. It is 
    /// possoble to specify from 1 to n tokens and replacement values. Values can 
    /// be any valid NAnt expression 
    /// </para>
    /// <para>
    /// Filters are intended to be used as a element of a <see cref="FilterChain"/>.
    /// </para>
    /// </remarks>
    /// <example>
    ///   <para>
    ///   Replace all occurrency of the string @DATE@ with the value of property
    ///   "TODAY".
    ///   </para>
    ///   <code>
    ///     <![CDATA[
    /// <replacetokens>
    ///     <token key="DATE" value="${TODAY}" />
    /// </replacetokens>
    ///     ]]>
    ///   </code>
    /// </example>
    /// <example>
    ///   <para>
    ///   Replace all occurrency of the string &lt;DATE&gt; with the value of 
    ///   property "TODAY".
    ///   </para>
    ///   <code>
    ///     <![CDATA[
    /// <replacetokens begintoken="&lt;" endtoken="&gt;">
    ///     <token key="DATE" value="${TODAY}" />
    /// </replacetokens>
    ///     ]]>
    ///   </code>
    /// </example>
    [ElementName("replacetokens")] 
    public class ReplaceTokens : Filter {
        /// <summary>
        /// Delegate for Read and Peek. Allows the same implementation
        /// to be used for both methods.
        /// </summary>
        delegate int AcquireCharDelegate();

        #region Private Instance Fields

        private char _beginToken = '@';
        private char _endToken = '@';
        private Token[] _tokens;
        private StringDictionary _tokenValues = new StringDictionary();
        private StringBuilder _tokenString;
        private int _maxTokenLength;
        private string _outputBuffer;
        private bool _endStreamAfterBuffer;
        private int _bufferPosition = 0;
        private bool _unknownToken = true;
        private bool _tokenNotFound = true;

        //Methods used for Read and Peek
        private AcquireCharDelegate ReadChar = null;
        private AcquireCharDelegate PeekChar = null;

        #endregion Private Instance Fields

        #region Public Instance Properties

        /// <summary>
        /// Marks the beginning of a token. The default is "@".
        /// </summary>
        [TaskAttribute("begintoken")]
        [StringValidator(AllowEmpty=false)]
        public char BeginToken {
            get { return _beginToken; }
            set { _beginToken = value; }
        }

        /// <summary>
        /// Marks the end of a token. The default is "@".
        /// </summary>
        [TaskAttribute("endtoken")]
        [StringValidator(AllowEmpty=false)]
        public char EndToken {
            get { return _endToken; }
            set { _endToken = value; }
        }

        /// <summary>
        /// Tokens and replacement values.
        /// </summary>
        [BuildElementArray("token")]
        public Token[] Tokens {
            get { return _tokens; }
            set { _tokens = value; }
        }

        #endregion Public Instance Properties

        #region Override implementation of ChainableReader

        /// <summary>
        /// Construct that allows this filter to be chained to the one
        /// in the parameter chainedReader.
        /// </summary>
        /// <param name="chainedReader">Filter that the filter will be chained to</param>
        public override void Chain(ChainableReader chainedReader) {
            base.Chain(chainedReader);
            ReadChar = new AcquireCharDelegate(base.Read);
            PeekChar = new AcquireCharDelegate(base.Peek);
        }

        /// <summary>
        /// Reads the next character applying the filter logic.
        /// </summary>
        /// <returns>Char as an int or -1 if at the end of the stream</returns>
        public override int Read() {
            return GetNextCharacter(ReadChar);
        }

        /// <summary>
        /// Reads the next character applying the filter logic without
        /// advancing the current position in the stream.
        ///
        /// Peek currently is not supported.
        /// </summary>
        /// <returns>
        /// Char as an int or -1 if at the end of the stream.
        /// </returns>
        public override int Peek() {
            //Need to maintain seperate state for Read and Peek for this to work
            throw new ApplicationException("Peek currently is not supported.");
            //return GetNextCharacter(PeekChar);
        }

        #endregion Override implementation of ChainableReader

        #region Override implementation of Element

        /// <summary>
        /// Initialize the filter by setting its parameters.
        /// </summary>
        protected override void InitializeElement(XmlNode elementNode) {
            foreach (Token token in Tokens) {
                if (token.IfDefined && !token.UnlessDefined) {
                    _tokenValues.Add(token.Key, token.Value);

                    // track max character length
                    if (token.Key.Length > _maxTokenLength) {
                        _maxTokenLength = token.Key.Length;
                    }
                }
            }

            if (_tokenValues.Count == 0) {
                throw new BuildException("One or more tokens and replacement values should be specified.", Location);
            }

            // create a string builder to use for a buffer while searching for tokens.
            _tokenString = new StringBuilder(_maxTokenLength + 1, _maxTokenLength + 1);
        }

        #endregion Override implementation of Element

        #region Private Instance Methods

        /// <summary>
        /// Finds a token give that we are positioned at a beginning token character.  Either a
        /// token replacement is returned or the characters that were read looking for the token.
        /// </summary>
        /// <param name="tokenNotFound">A token was not found</param>
        /// <param name="unknownToken">A token was found by there is no replacement</param>
        /// <param name="streamEnded">The stream ended while looking for the token</param>
        /// <returns>Either the replacement token or the characters that were read looking for the token</returns>
        private string FindTokenContents( out bool tokenNotFound, out bool unknownToken, out bool streamEnded) {
            int charactersScanned = 0;
            char currentChar = _beginToken;
            bool tokenFound = false;
            tokenNotFound = false;
            streamEnded = false;
            unknownToken = true;

            //Reset token string
            _tokenString.Length = 0;

            //Only peak within the limits of the largest token
            while ((charactersScanned <= _maxTokenLength)) {
                charactersScanned++;

                //Read a character
                int streamChar = base.Read();
                currentChar = (char) streamChar;

                //Check for end of stream
                if (streamChar == -1) {
                    //Two adjacent tokens were found
                    tokenNotFound = true;
                    unknownToken = true;
                    streamEnded = true;
                    return _tokenString.ToString();
                }

                if ((currentChar == _endToken)) {
                    tokenFound = true;
                    break;
                } else if ((currentChar == _beginToken) && (_endToken != _beginToken)) {
                    //Only happens if the beginning and ending tokens are not the same
                    //Add end char and break
                    tokenNotFound = true;
                    unknownToken = true;
                    _tokenString.Append((char)currentChar);
                    return _tokenString.ToString();
                } else {
                    //Add possiable token contents to the buffer
                    _tokenString.Append((char)currentChar);
                }
            }

            //Token found and length greater than 0
            if ((tokenFound)) {
                string replacementValue = null;

                //Look up token if not empty
                if (_tokenString.Length != 0) {
                    //Token found so look it up
                    string contentsRead = _tokenString.ToString();
                    replacementValue = _tokenValues[contentsRead];
                } else {
                    //Two adjacent tokens were found
                    tokenNotFound = true;
                    unknownToken = true;

                    return new string(currentChar, 1);
                }


                //Did we find a replacement value for the token?
                if (replacementValue != null) {
                    //This was a token we can handle
                    tokenNotFound = false;
                    unknownToken = false;

                    //Return the replacment value to output
                    return replacementValue;
                } else //We don't know about the token
                {
                    //The token was not in the list so just output it but add then ending
                    //token character back.
                    tokenNotFound = true;
                    unknownToken = true;
                    return _tokenString.Append(currentChar).ToString();
                }
            } else {  //Read max number of characters
                
                //return string to output in future reads
                tokenNotFound = true;
                unknownToken = false;

                return _tokenString.ToString();
            }

        }

        /// <summary>
        /// Returns the next character in the stream replacing the specified character. Using the
        /// <see cref="AcquireCharDelegate"/> allows for the same implementation for Read and Peek
        /// </summary>
        /// <param name="AcquireChar">Delegate to acquire the next character. (Read/Peek)</param>
        /// <returns>Char as an int or -1 if at the end of the stream</returns>
        private int GetNextCharacter(AcquireCharDelegate AcquireChar) {
            int ch;

            //Either read the next character or if there is a buffer output the next character
            if (_outputBuffer == null) {
                ch = base.Read();
            } else {
                //Characters left in the buffer?
                if (_bufferPosition < _outputBuffer.Length) {

                    //If this is the last character of a token string that is unknown
                    //process the charactor again since it might be the beginning of another token.
                    if ((_tokenNotFound == true) && (_unknownToken == true) && (_bufferPosition == _outputBuffer.Length - 1)) {
                        //Process token end char again. It could be the same as token begin.
                        ch = _outputBuffer[_outputBuffer.Length - 1];
                        _bufferPosition++;
                    } else {
                        //Pass along buffer character
                        return _outputBuffer[_bufferPosition++];
                    }
                } else  {//End of buffer

                    //Reset buff and get next char
                    _outputBuffer = null;
                    _bufferPosition = 0;

                    //Reaad the next character or end the stream the end of the stream
                    //was encountered while reading the buffer.
                    if (!_endStreamAfterBuffer) {
                        ch = ReadChar();
                    } else {
                        return -1;
                    }
                }
            }

            //Process beginning token
            if (ch == _beginToken) {
                //Look for a token after _beginToken and return either the replacement token
                //or the charactors that were read.
                _outputBuffer = FindTokenContents(out _tokenNotFound, out _unknownToken, out _endStreamAfterBuffer);

                //A token was not found so _beginToken needs to be accounted for.
                if (_tokenNotFound) {
                    _bufferPosition = 0;
                    return _beginToken;
                } else {
                    //Output first character of buffer
                    _bufferPosition = 1;
                    return _outputBuffer[0];
                }
            } else {
                //This was not a beginning token so just pass it through
                return ch;
            }
        }

        #endregion Private Instance Methods
    }
}

