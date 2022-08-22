namespace Amazon.Lambda.Annotations.SourceGenerator.Writers
{
    /// <summary>
    /// This interface contains utility methods to manipulate a YAML or JSON blob
    /// </summary>
    public interface ITemplateWriter
    {
        /// <summary>
        /// Checks if the dot(.) seperated path exists in the blob
        /// </summary>
        /// <param name="path">dot(.) seperated path. Example "Person.Name.FirstName"</param>
        /// <returns>true if the path exist, else false</returns>
        bool Exists(string path);

        /// <summary>
        /// Gets the object stored at the dot(.) seperated path. If the path does not exist then return the defaultToken.
        /// </summary>
        /// <param name="path">dot(.) seperated path. Example "Person.Name.FirstName"</param>
        /// <param name="defaultToken">The object that is returned if path does not exist.</param>
        object GetToken(string path, object defaultToken = null);

        /// <summary>
        /// Gets the object stored at the dot(.) seperated path. If the path does not exist then return the defaultToken.
        /// </summary>
        /// <param name="path">dot(.) seperated path. Example "Person.Name.FirstName"</param>
        /// <param name="defaultToken">The object that is returned if path does not exist.</param>
        T GetToken<T>(string path, object defaultToken = null);

        /// <summary>
        /// Sets the token at the dot(.) seperated path.
        /// </summary>
        /// <param name="path">dot(.) seperated path. Example "Person.Name.FirstName"</param>
        /// <param name="token">The object to set at the specified path</param>
        /// <param name="tokenType"><see cref="TokenType"/></param>
        void SetToken(string path, object token, TokenType tokenType = TokenType.Other);

        /// <summary>
        /// Deletes the token found at the dot(.) separated path.
        /// </summary>
        /// <param name="path">dot(.) seperated path. Example "Person.Name.FirstName"</param>
        void RemoveToken(string path);

        /// <summary>
        /// Returns the template as a string
        /// </summary>
        string GetContent();

        /// <summary>
        /// Converts the content into an in-memory representation of JSON or YAML node
        /// </summary>
        /// <param name="content"></param>
        void Parse(string content);

        /// <summary>
        /// If the string does not start with '@', return it as is.
        /// If a string value starts with '@' then a reference node is created and returned.
        /// </summary>
        object GetValueOrRef(string value);
    }
}