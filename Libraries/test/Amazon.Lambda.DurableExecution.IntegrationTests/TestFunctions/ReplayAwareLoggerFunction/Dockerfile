FROM public.ecr.aws/lambda/provided:al2023

RUN dnf install -y libicu

COPY bin/publish/ ${LAMBDA_TASK_ROOT}

# Emit structured JSON logs so the integration test can parse log records and
# assert the durable-execution scope keys (durableExecutionArn, operationId,
# etc.) appear as top-level fields.
ENV AWS_LAMBDA_LOG_FORMAT=JSON

ENTRYPOINT ["/var/task/bootstrap"]
