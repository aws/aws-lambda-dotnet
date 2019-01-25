#pragma warning disable 1587
#region Header
///
/// JsonData.cs
///   Generic type to hold JSON data (objects, arrays, and so on). This is
///   the default type returned by JsonMapper.ToObject().
///
/// The authors disclaim copyright to this source code. For more details, see
/// the COPYING file included with this distribution.
///
#endregion


using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;

namespace ThirdParty.Json.LitJson
{
    internal class JsonData : IJsonWrapper, IEquatable<JsonData>
    {
        #region Fields
        private IList<JsonData>               inst_array;
        private bool                          inst_boolean;
        private double                        inst_double;

        // number datatype that holds int, uint, long, and ulong values.
        // type field keeps track of the actual type of the value
        // and casted before returning the value to the client code.
        private ulong                         inst_number;

        private IDictionary<string, JsonData> inst_object;
        private string                        inst_string;
        private string                        json;
        private JsonType                      type;

        // Used to implement the IOrderedDictionary interface
        private IList<KeyValuePair<string, JsonData>> object_list;
        #endregion


        #region Properties
        public int Count {
            get { return EnsureCollection ().Count; }
        }

        public bool IsArray {
            get { return type == JsonType.Array; }
        }

        public bool IsBoolean {
            get { return type == JsonType.Boolean; }
        }

        public bool IsDouble {
            get { return type == JsonType.Double; }
        }

        public bool IsInt {
            get { return type == JsonType.Int; }
        }

        public bool IsUInt {
            get { return type == JsonType.UInt; }
        }

        public bool IsLong {
            get { return type == JsonType.Long; }
        }

        public bool IsULong {
            get { return type == JsonType.ULong; }
        }

        public bool IsObject {
            get { return type == JsonType.Object; }
        }

        public bool IsString {
            get { return type == JsonType.String; }
        }
        #endregion


        #region ICollection Properties
        int ICollection.Count {
            get {
                return Count;
            }
        }

        bool ICollection.IsSynchronized {
            get {
                return EnsureCollection ().IsSynchronized;
            }
        }

        object ICollection.SyncRoot {
            get {
                return EnsureCollection ().SyncRoot;
            }
        }
        #endregion


        #region IDictionary Properties
        bool IDictionary.IsFixedSize {
            get {
                return EnsureDictionary ().IsFixedSize;
            }
        }

        bool IDictionary.IsReadOnly {
            get {
                return EnsureDictionary ().IsReadOnly;
            }
        }

        ICollection IDictionary.Keys {
            get {
                EnsureDictionary ();
                IList<string> keys = new List<string> ();

                foreach (KeyValuePair<string, JsonData> entry in
                         object_list) {
                    keys.Add (entry.Key);
                }

                return (ICollection) keys;
            }
        }

        ICollection IDictionary.Values {
            get {
                EnsureDictionary ();
                IList<JsonData> values = new List<JsonData> ();

                foreach (KeyValuePair<string, JsonData> entry in
                         object_list) {
                    values.Add (entry.Value);
                }

                return (ICollection) values;
            }
        }
        #endregion



        #region IJsonWrapper Properties
        bool IJsonWrapper.IsArray {
            get { return IsArray; }
        }

        bool IJsonWrapper.IsBoolean {
            get { return IsBoolean; }
        }

        bool IJsonWrapper.IsDouble {
            get { return IsDouble; }
        }

        bool IJsonWrapper.IsInt {
            get { return IsInt; }
        }

        bool IJsonWrapper.IsLong {
            get { return IsLong; }
        }

        bool IJsonWrapper.IsObject {
            get { return IsObject; }
        }

        bool IJsonWrapper.IsString {
            get { return IsString; }
        }
        #endregion


        #region IList Properties
        bool IList.IsFixedSize {
            get {
                return EnsureList ().IsFixedSize;
            }
        }

        bool IList.IsReadOnly {
            get {
                return EnsureList ().IsReadOnly;
            }
        }
        #endregion


        #region IDictionary Indexer
        object IDictionary.this[object key] {
            get {
                return EnsureDictionary ()[key];
            }

            set {
                if (! (key is String))
                    throw new ArgumentException (
                        "The key has to be a string");

                JsonData data = ToJsonData (value);

                this[(string) key] = data;
            }
        }
        #endregion


        #region IOrderedDictionary Indexer
        object IOrderedDictionary.this[int idx] {
            get {
                EnsureDictionary ();
                return object_list[idx].Value;
            }

            set {
                EnsureDictionary ();
                JsonData data = ToJsonData (value);

                KeyValuePair<string, JsonData> old_entry = object_list[idx];

                inst_object[old_entry.Key] = data;

                KeyValuePair<string, JsonData> entry =
                    new KeyValuePair<string, JsonData> (old_entry.Key, data);

                object_list[idx] = entry;
            }
        }
        #endregion


        #region IList Indexer
        object IList.this[int index] {
            get {
                return EnsureList ()[index];
            }

            set {
                EnsureList ();
                JsonData data = ToJsonData (value);

                this[index] = data;
            }
        }
        #endregion


        #region Public Indexers

        public IEnumerable<string> PropertyNames
        {
            get
            {
                EnsureDictionary();
                return inst_object.Keys;
            }
        }

        public JsonData this[string prop_name] {
            get {
                EnsureDictionary ();
                JsonData data = null;
                inst_object.TryGetValue(prop_name, out data);
                return data;
            }

            set {
                EnsureDictionary ();

                KeyValuePair<string, JsonData> entry =
                    new KeyValuePair<string, JsonData> (prop_name, value);

                if (inst_object.ContainsKey (prop_name)) {
                    for (int i = 0; i < object_list.Count; i++) {
                        if (object_list[i].Key == prop_name) {
                            object_list[i] = entry;
                            break;
                        }
                    }
                } else
                    object_list.Add (entry);

                inst_object[prop_name] = value;

                json = null;
            }
        }

        public JsonData this[int index] {
            get {
                EnsureCollection ();

                if (type == JsonType.Array)
                    return inst_array[index];

                return object_list[index].Value;
            }

            set {
                EnsureCollection ();

                if (type == JsonType.Array)
                    inst_array[index] = value;
                else {
                    KeyValuePair<string, JsonData> entry = object_list[index];
                    KeyValuePair<string, JsonData> new_entry =
                        new KeyValuePair<string, JsonData> (entry.Key, value);

                    object_list[index] = new_entry;
                    inst_object[entry.Key] = value;
                }

                json = null;
            }
        }
        #endregion


        #region Constructors
        public JsonData ()
        {
        }

        public JsonData (bool boolean)
        {
            type = JsonType.Boolean;
            inst_boolean = boolean;
        }

        public JsonData (double number)
        {
            type = JsonType.Double;
            inst_double = number;
        }

        public JsonData (int number)
        {
            type = JsonType.Int;
            inst_number = (ulong)number;
        }

        public JsonData(uint number)
        {
            type = JsonType.UInt;
            inst_number = (ulong)number;
        }

        public JsonData (long number)
        {
            type = JsonType.Long;
            inst_number = (ulong)number;
        }

        public JsonData(ulong number)
        {
            type = JsonType.ULong;
            inst_number = number;
        }

        public JsonData (object obj)
        {
            if (obj is Boolean) {
                type = JsonType.Boolean;
                inst_boolean = (bool) obj;
                return;
            }

            if (obj is Double) {
                type = JsonType.Double;
                inst_double = (double) obj;
                return;
            }

            if (obj is Int32) {
                type = JsonType.Int;
                inst_number = (ulong)obj;
                return;
            }

            if (obj is UInt32)
            {
                type = JsonType.UInt;
                inst_number = (ulong)obj;
                return;
            }

            if (obj is Int64) {
                type = JsonType.Long;
                inst_number = (ulong)obj;
                return;
            }

            if (obj is UInt64)
            {
                type = JsonType.ULong;
                inst_number = (ulong)obj;
                return;
            }

            if (obj is String) {
                type = JsonType.String;
                inst_string = (string) obj;
                return;
            }

            throw new ArgumentException (
                "Unable to wrap the given object with JsonData");
        }

        public JsonData (string str)
        {
            type = JsonType.String;
            inst_string = str;
        }
        #endregion


        #region Implicit Conversions
        public static implicit operator JsonData (Boolean data)
        {
            return new JsonData (data);
        }

        public static implicit operator JsonData (Double data)
        {
            return new JsonData (data);
        }

        public static implicit operator JsonData (Int32 data)
        {
            return new JsonData (data);
        }

        public static implicit operator JsonData (Int64 data)
        {
            return new JsonData (data);
        }

        public static implicit operator JsonData (String data)
        {
            return new JsonData (data);
        }
        #endregion


        #region Explicit Conversions
        public static explicit operator Boolean (JsonData data)
        {
            if (data.type != JsonType.Boolean)
                throw new InvalidCastException (
                    "Instance of JsonData doesn't hold a double");

            return data.inst_boolean;
        }

        public static explicit operator Double (JsonData data)
        {
            if (data.type != JsonType.Double)
                throw new InvalidCastException (
                    "Instance of JsonData doesn't hold a double");

            return data.inst_double;
        }

        public static explicit operator Int32 (JsonData data)
        {
            if (data.type != JsonType.Int)
            {
                throw new InvalidCastException (
                    "Instance of JsonData doesn't hold an int");
            }
            
            return unchecked((int)data.inst_number);
        }

        public static explicit operator UInt32(JsonData data)
        {
            if (data.type != JsonType.UInt)
            {
                throw new InvalidCastException(
                    "Instance of JsonData doesn't hold an int");
            }

            return unchecked((uint)data.inst_number);
        }

        public static explicit operator Int64 (JsonData data)
        {
            if (data.type != JsonType.Int && data.type != JsonType.Long)
            {
                throw new InvalidCastException (
                    "Instance of JsonData doesn't hold an long");
            }

            return unchecked((long)data.inst_number);
        }

        public static explicit operator UInt64(JsonData data)
        {
            if (data.type != JsonType.UInt && data.type != JsonType.ULong)
            {
                throw new InvalidCastException(
                    "Instance of JsonData doesn't hold an long");
            }

            return (ulong)data.inst_number;
        }

        public static explicit operator String (JsonData data)
        {
            if (data.type != JsonType.String)
                throw new InvalidCastException (
                    "Instance of JsonData doesn't hold a string");

            return data.inst_string;
        }
        #endregion


        #region ICollection Methods
        void ICollection.CopyTo (Array array, int index)
        {
            EnsureCollection ().CopyTo (array, index);
        }
        #endregion


        #region IDictionary Methods
        void IDictionary.Add (object key, object value)
        {
            JsonData data = ToJsonData (value);

            EnsureDictionary ().Add (key, data);

            KeyValuePair<string, JsonData> entry =
                new KeyValuePair<string, JsonData> ((string) key, data);
            object_list.Add (entry);

            json = null;
        }

        void IDictionary.Clear ()
        {
            EnsureDictionary ().Clear ();
            object_list.Clear ();
            json = null;
        }

        bool IDictionary.Contains (object key)
        {
            return EnsureDictionary ().Contains (key);
        }

        IDictionaryEnumerator IDictionary.GetEnumerator ()
        {
            return ((IOrderedDictionary) this).GetEnumerator ();
        }

        void IDictionary.Remove (object key)
        {
            EnsureDictionary ().Remove (key);

            for (int i = 0; i < object_list.Count; i++) {
                if (object_list[i].Key == (string) key) {
                    object_list.RemoveAt (i);
                    break;
                }
            }

            json = null;
        }
        #endregion


        #region IEnumerable Methods
        IEnumerator IEnumerable.GetEnumerator ()
        {
            return EnsureCollection ().GetEnumerator ();
        }
        #endregion


        #region IJsonWrapper Methods
        bool IJsonWrapper.GetBoolean ()
        {
            if (type != JsonType.Boolean)
                throw new InvalidOperationException (
                    "JsonData instance doesn't hold a boolean");

            return inst_boolean;
        }

        double IJsonWrapper.GetDouble ()
        {
            if (type != JsonType.Double)
                throw new InvalidOperationException (
                    "JsonData instance doesn't hold a double");

            return inst_double;
        }

        int IJsonWrapper.GetInt ()
        {
            if (type != JsonType.Int)
                throw new InvalidOperationException (
                    "JsonData instance doesn't hold an int");

            return unchecked((int)inst_number);
        }

        uint IJsonWrapper.GetUInt()
        {
            if (type != JsonType.UInt)
                throw new InvalidOperationException(
                    "JsonData instance doesn't hold an int");

            return unchecked((uint)inst_number);
        }

        long IJsonWrapper.GetLong ()
        {
            if (type != JsonType.Long)
                throw new InvalidOperationException (
                    "JsonData instance doesn't hold a long");

            return unchecked((long)inst_number);
        }

        ulong IJsonWrapper.GetULong()
        {
            if (type != JsonType.ULong)
                throw new InvalidOperationException(
                    "JsonData instance doesn't hold a long");

            return inst_number;
        }

        string IJsonWrapper.GetString ()
        {
            if (type != JsonType.String)
                throw new InvalidOperationException (
                    "JsonData instance doesn't hold a string");

            return inst_string;
        }

        void IJsonWrapper.SetBoolean (bool val)
        {
            type = JsonType.Boolean;
            inst_boolean = val;
            json = null;
        }

        void IJsonWrapper.SetDouble (double val)
        {
            type = JsonType.Double;
            inst_double = val;
            json = null;
        }

        void IJsonWrapper.SetInt (int val)
        {
            type = JsonType.Int;
            inst_number = unchecked((ulong)val);
            json = null;
        }

        void IJsonWrapper.SetUInt(uint val)
        {
            type = JsonType.UInt;
            inst_number = unchecked((ulong)val);
            json = null;
        }

        void IJsonWrapper.SetLong (long val)
        {
            type = JsonType.Long;
            inst_number = unchecked((ulong)val);
            json = null;
        }

        void IJsonWrapper.SetULong(ulong val)
        {
            type = JsonType.ULong;
            inst_number = val;
            json = null;
        }

        void IJsonWrapper.SetString (string val)
        {
            type = JsonType.String;
            inst_string = val;
            json = null;
        }

        string IJsonWrapper.ToJson ()
        {
            return ToJson ();
        }

        void IJsonWrapper.ToJson (JsonWriter writer)
        {
            ToJson (writer);
        }
        #endregion


        #region IList Methods
        int IList.Add (object value)
        {
            return Add (value);
        }

        void IList.Clear ()
        {
            EnsureList ().Clear ();
            json = null;
        }

        bool IList.Contains (object value)
        {
            return EnsureList ().Contains (value);
        }

        int IList.IndexOf (object value)
        {
            return EnsureList ().IndexOf (value);
        }

        void IList.Insert (int index, object value)
        {
            EnsureList ().Insert (index, value);
            json = null;
        }

        void IList.Remove (object value)
        {
            EnsureList ().Remove (value);
            json = null;
        }

        void IList.RemoveAt (int index)
        {
            EnsureList ().RemoveAt (index);
            json = null;
        }
        #endregion


        #region IOrderedDictionary Methods
        IDictionaryEnumerator IOrderedDictionary.GetEnumerator ()
        {
            EnsureDictionary ();

            return new OrderedDictionaryEnumerator (
                object_list.GetEnumerator ());
        }

        void IOrderedDictionary.Insert (int idx, object key, object value)
        {
            string property = (string) key;
            JsonData data  = ToJsonData (value);

            this[property] = data;

            KeyValuePair<string, JsonData> entry =
                new KeyValuePair<string, JsonData> (property, data);

            object_list.Insert (idx, entry);
        }

        void IOrderedDictionary.RemoveAt (int idx)
        {
            EnsureDictionary ();

            inst_object.Remove (object_list[idx].Key);
            object_list.RemoveAt (idx);
        }
        #endregion


        #region Private Methods
        private ICollection EnsureCollection ()
        {
            if (type == JsonType.Array)
                return (ICollection) inst_array;

            if (type == JsonType.Object)
                return (ICollection) inst_object;

            throw new InvalidOperationException (
                "The JsonData instance has to be initialized first");
        }

        private IDictionary EnsureDictionary ()
        {
            if (type == JsonType.Object)
                return (IDictionary) inst_object;

            if (type != JsonType.None)
                throw new InvalidOperationException (
                    "Instance of JsonData is not a dictionary");

            type = JsonType.Object;
            inst_object = new Dictionary<string, JsonData> ();
            object_list = new List<KeyValuePair<string, JsonData>> ();

            return (IDictionary) inst_object;
        }

        private IList EnsureList ()
        {
            if (type == JsonType.Array)
                return (IList) inst_array;

            if (type != JsonType.None)
                throw new InvalidOperationException (
                    "Instance of JsonData is not a list");

            type = JsonType.Array;
            inst_array = new List<JsonData> ();

            return (IList) inst_array;
        }

        private JsonData ToJsonData (object obj)
        {
            if (obj == null)
                return null;

            if (obj is JsonData)
                return (JsonData) obj;

            return new JsonData (obj);
        }

        private static void WriteJson (IJsonWrapper obj, JsonWriter writer)
        {
            if (obj == null) {
                writer.Write(null);
                return;
            }

            if (obj.IsString) {
                writer.Write (obj.GetString ());
                return;
            }

            if (obj.IsBoolean) {
                writer.Write (obj.GetBoolean ());
                return;
            }

            if (obj.IsDouble) {
                writer.Write (obj.GetDouble ());
                return;
            }

            if (obj.IsInt) {
                writer.Write (obj.GetInt ());
                return;
            }

            if (obj.IsUInt)
            {
                writer.Write(obj.GetUInt());
                return;
            }

            if (obj.IsLong) {
                writer.Write (obj.GetLong ());
                return;
            }

            if (obj.IsULong)
            {
                writer.Write(obj.GetULong());
                return;
            }

            if (obj.IsArray) {
                writer.WriteArrayStart ();
                foreach (object elem in (IList) obj)
                    WriteJson ((JsonData) elem, writer);
                writer.WriteArrayEnd ();

                return;
            }

            if (obj.IsObject) {
                writer.WriteObjectStart ();

                foreach (DictionaryEntry entry in ((IDictionary) obj)) {
                    writer.WritePropertyName ((string) entry.Key);
                    WriteJson ((JsonData) entry.Value, writer);
                }
                writer.WriteObjectEnd ();

                return;
            }
        }
        #endregion


        public int Add (object value)
        {
            JsonData data = ToJsonData (value);

            json = null;

            return EnsureList ().Add (data);
        }

        public void Clear ()
        {
            if (IsObject) {
                ((IDictionary) this).Clear ();
                return;
            }

            if (IsArray) {
                ((IList) this).Clear ();
                return;
            }
        }

        public bool Equals (JsonData x)
        {
            if (x == null)
                return false;

            if (x.type != this.type)
            {
                bool thisIsSigned = (this.type == JsonType.Int || this.type == JsonType.Long);
                bool thisIsUnsigned = (this.type == JsonType.UInt || this.type == JsonType.ULong);
                bool xIsSigned = (x.type == JsonType.Int || x.type == JsonType.Long);
                bool xIsUnsigned = (x.type == JsonType.UInt || x.type == JsonType.ULong); 
               
                if (thisIsSigned == xIsSigned || thisIsUnsigned == xIsUnsigned)
                {
                    // only allow types between signed numbers and between unsigned numbers to be actually compared
                }
                else
                {
                    return false;
                }
            }

            switch (this.type) {
            case JsonType.None:
                return true;

            case JsonType.Object:
                return this.inst_object.Equals (x.inst_object);

            case JsonType.Array:
                return this.inst_array.Equals (x.inst_array);

            case JsonType.String:
                return this.inst_string.Equals (x.inst_string);

            case JsonType.Int:
            case JsonType.UInt:
            case JsonType.Long:
            case JsonType.ULong:
                return this.inst_number.Equals (x.inst_number);

            case JsonType.Double:
                return this.inst_double.Equals (x.inst_double);

            case JsonType.Boolean:
                return this.inst_boolean.Equals (x.inst_boolean);
            }

            return false;
        }

        public JsonType GetJsonType ()
        {
            return type;
        }

        public void SetJsonType (JsonType type)
        {
            if (this.type == type)
                return;

            switch (type) {
            case JsonType.None:
                break;

            case JsonType.Object:
                inst_object = new Dictionary<string, JsonData> ();
                object_list = new List<KeyValuePair<string, JsonData>> ();
                break;

            case JsonType.Array:
                inst_array = new List<JsonData> ();
                break;

            case JsonType.String:
                inst_string = default (String);
                break;

            case JsonType.Int:
                inst_number = default (Int32);
                break;
            
            case JsonType.UInt:
                inst_number = default(UInt32);
                break;

            case JsonType.Long:
                inst_number = default (Int64);
                break;
            
            case JsonType.ULong:
                inst_number = default(UInt64);
                break;

            case JsonType.Double:
                inst_double = default (Double);
                break;

            case JsonType.Boolean:
                inst_boolean = default (Boolean);
                break;
            }

            this.type = type;
        }

        public string ToJson ()
        {
            if (json != null)
                return json;

            StringWriter sw = new StringWriter ();
            JsonWriter writer = new JsonWriter (sw);
            writer.Validate = false;

            WriteJson (this, writer);
            json = sw.ToString ();

            return json;
        }

        public void ToJson (JsonWriter writer)
        {
            bool old_validate = writer.Validate;

            writer.Validate = false;

            WriteJson (this, writer);

            writer.Validate = old_validate;
        }

        public override string ToString ()
        {
            switch (type) {
            case JsonType.Array:
                return "JsonData array";

            case JsonType.Boolean:
                return inst_boolean.ToString ();

            case JsonType.Double:
                return inst_double.ToString ();

            case JsonType.Int:
                return unchecked((int)inst_number).ToString();
            case JsonType.UInt:
                return unchecked((uint)inst_number).ToString();
            case JsonType.Long:
                return unchecked((long)inst_number).ToString();
            case JsonType.ULong:
                return inst_number.ToString ();

            case JsonType.Object:
                return "JsonData object";

            case JsonType.String:
                return inst_string;
            }

            return "Uninitialized JsonData";
        }
    }


    internal class OrderedDictionaryEnumerator : IDictionaryEnumerator
    {
        IEnumerator<KeyValuePair<string, JsonData>> list_enumerator;


        public object Current {
            get { return Entry; }
        }

        public DictionaryEntry Entry {
            get {
                KeyValuePair<string, JsonData> curr = list_enumerator.Current;
                return new DictionaryEntry (curr.Key, curr.Value);
            }
        }

        public object Key {
            get { return list_enumerator.Current.Key; }
        }

        public object Value {
            get { return list_enumerator.Current.Value; }
        }


        public OrderedDictionaryEnumerator (
            IEnumerator<KeyValuePair<string, JsonData>> enumerator)
        {
            list_enumerator = enumerator;
        }


        public bool MoveNext ()
        {
            return list_enumerator.MoveNext ();
        }

        public void Reset ()
        {
            list_enumerator.Reset ();
        }
    }
}
