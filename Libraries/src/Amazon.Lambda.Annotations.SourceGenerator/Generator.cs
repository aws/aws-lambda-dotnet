using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator
{
    [Generator]
    public class Generator : ISourceGenerator
    {
        public Generator()
        {
#if DEBUG
            //if (!Debugger.IsAttached)
            //{
            //    Debugger.Launch();
            //}
#endif
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // retrieve the populated receiver
            if (!(context.SyntaxContextReceiver is SyntaxReceiver receiver))
            {
                return;
            }

            foreach (var lambdaFunction in receiver.LambdaFunctions)
            {
                var codeGenerator = new LambdaFunctionCodeGenerator(lambdaFunction, receiver.StartupClass, context);
                var (hint, sourceText) = codeGenerator.GenerateSource();
                context.AddSource(hint, sourceText);
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            // Register a syntax receiver that will be created for each generation pass
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }
    }
}
