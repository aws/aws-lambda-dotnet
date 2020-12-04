# AWS Lambda Container Images

This project contains the source needed to build the Docker image used in Lambda to run image-based developments. 

For .NET developments, the Docker image used to deploy those developments in Lambda is a based on an Amazon Linux base image. Lambda requires the images used to have the following folder structure:

- **/var/lang**
    - This folder should contain the .Net binaries
- **/var/runtime**
    - This folder should contain the Amazon Lambda Runtime Support libary which enables the end-user binaries to be detected and executed in Lambda
- **/var/task**
    - This folder should contain the end-user binaries, which is the code to be executed in Lambda


## Base Image
### Structure
The dockerfile(s) included in this project are used to build the image(s) used in Lambda based on the abovementioned structure. We are using a multi-stage dockerfile to install the .NET binaries, build and publish the Amazon Lambda Runtime Support libary and define an entrypoint to the end-user assemblies.

The 1st stage of the dockerfile uses a **lambda/provided:al2** image to install needed dependencies which will be copied into the **/rootfs** folder of the base image.

The 2nd stage of the dockerfile uses a **lambda/provided:al2** image to install the .NET binaries which will be copied into the **/var/lang/bin** folder. 

The 3rd stage of the dockerfile uses a **mcr.microsoft.com/dotnet/sdk** image to build the Amazon Lambda Runtime Support library and its dependencies. The Amazon Lambda Runtime Support library is responsible for locating and loading the user-code assemblies into context. The library takes in a Function Handler string which is used to locate the user's lambda function and allows the user-code to be invoked by the Lambda service. The project is built as an executable which the dotnet process will execute in order to locate the end-user binaries. The project is then published and copied to the **/var/runtime** folder of the base image.

The entrypoint of the image is a shell script that is used to find the end-user's .deps.json and .runtimeconfig.json files in the **/var/task** folder. The script then calls the dotnet process to execute the Amazon Lambda Runtime Library and pass the .deps.json and .runtimeconfig.json files that enables the detection of the end-user binaries in Lambda.

This file structure is based on https://github.com/dotnet/dotnet-docker

### Building

Run `.\build.ps1` to generate the specific `aws-lambda-dotnet:local` docker image.

## Using the Base Image

In order for you to deploy your Lambda function as an Image, you need to create a dockerfile and build on top of the `aws-lambda-dotnet:local` base image. The main purpose of this dockerfile is to put your project's assemblies inside the /var/task folder of the base image. 

We have included a sample project in the directory that contains a basic docker file. Essentially, the dockerfile is referencing the `aws-lambda-dotnet:local` as the base image.Then building the end-user's lambda project inside a `mcr.microsoft.com/dotnet/sdk` image and publishing the assemblies to the /var/task folder.