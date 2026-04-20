// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.Annotations.DynamoDB
{
    /// <summary>
    /// The position in the DynamoDB stream where Lambda starts reading.
    /// </summary>
    public enum StartingPosition
    {
        /// <summary>
        /// Start reading at the most recent record in the shard.
        /// </summary>
        LATEST,

        /// <summary>
        /// Start reading at the last untrimmed record in the shard.
        /// This is the oldest record in the shard.
        /// </summary>
        TRIM_HORIZON
    }
}
