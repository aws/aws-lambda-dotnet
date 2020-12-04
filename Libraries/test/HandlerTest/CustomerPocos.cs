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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HandlerTest
{
    public class CustomerPoco
    {
        public string Data { get; set; }

        public CustomerPoco(string data)
        {
            Data = data;
        }
    }

    public class CustomerPocoIn : CustomerPoco
    {
        public CustomerPocoIn(string data)
            : base(data) { }
    }
    public class CustomerPocoOut : CustomerPoco
    {
        public CustomerPocoOut(string data)
            : base(data) { }
    }

    public class SpecialCustomerPoco
    {
        public string Data { get; set; }
        public SpecialCustomerPoco(string data)
        {
            Data = data;
        }
    }

    public class Task2 : Task
    {
        public Task2(Action action)
            : base(action)
        {
        }
    }
    public class Task2<T> : Task<T>
    {
        public Task2(Func<T> func)
            : base(func)
        { }
    }
    public class Task3 : Task<string>
    {
        public Task3(Func<string> func)
            : base(func)
        { }
    }
    public class Task4<T,V> : Task<T>
    {
        public Task4(Func<T> func)
            : base(func)
        { }
    }
    public class Task5<V> : Task<string>
    {
        public Task5(Func<string> func)
            : base(func)
        { }

        public Task5()
            : base(() => typeof(V).FullName)
        { }
    }

}
