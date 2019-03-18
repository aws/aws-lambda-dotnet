/*******************************************************************************
 *  Copyright 2008-2017 Amazon.com, Inc. or its affiliates. All Rights Reserved.
 *  Licensed under the Apache License, Version 2.0 (the "License"). You may not use
 *  this file except in compliance with the License. A copy of the License is located at
 *
 *  http://aws.amazon.com/apache2.0
 *
 *  or in the "license" file accompanying this file.
 *  This file is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
 *  CONDITIONS OF ANY KIND, either express or implied. See the License for the
 *  specific language governing permissions and limitations under the License.
 * *****************************************************************************
 *    __  _    _  ___
 *   (  )( \/\/ )/ __)
 *   /__\ \    / \__ \
 *  (_)(_) \/\/  (___/
 *
 *  AWS SDK for .NET
 *
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Amazon.Util.Internal
{
    internal class AmazonUtils
    {
        public static readonly DateTime EPOCH_START = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static double ConvertToUnixEpochMilliSeconds(DateTime dateTime)
        {
            TimeSpan ts = new TimeSpan(dateTime.ToUniversalTime().Ticks - EPOCH_START.Ticks);
            double milli = Math.Round(ts.TotalMilliseconds, 0) / 1000.0;
            return milli;
        }
    }
    internal interface ITypeInfo
    {
        Type BaseType { get; }

        Type Type { get; }

        Assembly Assembly { get; }
        bool IsArray { get; }

        Array ArrayCreateInstance(int length);

        Type GetInterface(string name);
        Type[] GetInterfaces();

        IEnumerable<PropertyInfo> GetProperties();

        IEnumerable<FieldInfo> GetFields();
        FieldInfo GetField(string name);

        MethodInfo GetMethod(string name);
        MethodInfo GetMethod(string name, ITypeInfo[] paramTypes);

        MemberInfo[] GetMembers();


        ConstructorInfo GetConstructor(ITypeInfo[] paramTypes);

        PropertyInfo GetProperty(string name);

        bool IsAssignableFrom(ITypeInfo typeInfo);

        bool IsEnum {get;}

        bool IsClass { get; }
        bool IsValueType { get; }

        bool IsInterface { get; }
        bool IsAbstract { get; }
        bool IsSealed { get; }

        object EnumToObject(object value);

        ITypeInfo EnumGetUnderlyingType();

        object CreateInstance();

        ITypeInfo GetElementType();

        bool IsType(Type type);

        string FullName { get; }
        string Name { get; }

        bool IsGenericTypeDefinition { get; }
        bool IsGenericType { get; }
        bool ContainsGenericParameters { get; }
        Type GetGenericTypeDefinition();
        Type[] GetGenericArguments();

        object[] GetCustomAttributes(bool inherit);
        object[] GetCustomAttributes(ITypeInfo attributeType, bool inherit);

    }

    internal static class TypeFactory
    {
        public static readonly ITypeInfo[] EmptyTypes = new ITypeInfo[] { };
        public static ITypeInfo GetTypeInfo(Type type)
        {
            if (type == null)
                return null;

            return new TypeInfoWrapper(type);
        }

        abstract class AbstractTypeInfo : ITypeInfo
        {
            protected Type _type;

            internal AbstractTypeInfo(Type type)
            {
                this._type = type;
            }

            public Type Type
            {
                get { return this._type; }
            }

            public override int GetHashCode()
            {
                return this._type.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                var typeWrapper = obj as AbstractTypeInfo;
                if (typeWrapper == null)
                    return false;

                return this._type.Equals(typeWrapper._type);
            }

            public bool IsType(Type type)
            {
                return this._type == type;
            }

            public abstract Type BaseType { get; }
            public abstract Assembly Assembly { get; }
            public abstract Type GetInterface(string name);
            public abstract Type[] GetInterfaces();
            public abstract IEnumerable<PropertyInfo> GetProperties();
            public abstract IEnumerable<FieldInfo> GetFields();
            public abstract FieldInfo GetField(string name);
            public abstract MethodInfo GetMethod(string name);
            public abstract MethodInfo GetMethod(string name, ITypeInfo[] paramTypes);
            public abstract MemberInfo[] GetMembers();
            public abstract PropertyInfo GetProperty(string name);
            public abstract bool IsAssignableFrom(ITypeInfo typeInfo);
            public abstract bool IsClass { get; }
            public abstract bool IsInterface { get; }
            public abstract bool IsAbstract { get; }
            public abstract bool IsSealed { get; }
            public abstract bool IsEnum { get; }
            public abstract bool IsValueType { get; }
            public abstract ConstructorInfo GetConstructor(ITypeInfo[] paramTypes);

            public abstract object[] GetCustomAttributes(bool inherit);
            public abstract object[] GetCustomAttributes(ITypeInfo attributeType, bool inherit);

            public abstract bool ContainsGenericParameters { get; }
            public abstract bool IsGenericTypeDefinition { get; }
            public abstract bool IsGenericType { get; }
            public abstract Type GetGenericTypeDefinition();
            public abstract Type[] GetGenericArguments();

            public bool IsArray
            {
                get { return this._type.IsArray; }
            }


            public object EnumToObject(object value)
            {
                return Enum.ToObject(this._type, value);
            }

            public ITypeInfo EnumGetUnderlyingType()
            {
                return TypeFactory.GetTypeInfo(Enum.GetUnderlyingType(this._type));
            }

            public object CreateInstance()
            {
                return Activator.CreateInstance(this._type);
            }

            public Array ArrayCreateInstance(int length)
            {
                return Array.CreateInstance(this._type, length);
            }

            public ITypeInfo GetElementType()
            {
                return TypeFactory.GetTypeInfo(this._type.GetElementType());
            }

            public string FullName
            {
                get
                {
                    return this._type.FullName;
                }
            }

            public string Name
            {
                get
                {
                    return this._type.Name;
                }
            }
        }
        class TypeInfoWrapper : AbstractTypeInfo
        {

            internal TypeInfoWrapper(Type type)
                : base(type)
            {
            }

            public override Type BaseType
            {
                get { return this._type.BaseType; }
            }

            public override FieldInfo GetField(string name)
            {
                return this._type.GetField(name);
            }

            public override Type GetInterface(string name)
            {
                return this._type.GetInterfaces().FirstOrDefault(x => (x.Namespace + "." + x.Name) == name);
            }

            public override Type[] GetInterfaces()
            {
                return this._type.GetInterfaces();
            }

            public override IEnumerable<PropertyInfo> GetProperties()
            {
                return this._type.GetProperties();
            }

            public override IEnumerable<FieldInfo> GetFields()
            {
                return this._type.GetFields();
            }

            public override MemberInfo[] GetMembers()
            {
                return this._type.GetMembers();
            }

            public override bool IsClass
            {
                get { return this._type.IsClass; }
            }

            public override bool IsValueType
            {
                get { return this._type.IsValueType; }
            }

            public override bool IsInterface
            {
                get { return this._type.IsInterface; }
            }

            public override bool IsAbstract
            {
                get { return this._type.IsAbstract; }
            }

            public override bool IsSealed
            {
                get { return this._type.IsSealed; }
            }

            public override bool IsEnum
            {
                get { return this._type.IsEnum; }
            }

            public override MethodInfo GetMethod(string name)
            {
                return this._type.GetMethod(name);
            }

            public override MethodInfo GetMethod(string name, ITypeInfo[] paramTypes)
            {
                Type[] types = new Type[paramTypes.Length];
                for (int i = 0; i < paramTypes.Length; i++)
                    types[i] = ((AbstractTypeInfo)paramTypes[i]).Type;
                return this._type.GetMethod(name, types);
            }

            public override ConstructorInfo GetConstructor(ITypeInfo[] paramTypes)
            {
                Type[] types = new Type[paramTypes.Length];
                for (int i = 0; i < paramTypes.Length; i++)
                    types[i] = ((AbstractTypeInfo)paramTypes[i]).Type;
                var constructor = this._type.GetConstructor(types);
                return constructor;
            }

            public override PropertyInfo GetProperty(string name)
            {
                return this._type.GetProperty(name);
            }

            public override bool IsAssignableFrom(ITypeInfo typeInfo)
            {
                return this._type.IsAssignableFrom(((AbstractTypeInfo)typeInfo).Type);
            }

            public override bool ContainsGenericParameters
            {
                get { return this._type.ContainsGenericParameters; }
            }

            public override bool IsGenericTypeDefinition
            {
                get { return this._type.IsGenericTypeDefinition; }
            }

            public override bool IsGenericType
            {
                get { return this._type.IsGenericType; }
            }

            public override Type GetGenericTypeDefinition()
            {
                return this._type.GetGenericTypeDefinition();
            }

            public override Type[] GetGenericArguments()
            {
                return this._type.GetGenericArguments();
            }

            public override object[] GetCustomAttributes(bool inherit)
            {
                return this._type.GetCustomAttributes(inherit);
            }

            public override object[] GetCustomAttributes(ITypeInfo attributeType, bool inherit)
            {
                return this._type.GetCustomAttributes(((TypeInfoWrapper)attributeType)._type, inherit);
            }

            public override Assembly Assembly
            {
                get { return this._type.Assembly; }
            }
        }
    }
}
