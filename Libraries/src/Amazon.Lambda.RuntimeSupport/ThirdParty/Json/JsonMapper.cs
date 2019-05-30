#pragma warning disable 1587
#region Header
///
/// JsonMapper.cs
///   JSON to .Net object and object to JSON conversions.
///
/// The authors disclaim copyright to this source code. For more details, see
/// the COPYING file included with this distribution.
///
#endregion


using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

using Amazon.Util;
using Amazon.Util.Internal;

namespace ThirdParty.Json.LitJson
{
    internal struct PropertyMetadata
    {
        public MemberInfo Info;
        public bool       IsField;
        public Type       Type;
    }


    internal struct ArrayMetadata
    {
        private Type element_type;
        private bool is_array;
        private bool is_list;


        public Type ElementType {
            get {
                if (element_type == null)
                    return typeof (JsonData);

                return element_type;
            }

            set { element_type = value; }
        }

        public bool IsArray {
            get { return is_array; }
            set { is_array = value; }
        }

        public bool IsList {
            get { return is_list; }
            set { is_list = value; }
        }
    }


    internal struct ObjectMetadata
    {
        private Type element_type;
        private bool is_dictionary;

        private IDictionary<string, PropertyMetadata> properties;


        public Type ElementType {
            get {
                if (element_type == null)
                    return typeof (JsonData);

                return element_type;
            }

            set { element_type = value; }
        }

        public bool IsDictionary {
            get { return is_dictionary; }
            set { is_dictionary = value; }
        }

        public IDictionary<string, PropertyMetadata> Properties {
            get { return properties; }
            set { properties = value; }
        }
    }


    internal delegate void ExporterFunc    (object obj, JsonWriter writer);
    internal delegate void ExporterFunc<T> (T obj, JsonWriter writer);

    internal delegate object ImporterFunc                (object input);
    internal delegate TValue ImporterFunc<TJson, TValue> (TJson input);

    internal delegate IJsonWrapper WrapperFactory ();


    internal class JsonMapper
    {
        #region Fields
        private static int max_nesting_depth;

        private static IFormatProvider datetime_format;

        private static IDictionary<Type, ExporterFunc> base_exporters_table;
        private static IDictionary<Type, ExporterFunc> custom_exporters_table;

        private static IDictionary<Type,
                IDictionary<Type, ImporterFunc>> base_importers_table;
        private static IDictionary<Type,
                IDictionary<Type, ImporterFunc>> custom_importers_table;

        private static IDictionary<Type, ArrayMetadata> array_metadata;
        private static readonly object array_metadata_lock = new Object ();

        private static IDictionary<Type,
                IDictionary<Type, MethodInfo>> conv_ops;
        private static readonly object conv_ops_lock = new Object ();

        private static IDictionary<Type, ObjectMetadata> object_metadata;
        private static readonly object object_metadata_lock = new Object ();

        private static IDictionary<Type,
                IList<PropertyMetadata>> type_properties;
        private static readonly object type_properties_lock = new Object ();

        private static JsonWriter      static_writer;
        private static readonly object static_writer_lock = new Object ();

        private static readonly HashSet<string> dictionary_properties_to_ignore = new HashSet<string>(StringComparer.Ordinal)
        {
            "Comparer", "Count", "Keys", "Values"
        };
        #endregion


        #region Constructors
        static JsonMapper ()
        {
            max_nesting_depth = 100;

            array_metadata = new Dictionary<Type, ArrayMetadata> ();
            conv_ops = new Dictionary<Type, IDictionary<Type, MethodInfo>> ();
            object_metadata = new Dictionary<Type, ObjectMetadata> ();
            type_properties = new Dictionary<Type,
                            IList<PropertyMetadata>> ();

            static_writer = new JsonWriter ();

            datetime_format = DateTimeFormatInfo.InvariantInfo;

            base_exporters_table   = new Dictionary<Type, ExporterFunc> ();
            custom_exporters_table = new Dictionary<Type, ExporterFunc> ();

            base_importers_table = new Dictionary<Type,
                                 IDictionary<Type, ImporterFunc>> ();
            custom_importers_table = new Dictionary<Type,
                                   IDictionary<Type, ImporterFunc>> ();

            RegisterBaseExporters ();
            RegisterBaseImporters ();
        }
        #endregion


        #region Private Methods
        private static void AddArrayMetadata (Type type)
        {
            if (array_metadata.ContainsKey (type))
                return;

            ArrayMetadata data = new ArrayMetadata ();

            data.IsArray = type.IsArray;

            var typeInfo = TypeFactory.GetTypeInfo(type);
            if (typeInfo.GetInterface("System.Collections.IList") != null)
                data.IsList = true;

            foreach (PropertyInfo p_info in typeInfo.GetProperties())
            {
                if (p_info.Name != "Item")
                    continue;

                ParameterInfo[] parameters = p_info.GetIndexParameters ();

                if (parameters.Length != 1)
                    continue;

                if (parameters[0].ParameterType == typeof (int))
                    data.ElementType = p_info.PropertyType;
            }

            lock (array_metadata_lock) {
                try {
                    array_metadata.Add (type, data);
                } catch (ArgumentException) {
                    return;
                }
            }
        }

        private static void AddObjectMetadata (Type type)
        {
            if (object_metadata.ContainsKey (type))
                return;

            ObjectMetadata data = new ObjectMetadata ();

            var typeInfo = TypeFactory.GetTypeInfo(type);
            if (typeInfo.GetInterface("System.Collections.IDictionary") != null)
                data.IsDictionary = true;

            data.Properties = new Dictionary<string, PropertyMetadata> ();

            foreach (PropertyInfo p_info in typeInfo.GetProperties())
            {
                if (p_info.Name == "Item") {
                    ParameterInfo[] parameters = p_info.GetIndexParameters ();

                    if (parameters.Length != 1)
                        continue;

                    if (parameters[0].ParameterType == typeof (string))
                        data.ElementType = p_info.PropertyType;

                    continue;
                }

                if (data.IsDictionary && dictionary_properties_to_ignore.Contains(p_info.Name))
                    continue;

                PropertyMetadata p_data = new PropertyMetadata ();
                p_data.Info = p_info;
                p_data.Type = p_info.PropertyType;

                data.Properties.Add (p_info.Name, p_data);
            }

            foreach (FieldInfo f_info in typeInfo.GetFields())
            {
                PropertyMetadata p_data = new PropertyMetadata ();
                p_data.Info = f_info;
                p_data.IsField = true;
                p_data.Type = f_info.FieldType;

                data.Properties.Add (f_info.Name, p_data);
            }

            lock (object_metadata_lock) {
                try {
                    object_metadata.Add (type, data);
                } catch (ArgumentException) {
                    return;
                }
            }
        }

        private static void AddTypeProperties (Type type)
        {
            if (type_properties.ContainsKey (type))
                return;
            var typeInfo = TypeFactory.GetTypeInfo(type);
            IList<PropertyMetadata> props = new List<PropertyMetadata> ();

            foreach (PropertyInfo p_info in typeInfo.GetProperties())
            {
                if (p_info.Name == "Item")
                    continue;

                PropertyMetadata p_data = new PropertyMetadata ();
                p_data.Info = p_info;
                p_data.IsField = false;
                props.Add (p_data);
            }

            foreach (FieldInfo f_info in typeInfo.GetFields())
            {
                PropertyMetadata p_data = new PropertyMetadata ();
                p_data.Info = f_info;
                p_data.IsField = true;

                props.Add (p_data);
            }

            lock (type_properties_lock) {
                try {
                    type_properties.Add (type, props);
                } catch (ArgumentException) {
                    return;
                }
            }
        }

        private static MethodInfo GetConvOp (Type t1, Type t2)
        {
            lock (conv_ops_lock) {
                if (! conv_ops.ContainsKey (t1))
                    conv_ops.Add (t1, new Dictionary<Type, MethodInfo> ());
            }

            var typeInfoT1 = TypeFactory.GetTypeInfo(t1);
            var typeInfoT2 = TypeFactory.GetTypeInfo(t2);

            if (conv_ops[t1].ContainsKey (t2))
                return conv_ops[t1][t2];

            MethodInfo op = typeInfoT1.GetMethod(
                "op_Implicit", new ITypeInfo[] { typeInfoT2 });

            lock (conv_ops_lock) {
                try {
                    conv_ops[t1].Add (t2, op);
                } catch (ArgumentException) {
                    return conv_ops[t1][t2];
                }
            }

            return op;
        }

        private static object ReadValue (Type inst_type, JsonReader reader)
        {
            reader.Read ();
            var inst_typeInfo = TypeFactory.GetTypeInfo(inst_type);

            if (reader.Token == JsonToken.ArrayEnd)
                return null;

            //support for nullable types
            Type underlying_type = Nullable.GetUnderlyingType(inst_type);
            Type value_type = underlying_type ?? inst_type;
            
            if (reader.Token == JsonToken.Null) {

                if (inst_typeInfo.IsClass || underlying_type != null)
                {
                    return null;
                }
                
                throw new JsonException (String.Format (
                            "Can't assign null to an instance of type {0}",
                            inst_type));
            }

            if (reader.Token == JsonToken.Double ||
                reader.Token == JsonToken.Int ||
                reader.Token == JsonToken.UInt ||
                reader.Token == JsonToken.Long ||
                reader.Token == JsonToken.ULong ||
                reader.Token == JsonToken.String ||
                reader.Token == JsonToken.Boolean) {

                Type json_type = reader.Value.GetType ();
                var json_typeInfo = TypeFactory.GetTypeInfo(json_type);
                if (inst_typeInfo.IsAssignableFrom(json_typeInfo))
                    return reader.Value;

                // If there's a custom importer that fits, use it
                if (custom_importers_table.ContainsKey (json_type) &&
                    custom_importers_table[json_type].ContainsKey (
                        value_type)) {

                    ImporterFunc importer =
                        custom_importers_table[json_type][value_type];

                    return importer (reader.Value);
                }

                // Maybe there's a base importer that works
                if (base_importers_table.ContainsKey (json_type) &&
                    base_importers_table[json_type].ContainsKey (
                        value_type)) {

                    ImporterFunc importer =
                        base_importers_table[json_type][value_type];

                    return importer (reader.Value);
                }

                // Maybe it's an enum
                if (inst_typeInfo.IsEnum)
                    return Enum.ToObject (value_type, reader.Value);

                // Try using an implicit conversion operator
                MethodInfo conv_op = GetConvOp (value_type, json_type);

                if (conv_op != null)
                    return conv_op.Invoke (null,
                                           new object[] { reader.Value });

                // No luck
                throw new JsonException (String.Format (
                        "Can't assign value '{0}' (type {1}) to type {2}",
                        reader.Value, json_type, inst_type));
            }

            object instance = null;

            if (reader.Token == JsonToken.ArrayStart) {

                AddArrayMetadata (inst_type);
                ArrayMetadata t_data = array_metadata[inst_type];

                if (! t_data.IsArray && ! t_data.IsList)
                    throw new JsonException (String.Format (
                            "Type {0} can't act as an array",
                            inst_type));

                IList list;
                Type elem_type;

                if (! t_data.IsArray) {
                    list = (IList) Activator.CreateInstance (inst_type);
                    elem_type = t_data.ElementType;
                } else {
                    list = new List<object> ();
                    elem_type = inst_type.GetElementType ();
                }

                while (true) {
                    object item = ReadValue (elem_type, reader);
                    if (reader.Token == JsonToken.ArrayEnd)
                        break;

                    list.Add (item);
                }

                if (t_data.IsArray) {
                    int n = list.Count;
                    instance = Array.CreateInstance (elem_type, n);

                    for (int i = 0; i < n; i++)
                        ((Array) instance).SetValue (list[i], i);
                } else
                    instance = list;

            } else if (reader.Token == JsonToken.ObjectStart) {

                AddObjectMetadata (value_type);
                ObjectMetadata t_data = object_metadata[value_type];

                instance = Activator.CreateInstance (value_type);

                while (true) {
                    reader.Read ();

                    if (reader.Token == JsonToken.ObjectEnd)
                        break;

                    string property = (string) reader.Value;

                    if (t_data.Properties.ContainsKey (property)) {
                        PropertyMetadata prop_data =
                            t_data.Properties[property];

                        if (prop_data.IsField) {
                            ((FieldInfo) prop_data.Info).SetValue (
                                instance, ReadValue (prop_data.Type, reader));
                        } else {
                            PropertyInfo p_info =
                                (PropertyInfo) prop_data.Info;

                            if (p_info.CanWrite)
                                p_info.SetValue (
                                    instance,
                                    ReadValue (prop_data.Type, reader),
                                    null);
                            else
                                ReadValue (prop_data.Type, reader);
                        }

                    } else {
                        if (! t_data.IsDictionary)
                            throw new JsonException (String.Format (
                                    "The type {0} doesn't have the " +
                                    "property '{1}'", inst_type, property));

                        ((IDictionary) instance).Add (
                            property, ReadValue (
                                t_data.ElementType, reader));
                    }

                }

            }

            return instance;
        }

        private static IJsonWrapper ReadValue (WrapperFactory factory,
                                               JsonReader reader)
        {
            reader.Read ();

            if (reader.Token == JsonToken.ArrayEnd ||
                reader.Token == JsonToken.Null)
                return null;

            IJsonWrapper instance = factory ();

            if (reader.Token == JsonToken.String) {
                instance.SetString ((string) reader.Value);
                return instance;
            }

            if (reader.Token == JsonToken.Double) {
                instance.SetDouble ((double) reader.Value);
                return instance;
            }

            if (reader.Token == JsonToken.Int) {
                instance.SetInt ((int) reader.Value);
                return instance;
            }

            if (reader.Token == JsonToken.UInt) {
                instance.SetUInt((uint)reader.Value);
                return instance;
            }

            if (reader.Token == JsonToken.Long) {
                instance.SetLong ((long) reader.Value);
                return instance;
            }

            if (reader.Token == JsonToken.ULong) {
                instance.SetULong((ulong)reader.Value);
                return instance;
            }

            if (reader.Token == JsonToken.Boolean) {
                instance.SetBoolean ((bool) reader.Value);
                return instance;
            }

            if (reader.Token == JsonToken.ArrayStart) {
                instance.SetJsonType (JsonType.Array);

                while (true) {
                    IJsonWrapper item = ReadValue (factory, reader);
                    // nij - added check to see if the item is not null.  This is to handle arrays within arrays.
                    // In those cases when the outer array read the inner array an item was returned back the current
                    // reader.Token is at the ArrayEnd for the inner array.
                    if (item == null && reader.Token == JsonToken.ArrayEnd)
                        break;

                    ((IList) instance).Add (item);
                }
            }
            else if (reader.Token == JsonToken.ObjectStart) {
                instance.SetJsonType (JsonType.Object);

                while (true) {
                    reader.Read ();

                    if (reader.Token == JsonToken.ObjectEnd)
                        break;

                    string property = (string) reader.Value;

                    ((IDictionary) instance)[property] = ReadValue (
                        factory, reader);
                }

            }

            return instance;
        }

        private static void RegisterBaseExporters ()
        {
            base_exporters_table[typeof (byte)] =
                delegate (object obj, JsonWriter writer) {
                    writer.Write (Convert.ToInt32 ((byte) obj));
                };

            base_exporters_table[typeof (char)] =
                delegate (object obj, JsonWriter writer) {
                    writer.Write (Convert.ToString ((char) obj));
                };

            base_exporters_table[typeof (DateTime)] =
                delegate (object obj, JsonWriter writer) {
                    writer.Write (Convert.ToString ((DateTime) obj,
                                                    datetime_format));
                };

            base_exporters_table[typeof (decimal)] =
                delegate (object obj, JsonWriter writer) {
                    writer.Write ((decimal) obj);
                };

            base_exporters_table[typeof (sbyte)] =
                delegate (object obj, JsonWriter writer) {
                    writer.Write (Convert.ToInt32 ((sbyte) obj));
                };

            base_exporters_table[typeof (short)] =
                delegate (object obj, JsonWriter writer) {
                    writer.Write (Convert.ToInt32 ((short) obj));
                };

            base_exporters_table[typeof (ushort)] =
                delegate (object obj, JsonWriter writer) {
                    writer.Write (Convert.ToInt32 ((ushort) obj));
                };

            base_exporters_table[typeof (uint)] =
                delegate (object obj, JsonWriter writer) {
                    writer.Write (Convert.ToUInt64 ((uint) obj));
                };

            base_exporters_table[typeof (ulong)] =
                delegate (object obj, JsonWriter writer) {
                    writer.Write ((ulong) obj);
                };

            base_exporters_table[typeof(float)] =
                delegate (object obj,JsonWriter writer){
                    writer.Write(Convert.ToDouble((float) obj));
                };

            base_exporters_table[typeof(Int64)] =
                delegate (object obj,JsonWriter writer){
                    writer.Write(Convert.ToDouble((Int64) obj));
                };
        }

        private static void RegisterBaseImporters ()
        {
            ImporterFunc importer;

            importer = delegate (object input) {
                return Convert.ToByte ((int) input);
            };
            RegisterImporter (base_importers_table, typeof (int),
                              typeof (byte), importer);

            importer = delegate (object input) {
                return Convert.ToUInt64 ((int) input);
            };
            RegisterImporter (base_importers_table, typeof (int),
                              typeof (ulong), importer);

            importer = delegate (object input) {
                return Convert.ToSByte ((int) input);
            };
            RegisterImporter (base_importers_table, typeof (int),
                              typeof (sbyte), importer);

            importer = delegate (object input) {
                return Convert.ToInt16 ((int) input);
            };
            RegisterImporter (base_importers_table, typeof (int),
                              typeof (short), importer);

            importer = delegate (object input) {
                return Convert.ToUInt16 ((int) input);
            };
            RegisterImporter (base_importers_table, typeof (int),
                              typeof (ushort), importer);

            importer = delegate (object input) {
                return Convert.ToUInt32 ((int) input);
            };
            RegisterImporter (base_importers_table, typeof (int),
                              typeof (uint), importer);

            importer = delegate (object input) {
                return Convert.ToSingle ((int) input);
            };
            RegisterImporter (base_importers_table, typeof (int),
                              typeof (float), importer);

            importer = delegate (object input) {
                return Convert.ToDouble ((int) input);
            };
            RegisterImporter (base_importers_table, typeof (int),
                              typeof (double), importer);

            importer = delegate (object input) {
                return Convert.ToDecimal ((double) input);
            };
            RegisterImporter (base_importers_table, typeof (double),
                              typeof (decimal), importer);

            importer = delegate(object input)
            {
                return Convert.ToSingle((float)(double)input);
            };
            RegisterImporter(base_importers_table,typeof(double),
                              typeof(float),importer);

            importer = delegate (object input) {
                return Convert.ToUInt32 ((long) input);
            };
            RegisterImporter (base_importers_table, typeof (long),
                              typeof (uint), importer);

            importer = delegate (object input) {
                return Convert.ToChar ((string) input);
            };
            RegisterImporter (base_importers_table, typeof (string),
                              typeof (char), importer);

            importer = delegate (object input) {
                return Convert.ToDateTime ((string) input, datetime_format);
            };
            RegisterImporter (base_importers_table, typeof (string),
                              typeof (DateTime), importer);
                              
            importer = delegate(object input) {
                return Convert.ToInt64 ((Int32)input);
            };
            RegisterImporter (base_importers_table, typeof (Int32),
                              typeof(Int64), importer);
        }

        private static void RegisterImporter (
            IDictionary<Type, IDictionary<Type, ImporterFunc>> table,
            Type json_type, Type value_type, ImporterFunc importer)
        {
            if (! table.ContainsKey (json_type))
                table.Add (json_type, new Dictionary<Type, ImporterFunc> ());

            table[json_type][value_type] = importer;
        }

        private static void WriteValue (object obj, JsonWriter writer,
                                        bool writer_is_private,
                                        int depth)
        {
            if (depth > max_nesting_depth)
                throw new JsonException (
                    String.Format ("Max allowed object depth reached while " +
                                   "trying to export from type {0}",
                                   obj.GetType ()));

            if (obj == null) {
                writer.Write (null);
                return;
            }

            if (obj is IJsonWrapper) {
                if (writer_is_private)
                    writer.TextWriter.Write (((IJsonWrapper) obj).ToJson ());
                else
                    ((IJsonWrapper) obj).ToJson (writer);

                return;
            }

            if (obj is String) {
                writer.Write ((string) obj);
                return;
            }

            if (obj is Double) {
                writer.Write ((double) obj);
                return;
            }

            if (obj is Int32) {
                writer.Write ((int) obj);
                return;
            }

            if (obj is UInt32)
            {
                writer.Write((uint)obj);
                return;
            }

            if (obj is Boolean) {
                writer.Write ((bool) obj);
                return;
            }

            if (obj is Int64) {
                writer.Write ((long) obj);
                return;
            }

            if (obj is UInt64)
            {
                writer.Write((ulong)obj);
            }

            if (obj is Array) {
                writer.WriteArrayStart ();

                foreach (object elem in (Array) obj)
                    WriteValue (elem, writer, writer_is_private, depth + 1);

                writer.WriteArrayEnd ();

                return;
            }

            if (obj is IList) {
                writer.WriteArrayStart ();
                foreach (object elem in (IList) obj)
                    WriteValue (elem, writer, writer_is_private, depth + 1);
                writer.WriteArrayEnd ();

                return;
            }

            if (obj is IDictionary) {
                writer.WriteObjectStart ();
                foreach (DictionaryEntry entry in (IDictionary) obj) {
                    writer.WritePropertyName ((string) entry.Key);
                    WriteValue (entry.Value, writer, writer_is_private,
                                depth + 1);
                }
                writer.WriteObjectEnd ();

                return;
            }

            Type obj_type = obj.GetType ();

            // See if there's a custom exporter for the object
            if (custom_exporters_table.ContainsKey (obj_type)) {
                ExporterFunc exporter = custom_exporters_table[obj_type];
                exporter (obj, writer);

                return;
            }

            // If not, maybe there's a base exporter
            if (base_exporters_table.ContainsKey (obj_type)) {
                ExporterFunc exporter = base_exporters_table[obj_type];
                exporter (obj, writer);

                return;
            }

            // Last option, let's see if it's an enum
            if (obj is Enum) {
                Type e_type = Enum.GetUnderlyingType (obj_type);

                if (e_type == typeof (long)
                    || e_type == typeof (uint)
                    || e_type == typeof (ulong))
                    writer.Write ((ulong) obj);
                else
                    writer.Write ((int) obj);

                return;
            }

            // Okay, so it looks like the input should be exported as an
            // object
            AddTypeProperties (obj_type);
            IList<PropertyMetadata> props = type_properties[obj_type];

            writer.WriteObjectStart ();
            foreach (PropertyMetadata p_data in props) {
                if (p_data.IsField) {
                    writer.WritePropertyName (p_data.Info.Name);
                    WriteValue (((FieldInfo) p_data.Info).GetValue (obj),
                                writer, writer_is_private, depth + 1);
                }
                else {
                    PropertyInfo p_info = (PropertyInfo) p_data.Info;

                    if (p_info.CanRead)
                    {
                        writer.WritePropertyName(p_data.Info.Name);
                        WriteValue(p_info.GetGetMethod().Invoke(obj, null),
                                    writer, writer_is_private, depth + 1);
                    }
                }
            }
            writer.WriteObjectEnd ();
        }
        #endregion


        public static string ToJson (object obj)
        {
            lock (static_writer_lock) {
                static_writer.Reset ();

                WriteValue (obj, static_writer, true, 0);

                return static_writer.ToString ();
            }
        }

        public static void ToJson (object obj, JsonWriter writer)
        {
            WriteValue (obj, writer, false, 0);
        }

        public static JsonData ToObject (JsonReader reader)
        {
            return (JsonData) ToWrapper (
                delegate { return new JsonData (); }, reader);
        }

        public static JsonData ToObject (TextReader reader)
        {
            JsonReader json_reader = new JsonReader (reader);

            return (JsonData) ToWrapper (
                delegate { return new JsonData (); }, json_reader);
        }

        public static JsonData ToObject (string json)
        {
            return (JsonData) ToWrapper (
                delegate { return new JsonData (); }, json);
        }

        public static T ToObject<T> (JsonReader reader)
        {
            return (T) ReadValue (typeof (T), reader);
        }

        public static T ToObject<T> (TextReader reader)
        {
            JsonReader json_reader = new JsonReader (reader);

            return (T) ReadValue (typeof (T), json_reader);
        }

        public static T ToObject<T> (string json)
        {
            JsonReader reader = new JsonReader (json);

            return (T) ReadValue (typeof (T), reader);
        }

        public static IJsonWrapper ToWrapper (WrapperFactory factory,
                                              JsonReader reader)
        {
            return ReadValue (factory, reader);
        }

        public static IJsonWrapper ToWrapper (WrapperFactory factory,
                                              string json)
        {
            JsonReader reader = new JsonReader (json);

            return ReadValue (factory, reader);
        }

        public static void RegisterExporter<T> (ExporterFunc<T> exporter)
        {
            ExporterFunc exporter_wrapper =
                delegate (object obj, JsonWriter writer) {
                    exporter ((T) obj, writer);
                };

            custom_exporters_table[typeof (T)] = exporter_wrapper;
        }

        public static void RegisterImporter<TJson, TValue> (
            ImporterFunc<TJson, TValue> importer)
        {
            ImporterFunc importer_wrapper =
                delegate (object input) {
                    return importer ((TJson) input);
                };

            RegisterImporter (custom_importers_table, typeof (TJson),
                              typeof (TValue), importer_wrapper);
        }

        public static void UnregisterExporters ()
        {
            custom_exporters_table.Clear ();
        }

        public static void UnregisterImporters ()
        {
            custom_importers_table.Clear ();
        }
    }
}
