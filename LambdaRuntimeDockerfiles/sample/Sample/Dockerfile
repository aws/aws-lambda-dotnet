ARG AWS_LAMBDA_VERSION=local

FROM aws-lambda-dotnet:$AWS_LAMBDA_VERSION AS base

FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim as build
COPY . /Sample

WORKDIR /Sample

RUN dotnet build "Sample.csproj" -o /app/build


FROM build AS publish
RUN dotnet publish "Sample.csproj" -c Release -o /app/publish


FROM base AS final
COPY --from=publish /app/publish ${LAMBDA_TASK_ROOT}
# ref. https://docs.aws.amazon.com/lambda/latest/dg/csharp-handler.html#csharp-handler-signatures
CMD [ "Sample::Sample.Function::FunctionHandler" ]
