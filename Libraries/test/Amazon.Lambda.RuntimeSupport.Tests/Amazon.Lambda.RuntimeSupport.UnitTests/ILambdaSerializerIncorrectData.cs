/*
 * Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License").
 * You may not use this file except in compliance with the License.
 * A copy of the License is located at
 *
 *  http://aws.amazon.com/apache2.0
 *
 * or in the "license" file accompanying this file. This file is distributed
 * on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing
 * permissions and limitations under the License.
 */

using System.IO;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class ILSClass
    {
    }

    public interface ILSEmpty
    {
    }

    public interface ILSDeserializeNongeneric
    {
        void Deserialize();
    }

    public interface ILSDeserializeNoInputs
    {
        void Deserialize<T>();
    }

    public interface ILSDeserializeWrongInput
    {
        void Deserialize<T>(string input);
    }

    public interface ILSDeserializeWrongGenerics
    {
        void Deserialize<T, V>(Stream input);
    }

    public interface ILSDeserializeWrongOutput
    {
        object Deserialize<T>(Stream input);
    }

    public interface ILSSerializeMissing
    {
        T Deserialize<T>(Stream input);
    }

    public interface ILSSerializeNotGeneric
    {
        T Deserialize<T>(Stream input);

        void Serialize();
    }

    public interface ILSSerializeNotVoid
    {
        T Deserialize<T>(Stream input);

        object Serialize<T>();
    }

    public interface ILSSerializeNoInputs
    {
        T Deserialize<T>(Stream input);

        void Serialize<T>();
    }

    public interface ILSSerializeWrongGenerics
    {
        T Deserialize<T>(Stream input);

        void Serialize<T, V>(T obj, Stream output);
    }

    public interface ILSSerializeWrongFirstInput
    {
        T Deserialize<T>(Stream input);

        void Serialize<T>(bool obj, Stream output);
    }

    public interface ILSSerializeWrongSecondInput
    {
        T Deserialize<T>(Stream input);

        void Serialize<T>(T obj, string output);
    }
}