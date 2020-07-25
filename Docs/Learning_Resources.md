# Learning Resources

This page outlines a collection of learning resources curated by the .NET SDK/Lambda team to best help guide learning and development with the various tools included in this repo.

## Table of Contents
- [General](#general)
- [Blog Posts](#aws-blog-posts)
- [Community Posts](#community-posts)
- [Templates and Repos](#templates-&-repositories)
- [AWS Talks](#aws-recorded-talks)
- [Community Talks](#community-recorded-talks)

## General

[Lambda Developer Guide](https://docs.aws.amazon.com/lambda/latest/dg/welcome.html)
  * [Programming Model for Authoring Lambda Functions in C#](https://docs.aws.amazon.com/lambda/latest/dg/dotnet-programming-model.html)
  * [Creating a Deployment Package (C#)](https://docs.aws.amazon.com/lambda/latest/dg/lambda-dotnet-how-to-create-deployment-package.html)

## AWS Blog Posts
* [Announcing AWS Lambda support for .NET Core 3.1](https://aws.amazon.com/blogs/compute/announcing-aws-lambda-supports-for-net-core-3-1/)
* [Developing .NET Core AWS Lambda functions](https://aws.amazon.com/blogs/compute/developing-net-core-aws-lambda-functions/)
* [.NET Core Global Tools for AWS](https://aws.amazon.com/blogs/developer/net-core-global-tools-for-aws/)
  * Important read of users of the dotnet Lambda CLI tool.
* [AWS Lambda .NET Core 2.1 Support Released](https://aws.amazon.com/blogs/developer/aws-lambda-net-core-2-1-support-released/)
  * Contains useful information for migrating .NET Core 2.0 Lambda projects to .NET Core 2.1.
* [F# Tooling Support for AWS Lambda](https://aws.amazon.com/blogs/developer/f-tooling-support-for-aws-lambda/)
* [New AWS X-Ray .NET Core Support](https://aws.amazon.com/blogs/developer/new-aws-x-ray-net-core-support/)
  * Contains information on setting up X-Ray with .NET Core Lambda functions.
* [Serverless ASP.NET Core 2.0 Applications](https://aws.amazon.com/blogs/developer/serverless-asp-net-core-2-0-applications/)

## Community Posts
* [My First AWS Lambda Using .NET Core](http://solutionsbyraymond.com/2018/09/20/my-first-aws-lambda-using-net-core/) By Raymond Sanchez, September 2018
* [Developing .NET Core AWS Lambda functions](https://awscentral.blogspot.com/2018/09/developing-net-core-aws-lambda-functions.html?utm_source=dlvr.it&utm_medium=twitter) By Walker Cabay, June 2018
  * Focuses on debugging and diagnostics as well as using the SAM, serverless application model, cli.
* [Going serverless with .NET Core, AWS Lambda and the Serverless framework](http://josephwoodward.co.uk/2017/11/going-serverless-net-core-aws-lambda-serverless-framework) By Joseph Woodward, November 2017
  * Shows how to use the Serverless framework with .NET Core Lambda.
* [Creating a Serverless Application with .NET Core, AWS Lambda and AWS API Gateway](https://www.jerriepelser.com/blog/dotnet-core-aws-lambda-serverless-application/) By Jerrie Pelser, April 2017
  * Tutorial for building a Web API and **not** using ASP.NET Core.
* [Modular Powershell in AWS Lambda Functions](https://rollingwebsphere.home.blog/2020/01/18/aws-lambda-functions-with-modular-powershell/) By Brian Olson, January 2020
  * Tutorial for using modular powershell functions in AWS Lambda.
* [AWS Lambda for .NET Developers](https://marcroussy.com/2019/03/01/aws-lambda-for-dotnet-developers/) By Marc Roussy, March 2019
  * An introduction to the basic concepts of building AWS Lambda functions with .NET Core.

## Templates & Repositories  
* [serverlessDotNetStarter](https://github.com/pharindoko/serverlessDotNetStarter) - By Florian Fuß
  * Start with a simple template for Visual Studio Code
  * Debug locally with Lambda NET Mock Test Tool
  * Deploy easily with the serverless framework

## AWS Recorded Talks
* [Building a .NET Serverless Application on AWS](https://www.youtube.com/watch?v=TZUtB1xXduo) By Abby Fuller, Tara Walker and Nicki Klien 2018
  * Demo of a serverless application using the AWS .NET SDK, AWS Lambda, AWS CodeBuild, AWS X-Ray, Amazon Dynamo DB Accelerator (DAX), and the AWS Toolkit for Visual Studio.
* [Serverless Applications with AWS](https://www.youtube.com/watch?v=sgXq5-UGRt8&list=PL03Lrmd9CiGei7clxJEyIIbVTm5NWJPm7) - From NDC Minnesota 2018 by Norm Johanson
  * Description of how .NET Core Lambda works
  * Explain how AWS Lambda scales
  * How to use AWS Step Functions
  * A brief section on using the .NET Lambda tools for CI/CD
* [.NET Serverless Development on AWS](https://www.youtube.com/watch?v=IBeqDaMDjf0) - AWS Online Tech Talks by Norm Johanson 2018
  * Shows how to use both Visual Studio and dotnet CLI tools
  * Create an F# Lambda function
  * How to use X-Ray with Lambda
  * Demonstrate using the `dotnet lambda package-ci` command for CI/CD with AWS Code services. 
* [Containers and Serverless with AWS](https://www.youtube.com/watch?v=TYb-vw6knQ0&list=PL03Lrmd9CiGfprrIjzbjdA2RRShJMzYIM) - From NDC Oslo 2018 By Norm Johanson
  * Compares the serverless and container platforms to help inform deciding which platform to use.
* [How to Deploy .NET Code to AWS from Within Visual Studio](https://www.youtube.com/watch?v=pgRzdZeNxD8) - AWS Online Tech Talks, August 2017

## Community Recorded Talks
* [Create a Serverless .NET Core 2.1 Web API with AWS Lambda](https://www.youtube.com/watch?v=OhEANj3Y6ZQ) By Daniel Donbavand, August 2018
  * Tutorial for building a .NET Lambda Web API.
* [AWS for .NET Developers - AWS Lambda, S3, Rekognition - .NET Concept of the Week - Episode 15](https://www.youtube.com/watch?v=yFbLCqToEYc) By Greg Kalapos, July 2018
  * In this episode we create a "Not Hotdog" clone from Silicon Valley (HBO) called "SchnitzelOrNot" with .NET and AWS. For this we use AWS Lambda with .NET Core, S3, and Amazon Rekognition.
