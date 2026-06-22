FROM public.ecr.aws/lambda/dotnet:10

COPY bin/publish/ ${LAMBDA_TASK_ROOT}

ENTRYPOINT ["/var/task/bootstrap"]
