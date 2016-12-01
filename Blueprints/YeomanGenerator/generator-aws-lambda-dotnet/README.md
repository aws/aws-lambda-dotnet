# AWS Lambda .NET Blueprints

Blueprints are used for setting up new .NET Core projects for AWS Lambda. They are defined in a generic form 
so that they can be exposed through Visual Studio or [Yeoman](http://yeoman.io/).

## Definitions

The blueprints are defined in sub directories under the BlueprintDefinitions directory. For each blueprint there 
is a blueprint-manifest.json file containing the metadata for the blueprint and a src and test directory.
It is required that each blueprint must contain a test project.

## Packaging

The .NET Core console application BlueprintPackager is used to package up the blueprints for both Visual Studio and Yeoman.
The console application can be run by executing `dotnet run` in the project directory.

## Visual Studio

The BlueprintPackager will write the blueprints to the ../Deployment/Blueprints/VisualStudioBlueprints directory. You can test
how your blueprints work in Visual Studio by copying the directory to C:\Program Files (x86)\AWS Tools\HostedFiles\LambdaSampleFunctions\NETCore\v1
and then point AWS Toolkit for Visual Studio to C:\Program Files (x86)\AWS Tools\HostedFiles to get its metadata. To update
the toolkit open Visual Studio's Options dialog from the Tools menu, select AWS Toolkit and select "Use local file system location".

## Yeoman

To use the blueprints with Yeoman you must first install [npm](https://nodejs.org/en/) which is part of the Node.js 
install. Once npm is installed you can install Yeoman by running the following command.

```
npm install -g yo
```

To install the current distributed version of the AWS Lambda .NET Core blueprints run the following command.

```
npm install -g generator-aws-lambda-dotnet
```

When the BlueprintPackager runs it will copy the Yeoman generator to ../Deployment/Blueprints/generator-aws-lambda-dotnet.
To use these blueprints instead of the distributed version execute the command `npm link` in the directory. To switch 
back to the distributed version execute the command `npm unlink`.

To run the Yeoman generator which will allow you to pick a blueprint run the following command.
```
yo aws-lambda-dotnet
```