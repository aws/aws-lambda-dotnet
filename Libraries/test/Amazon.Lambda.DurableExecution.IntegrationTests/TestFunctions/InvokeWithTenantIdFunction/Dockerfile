FROM public.ecr.aws/lambda/provided:al2023

RUN dnf install -y libicu

COPY bin/publish/ ${LAMBDA_TASK_ROOT}

ENTRYPOINT ["/var/task/bootstrap"]
