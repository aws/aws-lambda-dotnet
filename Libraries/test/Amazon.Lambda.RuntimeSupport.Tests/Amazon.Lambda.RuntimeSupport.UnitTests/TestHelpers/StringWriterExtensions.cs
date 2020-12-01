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
using System.IO;
using System.Linq;
using Xunit.Abstractions;

namespace Amazon.Lambda.RuntimeSupport.UnitTests.TestHelpers
{
    public static class StringWriterExtensions
    {
        public static void Clear(this StringWriter writer)
        {
            var sb = writer.GetStringBuilder();
            sb.Remove(0, sb.Length);
        }

        private const double TicksPerMicrosecond = (TimeSpan.TicksPerMillisecond / 1000);

        public static string ToPrettyTime(this TimeSpan ts)
        {
            var times = new List<string>();

            if (ts.Days > 0)
                times.Add($"{ts.Days} days");
            if (ts.Hours > 0)
                times.Add($"{ts.Hours} hours");
            if (ts.Minutes > 0)
                times.Add($"{ts.Minutes} minutes");
            if (ts.Seconds > 0)
                times.Add($"{ts.Seconds} seconds");
            if (ts.Milliseconds > 0)
                times.Add($"{ts.Milliseconds} ms");
            var totalMicroseconds = ts.Ticks / TicksPerMicrosecond;
            var microseconds = totalMicroseconds % 1000;
            microseconds = Math.Round(microseconds, 3);
            if (microseconds > 0)
                times.Add($"{microseconds} microsecond");

            if (times.Count == 0)
                return "No time!";
            var text = string.Join(", ", times.Where(t => !string.IsNullOrEmpty(t)));
            return $"{text} ({ts.TotalMilliseconds}ms)";
        }

        public static Lazy<T> ToLazy<T>(this T self)
        {
            return new Lazy<T>(() => self);
        }

        public static Action<string> ToLoggingAction(this TextWriter writer)
        {
            return writer.WriteLine;
        }

        public static Action<string> ToLoggingAction(this ITestOutputHelper writer)
        {
            return writer.WriteLine;
        }
    }
}