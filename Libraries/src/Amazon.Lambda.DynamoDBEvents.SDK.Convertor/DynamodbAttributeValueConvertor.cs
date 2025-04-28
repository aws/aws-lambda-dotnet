namespace Amazon.Lambda.DynamoDBEvents.SDK.Convertor
{
    using System.Collections.Generic;

    /// <summary>
    /// Convert DynamoDB Event AttributeValue to SDK AttributeValue
    /// </summary>
    public static class DynamodbAttributeValueConvertor
    {
        /// <summary>
        /// Convert Lambda AttributeValue to SDK AttributeValue
        /// </summary>
        /// <param name="lambdaAttribute">The Lambda AttributeValue to convert.</param>
        /// <returns>The converted SDK AttributeValue.</returns>
        public static Amazon.DynamoDBv2.Model.AttributeValue ConvertToSdkAttribute(this
            DynamoDBEvent.AttributeValue lambdaAttribute)
        {
            if (lambdaAttribute == null)
                return null;

            var sdkAttribute = new Amazon.DynamoDBv2.Model.AttributeValue();

            // String
            if (!string.IsNullOrEmpty(lambdaAttribute.S))
                sdkAttribute.S = lambdaAttribute.S;

            // Number
            else if (!string.IsNullOrEmpty(lambdaAttribute.N))
                sdkAttribute.N = lambdaAttribute.N;

            // Boolean
            else if (lambdaAttribute.BOOL.HasValue)
            {
                sdkAttribute.BOOL = lambdaAttribute.BOOL.Value;
            }

            // Null
            else if (lambdaAttribute.NULL.HasValue)
                sdkAttribute.NULL = lambdaAttribute.NULL.Value;

            // Binary
            else if (lambdaAttribute.B != null)
                sdkAttribute.B = lambdaAttribute.B;

            // String Set
            else if (lambdaAttribute.SS != null)
                sdkAttribute.SS = new List<string>(lambdaAttribute.SS);

            // Number Set
            else if (lambdaAttribute.NS != null)
                sdkAttribute.NS = new List<string>(lambdaAttribute.NS);

            // Binary Set
            else if (lambdaAttribute.BS != null)
                sdkAttribute.BS = lambdaAttribute.BS;

            // List
            else if (lambdaAttribute.L != null)
            {
                sdkAttribute.L = new List<Amazon.DynamoDBv2.Model.AttributeValue>();
                foreach (var item in lambdaAttribute.L)
                {
                    sdkAttribute.L.Add(item.ConvertToSdkAttribute());
                }
            }

            // Map
            else if (lambdaAttribute.M != null)
            {
                sdkAttribute.M = new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>();
                foreach (var kvp in lambdaAttribute.M)
                {
                    sdkAttribute.M[kvp.Key] = kvp.Value.ConvertToSdkAttribute();
                }
            }

            return sdkAttribute;
        }

        /// <summary>
        /// Convert Dictionary of Lambda AttributeValue to SDK Dictionary of AttributeValue
        /// </summary>
        /// <param name="lambdaAttributes">The dictionary of Lambda AttributeValue to convert.</param>
        /// <returns>The converted dictionary of SDK AttributeValue.</returns>
        public static Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> ConvertToSdkAttributeValueDictionary(
           this Dictionary<string, DynamoDBEvent.AttributeValue> lambdaAttributes)
        {
            var sdkDictionary = new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>();

            if (lambdaAttributes == null)
                return sdkDictionary;

            foreach (var kvp in lambdaAttributes)
            {
                sdkDictionary[kvp.Key] = ConvertToSdkAttribute(kvp.Value);
            }

            return sdkDictionary;
        }

        /// <summary>
        /// Convert Lambda AttributeValue to SDK AttributeValue
        /// </summary>
        /// <param name="lambdaAttribute">The Lambda AttributeValue to convert.</param>
        /// <returns>The converted SDK AttributeValue.</returns>
        public static Amazon.DynamoDBStreams.Model.AttributeValue ConvertToSdkStreamAttribute(this
            DynamoDBEvent.AttributeValue lambdaAttribute)
        {
            if (lambdaAttribute == null)
                return null;

            var sdkAttribute = new Amazon.DynamoDBStreams.Model.AttributeValue();

            // String
            if (!string.IsNullOrEmpty(lambdaAttribute.S))
                sdkAttribute.S = lambdaAttribute.S;

            // Number
            else if (!string.IsNullOrEmpty(lambdaAttribute.N))
                sdkAttribute.N = lambdaAttribute.N;

            // Boolean
            else if (lambdaAttribute.BOOL.HasValue)
            {
                sdkAttribute.BOOL = lambdaAttribute.BOOL.Value;
            }

            // Null
            else if (lambdaAttribute.NULL.HasValue)
                sdkAttribute.NULL = lambdaAttribute.NULL.Value;

            // Binary
            else if (lambdaAttribute.B != null)
                sdkAttribute.B = lambdaAttribute.B;

            // String Set
            else if (lambdaAttribute.SS != null)
                sdkAttribute.SS = new List<string>(lambdaAttribute.SS);

            // Number Set
            else if (lambdaAttribute.NS != null)
                sdkAttribute.NS = new List<string>(lambdaAttribute.NS);

            // Binary Set
            else if (lambdaAttribute.BS != null)
                sdkAttribute.BS = lambdaAttribute.BS;

            // List
            else if (lambdaAttribute.L != null)
            {
                sdkAttribute.L = new List<Amazon.DynamoDBStreams.Model.AttributeValue>();
                foreach (var item in lambdaAttribute.L)
                {
                    sdkAttribute.L.Add(item.ConvertToSdkStreamAttribute());
                }
            }

            // Map
            else if (lambdaAttribute.M != null)
            {
                sdkAttribute.M = new Dictionary<string, Amazon.DynamoDBStreams.Model.AttributeValue>();
                foreach (var kvp in lambdaAttribute.M)
                {
                    sdkAttribute.M[kvp.Key] = kvp.Value.ConvertToSdkStreamAttribute();
                }
            }

            return sdkAttribute;
        }

        /// <summary>
        /// Convert Dictionary of Lambda AttributeValue to SDK Dictionary of AttributeValue
        /// </summary>
        /// <param name="lambdaAttributes">The dictionary of Lambda AttributeValue to convert.</param>
        /// <returns>The converted dictionary of SDK AttributeValue.</returns>
        public static Dictionary<string, Amazon.DynamoDBStreams.Model.AttributeValue> ConvertToSdkStreamAttributeValueDictionary(
           this Dictionary<string, DynamoDBEvent.AttributeValue> lambdaAttributes)
        {
            var sdkDictionary = new Dictionary<string, Amazon.DynamoDBStreams.Model.AttributeValue>();

            if (lambdaAttributes == null)
                return sdkDictionary;

            foreach (var kvp in lambdaAttributes)
            {
                sdkDictionary[kvp.Key] = ConvertToSdkStreamAttribute(kvp.Value);
            }

            return sdkDictionary;
        }
    }
}
