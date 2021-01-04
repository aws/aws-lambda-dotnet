ARG BASE_IMAGE=aws-lambda-dotnet:local

FROM $BASE_IMAGE AS base

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim as build
COPY . /src
WORKDIR /src
RUN dotnet build "ImageFunction.csproj" -o /app/build

FROM build AS publish
RUN dotnet publish "ImageFunction.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /var/task
COPY --from=publish /app/publish .
