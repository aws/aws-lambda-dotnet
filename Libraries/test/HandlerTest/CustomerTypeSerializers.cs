/* Copyright 2018 Amazon.com, Inc. or its affiliates. All Rights Reserved. */
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

using Amazon.Lambda.Core;
using System;
using System.IO;
using System.Text;

namespace HandlerTest
{
    public class ConstructorExceptionCustomerTypeSerializer : ILambdaSerializer
    {
        public T Deserialize<T>(Stream requestStream)
        {
            var type = typeof(T);
            if (type == typeof(CustomerPoco))
            {
                object poco = new CustomerPoco(Common.GetString(requestStream));
                return (T)poco;
            }
            if (type == typeof(CustomerPocoIn))
            {
                object poco = new CustomerPocoIn(Common.GetString(requestStream));
                return (T)poco;
            }
            if (type == typeof(CustomerPocoOut))
            {
                object poco = new CustomerPocoOut(Common.GetString(requestStream));
                return (T)poco;
            }

            throw new InvalidCastException();
        }

        public void Serialize<T>(T response, Stream responseStream)
        {
            string data;
            object obj = response;

            if (response is CustomerPoco)
            {
                var poco = (CustomerPoco)obj;
                data = poco.Data;
            }
            else
            {
                throw new InvalidCastException();
            }

            using (var writer = new StreamWriter(responseStream))
            {
                writer.Write(data);
            }
        }

        public ConstructorExceptionCustomerTypeSerializer()
        {
            throw new Exception();
        }
    }
    public class SpecialCustomerTypeSerializer : ILambdaSerializer
    {
        public T Deserialize<T>(Stream requestStream)
        {
            var type = typeof(T);
            if (type == typeof(SpecialCustomerPoco))
            {
                object poco = new SpecialCustomerPoco(Common.GetString(requestStream));
                return (T)poco;
            }

            throw new InvalidCastException();
        }

        public void Serialize<T>(T response, Stream responseStream)
        {
            string data;
            object obj = response;

            if (response is SpecialCustomerPoco)
            {
                SpecialCustomerPoco poco = (SpecialCustomerPoco)obj;
                data = poco.Data;
            }
            else
            {
                throw new InvalidCastException();
            }

            responseStream.Write(Encoding.ASCII.GetBytes(data), 0, data.Length);
        }
    }
    public class NoZeroParameterConstructorCustomerTypeSerializer : ILambdaSerializer
    {
        public T Deserialize<T>(Stream requestStream)
        {
            throw new NotImplementedException();
        }

        public void Serialize<T>(T response, Stream responseStream)
        {
            throw new NotImplementedException();
        }

        public NoZeroParameterConstructorCustomerTypeSerializer(int data)
        {

        }
    }
    public class ExceptionInConstructorCustomerTypeSerializer : ILambdaSerializer
    {
        public T Deserialize<T>(Stream requestStream)
        {
            throw new NotImplementedException();
        }

        public void Serialize<T>(T response, Stream responseStream)
        {
            throw new NotImplementedException();
        }

        public ExceptionInConstructorCustomerTypeSerializer(int data)
        {
            throw new ArithmeticException("Exception in constructor of serializer!");
        }
    }
    public class NoInterfaceCustomerTypeSerializer
    {
        public T Deserialize<T>(Stream requestStream)
        {
            throw new NotImplementedException();
        }

        public void Serialize<T>(T response, Stream responseStream)
        {
            throw new NotImplementedException();
        }
    }
}
