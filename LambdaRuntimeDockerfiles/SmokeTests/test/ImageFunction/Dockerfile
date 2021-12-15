ARG BASE_IMAGE=aws-lambda-dotnet:local
ARG BUILD_IMAGE=5.0-buster-slim
ARG TARGET_FRAMEWORK=net5.0

FROM $BASE_IMAGE AS base
ARG TARGET_FRAMEWORK

FROM mcr.microsoft.com/dotnet/sdk:$BUILD_IMAGE as build
ARG TARGET_FRAMEWORK
COPY . /src
WORKDIR /src
RUN echo $TARGET_FRAMEWORK
RUN dotnet build "ImageFunction.csproj" -o /app/build /p:Framework=$TARGET_FRAMEWORK

FROM build AS publish
ARG TARGET_FRAMEWORK
RUN echo $TARGET_FRAMEWORK
RUN dotnet publish "ImageFunction.csproj" -c Release -o /app/publish /p:Framework=$TARGET_FRAMEWORK

FROM base AS final
WORKDIR /var/task
COPY --from=publish /app/publish .
