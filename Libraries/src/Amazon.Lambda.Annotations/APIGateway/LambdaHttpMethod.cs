namespace Amazon.Lambda.Annotations.APIGateway
{
    /// <summary>
    /// HTTP Method/Verb
    /// </summary>
    public enum LambdaHttpMethod
    {
        /// <summary>
        /// Any HTTP Method/Verb
        /// </summary>
        Any,
        /// <summary>
        /// GET HTTP Method/Verb
        /// </summary>
        Get,
        /// <summary>
        /// POST HTTP Method/Verb
        /// </summary>
        Post,
        /// <summary>
        /// PUT HTTP Method/Verb
        /// </summary>
        Put,
        /// <summary>
        /// PATCH HTTP Method/Verb
        /// </summary>
        Patch,
        /// <summary>
        /// HEAED HTTP Method/Verb
        /// </summary>
        Head,
        /// <summary>
        /// DELETE HTTP Method/Verb
        /// </summary>
        Delete,
        /// <summary>
        /// OPTIONS HTTP Method/Verb
        /// </summary>
        Options
    }
}