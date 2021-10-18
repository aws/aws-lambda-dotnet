namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    public class LambdaFunctionModel : ILambdaFunctionSerializable
    {
        public string Handler { get; set; }
        public string Name { get; set; }
        public int Timeout { get; set; }
        public int MemorySize { get; set; }
        public string Role { get; set; }
        public string Policies { get; set; }
        
        // more will be added in the future
    }
}