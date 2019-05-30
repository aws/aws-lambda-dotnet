#pragma warning disable 1587
#region Header
///
/// JsonReader.cs
///   Stream-like access to JSON text.
///
/// The authors disclaim copyright to this source code. For more details, see
/// the COPYING file included with this distribution.
///
#endregion


using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;


namespace ThirdParty.Json.LitJson
{
    internal enum JsonToken
    {
        None,

        ObjectStart,
        PropertyName,
        ObjectEnd,

        ArrayStart,
        ArrayEnd,

        Int,
        UInt,
        Long,
        ULong,
        Double,

        String,

        Boolean,
        Null
    }


    internal class JsonReader
    {
        #region Fields

        private Stack<JsonToken> depth = new Stack<JsonToken>();
        private int           current_input;
        private int           current_symbol;
        private bool          end_of_json;
        private bool          end_of_input;
        private Lexer         lexer;
        private bool          parser_in_string;
        private bool          parser_return;
        private bool          read_started;
        private TextReader    reader;
        private bool          reader_is_owned;
        private object        token_value;
        private JsonToken     token;
        #endregion


        #region Public Properties
        public bool AllowComments {
            get { return lexer.AllowComments; }
            set { lexer.AllowComments = value; }
        }

        public bool AllowSingleQuotedStrings {
            get { return lexer.AllowSingleQuotedStrings; }
            set { lexer.AllowSingleQuotedStrings = value; }
        }

        public bool EndOfInput {
            get { return end_of_input; }
        }

        public bool EndOfJson {
            get { return end_of_json; }
        }

        public JsonToken Token {
            get { return token; }
        }

        public object Value {
            get { return token_value; }
        }
        #endregion


        #region Constructors

        public JsonReader (string json_text) :
            this (new StringReader (json_text), true)
        {
        }

        public JsonReader (TextReader reader) :
            this (reader, false)
        {
        }

        private JsonReader (TextReader reader, bool owned)
        {
            if (reader == null)
                throw new ArgumentNullException ("reader");

            parser_in_string = false;
            parser_return = false;

            read_started = false;

            lexer = new Lexer (reader);

            end_of_input = false;
            end_of_json  = false;

            this.reader = reader;
            reader_is_owned = owned;
        }
        #endregion



        #region Private Methods
        private void ProcessNumber (string number)
        {
            if (number.IndexOf ('.') != -1 ||
                number.IndexOf ('e') != -1 ||
                number.IndexOf ('E') != -1) {

                double n_double;
                if (Double.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out n_double))
                {
                    token = JsonToken.Double;
                    token_value = n_double;

                    return;
                }
            }

            int n_int32;
            if (Int32.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out n_int32))
            {
                token = JsonToken.Int;
                token_value = n_int32;

                return;
            }

            uint n_uint32;
            if (UInt32.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out n_uint32))
            {
                token = JsonToken.UInt;
                token_value = n_uint32;

                return;
            }

            long n_int64;
            if (Int64.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out n_int64))
            {
                token = JsonToken.Long;
                token_value = n_int64;

                return;
            }

            ulong n_uint64;
            if (UInt64.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out n_uint64))
            {
                token = JsonToken.ULong;
                token_value = n_uint64;

                return;
            }

            // Shouldn't happen, but just in case, return something
            token = JsonToken.ULong;
            token_value = default(ulong);
        }

        private void ProcessSymbol ()
        {
            if (current_symbol == '[')  {
                token = JsonToken.ArrayStart;
                parser_return = true;

            } else if (current_symbol == ']')  {
                token = JsonToken.ArrayEnd;
                parser_return = true;

            } else if (current_symbol == '{')  {
                token = JsonToken.ObjectStart;
                parser_return = true;

            } else if (current_symbol == '}')  {
                token = JsonToken.ObjectEnd;
                parser_return = true;

            } else if (current_symbol == '"')  {
                if (parser_in_string) {
                    parser_in_string = false;

                    parser_return = true;

                } else {
                    if (token == JsonToken.None)
                        token = JsonToken.String;

                    parser_in_string = true;
                }

            } else if (current_symbol == (int) ParserToken.CharSeq) {
                token_value = lexer.StringValue;

            } else if (current_symbol == (int) ParserToken.False)  {
                token = JsonToken.Boolean;
                token_value = false;
                parser_return = true;

            } else if (current_symbol == (int) ParserToken.Null)  {
                token = JsonToken.Null;
                parser_return = true;

            } else if (current_symbol == (int) ParserToken.Number)  {
                ProcessNumber (lexer.StringValue);

                parser_return = true;

            } else if (current_symbol == (int) ParserToken.Pair)  {
                token = JsonToken.PropertyName;

            } else if (current_symbol == (int) ParserToken.True)  {
                token = JsonToken.Boolean;
                token_value = true;
                parser_return = true;

            }
        }

        private bool ReadToken ()
        {
            if (end_of_input)
                return false;

            lexer.NextToken ();

            if (lexer.EndOfInput) {
                Close ();

                return false;
            }

            current_input = lexer.Token;

            return true;
        }
        #endregion


        public void Close ()
        {
            if (end_of_input)
                return;

            end_of_input = true;
            end_of_json  = true;

            reader = null;
        }
        
        public bool Read()
        {
            if (end_of_input)
                return false;

            if (end_of_json)
            {
                end_of_json = false;
            }

            token = JsonToken.None;
            parser_in_string = false;
            parser_return = false;

            // Check if the first read call. If so then do an extra ReadToken because Read assumes that the previous
            // call to Read has already called ReadToken.
            if (!read_started)
            {
                read_started = true;

                if (!ReadToken())
                    return false;
            }

            do
            {
                current_symbol = current_input;
                ProcessSymbol();
                if (parser_return)
                {
                    if (this.token == JsonToken.ObjectStart || this.token == JsonToken.ArrayStart)
                    {
                        depth.Push(this.token);
                    }
                    else if (this.token == JsonToken.ObjectEnd || this.token == JsonToken.ArrayEnd)
                    {
                        // Clear out property name if is on top. This could happen if the value for the property was null.
                        if (depth.Peek() == JsonToken.PropertyName)
                            depth.Pop();

                        // Pop the opening token for this closing token. Make sure it is of the right type otherwise 
                        // the document is invalid.
                        var opening = depth.Pop();
                        if (this.token == JsonToken.ObjectEnd && opening != JsonToken.ObjectStart)
                            throw new JsonException("Error: Current token is ObjectEnd which does not match the opening " + opening.ToString());
                        if (this.token == JsonToken.ArrayEnd && opening != JsonToken.ArrayStart)
                            throw new JsonException("Error: Current token is ArrayEnd which does not match the opening " + opening.ToString());

                        // If that last element is popped then we reached the end of the JSON object.
                        if (depth.Count == 0)
                        {
                            end_of_json = true;
                        }
                    }
                    // If the top of the stack is an object start and the next read is a string then it must be a property name
                    // to add to the stack.
                    else if (depth.Count > 0 && depth.Peek() != JsonToken.PropertyName &&
                        this.token == JsonToken.String && depth.Peek() == JsonToken.ObjectStart)
                    {
                        this.token = JsonToken.PropertyName;
                        depth.Push(this.token);
                    }

                    if (
                        (this.token == JsonToken.ObjectEnd ||
                        this.token == JsonToken.ArrayEnd ||
                        this.token == JsonToken.String ||
                        this.token == JsonToken.Boolean ||
                        this.token == JsonToken.Double ||
                        this.token == JsonToken.Int ||
                        this.token == JsonToken.UInt ||
                        this.token == JsonToken.Long ||
                        this.token == JsonToken.ULong ||
                        this.token == JsonToken.Null ||
                        this.token == JsonToken.String
                        ))
                    {
                        // If we found a value but we are not in an array or object then the document is an invalid json document.
                        if (depth.Count == 0)
                        {
                            if (this.token != JsonToken.ArrayEnd && this.token != JsonToken.ObjectEnd)
                            {
                                throw new JsonException("Value without enclosing object or array");
                            }
                        }
                        // The valud of the property has been processed so pop the property name from the stack.
                        else if (depth.Peek() == JsonToken.PropertyName)
                        {
                            depth.Pop();
                        }
                    }

                    // Read the next token that will be processed the next time the Read method is called.
                    // This is done ahead of the next read so we can detect if we are at the end of the json document.
                    // Otherwise EndOfInput would not return true until an attempt to read was made.
                    if (!ReadToken() && depth.Count != 0)
                        throw new JsonException("Incomplete JSON Document");
                    return true;
                }
            } while (ReadToken());

            // If we reached the end of the document but there is still elements left in the depth stack then
            // the document is invalid JSON.
            if (depth.Count != 0)
                throw new JsonException("Incomplete JSON Document");

            end_of_input = true;
            return false;
        }

    }
}
