ARG AWS_LAMBDA_VERSION=local

FROM aws-lambda-dotnet:$AWS_LAMBDA_VERSION AS base

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim as build
COPY . /Sample

WORKDIR /Sample

RUN dotnet build "Sample.csproj" -o /app/build

FROM build AS publish
RUN dotnet publish "Sample.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /var/task
COPY --from=publish /app/publish .
